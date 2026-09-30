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
    // Group category/channel/forum structure requests. Split out of DataSender.
    internal class GroupChannels_DS
    {
        internal static async void SaveGroupCategories(Character character, int groupID, List<GroupCategory> categories)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SaveGroupCategories);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(categories.Count);
                        foreach (var category in categories)
                        {
                            buffer.WriteInt(category.id);
                            buffer.WriteInt(category.sortOrder);
                            buffer.WriteString(category.name ?? string.Empty);
                            buffer.WriteString(category.description ?? string.Empty);
                            buffer.WriteBool(category.collapsed);

                            // Channels
                            int channelCount = category.channels?.Count ?? 0;
                            buffer.WriteInt(channelCount);
                            if (category.channels != null)
                            {
                                foreach (var channel in category.channels)
                                {
                                    buffer.WriteInt(channel.id);
                                    buffer.WriteInt(channel.index);
                                    buffer.WriteString(channel.name ?? string.Empty);
                                    buffer.WriteString(channel.description ?? string.Empty);
                                    buffer.WriteInt(channel.categoryID);
                                    buffer.WriteInt(channel.channelType);
                                    buffer.WriteBool(channel.everyoneCanView);
                                    buffer.WriteBool(channel.everyoneCanPost);
                                }
                            }
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SaveGroupCategories: " + ex.ToString());
                }
            }
        }

        internal static async void FetchGroupCategories(Character character, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchGroupCategories);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchGroupCategories: " + ex.ToString());
                }
            }
        }

        internal static async void SaveForumStructure(Character character, int groupID, List<GroupForumCategory> categories)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SaveForumStructure);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(categories.Count);

                        foreach (var category in categories)
                        {
                            buffer.WriteInt(category.id);
                            buffer.WriteInt(category.parentCategoryID);
                            buffer.WriteInt(category.categoryIndex);
                            buffer.WriteString(category.name ?? string.Empty);
                            buffer.WriteString(category.description ?? string.Empty);
                            buffer.WriteString(category.icon ?? string.Empty);
                            buffer.WriteBool(category.collapsed);
                            buffer.WriteByte((byte)category.categoryType);
                            buffer.WriteInt(category.sortOrder);

                            // Channels
                            int channelCount = category.channels?.Count ?? 0;
                            buffer.WriteInt(channelCount);
                            if (category.channels != null)
                            {
                                foreach (var channel in category.channels)
                                {
                                    buffer.WriteInt(channel.id);
                                    buffer.WriteInt(channel.parentChannelID);
                                    buffer.WriteInt(channel.channelIndex);
                                    buffer.WriteString(channel.name ?? string.Empty);
                                    buffer.WriteString(channel.description ?? string.Empty);
                                    buffer.WriteByte((byte)channel.channelType);
                                    buffer.WriteBool(channel.isLocked);
                                    buffer.WriteBool(channel.isNSFW);
                                    buffer.WriteInt(channel.sortOrder);

                                    // Subchannels
                                    int subchannelCount = channel.subChannels?.Count ?? 0;
                                    buffer.WriteInt(subchannelCount);
                                    if (channel.subChannels != null)
                                    {
                                        foreach (var subchannel in channel.subChannels)
                                        {
                                            buffer.WriteInt(subchannel.id);
                                            buffer.WriteInt(subchannel.channelIndex);
                                            buffer.WriteString(subchannel.name ?? string.Empty);
                                            buffer.WriteString(subchannel.description ?? string.Empty);
                                            buffer.WriteByte((byte)subchannel.channelType);
                                            buffer.WriteBool(subchannel.isLocked);
                                            buffer.WriteBool(subchannel.isNSFW);
                                            buffer.WriteInt(subchannel.sortOrder);
                                        }
                                    }
                                }
                            }
                        }

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SaveForumStructure: " + ex.ToString());
                }
            }
        }

        internal static async void SaveForumPermissions(Character character, int groupID, List<GroupForumChannelPermission> permissions)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SaveForumPermissions);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(permissions.Count);

                        foreach (var perm in permissions)
                        {
                            buffer.WriteInt(perm.channelID);
                            buffer.WriteInt(perm.rankID);
                            buffer.WriteInt(perm.userID);
                            buffer.WriteBool(perm.canView);
                            buffer.WriteBool(perm.canPost);
                            buffer.WriteBool(perm.canReply);
                            buffer.WriteBool(perm.canCreateThreads);
                            buffer.WriteBool(perm.canEditOwn);
                            buffer.WriteBool(perm.canDeleteOwn);
                            buffer.WriteBool(perm.canManage);
                            buffer.WriteBool(perm.canPin);
                            buffer.WriteBool(perm.canLock);
                        }

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SaveForumPermissions: " + ex.ToString());
                }
            }
        }

        internal static async void FetchForumStructure(Character character, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchForumStructure);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchForumStructure: " + ex.ToString());
                }
            }
        }

        internal static async void FetchForumPermissions(Character character, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchForumPermissions);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchForumPermissions: " + ex.ToString());
                }
            }
        }

        internal static async void RenameCategory(Character character, int groupID, int categoryID, string newName)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.RenameCategory);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(categoryID);
                        buffer.WriteString(newName);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in RenameCategory: " + ex.ToString());
                }
            }
        }

        internal static async void DeleteCategory(Character character, int groupID, int categoryID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.DeleteCategory);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(categoryID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in DeleteCategory: " + ex.ToString());
                }
            }
        }

        internal static async void ReorderCategory(Character character, int groupID, int categoryID, int newIndex)
        {
            Plugin.PluginLog.Information($"[GroupChannels_DS.ReorderCategory] Called with groupID={groupID}, categoryID={categoryID}, newIndex={newIndex}, connected={ClientTCP.IsConnected()}");
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.ReorderCategory);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(categoryID);
                        buffer.WriteInt(newIndex);
                        Plugin.PluginLog.Information($"[GroupChannels_DS.ReorderCategory] Sending packet {(int)ClientPackets.ReorderCategory}");
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                        Plugin.PluginLog.Information($"[GroupChannels_DS.ReorderCategory] Packet sent successfully");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in ReorderCategory: " + ex.ToString());
                }
            }
            else
            {
                Plugin.PluginLog.Warning("[GroupChannels_DS.ReorderCategory] Not connected to server!");
            }
        }

        internal static async void RenameChannel(Character character, int groupID, int channelID, string newName)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.RenameChannel);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(channelID);
                        buffer.WriteString(newName);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in RenameChannel: " + ex.ToString());
                }
            }
        }

        internal static async void DeleteChannel(Character character, int groupID, int channelID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.DeleteChannel);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(channelID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in DeleteChannel: " + ex.ToString());
                }
            }
        }

        internal static async void MoveChannel(Character character, int groupID, int channelID, int newCategoryID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.MoveChannel);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(channelID);
                        buffer.WriteInt(newCategoryID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in MoveChannel: " + ex.ToString());
                }
            }
        }

        internal static async void ReorderChannel(Character character, int groupID, int channelID, int newIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.ReorderChannel);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(channelID);
                        buffer.WriteInt(newIndex);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in ReorderChannel: " + ex.ToString());
                }
            }
        }

        internal static async void CreateCategory(Character character, int groupID, string name, string description = "")
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CreateCategory);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteString(name);
                        buffer.WriteString(description);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in CreateCategory: " + ex.ToString());
                }
            }
        }

        internal static async void CreateChannel(Character character, int groupID, int categoryID, string name, string description = "", int channelType = 0)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CreateChannel);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(categoryID);
                        buffer.WriteString(name);
                        buffer.WriteString(description);
                        buffer.WriteInt(channelType);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in CreateChannel: " + ex.ToString());
                }
            }
        }

        internal static async void CreateChannelWithPermissions(Character character, int groupID, int categoryID, string name, string description, int channelType, bool isNsfw, bool everyoneCanView, bool everyoneCanPost, List<GroupsData.ChannelPermissionEntry> memberPermissions, List<GroupsData.ChannelPermissionEntry> rankPermissions, List<GroupsData.ChannelPermissionEntry> rolePermissions = null)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CreateChannelWithPermissions);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(categoryID);
                        buffer.WriteString(name);
                        buffer.WriteString(description ?? "");
                        buffer.WriteInt(channelType);
                        buffer.WriteBool(isNsfw);
                        buffer.WriteBool(everyoneCanView);
                        buffer.WriteBool(everyoneCanPost);

                        // Write member permissions with individual canView/canPost flags
                        buffer.WriteInt(memberPermissions?.Count ?? 0);
                        if (memberPermissions != null)
                        {
                            foreach (var perm in memberPermissions)
                            {
                                buffer.WriteInt(perm.id);
                                buffer.WriteBool(perm.canView);
                                buffer.WriteBool(perm.canPost);
                            }
                        }

                        // Write rank permissions with individual canView/canPost flags
                        buffer.WriteInt(rankPermissions?.Count ?? 0);
                        if (rankPermissions != null)
                        {
                            foreach (var perm in rankPermissions)
                            {
                                buffer.WriteInt(perm.id);
                                buffer.WriteBool(perm.canView);
                                buffer.WriteBool(perm.canPost);
                            }
                        }

                        // Write self-assign role permissions
                        buffer.WriteInt(rolePermissions?.Count ?? 0);
                        if (rolePermissions != null)
                        {
                            foreach (var perm in rolePermissions)
                            {
                                buffer.WriteInt(perm.id);
                                buffer.WriteBool(perm.canView);
                                buffer.WriteBool(perm.canPost);
                            }
                        }

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in CreateChannelWithPermissions: " + ex.ToString());
                }
            }
        }

        internal static async void UpdateChannelWithPermissions(Character character, int groupID, int channelID, string name, string description, int channelType, bool isNsfw, bool everyoneCanView, bool everyoneCanPost, List<GroupsData.ChannelPermissionEntry> memberPermissions, List<GroupsData.ChannelPermissionEntry> rankPermissions, List<GroupsData.ChannelPermissionEntry> rolePermissions = null)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.UpdateChannelWithPermissions);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(channelID);
                        buffer.WriteString(name);
                        buffer.WriteString(description ?? "");
                        buffer.WriteInt(channelType);
                        buffer.WriteBool(isNsfw);
                        buffer.WriteBool(everyoneCanView);
                        buffer.WriteBool(everyoneCanPost);

                        // Write member permissions with individual canView/canPost flags
                        buffer.WriteInt(memberPermissions?.Count ?? 0);
                        if (memberPermissions != null)
                        {
                            foreach (var perm in memberPermissions)
                            {
                                buffer.WriteInt(perm.id);
                                buffer.WriteBool(perm.canView);
                                buffer.WriteBool(perm.canPost);
                            }
                        }

                        // Write rank permissions with individual canView/canPost flags
                        buffer.WriteInt(rankPermissions?.Count ?? 0);
                        if (rankPermissions != null)
                        {
                            foreach (var perm in rankPermissions)
                            {
                                buffer.WriteInt(perm.id);
                                buffer.WriteBool(perm.canView);
                                buffer.WriteBool(perm.canPost);
                            }
                        }

                        // Write self-assign role permissions
                        buffer.WriteInt(rolePermissions?.Count ?? 0);
                        if (rolePermissions != null)
                        {
                            foreach (var perm in rolePermissions)
                            {
                                buffer.WriteInt(perm.id);
                                buffer.WriteBool(perm.canView);
                                buffer.WriteBool(perm.canPost);
                            }
                        }

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in UpdateChannelWithPermissions: " + ex.ToString());
                }
            }
        }
    }
}
