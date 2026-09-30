using AbsoluteRP;
using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI.Pages;
using AbsoluteRP.Windows.Ect;
using AbsoluteRP.Windows.Listings;
using AbsoluteRP.Windows;
using AbsoluteRP.Windows.Moderator;
using AbsoluteRP.Windows.Profiles;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using AbsoluteRP.Windows.Social.Views;
using AbsoluteRP.Windows.Social.Views.Groups;
using AbsoluteRP.Windows.Social.Views.SubViews;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Common.Math;
using Serilog;
using System.Linq;
using System.Xml.Linq;
using Networking;

namespace AbsoluteRP.Network
{
    // Listings system packets. Split out of DataReceiver.
    internal class Listings_DR
    {
        public static void ReceiveListingsByType(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int listingCount = buffer.ReadInt();
                    for (int i = 0; i < listingCount; i++)
                    {
                        string name = buffer.ReadString();
                        string description = buffer.ReadString();
                        string rules = buffer.ReadString();
                        int category = buffer.ReadInt();
                        int type = buffer.ReadInt();
                        int focus = buffer.ReadInt();
                        int setting = buffer.ReadInt();
                        string bannerURL = buffer.ReadString();
                        int inclusion = buffer.ReadInt();
                        string startDate = buffer.ReadString();
                        string endDate = buffer.ReadString();
                        //  IDalamudTextureWrap banner = Imaging.DownloadImage(bannerURL, i);
                        //  Listing listing = new Listing(name, description, rules, category, type, focus, setting, banner, inclusion, startDate, endDate);
                        //   SocialWindow.listings.Add(listing);
                    }
                    Profiles_DR.ListingsLoadStatus = 1;
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveNoTargetOOCInfo message: {ex}");
            }
        }

        internal static async void ReceivePersonalListings(byte[] data)
        {
            try
            {
                SocialWindow.isSearchLoading = true;
                SocialWindow.searchLoadedCount = 0;

                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int listingCount = buffer.ReadInt();
                    SocialWindow.searchTotalCount = listingCount;
                    SocialWindow.listings.Clear();

                    // Read all listing data from buffer first (can't await inside buffer read loop)
                    var entries = new List<(int profileID, string name, byte[] avatarBytes, bool ARR, bool HW, bool SB, bool SHB, bool EW, bool DT, bool nsfw, bool triggering, float colX, float colY, float colZ, float colW)>();
                    for (int i = 0; i < listingCount; i++)
                    {
                        int profileID = buffer.ReadInt();
                        string name = buffer.ReadString();
                        int avatarLen = buffer.ReadInt();
                        byte[] avatarBytes = buffer.ReadBytes(avatarLen);
                        bool spoilerARR = buffer.ReadBool();
                        bool spoilerHW = buffer.ReadBool();
                        bool spoilerSB = buffer.ReadBool();
                        bool spoilerSHB = buffer.ReadBool();
                        bool spoilerEW = buffer.ReadBool();
                        bool spoilerDT = buffer.ReadBool();
                        bool nsfw = buffer.ReadBool();
                        bool triggering = buffer.ReadBool();
                        float colX = buffer.ReadFloat();
                        float colY = buffer.ReadFloat();
                        float colZ = buffer.ReadFloat();
                        float colW = buffer.ReadFloat();
                        entries.Add((profileID, name, avatarBytes, spoilerARR, spoilerHW, spoilerSB, spoilerSHB, spoilerEW, spoilerDT, nsfw, triggering, colX, colY, colZ, colW));
                    }

                    // Now process entries with async texture loading
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var e = entries[i];
                        IDalamudTextureWrap avatar = null;
                        if (e.avatarBytes != null && e.avatarBytes.Length > 0)
                        {
                            try
                            {
                                avatar = await Plugin.TextureProvider.CreateFromImageAsync(e.avatarBytes);
                            }
                            catch (Exception ex)
                            {
                                Plugin.PluginLog.Debug($"Invalid avatar image for profile {e.profileID}: {ex.Message}");
                            }
                        }

                        // Use a fallback avatar instead of skipping profiles without one
                        if (avatar == null)
                        {
                            try
                            {
                                avatar = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                            }
                            catch { }
                        }
                        if (avatar == null)
                        {
                            SocialWindow.searchLoadedCount++;
                            continue;
                        }

                        SocialWindow.listings.Add(
                            new Listing
                            {
                                type = 6,
                                id = e.profileID,
                                name = e.name,
                                avatar = avatar,
                                ARR = e.ARR, HW = e.HW, SB = e.SB,
                                SHB = e.SHB, EW = e.EW, DT = e.DT,
                                color = new Vector4(e.colX, e.colY, e.colZ, e.colW),
                            });
                        SocialWindow.searchLoadedCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceivePersonalListings message: {ex}");
            }
            finally
            {
                SocialWindow.isSearchLoading = false;
            }
        }

        // Listing System handlers. Each handler below processes a response from the listings subsystem (venues, events, services, etc.) and updates the ListingsWindow state.

        // Confirmation that a new listing was created successfully
        public static void HandleListingCreated(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet ID
                    bool success = buffer.ReadBool();
                    int listingId = buffer.ReadInt();
                    ListingsWindow.listingCreated = success;
                    ListingsWindow.lastCreatedListingId = listingId;
                    Plugin.PluginLog.Info($"[HandleListingCreated] success={success}, listingId={listingId}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"[HandleListingCreated] Error: {ex.Message}");
            }
        }

        public static void HandleListingUpdated(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet ID
                    bool success = buffer.ReadBool();
                    Plugin.PluginLog.Info($"[HandleListingUpdated] success={success}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"[HandleListingUpdated] Error: {ex.Message}");
            }
        }

        public static void HandleListingDeleted(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet ID
                    bool success = buffer.ReadBool();
                    Plugin.PluginLog.Info($"[HandleListingDeleted] success={success}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"[HandleListingDeleted] Error: {ex.Message}");
            }
        }

        public static async void HandleListingsList(byte[] data)
        {
            try
            {
                ListingsWindow.isLoading = true;
                ListingsWindow.listingsLoadedCount = 0;
                ListingsWindow.listings.Clear();
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet ID
                    int count = DataReceiver.SafeReadCount(buffer);
                    ListingsWindow.listingsTotalCount = count;
                    Plugin.PluginLog.Info($"[HandleListingsList] Receiving {count} listings");

                    for (int i = 0; i < count; i++)
                    {
                        var listing = new Listing();
                        listing.id = buffer.ReadInt();
                        listing.ownerId = buffer.ReadInt();
                        listing.type = buffer.ReadInt();
                        listing.name = buffer.ReadString();
                        listing.tagline = buffer.ReadString();
                        listing.category = buffer.ReadInt();
                        listing.world = buffer.ReadString();
                        listing.district = buffer.ReadString();
                        listing.ward = buffer.ReadInt();
                        listing.plot = buffer.ReadInt();
                        listing.isNSFW = buffer.ReadBool();
                        listing.viewCount = buffer.ReadInt();

                        // Banner image
                        {
                            byte[] bannerBytes = DataReceiver.SafeReadBytes(buffer);
                            if (bannerBytes != null)
                            {
                                try { listing.banner = await Plugin.TextureProvider.CreateFromImageAsync(bannerBytes); }
                                catch (Exception imgEx) { Plugin.PluginLog.Debug($"[HandleListingsList] Banner load failed: {imgEx.Message}"); }
                            }
                        }

                        // Logo image
                        {
                            byte[] logoBytes = DataReceiver.SafeReadBytes(buffer);
                            if (logoBytes != null)
                            {
                                try { listing.logo = await Plugin.TextureProvider.CreateFromImageAsync(logoBytes); }
                                catch (Exception imgEx) { Plugin.PluginLog.Debug($"[HandleListingsList] Logo load failed: {imgEx.Message}"); }
                            }
                        }

                        // Schedules
                        int scheduleCount = DataReceiver.SafeReadCount(buffer);
                        for (int s = 0; s < scheduleCount; s++)
                        {
                            var schedule = new ListingSchedule();
                            schedule.isRecurring = buffer.ReadBool();
                            if (schedule.isRecurring)
                            {
                                schedule.dayOfWeek = buffer.ReadInt();
                                int startMinutes = buffer.ReadInt();
                                int endMinutes = buffer.ReadInt();
                                schedule.startTime = TimeSpan.FromMinutes(startMinutes);
                                schedule.endTime = TimeSpan.FromMinutes(endMinutes);
                            }
                            else
                            {
                                long specificDateTicks = buffer.ReadLong();
                                long specificEndDateTicks = buffer.ReadLong();
                                schedule.specificDate = new DateTime(specificDateTicks);
                                schedule.specificEndDate = new DateTime(specificEndDateTicks);
                            }
                            listing.schedules.Add(schedule);
                        }

                        ListingsWindow.listings.Add(listing);
                        ListingsWindow.listingsLoadedCount++;
                    }
                }
                ListingsWindow.isLoading = false;
                Plugin.PluginLog.Info($"[HandleListingsList] Finished loading {ListingsWindow.listings.Count} listings");
            }
            catch (Exception ex)
            {
                ListingsWindow.isLoading = false;
                Plugin.PluginLog.Error($"[HandleListingsList] Error: {ex.Message}");
            }
        }

        public static async void HandleListingDetail(byte[] data)
        {
            try
            {
                ListingsWindow.isLoading = true;
                ListingsWindow.isDetailLoading = true;
                ListingsWindow.detailLoadingStep = "Loading venue info...";
                ListingsWindow.detailLoadedItems = 0;
                ListingsWindow.detailTotalItems = 0;
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet ID

                    var listing = new Listing();
                    listing.id = buffer.ReadInt();
                    listing.ownerId = buffer.ReadInt();
                    listing.type = buffer.ReadInt();
                    listing.name = buffer.ReadString();
                    listing.tagline = buffer.ReadString();
                    listing.description = buffer.ReadString();
                    listing.category = buffer.ReadInt();
                    listing.world = buffer.ReadString();
                    listing.datacenter = buffer.ReadString();
                    listing.district = buffer.ReadString();
                    listing.ward = buffer.ReadInt();
                    listing.plot = buffer.ReadInt();
                    listing.isNSFW = buffer.ReadBool();
                    listing.isActive = buffer.ReadBool();
                    listing.viewCount = buffer.ReadInt();
                    listing.bookmarkCount = buffer.ReadInt();
                    listing.discordLink = buffer.ReadString();
                    listing.websiteLink = buffer.ReadString();
                    listing.contactInfo = buffer.ReadString();
                    listing.tags = buffer.ReadString();
                    listing.isBookmarked = buffer.ReadBool();
                    listing.bookingEnabled = buffer.ReadBool();

                    // Banner image
                    ListingsWindow.detailLoadingStep = "Loading banner...";
                    {
                        byte[] bannerBytes = DataReceiver.SafeReadBytes(buffer);
                        if (bannerBytes != null)
                        {
                            try { listing.banner = await Plugin.TextureProvider.CreateFromImageAsync(bannerBytes); }
                            catch (Exception imgEx) { Plugin.PluginLog.Debug($"[HandleListingDetail] Banner load failed: {imgEx.Message}"); }
                        }
                    }

                    // Logo image
                    ListingsWindow.detailLoadingStep = "Loading logo...";
                    {
                        byte[] logoBytes = DataReceiver.SafeReadBytes(buffer);
                        if (logoBytes != null)
                        {
                            try { listing.logo = await Plugin.TextureProvider.CreateFromImageAsync(logoBytes); }
                            catch (Exception imgEx) { Plugin.PluginLog.Debug($"[HandleListingDetail] Logo load failed: {imgEx.Message}"); }
                        }
                    }

                    // Tabs
                    int tabCount = DataReceiver.SafeReadCount(buffer);
                    for (int t = 0; t < tabCount; t++)
                    {
                        var tab = new ListingTab();
                        tab.id = buffer.ReadInt();
                        tab.tabType = buffer.ReadInt();
                        tab.title = buffer.ReadString();
                        tab.content = buffer.ReadString();
                        tab.sortOrder = buffer.ReadInt();
                        tab.isVisible = buffer.ReadBool();
                        listing.tabs.Add(tab);
                    }

                    // Schedules
                    int scheduleCount = DataReceiver.SafeReadCount(buffer);
                    for (int s = 0; s < scheduleCount; s++)
                    {
                        var schedule = new ListingSchedule();
                        schedule.id = buffer.ReadInt();
                        schedule.isRecurring = buffer.ReadBool();
                        if (schedule.isRecurring)
                        {
                            schedule.dayOfWeek = buffer.ReadInt();
                            int startMinutes = buffer.ReadInt();
                            int endMinutes = buffer.ReadInt();
                            schedule.startTime = TimeSpan.FromMinutes(startMinutes);
                            schedule.endTime = TimeSpan.FromMinutes(endMinutes);
                        }
                        else
                        {
                            long specificDateTicks = buffer.ReadLong();
                            long specificEndDateTicks = buffer.ReadLong();
                            schedule.specificDate = new DateTime(specificDateTicks);
                            schedule.specificEndDate = new DateTime(specificEndDateTicks);
                        }
                        schedule.eventName = buffer.ReadString();
                        schedule.notes = buffer.ReadString();
                        schedule.timezone = buffer.ReadString();
                        listing.schedules.Add(schedule);
                    }

                    // Staff
                    ListingsWindow.detailLoadingStep = "Loading staff...";
                    int staffCount = DataReceiver.SafeReadCount(buffer);
                    ListingsWindow.detailTotalItems += staffCount;
                    for (int st = 0; st < staffCount; st++)
                    {
                        var staff = new StaffMember();
                        staff.id = buffer.ReadInt();
                        staff.userId = buffer.ReadInt();
                        staff.characterName = buffer.ReadString();
                        staff.characterWorld = buffer.ReadString();
                        staff.role = buffer.ReadString();
                        staff.description = buffer.ReadString();
                        staff.sortOrder = buffer.ReadInt();

                        // Custom fields
                        int fieldCount = DataReceiver.SafeReadCount(buffer);
                        for (int f = 0; f < fieldCount; f++)
                        {
                            staff.customFields.Add(new field
                            {
                                name = buffer.ReadString(),
                                description = buffer.ReadString()
                            });
                        }

                        ListingsWindow.detailLoadingStep = $"Loading staff avatar ({st + 1}/{staffCount})...";
                        {
                            byte[] avatarBytes = DataReceiver.SafeReadBytes(buffer);
                            if (avatarBytes != null)
                            {
                                try { staff.avatar = await Plugin.TextureProvider.CreateFromImageAsync(avatarBytes); }
                                catch { }
                            }
                        }
                        // Entry images
                        int staffImgCount = DataReceiver.SafeReadCount(buffer);
                        ListingsWindow.detailTotalItems += staffImgCount;
                        for (int img = 0; img < staffImgCount; img++)
                        {
                            ListingsWindow.detailLoadingStep = $"Loading staff image {img + 1}/{staffImgCount}...";
                            var entryImg = new EntryImage();
                            {
                                byte[] imgBytes = DataReceiver.SafeReadBytes(buffer);
                                if (imgBytes != null)
                                {
                                    entryImg.imageBytes = imgBytes;
                                    try { entryImg.texture = await Plugin.TextureProvider.CreateFromImageAsync(imgBytes); }
                                    catch { }
                                }
                            }
                            entryImg.isNSFW = buffer.ReadBool();
                            entryImg.isTriggering = buffer.ReadBool();
                            entryImg.caption = buffer.ReadString();
                            entryImg.sortOrder = img;
                            staff.images.Add(entryImg);
                            ListingsWindow.detailLoadedItems++;
                        }
                        listing.staff.Add(staff);
                        ListingsWindow.detailLoadedItems++;
                    }

                    // Menu Items
                    ListingsWindow.detailLoadingStep = "Loading menu...";
                    int menuItemCount = DataReceiver.SafeReadCount(buffer);
                    ListingsWindow.detailTotalItems += menuItemCount;
                    for (int m = 0; m < menuItemCount; m++)
                    {
                        var menuItem = new MenuItemData();
                        menuItem.id = buffer.ReadInt();
                        menuItem.category = buffer.ReadString();
                        menuItem.itemName = buffer.ReadString();
                        menuItem.description = buffer.ReadString();
                        menuItem.price = buffer.ReadString();
                        menuItem.isOOCPrice = buffer.ReadBool();
                        menuItem.sortOrder = buffer.ReadInt();
                        // Entry images
                        int menuImgCount = DataReceiver.SafeReadCount(buffer);
                        ListingsWindow.detailTotalItems += menuImgCount;
                        for (int img = 0; img < menuImgCount; img++)
                        {
                            ListingsWindow.detailLoadingStep = $"Loading menu image {img + 1}/{menuImgCount}...";
                            var entryImg = new EntryImage();
                            {
                                byte[] imgBytes = DataReceiver.SafeReadBytes(buffer);
                                if (imgBytes != null)
                                {
                                    entryImg.imageBytes = imgBytes;
                                    try { entryImg.texture = await Plugin.TextureProvider.CreateFromImageAsync(imgBytes); }
                                    catch { }
                                }
                            }
                            entryImg.isNSFW = buffer.ReadBool();
                            entryImg.isTriggering = buffer.ReadBool();
                            entryImg.caption = buffer.ReadString();
                            entryImg.sortOrder = img;
                            menuItem.images.Add(entryImg);
                            ListingsWindow.detailLoadedItems++;
                        }
                        listing.menuItems.Add(menuItem);
                        ListingsWindow.detailLoadedItems++;
                    }

                    // Bookable Entries
                    ListingsWindow.detailLoadingStep = "Loading bookable services...";
                    int bookableCount = DataReceiver.SafeReadCount(buffer);
                    ListingsWindow.detailTotalItems += bookableCount;
                    listing.bookableEntries = new List<BookableEntry>();
                    for (int b = 0; b < bookableCount; b++)
                    {
                        var entry = new BookableEntry();
                        entry.id = buffer.ReadInt();
                        entry.name = buffer.ReadString();
                        entry.description = buffer.ReadString();
                        entry.price = buffer.ReadString();
                        entry.isOOCPrice = buffer.ReadBool();
                        entry.maxSlots = buffer.ReadInt();
                        entry.isActive = buffer.ReadBool();
                        entry.sortOrder = buffer.ReadInt();
                        // Entry images
                        int bookableImgCount = DataReceiver.SafeReadCount(buffer);
                        ListingsWindow.detailTotalItems += bookableImgCount;
                        for (int img = 0; img < bookableImgCount; img++)
                        {
                            ListingsWindow.detailLoadingStep = $"Loading booking image {img + 1}/{bookableImgCount}...";
                            var entryImg = new EntryImage();
                            {
                                byte[] imgBytes = DataReceiver.SafeReadBytes(buffer);
                                if (imgBytes != null)
                                {
                                    entryImg.imageBytes = imgBytes;
                                    try { entryImg.texture = await Plugin.TextureProvider.CreateFromImageAsync(imgBytes); }
                                    catch { }
                                }
                            }
                            entryImg.isNSFW = buffer.ReadBool();
                            entryImg.isTriggering = buffer.ReadBool();
                            entryImg.caption = buffer.ReadString();
                            entryImg.sortOrder = img;
                            entry.images.Add(entryImg);
                            ListingsWindow.detailLoadedItems++;
                        }
                        int timeCount = DataReceiver.SafeReadCount(buffer);
                        for (int t = 0; t < timeCount; t++)
                        {
                            var time = new ListingSchedule();
                            time.isRecurring = buffer.ReadBool();
                            if (time.isRecurring)
                            {
                                time.dayOfWeek = buffer.ReadInt();
                                time.startTime = TimeSpan.FromMinutes(buffer.ReadInt());
                                time.endTime = TimeSpan.FromMinutes(buffer.ReadInt());
                            }
                            else
                            {
                                long specificDateTicks = buffer.ReadLong();
                                long specificEndDateTicks = buffer.ReadLong();
                                time.specificDate = new DateTime(specificDateTicks);
                                time.specificEndDate = new DateTime(specificEndDateTicks);
                            }
                            entry.availableTimes.Add(time);
                        }
                        listing.bookableEntries.Add(entry);
                        ListingsWindow.detailLoadedItems++;
                    }

                    // Services
                    int serviceCount = DataReceiver.SafeReadCount(buffer);
                    for (int sv = 0; sv < serviceCount; sv++)
                    {
                        var service = new ServiceData();
                        service.id = buffer.ReadInt();
                        service.serviceName = buffer.ReadString();
                        service.description = buffer.ReadString();
                        service.sortOrder = buffer.ReadInt();
                        listing.services.Add(service);
                    }

                    // Settings
                    int settingsCount = DataReceiver.SafeReadCount(buffer);
                    for (int se = 0; se < settingsCount; se++)
                    {
                        string key = buffer.ReadString();
                        string value = buffer.ReadString();
                        listing.settings[key] = value;
                    }

                    ListingsWindow.currentListing = listing;

                    // If we're in edit mode for this listing, sync the detailed data back into the edit fields
                    if (ListingsWindow.editListingId == listing.id)
                    {
                        ListingsWindow.venueBookingEnabled = listing.bookingEnabled;
                        ListingsWindow.venueName = listing.name ?? string.Empty;
                        ListingsWindow.venueTagline = listing.tagline ?? string.Empty;
                        ListingsWindow.venueDescription = listing.description ?? string.Empty;
                        ListingsWindow.venueWorld = listing.world ?? string.Empty;
                        ListingsWindow.venueDatacenter = listing.datacenter ?? string.Empty;
                        ListingsWindow.venueDistrict = listing.district ?? string.Empty;
                        ListingsWindow.venueWard = listing.ward;
                        ListingsWindow.venuePlot = listing.plot;
                        ListingsWindow.venueNSFW = listing.isNSFW;
                        ListingsWindow.venueContact = listing.contactInfo ?? string.Empty;
                        ListingsWindow.venueTags = listing.tags ?? string.Empty;
                        ListingsWindow.venueDiscord = listing.discordLink ?? string.Empty;
                        ListingsWindow.venueWebsite = listing.websiteLink ?? string.Empty;
                        ListingsWindow.createBannerTexture = listing.banner;
                        ListingsWindow.createLogoTexture = listing.logo;
                        ListingsWindow.createBannerBytes = null;
                        ListingsWindow.createLogoBytes = null;
                        if (listing.schedules != null) ListingsWindow.editSchedules = new List<ListingSchedule>(listing.schedules);
                        if (listing.menuItems != null) ListingsWindow.editMenuItems = listing.menuItems.Select(m => new MenuItemData
                        {
                            id = m.id, category = m.category, itemName = m.itemName,
                            description = m.description, price = m.price,
                            isOOCPrice = m.isOOCPrice, sortOrder = m.sortOrder,
                            images = m.images?.Select(img => new EntryImage
                            {
                                id = img.id, texture = img.texture, imageBytes = img.imageBytes,
                                isNSFW = img.isNSFW, isTriggering = img.isTriggering,
                                caption = img.caption, sortOrder = img.sortOrder
                            }).ToList() ?? new List<EntryImage>()
                        }).ToList();
                        // Load bookable entries into edit form
                        if (listing.bookableEntries != null)
                            ListingsWindow.editBookables = listing.bookableEntries.Select(b => new BookableEntry
                            {
                                id = b.id, listingId = b.listingId, name = b.name,
                                description = b.description, price = b.price,
                                isOOCPrice = b.isOOCPrice, maxSlots = b.maxSlots,
                                isActive = b.isActive, sortOrder = b.sortOrder,
                                availableTimes = b.availableTimes != null ? new List<ListingSchedule>(b.availableTimes) : new List<ListingSchedule>(),
                                images = b.images?.Select(img => new EntryImage
                                {
                                    id = img.id, texture = img.texture, imageBytes = img.imageBytes,
                                    isNSFW = img.isNSFW, isTriggering = img.isTriggering,
                                    caption = img.caption, sortOrder = img.sortOrder
                                }).ToList() ?? new List<EntryImage>()
                            }).ToList();
                        else
                            ListingsWindow.editBookables = new List<BookableEntry>();
                        // Load staff into edit form (convert StaffMember -> StaffEntry)
                        if (listing.staff != null)
                            ListingsWindow.editStaff = listing.staff.Select(s => new StaffEntry
                            {
                                id = s.id, name = s.characterName ?? string.Empty,
                                role = s.role ?? string.Empty,
                                description = s.description ?? string.Empty,
                                sortOrder = s.sortOrder,
                                customFields = s.customFields?.Select(f => new field
                                    { name = f.name, description = f.description }).ToList()
                                    ?? new List<field>(),
                                images = s.images?.Select(img => new EntryImage
                                {
                                    id = img.id, texture = img.texture, imageBytes = img.imageBytes,
                                    isNSFW = img.isNSFW, isTriggering = img.isTriggering,
                                    caption = img.caption, sortOrder = img.sortOrder
                                }).ToList() ?? new List<EntryImage>(),
                            }).ToList();
                        else
                            ListingsWindow.editStaff = new List<StaffEntry>();
                    }

                    ListingsWindow.detailLoadingStep = "Done!";
                    Plugin.PluginLog.Info($"[HandleListingDetail] Loaded listing detail: id={listing.id}, name='{listing.name}', bookingEnabled={listing.bookingEnabled}");
                }
                ListingsWindow.isLoading = false;
                ListingsWindow.isDetailLoading = false;
            }
            catch (Exception ex)
            {
                ListingsWindow.isLoading = false;
                ListingsWindow.isDetailLoading = false;
                Plugin.PluginLog.Error($"[HandleListingDetail] Error: {ex.Message}");
            }
        }

        public static void HandleMyListings(byte[] data)
        {
            try
            {
                ListingsWindow.myListings.Clear();
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet ID
                    int count = buffer.ReadInt();
                    Plugin.PluginLog.Info($"[HandleMyListings] Receiving {count} listings");

                    for (int i = 0; i < count; i++)
                    {
                        var listing = new Listing();
                        listing.id = buffer.ReadInt();
                        listing.type = buffer.ReadInt();
                        listing.name = buffer.ReadString();
                        listing.tagline = buffer.ReadString();
                        listing.isActive = buffer.ReadBool();
                        listing.viewCount = buffer.ReadInt();
                        long createdAtTicks = buffer.ReadLong();
                        listing.createdAt = new DateTime(createdAtTicks);
                        ListingsWindow.myListings.Add(listing);
                    }
                }
                Plugin.PluginLog.Info($"[HandleMyListings] Finished loading {ListingsWindow.myListings.Count} listings");
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"[HandleMyListings] Error: {ex.Message}");
            }
        }

        public static void HandleBookmarkResult(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet ID
                    bool success = buffer.ReadBool();
                    bool bookmarked = buffer.ReadBool();
                    Plugin.PluginLog.Info($"[HandleBookmarkResult] success={success}, bookmarked={bookmarked}");

                    if (success && ListingsWindow.currentListing != null)
                    {
                        ListingsWindow.currentListing.isBookmarked = bookmarked;
                        if (bookmarked)
                            ListingsWindow.currentListing.bookmarkCount++;
                        else
                            ListingsWindow.currentListing.bookmarkCount = Math.Max(0, ListingsWindow.currentListing.bookmarkCount - 1);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"[HandleBookmarkResult] Error: {ex.Message}");
            }
        }

        public static void HandleMenuUpdated(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet ID
                    bool success = buffer.ReadBool();
                    Plugin.PluginLog.Info($"[HandleMenuUpdated] success={success}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"[HandleMenuUpdated] Error: {ex.Message}");
            }
        }

        public static void HandleScheduleUpdated(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet ID
                    bool success = buffer.ReadBool();
                    Plugin.PluginLog.Info($"[HandleScheduleUpdated] success={success}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"[HandleScheduleUpdated] Error: {ex.Message}");
            }
        }

        public static void HandleImageUploaded(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet ID
                    bool success = buffer.ReadBool();
                    Plugin.PluginLog.Info($"[HandleImageUploaded] success={success}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"[HandleImageUploaded] Error: {ex.Message}");
            }
        }

        public static void HandleListingError(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet ID
                    string message = buffer.ReadString();
                    ListingsWindow.errorMessage = message;
                    Plugin.PluginLog.Warning($"[HandleListingError] {message}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"[HandleListingError] Error: {ex.Message}");
            }
        }
    }
}
