using System;
using System.Collections.Generic;
using System.Numerics;
using AbsoluteRP.RsUI;
using AbsoluteRP.Video;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AbsoluteRP.Social;

// Modal popup that plays a video URL via the existing LibVLC-backed. VideoPlayerSession pipeline (same one used for profile backgrounds). One live session at a time - opening a new URL disposes the previous.
public static class SocialVideoPopup
{
    private static string? _url;
    private static VideoPlayerSession? _session;
    private static bool _open;

    // Draw() stamps this each frame it runs. Plugin.Update reads it every framework tick via HeartbeatTick(); if the stamp is stale (social page not drawing -> popup Draw not being called), we tear down the session so its audio doesn't linger while the popup is invisible. Uses ticks so we don't need a frame counter of our own.
    private static long _lastDrawnTicksMs;
    private const int  StaleThresholdMs = 250;

    // Sessions swapped out this frame are held here for one full Present cycle before their D3D11 texture is released - otherwise the game's in-flight draw commands can still be pointing at the retired SRV when the driver executes them next frame, and that races NVIDIA's internal worker thread straight into an access violation.
    private static readonly System.Collections.Generic.List<VideoPlayerSession> _retired = new();

    private static void DrainRetired()
    {
        if (_retired.Count == 0) return;
        for (int i = 0; i < _retired.Count; i++)
        {
            try { _retired[i].Dispose(); }
            catch (Exception ex) { Plugin.PluginLog?.Debug("SocialVideoPopup retired dispose: " + ex.Message); }
        }
        _retired.Clear();
    }

    public static void Open(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        // Swap sessions if the user clicked a different video.
        if (_url != url)
        {
            DisposeSession();
            _url = url;
            try
            {
                _session = new VideoPlayerSession(Plugin.PluginInterface, Plugin.PluginLog, url, initialVolume: 60);
            }
            catch (Exception ex) { Plugin.PluginLog?.Debug("SocialVideoPopup start: " + ex.Message); _session = null; }
        }
        _open = true;
    }

    private static int _drawnFrame = -1;
    public static void Draw()
    {
        var frameNo = ImGui.GetFrameCount();
        if (frameNo == _drawnFrame) return;   // drawn already this frame
        _drawnFrame = frameNo;
        _lastDrawnTicksMs = System.Environment.TickCount64;
        // Drain last frame's retirees FIRST so the driver has had a full. Present cycle to consume any queued draws against them.
        DrainRetired();
        if (!_open) return;
        ImGui.OpenPopup("##arp_social_video");
        var view = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(view.WorkPos + view.WorkSize * 0.5f, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(RsTheme.S(720f), RsTheme.S(480f)), ImGuiCond.Appearing);

        if (ImGui.BeginPopupModal("##arp_social_video", ref _open,
                                  ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize))
        {
            var s = _session;
            if (s == null || s.RendererCrashed)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentDanger);
                ImGui.TextWrapped("Couldn't start video playback for this URL.");
                ImGui.PopStyleColor();
            }
            else
            {
                var avail = ImGui.GetContentRegionAvail();
                // Reserve two rows at the bottom: seek + controls.
                var controlsH = RsTheme.S(72f);
                var stageH    = MathF.Max(RsTheme.S(120f), avail.Y - controlsH);
                var stageW    = avail.X;

                var texHandle = default(Dalamud.Bindings.ImGui.ImTextureID);
                int vw = 0, vh = 0;
                var vt = s.CurrentTextureId;
                if (vt != IntPtr.Zero)
                {
                    texHandle = new Dalamud.Bindings.ImGui.ImTextureID(vt);
                    vw = s.Width;
                    vh = s.Height;
                }

                var drawList = ImGui.GetWindowDrawList();
                var start = ImGui.GetCursorScreenPos();
                var end   = start + new Vector2(stageW, stageH);

                // Solid black stage so letterbox bars read cleanly.
                drawList.AddRectFilled(start, end, ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 1f)));

                if (vw > 0 && vh > 0)
                {
                    var scale = MathF.Min(stageW / vw, stageH / vh);
                    var drawW = vw * scale;
                    var drawH = vh * scale;
                    var offX  = (stageW - drawW) * 0.5f;
                    var offY  = (stageH - drawH) * 0.5f;
                    var min   = new Vector2(start.X + offX, start.Y + offY);
                    var max   = new Vector2(min.X + drawW,  min.Y + drawH);
                    drawList.AddImage(texHandle, min, max);
                }

                // Click the stage -> toggle play/pause. Standard video- player affordance and the space is already there.
                ImGui.SetCursorScreenPos(start);
                if (ImGui.InvisibleButton("##stage_toggle", new Vector2(stageW, stageH)))
                    s.TogglePlayPause();

                ImGui.Spacing();
                DrawControls(s);
            }
            ImGui.EndPopup();
        }

        // If the modal was dismissed by clicking outside or Esc, cull the session so the LibVLC subprocess doesn't linger.
        if (!_open) DisposeSession();
    }

    // Bottom control strip: seek bar on top, play/pause + volume + time + Close on the row below. Values are pulled from the session's renderer-pushed telemetry so the seek bar stays synced.
    private static float _volumeUi = 60f;
    private static void DrawControls(AbsoluteRP.Video.VideoPlayerSession s)
    {
        var dur = Math.Max(0L, s.DurationMs);
        var pos = Math.Clamp(s.PositionMs, 0L, dur);

        // Seek bar. ImGui.SliderScalar for a long - cleanest with an int in seconds since millisecond precision isn't useful for the UI.
        var totalSec  = (int)(dur / 1000L);
        var curSec    = (int)(pos / 1000L);
        ImGui.SetNextItemWidth(-1f);
        int newCur = curSec;
        if (ImGui.SliderInt("##vid_seek", ref newCur, 0, Math.Max(1, totalSec),
                            FormatSeekLabel(curSec, totalSec), ImGuiSliderFlags.NoInput))
        {
            s.SeekMs((long)newCur * 1000L);
        }

        // Row: play/pause icon | volume | Close (right-aligned).
        var playIcon = s.IsPaused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause;
        var playStr  = playIcon.ToIconString();
        Vector2 playSz;
        using (AbsoluteRP.RsUI.RsIcons.Push()) playSz = ImGui.CalcTextSize(playStr);
        var buttonPad = RsTheme.S(10f);
        var buttonSz  = new Vector2(playSz.X + buttonPad * 2f, playSz.Y + buttonPad);
        var start     = ImGui.GetCursorScreenPos();
        if (ImGui.InvisibleButton("##vid_toggle", buttonSz)) s.TogglePlayPause();
        var dl   = ImGui.GetWindowDrawList();
        var glyphPos = new Vector2(start.X + buttonPad, start.Y + (buttonSz.Y - playSz.Y) * 0.5f);
        using (AbsoluteRP.RsUI.RsIcons.Push())
            dl.AddText(glyphPos, ImGui.ColorConvertFloat4ToU32(RsTheme.TextPrimary), playStr);

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        var volIcon = FontAwesomeIcon.VolumeUp.ToIconString();
        using (AbsoluteRP.RsUI.RsIcons.Push()) ImGui.TextUnformatted(volIcon);
        ImGui.PopStyleColor();
        ImGui.SameLine();
        // Sync UI volume from the session when the user hasn't dragged this frame - SetVolume also mirrors into s.Volume so the two stay linked without an extra state store.
        if (Math.Abs(_volumeUi - s.Volume) > 0.5f) _volumeUi = s.Volume;
        ImGui.SetNextItemWidth(RsTheme.S(140f));
        if (ImGui.SliderFloat("##vid_vol", ref _volumeUi, 0f, 100f, "%.0f"))
            s.SetVolume((int)_volumeUi);

        ImGui.SameLine();
        var closeW = ImGui.CalcTextSize("Close").X + RsTheme.S(36f);
        var availX = ImGui.GetContentRegionAvail().X;
        var offX   = availX - closeW;
        if (offX > 0f) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offX);
        if (RsElements.Button("Close", RsElements.ButtonVariant.Ghost))
        {
            _open = false;
            DisposeSession();
            ImGui.CloseCurrentPopup();
        }
    }

    private static string FormatSeekLabel(int curSec, int totalSec)
        => totalSec > 0 ? $"{FormatSec(curSec)} / {FormatSec(totalSec)}"
                        : FormatSec(curSec);

    private static string FormatSec(int sec)
    {
        if (sec < 0) sec = 0;
        var m = sec / 60;
        var s = sec % 60;
        var h = m  / 60;
        m     = m  % 60;
        return h > 0 ? $"{h}:{m:D2}:{s:D2}" : $"{m}:{s:D2}";
    }

    // Called every framework tick from Plugin.Update. If Draw hasn't run recently, retire the session so it doesn't keep decoding audio while the popup is invisible.
    public static void HeartbeatTick()
    {
        if (_session == null && !_open) return;
        var now = System.Environment.TickCount64;
        if (_lastDrawnTicksMs == 0) { _lastDrawnTicksMs = now; return; }
        if (now - _lastDrawnTicksMs > StaleThresholdMs)
        {
            _open = false;
            DisposeSession();
        }
    }

    private static void DisposeSession()
    {
        // Retire, don't destroy - let DrainRetired free it next frame.
        if (_session != null) _retired.Add(_session);
        _session = null;
        _url     = null;
    }
}
