using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Dalamud.Utility;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Networking;
using Dalamud.Interface.Internal;
using OtterGui;
using System.Linq;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Textures.TextureWraps;

using System.Diagnostics;
using System.Collections;
using OtterGui.Log;
using static Lumina.Data.Parsing.Layer.LayerCommon;
using Dalamud.Hooking;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.VertexShader;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Lumina.Excel.GeneratedSheets;
using OtterGui.Tasks;


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
        private IDalamudPluginInterface pg;
        private FileDialogManager _fileDialogManager; //for avatars only at the moment
        public Configuration configuration;
        public static int galleryImageCount = 0;
        public static IDalamudTextureWrap pictureTab; //picturetab.png for base picture in gallery
        public static string[] HookNames = new string[31];
        public static string[] HookContents = new string[31];
        public static string[] ChapterContents = new string[31];
        public static string[] ChapterNames = new string[31];
        public static string[] imageURLs = new string[31];
        public static bool[] NSFW = new bool[31]; //gallery images NSFW status
        public static bool[] TRIGGER = new bool[31]; //gallery images TRIGGER status
        public static bool[] ImageExists = new bool[31]; //used to check if an image exists in the gallery
        public static bool[] viewChapter = new bool[31]; //to check which chapter we are currently viewing
        public static bool[] hookExists = new bool[31]; //same as ImageExists but for hooks
        public static bool[] storyChapterExists = new bool[31]; //same again but for story chapters
        public static SortedList<TabValue, bool> TabOpen = new SortedList<TabValue, bool>(); //what part of the profile we have open
        public static bool editAvatar, addProfile, editProfile, ReorderGallery, addGalleryImageGUI, alignmentHidden, personalityHidden, loadPreview = false;
        public static string oocInfo, storyTitle = string.Empty;
        public static bool ExistingProfile, ExistingStory, ExistingOOC, ExistingHooks, ExistingGallery, ExistingBio, ReorderHooks, ReorderChapters, AddHooks, AddStoryChapter; //to check if we have data from the DataReceiver for the respective fields or to reorder the gallery or hooks after deletion
        public static int chapterCount, currentAlignment, currentPersonality_1, currentPersonality_2, currentPersonality_3, hookCount = 0; //values changed by DataReceiver as well
        public static byte[] avatarBytes; //avatar image in a byte array
        public static float loaderInd; //used for the gallery loading bar
        public static IDalamudTextureWrap avatarHolder, currentAvatarImg;
        public static List<IDalamudTextureWrap> galleryThumbsList = new List<IDalamudTextureWrap>();
        public static List<IDalamudTextureWrap> galleryImagesList = new List<IDalamudTextureWrap>();
        public static IDalamudTextureWrap[] galleryImages, galleryThumbs;
        public static string[] bioFieldsArr = new string[7]; //fields such as name, race, gender and so on
        private IDalamudTextureWrap persistAvatarHolder;
        public static bool drawChapter;
        public static int storyChapterCount = -1;
        public static int currentChapter;
        public static bool privateProfile; //sets whether the profile is allowed to be publicly viewed

        public ProfileWindow(Plugin plugin) : base(
       "PROFILE", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {

                MinimumSize = new Vector2(600, 400),
                MaximumSize = new Vector2(750, 950)
            };

            this.plugin = plugin;
            pg = Plugin.PluginInterface;
            configuration = plugin.Configuration;
            _fileDialogManager = new FileDialogManager();


        }
        public override void OnOpen()
        {
            TabOpen.Clear(); //clear our TabOpen array before populating again
            var avatarHolderImage = Defines.UICommonImage(Defines.CommonImageTypes.avatarHolder); //Load the avatarHolder TextureWrap from Constants.UICommonImage
            if (avatarHolderImage != null)
            {
                avatarHolder = avatarHolderImage; //set our avatarHolder ot the same TextureWrap
            }
            //same for pictureTab
            var pictureTabImage = Defines.UICommonImage(Defines.CommonImageTypes.blankPictureTab);
            if (pictureTabImage != null)
            {
                pictureTab = pictureTabImage;
            }
            persistAvatarHolder = avatarHolder; //unneeded at the moment, but I seem to keep needing and not needing it so I am leaving it for now.
            for (var bf = 0; bf < bioFieldsArr.Length; bf++)
            {
                //set all the bioFields to an empty string
                bioFieldsArr[bf] = string.Empty;
            }
            foreach (TabValue tab in Enum.GetValues(typeof(TabValue)))
            {
                TabOpen.Add(tab, false); //set all tabs to be closed by default
            }
            //set the base value for our arrays and lists
            for (var i = 0; i < 31; i++)
            {
                ChapterNames[i] = string.Empty;
                ChapterContents[i] = string.Empty;
                HookNames[i] = string.Empty;
                HookContents[i] = string.Empty;
                hookExists[i] = false;
                NSFW[i] = false;
                TRIGGER[i] = false;
                storyChapterExists[i] = false;
                viewChapter[i] = false;
                ImageExists[i] = false;
                galleryImagesList.Add(pictureTab);
                galleryThumbsList.Add(pictureTab);
                imageURLs[i] = string.Empty;
            }
            galleryImages = galleryImagesList.ToArray();
            galleryThumbs = galleryThumbsList.ToArray();

            //set all our text entry fields for the bio to empty strings
            for (var b = 0; b < bioFieldsArr.Length; b++)
            {
                bioFieldsArr[b] = string.Empty;
            }
            if (avatarBytes == null)
            {
                //set the avatar to the avatar_holder.png by default
                if (Plugin.PluginInterface is { AssemblyLocation.Directory.FullName: { } path })
                {
                    avatarBytes = File.ReadAllBytes(Path.Combine(path, "UI/common/profiles/avatar_holder.png"));
                }
            }

        }
        //method to check if we have loaded our data received from the server
        public static bool AllLoaded()
        {
            if (DataReceiver.StoryLoadStatus != -1 &&
                   DataReceiver.HooksLoadStatus != -1 &&
                   DataReceiver.BioLoadStatus != -1 &&
                   DataReceiver.GalleryLoadStatus != -1)
            {
                return true;
            }
            return false;
        }
        public override void Draw()
        {
            //if we have loaded all the data received from the server and we are logged in game
            if (AllLoaded() == true && plugin.IsOnline())
            {
                _fileDialogManager.Draw(); //file dialog mainly for avatar atm. galleries later possibly.


                if (ExistingProfile == true)//if we have a profile add the edit profile button
                {
                    if (ImGui.Checkbox("Set Private", ref privateProfile))
                    {
                        //send our privacy settings to the server
                        DataSender.SetProfileStatus(plugin.username.ToString(), plugin.playername, plugin.playerworld, privateProfile);
                    }
                    if (ImGui.Button("Save Profile"))
                    {
                        SubmitProfileData();
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
                    if (ProfileHasContent() == true)
                    {
                        using (OtterGui.Raii.ImRaii.Disabled(!Plugin.CtrlPressed()))
                        {
                            if (ImGui.Button("Delete Profile"))
                            {
                                DataSender.DeleteProfile(plugin.username, plugin.password, plugin.playername, plugin.playerworld);
                            }

                        }
                        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        {
                            ImGui.SetTooltip("Delete your profile (This is a destructive action!)");
                        }
                    }
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
                }
                if (ExistingProfile == false) //else create our add profile button to create a new profile
                {
                    if (ImGui.Button("Add Profile")) { DataSender.CreateProfile(plugin.playername, plugin.playerworld); }
                }
                else
                {
                    ImGui.Spacing();
                    ImGui.BeginTabBar("ProfileNavigation");
                    if (ImGui.BeginTabItem("Edit Bio")) { ClearUI(); TabOpen[TabValue.Bio] = true; ImGui.EndTabItem(); }
                    if (ImGui.BeginTabItem("Edit Hooks")) { ClearUI(); TabOpen[TabValue.Hooks] = true; ImGui.EndTabItem(); }
                    if (ImGui.BeginTabItem("Edit Story")) { ClearUI(); TabOpen[TabValue.Story] = true; ImGui.EndTabItem(); }
                    if (ImGui.BeginTabItem("Edit OOC")) { ClearUI(); TabOpen[TabValue.OOC] = true; ImGui.EndTabItem(); }
                    if (ImGui.BeginTabItem("Edit Gallery")) { ClearUI(); TabOpen[TabValue.Gallery] = true; ImGui.EndTabItem(); }
                    ImGui.EndTabBar();

                }

                using var ProfileTable = ImRaii.Child("PROFILE");
                if (ProfileTable)
                {
                    #region BIO
                    if (TabOpen[TabValue.Bio])
                    {
                        //display for avatar
                        ImGui.Image(currentAvatarImg.ImGuiHandle, new Vector2(100, 100));

                        if (ImGui.Button("Edit Avatar"))
                        {
                            editAvatar = true; //used to open the file dialog
                        }
                        ImGui.Spacing();
                        //simple for loop to get through our bio text fields
                        for (var i = 0; i < Defines.BioFieldVals.Length; i++)
                        {
                            var BioField = Defines.BioFieldVals[i];
                            //if our input type is single line 
                            if (BioField.Item4 == Defines.InputTypes.single)
                            {
                                ImGui.Text(BioField.Item1);
                                //if our label is not AFG use sameline
                                if (BioField.Item1 != "AT FIRST GLANCE:")
                                {
                                    ImGui.SameLine();
                                }
                                //add the input text for the field
                                ImGui.InputTextWithHint(BioField.Item2, BioField.Item3, ref bioFieldsArr[i], 100);
                            }
                            else
                            {
                                //text must be multiline so add the multiline field/fields
                                ImGui.Text(BioField.Item1);
                                ImGui.InputTextMultiline(BioField.Item2, ref bioFieldsArr[i], 3100, new Vector2(500, 150));
                            }
                        }
                        ImGui.Spacing();
                        ImGui.Spacing();

                        ImGui.TextColored(new Vector4(1, 1, 1, 1), "ALIGNMENT:");
                        AddAlignmentSelection(); //add alignment combo selection

                        ImGui.Spacing();

                        ImGui.TextColored(new Vector4(1, 1, 1, 1), "PERSONALITY TRAITS:");
                        //add personality combos
                        AddPersonalitySelection_1();
                        AddPersonalitySelection_2();
                        AddPersonalitySelection_3();
                      
                    }
                    #endregion
                    #region HOOKS
                    if (TabOpen[TabValue.Hooks])
                    {
                        if (ImGui.Button("Add Hook"))
                        {
                            if (hookCount < 30)
                            {
                                hookCount++;
                            }
                        }
                       
                        ImGui.NewLine();
                        AddHooks = true;
                        hookExists[hookCount] = true;
                    }
                    #endregion
                    #region STORY
                    if (TabOpen[TabValue.Story])
                    {
                        ImGui.Text("Story Title");
                        ImGui.SameLine();
                        ImGui.InputText("##storyTitle", ref storyTitle, 35);

                        ImGui.Text("Chapter");
                        ImGui.SameLine();
                        //add our chapter combo select input
                        AddChapterSelection();
                        ImGui.SameLine();
                        if (ImGui.Button("Add Chapter"))
                        {
                            CreateChapter();
                        }

                        
                        ImGui.NewLine();

                    }
                    #endregion
                    #region GALLERY

                    if (TabOpen[TabValue.Gallery])
                    {
                        if (ImGui.Button("Add Image"))
                        {
                            if (galleryImageCount < 28)
                            {
                                galleryImageCount++;
                            }
                        }
                        
                        ImGui.NewLine();
                        addGalleryImageGUI = true;
                        ImageExists[galleryImageCount] = true;
                    }
                    #endregion
                    #region OOC

                    if (TabOpen[TabValue.OOC])
                    {
                        ImGui.InputTextMultiline("##OOC", ref oocInfo, 50000, new Vector2(500, 600));
                       
                    }
                    #endregion
                    if (loadPreview == true)
                    {
                        //load gallery image preview if requested
                        plugin.OpenImagePreview();
                        loadPreview = false;
                    }
                    if (addGalleryImageGUI == true)
                    {
                        AddImageToGallery(plugin, galleryImageCount); //used to add our image to the gallery
                    }
                    if (AddHooks == true)
                    {
                        DrawHooksUI(plugin, hookCount);
                    }
                    if (editAvatar == true)
                    {
                        editAvatar = false;
                        EditImage(true, 0);
                    }
                    if (drawChapter == true)
                    {
                        ImGui.NewLine();
                        DrawChapter(currentChapter, plugin);
                    }

                    //if true, reorders the gallery
                    if (ReorderGallery == true)
                    {
                        ReorderGallery = false;

                        var nextExists = ImageExists[NextAvailableImageIndex() + 1];//bool to check if the next image in the list exists
                        var firstOpen = NextAvailableImageIndex(); //index of the first image that does not exist
                        ImageExists[firstOpen] = true; //set the image to exist again

                        if (nextExists) // if our next image in the list exists
                        {
                            for (var i = firstOpen; i < galleryImageCount; i++)
                            {
                                //swap the image behind it to the one ahead, along with hte imageUrl and such
                                galleryImages[i] = galleryImages[i + 1];
                                galleryThumbs[i] = galleryThumbs[i + 1];
                                imageURLs[i] = imageURLs[i + 1];
                                NSFW[i] = NSFW[i + 1];
                                TRIGGER[i] = TRIGGER[i + 1];

                            }
                        }
                        //lower the overall image count
                        galleryImageCount--;
                        //set the gallery image we removed back to the base picturetab.png
                        galleryImages[galleryImageCount] = pictureTab;
                        galleryThumbs[galleryImageCount] = pictureTab;
                        //set the image to not exist until added again
                        ImageExists[galleryImageCount] = false;

                    }
                    //pretty much the same logic but with our hooks
                    if (ReorderHooks == true)
                    {
                        ReorderHooks = false;
                        var nextHookExists = hookExists[NextAvailableHookIndex() + 1];
                        var firstHookOpen = NextAvailableHookIndex();
                        hookExists[firstHookOpen] = true;
                        if (nextHookExists)
                        {
                            for (var i = firstHookOpen; i < hookCount; i++)
                            {
                                HookNames[i] = HookNames[i + 1];
                                HookContents[i] = HookContents[i + 1];

                            }
                        }

                        hookCount--;
                        HookNames[hookCount] = string.Empty;
                        HookContents[hookCount] = string.Empty;
                        hookExists[hookCount] = false;

                    }
                    //same for chapters aswell
                    if (ReorderChapters == true)
                    {
                        ReorderChapters = false;
                        var nextChapterExists = storyChapterExists[NextAvailableChapterIndex() + 1];
                        var firstChapterOpen = NextAvailableChapterIndex();
                        storyChapterExists[firstChapterOpen] = true;
                        if (nextChapterExists)
                        {
                            for (var i = firstChapterOpen; i < storyChapterCount; i++)
                            {
                                ChapterNames[i] = ChapterNames[i + 1];
                                ChapterContents[i] = ChapterContents[i + 1];
                                DrawChapter(i, plugin);
                            }
                        }


                    }



                }
            }
            else
            {
                //if our content is not all loaded use the loader
                Misc.StartLoader(loaderInd, percentage, loading);
            }

        }

        private bool ProfileHasContent()
        {
            
            for (int i = 0; i < bioFieldsArr.Length; i++)
            {
                if(bioFieldsArr[i] != string.Empty)
                {
                    return true;
                }
            }
            if(oocInfo != string.Empty ||  storyTitle != string.Empty) { return true; }

            for (int i = 0; i < ChapterNames.Length; i++)
            {
                if(ChapterNames[i] != string.Empty) return true;

            }
            for (int i = 0; i < ChapterContents.Length; i++)
            {
                if (ChapterContents[i] != string.Empty)
                {
                    return true;
                }
            }
            for (int i = 0; i < HookNames.Length; i++)
            {
                if (HookNames[i] != string.Empty)
                {
                    return true;
                }
            }
            for (int i = 0; i < HookContents.Length; i++)
            {
                if (HookContents[i] != string.Empty)
                {
                    return true;
                }
            }
            for (int i = 0; i < imageURLs.Length; i++)
            {
                if (imageURLs[i] != string.Empty)
                {
                    return true;
                }
            }
            return false;
        }

        public void CreateChapter()
        {
            if (storyChapterCount < 30)
            {
                storyChapterCount++; //increase chapter count
                storyChapterExists[storyChapterCount] = true; //set our chapter to exist
                ChapterNames[storyChapterCount] = "New Chapter"; //set a base title
                currentChapter = storyChapterCount; //switch our current selected chapter to the one we just made
                viewChapter[storyChapterCount] = true; //view the chapter we just made aswell
            }

        }
        public void RemoveChapter(int index)
        {
            storyChapterCount--; //reduce our chapter count
            storyChapterExists[index] = false; //set the image to not exist
            ChapterNames[index] = string.Empty; //reset the name
            ChapterContents[index] = string.Empty; //reset the contents
            //if the story behind it exists
            if (storyChapterExists[index - 1] == true)
            {
                //we switch to that chapter to view it instead.
                currentChapter = index - 1;
                viewChapter[index - 1] = true;
            }
            ReorderChapters = true; //finally reorder chapters

        }
        public void ClearChaptersInView() //not used at the moment
        {
            for (var i = 0; i < viewChapter.Length; i++)
            {
                viewChapter[i] = false;
            }
        }
        public void DrawChapter(int i, Plugin plugin)
        {

            if (TabOpen[TabValue.Story] == true && i >= 0)
            {
                //if our chapter exists and we are viewing it
                if (storyChapterExists[i] == true && viewChapter[i] == true)
                {
                    //create a new child with the scale of the window size but inset slightly
                    var windowSize = ImGui.GetWindowSize();
                    using var profileTable = ImRaii.Child("##Chapter" + i, new Vector2(windowSize.X - 20, windowSize.Y - 130));
                    if (profileTable)
                    {
                        ImGui.TextUnformatted("Chapter Name:");
                        ImGui.SameLine();
                        ImGui.InputText(string.Empty, ref ChapterNames[i], 100);
                        //set an input size for our input text as well to adjust with window scale
                        var inputSize = new Vector2(windowSize.X - 30, windowSize.Y - 200); // Adjust as needed
                        ImGui.InputTextMultiline("##ChapterContent" + i, ref ChapterContents[i], 5000, inputSize);

                        using var chapterControlTable = ImRaii.Child("##ChapterControls" + i);
                        if (chapterControlTable)
                        {
                            using (OtterGui.Raii.ImRaii.Disabled(!Plugin.CtrlPressed()))
                            {
                                if (ImGui.Button("Remove##" + "chapter" + i))
                                {
                                    RemoveChapter(i);
                                }

                            }
                            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            {
                                ImGui.SetTooltip("Ctrl Click to Enable");
                            }


                        }
                    }


                }
            }
        }
        //simply draws the hook with the specified index and controls for said hook to the window
        public void DrawHook(int i, Plugin plugin)
        {
            if (hookExists[i] == true)
            {

                using var hookChild = ImRaii.Child("##Hook" + i, new Vector2(550, 250));
                if (hookChild)
                {
                    ImGui.InputTextWithHint("##HookName" + i, "Hook Name", ref HookNames[i], 300);
                    ImGui.InputTextMultiline("##HookContent" + i, ref HookContents[i], 5000, new Vector2(500, 200));

                    try
                    {

                        using var hookControlsTable = ImRaii.Child("##HookControls" + i);
                        if (hookControlsTable)
                        {
                            using (OtterGui.Raii.ImRaii.Disabled(!Plugin.CtrlPressed()))
                            {
                                if (ImGui.Button("Remove##" + "hook" + i))
                                {
                                    hookExists[i] = false;
                                    ReorderHooks = true;
                                }
                            }
                            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            {
                                ImGui.SetTooltip("Ctrl Click to Enable");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }
        }

        //adds an image to the gallery with the specified index with a table 4 columns wide
        public void AddImageToGallery(Plugin plugin, int imageIndex)
        {
            if (TabOpen[TabValue.Gallery])
            {
                using var table = ImRaii.Table("table_name", 4);
                if (table)
                {
                    for (var i = 0; i < imageIndex; i++)
                    {
                        ImGui.TableNextColumn();
                        DrawGalleryImage(i);
                    }
                }
            }
        }
        public void DrawHooksUI(Plugin plugin, int hookCount)
        {
            if (TabOpen[TabValue.Hooks])
            {
                for (var i = 0; i < hookCount; i++)
                {
                    DrawHook(i, plugin);
                }
            }
        }

        //gets the next image index that does not exist
        public static int NextAvailableImageIndex()
        {
            var load = true;
            var index = 0;
            for (var i = 0; i < ImageExists.Length; i++)
            {
                if (ImageExists[i] == false && load == true)
                {
                    load = false;
                    index = i;
                    return index;
                }
            }
            return index;
        }
        //gets the next chapter index that does not exist
        public static int NextAvailableChapterIndex()
        {
            var load = true;
            var index = 0;
            for (var i = 0; i < storyChapterExists.Length; i++)
            {
                if (storyChapterExists[i] == false && load == true)
                {
                    load = false;
                    index = i;
                    return index;
                }
            }
            return index;
        }
        //gets the next hook index that does not exist
        public int NextAvailableHookIndex()
        {
            var load = true;
            var index = 0;
            for (var i = 0; i < hookExists.Length; i++)
            {
                if (hookExists[i] == false && load == true)
                {
                    load = false;
                    index = i;
                    return index;
                }
            }
            return index;
        }


        public void DrawGalleryImage(int i)
        {
            if (ImageExists[i] == true)
            {

                using var galleryImageChild = ImRaii.Child("##GalleryImage" + i, new Vector2(150, 280));
                if (galleryImageChild)
                {
                    ImGui.Text("Will this image be 18+ ?");
                    if (ImGui.Checkbox("Yes 18+", ref NSFW[i]))
                    {
                        for (var g = 0; g < galleryImageCount; g++)
                        {
                            //send galleryImages on value change of 18+ incase the user forgets to hit submit gallery
                            DataSender.SendGalleryImage(plugin.username, plugin.playername, plugin.playerworld,
                                              NSFW[g], TRIGGER[g], imageURLs[g], g);

                        }
                    }
                    ImGui.Text("Is this a possible trigger ?");
                    if (ImGui.Checkbox("Yes Triggering", ref TRIGGER[i]))
                    {
                        for (var g = 0; g < galleryImageCount; g++)
                        {
                            //same for triggering, we don't want to lose this info if the user is forgetful
                            DataSender.SendGalleryImage(plugin.username, plugin.playername, plugin.playerworld,
                                              NSFW[g], TRIGGER[g], imageURLs[g], g);
                        }
                    }
                    ImGui.InputTextWithHint("##ImageURL" + i, "Image URL", ref imageURLs[i], 300);
                    try
                    {
                        //maximize the gallery image to preview it.
                        ImGui.Image(galleryThumbs[i].ImGuiHandle, new Vector2(galleryThumbs[i].Width, galleryThumbs[i].Height));
                        if (ImGui.IsItemHovered()) { ImGui.SetTooltip("Click to enlarge"); }
                        if (ImGui.IsItemClicked())
                        {
                            ImagePreview.width = galleryImages[i].Width;
                            ImagePreview.height = galleryImages[i].Height;
                            ImagePreview.PreviewImage = galleryImages[i];
                            loadPreview = true;
                        }


                        using var galleryImageControlsTable = ImRaii.Child("##GalleryImageControls" + i);
                        if (galleryImageControlsTable)
                        {
                            using (OtterGui.Raii.ImRaii.Disabled(!Plugin.CtrlPressed()))
                            {
                                //button to remove the gallery image
                                if (ImGui.Button("Remove##" + "gallery_remove" + i))
                                {
                                    ImageExists[i] = false;
                                    ReorderGallery = true;
                                    //remove the image immediately once pressed
                                    DataSender.RemoveGalleryImage(plugin.playername, plugin.playerworld, i, galleryImageCount);
                                }
                            }
                            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            {
                                ImGui.SetTooltip("Ctrl Click to Enable");
                            }

                        }
                    }
                    catch (Exception ex)
                    {
                    }
                }

            }






        }
        //method to reset the entire gallery to default (NOT CURRENTLY IN USE)
        public static async void ResetGallery()
        {
            try
            {
                for (var g = 0; g < galleryImages.Length; g++)
                {
                    galleryImageCount = 0;
                    ReorderGallery = true;
                }
                for (var i = 0; i < 30; i++)
                {
                    ImageExists[i] = false;
                }
                for (var i = 0; i < galleryImages.Length; i++)
                {
                    galleryImages[i] = pictureTab;
                    galleryThumbs[i] = pictureTab;
                }
            }
            catch (Exception ex)
            {
                // plugin.logger.Error("Could not reset gallery:: Results may be incorrect.");
            }
        }
        public static void RemoveExistingGallery()
        {
            for (var i = 0; i < galleryImages.Length; i++)
            {
                galleryImages[i]?.Dispose();
                galleryImages[i] = null;
            }
            for (var i = 0; i < galleryThumbs.Length; i++)
            {
                galleryThumbs[i]?.Dispose();
                galleryThumbs[i] = null;
            }
            for (var i = 0; i < galleryImagesList.Count; i++)
            {
                galleryImages[i]?.Dispose();
                galleryImages[i] = null;
            }
            for (var i = 0; i < galleryThumbsList.Count; i++)
            {
                galleryThumbsList[i]?.Dispose();
                galleryThumbsList[i] = null;
            }

        }
        //method ot reset the entire story section
        public static void ResetStory()
        {
            for (var s = 0; s < storyChapterCount; s++)
            {
                ChapterNames[s] = string.Empty;
                ChapterContents[s] = string.Empty;
                chapterCount = 0;
                storyChapterExists[s] = false;
            }


            currentChapter = 0;
            chapterCount = 0;
            storyChapterCount = -1;
            storyTitle = string.Empty;
        }

        //reset our tabs and go back to base ui with no tab selected
        public static void ClearUI()
        {
            TabOpen[TabValue.Bio] = false;
            TabOpen[TabValue.Hooks] = false;
            TabOpen[TabValue.Story] = false;
            TabOpen[TabValue.OOC] = false;
            TabOpen[TabValue.Gallery] = false;
        }

        public void Dispose()
        {
            avatarHolder?.Dispose();
            avatarHolder = null;
            pictureTab?.Dispose();
            pictureTab = null;
            currentAvatarImg?.Dispose();
            currentAvatarImg = null;
            persistAvatarHolder?.Dispose();
            persistAvatarHolder = null;
            for (var i = 0; i < galleryImagesList.Count; i++)
            {
                galleryImagesList[i]?.Dispose();
                galleryImagesList[i] = null;
            }
            for (var i = 0; i < galleryThumbsList.Count; i++)
            {
                galleryThumbsList[i]?.Dispose();
                galleryThumbsList[i] = null;
            }
        }

        public void AddChapterSelection()
        {
            var chapterName = ChapterNames[currentChapter];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Chapter", chapterName);
            if (!combo)
                return;
            foreach (var (newText, idx) in ChapterNames.WithIndex())
            {
                var label = newText;
                if (label == string.Empty)
                {
                    label = "New Chapter";
                }
                if (newText != string.Empty)
                {
                    if (ImGui.Selectable(label + "##" + idx, idx == currentChapter))
                    {
                        currentChapter = idx;
                        storyChapterExists[currentChapter] = true;
                        viewChapter[currentChapter] = true;
                        drawChapter = true;
                    }
                    ImGuiUtil.SelectableHelpMarker("Select to edit chapter");
                }
            }
        }
        public void AddAlignmentSelection()
        {
            var (text, desc) = Defines.AlignmentVals[currentAlignment];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Alignment", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;

            foreach (var ((newText, newDesc), idx) in Defines.AlignmentVals.WithIndex())
            {
                if (ImGui.Selectable(newText, idx == currentAlignment))
                    currentAlignment = idx;

                ImGuiUtil.SelectableHelpMarker(newDesc);
            }
        }
        public void AddPersonalitySelection_1()
        {
            var (text, desc) = Defines.PersonalityValues[currentPersonality_1];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Personality Feature #1", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;

            foreach (var ((newText, newDesc), idx) in Defines.PersonalityValues.WithIndex())
            {
                if (ImGui.Selectable(newText, idx == currentPersonality_1))
                    currentPersonality_1 = idx;

                ImGuiUtil.SelectableHelpMarker(newDesc);
            }
        }
        public void AddPersonalitySelection_2()
        {
            var (text, desc) = Defines.PersonalityValues[currentPersonality_2];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Personality Feature #2", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;

            foreach (var ((newText, newDesc), idx) in Defines.PersonalityValues.WithIndex())
            {
                if (ImGui.Selectable(newText, idx == currentPersonality_2))
                    currentPersonality_2 = idx;

                ImGuiUtil.SelectableHelpMarker(newDesc);
            }
        }
        public void AddPersonalitySelection_3()
        {
            var (text, desc) = Defines.PersonalityValues[currentPersonality_3];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Personality Feature #3", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;

            foreach (var ((newText, newDesc), idx) in Defines.PersonalityValues.WithIndex())
            {
                if (ImGui.Selectable(newText, idx == currentPersonality_3))
                    currentPersonality_3 = idx;

                ImGuiUtil.SelectableHelpMarker(newDesc);
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

                            string name= Misc.ExtractTextBetweenTags(backupContent, "name");
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

                            plugin.logger.Error($"Total hooks loaded: {hookData.Count}");


                            string storytitle = Misc.ExtractTextBetweenTags(backupContent, "storytitle");
                            // Story section with chapter extraction logic
                            string chapterPattern = @"<storychapters>(.*?)</storychapters>";
                            Regex chapterRegex = new Regex(chapterPattern, RegexOptions.Singleline);  // Ensure multiline content is captured

                            string chapterNamePattern = @"<chaptername>(.*?)</chaptername>";
                            string chapterContentPattern = @"<chaptercontent>(.*?)</chaptercontent>";

                            // Match the entire story chapters block
                            MatchCollection chapterTagMatches = chapterRegex.Matches(backupContent);

                            plugin.logger.Error($"Chapter tag matches found: {chapterTagMatches.Count}");

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

                            plugin.logger.Error($"Total chapters loaded: {chapterData.Count}");


                            storyData.chapters = chapterData;
                            // OOC Info
                            string OOC = Misc.ExtractTextBetweenTags(backupContent, "ooc");

                            // Gallery section with logic to extract info for the images and nsfw / trigger data
                            string galleryPattern = @"<gallery>(.*?)</gallery>";
                            Regex galleryRegex = new Regex(galleryPattern, RegexOptions.Singleline);

                            string galleryNSFWPattern = @"<nsfw>(.*?)</nsfw>";
                            string galleryTRIGGERPattern = @"<trigger>(.*?)</trigger>";
                            string galleryUrlPattern = @"<url>(.*?)</url>";

                            MatchCollection galleryMatches = galleryRegex.Matches(backupContent);

                            // Loop through each <gallery> block (we expect only one, but handling as a collection)
                            foreach (Match galleryMatch in galleryMatches)
                            {
                                string galleryContent = galleryMatch.Groups[1].Value; // Content inside <gallery>...</gallery>

                                // Extract all <nsfw>, <trigger>, and <url> within the single gallery block
                                MatchCollection nsfwMatches = Regex.Matches(galleryContent, galleryNSFWPattern);
                                MatchCollection triggerMatches = Regex.Matches(galleryContent, galleryTRIGGERPattern);
                                MatchCollection urlMatches = Regex.Matches(galleryContent, galleryUrlPattern);

                                for (int i = 0; i < urlMatches.Count; i++)
                                {
                                    bool nsfw = bool.TryParse(nsfwMatches[i].Groups[1].Value, out bool nsfwResult) ? nsfwResult : false;
                                    bool trigger = bool.TryParse(triggerMatches[i].Groups[1].Value, out bool triggerResult) ? triggerResult : false;
                                    string url = urlMatches[i].Groups[1].Value;

                                    if (!string.IsNullOrWhiteSpace(url))
                                    {
                                        galleryimagedata.Add(new ProfileGalleryImage { url = url, nsfw = nsfw, trigger = trigger });
                                    }
                                }
                            }

                            plugin.logger.Error($"Total images loaded: {galleryimagedata.Count}");

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
                            DataSender.SubmitProfileBio(plugin.playername, plugin.playerworld,
                                                  avatarBytesData, name, race, gender, age, height, weight, afg, alignment, pers1, pers2, pers3);
                            var hooks = new List<Tuple<int, string, string>>();
                            for (var i = 0; i < hookData.Count; i++)
                            {
                                //create a new hook tuple to add to the list
                                var hook = Tuple.Create(i, hookData[i].name, hookData[i].content);
                                hooks.Add(hook);
                            }
                            DataSender.SendHooks(plugin.playername, plugin.playerworld, hooks);

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
                            DataSender.SendStory(plugin.playername, plugin.playerworld, storytitle, storyChapters);

                            for (var i = 0; i < galleryimagedata.Count; i++)
                            {
                                //pretty simple stuff, just send the gallery related array values to the server
                                DataSender.SendGalleryImage(plugin.username, plugin.playername, plugin.playerworld,
                                                    galleryimagedata[i].nsfw, galleryimagedata[i].trigger, galleryimagedata[i].url, i);

                            }
                            //send the OOC info to the server, just a string really
                            DataSender.SendOOCInfo(plugin.playername, plugin.playerworld, OOC);

                            DataSender.FetchProfile(plugin.playername, plugin.playerworld);




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
       

        public async Task DeleteProfile(string password)
        {
            DataSender.DeleteProfile(plugin.username, password, plugin.playername, plugin.playerworld);
        }



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
            DataSender.SubmitProfileBio(plugin.playername, plugin.playerworld,
                                                  avatarBytes,
                                                  bioFieldsArr[(int)Defines.BioFieldTypes.name].Replace("'", "''"),
                                                  bioFieldsArr[(int)Defines.BioFieldTypes.race].Replace("'", "''"),
                                                  bioFieldsArr[(int)Defines.BioFieldTypes.gender].Replace("'", "''"),
                                                  bioFieldsArr[(int)Defines.BioFieldTypes.age].Replace("'", "''"),
                                                  bioFieldsArr[(int)Defines.BioFieldTypes.height].Replace("'", "''"),
                                                  bioFieldsArr[(int)Defines.BioFieldTypes.weight].Replace("'", "''"),
                                                  bioFieldsArr[(int)Defines.BioFieldTypes.afg].Replace("'", "''"),
                                                  currentAlignment, currentPersonality_1, currentPersonality_2, currentPersonality_3);
            var hooks = new List<Tuple<int, string, string>>();
            for (var i = 0; i < hookCount; i++)
            {
                //create a new hook tuple to add to the list
                var hook = Tuple.Create(i, HookNames[i], HookContents[i]);
                hooks.Add(hook);
            }
            DataSender.SendHooks(plugin.playername, plugin.playerworld, hooks);

            //create a new list for our stories to be held in
            var storyChapters = new List<Tuple<string, string>>();
            for (var i = 0; i < storyChapterCount + 1; i++)
            {
                //get the data from our chapterNames and Content and store them in a tuple ot be added in the storyChapters list
                var chapterName = ChapterNames[i].ToString();
                var chapterContent = ChapterContents[i].ToString();
                var chapter = Tuple.Create(chapterName, chapterContent);
                storyChapters.Add(chapter);
            }
            //finally send the story data to the server
            DataSender.SendStory(plugin.playername, plugin.playerworld, storyTitle, storyChapters);

            for (var i = 0; i < galleryImageCount; i++)
            {
                //pretty simple stuff, just send the gallery related array values to the server
                DataSender.SendGalleryImage(plugin.username, plugin.playername, plugin.playerworld,
                                    NSFW[i], TRIGGER[i], imageURLs[i], i);

            }
            //send the OOC info to the server, just a string really
            DataSender.SendOOCInfo(plugin.playername, plugin.playerworld, oocInfo);

            DataSender.FetchProfile(plugin.playername, plugin.playerworld);

        }
        public void SaveBackupFile()
        {
            _fileDialogManager.SaveFileDialog("Save Backup", "Data{.dat, .json}", "backup", ".dat", (s, f) =>
            {
                if (!s)
                    return;
                var dataPath = f.ToString();

                using (StreamWriter writer = new StreamWriter($"{dataPath}"))
                {
                    // Write avatarBytes as base64
                    string avatarBts = Convert.ToBase64String(avatarBytes);
                    // Bio fields
                    writer.WriteLine($"<avatar>{avatarBts}</avatar>");
                    writer.WriteLine($"<name>{EscapeTagContent(bioFieldsArr[(int)Defines.BioFieldTypes.name])}</name>");
                    writer.WriteLine($"<race>{EscapeTagContent(bioFieldsArr[(int)Defines.BioFieldTypes.race])}</race>");
                    writer.WriteLine($"<gender>{EscapeTagContent(bioFieldsArr[(int)Defines.BioFieldTypes.gender])}</gender>");
                    writer.WriteLine($"<age>{EscapeTagContent(bioFieldsArr[(int)Defines.BioFieldTypes.age])}</age>");
                    writer.WriteLine($"<height>{EscapeTagContent(bioFieldsArr[(int)Defines.BioFieldTypes.height])}</height>");
                    writer.WriteLine($"<weight>{EscapeTagContent(bioFieldsArr[(int)Defines.BioFieldTypes.weight])}</weight>");
                    writer.WriteLine($"<afg>{EscapeTagContent(bioFieldsArr[(int)Defines.BioFieldTypes.afg])}</afg>");
                    writer.WriteLine($"<alignment>{currentAlignment}</alignment>");
                    writer.WriteLine($"<personality_1>{currentPersonality_1}</personality_1>");
                    writer.WriteLine($"<personality_2>{currentPersonality_2}</personality_2>");
                    writer.WriteLine($"<personality_3>{currentPersonality_3}</personality_3>");

                    // Hooks
                    writer.WriteLine("<hooks>");
                    for (int i = 0; i < hookCount; i++)
                    {
                        plugin.logger.Error($"saving hook {i} with name{HookNames[i]} and content {HookContents[i]}");
                        writer.WriteLine($"<hookname>{EscapeTagContent(HookNames[i])}</hookname>");
                        writer.WriteLine($"<hookcontent>{EscapeTagContent(HookContents[i])}</hookcontent>");
                    }
                    writer.WriteLine("</hooks>");

                    // Story chapters
                    writer.WriteLine($"<storytitle>{storyTitle}</storytitle>");      
                    writer.WriteLine("<storychapters>");
                    for (int i = 0; i <= storyChapterCount; i++)
                    {
                        plugin.logger.Error($"saving chapter {i} with name{ChapterNames[i]} and content {ChapterContents[i]}");
                        writer.WriteLine($"<chaptername>{EscapeTagContent(ChapterNames[i])}</chaptername>");
                        writer.WriteLine($"<chaptercontent>{EscapeTagContent(ChapterContents[i])}</chaptercontent>");
                    }
                    writer.WriteLine("</storychapters>");

                    // OOC info
                    writer.WriteLine($"<ooc>{EscapeTagContent(oocInfo)}</ooc>");

                    // Gallery
                    writer.WriteLine("<gallery>");
                    for (int i = 0; i < galleryImageCount; i++)
                    {
                        writer.WriteLine($"<nsfw>{NSFW[i]}</nsfw>");
                        writer.WriteLine($"<trigger>{TRIGGER[i]}</trigger>");
                        writer.WriteLine($"<url>{EscapeTagContent(imageURLs[i])}</url>");
                    }
                    writer.WriteLine("</gallery>");
                }
            });
        }


         private string ExtractValue(string line, string tag)
        {
            // Extracts the content between <tag> and </tag>, accounting for escaped content
            int startIndex = line.IndexOf($"<{tag}>") + tag.Length + 2;
            int endIndex = line.IndexOf($"</{tag}>", startIndex);

            if (startIndex != -1 && endIndex != -1)
            {
                string content = line.Substring(startIndex, endIndex - startIndex);
                return UnescapeTagContent(content); // Unescape content when loading
            }
            return string.Empty;
        }
        private int SafeParseInt(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return 0; // Or some default value, depending on your use case
            }

            if (int.TryParse(input, out int result))
            {
                return result;
            }
            else
            {
                // Handle the case where the string is not a valid number
                throw new FormatException($"The input string '{input}' is not in a correct format.");
            }
        }

        // Helper function to escape special characters in tag content
        private string EscapeTagContent(string content)
        {
            return content.Replace("\\", "\\\\")   // Escape backslashes
                          .Replace("<", "\\<")     // Escape opening tags
                          .Replace(">", "\\>");    // Escape closing tags
        }

        // Helper function to unescape special characters when reading content
        private string UnescapeTagContent(string content)
        {
            return content.Replace("\\>", ">")     // Unescape closing tags
                          .Replace("\\<", "<")     // Unescape opening tags
                          .Replace("\\\\", "\\");  // Unescape backslashes
        }

        public void EditImage(bool avatar, int imageIndex)
        {
            _fileDialogManager.OpenFileDialog("Select Image", "Image{.png,.jpg}", (s, f) =>
            {
                if (!s)
                    return;
                var imagePath = f[0].ToString();
                var image = Path.GetFullPath(imagePath);
                var imageBytes = File.ReadAllBytes(image);
                if (avatar == true)
                {
                    avatarBytes = File.ReadAllBytes(imagePath);
                }
            }, 0, null, configuration.AlwaysOpenDefaultImport);

        }
        public static void ReloadProfile()
        {
            DataReceiver.BioLoadStatus = -1;
            DataReceiver.GalleryLoadStatus = -1;
            DataReceiver.HooksLoadStatus = -1;
            DataReceiver.StoryLoadStatus = -1;
        }
        public static void ClearOnLoad()
        {
            if (AllLoaded())
            {
                ClearUI();
            }
        }
    }
}


