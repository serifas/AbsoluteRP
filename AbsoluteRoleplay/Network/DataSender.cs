using AbsoluteRP;
using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Moderator;
using AbsoluteRP.Windows.Profiles;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Social.Views;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Numerics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Lumina.Data.Parsing.Layer.LayerCommon;

namespace Networking
{  // Add type aliases at the top of the namespace to resolve ambiguity

    public enum ClientPackets
    {
        SendUserTagCreation = 1,
        CLogin = 2,
        CCreateProfile = 3,
        CFetchProfile = 4,
        CSendNewSystem = 5,
        CSendRulebookPageContent = 6,
        CSendRulebookPage = 7,
        CSendSheetVerify = 8,
        CSendSystemStats = 9,
        CCreateProfileBio = 10,
        CBanAccount = 11,
        CStrikeAccount = 12,
        CEditProfileBio = 13,
        SendProfileDetails = 14,
        SRequestTargetProfile = 15,
        CRegister = 16,
        CDeleteHook = 17,
        CSendStory = 18,
        CSendLocation = 19,
        CSendBookmarkRequest = 20,
        CSendPlayerBookmark = 21,
        CSendRemovePlayerBookmark = 22,
        CSendGalleryImage = 23,
        CSendGalleryImagesReceived = 24,
        CSendGalleryImageRequest = 25,
        CSendGalleryRemoveRequest = 26,
        CReorderGallery = 27,
        CSendNSFWStatus = 28,
        CSendGallery = 29,
        CReportProfile = 30,
        CSendProfileNotes = 31,
        SSubmitVerificationKey = 32,
        SSubmitRestorationRequest = 33,
        SSubmitRestorationKey = 34,
        SSendInfo = 35,
        SSendUserConfiguration = 36,
        SendProfileConfiguration = 37,
        SSendProfileViewRequest = 38,
        SSendProfileAccessUpdate = 39,
        SSendConnectionsRequest = 40,
        SSendProfileStatus = 41,
        SSendChatMessage = 42,
        CreateGroup = 43,
        SRequestTooltip = 44,
        SDeleteProfile = 45,
        SCreateListing = 46,
        SRequestListing = 47,
        SFetchProfiles = 48,
        RenameProfile = 49,
        RequestTargetProfileByCharacter = 50,
        SetAsTooltip = 51,
        Logout = 52,
        PreviewProfile = 53,
        CreateItem = 54,
        SortItems = 55,
        FetchProfileItems = 56,
        SendCustomLayouts = 58,
        RequestOwnedListings = 59,
        CSendTreeData = 60,
        SendModeratorAction = 61,
        SendPersonalsRequest = 62,
        CreateTab = 63,
        CreateBio = 64,
        DeleteTab = 65,
        SendDynamicTab = 66,
        SendTradeRequest = 67,
        SendTradeUpdate = 68,
        SendTradeStatus = 69,
        SInventorySelection = 70,
        SendItemDeletion = 71,
        SendTradeSessionTargetInventory = 72,
        SendTreeLayout = 73,
        SendInventoryLayout = 74,
        SendTabReorder = 75,
        SendCompassRequest = 76,
        SendLodestoneURL = 77,
        SendCheckLodestoneEntry = 78,
        UnlinkAccount = 79,
        SetFauxNameStatus = 80,
        SetCompassStatus = 81,
        SaveGroup = 83,
        FetchGroups = 84,
        SendGroupChatMessage = 85,
        FetchGroupChatMessages = 86,
        UpdateChatReadStatus = 87,
        FetchGroupCategories = 88,
        SaveGroupCategories = 89,
        SaveGroupRosterFields = 90,
        FetchGroupRosterFields = 91,
        SaveMemberMetadata = 92,
        FetchMemberMetadata = 93,
        SaveMemberFieldValues = 94,
        FetchMemberFieldValues = 95,
        DeleteGroupChatMessage = 96,
        EditGroupChatMessage = 97,
        SendGroupInvite = 98,
        FetchGroupInvites = 99,
        RespondToGroupInvite = 100,
        CancelGroupInvite = 101,
        FetchGroupMembers = 102,
        SaveForumStructure = 103,
        FetchForumStructure = 104,
        SaveForumPermissions = 105,
        FetchForumPermissions = 106,
        ViewInviteeProfile = 107,
        FetchGroupRanks = 108,
        SaveGroupRank = 109,
        DeleteGroupRank = 110,
        UpdateRankHierarchies = 111,
        AssignMemberRank = 112,
        RemoveMemberRank = 113,
        KickGroupMember = 114,
        BanGroupMember = 115,
        DeleteGroup = 116,
        LeaveGroup = 117,
        TransferGroupOwnership = 118,
        FetchGroupMemberAvatar = 119,
        RenameCategory = 120,
        DeleteCategory = 121,
        RenameChannel = 122,
        DeleteChannel = 123,
        MoveChannel = 124,
        ReorderChannel = 125,
        CreateCategory = 126,
        CreateChannel = 127,
        LikeProfile = 128,
        FetchLikesRemaining = 129,
        FetchProfileLikeCounts = 130,
        FetchProfileLikes = 131,
        CreateChannelWithPermissions = 132,
        RemoveSpecificMemberRank = 133,
        PinGroupChatMessage = 134,
        FetchPinnedMessages = 135,
        LockChannel = 136,
        UnbanGroupMember = 137,
        ReorderCategory = 138,
        // Rules Channel & Self-Assign Roles
        SaveGroupRules = 139,
        AgreeToGroupRules = 140,
        FetchGroupRules = 141,
        CreateSelfAssignRole = 142,
        UpdateSelfAssignRole = 143,
        DeleteSelfAssignRole = 144,
        FetchSelfAssignRoles = 145,
        AssignSelfRole = 146,
        UnassignSelfRole = 147,
        SaveRoleChannelPermissions = 148,
        FetchMemberSelfRoles = 149,
        UpdateChannelWithPermissions = 150,
        CreateRoleSection = 151,
        DeleteRoleSection = 152,
        FetchRoleSections = 153,
        FetchGroupBans = 154,
        RequestJoinGroup = 155,
        FetchGroupInfo = 156,
        FetchProfileInfo = 157,
        // Form Channel
        CreateFormField = 158,
        UpdateFormField = 159,
        DeleteFormField = 160,
        FetchFormFields = 161,
        SubmitForm = 162,
        FetchFormSubmissions = 163,
        DeleteFormSubmission = 164,
        UpdateFormChannelSettings = 165,
        // Group Search
        SearchPublicGroups = 166,
        // Join Requests
        SendJoinRequest = 167,
        FetchJoinRequests = 168,
        RespondToJoinRequest = 169,
        CancelJoinRequest = 170,
    }
    public class DataSender
    {
        public static int userID;
        public static Plugin plugin;




        internal static async void CheckLodestoneEntry(string lodeSUrl, bool restoration)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendCheckLodestoneEntry);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(lodeSUrl);
                        buffer.WriteBool(restoration);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in CreateUserTag: " + ex.ToString());
                }
            }
        }
        internal static async void CreateUserTag(string tagName)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendUserTagCreation);
                        buffer.WriteString(tagName);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in CreateUserTag: " + ex.ToString());
                }
            }
        }
        internal static async void SubmitLodestoneURL(string lodeSUrl, string account_tag, bool restoration)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendLodestoneURL);
                        buffer.WriteString(account_tag);
                        buffer.WriteString(lodeSUrl);
                        buffer.WriteBool(restoration);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in CreateUserTag: " + ex.ToString());
                }
            }
        }
        internal static async void UnlinkAccount()
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.UnlinkAccount);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in CreateUserTag: " + ex.ToString());
                }
            }
        }

        internal static async void SendLogin()
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CLogin);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString("0.2.25");
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in CreateUserTag: " + ex.ToString());
                }
            }
        }
        public static async void ReportProfile(Character character, string reporterAccount, string playerName, string playerWorld, string reportInfo)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CReportProfile);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteString(playerName);
                        buffer.WriteString(playerWorld);
                        buffer.WriteString(reporterAccount);
                        buffer.WriteString(reportInfo);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in ReportProfile: " + ex.ToString());
                }
            }

        }
        public static async void SubmitGalleryLayout(Character character, int profileIndex, GalleryLayout layout)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {

                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSendGallery);
                        buffer.WriteInt(layout.tabIndex);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(layout.images.Count);

                        for (int i = 0; i < layout.images.Count; i++)
                        {
                            buffer.WriteString(layout.images[i].url);
                            buffer.WriteInt(layout.images[i].imageBytes.Length);
                            buffer.WriteBytes(layout.images[i].imageBytes);
                            buffer.WriteString(layout.images[i].tooltip);
                            buffer.WriteBool(layout.images[i].nsfw);
                            buffer.WriteBool(layout.images[i].trigger);
                            buffer.WriteInt(layout.images[i].index);
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendGalleryImage: " + ex.ToString());
                }
            }
        }
        public static async void RemoveGalleryImage(Character character, int profileIndex, int index, int tabIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSendGalleryRemoveRequest);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(index);
                        buffer.WriteInt(tabIndex);
                        Plugin.PluginLog.Debug(index.ToString());
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendGalleryImage: " + ex.ToString());
                }
            }
        }
        public static async void SubmitStoryLayout(Character character, int profileIndex, StoryLayout layout)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSendStory);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(layout.tabIndex);
                        buffer.WriteInt(layout.chapters.Count);

                        buffer.WriteString(layout.name);
                        for (int i = 0; i < layout.chapters.Count; i++)
                        {
                            buffer.WriteString(layout.chapters[i].title);
                            buffer.WriteString(layout.chapters[i].content);
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendStory: " + ex.ToString());
                }
            }
        }

        public static async void SendProfileAccessUpdate(Character character, string username, string localName, string localServer, string connectionName, string connectionWorld, int status)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SSendProfileAccessUpdate);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteString(connectionName);
                        buffer.WriteString(connectionWorld);
                        buffer.WriteInt(status);
                        Plugin.PluginLog.Debug($"Sending tooltipData access update: {localName} on {localServer} to {connectionName} on {connectionWorld} with status {status}");
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in Login: " + ex.ToString());
                }
            }
        }
        public static void ResetAllData()
        {
            try
            {
                // Reset loader tweens for target tooltipData loading
                Misc.ResetLoaderTween("tabs");
                Misc.ResetLoaderTween("gallery");

            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("TargetProfileWindow ResetAllData Debug: " + ex.Message);
            }
        }
        public static async void FetchProfile(Character character, bool self, int profileIndex, string targetName, string targetWorld, int profileID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {

                    // Always reset loader tweens and counters before loading
                    ResetAllData();


                    if (!self)
                    {
                        // Reset target tooltipData loading counters
                        DataReceiver.loadedTargetTabsCount = 0;
                        DataReceiver.tabsTargetCount = 0;
                        DataReceiver.loadedTargetGalleryImages = 0;
                        DataReceiver.TargetGalleryImagesToLoad = 0;


                        //Reset target tooltipData tabs
                        // Only proceed if the target window is in a default state
                        if (!TargetProfileWindow.IsDefault())
                            return;
                    }
                    else
                    {
                        // Reset self tooltipData loading counters
                        DataReceiver.loadedTabsCount = 0;
                        DataReceiver.tabsCount = 0;
                        DataReceiver.loadedGalleryImages = 0;
                        DataReceiver.GalleryImagesToLoad = 0;

                        //Reset tooltipData tabs

                    }

                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CFetchProfile);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteString(targetName);
                        buffer.WriteString(targetWorld);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(profileID);
                        buffer.WriteBool(self); // Indicate if this is a self tooltipData fetch
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchProfile: " + ex.ToString());
                }
            }
        }
        public static async void CreateProfile(Character character, string profileTitle, int profileType, int index)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CCreateProfile);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(index);
                        buffer.WriteInt(profileType);
                        buffer.WriteString(profileTitle);

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in CreateProfile: " + ex.ToString());
                }
            }
        }


        public static async void BookmarkPlayer(Character character, string playerName, string playerWorld, int profileID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSendPlayerBookmark);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteString(playerName);
                        buffer.WriteString(playerWorld);
                        buffer.WriteInt(profileID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in BookmarkProfile: " + ex.ToString());
                }
            }

        }
        public static async void RemoveBookmarkedPlayer(Character character, string playerName, int profileID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    Bookmarks.profileList.Clear();
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSendRemovePlayerBookmark);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(profileID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in RemoveBookmarkedPlayer: " + ex.ToString());
                }
            }
        }
        public static async void RequestBookmarks(Character character)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSendBookmarkRequest);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in RequestBookmarks: " + ex.ToString());
                }
            }

        }

        public static async void SubmitProfileBio(Character character, int profileIndex, BioLayout layout)
        {

            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CCreateProfileBio);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteBool(layout.isTooltip);
                        buffer.WriteInt(layout.tabIndex);
                        buffer.WriteString(layout.name);
                        buffer.WriteString(layout.race);
                        buffer.WriteString(layout.gender);
                        buffer.WriteString(layout.age);
                        buffer.WriteString(layout.height);
                        buffer.WriteString(layout.weight);
                        buffer.WriteString(layout.afg);
                        buffer.WriteInt(layout.alignment);
                        buffer.WriteInt(layout.personality_1);
                        buffer.WriteInt(layout.personality_2);
                        buffer.WriteInt(layout.personality_3);
                        buffer.WriteInt(layout.fields.Count);
                        buffer.WriteInt(layout.descriptors.Count);
                        buffer.WriteInt(layout.traits.Count);
                        for (int i = 0; i < layout.fields.Count; i++)
                        {
                            buffer.WriteString(layout.fields[i].name);
                            buffer.WriteString(layout.fields[i].description);
                        }
                        for (int i = 0; i < layout.descriptors.Count; i++)
                        {
                            buffer.WriteString(layout.descriptors[i].name);
                            buffer.WriteString(layout.descriptors[i].description);
                        }
                        for (int i = 0; i < layout.traits.Count; i++)
                        {
                            buffer.WriteString(layout.traits[i].name);
                            buffer.WriteString(layout.traits[i].description);
                            buffer.WriteInt(layout.traits[i].iconID);
                        }



                        await ClientTCP.SendDataAsync(buffer.ToArray());

                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SubmitProfileBio: " + ex.ToString());
                }
            }

        }

        public static async void SaveProfileConfiguration(Character character, bool showProfilePublicly, int profileIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendProfileConfiguration);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteBool(showProfilePublicly);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in sending user configuration: " + ex.ToString());
                }
            }
        }

        public static async void RequestTargetProfile(Character character, int profileID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SRequestTargetProfile);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);

                        buffer.WriteInt(profileID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                        NotesWindow.characterIndex = profileID;
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SubmitProfileBio: " + ex.ToString());
                }
            }

        }
        public static async void SubmitProfileDetails(Character character, int profileIndex, DetailsLayout layout)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendProfileDetails);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);

                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(layout.tabIndex);
                        buffer.WriteInt(layout.details.Count);
                        for (int i = 0; i < layout.details.Count; i++)
                        {
                            buffer.WriteInt(layout.details[i].id);
                            buffer.WriteString(layout.details[i].name);
                            buffer.WriteString(layout.details[i].content);
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendHooks: " + ex.ToString());
                }
            }

        }



        public static async void AddProfileNotes(Character character, int characterIndex, string notes)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {

                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSendProfileNotes);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(characterIndex);
                        buffer.WriteString(notes);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in AddProfileNotes: " + ex.ToString());
                }
            }
        }
        internal static async void SubmitInfoLayout(Character character, int currentProfile, InfoLayout layout)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SSendInfo);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(currentProfile);
                        buffer.WriteInt(layout.tabIndex);
                        buffer.WriteString(layout.text);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendInfoLayout: " + ex.ToString());
                }
            }
        }


        internal static async void RequestConnections(Character character)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SSendConnectionsRequest);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in RequestConnections: " + ex.ToString());
                }
            }
        }


        internal static async void SetProfileStatus(Character character, bool status, bool tooltipStatus, int profileIndex, string profileTitle, Vector4 color, byte[] avatarBytes, byte[] backgroundBytes, bool spoilerARR, bool spoilerHW, bool spoilerSB, bool spoilerSHB, bool spoilerEW, bool spoilerDT, bool NSFW, bool TRIGGERING)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SSendProfileStatus);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteString(profileTitle);
                        buffer.WriteFloat(color.X);
                        buffer.WriteFloat(color.Y);
                        buffer.WriteFloat(color.Z);
                        buffer.WriteFloat(color.W);
                        buffer.WriteInt(avatarBytes.Length);
                        buffer.WriteBytes(avatarBytes);
                        buffer.WriteInt(backgroundBytes.Length);
                        buffer.WriteBytes(backgroundBytes);
                        buffer.WriteBool(status);
                        buffer.WriteBool(tooltipStatus);
                        buffer.WriteBool(spoilerARR);
                        buffer.WriteBool(spoilerHW);
                        buffer.WriteBool(spoilerSB);
                        buffer.WriteBool(spoilerSHB);
                        buffer.WriteBool(spoilerEW);
                        buffer.WriteBool(spoilerDT);
                        buffer.WriteBool(NSFW);
                        buffer.WriteBool(TRIGGERING);
                        buffer.WriteInt(profileIndex);

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SetProfileStatus: " + ex.ToString());
                }
            }
        }



        internal static async void SendRequestPlayerTooltip(Character character, string playerName, string playerWorld)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SRequestTooltip);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteString(playerName);
                        buffer.WriteString(playerWorld);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendChatmessage: " + ex.ToString());
                }
            }
        }
        internal static async void SubmitListing(Character character, byte[] bannerBytes, string listingName, string listingDescription, string listingRules, int inclusion, int currentCategory, int currentType, int currentFocus, int currentSetting, bool nsfw, string triggers,
                                         int selectedStartYear, int selectedStartMonth, int selectedStartDay, int selectedStartHour, int selectedStartMinute, int selectedStartAmPm, int selectedStartTimezone,
                                         int selectedEndYear, int selectedEndMonth, int selectedEndDay, int selectedEndHour, int selectedEndMinute, int selectedEndAmPm, int selectedEndTimezone)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SCreateListing);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(bannerBytes.Length);
                        buffer.WriteBytes(bannerBytes);
                        buffer.WriteString(listingName);
                        buffer.WriteString(listingDescription);
                        buffer.WriteString(listingRules);
                        buffer.WriteInt(inclusion);
                        buffer.WriteInt(currentCategory);
                        buffer.WriteInt(currentType);
                        buffer.WriteInt(currentFocus);
                        buffer.WriteInt(currentSetting);
                        buffer.WriteBool(nsfw);
                        buffer.WriteString(triggers);
                        buffer.WriteInt(selectedStartYear);
                        buffer.WriteInt(selectedStartMonth);
                        buffer.WriteInt(selectedStartDay);
                        buffer.WriteInt(selectedStartHour);
                        buffer.WriteInt(selectedStartMinute);
                        buffer.WriteInt(selectedStartAmPm);
                        buffer.WriteInt(selectedStartTimezone);
                        buffer.WriteInt(selectedEndYear);
                        buffer.WriteInt(selectedEndMonth);
                        buffer.WriteInt(selectedEndDay);
                        buffer.WriteInt(selectedEndHour);
                        buffer.WriteInt(selectedEndMinute);
                        buffer.WriteInt(selectedEndAmPm);
                        buffer.WriteInt(selectedEndTimezone);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SubmitListing: " + ex.ToString());
                }
            }
        }


        internal static async void SendARPChatMessage(Character character, string message, bool isAnnouncement)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SSendChatMessage);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteString(message);
                        buffer.WriteBool(isAnnouncement);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendChatmessage: " + ex.ToString());
                }
            }
        }
        internal static async void DeleteProfile(Character character, int profileIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SDeleteProfile);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(profileIndex);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendChatmessage: " + ex.ToString());
                }
            }
        }

        internal static async void FetchProfiles(Character character)
        {

            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SFetchProfiles);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchProfiles: " + ex.ToString());
                }
            }
        }

        internal static async void SetProfileAsTooltip(Character character, bool isPrivate, string playername, string playerworld, int profileIndex, bool status)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SetAsTooltip);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteString(playername);
                        buffer.WriteString(playerworld);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteBool(status);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SetProfileAsTooltip: " + ex.ToString());
                }
            }
        }

        internal static async void RequestTargetProfileByCharacter(Character character, string name, string worldname)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.RequestTargetProfileByCharacter);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteString(name);
                        buffer.WriteString(worldname);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in RequestTargetProfileByCharacter: " + ex.ToString());
                }
            }
        }

        internal static async void PreviewProfile(Character character, int currentProfile)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.PreviewProfile);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(currentProfile);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in PreviewProfile: " + ex.ToString());
                }
            }
        }

        internal static async void SendItemCreation(Character character, int currentProfile, int tabIndex, string itemName, string itemDescription, int selectedItemType, int itemSubType, uint createItemIconID, int itemQuality)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        Plugin.PluginLog.Debug("tooltipData = " + currentProfile);
                        buffer.WriteInt((int)ClientPackets.CreateItem);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(currentProfile);
                        buffer.WriteInt(tabIndex);
                        buffer.WriteString(itemName);
                        buffer.WriteString(itemDescription);
                        buffer.WriteInt(selectedItemType);
                        buffer.WriteInt(itemSubType);
                        buffer.WriteInt((int)createItemIconID);
                        buffer.WriteInt(itemQuality);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendItemCreation: " + ex.ToString());
                }
            }
        }

        internal static async void SendItemOrder(Character character, int profileIndex, InventoryLayout layout, List<ItemDefinition> slotContents)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SortItems);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(layout.tabIndex);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(slotContents.Count);
                        for (int i = 0; i < slotContents.Count; i++)
                        {
                            buffer.WriteString(slotContents[i].name);
                            buffer.WriteString(slotContents[i].description);
                            buffer.WriteInt(slotContents[i].type);
                            buffer.WriteInt(slotContents[i].subtype);
                            buffer.WriteInt(slotContents[i].iconID);
                            buffer.WriteInt(slotContents[i].slot);
                            buffer.WriteInt(slotContents[i].quality);
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendItemOrder: " + ex.ToString());
                }
            }
        }
        internal static async void SendTradeUpdate(Character character, int profileIndex, string targetPlayerName, string targetPlayerWorld, InventoryLayout layout, List<ItemDefinition> slotContents)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendTradeUpdate);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(layout.tabIndex);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteString(targetPlayerName);
                        buffer.WriteString(targetPlayerWorld);
                        buffer.WriteInt(slotContents.Count);
                        for (int i = 0; i < slotContents.Count; i++)
                        {
                            buffer.WriteString(slotContents[i].name);
                            buffer.WriteString(slotContents[i].description);
                            buffer.WriteInt(slotContents[i].type);
                            buffer.WriteInt(slotContents[i].subtype);
                            buffer.WriteInt(slotContents[i].iconID);
                            buffer.WriteInt(slotContents[i].slot);
                            buffer.WriteInt(slotContents[i].quality);
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendItemOrder: " + ex.ToString());
                }
            }
        }

        internal static async void FetchProfileItems(Character character, int profileIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchProfileItems);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendProfileItems: " + ex.ToString());
                }
            }
        }

        internal static async void RequestOwnedListings(Character character, int profileIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchProfileItems);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendProfileItems: " + ex.ToString());
                }
            }
        }



        internal static async void SubmitModeratorAction(Character character, int capturedAuthor, string capturedMessage, string moderatorMessage, string moderatorNotes, ModeratorAction currentAction)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendModeratorAction);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(capturedAuthor);
                        buffer.WriteString(capturedMessage);
                        buffer.WriteString(moderatorMessage);
                        buffer.WriteString(moderatorNotes);
                        buffer.WriteInt((int)currentAction);

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendProfileItems: " + ex.ToString());
                }
            }
        }

        internal static async void RequestPersonals(Character character, string searchWorld, int index, int pageSize, string searchProfile, int selectedCategory)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendPersonalsRequest);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteString(searchWorld);
                        buffer.WriteString(searchProfile);
                        buffer.WriteInt(selectedCategory);
                        buffer.WriteInt(index);
                        buffer.WriteInt(pageSize);
                        Plugin.PluginLog.Debug("Selected Category = " + selectedCategory);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendProfileItems: " + ex.ToString());
                }
            }
        }
        internal static async void CreateTab(Character character, string name, int type, int profileIndex, int index)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CreateTab);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(profileIndex);
                        buffer.WriteString(name);
                        buffer.WriteInt(type);
                        buffer.WriteInt(index);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in Tab Creation: " + ex.ToString());
                }
            }
        }




        internal static async void CreateProfileBio(Character character, int index, int tabIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CreateBio);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(index);
                        buffer.WriteInt(tabIndex);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in Bio Creation: " + ex.ToString());
                }
            }
        }

        internal static async void DeleteTab(Character character, int profileIndex, int tabIndex, int tab_type)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.DeleteTab);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(tabIndex);
                        buffer.WriteInt(tab_type);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in Bio Creation: " + ex.ToString());
                }
            }
        }
        internal static async void SubmitDynamicLayout(Character character, int profileIndex, DynamicLayout dynamicLayout)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    List<string> nodes = new List<string>();
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendDynamicTab);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(dynamicLayout.tabIndex);
                        var nonCanceledChildren = dynamicLayout.RootNode.Children
                      .Where(n => !n.relatedElement.canceled)
                      .ToList();

                        buffer.WriteInt(nonCanceledChildren.Count);

                        foreach (var node in nonCanceledChildren)
                        {
                            bool nullName = node.Name == null;

                            buffer.WriteBool(nullName);
                            if (!nullName)
                            {
                                WriteLayoutNodeData(buffer, node);
                            }
                        }


                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Debug in SendDynamicData: {ex}");
                }
            }
        }
        internal static void WriteLayoutNodeData(ByteBuffer buffer, LayoutTreeNode node)
        {
            try
            {
                buffer.WriteInt(node.relatedElement.type);
                buffer.WriteInt(node.ID);
                Plugin.PluginLog.Debug($"WriteInt: node.ID = {node.ID}");
                Plugin.PluginLog.Debug($"WriteString: node.Name = '{node.Name ?? "NULL"}'");
                buffer.WriteString(node.Name);
                buffer.WriteBool(node.IsFolder);
                buffer.WriteInt(node.Parent != null ? node.Parent.ID : -1);
                int layoutElementType = node.relatedElement.type;

                if (layoutElementType == (int)LayoutElementTypes.Folder)
                {
                    FolderElement folderElement = (FolderElement)node.relatedElement;
                    buffer.WriteInt(folderElement.id);
                    Plugin.PluginLog.Debug(folderElement.id + " " + node.ID);
                }
                if (layoutElementType == (int)LayoutElementTypes.Text)
                {
                    TextElement textElement = (TextElement)node.relatedElement;
                    buffer.WriteInt(textElement.id);
                    buffer.WriteInt(textElement.type);
                    buffer.WriteInt(textElement.subType);
                    buffer.WriteFloat(textElement.width);
                    buffer.WriteFloat(textElement.height);
                    buffer.WriteFloat(textElement.PosX);
                    buffer.WriteFloat(textElement.PosY);
                    buffer.WriteString(textElement.text);
                }
                if (layoutElementType == (int)LayoutElementTypes.Image)
                {
                    ImageElement imageElement = (ImageElement)node.relatedElement;
                    buffer.WriteInt(imageElement.id);
                    buffer.WriteInt(imageElement.type);
                    buffer.WriteInt(imageElement.bytes.Length); // <-- use imageBytes
                    buffer.WriteBytes(imageElement.bytes);      // <-- use imageBytes
                    buffer.WriteFloat(imageElement.width);
                    buffer.WriteFloat(imageElement.height);
                    buffer.WriteFloat(imageElement.PosX);
                    buffer.WriteFloat(imageElement.PosY);
                    buffer.WriteBool(imageElement.proprotionalEditing);
                    buffer.WriteBool(imageElement.hasTooltip);
                    buffer.WriteString(imageElement.tooltip);
                    buffer.WriteBool(imageElement.maximizable);
                }
                if (layoutElementType == (int)LayoutElementTypes.Icon)
                {
                    IconElement iconElement = (IconElement)node.relatedElement;
                    buffer.WriteInt(iconElement.id);
                    buffer.WriteInt(iconElement.type);
                    buffer.WriteInt(iconElement.iconID);
                    buffer.WriteFloat(iconElement.PosX);
                    buffer.WriteFloat(iconElement.PosY);

                }
                if (layoutElementType == (int)LayoutElementTypes.Empty)
                {
                    EmptyElement empty = (EmptyElement)node.relatedElement;
                    buffer.WriteInt(empty.id);
                    buffer.WriteString(empty.name);
                }
                var nonCanceledChildren = node.Children
            .Where(n => !n.relatedElement.canceled)
            .ToList();

                buffer.WriteInt(nonCanceledChildren.Count);
                foreach (var child in nonCanceledChildren)
                {
                    WriteLayoutNodeData(buffer, child);
                }
                Plugin.PluginLog.Debug($"Wrote node: {node.Name} with ID: {node.ID} and Type: {layoutElementType}");
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug writing layout node data: {ex}");
            }
        }

        internal static async void RequestTargetTrade(Character character, string targetCharName, string targetCharWorld)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendTradeRequest);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteString(targetCharName);
                        buffer.WriteString(targetCharWorld);

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Debug in Request target trade: {ex}");
                }
                finally
                {
                    Plugin.PluginLog.Debug($"Requesting trade with {targetCharName} on {targetCharWorld}");
                }
            }
        }
        internal static async void SendTradeStatus(Character character, int tradeTabIndex, InventoryLayout layout, string targetName, string targetWorld, bool status, bool canceled)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendTradeStatus);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteBool(status); // true if sender is ready, false if not
                        buffer.WriteBool(canceled); // true for cancel, false for not canceled
                        buffer.WriteInt(tradeTabIndex);
                        buffer.WriteInt(layout.tradeSlotContents.Count);
                        buffer.WriteInt(layout.traderSlotContents.Count);


                        for (int i = 0; i < layout.tradeSlotContents.Count; i++)
                        {
                            buffer.WriteString(layout.tradeSlotContents[i].name);
                            buffer.WriteString(layout.tradeSlotContents[i].description);
                            buffer.WriteInt(layout.tradeSlotContents[i].type);
                            buffer.WriteInt(layout.tradeSlotContents[i].subtype);
                            buffer.WriteInt(layout.tradeSlotContents[i].iconID);
                            buffer.WriteInt(layout.tradeSlotContents[i].quality);
                            Plugin.PluginLog.Debug($"Trade Slot {i}: {layout.tradeSlotContents[i].name}");
                        }
                        for (int i = 0; i < layout.traderSlotContents.Count; i++)
                        {
                            buffer.WriteString(layout.traderSlotContents[i].name);
                            buffer.WriteString(layout.traderSlotContents[i].description);
                            buffer.WriteInt(layout.traderSlotContents[i].type);
                            buffer.WriteInt(layout.traderSlotContents[i].subtype);
                            buffer.WriteInt(layout.traderSlotContents[i].iconID);
                            buffer.WriteInt(layout.traderSlotContents[i].quality);
                            Plugin.PluginLog.Debug($"Trader Slot {i}: {layout.traderSlotContents[i].name}");
                        }

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Debug in Request target trade: {ex}");
                }
            }
        }

        internal static async void SendInventorySelection(Character character, int index, int tabID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SInventorySelection);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(tabID);
                        buffer.WriteInt(index);
                        Plugin.PluginLog.Debug("Index=" + index);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendInventorySelection: " + ex.ToString());
                }
            }
        }

        internal static async void SendDeleteItem(Character character, int profileIndex, InventoryLayout layout, int slotIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendItemDeletion);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(layout.tabIndex);
                        buffer.WriteInt(layout.id);
                        buffer.WriteInt(slotIndex);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendInventorySelection: " + ex.ToString());
                }
            }
        }

        internal static async void SendTradeSessionTargetInventory(Character character, int tabIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendTradeSessionTargetInventory);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(tabIndex);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendInventoryTargetSelection: " + ex.ToString());
                }
            }
        }

        internal static async void SubmitTreeLayout(Character character, int profileIndex, TreeLayout treeLayout)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendTreeLayout);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(treeLayout.tabIndex);

                        // Serialize Paths
                        buffer.WriteInt(treeLayout.Paths.Count);
                        foreach (var path in treeLayout.Paths)
                        {
                            buffer.WriteInt(path.Count);
                            foreach (var slot in path)
                            {
                                buffer.WriteInt(slot.x);
                                buffer.WriteInt(slot.y);
                            }
                        }

                        // Serialize PathConnections
                        buffer.WriteInt(treeLayout.PathConnections.Count);
                        foreach (var pathConnections in treeLayout.PathConnections)
                        {
                            buffer.WriteInt(pathConnections.Count);
                            foreach (var conn in pathConnections)
                            {
                                bool all0 = conn.from.x == 0 && conn.from.y == 0 && conn.to.x == 0 && conn.to.y == 0;
                                if (all0)
                                {
                                    buffer.WriteBool(true);
                                }
                                else
                                {
                                    buffer.WriteBool(false);
                                }

                                buffer.WriteInt(conn.from.x);
                                buffer.WriteInt(conn.from.y);
                                buffer.WriteInt(conn.to.x);
                                buffer.WriteInt(conn.to.y);
                            }
                        }

                        // Serialize Relationships (nodes)
                        buffer.WriteInt(treeLayout.relationships.Count);
                        foreach (var rel in treeLayout.relationships)
                        {
                            buffer.WriteString(rel.Name ?? "");

                            buffer.WriteString(rel.Description ?? "");

                            buffer.WriteInt(rel.IconID);

                            buffer.WriteBool(rel.active);
                            buffer.WriteBool(rel.Slot.HasValue);
                            if (rel.Slot.HasValue)
                            {
                                buffer.WriteInt(rel.Slot.Value.x);
                                buffer.WriteInt(rel.Slot.Value.y);
                                Plugin.PluginLog.Debug($"[PreSend] Slot: {rel.Slot.Value.x}, {rel.Slot.Value.y}");
                            }

                            // Serialize Links
                            buffer.WriteInt(rel.Links?.Count ?? 0);
                            if (rel.Links != null)
                            {
                                foreach (var link in rel.Links)
                                {
                                    buffer.WriteInt(link.From.x);
                                    buffer.WriteInt(link.From.y);
                                    buffer.WriteInt(link.To.x);
                                    buffer.WriteInt(link.To.y);
                                }
                            }
                        }

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SubmitTreeLayout: " + ex.ToString());
                }
            }
        }
        internal static async void SubmitInventoryLayout(Character character, int profileIndex, InventoryLayout inventoryLayout)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendInventoryLayout);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(inventoryLayout.tabIndex);
                        buffer.WriteInt(inventoryLayout.inventorySlotContents.Count);
                        foreach (var item in inventoryLayout.inventorySlotContents.Values)
                        {
                            buffer.WriteString(item.name);
                            buffer.WriteString(item.description);
                            buffer.WriteInt(item.type);
                            buffer.WriteInt(item.subtype);
                            buffer.WriteInt(item.iconID);
                            buffer.WriteInt(item.slot);
                            buffer.WriteInt(item.quality);
                            Plugin.PluginLog.Debug($"Inventory Slot {item.slot}: {item.name}");
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SubmitTreeLayout: " + ex.ToString());
                }
            }
        }


        internal static async void SendTabReorder(Character character, int profileIndex, List<(int oldIndex, int newIndex)> indexChanges)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendTabReorder);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(indexChanges.Count);
                        foreach (var (oldIdx, newIdx) in indexChanges)
                        {
                            buffer.WriteInt(oldIdx);
                            buffer.WriteInt(newIdx);
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SubmitTreeLayout: " + ex.ToString());
                }
            }
        }

        internal static async void RequestCompassFromList(Character character, List<IPlayerCharacter> players)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendCompassRequest);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(players.Count);
                        for (int i = 0; i < players.Count; i++)
                        {
                            buffer.WriteString(players[i].Name.ToString());
                            buffer.WriteString(players[i].HomeWorld.Value.Name.ToString());
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SubmitTreeLayout: " + ex.ToString());
                }
            }
        }
   
        internal static async void SetCompassStatus(Character character, bool status, int profileIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SetCompassStatus);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteBool(status);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SetCompassStatus: " + ex.ToString());
                }
            }
        }

        internal static async void SetFauxNameStatus(Character character, bool status, int profileIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SetFauxNameStatus);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteBool(status);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SetFauxNameStatus: " + ex.ToString());
                }
            }
        }
        internal static async void SetGroupValues(Character character, Group group, bool update, int leaderProfileIndex, int groupProfileIndex)
        {
            if (group == null) return;
            if (!ClientTCP.IsConnected()) return;

            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.SaveGroup);

                    // defensive guards to avoid NullReferenceException when fields are null
                    var accountKey = plugin?.Configuration?.account?.accountKey ?? string.Empty;
                    var charKey = character?.characterKey ?? string.Empty;
                    buffer.WriteString(accountKey);
                    buffer.WriteString(charKey);

                    buffer.WriteBool(update);
                    buffer.WriteInt(group.groupID);
                    buffer.WriteString(group.name ?? string.Empty);
                    buffer.WriteString(group.description ?? string.Empty);

                    var logoBytes = group.logoBytes ?? Array.Empty<byte>();
                    buffer.WriteInt(logoBytes.Length);
                    if (logoBytes.Length > 0)
                        buffer.WriteBytes(logoBytes);

                    var backgroundBytes = group.backgroundBytes ?? Array.Empty<byte>();
                    buffer.WriteInt(backgroundBytes.Length);
                    if (backgroundBytes.Length > 0)
                        buffer.WriteBytes(backgroundBytes);

                    buffer.WriteBool(group.visible);
                    buffer.WriteBool(group.openInvite);
                    buffer.WriteInt(groupProfileIndex);
                    buffer.WriteInt(leaderProfileIndex);

                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("Debug in SaveGroup: " + ex.ToString());
            }
        }

        internal static async void FetchGroups(Character character)
        {
            if (character == null) return;
            if (!ClientTCP.IsConnected()) return;

            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.FetchGroups);
                    buffer.WriteString(plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("Debug in SaveGroup: " + ex.ToString());
            }
        }

        /*
public static async void SendTreeData(int profileIndex, TreeNode rootNode)
{
if (ClientTCP.IsConnected())
{
try
{
using (var buffer = new ByteBuffer())
{
buffer.WriteInt((int)ClientPackets.CSendTreeData);
buffer.WriteString(plugin.Configuration.account.accountKey);
buffer.WriteString(character.characterKey);


buffer.WriteInt(profileIndex);

// Serialize the tree nodes and their related elements
SerializeTreeNodes(buffer, rootNode);

await ClientTCP.SendDataAsync(buffer.ToArray());
}
}
catch (Exception ex)
{
Plugin.PluginLog.Debug("Debug in SendTreeData: " + ex.ToString());
}
}
}

private static void SerializeTreeNodes(ByteBuffer buffer, TreeNode node)
{
// Write the node's basic information
buffer.WriteString(node.Name);
buffer.WriteBool(node.IsFolder);
buffer.WriteInt(node.ID);
buffer.WriteInt(node.layoutID);

// Write the related element's information if it exists
if (node.relatedElement != null)
{
buffer.WriteBool(true); // Indicates that the related element exists
SerializeLayoutElement(buffer, node.relatedElement);
}
else
{
buffer.WriteBool(false); // No related element
}

// Write the number of children
buffer.WriteInt(node.Children.Count);

// Recursively serialize children
foreach (var child in node.Children)
{
SerializeTreeNodes(buffer, child);
}
}

private static void SerializeLayoutElement(ByteBuffer buffer, LayoutElement element)
{
buffer.WriteInt(element.id);
buffer.WriteString(element.name);
buffer.WriteInt(element.type);
buffer.WriteFloat(element.PosX);
buffer.WriteFloat(element.PosY);
buffer.WriteBool(element.locked);
buffer.WriteBool(element.modifying);
buffer.WriteBool(element.canceled);

// Handle specific element types
if (element is TextElement textElement)
{
buffer.WriteString(textElement.text);
buffer.WriteFloat(textElement.color.X);
buffer.WriteFloat(textElement.color.Y);
buffer.WriteFloat(textElement.color.Z);
buffer.WriteFloat(textElement.color.W);
}
else if (element is ImageElement imageElement)
{
buffer.WriteInt(imageElement.bytes.Length);
buffer.WriteBytes(imageElement.bytes);
buffer.WriteString(imageElement.tooltip);
buffer.WriteFloat(imageElement.width);
buffer.WriteFloat(imageElement.height);
buffer.WriteBool(imageElement.initialized);
buffer.WriteBool(imageElement.proprotionalEditing);
buffer.WriteBool(imageElement.hasTooltip);
buffer.WriteBool(imageElement.maximizable);
}
else if (element is IconElement iconElement)
{
buffer.WriteInt((int)iconElement.State);
}
else if (element is FolderElement folderElement)
{
buffer.WriteString(folderElement.text);
}
else if (element is EmptyElement emptyElement)
{
buffer.WriteString(emptyElement.text);
buffer.WriteFloat(emptyElement.color.X);
buffer.WriteFloat(emptyElement.color.Y);
buffer.WriteFloat(emptyElement.color.Z);
buffer.WriteFloat(emptyElement.color.W);
}
}*/

        #region Group Chat & Roster Methods

        internal static async void SendGroupChatMessage(Character character, int groupID, int channelID, string messageContent)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendGroupChatMessage);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(channelID);
                        buffer.WriteString(messageContent);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendGroupChatMessage: " + ex.ToString());
                }
            }
        }

        internal static async void FetchGroupChatMessages(Character character, int groupID, int channelID, int limit = 50, int offset = 0)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchGroupChatMessages);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(channelID);
                        buffer.WriteInt(limit);
                        buffer.WriteInt(offset);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchGroupChatMessages: " + ex.ToString());
                }
            }
        }

        internal static async void EditGroupChatMessage(Character character, int messageID, string newContent)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.EditGroupChatMessage);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(messageID);
                        buffer.WriteString(newContent);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                        Plugin.PluginLog.Info($"[EditGroupChatMessage] Sent edit request for message {messageID}");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in EditGroupChatMessage: " + ex.ToString());
                }
            }
        }

        internal static async void DeleteGroupChatMessage(Character character, int messageID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.DeleteGroupChatMessage);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(messageID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                        Plugin.PluginLog.Info($"[DeleteGroupChatMessage] Sent delete request for message {messageID}");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in DeleteGroupChatMessage: " + ex.ToString());
                }
            }
        }

        internal static async void PinGroupChatMessage(Character character, int messageID, bool pin)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.PinGroupChatMessage);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(messageID);
                        buffer.WriteBool(pin);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                        Plugin.PluginLog.Info($"[PinGroupChatMessage] Sent {(pin ? "pin" : "unpin")} request for message {messageID}");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in PinGroupChatMessage: " + ex.ToString());
                }
            }
        }

        internal static async void FetchPinnedMessages(Character character, int groupID, int channelID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchPinnedMessages);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(channelID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                        Plugin.PluginLog.Info($"[FetchPinnedMessages] Sent request for group {groupID} channel {channelID}");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in FetchPinnedMessages: " + ex.ToString());
                }
            }
        }

        internal static async void LockChannel(Character character, int groupID, int channelID, bool isLocked)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.LockChannel);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(channelID);
                        buffer.WriteBool(isLocked);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                        Plugin.PluginLog.Info($"[LockChannel] Sent {(isLocked ? "lock" : "unlock")} request for channel {channelID} in group {groupID}");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in LockChannel: " + ex.ToString());
                }
            }
        }

        internal static async void UpdateChatReadStatus(Character character, int channelID, int lastReadMessageID, long timestamp)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.UpdateChatReadStatus);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteInt(channelID);
                        buffer.WriteInt(lastReadMessageID);
                        buffer.WriteLong(timestamp);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in UpdateChatReadStatus: " + ex.ToString());
                }
            }
        }

        internal static async void SaveGroupCategories(Character character, int groupID, List<GroupCategory> categories)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SaveGroupCategories);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(categories.Count);
                        foreach (var category in categories)
                        {
                            buffer.WriteInt(category.id);
                            buffer.WriteInt(category.sortOrder);
                            buffer.WriteString(category.name ?? string.Empty);
                            buffer.WriteString(category.description ?? string.Empty);
                            buffer.WriteBool(category.collapsed);

                            // Channels
                            int channelCount = category.channels?.Count ?? 0;
                            buffer.WriteInt(channelCount);
                            if (category.channels != null)
                            {
                                foreach (var channel in category.channels)
                                {
                                    buffer.WriteInt(channel.id);
                                    buffer.WriteInt(channel.index);
                                    buffer.WriteString(channel.name ?? string.Empty);
                                    buffer.WriteString(channel.description ?? string.Empty);
                                    buffer.WriteInt(channel.categoryID);
                                    buffer.WriteInt(channel.channelType);
                                    buffer.WriteBool(channel.everyoneCanView);
                                    buffer.WriteBool(channel.everyoneCanPost);
                                }
                            }
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SaveGroupCategories: " + ex.ToString());
                }
            }
        }

        internal static async void FetchGroupCategories(Character character, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchGroupCategories);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchGroupCategories: " + ex.ToString());
                }
            }
        }

        internal static async void SaveGroupRosterFields(Character character, int groupID, List<GroupRosterField> fields)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SaveGroupRosterFields);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(fields.Count);
                        foreach (var field in fields)
                        {
                            buffer.WriteInt(field.id);
                            buffer.WriteInt(field.sortOrder);
                            buffer.WriteString(field.name);
                            buffer.WriteInt(field.fieldType);
                            buffer.WriteBool(field.required);
                            buffer.WriteString(field.dropdownOptions ?? string.Empty);
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SaveGroupRosterFields: " + ex.ToString());
                }
            }
        }

        internal static async void FetchGroupRosterFields(Character character, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchGroupRosterFields);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchGroupRosterFields: " + ex.ToString());
                }
            }
        }

        internal static async void SaveMemberMetadata(Character character, int memberID, GroupMemberMetadata metadata)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SaveMemberMetadata);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteInt(memberID);
                        buffer.WriteLong(metadata.joinDate);
                        buffer.WriteLong(metadata.lastActive);
                        buffer.WriteString(metadata.customTitle ?? string.Empty);
                        buffer.WriteString(metadata.statusMessage ?? string.Empty);
                        buffer.WriteString(metadata.nicknameColor ?? "#FFFFFF");
                        buffer.WriteBool(metadata.isOnline);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SaveMemberMetadata: " + ex.ToString());
                }
            }
        }

        internal static async void FetchMemberMetadata(Character character, int memberID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchMemberMetadata);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteInt(memberID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchMemberMetadata: " + ex.ToString());
                }
            }
        }

        internal static async void SaveMemberFieldValues(Character character, int memberID, List<GroupMemberFieldValue> fieldValues)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SaveMemberFieldValues);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteInt(memberID);
                        buffer.WriteInt(fieldValues.Count);
                        foreach (var fv in fieldValues)
                        {
                            buffer.WriteInt(fv.fieldID);
                            buffer.WriteString(fv.fieldValue ?? string.Empty);
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SaveMemberFieldValues: " + ex.ToString());
                }
            }
        }

        internal static async void FetchMemberFieldValues(Character character, int memberID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchMemberFieldValues);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteInt(memberID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchMemberFieldValues: " + ex.ToString());
                }
            }
        }

        #endregion

        #region Group Invites & Forum


        internal static async void SendGroupInvite(Character character, int groupID, string inviteeName, string inviteeWorld, string message)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendGroupInvite);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteString(inviteeName ?? string.Empty);
                        buffer.WriteString(inviteeWorld ?? string.Empty);
                        buffer.WriteString(message ?? string.Empty);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendGroupInvite: " + ex.ToString());
                }
            }
        }

        internal static async void SaveForumStructure(Character character, int groupID, List<GroupForumCategory> categories)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SaveForumStructure);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(categories.Count);

                        foreach (var category in categories)
                        {
                            buffer.WriteInt(category.id);
                            buffer.WriteInt(category.parentCategoryID);
                            buffer.WriteInt(category.categoryIndex);
                            buffer.WriteString(category.name ?? string.Empty);
                            buffer.WriteString(category.description ?? string.Empty);
                            buffer.WriteString(category.icon ?? string.Empty);
                            buffer.WriteBool(category.collapsed);
                            buffer.WriteByte((byte)category.categoryType);
                            buffer.WriteInt(category.sortOrder);

                            // Channels
                            int channelCount = category.channels?.Count ?? 0;
                            buffer.WriteInt(channelCount);
                            if (category.channels != null)
                            {
                                foreach (var channel in category.channels)
                                {
                                    buffer.WriteInt(channel.id);
                                    buffer.WriteInt(channel.parentChannelID);
                                    buffer.WriteInt(channel.channelIndex);
                                    buffer.WriteString(channel.name ?? string.Empty);
                                    buffer.WriteString(channel.description ?? string.Empty);
                                    buffer.WriteByte((byte)channel.channelType);
                                    buffer.WriteBool(channel.isLocked);
                                    buffer.WriteBool(channel.isNSFW);
                                    buffer.WriteInt(channel.sortOrder);

                                    // Subchannels
                                    int subchannelCount = channel.subChannels?.Count ?? 0;
                                    buffer.WriteInt(subchannelCount);
                                    if (channel.subChannels != null)
                                    {
                                        foreach (var subchannel in channel.subChannels)
                                        {
                                            buffer.WriteInt(subchannel.id);
                                            buffer.WriteInt(subchannel.channelIndex);
                                            buffer.WriteString(subchannel.name ?? string.Empty);
                                            buffer.WriteString(subchannel.description ?? string.Empty);
                                            buffer.WriteByte((byte)subchannel.channelType);
                                            buffer.WriteBool(subchannel.isLocked);
                                            buffer.WriteBool(subchannel.isNSFW);
                                            buffer.WriteInt(subchannel.sortOrder);
                                        }
                                    }
                                }
                            }
                        }

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SaveForumStructure: " + ex.ToString());
                }
            }
        }

        internal static async void SaveForumPermissions(Character character, int groupID, List<GroupForumChannelPermission> permissions)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SaveForumPermissions);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(permissions.Count);

                        foreach (var perm in permissions)
                        {
                            buffer.WriteInt(perm.channelID);
                            buffer.WriteInt(perm.rankID);
                            buffer.WriteInt(perm.userID);
                            buffer.WriteBool(perm.canView);
                            buffer.WriteBool(perm.canPost);
                            buffer.WriteBool(perm.canReply);
                            buffer.WriteBool(perm.canCreateThreads);
                            buffer.WriteBool(perm.canEditOwn);
                            buffer.WriteBool(perm.canDeleteOwn);
                            buffer.WriteBool(perm.canManage);
                            buffer.WriteBool(perm.canPin);
                            buffer.WriteBool(perm.canLock);
                        }

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SaveForumPermissions: " + ex.ToString());
                }
            }
        }

        internal static async void FetchGroupInvites(Character character, bool fetchSentInvites = false, int groupID = -1)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchGroupInvites);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteBool(fetchSentInvites);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchGroupInvites: " + ex.ToString());
                }
            }
        }

        internal static async void RespondToGroupInvite(Character character, int inviteID, bool accept)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.RespondToGroupInvite);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(inviteID);
                        buffer.WriteBool(accept);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in RespondToGroupInvite: " + ex.ToString());
                }
            }
        }

        internal static async void CancelGroupInvite(Character character, int inviteID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CancelGroupInvite);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(inviteID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in CancelGroupInvite: " + ex.ToString());
                }
            }
        }

        internal static async void RequestJoinGroup(Character character, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.RequestJoinGroup);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in RequestJoinGroup: " + ex.ToString());
                }
            }
        }

        /// <summary>
        /// Sends a join request to a group that is visible but not open for direct joining.
        /// </summary>
        internal static async void SendJoinRequest(Character character, int groupID, string message)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendJoinRequest);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteString(message ?? string.Empty);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendJoinRequest: " + ex.ToString());
                }
            }
        }

        /// <summary>
        /// Fetches pending join requests for a group (for members with permission to accept requests).
        /// </summary>
        internal static async void FetchJoinRequests(Character character, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchJoinRequests);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchJoinRequests: " + ex.ToString());
                }
            }
        }

        /// <summary>
        /// Responds to a join request (accept or decline).
        /// </summary>
        /// <param name="accept">True to accept, false to decline</param>
        internal static async void RespondToJoinRequest(Character character, int requestID, int groupID, bool accept)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.RespondToJoinRequest);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(requestID);
                        buffer.WriteInt(groupID);
                        buffer.WriteBool(accept);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in RespondToJoinRequest: " + ex.ToString());
                }
            }
        }

        /// <summary>
        /// Cancels a pending join request (by the requester).
        /// </summary>
        internal static async void CancelJoinRequest(Character character, int requestID, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CancelJoinRequest);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(requestID);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in CancelJoinRequest: " + ex.ToString());
                }
            }
        }

        /// <summary>
        /// Fetches basic group info (name, logo URL) for displaying in embeds.
        /// </summary>
        internal static async void FetchGroupInfo(int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchGroupInfo);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchGroupInfo: " + ex.ToString());
                }
            }
        }

        /// <summary>
        /// Fetches basic profile info (name, avatar URL) for displaying in embeds.
        /// </summary>
        internal static async void FetchProfileInfo(int profileID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchProfileInfo);
                        buffer.WriteInt(profileID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchProfileInfo: " + ex.ToString());
                }
            }
        }

        internal static async void FetchGroupMembers(Character character, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchGroupMembers);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchGroupMembers: " + ex.ToString());
                }
            }
        }

        internal static async void FetchForumStructure(Character character, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchForumStructure);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchForumStructure: " + ex.ToString());
                }
            }
        }

        internal static async void FetchForumPermissions(Character character, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchForumPermissions);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchForumPermissions: " + ex.ToString());
                }
            }
        }

        internal static async void ViewInviteeProfile(Character character, int profileID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.ViewInviteeProfile);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(profileID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in ViewInviteeProfile: " + ex.ToString());
                }
            }
        }

        internal static async void FetchGroupRanks(Character character, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchGroupRanks);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchGroupRanks: " + ex.ToString());
                }
            }
        }

        internal static async void SaveGroupRank(Character character, GroupRank rank)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SaveGroupRank);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(rank.id);
                        buffer.WriteInt(rank.groupID);
                        buffer.WriteString(rank.name);
                        buffer.WriteString(rank.description ?? string.Empty);
                        buffer.WriteInt(rank.hierarchy);
                        buffer.WriteBool(rank.isDefaultMember);

                        // Member Permissions
                        buffer.WriteBool(rank.permissions.canInvite);
                        buffer.WriteBool(rank.permissions.canKick);
                        buffer.WriteBool(rank.permissions.canBan);
                        buffer.WriteBool(rank.permissions.canPromote);
                        buffer.WriteBool(rank.permissions.canDemote);

                        // Message Permissions
                        buffer.WriteBool(rank.permissions.canCreateAnnouncement);
                        buffer.WriteBool(rank.permissions.canReadMessages);
                        buffer.WriteBool(rank.permissions.canSendMessages);
                        buffer.WriteBool(rank.permissions.canDeleteOthersMessages);
                        buffer.WriteBool(rank.permissions.canPinMessages);

                        // Category Permissions
                        buffer.WriteBool(rank.permissions.canCreateCategory);
                        buffer.WriteBool(rank.permissions.canEditCategory);
                        buffer.WriteBool(rank.permissions.canDeleteCategory);
                        buffer.WriteBool(rank.permissions.canLockCategory);

                        // Forum Permissions
                        buffer.WriteBool(rank.permissions.canCreateForum);
                        buffer.WriteBool(rank.permissions.canEditForum);
                        buffer.WriteBool(rank.permissions.canDeleteForum);
                        buffer.WriteBool(rank.permissions.canLockForum);
                        buffer.WriteBool(rank.permissions.canMuteForum);

                        // Rank Management Permissions
                        buffer.WriteBool(rank.permissions.canManageRanks);
                        buffer.WriteBool(rank.permissions.canCreateRanks);

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SaveGroupRank: " + ex.ToString());
                }
            }
        }

        internal static async void DeleteGroupRank(Character character, int rankID, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.DeleteGroupRank);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(rankID);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in DeleteGroupRank: " + ex.ToString());
                }
            }
        }

        internal static async void UpdateRankHierarchies(Character character, int groupID, Dictionary<int, int> rankHierarchies)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.UpdateRankHierarchies);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(rankHierarchies.Count);

                        foreach (var kvp in rankHierarchies)
                        {
                            buffer.WriteInt(kvp.Key);   // rankID
                            buffer.WriteInt(kvp.Value); // new hierarchy value
                        }

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in UpdateRankHierarchies: " + ex.ToString());
                }
            }
        }

        internal static async void AssignMemberRank(Character character, int memberID, int rankID, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.AssignMemberRank);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(memberID);
                        buffer.WriteInt(rankID);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in AssignMemberRank: " + ex.ToString());
                }
            }
        }

        internal static async void RemoveMemberRank(Character character, int memberID, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.RemoveMemberRank);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(memberID);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in RemoveMemberRank: " + ex.ToString());
                }
            }
        }

        internal static async void RemoveSpecificMemberRank(Character character, int memberID, int rankID, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.RemoveSpecificMemberRank);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(memberID);
                        buffer.WriteInt(rankID);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in RemoveSpecificMemberRank: " + ex.ToString());
                }
            }
        }

        internal static async void KickGroupMember(Character character, int memberID, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.KickGroupMember);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(memberID);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in KickGroupMember: " + ex.ToString());
                }
            }
        }

        internal static async void BanGroupMember(Character character, int memberID, int userID, int profileID, string lodestoneURL, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.BanGroupMember);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(memberID);
                        buffer.WriteInt(userID);
                        buffer.WriteInt(profileID);
                        buffer.WriteString(lodestoneURL ?? string.Empty);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in BanGroupMember: " + ex.ToString());
                }
            }
        }

        internal static async void UnbanGroupMember(Character character, int banID, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.UnbanGroupMember);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(banID);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in UnbanGroupMember: " + ex.ToString());
                }
            }
        }

        internal static async void DeleteGroup(Character character, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    Plugin.PluginLog.Info($"[DeleteGroup] Sending delete request for group {groupID}");
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.DeleteGroup);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                        Plugin.PluginLog.Info($"[DeleteGroup] Delete request sent for group {groupID}");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in DeleteGroup: " + ex.ToString());
                }
            }
            else
            {
                Plugin.PluginLog.Warning($"[DeleteGroup] Not connected to server, cannot delete group {groupID}");
            }
        }

        internal static async void RenameCategory(Character character, int groupID, int categoryID, string newName)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.RenameCategory);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(categoryID);
                        buffer.WriteString(newName);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in RenameCategory: " + ex.ToString());
                }
            }
        }

        internal static async void DeleteCategory(Character character, int groupID, int categoryID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.DeleteCategory);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(categoryID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in DeleteCategory: " + ex.ToString());
                }
            }
        }

        internal static async void ReorderCategory(Character character, int groupID, int categoryID, int newIndex)
        {
            Plugin.PluginLog.Information($"[DataSender.ReorderCategory] Called with groupID={groupID}, categoryID={categoryID}, newIndex={newIndex}, connected={ClientTCP.IsConnected()}");
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.ReorderCategory);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(categoryID);
                        buffer.WriteInt(newIndex);
                        Plugin.PluginLog.Information($"[DataSender.ReorderCategory] Sending packet {(int)ClientPackets.ReorderCategory}");
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                        Plugin.PluginLog.Information($"[DataSender.ReorderCategory] Packet sent successfully");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in ReorderCategory: " + ex.ToString());
                }
            }
            else
            {
                Plugin.PluginLog.Warning("[DataSender.ReorderCategory] Not connected to server!");
            }
        }

        internal static async void RenameChannel(Character character, int groupID, int channelID, string newName)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.RenameChannel);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(channelID);
                        buffer.WriteString(newName);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in RenameChannel: " + ex.ToString());
                }
            }
        }

        internal static async void DeleteChannel(Character character, int groupID, int channelID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.DeleteChannel);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(channelID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in DeleteChannel: " + ex.ToString());
                }
            }
        }

        internal static async void MoveChannel(Character character, int groupID, int channelID, int newCategoryID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.MoveChannel);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(channelID);
                        buffer.WriteInt(newCategoryID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in MoveChannel: " + ex.ToString());
                }
            }
        }

        internal static async void ReorderChannel(Character character, int groupID, int channelID, int newIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.ReorderChannel);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(channelID);
                        buffer.WriteInt(newIndex);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in ReorderChannel: " + ex.ToString());
                }
            }
        }

        internal static async void CreateCategory(Character character, int groupID, string name, string description = "")
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CreateCategory);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteString(name);
                        buffer.WriteString(description);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in CreateCategory: " + ex.ToString());
                }
            }
        }

        internal static async void CreateChannel(Character character, int groupID, int categoryID, string name, string description = "", int channelType = 0)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CreateChannel);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(categoryID);
                        buffer.WriteString(name);
                        buffer.WriteString(description);
                        buffer.WriteInt(channelType);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in CreateChannel: " + ex.ToString());
                }
            }
        }

        internal static async void CreateChannelWithPermissions(Character character, int groupID, int categoryID, string name, string description, int channelType, bool isNsfw, bool everyoneCanView, bool everyoneCanPost, List<GroupsData.ChannelPermissionEntry> memberPermissions, List<GroupsData.ChannelPermissionEntry> rankPermissions, List<GroupsData.ChannelPermissionEntry> rolePermissions = null)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CreateChannelWithPermissions);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(categoryID);
                        buffer.WriteString(name);
                        buffer.WriteString(description ?? "");
                        buffer.WriteInt(channelType);
                        buffer.WriteBool(isNsfw);
                        buffer.WriteBool(everyoneCanView);
                        buffer.WriteBool(everyoneCanPost);

                        // Write member permissions with individual canView/canPost flags
                        buffer.WriteInt(memberPermissions?.Count ?? 0);
                        if (memberPermissions != null)
                        {
                            foreach (var perm in memberPermissions)
                            {
                                buffer.WriteInt(perm.id);
                                buffer.WriteBool(perm.canView);
                                buffer.WriteBool(perm.canPost);
                            }
                        }

                        // Write rank permissions with individual canView/canPost flags
                        buffer.WriteInt(rankPermissions?.Count ?? 0);
                        if (rankPermissions != null)
                        {
                            foreach (var perm in rankPermissions)
                            {
                                buffer.WriteInt(perm.id);
                                buffer.WriteBool(perm.canView);
                                buffer.WriteBool(perm.canPost);
                            }
                        }

                        // Write self-assign role permissions
                        buffer.WriteInt(rolePermissions?.Count ?? 0);
                        if (rolePermissions != null)
                        {
                            foreach (var perm in rolePermissions)
                            {
                                buffer.WriteInt(perm.id);
                                buffer.WriteBool(perm.canView);
                                buffer.WriteBool(perm.canPost);
                            }
                        }

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in CreateChannelWithPermissions: " + ex.ToString());
                }
            }
        }

        internal static async void UpdateChannelWithPermissions(Character character, int groupID, int channelID, string name, string description, int channelType, bool isNsfw, bool everyoneCanView, bool everyoneCanPost, List<GroupsData.ChannelPermissionEntry> memberPermissions, List<GroupsData.ChannelPermissionEntry> rankPermissions, List<GroupsData.ChannelPermissionEntry> rolePermissions = null)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.UpdateChannelWithPermissions);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(channelID);
                        buffer.WriteString(name);
                        buffer.WriteString(description ?? "");
                        buffer.WriteInt(channelType);
                        buffer.WriteBool(isNsfw);
                        buffer.WriteBool(everyoneCanView);
                        buffer.WriteBool(everyoneCanPost);

                        // Write member permissions with individual canView/canPost flags
                        buffer.WriteInt(memberPermissions?.Count ?? 0);
                        if (memberPermissions != null)
                        {
                            foreach (var perm in memberPermissions)
                            {
                                buffer.WriteInt(perm.id);
                                buffer.WriteBool(perm.canView);
                                buffer.WriteBool(perm.canPost);
                            }
                        }

                        // Write rank permissions with individual canView/canPost flags
                        buffer.WriteInt(rankPermissions?.Count ?? 0);
                        if (rankPermissions != null)
                        {
                            foreach (var perm in rankPermissions)
                            {
                                buffer.WriteInt(perm.id);
                                buffer.WriteBool(perm.canView);
                                buffer.WriteBool(perm.canPost);
                            }
                        }

                        // Write self-assign role permissions
                        buffer.WriteInt(rolePermissions?.Count ?? 0);
                        if (rolePermissions != null)
                        {
                            foreach (var perm in rolePermissions)
                            {
                                buffer.WriteInt(perm.id);
                                buffer.WriteBool(perm.canView);
                                buffer.WriteBool(perm.canPost);
                            }
                        }

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in UpdateChannelWithPermissions: " + ex.ToString());
                }
            }
        }

        internal static async void LeaveGroup(Character character, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.LeaveGroup);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in LeaveGroup: " + ex.ToString());
                }
            }
        }

        internal static async void TransferGroupOwnership(Character character, int groupID, int newOwnerMemberID, int newOwnerUserID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.TransferGroupOwnership);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(newOwnerMemberID);
                        buffer.WriteInt(newOwnerUserID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in TransferGroupOwnership: " + ex.ToString());
                }
            }
        }

        internal static async void FetchGroupMemberAvatar(Character character, int groupID, int userID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchGroupMemberAvatar);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(userID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                        Plugin.PluginLog.Info($"[FetchGroupMemberAvatar] Requested avatar for user {userID} in group {groupID}");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchGroupMemberAvatar: " + ex.ToString());
                }
            }
        }

        #endregion

        #region Profile Likes

        public static async void LikeProfile(Character character, int profileID, string comment, int likeCount)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.LikeProfile);
                buffer.WriteString(character.characterName);
                buffer.WriteString(character.characterWorld);
                buffer.WriteInt(profileID);
                buffer.WriteString(comment ?? string.Empty);
                buffer.WriteInt(likeCount);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"LikeProfile error: {ex.Message}");
            }
        }

        public static async void FetchLikesRemaining(Character character)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.FetchLikesRemaining);
                buffer.WriteString(character.characterName);
                buffer.WriteString(character.characterWorld);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"FetchLikesRemaining error: {ex.Message}");
            }
        }

        public static async void FetchProfileLikeCounts(Character character)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.FetchProfileLikeCounts);
                buffer.WriteString(character.characterName);
                buffer.WriteString(character.characterWorld);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"FetchProfileLikeCounts error: {ex.Message}");
            }
        }

        public static async void FetchProfileLikes(int profileID)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.FetchProfileLikes);
                buffer.WriteInt(profileID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"FetchProfileLikes error: {ex.Message}");
            }
        }

        #endregion

        #region Rules Channel & Self-Assign Roles

        public static async void SaveGroupRules(Character character, int groupID, string rulesContent)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.SaveGroupRules);
                buffer.WriteString($"{character.characterName}@{character.characterWorld}");
                buffer.WriteInt(groupID);
                buffer.WriteString(rulesContent ?? "");
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"SaveGroupRules error: {ex.Message}");
            }
        }

        public static async void AgreeToGroupRules(Character character, int groupID, int rulesVersion)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.AgreeToGroupRules);
                buffer.WriteString(plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(groupID);
                buffer.WriteInt(rulesVersion);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"AgreeToGroupRules error: {ex.Message}");
            }
        }

        public static async void FetchGroupRules(Character character, int groupID)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.FetchGroupRules);
                buffer.WriteString(plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(groupID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"FetchGroupRules error: {ex.Message}");
            }
        }

        public static async void CreateSelfAssignRole(Character character, int groupID, string name, string color, string description, int sectionID = 0)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.CreateSelfAssignRole);
                buffer.WriteString($"{character.characterName}@{character.characterWorld}");
                buffer.WriteInt(groupID);
                buffer.WriteString(name);
                buffer.WriteString(color ?? "#FFFFFF");
                buffer.WriteString(description ?? "");
                buffer.WriteInt(sectionID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"CreateSelfAssignRole error: {ex.Message}");
            }
        }

        public static async void UpdateSelfAssignRole(Character character, int groupID, int roleID, string name, string color, string description, int sectionID = 0)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.UpdateSelfAssignRole);
                buffer.WriteString($"{character.characterName}@{character.characterWorld}");
                buffer.WriteInt(groupID);
                buffer.WriteInt(roleID);
                buffer.WriteString(name);
                buffer.WriteString(color ?? "#FFFFFF");
                buffer.WriteString(description ?? "");
                buffer.WriteInt(sectionID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"UpdateSelfAssignRole error: {ex.Message}");
            }
        }

        public static async void DeleteSelfAssignRole(Character character, int groupID, int roleID)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.DeleteSelfAssignRole);
                buffer.WriteString($"{character.characterName}@{character.characterWorld}");
                buffer.WriteInt(groupID);
                buffer.WriteInt(roleID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"DeleteSelfAssignRole error: {ex.Message}");
            }
        }

        public static async void FetchSelfAssignRoles(Character character, int groupID)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.FetchSelfAssignRoles);
                buffer.WriteString(plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(groupID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"FetchSelfAssignRoles error: {ex.Message}");
            }
        }

        public static async void AssignSelfRole(Character character, int groupID, int roleID)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.AssignSelfRole);
                buffer.WriteString(plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(groupID);
                buffer.WriteInt(roleID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"AssignSelfRole error: {ex.Message}");
            }
        }

        public static async void UnassignSelfRole(Character character, int groupID, int roleID)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.UnassignSelfRole);
                buffer.WriteString(plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(groupID);
                buffer.WriteInt(roleID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"UnassignSelfRole error: {ex.Message}");
            }
        }

        public static async void SaveRoleChannelPermissions(Character character, int groupID, int roleID, List<GroupChannelRolePermission> permissions)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.SaveRoleChannelPermissions);
                buffer.WriteString($"{character.characterName}@{character.characterWorld}");
                buffer.WriteInt(groupID);
                buffer.WriteInt(roleID);
                buffer.WriteInt(permissions?.Count ?? 0);

                if (permissions != null)
                {
                    foreach (var perm in permissions)
                    {
                        buffer.WriteInt(perm.channelID);
                        buffer.WriteBool(perm.canView);
                        buffer.WriteBool(perm.canPost);
                    }
                }

                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"SaveRoleChannelPermissions error: {ex.Message}");
            }
        }

        public static async void FetchMemberSelfRoles(Character character, int groupID)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.FetchMemberSelfRoles);
                buffer.WriteString(plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(groupID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"FetchMemberSelfRoles error: {ex.Message}");
            }
        }

        public static async void CreateRoleSection(Character character, int groupID, string name)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.CreateRoleSection);
                buffer.WriteString(plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(groupID);
                buffer.WriteString(name);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"CreateRoleSection error: {ex.Message}");
            }
        }

        public static async void DeleteRoleSection(Character character, int groupID, int sectionID)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.DeleteRoleSection);
                buffer.WriteString(plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(groupID);
                buffer.WriteInt(sectionID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"DeleteRoleSection error: {ex.Message}");
            }
        }

        public static async void FetchRoleSections(Character character, int groupID)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.FetchRoleSections);
                buffer.WriteString(plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(groupID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"FetchRoleSections error: {ex.Message}");
            }
        }

        public static async void FetchGroupBans(Character character, int groupID)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.FetchGroupBans);
                buffer.WriteString(plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(groupID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"FetchGroupBans error: {ex.Message}");
            }
        }

        #endregion

        #region Form Channel

        public static async void CreateFormField(Character character, int channelId, string title, int fieldType, bool isOptional, int sortOrder)
        {
            try
            {
                Plugin.PluginLog.Info($"[CreateFormField] Sending: channelId={channelId}, title='{title}', fieldType={fieldType}, isOptional={isOptional}, sortOrder={sortOrder}");
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.CreateFormField);
                buffer.WriteString(plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(channelId);
                buffer.WriteString(title);
                buffer.WriteInt(fieldType);
                buffer.WriteBool(isOptional);
                buffer.WriteInt(sortOrder);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
                Plugin.PluginLog.Info($"[CreateFormField] Packet sent successfully");
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"CreateFormField error: {ex.Message}");
            }
        }

        public static async void UpdateFormField(Character character, int fieldId, string title, int fieldType, bool isOptional, int sortOrder)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.UpdateFormField);
                buffer.WriteString(plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(fieldId);
                buffer.WriteString(title);
                buffer.WriteInt(fieldType);
                buffer.WriteBool(isOptional);
                buffer.WriteInt(sortOrder);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"UpdateFormField error: {ex.Message}");
            }
        }

        public static async void DeleteFormField(Character character, int fieldId)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.DeleteFormField);
                buffer.WriteString(plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(fieldId);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"DeleteFormField error: {ex.Message}");
            }
        }

        public static async void FetchFormFields(Character character, int channelId)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.FetchFormFields);
                buffer.WriteString(plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(channelId);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"FetchFormFields error: {ex.Message}");
            }
        }

        public static async void SubmitForm(Character character, int channelId, int profileId, string profileName, List<(int fieldId, string value)> fieldValues)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.SubmitForm);
                buffer.WriteString(plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(channelId);
                buffer.WriteInt(profileId);
                buffer.WriteString(profileName ?? "");
                buffer.WriteInt(fieldValues?.Count ?? 0);
                if (fieldValues != null)
                {
                    foreach (var (fieldId, value) in fieldValues)
                    {
                        buffer.WriteInt(fieldId);
                        buffer.WriteString(value ?? "");
                    }
                }
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"SubmitForm error: {ex.Message}");
            }
        }

        public static async void FetchFormSubmissions(Character character, int channelId)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.FetchFormSubmissions);
                buffer.WriteString(plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(channelId);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"FetchFormSubmissions error: {ex.Message}");
            }
        }

        public static async void DeleteFormSubmission(Character character, int submissionId)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.DeleteFormSubmission);
                buffer.WriteString(plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(submissionId);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"DeleteFormSubmission error: {ex.Message}");
            }
        }

        public static async void UpdateFormChannelSettings(Character character, int channelId, bool allowFormatTags)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.UpdateFormChannelSettings);
                buffer.WriteString(plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(channelId);
                buffer.WriteBool(allowFormatTags);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"UpdateFormChannelSettings error: {ex.Message}");
            }
        }

        #endregion

        #region Group Search

        public static async void SearchPublicGroups(Character character, string searchQuery)
        {
            try
            {
                DataReceiver.groupSearchInProgress = true;
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.SearchPublicGroups);
                buffer.WriteString(plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteString(searchQuery ?? "");
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                DataReceiver.groupSearchInProgress = false;
                Plugin.PluginLog.Error($"SearchPublicGroups error: {ex.Message}");
            }
        }

        #endregion
    }
}