using System;
using System.Linq;
using AbsoluteRP.Video.Common;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace AbsoluteRP.Video;

public sealed class VideoPlayerSession : IDisposable
{
    // global cap. Each session is a renderer subprocess; more than a few at once eats. CPU and memory. When a new one starts past the cap, the OLDEST live one is frozen (its last frame is kept as a still, the subprocess goes away). Frozen or disposed sessions don't count.
    public const int MaxActive = 3;
    private static readonly System.Collections.Generic.List<VideoPlayerSession> _live = new();
    private static readonly object _liveLock = new();
    public static int ActiveCount { get { lock (_liveLock) return _live.Count(s => !s.disposed && !s.frozen); } }

    private static void Register(VideoPlayerSession s)
    {
        System.Collections.Generic.List<VideoPlayerSession> victims = new();
        lock (_liveLock)
        {
            _live.RemoveAll(x => x.disposed);
            _live.Add(s);
            var active = _live.Where(x => !x.frozen && !x.disposed).ToList();
            for (int i = 0; active.Count - i > MaxActive && i < active.Count - 1; i++) victims.Add(active[i]);
        }
        // Freeze needs a decoded frame; ones that haven't got one yet are retried by EnforceCap on later frames.
        foreach (var v in victims) { try { v.Freeze(); } catch { } }
    }

    // Called from the render loop by anyone drawing a session, so cap enforcement catches up once early frames arrive.
    public static void EnforceCap()
    {
        System.Collections.Generic.List<VideoPlayerSession> victims = new();
        lock (_liveLock)
        {
            _live.RemoveAll(x => x.disposed);
            var active = _live.Where(x => !x.frozen && !x.disposed).ToList();
            for (int i = 0; active.Count - i > MaxActive && i < active.Count - 1; i++) victims.Add(active[i]);
        }
        foreach (var v in victims) { try { v.Freeze(); } catch { } }
    }

    private readonly IDalamudPluginInterface pi;
    private readonly IPluginLog log;
    private readonly RpcHost rpc;
    private readonly RendererProcess proc;
    private readonly string url;
    private MmfFrameReader? texture;
    private int currentWidth;
    private int currentHeight;
    private bool disposed;

    public string Url => url;
    // Frozen sessions have intentionally shut down their subprocess after capturing a still frame, so !proc.IsRunning is expected there and must not be treated as a crash by callers polling this flag.
    public bool RendererCrashed => !frozen && (proc.LaunchFailed || (!proc.IsRunning && !rpc.Connected));
    public string? RendererError => proc.LaunchError;
    public bool TextureReady => texture is { Ready: true };
    public int Width => currentWidth;
    public int Height => currentHeight;
    public bool IsFrozen => frozen;
    private bool frozen;

    // Transport telemetry pushed by the renderer every ~250ms.
    public long PositionMs { get; private set; }
    public long DurationMs { get; private set; }
    public bool IsPaused   { get; private set; }
    public int  Volume     { get; private set; } = 60;

    // When true, the session auto-pauses as soon as its first frame is decoded - used by the social-feed thumbnail cache so it holds just the poster instead of continuously playing muted in the background.
    public bool PauseAfterFirstFrame { get; set; }
    private bool _autoPauseFired;

    // For backdrop rendering: applies the latest decoded frame and returns the SRV pointer. IntPtr.Zero when nothing is ready. Must be called on the framework thread (during Draw).
    public IntPtr CurrentTextureId
    {
        get
        {
            // Sessions that were over the cap before their first frame arrived get frozen here, once a frame exists.
            if (!frozen && ActiveCount > MaxActive) EnforceCap();
            var t = texture;
            if (t == null || t.Failed) return IntPtr.Zero;
            try { t.ApplyLatestFrame(); } catch { return IntPtr.Zero; }
            return t.TextureId;
        }
    }

    public VideoPlayerSession(
        IDalamudPluginInterface pi,
        IPluginLog log,
        string watchUrl,
        int initialVolume = 100,
        int width = 1280,
        int height = 720)
    {
        this.pi = pi;
        this.log = log;
        this.url = watchUrl;
        currentWidth = width;
        currentHeight = height;

        rpc = new RpcHost(log);
        rpc.FrameReady += OnFrameReady;
        rpc.Log += OnRendererLog;
        rpc.Disconnected += OnRendererDisconnected;
        rpc.TimeUpdate += OnTimeUpdate;
        Volume = initialVolume;
        rpc.Start();
        Register(this);

        proc = new RendererProcess(
            pi, log, rpc.PipeName,
            DxHandler.AdapterLuidLow, DxHandler.AdapterLuidHigh,
            width, height, watchUrl, initialVolume);
        // Subscribe BEFORE Start() so we don't miss an immediate exit
        proc.Exited += OnRendererExited;
        proc.Start();
    }

    private void OnRendererExited()
    {
        var t = texture;
        if (t == null) return;
        try { t.MarkFailed(); } catch { }
        // Runs on the RPC/process thread: never free the SRV here, the draw thread may be mid-frame with it in a draw list. Defer.
        AbsoluteRP.Helpers.TextureGraveyard.Enqueue(t);
        texture = null;
    }

    private long frameReadyCount;
    private void OnFrameReady(FrameReady f)
    {
        try
        {
            var t = texture;
            var hadReader = t != null && !t.Failed;
            // First frame, or renderer reconnect
            if (t == null || t.Failed)
            {
                // Dispose the old failed reader before replacing it - the. MMF handle and (game-device) D3D11 textures it owns would otherwise leak on every renderer reconnect and eventually destabilize the driver.
                var old = t;
                if (old != null)
                {
                    AbsoluteRP.Helpers.TextureGraveyard.Enqueue(old);   // may have been drawn this frame
                }
                texture = new MmfFrameReader(f.MmfName, f.MmfSize);
                t = texture;
                log.Information(
                    "[AbsoluteRP] OnFrameReady: opened MmfFrameReader name={Name} size={Size} failed={Failed} dxInit={Dx}",
                    f.MmfName, f.MmfSize, t.Failed, AbsoluteRP.Video.DxHandler.Initialized);
            }
            currentWidth = f.Width;
            currentHeight = f.Height;
            t.Update(f.Width, f.Height, f.FrameCounter);

            // Poster-thumbnail mode: once we've captured the first frame, pause the renderer so the shared texture holds still and the subprocess stops decoding audio + video.
            if (PauseAfterFirstFrame && !_autoPauseFired)
            {
                _autoPauseFired = true;
                try { SetPaused(true); } catch { }
            }
            var n = System.Threading.Interlocked.Increment(ref frameReadyCount);
            if (n == 1 || n % 100 == 0 || (t.Failed && n <= 5))
                log.Information(
                    "[AbsoluteRP] OnFrameReady #{N} {W}x{H} ctr={Ctr} ready={Ready} failed={Failed} reason={Reason}",
                    n, f.Width, f.Height, f.FrameCounter, t.Ready, t.Failed, t.FailureReason ?? "(none)");
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[AbsoluteRP] OnFrameReady failed");
        }
    }

    private void OnRendererLog(LogLine line)
    {
        // Mirror renderer-side logs into the Dalamud log.
        if (line.Level == "error") log.Warning("[AbsoluteRP renderer] {L}", line.Text);
        else log.Debug("[AbsoluteRP renderer] {L}", line.Text);
    }

    private void OnRendererDisconnected()
    {
        log.Debug("[AbsoluteRP] renderer pipe disconnected");
    }
    public void Render(System.Numerics.Vector2 size)
    {
        // Drop the texture if the renderer process is gone
        var rendererGone = !proc.IsRunning || !rpc.Connected;
        if (rendererGone && texture != null)
        {
            log.Debug("[AbsoluteRP] renderer gone; releasing shared texture before next render");
            AbsoluteRP.Helpers.TextureGraveyard.Enqueue(texture);   // drawn last frame / earlier this frame
            texture = null;
        }

        var t = texture;
        if (t == null || t.Failed)
        {
            Dalamud.Bindings.ImGui.ImGui.Dummy(size);
            return;
        }

        try
        {
            t.Render(size);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[AbsoluteRP] shared texture render failed; marking dead");
            try { t.MarkFailed(); } catch { }
            AbsoluteRP.Helpers.TextureGraveyard.Enqueue(t);
            texture = null;
            Dalamud.Bindings.ImGui.ImGui.Dummy(size);
        }
    }
    // mostly unused at the moment
    public void MouseMove(int x, int y)
        => rpc.Send(RpcOp.MouseMove, new MouseMove { X = x, Y = y });

    public void MouseButton(int x, int y, int button, bool down)
        => rpc.Send(RpcOp.MouseButton, new MouseButton { X = x, Y = y, Button = button, Down = down });

    public void MouseWheel(int x, int y, int deltaY)
        => rpc.Send(RpcOp.MouseWheel, new MouseWheel { X = x, Y = y, DeltaY = deltaY });

    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        if (width == currentWidth && height == currentHeight) return;
        currentWidth = width;
        currentHeight = height;
        rpc.Send(RpcOp.Resize, new Resize { Width = width, Height = height });
    }

    public void Navigate(string newUrl)
        => rpc.Send(RpcOp.Navigate, new Navigate { Url = newUrl });

    // Plugin renderer, change the libVLC playback volume.
    public void SetVolume(int volume)
    {
        if (disposed) return;
        var v = Math.Clamp(volume, 0, 100);
        Volume = v;
        try { rpc.Send(RpcOp.SetVolume, new SetVolume { Volume = v }); }
        catch (Exception ex) { log.Debug("[AbsoluteRP] SetVolume failed: {E}", ex.Message); }
    }

    // Transport controls - thin wrappers over the RPC. The renderer's. TimeUpdate loop keeps IsPaused / PositionMs / DurationMs current so callers can drive a seek bar without polling.
    public void SetPaused(bool paused)
    {
        if (disposed) return;
        IsPaused = paused;  // optimistic; next TimeUpdate confirms
        try { rpc.Send(RpcOp.SetPaused, new SetPaused { Paused = paused }); }
        catch (Exception ex) { log.Debug("[AbsoluteRP] SetPaused failed: {E}", ex.Message); }
    }

    public void TogglePlayPause() => SetPaused(!IsPaused);

    // Poster-still mode: kill the subprocess + RPC channel after we have a confirmed SRV, keeping the D3D11 texture alive on the game's device. Returns true iff we successfully captured a frame and the subsequent CurrentTextureId calls will return that frozen SRV. Once frozen, the session no longer decodes anything (zero CPU, zero subprocess) and cannot be resumed - the caller must dispose it and spin up a fresh VideoPlayerSession for playback (this is what the social feed does when the user clicks the thumbnail to play).
    public bool Freeze()
    {
        if (disposed || frozen) return frozen;
        var t = texture;
        if (t == null || t.Failed) return false;
        // Force-apply any pending frame so the SRV is populated before we pull the MMF out from under it. If no frame has arrived yet the. SRV will still be null and we bail - caller retries next frame.
        try { t.ApplyLatestFrame(); } catch { return false; }
        if (t.TextureId == IntPtr.Zero) return false;

        frozen = true;
        // Tear down the subprocess + pipe. rpc.Disconnected fires and its handler calls OnRendererExited -> t.MarkFailed() -> t.Dispose(), wiping the SRV we just captured. Suppress that path by nulling `texture` on this side FIRST, then closing the MMF ourselves without touching the D3D11 resources.
        var captured = t;
        texture = null; // hide from OnRendererExited
        try { rpc.Send(RpcOp.Shutdown, new AbsoluteRP.Video.Common.Shutdown()); } catch { }
        try { proc.Dispose(); } catch { }
        try { rpc.Dispose(); } catch { }
        try { captured.CloseMmfKeepTextures(); } catch { }
        // Restore the field so CurrentTextureId keeps serving the frozen SRV.
        texture = captured;
        return true;
    }

    // Turn off poster-thumbnail auto-pause so a subsequent resume sticks (otherwise the next FrameReady would immediately re-pause us). Also fires an explicit Play in case we were the paused party.
    public void ResumeInline(int volume)
    {
        PauseAfterFirstFrame = false;
        _autoPauseFired      = true;   // don't re-arm
        SetVolume(volume);
        SetPaused(false);
    }

    public void SeekMs(long ms)
    {
        if (disposed) return;
        if (ms < 0) ms = 0;
        try { rpc.Send(RpcOp.Seek, new AbsoluteRP.Video.Common.Seek { Ms = ms }); }
        catch (Exception ex) { log.Debug("[AbsoluteRP] Seek failed: {E}", ex.Message); }
    }

    private void OnTimeUpdate(TimeUpdate t)
    {
        PositionMs = t.PositionMs;
        DurationMs = t.DurationMs;
        IsPaused   = t.Paused;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lock (_liveLock) _live.Remove(this);

        // Fast path: things that MUST run on the framework/render thread (D3D11 resource release) happen synchronously and are cheap.
        var tex = texture;
        texture = null;
        AbsoluteRP.Helpers.TextureGraveyard.Enqueue(tex);   // the SRV may be in this frame's draw list

        // Slow path: subprocess wait-for-exit + job-handle close + pipe-server shutdown historically blocked the framework thread for up to 2 seconds each, so dumping 8 posters on a tab switch stalled the game for double-digit seconds. Offload to the thread pool - the kernel job object makes the process-side teardown safe from any thread and the RPC pipe cleanup is pure managed I/O.
        var localRpc  = rpc;
        var localProc = proc;
        System.Threading.Tasks.Task.Run(() =>
        {
            try { localRpc.Send(RpcOp.SetVolume, new SetVolume { Volume = 0 }); } catch { }
            try { localRpc.Send(RpcOp.Shutdown, new Shutdown()); } catch { }
            try { localProc.Dispose(); } catch { }
            try { localRpc.Dispose(); } catch { }
        });
    }
}
