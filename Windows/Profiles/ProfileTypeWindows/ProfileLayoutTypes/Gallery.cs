using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI;
using AbsoluteRP.Windows.Ect;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Networking;
using System.Numerics;
using AbsoluteRP.RsUI.Pages;
using AbsoluteRP.Network;
using static Dalamud.Interface.Utility.Raii.ImRaii;

namespace AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes
{
    // Gallery tab layout - displays a grid of images that can be clicked to preview. Supports adding/removing images, reordering, NSFW/trigger flags, and tooltips.
    internal class Gallery
    {
        public static bool addGalleryImageGUI, ReorderGallery;
        public static int galleryImageCount = 0;
        public static bool loadPreview;
        private static bool viewable = true;

        public static void RenderGalleryPreview(GalleryLayout galleryLayout, Vector4 TitleColor)
        {
            Misc.SetTitle(Plugin.plugin, true, galleryLayout.name, TitleColor);

            if (galleryLayout.images == null || galleryLayout.images.Count == 0) return;

            // Mosaic / collage layout: greedy row-packing where each row's images share a common height, and that height is chosen so the row's total width exactly matches the container. Wide shots stretch, tall shots narrow, gaps stay uniform - the "Google Photos" / "Flickr Justified" look the reference image is based on.
            float gap = RsTheme.S(4f);
            float targetRowH = RsTheme.S(110f);
            float minRowH = RsTheme.S(60f);
            float maxRowH = RsTheme.S(190f);
            float containerW = ImGui.GetContentRegionAvail().X;
            if (containerW < 1f) return;

            var images = galleryLayout.images;
            int i = 0;
            while (i < images.Count)
            {
                // Grow the row until adding another image would push the effective row height below the target - that's the sweet spot where images are close to their intended size.
                int rowStart = i;
                float aspectSum = 0f;
                int rowEnd = rowStart;
                while (rowEnd < images.Count)
                {
                    float a = SafeAspect(images[rowEnd]);
                    float nextSum = aspectSum + a;
                    // Effective height if we commit the row here.
                    float effH = (containerW - gap * (rowEnd - rowStart)) / Math.Max(nextSum, 0.001f);
                    if (effH < targetRowH && rowEnd > rowStart) break;
                    aspectSum = nextSum;
                    rowEnd++;
                }
                if (rowEnd == rowStart) { i++; continue; } // safety

                bool isLastRow = rowEnd >= images.Count;
                int count = rowEnd - rowStart;
                float rowH = (containerW - gap * (count - 1)) / Math.Max(aspectSum, 0.001f);
                // Last row: don't stretch a lone landscape shot to full width - keep it at target so trailing rows look natural.
                if (isLastRow) rowH = Math.Min(rowH, targetRowH);
                rowH = Math.Clamp(rowH, minRowH, maxRowH);

                Vector2 rowStartPos = ImGui.GetCursorScreenPos();
                float x = rowStartPos.X;
                for (int k = rowStart; k < rowEnd; k++)
                {
                    var image = images[k];
                    if (image == null) continue;
                    if (image.image == null || image.image.Handle == IntPtr.Zero)
                    {
                        image.image = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                    }
                    if (image.thumbnail == null || image.thumbnail.Handle == IntPtr.Zero)
                    {
                        image.thumbnail = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                    }

                    float aspect = SafeAspect(image);
                    float w = rowH * aspect;
                    ImGui.SetCursorScreenPos(new Vector2(x, rowStartPos.Y));
                    DrawGalleryTile(image, new Vector2(w, rowH));
                    x += w + gap;
                }

                // Advance the parent cursor to the next row.
                ImGui.SetCursorScreenPos(new Vector2(rowStartPos.X, rowStartPos.Y + rowH + gap));
                i = rowEnd;
            }

            if (loadPreview)
            {
                Plugin.plugin.OpenImagePreview();
                loadPreview = false;
            }
        }

        // Aspect ratio (w/h) with sensible defaults so placeholder-loaded images don't collapse to zero-width slivers. Read straight off the full-resolution image - the pre-baked thumbnail is a downscale that comes back blurry when the mosaic sizes it up.
        private static float SafeAspect(ProfileGalleryImage image)
        {
            switch (GalleryMedia.KindOf(image?.url)) { case 1: return 16f / 9f; case 2: return 1.6f; }
            var img = image?.image;
            if (img != null && img.Handle != IntPtr.Zero && img.Height > 0)
                return Math.Clamp((float)img.Width / img.Height, 0.35f, 3.5f);
            var t = image?.thumbnail;
            if (t != null && t.Handle != IntPtr.Zero && t.Height > 0)
                return Math.Clamp((float)t.Width / t.Height, 0.35f, 3.5f);
            return 1f;
        }

        // One mosaic cell: image + hover tooltip + click-to-preview. Draws the full-resolution image directly - Dalamud already has it uploaded to the GPU, so letting the driver downsample at draw time gives a sharper tile than the pre-baked (and aggressively compressed) thumbnail texture.
        private static void DrawGalleryTile(ProfileGalleryImage image, Vector2 size)
        {
            // Video / audio entries: a play tile that opens the player.
            if (GalleryMedia.IsMedia(image?.url))
            {
                if (GalleryMedia.DrawTile(image.url, size, image.tooltip)) GalleryMedia.Play(image.url, image.tooltip);
                return;
            }
            bool censored = image.nsfw || image.trigger;
            var tex = image.image != null && image.image.Handle != IntPtr.Zero
                    ? image.image : image.thumbnail;
            if (tex == null || tex.Handle == IntPtr.Zero) { ImGui.Dummy(size); return; }

            // Kick off a one-shot background job to build the blurred censor texture the first time we see a flagged image. The blur runs on a heavily downscaled copy so the O(w*h*r ) cost stays trivial; the result caches on the image itself.
            if (censored && !image.blurRequested)
            {
                image.blurRequested = true;
                EnsureBlurredAsync(image);
            }

            var drawList = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            var pmax = pos + size;

            if (censored && image.blurredImage != null && image.blurredImage.Handle != IntPtr.Zero)
            {
                ImGui.Image(image.blurredImage.Handle, size);
            }
            else if (censored)
            {
                // Blur not built yet - solid scrim so the raw image never flashes on-screen even for one frame.
                ImGui.Dummy(size);
                drawList.AddRectFilled(pos, pmax, ImGui.ColorConvertFloat4ToU32(new Vector4(0.08f, 0.08f, 0.10f, 1f)));
            }
            else
            {
                // Immersive themes may cut the tile with a mask image.
                var gdoc = AbsoluteRP.Immersive.ImmersiveMode.IsActive ? AbsoluteRP.Immersive.ImmersiveMode.Theme.Document : null;
                var maskRef = gdoc?.GalleryMask;
                Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap? maskedTex = null;
                if (maskRef != null && maskRef.IsSet && image.imageBytes != null && image.imageBytes.Length > 0)
                    maskedTex = AbsoluteRP.Immersive.Themes.ThemeAssets.Masked("gallery:" + image.index + ":" + image.imageBytes.Length, image.imageBytes,
                        AbsoluteRP.Immersive.Themes.ThemeAssets.Find(gdoc, maskRef.Asset));
                if (maskedTex != null && maskedTex.Handle != IntPtr.Zero) ImGui.Image(maskedTex.Handle, size);
                else ImGui.Image(tex.Handle, size);
            }

            if (censored)
                DrawCensorOverlay(drawList, pos, size, image.nsfw, image.trigger);

            // Immersive: the theme cuts the tile's rim in its own material (torn parchment, scorched paper) so images read as part of the surface rather than pasted on it.
            if (AbsoluteRP.Immersive.ImmersiveMode.IsActive)
            {
                var thm = AbsoluteRP.Immersive.ImmersiveMode.Theme;
                var frame = thm.Document?.GalleryFrame;
                if (frame != null && frame.IsSet)
                {
                    var fpad = RsUI.RsTheme.S(30f);
                    drawList.PushClipRect(pos - new Vector2(fpad), pmax + new Vector2(fpad), false);
                    AbsoluteRP.Immersive.Themes.ThemeAssets.Draw(drawList, thm.Document, frame, pos, pmax, 1f);
                    drawList.PopClipRect();
                }
                else thm.DrawImageEdges(drawList, pos, pmax, 1f, "tile" + image.index);
            }

            if (ImGui.IsItemHovered())
            {
                // Immersive: pop the tile forward with a mouse-driven skew on the foreground drawlist so it visibly leaps toward the viewer over neighbouring tiles. The regular tooltip path is skipped in this mode - the pop-out IS the hover feedback.
                if (AbsoluteRP.Immersive.ImmersiveMode.IsActive
                    && !censored
                    && tex != null && tex.Handle != IntPtr.Zero)
                {
                    DrawImmersivePopOut(tex.Handle, pos, size);
                }
                else if (AbsoluteRP.Immersive.ImmersiveMode.IsActive)
                {
                    // Censored tile in immersive mode: themed tooltip.
                    AbsoluteRP.Immersive.ImmersiveMode.Tooltip("Click to enlarge");
                }
                else
                {
                    ImGui.BeginTooltip();
                    Misc.RenderHtmlElements(image.tooltip, false, true, true, true, null, true);
                    ImGui.Separator();
                    ImGui.Text("Click to enlarge");
                    ImGui.EndTooltip();
                }
            }
            if (ImGui.IsItemClicked() && image.image != null && image.image.Handle != IntPtr.Zero)
            {
                ImagePreview.width = image.image.Width;
                ImagePreview.height = image.image.Height;
                ImagePreview.PreviewImage = image.image;
                loadPreview = true;
            }
        }

        // Foreground-drawn pop of a gallery tile for immersive mode: the tile lifts toward the viewer with a uniform scale (aspect and pixels untouched - no skew, no warp), a soft under-glow, a hologram frame with corner brackets, and a caption.
        private static void DrawImmersivePopOut(ImTextureID texHandle, Vector2 origMin, Vector2 origSize)
        {
            var fg = ImGui.GetForegroundDrawList();
            var center = origMin + origSize * 0.5f;
            var grow = 1.18f;
            var half = origSize * (0.5f * grow);
            var min = center - half;
            var max = center + half;
            var holo = AbsoluteRP.Immersive.ImmersiveMode.Holo;
            var theme = AbsoluteRP.Immersive.ImmersiveMode.Theme;

            // Glow / mat BEHIND the image, then the untouched image, then only edge treatment on top - the picture is never tinted.
            theme.DrawPopBack(fg, min, max, 1f);
            fg.AddImage(texHandle, min, max);
            var popFrame = theme.Document?.GalleryFrame;
            if (popFrame != null && popFrame.IsSet) AbsoluteRP.Immersive.Themes.ThemeAssets.Draw(fg, theme.Document, popFrame, min, max, 1f);
            else theme.DrawPopFrame(fg, min, max, 1f);

            var caption = "CLICK TO ENLARGE";
            var csz = ImGui.CalcTextSize(caption);
            var cy = max.Y - csz.Y - RsUI.RsTheme.S(6f);
            fg.AddRectFilledMultiColor(new Vector2(min.X, cy - RsUI.RsTheme.S(10f)), new Vector2(max.X, max.Y),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0f)), ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0f)),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.7f)), ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.7f)));
            fg.AddText(new Vector2(center.X - csz.X * 0.5f, cy), AbsoluteRP.Immersive.ImmersiveMode.Col(holo, 0.95f), caption);
        }

        // Overlay for censored tiles: a centered label ("NSFW", "TRIGGERING", or "NSFW & TRIGGERING") flanked by horizontal accent gradient lines that fade in from the tile edges. Draws straight to the current window's drawlist so it composites on top of the image and shares the tile's hover/click rect.
        private static void DrawCensorOverlay(ImDrawListPtr dl, Vector2 pos, Vector2 size, bool nsfw, bool trigger)
        {
            // Slight dark scrim on top of the blur so light-toned blurs don't wash out the label.
            uint scrim = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.28f));
            dl.AddRectFilled(pos, pos + size, scrim);

            string label = (nsfw && trigger) ? "NSFW & TRIGGERING"
                         : nsfw               ? "NSFW"
                         : "TRIGGERING";

            // Color coding - NSFW warns amber, trigger warns red, both together lands on red to match the higher-severity flag.
            Vector4 accent = (nsfw && trigger) ? new Vector4(1.00f, 0.30f, 0.30f, 1f)
                         : nsfw                ? new Vector4(1.00f, 0.72f, 0.20f, 1f)
                         :                       new Vector4(1.00f, 0.30f, 0.30f, 1f);
            uint accentU  = ImGui.ColorConvertFloat4ToU32(accent);
            uint accentT  = ImGui.ColorConvertFloat4ToU32(new Vector4(accent.X, accent.Y, accent.Z, 0f));
            uint textCol  = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.96f));

            // Fit the label to the tile: skip it entirely if the tile is too small for even a compact form.
            var raw = ImGui.CalcTextSize(label);
            if (raw.X + RsTheme.S(20f) > size.X)
            {
                // Try the shortest form the flags allow.
                if (nsfw && trigger) label = "NSFW+TRIG";
                else if (nsfw)        label = "18+";
                else                  label = "TW";
                raw = ImGui.CalcTextSize(label);
                if (raw.X + RsTheme.S(8f) > size.X || raw.Y * 3f > size.Y) return;
            }

            float lineH = Math.Max(1f, RsTheme.S(1.5f));
            float labelPad = RsTheme.S(6f);
            float sidePad = RsTheme.S(10f);
            float lineY1 = pos.Y + (size.Y - raw.Y) * 0.5f - labelPad - lineH;
            float lineY2 = pos.Y + (size.Y + raw.Y) * 0.5f + labelPad;
            float lineX0 = pos.X + sidePad;
            float lineX1 = pos.X + size.X - sidePad;
            float midX   = (lineX0 + lineX1) * 0.5f;

            // Transparent-at-edges -> accent-at-center. Two rects each, side by side, so the accent color sweeps through the middle.
            dl.AddRectFilledMultiColor(
                new Vector2(lineX0, lineY1), new Vector2(midX, lineY1 + lineH),
                accentT, accentU, accentU, accentT);
            dl.AddRectFilledMultiColor(
                new Vector2(midX, lineY1), new Vector2(lineX1, lineY1 + lineH),
                accentU, accentT, accentT, accentU);
            dl.AddRectFilledMultiColor(
                new Vector2(lineX0, lineY2), new Vector2(midX, lineY2 + lineH),
                accentT, accentU, accentU, accentT);
            dl.AddRectFilledMultiColor(
                new Vector2(midX, lineY2), new Vector2(lineX1, lineY2 + lineH),
                accentU, accentT, accentT, accentU);

            var textPos = new Vector2(pos.X + (size.X - raw.X) * 0.5f, pos.Y + (size.Y - raw.Y) * 0.5f);
            // 1px drop shadow keeps the label legible over any blur tone.
            dl.AddText(textPos + new Vector2(1f, 1f), ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.85f)), label);
            dl.AddText(textPos, textCol, label);
        }

        // One-shot background build of the blurred censor texture. Downsamples imageBytes to a ~96px thumbnail before blurring so BlurBytes' O(w*h*r ) cost stays trivial even on 4K sources.
        private static void EnsureBlurredAsync(ProfileGalleryImage image)
        {
            var srcBytes = image.imageBytes;
            if (srcBytes == null || srcBytes.Length < 12) return;
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    byte[] blurredBytes;
                    using (var ms = new System.IO.MemoryStream(srcBytes))
                    using (var bmp = new System.Drawing.Bitmap(ms))
                    {
                        int maxDim = 96;
                        int tw = bmp.Width, th = bmp.Height;
                        if (tw > 0 && th > 0)
                        {
                            double r = Math.Min((double)maxDim / tw, (double)maxDim / th);
                            if (r < 1.0)
                            {
                                tw = Math.Max(8, (int)(bmp.Width * r));
                                th = Math.Max(8, (int)(bmp.Height * r));
                            }
                        }
                        using var small = new System.Drawing.Bitmap(bmp, new System.Drawing.Size(tw, th));
                        blurredBytes = small.BlurBytes(4);
                    }
                    var tex = await Plugin.TextureProvider.CreateFromImageAsync(blurredBytes);
                    image.blurredImage = tex;
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog?.Debug("Gallery blur build failed: " + ex.Message);
                    // Let a future frame retry after ResetAllData / re-fetch clears the flag.
                    image.blurRequested = false;
                }
            });
        }
        public static void RenderGalleryLayout(int index, string uniqueID, GalleryLayout layout)
        {
            /*
            ImGui.Checkbox($"Viewable##Viewable{layout.id}", ref viewable);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("If checked, this tab will be viewable by others.\nIf unchecked, it will not be displayed.");
            }*/

            if (RsElements.Button("Add Image", RsElements.ButtonVariant.Primary))
            {
                byte[] baseImageBytes = new byte[0];
                if (Plugin.PluginInterface is { AssemblyLocation.Directory.FullName: { } imagePath })
                {
                    baseImageBytes = Misc.ImageToByteArray(Path.Combine(imagePath, "UI/profiles/galleries/picturetab.png"));
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


            ImGui.SameLine();
            using (ImRaii.Disabled(GalleryMedia.Uploading))
            {
                if (RsElements.Button("Add Video / Audio", RsElements.ButtonVariant.Ghost))
                {
                    GalleryMedia.PickAndUpload(url =>
                    {
                        layout.images.Add(new ProfileGalleryImage()
                        {
                            index = layout.images.Count,
                            url = url,
                            tooltip = string.Empty,
                        });
                    });
                }
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("Videos (mp4, webm, mov, mkv, m4v) up to 100 MB and audio (mp3, ogg, wav, flac, m4a) up to 30 MB.\nThey play in the video player or the mp3 player when someone clicks them.");
            if (!string.IsNullOrEmpty(GalleryMedia.UploadStatus))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, GalleryMedia.Uploading ? RsTheme.AccentWarning : RsTheme.TextSecondary);
                ImGui.TextWrapped(GalleryMedia.UploadStatus);
                ImGui.PopStyleColor();
            }

            ImGui.NewLine();
            galleryImageCount = layout.images?.Count ?? 0;
            AddImagesToGallery(Plugin.plugin, layout);

            // Consume the click-to-preview flag on the edit path too - DrawGalleryImage sets it but only RenderGalleryPreview used to poll it, so previews were queuing invisibly until the user opened the target profile viewer.
            if (loadPreview)
            {
                Plugin.plugin.OpenImagePreview();
                loadPreview = false;
            }
        }

        // adds an image to the gallery with the specified index with a table 4 columns wide
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
        // gets the next image index that does not exist
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
            RsElements.Checkbox("Yes 18+##" + i, ref image.nsfw);
            ImGui.Text("Is this a possible trigger ?");
            RsElements.Checkbox("Yes Triggering##" + i, ref image.trigger);
            RsElements.InputText("##ImageURL" + i, ref image.url, 300, "Image URL");
            RsElements.InputText("##ImageInfo" + i, ref image.tooltip, 400, "Info");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Tooltip of the image on hover");
            }

            if (GalleryMedia.IsMedia(image.url))
            {
                // A video / audio entry: a play tile instead of a thumbnail and no image upload.
                if (GalleryMedia.DrawTile(image.url, new Vector2(RsTheme.S(250f), RsTheme.S(140f)), image.tooltip)) GalleryMedia.Play(image.url, image.tooltip);
                if (GalleryMedia.KindOf(image.url) == 1)
                {
                    var entry = image;
                    using (ImRaii.Disabled(GalleryMedia.Uploading))
                    {
                        if (RsElements.Button((string.IsNullOrEmpty(GalleryMedia.PosterOf(image.url)) ? "Add cover picture" : "Change cover picture") + "##gcover" + i, RsElements.ButtonVariant.Ghost))
                            GalleryMedia.PickCover(u => entry.url = GalleryMedia.WithPoster(entry.url, u));
                    }
                    if (!string.IsNullOrEmpty(GalleryMedia.PosterOf(image.url)))
                    {
                        ImGui.SameLine();
                        if (RsElements.Button("Remove cover##gcoverrm" + i, RsElements.ButtonVariant.Ghost)) image.url = GalleryMedia.BaseUrl(image.url);
                    }
                }
                RsElements.InputText("##MediaInfo" + i, ref image.tooltip, 400, "Caption shown on hover");
                using (ImRaii.Disabled(!Plugin.CtrlPressed()))
                {
                    if (RsElements.Button("Remove##" + "gallery_remove_media" + i, RsElements.ButtonVariant.Danger))
                    {
                        layout.images.RemoveAt(i);
                        for (int j = 0; j < layout.images.Count; j++) layout.images[j].index = j;
                        ProfileTabs_DS.RemoveGalleryImage(Plugin.character, ProfilesPage.profileIndex, i, layout.tabIndex);
                    }
                }
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip("Ctrl Click to Enable");
                return;
            }

            // maximize the gallery image to preview it.
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
            if (RsElements.Button("Upload##" + i, RsElements.ButtonVariant.Ghost))
            {
                Misc.EditImage(plugin, ProfilesPage._fileDialogManager, layout, false, false, i);
            }
            using (ImRaii.Disabled(!Plugin.CtrlPressed()))
            {
                // button to remove the gallery image
                if (RsElements.Button("Remove##" + "gallery_remove" + i, RsElements.ButtonVariant.Danger))
                {
                    layout.images.RemoveAt(i); 
                    for (int j = 0; j < layout.images.Count; j++)
                    {
                        layout.images[j].index = j;
                    }
                    Plugin.PluginLog.Debug(i.ToString());
                    // remove the image immediately once pressed
                    ProfileTabs_DS.RemoveGalleryImage(Plugin.character, ProfilesPage.profileIndex, i, layout.tabIndex);
                }
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Ctrl Click to Enable");
            }       
        }
   
     
    }
}
