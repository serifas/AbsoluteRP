using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Ect;
using AbsoluteRP.Windows.NavLayouts;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Social.Views;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using Networking;
using System.Numerics;
using System.Reflection;

namespace AbsoluteRP.Windows.Listings
{
    internal class SocialWindow : Window, IDisposable
    {
        public static Configuration configuration;
        public static int navIndex = 0;
        public static Vector2 buttonScale;
        public static List<Listing> listings = new List<Listing>();
        public static List<Listing> communityListings = new List<Listing>();
        public static string loading; //loading status string for loading the tooltipData gallery mainly
        public static float percentage = 0f; //loading base value
        public static int loaderInd = 0;
        private FileDialogManager _fileDialogManager; //for banners only at the moment
        public static IDalamudTextureWrap banner;
        public static byte[] bannerBytes;
        public static int connections = 1;
        public static int bookmarks = 2;
        public static int search = 3;
        public static int groups = 4;
        public static int view = 1;
        public static bool AnyComboTargeted => Search.PageCountComboOpen || Search.CategoryComboOpen || Search.RegionComboOpen || Search.DataCenterComboOpen || Search.WorldComboOpen || Connections.ConnectionComboOpen;
        public bool DrawListingCreation { get; private set; }

        public SocialWindow() : base(
       "SOCIAL", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {

                MinimumSize = new Vector2(200, 400),
                MaximumSize = new Vector2(1000, 1000)
            };

            configuration = Plugin.plugin.Configuration;
            Search.worldSearchQuery = "Adamantoise";
            _fileDialogManager = new FileDialogManager();
        }
        public override void OnOpen()
        {
            if (ClientTCP.IsConnected())
            {
                DataSender.RequestBookmarks(Plugin.character);
                DataSender.RequestConnections(Plugin.character);
                DataSender.FetchProfiles(Plugin.character);
                DataSender.FetchGroups(Plugin.character);
            }
        }

        public override void Draw()
        {
            // Diagnostic check before we attempt to request focus.
            var hovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows);
            var clicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left);
            var anyActive = ImGui.IsAnyItemActive();
            var alreadyFocused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
            
            var focusRequested = hovered && clicked && !anyActive && !alreadyFocused;

            try
            {
                // Main panel position/size
                Vector2 mainPanelPos = ImGui.GetWindowPos();
                Vector2 mainPanelSize = ImGui.GetWindowSize();

                if (view == connections)
                {
                    Connections.LoadConnectionsUI();
                }
                if (view == bookmarks)
                {
                    Bookmarks.DrawBookmarksUI();
                }
                if (view == search)
                {
                    Search.LoadSearch();
                }
                if (view == groups)
                {
                    Groups.LoadGroupList();
                }

                // Move focus decision after the UI has been drawn so per-item flags are up-to-date.
                if (focusRequested
                    && !ImGui.IsAnyItemActive()
                    && !AnyComboTargeted)
                {
                    ImGui.SetWindowFocus("SocialNavigation");
                    ImGui.SetWindowFocus("SOCIAL");
                }

                // Navigation panel
                float headerHeight = 48f;
                float buttonSize = ImGui.GetIO().FontGlobalScale * 45;
                int buttonCount = 5;
                float navHeight = buttonSize * buttonCount * 1.2f;
                ImGui.SetNextWindowPos(new Vector2(mainPanelPos.X - buttonSize * 1.2f, mainPanelPos.Y + headerHeight), ImGuiCond.Always);
                ImGui.SetNextWindowSize(new Vector2(buttonSize * 1.2f, navHeight), ImGuiCond.Always);
                ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar;
                Navigation nav = NavigationLayouts.SocialNavigation();

                // Use the focus-aware DrawSideNavigation and check whether focus succeeded.
                UIHelpers.DrawSideNavigation("SOCIAL", "SocialNavigation", ref navIndex, flags, nav, focusRequested);

            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error("Error drawing social window", ex.ToString());
            }
        }

        public void Dispose()
        {
            foreach (Listing listing in listings)
            {
                WindowOperations.SafeDispose(listing.avatar);
                listing.avatar = null;
            }
        }

    }
}