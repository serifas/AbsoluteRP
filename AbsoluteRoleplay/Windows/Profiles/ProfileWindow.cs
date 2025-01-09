using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Dalamud.Utility;
using Networking;
using OtterGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Textures.TextureWraps;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AbsoluteRoleplay.Windows.Profiles.ProfileTabs;
using AbsoluteRoleplay.Windows.Inventory;
using AbsoluteRoleplay.Defines;
using System.Transactions;
using JetBrains.Annotations;


namespace AbsoluteRoleplay.Windows.Profiles
{
    public enum TabValue
    {
        Bio = 1,
        Hooks = 2,
        Story = 3,
        OOC = 4,
        Gallery = 5,
    }
    
    //changed
    public class ProfileWindow : Window, IDisposable
    {
        public static string loading; //loading status string for loading the profile gallery mainly
        public static float percentage = 0f; //loading base value
        private Plugin plugin;
        public static FileDialogManager _fileDialogManager; //for avatars only at the moment
        public Configuration configuration;
        public static IDalamudTextureWrap pictureTab; //picturetab.png for base picture in gallery
        public static SortedList<TabValue, bool> TabOpen = new SortedList<TabValue, bool>(); //what part of the profile we have open
        public static SortedList<int, bool> CustomTabOpen = new SortedList<int, bool>();
        public static bool addProfile, editProfile;
        public static string oocInfo = string.Empty;
        public static bool ExistingProfile = false;
        public static float loaderInd =  -1; //used for the gallery loading bar
        public static IDalamudTextureWrap avatarHolder;
        private IDalamudTextureWrap persistAvatarHolder;
        public static bool isPrivate;
        public static bool activeProfile;
        public static bool NSFW;
        public static bool Triggering;
        public static bool SpoilerARR;
        public static bool SpoilerHW;
        public static bool SpoilerSB;
        public static bool SpoilerSHB;
        public static bool SpoilerEW;
        public static bool SpoilerDT;
        public static int currentProfile = 0;

        
        public static List<PlayerProfile> profiles = new List<PlayerProfile>();
        public static List<Tuple<int, string, bool>> ProfileBaseData = new List<Tuple<int, string, bool>>();
        public static bool Bio, Hooks, Story, OOC, Gallery;
        public static List<bool> Customs = new List<bool>();
        private const int MaxTabs = 10; // Maximum number of tabs
        private string[] availableTabs = new string[MaxTabs]; // Array to store tab names
        private bool[] showInputPopup = new bool[MaxTabs]; // Array to track popup visibility
        private bool[] openTabs = new bool[MaxTabs]; // Tracks whether each tab is open
        private int customTabsCount = 0; // Current number of tabs
        private int tabToDeleteIndex = -1; // Index of the tab to delete
        private bool showDeleteConfirmationPopup = false; // Flag to show delete confirmation popup
        public static int currentElementID = 0;
        public static bool customTabSelected = false;
        public static bool Locked = false;
        public bool AddInputTextElement { get; private set; }
        public bool AddInputTextMultilineElement { get; private set; }
        public bool AddInputImageElement { get; private set; }

        public ProfileWindow(Plugin plugin) : base(
       "PROFILE", ImGuiWindowFlags.None)
        {
            SizeConstraints = new WindowSizeConstraints
            {

                MinimumSize = new Vector2(300, 300),
                MaximumSize = new Vector2(950, 950)
            };

            this.plugin = plugin;
            configuration = plugin.Configuration;
            _fileDialogManager = new FileDialogManager();
        }
        public override void OnOpen()
        {
            for(int i = 0; i < 10; i++)
            {
                showInputPopup[i] = false;
                availableTabs[i] = "";
            }
            InvTab.InitInventory();
            ProfileBaseData.Clear();
            TabOpen.Clear(); //clear our TabOpen array before populating again
            var avatarHolderImage = UI.UICommonImage(UI.CommonImageTypes.avatarHolder); //Load the avatarHolder TextureWrap from Constants.UICommonImage
            if (avatarHolderImage != null)
            {
                avatarHolder = avatarHolderImage; //set our avatarHolder ot the same TextureWrap
            }

            //same for pictureTab
            var pictureTabImage = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
            if (pictureTabImage != null)
            {
                pictureTab = pictureTabImage;
            }
            BioTab.currentAvatarImg = pictureTab;
            persistAvatarHolder = avatarHolder; //unneeded at the moment, but I seem to keep needing and not needing it so I am leaving it for now.
            for (var bf = 0; bf < BioTab.bioFieldsArr.Length; bf++)
            {
                //set all the bioFields to an empty string
                BioTab.bioFieldsArr[bf] = string.Empty;
            }
            foreach (TabValue tab in Enum.GetValues(typeof(TabValue)))
            {
                TabOpen.Add(tab, false); //set all tabs to be closed by default
            }
            //set the base value for our arrays and lists
            for (var i = 0; i < 31; i++)
            {
                StoryTab.ChapterNames[i] = string.Empty;
                StoryTab.ChapterContents[i] = string.Empty;
                HooksTab.HookNames[i] = string.Empty;
                HooksTab.HookContents[i] = string.Empty;
                HooksTab.hookExists[i] = false;
                GalleryTab.NSFW[i] = false;
                GalleryTab.TRIGGER[i] = false;
                StoryTab.storyChapterExists[i] = false;
                GalleryTab.imageTooltips[i] = string.Empty;
                StoryTab.viewChapter[i] = false;
                GalleryTab.ImageExists[i] = false;
                GalleryTab.galleryImagesList.Add(pictureTab);
                GalleryTab.galleryThumbsList.Add(pictureTab);
                GalleryTab.imageURLs[i] = string.Empty;
            }
            GalleryTab.galleryImages = GalleryTab.galleryImagesList.ToArray();
            GalleryTab.galleryThumbs = GalleryTab.galleryThumbsList.ToArray();
            //set all our text entry fields for the bio to empty strings
            for (var b = 0; b < BioTab.bioFieldsArr.Length; b++)
            {
                BioTab.bioFieldsArr[b] = string.Empty;
            }
            if (BioTab.avatarBytes == null)
            {
                //set the avatar to the avatar_holder.png by default
                if (Plugin.PluginInterface is { AssemblyLocation.Directory.FullName: { } path })
                {
                    BioTab.avatarBytes = File.ReadAllBytes(Path.Combine(path, "UI/common/profiles/avatar_holder.png"));
                }
            }

        }
        //method to check if we have loaded our data received from the server
      
        public static void ClearLoaded()
        {
            DataReceiver.StoryLoadStatus = -1;
            DataReceiver.HooksLoadStatus = -1;
            DataReceiver.BioLoadStatus = -1;
            DataReceiver.GalleryLoadStatus = -1;           
        }

        public void UpdateProfileData(int profileIndex)
        {
            ClearLoaded();
            SubmitProfileData();
            DataSender.FetchProfiles();
        }

        public override void Draw()
        {
            this.Flags = (customTabSelected && !DynamicInputs.Lockstatus || customTabSelected && DynamicInputs.EditStatus) ? ImGuiWindowFlags.NoMove : ImGuiWindowFlags.None;
            if (plugin.IsOnline())
            {
                //if we have loaded all
                //the data received from the server and we are logged in game
               
                _fileDialogManager.Draw(); //file dialog mainly for avatar atm. galleries later possibly.

                ImGui.SameLine();
                if (ImGui.Button("Add Profile"))
                {
                    isPrivate = true;
                    ResetOnChangeOrRemoval();
                    DataSender.CreateProfile(ProfileBaseData.Count);
                    currentProfile = ProfileBaseData.Count;
                    DataSender.FetchProfile(currentProfile);
                    ExistingProfile = true;
                }
                if (ProfileBaseData.Count > 0 && ExistingProfile == true)
                {
                    AddProfileSelection();
                    ImGui.SameLine();
                    if(ImGui.Button("Preview Profile"))
                    {
                        TargetWindow.characterNameVal = plugin.playername;
                        TargetWindow.characterWorldVal = plugin.playerworld;
                        DataSender.PreviewProfile(currentProfile);
                    }
                    DrawProfile();
                }
                if(ProfileBaseData.Count <= 0)
                {
                    ExistingProfile = false;
                }
                else
                {
                    ExistingProfile = true;
                }
                
            }
        }
        public async void DrawProfile()
        {
            if (percentage == loaderInd + 1)
            {

                ImGui.Checkbox("Set Private", ref isPrivate);
                ImGui.SameLine();
                ImGui.Checkbox("Set As Profile", ref activeProfile);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Set this as your profile for the current character.");
                }
                ImGui.Checkbox("Set as 18+", ref NSFW);
                ImGui.SameLine();
                ImGui.Checkbox("Set as Triggering", ref Triggering);

                ImGui.Text("Has Spoilers From:");
                ImGui.Checkbox("A Realm Reborn", ref SpoilerARR);
                ImGui.SameLine();
                ImGui.Checkbox("Heavensward", ref SpoilerHW);
                ImGui.SameLine();
                ImGui.Checkbox("Stormblood", ref SpoilerSB);

                ImGui.Checkbox("Shadowbringers", ref SpoilerSHB);
                ImGui.SameLine();
                ImGui.Checkbox("Endwalker", ref SpoilerEW);
                ImGui.SameLine();
                ImGui.Checkbox("Dawntrail", ref SpoilerDT);
               
                if (ImGui.Button("Save Profile"))
                {
                    SubmitProfileData();
                }
                ImGui.SameLine();
                using (OtterGui.Raii.ImRaii.Disabled(!Plugin.CtrlPressed()))
                {
                    if (ImGui.Button("Delete Profile"))
                    {
                        ClearLoaded();
                        DataSender.DeleteProfile(currentProfile);
                        currentProfile -= 1;
                        if(currentProfile < 0)
                        {
                            currentProfile = 0;
                        }
                        DataSender.FetchProfiles();
                        DataSender.FetchProfile(currentProfile);
                        if (ProfileBaseData.Count == 0)
                        {
                            ExistingProfile = false;
                        }
                        ResetOnChangeOrRemoval();

                    }
                }
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.SetTooltip("Delete your profile (This is a destructive action!)");
                }
                if (ImGui.Button("Backup"))
                {
                    SaveBackupFile();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Save a local backup of your profile.");
                }
                ImGui.SameLine();
                using (OtterGui.Raii.ImRaii.Disabled(ProfileHasContent()))
                {
                    if (ImGui.Button("Load Backup"))
                    {
                        LoadBackupFile();
                    }
                }
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.SetTooltip("To load a backup file you must have no existing data in your profile.");
                }

                ImGui.Spacing();
                // Button to trigger the popup for a new tab

                ImGui.BeginTabBar("ProfileNavigation");

                // Static tabs
                if (ImGui.BeginTabItem("Bio")) { ClearUI(); customTabSelected = false; TabOpen[TabValue.Bio] = true; ImGui.EndTabItem(); }
                if (ImGui.BeginTabItem("Hooks")) { ClearUI(); customTabSelected = false; TabOpen[TabValue.Hooks] = true; ImGui.EndTabItem(); }
                if (ImGui.BeginTabItem("Story")) { ClearUI(); customTabSelected = false; TabOpen[TabValue.Story] = true; ImGui.EndTabItem(); }
                if (ImGui.BeginTabItem("OOC")) { ClearUI(); customTabSelected = false; TabOpen[TabValue.OOC] = true; ImGui.EndTabItem(); }
                if (ImGui.BeginTabItem("Gallery")) { ClearUI(); customTabSelected = false; TabOpen[TabValue.Gallery] = true; ImGui.EndTabItem(); }

                // Custom tabs
                RenderCustomTabs();

                // Add the special "+ Add Tab" as a fake tab at the end
                if (ImGui.TabItemButton("  +  ##AddTab", ImGuiTabItemFlags.NoCloseWithMiddleMouseButton))
                {
                    if (customTabsCount < MaxTabs)
                    {
                        showInputPopup[customTabsCount] = true; // Open the popup for the new tab
                        availableTabs[customTabsCount] = ""; // Reset the name field
                        ImGui.OpenPopup($"New Page##{customTabsCount}"); // Trigger popup
                    }
                }

                ImGui.EndTabBar();

                // Render the popup for adding a new tab
              


                using var ProfileTable = ImRaii.Child("PROFILE");
                if (ProfileTable)
                {
                    if (TabOpen[TabValue.Bio])
                    {
                        BioTab.LoadBioTab();
                    }
                    if (TabOpen[TabValue.Hooks])
                    {
                        HooksTab.LoadHooksTab();
                    }
                    if (TabOpen[TabValue.Story])
                    {
                        StoryTab.LoadStoryTab();
                    }
                    if (TabOpen[TabValue.Gallery])
                    {
                        GalleryTab.LoadGalleryTab();
                    }
                    if (TabOpen[TabValue.OOC])
                    {
                        Vector2 inputSize = new Vector2(ImGui.GetWindowSize().X - 20, ImGui.GetWindowSize().Y - 20);
                       // oocInfo = Misc.WrapTextToFit(oocInfo, inputSize.X);
                        ImGui.InputTextMultiline("##OOC", ref oocInfo, 50000, inputSize);
                    }
                    if (GalleryTab.loadPreview == true)
                    {
                        //load gallery image preview if requested
                        plugin.OpenImagePreview();
                        GalleryTab.loadPreview = false;
                    }
                    if (GalleryTab.addGalleryImageGUI == true)
                    {
                        GalleryTab.AddImageToGallery(plugin, GalleryTab.galleryImageCount); //used to add our image to the gallery
                    }
                    if (HooksTab.AddHooks == true && TabOpen[TabValue.Hooks])
                    {
                        HooksTab.DrawHooksUI(plugin, HooksTab.hookCount);
                    }
                    if (BioTab.editAvatar == true)
                    {
                        BioTab.editAvatar = false;
                        Misc.EditImage(plugin, _fileDialogManager, true, 0);
                    }
                    if (StoryTab.drawChapter == true)
                    {
                        ImGui.NewLine();
                        StoryTab.DrawChapter(StoryTab.currentChapter, plugin);
                    }

                    //if true, reorders the gallery
                    if (GalleryTab.ReorderGallery == true)
                    {
                        GalleryTab.ReorderGallery = false;
                        GalleryTab.ReorderGalleryData();                     
                    }
                    //pretty much the same logic but with our hooks
                    if (HooksTab.ReorderHooks == true)
                    {
                        HooksTab.ReorderHooks = false;
                        HooksTab.ReorderHooksData(plugin);

                    }
                    //same for chapters aswell
                    if (StoryTab.ReorderChapters == true)
                    {
                        StoryTab.ReorderChapters = false;
                        StoryTab.ReorderChapterData(plugin);
                    }

                }

            }
            else
            {

                Misc.StartLoader(loaderInd, percentage, loading, ImGui.GetWindowSize());
            }
        }
        private void RenderDeleteConfirmationPopup()
        {
            if (showDeleteConfirmationPopup && ImGui.BeginPopupModal("Delete Tab Confirmation", ref showDeleteConfirmationPopup, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text($"Are you sure you want to delete the tab \"{availableTabs[tabToDeleteIndex]}\"?");
                ImGui.Spacing();

                // Confirm button
                if (ImGui.Button("Confirm"))
                {
                    openTabs[tabToDeleteIndex] = false; // Mark the tab as closed
                    showDeleteConfirmationPopup = false; // Close the popup
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();

                // Cancel button
                if (ImGui.Button("Cancel"))
                {
                    showDeleteConfirmationPopup = false; // Close the popup without deleting
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }
        public void RenderCustomTabs()
        {
            // Render all active popups
            for (int i = 0; i < MaxTabs; i++)
            {
                if (showInputPopup[i])
                {
                    if (ImGui.BeginPopupModal($"New Page##{i}", ref showInputPopup[i], ImGuiWindowFlags.AlwaysAutoResize))
                    {
                        ImGui.Text($"Enter the name for the page:");
                        ImGui.InputText($"##TabInput{i}", ref availableTabs[i], 100);

                        // Detect Enter key submission
                        var io = ImGui.GetIO();


                        if (ImGui.Button("Submit") || io.KeysDown[(int)ImGuiKey.Enter] && !string.IsNullOrWhiteSpace(availableTabs[i]))
                        {
                            openTabs[i] = true; // Mark the tab as open
                            customTabsCount++; // Increment the tab count
                            showInputPopup[i] = false; // Close the popup
                            ImGui.CloseCurrentPopup();

                        }
                        ImGui.SameLine();
                        // Optional cancel button
                        if (ImGui.Button("Cancel"))
                        {
                            showInputPopup[i] = false; // Close the popup without saving
                            availableTabs[i] = ""; // Clear the name
                            ImGui.CloseCurrentPopup();
                        }

                        ImGui.EndPopup();
                    }
                }
            }

            // Render all tabs
            for (int i = 0; i < customTabsCount; i++)
            {
                if (!string.IsNullOrWhiteSpace(availableTabs[i]))
                {
                    RenderTab(i, ref openTabs[i], availableTabs[i]);
                }
            }

            // Render the delete confirmation popup
            RenderDeleteConfirmationPopup();

            // Clean up closed tabs
            for (int i = customTabsCount - 1; i >= 0; i--)
            {
                if (!openTabs[i])
                {
                    for (int j = i; j < customTabsCount - 1; j++)
                    {
                        availableTabs[j] = availableTabs[j + 1];
                        openTabs[j] = openTabs[j + 1];
                        showInputPopup[j] = showInputPopup[j + 1];
                    }
                    customTabsCount--;
                }
            }
        }



        private void RenderTab(int index, ref bool isOpen, string tabName)
        {
            string uniqueId = $"{tabName}##{index}";

            // Render the tab with a close button
            if (ImGui.BeginTabItem(uniqueId, ref isOpen))
            {
                ClearUI();
                customTabSelected = true;
                // Ensure the layout exists
                var currentLayout = DynamicInputs.layouts.Values.FirstOrDefault(l => l.id == index);
                if (currentLayout == null)
                {
                    currentLayout = new Layout
                    {
                        id = index,
                        name = tabName,
                        elements = new List<LayoutElement>()

                    };
                    int ind = DynamicInputs.layouts.Count;
                    DynamicInputs.layouts.Add(ind, currentLayout);
                }
                if (Locked)
                {
                    if(ImGui.Button("Unlock All"))
                    {
                        DynamicInputs.Lockstatus = false;
                        for(int i = 0; i < DynamicInputs.layouts.Values.Count; i++)
                        {
                            for(int l = 0; l < DynamicInputs.layouts.Values[i].elements.Count; l++)
                            {
                                DynamicInputs.layouts[i].elements[l].locked = false;
                            }
                        }
                        Locked = false;
                    }
                }
                else
                {
                    if (ImGui.Button("Lock All"))
                    {
                        DynamicInputs.Lockstatus = true;
                        for (int i = 0; i < DynamicInputs.layouts.Values.Count; i++)
                        {
                            for (int l = 0; l < DynamicInputs.layouts.Values[i].elements.Count; l++)
                            {
                                DynamicInputs.layouts[i].elements[l].locked = true;
                            }
                        }
                        Locked = true;
                    }
                }
                // Render the Add Element button
                DynamicInputs.RenderAddElementButton(currentLayout);
                
                // Create a child window to contain the draggable elements
                if (ImGui.BeginChild($"DraggableContent##{index}", new Vector2(-1, -1), true, ImGuiWindowFlags.AlwaysUseWindowPadding))
                {
                    DynamicInputs.RenderElements(currentLayout, false, plugin);

                    ImGui.EndChild();
                }

                ImGui.EndTabItem();
            }

            // Handle the case where the tab is closed (close button pressed)
            if (!isOpen)
            {
                isOpen = true; // Reopen the tab temporarily to show the confirmation popup
                tabToDeleteIndex = index; // Store the index of the tab to delete
                showDeleteConfirmationPopup = true; // Show the delete confirmation popup
                ImGui.OpenPopup("Delete Tab Confirmation");
            }
        }




        private bool ProfileHasContent()
        {
            
            for (int i = 0; i < BioTab.bioFieldsArr.Length; i++)
            {
                if(BioTab.bioFieldsArr[i] != string.Empty)
                {
                    return true;
                }
            }
            if(oocInfo != string.Empty ||  StoryTab.storyTitle != string.Empty) { return true; }

            for (int i = 0; i < StoryTab.ChapterNames.Length; i++)
            {
                if(StoryTab.ChapterNames[i] != string.Empty) return true;

            }
            for (int i = 0; i < StoryTab.ChapterContents.Length; i++)
            {
                if (StoryTab.ChapterContents[i] != string.Empty)
                {
                    return true;
                }
            }
            for (int i = 0; i < HooksTab.HookNames.Length; i++)
            {
                if (HooksTab.HookNames[i] != string.Empty)
                {
                    return true;
                }
            }
            for (int i = 0; i < HooksTab.HookContents.Length; i++)
            {
                if (HooksTab.HookContents[i] != string.Empty)
                {
                    return true;
                }
            }
            for (int i = 0; i < GalleryTab.imageURLs.Length; i++)
            {
                if (GalleryTab.imageURLs[i] != string.Empty)
                {
                    return true;
                }
            }
            return false;
        }

     
       



      
        //method ot reset the entire story section
     

        //reset our tabs and go back to base ui with no tab selected
        public static void ClearUI()
        {
            TabOpen[TabValue.Bio] = false;
            TabOpen[TabValue.Hooks] = false;
            TabOpen[TabValue.Story] = false;
            TabOpen[TabValue.OOC] = false;
            TabOpen[TabValue.Gallery] = false;
        }
        public static void ResetOnChangeOrRemoval()
        {
            isPrivate = true;
            
            for(int i = 0; i < HooksTab.hookCount; i++)
            {
                HooksTab.hookExists[i] = false;
                HooksTab.HookNames[i] = string.Empty;
                HooksTab.HookContents[i] = string.Empty;
            }
            GalleryTab.ResetGallery();
            StoryTab.ResetStory();
            oocInfo = string.Empty;
            for(int i = 0; i < BioTab.bioFieldsArr.Length; i++)
            {
                BioTab.bioFieldsArr[i] = string.Empty;
            }
            BioTab.currentAvatarImg = UI.UICommonImage(UI.CommonImageTypes.avatarHolder);
            BioTab.currentAlignment = 9;
            BioTab.currentPersonality_1 = 26;
            BioTab.currentPersonality_2 = 26;
            BioTab.currentPersonality_3 = 26;
        }
        public void Dispose()
        {
            avatarHolder?.Dispose();
            avatarHolder = null;
            pictureTab?.Dispose();
            pictureTab = null;
            BioTab.currentAvatarImg?.Dispose();
            BioTab.currentAvatarImg = null;
            persistAvatarHolder?.Dispose();
            persistAvatarHolder = null;
            for (var i = 0; i < GalleryTab.galleryImagesList.Count; i++)
            {
                GalleryTab.galleryImagesList[i]?.Dispose();
                GalleryTab.galleryImagesList[i] = null;
            }
            for (var i = 0; i < GalleryTab.galleryThumbsList.Count; i++)
            {
                GalleryTab.galleryThumbsList[i]?.Dispose();
                GalleryTab.galleryThumbsList[i] = null;
            }
        }

      

        public void AddProfileSelection()
        { 
            List<string> profileNames = new List<string>();
            for(int i = 0; i < ProfileBaseData.Count; i++)
            {
                profileNames.Add(ProfileBaseData[i].Item2);
            }
            string[] ProfileNames = new string[profileNames.Count];
            ProfileNames = profileNames.ToArray();
            var profileName = ProfileNames[currentProfile];

            using var combo = OtterGui.Raii.ImRaii.Combo("##Profiles", profileName);
            if (!combo)
                return;
            foreach (var (newText, idx) in ProfileNames.WithIndex())
            {
                if(ProfileBaseData.Count > 0)
                {
                    var label = newText;
                    if (label == string.Empty)
                    {
                        label = "New Profile";
                    }
                    if (newText != string.Empty)
                    {
                        if (ImGui.Selectable(label + "##" + idx, idx == currentProfile))
                        {
                            for (int i = 0; i < HooksTab.hookCount; i++)
                            {
                                HooksTab.hookExists[i] = false;
                                HooksTab.HookNames[i] = string.Empty;
                                HooksTab.HookContents[i] = string.Empty;
                            }
                            GalleryTab.ResetGallery();
                            StoryTab.ResetStory();
                            oocInfo = string.Empty;
                            for (int i = 0; i < BioTab.bioFieldsArr.Length; i++)
                            {
                                BioTab.bioFieldsArr[i] = string.Empty;
                            }
                            BioTab.currentAvatarImg = UI.UICommonImage(UI.CommonImageTypes.avatarHolder);
                            BioTab.currentAlignment = 9;
                            BioTab.currentPersonality_1 = 26;
                            BioTab.currentPersonality_2 = 26;
                            BioTab.currentPersonality_3 = 26;
                            currentProfile = idx;
                            ClearLoaded();
                            DataSender.FetchProfile(currentProfile);
                            DataSender.FetchProfiles();
                        }
                        ImGuiUtil.SelectableHelpMarker("Select to edit profile");
                    }

                }
            }
        }








        public static int CountLinesBetweenStrings(string filePath, string startString, string endString)
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                string line;
                bool counting = false; // Flag to indicate if we're between the start and end strings
                int lineCount = 0; // Counter for lines

                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Contains(startString))
                    {
                        counting = true; // Start counting lines after finding the start string
                        continue; // Skip the start line itself
                    }

                    if (line.Contains(endString))
                    {
                        counting = false; // Stop counting lines after finding the end string
                        break; // Exit the loop
                    }

                    if (counting)
                    {
                        lineCount++; // Increment line count if we're in the counting state
                    }
                }

                return lineCount;
            }
        }
        public async void LoadBackupFile()
        {
            try
            {
                _fileDialogManager.OpenFileDialog("Load Backup", "Data{.dat, .json}", async (success, filePath) =>
                {
                    if (!success) return;

                    var dataPath = filePath.ToString();

                    try
                    {

                        using (StreamReader reader = new StreamReader($"{dataPath}"))
                        {
                            string backupContent = File.ReadAllText(dataPath);
                            // Read avatar bytes
                            string avatarByteString = Misc.ExtractTextBetweenTags(backupContent, "avatar");
                            byte[] avatarBytesData = Convert.FromBase64String(avatarByteString); // Convert back to byte array

                            string name = Misc.ExtractTextBetweenTags(backupContent, "name");
                            string race = Misc.ExtractTextBetweenTags(backupContent, "race");
                            string gender = Misc.ExtractTextBetweenTags(backupContent, "gender");
                            string age = Misc.ExtractTextBetweenTags(backupContent, "age");
                            string height = Misc.ExtractTextBetweenTags(backupContent, "height");
                            string weight = Misc.ExtractTextBetweenTags(backupContent, "weight");
                            string afg = Misc.ExtractTextBetweenTags(backupContent, "afg");

                            int alignment = int.Parse(Misc.ExtractTextBetweenTags(backupContent, "alignment"));
                            int pers1 = int.Parse(Misc.ExtractTextBetweenTags(backupContent, "personality_1"));
                            int pers2 = int.Parse(Misc.ExtractTextBetweenTags(backupContent, "personality_2"));
                            int pers3 = int.Parse(Misc.ExtractTextBetweenTags(backupContent, "personality_3"));


                            Story storyData = new Story();
                            List<ProfileGalleryImage> galleryimagedata = new List<ProfileGalleryImage>();
                            List<Chapter> chapterData = new List<Chapter>();
                            List<Hooks> hookData = new List<Hooks>();

                            //get hooks
                            string hookPattern = @"<hooks>(.*?)</hooks>";
                            Regex hookRegex = new Regex(hookPattern, RegexOptions.Singleline);

                            string hooknamePattern = @"<hookname>(.*?)</hookname>";
                            string hookcontentPattern = @"<hookcontent>(.*?)</hookcontent>";

                            MatchCollection hookMatches = hookRegex.Matches(backupContent);

                            foreach (Match hookMatch in hookMatches)
                            {
                                string hookContent = hookMatch.Groups[1].Value; // Content inside <hooks>...</hooks>

                                // Extract all <hookname> and <hookcontent> tags, even if they are empty
                                MatchCollection hookNameMatches = Regex.Matches(hookContent, hooknamePattern);
                                MatchCollection hookContentMatches = Regex.Matches(hookContent, hookcontentPattern);

                                // Get the maximum count of occurrences between hook names and hook content
                                int hookCount = Math.Max(hookNameMatches.Count, hookContentMatches.Count);

                                for (int i = 0; i < hookCount; i++)
                                {
                                    // Check if the current index is within bounds for hookNameMatches
                                    string hookName = i < hookNameMatches.Count ? hookNameMatches[i].Groups[1].Value : string.Empty;

                                    // Check if the current index is within bounds for hookContentMatches
                                    string hookContentValue = i < hookContentMatches.Count ? hookContentMatches[i].Groups[1].Value : string.Empty;

                                    // Add to hookData even if the name or content is empty
                                    hookData.Add(new Hooks { name = hookName, content = hookContentValue });
                                }
                            }



                            string storytitle = Misc.ExtractTextBetweenTags(backupContent, "storytitle");
                            // Story section with chapter extraction logic
                            string chapterPattern = @"<storychapters>(.*?)</storychapters>";
                            Regex chapterRegex = new Regex(chapterPattern, RegexOptions.Singleline);  // Ensure multiline content is captured

                            string chapterNamePattern = @"<chaptername>(.*?)</chaptername>";
                            string chapterContentPattern = @"<chaptercontent>(.*?)</chaptercontent>";

                            // Match the entire story chapters block
                            MatchCollection chapterTagMatches = chapterRegex.Matches(backupContent);


                            foreach (Match chapterMatch in chapterTagMatches)
                            {
                                string chapterTagContent = chapterMatch.Groups[1].Value; // Content inside <storychapters>...</storychapters>

                                // Extract all <chaptername> and <chaptercontent> tags, even if they contain multiline content
                                MatchCollection chapterNameMatches = Regex.Matches(chapterTagContent, chapterNamePattern, RegexOptions.Singleline);
                                MatchCollection chapterContentMatches = Regex.Matches(chapterTagContent, chapterContentPattern, RegexOptions.Singleline);

                                // Get the maximum count of occurrences between chapter names and contents
                                int chaptersCount = Math.Max(chapterNameMatches.Count, chapterContentMatches.Count);

                                for (int i = 0; i < chaptersCount; i++)
                                {
                                    // Safe access: Check if the current index is within bounds for chapterNameMatches
                                    string chapterName = i < chapterNameMatches.Count ? chapterNameMatches[i].Groups[1].Value : string.Empty;

                                    // Safe access: Check if the current index is within bounds for chapterContentMatches
                                    string chapterContent = i < chapterContentMatches.Count ? chapterContentMatches[i].Groups[1].Value : string.Empty;

                                    // Always add the chapter data even if name or content is null/empty
                                    chapterData.Add(new Chapter { name = chapterName, content = chapterContent });
                                }
                            }


                            storyData.chapters = chapterData;
                            // OOC Info
                            string OOC = Misc.ExtractTextBetweenTags(backupContent, "ooc");

                            // Gallery section with logic to extract info for the images and nsfw / trigger data
                            string galleryPattern = @"<gallery>(.*?)</gallery>";
                            Regex galleryRegex = new Regex(galleryPattern, RegexOptions.Singleline);

                            string galleryNSFWPattern = @"<nsfw>(.*?)</nsfw>";
                            string galleryTRIGGERPattern = @"<trigger>(.*?)</trigger>";
                            string galleryUrlPattern = @"<url>(.*?)</url>";
                            string galleryTooltipPattern = @"<tooltip>(.*?)</tooltip>";

                            MatchCollection galleryMatches = galleryRegex.Matches(backupContent);

                            // Loop through each <gallery> block (we expect only one, but handling as a collection)
                            foreach (Match galleryMatch in galleryMatches)
                            {
                                string galleryContent = galleryMatch.Groups[1].Value; // Content inside <gallery>...</gallery>

                                // Extract all <nsfw>, <trigger>, and <url> within the single gallery block
                                MatchCollection nsfwMatches = Regex.Matches(galleryContent, galleryNSFWPattern);
                                MatchCollection triggerMatches = Regex.Matches(galleryContent, galleryTRIGGERPattern);
                                MatchCollection urlMatches = Regex.Matches(galleryContent, galleryUrlPattern);
                                MatchCollection tooltipMatches = Regex.Matches(galleryContent, galleryTooltipPattern);

                                for (int i = 0; i < urlMatches.Count; i++)
                                {
                                    bool nsfw = bool.TryParse(nsfwMatches[i].Groups[1].Value, out bool nsfwResult) ? nsfwResult : false;
                                    bool trigger = bool.TryParse(triggerMatches[i].Groups[1].Value, out bool triggerResult) ? triggerResult : false;
                                    string url = urlMatches[i].Groups[1].Value;
                                    string tooltip = tooltipMatches[i].Groups[1].Value;

                                    if (!string.IsNullOrWhiteSpace(url))
                                    {
                                        galleryimagedata.Add(new ProfileGalleryImage { url = url, nsfw = nsfw, trigger = trigger, tooltip = tooltip });
                                    }
                                }
                            }

                            PlayerProfile profile = new PlayerProfile()
                            {
                                //bio
                                Name = name,
                                Race = race,
                                Age = age,
                                Gender = gender,
                                Height = height,
                                Weight = weight,
                                AFG = afg,
                                Alignment = alignment,
                                Personality_1 = pers1,
                                Personality_2 = pers2,
                                Personality_3 = pers3,
                                //ooc
                                OOC = OOC,
                                //story
                                Story = storyData,
                                //hooks
                                Hooks = hookData,
                                //gallery
                                GalleryImages = galleryimagedata
                            };

                            //send data to server
                            DataSender.SubmitProfileBio(currentProfile, avatarBytesData, name, race, gender, age, height, weight, afg, alignment, pers1, pers2, pers3);
                            var hooks = new List<Tuple<int, string, string>>();
                            for (var i = 0; i < hookData.Count; i++)
                            {
                                //create a new hook tuple to add to the list
                                var hook = Tuple.Create(i, hookData[i].name, hookData[i].content);
                                hooks.Add(hook);
                            }
                            DataSender.SendHooks(currentProfile, hooks);

                            //create a new list for our stories to be held in
                            var storyChapters = new List<Tuple<string, string>>();
                            for (var i = 0; i < chapterData.Count; i++)
                            {
                                //get the data from our chapterNames and Content and store them in a tuple ot be added in the storyChapters list
                                var chapterName = chapterData[i].name;
                                var chapterContent = chapterData[i].content;
                                var chapter = Tuple.Create(chapterName, chapterContent);
                                storyChapters.Add(chapter);
                            }
                            //finally send the story data to the server
                            DataSender.SendStory(currentProfile, storytitle, storyChapters);

                            for (var i = 0; i < galleryimagedata.Count; i++)
                            {
                                //pretty simple stuff, just send the gallery related array values to the server
                                DataSender.SendGalleryImage(currentProfile, galleryimagedata[i].nsfw, galleryimagedata[i].trigger, galleryimagedata[i].url, galleryimagedata[i].tooltip, i);

                            }
                            //send the OOC info to the server, just a string really
                            DataSender.SendOOCInfo(currentProfile, OOC);

                            DataSender.FetchProfile(currentProfile);



                            // Redraw the window to ensure content is visible
                        }
                    }
                    catch (Exception ex)
                    {
                        plugin.logger.Error($"Error reading backup file: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Error opening file dialog: {ex.Message}");
            }
        }


        // Helper function to extract values between tags



        // Helper method to extract tag content
        public async Task<string> ExtractTagFromFile(string filePath, string tag)
        {
            try
            {
                // Read the entire file content
                string fileContent = await File.ReadAllTextAsync(filePath);

                // Extract and return content for the specified tag
                return ExtractTagContent(fileContent, tag);
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., file not found, etc.)
                Console.Error.WriteLine($"Error reading file: {ex.Message}");
                return string.Empty; // Return empty if an error occurs
            }
        }

        private string ExtractTagContent(string content, string tagName)
        {
            var startTag = $"<{tagName}>";
            var endTag = $"</{tagName}>";

            var startIndex = content.IndexOf(startTag);
            var endIndex = content.IndexOf(endTag, startIndex + startTag.Length);

            if (startIndex >= 0 && endIndex > startIndex)
            {
                // Calculate the start position for content extraction
                startIndex += startTag.Length;

                // Return the extracted content
                return content.Substring(startIndex, endIndex - startIndex).Trim();
            }

            return string.Empty; // Return empty if tags are not found
        }

        // Helper method to read a tag value
        private string ReadTagValue(StreamReader reader, string tag)
        {
            string line = reader.ReadLine();
            return ExtractTagContent(line, tag);
        }
        
        public void SubmitProfileData()
        {
            try
            {

                DataSender.SetProfileStatus(isPrivate, activeProfile, currentProfile, SpoilerARR, SpoilerHW, SpoilerSB, SpoilerSHB, SpoilerEW, SpoilerDT, NSFW, Triggering);



                DataSender.SendLayouts(currentProfile, DynamicInputs.layouts);


                DataSender.SubmitProfileBio(currentProfile,
                                                  BioTab.avatarBytes,
                                                  BioTab.bioFieldsArr[(int)UI.BioFieldTypes.name].Replace("'", "''"),
                                                  BioTab.bioFieldsArr[(int)UI.BioFieldTypes.race].Replace("'", "''"),
                                                  BioTab.bioFieldsArr[(int)UI.BioFieldTypes.gender].Replace("'", "''"),
                                                  BioTab.bioFieldsArr[(int)UI.BioFieldTypes.age].Replace("'", "''"),
                                                  BioTab.bioFieldsArr[(int)UI.BioFieldTypes.height].Replace("'", "''"),
                                                  BioTab.bioFieldsArr[(int)UI.BioFieldTypes.weight].Replace("'", "''"),
                                                  BioTab.bioFieldsArr[(int)UI.BioFieldTypes.afg].Replace("'", "''"),
                                                  BioTab.currentAlignment, BioTab.currentPersonality_1, BioTab.currentPersonality_2, BioTab.currentPersonality_3
                                                  );
            var hooks = new List<Tuple<int, string, string>>();
            for (var i = 0; i < HooksTab.hookCount; i++)
            {
                //create a new hook tuple to add to the list
                var hook = Tuple.Create(i, HooksTab.HookNames[i], HooksTab.HookContents[i]);
                hooks.Add(hook);
            }
            DataSender.SendHooks(currentProfile, hooks);

            //create a new list for our stories to be held in
            var storyChapters = new List<Tuple<string, string>>();
            for (var i = 0; i < StoryTab.storyChapterCount + 1; i++)
            {
                //get the data from our chapterNames and Content and store them in a tuple ot be added in the storyChapters list
                var chapterName = StoryTab.ChapterNames[i].ToString();
                var chapterContent = StoryTab.ChapterContents[i].ToString();
                var chapter = Tuple.Create(chapterName, chapterContent);
                storyChapters.Add(chapter);
            }
            //finally send the story data to the server
            DataSender.SendStory(currentProfile, StoryTab.storyTitle, storyChapters);

            for (var i = 0; i < GalleryTab.galleryImageCount; i++)
            {
                //pretty simple stuff, just send the gallery related array values to the server
                DataSender.SendGalleryImage(currentProfile, GalleryTab.NSFW[i], GalleryTab.TRIGGER[i], GalleryTab.imageURLs[i], GalleryTab.imageTooltips[i], i);

            }
            //send the OOC info to the server, just a string really
            DataSender.SendOOCInfo(currentProfile, oocInfo);

            }
            catch(Exception ex)
            {
                plugin.logger.Error("Received exception in SubmitProfileBio " + ex.Message);
            }
            finally
            {
                DataSender.FetchProfiles();
                DataSender.FetchProfile(currentProfile);
            }
        }
        public void SaveBackupFile()
        {
            _fileDialogManager.SaveFileDialog("Save Backup", "Data{.dat, .json}", "backup", ".dat", (Action<bool, string>)((s, f) =>
            {
                if (!s)
                    return;
                var dataPath = f.ToString();

                using (var writer = new StreamWriter($"{dataPath}"))
                {
                    // Write avatarBytes as base64
                    string avatarBts = Convert.ToBase64String(BioTab.avatarBytes);
                    // Bio fields
                    writer.WriteLine($"<avatar>{avatarBts}</avatar>");
                    writer.WriteLine($"<name>{EscapeTagContent(BioTab.bioFieldsArr[(int)UI.BioFieldTypes.name])}</name>");
                    writer.WriteLine($"<race>{EscapeTagContent(BioTab.bioFieldsArr[(int)UI.BioFieldTypes.race])}</race>");
                    writer.WriteLine($"<gender>{EscapeTagContent(BioTab.bioFieldsArr[(int)UI.BioFieldTypes.gender])}</gender>");
                    writer.WriteLine($"<age>{EscapeTagContent(BioTab.bioFieldsArr[(int)UI.BioFieldTypes.age])}</age>");
                    writer.WriteLine($"<height>{EscapeTagContent(BioTab.bioFieldsArr[(int)UI.BioFieldTypes.height])}</height>");
                    writer.WriteLine($"<weight>{EscapeTagContent(BioTab.bioFieldsArr[(int)UI.BioFieldTypes.weight])}</weight>");
                    writer.WriteLine($"<afg>{EscapeTagContent(BioTab.bioFieldsArr[(int)UI.BioFieldTypes.afg])}</afg>");
                    writer.WriteLine($"<alignment>{BioTab.currentAlignment}</alignment>");
                    writer.WriteLine($"<personality_1>{BioTab.currentPersonality_1}</personality_1>");
                    writer.WriteLine($"<personality_2>{BioTab.currentPersonality_2}</personality_2>");
                    writer.WriteLine($"<personality_3>{BioTab.currentPersonality_3}</personality_3>");

                    // Hooks
                    writer.WriteLine("<hooks>");
                    for (int i = 0; i < HooksTab.hookCount; i++)
                    {
                        writer.WriteLine($"<hookname>{EscapeTagContent(HooksTab.HookNames[i])}</hookname>");
                        writer.WriteLine($"<hookcontent>{EscapeTagContent(HooksTab.HookContents[i])}</hookcontent>");
                    }
                    writer.WriteLine("</hooks>");

                    // Story chapters
                    writer.WriteLine($"<storytitle>{StoryTab.storyTitle}</storytitle>");      
                    writer.WriteLine("<storychapters>");
                    for (int i = 0; i <= StoryTab.storyChapterCount; i++)
                    {
                        writer.WriteLine($"<chaptername>{EscapeTagContent(StoryTab.ChapterNames[i])}</chaptername>");
                        writer.WriteLine($"<chaptercontent>{EscapeTagContent(StoryTab.ChapterContents[i])}</chaptercontent>");
                    }
                    writer.WriteLine("</storychapters>");

                    // OOC info
                    writer.WriteLine($"<ooc>{EscapeTagContent(oocInfo)}</ooc>");

                    // Gallery
                    writer.WriteLine("<gallery>");
                    for (int i = 0; i < GalleryTab.galleryImageCount; i++)
                    {
                        writer.WriteLine($"<nsfw>{GalleryTab.NSFW[i]}</nsfw>");
                        writer.WriteLine($"<trigger>{GalleryTab.TRIGGER[i]}</trigger>");
                        writer.WriteLine($"<url>{EscapeTagContent(GalleryTab.imageURLs[i])}</url>");
                        writer.WriteLine($"<tooltip>{EscapeTagContent(GalleryTab.imageTooltips[i])}</tooltip>");
                    }
                    writer.WriteLine("</gallery>");
                }
            }));
        }   


     

        // Helper function to escape special characters in tag content
        private string EscapeTagContent(string content)
        {
            return content.Replace("\\", "\\\\")   // Escape backslashes
                          .Replace("<", "\\<")     // Escape opening tags
                          .Replace(">", "\\>");    // Escape closing tags
        }

     
      
        public static void ClearOnLoad()
        {
            ClearUI();
        }
    }
}


