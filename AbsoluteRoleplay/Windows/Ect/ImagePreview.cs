using AbsoluteRP.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using System.Numerics;

namespace AbsoluteRP.Windows.Ect
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
        public ImagePreview() : base(
       "PREVIEW", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            configuration = Plugin.plugin.Configuration;
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
                if(PreviewImage.Handle != null && PreviewImage.Handle != IntPtr.Zero)
                {
                    // Here you would render the texture at the calculated width and height
                    ImGui.Image(PreviewImage.Handle, new Vector2(scaledWidth, scaledHeight));
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("ImagePreview Draw Debug: " + ex.Message);
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
            if(PreviewImage != null && PreviewImage.Handle != IntPtr.Zero)
            {
                WindowOperations.SafeDispose(PreviewImage);
                PreviewImage = null;
            }
        }


    }
}
