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
            var player = Plugin.ClientState.LocalPlayer;
            //if we have loaded all the data received from the server and we are logged in game
            if (AllLoaded() == true && plugin.IsOnline())
            {
                _fileDialogManager.Draw(); //file dialog mainly for avatar atm. galleries later possibly.


                if (ExistingProfile == true)//if we have a profile add the edit profile button
                {
                    if (ImGui.Checkbox("Set Private", ref privateProfile))
                    {
                        //send our privacy settings to the server
                        DataSender.SetProfileStatus(plugin.username.ToString(), player.Name.ToString(), player.HomeWorld.GameData.Name.ToString(), privateProfile);
                    }
                    if(ImGui.Button("Create Backup"))
                    {
                        SaveBackupFile();
                    }
                    ImGui.SameLine();
                    if(ImGui.Button("Load Backup"))
                    {
                        LoadBackupFile();
                    }
                    if(ImGui.Button("Save Profile"))
                    {
                        SubmitProfileData();
                    }
                }
                if (ExistingProfile == false) //else create our add profile button to create a new profile
                {
                    if (ImGui.Button("Add Profile", new Vector2(100, 20))) { DataSender.CreateProfile(player.Name.ToString(), player.HomeWorld.GameData.Name.ToString()); }
                }
                else
                {
                    ImGui.Spacing();
                    if (ImGui.Button("Edit Bio", new Vector2(100, 20))) { ClearUI(); TabOpen[TabValue.Bio] = true; }
                    ImGui.SameLine();
                    if (ImGui.Button("Edit Hooks", new Vector2(100, 20))) { ClearUI(); TabOpen[TabValue.Hooks] = true; }
                    ImGui.SameLine();
                    if (ImGui.Button("Edit Story", new Vector2(100, 20))) { ClearUI(); TabOpen[TabValue.Story] = true; }
                    ImGui.SameLine();
                    if (ImGui.Button("Edit OOC Info", new Vector2(100, 20))) { ClearUI(); TabOpen[TabValue.OOC] = true; }
                    ImGui.SameLine();
                    if (ImGui.Button("Edit Gallery", new Vector2(100, 20))) { ClearUI(); TabOpen[TabValue.Gallery] = true; }

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
            var player = Plugin.ClientState.LocalPlayer;

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
                            DataSender.SendGalleryImage(plugin.username, player.Name.ToString(), player.HomeWorld.GameData.Name.ToString(),
                                              NSFW[g], TRIGGER[g], imageURLs[g], g);

                        }
                    }
                    ImGui.Text("Is this a possible trigger ?");
                    if (ImGui.Checkbox("Yes Triggering", ref TRIGGER[i]))
                    {
                        for (var g = 0; g < galleryImageCount; g++)
                        {
                            //same for triggering, we don't want to lose this info if the user is forgetful
                            DataSender.SendGalleryImage(plugin.username, player.Name.ToString(), player.HomeWorld.GameData.Name.ToString(),
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
                                    DataSender.RemoveGalleryImage(player.Name.ToString(), player.HomeWorld.GameData.Name.ToString(), i, galleryImageCount);
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
                        using (StreamReader reader = new StreamReader($"{dataPath}.dat"))
                        {
                            // Read avatar bytes
                            string avatarBts = await reader.ReadLineAsync();
                            avatarBytes = Convert.FromBase64String(avatarBts); // Convert back to byte array
                            currentAvatarImg = Plugin.TextureProvider.CreateFromImageAsync(avatarBytes).Result;

                            // Initialize Bio Fields
                            bioFieldsArr[(int)Defines.BioFieldTypes.name] = await ExtractTagFromFile(dataPath, "name");
                            bioFieldsArr[(int)Defines.BioFieldTypes.race] = await ExtractTagFromFile(dataPath, "race");
                            bioFieldsArr[(int)Defines.BioFieldTypes.gender] = await ExtractTagFromFile(dataPath, "gender");
                            bioFieldsArr[(int)Defines.BioFieldTypes.age] = await ExtractTagFromFile(dataPath, "age");
                            bioFieldsArr[(int)Defines.BioFieldTypes.height] = await ExtractTagFromFile(dataPath, "height");
                            bioFieldsArr[(int)Defines.BioFieldTypes.weight] = await ExtractTagFromFile(dataPath, "weight");
                            bioFieldsArr[(int)Defines.BioFieldTypes.afg] = await ExtractTagFromFile(dataPath, "afg");

                            // For alignment and personality
                            currentAlignment = SafeParseInt(await ExtractTagFromFile(dataPath, "alignment"));
                            currentPersonality_1 = SafeParseInt(await ExtractTagFromFile(dataPath, "personality_1"));
                            currentPersonality_2 = SafeParseInt(await ExtractTagFromFile(dataPath, "personality_2"));
                            currentPersonality_3 = SafeParseInt(await ExtractTagFromFile(dataPath, "personality_3"));

                            // Hooks section
                            List<string> hookNames = new List<string>();
                            List<string> hookContents = new List<string>();

                            string line;
                            bool inHooksSection = false;

                            while ((line = await reader.ReadLineAsync()) != null)
                            {
                                if (line.Trim() == "<hooks>")
                                {
                                    inHooksSection = true;
                                    continue; // Start processing after finding <hooks> tag
                                }
                                if (inHooksSection)
                                {
                                    if (line.Trim() == "</hooks>") break;

                                    if (line.Contains("<hookname>"))
                                    {
                                        hookNames.Add(await ExtractTagFromFile(dataPath, "hookname"));
                                    }
                                    if (line.Contains("<hookcontent>"))
                                    {
                                        hookContents.Add(await ExtractTagFromFile(dataPath, "hookcontent"));
                                    }
                                }
                            }

                            for (int i = 0; i < hookNames.Count; i++)
                            {
                                hookCount = i;
                                hookExists[i] = true;
                            }

                            if (hookNames.Count > 0 && hookContents.Count > 0)
                            {
                                HookNames = hookNames.ToArray();
                                HookContents = hookContents.ToArray();
                            }

                            // Story section
                            storyTitle = await ExtractTagFromFile(dataPath, "storytitle");
                            List<string> chapterNames = new List<string>();
                            List<string> chapterContents = new List<string>();

                            while ((line = await reader.ReadLineAsync()) != null)
                            {
                                if (line.Trim() == "<storychapters>") break;

                                if (!string.IsNullOrWhiteSpace(line))
                                {
                                    chapterNames.Add(await ExtractTagFromFile(dataPath, "chaptername"));
                                    chapterContents.Add(await ExtractTagFromFile(dataPath, "chaptercontent"));
                                }
                            }
                            for (int i = 0; i < chapterNames.Count; i++)
                            {
                                storyChapterCount = i;
                                storyChapterExists[i] = true;
                            }
                            if (chapterNames.Count > 0 && chapterContents.Count > 0)
                            {
                                ChapterNames = chapterNames.ToArray();
                                ChapterContents = chapterContents.ToArray();
                            }

                            // OOC Info
                            oocInfo = await ExtractTagFromFile(dataPath, "ooc");

                            // Gallery section
                            List<bool> nsfwList = new List<bool>();
                            List<bool> triggerList = new List<bool>();
                            List<string> imageURLList = new List<string>();

                            while ((line = await reader.ReadLineAsync()) != null)
                            {
                                if (line.Trim() == "<gallery>") break;

                                if (!string.IsNullOrWhiteSpace(line))
                                {
                                        nsfwList.Add(bool.Parse(await ExtractTagFromFile(dataPath, "nsfw")));
                                        triggerList.Add(bool.Parse(await ExtractTagFromFile(dataPath, "trigger")));
                                        imageURLList.Add(await ExtractTagFromFile(dataPath, "url"));
                                }
                            }
                            for (int i = 0; i < imageURLList.Count; i++)
                            {
                                galleryImageCount = i;
                                ImageExists[i] = true;
                            }
                            if (nsfwList.Count > 0 && triggerList.Count > 0 && imageURLList.Count > 0)
                            {
                                NSFW = nsfwList.ToArray();
                                TRIGGER = triggerList.ToArray();
                                imageURLs = imageURLList.ToArray();
                            }

                            // Redraw the window to ensure content is visible
                            plugin.logger.Error("Profile loaded successfully.");
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
            var player = Plugin.ClientState.LocalPlayer;
            DataSender.SubmitProfileBio(player.Name.ToString(), player.HomeWorld.GameData.Name.ToString(),
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
            DataSender.SendHooks(player.Name.ToString(), player.HomeWorld.GameData.Name.ToString(), hooks);

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
            DataSender.SendStory(player.Name.ToString(), player.HomeWorld.GameData.Name.ToString(), storyTitle, storyChapters);

            for (var i = 0; i < galleryImageCount; i++)
            {
                //pretty simple stuff, just send the gallery related array values to the server
                DataSender.SendGalleryImage(plugin.username, player.Name.ToString(), player.HomeWorld.GameData.Name.ToString(),
                                    NSFW[i], TRIGGER[i], imageURLs[i], i);

            }
            //send the OOC info to the server, just a string really
            DataSender.SendOOCInfo(player.Name.ToString(), player.HomeWorld.GameData.Name.ToString(), oocInfo);

            DataSender.FetchProfile(Plugin.ClientState.LocalPlayer.Name.ToString(), Plugin.ClientState.LocalPlayer.HomeWorld.GameData.Name.ToString());

        }
        public void SaveBackupFile()
        {
            _fileDialogManager.SaveFileDialog("Save Backup", "Data{.dat, .json}", "backup", ".dat", (s, f) =>
            {
                if (!s)
                    return;
                var dataPath = f.ToString();

                using (StreamWriter writer = new StreamWriter($"{dataPath}.dat"))
                {
                    // Write avatarBytes as base64
                    string avatarBts = Convert.ToBase64String(avatarBytes);
                    writer.WriteLine(avatarBts);

                    // Bio fields
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
                        writer.WriteLine($"<hookname>{EscapeTagContent(HookNames[i])}</hookname>");
                        writer.WriteLine($"<hookcontent>{EscapeTagContent(HookContents[i])}</hookcontent>");
                    }
                    writer.WriteLine("</hooks>");

                    // Story chapters
                    writer.WriteLine("<storytitle>");
                    writer.WriteLine(storyTitle); // Assuming this isn't inside a tag
                    writer.WriteLine("</storytitle>");
                    writer.WriteLine("<storychapters>");
                    for (int i = 0; i < storyChapterCount; i++)
                    {
                        writer.WriteLine($"<chaptername>{EscapeTagContent(ChapterNames[i])}</chaptername>");
                        writer.WriteLine($"<chaptercontent>{EscapeTagContent(ChapterContents[i])}</chaptercontent>");
                    }
                    writer.WriteLine("</storychapters>");

                    // OOC info
                    writer.WriteLine("<ooc>");
                    writer.WriteLine(EscapeTagContent(oocInfo));
                    writer.WriteLine("</ooc>");

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


