using AbsoluteRP.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Networking;
using System;
using System.Linq;
using System.Numerics;
using AbsoluteRP.Network;
using AbsoluteRP.RsUI;

namespace AbsoluteRP.Windows.Social.Views.Groups
{
    /// Dialog for sending a join request to a group that requires approval.
    public static class GroupJoinRequestDialog
    {
        private static bool isOpen = false;
        private static int targetGroupID = -1;
        private static string targetGroupName = string.Empty;
        private static string targetGroupDescription = string.Empty;
        private static string requestMessage = string.Empty;

        public static void Open(int groupID, string groupName, string groupDescription)
        {
            targetGroupID = groupID;
            targetGroupName = groupName ?? "Unknown Group";
            targetGroupDescription = groupDescription ?? string.Empty;
            requestMessage = "Hi! I'd like to join your group.";
            isOpen = true;
        }

        public static void Draw()
        {
            if (!isOpen) return;

            ImGui.SetNextWindowSize(new Vector2(RsTheme.S(460f), RsTheme.S(320f)), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            // Drawn outside the themed WindowSystem pass, so apply the palette here.
            RsGlobalStyle.Push();
            try
            {
                var windowOpen = ImGui.Begin($"Request to Join Group##GroupJoinRequestDialog", ref isOpen, ImGuiWindowFlags.NoCollapse);
                try
                {
                    if (!windowOpen) return;

                    // Group info header
                    ImGui.TextUnformatted(targetGroupName);
                    if (!string.IsNullOrEmpty(targetGroupDescription))
                    {
                        GroupUi.MutedWrapped(targetGroupDescription);
                    }
                    ImGui.Dummy(new Vector2(0f, RsTheme.S(6f)));
                    GroupUi.Divider();

                    // Info text
                    ImGui.TextWrapped("This group requires approval to join. Write a message to introduce yourself to the group moderators.");
                    ImGui.Dummy(new Vector2(0f, RsTheme.S(8f)));

                    // Message input
                    GroupUi.SectionLabel("Your message");
                    RsElements.InputTextArea("grp_join_msg", ref requestMessage, 500, "Introduce yourself...", new Vector2(ImGui.GetContentRegionAvail().X, RsTheme.S(100f)));
                    GroupUi.Muted($"{requestMessage.Length}/500 characters");

                    ImGui.Dummy(new Vector2(0f, RsTheme.S(8f)));
                    GroupUi.Divider();

                    // Action buttons
                    if (GroupUi.Button("Send Request", new Vector2(120, 0)))
                    {
                        SendRequest();
                    }

                    ImGui.SameLine();

                    if (GroupUi.Ghost("Cancel", new Vector2(120, 0)))
                    {
                        isOpen = false;
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Error($"Error in GroupJoinRequestDialog: {ex.Message}");
                    ImGui.TextColored(RsTheme.AccentDanger, "An error occurred. Please try again.");
                }
                finally
                {
                    ImGui.End();
                }
            }
            finally
            {
                RsGlobalStyle.Pop();
            }
        }

        private static void SendRequest()
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

                // Send join request to server
                GroupSearch_DS.SendJoinRequest(character, targetGroupID, requestMessage);

                Plugin.PluginLog.Info($"Sent join request to group {targetGroupID} ({targetGroupName})");

                // Add to pending list so UI shows "Pending" status
                GroupsData.pendingJoinRequests.Add(targetGroupID);

                // Close dialog
                isOpen = false;
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"Error sending join request: {ex.Message}");
            }
        }
    }
}
