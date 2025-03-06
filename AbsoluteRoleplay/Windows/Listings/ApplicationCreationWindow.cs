using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Networking;
using OtterGui.Raii;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace AbsoluteRoleplay.Windows.Listings
{
    internal class ApplicationCreationWindow : Window, IDisposable
    {
        public Plugin plugin;
        public static Configuration configuration;

        public bool DrawListingCreation { get; private set; }

        public ApplicationCreationWindow(Plugin plugin) : base(
       "APPLICATION", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {

                MinimumSize = new Vector2(200, 400),
                MaximumSize = new Vector2(1000, 1000)
            };

            this.plugin = plugin;
            configuration = plugin.Configuration;
        }
        public override void OnOpen()
        {
            
        }
        public override void Draw()
        {

        }
        public void Dispose()
        {

        }
    }
}
