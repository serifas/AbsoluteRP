using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AbsoluteRP.Defines;
using AbsoluteRP.RsUI;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Networking;
using AbsoluteRP.Network;

namespace AbsoluteRP.RsUI.Pages;

// The RsUI-native Profiles page. Owns the entire profile-editing surface: character picker with Lodestone verification, profile picker with create/delete, per-profile visibility toggles, avatar + title, save/delete/backup, and the tab strip that dispatches into per-layout renderers. Reads/writes the same static state (ProfileWindow.profiles / CurrentProfile / profileIndex) as the legacy window, but never calls ProfileWindow.DrawContent - every pixel is rendered here or in ProfilesPageEditor.
public sealed class ProfilesPage : IPage
{
    public string Id => "profiles";
    public string Title => "Profiles";
    public FontAwesomeIcon Icon => FontAwesomeIcon.IdCard;

    // Dropdown positions for the "New tab" modal. Mapped to LayoutTypes ints via NewTabLayoutValues so we can present a shorter list than the raw enum without breaking the wire protocol.
    private static readonly string[] NewTabLayoutNames =
    {
        "Bio", "Details", "Story", "Info", "Gallery", "Inventory", "Relationship",
    };
    private static readonly int[] NewTabLayoutValues =
    {
        (int)LayoutTypes.Bio,
        (int)LayoutTypes.Details,
        (int)LayoutTypes.Story,
        (int)LayoutTypes.Info,
        (int)LayoutTypes.Gallery,
        (int)LayoutTypes.Inventory,
        (int)LayoutTypes.Relationship,
    };

    private const int MaxTabs = 10;

    private int _selectedCharacter;
    private int _selectedProfile;
    private int _selectedTab;

    private bool _newProfileOpen;
    private string _newProfileTitle = "";

    private bool _newTabOpen;
    private string _newTabName = "";
    private int _newTabLayoutIdx;

    private int _pendingRemoveTab = -1;
    private bool _removeTabConfirmOpen;

    private bool _removeProfileConfirmOpen;

    // (character@world#profileID) we last asked the server for. Guards FetchProfile from firing every frame while the profile is selected.
    private string _lastFetchKey = "";

    // True once we've asked the server for this character's profile list.
    private bool _profilesRequested;
    private double _profilesRequestTime;

    private string _lodestoneUrl = "";
    // Two-stage verification UI: the panel starts as a single "Verify Character" call-to-action; clicking it reveals the URL form. Kept per-page-instance so switching character in the dropdown collapses back to the button.
    private bool _showVerifyForm = false;

    // After Create Profile fires, we want to auto-select the freshly-added profile once the server response repopulates the list. We can't know the new profile's index up front (server assigns it), so we remember the title we just asked for and pick it up on the next redraw.
    private string? _pendingSelectByTitle;

    // shared state (moved off the old ProfileWindow) These live here now - DataReceiver writes them and every other consumer (InventoryWindow, Groups, ViewLikes, IPC, etc.) reads them. Kept as public static so cross-file references stay one-liners.
    public static List<AbsoluteRP.ProfileData> profiles = new();
    public static AbsoluteRP.ProfileData CurrentProfile = new();
    public static int profileIndex = 0;
    public static bool ExistingProfile = false;
    public static bool Fetching = false;
    public static bool Sending = false;
    public static long fetchStartedTicks = 0;

    // Lodestone verification state
    public static string lodeStoneKey = string.Empty;
    public static bool lodeStoneKeyVerified;
    public static bool VerificationSucceeded { get; set; } = false;
    public static bool VerificationFailed { get; internal set; } = false;
    public static string LodeSUrl = string.Empty;
    public static bool checking;

    // Textures + image state
    public static Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap pictureTab;
    public static Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap avatarHolder;
    public static Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap currentAvatarImg;
    public static Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap backgroundImage;

    // Editor flags (cross-window)
    public static bool editAvatar = false;
    public static bool editBackground = false;
    public static bool showOnCompass;
    public static bool addProfile;
    public static bool editProfile;
    public static bool showTypeCreation;

    // Misc profile-scoped state used elsewhere
    public static string oocInfo = string.Empty;
    public static string loading;
    public static float loaderInd = -1;
    public static float inputWidth = 500;
    public static SortedList<int, bool> CustomTabOpen = new();
    public static bool hasDrawException = false;
    public static int currentLayoutType = 0;
    public static int currentElementID = 0;
    public static bool customTabSelected = false;
    public static bool Locked = false;
    public static CustomLayout currentLayout;
    public static List<CustomLayout> customLayouts = new();
    public static InventoryLayout currentInventory;
    public static string NewProfileTitle = string.Empty;

    // File dialog manager used by the hero panel and backup dialogs
    public static AbsoluteRP.RsUI.RsFileDialogManager _fileDialogManager = new();

    // Warning banner (used by DataReceiver in a couple places)
    public static bool warning = false;
    public static string warningMessage = string.Empty;

    // Video-background support. When the picked background is an .mp4 / .webm / .mov (or the incoming bytes sniff as a video container), we spin up a VideoPlayerSession that decodes to a shared texture instead of using the static `backgroundImage`. Keyed by CONTENT hash - not the array reference - so that a Save round-trip (which produces a new byte[] with identical content) doesn't tear the LibVLC session down and back up, which was crashing the NVIDIA D3D driver mid-flight.
    private static AbsoluteRP.Video.VideoPlayerSession? _bgVideo;
    private static ulong _bgVideoBytesHash;
    private static string? _bgVideoTempPath;

    // Deferred-dispose queue for retired video sessions. The active frame's ImGui draw commands may still hold the SRV pointer from the last CurrentTextureId read, and the NVIDIA driver consumes those commands on its own worker thread AFTER our Draw returns. Freeing the SRV in the same frame that queued a draw against it is a use-after-free at the driver level (NVENCODEAPI_Thunk access-violation crash). We stash retired sessions here and let DrainRetiredVideos() release them at the top of the NEXT frame, guaranteeing at least one full present cycle in between.
    private readonly struct RetiredVideo
    {
        public readonly AbsoluteRP.Video.VideoPlayerSession Session;
        public readonly string? TempPath;
        public RetiredVideo(AbsoluteRP.Video.VideoPlayerSession s, string? p) { Session = s; TempPath = p; }
    }
    private static readonly System.Collections.Generic.List<RetiredVideo> _retiredVideos = new();

    // Snapshot the temp-file path at retirement time - by the time drain runs next frame, _bgVideoTempPath has been reassigned to the NEW session's file and would otherwise be wrongly deleted. A crashed renderer is restarted with a back-off, never every frame: re-spawning the process (and its D3D textures) each frame is what took the graphics driver down.
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

    private static void RetireBackgroundVideo(AbsoluteRP.Video.VideoPlayerSession session)
    {
        if (session == null) return;
        var oldPath = _bgVideoTempPath;
        _bgVideoTempPath = null;
        _retiredVideos.Add(new RetiredVideo(session, oldPath));
    }

    // Called at the very top of DrawContent, before the backdrop reads _bgVideo or any texture pointer.
    private static void DrainRetiredVideos()
    {
        if (_retiredVideos.Count == 0) return;
        for (int i = 0; i < _retiredVideos.Count; i++)
        {
            var r = _retiredVideos[i];
            try { r.Session.Dispose(); }
            catch (System.Exception ex) { Plugin.PluginLog.Debug("Retired bg-video dispose: " + ex.Message); }
            var p = r.TempPath;
            if (!string.IsNullOrEmpty(p))
            {
                try { if (System.IO.File.Exists(p)) System.IO.File.Delete(p); }
                catch { }
            }
        }
        _retiredVideos.Clear();
    }

    // Fast, non-crypto content hash (FNV-1a 64) - enough to tell "same bytes as last frame" from "genuinely new upload".
    private static ulong HashBytes(byte[] bytes)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime  = 1099511628211UL;
        var h = offset;
        for (int i = 0; i < bytes.Length; i++)
        {
            h ^= bytes[i];
            h *= prime;
        }
        // Fold length in too so different bytes of same prefix don't collide.
        h ^= (ulong)bytes.Length;
        return h;
    }

    // Sniff common video container magic numbers so we can transparently route uploaded video bytes to the video pipeline even without an explicit flag on ProfileData. Also handles the RSUR URL marker, where the server sends a streaming URL instead of the full mp4.
    public static bool LooksLikeVideoBytes(byte[]? bytes)
    {
        if (bytes == null || bytes.Length < 5) return false;
        // Server-sent URL marker: RSUR + UTF-8 URL. Handled by extracting the URL - the LibVLC session streams it directly.
        if (TryExtractBackgroundUrl(bytes, out _)) return true;
        if (bytes.Length < 12) return false;
        // ISO BMFF (mp4 / mov / m4v): bytes 4..7 == "ftyp"
        if (bytes[4] == (byte)'f' && bytes[5] == (byte)'t' &&
            bytes[6] == (byte)'y' && bytes[7] == (byte)'p') return true;
        // Matroska/WebM: 1A 45 DF A3 EBML header
        if (bytes[0] == 0x1A && bytes[1] == 0x45 &&
            bytes[2] == 0xDF && bytes[3] == 0xA3) return true;
        // AVI: "RIFF" .. "AVI "
        if (bytes[0] == (byte)'R' && bytes[1] == (byte)'I' &&
            bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
            bytes[8] == (byte)'A' && bytes[9] == (byte)'V' &&
            bytes[10] == (byte)'I' && bytes[11] == (byte)' ') return true;
        return false;
    }

    // If the server-provided bytes start with the RSUR marker, extracts the UTF-8 URL after it. Otherwise returns false.
    public static bool TryExtractBackgroundUrl(byte[]? bytes, out string url)
    {
        url = string.Empty;
        if (bytes == null || bytes.Length < 5) return false;
        if (bytes[0] != (byte)'R' || bytes[1] != (byte)'S'
            || bytes[2] != (byte)'U' || bytes[3] != (byte)'R') return false;
        try
        {
            url = System.Text.Encoding.UTF8.GetString(bytes, 4, bytes.Length - 4);
            // Basic sanity - URL should start with http/https.
            if (url.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase)) return true;
            url = string.Empty;
            return false;
        }
        catch { url = string.Empty; return false; }
    }

    public void Draw()
    {
        // (RsFileDialog.Draw is pumped by MainWindow.DrawBody, so we don't need to call it here.)

        // Tutorial: start a fresh anchor frame + kick off the walkthrough once per session for users who haven't seen it.
        AbsoluteRP.Helpers.TutorialManager.BeginFrame();
        MaybeAutoStartTutorial();

        // Free any video sessions retired last frame - must happen before the backdrop reads _bgVideo, and only after the driver has had a full present cycle to consume last frame's queued draws against the retired SRV.
        DrainRetiredVideos();

        var windowMin = ImGui.GetCursorScreenPos();

        // Backdrop first - every widget on the page draws on top. Anchored in screen coords adjusted so scroll doesn't drag the media with the content: adding GetScroll{X,Y} back to the cursor's screen position undoes the scroll shift, pinning the top-left to where the page's content would start at scroll=0. The bottom-right is the window frame's bottom, which never scrolls - so the backdrop stays parked in the visible viewport while widgets slide past on top of it.
        var scrollX = ImGui.GetScrollX();
        var scrollY = ImGui.GetScrollY();
        var winPos  = ImGui.GetWindowPos();
        var winSize = ImGui.GetWindowSize();
        Vector2 backdropMin = new Vector2(windowMin.X + scrollX, windowMin.Y + scrollY);
        Vector2 backdropMax = new Vector2(winPos.X + winSize.X, winPos.Y + winSize.Y);
        DrawProfileBackdrop(backdropMin, backdropMax);

        DrawTutorialControlsRow();
        ImGui.Spacing();

        DrawCharacterRow();
        ImGui.Spacing();

        // If the currently logged-in in-game character isn't in the linked list, force the verification card for THAT character before letting the user do anything else. Editing other verified characters can wait until the current one is claimed - otherwise a fresh alt could quietly ride on some other character's session without ever being registered.
        var inGame = BuildPlaceholderCharacter();
        if (!string.IsNullOrEmpty(inGame.characterName) && !string.IsNullOrEmpty(inGame.characterWorld))
        {
            var cfgChars = Plugin.plugin?.Configuration?.characters;
            bool inGameLinked = cfgChars != null && cfgChars.Any(c =>
                string.Equals(c.characterName,  inGame.characterName,  StringComparison.OrdinalIgnoreCase)
             && string.Equals(c.characterWorld, inGame.characterWorld, StringComparison.OrdinalIgnoreCase)
             && !string.IsNullOrEmpty(c.characterKey));
            if (!inGameLinked)
            {
                DrawVerificationCard(inGame);
                AbsoluteRP.Helpers.TutorialManager.AnchorRect(
                    AbsoluteRP.Helpers.ProfileTutorial.Anchor_Window,
                    windowMin, ImGui.GetCursorScreenPos() + ImGui.GetContentRegionAvail());
                AbsoluteRP.Helpers.TutorialManager.Draw();
                return;
            }
        }

        var character = CurrentCharacter();
        if (character == null)
        {
            // No linked characters yet - show the verification prompt so the user can start the Lodestone flow directly. Synthesize a placeholder Character sourced from the currently logged-in in-game character when we have one, so the card shows a real name instead of a generic label.
            DrawVerificationCard(BuildPlaceholderCharacter());
            AbsoluteRP.Helpers.TutorialManager.AnchorRect(
                AbsoluteRP.Helpers.ProfileTutorial.Anchor_Window,
                windowMin, ImGui.GetCursorScreenPos() + ImGui.GetContentRegionAvail());
            AbsoluteRP.Helpers.TutorialManager.Draw();
            return;
        }

        // Show the verification prompt whenever this character isn't provably-owned on the CURRENT server session - either the key is missing locally, or the server hasn't confirmed our key set this session (fresh account, mid-reconnect, etc.). Otherwise a stale characterKey cached under a previous account would pass the outer gate and pretend the character is authorized.
        if (string.IsNullOrEmpty(character.characterKey) || !Plugin.characterKeysVerified)
        {
            DrawVerificationCard(character);
            return;
        }

        // Ask for the profile list once per character.
        if (!_profilesRequested)
        {
            _profilesRequested = true;
            _profilesRequestTime = ImGui.GetTime();
            Profiles_DS.FetchProfiles(character);
        }

        // Mirror the old ProfileWindow flag so tutorial AutoAdvanceWhen predicates (which read ExistingProfile) can detect that a profile was successfully created / that any exist.
        ExistingProfile = ProfileWindow.profiles.Count > 0;

        // Save-in-progress takes priority over the loading spinner so the user sees exactly why the editor is unresponsive.
        if (ProfileSaveTracker.IsSaving)
        {
            ProfilesPageEditor.DrawSaveProgressOverlay();
            return;
        }

        var galleryLoading = Profiles_DR.GalleryImagesToLoad > 0
                             && Profiles_DR.loadedGalleryImages < Profiles_DR.GalleryImagesToLoad;
        if (ProfileWindow.Fetching || ProfileWindow.Sending || galleryLoading)
        {
            // The old ProfileWindow.DrawContent poll used to flip Fetching off once all tabs + gallery arrived. Since nothing else runs that loop now, do it here - otherwise Fetching stays true forever and the overlay never lifts.
            var allTabsLoaded = Profiles_DR.tabCountReceived
                                && Profiles_DR.loadedTabsCount >= Profiles_DR.tabsCount;
            var galleryDone = Profiles_DR.GalleryImagesToLoad == 0
                              || Profiles_DR.loadedGalleryImages >= Profiles_DR.GalleryImagesToLoad;

            double elapsedMs = ProfileWindow.fetchStartedTicks > 0
                ? (double)(System.Diagnostics.Stopwatch.GetTimestamp() - ProfileWindow.fetchStartedTicks)
                  / System.Diagnostics.Stopwatch.Frequency * 1000.0
                : 999999;
            var minTimeElapsed = elapsedMs >= 500;
            var timedOut       = elapsedMs >= 15000;

            if ((allTabsLoaded && galleryDone && !ProfileWindow.Sending && minTimeElapsed) || timedOut)
            {
                ProfileWindow.Fetching = false;
                ProfileWindow.fetchStartedTicks = 0;

                // Sort tabs by their stored tabIndex so reordering shows in the right positions.
                var cp = ProfileWindow.CurrentProfile;
                if (cp?.customTabs != null && cp.customTabs.Count > 1)
                {
                    cp.customTabs.Sort((a, b) =>
                    {
                        int ia = GetLayoutTabIndex(a.Layout, 0);
                        int ib = GetLayoutTabIndex(b.Layout, 0);
                        return ia.CompareTo(ib);
                    });
                }
                // Snapshot the server-authoritative tab_index onto each CustomTab. This is the value the DB actually stores and is what DeleteTab must target. Local reorder mutates layout.tabIndex (for on-screen ordering) but leaves CustomTab.ID alone, so a delete after an un-persisted reorder still hits the tab the user pointed at.
                if (cp?.customTabs != null)
                {
                    for (int i = 0; i < cp.customTabs.Count; i++)
                    {
                        var t = cp.customTabs[i];
                        if (t != null) t.ID = GetLayoutTabIndex(t.Layout, i);
                    }
                }
            }
            else
            {
                ProfilesPageEditor.DrawLoadingOverlay();
                return;
            }
        }

        // If we just created a profile, jump to it the moment the server's fresh profile list arrives. Also clamp the selected index if the list shrunk (e.g. after a delete) so we don't sit past the end.
        if (_pendingSelectByTitle != null && ProfileWindow.profiles.Count > 0)
        {
            var idx = ProfileWindow.profiles.FindIndex(p =>
                string.Equals(p?.title?.Trim(), _pendingSelectByTitle, System.StringComparison.Ordinal));
            if (idx >= 0)
            {
                _selectedProfile = idx;
                _selectedTab = 0;
                _lastFetchKey = "";
                _pendingSelectByTitle = null;
            }
        }
        if (ProfileWindow.profiles.Count > 0 && _selectedProfile >= ProfileWindow.profiles.Count)
        {
            _selectedProfile = ProfileWindow.profiles.Count - 1;
            _lastFetchKey = "";
        }

        DrawProfileRow(character);
        ImGui.Spacing();

        // Ask the server for this profile's tabs the first time we settle on it.
        var picked = SelectedProfile();
        // Keep video-background state in sync with the current selection.
        SyncBackgroundVideo(picked);
        if (picked != null)
        {
            // Keep the legacy statics in sync so any code that still reads ProfileWindow.CurrentProfile / profileIndex (layout renderers, save flow, etc.) sees the same selection we're rendering.
            ProfileWindow.CurrentProfile = picked;
            ProfileWindow.profileIndex   = picked.index;

            var key = character.characterName + "@" + character.characterWorld + "#" + picked.index;
            if (key != _lastFetchKey)
            {
                _lastFetchKey = key;
                // profileID = -1 matches the legacy self-fetch path - server looks up by (character, profileIndex), not by row id.
                Profiles_DS.FetchProfile(character, true, picked.index,
                                        character.characterName, character.characterWorld, -1);
            }
        }

        if (ProfileWindow.profiles.Count == 0)
        {
            // Give the fetch a moment before declaring the list empty.
            var waiting = ImGui.GetTime() - _profilesRequestTime < 3.0;
            RenderCallout(waiting
                ? "Loading profiles..."
                : "No profiles on this character yet. Create one.");
        }
        else if (picked != null)
        {
            ProfilesPageEditor.ActionsProfileIndex = _selectedProfile;
            ProfilesPageEditor.DrawHeroPanel(character, picked);
            ImGui.Spacing();
            ProfilesPageEditor.DrawVisibilityPanel(picked, _selectedProfile);
            ImGui.Spacing();
            ProfilesPageEditor.DrawContentWarningsPanel(picked);
            ImGui.Spacing();

            DrawTabBar(character, picked);
            ImGui.Spacing();
            DrawTabBody(character, picked);
        }

        if (_newProfileOpen)           DrawNewProfileModal(character);
        if (_removeProfileConfirmOpen) DrawRemoveProfileConfirm(character);
        if (_newTabOpen)               DrawNewTabModal(character);
        if (_removeTabConfirmOpen)     DrawRemoveTabConfirm(character);

        // Anchor the whole page as the tutorial's "window" bounding rect, then render the tutorial popup on top.
        AbsoluteRP.Helpers.TutorialManager.AnchorRect(
            AbsoluteRP.Helpers.ProfileTutorial.Anchor_Window,
            windowMin, ImGui.GetCursorScreenPos() + ImGui.GetContentRegionAvail());
        AbsoluteRP.Helpers.TutorialManager.Draw();
    }

    // Swap the video-background session if the picked profile's bytes just became a video (or switched to a different video/image). Writes the bytes to a temp file for LibVLC to consume, spins up a muted looping session, and disposes the previous one.
    private static void SyncBackgroundVideo(ProfileData? picked)
    {
        var bytes = picked?.backgroundBytes;
        var isVideo = bytes != null && bytes.Length > 12 && LooksLikeVideoBytes(bytes);

        // Not a video? Tear the session down (image path takes over). Deferred dispose - the current frame's ImGui draw list may still reference the SRV from CurrentTextureId a moment ago.
        if (!isVideo)
        {
            if (_bgVideo != null)
            {
                RetireBackgroundVideo(_bgVideo);
                _bgVideo = null;
                _bgVideoBytesHash = 0;
            }
            return;
        }

        // Same content as last frame? Session already covers it. Compare by CONTENT hash - a Save round-trip produces a new byte[] with identical bytes, and reference-equality would incorrectly tear the session down (which was crashing the NVIDIA driver).
        var hash = HashBytes(bytes!);
        if (_bgVideo != null && hash == _bgVideoBytesHash
            && (!_bgVideo.RendererCrashed || !MayRestartBgVideo((ulong)hash))) return;

        // Genuinely new / changed video - swap sessions. Same reasoning as the "not a video" branch: defer the old session's dispose one frame so the driver's async work on the outgoing SRV can drain before we free it. Temp-file cleanup rides along in DrainRetiredVideos() to stay paired with the dispose.
        if (_bgVideo != null)
        {
            RetireBackgroundVideo(_bgVideo);
            _bgVideo = null;
        }

        try
        {
            // Prefer the HTTP-streaming URL when the server provided one (RSUR marker) - no disk write, no full download, LibVLC starts playback as soon as bytes arrive.
            if (TryExtractBackgroundUrl(bytes!, out var streamUrl))
            {
                _bgVideoBytesHash = hash;
                _bgVideo = new AbsoluteRP.Video.VideoPlayerSession(
                    Plugin.PluginInterface, Plugin.PluginLog, streamUrl, initialVolume: 0);
                return;
            }

            // Legacy: raw video container inline in the bytes. Write to a hashed temp file so we can hand LibVLC a file:// URL.
            var dir = Plugin.PluginInterface?.ConfigDirectory?.FullName;
            if (string.IsNullOrEmpty(dir)) return;
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, "profile_bg_" + hash.ToString("x16") + ".bin");
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
            Plugin.PluginLog.Debug("Video-background session start failed: " + ex.Message);
            _bgVideo = null;
        }
    }

    // Called from Plugin.Dispose so an unload / reload doesn't leave the LibVLC renderer subprocess alive holding its .exe open. Runs synchronously - the plugin is going away, no more frames, no more draws, no use-after-free risk to defer for.
    public static void DisposeBackgroundVideo()
    {
        DrainRetiredVideos();
        if (_bgVideo != null)
        {
            try { _bgVideo.Dispose(); } catch { }
            _bgVideo = null;
        }
        _bgVideoBytesHash = 0;
        DeleteTempVideoFile();
    }

    private static void DeleteTempVideoFile()
    {
        var p = _bgVideoTempPath;
        _bgVideoTempPath = null;
        if (string.IsNullOrEmpty(p)) return;
        try { if (System.IO.File.Exists(p)) System.IO.File.Delete(p); }
        catch { }
    }

    // Draws the currently-selected profile's background image or video behind the entire page content. Panels above are translucent (RsTheme.PanelSurface) so the media shows through.
    private static void DrawProfileBackdrop(Vector2 pageMin, Vector2 pageMax)
    {
        // Video takes priority over the still image when a session is live.
        var vid = _bgVideo;
        var texHandle = default(Dalamud.Bindings.ImGui.ImTextureID);
        int texW = 0, texH = 0;
        if (vid != null && !vid.RendererCrashed)
        {
            var vt = vid.CurrentTextureId;
            if (vt != IntPtr.Zero)
            {
                texHandle = new Dalamud.Bindings.ImGui.ImTextureID(vt);
                texW = vid.Width;
                texH = vid.Height;
            }
        }
        if (texW == 0)
        {
            var img = backgroundImage;
            if (img == null || img.Handle == IntPtr.Zero) return;
            texHandle = img.Handle;
            texW = img.Width;
            texH = img.Height;
        }
        if (texW <= 0 || texH <= 0) return;

        var draw = ImGui.GetWindowDrawList();
        // Aspect-fill: cover the page area, no distortion.
        var pageW = pageMax.X - pageMin.X;
        var pageH = pageMax.Y - pageMin.Y;
        var scale = MathF.Max(pageW / texW, pageH / texH);
        var drawW = texW * scale;
        var drawH = texH * scale;
        var offX = (pageW - drawW) * 0.5f;
        var offY = (pageH - drawH) * 0.5f;
        var uMin = new Vector2(-offX / drawW, -offY / drawH);
        var uMax = new Vector2((pageW - offX) / drawW, (pageH - offY) / drawH);

        // Dim tint so text on top stays legible even over bright media.
        var tint = ImGui.ColorConvertFloat4ToU32(new Vector4(0.65f, 0.65f, 0.65f, 1f));
        draw.AddImage(texHandle, pageMin, pageMax, uMin, uMax, tint);
    }

    // Force a re-fetch of the currently-selected profile whenever the page becomes visible again (window reopened, or user came back from Social/another tab). Between deselect and re-select, other code paths - fetches for target profiles, group members, inventory previews - can clear the shared DataReceiver counters (tabsCount / loadedTabsCount / tabCountReceived) that the profile draw uses to know whether tab data has arrived. Without this refresh the page comes back showing just the avatar and title while the tabs and body sit empty because the client thinks the fetch already completed.
    public void OnSelected()
    {
        _lastFetchKey = "";
        _profilesRequested = false;
    }

    // Tear down the video pipeline whenever this page goes away - hides MainWindow closing, tab switch, whatever. Leaving the renderer subprocess running while the SRV isn't drawn each frame lets the shared D3D11 texture drift into a stale state, and the driver crashes on the next reopen (NVIDIA nvwgf2umx crash). Recreating on demand costs one subprocess spin-up per reopen, which is fine.
    public void OnDeselected()
    {
        try { DisposeBackgroundVideo(); } catch { }
    }

    // tutorial plumbing

    private static bool _tutorialAutoStartAttempted;

    private static void MaybeAutoStartTutorial()
    {
        if (_tutorialAutoStartAttempted) return;
        _tutorialAutoStartAttempted = true;
        var cfg = Plugin.plugin?.Configuration;
        if (cfg == null) return;
        if (cfg.TutorialsEnabled && !cfg.ProfileTutorialCompleted
            && !AbsoluteRP.Helpers.TutorialManager.Active)
        {
            AbsoluteRP.Helpers.ProfileTutorial.Install();
            AbsoluteRP.Helpers.TutorialManager.Start(
                AbsoluteRP.Helpers.ProfileTutorial.Flow,
                AbsoluteRP.Helpers.ProfileTutorial.Step_Welcome);
        }
    }

    private void DrawTutorialControlsRow()
    {
        var cfg = Plugin.plugin?.Configuration;
        if (cfg == null) return;

        var enabled = cfg.TutorialsEnabled;
        if (RsElements.Checkbox("Tutorials##prof.tut.toggle", ref enabled))
        {
            cfg.TutorialsEnabled = enabled;
            cfg.Save();
            if (!enabled)
            {
                AbsoluteRP.Helpers.TutorialManager.Stop();
            }
            else
            {
                cfg.ProfileTutorialCompleted = false;
                cfg.Save();
                AbsoluteRP.Helpers.ProfileTutorial.Install();
                AbsoluteRP.Helpers.TutorialManager.Start(
                    AbsoluteRP.Helpers.ProfileTutorial.Flow,
                    AbsoluteRP.Helpers.ProfileTutorial.Step_Welcome);
            }
        }
        AbsoluteRP.Helpers.TutorialManager.Anchor(
            AbsoluteRP.Helpers.ProfileTutorial.Anchor_TutorialToggle);

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        ImGui.TextUnformatted("(?)");
        ImGui.PopStyleColor();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Walks you through the profile editor step by step. Untick to stop showing the guide.");

        if (AbsoluteRP.Helpers.TutorialManager.Active || enabled)
        {
            ImGui.SameLine();
            var label = AbsoluteRP.Helpers.TutorialManager.Active ? "Restart" : "Start";
            if (RsElements.Button(label + "##prof.tut.action", RsElements.ButtonVariant.Ghost))
            {
                cfg.ProfileTutorialCompleted = false;
                cfg.Save();
                AbsoluteRP.Helpers.ProfileTutorial.Install();
                AbsoluteRP.Helpers.TutorialManager.Start(
                    AbsoluteRP.Helpers.ProfileTutorial.Flow,
                    AbsoluteRP.Helpers.ProfileTutorial.Step_Welcome);
            }
        }
    }

    // character row

    private Character? CurrentCharacter()
    {
        var cfg = Plugin.plugin?.Configuration;
        if (cfg == null || cfg.characters.Count == 0) return null;
        if (_selectedCharacter < 0 || _selectedCharacter >= cfg.characters.Count) _selectedCharacter = 0;
        return cfg.characters[_selectedCharacter];
    }

    // Per-character verification: having a stored Lodestone characterKey is the source of truth. This intentionally does NOT gate on the session-wide Plugin.characterKeysVerified flag - that flag flips false during transient disconnects, which would wrongly block editing profiles on characters the user has already verified. A key that the server has revoked will fail the eventual send anyway; blocking the UI on that hypothetical is worse UX than letting the user work across all their verified characters.
    private static bool IsCharacterVerified(Character? c)
        => c != null && !string.IsNullOrEmpty(c.characterKey);

    // Placeholder Character used to drive the verification card when there's no linked character yet. We seed it from the currently logged-in FFXIV character (via Dalamud's ClientState) so the panel title reads with a real name - the server-side registration is driven by the Lodestone URL, so it doesn't matter that the placeholder has no characterKey.
    private static Character BuildPlaceholderCharacter()
    {
        try
        {
            var lp = Plugin.ObjectTable?.LocalPlayer;
            if (lp != null)
            {
                var name  = lp.Name?.TextValue ?? "";
                var world = lp.HomeWorld.Value.Name.ExtractText() ?? "";
                return new Character
                {
                    characterName  = name,
                    characterWorld = world,
                    characterKey   = string.Empty,
                };
            }
        }
        catch { }
        return new Character
        {
            characterName  = "your character",
            characterWorld = "",
            characterKey   = string.Empty,
        };
    }

    private void DrawCharacterRow()
    {
        var cfg = Plugin.plugin?.Configuration;
        var names = cfg == null
            ? new List<string>()
            : cfg.characters.Select(c =>
                c.characterName + " @ " + c.characterWorld
                + (string.IsNullOrEmpty(c.characterKey) ? "  (unverified)" : "")
              ).ToList();
        if (names.Count == 0) names.Add("(no linked characters)");

        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        ImGui.Text("Character");
        ImGui.PopStyleColor();
        ImGui.SameLine();

        var before = _selectedCharacter;
        RsElements.Dropdown("prof_char", ref _selectedCharacter, names, width: 260f);
        if (_selectedCharacter != before)
        {
            ProfileWindow.profiles.Clear();
            _profilesRequested = false;
            _lastFetchKey = "";
            _selectedProfile = 0;
            _selectedTab = 0;
            // Collapse the verify form back to its call-to-action button whenever the user switches characters. Prevents a half-typed URL from one character sticking around while looking at another's verification prompt.
            _showVerifyForm = false;
            _lodestoneUrl = "";
        }

        ImGui.SameLine();
        var canRefresh = CurrentCharacter()?.characterKey?.Length > 0;
        if (!canRefresh) ImGui.BeginDisabled();
        if (RsElements.Button("Refresh", RsElements.ButtonVariant.Ghost))
        {
            var c = CurrentCharacter();
            if (c != null)
            {
                _profilesRequestTime = ImGui.GetTime();
                _lastFetchKey = "";
                Profiles_DS.FetchProfiles(c);
            }
        }
        if (!canRefresh) ImGui.EndDisabled();
    }

    // lodestone verification

    private void DrawVerificationCard(Character character)
    {
        if (!RsElements.BeginPanel("prof_verify",
                                    "Verify " + character.characterName,
                                    fitContentsX: false, fitContentsY: true))
        {
            RsElements.EndPanel();
            return;
        }

        try
        {
            // First-stage call-to-action: a single "Verify Character" button. Users don't need to see the URL form (or the multi-step instructions) until they've committed to starting the verification flow.
            if (!_showVerifyForm)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextSecondary);
                ImGui.TextWrapped($"{character.characterName} @ {character.characterWorld} isn't verified yet. Verifying links this character to your account so you can create and view profiles for it.");
                ImGui.PopStyleColor();
                ImGui.Spacing();

                if (RsElements.Button("Verify Character", RsElements.ButtonVariant.Primary,
                                      new Vector2(RsTheme.S(200f), 0f)))
                {
                    _showVerifyForm = true;
                    _lodestoneUrl = "";
                    ProfileWindow.lodeStoneKey = "";
                    ProfileWindow.VerificationSucceeded = false;
                    ProfileWindow.VerificationFailed    = false;
                }
                return;
            }

            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextSecondary);
            ImGui.TextWrapped("Paste your character's public Lodestone URL, click Get Token, drop the token in your character's Lodestone profile bio, then Verify.");
            ImGui.PopStyleColor();
            ImGui.Spacing();

            RsElements.InputText("verify_url", ref _lodestoneUrl, 256,
                                 placeholder: "https://na.finalfantasyxiv.com/lodestone/character/...",
                                 width: 480f);
            ImGui.Spacing();

            // Must be the accountKey, NOT accountName. Both the "Get Token" (SendLodestoneURL) and "Verify" (CheckLodestoneKey) server handlers key the character_keys row by the same encrypted account tag. CheckLodestoneEntry internally sends accountKey, so SubmitLodestoneURL has to as well - otherwise the two rows never join and verification always returns "Not Validated".
            var accountTag = Plugin.plugin?.Configuration?.account?.accountKey ?? "";
            var canSubmit = !string.IsNullOrWhiteSpace(_lodestoneUrl) && !string.IsNullOrEmpty(accountTag);

            if (!canSubmit) ImGui.BeginDisabled();
            if (RsElements.Button("Get Token", RsElements.ButtonVariant.Primary))
            {
                ProfileWindow.lodeStoneKey = "";
                ProfileWindow.VerificationSucceeded = false;
                ProfileWindow.VerificationFailed    = false;
                ProfileWindow.checking = true;
                Accounts_DS.SubmitLodestoneURL(_lodestoneUrl, accountTag, restoration: false);
            }
            ImGui.SameLine();
            // Verify only makes sense once a token exists to look for.
            var haveToken = !string.IsNullOrEmpty(ProfileWindow.lodeStoneKey);
            if (!haveToken) ImGui.BeginDisabled();
            var verifyClicked = RsElements.Button("Verify", RsElements.ButtonVariant.Success);
            if (!haveToken) ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(haveToken ? "Check your Lodestone bio for the token." : "Click Get Token first, then paste the token into your Lodestone bio.");
            if (verifyClicked && haveToken)
            {
                // Verify asks the server to inspect the character's Lodestone bio for the token we handed out under "Get Token" - it must NOT call SubmitLodestoneURL, which would just wipe the token and issue a fresh one, leaving the user in an endless "get token -> verify -> new token" loop.
                ProfileWindow.checking = true;
                Accounts_DS.CheckLodestoneEntry(_lodestoneUrl, restoration: false);
            }
            AbsoluteRP.Helpers.TutorialManager.Anchor(AbsoluteRP.Helpers.ProfileTutorial.Anchor_VerifyBtn);
            if (!canSubmit) ImGui.EndDisabled();

            if (!string.IsNullOrEmpty(ProfileWindow.lodeStoneKey))
            {
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.Text("Token to paste in your Lodestone bio:");
                ImGui.PopStyleColor();

                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentPrimary);
                ImGui.TextUnformatted(ProfileWindow.lodeStoneKey);
                ImGui.PopStyleColor();
                ImGui.SameLine();
                if (RsElements.Button("Copy", RsElements.ButtonVariant.Ghost))
                {
                    try { ImGui.SetClipboardText(ProfileWindow.lodeStoneKey); }
                    catch (Exception ex) { Plugin.PluginLog.Debug("Copy token: " + ex.Message); }
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Copy the verification token to your clipboard");
            }

            if (ProfileWindow.checking)
            {
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.Text("Waiting for server...");
                ImGui.PopStyleColor();
            }
        }
        finally
        {
            RsElements.EndPanel();
        }
    }

    // profile picker + create/delete

    private void DrawProfileRow(Character character)
    {
        var names = ProfileWindow.profiles
            .Select(p => string.IsNullOrEmpty(p.title) ? "(untitled)" : p.title)
            .ToList();
        if (names.Count == 0) names.Add("(no profiles)");

        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        ImGui.Text("Profile");
        ImGui.PopStyleColor();
        ImGui.SameLine();

        if (_selectedProfile < 0 || _selectedProfile >= names.Count) _selectedProfile = 0;
        var beforeProfile = _selectedProfile;
        RsElements.Dropdown("prof_pick", ref _selectedProfile, names, width: 260f);
        if (_selectedProfile != beforeProfile)
        {
            // Force a re-fetch for the newly picked profile.
            _lastFetchKey = "";
            _selectedTab = 0;
        }

        ImGui.SameLine();
        var canAddProfile = IsCharacterVerified(character);
        if (!canAddProfile) ImGui.BeginDisabled();
        if (RsElements.Button("+ New", RsElements.ButtonVariant.Ghost))
        {
            _newProfileTitle = "";
            _newProfileOpen = true;
            // Keep the tutorial's AutoAdvanceWhen predicate happy - the Step_AddProfile step waits for `showTypeCreation` to flip true, which used to be tied to the legacy Profile Creation popup. Our new modal is the equivalent surface.
            ProfilesPage.showTypeCreation = true;
        }
        if (!canAddProfile) ImGui.EndDisabled();
        if (!canAddProfile && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Verify this character on Lodestone before creating a profile for it.");
        AbsoluteRP.Helpers.TutorialManager.Anchor(AbsoluteRP.Helpers.ProfileTutorial.Anchor_AddProfileBtn);

        if (ProfileWindow.profiles.Count > 0)
        {
            ImGui.SameLine();
            if (RsElements.Button("Delete", RsElements.ButtonVariant.Danger))
            {
                _removeProfileConfirmOpen = true;
            }
        }
    }

    private void DrawNewProfileModal(Character character)
    {
        ImGui.OpenPopup("##arp_new_profile");
        CenterModal(380f);
        if (ImGui.BeginPopupModal("##arp_new_profile", ref _newProfileOpen,
                                  ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
            ImGui.Text("New profile");
            ImGui.PopStyleColor();
            ImGui.Spacing();

            RsElements.InputText("np_title", ref _newProfileTitle, 60, placeholder: "Profile title", width: 340f);
            AbsoluteRP.Helpers.TutorialManager.Anchor(AbsoluteRP.Helpers.ProfileTutorial.Anchor_ProfileTitle);
            ImGui.Spacing();

            // Final gate on the modal itself - the outer flow already hides the "+ New" button for unverified characters, but a stale modal (character un-linked while the popup was open, characterKeysVerified flipped false mid-session) would otherwise still fire CreateProfile.
            var canSubmit = !string.IsNullOrWhiteSpace(_newProfileTitle)
                            && IsCharacterVerified(character);
            if (!canSubmit) ImGui.BeginDisabled();
            var __createMin = ImGui.GetCursorScreenPos();
            if (RsElements.Button("Create", RsElements.ButtonVariant.Primary, new Vector2(RsTheme.S(120f), 0f)))
            {
                var trimmed = _newProfileTitle.Trim();
                var nextIndex = ProfileWindow.profiles.Count == 0
                    ? 0
                    : ProfileWindow.profiles.Max(p => p.index) + 1;
                Profiles_DS.CreateProfile(character, trimmed, 0, nextIndex);
                // Refresh the list and jump to the new profile as soon as the server response lands.
                _pendingSelectByTitle = trimmed;
                _lastFetchKey = "";
                Profiles_DS.FetchProfiles(character);
                _newProfileOpen = false;
                ProfilesPage.showTypeCreation = false;
                ImGui.CloseCurrentPopup();
            }
            AbsoluteRP.Helpers.TutorialManager.AnchorRect(
                AbsoluteRP.Helpers.ProfileTutorial.Anchor_CreateBtn,
                __createMin, ImGui.GetItemRectMax());
            if (!canSubmit) ImGui.EndDisabled();
            ImGui.SameLine();
            if (RsElements.Button("Cancel", RsElements.ButtonVariant.Ghost, new Vector2(RsTheme.S(120f), 0f)))
            {
                _newProfileOpen = false;
                ProfilesPage.showTypeCreation = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    private void DrawRemoveProfileConfirm(Character character)
    {
        ImGui.OpenPopup("##arp_del_profile");
        CenterModal(380f);
        if (ImGui.BeginPopupModal("##arp_del_profile", ref _removeProfileConfirmOpen,
                                  ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize))
        {
            var p = SelectedProfile();
            var name = p?.title ?? "(unknown)";
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentDanger);
            ImGui.Text($"Delete profile \"{name}\"?");
            ImGui.PopStyleColor();
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextSecondary);
            ImGui.TextWrapped("Hold Ctrl to enable Delete. This cannot be undone.");
            ImGui.PopStyleColor();
            ImGui.Spacing();

            if (RsElements.Button("Cancel", RsElements.ButtonVariant.Ghost, new Vector2(RsTheme.S(120f), 0f)))
            {
                _removeProfileConfirmOpen = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            var ctrl = ImGui.GetIO().KeyCtrl;
            if (!ctrl) ImGui.BeginDisabled();
            if (RsElements.Button("Delete", RsElements.ButtonVariant.Danger, new Vector2(RsTheme.S(120f), 0f)))
            {
                if (p != null) Profiles_DS.DeleteProfile(character, p.index);
                // Move the picker to a neighbour so we don't sit on a vanished index while the server response is in flight, then force a list refetch and clear the last-fetch key so the newly-selected profile actually loads.
                if (_selectedProfile > 0) _selectedProfile--;
                _selectedTab = 0;
                _lastFetchKey = "";
                _pendingSelectByTitle = null;
                Profiles_DS.FetchProfiles(character);
                _removeProfileConfirmOpen = false;
                ImGui.CloseCurrentPopup();
            }
            if (!ctrl) ImGui.EndDisabled();
            ImGui.EndPopup();
        }
    }

    // tabs

    private void DrawTabBar(Character character, ProfileData profile)
    {
        var canAdd = profile.customTabs.Count < MaxTabs;
        if (!canAdd) ImGui.BeginDisabled();
        if (RsElements.Button(profile.customTabs.Count == 0 ? "New tab" : "+ Add Page",
                              RsElements.ButtonVariant.Ghost))
        {
            _newTabName = "";
            _newTabLayoutIdx = 0; // "Bio" in NewTabLayoutNames
            _newTabOpen = true;
        }
        AbsoluteRP.Helpers.TutorialManager.Anchor(AbsoluteRP.Helpers.ProfileTutorial.Anchor_AddTabBtn);
        if (!canAdd) ImGui.EndDisabled();
        if (profile.customTabs.Count == 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.Text("No tabs yet. Add one to start building this profile.");
            ImGui.PopStyleColor();
        }
        else
        {
            if (_selectedTab < 0 || _selectedTab >= profile.customTabs.Count) _selectedTab = 0;

            var items = profile.customTabs
                .Select(t => new RsElements.NavItem(IconFor(t.type),
                                                    string.IsNullOrEmpty(t.Name) ? "(untitled)" : t.Name))
                .ToList();
            RsElements.NavigationMenu("prof_tabs", ref _selectedTab, items,
                                      onClose:   i => { _pendingRemoveTab = i; _removeTabConfirmOpen = true; },
                                      onReorder: (from, to) => ReorderTab(character, profile, from, to));
        }

       
    }

    private void DrawTabBody(Character character, ProfileData profile)
    {
        if (profile.customTabs.Count == 0) return;
        if (_selectedTab < 0 || _selectedTab >= profile.customTabs.Count) return;

        var tab = profile.customTabs[_selectedTab];
        ProfilesPageEditor.DrawTabBody(character, profile, tab, _selectedTab);
    }

    private void DrawNewTabModal(Character character)
    {
        ImGui.OpenPopup("##arp_new_tab");
        CenterModal(380f);
        if (ImGui.BeginPopupModal("##arp_new_tab", ref _newTabOpen,
                                  ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
            ImGui.Text("New tab");
            ImGui.PopStyleColor();
            ImGui.Spacing();

            RsElements.InputText("nt_name", ref _newTabName, 60, placeholder: "Tab name", width: 340f);
            AbsoluteRP.Helpers.TutorialManager.Anchor(AbsoluteRP.Helpers.ProfileTutorial.Anchor_NewPageName);
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.Text("Layout");
            ImGui.PopStyleColor();
            var layoutDdMin = ImGui.GetCursorScreenPos();
            RsElements.Dropdown("nt_layout", ref _newTabLayoutIdx, NewTabLayoutNames, width: 340f);
            var layoutDdMax = ImGui.GetItemRectMax();
            AbsoluteRP.Helpers.TutorialManager.AnchorRect(AbsoluteRP.Helpers.ProfileTutorial.Anchor_LayoutDropdown, layoutDdMin, layoutDdMax);
            AbsoluteRP.Helpers.TutorialManager.AnchorRect(AbsoluteRP.Helpers.ProfileTutorial.Anchor_LayoutItem, layoutDdMin, layoutDdMax);
            ImGui.Spacing();

            var canSubmit = !string.IsNullOrWhiteSpace(_newTabName);
            if (!canSubmit) ImGui.BeginDisabled();
            var __submitTabMin = ImGui.GetCursorScreenPos();
            if (RsElements.Button("Create", RsElements.ButtonVariant.Primary, new Vector2(RsTheme.S(120f), 0f)))
            {
                var profile = SelectedProfile();
                if (profile != null)
                {
                    var layoutInt = (_newTabLayoutIdx >= 0 && _newTabLayoutIdx < NewTabLayoutValues.Length)
                        ? NewTabLayoutValues[_newTabLayoutIdx]
                        : (int)LayoutTypes.Bio;
                    var nextIndex = profile.customTabs.Count + 1;
                    _ = ProfileTabs_DS.CreateTab(character, _newTabName.Trim(), layoutInt, profile.index, nextIndex);
                    // Ensure the newly created tab arrives from the server by clearing the fetch key so the next Draw re-fetches.
                    _lastFetchKey = "";
                }
                _newTabOpen = false;
                ImGui.CloseCurrentPopup();
            }
            AbsoluteRP.Helpers.TutorialManager.AnchorRect(
                AbsoluteRP.Helpers.ProfileTutorial.Anchor_SubmitTab,
                __submitTabMin, ImGui.GetItemRectMax());
            if (!canSubmit) ImGui.EndDisabled();
            ImGui.SameLine();
            if (RsElements.Button("Cancel", RsElements.ButtonVariant.Ghost, new Vector2(RsTheme.S(120f), 0f)))
            {
                _newTabOpen = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    private void DrawRemoveTabConfirm(Character character)
    {
        ImGui.OpenPopup("##arp_tab_remove");
        CenterModal(380f);
        if (ImGui.BeginPopupModal("##arp_tab_remove", ref _removeTabConfirmOpen,
                                  ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize))
        {
            var profile = SelectedProfile();
            var tab = (profile != null && _pendingRemoveTab >= 0 && _pendingRemoveTab < profile.customTabs.Count)
                ? profile.customTabs[_pendingRemoveTab] : null;
            var name = tab == null ? "" : (string.IsNullOrEmpty(tab.Name) ? "(untitled)" : tab.Name);
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentDanger);
            ImGui.Text($"Delete tab \"{name}\"?");
            ImGui.PopStyleColor();
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextSecondary);
            ImGui.TextWrapped("Hold Ctrl to enable Delete.");
            ImGui.PopStyleColor();
            ImGui.Spacing();

            if (RsElements.Button("Cancel", RsElements.ButtonVariant.Ghost, new Vector2(RsTheme.S(120f), 0f)))
            {
                _removeTabConfirmOpen = false;
                _pendingRemoveTab = -1;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            var ctrl = ImGui.GetIO().KeyCtrl;
            if (!ctrl) ImGui.BeginDisabled();
            if (RsElements.Button("Delete", RsElements.ButtonVariant.Danger, new Vector2(RsTheme.S(120f), 0f)))
            {
                if (profile != null && tab != null)
                {
                    // Use the server-authoritative tab_index (captured at fetch time as CustomTab.ID), not the local layout.tabIndex which the reorder path mutates. The two only match when the server's reorder persistence is fully in sync, and if it isn't the client's post-reorder tabIndex would delete the wrong DB row.
                    ProfileTabs_DS.DeleteTab(character, profile.index, tab.ID, tab.type);
                    _lastFetchKey = "";
                }
                _removeTabConfirmOpen = false;
                _pendingRemoveTab = -1;
                ImGui.CloseCurrentPopup();
            }
            if (!ctrl) ImGui.EndDisabled();
            ImGui.EndPopup();
        }
    }

    private void ReorderTab(Character character, ProfileData profile, int from, int to)
    {
        var tabs = profile.customTabs;
        if (from < 0 || from >= tabs.Count) return;
        if (to   < 0 || to   >= tabs.Count) return;
        if (from == to) return;

        // Build the (oldIndex, newIndex) pair list the server expects so every displaced slot gets its own row, matching ProfileWindow's reorder popup semantics. The server moves tabs by their stored tab_index, which is what each layout carries - not necessarily the list position (a deleted tab can leave a gap). Send (storedIndex -> newPosition) for every tab that moves.
        var stored = new List<int>();
        for (int i = 0; i < tabs.Count; i++) stored.Add(GetLayoutTabIndex(tabs[i].Layout, i));
        var order = stored.ToList();
        var movedIdx = order[from];
        order.RemoveAt(from);
        order.Insert(to, movedIdx);

        var indexChanges = new List<(int oldIndex, int newIndex)>();
        for (int newIdx = 0; newIdx < order.Count; newIdx++)
        {
            if (order[newIdx] != newIdx)
                indexChanges.Add((order[newIdx], newIdx));
        }

        var moved = tabs[from];
        tabs.RemoveAt(from);
        tabs.Insert(to, moved);

        _ = ProfileTabs_DS.SendTabReorder(character, profile.index, indexChanges);

        for (int i = 0; i < tabs.Count; i++)
            SetLayoutTabIndex(tabs[i].Layout, i);

        if (_selectedTab == from)                                          _selectedTab = to;
        else if (from < to && _selectedTab > from && _selectedTab <= to)   _selectedTab--;
        else if (from > to && _selectedTab >= to && _selectedTab < from)   _selectedTab++;
    }

    // helpers

    private ProfileData? SelectedProfile()
    {
        if (_selectedProfile < 0 || _selectedProfile >= ProfileWindow.profiles.Count) return null;
        return ProfileWindow.profiles[_selectedProfile];
    }

    private static int GetLayoutTabIndex(CustomLayout layout, int fallback)
    {
        if (layout == null) return fallback;
        return layout switch
        {
            BioLayout b         => b.tabIndex,
            DetailsLayout d     => d.tabIndex,
            StoryLayout s       => s.tabIndex,
            InfoLayout inf      => inf.tabIndex,
            GalleryLayout g     => g.tabIndex,
            InventoryLayout inv => inv.tabIndex,
            TreeLayout tr       => tr.tabIndex,
            DynamicLayout dyn   => dyn.tabIndex,
            _                   => fallback,
        };
    }

    private static void SetLayoutTabIndex(CustomLayout layout, int index)
    {
        if (layout == null) return;
        switch (layout)
        {
            case BioLayout b:         b.tabIndex   = index; break;
            case DetailsLayout d:     d.tabIndex   = index; break;
            case StoryLayout s:       s.tabIndex   = index; break;
            case InfoLayout inf:      inf.tabIndex = index; break;
            case GalleryLayout g:     g.tabIndex   = index; break;
            case InventoryLayout inv: inv.tabIndex = index; break;
            case TreeLayout tr:       tr.tabIndex  = index; break;
            case DynamicLayout dyn:   dyn.tabIndex = index; break;
        }
    }

    private static void CenterModal(float widthUnscaled)
    {
        var view = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(view.WorkPos + view.WorkSize * 0.5f,
                               ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(RsTheme.S(widthUnscaled), 0f));
    }

    private static void RenderCallout(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }

    private static FontAwesomeIcon IconFor(int layoutType)
    {
        // Map the raw LayoutTypes int to the same glyph the legacy window used, so tab icons stay recognizable across the port.
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
}
