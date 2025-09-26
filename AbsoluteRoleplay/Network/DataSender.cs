using AbsoluteRP;
using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Moderator;
using AbsoluteRP.Windows.Profiles;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
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
{
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
        SCreateGroupChat = 43,
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
        RestoreAccount = 80,
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
                        Plugin.PluginLog.Error(index.ToString());
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
                        Plugin.PluginLog.Debug($"Sending profile access update: {localName} on {localServer} to {connectionName} on {connectionWorld} with status {status}");
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
                // Reset loader tweens for target profile loading
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
                        // Reset target profile loading counters
                        DataReceiver.loadedTargetTabsCount = 0;
                        DataReceiver.tabsTargetCount = 0;
                        DataReceiver.loadedTargetGalleryImages = 0;
                        DataReceiver.TargetGalleryImagesToLoad = 0;


                        //Reset target profile tabs
                        // Only proceed if the target window is in a default state
                        if (!TargetProfileWindow.IsDefault())
                            return;
                    }
                    else
                    {
                        // Reset self profile loading counters
                        DataReceiver.loadedTabsCount = 0;
                        DataReceiver.tabsCount = 0;
                        DataReceiver.loadedGalleryImages = 0;
                        DataReceiver.GalleryImagesToLoad = 0;

                        //Reset profile tabs

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
                        buffer.WriteBool(self); // Indicate if this is a self profile fetch
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


        public static async void BookmarkPlayer(Character character, string playerName, string playerWorld)
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
                    BookmarksWindow.profileList.Clear();
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
                        for(int i = 0; i < layout.descriptors.Count; i++)
                        {
                            buffer.WriteString(layout.descriptors[i].name);
                            buffer.WriteString(layout.descriptors[i].description);
                        }
                        for(int i = 0; i < layout.traits.Count; i++)
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


        internal static async void CreateGroup(Character character, string groupName, string username, string password)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SCreateGroupChat);
                        buffer.WriteString(plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteString(groupName);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendChatmessage: " + ex.ToString());
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
                        Plugin.PluginLog.Debug("profile = " + currentProfile);
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
                            if(!nullName)
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
                        for(int i = 0; i < layout.traderSlotContents.Count; i++)
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
    }
}
