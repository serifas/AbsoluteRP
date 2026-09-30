using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Networking;
using AbsoluteRP.Network;
using AbsoluteRP.RsUI;

namespace AbsoluteRP.Windows.Social.Views.Groups
{
    // Popup notification window shown when the player receives a group invitation - accept or decline
    public class GroupInviteNotification : Window, IDisposable
    {
        private static List<GroupInvite> pendingInvites = new List<GroupInvite>();
        private static GroupInvite currentInvite = null;
        private static int currentInviteIndex = 0;

        public GroupInviteNotification() : base(
            "Group Invite##GroupInviteNotification",
            ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize)
        {
            IsOpen = false;
        }

        public static void AddInvite(GroupInvite invite)
        {
            // Check if invite already exists
            if (!pendingInvites.Any(i => i.inviteID == invite.inviteID))
            {
                pendingInvites.Add(invite);
            }

            // Show the notification window
            ShowNextInvite();
        }

        public static void ShowNextInvite()
        {
            if (pendingInvites.Count == 0)
            {
                currentInvite = null;
                Plugin.groupInviteNotification.IsOpen = false;
                return;
            }

            currentInviteIndex = 0;
            currentInvite = pendingInvites[currentInviteIndex];
            Plugin.groupInviteNotification.IsOpen = true;
        }

        public static void RemoveInvite(int inviteID)
        {
            pendingInvites.RemoveAll(i => i.inviteID == inviteID);

            if (currentInvite != null && currentInvite.inviteID == inviteID)
            {
                ShowNextInvite();
            }
        }

        public static int GetPendingInviteCount()
        {
            return pendingInvites.Count;
        }

        public override void Draw()
        {
            if (currentInvite == null)
            {
                IsOpen = false;
                return;
            }

            ImGui.SetWindowSize(new Vector2(RsTheme.S(450f), 0), ImGuiCond.Always);
            float S(float v) => RsTheme.S(v);

            // Header with invite count
            if (pendingInvites.Count > 1)
            {
                GroupUi.SectionLabel($"Invite {currentInviteIndex + 1} of {pendingInvites.Count}");
            }

            // Group icon - render using RenderHtmlElements if URL is available
            float logoSize = S(96f);
            if (!string.IsNullOrEmpty(currentInvite.groupLogoUrl))
            {
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - logoSize) * 0.5f);
                // Use RenderHtmlElements to render the logo from URL
                string logoHtml = $"<img>{currentInvite.groupLogoUrl}</img>";
                Misc.RenderHtmlElements(logoHtml, false, true, false, true, new Vector2(logoSize, logoSize));
            }
            else
            {
                // Placeholder: rounded tile with the group's initial
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - logoSize) * 0.5f);
                GroupUi.Logo(null, currentInvite.groupName, logoSize, S(16f));
            }

            ImGui.Dummy(new Vector2(0f, S(6f)));

            // Group name (centered)
            var groupName = currentInvite.groupName ?? "Unknown Group";
            var nameSize = ImGui.CalcTextSize(groupName);
            ImGui.SetCursorPosX((ImGui.GetWindowWidth() - nameSize.X) * 0.5f);
            ImGui.TextUnformatted(groupName);

            var invitedBy = $"Invited by {currentInvite.inviterName ?? "Unknown"}";
            var invitedSize = ImGui.CalcTextSize(invitedBy);
            ImGui.SetCursorPosX((ImGui.GetWindowWidth() - invitedSize.X) * 0.5f);
            GroupUi.Muted(invitedBy);

            ImGui.Dummy(new Vector2(0f, S(6f)));
            GroupUi.Divider();

            // Custom message
            if (!string.IsNullOrWhiteSpace(currentInvite.message))
            {
                GroupUi.SectionLabel("Message");
                ImGui.TextWrapped(currentInvite.message);
                ImGui.Dummy(new Vector2(0f, S(6f)));
            }

            // Group description
            if (!string.IsNullOrWhiteSpace(currentInvite.groupDescription))
            {
                GroupUi.SectionLabel("About this group");
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextSecondary);
                ImGui.PushTextWrapPos(ImGui.GetWindowWidth() - S(20f));
                ImGui.TextWrapped(currentInvite.groupDescription);
                ImGui.PopTextWrapPos();
                ImGui.PopStyleColor();
                ImGui.Dummy(new Vector2(0f, S(6f)));
            }

            GroupUi.Divider();

            // Action buttons (centered)
            float buttonWidth = 100f;
            float totalWidth = S(buttonWidth) * 2 + ImGui.GetStyle().ItemSpacing.X;
            GroupUi.CenterRow(totalWidth);

            if (GroupUi.Success("Accept", new Vector2(buttonWidth, 0)))
            {
                AcceptInvite();
            }

            ImGui.SameLine();

            if (GroupUi.Danger("Decline", new Vector2(buttonWidth, 0)))
            {
                DeclineInvite();
            }

            // Navigation buttons for multiple invites
            if (pendingInvites.Count > 1)
            {
                ImGui.Dummy(new Vector2(0f, S(6f)));
                GroupUi.Divider();

                float navButtonWidth = 80f;
                float navTotalWidth = S(navButtonWidth) * 2 + ImGui.GetStyle().ItemSpacing.X;
                GroupUi.CenterRow(navTotalWidth);

                if (GroupUi.Ghost("< Previous", new Vector2(navButtonWidth, 0)))
                {
                    currentInviteIndex = (currentInviteIndex - 1 + pendingInvites.Count) % pendingInvites.Count;
                    currentInvite = pendingInvites[currentInviteIndex];
                }

                ImGui.SameLine();

                if (GroupUi.Ghost("Next >", new Vector2(navButtonWidth, 0)))
                {
                    currentInviteIndex = (currentInviteIndex + 1) % pendingInvites.Count;
                    currentInvite = pendingInvites[currentInviteIndex];
                }
            }
        }

        private void AcceptInvite()
        {
            if (currentInvite == null) return;

            var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                x.characterName == Plugin.plugin.playername &&
                x.characterWorld == Plugin.plugin.playerworld);

            if (character != null)
            {
                Groups_DS.RespondToGroupInvite(character, currentInvite.inviteID, true);
                Plugin.PluginLog.Info($"Accepted invite to group {currentInvite.groupName}");
            }

            // Remove from pending and show next
            RemoveInvite(currentInvite.inviteID);
        }

        private void DeclineInvite()
        {
            if (currentInvite == null) return;

            var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                x.characterName == Plugin.plugin.playername &&
                x.characterWorld == Plugin.plugin.playerworld);

            if (character != null)
            {
                Groups_DS.RespondToGroupInvite(character, currentInvite.inviteID, false);
                Plugin.PluginLog.Info($"Declined invite to group {currentInvite.groupName}");
            }

            // Remove from pending and show next
            RemoveInvite(currentInvite.inviteID);
        }

        public void Dispose()
        {
            pendingInvites.Clear();
        }
    }
}
