using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Networking;
using System;
using System.Numerics;

namespace AbsoluteRP.Windows.Listings.Views
{
    internal static class ListingDetail
    {
        public static void Draw()
        {
            var listing = ListingsWindow.selectedListing;

            if (listing == null)
            {
                ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 1), "No listing selected");
                if (ImGui.Button("Back to Browse"))
                {
                    ListingsWindow.BackToBrowse();
                }
                return;
            }

            // Back button
            if (ImGui.Button("< Back"))
            {
                ListingsWindow.BackToBrowse();
            }

            ImGui.SameLine();

            // Bookmark button
            if (ImGui.Button("Bookmark"))
            {
                DataSender.BookmarkPlayer(Plugin.character, string.Empty, string.Empty, listing.id);
            }

            ImGui.Separator();

            using (ImRaii.Child("ListingDetailContent", new Vector2(0, 0), false))
            {
                // Banner
                if (listing.banner != null && listing.banner.Handle != nint.Zero)
                {
                    float bannerWidth = Math.Min(ImGui.GetContentRegionAvail().X, 600);
                    float aspectRatio = (float)listing.banner.Height / listing.banner.Width;
                    ImGui.Image(listing.banner.Handle, new Vector2(bannerWidth, bannerWidth * aspectRatio));
                }

                ImGui.Spacing();

                // Title
                ImGui.PushStyleColor(ImGuiCol.Text, listing.color);
                ImGui.TextWrapped(listing.name);
                ImGui.PopStyleColor();

                ImGui.Spacing();

                // Category/Type info
                DrawInfoRow("Category", GetCategoryText(listing.category));
                DrawInfoRow("Type", GetTypeText(listing.type));
                DrawInfoRow("Focus", GetFocusText(listing.focus));
                DrawInfoRow("Setting", GetSettingText(listing.setting));

                ImGui.Spacing();

                // Date/Time
                if (!string.IsNullOrEmpty(listing.startDate) || !string.IsNullOrEmpty(listing.endDate))
                {
                    ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.3f, 1), "Schedule:");
                    if (!string.IsNullOrEmpty(listing.startDate))
                    {
                        ImGui.Text($"  Starts: {listing.startDate}");
                    }
                    if (!string.IsNullOrEmpty(listing.endDate))
                    {
                        ImGui.Text($"  Ends: {listing.endDate}");
                    }
                    ImGui.Spacing();
                }

                // Expansion compatibility
                DrawExpansionTags(listing);

                ImGui.Spacing();
                ImGui.Separator();

                // Description
                if (!string.IsNullOrEmpty(listing.description))
                {
                    ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.3f, 1), "Description:");
                    ImGui.Spacing();
                    ImGui.TextWrapped(listing.description);
                    ImGui.Spacing();
                }

                // Rules
                if (!string.IsNullOrEmpty(listing.rules))
                {
                    ImGui.Separator();
                    ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.3f, 1), "Rules:");
                    ImGui.Spacing();
                    ImGui.TextWrapped(listing.rules);
                }
            }
        }

        private static void DrawInfoRow(string label, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), $"{label}:");
            ImGui.SameLine();
            ImGui.Text(value);
        }

        private static void DrawExpansionTags(Listing listing)
        {
            bool hasAny = listing.ARR || listing.HW || listing.SB || listing.SHB || listing.EW || listing.DT;
            if (!hasAny)
                return;

            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), "Expansions:");
            ImGui.SameLine();

            if (listing.ARR)
            {
                ImGui.TextColored(new Vector4(0.8f, 0.6f, 0.4f, 1), "[ARR]");
                ImGui.SameLine();
            }
            if (listing.HW)
            {
                ImGui.TextColored(new Vector4(0.4f, 0.6f, 0.9f, 1), "[HW]");
                ImGui.SameLine();
            }
            if (listing.SB)
            {
                ImGui.TextColored(new Vector4(0.9f, 0.3f, 0.3f, 1), "[SB]");
                ImGui.SameLine();
            }
            if (listing.SHB)
            {
                ImGui.TextColored(new Vector4(0.5f, 0.3f, 0.7f, 1), "[ShB]");
                ImGui.SameLine();
            }
            if (listing.EW)
            {
                ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.5f, 1), "[EW]");
                ImGui.SameLine();
            }
            if (listing.DT)
            {
                ImGui.TextColored(new Vector4(0.3f, 0.8f, 0.6f, 1), "[DT]");
                ImGui.SameLine();
            }

            ImGui.NewLine();
        }

        private static string GetCategoryText(int category)
        {
            if (UI.ListingCategoryVals == null || category < 0 || category >= UI.ListingCategoryVals.Length)
                return "";
            return UI.ListingCategoryVals[category].Item1;
        }

        private static string GetTypeText(int type)
        {
            if (UI.ListingTypeVals == null || type < 0 || type >= UI.ListingTypeVals.Length)
                return "";
            return UI.ListingTypeVals[type].Item1;
        }

        private static string GetFocusText(int focus)
        {
            if (UI.ListingFocusVals == null || focus < 0 || focus >= UI.ListingFocusVals.Length)
                return "";
            return UI.ListingFocusVals[focus].Item1;
        }

        private static string GetSettingText(int setting)
        {
            if (UI.ListingSettingVals == null || setting < 0 || setting >= UI.ListingSettingVals.Length)
                return "";
            return UI.ListingSettingVals[setting].Item1;
        }
    }
}
