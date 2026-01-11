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
    internal class GroupForums
    {
        private static int selectedCategoryIndex = -1;
        private static int selectedChannelIndex = -1;
        private static bool showAddCategory = false;
        private static bool showAddChannel = false;
        private static bool showDeleteConfirmation = false;
        private static string newCategoryName = string.Empty;
        private static string newCategoryDescription = string.Empty;
        private static string newChannelName = string.Empty;
        private static string newChannelDescription = string.Empty;
        private static byte channelType = 0; // 0 = forum, 1 = thread
        private static bool isNSFW = false;
        private static int itemToDelete = -1;
        private static bool deletingCategory = false;

        // Store forum structure locally
        private static List<GroupForumCategory> forumCategories = new List<GroupForumCategory>();

        public static void LoadGroupForums(Group group)
        {
            try
            {
                ImGui.Text("Forum Categories & Channels");
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Add Category button
                if (ImGui.Button("Add Forum Category", new Vector2(150, 0)))
                {
                    showAddCategory = true;
                    newCategoryName = string.Empty;
                    newCategoryDescription = string.Empty;
                }

                ImGui.Spacing();

                // Categories list
                using (var categoriesChild = ImRaii.Child("ForumCategoriesList", new Vector2(-1, -40), true))
                {
                    if (forumCategories.Count == 0)
                    {
                        ImGui.TextDisabled("No forum categories yet. Click 'Add Forum Category' to create one.");
                    }
                    else
                    {
                        for (int catIdx = 0; catIdx < forumCategories.Count; catIdx++)
                        {
                            var category = forumCategories[catIdx];
                            ImGui.PushID($"ForumCategory{catIdx}");

                            // Category header
                            bool categoryExpanded = ImGui.CollapsingHeader($"{category.name}##ForumCategoryHeader");

                            // Delete category button (same line)
                            ImGui.SameLine();
                            ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 30);
                            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 0.6f));
                            if (ImGui.SmallButton($"X##DelForumCat{catIdx}"))
                            {
                                itemToDelete = catIdx;
                                deletingCategory = true;
                                showDeleteConfirmation = true;
                            }
                            ImGui.PopStyleColor();

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip("Delete this forum category");
                            }

                            if (categoryExpanded)
                            {
                                ImGui.Indent();

                                // Category description
                                if (!string.IsNullOrEmpty(category.description))
                                {
                                    ImGui.TextWrapped(category.description);
                                    ImGui.Spacing();
                                }

                                // Add Channel button
                                if (ImGui.Button($"Add Forum Channel##{catIdx}", new Vector2(150, 0)))
                                {
                                    selectedCategoryIndex = catIdx;
                                    showAddChannel = true;
                                    newChannelName = string.Empty;
                                    newChannelDescription = string.Empty;
                                    channelType = 0;
                                    isNSFW = false;
                                }

                                ImGui.Spacing();

                                // Channels list
                                if (category.channels == null)
                                {
                                    category.channels = new List<GroupForumChannel>();
                                }

                                if (category.channels.Count == 0)
                                {
                                    ImGui.TextDisabled("  No forum channels in this category.");
                                }
                                else
                                {
                                    for (int chIdx = 0; chIdx < category.channels.Count; chIdx++)
                                    {
                                        var channel = category.channels[chIdx];
                                        ImGui.PushID($"ForumChannel{chIdx}");

                                        // Channel icon and name
                                        string icon = channel.isLocked ? "🔒" : "📄";
                                        string nsfwTag = channel.isNSFW ? " [NSFW]" : "";
                                        ImGui.Text($"  {icon} {channel.name}{nsfwTag}");

                                        // Delete channel button
                                        ImGui.SameLine();
                                        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 50);
                                        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 0.6f));
                                        if (ImGui.SmallButton($"X##DelForumCh{chIdx}"))
                                        {
                                            selectedCategoryIndex = catIdx;
                                            itemToDelete = chIdx;
                                            deletingCategory = false;
                                            showDeleteConfirmation = true;
                                        }
                                        ImGui.PopStyleColor();

                                        if (ImGui.IsItemHovered())
                                        {
                                            ImGui.SetTooltip("Delete this forum channel");
                                        }

                                        if (!string.IsNullOrEmpty(channel.description))
                                        {
                                            ImGui.TextDisabled($"    {channel.description}");
                                        }

                                        ImGui.Spacing();
                                        ImGui.PopID();
                                    }
                                }

                                ImGui.Unindent();
                            }

                            ImGui.Spacing();
                            ImGui.PopID();
                        }
                    }
                }

                // Save button
                ImGui.Spacing();
                if (Misc.DrawCenteredButton("Save Changes"))
                {
                    SaveForumChanges(group);
                }

                // Add Category Popup
                DrawAddCategoryPopup(group);

                // Add Channel Popup
                DrawAddChannelPopup(group);

                // Delete Confirmation Popup
                DrawDeleteConfirmationPopup(group);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"Error in LoadGroupForums: {ex.Message}");
                ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "An error occurred. Please try again.");
            }
        }

        private static void DrawAddCategoryPopup(Group group)
        {
            if (showAddCategory)
            {
                ImGui.OpenPopup("Add Forum Category");
            }

            ImGui.SetNextWindowSize(new Vector2(400, 250), ImGuiCond.Appearing);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool open = true;
            if (ImGui.BeginPopupModal("Add Forum Category", ref open, ImGuiWindowFlags.NoResize))
            {
                ImGui.Text("Forum Category Name:");
                ImGui.InputText("##ForumCategoryName", ref newCategoryName, 100);

                ImGui.Spacing();

                ImGui.Text("Description (optional):");
                ImGui.InputTextMultiline("##ForumCategoryDesc", ref newCategoryDescription, 500, new Vector2(-1, 60));

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.BeginDisabled(string.IsNullOrWhiteSpace(newCategoryName));
                if (ImGui.Button("Create", new Vector2(120, 0)))
                {
                    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var newCategory = new GroupForumCategory
                    {
                        id = 0, // Server will assign ID
                        parentCategoryID = 0,
                        categoryIndex = forumCategories.Count,
                        name = newCategoryName.Trim(),
                        description = newCategoryDescription.Trim(),
                        icon = "📁",
                        collapsed = false,
                        categoryType = 0,
                        sortOrder = forumCategories.Count,
                        groupID = group.groupID,
                        createdAt = timestamp,
                        updatedAt = timestamp,
                        channels = new List<GroupForumChannel>()
                    };

                    forumCategories.Add(newCategory);
                    Plugin.PluginLog.Info($"Created new forum category: {newCategory.name}");

                    ImGui.CloseCurrentPopup();
                    showAddCategory = false;
                }
                ImGui.EndDisabled();

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                {
                    ImGui.CloseCurrentPopup();
                    showAddCategory = false;
                }

                // Handle close via X button
                if (!open)
                {
                    ImGui.CloseCurrentPopup();
                    showAddCategory = false;
                }

                ImGui.EndPopup();
            }
        }

        private static void DrawAddChannelPopup(Group group)
        {
            if (showAddChannel)
            {
                ImGui.OpenPopup("Add Forum Channel");
            }

            ImGui.SetNextWindowSize(new Vector2(400, 350), ImGuiCond.Appearing);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool open = true;
            if (ImGui.BeginPopupModal("Add Forum Channel", ref open, ImGuiWindowFlags.NoResize))
            {
                ImGui.Text("Forum Channel Name:");
                ImGui.InputText("##ForumChannelName", ref newChannelName, 100);

                ImGui.Spacing();

                ImGui.Text("Description (optional):");
                ImGui.InputTextMultiline("##ForumChannelDesc", ref newChannelDescription, 500, new Vector2(-1, 60));

                ImGui.Spacing();

                ImGui.Text("Channel Type:");
                string[] channelTypes = { "Forum", "Thread" };
                int typeIndex = channelType;
                if (ImGui.Combo("##ForumChannelType", ref typeIndex, channelTypes, channelTypes.Length))
                {
                    channelType = (byte)typeIndex;
                }

                ImGui.Spacing();

                ImGui.Checkbox("Mark as NSFW", ref isNSFW);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("NSFW content will be flagged appropriately");
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.BeginDisabled(string.IsNullOrWhiteSpace(newChannelName) || selectedCategoryIndex < 0);
                if (ImGui.Button("Create", new Vector2(120, 0)))
                {
                    if (selectedCategoryIndex >= 0 && selectedCategoryIndex < forumCategories.Count)
                    {
                        var category = forumCategories[selectedCategoryIndex];
                        if (category.channels == null)
                        {
                            category.channels = new List<GroupForumChannel>();
                        }

                        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        var newChannel = new GroupForumChannel
                        {
                            id = 0, // Server will assign ID
                            parentChannelID = 0,
                            channelIndex = category.channels.Count,
                            name = newChannelName.Trim(),
                            description = newChannelDescription.Trim(),
                            channelType = channelType,
                            isLocked = false,
                            isNSFW = isNSFW,
                            sortOrder = category.channels.Count,
                            groupID = group.groupID,
                            categoryID = category.id,
                            createdAt = timestamp,
                            updatedAt = timestamp,
                            lastMessageAt = 0,
                            subChannels = new List<GroupForumChannel>()
                        };

                        category.channels.Add(newChannel);
                        Plugin.PluginLog.Info($"Created new forum channel: {newChannel.name}");
                    }

                    ImGui.CloseCurrentPopup();
                    showAddChannel = false;
                }
                ImGui.EndDisabled();

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                {
                    ImGui.CloseCurrentPopup();
                    showAddChannel = false;
                }

                // Handle close via X button
                if (!open)
                {
                    ImGui.CloseCurrentPopup();
                    showAddChannel = false;
                }

                ImGui.EndPopup();
            }
        }

        private static void DrawDeleteConfirmationPopup(Group group)
        {
            if (showDeleteConfirmation)
            {
                ImGui.OpenPopup("Delete Confirmation##Forums");
            }

            ImGui.SetNextWindowSize(new Vector2(400, 180), ImGuiCond.Appearing);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool open = true;
            if (ImGui.BeginPopupModal("Delete Confirmation##Forums", ref open, ImGuiWindowFlags.NoResize))
            {
                if (deletingCategory)
                {
                    if (itemToDelete >= 0 && itemToDelete < forumCategories.Count)
                    {
                        var category = forumCategories[itemToDelete];
                        ImGui.TextWrapped($"Are you sure you want to delete the forum category \"{category.name}\"?");
                        ImGui.Spacing();
                        ImGui.TextColored(new Vector4(1.0f, 0.4f, 0.4f, 1.0f), "This will also delete all forum channels in this category!");
                    }
                }
                else
                {
                    if (selectedCategoryIndex >= 0 && selectedCategoryIndex < forumCategories.Count)
                    {
                        var category = forumCategories[selectedCategoryIndex];
                        if (category.channels != null && itemToDelete >= 0 && itemToDelete < category.channels.Count)
                        {
                            var channel = category.channels[itemToDelete];
                            ImGui.TextWrapped($"Are you sure you want to delete the forum channel \"{channel.name}\"?");
                            ImGui.Spacing();
                            ImGui.TextColored(new Vector4(1.0f, 0.4f, 0.4f, 1.0f), "All posts in this forum will be lost!");
                        }
                    }
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.9f, 0.1f, 0.1f, 1.0f));

                if (ImGui.Button("Delete", new Vector2(120, 0)))
                {
                    if (deletingCategory)
                    {
                        if (itemToDelete >= 0 && itemToDelete < forumCategories.Count)
                        {
                            forumCategories.RemoveAt(itemToDelete);
                            Plugin.PluginLog.Info($"Deleted forum category at index {itemToDelete}");
                        }
                    }
                    else
                    {
                        if (selectedCategoryIndex >= 0 && selectedCategoryIndex < forumCategories.Count)
                        {
                            var category = forumCategories[selectedCategoryIndex];
                            if (category.channels != null && itemToDelete >= 0 && itemToDelete < category.channels.Count)
                            {
                                category.channels.RemoveAt(itemToDelete);
                                Plugin.PluginLog.Info($"Deleted forum channel at index {itemToDelete}");
                            }
                        }
                    }

                    ImGui.CloseCurrentPopup();
                    showDeleteConfirmation = false;
                }

                ImGui.PopStyleColor(3);

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                {
                    ImGui.CloseCurrentPopup();
                    showDeleteConfirmation = false;
                }

                // Handle close via X button
                if (!open)
                {
                    ImGui.CloseCurrentPopup();
                    showDeleteConfirmation = false;
                }

                ImGui.EndPopup();
            }
        }

        private static void SaveForumChanges(Group group)
        {
            try
            {
                var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                    x.characterName == Plugin.plugin.playername &&
                    x.characterWorld == Plugin.plugin.playerworld);

                if (character != null)
                {
                    // Update sort orders
                    for (int i = 0; i < forumCategories.Count; i++)
                    {
                        forumCategories[i].sortOrder = i;
                        forumCategories[i].categoryIndex = i;
                        if (forumCategories[i].channels != null)
                        {
                            for (int j = 0; j < forumCategories[i].channels.Count; j++)
                            {
                                forumCategories[i].channels[j].sortOrder = j;
                                forumCategories[i].channels[j].channelIndex = j;
                            }
                        }
                    }

                    DataSender.SaveForumStructure(character, group.groupID, forumCategories);
                    Plugin.PluginLog.Info($"Saved forum changes for group {group.groupID}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"Error saving forum changes: {ex.Message}");
            }
        }
    }
}
