using System;
using System.Linq;

namespace AbsoluteRP.Windows.Social.Views.Groups.GroupManager
{
    /// <summary>
    /// Centralized permission checking for group operations.
    /// Group owners ALWAYS have all permissions, regardless of rank.
    /// </summary>
    public static class GroupPermissions
    {
        /// <summary>
        /// Check if the current user has a specific permission in the group.
        /// Group owners always return true for any permission.
        /// Set enableDetailedLogging to true for debugging permission issues.
        /// </summary>
        public static bool HasPermission(Group group, Func<GroupRankPermissions, bool> permissionCheck, bool enableDetailedLogging = false)
        {
            if (group == null || group.members == null)
            {
                if (enableDetailedLogging)
                    Plugin.PluginLog.Warning($"[GroupPermissions] Group or members is null");
                return false;
            }

            try
            {
                var currentPlayerName = Plugin.plugin.playername;
                var currentUserID = Plugin.plugin.Configuration.account.userID;

                if (enableDetailedLogging)
                {
                    Plugin.PluginLog.Info($"[GroupPermissions] Checking permission for player '{currentPlayerName}' (userID:{currentUserID}) in group '{group.name}'");
                    Plugin.PluginLog.Info($"[GroupPermissions] Group has {group.members.Count} members");

                    // Debug: List all members
                    foreach (var m in group.members)
                    {
                        Plugin.PluginLog.Info($"[GroupPermissions]   - Member: name='{m.name}', userID={m.userID}, owner={m.owner}");
                    }
                }

                // Try to find member by name first (most reliable)
                var member = group.members.FirstOrDefault(m =>
                    !string.IsNullOrEmpty(m.name) &&
                    m.name.Equals(currentPlayerName, StringComparison.OrdinalIgnoreCase));

                // If not found by name, try by userID (if available)
                if (member == null && currentUserID > 0)
                {
                    if (enableDetailedLogging)
                        Plugin.PluginLog.Info($"[GroupPermissions] Not found by name, trying userID {currentUserID}");
                    member = group.members.FirstOrDefault(m => m.userID == currentUserID);
                }

                if (member == null)
                {
                    if (enableDetailedLogging)
                        Plugin.PluginLog.Warning($"[GroupPermissions] Player {currentPlayerName} (userID:{currentUserID}) not found in group {group.name}");
                    return false;
                }

                if (enableDetailedLogging)
                    Plugin.PluginLog.Info($"[GroupPermissions] Found member: name='{member.name}', userID={member.userID}, owner={member.owner}");

                // OWNERS ALWAYS HAVE ALL PERMISSIONS
                if (member.owner)
                {
                    if (enableDetailedLogging)
                        Plugin.PluginLog.Info($"[GroupPermissions] ✓ Player IS OWNER of {group.name}, granting all permissions");
                    return true;
                }

                // Check rank permissions
                if (member.rank != null && member.rank.permissions != null)
                {
                    bool hasPermission = permissionCheck(member.rank.permissions);
                    if (enableDetailedLogging)
                        Plugin.PluginLog.Info($"[GroupPermissions] Player has rank {member.rank.name} with permission={hasPermission} in {group.name}");
                    return hasPermission;
                }

                if (enableDetailedLogging)
                    Plugin.PluginLog.Warning($"[GroupPermissions] Player has no rank in {group.name}");
                return false;
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"[GroupPermissions] Error checking permission: {ex.Message}");
                if (enableDetailedLogging)
                    Plugin.PluginLog.Debug($"[GroupPermissions] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Check if the current user is the owner of the group.
        /// </summary>
        public static bool IsOwner(Group group)
        {
            if (group == null || group.members == null)
            {
                // Don't spam logs - this is called every frame
                return false;
            }

            try
            {
                var currentPlayerName = Plugin.plugin.playername;
                var currentUserID = Plugin.plugin.Configuration.account.userID;

                // Try to find member by name first
                var member = group.members.FirstOrDefault(m =>
                    !string.IsNullOrEmpty(m.name) &&
                    m.name.Equals(currentPlayerName, StringComparison.OrdinalIgnoreCase));

                // Fallback to userID
                if (member == null && currentUserID > 0)
                {
                    member = group.members.FirstOrDefault(m => m.userID == currentUserID);
                }

                if (member == null)
                {
                    // Only log once per group
                    Plugin.PluginLog.Debug($"[GroupPermissions.IsOwner] Member not found in group {group.name}");
                    return false;
                }

                return member.owner;
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"[GroupPermissions] Error checking owner status: {ex.Message}");
                return false;
            }
        }

        // Specific permission checks - all automatically grant permission to owners

        public static bool CanInvite(Group group, bool enableDetailedLogging = false)
            => HasPermission(group, p => p.canInvite, enableDetailedLogging);

        public static bool CanKick(Group group)
            => HasPermission(group, p => p.canKick);

        public static bool CanBan(Group group)
            => HasPermission(group, p => p.canBan);

        public static bool CanPromote(Group group)
            => HasPermission(group, p => p.canPromote);

        public static bool CanDemote(Group group)
            => HasPermission(group, p => p.canDemote);

        public static bool CanAcceptJoinRequests(Group group, bool enableDetailedLogging = false)
            => HasPermission(group, p => p.canAcceptJoinRequests, enableDetailedLogging);

        public static bool CanCreateAnnouncement(Group group)
            => HasPermission(group, p => p.canCreateAnnouncement);

        public static bool CanReadMessages(Group group)
            => HasPermission(group, p => p.canReadMessages);

        public static bool CanSendMessages(Group group)
            => HasPermission(group, p => p.canSendMessages);

        public static bool CanDeleteOthersMessages(Group group)
            => HasPermission(group, p => p.canDeleteOthersMessages);

        public static bool CanPinMessages(Group group)
            => HasPermission(group, p => p.canPinMessages);

        public static bool CanCreateCategory(Group group)
            => HasPermission(group, p => p.canCreateCategory);

        public static bool CanEditCategory(Group group)
            => HasPermission(group, p => p.canEditCategory);

        public static bool CanDeleteCategory(Group group)
            => HasPermission(group, p => p.canDeleteCategory);

        public static bool CanLockCategory(Group group)
            => HasPermission(group, p => p.canLockCategory);

        public static bool CanCreateForum(Group group)
            => HasPermission(group, p => p.canCreateForum);

        public static bool CanEditForum(Group group)
            => HasPermission(group, p => p.canEditForum);

        public static bool CanDeleteForum(Group group)
            => HasPermission(group, p => p.canDeleteForum);

        public static bool CanLockForum(Group group)
            => HasPermission(group, p => p.canLockForum);

        public static bool CanMuteForum(Group group)
            => HasPermission(group, p => p.canMuteForum);

        public static bool CanManageSelfAssignRoles(Group group)
            => HasPermission(group, p => p.canManageSelfAssignRoles);

        public static bool CanCreateForms(Group group)
            => HasPermission(group, p => p.canCreateForms);
    }
}
