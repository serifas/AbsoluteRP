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
    // Server shutdown/restart/broadcast notifications. Split out of DataReceiver.
    internal class ServerNotifications_DR
    {
        /// Server notification types matching the server enum.
        public enum ServerNotificationType : byte
        {
            Broadcast = 0,           // General broadcast message
            ShutdownScheduled = 1,   // Shutdown/restart has been scheduled
            ShutdownWarning = 2,     // Warning that shutdown/restart is imminent
            ShutdownImmediate = 3,   // Server is shutting down NOW
            ShutdownCancelled = 4,   // Scheduled shutdown was cancelled
        }

        /// Handles server notification packets (shutdown, restart, broadcast). Displays toast alerts to the user.
        // Receives server-wide notifications (shutdown warnings, restart notices, broadcasts). Displays them as important notices to the user.
        public static void HandleServerNotification(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet ID

                    var notificationType = (ServerNotificationType)buffer.ReadByte();
                    string message = buffer.ReadString();
                    int secondsRemaining = buffer.ReadInt();
                    bool isRestart = buffer.ReadBool();

                    Plugin.PluginLog.Info($"[HandleServerNotification] Type={notificationType}, Message='{message}', SecondsRemaining={secondsRemaining}, IsRestart={isRestart}");

                    // Display toast alert
                    ShowServerNotificationToast(notificationType, message, secondsRemaining, isRestart);
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"[HandleServerNotification] Error: {ex.Message}");
            }
        }

        /// Shows a toast alert for server notifications.
        internal static void ShowServerNotificationToast(ServerNotificationType type, string message, int secondsRemaining, bool isRestart)
        {
            var action = isRestart ? "restart" : "shutdown";

            switch (type)
            {
                case ServerNotificationType.Broadcast:
                    // Show broadcast as normal toast
                    Plugin.ToastGui?.ShowNormal($"[AbsoluteRP] {message}");
                    break;

                case ServerNotificationType.ShutdownScheduled:
                    {
                        var timeStr = FormatTimeRemaining(secondsRemaining);
                        Plugin.ToastGui?.ShowError($"[AbsoluteRP] Server {action} in {timeStr}: {message}");
                    }
                    break;

                case ServerNotificationType.ShutdownWarning:
                    {
                        var timeStr = FormatTimeRemaining(secondsRemaining);
                        // Use error toast for urgency on warnings under 60 seconds
                        if (secondsRemaining <= 60)
                            Plugin.ToastGui?.ShowError($"[AbsoluteRP] Server {action} in {timeStr}!");
                        else
                            Plugin.ToastGui?.ShowNormal($"[AbsoluteRP] Server {action} in {timeStr}: {message}");
                    }
                    break;

                case ServerNotificationType.ShutdownImmediate:
                    Plugin.ToastGui?.ShowError($"[AbsoluteRP] Server {action} NOW!");
                    break;

                case ServerNotificationType.ShutdownCancelled:
                    Plugin.ToastGui?.ShowNormal($"[AbsoluteRP] Server {action} cancelled");
                    break;
            }
        }

        /// Formats seconds into a human-readable time string.
        internal static string FormatTimeRemaining(int seconds)
        {
            if (seconds >= 60)
            {
                var minutes = seconds / 60;
                var secs = seconds % 60;
                return secs > 0 ? $"{minutes}m {secs}s" : $"{minutes} minute(s)";
            }
            return $"{seconds} second(s)";
        }
    }
}
