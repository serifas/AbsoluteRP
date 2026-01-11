using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AbsoluteRP.Defines;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Networking;

namespace AbsoluteRP.Windows.Social
{
    public class GroupInviteNotification : Window, IDisposable
    {
        private static List<GroupInvite> pendingInvites = new List<GroupInvite>();
        private static Dictionary<int, IDalamudTextureWrap> groupIconCache = new Dictionary<int, IDalamudTextureWrap>();
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

                // Load icon texture if available
                if (invite.groupIcon != null && invite.groupIcon.Length > 0)
                {
                    try
                    {
                        var texture = Plugin.TextureProvider.CreateFromImageAsync(invite.groupIcon).GetAwaiter().GetResult();
                        if (texture != null)
                        {
                            groupIconCache[invite.groupID] = texture;
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.PluginLog.Error($"Failed to load group icon: {ex.Message}");
                    }
                }
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

            ImGui.SetWindowSize(new Vector2(450, 0), ImGuiCond.Always);

            // Header with invite count
            if (pendingInvites.Count > 1)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), $"Invite {currentInviteIndex + 1} of {pendingInvites.Count}");
                ImGui.Separator();
            }

            // Group icon
            if (groupIconCache.ContainsKey(currentInvite.groupID))
            {
                var icon = groupIconCache[currentInvite.groupID];
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - 100) * 0.5f);
                ImGui.Image(icon.Handle, new Vector2(100, 100));
            }
            else
            {
                // Placeholder if no icon
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - 100) * 0.5f);
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.2f, 0.2f, 1f));
                ImGui.Button("##GroupIcon", new Vector2(100, 100));
                ImGui.PopStyleColor();

                // Draw group name initial
                var textSize = ImGui.CalcTextSize(currentInvite.groupName.Substring(0, 1));
                ImGui.SetCursorPos(new Vector2(
                    (ImGui.GetWindowWidth() - textSize.X) * 0.5f,
                    ImGui.GetCursorPosY() - 70 - textSize.Y * 0.5f));
                ImGui.Text(currentInvite.groupName.Substring(0, 1).ToUpper());
            }

            ImGui.Spacing();

            // Group name (centered)
            var nameSize = ImGui.CalcTextSize(currentInvite.groupName);
            ImGui.SetCursorPosX((ImGui.GetWindowWidth() - nameSize.X) * 0.5f);
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), currentInvite.groupName);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Inviter info
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Invited by:");
            ImGui.SameLine();
            ImGui.Text(currentInvite.inviterName);

            ImGui.Spacing();

            // Custom message
            if (!string.IsNullOrWhiteSpace(currentInvite.message))
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Message:");
                ImGui.TextWrapped(currentInvite.message);
                ImGui.Spacing();
            }

            // Group description
            if (!string.IsNullOrWhiteSpace(currentInvite.groupDescription))
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "About this group:");
                ImGui.PushTextWrapPos(ImGui.GetWindowWidth() - 20);
                ImGui.TextWrapped(currentInvite.groupDescription);
                ImGui.PopTextWrapPos();
                ImGui.Spacing();
            }

            ImGui.Separator();
            ImGui.Spacing();

            // Action buttons (centered)
            float buttonWidth = 100f;
            float totalWidth = buttonWidth * 2 + ImGui.GetStyle().ItemSpacing.X;
            ImGui.SetCursorPosX((ImGui.GetWindowWidth() - totalWidth) * 0.5f);

            // Accept button (green)
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.7f, 0.2f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.8f, 0.3f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.1f, 0.6f, 0.1f, 1f));

            if (ImGui.Button("Accept", new Vector2(buttonWidth, 0)))
            {
                AcceptInvite();
            }

            ImGui.PopStyleColor(3);
            ImGui.SameLine();

            // Decline button (red)
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.2f, 0.2f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.3f, 0.3f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.6f, 0.1f, 0.1f, 1f));

            if (ImGui.Button("Decline", new Vector2(buttonWidth, 0)))
            {
                DeclineInvite();
            }

            ImGui.PopStyleColor(3);

            // Navigation buttons for multiple invites
            if (pendingInvites.Count > 1)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                float navButtonWidth = 80f;
                float navTotalWidth = navButtonWidth * 2 + ImGui.GetStyle().ItemSpacing.X;
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - navTotalWidth) * 0.5f);

                if (ImGui.Button("< Previous", new Vector2(navButtonWidth, 0)))
                {
                    currentInviteIndex = (currentInviteIndex - 1 + pendingInvites.Count) % pendingInvites.Count;
                    currentInvite = pendingInvites[currentInviteIndex];
                }

                ImGui.SameLine();

                if (ImGui.Button("Next >", new Vector2(navButtonWidth, 0)))
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
                DataSender.RespondToGroupInvite(character, currentInvite.inviteID, true);
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
                DataSender.RespondToGroupInvite(character, currentInvite.inviteID, false);
                Plugin.PluginLog.Info($"Declined invite to group {currentInvite.groupName}");
            }

            // Remove from pending and show next
            RemoveInvite(currentInvite.inviteID);
        }

        public void Dispose()
        {
            // Dispose textures
            foreach (var texture in groupIconCache.Values)
            {
                texture?.Dispose();
            }
            groupIconCache.Clear();
            pendingInvites.Clear();
        }
    }
}
