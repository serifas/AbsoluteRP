using AbsoluteRP;
using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Ect;
using AbsoluteRP.Windows.NavLayouts;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Social.Views;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;

namespace AbsoluteRP.Windows.Listings
{
    internal class ListingsWindow : Window, IDisposable
    {
        public static Configuration configuration;
        public static int navIndex = 0;
        public static Vector2 buttonScale;
        public static List<Listing> listings = new List<Listing>();
        public static List<Listing> communityListings = new List<Listing>();
        public static List<Listing> myListings = new List<Listing>();
        public static Listing currentListing = null;
        public static bool isLoading = false;
        public static string errorMessage = string.Empty;

        // Detail loading state
        public static bool isDetailLoading = false;
        public static string detailLoadingStep = string.Empty;
        public static int detailLoadedItems = 0;
        public static int detailTotalItems = 0;
        public static bool listingCreated = false;
        public static int lastCreatedListingId = 0;
        public static string loading;
        public static float percentage = 0f;
        public static int loaderInd = 0;
        public static int listingsLoadedCount = 0;
        public static int listingsTotalCount = 0;
        private FileDialogManager _fileDialogManager;
        public static IDalamudTextureWrap banner;
        public static byte[] bannerBytes;
        public static int campaigns = 1;
        public static int events = 2;
        public static int freecompanies = 3;
        public static int venues = 4;
        public static int search = 5;
        public static int view = 4; // Default to venues
        public static bool AnyComboTargeted => Search.PageCountComboOpen || Search.CategoryComboOpen || Search.RegionComboOpen || Search.DataCenterComboOpen || Search.WorldComboOpen || Connections.ConnectionComboOpen;
        public bool DrawListingCreation { get; private set; }

        // Venue sub-view: 0 = Public, 1 = My Venues, 2 = Create/Edit, 3 = Detail
        public static int venueSubView = 0;
        public static bool fetchedPublicVenues = false;
        public static bool fetchedMyVenues = false;

        // Create / Edit state
        public static int editListingId = 0;
        public static string venueName = string.Empty;
        public static string venueTagline = string.Empty;
        public static string venueDescription = string.Empty;
        public static string venueWorld = string.Empty;
        public static string venueDatacenter = string.Empty;
        public static string venueDistrict = string.Empty;
        public static int venueWard = 0;
        public static int venuePlot = 0;
        public static bool venueNSFW = false;
        public static string venueContact = string.Empty;
        public static string venueTags = string.Empty;
        public static string venueDiscord = string.Empty;
        public static string venueWebsite = string.Empty;
        public static byte[] createBannerBytes = null;
        public static IDalamudTextureWrap createBannerTexture = null;
        public static byte[] createLogoBytes = null;
        public static IDalamudTextureWrap createLogoTexture = null;
        public static List<ListingSchedule> editSchedules = new List<ListingSchedule>();
        public static List<MenuItemData> editMenuItems = new List<MenuItemData>();
        public static List<StaffEntry> editStaff = new List<StaffEntry>();
        public static List<BookableEntry> editBookables = new List<BookableEntry>();
        public static bool venueBookingEnabled = false;
        public static bool pendingPublish = false; // Whether to publish after creation

        // Entry edit sub-view: -1 = card grid (default), >= 0 = editing entry at that index
        private static int editingMenuIndex = -1;
        private static int editingStaffIndex = -1;
        private static int editingBookableIndex = -1;

        // Delete confirmation state
        private static bool showDeleteConfirm = false;
        private static string deleteConfirmLabel = string.Empty;
        private static Action deleteConfirmAction = null;

        // Venue create/edit tab: 0=Details, 1=Schedule, 2=Menu, 3=Staff, 4=Booking
        public static int venueEditTab = 0;

        // Venue detail view tab: 0=Overview, 1=Menu, 2=Staff, 3=Reservations
        private static int venueDetailTab = 0;

        // View More popup state
        private static bool showViewMorePopup = false;
        private static string viewMoreTitle = string.Empty;
        private static string viewMoreSubtitle = string.Empty;
        private static string viewMoreDescription = string.Empty;
        private static string viewMorePrice = string.Empty;
        private static bool viewMoreIsOOCPrice = false;
        private static List<field> viewMoreCustomFields = new List<field>();
        private static List<EntryImage> viewMoreImages = new List<EntryImage>();
        private static List<ListingSchedule> viewMoreAvailableTimes = new List<ListingSchedule>();

        // My Bookings data
        public static List<BookingRequest> myBookings = new List<BookingRequest>();
        public static List<BookingRequest> incomingBookingRequests = new List<BookingRequest>();
        public static bool fetchedMyBookings = false;
        public static bool fetchedIncomingBookings = false;

        // Booking popup state
        private static bool showBookingPopup = false;
        private static int bookingEntryId = 0;
        private static string bookingEntryName = "";
        private static string bookingNotes = "";
        private static int bookingDateYear = DateTime.Now.Year;
        private static int bookingDateMonth = DateTime.Now.Month;
        private static int bookingDateDay = DateTime.Now.Day;
        private static int bookingTimeHour = 12;
        private static int bookingTimeMinute = 0;
        private static bool bookingTimePM = false;
        private static int bookingTimezoneIndex = 0;
        private static readonly string[] BookingTimezones = new[] { "UTC", "EST", "CST", "MST", "PST", "GMT", "CET", "JST", "AEST" };

        private static readonly string[] DayNames = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        private static readonly string[] TimezoneNames = { "EST", "CST", "MST", "PST", "UTC", "GMT", "CET", "EET", "JST", "AEST", "NZST" };
        private static readonly string[] AmPmNames = { "AM", "PM" };

        /// <summary>
        /// Draws a banner image that fits inside the given box without stretching.
        /// Uses "contain" mode: scales down to fit, centers, and letterboxes with bg color.
        /// </summary>
        private static void DrawBannerFit(ImDrawListPtr dl, IDalamudTextureWrap tex, Vector2 boxMin, float boxW, float boxH, float rounding = 0f, ImDrawFlags roundFlags = ImDrawFlags.None)
        {
            if (tex == null || tex.Handle == IntPtr.Zero) return;
            // Fill background first
            dl.AddRectFilled(boxMin, new Vector2(boxMin.X + boxW, boxMin.Y + boxH),
                ImGui.ColorConvertFloat4ToU32(ThemeManager.BgDark), rounding, roundFlags);

            float imgW = tex.Width;
            float imgH = tex.Height;
            if (imgW <= 0 || imgH <= 0) return;

            float imgAspect = imgW / imgH;
            float boxAspect = boxW / boxH;

            float drawW, drawH;
            if (imgAspect > boxAspect)
            {
                // Image is wider than box — fit to width
                drawW = boxW;
                drawH = boxW / imgAspect;
            }
            else
            {
                // Image is taller than box — fit to height
                drawH = boxH;
                drawW = boxH * imgAspect;
            }

            // Center in box
            float offsetX = (boxW - drawW) / 2f;
            float offsetY = (boxH - drawH) / 2f;
            var imgMin = new Vector2(boxMin.X + offsetX, boxMin.Y + offsetY);
            var imgMax = new Vector2(imgMin.X + drawW, imgMin.Y + drawH);

            dl.AddImageRounded(tex.Handle, imgMin, imgMax, Vector2.Zero, Vector2.One, 0xFFFFFFFF, rounding, roundFlags);
        }

        /// <summary>
        /// Draws a banner using ImGui.Image that fits without stretching.
        /// </summary>
        private static void DrawBannerImageFit(IDalamudTextureWrap tex, float maxW, float maxH)
        {
            if (tex == null || tex.Handle == IntPtr.Zero) { ImGui.Dummy(new Vector2(maxW, maxH)); return; }
            float imgW = tex.Width;
            float imgH = tex.Height;
            if (imgW <= 0 || imgH <= 0) { ImGui.Dummy(new Vector2(maxW, maxH)); return; }

            float imgAspect = imgW / imgH;
            float boxAspect = maxW / maxH;

            float drawW, drawH;
            if (imgAspect > boxAspect)
            {
                drawW = maxW;
                drawH = maxW / imgAspect;
            }
            else
            {
                drawH = maxH;
                drawW = maxH * imgAspect;
            }

            // Center horizontally
            float padX = (maxW - drawW) / 2f;
            if (padX > 0) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padX);
            ImGui.Image(tex.Handle, new Vector2(drawW, drawH));
        }

        public ListingsWindow() : base(
       "LISTINGS", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
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

        public override void OnOpen() { }

        public override void Draw()
        {
            var hovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows);
            var clicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left);
            var anyActive = ImGui.IsAnyItemActive();
            var alreadyFocused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
            var focusRequested = hovered && clicked && !anyActive && !alreadyFocused;

            _fileDialogManager.Draw();

            try
            {
                Vector2 mainPanelPos = ImGui.GetWindowPos();
                Vector2 mainPanelSize = ImGui.GetWindowSize();

                if (view == venues) DrawVenuesView(mainPanelSize);
                else if (view == campaigns) ThemeManager.SubtitleText("Campaigns coming soon...");
                else if (view == events) ThemeManager.SubtitleText("Events coming soon...");
                else if (view == freecompanies) ThemeManager.SubtitleText("Free Companies coming soon...");
                else if (view == search) ThemeManager.SubtitleText("Search coming soon...");

                if (!string.IsNullOrEmpty(errorMessage))
                    ImGui.TextColored(ThemeManager.Error, errorMessage);

                if (focusRequested && !ImGui.IsAnyItemActive() && !AnyComboTargeted)
                {
                    ImGui.SetWindowFocus("ListingNavigation");
                    ImGui.SetWindowFocus("LISTINGS");
                }

                float headerHeight = 48f;
                float buttonSize = ImGui.GetIO().FontGlobalScale * 45;
                int buttonCount = 5;
                float navHeight = buttonSize * buttonCount * 1.2f;
                ImGui.SetNextWindowPos(new Vector2(mainPanelPos.X - buttonSize * 1.5f, mainPanelPos.Y + headerHeight), ImGuiCond.Always);
                ImGui.SetNextWindowSize(new Vector2(buttonSize * 1.5f, navHeight), ImGuiCond.Always);
                ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar;
                Navigation nav = NavigationLayouts.ListingsNavigation();
                UIHelpers.DrawSideNavigation("LISTINGS", "ListingNavigation", ref navIndex, flags, nav, focusRequested);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error drawing listings window: {ex.Message}");
            }
        }

        #region Venues View

        private void DrawVenuesView(Vector2 windowSize)
        {
            if (venueSubView == 2) { DrawVenueCreateEdit(windowSize); return; }
            if (venueSubView == 3) { DrawVenueDetail(windowSize); return; }

            if (ImGui.BeginTabBar("##VenueTabs"))
            {
                if (ImGui.BeginTabItem("Public Venues"))
                {
                    venueSubView = 0;
                    DrawPublicVenues(windowSize);
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("My Venues"))
                {
                    venueSubView = 1;
                    DrawMyVenues(windowSize);
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("My Bookings"))
                {
                    DrawMyBookings(windowSize);
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
        }

        private void DrawPublicVenues(Vector2 windowSize)
        {
            if (!fetchedPublicVenues)
            {
                fetchedPublicVenues = true;
                DataSender.FetchListings(4, 0, string.Empty, string.Empty, false, false, 1, 20, 1);
            }

            if (isLoading)
            {
                ImGui.Spacing(); ImGui.Spacing();
                float progress = listingsTotalCount > 0 ? (float)listingsLoadedCount / listingsTotalCount : 0f;
                string label = listingsTotalCount > 0
                    ? $"Loading venues... {listingsLoadedCount}/{listingsTotalCount}"
                    : "Loading venues...";
                ThemeManager.StyledProgressBar(progress, new Vector2(windowSize.X - 32, 22), label, null);
                return;
            }
            if (listings.Count == 0) { ImGui.Spacing(); ThemeManager.SubtitleText("No public venues found."); return; }

            if (ImGui.BeginChild("##PublicVenuesList", new Vector2(windowSize.X - 16, windowSize.Y - 80), false))
            {
                float cardWidth = windowSize.X - 32;
                foreach (var venue in listings)
                {
                    if (venue.type != 4) continue;
                    DrawVenueCard(venue, cardWidth);
                    ImGui.Spacing();
                }
                ImGui.EndChild();
            }
        }

        private void DrawMyVenues(Vector2 windowSize)
        {
            if (ThemeManager.PillButton("Add Venue"))
            {
                ResetCreateForm();
                venueSubView = 2;
            }
            ImGui.Spacing();
            ThemeManager.GradientSeparator();
            ImGui.Spacing();

            if (!fetchedMyVenues)
            {
                fetchedMyVenues = true;
                DataSender.FetchMyListings();
            }

            if (isLoading)
            {
                ImGui.Spacing();
                float progress = listingsTotalCount > 0 ? (float)listingsLoadedCount / listingsTotalCount : 0f;
                string label = listingsTotalCount > 0
                    ? $"Loading your venues... {listingsLoadedCount}/{listingsTotalCount}"
                    : "Loading your venues...";
                ThemeManager.StyledProgressBar(progress, new Vector2(windowSize.X - 32, 22), label, null);
                return;
            }
            if (myListings.Count == 0) { ThemeManager.SubtitleText("You haven't created any venues yet."); return; }

            if (ImGui.BeginChild("##MyVenuesList", new Vector2(windowSize.X - 16, windowSize.Y - 120), false))
            {
                float cardWidth = windowSize.X - 32;
                foreach (var venue in myListings)
                {
                    if (venue.type != 4) continue;
                    if (ThemeManager.BeginCard($"myVenue_{venue.id}", new Vector2(cardWidth, 60)))
                    {
                        ImGui.Text(venue.name);
                        ImGui.SameLine(cardWidth - 220);
                        ThemeManager.Badge(venue.isActive ? "Published" : "Draft", venue.isActive ? ThemeManager.Success : ThemeManager.FontMuted);
                        ImGui.SameLine();
                        ThemeManager.SubtitleText($"{venue.viewCount} views");
                        ImGui.SameLine(cardWidth - 70);
                        if (ThemeManager.GhostButton($"Edit##{venue.id}"))
                            LoadVenueForEdit(venue);
                        ThemeManager.EndCard();
                    }
                    ImGui.Spacing();
                }
                ImGui.EndChild();
            }
        }

        private void DrawVenueCard(Listing venue, float cardWidth)
        {
            float cardHeight = 180;
            float bannerHeight = 100;
            float logoSize = 56;

            if (ThemeManager.BeginCard($"venueCard_{venue.id}", new Vector2(cardWidth, cardHeight)))
            {
                var cardPos = ImGui.GetCursorScreenPos();
                var dl = ImGui.GetWindowDrawList();

                // Banner (fit without stretching)
                if (venue.banner != null && venue.banner.Handle != IntPtr.Zero)
                    DrawBannerFit(dl, venue.banner, cardPos, cardWidth - 16, bannerHeight, 6f, ImDrawFlags.RoundCornersTop);
                else
                    dl.AddRectFilled(cardPos, new Vector2(cardPos.X + cardWidth - 16, cardPos.Y + bannerHeight), ImGui.ColorConvertFloat4ToU32(ThemeManager.BgDark), 6f, ImDrawFlags.RoundCornersTop);

                // Logo overlapping banner
                float logoX = cardPos.X + 12;
                float logoY = cardPos.Y + bannerHeight - logoSize / 2;
                if (venue.logo != null && venue.logo.Handle != IntPtr.Zero)
                {
                    dl.AddCircleFilled(new Vector2(logoX + logoSize / 2, logoY + logoSize / 2), logoSize / 2 + 2, 0xFFFFFFFF);
                    dl.AddImageRounded(venue.logo.Handle, new Vector2(logoX, logoY), new Vector2(logoX + logoSize, logoY + logoSize), Vector2.Zero, Vector2.One, 0xFFFFFFFF, logoSize / 2);
                }
                else
                    dl.AddCircleFilled(new Vector2(logoX + logoSize / 2, logoY + logoSize / 2), logoSize / 2, ImGui.ColorConvertFloat4ToU32(ThemeManager.AccentMuted));

                // Name + tagline
                ImGui.SetCursorScreenPos(new Vector2(logoX + logoSize + 10, logoY + 4));
                ThemeManager.AccentText(venue.name);
                ImGui.SetCursorScreenPos(new Vector2(logoX + logoSize + 10, logoY + 22));
                ThemeManager.SubtitleText(!string.IsNullOrEmpty(venue.tagline) ? venue.tagline : "");

                // Location row
                ImGui.SetCursorScreenPos(new Vector2(cardPos.X + 12, logoY + logoSize + 6));
                string loc = string.Empty;
                if (!string.IsNullOrEmpty(venue.district)) loc = venue.district;
                if (venue.ward > 0) loc += $" W{venue.ward}";
                if (venue.plot > 0) loc += $" P{venue.plot}";
                if (!string.IsNullOrEmpty(venue.world)) loc += $" - {venue.world}";
                ThemeManager.SubtitleText(loc);

                ImGui.SameLine(cardWidth - 90);
                if (ThemeManager.PillButton($"View##{venue.id}"))
                {
                    currentListing = null;
                    isDetailLoading = true;
                    detailLoadingStep = "Requesting venue data...";
                    detailLoadedItems = 0;
                    detailTotalItems = 0;
                    venueSubView = 3;
                    DataSender.FetchListingDetail(venue.id);
                }

                ThemeManager.EndCard();
            }
        }

        #endregion

        #region Venue Create / Edit

        private void ResetCreateForm()
        {
            editListingId = 0;
            venueName = venueTagline = venueDescription = venueWorld = venueDatacenter = venueDistrict = venueContact = venueTags = venueDiscord = venueWebsite = string.Empty;
            venueWard = venuePlot = 0;
            venueNSFW = false;
            venueBookingEnabled = false;
            createBannerBytes = createLogoBytes = null;
            createBannerTexture = createLogoTexture = null;
            editSchedules.Clear();
            editMenuItems.Clear();
            editStaff.Clear();
            editBookables.Clear();
            venueEditTab = 0;
            errorMessage = string.Empty;
        }

        private void LoadVenueForEdit(Listing venue)
        {
            editListingId = venue.id;
            venueName = venue.name; venueTagline = venue.tagline; venueDescription = venue.description;
            venueWorld = venue.world; venueDatacenter = venue.datacenter; venueDistrict = venue.district;
            venueWard = venue.ward; venuePlot = venue.plot; venueNSFW = venue.isNSFW;
            venueContact = venue.contactInfo; venueTags = venue.tags; venueDiscord = venue.discordLink; venueWebsite = venue.websiteLink;
            createBannerTexture = venue.banner; createLogoTexture = venue.logo;
            createBannerBytes = createLogoBytes = null;
            editSchedules = venue.schedules != null ? new List<ListingSchedule>(venue.schedules) : new List<ListingSchedule>();
            editMenuItems = venue.menuItems != null ? new List<MenuItemData>(venue.menuItems) : new List<MenuItemData>();
            venueBookingEnabled = venue.bookingEnabled;
            isDetailLoading = true;
            detailLoadingStep = "Loading venue for editing...";
            detailLoadedItems = 0;
            detailTotalItems = 0;
            venueSubView = 2;
            DataSender.FetchListingDetail(venue.id);
        }

        private void DrawVenueCreateEdit(Vector2 windowSize)
        {
            bool isEdit = editListingId > 0;
            if (ThemeManager.GhostButton("< Back")) { venueSubView = 1; return; }
            ImGui.SameLine();
            ThemeManager.SectionHeader(isEdit ? "Edit Venue" : "Create Venue");
            ImGui.Spacing();

            // Show loading bar while detail is loading for edit
            if (isEdit && isDetailLoading)
            {
                float progress = detailTotalItems > 0 ? (float)detailLoadedItems / detailTotalItems : 0f;
                string label = detailTotalItems > 0
                    ? $"{detailLoadingStep} ({detailLoadedItems}/{detailTotalItems})"
                    : detailLoadingStep;
                ThemeManager.StyledProgressBar(progress, new Vector2(windowSize.X - 32, 22), label, null);
                return;
            }

            // Tab bar for venue sections
            if (ImGui.BeginTabBar("##VenueEditTabs"))
            {
                if (ImGui.BeginTabItem("Details"))
                {
                    venueEditTab = 0;
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Schedule"))
                {
                    venueEditTab = 1;
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Menu"))
                {
                    venueEditTab = 2;
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Staff"))
                {
                    venueEditTab = 3;
                    ImGui.EndTabItem();
                }
                if (venueBookingEnabled && ImGui.BeginTabItem("Services"))
                {
                    venueEditTab = 4;
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
            ImGui.Spacing();

            // Calculate remaining height: leave room for buttons at the bottom
            float buttonsHeight = 50f;
            float remainingHeight = ImGui.GetContentRegionAvail().Y - buttonsHeight;
            if (remainingHeight < 100) remainingHeight = 100;

            if (ImGui.BeginChild("##VenueForm", new Vector2(windowSize.X - 16, remainingHeight), false))
            {
                switch (venueEditTab)
                {
                    case 0: DrawDetailsTab(windowSize); break;
                    case 1: DrawScheduleTab(windowSize); break;
                    case 2: DrawMenuTab(windowSize); break;
                    case 3: DrawStaffTab(windowSize); break;
                    case 4: DrawBookingTab(windowSize); break;
                }
                ImGui.EndChild();
            }

            // Publish controls always visible at bottom
            ImGui.Spacing();
            DrawPublishControls(isEdit);
            DrawDeleteConfirmPopup();
        }

        private void DrawDetailsTab(Vector2 windowSize)
        {
            // Banner
            ThemeManager.AccentText("Banner Image");
            ThemeManager.SubtitleText("Recommended: 1000 x 250 px");
            ImGui.Spacing();
            float bannerW = Math.Min(windowSize.X - 48, 500);
            if (createBannerTexture != null && createBannerTexture.Handle != IntPtr.Zero)
                DrawBannerImageFit(createBannerTexture, bannerW, 120);
            else
            {
                var pos = ImGui.GetCursorScreenPos();
                ImGui.GetWindowDrawList().AddRectFilled(pos, new Vector2(pos.X + bannerW, pos.Y + 120), ImGui.ColorConvertFloat4ToU32(ThemeManager.BgDark), 6f);
                ImGui.Dummy(new Vector2(bannerW, 120));
            }
            if (ThemeManager.GhostButton("Upload Banner"))
            {
                _fileDialogManager.OpenFileDialog("Select Banner", ".png,.jpg,.jpeg", (ok, path) =>
                {
                    if (ok && path != null && path.Count > 0)
                    {
                        try
                        {
                            createBannerBytes = System.IO.File.ReadAllBytes(path[0]);
                            Task.Run(async () => { createBannerTexture = await Plugin.TextureProvider.CreateFromImageAsync(createBannerBytes); });
                        }
                        catch { }
                    }
                }, 1, null, false);
            }
            ImGui.Spacing();

            // Logo
            ThemeManager.AccentText("Logo Image");
            ImGui.Spacing();
            if (createLogoTexture != null && createLogoTexture.Handle != IntPtr.Zero)
                ImGui.Image(createLogoTexture.Handle, new Vector2(64, 64));
            else
                ImGui.Dummy(new Vector2(64, 64));
            ImGui.SameLine();
            if (ThemeManager.GhostButton("Upload Logo"))
            {
                _fileDialogManager.OpenFileDialog("Select Logo", ".png,.jpg,.jpeg", (ok, path) =>
                {
                    if (ok && path != null && path.Count > 0)
                    {
                        try
                        {
                            createLogoBytes = System.IO.File.ReadAllBytes(path[0]);
                            Task.Run(async () => { createLogoTexture = await Plugin.TextureProvider.CreateFromImageAsync(createLogoBytes); });
                        }
                        catch { }
                    }
                }, 1, null, false);
            }

            ImGui.Spacing(); ThemeManager.GradientSeparator(); ImGui.Spacing();

            // Basic Info
            ThemeManager.SectionHeader("Basic Info");
            ImGui.Spacing();
            ThemeManager.StyledInput("Name", ref venueName, 100);
            ThemeManager.StyledInput("Tagline", ref venueTagline, 200);
            ImGui.Text("Description");
            ImGui.InputTextMultiline("##VenueDesc", ref venueDescription, 4000, new Vector2(windowSize.X - 48, 100));

            ImGui.Spacing(); ThemeManager.GradientSeparator(); ImGui.Spacing();

            // Location
            ThemeManager.SectionHeader("Location");
            ImGui.Spacing();
            ThemeManager.StyledInput("World", ref venueWorld, 50);
            ThemeManager.StyledInput("District", ref venueDistrict, 50);
            string wardStr = venueWard > 0 ? venueWard.ToString() : string.Empty;
            ThemeManager.StyledInput("Ward", ref wardStr, 5);
            if (int.TryParse(wardStr, out int w)) venueWard = w; else if (string.IsNullOrEmpty(wardStr)) venueWard = 0;
            string plotStr = venuePlot > 0 ? venuePlot.ToString() : string.Empty;
            ThemeManager.StyledInput("Plot", ref plotStr, 5);
            if (int.TryParse(plotStr, out int p)) venuePlot = p; else if (string.IsNullOrEmpty(plotStr)) venuePlot = 0;

            ImGui.Spacing(); ThemeManager.GradientSeparator(); ImGui.Spacing();

            // Links & Contact
            ThemeManager.SectionHeader("Links & Contact");
            ImGui.Spacing();
            ThemeManager.StyledInput("Discord", ref venueDiscord, 200);
            ThemeManager.StyledInput("Website", ref venueWebsite, 200);
            ThemeManager.StyledInput("Contact", ref venueContact, 200);
            ThemeManager.StyledInput("Tags (comma separated)", ref venueTags, 200);
            ImGui.Checkbox("NSFW", ref venueNSFW);
            ImGui.SameLine(); ImGui.Spacing(); ImGui.SameLine();
            ImGui.Checkbox("Enable Booking", ref venueBookingEnabled);
        }

        private void DrawScheduleTab(Vector2 windowSize)
        {
            ThemeManager.SectionHeader("Opening Schedule");
            ImGui.Spacing();
            DrawScheduleEditor();
        }

        private void DrawMenuTab(Vector2 windowSize)
        {
            // Sub-view: editing a specific menu item
            if (editingMenuIndex >= 0 && editingMenuIndex < editMenuItems.Count)
            {
                DrawEditEntry_Menu(editMenuItems[editingMenuIndex], editingMenuIndex, windowSize);
                return;
            }
            editingMenuIndex = -1;

            ThemeManager.SectionHeader("Menu Items");
            ThemeManager.SubtitleText("Add food, drinks, and specials to your venue's menu.");
            ImGui.Spacing();

            // Card grid
            float cardW = 320, cardH = 170, spacing = 10;
            int cols = Math.Max(1, (int)((windowSize.X - 32) / (cardW + spacing)));
            int col = 0;
            for (int i = 0; i < editMenuItems.Count; i++)
            {
                int capturedIdx = i;
                if (col > 0) ImGui.SameLine(0, spacing);
                DrawEditEntryCard(editMenuItems[i].itemName, editMenuItems[i].images, editMenuItems[i].mainImageIndex, $"mi_{i}", cardW, cardH,
                    () => { editingMenuIndex = capturedIdx; },
                    () => { if (capturedIdx < editMenuItems.Count) editMenuItems.RemoveAt(capturedIdx); });
                col++;
                if (col >= cols) col = 0;
            }

            ImGui.Spacing();
            if (ThemeManager.GhostButton("+ Add Menu Item"))
            {
                editMenuItems.Add(new MenuItemData { category = "Food", sortOrder = editMenuItems.Count });
            }
        }

        private void DrawEditEntry_Menu(MenuItemData item, int index, Vector2 windowSize)
        {
            if (ThemeManager.GhostButton("< Back to Menu Items")) { editingMenuIndex = -1; return; }
            ImGui.Spacing();
            ThemeManager.SectionHeader($"Edit: {(string.IsNullOrEmpty(item.itemName) ? "New Item" : item.itemName)}");
            ImGui.Spacing();

            string iName = item.itemName, iDesc = item.description, iCat = item.category, iPrice = item.price;
            ThemeManager.SubtitleText("Item Name"); ThemeManager.StyledInput("##miName", ref iName, 100); item.itemName = iName;
            ThemeManager.SubtitleText("Category"); ThemeManager.StyledInput("##miCat", ref iCat, 50); item.category = iCat;
            ThemeManager.SubtitleText("Description");
            ImGui.InputTextMultiline("##miDesc", ref iDesc, 2000, new Vector2(ImGui.GetContentRegionAvail().X, 80));
            item.description = iDesc;
            ThemeManager.SubtitleText("Price"); ThemeManager.StyledInput("##miPrice", ref iPrice, 20); item.price = iPrice;
            ImGui.SameLine();
            bool ooc = item.isOOCPrice;
            if (ImGui.Checkbox("OOC Price##mi", ref ooc)) item.isOOCPrice = ooc;

            ImGui.Spacing();
            ThemeManager.GradientSeparator(); ImGui.Spacing();
            int miMainIdx = item.mainImageIndex;
            DrawImageGalleryEditor(item.images, ref miMainIdx, $"mi_{index}", windowSize);
            item.mainImageIndex = miMainIdx;
        }

        private void DrawStaffTab(Vector2 windowSize)
        {
            if (editingStaffIndex >= 0 && editingStaffIndex < editStaff.Count)
            {
                DrawEditEntry_Staff(editStaff[editingStaffIndex], editingStaffIndex, windowSize);
                return;
            }
            editingStaffIndex = -1;

            ThemeManager.SectionHeader("Staff Members");
            ThemeManager.SubtitleText("Add your venue's staff with their roles and details.");
            ImGui.Spacing();

            float cardW = 320, cardH = 170, spacing = 10;
            int cols = Math.Max(1, (int)((windowSize.X - 32) / (cardW + spacing)));
            int col = 0;
            for (int i = 0; i < editStaff.Count; i++)
            {
                int capturedIdx = i;
                if (col > 0) ImGui.SameLine(0, spacing);
                DrawEditEntryCard(editStaff[i].name, editStaff[i].images, editStaff[i].mainImageIndex, $"st_{i}", cardW, cardH,
                    () => { editingStaffIndex = capturedIdx; },
                    () => { if (capturedIdx < editStaff.Count) editStaff.RemoveAt(capturedIdx); });
                col++;
                if (col >= cols) col = 0;
            }

            ImGui.Spacing();
            if (ThemeManager.GhostButton("+ Add Staff Member"))
            {
                editStaff.Add(new StaffEntry { sortOrder = editStaff.Count });
            }
        }

        private void DrawEditEntry_Staff(StaffEntry staff, int index, Vector2 windowSize)
        {
            if (ThemeManager.GhostButton("< Back to Staff")) { editingStaffIndex = -1; return; }
            ImGui.Spacing();
            ThemeManager.SectionHeader($"Edit: {(string.IsNullOrEmpty(staff.name) ? "New Staff" : staff.name)}");
            ImGui.Spacing();

            string sName = staff.name, sRole = staff.role, sDesc = staff.description;
            ThemeManager.SubtitleText("Name"); ThemeManager.StyledInput("##stName", ref sName, 100); staff.name = sName;
            ThemeManager.SubtitleText("Role"); ThemeManager.StyledInput("##stRole", ref sRole, 100); staff.role = sRole;
            ThemeManager.SubtitleText("Description");
            ImGui.InputTextMultiline("##stDesc", ref sDesc, 2000, new Vector2(ImGui.GetContentRegionAvail().X, 80));
            staff.description = sDesc;

            // Custom fields
            ImGui.Spacing();
            ThemeManager.SubtitleText("Custom Fields");
            for (int f = 0; f < staff.customFields.Count; f++)
            {
                ImGui.PushID($"sf_{index}_{f}");
                string fName = staff.customFields[f].name, fVal = staff.customFields[f].description;
                ImGui.SetNextItemWidth(120); ThemeManager.StyledInput("##fn", ref fName, 50);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(200); ThemeManager.StyledInput("##fv", ref fVal, 200);
                staff.customFields[f] = new field { index = f, name = fName, description = fVal };
                ImGui.SameLine();
                if (ThemeManager.DangerButton($"X##sf{f}"))
                { staff.customFields.RemoveAt(f); f--; }
                ImGui.PopID();
            }
            if (ThemeManager.GhostButton($"+ Add Field##st{index}"))
                staff.customFields.Add(new field { index = staff.customFields.Count });

            ImGui.Spacing();
            ThemeManager.GradientSeparator(); ImGui.Spacing();
            int stMainIdx = staff.mainImageIndex;
            DrawImageGalleryEditor(staff.images, ref stMainIdx, $"st_{index}", windowSize);
            staff.mainImageIndex = stMainIdx;
        }

        private void DrawBookingTab(Vector2 windowSize)
        {
            if (editingBookableIndex >= 0 && editingBookableIndex < editBookables.Count)
            {
                DrawEditEntry_Bookable(editBookables[editingBookableIndex], editingBookableIndex, windowSize);
                return;
            }
            editingBookableIndex = -1;

            ThemeManager.SectionHeader("Services");
            ThemeManager.SubtitleText("Create services with available times that visitors can book.");
            ImGui.Spacing();

            float cardW = 320, cardH = 170, spacing = 10;
            int cols = Math.Max(1, (int)((windowSize.X - 32) / (cardW + spacing)));
            int col = 0;
            for (int i = 0; i < editBookables.Count; i++)
            {
                int capturedIdx = i;
                if (col > 0) ImGui.SameLine(0, spacing);
                DrawEditEntryCard(editBookables[i].name, editBookables[i].images, editBookables[i].mainImageIndex, $"bk_{i}", cardW, cardH,
                    () => { editingBookableIndex = capturedIdx; },
                    () => { if (capturedIdx < editBookables.Count) editBookables.RemoveAt(capturedIdx); });
                col++;
                if (col >= cols) col = 0;
            }

            ImGui.Spacing();
            if (ThemeManager.GhostButton("+ Add Bookable Service"))
            {
                editBookables.Add(new BookableEntry { sortOrder = editBookables.Count });
            }
        }

        private void DrawEditEntry_Bookable(BookableEntry entry, int index, Vector2 windowSize)
        {
            if (ThemeManager.GhostButton("< Back to Services")) { editingBookableIndex = -1; return; }
            ImGui.Spacing();
            ThemeManager.SectionHeader($"Edit: {(string.IsNullOrEmpty(entry.name) ? "New Service" : entry.name)}");
            ImGui.Spacing();

            string bName = entry.name, bDesc = entry.description, bPrice = entry.price;
            ThemeManager.SubtitleText("Service Name"); ThemeManager.StyledInput("##bkName", ref bName, 100); entry.name = bName;
            ThemeManager.SubtitleText("Description");
            ImGui.InputTextMultiline("##bkDesc", ref bDesc, 2000, new Vector2(ImGui.GetContentRegionAvail().X, 80));
            entry.description = bDesc;
            ThemeManager.SubtitleText("Price"); ThemeManager.StyledInput("##bkPrice", ref bPrice, 20); entry.price = bPrice;
            ImGui.SameLine();
            bool ooc = entry.isOOCPrice;
            if (ImGui.Checkbox("OOC##bk", ref ooc)) entry.isOOCPrice = ooc;

            string maxStr = entry.maxSlots.ToString();
            ThemeManager.SubtitleText("Max Slots"); ThemeManager.StyledInput("##bkSlots", ref maxStr, 5);
            if (int.TryParse(maxStr, out int ms) && ms > 0) entry.maxSlots = ms;

            // Available times
            ImGui.Spacing();
            ThemeManager.GradientSeparator(); ImGui.Spacing();
            ThemeManager.SubtitleText("Available Times");
            for (int t = 0; t < entry.availableTimes.Count; t++)
            {
                ImGui.PushID($"bt_{index}_{t}");
                var sched = entry.availableTimes[t];
                int dayIdx = sched.dayOfWeek;
                ImGui.SetNextItemWidth(100);
                if (ImGui.Combo("##Day", ref dayIdx, DayNames, DayNames.Length)) sched.dayOfWeek = dayIdx;
                ImGui.SameLine();

                To12Hour(sched.startTime, out int sH, out int sM, out int sAP);
                ImGui.SetNextItemWidth(40); ImGui.InputInt("##sH", ref sH, 0); sH = Math.Clamp(sH, 1, 12);
                ImGui.SameLine(); ImGui.Text(":"); ImGui.SameLine();
                ImGui.SetNextItemWidth(40); ImGui.InputInt("##sM", ref sM, 0); sM = Math.Clamp(sM, 0, 59);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(50); ImGui.Combo("##sAP", ref sAP, AmPmNames, 2);
                sched.startTime = From12Hour(sH, sM, sAP);
                ImGui.SameLine(); ThemeManager.SubtitleText("to"); ImGui.SameLine();

                To12Hour(sched.endTime, out int eH, out int eM, out int eAP);
                ImGui.SetNextItemWidth(40); ImGui.InputInt("##eH", ref eH, 0); eH = Math.Clamp(eH, 1, 12);
                ImGui.SameLine(); ImGui.Text(":"); ImGui.SameLine();
                ImGui.SetNextItemWidth(40); ImGui.InputInt("##eM", ref eM, 0); eM = Math.Clamp(eM, 0, 59);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(50); ImGui.Combo("##eAP", ref eAP, AmPmNames, 2);
                sched.endTime = From12Hour(eH, eM, eAP);
                ImGui.SameLine();
                if (ThemeManager.DangerButton($"X##bt{t}"))
                { entry.availableTimes.RemoveAt(t); t--; }
                ImGui.PopID();
            }
            if (ThemeManager.GhostButton($"+ Add Time Slot##bk{index}"))
                entry.availableTimes.Add(new ListingSchedule { isRecurring = true, startTime = TimeSpan.FromHours(12), endTime = TimeSpan.FromHours(12) });

            ImGui.Spacing();
            ThemeManager.GradientSeparator(); ImGui.Spacing();
            int bkMainIdx = entry.mainImageIndex;
            DrawImageGalleryEditor(entry.images, ref bkMainIdx, $"bk_{index}", windowSize);
            entry.mainImageIndex = bkMainIdx;
        }

        /// <summary>
        /// Convert 24h total minutes to 12h components
        /// </summary>
        private static void To12Hour(TimeSpan time, out int hour12, out int minute, out int amPmIdx)
        {
            int h24 = (int)time.TotalMinutes / 60;
            minute = (int)time.TotalMinutes % 60;
            if (h24 == 0) { hour12 = 12; amPmIdx = 0; }
            else if (h24 < 12) { hour12 = h24; amPmIdx = 0; }
            else if (h24 == 12) { hour12 = 12; amPmIdx = 1; }
            else { hour12 = h24 - 12; amPmIdx = 1; }
        }

        /// <summary>
        /// Convert 12h components back to 24h TimeSpan
        /// </summary>
        private static TimeSpan From12Hour(int hour12, int minute, int amPmIdx)
        {
            int h24;
            if (amPmIdx == 0) // AM
                h24 = (hour12 == 12) ? 0 : hour12;
            else // PM
                h24 = (hour12 == 12) ? 12 : hour12 + 12;
            return TimeSpan.FromMinutes(h24 * 60 + minute);
        }

        /// <summary>Gets the main (cover) image for an entry, using mainImageIndex with fallback to first image.</summary>
        private static EntryImage GetMainImage(List<EntryImage> images, int mainImageIndex)
        {
            if (images == null || images.Count == 0) return null;
            int idx = (mainImageIndex >= 0 && mainImageIndex < images.Count) ? mainImageIndex : 0;
            return images[idx];
        }

        /// <summary>Draws a card for an entry in the edit grid with main image thumbnail, name, Edit and Remove buttons.</summary>
        private void DrawEditEntryCard(string name, List<EntryImage> images, int mainImgIdx, string id, float cardW, float cardH, Action onEdit, Action onRemove)
        {
            if (ThemeManager.BeginCard($"ec_{id}", new Vector2(cardW, cardH)))
            {
                float maxImgH = cardH - 24; // leave room for padding
                float maxImgW = 140;
                var mainImg = GetMainImage(images, mainImgIdx);
                if (mainImg != null && mainImg.texture != null && mainImg.texture.Handle != IntPtr.Zero)
                {
                    // Preserve aspect ratio
                    float imgW = mainImg.texture.Width, imgH = mainImg.texture.Height;
                    float scale = Math.Min(maxImgW / Math.Max(imgW, 1), maxImgH / Math.Max(imgH, 1));
                    float drawW = imgW * scale, drawH = imgH * scale;
                    ImGui.Image(mainImg.texture.Handle, new Vector2(drawW, drawH));
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Click to preview");
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                        {
                            ImagePreview.PreviewImage = mainImg.texture;
                            Plugin.plugin.OpenImagePreview();
                        }
                    }
                }
                else
                {
                    var curPos = ImGui.GetCursorScreenPos();
                    ImGui.GetWindowDrawList().AddRectFilled(curPos, new Vector2(curPos.X + maxImgW, curPos.Y + maxImgH),
                        ImGui.ColorConvertFloat4ToU32(ThemeManager.BgDark), 6f);
                    ImGui.Dummy(new Vector2(maxImgW, maxImgH));
                }
                ImGui.SameLine();

                ImGui.BeginGroup();
                ImGui.Text(string.IsNullOrEmpty(name) ? "(untitled)" : (name.Length > 22 ? name.Substring(0, 20) + ".." : name));
                if (images != null && images.Count > 0)
                    ThemeManager.SubtitleText($"{images.Count} image{(images.Count == 1 ? "" : "s")}");
                else
                    ThemeManager.SubtitleText("No images");
                ImGui.Spacing();
                if (ThemeManager.PillButton($"Edit##{id}")) onEdit?.Invoke();
                ImGui.SameLine();
                if (ThemeManager.DangerButton($"X##{id}"))
                {
                    deleteConfirmLabel = string.IsNullOrEmpty(name) ? "this entry" : name;
                    deleteConfirmAction = onRemove;
                    showDeleteConfirm = true;
                }
                ImGui.EndGroup();

                ThemeManager.EndCard();
            }
        }

        /// <summary>Draws delete confirmation popup. Call this once per frame in the parent draw method.</summary>
        private void DrawDeleteConfirmPopup()
        {
            if (!showDeleteConfirm) return;
            ImGui.OpenPopup("##DeleteConfirm");
            if (ImGui.BeginPopupModal("##DeleteConfirm", ref showDeleteConfirm, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar))
            {
                ThemeManager.SectionHeader("Confirm Delete");
                ImGui.Spacing();
                ImGui.Text($"Are you sure you want to delete \"{deleteConfirmLabel}\"?");
                ImGui.Text("This cannot be undone.");
                ImGui.Spacing();
                if (ThemeManager.DangerButton("Delete"))
                {
                    deleteConfirmAction?.Invoke();
                    showDeleteConfirm = false;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ThemeManager.GhostButton("Cancel"))
                {
                    showDeleteConfirm = false;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }

        /// <summary>Draws an uploadable image gallery grid with NSFW/triggering toggles, click-to-preview, and Set as Main.</summary>
        private void DrawImageGalleryEditor(List<EntryImage> images, ref int mainImageIndex, string id, Vector2 windowSize)
        {
            ThemeManager.SectionHeader("Gallery");
            ThemeManager.SubtitleText("Upload images for this entry. Click an image to preview it.");
            ImGui.Spacing();

            float thumbSize = 150, spacing = 10;
            int cols = Math.Max(1, (int)((windowSize.X - 48) / (thumbSize + spacing)));
            int col = 0;

            // Clamp mainImageIndex
            if (mainImageIndex < 0 || mainImageIndex >= images.Count) mainImageIndex = 0;

            for (int i = 0; i < images.Count; i++)
            {
                var img = images[i];
                ImGui.PushID($"img_{id}_{i}");

                if (col > 0) ImGui.SameLine(0, spacing);

                ImGui.BeginGroup();

                // Main image indicator
                bool isMain = (i == mainImageIndex);
                if (isMain)
                {
                    var indicatorPos = ImGui.GetCursorScreenPos();
                    var dl = ImGui.GetWindowDrawList();
                    dl.AddRectFilled(indicatorPos, new Vector2(indicatorPos.X + thumbSize, indicatorPos.Y + 18),
                        ImGui.ColorConvertFloat4ToU32(ThemeManager.Accent), 4f, ImDrawFlags.RoundCornersTop);
                    ImGui.SetCursorScreenPos(new Vector2(indicatorPos.X + 4, indicatorPos.Y + 1));
                    ImGui.TextColored(ThemeManager.Font, "Main Image");
                    ImGui.SetCursorScreenPos(new Vector2(indicatorPos.X, indicatorPos.Y + 18));
                }

                // Thumbnail (click to preview, preserve aspect ratio)
                if (img.texture != null && img.texture.Handle != IntPtr.Zero)
                {
                    float imgW = img.texture.Width, imgH = img.texture.Height;
                    float scale = Math.Min(thumbSize / Math.Max(imgW, 1), thumbSize / Math.Max(imgH, 1));
                    float drawW = imgW * scale, drawH = imgH * scale;
                    // Center within thumb area
                    float padX = (thumbSize - drawW) / 2, padY = (thumbSize - drawH) / 2;
                    if (padX > 0 || padY > 0)
                    {
                        var startPos = ImGui.GetCursorPos();
                        ImGui.SetCursorPos(new Vector2(startPos.X + padX, startPos.Y + padY));
                    }
                    ImGui.Image(img.texture.Handle, new Vector2(drawW, drawH));
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Click to preview full size");
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                        {
                            ImagePreview.PreviewImage = img.texture;
                            Plugin.plugin.OpenImagePreview();
                        }
                    }
                    // Ensure consistent spacing for the grid
                    if (drawH < thumbSize)
                    {
                        float remaining = thumbSize - drawH - (padY > 0 ? padY : 0);
                        if (remaining > 0) ImGui.Dummy(new Vector2(0, remaining));
                    }
                }
                else
                {
                    var curPos = ImGui.GetCursorScreenPos();
                    ImGui.GetWindowDrawList().AddRectFilled(curPos, new Vector2(curPos.X + thumbSize, curPos.Y + thumbSize),
                        ImGui.ColorConvertFloat4ToU32(ThemeManager.BgDark), 4f);
                    ImGui.Dummy(new Vector2(thumbSize, thumbSize));
                }

                // Controls row
                bool nsfw = img.isNSFW, trig = img.isTriggering;
                if (ImGui.Checkbox("NSFW", ref nsfw)) img.isNSFW = nsfw;
                ImGui.SameLine();
                if (ImGui.Checkbox("CW", ref trig)) img.isTriggering = trig;

                // Set as Main / Remove
                if (!isMain)
                {
                    if (ThemeManager.GhostButton($"Set Main##img{i}", new Vector2(thumbSize, 0)))
                        mainImageIndex = i;
                }
                if (ThemeManager.DangerButton($"Remove##img{i}", new Vector2(thumbSize, 0)))
                {
                    images.RemoveAt(i);
                    if (mainImageIndex >= images.Count) mainImageIndex = Math.Max(0, images.Count - 1);
                    if (mainImageIndex > i) mainImageIndex--;
                    i--;
                    ImGui.EndGroup();
                    ImGui.PopID();
                    col++;
                    if (col >= cols) col = 0;
                    continue;
                }
                ImGui.EndGroup();

                col++;
                if (col >= cols) col = 0;
                ImGui.PopID();
            }

            ImGui.Spacing();
            if (ThemeManager.GhostButton($"+ Add Image##{id}"))
            {
                _fileDialogManager.OpenFileDialog("Select Image", ".png,.jpg,.jpeg", (ok, path) =>
                {
                    if (ok && path != null && path.Count > 0)
                    {
                        try
                        {
                            var bytes = System.IO.File.ReadAllBytes(path[0]);
                            var newImg = new EntryImage { imageBytes = bytes, sortOrder = images.Count };
                            images.Add(newImg);
                            Task.Run(async () =>
                            {
                                try { newImg.texture = await Plugin.TextureProvider.CreateFromImageAsync(bytes); }
                                catch { }
                            });
                        }
                        catch { }
                    }
                }, 1, null, false);
            }
        }

        private static int FindTimezoneIndex(string tz)
        {
            for (int i = 0; i < TimezoneNames.Length; i++)
                if (string.Equals(TimezoneNames[i], tz, StringComparison.OrdinalIgnoreCase)) return i;
            return 0; // default EST
        }

        private void DrawScheduleEditor()
        {
            for (int i = 0; i < editSchedules.Count; i++)
            {
                var sched = editSchedules[i];
                ImGui.PushID($"sched_{i}");

                // Day selector
                int dayIdx = sched.dayOfWeek;
                ImGui.SetNextItemWidth(110);
                if (ImGui.Combo("##Day", ref dayIdx, DayNames, DayNames.Length)) sched.dayOfWeek = dayIdx;

                // Start time (12h format)
                To12Hour(sched.startTime, out int sh, out int sm, out int sAmPm);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(36); if (ImGui.InputInt("##SH", ref sh, 0)) sh = Math.Clamp(sh, 1, 12);
                ImGui.SameLine(); ImGui.Text(":"); ImGui.SameLine();
                ImGui.SetNextItemWidth(36); if (ImGui.InputInt("##SM", ref sm, 0)) sm = Math.Clamp(sm, 0, 59);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(52);
                ImGui.Combo("##SAP", ref sAmPm, AmPmNames, AmPmNames.Length);
                sched.startTime = From12Hour(sh, sm, sAmPm);

                // "to" label
                ImGui.SameLine(); ImGui.Text("to"); ImGui.SameLine();

                // End time (12h format)
                To12Hour(sched.endTime, out int eh, out int em, out int eAmPm);
                ImGui.SetNextItemWidth(36); if (ImGui.InputInt("##EH", ref eh, 0)) eh = Math.Clamp(eh, 1, 12);
                ImGui.SameLine(); ImGui.Text(":"); ImGui.SameLine();
                ImGui.SetNextItemWidth(36); if (ImGui.InputInt("##EM", ref em, 0)) em = Math.Clamp(em, 0, 59);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(52);
                ImGui.Combo("##EAP", ref eAmPm, AmPmNames, AmPmNames.Length);
                sched.endTime = From12Hour(eh, em, eAmPm);

                // Timezone selector
                ImGui.SameLine();
                int tzIdx = FindTimezoneIndex(sched.timezone);
                ImGui.SetNextItemWidth(70);
                if (ImGui.Combo("##TZ", ref tzIdx, TimezoneNames, TimezoneNames.Length))
                    sched.timezone = TimezoneNames[tzIdx];

                // Remove button
                ImGui.SameLine();
                if (ThemeManager.DangerButton($"X##rs{i}")) { editSchedules.RemoveAt(i); i--; }

                ImGui.PopID();
            }
            if (ThemeManager.GhostButton("+ Add Time Slot"))
                editSchedules.Add(new ListingSchedule
                {
                    isRecurring = true,
                    dayOfWeek = 0,
                    startTime = TimeSpan.FromHours(12), // 12:00 PM
                    endTime = TimeSpan.FromHours(12),   // 12:00 PM
                    timezone = "EST"
                });
        }

        private void DrawPublishControls(bool isEdit)
        {
            if (isEdit)
            {
                // Save button preserves current publish state
                if (ThemeManager.PillButton("Save Changes"))
                    SaveVenue(currentListing?.isActive ?? false);
                ImGui.SameLine();
                // Publish/Unpublish toggles the publish state only (also saves)
                if (currentListing != null && currentListing.isActive)
                {
                    if (ThemeManager.GhostButton("Unpublish"))
                    {
                        currentListing.isActive = false;
                        SaveVenue(false);
                    }
                }
                else
                {
                    if (ThemeManager.PillButton("Publish"))
                    {
                        if (currentListing != null) currentListing.isActive = true;
                        SaveVenue(true);
                    }
                }
                ImGui.SameLine();
                using (ImRaii.Disabled(!Plugin.CtrlPressed()))
                {
                    if (ThemeManager.DangerButton("Delete"))
                    {
                        DataSender.DeleteListing(editListingId);
                        fetchedMyVenues = false;
                        venueSubView = 1;
                    }
                }
            }
            else
            {
                if (ThemeManager.GhostButton("Save as Draft")) CreateVenue(false);
                ImGui.SameLine();
                if (ThemeManager.PillButton("Publish")) CreateVenue(true);
            }
        }

        private void CreateVenue(bool publish)
        {
            if (string.IsNullOrEmpty(venueName)) { errorMessage = "Please enter a venue name."; return; }
            errorMessage = string.Empty;
            pendingPublish = publish; // Store so OnListingCreated can publish after creation
            string schedulesJson = BuildSchedulesJson();
            var character = Plugin.character;
            int profileId = ProfileWindow.CurrentProfile?.id ?? -1;
            DataSender.CreateListing(character, profileId, 4, venueName, venueTagline, 0, venueWorld, venueDatacenter, venueDistrict, venueWard, venuePlot, venueNSFW, venueContact, venueTags, venueDiscord, venueWebsite, createBannerBytes, createLogoBytes, schedulesJson);
            fetchedMyVenues = false;
            fetchedPublicVenues = false;
        }

        private void SaveVenue(bool publish)
        {
            if (string.IsNullOrEmpty(venueName)) { errorMessage = "Please enter a venue name."; return; }
            errorMessage = string.Empty;
            var character = Plugin.character;
            DataSender.UpdateListing(character, editListingId, venueName, venueTagline, venueDescription, 0, venueWorld, venueDistrict, venueWard, venuePlot, venueNSFW, publish, venueDiscord, venueWebsite, venueContact, venueTags, venueBookingEnabled);
            // Always send these so removals/clears are saved (server deletes + re-inserts)
            DataSender.UpdateListingSchedule(editListingId, editSchedules);
            DataSender.UpdateListingMenu(editListingId, editMenuItems);
            DataSender.SaveBookableEntries(editListingId, editBookables);
            DataSender.SaveStaffEntries(editListingId, editStaff);
            if (createBannerBytes != null && createBannerBytes.Length > 0) DataSender.UploadListingImage(character, editListingId, 0, createBannerBytes);
            if (createLogoBytes != null && createLogoBytes.Length > 0) DataSender.UploadListingImage(character, editListingId, 1, createLogoBytes);
            fetchedMyVenues = false;
            fetchedPublicVenues = false;
        }

        private string BuildSchedulesJson()
        {
            if (editSchedules.Count == 0) return "[]";
            var parts = new List<string>();
            foreach (var s in editSchedules)
                parts.Add($"{{\"isRecurring\":true,\"dayOfWeek\":{s.dayOfWeek},\"startMinutes\":{(int)s.startTime.TotalMinutes},\"endMinutes\":{(int)s.endTime.TotalMinutes},\"timezone\":\"{s.timezone}\"}}");
            return "[" + string.Join(",", parts) + "]";
        }

        #endregion

        #region Venue Detail

        private void DrawVenueDetail(Vector2 windowSize)
        {
            if (ThemeManager.GhostButton("< Back")) { venueSubView = 0; venueDetailTab = 0; return; }
            if (currentListing == null || isDetailLoading)
            {
                ImGui.Spacing(); ImGui.Spacing();
                float progress = detailTotalItems > 0 ? (float)detailLoadedItems / detailTotalItems : 0f;
                string label = detailTotalItems > 0
                    ? $"{detailLoadingStep} ({detailLoadedItems}/{detailTotalItems})"
                    : (string.IsNullOrEmpty(detailLoadingStep) ? "Loading venue..." : detailLoadingStep);
                ThemeManager.StyledProgressBar(progress, new Vector2(windowSize.X - 32, 22), label, null);
                return;
            }

            var venue = currentListing;

            // Banner + Logo header (always visible)
            float bannerH = 140;
            if (venue.banner != null && venue.banner.Handle != IntPtr.Zero)
                DrawBannerImageFit(venue.banner, windowSize.X - 32, bannerH);
            else
                ImGui.Dummy(new Vector2(windowSize.X - 32, bannerH));

            float logoSize = 72;
            var afterBanner = ImGui.GetCursorScreenPos();
            var dl = ImGui.GetWindowDrawList();
            if (venue.logo != null && venue.logo.Handle != IntPtr.Zero)
            {
                float lx = afterBanner.X + 16, ly = afterBanner.Y - logoSize / 2;
                dl.AddCircleFilled(new Vector2(lx + logoSize / 2, ly + logoSize / 2), logoSize / 2 + 3, ImGui.ColorConvertFloat4ToU32(ThemeManager.Background));
                dl.AddImageRounded(venue.logo.Handle, new Vector2(lx, ly), new Vector2(lx + logoSize, ly + logoSize), Vector2.Zero, Vector2.One, 0xFFFFFFFF, logoSize / 2);
            }
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + logoSize / 2 + 8);
            ThemeManager.SectionHeader(venue.name);
            if (!string.IsNullOrEmpty(venue.tagline)) ThemeManager.SubtitleText(venue.tagline);
            ImGui.Spacing();

            // Tab bar
            if (ImGui.BeginTabBar("##VenueDetailTabs"))
            {
                if (ImGui.BeginTabItem("Overview")) { venueDetailTab = 0; ImGui.EndTabItem(); }
                if (venue.menuItems != null && venue.menuItems.Count > 0)
                    if (ImGui.BeginTabItem("Menu")) { venueDetailTab = 1; ImGui.EndTabItem(); }
                if (venue.staff != null && venue.staff.Count > 0)
                    if (ImGui.BeginTabItem("Staff")) { venueDetailTab = 2; ImGui.EndTabItem(); }
                if (venue.bookingEnabled && venue.bookableEntries != null && venue.bookableEntries.Count > 0)
                    if (ImGui.BeginTabItem("Reservations")) { venueDetailTab = 3; ImGui.EndTabItem(); }
                ImGui.EndTabBar();
            }

            // Tab content (scrollable)
            float contentH = ImGui.GetContentRegionAvail().Y;
            if (ImGui.BeginChild("##VenueDetailContent", new Vector2(windowSize.X - 16, contentH), false))
            {
                switch (venueDetailTab)
                {
                    case 0: DrawVenueOverviewTab(venue, windowSize); break;
                    case 1: DrawVenueMenuTab(venue, windowSize); break;
                    case 2: DrawVenueStaffTab(venue, windowSize); break;
                    case 3: DrawVenueReservationsTab(venue, windowSize); break;
                }
                ImGui.EndChild();
            }

            // View More popup
            DrawViewMorePopup(windowSize);

            // Booking popup
            DrawBookingPopup(venue);
        }

        private void DrawVenueOverviewTab(Listing venue, Vector2 windowSize)
        {
            // Location
            string loc = string.Empty;
            if (!string.IsNullOrEmpty(venue.district)) loc += venue.district;
            if (venue.ward > 0) loc += $" Ward {venue.ward}";
            if (venue.plot > 0) loc += $", Plot {venue.plot}";
            if (!string.IsNullOrEmpty(venue.world)) loc += $" - {venue.world}";
            if (!string.IsNullOrEmpty(loc)) { ThemeManager.AccentText(loc); ImGui.Spacing(); }

            if (!string.IsNullOrEmpty(venue.description))
            {
                ImGui.PushTextWrapPos(windowSize.X - 32);
                ImGui.TextWrapped(venue.description);
                ImGui.PopTextWrapPos();
                ImGui.Spacing();
            }

            // Schedule
            if (venue.schedules != null && venue.schedules.Count > 0)
            {
                ThemeManager.GradientSeparator(); ImGui.Spacing();
                ThemeManager.SectionHeader("Opening Hours"); ImGui.Spacing();
                foreach (var sched in venue.schedules)
                {
                    if (sched.isRecurring)
                    {
                        string day = sched.dayOfWeek >= 0 && sched.dayOfWeek < DayNames.Length ? DayNames[sched.dayOfWeek] : "?";
                        int sh = (int)sched.startTime.TotalMinutes / 60, sm = (int)sched.startTime.TotalMinutes % 60;
                        int eh = (int)sched.endTime.TotalMinutes / 60, em = (int)sched.endTime.TotalMinutes % 60;
                        string startAP = sh >= 12 ? "PM" : "AM"; int sh12 = sh > 12 ? sh - 12 : (sh == 0 ? 12 : sh);
                        string endAP = eh >= 12 ? "PM" : "AM"; int eh12 = eh > 12 ? eh - 12 : (eh == 0 ? 12 : eh);
                        string tz = !string.IsNullOrEmpty(sched.timezone) ? $" {sched.timezone}" : "";
                        ImGui.Text($"  {day}: {sh12}:{sm:D2} {startAP} - {eh12}:{em:D2} {endAP}{tz}");
                    }
                }
                ImGui.Spacing();
            }

            // Links
            if (!string.IsNullOrEmpty(venue.discordLink) || !string.IsNullOrEmpty(venue.websiteLink) || !string.IsNullOrEmpty(venue.contactInfo))
            {
                ThemeManager.GradientSeparator(); ImGui.Spacing();
                ThemeManager.SectionHeader("Links");
                if (!string.IsNullOrEmpty(venue.discordLink)) ImGui.Text($"  Discord: {venue.discordLink}");
                if (!string.IsNullOrEmpty(venue.websiteLink)) ImGui.Text($"  Website: {venue.websiteLink}");
                if (!string.IsNullOrEmpty(venue.contactInfo)) ImGui.Text($"  Contact: {venue.contactInfo}");
            }
        }

        private void DrawVenueMenuTab(Listing venue, Vector2 windowSize)
        {
            float cardW = 340, cardH = 160, spacing = 10;
            int cols = Math.Max(1, (int)((windowSize.X - 32) / (cardW + spacing)));
            string lastCat = string.Empty;
            int col = 0;

            foreach (var item in venue.menuItems.OrderBy(m => m.category).ThenBy(m => m.sortOrder))
            {
                if (item.category != lastCat)
                {
                    if (!string.IsNullOrEmpty(lastCat)) { ImGui.Spacing(); ImGui.Spacing(); }
                    ThemeManager.AccentText(item.category);
                    ImGui.Spacing();
                    lastCat = item.category;
                    col = 0;
                }

                if (col > 0) ImGui.SameLine(0, spacing);
                DrawEntryCard(item.itemName, TruncateText(item.description, 60), item.price, item.isOOCPrice,
                    item.images, $"menu_{item.id}", cardW, cardH,
                    () => OpenViewMore(item.itemName, item.category, item.description, item.price, item.isOOCPrice, null, item.images));
                col++;
                if (col >= cols) col = 0;
            }
        }

        private void DrawVenueStaffTab(Listing venue, Vector2 windowSize)
        {
            float cardW = 340, cardH = 160, spacing = 10;
            int cols = Math.Max(1, (int)((windowSize.X - 32) / (cardW + spacing)));
            int col = 0;

            foreach (var s in venue.staff.OrderBy(st => st.sortOrder))
            {
                if (col > 0) ImGui.SameLine(0, spacing);
                DrawEntryCard(s.characterName, s.role, null, false,
                    s.images, $"staff_{s.id}", cardW, cardH,
                    () => OpenViewMore(s.characterName, s.role, s.description, null, false, s.customFields, s.images));
                col++;
                if (col >= cols) col = 0;
            }
        }

        private void DrawVenueReservationsTab(Listing venue, Vector2 windowSize)
        {
            float resCardW = 340, resCardH = 130, resSpacing = 10;
            int resCols = Math.Max(1, (int)((windowSize.X - 32) / (resCardW + resSpacing)));
            int resCol = 0;

            foreach (var entry in venue.bookableEntries)
            {
                if (!entry.isActive) continue;
                if (resCol > 0) ImGui.SameLine(0, resSpacing);

                if (ThemeManager.BeginCard($"res_{entry.id}", new Vector2(resCardW, resCardH)))
                {
                    // Main image (use mainImageIndex or first image)
                    float imgSize = 80;
                    int mainIdx = entry.mainImageIndex;
                    bool hasImg = entry.images != null && entry.images.Count > mainIdx && mainIdx >= 0
                        && entry.images[mainIdx].texture != null;
                    if (hasImg)
                    {
                        var tex = entry.images[mainIdx].texture;
                        float aspect = (float)tex.Width / tex.Height;
                        float drawW = aspect >= 1 ? imgSize : imgSize * aspect;
                        float drawH = aspect >= 1 ? imgSize / aspect : imgSize;
                        ImGui.Image(tex.Handle, new Vector2(drawW, drawH));
                    }
                    else
                        ImGui.Dummy(new Vector2(imgSize, imgSize));
                    ImGui.SameLine();

                    ImGui.BeginGroup();
                    ThemeManager.AccentText(entry.name);
                    if (!string.IsNullOrEmpty(entry.price))
                        ThemeManager.SubtitleText($"{entry.price}{(entry.isOOCPrice ? " (OOC)" : " (IC)")}");

                    // Slot status
                    if (entry.bookedSlots >= entry.maxSlots)
                    {
                        ThemeManager.Badge("Fully Booked", ThemeManager.Error);
                    }
                    else
                    {
                        ImGui.Text($"Slots: {entry.bookedSlots}/{entry.maxSlots}");
                    }

                    // Buttons row
                    if (ThemeManager.GhostButton($"View Info##{entry.id}"))
                    {
                        viewMoreTitle = entry.name;
                        viewMoreSubtitle = "";
                        viewMoreDescription = entry.description ?? "";
                        viewMoreImages = entry.images ?? new List<EntryImage>();
                        viewMorePrice = entry.price ?? "";
                        viewMoreIsOOCPrice = entry.isOOCPrice;
                        viewMoreCustomFields = new List<field>();
                        viewMoreAvailableTimes = entry.availableTimes ?? new List<ListingSchedule>();
                        showViewMorePopup = true;
                    }
                    ImGui.SameLine();
                    if (entry.bookedSlots < entry.maxSlots)
                    {
                        if (ThemeManager.PillButton($"Book##{entry.id}"))
                        {
                            showBookingPopup = true;
                            bookingEntryId = entry.id;
                            bookingEntryName = entry.name;
                            bookingNotes = "";
                            bookingDateYear = DateTime.Now.Year;
                            bookingDateMonth = DateTime.Now.Month;
                            bookingDateDay = DateTime.Now.Day;
                            bookingTimeHour = 12;
                            bookingTimeMinute = 0;
                            bookingTimePM = false;
                            bookingTimezoneIndex = 0;
                            selectedTimeSlotIndex = -1;
                        }
                    }
                    ImGui.EndGroup();
                    ThemeManager.EndCard();
                }

                resCol++;
                if (resCol >= resCols) resCol = 0;
            }
        }

        /// <summary>Draws a compact entry card with thumbnail, name, short description, and View More button.</summary>
        private void DrawEntryCard(string name, string shortDesc, string price, bool isOOC,
            List<EntryImage> images, string id, float cardW, float cardH, Action onViewMore)
        {
            if (ThemeManager.BeginCard($"card_{id}", new Vector2(cardW, cardH)))
            {
                float maxImgW = 130, maxImgH = cardH - 24;
                // Thumbnail (first image, aspect-preserved)
                if (images != null && images.Count > 0 && images[0].texture != null)
                {
                    var img = images[0];
                    if ((img.isNSFW || img.isTriggering) && !img.revealed)
                    {
                        var curPos = ImGui.GetCursorScreenPos();
                        var idl = ImGui.GetWindowDrawList();
                        idl.AddRectFilled(curPos, new Vector2(curPos.X + maxImgW, curPos.Y + maxImgH),
                            ImGui.ColorConvertFloat4ToU32(ThemeManager.BgDark), 6f);
                        ImGui.SetCursorScreenPos(new Vector2(curPos.X + 8, curPos.Y + maxImgH / 2 - 8));
                        ThemeManager.SubtitleText(img.isNSFW ? "18+ Content" : "Content Warning");
                        ImGui.SetCursorScreenPos(new Vector2(curPos.X + maxImgW, curPos.Y));
                        ImGui.Dummy(new Vector2(0, maxImgH));
                    }
                    else
                    {
                        float imgW = img.texture.Width, imgH = img.texture.Height;
                        float scale = Math.Min(maxImgW / Math.Max(imgW, 1), maxImgH / Math.Max(imgH, 1));
                        float drawW = imgW * scale, drawH = imgH * scale;
                        ImGui.Image(img.texture.Handle, new Vector2(drawW, drawH));
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Click to preview");
                            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                            {
                                ImagePreview.PreviewImage = img.texture;
                                Plugin.plugin.OpenImagePreview();
                            }
                        }
                    }
                }
                else
                {
                    var curPos = ImGui.GetCursorScreenPos();
                    ImGui.GetWindowDrawList().AddRectFilled(curPos, new Vector2(curPos.X + maxImgW, curPos.Y + maxImgH),
                        ImGui.ColorConvertFloat4ToU32(ThemeManager.BgDark), 6f);
                    ImGui.Dummy(new Vector2(maxImgW, maxImgH));
                }
                ImGui.SameLine();

                ImGui.BeginGroup();
                ImGui.Text(name ?? "");
                if (!string.IsNullOrEmpty(shortDesc)) ThemeManager.SubtitleText(TruncateText(shortDesc, 80));
                if (!string.IsNullOrEmpty(price)) ThemeManager.SubtitleText($"{price}{(isOOC ? " (OOC)" : "")}");
                ImGui.Spacing();
                if (ThemeManager.GhostButton($"View More##{id}"))
                    onViewMore?.Invoke();
                ImGui.EndGroup();

                ThemeManager.EndCard();
            }
        }

        private void OpenViewMore(string title, string subtitle, string description, string price, bool isOOC,
            List<field> customFields, List<EntryImage> images)
        {
            viewMoreTitle = title ?? string.Empty;
            viewMoreSubtitle = subtitle ?? string.Empty;
            viewMoreDescription = description ?? string.Empty;
            viewMorePrice = price ?? string.Empty;
            viewMoreIsOOCPrice = isOOC;
            viewMoreCustomFields = customFields ?? new List<field>();
            viewMoreImages = images ?? new List<EntryImage>();
            showViewMorePopup = true;
        }

        private void DrawViewMorePopup(Vector2 windowSize)
        {
            if (!showViewMorePopup) return;

            ImGui.SetNextWindowSize(new Vector2(Math.Min(windowSize.X - 40, 500), Math.Min(windowSize.Y - 40, 500)), ImGuiCond.Appearing);
            if (ImGui.Begin($"{viewMoreTitle}##ViewMore", ref showViewMorePopup, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking))
            {
                ThemeManager.SectionHeader(viewMoreTitle);
                if (!string.IsNullOrEmpty(viewMoreSubtitle)) ThemeManager.AccentText(viewMoreSubtitle);
                ImGui.Spacing();

                if (!string.IsNullOrEmpty(viewMorePrice))
                {
                    ImGui.Text($"Price: {viewMorePrice}{(viewMoreIsOOCPrice ? " (OOC)" : " (IC)")}");
                    ImGui.Spacing();
                }

                if (!string.IsNullOrEmpty(viewMoreDescription))
                {
                    ThemeManager.GradientSeparator(); ImGui.Spacing();
                    ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
                    ImGui.TextWrapped(viewMoreDescription);
                    ImGui.PopTextWrapPos();
                    ImGui.Spacing();
                }

                // Custom fields
                if (viewMoreCustomFields.Count > 0)
                {
                    ThemeManager.GradientSeparator(); ImGui.Spacing();
                    foreach (var f in viewMoreCustomFields)
                    {
                        ThemeManager.AccentText(f.name ?? "");
                        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
                        ImGui.TextWrapped(f.description ?? "");
                        ImGui.PopTextWrapPos();
                        ImGui.Spacing();
                    }
                }

                // Available times (for services)
                if (viewMoreAvailableTimes.Count > 0)
                {
                    ThemeManager.GradientSeparator(); ImGui.Spacing();
                    ThemeManager.SectionHeader("Available Times"); ImGui.Spacing();
                    foreach (var slot in viewMoreAvailableTimes)
                    {
                        if (!slot.isRecurring) continue;
                        string day = slot.dayOfWeek >= 0 && slot.dayOfWeek < DayNames.Length ? DayNames[slot.dayOfWeek] : "?";
                        To12Hour(slot.startTime, out int sh, out int sm, out int sAP);
                        To12Hour(slot.endTime, out int eh, out int em, out int eAP);
                        string tz = !string.IsNullOrEmpty(slot.timezone) ? $" {slot.timezone}" : "";
                        ImGui.Text($"  {day}: {sh}:{sm:D2} {(sAP == 1 ? "PM" : "AM")} - {eh}:{em:D2} {(eAP == 1 ? "PM" : "AM")}{tz}");
                    }
                    ImGui.Spacing();
                }

                // Image gallery
                if (viewMoreImages.Count > 0)
                {
                    ThemeManager.GradientSeparator(); ImGui.Spacing();
                    ThemeManager.SectionHeader("Gallery"); ImGui.Spacing();
                    float thumbSize = 100;
                    int imgCols = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (thumbSize + 8)));
                    int imgCol = 0;
                    for (int i = 0; i < viewMoreImages.Count; i++)
                    {
                        var img = viewMoreImages[i];
                        if (img.texture == null) continue;

                        if (imgCol > 0) ImGui.SameLine(0, 8);

                        bool hidden = (img.isNSFW || img.isTriggering) && !img.revealed;
                        if (hidden)
                        {
                            var curPos = ImGui.GetCursorScreenPos();
                            var idl = ImGui.GetWindowDrawList();
                            idl.AddRectFilled(curPos, new Vector2(curPos.X + thumbSize, curPos.Y + thumbSize),
                                ImGui.ColorConvertFloat4ToU32(ThemeManager.BgDark), 6f);
                            float textY = curPos.Y + thumbSize / 2 - 16;
                            ImGui.SetCursorScreenPos(new Vector2(curPos.X + 8, textY));
                            ThemeManager.SubtitleText(img.isNSFW ? "18+ Content" : "Content Warning");
                            ImGui.SetCursorScreenPos(new Vector2(curPos.X + 8, textY + 20));
                            if (ThemeManager.GhostButton($"Reveal##img{i}", new Vector2(thumbSize - 16, 0)))
                                img.revealed = true;
                            ImGui.SetCursorScreenPos(new Vector2(curPos.X + thumbSize, curPos.Y));
                            ImGui.Dummy(new Vector2(0, thumbSize));
                        }
                        else
                        {
                            ImGui.Image(img.texture.Handle, new Vector2(thumbSize, thumbSize));
                            if (ImGui.IsItemClicked())
                            {
                                // Open full image preview
                                Plugin.plugin.OpenImagePreview();
                                ImagePreview.PreviewImage = img.texture;
                            }
                        }
                        if (!string.IsNullOrEmpty(img.caption) && ImGui.IsItemHovered())
                            ImGui.SetTooltip(img.caption);

                        imgCol++;
                        if (imgCol >= imgCols) imgCol = 0;
                    }
                }

                ImGui.End();
            }
        }

        // Booking popup state
        private int selectedTimeSlotIndex = -1;

        private void DrawBookingPopup(Listing venue)
        {
            if (!showBookingPopup) return;

            // Find the selected bookable entry
            BookableEntry selectedEntry = null;
            if (venue.bookableEntries != null)
                selectedEntry = venue.bookableEntries.FirstOrDefault(e => e.id == bookingEntryId);

            ImGui.SetNextWindowSize(new Vector2(400, 420));
            if (ImGui.Begin($"Book: {bookingEntryName}##BookingPopup", ref showBookingPopup, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking))
            {
                ThemeManager.SectionHeader($"Book: {bookingEntryName}");
                if (selectedEntry != null && !string.IsNullOrEmpty(selectedEntry.price))
                {
                    ThemeManager.SubtitleText($"Price: {selectedEntry.price}{(selectedEntry.isOOCPrice ? " (OOC)" : " (IC)")}");
                }
                ImGui.Spacing();

                // Show available time slots from the service
                bool hasTimeSlots = selectedEntry?.availableTimes != null && selectedEntry.availableTimes.Count > 0;

                if (hasTimeSlots)
                {
                    ThemeManager.SubtitleText("Select an available time slot:");
                    ImGui.Spacing();

                    for (int i = 0; i < selectedEntry.availableTimes.Count; i++)
                    {
                        var slot = selectedEntry.availableTimes[i];
                        if (!slot.isRecurring) continue;

                        string day = slot.dayOfWeek >= 0 && slot.dayOfWeek < DayNames.Length ? DayNames[slot.dayOfWeek] : "?";
                        To12Hour(slot.startTime, out int sh, out int sm, out int sAP);
                        To12Hour(slot.endTime, out int eh, out int em, out int eAP);
                        string tz = !string.IsNullOrEmpty(slot.timezone) ? $" {slot.timezone}" : "";
                        string slotLabel = $"{day}: {sh}:{sm:D2} {(sAP == 1 ? "PM" : "AM")} - {eh}:{em:D2} {(eAP == 1 ? "PM" : "AM")}{tz}";

                        bool selected = selectedTimeSlotIndex == i;
                        if (selected)
                            ImGui.PushStyleColor(ImGuiCol.Button, ImGui.ColorConvertFloat4ToU32(ThemeManager.AccentMuted));

                        if (ImGui.Button($"{slotLabel}##slot{i}", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
                            selectedTimeSlotIndex = i;

                        if (selected)
                            ImGui.PopStyleColor();
                    }

                    if (selectedTimeSlotIndex < 0)
                    {
                        ImGui.Spacing();
                        ThemeManager.SubtitleText("Please select a time slot above.");
                    }
                }
                else
                {
                    // No preset slots — allow custom date/time
                    ThemeManager.SubtitleText("Select a date and time:");
                    ImGui.Spacing();

                    ImGui.Text("Date:");
                    ImGui.SetNextItemWidth(60); ImGui.InputInt("##bYear", ref bookingDateYear, 0);
                    ImGui.SameLine(); ImGui.Text("/");
                    ImGui.SameLine(); ImGui.SetNextItemWidth(40); ImGui.InputInt("##bMonth", ref bookingDateMonth, 0);
                    bookingDateMonth = Math.Clamp(bookingDateMonth, 1, 12);
                    ImGui.SameLine(); ImGui.Text("/");
                    ImGui.SameLine(); ImGui.SetNextItemWidth(40); ImGui.InputInt("##bDay", ref bookingDateDay, 0);
                    bookingDateDay = Math.Clamp(bookingDateDay, 1, 31);

                    ImGui.Text("Time:");
                    ImGui.SetNextItemWidth(40); ImGui.InputInt("##bHour", ref bookingTimeHour, 0);
                    bookingTimeHour = Math.Clamp(bookingTimeHour, 1, 12);
                    ImGui.SameLine(); ImGui.Text(":");
                    ImGui.SameLine(); ImGui.SetNextItemWidth(40); ImGui.InputInt("##bMin", ref bookingTimeMinute, 0);
                    bookingTimeMinute = Math.Clamp(bookingTimeMinute, 0, 59);
                    ImGui.SameLine();
                    int ampmIdx = bookingTimePM ? 1 : 0;
                    ImGui.SetNextItemWidth(50); if (ImGui.Combo("##bAmPm", ref ampmIdx, AmPmNames, 2)) bookingTimePM = ampmIdx == 1;
                }

                ImGui.Spacing();
                ImGui.Text("Timezone:");
                ImGui.SetNextItemWidth(120); ImGui.Combo("##bTZ", ref bookingTimezoneIndex, BookingTimezones, BookingTimezones.Length);

                ImGui.Text("Notes (optional):");
                ImGui.InputTextMultiline("##bNotes", ref bookingNotes, 500, new Vector2(ImGui.GetContentRegionAvail().X, 60));

                ImGui.Spacing();

                // Only enable confirm if a slot is selected (when slots exist) or custom time is set
                bool canConfirm = !hasTimeSlots || selectedTimeSlotIndex >= 0;

                using (ImRaii.Disabled(!canConfirm))
                {
                    if (ThemeManager.PillButton("Confirm Booking"))
                    {
                        try
                        {
                            DateTime date;
                            TimeSpan time;

                            if (hasTimeSlots && selectedTimeSlotIndex >= 0)
                            {
                                var slot = selectedEntry.availableTimes[selectedTimeSlotIndex];
                                // Use the next occurrence of the selected day
                                var today = DateTime.Now;
                                int daysUntil = ((slot.dayOfWeek - (int)today.DayOfWeek) + 7) % 7;
                                if (daysUntil == 0 && today.TimeOfDay > slot.startTime) daysUntil = 7;
                                date = today.Date.AddDays(daysUntil);
                                time = slot.startTime;
                            }
                            else
                            {
                                date = new DateTime(bookingDateYear, bookingDateMonth, bookingDateDay);
                                int hour24 = bookingTimePM
                                    ? (bookingTimeHour == 12 ? 12 : bookingTimeHour + 12)
                                    : (bookingTimeHour == 12 ? 0 : bookingTimeHour);
                                time = new TimeSpan(hour24, bookingTimeMinute, 0);
                            }

                            string tz = BookingTimezones[bookingTimezoneIndex];
                            _ = DataSender.SendBookingRequest(venue.id, bookingEntryId, date, time, tz, bookingNotes);
                            showBookingPopup = false;
                            selectedTimeSlotIndex = -1;
                        }
                        catch { }
                    }
                }
                ImGui.SameLine();
                if (ThemeManager.GhostButton("Cancel"))
                {
                    showBookingPopup = false;
                    selectedTimeSlotIndex = -1;
                }
                ImGui.End();
            }
        }

        private static string TruncateText(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (text.Length <= maxLen) return text;
            return text.Substring(0, maxLen - 2) + "..";
        }

        private void DrawMyBookings(Vector2 windowSize)
        {
            // Fetch bookings and own venues on first visit
            if (!fetchedMyBookings)
            {
                fetchedMyBookings = true;
                _ = DataSender.FetchMyBookings();
                // Also fetch own venues so we can check for incoming bookings
                if (!fetchedMyVenues)
                {
                    fetchedMyVenues = true;
                    _ = DataSender.FetchMyListings();
                }
            }

            ThemeManager.SectionHeader("My Bookings");
            ThemeManager.SubtitleText("Your upcoming reservations at venues.");
            ImGui.Spacing();

            if (myBookings.Count == 0)
            {
                ThemeManager.SubtitleText("You don't have any bookings yet.");
                ThemeManager.SubtitleText("Visit a venue and click 'Book Reservation' to make one.");
            }
            else
            {
                foreach (var booking in myBookings)
                {
                    DrawBookingCard(booking, windowSize, false);
                }
            }

            // Incoming Requests section (for venue owners)
            ImGui.Spacing(); ImGui.Spacing();
            ThemeManager.GradientSeparator(); ImGui.Spacing();
            ThemeManager.SectionHeader("Incoming Booking Requests");
            ThemeManager.SubtitleText("Requests from visitors to book your venues.");
            ImGui.Spacing();

            // Fetch incoming once we know our venues (myListings populated by FetchMyListings callback)
            if (!fetchedIncomingBookings && myListings.Count > 0)
            {
                fetchedIncomingBookings = true;
                _ = DataSender.FetchIncomingBookings(0); // 0 = fetch ALL incoming for this user
            }

            if (incomingBookingRequests.Count == 0)
            {
                ThemeManager.SubtitleText("No incoming booking requests.");
            }
            else
            {
                foreach (var booking in incomingBookingRequests)
                {
                    DrawBookingCard(booking, windowSize, true);
                }
            }
        }

        private void DrawBookingCard(BookingRequest booking, Vector2 windowSize, bool showActions)
        {
            ImGui.BeginGroup();
            var cardPos = ImGui.GetCursorScreenPos();
            var dl = ImGui.GetWindowDrawList();
            float cardW = windowSize.X - 32;
            float cardH = showActions ? 110 : 80;

            // Card background
            dl.AddRectFilled(cardPos, new Vector2(cardPos.X + cardW, cardPos.Y + cardH),
                ImGui.ColorConvertFloat4ToU32(ThemeManager.Lighten(ThemeManager.Background, 0.03f)), 6f);
            dl.AddRect(cardPos, new Vector2(cardPos.X + cardW, cardPos.Y + cardH),
                ImGui.ColorConvertFloat4ToU32(ThemeManager.Border), 6f);

            // Status indicator
            Vector4 statusColor = booking.status switch
            {
                0 => ThemeManager.Warning,  // Pending
                1 => ThemeManager.Success,  // Accepted
                2 => ThemeManager.Error,    // Declined
                _ => ThemeManager.FontMuted
            };
            string statusText = booking.status switch
            {
                0 => "Pending",
                1 => "Confirmed",
                2 => "Declined",
                3 => "Declined (Repeat)",
                _ => "Unknown"
            };

            ImGui.SetCursorScreenPos(new Vector2(cardPos.X + 12, cardPos.Y + 8));
            ThemeManager.AccentText(showActions ? $"{booking.requesterName} @ {booking.requesterWorld}" : booking.venueName);
            ImGui.SetCursorScreenPos(new Vector2(cardPos.X + 12, cardPos.Y + 28));
            ImGui.Text(showActions ? $"{booking.venueName} - {booking.bookableEntryName}" : booking.bookableEntryName);
            ImGui.SetCursorScreenPos(new Vector2(cardPos.X + 12, cardPos.Y + 48));
            ThemeManager.SubtitleText($"{booking.requestedDate:MMM dd, yyyy} at {(int)booking.requestedTime.TotalHours:D2}:{booking.requestedTime.Minutes:D2} {booking.timezone}");
            if (!string.IsNullOrEmpty(booking.notes))
            {
                ImGui.SetCursorScreenPos(new Vector2(cardPos.X + 12, cardPos.Y + 64));
                ThemeManager.SubtitleText($"Notes: {booking.notes}");
            }

            // Status badge
            ImGui.SetCursorScreenPos(new Vector2(cardPos.X + cardW - 100, cardPos.Y + 12));
            ThemeManager.Badge(statusText, statusColor);

            // Action buttons for incoming requests
            if (showActions && booking.status == 0)
            {
                ImGui.SetCursorScreenPos(new Vector2(cardPos.X + 12, cardPos.Y + cardH - 32));
                if (ThemeManager.PillButton($"Accept##{booking.id}"))
                {
                    _ = DataSender.RespondToBooking(booking.id, 1);
                }
                ImGui.SameLine();
                if (ThemeManager.GhostButton($"Decline##{booking.id}"))
                {
                    _ = DataSender.RespondToBooking(booking.id, 2);
                }
                ImGui.SameLine();
                if (ThemeManager.DangerButton($"Decline & Block##{booking.id}"))
                {
                    _ = DataSender.RespondToBooking(booking.id, 3);
                }
            }

            ImGui.SetCursorScreenPos(new Vector2(cardPos.X, cardPos.Y + cardH));
            ImGui.Dummy(new Vector2(cardW, 0));
            ImGui.EndGroup();
            ImGui.Spacing();
        }

        #endregion

        public static void OnListingCreated(int listingId)
        {
            if (listingId > 0)
            {
                // Save menu items
                if (editMenuItems.Count > 0) DataSender.UpdateListingMenu(listingId, editMenuItems);
                // Save schedules
                if (editSchedules.Count > 0) DataSender.UpdateListingSchedule(listingId, editSchedules);
                // Publish if requested (creation defaults to draft, so we update isActive)
                if (pendingPublish)
                {
                    var character = Plugin.character;
                    DataSender.UpdateListing(character, listingId, venueName, venueTagline, venueDescription, 0,
                        venueWorld, venueDistrict, venueWard, venuePlot, venueNSFW, true,
                        venueDiscord, venueWebsite, venueContact, venueTags);
                    pendingPublish = false;
                }
            }
            fetchedMyVenues = false;
            fetchedPublicVenues = false;
            venueSubView = 1;
        }

        public override void OnClose() { base.OnClose(); }
        public void Dispose() { }
    }
}
