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
    // Group rules and self-assign role packets. Split out of DataReceiver.
    internal class GroupRoles_DR
    {
        // Rules Channel and Self-Assign Roles state
        public static string groupRulesContent = string.Empty;

        public static int groupRulesVersion = 0;
        public static bool hasAgreedToRules = false;
        public static bool isGroupOwner = false;
        public static bool rulesOperationSuccess = false;
        public static string rulesOperationMessage = string.Empty;
        public static List<GroupSelfAssignRole> selfAssignRoles = new List<GroupSelfAssignRole>();
        public static List<GroupRoleSection> roleSections = new List<GroupRoleSection>();
        public static bool canManageSelfAssignRoles = false;
        public static List<int> memberSelfRoleIDs = new List<int>();
        public static string selfRoleOperationMessage = string.Empty;
        public static bool selfRoleOperationSuccess = false;

        public static void HandleGroupRulesResponse(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                rulesOperationSuccess = buffer.ReadBool();
                rulesOperationMessage = buffer.ReadString();

                Plugin.PluginLog.Debug($"HandleGroupRulesResponse: groupID={groupID}, success={rulesOperationSuccess}, message={rulesOperationMessage}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleGroupRulesResponse error: {ex.Message}");
            }
        }

        public static void HandleRulesAgreementResponse(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                bool success = buffer.ReadBool();
                int agreedVersion = buffer.ReadInt();

                if (success)
                {
                    hasAgreedToRules = true;

                    // Update the current member's agreement status in the group
                    var group = AbsoluteRP.Windows.Social.Views.GroupsData.groups?.FirstOrDefault(g => g.groupID == groupID);
                    if (group != null && group.members != null)
                    {
                        var member = group.members.FirstOrDefault(m => m.userID == Accounts_DS.userID);
                        if (member != null)
                        {
                            member.hasAgreedToRules = true;
                            member.agreedRulesVersion = agreedVersion;
                        }
                    }
                }

                Plugin.PluginLog.Debug($"HandleRulesAgreementResponse: groupID={groupID}, success={success}, version={agreedVersion}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleRulesAgreementResponse error: {ex.Message}");
            }
        }

        public static void HandleGroupRules(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                groupRulesContent = buffer.ReadString();
                groupRulesVersion = buffer.ReadInt();
                hasAgreedToRules = buffer.ReadBool();
                isGroupOwner = buffer.ReadBool();

                Plugin.PluginLog.Debug($"HandleGroupRules: groupID={groupID}, version={groupRulesVersion}, hasAgreed={hasAgreedToRules}, isOwner={isGroupOwner}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleGroupRules error: {ex.Message}");
            }
        }

        public static void HandleSelfAssignRoleResponse(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                int roleID = buffer.ReadInt();
                selfRoleOperationSuccess = buffer.ReadBool();
                selfRoleOperationMessage = buffer.ReadString();

                Plugin.PluginLog.Debug($"HandleSelfAssignRoleResponse: groupID={groupID}, roleID={roleID}, success={selfRoleOperationSuccess}, message={selfRoleOperationMessage}");
                buffer.Dispose();

                // If operation was successful, refresh the roles list
                if (selfRoleOperationSuccess)
                {
                    var character = Plugin.plugin.Configuration.characters.FirstOrDefault(x =>
                        x.characterName == Plugin.plugin.playername &&
                        x.characterWorld == Plugin.plugin.playerworld);
                    if (character != null)
                    {
                        GroupRoles_DS.FetchSelfAssignRoles(character, groupID);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleSelfAssignRoleResponse error: {ex.Message}");
            }
        }

        public static void HandleSelfAssignRoles(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                canManageSelfAssignRoles = buffer.ReadBool();
                int roleCount = buffer.ReadInt();

                selfAssignRoles.Clear();

                for (int i = 0; i < roleCount; i++)
                {
                    var role = new GroupSelfAssignRole
                    {
                        id = buffer.ReadInt(),
                        sectionID = buffer.ReadInt(),
                        name = buffer.ReadString(),
                        color = buffer.ReadString(),
                        description = buffer.ReadString(),
                        sortOrder = buffer.ReadInt(),
                        groupID = groupID,
                        channelPermissions = new List<GroupChannelRolePermission>()
                    };

                    // Read channel permissions
                    int permCount = buffer.ReadInt();
                    for (int j = 0; j < permCount; j++)
                    {
                        role.channelPermissions.Add(new GroupChannelRolePermission
                        {
                            channelID = buffer.ReadInt(),
                            canView = buffer.ReadBool(),
                            canPost = buffer.ReadBool(),
                            roleID = role.id
                        });
                    }

                    selfAssignRoles.Add(role);
                }

                Plugin.PluginLog.Debug($"HandleSelfAssignRoles: groupID={groupID}, roleCount={roleCount}, canManage={canManageSelfAssignRoles}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleSelfAssignRoles error: {ex.Message}");
            }
        }

        public static void HandleSelfRoleAssignmentResponse(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                int roleID = buffer.ReadInt();
                bool assigned = buffer.ReadBool(); // true = assigned, false = unassigned
                bool success = buffer.ReadBool();

                if (success)
                {
                    if (assigned)
                    {
                        if (!memberSelfRoleIDs.Contains(roleID))
                        {
                            memberSelfRoleIDs.Add(roleID);
                        }
                    }
                    else
                    {
                        memberSelfRoleIDs.Remove(roleID);
                    }
                }

                Plugin.PluginLog.Debug($"HandleSelfRoleAssignmentResponse: groupID={groupID}, roleID={roleID}, assigned={assigned}, success={success}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleSelfRoleAssignmentResponse error: {ex.Message}");
            }
        }

        public static void HandleRoleChannelPermissionsResponse(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                int roleID = buffer.ReadInt();
                bool success = buffer.ReadBool();

                selfRoleOperationSuccess = success;
                selfRoleOperationMessage = success ? "Channel permissions saved" : "Failed to save channel permissions";

                Plugin.PluginLog.Debug($"HandleRoleChannelPermissionsResponse: groupID={groupID}, roleID={roleID}, success={success}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleRoleChannelPermissionsResponse error: {ex.Message}");
            }
        }

        public static void HandleMemberSelfRoles(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                int count = buffer.ReadInt();

                memberSelfRoleIDs.Clear();

                for (int i = 0; i < count; i++)
                {
                    memberSelfRoleIDs.Add(buffer.ReadInt());
                }

                Plugin.PluginLog.Debug($"HandleMemberSelfRoles: groupID={groupID}, roleCount={count}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleMemberSelfRoles error: {ex.Message}");
            }
        }

        public static void HandleRoleSections(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int groupID = buffer.ReadInt();
                int sectionCount = buffer.ReadInt();

                roleSections.Clear();

                for (int i = 0; i < sectionCount; i++)
                {
                    var section = new GroupRoleSection
                    {
                        id = buffer.ReadInt(),
                        groupID = groupID,
                        name = buffer.ReadString(),
                        sortOrder = buffer.ReadInt()
                    };
                    roleSections.Add(section);
                }

                Plugin.PluginLog.Debug($"HandleRoleSections: groupID={groupID}, sectionCount={sectionCount}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleRoleSections error: {ex.Message}");
            }
        }
    }
}
