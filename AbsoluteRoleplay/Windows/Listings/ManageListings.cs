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
    public class ManageListings : Window, IDisposable
    {
        private string ListingName = string.Empty, ListingDescription = string.Empty, ListingRules = string.Empty, triggers = string.Empty;
        public static IDalamudTextureWrap banner;
        public static byte[] bannerBytes;

        public static string loading; //loading status string for loading the profile gallery mainly
        public static float percentage = 0f; //loading base value
        private Plugin plugin;
        private IDalamudPluginInterface pg;
        private FileDialogManager fileDialogManager = new FileDialogManager(); 
        public Configuration configuration;
        public static int width = 0, height = 0;
        public static int currentCategory, currentType, currentFocus, currentSetting = 0;
        public static bool editBanner = false;
        public bool ReorderTriggers = false;
        public static bool IsNSFW = false;
        public static bool StartListingCreation  = false;
        // Variables for start date
        int selectedStartYear = 2024;
        int selectedStartMonth = 0; // Example: January
        int selectedStartDay = 1;
        int selectedStartHour = 1;
        int selectedStartMinute = 0;
        int selectedStartAmPm = 0; // Example: AM
        int selectedStartTimezone = 0; // Example: UTC
        public static IDalamudTextureWrap[] banners = new IDalamudTextureWrap[100];
        // Variables for end date
        int selectedEndYear = 2024;
        int selectedEndMonth = 0; // Example: January
        int selectedEndDay = 1;
        int selectedEndHour = 1;
        int selectedEndMinute = 0;
        int selectedEndAmPm = 0; // Example: AM
        int selectedEndTimezone = 0; // Example: UTC
        private static bool DrawDateTimeSelection = false;
        public static bool DrawBannerCreation = false;
        public static bool DrawDetailsCreation = false;
        public static bool DrawInfoCreation = false;
        public static bool DrawTriggerCreation = false;
        public static bool DrawListings = false;
        private bool setEndDate = false;
        private int inclusion = 0;
        internal static List<Listing> listings = new List<Listing>();
        public static string[] listingNames;
        public static float loaderInd;
        internal static bool allLoaded = false;

        public ManageListings(Plugin plugin) : base(
       "LISTINGS", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {

                MinimumSize = new Vector2(200, 200),
                MaximumSize = new Vector2(1000, 1000)
            };

            this.plugin = plugin;
            configuration = plugin.Configuration;
        }
        public override void OnOpen()
        {         
            if (bannerBytes == null)
            {
                //set the avatar to the avatar_holder.png by default
                if (Plugin.PluginInterface is { AssemblyLocation.Directory.FullName: { } path })
                {
                    bannerBytes = File.ReadAllBytes(Path.Combine(path, "UI/common/listings/banner.png"));
                }
            }
            banner = Defines.UICommonImage(Defines.CommonImageTypes.eventsBanner);
        }
        public override void Draw()
        {
            fileDialogManager.Draw();

            using var listing = ImRaii.Child("LISTING");
            if (listing)
            {
                ImGui.Columns(2, "layoutColumns", false);
                ImGui.SetColumnWidth(0, 110);
                for (int i = 0; i < Defines.ListingNavigationVals.Length; i++)
                {
                    var (id, navBtnName) = Defines.ListingNavigationVals[i];
                    if (ImGui.Button(navBtnName, new Vector2(100, 50)))
                    {

                        listings.Clear();
                        DataReceiver.ListingsLoadStatus = -1;
                        ResetElements();
                        DrawListings = true;
                        DataSender.RequestListingsSection(id);
                    }
                }
                ImGui.Spacing();
                if(ImGui.Button("Create Listing", new Vector2(100, 30)))
                {
                    listings.Clear();
                    ResetPages();
                    ResetElements();
                    StartListingCreation = true;
                    DrawDateTimeSelection = true;
                }
                ImGui.NextColumn();


                if(StartListingCreation == true)
                {
                    DrawListingCreation();
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
        }
        public static bool AllLoaded()
        {
            if (DataReceiver.ListingsLoadStatus != -1)
            {
                return true;
            }
            return false;
        }

        public void ResetPages()
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
        }

        public static void DrawListingData()
        {
            using var listingView = ImRaii.Child("LISTING");
            if (listingView)
            {
                for (int i = 0; i < listings.Count; i++)
                {
                    ImGui.Image(listings[i].banner.ImGuiHandle, new Vector2(500, 100));
                    ImGui.TextUnformatted(listings[i].name);
                }
            }
          
        }

        
        public void DrawListingCreation()
        {
            GetImGuiWindowDimensions(out var windowWidth, out var windowHeight);
         
            //banner uploader / viewer
          
            if(DrawDateTimeSelection == true)
            {
                DrawDatePickerTable();
                ImGui.Spacing();
                if (ImGui.Button("Next"))
                {
                    ResetElements();
                    DrawDetailsCreation = true;
                }
            }
            if (DrawDetailsCreation == true)
            {
                if (ImGui.Button("Upload Banner"))
                {
                    editBanner = true;
                }
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(255, 0, 0, 255), "All images are scaled to 500x100 and may NOT contain NSFW content");
                if(banner != null)
                {
                    ImGui.Image(banner.ImGuiHandle, new Vector2(500, 100));
                }
                
              

                //Specifications
                ImGui.Text("Name:");
                ImGui.SameLine();
                ImGui.InputText("##Name", ref ListingName, 100);
                ImGui.Spacing();

                ImGui.Text("Description:");
                ImGui.InputTextMultiline("##Description", ref ListingDescription, 5000, new Vector2(windowWidth, 100));
                ImGui.Spacing();

                ImGui.Text("Rules:");
                ImGui.InputTextMultiline("##Rules", ref ListingRules, 5000, new Vector2(windowWidth, 100));
                ImGui.Spacing();

                ImGui.Text("Trigger List");
                ImGui.InputTextMultiline("##Triggers", ref triggers, 10000, new Vector2(windowWidth, 100));
                ImGui.Spacing();

                ImGui.Text("Availility:");
                ImGui.Combo($"##inclusion", ref inclusion, Defines.inclusions, Defines.inclusions.Length);  // Unique ID
          

                if (ImGui.Button("Back"))
                {
                    ResetElements();
                    DrawDateTimeSelection = true;
                }
                ImGui.SameLine();
                if (ImGui.Button("Next"))
                {
                    ResetElements();
                    DrawInfoCreation = true;
                }
            }
            if(DrawInfoCreation == true)
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
                if (ImGui.Button("Back"))
                {
                    ResetElements();
                    DrawDetailsCreation = true;
                } 
                ImGui.SameLine();
                if (ImGui.Button("Submit Listing"))
                {

                    DataSender.SubmitListing(bannerBytes, ListingName, ListingDescription, ListingRules, inclusion,
                        currentCategory, currentType, currentFocus, currentSetting, IsNSFW, triggers,
                        selectedStartYear, selectedStartMonth + 1, selectedStartDay, selectedStartHour, selectedStartMinute, selectedStartAmPm, selectedStartTimezone,
                        selectedEndYear, selectedEndMonth + 1, selectedEndDay, selectedEndHour, selectedEndMinute, selectedEndAmPm, selectedEndTimezone
                        );
                }
            }

           
            if (editBanner == true)
            {
                editBanner = false;
                EditBanner();
            }
        }
        private void GetImGuiWindowDimensions(out int width, out int height)
        {
            var windowSize = ImGui.GetWindowSize();
            width = (int)windowSize.X -200;
            height = (int)windowSize.Y;
        }
       

    

        

        public void Dispose()
        {
            banner?.Dispose();
        }

        public void AddCategorySelection()
        {
            var (text, desc) = Defines.ListingCategoryVals[currentCategory];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Category", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;

            foreach (var ((newText, newDesc), idx) in Defines.ListingCategoryVals.WithIndex())
            {
                if (ImGui.Selectable(newText, idx == currentCategory))
                    currentCategory = idx;

                ImGuiUtil.SelectableHelpMarker(newDesc);
            }
        }
        public void AddTypeSelection()
        {
            var (text, desc) = Defines.ListingTypeVals[currentType];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Type", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;

            foreach (var ((newText, newDesc), idx) in Defines.ListingTypeVals.WithIndex())
            {
                if (ImGui.Selectable(newText, idx == currentType))
                    currentType = idx;

                ImGuiUtil.SelectableHelpMarker(newDesc);
            }
        }
        public void AddFocusSelection()
        {
            var (text, desc) = Defines.ListingFocusVals[currentFocus];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Focus", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;

            foreach (var ((newText, newDesc), idx) in Defines.ListingFocusVals.WithIndex())
            {
                if (ImGui.Selectable(newText, idx == currentFocus))
                    currentFocus = idx;

                ImGuiUtil.SelectableHelpMarker(newDesc);
            }
        }

        public void AddSettingSelection()
        {
            var (text, desc) = Defines.ListingSettingVals[currentSetting];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Setting", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;

            foreach (var ((newText, newDesc), idx) in Defines.ListingSettingVals.WithIndex())
            {
                if (ImGui.Selectable(newText, idx == currentSetting))
                    currentSetting = idx;

                ImGuiUtil.SelectableHelpMarker(newDesc);
            }
        }

        public void DrawDatePickerTable()
        {
            // Create a table with 2 columns, no row borders
            using var dateTable = ImRaii.Table("DatePickersTable", 2, ImGuiTableFlags.BordersInnerV);
            if (!dateTable)
                return;
            
            // Column 1: Start Date
            ImGui.TableNextColumn();  // Move to the first column
            ImGui.Text("Start Date:");
            DrawDateTimePicker(true);  // Pass 'true' for the start picker
            // Column 2: End Date
            ImGui.TableNextColumn();  // Move to the second column
            ImGui.Checkbox("Set End Date", ref setEndDate);
            if (setEndDate)
            {
                DrawDateTimePicker(false); // Pass 'false' for the end picker
            } // End the table
            
        }
        public void DrawDateTimePicker(bool starting)
        {
            string idPrefix = starting ? "Start" : "End";  // Unique prefix for UI element IDs

            // Variables based on whether we are modifying "Start" or "End"
            ref int year = ref (starting ? ref selectedStartYear : ref selectedEndYear);
            ref int month = ref (starting ? ref selectedStartMonth : ref selectedEndMonth);
            ref int day = ref (starting ? ref selectedStartDay : ref selectedEndDay);
            ref int hour = ref (starting ? ref selectedStartHour : ref selectedEndHour);
            ref int minute = ref (starting ? ref selectedStartMinute : ref selectedEndMinute);
            ref int ampm = ref (starting ? ref selectedStartAmPm : ref selectedEndAmPm);
            ref int timezone = ref (starting ? ref selectedStartTimezone : ref selectedEndTimezone);
            //display the selected datetime info
            ImGui.TextWrapped($"Selected {idPrefix} Date and Time: {year}-{month + 1:D2}-{day:D2} {hour:D2}:{minute:D2} {Defines.amPmOptions[ampm]}");
            ImGui.TextWrapped($"Selected {idPrefix} Timezone: {Defines.timezones[timezone]}");


            // Year selection (limited to current year + 2)
            ImGui.Text($"{idPrefix} Year");
            int minYear = DateTime.Now.Year;
            int maxYear = DateTime.Now.Year + 2;
            ImGui.SliderInt($"##{idPrefix}_year", ref year, minYear, maxYear);  // Unique ID

            // Month selection using Combo (index is 0-based)
            ImGui.Text($"{idPrefix} Month");
            if (ImGui.Combo($"##{idPrefix}_month", ref month, Defines.months, Defines.months.Length))  // Unique ID
            {
                day = 1;
            }

            // Get the correct number of days in the selected month and year
            int monthValue = month + 1;  // Convert 0-based index to 1-12 month value
            int daysInMonth = DateTime.DaysInMonth(year, monthValue);

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
            ImGui.Combo($"##{idPrefix}_ampm", ref ampm, Defines.amPmOptions, Defines.amPmOptions.Length);  // Unique ID

            ImGui.Text($"{idPrefix} Timezone");
            ImGui.Combo($"##{idPrefix}_timezone", ref timezone, Defines.timezones, Defines.timezones.Length);  // Unique ID
        }





        public void EditBanner()
        {
            fileDialogManager.OpenFileDialog("Select Image", "Image{.png,.jpg}", (s, f) =>
            {
                if (!s)
                    return;
                var imagePath = f[0].ToString();        
                bannerBytes = File.ReadAllBytes(imagePath);
                banner = Plugin.TextureProvider.CreateFromImageAsync(bannerBytes).Result;
            }, 0, null, configuration.AlwaysOpenDefaultImport);
        }
    }
}


