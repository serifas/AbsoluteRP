using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;
using Networking;
using OtterGui.Raii;
using OtterGui.Widgets;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows
{
    public class TargetProfileWindow : Window, IDisposable
    {
        private Plugin plugin;
        public static string loading;
        private IDalamudPluginInterface pg;
        public static float currentInd, max;
        public static bool addNotes, loadPreview = false;
        public static ProfileData profileData = new ProfileData();
        internal static string characterName;
        internal static string characterWorld;
        public static bool firstDraw = true;
        internal static bool warning;
        internal static string warningMessage;
        public static List<CustomLayout> profileLayouts = new List<CustomLayout>();
        public static CustomLayout currentLayout = new CustomLayout();
        public static bool ExistingProfile = false;
        public static byte[] backgroundBytes;
        public static IDalamudTextureWrap backgroundImage;

        public TargetProfileWindow(Plugin plugin) : base("TARGET")
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(300, 300),
                MaximumSize = new Vector2(950, 950)
            };
            this.plugin = plugin;
            pg = Plugin.PluginInterface;
        }

        private static void EnsureProfileData()
        {
            if (profileData == null)
                profileData = new ProfileData();
            if (profileData.customTabs == null)
                profileData.customTabs = new List<CustomTab>();
        }

        public override void OnOpen()
        {
            try
            {
                profileLayouts.Clear();
                currentLayout = null;
                EnsureProfileData();
            }
            catch (Exception ex)
            {
                Plugin.plugin.logger.Error("TargetProfileWindow OnOpen Error: " + ex.Message);
            }
        }

        public override void Draw()
        {
            try
            {
                EnsureProfileData();

                // Loader: Wait for all tabs and gallery images to load before drawing profile
                if (DataReceiver.loadedTargetTabsCount < DataReceiver.tabsTargetCount || DataReceiver.loadedTargetGalleryImages < DataReceiver.TargetGalleryImagesToLoad)
                {
                    if (DataReceiver.loadedTargetTabsCount < DataReceiver.tabsTargetCount)
                    {
                        Misc.StartLoader(DataReceiver.loadedTargetTabsCount, DataReceiver.tabsTargetCount, $"Loading Profile Tabs {DataReceiver.loadedTargetTabsCount + 1}", ImGui.GetWindowSize());
                    }
                    if (DataReceiver.loadedTargetGalleryImages < DataReceiver.TargetGalleryImagesToLoad)
                    {
                        Misc.StartLoader(DataReceiver.loadedTargetGalleryImages, DataReceiver.TargetGalleryImagesToLoad, $"Loading Gallery Images {DataReceiver.loadedTargetGalleryImages + 1}", ImGui.GetWindowSize());
                    }
                    return;
                }

                // Warning popup
                if (warning)
                {
                    ImGui.OpenPopup("WARNING");
                    try
                    {
                        if (ImGui.BeginPopupModal("WARNING", ref warning, ImGuiWindowFlags.AlwaysAutoResize))
                        {
                            ImGui.Text(warningMessage ?? "Warning");
                            ImGui.TextColored(new Vector4(1, 0, 0, 1), "Do you agree to view the profile anyway?");
                            if (ImGui.Button("Agree"))
                            {
                                warning = false;
                                ImGui.CloseCurrentPopup();
                            }
                            ImGui.SameLine();
                            if (ImGui.Button("Go back"))
                            {
                                IsOpen = false;
                                warning = false;
                                ImGui.CloseCurrentPopup();
                            }
                        }
                    }
                    finally
                    {
                        ImGui.EndPopup();
                    }
                }

                // No profile available
                if (!ExistingProfile)
                {
                    ImGuiHelpers.SafeTextWrapped("No Profile Data Available:\nIf this character has a profile, you can request to view it below.");
                    if (ImGui.Button("Request access"))
                    {
                        DataSender.SendProfileAccessUpdate(plugin.username, plugin.playername, plugin.playerworld, characterName, characterWorld, (int)UI.ConnectionStatus.pending);
                    }
                    return;
                }

                // First draw setup
                if (firstDraw)
                {
                    addNotes = false;
                    firstDraw = false;
                }

                // Draw background image if valid
                if (backgroundImage != null && backgroundImage.ImGuiHandle != IntPtr.Zero)
                {
                    var drawList = ImGui.GetWindowDrawList();
                    float alpha = 0.5f;
                    uint tintColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, alpha));
                    Vector2 maxWindowSize = new Vector2(950, 1200);
                    float scale = ImGui.GetIO().FontGlobalScale;
                    Vector2 scaledSize = maxWindowSize * scale;
                    Vector2 imageStartPos = ImGui.GetCursorScreenPos();
                    Vector2 imageEndPos = imageStartPos + scaledSize;
                    drawList.AddImage(backgroundImage.ImGuiHandle, imageStartPos, imageEndPos, new Vector2(0, 0), new Vector2(1, 1), tintColor);
                }

                // Draw avatar if valid
                if (profileData.avatar != null && profileData.avatar.ImGuiHandle != IntPtr.Zero)
                {
                    float centeredX = (ImGui.GetWindowSize().X - profileData.avatar.Size.X) / 2;
                    ImGui.SetCursorPosX(centeredX);
                    ImGui.Image(profileData.avatar.ImGuiHandle, profileData.avatar.Size * ImGui.GetIO().FontGlobalScale);
                }

                // Draw title if valid
                if (!string.IsNullOrEmpty(profileData.title))
                {
                    Misc.SetTitle(plugin, true, profileData.title, profileData.titleColor);
                }

                // Controls
                ImGui.Text("Controls");
                if (ImGui.Button("Notes")) { addNotes = true; }
                if (ImGui.IsItemHovered()) { ImGui.SetTooltip("Add personal notes about this profile."); }

                ImGui.SameLine();
                Misc.RenderAlignmentToRight("Report");

                if (ImGui.Button("Report"))
                {
                    ReportWindow.reportCharacterName = characterName;
                    ReportWindow.reportCharacterWorld = characterWorld;
                    plugin.OpenReportWindow();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Report this profile for inappropriate use.\n(Repeat false reports may result in your account being banned.)");
                }

                // Tabs
                if (profileData.customTabs != null && profileData.customTabs.Count > 0)
                {
                    if (ImGui.BeginTabBar("TargetNavigation"))
                    {
                        foreach (CustomTab tab in profileData.customTabs)
                        {
                            if (tab != null && !string.IsNullOrEmpty(tab.Name) && tab.Layout != null)
                            {
                                if (ImGui.BeginTabItem(tab.Name))
                                {
                                    try
                                    {
                                        switch (tab.Layout)
                                        {
                                            case BioLayout bioLayout:
                                                Bio.RenderBioPreview(bioLayout, profileData.titleColor);
                                                break;
                                            case DetailsLayout detailsLayout:
                                                Details.RenderDetailPreview(detailsLayout, profileData.titleColor);
                                                break;
                                            case GalleryLayout galleryLayout:
                                                Gallery.RenderGalleryPreview(galleryLayout, profileData.titleColor);
                                                break;
                                            case InfoLayout infoLayout:
                                                Misc.SetTitle(plugin, true, tab.Name, profileData.titleColor);
                                                ImGuiHelpers.SafeTextWrapped(infoLayout.text);
                                                break;
                                            case StoryLayout storyLayout:
                                                Story.RenderStoryPreview(storyLayout, profileData.titleColor);
                                                break;
                                            case TreeLayout treeLayout:
                                                string uniqueId = $"{tab.Name}##{treeLayout.tabIndex}";
                                                Tree.RenderTreeLayout(treeLayout.tabIndex, false, uniqueId, treeLayout, tab.Name, profileData.titleColor);
                                                break;
                                            default:
                                                break;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Plugin.plugin.logger.Error($"TargetProfileWindow Tab Render Error: {ex.Message}");
                                    }
                                    ImGui.EndTabItem();
                                }
                            }
                        }
                        ImGui.EndTabBar();
                    }
                }
                else
                {
                    ImGui.TextColored(new Vector4(1, 0, 0, 1), "No profile data available for this profile.");
                }

                // Layout selection warning
                if (currentLayout == null)
                {
                    return;
                }

                // Profile child region
                using var profileTable = ImRaii.Child("PROFILE");
                if (!profileTable)
                    return;

                // Notes and preview
                if (addNotes)
                {
                    plugin.OpenProfileNotes();
                    addNotes = false;
                }

                if (loadPreview)
                {
                    plugin.OpenImagePreview();
                    loadPreview = false;
                }
            }
            catch (Exception ex)
            {
                Plugin.plugin.logger.Error("TargetWindow Draw Error: " + ex.Message);
                loading = "An error occurred while loading the profile data.";
                currentInd = 0;
                max = 1;
            }
        }

        public static void Clear()
        {
            try
            {
                backgroundImage = null;
                backgroundBytes = null;
                if (profileData != null)
                {
                    profileData.avatar = null;
                    profileData.title = string.Empty;
                    profileData.titleColor = new Vector4(1,1,1,1);
                    profileData.customTabs?.Clear();
                }
                characterName = string.Empty;
                characterWorld = string.Empty;
                ExistingProfile = false;
                firstDraw = true;
                warning = false;
                warningMessage = string.Empty;
                profileLayouts?.Clear();                
                currentLayout = null;
            }
            catch (Exception ex)
            {
                Plugin.plugin.logger.Error("TargetProfileWindow Clear Error: " + ex.Message);
            }
        }
        public void Dispose()
        {
            try
            {
                WindowOperations.SafeDispose(backgroundImage);

                if (profileData != null)
                {
                    WindowOperations.SafeDispose(profileData.avatar);

                    if (profileData.customTabs != null)
                    {
                        foreach (var tab in profileData.customTabs)
                        {
                            switch (tab.Layout)
                            {
                                case GalleryLayout gallery:
                                    if (gallery.images != null)
                                    {
                                        foreach (var img in gallery.images)
                                        {
                                            WindowOperations.SafeDispose(img.image);
                                            WindowOperations.SafeDispose(img.thumbnail);
                                        }
                                        gallery.images.Clear();
                                    }
                                    break;

                                case InventoryLayout inventory:
                                    if (inventory.inventorySlotContents != null)
                                    {
                                        foreach (var item in inventory.inventorySlotContents.Values)
                                        {
                                            WindowOperations.SafeDispose(item.iconTexture);
                                        }
                                        inventory.inventorySlotContents.Clear();
                                    }
                                    break;
                                case BioLayout bio:
                                    if (bio.traits != null)
                                    {
                                        foreach (var trait in bio.traits)
                                        {
                                            if (trait.icon != null && trait.icon.icon != null)
                                            {
                                                WindowOperations.SafeDispose(trait.icon.icon);
                                            }
                                        }
                                        bio.traits.Clear();
                                    }
                                    bio.fields?.Clear();
                                    bio.descriptors?.Clear();
                                    break;

                                case DetailsLayout details:
                                    details.details?.Clear();
                                    break;

                                case StoryLayout story:
                                    story.chapters?.Clear();
                                    break;

                                case TreeLayout tree:
                                    tree.relationships?.Clear();
                                    tree.Paths?.Clear();
                                    tree.PathConnections?.Clear();
                                    break;

                                case InfoLayout info:
                                    info.text = string.Empty;
                                    break;
                            }
                        }
                        profileData.customTabs.Clear();
                    }
                }

                // Dispose and clear static layouts
                profileLayouts?.Clear();
                currentLayout = null;

                // Reset profile data
                profileData = new ProfileData();
                if (profileData.customTabs == null)
                    profileData.customTabs = new List<CustomTab>();
            }
            catch (Exception ex)
            {
                Plugin.plugin.logger.Error("TargetProfileWindow DisposeContent Error: " + ex.Message);
            }
        }
    }
}