using Dalamud.Interface.Internal;
using OtterGui.Log;
using System;
using System.Drawing;
using System.IO;
using System.Net;
using Dalamud.Plugin;
using Networking;
using ImGuiNET;
using JetBrains.Annotations;
using System.Drawing.Imaging;
using Dalamud.Interface.Utility;
using AbsoluteRoleplay.Windows.Profiles;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Generic;
using AbsoluteRoleplay.Windows.Profiles.ProfileTabs;
using System.Net.Http;

namespace AbsoluteRoleplay.Helpers
{
    internal static class Imaging
    {
        public static Plugin plugin;
        private static Dictionary<uint, IconInfo?> IconInfoCache = [];
      
        public static IDalamudTextureWrap DownloadImage(string url)
        {
            bool isImage = IsImageUrlAsync(url).GetAwaiter().GetResult();
            if(!isImage)
            {
                return UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
            }
            byte[] bannerBytes = null;
                //set the avatar to the avatar_holder.png by default
            if (Plugin.PluginInterface is { AssemblyLocation.Directory.FullName: { } path })
            {
                bannerBytes = File.ReadAllBytes(Path.Combine(path, "UI/common/blank.png"));
            }
            
            IDalamudTextureWrap image = Plugin.TextureProvider.CreateFromImageAsync(bannerBytes).Result;
            using (WebClient webClient = new WebClient())
            {
                try
                {
                    // Download the image data as a byte array
                    byte[] imageBytes = webClient.DownloadData(url);

                    // Convert the byte array to a Dalamud texture
                    image = Plugin.TextureProvider.CreateFromImageAsync(imageBytes).Result;
                }
                catch (Exception ex)
                {
                    // Handle exceptions, e.g., logging errors or falling back to default image
                    plugin.logger.Error($"Failed to download image from {url}: {ex.Message}");
                }
            }
            
            return image;
        }
        public static void DownloadProfileImage(bool self, string url, string tooltip, int profileID, bool nsfw, bool trigger, Plugin plugin, int index)
         {
            bool isImage = IsImageUrlAsync(url).GetAwaiter().GetResult();
            if (isImage)
             {
                 try
                 {
                     using (WebClient webClient = new WebClient())
                     {
                         // Get image data as byte array
                         byte[] imageBytes = webClient.DownloadData(url);
    
                         using (MemoryStream ms = new MemoryStream(imageBytes))
                         {
                             // Load the image from the memory stream
                             Image baseImage = Image.FromStream(ms);
    
                             // Convert scaled image to byte array
                             byte[] scaledImageBytes = ImageToByteArray(baseImage); 
                             
                             // If self is true, process for ProfileWindow, else for TargetWindow
                            if (self)
                            {
                                var image = Plugin.TextureProvider.CreateFromImageAsync(scaledImageBytes).Result;
                                if (image != null)
                                {
                                    GalleryTab.galleryImages[index] = image;
                                }
                                if (url.Contains("absolute-roleplay"))
                                {
                                    url = string.Empty;
                                }
                                GalleryTab.imageURLs[index] = url;
                                GalleryTab.NSFW[index] = nsfw;
                                GalleryTab.TRIGGER[index] = trigger;
                                GalleryTab.imageTooltips[index] = tooltip;
                                GalleryTab.imageBytes[index] = scaledImageBytes;
                             }
                             else
                             {
                                 var image = Plugin.TextureProvider.CreateFromImageAsync(scaledImageBytes).Result;
                                 if (image != null)
                                 {
                                    TargetWindow.galleryImages[index] = image;
                                    TargetWindow.imageTooltips[index] = tooltip;
                                 }
                             }
    
                             // Handle NSFW/trigger thumbnail logic
                             if (trigger && !nsfw)
                             {
                                 var triggerImage = UI.UICommonImage(UI.CommonImageTypes.TRIGGER);
                                 if (self)
                                 {
                                     GalleryTab.galleryThumbs[index] = triggerImage;
                                 }
                                 else
                                 {
                                     TargetWindow.galleryThumbs[index] = triggerImage;
                                 }
                             }
                             else if (nsfw && !trigger)
                             {
                                 var nsfwImage = UI.UICommonImage(UI.CommonImageTypes.NSFW);
                                 if (self)
                                 {
                                     GalleryTab.galleryThumbs[index] = nsfwImage;
                                 }
                                 else
                                 {
                                     TargetWindow.galleryThumbs[index] = nsfwImage;
                                 }
                             }
                             else if (nsfw && trigger)
                             {
                                 var nsfwTriggerImage = UI.UICommonImage(UI.CommonImageTypes.NSFWTRIGGER);
                                 if (self)
                                 {
                                     GalleryTab.galleryThumbs[index] = nsfwTriggerImage;
                                 }
                                 else
                                 {
                                     TargetWindow.galleryThumbs[index] = nsfwTriggerImage;
                                 }
                             }
                             else if (!nsfw && !trigger)
                             {
                                 // Scale and create the thumbnail
                                 Image thumbImage = ScaleImage(baseImage, 200,200);
                                 byte[] thumbImageBytes = ImageToByteArray(thumbImage);
    
                                 var thumbTexture = Plugin.TextureProvider.CreateFromImageAsync(thumbImageBytes).Result;
                                 if (self)
                                 {
                                    GalleryTab.galleryThumbs[index] = thumbTexture;
                                 }
                                 else
                                 {
                                     TargetWindow.galleryThumbs[index] = thumbTexture;
                                 }
                             }
                         }
                     }
                 }
                 catch (Exception ex)
                 {
                     plugin.logger.Error($"Unable to Read or Process Image: {ex.Message}");
                 }
             }
         }

     

        static string GetImageFileExtension(string url)
        {
            try
            {
                // Send a HEAD request to fetch only the headers
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "HEAD";

                // Get the response
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    // Check if the Content-Type header indicates an image
                    if (response.ContentType.ToLower().StartsWith("image/"))
                    {
                        // Extract the file extension from the Content-Type header
                        string contentType = response.ContentType;
                        string fileExtension = GetFileExtensionFromContentType(contentType);

                        return fileExtension;
                    }
                    else
                    {
                        throw new Exception("The URL does not point to an image.");
                    }
                }
            }
            catch (WebException ex)
            {
                throw new Exception($"An error occurred: {ex.Message}");
            }
        }

        static string GetFileExtensionFromContentType(string contentType)
        {
            switch (contentType.ToLower())
            {
                case "image/jpeg":
                    return "jpg";
                case "image/png":
                    return "png";
                default:
                    throw new Exception("Unsupported image format.");
            }
        }

        private static readonly HttpClient httpClient = new HttpClient();

        public static async Task<bool> IsImageUrlAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            try
            {
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, url))
                {
                    HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[Error] Failed to fetch URL: {response.StatusCode}");
                        return false;
                    }

                    // Check if Content-Type is an image
                    if (response.Content.Headers.ContentType?.MediaType?.StartsWith("image/") == true)
                        return true;
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[Error] HTTP Request failed: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Unexpected error: {ex.Message}");
                return false;
            }

            return false;
        }




        static void SaveImage(Image image, string directoryPath, string fileName)
        {
            // Check if the directory exists, if not, create it
            if (!System.IO.Directory.Exists(directoryPath))
            {
                System.IO.Directory.CreateDirectory(directoryPath);
            }

            // Combine the directory path and file name to get the full file path
            string filePath = System.IO.Path.Combine(directoryPath, fileName);

            // Save the image to the specified file path
            image.Save(filePath);

            Console.WriteLine("Image saved successfully.");
        }
        public static byte[] BlurBytes(this Bitmap image, Int32 blurSize)
        {
            var rectangle = new System.Drawing.Rectangle(0, 0, image.Width, image.Height);
            Bitmap blurred = new Bitmap(image.Width, image.Height);

            // make an exact copy of the bitmap provided
            using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(blurred))
                graphics.DrawImage(image, new System.Drawing.Rectangle(0, 0, image.Width, image.Height),
                    new System.Drawing.Rectangle(0, 0, image.Width, image.Height), System.Drawing.GraphicsUnit.Pixel);

            // look at every pixel in the blur rectangle
            for (Int32 xx = rectangle.X; xx < rectangle.X + rectangle.Width; xx++)
            {
                for (Int32 yy = rectangle.Y; yy < rectangle.Y + rectangle.Height; yy++)
                {
                    Int32 avgR = 0, avgG = 0, avgB = 0;
                    Int32 blurPixelCount = 0;

                    // average the color of the red, green and blue for each pixel in the
                    // blur size while making sure you don't go outside the image bounds
                    for (Int32 x = Math.Max(0, xx - blurSize); x <= Math.Min(xx + blurSize, image.Width - 1); x++)
                    {
                        for (Int32 y = Math.Max(0, yy - blurSize); y <= Math.Min(yy + blurSize, image.Height - 1); y++)
                        {
                            System.Drawing.Color pixel = blurred.GetPixel(x, y);

                            avgR += pixel.R;
                            avgG += pixel.G;
                            avgB += pixel.B;

                            blurPixelCount++;
                        }
                    }

                    avgR = avgR / blurPixelCount;
                    avgG = avgG / blurPixelCount;
                    avgB = avgB / blurPixelCount;

                    // now that we know the average for the blur size, set each pixel to that color
                    for (Int32 x = xx; x < xx + blurSize && x < image.Width && x < rectangle.Width; x++)
                        for (Int32 y = yy; y < yy + blurSize && y < image.Height && y < rectangle.Height; y++)
                            blurred.SetPixel(x, y, System.Drawing.Color.FromArgb(avgR, avgG, avgB));
                }
            }
            return ImageToByteArray(blurred);
        }
       
        public static System.Drawing.Image ScaleImage(System.Drawing.Image image, int maxWidth, int maxHeight)
        {
            int newWidth, newHeight;
            if (image.Width > maxWidth || image.Height > maxHeight)
            {
                // Calculate aspect ratio
                double ratioX = (double)maxWidth / image.Width;
                double ratioY = (double)maxHeight / image.Height;
                double ratio = Math.Min(ratioX, ratioY);

                // Calculate new dimensions
                newWidth = (int)(image.Width * ratio);
                newHeight = (int)(image.Height * ratio);

                // Create new bitmap with new dimensions
                Bitmap newImage = new Bitmap(newWidth, newHeight);

                // Draw the original image on the new bitmap with scaled dimensions
                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(newImage))
                {
                    graphics.DrawImage(image, 0, 0, newWidth, newHeight);
                }

                return newImage;
            }
            else
            {
                return image;
            }
        }
        public static byte[] ScaleImageBytes(byte[] imgBytes, int maxWidth, int maxHeight)
        {
            System.Drawing.Image img = byteArrayToImage(imgBytes);
            var ratioX = (double)maxWidth / img.Width;
            var ratioY = (double)maxHeight / img.Height;
            var ratio = Math.Min(ratioX, ratioY);

            var newWidth = (int)(img.Width * ratio);
            var newHeight = (int)(img.Height * ratio);

            var scaledImage = new Bitmap(newWidth, newHeight);

            using (var graphics = System.Drawing.Graphics.FromImage(scaledImage))
                graphics.DrawImage(img, 0, 0, newWidth, newHeight);

            byte[] scaledBytes = ImageToByteArray(scaledImage);

            return scaledBytes;
        }
        public static byte[] ImageToByteArray(System.Drawing.Image imageIn)
        {
            using (var ms = new MemoryStream())
            {
                imageIn.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return ms.ToArray();
            }
        }
        public static System.Drawing.Image byteArrayToImage(byte[] bytesArr)
        {
            using (MemoryStream memstr = new MemoryStream(bytesArr))
            {
                System.Drawing.Image img = System.Drawing.Image.FromStream(memstr);
                return img;
            }
        }

        internal static void RemoveAllImages(AbsoluteRoleplay.Plugin plugin)
        {
            if (Plugin.PluginInterface is { AssemblyLocation.Directory.FullName: { } path })
            {
                string GalleryPath = Path.Combine(path, "UI/Galleries/");
                if (Directory.Exists(GalleryPath))
                {
                    Directory.Delete(GalleryPath, true);
                }
            }

        }
    }
}
