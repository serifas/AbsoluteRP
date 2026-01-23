using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Networking;
using System;
using System.IO;
using System.Numerics;

namespace AbsoluteRP.Windows.Listings.Views
{
    internal static class ListingEditor
    {
        // Form fields
        private static string listingName = "";
        private static string listingDescription = "";
        private static string listingRules = "";
        private static string triggers = "";

        // Category/Type selections
        private static int currentCategory = 0;
        private static int currentType = 0;
        private static int currentFocus = 0;
        private static int currentSetting = 0;
        private static int inclusion = 0;
        private static bool nsfw = false;

        // Date/Time fields
        private static int selectedStartYear = DateTime.Now.Year;
        private static int selectedStartMonth = DateTime.Now.Month;
        private static int selectedStartDay = DateTime.Now.Day;
        private static int selectedStartHour = 12;
        private static int selectedStartMinute = 0;
        private static int selectedStartAmPm = 0; // 0 = AM, 1 = PM
        private static int selectedStartTimezone = 0;

        private static int selectedEndYear = DateTime.Now.Year;
        private static int selectedEndMonth = DateTime.Now.Month;
        private static int selectedEndDay = DateTime.Now.Day;
        private static int selectedEndHour = 12;
        private static int selectedEndMinute = 0;
        private static int selectedEndAmPm = 1; // 0 = AM, 1 = PM
        private static int selectedEndTimezone = 0;

        // Banner image
        private static byte[] bannerBytes = Array.Empty<byte>();
        private static IDalamudTextureWrap bannerPreview = null;
        private static FileDialogManager _fileDialogManager = new FileDialogManager();

        // Status
        private static string statusMessage = "";
        private static Vector4 statusColor = new Vector4(1, 1, 1, 1);

        public static void Draw()
        {
            string title = ListingsWindow.isEditMode ? "Edit Listing" : "Create New Listing";
            ImGui.TextColored(new Vector4(1, 0.8f, 0.3f, 1), title);
            ImGui.Separator();

            using (ImRaii.Child("EditorContent", new Vector2(0, -40), false))
            {
                DrawBasicInfo();
                ImGui.Spacing();
                DrawCategorySelections();
                ImGui.Spacing();
                DrawDateTimeSelections();
                ImGui.Spacing();
                DrawBannerSection();
            }

            DrawActionButtons();

            // Draw file dialog
            _fileDialogManager.Draw();
        }

        private static void DrawBasicInfo()
        {
            ImGui.Text("Listing Name:");
            ImGui.SetNextItemWidth(400);
            ImGui.InputText("##ListingName", ref listingName, 100);

            ImGui.Spacing();

            ImGui.Text("Description:");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.InputTextMultiline("##ListingDescription", ref listingDescription, 2000, new Vector2(0, 100));

            ImGui.Spacing();

            ImGui.Text("Rules:");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.InputTextMultiline("##ListingRules", ref listingRules, 1000, new Vector2(0, 80));

            ImGui.Spacing();

            ImGui.Text("Triggers/Warnings:");
            ImGui.SetNextItemWidth(400);
            ImGui.InputText("##Triggers", ref triggers, 500);

            ImGui.Spacing();

            ImGui.Checkbox("18+ / NSFW Content", ref nsfw);
        }

        private static void DrawCategorySelections()
        {
            ImGui.Text("Category & Type");
            ImGui.Separator();

            // Category
            if (UI.ListingCategoryVals != null && UI.ListingCategoryVals.Length > 0)
            {
                var (catText, catDesc) = UI.ListingCategoryVals[Math.Min(currentCategory, UI.ListingCategoryVals.Length - 1)];
                ImGui.SetNextItemWidth(200);
                using (var combo = ImRaii.Combo("Category", catText))
                {
                    if (combo)
                    {
                        foreach (var ((text, desc), idx) in UI.ListingCategoryVals.WithIndex())
                        {
                            if (ImGui.Selectable(text, idx == currentCategory))
                                currentCategory = idx;
                            UIHelpers.SelectableHelpMarker(desc);
                        }
                    }
                }
            }

            ImGui.SameLine();

            // Type
            if (UI.ListingTypeVals != null && UI.ListingTypeVals.Length > 0)
            {
                var (typeText, typeDesc) = UI.ListingTypeVals[Math.Min(currentType, UI.ListingTypeVals.Length - 1)];
                ImGui.SetNextItemWidth(200);
                using (var combo = ImRaii.Combo("Type", typeText))
                {
                    if (combo)
                    {
                        foreach (var ((text, desc), idx) in UI.ListingTypeVals.WithIndex())
                        {
                            if (ImGui.Selectable(text, idx == currentType))
                                currentType = idx;
                            UIHelpers.SelectableHelpMarker(desc);
                        }
                    }
                }
            }

            // Focus
            if (UI.ListingFocusVals != null && UI.ListingFocusVals.Length > 0)
            {
                var (focusText, focusDesc) = UI.ListingFocusVals[Math.Min(currentFocus, UI.ListingFocusVals.Length - 1)];
                ImGui.SetNextItemWidth(200);
                using (var combo = ImRaii.Combo("Focus", focusText))
                {
                    if (combo)
                    {
                        foreach (var ((text, desc), idx) in UI.ListingFocusVals.WithIndex())
                        {
                            if (ImGui.Selectable(text, idx == currentFocus))
                                currentFocus = idx;
                            UIHelpers.SelectableHelpMarker(desc);
                        }
                    }
                }
            }

            ImGui.SameLine();

            // Setting
            if (UI.ListingSettingVals != null && UI.ListingSettingVals.Length > 0)
            {
                var (settingText, settingDesc) = UI.ListingSettingVals[Math.Min(currentSetting, UI.ListingSettingVals.Length - 1)];
                ImGui.SetNextItemWidth(200);
                using (var combo = ImRaii.Combo("Setting", settingText))
                {
                    if (combo)
                    {
                        foreach (var ((text, desc), idx) in UI.ListingSettingVals.WithIndex())
                        {
                            if (ImGui.Selectable(text, idx == currentSetting))
                                currentSetting = idx;
                            UIHelpers.SelectableHelpMarker(desc);
                        }
                    }
                }
            }

        }

        private static void DrawDateTimeSelections()
        {
            ImGui.Text("Event Date & Time");
            ImGui.Separator();

            // Start Date/Time
            ImGui.Text("Start:");
            ImGui.SameLine();

            ImGui.SetNextItemWidth(80);
            ImGui.InputInt("##StartYear", ref selectedStartYear);
            ImGui.SameLine();
            ImGui.Text("/");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50);
            ImGui.InputInt("##StartMonth", ref selectedStartMonth);
            ImGui.SameLine();
            ImGui.Text("/");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50);
            ImGui.InputInt("##StartDay", ref selectedStartDay);

            ImGui.SameLine();
            ImGui.SetNextItemWidth(50);
            ImGui.InputInt("##StartHour", ref selectedStartHour);
            ImGui.SameLine();
            ImGui.Text(":");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50);
            ImGui.InputInt("##StartMinute", ref selectedStartMinute);

            ImGui.SameLine();
            string[] ampm = { "AM", "PM" };
            ImGui.SetNextItemWidth(60);
            ImGui.Combo("##StartAmPm", ref selectedStartAmPm, ampm, ampm.Length);

            // End Date/Time
            ImGui.Text("End:  ");
            ImGui.SameLine();

            ImGui.SetNextItemWidth(80);
            ImGui.InputInt("##EndYear", ref selectedEndYear);
            ImGui.SameLine();
            ImGui.Text("/");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50);
            ImGui.InputInt("##EndMonth", ref selectedEndMonth);
            ImGui.SameLine();
            ImGui.Text("/");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50);
            ImGui.InputInt("##EndDay", ref selectedEndDay);

            ImGui.SameLine();
            ImGui.SetNextItemWidth(50);
            ImGui.InputInt("##EndHour", ref selectedEndHour);
            ImGui.SameLine();
            ImGui.Text(":");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50);
            ImGui.InputInt("##EndMinute", ref selectedEndMinute);

            ImGui.SameLine();
            ImGui.SetNextItemWidth(60);
            ImGui.Combo("##EndAmPm", ref selectedEndAmPm, ampm, ampm.Length);

            // Clamp values
            selectedStartMonth = Math.Clamp(selectedStartMonth, 1, 12);
            selectedStartDay = Math.Clamp(selectedStartDay, 1, 31);
            selectedStartHour = Math.Clamp(selectedStartHour, 1, 12);
            selectedStartMinute = Math.Clamp(selectedStartMinute, 0, 59);

            selectedEndMonth = Math.Clamp(selectedEndMonth, 1, 12);
            selectedEndDay = Math.Clamp(selectedEndDay, 1, 31);
            selectedEndHour = Math.Clamp(selectedEndHour, 1, 12);
            selectedEndMinute = Math.Clamp(selectedEndMinute, 0, 59);
        }

        private static void DrawBannerSection()
        {
            ImGui.Text("Banner Image");
            ImGui.Separator();

            if (bannerPreview != null && bannerPreview.Handle != nint.Zero)
            {
                ImGui.Image(bannerPreview.Handle, new Vector2(300, 100));
            }
            else
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "No banner selected");
            }

            if (ImGui.Button("Select Banner Image"))
            {
                _fileDialogManager.OpenFileDialog("Select Banner Image", "Image{.png,.jpg,.jpeg,.gif,.webp}", (success, files) =>
                {
                    if (success && files.Count > 0)
                    {
                        try
                        {
                            var filePath = files[0].ToString();
                            bannerBytes = File.ReadAllBytes(filePath);
                            // Load preview texture
                            bannerPreview = Plugin.TextureProvider.CreateFromImageAsync(bannerBytes).Result;
                            statusMessage = "Banner loaded!";
                            statusColor = new Vector4(0.3f, 1, 0.3f, 1);
                        }
                        catch (Exception ex)
                        {
                            statusMessage = $"Failed to load banner: {ex.Message}";
                            statusColor = new Vector4(1, 0.3f, 0.3f, 1);
                        }
                    }
                }, 1, null, false);
            }

            if (bannerBytes.Length > 0)
            {
                ImGui.SameLine();
                if (ImGui.Button("Clear Banner"))
                {
                    bannerBytes = Array.Empty<byte>();
                    bannerPreview = null;
                }
            }
        }

        private static void DrawActionButtons()
        {
            ImGui.Separator();

            if (!string.IsNullOrEmpty(statusMessage))
            {
                ImGui.TextColored(statusColor, statusMessage);
            }

            if (ImGui.Button("Cancel", new Vector2(100, 0)))
            {
                ResetForm();
                ListingsWindow.BackToMyListings();
            }

            ImGui.SameLine();

            using (ImRaii.Disabled(string.IsNullOrEmpty(listingName)))
            {
                string submitLabel = ListingsWindow.isEditMode ? "Save Changes" : "Create Listing";
                if (ImGui.Button(submitLabel, new Vector2(120, 0)))
                {
                    SubmitListing();
                }
            }
        }

        private static void SubmitListing()
        {
            if (!ClientTCP.IsConnected())
            {
                statusMessage = "Not connected to server";
                statusColor = new Vector4(1, 0.3f, 0.3f, 1);
                return;
            }

            try
            {
                DataSender.SubmitListing(
                    Plugin.character,
                    bannerBytes,
                    listingName,
                    listingDescription,
                    listingRules,
                    inclusion,
                    currentCategory,
                    currentType,
                    currentFocus,
                    currentSetting,
                    nsfw,
                    triggers,
                    selectedStartYear,
                    selectedStartMonth,
                    selectedStartDay,
                    selectedStartHour,
                    selectedStartMinute,
                    selectedStartAmPm,
                    selectedStartTimezone,
                    selectedEndYear,
                    selectedEndMonth,
                    selectedEndDay,
                    selectedEndHour,
                    selectedEndMinute,
                    selectedEndAmPm,
                    selectedEndTimezone
                );

                statusMessage = ListingsWindow.isEditMode ? "Listing updated!" : "Listing created!";
                statusColor = new Vector4(0.3f, 1, 0.3f, 1);

                // Go back to my listings after a brief delay
                ListingsWindow.BackToMyListings();
            }
            catch (Exception ex)
            {
                statusMessage = $"Error: {ex.Message}";
                statusColor = new Vector4(1, 0.3f, 0.3f, 1);
            }
        }

        public static void ResetForm()
        {
            listingName = "";
            listingDescription = "";
            listingRules = "";
            triggers = "";
            currentCategory = 0;
            currentType = 0;
            currentFocus = 0;
            currentSetting = 0;
            inclusion = 0;
            nsfw = false;

            selectedStartYear = DateTime.Now.Year;
            selectedStartMonth = DateTime.Now.Month;
            selectedStartDay = DateTime.Now.Day;
            selectedStartHour = 12;
            selectedStartMinute = 0;
            selectedStartAmPm = 0;

            selectedEndYear = DateTime.Now.Year;
            selectedEndMonth = DateTime.Now.Month;
            selectedEndDay = DateTime.Now.Day;
            selectedEndHour = 12;
            selectedEndMinute = 0;
            selectedEndAmPm = 1;

            bannerBytes = Array.Empty<byte>();
            bannerPreview = null;
            statusMessage = "";
        }

        public static void LoadListing(Listing listing)
        {
            listingName = listing.name;
            listingDescription = listing.description;
            listingRules = listing.rules;
            currentCategory = listing.category;
            currentType = listing.type;
            currentFocus = listing.focus;
            currentSetting = listing.setting;
            inclusion = listing.inclusion;

            // Parse dates if available
            if (!string.IsNullOrEmpty(listing.startDate) && DateTime.TryParse(listing.startDate, out var startDt))
            {
                selectedStartYear = startDt.Year;
                selectedStartMonth = startDt.Month;
                selectedStartDay = startDt.Day;
                selectedStartHour = startDt.Hour > 12 ? startDt.Hour - 12 : (startDt.Hour == 0 ? 12 : startDt.Hour);
                selectedStartMinute = startDt.Minute;
                selectedStartAmPm = startDt.Hour >= 12 ? 1 : 0;
            }

            if (!string.IsNullOrEmpty(listing.endDate) && DateTime.TryParse(listing.endDate, out var endDt))
            {
                selectedEndYear = endDt.Year;
                selectedEndMonth = endDt.Month;
                selectedEndDay = endDt.Day;
                selectedEndHour = endDt.Hour > 12 ? endDt.Hour - 12 : (endDt.Hour == 0 ? 12 : endDt.Hour);
                selectedEndMinute = endDt.Minute;
                selectedEndAmPm = endDt.Hour >= 12 ? 1 : 0;
            }

            // Load banner if available
            bannerPreview = listing.banner;
        }
    }
}
