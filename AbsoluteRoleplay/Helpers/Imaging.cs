using AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using JetBrains.Annotations;
using Networking;
using OtterGui.Log;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Http;
namespace AbsoluteRoleplay.Helpers
{
    internal static class Imaging
    {
        public static Plugin plugin;
        private static Dictionary<uint, IconInfo?> IconInfoCache = [];
        public static void LoadIconSelection(Plugin plugin, IconElement currentIcon)
        {
            if (ImGui.Begin("ICONS", ImGuiWindowFlags.AlwaysAutoResize))
            {
                if (!WindowOperations.iconsLoaded)
                {
                    WindowOperations.LoadIconsLazy(plugin); // Load a small batch of icons
                }
                IDalamudTextureWrap? icon = currentIcon.icon;

                WindowOperations.RenderIcons(plugin, false, false, currentIcon, null, ref icon);
            }
            ImGui.End();
        }
        public static async Task<IDalamudTextureWrap> MaskIconWithCircleAsync(
       byte[] iconBytes,
       byte[] maskBytes,
       int width,
       int height,
       ITextureProvider textureProvider)
        {
            // Blend the icon and mask alpha channels
            var result = new byte[width * height * 4];
            for (int i = 0; i < width * height; i++)
            {
                byte ir = iconBytes[i * 4 + 0];
                byte ig = iconBytes[i * 4 + 1];
                byte ib = iconBytes[i * 4 + 2];
                byte ia = iconBytes[i * 4 + 3];
                byte ma = maskBytes[i * 4 + 3];
                result[i * 4 + 0] = ir;
                result[i * 4 + 1] = ig;
                result[i * 4 + 2] = ib;
                result[i * 4 + 3] = (byte)(ia * ma / 255);
            }

            // Use CreateFromImageAsync to create the texture
            return await textureProvider.CreateFromImageAsync(result);
        }
        public static async Task<ProfileGalleryImage> DownloadProfileImage(
        bool self, string url, string tooltip, int profileID, bool nsfw, bool trigger, Plugin plugin, int index)
        {
            Plugin.plugin.logger.Error(url);
            ProfileGalleryImage galleryImage = new ProfileGalleryImage();
            int maxRetries = 5;
            int delayMs = 1500;

            if (Plugin.TextureProvider == null)
            {
                plugin?.logger?.Error("TextureProvider is not initialized.");
                return galleryImage;
            }

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
            using (HttpClientHandler handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            })
            using (HttpClient client = new HttpClient(handler))
            {
                // Download the image as a byte array
                HttpResponseMessage response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();

                    using (MemoryStream ms = new MemoryStream(imageBytes))
                    {
                        Image baseImage = Image.FromStream(ms);

                        // Convert scaled image to byte array
                        byte[] scaledImageBytes = ImageToByteArray(baseImage);

                        // If self is true, process for ProfileWindow, else for TargetWindow
                        var image = await Plugin.TextureProvider.CreateFromImageAsync(scaledImageBytes);
                        Image thumbImage = ScaleImage(baseImage, 200, 200);
                        byte[] thumbImageBytes = ImageToByteArray(thumbImage);

                        var thumbTexture = await Plugin.TextureProvider.CreateFromImageAsync(thumbImageBytes);

                        if (image != null)
                        {
                            if (url.Contains("absolute-roleplay"))
                            {
                                url = string.Empty;
                            }
                        }

                        // Handle NSFW/trigger thumbnail logic
                        if (trigger && !nsfw)
                        {
                            thumbTexture = UI.UICommonImage(UI.CommonImageTypes.TRIGGER);
                        }
                        else if (nsfw && !trigger)
                        {
                            thumbTexture = UI.UICommonImage(UI.CommonImageTypes.NSFW);
                        }
                        else if (nsfw && trigger)
                        {
                            thumbTexture = UI.UICommonImage(UI.CommonImageTypes.NSFWTRIGGER);
                        }
                        galleryImage.url = url;
                        galleryImage.image = image;
                        galleryImage.tooltip = tooltip;
                        galleryImage.nsfw = nsfw;
                        galleryImage.trigger = trigger;
                        galleryImage.thumbnail = thumbTexture;
                        galleryImage.imageBytes = scaledImageBytes;
                        return galleryImage;
                    }
                }

                        else if (
                                 response.StatusCode == (HttpStatusCode)429 ||
                                 response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                                 response.StatusCode == HttpStatusCode.GatewayTimeout ||
                                 response.StatusCode == HttpStatusCode.BadGateway ||         // 502
                                 response.StatusCode == HttpStatusCode.InternalServerError   // 500
                             )
                        {
                    plugin?.logger?.Warning($"Image download attempt {attempt} failed with {response.StatusCode}. Retrying in {delayMs}ms...");
                    await Task.Delay(delayMs * attempt); // Exponential backoff
                    continue;
                }
                else
                {
                    plugin?.logger?.Error($"Image download failed with status code: {response.StatusCode}");
                    break;
                }
                if (!response.IsSuccessStatusCode)
                {
                    plugin?.logger?.Warning($"Non-success status code: {(int)response.StatusCode} ({response.StatusCode})");
                }
            }
        }
        catch (HttpRequestException httpEx)
        {
            plugin?.logger?.Error($"HTTP Request Error: {httpEx.Message}");
            if (attempt < maxRetries)
                await Task.Delay(delayMs * attempt);
        }
        catch (TaskCanceledException)
        {
            plugin?.logger?.Error("Download request timed out.");
            if (attempt < maxRetries)
                await Task.Delay(delayMs * attempt);
        }
        catch (Exception ex)
        {
            plugin?.logger?.Error($"Unexpected error: {ex.Message}");
            break;
        }
    }
    return galleryImage;
}
        public static async Task<byte[]> FetchUrlImageBytes(string url)
        {
            
            try
            {
                using (HttpClientHandler handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                })
                using (HttpClient client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromSeconds(10); // Set a timeout of 10 seconds
                    // Download the image as a byte array
                    byte[] imageBytes = await client.GetByteArrayAsync(url);
                    return imageBytes;
                }
            }
            catch (Exception ex)
            {
                plugin?.logger?.Error($"Error fetching image bytes: {ex.Message}");
                return Array.Empty<byte>();
            }
        }
        public static async Task<IDalamudTextureWrap> DownloadElementImage(bool self, string url, ImageElement element)
        {
            
            IDalamudTextureWrap image = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
            try
            {
                if (Plugin.TextureProvider == null)
                {
                    plugin?.logger?.Error("TextureProvider is not initialized.");
                }
                else
                {
                    using (HttpClientHandler handler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                    })
                    using (HttpClient client = new HttpClient(handler))
                    {
                        client.Timeout = TimeSpan.FromSeconds(10); // Set a timeout of 10 seconds


                        // Download the image as a byte array
                        byte[] imageBytes = await client.GetByteArrayAsync(url);

                        using (MemoryStream ms = new MemoryStream(imageBytes))
                        {
                            Image baseImage = Image.FromStream(ms);

                            // Convert scaled image to byte array
                            byte[] scaledImageBytes = ImageToByteArray(baseImage);
                            element.bytes = scaledImageBytes; // Store the byte array in the element

                            // If self is true, process for ProfileWindow, else for TargetWindow
                            var img = await Plugin.TextureProvider.CreateFromImageAsync(scaledImageBytes);
                            element.textureWrap = img;
                            image = img;
                            return image;
                        }
                    }

                }
            }
            catch (HttpRequestException httpEx)
            {
                plugin?.logger?.Error($"HTTP Request Error: {httpEx.Message}");
            }
            catch (TaskCanceledException)
            {
                plugin?.logger?.Error("Download request timed out.");
            }
            catch (Exception ex)
            {
                plugin?.logger?.Error($"Unexpected error: {ex.Message}");
            }
            return image;
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
