using AbsoluteRP;
using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI.Pages;
using AbsoluteRP.Windows.Ect;
using AbsoluteRP.Windows.Listings;
using AbsoluteRP.Windows;
using AbsoluteRP.Windows.Moderator;
using AbsoluteRP.Windows.Profiles;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using AbsoluteRP.Windows.Social.Views;
using AbsoluteRP.Windows.Social.Views.Groups;
using AbsoluteRP.Windows.Social.Views.SubViews;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Common.Math;
using Serilog;
using System.Linq;
using System.Xml.Linq;
using Networking;

namespace AbsoluteRP.Network
{
    // Profile, tab and gallery packets. Split out of DataReceiver.
    internal class Profiles_DR
    {
        // Profile loading progress tracking -1 = not started, 0 = empty/none, 1 = loaded. Used by the UI to show loading indicators and know when all sections are ready.
        public static bool LoadedSelf = false;

        public static int BioLoadStatus = -1, HooksLoadStatus = -1, StoryLoadStatus = -1, OOCLoadStatus = -1, GalleryLoadStatus = -1, BookmarkLoadStatus = -1,
                          TargetBioLoadStatus = -1, TargetHooksLoadStatus = -1, TargetStoryLoadStatus = -1, TargetOOCLoadStatus = -1, TargetGalleryLoadStatus = -1, TargetNotesLoadStatus = -1;

        public static Dictionary<int, string> profiles = new Dictionary<int, string>();
        public static int tabsCount = 0, loadedTabsCount = 0;
        public static bool tabCountReceived = false;
        public static bool silentUpdate;
        public static bool allLoaded;
        public static int loadedGalleryImages = 0;
        public static int GalleryImagesToLoad = 0;
        internal static int loadedTargetTabsCount;
        internal static int tabsTargetCount;
        internal static int loadedTargetGalleryImages;
        public static int ListingsLoadStatus { get; internal set; }
        public static int TargetGalleryImagesToLoad { get; internal set; }

        // Makes sure the target profile data object exists before handlers try to write into it. Prevents null reference exceptions when viewing another player's profile.
        internal static void EnsureTargetProfileData()
        {
            if (TargetProfileWindow.profileData == null)
                TargetProfileWindow.profileData = new ProfileData();
            if (TargetProfileWindow.profileData.customTabs == null)
                TargetProfileWindow.profileData.customTabs = new List<CustomTab>();
        }

        // Receives the player's saved bookmarks list and populates the Bookmarks window.
        public static void RecBookmarks(byte[] data)
        {
            try
            {
                Bookmarks.profileList.Clear();
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int bookmarkCount = buffer.ReadInt();
                    for (int i = 0; i < bookmarkCount; i++)
                    {
                        int profileIndex = buffer.ReadInt();
                        string profileName = buffer.ReadString();
                        string playerName = buffer.ReadString();
                        string playerWorld = buffer.ReadString();
                        Bookmark bookmark = new Bookmark() { profileIndex = profileIndex, ProfileName = profileName, PlayerName = playerName, PlayerWorld = playerWorld };
                        Bookmarks.profileList.Add(bookmark);
                    }
                    _ = Task.Run(async () => { try { await Plugin.plugin.UpdateStatusAsync(); } catch { } });
                    // Handle the message as needed
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling Bookmark message: {ex}");
            }
        }

        // Confirmation that a profile report was submitted to moderation
        public static void RecProfileReportedSuccessfully(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    ReportWindow.reportStatus = "Profile reported successfully. We are on it!";
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling RecProfileReportSuccessfully message: {ex}");
            }
        }

        public static void RecProfileAlreadyReported(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    ReportWindow.reportStatus = "Profile has already been reported!";
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling RecProfileAlreadyReported message: {ex}");
            }

        }

        // Server indicates the user has no profiles yet. Resets all profile-related. UI state so the user sees a clean slate to create their first profile. True when the last target fetch answered "no such profile".
        public static bool TargetHasNoProfile;
        public static bool targetTabCountReceived;

        public static void NoProfile(byte[] data)
        {
            try
            {
                // While another player's profile is being fetched, this reply is about them: say so and stop the loading screen, leaving the viewer's own profiles alone.
                bool forTarget = TargetProfileWindow.PendingFetch || TargetProfileWindow.RequestingProfile;
                if (forTarget && !ProfilesPage.Fetching)
                {
                    TargetHasNoProfile = true;
                    TargetProfileWindow.ExistingProfile = false;
                    TargetProfileWindow.AccessDenied = true;
                    TargetProfileWindow.RequestingProfile = false;
                    TargetProfileWindow.PendingFetch = false;
                    Plugin.plugin.OpenTargetWindow();
                    return;
                }
                // Late reply to a target request the viewer cancelled: drop it, never touch own profiles.
                if (TargetProfileWindow.RecentlyCancelled && !ProfilesPage.Fetching)
                    return;
                using (var buffer = new ByteBuffer())
                {
                    Inventory.ProfileBaseData.Clear();
                    ProfilesPage.profiles.Clear();
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    Accounts_DR.loggedIn = true;
                    BioLoadStatus = 0;
                    HooksLoadStatus = 0;
                    StoryLoadStatus = 0;
                    OOCLoadStatus = 0;
                    GalleryLoadStatus = 0;

                    AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.Story.storyTitle = string.Empty;
                    BookmarkLoadStatus = 0;

                    ProfilesPage.addProfile = false;
                    ProfilesPage.editProfile = false;
                    ProfilesPage.ExistingProfile = false;
                    Inventory.ExistingProfile = false;

                    ProfilesPage.Fetching = false;
                    ProfilesPage.Sending = false;
                    tabsCount = 0;
                    loadedTabsCount = 0;
                    tabCountReceived = true;
                    GalleryImagesToLoad = 0;
                    loadedGalleryImages = 0;
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling NoProfile message: {ex}");
            }

        }

        // The targeted player has no profile. Resets target profile UI and opens an empty target window so the user sees "no profile found".
        public static void NoTargetProfile(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    Accounts_DR.loggedIn = true;
                    TargetProfileWindow.ExistingProfile = false;
                    TargetProfileWindow.AccessDenied = true;
                    TargetProfileWindow.RequestingProfile = false;
                    TargetBioLoadStatus = 0;
                    TargetHooksLoadStatus = 0;
                    TargetStoryLoadStatus = 0;
                    TargetOOCLoadStatus = 0;
                    TargetGalleryLoadStatus = 0;
                    TargetNotesLoadStatus = 0;
                    TargetProfileWindow.addNotes = false;
                    Bookmarks.DisableBookmarkSelection = false;
                    ReportWindow.reportStatus = "";

                    // Reopen only for a live request; a cancelled one stays closed.
                    bool live = TargetProfileWindow.PendingFetch;
                    TargetProfileWindow.PendingFetch = false;
                    if (live) Plugin.plugin.OpenTargetWindow();
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling NoTargetProfile message: {ex}");
            }
        }

        // Receives core profile metadata (title, color, character info) for either the player's own profile or a target's profile. Routes data to the correct window (ProfileWindow for self, TargetProfileWindow for others).
        public static void HandleTargetProfilePacket(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    string profileTitle = buffer.ReadString();
                    float colorX = buffer.ReadFloat();
                    float colorY = buffer.ReadFloat();
                    float colorZ = buffer.ReadFloat();
                    float colorW = buffer.ReadFloat();
                    string characterName = buffer.ReadString();
                    string characterWorld = buffer.ReadString();
                    bool self = buffer.ReadBool();
                    int profileID = buffer.ReadInt();
                    int accountID = buffer.ReadInt();

                    // Store the profile ID and account ID
                    if (self)
                    {
                        ProfilesPage.CurrentProfile.id = profileID;
                        ProfilesPage.CurrentProfile.accountID = accountID;
                        ProfilesPage.CurrentProfile.playerName = characterName;
                        ProfilesPage.CurrentProfile.playerWorld = characterWorld;
                    }
                    else
                    {
                        TargetProfileWindow.profileData.id = profileID;
                        TargetProfileWindow.profileData.accountID = accountID;
                        TargetProfileWindow.profileData.playerName = characterName;
                        TargetProfileWindow.profileData.playerWorld = characterWorld;
                        // Notes are stored per profile: point the notes window at this one.
                        NotesWindow.characterIndex = profileID;
                        if (!string.IsNullOrEmpty(characterName))
                        {
                            TargetProfileWindow.characterName = characterName;
                            TargetProfileWindow.characterWorld = characterWorld ?? string.Empty;
                        }
                    }

                    Plugin.PluginLog.Debug($"[HandleTargetProfilePacket] Received profile data - ID: {profileID}, AccountID: {accountID}, Name: {characterName}@{characterWorld}, Self: {self}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling HandleTargetProfilePacket message: {ex}");
            }
        }

        // Confirms that a profile exists for the current user. Sets the ExistingProfile flag so the UI knows to show editing controls instead of creation controls.
        public static void ReceiveProfile(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int profileID = buffer.ReadInt();
                    string profileName = buffer.ReadString();
                    ProfilesPage.ExistingProfile = true;
                    Inventory.ExistingProfile = true;
                    Accounts_DR.loggedIn = true;
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveProfile message: {ex}");
            }
        }

        // Notifies the client that a profile already exists at the given index. Sets the CurrentProfile for self profiles, or marks the target as existing.
        public static void ExistingProfile(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int index = buffer.ReadInt();
                    bool self = buffer.ReadBool();
                    if (self)
                    {
                        ProfilesPage.ExistingProfile = true;
                        Inventory.ExistingProfile = true;
                        if (index >= 0 && index < ProfilesPage.profiles.Count)
                        {

                            ProfilesPage.CurrentProfile = ProfilesPage.profiles[index];
                        }
                        else
                        {
                            ProfilesPage.CurrentProfile = new ProfileData();
                            if (ProfilesPage.CurrentProfile.customTabs == null)
                                ProfilesPage.CurrentProfile.customTabs = new List<CustomTab>();
                        }
                    }
                    else
                    {
                        EnsureTargetProfileData();
                        TargetProfileWindow.ExistingProfile = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ExistingProfile message: {ex}");
            }
        }

        // Receives the list of all profiles owned by the current character. Populates the profile selector, inventory base data, and group creation dropdowns.
        public static void ReceiveProfiles(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int profileCount = buffer.ReadInt();
                    ProfilesPage.profiles.Clear();
                    Inventory.ProfileBaseData.Clear();
                    GroupCreation.profiles.Clear();
                    GroupManager.profiles.Clear();
                    for (int i = 0; i < profileCount; i++)
                    {
                        int index = buffer.ReadInt();
                        string name = buffer.ReadString();
                        int profileID = buffer.ReadInt();
                        int accountID = buffer.ReadInt();
                        string playerName = buffer.ReadString();
                        string playerWorld = buffer.ReadString();
                        // Server writes a length-prefixed avatar blob per profile. Not reading it here desyncs the buffer and every subsequent profile-record parse reads garbage - silent failure via the outer catch, visible as "the dropdown only has one entry".
                        int avatarLen = buffer.ReadInt();
                        byte[] avatarBytes = avatarLen > 0
                            ? buffer.ReadBytes(avatarLen)
                            : Array.Empty<byte>();

                        var listed = new ProfileData()
                        {
                            index = index,
                            title = name,
                            id = profileID,
                            accountID = accountID,
                            playerName = playerName,
                            playerWorld = playerWorld,
                            avatarBytes = avatarBytes
                        };
                        ProfilesPage.profiles.Add(listed);
                        // Small texture for pickers (system roster, group creation) that show the list.
                        if (avatarBytes.Length > 0)
                        {
                            var forTex = avatarBytes;
                            _ = System.Threading.Tasks.Task.Run(async () =>
                            {
                                try
                                {
                                    var scaled = Imaging.ScaleImageBytes(forTex, 160, 160);
                                    listed.avatar = await Plugin.TextureProvider.CreateFromImageAsync(scaled);
                                }
                                catch (Exception ex) { Plugin.PluginLog.Debug("profile list avatar: " + ex.Message); }
                            });
                        }
                        Inventory.ProfileBaseData.Add(Tuple.Create(index, name));
                        GroupCreation.profiles.Add(new ProfileData() { index = index, title = name });
                        GroupManager.profiles.Add(new ProfileData() { index = index, title = name });
                    }
                    try { AbsoluteRP.RsUI.Pages.ThemesPage.OnProfilesReceived(ProfilesPage.profiles); } catch { }

                    if (profileCount == 0)
                    {
                        ProfilesPage.Fetching = false;
                        ProfilesPage.Sending = false;
                        ProfilesPage.ExistingProfile = false;
                        tabsCount = 0;
                        loadedTabsCount = 0;
                        tabCountReceived = true;
                        GalleryImagesToLoad = 0;
                        loadedGalleryImages = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveProfileHooks message: {ex}");
            }
        }

        public static void NoProfileNotes(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    NotesWindow.profileNotes = string.Empty;
                    TargetNotesLoadStatus = 0;
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling NoProfileNotes message: {ex}");
            }
        }

        public static void RecProfileNotes(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    string notes = buffer.ReadString();
                    NotesWindow.profileNotes = notes;
                    TargetNotesLoadStatus = 1;
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling RecProfileNotes message: {ex}");
            }
        }

        internal static void ReceiveProfileItems(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int itemsCount = buffer.ReadInt();

                    Inventory.percentage = itemsCount;
                    for (int i = 0; i < itemsCount; i++)
                    {

                        string name = buffer.ReadString();
                        string description = buffer.ReadString();
                        int type = buffer.ReadInt();
                        int subType = buffer.ReadInt();
                        int iconID = buffer.ReadInt();
                        int slotID = buffer.ReadInt();
                        int quality = buffer.ReadInt();
                        ItemDefinition itemDefinition = new ItemDefinition
                        {
                            name = name,
                            description = description,
                            type = type,
                            subtype = subType,
                            iconID = iconID, // Ensure iconID is valid
                            slot = slotID,
                            quality = quality
                        };
                        // Validate and ensure compatibility
                        if (WindowOperations.RenderIconAsync(Plugin.plugin, iconID) == null)
                        {
                            throw new InvalidOperationException($"Invalid iconID: {iconID}");
                        }
                        Inventory.loaderInd = i;

                    }

                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveProfileItems message: {ex}");
            }
        }

        internal static void RecieveProfileWarning(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    bool ARR = buffer.ReadBool();
                    bool HW = buffer.ReadBool();
                    bool SB = buffer.ReadBool();
                    bool SHB = buffer.ReadBool();
                    bool EW = buffer.ReadBool();
                    bool DT = buffer.ReadBool();
                    bool NSFW = buffer.ReadBool();
                    bool TRIGGERING = buffer.ReadBool();
                    // A warning for a cancelled request would block the next fetch; ignore it.
                    if (!TargetProfileWindow.PendingFetch && !Plugin.plugin.IsTargetWindowOpen) return;


                    List<string> spoilers = new List<string>();

                    if (ARR) { spoilers.Add("A Realm Reborn"); }
                    if (HW) { spoilers.Add("Heavensward"); }
                    if (SB) { spoilers.Add("Stormblood"); }
                    if (SHB) { spoilers.Add("Shadowbringers"); }
                    if (EW) { spoilers.Add("Endwalker"); }
                    if (DT) { spoilers.Add("Dawntrail"); }
                    string message = "The tooltipData you are about to view contains:\n";
                    if (NSFW)
                    {
                        message += "NSFW (18+) content \n";
                    }
                    if (TRIGGERING)
                    {
                        message += "Triggering content \n";
                    }
                    if (spoilers.Count > 0)
                    {
                        message += "Spoilers from the expansions \n";
                    }
                    for (int i = 0; i < spoilers.Count; i++)
                    {
                        message += spoilers[i] + "\n";
                    }
                    TargetProfileWindow.warning = true;
                    TargetProfileWindow.warningMessage = message;
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling RecieveProfileWarning message: {ex}");
            }
        }

        // Receives a full profile's settings and display data (avatar, background, spoiler flags, NSFW/trigger warnings, accent color, tooltip status). Loads images asynchronously and populates the profile editor or target profile view.
        public static async void ReceiveProfileSettings(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int AVATARLEN = buffer.ReadInt();
                    byte[] AVATARBYTES = buffer.ReadBytes(AVATARLEN);
                    int BACKGROUNDBYTESLEN = buffer.ReadInt();
                    byte[] BACKGROUNDBYTES = buffer.ReadBytes(BACKGROUNDBYTESLEN);
                    string NAME = buffer.ReadString();
                    float colX = buffer.ReadFloat();
                    float colY = buffer.ReadFloat();
                    float colZ = buffer.ReadFloat();
                    float colW = buffer.ReadFloat();
                    bool isPrivate = buffer.ReadBool();
                    bool isTooltip = buffer.ReadBool();
                    bool ARR = buffer.ReadBool();
                    bool HW = buffer.ReadBool();
                    bool SB = buffer.ReadBool();
                    bool SHB = buffer.ReadBool();
                    bool EW = buffer.ReadBool();
                    bool DT = buffer.ReadBool();
                    bool NSFW = buffer.ReadBool();
                    bool TRIGGERING = buffer.ReadBool();
                    bool showCompass = buffer.ReadBool();
                    bool fauxName = buffer.ReadBool();
                    bool self = buffer.ReadBool();

                    bool equipmentPublic = buffer.ReadBool();
                    // Immersive theme is a trailing field - older servers don't send it, so only read when bytes remain.
                    int immersiveTheme = buffer.Length() >= 4 ? buffer.ReadInt() : 0;
                    // 1..N = built-in, >= GalleryIdBase = gallery theme id, anything else = viewer's choice.
                    if (immersiveTheme < 0
                        || (immersiveTheme > AbsoluteRP.Immersive.ImmersiveThemes.All.Length && immersiveTheme < AbsoluteRP.Immersive.ImmersiveThemes.GalleryIdBase))
                        immersiveTheme = 0;

                    Plugin.PluginLog.Info($"[ReceiveProfileSettings] self={self}, equipmentPublic={equipmentPublic}, theme={immersiveTheme}, name={NAME}");

                    if (self)
                    {
                        ProfilesPage.hasDrawException = false; // Reset so loading debug logs work for this fetch
                        ProfilesPage.CurrentProfile.customTabs.Clear();
                        if (AVATARBYTES == null || AVATARBYTES.Length == 0)
                        {
                            AVATARBYTES = UI.baseAvatarBytes();
                        }
                        if (BACKGROUNDBYTES != null && BACKGROUNDBYTES.Length != 0)
                        {
                            ProfilesPage.CurrentProfile.backgroundBytes = BACKGROUNDBYTES;
                        }
                        // Load textures concurrently with error handling. Skip decoding the background as an image when it's actually a video container - the video pipeline in. ProfilesPage.SyncBackgroundVideo will take over.
                        try
                        {
                            var bgIsVideo = BACKGROUNDBYTES != null && BACKGROUNDBYTES.Length > 12
                                            && ProfilesPage.LooksLikeVideoBytes(BACKGROUNDBYTES);
                            var bgTask = (!bgIsVideo && BACKGROUNDBYTES != null && BACKGROUNDBYTES.Length > 0)
                                ? Plugin.TextureProvider.CreateFromImageAsync(BACKGROUNDBYTES)
                                : Task.FromResult<IDalamudTextureWrap>(null);
                            var avatarTask = (AVATARBYTES != null && AVATARBYTES.Length > 0)
                                ? Plugin.TextureProvider.CreateFromImageAsync(AVATARBYTES)
                                : Task.FromResult<IDalamudTextureWrap>(null);
                            await Task.WhenAll(bgTask, avatarTask);
                            // Null out the still image when a video will take over, so the two don't fight for the backdrop.
                            ProfilesPage.backgroundImage = bgIsVideo ? null : bgTask.Result;
                            ProfilesPage.currentAvatarImg = avatarTask.Result;
                        }
                        catch (Exception texEx)
                        {
                            Plugin.PluginLog.Debug($"Failed to load profile textures: {texEx.Message}");
                        }
                        ProfilesPage.CurrentProfile.isPrivate = isPrivate;
                        ProfilesPage.CurrentProfile.isActive = isTooltip;
                        ProfilesPage.CurrentProfile.avatarBytes = AVATARBYTES;
                        ProfilesPage.CurrentProfile.title = NAME;
                        ProfilesPage.CurrentProfile.titleColor = new Vector4(colX, colY, colZ, colW);
                        ProfilesPage.CurrentProfile.SpoilerARR = ARR;
                        ProfilesPage.CurrentProfile.SpoilerHW = HW;
                        ProfilesPage.CurrentProfile.SpoilerSB = SB;
                        ProfilesPage.CurrentProfile.SpoilerSHB = SHB;
                        ProfilesPage.CurrentProfile.SpoilerEW = EW;
                        ProfilesPage.CurrentProfile.SpoilerDT = DT;
                        ProfilesPage.CurrentProfile.NSFW = NSFW;
                        ProfilesPage.CurrentProfile.TRIGGERING = TRIGGERING;
                        ProfilesPage.CurrentProfile.equipmentPublic = equipmentPublic;
                        ProfilesPage.CurrentProfile.immersiveTheme = immersiveTheme;
                        if (ProfileSaveTracker.IsSaving)
                        {
                            ProfileSaveTracker.Finish(() =>
                            {
                                ProfilesPage.Sending = false;
                                ProfilesPage.Fetching = false;
                            });
                        }
                        else
                        {
                            // Don't clear Fetching here - the draw loop handles it after confirming all tab data has arrived. This ensures the loading overlay actually renders.
                            ProfilesPage.Sending = false;
                        }
                        ProfilesPage.showOnCompass = showCompass;
                    }
                    else
                    {
                        TargetProfileWindow.ExistingProfile = true;
                        TargetProfileWindow.addNotes = false;
                        // Don't clear RequestingProfile here - the draw loop clears it after confirming all target tab data has arrived.
                        List<string> spoilers = new List<string>();

                        if (ARR) { spoilers.Add("A Realm Reborn"); }
                        if (HW) { spoilers.Add("Heavensward"); }
                        if (SB) { spoilers.Add("Stormblood"); }
                        if (SHB) { spoilers.Add("Shadowbringers"); }
                        if (EW) { spoilers.Add("Endwalker"); }
                        if (DT) { spoilers.Add("Dawntrail"); }
                        string message = "The tooltipData you are about to view contains:\n";
                        if (NSFW)
                        {
                            message += "NSFW (18+) content \n";
                        }
                        if (TRIGGERING)
                        {
                            message += "Triggering content \n";
                        }
                        if (spoilers.Count > 0)
                        {
                            message += "Spoilers from the expansions \n";
                        }
                        for (int i = 0; i < spoilers.Count; i++)
                        {
                            message += spoilers[i] + "\n";
                        }
                        // The server sends settings only after the viewer agreed to the content warning (or when there is none), so no prompt here.
                        TargetProfileWindow.warningMessage = message;
                        if (AVATARBYTES == null || AVATARBYTES.Length == 0)
                        {
                            AVATARBYTES = UI.baseAvatarBytes();
                        }
                        EnsureTargetProfileData();

                        IDalamudTextureWrap avatar = await Plugin.TextureProvider.CreateFromImageAsync(AVATARBYTES);
                        if (avatar == null || avatar.Handle == IntPtr.Zero)
                        {
                            avatar = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                        }
                        TargetProfileWindow.profileData.avatar = avatar;
                        TargetProfileWindow.profileData.title = NAME.Replace("''", "'");
                        TargetProfileWindow.profileData.titleColor = new Vector4(colX, colY, colZ, colW);
                        TargetProfileWindow.profileData.equipmentPublic = equipmentPublic;
                        TargetProfileWindow.profileData.immersiveTheme = immersiveTheme;

                        // Persist the raw bytes so the video pipeline (if this bg is an mp4/webm) can spin up a session.
                        TargetProfileWindow.profileData.backgroundBytes = BACKGROUNDBYTES ?? Array.Empty<byte>();

                        var targetBgIsVideo = BACKGROUNDBYTES != null && BACKGROUNDBYTES.Length > 12
                                              && ProfilesPage.LooksLikeVideoBytes(BACKGROUNDBYTES);
                        if (targetBgIsVideo)
                        {
                            // Video will be drawn by the target's video session;
                            // fall back the still image to the placeholder.
                            TargetProfileWindow.profileData.background = UI.UICommonImage(UI.CommonImageTypes.backgroundHolder);
                        }
                        else
                        {
                            IDalamudTextureWrap backgroundImage = (BACKGROUNDBYTES != null && BACKGROUNDBYTES.Length > 0)
                                ? await Plugin.TextureProvider.CreateFromImageAsync(BACKGROUNDBYTES)
                                : null;
                            if (backgroundImage == null || backgroundImage.Handle == IntPtr.Zero)
                            {
                                TargetProfileWindow.profileData.background = UI.UICommonImage(UI.CommonImageTypes.backgroundHolder);
                            }
                            else
                            {
                                TargetProfileWindow.profileData.background = backgroundImage;
                            }
                        }
                        TargetProfileWindow.profileData.isPrivate = isPrivate;
                        TargetProfileWindow.profileData.isActive = isTooltip;
                    }

                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveProfileSettings message: {ex}");
            }
        }

        internal static void ReceiveTargetTooltip(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    string title = buffer.ReadString();
                    float colX = buffer.ReadFloat();
                    float colY = buffer.ReadFloat();
                    float colZ = buffer.ReadFloat();
                    float colW = buffer.ReadFloat();
                    int avatarLen = buffer.ReadInt();
                    byte[] avatarBytes = buffer.ReadBytes(avatarLen);
                    string Name = buffer.ReadString();
                    string Race = buffer.ReadString();
                    string Gender = buffer.ReadString();
                    string Age = buffer.ReadString();
                    string Height = buffer.ReadString();
                    string Weight = buffer.ReadString();
                    int Alignment = buffer.ReadInt();
                    int Personality_1 = buffer.ReadInt();
                    int Personality_2 = buffer.ReadInt();
                    int Personality_3 = buffer.ReadInt();

                    int customFieldsCount = buffer.ReadInt();
                    int customDescriptorsCount = buffer.ReadInt();
                    int customPersonalitiesCount = buffer.ReadInt();
                    TooltipData profile = new TooltipData();
                    profile.fields.Clear();
                    profile.descriptors.Clear();
                    profile.personalities.Clear();
                    for (int i = 0; i < customFieldsCount; i++)
                    {
                        string customName = buffer.ReadString();
                        string customDescription = buffer.ReadString();
                        profile.fields.Add(
                           new field()
                           {
                               index = i,
                               name = customName,
                               description = customDescription
                           });
                    }
                    for (int i = 0; i < customDescriptorsCount; i++)
                    {
                        string customName = buffer.ReadString();
                        string customDescription = buffer.ReadString();
                        profile.descriptors.Add(new descriptor() { index = i, name = customName, description = customDescription });
                    }
                    for (int i = 0; i < customPersonalitiesCount; i++)
                    {
                        string customName = buffer.ReadString();
                        string customDescription = buffer.ReadString();
                        int customIconID = buffer.ReadInt();
                        IDalamudTextureWrap placeholderIcon = UI.UICommonImage(UI.CommonImageTypes.blank);
                        var newTrait = new trait() { index = i, name = customName, description = customDescription, iconID = customIconID, icon = new IconElement { icon = placeholderIcon } };
                        profile.personalities.Add(newTrait);

                        int capturedIconID = customIconID;
                        var capturedTrait = newTrait;
                        Task.Run(async () =>
                        {
                            try
                            {
                                var loadedIcon = await WindowOperations.RenderStatusIconAsync(Plugin.plugin, capturedIconID);
                                if (loadedIcon != null)
                                    capturedTrait.icon = new IconElement { icon = loadedIcon };
                            }
                            catch { }
                        });
                    }
                    // Trailing (newer servers): the owner's immersive theme id.
                    profile.immersiveTheme = buffer.Length() >= 4 ? buffer.ReadInt() : 0;
                    profile.avatar = Plugin.TextureProvider.CreateFromImageAsync(avatarBytes).Result;
                    profile.title = title;
                    profile.titleColor = new Vector4(colX, colY, colZ, colW);

                    profile.Name = Name.Replace("''", "'");
                    profile.Race = Race.Replace("''", "'");
                    profile.Gender = Gender.Replace("''", "'");
                    profile.Age = Age.Replace("''", "'");
                    profile.Height = Height.Replace("''", "'");
                    profile.Weight = Weight.Replace("''", "'");



                    if (Alignment != 9)
                    {
                        ARPTooltipWindow.hasAlignment = true;
                    }

                    else
                    {
                        ARPTooltipWindow.hasAlignment = false;
                    }
                    if (Personality_1 == 26) { ARPTooltipWindow.showPersonality1 = false; } else { ARPTooltipWindow.showPersonality1 = true; }
                    if (Personality_2 == 26) { ARPTooltipWindow.showPersonality2 = false; } else { ARPTooltipWindow.showPersonality2 = true; }
                    if (Personality_3 == 26) { ARPTooltipWindow.showPersonality3 = false; } else { ARPTooltipWindow.showPersonality3 = true; }
                    if (Personality_1 == 26 && Personality_2 == 26 && Personality_3 == 26)
                    {
                        ARPTooltipWindow.showPersonalities = false;
                    }
                    else
                    {
                        ARPTooltipWindow.showPersonalities = true;
                    }

                    profile.alignmentImg = UI.AlignmentIcon(Alignment);
                    profile.Alignment = Alignment;
                    profile.personality_1Img = UI.PersonalityIcon(Personality_1);
                    profile.personality_2Img = UI.PersonalityIcon(Personality_2);
                    profile.personality_3Img = UI.PersonalityIcon(Personality_3);
                    profile.Personality_1 = Personality_1;
                    profile.Personality_2 = Personality_2;
                    profile.Personality_3 = Personality_3;
                    ARPTooltipWindow.tooltipData = profile;





                    Plugin.tooltipLoaded = true;
                    Plugin.plugin.OpenARPTooltip();

                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveTooltip message: {ex}");
            }
        }

        internal static void ReceiveDynamicTab(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int profileID = buffer.ReadInt();
                    int layoutID = buffer.ReadInt();
                    string tabName = buffer.ReadString();
                    int tabIndex = buffer.ReadInt();
                    int nodeCount = buffer.ReadInt();

                    DynamicLayout dynamicLayout = new DynamicLayout
                    {
                        id = layoutID,
                        tabIndex = tabIndex,
                        tabName = tabName,
                        elements = new List<LayoutElement>() // Ensure it's initialized
                    };
                    // 1. Build all nodes and store by ID
                    var nodeLookup = new Dictionary<int, LayoutTreeNode>();
                    var parentIdLookup = new Dictionary<int, int>(); // nodeID -> parentID

                    for (int i = 0; i < nodeCount; i++)
                    {
                        int ID = buffer.ReadInt();
                        int ParentID = buffer.ReadInt();
                        string Name = buffer.ReadString();
                        bool IsFolder = buffer.ReadBool();
                        LayoutTreeNode node = new LayoutTreeNode(Name, IsFolder, ID, null)
                        {
                            ID = ID,
                            ParentID = ParentID,
                            Name = Name,
                            IsFolder = IsFolder,
                            Children = new List<LayoutTreeNode>(),
                        };
                        int elementType = buffer.ReadInt();

                        switch (elementType)
                        {
                            case (int)LayoutElementTypes.Empty:
                                EmptyElement emptyElement = new EmptyElement() { id = ID, name = Name };
                                node.relatedElement = emptyElement;
                                emptyElement.relatedNode = node;
                                dynamicLayout.elements.Add(emptyElement);
                                break;
                            case (int)LayoutElementTypes.Folder:
                                FolderElement folderElement = new FolderElement()
                                {
                                    name = Name
                                };
                                node.relatedElement = folderElement;
                                folderElement.relatedNode = node;
                                dynamicLayout.elements.Add(folderElement);
                                break;
                            case (int)LayoutElementTypes.Text:
                                TextElement textElement = new TextElement()
                                {
                                    id = buffer.ReadInt(),
                                    text = buffer.ReadString(),
                                    subType = buffer.ReadInt(),
                                    PosX = buffer.ReadFloat(),
                                    PosY = buffer.ReadFloat(),
                                    width = buffer.ReadFloat(),
                                    height = buffer.ReadFloat(),
                                };
                                node.relatedElement = textElement;
                                textElement.relatedNode = node;
                                dynamicLayout.elements.Add(textElement);
                                break;
                            case (int)LayoutElementTypes.Image:
                                ImageElement imageElement = new ImageElement()
                                {
                                    id = buffer.ReadInt(),
                                    url = buffer.ReadString(),
                                    hasTooltip = buffer.ReadBool(),
                                    tooltip = buffer.ReadString(),
                                    PosX = buffer.ReadFloat(),
                                    PosY = buffer.ReadFloat(),
                                    width = buffer.ReadFloat(),
                                    height = buffer.ReadFloat(),
                                    maximizable = buffer.ReadBool(),
                                };
                                // Load image in background to avoid blocking packet processing
                                var capturedImgEl = imageElement;
                                _ = Task.Run(async () => { try { capturedImgEl.textureWrap = await Imaging.DownloadElementImage(true, capturedImgEl.url, capturedImgEl); } catch { } });
                                node.relatedElement = imageElement;
                                imageElement.relatedNode = node;
                                dynamicLayout.elements.Add(imageElement);
                                break;
                            case (int)LayoutElementTypes.Icon:
                                IconElement iconElement = new IconElement()
                                {
                                    id = buffer.ReadInt(),
                                    iconID = buffer.ReadInt(),
                                    PosX = buffer.ReadFloat(),
                                    PosY = buffer.ReadFloat(),
                                };
                                node.relatedElement = iconElement;
                                iconElement.relatedNode = node;
                                dynamicLayout.elements.Add(iconElement);
                                break;
                            default:
                                throw new InvalidOperationException($"Unknown element type: {elementType}");
                        }
                        nodeLookup[ID] = node;
                    }
                    // 2. Build parent-child relationships
                    foreach (var node in nodeLookup.Values)
                    {
                        if (node.ParentID != -1 && nodeLookup.TryGetValue(node.ParentID, out var parentNode))
                        {
                            parentNode.AddChild(node);
                        }
                        else if (node.ParentID == -1)
                        {
                            // Root node, no parent
                            dynamicLayout.RootNode.AddChild(node);
                        }
                        else
                        {
                            // Invalid parent ID, log Debug
                            Plugin.PluginLog.Debug($"Node {node.ID} has invalid parent ID {node.ParentID}");
                        }
                    }


                    CustomTab tab = new CustomTab()
                    {
                        Name = tabName,
                        Layout = dynamicLayout,
                        IsOpen = true,
                        type = (int)UI.TabType.Dynamic
                    };

                    ProfilesPage.CurrentProfile.customTabs.Add(tab);
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveDynamicTab message: {ex}");
            }
        }

        internal static void ReceiveTabCount(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int tabCount = buffer.ReadInt();
                    bool self = buffer.ReadBool();

                    if (self)
                    {
                        tabsCount = tabCount;
                        tabCountReceived = true;
                        ProfilesPage.CurrentProfile.customTabs.Clear();
                    }
                    else
                    {
                        tabsTargetCount = tabCount;
                        targetTabCountReceived = true;
                        TargetProfileWindow.profileData.customTabs.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveConnectionsRequest message: {ex}");
            }
        }

        internal static void ReceiveTabsUpdate(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int tabCount = buffer.ReadInt();
                    Plugin.PluginLog.Debug($"ReceiveTabsUpdate: server reports tabCount={tabCount}");
                    for (int i = 0; i < tabCount; i++)
                    {
                        int profileID = buffer.ReadInt();
                        string tabName = buffer.ReadString();
                        int tabIndex = buffer.ReadInt();
                        int tabType = buffer.ReadInt();
                        Plugin.PluginLog.Debug($"ReceiveTabsUpdate: profileID={profileID} tabIndex={tabIndex} tabName='{tabName}' tabType={tabType}");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveConnectionsRequest message: {ex}");
            }
        }


        /* internal static void ReceiveTradeRequest(byte[] data)
         {
             try
             {
                 using (var buffer = new ByteBuffer())
                 {
                     buffer.WriteBytes(data);
                     var packetID = buffer.ReadInt();
                     int profileID = buffer.ReadInt();
                     string requesterProfileName = buffer.ReadString();
                     string receiverProfileName = buffer.ReadString();
                     string requesterCharacterName = buffer.ReadString();
                     string requesterCharacterWorld = buffer.ReadString();
                     int inventoryTabCount = buffer.ReadInt();
                     Dictionary<int, ItemDefinition> inventory = new Dictionary<int, ItemDefinition>();
                     for (int i = 0; i < inventoryCount; i++)
                     {
                         string itemName = buffer.ReadString();
                         string itemDescription = buffer.ReadString();
                         int itemType = buffer.ReadInt();
                         int itemSubType = buffer.ReadInt();
                         int iconID = buffer.ReadInt(); // Ensure iconID is valid
                         int slotID = buffer.ReadInt();
                         int quality = buffer.ReadInt();
                         ItemDefinition itemDefinition = new ItemDefinition
                         {
                             name = itemName,
                             description = itemDescription,
                             type = itemType,
                             subtype = itemSubType,
                             iconID = iconID, // Ensure iconID is valid
                             slot = slotID,
                             quality = quality
                         };
                         Plugin.PluginLog.Debug(itemDefinition.name);
                         inventory.Add(slotID, itemDefinition);
                         // Validate and ensure compatibility
                     }
                     InventoryLayout inventoryLayout = new InventoryLayout
                     {
                         id = inventoryID,
                         inventorySlotContents = inventory
                     };
                     TradeWindow.inventoryLayout = inventoryLayout;
                     TradeWindow.slotContents = inventory; // <-- Add this line
                     TradeWindow.targetProfile = profileID;
                     Plugin.PluginLog.Debug(requesterProfileName + " is requesting a trade with you.");

                 }
             }
             catch (Exception ex)
             {
                 Plugin.PluginLog.Debug($"Debug handling ReceiveConnectionsRequest message: {ex}");
             }
             finally
             {
                 Plugin.OpenTradeWindow();
             }
         }
        */

        // Receives an inventory tab's items for a profile (self or target). Each item has a name, description, icon, quantity, and rarity.
        public static void ReceiveInventoryTab(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int inventoryID = buffer.ReadInt();
                    int profileID = buffer.ReadInt();
                    string tabName = buffer.ReadString();
                    int tabIndex = buffer.ReadInt();
                    bool self = buffer.ReadBool();
                    int inventoryCount = buffer.ReadInt();

                    Dictionary<int, ItemDefinition> inventory = new Dictionary<int, ItemDefinition>();
                    for (int i = 0; i < inventoryCount; i++)
                    {
                        string itemName = buffer.ReadString();
                        string itemDescription = buffer.ReadString();
                        int itemType = buffer.ReadInt();
                        int itemSubType = buffer.ReadInt();
                        int iconID = buffer.ReadInt();
                        int slotID = buffer.ReadInt();
                        int quality = buffer.ReadInt();
                        bool itemLocked = buffer.ReadBool();
                        ItemDefinition itemDefinition = new ItemDefinition
                        {
                            name = itemName,
                            description = itemDescription,
                            type = itemType,
                            subtype = itemSubType,
                            iconID = iconID,
                            slot = slotID,
                            quality = quality,
                            locked = itemLocked
                        };
                        inventory.Add(slotID, itemDefinition);
                    }
                    InventoryLayout inventoryLayout = new InventoryLayout
                    {
                        id = inventoryID,
                        tabIndex = tabIndex,
                        name = tabName,
                        tabName = tabName,
                        inventorySlotContents = inventory
                    };
                    CustomTab tab = new CustomTab()
                    {
                        Name = tabName,
                        Layout = inventoryLayout,
                        IsOpen = true,
                        type = (int)UI.TabType.Inventory
                    };

                    if (self)
                    {
                        // Route inventory tabs to the standalone InventoryWindow
                        AbsoluteRP.Windows.Inventory.InventoryWindow.inventoryTabs.Add(tab);
                        AbsoluteRP.Windows.Inventory.InventoryWindow.OnTabsLoaded();
                        loadedTabsCount += 1;
                    }
                    else
                    {
                        EnsureTargetProfileData();
                        TargetProfileWindow.AddTabSorted(tab);
                        loadedTargetTabsCount += 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error handling ReceiveInventoryTab: {ex}");
                loadedTabsCount += 1; // Ensure counter progresses even on failure
            }
        }

        public static void ReceiveInfoTab(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int profileID = buffer.ReadInt();
                    string tabName = buffer.ReadString();
                    int tabIndex = buffer.ReadInt();
                    string info = buffer.ReadString();
                    bool self = buffer.ReadBool();

                    InfoLayout infoLayout = new InfoLayout
                    {
                        name = tabName,
                        tabIndex = tabIndex,
                        text = info.Replace("''", "'")
                    };
                    CustomTab tab = new CustomTab
                    {
                        Name = tabName,
                        Layout = infoLayout,
                        IsOpen = true,
                        type = (int)UI.TabType.Info
                    };
                    if (self)
                    {
                        if (ProfilesPage.CurrentProfile.customTabs == null)
                            ProfilesPage.CurrentProfile.customTabs = new List<CustomTab>();
                        ProfilesPage.CurrentProfile.customTabs.Add(tab);
                        loadedTabsCount += 1;
                    }
                    else
                    {
                        EnsureTargetProfileData();
                        TargetProfileWindow.AddTabSorted(tab);
                        loadedTargetTabsCount += 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error handling ReceiveInfoTab: {ex}");
                loadedTabsCount += 1;
            }
        }

        public static void ReceiveStoryTab(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int profileID = buffer.ReadInt();
                    string tabName = buffer.ReadString();
                    int tabIndex = buffer.ReadInt();
                    string storyTitle = buffer.ReadString();
                    bool self = buffer.ReadBool();
                    Plugin.PluginLog.Debug($"Story Title: {storyTitle}");
                    int chapterCount = buffer.ReadInt();
                    List<StoryChapter> chapters = new List<StoryChapter>();
                    for (int i = 0; i < chapterCount; i++)
                    {
                        int chapterIndex = buffer.ReadInt();
                        string chapterName = buffer.ReadString().Replace("''", "'");
                        string chapterContent = buffer.ReadString().Replace("''", "'");
                        chapters.Add(new StoryChapter()
                        {
                            id = chapterIndex,
                            title = chapterName,
                            content = chapterContent
                        });
                    }
                    StoryLayout storyLayout = new StoryLayout
                    {
                        tabIndex = tabIndex,
                        name = storyTitle.Replace("''", "'"),
                        chapters = chapters
                    };
                    CustomTab tab = new CustomTab
                    {
                        Name = tabName,
                        Layout = storyLayout,
                        IsOpen = true,
                        type = (int)UI.TabType.Story
                    };
                    if (self)
                    {
                        if (ProfilesPage.CurrentProfile.customTabs == null)
                            ProfilesPage.CurrentProfile.customTabs = new List<CustomTab>();
                        ProfilesPage.CurrentProfile.customTabs.Add(tab);
                        loadedTabsCount += 1;
                    }
                    else
                    {
                        EnsureTargetProfileData();
                        TargetProfileWindow.AddTabSorted(tab);
                        loadedTargetTabsCount += 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error handling ReceiveStoryTab: {ex}");
                loadedTabsCount += 1;
            }
        }

        public static void ReceiveDetailsTab(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int profileID = buffer.ReadInt();
                    string tabName = buffer.ReadString();
                    int tabIndex = buffer.ReadInt();
                    bool self = buffer.ReadBool();
                    int detailsCount = buffer.ReadInt();
                    List<Detail> details = new List<Detail>();

                    for (int i = 0; i < detailsCount; i++)
                    {
                        int id = buffer.ReadInt();
                        string name = buffer.ReadString().Replace("''", "'");
                        string content = buffer.ReadString().Replace("''", "'");
                        details.Add(new Detail()
                        {
                            id = id,
                            name = name,
                            content = content
                        });
                        Plugin.PluginLog.Debug($"{name}  {content} {id}");
                    }

                    DetailsLayout detailsLayout = new DetailsLayout
                    {
                        name = tabName.Replace("''", "'"),
                        tabIndex = tabIndex,
                        details = details
                    };

                    BioLoadStatus = 1;
                    CustomTab tab = new CustomTab
                    {
                        Name = tabName,
                        Layout = detailsLayout,
                        IsOpen = true,
                        type = (int)UI.TabType.Details
                    };
                    if (self)
                    {
                        if (ProfilesPage.CurrentProfile.customTabs == null)
                            ProfilesPage.CurrentProfile.customTabs = new List<CustomTab>();
                        ProfilesPage.CurrentProfile.customTabs.Add(tab);
                        loadedTabsCount += 1;
                    }
                    else
                    {
                        EnsureTargetProfileData();
                        TargetProfileWindow.AddTabSorted(tab);
                        loadedTargetTabsCount += 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error handling ReceiveDetailsTab: {ex}");
                loadedTabsCount += 1;
            }
        }

        public static void ReceiveProfileGalleryTab(byte[] data)
        {
            bool self = false;
            try
            {
                // Read all data from buffer SYNCHRONOUSLY first
                int profileID;
                string tabName;
                int tabIndex;
                int galleryImageCount;
                var imageEntries = new List<(string url, string tooltip, bool nsfw, bool trigger, int index)>();

                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packetID
                    profileID = buffer.ReadInt();
                    tabName = buffer.ReadString();
                    tabIndex = buffer.ReadInt();
                    self = buffer.ReadBool();
                    galleryImageCount = buffer.ReadInt();

                    for (int i = 0; i < galleryImageCount; i++)
                    {
                        string url = buffer.ReadString();
                        string tooltip = buffer.ReadString();
                        bool nsfw = buffer.ReadBool();
                        bool trigger = buffer.ReadBool();
                        imageEntries.Add((url, tooltip, nsfw, trigger, i));
                    }
                }

                // Create the gallery layout with an empty image list immediately
                var gallery = new List<ProfileGalleryImage>();
                GalleryLayout galleryLayout = new GalleryLayout
                {
                    name = tabName.Replace("''", "'"),
                    tabIndex = tabIndex,
                    images = gallery
                };
                CustomTab tab = new CustomTab
                {
                    Name = tabName,
                    Layout = galleryLayout,
                    IsOpen = true,
                    type = (int)UI.TabType.Gallery
                };

                // Add the tab and increment counter IMMEDIATELY. Set gallery counters so the loading overlay can show image download progress
                if (self)
                {
                    if (ProfilesPage.CurrentProfile.customTabs == null)
                        ProfilesPage.CurrentProfile.customTabs = new List<CustomTab>();
                    ProfilesPage.CurrentProfile.customTabs.Add(tab);
                    loadedTabsCount += 1;
                    loadedGalleryImages = 0;
                    GalleryImagesToLoad = galleryImageCount;
                }
                else
                {
                    EnsureTargetProfileData();
                    if (TargetProfileWindow.profileData.customTabs == null)
                        TargetProfileWindow.profileData.customTabs = new List<CustomTab>();
                    TargetProfileWindow.AddTabSorted(tab);
                    loadedTargetTabsCount += 1;
                    loadedTargetGalleryImages = 0;
                    TargetGalleryImagesToLoad = galleryImageCount;
                }

                // Download images in background - they'll populate the gallery list as they complete
                bool isSelf = self;
                Task.Run(async () =>
                {
                    try
                    {
                        var downloadTasks = imageEntries.Select(async entry =>
                        {
                            var galleryImage = GalleryMedia.IsMedia(entry.url)
                                ? new ProfileGalleryImage { index = entry.index, url = entry.url, tooltip = entry.tooltip, nsfw = entry.nsfw, trigger = entry.trigger }
                                : await Imaging.DownloadProfileImage(true, entry.url, entry.tooltip, profileID, entry.nsfw, entry.trigger, Plugin.plugin, entry.index);
                            if (isSelf)
                            {
                                loadedGalleryImages += 1;
                                ProfilesPage.loading = $"Gallery Image {loadedGalleryImages}/{galleryImageCount}";
                            }
                            else
                            {
                                loadedTargetGalleryImages += 1;
                                TargetProfileWindow.loading = $"Gallery Image {loadedTargetGalleryImages}/{galleryImageCount}";
                            }
                            return galleryImage;
                        }).ToArray();

                        var downloadedImages = await Task.WhenAll(downloadTasks);

                        foreach (var galleryImage in downloadedImages)
                        {
                            if (galleryImage.thumbnail == null || galleryImage.thumbnail.Handle == IntPtr.Zero)
                                galleryImage.image = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                            if (galleryImage.image == null || galleryImage.image.Handle == IntPtr.Zero)
                                galleryImage.image = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                            gallery.Add(galleryImage);
                        }
                    }
                    catch (Exception imgEx)
                    {
                        Plugin.PluginLog.Debug($"Error downloading gallery images: {imgEx}");
                    }
                    finally
                    {
                        if (isSelf) { loadedGalleryImages = 0; GalleryImagesToLoad = 0; }
                        else { loadedTargetGalleryImages = 0; TargetGalleryImagesToLoad = 0; }
                    }
                });
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error handling ReceiveProfileGalleryTab: {ex}");
                if (self) loadedTabsCount += 1;
                else loadedTargetTabsCount += 1;
            }
        }

        public static void ReceiveTreeLayout(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    int packetID = buffer.ReadInt();

                    int profileIndex = buffer.ReadInt();
                    int tabID = buffer.ReadInt();
                    string tabName = buffer.ReadString();
                    int tabIndex = buffer.ReadInt();
                    bool self = buffer.ReadBool();

                    // Read Paths
                    int pathCount = buffer.ReadInt();
                    var paths = new List<List<(int x, int y)>>();
                    for (int i = 0; i < pathCount; i++)
                    {
                        int slotCount = buffer.ReadInt();
                        var path = new List<(int x, int y)>();
                        for (int j = 0; j < slotCount; j++)
                        {
                            int x = buffer.ReadInt();
                            int y = buffer.ReadInt();
                            path.Add((x, y));
                        }
                        paths.Add(path);
                    }

                    // Read PathConnections
                    int pathConnCount = buffer.ReadInt();
                    var pathConnections = new List<List<((int x, int y) from, (int x, int y) to)>>();
                    for (int i = 0; i < pathConnCount; i++)
                    {
                        int connCount = buffer.ReadInt();
                        var conns = new List<((int x, int y) from, (int x, int y) to)>();
                        for (int j = 0; j < connCount; j++)
                        {
                            int fromX = buffer.ReadInt();
                            int fromY = buffer.ReadInt();
                            int toX = buffer.ReadInt();
                            int toY = buffer.ReadInt();
                            conns.Add(((fromX, fromY), (toX, toY)));
                            Plugin.PluginLog.Debug($"Path Connection: From ({fromX}, {fromY}) To ({toX}, {toY})");
                        }
                        pathConnections.Add(conns);
                    }

                    // Read Relationships
                    int relCount = buffer.ReadInt();
                    var relationships = new List<Relationship>();
                    for (int i = 0; i < relCount; i++)
                    {
                        var rel = new Relationship();
                        rel.Name = buffer.ReadString();
                        rel.Description = buffer.ReadString();
                        rel.IconID = buffer.ReadInt();
                        rel.active = buffer.ReadBool();
                        // Load icon in background to avoid blocking packet processing
                        var capturedRel = rel;
                        int capturedIconID = rel.IconID;
                        Task.Run(async () =>
                        {
                            try
                            {
                                capturedRel.IconTexture = await WindowOperations.RenderIconAsync(Plugin.plugin, capturedIconID);
                            }
                            catch { }
                        });
                        bool hasSlot = buffer.ReadBool();
                        if (hasSlot)
                        {
                            int slotX = buffer.ReadInt();
                            int slotY = buffer.ReadInt();
                            rel.Slot = (slotX, slotY);
                        }
                        else
                        {
                            rel.Slot = null;
                        }

                        int linkCount = buffer.ReadInt();
                        rel.Links = new List<RelationshipLink>();
                        for (int l = 0; l < linkCount; l++)
                        {
                            var link = new RelationshipLink();
                            link.From = (buffer.ReadInt(), buffer.ReadInt());
                            link.To = (buffer.ReadInt(), buffer.ReadInt());
                            rel.Links.Add(link);
                        }

                        rel.NodeKind = buffer.ReadInt();
                        rel.BondID = buffer.ReadInt();
                        rel.BondPeerAccountID = buffer.ReadInt();
                        rel.BondPeerProfileIndex = buffer.ReadInt();
                        rel.BondPeerName = buffer.ReadString();
                        rel.BondPeerWorld = buffer.ReadString();
                        rel.BondTitle = buffer.ReadString();
                        rel.BondRelation = buffer.ReadString();

                        relationships.Add(rel);
                    }

                    // Defensive: Ensure all collections are initialized
                    if (paths == null) paths = new List<List<(int x, int y)>>();
                    if (pathConnections == null) pathConnections = new List<List<((int x, int y) from, (int x, int y) to)>>();
                    if (relationships == null) relationships = new List<Relationship>();

                    var treeLayout = new TreeLayout
                    {
                        name = tabName,
                        tabName = tabName,
                        tabIndex = tabIndex,
                        Paths = paths,
                        PathConnections = pathConnections,
                        relationships = relationships
                    };

                    // Defensive: Ensure TreeLayout collections are not null
                    if (treeLayout.Paths == null) treeLayout.Paths = new List<List<(int x, int y)>>();
                    if (treeLayout.PathConnections == null) treeLayout.PathConnections = new List<List<((int x, int y) from, (int x, int y) to)>>();
                    if (treeLayout.relationships == null) treeLayout.relationships = new List<Relationship>();

                    foreach (var path in treeLayout.PathConnections)
                    {
                        if (path == null) continue;
                        foreach (var conn in path)
                        {
                            Plugin.PluginLog.Debug($"Path Connection: From ({conn.from.x}, {conn.from.y}) To ({conn.to.x}, {conn.to.y})");
                        }
                    }

                    var customTab = new CustomTab
                    {
                        Name = tabName,
                        Layout = treeLayout,
                        IsOpen = true,
                        type = (int)UI.TabType.Tree
                    };

                    if (self)
                    {
                        if (ProfilesPage.CurrentProfile?.customTabs == null)
                            ProfilesPage.CurrentProfile.customTabs = new List<CustomTab>();
                        ProfilesPage.CurrentProfile.customTabs.Add(customTab);
                        loadedTabsCount += 1;
                    }
                    else
                    {
                        EnsureTargetProfileData();
                        TargetProfileWindow.AddTabSorted(customTab);
                        loadedTargetTabsCount += 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error handling ReceiveTreeLayout: {ex}");
                loadedTabsCount += 1;
            }
        }

        // Receives a Bio tab's full data: character details (race, gender, age, etc.), alignment, personality traits, custom fields, and descriptors. Routes to either the self-profile or target-profile window.
        public static void RecieveBioTab(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int profileID = buffer.ReadInt();
                    string tabName = buffer.ReadString();
                    int tabIndex = buffer.ReadInt();
                    string name = buffer.ReadString();
                    string race = buffer.ReadString();
                    string gender = buffer.ReadString();
                    string age = buffer.ReadString();
                    string height = buffer.ReadString();
                    string weight = buffer.ReadString();
                    string atFirstGlance = buffer.ReadString();
                    int alignment = buffer.ReadInt();
                    int personality_1 = buffer.ReadInt();
                    int personality_2 = buffer.ReadInt();
                    int personality_3 = buffer.ReadInt();
                    bool self = buffer.ReadBool();
                    int customFieldsCount = buffer.ReadInt();
                    int customDescriptorsCount = buffer.ReadInt();
                    int customPersonalitiesCount = buffer.ReadInt();
                    bool isTooltip = buffer.ReadBool();
                    List<descriptor> descriptors = new List<descriptor>();
                    List<trait> traits = new List<trait>();
                    List<field> fields = new List<field>();

                    for (int i = 0; i < customFieldsCount; i++)
                    {
                        string customName = buffer.ReadString();
                        string customDescription = buffer.ReadString();
                        fields.Add(
                           new field()
                           {
                               index = i,
                               name = customName,
                               description = customDescription
                           });
                    }
                    for (int i = 0; i < customDescriptorsCount; i++)
                    {
                        string customName = buffer.ReadString();
                        string customDescription = buffer.ReadString();
                        descriptors.Add(new descriptor() { index = i, name = customName, description = customDescription });
                    }
                    // Target-side trait copies are built later via .Select into `targetBioLayout.traits`. We need to update BOTH the self-side and target-side copies when the icon finishes loading, otherwise the target profile window keeps rendering the blank placeholder and the traits look "missing". Track the target twin per index so the async callback can find it.
                    List<trait> targetTraits = new List<trait>();
                    for (int i = 0; i < customPersonalitiesCount; i++)
                    {
                        string customName = buffer.ReadString();
                        string customDescription = buffer.ReadString();
                        int customIconID = buffer.ReadInt();
                        // Use a placeholder icon immediately - load the real icon in the background to avoid blocking the packet processing thread
                        IDalamudTextureWrap placeholderIcon = UI.UICommonImage(UI.CommonImageTypes.blank);
                        var newTrait = new trait() { index = i, name = customName, description = customDescription, iconID = customIconID, icon = new IconElement { icon = placeholderIcon } };
                        var newTargetTrait = new trait() { index = i, name = customName, description = customDescription, iconID = customIconID, icon = new IconElement { icon = placeholderIcon } };
                        traits.Add(newTrait);
                        targetTraits.Add(newTargetTrait);

                        // Fire-and-forget async icon load - updates the trait icon on BOTH the self and target layouts when ready.
                        int capturedIconID = customIconID;
                        var capturedTrait = newTrait;
                        var capturedTargetTrait = newTargetTrait;
                        Task.Run(async () =>
                        {
                            try
                            {
                                var loadedIcon = await WindowOperations.RenderStatusIconAsync(Plugin.plugin, capturedIconID);
                                if (loadedIcon != null)
                                {
                                    var elem = new IconElement { icon = loadedIcon };
                                    capturedTrait.icon = elem;
                                    capturedTargetTrait.icon = elem;
                                }
                            }
                            catch { /* icon load failure is non-critical */ }
                        });
                    }

                    BioLayout bioLayout = new BioLayout
                    {
                        tabIndex = tabIndex,
                        name = name.Replace("''", "'"),
                        race = race.Replace("''", "'"),
                        gender = gender.Replace("''", "'"),
                        age = age.Replace("''", "'"),
                        height = height.Replace("''", "'"),
                        weight = weight.Replace("''", "'"),
                        afg = atFirstGlance.Replace("''", "'"),
                        alignment = alignment,
                        personality_1 = personality_1,
                        personality_2 = personality_2,
                        personality_3 = personality_3,
                        isTooltip = isTooltip,
                        descriptors = descriptors,
                        traits = traits,
                        fields = fields,
                    };
                    BioLayout targetBioLayout = new BioLayout
                    {
                        tabIndex = tabIndex,
                        name = name.Replace("''", "'"),
                        race = race.Replace("''", "'"),
                        gender = gender.Replace("''", "'"),
                        age = age.Replace("''", "'"),
                        height = height.Replace("''", "'"),
                        weight = weight.Replace("''", "'"),
                        afg = atFirstGlance.Replace("''", "'"),
                        alignment = alignment,
                        personality_1 = personality_1,
                        personality_2 = personality_2,
                        personality_3 = personality_3,
                        isTooltip = isTooltip,
                        descriptors = descriptors.Select(d => new descriptor { index = d.index, name = d.name, description = d.description }).ToList(),
                        // Use the pre-built target twins so async icon loads reach the target-side trait objects. A fresh .Select snapshot would freeze the placeholder icon here and never receive the loaded texture.
                        traits = targetTraits,
                        fields = fields.Select(f => new field { index = f.index, name = f.name, description = f.description }).ToList(),
                    };
                    BioLoadStatus = 1;

                    CustomTab tab = new CustomTab
                    {
                        Name = tabName,
                        Layout = bioLayout,
                        IsOpen = true,
                        type = (int)UI.TabType.Bio
                    };

                    CustomTab targetTab = new CustomTab
                    {
                        Name = tabName,
                        Layout = targetBioLayout,
                        IsOpen = true,
                        type = (int)UI.TabType.Bio
                    };


                    if (self)
                    {

                        if (ProfilesPage.CurrentProfile.customTabs == null)
                            ProfilesPage.CurrentProfile.customTabs = new List<CustomTab>();
                        ProfilesPage.CurrentProfile.customTabs.Add(tab);
                        ProfilesPage.CurrentProfile.id = profileID; // Store profile ID
                        loadedTabsCount += 1;
                    }
                    else
                    {
                        EnsureTargetProfileData();
                        TargetProfileWindow.AddTabSorted(targetTab);
                        TargetProfileWindow.profileData.id = profileID; // Store profile ID
                        TargetProfileWindow.profileData.playerName = TargetProfileWindow.characterName;
                        TargetProfileWindow.profileData.playerWorld = TargetProfileWindow.characterWorld;
                        loadedTargetTabsCount += 1;
                        if (TargetProfileWindow.profileData.customTabs.Count == 1)
                            TargetProfileWindow.currentLayout = targetTab.Layout as CustomLayout;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error handling RecieveBioTab: {ex}");
                loadedTabsCount += 1;
            }
        }

        public static event Action<string, List<ARPProfileSummary>>? OnProfilesByTagReceived;

        public static void HandleProfilesByAccountTag(byte[] data)
        {
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packetID
                string tagName = buffer.ReadString();
                int count = buffer.ReadInt();
                var list = new List<ARPProfileSummary>(count);
                for (int i = 0; i < count; i++)
                {
                    list.Add(new ARPProfileSummary
                    {
                        ProfileId    = buffer.ReadInt(),
                        ProfileIndex = buffer.ReadInt(),
                        ProfileName  = buffer.ReadString(),
                        PlayerName   = buffer.ReadString(),
                        PlayerWorld  = buffer.ReadString(),
                        ProfileType  = buffer.ReadInt(),
                        IsTooltip    = buffer.ReadInt() != 0,
                    });
                }
                Plugin.PluginLog.Info($"[ARPIpc] Received {count} profile(s) for tag '{tagName}'");
                try { OnProfilesByTagReceived?.Invoke(tagName, list); }
                catch (Exception ex) { Plugin.PluginLog.Debug($"OnProfilesByTagReceived handler threw: {ex.Message}"); }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"HandleProfilesByAccountTag Error: {ex.Message}");
            }
        }

        public sealed class ARPProfileSummary
        {
            public int    ProfileId    { get; set; }
            public int    ProfileIndex { get; set; }
            public string ProfileName  { get; set; } = string.Empty;
            public string PlayerName   { get; set; } = string.Empty;
            public string PlayerWorld  { get; set; } = string.Empty;
            public int    ProfileType  { get; set; }
            public bool   IsTooltip    { get; set; }
        }
    }
}
