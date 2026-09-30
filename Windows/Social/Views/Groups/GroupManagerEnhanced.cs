using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Social.Views.Groups.GroupManager;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AbsoluteRP.Network;
using AbsoluteRP.RsUI;
using Dalamud.Interface;

namespace AbsoluteRP.Windows.Social.Views.Groups
{
    /// Enhanced Group Manager with Discord-like settings and guild roster management
    internal class GroupManagerEnhanced
    {
        private static AbsoluteRP.RsUI.RsFileDialogManager _fileDialogManager;
        private static List<ProfileData> profiles = new List<ProfileData>();

        // Tab state
        private static int selectedTab = 0;

        // Roster fields state
        private static List<GroupRosterField> rosterFields = new List<GroupRosterField>();
        private static GroupRosterField editingField = null;

        // Category/Channel state
        private static GroupCategory editingCategory = null;
        private static GroupChannel editingChannel = null;

        public static void ManageGroup(Group group)
        {
            if (_fileDialogManager == null)
                _fileDialogManager = new AbsoluteRP.RsUI.RsFileDialogManager();

            _fileDialogManager.Draw();

            // Header with group logo and name
            DrawGroupHeader(group);

            GroupUi.Divider();
            ImGui.Spacing();

            // Tabbed interface
            if (ImGui.BeginTabBar("GroupManagerTabs", ImGuiTabBarFlags.None))
            {
                if (ImGui.BeginTabItem("General"))
                {
                    DrawGeneralTab(group);
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Chat & Channels"))
                {
                    DrawChannelsTab(group);
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Roster"))
                {
                    DrawRosterTab(group);
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Ranks"))
                {
                    DrawRanksTab(group);
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Permissions"))
                {
                    DrawPermissionsTab(group);
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Settings"))
                {
                    DrawSettingsTab(group);
                    ImGui.EndTabItem();
                }

                // Join Requests tab - only show if user has permission or is owner
                if (GroupPermissions.CanAcceptJoinRequests(group) || GroupPermissions.IsOwner(group))
                {
                    // Show badge with pending request count
                    int pendingCount = group.joinRequests?.Count(r => r.status == 0) ?? 0;
                    string tabLabel = pendingCount > 0 ? $"Join Requests ({pendingCount})" : "Join Requests";

                    if (ImGui.BeginTabItem(tabLabel))
                    {
                        DrawJoinRequestsTab(group);
                        ImGui.EndTabItem();
                    }
                }

                ImGui.EndTabBar();
            }
        }

        private static void DrawGroupHeader(Group group)
        {
            ImGui.BeginGroup();

            // Logo
            float logoSize = RsTheme.S(60f);
            GroupUi.Logo(group.logo, group.name, logoSize, RsTheme.S(12f));

            ImGui.SameLine(0f, RsTheme.S(12f));

            // Group name and description
            ImGui.BeginGroup();
            ImGui.TextUnformatted(group.name ?? "Group");

            GroupUi.Muted(group.description ?? "No description");

            // Quick stats
            GroupUi.Secondary($"Members: {group.members?.Count ?? 0}  ·  Ranks: {group.ranks?.Count ?? 0}  ·  Channels: {CountTotalChannels(group)}");
            ImGui.EndGroup();

            ImGui.EndGroup();
        }

        private static int CountTotalChannels(Group group)
        {
            int count = 0;
            if (group.categories != null)
            {
                foreach (var cat in group.categories)
                {
                    if (cat.channels != null)
                        count += cat.channels.Count;
                }
            }
            return count;
        }

        #region General Tab

        private static void DrawGeneralTab(Group group)
        {
            ImGui.BeginChild("GeneralSettings", new Vector2(-1, -1), false);

            GroupUi.SectionLabel("Basic Information");

            // Group Name
            ImGui.Text("Group Name:");
            string groupName = group.name ?? string.Empty;
            if (RsElements.InputText("gme_group_name", ref groupName, 100, "Group name"))
            {
                group.name = groupName;
            }

            // Group Description
            ImGui.Text("Description:");
            string groupDesc = group.description ?? string.Empty;
            if (RsElements.InputTextArea("gme_group_desc", ref groupDesc, 1000, "Describe the group", new Vector2(ImGui.GetContentRegionAvail().X, RsTheme.S(80f))))
            {
                group.description = groupDesc;
            }

            ImGui.Dummy(new Vector2(0f, RsTheme.S(8f)));
            GroupUi.SectionLabel("Appearance");

            // Logo
            ImGui.Text("Group Logo:");
            if (RsElements.Button("Change Logo##gme_logo", RsElements.ButtonVariant.Secondary))
            {
                Misc.EditGroupImage(Plugin.plugin, _fileDialogManager, group, true, false, 0);
            }

            // Background
            ImGui.Text("Group Background:");
            if (RsElements.Button("Change Background##gme_bg", RsElements.ButtonVariant.Secondary))
            {
                Misc.EditGroupImage(Plugin.plugin, _fileDialogManager, group, false, true, 0);
            }

            ImGui.Dummy(new Vector2(0f, RsTheme.S(8f)));
            GroupUi.SectionLabel("Visibility & Access");

            // Visibility
            bool visible = group.visible;
            if (RsElements.Checkbox("Visible in Group Search", ref visible))
            {
                group.visible = visible;
            }
            UIHelpers.SelectableHelpMarker("If enabled, this group will appear in public group searches");

            // Open Invite
            bool openInvite = group.openInvite;
            if (RsElements.Checkbox("Open Invitations", ref openInvite))
            {
                group.openInvite = openInvite;
            }
            UIHelpers.SelectableHelpMarker("If enabled, anyone can join without approval");

            ImGui.Spacing();
            GroupUi.Divider();

            // Save button
            if (RsElements.Button("Save General Settings##gme_save", RsElements.ButtonVariant.Primary))
            {
                Groups_DS.SetGroupValues(Plugin.character, group, true, 0, 0);
                Plugin.PluginLog.Info("General settings saved");
            }

            ImGui.EndChild();
        }

        #endregion

        #region Channels Tab

        private static void DrawChannelsTab(Group group)
        {
            ImGui.BeginChild("ChannelsSettings", new Vector2(-1, -1), false);

            ImGui.Text("Organize your group's chat channels into categories");
            ImGui.Spacing();

            // Two-column layout: Categories list | Category editor
            using (var table = ImRaii.Table("ChannelsLayout", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV))
            {
                if (!table)
                {
                    ImGui.EndChild();
                    return;
                }

                ImGui.TableSetupColumn("Categories", ImGuiTableColumnFlags.WidthFixed, 250f * ImGui.GetIO().FontGlobalScale);
                ImGui.TableSetupColumn("Editor", ImGuiTableColumnFlags.WidthStretch);

                ImGui.TableNextRow();

                // Left: Categories list
                ImGui.TableSetColumnIndex(0);
                DrawCategoriesList(group);

                // Right: Category/Channel editor
                ImGui.TableSetColumnIndex(1);
                DrawCategoryEditor(group);
            }

            ImGui.EndChild();
        }

        private static void DrawCategoriesList(Group group)
        {
            ImGui.BeginChild("CategoriesList", new Vector2(-1, -40f * ImGui.GetIO().FontGlobalScale), true);

            if (group.categories == null)
                group.categories = new List<GroupCategory>();

            for (int i = 0; i < group.categories.Count; i++)
            {
                var category = group.categories[i];

                if (ImGui.Selectable($"{category.name}##cat_{i}", editingCategory == category))
                {
                    editingCategory = category;
                    editingChannel = null;
                }

                // Channels under category
                if (category.channels != null)
                {
                    ImGui.Indent(15f * ImGui.GetIO().FontGlobalScale);
                    for (int j = 0; j < category.channels.Count; j++)
                    {
                        var channel = category.channels[j];
                        var rowMin = ImGui.GetCursorScreenPos();
                        if (ImGui.Selectable($"##ch_{j}", editingChannel == channel, ImGuiSelectableFlags.None, new Vector2(0f, ImGui.GetTextLineHeight() + RsTheme.S(6f))))
                        {
                            editingChannel = channel;
                            editingCategory = category;
                        }
                        var dl = ImGui.GetWindowDrawList();
                        var icon = GroupUi.ChannelIcon(channel.channelType);
                        var iconSz = GroupUi.IconSize(icon);
                        float rowH = ImGui.GetTextLineHeight() + RsTheme.S(6f);
                        GroupUi.DrawIconAt(dl, icon, new Vector2(rowMin.X + RsTheme.S(4f), rowMin.Y + (rowH - iconSz.Y) * 0.5f), editingChannel == channel ? RsTheme.U.AccentPrimary : RsTheme.U.TextMuted);
                        dl.AddText(new Vector2(rowMin.X + RsTheme.S(4f) + Math.Max(iconSz.X, RsTheme.S(14f)) + RsTheme.S(6f), rowMin.Y + RsTheme.S(3f)), RsTheme.U.TextPrimary, channel.name ?? string.Empty);
                    }
                    ImGui.Unindent(15f * ImGui.GetIO().FontGlobalScale);
                }

                ImGui.Spacing();
            }

            ImGui.EndChild();

            // Add category button
            if (GroupUi.Button("+ New Category", new Vector2(-1, 0)))
            {
                var newCategory = new GroupCategory
                {
                    id = 0,
                    sortOrder = group.categories.Count,
                    name = "New Category",
                    description = string.Empty,
                    collapsed = false,
                    channels = new List<GroupChannel>()
                };
                group.categories.Add(newCategory);
                editingCategory = newCategory;
                editingChannel = null;
            }
        }

        private static void DrawCategoryEditor(Group group)
        {
            ImGui.BeginChild("CategoryEditor", new Vector2(-1, -1), true);

            if (editingCategory == null && editingChannel == null)
            {
                GroupUi.Muted("Select a category or channel to edit");
                ImGui.EndChild();
                return;
            }

            if (editingChannel != null)
            {
                DrawChannelEditor(group, editingChannel);
            }
            else if (editingCategory != null)
            {
                DrawCategoryEditorFields(group, editingCategory);
            }

            ImGui.EndChild();
        }

        private static void DrawCategoryEditorFields(Group group, GroupCategory category)
        {
            GroupUi.SectionLabel("Category Settings");

            // Category Name
            ImGui.Text("Category Name:");
            string catName = category.name ?? string.Empty;
            if (RsElements.InputText("gme_cat_name", ref catName, 100, "Category name"))
            {
                category.name = catName;
            }

            // Description
            ImGui.Text("Description:");
            string catDesc = category.description ?? string.Empty;
            if (RsElements.InputTextArea("gme_cat_desc", ref catDesc, 500, "Category description", new Vector2(ImGui.GetContentRegionAvail().X, RsTheme.S(60f))))
            {
                category.description = catDesc;
            }

            ImGui.Dummy(new Vector2(0f, RsTheme.S(8f)));
            GroupUi.SectionLabel("Channels in this Category");

            // List channels
            if (category.channels == null)
                category.channels = new List<GroupChannel>();

            for (int i = 0; i < category.channels.Count; i++)
            {
                var ch = category.channels[i];
                ImGui.BulletText($"{ch.name}");
            }

            ImGui.Spacing();

            // Add channel button
            if (GroupUi.Button("+ Add Channel"))
            {
                var newChannel = new GroupChannel
                {
                    id = 0,
                    index = category.channels.Count,
                    name = "new-channel",
                    description = string.Empty,
                    categoryID = category.id,
                    channelType = 0,
                    AllowedMembers = new List<GroupMember>(),
                    AllowedRanks = new List<GroupRank>()
                };
                category.channels.Add(newChannel);
                editingChannel = newChannel;
            }

            ImGui.SameLine();

            // Delete category button
            if (GroupUi.Danger("Delete Category"))
            {
                group.categories.Remove(category);
                editingCategory = null;
            }

            ImGui.Spacing();
            GroupUi.Divider();

            if (GroupUi.Button("Save Categories", new Vector2(-1, 0)))
            {
                GroupChannels_DS.SaveGroupCategories(Plugin.character, group.groupID, group.categories);
            }
        }

        private static readonly List<string> ChannelTypeOptions = new List<string> { "Text Channel", "Announcement Channel" };

        private static void DrawChannelEditor(Group group, GroupChannel channel)
        {
            GroupUi.SectionLabel("Channel Settings");

            // Channel Name
            ImGui.Text("Channel Name:");
            string chName = channel.name ?? string.Empty;
            if (RsElements.InputText("gme_ch_name", ref chName, 100, "channel-name"))
            {
                channel.name = chName.ToLower().Replace(" ", "-");
            }

            // Description
            ImGui.Text("Description:");
            string chDesc = channel.description ?? string.Empty;
            if (RsElements.InputTextArea("gme_ch_desc", ref chDesc, 500, "Channel description", new Vector2(ImGui.GetContentRegionAvail().X, RsTheme.S(60f))))
            {
                channel.description = chDesc;
            }

            // Channel Type
            ImGui.Text("Channel Type:");
            int currentType = channel.channelType;
            if (RsElements.Dropdown("gme_ch_type", ref currentType, ChannelTypeOptions))
            {
                channel.channelType = currentType;
            }

            ImGui.Spacing();

            // Delete channel button
            if (GroupUi.Danger("Delete Channel"))
            {
                if (editingCategory != null && editingCategory.channels != null)
                {
                    editingCategory.channels.Remove(channel);
                    editingChannel = null;
                }
            }
        }

        #endregion

        #region Roster Tab

        private static void DrawRosterTab(Group group)
        {
            ImGui.BeginChild("RosterSettings", new Vector2(-1, -1), false);

            ImGui.Text("Group Roster Management");
            ImGui.Spacing();

            // Table with all members
            DrawRosterTable(group);

            ImGui.EndChild();
        }

        private static void DrawRosterTable(Group group)
        {
            if (group.members == null || group.members.Count == 0)
            {
                GroupUi.Muted("No members in this group");
                return;
            }

            using (var table = ImRaii.Table("RosterTable", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY))
            {
                if (!table) return;

                // Setup columns
                ImGui.TableSetupColumn("Avatar", ImGuiTableColumnFlags.WidthFixed, 40f * ImGui.GetIO().FontGlobalScale);
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 150f * ImGui.GetIO().FontGlobalScale);
                ImGui.TableSetupColumn("Rank", ImGuiTableColumnFlags.WidthFixed, 120f * ImGui.GetIO().FontGlobalScale);
                ImGui.TableSetupColumn("Custom Title", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 100f * ImGui.GetIO().FontGlobalScale);
                ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 80f * ImGui.GetIO().FontGlobalScale);
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableHeadersRow();

                // Draw rows
                foreach (var member in group.members)
                {
                    ImGui.TableNextRow();

                    // Avatar
                    ImGui.TableNextColumn();
                    ImGui.Text("[Avatar]"); // TODO: Implement avatar display with current Dalamud APIs

                    // Name
                    ImGui.TableNextColumn();
                    ImGui.Text(member.name ?? "Unknown");

                    // Rank
                    ImGui.TableNextColumn();
                    ImGui.Text(member.rank?.name ?? "No Rank");

                    // Custom Title
                    ImGui.TableNextColumn();
                    ImGui.Text(""); // Fetch from metadata

                    // Status
                    ImGui.TableNextColumn();
                    GroupUi.Chip(member.owner ? "Owner" : "Member", member.owner ? RsTheme.AccentWarning : RsTheme.AccentSuccess);

                    // Actions
                    ImGui.TableNextColumn();
                    if (RsElements.Button($"Edit##{member.id}", RsElements.ButtonVariant.Ghost))
                    {
                        // Open edit dialog
                    }
                }
            }
        }

        #endregion

        #region Ranks Tab

        private static void DrawRanksTab(Group group)
        {
            ImGui.BeginChild("RanksSettings", new Vector2(-1, -1), false);

            ImGui.Text("Manage group ranks and their base permissions");
            ImGui.Spacing();

            // Existing rank management (keep current implementation from GroupMembers.cs) TODO: Integrate existing rank system

            GroupUi.Muted("Rank management will be integrated here");

            ImGui.EndChild();
        }

        #endregion

        #region Permissions Tab

        private static void DrawPermissionsTab(Group group)
        {
            ImGui.BeginChild("PermissionsSettings", new Vector2(-1, -1), false);

            ImGui.Text("Configure which ranks can access which channels");
            ImGui.Spacing();

            // Matrix of Ranks x Channels with checkboxes for View/Post/Manage
            GroupUi.Muted("Channel permissions matrix will be implemented here");

            ImGui.EndChild();
        }

        #endregion

        #region Settings Tab

        private static void DrawSettingsTab(Group group)
        {
            ImGui.BeginChild("AdvancedSettings", new Vector2(-1, -1), false);

            GroupUi.SectionLabel("Roster Custom Fields");

            ImGui.Text("Define custom fields for your group roster");
            ImGui.Spacing();

            // List existing roster fields
            DrawRosterFieldsList(group);

            ImGui.Spacing();
            GroupUi.Divider();

            // Add new field button
            if (GroupUi.Button("+ Add Custom Field"))
            {
                var newField = new GroupRosterField
                {
                    id = 0,
                    sortOrder = rosterFields.Count,
                    name = "New Field",
                    fieldType = 0,
                    required = false,
                    dropdownOptions = string.Empty
                };
                rosterFields.Add(newField);
                editingField = newField;
            }

            ImGui.SameLine();

            if (GroupUi.Button("Save Roster Fields"))
            {
                Groups_DS.SaveGroupRosterFields(Plugin.character, group.groupID, rosterFields);
            }

            ImGui.EndChild();
        }

        private static int rosterFieldsRequestedFor = -1;

        private static void DrawRosterFieldsList(Group group)
        {
            // Fetch roster fields once per group (this used to send the request every frame while the list was empty)
            if (rosterFields.Count == 0 && rosterFieldsRequestedFor != group.groupID)
            {
                rosterFieldsRequestedFor = group.groupID;
                Groups_DS.FetchGroupRosterFields(Plugin.character, group.groupID);
                // Fields will be populated by data receiver
            }

            using (var table = ImRaii.Table("RosterFieldsTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                if (!table) return;

                ImGui.TableSetupColumn("Field Name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 100f * ImGui.GetIO().FontGlobalScale);
                ImGui.TableSetupColumn("Required", ImGuiTableColumnFlags.WidthFixed, 80f * ImGui.GetIO().FontGlobalScale);
                ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 80f * ImGui.GetIO().FontGlobalScale);
                ImGui.TableHeadersRow();

                for (int i = 0; i < rosterFields.Count; i++)
                {
                    var field = rosterFields[i];

                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.Text(field.name);

                    ImGui.TableNextColumn();
                    string[] fieldTypes = { "Text", "Number", "Date", "Dropdown" };
                    ImGui.Text(fieldTypes[field.fieldType]);

                    ImGui.TableNextColumn();
                    ImGui.Text(field.required ? "Yes" : "No");

                    ImGui.TableNextColumn();
                    if (RsElements.Button($"Edit##{i}", RsElements.ButtonVariant.Ghost))
                    {
                        editingField = field;
                    }
                    ImGui.SameLine();
                    if (RsElements.Button($"Del##{i}", RsElements.ButtonVariant.Danger))
                    {
                        rosterFields.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        #endregion

        #region Join Requests Tab

        private static bool joinRequestsFetched = false;
        private static int lastFetchedGroupID = -1;

        private static void DrawJoinRequestsTab(Group group)
        {
            ImGui.BeginChild("JoinRequestsSettings", new Vector2(-1, -1), false);

            ImGui.Text("Pending Join Requests");
            GroupUi.Muted("Users who want to join your group. Accept or decline their requests.");
            ImGui.Spacing();

            // Fetch join requests if not already fetched or group changed
            if (!joinRequestsFetched || lastFetchedGroupID != group.groupID)
            {
                var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                    x.characterName == Plugin.plugin.playername &&
                    x.characterWorld == Plugin.plugin.playerworld);
                if (character != null)
                {
                    GroupSearch_DS.FetchJoinRequests(character, group.groupID);
                    joinRequestsFetched = true;
                    lastFetchedGroupID = group.groupID;
                }
            }

            // Refresh button
            if (GroupUi.Ghost("Refresh"))
            {
                var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                    x.characterName == Plugin.plugin.playername &&
                    x.characterWorld == Plugin.plugin.playerworld);
                if (character != null)
                {
                    GroupSearch_DS.FetchJoinRequests(character, group.groupID);
                }
            }

            ImGui.Spacing();
            GroupUi.Divider();
            ImGui.Spacing();

            // Get pending requests
            var pendingRequests = group.joinRequests?.Where(r => r.status == 0).ToList() ?? new List<GroupJoinRequest>();

            if (pendingRequests.Count == 0)
            {
                GroupUi.Muted("No pending join requests.");
            }
            else
            {
                ImGui.Text($"{pendingRequests.Count} pending request(s)");
                ImGui.Spacing();

                using (var table = ImRaii.Table("JoinRequestsTable", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
                {
                    if (table)
                    {
                        ImGui.TableSetupColumn("Requester", ImGuiTableColumnFlags.WidthFixed, 180f * ImGui.GetIO().FontGlobalScale);
                        ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthFixed, 100f * ImGui.GetIO().FontGlobalScale);
                        ImGui.TableSetupColumn("Message", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("Requested", ImGuiTableColumnFlags.WidthFixed, 100f * ImGui.GetIO().FontGlobalScale);
                        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 140f * ImGui.GetIO().FontGlobalScale);
                        ImGui.TableSetupScrollFreeze(0, 1);
                        ImGui.TableHeadersRow();

                        foreach (var request in pendingRequests)
                        {
                            ImGui.TableNextRow();

                            // Requester name
                            ImGui.TableNextColumn();
                            ImGui.Text(request.requesterName ?? "Unknown");

                            // World
                            ImGui.TableNextColumn();
                            ImGui.Text(request.requesterWorld ?? "Unknown");

                            // Message
                            ImGui.TableNextColumn();
                            if (!string.IsNullOrEmpty(request.message))
                            {
                                ImGui.TextWrapped(request.message);
                            }
                            else
                            {
                                ImGui.TextDisabled("(No message)");
                            }

                            // Requested time
                            ImGui.TableNextColumn();
                            var requestDate = DateTimeOffset.FromUnixTimeSeconds(request.createdAt).LocalDateTime;
                            ImGui.Text(requestDate.ToString("MM/dd/yy"));
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(requestDate.ToString("F"));
                            }

                            // Actions
                            ImGui.TableNextColumn();
                            if (RsElements.Button($"Accept##{request.requestID}", RsElements.ButtonVariant.Success))
                            {
                                var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                                    x.characterName == Plugin.plugin.playername &&
                                    x.characterWorld == Plugin.plugin.playerworld);
                                if (character != null)
                                {
                                    GroupSearch_DS.RespondToJoinRequest(character, request.requestID, group.groupID, true);
                                    // Remove from local list
                                    group.joinRequests?.Remove(request);
                                }
                            }
                            ImGui.SameLine();
                            if (RsElements.Button($"Decline##{request.requestID}", RsElements.ButtonVariant.Danger))
                            {
                                var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                                    x.characterName == Plugin.plugin.playername &&
                                    x.characterWorld == Plugin.plugin.playerworld);
                                if (character != null)
                                {
                                    GroupSearch_DS.RespondToJoinRequest(character, request.requestID, group.groupID, false);
                                    // Remove from local list
                                    group.joinRequests?.Remove(request);
                                }
                            }
                        }
                    }
                }
            }

            ImGui.EndChild();
        }

        #endregion
    }
}
