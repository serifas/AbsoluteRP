using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Social.Views;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using Networking;
using System;
using System.Linq;
using System.Numerics;
using AbsoluteRP.Windows.Social.Views.Groups.GroupManager;
using AbsoluteRP.Network;
using AbsoluteRP.RsUI;

namespace AbsoluteRP.Windows.Social.Views.Groups
{
    // Dialog for inviting a player to a group - search by name and send an invitation
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
                Groups_DS.FetchGroups(character);
            }
        }

        public static void Draw()
        {
            if (!isOpen) return;

            ImGui.SetNextWindowSize(new Vector2(RsTheme.S(460f), RsTheme.S(440f)), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            // Drawn outside the themed WindowSystem pass, so apply the palette here.
            RsGlobalStyle.Push();
            try
            {
                var windowOpen = ImGui.Begin($"Invite {targetName} to Group##GroupInviteDialog", ref isOpen, ImGuiWindowFlags.NoCollapse);
                try
                {
                    if (!windowOpen) return;

                    // Get user's groups where they have invite permission (using pre-fetched canInvite flag)
                    var myGroups = GroupsData.groups.Where(g => g.canInvite).ToList();

                    if (myGroups.Count == 0)
                    {
                        GroupUi.MutedWrapped("You don't have permission to invite members to any groups, or you're not in any groups.");
                        ImGui.Spacing();

                        if (GroupUi.Ghost("Close", new Vector2(120, 0)))
                        {
                            isOpen = false;
                        }
                    }
                    else
                    {
                        ImGui.TextWrapped($"Select a group to invite {targetName}@{targetWorld}:");
                        ImGui.Dummy(new Vector2(0f, RsTheme.S(6f)));
                        GroupUi.SectionLabel("Your groups");

                        // Group selection list
                        ImGui.PushStyleColor(ImGuiCol.ChildBg, RsTheme.PanelSurface);
                        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, RsTheme.S(8f));
                        bool listOpen = ImGui.BeginChild("GroupsList", new Vector2(-1, RsTheme.S(200f)), true);
                        ImGui.PopStyleVar();
                        ImGui.PopStyleColor();
                        try
                        {
                            if (listOpen)
                            {
                                float logoSize = RsTheme.S(32f);
                                float rowH = logoSize + RsTheme.S(8f);
                                foreach (var group in myGroups)
                                {
                                    bool isSelected = selectedGroupID == group.groupID;
                                    var rowMin = ImGui.GetCursorScreenPos();

                                    if (ImGui.Selectable($"##Group{group.groupID}", isSelected, ImGuiSelectableFlags.None, new Vector2(0, rowH)))
                                    {
                                        selectedGroupID = group.groupID;
                                    }
                                    if (isSelected)
                                    {
                                        ImGui.SetItemDefaultFocus();
                                    }
                                    var rowMax = ImGui.GetItemRectMax();

                                    // Logo, name and description painted over the row
                                    var dl = ImGui.GetWindowDrawList();
                                    GroupUi.DrawLogoAt(dl, group.logo, group.name, new Vector2(rowMin.X + RsTheme.S(4f), rowMin.Y + RsTheme.S(4f)), logoSize, RsTheme.S(6f));
                                    float textX = rowMin.X + RsTheme.S(4f) + logoSize + RsTheme.S(10f);
                                    float lineH = ImGui.GetTextLineHeight();
                                    bool hasDesc = !string.IsNullOrEmpty(group.description);
                                    float textY = hasDesc ? rowMin.Y + (rowH - lineH * 2f) * 0.5f : rowMin.Y + (rowH - lineH) * 0.5f;
                                    dl.PushClipRect(new Vector2(textX, rowMin.Y), new Vector2(rowMax.X, rowMax.Y), true);
                                    dl.AddText(new Vector2(textX, textY), RsTheme.U.TextPrimary, group.name ?? "Group");
                                    if (hasDesc)
                                        dl.AddText(new Vector2(textX, textY + lineH), RsTheme.U.TextMuted, group.description.Replace('\n', ' '));
                                    dl.PopClipRect();
                                }
                            }
                        }
                        finally
                        {
                            ImGui.EndChild();
                        }

                        ImGui.Dummy(new Vector2(0f, RsTheme.S(8f)));

                        // Invite message
                        GroupUi.SectionLabel("Invite message");
                        RsElements.InputTextArea("grp_invite_msg", ref inviteMessage, 500, "Say hello...", new Vector2(ImGui.GetContentRegionAvail().X, RsTheme.S(60f)));

                        ImGui.Dummy(new Vector2(0f, RsTheme.S(8f)));
                        GroupUi.Divider();

                        // Action buttons
                        ImGui.BeginDisabled(selectedGroupID == -1);
                        if (GroupUi.Button("Send Invite", new Vector2(120, 0)))
                        {
                            SendInvite();
                        }
                        ImGui.EndDisabled();

                        ImGui.SameLine();

                        if (GroupUi.Ghost("Cancel", new Vector2(120, 0)))
                        {
                            isOpen = false;
                        }

                        // Show selected group info
                        if (selectedGroupID > 0)
                        {
                            var selectedGroup = myGroups.FirstOrDefault(g => g.groupID == selectedGroupID);
                            if (selectedGroup != null)
                            {
                                ImGui.SameLine(0f, RsTheme.S(12f));
                                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + RsTheme.S(6f));
                                GroupUi.Chip($"Selected: {selectedGroup.name}", RsTheme.AccentSuccess);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug($"Error in GroupInviteDialog: {ex.Message}");
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
                Groups_DS.SendGroupInvite(character, selectedGroupID, targetName, targetWorld, inviteMessage);

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
