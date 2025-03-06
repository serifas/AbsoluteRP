

using FFXIVClientStructs.FFXIV.Common.Math;
using AbsoluteRoleplay;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AbsoluteRoleplay.Helpers;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Interface.Textures;
using AbsoluteRoleplay.Windows.Profiles;
//using AbsoluteRoleplay.Windows.Chat;
using Dalamud.Interface.Textures.TextureWraps;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.GroupPoseModule;
using AbsoluteRoleplay.Windows.Profiles.ProfileTabs;
using AbsoluteRoleplay.Windows.Listings;
using AbsoluteRoleplay.Windows.Account;
using AbsoluteRoleplay.Windows.Ect;
using AbsoluteRoleplay.Windows.MainPanel;
using AbsoluteRoleplay.Defines;
using AbsoluteRoleplay.Windows.Inventory;
using System.Threading.Tasks;
using Lumina.Excel.Sheets;

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
    }
    class DataReceiver
    {
        public static string restorationStatus = "";
        public static bool LoadedSelf = false;
        public static int BioLoadStatus = -1, HooksLoadStatus = -1, StoryLoadStatus = -1, OOCLoadStatus = -1, GalleryLoadStatus = -1, BookmarkLoadStatus = -1,
                          TargetBioLoadStatus = -1, TargetHooksLoadStatus = -1, TargetStoryLoadStatus = -1, TargetOOCLoadStatus = -1, TargetGalleryLoadStatus = -1, TargetNotesLoadStatus = -1,
                          targetHookEditCount, ExistingGalleryImageCount, ExistingGalleryThumbCount,
                          lawfulGoodEditVal, neutralGoodEditVal, chaoticGoodEditVal,
                          lawfulNeutralEditVal, trueNeutralEditVal, chaoticNeutralEditVal,
                          lawfulEvilEditVal, neutralEvilEditVal, chaoticEvilEditVal;

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

        public static int ListingsLoadStatus { get; internal set; }

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
     
     
        public static void ExistingTargetProfile(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();      
                    string profileName = buffer.ReadString();
                    float colX = buffer.ReadFloat();
                    float colY = buffer.ReadFloat();
                    float colZ = buffer.ReadFloat();
                    float colW = buffer.ReadFloat();
                    string characterName = buffer.ReadString();
                    string characterWorld = buffer.ReadString();
                    bool self = buffer.ReadBool();
                    TargetWindow.self = self;
                    TargetWindow.TitleColor = new Vector4(colX, colY, colZ, colW);
                    TargetWindow.ExistingProfile = true;
                    TargetWindow.ClearUI();
                    ReportWindow.reportStatus = "";
                    TargetWindow.ReloadTarget();
                    TargetWindow.viewBio = true;
                    plugin.OpenTargetWindow();
                    TargetWindow.currentTab = "Bio"; // Set the "Bio" tab as active
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ExistingTargetProfile message: {ex}");
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
                    InventoryWindow.ProfileBaseData.Clear();
                    ProfileWindow.profiles.Clear();
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    loggedIn = true;
                    BioLoadStatus = 0;
                    HooksLoadStatus = 0;
                    StoryLoadStatus = 0;
                    OOCLoadStatus = 0;
                    GalleryLoadStatus = 0;

                    StoryTab.storyTitle = string.Empty;
                    GalleryTab.galleryImageCount = 0; 
                    for (int i = 0; i < GalleryTab.galleryThumbs.Length; i++)
                    {
                        GalleryTab.galleryThumbs[i] = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                    }
                    BookmarkLoadStatus = 0;
                    
                    ProfileWindow.addProfile = false;
                    ProfileWindow.editProfile = false;
                    ProfileWindow.ClearUI();
                    ProfileWindow.ExistingProfile = false;
                    InventoryWindow.ExistingProfile = false;
                    plugin.OpenProfileWindow();
                    ProfileWindow.ExistingProfile = false;
                    InventoryWindow.ExistingProfile = false;
                    
                    ProfileWindow.ClearOnLoad();
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
                    string characterName = buffer.ReadString();
                    string characterWorld = buffer.ReadString();
                    TargetWindow.characterName = characterName;
                    TargetWindow.characterWorld = characterWorld;
                    loggedIn = true;
                    TargetWindow.ExistingProfile = false;
                    TargetWindow.ExistingBio = false;
                    TargetWindow.ExistingHooks = false;
                    TargetWindow.ExistingStory = false;
                    TargetWindow.ExistingOOC = false;
                    TargetWindow.ExistingGallery = false;
                    TargetBioLoadStatus = 0;
                    TargetHooksLoadStatus = 0;
                    TargetStoryLoadStatus = 0;
                    TargetOOCLoadStatus = 0;
                    TargetGalleryLoadStatus = 0;
                    TargetNotesLoadStatus = 0;
                    TargetWindow.ClearUI();
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
        public static void NoTargetGallery(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    loggedIn = true;
                    TargetWindow.ExistingGallery = false;
                    BookmarksWindow.DisableBookmarkSelection = false;
                    TargetGalleryLoadStatus = 0;
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling NoTargetGallery message: {ex}");
            }
        }
        public static void NoTargetStory(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    loggedIn = true;
                    TargetWindow.ExistingStory = false;
                    TargetStoryLoadStatus = 0;
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling NoTargetStory message: {ex}");
            }
        }



        public static void NoProfileBio(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    ProfileWindow.ClearUI();
                    var currentAvatar = UI.UICommonImage(UI.CommonImageTypes.avatarHolder);
                    if (currentAvatar != null)
                    {
                        ProfileWindow.currentAvatarImg = currentAvatar;
                    }

                    BioTab.bioFieldsArr[(int)UI.BioFieldTypes.name] = "";
                    BioTab.bioFieldsArr[(int)UI.BioFieldTypes.race] = "";
                    BioTab.bioFieldsArr[(int)UI.BioFieldTypes.gender] = "";
                    BioTab.bioFieldsArr[(int)UI.BioFieldTypes.age] = "";
                    BioTab.bioFieldsArr[(int)UI.BioFieldTypes.height] = "";
                    BioTab.bioFieldsArr[(int)UI.BioFieldTypes.weight] = "";
                    BioTab.bioFieldsArr[(int)UI.BioFieldTypes.afg] = "";
                    BioTab.currentAlignment = 9;

                    BioTab.currentPersonality_1 = 26;
                    BioTab.currentPersonality_2 = 26;
                    BioTab.currentPersonality_3 = 26;
                    loggedIn = true;
                    BioLoadStatus = 0;
                    ProfileWindow.ClearOnLoad();
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling NoProfileBio message: {ex}");
            }


        }
        public static void NoTargetBio(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    TargetWindow.ExistingBio = false;
                    loggedIn = true;
                    TargetBioLoadStatus = 0;
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling NoTargetBio message: {ex}");
            }
        }
        
        public static void NoTargetHooks(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    TargetWindow.ExistingHooks = false;
                    TargetHooksLoadStatus = 0;
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling NoTargetHooks message: {ex}");
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
                    plugin.OpenProfileWindow();
                    ProfileWindow.ExistingProfile = true;
                    InventoryWindow.ExistingProfile = true;
                    ProfileWindow.ResetOnChangeOrRemoval();
                    ProfileWindow.ClearOnLoad();
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
                    if (status == (int)UI.StatusMessages.NO_AVAILABLE_PROFILE)
                    {
                        AlertWindow.alertColor = new Vector4(255, 0, 0, 255);
                        AlertWindow.alertStatus = "No profile available.";
                        plugin.OpenAlertWindow();
                    }
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling StatusMessage message: {ex}");
            }
        }


        public static void ReceiveTargetGalleryImage(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int imageCount = buffer.ReadInt();
                    int profileID = buffer.ReadInt();
                    TargetWindow.max = imageCount;
                    for (int i = 0; i < imageCount; i++)
                    {
                        string url = buffer.ReadString();
                        string tooltip = buffer.ReadString();
                        bool nsfw = buffer.ReadBool();
                        bool trigger = buffer.ReadBool();
                        Imaging.DownloadProfileImage(false, url, tooltip, profileID, nsfw, trigger, plugin, i);
                        TargetWindow.loading = "Gallery Image" + i;
                        TargetWindow.currentInd = i;
                    }
                    TargetWindow.existingGalleryImageCount = imageCount;
                    TargetWindow.ExistingGallery = true;
                    //BookmarksWindow.DisableBookmarkSelection = false;

                    TargetGalleryLoadStatus = 1;
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveTargetGalleryImage message: {ex}");
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
                    for (int i = 0; i < 30; i++)
                    {
                        var pictureTabImage = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                        GalleryTab.galleryImages[i] = pictureTabImage;
                        GalleryTab.galleryThumbs[i] = pictureTabImage;
                        GalleryTab.imageURLs[i] = string.Empty;
                        GalleryTab.imageTooltips[i] = string.Empty;
                    }
                    GalleryTab.ImageExists[0] = true;
                    GalleryTab.galleryImageCount = 2;
                    GalleryLoadStatus = 0;
                    ProfileWindow.ClearOnLoad();
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveNoProfileGallery message: {ex}");
            }
        }
        public static void ReceiveProfileGalleryImage(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int imageCount = buffer.ReadInt();
                    int profileID = buffer.ReadInt();
                    ProfileWindow.percentage = imageCount;
                    for (int i = 0; i < imageCount; i++)
                    {
                        string url = buffer.ReadString();
                        string tooltip = buffer.ReadString();
                        bool nsfw = buffer.ReadBool();
                        bool trigger = buffer.ReadBool();
                        Imaging.DownloadProfileImage(true, url, tooltip, profileID, nsfw, trigger, plugin, i);
                        GalleryTab.galleryImageCount = i + 1;
                        GalleryTab.ImageExists[i] = true;
                        ProfileWindow.loading = "Loading Gallery Image: " + i;
                        ProfileWindow.loaderInd = i;
                    }

                    GalleryLoadStatus = 1;
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveProfileGalleryImage message: {ex}");
            }

        }
        public static void ReceiveTargetBio(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();

                    int profileID = buffer.ReadInt();
                    string profileTitle = buffer.ReadString();
                    int avatarLen = buffer.ReadInt();
                    byte[] avatarBytes = buffer.ReadBytes(avatarLen);
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
                    int customFieldsCount = buffer.ReadInt();
                    int customDescriptorsCount = buffer.ReadInt();
                    int customPersonalitiesCount = buffer.ReadInt();
                    TargetWindow.fields.Clear();
                    TargetWindow.descriptors.Clear();
                    TargetWindow.personalities.Clear();
                    for (int i = 0; i < customFieldsCount; i++)
                    {
                        string customName = buffer.ReadString();
                        string customDescription = buffer.ReadString();
                        TargetWindow.fields.Add(
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
                        TargetWindow.descriptors.Add(new descriptor() { index = i, name = customName, description = customDescription });
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
                        TargetWindow.personalities.Add(new trait() { index = i, name = customName, description = customDescription, iconID = customIconID, icon = new IconElement { icon = customIcon } });
                    }
                    if (alignment != 9)
                    {
                        TargetWindow.showAlignment = true;
                        TargetWindow.alignment = alignment;
                    }
                    else
                    {
                        TargetWindow.showAlignment = false;
                    }
                    if (personality_1 == 26) { TargetWindow.showPersonality1 = false; } else { TargetWindow.showPersonality1 = true; }
                    if (personality_2 == 26) { TargetWindow.showPersonality2 = false; } else { TargetWindow.showPersonality2 = true; }
                    if (personality_3 == 26) { TargetWindow.showPersonality3 = false; } else { TargetWindow.showPersonality3 = true; }
                    if (personality_1 == 26 && personality_2 == 26 && personality_3 == 26) { TargetWindow.showPersonality = false; }
                    else { TargetWindow.showPersonality = true; }

                    TargetWindow.currentAvatarImg = Plugin.TextureProvider.CreateFromImageAsync(avatarBytes).Result;
                    TargetWindow.Title = profileTitle;
                    TargetWindow.characterEditName = name.Replace("''", "'"); TargetWindow.characterEditRace = race.Replace("''", "'"); TargetWindow.characterEditGender = gender.Replace("''", "'");
                    TargetWindow.characterEditAge = age.Replace("''", "'"); TargetWindow.characterEditHeight = height.Replace("''", "'"); TargetWindow.characterEditWeight = weight.Replace("''", "'");
                    TargetWindow.characterEditAfg = atFirstGlance.Replace("''", "'");
                    var alignmentImage = UI.AlignementIcon(alignment);
                    var personality1Image = UI.PersonalityIcon(personality_1);
                    var personality2Image = UI.PersonalityIcon(personality_2);
                    var personality3Image = UI.PersonalityIcon(personality_3);

                    if (alignmentImage != null) { TargetWindow.alignmentImg = alignmentImage; }
                    if (personality1Image != null) { TargetWindow.personalityImg1 = personality1Image; }
                    if (personality2Image != null) { TargetWindow.personalityImg2 = personality2Image; }
                    if (personality3Image != null) { TargetWindow.personalityImg3 = personality3Image; }

                    var (text, desc) = UI.AlignmentVals[alignment];
                    var (textpers1, descpers1) = UI.PersonalityValues[personality_1];
                    var (textpers2, descpers2) = UI.PersonalityValues[personality_2];
                    var (textpers3, descpers3) = UI.PersonalityValues[personality_3];
                    TargetWindow.alignmentTooltip = text + ": \n" + desc;
                    TargetWindow.personality1Tooltip = textpers1 + ": \n" + descpers1;
                    TargetWindow.personality2Tooltip = textpers2 + ": \n" + descpers2;
                    TargetWindow.personality3Tooltip = textpers3 + ": \n" + descpers3;

                    TargetWindow.existingAvatarBytes = avatarBytes;
                    TargetWindow.ExistingBio = true;
                    TargetBioLoadStatus = 1;
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveTargetBio message: {ex}");
            }
        }
        public static async void ReceiveProfileBio(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int profileID = buffer.ReadInt();
                    int avatarLen = buffer.ReadInt();
                    byte[] avatarBytes = buffer.ReadBytes(avatarLen);
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
                    int customFieldsCount = buffer.ReadInt();
                    int customDescriptorsCount = buffer.ReadInt();
                    int customPersonalitiesCount = buffer.ReadInt();
                    BioTab.fields.Clear();
                    BioTab.descriptors.Clear();
                    BioTab.personalities.Clear();
                    for (int i = 0; i < customFieldsCount; i++)
                    {
                        string customName = buffer.ReadString();
                        string customDescription = buffer.ReadString();
                        BioTab.fields.Add(
                           new field()
                            {
                                index = i,
                                name = customName,
                                description = customDescription
                            });
                    }
                    for(int i = 0; i < customDescriptorsCount; i++)
                    {
                        string customName = buffer.ReadString();
                        string customDescription = buffer.ReadString();
                        BioTab.descriptors.Add(new descriptor() {index=i, name = customName, description = customDescription });
                    }
                    for(int i = 0; i < customPersonalitiesCount; i++)
                    {
                        string customName = buffer.ReadString();
                        string customDescription = buffer.ReadString();
                        int customIconID = buffer.ReadInt();
                        IDalamudTextureWrap customIcon = WindowOperations.RenderStatusIconAsync(plugin, customIconID).GetAwaiter().GetResult();
                        if(customIcon == null)
                        {
                            customIcon = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                        }
                        BioTab.personalities.Add(new trait() { index =i, name = customName, description = customDescription, iconID=customIconID, icon = new IconElement { icon = customIcon } });
                    }



                    ProfileWindow.currentAvatarImg = Plugin.TextureProvider.CreateFromImageAsync(avatarBytes).Result;
                    BioTab.avatarBytes = avatarBytes;
                    BioTab.bioFieldsArr[(int)UI.BioFieldTypes.name] = name.Replace("''", "'");
                    BioTab.bioFieldsArr[(int)UI.BioFieldTypes.race] = race.Replace("''", "'");
                    BioTab.bioFieldsArr[(int)UI.BioFieldTypes.gender] = gender.Replace("''", "'");
                    BioTab.bioFieldsArr[(int)UI.BioFieldTypes.age] = age.ToString().Replace("''", "'");
                    BioTab.bioFieldsArr[(int)UI.BioFieldTypes.height] = height.Replace("''", "'");
                    BioTab.bioFieldsArr[(int)UI.BioFieldTypes.weight] = weight.Replace("''", "'");
                    BioTab.bioFieldsArr[(int)UI.BioFieldTypes.afg] = atFirstGlance.Replace("''", "'");
                    BioTab.currentAlignment = alignment;

                    BioTab.currentPersonality_1 = personality_1;
                    BioTab.currentPersonality_2 = personality_2;
                    BioTab.currentPersonality_3 = personality_3;

                    BioLoadStatus = 1;
                    ProfileWindow.ClearOnLoad();
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveProfileBio message: {ex}");
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
                    bool status = buffer.ReadBool();
                    bool tooltipStatus = buffer.ReadBool();
                    ProfileWindow.isPrivate = status;
                    ProfileWindow.activeProfile = tooltipStatus;
                    ProfileWindow.ExistingProfile = true;
                    InventoryWindow.ExistingProfile = true;
                    ProfileWindow.ClearOnLoad();

                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ExistingProfile message: {ex}");
            }

        }
        public static void ReceiveProfileHooks(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int hookCount = buffer.ReadInt();

                    for (int i = 0; i < hookCount; i++)
                    {
                        string hookName = buffer.ReadString();
                        string hookContent = buffer.ReadString();
                        HooksTab.hookExists[i] = true;
                        HooksTab.HookNames[i] = hookName;
                        HooksTab.HookContents[i] = hookContent;

                    }
                    HooksTab.hookCount = hookCount;
                    HooksLoadStatus = 1;
                    ProfileWindow.ClearOnLoad();
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveProfileHooks message: {ex}");
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
                    InventoryWindow.ProfileBaseData.Clear();
                    for (int i =0; i < profileCount; i++)
                    {
                        
                        int index = buffer.ReadInt();
                        string name = buffer.ReadString();
                        bool active = buffer.ReadBool();
                        ProfileWindow.profiles.Add(new PlayerProfile(){index = index, Name = name, isActive= active});
                        InventoryWindow.ProfileBaseData.Add(Tuple.Create(index, name, active));
                    }
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveProfileHooks message: {ex}");
            }
        }
        public static void ReceiveProfileStory(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int chapterCount = buffer.ReadInt();
                    string storyTitle = buffer.ReadString();
                    StoryTab.ResetStory();
                    StoryTab.storyTitle = storyTitle;
                    for (int i = 0; i < chapterCount; i++)
                    {
                        string chapterName = buffer.ReadString();
                        string chapterContent = buffer.ReadString();
                        StoryTab.storyChapterCount = i;
                        StoryTab.ChapterNames[i] = chapterName;
                        StoryTab.ChapterContents[i] = chapterContent;
                        StoryTab.storyChapterExists[i] = true;
                    }
                    StoryLoadStatus = 1;
                    ProfileWindow.ClearOnLoad();
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveProfileStory message: {ex}");
            }
        }

        public static void ReceiveTargetStory(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int chapterCount = buffer.ReadInt();
                    string storyTitle = buffer.ReadString();
                    TargetWindow.ExistingStory = true;
                    TargetWindow.storyTitle = storyTitle;
                    for (int i = 0; i < chapterCount; i++)
                    {
                        string chapterName = buffer.ReadString();
                        string chapterContent = buffer.ReadString();
                        TargetWindow.chapterCount = i + 1;
                        TargetWindow.ChapterTitle[i] = chapterName;
                        TargetWindow.ChapterContent[i] = chapterContent;
                        TargetWindow.ChapterExists[i] = true;
                    }
                    TargetStoryLoadStatus = 1;
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveTargetStory message: {ex}");
            }
        }
        public static void ReceiveTargetHooks(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int hookCount = buffer.ReadInt();
                    TargetWindow.ExistingHooks = true;

                    TargetWindow.hookEditCount = hookCount;
                    for (int i = 0; i < hookCount; i++)
                    {
                        string hookName = buffer.ReadString();
                        string hookContent = buffer.ReadString();
                        TargetWindow.HookNames[i] = hookName;
                        TargetWindow.HookContents[i] = hookContent;
                    }
                    TargetHooksLoadStatus = 1;
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveTargetHooks message: {ex}");
            }
        }
        public static void NoProfileHooks(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    HooksTab.hookCount = 0;
                    for (int i = 0; i < HooksTab.HookContents.Length; i++)
                    {
                        HooksTab.HookContents[i] = string.Empty;
                    }
                    for (int f = 0; f < HooksTab.HookNames.Length; f++)
                    {
                        HooksTab.HookNames[f] = string.Empty;
                    }
                    HooksLoadStatus = 0;
                    ProfileWindow.ClearOnLoad();
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling NoProfileHooks message: {ex}");
            }
        }
        public static void NoProfileStory(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    StoryTab.storyTitle = string.Empty;
                    for (int i = 0; i < StoryTab.ChapterNames.Count(); i++)
                    {
                        StoryTab.ChapterNames[i] = string.Empty;
                        StoryTab.ChapterContents[i] = string.Empty;
                    }
                    StoryLoadStatus = 0;
                    ProfileWindow.ClearOnLoad();
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling NoProfileStory message: {ex}");
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
        public static void ReceiveProfileOOC(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    string ooc = buffer.ReadString();
                    ProfileWindow.oocInfo = ooc;
                    OOCLoadStatus = 1;
                    ProfileWindow.ClearOnLoad();
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveProfileOOC message: {ex}");
            }
        }
        public static void ReceiveNoOOCInfo(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    ProfileWindow.oocInfo = string.Empty;
                    ProfileWindow.ClearOnLoad();
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveNoOOCInfo message: {ex}");
            }
        }
        public static void ReceiveTargetOOCInfo(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    string ooc = buffer.ReadString();
                    TargetWindow.oocInfo = ooc;
                    TargetWindow.ExistingOOC = true;
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveTargetOOCInfo message: {ex}");
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
        public static void ReceiveNoTargetOOCInfo(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    TargetWindow.oocInfo = string.Empty;
                    TargetWindow.ExistingOOC = false;
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
                        string playerName = Plugin.ClientState.LocalPlayer.Name.ToString();
                        string playerWorld = Plugin.ClientState.LocalPlayer.HomeWorld.Value.Name.ToString();
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

                    InventoryWindow.percentage = itemsCount;
                    for (int i = 0; i < itemsCount; i++)
                    {

                        string name = buffer.ReadString();
                        string description = buffer.ReadString();
                        int type = buffer.ReadInt();
                        int subType = buffer.ReadInt();
                        int iconID = buffer.ReadInt(); 
                        int slotID = buffer.ReadInt();
                        int quality = buffer.ReadInt();
                        InvTab.inventorySlotContents[type][slotID] = new ItemDefinition
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
                        InventoryWindow.loaderInd = i;

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
                    if (NSFW || TRIGGERING)
                    {

                    }
                    if (spoilers.Count > 0)
                    {
                        message += "Spoilers from the expansions \n";
                    }
                    for (int i = 0; i < spoilers.Count; i++)
                    {
                        message += spoilers[i] + "\n";
                    }
                    TargetWindow.warning = true;
                    TargetWindow.warningMessage = message;
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling RecieveProfileWarning message: {ex}");
            }
        }

        internal static void ReceiveProfileSettings(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    string NAME = buffer.ReadString();
                    float colX = buffer.ReadFloat();
                    float colY = buffer.ReadFloat();
                    float colZ = buffer.ReadFloat();
                    float colW = buffer.ReadFloat();
                    bool ARR = buffer.ReadBool();
                    bool HW = buffer.ReadBool();
                    bool SB = buffer.ReadBool();
                    bool SHB = buffer.ReadBool();
                    bool EW = buffer.ReadBool();
                    bool DT = buffer.ReadBool();
                    bool NSFW = buffer.ReadBool();
                    bool TRIGGERING = buffer.ReadBool();

                    ProfileWindow.ProfileTitle = NAME;
                    ProfileWindow.color = new Vector4(colX, colY, colZ, colW);
                    ProfileWindow.SpoilerARR = ARR;
                    ProfileWindow.SpoilerHW = HW;
                    ProfileWindow.SpoilerSB = SB;
                    ProfileWindow.SpoilerSHB = SHB;
                    ProfileWindow.SpoilerEW = EW;
                    ProfileWindow.SpoilerDT = DT;
                    ProfileWindow.NSFW = NSFW;
                    ProfileWindow.Triggering = TRIGGERING;
                    
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling RecieveProfileWarning message: {ex}");
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
                    PlayerProfile profile = new PlayerProfile();
                    profile.avatar = Plugin.TextureProvider.CreateFromImageAsync(avatarBytes).Result;
                    profile.title = title;
                    profile.titleColor = new Vector4(colX, colY, colZ, colW);
                    profile.Name = Name.Replace("''", "'");
                    profile.Race = Race.Replace("''", "'");
                    profile.Gender = Gender.Replace("''", "'");
                    profile.Age = Age.Replace("''", "'");
                    profile.Height = Height.Replace("''", "'");
                    profile.Weight = Weight.Replace("''", "'");
                    if(Alignment != 9)
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
                    if(Personality_1 == 26 && Personality_2 == 26 && Personality_3 == 26)
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

                    ARPTooltipWindow.AlignmentImg = UI.AlignementIcon(Alignment);
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
                    int id = buffer.ReadInt();
                    string Name = buffer.ReadString();
                    string World = buffer.ReadString();
                    string profileName = buffer.ReadString();
                    int avatarBytesLen = buffer.ReadInt();
                    byte[] avatarBytes = buffer.ReadBytes(avatarBytesLen);
                    string message = buffer.ReadString();
                    IDalamudTextureWrap avatar =  Plugin.TextureProvider.CreateFromImageAsync(avatarBytes).Result;
                    ARPChatWindow.messages.Add(new ChatMessage { author=id, name=Name, world=World, authorName = profileName, avatar = avatar, message = message });

                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveConnectionsRequest message: {ex}");
            }
        }


        //SYNC

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

                    for(int i = 0; i < modDataCount; i++)
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


    }
}
