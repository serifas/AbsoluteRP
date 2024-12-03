using AbsoluteRoleplay.Windows.Ect;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRoleplay.Windows.Profiles.ProfileTabs
{
    internal class GalleryTab
    {
        public static bool addGalleryImageGUI, ReorderGallery;
        public static int galleryImageCount = 0;
        public static bool loadPreview;
        public static string[] imageURLs = new string[31];
        public static bool[] NSFW = new bool[31]; //gallery images NSFW status
        public static bool[] TRIGGER = new bool[31]; //gallery images TRIGGER status
        public static bool[] ImageExists = new bool[31]; //used to check if an image exists in the gallery
        public static string[] imageTooltips = new string[31];
        public static List<IDalamudTextureWrap> galleryThumbsList = new List<IDalamudTextureWrap>();
        public static List<IDalamudTextureWrap> galleryImagesList = new List<IDalamudTextureWrap>();
        public static IDalamudTextureWrap[] galleryImages, galleryThumbs = new IDalamudTextureWrap[31];

        public static void LoadGalleryTab()
        {
            if (galleryImageCount < 0)
            {
                galleryImageCount = 0;
            }
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
        //adds an image to the gallery with the specified index with a table 4 columns wide
        public static void AddImageToGallery(Plugin plugin, int imageIndex)
        {
            if (ProfileWindow.TabOpen[TabValue.Gallery])
            {
                using var table = ImRaii.Table("table_name", 4);
                if (table)
                {
                    for (var i = 0; i < imageIndex; i++)
                    {
                        ImGui.TableNextColumn();
                        DrawGalleryImage(plugin, i);
                    }
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
        public static void DrawGalleryImage(Plugin plugin, int i)
        {
            if (ImageExists[i] == true)
            {

                ImGui.Text("Will this image be 18+ ?");
                if (ImGui.Checkbox("Yes 18+##" + i, ref NSFW[i]))
                {
                    for (var g = 0; g < galleryImageCount; g++)
                    {
                        //send galleryImages on value change of 18+ incase the user forgets to hit submit gallery
                        DataSender.SendGalleryImage(ProfileWindow.currentProfile, NSFW[g], TRIGGER[g], imageURLs[g], imageTooltips[i], g);
                    }
                }
                ImGui.Text("Is this a possible trigger ?");
                if (ImGui.Checkbox("Yes Triggering##" + i, ref TRIGGER[i]))
                {
                    for (var g = 0; g < galleryImageCount; g++)
                    {
                        //same for triggering, we don't want to lose this info if the user is forgetful
                        DataSender.SendGalleryImage(ProfileWindow.currentProfile, NSFW[g], TRIGGER[g], imageURLs[g], imageTooltips[i], g);
                    }
                }
                ImGui.InputTextWithHint("##ImageURL" + i, "Image URL", ref imageURLs[i], 300);
                ImGui.InputTextWithHint("##ImageInfo" + i, "Info", ref imageTooltips[i], 400);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Tooltip of the image on hover");
                }

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
                using (OtterGui.Raii.ImRaii.Disabled(!Plugin.CtrlPressed()))
                {
                    //button to remove the gallery image
                    if (ImGui.Button("Remove##" + "gallery_remove" + i))
                    {
                        ImageExists[i] = false;
                        ReorderGallery = true;
                        //remove the image immediately once pressed
                        DataSender.RemoveGalleryImage(ProfileWindow.currentProfile, plugin.playername, plugin.playerworld, i, galleryImageCount);
                    }
                }
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.SetTooltip("Ctrl Click to Enable");
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
                }
                for (var i = 0; i < 30; i++)
                {
                    ImageExists[i] = false;
                }
                for (var i = 0; i < galleryImages.Length; i++)
                {
                    galleryImages[i] = ProfileWindow.pictureTab;
                    galleryThumbs[i] = ProfileWindow.pictureTab;
                }
            }
            catch (Exception ex)
            {
                // plugin.logger.Error("Could not reset gallery:: Results may be incorrect.");
            }
        }

        public static void ReorderGalleryData()
        {
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
            galleryImages[galleryImageCount] = ProfileWindow.pictureTab;
            galleryThumbs[galleryImageCount] = ProfileWindow.pictureTab;
            //set the image to not exist until added again
            ImageExists[galleryImageCount] = false;

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
    }
}
