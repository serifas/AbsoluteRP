using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Social.Views.SubViews;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using NAudio.Wave;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using UIHelpers = AbsoluteRP.Helpers.UIHelpers;
namespace AbsoluteRP
{
    public class Misc
    {
        public class ParsedNode
        {
            public string Type; // "text", "img", "color", "url", "table", "column", etc.
            public string Content;
            public float Scale = 1.0f;
            public string ColorHex;
            public string Url;
            public List<ParsedNode> Children = new();
            public TextStyle Style = new();
        }
        public struct TextStyle
        {
            public bool Bold;
            public bool Italic;
            public bool Underline;
            public Vector4? Color;
            public int Scale;
        }
        const float minImageSize = 8f; // Minimum image width/height in pixels
        const int minFontSize = 12;    // Minimum font size
        public static Dictionary<int, ImFontPtr> FontSizes = new();

        private static Dictionary<string, IDalamudTextureWrap> _imageCache = new();
        private static HashSet<string> _imagesLoading = new();
        private static readonly object _imageCacheLock = new object();
        private static string previousInputText = "";
        private static float previousBoxWidth = 0f;
        private static string cachedWrappedText = ""; // Buffer for displaying wrapped text
        private static HashSet<string> _revealedNsfwSections = new(); // Track which NSFW sections have been revealed
        private static int _nsfwCounter = 0; // Counter for generating unique NSFW section IDs
        private static string _currentNsfwSessionId = ""; // Session ID to scope NSFW reveal states
        public static IFontHandle Jupiter;
        public static float _modVersionWidth;
        public static int loaderIndex = 0;
        private static Random random = new Random();

        public static List<ImFontPtr> availableFonts = new();
        public static ImFontPtr selectedFont;
        private static int selectedFontIndex = 0;
        private static bool allow;
        public static bool LoadUrl { get; set; } = false;
        public static string UrlToLoad { get; set; }

        // YouTube video embed state
        private static Dictionary<string, IDalamudTextureWrap> _youtubeThumbnailCache = new();
        private static HashSet<string> _youtubeThumbnailsLoading = new();
        private static string _expandedVideoId = null; // Track which video is currently expanded/fullscreen

        // Audio player state
        private static Dictionary<string, AudioPlayerState> _audioPlayers = new();
        private static HashSet<string> _audioDownloading = new();
        private static readonly object _audioLock = new object();

        /// <summary>
        /// State for an inline audio player
        /// </summary>
        private class AudioPlayerState : IDisposable
        {
            public string Url { get; set; }
            public string FileName { get; set; }
            public string TempFilePath { get; set; }
            public WaveOutEvent WaveOut { get; set; }
            public AudioFileReader AudioReader { get; set; }
            public bool IsLoading { get; set; } = true;
            public bool IsPlaying { get; set; }
            public bool IsPaused { get; set; }
            public string ErrorMessage { get; set; }
            public float DownloadProgress { get; set; }
            public float Volume { get; set; } = 0.5f; // 0.0 to 1.0, default 50%
            public TimeSpan CurrentTime => AudioReader?.CurrentTime ?? TimeSpan.Zero;
            public TimeSpan TotalTime => AudioReader?.TotalTime ?? TimeSpan.Zero;

            public void Dispose()
            {
                try
                {
                    Plugin.PluginLog.Info($"[AudioPlayer] Disposing audio player for: {FileName}");

                    if (WaveOut != null)
                    {
                        if (WaveOut.PlaybackState == PlaybackState.Playing || WaveOut.PlaybackState == PlaybackState.Paused)
                        {
                            Plugin.PluginLog.Info($"[AudioPlayer] Stopping playback...");
                            WaveOut.Stop();
                        }
                        WaveOut.Dispose();
                        WaveOut = null;
                        Plugin.PluginLog.Info($"[AudioPlayer] WaveOut disposed");
                    }

                    if (AudioReader != null)
                    {
                        AudioReader.Dispose();
                        AudioReader = null;
                        Plugin.PluginLog.Info($"[AudioPlayer] AudioReader disposed");
                    }

                    if (!string.IsNullOrEmpty(TempFilePath) && File.Exists(TempFilePath))
                    {
                        try { File.Delete(TempFilePath); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Error($"[AudioPlayer] Error disposing: {ex.Message}");
                }
            }
        }

        // YouTube URL regex patterns
        private static readonly Regex YoutubeUrlRegex = new Regex(
            @"(?:https?:\/\/)?(?:www\.)?(?:youtube\.com\/watch\?v=|youtu\.be\/|youtube\.com\/embed\/)([a-zA-Z0-9_-]{11})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Check if a URL is a YouTube video URL and extract the video ID
        /// </summary>
        public static bool TryGetYoutubeVideoId(string url, out string videoId)
        {
            videoId = null;
            if (string.IsNullOrWhiteSpace(url))
                return false;

            var match = YoutubeUrlRegex.Match(url);
            if (match.Success)
            {
                videoId = match.Groups[1].Value;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Get the YouTube embed URL for a video ID
        /// </summary>
        public static string GetYoutubeEmbedUrl(string videoId)
        {
            return $"https://www.youtube.com/embed/{videoId}?autoplay=1";
        }

        /// <summary>
        /// Get the YouTube thumbnail URL for a video ID
        /// </summary>
        public static string GetYoutubeThumbnailUrl(string videoId)
        {
            // mqdefault is 320x180, good balance of quality and size
            return $"https://img.youtube.com/vi/{videoId}/mqdefault.jpg";
        }

        /// <summary>
        /// Check if Browsingway plugin is installed and loaded
        /// </summary>
        public static bool IsBrowsingwayInstalled()
        {
            try
            {
                var browsingway = Plugin.PluginInterface.InstalledPlugins
                    .FirstOrDefault(p => p.InternalName == "Browsingway" || p.Name == "Browsingway");
                return browsingway != null && browsingway.IsLoaded;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if a URL points to an image based on extension or known image hosting services
        /// </summary>
        public static bool IsImageUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            url = url.ToLowerInvariant();

            // Check for common image extensions (with or without query params)
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg", ".tiff", ".ico" };
            foreach (var ext in imageExtensions)
            {
                // Check if URL contains the extension (handles query params like image.png?size=large)
                if (url.Contains(ext))
                    return true;
            }

            // Check for common image hosting services
            var imageHosts = new[] {
                "i.imgur.com",
                "media.discordapp.net",
                "cdn.discordapp.com",
                "pbs.twimg.com",
                "i.redd.it",
                "preview.redd.it",
                "i.pinimg.com",
                "images.unsplash.com"
            };
            foreach (var host in imageHosts)
            {
                if (url.Contains(host))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Check if a URL points to an audio file based on extension
        /// </summary>
        public static bool IsAudioUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            url = url.ToLowerInvariant();

            // Check for common audio extensions (with or without query params)
            var audioExtensions = new[] { ".mp3", ".wav", ".ogg", ".flac", ".m4a", ".aac", ".wma" };
            foreach (var ext in audioExtensions)
            {
                // Check if URL contains the extension before any query params
                var queryIndex = url.IndexOf('?');
                var pathPart = queryIndex > 0 ? url.Substring(0, queryIndex) : url;
                if (pathPart.EndsWith(ext))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Render a YouTube video embed in chat. Shows thumbnail with play button.
        /// Clicking opens the built-in video player.
        /// </summary>
        public static void RenderYoutubeEmbed(string url, string videoId, float maxWidth = 320f)
        {
            string thumbnailUrl = GetYoutubeThumbnailUrl(videoId);
            float scale = ImGui.GetIO().FontGlobalScale;

            // Check if this video is expanded
            bool isExpanded = _expandedVideoId == videoId;
            float videoWidth = isExpanded ? Math.Min(640f * scale, ImGui.GetContentRegionAvail().X - 20f) : Math.Min(maxWidth * scale, ImGui.GetContentRegionAvail().X - 20f);
            float videoHeight = videoWidth * 9f / 16f; // 16:9 aspect ratio

            ImGui.PushID($"youtube_{videoId}");

            // Draw video container box
            var cursorPos = ImGui.GetCursorScreenPos();
            var drawList = ImGui.GetWindowDrawList();

            // Background box
            drawList.AddRectFilled(
                cursorPos,
                new Vector2(cursorPos.X + videoWidth, cursorPos.Y + videoHeight),
                ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.1f, 1f)),
                4f);

            // Border (YouTube red)
            drawList.AddRect(
                cursorPos,
                new Vector2(cursorPos.X + videoWidth, cursorPos.Y + videoHeight),
                ImGui.GetColorU32(new Vector4(0.8f, 0.1f, 0.1f, 1f)),
                4f,
                ImDrawFlags.None,
                2f);

            // Try to load and display thumbnail
            bool thumbnailLoaded = false;
            lock (_imageCacheLock)
            {
                if (_youtubeThumbnailCache.TryGetValue(videoId, out var thumbnail) && thumbnail != null)
                {
                    // Draw thumbnail
                    ImGui.SetCursorScreenPos(cursorPos);
                    ImGui.Image(thumbnail.Handle, new Vector2(videoWidth, videoHeight));
                    thumbnailLoaded = true;
                }
                else if (!_youtubeThumbnailsLoading.Contains(videoId))
                {
                    // Start loading thumbnail
                    _youtubeThumbnailsLoading.Add(videoId);
                    LoadYoutubeThumbnailAsync(videoId, thumbnailUrl);
                }
            }

            if (!thumbnailLoaded)
            {
                // Show loading placeholder
                ImGui.SetCursorScreenPos(cursorPos);
                ImGui.Dummy(new Vector2(videoWidth, videoHeight));

                // Center "Loading..." text
                var loadingText = "Loading...";
                var textSize = ImGui.CalcTextSize(loadingText);
                drawList.AddText(
                    new Vector2(cursorPos.X + (videoWidth - textSize.X) / 2, cursorPos.Y + (videoHeight - textSize.Y) / 2),
                    ImGui.GetColorU32(new Vector4(0.7f, 0.7f, 0.7f, 1f)),
                    loadingText);
            }

            // Draw play button overlay
            var centerX = cursorPos.X + videoWidth / 2;
            var centerY = cursorPos.Y + videoHeight / 2;
            float playButtonRadius = 25f * scale;

            // Semi-transparent circle background (YouTube red)
            drawList.AddCircleFilled(
                new Vector2(centerX, centerY),
                playButtonRadius,
                ImGui.GetColorU32(new Vector4(0.8f, 0.1f, 0.1f, 0.9f)));

            // Play triangle
            float triangleSize = 12f * scale;
            drawList.AddTriangleFilled(
                new Vector2(centerX - triangleSize / 2 + 3f, centerY - triangleSize),
                new Vector2(centerX - triangleSize / 2 + 3f, centerY + triangleSize),
                new Vector2(centerX + triangleSize, centerY),
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)));

            // Invisible button for click detection on the video area
            ImGui.SetCursorScreenPos(cursorPos);
            if (ImGui.InvisibleButton($"play_{videoId}", new Vector2(videoWidth, videoHeight)))
            {
                // Open the WebView2 Forms window (more efficient than frame streaming)
                AbsoluteRP.Windows.Ect.YouTubePlayerWindow.OpenVideo(videoId, $"https://www.youtube.com/watch?v={videoId}");
            }

            // Tooltip on hover
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Click to play video");
            }

            // Move cursor past the video box
            ImGui.SetCursorScreenPos(new Vector2(cursorPos.X, cursorPos.Y + videoHeight + 4f));

            // Expand/Collapse button row
            var buttonRowY = cursorPos.Y + videoHeight + 4f;

            // Expand button (positioned in top-right of video)
            ImGui.SetCursorScreenPos(new Vector2(cursorPos.X + videoWidth - 30f * scale, cursorPos.Y + 5f));
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.1f, 0.1f, 0.1f, 0.7f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.2f, 0.2f, 0.2f, 0.9f));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
            if (ImGui.SmallButton(isExpanded ? "−" : "+"))
            {
                _expandedVideoId = isExpanded ? null : videoId;
            }
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(2);

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(isExpanded ? "Minimize preview" : "Expand preview");
            }

            // Position after video box
            ImGui.SetCursorScreenPos(new Vector2(cursorPos.X, buttonRowY + 4f));

            // Show URL below video (clickable to open in browser)
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 0.6f, 0.9f, 1f));
            ImGui.TextWrapped(url);
            ImGui.PopStyleColor();

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Click to open in browser");
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    Misc.LoadUrl = true;
                    Misc.UrlToLoad = url;
                }
            }

            ImGui.PopID();
        }

        /// <summary>
        /// Async load YouTube thumbnail
        /// </summary>
        private static async void LoadYoutubeThumbnailAsync(string videoId, string thumbnailUrl)
        {
            try
            {
                using (var webClient = new System.Net.WebClient())
                {
                    var imageBytes = await webClient.DownloadDataTaskAsync(thumbnailUrl);
                    if (imageBytes != null && imageBytes.Length > 0)
                    {
                        var tex = Plugin.TextureProvider.CreateFromImageAsync(imageBytes).Result;
                        lock (_imageCacheLock)
                        {
                            if (tex != null)
                                _youtubeThumbnailCache[videoId] = tex;
                            else
                                _youtubeThumbnailCache[videoId] = null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Warning($"Failed to load YouTube thumbnail for {videoId}: {ex.Message}");
                lock (_imageCacheLock)
                {
                    _youtubeThumbnailCache[videoId] = null;
                }
            }
            finally
            {
                lock (_imageCacheLock)
                {
                    _youtubeThumbnailsLoading.Remove(videoId);
                }
            }
        }

        /// <summary>
        /// Clear expanded video state (call when closing chat window, etc.)
        /// </summary>
        public static void ClearExpandedVideo()
        {
            _expandedVideoId = null;
        }

        /// <summary>
        /// Render an inline audio player for audio file URLs
        /// </summary>
        public static void RenderAudioEmbed(string url, float maxWidth = 320f)
        {
            float scale = ImGui.GetIO().FontGlobalScale;
            float playerWidth = Math.Min(maxWidth * scale, ImGui.GetContentRegionAvail().X - 20f);
            float playerHeight = 95f * scale; // Increased height for volume control

            string playerId = url.GetHashCode().ToString();

            ImGui.PushID($"audio_{playerId}");

            var cursorPos = ImGui.GetCursorScreenPos();
            var drawList = ImGui.GetWindowDrawList();

            // Background box (dark with blue tint for audio)
            drawList.AddRectFilled(
                cursorPos,
                new Vector2(cursorPos.X + playerWidth, cursorPos.Y + playerHeight),
                ImGui.GetColorU32(new Vector4(0.08f, 0.1f, 0.15f, 1f)),
                6f);

            // Border (blue for audio)
            drawList.AddRect(
                cursorPos,
                new Vector2(cursorPos.X + playerWidth, cursorPos.Y + playerHeight),
                ImGui.GetColorU32(new Vector4(0.2f, 0.4f, 0.8f, 1f)),
                6f,
                ImDrawFlags.None,
                2f);

            AudioPlayerState player;
            lock (_audioLock)
            {
                if (!_audioPlayers.TryGetValue(url, out player))
                {
                    // Create new player state
                    player = new AudioPlayerState
                    {
                        Url = url,
                        FileName = GetFileNameFromUrl(url),
                        IsLoading = true
                    };
                    _audioPlayers[url] = player;

                    // Start downloading
                    if (!_audioDownloading.Contains(url))
                    {
                        _audioDownloading.Add(url);
                        _ = LoadAudioAsync(url);
                    }
                }
            }

            // Draw content based on state
            float padding = 8f * scale;
            float buttonSize = 30f * scale;
            float contentStartX = cursorPos.X + padding;
            float contentStartY = cursorPos.Y + padding;

            if (player.IsLoading)
            {
                // Show loading state
                var loadingText = "Loading audio...";
                var textSize = ImGui.CalcTextSize(loadingText);
                drawList.AddText(
                    new Vector2(cursorPos.X + (playerWidth - textSize.X) / 2, cursorPos.Y + (playerHeight - textSize.Y) / 2),
                    ImGui.GetColorU32(new Vector4(0.7f, 0.7f, 0.7f, 1f)),
                    loadingText);
            }
            else if (!string.IsNullOrEmpty(player.ErrorMessage))
            {
                // Show error
                var errorText = $"Error: {player.ErrorMessage}";
                drawList.AddText(
                    new Vector2(contentStartX, contentStartY),
                    ImGui.GetColorU32(new Vector4(1f, 0.3f, 0.3f, 1f)),
                    errorText);
            }
            else
            {
                // Draw title/filename
                var titleText = "Audio";
                if (titleText.Length > 40)
                    titleText = titleText.Substring(0, 37) + "...";
                drawList.AddText(
                    new Vector2(contentStartX, contentStartY),
                    ImGui.GetColorU32(new Vector4(0.9f, 0.9f, 0.9f, 1f)),
                    titleText);

                // Control buttons row
                float controlsY = contentStartY + 22f * scale;

                // Play/Pause button
                ImGui.SetCursorScreenPos(new Vector2(contentStartX, controlsY));
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.4f, 0.8f, 0.8f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.5f, 0.9f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.2f, 0.3f, 0.7f, 1f));
                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);

                string playPauseIcon = player.IsPlaying && !player.IsPaused ? "Pause" : "Play";
                if (ImGui.Button(playPauseIcon, new Vector2(buttonSize * 2, buttonSize)))
                {
                    ToggleAudioPlayback(player);
                }

                // Stop button
                ImGui.SameLine();
                if (ImGui.Button("Stop", new Vector2(buttonSize * 2, buttonSize)))
                {
                    StopAudioPlayback(player);
                }
                ImGui.PopStyleVar();
                ImGui.PopStyleColor(3);

                // Time display
                ImGui.SameLine();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 7f * scale);
                string timeText = $"{FormatAudioTime(player.CurrentTime)} / {FormatAudioTime(player.TotalTime)}";
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), timeText);

                // Seek bar
                float seekBarY = controlsY + buttonSize + 4f * scale;
                float seekBarWidth = playerWidth - padding * 2;
                float seekBarHeight = 8f * scale;

                // Seek bar background
                var seekBarPos = new Vector2(contentStartX, seekBarY);
                drawList.AddRectFilled(
                    seekBarPos,
                    new Vector2(seekBarPos.X + seekBarWidth, seekBarPos.Y + seekBarHeight),
                    ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.2f, 1f)),
                    4f);

                // Seek bar progress
                float progress = 0f;
                if (player.TotalTime.TotalSeconds > 0)
                {
                    progress = (float)(player.CurrentTime.TotalSeconds / player.TotalTime.TotalSeconds);
                }
                if (progress > 0)
                {
                    drawList.AddRectFilled(
                        seekBarPos,
                        new Vector2(seekBarPos.X + seekBarWidth * progress, seekBarPos.Y + seekBarHeight),
                        ImGui.GetColorU32(new Vector4(0.2f, 0.5f, 0.9f, 1f)),
                        4f);
                }

                // Seek bar knob
                float knobX = seekBarPos.X + seekBarWidth * progress;
                drawList.AddCircleFilled(
                    new Vector2(knobX, seekBarPos.Y + seekBarHeight / 2),
                    6f * scale,
                    ImGui.GetColorU32(new Vector4(0.3f, 0.6f, 1f, 1f)));

                // Invisible button for seeking
                ImGui.SetCursorScreenPos(seekBarPos);
                if (ImGui.InvisibleButton($"seek_{playerId}", new Vector2(seekBarWidth, seekBarHeight + 8f * scale)))
                {
                    // Calculate seek position
                    var mousePos = ImGui.GetMousePos();
                    float seekProgress = (mousePos.X - seekBarPos.X) / seekBarWidth;
                    seekProgress = Math.Clamp(seekProgress, 0f, 1f);
                    SeekAudio(player, seekProgress);
                }

                // Dragging for seek
                if (ImGui.IsItemActive())
                {
                    var mousePos = ImGui.GetMousePos();
                    float seekProgress = (mousePos.X - seekBarPos.X) / seekBarWidth;
                    seekProgress = Math.Clamp(seekProgress, 0f, 1f);
                    SeekAudio(player, seekProgress);
                }

                // Volume control row
                float volumeRowY = seekBarY + seekBarHeight + 12f * scale;
                float volumeBarWidth = playerWidth - padding * 2 - 50f * scale; // Leave room for label
                float volumeBarHeight = 6f * scale;

                // Volume icon/label
                var volumeLabel = player.Volume < 0.01f ? "Mute" : "Vol";
                drawList.AddText(
                    new Vector2(contentStartX, volumeRowY - 2f * scale),
                    ImGui.GetColorU32(new Vector4(0.6f, 0.6f, 0.6f, 1f)),
                    volumeLabel);

                // Volume bar background
                var volumeBarPos = new Vector2(contentStartX + 35f * scale, volumeRowY);
                drawList.AddRectFilled(
                    volumeBarPos,
                    new Vector2(volumeBarPos.X + volumeBarWidth, volumeBarPos.Y + volumeBarHeight),
                    ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.2f, 1f)),
                    3f);

                // Volume bar fill
                if (player.Volume > 0)
                {
                    drawList.AddRectFilled(
                        volumeBarPos,
                        new Vector2(volumeBarPos.X + volumeBarWidth * player.Volume, volumeBarPos.Y + volumeBarHeight),
                        ImGui.GetColorU32(new Vector4(0.3f, 0.7f, 0.4f, 1f)),
                        3f);
                }

                // Volume knob
                float volumeKnobX = volumeBarPos.X + volumeBarWidth * player.Volume;
                drawList.AddCircleFilled(
                    new Vector2(volumeKnobX, volumeBarPos.Y + volumeBarHeight / 2),
                    5f * scale,
                    ImGui.GetColorU32(new Vector4(0.4f, 0.8f, 0.5f, 1f)));

                // Invisible button for volume control
                ImGui.SetCursorScreenPos(new Vector2(volumeBarPos.X, volumeBarPos.Y - 4f * scale));
                if (ImGui.InvisibleButton($"volume_{playerId}", new Vector2(volumeBarWidth, volumeBarHeight + 8f * scale)))
                {
                    var mousePos = ImGui.GetMousePos();
                    float newVolume = (mousePos.X - volumeBarPos.X) / volumeBarWidth;
                    newVolume = Math.Clamp(newVolume, 0f, 1f);
                    SetAudioVolume(player, newVolume);
                }

                // Dragging for volume
                if (ImGui.IsItemActive())
                {
                    var mousePos = ImGui.GetMousePos();
                    float newVolume = (mousePos.X - volumeBarPos.X) / volumeBarWidth;
                    newVolume = Math.Clamp(newVolume, 0f, 1f);
                    SetAudioVolume(player, newVolume);
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip($"Volume: {(int)(player.Volume * 100)}%");
                }
                ImGui.Spacing();
            }

            // Dummy to reserve space
            ImGui.SetCursorScreenPos(cursorPos);
            ImGui.Dummy(new Vector2(playerWidth, playerHeight));

            // Show URL below player
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 0.6f, 0.9f, 1f));
            var shortUrl = url.Length > 50 ? url.Substring(0, 47) + "..." : url;
            ImGui.TextWrapped(shortUrl);
            ImGui.PopStyleColor();

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Click to open in browser\n" + url);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    Misc.LoadUrl = true;
                    Misc.UrlToLoad = url;
                }
            }

            ImGui.PopID();
        }

        private static string GetFileNameFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var fileName = Path.GetFileName(uri.LocalPath);
                if (!string.IsNullOrEmpty(fileName))
                    return Uri.UnescapeDataString(fileName);
            }
            catch { }
            return "Audio File";
        }

        private static string FormatAudioTime(TimeSpan time)
        {
            if (time.TotalHours >= 1)
                return time.ToString(@"h\:mm\:ss");
            return time.ToString(@"mm\:ss");
        }

        private static async Task LoadAudioAsync(string url)
        {
            AudioPlayerState player;
            lock (_audioLock)
            {
                if (!_audioPlayers.TryGetValue(url, out player))
                    return;
            }

            string tempPath = null;
            try
            {
                Plugin.PluginLog.Info($"[AudioPlayer] Starting download: {url}");

                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(5);

                // Add headers that some servers require
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                httpClient.DefaultRequestHeaders.Add("Accept", "audio/*,*/*");

                var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

                Plugin.PluginLog.Info($"[AudioPlayer] Response status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
                }

                var contentLength = response.Content.Headers.ContentLength;
                Plugin.PluginLog.Info($"[AudioPlayer] Content length: {contentLength ?? -1} bytes");

                var extension = Path.GetExtension(new Uri(url).LocalPath);
                if (string.IsNullOrEmpty(extension))
                    extension = ".mp3";

                tempPath = Path.Combine(Path.GetTempPath(), $"ARP_Audio_{Guid.NewGuid()}{extension}");

                Plugin.PluginLog.Info($"[AudioPlayer] Downloading to: {tempPath}");

                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fileStream);
                }

                var fileInfo = new FileInfo(tempPath);
                Plugin.PluginLog.Info($"[AudioPlayer] Downloaded {fileInfo.Length} bytes");

                if (fileInfo.Length == 0)
                {
                    throw new Exception("Downloaded file is empty");
                }

                // Load audio file
                Plugin.PluginLog.Info($"[AudioPlayer] Loading audio file...");
                var audioReader = new AudioFileReader(tempPath);
                audioReader.Volume = player.Volume * 2f; // Set initial volume (doubled for louder output)
                var waveOut = new WaveOutEvent();
                waveOut.Init(audioReader);

                Plugin.PluginLog.Info($"[AudioPlayer] Audio loaded successfully. Duration: {audioReader.TotalTime}");

                waveOut.PlaybackStopped += (s, e) =>
                {
                    lock (_audioLock)
                    {
                        if (_audioPlayers.TryGetValue(url, out var p))
                        {
                            p.IsPlaying = false;
                            p.IsPaused = false;
                            if (p.AudioReader != null)
                                p.AudioReader.Position = 0;
                        }
                    }
                };

                lock (_audioLock)
                {
                    if (_audioPlayers.TryGetValue(url, out var p))
                    {
                        p.TempFilePath = tempPath;
                        p.AudioReader = audioReader;
                        p.WaveOut = waveOut;
                        p.IsLoading = false;
                    }
                }
            }
            catch (HttpRequestException httpEx)
            {
                Plugin.PluginLog.Error($"[AudioPlayer] HTTP error loading {url}: {httpEx.Message}");
                lock (_audioLock)
                {
                    if (_audioPlayers.TryGetValue(url, out var p))
                    {
                        p.IsLoading = false;
                        p.ErrorMessage = $"Network error: {httpEx.Message}";
                    }
                }
                CleanupTempFile(tempPath);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"[AudioPlayer] Failed to load audio from {url}: {ex.GetType().Name} - {ex.Message}");
                if (ex.InnerException != null)
                {
                    Plugin.PluginLog.Error($"[AudioPlayer] Inner exception: {ex.InnerException.Message}");
                }
                lock (_audioLock)
                {
                    if (_audioPlayers.TryGetValue(url, out var p))
                    {
                        p.IsLoading = false;
                        p.ErrorMessage = ex.Message.Length > 60 ? ex.Message.Substring(0, 57) + "..." : ex.Message;
                    }
                }
                CleanupTempFile(tempPath);
            }
            finally
            {
                lock (_audioLock)
                {
                    _audioDownloading.Remove(url);
                }
            }
        }

        private static void CleanupTempFile(string tempPath)
        {
            if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }

        private static void ToggleAudioPlayback(AudioPlayerState player)
        {
            if (player.WaveOut == null)
                return;

            if (!player.IsPlaying)
            {
                player.WaveOut.Play();
                player.IsPlaying = true;
                player.IsPaused = false;
            }
            else if (!player.IsPaused)
            {
                player.WaveOut.Pause();
                player.IsPaused = true;
            }
            else
            {
                player.WaveOut.Play();
                player.IsPaused = false;
            }
        }

        private static void StopAudioPlayback(AudioPlayerState player)
        {
            if (player.WaveOut == null)
                return;

            player.WaveOut.Stop();
            player.IsPlaying = false;
            player.IsPaused = false;

            if (player.AudioReader != null)
                player.AudioReader.Position = 0;
        }

        private static void SeekAudio(AudioPlayerState player, float progress)
        {
            if (player.AudioReader == null || player.AudioReader.Length <= 0)
                return;

            var position = (long)(progress * player.AudioReader.Length);
            player.AudioReader.Position = Math.Min(position, player.AudioReader.Length - 1);
        }

        private static void SetAudioVolume(AudioPlayerState player, float volume)
        {
            player.Volume = Math.Clamp(volume, 0f, 1f);

            // NAudio AudioFileReader Volume can go above 1.0 for amplification
            // Map 0-100% display to 0-200% actual volume for louder output
            if (player.AudioReader != null)
            {
                player.AudioReader.Volume = player.Volume * 2f;
            }
        }

        /// <summary>
        /// Pause all currently playing audio (call when closing windows with audio)
        /// </summary>
        public static void PauseAllAudio()
        {
            lock (_audioLock)
            {
                foreach (var player in _audioPlayers.Values)
                {
                    if (player.IsPlaying && !player.IsPaused && player.WaveOut != null)
                    {
                        player.WaveOut.Pause();
                        player.IsPaused = true;
                    }
                }
            }
        }

        /// <summary>
        /// Clean up all audio players (call when plugin is unloading)
        /// </summary>
        public static void CleanupAudioPlayers()
        {
            Plugin.PluginLog.Info($"[AudioPlayer] CleanupAudioPlayers called, {_audioPlayers.Count} players to clean up");
            lock (_audioLock)
            {
                foreach (var player in _audioPlayers.Values)
                {
                    player.Dispose();
                }
                _audioPlayers.Clear();
                _audioDownloading.Clear();
            }
            Plugin.PluginLog.Info($"[AudioPlayer] CleanupAudioPlayers complete");
        }

        public class LoaderTweenState
        {
            public float TweenedValue;
            public float TweenStartValue;
            public float TweenTargetValue;
            public float TweenStartTime;
            public float TweenDuration = 0.4f;
        }
        public static float ConvertToPercentage(float value)
        {
            // Clamp the value between 0 and 100
            value = Math.Max(0f, Math.Min(100f, value));

            // Return the percentage
            return value / 100f * 100f;
        }
    
        private static ImFontPtr GetFontForStyle(TextStyle style, int baseSize)
        {
            int key = baseSize;
            if (style.Bold && style.Italic)
                key = 300 + baseSize;
            else if (style.Bold)
                key = 100 + baseSize;
            else if (style.Italic)
                key = 200 + baseSize;

            if (FontSizes.TryGetValue(key, out var font) && !font.IsNull)
                return font;
            if (FontSizes.TryGetValue(baseSize, out var regularFont) && !regularFont.IsNull)
                return regularFont;
            return ImGui.GetFont();
        }

        private static void ParseStyledText(string text, TextStyle style, List<ParsedNode> nodes)
        {
            var tagRegex = new Regex(@"<(b|i|u)>(.*?)</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            int idx = 0;
            while (idx < text.Length)
            {
                var match = tagRegex.Match(text, idx);
                if (match.Success && match.Index == idx)
                {
                    var tag = match.Groups[1].Value.ToLower();
                    var inner = match.Groups[2].Value;
                    var newStyle = style;
                    if (tag == "b") newStyle.Bold = true;
                    else if (tag == "i") newStyle.Italic = true;
                    else if (tag == "u") newStyle.Underline = true;
                    ParseStyledText(inner, newStyle, nodes);
                    idx += match.Length;
                }
                else
                {
                    int nextTag = text.IndexOf('<', idx);
                    if (nextTag == -1) nextTag = text.Length;
                    string plain = text.Substring(idx, nextTag - idx);
                    if (!string.IsNullOrEmpty(plain))
                        nodes.Add(new ParsedNode { Type = "text", Content = plain, Style = style });
                    idx = nextTag;
                }
            }
        }
        private static float ParseFontSize(string sizeAttr)
        {
            if (string.IsNullOrEmpty(sizeAttr)) return ImGui.GetFontSize();
            if (float.TryParse(sizeAttr, out float sz)) return sz;
            return ImGui.GetFontSize();
        }
        public static ParsedNode ParseHtmlLayout(string text)
        {
            var root = new ParsedNode { Type = "root" };
            int idx = 0;
            while (idx < text.Length)
            {
                // ... existing tag parsing for nav, table, img, color, url, scale ...

                // Plain text and style tags
                int nextTagIdx = text.IndexOf('<', idx);
                if (nextTagIdx == -1) nextTagIdx = text.Length;
                string plainText = text.Substring(idx, nextTagIdx - idx);
                if (!string.IsNullOrWhiteSpace(plainText))
                    ParseStyledText(plainText, new TextStyle(), root.Children);
                idx = nextTagIdx;
            }
            return root;
        }
        // Add at the top of the Misc class:
        private static Dictionary<string, int> _tabIndices = new(); // Stores current tab index per unique tab group

        private static Dictionary<string, int> _navIndices = new(); // Stores current tab index per navigation group

        public static void RenderParsedLayout(ParsedNode node, float wrapWidth, float wrapHeight, bool url, bool image, bool color)
        {
            foreach (var child in node.Children)
            {
                switch (child.Type)
                {
                    case "nav":
                        // Only render current page (add your navigation logic here)
                        int pageIdx = 0;
                        if (child.Children.Count > pageIdx)
                            RenderParsedLayout(child.Children[pageIdx], wrapWidth, wrapHeight, url, image, color);
                        break;
                    case "page":
                        RenderParsedLayout(child.Children[0], wrapWidth, wrapHeight, url, image, color);
                        break;
                    case "table":
                        if (ImGui.BeginTable("CustomTable", child.Children.Count, ImGuiTableFlags.None))
                        {
                            ImGui.TableNextRow();
                            for (int col = 0; col < child.Children.Count; col++)
                            {
                                ImGui.TableSetColumnIndex(col);
                                ImGui.BeginGroup();
                                RenderParsedLayout(child.Children[col], wrapWidth / child.Children.Count, wrapHeight, url, image, color);
                                ImGui.EndGroup();
                            }
                            ImGui.EndTable();
                        }
                        break;
                    case "column":
                        foreach (var colChild in child.Children)
                            RenderParsedLayout(colChild, wrapWidth, wrapHeight, url, image, color);
                        break;
                    case "img":
                        if (image)
                        {
                            bool imgRendered = false;
                            IDalamudTextureWrap texture = null;
                            try
                            {
                                lock (_imageCacheLock)
                                {
                                    _imageCache.TryGetValue(child.Content, out texture);
                                }
                                if (texture != null)
                                {
                                    var handle = texture.Handle;
                                    if (handle != default)
                                    {
                                        Vector2 imgSize = new Vector2(texture.Width, texture.Height);
                                        imgSize.X = Math.Max(imgSize.X, minImageSize);
                                        imgSize.Y = Math.Max(imgSize.Y, minImageSize);
                                        ImGui.Image(handle, imgSize);
                                        imgRendered = true;
                                    }
                                }
                            }
                            catch (ObjectDisposedException) { }

                            if (!imgRendered)
                            {
                                ImGui.TextColored(new Vector4(1, 1, 0, 1), "[Loading image or image failed!]");
                            }
                        }
                        break;
                    case "color":
                        if (color && TryParseHexColor(child.ColorHex, out Vector4 colorVal))
                            ImGui.TextColored(colorVal, child.Content);
                        else
                            ImGui.Text(child.Content);
                        break;
                    case "url":
                        if (url)
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.2f, 0.5f, 1f, 1f));
                            ImGui.Text(child.Content);
                            ImGui.PopStyleColor();
                        }
                        else
                        {
                            ImGui.Text(child.Content);
                        }
                        break;
                    case "scale":
                        ImGui.PushFont(FontSizes.TryGetValue((int)Math.Round(child.Scale), out var font) ? font : ImGui.GetFont());
                        foreach (var scaleChild in child.Children)
                            RenderParsedLayout(scaleChild, wrapWidth, wrapHeight, url, image, color);
                        ImGui.PopFont();
                        break;
                    case "text":
                        ImFontPtr Font = ImGui.GetFont();
                        if (child.Style.Bold && FontSizes.TryGetValue(18, out var boldFont)) Font = boldFont;
                        if (child.Style.Italic && FontSizes.TryGetValue(16, out var italicFont)) Font = italicFont;
                        if (child.Style.Scale > 0 && FontSizes.TryGetValue(child.Style.Scale, out var scaledFont)) Font = scaledFont;

                        ImGui.PushFont(Font);

                        if (child.Style.Color.HasValue)
                            ImGui.TextColored(child.Style.Color.Value, child.Content);
                        else
                            ImGui.TextWrapped(child.Content);

                        ImGui.PopFont();

                        if (child.Style.Underline)
                        {
                            var min = ImGui.GetItemRectMin();
                            var max = ImGui.GetItemRectMax();
                            var drawList = ImGui.GetWindowDrawList();
                            drawList.AddLine(
                                new Vector2(min.X, max.Y),
                                new Vector2(max.X, max.Y),
                                ImGui.GetColorU32(child.Style.Color ?? new Vector4(1, 1, 1, 1)),
                                2.0f
                            );
                        }
                        break;
                }
            }
        }

        public static void RenderHtmlElements(string text, bool url, bool image, bool color, bool isLimited, Vector2? overrideWrapSize = null, bool disableWordWrap = false, bool limitImageWidth = false)
        {
            Vector2 wrapSize = overrideWrapSize ?? (ImGui.GetWindowSize() - new Vector2(50, 0));
            float wrapWidth = wrapSize.X;
            float wrapHeight = wrapSize.Y;

            // Navigation and page support
            var navRegex = new Regex(@"<(navigation|nav)(?:\s+id\s*=\s*""([^""]+)"")?\s*>(.*?)</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var navMatches = navRegex.Matches(text);

            int lastIndex = 0;
            int navBlockCount = 0;
            foreach (Match navMatch in navMatches)
            {
                // Render content before this navigation block
                if (navMatch.Index > lastIndex)
                {
                    string beforeNav = text.Substring(lastIndex, navMatch.Index - lastIndex);
                    if (!string.IsNullOrWhiteSpace(beforeNav))
                        RenderHtmlElementsNoSameline(beforeNav, url, image, color, wrapWidth, wrapHeight, isLimited, true, disableWordWrap, limitImageWidth);
                }

                string navId = navMatch.Groups[2].Success && !string.IsNullOrWhiteSpace(navMatch.Groups[2].Value)
                    ? navMatch.Groups[2].Value
                    : $"nav_{navBlockCount}";

                string navContent = navMatch.Groups[3].Value;

                // Parse <page title="...">...</page> blocks inside navigation
                var pageRegex = new Regex(@"<page\s+title\s*=\s*""([^""]+)""\s*>(.*?)</page>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                var pageMatches = pageRegex.Matches(navContent);

                List<string> pageTitles = new();
                List<string> pageContents = new();

                foreach (Match pageMatch in pageMatches)
                {
                    pageTitles.Add(pageMatch.Groups[1].Value);
                    pageContents.Add(pageMatch.Groups[2].Value);
                }

                if (!_navIndices.ContainsKey(navId))
                    _navIndices[navId] = 0;
                int currentPage = _navIndices[navId];

                using (ImRaii.Child("Navigation_" + navId, new Vector2(ImGui.GetWindowSize().X, ImGui.GetIO().FontGlobalScale * 32), true))
                {
                    if (currentPage > 0)
                    {
                        if (ImGui.Button("《 "))
                        {
                            _navIndices[navId] = currentPage - 1;
                            currentPage = _navIndices[navId];
                        }
                    }

                    ImGui.SameLine();
                    if (pageTitles.Count > currentPage)
                    {
                        ImGui.SetCursorPosX(ImGui.GetWindowSize().X / 2 - ImGui.CalcTextSize(pageTitles[currentPage].ToUpper()).X / 2);
                        ImGui.TextUnformatted(pageTitles[currentPage].ToUpper());
                    }
                    ImGui.SameLine();
                    if (currentPage < pageContents.Count - 1)
                    {
                        Misc.RenderAlignmentToRight(" 》");
                        if (ImGui.Button(" 》"))
                        {
                            _navIndices[navId] = currentPage + 1;
                            currentPage = _navIndices[navId];
                        }
                    }
                }

                // Render current page content
                if (pageContents.Count > currentPage)
                {
                    RenderHtmlElementsNoSameline(pageContents[currentPage], url, image, color, wrapWidth, wrapHeight, isLimited, true, disableWordWrap, limitImageWidth);
                }

                lastIndex = navMatch.Index + navMatch.Length;
                navBlockCount++;
            }

            // Render any content after the last navigation block
            if (lastIndex < text.Length)
            {
                string afterNav = text.Substring(lastIndex);
                if (!string.IsNullOrWhiteSpace(afterNav))
                    RenderHtmlElementsNoSameline(afterNav, url, image, color, wrapWidth, wrapHeight, isLimited, true, disableWordWrap, limitImageWidth);
            }
        }
        private static bool TryParseHexColor(string hex, out Vector4 color)
        {
            color = new Vector4(1, 1, 1, 1);
            if (hex.StartsWith("#"))
                hex = hex.Substring(1);
            if (hex.Length != 6)
                return false;
            if (int.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out int r) &&
                int.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out int g) &&
                int.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out int b))
            {
                color = new Vector4(r / 255f, g / 255f, b / 255f, 1);
                return true;
            }
            return false;
        }
        // ... (other helpers unchanged) ...
        private static void RenderStyledText(string input, TextStyle style)
        {
            int idx = 0;
            int inputLen = input.Length;
            while (idx < inputLen)
            {
                int openTag = input.IndexOf('<', idx);
                if (openTag == -1 || openTag + 1 >= inputLen)
                {
                    // No more tags, render the rest
                    string plain = input.Substring(idx);
                    if (!string.IsNullOrEmpty(plain))
                    {
                        RenderStyledTextSegment(plain, style);
                    }
                    break;
                }

                // Render text before the tag
                if (openTag > idx)
                {
                    string plain = input.Substring(idx, openTag - idx);
                    if (!string.IsNullOrEmpty(plain))
                    {
                        RenderStyledTextSegment(plain, style);
                    }
                }

                // Try to parse a tag
                int closeTag = input.IndexOf('>', openTag + 1);
                if (closeTag == -1)
                {
                    // Malformed tag, render as plain text
                    RenderStyledTextSegment(input.Substring(openTag), style);
                    break;
                }

                string tagName = null;
                string colorHex = null;
                bool isColorTag = false;
                // Support <b>, <i>, <u>, <color hex=xxxxxx>
                if (input[openTag + 1] == 'b' && input.Substring(openTag, closeTag - openTag + 1).StartsWith("<b>"))
                    tagName = "b";
                else if (input[openTag + 1] == 'i' && input.Substring(openTag, closeTag - openTag + 1).StartsWith("<i>"))
                    tagName = "i";
                else if (input[openTag + 1] == 'u' && input.Substring(openTag, closeTag - openTag + 1).StartsWith("<u>"))
                    tagName = "u";
                else if (input.Substring(openTag, closeTag - openTag + 1).StartsWith("<color"))
                {
                    // Parse color hex
                    var hexMatch = Regex.Match(input.Substring(openTag, closeTag - openTag + 1), @"hex\s*=\s*([A-Fa-f0-9]{6})");
                    if (hexMatch.Success)
                    {
                        colorHex = hexMatch.Groups[1].Value;
                        isColorTag = true;
                        tagName = "color";
                    }
                }

                if (tagName == null)
                {
                    // Not a supported tag, render as plain text
                    RenderStyledTextSegment(input.Substring(openTag, closeTag - openTag + 1), style);
                    idx = closeTag + 1;
                    continue;
                }

                // Find closing tag
                string closeTagStr = $"</{tagName}>";
                int closeTagIdx = input.IndexOf(closeTagStr, closeTag + 1, StringComparison.OrdinalIgnoreCase);
                if (closeTagIdx == -1)
                {
                    // Malformed, treat as plain text
                    RenderStyledTextSegment(input.Substring(openTag, closeTag - openTag + 1), style);
                    idx = closeTag + 1;
                    continue;
                }

                // Get inner text
                int innerStart = closeTag + 1;
                int innerLen = closeTagIdx - innerStart;
                string inner = input.Substring(innerStart, innerLen);

                // Update style
                var newStyle = style;
                if (tagName == "b") newStyle.Bold = true;
                else if (tagName == "i") newStyle.Italic = true;
                else if (tagName == "u") newStyle.Underline = true;
                else if (isColorTag && TryParseHexColor(colorHex, out Vector4 colorVal)) newStyle.Color = colorVal;

                // Render inner text (no recursion, just loop)
                RenderStyledText(inner, newStyle);

                idx = closeTagIdx + closeTagStr.Length;
            }
        }

        // Helper to render a segment with style
        private static void RenderStyledTextSegment(string text, TextStyle style)
        {
            int styleFontSize = style.Scale > 0 ? style.Scale : minFontSize;
            if (styleFontSize < minFontSize) styleFontSize = minFontSize;
            ImFontPtr font = GetFontForStyle(style, styleFontSize);

            ImGui.PushFont(font);

            if (style.Color.HasValue)
                ImGui.TextColored(style.Color.Value, text);
            else
                ImGui.TextWrapped(text);

            ImGui.PopFont();

            if (style.Underline)
            {
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                var drawList = ImGui.GetWindowDrawList();
                drawList.AddLine(
                    new Vector2(min.X, max.Y),
                    new Vector2(max.X, max.Y),
                    ImGui.GetColorU32(style.Color ?? new Vector4(1, 1, 1, 1)),
                    2.0f
                );
            }
        }

        // No changes needed for RenderHtmlElementsNoSameline and RenderHtmlElementsNoTable unless you want to support nested tabs/pages inside tables or other elements.
        // If you do, you can add similar tab/page parsing logic to those functions as well.

        private static void RenderHtmlElementsNoSameline(string text, bool url, bool image, bool color, float wrapWidth, float wrapHeight, bool isFirstSegment, bool isLimited, bool disableWordWrap, bool limitImageWidth = false)
        {
            var tableRegex = new Regex(@"<table>(.*?)</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            int lastIndex = 0;
            var tableMatches = tableRegex.Matches(text);

            if (tableMatches.Count == 0)
            {
                RenderHtmlElementsNoTable(text, url, image, color, wrapWidth, wrapHeight, isLimited, isFirstSegment, disableWordWrap, limitImageWidth);
                return;
            }

            bool firstTable = isFirstSegment;
            foreach (Match tableMatch in tableMatches)
            {
                int tableStart = tableMatch.Index;
                if (tableStart > lastIndex)
                {
                    string beforeTable = text.Substring(lastIndex, tableStart - lastIndex);
                    RenderHtmlElementsNoTable(beforeTable, url, image, color, wrapWidth, wrapHeight, isLimited, firstTable, disableWordWrap, limitImageWidth);
                    firstTable = false;
                }

                string tableContent = tableMatch.Groups[1].Value;

                var columns = new List<(string content, string tooltip)>();
                int idx = 0;
                while (idx < tableContent.Length)
                {
                    int colStart = tableContent.IndexOf("<column>", idx, StringComparison.OrdinalIgnoreCase);
                    if (colStart == -1) break;
                    int colEnd = tableContent.IndexOf("</column>", colStart, StringComparison.OrdinalIgnoreCase);
                    if (colEnd == -1) break;
                    int contentStart = colStart + "<column>".Length;
                    string colContent = tableContent.Substring(contentStart, colEnd - contentStart).TrimStart('\r', '\n', ' ', '\t');
                    idx = colEnd + "</column>".Length;

                    // Check for tooltip immediately after column
                    string tooltip = null;
                    var tooltipMatch = new Regex(@"<tooltip>(.*?)</tooltip>", RegexOptions.Singleline | RegexOptions.IgnoreCase)
                        .Match(tableContent, idx);
                    if (tooltipMatch.Success && tooltipMatch.Index == idx)
                    {
                        tooltip = tooltipMatch.Groups[1].Value;
                        idx = tooltipMatch.Index + tooltipMatch.Length;
                    }

                    columns.Add((colContent, tooltip));
                }

                int columnCount = columns.Count;
                if (columnCount > 0 && ImGui.BeginTable("CustomTable" + tableMatch.Index, columnCount, ImGuiTableFlags.None))
                {
                    ImGui.TableNextRow();
                    for (int col = 0; col < columnCount; col++)
                    {
                        ImGui.TableSetColumnIndex(col);

                        var colText = columns[col].content;
                        var tooltip = columns[col].tooltip;
                        ImGui.BeginGroup();
                        float columnWidth = wrapWidth / columnCount;
                        // Use NoSameline to allow nested tags (including <scale>, <img>, etc.)
                        RenderHtmlElementsNoSameline(colText, url, image, color, columnWidth, wrapHeight, isLimited, true, disableWordWrap, limitImageWidth);
                        ImGui.EndGroup();
                        if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.TextUnformatted(tooltip);
                            ImGui.EndTooltip();
                        }
                    }
                    ImGui.EndTable();
                }

                lastIndex = tableMatch.Index + tableMatch.Length;
            }

            if (lastIndex < text.Length)
            {
                string afterTable = text.Substring(lastIndex);
                RenderHtmlElementsNoTable(afterTable, url, image, color, wrapWidth, wrapHeight, isLimited, false, disableWordWrap, limitImageWidth);
            }
        }
        private static void RenderHtmlElementsNoTable(
            string text,
            bool url,
            bool image,
            bool color,
            float wrapWidth,
            float wrapHeight,
            bool isFirstSegment,
            bool isLimited,
            bool disableWordWrap,
            bool limitImageWidth = false)
        {
      
            var scaleBlockRegex = new Regex(@"<scale\s*=\s*""([\d\.]+)""\s*>(.*?)</scale>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var imgRegex = new Regex(@"<(img|image)>(.*?)</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var colorRegex = new Regex(@"<color\s+hex\s*=\s*([A-Fa-f0-9]{6})\s*>(.*?)</color>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var urlRegex = new Regex(@"<url>(.*?)</url>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            // Bare URL regex - matches http/https URLs not wrapped in tags
            // Uses negative lookbehind to skip URLs after > (inside tags)
            var bareUrlRegex = new Regex(@"(?<!>)(https?://[^\s<>""]+)", RegexOptions.IgnoreCase);
            var tooltipRegex = new Regex(@"<tooltip>(.*?)</tooltip>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var boldRegex = new Regex(@"<b>(.*?)</b>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var italicRegex = new Regex(@"<i>(.*?)</i>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var underlineRegex = new Regex(@"<u>(.*?)</u>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var nsfwRegex = new Regex(@"<nsfw>(.*?)</nsfw>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            var segments = new List<(string type, string content, float scale, string colorHex, string url)>();
            int idx = 0;
            while (idx < text.Length)
            {
                if (text.Substring(idx).StartsWith("</"))
                {
                    int closeIdx = text.IndexOf('>', idx);
                    if (closeIdx != -1)
                    {
                        idx = closeIdx + 1;
                        continue;
                    }
                }
                // Find the next tag
                var scaleMatch = scaleBlockRegex.Match(text, idx);
                var imgMatch = imgRegex.Match(text, idx);
                var colorMatch = colorRegex.Match(text, idx);
                var urlMatch = urlRegex.Match(text, idx);
                var bareUrlMatch = bareUrlRegex.Match(text, idx);
                var tooltipMatch = tooltipRegex.Match(text, idx);
                var boldMatch = boldRegex.Match(text, idx);
                var italicMatch = italicRegex.Match(text, idx);
                var underlineMatch = underlineRegex.Match(text, idx);
                var nsfwMatch = nsfwRegex.Match(text, idx);

                // Find the earliest tag
                int nextTagIdx = text.Length;
                string nextType = null;
                Match nextMatch = null;
                if (scaleMatch.Success && scaleMatch.Index < nextTagIdx) { nextTagIdx = scaleMatch.Index; nextType = "scale"; nextMatch = scaleMatch; }
                if (imgMatch.Success && imgMatch.Index < nextTagIdx) { nextTagIdx = imgMatch.Index; nextType = "img"; nextMatch = imgMatch; }
                if (colorMatch.Success && colorMatch.Index < nextTagIdx) { nextTagIdx = colorMatch.Index; nextType = "color"; nextMatch = colorMatch; }
                if (urlMatch.Success && urlMatch.Index < nextTagIdx) { nextTagIdx = urlMatch.Index; nextType = "url"; nextMatch = urlMatch; }
                // Bare URLs should be detected but only if they appear before a <url> tag (avoid double-matching)
                if (bareUrlMatch.Success && bareUrlMatch.Index < nextTagIdx) { nextTagIdx = bareUrlMatch.Index; nextType = "bareurl"; nextMatch = bareUrlMatch; }
                if (tooltipMatch.Success && tooltipMatch.Index < nextTagIdx) { nextTagIdx = tooltipMatch.Index; nextType = "tooltip"; nextMatch = tooltipMatch; }
                if (boldMatch.Success && boldMatch.Index < nextTagIdx) { nextTagIdx = boldMatch.Index; nextType = "bold"; nextMatch = boldMatch; }
                if (italicMatch.Success && italicMatch.Index < nextTagIdx) { nextTagIdx = italicMatch.Index; nextType = "italic"; nextMatch = italicMatch; }
                if (underlineMatch.Success && underlineMatch.Index < nextTagIdx) { nextTagIdx = underlineMatch.Index; nextType = "underline"; nextMatch = underlineMatch; }
                if (nsfwMatch.Success && nsfwMatch.Index < nextTagIdx) { nextTagIdx = nsfwMatch.Index; nextType = "nsfw"; nextMatch = nsfwMatch; }

                // Add only the text before the tag
                if (nextTagIdx > idx)
                {
                    string plainText = text.Substring(idx, nextTagIdx - idx);
                    if (!string.IsNullOrEmpty(plainText))
                        segments.Add(("text", plainText, 1.0f, null, null));
                }

                if (nextMatch == null)
                    break;

                // Handle the tag and skip over it
                if (nextType == "scale")
                {
                    float scale = 1.0f;
                    float.TryParse(nextMatch.Groups[1].Value, out scale);
                    if (scale < minFontSize) scale = minFontSize;
                    string scaleContent = nextMatch.Groups[2].Value;
                    // Recursively parse scaleContent for tags
                    int scaleIdx = 0;
                    while (scaleIdx < scaleContent.Length)
                    {
                        var imgMatch2 = imgRegex.Match(scaleContent, scaleIdx);
                        var colorMatch2 = colorRegex.Match(scaleContent, scaleIdx);
                        var urlMatch2 = urlRegex.Match(scaleContent, scaleIdx);
                        var bareUrlMatch2 = bareUrlRegex.Match(scaleContent, scaleIdx);
                        var tooltipMatch2 = tooltipRegex.Match(scaleContent, scaleIdx);
                        var boldMatch2 = boldRegex.Match(scaleContent, scaleIdx);
                        var italicMatch2 = italicRegex.Match(scaleContent, scaleIdx);
                        var underlineMatch2 = underlineRegex.Match(scaleContent, scaleIdx);
                        var nsfwMatch2 = nsfwRegex.Match(scaleContent, scaleIdx);

                        int nextTagIdx2 = scaleContent.Length;
                        string nextType2 = null;
                        Match nextMatch2 = null;
                        if (imgMatch2.Success && imgMatch2.Index < nextTagIdx2) { nextTagIdx2 = imgMatch2.Index; nextType2 = "img"; nextMatch2 = imgMatch2; }
                        if (colorMatch2.Success && colorMatch2.Index < nextTagIdx2) { nextTagIdx2 = colorMatch2.Index; nextType2 = "color"; nextMatch2 = colorMatch2; }
                        if (urlMatch2.Success && urlMatch2.Index < nextTagIdx2) { nextTagIdx2 = urlMatch2.Index; nextType2 = "url"; nextMatch2 = urlMatch2; }
                        if (bareUrlMatch2.Success && bareUrlMatch2.Index < nextTagIdx2) { nextTagIdx2 = bareUrlMatch2.Index; nextType2 = "bareurl"; nextMatch2 = bareUrlMatch2; }
                        if (tooltipMatch2.Success && tooltipMatch2.Index < nextTagIdx2) { nextTagIdx2 = tooltipMatch2.Index; nextType2 = "tooltip"; nextMatch2 = tooltipMatch2; }
                        if (boldMatch2.Success && boldMatch2.Index < nextTagIdx2) { nextTagIdx2 = boldMatch2.Index; nextType2 = "bold"; nextMatch2 = boldMatch2; }
                        if (italicMatch2.Success && italicMatch2.Index < nextTagIdx2) { nextTagIdx2 = italicMatch2.Index; nextType2 = "italic"; nextMatch2 = italicMatch2; }
                        if (underlineMatch2.Success && underlineMatch2.Index < nextTagIdx2) { nextTagIdx2 = underlineMatch2.Index; nextType2 = "underline"; nextMatch2 = underlineMatch2; }
                        if (nsfwMatch2.Success && nsfwMatch2.Index < nextTagIdx2) { nextTagIdx2 = nsfwMatch2.Index; nextType2 = "nsfw"; nextMatch2 = nsfwMatch2; }

                        if (nextTagIdx2 > scaleIdx)
                        {
                            string plainText2 = scaleContent.Substring(scaleIdx, nextTagIdx2 - scaleIdx);
                            if (!string.IsNullOrEmpty(plainText2))
                                segments.Add(("text", plainText2, scale, null, null));
                        }

                        if (nextMatch2 == null)
                            break;

                        if (nextType2 == "img")
                        {
                            string imgUrl = nextMatch2.Groups[2].Value.Trim();
                            float imgScale = scale < 0.01f ? 0.01f : scale;
                            segments.Add(("img", imgUrl, imgScale, null, null));
                            scaleIdx = nextMatch2.Index + nextMatch2.Length;
                        }
                        else if (nextType2 == "color")
                        {
                            string colorContent = nextMatch2.Groups[2].Value;
                            string colorHex = nextMatch2.Groups[1].Value;
                            segments.Add(("color", colorContent, scale, colorHex, null));
                            scaleIdx = nextMatch2.Index + nextMatch2.Length;
                        }
                        else if (nextType2 == "url")
                        {
                            string urlContent = nextMatch2.Groups[1].Value;
                            segments.Add(("url", urlContent, scale, null, urlContent));
                            scaleIdx = nextMatch2.Index + nextMatch2.Length;
                        }
                        else if (nextType2 == "bareurl")
                        {
                            // Bare URL without <url> tags
                            string urlContent = nextMatch2.Groups[1].Value;
                            segments.Add(("url", urlContent, scale, null, urlContent));
                            scaleIdx = nextMatch2.Index + nextMatch2.Length;
                        }
                        else if (nextType2 == "tooltip")
                        {
                            segments.Add(("tooltip", nextMatch2.Groups[1].Value, scale, null, null));
                            scaleIdx = nextMatch2.Index + nextMatch2.Length;
                        }
                        else if (nextType2 == "bold")
                        {
                            segments.Add(("bold", nextMatch2.Groups[1].Value, scale, null, null));
                            scaleIdx = nextMatch2.Index + nextMatch2.Length;
                        }
                        else if (nextType2 == "italic")
                        {
                            segments.Add(("italic", nextMatch2.Groups[1].Value, scale, null, null));
                            scaleIdx = nextMatch2.Index + nextMatch2.Length;
                        }
                        else if (nextType2 == "underline")
                        {
                            segments.Add(("underline", nextMatch2.Groups[1].Value, scale, null, null));
                            scaleIdx = nextMatch2.Index + nextMatch2.Length;
                        }
                        else if (nextType2 == "nsfw")
                        {
                            string nsfwContent2 = nextMatch2.Groups[1].Value;
                            string nsfwId2 = $"nsfw_{_currentNsfwSessionId}_{nsfwContent2.GetHashCode()}_{scaleIdx}";
                            segments.Add(("nsfw", nsfwContent2, scale, nsfwId2, null));
                            scaleIdx = nextMatch2.Index + nextMatch2.Length;
                        }
                    }
                    idx = nextMatch.Index + nextMatch.Length;
                }
                else if (nextType == "img")
                {
                    string imgUrl = nextMatch.Groups[2].Value.Trim();
                    segments.Add(("img", imgUrl, 1.0f, null, null));
                    idx = nextMatch.Index + nextMatch.Length;
                }
                else if (nextType == "color")
                {
                    string colorContent = nextMatch.Groups[2].Value;
                    string colorHex = nextMatch.Groups[1].Value;
                    segments.Add(("color", colorContent, 1.0f, colorHex, null));
                    idx = nextMatch.Index + nextMatch.Length;
                }
                else if (nextType == "url")
                {
                    string urlContent = nextMatch.Groups[1].Value;
                    segments.Add(("url", urlContent, 1.0f, null, urlContent));
                    idx = nextMatch.Index + nextMatch.Length;
                }
                else if (nextType == "bareurl")
                {
                    // Bare URL without <url> tags - treat the same as tagged URL
                    string urlContent = nextMatch.Groups[1].Value;
                    segments.Add(("url", urlContent, 1.0f, null, urlContent));
                    idx = nextMatch.Index + nextMatch.Length;
                }
                else if (nextType == "tooltip")
                {
                    segments.Add(("tooltip", nextMatch.Groups[1].Value, 1.0f, null, null));
                    idx = nextMatch.Index + nextMatch.Length;
                }
                else if (nextType == "bold")
                {
                    segments.Add(("bold", nextMatch.Groups[1].Value, 1.0f, null, null));
                    idx = nextMatch.Index + nextMatch.Length;
                }
                else if (nextType == "italic")
                {
                    segments.Add(("italic", nextMatch.Groups[1].Value, 1.0f, null, null));
                    idx = nextMatch.Index + nextMatch.Length;
                }
                else if (nextType == "underline")
                {
                    segments.Add(("underline", nextMatch.Groups[1].Value, 1.0f, null, null));
                    idx = nextMatch.Index + nextMatch.Length;
                }
                else if (nextType == "nsfw")
                {
                    // Store the NSFW content with a unique ID based on content hash, position, and session
                    string nsfwContent = nextMatch.Groups[1].Value;
                    string nsfwId = $"nsfw_{_currentNsfwSessionId}_{nsfwContent.GetHashCode()}_{nextMatch.Index}";
                    segments.Add(("nsfw", nsfwContent, 1.0f, nsfwId, null));
                    idx = nextMatch.Index + nextMatch.Length;
                }
            }

            string pendingTooltip = null;
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];

                // Image rendering (clamped)
                if (seg.type == "img" && image)
                {
                    string imgUrl = seg.content;
                    float imageScale = seg.scale < 0.01f ? 0.01f : seg.scale;

                    // Try to get cached texture safely with thread synchronization
                    IDalamudTextureWrap texture = null;
                    bool textureValid = false;
                    bool isLoading = false;
                    try
                    {
                        lock (_imageCacheLock)
                        {
                            if (_imageCache.TryGetValue(imgUrl, out texture) && texture != null)
                            {
                                var handle = texture.Handle;
                                textureValid = handle != default && texture.Width > 0 && texture.Height > 0;
                            }
                            isLoading = _imagesLoading.Contains(imgUrl);
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        textureValid = false;
                    }

                    if (textureValid && texture != null)
                    {
                        Vector2 imgSize = new Vector2(texture.Width, texture.Height) * imageScale;

                        // Additional scaling logic
                        if (disableWordWrap)
                        {
                            float maxWidth = 400f;
                            if (imgSize.X > maxWidth && imgSize.X > 0)
                            {
                                float scaleDown = maxWidth / imgSize.X;
                                imgSize *= scaleDown;
                            }
                        }
                        else if (isLimited)
                        {
                            float maxWidth = wrapWidth;
                            float maxHeight = wrapHeight;
                            if (imgSize.X > 0 && imgSize.Y > 0)
                            {
                                float widthScale = maxWidth / imgSize.X;
                                float heightScale = maxHeight / imgSize.Y;
                                float finalScale = Math.Min(widthScale, heightScale);
                                imgSize *= finalScale;
                            }
                        }
                        else if (limitImageWidth)
                        {
                            // Limit image width to 65% of chat window width and height to 50%
                            float maxWidth = wrapWidth * 0.65f;
                            float maxHeight = wrapHeight * 0.5f;
                            if (imgSize.X > 0 && imgSize.Y > 0)
                            {
                                float widthScale = imgSize.X > maxWidth ? maxWidth / imgSize.X : 1.0f;
                                float heightScale = imgSize.Y > maxHeight ? maxHeight / imgSize.Y : 1.0f;
                                float finalScale = Math.Min(widthScale, heightScale);
                                imgSize *= finalScale;
                            }
                        }
                        else
                        {
                            float maxWidth = wrapWidth;
                            if (imgSize.X > maxWidth && imgSize.X > 0)
                            {
                                float scaleDown = maxWidth / imgSize.X;
                                imgSize *= scaleDown;
                            }
                        }

                        // Clamp to minimum image size
                        imgSize.X = Math.Max(imgSize.X, minImageSize);
                        imgSize.Y = Math.Max(imgSize.Y, minImageSize);

                        if (imgSize.X > 0 && imgSize.Y > 0)
                        {
                            try
                            {
                                var handle = texture.Handle;
                                if (handle != default)
                                {
                                    ImGui.Image(handle, imgSize);

                                    // Check if image should be clickable for preview
                                    // Only allow preview if limitImageWidth is true (chat context)
                                    // and if the image is not inside a hidden nsfw section
                                    if (limitImageWidth && ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip("Click to preview full image");
                                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                                        {
                                            // Open image preview window
                                            Windows.Ect.ImagePreview.PreviewImage = texture;
                                            Windows.Ect.ImagePreview.width = texture.Width;
                                            Windows.Ect.ImagePreview.height = texture.Height;
                                            Windows.Ect.ImagePreview.WindowOpen = true;
                                            Plugin.plugin.OpenImagePreview();
                                        }
                                    }
                                }
                            }
                            catch (ObjectDisposedException)
                            {
                                // Texture was disposed, skip rendering
                            }
                        }
                        else
                        {
                            ImGui.TextColored(new Vector4(1, 1, 0, 1), "[Image size invalid]");
                        }
                    }
                    else
                    {
                        if (!isLoading)
                        {
                            lock (_imageCacheLock)
                            {
                                // Double-check after acquiring lock
                                if (!_imagesLoading.Contains(imgUrl))
                                {
                                    _imagesLoading.Add(imgUrl);
                                }
                                else
                                {
                                    isLoading = true;
                                }
                            }

                            if (!isLoading)
                            {
                                // Use async void for fire-and-forget image loading
                                async void LoadImageAsync(string url)
                                {
                                    try
                                    {
                                        using (var webClient = new System.Net.WebClient())
                                        {
                                            var imageBytes = await webClient.DownloadDataTaskAsync(url);
                                            var tex = await Plugin.TextureProvider.CreateFromImageAsync(imageBytes);
                                            lock (_imageCacheLock)
                                            {
                                                if (tex != null && tex.Handle != default &&
                                                    tex.Width > 0 && tex.Height > 0)
                                                {
                                                    _imageCache[url] = tex;
                                                }
                                                else
                                                {
                                                    _imageCache[url] = null;
                                                }
                                            }
                                        }
                                    }
                                    catch
                                    {
                                        lock (_imageCacheLock)
                                        {
                                            _imageCache[url] = null;
                                        }
                                    }
                                    finally
                                    {
                                        lock (_imageCacheLock)
                                        {
                                            _imagesLoading.Remove(url);
                                        }
                                    }
                                }
                                LoadImageAsync(imgUrl);
                            }
                        }
                        ImGui.TextColored(new Vector4(1, 1, 0, 1), "[Loading image or image failed!]");
                    }
                    continue;
                }
   

                // Font rendering (clamped)
                if (seg.type == "text")
                {
                    int fontSize = (int)Math.Round(seg.scale);
                    if (fontSize < minFontSize) fontSize = minFontSize;
                    ImFontPtr font = null;
                    if (FontSizes.TryGetValue(fontSize, out font) && !font.IsNull)
                    {
                        ImGui.PushFont(font);
                    }

                    if (disableWordWrap)
                        ImGui.TextUnformatted(seg.content);
                    else
                        ImGui.TextWrapped(seg.content);

                    if (!font.IsNull)
                        ImGui.PopFont();

                    continue;
                }

                // Colored text
                if (seg.type == "color" && color && seg.colorHex != null)
                {
                    if (TryParseHexColor(seg.colorHex, out Vector4 colorVal))
                    {
                        if (disableWordWrap)
                            ImGui.TextColored(colorVal, seg.content);
                        else
                        {
                            string wrapped = WrapTextToFit(seg.content, wrapWidth);
                            foreach (var line in wrapped.Split('\n'))
                                ImGui.TextColored(colorVal, line);
                        }
                    }
                    else
                    {
                        if (disableWordWrap)
                            ImGui.TextUnformatted(seg.content);
                        else
                            ImGui.TextWrapped(seg.content);
                    }
                    continue;
                }

                else if (seg.type == "url" && url && seg.url != null)
                {
                    // Check if this is a YouTube URL
                    if (TryGetYoutubeVideoId(seg.url, out string videoId))
                    {
                        // Render YouTube video embed
                        RenderYoutubeEmbed(seg.url, videoId, wrapWidth > 0 ? wrapWidth : 320f);
                    }
                    // Check if this is an audio URL
                    else if (IsAudioUrl(seg.url))
                    {
                        // Render audio player embed
                        RenderAudioEmbed(seg.url, wrapWidth > 0 ? wrapWidth : 320f);
                    }
                    // Check if this is an image URL (in case it wasn't wrapped in <img> tags)
                    else if (IsImageUrl(seg.url) && image)
                    {
                        // Render as image - reuse the same image rendering logic
                        string imgUrl = seg.url;
                        float imageScale = 1.0f;

                        IDalamudTextureWrap texture = null;
                        bool textureValid = false;
                        bool isLoading = false;
                        try
                        {
                            lock (_imageCacheLock)
                            {
                                if (_imageCache.TryGetValue(imgUrl, out texture) && texture != null)
                                {
                                    var handle = texture.Handle;
                                    textureValid = handle != default && texture.Width > 0 && texture.Height > 0;
                                }
                                isLoading = _imagesLoading.Contains(imgUrl);
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            textureValid = false;
                        }

                        if (textureValid && texture != null)
                        {
                            Vector2 imgSize = new Vector2(texture.Width, texture.Height) * imageScale;

                            // Limit image width for chat context
                            if (limitImageWidth)
                            {
                                float maxWidth = wrapWidth * 0.65f;
                                float maxHeight = wrapHeight * 0.5f;
                                if (imgSize.X > 0 && imgSize.Y > 0)
                                {
                                    float widthScale = imgSize.X > maxWidth ? maxWidth / imgSize.X : 1.0f;
                                    float heightScale = imgSize.Y > maxHeight ? maxHeight / imgSize.Y : 1.0f;
                                    float finalScale = Math.Min(widthScale, heightScale);
                                    imgSize *= finalScale;
                                }
                            }
                            else
                            {
                                float maxWidth = wrapWidth;
                                if (imgSize.X > maxWidth && imgSize.X > 0)
                                {
                                    float scaleDown = maxWidth / imgSize.X;
                                    imgSize *= scaleDown;
                                }
                            }

                            imgSize.X = Math.Max(imgSize.X, minImageSize);
                            imgSize.Y = Math.Max(imgSize.Y, minImageSize);

                            if (imgSize.X > 0 && imgSize.Y > 0)
                            {
                                try
                                {
                                    var handle = texture.Handle;
                                    if (handle != default)
                                    {
                                        ImGui.Image(handle, imgSize);

                                        if (limitImageWidth && ImGui.IsItemHovered())
                                        {
                                            ImGui.SetTooltip("Click to preview full image");
                                            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                                            {
                                                Windows.Ect.ImagePreview.PreviewImage = texture;
                                                Windows.Ect.ImagePreview.width = texture.Width;
                                                Windows.Ect.ImagePreview.height = texture.Height;
                                                Windows.Ect.ImagePreview.WindowOpen = true;
                                                Plugin.plugin.OpenImagePreview();
                                            }
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                        else if (!isLoading)
                        {
                            lock (_imageCacheLock)
                            {
                                if (!_imagesLoading.Contains(imgUrl))
                                {
                                    _imagesLoading.Add(imgUrl);
                                    // Fire-and-forget image loading
                                    async void LoadImageForUrlSegment(string loadUrl)
                                    {
                                        try
                                        {
                                            using (var webClient = new System.Net.WebClient())
                                            {
                                                var imageBytes = await webClient.DownloadDataTaskAsync(loadUrl);
                                                var tex = await Plugin.TextureProvider.CreateFromImageAsync(imageBytes);
                                                lock (_imageCacheLock)
                                                {
                                                    if (tex != null && tex.Handle != default && tex.Width > 0 && tex.Height > 0)
                                                        _imageCache[loadUrl] = tex;
                                                    else
                                                        _imageCache[loadUrl] = null;
                                                }
                                            }
                                        }
                                        catch
                                        {
                                            lock (_imageCacheLock) { _imageCache[loadUrl] = null; }
                                        }
                                        finally
                                        {
                                            lock (_imageCacheLock) { _imagesLoading.Remove(loadUrl); }
                                        }
                                    }
                                    LoadImageForUrlSegment(imgUrl);
                                }
                            }
                            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "[Loading image...]");
                        }
                        else
                        {
                            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "[Loading image...]");
                        }
                    }
                    else
                    {
                        // Regular URL - render as clickable link with visual feedback
                        string wrapped = WrapTextToFit(seg.content, wrapWidth);
                        foreach (var line in wrapped.Split('\n'))
                        {
                            var isHovered = false;

                            // Blue text for links
                            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.2f, 0.5f, 1f, 1f));
                            ImGui.Text(line);
                            ImGui.PopStyleColor();

                            isHovered = ImGui.IsItemHovered();

                            // Draw underline (always visible for links, brighter when hovered)
                            var min = ImGui.GetItemRectMin();
                            var max = ImGui.GetItemRectMax();
                            var drawList = ImGui.GetWindowDrawList();
                            var underlineColor = isHovered
                                ? ImGui.GetColorU32(new Vector4(0.4f, 0.7f, 1f, 1f))  // Brighter blue when hovered
                                : ImGui.GetColorU32(new Vector4(0.2f, 0.5f, 1f, 0.5f)); // Dimmer underline normally
                            drawList.AddLine(
                                new Vector2(min.X, max.Y),
                                new Vector2(max.X, max.Y),
                                underlineColor,
                                1.0f);

                            // Show tooltip with full URL on hover
                            if (isHovered)
                            {
                                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                                ImGui.BeginTooltip();
                                ImGui.Text(seg.url);
                                ImGui.EndTooltip();

                                // Handle click
                                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                                {
                                    Misc.LoadUrl = true;
                                    Misc.UrlToLoad = seg.url;
                                }
                            }
                        }
                    }
                }
                else if (seg.type == "bold" || seg.type == "italic" || seg.type == "underline" || seg.type == "text")
                {
                    var style = new TextStyle
                    {
                        Bold = seg.type == "bold",
                        Italic = seg.type == "italic",
                        Underline = seg.type == "underline",
                        Scale = (int)Math.Round(seg.scale) // <-- Use scale here!
                    };
                    int fontSize = style.Scale > 0 ? style.Scale : minFontSize;
                    if (fontSize < minFontSize) fontSize = minFontSize;
                    ImFontPtr font = GetFontForStyle(style, fontSize);
                    ImGui.PushFont(font);

                    if (seg.colorHex != null && color && TryParseHexColor(seg.colorHex, out Vector4 colorVal))
                        ImGui.TextColored(colorVal, seg.content);
                    else
                        ImGui.TextWrapped(seg.content);

                    ImGui.PopFont();

                    if (style.Underline)
                    {
                        var min = ImGui.GetItemRectMin();
                        var max = ImGui.GetItemRectMax();
                        var drawList = ImGui.GetWindowDrawList();
                        drawList.AddLine(
                            new Vector2(min.X, max.Y),
                            new Vector2(max.X, max.Y),
                            ImGui.GetColorU32(new Vector4(1, 1, 1, 1)),
                            2.0f
                        );
                    }
                    continue;
                }

                // Tooltip
                if (seg.type == "tooltip")
                {
                    pendingTooltip = seg.content;
                    continue;
                }

                // NSFW spoiler content
                if (seg.type == "nsfw")
                {
                    string nsfwId = seg.colorHex; // We stored the unique ID in colorHex field
                    bool isRevealed = _revealedNsfwSections.Contains(nsfwId);

                    if (isRevealed)
                    {
                        // Content is revealed - render it normally by recursively calling RenderHtmlElementsNoTable
                        RenderHtmlElementsNoTable(seg.content, url, image, color, wrapWidth, wrapHeight, isFirstSegment, isLimited, disableWordWrap, limitImageWidth);
                    }
                    else
                    {
                        // Content is hidden - render with blur/spoiler effect
                        ImGui.PushID(nsfwId);

                        // Calculate approximate size of the hidden content
                        Vector2 contentSize = CalculateNsfwContentSize(seg.content, wrapWidth, image);

                        // Ensure minimum size
                        contentSize.X = Math.Max(contentSize.X, 100f);
                        contentSize.Y = Math.Max(contentSize.Y, 24f);

                        // Get cursor position for drawing
                        Vector2 cursorPos = ImGui.GetCursorScreenPos();

                        // Create an invisible button for click detection
                        bool clicked = ImGui.InvisibleButton($"##nsfw_reveal_{nsfwId}", contentSize);

                        // Draw the spoiler overlay
                        var drawList = ImGui.GetWindowDrawList();
                        Vector2 rectMin = cursorPos;
                        Vector2 rectMax = new Vector2(cursorPos.X + contentSize.X, cursorPos.Y + contentSize.Y);

                        // Dark background with pattern
                        drawList.AddRectFilled(rectMin, rectMax, ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.15f, 0.95f)));

                        // Add diagonal lines pattern to simulate blur/hidden effect
                        uint lineColor = ImGui.GetColorU32(new Vector4(0.25f, 0.25f, 0.25f, 0.8f));
                        float lineSpacing = 8f;
                        for (float offset = 0; offset < contentSize.X + contentSize.Y; offset += lineSpacing)
                        {
                            Vector2 start = new Vector2(
                                Math.Max(rectMin.X, rectMin.X + offset - contentSize.Y),
                                Math.Min(rectMax.Y, rectMin.Y + offset)
                            );
                            Vector2 end = new Vector2(
                                Math.Min(rectMax.X, rectMin.X + offset),
                                Math.Max(rectMin.Y, rectMin.Y + offset - contentSize.X)
                            );
                            if (start.X < rectMax.X && end.Y < rectMax.Y)
                                drawList.AddLine(start, end, lineColor, 1.0f);
                        }

                        // Border
                        drawList.AddRect(rectMin, rectMax, ImGui.GetColorU32(new Vector4(0.5f, 0.3f, 0.3f, 1f)), 4f, ImDrawFlags.None, 2f);

                        // Center text "NSFW - Click to reveal"
                        string spoilerText = "NSFW - Click to reveal";
                        Vector2 textSize = ImGui.CalcTextSize(spoilerText);
                        Vector2 textPos = new Vector2(
                            rectMin.X + (contentSize.X - textSize.X) / 2f,
                            rectMin.Y + (contentSize.Y - textSize.Y) / 2f
                        );
                        drawList.AddText(textPos, ImGui.GetColorU32(new Vector4(0.8f, 0.5f, 0.5f, 1f)), spoilerText);

                        // Handle click to reveal
                        if (clicked)
                        {
                            _revealedNsfwSections.Add(nsfwId);
                        }

                        // Tooltip on hover
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Click to reveal hidden content");
                        }

                        ImGui.PopID();
                    }
                    continue;
                }
            }
        }

        /// <summary>
        /// Calculates the approximate size of NSFW content for the spoiler overlay
        /// </summary>
        private static Vector2 CalculateNsfwContentSize(string content, float wrapWidth, bool checkImages)
        {
            float totalHeight = 0f;
            float maxWidth = 0f;

            // Check for images in the content
            var imgRegex = new Regex(@"<(img|image)>(.*?)</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var imgMatches = imgRegex.Matches(content);

            foreach (Match imgMatch in imgMatches)
            {
                string imgUrl = imgMatch.Groups[2].Value.Trim();
                IDalamudTextureWrap texture = null;
                lock (_imageCacheLock)
                {
                    _imageCache.TryGetValue(imgUrl, out texture);
                }
                if (texture != null)
                {
                    try
                    {
                        // Use actual image size (scaled to fit)
                        float imgWidth = Math.Min(texture.Width, wrapWidth);
                        float imgHeight = texture.Height * (imgWidth / texture.Width);
                        totalHeight += imgHeight + 4f; // Add spacing
                        maxWidth = Math.Max(maxWidth, imgWidth);
                    }
                    catch (ObjectDisposedException)
                    {
                        // Texture was disposed, use default size
                        totalHeight += 150f;
                        maxWidth = Math.Max(maxWidth, 200f);
                    }
                }
                else
                {
                    // Default image placeholder size
                    totalHeight += 150f;
                    maxWidth = Math.Max(maxWidth, 200f);
                }
            }

            // Strip HTML tags to get plain text for measurement
            string plainText = Regex.Replace(content, @"<[^>]+>", "");
            if (!string.IsNullOrWhiteSpace(plainText))
            {
                string wrapped = WrapTextToFit(plainText, wrapWidth);
                var lines = wrapped.Split('\n');
                float lineHeight = ImGui.GetTextLineHeightWithSpacing();
                totalHeight += lines.Length * lineHeight;
                foreach (var line in lines)
                {
                    maxWidth = Math.Max(maxWidth, ImGui.CalcTextSize(line).X);
                }
            }

            // Clamp to reasonable bounds
            maxWidth = Math.Min(maxWidth, wrapWidth);
            totalHeight = Math.Min(totalHeight, 600f);

            return new Vector2(maxWidth, totalHeight);
        }

        /// <summary>
        /// Resets all revealed NSFW sections (call when switching profiles/views)
        /// </summary>
        public static void ResetNsfwRevealStates()
        {
            _revealedNsfwSections.Clear();
        }

        /// <summary>
        /// Sets a new NSFW session ID. Call this when loading new content (profile, chat messages, etc.)
        /// to ensure NSFW content is re-spoilered on refresh.
        /// </summary>
        public static void SetNsfwSession(string sessionId)
        {
            if (_currentNsfwSessionId != sessionId)
            {
                _currentNsfwSessionId = sessionId;
                _revealedNsfwSections.Clear();
            }
        }

        public static void RenderUrlModalPopup()
        {
            if (LoadUrl && !showUrlPopup && !string.IsNullOrEmpty(UrlToLoad))
            {
                ImGui.OpenPopup("Opening URL");
                showUrlPopup = true;
            }

            bool wasOpen = showUrlPopup;
            if (ImGui.BeginPopupModal("Opening URL", ref showUrlPopup, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text("Do you want to open this link?");
                ImGui.Checkbox("Allow Link", ref allow);

                if (allow)
                {
                    if (ImGui.Button("I Trust URL"))
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = UrlToLoad,
                                UseShellExecute = true
                            });
                        }
                        catch { }
                        UrlToLoad = string.Empty;
                        LoadUrl = false;
                        allow = false;
                        showUrlPopup = false;
                        ImGui.CloseCurrentPopup();
                    }
                }

                if (ImGui.Button("Cancel"))
                {
                    UrlToLoad = string.Empty;
                    LoadUrl = false;
                    allow = false;
                    showUrlPopup = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            // Handle closing via the "X" button
            if (wasOpen && !showUrlPopup)
            {
                UrlToLoad = string.Empty;
                LoadUrl = false;
                allow = false;
            }
        }
        private static void RenderHtmlTextSegment(string text, bool url, bool color, float wrapWidth)
        {
            var htmlDoc = new HtmlAgilityPack.HtmlDocument();
            htmlDoc.LoadHtml(WrapTextToFit(text, wrapWidth));

            bool nextSameLine = false;
            foreach (var node in htmlDoc.DocumentNode.ChildNodes)
            {
                string nodeText = node.InnerText.Replace("\r", "");
                string[] lines = nodeText.Split('\n');

                for (int i = 0; i < lines.Length; i++)
                {
                    if (nextSameLine)
                    {
                        ImGui.SameLine(0, 0);
                        nextSameLine = false;
                    }
                    if (node.Name == "color" && color && node.Attributes["hex"] != null)
                    {
                        var hexColor = node.Attributes["hex"].Value;
                        if (TryParseHexColor(hexColor, out Vector4 colorVal))
                            ImGui.TextColored(colorVal, lines[i]);
                        else
                            ImGui.TextUnformatted(lines[i]);
                    }
                    else if (node.Name == "url" && url)
                    {
                        string urlText = lines[i].Trim();
                        if (!string.IsNullOrWhiteSpace(urlText))
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.2f, 0.5f, 1f, 1f));
                            ImGui.Text(urlText);
                            ImGui.PopStyleColor();

                            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                            {
                                try
                                {
                                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                    {
                                        FileName = urlText,
                                        UseShellExecute = true
                                    });
                                }
                                catch { }
                            }
                        }
                    }
                    else if (node.Name == "sameline")
                    {
                        nextSameLine = true;
                        // Render the content of the sameline node inline
                        RenderHtmlTextSegment(node.InnerHtml, url, color, wrapWidth);
                    }
                    else
                    {
                        ImGui.TextUnformatted(lines[i]);
                    }
                }
            }
        }
        //sets position of content to center
        public static void RenderHtmlColoredTextInline(string text, float? overrideWrapWidth = null)
        {
            // Get the available width for wrapping (subtract a little for padding if needed)
            float wrapWidth = overrideWrapWidth ?? (ImGui.GetWindowSize().X - 10);

            string wrappedText = WrapTextToFit(text, wrapWidth);

            var htmlDoc = new HtmlAgilityPack.HtmlDocument();
            htmlDoc.LoadHtml(wrappedText);

            bool first = true;
            foreach (var node in htmlDoc.DocumentNode.ChildNodes)
            {
                string nodeText = node.InnerText.Replace("\r", "");
                string[] lines = nodeText.Split('\n');

                for (int i = 0; i < lines.Length; i++)
                {
                    // Only use SameLine if not the first segment and not after a line break
                    if (!first) ImGui.SameLine(0, 0);

                    if (node.Name == "color" && node.Attributes["hex"] != null)
                    {
                        var hexColor = node.Attributes["hex"].Value;
                        if (TryParseHexColor(hexColor, out Vector4 color))
                            ImGui.TextColored(color, lines[i]);
                        else
                            ImGui.TextUnformatted(lines[i]);
                    }
                    else
                    {
                        ImGui.TextUnformatted(lines[i]);
                    }

                    // If this is a line break, reset first so next segment starts a new line
                    first = (i < lines.Length - 1);
                }
            }
        }
        public static void DrawCenteredImage(IDalamudTextureWrap texture, Vector2 size, bool useFullWindowWidth = true)
        {
            if (texture == null)
            {
                ImGui.TextColored(new Vector4(1f, 1f, 0f, 1f), "[Image missing]");
                return;
            }

            try
            {
                var handle = texture.Handle;
                if (handle == default)
                {
                    ImGui.TextColored(new Vector4(1f, 1f, 0f, 1f), "[Image missing]");
                    return;
                }

                // Determine available width to center within
                float availWidth;
                if (useFullWindowWidth)
                {
                    var windowSize = ImGui.GetWindowSize();
                    var padding = ImGui.GetStyle().WindowPadding;
                    availWidth = Math.Max(0f, windowSize.X - padding.X * 2f);
                    // Cursor X for window content starts at WindowPadding.X
                    float contentStartX = padding.X;
                    float centeredX = contentStartX + Math.Max(0f, (availWidth - size.X) / 2f);
                    ImGui.SetCursorPosX(centeredX);
                }
                else
                {
                    // Center within remaining content region from current cursor
                    availWidth = ImGui.GetContentRegionAvail().X;
                    float centeredX = ImGui.GetCursorPosX() + Math.Max(0f, (availWidth - size.X) / 2f);
                    ImGui.SetCursorPosX(centeredX);
                }

                ImGui.Image(handle, size);
            }
            catch (ObjectDisposedException)
            {
                ImGui.TextColored(new Vector4(1f, 1f, 0f, 1f), "[Image disposed]");
            }
        }
        public static void SetCenter(Plugin plugin, string name)
        {
         
                int NameWidth = name.Length * 6;
                var decidingWidth = Math.Max(500, ImGui.GetWindowWidth());
                var offsetWidth = (decidingWidth - NameWidth) / 2;
                var offsetVersion = name.Length > 0
                    ? _modVersionWidth + ImGui.GetStyle().ItemSpacing.X + ImGui.GetStyle().WindowPadding.X
                    : 0;
                var offset = Math.Max(offsetWidth, offsetVersion);
                if (offset > 0)
                {
                    ImGui.SetCursorPosX(offset);
                }
        }
        public static string ExtractTextBetweenTags(string input, string tag)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(tag))
                return null;

            string pattern = $@"<{tag}>(.*?)</{tag}>";
            Match match = Regex.Match(input, pattern, RegexOptions.Singleline);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return null; // Return null if the tag is not found
        }
        public static void DrawCenteredInput(float center, Vector2 size, string label, string hint, ref string input, int length, ImGuiInputTextFlags flags)
        {
            var currentCursorY = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(new Vector2(center, currentCursorY));
            ImGui.PushItemWidth(size.X);
            ImGui.InputTextWithHint(label, hint, ref input, length, flags & (string.IsNullOrEmpty(input) ? ~ImGuiInputTextFlags.Password : ~ImGuiInputTextFlags.None));
            ImGui.PopItemWidth();
        }
        public static bool DrawXCenteredInput(string label, string id, ref string input, int length)
        {
            var size = ImGui.CalcTextSize(label).X + 400;

            var windowSize = ImGui.GetWindowSize();

            // Set the cursor position to center the button horizontally
            var currentCursorY = ImGui.GetCursorPosY();
            float centeredX = (ImGui.GetContentRegionAvail().X - size) / 2.0f;
            ImGui.SetCursorPos(new Vector2(centeredX, currentCursorY));
            ImGui.Text(label);
            ImGui.SameLine();
            ImGui.PushItemWidth(350);
            var centeredInput = ImGui.InputText("##ID" + id, ref input, length, ImGuiInputTextFlags.None);
            ImGui.PopItemWidth();
            return centeredInput;
        }
     
        public static void EditImage(Plugin plugin, FileDialogManager _fileDialogManager, GalleryLayout layout, bool avatar, bool background, int imageIndex)
        {
            _fileDialogManager.OpenFileDialog("Select Image", "Image{.png,.jpg}", (s, f) =>
            {
                if (!s)
                    return;
                var imagePath = f[0].ToString();
                var image = Path.GetFullPath(imagePath);
                var imageBytes = File.ReadAllBytes(image);
                if (avatar == true)
                {
                    ProfileWindow.CurrentProfile.avatarBytes = imageBytes;
                    ProfileWindow.currentAvatarImg = Plugin.TextureProvider.CreateFromImageAsync(Imaging.ScaleImageBytes( ProfileWindow.CurrentProfile.avatarBytes, 100,100)).Result;
                }
                else if(background == true)
                {
                    ProfileWindow.CurrentProfile.backgroundBytes = imageBytes;
                    ProfileWindow.backgroundImage = Plugin.TextureProvider.CreateFromImageAsync(Imaging.ScaleImageBytes(ProfileWindow.CurrentProfile.backgroundBytes, 1000, 1500)).Result;
                }
                else
                {
                    layout.images[imageIndex].imageBytes = imageBytes;
                    layout.images[imageIndex].thumbnail = Plugin.TextureProvider.CreateFromImageAsync(Imaging.ScaleImageBytes(imageBytes, 250, 250)).Result;
                    layout.images[imageIndex].image = Plugin.TextureProvider.CreateFromImageAsync(Imaging.ScaleImageBytes(imageBytes, 2000, 2000)).Result;
                }
            }, 0, null, plugin.Configuration.AlwaysOpenDefaultImport);

        }
        public static void EditGroupImage(Plugin plugin, FileDialogManager _fileDialogManager, Group group, bool logo, bool background, int imageIndex)
        {
            _fileDialogManager.OpenFileDialog("Select Image", "Image{.png,.jpg}", (s, f) =>
            {
                try
                {
                    if (!s)
                        return;

                    var imagePath = f?.FirstOrDefault()?.ToString();
                    if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                    {
                        Plugin.PluginLog.Debug($"EditGroupImage: invalid path '{imagePath}'");
                        return;
                    }

                    var image = Path.GetFullPath(imagePath);
                    var imageBytes = File.ReadAllBytes(image);

                    if (logo == true)
                    {
                        var scaledLogo = Imaging.ScaleImageBytes(imageBytes, 100, 100);
                        var logoTex = Plugin.TextureProvider.CreateFromImageAsync(scaledLogo).Result;
                        if (group != null)
                        {
                            // Use the selected bytes directly for scaling/texture creation (avoid referencing GroupCreation.group before assigning)
                            group.logoBytes = imageBytes;
                            group.logo = logoTex;
                        }
                        // Only update GroupCreation.group if it exists (some flows don't create it)
                        if (group != null)
                        {
                            group.logoBytes = imageBytes;
                            group.logo = logoTex;
                        }
                    }
                    else if (background == true)
                    {
                        // Create background texture and assign safely
                        var scaledBg = Imaging.ScaleImageBytes(imageBytes, 1000, 1500);
                        var bgTex = Plugin.TextureProvider.CreateFromImageAsync(scaledBg).Result;

                        if (group != null)
                        {
                            group.backgroundBytes = imageBytes;
                            group.background = bgTex;
                        }

                        if (group != null)
                        {
                            group.backgroundBytes = imageBytes;
                            group.background = bgTex;
                        }
                    }
                   
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("EditGroupImage callback error: " + ex);
                }
            }, 0, null, plugin.Configuration.AlwaysOpenDefaultImport);
        }
        public static bool DrawCenteredButton(string label)
        {
            var style = ImGui.GetStyle();

            // Determine available area start X and width (prefer the current content region if constrained)
            float regionAvail = ImGui.GetContentRegionAvail().X;
            float areaStartX = ImGui.GetCursorPosX();

            if (regionAvail <= 0f || regionAvail > ImGui.GetWindowSize().X - style.WindowPadding.X * 2f)
            {
                // Use full content width inside window (respect window padding)
                areaStartX = style.WindowPadding.X;
                regionAvail = Math.Max(0f, ImGui.GetWindowSize().X - style.WindowPadding.X * 2f);
            }

            // Compute "natural" button width from text size + frame padding
            var textSize = ImGui.CalcTextSize(label);
            float buttonWidth = textSize.X + style.FramePadding.X * 2f;
            // Optionally clamp button width so it doesn't exceed available region
            buttonWidth = Math.Min(buttonWidth, regionAvail);

            // Compute centered X and set cursor
            float centeredX = areaStartX + Math.Max(0f, (regionAvail - buttonWidth) / 2f);
            var currentCursorY = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(new System.Numerics.Vector2(centeredX, currentCursorY));

            // Draw button with calculated width (keep default height)
            bool clicked = ImGui.Button(label, new System.Numerics.Vector2(buttonWidth, 0f));
            return clicked;
        }
        public static bool DrawButton(string label)
        {
            using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.3f, 0.3f, 0.3f, 1.0f))) // Light gray on hover
            {
                var button = ImGui.Button(label);
                return button;
            }
        }
        public static void DrawCenteredButtonTable(int rows, List<ProfileTab> profileTabs)
        {
            int columns = profileTabs.Count;
            // Get window size
            var windowSize = ImGui.GetWindowSize();

            // Define button size (width and height)
            var buttonSize = new System.Numerics.Vector2(100, 50);

            // Calculate total width of the button table (button width + padding between buttons)
            float totalTableWidth = (buttonSize.X * columns) + (ImGui.GetStyle().ItemSpacing.X * (columns - 1));

            // Calculate the X position to start drawing the table (centered horizontally)
            float startX = (windowSize.X - totalTableWidth) / 2;

            // Set cursor position to center the table horizontally
            ImGui.SetCursorPosX(startX);

            // Create a table layout for the buttons
            using (var table = ImRaii.Table("ButtonTable", columns))
            {
                if (table)
                {
                    int buttonIndex = 0;
                    for (int row = 0; row < rows; row++)
                    {
                        ImGui.TableNextRow(); // Move to the next row in the table

                        for (int column = 0; column < columns; column++)
                        {
                            ImGui.TableSetColumnIndex(column); // Move to the next column in the table

                            if (buttonIndex < profileTabs.Count)
                            {
                                // Draw a button
                                if (ImGui.Button(profileTabs[buttonIndex].name, buttonSize))
                                {
                                    profileTabs[buttonIndex].action();
                                    profileTabs[buttonIndex].showValue = true;
                                }
                                buttonIndex++;
                            }
                        }
                    }
                }
            }
        }
        public static void DrawCenteredWrappedText(string text, bool useFullWindowWidth = true, bool centerVertically = false, float? leftWrapOffset = null)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var style = ImGui.GetStyle();
            var windowSize = ImGui.GetWindowSize();

            // Full content area (window padding respected)
            float contentStartX = style.WindowPadding.X;
            float contentStartY = style.WindowPadding.Y;
            float contentWidth = Math.Max(0f, windowSize.X - style.WindowPadding.X * 2f);
            float contentHeight = Math.Max(0f, windowSize.Y - style.WindowPadding.Y * 2f);

            // Current content region available (for table columns, groups, etc.)
            float regionStartX = ImGui.GetCursorPosX();
            float regionAvail = ImGui.GetContentRegionAvail().X;

            // Decide which area to use for centering/wrapping.
            // If we're inside a constrained content region (e.g. table column), prefer that width.
            bool insideConstrainedRegion = regionAvail > 0f && regionAvail < contentWidth - 1f;

            float availForCenter;
            float cursorXBase;
            if (insideConstrainedRegion)
            {
                availForCenter = regionAvail;
                cursorXBase = regionStartX;
            }
            else if (useFullWindowWidth)
            {
                availForCenter = contentWidth;
                cursorXBase = contentStartX;
            }
            else
            {
                availForCenter = regionAvail;
                cursorXBase = regionStartX;
            }

            // leftWrapOffset controls how early to wrap (max line width). It reduces wrapWidth but does NOT shift the centered block.
            float wrapWidth = availForCenter;
            if (leftWrapOffset.HasValue && leftWrapOffset.Value > 0f)
                wrapWidth = Math.Min(availForCenter, leftWrapOffset.Value);

            // Get logically wrapped lines using existing helper.
            var wrapped = WrapTextToFit(text, wrapWidth);
            var lines = wrapped.Replace("\r\n", "\n").Split('\n');

            // Measure lines
            var lineWidths = new float[lines.Length];
            var lineHeights = new float[lines.Length];
            float totalHeight = 0f;
            float itemSpacingY = style.ItemSpacing.Y;

            for (int i = 0; i < lines.Length; i++)
            {
                var l = lines[i] ?? string.Empty;
                var sz = ImGui.CalcTextSize(l, true, -1);
                if (string.IsNullOrEmpty(l))
                    sz.Y = ImGui.GetFontSize();
                lineWidths[i] = sz.X;
                lineHeights[i] = sz.Y;
                totalHeight += sz.Y;
            }

            if (lines.Length > 1)
                totalHeight += itemSpacingY * (lines.Length - 1);

            // Vertical start (optional)
            float startY = centerVertically ? (contentStartY + Math.Max(0f, (contentHeight - totalHeight) / 2f)) : ImGui.GetCursorPosY();

            // Render each wrapped line centered inside the chosen centering area (availForCenter).
            float curY = startY;
            for (int i = 0; i < lines.Length; i++)
            {
                float lineW = lineWidths[i];
                float x = cursorXBase + Math.Max(0f, (availForCenter - lineW) / 2f);

                ImGui.SetCursorPos(new System.Numerics.Vector2(x, curY));
                ImGui.TextUnformatted(lines[i] ?? string.Empty);

                curY += lineHeights[i];
                if (i < lines.Length - 1)
                    curY += itemSpacingY;
            }

            // Move cursor after block
            ImGui.SetCursorPosY(curY);
        }
        public static void DrawCenteredMultilineText(string text, bool useFullWindowWidth = true, bool centerVertically = false)
        {
            if (string.IsNullOrEmpty(text))
                return;

            // Normalize line endings and split
            var lines = text.Replace("\r\n", "\n").Split('\n');

            var style = ImGui.GetStyle();
            var windowSize = ImGui.GetWindowSize();

            // Content area (inside window padding)
            var contentStartX = style.WindowPadding.X;
            var contentStartY = style.WindowPadding.Y;
            var contentWidth = Math.Max(0f, windowSize.X - style.WindowPadding.X * 2f);
            var contentHeight = Math.Max(0f, windowSize.Y - style.WindowPadding.Y * 2f);

            // Measure each line and total height
            float maxLineWidth = 0f;
            float totalHeight = 0f;
            float lineSpacing = style.ItemSpacing.Y;
            var lineHeights = new List<float>(lines.Length);

            foreach (var line in lines)
            {
                var sz = ImGui.CalcTextSize(line);
                maxLineWidth = Math.Max(maxLineWidth, sz.X);
                lineHeights.Add(sz.Y);
                totalHeight += sz.Y;
            }

            if (lines.Length > 1)
                totalHeight += lineSpacing * (lines.Length - 1);

            // Compute start positions
            float startX;
            if (useFullWindowWidth)
                startX = contentStartX + Math.Max(0f, (contentWidth - maxLineWidth) / 2f);
            else
                startX = ImGui.GetCursorPosX() + Math.Max(0f, (ImGui.GetContentRegionAvail().X - maxLineWidth) / 2f);

            float startY = centerVertically
                ? contentStartY + Math.Max(0f, (contentHeight - totalHeight) / 2f)
                : ImGui.GetCursorPosY();

            // Ensure cursor Y is at startY and draw each line centered
            ImGui.SetCursorPos(new System.Numerics.Vector2(startX, startY));
            for (int i = 0; i < lines.Length; i++)
            {
                ImGui.SetCursorPosX(startX);
                ImGui.TextUnformatted(lines[i]);
                // advance Y manually if needed (TextUnformatted already advanced it); adjust for explicit spacing
                if (i < lines.Length - 1)
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (lineSpacing - 0.0f));
            }
        }
        //sets a title at the center of the window and resets the font back to default afterwards
        public static void SetTitle(Plugin plugin, bool center, string title, Vector4 borderColor, float offset = 0f)
        {
            Jupiter = Plugin.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Jupiter, 35));



            using var col = ImRaii.PushColor(ImGuiCol.Border, borderColor);
            using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 2 * ImGuiHelpers.GlobalScale);
            using var font = Jupiter.Push();
            if (center == true)
            {
                var size = ImGui.CalcTextSize(title);

                var windowSize = ImGui.GetWindowSize();

                // Set the cursor position to center the button horizontally
                float xPos = (windowSize.X - size.X - 15) / 2 + offset; // Center horizontally
                ImGui.SetCursorPosX(xPos);
            }
            UIHelpers.DrawTextButton(title, Vector2.Zero, 0);

            using var defInfFontDen = ImRaii.DefaultFont();
            using var defCol = ImRaii.DefaultColors();
            using var defStyle = ImRaii.DefaultStyle();
        }



        // Helper method to wrap text to fit within a specified width

        // WrapTextToFit now only returns the wrapped text without modifying the original input
        public static string WrapTextToFit(string inputText, float boxWidth)
        {
            if (inputText == previousInputText && boxWidth == previousBoxWidth)
                return cachedWrappedText;

            previousInputText = inputText;
            previousBoxWidth = boxWidth;

            var lines = inputText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            StringBuilder wrappedText = new StringBuilder();

            for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
            {
                var line = lines[lineIdx];

                // Preserve blank lines
                if (string.IsNullOrEmpty(line))
                {
                    wrappedText.AppendLine();
                    continue;
                }

                StringBuilder lineBuilder = new StringBuilder();
                float lineWidth = 0f;

                var words = line.Split(' ');
                bool isFirstWord = true;
                for (int w = 0; w < words.Length; w++)
                {
                    var word = words[w];
                    string wordWithSpace = isFirstWord ? word : " " + word;
                    float wordSize = ImGui.CalcTextSize(wordWithSpace).X;

                    if (lineWidth + wordSize > boxWidth && lineBuilder.Length > 0)
                    {
                        wrappedText.AppendLine(lineBuilder.ToString());
                        lineBuilder.Clear();
                        lineBuilder.Append(word);
                        lineWidth = ImGui.CalcTextSize(word).X;
                        isFirstWord = false; // After a wrap, next word is not first
                    }
                    else
                    {
                        lineBuilder.Append(wordWithSpace);
                        lineWidth += wordSize;
                        isFirstWord = false;
                    }
                }

                // Append the last part of the line (do not add extra newline if this is the last input line)
                if (lineBuilder.Length > 0)
                {
                    wrappedText.Append(lineBuilder.ToString());
                }

                // Only add a newline if this is not the last input line
                if (lineIdx < lines.Length - 1)
                    wrappedText.AppendLine();
            }

            cachedWrappedText = wrappedText.ToString();
            return cachedWrappedText;
        }
        //loader for ProfileWindow and TargetWindow
        public static void ResetLoaderTween(string key = "default")
        {
            loaderTweens.Remove(key);
        }
        public static void ResetAllData()
        {
            try
            {
                // ... existing code ...

                // Reset loader tweens for target tooltipData loading
                Misc.ResetLoaderTween("tabs");
                Misc.ResetLoaderTween("gallery");
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("TargetProfileWindow ResetAllData Debug: " + ex.Message);
            }
        }
        private static readonly Dictionary<string, LoaderTweenState> loaderTweens = new();
        public static  bool showUrlPopup;


        public static bool IsLoaderTweening(string key = "default")
        {
            if (!loaderTweens.TryGetValue(key, out var tween))
                return false;
            return Math.Abs(tween.TweenedValue - tween.TweenTargetValue) > 0.001f;
        }
        public static void StartLoader(float value, float max, string loading, Vector2 scale, string key = "default")
        {
            value = Math.Max(0f, Math.Min(max, value));
            float now = (float)ImGui.GetTime();

            if (!loaderTweens.TryGetValue(key, out var tween))
            {
                tween = new LoaderTweenState
                {
                    TweenedValue = value,
                    TweenStartValue = value,
                    TweenTargetValue = value,
                    TweenStartTime = now
                };
                loaderTweens[key] = tween;
            }

            // If the target value changed, start a new tween
            if (Math.Abs(value - tween.TweenTargetValue) > 0.001f)
            {
                tween.TweenStartValue = tween.TweenedValue;
                tween.TweenTargetValue = value;
                tween.TweenStartTime = now;
            }

            // Calculate tween progress
            float t = Math.Min(1f, (now - tween.TweenStartTime) / tween.TweenDuration);
            t = t * t * (3f - 2f * t); // smoothstep
            tween.TweenedValue = tween.TweenStartValue + (tween.TweenTargetValue - tween.TweenStartValue) * t;

            ImGui.ProgressBar(tween.TweenedValue / max, new Vector2(scale.X - 20, ImGui.GetIO().FontGlobalScale * 20), "Loading " + loading);
        }
        public static byte[] ImageToByteArray(string imagePath)
        {
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException("Image file not found.", imagePath);
            }

            return File.ReadAllBytes(imagePath);
        }
        public static void AddIncrementBar(ImDrawListPtr drawList, float width, Vector4 color)
        {
            var cursorPos = ImGui.GetCursorScreenPos();
            // Set the rectangle size and color
            float height = 10;
            uint colorVal = ImGui.GetColorU32(color); // Solid blue color (RGBA)

            // Draw the outlined rectangle
            drawList.AddRectFilled(new Vector2(cursorPos.X, cursorPos.Y), new Vector2(cursorPos.X + width, cursorPos.Y + height), colorVal);
        }

        public static void RenderAlignmentToRight(string buttonText)
        {
            float windowWidth = ImGui.GetWindowSize().X;
            float scale = ImGui.GetIO().FontGlobalScale;

            // Calculate button width dynamically based on the label text and UI scale
            float buttonWidth = ImGui.CalcTextSize(buttonText).X + (20f * scale); // Add padding to match button appearance

            // Calculate position for right alignment, keeping it within bounds
            float buttonXPosition = Math.Max(0, windowWidth - buttonWidth);

            // Set cursor to the calculated position
            ImGui.SetCursorPosX(buttonXPosition);

        }
        internal static Vector2 CalculateTooltipScale(string tooltip, float wrapWidth = 400f)
        {
            // Helper to recursively parse and calculate size, now using WrapTextToFit for text segments
            Vector2 Parse(string text, float parentScale)
            {
                // Table support
                var tableRegex = new Regex(@"<table>(.*?)</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                var tableMatch = tableRegex.Match(text);
                if (tableMatch.Success)
                {
                    string tableContent = tableMatch.Groups[1].Value;
                    var columnRegex = new Regex(@"<column>(.*?)</column>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    var columnMatches = columnRegex.Matches(tableContent);

                    float totalWidth = 0f;
                    float maxHeight = 0f;
                    foreach (Match colMatch in columnMatches)
                    {
                        string colContent = colMatch.Groups[1].Value.Trim();
                        Vector2 colSize = Parse(colContent, parentScale);
                        totalWidth += colSize.X;
                        maxHeight = Math.Max(maxHeight, colSize.Y);
                    }
                    return new Vector2(totalWidth, maxHeight);
                }

                // Split by line breaks
                var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                float maxWidth = 0f;
                float totalHeight = 0f;
                foreach (var line in lines)
                {
                    Vector2 lineSize = Vector2.Zero;
                    int idx = 0;
                    while (idx < line.Length)
                    {
                        // Scale block
                        var scaleBlockRegex = new Regex(@"<scale\s*=\s*""([\d\.]+)""\s*>(.*?)</scale>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        var scaleMatch = scaleBlockRegex.Match(line, idx);
                        if (scaleMatch.Success && scaleMatch.Index == idx)
                        {
                            float scale = float.TryParse(scaleMatch.Groups[1].Value, out var s) ? s : 1.0f;
                            string scaleContent = scaleMatch.Groups[2].Value;
                            Vector2 scaledSize = Parse(scaleContent, parentScale * scale);
                            lineSize.X += scaledSize.X;
                            lineSize.Y = Math.Max(lineSize.Y, scaledSize.Y);
                            idx += scaleMatch.Length;
                            continue;
                        }

                        // Image
                        var imgRegex = new Regex(@"<(img|image)>(.*?)</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        var imgMatch = imgRegex.Match(line, idx);
                        if (imgMatch.Success && imgMatch.Index == idx)
                        {
                            string imgUrl = imgMatch.Groups[2].Value.Trim();
                            Vector2 imgSize = new Vector2(100, 100); // Default
                            IDalamudTextureWrap texture = null;
                            lock (_imageCacheLock)
                            {
                                _imageCache.TryGetValue(imgUrl, out texture);
                            }
                            if (texture != null)
                            {
                                try
                                {
                                    imgSize = new Vector2(texture.Width, texture.Height);
                                }
                                catch (ObjectDisposedException)
                                {
                                    // Texture was disposed, use default size
                                }
                            }
                            imgSize *= parentScale;
                            lineSize.X += imgSize.X;
                            lineSize.Y = Math.Max(lineSize.Y, imgSize.Y);
                            idx += imgMatch.Length;
                            continue;
                        }

                        // Color
                        var colorRegex = new Regex(@"<color\s+hex\s*=\s*([A-Fa-f0-9]{6})\s*>(.*?)</color>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        var colorMatch = colorRegex.Match(line, idx);
                        if (colorMatch.Success && colorMatch.Index == idx)
                        {
                            string colorContent = colorMatch.Groups[2].Value;
                            string wrapped = WrapTextToFit(colorContent, wrapWidth);
                            foreach (var wrappedLine in wrapped.Split('\n'))
                            {
                                Vector2 colorTextSize = ImGui.CalcTextSize(wrappedLine) * parentScale;
                                lineSize.X = Math.Max(lineSize.X, colorTextSize.X);
                                lineSize.Y += colorTextSize.Y;
                            }
                            idx += colorMatch.Length;
                            continue;
                        }

                        // URL
                        var urlRegex = new Regex(@"<url>(.*?)</url>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        var urlMatch = urlRegex.Match(line, idx);
                        if (urlMatch.Success && urlMatch.Index == idx)
                        {
                            string urlContent = urlMatch.Groups[1].Value;
                            string wrapped = WrapTextToFit(urlContent, wrapWidth);
                            foreach (var wrappedLine in wrapped.Split('\n'))
                            {
                                Vector2 urlTextSize = ImGui.CalcTextSize(wrappedLine) * parentScale;
                                lineSize.X = Math.Max(lineSize.X, urlTextSize.X);
                                lineSize.Y += urlTextSize.Y;
                            }
                            idx += urlMatch.Length;
                            continue;
                        }

                        // Plain text
                        int nextTagIdx = line.IndexOf('<', idx);
                        if (nextTagIdx == -1)
                            nextTagIdx = line.Length;
                        string plainText = line.Substring(idx, nextTagIdx - idx);
                        if (!string.IsNullOrWhiteSpace(plainText))
                        {
                            string wrapped = WrapTextToFit(plainText, wrapWidth);
                            foreach (var wrappedLine in wrapped.Split('\n'))
                            {
                                Vector2 textSize = ImGui.CalcTextSize(wrappedLine) * parentScale;
                                lineSize.X = Math.Max(lineSize.X, textSize.X);
                                lineSize.Y += textSize.Y;
                            }
                        }
                        idx = nextTagIdx;
                    }
                    maxWidth = Math.Max(maxWidth, lineSize.X);
                    totalHeight += lineSize.Y;
                }

                return new Vector2(maxWidth, totalHeight);
            }

            // Add some padding
            Vector2 size = Parse(tooltip, 1.0f);
            size.X += 16;
            size.Y += 16;
            return size;
        }
    }
}
