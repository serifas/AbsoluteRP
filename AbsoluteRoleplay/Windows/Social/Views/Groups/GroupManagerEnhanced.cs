using AbsoluteRP.Helpers;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AbsoluteRP.Windows.Social.Views.Groups
{
    /// <summary>
    /// Enhanced Group Manager with Discord-like settings and guild roster management
    /// </summary>
    internal class GroupManagerEnhanced
    {
        private static FileDialogManager _fileDialogManager;
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
                _fileDialogManager = new FileDialogManager();

            _fileDialogManager.Draw();

            // Header with group logo and name
            DrawGroupHeader(group);

            ImGui.Separator();
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

                ImGui.EndTabBar();
            }
        }

        private static void DrawGroupHeader(Group group)
        {
            ImGui.BeginGroup();

            // Logo
            Misc.DrawCenteredImage(group.logo, new Vector2(60f, 60f) * ImGui.GetIO().FontGlobalScale, false);

            ImGui.SameLine();

            // Group name and description
            ImGui.BeginGroup();
            ImGui.Text(group.name);

            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), group.description ?? "No description");

            // Quick stats
            ImGui.Text($"Members: {group.members?.Count ?? 0} | Ranks: {group.ranks?.Count ?? 0} | Channels: {CountTotalChannels(group)}");
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

            ImGui.Separator();
            ImGui.Text("Basic Information");

            // Group Name
            ImGui.Text("Group Name:");
            string groupName = group.name ?? string.Empty;
            if (ImGui.InputText("##GroupName", ref groupName, 100))
            {
                group.name = groupName;
            }

            // Group Description
            ImGui.Text("Description:");
            string groupDesc = group.description ?? string.Empty;
            if (ImGui.InputTextMultiline("##GroupDesc", ref groupDesc, 1000, new Vector2(-1, 80f * ImGui.GetIO().FontGlobalScale)))
            {
                group.description = groupDesc;
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Appearance");

            // Logo
            ImGui.Text("Group Logo:");
            if (Misc.DrawCenteredButton("Change Logo"))
            {
                Misc.EditGroupImage(Plugin.plugin, _fileDialogManager, group, true, false, 0);
            }

            // Background
            ImGui.Text("Group Background:");
            if (Misc.DrawCenteredButton("Change Background"))
            {
                Misc.EditGroupImage(Plugin.plugin, _fileDialogManager, group, false, true, 0);
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Visibility & Access");

            // Visibility
            bool visible = group.visible;
            if (ImGui.Checkbox("Visible in Group Search", ref visible))
            {
                group.visible = visible;
            }
            UIHelpers.SelectableHelpMarker("If enabled, this group will appear in public group searches");

            // Open Invite
            bool openInvite = group.openInvite;
            if (ImGui.Checkbox("Open Invitations", ref openInvite))
            {
                group.openInvite = openInvite;
            }
            UIHelpers.SelectableHelpMarker("If enabled, anyone can join without approval");

            ImGui.Spacing();
            ImGui.Separator();

            // Save button
            if (Misc.DrawCenteredButton("Save General Settings"))
            {
                DataSender.SetGroupValues(Plugin.character, group, true, 0, 0);
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

                if (ImGui.Selectable($"📁 {category.name}##cat_{i}", editingCategory == category))
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
                        string icon = channel.channelType == 1 ? "📢" : "#";

                        if (ImGui.Selectable($"{icon} {channel.name}##ch_{j}", editingChannel == channel))
                        {
                            editingChannel = channel;
                            editingCategory = category;
                        }
                    }
                    ImGui.Unindent(15f * ImGui.GetIO().FontGlobalScale);
                }

                ImGui.Spacing();
            }

            ImGui.EndChild();

            // Add category button
            if (ImGui.Button("+ New Category", new Vector2(-1, 0)))
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
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "Select a category or channel to edit");
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
            ImGui.Separator();
            ImGui.Text("Category Settings");

            // Category Name
            ImGui.Text("Category Name:");
            string catName = category.name ?? string.Empty;
            if (ImGui.InputText("##CategoryName", ref catName, 100))
            {
                category.name = catName;
            }

            // Description
            ImGui.Text("Description:");
            string catDesc = category.description ?? string.Empty;
            if (ImGui.InputTextMultiline("##CategoryDesc", ref catDesc, 500, new Vector2(-1, 60f * ImGui.GetIO().FontGlobalScale)))
            {
                category.description = catDesc;
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Channels in this Category");

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
            if (ImGui.Button("+ Add Channel"))
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
            if (ImGui.Button("Delete Category"))
            {
                group.categories.Remove(category);
                editingCategory = null;
            }

            ImGui.Spacing();
            ImGui.Separator();

            if (ImGui.Button("Save Categories", new Vector2(-1, 0)))
            {
                DataSender.SaveGroupCategories(Plugin.character, group.groupID, group.categories);
            }
        }

        private static void DrawChannelEditor(Group group, GroupChannel channel)
        {
            ImGui.Separator();
            ImGui.Text("Channel Settings");

            // Channel Name
            ImGui.Text("Channel Name:");
            string chName = channel.name ?? string.Empty;
            if (ImGui.InputText("##ChannelName", ref chName, 100))
            {
                channel.name = chName.ToLower().Replace(" ", "-");
            }

            // Description
            ImGui.Text("Description:");
            string chDesc = channel.description ?? string.Empty;
            if (ImGui.InputTextMultiline("##ChannelDesc", ref chDesc, 500, new Vector2(-1, 60f * ImGui.GetIO().FontGlobalScale)))
            {
                channel.description = chDesc;
            }

            // Channel Type
            ImGui.Text("Channel Type:");
            string[] channelTypes = { "Text Channel", "Announcement Channel" };
            int currentType = channel.channelType;
            if (ImGui.Combo("##ChannelType", ref currentType, channelTypes, channelTypes.Length))
            {
                channel.channelType = currentType;
            }

            ImGui.Spacing();

            // Delete channel button
            if (ImGui.Button("Delete Channel"))
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
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "No members in this group");
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
                    Vector4 statusColor = member.owner ? new Vector4(1f, 0.8f, 0.2f, 1f) : new Vector4(0.6f, 1f, 0.6f, 1f);
                    ImGui.TextColored(statusColor, member.owner ? "Owner" : "Member");

                    // Actions
                    ImGui.TableNextColumn();
                    if (ImGui.SmallButton($"Edit##{member.id}"))
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

            // Existing rank management (keep current implementation from GroupMembers.cs)
            // TODO: Integrate existing rank system

            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "Rank management will be integrated here");

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
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "Channel permissions matrix will be implemented here");

            ImGui.EndChild();
        }

        #endregion

        #region Settings Tab

        private static void DrawSettingsTab(Group group)
        {
            ImGui.BeginChild("AdvancedSettings", new Vector2(-1, -1), false);

            ImGui.Separator();
            ImGui.Text("Roster Custom Fields");

            ImGui.Text("Define custom fields for your group roster");
            ImGui.Spacing();

            // List existing roster fields
            DrawRosterFieldsList(group);

            ImGui.Spacing();
            ImGui.Separator();

            // Add new field button
            if (ImGui.Button("+ Add Custom Field"))
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

            if (ImGui.Button("Save Roster Fields"))
            {
                DataSender.SaveGroupRosterFields(Plugin.character, group.groupID, rosterFields);
            }

            ImGui.EndChild();
        }

        private static void DrawRosterFieldsList(Group group)
        {
            // Fetch roster fields if not loaded
            if (rosterFields.Count == 0)
            {
                DataSender.FetchGroupRosterFields(Plugin.character, group.groupID);
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
                    if (ImGui.SmallButton($"Edit##{i}"))
                    {
                        editingField = field;
                    }
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"Del##{i}"))
                    {
                        rosterFields.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        #endregion
    }
}
