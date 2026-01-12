using AbsoluteRP.Windows.Social.Views;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using Networking;
using System;
using System.Linq;
using System.Numerics;
using AbsoluteRP.Windows.Social.Views.Groups.GroupManager;

namespace AbsoluteRP.Windows.Social.Views.Groups
{
    public static class GroupInviteDialog
    {
        private static bool isOpen = false;
        private static string targetName = string.Empty;
        private static string targetWorld = string.Empty;
        private static int selectedGroupID = -1;
        private static string inviteMessage = string.Empty;

        public static void Open(string characterName, string characterWorld)
        {
            targetName = characterName;
            targetWorld = characterWorld;
            selectedGroupID = -1;
            inviteMessage = $"You've been invited to join our group!";
            isOpen = true;

            // Always fetch groups when opening the dialog to ensure we have the latest data
            var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                x.characterName == Plugin.plugin.playername &&
                x.characterWorld == Plugin.plugin.playerworld);
            if (character != null)
            {
                DataSender.FetchGroups(character);
            }
        }

        public static void Draw()
        {
            if (!isOpen) return;

            ImGui.SetNextWindowSize(new Vector2(450, 400), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            var windowOpen = ImGui.Begin($"Invite {targetName} to Group##GroupInviteDialog", ref isOpen, ImGuiWindowFlags.NoCollapse);
            try
            {
                if (!windowOpen) return;

                // Get user's groups where they have invite permission (using pre-fetched canInvite flag)
                var myGroups = GroupsData.groups.Where(g => g.canInvite).ToList();

                if (myGroups.Count == 0)
                {
                    ImGui.TextWrapped("You don't have permission to invite members to any groups, or you're not in any groups.");
                    ImGui.Spacing();

                    if (ImGui.Button("Close", new Vector2(120, 0)))
                    {
                        isOpen = false;
                    }
                }
                else
                {
                    ImGui.TextWrapped($"Select a group to invite {targetName}@{targetWorld}:");
                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    // Group selection list
                    using (var child = ImRaii.Child("GroupsList", new Vector2(-1, 200), true))
                    {
                        foreach (var group in myGroups)
                        {
                            bool isSelected = selectedGroupID == group.groupID;

                            if (ImGui.Selectable($"{group.name}##Group{group.groupID}", isSelected, ImGuiSelectableFlags.None, new Vector2(0, 40)))
                            {
                                selectedGroupID = group.groupID;
                            }

                            if (isSelected)
                            {
                                ImGui.SetItemDefaultFocus();
                            }

                            // Show group description
                            if (!string.IsNullOrEmpty(group.description))
                            {
                                ImGui.SameLine();
                                ImGui.TextDisabled($" - {group.description}");
                            }
                        }
                    }

                    ImGui.Spacing();

                    // Invite message
                    ImGui.Text("Invite Message:");
                    ImGui.InputTextMultiline("##InviteMessage", ref inviteMessage, 500, new Vector2(-1, 60));

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    // Action buttons
                    ImGui.BeginDisabled(selectedGroupID == -1);
                    if (ImGui.Button("Send Invite", new Vector2(120, 0)))
                    {
                        SendInvite();
                    }
                    ImGui.EndDisabled();

                    ImGui.SameLine();

                    if (ImGui.Button("Cancel", new Vector2(120, 0)))
                    {
                        isOpen = false;
                    }

                    // Show selected group info
                    if (selectedGroupID > 0)
                    {
                        var selectedGroup = myGroups.FirstOrDefault(g => g.groupID == selectedGroupID);
                        if (selectedGroup != null)
                        {
                            ImGui.Spacing();
                            ImGui.TextColored(new Vector4(0.5f, 1f, 0.5f, 1f), $"Selected: {selectedGroup.name}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in GroupInviteDialog: {ex.Message}");
                ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "An error occurred. Please try again.");
            }
            finally
            {
                ImGui.End();
            }
        }

        private static void SendInvite()
        {
            try
            {
                var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                    x.characterName == Plugin.plugin.playername &&
                    x.characterWorld == Plugin.plugin.playerworld);

                if (character == null)
                {
                    Plugin.PluginLog.Warning("No active character found");
                    return;
                }

                // Send invite to server - server will look up user ID and profile ID
                DataSender.SendGroupInvite(character, selectedGroupID, targetName, targetWorld, inviteMessage);

                Plugin.PluginLog.Info($"Sent group invite to {targetName}@{targetWorld} for group {selectedGroupID}");

                // Close dialog
                isOpen = false;
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error sending group invite: {ex.Message}");
            }
        }
    }
}
