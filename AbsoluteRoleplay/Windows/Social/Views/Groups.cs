using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.NavLayouts;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Social.Views.SubViews;
using AbsoluteRP.Windows.Social.Views.Groups.GroupManager;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using AbsoluteRP.Defines;

namespace AbsoluteRP.Windows.Social.Views
{
    internal class GroupsData
    {
        public static Group currentGroup;
        public static bool openGroupCreation = false;

        public static List<Group> groups = new List<Group>();

        // Track pending join requests for visual feedback
        public static HashSet<int> pendingJoinRequests = new HashSet<int>();

        // Cache for group info (name, logo) - fetched from server for security
        public class GroupInfoCache
        {
            public string name { get; set; }
            public string logoUrl { get; set; }
            public IDalamudTextureWrap logo { get; set; }
        }
        private static Dictionary<int, GroupInfoCache> groupInfoCache = new Dictionary<int, GroupInfoCache>();
        // Track which group info is currently being fetched to avoid duplicate requests
        private static HashSet<int> pendingGroupInfoFetches = new HashSet<int>();

        /// <summary>
        /// Caches group info (name, logo) for display in embeds.
        /// </summary>
        public static void CacheGroupInfo(int groupID, string name, string logoUrl, IDalamudTextureWrap logo = null)
        {
            if (!groupInfoCache.TryGetValue(groupID, out var info))
            {
                info = new GroupInfoCache();
                groupInfoCache[groupID] = info;
            }
            if (!string.IsNullOrEmpty(name))
                info.name = name;
            if (!string.IsNullOrEmpty(logoUrl))
                info.logoUrl = logoUrl;
            if (logo != null)
                info.logo = logo;
            pendingGroupInfoFetches.Remove(groupID);
        }

        /// <summary>
        /// Gets cached group info, or null if not cached.
        /// </summary>
        public static GroupInfoCache GetCachedGroupInfo(int groupID)
        {
            if (groupInfoCache.TryGetValue(groupID, out var info))
            {
                return info;
            }
            return null;
        }

        /// <summary>
        /// Checks if group info is currently being fetched.
        /// </summary>
        public static bool IsGroupInfoFetchPending(int groupID)
        {
            return pendingGroupInfoFetches.Contains(groupID);
        }

        /// <summary>
        /// Marks a group info fetch as pending.
        /// </summary>
        public static void MarkGroupInfoFetchPending(int groupID)
        {
            pendingGroupInfoFetches.Add(groupID);
        }

        /// <summary>
        /// Fetches and caches a group logo asynchronously from a URL.
        /// </summary>
        public static async void FetchAndCacheLogoAsync(int groupID, string logoUrl)
        {
            if (string.IsNullOrEmpty(logoUrl)) return;

            var info = GetCachedGroupInfo(groupID);
            if (info?.logo != null) return; // Already have logo

            try
            {
                var logoBytes = await Imaging.FetchUrlImageBytes(logoUrl);
                if (logoBytes != null && logoBytes.Length > 0)
                {
                    var logoTexture = await Plugin.TextureProvider.CreateFromImageAsync(logoBytes);
                    CacheGroupInfo(groupID, null, logoUrl, logoTexture);
                }
            }
            catch
            {
                // Failed to fetch logo
            }
        }

        // Cache for profile info (name, avatar) - fetched from server for security
        public class ProfileInfoCache
        {
            public string name { get; set; }
            public string avatarUrl { get; set; }
            public IDalamudTextureWrap avatar { get; set; }
        }
        private static Dictionary<int, ProfileInfoCache> profileInfoCache = new Dictionary<int, ProfileInfoCache>();
        private static HashSet<int> pendingProfileInfoFetches = new HashSet<int>();

        /// <summary>
        /// Caches profile info (name, avatar) for display in embeds.
        /// </summary>
        public static void CacheProfileInfo(int profileID, string name, string avatarUrl, IDalamudTextureWrap avatar = null)
        {
            if (!profileInfoCache.TryGetValue(profileID, out var info))
            {
                info = new ProfileInfoCache();
                profileInfoCache[profileID] = info;
            }
            if (!string.IsNullOrEmpty(name))
                info.name = name;
            if (!string.IsNullOrEmpty(avatarUrl))
                info.avatarUrl = avatarUrl;
            if (avatar != null)
                info.avatar = avatar;
            pendingProfileInfoFetches.Remove(profileID);
        }

        /// <summary>
        /// Gets cached profile info, or null if not cached.
        /// </summary>
        public static ProfileInfoCache GetCachedProfileInfo(int profileID)
        {
            if (profileInfoCache.TryGetValue(profileID, out var info))
            {
                return info;
            }
            return null;
        }

        /// <summary>
        /// Checks if profile info is currently being fetched.
        /// </summary>
        public static bool IsProfileInfoFetchPending(int profileID)
        {
            return pendingProfileInfoFetches.Contains(profileID);
        }

        /// <summary>
        /// Marks a profile info fetch as pending.
        /// </summary>
        public static void MarkProfileInfoFetchPending(int profileID)
        {
            pendingProfileInfoFetches.Add(profileID);
        }

        /// <summary>
        /// Fetches and caches a profile avatar asynchronously from a URL.
        /// </summary>
        public static async void FetchAndCacheAvatarAsync(int profileID, string avatarUrl)
        {
            if (string.IsNullOrEmpty(avatarUrl)) return;

            var info = GetCachedProfileInfo(profileID);
            if (info?.avatar != null) return; // Already have avatar

            try
            {
                var avatarBytes = await Imaging.FetchUrlImageBytes(avatarUrl);
                if (avatarBytes != null && avatarBytes.Length > 0)
                {
                    var avatarTexture = await Plugin.TextureProvider.CreateFromImageAsync(avatarBytes);
                    CacheProfileInfo(profileID, null, avatarUrl, avatarTexture);
                }
            }
            catch
            {
                // Failed to fetch avatar
            }
        }

        /// <summary>
        /// Clears pending join requests for groups that are now in the user's groups list.
        /// Called when group memberships are refreshed from server.
        /// </summary>
        public static void ClearPendingJoinRequests()
        {
            if (groups != null)
            {
                foreach (var group in groups)
                {
                    pendingJoinRequests.Remove(group.groupID);
                }
            }
        }
        public static int selectedNavIndex = 0;
        private static int previousNavIndex = -1; // Track previous index to detect changes
        public static bool createGroup = false;
        private static string createGroupName;
        public static bool manageGroup = false;
        public static bool setBack = false;

        // Group Search
        private static string groupSearchQuery = string.Empty;
        private static bool showSearchResults = false;

        // Chat system state
        private static int selectedCategoryIndex = -1;
        private static int selectedChannelIndex = -1;
        private static GroupCategory selectedCategory = null;
        private static GroupChannel selectedChannel = null;
        private static List<GroupChatMessage> currentMessages = new List<GroupChatMessage>();
        private static readonly object messagesLock = new object(); // Thread safety for message list
        private static string messageInput = string.Empty;
        private static bool autoScroll = true;

        // Chat input resizing
        private static float chatInputHeight = 50f; // Default height
        private static bool isResizingChatInput = false;
        private const float minChatInputHeight = 30f;
        private const float maxChatInputHeight = 300f;

        // Message editing state
        private static GroupChatMessage editingMessage = null;

        // Message deletion confirmation
        private static GroupChatMessage messageToDelete = null;
        private static bool showDeleteConfirmation = false;

        // Leave group confirmation
        private static bool showLeaveGroupConfirmation = false;
        private static Group groupToLeave = null;

        // NSFW channel warning
        private static bool showNsfwWarning = false;
        private static GroupChannel pendingNsfwChannel = null;
        private static int pendingNsfwCategoryIndex = -1;
        private static int pendingNsfwChannelIndex = -1;
        private static GroupCategory pendingNsfwCategory = null;
        // NSFW agreements are now stored in Configuration.agreedNsfwChannelIds for persistence across sessions

        // Rules channel state
        private static string rulesEditContent = string.Empty;
        private static bool isEditingRules = false;

        // Role selection channel state
        private static bool showRoleManagement = false;
        private static string newRoleName = string.Empty;
        private static string newRoleDescription = string.Empty;
        private static Vector4 newRoleColor = new Vector4(1f, 1f, 1f, 1f);
        private static int newRoleSectionID = 0;
        private static GroupSelfAssignRole roleToDelete = null;
        private static bool showDeleteRoleConfirmation = false;
        private static GroupSelfAssignRole editingRole = null;
        private static string editRoleName = string.Empty;
        private static string editRoleDescription = string.Empty;
        private static Vector4 editRoleColor = new Vector4(1f, 1f, 1f, 1f);
        private static int editRoleSectionID = 0;

        // Role sections state
        private static string newSectionName = string.Empty;
        private static GroupRoleSection sectionToDelete = null;
        private static bool showDeleteSectionConfirmation = false;

        // Slash command state
        private static bool showSlashCommandPopup = false;
        private static string slashCommandSearch = string.Empty;
        private static int slashCommandSelectedIndex = 0;
        private static string slashCommandSelectedType = string.Empty; // "profile", "groupinvite", "spoiler", "nsfw"
        private static bool slashCommandNeedsSelection = false; // True when we need to show profile/group selection
        private static string slashCommandSelectionSearch = string.Empty;
        private static int slashCommandSelectionIndex = 0;
        private static readonly string[] slashCommands = { "/profile", "/groupinvite", "/spoiler", "/nsfw" };
        private static readonly string[] slashCommandDescriptions = {
            "Add a profile embed to your message",
            "Add a group invite link to your message",
            "Wrap text in a spoiler tag",
            "Wrap text in a NSFW spoiler tag"
        };

        // Edit channel state
        private static bool showEditChannelPopup = false;
        private static GroupChannel channelBeingEdited = null;
        private static int editingChannelCategoryIndex = -1;
        private static string editChannelName = string.Empty;
        private static string editChannelDescription = string.Empty;
        private static int editChannelType = 0;
        private static bool editEveryoneCanView = true;
        private static bool editEveryoneCanPost = true;
        private static bool editChannelIsNsfw = false;
        private static string editChannelPermissionSearchQuery = string.Empty;
        private static List<ChannelMemberPermission> editChannelPermissionSelectedMembers = new List<ChannelMemberPermission>();
        private static List<ChannelRankPermission> editChannelPermissionSelectedRanks = new List<ChannelRankPermission>();
        private static List<ChannelSelfRolePermission> editChannelPermissionSelectedRoles = new List<ChannelSelfRolePermission>();

        // Avatar texture cache - track textures by userID for proper disposal (chat messages)
        private static Dictionary<int, IDalamudTextureWrap> avatarTextureCache = new Dictionary<int, IDalamudTextureWrap>();
        private static readonly object avatarCacheLock = new object();

        // Member avatar texture cache - track textures by member id for proper disposal (member list)
        private static Dictionary<int, IDalamudTextureWrap> memberAvatarCache = new Dictionary<int, IDalamudTextureWrap>();
        private static readonly object memberAvatarCacheLock = new object();

        // Queue of textures pending disposal - deferred to avoid disposing while rendering
        private static Queue<IDalamudTextureWrap> texturesToDispose = new Queue<IDalamudTextureWrap>();
        private static readonly object disposeQueueLock = new object();

        // Track which avatars are currently being loaded to avoid duplicate requests
        private static HashSet<int> avatarsLoading = new HashSet<int>();

        /// <summary>
        /// Checks if a texture is valid and can be rendered.
        /// Returns false if texture is null, disposed, or has an invalid handle.
        /// </summary>
        public static bool IsTextureValid(IDalamudTextureWrap texture)
        {
            if (texture == null) return false;

            try
            {
                // Accessing Handle on a disposed texture may throw ObjectDisposedException
                var handle = texture.Handle;
                return handle != default;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        // Track textures already queued for disposal to prevent double-disposal
        // Use object reference equality to track textures
        private static HashSet<IDalamudTextureWrap> queuedTexturesForDisposal = new HashSet<IDalamudTextureWrap>();

        /// <summary>
        /// Queues a texture for deferred disposal. Use this instead of immediate Dispose()
        /// to avoid disposing textures while they may still be in use by the render thread.
        /// Prevents double-queueing of the same texture.
        /// </summary>
        public static void QueueTextureForDisposal(IDalamudTextureWrap texture)
        {
            if (texture == null) return;

            lock (disposeQueueLock)
            {
                // Don't queue if already queued (using reference equality)
                if (queuedTexturesForDisposal.Contains(texture))
                {
                    return;
                }

                // Check if texture is already disposed
                try
                {
                    var handle = texture.Handle;
                    if (handle == default)
                    {
                        return; // Already disposed
                    }
                }
                catch (ObjectDisposedException)
                {
                    return; // Already disposed
                }

                queuedTexturesForDisposal.Add(texture);
                texturesToDispose.Enqueue(texture);
            }
        }

        /// <summary>
        /// Processes all pending texture disposals. Call this at a safe point,
        /// such as at the end of a frame or when no rendering is in progress.
        /// </summary>
        public static void ProcessPendingDisposals()
        {
            lock (disposeQueueLock)
            {
                while (texturesToDispose.Count > 0)
                {
                    var texture = texturesToDispose.Dequeue();
                    try
                    {
                        if (texture != null)
                        {
                            // Remove from tracking set before disposing
                            queuedTexturesForDisposal.Remove(texture);
                            texture.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.PluginLog.Warning($"[ProcessPendingDisposals] Failed to dispose texture: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a texture is queued for disposal (and should not be used).
        /// </summary>
        private static bool IsTextureQueuedForDisposal(IDalamudTextureWrap texture)
        {
            if (texture == null) return true;
            lock (disposeQueueLock)
            {
                return queuedTexturesForDisposal.Contains(texture);
            }
        }

        /// <summary>
        /// Gets or caches a member avatar texture by member ID.
        /// Thread-safe method for accessing member avatars.
        /// </summary>
        public static IDalamudTextureWrap GetMemberAvatar(int memberId, IDalamudTextureWrap currentTexture)
        {
            lock (memberAvatarCacheLock)
            {
                // Check if we have a cached texture for this member
                if (memberAvatarCache.TryGetValue(memberId, out var cachedTexture))
                {
                    // Check if cached texture is valid and not queued for disposal
                    if (cachedTexture != null && IsTextureValid(cachedTexture) && !IsTextureQueuedForDisposal(cachedTexture))
                    {
                        return cachedTexture;
                    }
                    else
                    {
                        // Cached texture is invalid or queued for disposal, remove it
                        memberAvatarCache.Remove(memberId);
                        // Queue for disposal if not already
                        QueueTextureForDisposal(cachedTexture);
                    }
                }

                // If current texture is valid and not queued for disposal, cache it
                if (currentTexture != null && IsTextureValid(currentTexture) && !IsTextureQueuedForDisposal(currentTexture))
                {
                    memberAvatarCache[memberId] = currentTexture;
                    return currentTexture;
                }

                return null;
            }
        }

        /// <summary>
        /// Clears the member avatar cache and queues all textures for disposal.
        /// </summary>
        public static void ClearMemberAvatarCache()
        {
            lock (memberAvatarCacheLock)
            {
                foreach (var kvp in memberAvatarCache)
                {
                    if (kvp.Value != null)
                    {
                        QueueTextureForDisposal(kvp.Value);
                    }
                }
                memberAvatarCache.Clear();
                Plugin.PluginLog.Info($"[ClearMemberAvatarCache] Queued all member avatar textures for disposal");
            }
        }

        /// <summary>
        /// Queues all cached avatar textures for deferred disposal and clears the cache.
        /// Call this when switching channels or closing the group.
        /// </summary>
        private static void ClearAvatarCache()
        {
            lock (avatarCacheLock)
            {
                foreach (var kvp in avatarTextureCache)
                {
                    if (kvp.Value != null)
                    {
                        QueueTextureForDisposal(kvp.Value);
                    }
                }
                avatarTextureCache.Clear();
                avatarsLoading.Clear();
                Plugin.PluginLog.Info($"[ClearAvatarCache] Queued all avatar textures for disposal");
            }
        }

        /// <summary>
        /// Resets all group state including textures. Call this when the window is closed.
        /// </summary>
        public static void ResetState()
        {
            Plugin.PluginLog.Info($"[GroupsData] ResetState called - disposing all resources");

            // Clear avatar caches first (under lock)
            ClearAvatarCache();
            ClearMemberAvatarCache();

            // Process any pending disposals
            ProcessPendingDisposals();

            // Clear messages under lock
            lock (messagesLock)
            {
                currentMessages.Clear();
            }

            // Reset selection state
            currentGroup = null;
            selectedNavIndex = 0;
            previousNavIndex = -1;
            selectedCategoryIndex = -1;
            selectedChannelIndex = -1;
            selectedCategory = null;
            selectedChannel = null;
            messageInput = string.Empty;
            editingMessage = null;

            // NSFW agreements are now persisted in Configuration, no longer cleared on reset

            Plugin.PluginLog.Info($"[GroupsData] ResetState complete");
        }

        /// <summary>
        /// Clears the currently selected channel/category. Called when user is removed from group.
        /// </summary>
        public static void ClearSelectedChannel()
        {
            selectedCategoryIndex = -1;
            selectedChannelIndex = -1;
            selectedCategory = null;
            selectedChannel = null;
            lock (messagesLock)
            {
                currentMessages.Clear();
            }
        }

        /// <summary>
        /// Gets or creates an avatar texture for a user.
        /// Reuses cached textures to prevent memory leaks.
        /// </summary>
        private static IDalamudTextureWrap GetOrCreateAvatarTexture(int userID, IDalamudTextureWrap newTexture)
        {
            lock (avatarCacheLock)
            {
                if (avatarTextureCache.TryGetValue(userID, out var cachedTexture))
                {
                    // Check if cached texture is still valid and not queued for disposal
                    if (cachedTexture != null && IsTextureValid(cachedTexture) && !IsTextureQueuedForDisposal(cachedTexture))
                    {
                        // Return the cached texture, queue the new one for disposal if different
                        if (newTexture != null && newTexture != cachedTexture)
                        {
                            QueueTextureForDisposal(newTexture);
                        }
                        return cachedTexture;
                    }
                    else
                    {
                        // Cached texture is invalid, remove it and queue for disposal
                        avatarTextureCache.Remove(userID);
                        QueueTextureForDisposal(cachedTexture);
                        Plugin.PluginLog.Warning($"[GetOrCreateAvatarTexture] Removed invalid cached texture for user {userID}");
                    }
                }

                // Check if new texture is valid and not queued for disposal
                if (newTexture != null && IsTextureValid(newTexture) && !IsTextureQueuedForDisposal(newTexture))
                {
                    avatarTextureCache[userID] = newTexture;
                    return newTexture;
                }

                return null;
            }
        }

        public static void LoadGroupList()
        {
            // Search bar pinned at the top
            DrawGroupSearchBar();

            // Create a 2-column table: left for navigation (fixed width), right for group content (stretch).
            using var table = ImRaii.Table("GroupsTable", 2, ImGuiTableFlags.BordersV);
            if (!table)
                return;

            // Column setup: first column fixed width for stable nav positioning
            ImGui.TableSetupColumn("Nav", ImGuiTableColumnFlags.WidthFixed, ImGui.GetIO().FontGlobalScale * 45);
            ImGui.TableSetupColumn("Content", ImGuiTableColumnFlags.WidthStretch);

            // Single row containing both columns
            ImGui.TableNextRow();

            // Left column: navigation (vertical)
            ImGui.TableSetColumnIndex(0);
            ImGui.BeginGroup();
            var nav = NavigationLayouts.GroupsNavigation(groups);
            float buttonSize = ImGui.GetIO().FontGlobalScale * 45f;
            int rightClickedGroupIndex = UIHelpers.DrawInlineNavigation(nav, ref selectedNavIndex, false, buttonSize, true);

            // Handle right-click on group navigation button
            if (rightClickedGroupIndex >= 0 && rightClickedGroupIndex < groups.Count)
            {
                var clickedGroup = groups[rightClickedGroupIndex];
                if (clickedGroup != null)
                {
                    // Check if user is owner of this group
                    var member = clickedGroup.members?.FirstOrDefault(m => m.userID == Plugin.plugin?.Configuration?.account?.userID);
                    bool isOwner = member?.owner == true;

                    // Only show leave option if not owner
                    if (!isOwner)
                    {
                        groupToLeave = clickedGroup;
                        ImGui.OpenPopup("LeaveGroupContextMenu");
                    }
                }
            }

            // Context menu popup for leave group
            if (ImGui.BeginPopup("LeaveGroupContextMenu"))
            {
                if (groupToLeave != null)
                {
                    ImGui.TextDisabled(groupToLeave.name ?? "Group");
                    ImGui.Separator();
                    if (ImGui.MenuItem("Leave Group"))
                    {
                        showLeaveGroupConfirmation = true;
                        ImGui.CloseCurrentPopup();
                    }
                }
                ImGui.EndPopup();
            }

            // Check if navigation index changed and load the selected group
            if (selectedNavIndex != previousNavIndex && selectedNavIndex >= 0 && selectedNavIndex < groups.Count)
            {
                previousNavIndex = selectedNavIndex;
                var selectedGroup = groups[selectedNavIndex];
                Plugin.PluginLog.Info($"[Groups] Navigation changed to index {selectedNavIndex}, loading group: {selectedGroup.name} (ID: {selectedGroup.groupID})");
                LoadGroup(selectedGroup);
            }

            createGroup = CustomLayouts.TransparentImageButton(UI.UICommonImage(UI.CommonImageTypes.create).Handle, new Vector2(buttonSize, buttonSize), "Create Group");
            if (createGroup)
            {
                // open the creation editor and ensure GroupCreation has an edit buffer and file dialog manager
                openGroupCreation = true;

                // Provide a fresh in-memory Group instance so DrawGroupBaseEditor() will render.
                GroupCreation.group = new Group
                {
                    name = string.Empty,
                    description = string.Empty,
                    visible = true,
                    openInvite = false,
                    ranks = new List<GroupRank>(),
                    members = new List<GroupMember>(),
                    bans = new List<GroupBans>(),
                    categories = new List<GroupCategory>(),
                    application = null
                };

                // Ensure file dialog manager exists (DrawGroupBaseEditor calls _fileDialogManager.Draw())
                if (GroupCreation._fileDialogManager == null)
                    GroupCreation._fileDialogManager = new Dalamud.Interface.ImGuiFileDialog.FileDialogManager();
                if (GroupManager._fileDialogManager == null)
                    GroupManager._fileDialogManager = new Dalamud.Interface.ImGuiFileDialog.FileDialogManager();
            }
            ImGui.EndGroup();

            // Right column: selected group details (stays in fixed column so nav position stable)
            ImGui.TableSetColumnIndex(1);
            ImGui.BeginGroup();
            if (currentGroup != null && !openGroupCreation && !manageGroup)
            {
                Misc.DrawCenteredImage(currentGroup.logo, new System.Numerics.Vector2(ImGui.GetIO().FontGlobalScale * 50, ImGui.GetIO().FontGlobalScale * 50), false);

                // Show settings button for group owner or users with rank management permissions
                var currentUserMember = currentGroup.members?.FirstOrDefault(m => m.userID == Plugin.plugin.Configuration.account.userID);
                bool isOwner = currentUserMember?.owner == true;
                bool canManageRanks = currentUserMember?.rank?.permissions?.canManageRanks == true;
                bool canCreateRanks = currentUserMember?.rank?.permissions?.canCreateRanks == true;
                bool hasSettingsAccess = isOwner || canManageRanks || canCreateRanks;

                if (hasSettingsAccess)
                {
                    ImGui.SameLine();
                    ImGui.SetCursorPosX(ImGui.GetWindowSize().X - ImGui.GetIO().FontGlobalScale * 45);
                    if (CustomLayouts.TransparentImageButton(UI.UICommonImage(UI.CommonImageTypes.socialGroupSettings).Handle, new Vector2(ImGui.GetIO().FontGlobalScale * 20, ImGui.GetIO().FontGlobalScale * 20)))
                    {
                        manageGroup = true;
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Manage Group");
                    }
                }
                Misc.SetTitle(Plugin.plugin, true, currentGroup.name, new Vector4(1, 1, 1, 1), ImGui.GetIO().FontGlobalScale * 22.5f);
                ImGui.Spacing();
                Misc.DrawCenteredWrappedText(currentGroup.description, true, false);

                // View Group Profile button (visible to all members)
                ImGui.Spacing();
                bool hasGroupProfile = currentGroup.ProfileData != null && currentGroup.ProfileData.id > 0;

                // Center the button
                ImGui.Spacing();

                // Group Categories and Channels
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                DrawGroupChannelsAndChat();
            }
            if (openGroupCreation)
            {
                manageGroup = false;
                GroupCreation.DrawGroupBaseEditor();
            }
            if (manageGroup)
            {
                openGroupCreation = false;
                GroupManager.ManageGroup(currentGroup);
            }
            ImGui.EndGroup();
        }
        public static void LoadGroup(Group group)
        {
            Plugin.PluginLog.Info($"[Groups.LoadGroup] Loading group: {group.name} (ID: {group.groupID})");
            Plugin.PluginLog.Info($"[Groups.LoadGroup] ProfileData: {(group.ProfileData != null ? $"id={group.ProfileData.id}" : "null")}");

            currentGroup = group;
            // Reset chat state when switching groups
            selectedCategoryIndex = -1;
            selectedChannelIndex = -1;
            selectedCategory = null;
            selectedChannel = null;
            lock (messagesLock)
            {
                currentMessages.Clear();
            }
            messageInput = string.Empty;

            // Clear avatar texture cache when switching groups
            ClearAvatarCache();

            // Fetch all group data (same as GroupManager does)
            Plugin.PluginLog.Info($"[Groups.LoadGroup] Fetching all data for group {group.groupID}...");
            DataSender.FetchGroupMembers(Plugin.character, group.groupID);
            DataSender.FetchGroupRanks(Plugin.character, group.groupID);
            DataSender.FetchGroupCategories(Plugin.character, group.groupID);
            DataSender.FetchForumStructure(Plugin.character, group.groupID);
            DataSender.FetchGroupRosterFields(Plugin.character, group.groupID);
            DataSender.FetchGroupRules(Plugin.character, group.groupID); // Fetch rules to check agreement status
            DataSender.FetchSelfAssignRoles(Plugin.character, group.groupID); // Fetch self-assign roles for channel permissions
            DataSender.FetchRoleSections(Plugin.character, group.groupID); // Fetch role sections
            Plugin.PluginLog.Info($"[Groups.LoadGroup] Fetch requests sent for group {group.groupID}");
        }

        private static void DrawGroupChannelsAndChat()
        {
            if (currentGroup == null)
                return;

            // Tab bar for switching between Channels and Members
            if (ImGui.BeginTabBar("GroupMainTabs"))
            {
                if (ImGui.BeginTabItem("Channels"))
                {
                    mainGroupTab = 0;
                    ImGui.Spacing();

                    if (currentGroup.categories != null)
                    {
                        // Create a table with 2 columns: channel list (left) and chat area (right)
                        using var table = ImRaii.Table("ChannelChatTable", 2, ImGuiTableFlags.BordersV | ImGuiTableFlags.Resizable);
                        if (table)
                        {
                            // Column setup: channel list fixed width, chat area stretch
                            ImGui.TableSetupColumn("Channels", ImGuiTableColumnFlags.WidthFixed, ImGui.GetIO().FontGlobalScale * 200);
                            ImGui.TableSetupColumn("Chat", ImGuiTableColumnFlags.WidthStretch);

                            ImGui.TableNextRow();

                            // Left column: Channel list
                            ImGui.TableSetColumnIndex(0);
                            DrawChannelList();

                            // Right column: Chat area
                            ImGui.TableSetColumnIndex(1);
                            DrawChatArea();
                        }
                    }
                    else
                    {
                        ImGui.TextDisabled("No channels available.");
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Members"))
                {
                    mainGroupTab = 1;
                    ImGui.Spacing();
                    DrawMembersList();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

            // Draw member management popups outside of tabs so they work from either tab
            DrawMemberManagementPopups();
        }

        private static void DrawMembersList()
        {
            if (currentGroup == null || currentGroup.members == null)
            {
                ImGui.TextDisabled("No members data available.");
                return;
            }

            // Get current user info for permission checks
            var currentUserMember = currentGroup.members.FirstOrDefault(m => m.userID == Plugin.plugin.Configuration.account.userID);
            bool isCurrentUserOwner = currentUserMember?.owner == true;

            ImGui.Text($"Members ({currentGroup.members.Count})");
            ImGui.Separator();
            ImGui.Spacing();

            float availableHeight = ImGui.GetContentRegionAvail().Y;
            ImGui.BeginChild("MembersListScroll", new Vector2(0, availableHeight), true);

            // Sort members: owners first, then by name
            var sortedMembers = currentGroup.members
                .OrderByDescending(m => m.owner)
                .ThenBy(m => m.name ?? "")
                .ToList();

            foreach (var member in sortedMembers)
            {
                if (member == null) continue;

                ImGui.PushID($"member_{member.id}");

                // Draw member card background
                var drawList = ImGui.GetWindowDrawList();
                var cursorPos = ImGui.GetCursorScreenPos();
                float cardWidth = ImGui.GetContentRegionAvail().X;
                float cardHeight = 60;

                drawList.AddRectFilled(
                    cursorPos,
                    new Vector2(cursorPos.X + cardWidth, cursorPos.Y + cardHeight),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0.15f, 0.15f, 0.2f, 0.8f)),
                    4.0f
                );

                // Avatar - use centralized cache for safety
                float avatarSize = 50;
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 5);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 5);

                bool avatarDrawn = false;
                var memberAvatarRef = member.avatar; // Capture reference to avoid race conditions
                var cachedAvatar = GetMemberAvatar(member.id, memberAvatarRef);
                if (cachedAvatar != null && IsTextureValid(cachedAvatar))
                {
                    try
                    {
                        var handle = cachedAvatar.Handle;
                        if (handle != default)
                        {
                            ImGui.Image(handle, new Vector2(avatarSize, avatarSize));
                            avatarDrawn = true;
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Texture was disposed between validation and use - silently ignore
                    }
                    catch (Exception ex)
                    {
                        Plugin.PluginLog.Warning($"[DrawMembersList] Failed to draw avatar for member {member.userID}: {ex.Message}");
                    }
                }

                if (!avatarDrawn)
                {
                    // Placeholder for no avatar or failed to render
                    var placeholderPos = ImGui.GetCursorScreenPos();
                    drawList.AddRectFilled(
                        placeholderPos,
                        new Vector2(placeholderPos.X + avatarSize, placeholderPos.Y + avatarSize),
                        ImGui.ColorConvertFloat4ToU32(new Vector4(0.3f, 0.3f, 0.3f, 1f)),
                        4.0f
                    );
                    ImGui.Dummy(new Vector2(avatarSize, avatarSize));
                }

                ImGui.SameLine();

                // Member info
                ImGui.BeginGroup();

                // Name with owner indicator
                if (member.owner)
                {
                    ImGui.TextColored(new Vector4(1f, 0.84f, 0f, 1f), member.name ?? "Unknown");
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(1f, 0.84f, 0f, 0.7f), "(Owner)");
                }
                else
                {
                    ImGui.TextColored(new Vector4(1f, 1f, 1f, 1f), member.name ?? "Unknown");
                }

                // Rank
                if (member.rank != null && !string.IsNullOrEmpty(member.rank.name))
                {
                    ImGui.TextColored(new Vector4(0.6f, 0.8f, 1f, 1f), member.rank.name);
                }
                else
                {
                    ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "No Rank");
                }

                ImGui.EndGroup();

                // Tooltip with self-assigned roles when hovering over the member card
                // Use an invisible button to capture hover for the whole card area
                ImGui.SetCursorScreenPos(cursorPos);
                ImGui.InvisibleButton($"memberCard_{member.id}", new Vector2(cardWidth, cardHeight));
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text(member.name ?? "Unknown");
                    if (member.owner)
                    {
                        ImGui.SameLine();
                        ImGui.TextColored(new Vector4(1f, 0.84f, 0f, 0.7f), "(Owner)");
                    }
                    if (member.rank != null && !string.IsNullOrEmpty(member.rank.name))
                    {
                        ImGui.TextColored(new Vector4(0.6f, 0.8f, 1f, 1f), $"Rank: {member.rank.name}");
                    }
                    if (member.selfAssignedRoles != null && member.selfAssignedRoles.Count > 0)
                    {
                        ImGui.Separator();
                        ImGui.Text("Roles:");
                        foreach (var role in member.selfAssignedRoles)
                        {
                            Vector4 roleColor = ParseHexColor(role.color);
                            ImGui.TextColored(roleColor, $"  • {role.name}");
                        }
                    }
                    ImGui.EndTooltip();
                }

                // Right-click context menu for member management
                bool isOwnMember = member.userID == Plugin.plugin.Configuration.account.userID;
                if (!isOwnMember && !member.owner && ImGui.BeginPopupContextItem($"memberContext_{member.id}"))
                {
                    ImGui.TextDisabled(member.name ?? "Member");
                    ImGui.Separator();

                    // Check permissions
                    int currentUserHierarchy = isCurrentUserOwner ? int.MaxValue : (currentUserMember?.rank?.hierarchy ?? 0);
                    int targetHierarchy = member.rank?.hierarchy ?? 0;
                    bool canManageThisMember = isCurrentUserOwner || currentUserHierarchy > targetHierarchy;

                    if (canManageThisMember)
                    {
                        var perms = currentUserMember?.rank?.permissions;
                        bool canPromote = isCurrentUserOwner || (perms?.canPromote == true);
                        bool canDemote = isCurrentUserOwner || (perms?.canDemote == true);
                        bool canKick = isCurrentUserOwner || (perms?.canKick == true);
                        bool canBan = isCurrentUserOwner || (perms?.canBan == true);

                        if ((canPromote || canDemote) && ImGui.MenuItem("Change Rank"))
                        {
                            memberToManage = member;
                            showPromoteMemberPopup = true;
                            ImGui.CloseCurrentPopup();
                        }
                        if (canKick && ImGui.MenuItem("Kick from Group"))
                        {
                            memberToManage = member;
                            showKickMemberConfirmation = true;
                            ImGui.CloseCurrentPopup();
                        }
                        if (canBan && ImGui.MenuItem("Ban from Group"))
                        {
                            memberToManage = member;
                            showBanMemberConfirmation = true;
                            ImGui.CloseCurrentPopup();
                        }
                    }
                    else
                    {
                        ImGui.TextDisabled("No permissions");
                    }

                    ImGui.EndPopup();
                }

                // Move cursor past the card
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + cardHeight - avatarSize + 10);
                ImGui.Spacing();

                ImGui.PopID();
            }

            ImGui.EndChild();
        }

        // Drag and drop state
        private static GroupChannel draggedChannel = null;
        private static int draggedChannelCategoryIndex = -1;
        private static int draggedChannelIndex = -1;

        // Category drag and drop state
        private static GroupCategory draggedCategory = null;
        private static int draggedCategoryIndex = -1;

        // Rename state
        private static bool renamingCategory = false;
        private static bool renamingChannel = false;
        private static int renamingCategoryIndex = -1;
        private static int renamingChannelCategoryIndex = -1;
        private static int renamingChannelIndex = -1;
        private static string renameBuffer = "";

        // Right-click context tracking
        private static int rightClickedCategoryIndex = -1;
        private static int rightClickedChannelCategoryIndex = -1;
        private static int rightClickedChannelIndex = -1;

        // Delete channel confirmation
        private static bool showDeleteChannelConfirmation = false;
        private static GroupChannel channelToDelete = null;
        private static int channelToDeleteCategoryIndex = -1;

        // Delete category confirmation
        private static bool showDeleteCategoryConfirmation = false;
        private static GroupCategory categoryToDelete = null;
        private static int categoryToDeleteIndex = -1;

        // Create channel popup
        private static bool showCreateChannelPopup = false;
        private static int createChannelCategoryId = -1;
        private static string newChannelName = "";
        private static string newChannelDescription = "";
        private static int newChannelType = 0; // 0 = text, 1 = announcement
        private static bool newChannelIsNsfw = false; // NSFW channel flag

        // Channel permissions state for create channel popup
        private static string channelPermissionSearchQuery = "";
        private static List<ChannelMemberPermission> channelPermissionSelectedMembers = new List<ChannelMemberPermission>();
        private static List<ChannelRankPermission> channelPermissionSelectedRanks = new List<ChannelRankPermission>();
        private static List<ChannelSelfRolePermission> channelPermissionSelectedRoles = new List<ChannelSelfRolePermission>();
        private static bool channelPermissionEveryoneCanView = true;
        private static bool channelPermissionEveryoneCanPost = true;
        private static int channelPermissionTabIndex = 0; // 0 = Search, 1 = Roles

        // Permission classes to hold individual member/rank permissions
        private class ChannelMemberPermission
        {
            public GroupMember member;
            public bool canView = true;
            public bool canPost = true;
        }

        private class ChannelRankPermission
        {
            public GroupRank rank;
            public bool canView = true;
            public bool canPost = true;
        }

        private class ChannelSelfRolePermission
        {
            public GroupSelfAssignRole role;
            public bool canView = true;
            public bool canPost = true;
        }

        // Shared struct for sending permission data to server
        public struct ChannelPermissionEntry
        {
            public int id;
            public bool canView;
            public bool canPost;
        }

        // Create category popup
        private static bool showCreateCategoryPopup = false;
        private static string newCategoryName = "";

        // Member management popups
        private static bool showKickMemberConfirmation = false;
        private static bool showBanMemberConfirmation = false;
        private static bool showPromoteMemberPopup = false;
        private static GroupMember memberToManage = null;
        private static bool showPinnedMessagesPopup = false;
        private static int scrollToMessageID = 0; // Set when clicking a pinned message to scroll to it

        // Main group view tabs (Channels / Members)
        private static int mainGroupTab = 0; // 0 = Channels, 1 = Members

        private static void DrawChannelList()
        {
            ImGui.BeginGroup();
            ImGui.Text("CHANNELS");
            ImGui.Separator();

            // Get current member's permissions (needed for context menu even when empty)
            var currentMember = currentGroup.members?.FirstOrDefault(m => m.userID == Plugin.plugin.Configuration.account.userID);
            bool canEditCategory = currentMember?.owner == true || currentMember?.rank?.permissions?.canEditCategory == true;
            bool canDeleteCategory = currentMember?.owner == true || currentMember?.rank?.permissions?.canDeleteCategory == true;
            bool canEditForum = currentMember?.owner == true || currentMember?.rank?.permissions?.canEditForum == true;
            bool canDeleteForum = currentMember?.owner == true || currentMember?.rank?.permissions?.canDeleteForum == true;
            bool canLockForum = currentMember?.owner == true || currentMember?.rank?.permissions?.canLockForum == true;

            // Create scrollable child region for channel list
            using var child = ImRaii.Child("ChannelList", new Vector2(-1, -1), false);
            if (!child)
            {
                ImGui.EndGroup();
                return;
            }

            if (currentGroup.categories == null || currentGroup.categories.Count == 0)
            {
                ImGui.TextDisabled("No channels available");
                ImGui.TextDisabled("Right-click to create a category");

                // Right-click context menu for empty area
                if (ImGui.BeginPopupContextWindow("EmptyChannelListContext"))
                {
                    if (canEditCategory && ImGui.MenuItem("Create Category"))
                    {
                        newCategoryName = "";
                        showCreateCategoryPopup = true;
                    }
                    ImGui.EndPopup();
                }

                // Draw popups even when empty
                DrawChannelListPopups(canEditCategory, canEditForum);
                ImGui.EndGroup();
                return;
            }

            for (int catIdx = 0; catIdx < currentGroup.categories.Count; catIdx++)
            {
                var category = currentGroup.categories[catIdx];
                if (category == null)
                    continue;

                // Category header
                ImGui.PushID($"cat_{catIdx}");

                bool nodeOpen = false; // Track if tree node is open

                // Handle category renaming
                if (renamingCategory && renamingCategoryIndex == catIdx)
                {
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.InputText("##renameCategory", ref renameBuffer, 100, ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        if (!string.IsNullOrWhiteSpace(renameBuffer))
                        {
                            // Send rename request to server
                            var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                                x.characterName == Plugin.plugin.playername &&
                                x.characterWorld == Plugin.plugin.playerworld);
                            if (character != null)
                            {
                                DataSender.RenameCategory(character, currentGroup.groupID, category.id, renameBuffer);
                                category.name = renameBuffer;
                            }
                        }
                        renamingCategory = false;
                        renamingCategoryIndex = -1;
                    }
                    if (ImGui.IsItemDeactivated() && !ImGui.IsItemDeactivatedAfterEdit())
                    {
                        renamingCategory = false;
                        renamingCategoryIndex = -1;
                    }
                }
                else
                {
                    // Use TreeNode instead of CollapsingHeader for better control
                    bool isOpen = !category.collapsed;
                    ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.SpanAvailWidth;
                    if (isOpen)
                        flags |= ImGuiTreeNodeFlags.DefaultOpen;

                    nodeOpen = ImGui.TreeNodeEx($"{category.name}##cat", flags);

                    // Track right-clicked category
                    if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                    {
                        rightClickedCategoryIndex = catIdx;
                        rightClickedChannelCategoryIndex = -1;
                        rightClickedChannelIndex = -1;
                    }

                    // Context menu for category (must be right after TreeNodeEx)
                    if (ImGui.BeginPopupContextItem("", ImGuiPopupFlags.MouseButtonRight))
                    {
                        if (canEditCategory && ImGui.MenuItem("Create Category"))
                        {
                            newCategoryName = "";
                            showCreateCategoryPopup = true;
                        }
                        if (canEditForum && ImGui.MenuItem("Create Channel"))
                        {
                            ResetCreateChannelState();
                            createChannelCategoryId = category.id;
                            showCreateChannelPopup = true;
                        }
                        ImGui.Separator();
                        if (canEditCategory && ImGui.MenuItem("Rename Category"))
                        {
                            renamingCategory = true;
                            renamingCategoryIndex = catIdx;
                            renameBuffer = category.name;
                        }
                        if (canDeleteCategory && ImGui.MenuItem("Delete Category"))
                        {
                            categoryToDelete = category;
                            categoryToDeleteIndex = catIdx;
                            showDeleteCategoryConfirmation = true;
                        }
                        ImGui.EndPopup();
                    }

                    category.collapsed = !nodeOpen;

                    // Drag source for category reordering
                    if (canEditCategory && ImGui.BeginDragDropSource(ImGuiDragDropFlags.SourceNoDisableHover))
                    {
                        draggedCategory = category;
                        draggedCategoryIndex = catIdx;

                        Span<byte> payloadSpan = stackalloc byte[sizeof(int)];
                        BitConverter.TryWriteBytes(payloadSpan, catIdx);
                        ImGui.SetDragDropPayload("CATEGORY_DND", payloadSpan, ImGuiCond.Always);
                        ImGui.Text($"Moving: {category.name}");
                        ImGui.EndDragDropSource();
                    }

                    // Drag-drop target for category (to move channels into OR reorder categories)
                    if (ImGui.BeginDragDropTarget())
                    {
                        // Handle channel drops
                        var channelPayload = ImGui.AcceptDragDropPayload("CHANNEL_DND");
                        if (!channelPayload.IsNull && draggedChannel != null)
                        {
                            // Move channel to this category
                            var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                                x.characterName == Plugin.plugin.playername &&
                                x.characterWorld == Plugin.plugin.playerworld);
                            if (character != null)
                            {
                                DataSender.MoveChannel(character, currentGroup.groupID, draggedChannel.id, category.id);

                                // Update local state
                                if (draggedChannelCategoryIndex >= 0 && draggedChannelIndex >= 0)
                                {
                                    currentGroup.categories[draggedChannelCategoryIndex].channels.RemoveAt(draggedChannelIndex);
                                }
                                draggedChannel.categoryID = category.id;
                                category.channels.Add(draggedChannel);
                            }
                            draggedChannel = null;
                            draggedChannelCategoryIndex = -1;
                            draggedChannelIndex = -1;
                        }

                        // Handle category reorder drops
                        var categoryPayload = ImGui.AcceptDragDropPayload("CATEGORY_DND");
                        if (!categoryPayload.IsNull && draggedCategory != null && draggedCategoryIndex != catIdx)
                        {
                            var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                                x.characterName == Plugin.plugin.playername &&
                                x.characterWorld == Plugin.plugin.playerworld);
                            if (character != null)
                            {
                                Plugin.PluginLog.Information($"[ReorderCategory] Sending reorder: categoryID={draggedCategory.id}, fromIndex={draggedCategoryIndex}, toIndex={catIdx}");

                                // Send reorder to server
                                DataSender.ReorderCategory(character, currentGroup.groupID, draggedCategory.id, catIdx);

                                // Update local state - move category
                                var movedCategory = draggedCategory;
                                currentGroup.categories.RemoveAt(draggedCategoryIndex);
                                // Adjust insert index if we removed from before the target
                                int insertIdx = draggedCategoryIndex < catIdx ? catIdx - 1 : catIdx;
                                currentGroup.categories.Insert(insertIdx, movedCategory);
                            }
                            draggedCategory = null;
                            draggedCategoryIndex = -1;
                        }

                        ImGui.EndDragDropTarget();
                    }
                }

                if (nodeOpen && category.channels != null)
                {
                    // Check if rules exist and user hasn't agreed (non-owners only)
                    bool isOwner = GroupPermissions.IsOwner(currentGroup);
                    bool rulesExist = DataReceiver.groupRulesVersion > 0 && !string.IsNullOrEmpty(DataReceiver.groupRulesContent);
                    bool mustAgreeToRules = rulesExist && !DataReceiver.hasAgreedToRules && !isOwner;

                    for (int chIdx = 0; chIdx < category.channels.Count; chIdx++)
                    {
                        var channel = category.channels[chIdx];
                        if (channel == null)
                            continue;

                        // If user must agree to rules, only show the rules channel (type 2)
                        if (mustAgreeToRules && channel.channelType != 2)
                            continue;

                        ImGui.Indent();
                        ImGui.PushID($"ch_{chIdx}");

                        bool isSelected = selectedCategoryIndex == catIdx && selectedChannelIndex == chIdx;

                        // Handle channel renaming
                        if (renamingChannel && renamingChannelCategoryIndex == catIdx && renamingChannelIndex == chIdx)
                        {
                            ImGui.SetNextItemWidth(-1);
                            if (ImGui.InputText("##renameChannel", ref renameBuffer, 100, ImGuiInputTextFlags.EnterReturnsTrue))
                            {
                                if (!string.IsNullOrWhiteSpace(renameBuffer))
                                {
                                    var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                                        x.characterName == Plugin.plugin.playername &&
                                        x.characterWorld == Plugin.plugin.playerworld);
                                    if (character != null)
                                    {
                                        DataSender.RenameChannel(character, currentGroup.groupID, channel.id, renameBuffer);
                                        channel.name = renameBuffer;
                                    }
                                }
                                renamingChannel = false;
                                renamingChannelCategoryIndex = -1;
                                renamingChannelIndex = -1;
                            }
                            if (ImGui.IsItemDeactivated() && !ImGui.IsItemDeactivatedAfterEdit())
                            {
                                renamingChannel = false;
                                renamingChannelCategoryIndex = -1;
                                renamingChannelIndex = -1;
                            }
                        }
                        else
                        {
                            // Channel icon based on type
                            string icon = channel.channelType == 1 ? "📢" : channel.channelType == 2 ? "📜" : channel.channelType == 3 ? "🏷️" : channel.channelType == 4 ? "📝" : "#";

                            if (ImGui.Selectable($"{icon} {channel.name}##ch_{chIdx}", isSelected))
                            {
                                // Check if channel is NSFW and user hasn't agreed yet (check persisted config)
                                var agreedChannels = Plugin.plugin.Configuration.agreedNsfwChannelIds;
                                if (channel.isNsfw && !agreedChannels.Contains(channel.id))
                                {
                                    // Show NSFW warning popup
                                    pendingNsfwChannel = channel;
                                    pendingNsfwCategoryIndex = catIdx;
                                    pendingNsfwChannelIndex = chIdx;
                                    pendingNsfwCategory = category;
                                    showNsfwWarning = true;
                                }
                                else
                                {
                                    // Normal channel selection
                                    selectedCategoryIndex = catIdx;
                                    selectedChannelIndex = chIdx;
                                    selectedCategory = category;
                                    selectedChannel = channel;

                                    lock (messagesLock)
                                    {
                                        currentMessages.Clear();
                                    }
                                    ClearAvatarCache();
                                    FetchChannelMessages();
                                }
                            }

                            // Track right-clicked channel (must be checked before adding more items on same line)
                            bool channelHovered = ImGui.IsItemHovered();
                            if (channelHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                            {
                                rightClickedCategoryIndex = -1;
                                rightClickedChannelCategoryIndex = catIdx;
                                rightClickedChannelIndex = chIdx;
                            }

                            // Open context menu immediately after Selectable, before any SameLine items
                            ImGui.OpenPopupOnItemClick($"ChannelContext_{catIdx}_{chIdx}", ImGuiPopupFlags.MouseButtonRight);

                            // Show [Locked] indicator after channel name if locked
                            if (channel.isLocked)
                            {
                                ImGui.SameLine();
                                ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "[Locked]");
                            }

                            // Drag source for moving between categories
                            if (canEditForum && ImGui.BeginDragDropSource(ImGuiDragDropFlags.None))
                            {
                                draggedChannel = channel;
                                draggedChannelCategoryIndex = catIdx;
                                draggedChannelIndex = chIdx;

                                Span<byte> payloadSpan = stackalloc byte[sizeof(int)];
                                BitConverter.TryWriteBytes(payloadSpan, chIdx);
                                ImGui.SetDragDropPayload("CHANNEL_DND", payloadSpan, ImGuiCond.Always);
                                ImGui.Text($"Moving: {channel.name}");
                                ImGui.EndDragDropSource();
                            }

                            // Drag-drop target for reordering within category
                            if (canEditForum && ImGui.BeginDragDropTarget())
                            {
                                var payload = ImGui.AcceptDragDropPayload("CHANNEL_DND");
                                if (!payload.IsNull && draggedChannel != null && draggedChannelCategoryIndex == catIdx)
                                {
                                    // Reorder within same category
                                    if (draggedChannelIndex != chIdx)
                                    {
                                        var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                                            x.characterName == Plugin.plugin.playername &&
                                            x.characterWorld == Plugin.plugin.playerworld);
                                        if (character != null)
                                        {
                                            // Update server-side order
                                            DataSender.ReorderChannel(character, currentGroup.groupID, draggedChannel.id, chIdx);

                                            // Update local state
                                            category.channels.RemoveAt(draggedChannelIndex);
                                            category.channels.Insert(chIdx, draggedChannel);
                                            draggedChannel.index = chIdx;
                                        }
                                    }
                                    draggedChannel = null;
                                    draggedChannelCategoryIndex = -1;
                                    draggedChannelIndex = -1;
                                }
                                ImGui.EndDragDropTarget();
                            }

                            // Context menu for channel - opened via OpenPopupOnItemClick above
                            if (ImGui.BeginPopup($"ChannelContext_{catIdx}_{chIdx}"))
                            {
                                if (canEditForum && ImGui.MenuItem("Edit Channel"))
                                {
                                    channelBeingEdited = channel;
                                    editingChannelCategoryIndex = catIdx;
                                    editChannelName = channel.name ?? string.Empty;
                                    editChannelDescription = channel.description ?? string.Empty;
                                    editChannelType = channel.channelType;
                                    editEveryoneCanView = channel.everyoneCanView;
                                    editEveryoneCanPost = channel.everyoneCanPost;
                                    editChannelIsNsfw = channel.isNsfw;
                                    editChannelPermissionSearchQuery = string.Empty;
                                    // Initialize permission selections from channel data with canView/canPost flags
                                    editChannelPermissionSelectedRanks.Clear();
                                    editChannelPermissionSelectedRoles.Clear();
                                    editChannelPermissionSelectedMembers.Clear();

                                    Plugin.PluginLog.Info($"[EditChannel] Loading channel '{channel.name}' (id={channel.id}) everyoneCanView={channel.everyoneCanView} everyoneCanPost={channel.everyoneCanPost}");
                                    Plugin.PluginLog.Info($"[EditChannel] RankPermissions: {channel.RankPermissions?.Count ?? 0}, RolePermissions: {channel.RolePermissions?.Count ?? 0}, MemberPermissions: {channel.MemberPermissions?.Count ?? 0}");

                                    // Load rank permissions with actual canView/canPost values
                                    if (channel.RankPermissions != null && channel.RankPermissions.Count > 0)
                                    {
                                        Plugin.PluginLog.Info($"[EditChannel] Loading {channel.RankPermissions.Count} rank permissions");
                                        foreach (var rp in channel.RankPermissions)
                                        {
                                            Plugin.PluginLog.Info($"[EditChannel] RankPerm: rankID={rp.rankID}, rankName={rp.rankName}, canView={rp.canView}, canPost={rp.canPost}");
                                            // Find the rank in group ranks to get full rank data
                                            var rank = currentGroup?.ranks?.FirstOrDefault(r => r.id == rp.rankID);
                                            if (rank != null)
                                            {
                                                editChannelPermissionSelectedRanks.Add(new ChannelRankPermission { rank = rank, canView = rp.canView, canPost = rp.canPost });
                                                Plugin.PluginLog.Info($"[EditChannel] Added rank '{rank.name}' to selected");
                                            }
                                            else
                                            {
                                                Plugin.PluginLog.Warning($"[EditChannel] Could not find rank with id={rp.rankID} in currentGroup.ranks (count={currentGroup?.ranks?.Count ?? 0})");
                                            }
                                        }
                                    }

                                    // Load role permissions with actual canView/canPost values
                                    if (channel.RolePermissions != null && channel.RolePermissions.Count > 0)
                                    {
                                        Plugin.PluginLog.Info($"[EditChannel] Loading {channel.RolePermissions.Count} role permissions");
                                        foreach (var rolep in channel.RolePermissions)
                                        {
                                            Plugin.PluginLog.Info($"[EditChannel] RolePerm: roleID={rolep.roleID}, roleName={rolep.roleName}, canView={rolep.canView}, canPost={rolep.canPost}");
                                            // Find the role in self-assign roles to get full role data
                                            var role = DataReceiver.selfAssignRoles?.FirstOrDefault(r => r.id == rolep.roleID);
                                            if (role != null)
                                            {
                                                editChannelPermissionSelectedRoles.Add(new ChannelSelfRolePermission { role = role, canView = rolep.canView, canPost = rolep.canPost });
                                                Plugin.PluginLog.Info($"[EditChannel] Added role '{role.name}' to selected");
                                            }
                                            else
                                            {
                                                Plugin.PluginLog.Warning($"[EditChannel] Could not find role with id={rolep.roleID} in selfAssignRoles (count={DataReceiver.selfAssignRoles?.Count ?? 0})");
                                            }
                                        }
                                    }

                                    // Load member permissions with actual canView/canPost values
                                    if (channel.MemberPermissions != null && channel.MemberPermissions.Count > 0)
                                    {
                                        Plugin.PluginLog.Info($"[EditChannel] Loading {channel.MemberPermissions.Count} member permissions");
                                        foreach (var mp in channel.MemberPermissions)
                                        {
                                            Plugin.PluginLog.Info($"[EditChannel] MemberPerm: memberID={mp.memberID}, memberName={mp.memberName}, canView={mp.canView}, canPost={mp.canPost}");
                                            // Find the member in group members to get full member data
                                            var member = currentGroup?.members?.FirstOrDefault(m => m.id == mp.memberID);
                                            if (member != null)
                                            {
                                                editChannelPermissionSelectedMembers.Add(new ChannelMemberPermission { member = member, canView = mp.canView, canPost = mp.canPost });
                                                Plugin.PluginLog.Info($"[EditChannel] Added member '{member.name}' to selected");
                                            }
                                            else
                                            {
                                                Plugin.PluginLog.Warning($"[EditChannel] Could not find member with id={mp.memberID} in currentGroup.members (count={currentGroup?.members?.Count ?? 0})");
                                            }
                                        }
                                    }

                                    Plugin.PluginLog.Info($"[EditChannel] Final counts - Ranks: {editChannelPermissionSelectedRanks.Count}, Roles: {editChannelPermissionSelectedRoles.Count}, Members: {editChannelPermissionSelectedMembers.Count}");
                                    showEditChannelPopup = true;
                                }
                                if (canEditForum && ImGui.MenuItem("Rename Channel"))
                                {
                                    renamingChannel = true;
                                    renamingChannelCategoryIndex = catIdx;
                                    renamingChannelIndex = chIdx;
                                    renameBuffer = channel.name;
                                }
                                if (canLockForum)
                                {
                                    string lockLabel = channel.isLocked ? "Unlock Channel" : "Lock Channel";
                                    if (ImGui.MenuItem(lockLabel))
                                    {
                                        bool newLockStatus = !channel.isLocked;
                                        var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                                            x.characterName == Plugin.plugin.playername &&
                                            x.characterWorld == Plugin.plugin.playerworld);
                                        if (character != null)
                                        {
                                            DataSender.LockChannel(character, currentGroup.groupID, channel.id, newLockStatus);
                                        }
                                    }
                                }
                                if (canDeleteForum && ImGui.MenuItem("Delete Channel"))
                                {
                                    channelToDelete = channel;
                                    channelToDeleteCategoryIndex = catIdx;
                                    showDeleteChannelConfirmation = true;
                                }
                                ImGui.EndPopup();
                            }

                            // Show unread count if any
                            if (channel.unreadCount > 0 && !isSelected)
                            {
                                ImGui.SameLine();
                                ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - 30);
                                ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 1), $"({channel.unreadCount})");
                            }
                        }

                        ImGui.PopID();
                        ImGui.Unindent();
                    }
                }

                // Close tree node if it was opened
                if (nodeOpen)
                {
                    ImGui.TreePop();
                }

                ImGui.PopID();
                ImGui.Spacing();
            }

            // Right-click context menu for empty space only
            // Note: Category and channel context menus are handled inline via BeginPopupContextItem
            if (ImGui.BeginPopupContextWindow("ChannelListContext", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
            {
                // Empty space options only
                {
                    if (canEditCategory && ImGui.MenuItem("Create Category"))
                    {
                        newCategoryName = "";
                        showCreateCategoryPopup = true;
                    }
                }
                ImGui.EndPopup();
            }

            // Delete channel confirmation popup
            if (showDeleteChannelConfirmation)
            {
                ImGui.OpenPopup("Delete Channel?");
            }

            var center = ImGui.GetMainViewport().GetCenter();
            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool deletePopupOpen = true;
            if (ImGui.BeginPopupModal("Delete Channel?", ref deletePopupOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text($"Are you sure you want to delete the channel '{channelToDelete?.name}'?");
                ImGui.Text("This will delete all messages in this channel.");
                ImGui.Text("This action cannot be undone.");
                ImGui.Separator();
                ImGui.Spacing();

                float buttonWidth = 80f;
                float spacing = 10f;
                float totalWidth = (buttonWidth * 2) + spacing;
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - totalWidth) * 0.5f);

                if (ImGui.Button("Delete", new Vector2(buttonWidth, 0)))
                {
                    if (channelToDelete != null)
                    {
                        var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                            x.characterName == Plugin.plugin.playername &&
                            x.characterWorld == Plugin.plugin.playerworld);
                        if (character != null)
                        {
                            DataSender.DeleteChannel(character, currentGroup.groupID, channelToDelete.id);
                            // Remove from local list
                            if (channelToDeleteCategoryIndex >= 0 && channelToDeleteCategoryIndex < currentGroup.categories.Count)
                            {
                                var cat = currentGroup.categories[channelToDeleteCategoryIndex];
                                cat.channels?.RemoveAll(c => c.id == channelToDelete.id);
                            }
                            // Clear selection if deleted channel was selected
                            if (selectedChannel?.id == channelToDelete.id)
                            {
                                selectedChannel = null;
                            }
                        }
                        channelToDelete = null;
                        channelToDeleteCategoryIndex = -1;
                    }
                    showDeleteChannelConfirmation = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(buttonWidth, 0)))
                {
                    channelToDelete = null;
                    channelToDeleteCategoryIndex = -1;
                    showDeleteChannelConfirmation = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            if (!deletePopupOpen)
            {
                showDeleteChannelConfirmation = false;
                channelToDelete = null;
                channelToDeleteCategoryIndex = -1;
            }

            // Delete category confirmation popup
            if (showDeleteCategoryConfirmation)
            {
                ImGui.OpenPopup("Delete Category?");
            }

            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool deleteCategoryPopupOpen = true;
            if (ImGui.BeginPopupModal("Delete Category?", ref deleteCategoryPopupOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text($"Are you sure you want to delete the category '{categoryToDelete?.name}'?");
                ImGui.Text("This will delete all channels and messages in this category.");
                ImGui.Text("This action cannot be undone.");
                ImGui.Separator();
                ImGui.Spacing();

                float buttonWidthCat = 80f;
                float spacingCat = 10f;
                float totalWidthCat = (buttonWidthCat * 2) + spacingCat;
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - totalWidthCat) * 0.5f);

                if (ImGui.Button("Delete##Category", new Vector2(buttonWidthCat, 0)))
                {
                    if (categoryToDelete != null)
                    {
                        var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                            x.characterName == Plugin.plugin.playername &&
                            x.characterWorld == Plugin.plugin.playerworld);
                        if (character != null)
                        {
                            DataSender.DeleteCategory(character, currentGroup.groupID, categoryToDelete.id);
                            // Remove from local list
                            if (categoryToDeleteIndex >= 0 && categoryToDeleteIndex < currentGroup.categories.Count)
                            {
                                currentGroup.categories.RemoveAt(categoryToDeleteIndex);
                            }
                            // Clear channel selection if it was in the deleted category
                            if (selectedChannel != null && categoryToDelete.channels != null &&
                                categoryToDelete.channels.Any(c => c.id == selectedChannel.id))
                            {
                                selectedChannel = null;
                            }
                        }
                        categoryToDelete = null;
                        categoryToDeleteIndex = -1;
                    }
                    showDeleteCategoryConfirmation = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();

                if (ImGui.Button("Cancel##Category", new Vector2(buttonWidthCat, 0)))
                {
                    categoryToDelete = null;
                    categoryToDeleteIndex = -1;
                    showDeleteCategoryConfirmation = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            if (!deleteCategoryPopupOpen)
            {
                showDeleteCategoryConfirmation = false;
                categoryToDelete = null;
                categoryToDeleteIndex = -1;
            }

            // Edit Channel popup
            if (showEditChannelPopup)
            {
                ImGui.OpenPopup("Edit Channel##SidebarEdit");
            }

            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new Vector2(500, 550), ImGuiCond.FirstUseEver);

            bool editChannelPopupOpen = true;
            if (ImGui.BeginPopupModal("Edit Channel##SidebarEdit", ref editChannelPopupOpen, ImGuiWindowFlags.None))
            {
                if (channelBeingEdited == null)
                {
                    ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "Error: No channel selected");
                    if (ImGui.Button("Close"))
                    {
                        ResetEditChannelState();
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }
                else
                {
                    ImGui.Text("Edit channel");
                    ImGui.Separator();
                    ImGui.Spacing();

                    // Basic channel info
                    ImGui.Text("Channel Name:");
                    ImGui.SetNextItemWidth(-1);
                    ImGui.InputText("##EditChannelName", ref editChannelName, 100);

                    ImGui.Spacing();

                    ImGui.Text("Description (optional):");
                    ImGui.SetNextItemWidth(-1);
                    ImGui.InputText("##EditChannelDescription", ref editChannelDescription, 500);

                    ImGui.Spacing();

                    ImGui.Text("Channel Type:");
                    if (ImGui.RadioButton("Text Channel##Edit", editChannelType == 0))
                    {
                        editChannelType = 0;
                    }
                    ImGui.SameLine();
                    if (ImGui.RadioButton("Announcement##Edit", editChannelType == 1))
                    {
                        editChannelType = 1;
                    }
                    ImGui.SameLine();
                    // Check if rules channel already exists (allow if this is already the rules channel)
                    bool hasRulesChannel = currentGroup?.categories?.Any(c => c.channels?.Any(ch => ch.channelType == 2 && ch.id != channelBeingEdited.id) ?? false) ?? false;
                    if (hasRulesChannel)
                    {
                        ImGui.BeginDisabled();
                    }
                    if (ImGui.RadioButton("Rules##Edit", editChannelType == 2))
                    {
                        editChannelType = 2;
                    }
                    if (hasRulesChannel)
                    {
                        ImGui.EndDisabled();
                        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        {
                            ImGui.SetTooltip("Only one rules channel allowed per group");
                        }
                    }
                    ImGui.SameLine();
                    // Check if role selection channel already exists (allow if this is already the role selection channel)
                    bool hasRoleSelectionChannel = currentGroup?.categories?.Any(c => c.channels?.Any(ch => ch.channelType == 3 && ch.id != channelBeingEdited.id) ?? false) ?? false;
                    if (hasRoleSelectionChannel)
                    {
                        ImGui.BeginDisabled();
                    }
                    if (ImGui.RadioButton("Role Selection##Edit", editChannelType == 3))
                    {
                        editChannelType = 3;
                    }
                    if (hasRoleSelectionChannel)
                    {
                        ImGui.EndDisabled();
                        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        {
                            ImGui.SetTooltip("Only one role selection channel allowed per group");
                        }
                    }
                    ImGui.SameLine();
                    if (ImGui.RadioButton("Form##Edit", editChannelType == 4))
                    {
                        editChannelType = 4;
                    }

                    if (editChannelType == 1)
                    {
                        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Only members with announcement permission can post.");
                    }
                    else if (editChannelType == 2)
                    {
                        ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.2f, 1f), "Only the group owner can post rules. Members must agree before accessing other channels.");
                    }
                    else if (editChannelType == 3)
                    {
                        ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.8f, 1f), "Members can select self-assign roles that grant channel access.");
                    }
                    else if (editChannelType == 4)
                    {
                        ImGui.TextColored(new Vector4(0.2f, 0.6f, 1f, 1f), "Create custom forms that members can fill out and submit. View submissions in a dedicated tab.");
                    }

                    ImGui.Spacing();

                    // NSFW Channel option
                    ImGui.Checkbox("NSFW Channel##Edit", ref editChannelIsNsfw);
                    if (editChannelIsNsfw)
                    {
                        ImGui.TextColored(new Vector4(1f, 0.5f, 0.5f, 1f), "Users must agree to view adult content before entering.");
                    }

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    // Channel Permissions Section
                    ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), "Channel Permissions");
                    ImGui.Spacing();

                    // Default permissions toggle
                    ImGui.Checkbox("Everyone can view##Edit", ref editEveryoneCanView);
                    ImGui.Checkbox("Everyone can post##Edit", ref editEveryoneCanPost);

                    ImGui.Spacing();
                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Or restrict access to specific members/ranks:");
                    ImGui.Spacing();

                    // Search box for adding members/ranks
                    ImGui.Text("Search members or ranks:");
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.InputText("##EditPermissionSearch", ref editChannelPermissionSearchQuery, 100))
                    {
                        // Filter as user types
                    }

                    // Search results (members, ranks, and self-assign roles combined)
                    if (!string.IsNullOrWhiteSpace(editChannelPermissionSearchQuery))
                    {
                        using (var searchResults = ImRaii.Child("EditSearchResults", new Vector2(-1, 120), true))
                        {
                            if (searchResults)
                            {
                                string searchLower = editChannelPermissionSearchQuery.ToLower();
                                bool foundMatch = false;

                                // Show matching ranks
                                if (currentGroup.ranks != null)
                                {
                                    foreach (var rank in currentGroup.ranks.Where(r => r.name.ToLower().Contains(searchLower)))
                                    {
                                        foundMatch = true;
                                        bool alreadySelected = editChannelPermissionSelectedRanks.Any(r => r.rank.id == rank.id);
                                        if (alreadySelected)
                                        {
                                            ImGui.TextDisabled($"[Rank] {rank.name} (already added)");
                                        }
                                        else if (ImGui.Selectable($"[Rank] {rank.name}##editrank{rank.id}"))
                                        {
                                            editChannelPermissionSelectedRanks.Add(new ChannelRankPermission { rank = rank, canView = true, canPost = true });
                                            editChannelPermissionSearchQuery = "";
                                        }
                                    }
                                }

                                // Show matching self-assign roles
                                if (DataReceiver.selfAssignRoles != null)
                                {
                                    foreach (var role in DataReceiver.selfAssignRoles.Where(r => r.name.ToLower().Contains(searchLower)))
                                    {
                                        foundMatch = true;
                                        bool alreadySelected = editChannelPermissionSelectedRoles.Any(r => r.role.id == role.id);
                                        Vector4 roleColor = ParseHexColor(role.color);
                                        if (alreadySelected)
                                        {
                                            ImGui.TextDisabled($"[Role] {role.name} (already added)");
                                        }
                                        else if (ImGui.Selectable($"[Role] {role.name}##editrole{role.id}"))
                                        {
                                            editChannelPermissionSelectedRoles.Add(new ChannelSelfRolePermission { role = role, canView = true, canPost = true });
                                            editChannelPermissionSearchQuery = "";
                                        }
                                    }
                                }

                                // Show matching members
                                if (currentGroup.members != null)
                                {
                                    foreach (var member in currentGroup.members.Where(m =>
                                        (m.name?.ToLower().Contains(searchLower) ?? false)))
                                    {
                                        foundMatch = true;
                                        bool alreadySelected = editChannelPermissionSelectedMembers.Any(mp => mp.member.id == member.id);
                                        if (alreadySelected)
                                        {
                                            ImGui.TextDisabled($"[Member] {member.name} (already added)");
                                        }
                                        else if (ImGui.Selectable($"[Member] {member.name}##editmember{member.id}"))
                                        {
                                            editChannelPermissionSelectedMembers.Add(new ChannelMemberPermission { member = member, canView = true, canPost = true });
                                            editChannelPermissionSearchQuery = "";
                                        }
                                    }
                                }

                                if (!foundMatch)
                                {
                                    ImGui.TextDisabled("No matches found");
                                }
                            }
                        }
                    }

                    // Display selected members, ranks, and roles with permission toggles
                    if (editChannelPermissionSelectedRanks.Count > 0 || editChannelPermissionSelectedMembers.Count > 0 || editChannelPermissionSelectedRoles.Count > 0)
                    {
                        ImGui.Spacing();
                        ImGui.Text("Permissions:");
                        ImGui.SameLine();
                        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "(Toggle View/Post for each)");

                        using (var selectedList = ImRaii.Child("EditSelectedPermissions", new Vector2(-1, 150), true))
                        {
                            if (selectedList)
                            {
                                // Column headers
                                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Name");
                                ImGui.SameLine(200);
                                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "View");
                                ImGui.SameLine(250);
                                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Post");
                                ImGui.SameLine(300);
                                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "");
                                ImGui.Separator();

                                // Show selected ranks with permission toggles
                                for (int i = editChannelPermissionSelectedRanks.Count - 1; i >= 0; i--)
                                {
                                    var rankPerm = editChannelPermissionSelectedRanks[i];
                                    ImGui.PushID($"editRankPerm{rankPerm.rank.id}");

                                    ImGui.TextColored(new Vector4(0.6f, 0.8f, 1f, 1f), $"[Rank] {rankPerm.rank.name}");
                                    ImGui.SameLine(200);
                                    ImGui.Checkbox("##view", ref rankPerm.canView);
                                    ImGui.SameLine(250);
                                    ImGui.Checkbox("##post", ref rankPerm.canPost);
                                    ImGui.SameLine(300);
                                    if (ImGui.SmallButton("X"))
                                    {
                                        editChannelPermissionSelectedRanks.RemoveAt(i);
                                    }

                                    ImGui.PopID();
                                }

                                // Show selected members with permission toggles
                                for (int i = editChannelPermissionSelectedMembers.Count - 1; i >= 0; i--)
                                {
                                    var memberPerm = editChannelPermissionSelectedMembers[i];
                                    ImGui.PushID($"editMemberPerm{memberPerm.member.id}");

                                    ImGui.TextColored(new Vector4(0.8f, 1f, 0.6f, 1f), $"[Member] {memberPerm.member.name}");
                                    ImGui.SameLine(200);
                                    ImGui.Checkbox("##view", ref memberPerm.canView);
                                    ImGui.SameLine(250);
                                    ImGui.Checkbox("##post", ref memberPerm.canPost);
                                    ImGui.SameLine(300);
                                    if (ImGui.SmallButton("X"))
                                    {
                                        editChannelPermissionSelectedMembers.RemoveAt(i);
                                    }

                                    ImGui.PopID();
                                }

                                // Show selected self-assign roles with permission toggles
                                for (int i = editChannelPermissionSelectedRoles.Count - 1; i >= 0; i--)
                                {
                                    var rolePerm = editChannelPermissionSelectedRoles[i];
                                    ImGui.PushID($"editRolePerm{rolePerm.role.id}");

                                    Vector4 roleColor = ParseHexColor(rolePerm.role.color);
                                    ImGui.TextColored(roleColor, $"[Role] {rolePerm.role.name}");
                                    ImGui.SameLine(200);
                                    ImGui.Checkbox("##view", ref rolePerm.canView);
                                    ImGui.SameLine(250);
                                    ImGui.Checkbox("##post", ref rolePerm.canPost);
                                    ImGui.SameLine(300);
                                    if (ImGui.SmallButton("X"))
                                    {
                                        editChannelPermissionSelectedRoles.RemoveAt(i);
                                    }

                                    ImGui.PopID();
                                }
                            }
                        }
                    }

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    // Action buttons
                    float buttonWidth2 = 80f;
                    float spacing2 = 10f;
                    float totalWidth2 = (buttonWidth2 * 2) + spacing2;
                    ImGui.SetCursorPosX((ImGui.GetWindowWidth() - totalWidth2) * 0.5f);

                    bool canSave = !string.IsNullOrWhiteSpace(editChannelName);
                    if (!canSave) ImGui.BeginDisabled();

                    if (ImGui.Button("Save##EditCh", new Vector2(buttonWidth2, 0)))
                    {
                        var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                            x.characterName == Plugin.plugin.playername &&
                            x.characterWorld == Plugin.plugin.playerworld);
                        if (character != null && channelBeingEdited != null)
                        {
                            // Collect permission data with individual view/post flags
                            var memberPermissions = editChannelPermissionSelectedMembers.Select(m => new ChannelPermissionEntry
                            {
                                id = m.member.id,
                                canView = m.canView,
                                canPost = m.canPost
                            }).ToList();

                            var rankPermissions = editChannelPermissionSelectedRanks.Select(r => new ChannelPermissionEntry
                            {
                                id = r.rank.id,
                                canView = r.canView,
                                canPost = r.canPost
                            }).ToList();

                            var rolePermissions = editChannelPermissionSelectedRoles.Select(r => new ChannelPermissionEntry
                            {
                                id = r.role.id,
                                canView = r.canView,
                                canPost = r.canPost
                            }).ToList();

                            // Send channel update with permissions
                            DataSender.UpdateChannelWithPermissions(
                                character,
                                currentGroup.groupID,
                                channelBeingEdited.id,
                                editChannelName,
                                editChannelDescription,
                                editChannelType,
                                editChannelIsNsfw,
                                editEveryoneCanView,
                                editEveryoneCanPost,
                                memberPermissions,
                                rankPermissions,
                                rolePermissions);

                            DataSender.FetchGroupCategories(Plugin.character, currentGroup.groupID);
                        }
                        ResetEditChannelState();
                        ImGui.CloseCurrentPopup();
                    }

                    if (!canSave) ImGui.EndDisabled();

                    ImGui.SameLine();

                    if (ImGui.Button("Cancel##EditCh", new Vector2(buttonWidth2, 0)))
                    {
                        ResetEditChannelState();
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.EndPopup();
                }
            }

            if (!editChannelPopupOpen)
            {
                ResetEditChannelState();
            }

            // NSFW warning popup
            if (showNsfwWarning)
            {
                ImGui.OpenPopup("NSFW Content Warning##NsfwWarning");
            }

            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool nsfwPopupOpen = true;
            if (ImGui.BeginPopupModal("NSFW Content Warning##NsfwWarning", ref nsfwPopupOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.3f, 0.3f, 1f));
                ImGui.TextUnformatted("WARNING: NSFW Content");
                ImGui.PopStyleColor();

                ImGui.Separator();
                ImGui.Spacing();

                ImGui.TextWrapped("This channel contains sensitive and adult themes including but not limited to nudity and other mature content.");
                ImGui.Spacing();
                ImGui.TextWrapped("By clicking 'I Agree', you confirm that you are of legal age to view such content in your jurisdiction and consent to viewing it.");
                ImGui.Spacing();

                float buttonWidth = 120f;
                float spacing = 10f;
                float totalWidth = (buttonWidth * 2) + spacing;
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - totalWidth) * 0.5f);

                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.2f, 0.2f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.7f, 0.3f, 0.3f, 1f));
                if (ImGui.Button("I Agree", new Vector2(buttonWidth, 0)))
                {
                    if (pendingNsfwChannel != null)
                    {
                        // Mark this channel as agreed and persist to configuration
                        Plugin.plugin.Configuration.agreedNsfwChannelIds.Add(pendingNsfwChannel.id);
                        Plugin.plugin.Configuration.Save();

                        // Now select the channel
                        selectedCategoryIndex = pendingNsfwCategoryIndex;
                        selectedChannelIndex = pendingNsfwChannelIndex;
                        selectedCategory = pendingNsfwCategory;
                        selectedChannel = pendingNsfwChannel;

                        lock (messagesLock)
                        {
                            currentMessages.Clear();
                        }
                        ClearAvatarCache();
                        FetchChannelMessages();
                    }

                    // Clear pending state
                    pendingNsfwChannel = null;
                    pendingNsfwCategoryIndex = -1;
                    pendingNsfwChannelIndex = -1;
                    pendingNsfwCategory = null;
                    showNsfwWarning = false;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.PopStyleColor(2);

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(buttonWidth, 0)))
                {
                    pendingNsfwChannel = null;
                    pendingNsfwCategoryIndex = -1;
                    pendingNsfwChannelIndex = -1;
                    pendingNsfwCategory = null;
                    showNsfwWarning = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            if (!nsfwPopupOpen)
            {
                showNsfwWarning = false;
                pendingNsfwChannel = null;
                pendingNsfwCategoryIndex = -1;
                pendingNsfwChannelIndex = -1;
                pendingNsfwCategory = null;
            }

            // Draw all popups
            DrawChannelListPopups(canEditCategory, canEditForum);

            ImGui.EndGroup();
        }

        private static void DrawChannelListPopups(bool canEditCategory, bool canEditForum)
        {
            var center = ImGui.GetMainViewport().GetCenter();

            // Create category popup
            if (showCreateCategoryPopup)
            {
                ImGui.OpenPopup("Create Category##CreateCatPopup");
            }

            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool createCatPopupOpen = true;
            if (ImGui.BeginPopupModal("Create Category##CreateCatPopup", ref createCatPopupOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text("Create a new category");
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.Text("Category Name:");
                ImGui.SetNextItemWidth(250);
                ImGui.InputText("##CategoryName", ref newCategoryName, 100);

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                float buttonWidthCat = 80f;
                float spacingCat = 10f;
                float totalWidthCat = (buttonWidthCat * 2) + spacingCat;
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - totalWidthCat) * 0.5f);

                bool canCreateCat = !string.IsNullOrWhiteSpace(newCategoryName);
                if (!canCreateCat) ImGui.BeginDisabled();

                if (ImGui.Button("Create##CreateCat", new Vector2(buttonWidthCat, 0)))
                {
                    var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                        x.characterName == Plugin.plugin.playername &&
                        x.characterWorld == Plugin.plugin.playerworld);
                    if (character != null)
                    {
                        DataSender.CreateCategory(character, currentGroup.groupID, newCategoryName);
                        DataSender.FetchGroupCategories(Plugin.character, currentGroup.groupID);
                    }
                    newCategoryName = "";
                    showCreateCategoryPopup = false;
                    ImGui.CloseCurrentPopup();
                }

                if (!canCreateCat) ImGui.EndDisabled();

                ImGui.SameLine();

                if (ImGui.Button("Cancel##CancelCat", new Vector2(buttonWidthCat, 0)))
                {
                    newCategoryName = "";
                    showCreateCategoryPopup = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            if (!createCatPopupOpen)
            {
                showCreateCategoryPopup = false;
                newCategoryName = "";
            }

            // Create channel popup with permissions
            if (showCreateChannelPopup)
            {
                ImGui.OpenPopup("Create Channel##CreateChPopup");
            }

            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new Vector2(500, 550), ImGuiCond.FirstUseEver);

            bool createPopupOpen = true;
            if (ImGui.BeginPopupModal("Create Channel##CreateChPopup", ref createPopupOpen, ImGuiWindowFlags.None))
            {
                ImGui.Text("Create a new channel");
                ImGui.Separator();
                ImGui.Spacing();

                // Display error message if there is one
                if (!string.IsNullOrEmpty(DataReceiver.createChannelError))
                {
                    ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), DataReceiver.createChannelError);
                    ImGui.Spacing();
                }

                // Basic channel info
                ImGui.Text("Channel Name:");
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("##ChannelName", ref newChannelName, 100);

                ImGui.Spacing();

                ImGui.Text("Description (optional):");
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("##ChannelDescription", ref newChannelDescription, 500);

                ImGui.Spacing();

                ImGui.Text("Channel Type:");
                if (ImGui.RadioButton("Text Channel", newChannelType == 0))
                {
                    newChannelType = 0;
                }
                ImGui.SameLine();
                if (ImGui.RadioButton("Announcement", newChannelType == 1))
                {
                    newChannelType = 1;
                }
                ImGui.SameLine();
                // Check if rules channel already exists
                bool hasRulesChannel = currentGroup?.categories?.Any(c => c.channels?.Any(ch => ch.channelType == 2) ?? false) ?? false;
                if (hasRulesChannel)
                {
                    ImGui.BeginDisabled();
                }
                if (ImGui.RadioButton("Rules", newChannelType == 2))
                {
                    newChannelType = 2;
                }
                if (hasRulesChannel)
                {
                    ImGui.EndDisabled();
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    {
                        ImGui.SetTooltip("Only one rules channel allowed per group");
                    }
                }
                ImGui.SameLine();
                // Check if role selection channel already exists
                bool hasRoleSelectionChannel = currentGroup?.categories?.Any(c => c.channels?.Any(ch => ch.channelType == 3) ?? false) ?? false;
                if (hasRoleSelectionChannel)
                {
                    ImGui.BeginDisabled();
                }
                if (ImGui.RadioButton("Role Selection", newChannelType == 3))
                {
                    newChannelType = 3;
                }
                if (hasRoleSelectionChannel)
                {
                    ImGui.EndDisabled();
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    {
                        ImGui.SetTooltip("Only one role selection channel allowed per group");
                    }
                }
                ImGui.SameLine();
                if (ImGui.RadioButton("Form", newChannelType == 4))
                {
                    newChannelType = 4;
                }

                if (newChannelType == 1)
                {
                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Only members with announcement permission can post.");
                }
                else if (newChannelType == 2)
                {
                    ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.2f, 1f), "Only the group owner can post rules. Members must agree before accessing other channels.");
                }
                else if (newChannelType == 3)
                {
                    ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.8f, 1f), "Members can select self-assign roles that grant channel access.");
                }
                else if (newChannelType == 4)
                {
                    ImGui.TextColored(new Vector4(0.2f, 0.6f, 1f, 1f), "Create custom forms that members can fill out and submit. View submissions in a dedicated tab.");
                }

                ImGui.Spacing();

                // NSFW Channel option
                ImGui.Checkbox("NSFW Channel", ref newChannelIsNsfw);
                if (newChannelIsNsfw)
                {
                    ImGui.TextColored(new Vector4(1f, 0.5f, 0.5f, 1f), "Users must agree to view adult content before entering.");
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Channel Permissions Section
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), "Channel Permissions");
                ImGui.Spacing();

                // Default permissions toggle
                ImGui.Checkbox("Everyone can view", ref channelPermissionEveryoneCanView);
                ImGui.Checkbox("Everyone can post", ref channelPermissionEveryoneCanPost);

                ImGui.Spacing();
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Or restrict access to specific members/ranks:");
                ImGui.Spacing();

                // Search box for adding members/ranks
                ImGui.Text("Search members or ranks:");
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##PermissionSearch", ref channelPermissionSearchQuery, 100))
                {
                    // Filter as user types
                }

                // Search results (members, ranks, and self-assign roles combined)
                if (!string.IsNullOrWhiteSpace(channelPermissionSearchQuery))
                {
                    using (var searchResults = ImRaii.Child("SearchResults", new Vector2(-1, 120), true))
                    {
                        if (searchResults)
                        {
                            string searchLower = channelPermissionSearchQuery.ToLower();
                            bool foundMatch = false;

                            // Show matching ranks
                            if (currentGroup.ranks != null)
                            {
                                foreach (var rank in currentGroup.ranks.Where(r => r.name.ToLower().Contains(searchLower)))
                                {
                                    foundMatch = true;
                                    bool alreadySelected = channelPermissionSelectedRanks.Any(r => r.rank.id == rank.id);
                                    if (alreadySelected)
                                    {
                                        ImGui.TextDisabled($"[Rank] {rank.name} (already added)");
                                    }
                                    else if (ImGui.Selectable($"[Rank] {rank.name}##rank{rank.id}"))
                                    {
                                        channelPermissionSelectedRanks.Add(new ChannelRankPermission { rank = rank, canView = true, canPost = true });
                                        channelPermissionSearchQuery = "";
                                    }
                                }
                            }

                            // Show matching self-assign roles
                            if (DataReceiver.selfAssignRoles != null)
                            {
                                foreach (var role in DataReceiver.selfAssignRoles.Where(r => r.name.ToLower().Contains(searchLower)))
                                {
                                    foundMatch = true;
                                    bool alreadySelected = channelPermissionSelectedRoles.Any(r => r.role.id == role.id);
                                    Vector4 roleColor = ParseHexColor(role.color);
                                    if (alreadySelected)
                                    {
                                        ImGui.TextDisabled($"[Role] {role.name} (already added)");
                                    }
                                    else if (ImGui.Selectable($"[Role] {role.name}##role{role.id}"))
                                    {
                                        channelPermissionSelectedRoles.Add(new ChannelSelfRolePermission { role = role, canView = true, canPost = true });
                                        channelPermissionSearchQuery = "";
                                    }
                                }
                            }

                            // Show matching members
                            if (currentGroup.members != null)
                            {
                                foreach (var member in currentGroup.members.Where(m =>
                                    (m.name?.ToLower().Contains(searchLower) ?? false)))
                                {
                                    foundMatch = true;
                                    bool alreadySelected = channelPermissionSelectedMembers.Any(m => m.member.id == member.id);
                                    if (alreadySelected)
                                    {
                                        ImGui.TextDisabled($"[Member] {member.name} (already added)");
                                    }
                                    else if (ImGui.Selectable($"[Member] {member.name}##member{member.id}"))
                                    {
                                        channelPermissionSelectedMembers.Add(new ChannelMemberPermission { member = member, canView = true, canPost = true });
                                        channelPermissionSearchQuery = "";
                                    }
                                }
                            }

                            if (!foundMatch)
                            {
                                ImGui.TextDisabled("No matches found");
                            }
                        }
                    }
                } 

                // Display selected members, ranks, and roles with permission toggles
                if (channelPermissionSelectedRanks.Count > 0 || channelPermissionSelectedMembers.Count > 0 || channelPermissionSelectedRoles.Count > 0)
                {
                    ImGui.Spacing();
                    ImGui.Text("Permissions:");
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "(Toggle View/Post for each)");

                    using (var selectedList = ImRaii.Child("SelectedPermissions", new Vector2(-1, 150), true))
                    {
                        if (selectedList)
                        {
                            // Column headers
                            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Name");
                            ImGui.SameLine(200);
                            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "View");
                            ImGui.SameLine(250);
                            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Post");
                            ImGui.SameLine(300);
                            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "");
                            ImGui.Separator();

                            // Show selected ranks with permission toggles
                            for (int i = channelPermissionSelectedRanks.Count - 1; i >= 0; i--)
                            {
                                var rankPerm = channelPermissionSelectedRanks[i];
                                ImGui.PushID($"rankPerm{rankPerm.rank.id}");

                                ImGui.TextColored(new Vector4(0.6f, 0.8f, 1f, 1f), $"[Rank] {rankPerm.rank.name}");
                                ImGui.SameLine(200);
                                ImGui.Checkbox("##view", ref rankPerm.canView);
                                ImGui.SameLine(250);
                                ImGui.Checkbox("##post", ref rankPerm.canPost);
                                ImGui.SameLine(300);
                                if (ImGui.SmallButton("X"))
                                {
                                    channelPermissionSelectedRanks.RemoveAt(i);
                                }

                                ImGui.PopID();
                            }

                            // Show selected members with permission toggles
                            for (int i = channelPermissionSelectedMembers.Count - 1; i >= 0; i--)
                            {
                                var memberPerm = channelPermissionSelectedMembers[i];
                                ImGui.PushID($"memberPerm{memberPerm.member.id}");

                                ImGui.TextColored(new Vector4(0.8f, 1f, 0.6f, 1f), $"[Member] {memberPerm.member.name}");
                                ImGui.SameLine(200);
                                ImGui.Checkbox("##view", ref memberPerm.canView);
                                ImGui.SameLine(250);
                                ImGui.Checkbox("##post", ref memberPerm.canPost);
                                ImGui.SameLine(300);
                                if (ImGui.SmallButton("X"))
                                {
                                    channelPermissionSelectedMembers.RemoveAt(i);
                                }

                                ImGui.PopID();
                            }

                            // Show selected self-assign roles with permission toggles
                            for (int i = channelPermissionSelectedRoles.Count - 1; i >= 0; i--)
                            {
                                var rolePerm = channelPermissionSelectedRoles[i];
                                ImGui.PushID($"rolePerm{rolePerm.role.id}");

                                Vector4 roleColor = ParseHexColor(rolePerm.role.color);
                                ImGui.TextColored(roleColor, $"[Role] {rolePerm.role.name}");
                                ImGui.SameLine(200);
                                ImGui.Checkbox("##view", ref rolePerm.canView);
                                ImGui.SameLine(250);
                                ImGui.Checkbox("##post", ref rolePerm.canPost);
                                ImGui.SameLine(300);
                                if (ImGui.SmallButton("X"))
                                {
                                    channelPermissionSelectedRoles.RemoveAt(i);
                                }

                                ImGui.PopID();
                            }
                        }
                    }
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Action buttons
                float buttonWidth2 = 80f;
                float spacing2 = 10f;
                float totalWidth2 = (buttonWidth2 * 2) + spacing2;
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - totalWidth2) * 0.5f);

                bool canCreate = !string.IsNullOrWhiteSpace(newChannelName);
                if (!canCreate) ImGui.BeginDisabled();

                if (ImGui.Button("Create##CreateCh", new Vector2(buttonWidth2, 0)))
                {
                    var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                        x.characterName == Plugin.plugin.playername &&
                        x.characterWorld == Plugin.plugin.playerworld);
                    if (character != null && createChannelCategoryId > 0)
                    {
                        // Collect permission data with individual view/post flags
                        var memberPermissions = channelPermissionSelectedMembers.Select(m => new ChannelPermissionEntry
                        {
                            id = m.member.id,
                            canView = m.canView,
                            canPost = m.canPost
                        }).ToList();

                        var rankPermissions = channelPermissionSelectedRanks.Select(r => new ChannelPermissionEntry
                        {
                            id = r.rank.id,
                            canView = r.canView,
                            canPost = r.canPost
                        }).ToList();

                        var rolePermissions = channelPermissionSelectedRoles.Select(r => new ChannelPermissionEntry
                        {
                            id = r.role.id,
                            canView = r.canView,
                            canPost = r.canPost
                        }).ToList();

                        // Send channel creation with permissions
                        DataSender.CreateChannelWithPermissions(
                            character,
                            currentGroup.groupID,
                            createChannelCategoryId,
                            newChannelName,
                            newChannelDescription,
                            newChannelType,
                            newChannelIsNsfw,
                            channelPermissionEveryoneCanView,
                            channelPermissionEveryoneCanPost,
                            memberPermissions,
                            rankPermissions,
                            rolePermissions);

                        DataSender.FetchGroupCategories(Plugin.character, currentGroup.groupID);
                    }
                    ResetCreateChannelState();
                    ImGui.CloseCurrentPopup();
                }

                if (!canCreate) ImGui.EndDisabled();

                ImGui.SameLine();

                if (ImGui.Button("Cancel##CancelCh", new Vector2(buttonWidth2, 0)))
                {
                    ResetCreateChannelState();
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            if (!createPopupOpen)
            {
                ResetCreateChannelState();
            }
        }

        private static void ResetCreateChannelState()
        {
            newChannelName = "";
            newChannelDescription = "";
            newChannelType = 0;
            newChannelIsNsfw = false;
            createChannelCategoryId = -1;
            showCreateChannelPopup = false;
            channelPermissionSearchQuery = "";
            channelPermissionSelectedMembers.Clear();
            channelPermissionSelectedRanks.Clear();
            channelPermissionSelectedRoles.Clear();
            channelPermissionEveryoneCanView = true;
            channelPermissionEveryoneCanPost = true;
            channelPermissionTabIndex = 0;
            DataReceiver.createChannelError = string.Empty;
        }

        private static void ResetEditChannelState()
        {
            showEditChannelPopup = false;
            channelBeingEdited = null;
            editingChannelCategoryIndex = -1;
            editChannelName = string.Empty;
            editChannelDescription = string.Empty;
            editChannelType = 0;
            editEveryoneCanView = true;
            editEveryoneCanPost = true;
            editChannelIsNsfw = false;
            editChannelPermissionSearchQuery = string.Empty;
            editChannelPermissionSelectedMembers.Clear();
            editChannelPermissionSelectedRanks.Clear();
            editChannelPermissionSelectedRoles.Clear();
        }

        private static void DrawMemberManagementPopups()
        {
            var center = ImGui.GetMainViewport().GetCenter();

            // Kick member confirmation popup
            if (showKickMemberConfirmation)
            {
                ImGui.OpenPopup("Kick Member?");
            }

            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool kickPopupOpen = true;
            if (ImGui.BeginPopupModal("Kick Member?", ref kickPopupOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text($"Are you sure you want to kick '{memberToManage?.name}'?");
                ImGui.Text("They will be removed from the group but can be re-invited.");
                ImGui.Separator();
                ImGui.Spacing();

                float kickButtonWidth = 80f;
                float kickSpacing = 10f;
                float kickTotalWidth = (kickButtonWidth * 2) + kickSpacing;
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - kickTotalWidth) * 0.5f);

                if (ImGui.Button("Kick", new Vector2(kickButtonWidth, 0)))
                {
                    if (memberToManage != null)
                    {
                        var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                            x.characterName == Plugin.plugin.playername &&
                            x.characterWorld == Plugin.plugin.playerworld);
                        if (character != null)
                        {
                            DataSender.KickGroupMember(character, memberToManage.id, currentGroup.groupID);
                            currentGroup.members?.RemoveAll(m => m.id == memberToManage.id);
                        }
                    }
                    memberToManage = null;
                    showKickMemberConfirmation = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();

                if (ImGui.Button("Cancel##Kick", new Vector2(kickButtonWidth, 0)))
                {
                    memberToManage = null;
                    showKickMemberConfirmation = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            if (!kickPopupOpen)
            {
                showKickMemberConfirmation = false;
                memberToManage = null;
            }

            // Ban member confirmation popup
            if (showBanMemberConfirmation)
            {
                ImGui.OpenPopup("Ban Member?");
            }

            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool banPopupOpen = true;
            if (ImGui.BeginPopupModal("Ban Member?", ref banPopupOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text($"Are you sure you want to ban '{memberToManage?.name}'?");
                ImGui.TextColored(new Vector4(1f, 0.5f, 0.5f, 1f), "This member will be permanently banned from the group.");
                ImGui.Separator();
                ImGui.Spacing();

                float banButtonWidth = 80f;
                float banSpacing = 10f;
                float banTotalWidth = (banButtonWidth * 2) + banSpacing;
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - banTotalWidth) * 0.5f);

                if (ImGui.Button("Ban", new Vector2(banButtonWidth, 0)))
                {
                    if (memberToManage != null)
                    {
                        var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                            x.characterName == Plugin.plugin.playername &&
                            x.characterWorld == Plugin.plugin.playerworld);
                        if (character != null)
                        {
                            DataSender.BanGroupMember(character, memberToManage.id, memberToManage.userID,
                                memberToManage.profileID, memberToManage.lodestoneURL ?? "", currentGroup.groupID);
                            currentGroup.members?.RemoveAll(m => m.id == memberToManage.id);
                            // Refresh bans list after a short delay to allow server to process
                            var groupID = currentGroup.groupID;
                            Task.Run(async () =>
                            {
                                await Task.Delay(500);
                                DataSender.FetchGroupBans(character, groupID);
                            });
                        }
                    }
                    memberToManage = null;
                    showBanMemberConfirmation = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();

                if (ImGui.Button("Cancel##Ban", new Vector2(banButtonWidth, 0)))
                {
                    memberToManage = null;
                    showBanMemberConfirmation = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            if (!banPopupOpen)
            {
                showBanMemberConfirmation = false;
                memberToManage = null;
            }

            // Promote/Demote member popup (change rank)
            if (showPromoteMemberPopup)
            {
                ImGui.OpenPopup("Change Member Rank");
            }

            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool promotePopupOpen = true;
            if (ImGui.BeginPopupModal("Change Member Rank", ref promotePopupOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text($"Change rank for '{memberToManage?.name}'");
                ImGui.Separator();
                ImGui.Spacing();

                // Get current user's hierarchy to filter available ranks
                var currentUser = currentGroup.members?.FirstOrDefault(m => m.userID == DataSender.userID);
                bool isOwner = currentUser?.owner == true;
                int currentUserHierarchy = isOwner ? int.MaxValue : (currentUser?.rank?.hierarchy ?? 0);

                // Show available ranks (only those below current user's rank)
                if (currentGroup.ranks != null && currentGroup.ranks.Count > 0)
                {
                    ImGui.Text("Select new rank:");
                    ImGui.Spacing();

                    foreach (var rank in currentGroup.ranks.OrderByDescending(r => r.hierarchy))
                    {
                        // Can only assign ranks below your own hierarchy (owners can assign any)
                        if (isOwner || rank.hierarchy < currentUserHierarchy)
                        {
                            bool isCurrentRank = memberToManage?.rank?.id == rank.id;
                            string label = isCurrentRank ? $"{rank.name} (Current)" : rank.name;

                            if (ImGui.Selectable(label, isCurrentRank))
                            {
                                if (!isCurrentRank && memberToManage != null)
                                {
                                    var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                                        x.characterName == Plugin.plugin.playername &&
                                        x.characterWorld == Plugin.plugin.playerworld);
                                    if (character != null)
                                    {
                                        DataSender.AssignMemberRank(character, memberToManage.id, rank.id, currentGroup.groupID);
                                        // Update local member rank
                                        if (memberToManage != null)
                                        {
                                            memberToManage.rank = rank;
                                        }
                                    }
                                }
                                memberToManage = null;
                                showPromoteMemberPopup = false;
                                ImGui.CloseCurrentPopup();
                            }
                        }
                    }

                    // Option to remove rank
                    ImGui.Spacing();
                    ImGui.Separator();
                    if (ImGui.Selectable("Remove Rank"))
                    {
                        if (memberToManage != null)
                        {
                            var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                                x.characterName == Plugin.plugin.playername &&
                                x.characterWorld == Plugin.plugin.playerworld);
                            if (character != null)
                            {
                                DataSender.RemoveMemberRank(character, memberToManage.id, currentGroup.groupID);
                                memberToManage.rank = null;
                            }
                        }
                        memberToManage = null;
                        showPromoteMemberPopup = false;
                        ImGui.CloseCurrentPopup();
                    }
                }
                else
                {
                    ImGui.TextDisabled("No ranks available in this group.");
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                float promoteButtonWidth = 80f;
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - promoteButtonWidth) * 0.5f);

                if (ImGui.Button("Cancel##Promote", new Vector2(promoteButtonWidth, 0)))
                {
                    memberToManage = null;
                    showPromoteMemberPopup = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            if (!promotePopupOpen)
            {
                showPromoteMemberPopup = false;
                memberToManage = null;
            }

            // Leave group confirmation popup
            if (showLeaveGroupConfirmation)
            {
                ImGui.OpenPopup("Leave Group?");
            }

            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool leavePopupOpen = true;
            if (ImGui.BeginPopupModal("Leave Group?", ref leavePopupOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text($"Are you sure you want to leave '{groupToLeave?.name}'?");
                ImGui.TextColored(new Vector4(1f, 0.7f, 0.3f, 1f), "You will need to be re-invited to rejoin this group.");
                ImGui.Separator();
                ImGui.Spacing();

                float leaveButtonWidth = 80f;
                float leaveSpacing = 10f;
                float leaveTotalWidth = (leaveButtonWidth * 2) + leaveSpacing;
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - leaveTotalWidth) * 0.5f);

                if (ImGui.Button("Leave", new Vector2(leaveButtonWidth, 0)))
                {
                    if (groupToLeave != null)
                    {
                        var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                            x.characterName == Plugin.plugin.playername &&
                            x.characterWorld == Plugin.plugin.playerworld);
                        if (character != null)
                        {
                            DataSender.LeaveGroup(character, groupToLeave.groupID);
                            // Remove group from local list
                            groups.RemoveAll(g => g.groupID == groupToLeave.groupID);
                            // Reset selection if we left the current group
                            if (currentGroup?.groupID == groupToLeave.groupID)
                            {
                                currentGroup = null;
                                selectedNavIndex = groups.Count > 0 ? 0 : -1;
                                previousNavIndex = -1;
                            }
                        }
                    }
                    groupToLeave = null;
                    showLeaveGroupConfirmation = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();

                if (ImGui.Button("Cancel##Leave", new Vector2(leaveButtonWidth, 0)))
                {
                    groupToLeave = null;
                    showLeaveGroupConfirmation = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            if (!leavePopupOpen)
            {
                showLeaveGroupConfirmation = false;
                groupToLeave = null;
            }
        }

        private static void DrawChatArea()
        {
            if (selectedChannel == null)
            {
                ImGui.BeginGroup();
                ImGui.SetCursorPos(new Vector2(
                    ImGui.GetContentRegionAvail().X / 2 - 100,
                    ImGui.GetContentRegionAvail().Y / 2 - 20
                ));
                ImGui.TextDisabled("Select a channel to view messages");
                ImGui.EndGroup();
                return;
            }

            ImGui.BeginGroup();

            // Channel header with pinned button on right
            float availableWidth = ImGui.GetContentRegionAvail().X;

            // Pinned messages button (aligned to right side of header)
            float buttonWidth = 70;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + availableWidth - buttonWidth);
            if (ImGui.SmallButton("Pinned"))
            {
                showPinnedMessagesPopup = true;
                DataReceiver.pinnedMessagesLoaded = false;

                // Fetch pinned messages from server
                var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                    x.characterName == Plugin.plugin.playername &&
                    x.characterWorld == Plugin.plugin.playerworld);
                if (character != null && currentGroup != null && selectedChannel != null)
                {
                    DataSender.FetchPinnedMessages(character, currentGroup.groupID, selectedChannel.id);
                }
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("View pinned messages in this channel");
            }

            // Channel name and info on next line
            string channelIcon = selectedChannel.channelType == 1 ? "📢" : selectedChannel.channelType == 2 ? "📜" : selectedChannel.channelType == 3 ? "🏷️" : selectedChannel.channelType == 4 ? "📝" : "#";
            ImGui.Text($"{channelIcon} {selectedChannel.name}");
            if (selectedChannel.isLocked)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "[Locked]");
            }
            if (!string.IsNullOrEmpty(selectedChannel.description))
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"- {selectedChannel.description}");
            }

            ImGui.Separator();

            // Handle special channel types
            if (selectedChannel.channelType == 2)
            {
                // Rules channel - special UI
                DrawRulesChannelContent();
            }
            else if (selectedChannel.channelType == 3)
            {
                // Role Selection channel - special UI
                DrawRoleSelectionChannelContent();
            }
            else if (selectedChannel.channelType == 4)
            {
                // Form channel - special UI
                DrawFormChannelContent();
            }
            else
            {
                // Normal text/announcement channel - show messages and input
                // Messages area - calculate height to leave room for resizable chat input
                // Account for: resize handle (6px) + chat input (variable) + send button row + spacing
                float resizeHandleHeight = 6f;
                float scaledChatInputHeight = chatInputHeight * ImGui.GetIO().FontGlobalScale;
                float inputAreaHeight = resizeHandleHeight + scaledChatInputHeight + 20f; // 20f for spacing/margins
                using (var messagesChild = ImRaii.Child("Messages", new Vector2(-1, -inputAreaHeight), true))
                {
                    if (messagesChild)
                    {
                        try
                        {
                            DrawMessages();
                        }
                        catch (Exception ex)
                        {
                            Plugin.PluginLog.Debug($"[DrawChatArea] Error in DrawMessages: {ex.Message}");
                            Plugin.PluginLog.Debug($"[DrawChatArea] Stack trace: {ex.StackTrace}");
                            ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "Error loading messages. Retrying...");
                        }
                    }
                }

                // Input area
                ImGui.Spacing();
                DrawMessageInput();
            }

            ImGui.EndGroup();
        }

        private static void DrawMessages()
        {
            // Create a snapshot of messages to avoid holding the lock during rendering
            List<GroupChatMessage> messagesToRender;
            lock (messagesLock)
            {
                if (currentMessages == null || currentMessages.Count == 0)
                {
                    ImGui.TextDisabled("No messages yet. Be the first to say something!");
                    return;
                }

                // Create a shallow copy of the list to iterate safely
                messagesToRender = new List<GroupChatMessage>(currentMessages);
            }

            // Now render outside the lock
            bool isOwnMessage = false;

            foreach (var message in messagesToRender)
            {
                if (message == null || message.deleted)
                    continue;

                ImGui.PushID($"msg_{message.messageID}");

                // Check if we need to scroll to this message
                if (scrollToMessageID > 0 && message.messageID == scrollToMessageID)
                {
                    ImGui.SetScrollHereY(0.5f); // Center the message vertically
                    scrollToMessageID = 0; // Reset after scrolling
                }

                // Check if this is the current user's message
                isOwnMessage = message.senderUserID == DataSender.userID;

                // Get avatar from message (sent with each message from server)
                // Capture reference locally to avoid race conditions
                IDalamudTextureWrap avatarTexture = message.avatar;

                // Draw avatar (either from member data or placeholder)
                bool avatarDrawn = false;
                if (IsTextureValid(avatarTexture))
                {
                    try
                    {
                        var handle = avatarTexture.Handle;
                        if (handle != default)
                        {
                            ImGui.Image(handle, new Vector2(40, 40));
                            ImGui.SameLine();
                            avatarDrawn = true;
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Texture was disposed between validation and use - silently ignore
                    }
                    catch (Exception ex)
                    {
                        Plugin.PluginLog.Warning($"[DrawMessages] Failed to draw avatar for message {message.messageID}: {ex.Message}");
                        // Fall through to placeholder
                    }
                }

                if (!avatarDrawn)
                {
                    // Draw placeholder avatar circle
                    var drawList = ImGui.GetWindowDrawList();
                    var cursorPos = ImGui.GetCursorScreenPos();
                    var center = new Vector2(cursorPos.X + 20, cursorPos.Y + 20);
                    drawList.AddCircleFilled(center, 20, ImGui.GetColorU32(new Vector4(0.3f, 0.3f, 0.3f, 1f)));

                    // Draw first letter of name
                    if (!string.IsNullOrEmpty(message.senderName))
                    {
                        var letter = message.senderName[0].ToString().ToUpper();
                        var textSize = ImGui.CalcTextSize(letter);
                        drawList.AddText(new Vector2(center.X - textSize.X / 2, center.Y - textSize.Y / 2),
                            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), letter);
                    }

                    ImGui.Dummy(new Vector2(40, 40));
                    ImGui.SameLine();
                }

                ImGui.BeginGroup();

                // Sender name and timestamp
                ImGui.TextColored(new Vector4(0.7f, 0.9f, 1.0f, 1.0f), message.senderName ?? "Unknown");

                // Tooltip for sender showing their rank and roles
                if (ImGui.IsItemHovered() && currentGroup != null && currentGroup.members != null)
                {
                    var senderMember = currentGroup.members.FirstOrDefault(m => m.userID == message.senderUserID);
                    if (senderMember != null)
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text(senderMember.name ?? "Unknown");

                        if (senderMember.owner)
                        {
                            ImGui.TextColored(new Vector4(1f, 0.84f, 0f, 1f), "Owner");
                        }
                        else if (senderMember.rank != null && !string.IsNullOrEmpty(senderMember.rank.name))
                        {
                            ImGui.TextColored(new Vector4(0.6f, 0.8f, 1f, 1f), $"Rank: {senderMember.rank.name}");
                        }

                        if (senderMember.selfAssignedRoles != null && senderMember.selfAssignedRoles.Count > 0)
                        {
                            ImGui.Separator();
                            ImGui.Text("Roles:");
                            foreach (var role in senderMember.selfAssignedRoles)
                            {
                                Vector4 roleColor = ParseHexColor(role.color);
                                ImGui.TextColored(roleColor, $"  • {role.name}");
                            }
                        }

                        ImGui.EndTooltip();
                    }
                }

                ImGui.SameLine();

                var time = DateTimeOffset.FromUnixTimeMilliseconds(message.timestamp).ToLocalTime();
                ImGui.TextDisabled($"{time:HH:mm}");

                if (message.isEdited && message.editedTimestamp.HasValue)
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled("(edited)");
                }

                // Show pin indicator if message is pinned
                if (message.isPinned)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(0.3f, 0.9f, 0.3f, 1f), "[Pinned]");
                }

                // Message content - render with special embed support
                RenderMessageWithEmbeds(message.messageContent ?? string.Empty, message.messageID);

                ImGui.EndGroup();

                // Make the entire message clickable for context menu
                var messageMin = ImGui.GetItemRectMin();
                var messageMax = ImGui.GetItemRectMax();
                var messageSize = new Vector2(messageMax.X - messageMin.X, messageMax.Y - messageMin.Y);

                // Invisible button to capture right-clicks
                ImGui.SetCursorScreenPos(messageMin);
                ImGui.InvisibleButton($"msgArea_{message.messageID}", messageSize);

                // Check permissions for moderation
                bool canEdit = isOwnMessage;
                bool canDelete = isOwnMessage;

                // Debug log
                if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    Plugin.PluginLog.Info($"[GroupChat] Right-clicked message {message.messageID}, isOwnMessage={isOwnMessage}, senderUserID={message.senderUserID}, myUserID={DataSender.userID}");
                }

                // Check permissions for member management
                bool canKick = false;
                bool canBan = false;
                bool canPromote = false;
                bool canDemote = false;
                bool canPin = false;
                bool canManageThisMember = false;
                GroupMember currentUserMember = null;
                GroupMember targetMember = null;

                // Check pin permission for current user
                if (currentGroup != null && currentGroup.members != null)
                {
                    var myMember = currentGroup.members.FirstOrDefault(m => m.userID == DataSender.userID);
                    if (myMember != null)
                    {
                        bool isOwner = myMember.owner;
                        canPin = isOwner || (myMember.rank?.permissions?.canPinMessages == true);
                    }
                }

                if (!isOwnMessage && currentGroup != null && currentGroup.members != null)
                {
                    currentUserMember = currentGroup.members.FirstOrDefault(m => m.userID == DataSender.userID);
                    targetMember = currentGroup.members.FirstOrDefault(m => m.userID == message.senderUserID);

                    if (currentUserMember != null)
                    {
                        bool isCurrentUserOwner = currentUserMember.owner;

                        // Check delete others messages permission
                        if (currentUserMember.rank != null && currentUserMember.rank.permissions != null)
                        {
                            canDelete = currentUserMember.rank.permissions.canDeleteOthersMessages || isCurrentUserOwner;
                        }
                        else if (isCurrentUserOwner)
                        {
                            canDelete = true;
                        }

                        // Check member management permissions
                        if (targetMember != null && !targetMember.owner) // Can't manage owners
                        {
                            // Check hierarchy - can only manage members with lower rank
                            int currentUserHierarchy = isCurrentUserOwner ? int.MaxValue : (currentUserMember.rank?.hierarchy ?? 0);
                            int targetHierarchy = targetMember.rank?.hierarchy ?? 0;
                            canManageThisMember = currentUserHierarchy > targetHierarchy;

                            if (canManageThisMember || isCurrentUserOwner)
                            {
                                var perms = currentUserMember.rank?.permissions;
                                canKick = isCurrentUserOwner || (perms?.canKick == true);
                                canBan = isCurrentUserOwner || (perms?.canBan == true);
                                canPromote = isCurrentUserOwner || (perms?.canPromote == true);
                                canDemote = isCurrentUserOwner || (perms?.canDemote == true);
                            }
                        }
                    }
                }

                // Context menu (own messages, moderation permissions, or member management)
                bool hasAnyContextOption = canEdit || canDelete || canKick || canBan || canPromote || canDemote || canPin;
                if (hasAnyContextOption && ImGui.BeginPopupContextItem($"msgContext_{message.messageID}"))
                {
                    Plugin.PluginLog.Info($"[GroupChat] Context menu OPENED for message {message.messageID}");

                    // Message options
                    if (canEdit && ImGui.MenuItem("Edit Message"))
                    {
                        Plugin.PluginLog.Info($"[GroupChat] Edit clicked for message {message.messageID}");
                        StartEditingMessage(message);
                        ImGui.CloseCurrentPopup();
                    }
                    if (canDelete && ImGui.MenuItem("Delete Message"))
                    {
                        Plugin.PluginLog.Info($"[GroupChat] Delete clicked for message {message.messageID}");
                        messageToDelete = message;
                        showDeleteConfirmation = true;
                        ImGui.CloseCurrentPopup();
                    }
                    if (canPin)
                    {
                        string pinLabel = message.isPinned ? "Unpin Message" : "Pin Message";
                        if (ImGui.MenuItem(pinLabel))
                        {
                            Plugin.PluginLog.Info($"[GroupChat] {pinLabel} clicked for message {message.messageID}");
                            var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                                x.characterName == Plugin.plugin.playername &&
                                x.characterWorld == Plugin.plugin.playerworld);
                            if (character != null)
                            {
                                DataSender.PinGroupChatMessage(character, message.messageID, !message.isPinned);
                            }
                            ImGui.CloseCurrentPopup();
                        }
                    }

                    // Member management options (only for other members' messages)
                    if (!isOwnMessage && targetMember != null && (canKick || canBan || canPromote || canDemote))
                    {
                        ImGui.Separator();
                        ImGui.TextDisabled($"Member: {message.senderName}");

                        if ((canPromote || canDemote) && ImGui.MenuItem("Change Rank"))
                        {
                            memberToManage = targetMember;
                            showPromoteMemberPopup = true;
                            ImGui.CloseCurrentPopup();
                        }
                        if (canKick && ImGui.MenuItem("Kick from Group"))
                        {
                            memberToManage = targetMember;
                            showKickMemberConfirmation = true;
                            ImGui.CloseCurrentPopup();
                        }
                        if (canBan && ImGui.MenuItem("Ban from Group"))
                        {
                            memberToManage = targetMember;
                            showBanMemberConfirmation = true;
                            ImGui.CloseCurrentPopup();
                        }
                    }
                    ImGui.EndPopup();
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.PopID();
            }

            // Auto-scroll to bottom
            if (autoScroll && ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
            {
                ImGui.SetScrollHereY(1.0f);
            }
        }

        private static void DrawRulesChannelContent()
        {
            var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                x.characterName == Plugin.plugin.playername &&
                x.characterWorld == Plugin.plugin.playerworld);

            if (currentGroup == null || character == null) return;

            bool isOwner = GroupPermissions.IsOwner(currentGroup);

            // Use data from DataReceiver
            string rulesContent = DataReceiver.groupRulesContent;
            int rulesVersion = DataReceiver.groupRulesVersion;
            bool hasAgreed = DataReceiver.hasAgreedToRules;

            // Full height content area for rules
            using (var rulesChild = ImRaii.Child("RulesContent", new Vector2(-1, -1), true))
            {
                if (rulesChild)
                {
                    if (isOwner)
                    {
                        // Owner view - can edit rules
                        ImGui.TextColored(new Vector4(0.3f, 0.9f, 0.3f, 1f), "Group Rules (Owner View)");
                        ImGui.Separator();

                        if (!isEditingRules)
                        {
                            // Display current rules
                            if (!string.IsNullOrEmpty(rulesContent))
                            {
                                ImGui.TextWrapped(rulesContent);
                            }
                            else
                            {
                                ImGui.TextDisabled("No rules have been set yet.");
                            }

                            ImGui.Spacing();
                            ImGui.Spacing();

                            if (ImGui.Button("Edit Rules"))
                            {
                                rulesEditContent = rulesContent ?? string.Empty;
                                isEditingRules = true;
                            }

                            if (rulesVersion > 0)
                            {
                                ImGui.SameLine();
                                ImGui.TextDisabled($"Version: {rulesVersion}");
                            }
                        }
                        else
                        {
                            // Edit mode
                            ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "Warning: Saving rules will require all members to re-agree.");
                            ImGui.Spacing();

                            ImGui.Text("Rules Content:");
                            float textHeight = ImGui.GetContentRegionAvail().Y - 60;
                            ImGui.InputTextMultiline("##RulesEdit", ref rulesEditContent, 10000, new Vector2(-1, textHeight));

                            ImGui.Spacing();

                            if (ImGui.Button("Save Rules"))
                            {
                                DataSender.SaveGroupRules(character, currentGroup.groupID, rulesEditContent);
                                isEditingRules = false;
                            }
                            ImGui.SameLine();
                            if (ImGui.Button("Cancel"))
                            {
                                isEditingRules = false;
                                rulesEditContent = string.Empty;
                            }
                        }
                    }
                    else
                    {
                        // Member view - show rules and agree button
                        ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.3f, 1f), "Group Rules");
                        ImGui.Separator();

                        if (!string.IsNullOrEmpty(rulesContent))
                        {
                            Misc.RenderHtmlElements(rulesContent, true, true, true, false, null, false, true);
                        }
                        else
                        {
                            ImGui.TextDisabled("No rules have been set yet.");
                        }

                        ImGui.Spacing();
                        ImGui.Spacing();
                        ImGui.Separator();

                        if (hasAgreed)
                        {
                            ImGui.TextColored(new Vector4(0.3f, 0.9f, 0.3f, 1f), "✓ You have agreed to the current rules.");
                        }
                        else if (!string.IsNullOrEmpty(rulesContent))
                        {
                            ImGui.TextColored(new Vector4(1f, 0.5f, 0.2f, 1f), "You must agree to the rules to access other channels.");
                            ImGui.Spacing();

                            if (ImGui.Button("I Agree to These Rules", new Vector2(200, 30)))
                            {
                                DataSender.AgreeToGroupRules(character, currentGroup.groupID, rulesVersion);
                            }
                        }
                    }
                }
            }
        }

        // Form channel state
        private static int formTabIndex = 0; // 0 = Fill Form, 1 = Submissions (admin only)
        private static Dictionary<int, string> formInputValues = new Dictionary<int, string>();
        private static int? editingFormFieldId = null;
        private static string editFormFieldTitle = "";
        private static int editFormFieldType = 0;
        private static bool editFormFieldOptional = false;
        private static string newFormFieldTitle = "";
        private static int newFormFieldType = 0;
        private static bool newFormFieldOptional = false;
        private static HashSet<int> expandedSubmissions = new HashSet<int>();
        private static bool formAllowFormatTags = false;

        private static void DrawFormChannelContent()
        {
            var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                x.characterName == Plugin.plugin.playername &&
                x.characterWorld == Plugin.plugin.playerworld);

            if (currentGroup == null || character == null || selectedChannel == null) return;

            bool isOwner = GroupPermissions.IsOwner(currentGroup);
            bool canManageForms = isOwner || GroupPermissions.CanCreateForms(currentGroup);

            // Get form fields from cache
            var fields = DataReceiver.formFields.ContainsKey(selectedChannel.id)
                ? DataReceiver.formFields[selectedChannel.id]
                : new List<FormField>();

            // Get submissions from cache
            var submissions = DataReceiver.formSubmissions.ContainsKey(selectedChannel.id)
                ? DataReceiver.formSubmissions[selectedChannel.id]
                : new List<FormSubmission>();

            formAllowFormatTags = selectedChannel.allowFormatTags;

            using (var formChild = ImRaii.Child("FormContent", new Vector2(-1, -1), true))
            {
                if (formChild)
                {
                    if (canManageForms)
                    {
                        // Tab bar for admins/owners
                        using (var tabBar = ImRaii.TabBar("FormTabs"))
                        {
                            if (tabBar)
                            {
                                using (var editorTab = ImRaii.TabItem("Form Editor"))
                                {
                                    if (editorTab)
                                    {
                                        formTabIndex = 0;
                                        DrawFormEditor(character, fields);
                                    }
                                }

                                using (var submissionsTab = ImRaii.TabItem($"Submissions ({submissions.Count})"))
                                {
                                    if (submissionsTab)
                                    {
                                        formTabIndex = 1;
                                        DrawFormSubmissions(character, submissions);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // Regular members - show form to fill
                        DrawFormFill(character, fields);
                    }
                }
            }
        }

        private static void DrawFormEditor(Character character, List<FormField> fields)
        {
            ImGui.TextColored(new Vector4(0.2f, 0.6f, 1f, 1f), "Form Editor");
            ImGui.TextDisabled("Create and manage form fields that members will fill out.");
            ImGui.Separator();
            ImGui.Spacing();

            // Settings
            bool allowTags = formAllowFormatTags;
            if (ImGui.Checkbox("Allow Format Tags in Submissions", ref allowTags))
            {
                DataSender.UpdateFormChannelSettings(character, selectedChannel.id, allowTags);
                selectedChannel.allowFormatTags = allowTags;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("When enabled, submissions can contain images, colored text, etc.");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Existing fields
            ImGui.Text("Form Fields:");
            if (fields.Count == 0)
            {
                ImGui.TextDisabled("No fields created yet. Add your first field below.");
            }
            else
            {
                for (int i = 0; i < fields.Count; i++)
                {
                    var field = fields[i];
                    ImGui.PushID($"field_{field.id}");

                    // Check if editing this field
                    if (editingFormFieldId == field.id)
                    {
                        // Edit mode
                        ImGui.SetNextItemWidth(200);
                        ImGui.InputText("Title##Edit", ref editFormFieldTitle, 255);
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(120);
                        string[] types = { "Single Line", "Multi Line" };
                        ImGui.Combo("Type##Edit", ref editFormFieldType, types, types.Length);
                        ImGui.SameLine();
                        ImGui.Checkbox("Optional##Edit", ref editFormFieldOptional);
                        ImGui.SameLine();
                        if (ImGui.Button("Save##EditField"))
                        {
                            DataSender.UpdateFormField(character, field.id, editFormFieldTitle, editFormFieldType, editFormFieldOptional, field.sortOrder);
                            editingFormFieldId = null;
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Cancel##EditField"))
                        {
                            editingFormFieldId = null;
                        }
                    }
                    else
                    {
                        // Display mode
                        string typeStr = field.fieldType == 0 ? "[Single Line]" : "[Multi Line]";
                        string optStr = field.isOptional ? "(Optional)" : "*";

                        ImGui.Text($"{i + 1}. {field.title} {typeStr} {optStr}");
                        ImGui.SameLine();
                        if (ImGui.SmallButton("Edit"))
                        {
                            editingFormFieldId = field.id;
                            editFormFieldTitle = field.title;
                            editFormFieldType = field.fieldType;
                            editFormFieldOptional = field.isOptional;
                        }
                        ImGui.SameLine();
                        if (ImGui.SmallButton("Delete"))
                        {
                            DataSender.DeleteFormField(character, field.id);
                        }

                        // Move buttons
                        if (i > 0)
                        {
                            ImGui.SameLine();
                            if (ImGui.SmallButton("Up"))
                            {
                                // Swap sort orders
                                var prevField = fields[i - 1];
                                DataSender.UpdateFormField(character, field.id, field.title, field.fieldType, field.isOptional, prevField.sortOrder);
                                DataSender.UpdateFormField(character, prevField.id, prevField.title, prevField.fieldType, prevField.isOptional, field.sortOrder);
                            }
                        }
                        if (i < fields.Count - 1)
                        {
                            ImGui.SameLine();
                            if (ImGui.SmallButton("Down"))
                            {
                                // Swap sort orders
                                var nextField = fields[i + 1];
                                DataSender.UpdateFormField(character, field.id, field.title, field.fieldType, field.isOptional, nextField.sortOrder);
                                DataSender.UpdateFormField(character, nextField.id, nextField.title, nextField.fieldType, nextField.isOptional, field.sortOrder);
                            }
                        }
                    }

                    ImGui.PopID();
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Add new field
            ImGui.Text("Add New Field:");
            ImGui.SetNextItemWidth(200);
            ImGui.InputText("Title##New", ref newFormFieldTitle, 255);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(120);
            string[] fieldTypes = { "Single Line", "Multi Line" };
            ImGui.Combo("Type##New", ref newFormFieldType, fieldTypes, fieldTypes.Length);
            ImGui.SameLine();
            ImGui.Checkbox("Optional##New", ref newFormFieldOptional);
            ImGui.SameLine();

            bool canAdd = !string.IsNullOrWhiteSpace(newFormFieldTitle);
            if (!canAdd) ImGui.BeginDisabled();
            if (ImGui.Button("+ Add Field"))
            {
                int nextSortOrder = fields.Count > 0 ? fields.Max(f => f.sortOrder) + 1 : 0;
                DataSender.CreateFormField(character, selectedChannel.id, newFormFieldTitle, newFormFieldType, newFormFieldOptional, nextSortOrder);
                newFormFieldTitle = "";
                newFormFieldType = 0;
                newFormFieldOptional = false;
            }
            if (!canAdd) ImGui.EndDisabled();
        }

        private static void DrawFormFill(Character character, List<FormField> fields)
        {
            ImGui.TextColored(new Vector4(0.2f, 0.6f, 1f, 1f), selectedChannel.name);
            if (!string.IsNullOrEmpty(selectedChannel.description))
            {
                ImGui.TextDisabled(selectedChannel.description);
            }
            ImGui.Separator();
            ImGui.Spacing();

            if (fields.Count == 0)
            {
                ImGui.TextDisabled("This form has no fields yet. Please wait for an administrator to set it up.");
                return;
            }

            // Initialize input values if needed
            foreach (var field in fields)
            {
                if (!formInputValues.ContainsKey(field.id))
                {
                    formInputValues[field.id] = "";
                }
            }

            // Display form fields
            foreach (var field in fields.OrderBy(f => f.sortOrder))
            {
                string label = field.isOptional ? $"{field.title} (Optional)" : $"{field.title} *";
                ImGui.Text(label);

                string value = formInputValues[field.id];
                if (field.fieldType == 0)
                {
                    // Single line
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.InputText($"##{field.id}", ref value, 1000))
                    {
                        formInputValues[field.id] = value;
                    }
                }
                else
                {
                    // Multi line
                    if (ImGui.InputTextMultiline($"##{field.id}", ref value, 5000, new Vector2(-1, 100)))
                    {
                        formInputValues[field.id] = value;
                    }
                }
                ImGui.Spacing();
            }

            ImGui.Spacing();

            // Submit button
            if (ImGui.Button("Submit", new Vector2(100, 30)))
            {
                // Validate required fields
                bool valid = true;
                foreach (var field in fields)
                {
                    if (!field.isOptional && string.IsNullOrWhiteSpace(formInputValues.GetValueOrDefault(field.id, "")))
                    {
                        valid = false;
                        DataReceiver.formSubmitResultSuccess = false;
                        DataReceiver.formSubmitResultMessage = $"Please fill in the required field: {field.title}";
                        break;
                    }
                }

                if (valid)
                {
                    // Get current member info from group
                    var currentMember = currentGroup?.members?.FirstOrDefault(m => m.userID == DataSender.userID);
                    int profileId = currentMember?.profileID ?? 0;
                    string profileName = currentMember?.name ?? character.characterName;

                    var fieldValues = formInputValues.Select(kv => (kv.Key, kv.Value)).ToList();
                    DataSender.SubmitForm(character, selectedChannel.id, profileId, profileName, fieldValues);

                    // Clear form
                    formInputValues.Clear();
                }
            }

            // Show result message
            if (!string.IsNullOrEmpty(DataReceiver.formSubmitResultMessage))
            {
                ImGui.Spacing();
                if (DataReceiver.formSubmitResultSuccess)
                {
                    ImGui.TextColored(new Vector4(0.3f, 0.9f, 0.3f, 1f), DataReceiver.formSubmitResultMessage);
                }
                else
                {
                    ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), DataReceiver.formSubmitResultMessage);
                }
            }
        }

        private static void DrawFormSubmissions(Character character, List<FormSubmission> submissions)
        {
            ImGui.TextColored(new Vector4(0.9f, 0.7f, 0.2f, 1f), "Form Submissions");
            ImGui.TextDisabled($"{submissions.Count} submission(s)");
            ImGui.Separator();
            ImGui.Spacing();

            if (submissions.Count == 0)
            {
                ImGui.TextDisabled("No submissions yet.");
                return;
            }

            foreach (var submission in submissions)
            {
                ImGui.PushID($"submission_{submission.id}");

                bool isExpanded = expandedSubmissions.Contains(submission.id);
                string header = $"{submission.profileName} - {submission.submittedAt:g}";

                // Expandable header
                if (ImGui.CollapsingHeader(header, ImGuiTreeNodeFlags.DefaultOpen))
                {
                    expandedSubmissions.Add(submission.id);

                    ImGui.Indent();

                    // Display field values
                    foreach (var val in submission.values)
                    {
                        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), $"{val.fieldTitle}:");
                        if (formAllowFormatTags)
                        {
                            Misc.RenderHtmlElements(val.value, true, true, true, false, null, false, true);
                        }
                        else
                        {
                            ImGui.TextWrapped(val.value);
                        }
                        ImGui.Spacing();
                    }

                    // Delete button
                    if (ImGui.SmallButton("Delete Submission"))
                    {
                        DataSender.DeleteFormSubmission(character, submission.id);
                    }

                    ImGui.Unindent();
                }
                else
                {
                    expandedSubmissions.Remove(submission.id);
                }

                ImGui.PopID();
            }
        }

        private static void DrawRoleSelectionChannelContent()
        {
            var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                x.characterName == Plugin.plugin.playername &&
                x.characterWorld == Plugin.plugin.playerworld);

            if (currentGroup == null || character == null) return;

            bool isOwner = GroupPermissions.IsOwner(currentGroup);
            bool canManageRoles = isOwner || DataReceiver.canManageSelfAssignRoles;

            // Full height content area for role selection
            using (var rolesChild = ImRaii.Child("RoleSelectionContent", new Vector2(-1, -1), true))
            {
                if (rolesChild)
                {
                    // Tab bar for users with management permissions
                    if (canManageRoles)
                    {
                        using (var tabBar = ImRaii.TabBar("RoleSelectionTabs"))
                        {
                            if (tabBar)
                            {
                                using (var selectTab = ImRaii.TabItem("Select Roles"))
                                {
                                    if (selectTab)
                                    {
                                        DrawRoleSelectionList(character);
                                    }
                                }

                                using (var manageTab = ImRaii.TabItem("Manage Roles"))
                                {
                                    if (manageTab)
                                    {
                                        DrawRoleManagement(character);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // Regular members just see the role selection list
                        DrawRoleSelectionList(character);
                    }
                }
            }

            // Delete role confirmation popup
            if (showDeleteRoleConfirmation && roleToDelete != null)
            {
                ImGui.OpenPopup("Delete Role?");
            }

            if (ImGui.BeginPopupModal("Delete Role?", ref showDeleteRoleConfirmation, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text($"Are you sure you want to delete the role \"{roleToDelete?.name}\"?");
                ImGui.Text("This will remove the role from all members who have it.");
                ImGui.Spacing();

                if (ImGui.Button("Delete", new Vector2(100, 0)))
                {
                    DataSender.DeleteSelfAssignRole(character, currentGroup.groupID, roleToDelete.id);
                    showDeleteRoleConfirmation = false;
                    roleToDelete = null;
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(100, 0)))
                {
                    showDeleteRoleConfirmation = false;
                    roleToDelete = null;
                }
                ImGui.EndPopup();
            }

            // Delete section confirmation
            if (showDeleteSectionConfirmation && sectionToDelete != null)
            {
                ImGui.OpenPopup("Delete Section?");
            }

            if (ImGui.BeginPopupModal("Delete Section?", ref showDeleteSectionConfirmation, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text($"Are you sure you want to delete the section \"{sectionToDelete?.name}\"?");
                ImGui.Text("This will delete all roles in this section as well.");
                ImGui.Spacing();

                if (ImGui.Button("Delete", new Vector2(100, 0)))
                {
                    DataSender.DeleteRoleSection(character, currentGroup.groupID, sectionToDelete.id);
                    showDeleteSectionConfirmation = false;
                    sectionToDelete = null;
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(100, 0)))
                {
                    showDeleteSectionConfirmation = false;
                    sectionToDelete = null;
                }
                ImGui.EndPopup();
            }
        }

        private static void DrawRoleSelectionList(Character character)
        {
            ImGui.TextColored(new Vector4(0.3f, 0.9f, 0.9f, 1f), "Self-Assign Roles");
            ImGui.TextDisabled("Toggle roles to add or remove them from your profile.");
            ImGui.Separator();
            ImGui.Spacing();

            // Use data from DataReceiver
            var roles = DataReceiver.selfAssignRoles;
            var sections = DataReceiver.roleSections;
            var memberRoleIds = DataReceiver.memberSelfRoleIDs.ToHashSet();

            if (roles == null || roles.Count == 0)
            {
                ImGui.TextDisabled("No self-assign roles have been created yet.");
                return;
            }

            // Group roles by section
            var rolesBySection = roles.GroupBy(r => r.sectionID).OrderBy(g =>
            {
                if (g.Key == 0) return int.MaxValue; // Uncategorized at the end
                var section = sections?.FirstOrDefault(s => s.id == g.Key);
                return section?.sortOrder ?? int.MaxValue;
            });

            foreach (var sectionGroup in rolesBySection)
            {
                // Get section name
                string sectionName = "Uncategorized";
                if (sectionGroup.Key != 0)
                {
                    var section = sections?.FirstOrDefault(s => s.id == sectionGroup.Key);
                    if (section != null)
                    {
                        sectionName = section.name;
                    }
                }

                // Display section header
                ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.3f, 1f), sectionName);
                ImGui.Separator();

                foreach (var role in sectionGroup.OrderBy(r => r.sortOrder))
                {
                    ImGui.PushID($"role_{role.id}");

                    bool hasRole = memberRoleIds.Contains(role.id);

                    // Parse color from hex
                    Vector4 roleColor = ParseHexColor(role.color);

                    // Role toggle checkbox
                    if (ImGui.Checkbox($"##toggle_{role.id}", ref hasRole))
                    {
                        if (hasRole)
                        {
                            DataSender.AssignSelfRole(character, currentGroup.groupID, role.id);
                        }
                        else
                        {
                            DataSender.UnassignSelfRole(character, currentGroup.groupID, role.id);
                        }
                    }

                    ImGui.SameLine();

                    // Role name with color
                    ImGui.TextColored(roleColor, role.name);

                    // Role description on hover
                    if (!string.IsNullOrEmpty(role.description) && ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(role.description);
                    }

                    ImGui.PopID();
                }

                ImGui.Spacing();
            }
        }

        private static void DrawRoleManagement(Character character)
        {
            ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.3f, 1f), "Manage Self-Assign Roles");
            ImGui.Separator();
            ImGui.Spacing();

            var sections = DataReceiver.roleSections ?? new List<GroupRoleSection>();

            // Section management
            ImGui.Text("Role Sections:");
            ImGui.SetNextItemWidth(200);
            ImGui.InputText("##NewSectionName", ref newSectionName, 50);
            ImGui.SameLine();
            bool canCreateSection = !string.IsNullOrWhiteSpace(newSectionName);
            if (!canCreateSection) ImGui.BeginDisabled();
            if (ImGui.Button("Create Section"))
            {
                DataSender.CreateRoleSection(character, currentGroup.groupID, newSectionName);
                newSectionName = string.Empty;
            }
            if (!canCreateSection) ImGui.EndDisabled();

            // List existing sections with delete buttons
            if (sections.Count > 0)
            {
                ImGui.Indent();
                foreach (var section in sections.OrderBy(s => s.sortOrder))
                {
                    ImGui.PushID($"section_{section.id}");
                    ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.3f, 1f), section.name);
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Delete##section"))
                    {
                        sectionToDelete = section;
                        showDeleteSectionConfirmation = true;
                    }
                    ImGui.PopID();
                }
                ImGui.Unindent();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Create new role section
            ImGui.Text("Create New Role:");
            ImGui.SetNextItemWidth(200);
            ImGui.InputText("Name##NewRole", ref newRoleName, 50);
            ImGui.SameLine();
            ImGui.ColorEdit4("Color##NewRole", ref newRoleColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoAlpha);

            ImGui.SetNextItemWidth(300);
            ImGui.InputText("Description##NewRole", ref newRoleDescription, 200);

            // Section dropdown for new role
            ImGui.SetNextItemWidth(200);
            string[] sectionOptions = new string[sections.Count + 1];
            sectionOptions[0] = "Uncategorized";
            for (int i = 0; i < sections.Count; i++)
            {
                sectionOptions[i + 1] = sections[i].name;
            }
            int sectionIndex = 0;
            if (newRoleSectionID != 0)
            {
                for (int i = 0; i < sections.Count; i++)
                {
                    if (sections[i].id == newRoleSectionID)
                    {
                        sectionIndex = i + 1;
                        break;
                    }
                }
            }
            if (ImGui.Combo("Section##NewRole", ref sectionIndex, sectionOptions, sectionOptions.Length))
            {
                newRoleSectionID = sectionIndex == 0 ? 0 : sections[sectionIndex - 1].id;
            }
            ImGui.SameLine();

            bool canCreate = !string.IsNullOrWhiteSpace(newRoleName);
            if (!canCreate) ImGui.BeginDisabled();
            if (ImGui.Button("Create Role"))
            {
                string hexColor = ColorToHex(newRoleColor);
                DataSender.CreateSelfAssignRole(character, currentGroup.groupID, newRoleName, hexColor, newRoleDescription, newRoleSectionID);
                newRoleName = string.Empty;
                newRoleDescription = string.Empty;
                newRoleColor = new Vector4(1f, 1f, 1f, 1f);
                newRoleSectionID = 0;
            }
            if (!canCreate) ImGui.EndDisabled();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Existing roles list
            ImGui.Text("Existing Roles:");

            // Use data from DataReceiver
            var roles = DataReceiver.selfAssignRoles;

            if (roles == null || roles.Count == 0)
            {
                ImGui.TextDisabled("No roles created yet.");
                return;
            }

            foreach (var role in roles.OrderBy(r => r.sortOrder))
            {
                ImGui.PushID($"manage_role_{role.id}");

                Vector4 roleColor = ParseHexColor(role.color);

                if (editingRole?.id == role.id)
                {
                    // Edit mode for this role
                    ImGui.SetNextItemWidth(150);
                    ImGui.InputText("##EditName", ref editRoleName, 50);
                    ImGui.SameLine();
                    ImGui.ColorEdit4("##EditColor", ref editRoleColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoAlpha);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(200);
                    ImGui.InputText("##EditDesc", ref editRoleDescription, 200);

                    // Section dropdown for edit role
                    ImGui.SetNextItemWidth(150);
                    string[] editSectionOptions = new string[sections.Count + 1];
                    editSectionOptions[0] = "Uncategorized";
                    for (int i = 0; i < sections.Count; i++)
                    {
                        editSectionOptions[i + 1] = sections[i].name;
                    }
                    int editSectionIndex = 0;
                    if (editRoleSectionID != 0)
                    {
                        for (int i = 0; i < sections.Count; i++)
                        {
                            if (sections[i].id == editRoleSectionID)
                            {
                                editSectionIndex = i + 1;
                                break;
                            }
                        }
                    }
                    if (ImGui.Combo("##EditSection", ref editSectionIndex, editSectionOptions, editSectionOptions.Length))
                    {
                        editRoleSectionID = editSectionIndex == 0 ? 0 : sections[editSectionIndex - 1].id;
                    }
                    ImGui.SameLine();

                    if (ImGui.SmallButton("Save"))
                    {
                        string hexColor = ColorToHex(editRoleColor);
                        DataSender.UpdateSelfAssignRole(character, currentGroup.groupID, role.id, editRoleName, hexColor, editRoleDescription, editRoleSectionID);
                        editingRole = null;
                    }
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Cancel"))
                    {
                        editingRole = null;
                    }
                }
                else
                {
                    // Display mode
                    ImGui.TextColored(roleColor, role.name);
                    if (!string.IsNullOrEmpty(role.description))
                    {
                        ImGui.SameLine();
                        ImGui.TextDisabled($"- {role.description}");
                    }
                    // Show section name
                    if (role.sectionID != 0)
                    {
                        var roleSection = sections.FirstOrDefault(s => s.id == role.sectionID);
                        if (roleSection != null)
                        {
                            ImGui.SameLine();
                            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), $"[{roleSection.name}]");
                        }
                    }
                    ImGui.SameLine();
                    float buttonPosX = ImGui.GetContentRegionAvail().X - 100;
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + buttonPosX);

                    if (ImGui.SmallButton("Edit"))
                    {
                        editingRole = role;
                        editRoleName = role.name;
                        editRoleDescription = role.description ?? string.Empty;
                        editRoleColor = ParseHexColor(role.color);
                        editRoleSectionID = role.sectionID;
                    }
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Delete"))
                    {
                        roleToDelete = role;
                        showDeleteRoleConfirmation = true;
                    }
                }

                ImGui.PopID();
            }
        }

        private static Vector4 ParseHexColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return new Vector4(1f, 1f, 1f, 1f);

            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                try
                {
                    int r = Convert.ToInt32(hex.Substring(0, 2), 16);
                    int g = Convert.ToInt32(hex.Substring(2, 2), 16);
                    int b = Convert.ToInt32(hex.Substring(4, 2), 16);
                    return new Vector4(r / 255f, g / 255f, b / 255f, 1f);
                }
                catch
                {
                    return new Vector4(1f, 1f, 1f, 1f);
                }
            }
            return new Vector4(1f, 1f, 1f, 1f);
        }

        private static string ColorToHex(Vector4 color)
        {
            int r = (int)(color.X * 255);
            int g = (int)(color.Y * 255);
            int b = (int)(color.Z * 255);
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        private static void DrawMessageInput()
        {
            // Check if the channel is locked
            if (selectedChannel != null && selectedChannel.isLocked)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "This channel is locked. Messages cannot be sent.");
                return;
            }

            // Check permissions for announcement channels
            if (selectedChannel != null && selectedChannel.channelType == 1)
            {
                // Announcement channel - check if user has permission
                bool canPost = GroupPermissions.CanCreateAnnouncement(currentGroup);
                if (!canPost)
                {
                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Only members with announcement permission can post in this channel.");
                    return;
                }
            }

            // Resize handle for chat input
            float resizeHandleHeight = 6f;
            Vector2 resizeHandlePos = ImGui.GetCursorScreenPos();
            float availableWidth = ImGui.GetContentRegionAvail().X - 70; // Account for send button

            // Draw resize handle bar
            var drawList = ImGui.GetWindowDrawList();
            Vector2 handleMin = resizeHandlePos;
            Vector2 handleMax = new Vector2(resizeHandlePos.X + availableWidth, resizeHandlePos.Y + resizeHandleHeight);

            // Check if mouse is hovering over resize handle
            Vector2 mousePos = ImGui.GetMousePos();
            bool hoveringHandle = mousePos.X >= handleMin.X && mousePos.X <= handleMax.X &&
                                  mousePos.Y >= handleMin.Y && mousePos.Y <= handleMax.Y;

            // Draw handle with different color when hovering
            uint handleColor = hoveringHandle || isResizingChatInput
                ? ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.5f, 1f))
                : ImGui.GetColorU32(new Vector4(0.3f, 0.3f, 0.3f, 1f));
            drawList.AddRectFilled(handleMin, handleMax, handleColor);

            // Draw grip lines in the center
            float gripWidth = 30f;
            float gripStartX = handleMin.X + (availableWidth - gripWidth) / 2f;
            float gripY = handleMin.Y + resizeHandleHeight / 2f;
            uint gripColor = ImGui.GetColorU32(new Vector4(0.6f, 0.6f, 0.6f, 1f));
            drawList.AddLine(new Vector2(gripStartX, gripY - 1), new Vector2(gripStartX + gripWidth, gripY - 1), gripColor, 1f);
            drawList.AddLine(new Vector2(gripStartX, gripY + 1), new Vector2(gripStartX + gripWidth, gripY + 1), gripColor, 1f);

            // Handle resize interaction
            if (hoveringHandle)
            {
                ImGui.SetMouseCursor((ImGuiMouseCursor)3); // ResizeNS = 3 (vertical up-down arrow)
            }

            if (hoveringHandle && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                isResizingChatInput = true;
            }

            if (isResizingChatInput)
            {
                ImGui.SetMouseCursor((ImGuiMouseCursor)3); // ResizeNS = 3 (vertical up-down arrow)
                // Prevent window from being dragged while resizing
                ImGui.GetIO().ConfigWindowsMoveFromTitleBarOnly = true;
                if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    float delta = ImGui.GetIO().MouseDelta.Y;
                    chatInputHeight = Math.Clamp(chatInputHeight - delta, minChatInputHeight, maxChatInputHeight);
                }
                else
                {
                    isResizingChatInput = false;
                    // Restore normal window dragging behavior
                    ImGui.GetIO().ConfigWindowsMoveFromTitleBarOnly = false;
                }
            }

            // Move cursor past the resize handle
            ImGui.Dummy(new Vector2(0, resizeHandleHeight));

            ImGui.SetNextItemWidth(-70);

            float scaledHeight = chatInputHeight * ImGui.GetIO().FontGlobalScale;

            // Get input position before drawing for popup positioning
            Vector2 inputPos = ImGui.GetCursorScreenPos();

            ImGui.InputTextMultiline("##MessageInput", ref messageInput, 10000,
                new Vector2(-200, scaledHeight));

            // Check if Enter was pressed without Shift while input is focused
            bool inputFocused = ImGui.IsItemFocused();
            bool enterPressed = ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter);
            bool shiftHeld = ImGui.GetIO().KeyShift;

            // Handle slash command popup
            bool slashCommandHandledEnter = false;
            HandleSlashCommandPopup(inputPos, scaledHeight, inputFocused, enterPressed, ref slashCommandHandledEnter);

            bool shouldSend = inputFocused && enterPressed && !shiftHeld && !slashCommandHandledEnter && !showSlashCommandPopup;

            if (shouldSend)
            {
                // Remove the newline that InputTextMultiline added when Enter was pressed
                messageInput = messageInput.TrimEnd('\n', '\r');
            }

            ImGui.SameLine();

            // Buttons on the right side - stacked vertically
            float buttonWidth = 100f;
            ImGui.BeginGroup();
            if (editingMessage != null)
            {
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "Editing...");
                // Cancel button on top
                if (ImGui.Button("Cancel"))
                {
                    CancelEditing();
                }
                ImGui.SameLine();
                // Save button below
                if (ImGui.Button("Save") || shouldSend)
                {
                    SaveEditedMessage();
                    if (shouldSend)
                    {
                        ImGui.SetKeyboardFocusHere(-1);
                    }
                }
            }
            else
            {
                // Just Send button when not editing
                if (ImGui.Button("Send", new Vector2(buttonWidth, 45)) || shouldSend)
                {
                    SendMessage();
                    if (shouldSend)
                    {
                        ImGui.SetKeyboardFocusHere(-1);
                    }
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Click or press Enter to send\nShift+Enter for new line");
                }
            }
            ImGui.EndGroup();

            // Delete confirmation popup
            if (showDeleteConfirmation)
            {
                ImGui.OpenPopup("Delete Message?");
            }

            // Center the popup
            var center = ImGui.GetMainViewport().GetCenter();
            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool popupOpen = true;
            if (ImGui.BeginPopupModal("Delete Message?", ref popupOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text("Are you sure you want to delete this message?");
                ImGui.Text("This action cannot be undone.");
                ImGui.Separator();
                ImGui.Spacing();

                // Center buttons
                float spacing = 10f;
                float totalWidth = (buttonWidth * 2) + spacing;
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - totalWidth) * 0.5f);

                if (ImGui.Button("Delete", new Vector2(buttonWidth, 0)))
                {
                    if (messageToDelete != null)
                    {
                        DeleteMessage(messageToDelete);
                        messageToDelete = null;
                    }
                    showDeleteConfirmation = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(buttonWidth, 0)))
                {
                    messageToDelete = null;
                    showDeleteConfirmation = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            // Reset if popup was closed via X button
            if (!popupOpen)
            {
                showDeleteConfirmation = false;
                messageToDelete = null;
            }

            // Pinned messages popup
            DrawPinnedMessagesPopup();
        }

        private static void DrawPinnedMessagesPopup()
        {
            if (!showPinnedMessagesPopup) return;

            ImGui.OpenPopup("Pinned Messages");

            var center = ImGui.GetMainViewport().GetCenter();
            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new Vector2(450, 400), ImGuiCond.FirstUseEver);

            bool pinnedPopupOpen = true;
            if (ImGui.BeginPopupModal("Pinned Messages", ref pinnedPopupOpen, ImGuiWindowFlags.NoCollapse))
            {
                if (selectedChannel != null)
                {
                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), $"Pinned messages in #{selectedChannel.name}");
                    ImGui.Separator();
                }

                if (!DataReceiver.pinnedMessagesLoaded)
                {
                    ImGui.Text("Loading pinned messages...");
                }
                else if (DataReceiver.pinnedMessages.Count == 0)
                {
                    ImGui.TextWrapped("No pinned messages in this channel.");
                    ImGui.Spacing();
                    ImGui.TextDisabled("Members with pin permissions can pin important messages by right-clicking on them.");
                }
                else
                {
                    using (var child = ImRaii.Child("PinnedMessagesList", new Vector2(-1, -40), true))
                    {
                        if (child)
                        {
                            foreach (var msg in DataReceiver.pinnedMessages)
                            {
                                ImGui.PushID($"pinned_{msg.messageID}");

                                // Avatar
                                float avatarSize = 32;
                                bool pinnedAvatarDrawn = false;
                                var msgAvatar = msg.avatar; // Capture reference to avoid race conditions
                                if (IsTextureValid(msgAvatar))
                                {
                                    try
                                    {
                                        var handle = msgAvatar.Handle;
                                        if (handle != default)
                                        {
                                            ImGui.Image(handle, new Vector2(avatarSize, avatarSize));
                                            pinnedAvatarDrawn = true;
                                        }
                                    }
                                    catch (ObjectDisposedException) { }
                                    catch { }
                                }

                                if (!pinnedAvatarDrawn)
                                {
                                    // Placeholder avatar
                                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.3f, 0.3f, 1f));
                                    ImGui.Button("##avatar", new Vector2(avatarSize, avatarSize));
                                    ImGui.PopStyleColor();
                                }

                                ImGui.SameLine();

                                ImGui.BeginGroup();
                                // Sender name
                                ImGui.TextColored(new Vector4(0.8f, 0.8f, 1f, 1f), msg.senderName ?? "Unknown");

                                // Timestamp
                                ImGui.SameLine();
                                var messageTime = DateTimeOffset.FromUnixTimeMilliseconds(msg.timestamp).LocalDateTime;
                                ImGui.TextDisabled(messageTime.ToString("MMM d, yyyy h:mm tt"));

                                // Message content (truncated)
                                string content = msg.messageContent ?? "";
                                if (content.Length > 100)
                                {
                                    content = content.Substring(0, 100) + "...";
                                }
                                Misc.RenderHtmlElements(content, true, true, true, false, limitImageWidth: true);
                                ImGui.EndGroup();

                                // Jump to message button
                                ImGui.SameLine(ImGui.GetWindowWidth() - 70);
                                if (ImGui.SmallButton("Jump"))
                                {
                                    scrollToMessageID = msg.messageID;
                                    showPinnedMessagesPopup = false;
                                    ImGui.CloseCurrentPopup();
                                }
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip("Scroll to this message in chat");
                                }

                                ImGui.Separator();
                                ImGui.PopID();
                            }
                        }
                    }
                }

                ImGui.Spacing();

                // Close button
                float buttonWidth = 100f;
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - buttonWidth) * 0.5f);
                if (ImGui.Button("Close", new Vector2(buttonWidth, 0)))
                {
                    showPinnedMessagesPopup = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            if (!pinnedPopupOpen)
            {
                showPinnedMessagesPopup = false;
            }
        }

        private static void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(messageInput) || selectedChannel == null || currentGroup == null)
                return;

            try
            {
                var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x => x.characterName == Plugin.plugin.playername && x.characterWorld == Plugin.plugin.playerworld);
                if (character != null)
                {
                    // Process message to wrap image URLs in <img> tags
                    string processedMessage = WrapImageUrls(messageInput.Trim());

                    // Send message via DataSender
                    DataSender.SendGroupChatMessage(character, currentGroup.groupID, selectedChannel.id, processedMessage);

                    // Clear input
                    messageInput = string.Empty;
                    autoScroll = true;
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error sending message: {ex.Message}");
            }
        }

        /// <summary>
        /// Wraps image URLs (http/https containing .jpg, .jpeg, .png, .gif, .webp) in img tags
        /// Handles URLs with query parameters like image.png?size=large
        /// </summary>
        private static string WrapImageUrls(string message)
        {
            // Regex to match URLs that contain image extensions anywhere (including with query params)
            // Matches URLs not already wrapped in <img> tags
            var imageUrlPattern = new System.Text.RegularExpressions.Regex(
                @"(?<!<img>)(https?://[^\s<>""]*\.(?:jpg|jpeg|png|gif|webp)(?:[^\s<>""]*)?)(?!</img>)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return imageUrlPattern.Replace(message, "<img>$1</img>");
        }

        private static void FetchChannelMessages()
        {
            if (selectedChannel == null || currentGroup == null)
                return;

            Plugin.PluginLog.Info($"[GroupsData] FetchChannelMessages - channelID={selectedChannel.id}, groupID={currentGroup.groupID}, type={selectedChannel.channelType}");

            // Reset NSFW spoiler states when switching channels
            Misc.SetNsfwSession($"channel_{currentGroup.groupID}_{selectedChannel.id}");

            try
            {
                var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x => x.characterName == Plugin.plugin.playername && x.characterWorld == Plugin.plugin.playerworld);
                if (character != null)
                {
                    // Handle special channel types
                    if (selectedChannel.channelType == 2)
                    {
                        // Rules channel - fetch rules
                        Plugin.PluginLog.Info($"[GroupsData] Fetching rules for group {currentGroup.groupID}");
                        DataSender.FetchGroupRules(character, currentGroup.groupID);
                    }
                    else if (selectedChannel.channelType == 3)
                    {
                        // Role Selection channel - fetch self-assign roles and member's current roles
                        Plugin.PluginLog.Info($"[GroupsData] Fetching self-assign roles for group {currentGroup.groupID}");
                        DataSender.FetchSelfAssignRoles(character, currentGroup.groupID);
                        DataSender.FetchMemberSelfRoles(character, currentGroup.groupID);
                        DataSender.FetchRoleSections(character, currentGroup.groupID);
                    }
                    else if (selectedChannel.channelType == 4)
                    {
                        // Form channel - fetch form fields and submissions (if owner/admin)
                        Plugin.PluginLog.Info($"[GroupsData] Fetching form fields for channel {selectedChannel.id}");
                        DataSender.FetchFormFields(character, selectedChannel.id);
                        DataSender.FetchFormSubmissions(character, selectedChannel.id);
                    }
                    else
                    {
                        // Normal text/announcement channel - fetch messages
                        Plugin.PluginLog.Info($"[GroupsData] Requesting messages from server for channel {selectedChannel.id}");
                        DataSender.FetchGroupChatMessages(character, currentGroup.groupID, selectedChannel.id, 50, 0);
                    }
                }
                else
                {
                    Plugin.PluginLog.Warning($"[GroupsData] Cannot fetch messages - character not found");
                }

                // Mark channel as read
                selectedChannel.unreadCount = 0;
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error fetching messages: {ex.Message}");
            }
        }

        /// <summary>
        /// Asynchronously loads avatars for users in the message list.
        /// This is called after messages are displayed to avoid blocking initial load.
        /// </summary>
        private static void LoadAvatarsAsync(List<GroupChatMessage> messages)
        {
            if (messages == null || messages.Count == 0)
            {
                Plugin.PluginLog.Info($"[LoadAvatarsAsync] No messages to process");
                return;
            }

            Plugin.PluginLog.Info($"[LoadAvatarsAsync] Starting async avatar load for {messages.Count} messages");

            // Get unique user IDs that need avatars loaded
            var userIDsNeedingAvatars = messages
                .Where(m => m != null && m.senderUserID > 0)
                .Select(m => m.senderUserID)
                .Distinct()
                .Where(userID => !avatarTextureCache.ContainsKey(userID) && !avatarsLoading.Contains(userID))
                .ToList();

            Plugin.PluginLog.Info($"[LoadAvatarsAsync] Unique users in messages: {messages.Select(m => m.senderUserID).Distinct().Count()}");
            Plugin.PluginLog.Info($"[LoadAvatarsAsync] Users already in cache: {avatarTextureCache.Count}");
            Plugin.PluginLog.Info($"[LoadAvatarsAsync] Users currently loading: {avatarsLoading.Count}");
            Plugin.PluginLog.Info($"[LoadAvatarsAsync] Users needing avatars: {userIDsNeedingAvatars.Count}");

            if (userIDsNeedingAvatars.Count == 0)
            {
                Plugin.PluginLog.Info($"[LoadAvatarsAsync] No avatars need loading - all cached or loading");
                return;
            }

            Plugin.PluginLog.Info($"[LoadAvatarsAsync] Will request avatars for users: {string.Join(", ", userIDsNeedingAvatars)}");

            // Mark these avatars as loading
            foreach (var userID in userIDsNeedingAvatars)
            {
                avatarsLoading.Add(userID);
            }

            // Request avatars from server
            Task.Run(async () =>
            {
                try
                {
                    var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x => x.characterName == Plugin.plugin.playername && x.characterWorld == Plugin.plugin.playerworld);
                    if (character != null && currentGroup != null)
                    {
                        // Request avatars for these users
                        foreach (var userID in userIDsNeedingAvatars)
                        {
                            DataSender.FetchGroupMemberAvatar(character, currentGroup.groupID, userID);
                            await Task.Delay(50); // Small delay to avoid flooding the server
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug($"[LoadAvatarsAsync] Error loading avatars: {ex.Message}");
                }
                finally
                {
                    // Remove from loading set
                    foreach (var userID in userIDsNeedingAvatars)
                    {
                        avatarsLoading.Remove(userID);
                    }
                }
            });
        }

        // Called from ClientHandleData when messages are received
        public static void OnMessagesReceived(List<GroupChatMessage> messages)
        {
            if (messages == null)
                return;

            Plugin.PluginLog.Info($"[GroupsData] OnMessagesReceived - received {messages.Count} messages");

            // If no messages, just clear and return (empty channel)
            if (messages.Count == 0)
            {
                Plugin.PluginLog.Info($"[GroupsData] No messages received, clearing current messages");
                lock (messagesLock)
                {
                    currentMessages.Clear();
                }
                return;
            }

            // Only update if messages are for the currently selected channel
            if (selectedChannel != null && messages[0].channelID == selectedChannel.id)
            {
                Plugin.PluginLog.Info($"[GroupsData] Messages are for current channel {selectedChannel.id}, updating display");

                // Check cached avatars only - don't wait for server avatars on initial load
                foreach (var message in messages)
                {
                    // Only use cached avatars - messages arrive without avatars for fast loading
                    if (avatarTextureCache.TryGetValue(message.senderUserID, out var cachedTexture))
                    {
                        message.avatar = cachedTexture;
                        Plugin.PluginLog.Info($"[OnMessagesReceived] Using cached avatar for user {message.senderUserID}");
                    }
                    else
                    {
                        // Avatar will be loaded asynchronously
                        message.avatar = null;
                    }
                }

                lock (messagesLock)
                {
                    currentMessages = messages;
                }
                autoScroll = true;

                // Trigger async avatar loading in the background
                LoadAvatarsAsync(messages);
            }
            else
            {
                Plugin.PluginLog.Info($"[GroupsData] Messages are for channel {messages[0].channelID}, but current channel is {selectedChannel?.id}, ignoring");

                // Don't dispose textures here - they will be disposed when we switch channels via ClearAvatarCache()
                // Disposing during message receive can cause crashes if ImGui is rendering those textures
            }
        }

        /// <summary>
        /// Called when a message is deleted (broadcast from server).
        /// Removes the message from the current messages list if it's in the current channel.
        /// </summary>
        public static void OnMessageDeleted(int messageID, int groupID, int channelID)
        {
            Plugin.PluginLog.Info($"[Groups] OnMessageDeleted - messageID={messageID}, groupID={groupID}, channelID={channelID}");

            // Only update if this is for the currently selected channel
            if (selectedChannel != null && selectedChannel.id == channelID && currentGroup != null && currentGroup.groupID == groupID)
            {
                lock (messagesLock)
                {
                    var messageToRemove = currentMessages.FirstOrDefault(m => m.messageID == messageID);
                    if (messageToRemove != null)
                    {
                        currentMessages.Remove(messageToRemove);
                        Plugin.PluginLog.Info($"[Groups] Removed message {messageID} from display");
                    }
                }
            }
        }

        /// <summary>
        /// Called when a message is edited (broadcast from server).
        /// Updates the message content in the current messages list if it's in the current channel.
        /// </summary>
        public static void OnMessageEdited(int messageID, int groupID, int channelID, string newContent)
        {
            Plugin.PluginLog.Info($"[Groups] OnMessageEdited - messageID={messageID}, groupID={groupID}, channelID={channelID}");

            // Only update if this is for the currently selected channel
            if (selectedChannel != null && selectedChannel.id == channelID && currentGroup != null && currentGroup.groupID == groupID)
            {
                lock (messagesLock)
                {
                    var messageToEdit = currentMessages.FirstOrDefault(m => m.messageID == messageID);
                    if (messageToEdit != null)
                    {
                        messageToEdit.messageContent = newContent;
                        messageToEdit.isEdited = true;
                        Plugin.PluginLog.Info($"[Groups] Updated message {messageID} with new content");
                    }
                }
            }
        }

        public static void OnMessagePinUpdated(int messageID, int groupID, int channelID, bool isPinned)
        {
            Plugin.PluginLog.Info($"[Groups] OnMessagePinUpdated - messageID={messageID}, groupID={groupID}, channelID={channelID}, isPinned={isPinned}");

            // Only update if this is for the currently selected channel
            if (selectedChannel != null && selectedChannel.id == channelID && currentGroup != null && currentGroup.groupID == groupID)
            {
                lock (messagesLock)
                {
                    var messageToUpdate = currentMessages.FirstOrDefault(m => m.messageID == messageID);
                    if (messageToUpdate != null)
                    {
                        messageToUpdate.isPinned = isPinned;
                        Plugin.PluginLog.Info($"[Groups] Updated message {messageID} pin status to {isPinned}");
                    }
                }
            }
        }

        public static void OnChannelLockUpdated(int groupID, int channelID, bool isLocked)
        {
            Plugin.PluginLog.Info($"[Groups] OnChannelLockUpdated - groupID={groupID}, channelID={channelID}, isLocked={isLocked}");

            // Update the channel lock status in categories
            if (currentGroup != null && currentGroup.groupID == groupID && currentGroup.categories != null)
            {
                foreach (var category in currentGroup.categories)
                {
                    if (category.channels != null)
                    {
                        var channel = category.channels.FirstOrDefault(c => c.id == channelID);
                        if (channel != null)
                        {
                            channel.isLocked = isLocked;
                            Plugin.PluginLog.Info($"[Groups] Updated channel {channelID} lock status to {isLocked}");
                            break;
                        }
                    }
                }
            }

            // Also update selectedChannel if it matches
            if (selectedChannel != null && selectedChannel.id == channelID)
            {
                selectedChannel.isLocked = isLocked;
            }
        }

        /// <summary>
        /// Called when an avatar is received from the server.
        /// Updates all messages from this user with the new avatar.
        /// </summary>
        public static void OnAvatarReceived(int userID, byte[] avatarBytes)
        {
            if (avatarBytes == null || avatarBytes.Length == 0)
            {
                Plugin.PluginLog.Warning($"[OnAvatarReceived] Received empty avatar for user {userID}");
                return;
            }

            Plugin.PluginLog.Info($"[OnAvatarReceived] Received avatar for user {userID}, {avatarBytes.Length} bytes");

            try
            {
                // Create texture from bytes
                var texture = Plugin.TextureProvider.CreateFromImageAsync(avatarBytes).GetAwaiter().GetResult();
                if (texture != null && IsTextureValid(texture))
                {
                    IDalamudTextureWrap oldTexture = null;

                    // Update cache and messages atomically under both locks to prevent race conditions
                    lock (avatarCacheLock)
                    {
                        // Check if we already have a valid texture for this user that's not queued for disposal
                        if (avatarTextureCache.TryGetValue(userID, out oldTexture))
                        {
                            if (oldTexture != null && IsTextureValid(oldTexture) && !IsTextureQueuedForDisposal(oldTexture))
                            {
                                // Already have a valid texture, queue the new one for disposal
                                QueueTextureForDisposal(texture);
                                Plugin.PluginLog.Info($"[OnAvatarReceived] Already have valid texture for user {userID}, ignoring new one");
                                return;
                            }
                            // Old texture is invalid or queued for disposal, we'll replace it
                        }

                        avatarTextureCache[userID] = texture;

                        // Update all messages from this user
                        lock (messagesLock)
                        {
                            foreach (var message in currentMessages)
                            {
                                if (message != null && message.senderUserID == userID)
                                {
                                    message.avatar = texture;
                                }
                            }
                        }
                    }

                    // Queue old texture for deferred disposal (not immediate!)
                    if (oldTexture != null && oldTexture != texture)
                    {
                        QueueTextureForDisposal(oldTexture);
                    }

                    Plugin.PluginLog.Info($"[OnAvatarReceived] Avatar updated for user {userID}");
                }
                else
                {
                    Plugin.PluginLog.Warning($"[OnAvatarReceived] Failed to create valid texture for user {userID}");
                    // Queue invalid texture for disposal
                    if (texture != null)
                    {
                        QueueTextureForDisposal(texture);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"[OnAvatarReceived] Error processing avatar for user {userID}: {ex.Message}");
            }
        }

        // Called from ClientHandleData when a new message broadcast is received
        public static void OnNewMessageBroadcast(GroupChatMessage message)
        {
            Plugin.PluginLog.Info($"[GroupsData] OnNewMessageBroadcast called - message null? {message == null}");

            if (message == null)
                return;

            Plugin.PluginLog.Info($"[GroupsData] selectedChannel: {(selectedChannel != null ? $"ID={selectedChannel.id}" : "null")}, message.channelID={message.channelID}");

            int messageCountBefore;
            lock (messagesLock)
            {
                messageCountBefore = currentMessages.Count;
            }
            Plugin.PluginLog.Info($"[GroupsData] currentMessages.Count before: {messageCountBefore}");

            // Add to current messages if viewing the same channel
            if (selectedChannel != null && message.channelID == selectedChannel.id)
            {
                // Manage avatar texture - reuse cached or cache new
                if (message.avatar != null)
                {
                    message.avatar = GetOrCreateAvatarTexture(message.senderUserID, message.avatar);
                }

                int newCount;
                lock (messagesLock)
                {
                    currentMessages.Add(message);
                    newCount = currentMessages.Count;
                }
                autoScroll = true;
                Plugin.PluginLog.Info($"[GroupsData] Message added to currentMessages! New count: {newCount}");
            }
            else
            {
                Plugin.PluginLog.Info($"[GroupsData] Not viewing this channel, incrementing unread count");

                // Don't dispose textures here - they will be disposed when we switch channels via ClearAvatarCache()
                // Disposing during message broadcast can cause crashes if ImGui is rendering those textures

                // Increment unread count for the channel
                var channel = FindChannelByID(message.channelID);
                if (channel != null)
                {
                    channel.unreadCount++;
                    Plugin.PluginLog.Info($"[GroupsData] Channel found, unreadCount incremented to {channel.unreadCount}");
                }
                else
                {
                    Plugin.PluginLog.Warning($"[GroupsData] Channel with ID {message.channelID} not found!");
                }
            }
        }

        private static GroupChannel FindChannelByID(int channelID)
        {
            if (currentGroup == null || currentGroup.categories == null)
                return null;

            foreach (var category in currentGroup.categories)
            {
                if (category?.channels != null)
                {
                    foreach (var channel in category.channels)
                    {
                        if (channel?.id == channelID)
                            return channel;
                    }
                }
            }

            return null;
        }

        private static void StartEditingMessage(GroupChatMessage message)
        {
            editingMessage = message;
            messageInput = message.messageContent;
            Plugin.PluginLog.Info($"[GroupsData] Started editing message {message.messageID}");
        }

        private static void CancelEditing()
        {
            editingMessage = null;
            messageInput = string.Empty;
            Plugin.PluginLog.Info($"[GroupsData] Cancelled editing");
        }

        private static void SaveEditedMessage()
        {
            if (editingMessage == null || string.IsNullOrWhiteSpace(messageInput))
                return;

            try
            {
                Plugin.PluginLog.Info($"[GroupsData] Saving edited message {editingMessage.messageID}");

                var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x => x.characterName == Plugin.plugin.playername && x.characterWorld == Plugin.plugin.playerworld);
                if (character != null)
                {
                    // Send edit request to server
                    DataSender.EditGroupChatMessage(character, editingMessage.messageID, messageInput);

                    // Update local message
                    editingMessage.messageContent = messageInput;
                    editingMessage.isEdited = true;
                    editingMessage.editedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    Plugin.PluginLog.Info($"[GroupsData] Message edited successfully");
                }

                // Clear editing state
                editingMessage = null;
                messageInput = string.Empty;
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error editing message: {ex.Message}");
            }
        }

        private static void DeleteMessage(GroupChatMessage message)
        {
            if (message == null)
                return;

            try
            {
                Plugin.PluginLog.Info($"[GroupsData] Deleting message {message.messageID}");

                var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x => x.characterName == Plugin.plugin.playername && x.characterWorld == Plugin.plugin.playerworld);
                if (character != null)
                {
                    // Send delete request to server
                    DataSender.DeleteGroupChatMessage(character, message.messageID);

                    // Mark as deleted locally
                    message.deleted = true;

                    Plugin.PluginLog.Info($"[GroupsData] Message deleted successfully");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Error deleting message: {ex.Message}");
            }
        }

        #region Slash Commands

        private static void HandleSlashCommandPopup(Vector2 inputPos, float inputHeight, bool inputFocused, bool enterPressed, ref bool handledEnter)
        {
            // Check if user is typing a slash command
            bool startsWithSlash = messageInput.StartsWith("/");
            string currentWord = GetCurrentSlashWord();

            // Show popup when typing a slash at the start or when popup is already showing selection
            if (startsWithSlash && !slashCommandNeedsSelection)
            {
                showSlashCommandPopup = true;
                slashCommandSearch = currentWord;
            }
            else if (!slashCommandNeedsSelection)
            {
                showSlashCommandPopup = false;
                slashCommandSearch = string.Empty;
                slashCommandSelectedIndex = 0;
            }

            if (!showSlashCommandPopup && !slashCommandNeedsSelection)
                return;

            // Position popup above the input
            float popupHeight = slashCommandNeedsSelection ? 250 : 150;
            ImGui.SetNextWindowPos(new Vector2(inputPos.X, inputPos.Y - popupHeight - 5));
            ImGui.SetNextWindowSize(new Vector2(350, popupHeight));

            if (ImGui.Begin("##SlashCommandPopup", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar))
            {
                if (slashCommandNeedsSelection)
                {
                    // Show selection UI for profile or group
                    DrawSlashCommandSelection(ref handledEnter, enterPressed);
                }
                else
                {
                    // Show command list
                    DrawSlashCommandList(ref handledEnter, enterPressed);
                }

                ImGui.End();
            }

            // Handle keyboard navigation
            if (inputFocused || showSlashCommandPopup)
            {
                if (ImGui.IsKeyPressed(ImGuiKey.UpArrow))
                {
                    if (slashCommandNeedsSelection)
                        slashCommandSelectionIndex = Math.Max(0, slashCommandSelectionIndex - 1);
                    else
                        slashCommandSelectedIndex = Math.Max(0, slashCommandSelectedIndex - 1);
                }
                if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))
                {
                    if (slashCommandNeedsSelection)
                        slashCommandSelectionIndex++;
                    else
                        slashCommandSelectedIndex = Math.Min(GetFilteredCommandCount() - 1, slashCommandSelectedIndex + 1);
                }
                if (ImGui.IsKeyPressed(ImGuiKey.Escape))
                {
                    CloseSlashCommandPopup();
                }
            }
        }

        private static string GetCurrentSlashWord()
        {
            if (string.IsNullOrEmpty(messageInput))
                return string.Empty;

            // Get the word being typed (from last space or start to current position)
            int lastSpace = messageInput.LastIndexOf(' ');
            string word = lastSpace >= 0 ? messageInput.Substring(lastSpace + 1) : messageInput;

            return word.StartsWith("/") ? word : string.Empty;
        }

        private static int GetFilteredCommandCount()
        {
            if (string.IsNullOrEmpty(slashCommandSearch))
                return slashCommands.Length;

            return slashCommands.Count(c => c.StartsWith(slashCommandSearch, StringComparison.OrdinalIgnoreCase));
        }

        private static void DrawSlashCommandList(ref bool handledEnter, bool enterPressed)
        {
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Slash Commands");
            ImGui.Separator();

            var filtered = slashCommands
                .Select((cmd, idx) => new { Command = cmd, Description = slashCommandDescriptions[idx], Index = idx })
                .Where(x => string.IsNullOrEmpty(slashCommandSearch) || x.Command.StartsWith(slashCommandSearch, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (filtered.Count == 0)
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "No matching commands");
                return;
            }

            // Clamp selection index
            slashCommandSelectedIndex = Math.Clamp(slashCommandSelectedIndex, 0, filtered.Count - 1);

            for (int i = 0; i < filtered.Count; i++)
            {
                var item = filtered[i];
                bool isSelected = i == slashCommandSelectedIndex;

                if (isSelected)
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.8f, 0.3f, 1f));

                if (ImGui.Selectable($"{item.Command}##cmd{i}", isSelected))
                {
                    SelectSlashCommand(item.Command);
                    handledEnter = true;
                }

                if (isSelected)
                    ImGui.PopStyleColor();

                ImGui.SameLine(150);
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), item.Description);
            }

            // Handle Enter to select
            if (enterPressed && filtered.Count > 0)
            {
                SelectSlashCommand(filtered[slashCommandSelectedIndex].Command);
                handledEnter = true;
            }
        }

        private static void SelectSlashCommand(string command)
        {
            slashCommandSelectedType = command.TrimStart('/').ToLower();

            if (slashCommandSelectedType == "profile" || slashCommandSelectedType == "groupinvite")
            {
                // Need to show selection popup
                slashCommandNeedsSelection = true;
                slashCommandSelectionSearch = string.Empty;
                slashCommandSelectionIndex = 0;

                // Remove the slash command text from input
                int lastSpace = messageInput.LastIndexOf(' ');
                if (lastSpace >= 0)
                    messageInput = messageInput.Substring(0, lastSpace + 1);
                else
                    messageInput = string.Empty;
            }
            else if (slashCommandSelectedType == "spoiler" || slashCommandSelectedType == "nsfw")
            {
                // Insert the tag directly
                InsertSlashCommandTag(slashCommandSelectedType);
                CloseSlashCommandPopup();
            }
        }

        private static void DrawSlashCommandSelection(ref bool handledEnter, bool enterPressed)
        {
            if (slashCommandSelectedType == "profile")
            {
                DrawProfileSelection(ref handledEnter, enterPressed);
            }
            else if (slashCommandSelectedType == "groupinvite")
            {
                DrawGroupInviteSelection(ref handledEnter, enterPressed);
            }
        }

        private static void DrawProfileSelection(ref bool handledEnter, bool enterPressed)
        {
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Select a Profile");
            ImGui.Separator();

            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##ProfileSearch", "Search profiles...", ref slashCommandSelectionSearch, 100);

            ImGui.Spacing();

            // Get profiles from ProfileWindow
            var profiles = ProfileWindow.profiles ?? new List<ProfileData>();

            // Filter by search
            var filtered = profiles
                .Where(p => string.IsNullOrEmpty(slashCommandSelectionSearch) ||
                           (p.title?.Contains(slashCommandSelectionSearch, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();

            if (filtered.Count == 0)
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "No profiles found");

                // Allow manual ID entry
                ImGui.Spacing();
                ImGui.Text("Or enter Profile ID:");
                ImGui.SetNextItemWidth(100);
                if (ImGui.InputInt("##ProfileID", ref slashCommandSelectionIndex))
                {
                    // slashCommandSelectionIndex used as profile ID here
                }
                if (ImGui.Button("Add by ID") || (enterPressed && slashCommandSelectionIndex > 0))
                {
                    InsertProfileEmbed(slashCommandSelectionIndex);
                    CloseSlashCommandPopup();
                    handledEnter = true;
                }
                return;
            }

            // Clamp selection
            slashCommandSelectionIndex = Math.Clamp(slashCommandSelectionIndex, 0, filtered.Count - 1);

            using (var child = ImRaii.Child("ProfileList", new Vector2(-1, 120), true))
            {
                for (int i = 0; i < filtered.Count; i++)
                {
                    var profile = filtered[i];
                    bool isSelected = i == slashCommandSelectionIndex;

                    if (ImGui.Selectable($"{profile.title ?? "Unnamed"} (ID: {profile.id})##profile{i}", isSelected))
                    {
                        InsertProfileEmbed(profile.id);
                        CloseSlashCommandPopup();
                        handledEnter = true;
                    }
                }
            }

            // Handle Enter to select
            if (enterPressed && filtered.Count > 0)
            {
                var selected = filtered[slashCommandSelectionIndex];
                InsertProfileEmbed(selected.id);
                CloseSlashCommandPopup();
                handledEnter = true;
            }

            if (ImGui.Button("Cancel"))
            {
                CloseSlashCommandPopup();
            }
        }

        private static void DrawGroupInviteSelection(ref bool handledEnter, bool enterPressed)
        {
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Select a Group to Invite To");
            ImGui.Separator();

            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##GroupSearch", "Search groups...", ref slashCommandSelectionSearch, 100);

            ImGui.Spacing();

            // Get groups where user can invite
            var invitableGroups = groups?
                .Where(g => GroupPermissions.CanInvite(g, false))
                .Where(g => string.IsNullOrEmpty(slashCommandSelectionSearch) ||
                           (g.name?.Contains(slashCommandSelectionSearch, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList() ?? new List<Group>();

            if (invitableGroups.Count == 0)
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "No groups available for invites");
                ImGui.TextWrapped("You need invite permission in at least one group.");

                if (ImGui.Button("Cancel"))
                {
                    CloseSlashCommandPopup();
                }
                return;
            }

            // Clamp selection
            slashCommandSelectionIndex = Math.Clamp(slashCommandSelectionIndex, 0, invitableGroups.Count - 1);

            using (var child = ImRaii.Child("GroupList", new Vector2(-1, 120), true))
            {
                for (int i = 0; i < invitableGroups.Count; i++)
                {
                    var group = invitableGroups[i];
                    bool isSelected = i == slashCommandSelectionIndex;

                    if (ImGui.Selectable($"{group.name ?? "Unnamed Group"}##group{i}", isSelected))
                    {
                        InsertGroupInviteEmbed(group.groupID);
                        CloseSlashCommandPopup();
                        handledEnter = true;
                    }
                }
            }

            // Handle Enter to select
            if (enterPressed && invitableGroups.Count > 0)
            {
                var selected = invitableGroups[slashCommandSelectionIndex];
                InsertGroupInviteEmbed(selected.groupID);
                CloseSlashCommandPopup();
                handledEnter = true;
            }

            if (ImGui.Button("Cancel"))
            {
                CloseSlashCommandPopup();
            }
        }

        private static void InsertProfileEmbed(int profileID)
        {
            // Insert profile embed tag - only includes ID for security
            // Name and avatar are fetched from server to prevent spoofing
            // Format: [profile:ID]
            string embed = $"[profile:{profileID}]";
            messageInput += embed;
        }

        private static void InsertGroupInviteEmbed(int groupID)
        {
            // Insert group invite embed tag - only includes ID for security
            // Name and logo are fetched from server to prevent spoofing
            // Format: [groupinvite:ID]
            string embed = $"[groupinvite:{groupID}]";
            messageInput += embed;
        }

        private static void InsertSlashCommandTag(string type)
        {
            // Remove the slash command from input
            int lastSpace = messageInput.LastIndexOf(' ');
            if (lastSpace >= 0)
                messageInput = messageInput.Substring(0, lastSpace + 1);
            else
                messageInput = string.Empty;

            // Insert the appropriate tag
            if (type == "spoiler")
            {
                messageInput += "<spoiler>";
            }
            else if (type == "nsfw")
            {
                messageInput += "<nsfw>";
            }
        }

        private static void CloseSlashCommandPopup()
        {
            showSlashCommandPopup = false;
            slashCommandNeedsSelection = false;
            slashCommandSearch = string.Empty;
            slashCommandSelectedIndex = 0;
            slashCommandSelectionSearch = string.Empty;
            slashCommandSelectionIndex = 0;
            slashCommandSelectedType = string.Empty;
        }

        #endregion

        #region Special Message Rendering

        // Track revealed spoilers/nsfw content per message
        private static HashSet<string> revealedSpoilers = new HashSet<string>();
        private static HashSet<string> revealedNsfw = new HashSet<string>();

        /// <summary>
        /// Renders message content with support for profile embeds, group invites, spoilers, and nsfw tags
        /// </summary>
        private static void RenderMessageWithEmbeds(string content, long messageID)
        {
            if (string.IsNullOrEmpty(content))
                return;

            // Process content in segments
            int lastIndex = 0;
            // Profile format: [profile:ID] - name/avatar fetched from server for security
            var profileRegex = new System.Text.RegularExpressions.Regex(@"\[profile:(\d+)\]");
            // Group invite format: [groupinvite:ID] - name/logo fetched from server for security
            var groupInviteRegex = new System.Text.RegularExpressions.Regex(@"\[groupinvite:(\d+)\]");
            var spoilerRegex = new System.Text.RegularExpressions.Regex(@"<spoiler>(.*?)</spoiler>", System.Text.RegularExpressions.RegexOptions.Singleline);
            var nsfwRegex = new System.Text.RegularExpressions.Regex(@"<nsfw>(.*?)</nsfw>", System.Text.RegularExpressions.RegexOptions.Singleline);
            var spoilerOpenRegex = new System.Text.RegularExpressions.Regex(@"<spoiler>");
            var nsfwOpenRegex = new System.Text.RegularExpressions.Regex(@"<nsfw>");

            // Collect all special elements with their positions
            var elements = new List<(int start, int end, string type, System.Text.RegularExpressions.Match match)>();

            foreach (System.Text.RegularExpressions.Match m in profileRegex.Matches(content))
                elements.Add((m.Index, m.Index + m.Length, "profile", m));
            foreach (System.Text.RegularExpressions.Match m in groupInviteRegex.Matches(content))
                elements.Add((m.Index, m.Index + m.Length, "groupinvite", m));
            foreach (System.Text.RegularExpressions.Match m in spoilerRegex.Matches(content))
                elements.Add((m.Index, m.Index + m.Length, "spoiler", m));
            foreach (System.Text.RegularExpressions.Match m in nsfwRegex.Matches(content))
                elements.Add((m.Index, m.Index + m.Length, "nsfw", m));

            // Handle unclosed spoiler/nsfw tags (text after opening tag goes until end)
            if (!spoilerRegex.IsMatch(content))
            {
                var openMatch = spoilerOpenRegex.Match(content);
                if (openMatch.Success)
                {
                    // Fake a match that covers from opening tag to end of content
                    elements.Add((openMatch.Index, content.Length, "spoiler_open", openMatch));
                }
            }
            if (!nsfwRegex.IsMatch(content))
            {
                var openMatch = nsfwOpenRegex.Match(content);
                if (openMatch.Success)
                {
                    elements.Add((openMatch.Index, content.Length, "nsfw_open", openMatch));
                }
            }

            // Sort by position
            elements = elements.OrderBy(e => e.start).ToList();

            // Render content segments
            foreach (var element in elements)
            {
                // Render text before this element
                if (element.start > lastIndex)
                {
                    string beforeText = content.Substring(lastIndex, element.start - lastIndex);
                    if (!string.IsNullOrWhiteSpace(beforeText))
                    {
                        Misc.RenderHtmlElements(beforeText, true, true, true, false, limitImageWidth: true);
                    }
                }

                // Render the special element
                switch (element.type)
                {
                    case "profile":
                        RenderProfileEmbed(element.match, messageID);
                        break;
                    case "groupinvite":
                        RenderGroupInviteEmbed(element.match, messageID);
                        break;
                    case "spoiler":
                        RenderSpoilerContent(element.match.Groups[1].Value, messageID, element.start);
                        break;
                    case "nsfw":
                        RenderNsfwContent(element.match.Groups[1].Value, messageID, element.start);
                        break;
                    case "spoiler_open":
                        // Content from after <spoiler> to end
                        string spoilerContent = content.Substring(element.match.Index + element.match.Length);
                        RenderSpoilerContent(spoilerContent, messageID, element.start);
                        lastIndex = content.Length;
                        continue;
                    case "nsfw_open":
                        string nsfwContent = content.Substring(element.match.Index + element.match.Length);
                        RenderNsfwContent(nsfwContent, messageID, element.start);
                        lastIndex = content.Length;
                        continue;
                }

                lastIndex = element.end;
            }

            // Render any remaining text
            if (lastIndex < content.Length)
            {
                string afterText = content.Substring(lastIndex);
                if (!string.IsNullOrWhiteSpace(afterText))
                {
                    Misc.RenderHtmlElements(afterText, true, true, true, false, limitImageWidth: true);
                }
            }
        }

        private static void RenderProfileEmbed(System.Text.RegularExpressions.Match match, long messageID)
        {
            int profileID = int.Parse(match.Groups[1].Value);

            // Get profile info from cache or fetch from server
            string profileName = null;
            IDalamudTextureWrap avatarTexture = null;

            // Check the profile info cache first
            var cachedInfo = GroupsData.GetCachedProfileInfo(profileID);
            if (cachedInfo != null)
            {
                profileName = cachedInfo.name;
                avatarTexture = cachedInfo.avatar;

                // If we have URL but no avatar texture yet, fetch it
                if (avatarTexture == null && !string.IsNullOrEmpty(cachedInfo.avatarUrl))
                {
                    GroupsData.FetchAndCacheAvatarAsync(profileID, cachedInfo.avatarUrl);
                }
            }
            else
            {
                // Try to get from current group members as fallback
                if (currentGroup?.members != null)
                {
                    var member = currentGroup.members.FirstOrDefault(m => m.profileID == profileID);
                    if (member != null)
                    {
                        profileName = member.name;
                        avatarTexture = member.avatar;
                    }
                }

                // If still no info, request from server
                if (string.IsNullOrEmpty(profileName) && !GroupsData.IsProfileInfoFetchPending(profileID))
                {
                    GroupsData.MarkProfileInfoFetchPending(profileID);
                    DataSender.FetchProfileInfo(profileID);
                }
            }

            // Use placeholder if name not yet loaded
            if (string.IsNullOrEmpty(profileName))
            {
                profileName = "Loading...";
            }

            // Calculate dynamic size based on content
            float avatarSize = 40f;
            float padding = 8f;
            float buttonHeight = ImGui.GetFrameHeight();
            float textHeight = ImGui.GetTextLineHeight();
            float contentHeight = Math.Max(avatarSize, textHeight + buttonHeight + 8f) + padding * 2;

            // Calculate width based on name length
            float nameWidth = ImGui.CalcTextSize(profileName).X;
            float buttonWidth = ImGui.CalcTextSize("View Profile").X + 20f;
            float textSectionWidth = Math.Max(nameWidth, buttonWidth) + 10f;
            float contentWidth = avatarSize + textSectionWidth + padding * 3;
            contentWidth = Math.Max(contentWidth, 200f); // Minimum width

            using (ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.15f, 0.15f, 0.2f, 1f)))
            {
                using (var child = ImRaii.Child($"ProfileEmbed_{messageID}_{profileID}", new Vector2(contentWidth, contentHeight), true, ImGuiWindowFlags.NoScrollbar))
                {
                    if (child.Success)
                    {
                        // Avatar section
                        ImGui.BeginGroup();

                        if (avatarTexture != null)
                        {
                            ImGui.Image(avatarTexture.Handle, new Vector2(avatarSize, avatarSize));
                        }
                        else
                        {
                            // Colored circle as avatar placeholder
                            var drawList = ImGui.GetWindowDrawList();
                            var cursorPos = ImGui.GetCursorScreenPos();
                            drawList.AddCircleFilled(
                                new Vector2(cursorPos.X + avatarSize / 2, cursorPos.Y + avatarSize / 2),
                                avatarSize / 2,
                                ImGui.GetColorU32(new Vector4(0.4f, 0.6f, 0.8f, 1f))
                            );
                            ImGui.Dummy(new Vector2(avatarSize, avatarSize));
                        }

                        ImGui.EndGroup();
                        ImGui.SameLine();

                        // Text and button section
                        ImGui.BeginGroup();
                        ImGui.TextColored(new Vector4(0.8f, 0.8f, 1f, 1f), profileName);

                        if (ImGui.SmallButton($"View Profile##{messageID}_{profileID}"))
                        {
                            // Open the target profile window and fetch the profile
                            Plugin.plugin.OpenTargetWindow();
                            TargetProfileWindow.RequestingProfile = true;
                            TargetProfileWindow.ResetAllData();
                            DataSender.FetchProfile(Plugin.character, false, -1, string.Empty, string.Empty, profileID);
                        }
                        ImGui.EndGroup();
                    }
                }
            }
        }

        #region Group Search UI

        /// <summary>
        /// Draws the group search bar and results at the top of the groups panel
        /// </summary>
        private static void DrawGroupSearchBar()
        {
            float searchBarWidth = ImGui.GetContentRegionAvail().X;

            // Search input with icon
            ImGui.SetNextItemWidth(searchBarWidth - 80);
            bool enterPressed = ImGui.InputTextWithHint("##GroupSearchInput", "Search public groups...", ref groupSearchQuery, 100, ImGuiInputTextFlags.EnterReturnsTrue);

            ImGui.SameLine();

            // Search button
            bool searchClicked = ImGui.Button("Search", new Vector2(70, 0));

            if ((enterPressed || searchClicked) && !string.IsNullOrWhiteSpace(groupSearchQuery))
            {
                DataSender.SearchPublicGroups(Plugin.character, groupSearchQuery);
                showSearchResults = true;
            }

            // Show search results if we have any or if search is in progress
            if (showSearchResults)
            {
                DrawGroupSearchResults();
            }

            ImGui.Separator();
        }

        /// <summary>
        /// Draws the search results panel
        /// </summary>
        private static void DrawGroupSearchResults()
        {
            // Show loading indicator
            if (DataReceiver.groupSearchInProgress)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Searching...");
                return;
            }

            var results = DataReceiver.groupSearchResults;

            // Header with close button
            ImGui.BeginGroup();
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1f), $"Search Results ({results?.Count ?? 0})");
            ImGui.SameLine(ImGui.GetContentRegionAvail().X - 20);
            if (ImGui.SmallButton("X##CloseSearch"))
            {
                showSearchResults = false;
                DataReceiver.groupSearchResults.Clear();
            }
            ImGui.EndGroup();

            if (results == null || results.Count == 0)
            {
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "No public groups found matching your search.");
                ImGui.Spacing();
                return;
            }

            // Scrollable results area
            float resultsHeight = Math.Min(results.Count * 70f, 250f);
            using (var child = ImRaii.Child("GroupSearchResults", new Vector2(-1, resultsHeight), true))
            {
                if (child.Success)
                {
                    foreach (var result in results)
                    {
                        DrawGroupSearchResultItem(result);
                    }
                }
            }
        }

        /// <summary>
        /// Draws a single search result item similar to the group invite embed
        /// </summary>
        private static void DrawGroupSearchResultItem(GroupSearchResult result)
        {
            // Check if user is already a member of this group
            var memberGroup = groups?.FirstOrDefault(g => g.groupID == result.groupID);
            bool isMember = memberGroup != null;
            bool isPending = GroupsData.pendingJoinRequests.Contains(result.groupID);

            // Fetch logo if needed
            if (result.logo == null && !string.IsNullOrEmpty(result.logoUrl))
            {
                FetchSearchResultLogo(result);
            }

            float logoSize = 40f;
            float padding = 8f;
            float contentHeight = logoSize + padding * 2;

            using (ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.15f, 0.15f, 0.2f, 1f)))
            {
                using (var child = ImRaii.Child($"SearchResult_{result.groupID}", new Vector2(-1, contentHeight), true, ImGuiWindowFlags.NoScrollbar))
                {
                    if (child.Success)
                    {
                        // Logo
                        ImGui.BeginGroup();
                        if (result.logo != null)
                        {
                            ImGui.Image(result.logo.Handle, new Vector2(logoSize, logoSize));
                        }
                        else
                        {
                            // Placeholder
                            var drawList = ImGui.GetWindowDrawList();
                            var cursorPos = ImGui.GetCursorScreenPos();
                            drawList.AddRectFilled(
                                cursorPos,
                                new Vector2(cursorPos.X + logoSize, cursorPos.Y + logoSize),
                                ImGui.GetColorU32(new Vector4(0.4f, 0.4f, 0.5f, 1f)),
                                4f
                            );
                            ImGui.Dummy(new Vector2(logoSize, logoSize));
                        }
                        ImGui.EndGroup();
                        ImGui.SameLine();

                        // Text section
                        ImGui.BeginGroup();
                        ImGui.TextColored(new Vector4(1f, 1f, 1f, 1f), result.name ?? "Unknown Group");

                        // Member count
                        ImGui.SameLine();
                        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), $"({result.memberCount} members)");

                        // Description (truncated)
                        string desc = result.description ?? "";
                        if (desc.Length > 60) desc = desc.Substring(0, 57) + "...";
                        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), desc);
                        ImGui.EndGroup();

                        // Button section (right-aligned)
                        ImGui.SameLine(ImGui.GetContentRegionAvail().X - 100);
                        ImGui.BeginGroup();
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 8); // Center vertically

                        if (isMember)
                        {
                            if (ImGui.SmallButton($"View##{result.groupID}"))
                            {
                                LoadGroup(memberGroup);
                                showSearchResults = false;
                            }
                        }
                        else if (isPending)
                        {
                            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.3f, 1f), "Pending");
                        }
                        else if (result.openInvite)
                        {
                            // Open groups - direct join
                            using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.2f, 0.5f, 0.2f, 1f)))
                            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.6f, 0.3f, 1f)))
                            using (ImRaii.PushColor(ImGuiCol.ButtonActive, new Vector4(0.15f, 0.4f, 0.15f, 1f)))
                            {
                                if (ImGui.SmallButton($"Join##{result.groupID}"))
                                {
                                    DataSender.RequestJoinGroup(Plugin.character, result.groupID);
                                    GroupsData.pendingJoinRequests.Add(result.groupID);
                                }
                            }
                        }
                        else
                        {
                            // Closed groups - request to join
                            using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.3f, 0.3f, 0.5f, 1f)))
                            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.4f, 0.4f, 0.6f, 1f)))
                            using (ImRaii.PushColor(ImGuiCol.ButtonActive, new Vector4(0.25f, 0.25f, 0.4f, 1f)))
                            {
                                if (ImGui.SmallButton($"Request##{result.groupID}"))
                                {
                                    // Open the join request dialog
                                    AbsoluteRP.Windows.Social.Views.Groups.GroupJoinRequestDialog.Open(result.groupID, result.name, result.description);
                                }
                            }
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip("This group requires approval to join. Click to send a request.");
                            }
                        }
                        ImGui.EndGroup();
                    }
                }
            }
            ImGui.Spacing();
        }

        /// <summary>
        /// Fetches and caches logo for a search result
        /// </summary>
        private static async void FetchSearchResultLogo(GroupSearchResult result)
        {
            if (string.IsNullOrEmpty(result.logoUrl)) return;
            if (result.logo != null) return;

            try
            {
                var logoBytes = await Imaging.FetchUrlImageBytes(result.logoUrl);
                if (logoBytes != null && logoBytes.Length > 0)
                {
                    var logoTexture = await Plugin.TextureProvider.CreateFromImageAsync(logoBytes);
                    if (logoTexture != null)
                    {
                        result.logo = logoTexture;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Failed to fetch search result logo: {ex.Message}");
            }
        }

        #endregion

        private static void RenderGroupInviteEmbed(System.Text.RegularExpressions.Match match, long messageID)
        {
            int groupID = int.Parse(match.Groups[1].Value);

            // Get group info from cache or fetch from server
            string groupName = null;
            IDalamudTextureWrap logoTexture = null;

            // First check if user is a member (most up-to-date info)
            var memberGroup = groups?.FirstOrDefault(g => g.groupID == groupID);
            if (memberGroup != null)
            {
                groupName = memberGroup.name;
                logoTexture = memberGroup.logo;
                // Also update the cache
                GroupsData.CacheGroupInfo(groupID, memberGroup.name, memberGroup.logoUrl, memberGroup.logo);
            }
            else
            {
                // Check the group info cache
                var cachedInfo = GroupsData.GetCachedGroupInfo(groupID);
                if (cachedInfo != null)
                {
                    groupName = cachedInfo.name;
                    logoTexture = cachedInfo.logo;

                    // If we have URL but no logo texture yet, fetch it
                    if (logoTexture == null && !string.IsNullOrEmpty(cachedInfo.logoUrl))
                    {
                        GroupsData.FetchAndCacheLogoAsync(groupID, cachedInfo.logoUrl);
                    }
                }
                else if (!GroupsData.IsGroupInfoFetchPending(groupID))
                {
                    // Request group info from server
                    GroupsData.MarkGroupInfoFetchPending(groupID);
                    DataSender.FetchGroupInfo(groupID);
                }
            }

            // Use placeholder if name not yet loaded
            if (string.IsNullOrEmpty(groupName))
            {
                groupName = "Loading...";
            }

            // Check if user is already a member of this group
            bool isMember = memberGroup != null;
            bool isPending = GroupsData.pendingJoinRequests.Contains(groupID);

            // Calculate dynamic size based on content
            float logoSize = 40f;
            float padding = 8f;
            float textHeight = ImGui.GetTextLineHeight();
            float buttonHeight = ImGui.GetFrameHeight();
            float contentHeight = Math.Max(logoSize, textHeight + buttonHeight + 8f) + padding * 2;

            // Calculate width based on name length
            float nameWidth = ImGui.CalcTextSize(groupName).X;
            float buttonWidth = ImGui.CalcTextSize("View Group").X + 20f;
            float joinButtonWidth = ImGui.CalcTextSize("Join Group").X + 20f;
            float textSectionWidth = Math.Max(nameWidth, Math.Max(buttonWidth, joinButtonWidth)) + 10f;
            float contentWidth = logoSize + textSectionWidth + padding * 3;
            contentWidth = Math.Max(contentWidth, 220f); // Minimum width

            using (ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.2f, 0.15f, 0.15f, 1f)))
            {
                using (var child = ImRaii.Child($"GroupInviteEmbed_{messageID}_{groupID}", new Vector2(contentWidth, contentHeight), true, ImGuiWindowFlags.NoScrollbar))
                {
                    if (child.Success)
                    {
                        // Logo section
                        ImGui.BeginGroup();

                        if (logoTexture != null)
                        {
                            ImGui.Image(logoTexture.Handle, new Vector2(logoSize, logoSize));
                        }
                        else
                        {
                            // Colored rectangle as logo placeholder
                            var drawList = ImGui.GetWindowDrawList();
                            var cursorPos = ImGui.GetCursorScreenPos();
                            drawList.AddRectFilled(
                                cursorPos,
                                new Vector2(cursorPos.X + logoSize, cursorPos.Y + logoSize),
                                ImGui.GetColorU32(new Vector4(0.6f, 0.4f, 0.4f, 1f)),
                                4f
                            );
                            ImGui.Dummy(new Vector2(logoSize, logoSize));
                        }

                        ImGui.EndGroup();
                        ImGui.SameLine();

                        // Text and button section
                        ImGui.BeginGroup();
                        ImGui.TextColored(new Vector4(1f, 0.8f, 0.8f, 1f), groupName);

                        if (isMember)
                        {
                            // User is already a member - show View Group button
                            if (ImGui.SmallButton($"View Group##{messageID}_{groupID}"))
                            {
                                // Switch to this group
                                LoadGroup(memberGroup);
                            }
                        }
                        else if (isPending)
                        {
                            // Request already sent
                            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.3f, 1f), "Request Sent");
                        }
                        else
                        {
                            // User is not a member - show Join Group button
                            using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.2f, 0.5f, 0.2f, 1f)))
                            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.6f, 0.3f, 1f)))
                            using (ImRaii.PushColor(ImGuiCol.ButtonActive, new Vector4(0.15f, 0.4f, 0.15f, 1f)))
                            {
                                if (ImGui.SmallButton($"Join Group##{messageID}_{groupID}"))
                                {
                                    // Request to join the group
                                    DataSender.RequestJoinGroup(Plugin.character, groupID);
                                    GroupsData.pendingJoinRequests.Add(groupID);
                                }
                            }
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip("Request to join this group");
                            }
                        }
                        ImGui.EndGroup();
                    }
                }
            }
        }

        private static void RenderSpoilerContent(string content, long messageID, int position)
        {
            string spoilerKey = $"{messageID}_{position}";
            bool isRevealed = revealedSpoilers.Contains(spoilerKey);

            if (!isRevealed)
            {
                using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.3f, 0.3f, 0.3f, 1f)))
                using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.4f, 0.4f, 0.4f, 1f)))
                {
                    if (ImGui.Button($"[Spoiler - Click to reveal]##{spoilerKey}"))
                    {
                        revealedSpoilers.Add(spoilerKey);
                    }
                }
            }
            else
            {
                using (ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.2f, 0.2f, 0.25f, 0.8f)))
                {
                    ImGui.BeginGroup();
                    ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.7f, 1f), "[Spoiler]");
                    Misc.RenderHtmlElements(content, true, true, true, false, limitImageWidth: true);

                    ImGui.SameLine();
                    if (ImGui.SmallButton($"Hide##{spoilerKey}"))
                    {
                        revealedSpoilers.Remove(spoilerKey);
                    }
                    ImGui.EndGroup();
                }
            }
        }

        private static void RenderNsfwContent(string content, long messageID, int position)
        {
            string nsfwKey = $"{messageID}_{position}";
            bool isRevealed = revealedNsfw.Contains(nsfwKey);

            if (!isRevealed)
            {
                using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.5f, 0.2f, 0.2f, 1f)))
                using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.6f, 0.3f, 0.3f, 1f)))
                {
                    if (ImGui.Button($"[NSFW Content - Click to reveal]##{nsfwKey}"))
                    {
                        revealedNsfw.Add(nsfwKey);
                    }
                }
            }
            else
            {
                using (ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.3f, 0.15f, 0.15f, 0.8f)))
                {
                    ImGui.BeginGroup();
                    ImGui.TextColored(new Vector4(0.9f, 0.4f, 0.4f, 1f), "[NSFW]");
                    Misc.RenderHtmlElements(content, true, true, true, false, limitImageWidth: true);

                    ImGui.SameLine();
                    if (ImGui.SmallButton($"Hide##{nsfwKey}"))
                    {
                        revealedNsfw.Remove(nsfwKey);
                    }
                    ImGui.EndGroup();
                }
            }
        }

        #endregion
    }
}