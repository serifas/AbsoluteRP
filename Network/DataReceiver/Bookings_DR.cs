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
    // Booking system packets. Split out of DataReceiver.
    internal class Bookings_DR
    {
        // Booking System handlers

        // Result of a booking request (accepted, declined, etc.)
        public static void HandleBookingRequestResult(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet ID
                    bool success = buffer.ReadBool();
                    string message = buffer.ReadString();
                    if (!success)
                    {
                        ListingsWindow.errorMessage = message;
                    }
                    Plugin.PluginLog.Info($"[HandleBookingRequestResult] success={success}, message={message}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"[HandleBookingRequestResult] Error: {ex.Message}");
            }
        }

        public static void HandleMyBookings(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet ID
                    int count = buffer.ReadInt();
                    var bookings = new List<BookingRequest>();
                    for (int i = 0; i < count; i++)
                    {
                        var booking = new BookingRequest
                        {
                            id = buffer.ReadInt(),
                            listingId = buffer.ReadInt(),
                            bookableEntryId = buffer.ReadInt(),
                            bookableEntryName = buffer.ReadString(),
                            venueName = buffer.ReadString(),
                            requesterName = buffer.ReadString(),
                            requesterWorld = buffer.ReadString(),
                            requestedDate = new DateTime(buffer.ReadLong()),
                            requestedTime = TimeSpan.FromMinutes(buffer.ReadInt()),
                            timezone = buffer.ReadString(),
                            notes = buffer.ReadString(),
                            status = buffer.ReadInt(),
                            createdAt = new DateTime(buffer.ReadLong()),
                        };
                        bookings.Add(booking);
                    }
                    ListingsWindow.myBookings = bookings;
                    ListingsWindow.fetchedMyBookings = true;
                    Plugin.PluginLog.Info($"[HandleMyBookings] Received {count} bookings");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"[HandleMyBookings] Error: {ex.Message}");
            }
        }

        public static void HandleBookingResponseResult(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet ID
                    bool success = buffer.ReadBool();
                    int bookingId = buffer.ReadInt();
                    int newStatus = buffer.ReadInt();
                    if (success)
                    {
                        // Update in incoming requests
                        var incoming = ListingsWindow.incomingBookingRequests.FirstOrDefault(b => b.id == bookingId);
                        if (incoming != null) incoming.status = newStatus;
                        // Update in my bookings
                        var mine = ListingsWindow.myBookings.FirstOrDefault(b => b.id == bookingId);
                        if (mine != null) mine.status = newStatus;
                    }
                    Plugin.PluginLog.Info($"[HandleBookingResponseResult] success={success}, bookingId={bookingId}, newStatus={newStatus}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"[HandleBookingResponseResult] Error: {ex.Message}");
            }
        }

        public static void HandleIncomingBookings(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet ID
                    int count = buffer.ReadInt();
                    var bookings = new List<BookingRequest>();
                    for (int i = 0; i < count; i++)
                    {
                        var booking = new BookingRequest
                        {
                            id = buffer.ReadInt(),
                            listingId = buffer.ReadInt(),
                            bookableEntryId = buffer.ReadInt(),
                            bookableEntryName = buffer.ReadString(),
                            venueName = buffer.ReadString(),
                            requesterName = buffer.ReadString(),
                            requesterWorld = buffer.ReadString(),
                            requestedDate = new DateTime(buffer.ReadLong()),
                            requestedTime = TimeSpan.FromMinutes(buffer.ReadInt()),
                            timezone = buffer.ReadString(),
                            notes = buffer.ReadString(),
                            status = buffer.ReadInt(),
                            createdAt = new DateTime(buffer.ReadLong()),
                            requesterUserId = buffer.ReadInt(),
                        };
                        bookings.Add(booking);
                    }
                    ListingsWindow.incomingBookingRequests = bookings;
                    Plugin.PluginLog.Info($"[HandleIncomingBookings] Received {count} incoming bookings");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"[HandleIncomingBookings] Error: {ex.Message}");
            }
        }

        public static void HandleBookableEntriesSaved(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet ID
                    bool success = buffer.ReadBool();
                    Plugin.PluginLog.Info($"[HandleBookableEntriesSaved] success={success}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"[HandleBookableEntriesSaved] Error: {ex.Message}");
            }
        }

        public static void HandleBookingNotification(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet ID
                    int bookingId = buffer.ReadInt();
                    int listingId = buffer.ReadInt();
                    string venueName = buffer.ReadString();
                    string requesterName = buffer.ReadString();
                    int status = buffer.ReadInt();
                    string message = buffer.ReadString();
                    Plugin.PluginLog.Info($"[HandleBookingNotification] bookingId={bookingId}, venue={venueName}, requester={requesterName}, status={status}, msg={message}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"[HandleBookingNotification] Error: {ex.Message}");
            }
        }
    }
}
