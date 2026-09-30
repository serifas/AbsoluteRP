using AbsoluteRP.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AbsoluteRP.Network;
using AbsoluteRP.Immersive.Themes;
namespace Networking
{
    // Responsible for receiving raw bytes from the server, reassembling them into complete packets, and dispatching each packet to the correct handler method. Works hand-in-hand with DataReceiver (which contains the actual handler logic).
    static class ClientHandleData
    {
        private static ByteBuffer playerBuffer; // accumulates incoming bytes until a full packet is available
        public static DataReceiver dr = new DataReceiver();
        public delegate void Packet(byte[] data); // signature every packet handler must match
        public static Dictionary<int, Packet> packets = new Dictionary<int, Packet>(); // maps packet IDs to their handler delegates

        // Registers every known server packet ID to its handler function. Must be called once at startup before any data arrives so the dispatch table is ready. Packet IDs must match the server-side enum exactly.
        public static void InitializePackets()
        {
            packets.Clear();
            // Core authentication and profile packets
            packets.Add((int)ServerPackets.SWelcomeMessage, Accounts_DR.HandleWelcomeMessage);
            packets.Add((int)ServerPackets.SRecLoginStatus, Accounts_DR.StatusMessage);
            packets.Add((int)ServerPackets.SRecProfileBio, Profiles_DR.RecieveBioTab);
            packets.Add((int)ServerPackets.SNoProfile, Profiles_DR.NoProfile);
            packets.Add((int)ServerPackets.SSendProfile, Profiles_DR.ReceiveProfile);
            packets.Add((int)ServerPackets.SRecExistingProfile, Profiles_DR.ExistingProfile);
            packets.Add((int)ServerPackets.SSendNoProfileNotes, Profiles_DR.NoProfileNotes);
            packets.Add((int)ServerPackets.SSendProfileNotes, Profiles_DR.RecProfileNotes);
            packets.Add((int)ServerPackets.SRecBookmarks, Profiles_DR.RecBookmarks);
            packets.Add((int)ServerPackets.CProfileReportedSuccessfully, Profiles_DR.RecProfileReportedSuccessfully);
            packets.Add((int)ServerPackets.CProfileAlreadyReported, Profiles_DR.RecProfileAlreadyReported);
            packets.Add((int)ServerPackets.SRecNoTargetProfile, Profiles_DR.NoTargetProfile);
            packets.Add((int)ServerPackets.SRecTargetProfile, Profiles_DR.HandleTargetProfilePacket);
            packets.Add((int)ServerPackets.SSendNoAuthorization, Accounts_DR.ReceiveNoAuthorization);
            packets.Add((int)ServerPackets.ReceiveConnections, Connections_DR.ReceiveConnections);
            packets.Add((int)ServerPackets.ReceiveNewConnectionRequest, Connections_DR.ReceiveConnectionsRequest);
            packets.Add((int)ServerPackets.RecieveTargetTooltip, Profiles_DR.ReceiveTargetTooltip);
            packets.Add((int)ServerPackets.ReceiveProfiles, Profiles_DR.ReceiveProfiles);
            packets.Add((int)ServerPackets.CreateItem, Profiles_DR.ReceiveProfileItems);
            packets.Add((int)ServerPackets.ReceiveProfileSettings, Profiles_DR.ReceiveProfileSettings);

            // Chat, social, and UI feature packets
            packets.Add((int)ServerPackets.ReceiveProfileWarning, Profiles_DR.RecieveProfileWarning);
            packets.Add((int)ServerPackets.ReceiveProfileListings, Listings_DR.ReceivePersonalListings);
            packets.Add((int)ServerPackets.ReceiveProfileDetails, Profiles_DR.ReceiveDetailsTab);
            packets.Add((int)ServerPackets.ReceiveTabCount, Profiles_DR.ReceiveTabCount);
            packets.Add((int)ServerPackets.ReceiveGalleryTab, Profiles_DR.ReceiveProfileGalleryTab);
            packets.Add((int)ServerPackets.ReceiveInfoTab, Profiles_DR.ReceiveInfoTab);
            packets.Add((int)ServerPackets.SRecProfileStory, Profiles_DR.ReceiveStoryTab);
            packets.Add((int)ServerPackets.ReceiveTabsUpdate, Profiles_DR.ReceiveTabsUpdate);
            packets.Add((int)ServerPackets.ReceiveInventoryTab, Profiles_DR.ReceiveInventoryTab);
            packets.Add((int)ServerPackets.ReceiveDynamicTab, Profiles_DR.ReceiveDynamicTab);
            packets.Add((int)ServerPackets.ReceiveTradeRequest, Trades_DR.ReceiveTradeRequest);
            packets.Add((int)ServerPackets.ReceiveTradeUpdate, Trades_DR.ReceiveTradeUpdate);
            packets.Add((int)ServerPackets.ReceiveTradeStatus, Trades_DR.ReceiveTradeStatus);
            packets.Add((int)ServerPackets.ReceiveTradeInventory, Trades_DR.ReceiveTradeInventory);
            packets.Add((int)ServerPackets.ReceiveTreeLayout, Profiles_DR.ReceiveTreeLayout);
            packets.Add((int)ServerPackets.RecConnectedPlayersInMap, Connections_DR.ReceiveConnectedPlayersInMap);
            // Group management packets
            packets.Add((int)ServerPackets.ReceiveGroup, Groups_DR.ReceiveGroup);
            packets.Add((int)ServerPackets.ReceiveGroupMemberships, Groups_DR.ReceiveGroupMemberships);
            packets.Add((int)ServerPackets.SendGroupRanks, Groups_DR.HandleGroupRanks);
            packets.Add((int)ServerPackets.SendRankOperationResult, Groups_DR.HandleRankOperationResult);
            packets.Add((int)ServerPackets.SendGroupMembers, Groups_DR.HandleGroupMembers);
            packets.Add((int)ServerPackets.SendGroupCategories, Groups_DR.HandleFetchGroupCategories);
            packets.Add((int)ServerPackets.SendGroupChatMessages, GroupChat_DR.HandleSendGroupChatMessage);
            packets.Add((int)ServerPackets.SendGroupChatMessageBroadcast, GroupChat_DR.HandleGroupChatMessageBroadcast);
            packets.Add((int)ServerPackets.SendGroupMemberAvatar, Groups_DR.HandleGroupMemberAvatar);
            packets.Add((int)ServerPackets.SendGroupRosterFields, Groups_DR.HandleFetchGroupRosterFields);
            packets.Add((int)ServerPackets.SendMemberMetadata, Groups_DR.HandleFetchMemberMetadata);
            packets.Add((int)ServerPackets.SendMemberFieldValues, Groups_DR.HandleFetchMemberFieldValues);
            packets.Add((int)ServerPackets.SendChatMessageDeleted, GroupChat_DR.HandleDeleteGroupChatMessage);
            packets.Add((int)ServerPackets.SendChatMessageEdited, GroupChat_DR.HandleEditGroupChatMessage);
            packets.Add((int)ServerPackets.SendGroupInvites, Groups_DR.HandleGroupInvites);
            packets.Add((int)ServerPackets.SendGroupInviteResult, Groups_DR.HandleGroupInviteResult);
            packets.Add((int)ServerPackets.SendForumStructure, Groups_DR.HandleForumStructure);
            packets.Add((int)ServerPackets.SendForumPermissions, Groups_DR.HandleForumPermissions);
            packets.Add((int)ServerPackets.SendInviteNotification, Groups_DR.HandleInviteNotification);
            packets.Add((int)ServerPackets.SendInviteeProfile, Groups_DR.HandleInviteeProfile);
            packets.Add((int)ServerPackets.SendLikesRemaining, ProfileLikes_DR.HandleLikesRemainingPacket);
            packets.Add((int)ServerPackets.SendLikeResult, ProfileLikes_DR.HandleLikeResultPacket);
            packets.Add((int)ServerPackets.SendProfileLikeCounts, ProfileLikes_DR.HandleProfileLikeCountsPacket);
            packets.Add((int)ServerPackets.SendProfileLikes, ProfileLikes_DR.HandleProfileLikesPacket);
            packets.Add((int)ServerPackets.SendPinnedMessages, GroupChat_DR.HandlePinnedMessages);
            packets.Add((int)ServerPackets.SendMessagePinResult, GroupChat_DR.HandleMessagePinResult);
            packets.Add((int)ServerPackets.SendMessagePinUpdate, GroupChat_DR.HandleMessagePinUpdate);
            packets.Add((int)ServerPackets.SendChannelLockUpdate, GroupChat_DR.HandleChannelLockUpdate);

            // Rules Channel & Self-Assign Roles
            packets.Add((int)ServerPackets.SendGroupRulesResponse, GroupRoles_DR.HandleGroupRulesResponse);
            packets.Add((int)ServerPackets.SendRulesAgreementResponse, GroupRoles_DR.HandleRulesAgreementResponse);
            packets.Add((int)ServerPackets.SendGroupRules, GroupRoles_DR.HandleGroupRules);
            packets.Add((int)ServerPackets.SendSelfAssignRoleResponse, GroupRoles_DR.HandleSelfAssignRoleResponse);
            packets.Add((int)ServerPackets.SendSelfAssignRoles, GroupRoles_DR.HandleSelfAssignRoles);
            packets.Add((int)ServerPackets.SendSelfRoleAssignmentResponse, GroupRoles_DR.HandleSelfRoleAssignmentResponse);
            packets.Add((int)ServerPackets.SendRoleChannelPermissionsResponse, GroupRoles_DR.HandleRoleChannelPermissionsResponse);
            packets.Add((int)ServerPackets.SendMemberSelfRoles, GroupRoles_DR.HandleMemberSelfRoles);
            packets.Add((int)ServerPackets.SendCreateChannelError, Groups_DR.HandleCreateChannelError);
            packets.Add((int)ServerPackets.SendRoleSections, GroupRoles_DR.HandleRoleSections);
            packets.Add((int)ServerPackets.SendGroupBans, Groups_DR.HandleGroupBans);
            packets.Add((int)ServerPackets.SendMemberRemovedFromGroup, Groups_DR.HandleMemberRemovedFromGroup);
            packets.Add((int)ServerPackets.SendGroupInfo, Groups_DR.HandleGroupInfo);
            packets.Add((int)ServerPackets.SendProfileInfoEmbed, Groups_DR.HandleProfileInfoEmbed);
            // Form Channel
            packets.Add((int)ServerPackets.SendFormFields, GroupForms_DR.HandleFormFields);
            packets.Add((int)ServerPackets.SendFormSubmissions, GroupForms_DR.HandleFormSubmissions);
            packets.Add((int)ServerPackets.SendFormSubmitResult, GroupForms_DR.HandleFormSubmitResult);
            // Group Search
            packets.Add((int)ServerPackets.SendPublicGroupSearchResults, GroupSearch_DR.HandlePublicGroupSearchResults);
            // Join Requests
            packets.Add((int)ServerPackets.SendJoinRequests, GroupSearch_DR.HandleJoinRequests);
            packets.Add((int)ServerPackets.SendJoinRequestResult, GroupSearch_DR.HandleJoinRequestResult);
            packets.Add((int)ServerPackets.SendJoinRequestNotification, GroupSearch_DR.HandleJoinRequestNotification);

            // Character Sync
            packets.Add((int)ServerPackets.SendVerifiedCharacters, Accounts_DR.HandleVerifiedCharacters);

            // Server Notifications (shutdown, restart, broadcast)
            packets.Add((int)ServerPackets.SendServerNotification, ServerNotifications_DR.HandleServerNotification);

            // Listings System
            packets.Add((int)ServerPackets.SendListingCreated, Listings_DR.HandleListingCreated);
            packets.Add((int)ServerPackets.SendListingUpdated, Listings_DR.HandleListingUpdated);
            packets.Add((int)ServerPackets.SendListingDeleted, Listings_DR.HandleListingDeleted);
            packets.Add((int)ServerPackets.SendListingsList, Listings_DR.HandleListingsList);
            packets.Add((int)ServerPackets.SendListingDetail, Listings_DR.HandleListingDetail);
            packets.Add((int)ServerPackets.SendMyListings, Listings_DR.HandleMyListings);
            packets.Add((int)ServerPackets.SendBookmarkResult, Listings_DR.HandleBookmarkResult);
            packets.Add((int)ServerPackets.SendMenuUpdated, Listings_DR.HandleMenuUpdated);
            packets.Add((int)ServerPackets.SendScheduleUpdated, Listings_DR.HandleScheduleUpdated);
            packets.Add((int)ServerPackets.SendImageUploaded, Listings_DR.HandleImageUploaded);
            packets.Add((int)ServerPackets.SendListingError, Listings_DR.HandleListingError);

            // Booking System
            packets.Add((int)ServerPackets.SendBookingRequestResult, Bookings_DR.HandleBookingRequestResult);
            packets.Add((int)ServerPackets.SendMyBookings, Bookings_DR.HandleMyBookings);
            packets.Add((int)ServerPackets.SendBookingResponseResult, Bookings_DR.HandleBookingResponseResult);
            packets.Add((int)ServerPackets.SendIncomingBookings, Bookings_DR.HandleIncomingBookings);
            packets.Add((int)ServerPackets.SendBookableEntriesSaved, Bookings_DR.HandleBookableEntriesSaved);
            packets.Add((int)ServerPackets.SendBookingNotification, Bookings_DR.HandleBookingNotification);

            // RP Systems
            packets.Add((int)ServerPackets.SendSystemCreated, Systems_DR.HandleSystemCreated);
            packets.Add((int)ServerPackets.SendSystemDeleted, Systems_DR.HandleSystemDeleted);
            packets.Add((int)ServerPackets.SendMySystems, Systems_DR.HandleMySystems);
            packets.Add((int)ServerPackets.SendSystemData, Systems_DR.HandleSystemData);
            packets.Add((int)ServerPackets.SendStatsSaved, Systems_DR.HandleStatsSaved);
            packets.Add((int)ServerPackets.SendCombatConfigSaved, Systems_DR.HandleCombatConfigSaved);
            packets.Add((int)ServerPackets.SendSkillClassesSaved, Systems_DR.HandleSkillClassesSaved);
            packets.Add((int)ServerPackets.SendSkillsSaved, Systems_DR.HandleSkillsSaved);
            packets.Add((int)ServerPackets.SendSystemError, Systems_DR.HandleSystemError);

            // Sheet submission & roster
            packets.Add((int)ServerPackets.SendSubmitSheetResult, Systems_DR.HandleSubmitSheetResult);
            packets.Add((int)ServerPackets.SendSystemRoster, Systems_DR.HandleSystemRoster);
            packets.Add((int)ServerPackets.SendSheetResponse, Systems_DR.HandleSheetResponse);
            packets.Add((int)ServerPackets.SendSystemBans, Systems_DR.HandleSystemBans);

            // Joined systems
            packets.Add((int)ServerPackets.SendJoinedSystems, Systems_DR.HandleJoinedSystems);

            
            packets.Add((int)ServerPackets.SendProfilesByAccountTag, Profiles_DR.HandleProfilesByAccountTag);

            // Equipment
            packets.Add((int)ServerPackets.SendEquipment, Equipment_DR.HandleEquipment);
            packets.Add((int)ServerPackets.SendTargetEquipment, Equipment_DR.HandleTargetEquipment);

            // Social feed
            packets.Add((int)ServerPackets.SSendSocialFeed,             SocialFeed_DR.HandleSendSocialFeed);
            packets.Add((int)ServerPackets.SSocialPostSaved,            SocialFeed_DR.HandleSocialPostSaved);
            packets.Add((int)ServerPackets.SSocialPostDeleted,          SocialFeed_DR.HandleSocialPostDeleted);
            packets.Add((int)ServerPackets.SSendSocialBookmarks,        SocialFeed_DR.HandleSendSocialBookmarks);
            packets.Add((int)ServerPackets.SSendSocialFollows,          SocialFeed_DR.HandleSendSocialFollows);
            packets.Add((int)ServerPackets.SSocialFollowChanged,        SocialFeed_DR.HandleSocialFollowChanged);
            packets.Add((int)ServerPackets.SSocialBookmarkChanged,      SocialFeed_DR.HandleSocialBookmarkChanged);
            packets.Add((int)ServerPackets.SSendSocialNotifications,    SocialFeed_DR.HandleSendSocialNotifications);
            packets.Add((int)ServerPackets.SSocialNotificationPush,     SocialFeed_DR.HandleSocialNotificationPush);
            packets.Add((int)ServerPackets.SSendSocialComments,         SocialFeed_DR.HandleSendSocialComments);
            packets.Add((int)ServerPackets.SSocialCommentSaved,         SocialFeed_DR.HandleSocialCommentSaved);
            packets.Add((int)ServerPackets.SSocialCommentDeleted,       SocialFeed_DR.HandleSocialCommentDeleted);
            packets.Add((int)ServerPackets.SSocialLikeChanged,          SocialFeed_DR.HandleSocialLikeChanged);
            packets.Add((int)ServerPackets.SSocialRepostChanged,        SocialFeed_DR.HandleSocialRepostChanged);
            packets.Add((int)ServerPackets.SSocialMediaUploaded,        SocialFeed_DR.HandleSocialMediaUploaded);
            packets.Add((int)ServerPackets.SendPostsByAuthor,           SocialFeed_DR.HandleSendPostsByAuthor);
            packets.Add((int)ServerPackets.SSocialPostReported,         SocialFeed_DR.HandlePostReported);

            // Profile links (relationship bonds)
            packets.Add((int)ServerPackets.SendRelationshipsList,           Relationships_DR.HandleRelationshipsList);
            packets.Add((int)ServerPackets.SendRelationshipUpdate,          Relationships_DR.HandleRelationshipUpdate);
            packets.Add((int)ServerPackets.SendIncomingRelationshipRequest, Relationships_DR.HandleIncomingRelationshipRequest);
            packets.Add((int)ServerPackets.SProfileAvatar,                  Relationships_DR.HandleProfileAvatar);

            // Account profile
            packets.Add((int)ServerPackets.SendAccountProfile, Accounts_DR.HandleSendAccountProfile);

            // Immersive theme gallery.
            packets.Add((int)ServerPackets.SThemeList,     ThemeNetwork.HandleList);
            packets.Add((int)ServerPackets.SThemeDocument, ThemeNetwork.HandleDocument);
            packets.Add((int)ServerPackets.SThemeSaved,    ThemeNetwork.HandleSaved);
            packets.Add((int)ServerPackets.SThemeResult,   ThemeNetwork.HandleResult);
            packets.Add((int)ServerPackets.SThemeVersions, ThemeNetwork.HandleVersions);

            // simple message back from server, simply for verification that the user is connected
        }

        // Entry point for all incoming server data. Accumulates raw bytes into playerBuffer, then loops extracting complete packets. Each packet is length-prefixed (4-byte int) followed by that many bytes of payload. Handles partial reads gracefully by keeping leftover bytes in the buffer until the next call delivers the rest.
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

        // Extracts the packet ID (first 4 bytes) from a complete packet payload, looks it up in the dispatch table, and invokes the matching handler. The full payload (including the ID) is passed to the handler because each handler re-reads the ID and then continues parsing its own fields.
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
