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
    // Player search UI — search for other players by name/world and view their profiles
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
        public static int profileViewCount = 10; //default tooltipData view count

        public static bool WorldComboOpen { get; private set; }
        public static bool DataCenterComboOpen { get; private set; }
        public static bool RegionComboOpen { get; private set; }
        public static bool CategoryComboOpen { get; private set; }
        public static bool PageCountComboOpen { get; private set; }

        public static void LoadSearch()
        {
            DrawFFXIVLocationSelectors();
            ThemeManager.SubtitleText("Profile Name");
            ImGui.SameLine();
            ThemeManager.StyledInput("##ProfileName", ref profileSearchQuery, 100);
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
            if (ThemeManager.PillButton("Search"))
            {
                if (profileSearchQuery == string.Empty)
                {
                    profileSearchQuery = "ALL PROFILES";
                }
                SocialWindow.isSearchLoading = true;
                SocialWindow.searchLoadedCount = 0;
                SocialWindow.searchTotalCount = 0;
                SocialWindow.listings.Clear();
                DataSender.RequestPersonals(Plugin.character, worldSearchQuery, currentIndex, currentViewCount, profileSearchQuery, currentCategory);
            }

            // Loading bar while profiles are being fetched
            if (SocialWindow.isSearchLoading)
            {
                ImGui.Spacing(); ImGui.Spacing();
                float progress = SocialWindow.searchTotalCount > 0
                    ? (float)SocialWindow.searchLoadedCount / SocialWindow.searchTotalCount
                    : 0f;
                string progressLabel = SocialWindow.searchTotalCount > 0
                    ? $"Loading profiles... {SocialWindow.searchLoadedCount}/{SocialWindow.searchTotalCount}"
                    : "Searching...";
                ThemeManager.StyledProgressBar(progress, new Vector2(ImGui.GetContentRegionAvail().X, 22), progressLabel, null);
                return;
            }

            if (SocialWindow.listings.Count == 0)
            {
                ImGui.TextUnformatted("No listings loaded.");
                return;
            }

            ImGui.Spacing();
            DrawProfileCardGrid();
        }
        private static void DrawProfileCardGrid()
        {
            float windowWidth = ImGui.GetContentRegionAvail().X;
            float cardWidth = 300f;
            float cardSpacing = 10f;
            float cardHeight = 230f;
            float rowSpacing = 10f;
            int columns = Math.Max(1, (int)((windowWidth) / (cardWidth + cardSpacing)));

            var filtered = SocialWindow.listings.Where(l => l.type == type).ToList();
            if (filtered.Count == 0) return;

            // Calculate total height needed for all rows
            int rows = (int)Math.Ceiling((double)filtered.Count / columns);
            float totalHeight = rows * (cardHeight + rowSpacing);

            // Scrollable region that encompasses all cards
            if (ImGui.BeginChild("##ProfileGrid", new Vector2(windowWidth, ImGui.GetContentRegionAvail().Y), false, ImGuiWindowFlags.AlwaysVerticalScrollbar))
            {
                // Reserve the full content height so the scrollbar works
                var startPos = ImGui.GetCursorPos();

                for (int i = 0; i < filtered.Count; i++)
                {
                    int col = i % columns;
                    int row = i / columns;
                    float x = startPos.X + col * (cardWidth + cardSpacing);
                    float y = startPos.Y + row * (cardHeight + rowSpacing);

                    ImGui.SetCursorPos(new Vector2(x, y));
                    DrawProfileCard(filtered[i], cardWidth);
                }

                // Move cursor to the end so ImGui knows the full content size
                ImGui.SetCursorPos(new Vector2(startPos.X, startPos.Y + totalHeight));
                ImGui.Dummy(new Vector2(0, 0));
            }
            ImGui.EndChild();
        }

        private static void DrawProfileCard(Listing listing, float cardWidth)
        {
            float cardHeight = 230f;
            float avatarSize = 80f;
            float avatarY = 16f;
            float rounding = 8f;

            // Use BeginGroup + Dummy + DrawList instead of BeginCard (which uses BeginChild).
            // BeginChild creates nested scroll regions that don't contribute to parent scroll height.
            // BeginGroup properly extends the parent's content area.
            ImGui.BeginGroup();
            var cardPos = ImGui.GetCursorScreenPos();
            var dl = ImGui.GetWindowDrawList();
            float centerX = cardPos.X + cardWidth / 2f;

            // Card background
            var cardBg = ThemeManager.Lighten(ThemeManager.Background, 0.03f);
            var cardBorder = new Vector4(ThemeManager.Border.X, ThemeManager.Border.Y, ThemeManager.Border.Z, ThemeManager.Border.W * 0.6f);
            var cardMax = new Vector2(cardPos.X + cardWidth, cardPos.Y + cardHeight);
            dl.AddRectFilled(cardPos, cardMax, ImGui.ColorConvertFloat4ToU32(cardBg), rounding);
            dl.AddRect(cardPos, cardMax, ImGui.ColorConvertFloat4ToU32(cardBorder), rounding, ImDrawFlags.None, 1f);

            // Accent color strip at top of card
            uint accentCol = ImGui.ColorConvertFloat4ToU32(new Vector4(listing.color.X, listing.color.Y, listing.color.Z, 0.6f));
            dl.AddRectFilled(cardPos, new Vector2(cardPos.X + cardWidth, cardPos.Y + 4), accentCol, rounding, ImDrawFlags.RoundCornersTop);

            // Avatar circle (centered)
            float avX = centerX - avatarSize / 2f;
            float avY = cardPos.Y + avatarY;
            Vector2 avCenter = new Vector2(avX + avatarSize / 2f, avY + avatarSize / 2f);

            // Glow ring in profile color
            uint glowCol = ImGui.ColorConvertFloat4ToU32(new Vector4(listing.color.X, listing.color.Y, listing.color.Z, 0.35f));
            dl.AddCircleFilled(avCenter, avatarSize / 2f + 4, glowCol);

            // White border
            dl.AddCircleFilled(avCenter, avatarSize / 2f + 2, 0xFFFFFFFF);

            // Avatar image
            if (listing.avatar != null && listing.avatar.Handle != IntPtr.Zero)
            {
                dl.AddImageRounded(listing.avatar.Handle,
                    new Vector2(avX, avY),
                    new Vector2(avX + avatarSize, avY + avatarSize),
                    Vector2.Zero, Vector2.One,
                    0xFFFFFFFF, avatarSize / 2f);
            }
            else
            {
                dl.AddCircleFilled(avCenter, avatarSize / 2f,
                    ImGui.ColorConvertFloat4ToU32(ThemeManager.BgDark));
            }

            // Name (centered, in profile color) — truncate to fit card inner width
            float nameY = avY + avatarSize + 8;
            float cardInner = cardWidth - 32;
            string displayName = listing.name;
            float nameWidth = ImGui.CalcTextSize(displayName).X;
            if (nameWidth > cardInner)
            {
                while (displayName.Length > 3 && ImGui.CalcTextSize(displayName + "..").X > cardInner)
                    displayName = displayName.Substring(0, displayName.Length - 1);
                displayName += "..";
                nameWidth = ImGui.CalcTextSize(displayName).X;
            }
            ImGui.SetCursorScreenPos(new Vector2(centerX - nameWidth / 2f, nameY));
            ImGui.TextColored(listing.color, displayName);

            // Spoiler badges row
            float badgeY = nameY + ImGui.GetTextLineHeightWithSpacing() + 2;
            List<string> spoilerTags = new List<string>();
            if (listing.ARR) spoilerTags.Add("ARR");
            if (listing.HW) spoilerTags.Add("HW");
            if (listing.SB) spoilerTags.Add("SB");
            if (listing.SHB) spoilerTags.Add("SHB");
            if (listing.EW) spoilerTags.Add("EW");
            if (listing.DT) spoilerTags.Add("DT");
            if (spoilerTags.Count > 0)
            {
                string spoilerStr = string.Join("  ", spoilerTags);
                float spoilerW = ImGui.CalcTextSize(spoilerStr).X;
                ImGui.SetCursorScreenPos(new Vector2(centerX - spoilerW / 2f, badgeY));
                ThemeManager.SubtitleText(spoilerStr);
            }

            // View + Bookmark buttons (auto-sized to text, centered at bottom)
            float btnY = badgeY + ImGui.GetTextLineHeightWithSpacing() + 4;
            float btnPadX = 32f;
            float btnH = ImGui.GetTextLineHeight() + 18f;
            string viewLabel = $"View Profile##{listing.id}";
            string bookmarkLabel = $"Bookmark##{listing.id}";
            float viewBtnW = ImGui.CalcTextSize("View Profile").X + btnPadX;
            float bookmarkBtnW = ImGui.CalcTextSize("Bookmark").X + btnPadX;
            float btnGap = 8f;
            float totalBtnW = viewBtnW + bookmarkBtnW + btnGap;
            ImGui.SetCursorScreenPos(new Vector2(centerX - totalBtnW / 2f, btnY));

            if (ThemeManager.PillButton(viewLabel, new Vector2(viewBtnW, btnH)))
            {
                Plugin.plugin.OpenTargetWindow();
                TargetProfileWindow.RequestingProfile = true;
                TargetProfileWindow.ResetAllData();
                DataSender.FetchProfile(Plugin.character, false, -1, string.Empty, string.Empty, listing.id);
            }
            ImGui.SameLine(0, btnGap);
            if (ThemeManager.GhostButton(bookmarkLabel, new Vector2(bookmarkBtnW, btnH)))
            {
                DataSender.BookmarkPlayer(Plugin.character, string.Empty, string.Empty, listing.id);
            }

            // Reserve the full card height so ImGui knows how tall this group is
            ImGui.SetCursorScreenPos(new Vector2(cardPos.X, cardPos.Y + cardHeight));
            ImGui.Dummy(new Vector2(cardWidth, 0));
            ImGui.EndGroup();
        }

        public static void DrawPageCountSelection()
        {
            using var combo = ImRaii.Combo("##PageCount", currentViewCount.ToString());
            PageCountComboOpen = ImGui.IsPopupOpen("##PageCount");
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
            CategoryComboOpen = ImGui.IsPopupOpen("##Category");
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
                RegionComboOpen = ImGui.IsPopupOpen("Region");
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
                DataCenterComboOpen = ImGui.IsPopupOpen("Data Center");
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
                WorldComboOpen = ImGui.IsPopupOpen("World");
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
