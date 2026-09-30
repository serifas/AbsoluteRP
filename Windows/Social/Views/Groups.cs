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
using AbsoluteRP.RsUI.Pages;
using AbsoluteRP.Network;
using AbsoluteRP.RsUI;
using Dalamud.Interface;

namespace AbsoluteRP.Windows.Social.Views
{
    // Shared RsUI helpers for every Groups screen (list, chat, manager, dialogs). Thin wrappers so the legacy call sites read the same while drawing with RsElements / RsTheme instead of ThemeManager and hard-coded colours.
    internal static class GroupUi
    {
        public static float S(float v) => RsTheme.S(v);

        public static Vector4 Fade(Vector4 c, float alpha) => new Vector4(c.X, c.Y, c.Z, alpha);

        // Thin border-coloured rule with a little breathing room after it.
        public static void Divider(float after = 6f)
        {
            var dl = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            float w = Math.Max(0f, ImGui.GetContentRegionAvail().X);
            dl.AddLine(new Vector2(pos.X, pos.Y + S(1f)), new Vector2(pos.X + w, pos.Y + S(1f)), RsTheme.U.Border, RsTheme.BorderThickness);
            ImGui.Dummy(new Vector2(0f, S(after)));
        }

        // Small upper-case muted heading used to chunk cards and forms.
        public static void SectionLabel(string text)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted((text ?? string.Empty).ToUpperInvariant());
            ImGui.PopStyleColor();
            ImGui.Dummy(new Vector2(0f, S(2f)));
        }

        public static void Muted(string text)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted(text ?? string.Empty);
            ImGui.PopStyleColor();
        }

        public static void MutedWrapped(string text)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextWrapped(text ?? string.Empty);
            ImGui.PopStyleColor();
        }

        public static void Secondary(string text)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextSecondary);
            ImGui.TextUnformatted(text ?? string.Empty);
            ImGui.PopStyleColor();
        }

        // Legacy sizes are design pixels; scale them and resolve negative (avail-relative) axes like ThemeManager used to.
        private static Vector2 ResolveSize(Vector2 size)
        {
            if (size.X > 0f) size.X = S(size.X);
            if (size.Y > 0f) size.Y = S(size.Y);
            if (size.X < 0f || size.Y < 0f)
            {
                var avail = ImGui.GetContentRegionAvail();
                if (size.X < 0f) size.X = Math.Max(4f, avail.X + size.X);
                if (size.Y < 0f) size.Y = Math.Max(4f, avail.Y + size.Y);
            }
            return size;
        }

        public static bool Button(string label, Vector2 size = default, RsElements.ButtonVariant variant = RsElements.ButtonVariant.Primary)
            => RsElements.Button(label, variant, ResolveSize(size));

        public static bool Ghost(string label, Vector2 size = default)
            => RsElements.Button(label, RsElements.ButtonVariant.Ghost, ResolveSize(size));

        public static bool Danger(string label, Vector2 size = default)
            => RsElements.Button(label, RsElements.ButtonVariant.Danger, ResolveSize(size));

        public static bool Success(string label, Vector2 size = default)
            => RsElements.Button(label, RsElements.ButtonVariant.Success, ResolveSize(size));

        // RsElements.InputText sized from the pending SetNextItemWidth (or the default item width). A visible "Label##id" becomes the placeholder.
        public static bool Input(string label, ref string value, int maxLength = 256, string placeholder = null)
        {
            float w = ImGui.CalcItemWidth();
            if (w <= 0f) w = ImGui.GetContentRegionAvail().X;
            int hash = label.IndexOf("##", StringComparison.Ordinal);
            if (placeholder == null)
                placeholder = hash > 0 ? label.Substring(0, hash) : string.Empty;
            return RsElements.InputText(label, ref value, maxLength, placeholder, Math.Max(40f, w) / Math.Max(0.01f, RsTheme.Scale));
        }

        // Rounded tinted tag ("Owner", "Pending", unread counts ...).
        public static void Chip(string text, Vector4 color)
        {
            var dl = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            var ts = ImGui.CalcTextSize(text);
            var pad = new Vector2(S(7f), S(2f));
            var size = ts + pad * 2f;
            dl.AddRectFilled(pos, pos + size, ImGui.ColorConvertFloat4ToU32(Fade(color, 0.18f)), size.Y * 0.5f);
            dl.AddRect(pos, pos + size, ImGui.ColorConvertFloat4ToU32(Fade(color, 0.55f)), size.Y * 0.5f, ImDrawFlags.None, RsTheme.BorderThickness);
            dl.AddText(pos + pad, ImGui.ColorConvertFloat4ToU32(color), text);
            ImGui.Dummy(size);
        }

        public static void Icon(FontAwesomeIcon icon, Vector4 color)
        {
            using (RsIcons.Push())
            {
                ImGui.PushStyleColor(ImGuiCol.Text, color);
                ImGui.TextUnformatted(icon.ToIconString());
                ImGui.PopStyleColor();
            }
        }

        public static Vector2 IconSize(FontAwesomeIcon icon)
        {
            using (RsIcons.Push())
                return ImGui.CalcTextSize(icon.ToIconString());
        }

        public static void DrawIconAt(ImDrawListPtr dl, FontAwesomeIcon icon, Vector2 pos, uint color)
        {
            using (RsIcons.Push())
                dl.AddText(pos, color, icon.ToIconString());
        }

        public static FontAwesomeIcon ChannelIcon(int channelType) => channelType switch
        {
            1 => FontAwesomeIcon.Bullhorn,
            2 => FontAwesomeIcon.Scroll,
            3 => FontAwesomeIcon.Tags,
            4 => FontAwesomeIcon.ClipboardList,
            _ => FontAwesomeIcon.Hashtag,
        };

        // Card surface drawn straight onto the draw list (cheap, nest-safe).
        public static void CardBackground(Vector2 min, Vector2 max, bool hovered = false, bool selected = false)
        {
            var dl = ImGui.GetWindowDrawList();
            var r = S(8f);
            dl.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(hovered ? RsTheme.BgSecondary : RsTheme.PanelSurface), r);
            var border = selected ? RsTheme.U.AccentPrimary : hovered ? RsTheme.U.BorderStrong : RsTheme.U.Border;
            dl.AddRect(min, max, border, r, ImDrawFlags.None, RsTheme.BorderThickness);
        }

        // Circular avatar from a texture, falling back to an initial on a tertiary disc. Advances the cursor like a widget.
        public static void Avatar(IDalamudTextureWrap tex, string name, float diameter, Vector4? ring = null)
        {
            var dl = ImGui.GetWindowDrawList();
            var min = ImGui.GetCursorScreenPos();
            var max = min + new Vector2(diameter, diameter);
            var center = (min + max) * 0.5f;
            float radius = diameter * 0.5f;
            dl.AddCircleFilled(center, radius, RsTheme.U.BgTertiary, 32);
            bool drawn = false;
            if (GroupsData.IsTextureValid(tex))
            {
                try
                {
                    dl.AddImageRounded(tex.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, radius);
                    drawn = true;
                }
                catch (ObjectDisposedException) { }
            }
            if (!drawn && !string.IsNullOrEmpty(name))
            {
                var letter = name.Substring(0, 1).ToUpperInvariant();
                var ts = ImGui.CalcTextSize(letter);
                dl.AddText(center - ts * 0.5f, RsTheme.U.TextSecondary, letter);
            }
            dl.AddCircle(center, radius, ImGui.ColorConvertFloat4ToU32(ring ?? RsTheme.Border), 32, Math.Max(1f, S(1.5f)));
            ImGui.Dummy(new Vector2(diameter, diameter));
        }

        // Rounded-square logo (group icons), same fallback as Avatar.
        public static void Logo(IDalamudTextureWrap tex, string name, float size, float rounding)
        {
            var dl = ImGui.GetWindowDrawList();
            var min = ImGui.GetCursorScreenPos();
            DrawLogoAt(dl, tex, name, min, size, rounding);
            ImGui.Dummy(new Vector2(size, size));
        }

        public static void DrawLogoAt(ImDrawListPtr dl, IDalamudTextureWrap tex, string name, Vector2 min, float size, float rounding)
        {
            var max = min + new Vector2(size, size);
            dl.AddRectFilled(min, max, RsTheme.U.BgTertiary, rounding);
            if (!string.IsNullOrEmpty(name))
            {
                var letter = name.Substring(0, 1).ToUpperInvariant();
                var ts = ImGui.CalcTextSize(letter);
                dl.AddText((min + max) * 0.5f - ts * 0.5f, RsTheme.U.TextSecondary, letter);
            }
            if (GroupsData.IsTextureValid(tex))
            {
                try { dl.AddImageRounded(tex.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding); }
                catch (ObjectDisposedException) { }
            }
        }

        // Centre the next row of `totalWidth` (already scaled) in the window.
        public static void CenterRow(float totalWidth)
        {
            float x = (ImGui.GetWindowWidth() - totalWidth) * 0.5f;
            if (x > 0f) ImGui.SetCursorPosX(x);
        }

        // Parse "#RRGGBB" once per distinct string instead of every frame.
        private static readonly Dictionary<string, Vector4> hexCache = new Dictionary<string, Vector4>();
        public static Vector4 Hex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return RsTheme.TextPrimary;
            if (hexCache.TryGetValue(hex, out var c)) return c;
            c = new Vector4(1f, 1f, 1f, 1f);
            var h = hex.TrimStart('#');
            if (h.Length == 6)
            {
                try
                {
                    c = new Vector4(Convert.ToInt32(h.Substring(0, 2), 16) / 255f,
                                    Convert.ToInt32(h.Substring(2, 2), 16) / 255f,
                                    Convert.ToInt32(h.Substring(4, 2), 16) / 255f, 1f);
                }
                catch { }
            }
            if (hexCache.Count > 512) hexCache.Clear();
            hexCache[hex] = c;
            return c;
        }
    }

    // Main groups UI - handles the groups list, group view with channels/chat, and all group interactions. This is the largest UI file and contains the chat rendering, member list, and group navigation.
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

        /// Caches group info (name, logo) for display in embeds.
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

        /// Gets cached group info, or null if not cached.
        public static GroupInfoCache GetCachedGroupInfo(int groupID)
        {
            if (groupInfoCache.TryGetValue(groupID, out var info))
            {
                return info;
            }
            return null;
        }

        /// Checks if group info is currently being fetched.
        public static bool IsGroupInfoFetchPending(int groupID)
        {
            return pendingGroupInfoFetches.Contains(groupID);
        }

        /// Marks a group info fetch as pending.
        public static void MarkGroupInfoFetchPending(int groupID)
        {
            pendingGroupInfoFetches.Add(groupID);
        }

        /// Fetches and caches a group logo asynchronously from a URL.
        public static async void FetchAndCacheLogoAsync(int groupID, string logoUrl)
        {
            if (string.IsNullOrEmpty(logoUrl)) return;

            var info = GetCachedGroupInfo(groupID);
            if (info?.logo != null) return; // Already have logo

            // Called from the draw loop every frame until the logo exists - only one download per group may be in flight.
            string key = "grouplogo:" + groupID;
            if (!TryBeginImageFetch(key)) return;
            bool ok = false;
            try
            {
                var logoBytes = await Imaging.FetchUrlImageBytes(logoUrl);
                if (logoBytes != null && logoBytes.Length > 0)
                {
                    var logoTexture = await Plugin.TextureProvider.CreateFromImageAsync(logoBytes);
                    CacheGroupInfo(groupID, null, logoUrl, logoTexture);
                    ok = logoTexture != null;
                }
            }
            catch
            {
                // Failed to fetch logo
            }
            finally
            {
                EndImageFetch(key, ok);
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

        /// Caches profile info (name, avatar) for display in embeds.
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

        /// Gets cached profile info, or null if not cached.
        public static ProfileInfoCache GetCachedProfileInfo(int profileID)
        {
            if (profileInfoCache.TryGetValue(profileID, out var info))
            {
                return info;
            }
            return null;
        }

        /// Checks if profile info is currently being fetched.
        public static bool IsProfileInfoFetchPending(int profileID)
        {
            return pendingProfileInfoFetches.Contains(profileID);
        }

        /// Marks a profile info fetch as pending.
        public static void MarkProfileInfoFetchPending(int profileID)
        {
            pendingProfileInfoFetches.Add(profileID);
        }

        /// Fetches and caches a profile avatar asynchronously from a URL.
        public static async void FetchAndCacheAvatarAsync(int profileID, string avatarUrl)
        {
            if (string.IsNullOrEmpty(avatarUrl)) return;

            var info = GetCachedProfileInfo(profileID);
            if (info?.avatar != null) return; // Already have avatar

            // Called from the draw loop every frame until the avatar exists - only one download per profile may be in flight.
            string key = "profileavatar:" + profileID;
            if (!TryBeginImageFetch(key)) return;
            bool ok = false;
            try
            {
                var avatarBytes = await Imaging.FetchUrlImageBytes(avatarUrl);
                if (avatarBytes != null && avatarBytes.Length > 0)
                {
                    var avatarTexture = await Plugin.TextureProvider.CreateFromImageAsync(avatarBytes);
                    CacheProfileInfo(profileID, null, avatarUrl, avatarTexture);
                    ok = avatarTexture != null;
                }
            }
            catch
            {
                // Failed to fetch avatar
            }
            finally
            {
                EndImageFetch(key, ok);
            }
        }

        /// Clears pending join requests for groups that are now in the user's groups list. Called when group memberships are refreshed from server.
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

        /// Checks if a texture is valid and can be rendered. Returns false if texture is null, disposed, or has an invalid handle.
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

        // Track textures already queued for disposal to prevent double-disposal Use object reference equality to track textures
        private static HashSet<IDalamudTextureWrap> queuedTexturesForDisposal = new HashSet<IDalamudTextureWrap>();

        /// Queues a texture for deferred disposal. Use this instead of immediate Dispose() to avoid disposing textures while they may still be in use by the render thread. Prevents double-queueing of the same texture.
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

        /// Processes all pending texture disposals. Call this at a safe point, such as at the end of a frame or when no rendering is in progress.
        public static void ProcessPendingDisposals()
        {
            lock (disposeQueueLock)
            {
                // This runs right after LoadGroupList in the same frame, so the textures may still be referenced by this frame's draw list. Hand them to the graveyard, which frees them a few frames later. They stay in queuedTexturesForDisposal so nothing re-caches them in the meantime.
                while (texturesToDispose.Count > 0)
                {
                    var texture = texturesToDispose.Dequeue();
                    if (texture != null)
                        TextureGraveyard.Enqueue(texture);
                }
                // The set only guards against re-use; trim it once it grows large (the graveyard has long since freed those wrappers).
                if (queuedTexturesForDisposal.Count > 2048)
                    queuedTexturesForDisposal.Clear();
            }
        }

        /// Checks if a texture is queued for disposal (and should not be used).
        private static bool IsTextureQueuedForDisposal(IDalamudTextureWrap texture)
        {
            if (texture == null) return true;
            lock (disposeQueueLock)
            {
                return queuedTexturesForDisposal.Contains(texture);
            }
        }

        /// Gets or caches a member avatar texture by member ID. Thread-safe method for accessing member avatars.
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

        /// Clears the member avatar cache and queues all textures for disposal.
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

        /// Queues all cached avatar textures for deferred disposal and clears the cache. Call this when switching channels or closing the group.
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

        /// Resets all group state including textures. Call this when the window is closed.
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

        /// Clears the currently selected channel/category. Called when user is removed from group.
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

        /// Gets or creates an avatar texture for a user. Reuses cached textures to prevent memory leaks.
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
            // Legacy widgets inside the groups screens (tree nodes, tabs, checkboxes, modals) pick up the RsTheme palette from here.
            RsGlobalStyle.Push();
            try
            {
                DrawGroupsRoot();
            }
            finally
            {
                RsGlobalStyle.Pop();
            }
        }

        private static float S(float v) => RsTheme.S(v);

        private static void DrawGroupsRoot()
        {
            // Search bar pinned at the top
            DrawGroupSearchBar();

            // Auto-load the selected group once (initial selection / after the list refreshes). Clicks load directly in DrawGroupRail, so this never double-fires the eight fetch requests.
            if (selectedNavIndex != previousNavIndex && selectedNavIndex >= 0 && selectedNavIndex < groups.Count)
            {
                previousNavIndex = selectedNavIndex;
                var selectedGroup = groups[selectedNavIndex];
                Plugin.PluginLog.Info($"[Groups] Navigation changed to index {selectedNavIndex}, loading group: {selectedGroup.name} (ID: {selectedGroup.groupID})");
                LoadGroup(selectedGroup);
            }

            var avail = ImGui.GetContentRegionAvail();
            float height = Math.Max(S(240f), avail.Y);
            float railW = S(68f);

            // Left rail: one rounded logo per group + create button
            ImGui.PushStyleColor(ImGuiCol.ChildBg, RsTheme.PanelSurface);
            ImGui.PushStyleColor(ImGuiCol.Border, RsTheme.Border);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, S(8f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(S(10f), S(10f)));
            bool railOpen = ImGui.BeginChild("##groups_rail", new Vector2(railW, height), true, ImGuiWindowFlags.NoScrollbar);
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(2);
            try
            {
                if (railOpen)
                    DrawGroupRail();
            }
            finally
            {
                ImGui.EndChild();
            }

            ImGui.SameLine(0f, S(10f));

            // Right: header card + channels/chat, or creation / manager
            bool mainOpen = ImGui.BeginChild("##groups_main", new Vector2(0f, height), false);
            try
            {
                if (mainOpen)
                    DrawGroupMain();
            }
            finally
            {
                ImGui.EndChild();
            }
        }

        private static void DrawGroupRail()
        {
            float btn = S(44f);
            var dl = ImGui.GetWindowDrawList();
            float innerW = ImGui.GetContentRegionAvail().X;
            float offsetX = Math.Max(0f, (innerW - btn) * 0.5f);
            int rightClickedGroupIndex = -1;

            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                if (g == null) continue;
                ImGui.PushID(i);
                try
                {
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);
                    var pos = ImGui.GetCursorScreenPos();
                    bool clicked = ImGui.InvisibleButton("##grp", new Vector2(btn, btn));
                    bool hovered = ImGui.IsItemHovered();
                    if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                        rightClickedGroupIndex = i;

                    bool selected = currentGroup != null && currentGroup.groupID == g.groupID;
                    float rounding = (selected || hovered) ? S(12f) : btn * 0.5f;

                    // Selection pill hugging the rail's left edge.
                    if (selected || hovered)
                    {
                        float pillH = selected ? btn * 0.7f : btn * 0.35f;
                        float px = pos.X - offsetX - S(6f);
                        float py = pos.Y + (btn - pillH) * 0.5f;
                        dl.AddRectFilled(new Vector2(px, py), new Vector2(px + S(4f), py + pillH),
                            selected ? RsTheme.U.AccentPrimary : RsTheme.U.TextMuted, S(2f));
                    }

                    GroupUi.DrawLogoAt(dl, g.logo, g.name, pos, btn, rounding);
                    if (selected)
                        dl.AddRect(pos, pos + new Vector2(btn, btn), RsTheme.U.AccentPrimary, rounding, ImDrawFlags.None, Math.Max(1f, S(2f)));
                    else if (hovered)
                        dl.AddRect(pos, pos + new Vector2(btn, btn), RsTheme.U.BorderStrong, rounding, ImDrawFlags.None, RsTheme.BorderThickness);

                    // Unread badge (top-right).
                    int unread = g.GetTotalUnreadCount();
                    if (unread > 0)
                    {
                        string badge = unread > 99 ? "99+" : unread.ToString();
                        var ts = ImGui.CalcTextSize(badge);
                        float bh = ts.Y + S(2f);
                        float bw = Math.Max(bh, ts.X + S(8f));
                        var bmax = new Vector2(pos.X + btn + S(3f), pos.Y + bh - S(3f));
                        var bmin = new Vector2(bmax.X - bw, bmax.Y - bh);
                        dl.AddRectFilled(bmin, bmax, RsTheme.U.AccentDanger, bh * 0.5f);
                        dl.AddRect(bmin, bmax, RsTheme.U.BgSecondary, bh * 0.5f, ImDrawFlags.None, Math.Max(1f, S(2f)));
                        dl.AddText(new Vector2(bmin.X + (bw - ts.X) * 0.5f, bmin.Y + (bh - ts.Y) * 0.5f), 0xFFFFFFFFu, badge);
                    }

                    if (hovered)
                        ImGui.SetTooltip(unread > 0 ? $"{g.name}  ({unread} unread)" : g.name);

                    if (clicked)
                    {
                        // One load per click (the old nav both invoked its action and tripped the index-change path, sending every fetch twice). Re-clicking the open group refreshes it when online, as before.
                        openGroupCreation = false;
                        manageGroup = false;
                        selectedNavIndex = i;
                        previousNavIndex = i;
                        if (!selected || Plugin.IsOnline())
                            LoadGroup(g);
                    }
                }
                finally
                {
                    ImGui.PopID();
                }
                ImGui.Dummy(new Vector2(0f, S(4f)));
            }

            // Handle right-click on a group icon
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
                    GroupUi.Muted(groupToLeave.name ?? "Group");
                    GroupUi.Divider(4f);
                    if (ImGui.MenuItem("Leave Group"))
                    {
                        showLeaveGroupConfirmation = true;
                        ImGui.CloseCurrentPopup();
                    }
                }
                ImGui.EndPopup();
            }

            // Separator + create button
            if (groups.Count > 0)
            {
                var p = ImGui.GetCursorScreenPos();
                dl.AddLine(new Vector2(p.X + offsetX + btn * 0.2f, p.Y), new Vector2(p.X + offsetX + btn * 0.8f, p.Y), RsTheme.U.Border, RsTheme.BorderThickness);
                ImGui.Dummy(new Vector2(0f, S(6f)));
            }

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);
            createGroup = RsElements.IconButton(FontAwesomeIcon.Plus, "grp_create", RsElements.ButtonVariant.Ghost, 44f);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Create Group");
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
                    GroupCreation._fileDialogManager = new AbsoluteRP.RsUI.RsFileDialogManager();
                if (GroupManager._fileDialogManager == null)
                    GroupManager._fileDialogManager = new AbsoluteRP.RsUI.RsFileDialogManager();
            }
        }

        // Small "back" row shown above the creation editor and group manager.
        private static bool DrawBackRow(string id, string title)
        {
            bool back = RsElements.IconButton(FontAwesomeIcon.ArrowLeft, id, RsElements.ButtonVariant.Ghost, 30f);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Back to chat");
            ImGui.SameLine(0f, S(10f));
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (S(30f) - ImGui.GetTextLineHeight()) * 0.5f);
            ImGui.TextUnformatted(title);
            ImGui.Dummy(new Vector2(0f, S(4f)));
            GroupUi.Divider(8f);
            return back;
        }

        private static void DrawGroupMain()
        {
            if (openGroupCreation)
            {
                manageGroup = false;
                if (DrawBackRow("grp_create_back", "Create a group"))
                    openGroupCreation = false;
                else
                    GroupCreation.DrawGroupBaseEditor();
                return;
            }

            if (manageGroup)
            {
                if (currentGroup == null)
                {
                    manageGroup = false;
                }
                else
                {
                    if (DrawBackRow("grp_manage_back", $"Manage {currentGroup.name}"))
                        manageGroup = false;
                    else
                        GroupManager.ManageGroup(currentGroup);
                    return;
                }
            }

            if (currentGroup == null)
            {
                DrawNoGroupSelected();
                return;
            }

            DrawGroupHeader();
            DrawGroupChannelsAndChat();
        }

        private static void DrawNoGroupSelected()
        {
            var avail = ImGui.GetContentRegionAvail();
            string title = groups.Count == 0 ? "You're not in any groups yet" : "Pick a group";
            string body = groups.Count == 0
                ? "Search for a public group above, or create your own with the + button."
                : "Choose a group from the left to open its channels.";
            float w = Math.Min(avail.X, S(420f));
            ImGui.SetCursorPos(new Vector2(ImGui.GetCursorPosX() + Math.Max(0f, (avail.X - w) * 0.5f),
                                           ImGui.GetCursorPosY() + Math.Max(0f, avail.Y * 0.3f)));
            ImGui.BeginGroup();
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + w);
            ImGui.TextUnformatted(title);
            GroupUi.MutedWrapped(body);
            ImGui.PopTextWrapPos();
            ImGui.EndGroup();
        }

        // Group banner card: logo, name, member count, description and the settings cog for owners / rank managers.
        private static void DrawGroupHeader()
        {
            var me = GetMyMember();
            bool isOwner = me?.owner == true;
            bool canManageRanks = me?.rank?.permissions?.canManageRanks == true;
            bool canCreateRanks = me?.rank?.permissions?.canCreateRanks == true;
            bool hasSettingsAccess = isOwner || canManageRanks || canCreateRanks;

            if (!RsElements.BeginPanel("grp_header_" + currentGroup.groupID, null, fitContentsX: false, fitContentsY: true, innerPadding: 12f))
            {
                RsElements.EndPanel();
                return;
            }
            try
            {
                float logo = S(52f);
                var rowStart = ImGui.GetCursorScreenPos();
                GroupUi.Logo(currentGroup.logo, currentGroup.name, logo, S(12f));
                ImGui.SameLine(0f, S(12f));
                ImGui.BeginGroup();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + S(4f));
                ImGui.TextUnformatted(currentGroup.name ?? "Group");
                int memberCount = currentGroup.members?.Count ?? 0;
                GroupUi.Muted($"{memberCount} member{(memberCount == 1 ? "" : "s")}{(isOwner ? "  ·  Owner" : me?.rank != null && !string.IsNullOrEmpty(me.rank.name) ? "  ·  " + me.rank.name : "")}");
                ImGui.EndGroup();

                if (hasSettingsAccess)
                {
                    float cog = 32f;
                    float right = RsElements.AvailContentWidth();
                    ImGui.SameLine();
                    var cur = ImGui.GetCursorScreenPos();
                    ImGui.SetCursorScreenPos(new Vector2(rowStart.X + right - S(cog), rowStart.Y + (logo - S(cog)) * 0.5f));
                    if (RsElements.IconButton(FontAwesomeIcon.Cog, "grp_settings", RsElements.ButtonVariant.Ghost, cog))
                        manageGroup = true;
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Manage Group");
                    ImGui.SetCursorScreenPos(new Vector2(rowStart.X, Math.Max(cur.Y, rowStart.Y) + logo));
                    ImGui.Dummy(Vector2.Zero);
                }

                if (!string.IsNullOrWhiteSpace(currentGroup.description))
                {
                    ImGui.Dummy(new Vector2(0f, S(4f)));
                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextSecondary);
                    ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + RsElements.AvailContentWidth());
                    ImGui.TextUnformatted(currentGroup.description);
                    ImGui.PopTextWrapPos();
                    ImGui.PopStyleColor();
                }
            }
            finally
            {
                RsElements.EndPanel();
            }
            ImGui.Dummy(new Vector2(0f, S(8f)));
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
            Groups_DS.FetchGroupMembers(Plugin.character, group.groupID);
            Groups_DS.FetchGroupRanks(Plugin.character, group.groupID);
            GroupChannels_DS.FetchGroupCategories(Plugin.character, group.groupID);
            GroupChannels_DS.FetchForumStructure(Plugin.character, group.groupID);
            Groups_DS.FetchGroupRosterFields(Plugin.character, group.groupID);
            GroupRoles_DS.FetchGroupRules(Plugin.character, group.groupID); // Fetch rules to check agreement status
            GroupRoles_DS.FetchSelfAssignRoles(Plugin.character, group.groupID); // Fetch self-assign roles for channel permissions
            GroupRoles_DS.FetchRoleSections(Plugin.character, group.groupID); // Fetch role sections
            Plugin.PluginLog.Info($"[Groups.LoadGroup] Fetch requests sent for group {group.groupID}");
        }

        // Member lookups
        // Chat rows, tooltips and permission checks used to scan the member list with FirstOrDefault once or twice per message per frame. Index it by userID once and rebuild only when the list changes.
        private static List<GroupMember> memberIndexSource;
        private static int memberIndexCount = -1;
        private static readonly Dictionary<int, GroupMember> memberIndexByUser = new Dictionary<int, GroupMember>();
        private static List<GroupMember> sortedMembers = new List<GroupMember>();

        private static Dictionary<int, GroupMember> GetMemberIndex()
        {
            var members = currentGroup?.members;
            if (!ReferenceEquals(members, memberIndexSource) || (members?.Count ?? -1) != memberIndexCount)
            {
                memberIndexSource = members;
                memberIndexCount = members?.Count ?? -1;
                memberIndexByUser.Clear();
                sortedMembers = new List<GroupMember>();
                if (members != null)
                {
                    foreach (var m in members)
                    {
                        if (m == null) continue;
                        memberIndexByUser.TryAdd(m.userID, m);
                        sortedMembers.Add(m);
                    }
                    // Owners first, then by name - sorted once per change, not per frame.
                    sortedMembers = sortedMembers
                        .OrderByDescending(m => m.owner)
                        .ThenBy(m => m.name ?? "")
                        .ToList();
                }
            }
            return memberIndexByUser;
        }

        private static GroupMember GetMemberByUser(int userID)
            => GetMemberIndex().TryGetValue(userID, out var m) ? m : null;

        private static GroupMember GetMyMember()
            => GetMemberByUser(Plugin.plugin?.Configuration?.account?.userID ?? Accounts_DS.userID);

        private static readonly List<RsElements.NavItem> groupTabs = new List<RsElements.NavItem>
        {
            new RsElements.NavItem(FontAwesomeIcon.Hashtag, "Channels"),
            new RsElements.NavItem(FontAwesomeIcon.Users, "Members"),
        };

        private static void DrawGroupChannelsAndChat()
        {
            if (currentGroup == null)
                return;

            // Tab strip for switching between Channels and Members
            int tab = mainGroupTab;
            RsElements.NavigationMenu("grp_main_tabs", ref tab, groupTabs);
            mainGroupTab = tab;
            ImGui.Dummy(new Vector2(0f, S(8f)));

            if (mainGroupTab == 0)
            {
                if (currentGroup.categories != null)
                {
                    var avail = ImGui.GetContentRegionAvail();
                    float listW = S(210f);

                    // Left: channel list card
                    ImGui.PushStyleColor(ImGuiCol.ChildBg, RsTheme.PanelSurface);
                    ImGui.PushStyleColor(ImGuiCol.Border, RsTheme.Border);
                    ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, S(8f));
                    ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(S(8f), S(10f)));
                    bool listOpen = ImGui.BeginChild("##grp_channels_card", new Vector2(listW, avail.Y), true);
                    ImGui.PopStyleVar(2);
                    ImGui.PopStyleColor(2);
                    try
                    {
                        if (listOpen)
                            DrawChannelList();
                    }
                    finally
                    {
                        ImGui.EndChild();
                    }

                    ImGui.SameLine(0f, S(10f));

                    // Right: chat card
                    ImGui.PushStyleColor(ImGuiCol.ChildBg, RsTheme.PanelSurface);
                    ImGui.PushStyleColor(ImGuiCol.Border, RsTheme.Border);
                    ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, S(8f));
                    ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(S(12f), S(10f)));
                    bool chatOpen = ImGui.BeginChild("##grp_chat_card", new Vector2(0f, avail.Y), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
                    ImGui.PopStyleVar(2);
                    ImGui.PopStyleColor(2);
                    try
                    {
                        if (chatOpen)
                            DrawChatArea();
                    }
                    finally
                    {
                        ImGui.EndChild();
                    }
                }
                else
                {
                    GroupUi.Muted("No channels available.");
                }
            }
            else
            {
                DrawMembersList();
            }

            // Draw member management popups outside of tabs so they work from either tab
            DrawMemberManagementPopups();
        }

        private static void DrawMembersList()
        {
            if (currentGroup == null || currentGroup.members == null)
            {
                GroupUi.Muted("No members data available.");
                return;
            }

            // Get current user info for permission checks
            GetMemberIndex();
            var currentUserMember = GetMyMember();
            bool isCurrentUserOwner = currentUserMember?.owner == true;
            int myUserID = Plugin.plugin?.Configuration?.account?.userID ?? Accounts_DS.userID;

            GroupUi.SectionLabel($"Members — {currentGroup.members.Count}");

            float availableHeight = ImGui.GetContentRegionAvail().Y;
            bool open = ImGui.BeginChild("MembersListScroll", new Vector2(0, availableHeight), false);
            try
            {
                if (!open) return;

                float cardHeight = S(60f);
                float gap = S(6f);
                float avatarSize = S(42f);

                foreach (var member in sortedMembers)
                {
                    if (member == null) continue;

                    float cardWidth = ImGui.GetContentRegionAvail().X - S(2f);
                    var cursorPos = ImGui.GetCursorScreenPos();

                    // Off-screen rows only reserve their space.
                    if (!ImGui.IsRectVisible(new Vector2(cardWidth, cardHeight)))
                    {
                        ImGui.Dummy(new Vector2(cardWidth, cardHeight + gap));
                        continue;
                    }

                    ImGui.PushID($"member_{member.id}");
                    try
                    {
                        // Hit area first so the card can react to hover.
                        ImGui.InvisibleButton($"memberCard_{member.id}", new Vector2(cardWidth, cardHeight));
                        bool hovered = ImGui.IsItemHovered();
                        var cardMax = cursorPos + new Vector2(cardWidth, cardHeight);
                        GroupUi.CardBackground(cursorPos, cardMax, hovered);

                        if (hovered)
                        {
                            ImGui.BeginTooltip();
                            ImGui.TextUnformatted(member.name ?? "Unknown");
                            if (member.owner)
                            {
                                ImGui.SameLine();
                                ImGui.TextColored(RsTheme.AccentWarning, "(Owner)");
                            }
                            if (member.rank != null && !string.IsNullOrEmpty(member.rank.name))
                            {
                                ImGui.TextColored(RsTheme.TextSecondary, $"Rank: {member.rank.name}");
                            }
                            if (member.selfAssignedRoles != null && member.selfAssignedRoles.Count > 0)
                            {
                                GroupUi.Divider(4f);
                                GroupUi.Muted("Roles:");
                                foreach (var role in member.selfAssignedRoles)
                                {
                                    ImGui.TextColored(ParseHexColor(role.color), $"  • {role.name}");
                                }
                            }
                            ImGui.EndTooltip();
                        }

                        // Right-click context menu for member management
                        bool isOwnMember = member.userID == myUserID;
                        if (!isOwnMember && !member.owner && ImGui.BeginPopupContextItem($"memberContext_{member.id}"))
                        {
                            GroupUi.Muted(member.name ?? "Member");
                            GroupUi.Divider(4f);

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
                                GroupUi.Muted("No permissions");
                            }

                            ImGui.EndPopup();
                        }

                        // Card contents: avatar, name + owner chip, rank.
                        float pad = (cardHeight - avatarSize) * 0.5f;
                        ImGui.SetCursorScreenPos(new Vector2(cursorPos.X + S(10f), cursorPos.Y + pad));
                        GroupUi.Avatar(member.avatar, member.name, avatarSize, member.owner ? RsTheme.AccentWarning : (Vector4?)null);

                        float lineH = ImGui.GetTextLineHeight();
                        float textX = cursorPos.X + S(10f) + avatarSize + S(12f);
                        float textY = cursorPos.Y + (cardHeight - (lineH * 2f + S(4f))) * 0.5f;
                        ImGui.SetCursorScreenPos(new Vector2(textX, textY));
                        ImGui.TextUnformatted(member.name ?? "Unknown");
                        if (member.owner)
                        {
                            ImGui.SameLine(0f, S(8f));
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - S(2f));
                            GroupUi.Chip("Owner", RsTheme.AccentWarning);
                        }
                        ImGui.SetCursorScreenPos(new Vector2(textX, textY + lineH + S(4f)));
                        if (member.rank != null && !string.IsNullOrEmpty(member.rank.name))
                            GroupUi.Secondary(member.rank.name);
                        else
                            GroupUi.Muted("No rank");

                        // Park the cursor below the card.
                        ImGui.SetCursorScreenPos(new Vector2(cursorPos.X, cardMax.Y + gap));
                        ImGui.Dummy(Vector2.Zero);
                    }
                    finally
                    {
                        ImGui.PopID();
                    }
                }
            }
            finally
            {
                ImGui.EndChild();
            }
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
            try
            {
                DrawChannelListInner();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"[Groups] DrawChannelList threw: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                ImGui.EndGroup();
            }
        }

        private static void DrawChannelListInner()
        {
            if (currentGroup == null) return;

            GroupUi.SectionLabel("Channels");

            var currentMember = GetMyMember();
            bool canEditCategory = currentMember?.owner == true || currentMember?.rank?.permissions?.canEditCategory == true;
            bool canDeleteCategory = currentMember?.owner == true || currentMember?.rank?.permissions?.canDeleteCategory == true;
            bool canEditForum = currentMember?.owner == true || currentMember?.rank?.permissions?.canEditForum == true;
            bool canDeleteForum = currentMember?.owner == true || currentMember?.rank?.permissions?.canDeleteForum == true;
            bool canLockForum = currentMember?.owner == true || currentMember?.rank?.permissions?.canLockForum == true;

            // Create scrollable child region for channel list
            using var child = ImRaii.Child("ChannelList", new Vector2(-1, -1), false);
            if (!child)
            {
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
                                GroupChannels_DS.RenameCategory(character, currentGroup.groupID, category.id, renameBuffer);
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

                    // Muted upper-case category heading; "###cat" keeps the tree id stable regardless of the displayed text.
                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                    ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0f, 0f, 0f, 0f));
                    ImGui.PushStyleColor(ImGuiCol.HeaderHovered, GroupUi.Fade(RsTheme.AccentPrimary, 0.12f));
                    try
                    {
                        nodeOpen = ImGui.TreeNodeEx($"{(category.name ?? string.Empty).ToUpperInvariant()}###cat", flags);
                    }
                    finally
                    {
                        ImGui.PopStyleColor(3);
                    }

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
                        GroupUi.Divider();
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
                                GroupChannels_DS.MoveChannel(character, currentGroup.groupID, draggedChannel.id, category.id);

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
                                GroupChannels_DS.ReorderCategory(character, currentGroup.groupID, draggedCategory.id, catIdx);

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
                    bool rulesExist = GroupRoles_DR.groupRulesVersion > 0 && !string.IsNullOrEmpty(GroupRoles_DR.groupRulesContent);
                    bool mustAgreeToRules = rulesExist && !GroupRoles_DR.hasAgreedToRules && !isOwner;

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
                                        GroupChannels_DS.RenameChannel(character, currentGroup.groupID, channel.id, renameBuffer);
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
                            // Row: selectable hit area, then icon / name / lock / unread painted on top so the game font never has to render emoji.
                            float rowH = ImGui.GetTextLineHeight() + S(10f);
                            var rowMin = ImGui.GetCursorScreenPos();
                            bool rowClicked = ImGui.Selectable($"##ch_{chIdx}", isSelected, ImGuiSelectableFlags.None, new Vector2(0f, rowH));
                            {
                                var rowDl = ImGui.GetWindowDrawList();
                                var rowMax = ImGui.GetItemRectMax();
                                bool rowHovered = ImGui.IsItemHovered();
                                bool emphasised = isSelected || rowHovered || (channel.unreadCount > 0 && !isSelected);
                                uint nameCol = emphasised ? RsTheme.U.TextPrimary : RsTheme.U.TextSecondary;
                                var icon = GroupUi.ChannelIcon(channel.channelType);
                                var iconSz = GroupUi.IconSize(icon);
                                float midY = rowMin.Y + rowH * 0.5f;
                                GroupUi.DrawIconAt(rowDl, icon, new Vector2(rowMin.X + S(6f), midY - iconSz.Y * 0.5f), isSelected ? RsTheme.U.AccentPrimary : RsTheme.U.TextMuted);
                                float textX = rowMin.X + S(6f) + Math.Max(iconSz.X, S(14f)) + S(8f);
                                float rightX = rowMax.X - S(6f);

                                // Unread pill / lock glyph on the right.
                                if (channel.unreadCount > 0 && !isSelected)
                                {
                                    string cnt = channel.unreadCount > 99 ? "99+" : channel.unreadCount.ToString();
                                    var cs = ImGui.CalcTextSize(cnt);
                                    float bh = cs.Y + S(2f);
                                    float bw = Math.Max(bh, cs.X + S(10f));
                                    var bmin = new Vector2(rightX - bw, midY - bh * 0.5f);
                                    rowDl.AddRectFilled(bmin, bmin + new Vector2(bw, bh), RsTheme.U.AccentDanger, bh * 0.5f);
                                    rowDl.AddText(new Vector2(bmin.X + (bw - cs.X) * 0.5f, bmin.Y + (bh - cs.Y) * 0.5f), 0xFFFFFFFFu, cnt);
                                    rightX = bmin.X - S(6f);
                                }
                                if (channel.isLocked)
                                {
                                    var lockSz = GroupUi.IconSize(FontAwesomeIcon.Lock);
                                    GroupUi.DrawIconAt(rowDl, FontAwesomeIcon.Lock, new Vector2(rightX - lockSz.X, midY - lockSz.Y * 0.5f), RsTheme.U.AccentDanger);
                                    rightX -= lockSz.X + S(6f);
                                }
                                if (channel.isNsfw)
                                {
                                    var ns = ImGui.CalcTextSize("18+");
                                    rowDl.AddText(new Vector2(rightX - ns.X, midY - ns.Y * 0.5f), RsTheme.U.AccentWarning, "18+");
                                    rightX -= ns.X + S(6f);
                                }

                                rowDl.PushClipRect(new Vector2(textX, rowMin.Y), new Vector2(Math.Max(textX, rightX), rowMin.Y + rowH), true);
                                rowDl.AddText(new Vector2(textX, midY - ImGui.GetTextLineHeight() * 0.5f), nameCol, channel.name ?? string.Empty);
                                rowDl.PopClipRect();
                                if (rowHovered && channel.isLocked)
                                    ImGui.SetTooltip("Locked — messages cannot be sent");
                            }

                            if (rowClicked)
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
                                    // Avatar textures are kept for the whole group session (cleared in LoadGroup) so switching channels doesn't re-download every avatar.
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

                            // (Locked / unread indicators are painted inside the row above.)

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
                                            GroupChannels_DS.ReorderChannel(character, currentGroup.groupID, draggedChannel.id, chIdx);

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
                                            var role = GroupRoles_DR.selfAssignRoles?.FirstOrDefault(r => r.id == rolep.roleID);
                                            if (role != null)
                                            {
                                                editChannelPermissionSelectedRoles.Add(new ChannelSelfRolePermission { role = role, canView = rolep.canView, canPost = rolep.canPost });
                                                Plugin.PluginLog.Info($"[EditChannel] Added role '{role.name}' to selected");
                                            }
                                            else
                                            {
                                                Plugin.PluginLog.Warning($"[EditChannel] Could not find role with id={rolep.roleID} in selfAssignRoles (count={GroupRoles_DR.selfAssignRoles?.Count ?? 0})");
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
                                            GroupChat_DS.LockChannel(character, currentGroup.groupID, channel.id, newLockStatus);
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

            // Right-click context menu for empty space only Note: Category and channel context menus are handled inline via BeginPopupContextItem
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
                GroupUi.Divider();
                ImGui.Spacing();

                float buttonWidth = 80f;
                float spacing = 10f;
                float totalWidth = (buttonWidth * 2) + spacing;
                GroupUi.CenterRow(GroupUi.S(totalWidth));

                if (GroupUi.Danger("Delete", new Vector2(buttonWidth, 0)))
                {
                    if (channelToDelete != null)
                    {
                        var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                            x.characterName == Plugin.plugin.playername &&
                            x.characterWorld == Plugin.plugin.playerworld);
                        if (character != null)
                        {
                            GroupChannels_DS.DeleteChannel(character, currentGroup.groupID, channelToDelete.id);
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

                if (GroupUi.Ghost("Cancel", new Vector2(buttonWidth, 0)))
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
                GroupUi.Divider();
                ImGui.Spacing();

                float buttonWidthCat = 80f;
                float spacingCat = 10f;
                float totalWidthCat = (buttonWidthCat * 2) + spacingCat;
                GroupUi.CenterRow(GroupUi.S(totalWidthCat));

                if (GroupUi.Danger("Delete##Category", new Vector2(buttonWidthCat, 0)))
                {
                    if (categoryToDelete != null)
                    {
                        var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                            x.characterName == Plugin.plugin.playername &&
                            x.characterWorld == Plugin.plugin.playerworld);
                        if (character != null)
                        {
                            GroupChannels_DS.DeleteCategory(character, currentGroup.groupID, categoryToDelete.id);
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

                if (GroupUi.Ghost("Cancel##Category", new Vector2(buttonWidthCat, 0)))
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
                    ImGui.TextColored(RsTheme.AccentDanger, "Error: No channel selected");
                    if (GroupUi.Ghost("Close"))
                    {
                        ResetEditChannelState();
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }
                else
                {
                    ImGui.Text("Edit channel");
                    GroupUi.Divider();
                    ImGui.Spacing();

                    // Basic channel info
                    GroupUi.SectionLabel("Channel Name");
                    ImGui.SetNextItemWidth(-1);
                    GroupUi.Input("##EditChannelName", ref editChannelName, 100);

                    ImGui.Spacing();

                    GroupUi.SectionLabel("Description (optional)");
                    ImGui.SetNextItemWidth(-1);
                    GroupUi.Input("##EditChannelDescription", ref editChannelDescription, 500);

                    ImGui.Spacing();

                    GroupUi.SectionLabel("Channel Type");
                    RsElements.Radio("Text Channel##Edit", ref editChannelType, 0);
                    ImGui.SameLine();
                    RsElements.Radio("Announcement##Edit", ref editChannelType, 1);
                    ImGui.SameLine();
                    // Check if rules channel already exists (allow if this is already the rules channel)
                    bool hasRulesChannel = currentGroup?.categories?.Any(c => c.channels?.Any(ch => ch.channelType == 2 && ch.id != channelBeingEdited.id) ?? false) ?? false;
                    if (hasRulesChannel)
                    {
                        ImGui.BeginDisabled();
                    }
                    RsElements.Radio("Rules##Edit", ref editChannelType, 2);
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
                    RsElements.Radio("Role Selection##Edit", ref editChannelType, 3);
                    if (hasRoleSelectionChannel)
                    {
                        ImGui.EndDisabled();
                        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        {
                            ImGui.SetTooltip("Only one role selection channel allowed per group");
                        }
                    }
                    ImGui.SameLine();
                    RsElements.Radio("Form##Edit", ref editChannelType, 4);

                    if (editChannelType == 1)
                    {
                        ImGui.TextColored(RsTheme.TextMuted, "Only members with announcement permission can post.");
                    }
                    else if (editChannelType == 2)
                    {
                        ImGui.TextColored(RsTheme.AccentWarning, "Only the group owner can post rules. Members must agree before accessing other channels.");
                    }
                    else if (editChannelType == 3)
                    {
                        ImGui.TextColored(RsTheme.AccentPrimary, "Members can select self-assign roles that grant channel access.");
                    }
                    else if (editChannelType == 4)
                    {
                        ImGui.TextColored(RsTheme.AccentPrimary, "Create custom forms that members can fill out and submit. View submissions in a dedicated tab.");
                    }

                    ImGui.Spacing();

                    // NSFW Channel option
                    RsElements.Checkbox("NSFW Channel##Edit", ref editChannelIsNsfw);
                    if (editChannelIsNsfw)
                    {
                        ImGui.TextColored(RsTheme.AccentDanger, "Users must agree to view adult content before entering.");
                    }

                    ImGui.Spacing();
                    GroupUi.Divider();
                    ImGui.Spacing();

                    // Channel Permissions Section
                    ImGui.TextColored(RsTheme.AccentWarning, "Channel Permissions");
                    ImGui.Spacing();

                    // Default permissions toggle
                    RsElements.Checkbox("Everyone can view##Edit", ref editEveryoneCanView);
                    RsElements.Checkbox("Everyone can post##Edit", ref editEveryoneCanPost);

                    ImGui.Spacing();
                    ImGui.TextColored(RsTheme.TextMuted, "Or restrict access to specific members/ranks:");
                    ImGui.Spacing();

                    // Search box for adding members/ranks
                    GroupUi.SectionLabel("Search members or ranks");
                    ImGui.SetNextItemWidth(-1);
                    if (GroupUi.Input("##EditPermissionSearch", ref editChannelPermissionSearchQuery, 100))
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
                                if (GroupRoles_DR.selfAssignRoles != null)
                                {
                                    foreach (var role in GroupRoles_DR.selfAssignRoles.Where(r => r.name.ToLower().Contains(searchLower)))
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
                        ImGui.TextColored(RsTheme.TextMuted, "(Toggle View/Post for each)");

                        using (var selectedList = ImRaii.Child("EditSelectedPermissions", new Vector2(-1, 150), true))
                        {
                            if (selectedList)
                            {
                                // Column headers
                                ImGui.TextColored(RsTheme.TextMuted, "Name");
                                ImGui.SameLine(200);
                                ImGui.TextColored(RsTheme.TextMuted, "View");
                                ImGui.SameLine(250);
                                ImGui.TextColored(RsTheme.TextMuted, "Post");
                                ImGui.SameLine(300);
                                ImGui.TextColored(RsTheme.TextMuted, "");
                                GroupUi.Divider();

                                // Show selected ranks with permission toggles
                                for (int i = editChannelPermissionSelectedRanks.Count - 1; i >= 0; i--)
                                {
                                    var rankPerm = editChannelPermissionSelectedRanks[i];
                                    ImGui.PushID($"editRankPerm{rankPerm.rank.id}");

                                    ImGui.TextColored(RsTheme.AccentPrimary, $"[Rank] {rankPerm.rank.name}");
                                    ImGui.SameLine(200);
                                    RsElements.Checkbox("##view", ref rankPerm.canView);
                                    ImGui.SameLine(250);
                                    RsElements.Checkbox("##post", ref rankPerm.canPost);
                                    ImGui.SameLine(300);
                                    if (RsElements.IconButton(FontAwesomeIcon.Times, "perm_remove", RsElements.ButtonVariant.Ghost, 22f))
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

                                    ImGui.TextColored(RsTheme.AccentSuccess, $"[Member] {memberPerm.member.name}");
                                    ImGui.SameLine(200);
                                    RsElements.Checkbox("##view", ref memberPerm.canView);
                                    ImGui.SameLine(250);
                                    RsElements.Checkbox("##post", ref memberPerm.canPost);
                                    ImGui.SameLine(300);
                                    if (RsElements.IconButton(FontAwesomeIcon.Times, "perm_remove", RsElements.ButtonVariant.Ghost, 22f))
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
                                    RsElements.Checkbox("##view", ref rolePerm.canView);
                                    ImGui.SameLine(250);
                                    RsElements.Checkbox("##post", ref rolePerm.canPost);
                                    ImGui.SameLine(300);
                                    if (RsElements.IconButton(FontAwesomeIcon.Times, "perm_remove", RsElements.ButtonVariant.Ghost, 22f))
                                    {
                                        editChannelPermissionSelectedRoles.RemoveAt(i);
                                    }

                                    ImGui.PopID();
                                }
                            }
                        }
                    }

                    ImGui.Spacing();
                    GroupUi.Divider();
                    ImGui.Spacing();

                    // Action buttons
                    float buttonWidth2 = 80f;
                    float spacing2 = 10f;
                    float totalWidth2 = (buttonWidth2 * 2) + spacing2;
                    GroupUi.CenterRow(GroupUi.S(totalWidth2));

                    bool canSave = !string.IsNullOrWhiteSpace(editChannelName);
                    if (!canSave) ImGui.BeginDisabled();

                    if (GroupUi.Button("Save##EditCh", new Vector2(buttonWidth2, 0)))
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
                            GroupChannels_DS.UpdateChannelWithPermissions(
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

                            GroupChannels_DS.FetchGroupCategories(Plugin.character, currentGroup.groupID);
                        }
                        ResetEditChannelState();
                        ImGui.CloseCurrentPopup();
                    }

                    if (!canSave) ImGui.EndDisabled();

                    ImGui.SameLine();

                    if (GroupUi.Ghost("Cancel##EditCh", new Vector2(buttonWidth2, 0)))
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
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentDanger);
                ImGui.TextUnformatted("WARNING: NSFW Content");
                ImGui.PopStyleColor();

                GroupUi.Divider();
                ImGui.Spacing();

                ImGui.TextWrapped("This channel contains sensitive and adult themes including but not limited to nudity and other mature content.");
                ImGui.Spacing();
                ImGui.TextWrapped("By clicking 'I Agree', you confirm that you are of legal age to view such content in your jurisdiction and consent to viewing it.");
                ImGui.Spacing();

                float buttonWidth = 120f;
                float spacing = 10f;
                float totalWidth = (buttonWidth * 2) + spacing;
                GroupUi.CenterRow(GroupUi.S(totalWidth));

                if (GroupUi.Danger("I Agree", new Vector2(buttonWidth, 0)))
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

                ImGui.SameLine();

                if (GroupUi.Ghost("Cancel", new Vector2(buttonWidth, 0)))
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
                GroupUi.Divider();
                ImGui.Spacing();

                GroupUi.SectionLabel("Category Name");
                ImGui.SetNextItemWidth(250);
                GroupUi.Input("##CategoryName", ref newCategoryName, 100);

                ImGui.Spacing();
                GroupUi.Divider();
                ImGui.Spacing();

                float buttonWidthCat = 80f;
                float spacingCat = 10f;
                float totalWidthCat = (buttonWidthCat * 2) + spacingCat;
                GroupUi.CenterRow(GroupUi.S(totalWidthCat));

                bool canCreateCat = !string.IsNullOrWhiteSpace(newCategoryName);
                if (!canCreateCat) ImGui.BeginDisabled();

                if (GroupUi.Button("Create##CreateCat", new Vector2(buttonWidthCat, 0)))
                {
                    var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                        x.characterName == Plugin.plugin.playername &&
                        x.characterWorld == Plugin.plugin.playerworld);
                    if (character != null)
                    {
                        GroupChannels_DS.CreateCategory(character, currentGroup.groupID, newCategoryName);
                        GroupChannels_DS.FetchGroupCategories(Plugin.character, currentGroup.groupID);
                    }
                    newCategoryName = "";
                    showCreateCategoryPopup = false;
                    ImGui.CloseCurrentPopup();
                }

                if (!canCreateCat) ImGui.EndDisabled();

                ImGui.SameLine();

                if (GroupUi.Ghost("Cancel##CancelCat", new Vector2(buttonWidthCat, 0)))
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
                GroupUi.Divider();
                ImGui.Spacing();

                // Display error message if there is one
                if (!string.IsNullOrEmpty(Groups_DR.createChannelError))
                {
                    ImGui.TextColored(RsTheme.AccentDanger, Groups_DR.createChannelError);
                    ImGui.Spacing();
                }

                // Basic channel info
                GroupUi.SectionLabel("Channel Name");
                ImGui.SetNextItemWidth(-1);
                GroupUi.Input("##ChannelName", ref newChannelName, 100);

                ImGui.Spacing();

                GroupUi.SectionLabel("Description (optional)");
                ImGui.SetNextItemWidth(-1);
                GroupUi.Input("##ChannelDescription", ref newChannelDescription, 500);

                ImGui.Spacing();

                GroupUi.SectionLabel("Channel Type");
                RsElements.Radio("Text Channel", ref newChannelType, 0);
                ImGui.SameLine();
                RsElements.Radio("Announcement", ref newChannelType, 1);
                ImGui.SameLine();
                // Check if rules channel already exists
                bool hasRulesChannel = currentGroup?.categories?.Any(c => c.channels?.Any(ch => ch.channelType == 2) ?? false) ?? false;
                if (hasRulesChannel)
                {
                    ImGui.BeginDisabled();
                }
                RsElements.Radio("Rules", ref newChannelType, 2);
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
                RsElements.Radio("Role Selection", ref newChannelType, 3);
                if (hasRoleSelectionChannel)
                {
                    ImGui.EndDisabled();
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    {
                        ImGui.SetTooltip("Only one role selection channel allowed per group");
                    }
                }
                ImGui.SameLine();
                RsElements.Radio("Form", ref newChannelType, 4);

                if (newChannelType == 1)
                {
                    ImGui.TextColored(RsTheme.TextMuted, "Only members with announcement permission can post.");
                }
                else if (newChannelType == 2)
                {
                    ImGui.TextColored(RsTheme.AccentWarning, "Only the group owner can post rules. Members must agree before accessing other channels.");
                }
                else if (newChannelType == 3)
                {
                    ImGui.TextColored(RsTheme.AccentPrimary, "Members can select self-assign roles that grant channel access.");
                }
                else if (newChannelType == 4)
                {
                    ImGui.TextColored(RsTheme.AccentPrimary, "Create custom forms that members can fill out and submit. View submissions in a dedicated tab.");
                }

                ImGui.Spacing();

                // NSFW Channel option
                RsElements.Checkbox("NSFW Channel", ref newChannelIsNsfw);
                if (newChannelIsNsfw)
                {
                    ImGui.TextColored(RsTheme.AccentDanger, "Users must agree to view adult content before entering.");
                }

                ImGui.Spacing();
                GroupUi.Divider();
                ImGui.Spacing();

                // Channel Permissions Section
                ImGui.TextColored(RsTheme.AccentWarning, "Channel Permissions");
                ImGui.Spacing();

                // Default permissions toggle
                RsElements.Checkbox("Everyone can view", ref channelPermissionEveryoneCanView);
                RsElements.Checkbox("Everyone can post", ref channelPermissionEveryoneCanPost);

                ImGui.Spacing();
                ImGui.TextColored(RsTheme.TextMuted, "Or restrict access to specific members/ranks:");
                ImGui.Spacing();

                // Search box for adding members/ranks
                GroupUi.SectionLabel("Search members or ranks");
                ImGui.SetNextItemWidth(-1);
                if (GroupUi.Input("##PermissionSearch", ref channelPermissionSearchQuery, 100))
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
                            if (GroupRoles_DR.selfAssignRoles != null)
                            {
                                foreach (var role in GroupRoles_DR.selfAssignRoles.Where(r => r.name.ToLower().Contains(searchLower)))
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
                    ImGui.TextColored(RsTheme.TextMuted, "(Toggle View/Post for each)");

                    using (var selectedList = ImRaii.Child("SelectedPermissions", new Vector2(-1, 150), true))
                    {
                        if (selectedList)
                        {
                            // Column headers
                            ImGui.TextColored(RsTheme.TextMuted, "Name");
                            ImGui.SameLine(200);
                            ImGui.TextColored(RsTheme.TextMuted, "View");
                            ImGui.SameLine(250);
                            ImGui.TextColored(RsTheme.TextMuted, "Post");
                            ImGui.SameLine(300);
                            ImGui.TextColored(RsTheme.TextMuted, "");
                            GroupUi.Divider();

                            // Show selected ranks with permission toggles
                            for (int i = channelPermissionSelectedRanks.Count - 1; i >= 0; i--)
                            {
                                var rankPerm = channelPermissionSelectedRanks[i];
                                ImGui.PushID($"rankPerm{rankPerm.rank.id}");

                                ImGui.TextColored(RsTheme.AccentPrimary, $"[Rank] {rankPerm.rank.name}");
                                ImGui.SameLine(200);
                                RsElements.Checkbox("##view", ref rankPerm.canView);
                                ImGui.SameLine(250);
                                RsElements.Checkbox("##post", ref rankPerm.canPost);
                                ImGui.SameLine(300);
                                if (RsElements.IconButton(FontAwesomeIcon.Times, "perm_remove", RsElements.ButtonVariant.Ghost, 22f))
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

                                ImGui.TextColored(RsTheme.AccentSuccess, $"[Member] {memberPerm.member.name}");
                                ImGui.SameLine(200);
                                RsElements.Checkbox("##view", ref memberPerm.canView);
                                ImGui.SameLine(250);
                                RsElements.Checkbox("##post", ref memberPerm.canPost);
                                ImGui.SameLine(300);
                                if (RsElements.IconButton(FontAwesomeIcon.Times, "perm_remove", RsElements.ButtonVariant.Ghost, 22f))
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
                                RsElements.Checkbox("##view", ref rolePerm.canView);
                                ImGui.SameLine(250);
                                RsElements.Checkbox("##post", ref rolePerm.canPost);
                                ImGui.SameLine(300);
                                if (RsElements.IconButton(FontAwesomeIcon.Times, "perm_remove", RsElements.ButtonVariant.Ghost, 22f))
                                {
                                    channelPermissionSelectedRoles.RemoveAt(i);
                                }

                                ImGui.PopID();
                            }
                        }
                    }
                }

                ImGui.Spacing();
                GroupUi.Divider();
                ImGui.Spacing();

                // Action buttons
                float buttonWidth2 = 80f;
                float spacing2 = 10f;
                float totalWidth2 = (buttonWidth2 * 2) + spacing2;
                GroupUi.CenterRow(GroupUi.S(totalWidth2));

                bool canCreate = !string.IsNullOrWhiteSpace(newChannelName);
                if (!canCreate) ImGui.BeginDisabled();

                if (GroupUi.Button("Create##CreateCh", new Vector2(buttonWidth2, 0)))
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
                        GroupChannels_DS.CreateChannelWithPermissions(
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

                        GroupChannels_DS.FetchGroupCategories(Plugin.character, currentGroup.groupID);
                    }
                    ResetCreateChannelState();
                    ImGui.CloseCurrentPopup();
                }

                if (!canCreate) ImGui.EndDisabled();

                ImGui.SameLine();

                if (GroupUi.Ghost("Cancel##CancelCh", new Vector2(buttonWidth2, 0)))
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
            Groups_DR.createChannelError = string.Empty;
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
                GroupUi.Divider();
                ImGui.Spacing();

                float kickButtonWidth = 80f;
                float kickSpacing = 10f;
                float kickTotalWidth = (kickButtonWidth * 2) + kickSpacing;
                GroupUi.CenterRow(GroupUi.S(kickTotalWidth));

                if (GroupUi.Danger("Kick", new Vector2(kickButtonWidth, 0)))
                {
                    if (memberToManage != null)
                    {
                        var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                            x.characterName == Plugin.plugin.playername &&
                            x.characterWorld == Plugin.plugin.playerworld);
                        if (character != null)
                        {
                            Groups_DS.KickGroupMember(character, memberToManage.id, currentGroup.groupID);
                            currentGroup.members?.RemoveAll(m => m.id == memberToManage.id);
                        }
                    }
                    memberToManage = null;
                    showKickMemberConfirmation = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();

                if (GroupUi.Ghost("Cancel##Kick", new Vector2(kickButtonWidth, 0)))
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
                ImGui.TextColored(RsTheme.AccentDanger, "This member will be permanently banned from the group.");
                GroupUi.Divider();
                ImGui.Spacing();

                float banButtonWidth = 80f;
                float banSpacing = 10f;
                float banTotalWidth = (banButtonWidth * 2) + banSpacing;
                GroupUi.CenterRow(GroupUi.S(banTotalWidth));

                if (GroupUi.Danger("Ban", new Vector2(banButtonWidth, 0)))
                {
                    if (memberToManage != null)
                    {
                        var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                            x.characterName == Plugin.plugin.playername &&
                            x.characterWorld == Plugin.plugin.playerworld);
                        if (character != null)
                        {
                            Groups_DS.BanGroupMember(character, memberToManage.id, memberToManage.userID,
                                memberToManage.profileID, memberToManage.lodestoneURL ?? "", currentGroup.groupID);
                            currentGroup.members?.RemoveAll(m => m.id == memberToManage.id);
                            // Refresh bans list after a short delay to allow server to process
                            var groupID = currentGroup.groupID;
                            Task.Run(async () =>
                            {
                                await Task.Delay(500);
                                Groups_DS.FetchGroupBans(character, groupID);
                            });
                        }
                    }
                    memberToManage = null;
                    showBanMemberConfirmation = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();

                if (GroupUi.Ghost("Cancel##Ban", new Vector2(banButtonWidth, 0)))
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
                GroupUi.Divider();
                ImGui.Spacing();

                // Get current user's hierarchy to filter available ranks
                var currentUser = currentGroup.members?.FirstOrDefault(m => m.userID == Accounts_DS.userID);
                bool isOwner = currentUser?.owner == true;
                int currentUserHierarchy = isOwner ? int.MaxValue : (currentUser?.rank?.hierarchy ?? 0);

                // Show available ranks (only those below current user's rank)
                if (currentGroup.ranks != null && currentGroup.ranks.Count > 0)
                {
                    GroupUi.SectionLabel("Select new rank");
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
                                        Groups_DS.AssignMemberRank(character, memberToManage.id, rank.id, currentGroup.groupID);
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
                    GroupUi.Divider();
                    if (ImGui.Selectable("Remove Rank"))
                    {
                        if (memberToManage != null)
                        {
                            var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                                x.characterName == Plugin.plugin.playername &&
                                x.characterWorld == Plugin.plugin.playerworld);
                            if (character != null)
                            {
                                Groups_DS.RemoveMemberRank(character, memberToManage.id, currentGroup.groupID);
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
                GroupUi.Divider();
                ImGui.Spacing();

                float promoteButtonWidth = 80f;
                GroupUi.CenterRow(GroupUi.S(promoteButtonWidth));

                if (GroupUi.Ghost("Cancel##Promote", new Vector2(promoteButtonWidth, 0)))
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
                ImGui.TextColored(RsTheme.AccentWarning, "You will need to be re-invited to rejoin this group.");
                GroupUi.Divider();
                ImGui.Spacing();

                float leaveButtonWidth = 80f;
                float leaveSpacing = 10f;
                float leaveTotalWidth = (leaveButtonWidth * 2) + leaveSpacing;
                GroupUi.CenterRow(GroupUi.S(leaveTotalWidth));

                if (GroupUi.Danger("Leave", new Vector2(leaveButtonWidth, 0)))
                {
                    if (groupToLeave != null)
                    {
                        var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                            x.characterName == Plugin.plugin.playername &&
                            x.characterWorld == Plugin.plugin.playerworld);
                        if (character != null)
                        {
                            Groups_DS.LeaveGroup(character, groupToLeave.groupID);
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

                if (GroupUi.Ghost("Cancel##Leave", new Vector2(leaveButtonWidth, 0)))
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
                var avail = ImGui.GetContentRegionAvail();
                const string hint = "Select a channel to view messages";
                var ts = ImGui.CalcTextSize(hint);
                ImGui.SetCursorPos(new Vector2(
                    ImGui.GetCursorPosX() + Math.Max(0f, (avail.X - ts.X) * 0.5f),
                    ImGui.GetCursorPosY() + Math.Max(0f, (avail.Y - ts.Y) * 0.5f)));
                GroupUi.Muted(hint);
                return;
            }

            ImGui.BeginGroup();
            try
            {
                DrawChatHeader();

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
                    // Normal text/announcement channel - show messages and input. Leave room for: resize handle + chat input + spacing. Fixed-height composer anchored to the bottom; the message list takes whatever is left, so the composer can never grow the window.
                    float areaAvailY = ImGui.GetContentRegionAvail().Y;
                    float inputAreaHeight = S(8f) + S(chatInputHeight) + S(16f);
                    float maxArea = Math.Max(S(70f), areaAvailY * 0.6f);
                    if (inputAreaHeight > maxArea) inputAreaHeight = maxArea;
                    float listReserve = inputAreaHeight + ImGui.GetStyle().ItemSpacing.Y;
                    using (var messagesChild = ImRaii.Child("Messages", new Vector2(-1, -listReserve), false))
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
                                ImGui.TextColored(RsTheme.AccentDanger, "Error loading messages. Retrying...");
                            }
                        }
                    }

                    // Input area (fixed-size child: its contents can't resize/move the window)
                    if (ImGui.BeginChild("##grp_composer", new Vector2(0f, inputAreaHeight), false,
                            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
                    {
                        DrawMessageInput();
                    }
                    ImGui.EndChild();
                }
            }
            finally
            {
                ImGui.EndGroup();
            }
        }

        // Channel title row: icon + name + description, lock state and the pinned-messages button on the right.
        private static void DrawChatHeader()
        {
            var start = ImGui.GetCursorScreenPos();
            float width = ImGui.GetContentRegionAvail().X;
            float rowH = S(32f);
            var dl = ImGui.GetWindowDrawList();
            float midY = start.Y + rowH * 0.5f;

            var icon = GroupUi.ChannelIcon(selectedChannel.channelType);
            var iconSz = GroupUi.IconSize(icon);
            GroupUi.DrawIconAt(dl, icon, new Vector2(start.X, midY - iconSz.Y * 0.5f), RsTheme.U.TextMuted);
            float x = start.X + Math.Max(iconSz.X, S(14f)) + S(8f);

            // Pinned button on the far right.
            const string pinLabel = "Pinned##grp_pinned_btn";
            float pinW = RsElements.MeasureButtonWidth(pinLabel);
            float rightLimit = start.X + width - pinW - S(8f);

            float lineH = ImGui.GetTextLineHeight();
            string name = selectedChannel.name ?? string.Empty;
            var nameSz = ImGui.CalcTextSize(name);
            dl.AddText(new Vector2(x, midY - lineH * 0.5f), RsTheme.U.TextPrimary, name);
            x += nameSz.X + S(10f);

            if (selectedChannel.isLocked)
            {
                var lockSz = GroupUi.IconSize(FontAwesomeIcon.Lock);
                GroupUi.DrawIconAt(dl, FontAwesomeIcon.Lock, new Vector2(x, midY - lockSz.Y * 0.5f), RsTheme.U.AccentDanger);
                x += lockSz.X + S(4f);
                dl.AddText(new Vector2(x, midY - lineH * 0.5f), RsTheme.U.AccentDanger, "Locked");
                x += ImGui.CalcTextSize("Locked").X + S(10f);
            }

            if (!string.IsNullOrEmpty(selectedChannel.description) && x < rightLimit)
            {
                dl.AddLine(new Vector2(x, midY - lineH * 0.4f), new Vector2(x, midY + lineH * 0.4f), RsTheme.U.Border, RsTheme.BorderThickness);
                x += S(10f);
                dl.PushClipRect(new Vector2(x, start.Y), new Vector2(Math.Max(x, rightLimit), start.Y + rowH), true);
                dl.AddText(new Vector2(x, midY - lineH * 0.5f), RsTheme.U.TextMuted, selectedChannel.description.Replace('\n', ' '));
                dl.PopClipRect();
            }

            // Hover tooltip for the full description.
            ImGui.SetCursorScreenPos(start);
            ImGui.InvisibleButton("##grp_chat_title", new Vector2(Math.Max(1f, rightLimit - start.X), rowH));
            if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(selectedChannel.description))
                ImGui.SetTooltip(selectedChannel.description);

            ImGui.SetCursorScreenPos(new Vector2(start.X + width - pinW, start.Y + (rowH - (lineH + S(18f))) * 0.5f));
            if (RsElements.Button(pinLabel, RsElements.ButtonVariant.Ghost))
            {
                showPinnedMessagesPopup = true;
                GroupChat_DR.pinnedMessagesLoaded = false;

                // Fetch pinned messages from server
                var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                    x.characterName == Plugin.plugin.playername &&
                    x.characterWorld == Plugin.plugin.playerworld);
                if (character != null && currentGroup != null && selectedChannel != null)
                {
                    GroupChat_DS.FetchPinnedMessages(character, currentGroup.groupID, selectedChannel.id);
                }
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("View pinned messages in this channel");
            }

            ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + rowH + S(6f)));
            GroupUi.Divider(6f);
        }

        // Per-message layout cache: last measured height (for culling off-screen rows) and which row the mouse was over last frame (for the hover highlight, which must be drawn before the row content).
        private static readonly Dictionary<int, float> messageHeights = new Dictionary<int, float>();
        private static int hoveredMessageID = -1;

        // Fallback for missed broadcasts: while a channel is on screen its messages are re-requested every few seconds and merged in.
        private static long _lastMessagePollMs;
        private const int MessagePollMs = 4000;
        private static void PollMessages()
        {
            if (selectedChannel == null || currentGroup == null) return;
            var now = Environment.TickCount64;
            if (now - _lastMessagePollMs < MessagePollMs) return;
            _lastMessagePollMs = now;
            try
            {
                var character = Plugin.character;
                if (character != null) GroupChat_DS.FetchGroupChatMessages(character, currentGroup.groupID, selectedChannel.id, 50, 0);
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("PollMessages: " + ex.Message); }
        }

        private static void DrawMessages()
        {
            PollMessages();
            // Create a snapshot of messages to avoid holding the lock during rendering
            List<GroupChatMessage> messagesToRender;
            lock (messagesLock)
            {
                if (currentMessages == null || currentMessages.Count == 0)
                {
                    GroupUi.Muted("No messages yet. Be the first to say something!");
                    return;
                }

                // Create a shallow copy of the list to iterate safely
                messagesToRender = new List<GroupChatMessage>(currentMessages);
            }

            if (messageHeights.Count > 4000) messageHeights.Clear();

            // Permission context is the same for every row - work it out once per frame instead of scanning the member list per message.
            GetMemberIndex();
            var myMember = GetMemberByUser(Accounts_DS.userID);
            bool isCurrentUserOwner = myMember?.owner == true;
            bool canPin = myMember != null && (isCurrentUserOwner || (myMember.rank?.permissions?.canPinMessages == true));
            bool canDeleteOthers = myMember != null && (isCurrentUserOwner || myMember.rank?.permissions?.canDeleteOthersMessages == true);
            int myHierarchy = isCurrentUserOwner ? int.MaxValue : (myMember?.rank?.hierarchy ?? 0);
            var myPerms = myMember?.rank?.permissions;

            var dl = ImGui.GetWindowDrawList();
            float avatarSize = S(36f);
            int newHovered = -1;
            GroupChatMessage prev = null;

            foreach (var message in messagesToRender)
            {
                if (message == null || message.deleted)
                    continue;

                // Consecutive messages from the same sender within a few minutes collapse under one avatar/name header.
                bool continued = prev != null && prev.senderUserID == message.senderUserID
                                 && message.timestamp - prev.timestamp < 5 * 60 * 1000
                                 && message.timestamp >= prev.timestamp;
                prev = message;

                ImGui.PushID($"msg_{message.messageID}");
                try
                {
                    var rowStart = ImGui.GetCursorScreenPos();
                    float rowW = ImGui.GetContentRegionAvail().X;
                    float gapAfter = continued ? S(2f) : S(8f);

                    // Check if we need to scroll to this message
                    if (scrollToMessageID > 0 && message.messageID == scrollToMessageID)
                    {
                        ImGui.SetScrollHereY(0.5f); // Center the message vertically
                        scrollToMessageID = 0; // Reset after scrolling
                    }

                    // Cull rows we've measured before that are off-screen.
                    if (messageHeights.TryGetValue(message.messageID, out var knownH)
                        && !ImGui.IsRectVisible(new Vector2(Math.Max(1f, rowW), knownH)))
                    {
                        // Same advance as a drawn row: height + gap.
                        ImGui.Dummy(new Vector2(Math.Max(1f, rowW), knownH + Math.Max(0f, gapAfter - ImGui.GetStyle().ItemSpacing.Y)));
                        continue;
                    }

                    // Hover highlight (uses last frame's hover + height).
                    if (hoveredMessageID == message.messageID && knownH > 0f)
                    {
                        dl.AddRectFilled(new Vector2(rowStart.X - S(6f), rowStart.Y - S(3f)),
                                         new Vector2(rowStart.X + rowW + S(2f), rowStart.Y + knownH + S(3f)),
                                         ImGui.ColorConvertFloat4ToU32(GroupUi.Fade(RsTheme.BgSecondary, 0.9f)), S(6f));
                    }

                    // Check if this is the current user's message
                    bool isOwnMessage = message.senderUserID == Accounts_DS.userID;

                    if (continued)
                    {
                        ImGui.Dummy(new Vector2(avatarSize, 1f));
                        ImGui.SameLine(0f, S(12f));
                    }
                    else
                    {
                        // Get avatar from message (sent with each message from server) Capture reference locally to avoid race conditions
                        IDalamudTextureWrap avatarTexture = message.avatar;
                        GroupUi.Avatar(avatarTexture, message.senderName, avatarSize);
                        ImGui.SameLine(0f, S(12f));
                    }

                    ImGui.BeginGroup();
                    try
                    {
                        if (!continued)
                        {
                            // Sender name and timestamp
                            ImGui.PushStyleColor(ImGuiCol.Text, isOwnMessage ? RsTheme.AccentPrimary : RsTheme.TextPrimary);
                            ImGui.TextUnformatted(message.senderName ?? "Unknown");
                            ImGui.PopStyleColor();

                            // Tooltip for sender showing their rank and roles
                            if (ImGui.IsItemHovered())
                            {
                                var senderMember = GetMemberByUser(message.senderUserID);
                                if (senderMember != null)
                                {
                                    ImGui.BeginTooltip();
                                    ImGui.TextUnformatted(senderMember.name ?? "Unknown");

                                    if (senderMember.owner)
                                    {
                                        ImGui.TextColored(RsTheme.AccentWarning, "Owner");
                                    }
                                    else if (senderMember.rank != null && !string.IsNullOrEmpty(senderMember.rank.name))
                                    {
                                        ImGui.TextColored(RsTheme.TextSecondary, $"Rank: {senderMember.rank.name}");
                                    }

                                    if (senderMember.selfAssignedRoles != null && senderMember.selfAssignedRoles.Count > 0)
                                    {
                                        GroupUi.Divider(4f);
                                        GroupUi.Muted("Roles:");
                                        foreach (var role in senderMember.selfAssignedRoles)
                                        {
                                            ImGui.TextColored(ParseHexColor(role.color), $"  • {role.name}");
                                        }
                                    }

                                    ImGui.EndTooltip();
                                }
                            }

                            ImGui.SameLine(0f, S(8f));
                            var time = DateTimeOffset.FromUnixTimeMilliseconds(message.timestamp).ToLocalTime();
                            GroupUi.Muted($"{time:HH:mm}");
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip($"{time:f}");

                            if (message.isEdited && message.editedTimestamp.HasValue)
                            {
                                ImGui.SameLine(0f, S(6f));
                                GroupUi.Muted("(edited)");
                            }

                            // Show pin indicator if message is pinned
                            if (message.isPinned)
                            {
                                ImGui.SameLine(0f, S(8f));
                                GroupUi.Icon(FontAwesomeIcon.Thumbtack, RsTheme.AccentWarning);
                                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Pinned");
                            }
                        }
                        else if ((message.isEdited && message.editedTimestamp.HasValue) || message.isPinned)
                        {
                            if (message.isPinned)
                            {
                                GroupUi.Icon(FontAwesomeIcon.Thumbtack, RsTheme.AccentWarning);
                                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Pinned");
                                ImGui.SameLine(0f, S(6f));
                            }
                            if (message.isEdited && message.editedTimestamp.HasValue)
                                GroupUi.Muted("(edited)");
                        }

                        // Message content - render with special embed support
                        RenderMessageWithEmbeds(message.messageContent ?? string.Empty, message.messageID);
                    }
                    finally
                    {
                        ImGui.EndGroup();
                    }

                    // Make the entire message clickable for context menu
                    var messageMin = ImGui.GetItemRectMin();
                    var messageMax = ImGui.GetItemRectMax();
                    var messageSize = new Vector2(Math.Max(1f, messageMax.X - messageMin.X), Math.Max(1f, messageMax.Y - messageMin.Y));
                    messageHeights[message.messageID] = Math.Max(messageMax.Y, rowStart.Y + (continued ? 0f : avatarSize)) - rowStart.Y;

                    // Invisible button to capture right-clicks
                    ImGui.SetCursorScreenPos(messageMin);
                    ImGui.InvisibleButton($"msgArea_{message.messageID}", messageSize);
                    if (ImGui.IsMouseHoveringRect(rowStart, new Vector2(rowStart.X + rowW, rowStart.Y + messageHeights[message.messageID])) && ImGui.IsWindowHovered())
                        newHovered = message.messageID;

                    // Check permissions for moderation
                    bool canEdit = isOwnMessage;
                    bool canDelete = isOwnMessage;

                    // Debug log
                    if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                    {
                        Plugin.PluginLog.Info($"[GroupChat] Right-clicked message {message.messageID}, isOwnMessage={isOwnMessage}, senderUserID={message.senderUserID}, myUserID={Accounts_DS.userID}");
                    }

                    // Check permissions for member management
                    bool canKick = false;
                    bool canBan = false;
                    bool canPromote = false;
                    bool canDemote = false;
                    GroupMember targetMember = null;

                    if (!isOwnMessage && myMember != null)
                    {
                        targetMember = GetMemberByUser(message.senderUserID);

                        // Check delete others messages permission
                        canDelete = canDeleteOthers;

                        // Check member management permissions
                        if (targetMember != null && !targetMember.owner) // Can't manage owners
                        {
                            // Check hierarchy - can only manage members with lower rank
                            int targetHierarchy = targetMember.rank?.hierarchy ?? 0;
                            bool canManageThisMember = myHierarchy > targetHierarchy;

                            if (canManageThisMember || isCurrentUserOwner)
                            {
                                canKick = isCurrentUserOwner || (myPerms?.canKick == true);
                                canBan = isCurrentUserOwner || (myPerms?.canBan == true);
                                canPromote = isCurrentUserOwner || (myPerms?.canPromote == true);
                                canDemote = isCurrentUserOwner || (myPerms?.canDemote == true);
                            }
                        }
                    }

                    // Context menu (own messages, moderation permissions, or member management)
                    bool hasAnyContextOption = canEdit || canDelete || canKick || canBan || canPromote || canDemote || canPin;
                    if (hasAnyContextOption && ImGui.BeginPopupContextItem($"msgContext_{message.messageID}"))
                    {
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
                                    GroupChat_DS.PinGroupChatMessage(character, message.messageID, !message.isPinned);
                                }
                                ImGui.CloseCurrentPopup();
                            }
                        }

                        // Member management options (only for other members' messages)
                        if (!isOwnMessage && targetMember != null && (canKick || canBan || canPromote || canDemote))
                        {
                            GroupUi.Divider(4f);
                            GroupUi.Muted($"Member: {message.senderName}");

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

                    // Park the cursor below the row (avatar can be taller than the text).
                    ImGui.SetCursorScreenPos(new Vector2(rowStart.X, rowStart.Y + messageHeights[message.messageID]));
                    ImGui.Dummy(new Vector2(0f, Math.Max(0f, gapAfter - ImGui.GetStyle().ItemSpacing.Y)));
                }
                finally
                {
                    ImGui.PopID();
                }
            }
            hoveredMessageID = newHovered;

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
            string rulesContent = GroupRoles_DR.groupRulesContent;
            int rulesVersion = GroupRoles_DR.groupRulesVersion;
            bool hasAgreed = GroupRoles_DR.hasAgreedToRules;

            // Full height content area for rules
            using (var rulesChild = ImRaii.Child("RulesContent", new Vector2(-1, -1), true))
            {
                if (rulesChild)
                {
                    if (isOwner)
                    {
                        // Owner view - can edit rules
                        ImGui.TextColored(RsTheme.AccentSuccess, "Group Rules (Owner View)");
                        GroupUi.Divider();

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

                            if (GroupUi.Ghost("Edit Rules"))
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
                            ImGui.TextColored(RsTheme.AccentWarning, "Warning: Saving rules will require all members to re-agree.");
                            ImGui.Spacing();

                            GroupUi.SectionLabel("Rules Content");
                            float textHeight = ImGui.GetContentRegionAvail().Y - 60;
                            RsElements.InputTextArea("rules_edit", ref rulesEditContent, 10000, "Write the group rules...", new Vector2(RsElements.AvailContentWidth(), Math.Max(S(80f), textHeight)));

                            ImGui.Spacing();

                            if (GroupUi.Button("Save Rules"))
                            {
                                GroupRoles_DS.SaveGroupRules(character, currentGroup.groupID, rulesEditContent);
                                isEditingRules = false;
                            }
                            ImGui.SameLine();
                            if (GroupUi.Ghost("Cancel"))
                            {
                                isEditingRules = false;
                                rulesEditContent = string.Empty;
                            }
                        }
                    }
                    else
                    {
                        // Member view - show rules and agree button
                        ImGui.TextColored(RsTheme.AccentWarning, "Group Rules");
                        GroupUi.Divider();

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
                        GroupUi.Divider();

                        if (hasAgreed)
                        {
                            ImGui.TextColored(RsTheme.AccentSuccess, "You have agreed to the current rules.");
                        }
                        else if (!string.IsNullOrEmpty(rulesContent))
                        {
                            ImGui.TextColored(RsTheme.AccentWarning, "You must agree to the rules to access other channels.");
                            ImGui.Spacing();

                            if (GroupUi.Button("I Agree to These Rules", new Vector2(200, 30)))
                            {
                                GroupRoles_DS.AgreeToGroupRules(character, currentGroup.groupID, rulesVersion);
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
            var fields = GroupForms_DR.formFields.ContainsKey(selectedChannel.id)
                ? GroupForms_DR.formFields[selectedChannel.id]
                : new List<FormField>();

            // Get submissions from cache
            var submissions = GroupForms_DR.formSubmissions.ContainsKey(selectedChannel.id)
                ? GroupForms_DR.formSubmissions[selectedChannel.id]
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

        private static readonly List<string> FormFieldTypeOptions = new List<string> { "Single Line", "Multi Line" };

        private static void DrawFormEditor(Character character, List<FormField> fields)
        {
            ImGui.TextColored(RsTheme.AccentPrimary, "Form Editor");
            ImGui.TextDisabled("Create and manage form fields that members will fill out.");
            GroupUi.Divider();
            ImGui.Spacing();

            // Settings
            bool allowTags = formAllowFormatTags;
            if (RsElements.Checkbox("Allow Format Tags in Submissions", ref allowTags))
            {
                GroupForms_DS.UpdateFormChannelSettings(character, selectedChannel.id, allowTags);
                selectedChannel.allowFormatTags = allowTags;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("When enabled, submissions can contain images, colored text, etc.");

            ImGui.Spacing();
            GroupUi.Divider();
            ImGui.Spacing();

            // Existing fields
            GroupUi.SectionLabel("Form Fields");
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
                        GroupUi.Input("Title##Edit", ref editFormFieldTitle, 255);
                        ImGui.SameLine();
                        RsElements.Dropdown("form_field_type_edit", ref editFormFieldType, FormFieldTypeOptions, 120f);
                        ImGui.SameLine();
                        RsElements.Checkbox("Optional##Edit", ref editFormFieldOptional);
                        ImGui.SameLine();
                        if (GroupUi.Button("Save##EditField"))
                        {
                            GroupForms_DS.UpdateFormField(character, field.id, editFormFieldTitle, editFormFieldType, editFormFieldOptional, field.sortOrder);
                            editingFormFieldId = null;
                        }
                        ImGui.SameLine();
                        if (GroupUi.Ghost("Cancel##EditField"))
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
                        if (RsElements.Button("Edit##row", RsElements.ButtonVariant.Ghost))
                        {
                            editingFormFieldId = field.id;
                            editFormFieldTitle = field.title;
                            editFormFieldType = field.fieldType;
                            editFormFieldOptional = field.isOptional;
                        }
                        ImGui.SameLine();
                        if (RsElements.Button("Delete##row", RsElements.ButtonVariant.Danger))
                        {
                            GroupForms_DS.DeleteFormField(character, field.id);
                        }

                        // Move buttons
                        if (i > 0)
                        {
                            ImGui.SameLine();
                            if (RsElements.IconButton(FontAwesomeIcon.ArrowUp, "row_up", RsElements.ButtonVariant.Ghost, 26f))
                            {
                                // Swap sort orders
                                var prevField = fields[i - 1];
                                GroupForms_DS.UpdateFormField(character, field.id, field.title, field.fieldType, field.isOptional, prevField.sortOrder);
                                GroupForms_DS.UpdateFormField(character, prevField.id, prevField.title, prevField.fieldType, prevField.isOptional, field.sortOrder);
                            }
                        }
                        if (i < fields.Count - 1)
                        {
                            ImGui.SameLine();
                            if (RsElements.IconButton(FontAwesomeIcon.ArrowDown, "row_down", RsElements.ButtonVariant.Ghost, 26f))
                            {
                                // Swap sort orders
                                var nextField = fields[i + 1];
                                GroupForms_DS.UpdateFormField(character, field.id, field.title, field.fieldType, field.isOptional, nextField.sortOrder);
                                GroupForms_DS.UpdateFormField(character, nextField.id, nextField.title, nextField.fieldType, nextField.isOptional, field.sortOrder);
                            }
                        }
                    }

                    ImGui.PopID();
                }
            }

            ImGui.Spacing();
            GroupUi.Divider();
            ImGui.Spacing();

            // Add new field
            GroupUi.SectionLabel("Add New Field");
            ImGui.SetNextItemWidth(200);
            GroupUi.Input("Title##New", ref newFormFieldTitle, 255);
            ImGui.SameLine();
            RsElements.Dropdown("form_field_type_new", ref newFormFieldType, FormFieldTypeOptions, 120f);
            ImGui.SameLine();
            RsElements.Checkbox("Optional##New", ref newFormFieldOptional);
            ImGui.SameLine();

            bool canAdd = !string.IsNullOrWhiteSpace(newFormFieldTitle);
            if (!canAdd) ImGui.BeginDisabled();
            if (GroupUi.Ghost("+ Add Field"))
            {
                int nextSortOrder = fields.Count > 0 ? fields.Max(f => f.sortOrder) + 1 : 0;
                GroupForms_DS.CreateFormField(character, selectedChannel.id, newFormFieldTitle, newFormFieldType, newFormFieldOptional, nextSortOrder);
                newFormFieldTitle = "";
                newFormFieldType = 0;
                newFormFieldOptional = false;
            }
            if (!canAdd) ImGui.EndDisabled();
        }

        private static void DrawFormFill(Character character, List<FormField> fields)
        {
            ImGui.TextColored(RsTheme.AccentPrimary, selectedChannel.name);
            if (!string.IsNullOrEmpty(selectedChannel.description))
            {
                ImGui.TextDisabled(selectedChannel.description);
            }
            GroupUi.Divider();
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
                    if (GroupUi.Input($"##{field.id}", ref value, 1000))
                    {
                        formInputValues[field.id] = value;
                    }
                }
                else
                {
                    // Multi line
                    if (RsElements.InputTextArea($"form_field_{field.id}", ref value, 5000, "", new Vector2(RsElements.AvailContentWidth(), S(100f))))
                    {
                        formInputValues[field.id] = value;
                    }
                }
                ImGui.Spacing();
            }

            ImGui.Spacing();

            // Submit button
            if (GroupUi.Button("Submit", new Vector2(100, 30)))
            {
                // Validate required fields
                bool valid = true;
                foreach (var field in fields)
                {
                    if (!field.isOptional && string.IsNullOrWhiteSpace(formInputValues.GetValueOrDefault(field.id, "")))
                    {
                        valid = false;
                        GroupForms_DR.formSubmitResultSuccess = false;
                        GroupForms_DR.formSubmitResultMessage = $"Please fill in the required field: {field.title}";
                        break;
                    }
                }

                if (valid)
                {
                    // Get current member info from group
                    var currentMember = currentGroup?.members?.FirstOrDefault(m => m.userID == Accounts_DS.userID);
                    int profileId = currentMember?.profileID ?? 0;
                    string profileName = currentMember?.name ?? character.characterName;

                    var fieldValues = formInputValues.Select(kv => (kv.Key, kv.Value)).ToList();
                    GroupForms_DS.SubmitForm(character, selectedChannel.id, profileId, profileName, fieldValues);

                    // Clear form
                    formInputValues.Clear();
                }
            }

            // Show result message
            if (!string.IsNullOrEmpty(GroupForms_DR.formSubmitResultMessage))
            {
                ImGui.Spacing();
                if (GroupForms_DR.formSubmitResultSuccess)
                {
                    ImGui.TextColored(RsTheme.AccentSuccess, GroupForms_DR.formSubmitResultMessage);
                }
                else
                {
                    ImGui.TextColored(RsTheme.AccentDanger, GroupForms_DR.formSubmitResultMessage);
                }
            }
        }

        private static void DrawFormSubmissions(Character character, List<FormSubmission> submissions)
        {
            ImGui.TextColored(RsTheme.AccentWarning, "Form Submissions");
            ImGui.TextDisabled($"{submissions.Count} submission(s)");
            GroupUi.Divider();
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
                        ImGui.TextColored(RsTheme.TextMuted, $"{val.fieldTitle}:");
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
                    if (RsElements.Button("Delete Submission##row", RsElements.ButtonVariant.Danger))
                    {
                        GroupForms_DS.DeleteFormSubmission(character, submission.id);
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
            bool canManageRoles = isOwner || GroupRoles_DR.canManageSelfAssignRoles;

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

                if (GroupUi.Danger("Delete", new Vector2(100, 0)))
                {
                    GroupRoles_DS.DeleteSelfAssignRole(character, currentGroup.groupID, roleToDelete.id);
                    showDeleteRoleConfirmation = false;
                    roleToDelete = null;
                }
                ImGui.SameLine();
                if (GroupUi.Ghost("Cancel", new Vector2(100, 0)))
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

                if (GroupUi.Danger("Delete", new Vector2(100, 0)))
                {
                    GroupRoles_DS.DeleteRoleSection(character, currentGroup.groupID, sectionToDelete.id);
                    showDeleteSectionConfirmation = false;
                    sectionToDelete = null;
                }
                ImGui.SameLine();
                if (GroupUi.Ghost("Cancel", new Vector2(100, 0)))
                {
                    showDeleteSectionConfirmation = false;
                    sectionToDelete = null;
                }
                ImGui.EndPopup();
            }
        }

        private static void DrawRoleSelectionList(Character character)
        {
            ImGui.TextColored(RsTheme.AccentPrimary, "Self-Assign Roles");
            ImGui.TextDisabled("Toggle roles to add or remove them from your profile.");
            GroupUi.Divider();
            ImGui.Spacing();

            // Use data from DataReceiver
            var roles = GroupRoles_DR.selfAssignRoles;
            var sections = GroupRoles_DR.roleSections;
            var memberRoleIds = GroupRoles_DR.memberSelfRoleIDs.ToHashSet();

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
                ImGui.TextColored(RsTheme.AccentWarning, sectionName);
                GroupUi.Divider();

                foreach (var role in sectionGroup.OrderBy(r => r.sortOrder))
                {
                    ImGui.PushID($"role_{role.id}");

                    bool hasRole = memberRoleIds.Contains(role.id);

                    // Parse color from hex
                    Vector4 roleColor = ParseHexColor(role.color);

                    // Role toggle checkbox
                    if (RsElements.Checkbox($"##toggle_{role.id}", ref hasRole))
                    {
                        if (hasRole)
                        {
                            GroupRoles_DS.AssignSelfRole(character, currentGroup.groupID, role.id);
                        }
                        else
                        {
                            GroupRoles_DS.UnassignSelfRole(character, currentGroup.groupID, role.id);
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
            ImGui.TextColored(RsTheme.AccentWarning, "Manage Self-Assign Roles");
            GroupUi.Divider();
            ImGui.Spacing();

            var sections = GroupRoles_DR.roleSections ?? new List<GroupRoleSection>();

            // Section management
            GroupUi.SectionLabel("Role Sections");
            ImGui.SetNextItemWidth(200);
            GroupUi.Input("##NewSectionName", ref newSectionName, 50);
            ImGui.SameLine();
            bool canCreateSection = !string.IsNullOrWhiteSpace(newSectionName);
            if (!canCreateSection) ImGui.BeginDisabled();
            if (GroupUi.Button("Create Section"))
            {
                GroupRoles_DS.CreateRoleSection(character, currentGroup.groupID, newSectionName);
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
                    ImGui.TextColored(RsTheme.AccentWarning, section.name);
                    ImGui.SameLine();
                    if (RsElements.IconButton(FontAwesomeIcon.Trash, "section_delete", RsElements.ButtonVariant.Ghost, 24f))
                    {
                        sectionToDelete = section;
                        showDeleteSectionConfirmation = true;
                    }
                    ImGui.PopID();
                }
                ImGui.Unindent();
            }

            ImGui.Spacing();
            GroupUi.Divider();
            ImGui.Spacing();

            // Create new role section
            GroupUi.SectionLabel("Create New Role");
            ImGui.SetNextItemWidth(200);
            GroupUi.Input("Name##NewRole", ref newRoleName, 50);
            ImGui.SameLine();
            ImGui.ColorEdit4("Color##NewRole", ref newRoleColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoAlpha);

            ImGui.SetNextItemWidth(300);
            GroupUi.Input("Description##NewRole", ref newRoleDescription, 200);

            // Section dropdown for new role
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
            if (RsElements.Dropdown("role_section_new", ref sectionIndex, sectionOptions, 200f))
            {
                newRoleSectionID = sectionIndex == 0 ? 0 : sections[sectionIndex - 1].id;
            }
            ImGui.SameLine();

            bool canCreate = !string.IsNullOrWhiteSpace(newRoleName);
            if (!canCreate) ImGui.BeginDisabled();
            if (GroupUi.Button("Create Role"))
            {
                string hexColor = ColorToHex(newRoleColor);
                GroupRoles_DS.CreateSelfAssignRole(character, currentGroup.groupID, newRoleName, hexColor, newRoleDescription, newRoleSectionID);
                newRoleName = string.Empty;
                newRoleDescription = string.Empty;
                newRoleColor = new Vector4(1f, 1f, 1f, 1f);
                newRoleSectionID = 0;
            }
            if (!canCreate) ImGui.EndDisabled();

            ImGui.Spacing();
            GroupUi.Divider();
            ImGui.Spacing();

            // Existing roles list
            GroupUi.SectionLabel("Existing Roles");

            // Use data from DataReceiver
            var roles = GroupRoles_DR.selfAssignRoles;

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
                    GroupUi.Input("##EditName", ref editRoleName, 50);
                    ImGui.SameLine();
                    ImGui.ColorEdit4("##EditColor", ref editRoleColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoAlpha);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(200);
                    GroupUi.Input("##EditDesc", ref editRoleDescription, 200);

                    // Section dropdown for edit role
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
                    if (RsElements.Dropdown("role_section_edit", ref editSectionIndex, editSectionOptions, 150f))
                    {
                        editRoleSectionID = editSectionIndex == 0 ? 0 : sections[editSectionIndex - 1].id;
                    }
                    ImGui.SameLine();

                    if (RsElements.Button("Save##row", RsElements.ButtonVariant.Primary))
                    {
                        string hexColor = ColorToHex(editRoleColor);
                        GroupRoles_DS.UpdateSelfAssignRole(character, currentGroup.groupID, role.id, editRoleName, hexColor, editRoleDescription, editRoleSectionID);
                        editingRole = null;
                    }
                    ImGui.SameLine();
                    if (RsElements.Button("Cancel##row", RsElements.ButtonVariant.Ghost))
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
                            ImGui.TextColored(RsTheme.TextMuted, $"[{roleSection.name}]");
                        }
                    }
                    ImGui.SameLine();
                    float buttonPosX = ImGui.GetContentRegionAvail().X - 100;
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + buttonPosX);

                    if (RsElements.Button("Edit##row", RsElements.ButtonVariant.Ghost))
                    {
                        editingRole = role;
                        editRoleName = role.name;
                        editRoleDescription = role.description ?? string.Empty;
                        editRoleColor = ParseHexColor(role.color);
                        editRoleSectionID = role.sectionID;
                    }
                    ImGui.SameLine();
                    if (RsElements.Button("Delete##row", RsElements.ButtonVariant.Danger))
                    {
                        roleToDelete = role;
                        showDeleteRoleConfirmation = true;
                    }
                }

                ImGui.PopID();
            }
        }

        // Cached: this runs per role per frame in tooltips and role lists.
        private static Vector4 ParseHexColor(string hex) => GroupUi.Hex(hex);

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
                GroupUi.Icon(FontAwesomeIcon.Lock, RsTheme.TextMuted);
                ImGui.SameLine(0f, S(6f));
                GroupUi.Muted("This channel is locked. Messages cannot be sent.");
                DrawPinnedMessagesPopup();
                return;
            }

            // Check permissions for announcement channels
            if (selectedChannel != null && selectedChannel.channelType == 1)
            {
                // Announcement channel - check if user has permission
                bool canPost = GroupPermissions.CanCreateAnnouncement(currentGroup);
                if (!canPost)
                {
                    GroupUi.Icon(FontAwesomeIcon.Bullhorn, RsTheme.TextMuted);
                    ImGui.SameLine(0f, S(6f));
                    GroupUi.Muted("Only members with announcement permission can post in this channel.");
                    DrawPinnedMessagesPopup();
                    return;
                }
            }

            // Widths derived from the available region so input + attach + send always fit inside the chat area (1px slack for rounding).
            float gap = S(8f);
            float btnH = S(32f);
            float sendBtnW = S(64f);
            float sendW = btnH + S(4f) + sendBtnW;
            float regionW = ImGui.GetContentRegionAvail().X - 1f;

            // Resize handle for chat input
            float resizeHandleHeight = S(8f);
            Vector2 resizeHandlePos = ImGui.GetCursorScreenPos();
            float availableWidth = Math.Max(S(60f), regionW - sendW - gap);

            var drawList = ImGui.GetWindowDrawList();
            Vector2 handleMin = resizeHandlePos;
            Vector2 handleMax = new Vector2(resizeHandlePos.X + availableWidth, resizeHandlePos.Y + resizeHandleHeight);

            // Check if mouse is hovering over resize handle
            Vector2 mousePos = ImGui.GetMousePos();
            bool hoveringHandle = mousePos.X >= handleMin.X && mousePos.X <= handleMax.X &&
                                  mousePos.Y >= handleMin.Y && mousePos.Y <= handleMax.Y;

            // Grip: a short rounded bar, brighter while hovered / dragging.
            float gripWidth = S(36f);
            float gripStartX = handleMin.X + (availableWidth - gripWidth) / 2f;
            float gripY = handleMin.Y + resizeHandleHeight / 2f;
            uint gripColor = hoveringHandle || isResizingChatInput ? RsTheme.U.AccentPrimary : RsTheme.U.BorderStrong;
            drawList.AddRectFilled(new Vector2(gripStartX, gripY - S(1.5f)), new Vector2(gripStartX + gripWidth, gripY + S(1.5f)), gripColor, S(1.5f));

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

            // Move cursor past the resize handle (an item, so dragging it never drags the window)
            ImGui.InvisibleButton("##grp_input_resize", new Vector2(Math.Max(1f, availableWidth), resizeHandleHeight));

            // Input fills the fixed composer region (minus room for the upload status line).
            float scaledHeight = Math.Max(S(24f), ImGui.GetContentRegionAvail().Y - S(16f));
            btnH = Math.Min(btnH, scaledHeight);

            // Get input position before drawing for popup positioning
            Vector2 inputPos = ImGui.GetCursorScreenPos();

            // RsElements-style frame around the multiline input (kept as a raw InputTextMultiline for the Enter-to-send / slash-command logic).
            ImGui.PushStyleColor(ImGuiCol.FrameBg, RsTheme.BgTertiary);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, RsTheme.BgTertiary);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, RsTheme.BgTertiary);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, S(6f));
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(S(10f), S(8f)));
            bool inputActive;
            try
            {
                ImGui.InputTextMultiline("##MessageInput", ref messageInput, 10000,
                    new Vector2(availableWidth, scaledHeight));
                inputActive = ImGui.IsItemActive();
            }
            finally
            {
                ImGui.PopStyleVar(3);
                ImGui.PopStyleColor(3);
            }
            drawList.AddRect(inputPos, inputPos + new Vector2(availableWidth, scaledHeight),
                inputActive ? RsTheme.U.AccentPrimary : RsTheme.U.Border, S(6f), ImDrawFlags.None,
                RsTheme.BorderThickness + (inputActive ? 1f : 0f));
            if (string.IsNullOrEmpty(messageInput) && !inputActive && selectedChannel != null)
            {
                drawList.AddText(inputPos + new Vector2(S(10f), S(8f)), RsTheme.U.TextMuted,
                    $"Message #{selectedChannel.name}   ·   / for commands");
            }

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

            ImGui.SameLine(0f, gap);

            // Buttons on the right side
            float buttonWidth = 100f;
            ImGui.BeginGroup();
            if (editingMessage != null)
            {
                GroupUi.Chip("Editing", RsTheme.AccentWarning);
                float half = (sendW - S(4f)) * 0.5f;
                if (RsElements.IconButton(FontAwesomeIcon.Times, "grp_edit_cancel", RsElements.ButtonVariant.Ghost, half / Math.Max(0.01f, RsTheme.Scale)))
                {
                    CancelEditing();
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Cancel editing");
                ImGui.SameLine(0f, S(4f));
                if (RsElements.IconButton(FontAwesomeIcon.Check, "grp_edit_save", RsElements.ButtonVariant.Primary, half / Math.Max(0.01f, RsTheme.Scale)) || shouldSend)
                {
                    SaveEditedMessage();
                    if (shouldSend)
                    {
                        ImGui.SetKeyboardFocusHere(-1);
                    }
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Save edit");
            }
            else
            {
                // Attach a file, then Send.
                AbsoluteRP.Helpers.ChatAttachments.DrawAttachButton("grp_attach", btnH / Math.Max(0.01f, RsTheme.Scale),
                    url => messageInput = (string.IsNullOrWhiteSpace(messageInput) ? "" : messageInput.TrimEnd() + " ") + url + " ");
                ImGui.SameLine(0f, S(4f));
                if (RsElements.Button("Send##grp_send", RsElements.ButtonVariant.Primary, new Vector2(sendBtnW, btnH)) || shouldSend)
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
            AbsoluteRP.Helpers.ChatAttachments.DrawStatus();   // upload progress / errors under the input

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
                ImGui.TextUnformatted("Are you sure you want to delete this message?");
                GroupUi.Muted("This action cannot be undone.");
                GroupUi.Divider(8f);

                // Center buttons
                float spacing = 10f;
                float totalWidth = S(buttonWidth * 2 + spacing);
                GroupUi.CenterRow(totalWidth);

                if (GroupUi.Danger("Delete", new Vector2(buttonWidth, 0)))
                {
                    if (messageToDelete != null)
                    {
                        DeleteMessage(messageToDelete);
                        messageToDelete = null;
                    }
                    showDeleteConfirmation = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine(0f, S(spacing));

                if (GroupUi.Ghost("Cancel", new Vector2(buttonWidth, 0)))
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
            ImGui.SetNextWindowSize(new Vector2(S(460f), S(420f)), ImGuiCond.FirstUseEver);

            bool pinnedPopupOpen = true;
            if (ImGui.BeginPopupModal("Pinned Messages", ref pinnedPopupOpen, ImGuiWindowFlags.NoCollapse))
            {
                if (selectedChannel != null)
                {
                    GroupUi.SectionLabel($"Pinned in #{selectedChannel.name}");
                }

                if (!GroupChat_DR.pinnedMessagesLoaded)
                {
                    GroupUi.Muted("Loading pinned messages...");
                }
                else if (GroupChat_DR.pinnedMessages.Count == 0)
                {
                    ImGui.TextWrapped("No pinned messages in this channel.");
                    ImGui.Spacing();
                    GroupUi.MutedWrapped("Members with pin permissions can pin important messages by right-clicking on them.");
                }
                else
                {
                    using (var child = ImRaii.Child("PinnedMessagesList", new Vector2(-1, -S(48f)), false))
                    {
                        if (child)
                        {
                            foreach (var msg in GroupChat_DR.pinnedMessages)
                            {
                                ImGui.PushID($"pinned_{msg.messageID}");

                                var rowMin = ImGui.GetCursorScreenPos();
                                float rowW = ImGui.GetContentRegionAvail().X;

                                // Avatar
                                GroupUi.Avatar(msg.avatar, msg.senderName, S(32f));
                                ImGui.SameLine(0f, S(10f));

                                ImGui.BeginGroup();
                                // Sender name + timestamp
                                ImGui.TextUnformatted(msg.senderName ?? "Unknown");
                                ImGui.SameLine(0f, S(8f));
                                var messageTime = DateTimeOffset.FromUnixTimeMilliseconds(msg.timestamp).LocalDateTime;
                                GroupUi.Muted(messageTime.ToString("MMM d, yyyy h:mm tt"));

                                // Message content (truncated)
                                string content = msg.messageContent ?? "";
                                if (content.Length > 100)
                                {
                                    content = content.Substring(0, 100) + "...";
                                }
                                Misc.RenderHtmlElements(content, true, true, true, false, limitImageWidth: true);
                                ImGui.EndGroup();
                                float rowBottom = Math.Max(ImGui.GetItemRectMax().Y, rowMin.Y + S(32f));

                                // Jump to message button (top-right of the row)
                                const string jumpLabel = "Jump##pin_jump";
                                float jumpW = RsElements.MeasureButtonWidth(jumpLabel);
                                ImGui.SetCursorScreenPos(new Vector2(rowMin.X + rowW - jumpW, rowMin.Y));
                                if (RsElements.Button(jumpLabel, RsElements.ButtonVariant.Ghost))
                                {
                                    scrollToMessageID = msg.messageID;
                                    showPinnedMessagesPopup = false;
                                    ImGui.CloseCurrentPopup();
                                }
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip("Scroll to this message in chat");
                                }
                                rowBottom = Math.Max(rowBottom, ImGui.GetItemRectMax().Y);

                                ImGui.SetCursorScreenPos(new Vector2(rowMin.X, rowBottom + S(6f)));
                                GroupUi.Divider(6f);
                                ImGui.PopID();
                            }
                        }
                    }
                }

                ImGui.Spacing();

                // Close button
                float buttonWidth = 100f;
                GroupUi.CenterRow(S(buttonWidth));
                if (GroupUi.Ghost("Close", new Vector2(buttonWidth, 0)))
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
                    GroupChat_DS.SendGroupChatMessage(character, currentGroup.groupID, selectedChannel.id, processedMessage);

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

        /// Wraps image URLs (http/https containing .jpg, .jpeg, .png, .gif, .webp) in img tags. Handles URLs with query parameters like image.png?size=large
        // Regex to match URLs that contain image extensions anywhere (including with query params). Matches URLs not already wrapped in <img> tags. Built once, not per send.
        private static readonly System.Text.RegularExpressions.Regex imageUrlPattern = new System.Text.RegularExpressions.Regex(
            @"(?<!<img>)(https?://[^\s<>""]*\.(?:jpg|jpeg|png|gif|webp)(?:[^\s<>""]*)?)(?!</img>)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        private static string WrapImageUrls(string message)
        {
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
                        GroupRoles_DS.FetchGroupRules(character, currentGroup.groupID);
                    }
                    else if (selectedChannel.channelType == 3)
                    {
                        // Role Selection channel - fetch self-assign roles and member's current roles
                        Plugin.PluginLog.Info($"[GroupsData] Fetching self-assign roles for group {currentGroup.groupID}");
                        GroupRoles_DS.FetchSelfAssignRoles(character, currentGroup.groupID);
                        GroupRoles_DS.FetchMemberSelfRoles(character, currentGroup.groupID);
                        GroupRoles_DS.FetchRoleSections(character, currentGroup.groupID);
                    }
                    else if (selectedChannel.channelType == 4)
                    {
                        // Form channel - fetch form fields and submissions (if owner/admin)
                        Plugin.PluginLog.Info($"[GroupsData] Fetching form fields for channel {selectedChannel.id}");
                        GroupForms_DS.FetchFormFields(character, selectedChannel.id);
                        GroupForms_DS.FetchFormSubmissions(character, selectedChannel.id);
                    }
                    else
                    {
                        // Normal text/announcement channel - fetch messages
                        Plugin.PluginLog.Info($"[GroupsData] Requesting messages from server for channel {selectedChannel.id}");
                        GroupChat_DS.FetchGroupChatMessages(character, currentGroup.groupID, selectedChannel.id, 50, 0);
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

        /// Asynchronously loads avatars for users in the message list. This is called after messages are displayed to avoid blocking initial load.
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
                            Groups_DS.FetchGroupMemberAvatar(character, currentGroup.groupID, userID);
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

                bool changed;
                lock (messagesLock)
                {
                    if (currentMessages.Count == 0)
                    {
                        currentMessages = messages;
                        changed = true;
                    }
                    else
                    {
                        // A refresh of a channel already on screen: add what is new, update what was edited, drop what was deleted - keeping the existing rows (and their loaded avatars) in place.
                        changed = false;
                        var byId = currentMessages.ToDictionary(m => m.messageID, m => m);
                        foreach (var m in messages)
                        {
                            if (byId.TryGetValue(m.messageID, out var have))
                            {
                                if (have.messageContent != m.messageContent || have.isEdited != m.isEdited || have.isPinned != m.isPinned)
                                { have.messageContent = m.messageContent; have.isEdited = m.isEdited; have.isPinned = m.isPinned; changed = true; }
                            }
                            else { currentMessages.Add(m); changed = true; }
                        }
                        var incoming = new HashSet<int>(messages.Select(m => m.messageID));
                        int oldestIncoming = messages.Min(m => m.messageID);
                        int removed = currentMessages.RemoveAll(m => m.messageID >= oldestIncoming && !incoming.Contains(m.messageID));
                        if (removed > 0) changed = true;
                        if (changed) currentMessages.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));
                    }
                }
                if (changed) autoScroll = true;

                // Trigger async avatar loading in the background
                LoadAvatarsAsync(messages);
            }
            else
            {
                Plugin.PluginLog.Info($"[GroupsData] Messages are for channel {messages[0].channelID}, but current channel is {selectedChannel?.id}, ignoring");

                // Don't dispose textures here - they will be disposed when we switch channels via ClearAvatarCache() Disposing during message receive can cause crashes if ImGui is rendering those textures
            }
        }

        /// Called when a message is deleted (broadcast from server). Removes the message from the current messages list if it's in the current channel.
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

        /// Called when a message is edited (broadcast from server). Updates the message content in the current messages list if it's in the current channel.
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

        /// Called when an avatar is received from the server. Updates all messages from this user with the new avatar.
        public static void OnAvatarReceived(int userID, byte[] avatarBytes)
        {
            if (avatarBytes == null || avatarBytes.Length == 0)
            {
                Plugin.PluginLog.Warning($"[OnAvatarReceived] Received empty avatar for user {userID}");
                return;
            }

            Plugin.PluginLog.Info($"[OnAvatarReceived] Received avatar for user {userID}, {avatarBytes.Length} bytes");

            // Decode off the calling thread instead of blocking it on CreateFromImageAsync(...).GetAwaiter().GetResult().
            _ = ApplyReceivedAvatarAsync(userID, avatarBytes);
        }

        private static async Task ApplyReceivedAvatarAsync(int userID, byte[] avatarBytes)
        {
            try
            {
                // Create texture from bytes
                var texture = await Plugin.TextureProvider.CreateFromImageAsync(avatarBytes).ConfigureAwait(false);
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

                // Don't dispose textures here - they will be disposed when we switch channels via ClearAvatarCache() Disposing during message broadcast can cause crashes if ImGui is rendering those textures

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
                    GroupChat_DS.EditGroupChatMessage(character, editingMessage.messageID, messageInput);

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
                    GroupChat_DS.DeleteGroupChatMessage(character, message.messageID);

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
            ImGui.TextColored(RsTheme.TextMuted, "Slash Commands");
            GroupUi.Divider();

            var filtered = slashCommands
                .Select((cmd, idx) => new { Command = cmd, Description = slashCommandDescriptions[idx], Index = idx })
                .Where(x => string.IsNullOrEmpty(slashCommandSearch) || x.Command.StartsWith(slashCommandSearch, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (filtered.Count == 0)
            {
                ImGui.TextColored(RsTheme.TextMuted, "No matching commands");
                return;
            }

            // Clamp selection index
            slashCommandSelectedIndex = Math.Clamp(slashCommandSelectedIndex, 0, filtered.Count - 1);

            for (int i = 0; i < filtered.Count; i++)
            {
                var item = filtered[i];
                bool isSelected = i == slashCommandSelectedIndex;

                if (isSelected)
                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentWarning);

                if (ImGui.Selectable($"{item.Command}##cmd{i}", isSelected))
                {
                    SelectSlashCommand(item.Command);
                    handledEnter = true;
                }

                if (isSelected)
                    ImGui.PopStyleColor();

                ImGui.SameLine(150);
                ImGui.TextColored(RsTheme.TextMuted, item.Description);
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
            ImGui.TextColored(RsTheme.TextMuted, "Select a Profile");
            GroupUi.Divider();

            RsElements.InputText("slash_profile_search", ref slashCommandSelectionSearch, 100, "Search profiles...");

            ImGui.Spacing();

            // Get profiles from ProfileWindow
            var profiles = ProfilesPage.profiles ?? new List<ProfileData>();

            // Filter by search
            var filtered = profiles
                .Where(p => string.IsNullOrEmpty(slashCommandSelectionSearch) ||
                           (p.title?.Contains(slashCommandSelectionSearch, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();

            if (filtered.Count == 0)
            {
                ImGui.TextColored(RsTheme.TextMuted, "No profiles found");

                // Allow manual ID entry
                ImGui.Spacing();
                GroupUi.SectionLabel("Or enter Profile ID");
                ImGui.SetNextItemWidth(100);
                if (ImGui.InputInt("##ProfileID", ref slashCommandSelectionIndex))
                {
                    // slashCommandSelectionIndex used as profile ID here
                }
                if (GroupUi.Button("Add by ID") || (enterPressed && slashCommandSelectionIndex > 0))
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

            if (GroupUi.Ghost("Cancel"))
            {
                CloseSlashCommandPopup();
            }
        }

        private static void DrawGroupInviteSelection(ref bool handledEnter, bool enterPressed)
        {
            ImGui.TextColored(RsTheme.TextMuted, "Select a Group to Invite To");
            GroupUi.Divider();

            RsElements.InputText("slash_group_search", ref slashCommandSelectionSearch, 100, "Search groups...");

            ImGui.Spacing();

            // Get groups where user can invite
            var invitableGroups = groups?
                .Where(g => GroupPermissions.CanInvite(g, false))
                .Where(g => string.IsNullOrEmpty(slashCommandSelectionSearch) ||
                           (g.name?.Contains(slashCommandSelectionSearch, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList() ?? new List<Group>();

            if (invitableGroups.Count == 0)
            {
                ImGui.TextColored(RsTheme.TextMuted, "No groups available for invites");
                ImGui.TextWrapped("You need invite permission in at least one group.");

                if (GroupUi.Ghost("Cancel"))
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

            if (GroupUi.Ghost("Cancel"))
            {
                CloseSlashCommandPopup();
            }
        }

        private static void InsertProfileEmbed(int profileID)
        {
            // Insert profile embed tag - only includes ID for security Name and avatar are fetched from server to prevent spoofing Format: [profile:ID]
            string embed = $"[profile:{profileID}]";
            messageInput += embed;
        }

        private static void InsertGroupInviteEmbed(int groupID)
        {
            // Insert group invite embed tag - only includes ID for security Name and logo are fetched from server to prevent spoofing Format: [groupinvite:ID]
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

        // Parsed message cache
        // Every chat row used to construct six Regex objects and re-scan its content every frame. The patterns are now static and each message's segment list is cached until its text changes.
        private static readonly System.Text.RegularExpressions.Regex profileEmbedRegex =
            new System.Text.RegularExpressions.Regex(@"\[profile:(\d+)\]", System.Text.RegularExpressions.RegexOptions.Compiled);
        private static readonly System.Text.RegularExpressions.Regex groupInviteEmbedRegex =
            new System.Text.RegularExpressions.Regex(@"\[groupinvite:(\d+)\]", System.Text.RegularExpressions.RegexOptions.Compiled);
        private static readonly System.Text.RegularExpressions.Regex spoilerRegex =
            new System.Text.RegularExpressions.Regex(@"<spoiler>(.*?)</spoiler>", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Compiled);
        private static readonly System.Text.RegularExpressions.Regex nsfwRegex =
            new System.Text.RegularExpressions.Regex(@"<nsfw>(.*?)</nsfw>", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Compiled);
        private static readonly System.Text.RegularExpressions.Regex spoilerOpenRegex =
            new System.Text.RegularExpressions.Regex(@"<spoiler>", System.Text.RegularExpressions.RegexOptions.Compiled);
        private static readonly System.Text.RegularExpressions.Regex nsfwOpenRegex =
            new System.Text.RegularExpressions.Regex(@"<nsfw>", System.Text.RegularExpressions.RegexOptions.Compiled);

        private enum SegmentKind { Text, Profile, GroupInvite, Spoiler, Nsfw }

        private readonly struct MessageSegment
        {
            public readonly SegmentKind Kind;
            public readonly string Text;   // text / spoiler / nsfw body
            public readonly int Id;        // profile or group id
            public readonly int Position;  // spoiler / nsfw reveal key
            public MessageSegment(SegmentKind kind, string text, int id, int position)
            { Kind = kind; Text = text; Id = id; Position = position; }
        }

        private sealed class ParsedMessage
        {
            public string Source;
            public List<MessageSegment> Segments;
        }

        private static readonly Dictionary<long, ParsedMessage> parsedMessageCache = new Dictionary<long, ParsedMessage>();

        private static List<MessageSegment> GetMessageSegments(string content, long messageID)
        {
            if (parsedMessageCache.TryGetValue(messageID, out var cached) && string.Equals(cached.Source, content, StringComparison.Ordinal))
                return cached.Segments;

            if (parsedMessageCache.Count > 4000)
                parsedMessageCache.Clear();

            var segments = ParseMessageSegments(content);
            parsedMessageCache[messageID] = new ParsedMessage { Source = content, Segments = segments };
            return segments;
        }

        // Same splitting rules as the old per-frame parser: collect every special element, order by start, and emit the text between them.
        private static List<MessageSegment> ParseMessageSegments(string content)
        {
            var result = new List<MessageSegment>();
            if (string.IsNullOrEmpty(content))
                return result;

            var elements = new List<(int start, int end, string type, System.Text.RegularExpressions.Match match)>();

            foreach (System.Text.RegularExpressions.Match m in profileEmbedRegex.Matches(content))
                elements.Add((m.Index, m.Index + m.Length, "profile", m));
            foreach (System.Text.RegularExpressions.Match m in groupInviteEmbedRegex.Matches(content))
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
                    elements.Add((openMatch.Index, content.Length, "spoiler_open", openMatch));
            }
            if (!nsfwRegex.IsMatch(content))
            {
                var openMatch = nsfwOpenRegex.Match(content);
                if (openMatch.Success)
                    elements.Add((openMatch.Index, content.Length, "nsfw_open", openMatch));
            }

            // Sort by position
            elements = elements.OrderBy(e => e.start).ToList();

            int lastIndex = 0;
            foreach (var element in elements)
            {
                // Text before this element
                if (element.start > lastIndex)
                {
                    string beforeText = content.Substring(lastIndex, element.start - lastIndex);
                    if (!string.IsNullOrWhiteSpace(beforeText))
                        result.Add(new MessageSegment(SegmentKind.Text, beforeText, 0, 0));
                }

                switch (element.type)
                {
                    case "profile":
                        if (int.TryParse(element.match.Groups[1].Value, out var pid))
                            result.Add(new MessageSegment(SegmentKind.Profile, null, pid, element.start));
                        break;
                    case "groupinvite":
                        if (int.TryParse(element.match.Groups[1].Value, out var gid))
                            result.Add(new MessageSegment(SegmentKind.GroupInvite, null, gid, element.start));
                        break;
                    case "spoiler":
                        result.Add(new MessageSegment(SegmentKind.Spoiler, element.match.Groups[1].Value, 0, element.start));
                        break;
                    case "nsfw":
                        result.Add(new MessageSegment(SegmentKind.Nsfw, element.match.Groups[1].Value, 0, element.start));
                        break;
                    case "spoiler_open":
                        result.Add(new MessageSegment(SegmentKind.Spoiler, content.Substring(element.match.Index + element.match.Length), 0, element.start));
                        lastIndex = content.Length;
                        continue;
                    case "nsfw_open":
                        result.Add(new MessageSegment(SegmentKind.Nsfw, content.Substring(element.match.Index + element.match.Length), 0, element.start));
                        lastIndex = content.Length;
                        continue;
                }

                lastIndex = element.end;
            }

            // Any remaining text
            if (lastIndex < content.Length)
            {
                string afterText = content.Substring(lastIndex);
                if (!string.IsNullOrWhiteSpace(afterText))
                    result.Add(new MessageSegment(SegmentKind.Text, afterText, 0, 0));
            }
            return result;
        }

        /// Renders message content with support for profile embeds, group invites, spoilers, and nsfw tags
        private static void RenderMessageWithEmbeds(string content, long messageID)
        {
            if (string.IsNullOrEmpty(content))
                return;

            var segments = GetMessageSegments(content, messageID);
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                switch (seg.Kind)
                {
                    case SegmentKind.Text:
                        if (AbsoluteRP.Helpers.ChatAttachments.HasMedia(seg.Text))
                            AbsoluteRP.Helpers.ChatAttachments.RenderWithMedia(seg.Text, s => Misc.RenderHtmlElements(s, true, true, true, false, limitImageWidth: true), ImGui.GetContentRegionAvail().X);
                        else
                            Misc.RenderHtmlElements(seg.Text, true, true, true, false, limitImageWidth: true);
                        break;
                    case SegmentKind.Profile:
                        RenderProfileEmbed(seg.Id, messageID);
                        break;
                    case SegmentKind.GroupInvite:
                        RenderGroupInviteEmbed(seg.Id, messageID);
                        break;
                    case SegmentKind.Spoiler:
                        RenderSpoilerContent(seg.Text, messageID, seg.Position);
                        break;
                    case SegmentKind.Nsfw:
                        RenderNsfwContent(seg.Text, messageID, seg.Position);
                        break;
                }
            }
        }

        // Embed card chrome shared by profile and group-invite embeds.
        private static bool BeginEmbedCard(string id, float width, float height)
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, RsTheme.BgTertiary);
            ImGui.PushStyleColor(ImGuiCol.Border, RsTheme.Border);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, S(8f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(S(8f), S(8f)));
            bool open = ImGui.BeginChild(id, new Vector2(width, height), true, ImGuiWindowFlags.NoScrollbar);
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(2);
            return open;
        }

        private static void RenderProfileEmbed(int profileID, long messageID)
        {
            // Get profile info from cache or fetch from server
            string profileName = null;
            IDalamudTextureWrap avatarTexture = null;

            // Check the profile info cache first
            var cachedInfo = GroupsData.GetCachedProfileInfo(profileID);
            if (cachedInfo != null)
            {
                profileName = cachedInfo.name;
                avatarTexture = cachedInfo.avatar;

                // If we have URL but no avatar texture yet, fetch it (guarded inside so it's one download, not one per frame)
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
                    Groups_DS.FetchProfileInfo(profileID);
                }
            }

            // Use placeholder if name not yet loaded
            if (string.IsNullOrEmpty(profileName))
            {
                profileName = "Loading...";
            }

            float avatarSize = S(40f);
            float padding = S(8f);
            const string viewLabel = "View Profile";
            float buttonWidth = RsElements.MeasureButtonWidth(viewLabel);
            float buttonHeight = ImGui.GetTextLineHeight() + S(18f);
            float textHeight = ImGui.GetTextLineHeight();
            float contentHeight = Math.Max(avatarSize, textHeight + buttonHeight + S(6f)) + padding * 2;
            float nameWidth = ImGui.CalcTextSize(profileName).X;
            float contentWidth = Math.Max(avatarSize + Math.Max(nameWidth, buttonWidth) + padding * 3 + S(12f), S(220f));

            bool open = BeginEmbedCard($"ProfileEmbed_{messageID}_{profileID}", contentWidth, contentHeight);
            try
            {
                if (open)
                {
                    GroupUi.Avatar(avatarTexture, profileName, avatarSize);
                    ImGui.SameLine(0f, S(10f));

                    ImGui.BeginGroup();
                    ImGui.TextUnformatted(profileName);
                    if (RsElements.Button($"{viewLabel}##{messageID}_{profileID}", RsElements.ButtonVariant.Secondary))
                    {
                        // Open the target profile window and fetch the profile
                        Plugin.plugin.OpenTargetWindow();
                        TargetProfileWindow.RequestingProfile = true;
                        TargetProfileWindow.ResetAllData();
                        Profiles_DS.FetchProfile(Plugin.character, false, -1, string.Empty, string.Empty, profileID);
                    }
                    ImGui.EndGroup();
                }
            }
            finally
            {
                ImGui.EndChild();
            }
        }

        #region Group Search UI

        /// Draws the group search bar and results at the top of the groups panel
        private static void DrawGroupSearchBar()
        {
            const string searchLabel = "Search##grp_search_btn";
            float btnW = RsElements.MeasureButtonWidth(searchLabel);
            float inputW = Math.Max(S(120f), ImGui.GetContentRegionAvail().X - btnW - S(8f));

            // Search input with a trailing button; Enter also searches.
            RsElements.InputText("grp_search_input", ref groupSearchQuery, 100, "Search public groups...", inputW / Math.Max(0.01f, RsTheme.Scale));
            bool enterPressed = ImGui.IsItemDeactivated() && (ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter));

            ImGui.SameLine(0f, S(8f));
            bool searchClicked = RsElements.Button(searchLabel, RsElements.ButtonVariant.Primary);

            if ((enterPressed || searchClicked) && !string.IsNullOrWhiteSpace(groupSearchQuery))
            {
                GroupSearch_DS.SearchPublicGroups(Plugin.character, groupSearchQuery);
                showSearchResults = true;
            }

            ImGui.Dummy(new Vector2(0f, S(4f)));

            // Show search results if we have any or if search is in progress
            if (showSearchResults)
            {
                DrawGroupSearchResults();
                ImGui.Dummy(new Vector2(0f, S(4f)));
            }
        }

        /// Draws the search results panel
        private static void DrawGroupSearchResults()
        {
            // Show loading indicator
            if (GroupSearch_DR.groupSearchInProgress)
            {
                GroupUi.Muted("Searching...");
                return;
            }

            var results = GroupSearch_DR.groupSearchResults;

            // Header with close button
            var headerStart = ImGui.GetCursorPos();
            GroupUi.SectionLabel($"Search results — {results?.Count ?? 0}");
            float closeSize = 24f;
            ImGui.SetCursorPos(new Vector2(headerStart.X + ImGui.GetContentRegionAvail().X - S(closeSize), headerStart.Y - S(4f)));
            if (RsElements.IconButton(FontAwesomeIcon.Times, "grp_search_close", RsElements.ButtonVariant.Ghost, closeSize))
            {
                showSearchResults = false;
                // Retire the result logos safely (they may have been drawn this frame).
                foreach (var r in GroupSearch_DR.groupSearchResults)
                {
                    if (r?.logo != null)
                    {
                        TextureGraveyard.Enqueue(r.logo);
                        r.logo = null;
                    }
                }
                GroupSearch_DR.groupSearchResults.Clear();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Close results");
            ImGui.SetCursorPos(new Vector2(headerStart.X, Math.Max(ImGui.GetCursorPosY(), headerStart.Y + ImGui.GetTextLineHeight() + S(6f))));

            if (results == null || results.Count == 0)
            {
                GroupUi.Muted("No public groups found matching your search.");
                ImGui.Spacing();
                return;
            }

            // Scrollable results area
            float itemH = S(64f) + ImGui.GetStyle().ItemSpacing.Y;
            float resultsHeight = Math.Min(results.Count * itemH, S(260f));
            using (var child = ImRaii.Child("GroupSearchResults", new Vector2(-1, resultsHeight), false))
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

        /// Draws a single search result item similar to the group invite embed
        private static void DrawGroupSearchResultItem(GroupSearchResult result)
        {
            // Check if user is already a member of this group
            var memberGroup = groups?.FirstOrDefault(g => g.groupID == result.groupID);
            bool isMember = memberGroup != null;
            bool isPending = GroupsData.pendingJoinRequests.Contains(result.groupID);

            // Fetch logo if needed (one download per result, not per frame)
            if (result.logo == null && !string.IsNullOrEmpty(result.logoUrl))
            {
                FetchSearchResultLogo(result);
            }

            float logoSize = S(40f);
            float cardH = S(64f);
            var cardMin = ImGui.GetCursorScreenPos();
            float cardW = ImGui.GetContentRegionAvail().X - S(2f);

            ImGui.PushID($"search_{result.groupID}");
            try
            {
                // Hover detection for the card
                bool hovered = ImGui.IsMouseHoveringRect(cardMin, cardMin + new Vector2(cardW, cardH)) && ImGui.IsWindowHovered();
                GroupUi.CardBackground(cardMin, cardMin + new Vector2(cardW, cardH), hovered);

                // Logo
                ImGui.SetCursorScreenPos(cardMin + new Vector2(S(12f), (cardH - logoSize) * 0.5f));
                GroupUi.Logo(result.logo, result.name, logoSize, S(8f));

                // Text section
                float textX = cardMin.X + S(12f) + logoSize + S(12f);
                float lineH = ImGui.GetTextLineHeight();
                float textY = cardMin.Y + (cardH - (lineH * 2f + S(4f))) * 0.5f;

                // Action button (right-aligned)
                string actionLabel = isMember ? $"View##{result.groupID}"
                                   : result.openInvite ? $"Join##{result.groupID}"
                                   : $"Request##{result.groupID}";
                float actionW = isPending && !isMember ? ImGui.CalcTextSize("Pending").X + S(14f) : RsElements.MeasureButtonWidth(actionLabel);
                float textRight = cardMin.X + cardW - actionW - S(24f);

                var dl = ImGui.GetWindowDrawList();
                dl.PushClipRect(new Vector2(textX, cardMin.Y), new Vector2(Math.Max(textX, textRight), cardMin.Y + cardH), true);
                string title = result.name ?? "Unknown Group";
                dl.AddText(new Vector2(textX, textY), RsTheme.U.TextPrimary, title);
                float titleW = ImGui.CalcTextSize(title).X;
                dl.AddText(new Vector2(textX + titleW + S(8f), textY), RsTheme.U.TextMuted, $"{result.memberCount} members");

                // Description (truncated)
                string desc = result.description ?? "";
                if (desc.Length > 60) desc = desc.Substring(0, 57) + "...";
                dl.AddText(new Vector2(textX, textY + lineH + S(4f)), RsTheme.U.TextSecondary, desc.Replace('\n', ' '));
                dl.PopClipRect();

                float btnH = ImGui.GetTextLineHeight() + S(18f);
                ImGui.SetCursorScreenPos(new Vector2(cardMin.X + cardW - actionW - S(12f), cardMin.Y + (cardH - btnH) * 0.5f));

                if (isMember)
                {
                    if (RsElements.Button(actionLabel, RsElements.ButtonVariant.Ghost))
                    {
                        LoadGroup(memberGroup);
                        showSearchResults = false;
                    }
                }
                else if (isPending)
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (btnH - lineH - S(4f)) * 0.5f);
                    GroupUi.Chip("Pending", RsTheme.AccentWarning);
                }
                else if (result.openInvite)
                {
                    // Open groups - direct join
                    if (RsElements.Button(actionLabel, RsElements.ButtonVariant.Success))
                    {
                        Groups_DS.RequestJoinGroup(Plugin.character, result.groupID);
                        GroupsData.pendingJoinRequests.Add(result.groupID);
                    }
                }
                else
                {
                    // Closed groups - request to join
                    if (RsElements.Button(actionLabel, RsElements.ButtonVariant.Primary))
                    {
                        // Open the join request dialog
                        AbsoluteRP.Windows.Social.Views.Groups.GroupJoinRequestDialog.Open(result.groupID, result.name, result.description);
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("This group requires approval to join. Click to send a request.");
                    }
                }

                // Park the cursor under the card
                ImGui.SetCursorScreenPos(new Vector2(cardMin.X, cardMin.Y + cardH));
                ImGui.Dummy(new Vector2(cardW, 0f));
            }
            finally
            {
                ImGui.PopID();
            }
        }

        // One outstanding download per URL, and a cool-down after a failure, so a result/embed without a logo doesn't start a new HTTP fetch and texture upload every frame while the first one is still running.
        private static readonly HashSet<string> imageFetchesInFlight = new HashSet<string>();
        private static readonly Dictionary<string, long> imageFetchFailedAt = new Dictionary<string, long>();
        private const long ImageFetchRetryMs = 30000;

        internal static bool TryBeginImageFetch(string key)
        {
            lock (imageFetchesInFlight)
            {
                if (imageFetchesInFlight.Contains(key)) return false;
                if (imageFetchFailedAt.TryGetValue(key, out var failedAt) && Environment.TickCount64 - failedAt < ImageFetchRetryMs)
                    return false;
                imageFetchesInFlight.Add(key);
                return true;
            }
        }

        internal static void EndImageFetch(string key, bool success)
        {
            lock (imageFetchesInFlight)
            {
                imageFetchesInFlight.Remove(key);
                if (success) imageFetchFailedAt.Remove(key);
                else imageFetchFailedAt[key] = Environment.TickCount64;
            }
        }

        /// Fetches and caches logo for a search result
        private static async void FetchSearchResultLogo(GroupSearchResult result)
        {
            if (string.IsNullOrEmpty(result.logoUrl)) return;
            if (result.logo != null) return;

            string key = "search:" + result.groupID + ":" + result.logoUrl;
            if (!TryBeginImageFetch(key)) return;
            bool ok = false;
            try
            {
                var logoBytes = await Imaging.FetchUrlImageBytes(result.logoUrl);
                if (logoBytes != null && logoBytes.Length > 0)
                {
                    var logoTexture = await Plugin.TextureProvider.CreateFromImageAsync(logoBytes);
                    if (logoTexture != null)
                    {
                        if (result.logo == null)
                            result.logo = logoTexture;
                        else
                            TextureGraveyard.Enqueue(logoTexture);
                        ok = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Failed to fetch search result logo: {ex.Message}");
            }
            finally
            {
                EndImageFetch(key, ok);
            }
        }

        #endregion

        private static void RenderGroupInviteEmbed(int groupID, long messageID)
        {
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
                    Groups_DS.FetchGroupInfo(groupID);
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

            float logoSize = S(40f);
            float padding = S(8f);
            float textHeight = ImGui.GetTextLineHeight();
            float buttonHeight = textHeight + S(18f);
            float contentHeight = Math.Max(logoSize, textHeight + buttonHeight + S(6f)) + padding * 2;
            float nameWidth = ImGui.CalcTextSize(groupName).X;
            float buttonWidth = Math.Max(RsElements.MeasureButtonWidth("View Group"), RsElements.MeasureButtonWidth("Join Group"));
            float contentWidth = Math.Max(logoSize + Math.Max(nameWidth, buttonWidth) + padding * 3 + S(12f), S(240f));

            bool open = BeginEmbedCard($"GroupInviteEmbed_{messageID}_{groupID}", contentWidth, contentHeight);
            try
            {
                if (open)
                {
                    GroupUi.Logo(logoTexture, groupName, logoSize, S(8f));
                    ImGui.SameLine(0f, S(10f));

                    ImGui.BeginGroup();
                    ImGui.TextUnformatted(groupName);

                    if (isMember)
                    {
                        // User is already a member - show View Group button
                        if (RsElements.Button($"View Group##{messageID}_{groupID}", RsElements.ButtonVariant.Secondary))
                        {
                            // Switch to this group
                            LoadGroup(memberGroup);
                        }
                    }
                    else if (isPending)
                    {
                        // Request already sent
                        GroupUi.Chip("Request Sent", RsTheme.AccentWarning);
                    }
                    else
                    {
                        // User is not a member - show Join Group button
                        if (RsElements.Button($"Join Group##{messageID}_{groupID}", RsElements.ButtonVariant.Success))
                        {
                            // Request to join the group
                            Groups_DS.RequestJoinGroup(Plugin.character, groupID);
                            GroupsData.pendingJoinRequests.Add(groupID);
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Request to join this group");
                        }
                    }
                    ImGui.EndGroup();
                }
            }
            finally
            {
                ImGui.EndChild();
            }
        }

        private static void RenderSpoilerContent(string content, long messageID, int position)
        {
            string spoilerKey = $"{messageID}_{position}";
            bool isRevealed = revealedSpoilers.Contains(spoilerKey);

            if (!isRevealed)
            {
                if (RsElements.Button($"Spoiler — click to reveal##{spoilerKey}", RsElements.ButtonVariant.Secondary))
                {
                    revealedSpoilers.Add(spoilerKey);
                }
            }
            else
            {
                ImGui.BeginGroup();
                GroupUi.Chip("Spoiler", RsTheme.TextMuted);
                Misc.RenderHtmlElements(content, true, true, true, false, limitImageWidth: true);

                ImGui.SameLine();
                if (RsElements.Button($"Hide##{spoilerKey}", RsElements.ButtonVariant.Ghost))
                {
                    revealedSpoilers.Remove(spoilerKey);
                }
                ImGui.EndGroup();
            }
        }

        private static void RenderNsfwContent(string content, long messageID, int position)
        {
            string nsfwKey = $"{messageID}_{position}";
            bool isRevealed = revealedNsfw.Contains(nsfwKey);

            if (!isRevealed)
            {
                if (RsElements.Button($"NSFW content — click to reveal##{nsfwKey}", RsElements.ButtonVariant.Danger))
                {
                    revealedNsfw.Add(nsfwKey);
                }
            }
            else
            {
                ImGui.BeginGroup();
                GroupUi.Chip("NSFW", RsTheme.AccentDanger);
                Misc.RenderHtmlElements(content, true, true, true, false, limitImageWidth: true);

                ImGui.SameLine();
                if (RsElements.Button($"Hide##{nsfwKey}", RsElements.ButtonVariant.Ghost))
                {
                    revealedNsfw.Remove(nsfwKey);
                }
                ImGui.EndGroup();
            }
        }

        #endregion
    }
}