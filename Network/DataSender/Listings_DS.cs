using AbsoluteRP;
using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI.Pages;
using AbsoluteRP.Windows.Moderator;
using AbsoluteRP.Windows.Profiles;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Social.Views;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Numerics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Lumina.Data.Parsing.Layer.LayerCommon;
using Networking;

namespace AbsoluteRP.Network
{
    // Listings system requests. Split out of DataSender.
    internal class Listings_DS
    {
        internal static async void SubmitListing(Character character, byte[] bannerBytes, string listingName, string listingDescription, string listingRules, int inclusion, int currentCategory, int currentType, int currentFocus, int currentSetting, bool nsfw, string triggers,
                                         int selectedStartYear, int selectedStartMonth, int selectedStartDay, int selectedStartHour, int selectedStartMinute, int selectedStartAmPm, int selectedStartTimezone,
                                         int selectedEndYear, int selectedEndMonth, int selectedEndDay, int selectedEndHour, int selectedEndMinute, int selectedEndAmPm, int selectedEndTimezone)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SCreateListing);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(bannerBytes.Length);
                        buffer.WriteBytes(bannerBytes);
                        buffer.WriteString(listingName);
                        buffer.WriteString(listingDescription);
                        buffer.WriteString(listingRules);
                        buffer.WriteInt(inclusion);
                        buffer.WriteInt(currentCategory);
                        buffer.WriteInt(currentType);
                        buffer.WriteInt(currentFocus);
                        buffer.WriteInt(currentSetting);
                        buffer.WriteBool(nsfw);
                        buffer.WriteString(triggers);
                        buffer.WriteInt(selectedStartYear);
                        buffer.WriteInt(selectedStartMonth);
                        buffer.WriteInt(selectedStartDay);
                        buffer.WriteInt(selectedStartHour);
                        buffer.WriteInt(selectedStartMinute);
                        buffer.WriteInt(selectedStartAmPm);
                        buffer.WriteInt(selectedStartTimezone);
                        buffer.WriteInt(selectedEndYear);
                        buffer.WriteInt(selectedEndMonth);
                        buffer.WriteInt(selectedEndDay);
                        buffer.WriteInt(selectedEndHour);
                        buffer.WriteInt(selectedEndMinute);
                        buffer.WriteInt(selectedEndAmPm);
                        buffer.WriteInt(selectedEndTimezone);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SubmitListing: " + ex.ToString());
                }
            }
        }

        // Listing System Methods. These methods handle creating, updating, and managing venue/event/service listings.

        internal static async Task CreateListing(Character character, int profileId, int listingType, string name, string tagline, int category, string world, string datacenter, string district, int ward, int plot, bool isNSFW, string contactInfo, string tags, string discordLink, string websiteLink, byte[] bannerImage, byte[] logoImage, string schedulesJson)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CCreateListing);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(profileId);
                        buffer.WriteInt(listingType);
                        buffer.WriteString(name);
                        buffer.WriteString(tagline);
                        buffer.WriteInt(category);
                        buffer.WriteString(world);
                        buffer.WriteString(datacenter);
                        buffer.WriteString(district);
                        buffer.WriteInt(ward);
                        buffer.WriteInt(plot);
                        buffer.WriteBool(isNSFW);
                        buffer.WriteString(contactInfo);
                        buffer.WriteString(tags);
                        buffer.WriteString(discordLink);
                        buffer.WriteString(websiteLink);
                        buffer.WriteInt(bannerImage != null ? bannerImage.Length : 0);
                        if (bannerImage != null && bannerImage.Length > 0)
                            buffer.WriteBytes(bannerImage);
                        buffer.WriteInt(logoImage != null ? logoImage.Length : 0);
                        if (logoImage != null && logoImage.Length > 0)
                            buffer.WriteBytes(logoImage);
                        buffer.WriteString(schedulesJson);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in CreateListing: " + ex.ToString());
                }
            }
        }

        internal static async Task UpdateListing(Character character, int listingId, string name, string tagline, string description, int category, string world, string district, int ward, int plot, bool isNSFW, bool isActive, string discordLink, string websiteLink, string contactInfo, string tags, bool bookingEnabled = false)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CUpdateListing);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(listingId);
                        buffer.WriteString(name);
                        buffer.WriteString(tagline);
                        buffer.WriteString(description);
                        buffer.WriteInt(category);
                        buffer.WriteString(world);
                        buffer.WriteString(district);
                        buffer.WriteInt(ward);
                        buffer.WriteInt(plot);
                        buffer.WriteBool(isNSFW);
                        buffer.WriteBool(isActive);
                        buffer.WriteString(discordLink);
                        buffer.WriteString(websiteLink);
                        buffer.WriteString(contactInfo);
                        buffer.WriteString(tags);
                        buffer.WriteBool(bookingEnabled);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in UpdateListing: " + ex.ToString());
                }
            }
        }

        internal static async Task DeleteListing(int listingId)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CDeleteListing);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteInt(listingId);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in DeleteListing: " + ex.ToString());
                }
            }
        }

        internal static async Task FetchListings(int listingType, int category, string world, string searchQuery, bool includeNSFW, bool openNow, int page, int pageSize, int sortBy)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CFetchListings);
                        buffer.WriteInt(listingType);
                        buffer.WriteInt(category);
                        buffer.WriteString(world);
                        buffer.WriteString(searchQuery);
                        buffer.WriteBool(includeNSFW);
                        buffer.WriteBool(openNow);
                        buffer.WriteInt(page);
                        buffer.WriteInt(pageSize);
                        buffer.WriteInt(sortBy);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchListings: " + ex.ToString());
                }
            }
        }

        internal static async Task FetchListingDetail(int listingId)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CFetchListingDetail);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteInt(listingId);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchListingDetail: " + ex.ToString());
                }
            }
        }

        internal static async Task FetchMyListings()
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CFetchMyListings);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchMyListings: " + ex.ToString());
                }
            }
        }

        internal static async Task BookmarkListing(int listingId, bool bookmark)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CBookmarkListing);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteInt(listingId);
                        buffer.WriteBool(bookmark);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in BookmarkListing: " + ex.ToString());
                }
            }
        }

        internal static async Task UpdateListingMenu(int listingId, List<MenuItemData> menuItems)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CUpdateListingMenu);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteInt(listingId);
                        buffer.WriteInt(menuItems.Count);
                        foreach (var item in menuItems)
                        {
                            buffer.WriteString(item.category ?? string.Empty);
                            buffer.WriteString(item.itemName ?? string.Empty);
                            buffer.WriteString(item.description ?? string.Empty);
                            buffer.WriteString(item.price ?? string.Empty);
                            buffer.WriteBool(item.isOOCPrice);
                            buffer.WriteInt(item.sortOrder);
                            var menuImages = item.images ?? new List<EntryImage>();
                            buffer.WriteInt(menuImages.Count);
                            foreach (var img in menuImages)
                            {
                                buffer.WriteInt(img.imageBytes?.Length ?? 0);
                                if (img.imageBytes != null && img.imageBytes.Length > 0)
                                    buffer.WriteBytes(img.imageBytes);
                                buffer.WriteBool(img.isNSFW);
                                buffer.WriteBool(img.isTriggering);
                                buffer.WriteString(img.caption ?? string.Empty);
                            }
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in UpdateListingMenu: " + ex.ToString());
                }
            }
        }

        internal static async Task UpdateListingSchedule(int listingId, List<ListingSchedule> schedules)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CUpdateListingSchedule);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteInt(listingId);
                        buffer.WriteInt(schedules.Count);
                        foreach (var schedule in schedules)
                        {
                            buffer.WriteBool(schedule.isRecurring);
                            if (schedule.isRecurring)
                            {
                                buffer.WriteInt(schedule.dayOfWeek);
                                buffer.WriteInt((int)schedule.startTime.TotalMinutes);
                                buffer.WriteInt((int)schedule.endTime.TotalMinutes);
                            }
                            else
                            {
                                buffer.WriteLong(schedule.specificDate.HasValue ? schedule.specificDate.Value.Ticks : 0);
                                buffer.WriteLong(schedule.specificEndDate.HasValue ? schedule.specificEndDate.Value.Ticks : 0);
                            }
                            buffer.WriteString(schedule.eventName);
                            buffer.WriteString(schedule.notes);
                            buffer.WriteString(schedule.timezone);
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in UpdateListingSchedule: " + ex.ToString());
                }
            }
        }

        internal static async Task UploadListingImage(Character character, int listingId, int imageType, byte[] imageBytes)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CUploadListingImage);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(listingId);
                        buffer.WriteInt(imageType);
                        buffer.WriteInt(imageBytes.Length);
                        buffer.WriteBytes(imageBytes);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in UploadListingImage: " + ex.ToString());
                }
            }
        }

        internal static async void RequestOwnedListings(Character character, int profileIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchProfileItems);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendProfileItems: " + ex.ToString());
                }
            }
        }

        internal static async void RequestPersonals(Character character, string searchWorld, int index, int pageSize, string searchProfile, int selectedCategory)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendPersonalsRequest);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteString(searchWorld);
                        buffer.WriteString(searchProfile);
                        buffer.WriteInt(selectedCategory);
                        buffer.WriteInt(index);
                        buffer.WriteInt(pageSize);
                        Plugin.PluginLog.Debug("Selected Category = " + selectedCategory);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendProfileItems: " + ex.ToString());
                }
            }
        }
    }
}
