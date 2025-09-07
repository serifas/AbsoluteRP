using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using Networking;
using System.Numerics;
using System.Text.RegularExpressions;
using AbsoluteRP.Helpers;
using System.Runtime.CompilerServices;
using AbsoluteRP.Defines;

namespace AbsoluteRP.Windows.Profiles.ProfileTypeWindows
{
    
    //changed
    public class ProfileWindow : Window, IDisposable
    {
        private bool openVerifyPopup;
        public static string lodeStoneKey = string.Empty;
        public static bool lodeStoneKeyVerified;
        public static bool VerificationSucceeded { get; set; } = false;
        public static string LodeSUrl;
        public static string loading; 
        public static FileDialogManager _fileDialogManager; //for avatars only at the moment
        public Configuration configuration;
        public static IDalamudTextureWrap pictureTab; //picturetab.png for base picture in gallery
        public static SortedList<int, bool> CustomTabOpen = new SortedList<int, bool>();
        public static bool hasDrawException = false;
        public static Vector4 color = new Vector4(1, 1, 1, 1);
        public static bool addProfile, editProfile;
        public static string oocInfo = string.Empty;
        public static bool ExistingProfile = false;
        public static IDalamudTextureWrap backgroundImage; //background image for the profile window
        public static float loaderInd =  -1; //used for the gallery loading bar
        public static IDalamudTextureWrap avatarHolder;
        public static byte[] avatarBytes;
        public static byte[] backgroundBytes;
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
        public static bool showTypeCreation;
        public static string ProfileTitle = string.Empty;
        public static int profileIndex = 0;
        public static float inputWidth = 500;
        public static IDalamudTextureWrap currentAvatarImg;
        public static List<ProfileData> profiles = new List<ProfileData>();
        public static ProfileData CurrentProfile = new ProfileData();
        public (string, string) profileType = UI.ListingCategoryVals[(int)ProfileTypes.Personal];
        public (string, string, Type) layoutType = UI.LayoutTypeVals[(int)LayoutTypes.Relationship];
        public static int currentLayoutType = 0;
        private const int MaxTabs = 10; // Maximum number of tabs
        private bool[] showInputPopup = new bool[MaxTabs]; // Array of flags to show the input popups for new tabs
        private string[] newTabNames = new string[MaxTabs];
        private int customTabsCount = 0; // Current number of tabs
        private int tabToDeleteIndex = -1; // Index of the tab to delete
        private bool showDeleteConfirmationPopup = false; // Flag to show delete confirmation popup
        public static int currentElementID = 0;
        public static bool customTabSelected = false;
        public static bool Locked = false;
        public static CustomLayout currentLayout;
        public static List<CustomLayout> customLayouts = new List<CustomLayout>(); 
        public static ProfileWindow profileWindow;
        private List<int> tabOrder = new List<int>();         // Current order (updated by user)
        private List<int> initialTabOrder = new List<int>();  // Original order (for comparison)
        private bool showReorderTabsPopup;
        public static InventoryLayout currentInventory = null;
        public static bool AddInputTextElement { get; private set; }
        public static bool AddInputTextMultilineElement { get; private set; }
        public static bool AddInputImageElement { get; private set; }
        public static bool editBackground { get; private set; }
        public static bool Sending { get; set; } = false;
        public static bool VerificationFailed { get; internal set; } = false;

        public static bool editAvatar = false;
        private static int currentProfileType;
        public static string NewProfileTitle = string.Empty;
        public static bool Fetching = false;
        public static bool checking;

        public ProfileWindow() : base(
       "PROFILE", ImGuiWindowFlags.None)
        {
            SizeConstraints = new WindowSizeConstraints
            {

                MinimumSize = new Vector2(300, 300),
                MaximumSize = new Vector2(600, 1000)
            };

            configuration = Plugin.plugin.Configuration;
            _fileDialogManager = new FileDialogManager();
            profileWindow = this; 
        }
        public override void OnOpen()
        {
            try
            {
                for (int i = 0; i < MaxTabs; i++)
                {
                    showInputPopup[i] = false;
                }
                profiles.Clear();

                if (currentAvatarImg == null)
                {
                    // Try to recover by setting a default image
                    currentAvatarImg = UI.UICommonImage(UI.CommonImageTypes.avatarHolder) ?? pictureTab;
                
                    if(avatarHolder == null)
                    {
                        avatarHolder = currentAvatarImg;
                    }
                }
                //same for pictureTab
                var pictureTabImage = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                if (pictureTabImage != null)
                {
                    if (pictureTab == null)
                        pictureTab = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                }
                CurrentProfile.GalleryLayouts.Clear();
                for(int i = 0; i < profiles.Count; i++)
                {
                    if (profiles[i].isActive == true && !profiles[i].isPrivate)
                    {
                        foreach (var tab in profiles[i].customTabs)
                        {
                            if (tab.Layout is InventoryLayout inventory)
                            {
                                currentInventory = inventory; // Set the current inventory layout if it exists
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("ProfileWindow OnOpen Debug: " + ex.Message);
            }
        }
        //method to check if we have loaded our data received from the server
      
      

        public bool CheckIfNull()
        {
            bool allNotNull = true;
            var nullFields = new List<string>();

            // Instance fields
            if (Plugin.plugin == null) { nullFields.Add(nameof(Plugin.plugin)); allNotNull = false; }
            if (configuration == null) { nullFields.Add(nameof(configuration)); allNotNull = false; }

            // Static fields
            if (_fileDialogManager == null) { nullFields.Add(nameof(_fileDialogManager)); allNotNull = false; }
            if (pictureTab == null) { nullFields.Add(nameof(pictureTab)); allNotNull = false; }
            if (CustomTabOpen == null) { nullFields.Add(nameof(CustomTabOpen)); allNotNull = false; }
            if (avatarHolder == null) { nullFields.Add(nameof(avatarHolder)); allNotNull = false; }
            if (currentAvatarImg == null) { nullFields.Add(nameof(currentAvatarImg)); allNotNull = false; }
            if (profiles == null) { nullFields.Add(nameof(profiles)); allNotNull = false; }
            if (CurrentProfile == null) { nullFields.Add(nameof(CurrentProfile)); allNotNull = false; }
            if (profileWindow == null) { nullFields.Add(nameof(profileWindow)); allNotNull = false; }

            // Print any null fields
            if (nullFields.Count > 0)
            {
                Plugin.PluginLog.Debug("Null ProfileWindow fields: " + string.Join(", ", nullFields));
            }

            return allNotNull;
        }
        public override void Draw()
        {
            try
            {
                Character character = Plugin.plugin.Configuration.characters.FirstOrDefault(x => x.characterName == Plugin.plugin.playername && x.characterWorld == Plugin.plugin.playerworld);
                if (character == null)
                {
                    ImGui.Text("You must verify your character before accessing a profile");
                    if (ImGui.Button("Verify Character"))
                    {
                        openVerifyPopup = true;
                        ImGui.OpenPopup("Verify Character");
                    }

                    // Popup logic
                    if (openVerifyPopup)
                    {
                        ImGui.OpenPopup("Verify Character");
                        if (ImGui.BeginPopupModal("Verify Character", ref openVerifyPopup, ImGuiWindowFlags.AlwaysAutoResize))
                        {
                            ImGui.Text("Please insert the current character's Lodestone url");

                            ImGui.InputTextWithHint("##LodestoneURL", "Lodestone URL", ref LodeSUrl);

                            if (lodeStoneKey == string.Empty)
                            {
                                
                                if (ImGui.Button("Submit", new Vector2(120, 0)))
                                {       
                                    if(LodeSUrl != string.Empty)
                                    {
                                        DataSender.SubmitLodestoneURL(LodeSUrl, Plugin.plugin.Configuration.account.accountKey, false);
                                    }
                                    
                                }
                            }
                            if (lodeStoneKey != string.Empty)
                            {
                                ImGui.Text("Please add this key to your character profile");
                                ImGui.Text(lodeStoneKey);
                                ImGui.SameLine();
                                if (ImGui.Button("Copy"))
                                {
                                    ImGui.SetClipboardText(lodeStoneKey);
                                }
                                if (VerificationSucceeded)
                                {

                                    DataSender.FetchProfiles(character);
                                    DataSender.FetchProfile(Plugin.character, true, 0, Plugin.character.characterName, Plugin.character.characterWorld, -1);
                                }
                                else
                                {
                                    if(ImGui.Button("Request New Key"))
                                    {
                                        if(LodeSUrl != string.Empty)
                                        {
                                            DataSender.SubmitLodestoneURL(LodeSUrl, Plugin.plugin.Configuration.account.accountKey, false);
                                        }
                                    }
                                    // ... inside your popup logic
                                    using (ImRaii.Disabled(checking))
                                    {
                                        // Only allow pressing the button if not already checking
                                        if (ImGui.Button("Verify Lodestone") && !checking)
                                        {
                                            checking = true;
                                            if (LodeSUrl != string.Empty)
                                            {
                                                DataSender.CheckLodestoneEntry(LodeSUrl, false);
                                                // Set checking = false in your callback/response handler, not here!
                                            }
                                        }
                                    }
                                }
                                if (VerificationFailed)
                                {
                                    ImGui.TextColored(new Vector4(1, 0, 0, 1), "Verification Failed");
                                }
                            }
                            ImGui.SameLine();
                            if (ImGui.Button("Cancel", new Vector2(120, 0)))
                            {
                                ImGui.CloseCurrentPopup();
                                openVerifyPopup = false;
                            }

                            ImGui.EndPopup();
                        }
                    }
                }else{
                    bool tabsLoading = DataReceiver.loadedTabsCount < DataReceiver.tabsCount;
                    bool galleryLoading = DataReceiver.loadedGalleryImages < DataReceiver.GalleryImagesToLoad;
                    if (tabsLoading || galleryLoading)
                    {
                        if (tabsLoading)
                            Misc.StartLoader(DataReceiver.loadedTabsCount, DataReceiver.tabsCount, $"Loading Profile Tabs {DataReceiver.loadedTabsCount + 1}", ImGui.GetWindowSize(), "tabs");
                        if (galleryLoading)
                            Misc.StartLoader(DataReceiver.loadedGalleryImages, DataReceiver.GalleryImagesToLoad, $"Loading Gallery Images {DataReceiver.loadedGalleryImages + 1}", ImGui.GetWindowSize(), "gallery");
                        return;

                    }

                    // Block further UI until all tweens are finished
                    if ((tabsLoading && Misc.IsLoaderTweening("tabs")) ||
                        (galleryLoading && Misc.IsLoaderTweening("gallery")))
                    {
                        return;
                    }
                    if (Sending)
                    {
                        Misc.SetTitle(Plugin.plugin, true, "Sending Data", new Vector4(1, 1, 0, 1));   
                        return; // Skip drawing the rest of the window while sending data
                    }
                    if (Fetching)
                    {
                        Misc.SetTitle(Plugin.plugin, true, "Fetching Data", new Vector4(1, 1, 0, 1));
                        return; // Skip drawing the rest of the window while fetching data
                    }
                    else
                    {
                        if (Locked)
                        {
                            this.Flags = ImGuiWindowFlags.NoMove;
                        }
                        else
                        {
                            this.Flags = ImGuiWindowFlags.None;
                        }
                        DataReceiver.tabsCount = 0;
                        DataReceiver.loadedTabsCount = 0;
                        // Early out: show loader and skip all checks if still loading                  

                        // Fallback initialization for static fields (runs only after loading is done)
                        if (pictureTab == null)
                            pictureTab = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);

                        if (avatarHolder == null)
                            avatarHolder = UI.UICommonImage(UI.CommonImageTypes.avatarHolder);

                        if (currentAvatarImg == null)
                            currentAvatarImg = pictureTab ?? avatarHolder;

                        // Only proceed if all required fields are now initialized
                        if (!CheckIfNull())
                        {
                            ImGui.Text("Profile window is still loading...");
                            return;
                        }

                        if (Locked)
                        {
                            this.Flags = ImGuiWindowFlags.NoMove;
                        }
                        else
                        {
                            this.Flags = ImGuiWindowFlags.None;
                        }

                        if (Plugin.plugin.IsOnline())
                        {
                            _fileDialogManager.Draw();

                            ImGui.SameLine();
                            if (ImGui.Button("Add Profile"))
                            {
                                showTypeCreation = true;
                            }
                            if (profiles.Count > 0 && ExistingProfile == true)
                            {
                                AddProfileSelection();
                                ImGui.SameLine();
                                if (ImGui.Button("Preview Profile"))
                                {
                                    TargetProfileWindow.RequestingProfile = true;
                                    TargetProfileWindow.ResetAllData();
                                    Plugin.plugin.OpenTargetWindow();
                                    DataSender.FetchProfile(Plugin.character, false, CurrentProfile.index, Plugin.plugin.playername, Plugin.plugin.playerworld, -1);
                                }
                                DrawProfile();
                            }
                            if (profiles.Count <= 0)
                            {
                                ExistingProfile = false;
                            }
                            else
                            {
                                ExistingProfile = true;
                            }
                            if (showTypeCreation == true)
                            {
                                ImGui.OpenPopup($"Profile Creation##{profiles.Count}");
                                RenderProfileTypeCreation(profiles.Count);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if(hasDrawException == false) // Prevent spamming the log with the same Debug  
                {
                    hasDrawException = true;
                    Plugin.PluginLog.Debug("ProfileWindow Draw Debug: " + ex.Message);
                }
            }
        }
        public static void CreateProfile()
        {
            DataSender.CreateProfile(Plugin.character, NewProfileTitle, currentProfileType, profiles.Count);
            profileIndex = profiles.Count;
            Plugin.PluginLog.Debug(profileIndex.ToString());
            DataSender.FetchProfiles(Plugin.character);
            DataSender.FetchProfile(Plugin.character, true, profileIndex, Plugin.plugin.playername, Plugin.plugin.playerworld, -1);
            ExistingProfile = true;
        }
        private void RenderProfileTypeCreation(int index)
        {
            try
            {
                if (showTypeCreation && ImGui.BeginPopupModal($"Profile Creation##{index}", ref showTypeCreation, ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGui.Text($"Profile Type:");
                    ImGui.SameLine();
                    DrawProfileTypeSelection();
                    ImGui.Spacing();
                    ImGui.Text("Profile Title:");
                    ImGui.SameLine();
                    ImGui.InputText("##ProfileTitle", ref NewProfileTitle, 50);
                    // Confirm button
                    if (ImGui.Button("Create"))
                    {
                        CreateProfile();
                        showTypeCreation = false; // Close the popup
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.SameLine();

                    // Cancel button
                    if (ImGui.Button("Cancel"))
                    {
                        showTypeCreation = false; // Close the popup without deleting
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.EndPopup();
                }
            }catch(Exception ex)
            {
                Plugin.PluginLog.Debug("ProfileWindow RenderProfileTypeCreation Debug: " + ex.Message);
            }
        }
        public void DrawProfile()
        {
            // Defensive checks to prevent null reference spam
            if (profiles == null || profiles.Count == 0 || profileIndex < 0 || profileIndex >= profiles.Count)
            {
                Plugin.PluginLog.Debug("DrawProfile: Profiles not loaded or profileIndex out of range.");
                return;
            }
            if (CurrentProfile == null)
            {
                Plugin.PluginLog.Debug("DrawProfile: CurrentProfile is null.");
                return;
            }
            if (CurrentProfile.customTabs == null)
            {
                Plugin.PluginLog.Debug("DrawProfile: CurrentProfile.customTabs is null.");
                return;
            }
            if (currentAvatarImg == null)
            {
                // Try to recover by setting a default image
                currentAvatarImg = pictureTab ?? UI.UICommonImage(UI.CommonImageTypes.avatarHolder);
                if (currentAvatarImg == null)
                {
                    Plugin.PluginLog.Debug("DrawProfile: currentAvatarImg is still null after fallback. Skipping draw.");
                    return;
                }
            }  
              
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
                SubmitProfileData(false);
            }
            ImGui.SameLine();
            using (ImRaii.Disabled(!Plugin.CtrlPressed()))
            {
                if (ImGui.Button("Delete Profile"))
                {
                    DataSender.DeleteProfile(Plugin.character, profileIndex);
                    profileIndex -= 1;
                    if (profileIndex < 0)
                    {
                        profileIndex = 0;
                    }
                    TargetProfileWindow.ResetAllData();
                    DataSender.FetchProfiles(Plugin.character);
                    DataSender.FetchProfile(Plugin.character, true, profileIndex, Plugin.plugin.playername, Plugin.plugin.playerworld, -1);
                    if (profiles.Count == 0)
                    {
                        ExistingProfile = false;
                    }
                }
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Hold Ctrl to delete your profile (This is a destructive action!)");
            }
            
            if (ImGui.Button("Backup"))
            {
                  LoadBackupSaveDialog();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Save a local backup of your profile.");
            }
            ImGui.SameLine();
            if (ImGui.Button("Load Backup"))
            {
                LoadBackupLoaderDialog();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Loading a backup can completely replace this profile, if you do not wish to overwrite it, please make a new profile to load on to.");
            }

            ImGui.Spacing();
            Vector2 imageStartPos = ImGui.GetCursorScreenPos();
            Vector2 windowPos = ImGui.GetWindowPos();
            Vector2 windowSize = ImGui.GetWindowSize();
            if (backgroundImage != null && backgroundImage.Handle != IntPtr.Zero)
            {
                var drawList = ImGui.GetWindowDrawList();
                float alpha = 0.5f; // 50% opacity
                uint tintColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, alpha));

                // Use the maximum window size and apply scaling
                float scale = ImGui.GetIO().FontGlobalScale;
                Vector2 scaledSize = backgroundImage.Size * scale;

                Vector2 imageEndPos = imageStartPos + scaledSize;

                drawList.AddImage(
                    backgroundImage.Handle,
                    imageStartPos,
                    imageEndPos,
                    new Vector2(0, 0),
                    new Vector2(1, 1),
                    tintColor
                );
            }
            if (currentAvatarImg == null || currentAvatarImg.Size == null)
            {
                Plugin.PluginLog.Debug("DrawProfile: currentAvatarImg or its Size is null. Skipping draw.");
                return;
            }
            Vector2 avatarSize = currentAvatarImg.Size * ImGui.GetIO().FontGlobalScale;
            float centeredX = (ImGui.GetContentRegionAvail().X - avatarSize.X) / 2;
            var avatarBtnSize = ImGui.CalcTextSize("Edit Avatar") + new Vector2(10, 10);
            float avatarXPos = (windowSize.X - avatarBtnSize.X) / 2;
            ImGui.SetCursorPosX(centeredX);
            if(currentAvatarImg != null && currentAvatarImg.Handle != IntPtr.Zero)
            {
                ImGui.Image(currentAvatarImg.Handle, avatarSize);
            }
            ImGui.SetCursorPosX(avatarXPos);
            if (ImGui.Button("Edit Avatar"))
            {
                editAvatar = true;
            }
            ImGui.Spacing();

            if (ProfileTitle.Length > 0)
            {
                Misc.SetTitle(Plugin.plugin, true, ProfileTitle, color);
            }
            Misc.DrawXCenteredInput("TITLE:", $"Title{profileIndex}", ref ProfileTitle, 50);
            ImGui.SameLine();
            ImGui.ColorEdit4($"##Text Input Color{profileIndex}", ref color, ImGuiColorEditFlags.NoInputs);


            var uploadBtnSize = ImGui.CalcTextSize("Set Background") + new Vector2(10, 10);
            float uploadXPos = (windowSize.X - uploadBtnSize.X) / 2;

            ImGui.SetCursorPosX(uploadXPos);
            if (ImGui.Button("Set Background"))
            {
                editBackground = true;
            }

            ImGui.Spacing();


            ImGui.TextColored(new Vector4(0.6f, 0.8f, 1.0f, 1.0f), "Format Information");

            // Make the text clickable
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(UI.inputHelperUrlInfo);
            }
            ImGui.Spacing();
            ImGui.Separator();
            using (var navigation = ImRaii.TabBar("ProfileNavigation"))
            {

                if (navigation)
                {
                    // Store overlay start position BEFORE rendering tab content
                    float overlayStartY = ImGui.GetCursorScreenPos().Y;
                    Vector2 overlayStart = new Vector2(windowPos.X, overlayStartY);
                    Vector2 overlayEnd = new Vector2(windowPos.X + windowSize.X, windowPos.Y + windowSize.Y);

                    var drawList = ImGui.GetWindowDrawList();
                    uint overlayColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.5f));
                    drawList.AddRectFilled(overlayStart, overlayEnd, overlayColor);

                    // Now render tab content
                    RenderCustomTabs();
                    if (CurrentProfile.customTabs.Count < MaxTabs)
                    {
                        if (ImGui.TabItemButton("  +  ##AddTab", ImGuiTabItemFlags.NoCloseWithMiddleMouseButton))
                        {
                            showInputPopup[customTabsCount] = true;
                            newTabNames[customTabsCount] = "";
                            ImGui.OpenPopup($"New Page##{customTabsCount}");
                        }
                    }
                    /*
                    // "Reorder Tabs" button at the end of the tab bar
                    ImGui.SameLine();
                    if (ImGui.Button("Reorder Tabs"))
                    {
                        // Initialize tabOrder with current order
                        tabOrder = Enumerable.Range(0, CurrentProfile.customTabs.Count).ToList();
                        showReorderTabsPopup = true;
                        ImGui.OpenPopup("ReorderTabsPopup");
                    }*/

                    // Popup for reordering tabs with Up/Down buttons
                    if (showReorderTabsPopup)
                    {
                        if (ImGui.BeginPopupModal("ReorderTabsPopup", ref showReorderTabsPopup, ImGuiWindowFlags.AlwaysAutoResize))
                        {
                            ImGui.Text("Use Up/Down to reorder tabs:");
                            ImGui.Separator();

                            for (int i = 0; i < tabOrder.Count; i++)
                            {
                                int tabIdx = tabOrder[i];
                                var tab = CurrentProfile.customTabs[tabIdx];
                                ImGui.PushID(i);

                                ImGui.Text(tab.Name);
                                ImGui.SameLine();

                                // Up button
                                if (ImGui.Button("Up") && i > 0)
                                {
                                    (tabOrder[i - 1], tabOrder[i]) = (tabOrder[i], tabOrder[i - 1]);
                                }
                                ImGui.SameLine();

                                // Down button
                                if (ImGui.Button("Down") && i < tabOrder.Count - 1)
                                {
                                    (tabOrder[i], tabOrder[i + 1]) = (tabOrder[i + 1], tabOrder[i]);
                                }

                                ImGui.PopID();
                            }

                            ImGui.Separator();
                            if (ImGui.Button("Confirm"))
                            {
                                // Build list of changes
                                var indexChanges = new List<(int oldIndex, int newIndex)>();
                                for (int newIdx = 0; newIdx < tabOrder.Count; newIdx++)
                                {
                                    int tabId = tabOrder[newIdx];
                                    int oldIdx = initialTabOrder.IndexOf(tabId);
                                    if (oldIdx != newIdx)
                                        indexChanges.Add((oldIdx, newIdx));
                                }
                                DataSender.SendTabReorder(Plugin.character, profileIndex, indexChanges);

                                // Actually reorder the tabs in the UI
                                var newTabs = tabOrder.Select(idx => CurrentProfile.customTabs[idx]).ToList();
                                CurrentProfile.customTabs.Clear();
                                CurrentProfile.customTabs.AddRange(newTabs);

                                showReorderTabsPopup = false;
                                ImGui.CloseCurrentPopup();
                            }
                            ImGui.SameLine();
                            if (ImGui.Button("Cancel"))
                            {
                                showReorderTabsPopup = false;
                                ImGui.CloseCurrentPopup();
                            }
                            ImGui.EndPopup();
                        }
                    }
                }

                if (Gallery.loadPreview == true)
                {
                    //load gallery image preview if requested
                    Plugin.plugin.OpenImagePreview();
                    Gallery.loadPreview = false;
                }
                if (Gallery.addGalleryImageGUI == true)
                {
                    // Gallery.galleryImageCount = CurrentProfile?.GalleryLayouts?.Count ?? 0;

                    // Gallery.AddImagesToGallery(plugin, CurrentProfile); //used to add our image to the gallery
                }
                 
                if (editAvatar == true)
                {
                    editAvatar = false;
                    Misc.EditImage(Plugin.plugin, _fileDialogManager, null, true, false, 0);
                }

                if (editBackground == true)
                {
                    editBackground = false;
                    Misc.EditImage(Plugin.plugin, _fileDialogManager, null, false, true, 0);
                }
            }      

        }
     
        public void RenderCustomTabs()
        {
            try
            {
                // Render all active popups for new tab creation
                for (int i = 0; i < MaxTabs; i++)
                {
                    if (showInputPopup[i])
                    {
                        string tabName = newTabNames[i];
                        bool showPopup = showInputPopup[i];
                        using (var pageBtn = ImRaii.PopupModal($"New Page##{i}", ref showPopup, ImGuiWindowFlags.AlwaysAutoResize))
                        {
                            if (pageBtn)
                            {
                                try
                                {
                                    showInputPopup[i] = showPopup;
                                    ImGui.Text($"Enter the name for the page:");
                                    ImGui.InputText($"##TabInput{i}", ref newTabNames[i], 100);
                                    DrawLayoutTypeSelection();
                                    ImGui.TextColored(new Vector4(1, 0, 0, 1), "Please save your content before creating new tabs.\nThis will remove your current unsaved data.");
                                    var io = ImGui.GetIO();
                                    if ((ImGui.Button("Submit") || io.KeysDown[(int)ImGuiKey.Enter]) && !string.IsNullOrWhiteSpace(newTabNames[i]))
                                    {
                                        showInputPopup[i] = false;
                                        customTabsCount = CurrentProfile.customTabs.Count;

                                        // Instantiate the correct layout type
                                        CustomLayout layout = null;
                                        if (currentLayoutType == (int)LayoutTypes.Bio)
                                            layout = new BioLayout { tabIndex = customTabsCount };
                                        else if (currentLayoutType == (int)LayoutTypes.Details)
                                            layout = new DetailsLayout { tabIndex = customTabsCount };
                                        else if (currentLayoutType == (int)LayoutTypes.Gallery)
                                            layout = new GalleryLayout { tabIndex = customTabsCount };
                                        else if (currentLayoutType == (int)LayoutTypes.Info)
                                            layout = new InfoLayout { tabIndex = customTabsCount };
                                        else if (currentLayoutType == (int)LayoutTypes.Story)
                                            layout = new StoryLayout { tabIndex = customTabsCount };
                                        else if (currentLayoutType == (int)LayoutTypes.Inventory)
                                            layout = new InventoryLayout { tabIndex = customTabsCount };
                                        else if (currentLayoutType == (int)LayoutTypes.Relationship)
                                            layout = new TreeLayout { tabIndex = customTabsCount };
                                        else
                                            Plugin.PluginLog.Debug($"Unknown layout type: {currentLayoutType}");



                                        CustomTab tab = new CustomTab
                                        {
                                            Name = tabName,
                                            Layout = layout,
                                            IsOpen = true,
                                            type = (int)layout.layoutType
                                        };
                                        DataSender.CreateTab(Plugin.character, newTabNames[i], currentLayoutType, CurrentProfile.index, customTabsCount + 1);
                                    }

                                    ImGui.SameLine();
                                    if (ImGui.Button("Cancel"))
                                    {
                                        showInputPopup[i] = false;
                                        ImGui.CloseCurrentPopup();
                                    }
                                }
                                catch (Exception e)
                                {
                                    Plugin.PluginLog.Debug(e.ToString());
                                }
                            }
                        }
                    }
                }

                // Render all tabs with unique keys (name + index)
                var renderedTabKeys = new HashSet<string>();
                for (int i = 0; i < CurrentProfile.customTabs.Count; i++)
                {
                    var tab = CurrentProfile.customTabs[i];
                    bool isOpen = tab.IsOpen;
                    RenderTab(tab.Layout, i, ref isOpen, tab.Name);
                }

                // Render the delete confirmation popup
                if (showDeleteConfirmationPopup)
                {
                    using (var confirmation = ImRaii.PopupModal("Delete Tab Confirmation", ref showDeleteConfirmationPopup, ImGuiWindowFlags.AlwaysAutoResize))
                    {
                        if (confirmation)
                        {
                            if (tabToDeleteIndex >= 0 && tabToDeleteIndex < CurrentProfile.customTabs.Count)
                            {
                                ImGui.Text($"Are you sure you want to delete the tab \"{CurrentProfile.customTabs[tabToDeleteIndex].Name}\"?");
                            }
                            else
                            {
                                ImGui.Text("Tab not found.");
                            }
                            ImGui.Spacing();
                            using (ImRaii.Disabled(!Plugin.CtrlPressed()))
                            {
                                if (ImGui.Button("Confirm"))
                                {
                                    // Remove the tab immediately from the UI
                                    if (tabToDeleteIndex >= 0 && tabToDeleteIndex < CurrentProfile.customTabs.Count)
                                        CurrentProfile.customTabs.RemoveAt(tabToDeleteIndex);

                                    // Send delete request to server (optional, for persistence)
                                    DataSender.DeleteTab(
                                        Plugin.character,
                                        CurrentProfile.index,
                                        tabToDeleteIndex + 1,
                                        (tabToDeleteIndex >= 0 && tabToDeleteIndex < CurrentProfile.customTabs.Count)
                                            ? CurrentProfile.customTabs[tabToDeleteIndex].type
                                            : 0
                                    );

                                    customTabsCount = CurrentProfile.customTabs.Count;
                                    for (int j = showInputPopup.Length; j < MaxTabs; j++)
                                        showInputPopup[j] = false;

                                    showDeleteConfirmationPopup = false;
                                    tabToDeleteIndex = -1;

                                    ImGui.CloseCurrentPopup();
                                }
                            }
                            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            {
                                ImGui.SetTooltip("Hold Ctrl to delete the selected tab. This action cannot be undone.");
                            }
                            ImGui.SameLine();
                            if (ImGui.Button("Cancel"))
                            {
                                showDeleteConfirmationPopup = false;
                                tabToDeleteIndex = -1;
                                ImGui.CloseCurrentPopup();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("ProfileWindow RenderCustomTabs Debug: " + ex.Message);
                loading = "An Debug occurred while rendering custom tabs.";
            }
        }

        private void RenderTab(CustomLayout layout, int index, ref bool isOpen, string tabName)
        {
            try
            {
                string uniqueId = $"{tabName}##{index}";
                // Render the tab with a close button
                using (var tab = ImRaii.TabItem(uniqueId, ref isOpen))
                {
                    if (tab)
                    {
                        customTabSelected = true;
                        if(layout != null)
                        {
                            switch (layout){

                                case BioLayout bioLayout:
                                    Bio.RenderBioLayout(index, uniqueId, bioLayout);
                                    break;
                                case DetailsLayout detailsLayout:
                                    Details.RenderDetailsLayout(index, uniqueId, detailsLayout);
                                    break;
                                case GalleryLayout galleryLayout:
                                    Gallery.RenderGalleryLayout(index, uniqueId, galleryLayout);
                                    break;
                                case InfoLayout infoLayout:
                                    Info.RenderInfoLayout(index, uniqueId, infoLayout);
                                    break;
                                case StoryLayout storyLayout:
                                    Story.RenderStoryLayout(index, uniqueId, storyLayout);
                                    break;
                                case InventoryLayout inventroyLayout:
                                    Inventory.RenderInventoryLayout(index, uniqueId, inventroyLayout);
                                    break;
                                case TreeLayout treeLayout:                                    
                                    Tree.RenderTreeLayout(index, true, uniqueId, treeLayout, string.Empty, new Vector4(0,0,0,0));
                                    break;
                                default:
                                    break;
                            }
                        }

                    }

                }

                // Handle the case where the tab is closed (close button pressed)
                if (!isOpen)
                {
                    isOpen = true; // Reopen the tab temporarily to show the confirmation popup
                    tabToDeleteIndex = index; // Store the index of the tab to delete
                    showDeleteConfirmationPopup = true; // Show the delete confirmation popup
                    ImGui.OpenPopup("Delete Tab Confirmation");
                }
            }catch(Exception ex)
            {
                Plugin.PluginLog.Debug($"ProfileWindow RenderTab Debug: {ex.Message}");
            }
        }



        private bool ProfileHasContent()
        {
            
            if(oocInfo != string.Empty || Story.storyTitle != string.Empty) { return true; }

            for (int i = 0; i < Story.ChapterNames.Length; i++)
            {
                if(Story.ChapterNames[i] != string.Empty) return true;

            }
            for (int i = 0; i < Story.ChapterContents.Length; i++)
            {
                if (Story.ChapterContents[i] != string.Empty)
                {
                    return true;
                }
            }

            if (CurrentProfile != null && CurrentProfile.GalleryLayouts?.Count > 0)
            {
                return true;
            }
            return false;
        }

     
       



      
        //method ot reset the entire story section
     

        //reset our tabs and go back to base ui with no tab selected

      
        public void Dispose()
        {
            WindowOperations.SafeDispose(currentAvatarImg);
            currentAvatarImg = null;
            WindowOperations.SafeDispose(pictureTab);
            pictureTab = null;
            WindowOperations.SafeDispose(backgroundImage);
            backgroundImage = null;
            WindowOperations.SafeDispose(avatarHolder);
            avatarHolder = null;

            // Dispose and dereference custom layouts
            if (customLayouts != null)
            {
                foreach (var layout in customLayouts)
                {
                    switch (layout)
                    {
                        case GalleryLayout gallery:
                            if (gallery.images != null)
                            {
                                foreach (var img in gallery.images)
                                {
                                    WindowOperations.SafeDispose(img.image);
                                    img.image = null;
                                    WindowOperations.SafeDispose(img.thumbnail);
                                    img.thumbnail = null;
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
                                    item.iconTexture = null;
                                }
                                inventory.inventorySlotContents.Clear();
                            }
                            break;

                        case BioLayout bio:
                            if (bio.traits != null)
                            {
                                foreach (var trait in bio.traits)
                                {
                                    WindowOperations.SafeDispose(trait.icon?.icon);
                                    trait.icon.icon = null;
                                    trait.icon = null;
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

                        case DynamicLayout dynamicLayout:
                            dynamicLayout.elements?.Clear();
                            dynamicLayout.RootNode = null;
                            break;
                    }
                }
                customLayouts.Clear();
            }

            // Dispose and dereference inventory
            currentInventory = null;

            // Clear profile data
            CurrentProfile = null;
            profiles?.Clear();
        }

        private void DrawProfileTypeSelection()
        {
            try
            {
                using var combo = ImRaii.Combo("##Profiles", profileType.Item1);
                if (!combo)
                    return;
                foreach (var ((name, description), idx) in UI.ListingCategoryVals.WithIndex())
                {
                    if (ImGui.Selectable(name + "##" + idx, idx == currentProfileType))
                    {
                        profileType = UI.ListingCategoryVals[idx];
                        currentProfileType = idx;
                    }
                    UIHelpers.SelectableHelpMarker(description);
                }
            }catch(Exception ex)
            {
                Plugin.PluginLog.Debug("ProfileWindow DrawProfileTypeSelection Debug: " + ex.Message);
            }
        }
        private void DrawLayoutTypeSelection()
        {
            try { 
            using var combo = ImRaii.Combo("##LayoutTypes", layoutType.Item1);
            if (!combo)
                return;
                foreach (var ((name, description, Type), idx) in UI.LayoutTypeVals.WithIndex())
                {
                    if(name != "Roster")
                    {
                        if (ImGui.Selectable(name + "##" + idx, idx == currentLayoutType))
                        {
                            layoutType = UI.LayoutTypeVals[idx];
                            currentLayoutType = idx;
                        }
                        UIHelpers.SelectableHelpMarker(description);
                    }
                }
            }
            catch(Exception ex)
            {
                Plugin.PluginLog.Debug("ProfileWindow DrawLayoutTypeSelection Debug: " + ex.Message);
            }
        }



        public void AddProfileSelection()
        {
            try
            {
                List<string> profileNames = new List<string>();
                for (int i = 0; i < profiles.Count; i++)
                {
                    profileNames.Add(profiles[i].Name);
                }
                string[] ProfileNames = new string[profileNames.Count];
                ProfileNames = profileNames.ToArray();
                var profileName = ProfileNames[profileIndex];

                using var combo = ImRaii.Combo("##Profiles", profileName);
                if (!combo)
                    return;
                foreach (var (newText, idx) in ProfileNames.WithIndex())
                {
                    if (profiles.Count > 0)
                    {
                        var label = newText;
                        if (label == string.Empty)
                        {
                            label = "New Profile";
                        }
                        if (newText != string.Empty)
                        {
                            if (ImGui.Selectable(label + "##" + idx, idx == profileIndex))
                            {
                                CurrentProfile = profiles[idx];
                                currentAvatarImg = UI.UICommonImage(UI.CommonImageTypes.avatarHolder);
                                Bio.currentAlignment = 9;
                                profileIndex = idx;
                                TargetProfileWindow.ResetAllData();
                                Fetching = true;
                                DataSender.FetchProfiles(Plugin.character);
                                DataSender.FetchProfile(Plugin.character, true, profileIndex, Plugin.plugin.playername, Plugin.plugin.playerworld, -1);
                            }
                            UIHelpers.SelectableHelpMarker("Select to edit profile");
                        }

                        if (showTypeCreation == true)
                        {
                            ImGui.OpenPopup($"Profile Creation##{profiles.Count}");
                            RenderProfileTypeCreation(profiles.Count);
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("ProfileWindow AddProfileSelection Debug: " + ex.Message);
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
                Plugin.PluginLog.Debug($"Debug reading file: {ex.Message}");
                return string.Empty; // Return empty if an Debug occurs
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
        public void LoadBackupLoaderDialog()
        {
            _fileDialogManager.OpenFileDialog(
                "Load Backup",
                "Data{.dat, .json, .txt}",
                (bool success, string filePath) =>
                {
                    if (!success || string.IsNullOrEmpty(filePath))
                        return;

                    try
                    {
                        string fileContent = File.ReadAllTextAsync(filePath).GetAwaiter().GetResult();
                        // Clear current tabs before loading
                        CurrentProfile.customTabs.Clear();
                        customLayouts.Clear();

                        // Regex to find all tab-like tags in order
                        var tagRegex = new Regex(@"<(?<tag>\w+Tab)>(?<content>.*?)</\k<tag>>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        string avatarBase64 = ExtractTagContent(fileContent, "avatar");
                        string backgroundBase64 = ExtractTagContent(fileContent, "background");
                        string title = ExtractTagContent(fileContent, "title");
                        string titleColorStr = ExtractTagContent(fileContent, "titleColor");
                        if (!string.IsNullOrEmpty(avatarBase64))
                            avatarBytes = Convert.FromBase64String(avatarBase64);

                        if (!string.IsNullOrEmpty(backgroundBase64))
                            backgroundBytes = Convert.FromBase64String(backgroundBase64);

                        // Set title
                        ProfileTitle = title;
                        CurrentProfile.title = title;

                        // Parse and set title color
                        if (!string.IsNullOrEmpty(titleColorStr))
                        {
                            var colorParts = titleColorStr.Split(',');
                            if (colorParts.Length == 4 &&
                                float.TryParse(colorParts[0], out float r) &&
                                float.TryParse(colorParts[1], out float g) &&
                                float.TryParse(colorParts[2], out float b) &&
                                float.TryParse(colorParts[3], out float a))
                            {
                                color = new Vector4(r, g, b, a);
                                CurrentProfile.titleColor = color;
                            }
                        }

                        var matches = tagRegex.Matches(fileContent);
                        foreach (Match match in matches)
                        {
                            string tag = match.Groups["tag"].Value;
                            string tabContent = match.Value;


                            switch (tag.ToLower())
                            {
                                case "biotab":
                                    LoadTabsFromBackup(tabContent, "bioTab", BackupLoader.LoadBioLayout).GetAwaiter().GetResult();
                                    break;
                                case "detailstab":
                                    LoadTabsFromBackup(tabContent, "detailsTab", BackupLoader.LoadDetailsLayout).GetAwaiter().GetResult();
                                    break;
                                case "gallerytab":
                                    LoadTabsFromBackup(tabContent, "galleryTab", BackupLoader.LoadGalleryLayout).GetAwaiter().GetResult();
                                    break;
                                case "infotab":
                                    LoadTabsFromBackup(tabContent, "infoTab", BackupLoader.LoadInfoLayout).GetAwaiter().GetResult();
                                    break;
                                case "storytab":
                                    LoadTabsFromBackup(tabContent, "storyTab", BackupLoader.LoadStoryLayout).GetAwaiter().GetResult();
                                    break;
                                case "inventorytab": // <-- Corrected
                                    LoadTabsFromBackup(tabContent, "inventoryTab", BackupLoader.LoadInventoryLayout).GetAwaiter().GetResult();
                                    break;
                                case "treetab": // <-- Corrected
                                    LoadTabsFromBackup(tabContent, "treeTab", BackupLoader.LoadTreeLayout).GetAwaiter().GetResult();
                                    break;
                                default:
                                    Plugin.PluginLog.Debug($"Unknown tab type in backup: {tag}");
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.PluginLog.Debug($"Debug loading backup file: {ex.Message}");
                    }
                    finally
                    {
                        SubmitProfileData(false);
                    }
                }
            );
        }
        // Helper to extract and load all tabs of a given type
        private async Task LoadTabsFromBackup(string fileContent, string tag, Func<string, CustomLayout> layoutLoader)
        {
            var matches = Regex.Matches(fileContent, $"<{tag}>(.*?)</{tag}>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                string tabContent = match.Groups[1].Value;
                var tabNameMatch = Regex.Match(tabContent, "<tabName>(.*?)</tabName>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                string tabName = tabNameMatch.Success ? tabNameMatch.Groups[1].Value.Trim() : tag;

                var tabIndexMatch = Regex.Match(tabContent, "<tabIndex>(.*?)</tabIndex>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                int tabIndex = tabIndexMatch.Success ? int.Parse(tabIndexMatch.Groups[1].Value.Trim()) : 0;

                int tabType = GetTabTypeFromTag(tag); // Implement this mapping

                var layout = layoutLoader(tabContent);
                layout.name = tabName;

                switch (layout)
                {
                    case BioLayout bioLayout: bioLayout.tabIndex = tabIndex; break;
                    case DetailsLayout detailsLayout: detailsLayout.tabIndex = tabIndex; break;
                    case GalleryLayout galleryLayout: galleryLayout.tabIndex = tabIndex; break;
                    case InfoLayout infoLayout: infoLayout.tabIndex = tabIndex; break;
                    case StoryLayout storyLayout: storyLayout.tabIndex = tabIndex; break;
                    case InventoryLayout inventoryLayout: inventoryLayout.tabIndex = tabIndex; break;
                    case TreeLayout treeLayout: treeLayout.tabIndex = tabIndex; break;
                }

                var tab = new CustomTab
                {
                    Name = tabName,
                    Layout = layout,
                    IsOpen = true,
                    type = tabType,   // <-- Set type
                };
                DataSender.CreateTab(Plugin.character, tab.Name, tab.type, profileIndex, tabIndex);
            }
        }
        private int GetTabTypeFromTag(string tag)
        {
            return tag.ToLower() switch
            {
                "biotab" => (int)LayoutTypes.Bio,
                "detailstab" => (int)LayoutTypes.Details,
                "gallerytab" => (int)LayoutTypes.Gallery,
                "infotab" => (int)LayoutTypes.Info,
                "storytab" => (int)LayoutTypes.Story,
                "inventorytab" => (int)LayoutTypes.Inventory,
                "treetab" => (int)LayoutTypes.Relationship,
                _ => 0
            };
        }
        public void SubmitProfileData(bool silent)
        {
            try
            {
                DataSender.SetProfileStatus(Plugin.character, isPrivate, activeProfile, profileIndex, ProfileTitle, color, avatarBytes, backgroundBytes, SpoilerARR, SpoilerHW, SpoilerSB, SpoilerSHB, SpoilerEW, SpoilerDT, NSFW, Triggering);
                Plugin.PluginLog.Debug($"Tabs count before submit: {CurrentProfile.customTabs.Count}");
                foreach (var tab in CurrentProfile.customTabs)
                {
                    Plugin.PluginLog.Debug($"Tab: {tab.Name}, Type: {tab.Layout?.GetType().Name}");
                }

                Sending = true;
                foreach (CustomTab tab in CurrentProfile.customTabs)
                {                    
                    if (tab.Layout is BioLayout bioLayout)
                    {
                        DataSender.SubmitProfileBio(Plugin.character, profileIndex, bioLayout);
                    }
                    if(tab.Layout is DetailsLayout detailsLayout)
                    {
                        DataSender.SubmitProfileDetails(Plugin.character, profileIndex, detailsLayout);
                    }
                    if(tab.Layout is GalleryLayout galleryLayout)
                    {
                        DataSender.SubmitGalleryLayout(Plugin.character, profileIndex, galleryLayout);
                    }
                    if(tab.Layout is InfoLayout infoLayout)
                    {
                        DataSender.SubmitInfoLayout(Plugin.character, profileIndex, infoLayout);
                    }
                    if(tab.Layout is StoryLayout storyLayout)
                    {
                        DataSender.SubmitStoryLayout(Plugin.character, profileIndex, storyLayout);
                    }
                    if(tab.Layout is InventoryLayout inventoryLayout)
                    {
                        DataSender.SubmitInventoryLayout(Plugin.character, profileIndex, inventoryLayout);    
                    }
                    if(tab.Layout is TreeLayout treeLayout)
                    {
                        DataSender.SubmitTreeLayout(Plugin.character, profileIndex, treeLayout);
                    }
                    //SaveBackupFile(configuration.dataSavePath);
                }          
            }
            catch(Exception ex)
            {
                Plugin.PluginLog.Debug("Received exception in SubmitProfileBio " + ex.Message);
            }
            finally

            {
                if (Plugin.plugin.Configuration.AutobackupEnabled)
                {
                    SaveBackupFile(configuration.dataSavePath).GetAwaiter().GetResult();
                }
                CurrentProfile.customTabs.Clear();
                customLayouts.Clear();
                DataSender.FetchProfile(Plugin.character, true, profileIndex, Plugin.plugin.playername, Plugin.plugin.playerworld, -1);
            }

        }
        public  void LoadBackupSaveDialog()
        {
            try
            {
                _fileDialogManager.SaveFileDialog("Save Backup", "Data{.dat, .json}", "Backup Name", ".dat", (Action<bool, string>)((s, f) =>
                {
                    if (!s)
                        return;
                    var dataPath = f.ToString();
                    SaveBackupFile(dataPath).GetAwaiter().GetResult();
                }));
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug Loading Backup Dialog: {ex.Message}");
            }
        }
        public async Task SaveBackupFile(string dataPath)
        {
            try
            {
                if (!Directory.Exists(dataPath))
                    Directory.CreateDirectory(dataPath);

                string dateTimeSuffix = DateTime.Now.ToString("yyyy-MM-dd_hh-mm-ss_tt");
                string fileNameBase = string.Concat(ProfileTitle.Split(Path.GetInvalidFileNameChars()));
                string fileName = $"{fileNameBase}_{dateTimeSuffix}.dat";
                fileName = string.Concat(fileName.Split(Path.GetInvalidFileNameChars()));
                string filePath = Path.Combine(dataPath, fileName);

                // Prevent writing if a directory exists with the same name
                if (Directory.Exists(filePath))
                {
                    Plugin.PluginLog.Debug($"Cannot overwrite: '{filePath}' is a directory.");
                    return;
                }

                using (var writer = new StreamWriter(filePath))
                {
                    writer.WriteLine("<characterProfile>");
                    writer.WriteLine($"<avatar>{Convert.ToBase64String(avatarBytes)}</avatar>");
                    writer.WriteLine($"<background>{Convert.ToBase64String(backgroundBytes)}</background>");
                    writer.WriteLine($"<title>{CurrentProfile.title}</title>");
                    writer.WriteLine($"<titleColor>{CurrentProfile.titleColor.X},{CurrentProfile.titleColor.Y}, {CurrentProfile.titleColor.Z},{CurrentProfile.titleColor.W}</titleColor>");
                    foreach (var customTab in CurrentProfile.customTabs)
                    {
                        BackupWriter.WriteTabContent(customTab, writer);
                    }
                    writer.WriteLine("</characterProfile>");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug saving backup file: {ex.Message}");
            }
        }

    }
}



