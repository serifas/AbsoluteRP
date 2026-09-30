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
    // Group, membership, rank and invite packets. Split out of DataReceiver.
    internal class Groups_DR
    {
        // Shared state used by group-related handlers
        public static List<GroupCategory> categories = new List<GroupCategory>();

        public static List<GroupRosterField> rosterFields = new List<GroupRosterField>();
        public static List<GroupInvite> invites = new List<GroupInvite>();
        public static List<GroupMember> members = new List<GroupMember>();
        public static List<GroupForumCategory> forumStructure = new List<GroupForumCategory>();
        public static List<GroupForumChannelPermission> forumPermissions = new List<GroupForumChannelPermission>();
        public static List<GroupRank> ranks = new List<GroupRank>();
        public static Dictionary<int, Dictionary<string, string>> memberMetadata = new Dictionary<int, Dictionary<string, string>>(); // member ID -> key/value metadata pairs
        public static Dictionary<int, Dictionary<int, string>> memberFieldValues = new Dictionary<int, Dictionary<int, string>>(); // member ID -> field ID/value pairs
        public static string rankOperationMessage = string.Empty;
        public static bool rankOperationSuccess = false;
        public static string createChannelError = string.Empty;

        // Receives all groups the current user belongs to. For each group, loads its logo image asynchronously and adds it to the groups list. Also caches group info for embed display even after leaving a group.
        public static async void ReceiveGroupMemberships(byte[] data)
        {
            try
            {
                GroupsData.groups.Clear();
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int groupCount = buffer.ReadInt();
                    Plugin.PluginLog.Info($"[ReceiveGroupMemberships] Receiving {groupCount} groups");
                    for (int i = 0; i < groupCount; i++)
                    {
                        try
                        {
                            int id = buffer.ReadInt();
                            string name = buffer.ReadString();
                            string logoURL = buffer.ReadString();
                            string imgURL = buffer.ReadString();
                            int profileID = buffer.ReadInt();
                            bool visible = buffer.ReadBool();
                            bool openInvite = buffer.ReadBool();
                            bool canInvite = buffer.ReadBool();
                            Plugin.PluginLog.Info($"[ReceiveGroupMemberships] Group {i+1}/{groupCount}: id={id}, name='{name}', profileID={profileID}, visible={visible}, openInvite={openInvite}, canInvite={canInvite}");

                            // Try to load logo image, but don't fail the whole group if it fails
                            IDalamudTextureWrap logoImg = null;
                            if (!string.IsNullOrEmpty(logoURL))
                            {
                                try
                                {
                                    byte[] logoBytes = await Imaging.FetchUrlImageBytes(logoURL);
                                    if (logoBytes != null && logoBytes.Length > 4)
                                    {
                                        logoImg = await Plugin.TextureProvider.CreateFromImageAsync(logoBytes);
                                    }
                                }
                                catch (Exception imgEx)
                                {
                                    Plugin.PluginLog.Debug($"[ReceiveGroupMemberships] Logo load failed for group {id}: {imgEx.Message}");
                                }
                            }
                            if (logoImg == null)
                            {
                                logoImg = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                            }

                            Group group = new Group()
                            {
                                groupID = id,
                                name = name,
                                logo = logoImg,
                                logoUrl = logoURL,
                                visible = visible,
                                openInvite = openInvite,
                                canInvite = canInvite,
                                ProfileData = profileID > 0 ? new ProfileData { id = profileID } : null
                            };
                            GroupsData.groups.Add(group);
                            // Cache the group info for embed display even after leaving the group
                            GroupsData.CacheGroupInfo(id, name, logoURL, logoImg);
                        }
                        catch (Exception groupEx)
                        {
                            Plugin.PluginLog.Error($"[ReceiveGroupMemberships] Failed to parse group {i+1}/{groupCount}: {groupEx.Message}");
                        }
                    }
                    // Clear any pending join requests for groups we're now a member of
                    AbsoluteRP.Windows.Social.Views.GroupsData.ClearPendingJoinRequests();
                    _ = Task.Run(async () => { try { await Plugin.plugin.UpdateStatusAsync(); } catch { } });
                    // Handle the message as needed
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveGroupMemberships message: {ex}");
            }
        }

        internal static void ReceiveGroup(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();

                    // Server writes a presence bool first
                    bool hasGroup = buffer.ReadBool();
                    if (!hasGroup)
                        return;

                    // Basic group data (matches server ReceiveGroup layout)
                    int groupID = buffer.ReadInt();
                    string name = buffer.ReadString();
                    string description = buffer.ReadString();
                    string logoURL = buffer.ReadString();
                    string backgroundURL = buffer.ReadString();
                    bool openInvite = buffer.ReadBool();
                    bool visible = buffer.ReadBool();
                    int profileID = buffer.ReadInt();

                    // Ranks
                    int rankCount = buffer.ReadInt();
                    var ranks = new List<object>(); // server sends full rank objects; client can expand later
                    for (int i = 0; i < rankCount; i++)
                    {
                        int rid = buffer.ReadInt();
                        string rname = buffer.ReadString();
                        string rdesc = buffer.ReadString();
                        int permCount = buffer.ReadInt();
                        var perms = new List<(int, bool, bool, bool, bool, bool)>();
                        for (int p = 0; p < permCount; p++)
                        {
                            int prank = buffer.ReadInt();
                            bool canAnn = buffer.ReadBool();
                            bool canWarn = false;
                            try { canWarn = buffer.ReadBool(); } catch { canWarn = false; } // fallbacks
                            bool canStrike = buffer.ReadBool();
                            bool canSuspend = buffer.ReadBool();
                            bool canBan = buffer.ReadBool();
                            bool canPromote = false;
                            try { canPromote = buffer.ReadBool(); } catch { canPromote = false; }

                            perms.Add((prank, canAnn, canWarn, canStrike, canSuspend, canBan));
                        }
                        ranks.Add(new { id = rid, name = rname, description = rdesc, perms = perms });
                    }

                    // Members
                    int memberCount = buffer.ReadInt();
                    var members = new List<object>();
                    for (int i = 0; i < memberCount; i++)
                    {
                        int mid = buffer.ReadInt();
                        int mprofileID = buffer.ReadInt();
                        bool owner = buffer.ReadBool();
                        string mname = buffer.ReadString();
                        string mnote = buffer.ReadString();
                        int mrankId = buffer.ReadInt();
                        int avatarLen = buffer.ReadInt();
                        byte[] avatarBytes = buffer.ReadBytes(avatarLen);
                        IDalamudTextureWrap avatar = null;
                        var memberEntry = new { id = mid, profileID = mprofileID, owner = owner, name = mname, note = mnote, rankID = mrankId, avatar = avatar };
                        members.Add(memberEntry);
                        // Load avatar in background
                        var capturedBytes = avatarBytes;
                        var capturedIdx = members.Count - 1;
                        _ = Task.Run(async () => { try { var tex = await Plugin.TextureProvider.CreateFromImageAsync(capturedBytes); /* avatar loaded async */ } catch { } });
                    }

                    // Bans
                    int banCount = buffer.ReadInt();
                    var bansList = new List<GroupBans>();
                    for (int i = 0; i < banCount; i++)
                    {
                        int banId = buffer.ReadInt();
                        int banUserID = buffer.ReadInt();
                        int banProfileID = buffer.ReadInt();
                        string banName = buffer.ReadString();
                        string lodestone = buffer.ReadString();
                        bansList.Add(new GroupBans
                        {
                            id = banId,
                            userID = banUserID,
                            profileID = banProfileID,
                            name = banName,
                            lodestoneURL = lodestone
                        });
                    }

                    // Application
                    object application = null;
                    bool hasApp = buffer.ReadBool();
                    if (hasApp)
                    {
                        int appId = buffer.ReadInt();
                        string appName = buffer.ReadString();
                        int secCount = buffer.ReadInt();
                        var sections = new List<object>();
                        for (int s = 0; s < secCount; s++)
                        {
                            int sIndex = buffer.ReadInt();
                            string sName = buffer.ReadString();
                            string sDesc = buffer.ReadString();
                            int inputsCount = buffer.ReadInt();
                            var inputs = new List<object>();
                            for (int inp = 0; inp < inputsCount; inp++)
                            {
                                int idx = buffer.ReadInt();
                                int type = buffer.ReadInt();
                                string inName = buffer.ReadString();
                                string inDesc = buffer.ReadString();
                                inputs.Add(new { index = idx, type = type, name = inName, description = inDesc });
                            }
                            sections.Add(new { index = sIndex, name = sName, description = sDesc, inputs = inputs });
                        }
                        application = new { id = appId, name = appName, sections = sections };
                    }

                    // Channels
                    int channelCount = buffer.ReadInt();
                    var channels = new List<object>();
                    for (int i = 0; i < channelCount; i++)
                    {
                        int cid = buffer.ReadInt();
                        int cindex = buffer.ReadInt();
                        string cname = buffer.ReadString();
                        string cdesc = buffer.ReadString();

                        int allowedMembersCount = buffer.ReadInt();
                        var allowedMembers = new List<int>();
                        for (int am = 0; am < allowedMembersCount; am++)
                            allowedMembers.Add(buffer.ReadInt());

                        int allowedRanksCount = buffer.ReadInt();
                        var allowedRanks = new List<int>();
                        for (int ar = 0; ar < allowedRanksCount; ar++)
                            allowedRanks.Add(buffer.ReadInt());

                        channels.Add(new { id = cid, index = cindex, name = cname, description = cdesc, allowedMembers, allowedRanks });
                    }

                    // Build client Group model immediately with null textures, load images in background
                    Group group = new Group()
                    {
                        groupID = groupID,
                        name = name ?? string.Empty,
                        description = description ?? string.Empty,
                        openInvite = openInvite,
                        visible = visible,
                        logo = null,
                        background = null,
                        ProfileData = new ProfileData() { id = profileID },
                        bans = bansList,
                    };

                    // Load logo and background images in parallel background tasks
                    var capturedGroup = group;
                    if (!string.IsNullOrEmpty(backgroundURL))
                    {
                        var bgUrl = backgroundURL;
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var bgBytes = await Imaging.FetchUrlImageBytes(bgUrl);
                                if (bgBytes != null && bgBytes.Length > 0)
                                    capturedGroup.background = await Plugin.TextureProvider.CreateFromImageAsync(bgBytes);
                            }
                            catch (Exception ex) { Plugin.PluginLog.Debug($"ReceiveGroup: background load failed: {ex.Message}"); }
                        });
                    }
                    if (!string.IsNullOrEmpty(logoURL))
                    {
                        var lUrl = logoURL;
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var logoBytes = await Imaging.FetchUrlImageBytes(lUrl);
                                if (logoBytes != null && logoBytes.Length > 0)
                                    capturedGroup.logo = await Plugin.TextureProvider.CreateFromImageAsync(logoBytes);
                            }
                            catch (Exception ex) { Plugin.PluginLog.Debug($"ReceiveGroup: logo load failed: {ex.Message}"); }
                        });
                    }

                    GroupsData.groups.Add(group);

                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveGroup message: {ex}");
            }
        }

        public static void HandleInviteNotification(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int inviteID = buffer.ReadInt();
                int groupID = buffer.ReadInt();
                string groupName = buffer.ReadString();
                string groupLogoUrl = buffer.ReadString();
                int inviterUserID = buffer.ReadInt();
                string inviterName = buffer.ReadString();
                string message = buffer.ReadString();
                long invitedAt = buffer.ReadLong();

                buffer.Dispose();

                Plugin.PluginLog.Info($"Received invite notification from {inviterName} for group {groupName} (ID: {groupID})");

                // Create a GroupInvite object for the notification window
                var invite = new GroupInvite
                {
                    inviteID = inviteID,
                    groupID = groupID,
                    groupName = groupName,
                    groupLogoUrl = groupLogoUrl,
                    inviterUserID = inviterUserID,
                    inviterName = inviterName,
                    message = message,
                    createdAt = invitedAt,
                    status = 0 // pending
                };

                // Show the notification to the user
                GroupInviteNotification.AddInvite(invite);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleInviteNotification: {ex.Message}");
            }
        }

        public static async void HandleGroupMemberAvatar(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int userID = buffer.ReadInt();
                bool hasAvatar = buffer.ReadBool();

                Plugin.PluginLog.Info($"[HandleGroupMemberAvatar] Received avatar for userID {userID}, hasAvatar: {hasAvatar}");

                if (hasAvatar)
                {
                    int avatarLength = buffer.ReadInt();
                    byte[] avatarBytes = buffer.ReadBytes(avatarLength);

                    Plugin.PluginLog.Info($"[HandleGroupMemberAvatar] Avatar bytes length: {avatarLength}");

                    // Call Groups.OnAvatarReceived to update messages
                    AbsoluteRP.Windows.Social.Views.GroupsData.OnAvatarReceived(userID, avatarBytes);
                }
                else
                {
                    Plugin.PluginLog.Warning($"[HandleGroupMemberAvatar] No avatar data for userID {userID}");
                }

                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleGroupMemberAvatar: {ex.Message}");
            }
        }

        public static void HandleFetchGroupCategories(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int categoryCount = buffer.ReadInt();

                List<GroupCategory> categories = new List<GroupCategory>();
                for (int i = 0; i < categoryCount; i++)
                {
                    var category = new GroupCategory
                    {
                        id = buffer.ReadInt(),
                        groupID = buffer.ReadInt(),
                        sortOrder = buffer.ReadInt(),
                        name = buffer.ReadString(),
                        description = buffer.ReadString(),
                        collapsed = buffer.ReadBool()
                    };

                    // Read channels for this category
                    int channelCount = buffer.ReadInt();
                    category.channels = new List<GroupChannel>();
                    for (int j = 0; j < channelCount; j++)
                    {
                        int channelId = buffer.ReadInt();
                        int channelGroupId = buffer.ReadInt();
                        int channelCategoryId = buffer.ReadInt();
                        int channelIndex = buffer.ReadInt();
                        string channelName = buffer.ReadString();
                        string channelDescription = buffer.ReadString();
                        int channelChannelType = buffer.ReadInt();
                        bool channelIsLocked = buffer.ReadBool();
                        bool channelIsNsfw = buffer.ReadBool();
                        bool channelEveryoneCanView = buffer.ReadBool();
                        bool channelEveryoneCanPost = buffer.ReadBool();

                        // Read member permissions
                        int memberPermCount = buffer.ReadInt();
                        var memberPermissions = new List<ChannelMemberPermission>();
                        for (int mp = 0; mp < memberPermCount; mp++)
                        {
                            memberPermissions.Add(new ChannelMemberPermission
                            {
                                memberID = buffer.ReadInt(),
                                memberName = buffer.ReadString(),
                                canView = buffer.ReadBool(),
                                canPost = buffer.ReadBool()
                            });
                        }

                        // Read rank permissions
                        int rankPermCount = buffer.ReadInt();
                        var rankPermissions = new List<ChannelRankPermission>();
                        for (int rp = 0; rp < rankPermCount; rp++)
                        {
                            rankPermissions.Add(new ChannelRankPermission
                            {
                                rankID = buffer.ReadInt(),
                                rankName = buffer.ReadString(),
                                canView = buffer.ReadBool(),
                                canPost = buffer.ReadBool()
                            });
                        }

                        // Read role permissions
                        int rolePermCount = buffer.ReadInt();
                        var rolePermissions = new List<ChannelRolePermission>();
                        for (int rolep = 0; rolep < rolePermCount; rolep++)
                        {
                            rolePermissions.Add(new ChannelRolePermission
                            {
                                roleID = buffer.ReadInt(),
                                roleName = buffer.ReadString(),
                                roleColor = buffer.ReadString(),
                                canView = buffer.ReadBool(),
                                canPost = buffer.ReadBool()
                            });
                        }

                        var channel = new GroupChannel
                        {
                            id = channelId,
                            groupID = channelGroupId,
                            categoryID = channelCategoryId,
                            index = channelIndex,
                            name = channelName,
                            description = channelDescription,
                            channelType = channelChannelType,
                            isLocked = channelIsLocked,
                            isNsfw = channelIsNsfw,
                            everyoneCanView = channelEveryoneCanView,
                            everyoneCanPost = channelEveryoneCanPost,
                            MemberPermissions = memberPermissions,
                            RankPermissions = rankPermissions,
                            RolePermissions = rolePermissions
                        };
                        Plugin.PluginLog.Info($"[HandleFetchGroupCategories] Channel '{channelName}' (id={channelId}) everyoneCanView={channelEveryoneCanView} everyoneCanPost={channelEveryoneCanPost} members={memberPermCount} ranks={rankPermCount} roles={rolePermCount}");
                        category.channels.Add(channel);
                    }

                    categories.Add(category);
                }

                Plugin.PluginLog.Info($"Received {categories.Count} categories with channels from server");
                Groups_DR.categories = categories;

                // Update the current group's categories if it exists
                if (AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup != null && categories.Count > 0)
                {
                    int groupID = categories[0].groupID;
                    if (AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.groupID == groupID)
                    {
                        AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.categories = categories;
                        Plugin.PluginLog.Info($"Updated currentGroup.categories with {categories.Count} categories");
                    }
                }

                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleFetchGroupCategories: {ex.Message}");
            }
        }

        public static async void HandleSaveGroupRosterFields(int connectionID, byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                bool success = buffer.ReadBool();
                string message = buffer.ReadString();
                buffer.Dispose();

                if (success)
                {
                    Plugin.PluginLog.Info("Roster fields saved successfully");
                }
                else
                {
                    Plugin.PluginLog.Debug($"Failed to save roster fields: {message}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleSaveGroupRosterFields: {ex.Message}");
            }
        }

        public static void HandleFetchGroupRosterFields(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int groupID = buffer.ReadInt();
                int fieldCount = buffer.ReadInt();

                List<GroupRosterField> fields = new List<GroupRosterField>();
                for (int i = 0; i < fieldCount; i++)
                {
                    var field = new GroupRosterField
                    {
                        id = buffer.ReadInt(),
                        groupID = groupID,
                        name = buffer.ReadString(),
                        fieldType = buffer.ReadInt(),
                        required = buffer.ReadBool(),
                        sortOrder = buffer.ReadInt()
                    };
                    fields.Add(field);
                }
                buffer.Dispose();

                // Update the group manager with roster fields
                // TODO: Implement GroupManager
                // Plugin.GroupManager?.UpdateRosterFields(groupID, fields);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleFetchGroupRosterFields: {ex.Message}");
            }
        }

        public static async void HandleSaveMemberMetadata(int connectionID, byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                bool success = buffer.ReadBool();
                string message = buffer.ReadString();
                buffer.Dispose();

                if (success)
                {
                    Plugin.PluginLog.Info("Member metadata saved successfully");
                }
                else
                {
                    Plugin.PluginLog.Debug($"Failed to save member metadata: {message}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleSaveMemberMetadata: {ex.Message}");
            }
        }

        public static void HandleFetchMemberMetadata(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int groupID = buffer.ReadInt();
                int memberID = buffer.ReadInt();
                int metadataCount = buffer.ReadInt();

                Dictionary<string, string> metadata = new Dictionary<string, string>();
                for (int i = 0; i < metadataCount; i++)
                {
                    string key = buffer.ReadString();
                    string value = buffer.ReadString();
                    metadata[key] = value;
                }
                buffer.Dispose();

                // Update member metadata in the group manager
                // TODO: Implement GroupManager
                // Plugin.GroupManager?.UpdateMemberMetadata(groupID, memberID, metadata);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleFetchMemberMetadata: {ex.Message}");
            }
        }

        public static async void HandleSaveMemberFieldValues(int connectionID, byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                bool success = buffer.ReadBool();
                string message = buffer.ReadString();
                buffer.Dispose();

                if (success)
                {
                    Plugin.PluginLog.Info("Member field values saved successfully");
                }
                else
                {
                    Plugin.PluginLog.Debug($"Failed to save member field values: {message}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleSaveMemberFieldValues: {ex.Message}");
            }
        }

        public static void HandleFetchMemberFieldValues(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int groupID = buffer.ReadInt();
                int memberID = buffer.ReadInt();
                int valueCount = buffer.ReadInt();

                Dictionary<int, string> fieldValues = new Dictionary<int, string>();
                for (int i = 0; i < valueCount; i++)
                {
                    int fieldID = buffer.ReadInt();
                    string value = buffer.ReadString();
                    fieldValues[fieldID] = value;
                }
                buffer.Dispose();

                // Update member field values in the group manager
                // TODO: Implement GroupManager
                // Plugin.GroupManager?.UpdateMemberFieldValues(groupID, memberID, fieldValues);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleFetchMemberFieldValues: {ex.Message}");
            }
        }

        public static void HandleGroupInviteResult(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                bool success = buffer.ReadBool();
                string message = buffer.ReadString();
                buffer.Dispose();

                if (success)
                {
                    Plugin.PluginLog.Info($"Group invite: {message}");
                }
                else
                {
                    Plugin.PluginLog.Debug($"Group invite failed: {message}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleGroupInviteResult: {ex.Message}");
            }
        }

        public static void HandleGroupInvites(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int inviteCount = buffer.ReadInt();

                List<GroupInvite> invites = new List<GroupInvite>();
                for (int i = 0; i < inviteCount; i++)
                {
                    var invite = new GroupInvite
                    {
                        inviteID = buffer.ReadInt(),
                        groupID = buffer.ReadInt(),
                        groupName = buffer.ReadString(),
                        groupDescription = buffer.ReadString(),
                        inviterUserID = buffer.ReadInt(),
                        inviterName = buffer.ReadString(),
                        inviteeUserID = buffer.ReadInt(),
                        inviteeName = buffer.ReadString(),
                        inviteeProfileID = buffer.ReadInt(),
                        message = buffer.ReadString(),
                        status = buffer.ReadByte(),
                        createdAt = buffer.ReadLong(),
                        groupLogoUrl = buffer.ReadString()
                    };

                    invites.Add(invite);

                    // Add pending invites to notification window only if I am the invitee
                    if (invite.status == 0 && invite.inviteeUserID == Accounts_DS.userID) // 0 = pending
                    {
                        GroupInviteNotification.AddInvite(invite);
                    }
                }

                // Only open the notification window if there are pending invites for me
                if (GroupInviteNotification.GetPendingInviteCount() > 0)
                {
                    Plugin.groupInviteNotification.IsOpen = true;
                }
                // Update currentGroup's invites list if it exists
                if (GroupsData.currentGroup != null && invites.Count > 0)
                {
                    // Check if these invites belong to the current group
                    int firstInviteGroupID = invites[0].groupID;
                    if (firstInviteGroupID == GroupsData.currentGroup.groupID)
                    {
                        GroupsData.currentGroup.invites = invites;
                        Plugin.PluginLog.Info($"Updated currentGroup.invites with {invites.Count} invites");
                    }
                }

                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleGroupInvites: {ex.Message}");
            }
        }

        // Receives the full member list for a group, including each member's name, world, rank IDs, join date, and online status.
        public static void HandleGroupMembers(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int memberCount = buffer.ReadInt(); // Server sends member count, NOT groupID

                List<GroupMember> members = new List<GroupMember>();
                for (int i = 0; i < memberCount; i++)
                {
                    var member = new GroupMember
                    {
                        id = buffer.ReadInt(),
                        userID = buffer.ReadInt(), // Read userID (was missing)
                        profileID = buffer.ReadInt(),
                        owner = buffer.ReadBool(),
                        name = buffer.ReadString(),
                        note = buffer.ReadString()
                    };

                    // Read legacy single rank (optional)
                    bool hasRank = buffer.ReadBool();
                    if (hasRank)
                    {
                        member.rank = new GroupRank
                        {
                            id = buffer.ReadInt(),
                            name = buffer.ReadString()
                        };
                    }

                    // Initialize ranks list
                    member.ranks = new List<GroupRank>();

                    // Read multiple ranks (new format)
                    // Server sends: int rankCount, then for each rank: int id, string name, int hierarchy
                    int rankCount = buffer.ReadInt();
                    for (int r = 0; r < rankCount; r++)
                    {
                        var rank = new GroupRank
                        {
                            id = buffer.ReadInt(),
                            name = buffer.ReadString(),
                            hierarchy = buffer.ReadInt()
                        };
                        member.ranks.Add(rank);
                    }

                    // If no multiple ranks but has legacy rank, add it to the list
                    if (member.ranks.Count == 0 && member.rank != null)
                    {
                        member.ranks.Add(member.rank);
                    }

                    // Read avatar (optional)
                    bool hasAvatar = buffer.ReadBool();
                    if (hasAvatar)
                    {
                        int avatarLength = buffer.ReadInt();
                        byte[] avatarBytes = buffer.ReadBytes(avatarLength);

                        // Convert avatar bytes to texture
                        if (avatarBytes != null && avatarBytes.Length > 0)
                        {
                            try
                            {
                                var capturedMember = member;
                                var capturedMemberAvBytes = avatarBytes;
                                _ = Task.Run(async () => { try { capturedMember.avatar = await Plugin.TextureProvider.CreateFromImageAsync(capturedMemberAvBytes); } catch { } });
                            }
                            catch (Exception ex)
                            {
                                Plugin.PluginLog.Debug($"[HandleGroupMembers] Failed to load avatar for member {member.name}: {ex.Message}");
                            }
                        }
                    }

                    // Read self-assigned roles
                    int selfRoleCount = buffer.ReadInt();
                    member.selfAssignedRoles = new List<GroupSelfAssignRole>();
                    for (int r = 0; r < selfRoleCount; r++)
                    {
                        member.selfAssignedRoles.Add(new GroupSelfAssignRole
                        {
                            id = buffer.ReadInt(),
                            name = buffer.ReadString(),
                            color = buffer.ReadString()
                        });
                    }

                    members.Add(member);
                }

                Plugin.PluginLog.Info($"Received {members.Count} members from server");
                Groups_DR.members = members; // Store in static cache

                // Update the current group's members if it exists and members have groupID. Note: We can't determine groupID from packet, so update currentGroup if it exists
                if (AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup != null && members.Count > 0)
                {
                    // Queue old avatar textures for deferred disposal to prevent race conditions with rendering. Clear the member avatar cache which will queue textures for safe disposal
                    AbsoluteRP.Windows.Social.Views.GroupsData.ClearMemberAvatarCache();

                    if (AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.members != null)
                    {
                        Plugin.PluginLog.Info($"[HandleGroupMembers] Queueing {AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.members.Count} old avatar textures for disposal");
                        foreach (var oldMember in AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.members)
                        {
                            if (oldMember.avatar != null)
                            {
                                // Queue for deferred disposal instead of immediate dispose. IMPORTANT: Do NOT set oldMember.avatar = null here! The render thread may still be using this reference. The disposal queue will handle cleanup at end of frame.
                                AbsoluteRP.Windows.Social.Views.GroupsData.QueueTextureForDisposal(oldMember.avatar);
                                Plugin.PluginLog.Info($"[HandleGroupMembers] Queued avatar for {oldMember.name} for deferred disposal");
                            }
                        }
                    }

                    // Replace the members list atomically - the old members list (and its avatar references)
                    // will be garbage collected after this, but textures are queued for safe disposal
                    AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.members = members;
                    Plugin.PluginLog.Info($"Updated currentGroup.members with {members.Count} members");

                    // Link member ranks to full rank data with permissions
                    if (AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.ranks != null)
                    {
                        foreach (var member in members)
                        {
                            // Link legacy single rank
                            if (member.rank != null && member.rank.id > 0)
                            {
                                var fullRank = AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.ranks.FirstOrDefault(r => r.id == member.rank.id);
                                if (fullRank != null)
                                {
                                    member.rank = fullRank;
                                    Plugin.PluginLog.Info($"[HandleGroupMembers] Linked member {member.name} to legacy rank {fullRank.name} with permissions");
                                }
                            }

                            // Link multiple ranks to full rank data
                            if (member.ranks != null)
                            {
                                for (int r = 0; r < member.ranks.Count; r++)
                                {
                                    var memberRank = member.ranks[r];
                                    if (memberRank != null && memberRank.id > 0)
                                    {
                                        var fullRank = AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.ranks.FirstOrDefault(x => x.id == memberRank.id);
                                        if (fullRank != null)
                                        {
                                            member.ranks[r] = fullRank;
                                            Plugin.PluginLog.Info($"[HandleGroupMembers] Linked member {member.name} to rank {fullRank.name} with permissions");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleGroupMembers: {ex.Message}");
            }
        }

        public static void HandleForumStructure(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int categoryCount = buffer.ReadInt(); // Server sends category count, NOT groupID

                List<GroupForumCategory> categories = new List<GroupForumCategory>();
                for (int i = 0; i < categoryCount; i++)
                {
                    var category = new GroupForumCategory
                    {
                        id = buffer.ReadInt(),
                        groupID = buffer.ReadInt(), // Read groupID from packet
                        parentCategoryID = buffer.ReadInt(),
                        categoryIndex = buffer.ReadInt(),
                        name = buffer.ReadString(),
                        description = buffer.ReadString(),
                        icon = buffer.ReadString(),
                        collapsed = buffer.ReadBool(),
                        categoryType = (byte)buffer.ReadInt(), // Server writes INT, cast to byte
                        sortOrder = buffer.ReadInt(),
                        createdAt = buffer.ReadLong(),
                        updatedAt = buffer.ReadLong(),
                        channels = new List<GroupForumChannel>()
                    };

                    int channelCount = buffer.ReadInt();
                    for (int j = 0; j < channelCount; j++)
                    {
                        var channel = new GroupForumChannel
                        {
                            id = buffer.ReadInt(),
                            groupID = buffer.ReadInt(), // Read groupID from packet
                            categoryID = buffer.ReadInt(), // Read categoryID from packet
                            parentChannelID = buffer.ReadInt(),
                            channelIndex = buffer.ReadInt(),
                            name = buffer.ReadString(),
                            description = buffer.ReadString(),
                            channelType = (byte)buffer.ReadInt(), // Server writes INT, cast to byte
                            isLocked = buffer.ReadBool(),
                            isNSFW = buffer.ReadBool(),
                            sortOrder = buffer.ReadInt(),
                            createdAt = buffer.ReadLong(),
                            updatedAt = buffer.ReadLong(),
                            lastMessageAt = buffer.ReadLong(),
                            subChannels = new List<GroupForumChannel>()
                        };

                        int subChannelCount = buffer.ReadInt();
                        for (int k = 0; k < subChannelCount; k++)
                        {
                            var subChannel = new GroupForumChannel
                            {
                                id = buffer.ReadInt(),
                                groupID = buffer.ReadInt(), // Read groupID from packet
                                parentChannelID = buffer.ReadInt(),
                                channelIndex = buffer.ReadInt(),
                                name = buffer.ReadString(),
                                description = buffer.ReadString(),
                                channelType = (byte)buffer.ReadInt(), // Server writes INT, cast to byte
                                isLocked = buffer.ReadBool(),
                                isNSFW = buffer.ReadBool(),
                                sortOrder = buffer.ReadInt(),
                                createdAt = buffer.ReadLong(),
                                updatedAt = buffer.ReadLong(),
                                lastMessageAt = buffer.ReadLong()
                            };
                            channel.subChannels.Add(subChannel);
                        }

                        category.channels.Add(channel);
                    }

                    categories.Add(category);
                }
                buffer.Dispose();

                Plugin.PluginLog.Info($"Received forum structure with {categories.Count} categories");

                // Update the forum structure in the group manager
                // TODO: Implement GroupManager
                // Plugin.GroupManager?.UpdateForumStructure(groupID, categories);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleForumStructure: {ex.Message}");
            }
        }

        public static void HandleForumPermissions(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();
                int groupID = buffer.ReadInt();
                int permissionCount = buffer.ReadInt();

                List<GroupForumChannelPermission> permissions = new List<GroupForumChannelPermission>();
                for (int i = 0; i < permissionCount; i++)
                {
                    var perm = new GroupForumChannelPermission
                    {
                        channelID = buffer.ReadInt(),
                        rankID = buffer.ReadInt(),
                        userID = buffer.ReadInt(),
                        canView = buffer.ReadBool(),
                        canPost = buffer.ReadBool(),
                        canReply = buffer.ReadBool(),
                        canCreateThreads = buffer.ReadBool(),
                        canEditOwn = buffer.ReadBool(),
                        canDeleteOwn = buffer.ReadBool(),
                        canManage = buffer.ReadBool(),
                        canPin = buffer.ReadBool(),
                        canLock = buffer.ReadBool()
                    };
                    permissions.Add(perm);
                }
                buffer.Dispose();

                // Update the forum permissions in the group manager
                // TODO: Implement GroupManager
                // Plugin.GroupManager?.UpdateForumPermissions(groupID, permissions);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleForumPermissions: {ex.Message}");
            }
        }

        public static void HandleInviteeProfile(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                int packetID = buffer.ReadInt();

                bool hasProfile = buffer.ReadBool();
                if (!hasProfile)
                {
                    buffer.Dispose();
                    Plugin.PluginLog.Info("No invitee profile data available");
                    return;
                }

                // Read profile data (structure depends on your Profile class)
                int profileID = buffer.ReadInt();
                string profileName = buffer.ReadString();
                // Add more fields as needed based on your Profile structure

                buffer.Dispose();

                // Display or cache the invitee profile
                // TODO: Implement GroupManager
                // Plugin.GroupManager?.ShowInviteeProfile(profileID, profileName);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleInviteeProfile: {ex.Message}");
            }
        }

        // Receives all rank definitions for a group including permissions, colors, and hierarchy ordering. Also returns the current user's permissions.
        public static void HandleGroupRanks(byte[] data)
        {
            try
            {
                using (ByteBuffer buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    int packetID = buffer.ReadInt();
                    int rankCount = buffer.ReadInt();

                    ranks.Clear();

                    for (int i = 0; i < rankCount; i++)
                    {
                        var rank = new GroupRank
                        {
                            id = buffer.ReadInt(),
                            groupID = buffer.ReadInt(),
                            name = buffer.ReadString(),
                            description = buffer.ReadString(),
                            hierarchy = buffer.ReadInt(),
                            isDefaultMember = buffer.ReadBool(),
                            permissions = new GroupRankPermissions
                            {
                                // Member Permissions
                                canInvite = buffer.ReadBool(),
                                canKick = buffer.ReadBool(),
                                canBan = buffer.ReadBool(),
                                canPromote = buffer.ReadBool(),
                                canDemote = buffer.ReadBool(),

                                // Message Permissions
                                canCreateAnnouncement = buffer.ReadBool(),
                                canReadMessages = buffer.ReadBool(),
                                canSendMessages = buffer.ReadBool(),
                                canDeleteOthersMessages = buffer.ReadBool(),
                                canPinMessages = buffer.ReadBool(),

                                // Category Permissions
                                canCreateCategory = buffer.ReadBool(),
                                canEditCategory = buffer.ReadBool(),
                                canDeleteCategory = buffer.ReadBool(),
                                canLockCategory = buffer.ReadBool(),

                                // Forum Permissions
                                canCreateForum = buffer.ReadBool(),
                                canEditForum = buffer.ReadBool(),
                                canDeleteForum = buffer.ReadBool(),
                                canLockForum = buffer.ReadBool(),
                                canMuteForum = buffer.ReadBool(),

                                // Rank Management Permissions
                                canManageRanks = buffer.ReadBool(),
                                canCreateRanks = buffer.ReadBool()
                            }
                        };

                        ranks.Add(rank);
                    }

                    Plugin.PluginLog.Info($"Received {rankCount} ranks from server");

                    // Update the current group's ranks if it exists
                    if (AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup != null && ranks.Count > 0)
                    {
                        int groupID = ranks[0].groupID;
                        if (AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.groupID == groupID)
                        {
                            AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.ranks = new List<GroupRank>(ranks);
                            Plugin.PluginLog.Info($"Updated currentGroup.ranks with {ranks.Count} ranks");

                            // Link existing members to the full rank data with permissions
                            if (AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.members != null)
                            {
                                foreach (var member in AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.members)
                                {
                                    if (member.rank != null && member.rank.id > 0)
                                    {
                                        var fullRank = ranks.FirstOrDefault(r => r.id == member.rank.id);
                                        if (fullRank != null)
                                        {
                                            member.rank = fullRank;
                                            Plugin.PluginLog.Info($"[HandleGroupRanks] Linked member {member.name} to rank {fullRank.name} with permissions");
                                        }
                                    }
                                }
                            }
                        }
                    }

                    buffer.Dispose();
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleGroupRanks: {ex.Message}");
            }
        }

        public static void HandleRankOperationResult(byte[] data)
        {
            try
            {
                using (ByteBuffer buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    int packetID = buffer.ReadInt();
                    rankOperationSuccess = buffer.ReadBool();
                    rankOperationMessage = buffer.ReadString();

                    buffer.Dispose();

                    if (rankOperationSuccess)
                    {
                        Plugin.PluginLog.Info($"Rank operation succeeded: {rankOperationMessage}");
                    }
                    else
                    {
                        Plugin.PluginLog.Warning($"Rank operation failed: {rankOperationMessage}");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error in HandleRankOperationResult: {ex.Message}");
            }
        }

        public static void HandleCreateChannelError(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                createChannelError = buffer.ReadString();

                Plugin.PluginLog.Debug($"HandleCreateChannelError: {createChannelError}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleCreateChannelError error: {ex.Message}");
            }
        }

        public static void HandleGroupBans(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                int banCount = buffer.ReadInt();

                var bansList = new List<GroupBans>();

                for (int i = 0; i < banCount; i++)
                {
                    bansList.Add(new GroupBans
                    {
                        id = buffer.ReadInt(),
                        userID = buffer.ReadInt(),
                        profileID = buffer.ReadInt(),
                        name = buffer.ReadString(),
                        lodestoneURL = buffer.ReadString()
                    });
                }

                // Update the current group's bans list if it matches
                if (AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup != null &&
                    AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.groupID == groupID)
                {
                    AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.bans = bansList;
                }

                Plugin.PluginLog.Debug($"HandleGroupBans: groupID={groupID}, banCount={banCount}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleGroupBans error: {ex.Message}");
            }
        }

        public static void HandleMemberRemovedFromGroup(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                int removedMemberID = buffer.ReadInt();
                int removedUserID = buffer.ReadInt();
                bool isBan = buffer.ReadBool();
                string groupName = buffer.ReadString();

                buffer.Dispose();

                Plugin.PluginLog.Info($"HandleMemberRemovedFromGroup: groupID={groupID}, memberID={removedMemberID}, userID={removedUserID}, isBan={isBan}, groupName={groupName}");

                // Check if this is about the current user being removed
                bool isMe = removedUserID == Accounts_DS.userID;

                if (isMe)
                {
                    // I was kicked/banned from the group
                    string action = isBan ? "banned from" : "kicked from";
                    Plugin.Chat?.Print($"You have been {action} the group: {groupName}");

                    // Remove the group from our groups list
                    AbsoluteRP.Windows.Social.Views.GroupsData.groups?.RemoveAll(g => g.groupID == groupID);

                    // If this is the current group, clear it
                    if (AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup?.groupID == groupID)
                    {
                        AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup = null;
                        AbsoluteRP.Windows.Social.Views.GroupsData.ClearSelectedChannel();
                    }
                }
                else
                {
                    // Someone else was kicked/banned - remove them from the current group's member list
                    if (AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup?.groupID == groupID)
                    {
                        AbsoluteRP.Windows.Social.Views.GroupsData.currentGroup.members?.RemoveAll(m => m.id == removedMemberID);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleMemberRemovedFromGroup error: {ex.Message}");
            }
        }

        /// Handles group info response for embed display. Caches the group name and logo URL for security.
        public static void HandleGroupInfo(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                string name = buffer.ReadString();
                string logoUrl = buffer.ReadString();

                buffer.Dispose();

                Plugin.PluginLog.Debug($"HandleGroupInfo: groupID={groupID}, name={name}, logoUrl={logoUrl}");

                // Cache the group info
                AbsoluteRP.Windows.Social.Views.GroupsData.CacheGroupInfo(groupID, name, logoUrl, null);

                // Start async logo fetch if we have a URL
                if (!string.IsNullOrEmpty(logoUrl))
                {
                    AbsoluteRP.Windows.Social.Views.GroupsData.FetchAndCacheLogoAsync(groupID, logoUrl);
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleGroupInfo error: {ex.Message}");
            }
        }

        /// Handles profile info response for embed display. Caches the profile name and avatar URL for security.
        public static void HandleProfileInfoEmbed(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int profileID = buffer.ReadInt();
                string name = buffer.ReadString();
                string avatarUrl = buffer.ReadString();

                buffer.Dispose();

                Plugin.PluginLog.Debug($"HandleProfileInfoEmbed: profileID={profileID}, name={name}, avatarUrl={avatarUrl}");

                // Cache the profile info
                AbsoluteRP.Windows.Social.Views.GroupsData.CacheProfileInfo(profileID, name, avatarUrl, null);

                // Start async avatar fetch if we have a URL
                if (!string.IsNullOrEmpty(avatarUrl))
                {
                    AbsoluteRP.Windows.Social.Views.GroupsData.FetchAndCacheAvatarAsync(profileID, avatarUrl);
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleProfileInfoEmbed error: {ex.Message}");
            }
        }
    }
}
