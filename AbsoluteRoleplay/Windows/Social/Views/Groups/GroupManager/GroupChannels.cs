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
    internal class GroupChannels
    {
        private static int selectedCategoryIndex = -1;
        private static int selectedChannelIndex = -1;
        private static bool showAddCategory = false;
        private static bool showAddChannel = false;
        private static bool showEditChannel = false;
        private static bool showDeleteConfirmation = false;
        private static string newCategoryName = string.Empty;
        private static string newCategoryDescription = string.Empty;
        private static string newChannelName = string.Empty;
        private static string newChannelDescription = string.Empty;
        private static int channelType = 0; // 0 = text, 1 = announcement
        private static int itemToDelete = -1;
        private static bool deletingCategory = false;

        // Edit channel state
        private static string editChannelName = string.Empty;
        private static string editChannelDescription = string.Empty;
        private static int editChannelType = 0;
        private static bool editEveryoneCanView = true;
        private static bool editEveryoneCanPost = true;
        private static GroupChannel channelBeingEdited = null;

        public static void LoadGroupChannels(Group group)
        {
            try
            {
                if (group.categories == null)
                {
                    group.categories = new List<GroupCategory>();
                }

                ImGui.Text("Chat Categories & Channels");
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Add Category button
                if (ImGui.Button("Add Category", new Vector2(120, 0)))
                {
                    showAddCategory = true;
                    newCategoryName = string.Empty;
                    newCategoryDescription = string.Empty;
                }

                ImGui.Spacing();

                // Categories list
                using (var categoriesChild = ImRaii.Child("CategoriesList", new Vector2(-1, -40), true))
                {
                    if (group.categories.Count == 0)
                    {
                        ImGui.TextDisabled("No categories yet. Click 'Add Category' to create one.");
                    }
                    else
                    {
                        for (int catIdx = 0; catIdx < group.categories.Count; catIdx++)
                        {
                            var category = group.categories[catIdx];
                            ImGui.PushID($"Category{catIdx}");

                            // Category header
                            bool categoryExpanded = ImGui.CollapsingHeader($"{category.name}##CategoryHeader");

                            // Delete category button (same line)
                            ImGui.SameLine();
                            ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 30);
                            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 0.6f));
                            if (ImGui.SmallButton($"X##DelCat{catIdx}"))
                            {
                                itemToDelete = catIdx;
                                deletingCategory = true;
                                showDeleteConfirmation = true;
                            }
                            ImGui.PopStyleColor();

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip("Delete this category");
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
                                if (ImGui.Button($"Add Channel##{catIdx}", new Vector2(120, 0)))
                                {
                                    selectedCategoryIndex = catIdx;
                                    showAddChannel = true;
                                    newChannelName = string.Empty;
                                    newChannelDescription = string.Empty;
                                    channelType = 0;
                                }

                                ImGui.Spacing();

                                // Channels list
                                if (category.channels == null)
                                {
                                    category.channels = new List<GroupChannel>();
                                }

                                if (category.channels.Count == 0)
                                {
                                    ImGui.TextDisabled("  No channels in this category.");
                                }
                                else
                                {
                                    for (int chIdx = 0; chIdx < category.channels.Count; chIdx++)
                                    {
                                        var channel = category.channels[chIdx];
                                        ImGui.PushID($"Channel{chIdx}");

                                        // Channel type icon
                                        string icon = channel.channelType == 1 ? "📢" :
                                                      channel.channelType == 2 ? "📜" :
                                                      channel.channelType == 3 ? "🏷️" : "#";

                                        // Show permission indicator
                                        string permIndicator = "";
                                        if (!channel.everyoneCanView)
                                        {
                                            permIndicator = " Ø";
                                        }

                                        bool isSelected = ImGui.Selectable($"  {icon} {channel.name}{permIndicator}##ch{chIdx}", false, ImGuiSelectableFlags.AllowDoubleClick);

                                        // Right-click context menu
                                        ImGui.OpenPopupOnItemClick($"ChannelContext{catIdx}_{chIdx}", ImGuiPopupFlags.MouseButtonRight);
                                        if (ImGui.BeginPopup($"ChannelContext{catIdx}_{chIdx}"))
                                        {
                                            if (ImGui.MenuItem("Edit Channel"))
                                            {
                                                selectedCategoryIndex = catIdx;
                                                selectedChannelIndex = chIdx;
                                                channelBeingEdited = channel;
                                                editChannelName = channel.name ?? string.Empty;
                                                editChannelDescription = channel.description ?? string.Empty;
                                                editChannelType = channel.channelType;
                                                editEveryoneCanView = channel.everyoneCanView;
                                                editEveryoneCanPost = channel.everyoneCanPost;
                                                showEditChannel = true;
                                            }

                                            ImGui.Separator();

                                            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f));
                                            if (ImGui.MenuItem("Delete Channel"))
                                            {
                                                selectedCategoryIndex = catIdx;
                                                itemToDelete = chIdx;
                                                deletingCategory = false;
                                                showDeleteConfirmation = true;
                                            }
                                            ImGui.PopStyleColor();

                                            ImGui.EndPopup();
                                        }

                                        // Double-click to edit
                                        if (isSelected && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                                        {
                                            selectedCategoryIndex = catIdx;
                                            selectedChannelIndex = chIdx;
                                            channelBeingEdited = channel;
                                            editChannelName = channel.name ?? string.Empty;
                                            editChannelDescription = channel.description ?? string.Empty;
                                            editChannelType = channel.channelType;
                                            editEveryoneCanView = channel.everyoneCanView;
                                            editEveryoneCanPost = channel.everyoneCanPost;
                                            showEditChannel = true;
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
                    SaveChannelChanges(group);
                }

                // Add Category Popup
                DrawAddCategoryPopup(group);

                // Add Channel Popup
                DrawAddChannelPopup(group);

                // Edit Channel Popup
                DrawEditChannelPopup(group);

                // Delete Confirmation Popup
                DrawDeleteConfirmationPopup(group);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in LoadGroupChannels: {ex.Message}");
                ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "An error occurred. Please try again.");
            }
        }

        private static void DrawAddCategoryPopup(Group group)
        {
            if (showAddCategory)
            {
                ImGui.OpenPopup("Add Category");
            }

            ImGui.SetNextWindowSize(new Vector2(400, 250), ImGuiCond.Appearing);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool open = true;
            if (ImGui.BeginPopupModal("Add Category", ref open, ImGuiWindowFlags.NoResize))
            {
                ImGui.Text("Category Name:");
                ImGui.InputText("##CategoryName", ref newCategoryName, 100);

                ImGui.Spacing();

                ImGui.Text("Description (optional):");
                ImGui.InputTextMultiline("##CategoryDesc", ref newCategoryDescription, 500, new Vector2(-1, 60));

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.BeginDisabled(string.IsNullOrWhiteSpace(newCategoryName));
                if (ImGui.Button("Create", new Vector2(120, 0)))
                {
                    var newCategory = new GroupCategory
                    {
                        id = 0, // Server will assign ID
                        groupID = group.groupID,
                        name = newCategoryName.Trim(),
                        description = newCategoryDescription.Trim(),
                        sortOrder = group.categories.Count,
                        channels = new List<GroupChannel>(),
                        collapsed = false
                    };

                    group.categories.Add(newCategory);
                    Plugin.PluginLog.Info($"Created new category: {newCategory.name}");

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
                ImGui.OpenPopup("Add Channel");
            }

            ImGui.SetNextWindowSize(new Vector2(450, 400), ImGuiCond.Appearing);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool open = true;
            if (ImGui.BeginPopupModal("Add Channel", ref open, ImGuiWindowFlags.NoResize))
            {
                ImGui.Text("Channel Name:");
                ImGui.InputText("##ChannelName", ref newChannelName, 100);

                ImGui.Spacing();

                ImGui.Text("Description (optional):");
                ImGui.InputTextMultiline("##ChannelDesc", ref newChannelDescription, 500, new Vector2(-1, 60));

                ImGui.Spacing();

                ImGui.Text("Channel Type:");
                string[] channelTypes = { "Text Channel", "Announcement Channel", "Rules Channel", "Role Selection Channel" };
                ImGui.Combo("##ChannelType", ref channelType, channelTypes, channelTypes.Length);

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Permission settings
                ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), "Permissions");
                ImGui.Spacing();

                bool tempEveryoneCanView = true;
                bool tempEveryoneCanPost = true;

                ImGui.Checkbox("Everyone can view this channel", ref tempEveryoneCanView);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("When disabled, only members with specific permissions can see this channel");
                }

                ImGui.Checkbox("Everyone can post in this channel", ref tempEveryoneCanPost);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("When disabled, only members with specific permissions can post messages");
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.BeginDisabled(string.IsNullOrWhiteSpace(newChannelName) || selectedCategoryIndex < 0);
                if (ImGui.Button("Create", new Vector2(120, 0)))
                {
                    if (selectedCategoryIndex >= 0 && selectedCategoryIndex < group.categories.Count)
                    {
                        var category = group.categories[selectedCategoryIndex];
                        if (category.channels == null)
                        {
                            category.channels = new List<GroupChannel>();
                        }

                        var newChannel = new GroupChannel
                        {
                            id = 0, // Server will assign ID
                            index = category.channels.Count,
                            name = newChannelName.Trim(),
                            description = newChannelDescription.Trim(),
                            categoryID = category.id,
                            groupID = group.groupID,
                            channelType = channelType,
                            everyoneCanView = tempEveryoneCanView,
                            everyoneCanPost = tempEveryoneCanPost,
                            AllowedMembers = new List<GroupMember>(),
                            AllowedRanks = new List<GroupRank>(),
                            AllowedRoles = new List<GroupSelfAssignRole>(),
                            unreadCount = 0
                        };

                        category.channels.Add(newChannel);
                        Plugin.PluginLog.Info($"Created new channel: {newChannel.name}");
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

        private static void DrawEditChannelPopup(Group group)
        {
            if (showEditChannel)
            {
                ImGui.OpenPopup("Edit Channel");
            }

            ImGui.SetNextWindowSize(new Vector2(500, 500), ImGuiCond.Appearing);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool open = true;
            if (ImGui.BeginPopupModal("Edit Channel", ref open, ImGuiWindowFlags.NoResize))
            {
                if (channelBeingEdited == null)
                {
                    ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "Error: No channel selected");
                    if (ImGui.Button("Close"))
                    {
                        ImGui.CloseCurrentPopup();
                        showEditChannel = false;
                    }
                    ImGui.EndPopup();
                    return;
                }

                ImGui.Text("Channel Name:");
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("##EditChannelName", ref editChannelName, 100);

                ImGui.Spacing();

                ImGui.Text("Description (optional):");
                ImGui.InputTextMultiline("##EditChannelDesc", ref editChannelDescription, 500, new Vector2(-1, 60));

                ImGui.Spacing();

                ImGui.Text("Channel Type:");
                string[] channelTypes = { "Text Channel", "Announcement Channel", "Rules Channel", "Role Selection Channel" };
                ImGui.SetNextItemWidth(-1);
                ImGui.Combo("##EditChannelType", ref editChannelType, channelTypes, channelTypes.Length);

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Permission settings
                ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), "Permissions");
                ImGui.Spacing();

                ImGui.Checkbox("Everyone can view this channel", ref editEveryoneCanView);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("When disabled, only members with specific permissions can see this channel.\nMembers must also agree to group rules (if set) to view channels.");
                }

                ImGui.Checkbox("Everyone can post in this channel", ref editEveryoneCanPost);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("When disabled, only members with specific permissions can post messages");
                }

                ImGui.Spacing();

                // Show current allowed members/ranks/roles if permissions are restricted
                if (!editEveryoneCanView || !editEveryoneCanPost)
                {
                    ImGui.Separator();
                    ImGui.Spacing();
                    ImGui.TextColored(new Vector4(1f, 0.9f, 0.5f, 1.0f), "Specific Permissions");
                    ImGui.TextWrapped("Members, ranks, and roles with access to this channel can be managed after saving.");
                    ImGui.Spacing();

                    // Show current allowed ranks
                    if (channelBeingEdited.AllowedRanks != null && channelBeingEdited.AllowedRanks.Count > 0)
                    {
                        ImGui.Text("Allowed Ranks:");
                        foreach (var rank in channelBeingEdited.AllowedRanks)
                        {
                            ImGui.BulletText(rank.name ?? "Unknown Rank");
                        }
                    }

                    // Show current allowed roles
                    if (channelBeingEdited.AllowedRoles != null && channelBeingEdited.AllowedRoles.Count > 0)
                    {
                        ImGui.Text("Allowed Roles:");
                        foreach (var role in channelBeingEdited.AllowedRoles)
                        {
                            ImGui.BulletText(role.name ?? "Unknown Role");
                        }
                    }
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Save button
                ImGui.BeginDisabled(string.IsNullOrWhiteSpace(editChannelName));
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.7f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.1f, 0.5f, 0.1f, 1.0f));

                if (ImGui.Button("Save Changes", new Vector2(120, 0)))
                {
                    // Apply changes to the channel
                    channelBeingEdited.name = editChannelName.Trim();
                    channelBeingEdited.description = editChannelDescription.Trim();
                    channelBeingEdited.channelType = editChannelType;
                    channelBeingEdited.everyoneCanView = editEveryoneCanView;
                    channelBeingEdited.everyoneCanPost = editEveryoneCanPost;

                    Plugin.PluginLog.Info($"Updated channel: {channelBeingEdited.name}");

                    ImGui.CloseCurrentPopup();
                    showEditChannel = false;
                    channelBeingEdited = null;
                }

                ImGui.PopStyleColor(3);
                ImGui.EndDisabled();

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                {
                    ImGui.CloseCurrentPopup();
                    showEditChannel = false;
                    channelBeingEdited = null;
                }

                // Handle close via X button
                if (!open)
                {
                    ImGui.CloseCurrentPopup();
                    showEditChannel = false;
                    channelBeingEdited = null;
                }

                ImGui.EndPopup();
            }
        }

        private static void DrawDeleteConfirmationPopup(Group group)
        {
            if (showDeleteConfirmation)
            {
                ImGui.OpenPopup("Delete Confirmation##Channels");
            }

            ImGui.SetNextWindowSize(new Vector2(400, 180), ImGuiCond.Appearing);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool open = true;
            if (ImGui.BeginPopupModal("Delete Confirmation##Channels", ref open, ImGuiWindowFlags.NoResize))
            {
                if (deletingCategory)
                {
                    if (itemToDelete >= 0 && itemToDelete < group.categories.Count)
                    {
                        var category = group.categories[itemToDelete];
                        ImGui.TextWrapped($"Are you sure you want to delete the category \"{category.name}\"?");
                        ImGui.Spacing();
                        ImGui.TextColored(new Vector4(1.0f, 0.4f, 0.4f, 1.0f), "This will also delete all channels in this category!");
                    }
                }
                else
                {
                    if (selectedCategoryIndex >= 0 && selectedCategoryIndex < group.categories.Count)
                    {
                        var category = group.categories[selectedCategoryIndex];
                        if (category.channels != null && itemToDelete >= 0 && itemToDelete < category.channels.Count)
                        {
                            var channel = category.channels[itemToDelete];
                            ImGui.TextWrapped($"Are you sure you want to delete the channel \"{channel.name}\"?");
                            ImGui.Spacing();
                            ImGui.TextColored(new Vector4(1.0f, 0.4f, 0.4f, 1.0f), "All messages in this channel will be lost!");
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
                        if (itemToDelete >= 0 && itemToDelete < group.categories.Count)
                        {
                            group.categories.RemoveAt(itemToDelete);
                            Plugin.PluginLog.Info($"Deleted category at index {itemToDelete}");
                        }
                    }
                    else
                    {
                        if (selectedCategoryIndex >= 0 && selectedCategoryIndex < group.categories.Count)
                        {
                            var category = group.categories[selectedCategoryIndex];
                            if (category.channels != null && itemToDelete >= 0 && itemToDelete < category.channels.Count)
                            {
                                category.channels.RemoveAt(itemToDelete);
                                Plugin.PluginLog.Info($"Deleted channel at index {itemToDelete}");
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

        private static void SaveChannelChanges(Group group)
        {
            try
            {
                var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                    x.characterName == Plugin.plugin.playername &&
                    x.characterWorld == Plugin.plugin.playerworld);

                if (character != null)
                {
                    // Update sort orders
                    for (int i = 0; i < group.categories.Count; i++)
                    {
                        group.categories[i].sortOrder = i;
                        if (group.categories[i].channels != null)
                        {
                            for (int j = 0; j < group.categories[i].channels.Count; j++)
                            {
                                group.categories[i].channels[j].index = j;
                                group.categories[i].channels[j].groupID = group.groupID;
                            }
                        }
                    }

                    DataSender.SaveGroupCategories(character, group.groupID, group.categories);
                    Plugin.PluginLog.Info($"Saved channel changes for group {group.groupID}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error saving channel changes: {ex.Message}");
            }
        }
    }
}
