using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Networking;
using System;
using System.Linq;
using System.Numerics;

namespace AbsoluteRP.Windows.Social.Views.Groups
{
    /// <summary>
    /// Dialog for sending a join request to a group that requires approval.
    /// </summary>
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

            ImGui.SetNextWindowSize(new Vector2(450, 300), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            var windowOpen = ImGui.Begin($"Request to Join Group##GroupJoinRequestDialog", ref isOpen, ImGuiWindowFlags.NoCollapse);
            try
            {
                if (!windowOpen) return;

                // Group info header
                ImGui.TextColored(new Vector4(0.8f, 0.8f, 1f, 1f), targetGroupName);
                if (!string.IsNullOrEmpty(targetGroupDescription))
                {
                    ImGui.TextWrapped(targetGroupDescription);
                }
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Info text
                ImGui.TextWrapped("This group requires approval to join. Write a message to introduce yourself to the group moderators.");
                ImGui.Spacing();

                // Message input
                ImGui.Text("Your Message:");
                ImGui.InputTextMultiline("##JoinRequestMessage", ref requestMessage, 500, new Vector2(-1, 100));
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), $"{requestMessage.Length}/500 characters");

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Action buttons
                if (ImGui.Button("Send Request", new Vector2(120, 0)))
                {
                    SendRequest();
                }

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                {
                    isOpen = false;
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"Error in GroupJoinRequestDialog: {ex.Message}");
                ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "An error occurred. Please try again.");
            }
            finally
            {
                ImGui.End();
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
                DataSender.SendJoinRequest(character, targetGroupID, requestMessage);

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
