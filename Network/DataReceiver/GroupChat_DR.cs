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
    // Group chat message, pin and lock packets. Split out of DataReceiver.
    internal class GroupChat_DR
    {
        // Receives a batch of group chat messages (used when loading chat history). Each message includes sender info, content, timestamp, and edit/pin status.
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
                            // Load avatar texture in background to avoid blocking packet processing
                            var capturedMsg = chatMessage;
                            var capturedAvatarBytes = avatarBytes;
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    var texture = await Plugin.TextureProvider.CreateFromImageAsync(capturedAvatarBytes);
                                    if (texture != null) { capturedMsg.avatar = texture; userAvatarCache[capturedMsg.senderUserID] = texture; }
                                }
                                catch { }
                            });
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

        // Receives a single new chat message broadcast in real-time (as opposed to the bulk history load). Inserts it into the correct channel's message list.
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
                byte[] broadcastAvatarBytes = null;
                bool hasAvatar = buffer.ReadBool();
                if (hasAvatar)
                {
                    int avatarLength = buffer.ReadInt();
                    broadcastAvatarBytes = buffer.ReadBytes(avatarLength);
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

                // Load avatar in background after message is created
                if (broadcastAvatarBytes != null && broadcastAvatarBytes.Length > 0)
                {
                    var capturedChatMsg = chatMessage;
                    var capturedAvBytes = broadcastAvatarBytes;
                    _ = Task.Run(async () =>
                    {
                        try { capturedChatMsg.avatar = await Plugin.TextureProvider.CreateFromImageAsync(capturedAvBytes); } catch { }
                    });
                }

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
                        var capturedPinnedMsg = msg;
                        var capturedPinnedAvBytes = avatarBytes;
                        _ = Task.Run(async () => { try { capturedPinnedMsg.avatar = await Plugin.TextureProvider.CreateFromImageAsync(capturedPinnedAvBytes); } catch { } });
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

        internal static Dictionary<int, IDalamudTextureWrap> messageAvatarCache = new Dictionary<int, IDalamudTextureWrap>();

        internal static async Task LoadMessageAvatar(GroupChatMessage message, int profileID)
        {
            try
            {
                // Check cache first
                if (messageAvatarCache.ContainsKey(profileID))
                {
                    message.avatar = messageAvatarCache[profileID];
                    return;
                }

                // TODO: Implement profile data fetching from server for avatars. For now, use default avatar placeholder. Future: Request profile avatar bytes from server using profileID
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
    }
}
