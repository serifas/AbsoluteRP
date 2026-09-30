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
    // Public group search and join request packets. Split out of DataReceiver.
    internal class GroupSearch_DR
    {
        public static List<GroupJoinRequest> joinRequests = new List<GroupJoinRequest>();

        // Group Search state
        public static List<GroupSearchResult> groupSearchResults = new List<GroupSearchResult>();

        public static bool groupSearchInProgress = false;

        /// Handles public group search results from server
        public static void HandlePublicGroupSearchResults(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int count = buffer.ReadInt();
                Plugin.PluginLog.Info($"[HandlePublicGroupSearchResults] Received {count} results");

                var results = new List<GroupSearchResult>();
                for (int i = 0; i < count; i++)
                {
                    var result = new GroupSearchResult
                    {
                        groupID = buffer.ReadInt(),
                        name = buffer.ReadString(),
                        description = buffer.ReadString(),
                        logoUrl = buffer.ReadString(),
                        memberCount = buffer.ReadInt(),
                        openInvite = buffer.ReadBool() // Whether anyone can join or if it requires a request
                    };
                    results.Add(result);
                    Plugin.PluginLog.Info($"[HandlePublicGroupSearchResults] Result {i}: id={result.groupID}, name='{result.name}', members={result.memberCount}, openInvite={result.openInvite}");
                }

                buffer.Dispose();

                groupSearchResults = results;
                groupSearchInProgress = false;
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"HandlePublicGroupSearchResults error: {ex.Message}");
                groupSearchInProgress = false;
            }
        }

        /// Handles the list of join requests for a group.
        public static void HandleJoinRequests(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                int count = buffer.ReadInt();
                Plugin.PluginLog.Info($"[HandleJoinRequests] Received {count} join requests for group {groupID}");

                var requests = new List<GroupJoinRequest>();
                for (int i = 0; i < count; i++)
                {
                    var request = new GroupJoinRequest
                    {
                        requestID = buffer.ReadInt(),
                        groupID = groupID,
                        requesterUserID = buffer.ReadInt(),
                        requesterProfileID = buffer.ReadInt(),
                        requesterName = buffer.ReadString(),
                        requesterWorld = buffer.ReadString(),
                        avatarUrl = buffer.ReadString(),
                        message = buffer.ReadString(),
                        status = buffer.ReadByte(),
                        createdAt = buffer.ReadLong()
                    };
                    requests.Add(request);
                    Plugin.PluginLog.Debug($"[HandleJoinRequests] Request {i}: id={request.requestID}, name='{request.requesterName}@{request.requesterWorld}'");
                }

                buffer.Dispose();

                // Update the join requests list
                joinRequests = requests;

                // Also update the group's join requests if we have it loaded
                var group = AbsoluteRP.Windows.Social.Views.GroupsData.groups?.FirstOrDefault(g => g.groupID == groupID);
                if (group != null)
                {
                    group.joinRequests = requests;
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"HandleJoinRequests error: {ex.Message}");
            }
        }

        /// Handles the result of sending a join request or responding to one.
        public static void HandleJoinRequestResult(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                bool success = buffer.ReadBool();
                string message = buffer.ReadString();
                int actionType = buffer.ReadInt(); // 0=sent, 1=accepted, 2=declined, 3=cancelled

                buffer.Dispose();

                Plugin.PluginLog.Info($"[HandleJoinRequestResult] Success: {success}, Action: {actionType}, Message: {message}");

                // Show notification to user
                if (success)
                {
                    switch (actionType)
                    {
                        case 0:
                            Plugin.PluginLog.Info("Join request sent successfully!");
                            break;
                        case 1:
                            Plugin.PluginLog.Info("Join request accepted!");
                            break;
                        case 2:
                            Plugin.PluginLog.Info("Join request declined.");
                            break;
                        case 3:
                            Plugin.PluginLog.Info("Join request cancelled.");
                            break;
                    }
                }
                else
                {
                    Plugin.PluginLog.Warning($"Join request action failed: {message}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"HandleJoinRequestResult error: {ex.Message}");
            }
        }

        /// Handles a notification that a new join request has been received (for group admins).
        public static void HandleJoinRequestNotification(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                string groupName = buffer.ReadString();
                var request = new GroupJoinRequest
                {
                    requestID = buffer.ReadInt(),
                    groupID = groupID,
                    requesterUserID = buffer.ReadInt(),
                    requesterProfileID = buffer.ReadInt(),
                    requesterName = buffer.ReadString(),
                    requesterWorld = buffer.ReadString(),
                    avatarUrl = buffer.ReadString(),
                    message = buffer.ReadString(),
                    status = 0, // pending
                    createdAt = buffer.ReadLong()
                };

                buffer.Dispose();

                Plugin.PluginLog.Info($"[HandleJoinRequestNotification] New join request from {request.requesterName}@{request.requesterWorld} for group '{groupName}'");

                // Add to the group's join requests if loaded
                var group = AbsoluteRP.Windows.Social.Views.GroupsData.groups?.FirstOrDefault(g => g.groupID == groupID);
                if (group != null)
                {
                    if (group.joinRequests == null)
                        group.joinRequests = new List<GroupJoinRequest>();

                    // Only add if not already present
                    if (!group.joinRequests.Any(r => r.requestID == request.requestID))
                    {
                        group.joinRequests.Add(request);
                    }
                }

                // TODO: Show notification window similar to GroupInviteNotification
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"HandleJoinRequestNotification error: {ex.Message}");
            }
        }
    }
}
