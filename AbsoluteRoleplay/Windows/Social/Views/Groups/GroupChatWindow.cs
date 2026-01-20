using AbsoluteRP;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Social.Views;
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

namespace AbsoluteRP.Windows.Social.Views.Groups
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

        // Chat input resizing
        private float chatInputHeight = 50f;
        private bool isResizingChatInput = false;
        private const float minChatInputHeight = 30f;
        private const float maxChatInputHeight = 300f;

        // Message cache per channel
        private Dictionary<int, List<GroupChatMessage>> channelMessageCache = new Dictionary<int, List<GroupChatMessage>>();

        // Track window open state for audio pause
        private bool wasOpen = false;

        // Edit channel state
        private bool showEditChannelPopup = false;
        private GroupChannel channelBeingEdited = null;
        private string editChannelName = string.Empty;
        private string editChannelDescription = string.Empty;
        private int editChannelType = 0;
        private bool editEveryoneCanView = true;
        private bool editEveryoneCanPost = true;

        public GroupChatWindow(Plugin plugin, Group group) : base($"{group.name} - Group Chat###GroupChat_{group.groupID}")
        {
            this.plugin = plugin;
            currentGroup = group;
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

        public override void OnClose()
        {
            // Stop and dispose all audio players when closing the window
            Misc.CleanupAudioPlayers();
            base.OnClose();
        }

        public void Dispose()
        {
            // Stop and dispose all audio players
            Misc.CleanupAudioPlayers();

            channelMessageCache.Clear();
            if (CurrentInstance == this)
                CurrentInstance = null;
        }

        public void SetGroup(Group group)
        {
            currentGroup = group;
            WindowName = $"{group.name} - Group Chat###GroupChat_{group.groupID}";
        }

        public override void PreDraw()
        {
            // Check if window was just closed (IsOpen changed from true to false)
            if (wasOpen && !IsOpen)
            {
                Misc.PauseAllAudio();
            }
            wasOpen = IsOpen;
            base.PreDraw();
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

            // Draw edit channel popup (outside of table)
            DrawEditChannelPopup();
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

                    if (isOwner || permissions != null && permissions.canCreateForum)
                    {
                        if (ImGui.MenuItem("Create Channel"))
                        {
                            // TODO: Open create channel dialog
                        }
                    }

                    if (isOwner || permissions != null && permissions.canEditCategory)
                    {
                        if (ImGui.MenuItem("Edit Category"))
                        {
                            // TODO: Open edit category dialog
                        }
                    }

                    if (isOwner || permissions != null && permissions.canDeleteCategory)
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
                if (isOwner || permissions != null && permissions.canEditForum)
                {
                    if (ImGui.MenuItem("Edit Channel"))
                    {
                        channelBeingEdited = channel;
                        editChannelName = channel.name ?? string.Empty;
                        editChannelDescription = channel.description ?? string.Empty;
                        editChannelType = channel.channelType;
                        editEveryoneCanView = channel.everyoneCanView;
                        editEveryoneCanPost = channel.everyoneCanPost;
                        showEditChannelPopup = true;
                    }
                }

                if (isOwner || permissions != null && permissions.canDeleteForum)
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

            // Messages area - calculate height to leave room for resizable chat input
            // Account for: resize handle (6px) + chat input (variable) + send button row + spacing + input child padding
            float resizeHandleHeight = 6f;
            float scaledChatInputHeight = chatInputHeight * ImGui.GetIO().FontGlobalScale;
            float inputAreaHeight = resizeHandleHeight + scaledChatInputHeight + 30f; // 30f for spacing/margins/child padding
            ImGui.BeginChild("Messages", new Vector2(-1, -inputAreaHeight), true, ImGuiWindowFlags.HorizontalScrollbar);

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

            // Try to get avatar from group members list using centralized cache
            IDalamudTextureWrap avatarTexture = null;
            if (currentGroup != null && currentGroup.members != null)
            {
                var member = currentGroup.members.FirstOrDefault(m => m.userID == message.senderUserID);
                if (member != null)
                {
                    // Use centralized cache for safe texture access
                    avatarTexture = GroupsData.GetMemberAvatar(member.id, member.avatar);
                }
            }

            // Draw avatar (either from member data or placeholder)
            bool avatarDrawn = false;
            if (avatarTexture != null && GroupsData.IsTextureValid(avatarTexture))
            {
                try
                {
                    ImGui.Image(avatarTexture.Handle, new Vector2(40, 40));
                    ImGui.SameLine();
                    avatarDrawn = true;
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Warning($"[GroupChatWindow] Failed to draw avatar for message {message.messageID}: {ex.Message}");
                }
            }

            if (!avatarDrawn)
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

            // Message content - RenderHtmlElements now handles YouTube URLs automatically
            Misc.RenderHtmlElements(message.messageContent ?? string.Empty, true, true, true, false, limitImageWidth: true);

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

            // Resize handle for chat input
            float resizeHandleHeight = 6f;
            Vector2 resizeHandlePos = ImGui.GetCursorScreenPos();
            float buttonWidth = 80f * ImGui.GetIO().FontGlobalScale;
            float availableWidth = ImGui.GetContentRegionAvail().X - buttonWidth;

            // Draw resize handle bar
            var drawList = ImGui.GetWindowDrawList();
            Vector2 handleMin = resizeHandlePos;
            Vector2 handleMax = new Vector2(resizeHandlePos.X + availableWidth, resizeHandlePos.Y + resizeHandleHeight);

            // Check if mouse is hovering over resize handle
            Vector2 mousePos = ImGui.GetMousePos();
            bool hoveringHandle = mousePos.X >= handleMin.X && mousePos.X <= handleMax.X &&
                                  mousePos.Y >= handleMin.Y && mousePos.Y <= handleMax.Y;

            // Draw handle with different color when hovering
            uint handleColor = hoveringHandle || isResizingChatInput
                ? ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.5f, 1f))
                : ImGui.GetColorU32(new Vector4(0.3f, 0.3f, 0.3f, 1f));
            drawList.AddRectFilled(handleMin, handleMax, handleColor);

            // Draw grip lines in the center
            float gripWidth = 30f;
            float gripStartX = handleMin.X + (availableWidth - gripWidth) / 2f;
            float gripY = handleMin.Y + resizeHandleHeight / 2f;
            uint gripColor = ImGui.GetColorU32(new Vector4(0.6f, 0.6f, 0.6f, 1f));
            drawList.AddLine(new Vector2(gripStartX, gripY - 1), new Vector2(gripStartX + gripWidth, gripY - 1), gripColor, 1f);
            drawList.AddLine(new Vector2(gripStartX, gripY + 1), new Vector2(gripStartX + gripWidth, gripY + 1), gripColor, 1f);

            // Handle resize interaction
            if (hoveringHandle)
            {
                ImGui.SetMouseCursor((ImGuiMouseCursor)3); // ResizeNS = 3 (vertical up-down arrow)
            }

            if (hoveringHandle && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                isResizingChatInput = true;
            }

            if (isResizingChatInput)
            {
                ImGui.SetMouseCursor((ImGuiMouseCursor)3); // ResizeNS = 3 (vertical up-down arrow)
                // Prevent window from being dragged while resizing
                ImGui.GetIO().ConfigWindowsMoveFromTitleBarOnly = true;
                if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    float delta = ImGui.GetIO().MouseDelta.Y;
                    chatInputHeight = Math.Clamp(chatInputHeight - delta, minChatInputHeight, maxChatInputHeight);
                }
                else
                {
                    isResizingChatInput = false;
                    // Restore normal window dragging behavior
                    ImGui.GetIO().ConfigWindowsMoveFromTitleBarOnly = false;
                }
            }

            // Move cursor past the resize handle
            ImGui.Dummy(new Vector2(0, resizeHandleHeight));

            // Message input
            float scaledHeight = chatInputHeight * ImGui.GetIO().FontGlobalScale;
            ImGui.SetNextItemWidth(-buttonWidth);
            ImGui.InputTextMultiline("##MessageInput", ref messageInput, 2000, new Vector2(-buttonWidth, scaledHeight));

            // Enter to send (without Shift), Shift+Enter for new line
            bool enterPressed = ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter);
            bool shouldSend = ImGui.IsItemFocused() && enterPressed && !ImGui.GetIO().KeyShift;
            if (shouldSend)
            {
                // Remove the trailing newline that was added by pressing Enter
                if (messageInput.EndsWith("\n"))
                {
                    messageInput = messageInput.TrimEnd('\n', '\r');
                }
                SendOrEditMessage();
                ImGui.SetKeyboardFocusHere(-1); // Keep focus on input
            }

            ImGui.SameLine();

            // Send button
            if (ImGui.Button(editingMessage != null ? "Save" : "Send", new Vector2(70f * ImGui.GetIO().FontGlobalScale, scaledHeight)))
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
            // Reset NSFW spoiler states when switching channels
            Misc.SetNsfwSession($"chatwindow_{currentGroup.groupID}_{channelID}");

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

            // Process message to wrap URLs in appropriate tags
            // First wrap image URLs in <img> tags, then wrap remaining URLs in <url> tags
            string processedMessage = WrapImageUrls(messageInput);
            processedMessage = WrapUrls(processedMessage);

            if (editingMessage != null)
            {
                // Edit existing message
                DataSender.EditGroupChatMessage(Plugin.character, editingMessage.messageID, processedMessage);
                editingMessage.messageContent = processedMessage;
                editingMessage.isEdited = true;
                editingMessage = null;
            }
            else
            {
                // Send new message
                DataSender.SendGroupChatMessage(Plugin.character, currentGroup.groupID, selectedChannel.id, processedMessage);

                // Optimistically add to local list (server will confirm)
                var newMessage = new GroupChatMessage
                {
                    groupID = currentGroup.groupID,
                    channelID = selectedChannel.id,
                    senderUserID = DataSender.userID,
                    senderName = Plugin.character.characterName, // Use actual character name
                    messageContent = processedMessage,
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

        /// <summary>
        /// Wraps image URLs (http/https containing .jpg, .jpeg, .png, .gif, .webp, .bmp, .svg, .tiff, .ico) in img tags
        /// Handles URLs with query parameters like image.png?size=large
        /// Also handles URLs from common image hosting services
        /// </summary>
        private string WrapImageUrls(string message)
        {
            // First, handle URLs that are already wrapped - don't modify them
            // Then match image URLs by extension or common image hosting patterns
            var imageUrlPattern = new System.Text.RegularExpressions.Regex(
                @"(?<!<img>|<url>)(https?://[^\s<>""]*?(?:\.(?:jpg|jpeg|png|gif|webp|bmp|svg|tiff|ico)|/(?:i\.imgur\.com|media\.discordapp\.net|cdn\.discordapp\.com|pbs\.twimg\.com|i\.redd\.it))[^\s<>""]*)(?!</img>|</url>)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return imageUrlPattern.Replace(message, "<img>$1</img>");
        }

        /// <summary>
        /// Wraps non-image URLs in url tags for clickable links and YouTube embeds
        /// Skips URLs already wrapped in img or url tags
        /// </summary>
        private string WrapUrls(string message)
        {
            // Match URLs not already wrapped in <img> or <url> tags
            // This handles YouTube links, regular links, etc.
            var urlPattern = new System.Text.RegularExpressions.Regex(
                @"(?<!<img>|<url>)(https?://[^\s<>""]+)(?!</img>|</url>)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return urlPattern.Replace(message, "<url>$1</url>");
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

        private void DrawEditChannelPopup()
        {
            if (showEditChannelPopup)
            {
                ImGui.OpenPopup("Edit Channel##ChatWindow");
                showEditChannelPopup = false;
            }

            ImGui.SetNextWindowSize(new Vector2(500, 450), ImGuiCond.Appearing);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool open = true;
            if (ImGui.BeginPopupModal("Edit Channel##ChatWindow", ref open, ImGuiWindowFlags.NoResize))
            {
                if (channelBeingEdited == null)
                {
                    ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "Error: No channel selected");
                    if (ImGui.Button("Close"))
                    {
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                    return;
                }

                ImGui.Text("Channel Name:");
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("##EditChannelName", ref editChannelName, 100);

                ImGui.Spacing();

                ImGui.Text("Description (optional):");
                ImGui.InputTextMultiline("##EditChannelDesc", ref editChannelDescription, 500, new Vector2(-1, 60));

                ImGui.Spacing();

                ImGui.Text("Channel Type:");
                string[] channelTypes = { "Text Channel", "Announcement Channel", "Rules Channel", "Role Selection Channel" };
                ImGui.SetNextItemWidth(-1);
                ImGui.Combo("##EditChannelType", ref editChannelType, channelTypes, channelTypes.Length);

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Permission settings
                ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), "Permissions");
                ImGui.Spacing();

                ImGui.Checkbox("Everyone can view this channel", ref editEveryoneCanView);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("When disabled, only members with specific permissions can see this channel.\nMembers must also agree to group rules (if set) to view channels.");
                }

                ImGui.Checkbox("Everyone can post in this channel", ref editEveryoneCanPost);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("When disabled, only members with specific permissions can post messages");
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Save button
                ImGui.BeginDisabled(string.IsNullOrWhiteSpace(editChannelName));
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.7f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.1f, 0.5f, 0.1f, 1.0f));

                if (ImGui.Button("Save Changes", new Vector2(120, 0)))
                {
                    // Apply changes to the channel
                    channelBeingEdited.name = editChannelName.Trim();
                    channelBeingEdited.description = editChannelDescription.Trim();
                    channelBeingEdited.channelType = editChannelType;
                    channelBeingEdited.everyoneCanView = editEveryoneCanView;
                    channelBeingEdited.everyoneCanPost = editEveryoneCanPost;

                    // Save to server
                    var character = plugin.Configuration.characters.FirstOrDefault(x =>
                        x.characterName == plugin.playername &&
                        x.characterWorld == plugin.playerworld);

                    if (character != null && currentGroup.categories != null)
                    {
                        DataSender.SaveGroupCategories(character, currentGroup.groupID, currentGroup.categories);
                    }

                    Plugin.PluginLog.Info($"Updated channel: {channelBeingEdited.name}");

                    ImGui.CloseCurrentPopup();
                    channelBeingEdited = null;
                }

                ImGui.PopStyleColor(3);
                ImGui.EndDisabled();

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                {
                    ImGui.CloseCurrentPopup();
                    channelBeingEdited = null;
                }

                // Handle close via X button
                if (!open)
                {
                    ImGui.CloseCurrentPopup();
                    channelBeingEdited = null;
                }

                ImGui.EndPopup();
            }
        }
    }
}
