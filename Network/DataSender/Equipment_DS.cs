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
    // Equipment requests. Split out of DataSender.
    internal class Equipment_DS
    {
        // Equipment System

        internal static async void SendSaveEquipment(Character character, int profileIndex, Dictionary<int, ItemDefinition> equipment)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SaveEquipment);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(equipment.Count);
                        foreach (var kvp in equipment)
                        {
                            buffer.WriteInt(kvp.Key); // slotIndex
                            buffer.WriteString(kvp.Value.name ?? string.Empty);
                            buffer.WriteString(kvp.Value.description ?? string.Empty);
                            buffer.WriteInt(kvp.Value.type);
                            buffer.WriteInt(kvp.Value.subtype);
                            buffer.WriteInt(kvp.Value.iconID);
                            buffer.WriteInt(kvp.Value.quality);
                            buffer.WriteBool(kvp.Value.locked);
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendSaveEquipment: " + ex.ToString());
                }
            }
        }

        internal static async void SendFetchEquipment(Character character, int profileIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchEquipment);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(profileIndex);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendFetchEquipment: " + ex.ToString());
                }
            }
        }

        internal static async void SendFetchTargetEquipment(Character character, string targetName, string targetWorld)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchTargetEquipment);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteString(targetName);
                        buffer.WriteString(targetWorld);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendFetchTargetEquipment: " + ex.ToString());
                }
            }
        }
    }
}
