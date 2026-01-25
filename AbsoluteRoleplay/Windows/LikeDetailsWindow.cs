using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Networking;
using AbsoluteRP.Defines;

namespace AbsoluteRP.Windows
{
    public class LikeDetailsWindow : Window
    {
        private ProfileData currentProfile;
        private HashSet<string> expandedComments = new HashSet<string>();

        public LikeDetailsWindow() : base("Profile Likes Details##likeDetails")
        {
            Size = new Vector2(600, 700);
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        public void SetProfile(ProfileData profile)
        {
            currentProfile = profile;
            // Clear expanded state when switching profiles
            expandedComments.Clear();
        }

        public override void Draw()
        {
            if (currentProfile == null)
            {
                ImGui.TextColored(new Vector4(1f, 0.5f, 0.5f, 1f), "No profile selected");
                return;
            }

            ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), $"Likes for: {currentProfile.title}");

            // Debug: Log the number of likes received

            // Calculate total likes (sum of all like_count)
            int totalLikes = 0;
            foreach (var like in DataReceiver.currentProfileLikes)
            {
                totalLikes += like.likeCount;
            }
            ImGui.Text($"Total: {totalLikes} likes from {DataReceiver.currentProfileLikes.Count} user(s)");
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), " ♥");
            ImGui.Separator();
            ImGui.Spacing();

            if (DataReceiver.currentProfileLikes.Count == 0)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "No likes yet. Share your profile to get likes!");
                return;
            }

            ImGui.BeginChild("LikesScroll", new Vector2(0, 0), true);

            // Sort likes by most recent first (descending by likedAt timestamp)
            var sortedLikes = DataReceiver.currentProfileLikes.OrderByDescending(l => l.likedAt).ToList();

            foreach (var like in sortedLikes)
            {
                string uniqueId = $"{like.likerUserID}_{like.likedAt}";
                ImGui.PushID(uniqueId);

                bool hasComment = !string.IsNullOrEmpty(like.comment);
                bool isExpanded = expandedComments.Contains(uniqueId);

                // Draw a separator/card header background using a colored rectangle
                var drawList = ImGui.GetWindowDrawList();
                var cursorPos = ImGui.GetCursorScreenPos();
                float cardWidth = ImGui.GetContentRegionAvail().X;
                float headerHeight = ImGui.GetTextLineHeightWithSpacing() + 8;

                // Draw card background
                drawList.AddRectFilled(
                    cursorPos,
                    new Vector2(cursorPos.X + cardWidth, cursorPos.Y + headerHeight),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0.15f, 0.15f, 0.2f, 0.8f)),
                    4.0f
                );

                // Add some padding
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);

                // Header row with name, like count, heart icon, and timestamp
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), like.likerName);
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1f, 1f, 1f, 1f), $"x{like.likeCount}");
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "♥");
                ImGui.SameLine();

                // Timestamp on the right
                DateTime likedDate = DateTimeOffset.FromUnixTimeSeconds(like.likedAt).LocalDateTime;
                string timeStr = likedDate.ToString("yyyy-MM-dd HH:mm");
                float textWidth = ImGui.CalcTextSize(timeStr).X;
                ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - textWidth + 8);
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), timeStr);

                // Reset X position for next elements
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8);

                // Comment section - collapsible
                if (hasComment)
                {
                    ImGui.Spacing();

                    // Toggle button to show/hide comment
                    string buttonLabel = isExpanded ? "Hide Comment" : "View Comment";
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.25f, 0.4f, 0.6f, 0.8f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.35f, 0.5f, 0.7f, 0.9f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.2f, 0.35f, 0.55f, 1f));

                    if (ImGui.SmallButton(buttonLabel))
                    {
                        if (isExpanded)
                            expandedComments.Remove(uniqueId);
                        else
                            expandedComments.Add(uniqueId);
                    }

                    ImGui.PopStyleColor(3);

                    // Show comment if expanded
                    if (isExpanded)
                    {
                        ImGui.Spacing();
                        ImGui.Indent(10);
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1f));
                        ImGui.Text("Comment:");
                        ImGui.PopStyleColor();
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.95f, 0.95f, 0.95f, 1f));
                        ImGui.TextWrapped($"\"{like.comment}\"");
                        ImGui.PopStyleColor();
                        ImGui.Unindent(10);
                    }
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.PopID();
            }

            ImGui.EndChild();
        }
    }
}
