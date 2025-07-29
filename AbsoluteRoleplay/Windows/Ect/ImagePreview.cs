using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;
using ImGuiNET;
using Networking;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using static Dalamud.Interface.Windowing.Window;
using static Lumina.Data.Files.ScdFile;

namespace AbsoluteRoleplay.Windows.Ect
{
    public class ImagePreview : Window, IDisposable
    {
        public static IDalamudTextureWrap PreviewImage;
        public static bool isAdmin;
        public Configuration configuration;
        public static bool WindowOpen;
        public string msg;
        public bool openedProfile = false;
        public static int width = 0, height = 0;
        public bool openedTargetProfile = false;
        public ImagePreview(Plugin plugin) : base(
       "PREVIEW", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            configuration = plugin.Configuration;
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(100, 100),
                MaximumSize = new Vector2(2000, 2000)
            };
        }

        public override void Draw()
        {
            try
            {

                (var scaledWidth, var scaledHeight) = CalculateScaledDimensions();
                if(PreviewImage.ImGuiHandle != null && PreviewImage.ImGuiHandle != IntPtr.Zero)
                {
                    // Here you would render the texture at the calculated width and height
                    ImGui.Image(PreviewImage.ImGuiHandle, new Vector2(scaledWidth, scaledHeight));
                }
            }
            catch (Exception ex)
            {
                Plugin.plugin.logger.Error("ImagePreview Draw Error: " + ex.Message);
            }
        }
        private void GetImGuiWindowDimensions(out int width, out int height)
        {
            var windowSize = ImGui.GetWindowSize();
            width = (int)windowSize.X;
            height = (int)windowSize.Y;
        }

        private (int, int) CalculateScaledDimensions()
        {
            GetImGuiWindowDimensions(out var windowWidth, out var windowHeight);

            // Calculate the aspect ratios
            var windowAspect = (float)windowWidth / windowHeight;
            var textureAspect = (float)width / height;

            // Determine the scale factor
            int newWidth, newHeight;
            if (windowAspect > textureAspect)
            {
                // Window is wider relative to the texture's aspect ratio
                newHeight = windowHeight;
                newWidth = (int)(newHeight * textureAspect);
            }
            else
            {
                // Window is taller relative to the texture's aspect ratio
                newWidth = windowWidth;
                newHeight = (int)(newWidth / textureAspect);
            }

            return (newWidth, newHeight);
        }



        public void Dispose()
        {
            WindowOpen = false;
            if(PreviewImage != null && PreviewImage.ImGuiHandle != IntPtr.Zero)
            {
               WindowOperations.SafeDispose(PreviewImage);
            }
        }


    }
}
