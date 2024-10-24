

using FFXIVClientStructs.FFXIV.Common.Math;
using AbsoluteRoleplay;
using AbsoluteRoleplay.Windows;
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
using static AbsoluteRoleplay.Defines;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.GroupPoseModule;

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

        public static void RecBookmarks(byte[] data)
        {
            var buffer = new ByteBuffer();
            buffer.WriteBytes(data);
            var packetID = buffer.ReadInt();
            string bookmarkVals = buffer.ReadString();

            plugin.OpenBookmarksWindow();

            Regex nameRx = new Regex(@"<bookmarkName>(.*?)</bookmarkName>");
            Regex worldRx = new Regex(@"<bookmarkWorld>(.*?)</bookmarkWorld>");
            string[] bookmarkSplit = bookmarkVals.Replace("|||", "~").Split('~');
            BookmarksWindow.profiles.Clear();
            for (int i = 0; i < bookmarkSplit.Count(); i++)
            {
                string characterName = nameRx.Match(bookmarkSplit[i]).Groups[1].Value;
                string characterWorld = worldRx.Match(bookmarkSplit[i]).Groups[1].Value;

                BookmarksWindow.profiles.Add(characterName, characterWorld);
            }
            BookmarkLoadStatus = 1;

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
                    TargetWindow.ExistingProfile = true;
                    TargetWindow.ClearUI();
                    ReportWindow.reportStatus = "";
                    TargetWindow.ReloadTarget();
                    TargetWindow.viewBio = true;
                    plugin.OpenTargetWindow();
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
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    loggedIn = true;
                    BioLoadStatus = 0;
                    HooksLoadStatus = 0;
                    StoryLoadStatus = 0;
                    OOCLoadStatus = 0;
                    GalleryLoadStatus = 0;
                    ProfileWindow.storyTitle = string.Empty;
                    ProfileWindow.galleryImageCount = 0; 
                    for (int i = 0; i < ProfileWindow.galleryThumbs.Length; i++)
                    {
                        ProfileWindow.galleryThumbs[i] = Defines.UICommonImage(CommonImageTypes.blankPictureTab);
                    }
                    BookmarkLoadStatus = 0;
                    ProfileWindow.addProfile = false;
                    ProfileWindow.editProfile = false;
                    ProfileWindow.ClearUI();
                    plugin.OpenProfileWindow();
                    ProfileWindow.ExistingProfile = false;
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
                    var currentAvatar = Defines.UICommonImage(Defines.CommonImageTypes.avatarHolder);
                    if (currentAvatar != null)
                    {
                        ProfileWindow.currentAvatarImg = currentAvatar;
                    }

                    ProfileWindow.bioFieldsArr[(int)Defines.BioFieldTypes.name] = "";
                    ProfileWindow.bioFieldsArr[(int)Defines.BioFieldTypes.race] = "";
                    ProfileWindow.bioFieldsArr[(int)Defines.BioFieldTypes.gender] = "";
                    ProfileWindow.bioFieldsArr[(int)Defines.BioFieldTypes.age] = "";
                    ProfileWindow.bioFieldsArr[(int)Defines.BioFieldTypes.height] = "";
                    ProfileWindow.bioFieldsArr[(int)Defines.BioFieldTypes.weight] = "";
                    ProfileWindow.bioFieldsArr[(int)Defines.BioFieldTypes.afg] = "";
                    ProfileWindow.currentAlignment = 0;

                    ProfileWindow.currentPersonality_1 = 0;
                    ProfileWindow.currentPersonality_2 = 0;
                    ProfileWindow.currentPersonality_3 = 0;
                    loggedIn = true;
                    ProfileWindow.ExistingBio = false;
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
                    string username = buffer.ReadString();
                    int status = buffer.ReadInt();
                    plugin.username = username;
                    //account window
                    if (status == (int)Defines.StatusMessages.LOGIN_BANNED)
                    {
                        MainPanel.statusColor = new Vector4(255, 0, 0, 255);
                        MainPanel.status = "Account Banned";
                    }
                    if (status == (int)Defines.StatusMessages.LOGIN_UNVERIFIED)
                    {
                        plugin.loggedIn = false;
                        MainPanel.statusColor = new Vector4(255, 255, 0, 255);
                        MainPanel.status = "Unverified Account";
                    }
                    if (status == (int)Defines.StatusMessages.LOGIN_VERIFIED)
                    {
                        plugin.loggedIn = true;
                        MainPanel.status = "Logged In";
                        MainPanel.statusColor = new Vector4(0, 255, 0, 255);
                        MainPanel.viewMainWindow = true;
                    }
                    if (status == (int)Defines.StatusMessages.LOGIN_WRONG_INFORMATION)
                    {
                        MainPanel.statusColor = new System.Numerics.Vector4(255, 0, 0, 255);
                        MainPanel.status = "Incorrect login details";
                    }
                    if (status == (int)Defines.StatusMessages.REGISTRATION_DUPLICATE_USERNAME)
                    {
                        MainPanel.statusColor = new Vector4(255, 255, 0, 255);
                        MainPanel.status = "Username already in use.";
                    }

                    if (status == (int)Defines.StatusMessages.REGISTRATION_DUPLICATE_EMAIL)
                    {
                        MainPanel.statusColor = new Vector4(255, 255, 0, 255);
                        MainPanel.status = "Email already in use.";
                    }
                    if (status == (int)Defines.StatusMessages.LOGIN_WRONG_INFORMATION)
                    {
                        MainPanel.statusColor = new Vector4(255, 255, 0, 255);
                        MainPanel.status = "Incorrect Account Info";
                        MainPanel.viewMainWindow = false;
                    }
                    if (status == (int)Defines.StatusMessages.FORGOT_REQUEST_RECEIVED)
                    {
                        MainPanel.statusColor = new Vector4(0, 255, 0, 255);
                        MainPanel.status = "Request received, please stand by...";
                    }
                    if (status == (int)Defines.StatusMessages.FORGOT_REQUEST_INCORRECT)
                    {
                        MainPanel.statusColor = new Vector4(255, 255, 0, 255);
                        MainPanel.status = "There is no account with this email.";
                    }
                    //Restoration window
                    if (status == (int)Defines.StatusMessages.PASSCHANGE_INCORRECT_RESTORATION_KEY)
                    {
                        RestorationWindow.restorationCol = new Vector4(255, 0, 0, 255);
                        RestorationWindow.restorationStatus = "Incorrect Key.";
                    }
                    if (status == (int)Defines.StatusMessages.PASSCHANGE_PASSWORD_CHANGED)
                    {
                        RestorationWindow.restorationCol = new Vector4(0, 255, 0, 255);
                        RestorationWindow.restorationStatus = "Password updated, you may close this window.";
                    }
                    //Verification window
                    if (status == (int)Defines.StatusMessages.VERIFICATION_KEY_VERIFIED)
                    {
                        VerificationWindow.verificationCol = new Vector4(0, 255, 0, 255);
                        VerificationWindow.verificationStatus = "Account Verified! you may now log in.";
                        MainPanel.statusColor = new Vector4(255, 0, 0, 255);
                        MainPanel.status = "Logged Out";
                        MainPanel.login = true;
                        MainPanel.register = false;

                    }
                    if (status == (int)Defines.StatusMessages.VERIFICATION_INCORRECT_KEY)
                    {
                        VerificationWindow.verificationCol = new Vector4(255, 0, 0, 255);
                        VerificationWindow.verificationStatus = "Incorrect verification key.";
                    }
                    if (status == (int)Defines.StatusMessages.REGISTRATION_INSUFFICIENT_DATA)
                    {
                        MainPanel.statusColor = new Vector4(255, 0, 0, 255);
                        MainPanel.status = "Please fill all fields.";
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
                        bool nsfw = buffer.ReadBool();
                        bool trigger = buffer.ReadBool();
                        Imaging.DownloadProfileImage(false, url, profileID, nsfw, trigger, plugin, i);
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
                        ProfileWindow.galleryImages[i] = ProfileWindow.pictureTab;
                        ProfileWindow.imageURLs[i] = string.Empty;
                    }
                    ProfileWindow.ImageExists[0] = true;
                    ProfileWindow.galleryImageCount = 2;
                    GalleryLoadStatus = 0;
                    ProfileWindow.ExistingGallery = false;
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
                        bool nsfw = buffer.ReadBool();
                        bool trigger = buffer.ReadBool();
                        Imaging.DownloadProfileImage(true, url, profileID, nsfw, trigger, plugin, i);
                        ProfileWindow.galleryImageCount = i + 1;
                        ProfileWindow.ImageExists[i] = true;
                        ProfileWindow.loading = "Gallery Image: " + i;
                        ProfileWindow.loaderInd = i;
                    }
                    ProfileWindow.ExistingGallery = true;
                    ProfileWindow.ClearOnLoad();

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

                    if (alignment != 9)
                    {
                        TargetWindow.showAlignment = true;
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
                    TargetWindow.characterEditName = name.Replace("''", "'"); TargetWindow.characterEditRace = race.Replace("''", "'"); TargetWindow.characterEditGender = gender.Replace("''", "'");
                    TargetWindow.characterEditAge = age.Replace("''", "'"); TargetWindow.characterEditHeight = height.Replace("''", "'"); TargetWindow.characterEditWeight = weight.Replace("''", "'");
                    TargetWindow.characterEditAfg = atFirstGlance.Replace("''", "'");
                    var alignmentImage = Defines.AlignementIcon(alignment);
                    var personality1Image = Defines.PersonalityIcon(personality_1);
                    var personality2Image = Defines.PersonalityIcon(personality_2);
                    var personality3Image = Defines.PersonalityIcon(personality_3);

                    if (alignmentImage != null) { TargetWindow.alignmentImg = alignmentImage; }
                    if (personality1Image != null) { TargetWindow.personalityImg1 = personality1Image; }
                    if (personality2Image != null) { TargetWindow.personalityImg2 = personality2Image; }
                    if (personality3Image != null) { TargetWindow.personalityImg3 = personality3Image; }

                    var (text, desc) = Defines.AlignmentVals[alignment];
                    var (textpers1, descpers1) = Defines.PersonalityValues[personality_1];
                    var (textpers2, descpers2) = Defines.PersonalityValues[personality_2];
                    var (textpers3, descpers3) = Defines.PersonalityValues[personality_3];
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
        public static void ReceiveProfileBio(byte[] data)
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
                    ProfileWindow.ExistingBio = true;

                    ProfileWindow.currentAvatarImg = Plugin.TextureProvider.CreateFromImageAsync(avatarBytes).Result;
                    ProfileWindow.avatarBytes = avatarBytes;
                    if (alignment == 9)
                    {
                        ProfileWindow.alignmentHidden = true;
                    }
                    else
                    {
                        ProfileWindow.alignmentHidden = false;
                    }
                    if (personality_1 == 26 && personality_2 == 26 && personality_3 == 26)
                    {
                        ProfileWindow.personalityHidden = true;
                    }
                    else
                    {
                        ProfileWindow.personalityHidden = false;
                    }
                    ProfileWindow.bioFieldsArr[(int)Defines.BioFieldTypes.name] = name.Replace("''", "'");
                    ProfileWindow.bioFieldsArr[(int)Defines.BioFieldTypes.race] = race.Replace("''", "'");
                    ProfileWindow.bioFieldsArr[(int)Defines.BioFieldTypes.gender] = gender.Replace("''", "'");
                    ProfileWindow.bioFieldsArr[(int)Defines.BioFieldTypes.age] = age.ToString().Replace("''", "'");
                    ProfileWindow.bioFieldsArr[(int)Defines.BioFieldTypes.height] = height.Replace("''", "'");
                    ProfileWindow.bioFieldsArr[(int)Defines.BioFieldTypes.weight] = weight.Replace("''", "'");
                    ProfileWindow.bioFieldsArr[(int)Defines.BioFieldTypes.afg] = atFirstGlance.Replace("''", "'");
                    ProfileWindow.currentAlignment = alignment;

                    ProfileWindow.currentPersonality_1 = personality_1;
                    ProfileWindow.currentPersonality_2 = personality_2;
                    ProfileWindow.currentPersonality_3 = personality_3;

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
                    plugin.OpenProfileWindow();
                    ProfileWindow.privateProfile = status;
                    ProfileWindow.ExistingProfile = true;
                    ProfileWindow.ReloadProfile();
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
                    ProfileWindow.ExistingHooks = true;

                    for (int i = 0; i < hookCount; i++)
                    {
                        string hookName = buffer.ReadString();
                        string hookContent = buffer.ReadString();
                        ProfileWindow.hookExists[i] = true;
                        ProfileWindow.HookNames[i] = hookName;
                        ProfileWindow.HookContents[i] = hookContent;

                    }
                    ProfileWindow.hookCount = hookCount;
                    HooksLoadStatus = 1;
                    ProfileWindow.ClearOnLoad();
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
                    ProfileWindow.ResetStory();
                    ProfileWindow.ExistingStory = true;
                    ProfileWindow.storyTitle = storyTitle;
                    for (int i = 0; i < chapterCount; i++)
                    {
                        string chapterName = buffer.ReadString();
                        string chapterContent = buffer.ReadString();
                        ProfileWindow.storyChapterCount = i;
                        ProfileWindow.ChapterNames[i] = chapterName;
                        ProfileWindow.ChapterContents[i] = chapterContent;
                        ProfileWindow.storyChapterExists[i] = true;
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
                    ProfileWindow.ExistingHooks = false;
                    ProfileWindow.hookCount = 0;
                    for (int i = 0; i < ProfileWindow.HookContents.Length; i++)
                    {
                        ProfileWindow.HookContents[i] = string.Empty;
                    }
                    for (int f = 0; f < ProfileWindow.HookNames.Length; f++)
                    {
                        ProfileWindow.HookNames[f] = string.Empty;
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
                    ProfileWindow.ExistingStory = false;
                    for (int i = 0; i < ProfileWindow.ChapterNames.Count(); i++)
                    {
                        ProfileWindow.ChapterNames[i] = string.Empty;
                        ProfileWindow.ChapterContents[i] = string.Empty;
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
                    ProfileWindow.ExistingOOC = true;
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
                    ProfileWindow.ExistingOOC = false;
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
                        string playerWorld = Plugin.ClientState.LocalPlayer.HomeWorld.GameData.Name.ToString();
                        Tuple<string, string> requester = Tuple.Create(requesterName, requesterWorld);
                        Tuple<string, string> receiver = Tuple.Create(receiverName, receiverWorld);
                        if (isReceiver)
                        {
                            if (status == (int)Defines.ConnectionStatus.pending)
                            {
                                ConnectionsWindow.receivedProfileRequests.Add(requester);
                            }
                            if (status == (int)Defines.ConnectionStatus.accepted)
                            {
                                ConnectionsWindow.connetedProfileList.Add(requester);
                            }
                            if (status == (int)Defines.ConnectionStatus.blocked)
                            {
                                ConnectionsWindow.blockedProfileRequests.Add(requester);
                            }
                            if (status == (int)Defines.ConnectionStatus.refused)
                            {
                                if (ConnectionsWindow.receivedProfileRequests.Contains(requester))
                                {
                                    ConnectionsWindow.receivedProfileRequests.Remove(requester);
                                }
                            }
                        }
                        else if (!isReceiver)
                        {
                            if (status == (int)Defines.ConnectionStatus.pending)
                            {
                                ConnectionsWindow.sentProfileRequests.Add(receiver);
                            }
                            if (status == (int)Defines.ConnectionStatus.accepted)
                            {
                                ConnectionsWindow.connetedProfileList.Add(receiver);
                            }
                            if (status == (int)Defines.ConnectionStatus.blocked)
                            {
                                ConnectionsWindow.blockedProfileRequests.Add(receiver);
                            }
                            if (status == (int)Defines.ConnectionStatus.refused)
                            {
                                ConnectionsWindow.sentProfileRequests.Add(receiver);
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

        internal static void ReceiveTargetTooltip(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
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

                    PlayerProfile profile = new PlayerProfile();
                    profile.avatar = Plugin.TextureProvider.CreateFromImageAsync(avatarBytes).Result;
                    profile.Name = Name.Replace("''", "'");
                    profile.Race = Race.Replace("''", "'");
                    profile.Gender = Gender.Replace("''", "'");
                    profile.Age = Age.Replace("''", "'");
                    profile.Height = Height.Replace("''", "'");
                    profile.Weight = Weight.Replace("''", "'");
                    profile.Alignment = Alignment;
                    profile.Personality_1 = Personality_1;
                    profile.Personality_2 = Personality_2;
                    profile.Personality_3 = Personality_3;
                    ARPTooltipWindow.profile = profile;

                    ARPTooltipWindow.AlignmentImg = Defines.AlignementIcon(Alignment);
                    ARPTooltipWindow.personality_1Img = Defines.PersonalityIcon(Personality_1);
                    ARPTooltipWindow.personality_2Img = Defines.PersonalityIcon(Personality_2);
                    ARPTooltipWindow.personality_3Img = Defines.PersonalityIcon(Personality_3);

                    Plugin.tooltipLoaded = true;
                    plugin.OpenARPTooltip();

                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error handling ReceiveTooltip message: {ex}");
            }
        }
 


    }
}
