using AbsoluteRP.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Networking
{
    // Responsible for receiving raw bytes from the server, reassembling them into
    // complete packets, and dispatching each packet to the correct handler method.
    // Works hand-in-hand with DataReceiver (which contains the actual handler logic).
    static class ClientHandleData
    {
        private static ByteBuffer playerBuffer; // accumulates incoming bytes until a full packet is available
        public static DataReceiver dr = new DataReceiver();
        public delegate void Packet(byte[] data); // signature every packet handler must match
        public static Dictionary<int, Packet> packets = new Dictionary<int, Packet>(); // maps packet IDs to their handler delegates

        // Registers every known server packet ID to its handler function.
        // Must be called once at startup before any data arrives so the dispatch
        // table is ready. Packet IDs must match the server-side enum exactly.
        public static void InitializePackets()
        {
            packets.Clear();
            // --- Core authentication and profile packets ---
            packets.Add((int)ServerPackets.SWelcomeMessage, DataReceiver.HandleWelcomeMessage);
            packets.Add((int)ServerPackets.SRecLoginStatus, DataReceiver.StatusMessage);
            packets.Add((int)ServerPackets.SRecProfileBio, DataReceiver.RecieveBioTab);
            packets.Add((int)ServerPackets.SNoProfile, DataReceiver.NoProfile);
            packets.Add((int)ServerPackets.SSendProfile, DataReceiver.ReceiveProfile);
            packets.Add((int)ServerPackets.SRecExistingProfile, DataReceiver.ExistingProfile);
            packets.Add((int)ServerPackets.SSendNoProfileNotes, DataReceiver.NoProfileNotes);
            packets.Add((int)ServerPackets.SSendProfileNotes, DataReceiver.RecProfileNotes);
            packets.Add((int)ServerPackets.SRecBookmarks, DataReceiver.RecBookmarks);
            packets.Add((int)ServerPackets.CProfileReportedSuccessfully, DataReceiver.RecProfileReportedSuccessfully);
            packets.Add((int)ServerPackets.CProfileAlreadyReported, DataReceiver.RecProfileAlreadyReported);
            packets.Add((int)ServerPackets.SRecNoTargetProfile, DataReceiver.NoTargetProfile);
            packets.Add((int)ServerPackets.SRecTargetProfile, DataReceiver.HandleTargetProfilePacket);
            packets.Add((int)ServerPackets.SSendNoAuthorization, DataReceiver.ReceiveNoAuthorization);
            packets.Add((int)ServerPackets.ReceiveConnections, DataReceiver.ReceiveConnections);
            packets.Add((int)ServerPackets.ReceiveNewConnectionRequest, DataReceiver.ReceiveConnectionsRequest);
            packets.Add((int)ServerPackets.RecieveTargetTooltip, DataReceiver.ReceiveTargetTooltip);
            packets.Add((int)ServerPackets.ReceiveProfiles, DataReceiver.ReceiveProfiles);
            packets.Add((int)ServerPackets.CreateItem, DataReceiver.ReceiveProfileItems);
            packets.Add((int)ServerPackets.ReceiveProfileSettings, DataReceiver.ReceiveProfileSettings);

            // --- Chat, social, and UI feature packets ---
            packets.Add((int)ServerPackets.ReceiveChatMessage, DataReceiver.ReceiveChatMessage);
            packets.Add((int)ServerPackets.ReceiveProfileWarning, DataReceiver.RecieveProfileWarning);
            packets.Add((int)ServerPackets.ReceiveProfileListings, DataReceiver.ReceivePersonalListings);
            packets.Add((int)ServerPackets.ReceiveProfileDetails, DataReceiver.ReceiveDetailsTab);
            packets.Add((int)ServerPackets.ReceiveTabCount, DataReceiver.ReceiveTabCount);
            packets.Add((int)ServerPackets.ReceiveGalleryTab, DataReceiver.ReceiveProfileGalleryTab);
            packets.Add((int)ServerPackets.ReceiveInfoTab, DataReceiver.ReceiveInfoTab);
            packets.Add((int)ServerPackets.SRecProfileStory, DataReceiver.ReceiveStoryTab);
            packets.Add((int)ServerPackets.ReceiveTabsUpdate, DataReceiver.ReceiveTabsUpdate);
            packets.Add((int)ServerPackets.ReceiveInventoryTab, DataReceiver.ReceiveInventoryTab);
            packets.Add((int)ServerPackets.ReceiveDynamicTab, DataReceiver.ReceiveDynamicTab);
            packets.Add((int)ServerPackets.ReceiveTradeRequest, DataReceiver.ReceiveTradeRequest);
            packets.Add((int)ServerPackets.ReceiveTradeUpdate, DataReceiver.ReceiveTradeUpdate);
            packets.Add((int)ServerPackets.ReceiveTradeStatus, DataReceiver.ReceiveTradeStatus);
            packets.Add((int)ServerPackets.ReceiveTradeInventory, DataReceiver.ReceiveTradeInventory);
            packets.Add((int)ServerPackets.ReceiveTreeLayout, DataReceiver.ReceiveTreeLayout);
            packets.Add((int)ServerPackets.RecConnectedPlayersInMap, DataReceiver.ReceiveConnectedPlayersInMap);
            // --- Group management packets ---
            packets.Add((int)ServerPackets.ReceiveGroup, DataReceiver.ReceiveGroup);
            packets.Add((int)ServerPackets.ReceiveGroupMemberships, DataReceiver.ReceiveGroupMemberships);
            packets.Add((int)ServerPackets.SendGroupRanks, DataReceiver.HandleGroupRanks);
            packets.Add((int)ServerPackets.SendRankOperationResult, DataReceiver.HandleRankOperationResult);
            packets.Add((int)ServerPackets.SendGroupMembers, DataReceiver.HandleGroupMembers);
            packets.Add((int)ServerPackets.SendGroupCategories, DataReceiver.HandleFetchGroupCategories);
            packets.Add((int)ServerPackets.SendGroupChatMessages, DataReceiver.HandleSendGroupChatMessage);
            packets.Add((int)ServerPackets.SendGroupChatMessageBroadcast, DataReceiver.HandleGroupChatMessageBroadcast);
            packets.Add((int)ServerPackets.SendGroupMemberAvatar, DataReceiver.HandleGroupMemberAvatar);
            packets.Add((int)ServerPackets.SendGroupRosterFields, DataReceiver.HandleFetchGroupRosterFields);
            packets.Add((int)ServerPackets.SendMemberMetadata, DataReceiver.HandleFetchMemberMetadata);
            packets.Add((int)ServerPackets.SendMemberFieldValues, DataReceiver.HandleFetchMemberFieldValues);
            packets.Add((int)ServerPackets.SendChatMessageDeleted, DataReceiver.HandleDeleteGroupChatMessage);
            packets.Add((int)ServerPackets.SendChatMessageEdited, DataReceiver.HandleEditGroupChatMessage);
            packets.Add((int)ServerPackets.SendGroupInvites, DataReceiver.HandleGroupInvites);
            packets.Add((int)ServerPackets.SendGroupInviteResult, DataReceiver.HandleGroupInviteResult);
            packets.Add((int)ServerPackets.SendForumStructure, DataReceiver.HandleForumStructure);
            packets.Add((int)ServerPackets.SendForumPermissions, DataReceiver.HandleForumPermissions);
            packets.Add((int)ServerPackets.SendInviteNotification, DataReceiver.HandleInviteNotification);
            packets.Add((int)ServerPackets.SendInviteeProfile, DataReceiver.HandleInviteeProfile);
            packets.Add((int)ServerPackets.SendLikesRemaining, DataReceiver.HandleLikesRemainingPacket);
            packets.Add((int)ServerPackets.SendLikeResult, DataReceiver.HandleLikeResultPacket);
            packets.Add((int)ServerPackets.SendProfileLikeCounts, DataReceiver.HandleProfileLikeCountsPacket);
            packets.Add((int)ServerPackets.SendProfileLikes, DataReceiver.HandleProfileLikesPacket);
            packets.Add((int)ServerPackets.SendPinnedMessages, DataReceiver.HandlePinnedMessages);
            packets.Add((int)ServerPackets.SendMessagePinResult, DataReceiver.HandleMessagePinResult);
            packets.Add((int)ServerPackets.SendMessagePinUpdate, DataReceiver.HandleMessagePinUpdate);
            packets.Add((int)ServerPackets.SendChannelLockUpdate, DataReceiver.HandleChannelLockUpdate);

            // Rules Channel & Self-Assign Roles
            packets.Add((int)ServerPackets.SendGroupRulesResponse, DataReceiver.HandleGroupRulesResponse);
            packets.Add((int)ServerPackets.SendRulesAgreementResponse, DataReceiver.HandleRulesAgreementResponse);
            packets.Add((int)ServerPackets.SendGroupRules, DataReceiver.HandleGroupRules);
            packets.Add((int)ServerPackets.SendSelfAssignRoleResponse, DataReceiver.HandleSelfAssignRoleResponse);
            packets.Add((int)ServerPackets.SendSelfAssignRoles, DataReceiver.HandleSelfAssignRoles);
            packets.Add((int)ServerPackets.SendSelfRoleAssignmentResponse, DataReceiver.HandleSelfRoleAssignmentResponse);
            packets.Add((int)ServerPackets.SendRoleChannelPermissionsResponse, DataReceiver.HandleRoleChannelPermissionsResponse);
            packets.Add((int)ServerPackets.SendMemberSelfRoles, DataReceiver.HandleMemberSelfRoles);
            packets.Add((int)ServerPackets.SendCreateChannelError, DataReceiver.HandleCreateChannelError);
            packets.Add((int)ServerPackets.SendRoleSections, DataReceiver.HandleRoleSections);
            packets.Add((int)ServerPackets.SendGroupBans, DataReceiver.HandleGroupBans);
            packets.Add((int)ServerPackets.SendMemberRemovedFromGroup, DataReceiver.HandleMemberRemovedFromGroup);
            packets.Add((int)ServerPackets.SendGroupInfo, DataReceiver.HandleGroupInfo);
            packets.Add((int)ServerPackets.SendProfileInfoEmbed, DataReceiver.HandleProfileInfoEmbed);
            // Form Channel
            packets.Add((int)ServerPackets.SendFormFields, DataReceiver.HandleFormFields);
            packets.Add((int)ServerPackets.SendFormSubmissions, DataReceiver.HandleFormSubmissions);
            packets.Add((int)ServerPackets.SendFormSubmitResult, DataReceiver.HandleFormSubmitResult);
            // Group Search
            packets.Add((int)ServerPackets.SendPublicGroupSearchResults, DataReceiver.HandlePublicGroupSearchResults);
            // Join Requests
            packets.Add((int)ServerPackets.SendJoinRequests, DataReceiver.HandleJoinRequests);
            packets.Add((int)ServerPackets.SendJoinRequestResult, DataReceiver.HandleJoinRequestResult);
            packets.Add((int)ServerPackets.SendJoinRequestNotification, DataReceiver.HandleJoinRequestNotification);

            // Character Sync
            packets.Add((int)ServerPackets.SendVerifiedCharacters, DataReceiver.HandleVerifiedCharacters);

            // Server Notifications (shutdown, restart, broadcast)
            packets.Add((int)ServerPackets.SendServerNotification, DataReceiver.HandleServerNotification);

            // Listings System
            packets.Add((int)ServerPackets.SendListingCreated, DataReceiver.HandleListingCreated);
            packets.Add((int)ServerPackets.SendListingUpdated, DataReceiver.HandleListingUpdated);
            packets.Add((int)ServerPackets.SendListingDeleted, DataReceiver.HandleListingDeleted);
            packets.Add((int)ServerPackets.SendListingsList, DataReceiver.HandleListingsList);
            packets.Add((int)ServerPackets.SendListingDetail, DataReceiver.HandleListingDetail);
            packets.Add((int)ServerPackets.SendMyListings, DataReceiver.HandleMyListings);
            packets.Add((int)ServerPackets.SendBookmarkResult, DataReceiver.HandleBookmarkResult);
            packets.Add((int)ServerPackets.SendMenuUpdated, DataReceiver.HandleMenuUpdated);
            packets.Add((int)ServerPackets.SendScheduleUpdated, DataReceiver.HandleScheduleUpdated);
            packets.Add((int)ServerPackets.SendImageUploaded, DataReceiver.HandleImageUploaded);
            packets.Add((int)ServerPackets.SendListingError, DataReceiver.HandleListingError);

            // Booking System
            packets.Add((int)ServerPackets.SendBookingRequestResult, DataReceiver.HandleBookingRequestResult);
            packets.Add((int)ServerPackets.SendMyBookings, DataReceiver.HandleMyBookings);
            packets.Add((int)ServerPackets.SendBookingResponseResult, DataReceiver.HandleBookingResponseResult);
            packets.Add((int)ServerPackets.SendIncomingBookings, DataReceiver.HandleIncomingBookings);
            packets.Add((int)ServerPackets.SendBookableEntriesSaved, DataReceiver.HandleBookableEntriesSaved);
            packets.Add((int)ServerPackets.SendBookingNotification, DataReceiver.HandleBookingNotification);

            // RP Systems
            packets.Add((int)ServerPackets.SendSystemCreated, DataReceiver.HandleSystemCreated);
            packets.Add((int)ServerPackets.SendSystemDeleted, DataReceiver.HandleSystemDeleted);
            packets.Add((int)ServerPackets.SendMySystems, DataReceiver.HandleMySystems);
            packets.Add((int)ServerPackets.SendSystemData, DataReceiver.HandleSystemData);
            packets.Add((int)ServerPackets.SendStatsSaved, DataReceiver.HandleStatsSaved);
            packets.Add((int)ServerPackets.SendCombatConfigSaved, DataReceiver.HandleCombatConfigSaved);
            packets.Add((int)ServerPackets.SendSkillClassesSaved, DataReceiver.HandleSkillClassesSaved);
            packets.Add((int)ServerPackets.SendSkillsSaved, DataReceiver.HandleSkillsSaved);
            packets.Add((int)ServerPackets.SendSystemError, DataReceiver.HandleSystemError);

            // Sheet submission & roster
            packets.Add((int)ServerPackets.SendSubmitSheetResult, DataReceiver.HandleSubmitSheetResult);
            packets.Add((int)ServerPackets.SendSystemRoster, DataReceiver.HandleSystemRoster);
            packets.Add((int)ServerPackets.SendSheetResponse, DataReceiver.HandleSheetResponse);
            packets.Add((int)ServerPackets.SendSystemBans, DataReceiver.HandleSystemBans);

            // Joined systems
            packets.Add((int)ServerPackets.SendJoinedSystems, DataReceiver.HandleJoinedSystems);

            // Equipment
            packets.Add((int)ServerPackets.SendEquipment, DataReceiver.HandleEquipment);
            packets.Add((int)ServerPackets.SendTargetEquipment, DataReceiver.HandleTargetEquipment);

            //simple message back from server, simply for verification that the user is connected
        }

        // Entry point for all incoming server data. Accumulates raw bytes into playerBuffer,
        // then loops extracting complete packets. Each packet is length-prefixed (4-byte int)
        // followed by that many bytes of payload. Handles partial reads gracefully by keeping
        // leftover bytes in the buffer until the next call delivers the rest.
        public static void HandleData(byte[] data)
        {
            // Clone the incoming data to avoid modifying the original buffer
            var buffer = (byte[])data.Clone();
            var pLength = 0;

            // Initialize the player buffer if it's null
            if (playerBuffer == null)
            {
                playerBuffer = new ByteBuffer();
            }

            // Write the cloned data into the player buffer
            playerBuffer.WriteBytes(buffer);

            // Check if the buffer is empty; clear and return if true
            if (playerBuffer.Count() == 0)
            {
                playerBuffer.Clear();
                return;
            }

            // Ensure there are at least 4 bytes (length header) in the buffer
            if (playerBuffer.Length() > 4)
            {
                // Read the packet length without removing it from the buffer
                pLength = playerBuffer.ReadInt(false);

                // If the length is invalid, clear the buffer and exit
                if (pLength <= 0)
                {
                    playerBuffer.Clear();
                    return;
                }
            }

            // Process the data packets while there are valid lengths and enough data in the buffer
            while (pLength > 0 && pLength <= playerBuffer.Length() - 4)
            {
                // Check again if there is enough data for the packet
                if (pLength <= playerBuffer.Length() - 4)
                {
                    // Consume the packet length header
                    playerBuffer.ReadInt();

                    // Read the actual data packet
                    data = playerBuffer.ReadBytes(pLength);

                    // Handle the extracted data packet
                    HandleDataPackets(data);
                }

                // Reset packet length and check for the next packet
                pLength = 0;

                // Ensure there are still enough bytes for another length header
                if (playerBuffer.Length() > 4)
                {
                    // Peek the next packet length without removing it
                    pLength = playerBuffer.ReadInt(false);

                    // If the length is invalid, clear the buffer and exit
                    if (pLength <= 0)
                    {
                        playerBuffer.Clear();
                        return;
                    }
                }
            }

            // If no valid packet remains, clear the buffer
            if (pLength <= 1)
            {
                playerBuffer.Clear();
            }
        }

        // Extracts the packet ID (first 4 bytes) from a complete packet payload,
        // looks it up in the dispatch table, and invokes the matching handler.
        // The full payload (including the ID) is passed to the handler because
        // each handler re-reads the ID and then continues parsing its own fields.
        private static void HandleDataPackets(byte[] data)
        {
            var buffer = new ByteBuffer();
            buffer.WriteBytes(data);
            var packetID = buffer.ReadInt(); // first int in every packet is its type ID
            WindowOperations.SafeDispose(buffer);
            buffer = null;
            if (packets.TryGetValue(packetID, out var packet))
            {
                packet.Invoke(data);
            }
        }
    }
}
