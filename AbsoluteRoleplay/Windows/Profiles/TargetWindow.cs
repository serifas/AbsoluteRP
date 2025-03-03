using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Dalamud.Interface.GameFonts;
using OtterGui.Raii;
using Networking;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Internal;
using AbsoluteRoleplay.Helpers;
using Microsoft.VisualBasic;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using OtterGuiInternal.Enums;
using AbsoluteRoleplay.Windows.Ect;
using System.Diagnostics;
using Dalamud.Interface.Colors;
using AbsoluteRoleplay.Windows.Profiles.ProfileTabs;

namespace AbsoluteRoleplay.Windows.Profiles
{
    public class TargetWindow : Window, IDisposable
    {
        private Plugin plugin;
        public static string loading;
        private IDalamudPluginInterface pg;
        public static float currentInd, max;
        public static string characterNameVal, characterWorldVal;
        public static string[] ChapterContent = new string[30];
        public static string[] ChapterTitle = new string[30];
        public static string[] HookNames = new string[30];
        public static string[] HookContents = new string[30];
        public static string[] HookEditContent = new string[30];
        public static int chapterCount;
        public static bool viewBio, viewHooks, viewStory, viewOOC, viewGallery, addNotes, loadPreview = false; //used to specify what view to show
        public static bool ExistingBio;
        public static bool ExistingHooks;
        public static int hookEditCount, existingGalleryImageCount;
        public static bool showAlignment;
        public static bool showPersonality, showPersonality1, showPersonality2, showPersonality3 = false;
        public static bool ExistingStory;
        public static bool ExistingOOC;
        public static bool ExistingGallery;
        public static bool ExistingProfile;
        public static string storyTitle = "";
        public static byte[] existingAvatarBytes;
        public static string currentTab = null;
        public static bool isActive = currentTab == "Bio";
        public static string[] imageTooltips = new string[30];
        //BIO VARS
        public static IDalamudTextureWrap alignmentImg, personalityImg1, personalityImg2, personalityImg3;
        public static IDalamudTextureWrap[] galleryImages, galleryThumbs = new IDalamudTextureWrap[30];
        public static List<IDalamudTextureWrap> galleryThumbsList = new List<IDalamudTextureWrap>();
        public static List<IDalamudTextureWrap> galleryImagesList = new List<IDalamudTextureWrap>();
        public static Vector2 avatarSize = new Vector2(100,100);
        public static IDalamudTextureWrap currentAvatarImg, pictureTab;
        public static List<trait> personalities = new List<trait>();
        public static List<descriptor> descriptors = new List<descriptor>();
        public static List<field> fields = new List<field>();
        //profile vars
        public static string characterEditName,
                                characterEditRace,
                                characterEditGender,
                                characterEditAge,
                                characterEditAfg,
                                characterEditHeight,
                                characterEditWeight,
                                fileName,
                                reportInfo,
                                profileNotes,
                                alignmentTooltip,
                                personality1Tooltip,
                                personality2Tooltip,
                                personality3Tooltip,
                                oocInfo = string.Empty;
        public static bool[] ChapterExists = new bool[30];
        internal static string characterName;
        internal static string characterWorld;
        public static bool firstDraw = true;
        public static string activeTab;
        public static bool self = false;
        internal static bool warning;
        internal static string warningMessage;

        public static Vector4 TitleColor { get; set; }
        public static string Title { get; internal set; }

        public TargetWindow(Plugin plugin) : base(
       "TARGET", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(300, 300),
                MaximumSize = new Vector2(950, 950)
            };
            this.plugin = plugin;
            pg = Plugin.PluginInterface;
        }
        public override void OnOpen()
        {
           
            var blankPictureTab = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
            if (blankPictureTab != null)
            {
                pictureTab = blankPictureTab;
            }

            //alignment icons
            for (var i = 0; i < 30; i++)
            {
                ChapterContent[i] = string.Empty;
                ChapterTitle[i] = string.Empty;
                ChapterExists[i] = false;
                HookContents[i] = string.Empty;
                HookNames[i] = string.Empty;
                galleryImagesList.Add(pictureTab);
                galleryThumbsList.Add(pictureTab);

            }
            galleryImages = galleryImagesList.ToArray();
            galleryThumbs = galleryThumbsList.ToArray();
        }

        public override void Draw()
        {
            if (!plugin.IsOnline())
                return;

            if (!AllLoaded())
            {
                Misc.StartLoader(currentInd, max, loading, ImGui.GetWindowSize());
                return;
            }

            if (warning)
            {
                ImGui.OpenPopup("WARNING");

                if (ImGui.BeginPopupModal("WARNING", ref warning, ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGui.Text(warningMessage);
                    ImGui.TextColored(new Vector4(1, 0, 0, 1), "Do you agree to view the profile anyway?");

                    if (ImGui.Button("Agree"))
                    {
                        warning = false;
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Go back"))
                    {
                        this.IsOpen = false;
                        warning = false;
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.EndPopup();
                }
            }

            if (!ExistingProfile)
                return;

            if (firstDraw)
            {
                currentTab = "Bio";
                ClearUI();
                viewBio = true;
                firstDraw = false;
            }

            float centeredX = (ImGui.GetWindowSize().X - currentAvatarImg.Size.X) / 2;
            ImGui.SetCursorPosX(centeredX);
            ImGui.Image(currentAvatarImg.ImGuiHandle, new Vector2(ImGui.GetIO().FontGlobalScale / 0.015f));
            Misc.SetTitle(plugin, true, Title, TitleColor);
            if (!self)
            {
                ImGui.Text("Controls");
                if (ImGui.Button("Notes")) { addNotes = true; }
                if (ImGui.IsItemHovered()) { ImGui.SetTooltip("Add personal notes about this profile."); }

                ImGui.SameLine();
                Misc.RenderAlignmentToRight("Report");

                if (ImGui.Button("Report"))
                {
                    ReportWindow.reportCharacterName = characterNameVal;
                    ReportWindow.reportCharacterWorld = characterWorldVal;
                    plugin.OpenReportWindow();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Report this profile for inappropriate use.\n(Repeat false reports may result in your account being banned.)");
                }
            }

            if (ImGui.BeginTabBar("TargetNavigation"))
            {
                if (activeTab == "BIO")
                {
                    ClearUI();
                    currentTab = "Bio";
                    viewBio = true;
                }


                if (ExistingBio && ImGui.BeginTabItem("Bio"))
                {
                    if (currentTab != "Bio") { ClearUI(); currentTab = "Bio"; viewBio = true; }
                    ImGui.EndTabItem();
                }
                if (ExistingHooks && ImGui.BeginTabItem("Hooks"))
                {
                    if (currentTab != "Hooks") { ClearUI(); currentTab = "Hooks"; viewHooks = true; }
                    ImGui.EndTabItem();
                }
                if (ExistingStory && ImGui.BeginTabItem("Story"))
                {
                    if (currentTab != "Story") { ClearUI(); currentTab = "Story"; viewStory = true; }
                    ImGui.EndTabItem();
                }
                if (!string.IsNullOrEmpty(oocInfo) && ImGui.BeginTabItem("OOC"))
                {
                    if (currentTab != "OOC") { ClearUI(); currentTab = "OOC"; viewOOC = true; }
                    ImGui.EndTabItem();
                }
                if (ExistingGallery && ImGui.BeginTabItem("Gallery"))
                {
                    if (currentTab != "Gallery") { ClearUI(); currentTab = "Gallery"; viewGallery = true; }
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

            using var profileTable = ImRaii.Child("PROFILE");
            if (!profileTable)
                return;

            if (!ExistingBio && !ExistingHooks && !ExistingStory && !ExistingOOC && !ExistingGallery)
            {
                ImGuiHelpers.SafeTextWrapped("No Profile Data Available:\nIf this character has a profile, you can request to view it below.");
                if (ImGui.Button("Request access"))
                {
                    DataSender.SendProfileAccessUpdate(plugin.username, plugin.playername, plugin.playerworld, characterName, characterWorld, (int)UI.ConnectionStatus.pending);
                }
                return;
            }

            if (viewBio)
            {
                if (!string.IsNullOrEmpty(characterEditName) && characterEditName != "New Profile")
                {
                    ImGui.Spacing();
                    ImGuiHelpers.SafeTextWrapped("NAME:   " + characterEditName);
                }
                if (!string.IsNullOrEmpty(characterEditRace))
                {
                    ImGui.Spacing();
                    ImGuiHelpers.SafeTextWrapped("RACE:   " + characterEditRace);
                }
                if (!string.IsNullOrEmpty(characterEditGender))
                {
                    ImGui.Spacing();
                    ImGuiHelpers.SafeTextWrapped("GENDER:   " + characterEditGender);
                }
                if (!string.IsNullOrEmpty(characterEditAge))
                {
                    ImGui.Spacing();
                    ImGuiHelpers.SafeTextWrapped("AGE:   " + characterEditAge);
                }
                if (!string.IsNullOrEmpty(characterEditHeight))
                {
                    ImGui.Spacing();
                    ImGuiHelpers.SafeTextWrapped("HEIGHT:   " + characterEditHeight);
                }
                if (!string.IsNullOrEmpty(characterEditWeight))
                {
                    ImGui.Spacing();
                    ImGuiHelpers.SafeTextWrapped("WEIGHT:   " + characterEditWeight);
                }
                foreach (var descriptor in descriptors)
                {
                    ImGui.Spacing();
                    ImGuiHelpers.SafeTextWrapped(descriptor.name.ToUpper() + ":   " + descriptor.description);
                }
                if (!string.IsNullOrEmpty(characterEditAfg))
                {
                    ImGui.Spacing();
                    ImGuiHelpers.SafeTextWrapped("AT FIRST GLANCE: \n" + characterEditAfg);
                }
                foreach (var field in fields)
                {
                    ImGui.Spacing();
                    ImGuiHelpers.SafeTextWrapped(field.name.ToUpper() + ":   \n" + field.description);
                }
                Vector2 alignmentSize = new Vector2(ImGui.GetIO().FontGlobalScale * 25, ImGui.GetIO().FontGlobalScale * 32);
                if (showPersonality == true)
                {
                    ImGui.Spacing();

                    ImGui.TextColored(new Vector4(1, 1, 1, 1), "TRAITS:");
                    if (showPersonality1 == true)
                    {
                        ImGui.Image(personalityImg1.ImGuiHandle, alignmentSize);

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(personality1Tooltip);
                        }
                        ImGui.SameLine();
                    }
                    if (showPersonality2 == true)
                    {
                        ImGui.Image(personalityImg2.ImGuiHandle, alignmentSize);

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(personality2Tooltip);
                        }
                        ImGui.SameLine();
                    }
                    if (showPersonality3 == true)
                    {
                        ImGui.Image(personalityImg3.ImGuiHandle, alignmentSize);

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(personality3Tooltip);
                        }
                    }
                    foreach (trait personality in personalities)
                    {
                        ImGui.Spacing();
                        ImGui.Image(personality.icon.icon.ImGuiHandle, alignmentSize);
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(personality.name + ": \n" + personality.description);
                        }
                    }
                }
            }

            if (viewHooks)
            {
                Misc.SetTitle(plugin, true, "Hooks", TitleColor);
                for (var h = 0; h < hookEditCount; h++)
                {
                    ImGuiHelpers.SafeTextWrapped(HookNames[h].ToUpper());
                    ImGuiHelpers.SafeTextWrapped(HookContents[h]);
                }
            }

            if (viewStory)
            {
                Misc.SetTitle(plugin, true, storyTitle, TitleColor);
                for (var h = 0; h < chapterCount; h++)
                {
                    ImGuiHelpers.SafeTextWrapped(ChapterTitle[h].ToUpper());
                    ImGui.Spacing();
                    ImGuiHelpers.SafeTextWrapped(ChapterContent[h]);
                }
            }

            if (viewOOC)
            {
                Misc.SetTitle(plugin, true, "OOC Information", TitleColor);
                ImGuiHelpers.SafeTextWrapped(oocInfo);
            }

            if (viewGallery)
            {
                Misc.SetTitle(plugin, true, "Gallery", TitleColor);
                using var table = ImRaii.Table("GalleryTargetTable", 4);
                if (table)
                {
                    for (var i = 0; i < existingGalleryImageCount; i++)
                    {
                        ImGui.TableNextColumn();
                        ImGui.Image(galleryThumbs[i].ImGuiHandle, new Vector2(galleryThumbs[i].Width, galleryThumbs[i].Height));
                        if (ImGui.IsItemHovered()) { ImGui.SetTooltip(imageTooltips[i] + "\nClick to enlarge"); }
                        if (ImGui.IsItemClicked())
                        {
                            ImagePreview.width = galleryImages[i].Width;
                            ImagePreview.height = galleryImages[i].Height;
                            ImagePreview.PreviewImage = galleryImages[i];
                            loadPreview = true;
                        }
                    }
                }
            }

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


        public static void ClearUI()
        {
            viewBio = false;
            viewHooks = false;
            viewStory = false;
            viewOOC = false;
            viewGallery = false;
            addNotes = false;
           
        }
        public static void ReloadTarget()
        {
            DataReceiver.TargetBioLoadStatus = -1;
            DataReceiver.TargetGalleryLoadStatus = -1;
            DataReceiver.TargetHooksLoadStatus = -1;
            DataReceiver.TargetStoryLoadStatus = -1;
            DataReceiver.TargetNotesLoadStatus = -1;
        }
        public void Dispose()
        {
            // Properly dispose of IDisposable resources
            currentAvatarImg?.Dispose();
            currentAvatarImg = null;
            pictureTab?.Dispose();
            pictureTab = null;
            alignmentImg?.Dispose();
            alignmentImg = null;
            personalityImg1?.Dispose();
            personalityImg1 = null;
            personalityImg2?.Dispose();
            personalityImg2 = null;
            personalityImg3?.Dispose();
            personalityImg3 = null;

            // Dispose gallery images and thumbs
            DisposeListResources(galleryImagesList);
            DisposeListResources(galleryThumbsList);
        }
        //method to check if all our data for the window is loaded
        public bool AllLoaded()
        {
            var loaded = false;
            if (DataReceiver.TargetStoryLoadStatus != -1 &&
              DataReceiver.TargetHooksLoadStatus != -1 &&
              DataReceiver.TargetBioLoadStatus != -1 &&
              DataReceiver.TargetGalleryLoadStatus != -1 &&
              DataReceiver.TargetNotesLoadStatus != -1)
            {
                loaded = true;
            }
            return loaded;
        }

        // Helper method to dispose resources in a list
        private void DisposeListResources(List<IDalamudTextureWrap> resources)
        {
            if (resources != null)
            {
                foreach (var resource in resources)
                {
                    if (resource != null)
                    {
                        resource?.Dispose();
                    }
                }
                resources.Clear();
            }
        }

    }
}
