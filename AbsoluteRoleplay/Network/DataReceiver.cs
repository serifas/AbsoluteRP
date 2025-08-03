

using AbsoluteRoleplay;
using AbsoluteRoleplay.Defines;
using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Windows.Account;
using AbsoluteRoleplay.Windows.Ect;
using AbsoluteRoleplay.Windows.Listings;
using AbsoluteRoleplay.Windows.MainPanel;
using AbsoluteRoleplay.Windows.MainPanel.Views.Account;
using AbsoluteRoleplay.Windows.Moderator;
using AbsoluteRoleplay.Windows.Profiles;
using AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Common.Math;

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
        ReceiveTreeLayout = 79
    }
    class DataReceiver
    {
        public static string restorationStatus = "";
        public static bool LoadedSelf = false;
        public static int BioLoadStatus = -1, HooksLoadStatus = -1, StoryLoadStatus = -1, OOCLoadStatus = -1, GalleryLoadStatus = -1, BookmarkLoadStatus = -1,
                          TargetBioLoadStatus = -1, TargetHooksLoadStatus = -1, TargetStoryLoadStatus = -1, TargetOOCLoadStatus = -1, TargetGalleryLoadStatus = -1, TargetNotesLoadStatus = -1;

        public static RankPermissions permissions { get; set; }
        public static Vector4 accounStatusColor, verificationStatusColor, forgotStatusColor, restorationStatusColor = new Vector4(255, 255, 255, 255);
        public static Plugin plugin;
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
                BookmarksWindow.profileList.Clear();
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
                        BookmarksWindow.profileList.Add(bookmark);
                    }
                    plugin.UpdateStatus();
                    plugin.OpenBookmarksWindow();
                    // Handle the message as needed
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling Bookmark message: {ex}");
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
                    plugin.UpdateStatus();
                    // Handle the message as needed
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling Welcome message: {ex}");
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
                plugin.logger.Error($"Error handling BadLogin message: {ex}");
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
                plugin.logger.Error($"Error handling RecProfileReportSuccessfully message: {ex}");
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
                plugin.logger.Error($"Error handling RecProfileAlreadyReported message: {ex}");
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

                    AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.Story.storyTitle = string.Empty;
                    ProfileWindow.CurrentProfile.GalleryLayouts.Clear();
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
                plugin.logger.Error($"Error handling NoProfile message: {ex}");
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
                    BookmarksWindow.DisableBookmarkSelection = false;
                    ReportWindow.reportStatus = "";

                    plugin.OpenTargetWindow();
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling NoTargetProfile message: {ex}");
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
                plugin.logger.Error($"Error handling ReceiveProfile message: {ex}");
            }
        }


        public static void StatusMessage(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int status = buffer.ReadInt();
                    int rank = buffer.ReadInt();
                    bool announce = buffer.ReadBool();
                    bool suspend = buffer.ReadBool();
                    bool ban = buffer.ReadBool();
                    bool warn = buffer.ReadBool();
                    string message = buffer.ReadString();
                    permissions = new RankPermissions() { can_announce = announce, can_suspend = suspend, can_ban = ban, rank = rank, can_warn = warn };

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
                        plugin.loginAttempted = true;
                    }
                    if (status == (int)UI.StatusMessages.LOGIN_UNVERIFIED)
                    {
                        MainPanel.statusColor = new Vector4(255, 255, 0, 255);
                        MainPanel.status = "Unverified Account";
                        plugin.loginAttempted = true;
                    }
                    if (status == (int)UI.StatusMessages.LOGIN_VERIFIED)
                    {
                        MainPanel.status = "Logged In";
                        MainPanel.statusColor = new Vector4(0, 255, 0, 255);
                        MainPanel.loggedIn = true;
                        plugin.loginAttempted = true;
                        plugin.loggedIn = true;
                    }
                    if (status == (int)UI.StatusMessages.LOGIN_WRONG_INFORMATION)
                    {
                        MainPanel.statusColor = new System.Numerics.Vector4(255, 0, 0, 255);
                        MainPanel.status = "Incorrect login details";
                        plugin.loginAttempted = true;
                    }
                    if (status == (int)UI.StatusMessages.REGISTRATION_SUCCESSFUL)
                    {
                        MainPanel.statusColor = new Vector4(0, 255, 0, 255);
                        MainPanel.status = "Registration Successful";
                    }
                    if (status == (int)UI.StatusMessages.REGISTRATION_DUPLICATE_USERNAME)
                    {
                        MainPanel.statusColor = new Vector4(255, 255, 0, 255);
                        MainPanel.status = "Username already in use.";
                    }

                    if (status == (int)UI.StatusMessages.REGISTRATION_DUPLICATE_EMAIL)
                    {
                        MainPanel.statusColor = new Vector4(255, 255, 0, 255);
                        MainPanel.status = "Email already in use.";
                    }
                    if (status == (int)UI.StatusMessages.LOGIN_WRONG_INFORMATION)
                    {
                        MainPanel.statusColor = new Vector4(255, 255, 0, 255);
                        MainPanel.status = "Incorrect Account Info";
                        MainPanel.loggedIn = false;
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
                    if (status == (int)UI.StatusMessages.PASSCHANGE_INCORRECT_RESTORATION_KEY)
                    {
                        RestorationWindow.restorationCol = new Vector4(255, 0, 0, 255);
                        RestorationWindow.restorationStatus = "Incorrect Key.";
                    }
                    if (status == (int)UI.StatusMessages.PASSCHANGE_PASSWORD_CHANGED)
                    {
                        RestorationWindow.restorationCol = new Vector4(0, 255, 0, 255);
                        RestorationWindow.restorationStatus = "Password updated, you may close this window.";
                    }
                    //Verification window
                    if (status == (int)UI.StatusMessages.VERIFICATION_KEY_VERIFIED)
                    {
                        VerificationWindow.verificationCol = new Vector4(0, 255, 0, 255);
                        VerificationWindow.verificationStatus = "Account Verified! you may now log in.";
                        MainPanel.statusColor = new Vector4(255, 0, 0, 255);
                        MainPanel.status = "Logged Out";
                        MainPanel.login = true;
                        MainPanel.register = false;

                    }
                    if (status == (int)UI.StatusMessages.VERIFICATION_INCORRECT_KEY)
                    {
                        VerificationWindow.verificationCol = new Vector4(255, 0, 0, 255);
                        VerificationWindow.verificationStatus = "Incorrect verification key.";
                    }
                    if (status == (int)UI.StatusMessages.REGISTRATION_INSUFFICIENT_DATA)
                    {
                        MainPanel.statusColor = new Vector4(255, 0, 0, 255);
                        MainPanel.status = "Please fill all fields.";
                    }
                    if (status == (int)UI.StatusMessages.ACCOUNT_WARNING)
                    {
                        ImportantNotice.messageTitle = "Warning";
                        ImportantNotice.moderatorMessage = message;
                        plugin.OpenImportantNoticeWindow();
                    }
                    if (status == (int)UI.StatusMessages.ACCOUNT_STRIKE)
                    {
                        ImportantNotice.messageTitle = "Your account received a strike!";
                        ImportantNotice.moderatorMessage = message;
                        plugin.OpenImportantNoticeWindow();
                    }
                    if (status == (int)UI.StatusMessages.ACCOUNT_SUSPENDED)
                    {
                        plugin.loginAttempted = true;
                        plugin.DisconnectAndLogOut();
                        plugin.username = string.Empty;
                        plugin.password = string.Empty;
                        Login.username = string.Empty;
                        Login.password = string.Empty;
                        plugin.Configuration.username = string.Empty;
                        plugin.Configuration.password = string.Empty;
                        plugin.Configuration.Save();
                        MainPanel.statusColor = new Vector4(255, 0, 0, 255);
                        MainPanel.status = "Account suspended"; ;
                        if (message != string.Empty)
                        {
                            ImportantNotice.messageTitle = "Account Suspended!";
                            ImportantNotice.moderatorMessage = message;
                            plugin.OpenImportantNoticeWindow();
                        }
                    }
                    if (status == (int)UI.StatusMessages.ACCOUNT_BANNED)
                    {
                        plugin.DisconnectAndLogOut();
                        MainPanel.statusColor = new Vector4(255, 0, 0, 255);
                        MainPanel.status = "Account banned";
                        ImportantNotice.messageTitle = "Account Banned!";
                        ImportantNotice.moderatorMessage = message;
                        plugin.OpenImportantNoticeWindow();
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
                plugin.logger.Error($"Error handling StatusMessage message: {ex}");
            }
        }


        public static void ReceiveNoProfileGallery(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    ProfileWindow.CurrentProfile.GalleryLayouts.Clear();
                    GalleryLoadStatus = 0;

                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveNoProfileGallery message: {ex}");
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
                plugin.logger.Error($"Error handling ExistingProfile message: {ex}");
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
                    for (int i = 0; i < profileCount; i++)
                    {

                        int index = buffer.ReadInt();
                        string name = buffer.ReadString();
                        ProfileWindow.profiles.Add(new ProfileData() { index = index, Name = name });
                        Inventory.ProfileBaseData.Add(Tuple.Create(index, name));
                    }
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveProfileHooks message: {ex}");
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
                plugin.logger.Error($"Error handling NoProfileNotes message: {ex}");
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
                plugin.logger.Error($"Error handling RecProfileNotes message: {ex}");
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
                plugin.logger.Error($"Error handling ReceiveNoAuthorization message: {ex}");
            }
        }
        public static void ReceiveVerificationMessage(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    plugin.OpenVerificationWindow();
                    MainPanel.status = "Successfully Registered!";
                    MainPanel.statusColor = new Vector4(0, 255, 0, 255);
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveVerificationMessage message: {ex}");
            }
        }
        public static void ReceivePasswordModificationForm(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    string email = buffer.ReadString();
                    RestorationWindow.restorationEmail = email;
                    plugin.OpenRestorationWindow();
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceivePasswordModificationForm message: {ex}");
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
                    ListingsWindow.percentage = listingCount;
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
                        //   ListingsWindow.listings.Add(listing);
                        ListingsWindow.loading = "Listing: " + i;
                        ListingsWindow.loaderInd = i;
                    }
                    ListingsLoadStatus = 1;
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveNoTargetOOCInfo message: {ex}");
            }
        }
        internal static void ReceiveConnectedPlayers(byte[] data)
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
                        string playerName = buffer.ReadString();
                        string playerWorld = buffer.ReadString();
                        PlayerData playerData = new PlayerData() { playername = playerName, worldname = playerWorld };
                        PlayerInteraction.playerDataMap.Add(playerData);
                    }
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveConnections message: {ex}");
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
                    ConnectionsWindow.connetedProfileList.Clear();
                    ConnectionsWindow.sentProfileRequests.Clear();
                    ConnectionsWindow.receivedProfileRequests.Clear();
                    ConnectionsWindow.blockedProfileRequests.Clear();
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
                                ConnectionsWindow.receivedProfileRequests.Add(requester);
                            }
                            if (status == (int)UI.ConnectionStatus.accepted)
                            {
                                PlayerData playerData = new PlayerData() { playername = requesterName, worldname = requesterWorld };
                                PlayerInteraction.playerDataMap.Add(playerData);
                                ConnectionsWindow.connetedProfileList.Add(requester);
                            }
                            if (status == (int)UI.ConnectionStatus.blocked)
                            {
                                ConnectionsWindow.blockedProfileRequests.Add(requester);
                            }
                            if (status == (int)UI.ConnectionStatus.refused)
                            {
                                if (ConnectionsWindow.receivedProfileRequests.Contains(requester))
                                {
                                    ConnectionsWindow.receivedProfileRequests.Remove(requester);
                                }
                            }
                        }
                        else if (!isReceiver)
                        {
                            if (status == (int)UI.ConnectionStatus.pending)
                            {
                                ConnectionsWindow.sentProfileRequests.Add(receiver);
                            }
                            if (status == (int)UI.ConnectionStatus.accepted)
                            {
                                PlayerData playerData = new PlayerData() { playername = receiverName, worldname = receiverWorld };
                                PlayerInteraction.playerDataMap.Add(playerData);
                                ConnectionsWindow.connetedProfileList.Add(receiver);
                            }
                            if (status == (int)UI.ConnectionStatus.blocked)
                            {
                                ConnectionsWindow.blockedProfileRequests.Add(receiver);
                            }
                            if (status == (int)UI.ConnectionStatus.refused)
                            {
                                //ConnectionsWindow.sentProfileRequests.Add(receiver);
                            }
                        }
                    }

                    plugin.OpenConnectionsWindow();
                    plugin.newConnection = false;
                    plugin.CheckConnectionsRequestStatus();

                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveConnections message: {ex}");
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
                    plugin.newConnection = true;
                    plugin.CheckConnectionsRequestStatus();

                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveConnectionsRequest message: {ex}");
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
                        if (WindowOperations.RenderIconAsync(plugin, iconID) == null)
                        {
                            throw new InvalidOperationException($"Invalid iconID: {iconID}");
                        }
                        Inventory.loaderInd = i;

                    }

                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveProfileItems message: {ex}");
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
                    string message = "The profile you are about to view contains:\n";
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
                plugin.logger.Error($"Error handling RecieveProfileWarning message: {ex}");
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
                    bool self = buffer.ReadBool();
                    if (self)
                    {
                        if (AVATARBYTES == null || AVATARBYTES.Length == 0)
                        {
                            AVATARBYTES = UI.baseAvatarBytes();
                        }
                        if (BACKGROUNDBYTES != null || BACKGROUNDBYTES.Length != 0)
                        {
                            ProfileWindow.backgroundBytes = BACKGROUNDBYTES;
                        }
                        ProfileWindow.backgroundImage = await Plugin.TextureProvider.CreateFromImageAsync(BACKGROUNDBYTES);
                        ProfileWindow.isPrivate = isPrivate;
                        ProfileWindow.activeProfile = isTooltip;
                        ProfileWindow.avatarBytes = AVATARBYTES;
                        ProfileWindow.currentAvatarImg = await Plugin.TextureProvider.CreateFromImageAsync(AVATARBYTES);
                        ProfileWindow.ProfileTitle = NAME;
                        ProfileWindow.color = new Vector4(colX, colY, colZ, colW);
                        ProfileWindow.SpoilerARR = ARR;
                        ProfileWindow.SpoilerHW = HW;
                        ProfileWindow.SpoilerSB = SB;
                        ProfileWindow.SpoilerSHB = SHB;
                        ProfileWindow.SpoilerEW = EW;
                        ProfileWindow.SpoilerDT = DT;
                        ProfileWindow.NSFW = NSFW;
                        ProfileWindow.Sending = false;
                        ProfileWindow.Fetching = false;
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
                        string message = "The profile you are about to view contains:\n";
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
                        if (avatar == null || avatar.ImGuiHandle == IntPtr.Zero)
                        {
                            avatar = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                        }
                        TargetProfileWindow.profileData.avatar = avatar;
                        TargetProfileWindow.profileData.title = NAME.Replace("''", "'");
                        TargetProfileWindow.profileData.titleColor = new Vector4(colX, colY, colZ, colW);
                        IDalamudTextureWrap backgroundImage = await Plugin.TextureProvider.CreateFromImageAsync(BACKGROUNDBYTES);
                        if (backgroundImage == null || backgroundImage.ImGuiHandle == IntPtr.Zero)
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
                plugin.logger.Error($"Error handling ReceiveProfileSettings message: {ex}");
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
                    ARPTooltipWindow.fields.Clear();
                    ARPTooltipWindow.descriptors.Clear();
                    ARPTooltipWindow.personalities.Clear();
                    for (int i = 0; i < customFieldsCount; i++)
                    {
                        string customName = buffer.ReadString();
                        string customDescription = buffer.ReadString();
                        ARPTooltipWindow.fields.Add(
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
                        ARPTooltipWindow.descriptors.Add(new descriptor() { index = i, name = customName, description = customDescription });
                    }
                    for (int i = 0; i < customPersonalitiesCount; i++)
                    {
                        string customName = buffer.ReadString();
                        string customDescription = buffer.ReadString();
                        int customIconID = buffer.ReadInt();
                        IDalamudTextureWrap customIcon = WindowOperations.RenderStatusIconAsync(plugin, customIconID).GetAwaiter().GetResult();
                        if (customIcon == null)
                        {
                            customIcon = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                        }
                        ARPTooltipWindow.personalities.Add(new trait() { index = i, name = customName, description = customDescription, iconID = customIconID, icon = new IconElement { icon = customIcon } });
                    }
                    ProfileData profile = new ProfileData();
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
                    profile.Alignment = Alignment;
                    profile.Personality_1 = Personality_1;
                    profile.Personality_2 = Personality_2;
                    profile.Personality_3 = Personality_3;
                    ARPTooltipWindow.profile = profile;

                    ARPTooltipWindow.AlignmentImg = UI.AlignmentIcon(Alignment);
                    ARPTooltipWindow.personality_1Img = UI.PersonalityIcon(Personality_1);
                    ARPTooltipWindow.personality_2Img = UI.PersonalityIcon(Personality_2);
                    ARPTooltipWindow.personality_3Img = UI.PersonalityIcon(Personality_3);





                    Plugin.tooltipLoaded = true;
                    plugin.OpenARPTooltip();

                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveTooltip message: {ex}");
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
                plugin.logger.Error($"Error handling ReceiveConnectionsRequest message: {ex}");
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
                            // Invalid parent ID, log error
                            plugin.logger.Error($"Node {node.ID} has invalid parent ID {node.ParentID}");
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
                plugin.logger.Error($"Error handling ReceiveDynamicTab message: {ex}");
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
                    ListingsWindow.percentage = listingCount;
                    ListingsWindow.listings.Clear();
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
                                plugin.logger.Error($"Invalid avatar image for profile {profileID}: {ex.Message}");
                                avatar = null;
                            }
                        }

                        // Now skip adding the listing if avatar is null, but all fields have been read
                        if (avatar == null)
                            continue;

                        ListingsWindow.type = 6;
                        ListingsWindow.listings.Add(
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

                        ListingsWindow.loading = "Personal Listing: " + i;
                        ListingsWindow.loaderInd = i;
                    }
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceivePersonalListings message: {ex}");
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
                plugin.logger.Error($"Error handling ReceiveConnectionsRequest message: {ex}");
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
                    }
                    else
                    {
                        tabsTargetCount = tabCount;
                    }
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveConnectionsRequest message: {ex}");
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
                    for (int i = 0; i < tabCount; i++)
                    {
                        int profileID = buffer.ReadInt();
                        string tabName = buffer.ReadString();
                        int tabIndex = buffer.ReadInt();
                        int tabType = buffer.ReadInt();

                      


                    }
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveConnectionsRequest message: {ex}");
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
                         Plugin.plugin.logger.Error(itemDefinition.name);
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
                     Plugin.plugin.logger.Error(requesterProfileName + " is requesting a trade with you.");

                 }
             }
             catch (Exception ex)
             {
                 plugin.logger.Error($"Error handling ReceiveConnectionsRequest message: {ex}");
             }
             finally
             {
                 Plugin.plugin.OpenTradeWindow();
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
                plugin.logger.Error($"Error handling ReceiveConnectionsRequest message: {ex}");
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
                plugin.logger.Error($"Error handling ReceiveConnectionsRequest message: {ex}");
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
                        Plugin.plugin.logger.Error($"Received item for trade: {itemDefinition.name} (Slot: {slot})");
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
                plugin.logger.Error($"Error handling ReceiveTradeUpdate message: {ex}");
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
                    Plugin.plugin.logger.Error($"Trade status updated - Sender: {senderStatus}, Receiver: {receiverStatus}");
                    Plugin.plugin.CloseTradeWindow();
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveTradeUpdate message: {ex}");
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
                plugin.logger.Error($"Error handling ReceiveInventoryTab message: {ex}");
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
                plugin.logger.Error($"Error handling ReceiveTargetOOCInfo message: {ex}");
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
                    Plugin.plugin.logger.Error($"Story Title: {storyTitle}");
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
                plugin.logger.Error($"Error handling ReceiveProfileBio message: {ex}");
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
                        Plugin.plugin.logger.Error($"{name}  {content} {id}");
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
                plugin.logger.Error($"Error handling ReceiveProfileBio message: {ex}");
            }
            finally
            {
                loadedTabsCount += 1;
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
                        string tooltip = buffer.ReadString();
                        bool nsfw = buffer.ReadBool();
                        bool trigger = buffer.ReadBool();
                        ProfileGalleryImage galleryImage = Imaging.DownloadProfileImage(true, url, tooltip, profileID, nsfw, trigger, Plugin.plugin, i).GetAwaiter().GetResult();
                        if (galleryImage.thumbnail == null || galleryImage.thumbnail.ImGuiHandle == IntPtr.Zero)
                        {
                            galleryImage.image = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                        }
                        if (galleryImage.image == null || galleryImage.image.ImGuiHandle == IntPtr.Zero)
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
                plugin.logger.Error($"Error handling ReceiveProfileBio message: {ex}");
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
                            Plugin.plugin.logger.Error($"Path Connection: From ({fromX}, {fromY}) To ({toX}, {toY})");
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
                        rel.IconTexture = WindowOperations.RenderIconAsync(plugin, rel.IconID).GetAwaiter().GetResult();
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
                            Plugin.plugin.logger.Error($"Path Connection: From ({conn.from.x}, {conn.from.y}) To ({conn.to.x}, {conn.to.y})");
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
                plugin.logger.Error($"Error handling ReceiveTreeLayout: {ex}");
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
                        IDalamudTextureWrap customIcon = WindowOperations.RenderStatusIconAsync(plugin, customIconID).GetAwaiter().GetResult();
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
                        loadedTabsCount += 1;
                    }
                    else
                    {
                        EnsureTargetProfileData();
                        TargetProfileWindow.profileData.customTabs.Add(targetTab);
                        loadedTargetTabsCount += 1;
                        if (TargetProfileWindow.profileData.customTabs.Count == 1)
                            TargetProfileWindow.currentLayout = targetTab.Layout as CustomLayout;
                    }
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveProfileBio message: {ex}");
            }
        }
    }
}
