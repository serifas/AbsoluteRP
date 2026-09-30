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
    // Public group search and join requests. Split out of DataSender.
    internal class GroupSearch_DS
    {
        /// Sends a join request to a group that is visible but not open for direct joining.
        internal static async void SendJoinRequest(Character character, int groupID, string message)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendJoinRequest);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteString(message ?? string.Empty);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendJoinRequest: " + ex.ToString());
                }
            }
        }

        /// Fetches pending join requests for a group (for members with permission to accept requests).
        internal static async void FetchJoinRequests(Character character, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchJoinRequests);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchJoinRequests: " + ex.ToString());
                }
            }
        }

        /// Responds to a join request (accept or decline). True to accept, false to decline
        internal static async void RespondToJoinRequest(Character character, int requestID, int groupID, bool accept)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.RespondToJoinRequest);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(requestID);
                        buffer.WriteInt(groupID);
                        buffer.WriteBool(accept);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in RespondToJoinRequest: " + ex.ToString());
                }
            }
        }

        /// Cancels a pending join request (by the requester).
        internal static async void CancelJoinRequest(Character character, int requestID, int groupID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CancelJoinRequest);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(requestID);
                        buffer.WriteInt(groupID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in CancelJoinRequest: " + ex.ToString());
                }
            }
        }

        public static async void SearchPublicGroups(Character character, string searchQuery)
        {
            try
            {
                GroupSearch_DR.groupSearchInProgress = true;
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.SearchPublicGroups);
                buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteString(searchQuery ?? "");
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                GroupSearch_DR.groupSearchInProgress = false;
                Plugin.PluginLog.Error($"SearchPublicGroups error: {ex.Message}");
            }
        }
    }
}
