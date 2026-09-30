using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using AbsoluteRP.RsUI;
using AbsoluteRP.Social;
using Dalamud.Bindings.ImGui;

namespace AbsoluteRP.Helpers
{
    // Files in group chat messages. A picked file is uploaded through the same media upload the feed uses and its link is put into the message; when a message is drawn, image links show as pictures (existing behaviour), video links as a play tile and audio links as the mp3 player.
    public static class ChatAttachments
    {
        public static bool Uploading;
        public static string Status = string.Empty;
        private static readonly string[] ImageExts = { "png", "jpg", "jpeg", "webp", "gif" };
        private static readonly string[] VideoExts = { "mp4", "webm", "mov", "mkv", "m4v" };
        private static readonly string[] AudioExts = { "mp3", "ogg", "wav", "flac", "m4a" };

        // Opens the file dialog; the uploaded link is handed to onUrl.
        public static void Pick(Action<string> onUrl)
        {
            if (Uploading) { Status = "Another upload is still running."; return; }
            var all = ImageExts.Concat(VideoExts).Concat(AudioExts).ToList();
            RsFileDialog.OpenImagePicker("Attach a file to the message", (ok, path) =>
            {
                if (!ok || string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
                try
                {
                    var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
                    SocialFeed.MediaKind kind;
                    if (ImageExts.Contains(ext)) kind = SocialFeed.MediaKind.Image;
                    else if (VideoExts.Contains(ext)) kind = SocialFeed.MediaKind.Video;
                    else if (AudioExts.Contains(ext)) kind = SocialFeed.MediaKind.Audio;
                    else { Status = "That file type can't be sent."; return; }
                    var bytes = File.ReadAllBytes(path);
                    var cap = kind == SocialFeed.MediaKind.Image ? 10L << 20 : kind == SocialFeed.MediaKind.Audio ? 30L << 20 : 100L << 20;
                    if (bytes.Length == 0 || bytes.Length > cap) { Status = $"Too large: the limit is {cap >> 20} MB."; return; }
                    Uploading = true;
                    Status = "Uploading " + Path.GetFileName(path) + "…";
                    SocialFeed.PendingUploadCallback = (k, url, error) =>
                    {
                        Uploading = false;
                        if (string.IsNullOrEmpty(url)) { Status = "Upload failed: " + (string.IsNullOrEmpty(error) ? "unknown error" : error); return; }
                        Status = string.Empty;
                        try { onUrl(url); } catch (Exception ex) { Plugin.PluginLog.Debug("ChatAttachments onUrl: " + ex.Message); }
                    };
                    _ = SocialFeed.UploadMedia(kind, ext, bytes);
                }
                catch (Exception ex) { Uploading = false; Status = "Could not read that file: " + ex.Message; }
            }, startPath: null, extensions: all);
        }

        // The paperclip beside a chat input. Returns true when a file was picked.
        public static void DrawAttachButton(string id, float size, Action<string> onUrl)
        {
            using (Dalamud.Interface.Utility.Raii.ImRaii.Disabled(Uploading))
            {
                if (RsElements.IconButton(Dalamud.Interface.FontAwesomeIcon.Paperclip, id, RsElements.ButtonVariant.Ghost, size)) Pick(onUrl);
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(Uploading ? Status : "Attach a picture, video or audio clip (images up to 10 MB, audio 30 MB, video 100 MB)");
        }

        public static void DrawStatus()
        {
            if (string.IsNullOrEmpty(Status)) return;
            ImGui.PushStyleColor(ImGuiCol.Text, Uploading ? RsTheme.AccentWarning : RsTheme.TextMuted);
            ImGui.TextWrapped(Status);
            ImGui.PopStyleColor();
        }

        // Video / audio links inside message text.
        private static readonly Regex MediaLink = new(@"https?://[^\s<>""]+\.(?:mp4|webm|mov|mkv|m4v|mp3|ogg|wav|flac|m4a)(?:\?[^\s<>""]*)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static bool HasMedia(string text) => !string.IsNullOrEmpty(text) && MediaLink.IsMatch(text);

        // Draws `text`, using `drawText` for the plain parts and media tiles for the links.
        public static void RenderWithMedia(string text, Action<string> drawText, float maxWidth)
        {
            if (string.IsNullOrEmpty(text)) return;
            int last = 0;
            foreach (Match m in MediaLink.Matches(text))
            {
                var before = text[last..m.Index];
                if (!string.IsNullOrWhiteSpace(before)) drawText(before);
                var url = m.Value;
                var w = MathF.Min(maxWidth, RsTheme.S(360f));
                if (GalleryMedia.KindOf(url) == 2) Misc.RenderAudioEmbed(url, w);
                else if (GalleryMedia.DrawTile(url, new Vector2(w, w * 9f / 16f), null)) GalleryMedia.Play(url);
                last = m.Index + m.Length;
            }
            var tail = text[last..];
            if (!string.IsNullOrWhiteSpace(tail)) drawText(tail);
        }
    }
}
