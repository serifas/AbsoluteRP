using AbsoluteRoleplay.Defines;
using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Networking;
using Dalamud.Bindings.ImGui;
using System.Numerics;
using Dalamud.Interface.Utility.Raii;

namespace AbsoluteRoleplay.Windows.Listings
{
    internal class ListingsWindow : Window, IDisposable
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
        public static int currentType = 0;
        public static string searchQuery = string.Empty; //search query for listings
        public static int type = 6; //0 = all, 1 = personals
        private string worldSearchQuery = "";
        private string profileSearchQuery = "";
        public static int currentCategory = 0;
        public static int profileViewCount = 10; //default profile view count
        private static int currentViewCount = 10;
        private static int currentIndex = 1; //current index for the listings
        private FFXIVRegion selectedRegion = FFXIVRegion.NorthAmerica;
        private FFXIVDataCenter selectedDataCenter = FFXIVDataCenter.Aether;
        private FFXIVWorld selectedWorld = FFXIVWorld.Aether_Adamantoise;


        public bool DrawListingCreation { get; private set; }

        public ListingsWindow() : base(
       "LISTINGS", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {

                MinimumSize = new Vector2(200, 400),
                MaximumSize = new Vector2(1000, 1000)
            };

            configuration = Plugin.plugin.Configuration;
            worldSearchQuery = "Adamantoise";
            _fileDialogManager = new FileDialogManager();
        }
        // ... (rest of the file unchanged)

        // ... (rest of the file unchanged)

        // ... (rest of the file unchanged)

        public override void Draw()
        {
            DrawFFXIVLocationSelectors();
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
                ImGui.SetCursorPosX(ImGui.GetWindowSize().X / 2 - ImGui.CalcTextSize($"{currentIndex}").X / 2);
                ImGui.TextUnformatted($"{currentIndex}");
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
                if (profileSearchQuery == string.Empty)
                {
                    profileSearchQuery = "ALL PROFILES";
                }
                DataSender.RequestPersonals(worldSearchQuery, currentIndex, currentViewCount, profileSearchQuery, currentCategory);
            }

            // Draw personal listings
            if (ImGui.BeginTable("Personal Listings", 2, ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Profile", ImGuiTableColumnFlags.WidthFixed, 200);
                ImGui.TableSetupColumn("Controls", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableHeadersRow();

                foreach (var listing in listings.Where(l => l.type == type))
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);

                    if (listing.avatar != null && listing.avatar.Handle != IntPtr.Zero)
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
                        DataSender.FetchProfile(false, -1, string.Empty, string.Empty, listing.id);
                    }
                }
                ImGui.EndTable();
            }
        }
        public static void DrawPageCountSelection()
        {
            using var combo = ImRaii.Combo("##PageCount", currentViewCount.ToString());
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
            using var combo = ImRaii.Combo("##Category", text);
            if (!combo)
                return;
            foreach (var ((newText, newDesc), idx) in UI.ListingCategorySearchVals.WithIndex())
            {
                if (ImGui.Selectable(newText, idx == currentCategory))
                    currentCategory = idx;
                UIHelpers.SelectableHelpMarker(newDesc);
            }
        }

        public void Dispose()
        {
            foreach(Listing listing in listings)
            {
                WindowOperations.SafeDispose(listing.avatar);
                listing.avatar = null;
            }
        }
        private void DrawFFXIVLocationSelectors()
        {
            // Region Combo
            var regions = GameData.GetAllRegions();
            var regionNames = regions.ConvertAll(GameData.GetRegionName);
            int regionIdx = regions.IndexOf(selectedRegion);
            ImGui.PushItemWidth(ImGui.GetWindowSize().X / 5);
            using (var regionCombo = ImRaii.Combo("Region", regionNames[regionIdx]))
            {
                if (regionCombo)
                {
                    for (int i = 0; i < regionNames.Count; i++)
                    {
                        if (ImGui.Selectable(regionNames[i], i == regionIdx))
                        {
                            selectedRegion = regions[i];
                            // Reset datacenter/world when region changes
                            var dcs = GameData.GetDataCentersByRegion(selectedRegion);
                            selectedDataCenter = dcs.Count > 0 ? dcs[0] : default;
                            var newWorldsList = GameData.GetWorldsByDataCenter(selectedDataCenter);
                            selectedWorld = newWorldsList.Count > 0 ? newWorldsList[0] : default;
                        }
                    }
                }
            } 
            ImGui.SameLine();

            // Data Center Combo
            var dataCenters = GameData.GetDataCentersByRegion(selectedRegion);
            var dcNames = dataCenters.ConvertAll(GameData.GetDataCenterName);
            int dcIdx = dataCenters.IndexOf(selectedDataCenter);

            using (var dcCombo = ImRaii.Combo("Data Center", dcNames.Count > 0 && dcIdx >= 0 ? dcNames[dcIdx] : ""))
            {
                if (dcCombo)
                {
                    for (int i = 0; i < dcNames.Count; i++)
                    {
                        if (ImGui.Selectable(dcNames[i], i == dcIdx))
                        {
                            selectedDataCenter = dataCenters[i];
                            // Reset world when datacenter changes
                            var updatedWorldsList = GameData.GetWorldsByDataCenter(selectedDataCenter);
                            selectedWorld = updatedWorldsList.Count > 0 ? updatedWorldsList[0] : default;
                        }
                    }
                }
            }
            ImGui.SameLine();

            // World Combo
            var worldsList = GameData.GetWorldsByDataCenter(selectedDataCenter);
            var worldNames = worldsList.ConvertAll(w =>
            {
                var name = w.ToString();
                var idx = name.IndexOf('_');
                return idx >= 0 ? name.Substring(idx + 1) : name;
            });
            int worldIdx = worldsList.IndexOf(selectedWorld);

            using (var worldCombo = ImRaii.Combo("World", worldNames.Count > 0 && worldIdx >= 0 ? worldNames[worldIdx] : ""))
            {
                if (worldCombo)
                {
                    for (int i = 0; i < worldNames.Count; i++)
                    {
                        if (ImGui.Selectable(worldNames[i], i == worldIdx))
                        {
                            worldSearchQuery = worldNames[i];
                            selectedWorld = worldsList[i];
                        }
                    }
                }
            }
            ImGui.PopItemWidth();
        }
    }
}
