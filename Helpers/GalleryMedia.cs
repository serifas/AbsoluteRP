using System;
using System.IO;
using System.Linq;
using System.Numerics;
using AbsoluteRP.RsUI;
using AbsoluteRP.Social;
using Dalamud.Bindings.ImGui;

namespace AbsoluteRP.Helpers
{
    public static class GalleryMedia
    {
        public static readonly string[] VideoExts = { ".mp4", ".webm", ".mov", ".mkv", ".m4v" };
        public static readonly string[] AudioExts = { ".mp3", ".ogg", ".wav", ".flac", ".m4a" };
        public const long MaxVideoBytes = 100L * 1024 * 1024;
        public const long MaxAudioBytes = 30L * 1024 * 1024;

        // 0 = image (or nothing), 1 = video, 2 = audio.
        public static int KindOf(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return 0;
            var u = url.ToLowerInvariant();
            var q = u.IndexOfAny(new[] { '?', '#' });
            if (q >= 0) u = u[..q];
            if (VideoExts.Any(e => u.EndsWith(e))) return 1;
            if (AudioExts.Any(e => u.EndsWith(e))) return 2;
            return 0;
        }
        public static bool IsMedia(string? url) => KindOf(url) != 0;

        // A video entry can carry a cover picture: "video.mp4#poster=<image url>".
        public static string BaseUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return string.Empty;
            var i = url.IndexOf("#poster=", StringComparison.OrdinalIgnoreCase);
            return i >= 0 ? url[..i] : url;
        }
        public static string PosterOf(string? url)
        {
            if (string.IsNullOrEmpty(url)) return string.Empty;
            var i = url.IndexOf("#poster=", StringComparison.OrdinalIgnoreCase);
            return i >= 0 ? url[(i + 8)..] : string.Empty;
        }
        public static string WithPoster(string url, string poster)
            => string.IsNullOrEmpty(poster) ? BaseUrl(url) : BaseUrl(url) + "#poster=" + poster;

        // Pick an image (with the crop step), upload it, and hand back its url.
        public static void PickCover(Action<string> onUrl)
        {
            RsFileDialog.OpenImagePicker("Choose a cover picture for the video", (ok, path) =>
            {
                if (!ok || string.IsNullOrEmpty(path) || !File.Exists(path)) return;
                try
                {
                    if (Uploading) { UploadStatus = "Another upload is still running."; return; }
                    var bytes = File.ReadAllBytes(path);
                    // Covers are shown small: keep them light.
                    try { bytes = Imaging.ScaleImageBytes(bytes, 1280, 1280); } catch { }
                    Uploading = true; UploadStatus = "Uploading cover…";
                    SocialFeed.PendingUploadCallback = (kind, url, error) =>
                    {
                        Uploading = false;
                        if (string.IsNullOrEmpty(url)) { UploadStatus = "Cover upload failed: " + (string.IsNullOrEmpty(error) ? "unknown error" : error); return; }
                        UploadStatus = "Cover set. Save the profile to keep it.";
                        try { onUrl(url); } catch { }
                    };
                    _ = SocialFeed.UploadMedia(SocialFeed.MediaKind.Image, "png", bytes);
                }
                catch (Exception ex) { Uploading = false; UploadStatus = "Could not read that picture: " + ex.Message; }
            });
        }

        // playback
        private static string _audioUrl = string.Empty;
        private static string _audioTitle = string.Empty;
        private static bool _audioOpen;

        public static void Play(string url, string? title = null)
        {
            url = BaseUrl(url);
            switch (KindOf(url))
            {
                case 1: SocialVideoPopup.Open(url); break;
                case 2:
                    if (_audioUrl != url) { try { Misc.PauseAllAudio(); } catch { } }
                    _audioUrl = url; _audioTitle = title ?? string.Empty; _audioOpen = true;
                    break;
            }
        }

        // Once per frame from the plugin's draw loop.
        public static void DrawPopups()
        {
            try { SocialVideoPopup.Draw(); } catch (Exception ex) { Plugin.PluginLog.Debug("GalleryMedia video popup: " + ex.Message); }
            if (!_audioOpen) return;
            ImGui.SetNextWindowSize(new Vector2(RsTheme.S(420f), 0f), ImGuiCond.Always);
            var shown = ImGui.Begin("Audio###arp_gallery_audio", ref _audioOpen, ImGuiWindowFlags.NoCollapse);
            try
            {
                if (shown)
                {
                    if (!string.IsNullOrWhiteSpace(_audioTitle))
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextSecondary);
                        ImGui.TextWrapped(_audioTitle);
                        ImGui.PopStyleColor();
                    }
                    Misc.RenderAudioEmbed(_audioUrl, RsTheme.S(390f));
                }
            }
            finally { ImGui.End(); }
            if (!_audioOpen) { try { Misc.PauseAllAudio(); } catch { } }
        }

        // tile. A dark plate with a play / music glyph. Returns true when clicked.
        public static bool DrawTile(string url, Vector2 size, string? tooltip)
        {
            int kind = KindOf(url);
            var pos = ImGui.GetCursorScreenPos();
            var clicked = ImGui.InvisibleButton("##gmedia_" + url.GetHashCode() + "_" + (int)pos.X + "_" + (int)pos.Y, size);
            var hov = ImGui.IsItemHovered();
            var dl = ImGui.GetWindowDrawList();
            var r = RsTheme.S(6f);
            dl.AddRectFilled(pos, pos + size, ImGui.GetColorU32(RsTheme.BgTertiary), r);
            var accent = kind == 1 ? RsTheme.AccentPrimary : RsTheme.AccentWarning;
            // Cover picture, cropped to fill the tile, dimmed so the play button reads.
            var poster = PosterOf(url);
            var ptex = string.IsNullOrEmpty(poster) ? null : SocialMediaCache.Get(poster);
            bool drewPicture = false;
            if (ptex != null && ptex.Width > 0 && ptex.Height > 0)
            {
                var ta = ptex.Width / (float)ptex.Height; var ba = size.X / MathF.Max(1f, size.Y);
                Vector2 uv0 = Vector2.Zero, uv1 = Vector2.One;
                if (ta > ba) { var f = ba / ta; uv0.X = (1f - f) * 0.5f; uv1.X = 1f - uv0.X; }
                else { var f = ta / ba; uv0.Y = (1f - f) * 0.5f; uv1.Y = 1f - uv0.Y; }
                dl.AddImageRounded(ptex.Handle, pos, pos + size, uv0, uv1, 0xFFFFFFFF, r);
                drewPicture = true;
            }
            else if (kind == 1)
            {
                // No cover set: a frame of the video itself, the way the feed previews videos.
                SocialVideoThumbCache.BeginFrame();
                if (SocialVideoThumbCache.TryGet(BaseUrl(url), out var thumbId, out var vw, out var vh) && vw > 0 && vh > 0)
                {
                    var scale = MathF.Min(size.X / vw, size.Y / vh);
                    var dw = vw * scale; var dh = vh * scale;
                    var iMin = pos + new Vector2((size.X - dw) * 0.5f, (size.Y - dh) * 0.5f);
                    dl.AddRectFilled(pos, pos + size, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 1f)), r);
                    dl.PushClipRect(pos, pos + size, true);
                    dl.AddImage(new ImTextureID(thumbId), iMin, iMin + new Vector2(dw, dh));
                    dl.PopClipRect();
                    drewPicture = true;
                }
            }
            if (drewPicture) dl.AddRectFilled(pos, pos + size, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, hov ? 0.15f : 0.3f)), r);
            dl.AddRect(pos, pos + size, ImGui.GetColorU32(hov ? accent : RsTheme.Border), r, ImDrawFlags.None, RsTheme.BorderThickness);
            var c = pos + size * 0.5f;
            var disc = MathF.Min(size.X, size.Y) * 0.22f;
            dl.AddCircleFilled(c, disc, ImGui.GetColorU32(new Vector4(accent.X, accent.Y, accent.Z, hov ? 0.95f : 0.75f)), 32);
            var glyph = (kind == 1 ? Dalamud.Interface.FontAwesomeIcon.Play : Dalamud.Interface.FontAwesomeIcon.Music);
            var gs = Dalamud.Interface.FontAwesomeExtensions.ToIconString(glyph);
            using (RsIcons.Push())
            {
                var sz = ImGui.CalcTextSize(gs);
                dl.AddText(c - sz * 0.5f + new Vector2(kind == 1 ? sz.X * 0.08f : 0f, 0f), 0xFFFFFFFF, gs);
            }
            var label = kind == 1 ? (drewPicture ? "VIDEO" : "VIDEO · loading preview…") : "AUDIO";
            var ls = ImGui.CalcTextSize(label);
            dl.AddText(new Vector2(pos.X + RsTheme.S(8f), pos.Y + size.Y - ls.Y - RsTheme.S(6f)), ImGui.GetColorU32(RsTheme.TextMuted), label);
            if (hov)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                var tip = string.IsNullOrWhiteSpace(tooltip) ? string.Empty : tooltip + "\n";
                ImGui.SetTooltip(tip + (kind == 1 ? "Click to play video" : "Click to play audio"));
            }
            return clicked;
        }

        // upload
        public static string UploadStatus = string.Empty;
        public static bool Uploading;

        // Pick a video / audio file, upload it, then hand back its url.
        public static void PickAndUpload(Action<string> onUrl)
        {
            var exts = VideoExts.Concat(AudioExts).ToList();
            RsFileDialog.OpenFile("Choose a video or audio file", exts, (ok, path) =>
            {
                if (!ok || string.IsNullOrEmpty(path) || !File.Exists(path)) return;
                try
                {
                    var ext = Path.GetExtension(path).ToLowerInvariant();
                    bool video = VideoExts.Contains(ext);
                    var len = new FileInfo(path).Length;
                    var cap = video ? MaxVideoBytes : MaxAudioBytes;
                    if (len > cap) { UploadStatus = $"That file is {len / (1024 * 1024)} MB; the limit is {cap / (1024 * 1024)} MB."; return; }
                    if (Uploading) { UploadStatus = "Another upload is still running."; return; }
                    var bytes = File.ReadAllBytes(path);
                    Uploading = true;
                    UploadStatus = $"Uploading {Path.GetFileName(path)}…";
                    SocialFeed.PendingUploadCallback = (kind, url, error) =>
                    {
                        Uploading = false;
                        if (string.IsNullOrEmpty(url)) { UploadStatus = "Upload failed: " + (string.IsNullOrEmpty(error) ? "unknown error" : error); return; }
                        UploadStatus = "Uploaded. Save the profile to keep it.";
                        try { onUrl(url); } catch (Exception ex) { Plugin.PluginLog.Debug("GalleryMedia onUrl: " + ex.Message); }
                    };
                    _ = SocialFeed.UploadMedia(video ? SocialFeed.MediaKind.Video : SocialFeed.MediaKind.Audio, ext, bytes);
                }
                catch (Exception ex)
                {
                    Uploading = false;
                    UploadStatus = "Could not read that file: " + ex.Message;
                }
            });
        }
    }
}
