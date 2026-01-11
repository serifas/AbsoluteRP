using AbsoluteRP.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AbsoluteRP.Windows.Social.Views.Groups.GroupManager
{
    internal static class GroupRanks
    {
        // UI State
        private static bool showCreateDialog = false;
        private static bool showEditDialog = false;
        private static bool showDeleteConfirm = false;
        private static GroupRank editingRank = null;
        private static GroupRank deletingRank = null;

        // Editing state
        private static string rankName = string.Empty;
        private static string rankDescription = string.Empty;
        private static int rankHierarchy = 0;

        // Default member rank flag
        private static bool isDefaultMember = false;

        // Permission state
        private static bool canInvite = false;
        private static bool canKick = false;
        private static bool canBan = false;
        private static bool canPromote = false;
        private static bool canDemote = false;
        private static bool canCreateAnnouncement = false;
        private static bool canReadMessages = true;
        private static bool canSendMessages = true;
        private static bool canDeleteOthersMessages = false;
        private static bool canPinMessages = false;
        private static bool canCreateCategory = false;
        private static bool canEditCategory = false;
        private static bool canDeleteCategory = false;
        private static bool canLockCategory = false;
        private static bool canCreateForum = false;
        private static bool canEditForum = false;
        private static bool canDeleteForum = false;
        private static bool canLockForum = false;
        private static bool canMuteForum = false;
        private static bool canManageRanks = false;
        private static bool canCreateRanks = false;
        private static bool canManageSelfAssignRoles = false;
        private static bool canCreateForms = false;

        // Fetch state
        private static bool ranksFetched = false;
        private static int lastFetchedGroupID = -1;

        public static void LoadGroupRanks(Group group)
        {
            if (group == null)
                return;

            // Fetch ranks from server if not already fetched for this group
            if (!ranksFetched || lastFetchedGroupID != group.groupID)
            {
                var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x => x.characterName == Plugin.plugin.playername && x.characterWorld == Plugin.plugin.playerworld);
                if (character != null)
                {
                    DataSender.FetchGroupRanks(character, group.groupID);
                    lastFetchedGroupID = group.groupID;
                    ranksFetched = true;
                }
            }

            ImGui.BeginGroup();

            // Header with create button
            ImGui.Text("Group Ranks");
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - 100);
            if (ImGui.Button("Create Rank", new Vector2(100, 0)))
            {
                OpenCreateDialog(group);
            }

            ImGui.Separator();
            ImGui.Spacing();

            // Rank list
            DrawRankList(group);

            ImGui.EndGroup();

            // Dialogs
            DrawCreateEditDialog(group, false);
            DrawCreateEditDialog(group, true);
            DrawDeleteConfirmation(group);

            // Show operation result
            if (!string.IsNullOrEmpty(DataReceiver.rankOperationMessage))
            {
                if (DataReceiver.rankOperationSuccess)
                {
                    ImGui.TextColored(new Vector4(0.3f, 1.0f, 0.3f, 1.0f), DataReceiver.rankOperationMessage);
                }
                else
                {
                    ImGui.TextColored(new Vector4(1.0f, 0.3f, 0.3f, 1.0f), DataReceiver.rankOperationMessage);
                }
            }
        }

        private static void DrawRankList(Group group)
        {
            if (DataReceiver.ranks == null || DataReceiver.ranks.Count == 0)
            {
                ImGui.TextDisabled("No ranks created yet. Click 'Create Rank' to add one.");
                return;
            }

            // Filter ranks for this group and sort by hierarchy (highest first)
            var groupRanks = DataReceiver.ranks
                .Where(r => r.groupID == group.groupID)
                .OrderByDescending(r => r.hierarchy)
                .ToList();

            if (groupRanks.Count == 0)
            {
                ImGui.TextDisabled("No ranks created yet. Click 'Create Rank' to add one.");
                return;
            }

            using var child = ImRaii.Child("RankListChild", new Vector2(-1, -1), true);
            if (!child)
                return;

            using var table = ImRaii.Table("RanksTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg);
            if (!table)
                return;

            // Setup columns
            ImGui.TableSetupColumn("Hierarchy", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableHeadersRow();

            // Draw ranks
            foreach (var rank in groupRanks)
            {
                ImGui.TableNextRow();

                // Hierarchy
                ImGui.TableSetColumnIndex(0);
                ImGui.Text(rank.hierarchy.ToString());

                // Name
                ImGui.TableSetColumnIndex(1);
                ImGui.Text(rank.name ?? "Unnamed Rank");

                // Description
                ImGui.TableSetColumnIndex(2);
                ImGui.TextWrapped(rank.description ?? string.Empty);

                // Actions
                ImGui.TableSetColumnIndex(3);

                if (ImGui.SmallButton($"Edit##{rank.id}"))
                {
                    OpenEditDialog(group, rank);
                }

                ImGui.SameLine();

                if (ImGui.SmallButton($"Delete##{rank.id}"))
                {
                    OpenDeleteConfirm(rank);
                }
            }
        }

        private static void OpenCreateDialog(Group group)
        {
            showCreateDialog = true;
            showEditDialog = false;
            editingRank = null;

            // Reset to defaults
            rankName = string.Empty;
            rankDescription = string.Empty;
            rankHierarchy = 0;
            isDefaultMember = false;

            // Default permissions
            canInvite = false;
            canKick = false;
            canBan = false;
            canPromote = false;
            canDemote = false;
            canCreateAnnouncement = false;
            canReadMessages = true;
            canSendMessages = true;
            canDeleteOthersMessages = false;
            canPinMessages = false;
            canCreateCategory = false;
            canEditCategory = false;
            canDeleteCategory = false;
            canLockCategory = false;
            canCreateForum = false;
            canEditForum = false;
            canDeleteForum = false;
            canLockForum = false;
            canMuteForum = false;
            canManageRanks = false;
            canCreateRanks = false;
            canManageSelfAssignRoles = false;
            canCreateForms = false;
        }

        private static void OpenEditDialog(Group group, GroupRank rank)
        {
            showEditDialog = true;
            showCreateDialog = false;
            editingRank = rank;

            // Load rank data
            rankName = rank.name ?? string.Empty;
            rankDescription = rank.description ?? string.Empty;
            rankHierarchy = rank.hierarchy;
            isDefaultMember = rank.isDefaultMember;

            // Load permissions
            if (rank.permissions != null)
            {
                canInvite = rank.permissions.canInvite;
                canKick = rank.permissions.canKick;
                canBan = rank.permissions.canBan;
                canPromote = rank.permissions.canPromote;
                canDemote = rank.permissions.canDemote;
                canCreateAnnouncement = rank.permissions.canCreateAnnouncement;
                canReadMessages = rank.permissions.canReadMessages;
                canSendMessages = rank.permissions.canSendMessages;
                canDeleteOthersMessages = rank.permissions.canDeleteOthersMessages;
                canPinMessages = rank.permissions.canPinMessages;
                canCreateCategory = rank.permissions.canCreateCategory;
                canEditCategory = rank.permissions.canEditCategory;
                canDeleteCategory = rank.permissions.canDeleteCategory;
                canLockCategory = rank.permissions.canLockCategory;
                canCreateForum = rank.permissions.canCreateForum;
                canEditForum = rank.permissions.canEditForum;
                canDeleteForum = rank.permissions.canDeleteForum;
                canLockForum = rank.permissions.canLockForum;
                canMuteForum = rank.permissions.canMuteForum;
                canManageRanks = rank.permissions.canManageRanks;
                canCreateRanks = rank.permissions.canCreateRanks;
                canManageSelfAssignRoles = rank.permissions.canManageSelfAssignRoles;
                canCreateForms = rank.permissions.canCreateForms;
            }
        }

        private static void OpenDeleteConfirm(GroupRank rank)
        {
            showDeleteConfirm = true;
            deletingRank = rank;
        }

        private static void DrawCreateEditDialog(Group group, bool isEdit)
        {
            string title = isEdit ? "Edit Rank" : "Create New Rank";
            bool shouldShow = isEdit ? showEditDialog : showCreateDialog;

            if (!shouldShow)
                return;

            // Open popup once when flag is set
            if (!ImGui.IsPopupOpen(title))
            {
                ImGui.OpenPopup(title);
            }

            ImGui.SetNextWindowSize(new Vector2(600, 700), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool open = true;
            if (!ImGui.BeginPopupModal(title, ref open, ImGuiWindowFlags.NoResize))
                return;

            // Check if user closed via X button
            if (!open)
            {
                showCreateDialog = false;
                showEditDialog = false;
                editingRank = null;
                DataReceiver.rankOperationMessage = string.Empty;
                ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
                return;
            }

            // Rank name
            ImGui.Text("Rank Name:");
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##RankName", ref rankName, 100);

            ImGui.Spacing();

            // Rank description
            ImGui.Text("Description:");
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextMultiline("##RankDescription", ref rankDescription, 500, new Vector2(-1, 60));

            ImGui.Spacing();

            // Hierarchy
            ImGui.Text("Hierarchy (higher = more powerful):");
            ImGui.SetNextItemWidth(-1);
            ImGui.InputInt("##RankHierarchy", ref rankHierarchy);

            ImGui.Spacing();

            // Default Member Rank checkbox
            ImGui.Checkbox("Default Member Rank", ref isDefaultMember);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("When checked, new members joining the group will be assigned this rank automatically");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Permissions
            ImGui.Text("Permissions:");

            using (var child = ImRaii.Child("PermissionsChild", new Vector2(-1, 400), true))
            {
                if (child)
                {
                    DrawPermissionEditor();
                }
            }

            ImGui.Spacing();

            // Buttons
            if (ImGui.Button(isEdit ? "Save Changes" : "Create Rank", new Vector2(120, 0)))
            {
                SaveRank(group, isEdit);
            }

            ImGui.SameLine();

            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                showCreateDialog = false;
                showEditDialog = false;
                editingRank = null;
                DataReceiver.rankOperationMessage = string.Empty;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }

        private static void DrawPermissionEditor()
        {
            // Member Permissions
            if (ImGui.CollapsingHeader("Member Permissions", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();
                ImGui.Checkbox("Can Invite Members", ref canInvite);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Allow this rank to invite new members to the group");

                ImGui.Checkbox("Can Kick Members", ref canKick);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Allow this rank to kick members from the group");

                ImGui.Checkbox("Can Ban Members", ref canBan);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Allow this rank to ban members from the group");

                ImGui.Checkbox("Can Promote Members", ref canPromote);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Allow this rank to promote members to higher ranks");

                ImGui.Checkbox("Can Demote Members", ref canDemote);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Allow this rank to demote members to lower ranks");

                ImGui.Unindent();
            }

            ImGui.Spacing();

            // Message Permissions
            if (ImGui.CollapsingHeader("Message Permissions", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();
                ImGui.Checkbox("Can Create Announcements", ref canCreateAnnouncement);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Allow this rank to create announcements");

                ImGui.Checkbox("Can Read Messages", ref canReadMessages);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Allow this rank to read messages in channels");

                ImGui.Checkbox("Can Send Messages", ref canSendMessages);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Allow this rank to send messages in channels");

                ImGui.Checkbox("Can Delete Others' Messages", ref canDeleteOthersMessages);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Allow this rank to delete messages from other members");

                ImGui.Checkbox("Can Pin Messages", ref canPinMessages);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Allow this rank to pin important messages");

                ImGui.Unindent();
            }

            ImGui.Spacing();

            // Category Permissions
            if (ImGui.CollapsingHeader("Category Permissions", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();
                ImGui.Checkbox("Can Create Categories", ref canCreateCategory);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Allow this rank to create new categories");

                ImGui.Checkbox("Can Edit Categories", ref canEditCategory);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Allow this rank to edit existing categories");

                ImGui.Checkbox("Can Delete Categories", ref canDeleteCategory);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Allow this rank to delete categories");

                ImGui.Checkbox("Can Lock Categories", ref canLockCategory);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Allow this rank to lock/unlock categories");

                ImGui.Unindent();
            }

            ImGui.Spacing();

            // Forum Permissions
            if (ImGui.CollapsingHeader("Forum (Channel) Permissions", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();
                ImGui.Checkbox("Can Create Forums", ref canCreateForum);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Allow this rank to create new forum channels");

                ImGui.Checkbox("Can Edit Forums", ref canEditForum);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Allow this rank to edit existing forum channels");

                ImGui.Checkbox("Can Delete Forums", ref canDeleteForum);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Allow this rank to delete forum channels");

                ImGui.Checkbox("Can Lock Forums", ref canLockForum);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Allow this rank to lock/unlock forum channels");

                ImGui.Unindent();
            }

            ImGui.Spacing();

            // Rank Management Permissions
            if (ImGui.CollapsingHeader("Rank Management Permissions", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();
                ImGui.Checkbox("Can Create Ranks", ref canCreateRanks);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Allow this rank to create new ranks below their own hierarchy level");

                ImGui.Checkbox("Can Manage Ranks", ref canManageRanks);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Allow this rank to edit and delete ranks below their own hierarchy level");

                ImGui.Unindent();
            }

            ImGui.Spacing();

            // Special Permissions
            if (ImGui.CollapsingHeader("Special Permissions", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();
                ImGui.Checkbox("Can Manage Self-Assign Roles", ref canManageSelfAssignRoles);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Allow this rank to create, edit, and delete self-assignable roles");

                ImGui.Checkbox("Can Create Forms", ref canCreateForms);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Allow this rank to create and manage form channels");

                ImGui.Unindent();
            }
        }

        private static void SaveRank(Group group, bool isEdit)
        {
            // Validate
            if (string.IsNullOrWhiteSpace(rankName))
            {
                DataReceiver.rankOperationSuccess = false;
                DataReceiver.rankOperationMessage = "Rank name cannot be empty";
                return;
            }

            var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x => x.characterName == Plugin.plugin.playername && x.characterWorld == Plugin.plugin.playerworld);
            if (character == null)
            {
                DataReceiver.rankOperationSuccess = false;
                DataReceiver.rankOperationMessage = "No active character";
                return;
            }

            // Create rank object
            var rank = new GroupRank
            {
                id = isEdit ? editingRank.id : 0,
                groupID = group.groupID,
                name = rankName.Trim(),
                description = rankDescription.Trim(),
                hierarchy = rankHierarchy,
                isDefaultMember = isDefaultMember,
                permissions = new GroupRankPermissions
                {
                    // Member Permissions
                    canInvite = canInvite,
                    canKick = canKick,
                    canBan = canBan,
                    canPromote = canPromote,
                    canDemote = canDemote,

                    // Message Permissions
                    canCreateAnnouncement = canCreateAnnouncement,
                    canReadMessages = canReadMessages,
                    canSendMessages = canSendMessages,
                    canDeleteOthersMessages = canDeleteOthersMessages,
                    canPinMessages = canPinMessages,

                    // Category Permissions
                    canCreateCategory = canCreateCategory,
                    canEditCategory = canEditCategory,
                    canDeleteCategory = canDeleteCategory,
                    canLockCategory = canLockCategory,

                    // Forum Permissions
                    canCreateForum = canCreateForum,
                    canEditForum = canEditForum,
                    canDeleteForum = canDeleteForum,
                    canLockForum = canLockForum,
                    canMuteForum = canMuteForum,

                    // Rank Management Permissions
                    canManageRanks = canManageRanks,
                    canCreateRanks = canCreateRanks,

                    // Special Permissions
                    canManageSelfAssignRoles = canManageSelfAssignRoles,
                    canCreateForms = canCreateForms
                }
            };

            // Send to server
            DataSender.SaveGroupRank(character, rank);

            // Close dialog
            showCreateDialog = false;
            showEditDialog = false;
            editingRank = null;

            // Refresh ranks after a short delay
            System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
            {
                DataSender.FetchGroupRanks(character, group.groupID);
            });
        }

        private static void DrawDeleteConfirmation(Group group)
        {
            if (!showDeleteConfirm || deletingRank == null)
                return;

            // Open popup once when flag is set
            if (!ImGui.IsPopupOpen("Delete Rank?"))
            {
                ImGui.OpenPopup("Delete Rank?");
            }

            ImGui.SetNextWindowSize(new Vector2(400, 150), ImGuiCond.Always);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool open = true;
            if (!ImGui.BeginPopupModal("Delete Rank?", ref open, ImGuiWindowFlags.NoResize))
                return;

            // Check if user closed via X button
            if (!open)
            {
                showDeleteConfirm = false;
                deletingRank = null;
                ImGui.EndPopup();
                return;
            }

            ImGui.TextWrapped($"Are you sure you want to delete the rank '{deletingRank?.name}'?");
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 1), "Members with this rank will have their rank set to none.");
            ImGui.Spacing();

            if (ImGui.Button("Delete", new Vector2(120, 0)))
            {
                var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x => x.characterName == Plugin.plugin.playername && x.characterWorld == Plugin.plugin.playerworld);
                if (character != null && deletingRank != null)
                {
                    DataSender.DeleteGroupRank(character, deletingRank.id, group.groupID);

                    // Refresh ranks after a short delay
                    System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
                    {
                        DataSender.FetchGroupRanks(character, group.groupID);
                    });
                }

                showDeleteConfirm = false;
                deletingRank = null;
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();

            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                showDeleteConfirm = false;
                deletingRank = null;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }

        public static void ShowInviteeProfile(int profileID, string profileName)
        {
            // TODO: Implement invitee profile display
            Plugin.PluginLog.Info($"Showing invitee profile: {profileName} (ID: {profileID})");
        }
    }
}
