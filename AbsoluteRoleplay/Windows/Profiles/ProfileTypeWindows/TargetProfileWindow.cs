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
        public static CustomLayout currentLayout;
        public static bool ExistingProfile = false;

        public TargetProfileWindow(Plugin plugin) : base("TARGET")
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(300, 300),
                MaximumSize = new Vector2(600, 950)
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


                if (profileData.background == null || profileData.background.ImGuiHandle == IntPtr.Zero)
                {
                    profileData.background = UI.UICommonImage(UI.CommonImageTypes.backgroundHolder);
                    Plugin.plugin.logger.Debug("[TargetProfileWindow] Set background to default backgroundHolder.");
                }

                if (profileData.avatar == null || profileData.avatar.ImGuiHandle == IntPtr.Zero)
                {
                    profileData.avatar = UI.UICommonImage(UI.CommonImageTypes.avatarHolder);
                    Plugin.plugin.logger.Debug("[TargetProfileWindow] Set avatar to default avatarHolder.");
                }

                EnsureProfileData();

                // Loader: Wait for all tabs and gallery images to load before drawing profile
           
                bool tabsLoading = DataReceiver.loadedTargetTabsCount < DataReceiver.tabsTargetCount;
                bool galleryLoading = DataReceiver.loadedTargetGalleryImages < DataReceiver.TargetGalleryImagesToLoad;

                if (tabsLoading)
                {
                    Misc.StartLoader(DataReceiver.loadedTargetTabsCount, DataReceiver.tabsTargetCount, $"Loading Profile Tabs {DataReceiver.loadedTabsCount + 1}", ImGui.GetWindowSize(), "tabs");

                    if (galleryLoading)
                    {
                        Misc.StartLoader(DataReceiver.loadedTargetGalleryImages, DataReceiver.TargetGalleryImagesToLoad, $"Loading Gallery Images {DataReceiver.loadedGalleryImages + 1}", ImGui.GetWindowSize(), "gallery");     
                    }
                    return;
                }
                // Block further UI until all tweens are finished
                if ((tabsLoading && Misc.IsLoaderTweening("tabs")) ||
                    (galleryLoading && Misc.IsLoaderTweening("gallery")))
                {
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
                    catch (Exception ex)
                    {
                        Plugin.plugin.logger.Error($"[TargetProfileWindow] Warning popup error: {ex.Message}");
                    }
                    finally
                    {
                        ImGui.EndPopup();
                    }
                }

                // No profile available
                if (!ExistingProfile)
                {
                    Plugin.plugin.logger.Debug("[TargetProfileWindow] No profile data available.");
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
                if (profileData.background == null || profileData.background.ImGuiHandle == IntPtr.Zero)
                {
                    Plugin.plugin.logger.Debug("[TargetProfileWindow] Background image is null or handle is zero.");
                }
                else
                {
                    try
                    {
                        var drawList = ImGui.GetWindowDrawList();
                        float alpha = 0.5f;
                        uint tintColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, alpha));
                        float scale = ImGui.GetIO().FontGlobalScale;
                        Vector2 scaledSize = profileData.background.Size * scale;
                        Vector2 imageStartPos = ImGui.GetCursorScreenPos();
                        Vector2 imageEndPos = imageStartPos + scaledSize;
                        drawList.AddImage(profileData.background.ImGuiHandle, imageStartPos, imageEndPos, new Vector2(0, 0), new Vector2(1, 1), tintColor);
                        // Draw dark overlay over the background image
                        uint overlayColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.5f)); // 0.5f = 50% opacity
                        drawList.AddRectFilled(imageStartPos, imageEndPos, overlayColor);
                    }
                    catch (Exception ex)
                    {
                        Plugin.plugin.logger.Error($"[TargetProfileWindow] Failed to draw background image: {ex.Message}");
                    }
                }

                // Draw avatar if valid
                if (profileData.avatar != null && profileData.avatar.ImGuiHandle != IntPtr.Zero)
                {
                    try
                    {
                        Vector2 avatarSize = profileData.avatar.Size * ImGui.GetIO().FontGlobalScale;
                        float centeredX = (ImGui.GetContentRegionAvail().X - avatarSize.X) / 2;
                        var avatarBtnSize = ImGui.CalcTextSize("Edit Avatar") + new Vector2(10, 10);
                        float avatarXPos = (ImGui.GetWindowSize().X - avatarBtnSize.X) / 2;
                        ImGui.SetCursorPosX(centeredX);
                        if (profileData.avatar != null && profileData.avatar.ImGuiHandle != IntPtr.Zero)
                        {
                            ImGui.Image(profileData.avatar.ImGuiHandle, avatarSize);
                        }
                        Plugin.plugin.logger.Debug("[TargetProfileWindow] Drew avatar image.");
                    }
                    catch (Exception ex)
                    {
                        Plugin.plugin.logger.Error($"[TargetProfileWindow] Failed to draw avatar image: {ex.Message}");
                    }
                }
                else
                {
                    Plugin.plugin.logger.Debug("[TargetProfileWindow] Avatar image is null or handle is zero.");
                }

                // Draw title if valid
                if (!string.IsNullOrEmpty(profileData.title))
                {
                    try
                    {
                        Misc.SetTitle(plugin, true, profileData.title, profileData.titleColor);
                    }
                    catch (Exception ex)
                    {
                        Plugin.plugin.logger.Error($"[TargetProfileWindow] Failed to set title: {ex.Message}");
                    }
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
                        foreach(CustomTab tab in profileData.customTabs.ToList())
                        {
                            if (tab != null && !string.IsNullOrEmpty(tab.Name) && tab.Layout != null)
                            {
                                if (ImGui.BeginTabItem(tab.Name))
                                {
                                    currentLayout = tab.Layout as CustomLayout;
                                    try
                                    {
                                        Plugin.plugin.logger.Debug($"[TargetProfileWindow] Rendering tab: {tab.Name} ({tab.Layout.GetType().Name})");
                                        switch (tab.Layout)
                                        {
                                            case BioLayout bioLayout:                                               
                                                try { Bio.RenderBioPreview(bioLayout, tab.Name, profileData.titleColor); } catch(Exception ex){ Plugin.plugin.logger.Error($"[TargetProfileWindow] Bio.RenderBioPreview failed, trying to set default avatar and background.{ex.ToString()}"); }
                                                break;
                                            case DetailsLayout detailsLayout:
                                                try { Details.RenderDetailPreview(detailsLayout, profileData.titleColor); } catch (Exception ex) { Plugin.plugin.logger.Error($"[TargetProfileWindow] Details.RenderDetailPreview error: {ex.Message}"); }
                                                break;
                                            case GalleryLayout galleryLayout:
                                                try { Gallery.RenderGalleryPreview(galleryLayout, profileData.titleColor); } catch (Exception ex) { Plugin.plugin.logger.Error($"[TargetProfileWindow] Gallery.RenderGalleryPreview error: {ex.Message}"); }
                                                break;
                                            case InfoLayout infoLayout:
                                                try
                                                {
                                                    Misc.SetTitle(plugin, true, tab.Name, profileData.titleColor);
                                                    Misc.RenderHtmlColoredTextInline(infoLayout.text, 400);
                                                }
                                                catch (Exception ex)
                                                {
                                                    Plugin.plugin.logger.Error($"[TargetProfileWindow] InfoLayout render error: {ex.Message}");
                                                }
                                                break;
                                            case StoryLayout storyLayout:
                                                try { Story.RenderStoryPreview(storyLayout, profileData.titleColor); } catch (Exception ex) { Plugin.plugin.logger.Error($"[TargetProfileWindow] Story.RenderStoryPreview error: {ex.Message}"); }
                                                break;
                                            case TreeLayout treeLayout:
                                                string uniqueId = $"{tab.Name}##{treeLayout.tabIndex}";
                                                try { Tree.RenderTreeLayout(treeLayout.tabIndex, false, uniqueId, treeLayout, tab.Name, profileData.titleColor); } catch (Exception ex) { Plugin.plugin.logger.Error($"[TargetProfileWindow] Tree.RenderTreeLayout error: {ex.Message}"); }
                                                break;
                                            default:
                                                Plugin.plugin.logger.Debug($"[TargetProfileWindow] Unknown tab layout type: {tab.Layout.GetType().Name}");
                                                break;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Plugin.plugin.logger.Error($"[TargetProfileWindow] Tab Render Error: {ex.Message}");
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
                    Plugin.plugin.logger.Debug("[TargetProfileWindow] No custom tabs to render.");
                    ImGui.TextColored(new Vector4(1, 0, 0, 1), "No profile data available for this profile.");
                }

                // Layout selection warning
                if (currentLayout == null)
                {
                    Plugin.plugin.logger.Debug("[TargetProfileWindow] currentLayout is null, returning.");
                    return;
                }

                // Profile child region
                using var profileTable = ImRaii.Child("PROFILE");
                if (!profileTable)
                {
                    Plugin.plugin.logger.Debug("[TargetProfileWindow] profileTable child region not created.");
                    return;
                }

                // Notes and preview
                if (addNotes)
                {
                    try
                    {
                        plugin.OpenProfileNotes();
                    }
                    catch (Exception ex)
                    {
                        Plugin.plugin.logger.Error($"[TargetProfileWindow] OpenProfileNotes error: {ex.Message}");
                    }
                    addNotes = false;
                }

                if (loadPreview)
                {
                    try
                    {
                        plugin.OpenImagePreview();
                    }
                    catch (Exception ex)
                    {
                        Plugin.plugin.logger.Error($"[TargetProfileWindow] OpenImagePreview error: {ex.Message}");
                    }
                    loadPreview = false;
                }

                Plugin.plugin.logger.Debug("[TargetProfileWindow] Draw finished.");
            }
            catch (Exception ex)
            {
                Plugin.plugin.logger.Error("TargetWindow Draw Error: " + ex.Message);
                loading = "An error occurred while loading the profile data.";
                currentInd = 0;
                max = 1;
            }
        }
        public static void ResetAllData()
        {
            try
            {
                // Dispose of any textures to avoid memory leaks
                if (profileData != null)
                {
                    profileData.background = null;
                    profileData.avatar = null;

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
                                            img.image = null; // Thumbnail is disposed in the loop
                                            img.thumbnail = null; // Clear reference to avoid dangling pointers
                                        }
                                        gallery.images.Clear();
                                    }
                                    break;
                                case InventoryLayout inventory:
                                    if (inventory.inventorySlotContents != null)
                                    {
                                        foreach (var item in inventory.inventorySlotContents.Values)
                                        {
                                            item.iconTexture = null; // Clear reference to avoid dangling pointers
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
                                                trait.icon.icon = null; // Clear reference to avoid dangling pointers
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

                // Reset all static and instance fields to their initial state
                profileData = new ProfileData
                {
                    avatar = null,
                    background = null,
                    title = string.Empty,
                    titleColor = new Vector4(1, 1, 1, 1),
                    isPrivate = false,
                    isActive = false,
                    Name = string.Empty,
                    Race = string.Empty,
                    Gender = string.Empty,
                    Age = string.Empty,
                    Height = string.Empty,
                    Weight = string.Empty,
                    AFG = string.Empty,
                    Alignment = 0,
                    Personality_1 = 0,
                    Personality_2 = 0,
                    Personality_3 = 0,
                    StoryLayouts = null,
                    fields = new List<field>(),
                    descriptors = new List<descriptor>(),
                    traits = new List<trait>(),
                    OOC = string.Empty,
                    customTabs = new List<CustomTab>()
                };

                profileLayouts?.Clear();
                profileLayouts = new List<CustomLayout>();
                currentLayout = new CustomLayout();

                loading = string.Empty;
                currentInd = 0;
                max = 0;
                addNotes = false;
                loadPreview = false;
                firstDraw = true;
                warning = false;
                warningMessage = string.Empty;
                ExistingProfile = false;
                characterName = string.Empty;
                characterWorld = string.Empty;
            }
            catch (Exception ex)
            {
                Plugin.plugin.logger.Error("TargetProfileWindow ResetAllData Error: " + ex.Message);
            }
        }

        public static bool IsDefault()
        {
            var pd = profileData;
            bool profileDataDefault =
                pd != null &&
                pd.avatar == null &&
                pd.background == null &&
                pd.title == string.Empty &&
                pd.titleColor == new Vector4(1, 1, 1, 1) &&
                pd.isPrivate == false &&
                pd.isActive == false &&
                pd.Name == string.Empty &&
                pd.Race == string.Empty &&
                pd.Gender == string.Empty &&
                pd.Age == string.Empty &&
                pd.Height == string.Empty &&
                pd.Weight == string.Empty &&
                pd.AFG == string.Empty &&
                pd.Alignment == 0 &&
                pd.Personality_1 == 0 &&
                pd.Personality_2 == 0 &&
                pd.Personality_3 == 0 &&
                pd.StoryLayouts == null &&
                pd.fields != null && pd.fields.Count == 0 &&
                pd.descriptors != null && pd.descriptors.Count == 0 &&
                pd.traits != null && pd.traits.Count == 0 &&
                pd.OOC == string.Empty &&
                pd.customTabs != null && pd.customTabs.Count == 0;

            bool otherDefaults =
                string.IsNullOrEmpty(loading) &&
                currentInd == 0 &&
                max == 0 &&
                addNotes == false &&
                loadPreview == false &&
                firstDraw == true &&
                warning == false &&
                string.IsNullOrEmpty(warningMessage) &&
                ExistingProfile == false &&
                (profileLayouts == null || profileLayouts.Count == 0) &&
                currentLayout != null && // currentLayout is set to new CustomLayout()
                characterName == string.Empty &&
                characterWorld == string.Empty;

            return profileDataDefault && otherDefaults;
        }
        public void Dispose()
        {
            
            if (profileData != null)
            {
                WindowOperations.SafeDispose(profileData.background);
                profileData.background = null;
                WindowOperations.SafeDispose(profileData.avatar);
                profileData.avatar = null;

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
                                        img.image = null; // Thumbnail is disposed in the loop
                                        WindowOperations.SafeDispose(img.thumbnail);
                                        img.thumbnail = null; // Clear reference to avoid dangling pointers
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
                                        item.iconTexture = null; // Clear reference to avoid dangling pointers
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
                                            trait.icon.icon = null; // Clear reference to avoid dangling pointers
                                            trait.icon = null; // Clear reference to avoid dangling pointers
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

                // Dispose and clear static layouts
                profileLayouts?.Clear();
                currentLayout = null;

                // Reset profile data
                profileData = new ProfileData();
                if (profileData.customTabs == null)
                    profileData.customTabs = new List<CustomTab>();
            }
           
        }
    }
}
