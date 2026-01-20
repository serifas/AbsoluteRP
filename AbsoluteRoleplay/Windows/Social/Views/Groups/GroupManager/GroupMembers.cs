using AbsoluteRP.Windows.Listings;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Social.Views;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRP.Windows.Social.Views.Groups.GroupManager
{
    public static class GroupMembers
    {
        private static int selectedMemberForRank = -1;
        private static bool showAssignRankDialog = false;
        private static int selectedRankToAssign = -1;

        private static int selectedMemberForKick = -1;
        private static bool showKickConfirmDialog = false;

        private static int selectedMemberForBan = -1;
        private static bool showBanConfirmDialog = false;

        /// <summary>
        /// Safely renders a member avatar using defensive checks to prevent crashes from disposed textures.
        /// </summary>
        private static void RenderMemberAvatar(GroupMember member, Vector2 size)
        {
            try
            {
                // Check if member has a valid avatar texture
                if (member?.avatar != null && member.avatar.Handle != IntPtr.Zero)
                {
                    ImGui.Image(member.avatar.Handle, size);
                }
                else
                {
                    // Draw a placeholder box if no avatar available
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.2f, 0.3f, 1f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.2f, 0.2f, 0.3f, 1f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.2f, 0.2f, 0.3f, 1f));
                    ImGui.Button($"##avatar_{member?.id ?? 0}", size);
                    ImGui.PopStyleColor(3);
                }
            }
            catch (Exception ex)
            {
                // If rendering fails, show placeholder and log the error
                Plugin.PluginLog.Debug($"[GroupMembers] RenderMemberAvatar exception: {ex.Message}");
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.2f, 0.3f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.2f, 0.2f, 0.3f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.2f, 0.2f, 0.3f, 1f));
                ImGui.Button($"##avatar_{member?.id ?? 0}", size);
                ImGui.PopStyleColor(3);
            }
        }

        public static void LoadGroupMembers(Group group)
        {
            try
            {
                if (group == null)
                {
                    Plugin.PluginLog.Debug("[GroupMembers] LoadGroupMembers called with null group.");
                    return;
                }

                // IMPORTANT: ImRaii.Table returns a disposable that wraps whether the table was created.
                // We must check if the table is valid before calling table operations to prevent crashes.
                using var table = ImRaii.Table($"GroupMembers##GroupMembers{group.groupID}", 3, ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg);

                // If table creation failed, don't try to use table operations
                if (!table)
                {
                    Plugin.PluginLog.Debug("[GroupMembers] Table creation failed, skipping table rendering.");
                    // Still render dialogs even if table failed
                    RenderAssignRankDialog(group);
                    RenderKickConfirmDialog(group);
                    RenderBanConfirmDialog(group);
                    return;
                }

                ImGui.TableSetupColumn($"Member##MemberDetails{group.groupID}", ImGuiTableColumnFlags.WidthFixed, 200);
                ImGui.TableSetupColumn($"Rank##MemberRank{group.groupID}", ImGuiTableColumnFlags.WidthFixed, 150);
                ImGui.TableSetupColumn($"Controls##MemberControls{group.groupID}", ImGuiTableColumnFlags.WidthStretch);

                ImGui.TableHeadersRow();

                // Guard against null members list
                foreach (GroupMember member in group.members ?? Enumerable.Empty<GroupMember>())
                {
                    if (member == null) continue;

                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);

                    // Render avatar using centralized texture cache for safety
                    RenderMemberAvatar(member, new Vector2(100, 100));

                    ImGui.Text(member.name ?? string.Empty);

                    if (member.owner)
                    {
                        ImGui.TextColored(new Vector4(1f, 0.84f, 0f, 1f), "(Owner)");
                    }

                    ImGui.TableSetColumnIndex(1);

                    // Display all ranks (multiple ranks support)
                    if (member.ranks != null && member.ranks.Count > 0)
                    {
                        // Sort by hierarchy (highest first) and display each rank
                        var sortedRanks = member.ranks.OrderByDescending(r => r.hierarchy).ToList();
                        for (int i = 0; i < sortedRanks.Count; i++)
                        {
                            var r = sortedRanks[i];
                            if (r != null && !string.IsNullOrEmpty(r.name))
                            {
                                ImGui.Text(r.name);
                            }
                        }
                    }
                    else if (member.rank != null && !string.IsNullOrEmpty(member.rank.name))
                    {
                        // Fallback to legacy single rank
                        ImGui.Text(member.rank.name);
                    }
                    else
                    {
                        ImGui.TextDisabled("No Rank");
                    }

                    ImGui.TableSetColumnIndex(2);

                    // Member management controls
                    RenderMemberControls(group, member);
                }

                // Render dialogs
                RenderAssignRankDialog(group);
                RenderKickConfirmDialog(group);
                RenderBanConfirmDialog(group);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"[GroupMembers] LoadGroupMembers exception: {ex}");
            }
        }

        private static void RenderMemberControls(Group group, GroupMember member)
        {
            try
            {
                // Guard against null configuration
                if (Plugin.plugin?.Configuration?.characters == null || Plugin.plugin?.Configuration?.account == null)
                {
                    ImGui.TextDisabled("(Loading...)");
                    return;
                }

                var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x => x.characterName == Plugin.plugin.playername && x.characterWorld == Plugin.plugin.playerworld);
                if (character == null)
                {
                    ImGui.TextDisabled("(No character)");
                    return;
                }

                // Don't show controls for yourself or if you're not in the group
                var currentMember = group.members?.FirstOrDefault(m => m.userID == Plugin.plugin.Configuration.account.userID);
                if (currentMember == null)
                {
                    ImGui.TextDisabled("(Not a member)");
                    return;
                }

                // Can't manage yourself - allow assigning ranks to self though
                bool isSelf = member.userID == Plugin.plugin.Configuration.account.userID;
                if (isSelf)
                {
                    ImGui.TextDisabled("(You)");
                    // Still allow rank assignment for owner
                    if (currentMember.owner)
                    {
                        ImGui.SameLine();
                        if (ImGui.SmallButton($"Assign Rank##AssignRankSelf{member.id}"))
                        {
                            selectedMemberForRank = member.id;
                            selectedRankToAssign = member.rank?.id ?? -1;
                            showAssignRankDialog = true;
                        }
                    }
                    return;
                }

                // Can't manage owners (unless you're also an owner)
                if (member.owner && !currentMember.owner)
                {
                    ImGui.TextDisabled("(Owner - Cannot Manage)");
                    return;
                }

                ImGui.BeginGroup();

            // Assign Rank Button
            if (ImGui.Button($"Assign Rank##AssignRank{member.id}"))
            {
                selectedMemberForRank = member.id;
                selectedRankToAssign = member.rank?.id ?? -1;
                showAssignRankDialog = true;
            }

            ImGui.SameLine();

            // Remove Rank Button
            if (ImGui.Button($"Remove Rank##RemoveRank{member.id}"))
            {
                DataSender.RemoveMemberRank(character, member.id, group.groupID);
            }

            ImGui.SameLine();

            // Kick Button
            if (ImGui.Button($"Kick##Kick{member.id}"))
            {
                selectedMemberForKick = member.id;
                showKickConfirmDialog = true;
            }

            ImGui.SameLine();

            // Ban Button
            if (ImGui.Button($"Ban##Ban{member.id}"))
            {
                selectedMemberForBan = member.id;
                showBanConfirmDialog = true;
            }

                ImGui.EndGroup();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"[GroupMembers] RenderMemberControls exception: {ex}");
            }
        }

        private static void RenderAssignRankDialog(Group group)
        {
            if (!showAssignRankDialog) return;

            ImGui.SetNextWindowSize(new Vector2(450, 400), ImGuiCond.FirstUseEver);

            // IMPORTANT: ImGui.End() must ALWAYS be called after ImGui.Begin(), regardless of return value
            // This prevents ImGui state corruption that can cause crashes
            var windowOpen = ImGui.Begin("Manage Ranks##AssignRankDialog", ref showAssignRankDialog);
            try
            {
                if (!windowOpen) return;

                var member = group.members?.FirstOrDefault(m => m.id == selectedMemberForRank);
                if (member == null)
                {
                    ImGui.Text("Member not found");
                    return;
                }

                ImGui.Text($"Manage ranks for: {member.name}");
                ImGui.Separator();
                ImGui.Spacing();

                // Show current ranks
                if (member.ranks != null && member.ranks.Count > 0)
                {
                    ImGui.Text("Current Ranks:");
                    using (ImRaii.Child("CurrentRanks", new Vector2(-1, 80), true))
                    {
                        var sortedRanks = member.ranks.OrderByDescending(r => r.hierarchy).ToList();
                        foreach (var rank in sortedRanks)
                        {
                            if (rank != null && !string.IsNullOrEmpty(rank.name))
                            {
                                ImGui.Text($"  - {rank.name} (Hierarchy: {rank.hierarchy})");
                                ImGui.SameLine();
                                if (ImGui.SmallButton($"Remove##RemoveRank{rank.id}"))
                                {
                                    var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x => x.characterName == Plugin.plugin.playername && x.characterWorld == Plugin.plugin.playerworld);
                                    if (character != null)
                                    {
                                        DataSender.RemoveSpecificMemberRank(character, member.id, rank.id, group.groupID);
                                    }
                                }
                            }
                        }
                    }
                    ImGui.Spacing();
                }
                else
                {
                    ImGui.TextDisabled("No ranks assigned");
                    ImGui.Spacing();
                }

                ImGui.Separator();
                ImGui.Spacing();

                // Get available ranks (sorted by hierarchy)
                var allRanks = DataReceiver.ranks?.Where(r => r.groupID == group.groupID)
                                               .OrderByDescending(r => r.hierarchy)
                                               .ToList() ?? new List<GroupRank>();

                // Filter out ranks the member already has
                var memberRankIds = member.ranks?.Select(r => r.id).ToHashSet() ?? new HashSet<int>();
                var availableRanks = allRanks.Where(r => !memberRankIds.Contains(r.id)).ToList();

                if (allRanks.Count == 0)
                {
                    ImGui.Text("No ranks available. Create ranks in the Ranks tab first.");
                }
                else if (availableRanks.Count == 0)
                {
                    ImGui.TextColored(new Vector4(0.5f, 1f, 0.5f, 1f), "Member has all available ranks.");
                }
                else
                {
                    ImGui.Text("Add Rank:");
                    ImGui.Spacing();

                    using (ImRaii.Child("RanksList", new Vector2(-1, -40), true))
                    {
                        foreach (var rank in availableRanks)
                        {
                            bool isSelected = selectedRankToAssign == rank.id;

                            if (ImGui.Selectable($"{rank.name} (Hierarchy: {rank.hierarchy})##Rank{rank.id}", isSelected))
                            {
                                selectedRankToAssign = rank.id;
                            }

                            if (!string.IsNullOrEmpty(rank.description))
                            {
                                ImGui.TextDisabled($"  {rank.description}");
                            }
                        }
                    }

                    ImGui.Spacing();

                    if (ImGui.Button("Add Rank", new Vector2(120, 0)))
                    {
                        if (selectedRankToAssign > 0)
                        {
                            var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x => x.characterName == Plugin.plugin.playername && x.characterWorld == Plugin.plugin.playerworld);
                            if (character != null)
                            {
                                DataSender.AssignMemberRank(character, selectedMemberForRank, selectedRankToAssign, group.groupID);
                                selectedRankToAssign = -1; // Reset selection
                            }
                        }
                    }

                    ImGui.SameLine();

                    if (ImGui.Button("Cancel", new Vector2(120, 0)))
                    {
                        showAssignRankDialog = false;
                    }
                }
            }
            finally
            {
                ImGui.End();
            }
        }

        private static void RenderKickConfirmDialog(Group group)
        {
            if (!showKickConfirmDialog) return;

            ImGui.SetNextWindowSize(new Vector2(350, 150), ImGuiCond.FirstUseEver);

            // IMPORTANT: ImGui.End() must ALWAYS be called after ImGui.Begin(), regardless of return value
            // This prevents ImGui state corruption that can cause crashes
            var windowOpen = ImGui.Begin("Confirm Kick##KickConfirmDialog", ref showKickConfirmDialog);
            try
            {
                if (!windowOpen) return;

                var member = group.members?.FirstOrDefault(m => m.id == selectedMemberForKick);
                if (member == null)
                {
                    ImGui.Text("Member not found");
                    return;
                }

                ImGui.Text($"Are you sure you want to kick {member.name}?");
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(1f, 0.5f, 0f, 1f), "This action will remove them from the group.");
                ImGui.Spacing();

                if (ImGui.Button("Kick", new Vector2(120, 0)))
                {
                    var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x => x.characterName == Plugin.plugin.playername && x.characterWorld == Plugin.plugin.playerworld);
                    if (character != null)
                    {
                        DataSender.KickGroupMember(character, selectedMemberForKick, group.groupID);
                        showKickConfirmDialog = false;
                    }
                }

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                {
                    showKickConfirmDialog = false;
                }
            }
            finally
            {
                ImGui.End();
            }
        }

        private static void RenderBanConfirmDialog(Group group)
        {
            if (!showBanConfirmDialog) return;

            ImGui.SetNextWindowSize(new Vector2(350, 150), ImGuiCond.FirstUseEver);

            // IMPORTANT: ImGui.End() must ALWAYS be called after ImGui.Begin(), regardless of return value
            // This prevents ImGui state corruption that can cause crashes
            var windowOpen = ImGui.Begin("Confirm Ban##BanConfirmDialog", ref showBanConfirmDialog);
            try
            {
                if (!windowOpen) return;

                var member = group.members?.FirstOrDefault(m => m.id == selectedMemberForBan);
                if (member == null)
                {
                    ImGui.Text("Member not found");
                    return;
                }

                ImGui.Text($"Are you sure you want to ban {member.name}?");
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(1f, 0f, 0f, 1f), "This action will remove them and prevent them from rejoining.");
                ImGui.Spacing();

                if (ImGui.Button("Ban", new Vector2(120, 0)))
                {
                    var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x => x.characterName == Plugin.plugin.playername && x.characterWorld == Plugin.plugin.playerworld);
                    if (character != null)
                    {
                        DataSender.BanGroupMember(
                            character,
                            selectedMemberForBan,
                            member.userID,
                            member.profileID,
                            member.lodestoneURL ?? string.Empty,
                            group.groupID
                        );
                        // Refresh bans list after a short delay to allow server to process
                        Task.Run(async () =>
                        {
                            await Task.Delay(500);
                            DataSender.FetchGroupBans(character, group.groupID);
                        });
                        showBanConfirmDialog = false;
                    }
                }

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                {
                    showBanConfirmDialog = false;
                }
            }
            finally
            {
                ImGui.End();
            }
        }
    }
}
