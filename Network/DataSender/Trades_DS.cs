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
    // Trade session requests. Split out of DataSender.
    internal class Trades_DS
    {
        internal static async void SendTradeUpdate(Character character, int profileIndex, string targetPlayerName, string targetPlayerWorld, InventoryLayout layout, List<ItemDefinition> slotContents)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendTradeUpdate);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(layout.tabIndex);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteString(targetPlayerName);
                        buffer.WriteString(targetPlayerWorld);
                        buffer.WriteInt(slotContents.Count);
                        for (int i = 0; i < slotContents.Count; i++)
                        {
                            buffer.WriteString(slotContents[i].name);
                            buffer.WriteString(slotContents[i].description);
                            buffer.WriteInt(slotContents[i].type);
                            buffer.WriteInt(slotContents[i].subtype);
                            buffer.WriteInt(slotContents[i].iconID);
                            buffer.WriteInt(slotContents[i].slot);
                            buffer.WriteInt(slotContents[i].quality);
                            buffer.WriteBool(slotContents[i].locked);
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendTradeUpdate: " + ex.ToString());
                }
            }
        }

        internal static async void RequestTargetTrade(Character character, string targetCharName, string targetCharWorld)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendTradeRequest);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteString(targetCharName);
                        buffer.WriteString(targetCharWorld);

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug($"Debug in Request target trade: {ex}");
                }
                finally
                {
                    Plugin.PluginLog.Debug($"Requesting trade with {targetCharName} on {targetCharWorld}");
                }
            }
        }

        internal static async void SendTradeStatus(Character character, int tradeTabIndex, InventoryLayout layout, string targetName, string targetWorld, bool status, bool canceled)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendTradeStatus);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteBool(status); // true if sender is ready, false if not
                        buffer.WriteBool(canceled); // true for cancel, false for not canceled
                        buffer.WriteInt(tradeTabIndex);
                        buffer.WriteInt(layout.tradeSlotContents.Count);
                        buffer.WriteInt(layout.traderSlotContents.Count);

                        foreach (var kvp in layout.tradeSlotContents)
                        {
                            buffer.WriteString(kvp.Value.name);
                            buffer.WriteString(kvp.Value.description);
                            buffer.WriteInt(kvp.Value.type);
                            buffer.WriteInt(kvp.Value.subtype);
                            buffer.WriteInt(kvp.Value.iconID);
                            buffer.WriteInt(kvp.Value.quality);
                            buffer.WriteBool(kvp.Value.locked);
                        }
                        foreach (var kvp in layout.traderSlotContents)
                        {
                            buffer.WriteString(kvp.Value.name);
                            buffer.WriteString(kvp.Value.description);
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
                    Console.WriteLine($"Debug in Request target trade: {ex}");
                }
            }
        }

        internal static async void SendTradeSessionTargetInventory(Character character, int tabIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendTradeSessionTargetInventory);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(tabIndex);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendInventoryTargetSelection: " + ex.ToString());
                }
            }
        }
    }
}
