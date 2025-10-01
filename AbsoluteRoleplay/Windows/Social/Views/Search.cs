using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Listings;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRP.Windows.Social.Views
{
    internal class Search
    {
        public static string worldSearchQuery = "";
        public static string profileSearchQuery = "";
        private static int currentViewCount = 10;
        private static int currentIndex = 1; //current index for the listings
        private static FFXIVRegion selectedRegion = FFXIVRegion.NorthAmerica;
        private static FFXIVDataCenter selectedDataCenter = FFXIVDataCenter.Aether;
        private static FFXIVWorld selectedWorld = FFXIVWorld.Aether_Adamantoise;
        public static int currentType = 0;
        public static string searchQuery = string.Empty; //search query for listings
        public static int type = 6; //0 = all, 1 = personals
        public static int currentCategory = 0;
        public static int profileViewCount = 10; //default profile view count
        public static void LoadSearch()
        {
            DrawFFXIVLocationSelectors();
            ImGui.Text("Profile Name");
            ImGui.SameLine();
            ImGui.InputText("##ProfileName", ref profileSearchQuery, 100, ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll);
            DrawListingCategorySelection();
            DrawPageCountSelection();
            using (ImRaii.Child($"ProfileNavigation", new Vector2(ImGui.GetWindowSize().X, ImGui.GetIO().FontGlobalScale * 32), true))
            {
                if (currentIndex > 1)
                {
                    if (ImGui.Button("《 "))
                    {
                        currentIndex--;
                        DataSender.RequestPersonals(Plugin.character, worldSearchQuery, currentIndex, currentViewCount, profileSearchQuery, currentCategory);
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
                    DataSender.RequestPersonals(Plugin.character, worldSearchQuery, currentIndex, currentViewCount, profileSearchQuery, currentCategory);
                }
            }
            if (ImGui.Button("Search"))
            {
                if (profileSearchQuery == string.Empty)
                {
                    profileSearchQuery = "ALL PROFILES";
                }
                DataSender.RequestPersonals(Plugin.character, worldSearchQuery, currentIndex, currentViewCount, profileSearchQuery, currentCategory);
            }
            if (SocialWindow.listings.Count == 0)
            {
                ImGui.TextUnformatted("No listings loaded.");
                return;
            }


            using (ImRaii.Table("Personal Listings", 2, ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Profile", ImGuiTableColumnFlags.WidthFixed, 200);
                ImGui.TableSetupColumn("Controls", ImGuiTableColumnFlags.WidthStretch);

                foreach (var listing in SocialWindow.listings.Where(l => l.type == type))
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    if (listing.avatar.Handle != null && listing.avatar.Handle != nint.Zero)
                    {
                        ImGui.Image(listing.avatar.Handle, new Vector2(100, 100));
                    }
                    ImGui.TextColored(listing.color, listing.name);

                    if (ImGui.Button($"View##{listing.id}"))
                    {
                        Plugin.plugin.OpenTargetWindow();
                        TargetProfileWindow.RequestingProfile = true;
                        TargetProfileWindow.ResetAllData();
                        DataSender.FetchProfile(Plugin.character, false, -1, string.Empty, string.Empty, listing.id);
                    }
                    ImGui.SameLine();
                    if (ImGui.Button($"Bookmark##{listing.id}"))
                    {
                        DataSender.BookmarkPlayer(Plugin.character, string.Empty, string.Empty, listing.id);
                    }
                    ImGui.TableSetColumnIndex(1);
                    //description here
                }
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
        public static void DrawFFXIVLocationSelectors()
        {
            // Region Combo
            var regions = GameData.GetAllRegions();
            var regionNames = regions.ConvertAll(GameData.GetRegionName);
            int regionIdx = regions.IndexOf(selectedRegion);
            ImGui.PushItemWidth(ImGui.GetWindowSize().X / 5);
            string regionLabel = regionNames.Count > 0 && regionIdx >= 0 ? regionNames[regionIdx] : "";

            using (var regionCombo = ImRaii.Combo("Region", regionLabel))
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
            string dcLabel = dcNames.Count > 0 && dcIdx >= 0 ? dcNames[dcIdx] : "";

            using (var dcCombo = ImRaii.Combo("Data Center", dcLabel))
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
            string worldLabel = worldNames.Count > 0 && worldIdx >= 0 ? worldNames[worldIdx] : "";

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
