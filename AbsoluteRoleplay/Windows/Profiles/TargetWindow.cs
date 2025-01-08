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
            if (warning)
            {
                if (ImGui.BeginPopupModal($"WARNING MESSAGE", ref warning, ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGuiHelpers.SafeTextWrapped(warningMessage);
                    ImGui.Text("Do you agree to view the profile.");
                    if (ImGui.Button("Agree"))
                    {
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.SameLine();

                    if (ImGui.Button("Cancel"))
                    {
                        this.IsOpen = false;
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.EndPopup();
                }
            }
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
            if (plugin.IsOnline())
            {
                
                if (AllLoaded() == true)
                {
                    //if we receive that there is an existing profile that we can view show the available view buttons
                    if (ExistingProfile == true)
                    {
                        if (firstDraw)
                        {
                            currentTab = "Bio";
                            ClearUI();
                            viewBio = true;
                            firstDraw = false;
                        }

                        ImGui.BeginTabBar("TargetNavigation");

                        if(activeTab == "BIO")
                        {
                            ClearUI(); currentTab = "Bio"; viewBio = true;
                        }
                        ImGui.BeginTabBar("TargetNavigation");
                        if (ExistingBio) { if (ImGui.BeginTabItem("Bio")) { if (currentTab != "Bio") { ClearUI(); currentTab = "Bio"; viewBio = true; } ImGui.EndTabItem(); } }
                        if (ExistingHooks) { if (ImGui.BeginTabItem("Hooks")) { if (currentTab != "Hooks") { ClearUI(); currentTab = "Hooks"; viewHooks = true; } ImGui.EndTabItem(); } }
                        if (ExistingStory) { if (ImGui.BeginTabItem("Story")) { if (currentTab != "Story") { ClearUI(); currentTab = "Story"; viewStory = true; } ImGui.EndTabItem(); } }
                        if (oocInfo != string.Empty) { if (ImGui.BeginTabItem("OOC")) { if (currentTab != "OOC") { ClearUI(); currentTab = "OOC"; viewOOC = true; } ImGui.EndTabItem(); } }
                        if (ExistingGallery) { if (ImGui.BeginTabItem("Gallery")) { if (currentTab != "Gallery") { ClearUI(); currentTab = "Gallery"; viewGallery = true; } ImGui.EndTabItem(); } }
                        ImGui.EndTabBar();
                        //personal controls for viewing user
                        if(self == false)
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
                            if (ImGui.IsItemHovered()) { ImGui.SetTooltip("Report this profile for inappropriate use.\n(Repeat false reports may result in your account being banned.)"); }


                        }
                    }

                    using var profileTable = ImRaii.Child("PROFILE");
                    if (profileTable)
                    {
                        //if there is absolutely no items to view
                        if (ExistingBio == false && ExistingHooks == false && ExistingStory == false && ExistingOOC == false && ExistingOOC == false && ExistingGallery == false)
                        {
                            //inform the viewer that there is no profile to view
                            ImGuiHelpers.SafeTextWrapped("No Profile Data Available:\nIf this character has a profile, you can request to view it below.");

                            //but incase the profile is set to private, give the user a request button to ask for access
                            if (ImGui.Button("Request access"))
                            {
                                //send a new request to the server and then the profile owner if pressed
                                DataSender.SendProfileAccessUpdate(plugin.username, plugin.playername, plugin.playerworld, characterName, characterWorld, (int)UI.ConnectionStatus.pending);
                            }
                        }
                        else
                        {
                            if (viewBio == true)
                            {
                                //set bordered title at top of window and set fonts back to normal
                                Misc.SetTitle(plugin, true, characterEditName);
                                ImGui.Image(currentAvatarImg.ImGuiHandle, new Vector2(ImGui.GetIO().FontGlobalScale / 0.015f)); //display avatar image
                                if (characterEditName != string.Empty && characterEditName != "New Profile")
                                {
                                    ImGui.Spacing();
                                    ImGuiHelpers.SafeTextWrapped("NAME:   " + characterEditName); // display character name
                                }
                                if (characterEditRace != string.Empty)
                                {
                                    ImGui.Spacing();
                                    ImGuiHelpers.SafeTextWrapped("RACE:   " + characterEditRace); // race
                                }
                                if (characterEditGender != string.Empty)
                                {
                                    ImGui.Spacing();
                                    ImGuiHelpers.SafeTextWrapped("GENDER:   " + characterEditGender); //and so on
                                }
                                if (characterEditAge != string.Empty)
                                {
                                    ImGui.Spacing();
                                    ImGuiHelpers.SafeTextWrapped("AGE:   " + characterEditAge);
                                }
                                if (characterEditHeight != string.Empty)
                                {
                                    ImGui.Spacing();
                                    ImGuiHelpers.SafeTextWrapped("HEIGHT:   " + characterEditHeight);
                                }
                                if (characterEditWeight != string.Empty)
                                {
                                    ImGui.Spacing();
                                    ImGuiHelpers.SafeTextWrapped("WEIGHT:   " + characterEditWeight);
                                }
                                if (characterEditAfg != string.Empty)
                                {
                                    ImGui.Spacing();
                                    ImGuiHelpers.SafeTextWrapped("AT FIRST GLANCE: \n" + characterEditAfg);
                                }
                                ImGui.Spacing();
                                if (showAlignment == true)
                                {
                                    ImGui.TextColored(new Vector4(1, 1, 1, 1), "ALIGNMENT:");

                                    ImGui.Image(alignmentImg.ImGuiHandle, new Vector2(ImGui.GetIO().FontGlobalScale * 40));

                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip(alignmentTooltip);
                                    }
                                }
                                if (showPersonality == true)
                                {
                                    ImGui.Spacing();

                                    Vector2 alignmentSize = new Vector2(ImGui.GetIO().FontGlobalScale * 25, ImGui.GetIO().FontGlobalScale * 32);
                                    ImGui.TextColored(new Vector4(1, 1, 1, 1), "PERSONALITY TRAITS:");
                                    if(showPersonality1 == true)
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
                                    if(showPersonality3 == true)
                                    {
                                        ImGui.Image(personalityImg3.ImGuiHandle, alignmentSize);

                                        if (ImGui.IsItemHovered())
                                        {
                                            ImGui.SetTooltip(personality3Tooltip);
                                        }
                                    }
                                  
                                   

                                }


                            }



                            if (viewHooks == true)
                            {
                                Misc.SetTitle(plugin, true, "Hooks"); //set title again
                                for (var h = 0; h < hookEditCount; h++)
                                {
                                    Misc.SetCenter(plugin, HookNames[h].ToString()); // set the position to the center of the window
                                    ImGuiHelpers.SafeTextWrapped(HookNames[h].ToUpper()); //display the title in the center
                                    ImGuiHelpers.SafeTextWrapped(HookContents[h]); //display the content
                                }

                            }

                            if (viewStory == true)
                            {
                                Misc.SetTitle(plugin, true, storyTitle);
                                var chapterMsg = "";


                                for (var h = 0; h < chapterCount; h++)
                                {
                                    Misc.SetCenter(plugin, ChapterTitle[h]);
                                    ImGuiHelpers.SafeTextWrapped(ChapterTitle[h].ToUpper());
                                    ImGui.Spacing();
                                    using var defInfFontDen = ImRaii.DefaultFont();
                                    ImGuiHelpers.SafeTextWrapped(ChapterContent[h]);
                                }


                            }
                            if (viewOOC == true)
                            {
                                Misc.SetTitle(plugin, true, "OOC Information");
                                ImGuiHelpers.SafeTextWrapped(oocInfo);
                            }
                            if (viewGallery == true)
                            {
                                Misc.SetTitle(plugin, true, "Gallery");
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

                            if (addNotes == true)
                            {
                                plugin.OpenProfileNotes();
                                addNotes = false;
                            }
                            if (loadPreview == true)
                            {
                                plugin.OpenImagePreview();
                                loadPreview = false;
                            }
                        }

                    }
                }
                else
                {
                    Misc.StartLoader(currentInd, max, loading, ImGui.GetWindowSize());
                }
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
