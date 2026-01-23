using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Networking;
using System;
using System.Linq;
using System.Numerics;

namespace AbsoluteRP.Windows.Listings.Views
{
    internal static class ListingBrowse
    {
        private static string searchQuery = "";
        private static int selectedTypeIndex = 0; // Default to All
        private static int currentPage = 1;
        private static int pageSize = 20;

        // Location filters
        private static FFXIVRegion selectedRegion = FFXIVRegion.NorthAmerica;
        private static FFXIVDataCenter selectedDataCenter = FFXIVDataCenter.Aether;
        private static FFXIVWorld selectedWorld = FFXIVWorld.Aether_Adamantoise;
        private static string worldSearchQuery = "Adamantoise";
        private static int currentCategory = 0;

        public static void Draw()
        {
            DrawFilters();
            ImGui.Separator();
            DrawResults();
        }

        private static void DrawFilters()
        {
            // Location selectors
            DrawFFXIVLocationSelectors();

            ImGui.Spacing();

            // Search box
            ImGui.Text("Search:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(300);
            if (ImGui.InputText("##SearchQuery", ref searchQuery, 100, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                currentPage = 1;
                PerformSearch();
            }
            ImGui.SameLine();

            if (ImGui.Button("Search"))
            {
                currentPage = 1;
                PerformSearch();
            }

            // Category selection
            DrawListingCategorySelection();
        }

        private static void DrawResults()
        {
            var listings = ListingsWindow.searchResults;

            if (listings == null || listings.Count == 0)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "No listings found. Try searching or adjusting your filters.");
                return;
            }

            ImGui.Text($"Showing {listings.Count} listings");
            ImGui.Spacing();

            // Listings table
            using var table = ImRaii.Table("Listings", 2, ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg);
            if (!table)
                return;

            ImGui.TableSetupColumn("Listing", ImGuiTableColumnFlags.WidthFixed, 250);
            ImGui.TableSetupColumn("Details", ImGuiTableColumnFlags.WidthStretch);

            foreach (var listing in listings)
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);

                // Avatar/Banner
                if (listing.avatar != null && listing.avatar.Handle != nint.Zero)
                {
                    ImGui.Image(listing.avatar.Handle, new Vector2(100, 100));
                }

                // Name with color
                ImGui.TextColored(listing.color, listing.name);

                // Action buttons
                if (ImGui.Button($"View##{listing.id}"))
                {
                    ListingsWindow.OpenListingDetail(listing);
                }
                ImGui.SameLine();
                if (ImGui.Button($"Bookmark##{listing.id}"))
                {
                    DataSender.BookmarkPlayer(Plugin.character, string.Empty, string.Empty, listing.id);
                }

                ImGui.TableSetColumnIndex(1);

                // Description
                if (!string.IsNullOrEmpty(listing.description))
                {
                    string shortDesc = listing.description.Length > 150
                        ? listing.description.Substring(0, 147) + "..."
                        : listing.description;
                    ImGui.TextWrapped(shortDesc);
                }

                // Category info
                if (listing.category >= 0 && listing.category < UI.ListingCategorySearchVals.Length)
                {
                    var (catText, _) = UI.ListingCategorySearchVals[listing.category];
                    ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), $"Category: {catText}");
                }

                // Date info
                if (!string.IsNullOrEmpty(listing.startDate))
                {
                    ImGui.TextColored(new Vector4(0.5f, 0.8f, 0.5f, 1), $"Starts: {listing.startDate}");
                }
            }
        }

        private static void DrawListingCategorySelection()
        {
            if (UI.ListingCategorySearchVals == null || UI.ListingCategorySearchVals.Length == 0)
                return;

            var (text, desc) = UI.ListingCategorySearchVals[currentCategory];
            using var combo = ImRaii.Combo("Category", text);
            if (!combo)
                return;

            foreach (var ((newText, newDesc), idx) in UI.ListingCategorySearchVals.WithIndex())
            {
                if (ImGui.Selectable(newText, idx == currentCategory))
                    currentCategory = idx;
                UIHelpers.SelectableHelpMarker(newDesc);
            }
        }

        private static void DrawFFXIVLocationSelectors()
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

        private static void PerformSearch()
        {
            if (!ClientTCP.IsConnected())
            {
                Plugin.PluginLog.Warning("Not connected to server");
                return;
            }

            string query = string.IsNullOrEmpty(searchQuery) ? "ALL PROFILES" : searchQuery;
            DataSender.RequestPersonals(Plugin.character, worldSearchQuery, currentPage, pageSize, query, currentCategory);
        }
    }
}
