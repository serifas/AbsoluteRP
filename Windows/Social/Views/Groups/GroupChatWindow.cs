using AbsoluteRP;
using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI;
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
using AbsoluteRP.Network;

namespace AbsoluteRP.Windows.Social.Views.Groups
{
    // Standalone group chat window - displays messages for a group channel with send/edit/delete/pin support
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

        // Sorted view of `messages` - rebuilt only when the list changes instead of OrderBy().ToList() every frame.
        private List<GroupChatMessage> sortedMessages = new List<GroupChatMessage>();
        private List<GroupChatMessage> sortedSource = null;
        private int sortedCount = -1;

        // Member lookup by user id, rebuilt when the member list changes.
        private readonly Dictionary<int, GroupMember> membersByUser = new Dictionary<int, GroupMember>();
        private List<GroupMember> membersSource = null;
        private int membersCount = -1;

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

        private static readonly List<string> EditChannelTypes = new List<string> { "Text Channel", "Announcement Channel", "Rules Channel", "Role Selection Channel" };

        private static float S(float v) => RsTheme.S(v);

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

            var avail = ImGui.GetContentRegionAvail();
            float listW = S(210f);

            // Left: channel list card
            ImGui.PushStyleColor(ImGuiCol.ChildBg, RsTheme.PanelSurface);
            ImGui.PushStyleColor(ImGuiCol.Border, RsTheme.Border);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, S(8f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(S(8f), S(10f)));
            bool listOpen = ImGui.BeginChild("##gcw_channels_card", new Vector2(listW, avail.Y), true);
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(2);
            try
            {
                if (listOpen) DrawChannelList();
            }
            finally
            {
                ImGui.EndChild();
            }

            ImGui.SameLine(0f, S(10f));

            // Right: chat card
            ImGui.PushStyleColor(ImGuiCol.ChildBg, RsTheme.PanelSurface);
            ImGui.PushStyleColor(ImGuiCol.Border, RsTheme.Border);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, S(8f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(S(12f), S(10f)));
            bool chatOpen = ImGui.BeginChild("##gcw_chat_card", new Vector2(0f, avail.Y), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(2);
            try
            {
                if (chatOpen) DrawChatArea();
            }
            finally
            {
                ImGui.EndChild();
            }

            // Draw edit channel popup (outside of the layout)
            DrawEditChannelPopup();
        }

        private void DrawChannelList()
        {
            // Group name header
            GroupUi.Logo(currentGroup.logo, currentGroup.name, S(28f), S(6f));
            ImGui.SameLine(0f, S(8f));
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (S(28f) - ImGui.GetTextLineHeight()) * 0.5f);
            ImGui.TextUnformatted(currentGroup.name ?? "Group");
            ImGui.Dummy(new Vector2(0f, S(4f)));
            GroupUi.Divider(6f);

            if (currentGroup.categories == null || currentGroup.categories.Count == 0)
            {
                GroupUi.Muted("No channels available");
                return;
            }

            bool open = ImGui.BeginChild("ChannelList", new Vector2(-1, -1), false);
            try
            {
                if (!open) return;

                // Draw categories with channels
                for (int catIdx = 0; catIdx < currentGroup.categories.Count; catIdx++)
                {
                    var category = currentGroup.categories[catIdx];

                    // Category header (collapsible)
                    ImGuiTreeNodeFlags nodeFlags = ImGuiTreeNodeFlags.SpanFullWidth;
                    if (!category.collapsed)
                        nodeFlags |= ImGuiTreeNodeFlags.DefaultOpen;

                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                    ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0f, 0f, 0f, 0f));
                    ImGui.PushStyleColor(ImGuiCol.HeaderHovered, GroupUi.Fade(RsTheme.AccentPrimary, 0.12f));
                    bool categoryOpen;
                    try
                    {
                        categoryOpen = ImGui.CollapsingHeader($"{(category.name ?? string.Empty).ToUpperInvariant()}###cat_{catIdx}", nodeFlags);
                    }
                    finally
                    {
                        ImGui.PopStyleColor(3);
                    }

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
                        ImGui.Indent(S(8f));

                        foreach (var channel in category.channels)
                        {
                            DrawChannelItem(channel);
                        }

                        ImGui.Unindent(S(8f));
                    }

                    ImGui.Dummy(new Vector2(0f, S(4f)));
                }
            }
            finally
            {
                ImGui.EndChild();
            }
        }

        private void DrawChannelItem(GroupChannel channel)
        {
            bool isSelected = selectedChannel != null && selectedChannel.id == channel.id;

            float rowH = ImGui.GetTextLineHeight() + S(10f);
            var rowMin = ImGui.GetCursorScreenPos();
            if (ImGui.Selectable($"##channel_{channel.id}", isSelected, ImGuiSelectableFlags.None, new Vector2(0f, rowH)))
            {
                SelectChannel(channel);
            }
            var rowMax = ImGui.GetItemRectMax();
            bool hovered = ImGui.IsItemHovered();

            // Icon, name and unread pill painted over the selectable.
            var dl = ImGui.GetWindowDrawList();
            float midY = rowMin.Y + rowH * 0.5f;
            var icon = GroupUi.ChannelIcon(channel.channelType);
            var iconSz = GroupUi.IconSize(icon);
            GroupUi.DrawIconAt(dl, icon, new Vector2(rowMin.X + S(6f), midY - iconSz.Y * 0.5f), isSelected ? RsTheme.U.AccentPrimary : RsTheme.U.TextMuted);
            float textX = rowMin.X + S(6f) + Math.Max(iconSz.X, S(14f)) + S(8f);
            float rightX = rowMax.X - S(6f);

            if (channel.unreadCount > 0 && !isSelected)
            {
                string cnt = channel.unreadCount > 99 ? "99+" : channel.unreadCount.ToString();
                var cs = ImGui.CalcTextSize(cnt);
                float bh = cs.Y + S(2f);
                float bw = Math.Max(bh, cs.X + S(10f));
                var bmin = new Vector2(rightX - bw, midY - bh * 0.5f);
                dl.AddRectFilled(bmin, bmin + new Vector2(bw, bh), RsTheme.U.AccentDanger, bh * 0.5f);
                dl.AddText(new Vector2(bmin.X + (bw - cs.X) * 0.5f, bmin.Y + (bh - cs.Y) * 0.5f), 0xFFFFFFFFu, cnt);
                rightX = bmin.X - S(6f);
            }

            bool emphasised = isSelected || hovered || channel.unreadCount > 0;
            dl.PushClipRect(new Vector2(textX, rowMin.Y), new Vector2(Math.Max(textX, rightX), rowMin.Y + rowH), true);
            dl.AddText(new Vector2(textX, midY - ImGui.GetTextLineHeight() * 0.5f), emphasised ? RsTheme.U.TextPrimary : RsTheme.U.TextSecondary, channel.name ?? string.Empty);
            dl.PopClipRect();

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
                var avail = ImGui.GetContentRegionAvail();
                const string hint = "Select a channel to start chatting";
                var ts = ImGui.CalcTextSize(hint);
                ImGui.SetCursorPos(new Vector2(
                    ImGui.GetCursorPosX() + Math.Max(0f, (avail.X - ts.X) * 0.5f),
                    ImGui.GetCursorPosY() + Math.Max(0f, (avail.Y - ts.Y) * 0.5f)));
                GroupUi.Muted(hint);
                return;
            }

            // Channel header
            {
                var start = ImGui.GetCursorScreenPos();
                float width = ImGui.GetContentRegionAvail().X;
                float rowH = S(30f);
                var dl = ImGui.GetWindowDrawList();
                float midY = start.Y + rowH * 0.5f;
                float lineH = ImGui.GetTextLineHeight();

                var icon = GroupUi.ChannelIcon(selectedChannel.channelType);
                var iconSz = GroupUi.IconSize(icon);
                GroupUi.DrawIconAt(dl, icon, new Vector2(start.X, midY - iconSz.Y * 0.5f), RsTheme.U.TextMuted);
                float x = start.X + Math.Max(iconSz.X, S(14f)) + S(8f);
                string name = selectedChannel.name ?? string.Empty;
                dl.AddText(new Vector2(x, midY - lineH * 0.5f), RsTheme.U.TextPrimary, name);
                x += ImGui.CalcTextSize(name).X + S(10f);
                if (!string.IsNullOrEmpty(selectedChannel.description))
                {
                    dl.AddLine(new Vector2(x, midY - lineH * 0.4f), new Vector2(x, midY + lineH * 0.4f), RsTheme.U.Border, RsTheme.BorderThickness);
                    x += S(10f);
                    dl.PushClipRect(new Vector2(x, start.Y), new Vector2(start.X + width, start.Y + rowH), true);
                    dl.AddText(new Vector2(x, midY - lineH * 0.5f), RsTheme.U.TextMuted, selectedChannel.description.Replace('\n', ' '));
                    dl.PopClipRect();
                }
                ImGui.Dummy(new Vector2(width, rowH));
                GroupUi.Divider(6f);
            }

            // Messages area - calculate height to leave room for resizable chat input Fixed-height composer anchored to the bottom; the list takes the rest.
            float areaAvailY = ImGui.GetContentRegionAvail().Y;
            float inputAreaHeight = S(8f) + S(chatInputHeight) + S(16f) + (editingMessage != null ? ImGui.GetFrameHeightWithSpacing() : 0f);
            float maxArea = Math.Max(S(70f), areaAvailY * 0.6f);
            if (inputAreaHeight > maxArea) inputAreaHeight = maxArea;
            float listReserve = inputAreaHeight + ImGui.GetStyle().ItemSpacing.Y;
            bool msgOpen = ImGui.BeginChild("Messages", new Vector2(-1, -listReserve), false, ImGuiWindowFlags.HorizontalScrollbar);
            try
            {
                if (msgOpen)
                {
                    DrawMessages();

                    // Stick to the bottom when new messages arrive, or while the reader is already at the bottom.
                    if (autoScroll || ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
                    {
                        ImGui.SetScrollHereY(1.0f);
                        autoScroll = false;
                    }
                }
            }
            finally
            {
                ImGui.EndChild();
            }

            // Input area (fixed-size child: contents can't resize/move the window)
            if (ImGui.BeginChild("##gcw_composer", new Vector2(0f, inputAreaHeight), false,
                    ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                DrawMessageInput();
            }
            ImGui.EndChild();
        }

        private void DrawMessages()
        {
            if (messages == null || messages.Count == 0)
            {
                GroupUi.Muted("No messages yet. Start the conversation!");
                return;
            }

            PollMessages();
            // Display messages (oldest first, newest at bottom) - sorted once per change.
            if (!ReferenceEquals(messages, sortedSource) || messages.Count != sortedCount)
            {
                sortedSource = messages;
                sortedCount = messages.Count;
                sortedMessages = messages.OrderBy(m => m.timestamp).ToList();
            }

            GroupChatMessage prev = null;
            foreach (var message in sortedMessages)
            {
                if (message.deleted)
                    continue;

                bool continued = prev != null && prev.senderUserID == message.senderUserID
                                 && message.timestamp - prev.timestamp < 5 * 60 * 1000;
                prev = message;
                DrawMessageItem(message, continued);
            }
        }

        private GroupMember FindMember(int userID)
        {
            var members = currentGroup?.members;
            if (!ReferenceEquals(members, membersSource) || (members?.Count ?? -1) != membersCount)
            {
                membersSource = members;
                membersCount = members?.Count ?? -1;
                membersByUser.Clear();
                if (members != null)
                    foreach (var m in members)
                        if (m != null) membersByUser.TryAdd(m.userID, m);
            }
            return membersByUser.TryGetValue(userID, out var found) ? found : null;
        }

        private void DrawMessageItem(GroupChatMessage message, bool continued)
        {
            ImGui.PushID($"msg_{message.messageID}");
            try
            {
                // Check if this is the current user's message
                bool isOwnMessage = message.senderUserID == Accounts_DS.userID;
                float avatarSize = S(36f);
                var rowStart = ImGui.GetCursorScreenPos();

                if (continued)
                {
                    ImGui.Dummy(new Vector2(avatarSize, 1f));
                    ImGui.SameLine(0f, S(12f));
                }
                else
                {
                    // Try to get avatar from group members list using centralized cache
                    IDalamudTextureWrap avatarTexture = null;
                    var member = FindMember(message.senderUserID);
                    if (member != null)
                    {
                        // Use centralized cache for safe texture access
                        avatarTexture = GroupsData.GetMemberAvatar(member.id, member.avatar);
                    }
                    GroupUi.Avatar(avatarTexture, message.senderName, avatarSize);
                    ImGui.SameLine(0f, S(12f));
                }

                ImGui.BeginGroup();
                try
                {
                    if (!continued)
                    {
                        // Sender name and timestamp
                        var dateTime = DateTimeOffset.FromUnixTimeMilliseconds(message.timestamp).LocalDateTime;
                        string timeStr = dateTime.ToString("HH:mm");

                        ImGui.PushStyleColor(ImGuiCol.Text, isOwnMessage ? RsTheme.AccentPrimary : RsTheme.TextPrimary);
                        ImGui.TextUnformatted(message.senderName ?? "Unknown");
                        ImGui.PopStyleColor();

                        ImGui.SameLine(0f, S(8f));
                        GroupUi.Muted(timeStr);

                        // Edited indicator
                        if (message.isEdited)
                        {
                            ImGui.SameLine(0f, S(6f));
                            GroupUi.Muted("(edited)");
                        }
                    }
                    else if (message.isEdited)
                    {
                        GroupUi.Muted("(edited)");
                    }

                    // Message content - RenderHtmlElements now handles YouTube URLs automatically
                    if (AbsoluteRP.Helpers.ChatAttachments.HasMedia(message.messageContent ?? string.Empty))
                        AbsoluteRP.Helpers.ChatAttachments.RenderWithMedia(message.messageContent ?? string.Empty, s => Misc.RenderHtmlElements(s, true, true, true, false, limitImageWidth: true), ImGui.GetContentRegionAvail().X);
                    else
                        Misc.RenderHtmlElements(message.messageContent ?? string.Empty, true, true, true, false, limitImageWidth: true);
                }
                finally
                {
                    ImGui.EndGroup();
                }

                // Make the entire message clickable for context menu
                var messageMin = ImGui.GetItemRectMin();
                var messageMax = ImGui.GetItemRectMax();
                var messageSize = new Vector2(Math.Max(1f, messageMax.X - messageMin.X), Math.Max(1f, messageMax.Y - messageMin.Y));
                float rowBottom = Math.Max(messageMax.Y, rowStart.Y + (continued ? 0f : avatarSize));

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

                ImGui.SetCursorScreenPos(new Vector2(rowStart.X, rowBottom));
                ImGui.Dummy(new Vector2(0f, continued ? 0f : S(4f)));
            }
            finally
            {
                ImGui.PopID();
            }
        }

        private void DrawMessageInput()
        {
            float gap = S(8f);
            float btnH = S(32f);
            float sendBtnW = S(64f);
            float sendW = btnH + S(4f) + sendBtnW;
            float regionW = ImGui.GetContentRegionAvail().X - 1f;

            // Resize handle for chat input
            float resizeHandleHeight = S(8f);
            Vector2 resizeHandlePos = ImGui.GetCursorScreenPos();
            float availableWidth = Math.Max(S(60f), regionW - sendW - gap);

            var drawList = ImGui.GetWindowDrawList();
            Vector2 handleMin = resizeHandlePos;
            Vector2 handleMax = new Vector2(resizeHandlePos.X + availableWidth, resizeHandlePos.Y + resizeHandleHeight);

            // Check if mouse is hovering over resize handle
            Vector2 mousePos = ImGui.GetMousePos();
            bool hoveringHandle = mousePos.X >= handleMin.X && mousePos.X <= handleMax.X &&
                                  mousePos.Y >= handleMin.Y && mousePos.Y <= handleMax.Y;

            // Grip bar
            float gripWidth = S(36f);
            float gripStartX = handleMin.X + (availableWidth - gripWidth) / 2f;
            float gripY = handleMin.Y + resizeHandleHeight / 2f;
            uint gripColor = hoveringHandle || isResizingChatInput ? RsTheme.U.AccentPrimary : RsTheme.U.BorderStrong;
            drawList.AddRectFilled(new Vector2(gripStartX, gripY - S(1.5f)), new Vector2(gripStartX + gripWidth, gripY + S(1.5f)), gripColor, S(1.5f));

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

            // Resize handle as an item so dragging it never drags the window
            ImGui.InvisibleButton("##gcw_input_resize", new Vector2(Math.Max(1f, availableWidth), resizeHandleHeight));

            // Editing banner
            if (editingMessage != null)
            {
                GroupUi.Chip("Editing message", RsTheme.AccentWarning);
                ImGui.SameLine(0f, S(6f));
                if (RsElements.Button("Cancel##gcw_edit_cancel", RsElements.ButtonVariant.Ghost))
                {
                    CancelEditing();
                }
            }

            // Message input
            float scaledHeight = Math.Max(S(24f), ImGui.GetContentRegionAvail().Y - S(16f));
            btnH = Math.Min(btnH, scaledHeight);
            var inputPos = ImGui.GetCursorScreenPos();
            ImGui.PushStyleColor(ImGuiCol.FrameBg, RsTheme.BgTertiary);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, RsTheme.BgTertiary);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, RsTheme.BgTertiary);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, S(6f));
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(S(10f), S(8f)));
            bool inputActive;
            try
            {
                ImGui.InputTextMultiline("##MessageInput", ref messageInput, 2000, new Vector2(availableWidth, scaledHeight));
                inputActive = ImGui.IsItemActive();
            }
            finally
            {
                ImGui.PopStyleVar(3);
                ImGui.PopStyleColor(3);
            }
            drawList.AddRect(inputPos, inputPos + new Vector2(availableWidth, scaledHeight),
                inputActive ? RsTheme.U.AccentPrimary : RsTheme.U.Border, S(6f), ImDrawFlags.None,
                RsTheme.BorderThickness + (inputActive ? 1f : 0f));
            if (string.IsNullOrEmpty(messageInput) && !inputActive && selectedChannel != null)
            {
                drawList.AddText(inputPos + new Vector2(S(10f), S(8f)), RsTheme.U.TextMuted, $"Message #{selectedChannel.name}");
            }

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

            ImGui.SameLine(0f, gap);

            // Attach, then Send.
            AbsoluteRP.Helpers.ChatAttachments.DrawAttachButton("gcw_attach", btnH / Math.Max(0.01f, RsTheme.Scale),
                url => messageInput = (string.IsNullOrWhiteSpace(messageInput) ? "" : messageInput.TrimEnd() + " ") + url + " ");
            ImGui.SameLine(0f, S(4f));
            if (RsElements.Button(editingMessage != null ? "Save##gcw_send" : "Send##gcw_send", RsElements.ButtonVariant.Primary, new Vector2(sendBtnW, btnH)))
            {
                SendOrEditMessage();
            }
            AbsoluteRP.Helpers.ChatAttachments.DrawStatus();
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
                autoScroll = true;
                return;
            }

            // Fetch from server
            messages = new List<GroupChatMessage>();
            GroupChat_DS.FetchGroupChatMessages(Plugin.character, currentGroup.groupID, channelID, 50, 0);
        }

        private void SendOrEditMessage()
        {
            if (string.IsNullOrWhiteSpace(messageInput))
                return;

            // Process message to wrap URLs in appropriate tags First wrap image URLs in <img> tags, then wrap remaining URLs in <url> tags
            string processedMessage = WrapImageUrls(messageInput);
            processedMessage = WrapUrls(processedMessage);

            if (editingMessage != null)
            {
                // Edit existing message
                GroupChat_DS.EditGroupChatMessage(Plugin.character, editingMessage.messageID, processedMessage);
                editingMessage.messageContent = processedMessage;
                editingMessage.isEdited = true;
                editingMessage = null;
            }
            else
            {
                // Send new message
                GroupChat_DS.SendGroupChatMessage(Plugin.character, currentGroup.groupID, selectedChannel.id, processedMessage);

                // Optimistically add to local list (server will confirm)
                var newMessage = new GroupChatMessage
                {
                    groupID = currentGroup.groupID,
                    channelID = selectedChannel.id,
                    senderUserID = Accounts_DS.userID,
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

        // Built once - these used to be constructed on every send.
        private static readonly System.Text.RegularExpressions.Regex ImageUrlPattern = new System.Text.RegularExpressions.Regex(
            @"(?<!<img>|<url>)(https?://[^\s<>""]*?(?:\.(?:jpg|jpeg|png|gif|webp|bmp|svg|tiff|ico)|/(?:i\.imgur\.com|media\.discordapp\.net|cdn\.discordapp\.com|pbs\.twimg\.com|i\.redd\.it))[^\s<>""]*)(?!</img>|</url>)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex UrlPattern = new System.Text.RegularExpressions.Regex(
            @"(?<!<img>|<url>)(https?://[^\s<>""]+)(?!</img>|</url>)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        /// Wraps image URLs (http/https containing .jpg, .jpeg, .png, .gif, .webp, .bmp, .svg, .tiff, .ico) in img tags Handles URLs with query parameters like image.png?size=large Also handles URLs from common image hosting services
        private string WrapImageUrls(string message)
        {
            return ImageUrlPattern.Replace(message, "<img>$1</img>");
        }

        /// Wraps non-image URLs in url tags for clickable links and YouTube embeds Skips URLs already wrapped in img or url tags
        private string WrapUrls(string message)
        {
            return UrlPattern.Replace(message, "<url>$1</url>");
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
            GroupChat_DS.DeleteGroupChatMessage(Plugin.character, message.messageID);
            message.deleted = true;
        }

        private void MarkChannelAsRead(GroupChannel channel)
        {
            if (messages.Count == 0) return;

            var lastMessage = messages.OrderByDescending(m => m.timestamp).FirstOrDefault();
            if (lastMessage != null)
            {
                GroupChat_DS.UpdateChatReadStatus(Plugin.character, channel.id, lastMessage.messageID, lastMessage.timestamp);
                channel.unreadCount = 0;
            }
        }

        // Called by DataReceiver when messages are received from server Missed-broadcast fallback: re-request the open channel every few seconds.
        private long _lastPollMs;
        private void PollMessages()
        {
            if (selectedChannel == null || currentGroup == null || Plugin.character == null) return;
            var now = Environment.TickCount64;
            if (now - _lastPollMs < 4000) return;
            _lastPollMs = now;
            try { GroupChat_DS.FetchGroupChatMessages(Plugin.character, currentGroup.groupID, selectedChannel.id, 50, 0); } catch { }
        }

        public void OnMessagesReceived(int channelID, List<GroupChatMessage> receivedMessages)
        {
            if (channelID == selectedChannel?.id)
            {
                if (messages == null || messages.Count == 0 || receivedMessages.Count == 0)
                {
                    messages = receivedMessages;
                    autoScroll = true;
                }
                else
                {
                    // Merge into the list on screen instead of replacing it.
                    var byId = messages.ToDictionary(m => m.messageID, m => m);
                    bool changed = false;
                    foreach (var m in receivedMessages)
                    {
                        if (byId.TryGetValue(m.messageID, out var have))
                        {
                            if (have.messageContent != m.messageContent || have.isEdited != m.isEdited || have.isPinned != m.isPinned)
                            { have.messageContent = m.messageContent; have.isEdited = m.isEdited; have.isPinned = m.isPinned; changed = true; }
                        }
                        else { messages.Add(m); changed = true; }
                    }
                    var incoming = new HashSet<int>(receivedMessages.Select(m => m.messageID));
                    int oldest = receivedMessages.Min(m => m.messageID);
                    if (messages.RemoveAll(m => m.messageID >= oldest && !incoming.Contains(m.messageID)) > 0) changed = true;
                    if (changed) autoScroll = true;
                }
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

            ImGui.SetNextWindowSize(new Vector2(S(500f), S(450f)), ImGuiCond.Appearing);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool open = true;
            if (ImGui.BeginPopupModal("Edit Channel##ChatWindow", ref open, ImGuiWindowFlags.NoResize))
            {
                if (channelBeingEdited == null)
                {
                    ImGui.TextColored(RsTheme.AccentDanger, "Error: No channel selected");
                    if (GroupUi.Ghost("Close"))
                    {
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                    return;
                }

                GroupUi.SectionLabel("Channel name");
                RsElements.InputText("gcw_edit_name", ref editChannelName, 100, "Channel name");

                ImGui.Dummy(new Vector2(0f, S(8f)));
                GroupUi.SectionLabel("Description (optional)");
                RsElements.InputTextArea("gcw_edit_desc", ref editChannelDescription, 500, "Describe the channel", new Vector2(ImGui.GetContentRegionAvail().X, S(60f)));

                ImGui.Dummy(new Vector2(0f, S(8f)));
                GroupUi.SectionLabel("Channel type");
                RsElements.Dropdown("gcw_edit_type", ref editChannelType, EditChannelTypes);

                ImGui.Dummy(new Vector2(0f, S(8f)));
                GroupUi.Divider();

                // Permission settings
                GroupUi.SectionLabel("Permissions");

                RsElements.Checkbox("Everyone can view this channel##gcw", ref editEveryoneCanView);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("When disabled, only members with specific permissions can see this channel.\nMembers must also agree to group rules (if set) to view channels.");
                }

                RsElements.Checkbox("Everyone can post in this channel##gcw", ref editEveryoneCanPost);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("When disabled, only members with specific permissions can post messages");
                }

                ImGui.Dummy(new Vector2(0f, S(8f)));
                GroupUi.Divider();

                // Save button
                ImGui.BeginDisabled(string.IsNullOrWhiteSpace(editChannelName));
                if (GroupUi.Success("Save Changes", new Vector2(120, 0)))
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
                        GroupChannels_DS.SaveGroupCategories(character, currentGroup.groupID, currentGroup.categories);
                    }

                    Plugin.PluginLog.Info($"Updated channel: {channelBeingEdited.name}");

                    ImGui.CloseCurrentPopup();
                    channelBeingEdited = null;
                }
                ImGui.EndDisabled();

                ImGui.SameLine();

                if (GroupUi.Ghost("Cancel", new Vector2(120, 0)))
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
