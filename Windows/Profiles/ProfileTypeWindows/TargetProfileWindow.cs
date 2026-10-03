using AbsoluteRP.Caching;
using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI;
using AbsoluteRP.Windows.Inventory;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs;
using Networking;
using System.Linq;
using System.Numerics;
using AbsoluteRP.Network;

namespace AbsoluteRP.Windows.Profiles.ProfileTypeWindows
{
    // Read-only view of another player's profile - opened when clicking "View Profile" on a target. Renders the same tab layouts as ProfileWindow but in view-only mode with no editing.
    public class TargetProfileWindow : Window, IDisposable
    {
        public static string loading;
        public static float currentInd, max;
        public static bool addNotes, loadPreview = false;
        public static ProfileData profileData = new ProfileData();
        internal static string characterName;
        internal static string characterWorld;
        public static bool firstDraw = true;
        internal static bool warning;
        // The last target fetch, so agreeing to a content warning can repeat it.
        internal static (int index, string name, string world, int id) LastFetch;
        internal static string warningMessage;
        public static List<CustomLayout> profileLayouts = new List<CustomLayout>();
        public static CustomLayout currentLayout;
        public static bool RequestingProfile = false;
        public static bool ExistingProfile = false;
        // Set only when the server refused the profile (private, or none). The access panel shows for this, never merely because data hasn't arrived yet.
        public static bool AccessDenied = false;
        // True from a target fetch until the server answers or the viewer cancels.
        public static bool PendingFetch = false;
        private static long _fetchStartedTicks;
        // A fetch with no reply at all after this long falls back to the access panel.
        private const double FetchTimeoutSeconds = 15.0;
        public static bool showUrlPopup;
        private static bool allow;
        public static string playername;
        public static string playerworld;
        public static bool LoadUrl { get; set; }
        public static string UrlToLoad { get; set; }
        private static bool fetchedLikes = false;
        private static bool showLikeCommentDialog = false;
        private static string likeCommentBuffer = string.Empty;
        private static int likeCountInput = 1;
        private static bool showEquipmentInspect = false;
        private static int selectedTab = 0;

        // Video-background support for target profiles. Same shape as ProfilesPage's - content-hashed so a re-fetch of the same profile doesn't tear the LibVLC session down and back up.
        private static AbsoluteRP.Video.VideoPlayerSession? _bgVideo;
        private static ulong _bgVideoBytesHash;
        private static string? _bgVideoTempPath;

        // Retire-then-drain queue for the same reason ProfilesPage has one: disposing the LibVLC session (and its shared D3D11 SRV) during the same frame the backdrop drew with it races the driver into an access violation on its own worker thread. See ProfilesPage for the canonical version of this pattern.
        private readonly struct RetiredBgVideo
        {
            public readonly AbsoluteRP.Video.VideoPlayerSession Session;
            public readonly string? TempPath;
            public RetiredBgVideo(AbsoluteRP.Video.VideoPlayerSession s, string? p) { Session = s; TempPath = p; }
        }
        private static readonly List<RetiredBgVideo> _retiredBgVideos = new();

        // A crashed renderer is restarted with a back-off, never every frame: re-spawning the process (and its D3D textures) each frame is what took the graphics driver down.
        private static DateTime _bgRestartAt = DateTime.MinValue;
        private static int _bgRestarts;
        private static ulong _bgRestartHash;
        private static bool MayRestartBgVideo(ulong hash)
        {
            if (hash != _bgRestartHash) { _bgRestartHash = hash; _bgRestarts = 0; _bgRestartAt = DateTime.MinValue; }
            if (_bgRestarts >= 3) return false;
            if (DateTime.UtcNow < _bgRestartAt) return false;
            _bgRestarts++;
            _bgRestartAt = DateTime.UtcNow.AddSeconds(15);
            return true;
        }

        public static void DisposeBackgroundVideo()
        {
            if (_bgVideo != null)
            {
                // Snapshot the temp-file path - by the time drain runs next frame, _bgVideoTempPath may have been reassigned to a fresh session.
                _retiredBgVideos.Add(new RetiredBgVideo(_bgVideo, _bgVideoTempPath));
                _bgVideo = null;
                _bgVideoTempPath = null;
            }
            _bgVideoBytesHash = 0;
        }

        private static void DrainRetiredBgVideos()
        {
            if (_retiredBgVideos.Count == 0) return;
            for (int i = 0; i < _retiredBgVideos.Count; i++)
            {
                var r = _retiredBgVideos[i];
                try { r.Session.Dispose(); }
                catch (Exception ex) { Plugin.PluginLog?.Debug("Target retired bg-video dispose: " + ex.Message); }
                var p = r.TempPath;
                if (!string.IsNullOrEmpty(p))
                {
                    try { if (System.IO.File.Exists(p)) System.IO.File.Delete(p); } catch { }
                }
            }
            _retiredBgVideos.Clear();
        }

        // Spins up (or reuses) a LibVLC session for the target profile's background bytes if they are a video container. Called each frame from Draw so profile switches / fresh fetches take effect.
        private static void SyncBackgroundVideo()
        {
            var bytes = profileData?.backgroundBytes;
            var isVideo = bytes != null && bytes.Length > 12
                          && AbsoluteRP.RsUI.Pages.ProfilesPage.LooksLikeVideoBytes(bytes);
            if (!isVideo)
            {
                DisposeBackgroundVideo();
                return;
            }
            var hash = HashBytesTarget(bytes!);
            if (_bgVideo != null && hash == _bgVideoBytesHash && (!_bgVideo.RendererCrashed || !MayRestartBgVideo((ulong)hash))) return;
            DisposeBackgroundVideo();
            try
            {
                // Prefer streaming from the URL the server sent (RSUR marker).
                if (AbsoluteRP.RsUI.Pages.ProfilesPage.TryExtractBackgroundUrl(bytes!, out var streamUrl))
                {
                    _bgVideoBytesHash = hash;
                    _bgVideo = new AbsoluteRP.Video.VideoPlayerSession(
                        Plugin.PluginInterface, Plugin.PluginLog, streamUrl, initialVolume: 0);
                    return;
                }

                var dir = Plugin.PluginInterface?.ConfigDirectory?.FullName;
                if (string.IsNullOrEmpty(dir)) return;
                System.IO.Directory.CreateDirectory(dir);
                var path = System.IO.Path.Combine(dir, "target_bg_" + hash.ToString("x16") + ".bin");
                if (!System.IO.File.Exists(path))
                    System.IO.File.WriteAllBytes(path, bytes!);
                _bgVideoTempPath = path;
                _bgVideoBytesHash = hash;
                var url = new System.Uri(path).AbsoluteUri;
                _bgVideo = new AbsoluteRP.Video.VideoPlayerSession(
                    Plugin.PluginInterface, Plugin.PluginLog, url, initialVolume: 0);
            }
            catch (System.Exception ex)
            {
                Plugin.PluginLog.Debug("Target video-bg session start failed: " + ex.Message);
                _bgVideo = null;
            }
        }

        private static ulong HashBytesTarget(byte[] bytes)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime  = 1099511628211UL;
            var h = offset;
            for (int i = 0; i < bytes.Length; i++) { h ^= bytes[i]; h *= prime; }
            h ^= (ulong)bytes.Length;
            return h;
        }

        public TargetProfileWindow() : base("TARGET")
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(300, 300),
                MaximumSize = new Vector2(600, 950)
            };
        }

        private static void EnsureProfileData(ProfileData data = null)
        {
            if (data != null)
            {
                profileData = data;
            }
            if (profileData == null)
                profileData = new ProfileData();
            if (profileData.customTabs == null)
                profileData.customTabs = new List<CustomTab>();
        }


        public override void OnOpen()
        {
            EnsureData();
        }

        // Immersive mode: this Dalamud window becomes an invisible, input-less host. Its only job is to carry IsOpen and to give ImmersiveHud a place in the draw order - the HUD draws its own set of floating panels from Draw(). Non-immersive mode gets the standard chrome back.
        public override void PreDraw()
        {
            base.PreDraw();
            if (AbsoluteRP.Immersive.ImmersiveMode.IsActive)
            {
                Flags = ImGuiWindowFlags.NoTitleBar
                      | ImGuiWindowFlags.NoResize
                      | ImGuiWindowFlags.NoScrollbar
                      | ImGuiWindowFlags.NoMove
                      | ImGuiWindowFlags.NoBackground
                      | ImGuiWindowFlags.NoInputs
                      | ImGuiWindowFlags.NoNav
                      | ImGuiWindowFlags.NoFocusOnAppearing
                      | ImGuiWindowFlags.NoBringToFrontOnFocus
                      | ImGuiWindowFlags.NoSavedSettings;
                var vp = ImGui.GetMainViewport();
                ImGui.SetNextWindowPos(vp.WorkPos, ImGuiCond.Always);
                ImGui.SetNextWindowSize(new Vector2(300f, 300f), ImGuiCond.Always);
                ImGui.SetNextWindowBgAlpha(0f);
            }
            else
            {
                Flags = ImGuiWindowFlags.None;
            }
        }


        public static void EnsureData(ProfileData data = null)
        {
            try
            {
                EnsureProfileData(data);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("TargetProfileWindow OnOpen Debug: " + ex.Message);
            }
        }

        // Marks a target fetch as in flight; stale state from the last view is dropped.
        internal static void BeginFetch()
        {
            PendingFetch = true;
            RequestingProfile = true;
            ExistingProfile = false;
            AccessDenied = false;
            _fetchStartedTicks = System.Diagnostics.Stopwatch.GetTimestamp();
        }

        // Drops a pending fetch so a late reply can neither reopen nor repaint the window.
        internal static void CancelFetch()
        {
            if (PendingFetch) _cancelledTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            PendingFetch = false;
            RequestingProfile = false;
            warning = false;
            warningMessage = string.Empty;
        }

        private static long _cancelledTicks;

        // True shortly after a cancel, so a late reply to it is swallowed instead of misrouted.
        internal static bool RecentlyCancelled
            => _cancelledTicks != 0
            && (System.Diagnostics.Stopwatch.GetTimestamp() - _cancelledTicks) / (double)System.Diagnostics.Stopwatch.Frequency < 60.0;

        // Ends the pending state once the server answered; shows the access panel if it never did.
        internal static void TickFetch()
        {
            if (!PendingFetch) return;
            if (ExistingProfile || AccessDenied) { PendingFetch = false; return; }
            // The viewer is deciding on a content warning; that is an answer, not a stall.
            if (warning) { _fetchStartedTicks = System.Diagnostics.Stopwatch.GetTimestamp(); return; }
            var elapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - _fetchStartedTicks) / (double)System.Diagnostics.Stopwatch.Frequency;
            if (elapsed < FetchTimeoutSeconds) return;
            PendingFetch = false;
            RequestingProfile = false;
            AccessDenied = true;
        }

        public override void Draw()
        {
            TickFetch();
            if (AbsoluteRP.Immersive.ImmersiveMode.IsActive)
            {
                // The content warning is owned by this host window so it is opened before any HUD panel exists and stays on top of everything the HUD draws.
                if (ExistingProfile) DrawWarningPopup();
                try { AbsoluteRP.Immersive.ImmersiveHud.Draw(); }
                catch (Exception ex) { Plugin.PluginLog.Debug("ImmersiveHud: " + ex); }
                return;
            }
            DrawProfile();
        }

        // shared pieces (used by both the classic window and the HUD)

        internal static bool IsOwnProfile
            => profileData == null
            || Plugin.plugin?.Configuration?.account == null
            || profileData.accountID == Plugin.plugin.Configuration.account.userID;

        internal static int SelectedTab
        {
            get => selectedTab;
            set => selectedTab = value;
        }

        internal static bool ShowEquipmentInspect
        {
            get => showEquipmentInspect;
            set => showEquipmentInspect = value;
        }

        internal static void OpenNotes() => addNotes = true;

        internal static void OpenReport()
        {
            ReportWindow.reportCharacterName = characterName;
            ReportWindow.reportCharacterWorld = characterWorld;
            Plugin.plugin.OpenReportWindow();
        }

        internal static void ToggleEquipmentInspect()
        {
            showEquipmentInspect = !showEquipmentInspect;
            if (showEquipmentInspect && Plugin.character != null)
            {
                EquipmentPage.ClearTargetEquipment();
                Equipment_DS.SendFetchTargetEquipment(Plugin.character, characterName, characterWorld);
            }
        }

        internal static void RequestLikeDialog()
        {
            if (!fetchedLikes && Plugin.character != null)
            {
                ProfileLikes_DS.FetchLikesRemaining(Plugin.character);
                fetchedLikes = true;
            }
            showLikeCommentDialog = true;
        }

        internal static void EnsureLikesFetched()
        {
            if (!fetchedLikes && Plugin.character != null)
            {
                ProfileLikes_DS.FetchLikesRemaining(Plugin.character);
                fetchedLikes = true;
            }
        }

        internal static List<CustomTab> GetVisibleTabs()
        {
            EnsureProfileData();
            return profileData.customTabs
                .Where(t => t != null && !string.IsNullOrEmpty(t.Name) && t.Layout != null)
                .ToList();
        }

        // Mirrors the classic window's gating: while a request is in flight we wait for every tab and gallery image to arrive.
        internal static bool IsLoading(out bool tabsLoading, out bool galleryLoading)
        {
            tabsLoading = Profiles_DR.tabsTargetCount > 0
                && Profiles_DR.loadedTargetTabsCount < Profiles_DR.tabsTargetCount;
            galleryLoading = Profiles_DR.TargetGalleryImagesToLoad > 0
                && Profiles_DR.loadedTargetGalleryImages < Profiles_DR.TargetGalleryImagesToLoad;
            if (RequestingProfile)
            {
                bool allTabs = Profiles_DR.tabsTargetCount > 0
                    && Profiles_DR.loadedTargetTabsCount >= Profiles_DR.tabsTargetCount;
                // A profile with no tabs at all is done as soon as its count arrives.
                if (Profiles_DR.targetTabCountReceived && Profiles_DR.tabsTargetCount <= 0) allTabs = true;
                if (allTabs && !galleryLoading) RequestingProfile = false;
            }
            return RequestingProfile || tabsLoading || galleryLoading;
        }

        internal static void EnsureDefaultTextures()
        {
            if (profileData.background == null || profileData.background.Handle == IntPtr.Zero)
                profileData.background = UI.UICommonImage(UI.CommonImageTypes.backgroundHolder);
            if (profileData.avatar == null || profileData.avatar.Handle == IntPtr.Zero)
                profileData.avatar = UI.UICommonImage(UI.CommonImageTypes.avatarHolder);
        }

        // Keeps the video-background session in sync and returns the texture to paint behind the profile - the live video frame when one exists, otherwise the still image. Handle is default when nothing is available.
        internal static ImTextureID GetBackdropTexture(out int texW, out int texH)
        {
            texW = 0; texH = 0;
            // The theme editor's preview swaps in its own sample profile every frame; it must never create or tear down the video session (that thrashed a renderer process per frame). It shows the live session's frame when the bytes match, otherwise the still image.
            var editing = AbsoluteRP.Immersive.ImmersiveHud.EditMode;
            if (!editing)
            {
                DrainRetiredBgVideos();
                SyncBackgroundVideo();
            }
            var vidMatches = !editing || (profileData?.backgroundBytes != null && profileData.backgroundBytes.Length > 12
                && HashBytesTarget(profileData.backgroundBytes) == _bgVideoBytesHash);
            if (_bgVideo != null && !_bgVideo.RendererCrashed && vidMatches)
            {
                var vt = _bgVideo.CurrentTextureId;
                if (vt != IntPtr.Zero)
                {
                    texW = _bgVideo.Width;
                    texH = _bgVideo.Height;
                    return new ImTextureID(vt);
                }
            }
            if (profileData?.background != null && profileData.background.Handle != IntPtr.Zero)
            {
                texW = profileData.background.Width;
                texH = profileData.background.Height;
                return profileData.background.Handle;
            }
            return default;
        }

        // UV rect for aspect-filling a texture of texWxtexH into a pageWxpageH area.
        internal static void AspectFillUv(int texW, int texH, float pageW, float pageH, out Vector2 uMin, out Vector2 uMax)
        {
            var scale = MathF.Max(pageW / texW, pageH / texH);
            var drawW = texW * scale;
            var drawH = texH * scale;
            var offX = (pageW - drawW) * 0.5f;
            var offY = (pageH - drawH) * 0.5f;
            uMin = new Vector2(-offX / drawW, -offY / drawH);
            uMax = new Vector2((pageW - offX) / drawW, (pageH - offY) / drawH);
        }

        private static int _warningFrame = -1;

        internal static void DrawWarningPopup()
        {
            if (!warning) return;
            // Never from the theme editor's preview render - a second modal opened from an input-less preview window would sit on top of the real one and swallow every click.
            if (AbsoluteRP.Immersive.ImmersiveHud.EditMode) return;
            // One modal per frame, whoever asks first (the immersive host window asks before any HUD panel) - never two stacked copies.
            var frame = ImGui.GetFrameCount();
            if (_warningFrame == frame) return;
            _warningFrame = frame;
            // Escape always dismisses (declines) the warning.
            if (ImGui.IsKeyPressed(ImGuiKey.Escape))
            {
                warning = false;
                Plugin.plugin.CloseTargetWindow();
                return;
            }
            ImGui.OpenPopup("WARNING");
            // Centre it on the screen so it can never hide off-screen or under a panel; modals render on the popup layer, above all regular windows.
            var vp = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(vp.WorkPos + vp.WorkSize * 0.5f, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
            // ImGui puts newly created windows at the display front, so HUD panels created after the modal would cover it while it still captures input. Re-focusing it every frame keeps it in front.
            ImGui.SetNextWindowFocus();
            bool popupOpen = false;
            try
            {
                popupOpen = ImGui.BeginPopupModal("WARNING", ref warning, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings);
                // The title-bar X declines like "Go back" instead of leaving an endless loader behind.
                if (!warning && !popupOpen)
                {
                    Plugin.plugin.CloseTargetWindow();
                    return;
                }
                if (popupOpen)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
                    ImGui.TextUnformatted(warningMessage ?? "Warning");
                    ImGui.PopStyleColor();
                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentDanger);
                    ImGui.TextUnformatted("Do you agree to view the profile anyway?");
                    ImGui.PopStyleColor();
                    ImGui.Spacing();
                    if (RsElements.Button("Agree", RsElements.ButtonVariant.Primary))
                    {
                        warning = false;
                        ImGui.CloseCurrentPopup();
                        // The server sent only the warning; now ask for the profile itself.
                        if (!ExistingProfile || profileData == null || profileData.customTabs == null || profileData.customTabs.Count == 0)
                        {
                            RequestingProfile = true;
                            Profiles_DS.FetchProfile(Plugin.character, false, LastFetch.index, LastFetch.name, LastFetch.world, LastFetch.id, acknowledgedWarning: true);
                        }
                    }
                    ImGui.SameLine();
                    if (RsElements.Button("Go back", RsElements.ButtonVariant.Ghost))
                    {
                        warning = false;
                        Plugin.plugin.CloseTargetWindow();
                        ImGui.CloseCurrentPopup();
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"[TargetProfileWindow] Warning popup Debug: {ex.Message}");
            }
            finally
            {
                if (popupOpen) ImGui.EndPopup();
            }
        }

        internal static void DrawLikeDialog()
        {
            if (!showLikeCommentDialog) return;
            ImGui.SetNextWindowSize(new Vector2(400, 320), ImGuiCond.FirstUseEver);
            // The HUD makes window backgrounds transparent; this dialog stays solid.
            ImGui.PushStyleColor(ImGuiCol.WindowBg, RsTheme.BgSecondary);
            ImGui.PushStyleColor(ImGuiCol.ChildBg, RsTheme.BgSecondary);
            var dialogOpen = ImGui.Begin("Like Profile##likeDialog", ref showLikeCommentDialog, ImGuiWindowFlags.NoCollapse);
            ImGui.PopStyleColor(2);
            try
            {
                if (dialogOpen)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentWarning);
                    ImGui.TextUnformatted($"Liking: {profileData.title}");
                    ImGui.PopStyleColor();
                    ImGui.Separator();
                    ImGui.Spacing();

                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
                    ImGui.TextUnformatted("Number of likes to send:");
                    ImGui.PopStyleColor();
                    ImGui.SetNextItemWidth(RsTheme.S(200f));
                    if (ImGui.InputInt("##likeCount", ref likeCountInput))
                    {
                        if (likeCountInput < 1) likeCountInput = 1;
                        if (likeCountInput > ProfileLikes_DR.likesRemaining) likeCountInput = ProfileLikes_DR.likesRemaining;
                    }
                    ImGui.SameLine();
                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                    ImGui.TextUnformatted($"(Max: {ProfileLikes_DR.likesRemaining})");
                    ImGui.PopStyleColor();

                    ImGui.Spacing();

                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
                    ImGui.TextUnformatted("Leave an optional comment:");
                    ImGui.PopStyleColor();
                    RsElements.InputTextArea("likeComment", ref likeCommentBuffer, 500,
                                             placeholder: "Optional comment",
                                             size: new Vector2(RsTheme.S(380f), RsTheme.S(100f)));

                    ImGui.Spacing();

                    if (RsElements.Button($"Send {likeCountInput} Like(s)", RsElements.ButtonVariant.Primary,
                                          new Vector2(RsTheme.S(150f), 0f)))
                    {
                        if (Plugin.character != null && profileData != null && likeCountInput > 0)
                        {
                            ProfileLikes_DS.LikeProfile(Plugin.character, profileData.id, likeCommentBuffer, likeCountInput);
                            likeCommentBuffer = string.Empty;
                            likeCountInput = 1;
                            showLikeCommentDialog = false;
                        }
                    }

                    ImGui.SameLine();

                    if (RsElements.Button("Cancel", RsElements.ButtonVariant.Ghost,
                                          new Vector2(RsTheme.S(120f), 0f)))
                    {
                        likeCommentBuffer = string.Empty;
                        likeCountInput = 1;
                        showLikeCommentDialog = false;
                    }
                }
            }
            finally
            {
                ImGui.End();
            }
        }

        // Renders the body of one tab. Caller is responsible for the surrounding panel / scroll region.
        internal static void RenderTabBody(CustomTab tab)
        {
            if (tab?.Layout == null) return;
            try
            {
                switch (tab.Layout)
                {
                    case BioLayout bioLayout:
                        try { Bio.RenderBioPreview(bioLayout, tab.Name, profileData.titleColor); } catch (Exception ex) { Plugin.PluginLog.Debug($"[TargetProfileWindow] Bio.RenderBioPreview failed. {ex}"); }
                        break;
                    case DetailsLayout detailsLayout:
                        try { Details.RenderDetailPreview(detailsLayout, profileData.titleColor); } catch (Exception ex) { Plugin.PluginLog.Debug($"[TargetProfileWindow] Details.RenderDetailPreview Debug: {ex.Message}"); }
                        break;
                    case GalleryLayout galleryLayout:
                        try { Gallery.RenderGalleryPreview(galleryLayout, profileData.titleColor); } catch (Exception ex) { Plugin.PluginLog.Debug($"[TargetProfileWindow] Gallery.RenderGalleryPreview Debug: {ex.Message}"); }
                        break;
                    case InfoLayout infoLayout:
                        try
                        {
                            Misc.SetTitle(Plugin.plugin, true, tab.Name, profileData.titleColor);
                            Misc.RenderHtmlElements(infoLayout.text, true, true, true, false);
                        }
                        catch (Exception ex)
                        {
                            Plugin.PluginLog.Debug($"[TargetProfileWindow] InfoLayout render Debug: {ex.Message}");
                        }
                        break;
                    case StoryLayout storyLayout:
                        try { Story.RenderStoryPreview(storyLayout, profileData.titleColor); } catch (Exception ex) { Plugin.PluginLog.Debug($"[TargetProfileWindow] Story.RenderStoryPreview Debug: {ex.Message}"); }
                        break;
                    case TreeLayout treeLayout:
                        string uniqueId = $"{tab.Name}##{treeLayout.tabIndex}";
                        try { Tree.RenderTreeLayout(treeLayout.tabIndex, false, uniqueId, treeLayout, tab.Name, profileData.titleColor); } catch (Exception ex) { Plugin.PluginLog.Debug($"[TargetProfileWindow] Tree.RenderTreeLayout Debug: {ex.Message}"); }
                        break;
                    default:
                        Plugin.PluginLog.Debug($"[TargetProfileWindow] Unknown tab layout type: {tab.Layout.GetType().Name}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"[TargetProfileWindow] Tab Render Debug: {ex.Message}");
            }
        }

        // Opens the notes / image-preview windows that a tab body asked for during this frame.
        internal static void ProcessDeferredOpens()
        {
            if (addNotes)
            {
                try { Plugin.plugin.OpenProfileNotes(); }
                catch (Exception ex) { Plugin.PluginLog.Debug($"[TargetProfileWindow] OpenProfileNotes Debug: {ex.Message}"); }
                addNotes = false;
            }
            if (loadPreview)
            {
                try { Plugin.plugin.OpenImagePreview(); }
                catch (Exception ex) { Plugin.PluginLog.Debug($"[TargetProfileWindow] OpenImagePreview Debug: {ex.Message}"); }
                loadPreview = false;
            }
        }


        public static void DrawProfile(ProfileData data = null)
        {

            try
            {
                if (data != null)
                {
                    profileData = data;
                }
                // Refused, missing, or timed out: the request panel. A fetch still in flight shows the loader below.
                if (AccessDenied || (!ExistingProfile && !RequestingProfile))
                {
                    RequestingProfile = false;
                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextSecondary);
                    ImGui.TextWrapped("This player either does not have an active profile or has not granted you permission to view it.");
                    ImGui.PopStyleColor();
                    ImGui.Spacing();
                    if (RsElements.Button("Request Access", RsElements.ButtonVariant.Primary))
                    {
                        Profiles_DS.SendProfileAccessUpdate(Plugin.character, Plugin.plugin.username, Plugin.plugin.playername, Plugin.plugin.playerworld, characterName, characterWorld, (int)UI.ConnectionStatus.pending);
                    }
                    ImGui.SameLine();
                    if (RsElements.Button("Close##targetAccessClose", RsElements.ButtonVariant.Ghost))
                        Plugin.plugin.CloseTargetWindow();
                }
                else
                {

                    if (RequestingProfile)
                    {
                        // Check if all target tabs AND gallery images loaded
                        bool allTargetTabsLoaded = Profiles_DR.tabsTargetCount > 0
                            && Profiles_DR.loadedTargetTabsCount >= Profiles_DR.tabsTargetCount;
                        // A profile with no tabs at all is done as soon as its count arrives.
                        if (ExistingProfile && Profiles_DR.targetTabCountReceived && Profiles_DR.tabsTargetCount <= 0) allTargetTabsLoaded = true;
                        bool galleryDone = Profiles_DR.TargetGalleryImagesToLoad == 0
                            || Profiles_DR.loadedTargetGalleryImages >= Profiles_DR.TargetGalleryImagesToLoad;

                        if (allTargetTabsLoaded && galleryDone)
                        {
                            RequestingProfile = false;
                        }
                        else
                        {
                            var windowPos = ImGui.GetWindowPos();
                            var windowSize = ImGui.GetWindowSize();
                            var dl = ImGui.GetWindowDrawList();
                            var scrim = new Vector4(RsTheme.BgPrimary.X, RsTheme.BgPrimary.Y, RsTheme.BgPrimary.Z, 0.85f);
                            dl.AddRectFilled(windowPos, windowPos + windowSize, ImGui.ColorConvertFloat4ToU32(scrim));

                            bool isTabsLoading = Profiles_DR.tabsTargetCount > 0
                                && Profiles_DR.loadedTargetTabsCount < Profiles_DR.tabsTargetCount;
                            bool isGalleryLoading = Profiles_DR.TargetGalleryImagesToLoad > 0
                                && Profiles_DR.loadedTargetGalleryImages < Profiles_DR.TargetGalleryImagesToLoad;

                            float cardWidth = MathF.Min(windowSize.X - RsTheme.S(40f), RsTheme.S(340f));
                            var lineH = RsTheme.S(24f);
                            int lineCount = 1;
                            if (isTabsLoading) lineCount += 2;
                            if (isGalleryLoading) lineCount += 2;
                            if (!isTabsLoading && !isGalleryLoading) lineCount += 1;
                            float cardHeight = RsTheme.S(40f) + lineCount * lineH;

                            float cardX = windowPos.X + (windowSize.X - cardWidth) * 0.5f;
                            float cardY = windowPos.Y + (windowSize.Y - cardHeight) * 0.5f;
                            var cardMin = new Vector2(cardX, cardY);
                            var cardMax = new Vector2(cardX + cardWidth, cardY + cardHeight);
                            dl.AddRectFilled(cardMin, cardMax, RsTheme.U.BgSecondary, RsTheme.CornerRadius);
                            dl.AddRect(cardMin, cardMax, RsTheme.U.Border, RsTheme.CornerRadius, ImDrawFlags.None, RsTheme.BorderThickness);

                            var pad = RsTheme.S(16f);
                            float yPos = cardY + RsTheme.S(14f);

                            ImGui.SetCursorScreenPos(new Vector2(cardX + pad, yPos));
                            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentPrimary);
                            try { ImGui.TextUnformatted("Loading Profile..."); }
                            finally { ImGui.PopStyleColor(); }
                            yPos += lineH;

                            if (isTabsLoading)
                            {
                                DrawLoadingMutedLine(cardX + pad, yPos,
                                    $"Loading tabs ({Profiles_DR.loadedTargetTabsCount}/{Profiles_DR.tabsTargetCount})...");
                                yPos += lineH;
                                float tabProgress = (float)Profiles_DR.loadedTargetTabsCount / Profiles_DR.tabsTargetCount;
                                DrawLoadingProgress(dl, cardX + pad, yPos, cardWidth - pad * 2f, tabProgress);
                                yPos += lineH;
                            }

                            if (isGalleryLoading)
                            {
                                DrawLoadingMutedLine(cardX + pad, yPos,
                                    $"Loading images ({Profiles_DR.loadedTargetGalleryImages}/{Profiles_DR.TargetGalleryImagesToLoad})...");
                                yPos += lineH;
                                float imgProgress = (float)Profiles_DR.loadedTargetGalleryImages / Profiles_DR.TargetGalleryImagesToLoad;
                                DrawLoadingProgress(dl, cardX + pad, yPos, cardWidth - pad * 2f, imgProgress);
                                yPos += lineH;
                            }

                            if (!isTabsLoading && !isGalleryLoading)
                            {
                                DrawLoadingMutedLine(cardX + pad, yPos, "Requesting profile data...");
                            }

                            // Themed X in the card corner so a pending request can be cancelled.
                            var closeSize = RsTheme.S(26f);
                            ImGui.SetCursorScreenPos(new Vector2(cardMax.X - closeSize - RsTheme.S(6f), cardY + RsTheme.S(6f)));
                            if (RsElements.IconButton(FontAwesomeIcon.Times, "targetLoadingClose", RsElements.ButtonVariant.Ghost, 26f))
                                Plugin.plugin.CloseTargetWindow();
                            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Cancel");
                            return;
                        }
                    }
                    EnsureDefaultTextures();
                    EnsureProfileData();

                    // Loader: Wait for all tabs and gallery images to load before drawing tooltipData

                    bool tabsLoading = Profiles_DR.loadedTargetTabsCount < Profiles_DR.tabsTargetCount;
                    bool galleryLoading = Profiles_DR.loadedTargetGalleryImages < Profiles_DR.TargetGalleryImagesToLoad;

                    if (tabsLoading || galleryLoading)
                    {
                        if (tabsLoading)
                        {
                            Misc.StartLoader(Profiles_DR.loadedTargetTabsCount, Profiles_DR.tabsTargetCount, $"Loading Profile Tabs {Profiles_DR.loadedTargetTabsCount + 1}", ImGui.GetWindowSize(), "tabs");
                        }

                        if (galleryLoading)
                        {
                            Misc.StartLoader(Profiles_DR.loadedTargetGalleryImages, Profiles_DR.TargetGalleryImagesToLoad, $"Loading Gallery Images {Profiles_DR.loadedTargetGalleryImages + 1}", ImGui.GetWindowSize(), "gallery");
                        }
                        return;
                    }
                    // Block further UI until all tweens are finished
                    if ((tabsLoading && Misc.IsLoaderTweening("tabs")) ||
                        (galleryLoading && Misc.IsLoaderTweening("gallery")))
                    {
                        return;
                    }
                    // Warning popup
                    DrawWarningPopup();


                    // First draw setup
                    if (firstDraw)
                    {
                        addNotes = false;
                        firstDraw = false;
                    }

                    // Backdrop - video takes priority, still image is the fallback. Anchored to the window's screen rect so scrolling the content leaves it pinned.
                    try
                    {
                        var backdrop = ImGui.GetWindowDrawList();
                        Vector2 pageMin = ImGui.GetWindowPos();
                        Vector2 pageMax = pageMin + ImGui.GetWindowSize();
                        var avail = pageMax - pageMin;
                        var texHandle = GetBackdropTexture(out var texW, out var texH);
                        if (texW > 0 && texH > 0 && avail.X > 0 && avail.Y > 0)
                        {
                            AspectFillUv(texW, texH, avail.X, avail.Y, out var uMin, out var uMax);
                            uint tint = ImGui.ColorConvertFloat4ToU32(new Vector4(0.65f, 0.65f, 0.65f, 1f));
                            backdrop.AddImage(texHandle, pageMin, pageMax, uMin, uMax, tint);
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.PluginLog.Debug($"[TargetProfileWindow] Failed to draw backdrop: {ex.Message}");
                    }
                    // Draw Like Controls
                    if (ExistingProfile && !IsOwnProfile)
                    {
                        EnsureLikesFetched();
                        if (RsElements.Button($"♥ ({ProfileLikes_DR.likesRemaining})##likeBtn", RsElements.ButtonVariant.Danger))
                            showLikeCommentDialog = true;
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Like this profile");
                    }

                    // Comment dialog
                    DrawLikeDialog();


                    string animKey = $"{characterName}@{characterWorld}/{profileData?.title}";
                    Helpers.Anim.ResetKey("target", animKey);
                    string anim = "target/" + animKey;

                    
                    if (profileData.avatar != null && profileData.avatar.Handle != IntPtr.Zero)
                    {
                        try
                        {
                            Vector2 avatarSize = profileData.avatar.Size * ImGui.GetIO().FontGlobalScale;
                            Helpers.Anim.DrawScaledCenteredCircleAvatar(
                                anim + ".avatar",
                                profileData.avatar.Handle,
                                avatarSize,
                                borderColor: profileData.titleColor,
                                duration: 0.70f,
                                delay: 0.08f,
                                ease: Helpers.Anim.Ease.OutBack,
                                fromScale: 0.30f,
                                borderThickness: 3f);
                        }
                        catch (Exception ex)
                        {
                            Plugin.PluginLog.Debug($"[TargetProfileWindow] Failed to draw avatar image: {ex.Message}");
                        }
                    }
                    else
                    {
                        Plugin.PluginLog.Debug("[TargetProfileWindow] Avatar image is null or handle is zero.");
                    }

                    // Draw title if valid
                    if (!string.IsNullOrEmpty(profileData.title))
                    {
                        try
                        {
                            Misc.SetTitle(Plugin.plugin, true, profileData.title, profileData.titleColor);
                        }
                        catch (Exception ex)
                        {
                            Plugin.PluginLog.Debug($"[TargetProfileWindow] Failed to set title: {ex.Message}");
                        }
                    }

                    Helpers.Anim.PushAlpha(anim + ".controls", 0.45f, 0.55f);
                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                    ImGui.TextUnformatted("Controls");
                    ImGui.PopStyleColor();
                    if (RsElements.Button("Notes", RsElements.ButtonVariant.Ghost)) { addNotes = true; }
                    if (ImGui.IsItemHovered()) { ImGui.SetTooltip("Add personal notes about this Profile."); }

                    if (profileData.equipmentPublic)
                    {
                        ImGui.SameLine();
                        if (RsElements.Button("Inspect Equipment", RsElements.ButtonVariant.Ghost))
                            ToggleEquipmentInspect();
                        if (ImGui.IsItemHovered()) { ImGui.SetTooltip("View this player's equipped RP items."); }
                    }

                    ImGui.SameLine();
                    // Right-align "Report" inside the current content region (panel or window). Misc.RenderAlignmentToRight used GetWindowSize + a fixed 20*FontGlobalScale padding, which doesn't match RsElements.Button's Sv(18, 9) inner pad or the panel's own padding, so the button hung off the right edge. Compute width from actual text + RsElements padding, and use the remaining content-region X on this row.
                    {
                        var reportW = ImGui.CalcTextSize("Report").X + RsTheme.S(36f);
                        var avail = ImGui.GetContentRegionAvail().X;
                        var offset = avail - reportW;
                        if (offset > 0f) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
                    }

                    if (RsElements.Button("Report", RsElements.ButtonVariant.Danger))
                        OpenReport();
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Report this tooltipData for inappropriate use.\n(Repeat false reports may result in your account being banned.)");
                    }
                    Helpers.Anim.PopAlpha();
                    if (!string.IsNullOrEmpty(ProfileLikes_DR.likeResultMessage))
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text,
                            ProfileLikes_DR.likeResultSuccess ? RsTheme.AccentSuccess : RsTheme.AccentDanger);
                        ImGui.TextUnformatted(ProfileLikes_DR.likeResultMessage);
                        ImGui.PopStyleColor();

                        if (ImGui.GetFrameCount() % 300 == 0)
                        {
                            ProfileLikes_DR.likeResultMessage = string.Empty;
                        }
                    }
                    
                    ImGui.Spacing();
                    if (showEquipmentInspect && profileData.equipmentPublic)
                    {
                        ImGui.Spacing();
                        if (RsElements.Button("Back to Profile", RsElements.ButtonVariant.Ghost))
                        {
                            showEquipmentInspect = false;
                        }
                        ImGui.Spacing();
                        if (RsElements.BeginPanel("target_equipment", "Equipment", default, false, true))
                        {
                            try { EquipmentPage.RenderEquipmentPreview(Plugin.plugin); }
                            finally { RsElements.EndPanel(); }
                        }
                        else
                        {
                            RsElements.EndPanel();
                        }
                        ImGui.Spacing();
                    }
                    else
                    if (profileData.customTabs != null && profileData.customTabs.Count > 0)
                    {
                        ImGui.Spacing();
                        Helpers.Anim.DrawGrowingDivider(anim + ".divider", duration: 0.65f, delay: 0.85f, height: 2f);
                        Helpers.Anim.PushAlpha(anim + ".tabs", 0.45f, 1.10f);

                        var visibleTabs = GetVisibleTabs();

                        if (visibleTabs.Count > 0)
                        {
                            if (selectedTab < 0 || selectedTab >= visibleTabs.Count) selectedTab = 0;
                            var navItems = visibleTabs
                                .Select(t => new RsElements.NavItem(IconForLayout(t.type), t.Name))
                                .ToList();
                            RsElements.NavigationMenu("target_tabs", ref selectedTab, navItems);

                            var tab = visibleTabs[selectedTab];
                            currentLayout = tab.Layout as CustomLayout;
                            Helpers.Anim.ResetKey("targetTab", $"{characterName}@{characterWorld}/{tab.Name}");
                            string tabAnim = $"targetTab/{characterName}@{characterWorld}/{tab.Name}";

                            Helpers.Anim.DrawGrowingDivider(tabAnim + ".accent", duration: 0.45f, delay: 0.0f, height: 2f);
                            Helpers.Anim.PushAlpha(tabAnim + ".content", 0.40f, 0.08f);
                            float slide = Helpers.Anim.SlideOffsetY(tabAnim + ".slide", 0.55f, distance: 22f, delay: 0.08f, ease: Helpers.Anim.Ease.OutQuint);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + slide);

                            if (RsElements.BeginPanel("target_tab_body_" + selectedTab, tab.Name, default, false, true))
                            {
                                RenderTabBody(tab);
                                RsElements.EndPanel();
                            }
                            else
                            {
                                RsElements.EndPanel();
                            }

                            Helpers.Anim.PopAlpha();
                        }
                        Helpers.Anim.PopAlpha();
                    }
                    else
                    {
                        Plugin.PluginLog.Debug("[TargetProfileWindow] No custom tabs to render.");
                        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentDanger);
                        ImGui.TextUnformatted("No data available for this profile.");
                        ImGui.PopStyleColor();
                    }

                    // Layout selection warning
                    if (currentLayout == null)
                    {
                        Plugin.PluginLog.Debug("[TargetProfileWindow] currentLayout is null, returning.");
                        return;
                    }

                    // Profile child region
                    using var profileTable = ImRaii.Child("PROFILE");
                    if (!profileTable)
                    {
                        return;
                    }

                    // Notes and preview
                    ProcessDeferredOpens();

                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("TargetWindow Draw Debug: " + ex.Message);
                loading = "An Debug occurred while loading the tooltipData data.";
                currentInd = 0;
                max = 1;
            }

            Misc.RenderUrlModalPopup();
        }

        private static void DrawLoadingMutedLine(float x, float y, string text)
        {
            ImGui.SetCursorScreenPos(new Vector2(x, y));
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            try { ImGui.TextUnformatted(text ?? string.Empty); }
            finally { ImGui.PopStyleColor(); }
        }

        private static void DrawLoadingProgress(ImDrawListPtr dl, float x, float y, float width, float frac01)
        {
            frac01 = Math.Clamp(frac01, 0f, 1f);
            var h = RsTheme.S(8f);
            var radius = h * 0.5f;
            var min = new Vector2(x, y);
            var max = new Vector2(x + width, y + h);
            dl.AddRectFilled(min, max, RsTheme.U.BgTertiary, radius);
            if (frac01 > 0f)
            {
                var fillMax = new Vector2(x + width * frac01, y + h);
                var col = frac01 >= 1f ? RsTheme.U.AccentSuccess : RsTheme.U.AccentPrimary;
                dl.AddRectFilled(min, fillMax, col, radius);
            }
            dl.AddRect(min, max, RsTheme.U.Border, radius, ImDrawFlags.None, RsTheme.BorderThickness);
        }

        private static FontAwesomeIcon IconForLayout(int layoutType)
        {
            return (LayoutTypes)layoutType switch
            {
                LayoutTypes.Bio          => FontAwesomeIcon.Book,
                LayoutTypes.Story        => FontAwesomeIcon.Scroll,
                LayoutTypes.Gallery      => FontAwesomeIcon.Image,
                LayoutTypes.Info         => FontAwesomeIcon.InfoCircle,
                LayoutTypes.Details      => FontAwesomeIcon.ListUl,
                LayoutTypes.Inventory    => FontAwesomeIcon.BoxOpen,
                LayoutTypes.Relationship => FontAwesomeIcon.ProjectDiagram,
                LayoutTypes.Roster       => FontAwesomeIcon.Users,
                LayoutTypes.VenueInfo    => FontAwesomeIcon.MapMarkerAlt,
                _                        => FontAwesomeIcon.Question,
            };
        }

        public static void ResetAllData()
        {
            try
            {
                // Dispose of any textures to avoid memory leaks
                if (profileData != null)
                {
                    profileData.background = null;
                    profileData.avatar = null;

                    if (profileData.customTabs != null)
                    {
                        foreach (var tab in profileData.customTabs)
                        {
                            switch (tab.Layout)
                            {
                                case GalleryLayout gallery:
                                    if (gallery.images != null)
                                    {
                                        foreach (var img in gallery.images)
                                        {
                                            img.image = null; // Thumbnail is disposed in the loop
                                            img.thumbnail = null; // Clear reference to avoid dangling pointers
                                        }
                                        gallery.images.Clear();
                                    }
                                    break;
                                case InventoryLayout inventory:
                                    if (inventory.inventorySlotContents != null)
                                    {
                                        foreach (var item in inventory.inventorySlotContents.Values)
                                        {
                                            item.iconTexture = null; // Clear reference to avoid dangling pointers
                                        }
                                        inventory.inventorySlotContents.Clear();
                                    }
                                    break;
                                case BioLayout bio:
                                    if (bio.traits != null)
                                    {
                                        foreach (var trait in bio.traits)
                                        {
                                            if (trait.icon != null && trait.icon.icon != null)
                                            {
                                                trait.icon.icon = null; // Clear reference to avoid dangling pointers
                                            }
                                        }
                                        bio.traits.Clear();
                                    }
                                    bio.fields?.Clear();
                                    bio.descriptors?.Clear();
                                    break;
                                case DetailsLayout details:
                                    details.details?.Clear();
                                    break;
                                case StoryLayout story:
                                    story.chapters?.Clear();
                                    break;
                                case TreeLayout tree:
                                    tree.relationships?.Clear();
                                    tree.Paths?.Clear();
                                    tree.PathConnections?.Clear();
                                    break;
                                case InfoLayout info:
                                    info.text = string.Empty;
                                    break;
                            }
                        }
                        profileData.customTabs.Clear();
                    }
                }

                // Reset all static and instance fields to their initial state
                profileData = new ProfileData
                {
                    avatar = null,
                    background = null,
                    title = string.Empty,
                    titleColor = new Vector4(1, 1, 1, 1),
                    isPrivate = false,
                    isActive = false,
                    customTabs = new List<CustomTab>()
                };

                profileLayouts?.Clear();
                profileLayouts = new List<CustomLayout>();
                currentLayout = new CustomLayout();

                loading = string.Empty;
                currentInd = 0;
                max = 0;
                addNotes = false;
                loadPreview = false;
                firstDraw = true;
                warning = false;
                warningMessage = string.Empty;

                // Reset NSFW spoiler states when loading a new profile
                Misc.ResetNsfwRevealStates();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("TargetProfileWindow ResetAllData Debug: " + ex.Message);
            }
        }

        public static bool IsDefault()
        {
            var pd = profileData;
            bool profileDataDefault =
                pd != null &&
                pd.avatar == null &&
                pd.background == null &&
                pd.title == string.Empty &&
                pd.titleColor == new Vector4(1, 1, 1, 1) &&
                pd.isPrivate == false &&
                pd.isActive == false &&
                pd.customTabs != null && pd.customTabs.Count == 0;

            bool otherDefaults =
                string.IsNullOrEmpty(loading) &&
                currentInd == 0 &&
                max == 0 &&
                addNotes == false &&
                loadPreview == false &&
                firstDraw == true &&
                warning == false &&
                string.IsNullOrEmpty(warningMessage) &&
                (profileLayouts == null || profileLayouts.Count == 0) &&
                currentLayout != null;  // currentLayout is set to new CustomLayout();

            return profileDataDefault && otherDefaults;
        }

        public override void OnClose()
        {
            // Stop and dispose all audio players when closing the window
            Misc.CleanupAudioPlayers();
            // Closing mid-request cancels it, so the reply cannot pop the window back up.
            CancelFetch();
            base.OnClose();
        }

        public void Dispose()
        {
            // Stop and dispose all audio players
            Misc.CleanupAudioPlayers();

            if (profileData != null)
            {
                WindowOperations.SafeDispose(profileData.background);
                profileData.background = null;
                WindowOperations.SafeDispose(profileData.avatar);
                profileData.avatar = null;

                if (profileData.customTabs != null)
                {
                    foreach (var tab in profileData.customTabs)
                    {
                        switch (tab.Layout)
                        {
                            case GalleryLayout gallery:
                                if (gallery.images != null)
                                {
                                    foreach (var img in gallery.images)
                                    {
                                        WindowOperations.SafeDispose(img.image);
                                        img.image = null; // Thumbnail is disposed in the loop
                                        WindowOperations.SafeDispose(img.thumbnail);
                                        img.thumbnail = null; // Clear reference to avoid dangling pointers
                                    }
                                    gallery.images.Clear();
                                }
                                break;

                            case InventoryLayout inventory:
                                if (inventory.inventorySlotContents != null)
                                {
                                    foreach (var item in inventory.inventorySlotContents.Values)
                                    {
                                        WindowOperations.SafeDispose(item.iconTexture);
                                        item.iconTexture = null; // Clear reference to avoid dangling pointers
                                    }
                                    inventory.inventorySlotContents.Clear();
                                }
                                break;
                            case BioLayout bio:
                                if (bio.traits != null)
                                {
                                    foreach (var trait in bio.traits)
                                    {
                                        if (trait.icon != null && trait.icon.icon != null)
                                        {
                                            WindowOperations.SafeDispose(trait.icon.icon);
                                            trait.icon.icon = null; // Clear reference to avoid dangling pointers
                                            trait.icon = null; // Clear reference to avoid dangling pointers
                                        }
                                    }
                                    bio.traits.Clear();
                                }
                                bio.fields?.Clear();
                                bio.descriptors?.Clear();
                                break;

                            case DetailsLayout details:
                                details.details?.Clear();
                                break;

                            case StoryLayout story:
                                story.chapters?.Clear();
                                break;

                            case TreeLayout tree:
                                tree.relationships?.Clear();
                                tree.Paths?.Clear();
                                tree.PathConnections?.Clear();
                                break;

                            case InfoLayout info:
                                info.text = string.Empty;
                                break;
                        }
                    }
                    profileData.customTabs.Clear();
                }

                // Dispose and clear static layouts
                profileLayouts?.Clear();
                currentLayout = null;

                // Reset tooltipData data
                profileData = new ProfileData();
                if (profileData.customTabs == null)
                    profileData.customTabs = new List<CustomTab>();
            }

        }

        private static int GetTabIndex(CustomTab tab)
        {
            if (tab?.Layout == null) return int.MaxValue;
            return tab.Layout switch
            {
                BioLayout b => b.tabIndex,
                DetailsLayout d => d.tabIndex,
                DynamicLayout dyn => dyn.tabIndex,
                GalleryLayout g => g.tabIndex,
                InfoLayout inf => inf.tabIndex,
                StoryLayout s => s.tabIndex,
                InventoryLayout inv => inv.tabIndex,
                TreeLayout tr => tr.tabIndex,
                _ => int.MaxValue,
            };
        }

        public static void AddTabSorted(CustomTab tab)
        {
            profileData.customTabs.Add(tab);
            profileData.customTabs.Sort((a, b) => GetTabIndex(a).CompareTo(GetTabIndex(b)));
        }
    }
}
