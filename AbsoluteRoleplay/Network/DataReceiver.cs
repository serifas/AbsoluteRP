


using AbsoluteRP;
using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Ect;
using AbsoluteRP.Windows.Listings;
using AbsoluteRP.Windows.MainPanel;
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

namespace Networking
{
    /// <summary>
    /// This entire script simply receives data from the server and sets variables in the windows as needed
    /// Not too much to look at, simply just for receiving our info and using it as needed
    /// </summary>
    //Packets that can be received from the server (Must match server packet number on server)
    public enum ServerPackets
    {
        SWelcomeMessage = 1,
        SRecLoginStatus = 2,
        SRecAccPermissions = 3,
        SRecProfileBio = 4,
        SRecExistingProfile = 5,
        SSendProfile = 20,
        SDoneSending = 21,
        SNoProfileBio = 22,
        SNoProfile = 23,
        SSendProfileHook = 24,
        SSendNoProfileHooks = 25,
        SRecNoTargetHooks = 26,
        SRecNoTargetBio = 27,
        SRecTargetHooks = 28,
        SRecTargetBio = 29,
        SRecTargetProfile = 30,
        SRecNoTargetProfile = 31,
        SRecProfileStory = 32,
        SRecTargetStory = 33,
        SRecBookmarks = 34,
        SRecNoTargetStory = 35,
        SRecNoProfileStory = 36,
        SRecProfileGallery = 37,
        SRecGalleryImageLoaded = 38,
        SRecImageDeletionStatus = 39,
        SRecNoTargetGallery = 40,
        SRecTargetGallery = 41,
        SRecNoProfileGallery = 42,
        CProfileAlreadyReported = 43,
        CProfileReportedSuccessfully = 44,
        SSendProfileNotes = 45,
        SSendNoProfileNotes = 46,
        SSendNoAuthorization = 47,
        SSendVerificationMessage = 48,
        SSendVerified = 49,
        SSendPasswordModificationForm = 50,
        SSendOOC = 51,
        SSendTargetOOC = 52,
        SSendNoOOCInfo = 53,
        SSendNoTargetOOCInfo = 54,
        ReceiveConnections = 55,
        ReceiveNewConnectionRequest = 56,
        ReceiveChatMessage = 57,
        ReceiveGroupMemberships = 58,
        RecieveTargetTooltip = 59,
        ReceiveProfiles = 60,
        CreateItem = 61,
        ReceiveProfileWarning = 62,
        ReceiveProfileSettings = 63,
        ReceiveProfileListings = 64,
        ReceiveProfileDetails = 65,
        ReceiveReloadProfiles = 66,
        ReceiveTabCount = 67,
        ReceiveGalleryTab = 68,
        ReceiveInfoTab = 69,
        ReceiveTabsUpdate = 70,
        ReceiveInventoryTab = 71,
        ReceiveDynamicTab = 72,
        ReceiveRelationshipsTab = 73,
        ReceiveSingleTab = 74,
        ReceiveTradeRequest = 75,
        ReceiveTradeUpdate = 76,
        ReceiveTradeStatus = 77,
        ReceiveTradeInventory = 78,
        ReceiveTreeLayout = 79,
        RecConnectedPlayersInMap = 80,
        ReceiveGroup = 81,
        SendGroupChatMessages = 83,
        SendGroupChatMessageBroadcast = 84,
        SendGroupCategories = 85,
        SendGroupRosterFields = 86,
        SendMemberMetadata = 87,
        SendMemberFieldValues = 88,
        SendChatMessageDeleted = 89,
        SendChatMessageEdited = 90,
        SendGroupInvites = 91,
        SendGroupInviteResult = 92,
        SendGroupMembers = 93,
        SendForumStructure = 94,
        SendForumPermissions = 95,
        SendInviteNotification = 96,
        SendInviteeProfile = 97,
        SendGroupRanks = 98,
        SendRankOperationResult = 99,
        SendGroupMemberAvatar = 100,
        SendLikesRemaining = 101,
        SendLikeResult = 102,
        SendProfileLikeCounts = 103,
        SendProfileLikes = 104,
        SendPinnedMessages = 105,
        SendMessagePinResult = 106,
        SendMessagePinUpdate = 107,
        SendChannelLockUpdate = 108,
        // Rules Channel & Self-Assign Roles
        SendGroupRulesResponse = 109,
        SendRulesAgreementResponse = 110,
        SendGroupRules = 111,
        SendSelfAssignRoleResponse = 112,
        SendSelfAssignRoles = 113,
        SendSelfRoleAssignmentResponse = 114,
        SendRoleChannelPermissionsResponse = 115,
        SendMemberSelfRoles = 116,
        SendCreateChannelError = 117,
        SendRoleSections = 118,
        SendGroupBans = 119,
        SendMemberRemovedFromGroup = 120,
        SendGroupInfo = 121,
        SendProfileInfoEmbed = 122,
        // Form Channel
        SendFormFields = 123,
        SendFormSubmissions = 124,
        SendFormSubmitResult = 125,
        // Group Search
        SendPublicGroupSearchResults = 126,
    }
    class DataReceiver
    {
        // Add these public static properties/lists at the top of your GroupManager class
        public static List<GroupCategory> categories = new List<GroupCategory>();
        public static List<GroupRosterField> rosterFields = new List<GroupRosterField>();
        public static List<GroupInvite> invites = new List<GroupInvite>();
        public static List<GroupMember> members = new List<GroupMember>();
        public static List<GroupForumCategory> forumStructure = new List<GroupForumCategory>();
        public static List<GroupForumChannelPermission> forumPermissions = new List<GroupForumChannelPermission>();
        public static List<GroupRank> ranks = new List<GroupRank>();
        public static Dictionary<int, Dictionary<string, string>> memberMetadata = new Dictionary<int, Dictionary<string, string>>();
        public static Dictionary<int, Dictionary<int, string>> memberFieldValues = new Dictionary<int, Dictionary<int, string>>();
        public static string rankOperationMessage = string.Empty;
        public static bool rankOperationSuccess = false;
        public static string restorationStatus = "";

        // Profile Likes System
        public static int likesRemaining = 0;
        public static string likeResultMessage = string.Empty;
        public static bool likeResultSuccess = false;
        public static Dictionary<int, int> profileLikeCounts = new Dictionary<int, int>();
        public static List<ProfileLike> currentProfileLikes = new List<ProfileLike>();

        // Rules Channel & Self-Assign Roles
        public static string groupRulesContent = string.Empty;
        public static int groupRulesVersion = 0;
        public static bool hasAgreedToRules = false;
        public static bool isGroupOwner = false;
        public static bool rulesOperationSuccess = false;
        public static string rulesOperationMessage = string.Empty;
        public static List<GroupSelfAssignRole> selfAssignRoles = new List<GroupSelfAssignRole>();
        public static List<GroupRoleSection> roleSections = new List<GroupRoleSection>();
        public static bool canManageSelfAssignRoles = false;
        public static List<int> memberSelfRoleIDs = new List<int>();
        public static string selfRoleOperationMessage = string.Empty;
        public static bool selfRoleOperationSuccess = false;
        public static string createChannelError = string.Empty;

        // Form Channel
        public static Dictionary<int, List<FormField>> formFields = new Dictionary<int, List<FormField>>();
        public static Dictionary<int, List<FormSubmission>> formSubmissions = new Dictionary<int, List<FormSubmission>>();
        public static string formSubmitResultMessage = string.Empty;
        public static bool formSubmitResultSuccess = false;

        // Group Search
        public static List<GroupSearchResult> groupSearchResults = new List<GroupSearchResult>();
        public static bool groupSearchInProgress = false;

        public static bool LoadedSelf = false;
        public static int BioLoadStatus = -1, HooksLoadStatus = -1, StoryLoadStatus = -1, OOCLoadStatus = -1, GalleryLoadStatus = -1, BookmarkLoadStatus = -1,
                          TargetBioLoadStatus = -1, TargetHooksLoadStatus = -1, TargetStoryLoadStatus = -1, TargetOOCLoadStatus = -1, TargetGalleryLoadStatus = -1, TargetNotesLoadStatus = -1;

        public static RankPermissions permissions { get; set; }
        public static Vector4 accounStatusColor, verificationStatusColor, forgotStatusColor, restorationStatusColor = new Vector4(255, 255, 255, 255);
        public static Dictionary<int, string> characters = new Dictionary<int, string>();
        public static Dictionary<int, string> adminCharacters = new Dictionary<int, string>();
        public static Dictionary<int, byte[]> adminCharacterAvatars = new Dictionary<int, byte[]>();
        public static SortedList<int, string> pages = new SortedList<int, string>();
        public static SortedList<string, string> pagesContent = new SortedList<string, string>();
        public static Dictionary<int, string> profiles = new Dictionary<int, string>();
        public static Dictionary<int, byte[]> characterAvatars = new Dictionary<int, byte[]>();
        public static Dictionary<int, int> characterVerificationStatuses = new Dictionary<int, int>();
        public static bool loggedIn;
        public static bool isAdmin;
        public static int tabsCount = 0, loadedTabsCount = 0;
        public static bool silentUpdate;
        public static bool allLoaded;
        public static int loadedGalleryImages = 0;
        public static int GalleryImagesToLoad = 0;
        internal static int loadedTargetTabsCount;
        internal static int tabsTargetCount;
        internal static int loadedTargetGalleryImages;

        public static int ListingsLoadStatus { get; internal set; }
        public static int TargetGalleryImagesToLoad { get; internal set; }
        private static void EnsureTargetProfileData()
        {
            if (TargetProfileWindow.profileData == null)
                TargetProfileWindow.profileData = new ProfileData();
            if (TargetProfileWindow.profileData.customTabs == null)
                TargetProfileWindow.profileData.customTabs = new List<CustomTab>();
        }
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
                    Plugin.plugin.UpdateStatusAsync().GetAwaiter().GetResult();
                    // Handle the message as needed
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling Bookmark message: {ex}");
            }
        }
        public static void ReceiveGroupMemberships(byte[] data)
        {
            try
            {
                GroupsData.groups.Clear();
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int groupCount = buffer.ReadInt();
                    Plugin.PluginLog.Info($"[ReceiveGroupMemberships] Receiving {groupCount} groups");
                    for (int i = 0; i < groupCount; i++)
                    {
                        int id = buffer.ReadInt();
                        string name = buffer.ReadString();
                        string logoURL = buffer.ReadString();
                        string imgURL = buffer.ReadString();
                        int profileID = buffer.ReadInt();
                        bool visible = buffer.ReadBool();
                        bool openInvite = buffer.ReadBool();
                        Plugin.PluginLog.Info($"[ReceiveGroupMemberships] Group {id} '{name}' has profileID: {profileID}, visible: {visible}, openInvite: {openInvite}");
                        byte[] logoBytes = Imaging.FetchUrlImageBytes(logoURL).GetAwaiter().GetResult();
                        IDalamudTextureWrap logoImg = Plugin.TextureProvider.CreateFromImageAsync(logoBytes).GetAwaiter().GetResult();
                        Group group = new Group()
                        {
                            groupID = id,
                            name = name,
                            logo = logoImg,
                            logoUrl = logoURL,
                            visible = visible,
                            openInvite = openInvite,
                            ProfileData = profileID > 0 ? new ProfileData { id = profileID } : null
                        };
                        GroupsData.groups.Add(group);
                        // Cache the group info for embed display even after leaving the group
                        GroupsData.CacheGroupInfo(id, name, logoURL, logoImg);
                    }
                    // Clear any pending join requests for groups we're now a member of
                    AbsoluteRP.Windows.Social.Views.GroupsData.ClearPendingJoinRequests();
                    Plugin.plugin.UpdateStatusAsync().GetAwaiter().GetResult();
                    // Handle the message as needed
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveGroupMemberships message: {ex}");
            }
        }

        public static void HandleWelcomeMessage(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    var msg = buffer.ReadString();
                    Plugin.plugin.UpdateStatusAsync().GetAwaiter().GetResult();
                    // Handle the message as needed
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling Welcome message: {ex}");
            }

        }
        public static void BadLogin(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    var profiles = buffer.ReadString();
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling BadLogin message: {ex}");
            }
        }



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
        public static void NoProfile(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    Inventory.ProfileBaseData.Clear();
                    ProfileWindow.profiles.Clear();
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    loggedIn = true;
                    BioLoadStatus = 0;
                    HooksLoadStatus = 0;
                    StoryLoadStatus = 0;
                    OOCLoadStatus = 0;
                    GalleryLoadStatus = 0;

                    AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.Story.storyTitle = string.Empty;
                    BookmarkLoadStatus = 0;

                    ProfileWindow.addProfile = false;
                    ProfileWindow.editProfile = false;
                    ProfileWindow.ExistingProfile = false;
                    Inventory.ExistingProfile = false;
                    ProfileWindow.ExistingProfile = false;
                    Inventory.ExistingProfile = false;


                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling NoProfile message: {ex}");
            }

        }
        public static void NoTargetProfile(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    loggedIn = true;
                    TargetProfileWindow.ExistingProfile = false;
                    TargetBioLoadStatus = 0;
                    TargetHooksLoadStatus = 0;
                    TargetStoryLoadStatus = 0;
                    TargetOOCLoadStatus = 0;
                    TargetGalleryLoadStatus = 0;
                    TargetNotesLoadStatus = 0;
                    TargetProfileWindow.addNotes = false;
                    Bookmarks.DisableBookmarkSelection = false;
                    ReportWindow.reportStatus = "";

                    Plugin.plugin.OpenTargetWindow();
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling NoTargetProfile message: {ex}");
            }
        }

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
                        ProfileWindow.CurrentProfile.id = profileID;
                        ProfileWindow.CurrentProfile.accountID = accountID;
                        ProfileWindow.CurrentProfile.playerName = characterName;
                        ProfileWindow.CurrentProfile.playerWorld = characterWorld;
                    }
                    else
                    {
                        TargetProfileWindow.profileData.id = profileID;
                        TargetProfileWindow.profileData.accountID = accountID;
                        TargetProfileWindow.profileData.playerName = characterName;
                        TargetProfileWindow.profileData.playerWorld = characterWorld;
                    }

                    Plugin.PluginLog.Debug($"[HandleTargetProfilePacket] Received profile data - ID: {profileID}, AccountID: {accountID}, Name: {characterName}@{characterWorld}, Self: {self}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling HandleTargetProfilePacket message: {ex}");
            }
        }



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
                    ProfileWindow.ExistingProfile = true;
                    Inventory.ExistingProfile = true;
                    loggedIn = true;
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveProfile message: {ex}");
            }
        }

        public static void StatusMessage(byte[] data)
        {
            try
            {
                Plugin.PluginLog.Info($"[StatusMessage] CALLED - data length: {data.Length}");
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    Plugin.PluginLog.Info($"[StatusMessage] packetID: {packetID}");
                    int userID = buffer.ReadInt(); // ADDED: Read userID from server
                    Plugin.PluginLog.Info($"[StatusMessage] userID read from buffer: {userID}");
                    int status = buffer.ReadInt();
                    Plugin.PluginLog.Info($"[StatusMessage] status: {status}");
                    int rank = buffer.ReadInt();
                    bool announce = buffer.ReadBool();
                    bool suspend = buffer.ReadBool();
                    bool ban = buffer.ReadBool();
                    bool warn = buffer.ReadBool();
                    string message = buffer.ReadString();
                    string message2 = buffer.ReadString();
                    string characterName = buffer.ReadString();
                    string characterWorld = buffer.ReadString();
                    permissions = new RankPermissions() { can_announce = announce, can_suspend = suspend, can_ban = ban, rank = rank, can_warn = warn };

                    // ADDED: Store userID for current user
                    DataSender.userID = userID;
                    Plugin.plugin.Configuration.account.userID = userID;
                    Plugin.PluginLog.Info($"[StatusMessage] Set userID to {userID}");

                    //Receive Status
                    if (status == (int)UI.StatusMessages.RECEIVE_SILENT)
                    {
                        silentUpdate = true;
                    }
                    if (status == (int)UI.StatusMessages.RECEIVE_UPDATES)
                    {
                        silentUpdate = false;
                    }

                    //account window
                    if (status == (int)UI.StatusMessages.LOGIN_BANNED)
                    {
                        MainPanel.statusColor = new Vector4(255, 0, 0, 255);
                        MainPanel.status = "Account Banned";
                    }
                    if (status == (int)UI.StatusMessages.LOGIN_UNVERIFIED)
                    {
                        MainPanel.statusColor = new Vector4(255, 255, 0, 255);
                        MainPanel.status = "Unverified Account";
                    }
                    if (status == (int)UI.StatusMessages.LOGIN_VERIFIED)
                    {
                        MainPanel.status = "Logged In";
                        MainPanel.statusColor = new Vector4(0, 255, 0, 255);
                        MainPanel.loggedIn = true;
                        Plugin.plugin.loggedIn = true;
                    }
                    if (status == (int)UI.StatusMessages.REGISTRATION_SUCCESSFUL)
                    {
                        MainPanel.statusColor = new Vector4(0, 255, 0, 255);
                        MainPanel.status = "Tag Creation Succeeded";
                        Account account = new Account()
                        {
                            accountKey = message,
                            accountName = message2
                        };
                        Plugin.plugin.Configuration.account = account;
                        Plugin.plugin.Configuration.Save();

                    }

                    if (status == (int)UI.StatusMessages.REGISTRATION_DUPLICATE_TAG_NAME)
                    {
                        MainPanel.statusColor = new Vector4(255, 255, 0, 255);
                        MainPanel.status = "Tag already in use.";
                    }
                    if (status == (int)UI.StatusMessages.CHARACTER_REGISTRATION_VALID_LODESTONE)
                    {
                        Character character = new Character()
                        {
                            characterName = characterName,
                            characterWorld = characterWorld,
                            characterKey = message2,
                        };
                        Plugin.plugin.Configuration.characters.Add(character);

                        Plugin.plugin.Configuration.Save();

                        ProfileWindow.VerificationSucceeded = true;
                        ProfileWindow.checking = false;
                        DataSender.FetchProfiles(character);
                        DataSender.FetchProfile(character, true, 0, character.characterName, character.characterWorld, -1);
                    }
                    if (status == (int)UI.StatusMessages.CHARACTER_REGISTRATIO_INVALID_LODESTONE)
                    {
                        ProfileWindow.VerificationFailed = true;
                        ProfileWindow.VerificationSucceeded = false;
                        ProfileWindow.checking = false;
                    }
                    if (status == (int)UI.StatusMessages.CHARACTER_REGISTRATION_LODESTONE_KEY)
                    {
                        ProfileWindow.lodeStoneKey = message;
                    }
                    if (status == (int)UI.StatusMessages.FORGOT_REQUEST_RECEIVED)
                    {
                        MainPanel.statusColor = new Vector4(0, 255, 0, 255);
                        MainPanel.status = "Request received, please stand by...";
                    }
                    if (status == (int)UI.StatusMessages.FORGOT_REQUEST_INCORRECT)
                    {
                        MainPanel.statusColor = new Vector4(255, 255, 0, 255);
                        MainPanel.status = "There is no account with this email.";
                    }
                    //Restoration window

                    if (status == (int)UI.StatusMessages.REGISTRATION_INSUFFICIENT_DATA)
                    {
                        MainPanel.statusColor = new Vector4(255, 0, 0, 255);
                        MainPanel.status = "Please fill all fields.";
                    }
                    if (status == (int)UI.StatusMessages.ACCOUNT_WARNING)
                    {
                        ImportantNotice.messageTitle = "Warning";
                        ImportantNotice.moderatorMessage = message;
                        Plugin.plugin.OpenImportantNoticeWindow();
                    }
                    if (status == (int)UI.StatusMessages.ACCOUNT_STRIKE)
                    {
                        ImportantNotice.messageTitle = "Your account received a strike!";
                        ImportantNotice.moderatorMessage = message;
                        Plugin.plugin.OpenImportantNoticeWindow();
                    }
                    if (status == (int)UI.StatusMessages.ACCOUNT_SUSPENDED)
                    {
                        Plugin.plugin.DisconnectAndLogOut();
                        Plugin.plugin.Configuration.Save();
                        MainPanel.statusColor = new Vector4(255, 0, 0, 255);
                        MainPanel.status = "Account suspended"; ;
                        if (message != string.Empty)
                        {
                            ImportantNotice.messageTitle = "Account Suspended!";
                            ImportantNotice.moderatorMessage = message;
                            Plugin.plugin.OpenImportantNoticeWindow();
                        }
                    }
                    if (status == (int)UI.StatusMessages.ACCOUNT_BANNED)
                    {
                        Plugin.plugin.DisconnectAndLogOut();
                        MainPanel.statusColor = new Vector4(255, 0, 0, 255);
                        MainPanel.status = "Account banned";
                        ImportantNotice.messageTitle = "Account Banned!";
                        ImportantNotice.moderatorMessage = message;
                        Plugin.plugin.OpenImportantNoticeWindow();
                    }
                    if (status == (int)UI.StatusMessages.ACTION_SUCCESS)
                    {
                        ModPanel.status = "Action was submitted";
                        ModPanel.statusColor = new Vector4(255, 0, 0, 255);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling StatusMessage message: {ex}");
            }
        }




      






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
                        ProfileWindow.ExistingProfile = true;
                        Inventory.ExistingProfile = true;
                        if (index >= 0 && index < ProfileWindow.profiles.Count)
                        {

                            ProfileWindow.CurrentProfile = ProfileWindow.profiles[index];
                        }
                        else
                        {
                            ProfileWindow.CurrentProfile = new ProfileData();
                            if (ProfileWindow.CurrentProfile.customTabs == null)
                                ProfileWindow.CurrentProfile.customTabs = new List<CustomTab>();
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

        public static void ReceiveProfiles(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int profileCount = buffer.ReadInt();
                    ProfileWindow.profiles.Clear();
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

                        ProfileWindow.profiles.Add(new ProfileData()
                        {
                            index = index,
                            title = name,
                            id = profileID,
                            accountID = accountID,
                            playerName = playerName,
                            playerWorld = playerWorld
                        });
                        Inventory.ProfileBaseData.Add(Tuple.Create(index, name));
                        GroupCreation.profiles.Add(new ProfileData() { index = index, title = name });
                        GroupManager.profiles.Add(new ProfileData() { index = index, title = name });
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

        public static void ReceiveNoAuthorization(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    MainPanel.statusColor = new Vector4(1, 0, 0, 1);
                    MainPanel.status = "Unauthorized Access to Profile.";
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveNoAuthorization message: {ex}");
            }
        }



        public static void ReceiveListingsByType(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int listingCount = buffer.ReadInt();
                    for (int i = 0; i < listingCount; i++)
                    {
                        string name = buffer.ReadString();
                        string description = buffer.ReadString();
                        string rules = buffer.ReadString();
                        int category = buffer.ReadInt();
                        int type = buffer.ReadInt();
                        int focus = buffer.ReadInt();
                        int setting = buffer.ReadInt();
                        string bannerURL = buffer.ReadString();
                        int inclusion = buffer.ReadInt();
                        string startDate = buffer.ReadString();
                        string endDate = buffer.ReadString();
                        //  IDalamudTextureWrap banner = Imaging.DownloadImage(bannerURL, i);
                        //  Listing listing = new Listing(name, description, rules, category, type, focus, setting, banner, inclusion, startDate, endDate);
                        //   SocialWindow.listings.Add(listing);
                    }
                    ListingsLoadStatus = 1;
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveNoTargetOOCInfo message: {ex}");
            }
        }
        internal static void ReceiveConnectedPlayers(byte[] data)
        {
            try
            {
                List<PlayerData> connectedPlayers = new List<PlayerData>();
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int connectionsCount = buffer.ReadInt();

                    for (int i = 0; i < connectionsCount; i++)
                    {
                        string playerName = buffer.ReadString();
                        string playerWorld = buffer.ReadString();
                        bool customName = buffer.ReadBool();
                        string profileName = buffer.ReadString();
                        PlayerData playerData = new PlayerData() { playername = playerName, worldname = playerWorld, profileName = profileName, customName = customName };
                        connectedPlayers.Add(playerData);
                    }
                    PlayerInteractions.playerDataMap = connectedPlayers;
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveConnections message: {ex}");
            }
        }
        internal static void ReceiveConnections(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int connectionsCount = buffer.ReadInt();
                    Connections.connetedProfileList.Clear();
                    Connections.sentProfileRequests.Clear();
                    Connections.receivedProfileRequests.Clear();
                    Connections.blockedProfileRequests.Clear();
                    for (int i = 0; i < connectionsCount; i++)
                    {
                        string requesterName = buffer.ReadString();
                        string requesterWorld = buffer.ReadString();
                        string receiverName = buffer.ReadString();
                        string receiverWorld = buffer.ReadString();
                        int status = buffer.ReadInt();
                        bool isReceiver = buffer.ReadBool();
                        Tuple<string, string> requester = Tuple.Create(requesterName, requesterWorld);
                        Tuple<string, string> receiver = Tuple.Create(receiverName, receiverWorld);
                        if (isReceiver)
                        {
                            if (status == (int)UI.ConnectionStatus.pending)
                            {
                                Connections.receivedProfileRequests.Add(requester);
                            }
                            if (status == (int)UI.ConnectionStatus.accepted)
                            {
                                PlayerData playerData = new PlayerData() { playername = requesterName, worldname = requesterWorld };
                                PlayerInteractions.playerDataMap.Add(playerData);
                                Connections.connetedProfileList.Add(requester);
                            }
                            if (status == (int)UI.ConnectionStatus.blocked)
                            {
                                Connections.blockedProfileRequests.Add(requester);
                            }
                            if (status == (int)UI.ConnectionStatus.refused)
                            {
                                if (Connections.receivedProfileRequests.Contains(requester))
                                {
                                    Connections.receivedProfileRequests.Remove(requester);
                                }
                            }
                        }
                        else if (!isReceiver)
                        {
                            if (status == (int)UI.ConnectionStatus.pending)
                            {
                                Connections.sentProfileRequests.Add(receiver);
                            }
                            if (status == (int)UI.ConnectionStatus.accepted)
                            {
                                PlayerData playerData = new PlayerData() { playername = receiverName, worldname = receiverWorld };
                                PlayerInteractions.playerDataMap.Add(playerData);
                                Connections.connetedProfileList.Add(receiver);
                            }
                            if (status == (int)UI.ConnectionStatus.blocked)
                            {
                                Connections.blockedProfileRequests.Add(receiver);
                            }
                            if (status == (int)UI.ConnectionStatus.refused)
                            {
                                //ConnectionsWindow.sentProfileRequests.Add(receiver);
                            }
                        }
                    }

                    Plugin.plugin.OpenSocialWindow();
                    Plugin.plugin.newConnection = false;
                    Plugin.plugin.CheckConnectionsRequestStatus();

                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveConnections message: {ex}");
            }
        }

        internal static void ReceiveConnectionsRequest(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    Plugin.PluginLog.Info("[ReceiveConnectionsRequest] Server notified of pending connection request - showing DTR bar");
                    Plugin.plugin.newConnection = true;
                    Plugin.plugin.CheckConnectionsRequestStatus();

                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveConnectionsRequest message: {ex}");
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
                    if (self)
                    {
                        ProfileWindow.CurrentProfile.customTabs.Clear();
                        if (AVATARBYTES == null || AVATARBYTES.Length == 0)
                        {
                            AVATARBYTES = UI.baseAvatarBytes();
                        }
                        if (BACKGROUNDBYTES != null || BACKGROUNDBYTES.Length != 0)
                        {
                            ProfileWindow.CurrentProfile.backgroundBytes = BACKGROUNDBYTES;
                        }
                        ProfileWindow.backgroundImage = await Plugin.TextureProvider.CreateFromImageAsync(BACKGROUNDBYTES);
                        ProfileWindow.CurrentProfile.isPrivate = isPrivate;
                        ProfileWindow.CurrentProfile.isActive = isTooltip;
                        ProfileWindow.CurrentProfile.avatarBytes = AVATARBYTES;
                        ProfileWindow.currentAvatarImg = await Plugin.TextureProvider.CreateFromImageAsync(AVATARBYTES);
                        ProfileWindow.CurrentProfile.title = NAME;
                        ProfileWindow.CurrentProfile.titleColor = new Vector4(colX, colY, colZ, colW);
                        ProfileWindow.CurrentProfile.SpoilerARR = ARR;
                        ProfileWindow.CurrentProfile.SpoilerHW = HW;
                        ProfileWindow.CurrentProfile.SpoilerSB = SB;
                        ProfileWindow.CurrentProfile.SpoilerSHB = SHB;
                        ProfileWindow.CurrentProfile.SpoilerEW = EW;
                        ProfileWindow.CurrentProfile.SpoilerDT = DT;
                        ProfileWindow.CurrentProfile.NSFW = NSFW;
                        ProfileWindow.CurrentProfile.TRIGGERING = TRIGGERING;
                        ProfileWindow.Sending = false;
                        ProfileWindow.Fetching = false;
                        ProfileWindow.setFauxName = fauxName;
                        ProfileWindow.showOnCompass = showCompass;
                    }
                    else
                    {
                        TargetProfileWindow.ExistingProfile = true;
                        TargetProfileWindow.addNotes = false;

                        TargetProfileWindow.RequestingProfile = false;
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
                        if (ARR || HW || SB || SHB || EW || DT || NSFW || TRIGGERING)
                        {
                            TargetProfileWindow.warning = true;
                        }
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
                        IDalamudTextureWrap backgroundImage = await Plugin.TextureProvider.CreateFromImageAsync(BACKGROUNDBYTES);
                        if (backgroundImage == null || backgroundImage.Handle == IntPtr.Zero)
                        {
                            TargetProfileWindow.profileData.background = UI.UICommonImage(UI.CommonImageTypes.backgroundHolder);
                        }
                        else
                        {
                            TargetProfileWindow.profileData.background = backgroundImage;
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
                        IDalamudTextureWrap customIcon = WindowOperations.RenderStatusIconAsync(Plugin.plugin, customIconID).GetAwaiter().GetResult();
                        if (customIcon == null)
                        {
                            customIcon = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                        }
                        profile.personalities.Add(new trait() { index = i, name = customName, description = customDescription, iconID = customIconID, icon = new IconElement { icon = customIcon } });
                    }
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

        internal static void ReceiveChatMessage(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int userID = buffer.ReadInt();
                    int profileID = buffer.ReadInt();
                    string Name = buffer.ReadString();
                    string World = buffer.ReadString();
                    string profileName = buffer.ReadString();
                    int avatarBytesLen = buffer.ReadInt();
                    byte[] avatarBytes = buffer.ReadBytes(avatarBytesLen);
                    string message = buffer.ReadString();
                    bool isAnnouncement = buffer.ReadBool();
                    IDalamudTextureWrap avatar = Plugin.TextureProvider.CreateFromImageAsync(avatarBytes).Result;
                    if (isAnnouncement)
                    {
                        message = message.Replace("/announce", "[ANNOUNCEMENT]");
                    }
                    ARPChatWindow.messages.Add(new ChatMessage { isAnnouncement = isAnnouncement, authorUserID = userID, authorProfileID = profileID, name = Name, world = World, authorName = profileName, avatar = avatar, message = message });

                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveConnectionsRequest message: {ex}");
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
                                imageElement.textureWrap = Imaging.DownloadElementImage(true, imageElement.url, imageElement).GetAwaiter().GetResult();
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

                    ProfileWindow.CurrentProfile.customTabs.Add(tab);
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveDynamicTab message: {ex}");
            }
        }

        internal static void ReceivePersonalListings(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int listingCount = buffer.ReadInt();
                    SocialWindow.listings.Clear();
                    for (int i = 0; i < listingCount; i++)
                    {
                        int profileID = buffer.ReadInt();
                        string name = buffer.ReadString();
                        int avatarLen = buffer.ReadInt();
                        byte[] avatarBytes = buffer.ReadBytes(avatarLen);

                        // Read all fields regardless of avatar validity
                        bool spoilerARR = buffer.ReadBool();
                        bool spoilerHW = buffer.ReadBool();
                        bool spoilerSB = buffer.ReadBool();
                        bool spoilerSHB = buffer.ReadBool();
                        bool spoilerEW = buffer.ReadBool();
                        bool spoilerDT = buffer.ReadBool();
                        bool nsfw = buffer.ReadBool();
                        bool triggering = buffer.ReadBool();
                        float colX = buffer.ReadFloat();
                        float colY = buffer.ReadFloat();
                        float colZ = buffer.ReadFloat();
                        float colW = buffer.ReadFloat();

                        IDalamudTextureWrap avatar = null;
                        if (avatarBytes != null && avatarBytes.Length > 0)
                        {
                            try
                            {
                                avatar = Plugin.TextureProvider.CreateFromImageAsync(avatarBytes).Result;
                            }
                            catch (Exception ex)
                            {
                                Plugin.PluginLog.Debug($"Invalid avatar image for tooltipData {profileID}: {ex.Message}");
                                avatar = null;
                            }
                        }

                        // Now skip adding the listing if avatar is null, but all fields have been read
                        if (avatar == null)
                            continue;

                        SocialWindow.listings.Add(
                            new Listing
                            {
                                type = 6,
                                id = profileID,
                                name = name,
                                avatar = avatar,
                                ARR = spoilerARR,
                                HW = spoilerHW,
                                SB = spoilerSB,
                                SHB = spoilerSHB,
                                EW = spoilerEW,
                                DT = spoilerDT,
                                color = new Vector4(colX, colY, colZ, colW),
                            });

                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceivePersonalListings message: {ex}");
            }
        }

        internal static void ReceivePlayerSyncData(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    string name = buffer.ReadString();
                    int modDataCount = buffer.ReadInt();

                    for (int i = 0; i < modDataCount; i++)
                    {
                        int byteLen = buffer.ReadInt();
                        byte[] bytes = buffer.ReadBytes(byteLen);

                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveConnectionsRequest message: {ex}");
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
                        ProfileWindow.CurrentProfile.customTabs.Clear();
                    }
                    else
                    {
                        tabsTargetCount = tabCount;
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

        internal static void ReceiveTradeRequest(byte[] data)
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
                    string receiverCharacterName = buffer.ReadString();
                    string receiverCharacterWorld = buffer.ReadString();
                    TradeWindow.tradeTargetName = receiverCharacterName;
                    TradeWindow.tradeTargetWorld = receiverCharacterWorld;
                    int inventoryTabCount = buffer.ReadInt();
                    TradeWindow.inventoryTabs.Clear();
                    for (int i = 0; i < inventoryTabCount; i++)
                    {
                        int index = buffer.ReadInt();
                        int id = buffer.ReadInt();
                        string tabName = buffer.ReadString();
                        Tuple<int, int, string> tab = Tuple.Create(index, id, tabName);

                        TradeWindow.inventoryTabs.Add(tab);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveConnectionsRequest message: {ex}");
            }
            finally
            {
                Plugin.plugin.OpenTradeWindow();
            }
        }
        internal static void ReceiveTradeInventory(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int inventoryID = buffer.ReadInt();
                    int inventoryCount = buffer.ReadInt();
                    TradeWindow.inventoryLayout.inventorySlotContents.Clear();
                    TradeWindow.inventoryLayout.tradeSlotContents.Clear();
                    TradeWindow.inventoryLayout.traderSlotContents.Clear();
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
                        TradeWindow.inventoryLayout.inventorySlotContents.Add(slotID, itemDefinition);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveConnectionsRequest message: {ex}");
            }
            finally
            {
                Plugin.plugin.OpenTradeWindow();
            }
        }
        internal static void ReceiveTradeUpdate(byte[] data)
        {
            try
            {
                Dictionary<int, ItemDefinition> traderItems = new Dictionary<int, ItemDefinition>();
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int profileID = buffer.ReadInt();
                    int itemCount = buffer.ReadInt();

                    for (int i = 0; i < itemCount; i++)
                    {
                        string name = buffer.ReadString();
                        string description = buffer.ReadString();
                        int type = buffer.ReadInt();
                        int subtype = buffer.ReadInt();
                        int iconID = buffer.ReadInt();
                        int slot = buffer.ReadInt();
                        int quality = buffer.ReadInt();

                        ItemDefinition itemDefinition = new ItemDefinition
                        {
                            name = name,
                            description = description,
                            type = type,
                            subtype = subtype,
                            iconID = iconID,
                            slot = slot,
                            quality = quality
                        };
                        Plugin.PluginLog.Debug($"Received item for trade: {itemDefinition.name} (Slot: {slot})");
                        traderItems[slot] = itemDefinition;
                    }

                    // Update the active trade window's InventoryLayout
                    if (TradeWindow.inventoryLayout != null)
                    {
                        TradeWindow.inventoryLayout.traderSlotContents = traderItems;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveTradeUpdate message: {ex}");
            }
            finally
            {
                Plugin.plugin.OpenTradeWindow();
            }
        }
        internal static void ReceiveTradeStatus(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    bool senderStatus = buffer.ReadBool();
                    bool receiverStatus = buffer.ReadBool();
                    TradeWindow.receiverReady = receiverStatus;
                    TradeWindow.senderReady = senderStatus;
                    Plugin.PluginLog.Debug($"Trade status updated - Sender: {senderStatus}, Receiver: {receiverStatus}");
                    Plugin.plugin.CloseTradeWindow();
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveTradeUpdate message: {ex}");
            }
        }
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
                        ItemDefinition itemDefinition = new ItemDefinition
                        {
                            name = itemName,
                            description = itemDescription,
                            type = itemType,
                            subtype = itemSubType,
                            iconID = iconID,
                            slot = slotID,
                            quality = quality
                        };
                        inventory.Add(i, itemDefinition);
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
                        if (ProfileWindow.CurrentProfile.customTabs == null)
                            ProfileWindow.CurrentProfile.customTabs = new List<CustomTab>();
                        ProfileWindow.CurrentProfile.customTabs.Add(tab);
                    }
                    else
                    {
                        EnsureTargetProfileData();
                        TargetProfileWindow.profileData.customTabs.Add(tab);
                    }
                    loadedTabsCount += 1;
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveInventoryTab message: {ex}");
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
                        if (ProfileWindow.CurrentProfile.customTabs == null)
                            ProfileWindow.CurrentProfile.customTabs = new List<CustomTab>();
                        ProfileWindow.CurrentProfile.customTabs.Add(tab);
                        loadedTabsCount += 1;
                    }
                    else
                    {
                        EnsureTargetProfileData();
                        TargetProfileWindow.profileData.customTabs.Add(tab);
                        loadedTargetTabsCount += 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveTargetOOCInfo message: {ex}");
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
                        if (ProfileWindow.CurrentProfile.customTabs == null)
                            ProfileWindow.CurrentProfile.customTabs = new List<CustomTab>();
                        ProfileWindow.CurrentProfile.customTabs.Add(tab);
                        loadedTabsCount += 1;
                    }
                    else
                    {
                        EnsureTargetProfileData();
                        TargetProfileWindow.profileData.customTabs.Add(tab);
                        loadedTargetTabsCount += 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveProfileBio message: {ex}");
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
                        if (ProfileWindow.CurrentProfile.customTabs == null)
                            ProfileWindow.CurrentProfile.customTabs = new List<CustomTab>();
                        ProfileWindow.CurrentProfile.customTabs.Add(tab);
                        loadedTabsCount += 1;
                    }
                    else
                    {
                        EnsureTargetProfileData();
                        TargetProfileWindow.profileData.customTabs.Add(tab);
                        loadedTargetTabsCount += 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveProfileBio message: {ex}");
            }
        }

        public static void ReceiveProfileGalleryTab(byte[] data)
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
                    int galleryImageCount = buffer.ReadInt();
                    List<ProfileGalleryImage> gallery = new List<ProfileGalleryImage>();

                    for (int i = 0; i < galleryImageCount; i++)
                    {
                        string url = buffer.ReadString();
                        Plugin.PluginLog.Debug(url);
                        string tooltip = buffer.ReadString();
                        bool nsfw = buffer.ReadBool();
                        bool trigger = buffer.ReadBool();
                        ProfileGalleryImage galleryImage = Imaging.DownloadProfileImage(true, url, tooltip, profileID, nsfw, trigger, Plugin.plugin, i).GetAwaiter().GetResult();
                        if (galleryImage.thumbnail == null || galleryImage.thumbnail.Handle == IntPtr.Zero)
                        {
                            galleryImage.image = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                        }
                        if (galleryImage.image == null || galleryImage.image.Handle == IntPtr.Zero)
                        {
                            galleryImage.image = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                        }
                        gallery.Add(galleryImage);

                        if (self)
                        {
                            loadedGalleryImages += 1;
                            GalleryImagesToLoad = galleryImageCount;
                            ProfileWindow.loading = "Gallery Image" + i;
                        }
                        else
                        {
                            loadedTargetGalleryImages += 1;
                            TargetGalleryImagesToLoad = galleryImageCount;
                            TargetProfileWindow.loading = "Gallery Image" + i;
                        }
                    }

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

                    if (self)
                    {
                        if (ProfileWindow.CurrentProfile.customTabs == null)
                            ProfileWindow.CurrentProfile.customTabs = new List<CustomTab>();

                        ProfileWindow.CurrentProfile.customTabs.Add(tab);
                        loadedTabsCount += 1;
                        loadedGalleryImages = 0;
                        GalleryImagesToLoad = 0;
                    }
                    else
                    {
                        if (TargetProfileWindow.profileData.customTabs == null)
                            TargetProfileWindow.profileData.customTabs = new List<CustomTab>();
                        EnsureTargetProfileData();
                        TargetProfileWindow.profileData.customTabs.Add(tab);
                        loadedTargetTabsCount += 1;
                        loadedTargetGalleryImages = 0;
                        TargetGalleryImagesToLoad = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveProfileBio message: {ex}");
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

                    // --- Read Paths ---
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

                    // --- Read PathConnections ---
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

                    // --- Read Relationships ---
                    int relCount = buffer.ReadInt();
                    var relationships = new List<Relationship>();
                    for (int i = 0; i < relCount; i++)
                    {
                        var rel = new Relationship();
                        rel.Name = buffer.ReadString();
                        rel.Description = buffer.ReadString();
                        rel.IconID = buffer.ReadInt();
                        rel.active = buffer.ReadBool();
                        rel.IconTexture = WindowOperations.RenderIconAsync(Plugin.plugin, rel.IconID).GetAwaiter().GetResult();
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
                        if (ProfileWindow.CurrentProfile?.customTabs == null)
                            ProfileWindow.CurrentProfile.customTabs = new List<CustomTab>();
                        ProfileWindow.CurrentProfile.customTabs.Add(customTab);
                        loadedTabsCount += 1;
                    }
                    else
                    {
                        EnsureTargetProfileData();
                        TargetProfileWindow.profileData.customTabs.Add(customTab);
                        loadedTargetTabsCount += 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveTreeLayout: {ex}");
            }
        }
        public static void ReceiveConnectedPlayersInMap(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int connectionsCount = buffer.ReadInt();
                    for (int i = 0; i < connectionsCount; i++)
                    {
                        bool active = buffer.ReadBool();
                        string fauxName = buffer.ReadString();
                        string playerName = buffer.ReadString();
                        string playerWorld = buffer.ReadString();

                        if (active)
                        {
                            PlayerData playerData = new PlayerData
                            {
                                playername = playerName,
                                worldname = playerWorld,
                                fauxName = fauxName,
                            };
                            PlayerInteractions.playerDataMap.Add(playerData);
                        }



                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveConnectedPlayersInMap message: {ex}");
            }
        }

     
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
                    for (int i = 0; i < customPersonalitiesCount; i++)
                    {
                        string customName = buffer.ReadString();
                        string customDescription = buffer.ReadString();
                        int customIconID = buffer.ReadInt();
                        IDalamudTextureWrap customIcon = WindowOperations.RenderStatusIconAsync(Plugin.plugin, customIconID).GetAwaiter().GetResult();
                        if (customIcon == null)
                        {
                            customIcon = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                        }
                        traits.Add(new trait() { index = i, name = customName, description = customDescription, iconID = customIconID, icon = new IconElement { icon = customIcon } });
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
                        traits = traits.Select(t => new trait
                        {
                            index = t.index,
                            name = t.name,
                            description = t.description,
                            iconID = t.iconID,
                            modifying = t.modifying,
                            icon = new IconElement { icon = t.icon.icon }
                        }).ToList(),
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

                        if (ProfileWindow.CurrentProfile.customTabs == null)
                            ProfileWindow.CurrentProfile.customTabs = new List<CustomTab>();
                        ProfileWindow.CurrentProfile.customTabs.Add(tab);
                        ProfileWindow.CurrentProfile.id = profileID; // Store profile ID
                        loadedTabsCount += 1;
                    }
                    else
                    {
                        EnsureTargetProfileData();
                        TargetProfileWindow.profileData.customTabs.Add(targetTab);
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
                Plugin.PluginLog.Debug($"Debug handling ReceiveProfileBio message: {ex}");
            }
        }

        internal static void ReceiveGroup(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();

                    // Server writes a presence bool first
                    bool hasGroup = buffer.ReadBool();
                    if (!hasGroup)
                        return;

                    // Basic group data (matches server ReceiveGroup layout)
                    int groupID = buffer.ReadInt();
                    string name = buffer.ReadString();
                    string description = buffer.ReadString();
                    string logoURL = buffer.ReadString();
                    string backgroundURL = buffer.ReadString();
                    bool openInvite = buffer.ReadBool();
                    bool visible = buffer.ReadBool();
                    int profileID = buffer.ReadInt();

                    // Ranks
                    int rankCount = buffer.ReadInt();
                    var ranks = new List<object>(); // server sends full rank objects; client can expand later
                    for (int i = 0; i < rankCount; i++)
                    {
                        int rid = buffer.ReadInt();
                        string rname = buffer.ReadString();
                        string rdesc = buffer.ReadString();
                        int permCount = buffer.ReadInt();
                        var perms = new List<(int, bool, bool, bool, bool, bool)>();
                        for (int p = 0; p < permCount; p++)
                        {
                            int prank = buffer.ReadInt();
                            bool canAnn = buffer.ReadBool();
                            bool canWarn = false;
                            try { canWarn = buffer.ReadBool(); } catch { canWarn = false; } // fallbacks
                            bool canStrike = buffer.ReadBool();
                            bool canSuspend = buffer.ReadBool();
                            bool canBan = buffer.ReadBool();
                            bool canPromote = false;
                            try { canPromote = buffer.ReadBool(); } catch { canPromote = false; }

                            perms.Add((prank, canAnn, canWarn, canStrike, canSuspend, canBan));
                        }
                        ranks.Add(new { id = rid, name = rname, description = rdesc, perms = perms });
                    }

                    // Members
                    int memberCount = buffer.ReadInt();
                    var members = new List<object>();
                    for (int i = 0; i < memberCount; i++)
                    {
                        int mid = buffer.ReadInt();
                        int mprofileID = buffer.ReadInt();
                        bool owner = buffer.ReadBool();
                        string mname = buffer.ReadString();
                        string mnote = buffer.ReadString();
                        int mrankId = buffer.ReadInt();
                        int avatarLen = buffer.ReadInt();
                        byte[] avatarBytes = buffer.ReadBytes(avatarLen);
                        IDalamudTextureWrap avatar = Plugin.TextureProvider.CreateFromImageAsync(avatarBytes).GetAwaiter().GetResult();
                        members.Add(new { id = mid, profileID = mprofileID, owner = owner, name = mname, note = mnote, rankID = mrankId, avatar=avatar });
                    }

                    // Bans
                    int banCount = buffer.ReadInt();
                    var bansList = new List<GroupBans>();
                    for (int i = 0; i < banCount; i++)
                    {
                        int banId = buffer.ReadInt();
                        int banUserID = buffer.ReadInt();
                        int banProfileID = buffer.ReadInt();
                        string banName = buffer.ReadString();
                        string lodestone = buffer.ReadString();
                        bansList.Add(new GroupBans
                        {
                            id = banId,
                            userID = banUserID,
                            profileID = banProfileID,
                            name = banName,
                            lodestoneURL = lodestone
                        });
                    }

                    // Application
                    object application = null;
                    bool hasApp = buffer.ReadBool();
                    if (hasApp)
                    {
                        int appId = buffer.ReadInt();
                        string appName = buffer.ReadString();
                        int secCount = buffer.ReadInt();
                        var sections = new List<object>();
                        for (int s = 0; s < secCount; s++)
                        {
                            int sIndex = buffer.ReadInt();
                            string sName = buffer.ReadString();
                            string sDesc = buffer.ReadString();
                            int inputsCount = buffer.ReadInt();
                            var inputs = new List<object>();
                            for (int inp = 0; inp < inputsCount; inp++)
                            {
                                int idx = buffer.ReadInt();
                                int type = buffer.ReadInt();
                                string inName = buffer.ReadString();
                                string inDesc = buffer.ReadString();
                                inputs.Add(new { index = idx, type = type, name = inName, description = inDesc });
                            }
                            sections.Add(new { index = sIndex, name = sName, description = sDesc, inputs = inputs });
                        }
                        application = new { id = appId, name = appName, sections = sections };
                    }

                    // Channels
                    int channelCount = buffer.ReadInt();
                    var channels = new List<object>();
                    for (int i = 0; i < channelCount; i++)
                    {
                        int cid = buffer.ReadInt();
                        int cindex = buffer.ReadInt();
                        string cname = buffer.ReadString();
                        string cdesc = buffer.ReadString();

                        int allowedMembersCount = buffer.ReadInt();
                        var allowedMembers = new List<int>();
                        for (int am = 0; am < allowedMembersCount; am++)
                            allowedMembers.Add(buffer.ReadInt());

                        int allowedRanksCount = buffer.ReadInt();
                        var allowedRanks = new List<int>();
                        for (int ar = 0; ar < allowedRanksCount; ar++)
                            allowedRanks.Add(buffer.ReadInt());

                        channels.Add(new { id = cid, index = cindex, name = cname, description = cdesc, allowedMembers, allowedRanks });
                    }

                    // Best-effort: create textures (kept separate so object initializer stays valid)
                    IDalamudTextureWrap backgroundTex = null;
                    IDalamudTextureWrap logoTex = null;
                    // Attempt to download group logo and background (best-effort). Use plugin texture provider.
                    try
                    {
                        if (!string.IsNullOrEmpty(backgroundURL))
                        {
                            var bgBytes = Imaging.FetchUrlImageBytes(backgroundURL).GetAwaiter().GetResult();
                            if (bgBytes != null && bgBytes.Length > 0)
                                backgroundTex = Plugin.TextureProvider.CreateFromImageAsync(bgBytes).GetAwaiter().GetResult();
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.PluginLog.Debug($"ReceiveGroup: background load failed: {ex}");
                    }

                    try
                    {
                        if (!string.IsNullOrEmpty(logoURL))
                        {
                            var logoBytes = Imaging.FetchUrlImageBytes(logoURL).GetAwaiter().GetResult();
                            if (logoBytes != null && logoBytes.Length > 0)
                                logoTex = Plugin.TextureProvider.CreateFromImageAsync(logoBytes).GetAwaiter().GetResult();
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.PluginLog.Debug($"ReceiveGroup: logo load failed: {ex}");
                    }

                    // Build client Group model (uses AbsoluteRP.Defines.Group)
                    Group group = new Group()
                    {
                        groupID = groupID,
                        name = name ?? string.Empty,
                        description = description ?? string.Empty,
                        openInvite = openInvite,
                        visible = visible,
                        // set texture properties if Group has them
                        logo = logoTex,
                        background = backgroundTex,
                        // Set ProfileData with the profile ID from server
                        ProfileData = new ProfileData() { id = profileID },
                        // Set bans list
                        bans = bansList,
                    };

                    GroupsData.groups.Add(group);

                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveGroup message: {ex}");
            }
        }
        public static void HandleSendGroupChatMessage(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int messageCount = buffer.ReadInt(); // Server sends message count, not groupID

                List<GroupChatMessage> messages = new List<GroupChatMessage>();

                // Cache textures by userID to avoid creating duplicate textures for same user
                Dictionary<int, IDalamudTextureWrap> userAvatarCache = new Dictionary<int, IDalamudTextureWrap>();

                for (int i = 0; i < messageCount; i++)
                {
                    var chatMessage = new GroupChatMessage
                    {
                        messageID = buffer.ReadInt(),
                        groupID = buffer.ReadInt(),
                        channelID = buffer.ReadInt(),
                        senderUserID = buffer.ReadInt(),
                        senderName = buffer.ReadString(),
                        messageContent = buffer.ReadString(),
                        timestamp = buffer.ReadLong(),
                        isPinned = buffer.ReadBool()
                    };

                    // Read avatar if present
                    bool hasAvatar = buffer.ReadBool();
                    if (hasAvatar)
                    {
                        int avatarLength = buffer.ReadInt();
                        byte[] avatarBytes = buffer.ReadBytes(avatarLength);

                        // Check if we already created a texture for this user
                        if (userAvatarCache.TryGetValue(chatMessage.senderUserID, out IDalamudTextureWrap cachedTexture))
                        {
                            // Reuse existing texture
                            chatMessage.avatar = cachedTexture;
                            Plugin.PluginLog.Info($"[HandleSendGroupChatMessage] Reusing cached avatar for message {chatMessage.messageID}, user {chatMessage.senderName}");
                        }
                        else if (avatarBytes != null && avatarBytes.Length > 0)
                        {
                            // Convert to texture for first time
                            try
                            {
                                var texture = Plugin.TextureProvider.CreateFromImageAsync(avatarBytes).GetAwaiter().GetResult();
                                if (texture != null)
                                {
                                    chatMessage.avatar = texture;
                                    userAvatarCache[chatMessage.senderUserID] = texture; // Cache it
                                    Plugin.PluginLog.Info($"[HandleSendGroupChatMessage] Loaded and cached avatar for message {chatMessage.messageID}, user {chatMessage.senderName}");
                                }
                                else
                                {
                                    Plugin.PluginLog.Warning($"[HandleSendGroupChatMessage] Texture creation returned null for message {chatMessage.messageID}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Plugin.PluginLog.Debug($"[HandleSendGroupChatMessage] Failed to load avatar for message {chatMessage.messageID}: {ex.Message}");
                            }
                        }
                    }

                    messages.Add(chatMessage);
                }

                Plugin.PluginLog.Info($"Received {messages.Count} chat messages from server");

                // Update the chat window/view if it's open for this group/channel
                if (messages.Count > 0)
                {
                    int channelID = messages[0].channelID;
                    bool delivered = false;

                    // Try to deliver to GroupChatWindow (standalone window)
                    var groupChatWindow = GroupChatWindow.CurrentInstance;
                    if (groupChatWindow != null)
                    {
                        Plugin.PluginLog.Info($"Found open GroupChatWindow, calling OnMessagesReceived with {messages.Count} messages for channel {channelID}");
                        groupChatWindow.OnMessagesReceived(channelID, messages);
                        delivered = true;
                    }

                    // ALSO deliver to Groups.cs view (embedded chat)
                    Plugin.PluginLog.Info($"Delivering {messages.Count} messages to GroupsData.OnMessagesReceived");
                    AbsoluteRP.Windows.Social.Views.GroupsData.OnMessagesReceived(messages);
                    delivered = true;

                    if (!delivered)
                    {
                        Plugin.PluginLog.Info($"No open chat interface found to deliver {messages.Count} messages");
                    }
                }

                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleSendGroupChatMessage: {ex.Message}");
            }
        }

        public static void HandleGroupChatMessageBroadcast(byte[] data)
        {
            try
            {
                Plugin.PluginLog.Info($"[CLIENT] HandleGroupChatMessageBroadcast called - data length: {data.Length}");

                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int messageID = buffer.ReadInt();
                int groupID = buffer.ReadInt();
                int channelID = buffer.ReadInt();
                int userID = buffer.ReadInt();
                string profileName = buffer.ReadString();
                string messageContent = buffer.ReadString();
                long timestamp = buffer.ReadLong();

                // Read avatar bytes if present
                IDalamudTextureWrap avatarTexture = null;
                bool hasAvatar = buffer.ReadBool();
                if (hasAvatar)
                {
                    int avatarLength = buffer.ReadInt();
                    byte[] avatarBytes = buffer.ReadBytes(avatarLength);

                    Plugin.PluginLog.Info($"[CLIENT] Received avatar with broadcast: {avatarLength} bytes");

                    // Convert to texture with error handling
                    if (avatarBytes != null && avatarBytes.Length > 0)
                    {
                        try
                        {
                            avatarTexture = Plugin.TextureProvider.CreateFromImageAsync(avatarBytes).GetAwaiter().GetResult();
                            if (avatarTexture != null)
                            {
                                Plugin.PluginLog.Info($"[CLIENT] Converted avatar to texture");
                            }
                            else
                            {
                                Plugin.PluginLog.Warning($"[CLIENT] Avatar texture creation returned null");
                            }
                        }
                        catch (Exception ex)
                        {
                            Plugin.PluginLog.Debug($"[CLIENT] Failed to create avatar texture: {ex.Message}");
                        }
                    }
                }
                else
                {
                    Plugin.PluginLog.Info($"[CLIENT] No avatar included in broadcast");
                }

                buffer.Dispose();

                Plugin.PluginLog.Info($"[CLIENT] Broadcast details - messageID={messageID}, groupID={groupID}, channelID={channelID}, userID={userID}, profileName='{profileName}', content='{messageContent}'");

                // Handle the incoming broadcast message
                var chatMessage = new GroupChatMessage
                {
                    messageID = messageID,
                    groupID = groupID,
                    channelID = channelID,
                    senderUserID = userID,
                    senderName = profileName,
                    messageContent = messageContent,
                    timestamp = timestamp,
                    isEdited = false,
                    deleted = false,
                    avatar = avatarTexture
                };

                Plugin.PluginLog.Info($"[CLIENT] Calling GroupsData.OnNewMessageBroadcast...");

                // Update the Groups view with the new message
                AbsoluteRP.Windows.Social.Views.GroupsData.OnNewMessageBroadcast(chatMessage);

                Plugin.PluginLog.Info($"[CLIENT] GroupsData.OnNewMessageBroadcast completed");

                // Also update GroupChatWindow if it's open
                if (GroupChatWindow.CurrentInstance != null)
                {
                    Plugin.PluginLog.Info($"[CLIENT] Calling GroupChatWindow.OnNewMessageBroadcast...");
                    GroupChatWindow.CurrentInstance.OnNewMessageBroadcast(chatMessage);
                    Plugin.PluginLog.Info($"[CLIENT] GroupChatWindow.OnNewMessageBroadcast completed");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"[CLIENT] Error in HandleGroupChatMessageBroadcast: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        public static void HandleInviteNotification(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int inviteID = buffer.ReadInt();
                int groupID = buffer.ReadInt();
                string groupName = buffer.ReadString();
                string groupLogoUrl = buffer.ReadString();
                int inviterUserID = buffer.ReadInt();
                string inviterName = buffer.ReadString();
                string message = buffer.ReadString();
                long invitedAt = buffer.ReadLong();

                buffer.Dispose();

                Plugin.PluginLog.Info($"Received invite notification from {inviterName} for group {groupName} (ID: {groupID})");

                // Create a GroupInvite object for the notification window
                var invite = new GroupInvite
                {
                    inviteID = inviteID,
                    groupID = groupID,
                    groupName = groupName,
                    groupLogoUrl = groupLogoUrl,
                    inviterUserID = inviterUserID,
                    inviterName = inviterName,
                    message = message,
                    createdAt = invitedAt,
                    status = 0 // pending
                };

                // Show the notification to the user
                GroupInviteNotification.AddInvite(invite);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleInviteNotification: {ex.Message}");
            }
        }

        public static async void HandleGroupMemberAvatar(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int userID = buffer.ReadInt();
                bool hasAvatar = buffer.ReadBool();

                Plugin.PluginLog.Info($"[HandleGroupMemberAvatar] Received avatar for userID {userID}, hasAvatar: {hasAvatar}");

                if (hasAvatar)
                {
                    int avatarLength = buffer.ReadInt();
                    byte[] avatarBytes = buffer.ReadBytes(avatarLength);

                    Plugin.PluginLog.Info($"[HandleGroupMemberAvatar] Avatar bytes length: {avatarLength}");

                    // Call Groups.OnAvatarReceived to update messages
                    AbsoluteRP.Windows.Social.Views.GroupsData.OnAvatarReceived(userID, avatarBytes);
                }
                else
                {
                    Plugin.PluginLog.Warning($"[HandleGroupMemberAvatar] No avatar data for userID {userID}");
                }

                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleGroupMemberAvatar: {ex.Message}");
            }
        }

        public static async void HandleFetchGroupChatMessages(int connectionID, byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int groupID = buffer.ReadInt();
                int channelID = buffer.ReadInt();
                int messageCount = buffer.ReadInt();

                List<GroupChatMessage> messages = new List<GroupChatMessage>();
                for (int i = 0; i < messageCount; i++)
                {
                    var msg = new GroupChatMessage
                    {
                        messageID = buffer.ReadInt(),
                        groupID = groupID,
                        channelID = channelID,
                        senderUserID = buffer.ReadInt(),
                        senderName = buffer.ReadString(),
                        senderProfileID = buffer.ReadInt(),
                        messageContent = buffer.ReadString(),
                        timestamp = buffer.ReadLong()
                    };
                    messages.Add(msg);
                }
                buffer.Dispose();

                // Load avatars for all messages
                foreach (var msg in messages)
                {
                    if (msg.senderProfileID > 0)
                    {
                        await LoadMessageAvatar(msg, msg.senderProfileID);
                    }
                }

                // Load messages into the chat window
                // TODO: Implement GroupChatWindow
                // GroupChatWindow.LoadMessages(groupID, channelID, messages);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleFetchGroupChatMessages: {ex.Message}");
            }
        }

        public static async void HandleUpdateChatReadStatus(int connectionID, byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int groupID = buffer.ReadInt();
                int channelID = buffer.ReadInt();
                long lastReadTimestamp = buffer.ReadLong();
                buffer.Dispose();

                // Update the read status for this channel
                // TODO: Implement GroupChatWindow
                // Plugin.GroupChatWindow?.UpdateReadStatus(groupID, channelID, lastReadTimestamp);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleUpdateChatReadStatus: {ex.Message}");
            }
        }

        public static void HandleFetchGroupCategories(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int categoryCount = buffer.ReadInt();

                List<GroupCategory> categories = new List<GroupCategory>();
                for (int i = 0; i < categoryCount; i++)
                {
                    var category = new GroupCategory
                    {
                        id = buffer.ReadInt(),
                        groupID = buffer.ReadInt(),
                        sortOrder = buffer.ReadInt(),
                        name = buffer.ReadString(),
                        description = buffer.ReadString(),
                        collapsed = buffer.ReadBool()
                    };

                    // Read channels for this category
                    int channelCount = buffer.ReadInt();
                    category.channels = new List<GroupChannel>();
                    for (int j = 0; j < channelCount; j++)
                    {
                        int channelId = buffer.ReadInt();
                        int channelGroupId = buffer.ReadInt();
                        int channelCategoryId = buffer.ReadInt();
                        int channelIndex = buffer.ReadInt();
                        string channelName = buffer.ReadString();
                        string channelDescription = buffer.ReadString();
                        int channelChannelType = buffer.ReadInt();
                        bool channelIsLocked = buffer.ReadBool();
                        bool channelIsNsfw = buffer.ReadBool();
                        bool channelEveryoneCanView = buffer.ReadBool();
                        bool channelEveryoneCanPost = buffer.ReadBool();

                        // Read member permissions
                        int memberPermCount = buffer.ReadInt();
                        var memberPermissions = new List<ChannelMemberPermission>();
                        for (int mp = 0; mp < memberPermCount; mp++)
                        {
                            memberPermissions.Add(new ChannelMemberPermission
                            {
                                memberID = buffer.ReadInt(),
                                memberName = buffer.ReadString(),
                                canView = buffer.ReadBool(),
                                canPost = buffer.ReadBool()
                            });
                        }

                        // Read rank permissions
                        int rankPermCount = buffer.ReadInt();
                        var rankPermissions = new List<ChannelRankPermission>();
                        for (int rp = 0; rp < rankPermCount; rp++)
                        {
                            rankPermissions.Add(new ChannelRankPermission
                            {
                                rankID = buffer.ReadInt(),
                                rankName = buffer.ReadString(),
                                canView = buffer.ReadBool(),
                                canPost = buffer.ReadBool()
                            });
                        }

                        // Read role permissions
                        int rolePermCount = buffer.ReadInt();
                        var rolePermissions = new List<ChannelRolePermission>();
                        for (int rolep = 0; rolep < rolePermCount; rolep++)
                        {
                            rolePermissions.Add(new ChannelRolePermission
                            {
                                roleID = buffer.ReadInt(),
                                roleName = buffer.ReadString(),
                                roleColor = buffer.ReadString(),
                                canView = buffer.ReadBool(),
                                canPost = buffer.ReadBool()
                            });
                        }

                        var channel = new GroupChannel
                        {
                            id = channelId,
                            groupID = channelGroupId,
                            categoryID = channelCategoryId,
                            index = channelIndex,
                            name = channelName,
                            description = channelDescription,
                            channelType = channelChannelType,
                            isLocked = channelIsLocked,
                            isNsfw = channelIsNsfw,
                            everyoneCanView = channelEveryoneCanView,
                            everyoneCanPost = channelEveryoneCanPost,
                            MemberPermissions = memberPermissions,
                            RankPermissions = rankPermissions,
                            RolePermissions = rolePermissions
                        };
                        Plugin.PluginLog.Info($"[HandleFetchGroupCategories] Channel '{channelName}' (id={channelId}) everyoneCanView={channelEveryoneCanView} everyoneCanPost={channelEveryoneCanPost} members={memberPermCount} ranks={rankPermCount} roles={rolePermCount}");
                        category.channels.Add(channel);
                    }

                    categories.Add(category);
                }

                Plugin.PluginLog.Info($"Received {categories.Count} categories with channels from server");
                DataReceiver.categories = categories;

                // Update the current group's categories if it exists
                if (AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup != null && categories.Count > 0)
                {
                    int groupID = categories[0].groupID;
                    if (AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.groupID == groupID)
                    {
                        AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.categories = categories;
                        Plugin.PluginLog.Info($"Updated currentGroup.categories with {categories.Count} categories");
                    }
                }

                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleFetchGroupCategories: {ex.Message}");
            }
        }

        public static async void HandleSaveGroupRosterFields(int connectionID, byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                bool success = buffer.ReadBool();
                string message = buffer.ReadString();
                buffer.Dispose();

                if (success)
                {
                    Plugin.PluginLog.Info("Roster fields saved successfully");
                }
                else
                {
                    Plugin.PluginLog.Debug($"Failed to save roster fields: {message}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleSaveGroupRosterFields: {ex.Message}");
            }
        }

        public static void HandleFetchGroupRosterFields(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int groupID = buffer.ReadInt();
                int fieldCount = buffer.ReadInt();

                List<GroupRosterField> fields = new List<GroupRosterField>();
                for (int i = 0; i < fieldCount; i++)
                {
                    var field = new GroupRosterField
                    {
                        id = buffer.ReadInt(),
                        groupID = groupID,
                        name = buffer.ReadString(),
                        fieldType = buffer.ReadInt(),
                        required = buffer.ReadBool(),
                        sortOrder = buffer.ReadInt()
                    };
                    fields.Add(field);
                }
                buffer.Dispose();

                // Update the group manager with roster fields
                // TODO: Implement GroupManager
                // Plugin.GroupManager?.UpdateRosterFields(groupID, fields);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleFetchGroupRosterFields: {ex.Message}");
            }
        }

        public static async void HandleSaveMemberMetadata(int connectionID, byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                bool success = buffer.ReadBool();
                string message = buffer.ReadString();
                buffer.Dispose();

                if (success)
                {
                    Plugin.PluginLog.Info("Member metadata saved successfully");
                }
                else
                {
                    Plugin.PluginLog.Debug($"Failed to save member metadata: {message}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleSaveMemberMetadata: {ex.Message}");
            }
        }

        public static void HandleFetchMemberMetadata(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int groupID = buffer.ReadInt();
                int memberID = buffer.ReadInt();
                int metadataCount = buffer.ReadInt();

                Dictionary<string, string> metadata = new Dictionary<string, string>();
                for (int i = 0; i < metadataCount; i++)
                {
                    string key = buffer.ReadString();
                    string value = buffer.ReadString();
                    metadata[key] = value;
                }
                buffer.Dispose();

                // Update member metadata in the group manager
                // TODO: Implement GroupManager
                // Plugin.GroupManager?.UpdateMemberMetadata(groupID, memberID, metadata);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleFetchMemberMetadata: {ex.Message}");
            }
        }

        public static async void HandleSaveMemberFieldValues(int connectionID, byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                bool success = buffer.ReadBool();
                string message = buffer.ReadString();
                buffer.Dispose();

                if (success)
                {
                    Plugin.PluginLog.Info("Member field values saved successfully");
                }
                else
                {
                    Plugin.PluginLog.Debug($"Failed to save member field values: {message}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleSaveMemberFieldValues: {ex.Message}");
            }
        }

        public static void HandleFetchMemberFieldValues(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int groupID = buffer.ReadInt();
                int memberID = buffer.ReadInt();
                int valueCount = buffer.ReadInt();

                Dictionary<int, string> fieldValues = new Dictionary<int, string>();
                for (int i = 0; i < valueCount; i++)
                {
                    int fieldID = buffer.ReadInt();
                    string value = buffer.ReadString();
                    fieldValues[fieldID] = value;
                }
                buffer.Dispose();

                // Update member field values in the group manager
                // TODO: Implement GroupManager
                // Plugin.GroupManager?.UpdateMemberFieldValues(groupID, memberID, fieldValues);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleFetchMemberFieldValues: {ex.Message}");
            }
        }

        public static void HandleDeleteGroupChatMessage(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int messageID = buffer.ReadInt();
                int groupID = buffer.ReadInt();
                int channelID = buffer.ReadInt();
                buffer.Dispose();

                Plugin.PluginLog.Info($"[HandleDeleteGroupChatMessage] Received delete broadcast for message {messageID} in group {groupID}, channel {channelID}");

                // Remove the message from the current messages list
                GroupsData.OnMessageDeleted(messageID, groupID, channelID);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleDeleteGroupChatMessage: {ex.Message}");
            }
        }

        public static void HandleEditGroupChatMessage(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int messageID = buffer.ReadInt();
                int groupID = buffer.ReadInt();
                int channelID = buffer.ReadInt();
                string newContent = buffer.ReadString();
                buffer.Dispose();

                Plugin.PluginLog.Info($"[HandleEditGroupChatMessage] Received edit broadcast for message {messageID} in group {groupID}, channel {channelID}");

                // Update the message in the current messages list
                GroupsData.OnMessageEdited(messageID, groupID, channelID, newContent);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleEditGroupChatMessage: {ex.Message}");
            }
        }

        // Static storage for pinned messages
        public static List<GroupChatMessage> pinnedMessages = new List<GroupChatMessage>();
        public static bool pinnedMessagesLoaded = false;
        public static string pinOperationMessage = string.Empty;

        public static void HandlePinnedMessages(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int groupID = buffer.ReadInt();
                int channelID = buffer.ReadInt();
                int messageCount = buffer.ReadInt();

                var messages = new List<GroupChatMessage>();
                for (int i = 0; i < messageCount; i++)
                {
                    var msg = new GroupChatMessage
                    {
                        messageID = buffer.ReadInt(),
                        senderUserID = buffer.ReadInt(),
                        senderProfileID = buffer.ReadInt(),
                        senderName = buffer.ReadString(),
                        messageContent = buffer.ReadString(),
                        timestamp = buffer.ReadLong(),
                        isEdited = buffer.ReadBool(),
                        isPinned = buffer.ReadBool(),
                        groupID = groupID,
                        channelID = channelID
                    };

                    // Read avatar
                    bool hasAvatar = buffer.ReadBool();
                    if (hasAvatar)
                    {
                        int avatarLength = buffer.ReadInt();
                        byte[] avatarBytes = buffer.ReadBytes(avatarLength);
                        try
                        {
                            msg.avatar = Plugin.TextureProvider.CreateFromImageAsync(avatarBytes).GetAwaiter().GetResult();
                        }
                        catch (Exception ex)
                        {
                            Plugin.PluginLog.Debug($"Failed to load avatar for pinned message: {ex.Message}");
                        }
                    }

                    messages.Add(msg);
                }
                buffer.Dispose();

                pinnedMessages = messages;
                pinnedMessagesLoaded = true;

                Plugin.PluginLog.Info($"[HandlePinnedMessages] Received {messages.Count} pinned messages for group {groupID}, channel {channelID}");
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandlePinnedMessages: {ex.Message}");
            }
        }

        public static void HandleMessagePinResult(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                bool success = buffer.ReadBool();
                string message = buffer.ReadString();
                buffer.Dispose();

                pinOperationMessage = message;

                if (success)
                {
                    Plugin.PluginLog.Info($"Pin operation: {message}");
                }
                else
                {
                    Plugin.PluginLog.Debug($"Pin operation failed: {message}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleMessagePinResult: {ex.Message}");
            }
        }

        public static void HandleMessagePinUpdate(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int groupID = buffer.ReadInt();
                int channelID = buffer.ReadInt();
                int messageID = buffer.ReadInt();
                bool isPinned = buffer.ReadBool();
                buffer.Dispose();

                Plugin.PluginLog.Info($"[HandleMessagePinUpdate] Message {messageID} in group {groupID}, channel {channelID} is now {(isPinned ? "pinned" : "unpinned")}");

                // Update the message in the current messages list
                GroupsData.OnMessagePinUpdated(messageID, groupID, channelID, isPinned);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleMessagePinUpdate: {ex.Message}");
            }
        }

        public static void HandleChannelLockUpdate(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int groupID = buffer.ReadInt();
                int channelID = buffer.ReadInt();
                bool isLocked = buffer.ReadBool();
                buffer.Dispose();

                Plugin.PluginLog.Info($"[HandleChannelLockUpdate] Channel {channelID} in group {groupID} is now {(isLocked ? "locked" : "unlocked")}");

                // Update the channel lock status in the local categories
                GroupsData.OnChannelLockUpdated(groupID, channelID, isLocked);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleChannelLockUpdate: {ex.Message}");
            }
        }

        public static void HandleGroupInviteResult(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                bool success = buffer.ReadBool();
                string message = buffer.ReadString();
                buffer.Dispose();

                if (success)
                {
                    Plugin.PluginLog.Info($"Group invite: {message}");
                }
                else
                {
                    Plugin.PluginLog.Debug($"Group invite failed: {message}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleGroupInviteResult: {ex.Message}");
            }
        }

        public static void HandleGroupInvites(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int inviteCount = buffer.ReadInt();

                List<GroupInvite> invites = new List<GroupInvite>();
                for (int i = 0; i < inviteCount; i++)
                {
                    var invite = new GroupInvite
                    {
                        inviteID = buffer.ReadInt(),
                        groupID = buffer.ReadInt(),
                        groupName = buffer.ReadString(),
                        groupDescription = buffer.ReadString(),
                        inviterUserID = buffer.ReadInt(),
                        inviterName = buffer.ReadString(),
                        inviteeUserID = buffer.ReadInt(),
                        inviteeName = buffer.ReadString(),
                        inviteeProfileID = buffer.ReadInt(),
                        message = buffer.ReadString(),
                        status = buffer.ReadByte(),
                        createdAt = buffer.ReadLong(),
                        groupLogoUrl = buffer.ReadString()
                    };

                    invites.Add(invite);

                    // Add pending invites to notification window only if I am the invitee
                    if (invite.status == 0 && invite.inviteeUserID == DataSender.userID) // 0 = pending
                    {
                        GroupInviteNotification.AddInvite(invite);
                    }
                }

                // Only open the notification window if there are pending invites for me
                if (GroupInviteNotification.GetPendingInviteCount() > 0)
                {
                    Plugin.groupInviteNotification.IsOpen = true;
                }
                // Update currentGroup's invites list if it exists
                if (GroupsData.currentGroup != null && invites.Count > 0)
                {
                    // Check if these invites belong to the current group
                    int firstInviteGroupID = invites[0].groupID;
                    if (firstInviteGroupID == GroupsData.currentGroup.groupID)
                    {
                        GroupsData.currentGroup.invites = invites;
                        Plugin.PluginLog.Info($"Updated currentGroup.invites with {invites.Count} invites");
                    }
                }

                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleGroupInvites: {ex.Message}");
            }
        }

        public static void HandleGroupMembers(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int memberCount = buffer.ReadInt(); // Server sends member count, NOT groupID

                List<GroupMember> members = new List<GroupMember>();
                for (int i = 0; i < memberCount; i++)
                {
                    var member = new GroupMember
                    {
                        id = buffer.ReadInt(),
                        userID = buffer.ReadInt(), // Read userID (was missing)
                        profileID = buffer.ReadInt(),
                        owner = buffer.ReadBool(),
                        name = buffer.ReadString(),
                        note = buffer.ReadString()
                    };

                    // Read legacy single rank (optional)
                    bool hasRank = buffer.ReadBool();
                    if (hasRank)
                    {
                        member.rank = new GroupRank
                        {
                            id = buffer.ReadInt(),
                            name = buffer.ReadString()
                        };
                    }

                    // Initialize ranks list
                    member.ranks = new List<GroupRank>();

                    // Read multiple ranks (new format)
                    // Server sends: int rankCount, then for each rank: int id, string name, int hierarchy
                    int rankCount = buffer.ReadInt();
                    for (int r = 0; r < rankCount; r++)
                    {
                        var rank = new GroupRank
                        {
                            id = buffer.ReadInt(),
                            name = buffer.ReadString(),
                            hierarchy = buffer.ReadInt()
                        };
                        member.ranks.Add(rank);
                    }

                    // If no multiple ranks but has legacy rank, add it to the list
                    if (member.ranks.Count == 0 && member.rank != null)
                    {
                        member.ranks.Add(member.rank);
                    }

                    // Read avatar (optional)
                    bool hasAvatar = buffer.ReadBool();
                    if (hasAvatar)
                    {
                        int avatarLength = buffer.ReadInt();
                        byte[] avatarBytes = buffer.ReadBytes(avatarLength);

                        // Convert avatar bytes to texture
                        if (avatarBytes != null && avatarBytes.Length > 0)
                        {
                            try
                            {
                                member.avatar = Plugin.TextureProvider.CreateFromImageAsync(avatarBytes).GetAwaiter().GetResult();
                                Plugin.PluginLog.Info($"[HandleGroupMembers] Loaded avatar for member {member.name} (userID={member.userID})");
                            }
                            catch (Exception ex)
                            {
                                Plugin.PluginLog.Debug($"[HandleGroupMembers] Failed to load avatar for member {member.name}: {ex.Message}");
                            }
                        }
                    }

                    // Read self-assigned roles
                    int selfRoleCount = buffer.ReadInt();
                    member.selfAssignedRoles = new List<GroupSelfAssignRole>();
                    for (int r = 0; r < selfRoleCount; r++)
                    {
                        member.selfAssignedRoles.Add(new GroupSelfAssignRole
                        {
                            id = buffer.ReadInt(),
                            name = buffer.ReadString(),
                            color = buffer.ReadString()
                        });
                    }

                    members.Add(member);
                }

                Plugin.PluginLog.Info($"Received {members.Count} members from server");
                DataReceiver.members = members; // Store in static cache

                // Update the current group's members if it exists and members have groupID
                // Note: We can't determine groupID from packet, so update currentGroup if it exists
                if (AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup != null && members.Count > 0)
                {
                    // Queue old avatar textures for deferred disposal to prevent race conditions with rendering
                    // Clear the member avatar cache which will queue textures for safe disposal
                    AbsoluteRP.Windows.Social.Views.GroupsData.ClearMemberAvatarCache();

                    if (AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.members != null)
                    {
                        Plugin.PluginLog.Info($"[HandleGroupMembers] Queueing {AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.members.Count} old avatar textures for disposal");
                        foreach (var oldMember in AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.members)
                        {
                            if (oldMember.avatar != null)
                            {
                                // Queue for deferred disposal instead of immediate dispose
                                AbsoluteRP.Windows.Social.Views.GroupsData.QueueTextureForDisposal(oldMember.avatar);
                                oldMember.avatar = null; // Clear reference immediately
                                Plugin.PluginLog.Info($"[HandleGroupMembers] Queued avatar for {oldMember.name} for deferred disposal");
                            }
                        }
                    }

                    AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.members = members;
                    Plugin.PluginLog.Info($"Updated currentGroup.members with {members.Count} members");

                    // Link member ranks to full rank data with permissions
                    if (AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.ranks != null)
                    {
                        foreach (var member in members)
                        {
                            // Link legacy single rank
                            if (member.rank != null && member.rank.id > 0)
                            {
                                var fullRank = AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.ranks.FirstOrDefault(r => r.id == member.rank.id);
                                if (fullRank != null)
                                {
                                    member.rank = fullRank;
                                    Plugin.PluginLog.Info($"[HandleGroupMembers] Linked member {member.name} to legacy rank {fullRank.name} with permissions");
                                }
                            }

                            // Link multiple ranks to full rank data
                            if (member.ranks != null)
                            {
                                for (int r = 0; r < member.ranks.Count; r++)
                                {
                                    var memberRank = member.ranks[r];
                                    if (memberRank != null && memberRank.id > 0)
                                    {
                                        var fullRank = AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.ranks.FirstOrDefault(x => x.id == memberRank.id);
                                        if (fullRank != null)
                                        {
                                            member.ranks[r] = fullRank;
                                            Plugin.PluginLog.Info($"[HandleGroupMembers] Linked member {member.name} to rank {fullRank.name} with permissions");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleGroupMembers: {ex.Message}");
            }
        }

        public static void HandleForumStructure(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int categoryCount = buffer.ReadInt(); // Server sends category count, NOT groupID

                List<GroupForumCategory> categories = new List<GroupForumCategory>();
                for (int i = 0; i < categoryCount; i++)
                {
                    var category = new GroupForumCategory
                    {
                        id = buffer.ReadInt(),
                        groupID = buffer.ReadInt(), // Read groupID from packet
                        parentCategoryID = buffer.ReadInt(),
                        categoryIndex = buffer.ReadInt(),
                        name = buffer.ReadString(),
                        description = buffer.ReadString(),
                        icon = buffer.ReadString(),
                        collapsed = buffer.ReadBool(),
                        categoryType = (byte)buffer.ReadInt(), // Server writes INT, cast to byte
                        sortOrder = buffer.ReadInt(),
                        createdAt = buffer.ReadLong(),
                        updatedAt = buffer.ReadLong(),
                        channels = new List<GroupForumChannel>()
                    };

                    int channelCount = buffer.ReadInt();
                    for (int j = 0; j < channelCount; j++)
                    {
                        var channel = new GroupForumChannel
                        {
                            id = buffer.ReadInt(),
                            groupID = buffer.ReadInt(), // Read groupID from packet
                            categoryID = buffer.ReadInt(), // Read categoryID from packet
                            parentChannelID = buffer.ReadInt(),
                            channelIndex = buffer.ReadInt(),
                            name = buffer.ReadString(),
                            description = buffer.ReadString(),
                            channelType = (byte)buffer.ReadInt(), // Server writes INT, cast to byte
                            isLocked = buffer.ReadBool(),
                            isNSFW = buffer.ReadBool(),
                            sortOrder = buffer.ReadInt(),
                            createdAt = buffer.ReadLong(),
                            updatedAt = buffer.ReadLong(),
                            lastMessageAt = buffer.ReadLong(),
                            subChannels = new List<GroupForumChannel>()
                        };

                        int subChannelCount = buffer.ReadInt();
                        for (int k = 0; k < subChannelCount; k++)
                        {
                            var subChannel = new GroupForumChannel
                            {
                                id = buffer.ReadInt(),
                                groupID = buffer.ReadInt(), // Read groupID from packet
                                parentChannelID = buffer.ReadInt(),
                                channelIndex = buffer.ReadInt(),
                                name = buffer.ReadString(),
                                description = buffer.ReadString(),
                                channelType = (byte)buffer.ReadInt(), // Server writes INT, cast to byte
                                isLocked = buffer.ReadBool(),
                                isNSFW = buffer.ReadBool(),
                                sortOrder = buffer.ReadInt(),
                                createdAt = buffer.ReadLong(),
                                updatedAt = buffer.ReadLong(),
                                lastMessageAt = buffer.ReadLong()
                            };
                            channel.subChannels.Add(subChannel);
                        }

                        category.channels.Add(channel);
                    }

                    categories.Add(category);
                }
                buffer.Dispose();

                Plugin.PluginLog.Info($"Received forum structure with {categories.Count} categories");

                // Update the forum structure in the group manager
                // TODO: Implement GroupManager
                // Plugin.GroupManager?.UpdateForumStructure(groupID, categories);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleForumStructure: {ex.Message}");
            }
        }

        public static void HandleForumPermissions(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int groupID = buffer.ReadInt();
                int permissionCount = buffer.ReadInt();

                List<GroupForumChannelPermission> permissions = new List<GroupForumChannelPermission>();
                for (int i = 0; i < permissionCount; i++)
                {
                    var perm = new GroupForumChannelPermission
                    {
                        channelID = buffer.ReadInt(),
                        rankID = buffer.ReadInt(),
                        userID = buffer.ReadInt(),
                        canView = buffer.ReadBool(),
                        canPost = buffer.ReadBool(),
                        canReply = buffer.ReadBool(),
                        canCreateThreads = buffer.ReadBool(),
                        canEditOwn = buffer.ReadBool(),
                        canDeleteOwn = buffer.ReadBool(),
                        canManage = buffer.ReadBool(),
                        canPin = buffer.ReadBool(),
                        canLock = buffer.ReadBool()
                    };
                    permissions.Add(perm);
                }
                buffer.Dispose();

                // Update the forum permissions in the group manager
                // TODO: Implement GroupManager
                // Plugin.GroupManager?.UpdateForumPermissions(groupID, permissions);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleForumPermissions: {ex.Message}");
            }
        }

        public static void HandleInviteeProfile(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();

                bool hasProfile = buffer.ReadBool();
                if (!hasProfile)
                {
                    buffer.Dispose();
                    Plugin.PluginLog.Info("No invitee profile data available");
                    return;
                }

                // Read profile data (structure depends on your Profile class)
                int profileID = buffer.ReadInt();
                string profileName = buffer.ReadString();
                // Add more fields as needed based on your Profile structure

                buffer.Dispose();

                // Display or cache the invitee profile
                // TODO: Implement GroupManager
                // Plugin.GroupManager?.ShowInviteeProfile(profileID, profileName);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleInviteeProfile: {ex.Message}");
            }
        }

        public static void HandleGroupRanks(byte[] data)
        {
            try
            {
                using (ByteBuffer buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    int packetID = buffer.ReadInt();
                    int rankCount = buffer.ReadInt();

                    ranks.Clear();

                    for (int i = 0; i < rankCount; i++)
                    {
                        var rank = new GroupRank
                        {
                            id = buffer.ReadInt(),
                            groupID = buffer.ReadInt(),
                            name = buffer.ReadString(),
                            description = buffer.ReadString(),
                            hierarchy = buffer.ReadInt(),
                            isDefaultMember = buffer.ReadBool(),
                            permissions = new GroupRankPermissions
                            {
                                // Member Permissions
                                canInvite = buffer.ReadBool(),
                                canKick = buffer.ReadBool(),
                                canBan = buffer.ReadBool(),
                                canPromote = buffer.ReadBool(),
                                canDemote = buffer.ReadBool(),

                                // Message Permissions
                                canCreateAnnouncement = buffer.ReadBool(),
                                canReadMessages = buffer.ReadBool(),
                                canSendMessages = buffer.ReadBool(),
                                canDeleteOthersMessages = buffer.ReadBool(),
                                canPinMessages = buffer.ReadBool(),

                                // Category Permissions
                                canCreateCategory = buffer.ReadBool(),
                                canEditCategory = buffer.ReadBool(),
                                canDeleteCategory = buffer.ReadBool(),
                                canLockCategory = buffer.ReadBool(),

                                // Forum Permissions
                                canCreateForum = buffer.ReadBool(),
                                canEditForum = buffer.ReadBool(),
                                canDeleteForum = buffer.ReadBool(),
                                canLockForum = buffer.ReadBool(),
                                canMuteForum = buffer.ReadBool(),

                                // Rank Management Permissions
                                canManageRanks = buffer.ReadBool(),
                                canCreateRanks = buffer.ReadBool()
                            }
                        };

                        ranks.Add(rank);
                    }

                    Plugin.PluginLog.Info($"Received {rankCount} ranks from server");

                    // Update the current group's ranks if it exists
                    if (AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup != null && ranks.Count > 0)
                    {
                        int groupID = ranks[0].groupID;
                        if (AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.groupID == groupID)
                        {
                            AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.ranks = new List<GroupRank>(ranks);
                            Plugin.PluginLog.Info($"Updated currentGroup.ranks with {ranks.Count} ranks");

                            // Link existing members to the full rank data with permissions
                            if (AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.members != null)
                            {
                                foreach (var member in AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.members)
                                {
                                    if (member.rank != null && member.rank.id > 0)
                                    {
                                        var fullRank = ranks.FirstOrDefault(r => r.id == member.rank.id);
                                        if (fullRank != null)
                                        {
                                            member.rank = fullRank;
                                            Plugin.PluginLog.Info($"[HandleGroupRanks] Linked member {member.name} to rank {fullRank.name} with permissions");
                                        }
                                    }
                                }
                            }
                        }
                    }

                    buffer.Dispose();
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleGroupRanks: {ex.Message}");
            }
        }

        public static void HandleRankOperationResult(byte[] data)
        {
            try
            {
                using (ByteBuffer buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    int packetID = buffer.ReadInt();
                    rankOperationSuccess = buffer.ReadBool();
                    rankOperationMessage = buffer.ReadString();

                    buffer.Dispose();

                    if (rankOperationSuccess)
                    {
                        Plugin.PluginLog.Info($"Rank operation succeeded: {rankOperationMessage}");
                    }
                    else
                    {
                        Plugin.PluginLog.Warning($"Rank operation failed: {rankOperationMessage}");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleRankOperationResult: {ex.Message}");
            }
        }

        #region Helper Methods for Group Chat

        private static Dictionary<int, IDalamudTextureWrap> messageAvatarCache = new Dictionary<int, IDalamudTextureWrap>();

        private static async Task LoadMessageAvatar(GroupChatMessage message, int profileID)
        {
            try
            {
                // Check cache first
                if (messageAvatarCache.ContainsKey(profileID))
                {
                    message.avatar = messageAvatarCache[profileID];
                    return;
                }

                // TODO: Implement profile data fetching from server for avatars
                // For now, use default avatar placeholder
                // Future: Request profile avatar bytes from server using profileID
                var defaultAvatar = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                messageAvatarCache[profileID] = defaultAvatar;
                message.avatar = defaultAvatar;
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error loading message avatar: {ex.Message}");
                message.avatar = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
            }
        }

        #endregion

        #region Profile Likes Handlers

        public static void HandleLikesRemainingPacket(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID
                likesRemaining = buffer.ReadInt();
                Plugin.PluginLog.Debug($"Likes remaining: {likesRemaining}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleLikesRemainingPacket error: {ex.Message}");
            }
        }

        public static void HandleLikeResultPacket(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID
                likeResultSuccess = buffer.ReadBool();
                likeResultMessage = buffer.ReadString();
                Plugin.PluginLog.Debug($"Like result: {likeResultSuccess} - {likeResultMessage}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleLikeResultPacket error: {ex.Message}");
            }
        }

        public static void HandleProfileLikeCountsPacket(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID
                profileLikeCounts.Clear();
                int count = buffer.ReadInt();
                for (int i = 0; i < count; i++)
                {
                    int profileID = buffer.ReadInt();
                    int likeCount = buffer.ReadInt();
                    profileLikeCounts[profileID] = likeCount;
                }
                Plugin.PluginLog.Debug($"Received like counts for {count} profiles");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleProfileLikeCountsPacket error: {ex.Message}");
            }
        }

        public static void HandleProfileLikesPacket(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID
                currentProfileLikes.Clear();
                int profileID = buffer.ReadInt();
                int count = buffer.ReadInt();

                for (int i = 0; i < count; i++)
                {
                    ProfileLike like = new ProfileLike
                    {
                        likerUserID = buffer.ReadInt(),
                        likerProfileID = buffer.ReadInt(),
                        likerName = buffer.ReadString(),
                        comment = buffer.ReadString(),
                        likeCount = buffer.ReadInt(),
                        likedAt = buffer.ReadLong()
                    };

                    if (like.likerProfileID == -1)
                        like.likerProfileID = 0;

                    currentProfileLikes.Add(like);
                }

                Plugin.PluginLog.Debug($"Received {count} likes for profile {profileID}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleProfileLikesPacket error: {ex.Message}");
            }
        }

        #endregion

        #region Rules Channel & Self-Assign Roles

        public static void HandleGroupRulesResponse(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                rulesOperationSuccess = buffer.ReadBool();
                rulesOperationMessage = buffer.ReadString();

                Plugin.PluginLog.Debug($"HandleGroupRulesResponse: groupID={groupID}, success={rulesOperationSuccess}, message={rulesOperationMessage}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleGroupRulesResponse error: {ex.Message}");
            }
        }

        public static void HandleRulesAgreementResponse(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                bool success = buffer.ReadBool();
                int agreedVersion = buffer.ReadInt();

                if (success)
                {
                    hasAgreedToRules = true;

                    // Update the current member's agreement status in the group
                    var group = AbsoluteRP.Windows.Social.Views.GroupsData.groups?.FirstOrDefault(g => g.groupID == groupID);
                    if (group != null && group.members != null)
                    {
                        var member = group.members.FirstOrDefault(m => m.userID == DataSender.userID);
                        if (member != null)
                        {
                            member.hasAgreedToRules = true;
                            member.agreedRulesVersion = agreedVersion;
                        }
                    }
                }

                Plugin.PluginLog.Debug($"HandleRulesAgreementResponse: groupID={groupID}, success={success}, version={agreedVersion}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleRulesAgreementResponse error: {ex.Message}");
            }
        }

        public static void HandleGroupRules(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                groupRulesContent = buffer.ReadString();
                groupRulesVersion = buffer.ReadInt();
                hasAgreedToRules = buffer.ReadBool();
                isGroupOwner = buffer.ReadBool();

                Plugin.PluginLog.Debug($"HandleGroupRules: groupID={groupID}, version={groupRulesVersion}, hasAgreed={hasAgreedToRules}, isOwner={isGroupOwner}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleGroupRules error: {ex.Message}");
            }
        }

        public static void HandleSelfAssignRoleResponse(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                int roleID = buffer.ReadInt();
                selfRoleOperationSuccess = buffer.ReadBool();
                selfRoleOperationMessage = buffer.ReadString();

                Plugin.PluginLog.Debug($"HandleSelfAssignRoleResponse: groupID={groupID}, roleID={roleID}, success={selfRoleOperationSuccess}, message={selfRoleOperationMessage}");
                buffer.Dispose();

                // If operation was successful, refresh the roles list
                if (selfRoleOperationSuccess)
                {
                    var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                        x.characterName == Plugin.plugin.playername &&
                        x.characterWorld == Plugin.plugin.playerworld);
                    if (character != null)
                    {
                        DataSender.FetchSelfAssignRoles(character, groupID);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleSelfAssignRoleResponse error: {ex.Message}");
            }
        }

        public static void HandleSelfAssignRoles(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                canManageSelfAssignRoles = buffer.ReadBool();
                int roleCount = buffer.ReadInt();

                selfAssignRoles.Clear();

                for (int i = 0; i < roleCount; i++)
                {
                    var role = new GroupSelfAssignRole
                    {
                        id = buffer.ReadInt(),
                        sectionID = buffer.ReadInt(),
                        name = buffer.ReadString(),
                        color = buffer.ReadString(),
                        description = buffer.ReadString(),
                        sortOrder = buffer.ReadInt(),
                        groupID = groupID,
                        channelPermissions = new List<GroupChannelRolePermission>()
                    };

                    // Read channel permissions
                    int permCount = buffer.ReadInt();
                    for (int j = 0; j < permCount; j++)
                    {
                        role.channelPermissions.Add(new GroupChannelRolePermission
                        {
                            channelID = buffer.ReadInt(),
                            canView = buffer.ReadBool(),
                            canPost = buffer.ReadBool(),
                            roleID = role.id
                        });
                    }

                    selfAssignRoles.Add(role);
                }

                Plugin.PluginLog.Debug($"HandleSelfAssignRoles: groupID={groupID}, roleCount={roleCount}, canManage={canManageSelfAssignRoles}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleSelfAssignRoles error: {ex.Message}");
            }
        }

        public static void HandleSelfRoleAssignmentResponse(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                int roleID = buffer.ReadInt();
                bool assigned = buffer.ReadBool(); // true = assigned, false = unassigned
                bool success = buffer.ReadBool();

                if (success)
                {
                    if (assigned)
                    {
                        if (!memberSelfRoleIDs.Contains(roleID))
                        {
                            memberSelfRoleIDs.Add(roleID);
                        }
                    }
                    else
                    {
                        memberSelfRoleIDs.Remove(roleID);
                    }
                }

                Plugin.PluginLog.Debug($"HandleSelfRoleAssignmentResponse: groupID={groupID}, roleID={roleID}, assigned={assigned}, success={success}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleSelfRoleAssignmentResponse error: {ex.Message}");
            }
        }

        public static void HandleRoleChannelPermissionsResponse(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                int roleID = buffer.ReadInt();
                bool success = buffer.ReadBool();

                selfRoleOperationSuccess = success;
                selfRoleOperationMessage = success ? "Channel permissions saved" : "Failed to save channel permissions";

                Plugin.PluginLog.Debug($"HandleRoleChannelPermissionsResponse: groupID={groupID}, roleID={roleID}, success={success}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleRoleChannelPermissionsResponse error: {ex.Message}");
            }
        }

        public static void HandleMemberSelfRoles(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                int count = buffer.ReadInt();

                memberSelfRoleIDs.Clear();

                for (int i = 0; i < count; i++)
                {
                    memberSelfRoleIDs.Add(buffer.ReadInt());
                }

                Plugin.PluginLog.Debug($"HandleMemberSelfRoles: groupID={groupID}, roleCount={count}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleMemberSelfRoles error: {ex.Message}");
            }
        }

        public static void HandleCreateChannelError(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                createChannelError = buffer.ReadString();

                Plugin.PluginLog.Debug($"HandleCreateChannelError: {createChannelError}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleCreateChannelError error: {ex.Message}");
            }
        }

        public static void HandleRoleSections(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                int sectionCount = buffer.ReadInt();

                roleSections.Clear();

                for (int i = 0; i < sectionCount; i++)
                {
                    var section = new GroupRoleSection
                    {
                        id = buffer.ReadInt(),
                        groupID = groupID,
                        name = buffer.ReadString(),
                        sortOrder = buffer.ReadInt()
                    };
                    roleSections.Add(section);
                }

                Plugin.PluginLog.Debug($"HandleRoleSections: groupID={groupID}, sectionCount={sectionCount}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleRoleSections error: {ex.Message}");
            }
        }

        public static void HandleGroupBans(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                int banCount = buffer.ReadInt();

                var bansList = new List<GroupBans>();

                for (int i = 0; i < banCount; i++)
                {
                    bansList.Add(new GroupBans
                    {
                        id = buffer.ReadInt(),
                        userID = buffer.ReadInt(),
                        profileID = buffer.ReadInt(),
                        name = buffer.ReadString(),
                        lodestoneURL = buffer.ReadString()
                    });
                }

                // Update the current group's bans list if it matches
                if (AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup != null &&
                    AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.groupID == groupID)
                {
                    AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.bans = bansList;
                }

                Plugin.PluginLog.Debug($"HandleGroupBans: groupID={groupID}, banCount={banCount}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleGroupBans error: {ex.Message}");
            }
        }

        public static void HandleMemberRemovedFromGroup(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                int removedMemberID = buffer.ReadInt();
                int removedUserID = buffer.ReadInt();
                bool isBan = buffer.ReadBool();
                string groupName = buffer.ReadString();

                buffer.Dispose();

                Plugin.PluginLog.Info($"HandleMemberRemovedFromGroup: groupID={groupID}, memberID={removedMemberID}, userID={removedUserID}, isBan={isBan}, groupName={groupName}");

                // Check if this is about the current user being removed
                bool isMe = removedUserID == DataSender.userID;

                if (isMe)
                {
                    // I was kicked/banned from the group
                    string action = isBan ? "banned from" : "kicked from";
                    Plugin.Chat?.Print($"You have been {action} the group: {groupName}");

                    // Remove the group from our groups list
                    AbsoluteRP.Windows.Social.Views.GroupsData.groups?.RemoveAll(g => g.groupID == groupID);

                    // If this is the current group, clear it
                    if (AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup?.groupID == groupID)
                    {
                        AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup = null;
                        AbsoluteRP.Windows.Social.Views.GroupsData.ClearSelectedChannel();
                    }
                }
                else
                {
                    // Someone else was kicked/banned - remove them from the current group's member list
                    if (AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup?.groupID == groupID)
                    {
                        AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.members?.RemoveAll(m => m.id == removedMemberID);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleMemberRemovedFromGroup error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles group info response for embed display.
        /// Caches the group name and logo URL for security.
        /// </summary>
        public static void HandleGroupInfo(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                string name = buffer.ReadString();
                string logoUrl = buffer.ReadString();

                buffer.Dispose();

                Plugin.PluginLog.Debug($"HandleGroupInfo: groupID={groupID}, name={name}, logoUrl={logoUrl}");

                // Cache the group info
                AbsoluteRP.Windows.Social.Views.GroupsData.CacheGroupInfo(groupID, name, logoUrl, null);

                // Start async logo fetch if we have a URL
                if (!string.IsNullOrEmpty(logoUrl))
                {
                    AbsoluteRP.Windows.Social.Views.GroupsData.FetchAndCacheLogoAsync(groupID, logoUrl);
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleGroupInfo error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles profile info response for embed display.
        /// Caches the profile name and avatar URL for security.
        /// </summary>
        public static void HandleProfileInfoEmbed(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int profileID = buffer.ReadInt();
                string name = buffer.ReadString();
                string avatarUrl = buffer.ReadString();

                buffer.Dispose();

                Plugin.PluginLog.Debug($"HandleProfileInfoEmbed: profileID={profileID}, name={name}, avatarUrl={avatarUrl}");

                // Cache the profile info
                AbsoluteRP.Windows.Social.Views.GroupsData.CacheProfileInfo(profileID, name, avatarUrl, null);

                // Start async avatar fetch if we have a URL
                if (!string.IsNullOrEmpty(avatarUrl))
                {
                    AbsoluteRP.Windows.Social.Views.GroupsData.FetchAndCacheAvatarAsync(profileID, avatarUrl);
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleProfileInfoEmbed error: {ex.Message}");
            }
        }

        #endregion

        #region Form Channel Handlers

        /// <summary>
        /// Handles form fields response for a channel
        /// </summary>
        public static void HandleFormFields(byte[] data)
        {
            try
            {
                Plugin.PluginLog.Info($"[HandleFormFields] Received form fields packet");
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int channelId = buffer.ReadInt();
                int fieldCount = buffer.ReadInt();
                Plugin.PluginLog.Info($"[HandleFormFields] channelId={channelId}, fieldCount={fieldCount}");

                var fields = new List<FormField>();
                for (int i = 0; i < fieldCount; i++)
                {
                    var field = new FormField
                    {
                        id = buffer.ReadInt(),
                        channelId = buffer.ReadInt(),
                        title = buffer.ReadString(),
                        fieldType = buffer.ReadInt(),
                        isOptional = buffer.ReadBool(),
                        sortOrder = buffer.ReadInt()
                    };
                    fields.Add(field);
                    Plugin.PluginLog.Info($"[HandleFormFields] Field {i}: id={field.id}, title='{field.title}', type={field.fieldType}");
                }

                buffer.Dispose();

                formFields[channelId] = fields;
                Plugin.PluginLog.Info($"[HandleFormFields] Updated cache for channelId={channelId} with {fields.Count} fields");
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleFormFields error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles form submissions response for a channel
        /// </summary>
        public static void HandleFormSubmissions(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int channelId = buffer.ReadInt();
                int submissionCount = buffer.ReadInt();

                var submissions = new List<FormSubmission>();
                for (int i = 0; i < submissionCount; i++)
                {
                    var submission = new FormSubmission
                    {
                        id = buffer.ReadInt(),
                        channelId = buffer.ReadInt(),
                        userId = buffer.ReadInt(),
                        profileId = buffer.ReadInt(),
                        profileName = buffer.ReadString(),
                        submittedAt = DateTimeOffset.FromUnixTimeMilliseconds(buffer.ReadLong()).LocalDateTime,
                        values = new List<FormSubmissionValue>()
                    };

                    int valueCount = buffer.ReadInt();
                    for (int j = 0; j < valueCount; j++)
                    {
                        submission.values.Add(new FormSubmissionValue
                        {
                            fieldId = buffer.ReadInt(),
                            fieldTitle = buffer.ReadString(),
                            value = buffer.ReadString()
                        });
                    }

                    submissions.Add(submission);
                }

                buffer.Dispose();

                formSubmissions[channelId] = submissions;
                Plugin.PluginLog.Debug($"HandleFormSubmissions: channelId={channelId}, submissionCount={submissionCount}");
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleFormSubmissions error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles form submission result
        /// </summary>
        public static void HandleFormSubmitResult(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                formSubmitResultSuccess = buffer.ReadBool();
                formSubmitResultMessage = buffer.ReadString();

                buffer.Dispose();

                Plugin.PluginLog.Debug($"HandleFormSubmitResult: success={formSubmitResultSuccess}, message={formSubmitResultMessage}");
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleFormSubmitResult error: {ex.Message}");
            }
        }

        #endregion

        #region Group Search Handlers

        /// <summary>
        /// Handles public group search results from server
        /// </summary>
        public static void HandlePublicGroupSearchResults(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int count = buffer.ReadInt();
                Plugin.PluginLog.Info($"[HandlePublicGroupSearchResults] Received {count} results");

                var results = new List<GroupSearchResult>();
                for (int i = 0; i < count; i++)
                {
                    var result = new GroupSearchResult
                    {
                        groupID = buffer.ReadInt(),
                        name = buffer.ReadString(),
                        description = buffer.ReadString(),
                        logoUrl = buffer.ReadString(),
                        memberCount = buffer.ReadInt()
                    };
                    results.Add(result);
                    Plugin.PluginLog.Info($"[HandlePublicGroupSearchResults] Result {i}: id={result.groupID}, name='{result.name}', members={result.memberCount}");
                }

                buffer.Dispose();

                groupSearchResults = results;
                groupSearchInProgress = false;
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"HandlePublicGroupSearchResults error: {ex.Message}");
                groupSearchInProgress = false;
            }
        }

        #endregion
    }
}