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
    // Group chat requests. Split out of DataSender.
    internal class GroupChat_DS
    {
        internal static async void SendGroupChatMessage(Character character, int groupID, int channelID, string messageContent)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendGroupChatMessage);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(channelID);
                        buffer.WriteString(messageContent);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendGroupChatMessage: " + ex.ToString());
                }
            }
        }

        internal static async void FetchGroupChatMessages(Character character, int groupID, int channelID, int limit = 50, int offset = 0)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchGroupChatMessages);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(channelID);
                        buffer.WriteInt(limit);
                        buffer.WriteInt(offset);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchGroupChatMessages: " + ex.ToString());
                }
            }
        }

        internal static async void EditGroupChatMessage(Character character, int messageID, string newContent)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.EditGroupChatMessage);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(messageID);
                        buffer.WriteString(newContent);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                        Plugin.PluginLog.Info($"[EditGroupChatMessage] Sent edit request for message {messageID}");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in EditGroupChatMessage: " + ex.ToString());
                }
            }
        }

        internal static async void DeleteGroupChatMessage(Character character, int messageID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.DeleteGroupChatMessage);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(messageID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                        Plugin.PluginLog.Info($"[DeleteGroupChatMessage] Sent delete request for message {messageID}");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in DeleteGroupChatMessage: " + ex.ToString());
                }
            }
        }

        internal static async void PinGroupChatMessage(Character character, int messageID, bool pin)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.PinGroupChatMessage);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(messageID);
                        buffer.WriteBool(pin);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                        Plugin.PluginLog.Info($"[PinGroupChatMessage] Sent {(pin ? "pin" : "unpin")} request for message {messageID}");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in PinGroupChatMessage: " + ex.ToString());
                }
            }
        }

        internal static async void FetchPinnedMessages(Character character, int groupID, int channelID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchPinnedMessages);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(channelID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                        Plugin.PluginLog.Info($"[FetchPinnedMessages] Sent request for group {groupID} channel {channelID}");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in FetchPinnedMessages: " + ex.ToString());
                }
            }
        }

        internal static async void LockChannel(Character character, int groupID, int channelID, bool isLocked)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.LockChannel);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(groupID);
                        buffer.WriteInt(channelID);
                        buffer.WriteBool(isLocked);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                        Plugin.PluginLog.Info($"[LockChannel] Sent {(isLocked ? "lock" : "unlock")} request for channel {channelID} in group {groupID}");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Error in LockChannel: " + ex.ToString());
                }
            }
        }

        internal static async void UpdateChatReadStatus(Character character, int channelID, int lastReadMessageID, long timestamp)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.UpdateChatReadStatus);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteInt(channelID);
                        buffer.WriteInt(lastReadMessageID);
                        buffer.WriteLong(timestamp);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in UpdateChatReadStatus: " + ex.ToString());
                }
            }
        }
    }
}
