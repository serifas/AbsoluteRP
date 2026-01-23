using AbsoluteRP.Backups;
using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Networking;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AbsoluteRP.Windows.Profiles.ProfileTypeWindows
{
    
    //changed
    public class ProfileWindow : Window, IDisposable
    {
        private bool openVerifyPopup;
        public static string lodeStoneKey = string.Empty;
        public static bool lodeStoneKeyVerified;
        public static bool setFauxName = false;
        public static bool VerificationSucceeded { get; set; } = false;
        public static string LodeSUrl;
        public static string loading; 
        public static FileDialogManager _fileDialogManager; //for avatars only at the moment
        public Configuration configuration;
        public static IDalamudTextureWrap pictureTab; //picturetab.png for base picture in gallery
        public static SortedList<int, bool> CustomTabOpen = new SortedList<int, bool>();
        public static bool hasDrawException = false;
        public static bool addProfile, editProfile;
        public static string oocInfo = string.Empty;
        public static bool ExistingProfile = false;
        public static IDalamudTextureWrap backgroundImage; //background image for the tooltipData window
        public static float loaderInd =  -1; //used for the gallery loading bar
        public static IDalamudTextureWrap avatarHolder;
        public static bool showTypeCreation;
        public static int profileIndex = 0;
        public static float inputWidth = 500;
        public static IDalamudTextureWrap currentAvatarImg;
        public static List<ProfileData> profiles = new List<ProfileData>(); 
        private bool profileTypeCreationJustOpened = false;
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
        private static int currentProfileType = 5;
        public static string NewProfileTitle = string.Empty;
        public static bool Fetching = false;
        public static bool checking;
        internal static bool showOnCompass;

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
                CurrentProfile.customTabs.Clear();
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
                Defines.Character character = Plugin.plugin.Configuration.characters.FirstOrDefault(x => x.characterName == Plugin.plugin.playername && x.characterWorld == Plugin.plugin.playerworld);
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
                                ImGui.Text("Please add this key to your character lodestone");
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
                                    ImGui.SameLine();
                                    if (ImGui.Button("Start Over"))
                                    {
                                        // Reset all verification-related values
                                        lodeStoneKey = string.Empty;
                                        LodeSUrl = string.Empty;
                                        lodeStoneKeyVerified = false;
                                        VerificationSucceeded = false;
                                        VerificationFailed = false;
                                        checking = false;
                                    }
                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip("Reset verification to start fresh with a new key");
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

                        if (Plugin.IsOnline())
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
                                    DataSender.FetchProfile(Plugin.character, false, profileIndex, Plugin.character.characterName, Plugin.character.characterWorld, -1);
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
            DataSender.CreateProfile(Plugin.character, NewProfileTitle, currentProfileType + 1, profiles.Count);
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
                // Only set the default when the popup is first opened
                if (showTypeCreation && !profileTypeCreationJustOpened)
                {
                    currentProfileType = 5;
                    profileType = UI.ListingCategoryVals[currentProfileType];
                    profileTypeCreationJustOpened = true;
                }

                if (showTypeCreation && ImGui.BeginPopupModal($"Profile Creation##{index}", ref showTypeCreation, ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGui.Text($"Profile Type:");
                    ImGui.SameLine();
                    DrawProfileTypeSelection();
                    ImGui.Spacing();
                    ImGui.Text("Profile Title:");
                    ImGui.SameLine();
                    ImGui.InputText("##ProfileTitle", ref NewProfileTitle, 50);

                    if (ImGui.Button("Create"))
                    {
                        CreateProfile();
                        showTypeCreation = false;
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.SameLine();

                    if (ImGui.Button("Cancel"))
                    {
                        showTypeCreation = false;
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.EndPopup();
                }
                else if (!showTypeCreation)
                {
                    // Reset the flag when the popup is closed
                    profileTypeCreationJustOpened = false;
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("ProfileWindow RenderProfileTypeCreation Debug: " + ex.Message);
            }
        }
        public void DrawProfile()
        {
           
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
            bool isPrivate = CurrentProfile.isPrivate;
            bool activeProfile = CurrentProfile.isActive;
            bool NSFW = CurrentProfile.NSFW;
            bool Triggering = CurrentProfile.TRIGGERING;
            bool SpoilerARR = CurrentProfile.SpoilerARR;
            bool SpoilerHW = CurrentProfile.SpoilerHW;
            bool SpoilerSB = CurrentProfile.SpoilerSB;
            bool SpoilerSHB = CurrentProfile.SpoilerSHB;
            bool SpoilerEW = CurrentProfile.SpoilerEW;
            bool SpoilerDT = CurrentProfile.SpoilerDT;
            if(ImGui.Checkbox("Set Private", ref isPrivate)) { CurrentProfile.isPrivate = isPrivate; }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Leave unchecked to keep profile publicly viewable");
            }
            ImGui.SameLine();
            if(ImGui.Checkbox("Set As Current", ref activeProfile)) { CurrentProfile.isActive = activeProfile; }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Sets this profile as your current viewable profile when public");
            }
            ImGui.SameLine();
            if(ImGui.Checkbox("Show on Compass", ref showOnCompass))
            {
                DataSender.SetCompassStatus(Plugin.character, showOnCompass, profileIndex);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("This will make your position publicly visible on the compass while this profile is set as current");
            }
            if(ImGui.Checkbox("Set as 18+", ref NSFW)) { CurrentProfile.NSFW = NSFW; }
            ImGui.SameLine();
            if(ImGui.Checkbox("Set as Triggering", ref Triggering)) { CurrentProfile.TRIGGERING = Triggering; }
            ImGui.Text("Has Spoilers From:");
            if(ImGui.Checkbox("A Realm Reborn", ref SpoilerARR)) {  CurrentProfile.SpoilerARR = SpoilerARR; }
            ImGui.SameLine();
            if(ImGui.Checkbox("Heavensward", ref SpoilerHW)) {  CurrentProfile.SpoilerHW = SpoilerHW; }
            ImGui.SameLine();
            if(ImGui.Checkbox("Stormblood", ref SpoilerSB)) { CurrentProfile.SpoilerSB = SpoilerSB; }
            if(ImGui.Checkbox("Shadowbringers", ref SpoilerSHB)) {  CurrentProfile.SpoilerSHB = SpoilerSHB; }
            ImGui.SameLine();
            if(ImGui.Checkbox("Endwalker", ref SpoilerEW)) {  CurrentProfile.SpoilerEW = SpoilerEW; }
            ImGui.SameLine();
            if(ImGui.Checkbox("Dawntrail", ref SpoilerDT)) { CurrentProfile.SpoilerDT = SpoilerDT; }

            if (ImGui.Button("Save Profile"))
            {
                SubmitProfileData(false);
            }
            ImGui.SameLine();
            using (ImRaii.Disabled(!Plugin.CtrlPressed()))
            {
                if (ImGui.Button("Delete Profile"))
                {
                    try
                    {
                        DataSender.DeleteProfile(Plugin.character, profileIndex);
                        profileIndex -= 1;
                        if (profileIndex < 0)
                        {
                            profileIndex = 0;
                            ExistingProfile = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.PluginLog.Debug("Could not delete profile properly", ex.Message);
                    }
                    finally
                    {
                        profiles.Clear();
                        DataSender.FetchProfiles(Plugin.character);
                        if(profileIndex > -1)
                        {
                            DataSender.FetchProfile(Plugin.character, true, profileIndex, Plugin.character.characterName, Plugin.character.characterWorld, -1);
                        }
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
            Vector4 color = CurrentProfile.titleColor;  
            if (CurrentProfile.title.Length > 0)
            {
                Misc.SetTitle(Plugin.plugin, true, CurrentProfile.title, color);
            }
            string ProfileTitle = CurrentProfile.title;
            if(Misc.DrawXCenteredInput("TITLE:", $"Title{profileIndex}", ref ProfileTitle, 50))
            {
                CurrentProfile.title = ProfileTitle;
            }
            ImGui.SameLine();
            if(ImGui.ColorEdit4($"##Text Input Color{profileIndex}", ref color, ImGuiColorEditFlags.NoInputs)) { CurrentProfile.titleColor = color; }
            // Get the actual name and world ID
            var uploadBtnSize = ImGui.CalcTextSize("Set Background") + new Vector2(10, 10);
            float uploadXPos = (windowSize.X - uploadBtnSize.X) / 2;

            ImGui.SetCursorPosX(uploadXPos);
            if (ImGui.Button("Set Background"))
            {
                editBackground = true;
            }
            ImGui.SameLine();
            if (ImGui.Button("X##RemoveBackground"))
            {
                // Create a 4x4 transparent PNG
                CurrentProfile.backgroundBytes = CreateTransparentPng();
                backgroundImage = null;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Remove background image");
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

                    // Persist back the open/closed state to the model so close button behavior is consistent
                    tab.IsOpen = isOpen;

                    // If user clicked the tab header, mark it as the currently targeted tab for delete/other actions
                    if (ImGui.IsItemClicked() || ImGui.IsItemActivated() || ImGui.IsItemFocused())
                    {
                        tabToDeleteIndex = i;
                    }
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
                                    try
                                    {
                                        if (tabToDeleteIndex >= 0 && tabToDeleteIndex < CurrentProfile.customTabs.Count)
                                        {
                                            var tabToDelete = CurrentProfile.customTabs[tabToDeleteIndex];
                                            var tabTypeToSend = tabToDelete.type;

                                            // Get the actual tabIndex from the layout, not the list position
                                            int actualTabIndex = tabToDeleteIndex; // Default to list position
                                            if (tabToDelete.Layout != null)
                                            {
                                                actualTabIndex = tabToDelete.Layout switch
                                                {
                                                    BioLayout bio => bio.tabIndex,
                                                    DetailsLayout details => details.tabIndex,
                                                    StoryLayout story => story.tabIndex,
                                                    InfoLayout info => info.tabIndex,
                                                    GalleryLayout gallery => gallery.tabIndex,
                                                    InventoryLayout inv => inv.tabIndex,
                                                    TreeLayout tree => tree.tabIndex,
                                                    DynamicLayout dyn => dyn.tabIndex,
                                                    _ => tabToDeleteIndex
                                                };
                                            }

                                            // Send delete request to server first
                                            try
                                            {
                                                DataSender.DeleteTab(Plugin.character, CurrentProfile.index, actualTabIndex, tabTypeToSend);
                                                Plugin.PluginLog.Debug($"RenderCustomTabs: DeleteTab sent for profileIndex={CurrentProfile?.index} tabIndex={actualTabIndex} (listPos={tabToDeleteIndex}) type={tabTypeToSend}");
                                            }
                                            catch (Exception exDel)
                                            {
                                                Plugin.PluginLog.Debug($"RenderCustomTabs: DeleteTab call failed: {exDel.Message}");
                                            }

                                            // Remove the tab locally and clean up runtime structures
                                            // Dispose/cleanup runtime layout entry if present
                                            try
                                            {
                                                if (tabToDeleteIndex >= 0 && tabToDeleteIndex < customLayouts.Count)
                                                {
                                                    // best-effort removal from customLayouts; disposal handled elsewhere on full Dispose
                                                    customLayouts.RemoveAt(tabToDeleteIndex);
                                                }
                                            }
                                            catch { /* non-fatal */ }

                                            CurrentProfile.customTabs.RemoveAt(tabToDeleteIndex);

                                            // Rebuild CustomTabOpen mapping to keep keys in sync
                                            CustomTabOpen.Clear();
                                            for (int kk = 0; kk < CurrentProfile.customTabs.Count; kk++)
                                                CustomTabOpen[kk] = CurrentProfile.customTabs[kk].IsOpen;

                                            // Rebuild ordering lists to remain consistent
                                            tabOrder = Enumerable.Range(0, CurrentProfile.customTabs.Count).ToList();
                                            initialTabOrder = Enumerable.Range(0, CurrentProfile.customTabs.Count).ToList();

                                            customTabsCount = CurrentProfile.customTabs.Count;
                                        }

                                        // Close confirmation state
                                        showDeleteConfirmationPopup = false;
                                        tabToDeleteIndex = -1;
                                        ImGui.CloseCurrentPopup();
                                    }
                                    catch (Exception ex)
                                    {
                                        Plugin.PluginLog.Debug($"RenderCustomTabs: deletion flow failed: {ex}");
                                        // Attempt to restore sane state
                                        showDeleteConfirmationPopup = false;
                                        tabToDeleteIndex = -1;
                                        ImGui.CloseCurrentPopup();
                                    }
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
        }// Insert this helper method inside the ProfileWindow class (e.g., near the bottom before Dispose/other helpers)
        private static void NormalizeTreeLayoutSlots(TreeLayout tr)
        {
            if (tr == null || tr.relationships == null || tr.relationships.Count == 0)
                return;

            const int gridSizeX = 5;
            const int gridSizeY = 8;
            var centerX = gridSizeX / 2;
            var centerY = gridSizeY / 2;

            // Helper: is slot inside grid
            static bool InRange((int x, int y) s, int maxX, int maxY)
                => s.x >= 0 && s.x < maxX && s.y >= 0 && s.y < maxY;

            // Build list of defined slots (skip nulls)
            var definedSlots = tr.relationships
                .Where(r => r?.Slot.HasValue == true)
                .Select(r => r.Slot!.Value)
                .ToList();

            // If none defined, nothing to normalize
            if (definedSlots.Count == 0)
                return;

            // Quick accept: if most slots already in range, do nothing
            var inRangeCount = definedSlots.Count(s => InRange(s, gridSizeX, gridSizeY));
            if (inRangeCount >= definedSlots.Count)
            {
                Plugin.PluginLog?.Debug($"NormalizeTreeLayoutSlots: all slots already in-range ({inRangeCount}/{definedSlots.Count}). No transform applied.");
                return;
            }

            // Candidate transforms to test
            (string name, Func<(int x, int y), (int x, int y)> fn)[] transforms =
            {
        ("identity", s => (s.x, s.y)),
        ("addCenter", s => (s.x + centerX, s.y + centerY)),
        ("swapXY", s => (s.y, s.x)),
        ("swapXY_addCenter", s => (s.y + centerX, s.x + centerY))
    };

            // Score each transform by how many slots fall in-range after applying it
            var best = transforms
                .Select(t => new
                {
                    t.name,
                    t.fn,
                    count = definedSlots.Select(s => t.fn(s)).Count(s2 => InRange(s2, gridSizeX, gridSizeY))
                })
                .OrderByDescending(x => x.count)
                .First();

            Plugin.PluginLog?.Debug($"NormalizeTreeLayoutSlots: best transform='{best.name}' maps {best.count}/{definedSlots.Count} slots in-range.");

            // If identity is best but still maps zero, still try addCenter as fallback
            // If identity is best but still maps zero, still try addCenter as fallback
            if (best.count == 0 && transforms.Any(t => t.name == "addCenter"))
            {
                var addCenter = transforms.First(t => t.name == "addCenter");
                var cnt = definedSlots.Select(s => addCenter.fn(s)).Count(s2 => InRange(s2, gridSizeX, gridSizeY));
                if (cnt > best.count)
                {
                    // Construct an anonymous object with the same property names/types as 'best'
                    best = new { name = addCenter.name, fn = addCenter.fn, count = cnt };
                    Plugin.PluginLog?.Debug($"NormalizeTreeLayoutSlots: fallback chose addCenter, maps {cnt}/{definedSlots.Count} in-range.");
                }
            }

            // Only apply if it improves mapping
            if (best.count > inRangeCount)
            {
                try
                {
                    foreach (var rel in tr.relationships)
                    {
                        if (rel?.Slot.HasValue != true) continue;
                        var old = rel.Slot.Value;
                        var @new = best.fn(old);
                        rel.Slot = @new;
                        Plugin.PluginLog?.Debug($"NormalizeTreeLayoutSlots: rel '{rel.Name}' slot {old} -> {@new}");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog?.Debug($"NormalizeTreeLayoutSlots: failed applying transform '{best.name}': {ex.Message}");
                }
            }
            else
            {
                Plugin.PluginLog?.Debug("NormalizeTreeLayoutSlots: no transform improved mapping; leaving slots as-is.");
            }
        }
        private void RenderTab(CustomLayout layout, int index, ref bool isOpen, string tabName)
        {
            try
            {
                // Defensive defaults
                tabName ??= $"Tab{index}";
                string uniqueId = $"{tabName}##{index}";

                using (var tab = ImRaii.TabItem(uniqueId, ref isOpen))
                {
                    // IMPORTANT: do NOT return when tab == false.
                    // Begin/TabItem may return false for an inactive tab but it still updates the ref 'isOpen'
                    // when the user clicks the little close button. We must let the method continue so
                    // the close handling below runs.
                    if (tab)
                    {
                        customTabSelected = true;

                        // Capture which tab header the user interacted with so delete targets the expected tab
                        try
                        {
                            if (ImGui.IsItemClicked() || ImGui.IsItemActivated() || ImGui.IsItemFocused())
                            {
                                tabToDeleteIndex = index;
                            }
                        }
                        catch { /* non-fatal: defensive */ }

                        if (layout == null)
                        {
                            Plugin.PluginLog.Debug($"RenderTab: layout is null for tab '{tabName}' index={index}.");
                        }
                        else
                        {
                            var layoutTypeName = layout.GetType().Name;
                            try
                            {
                                switch (layout)
                                {
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
                                    case InventoryLayout inventoryLayout:
                                        Inventory.RenderInventoryLayout(index, uniqueId, inventoryLayout);
                                        break;
                                    case TreeLayout treeLayout:
                                        Tree.RenderTreeLayout(index, true, uniqueId, treeLayout, string.Empty, new Vector4(0, 0, 0, 0));
                                        break;
                                    default:
                                        Plugin.PluginLog.Debug($"RenderTab: unsupported layout type '{layoutTypeName}' for tab '{tabName}' index={index}.");
                                        break;
                                }
                            }
                            catch (Exception exLayout)
                            {
                                // Log full exception + context so we can identify the root cause inside the layout renderer.
                                Plugin.PluginLog.Debug($"RenderTab: Exception while rendering layout '{layoutTypeName}' for tab '{tabName}' (index={index}): {exLayout}");
                            }
                        }
                    }
                }

                // Handle the case where the tab was closed (close button pressed)
                if (!isOpen)
                {
                    // Keep UI behaviour: open confirmation instead of immediately removing
                    isOpen = true; // reopen temporarily to show confirmation
                    tabToDeleteIndex = index;
                    showDeleteConfirmationPopup = true;
                    ImGui.OpenPopup("Delete Tab Confirmation");
                }
            }
            catch (Exception ex)
            {
                // Log full exception with context
                Plugin.PluginLog.Debug($"ProfileWindow RenderTab Debug: Exception rendering tab '{tabName}' index={index}: {ex}");
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

            if (CurrentProfile != null && CurrentProfile.customTabs?.Count > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Creates a 4x4 transparent PNG as a byte array.
        /// </summary>
        private static byte[] CreateTransparentPng()
        {
            // Minimal valid 4x4 transparent PNG (manually constructed)
            // PNG header + IHDR chunk + IDAT chunk (compressed transparent pixels) + IEND chunk
            return new byte[]
            {
                // PNG signature
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                // IHDR chunk (13 bytes data)
                0x00, 0x00, 0x00, 0x0D, // chunk length
                0x49, 0x48, 0x44, 0x52, // "IHDR"
                0x00, 0x00, 0x00, 0x04, // width: 4
                0x00, 0x00, 0x00, 0x04, // height: 4
                0x08,                   // bit depth: 8
                0x06,                   // color type: RGBA
                0x00,                   // compression method
                0x00,                   // filter method
                0x00,                   // interlace method
                0x90, 0x6B, 0x0B, 0x5C, // CRC
                // IDAT chunk (compressed data for 4x4 transparent image)
                0x00, 0x00, 0x00, 0x1B, // chunk length: 27
                0x49, 0x44, 0x41, 0x54, // "IDAT"
                0x78, 0x9C, 0x62, 0x60, 0x60, 0x60, 0x60, 0x60,
                0x00, 0x02, 0x06, 0x20, 0x08, 0x00, 0x00, 0x00,
                0x00, 0x04, 0x00, 0x01, 0x00, 0x00, 0x15, 0x7F,
                0x00, 0x11,
                0x11, 0x94, 0x19, 0x8E, // CRC
                // IEND chunk
                0x00, 0x00, 0x00, 0x00, // chunk length: 0
                0x49, 0x45, 0x4E, 0x44, // "IEND"
                0xAE, 0x42, 0x60, 0x82  // CRC
            };
        }

        public override void OnClose()
        {
            // Stop and dispose all audio players when closing the window
            Misc.CleanupAudioPlayers();
            base.OnClose();
        }

        public void Dispose()
        {
            // Stop and dispose all audio players
            Misc.CleanupAudioPlayers();

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

            // Clear tooltipData data
            CurrentProfile = null;
            profiles?.Clear();
        }

        private void DrawProfileTypeSelection()
        {
            try
            {
                // Use the current index for the label
                var currentLabel = UI.ListingCategoryVals[currentProfileType].Item1;

                using var combo = ImRaii.Combo("##Profiles", currentLabel);
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
            }
            catch (Exception ex)
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
                    profileNames.Add(profiles[i].title);
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
                            UIHelpers.SelectableHelpMarker("Select to edit the selected profile");
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
                "JSON{.json}",
                (bool success, string filePath) =>
                {
                    if (!success || string.IsNullOrEmpty(filePath))
                        return;

                    try
                    {
                        ProfileData profile = BackupData.ImportProfileFromJsonAsync(filePath).GetAwaiter().GetResult();
                        if (profile == null)
                        {
                            Plugin.PluginLog.Debug("LoadBackupLoaderDialog: imported profile is null.");
                            return;
                        }

                        // Apply profile into CurrentProfile and attempt to map it to the currently selected slot
                        LoadAndApplyProfile(profile);
                        Plugin.PluginLog.Debug($"Backup loaded (pre-apply): title='{profile.title}', tabs={profile.customTabs?.Count ?? 0}, importedIndex={profile.index}");

                        // If there is an existing profile slot selected, overwrite that slot so the backup is applied
                        if (profiles != null && profiles.Count > 0 && profileIndex >= 0 && profileIndex < profiles.Count)
                        {
                            try
                            {
                                // Preserve server-side index (the numeric id the rest of the code expects)
                                profile.index = profiles[profileIndex].index;
                                profiles[profileIndex] = profile;
                                CurrentProfile = profile;
                                Plugin.PluginLog.Debug($"LoadBackupLoaderDialog: applied backup to existing slot profileIndex={profileIndex}, serverIndex={profile.index}");
                            }
                            catch (Exception ex)
                            {
                                Plugin.PluginLog.Debug($"LoadBackupLoaderDialog: failed to apply backup to selected slot: {ex}");
                            }

                            // Submit the profile to server (voidData true will create tabs server-side for this profile index)
                            SubmitProfileData(true);
                        }
                        else
                        {
                            // No existing slot selected — do not blindly submit to index 0.
                            // Log and inform developer/user to create a profile first.
                            Plugin.PluginLog.Debug("LoadBackupLoaderDialog: no existing profile slot to apply the backup to. Please create a profile first.");
                            // Still set CurrentProfile so user can inspect and manually create/choose a profile to apply onto.
                            CurrentProfile = profile;
                        }

                        Plugin.PluginLog.Debug($"Backup loaded: title='{profile.title}', tabs={profile.customTabs?.Count ?? 0}");
                    }
                    catch (Exception ex)
                    {
                        Plugin.PluginLog.Debug($"Debug loading backup file: {ex.Message}");
                    }
                    // NOTE: SubmitProfileData is now invoked only when we have a valid existing slot to overwrite.
                }
            );
        }
        public void LoadBackupSaveDialog()
        {
            try
            {
                _fileDialogManager.SaveFileDialog("Save Backup", "JSON{.json}", "Backup Name", ".json", (Action<bool, string>)((s, f) =>
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
                if (string.IsNullOrWhiteSpace(dataPath))
                    throw new ArgumentNullException(nameof(dataPath));

                dataPath = dataPath.Trim();

                // If caller passed a directory, write a default filename inside it
                if (System.IO.Directory.Exists(dataPath))
                {
                    dataPath = System.IO.Path.Combine(dataPath, "Backup.json");
                }

                // Ensure file has an extension
                if (!System.IO.Path.HasExtension(dataPath))
                    dataPath = System.IO.Path.ChangeExtension(dataPath, ".json");

                var dir = System.IO.Path.GetDirectoryName(dataPath);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                // Write file
                await BackupData.ExportProfileToJsonAsync(CurrentProfile, dataPath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug saving backup file: {ex.Message}");
            }
        }
        public void SubmitProfileData(bool voidData)
        {
            try
            {

                DataSender.SetProfileStatus(Plugin.character, CurrentProfile.isPrivate, CurrentProfile.isActive, profileIndex, CurrentProfile.title, CurrentProfile.titleColor, CurrentProfile.avatarBytes, CurrentProfile.backgroundBytes, CurrentProfile.SpoilerARR, CurrentProfile.SpoilerHW, CurrentProfile.SpoilerSB, CurrentProfile.SpoilerSHB, CurrentProfile.SpoilerEW, CurrentProfile.SpoilerDT, CurrentProfile.NSFW, CurrentProfile.TRIGGERING);
                Plugin.PluginLog.Debug($"Tabs count before submit: {CurrentProfile.customTabs.Count}");
                foreach (var tab in CurrentProfile.customTabs)
                {
                    Plugin.PluginLog.Debug($"Tab: {tab.Name}, Type: {tab.Layout?.GetType().Name}");
                }

                Sending = true;
                if (voidData)
                {
                    for(int i =0; i < CurrentProfile.customTabs.Count; i++) 
                    {
                        CustomTab tab = CurrentProfile.customTabs[i];   
                        DataSender.CreateTab(Plugin.character, tab.Name, (int)tab.Layout.layoutType, CurrentProfile.index, i);
                    }                 
                }
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
        public void LoadAndApplyProfile(ProfileData profile)
        {
            try
            {
                if (profile == null) throw new ArgumentNullException(nameof(profile));

                // Assign
                CurrentProfile = profile;

                // Ensure tabs list exists
                if (CurrentProfile.customTabs == null)
                    CurrentProfile.customTabs = new System.Collections.Generic.List<CustomTab>();

                // Reset internal helpers
                customLayouts.Clear();
                CustomTabOpen.Clear();
                tabOrder.Clear();
                initialTabOrder.Clear();

                customTabsCount = CurrentProfile.customTabs.Count;

                // Ensure every tab has a runtime layout instance so RenderCustomTabs will display it
                for (int i = 0; i < CurrentProfile.customTabs.Count; i++)
                {
                    var t = CurrentProfile.customTabs[i];

                    // Ensure tab name is not null
                    t.Name ??= $"Page {i + 1}";

                    // Keep IsOpen true by default so user sees the tab
                    t.IsOpen = t.IsOpen;

                    // If Layout is missing, create an empty layout based on the stored type
                    if (t.Layout == null)
                    {
                        try
                        {
                            // t.type is stored as int matching LayoutTypes
                            var lt = (LayoutTypes)Math.Clamp(t.type, 0, Enum.GetValues(typeof(LayoutTypes)).Length - 1);

                            CustomLayout created = lt switch
                            {
                                LayoutTypes.Bio => new BioLayout { tabIndex = i },
                                LayoutTypes.Details => new DetailsLayout { tabIndex = i },
                                LayoutTypes.Gallery => new GalleryLayout { tabIndex = i },
                                LayoutTypes.Info => new InfoLayout { tabIndex = i },
                                LayoutTypes.Story => new StoryLayout { tabIndex = i },
                                LayoutTypes.Inventory => new InventoryLayout { tabIndex = i },
                                LayoutTypes.Relationship => new TreeLayout { tabIndex = i },
                                _ => new CustomLayout { id = 0, name = t.Name ?? string.Empty, layoutType = lt, viewable = true }
                            };

                            t.Layout = created;
                        }
                        catch
                        {
                            // Fallback: ensure there is at least a generic layout
                            t.Layout = new CustomLayout { id = 0, name = t.Name ?? string.Empty, layoutType = LayoutTypes.Info, viewable = true };
                        }
                    }
                    else
                    {
                        // If layout already exists, try to set/repair tabIndex where applicable
                        switch (t.Layout)
                        {
                            case BioLayout b: b.tabIndex = i; break;
                            case DetailsLayout d: d.tabIndex = i; break;
                            case GalleryLayout g: g.tabIndex = i; break;
                            case InfoLayout inf: inf.tabIndex = i; break;
                            case StoryLayout s: s.tabIndex = i; break;
                            case InventoryLayout inventoryLayout: inventoryLayout.tabIndex = i; break;
                            case TreeLayout tr:  tr.tabIndex = i;NormalizeTreeLayoutSlots(tr); break;
                        }
                    }

                    // Track layout instances for disposal/management
                    if (t.Layout != null)
                    {
                        customLayouts.Add(t.Layout);
                    }

                    // Track open state and ordering
                    CustomTabOpen[i] = t.IsOpen;
                    tabOrder.Add(i);
                    initialTabOrder.Add(i);

                    // capture inventory layout for quick access
                    if (t.Layout is InventoryLayout inv && currentInventory == null)
                        currentInventory = inv;
                }

                // Choose a safe avatar image fallback (we don't attempt byte->texture here)
                if (currentAvatarImg == null)
                    currentAvatarImg = avatarHolder ?? pictureTab ?? UI.UICommonImage(UI.CommonImageTypes.avatarHolder);

                // If profile is already present in the profiles list, set profileIndex accordingly
                var idx = profiles?.IndexOf(profile) ?? -1;
                if (idx >= 0)
                    profileIndex = idx;

                // Diagnostics
                Plugin.PluginLog.Debug($"LoadAndApplyProfile: title='{CurrentProfile.title}', tabs={CurrentProfile.customTabs.Count}, profileIndex={profileIndex}");
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"LoadAndApplyProfile Debug: {ex.Message}");
            }
        }

    }
}



