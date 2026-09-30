using AbsoluteRP;
using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI.Pages;
using AbsoluteRP.Windows.Moderator;
using AbsoluteRP.Windows.Profiles;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Social.Views;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Numerics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Lumina.Data.Parsing.Layer.LayerCommon;
using Networking;

namespace AbsoluteRP.Network
{
    // Group, membership, rank and invite requests. Split out of DataSender.
    internal class Groups_DS
    {
        // Group Management Methods. These methods handle creating, updating, and managing groups, channels, ranks, members, invites, forums, and chat within the group system.

        // Creates or updates a group's core settings (name, visibility, logo, invite policy)
        internal static async void SetGroupValues(Character character, Group group, bool update, int leaderProfileIndex, int groupProfileIndex)
        {
            if (group == null) return;
            if (!ClientTCP.IsConnected()) return;

            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.SaveGroup);

                    // defensive guards to avoid NullReferenceException when fields are null
                    var accountKey = DataSender.plugin?.Configuration?.account?.accountKey ?? string.Empty;
                    var charKey = character?.characterKey ?? string.Empty;
                    buffer.WriteString(accountKey);
                    buffer.WriteString(charKey);

                    buffer.WriteBool(update);
                    buffer.WriteInt(group.groupID);
                    buffer.WriteString(group.name ?? string.Empty);
                    buffer.WriteString(group.description ?? string.Empty);

                    var logoBytes = group.logoBytes ?? Array.Empty<byte>();
                    buffer.WriteInt(logoBytes.Length);
                    if (logoBytes.Length > 0)
                        buffer.WriteBytes(logoBytes);

                    var backgroundBytes = group.backgroundBytes ?? Array.Empty<byte>();
                    buffer.WriteInt(backgroundBytes.Length);
                    if (backgroundBytes.Length > 0)
                        buffer.WriteBytes(backgroundBytes);

                    buffer.WriteBool(group.visible);
                    buffer.WriteBool(group.openInvite);
                    buffer.WriteInt(groupProfileIndex);
                    buffer.WriteInt(leaderProfileIndex);

                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("Debug in SaveGroup: " + ex.ToString());
            }
        }

        internal static async void FetchGroups(Character character)
        {
            if (character == null) return;
            if (!ClientTCP.IsConnected()) return;

            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.FetchGroups);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("Debug in SaveGroup: " + ex.ToString());
            }
        }

        /*
public static async void SendTreeData(int profileIndex, TreeNode rootNode)
{
if (ClientTCP.IsConnected())
{
try
{
using (var buffer = new ByteBuffer())
{
buffer.WriteInt((int)ClientPackets.CSendTreeData);
buffer.WriteString(plugin.Configuration.account.accountKey);
buffer.WriteString(character.characterKey);


buffer.WriteInt(profileIndex);

// Serialize the tree nodes and their related elements
SerializeTreeNodes(buffer, rootNode);

await ClientTCP.SendDataAsync(buffer.ToArray());
}
}
catch (Exception ex)
{
Plugin.PluginLog.Debug("Debug in SendTreeData: " + ex.ToString());
}
}
}

private static void SerializeTreeNodes(ByteBuffer buffer, TreeNode node)
{
// Write the node's basic information
buffer.WriteString(node.Name);
buffer.WriteBool(node.IsFolder);
buffer.WriteInt(node.ID);
buffer.WriteInt(node.layoutID);

// Write the related element's information if it exists
if (node.relatedElement != null)
{
buffer.WriteBool(true); // Indicates that the related element exists
SerializeLayoutElement(buffer, node.relatedElement);
}
else
{
buffer.WriteBool(false); // No related element
}

// Write the number of children
buffer.WriteInt(node.Children.Count);

// Recursively serialize children
foreach (var child in node.Children)
{
SerializeTreeNodes(buffer, child);
}
}

private static void SerializeLayoutElement(ByteBuffer buffer, LayoutElement element)
{
buffer.WriteInt(element.id);
buffer.WriteString(element.name);
buffer.WriteInt(element.type);
buffer.WriteFloat(element.PosX);
buffer.WriteFloat(element.PosY);
buffer.WriteBool(element.locked);
buffer.WriteBool(element.modifying);
buffer.WriteBool(element.canceled);

// Handle specific element types
if (element is TextElement textElement)
{
buffer.WriteString(textElement.text);
buffer.WriteFloat(textElement.color.X);
buffer.WriteFloat(textElement.color.Y);
buffer.WriteFloat(textElement.color.Z);
buffer.WriteFloat(textElement.color.W);
}
else if (element is ImageElement imageElement)
{
buffer.WriteInt(imageElement.bytes.Length);
buffer.WriteBytes(imageElement.bytes);
buffer.WriteString(imageElement.tooltip);
buffer.WriteFloat(imageElement.width);
buffer.WriteFloat(imageElement.height);
buffer.WriteBool(imageElement.initialized);
buffer.WriteBool(imageElement.proprotionalEditing);
buffer.WriteBool(imageElement.hasTooltip);
buffer.WriteBool(imageElement.maximizable);
}
else if (element is IconElement iconElement)
{
buffer.WriteInt((int)iconElement.State);
}
else if (element is FolderElement folderElement)
{
buffer.WriteString(folderElement.text);
}
else if (element is EmptyElement emptyElement)
{
buffer.WriteString(emptyElement.text);
buffer.WriteFloat(emptyElement.color.X);
buffer.WriteFloat(emptyElement.color.Y);
buffer.WriteFloat(emptyElement.color.Z);
buffer.WriteFloat(emptyElement.color.W);
}
}*/

        internal static async void SaveGroupRosterFields(Character character, int groupID, List<GroupRosterField> fields)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SaveGroupRosterFields);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(fields.Count);
                        foreach (var field in fields)
                        {
                            buffer.WriteInt(field.id);
                            buffer.WriteInt(field.sortOrder);
                            buffer.WriteString(field.name);
                            buffer.WriteInt(field.fieldType);
                            buffer.WriteBool(field.required);
                            buffer.WriteString(field.dropdownOptions ?? string.Empty);
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SaveGroupRosterFields: " + ex.ToString());
                }
            }
        }

        internal static async void FetchGroupRosterFields(Character character, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchGroupRosterFields);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchGroupRosterFields: " + ex.ToString());
                }
            }
        }

        internal static async void SaveMemberMetadata(Character character, int memberID, GroupMemberMetadata metadata)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SaveMemberMetadata);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteInt(memberID);
                        buffer.WriteLong(metadata.joinDate);
                        buffer.WriteLong(metadata.lastActive);
                        buffer.WriteString(metadata.customTitle ?? string.Empty);
                        buffer.WriteString(metadata.statusMessage ?? string.Empty);
                        buffer.WriteString(metadata.nicknameColor ?? "#FFFFFF");
                        buffer.WriteBool(metadata.isOnline);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SaveMemberMetadata: " + ex.ToString());
                }
            }
        }

        internal static async void FetchMemberMetadata(Character character, int memberID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchMemberMetadata);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteInt(memberID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchMemberMetadata: " + ex.ToString());
                }
            }
        }

        internal static async void SaveMemberFieldValues(Character character, int memberID, List<GroupMemberFieldValue> fieldValues)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SaveMemberFieldValues);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteInt(memberID);
                        buffer.WriteInt(fieldValues.Count);
                        foreach (var fv in fieldValues)
                        {
                            buffer.WriteInt(fv.fieldID);
                            buffer.WriteString(fv.fieldValue ?? string.Empty);
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SaveMemberFieldValues: " + ex.ToString());
                }
            }
        }

        internal static async void FetchMemberFieldValues(Character character, int memberID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchMemberFieldValues);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteInt(memberID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchMemberFieldValues: " + ex.ToString());
                }
            }
        }

        internal static async void SendGroupInvite(Character character, int groupID, string inviteeName, string inviteeWorld, string message)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendGroupInvite);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteString(inviteeName ?? string.Empty);
                        buffer.WriteString(inviteeWorld ?? string.Empty);
                        buffer.WriteString(message ?? string.Empty);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendGroupInvite: " + ex.ToString());
                }
            }
        }

        internal static async void FetchGroupInvites(Character character, bool fetchSentInvites = false, int groupID = -1)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchGroupInvites);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteBool(fetchSentInvites);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchGroupInvites: " + ex.ToString());
                }
            }
        }

        internal static async void RespondToGroupInvite(Character character, int inviteID, bool accept)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.RespondToGroupInvite);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(inviteID);
                        buffer.WriteBool(accept);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in RespondToGroupInvite: " + ex.ToString());
                }
            }
        }

        internal static async void CancelGroupInvite(Character character, int inviteID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CancelGroupInvite);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(inviteID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in CancelGroupInvite: " + ex.ToString());
                }
            }
        }

        internal static async void RequestJoinGroup(Character character, int groupID, string message = "")
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.RequestJoinGroup);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteString(message ?? string.Empty);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in RequestJoinGroup: " + ex.ToString());
                }
            }
        }

        /// Fetches basic group info (name, logo URL) for displaying in embeds.
        internal static async void FetchGroupInfo(int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchGroupInfo);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchGroupInfo: " + ex.ToString());
                }
            }
        }

        /// Fetches basic profile info (name, avatar URL) for displaying in embeds.
        internal static async void FetchProfileInfo(int profileID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchProfileInfo);
                        buffer.WriteInt(profileID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchProfileInfo: " + ex.ToString());
                }
            }
        }

        internal static async void FetchGroupMembers(Character character, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchGroupMembers);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchGroupMembers: " + ex.ToString());
                }
            }
        }

        internal static async void ViewInviteeProfile(Character character, int profileID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.ViewInviteeProfile);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(profileID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in ViewInviteeProfile: " + ex.ToString());
                }
            }
        }

        internal static async void FetchGroupRanks(Character character, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchGroupRanks);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchGroupRanks: " + ex.ToString());
                }
            }
        }

        internal static async void SaveGroupRank(Character character, GroupRank rank)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SaveGroupRank);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(rank.id);
                        buffer.WriteInt(rank.groupID);
                        buffer.WriteString(rank.name);
                        buffer.WriteString(rank.description ?? string.Empty);
                        buffer.WriteInt(rank.hierarchy);
                        buffer.WriteBool(rank.isDefaultMember);

                        // Member Permissions
                        buffer.WriteBool(rank.permissions.canInvite);
                        buffer.WriteBool(rank.permissions.canKick);
                        buffer.WriteBool(rank.permissions.canBan);
                        buffer.WriteBool(rank.permissions.canPromote);
                        buffer.WriteBool(rank.permissions.canDemote);

                        // Message Permissions
                        buffer.WriteBool(rank.permissions.canCreateAnnouncement);
                        buffer.WriteBool(rank.permissions.canReadMessages);
                        buffer.WriteBool(rank.permissions.canSendMessages);
                        buffer.WriteBool(rank.permissions.canDeleteOthersMessages);
                        buffer.WriteBool(rank.permissions.canPinMessages);

                        // Category Permissions
                        buffer.WriteBool(rank.permissions.canCreateCategory);
                        buffer.WriteBool(rank.permissions.canEditCategory);
                        buffer.WriteBool(rank.permissions.canDeleteCategory);
                        buffer.WriteBool(rank.permissions.canLockCategory);

                        // Forum Permissions
                        buffer.WriteBool(rank.permissions.canCreateForum);
                        buffer.WriteBool(rank.permissions.canEditForum);
                        buffer.WriteBool(rank.permissions.canDeleteForum);
                        buffer.WriteBool(rank.permissions.canLockForum);
                        buffer.WriteBool(rank.permissions.canMuteForum);

                        // Rank Management Permissions
                        buffer.WriteBool(rank.permissions.canManageRanks);
                        buffer.WriteBool(rank.permissions.canCreateRanks);

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SaveGroupRank: " + ex.ToString());
                }
            }
        }

        internal static async void DeleteGroupRank(Character character, int rankID, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.DeleteGroupRank);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(rankID);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in DeleteGroupRank: " + ex.ToString());
                }
            }
        }

        internal static async void UpdateRankHierarchies(Character character, int groupID, Dictionary<int, int> rankHierarchies)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.UpdateRankHierarchies);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(rankHierarchies.Count);

                        foreach (var kvp in rankHierarchies)
                        {
                            buffer.WriteInt(kvp.Key);   // rankID
                            buffer.WriteInt(kvp.Value); // new hierarchy value
                        }

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in UpdateRankHierarchies: " + ex.ToString());
                }
            }
        }

        internal static async void AssignMemberRank(Character character, int memberID, int rankID, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.AssignMemberRank);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(memberID);
                        buffer.WriteInt(rankID);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in AssignMemberRank: " + ex.ToString());
                }
            }
        }

        internal static async void RemoveMemberRank(Character character, int memberID, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.RemoveMemberRank);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(memberID);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in RemoveMemberRank: " + ex.ToString());
                }
            }
        }

        internal static async void RemoveSpecificMemberRank(Character character, int memberID, int rankID, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.RemoveSpecificMemberRank);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(memberID);
                        buffer.WriteInt(rankID);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in RemoveSpecificMemberRank: " + ex.ToString());
                }
            }
        }

        internal static async void KickGroupMember(Character character, int memberID, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.KickGroupMember);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(memberID);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in KickGroupMember: " + ex.ToString());
                }
            }
        }

        internal static async void BanGroupMember(Character character, int memberID, int userID, int profileID, string lodestoneURL, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.BanGroupMember);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(memberID);
                        buffer.WriteInt(userID);
                        buffer.WriteInt(profileID);
                        buffer.WriteString(lodestoneURL ?? string.Empty);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in BanGroupMember: " + ex.ToString());
                }
            }
        }

        internal static async void UnbanGroupMember(Character character, int banID, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.UnbanGroupMember);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(banID);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in UnbanGroupMember: " + ex.ToString());
                }
            }
        }

        internal static async void DeleteGroup(Character character, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    Plugin.PluginLog.Info($"[DeleteGroup] Sending delete request for group {groupID}");
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.DeleteGroup);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                        Plugin.PluginLog.Info($"[DeleteGroup] Delete request sent for group {groupID}");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in DeleteGroup: " + ex.ToString());
                }
            }
            else
            {
                Plugin.PluginLog.Warning($"[DeleteGroup] Not connected to server, cannot delete group {groupID}");
            }
        }

        internal static async void LeaveGroup(Character character, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.LeaveGroup);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in LeaveGroup: " + ex.ToString());
                }
            }
        }

        internal static async void TransferGroupOwnership(Character character, int groupID, int newOwnerMemberID, int newOwnerUserID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.TransferGroupOwnership);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(newOwnerMemberID);
                        buffer.WriteInt(newOwnerUserID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in TransferGroupOwnership: " + ex.ToString());
                }
            }
        }

        internal static async void FetchGroupMemberAvatar(Character character, int groupID, int userID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchGroupMemberAvatar);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(userID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                        Plugin.PluginLog.Info($"[FetchGroupMemberAvatar] Requested avatar for user {userID} in group {groupID}");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchGroupMemberAvatar: " + ex.ToString());
                }
            }
        }

        public static async void FetchGroupBans(Character character, int groupID)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.FetchGroupBans);
                buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(groupID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"FetchGroupBans error: {ex.Message}");
            }
        }
    }
}
