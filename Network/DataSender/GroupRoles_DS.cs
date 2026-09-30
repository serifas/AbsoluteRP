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
    // Group rules and self-assign role requests. Split out of DataSender.
    internal class GroupRoles_DS
    {
        public static async void SaveGroupRules(Character character, int groupID, string rulesContent)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.SaveGroupRules);
                buffer.WriteString($"{character.characterName}@{character.characterWorld}");
                buffer.WriteInt(groupID);
                buffer.WriteString(rulesContent ?? "");
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"SaveGroupRules error: {ex.Message}");
            }
        }

        public static async void AgreeToGroupRules(Character character, int groupID, int rulesVersion)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.AgreeToGroupRules);
                buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(groupID);
                buffer.WriteInt(rulesVersion);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"AgreeToGroupRules error: {ex.Message}");
            }
        }

        public static async void FetchGroupRules(Character character, int groupID)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.FetchGroupRules);
                buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(groupID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"FetchGroupRules error: {ex.Message}");
            }
        }

        public static async void CreateSelfAssignRole(Character character, int groupID, string name, string color, string description, int sectionID = 0)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.CreateSelfAssignRole);
                buffer.WriteString($"{character.characterName}@{character.characterWorld}");
                buffer.WriteInt(groupID);
                buffer.WriteString(name);
                buffer.WriteString(color ?? "#FFFFFF");
                buffer.WriteString(description ?? "");
                buffer.WriteInt(sectionID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"CreateSelfAssignRole error: {ex.Message}");
            }
        }

        public static async void UpdateSelfAssignRole(Character character, int groupID, int roleID, string name, string color, string description, int sectionID = 0)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.UpdateSelfAssignRole);
                buffer.WriteString($"{character.characterName}@{character.characterWorld}");
                buffer.WriteInt(groupID);
                buffer.WriteInt(roleID);
                buffer.WriteString(name);
                buffer.WriteString(color ?? "#FFFFFF");
                buffer.WriteString(description ?? "");
                buffer.WriteInt(sectionID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"UpdateSelfAssignRole error: {ex.Message}");
            }
        }

        public static async void DeleteSelfAssignRole(Character character, int groupID, int roleID)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.DeleteSelfAssignRole);
                buffer.WriteString($"{character.characterName}@{character.characterWorld}");
                buffer.WriteInt(groupID);
                buffer.WriteInt(roleID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"DeleteSelfAssignRole error: {ex.Message}");
            }
        }

        public static async void FetchSelfAssignRoles(Character character, int groupID)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.FetchSelfAssignRoles);
                buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(groupID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"FetchSelfAssignRoles error: {ex.Message}");
            }
        }

        public static async void AssignSelfRole(Character character, int groupID, int roleID)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.AssignSelfRole);
                buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(groupID);
                buffer.WriteInt(roleID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"AssignSelfRole error: {ex.Message}");
            }
        }

        public static async void UnassignSelfRole(Character character, int groupID, int roleID)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.UnassignSelfRole);
                buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(groupID);
                buffer.WriteInt(roleID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"UnassignSelfRole error: {ex.Message}");
            }
        }

        public static async void SaveRoleChannelPermissions(Character character, int groupID, int roleID, List<GroupChannelRolePermission> permissions)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.SaveRoleChannelPermissions);
                buffer.WriteString($"{character.characterName}@{character.characterWorld}");
                buffer.WriteInt(groupID);
                buffer.WriteInt(roleID);
                buffer.WriteInt(permissions?.Count ?? 0);

                if (permissions != null)
                {
                    foreach (var perm in permissions)
                    {
                        buffer.WriteInt(perm.channelID);
                        buffer.WriteBool(perm.canView);
                        buffer.WriteBool(perm.canPost);
                    }
                }

                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"SaveRoleChannelPermissions error: {ex.Message}");
            }
        }

        public static async void FetchMemberSelfRoles(Character character, int groupID)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.FetchMemberSelfRoles);
                buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(groupID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"FetchMemberSelfRoles error: {ex.Message}");
            }
        }

        public static async void CreateRoleSection(Character character, int groupID, string name)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.CreateRoleSection);
                buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(groupID);
                buffer.WriteString(name);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"CreateRoleSection error: {ex.Message}");
            }
        }

        public static async void DeleteRoleSection(Character character, int groupID, int sectionID)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.DeleteRoleSection);
                buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(groupID);
                buffer.WriteInt(sectionID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"DeleteRoleSection error: {ex.Message}");
            }
        }

        public static async void FetchRoleSections(Character character, int groupID)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.FetchRoleSections);
                buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(groupID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"FetchRoleSections error: {ex.Message}");
            }
        }
    }
}
