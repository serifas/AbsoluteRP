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
    // Trade session packets. Split out of DataReceiver.
    internal class Trades_DR
    {
        internal static void ReceiveTradeRequest(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int profileID = buffer.ReadInt();
                    string requesterProfileName = buffer.ReadString();
                    string receiverProfileName = buffer.ReadString();
                    string requesterCharacterName = buffer.ReadString();
                    string requesterCharacterWorld = buffer.ReadString();
                    string receiverCharacterName = buffer.ReadString();
                    string receiverCharacterWorld = buffer.ReadString();
                    TradeWindow.tradeTargetName = receiverCharacterName;
                    TradeWindow.tradeTargetWorld = receiverCharacterWorld;
                    int inventoryTabCount = buffer.ReadInt();
                    TradeWindow.inventoryTabs.Clear();
                    for (int i = 0; i < inventoryTabCount; i++)
                    {
                        int index = buffer.ReadInt();
                        int id = buffer.ReadInt();
                        string tabName = buffer.ReadString();
                        Tuple<int, int, string> tab = Tuple.Create(index, id, tabName);

                        TradeWindow.inventoryTabs.Add(tab);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveConnectionsRequest message: {ex}");
            }
            finally
            {
                Plugin.plugin.OpenTradeWindow();
                // Auto-select first inventory tab so the server knows where to send/receive items
                if (TradeWindow.inventoryTabs.Count > 0 && Plugin.character != null)
                {
                    var firstTab = TradeWindow.inventoryTabs[0];
                    ProfileTabs_DS.SendInventorySelection(Plugin.character, firstTab.Item1, firstTab.Item2);
                }
            }
        }

        internal static void ReceiveTradeInventory(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int inventoryID = buffer.ReadInt();
                    int inventoryCount = buffer.ReadInt();
                    // Only clear the inventory contents - preserve trade sending/receiving slots
                    TradeWindow.inventoryLayout.inventorySlotContents.Clear();
                    for (int i = 0; i < inventoryCount; i++)
                    {
                        string itemName = buffer.ReadString();
                        string itemDescription = buffer.ReadString();
                        int itemType = buffer.ReadInt();
                        int itemSubType = buffer.ReadInt();
                        int iconID = buffer.ReadInt(); // Ensure iconID is valid
                        int slotID = buffer.ReadInt();
                        int quality = buffer.ReadInt();
                        bool itemLocked = buffer.ReadBool();
                        ItemDefinition itemDefinition = new ItemDefinition
                        {
                            name = itemName,
                            description = itemDescription,
                            type = itemType,
                            subtype = itemSubType,
                            iconID = iconID,
                            slot = slotID,
                            quality = quality,
                            locked = itemLocked
                        };
                        TradeWindow.inventoryLayout.inventorySlotContents.Add(slotID, itemDefinition);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveConnectionsRequest message: {ex}");
            }
            finally
            {
                Plugin.plugin.OpenTradeWindow();
            }
        }

        internal static void ReceiveTradeUpdate(byte[] data)
        {
            try
            {
                Dictionary<int, ItemDefinition> traderItems = new Dictionary<int, ItemDefinition>();
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int profileID = buffer.ReadInt();
                    int itemCount = buffer.ReadInt();

                    for (int i = 0; i < itemCount; i++)
                    {
                        string name = buffer.ReadString();
                        string description = buffer.ReadString();
                        int type = buffer.ReadInt();
                        int subtype = buffer.ReadInt();
                        int iconID = buffer.ReadInt();
                        int slot = buffer.ReadInt();
                        int quality = buffer.ReadInt();
                        bool itemLocked = buffer.ReadBool();

                        ItemDefinition itemDefinition = new ItemDefinition
                        {
                            name = name,
                            description = description,
                            type = type,
                            subtype = subtype,
                            iconID = iconID,
                            slot = slot,
                            quality = quality,
                            locked = itemLocked
                        };
                        traderItems[slot] = itemDefinition;
                    }

                    // Update the active trade window's InventoryLayout
                    if (TradeWindow.inventoryLayout != null)
                    {
                        TradeWindow.inventoryLayout.traderSlotContents = traderItems;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveTradeUpdate message: {ex}");
            }
            finally
            {
                Plugin.plugin.OpenTradeWindow();
            }
        }

        internal static void ReceiveTradeStatus(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    bool senderStatus = buffer.ReadBool();
                    bool receiverStatus = buffer.ReadBool();
                    TradeWindow.receiverReady = receiverStatus;
                    TradeWindow.senderReady = senderStatus;

                    // Update status text
                    TradeWindow.senderStatus = senderStatus ? "Ready" : "Awaiting Confirmation...";
                    TradeWindow.receiverStatus = receiverStatus ? "Ready" : "Awaiting Confirmation...";

                    // Trade complete (both confirmed) or cancelled (both false)
                    if ((senderStatus && receiverStatus) || (!senderStatus && !receiverStatus))
                    {
                        // Clear all trade slots - items have been transferred by the server
                        TradeWindow.inventoryLayout.tradeSlotContents.Clear();
                        TradeWindow.inventoryLayout.traderSlotContents.Clear();
                        Plugin.plugin.CloseTradeWindow();

                        // Re-fetch inventory to reflect the updated items
                        if (senderStatus && receiverStatus && Plugin.character != null)
                        {
                            Profiles_DS.FetchProfile(Plugin.character, true, ProfilesPage.profileIndex,
                                Plugin.plugin.playername, Plugin.plugin.playerworld, -1);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveTradeStatus message: {ex}");
            }
        }
    }
}
