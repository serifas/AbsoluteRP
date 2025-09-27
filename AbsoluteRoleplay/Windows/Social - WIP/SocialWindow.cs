using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Ect;
using AbsoluteRP.Windows.Profiles;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Social___WIP;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Networking;
using System.Numerics;
using System.Reflection;

namespace AbsoluteRP.Windows.Listings
{
    internal class SocialWindow : Window, IDisposable
    {
        public static Configuration configuration;
        public static Vector2 buttonScale;
        public static List<Listing> listings = new List<Listing>();
        public static string loading; //loading status string for loading the profile gallery mainly
        public static float percentage = 0f; //loading base value
        public static int loaderInd = 0;
        private FileDialogManager _fileDialogManager; //for banners only at the moment
        public static IDalamudTextureWrap banner;
        public static byte[] bannerBytes;


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
            }
        }
        public override void Draw()
        {
            try
            {
                ImGui.BeginTabBar("Social");

                if (ImGui.BeginTabItem("Connections"))
                {
                    Connections.LoadConnectionsTab();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Bookmarks"))
                {
                    Bookmarks.DrawBookmarksUI();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Search"))
                {
                    Search.LoadSearch();
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error("Error drawing listing window", ex.ToString());
            }
        }


        public static void DrawSocialNavigation()
        {

            // Draw personal listings
            using (ImRaii.Table("Social", 2, ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Profile", ImGuiTableColumnFlags.WidthFixed, 200);
                ImGui.TableSetupColumn("Controls", ImGuiTableColumnFlags.WidthStretch);

                foreach (var listing in listings.Where(l => l.type == Search.type))
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    if (listing.avatar.Handle != null && listing.avatar.Handle != IntPtr.Zero)
                    {
                        ImGui.Image(listing.avatar.Handle, new Vector2(100, 100));
                    }
                    ImGui.TextColored(listing.color, listing.name);
                    ImGui.TableSetColumnIndex(1);
                    if (ImGui.Button($"View##{listing.id}"))
                    {
                        Plugin.plugin.OpenTargetWindow();
                        TargetProfileWindow.RequestingProfile = true;
                        TargetProfileWindow.ResetAllData();
                        DataSender.FetchProfile(Plugin.character, false, -1, string.Empty, string.Empty, listing.id);
                    }
                }
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
