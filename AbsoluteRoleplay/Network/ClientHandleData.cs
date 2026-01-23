using AbsoluteRP.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Networking
{ 
    static class ClientHandleData
    {
        private static ByteBuffer playerBuffer;
        public static DataReceiver dr = new DataReceiver();
        public delegate void Packet(byte[] data);
        public static Dictionary<int, Packet> packets = new Dictionary<int, Packet>();

        //add our packets so we don't need to load them on the go.
        //should be added to start of client loading up
        public static void InitializePackets()
        {
            packets.Clear();
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

            packets.Add((int)ServerPackets.RecFauxNameBroadcast, DataReceiver.ReceiveFauxName);
            //simple message back from server, simply for verification that the user is connected
        }

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

        private static void HandleDataPackets(byte[] data)
        {
            var buffer = new ByteBuffer();
            buffer.WriteBytes(data);
            var packetID = buffer.ReadInt();
            WindowOperations.SafeDispose(buffer);
            buffer = null;
            if (packets.TryGetValue(packetID, out var packet))
            {
                packet.Invoke(data);
            }
        }
    }
}
