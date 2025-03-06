using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Networking;
using OtterGui;
using OtterGui.Raii;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace AbsoluteRoleplay.Windows.Listings
{
    internal class ListingsWindow : Window, IDisposable
    {
        public Plugin plugin;
        public static Configuration configuration;
        public static Vector2 buttonScale;
        public static List<BaseListingData> listings = new List<BaseListingData>();
        public static string loading; //loading status string for loading the profile gallery mainly
        public static float percentage = 0f; //loading base value
        public static int loaderInd = 0;
        private FileDialogManager _fileDialogManager; //for banners only at the moment
        public static IDalamudTextureWrap banner;
        public static byte[] bannerBytes;
        public static int currentType = 0;

        public bool DrawListingCreation { get; private set; }

        public ListingsWindow(Plugin plugin) : base(
       "LISTINGS", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {

                MinimumSize = new Vector2(200, 400),
                MaximumSize = new Vector2(1000, 1000)
            };

            this.plugin = plugin;
            configuration = plugin.Configuration;
            _fileDialogManager = new FileDialogManager();
        }
        public override void OnOpen()
        {

        }
        public override void Draw()
        {
            LoadListingSelection(currentType);
        }
        public void Dispose()
        {

        }
        public void LoadListingTypeSelection()
        {

        }
        public static void LoadListingSelection(int type)
        {
            var (id, text, image) = UI.ListingNavigationVals[0];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Listings", text);
            ImGuiUtil.HoverTooltip(text);
            if (!combo)
                return;
            foreach (var ((newID, newText, newImg), idx) in UI.ListingNavigationVals.WithIndex())
            {
                if (id == type)
                {
                    if (ImGui.Selectable(newText, idx == type))
                        type = idx;
                    ImGuiUtil.SelectableHelpMarker(text);
                }

            }


        }
    }
}
