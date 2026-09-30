using System;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AbsoluteRP.Video.Common;
using LibVLCSharp.Shared;

namespace AbsoluteRP.Video.Renderer;

internal sealed class RendererHost : IDisposable
{
    private readonly RendererArgs args;
    private readonly CancellationTokenSource cts = new();
    private readonly MmfFrameWriter frameWriter;
    private NamedPipeClientStream? pipe;
    private LibVLC? libvlc;
    private MediaPlayer? player;
    private EventWaitHandle? keepAlive;
    private Thread? keepAliveThread;

    private readonly SemaphoreSlim writeGate = new(1, 1);


    // underneath the C++ side.
    private IntPtr videoScratch;
    private long videoScratchBytes;
    private int videoWidth;
    private int videoHeight;

    private MediaPlayer.LibVLCVideoLockCb? lockCb;
    private MediaPlayer.LibVLCVideoUnlockCb? unlockCb;
    private MediaPlayer.LibVLCVideoDisplayCb? displayCb;
    private MediaPlayer.LibVLCVideoFormatCb? formatCb;
    private MediaPlayer.LibVLCVideoCleanupCb? formatCleanupCb;

    private int lastVolume = 100;

    public RendererHost(RendererArgs args)
    {
        this.args = args;
        frameWriter = new MmfFrameWriter();
        lastVolume = args.InitialVolume;
    }

    public void Run()
    {
        frameWriter.Initialize();


        if (!string.IsNullOrEmpty(args.KeepAliveEvent))
        {
            try { keepAlive = EventWaitHandle.OpenExisting(args.KeepAliveEvent); }
            catch { keepAlive = null; }
            if (keepAlive != null)
            {
                keepAliveThread = new Thread(() =>
                {
                    try { keepAlive.WaitOne(); } catch { }
                    cts.Cancel();
                }) { IsBackground = true, Name = "AbsoluteRP.KeepAlive" };
                keepAliveThread.Start();
            }
        }

        pipe = new NamedPipeClientStream(".", args.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try { pipe.Connect(5000); }
        catch (TimeoutException)
        {
            Console.Error.WriteLine("renderer: pipe connect timeout");
            return;
        }

        try
        {
            Core.Initialize();
            Console.Error.WriteLine("renderer: libVLC Core.Initialize OK");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("renderer: libVLC Core.Initialize failed: " + ex);
            return;
        }

        // Leave --aout unset so LibVLC picks the best available module
        // (mmdevice/wasapi/directsound in that order on Windows). Forcing
        // one specific module was causing silent playback when that
        // module happened to fail to initialize.
        libvlc = new LibVLC(
            "--no-video-title-show",
            "--network-caching=300",
            "--quiet"
        );

        SetVideoBufferSize(args.Width, args.Height);

        player = new MediaPlayer(libvlc);
        // Volume set before the audio output has been created
        player.Playing += (_, __) =>
        {
            try { player.Volume = lastVolume; } catch { }
            try { if (player.Mute) player.Mute = false; } catch { }
        };
        lockCb = OnVideoLock;
        unlockCb = OnVideoUnlock;
        displayCb = OnVideoDisplay;
        formatCb = OnVideoFormat;
        formatCleanupCb = OnVideoFormatCleanup;
        player.SetVideoCallbacks(lockCb, unlockCb, displayCb);
        // Adopt the source video's native resolution so aspect ratio is preserved
        player.SetVideoFormatCallbacks(formatCb, formatCleanupCb);
        Console.Error.WriteLine("renderer: video callbacks + native-format callback registered (BGRA)");

        try
        {
            var media = new Media(libvlc, args.Url, FromType.FromLocation);
            // Loop the media indefinitely — 65535 repeats ≈ "forever"
            try { media.AddOption(":input-repeat=65535"); } catch { }
            player.Play(media);
            try { player.Volume = lastVolume; } catch { }
            Console.Error.WriteLine("renderer: libVLC MediaPlayer.Play(" + args.Url + "), volume=" + lastVolume + ", loop=1");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("renderer: MediaPlayer.Play failed: " + ex);
            return;
        }

        try
        {
            SendAsync(RpcOp.Hello, new HelloRequest { Pid = Environment.ProcessId }).GetAwaiter().GetResult();
            // Kick off the position-heartbeat pump; runs alongside the read loop until cancellation.
            _ = TimeUpdateLoopAsync(cts.Token);
            ReadLoopAsync(cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.Error.WriteLine("renderer loop error: " + ex);
        }
    }

    // Emits a TimeUpdate every ~250 ms so the client seek bar stays insync without having to poll the RPC channel.
    private async Task TimeUpdateLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(250, ct).ConfigureAwait(false);
                var mp = player;
                if (mp == null) continue;
                long pos = 0, dur = 0;
                bool paused = false;
                try { pos    = mp.Time;      } catch { }
                try { dur    = mp.Length;    } catch { }
                try { paused = !mp.IsPlaying; } catch { }
                try
                {
                    await SendAsync(RpcOp.TimeUpdate, new TimeUpdate
                    {
                        PositionMs = pos,
                        DurationMs = dur,
                        Paused     = paused,
                    }).ConfigureAwait(false);
                }
                catch { /* pipe close is handled by the read loop */ }
            }
        }
        catch (OperationCanceledException) { }
    }

    private void SetVideoBufferSize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        var newBytes = (long)width * height * 4L;
        if (videoScratch != IntPtr.Zero && newBytes == videoScratchBytes)
        {
            videoWidth = width;
            videoHeight = height;
            return;
        }
        if (videoScratch != IntPtr.Zero)
        {
            try { Marshal.FreeHGlobal(videoScratch); } catch { }
            videoScratch = IntPtr.Zero;
        }
        videoScratch = Marshal.AllocHGlobal((nint)newBytes);
        videoScratchBytes = newBytes;
        videoWidth = width;
        videoHeight = height;
    }

    // Called by libVLC once when it needs the picture buffer format for the current media.
    private uint OnVideoFormat(
        ref IntPtr opaque,
        IntPtr chroma,
        ref uint width,
        ref uint height,
        ref uint pitches,
        ref uint lines)
    {
        try
        {
            unsafe
            {
                var c = (byte*)chroma;
                c[0] = (byte)'B'; c[1] = (byte)'G'; c[2] = (byte)'R'; c[3] = (byte)'A';
            }

            var w = (int)width;
            var h = (int)height;
            if (w <= 0 || h <= 0) { w = args.Width; h = args.Height; }

            // Cap to what our shared-memory buffer can hold, preserving aspect.
            const int maxW = MmfFrameWriter.MaxWidth;
            const int maxH = MmfFrameWriter.MaxHeight;
            if (w > maxW || h > maxH)
            {
                var s = Math.Min((double)maxW / w, (double)maxH / h);
                w = Math.Max(2, (int)(w * s));
                h = Math.Max(2, (int)(h * s));
            }

            // Even dimensions keep libVLC's decoders happy — most chromas
            // insist on it. Round up so we never lose pixels.
            w = (w + 1) & ~1;
            h = (h + 1) & ~1;

            width   = (uint)w;
            height  = (uint)h;
            pitches = (uint)(w * 4);
            lines   = (uint)h;

            SetVideoBufferSize(w, h);
            Console.Error.WriteLine($"renderer: format cb -> {w}x{h} BGRA");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("renderer: OnVideoFormat threw: " + ex.Message);
            // 0 = failure — libVLC will fall back to default handling.
            return 0;
        }
    }

    private void OnVideoFormatCleanup(ref IntPtr opaque) { }

    private long lockCallCount;
    private IntPtr OnVideoLock(IntPtr opaque, IntPtr planes)
    {
        unsafe
        {
            *(IntPtr*)planes = videoScratch;
        }
        var n = System.Threading.Interlocked.Increment(ref lockCallCount);
        if (n == 1 || n % 100 == 0)
            Console.Error.WriteLine($"renderer: video Lock #{n} (scratch=0x{videoScratch.ToInt64():X})");
        return videoScratch;
    }

    private void OnVideoUnlock(IntPtr opaque, IntPtr picture, IntPtr planes) { }

    private long displayCallCount;
    private void OnVideoDisplay(IntPtr opaque, IntPtr picture)
    {
        if (videoScratch == IntPtr.Zero) return;
        var wrote = frameWriter.Write(videoScratch, videoWidth, videoHeight);
        var n = System.Threading.Interlocked.Increment(ref displayCallCount);
        if (n == 1 || n % 100 == 0)
            Console.Error.WriteLine($"renderer: video Display #{n} (mmf write={wrote}, {videoWidth}x{videoHeight})");
        if (wrote)
        {
            try
            {
                SendAsync(RpcOp.FrameReady, new FrameReady
                {
                    MmfName = frameWriter.MmfName,
                    MmfSize = frameWriter.Capacity,
                    Width = videoWidth,
                    Height = videoHeight,
                    FrameCounter = frameWriter.FrameCounter,
                }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("send frame_ready failed: " + ex.Message);
            }
        }
    }

    private async Task SendAsync<T>(string op, T payload)
    {
        var p = pipe;
        if (p == null || !p.IsConnected) return;
        var env = RpcWire.Make(op, payload);
        try { await writeGate.WaitAsync(cts.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }
        catch (ObjectDisposedException) { return; }
        try
        {
            await RpcWire.WriteAsync(p, env, cts.Token).ConfigureAwait(false);
        }
        finally
        {
            try { writeGate.Release(); } catch { }
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var p = pipe!;
        while (!ct.IsCancellationRequested && p.IsConnected)
        {
            var msg = await RpcWire.ReadAsync(p, ct).ConfigureAwait(false);
            if (msg == null) break;
            Dispatch(msg);
        }
    }

    private void Dispatch(RpcMessage msg)
    {
        try
        {
            switch (msg.Op)
            {
                case RpcOp.MouseMove:
                case RpcOp.MouseButton:
                case RpcOp.MouseWheel:
                    break;

                case RpcOp.Resize:
                {
                    var m = RpcWire.Decode<Resize>(msg);
                    if (m == null || m.Width <= 0 || m.Height <= 0) return;
                    break;
                }
                case RpcOp.Navigate:
                {
                    var m = RpcWire.Decode<Navigate>(msg);
                    if (m == null || string.IsNullOrEmpty(m.Url) || libvlc == null || player == null) return;
                    try
                    {
                        player.Stop();
                        var media = new Media(libvlc, m.Url, FromType.FromLocation);
                        try { media.AddOption(":input-repeat=65535"); } catch { }
                        player.Play(media);
                        try { player.Volume = lastVolume; } catch { }
                        Console.Error.WriteLine("renderer: switched to " + m.Url + ", volume=" + lastVolume + ", loop=1");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("renderer: navigate failed: " + ex.Message);
                    }
                    break;
                }
                case RpcOp.SetVolume:
                {
                    var m = RpcWire.Decode<SetVolume>(msg);
                    if (m == null || player == null) return;
                    var v = Math.Clamp(m.Volume, 0, 100);
                    try
                    {
                        player.Volume = v;
                        lastVolume = v;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("renderer: set_volume failed: " + ex.Message);
                    }
                    break;
                }
                case RpcOp.SetPaused:
                {
                    var m = RpcWire.Decode<SetPaused>(msg);
                    if (m == null || player == null) return;
                    try
                    {
                        if (m.Paused) player.Pause();
                        else          player.Play();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("renderer: set_paused failed: " + ex.Message);
                    }
                    break;
                }
                case RpcOp.Seek:
                {
                    var m = RpcWire.Decode<Seek>(msg);
                    if (m == null || player == null) return;
                    try
                    {
                        // LibVLC seek is a Time set in milliseconds.
                        if (player.IsSeekable) player.Time = m.Ms;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("renderer: seek failed: " + ex.Message);
                    }
                    break;
                }
                case RpcOp.Shutdown:
                    cts.Cancel();
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("dispatch error op=" + msg.Op + ": " + ex.Message);
        }
    }

    public void Dispose()
    {
        try { cts.Cancel(); } catch { }
        try { player?.Stop(); } catch { }
        try { player?.Dispose(); } catch { }
        try { libvlc?.Dispose(); } catch { }
        try { pipe?.Dispose(); } catch { }
        try { frameWriter.Dispose(); } catch { }
        try { keepAlive?.Dispose(); } catch { }
        if (videoScratch != IntPtr.Zero)
        {
            try { Marshal.FreeHGlobal(videoScratch); } catch { }
            videoScratch = IntPtr.Zero;
        }
    }
}
