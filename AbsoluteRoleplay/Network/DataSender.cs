using AbsoluteRoleplay.Packets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace Networking
{
    

    public class DataSender
    {
        private static ClientWebSocket _client = new ClientWebSocket();

        public static async Task<bool> ConnectAsync(string serverUri)
        {
            try
            {
                // Ensure using wss for secure connection
                serverUri = serverUri.Replace("ws://", "wss://");

                await _client.ConnectAsync(new Uri(serverUri), CancellationToken.None);
                return _client.State == WebSocketState.Open;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection failed: {ex.Message}");
                return false;
            }
        }

        // Send packet over WebSocket
        public static async Task SendPacketAsync<T>(int packetId, T payload)
        {
            if (_client.State != WebSocketState.Open)
            {
                Console.WriteLine("WebSocket is not connected.");
                return;
            }

            // Convert packetId to bytes (using little-endian)
            byte[] packetIdBytes = BitConverter.GetBytes(packetId);

            // Serialize the payload to JSON and convert to byte array
            byte[] payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);

            // Combine packetId and payload into one byte array
            byte[] dataToSend = new byte[packetIdBytes.Length + payloadBytes.Length];
            Buffer.BlockCopy(packetIdBytes, 0, dataToSend, 0, packetIdBytes.Length);
            Buffer.BlockCopy(payloadBytes, 0, dataToSend, packetIdBytes.Length, payloadBytes.Length);

            // Send the combined packet (packetId + payload)
            await _client.SendAsync(new ArraySegment<byte>(dataToSend), WebSocketMessageType.Binary, true, CancellationToken.None);
            Console.WriteLine("Packet sent.");
        }


        // Method to send login data to the server
        public static async Task SendLoginAsync(string username, string password, string characterName, string characterWorld)
        {
            var payload = new LoginPayload
            {
                Username = username,
                Password = password,
                CharacterName = characterName,
                CharacterWorld = characterWorld
            };

            await SendPacketAsync((int)Packets.SenderPackets.CLogin, payload);
        }

        // Method to send profile creation data to the server
        public static async Task SendCreateProfileAsync(string playerName, string playerWorld)
        {
            var payload = new { PlayerName = playerName, PlayerWorld = playerWorld };
            await SendPacketAsync((int)Packets.SenderPackets.CCreateProfile, payload);
        }

        // Method to fetch profiles from the server
        public static async Task SendFetchProfilesAsync(string characterName, string world)
        {
            var payload = new { CharacterName = characterName, World = world };
            await SendPacketAsync((int)Packets.SenderPackets.CFetchProfile, payload);
        }
        // Method to create profile bio
        public static async Task SendCreateProfileBioAsync(string playerName, string playerServer, byte[] avatarBytes, string name, string race, string gender, string age, string height, string weight, string atFirstGlance, int alignment, int personality_1, int personality_2, int personality_3)
        {
            var payload = new ProfileBioPayload
            {
                PlayerName = playerName,
                PlayerServer = playerServer,
                AvatarBytes = avatarBytes,
                Name = name,
                Race = race,
                Gender = gender,
                Age = age,
                Height = height,
                Weight = weight,
                AtFirstGlance = atFirstGlance,
                Alignment = alignment,
                Personality1 = personality_1,
                Personality2 = personality_2,
                Personality3 = personality_3
            };

            await SendPacketAsync((int)Packets.SenderPackets.CCreateProfileBio, payload);
        }


        // Method to send hooks
        public static async Task SendHooksAsync(string characterName, string characterWorld, List<Tuple<int, string, string>> hooks)
        {
            var payload = new { CharacterName = characterName, CharacterWorld = characterWorld, Hooks = hooks };
            await SendPacketAsync((int)Packets.SenderPackets.CRecProfileHooks, payload);
        }

        // Method to request target profile from the server
        public static async Task SendRequestTargetProfileAsync(string targetPlayerName, string targetPlayerWorld, string requesterUsername)
        {
            var payload = new { TargetPlayerName = targetPlayerName, TargetPlayerWorld = targetPlayerWorld, RequesterUsername = requesterUsername };
            await SendPacketAsync((int)Packets.SenderPackets.CRecTargetProfileRequest, payload);
        }

        // Method to register a new user
        public static async Task SendRegisterAsync(string username, string password, string email)
        {
            var payload = new { Username = username, Password = password, Email = email };
            await SendPacketAsync((int)Packets.SenderPackets.CRecRegister, payload);
        }

        // Method to send story data
        public static async Task SendStoryAsync(string playerName, string playerWorld, string storyTitle, List<Tuple<string, string>> chapters)
        {
            var payload = new { PlayerName = playerName, PlayerWorld = playerWorld, StoryTitle = storyTitle, Chapters = chapters };
            await SendPacketAsync((int)Packets.SenderPackets.CRecStoryCreation, payload);
        }

        // Method to send a bookmark request
        public static async Task SendBookmarkRequestAsync(string username)
        {
            await SendPacketAsync((int)Packets.SenderPackets.CRecBookmarkRequest, new { Username = username });
        }

        // Method to send player bookmark
        public static async Task SendPlayerBookmarkAsync(string username, string playerName, string playerWorld)
        {
            var payload = new { Username = username, PlayerName = playerName, PlayerWorld = playerWorld };
            await SendPacketAsync((int)Packets.SenderPackets.CRecPlayerBookmark, payload);
        }

        // Method to remove player bookmark
        public static async Task SendRemovePlayerBookmarkAsync(string username, string playerName, string playerWorld)
        {
            var payload = new { Username = username, PlayerName = playerName, PlayerWorld = playerWorld };
            await SendPacketAsync((int)Packets.SenderPackets.CRecRemovePlayerBookmark, payload);
        }

        // Method to send gallery image data
        public static async Task SendGalleryImageAsync(string username, string playerName, string playerWorld, string imageUrl, bool isNSFW, bool isTrigger, int index)
        {
            var payload = new { Username = username, PlayerName = playerName, PlayerWorld = playerWorld, ImageUrl = imageUrl, IsNSFW = isNSFW, IsTrigger = isTrigger, Index = index };
            await SendPacketAsync((int)Packets.SenderPackets.CRecGalleryImage, payload);
        }

        // Method to send gallery image request
        public static async Task SendGalleryImageRequestAsync(string playerName, string playerWorld, int imageIndex)
        {
            var payload = new { PlayerName = playerName, PlayerWorld = playerWorld, ImageIndex = imageIndex };
            await SendPacketAsync((int)Packets.SenderPackets.CRecGalleryImagesRequest, payload);
        }

        // Method to request gallery image removal
        public static async Task SendGalleryRemoveRequestAsync(string playerName, string playerWorld, int imageIndex, int count)
        {
            var payload = new { PlayerName = playerName, PlayerWorld = playerWorld, ImageIndex = imageIndex, GalleryImageCount = count};
            await SendPacketAsync((int)Packets.SenderPackets.CRecGalleryRemoveImageRequest, payload);
        }

        // Method to send a report profile request
        public static async Task SendReportProfileAsync(string reporterUsername, string playerName, string playerWorld, string reportDetails)
        {
            var payload = new { ReporterUsername = reporterUsername, PlayerName = playerName, PlayerWorld = playerWorld, ReportDetails = reportDetails };
            await SendPacketAsync((int)Packets.SenderPackets.CReportProfile, payload);
        }

        // Method to send profile notes
        public static async Task SendProfileNotesAsync(string username, string characterName, string characterWorld, string notes)
        {
            var payload = new { Username = username, CharacterName = characterName, CharacterWorld = characterWorld, Notes = notes };
            await SendPacketAsync((int)Packets.SenderPackets.CAddProfileNotes, payload);
        }

        // Method to submit verification key
        public static async Task SendVerificationKeyAsync(string username, string verificationKey)
        {
            var payload = new { Username = username, VerificationKey = verificationKey };
            await SendPacketAsync((int)Packets.SenderPackets.CRecVerificationKey, payload);
        }

        // Method to send restoration request
        public static async Task SendRestorationRequestAsync(string email)
        {
            await SendPacketAsync((int)Packets.SenderPackets.SSubmitRestorationRequest, new { Email = email });
        }

        // Method to send restoration key
        public static async Task SendRestorationKeyAsync(string email, string password, string restorationKey)
        {
            var payload = new { Email = email, Password = password, RestorationKey = restorationKey };
            await SendPacketAsync((int)Packets.SenderPackets.SRecPasswordChange, payload);
        }


        // Method to send OOC information
        public static async Task SendOOCAsync(string characterName, string characterWorld, string oocInfo)
        {
            var payload = new { CharacterName = characterName, CharacterWorld = characterWorld, OOCInfo = oocInfo };
            await SendPacketAsync((int)Packets.SenderPackets.SSendOOC, payload);
        }


        // Method to send profile configuration
        public static async Task SendProfileConfigurationAsync(bool showProfilePublicly, string playerName, string playerWorld)
        {
            var payload = new { ShowProfilePublicly = showProfilePublicly, PlayerName = playerName, PlayerWorld = playerWorld };
            await SendPacketAsync((int)Packets.SenderPackets.SSendProfileStatus, payload);
        }
        // Method to send profile access update
        public static async Task SendProfileAccessUpdateAsync(string username, string senderName, string senderWorld, string connectionName, string connectionWorld, int status)
        {
            var payload = new { Username = username, SenderName = senderName, SenderWorld = senderWorld, ConnectionName = connectionName, ConnectionWorld = connectionWorld, Status = status };
            await SendPacketAsync((int)Packets.SenderPackets.SSendProfileAccessUpdate, payload);
        }

        // Method to send connection request
        public static async Task SendConnectionsRequestAsync(string username, string playername, string playerworld)
        {
            await SendPacketAsync((int)Packets.SenderPackets.SSendConnectionsRequest, new { Username = username, PlayerName = playername, PlayerWorld = playerworld});
        }

        // Method to send profile status update
        public static async Task SendProfileStatusAsync(string username, string characterName, string characterWorld, bool privateProfile)
        {
            var payload = new { Username = username, CharacterName = characterName, CharacterWorld = characterWorld, Private = privateProfile };
            await SendPacketAsync((int)Packets.SenderPackets.SSendProfileStatus, payload);
        }

    }

    public class LoginPayload
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string CharacterName { get; set; }
        public string CharacterWorld { get; set; }
    }

    public class ProfileBioPayload
    {
        public string PlayerName { get; set; }
        public string PlayerServer { get; set; }
        public byte[] AvatarBytes { get; set; }
        public string Name { get; set; }
        public string Race { get; set; }
        public string Gender { get; set; }
        public string Age { get; set; }
        public string Height { get; set; }
        public string Weight { get; set; }
        public string AtFirstGlance { get; set; }
        public int Alignment { get; set; }
        public int Personality1 { get; set; }
        public int Personality2 { get; set; }
        public int Personality3 { get; set; }
    }

    public class ReceiverPacket<T>
    {
        public int PacketId { get; set; }
        public T Payload { get; set; }
    }
}
