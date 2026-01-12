using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Social.Views.Groups.GroupManager;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRP.Windows.Social.Views.SubViews
{
    internal class GroupManager
    {
        public static List<ProfileData> profiles = new List<ProfileData>();
        public static ProfileData groupLeaderProfile = new ProfileData();
        public static ProfileData groupProfile = new ProfileData();
        public static FileDialogManager _fileDialogManager;
        public static int profileIndex;
        public static int leadProfileIndex;
        private static bool showDeleteConfirmation = false;
        private static int groupToDelete = -1;
        private static bool showLeaveConfirmation = false;
        private static bool showTransferOwnershipDialog = false;
        private static int selectedMemberForOwnership = -1;
        private static int lastLoadedGroupID = -1;

        private static string groupNameInput = string.Empty;
        private static bool bansFetched = false;

        public static void ManageGroup(Group group)
        {
            // Fetch all group data if this is a new group or first load
            if (lastLoadedGroupID != group.groupID)
            {
                lastLoadedGroupID = group.groupID;
                groupNameInput = group.name ?? string.Empty;
                bansFetched = false;

                // Fetch members
                DataSender.FetchGroupMembers(Plugin.character, group.groupID);

                // Fetch ranks
                DataSender.FetchGroupRanks(Plugin.character, group.groupID);

                // Fetch categories (for Communication tab)
                DataSender.FetchGroupCategories(Plugin.character, group.groupID);

                // Fetch forum structure (for Communication tab)
                DataSender.FetchForumStructure(Plugin.character, group.groupID);

                // Fetch pending invites (for Invites tab)
                DataSender.FetchGroupInvites(Plugin.character, false, group.groupID);

                // Fetch join requests (for Join Requests tab)
                DataSender.FetchJoinRequests(Plugin.character, group.groupID);

                // Fetch roster fields if they exist
                DataSender.FetchGroupRosterFields(Plugin.character, group.groupID);

                // Fetch bans (for Bans tab)
                DataSender.FetchGroupBans(Plugin.character, group.groupID);
            }

            if (_fileDialogManager == null)
                _fileDialogManager = new Dalamud.Interface.ImGuiFileDialog.FileDialogManager();

            _fileDialogManager.Draw();
            Misc.DrawCenteredImage(group.logo, new System.Numerics.Vector2(ImGui.GetIO().FontGlobalScale * 50, ImGui.GetIO().FontGlobalScale * 50), false);

            // Check if current user is owner
            bool isOwner = group.members != null && group.members.Count > 0
                ? GroupPermissions.IsOwner(group)
                : false;

            if (isOwner)
            {
                if (Misc.DrawCenteredButton("Change Logo"))
                {
                    Misc.EditGroupImage(Plugin.plugin, _fileDialogManager, group, true, false, 0);
                }

                // Group name input (owner only)
                ImGui.Text("Group Name");
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##group_name", ref groupNameInput, 100))
                {
                    group.name = groupNameInput;
                }

                ImGui.Text("Group Leader");
                AddGroupLeaderSelection();
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Select the profile that will be represented as the leader of this group");
                }
                ImGui.Text("Group Profile");
                AddGroupProfileSelection();
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Select the profile that will represent the group");
                }
            }

            if (isOwner)
            {
                if (Misc.DrawCenteredButton("Save Settings"))
                {
                    DataSender.SetGroupValues(Plugin.character, group, true, leadProfileIndex, profileIndex);
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Transfer Ownership button (only for owners)
            if (isOwner)
            {
                if (Misc.DrawCenteredButton("Transfer Ownership"))
                {
                    showTransferOwnershipDialog = true;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Transfer ownership of this group to another member");
                }

                ImGui.Spacing();
            }

            // Leave Group button (not for owners - they must transfer or delete)
            if (!isOwner)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.6f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.0f, 0.7f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.9f, 0.5f, 0.1f, 1.0f));

                if (Misc.DrawCenteredButton("Leave Group"))
                {
                    showLeaveConfirmation = true;
                }

                ImGui.PopStyleColor(3);

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Leave this group");
                }

                ImGui.Spacing();
            }

            // Delete Group button (requires Ctrl to be held, only for owners)
            if (isOwner)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
            }

            // Delete Group button (only for owners)
            if (isOwner)
            {
                bool ctrlHeld = ImGui.GetIO().KeyCtrl;

                if (!ctrlHeld)
                {
                    ImGui.BeginDisabled();
                }

                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.9f, 0.1f, 0.1f, 1.0f));

                if (Misc.DrawCenteredButton("Delete Group (Hold Ctrl)"))
                {
                    showDeleteConfirmation = true;
                    groupToDelete = group.groupID;
                }

                ImGui.PopStyleColor(3);

                if (!ctrlHeld)
                {
                    ImGui.EndDisabled();
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    {
                        ImGui.SetTooltip("Hold Ctrl to enable this button");
                    }
                }
                else
                {
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Permanently delete this group and all its data");
                    }
                }
            }

            // Dialogs
            DrawDeleteConfirmationPopup(group);
            DrawLeaveConfirmationPopup(group);
            DrawTransferOwnershipDialog(group);

            ImGui.BeginTabBar("MouseTargetTooltipOptions");

            if (ImGui.BeginTabItem("Members"))
            {
                GroupMembers.LoadGroupMembers(group);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Ranks"))
            {
                GroupRanks.LoadGroupRanks(group);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Communication"))
            {
                GroupCommunication.LoadGroupCommunication(group);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Invites"))
            {
                DrawInvitesTab(group);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Join Requests"))
            {
                DrawJoinRequestsTab(group);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Bans"))
            {
                // Fetch bans when entering the tab if not already fetched
                if (!bansFetched)
                {
                    bansFetched = true;
                    DataSender.FetchGroupBans(Plugin.character, group.groupID);
                }
                DrawBansTab(group);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Access"))
            {
                DrawAccessTab(group);
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }

        public static void AddGroupProfileSelection()
        {
            try
            {
                if (profiles == null || profiles.Count == 0)
                {
                    ImGui.TextDisabled("No profiles available");
                    return;
                }

                List<string> profileNames = new List<string>();
                for (int i = 0; i < profiles.Count; i++)
                {
                    profileNames.Add(profiles[i].title);
                }
                string[] ProfileNames = profileNames.ToArray();

                // Ensure profileIndex is within bounds
                if (profileIndex < 0 || profileIndex >= ProfileNames.Length)
                {
                    profileIndex = 0;
                }

                var profileName = ProfileNames[profileIndex];

                using var combo = ImRaii.Combo("##GroupProfile", profileName);
                if (!combo)
                    return;
                foreach (var (newText, idx) in ProfileNames.WithIndex())
                {
                    if (profiles.Count > 0)
                    {
                        var label = newText;
                        if (label == string.Empty)
                        {
                            label = "New Profile";
                        }
                        if (newText != string.Empty)
                        {
                            if (ImGui.Selectable(label + "##" + idx, idx == profileIndex))
                            {
                                groupProfile = profiles[idx];
                                profileIndex = idx;
                            }
                            UIHelpers.SelectableHelpMarker("Select to edit tooltipData");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("ProfileWindow AddProfileSelection Debug: " + ex.Message);
            }
        }
        public static void AddGroupLeaderSelection()
        {
            try
            {
                if (profiles == null || profiles.Count == 0)
                {
                    ImGui.TextDisabled("No profiles available");
                    return;
                }

                List<string> profileNames = new List<string>();
                for (int i = 0; i < profiles.Count; i++)
                {
                    profileNames.Add(profiles[i].title);
                }
                string[] ProfileNames = profileNames.ToArray();

                // Ensure leadProfileIndex is within bounds
                if (leadProfileIndex < 0 || leadProfileIndex >= ProfileNames.Length)
                {
                    leadProfileIndex = 0;
                }

                var profileName = ProfileNames[leadProfileIndex];

                using var combo = ImRaii.Combo("##GroupLeader", profileName);
                if (!combo)
                    return;
                foreach (var (newText, idx) in ProfileNames.WithIndex())
                {
                    if (profiles.Count > 0)
                    {
                        var label = newText;
                        if (label == string.Empty)
                        {
                            label = "New Profile";
                        }
                        if (newText != string.Empty)
                        {
                            if (ImGui.Selectable(label + "##" + idx, idx == leadProfileIndex))
                            {
                                groupLeaderProfile = profiles[idx];
                                leadProfileIndex = idx;
                            }
                            UIHelpers.SelectableHelpMarker("Select to edit tooltipData");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("ProfileWindow AddProfileSelection Debug: " + ex.Message);
            }
        }

        private static void DrawDeleteConfirmationPopup(Group group)
        {
            if (showDeleteConfirmation)
            {
                ImGui.OpenPopup("Delete Group Confirmation");
                showDeleteConfirmation = false;
            }

            ImGui.SetNextWindowSize(new Vector2(400, 200), ImGuiCond.Appearing);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool open = true;
            if (ImGui.BeginPopupModal("Delete Group Confirmation", ref open, ImGuiWindowFlags.NoResize))
            {
                ImGui.TextWrapped($"Are you sure you want to permanently delete \"{group.name}\"?");
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(1.0f, 0.4f, 0.4f, 1.0f), "WARNING: This action cannot be undone!");
                ImGui.TextWrapped("All group data including members, ranks, messages, and settings will be permanently deleted.");
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - 240) * 0.5f);

                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.9f, 0.1f, 0.1f, 1.0f));

                if (ImGui.Button("Delete Permanently", new Vector2(120, 0)))
                {
                    var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                        x.characterName == Plugin.plugin.playername &&
                        x.characterWorld == Plugin.plugin.playerworld);

                    if (character != null)
                    {
                        DataSender.DeleteGroup(character, groupToDelete);
                        Plugin.PluginLog.Info($"Deleting group {groupToDelete}");

                        // Remove the group from the local groups list
                        var groupToRemove = GroupsData.groups.FirstOrDefault(g => g.groupID == groupToDelete);
                        if (groupToRemove != null)
                        {
                            GroupsData.groups.Remove(groupToRemove);
                            Plugin.PluginLog.Info($"Removed group {groupToDelete} from local list");
                        }

                        // Close the group management view
                        GroupsData.manageGroup = false;
                        GroupsData.currentGroup = null;
                    }

                    ImGui.CloseCurrentPopup();
                }

                ImGui.PopStyleColor(3);

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                {
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }

        private static void DrawLeaveConfirmationPopup(Group group)
        {
            if (showLeaveConfirmation)
            {
                ImGui.OpenPopup("Leave Group Confirmation");
                showLeaveConfirmation = false;
            }

            ImGui.SetNextWindowSize(new Vector2(400, 200), ImGuiCond.Appearing);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool open = true;
            if (ImGui.BeginPopupModal("Leave Group Confirmation", ref open, ImGuiWindowFlags.NoResize))
            {
                ImGui.TextWrapped($"Are you sure you want to leave \"{group.name}\"?");
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.4f, 1.0f), "You will lose access to all group content.");
                ImGui.TextWrapped("You can be re-invited by a member with invite permissions.");
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - 240) * 0.5f);

                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.6f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.0f, 0.7f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.9f, 0.5f, 0.1f, 1.0f));

                if (ImGui.Button("Leave Group", new Vector2(120, 0)))
                {
                    var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                        x.characterName == Plugin.plugin.playername &&
                        x.characterWorld == Plugin.plugin.playerworld);

                    if (character != null)
                    {
                        DataSender.LeaveGroup(character, group.groupID);
                        Plugin.PluginLog.Info($"Leaving group {group.groupID}");

                        // Close the group management view
                        GroupsData.manageGroup = false;
                        GroupsData.currentGroup = null;
                    }

                    ImGui.CloseCurrentPopup();
                }

                ImGui.PopStyleColor(3);

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                {
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }

        private static void DrawTransferOwnershipDialog(Group group)
        {
            if (showTransferOwnershipDialog)
            {
                ImGui.OpenPopup("Transfer Ownership");
                showTransferOwnershipDialog = false;
                selectedMemberForOwnership = -1;
            }

            ImGui.SetNextWindowSize(new Vector2(500, 400), ImGuiCond.Appearing);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool open = true;
            if (ImGui.BeginPopupModal("Transfer Ownership", ref open, ImGuiWindowFlags.NoResize))
            {
                ImGui.TextWrapped($"Select a member to transfer ownership of \"{group.name}\":");
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(1.0f, 0.4f, 0.4f, 1.0f), "WARNING: You will lose owner privileges!");
                ImGui.TextWrapped("The new owner will have full control over the group, including the ability to remove you.");
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Member selection list
                using (var child = ImRaii.Child("MembersList", new Vector2(-1, 200), true))
                {
                    if (group.members != null)
                    {
                        var currentUserID = Plugin.plugin.Configuration.account.userID;

                        foreach (var member in group.members.Where(m => m.userID != currentUserID))
                        {
                            bool isSelected = selectedMemberForOwnership == member.id;

                            if (ImGui.Selectable($"{member.name}##Member{member.id}", isSelected))
                            {
                                selectedMemberForOwnership = member.id;
                            }

                            if (isSelected)
                            {
                                ImGui.SetItemDefaultFocus();
                            }

                            // Show rank
                            if (member.rank != null && !string.IsNullOrEmpty(member.rank.name))
                            {
                                ImGui.SameLine();
                                ImGui.TextDisabled($"({member.rank.name})");
                            }
                        }
                    }
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Transfer button
                ImGui.BeginDisabled(selectedMemberForOwnership <= 0);

                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.6f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.0f, 0.7f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.9f, 0.5f, 0.1f, 1.0f));

                if (ImGui.Button("Transfer Ownership", new Vector2(150, 0)))
                {
                    var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                        x.characterName == Plugin.plugin.playername &&
                        x.characterWorld == Plugin.plugin.playerworld);

                    if (character != null && selectedMemberForOwnership > 0)
                    {
                        var selectedMember = group.members.FirstOrDefault(m => m.id == selectedMemberForOwnership);
                        if (selectedMember != null)
                        {
                            DataSender.TransferGroupOwnership(character, group.groupID, selectedMemberForOwnership, selectedMember.userID);
                            Plugin.PluginLog.Info($"Transferring ownership of group {group.groupID} to member {selectedMemberForOwnership}");

                            // Close the group management view
                            GroupsData.manageGroup = false;
                            GroupsData.currentGroup = null;
                        }
                    }

                    ImGui.CloseCurrentPopup();
                }

                ImGui.PopStyleColor(3);
                ImGui.EndDisabled();

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                {
                    ImGui.CloseCurrentPopup();
                }

                // Show selected member
                if (selectedMemberForOwnership > 0)
                {
                    var selectedMember = group.members?.FirstOrDefault(m => m.id == selectedMemberForOwnership);
                    if (selectedMember != null)
                    {
                        ImGui.Spacing();
                        ImGui.TextColored(new Vector4(0.5f, 1f, 0.5f, 1f), $"Selected: {selectedMember.name}");
                    }
                }

                ImGui.EndPopup();
            }
        }

        private static void DrawInvitesTab(Group group)
        {
            if (group.invites == null || group.invites.Count == 0)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "No pending invites.");
                return;
            }

            // Get current user to check permissions
            var currentMember = group.members?.FirstOrDefault(m => m.userID == Plugin.plugin.Configuration.account.userID);
            bool canManageInvites = currentMember?.owner == true || currentMember?.rank?.permissions?.canInvite == true;

            ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), $"Pending Invites ({group.invites.Count})");
            ImGui.Separator();
            ImGui.Spacing();

            // Filter to only show pending invites (status = 0)
            var pendingInvites = group.invites.Where(i => i.status == 0).ToList();

            if (pendingInvites.Count == 0)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "No pending invites.");
                return;
            }

            // Display invites in a table
            if (ImGui.BeginTable("##InvitesTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Invitee", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Invited By", ImGuiTableColumnFlags.WidthFixed, 150);
                ImGui.TableSetupColumn("Date", ImGuiTableColumnFlags.WidthFixed, 120);
                ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableHeadersRow();

                int inviteToCancel = -1;

                foreach (var invite in pendingInvites)
                {
                    ImGui.TableNextRow();

                    // Invitee name
                    ImGui.TableNextColumn();
                    ImGui.Text(invite.inviteeName ?? "Unknown");

                    // Inviter name
                    ImGui.TableNextColumn();
                    ImGui.Text(invite.inviterName ?? "Unknown");

                    // Date
                    ImGui.TableNextColumn();
                    if (invite.createdAt > 0)
                    {
                        var dateTime = DateTimeOffset.FromUnixTimeMilliseconds(invite.createdAt).LocalDateTime;
                        ImGui.Text(dateTime.ToString("yyyy-MM-dd HH:mm"));
                    }
                    else
                    {
                        ImGui.Text("N/A");
                    }

                    // Actions
                    ImGui.TableNextColumn();
                    if (canManageInvites)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.2f, 0.2f, 1f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.3f, 0.3f, 1f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.6f, 0.1f, 0.1f, 1f));

                        if (ImGui.Button($"Cancel##invite_{invite.inviteID}", new Vector2(-1, 0)))
                        {
                            inviteToCancel = invite.inviteID;
                        }

                        ImGui.PopStyleColor(3);
                    }
                    else
                    {
                        ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "No permission");
                    }
                }

                ImGui.EndTable();

                // Handle cancel action outside the loop
                if (inviteToCancel > 0)
                {
                    var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                        x.characterName == Plugin.plugin.playername &&
                        x.characterWorld == Plugin.plugin.playerworld);

                    if (character != null)
                    {
                        DataSender.CancelGroupInvite(character, inviteToCancel);
                        Plugin.PluginLog.Info($"Cancelling invite {inviteToCancel}");

                        // Remove from local list immediately
                        group.invites.RemoveAll(i => i.inviteID == inviteToCancel);
                    }
                }
            }
        }

        private static void DrawBansTab(Group group)
        {
            // Refresh button at the top
            if (ImGui.Button("Refresh Bans"))
            {
                DataSender.FetchGroupBans(Plugin.character, group.groupID);
            }
            ImGui.Separator();
            ImGui.Spacing();

            if (group.bans == null || group.bans.Count == 0)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "No banned members.");
                return;
            }

            // Get current user to check permissions
            var currentMember = group.members?.FirstOrDefault(m => m.userID == Plugin.plugin.Configuration.account.userID);
            bool isOwner = currentMember?.owner == true;
            bool canBan = isOwner || currentMember?.rank?.permissions?.canBan == true;

            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), $"Banned Members ({group.bans.Count})");
            ImGui.Separator();
            ImGui.Spacing();

            // Display bans in a table
            if (ImGui.BeginTable("##BansTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Lodestone", ImGuiTableColumnFlags.WidthFixed, 200);
                ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableHeadersRow();

                int banToRemove = -1;

                foreach (var ban in group.bans)
                {
                    ImGui.TableNextRow();

                    // Name
                    ImGui.TableNextColumn();
                    ImGui.Text(ban.name ?? "Unknown");

                    // Lodestone URL
                    ImGui.TableNextColumn();
                    if (!string.IsNullOrEmpty(ban.lodestoneURL))
                    {
                        // Truncate long URLs for display
                        string displayUrl = ban.lodestoneURL.Length > 30
                            ? ban.lodestoneURL.Substring(0, 27) + "..."
                            : ban.lodestoneURL;
                        ImGui.Text(displayUrl);
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(ban.lodestoneURL);
                        }
                    }
                    else
                    {
                        ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "N/A");
                    }

                    // Actions
                    ImGui.TableNextColumn();
                    if (canBan)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.2f, 1f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.7f, 0.3f, 1f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.1f, 0.5f, 0.1f, 1f));

                        if (ImGui.Button($"Unban##ban_{ban.id}", new Vector2(-1, 0)))
                        {
                            banToRemove = ban.id;
                        }

                        ImGui.PopStyleColor(3);
                    }
                    else
                    {
                        ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "No permission");
                    }
                }

                ImGui.EndTable();

                // Handle unban action outside the loop
                if (banToRemove > 0)
                {
                    var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                        x.characterName == Plugin.plugin.playername &&
                        x.characterWorld == Plugin.plugin.playerworld);

                    if (character != null)
                    {
                        DataSender.UnbanGroupMember(character, banToRemove, group.groupID);
                        Plugin.PluginLog.Info($"Unbanning ban {banToRemove} from group {group.groupID}");

                        // Remove from local list immediately
                        group.bans.RemoveAll(b => b.id == banToRemove);
                    }
                }
            }
        }

        private static void DrawJoinRequestsTab(Group group)
        {
            // Refresh button at the top
            if (ImGui.Button("Refresh Requests"))
            {
                DataSender.FetchJoinRequests(Plugin.character, group.groupID);
            }
            ImGui.Separator();
            ImGui.Spacing();

            // Get current user to check permissions
            var currentMember = group.members?.FirstOrDefault(m => m.userID == Plugin.plugin.Configuration.account.userID);
            bool isOwner = currentMember?.owner == true;
            bool canAcceptRequests = isOwner || currentMember?.rank?.permissions?.canAcceptJoinRequests == true;

            if (group.joinRequests == null || group.joinRequests.Count == 0)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "No pending join requests.");
                return;
            }

            // Filter to only show pending requests (status = 0)
            var pendingRequests = group.joinRequests.Where(r => r.status == 0).ToList();

            if (pendingRequests.Count == 0)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "No pending join requests.");
                return;
            }

            ImGui.TextColored(new Vector4(0.3f, 0.8f, 1f, 1f), $"Pending Join Requests ({pendingRequests.Count})");
            ImGui.Separator();
            ImGui.Spacing();

            // Display requests in a table
            if (ImGui.BeginTable("##JoinRequestsTable", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Requester", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthFixed, 100);
                ImGui.TableSetupColumn("Message", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Date", ImGuiTableColumnFlags.WidthFixed, 120);
                ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 150);
                ImGui.TableHeadersRow();

                int requestToAccept = -1;
                int requestToDecline = -1;

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
                    string displayMessage = request.message ?? "";
                    if (displayMessage.Length > 50)
                    {
                        displayMessage = displayMessage.Substring(0, 47) + "...";
                    }
                    ImGui.Text(displayMessage);
                    if (!string.IsNullOrEmpty(request.message) && request.message.Length > 50)
                    {
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(request.message);
                        }
                    }

                    // Date
                    ImGui.TableNextColumn();
                    if (request.createdAt > 0)
                    {
                        var dateTime = DateTimeOffset.FromUnixTimeMilliseconds(request.createdAt).LocalDateTime;
                        ImGui.Text(dateTime.ToString("yyyy-MM-dd HH:mm"));
                    }
                    else
                    {
                        ImGui.Text("N/A");
                    }

                    // Actions
                    ImGui.TableNextColumn();
                    if (canAcceptRequests)
                    {
                        // Accept button
                        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.2f, 1f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.7f, 0.3f, 1f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.1f, 0.5f, 0.1f, 1f));

                        if (ImGui.Button($"Accept##req_{request.requestID}", new Vector2(65, 0)))
                        {
                            requestToAccept = request.requestID;
                        }

                        ImGui.PopStyleColor(3);

                        ImGui.SameLine();

                        // Decline button
                        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.2f, 0.2f, 1f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.3f, 0.3f, 1f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.6f, 0.1f, 0.1f, 1f));

                        if (ImGui.Button($"Decline##req_{request.requestID}", new Vector2(65, 0)))
                        {
                            requestToDecline = request.requestID;
                        }

                        ImGui.PopStyleColor(3);
                    }
                    else
                    {
                        ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "No permission");
                    }
                }

                ImGui.EndTable();

                // Handle accept action outside the loop
                if (requestToAccept > 0)
                {
                    var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                        x.characterName == Plugin.plugin.playername &&
                        x.characterWorld == Plugin.plugin.playerworld);

                    if (character != null)
                    {
                        DataSender.RespondToJoinRequest(character, requestToAccept, group.groupID, true);
                        Plugin.PluginLog.Info($"Accepting join request {requestToAccept} for group {group.groupID}");

                        // Remove from local list immediately
                        group.joinRequests.RemoveAll(r => r.requestID == requestToAccept);
                    }
                }

                // Handle decline action outside the loop
                if (requestToDecline > 0)
                {
                    var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                        x.characterName == Plugin.plugin.playername &&
                        x.characterWorld == Plugin.plugin.playerworld);

                    if (character != null)
                    {
                        DataSender.RespondToJoinRequest(character, requestToDecline, group.groupID, false);
                        Plugin.PluginLog.Info($"Declining join request {requestToDecline} for group {group.groupID}");

                        // Remove from local list immediately
                        group.joinRequests.RemoveAll(r => r.requestID == requestToDecline);
                    }
                }
            }
        }

        private static void DrawAccessTab(Group group)
        {
            // Check if user is owner
            var currentMember = group.members?.FirstOrDefault(m => m.userID == Plugin.plugin.Configuration.account.userID);
            bool isOwner = currentMember?.owner == true;

            if (!isOwner)
            {
                ImGui.TextColored(new Vector4(1f, 0.5f, 0.5f, 1f), "Only the group owner can modify access settings.");
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Show current settings as read-only
                ImGui.Text("Current Settings:");
                ImGui.Spacing();
                ImGui.Text($"Visible: {(group.visible ? "Yes" : "No")}");
                ImGui.Text($"Open Invite: {(group.openInvite ? "Yes" : "No")}");
                return;
            }

            ImGui.Text("Group Access Settings");
            ImGui.Separator();
            ImGui.Spacing();

            // Visible checkbox
            bool visible = group.visible;
            if (ImGui.Checkbox("Visible", ref visible))
            {
                group.visible = visible;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("When enabled, this group will appear in public group searches and listings.");
            }

            ImGui.Spacing();

            // Open Invite checkbox
            bool openInvite = group.openInvite;
            if (ImGui.Checkbox("Open Invite", ref openInvite))
            {
                group.openInvite = openInvite;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("When enabled, anyone can join this group without needing an invite.");
            }

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Save button
            using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.2f, 0.5f, 0.2f, 1f)))
            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.6f, 0.3f, 1f)))
            using (ImRaii.PushColor(ImGuiCol.ButtonActive, new Vector4(0.15f, 0.4f, 0.15f, 1f)))
            {
                if (ImGui.Button("Save Access Settings", new Vector2(200, 0)))
                {
                    // Save the group settings
                    DataSender.SetGroupValues(Plugin.character, group, true, leadProfileIndex, profileIndex);
                    Plugin.PluginLog.Info($"Saved access settings for group {group.groupID}: visible={group.visible}, openInvite={group.openInvite}");
                }
            }
        }
    }
}
