using AbsoluteRP.Helpers;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;

namespace AbsoluteRP.Windows.Social
{
    public class GroupChatWindow : Window, IDisposable
    {
        // Static reference to current instance for DataReceiver
        public static GroupChatWindow CurrentInstance { get; private set; }

        private Plugin plugin;
        private Group currentGroup;

        // UI State
        private int selectedCategoryIndex = -1;
        private GroupChannel selectedChannel = null;
        private string messageInput = string.Empty;
        private List<GroupChatMessage> messages = new List<GroupChatMessage>();
        private bool autoScroll = true;
        private GroupChatMessage editingMessage = null;

        // Message cache per channel
        private Dictionary<int, List<GroupChatMessage>> channelMessageCache = new Dictionary<int, List<GroupChatMessage>>();

        public GroupChatWindow(Plugin plugin, Group group) : base($"{group.name} - Group Chat###GroupChat_{group.groupID}")
        {
            this.plugin = plugin;
            this.currentGroup = group;
            CurrentInstance = this; // Set static reference

            Size = new Vector2(800, 600);
            SizeCondition = ImGuiCond.FirstUseEver;

            // Select first channel by default
            if (group.categories != null && group.categories.Count > 0 &&
                group.categories[0].channels != null && group.categories[0].channels.Count > 0)
            {
                selectedChannel = group.categories[0].channels[0];
                LoadMessages(selectedChannel.id);
            }
        }

        public void Dispose()
        {
            channelMessageCache.Clear();
            if (CurrentInstance == this)
                CurrentInstance = null;
        }

        public void SetGroup(Group group)
        {
            currentGroup = group;
            WindowName = $"{group.name} - Group Chat###GroupChat_{group.groupID}";
        }

        public override void Draw()
        {
            if (currentGroup == null) return;

            // Two-column layout: Channels | Chat
            using (var table = ImRaii.Table("GroupChatLayout", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV))
            {
                if (!table) return;

                // Column setup
                ImGui.TableSetupColumn("Channels", ImGuiTableColumnFlags.WidthFixed, 200f * ImGui.GetIO().FontGlobalScale);
                ImGui.TableSetupColumn("Chat", ImGuiTableColumnFlags.WidthStretch);

                ImGui.TableNextRow();

                // Left column: Categories & Channels
                ImGui.TableSetColumnIndex(0);
                DrawChannelList();

                // Right column: Chat messages + input
                ImGui.TableSetColumnIndex(1);
                DrawChatArea();
            }
        }

        private void DrawChannelList()
        {
            ImGui.BeginChild("ChannelList", new Vector2(-1, -1), false);

            // Group name header
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1f), currentGroup.name);
            ImGui.Separator();

            if (currentGroup.categories == null || currentGroup.categories.Count == 0)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "No channels available");
                ImGui.EndChild();
                return;
            }

            // Draw categories with channels
            for (int catIdx = 0; catIdx < currentGroup.categories.Count; catIdx++)
            {
                var category = currentGroup.categories[catIdx];

                // Category header (collapsible)
                ImGuiTreeNodeFlags nodeFlags = ImGuiTreeNodeFlags.SpanFullWidth;
                if (!category.collapsed)
                    nodeFlags |= ImGuiTreeNodeFlags.DefaultOpen;

                bool categoryOpen = ImGui.CollapsingHeader($"▼ {category.name.ToUpper()}##cat_{catIdx}", nodeFlags);

                // Right-click menu for category
                if (ImGui.BeginPopupContextItem($"categoryContext_{catIdx}"))
                {
                    var permissions = GetCurrentUserPermissions();
                    bool isOwner = IsCurrentUserOwner();

                    if (isOwner || (permissions != null && permissions.canCreateForum))
                    {
                        if (ImGui.MenuItem("Create Channel"))
                        {
                            // TODO: Open create channel dialog
                        }
                    }

                    if (isOwner || (permissions != null && permissions.canEditCategory))
                    {
                        if (ImGui.MenuItem("Edit Category"))
                        {
                            // TODO: Open edit category dialog
                        }
                    }

                    if (isOwner || (permissions != null && permissions.canDeleteCategory))
                    {
                        if (ImGui.MenuItem("Delete Category"))
                        {
                            // TODO: Confirm and delete category
                        }
                    }

                    ImGui.EndPopup();
                }

                if (categoryOpen && category.channels != null)
                {
                    ImGui.Indent(10f * ImGui.GetIO().FontGlobalScale);

                    foreach (var channel in category.channels)
                    {
                        DrawChannelItem(channel);
                    }

                    ImGui.Unindent(10f * ImGui.GetIO().FontGlobalScale);
                }

                ImGui.Spacing();
            }

            ImGui.EndChild();
        }

        private void DrawChannelItem(GroupChannel channel)
        {
            bool isSelected = selectedChannel != null && selectedChannel.id == channel.id;

            // Channel icon based on type
            string icon = channel.channelType == 1 ? "📢" : "#";

            // Unread indicator
            string unreadBadge = channel.unreadCount > 0 ? $" ({channel.unreadCount})" : "";

            // Color coding
            Vector4 color = isSelected ? new Vector4(1f, 1f, 1f, 1f) : new Vector4(0.7f, 0.7f, 0.7f, 1f);
            if (channel.unreadCount > 0)
                color = new Vector4(1f, 1f, 1f, 1f);

            ImGui.PushStyleColor(ImGuiCol.Text, color);

            if (ImGui.Selectable($"{icon} {channel.name}{unreadBadge}##channel_{channel.id}", isSelected))
            {
                SelectChannel(channel);
            }

            ImGui.PopStyleColor();

            // Right-click menu
            if (ImGui.BeginPopupContextItem($"channelContext_{channel.id}"))
            {
                var permissions = GetCurrentUserPermissions();
                bool isOwner = IsCurrentUserOwner();

                if (ImGui.MenuItem("Mark as Read"))
                {
                    MarkChannelAsRead(channel);
                }
                if (ImGui.MenuItem("Mute Notifications"))
                {
                    // TODO: Implement mute
                }

                // Permission-based options
                if (isOwner || (permissions != null && permissions.canEditForum))
                {
                    if (ImGui.MenuItem("Edit Channel"))
                    {
                        // TODO: Open edit channel dialog
                    }
                }

                if (isOwner || (permissions != null && permissions.canDeleteForum))
                {
                    if (ImGui.MenuItem("Delete Channel"))
                    {
                        // TODO: Confirm and delete channel
                    }
                }

                ImGui.EndPopup();
            }
        }

        private void DrawChatArea()
        {
            if (selectedChannel == null)
            {
                ImGui.BeginChild("NoChannel", new Vector2(-1, -1), false);
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "Select a channel to start chatting");
                ImGui.EndChild();
                return;
            }

            // Channel header
            ImGui.BeginChild("ChatHeader", new Vector2(-1, 40f * ImGui.GetIO().FontGlobalScale), true);
            ImGui.Text($"# {selectedChannel.name}");
            if (!string.IsNullOrEmpty(selectedChannel.description))
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), $" - {selectedChannel.description}");
            }
            ImGui.EndChild();

            // Messages area
            float inputHeight = 80f * ImGui.GetIO().FontGlobalScale;
            ImGui.BeginChild("Messages", new Vector2(-1, -inputHeight), true, ImGuiWindowFlags.HorizontalScrollbar);

            DrawMessages();

            // Auto-scroll to bottom
            if (autoScroll)
                ImGui.SetScrollHereY(1.0f);

            ImGui.EndChild();

            // Input area
            ImGui.BeginChild("Input", new Vector2(-1, -1), true);
            DrawMessageInput();
            ImGui.EndChild();
        }

        private void DrawMessages()
        {
            if (messages == null || messages.Count == 0)
            {
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "No messages yet. Start the conversation!");
                return;
            }

            // Display messages (reverse order, newest at bottom)
            var sortedMessages = messages.OrderBy(m => m.timestamp).ToList();

            foreach (var message in sortedMessages)
            {
                if (message.deleted)
                    continue;

                DrawMessageItem(message);
            }
        }

        private void DrawMessageItem(GroupChatMessage message)
        {
            ImGui.PushID($"msg_{message.messageID}");

            // Check if this is the current user's message
            bool isOwnMessage = message.senderUserID == DataSender.userID;

            // Try to get avatar from group members list
            IDalamudTextureWrap avatarTexture = null;
            if (currentGroup != null && currentGroup.members != null)
            {
                var member = currentGroup.members.FirstOrDefault(m => m.userID == message.senderUserID);
                if (member != null && member.avatar != null)
                {
                    avatarTexture = member.avatar;
                }
            }

            // Draw avatar (either from member data or placeholder)
            if (avatarTexture != null && avatarTexture.Handle != IntPtr.Zero)
            {
                ImGui.Image(avatarTexture.Handle, new Vector2(40, 40));
                ImGui.SameLine();
            }
            else
            {
                // Draw placeholder avatar circle
                var drawList = ImGui.GetWindowDrawList();
                var cursorPos = ImGui.GetCursorScreenPos();
                var center = new Vector2(cursorPos.X + 20, cursorPos.Y + 20);
                drawList.AddCircleFilled(center, 20, ImGui.GetColorU32(new Vector4(0.3f, 0.3f, 0.3f, 1f)));

                // Draw first letter of name
                if (!string.IsNullOrEmpty(message.senderName))
                {
                    var letter = message.senderName[0].ToString().ToUpper();
                    var textSize = ImGui.CalcTextSize(letter);
                    drawList.AddText(new Vector2(center.X - textSize.X / 2, center.Y - textSize.Y / 2),
                        ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), letter);
                }

                ImGui.Dummy(new Vector2(40, 40));
                ImGui.SameLine();
            }

            ImGui.BeginGroup();

            // Sender name and timestamp
            var dateTime = DateTimeOffset.FromUnixTimeMilliseconds(message.timestamp).LocalDateTime;
            string timeStr = dateTime.ToString("HH:mm");
            Vector4 nameColor = isOwnMessage ? new Vector4(0.4f, 0.7f, 1f, 1f) : new Vector4(0.9f, 0.9f, 0.9f, 1f);

            ImGui.PushStyleColor(ImGuiCol.Text, nameColor);
            ImGui.Text(message.senderName);
            ImGui.PopStyleColor();

            ImGui.SameLine();
            ImGui.TextDisabled(timeStr);

            // Edited indicator
            if (message.isEdited)
            {
                ImGui.SameLine();
                ImGui.TextDisabled("(edited)");
            }

            // Message content
            ImGui.TextWrapped(message.messageContent);

            ImGui.EndGroup();

            // Make the entire message clickable for context menu
            var messageMin = ImGui.GetItemRectMin();
            var messageMax = ImGui.GetItemRectMax();
            var messageSize = new Vector2(messageMax.X - messageMin.X, messageMax.Y - messageMin.Y);

            // Invisible button to capture right-clicks
            ImGui.SetCursorScreenPos(messageMin);
            ImGui.InvisibleButton($"msgArea_{message.messageID}", messageSize);

            // Right-click context menu (own messages only)
            if (isOwnMessage && ImGui.BeginPopupContextItem($"msgContext_{message.messageID}"))
            {
                if (ImGui.MenuItem("Edit"))
                {
                    StartEditingMessage(message);
                    ImGui.CloseCurrentPopup();
                }
                if (ImGui.MenuItem("Delete"))
                {
                    DeleteMessage(message);
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.PopID();
        }

        private void DrawMessageInput()
        {
            // If editing, show edit UI
            if (editingMessage != null)
            {
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), $"Editing message:");
                ImGui.SameLine();
                if (ImGui.SmallButton("Cancel"))
                {
                    CancelEditing();
                }
            }

            // Message input
            ImGui.SetNextItemWidth(-80f * ImGui.GetIO().FontGlobalScale);
            if (ImGui.InputTextMultiline("##MessageInput", ref messageInput, 2000, new Vector2(-80f * ImGui.GetIO().FontGlobalScale, 50f * ImGui.GetIO().FontGlobalScale)))
            {
                // Handle input
            }

            // Enter to send
            if (ImGui.IsItemFocused() && ImGui.IsKeyPressed(ImGuiKey.Enter) && !ImGui.GetIO().KeyShift)
            {
                SendOrEditMessage();
                ImGui.SetKeyboardFocusHere(-1); // Keep focus on input
            }

            ImGui.SameLine();

            // Send button
            if (ImGui.Button(editingMessage != null ? "Save" : "Send", new Vector2(70f * ImGui.GetIO().FontGlobalScale, 50f * ImGui.GetIO().FontGlobalScale)))
            {
                SendOrEditMessage();
            }
        }

        private void SelectChannel(GroupChannel channel)
        {
            selectedChannel = channel;
            LoadMessages(channel.id);
            MarkChannelAsRead(channel);
        }

        private void LoadMessages(int channelID)
        {
            // Check cache first
            if (channelMessageCache.TryGetValue(channelID, out var cachedMessages))
            {
                messages = cachedMessages;
                return;
            }

            // Fetch from server
            messages = new List<GroupChatMessage>();
            DataSender.FetchGroupChatMessages(Plugin.character, currentGroup.groupID, channelID, 50, 0);
        }

        private void SendOrEditMessage()
        {
            if (string.IsNullOrWhiteSpace(messageInput))
                return;

            if (editingMessage != null)
            {
                // Edit existing message
                DataSender.EditGroupChatMessage(Plugin.character, editingMessage.messageID, messageInput);
                editingMessage.messageContent = messageInput;
                editingMessage.isEdited = true;
                editingMessage = null;
            }
            else
            {
                // Send new message
                DataSender.SendGroupChatMessage(Plugin.character, currentGroup.groupID, selectedChannel.id, messageInput);

                // Optimistically add to local list (server will confirm)
                var newMessage = new GroupChatMessage
                {
                    groupID = currentGroup.groupID,
                    channelID = selectedChannel.id,
                    senderUserID = DataSender.userID,
                    senderName = Plugin.character.characterName, // Use actual character name
                    messageContent = messageInput,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    isEdited = false,
                    deleted = false
                };
                messages.Add(newMessage);

                // Update cache
                if (channelMessageCache.ContainsKey(selectedChannel.id))
                    channelMessageCache[selectedChannel.id].Add(newMessage);
            }

            messageInput = string.Empty;
            autoScroll = true;
        }

        private void StartEditingMessage(GroupChatMessage message)
        {
            editingMessage = message;
            messageInput = message.messageContent;
        }

        private void CancelEditing()
        {
            editingMessage = null;
            messageInput = string.Empty;
        }

        private void DeleteMessage(GroupChatMessage message)
        {
            DataSender.DeleteGroupChatMessage(Plugin.character, message.messageID);
            message.deleted = true;
        }

        private void MarkChannelAsRead(GroupChannel channel)
        {
            if (messages.Count == 0) return;

            var lastMessage = messages.OrderByDescending(m => m.timestamp).FirstOrDefault();
            if (lastMessage != null)
            {
                DataSender.UpdateChatReadStatus(Plugin.character, channel.id, lastMessage.messageID, lastMessage.timestamp);
                channel.unreadCount = 0;
            }
        }

        // Called by DataReceiver when messages are received from server
        public void OnMessagesReceived(int channelID, List<GroupChatMessage> receivedMessages)
        {
            if (channelID == selectedChannel?.id)
            {
                messages = receivedMessages;
                autoScroll = true;
            }

            // Update cache
            channelMessageCache[channelID] = receivedMessages;
        }

        // Called by DataReceiver when a new message broadcast is received
        public void OnNewMessageBroadcast(GroupChatMessage message)
        {
            // Add to appropriate channel's messages
            if (message.channelID == selectedChannel?.id)
            {
                messages.Add(message);
                autoScroll = true;
            }

            // Update cache
            if (channelMessageCache.TryGetValue(message.channelID, out var cachedMessages))
            {
                cachedMessages.Add(message);
            }

            // Update unread count if not currently viewing this channel
            if (selectedChannel == null || message.channelID != selectedChannel.id)
            {
                // Find the channel and increment unread count
                foreach (var category in currentGroup.categories)
                {
                    var channel = category.channels?.FirstOrDefault(c => c.id == message.channelID);
                    if (channel != null)
                    {
                        channel.unreadCount++;
                        break;
                    }
                }
            }
        }

        // Helper methods for permissions
        private bool IsCurrentUserOwner()
        {
            if (currentGroup?.members == null || currentGroup.ProfileData == null)
                return false;

            var currentMember = currentGroup.members.FirstOrDefault(m =>
                m.profileID == currentGroup.ProfileData.id);

            return currentMember != null && currentMember.owner;
        }

        private GroupRankPermissions GetCurrentUserPermissions()
        {
            if (currentGroup?.members == null || currentGroup.ProfileData == null)
                return null;

            var currentMember = currentGroup.members.FirstOrDefault(m =>
                m.profileID == currentGroup.ProfileData.id);

            if (currentMember == null)
                return null;

            // Owner has all permissions
            if (currentMember.owner)
            {
                return new GroupRankPermissions
                {
                    canCreateCategory = true,
                    canEditCategory = true,
                    canDeleteCategory = true,
                    canCreateForum = true,
                    canEditForum = true,
                    canDeleteForum = true,
                    canSendMessages = true,
                    canInvite = true,
                    canKick = true,
                    canBan = true,
                    canPromote = true,
                    canDemote = true,
                    canCreateAnnouncement = true,
                    canReadMessages = true,
                    canDeleteOthersMessages = true,
                    canPinMessages = true,
                    canLockCategory = true,
                    canLockForum = true,
                    canMuteForum = true
                };
            }

            return currentMember.rank?.permissions;
        }
    }
}
