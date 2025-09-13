using AbsoluteRP.Windows.Ect;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Networking;
using System.Numerics;
using static Dalamud.Interface.Utility.Raii.ImRaii;

namespace AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes
{
    internal class Gallery
    {
        public static bool addGalleryImageGUI, ReorderGallery;
        public static int galleryImageCount = 0;
        public static bool loadPreview;
        private static bool viewable = true;

        public static void RenderGalleryPreview(GalleryLayout galleryLayout, Vector4 TitleColor)
        {
            Misc.SetTitle(Plugin.plugin, true, galleryLayout.name, TitleColor);

            using var table = ImRaii.Table("GalleryTargetTable", 4);
            if (table)
            {
                Vector2 cellSize = ImGui.GetContentRegionAvail() / 4f; // 4 columns
                foreach (ProfileGalleryImage image in galleryLayout.images)
                {
                    ImGui.TableNextColumn();
                    if (image.image == null && image.image.Handle == IntPtr.Zero)
                    {
                        image.image = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                        image.thumbnail = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                    }
                    else if (image.thumbnail != null && image.thumbnail.Handle != IntPtr.Zero)
                    {
                        // Calculate scaled size while keeping aspect ratio
                        float aspect = (float)image.thumbnail.Width / Math.Max(1, image.thumbnail.Height);
                        float maxWidth = cellSize.X * 0.9f;
                        float maxHeight = cellSize.Y * 0.9f;
                        float width = maxWidth;
                        float height = width / aspect;
                        if (height > maxHeight)
                        {
                            height = maxHeight;
                            width = height * aspect;
                        }
                        Vector2 scaledSize = new Vector2(width, height);
                        if (image.image != null && image.image.Handle != IntPtr.Zero)
                        {
                            ImGui.Image(image.thumbnail.Handle, scaledSize);
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                Misc.RenderHtmlElements(image.tooltip, false, true, true, true, null, true);
                                ImGui.Separator();
                                ImGui.Text("Click to enlarge");
                                ImGui.EndTooltip();
                            }
                            if (ImGui.IsItemClicked())
                            {
                                if (image.image != null && image.image.Handle != IntPtr.Zero)
                                {
                                    ImagePreview.width = image.image.Width;
                                    ImagePreview.height = image.image.Height;
                                    ImagePreview.PreviewImage = image.image;
                                    loadPreview = true;
                                }
                            }
                        }

                    }
                }
            }
            if (loadPreview)
            {
                Plugin.plugin.OpenImagePreview();
                loadPreview = false;
            }
        }
        public static void RenderGalleryLayout(int index, string uniqueID, GalleryLayout layout)
        {
            /*
            ImGui.Checkbox($"Viewable##Viewable{layout.id}", ref viewable);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("If checked, this tab will be viewable by others.\nIf unchecked, it will not be displayed.");
            }*/

            if (ImGui.Button("Add Image"))
            {
                byte[] baseImageBytes = new byte[0];
                if (Plugin.PluginInterface is { AssemblyLocation.Directory.FullName: { } imagePath })
                {
                    baseImageBytes = Misc.ImageToByteArray(Path.Combine(imagePath, "UI/common/profiles/galleries/picturetab.png"));
                }
                layout.images.Add(new ProfileGalleryImage()
                {
                    index = layout.images.Count,
                    imageBytes = baseImageBytes,
                    image = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab),
                    thumbnail = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab),
                    nsfw = false,
                    trigger = false,
                    tooltip = string.Empty,
                    url = string.Empty
                });
            }


            ImGui.NewLine();
            galleryImageCount = layout.images?.Count ?? 0;
            AddImagesToGallery(Plugin.plugin, layout);
        }

        //adds an image to the gallery with the specified index with a table 4 columns wide
        public static void AddImagesToGallery(Plugin plugin, GalleryLayout layout)
        {            
            using var table = ImRaii.Table("table_name", 4);
            if (table)
            {
                for (var i = 0; i < layout.images?.Count; i++)
                {
                    if (layout.images[i] == null)
                        continue;

                    ImGui.TableNextColumn();
                    DrawGalleryImage(plugin, layout.images[i], layout, i);
                }
            }
        }
        //gets the next image index that does not exist
        public static int NextAvailableImageIndex(GalleryLayout layout)
        {
            var load = true;
            var index = 0;
            for (var i = 0; i < layout.images.Count; i++)
            {
                if (layout.images[i] != null && load == true)
                {
                    load = false;
                    index = i;
                    return index;
                }
            }
            return index;
        }
        public static void DrawGalleryImage(Plugin plugin, ProfileGalleryImage image, GalleryLayout layout, int i)
        {

            ImGui.Text("Will this image be 18+ ?");
            ImGui.Checkbox("Yes 18+##" + i, ref image.nsfw);
            ImGui.Text("Is this a possible trigger ?");
            ImGui.Checkbox("Yes Triggering##" + i, ref image.trigger);
            ImGui.InputTextWithHint("##ImageURL" + i, "Image URL", ref image.url, 300);
            ImGui.InputTextWithHint("##ImageInfo" + i, "Info", ref image.tooltip, 400);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Tooltip of the image on hover");
            }

            //maximize the gallery image to preview it.
            if (image.thumbnail != null && image.thumbnail.Handle != null && image.thumbnail.Handle != IntPtr.Zero)
            {
                ImGui.Image(image.thumbnail.Handle, new Vector2(image.thumbnail.Width, image.thumbnail.Height));
            }
            else
            {
                ImGui.Text("Thumbnail not available.");
            }
            if (ImGui.IsItemHovered()) { ImGui.SetTooltip("Click to enlarge"); }
            if (ImGui.IsItemClicked())
            {
                ImagePreview.width = image.image.Width;
                ImagePreview.height = image.image.Height;
                ImagePreview.PreviewImage = image.image;
                loadPreview = true;
            }
            if(ImGui.Button("Upload##" + i))
            {
                Misc.EditImage(plugin, ProfileWindow._fileDialogManager, layout, false, false, i);
            }
            using (ImRaii.Disabled(!Plugin.CtrlPressed()))
            {
                //button to remove the gallery image
                if (ImGui.Button("Remove##" + "gallery_remove" + i))
                {
                    layout.images.RemoveAt(i); 
                    for (int j = 0; j < layout.images.Count; j++)
                    {
                        layout.images[j].index = j;
                    }
                    Plugin.PluginLog.Error(i.ToString());
                    //remove the image immediately once pressed
                    DataSender.RemoveGalleryImage(Plugin.character, ProfileWindow.profileIndex, i, layout.tabIndex);
                }
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Ctrl Click to Enable");
            }       
        }
   
     
    }
}
