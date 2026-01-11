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
        private static bool showWizard = false;
        private static int wizardStep = 0;
        private static int lastLoadedGroupID = -1;

        // Wizard state
        private static CommunicationType wizardType = CommunicationType.Chat;
        private static string wizardCategoryName = string.Empty;
        private static string wizardCategoryDescription = string.Empty;
        private static List<WizardChannel> wizardChannels = new List<WizardChannel>();

        public static bool AddChatCategory { get; private set; }
        public static bool AddForumCategory { get; private set; }

        private class WizardChannel
        {
            public string name = string.Empty;
            public string description = string.Empty;
            public int type = 0; // Chat: 0=text, 1=announcement | Forum: 0=forum, 1=thread
            public bool isNSFW = false; // Forum only
        }

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

                // Type selector tabs
                if (ImGui.RadioButton("Chat Channels", selectedType == CommunicationType.Chat))
                {
                    selectedType = CommunicationType.Chat;
                }

                ImGui.SameLine();

                if (ImGui.RadioButton("Forums", selectedType == CommunicationType.Forum))
                {
                    selectedType = CommunicationType.Forum;
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Quick setup wizard button
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.8f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.7f, 0.9f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.1f, 0.5f, 0.7f, 1.0f));

                if (ImGui.Button("🎯 Quick Setup Wizard", new Vector2(180, 30)))
                {
                    StartWizard();
                }

                ImGui.PopStyleColor(3);

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Step-by-step wizard to create a category with multiple channels");
                }

                ImGui.SameLine();

                // Quick add buttons
                if (selectedType == CommunicationType.Chat)
                {
                    if (ImGui.Button("+ Chat Category", new Vector2(150, 30)))
                    {
                        AddChatCategory = true;
                    }
                }
                else
                {
                    if (ImGui.Button("+ Forum Category", new Vector2(150, 30)))
                    {
                        AddForumCategory = true;
                    }
                }
                if (AddChatCategory)
                {
                    GroupChannels.LoadGroupChannels(group);
                }
                if (AddForumCategory)
                {
                    GroupForums.LoadGroupForums(group);
                }
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Content area
                if (selectedType == CommunicationType.Chat)
                {
                    DrawChatChannelsPreview(group);
                }
                else
                {
                    DrawForumsPreview(group);
                }

                ImGui.EndGroup();

                // Wizard
                DrawSetupWizard(group);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"Error in LoadGroupCommunication: {ex.Message}");
                ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "An error occurred. Please try again.");
            }
        }

        private static void DrawChatChannelsPreview(Group group)
        {
            if (group.categories == null || group.categories.Count == 0)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "No chat categories yet.");
                ImGui.Spacing();
                ImGui.TextWrapped("💡 Tip: Use the Quick Setup Wizard to create a category with common channels like #general, #announcements, and #events!");
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
            ImGui.TextWrapped("💡 Tip: Use the Quick Setup Wizard to create forum categories for roleplay, events, and general discussion!");
        }

        private static void StartWizard()
        {
            showWizard = true;
            wizardStep = 0;
            wizardType = selectedType;
            wizardCategoryName = string.Empty;
            wizardCategoryDescription = string.Empty;
            wizardChannels = new List<WizardChannel>();
        }

        private static void DrawSetupWizard(Group group)
        {
            if (showWizard)
            {
                ImGui.OpenPopup("Quick Setup Wizard");
                showWizard = false;
            }

            ImGui.SetNextWindowSize(new Vector2(600, 500), ImGuiCond.Appearing);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool open = true;
            if (ImGui.BeginPopupModal("Quick Setup Wizard", ref open, ImGuiWindowFlags.NoResize))
            {
                // Progress bar
                float progress = (wizardStep + 1) / 4.0f;
                ImGui.ProgressBar(progress, new Vector2(-1, 0), $"Step {wizardStep + 1} of 4");
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Step content
                switch (wizardStep)
                {
                    case 0:
                        DrawWizardStep1_CategoryName();
                        break;
                    case 1:
                        DrawWizardStep2_PresetOrCustom();
                        break;
                    case 2:
                        DrawWizardStep3_ChannelList();
                        break;
                    case 3:
                        DrawWizardStep4_Review(group);
                        break;
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Navigation buttons
                if (wizardStep > 0)
                {
                    if (ImGui.Button("← Back", new Vector2(100, 0)))
                    {
                        wizardStep--;
                    }
                    ImGui.SameLine();
                }

                ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 230);

                if (wizardStep < 3)
                {
                    bool canProceed = CanProceedToNextStep();
                    ImGui.BeginDisabled(!canProceed);

                    if (ImGui.Button("Next →", new Vector2(100, 0)))
                    {
                        wizardStep++;
                    }

                    ImGui.EndDisabled();
                }
                else
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.8f, 0.2f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.9f, 0.3f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.1f, 0.7f, 0.1f, 1.0f));

                    if (ImGui.Button("✓ Create", new Vector2(100, 0)))
                    {
                        CreateFromWizard(group);
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.PopStyleColor(3);
                }

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(100, 0)))
                {
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }

        private static void DrawWizardStep1_CategoryName()
        {
            ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), "Step 1: Category Information");
            ImGui.Spacing();

            ImGui.Text($"Creating: {(wizardType == CommunicationType.Chat ? "Chat Category" : "Forum Category")}");
            ImGui.Spacing();

            ImGui.Text("Category Name:");
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##WizCatName", ref wizardCategoryName, 100);

            ImGui.Spacing();

            ImGui.Text("Description (optional):");
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextMultiline("##WizCatDesc", ref wizardCategoryDescription, 500, new Vector2(-1, 100));

            ImGui.Spacing();
            ImGui.TextWrapped("💡 Tip: Choose a descriptive name like \"General\", \"Roleplay\", or \"Events\"");
        }

        private static void DrawWizardStep2_PresetOrCustom()
        {
            ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), "Step 2: Choose Channels");
            ImGui.Spacing();

            ImGui.TextWrapped("Would you like to use a preset or create custom channels?");
            ImGui.Spacing();

            if (wizardType == CommunicationType.Chat)
            {
                if (ImGui.Button("📋 Preset: General Channels", new Vector2(-1, 50)))
                {
                    wizardChannels = new List<WizardChannel>
                    {
                        new WizardChannel { name = "general", description = "General discussion", type = 0 },
                        new WizardChannel { name = "announcements", description = "Important updates", type = 1 },
                        new WizardChannel { name = "events", description = "Event planning and coordination", type = 0 }
                    };
                    wizardStep++;
                }

                if (ImGui.Button("🎭 Preset: Roleplay Channels", new Vector2(-1, 50)))
                {
                    wizardChannels = new List<WizardChannel>
                    {
                        new WizardChannel { name = "in-character", description = "IC roleplay chat", type = 0 },
                        new WizardChannel { name = "out-of-character", description = "OOC discussion", type = 0 },
                        new WizardChannel { name = "plotting", description = "Story planning", type = 0 },
                        new WizardChannel { name = "announcements", description = "RP announcements", type = 1 }
                    };
                    wizardStep++;
                }

                if (ImGui.Button("🎪 Preset: Event Channels", new Vector2(-1, 50)))
                {
                    wizardChannels = new List<WizardChannel>
                    {
                        new WizardChannel { name = "event-chat", description = "Event discussion", type = 0 },
                        new WizardChannel { name = "scheduling", description = "Event scheduling", type = 0 },
                        new WizardChannel { name = "announcements", description = "Event announcements", type = 1 }
                    };
                    wizardStep++;
                }
            }
            else
            {
                if (ImGui.Button("📋 Preset: General Forums", new Vector2(-1, 50)))
                {
                    wizardChannels = new List<WizardChannel>
                    {
                        new WizardChannel { name = "General Discussion", description = "General topics", type = 0 },
                        new WizardChannel { name = "Announcements", description = "Important news", type = 0 },
                        new WizardChannel { name = "Questions & Help", description = "Ask questions", type = 0 }
                    };
                    wizardStep++;
                }

                if (ImGui.Button("🎭 Preset: Roleplay Forums", new Vector2(-1, 50)))
                {
                    wizardChannels = new List<WizardChannel>
                    {
                        new WizardChannel { name = "Character Profiles", description = "Character bios", type = 0 },
                        new WizardChannel { name = "Story Threads", description = "Ongoing stories", type = 0 },
                        new WizardChannel { name = "Plot Discussion", description = "Story planning", type = 0 },
                        new WizardChannel { name = "Archives", description = "Completed stories", type = 0 }
                    };
                    wizardStep++;
                }
            }

            ImGui.Spacing();

            if (ImGui.Button("✏️ Custom Channels", new Vector2(-1, 50)))
            {
                wizardChannels = new List<WizardChannel> { new WizardChannel() };
                wizardStep++;
            }

            ImGui.Spacing();
            ImGui.TextWrapped("💡 Tip: You can edit these channels in the next step!");
        }

        private static void DrawWizardStep3_ChannelList()
        {
            ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), "Step 3: Customize Channels");
            ImGui.Spacing();

            using (var child = ImRaii.Child("ChannelEdit", new Vector2(-1, 250), true))
            {
                for (int i = 0; i < wizardChannels.Count; i++)
                {
                    ImGui.PushID($"WizCh{i}");
                    var channel = wizardChannels[i];

                    ImGui.Separator();
                    ImGui.Text($"Channel {i + 1}");

                    ImGui.SetNextItemWidth(200);
                    ImGui.InputText("##Name", ref channel.name, 100);

                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(300);
                    ImGui.InputText("##Desc", ref channel.description, 200);

                    ImGui.SameLine();

                    if (wizardType == CommunicationType.Chat)
                    {
                        string[] types = { "Text", "Announcement" };
                        ImGui.SetNextItemWidth(120);
                        ImGui.Combo("##Type", ref channel.type, types, types.Length);
                    }
                    else
                    {
                        ImGui.Checkbox("NSFW", ref channel.isNSFW);
                    }

                    ImGui.SameLine();

                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 0.6f));
                    if (ImGui.SmallButton("X"))
                    {
                        wizardChannels.RemoveAt(i);
                        i--;
                    }
                    ImGui.PopStyleColor();

                    ImGui.PopID();
                }
            }

            ImGui.Spacing();

            if (ImGui.Button("+ Add Another Channel", new Vector2(-1, 0)))
            {
                wizardChannels.Add(new WizardChannel());
            }

            ImGui.Spacing();
            ImGui.TextWrapped($"💡 Tip: {wizardChannels.Count} channel(s) will be created in this category");
        }

        private static void DrawWizardStep4_Review(Group group)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), "Step 4: Review & Create");
            ImGui.Spacing();

            ImGui.Text("Category Name:");
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.8f, 1.0f, 0.8f, 1.0f), wizardCategoryName);

            if (!string.IsNullOrEmpty(wizardCategoryDescription))
            {
                ImGui.Text("Description:");
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.8f, 1.0f, 0.8f, 1.0f), wizardCategoryDescription);
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text($"Channels ({wizardChannels.Count}):");
            ImGui.Spacing();

            using (var child = ImRaii.Child("ReviewList", new Vector2(-1, 250), true))
            {
                foreach (var channel in wizardChannels.Where(c => !string.IsNullOrEmpty(c.name)))
                {
                    if (wizardType == CommunicationType.Chat)
                    {
                        string icon = channel.type == 1 ? "📢" : "#";
                        ImGui.Text($"{icon} {channel.name}");
                    }
                    else
                    {
                        string nsfwTag = channel.isNSFW ? " [NSFW]" : "";
                        ImGui.Text($"📄 {channel.name}{nsfwTag}");
                    }

                    if (!string.IsNullOrEmpty(channel.description))
                    {
                        ImGui.SameLine();
                        ImGui.TextDisabled($"- {channel.description}");
                    }
                }
            }

            ImGui.Spacing();
            ImGui.TextWrapped("✓ Ready to create! Click 'Create' to add this category with all channels to your group.");
        }

        private static bool CanProceedToNextStep()
        {
            switch (wizardStep)
            {
                case 0:
                    return !string.IsNullOrWhiteSpace(wizardCategoryName);
                case 1:
                    return true; // Handled by buttons
                case 2:
                    return wizardChannels.Any(c => !string.IsNullOrWhiteSpace(c.name));
                case 3:
                    return true;
                default:
                    return false;
            }
        }

        private static void CreateFromWizard(Group group)
        {
            try
            {
                var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                    x.characterName == Plugin.plugin.playername &&
                    x.characterWorld == Plugin.plugin.playerworld);

                if (character == null)
                {
                    Plugin.PluginLog.Error("No character found for wizard creation");
                    return;
                }

                if (wizardType == CommunicationType.Chat)
                {
                    // Create chat category
                    if (group.categories == null)
                        group.categories = new List<GroupCategory>();

                    var category = new GroupCategory
                    {
                        id = 0,
                        groupID = group.groupID,
                        name = wizardCategoryName.Trim(),
                        description = wizardCategoryDescription.Trim(),
                        sortOrder = group.categories.Count,
                        channels = new List<GroupChannel>(),
                        collapsed = false
                    };

                    // Add channels
                    foreach (var wizCh in wizardChannels.Where(c => !string.IsNullOrWhiteSpace(c.name)))
                    {
                        category.channels.Add(new GroupChannel
                        {
                            id = 0,
                            index = category.channels.Count,
                            name = wizCh.name.Trim(),
                            description = wizCh.description.Trim(),
                            categoryID = 0,
                            channelType = wizCh.type,
                            AllowedMembers = new List<GroupMember>(),
                            AllowedRanks = new List<GroupRank>(),
                            unreadCount = 0
                        });
                    }

                    group.categories.Add(category);
                    DataSender.SaveGroupCategories(character, group.groupID, group.categories);
                    Plugin.PluginLog.Info($"Created chat category '{category.name}' with {category.channels.Count} channels via wizard");
                }
                else
                {
                    // Create forum category
                    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var forumCategories = new List<GroupForumCategory>();

                    var category = new GroupForumCategory
                    {
                        id = 0,
                        parentCategoryID = 0,
                        categoryIndex = 0,
                        name = wizardCategoryName.Trim(),
                        description = wizardCategoryDescription.Trim(),
                        icon = "📁",
                        collapsed = false,
                        categoryType = 0,
                        sortOrder = 0,
                        groupID = group.groupID,
                        createdAt = timestamp,
                        updatedAt = timestamp,
                        channels = new List<GroupForumChannel>()
                    };

                    // Add channels
                    foreach (var wizCh in wizardChannels.Where(c => !string.IsNullOrWhiteSpace(c.name)))
                    {
                        category.channels.Add(new GroupForumChannel
                        {
                            id = 0,
                            parentChannelID = 0,
                            channelIndex = category.channels.Count,
                            name = wizCh.name.Trim(),
                            description = wizCh.description.Trim(),
                            channelType = (byte)wizCh.type,
                            isLocked = false,
                            isNSFW = wizCh.isNSFW,
                            sortOrder = category.channels.Count,
                            groupID = group.groupID,
                            categoryID = 0,
                            createdAt = timestamp,
                            updatedAt = timestamp,
                            lastMessageAt = 0,
                            subChannels = new List<GroupForumChannel>()
                        });
                    }

                    forumCategories.Add(category);
                    DataSender.SaveForumStructure(character, group.groupID, forumCategories);
                    Plugin.PluginLog.Info($"Created forum category '{category.name}' with {category.channels.Count} channels via wizard");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"Error creating from wizard: {ex.Message}");
            }
        }
    }
}
