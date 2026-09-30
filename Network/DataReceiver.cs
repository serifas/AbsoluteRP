namespace Networking
{
    // Enum of every packet the server can send to this client. Each value must match the server-side packet ID exactly, otherwise the client will either ignore the packet or route it to the wrong handler. Grouped by feature area (core, groups, listings, RP systems, etc.).
    public enum ServerPackets
    {
        // Profile links (relationship bonds)
        SendRelationshipsList = 290,
        SendRelationshipUpdate = 291,
        SendIncomingRelationshipRequest = 292,
        SProfileAvatar = 346,
        SWelcomeMessage = 1,
        SRecLoginStatus = 2,
        SRecAccPermissions = 3,
        SRecProfileBio = 4,
        SRecExistingProfile = 5,
        SSendProfile = 20,
        SDoneSending = 21,
        SNoProfileBio = 22,
        SNoProfile = 23,
        SSendProfileHook = 24,
        SSendNoProfileHooks = 25,
        SRecNoTargetHooks = 26,
        SRecNoTargetBio = 27,
        SRecTargetHooks = 28,
        SRecTargetBio = 29,
        SRecTargetProfile = 30,
        SRecNoTargetProfile = 31,
        SRecProfileStory = 32,
        SRecTargetStory = 33,
        SRecBookmarks = 34,
        SRecNoTargetStory = 35,
        SRecNoProfileStory = 36,
        SRecProfileGallery = 37,
        SRecGalleryImageLoaded = 38,
        SRecImageDeletionStatus = 39,
        SRecNoTargetGallery = 40,
        SRecTargetGallery = 41,
        SRecNoProfileGallery = 42,
        CProfileAlreadyReported = 43,
        CProfileReportedSuccessfully = 44,
        SSendProfileNotes = 45,
        SSendNoProfileNotes = 46,
        SSendNoAuthorization = 47,
        SSendVerificationMessage = 48,
        SSendVerified = 49,
        SSendPasswordModificationForm = 50,
        SSendOOC = 51,
        SSendTargetOOC = 52,
        SSendNoOOCInfo = 53,
        SSendNoTargetOOCInfo = 54,
        ReceiveConnections = 55,
        ReceiveNewConnectionRequest = 56,
        ReceiveChatMessage = 57,
        ReceiveGroupMemberships = 58,
        RecieveTargetTooltip = 59,
        ReceiveProfiles = 60,
        CreateItem = 61,
        ReceiveProfileWarning = 62,
        ReceiveProfileSettings = 63,
        ReceiveProfileListings = 64,
        ReceiveProfileDetails = 65,
        ReceiveReloadProfiles = 66,
        ReceiveTabCount = 67,
        ReceiveGalleryTab = 68,
        ReceiveInfoTab = 69,
        ReceiveTabsUpdate = 70,
        ReceiveInventoryTab = 71,
        ReceiveDynamicTab = 72,
        ReceiveRelationshipsTab = 73,
        ReceiveSingleTab = 74,
        ReceiveTradeRequest = 75,
        ReceiveTradeUpdate = 76,
        ReceiveTradeStatus = 77,
        ReceiveTradeInventory = 78,
        ReceiveTreeLayout = 79,
        RecConnectedPlayersInMap = 80,
        ReceiveGroup = 81,
        SendGroupChatMessages = 83,
        SendGroupChatMessageBroadcast = 84,
        SendGroupCategories = 85,
        SendGroupRosterFields = 86,
        SendMemberMetadata = 87,
        SendMemberFieldValues = 88,
        SendChatMessageDeleted = 89,
        SendChatMessageEdited = 90,
        SendGroupInvites = 91,
        SendGroupInviteResult = 92,
        SendGroupMembers = 93,
        SendForumStructure = 94,
        SendForumPermissions = 95,
        SendInviteNotification = 96,
        SendInviteeProfile = 97,
        SendGroupRanks = 98,
        SendRankOperationResult = 99,
        SendGroupMemberAvatar = 100,
        SendLikesRemaining = 101,
        SendLikeResult = 102,
        SendProfileLikeCounts = 103,
        SendProfileLikes = 104,
        SendPinnedMessages = 105,
        SendMessagePinResult = 106,
        SendMessagePinUpdate = 107,
        SendChannelLockUpdate = 108,
        // Rules Channel & Self-Assign Roles
        SendGroupRulesResponse = 109,
        SendRulesAgreementResponse = 110,
        SendGroupRules = 111,
        SendSelfAssignRoleResponse = 112,
        SendSelfAssignRoles = 113,
        SendSelfRoleAssignmentResponse = 114,
        SendRoleChannelPermissionsResponse = 115,
        SendMemberSelfRoles = 116,
        SendCreateChannelError = 117,
        SendRoleSections = 118,
        SendGroupBans = 119,
        SendMemberRemovedFromGroup = 120,
        SendGroupInfo = 121,
        SendProfileInfoEmbed = 122,
        // Form Channel
        SendFormFields = 123,
        SendFormSubmissions = 124,
        SendFormSubmitResult = 125,
        // Group Search
        SendPublicGroupSearchResults = 126,
        // Join Requests
        SendJoinRequests = 127,
        SendJoinRequestResult = 128,
        SendJoinRequestNotification = 129,
        // Character Sync
        SendVerifiedCharacters = 141,

        // Server Notifications (shutdown, restart, broadcast)
        SendServerNotification = 130,

        // Equipment
        SendEquipment = 160,
        SendTargetEquipment = 161,

        // Listings System
        SendListingCreated = 185,
        SendListingUpdated = 186,
        SendListingDeleted = 187,
        SendListingsList = 188,
        SendListingDetail = 189,
        SendMyListings = 190,
        SendBookmarkResult = 191,
        SendStaffInviteSent = 192,
        SendStaffInviteAccepted = 193,
        SendStaffRemoved = 194,
        SendMenuUpdated = 195,
        SendServicesUpdated = 196,
        SendRosterUpdated = 197,
        SendRSVPResult = 198,
        SendListingRSVPs = 199,
        SendScheduleUpdated = 200,
        SendImageUploaded = 201,
        SendSettingsUpdated = 202,
        SendListingError = 203,

        // Social feed
        SSendSocialFeed = 300,
        SSocialPostSaved = 301,
        SSocialPostDeleted = 302,
        SSendSocialBookmarks = 303,
        SSendSocialFollows = 304,
        SSocialFollowChanged = 305,
        SSocialBookmarkChanged = 306,
        SSendSocialNotifications = 307,
        SSocialNotificationPush = 308,
        SSendSocialComments = 309,
        SSocialCommentSaved = 310,
        SSocialCommentDeleted = 311,
        SSocialLikeChanged = 312,
        SSocialRepostChanged = 313,
        SSocialMediaUploaded = 314,

        // Booking System
        SendBookingRequestResult = 220,
        SendMyBookings = 221,
        SendBookingResponseResult = 222,
        SendIncomingBookings = 223,
        SendBookableEntriesSaved = 224,
        SendBookingNotification = 225,

        // RP Systems
        SendSystemCreated = 240,
        SendSystemUpdated = 241,
        SendSystemDeleted = 242,
        SendSystemData = 243,
        SendMySystems = 244,
        SendImportSystemResult = 245,
        SendStatsSaved = 246,
        SendResourcesSaved = 247,
        SendCombatConfigSaved = 248,
        SendSkillClassesSaved = 249,
        SendSkillsSaved = 250,
        SendSkillConnectionsSaved = 251,
        SendCharacterSheetSaved = 252,
        SendCharacterSheet = 253,
        SendCharacterSheets = 254,
        SendSystemError = 255,

        // Systems Phase 2 - Sheet submission, roster, public fetch
        SendSubmitSheetResult = 260,
        SendSystemRoster = 261,
        SendSheetResponse = 262,
        SendPublicSystemData = 263,
        SendSystemBans = 268,
        SendJoinedSystems = 273,
        SendProfilesByAccountTag = 274,

        // Account profile (display name / avatar / gender / age / RP prefs)
        SendAccountProfile = 320,

        // Posts authored by a specific user (drives the profile popup's post list).
        SendPostsByAuthor = 322,

        // Immersive theme gallery - see Immersive/Themes/ThemeNetwork.cs.
        SThemeList     = 340,
        SThemeDocument = 341,
        SThemeSaved    = 342,
        SThemeResult   = 343,
        SThemeVersions = 344,
        SSocialPostReported = 345,   // int postId, bool ok, string message
    }
    // Contains all packet handler methods that process data received from the server. Every handler follows the same pattern: 1. Write the raw byte[] into a ByteBuffer 2. Read the packet ID (to advance past it - it was already used for dispatch) 3. Read the remaining fields in the exact order the server wrote them 4. Update the appropriate UI window state with the new data. Most handlers are static because they update static UI state shared across windows.
    class DataReceiver
    {
        // Safety limits to prevent crashes from corrupt or oversized server data
        internal const int MaxImageBytes = 10 * 1024 * 1024; // 10MB max per image
        internal const int MaxItemCount = 500; // max items in any list

        // Reads an item count from the buffer, clamped to safe bounds. Returns 0 for negative or excessively large values to avoid allocating huge arrays from corrupted data.
        internal static int SafeReadCount(ByteBuffer buffer)
        {
            int count = buffer.ReadInt();
            if (count < 0) count = 0;
            if (count > MaxItemCount) count = 0;
            return count;
        }

        // Reads a length-prefixed byte array (like an image) with safety checks. Returns null if the length is out of bounds or the read fails.
        internal static byte[] SafeReadBytes(ByteBuffer buffer)
        {
            int len = buffer.ReadInt();
            if (len <= 0 || len > MaxImageBytes) return null;
            try { return buffer.ReadBytes(len); }
            catch { return null; }
        }
        public static SortedList<int, string> pages = new SortedList<int, string>();
        public static SortedList<string, string> pagesContent = new SortedList<string, string>();
    }
}