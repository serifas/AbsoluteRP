using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using AbsoluteRoleplay;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Xml.Linq;
using AbsoluteRoleplay.Windows.Profiles;
using System.Threading.Tasks;
using AbsoluteRoleplay.Defines;
using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Windows.Profiles.ProfileTabs;
using System.Numerics;
using Dalamud.Interface.Textures.TextureWraps;
using OtterGui.Text.EndObjects;
using AbsoluteRoleplay.Windows.Moderator;

namespace Networking
{
    public enum ClientPackets
    {
        CHelloServer = 1,
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
        CSendHooks = 14,
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
        SSendOOC = 35,
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
    }
    public class DataSender
    {
        public static int userID;
        public static Plugin plugin;
        public static async void Login(string username, string password, string playerName, string playerWorld)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    Plugin.justRegistered = false;
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CLogin);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in Login: " + ex.ToString());
                }

            }

        }
        public static async void Logout(string username, string password, string playerName, string playerWorld)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.Logout);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in Logout: " + ex.ToString());
                }
            }
        }

        public static async void Register(string username, string password, string email)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    Plugin.justRegistered = true;
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CRegister);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(email);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in Register: " + ex.ToString());
                }
            }
        }
        public static async void ReportProfile(string reporterAccount, string playerName, string playerWorld, string reportInfo)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CReportProfile);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(playerName);
                        buffer.WriteString(playerWorld);
                        buffer.WriteString(reporterAccount);
                        buffer.WriteString(reportInfo);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in ReportProfile: " + ex.ToString());
                }
            }

        }
        public static async void SendGalleryImages(int profileIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {

                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSendGallery);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(plugin.playername);
                        buffer.WriteString(plugin.playerworld);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(GalleryTab.galleryImageCount);
                        byte[] imageBytes = new byte[0];
                        if (Plugin.PluginInterface is { AssemblyLocation.Directory.FullName: { } imagePath })
                        {
                            imageBytes = File.ReadAllBytes(Path.Combine(imagePath, "UI/common/profiles/galleries/picturetab.png"));                            
                        }


                        for (int i = 0; i < GalleryTab.galleryImageCount; i++)
                        {

                            if (GalleryTab.imageBytes[i] == imageBytes)
                            {
                                buffer.WriteBool(false);
                            }
                            else
                            {
                                buffer.WriteBool(true);
                            }

                            buffer.WriteString(GalleryTab.imageURLs[i]);
                            buffer.WriteInt(GalleryTab.imageBytes[i].Length);
                            buffer.WriteBytes(GalleryTab.imageBytes[i]);
                            buffer.WriteString(GalleryTab.imageTooltips[i]);
                            buffer.WriteBool(GalleryTab.NSFW[i]);
                            buffer.WriteBool(GalleryTab.TRIGGER[i]);
                            buffer.WriteInt(i);
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in SendGalleryImage: " + ex.ToString());
                }
            }
        }
        public static async void RemoveGalleryImage(int profileIndex, string playername, string playerworld, int index, int imageCount)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSendGalleryRemoveRequest);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(playername);
                        buffer.WriteString(playerworld);

                        buffer.WriteInt(index);
                        buffer.WriteInt(imageCount);

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in SendGalleryImage: " + ex.ToString());
                }
            }
        }
        public static async void SendStory(int profileIndex, string storyTitle,   List<Tuple<string, string>> storyChapters)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSendStory);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(plugin.playername);
                        buffer.WriteString(plugin.playerworld);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(storyChapters.Count);
                        buffer.WriteString(storyTitle);
                        for (int i = 0; i < storyChapters.Count; i++)
                        {
                            buffer.WriteString(storyChapters[i].Item1);
                            buffer.WriteString(storyChapters[i].Item2);
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in SendStory: " + ex.ToString());
                }
            }
        }
       
        public static async void SendProfileAccessUpdate(string username, string localName, string localServer, string connectionName, string connectionWorld, int status)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SSendProfileAccessUpdate);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(localName);
                        buffer.WriteString(localServer);
                        buffer.WriteString(connectionName);
                        buffer.WriteString(connectionWorld);
                        buffer.WriteInt(status);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in Login: " + ex.ToString());
                }
            }
        }
        public static async void RenameProfile(int profileIndex, string name)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.RenameProfile);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(plugin.playername);
                        buffer.WriteString(plugin.playerworld);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteString(name);

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in RenameProfile: " + ex.ToString());
                }
            }
        }
    
   
        public static async void FetchProfile(int profileIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                   
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CFetchProfile);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(plugin.playername);
                        buffer.WriteString(plugin.playerworld);
                        buffer.WriteInt(profileIndex);
                        
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in FetchProfile: " + ex.ToString());
                }
            }
        }
        public static async void CreateProfile(int index)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CCreateProfile);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(plugin.playername);
                        buffer.WriteString(plugin.playerworld);
                        buffer.WriteInt(index);
                       
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in CreateProfile: " + ex.ToString());
                }
            }
        }


        public static async void BookmarkPlayer(string playerName, string playerWorld)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSendPlayerBookmark);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(playerName);
                        buffer.WriteString(playerWorld);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in BookmarkProfile: " + ex.ToString());
                }
            }

        }
        public static async void RemoveBookmarkedPlayer(string playerName, int profileID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    BookmarksWindow.profileList.Clear();
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSendRemovePlayerBookmark);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteInt(profileID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in RemoveBookmarkedPlayer: " + ex.ToString());
                }
            }
        }
        public static async void RequestBookmarks()
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSendBookmarkRequest);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in RequestBookmarks: " + ex.ToString());
                }
            }

        }

        public static async void SubmitProfileBio(int profileIndex, byte[] avatarBytes, string name, string race, string gender, string age,
                                            string height, string weight, string atFirstGlance, int alignment, int personality_1, int personality_2, int personality_3,
                                            List<field> customFields, List<descriptor> customDescriptors, List<trait> customPersonalities)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CCreateProfileBio);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(plugin.playername);
                        buffer.WriteString(plugin.playerworld);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(avatarBytes.Length);
                        buffer.WriteBytes(avatarBytes);
                        buffer.WriteString(name);
                        buffer.WriteString(race);
                        buffer.WriteString(gender);
                        buffer.WriteString(age);
                        buffer.WriteString(height);
                        buffer.WriteString(weight);
                        buffer.WriteString(atFirstGlance);
                        buffer.WriteInt(alignment);
                        buffer.WriteInt(personality_1);
                        buffer.WriteInt(personality_2);
                        buffer.WriteInt(personality_3);
                        buffer.WriteInt(customFields.Count);
                        buffer.WriteInt(customDescriptors.Count);
                        buffer.WriteInt(customPersonalities.Count);

                        for(int i = 0; i < customFields.Count; i++)
                        {
                            buffer.WriteString(customFields[i].name);
                            buffer.WriteString(customFields[i].description);
                        }
                        for(int i = 0; i < customDescriptors.Count; i++)
                        {
                            buffer.WriteString(customDescriptors[i].name);
                            buffer.WriteString(customDescriptors[i].description);
                        }
                        for(int i = 0; i < customPersonalities.Count; i++)
                        {
                            buffer.WriteString(customPersonalities[i].name);
                            buffer.WriteString(customPersonalities[i].description);
                            buffer.WriteInt(customPersonalities[i].iconID);
                        }



                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in SubmitProfileBio: " + ex.ToString());
                }
            }

        }
      
        public static async void SaveProfileConfiguration(bool showProfilePublicly, int profileIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendProfileConfiguration);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteBool(showProfilePublicly);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in sending user configuration: " + ex.ToString());
                }
            }
        }
        public static async void RequestTargetProfiles(string targetPlayerName, string targetPlayerWorld)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        BookmarksWindow.profileList.Clear();
                        buffer.WriteInt((int)ClientPackets.RequestTargetProfileByCharacter);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(targetPlayerName);
                        buffer.WriteString(targetPlayerWorld);
                        await ClientTCP.SendDataAsync(buffer.ToArray());

                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in SubmitProfileBio: " + ex.ToString());
                }
            }

        }
        public static async void RequestTargetProfile(int currentIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SRequestTargetProfile);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(plugin.playername);
                        buffer.WriteString(plugin.playerworld);
                        buffer.WriteInt(currentIndex);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                        NotesWindow.characterIndex = currentIndex;
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in SubmitProfileBio: " + ex.ToString());
                }
            }

        }
        public static async void SendHooks(int profileIndex, List<Tuple<int, string, string>> hooks)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSendHooks);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(plugin.playername);
                        buffer.WriteString(plugin.playerworld);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(hooks.Count);
                        for (int i = 0; i < hooks.Count; i++)
                        {
                            buffer.WriteInt(hooks[i].Item1);
                            buffer.WriteString(hooks[i].Item2);
                            buffer.WriteString(hooks[i].Item3);
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in SendHooks: " + ex.ToString());
                }
            }

        }



        public static async void AddProfileNotes(int characterIndex, string notes)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {

                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSendProfileNotes);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteInt(characterIndex);
                        buffer.WriteString(notes);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in AddProfileNotes: " + ex.ToString());
                }
            }
        }

        internal static async void SendVerification(string username, string verificationKey)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SSubmitVerificationKey);
                        buffer.WriteString(username);
                        buffer.WriteString(verificationKey);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in SendVerification: " + ex.ToString());
                }
            }

        }

        internal static async void SendRestorationRequest(string restorationEmail)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SSubmitRestorationRequest);
                        buffer.WriteString(restorationEmail);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in SendRestorationRequest: " + ex.ToString());
                }
            }
        }

        internal static async void SendRestoration(string email, string password, string restorationKey)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SSubmitRestorationKey);
                        buffer.WriteString(password);
                        buffer.WriteString(restorationKey);
                        buffer.WriteString(email);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in SendRestoration: " + ex.ToString());
                }
            }
        }

        internal static async void SendOOCInfo(int currentProfile, string OOC)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SSendOOC);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(plugin.playername);
                        buffer.WriteString(plugin.playerworld);
                        buffer.WriteInt(currentProfile);
                        buffer.WriteString(OOC);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in SendOOCInfo: " + ex.ToString());
                }
            }
        }


        internal static async void RequestConnections(string username, string password, bool windowRequest)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SSendConnectionsRequest);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in RequestConnections: " + ex.ToString());
                }
            }
        }


        internal static async void SetProfileStatus(bool status, bool tooltipStatus, int profileIndex, string profileTitle, Vector4 color, bool spoilerARR, bool spoilerHW, bool spoilerSB, bool spoilerSHB, bool spoilerEW, bool spoilerDT, bool NSFW, bool TRIGGERING)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SSendProfileStatus);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(plugin.playername);
                        buffer.WriteString(plugin.playerworld);
                        buffer.WriteString(profileTitle);
                        buffer.WriteFloat(color.X);
                        buffer.WriteFloat(color.Y);
                        buffer.WriteFloat(color.Z);
                        buffer.WriteFloat(color.W);
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
                    plugin.logger.Error("Error in SetProfileStatus: " + ex.ToString());
                }
            }
        }


        internal static async void CreateGroup(string groupName, string username, string password)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SCreateGroupChat);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(groupName);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in SendChatmessage: " + ex.ToString());
                }
            }
        }

        internal static async void SendRequestPlayerTooltip(string playerName, string playerWorld)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SRequestTooltip);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(playerName);
                        buffer.WriteString(playerWorld);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in SendChatmessage: " + ex.ToString());
                }
            }
        }
        internal static async void SubmitListing(byte[] bannerBytes, string listingName, string listingDescription, string listingRules, int inclusion, int currentCategory, int currentType, int currentFocus, int currentSetting, bool nsfw, string triggers,
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
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
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
                    plugin.logger.Error("Error in SubmitListing: " + ex.ToString());
                }
            }
        }

        internal static async void RequestListingsSection(int id)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SRequestListing);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteInt(id);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in RequestListing: " + ex.ToString());
                }
            }
        }

        internal static async void SendARPChatMessage(string message, bool isAnnouncement)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SSendChatMessage);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(plugin.playername);
                        buffer.WriteString(plugin.playerworld);
                        buffer.WriteString(message);
                        buffer.WriteBool(isAnnouncement);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in SendChatmessage: " + ex.ToString());
                }
            }
        }
        internal static async void DeleteProfile(int profileIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SDeleteProfile);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(plugin.playername);
                        buffer.WriteString(plugin.playerworld);
                        buffer.WriteInt(profileIndex);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in SendChatmessage: " + ex.ToString());
                }
            }
        }

        internal static async void FetchProfiles()
        {
            
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SFetchProfiles);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(plugin.playername);
                        buffer.WriteString(plugin.playerworld);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in FetchProfiles: " + ex.ToString());
                }               
            }
        }

        internal static async void SetProfileAsTooltip(bool isPrivate, string playername, string playerworld, int profileIndex, bool status)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SetAsTooltip);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(playername);
                        buffer.WriteString(playerworld);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteBool(status);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in SetProfileAsTooltip: " + ex.ToString());
                }
            }
        }

        internal static async void RequestTargetProfileByCharacter(string name, string worldname)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.RequestTargetProfileByCharacter);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(name);
                        buffer.WriteString(worldname);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in RequestTargetProfileByCharacter: " + ex.ToString());
                }
            }
        }

        internal static async void PreviewProfile(int currentProfile)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.PreviewProfile);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(plugin.playername);
                        buffer.WriteString(plugin.playerworld);
                        buffer.WriteInt(currentProfile);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in PreviewProfile: " + ex.ToString());
                }
            }
        }

        internal static async void SendItemCreation(int currentProfile, string itemName, string itemDescription, int selectedItemType, int itemSubType, uint createItemIconID, int itemQuality)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CreateItem);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(plugin.playername);
                        buffer.WriteString(plugin.playerworld);
                        buffer.WriteInt(currentProfile);
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
                    plugin.logger.Error("Error in SendItemCreation: " + ex.ToString());
                }
            }
        }

        internal static async void SendItemOrder(int profileIndex, List<ItemDefinition> slotContents)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SortItems);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(plugin.playername);
                        buffer.WriteString(plugin.playerworld);
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
                    plugin.logger.Error("Error in SendItemOrder: " + ex.ToString());
                }
            }
        }

        internal static async void FetchProfileItems(int profileIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchProfileItems);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(plugin.playername);
                        buffer.WriteString(plugin.playerworld);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in SendProfileItems: " + ex.ToString());
                }
            }
        }

        internal static async void RequestOwnedListings(int profileIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchProfileItems);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in SendProfileItems: " + ex.ToString());
                }
            }
        }

        internal static async void SendCustomLayouts(int currentProfileIndex, SortedList<int, Layout> layouts)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendCustomLayouts);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(plugin.playername);
                        buffer.WriteString(plugin.playerworld);
                        buffer.WriteInt(currentProfileIndex);
                        buffer.WriteInt(layouts.Count);
                        for(int i = 0; i < layouts.Count; i++)
                        {
                            buffer.WriteInt(layouts.Values[i].id);
                            buffer.WriteString(layouts.Values[i].name);
                            List<LayoutElement> elements = layouts.Values[i].elements;
                            buffer.WriteInt(elements.Count);
                            for (int e = 0; e < elements.Count; e++)
                            {  
                                LayoutElement element = elements[e];
                                if(element.canceled)
                                buffer.WriteInt(element.id);
                                buffer.WriteString(element.name);
                                buffer.WriteInt(element.type);
                                buffer.WriteBool(element.canceled);    
                            }
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in SendProfileItems: " + ex.ToString());
                }
            }
        }

        internal static async void AssignWarning(int author, string message)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendCustomLayouts);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(plugin.playername);
                        buffer.WriteString(plugin.playerworld);
                        buffer.WriteInt(author);
                        buffer.WriteString(message);
                     
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error("Error in SendProfileItems: " + ex.ToString());
                }
            }
        }

        internal static async void SubmitModeratorAction(int capturedAuthor, string capturedMessage, string moderatorMessage, string moderatorNotes, ModeratorAction currentAction)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendModeratorAction);
                        buffer.WriteString(plugin.username);
                        buffer.WriteString(plugin.password);
                        buffer.WriteString(plugin.playername);
                        buffer.WriteString(plugin.playerworld);
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
                    plugin.logger.Error("Error in SendProfileItems: " + ex.ToString());
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
buffer.WriteString(plugin.username);
buffer.WriteString(plugin.password);
buffer.WriteString(plugin.playername);
buffer.WriteString(plugin.playerworld);
buffer.WriteInt(profileIndex);

// Serialize the tree nodes and their related elements
SerializeTreeNodes(buffer, rootNode);

await ClientTCP.SendDataAsync(buffer.ToArray());
}
}
catch (Exception ex)
{
plugin.logger.Error("Error in SendTreeData: " + ex.ToString());
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
