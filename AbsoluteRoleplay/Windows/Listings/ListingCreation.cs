using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Dalamud.Utility;
using Networking;
using OtterGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Textures.TextureWraps;

namespace AbsoluteRoleplay.Windows.Listings
{

    //changed
    public class ListingCreation
    {
        private static string ListingName = string.Empty, ListingDescription = string.Empty, ListingRules = string.Empty, triggers = string.Empty;
        public static IDalamudTextureWrap banner;
        public static byte[] bannerBytes;

        public static string loading; //loading status string for loading the profile gallery mainly
        public static float percentage = 0f; //loading base value
        private Plugin plugin;
        private static IDalamudPluginInterface pg;
        public static FileDialogManager fileDialogManager;
        public static Configuration configuration;
        public static Listing listing;
        public static int width = 0, height = 0;
        public static int currentCategory, currentType, currentFocus, currentSetting = 0;
        public static bool editBanner = false;
        public bool ReorderTriggers = false;
        public static bool IsNSFW = false;
        public static bool StartListingCreation = false;
        // Variables for start date
        public static int selectedStartYear = DateTime.Now.Year;
        public static int selectedStartMonth = 0; // Example: January
        public static int selectedStartDay = 1;
        public static int selectedStartHour = 1;
        public static int selectedStartMinute = 0;
        public static int selectedStartAmPm = 0; // Example: AM
        public static int selectedStartTimezone = 0; // Example: UTC
        public static IDalamudTextureWrap[] banners = new IDalamudTextureWrap[100];
        // Variables for end date
        public static int selectedEndYear = DateTime.Now.Year;
        public static int selectedEndMonth = 0; // Example: January
        public static int selectedEndDay = 1;
        public static int selectedEndHour = 1;
        public static int selectedEndMinute = 0;
        public static int selectedEndAmPm = 0; // Example: AM
        public static int selectedEndTimezone = 0; // Example: UTC
        public static bool DrawDateTimeSelection = false;
        public static bool DrawBannerCreation = false;
        public static bool DrawDetailsCreation = false;
        public static bool DrawInfoCreation = false;
        public static bool DrawTriggerCreation = false;
        public static bool DrawRosterCreation = false;
        public static bool DrawListings = false;
        private static bool setEndDate = false;
        private static int inclusion = 0;
        internal static List<Listing> listings = new List<Listing>();
        public static string[] listingNames;
        public static float loaderInd;
        internal static bool allLoaded = false;
        public static Vector2 buttonScale;
        private static bool setStartDate;
        private static bool hiring;

        public  static void DrawListingsCreation()
        {
            if (bannerBytes == null)
            {
                //set the avatar to the avatar_holder.png by default
                if (Plugin.PluginInterface is { AssemblyLocation.Directory.FullName: { } path })
                {
                    bannerBytes = File.ReadAllBytes(Path.Combine(path, "UI/common/blank.png"));
                }
            }
            banner = Plugin.TextureProvider.CreateFromImageAsync(bannerBytes).Result;
            buttonScale = new Vector2(ImGui.GetIO().FontGlobalScale / 0.015f, ImGui.GetIO().FontGlobalScale / 0.030f);

          
             

            if (StartListingCreation == true)
            {
                DrawListingManagement();
            }
            if (DrawListings == true)
            {
                if (AllLoaded() == true)
                {
                    DrawListingData();
                }
                else
                {
                    Misc.StartLoader(loaderInd, percentage, loading, ImGui.GetWindowSize());
                }

            }

         }
        
        public static bool AllLoaded()
        {
            if (DataReceiver.ListingsLoadStatus != -1)
            {
                return true;
            }
            return false;
        }

        public static void ResetPages()
        {
            StartListingCreation = false;
        }


        public static void ResetElements()
        {
            DrawDateTimeSelection = false;
            DrawBannerCreation = false;
            DrawDetailsCreation = false;
            DrawInfoCreation = false;
            DrawTriggerCreation = false;
            DrawRosterCreation = false;
        }

        public static void DrawListingData()
        {
            using var listingView = ImRaii.Child("LISTING");
            if (listingView)
            {
                for (var i = 0; i < listings.Count; i++)
                {
                    ImGui.Image(listings[i].banner.ImGuiHandle, new Vector2(500, 100));
                    ImGui.TextUnformatted(listings[i].name);
                }
            }

        }

        
        public static void DrawListingManagement()
        {
            
            GetImGuiWindowDimensions(out var windowWidth, out var windowHeight);

            #region CATEGORY - SETTING
            if (DrawInfoCreation == true)
            {

                ImGui.Text("Category:");
                ImGui.SameLine();
                AddCategorySelection();

                ImGui.Text("Type:");
                ImGui.SameLine();
                AddTypeSelection();

                ImGui.Text("Focus:");
                ImGui.SameLine();
                AddFocusSelection();

                ImGui.Text("Setting:");
                ImGui.SameLine();
                AddSettingSelection();
                ImGui.Spacing();


                ImGui.Text("NSFW");
                ImGui.SameLine();
                ImGui.Checkbox("##nsfw", ref IsNSFW);
                if (ImGui.Button("Next"))
                {
                    ResetElements();
                    DrawDateTimeSelection = true;
                }
            }
            #endregion
            #region START AND END DAATE
            if (DrawDateTimeSelection == true)
            {
                DrawDatePickerTable();
                ImGui.Spacing();
                if (ImGui.Button("Back"))
                {
                    ResetElements();
                    DrawInfoCreation = true;
                }
                ImGui.SameLine();
                if (ImGui.Button("Next"))
                {
                    ResetElements();
                    DrawDetailsCreation = true;
                }
            }
            #endregion
            #region DETAILS
            if (DrawDetailsCreation == true)
            {
                if (ImGui.Button("Upload Banner"))
                {
                    editBanner = true;
                }
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(255, 0, 0, 255), "All images are scaled to 500x100 and may NOT contain NSFW content");
                if (banner != null)
                {
                    ImGui.Image(ListingsWindow.banner.ImGuiHandle, new Vector2(500, 100));
                }

                //Specifications
                ImGui.Text("Name:");
                ImGui.SameLine();
                ImGui.InputText("##Name", ref ListingName, 100);
                ImGui.Spacing();

                ImGui.Text("Description:");
                ImGui.InputTextMultiline("##Description", ref ListingDescription, 5000, new Vector2(windowWidth - 50, 100));
                ImGui.Spacing();

                ImGui.Text("Rules:");
                ImGui.InputTextMultiline("##Rules", ref ListingRules, 5000, new Vector2(windowWidth - 50, 100));
                ImGui.Spacing();

                ImGui.Text("Trigger List");
                ImGui.InputTextMultiline("##Triggers", ref triggers, 10000, new Vector2(windowWidth - 50, 100));
                ImGui.Spacing();

                ImGui.Text("Availility:");
                ImGui.Combo($"##inclusion", ref inclusion, UI.inclusions, UI.inclusions.Length);  // Unique ID


                if (ImGui.Button("Back"))
                {
                    ResetElements();
                    DrawDateTimeSelection = true;
                }

                if (ImGui.Button("Submit Listing"))
                {
                    DataSender.SubmitListing(bannerBytes, ListingName, ListingDescription, ListingRules, inclusion,
                        currentCategory, currentType, currentFocus, currentSetting, IsNSFW, triggers,
                        selectedStartYear, selectedStartMonth + 1, selectedStartDay, selectedStartHour, selectedStartMinute, selectedStartAmPm, selectedStartTimezone,
                        selectedEndYear, selectedEndMonth + 1, selectedEndDay, selectedEndHour, selectedEndMinute, selectedEndAmPm, selectedEndTimezone
                        );
                }
            }
            #endregion          
             

        }
        private static void GetImGuiWindowDimensions(out int width, out int height)
        {
            var windowSize = ImGui.GetWindowSize();
            width = (int)windowSize.X - 200;
            height = (int)windowSize.Y;
        }


        public static void AddCategorySelection()
        {
            var (text, desc) = UI.ListingCategoryVals[currentCategory];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Category", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;

            foreach (var ((newText, newDesc), idx) in UI.ListingCategoryVals.WithIndex())
            {
                if (ImGui.Selectable(newText, idx == currentCategory))
                    currentCategory = idx;

                ImGuiUtil.SelectableHelpMarker(newDesc);
            }
        }
        public static void AddTypeSelection()
        {
            var (text, desc) = UI.ListingTypeVals[currentType];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Type", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;

            foreach (var ((newText, newDesc), idx) in UI.ListingTypeVals.WithIndex())
            {
                if (ImGui.Selectable(newText, idx == currentType))
                    currentType = idx;

                ImGuiUtil.SelectableHelpMarker(newDesc);
            }
        }
        public static void AddFocusSelection()
        {
            var (text, desc) = UI.ListingFocusVals[currentFocus];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Focus", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;

            foreach (var ((newText, newDesc), idx) in UI.ListingFocusVals.WithIndex())
            {
                if (ImGui.Selectable(newText, idx == currentFocus))
                    currentFocus = idx;

                ImGuiUtil.SelectableHelpMarker(newDesc);
            }
        }

        public static void AddSettingSelection()
        {
            var (text, desc) = UI.ListingSettingVals[currentSetting];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Setting", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;

            foreach (var ((newText, newDesc), idx) in UI.ListingSettingVals.WithIndex())
            {
                if (ImGui.Selectable(newText, idx == currentSetting))
                    currentSetting = idx;

                ImGuiUtil.SelectableHelpMarker(newDesc);
            }
        }
        public void AddListingsSelection()
        {
            List<string> listingNames = new List<string>();
            var (text, desc) = UI.ListingSettingVals[currentSetting];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Listings", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;

            foreach (var ((newText, newDesc), idx) in UI.ListingSettingVals.WithIndex())
            {
                if (ImGui.Selectable(newText, idx == currentSetting))
                    currentSetting = idx;

                ImGuiUtil.SelectableHelpMarker(newDesc);
            }
        }

        public static void DrawDatePickerTable()
        {
            // Create a table with 2 columns, no row borders
            using var dateTable = ImRaii.Table("DatePickersTable", 2, ImGuiTableFlags.BordersInnerV);
            if (!dateTable)
                return;

            // Column 1: Start Date
            ImGui.TableNextColumn();
            ImGui.Checkbox("Set Start Date", ref setStartDate);
            if (setStartDate)
            {
                DrawDateTimePicker(true);
            }
            // Pass 'true' for the start picker
            // Column 2: End Date
            ImGui.TableNextColumn();  // Move to the second column
            ImGui.Checkbox("Set End Date", ref setEndDate);
            if (setEndDate)
            {
                DrawDateTimePicker(false); // Pass 'false' for the end picker
            } // End the table

        }
        public static void DrawDateTimePicker(bool starting)
        {
            var idPrefix = starting ? "Start" : "End";  // Unique prefix for UI element IDs

            // Variables based on whether we are modifying "Start" or "End"
            ref int year = ref (starting ? ref selectedStartYear : ref selectedEndYear);
            ref int month = ref (starting ? ref selectedStartMonth : ref selectedEndMonth);
            ref int day = ref (starting ? ref selectedStartDay : ref selectedEndDay);
            ref int hour = ref (starting ? ref selectedStartHour : ref selectedEndHour);
            ref int minute = ref (starting ? ref selectedStartMinute : ref selectedEndMinute);
            ref int ampm = ref (starting ? ref selectedStartAmPm : ref selectedEndAmPm);
            ref int timezone = ref (starting ? ref selectedStartTimezone : ref selectedEndTimezone);
            //display the selected datetime info
            ImGui.TextWrapped($"Selected {idPrefix} Date and Time: {year}-{month + 1:D2}-{day:D2} {hour:D2}:{minute:D2} {UI.amPmOptions[ampm]}");
            ImGui.TextWrapped($"Selected {idPrefix} Timezone: {UI.timezones[timezone]}");


            // Year selection (limited to current year + 2)
            ImGui.Text($"{idPrefix} Year");
            var minYear = DateTime.Now.Year;
            var maxYear = DateTime.Now.Year + 2;
            ImGui.SliderInt($"##{idPrefix}_year", ref year, minYear, maxYear);  // Unique ID

            // Month selection using Combo (index is 0-based)
            ImGui.Text($"{idPrefix} Month");
            if (ImGui.Combo($"##{idPrefix}_month", ref month, UI.months, UI.months.Length))  // Unique ID
            {
                day = 1;
            }

            // Get the correct number of days in the selected month and year
            var monthValue = month + 1;  // Convert 0-based index to 1-12 month value
            var daysInMonth = DateTime.DaysInMonth(year, monthValue);

            // Day selection (adjust dynamically based on month and year)
            ImGui.Text($"{idPrefix} Day");
            ImGui.SliderInt($"##{idPrefix}_day", ref day, 1, daysInMonth);  // Unique ID

            // Time selection (Hour in 12-hour format)
            ImGui.Text($"{idPrefix} Hour");
            ImGui.SliderInt($"##{idPrefix}_hour", ref hour, 1, 12);  // Unique ID

            // Minute selection
            ImGui.Text($"{idPrefix} Minute");
            ImGui.SliderInt($"##{idPrefix}_minute", ref minute, 0, 59);  // Unique ID

            // AM/PM selection using Combo
            ImGui.Text($"{idPrefix} AM/PM");
            ImGui.Combo($"##{idPrefix}_ampm", ref ampm, UI.amPmOptions, UI.amPmOptions.Length);  // Unique ID

            ImGui.Text($"{idPrefix} Timezone");
            ImGui.Combo($"##{idPrefix}_timezone", ref timezone, UI.timezones, UI.timezones.Length);  // Unique ID
        }



    }
}


