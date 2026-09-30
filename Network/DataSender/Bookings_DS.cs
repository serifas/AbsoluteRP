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
    // Booking system requests. Split out of DataSender.
    internal class Bookings_DS
    {
        internal static async Task SendBookingRequest(int listingId, int bookableEntryId, DateTime requestedDate, TimeSpan requestedTime, string timezone, string notes)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSendBookingRequest);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteInt(listingId);
                        buffer.WriteInt(bookableEntryId);
                        buffer.WriteLong(requestedDate.Ticks);
                        buffer.WriteInt((int)requestedTime.TotalMinutes);
                        buffer.WriteString(timezone);
                        buffer.WriteString(notes);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendBookingRequest: " + ex.ToString());
                }
            }
        }

        internal static async Task FetchMyBookings()
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CFetchMyBookings);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchMyBookings: " + ex.ToString());
                }
            }
        }

        internal static async Task RespondToBooking(int bookingId, int status)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CRespondToBooking);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteInt(bookingId);
                        buffer.WriteInt(status);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in RespondToBooking: " + ex.ToString());
                }
            }
        }

        internal static async Task FetchIncomingBookings(int listingId)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CFetchIncomingBookings);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteInt(listingId);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchIncomingBookings: " + ex.ToString());
                }
            }
        }

        internal static async Task SaveBookableEntries(int listingId, List<BookableEntry> entries)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSaveBookableEntries);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteInt(listingId);
                        buffer.WriteInt(entries.Count);
                        foreach (var entry in entries)
                        {
                            buffer.WriteString(entry.name);
                            buffer.WriteString(entry.description);
                            buffer.WriteString(entry.price);
                            buffer.WriteBool(entry.isOOCPrice);
                            buffer.WriteInt(entry.maxSlots);
                            buffer.WriteBool(entry.isActive);
                            buffer.WriteInt(entry.sortOrder);
                            var bookableImages = entry.images ?? new List<EntryImage>();
                            buffer.WriteInt(bookableImages.Count);
                            foreach (var img in bookableImages)
                            {
                                buffer.WriteInt(img.imageBytes?.Length ?? 0);
                                if (img.imageBytes != null && img.imageBytes.Length > 0)
                                    buffer.WriteBytes(img.imageBytes);
                                buffer.WriteBool(img.isNSFW);
                                buffer.WriteBool(img.isTriggering);
                                buffer.WriteString(img.caption ?? string.Empty);
                            }
                            buffer.WriteInt(entry.availableTimes.Count);
                            foreach (var time in entry.availableTimes)
                            {
                                buffer.WriteBool(time.isRecurring);
                                if (time.isRecurring)
                                {
                                    buffer.WriteInt(time.dayOfWeek);
                                    buffer.WriteInt((int)time.startTime.TotalMinutes);
                                    buffer.WriteInt((int)time.endTime.TotalMinutes);
                                }
                                else
                                {
                                    buffer.WriteLong(time.specificDate.HasValue ? time.specificDate.Value.Ticks : 0);
                                    buffer.WriteLong(time.specificEndDate.HasValue ? time.specificEndDate.Value.Ticks : 0);
                                }
                            }
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SaveBookableEntries: " + ex.ToString());
                }
            }
        }

        internal static async Task SaveStaffEntries(int listingId, List<StaffEntry> entries)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSaveStaffEntries);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteInt(listingId);
                        buffer.WriteInt(entries.Count);
                        foreach (var entry in entries)
                        {
                            buffer.WriteString(entry.name ?? string.Empty);
                            buffer.WriteString(entry.role ?? string.Empty);
                            buffer.WriteString(entry.description ?? string.Empty);
                            buffer.WriteInt(entry.sortOrder);
                            buffer.WriteInt(entry.customFields?.Count ?? 0);
                            if (entry.customFields != null)
                            {
                                foreach (var f in entry.customFields)
                                {
                                    buffer.WriteString(f.name ?? string.Empty);
                                    buffer.WriteString(f.description ?? string.Empty);
                                }
                            }
                            // Multi-image gallery (replaces old single image)
                            var staffImages = entry.images ?? new List<EntryImage>();
                            // If entry has old-style imageBytes but no images list, convert
                            if (staffImages.Count == 0 && entry.imageBytes != null && entry.imageBytes.Length > 0)
                                staffImages.Add(new EntryImage { imageBytes = entry.imageBytes, sortOrder = 0 });
                            buffer.WriteInt(staffImages.Count);
                            foreach (var img in staffImages)
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
                    Plugin.PluginLog.Debug("Debug in SaveStaffEntries: " + ex.ToString());
                }
            }
        }
    }
}
