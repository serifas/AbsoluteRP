using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Networking;
using OtterGui;
using OtterGui.Extensions;
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
        public static List<Listing> listings = new List<Listing>();
        public static string loading; //loading status string for loading the profile gallery mainly
        public static float percentage = 0f; //loading base value
        public static int loaderInd = 0;
        private FileDialogManager _fileDialogManager; //for banners only at the moment
        public static IDalamudTextureWrap banner;
        public static byte[] bannerBytes;
        public static int currentType = 0;
        public static string searchQuery = string.Empty; //search query for listings
        public static int type = 6; //0 = all, 1 = personals
        private string worldSearchQuery = "";
        private string profileSearchQuery = "";
        public static int currentCategory = 0;
        public static int profileViewCount = 10; //default profile view count
        private static int currentViewCount;
        private static int currentIndex = 1; //current index for the listings

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
        public override void Draw()
        {
            ImGui.InputText("World Name", ref worldSearchQuery, 100, ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll);
            ImGui.InputText("Profile Name", ref profileSearchQuery, 100, ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll);
            DrawListingCategorySelection();
            DrawPageCountSelection();
            using (ImRaii.Child($"ProfileNavigation", new Vector2(ImGui.GetWindowSize().X, ImGui.GetIO().FontGlobalScale * 32), true))
            {
                if (currentIndex > 1)
                {
                    if (ImGui.Button("《 "))
                    {
                        currentIndex--;
                        DataSender.RequestPersonals(worldSearchQuery, currentIndex, currentViewCount, profileSearchQuery, currentCategory);
                    }
                }
                ImGui.SameLine();
                Misc.RenderAlignmentToRight(" 》");
                if (ImGui.Button(" 》"))
                {
                    currentIndex++;
                    DataSender.RequestPersonals(worldSearchQuery, currentIndex, currentViewCount, profileSearchQuery, currentCategory);
                }
            }
            if (ImGui.Button("Search"))
            {
                if (worldSearchQuery == string.Empty)
                {
                    worldSearchQuery = "ALL WORLDS";
                }
                if (profileSearchQuery == string.Empty)
                {
                    profileSearchQuery = "ALL PROFILES";
                }
                DataSender.RequestPersonals(worldSearchQuery, currentIndex, currentViewCount, profileSearchQuery, currentCategory);
            }
            // Draw personal listings
            using (ImRaii.Table("Personal Listings", 3, ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Profile", ImGuiTableColumnFlags.WidthFixed,200);
                ImGui.TableSetupColumn("Controls", ImGuiTableColumnFlags.WidthStretch);

                foreach (var listing in listings.Where(l => l.type == type))
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    if(listing.avatar.ImGuiHandle != null && listing.avatar.ImGuiHandle != IntPtr.Zero)
                    {
                        ImGui.Image(listing.avatar.ImGuiHandle, new Vector2(100, 100));
                    }
                    ImGui.TextColored(listing.color, listing.name);
                    ImGui.TableSetColumnIndex(1);
                    if (ImGui.Button($"View##{listing.id}"))
                    {
                        Plugin.plugin.OpenTargetWindow();
                        DataSender.FetchProfile(false, -1, string.Empty, string.Empty, listing.id);
                    }
                }
            }
        }
        public static void DrawPageCountSelection()
        {
            using var combo = OtterGui.Raii.ImRaii.Combo("##PageCount", currentViewCount.ToString());
            if (!combo)
                return;

            if (ImGui.Selectable("10", profileViewCount == currentViewCount))
                currentViewCount = 10;

            if (ImGui.Selectable("20", profileViewCount == currentViewCount))
                currentViewCount = 20;

            if (ImGui.Selectable("50", profileViewCount == currentViewCount))
                currentViewCount = 50;

            if (ImGui.Selectable("100", profileViewCount == currentViewCount))
                currentViewCount = 100;
        }
        public static void DrawListingCategorySelection()
        {
            var (text, desc) = UI.ListingCategorySearchVals[currentCategory];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Category", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;
            foreach (var ((newText, newDesc), idx) in UI.ListingCategorySearchVals.WithIndex())
            {
                if (ImGui.Selectable(newText, idx == currentCategory))
                    currentCategory = idx;
                ImGuiUtil.SelectableHelpMarker(newDesc);
            }
        }

        public void Dispose()
        {
            foreach(Listing listing in listings)
            {
                WindowOperations.SafeDispose(listing.avatar);
            }
        }
    }
}
