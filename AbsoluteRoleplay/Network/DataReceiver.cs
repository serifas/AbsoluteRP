
using System;
using System.Text;
using System.Text.Json;
using AbsoluteRoleplay;
using Dalamud.Interface.Textures;
using System.Text.RegularExpressions;
using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Windows.Profiles;
using AbsoluteRoleplay.Windows;
using static AbsoluteRoleplay.Packets.Packets;
using System.Linq;
using JetBrains.Annotations;
using System.Collections.Generic;
using System.Numerics;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Networking
{
    public class DataReceiver
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
        private static ClientWebSocket _client = new ClientWebSocket();

        public static async Task ReceivePacketsAsync()
        {
            var buffer = new byte[1024];  // Buffer for receiving WebSocket data

            while (_client.State == WebSocketState.Open)
            {
                // Receive data from WebSocket
                WebSocketReceiveResult result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    // Handle WebSocket close
                    await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    Console.WriteLine("WebSocket closed.");
                }
                else
                {
                    // Only process data if it's not a close message
                    if (result.Count > 0)
                    {
                        // Copy the received data to a byte array of the correct size
                        byte[] receivedData = new byte[result.Count];
                        Array.Copy(buffer, 0, receivedData, 0, result.Count);

                        // Call HandleData with the byte array
                        HandleData(receivedData);
                    }
                }
            }
        }

        public static void HandleData(byte[] data)
        {
            
            var packetID = BitConverter.ToInt32(data, 0);
            var packetType = (ReceveirPackets)packetID;
            byte[] payloadData = data.Skip(4).ToArray();

            switch (packetType)
            {
                case ReceveirPackets.SWelcomeMessage:
                    HandleWelcomeMessage(payloadData);
                    break;

                case ReceveirPackets.SRecLoginStatus:
                    HandleLoginStatus(payloadData);
                    break;

                case ReceveirPackets.SRecProfileBio:
                    ReceiveProfileBio(payloadData);
                    break;

                case ReceveirPackets.SRecTargetBio:
                    ReceiveTargetBio(payloadData);
                    break;

                case ReceveirPackets.SRecBookmarks:
                    RecBookmarks(payloadData);
                    break;

                case ReceveirPackets.SSendProfileHook:
                    ReceiveProfileHooks(payloadData);
                    break;

                case ReceveirPackets.SRecProfileStory:
                    ReceiveProfileStory(payloadData);
                    break;

                case ReceveirPackets.SRecTargetStory:
                    ReceiveTargetStory(payloadData);
                    break;

                case ReceveirPackets.SRecNoProfileStory:
                    NoProfileStory(payloadData);
                    break;

                case ReceveirPackets.SSendNoAuthorization:
                    ReceiveNoAuthorization(payloadData);
                    break;

                case ReceveirPackets.CProfileReportedSuccessfully:
                    RecProfileReportedSuccessfully(payloadData);
                    break;

                case ReceveirPackets.SNoProfileBio:
                    NoProfileBio(payloadData);
                    break;

                case ReceveirPackets.SNoProfile:
                    NoProfile(payloadData);
                    break;

                case ReceveirPackets.SRecNoTargetBio:
                    NoTargetBio(payloadData);
                    break;

                case ReceveirPackets.SRecNoTargetProfile:
                    NoTargetProfile(payloadData);
                    break;

                case ReceveirPackets.SRecTargetHooks:
                    ReceiveTargetHooks(payloadData);
                    break;

                case ReceveirPackets.SRecNoTargetHooks:
                    NoTargetHooks(payloadData);
                    break;

                case ReceveirPackets.SRecGalleryImageLoaded:
                    ReceiveGalleryImageLoaded(payloadData);
                    break;

                case ReceveirPackets.SRecNoTargetGallery:
                    NoTargetGallery(payloadData);
                    break;

                case ReceveirPackets.SRecProfileGallery:
                    ReceiveProfileGallery(payloadData);
                    break;

                case ReceveirPackets.SRecNoProfileGallery:
                    NoProfileGallery(payloadData);
                    break;

                case ReceveirPackets.SSendProfileNotes:
                    ReceiveProfileNotes(payloadData);
                    break;

                case ReceveirPackets.SSendNoProfileNotes:
                    NoProfileNotes(payloadData);
                    break;

                case ReceveirPackets.SSendVerificationMessage:
                    ReceiveVerificationMessage(payloadData);
                    break;

                case ReceveirPackets.SSendPasswordModificationForm:
                    ReceivePasswordModificationForm(payloadData);
                    break;

                case ReceveirPackets.SSendOOC:
                    ReceiveProfileOOC(payloadData);
                    break;

                case ReceveirPackets.SSendTargetOOC:
                    ReceiveTargetOOCInfo(payloadData);
                    break;

                case ReceveirPackets.SSendNoOOCInfo:
                    ReceiveNoOOCInfo(payloadData);
                    break;

                case ReceveirPackets.SSendNoTargetOOCInfo:
                    ReceiveNoTargetOOCInfo(payloadData);
                    break;

                case ReceveirPackets.ReceiveConnections:
                    ReceiveConnections(payloadData);
                    break;

                default:
                    Console.WriteLine($"Unhandled packet type: {packetType}");
                    break;
            }
        }

        private static void HandleWelcomeMessage(byte[] data)
        {
            var payload = DeserializePayload<WelcomeMessagePayload>(data);
            if (payload != null)
            {
                plugin.UpdateStatus();
                Console.WriteLine($"Received Welcome Message: {payload.Message}");
            }
        }

        private static void HandleLoginStatus(byte[] data)
        {
            var payload = DeserializePayload<SendStatusMessagePayload>(data);
            if (payload != null)
            {
                Console.WriteLine($"Login Status for {payload.Username}: {payload.Status}");
            }
        }
        public static void ReceiveProfileBio(byte[] data)
        {
            var payload = DeserializePayload<SendProfileBioPayload>(data);
            if (payload != null)
            {
                ProfileWindow.ExistingBio = true;
                ProfileWindow.currentAvatarImg = Plugin.TextureProvider.CreateFromImageAsync(payload.Avatar).Result;
                ProfileWindow.avatarBytes = payload.Avatar;

                // Updating bio fields with the received data
                ProfileWindow.bioFieldsArr[(int)Defines.BioFieldTypes.name] = payload.Name.Replace("''", "'");
                ProfileWindow.bioFieldsArr[(int)Defines.BioFieldTypes.race] = payload.Race.Replace("''", "'");
                ProfileWindow.bioFieldsArr[(int)Defines.BioFieldTypes.gender] = payload.Gender.Replace("''", "'");
                ProfileWindow.bioFieldsArr[(int)Defines.BioFieldTypes.age] = payload.Age.Replace("''", "'");
                ProfileWindow.bioFieldsArr[(int)Defines.BioFieldTypes.height] = payload.Height.Replace("''", "'");
                ProfileWindow.bioFieldsArr[(int)Defines.BioFieldTypes.weight] = payload.Weight.Replace("''", "'");
                ProfileWindow.bioFieldsArr[(int)Defines.BioFieldTypes.afg] = payload.AtFirstGlance.Replace("''", "'");

                // Updating alignment and personality fields
                ProfileWindow.currentAlignment = payload.Alignment;
                ProfileWindow.currentPersonality_1 = payload.Personality1;
                ProfileWindow.currentPersonality_2 = payload.Personality2;
                ProfileWindow.currentPersonality_3 = payload.Personality3;

                // Updating UI statuses and clearing old data
                BioLoadStatus = 1;
                ProfileWindow.ClearOnLoad();
            }
        }

        public static void ReceiveTargetBio(byte[] data)
        {
            var payload = DeserializePayload<SendTargetBioPayload>(data);
            if (payload != null)
            {
                TargetWindow.currentAvatarImg = Plugin.TextureProvider.CreateFromImageAsync(payload.Avatar).Result;
                TargetWindow.characterEditName = payload.Name.Replace("''", "'");
                TargetWindow.characterEditRace = payload.Race.Replace("''", "'");
                TargetWindow.characterEditGender = payload.Gender.Replace("''", "'");
                TargetWindow.characterEditAge = payload.Age.Replace("''", "'");
                TargetWindow.characterEditHeight = payload.Height.Replace("''", "'");
                TargetWindow.characterEditWeight = payload.Weight.Replace("''", "'");
                TargetWindow.characterEditAfg = payload.AtFirstGlance.Replace("''", "'");

                var alignmentImage = Defines.AlignementIcon(payload.Alignment);
                var personality1Image = Defines.PersonalityIcon(payload.Personality1);
                var personality2Image = Defines.PersonalityIcon(payload.Personality2);
                var personality3Image = Defines.PersonalityIcon(payload.Personality3);

                if (alignmentImage != null) { TargetWindow.alignmentImg = alignmentImage; }
                if (personality1Image != null) { TargetWindow.personalityImg1 = personality1Image; }
                if (personality2Image != null) { TargetWindow.personalityImg2 = personality2Image; }
                if (personality3Image != null) { TargetWindow.personalityImg3 = personality3Image; }

                var (text, desc) = Defines.AlignmentVals[payload.Alignment];
                var (textpers1, descpers1) = Defines.PersonalityValues[payload.Personality1];
                var (textpers2, descpers2) = Defines.PersonalityValues[payload.Personality2];
                var (textpers3, descpers3) = Defines.PersonalityValues[payload.Personality3];

                TargetWindow.alignmentTooltip = $"{text}: \n{desc}";
                TargetWindow.personality1Tooltip = $"{textpers1}: \n{descpers1}";
                TargetWindow.personality2Tooltip = $"{textpers2}: \n{descpers2}";
                TargetWindow.personality3Tooltip = $"{textpers3}: \n{descpers3}";

                TargetWindow.existingAvatarBytes = payload.Avatar;
                TargetWindow.ExistingBio = true;
                TargetBioLoadStatus = 1;
            }
        }

        public static void RecBookmarks(byte[] data)
        {
            var payload = DeserializePayload<SendBookmarksPayload>(data);
            if (payload != null)
            {
                plugin.OpenBookmarksWindow();
                BookmarksWindow.profiles.Clear();
                string[] bookmarkSplit = payload.Bookmark.Replace("|||", "~").Split('~');
                foreach (var bookmark in bookmarkSplit)
                {
                    string characterName = Regex.Match(bookmark, @"<bookmarkName>(.*?)</bookmarkName>").Groups[1].Value;
                    string characterWorld = Regex.Match(bookmark, @"<bookmarkWorld>(.*?)</bookmarkWorld>").Groups[1].Value;
                    BookmarksWindow.profiles.Add(characterName, characterWorld);
                }
                BookmarkLoadStatus = 1;
            }
        }

        public static void ReceiveProfileHooks(byte[] data)
        {
            var payload = DeserializePayload<SendHooksPayload>(data);
            if (payload != null)
            {
                ProfileWindow.ExistingHooks = true;
                ProfileWindow.HookNames = new string[payload.Hooks.Count];
                ProfileWindow.HookContents = new string[payload.Hooks.Count];

                int i = 0;
                foreach (var hook in payload.Hooks)
                {
                    ProfileWindow.HookNames[i] = hook.Value.Item1;
                    ProfileWindow.HookContents[i] = hook.Value.Item2;
                    i++;
                }
                HooksLoadStatus = 1;
                ProfileWindow.ClearOnLoad();
            }
        }

        public static void ReceiveProfileStory(byte[] data)
        {
            var payload = DeserializePayload<SendProfileStoryPayload>(data);
            if (payload != null)
            {
                ProfileWindow.ExistingStory = true;
                ProfileWindow.storyTitle = payload.StoryTitle;
                int i = 0;
                foreach (var chapter in payload.Chapters)
                {
                    ProfileWindow.ChapterNames[i] = chapter.Value.Item1;
                    ProfileWindow.ChapterContents[i] = chapter.Value.Item2;
                    ProfileWindow.storyChapterExists[i] = true;
                    i++;
                }
                StoryLoadStatus = 1;
                ProfileWindow.ClearOnLoad();
            }
        }

        public static void ReceiveTargetStory(byte[] data)
        {
            var payload = DeserializePayload<SendTargetStoryPayload>(data);
            if (payload != null)
            {
                TargetWindow.ExistingStory = true;
                TargetWindow.storyTitle = payload.StoryTitle;
                int i = 0;
                foreach (var chapter in payload.Chapters)
                {
                    TargetWindow.ChapterTitle[i] = chapter.Value.Item1;
                    TargetWindow.ChapterContent[i] = chapter.Value.Item2;
                    TargetWindow.ChapterExists[i] = true;
                    i++;
                }
                TargetStoryLoadStatus = 1;
            }
        }

        public static void NoProfileStory(byte[] data)
        {
            ProfileWindow.ExistingStory = false;
            for (int i = 0; i < ProfileWindow.ChapterNames.Length; i++)
            {
                ProfileWindow.ChapterNames[i] = string.Empty;
                ProfileWindow.ChapterContents[i] = string.Empty;
            }
            StoryLoadStatus = 0;
            ProfileWindow.ClearOnLoad();
        }

        public static void ReceiveNoAuthorization(byte[] data)
        {
            MainPanel.statusColor = new Vector4(255, 0, 0, 255);
            MainPanel.status = "Unauthorized Access to Profile.";
        }

        public static void RecProfileReportedSuccessfully(byte[] data)
        {
            ReportWindow.reportStatus = "Profile reported successfully. We are on it!";
        }

        public static void NoProfileBio(byte[] data)
        {
            ProfileWindow.ClearUI();
            ProfileWindow.currentAvatarImg = Defines.UICommonImage(Defines.CommonImageTypes.avatarHolder);
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
            ProfileWindow.ExistingBio = false;
            BioLoadStatus = 0;
            ProfileWindow.ClearOnLoad();
        }

        public static void NoProfile(byte[] data)
        {
            ProfileWindow.addProfile = false;
            ProfileWindow.editProfile = false;
            ProfileWindow.ClearUI();
            plugin.OpenProfileWindow();
            ProfileWindow.ExistingProfile = false;
            ProfileWindow.ClearOnLoad();
        }

        public static void NoTargetBio(byte[] data)
        {
            TargetWindow.ExistingBio = false;
            TargetBioLoadStatus = 0;
        }

        public static void NoTargetProfile(byte[] data)
        {
            TargetWindow.characterName = string.Empty;
            TargetWindow.characterWorld = string.Empty;
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
        }

        public static void ReceiveTargetHooks(byte[] data)
        {
            var payload = DeserializePayload<SendHooksPayload>(data);
            if (payload != null)
            {
                TargetWindow.ExistingHooks = true;
                TargetWindow.hookEditCount = payload.Hooks.Count;

                int i = 0;
                foreach (var hook in payload.Hooks)
                {
                    TargetWindow.HookNames[i] = hook.Value.Item1;
                    TargetWindow.HookContents[i] = hook.Value.Item2;
                    i++;
                }
                TargetHooksLoadStatus = 1;
            }
        }

        public static void NoTargetHooks(byte[] data)
        {
            TargetWindow.ExistingHooks = false;
            TargetHooksLoadStatus = 0;
        }

        public static void ReceiveGalleryImageLoaded(byte[] data)
        {
            var payload = DeserializePayload<SendImageLoadedPayload>(data);
            if (payload != null)
            {
                TargetWindow.loading = $"Gallery Image {payload.Index}";
                TargetWindow.currentInd = payload.Index;
            }
        }

        public static void NoTargetGallery(byte[] data)
        {
            TargetWindow.ExistingGallery = false;
            TargetGalleryLoadStatus = 0;
        }

        public static void ReceiveProfileGallery(byte[] data)
        {
            var payload = DeserializePayload<SendGalleryImagesPayload>(data);
            if (payload != null)
            {
                ProfileWindow.galleryImageCount = payload.Urls.Count;
                for (int i = 0; i < payload.Urls.Count; i++)
                {
                    Imaging.DownloadProfileImage(true, payload.Urls[i], payload.ProfileId, payload.NSFWImages[i], payload.TriggerImages[i], plugin, i);
                    ProfileWindow.ImageExists[i] = true;
                    ProfileWindow.loading = $"Gallery Image: {i}";
                }
                ProfileWindow.ExistingGallery = true;
                ProfileWindow.ClearOnLoad();
            }
        }

        public static void NoProfileGallery(byte[] data)
        {
            for (int i = 0; i < 30; i++)
            {
                ProfileWindow.galleryImages[i] = ProfileWindow.pictureTab;
                ProfileWindow.imageURLs[i] = string.Empty;
            }
            ProfileWindow.ImageExists[0] = true;
            ProfileWindow.galleryImageCount = 2;
            ProfileWindow.ExistingGallery = false;
            GalleryLoadStatus = 0;
            ProfileWindow.ClearOnLoad();
        }

        public static void ReceiveProfileNotes(byte[] data)
        {
            var payload = DeserializePayload<SendProfileNotesPayload>(data);
            if (payload != null)
            {
                TargetWindow.profileNotes = payload.Notes;
                TargetNotesLoadStatus = 1;
            }
        }

        public static void NoProfileNotes(byte[] data)
        {
            TargetWindow.profileNotes = string.Empty;
            TargetNotesLoadStatus = 0;
        }

        public static void ReceiveVerificationMessage(byte[] data)
        {
            MainPanel.status = "Successfully Registered!";
            MainPanel.statusColor = new Vector4(0, 255, 0, 255);
            plugin.OpenVerificationWindow();
        }

        public static void ReceivePasswordModificationForm(byte[] data)
        {
            var payload = DeserializePayload<SendPasswordModificationFormPayload>(data);
            if (payload != null)
            {
                RestorationWindow.restorationEmail = payload.Email;
                plugin.OpenRestorationWindow();
            }
        }

        public static void ReceiveProfileOOC(byte[] data)
        {
            var payload = DeserializePayload<SendOocPayload>(data);
            if (payload != null)
            {
                ProfileWindow.ExistingOOC = true;
                ProfileWindow.oocInfo = payload.Ooc;
                OOCLoadStatus = 1;
                ProfileWindow.ClearOnLoad();
            }
        }

        public static void ReceiveNoOOCInfo(byte[] data)
        {
            ProfileWindow.oocInfo = string.Empty;
            ProfileWindow.ExistingOOC = false;
            ProfileWindow.ClearOnLoad();
        }

        public static void ReceiveTargetOOCInfo(byte[] data)
        {
            var payload = DeserializePayload<SendOocPayload>(data);
            if (payload != null)
            {
                TargetWindow.oocInfo = payload.Ooc;
                TargetWindow.ExistingOOC = true;
            }
        }

        public static void ReceiveNoTargetOOCInfo(byte[] data)
        {
            TargetWindow.oocInfo = string.Empty;
            TargetWindow.ExistingOOC = false;
        }

        public static void ReceiveConnections(byte[] data)
        {
            var payload = DeserializePayload<SendConnectionsPayload>(data);
            if (payload != null)
            {
                ConnectionsWindow.connetedProfileList.Clear();
                ConnectionsWindow.sentProfileRequests.Clear();
                ConnectionsWindow.receivedProfileRequests.Clear();
                ConnectionsWindow.blockedProfileRequests.Clear();

                foreach (var connection in payload.Connections)
                {
                    var requester = Tuple.Create(connection.RequesterName, connection.RequesterWorld);
                    var receiver = Tuple.Create(connection.ReceiverName, connection.ReceiverWorld);

                    if (connection.IsReceiver)
                    {
                        if (connection.Status == (int)Defines.ConnectionStatus.pending)
                        {
                            ConnectionsWindow.receivedProfileRequests.Add(requester);
                        }
                        else if (connection.Status == (int)Defines.ConnectionStatus.accepted)
                        {
                            ConnectionsWindow.connetedProfileList.Add(requester);
                        }
                        else if (connection.Status == (int)Defines.ConnectionStatus.blocked)
                        {
                            ConnectionsWindow.blockedProfileRequests.Add(requester);
                        }
                    }
                    else
                    {
                        if (connection.Status == (int)Defines.ConnectionStatus.pending)
                        {
                            ConnectionsWindow.sentProfileRequests.Add(receiver);
                        }
                        else if (connection.Status == (int)Defines.ConnectionStatus.accepted)
                        {
                            ConnectionsWindow.connetedProfileList.Add(receiver);
                        }
                        else if (connection.Status == (int)Defines.ConnectionStatus.blocked)
                        {
                            ConnectionsWindow.blockedProfileRequests.Add(receiver);
                        }
                    }
                }

                plugin.OpenConnectionsWindow();
                plugin.newConnection = false;
                plugin.CheckConnectionsRequestStatus();
            }
        }

        private static T DeserializePayload<T>(byte[] data)
        {
            try
            {
                string json = Encoding.UTF8.GetString(data);
                return JsonSerializer.Deserialize<T>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deserializing payload: {ex.Message}");
                return default(T);
            }
        }
    }
}
