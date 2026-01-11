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
    /// <summary>
    /// Unified UI for managing both chat channels and forums with a user-friendly interface
    /// </summary>
    internal class GroupCommunication
    {
        private enum CommunicationType
        {
            Chat,
            Forum
        }

        // UI State
        private static CommunicationType selectedType = CommunicationType.Chat;
        private static int selectedCategoryIndex = -1;
        private static int lastLoadedGroupID = -1;

        public static bool AddChatCategory { get; private set; }
        public static bool AddForumCategory { get; private set; }

        public static void LoadGroupCommunication(Group group)
        {
            try
            {
                // Note: Categories and forum structure are now fetched by GroupManager.ManageGroup()
                // when the settings window is first opened, so no need to fetch here

                ImGui.BeginGroup();

                // Header with tabs
                ImGui.Text("Communication Management");
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Quick add chat category button
                if (ImGui.Button("+ Chat Category", new Vector2(150, 30)))
                {
                    AddChatCategory = true;
                }
                if (AddChatCategory)
                {
                    GroupChannels.LoadGroupChannels(group);
                }
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Content area - only show chat channels
                DrawChatChannelsPreview(group);

                ImGui.EndGroup();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in LoadGroupCommunication: {ex.Message}");
                ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "An error occurred. Please try again.");
            }
        }

        private static void DrawChatChannelsPreview(Group group)
        {
            if (group.categories == null || group.categories.Count == 0)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "No chat categories yet.");
                ImGui.Spacing();
                ImGui.TextWrapped("Click '+ Chat Category' to create a new category with channels.");
                return;
            }

            using (var child = ImRaii.Child("ChatPreview", new Vector2(-1, -1), true))
            {
                foreach (var category in group.categories)
                {
                    ImGui.PushID($"Cat{category.id}");

                    // Category header
                    ImGui.TextColored(new Vector4(0.8f, 0.9f, 1.0f, 1.0f), $"📁 {category.name}");

                    if (!string.IsNullOrEmpty(category.description))
                    {
                        ImGui.SameLine();
                        ImGui.TextDisabled($"- {category.description}");
                    }

                    // Channels in category
                    if (category.channels != null && category.channels.Count > 0)
                    {
                        ImGui.Indent();
                        foreach (var channel in category.channels)
                        {
                            string icon = channel.channelType == 1 ? "📢" : "#";
                            ImGui.Text($"  {icon} {channel.name}");

                            if (!string.IsNullOrEmpty(channel.description))
                            {
                                ImGui.SameLine();
                                ImGui.TextDisabled($"- {channel.description}");
                            }
                        }
                        ImGui.Unindent();
                    }
                    else
                    {
                        ImGui.Indent();
                        ImGui.TextDisabled("  (No channels)");
                        ImGui.Unindent();
                    }

                    ImGui.Spacing();
                    ImGui.PopID();
                }
            }

            ImGui.Spacing();
            ImGui.TextDisabled($"Total: {group.categories.Count} categories, {group.categories.Sum(c => c.channels?.Count ?? 0)} channels");
        }

        private static void DrawForumsPreview(Group group)
        {
            // Note: Forums use a separate list, not group.categories
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "No forum categories yet.");
            ImGui.Spacing();
            ImGui.TextWrapped("Click '+ Forum Category' to create a new forum category.");
        }
    }
}
