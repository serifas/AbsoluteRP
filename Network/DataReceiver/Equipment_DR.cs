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
    // Equipment packets. Split out of DataReceiver.
    internal class Equipment_DR
    {
        // Equipment handlers

        // Receives the current character's equipment loadout
        public static void HandleEquipment(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet id
                    int slotCount = buffer.ReadInt();
                    AbsoluteRP.Windows.Inventory.EquipmentPage.equippedItems.Clear();
                    for (int i = 0; i < slotCount; i++)
                    {
                        int slotIndex = buffer.ReadInt();
                        string name = buffer.ReadString();
                        string description = buffer.ReadString();
                        int type = buffer.ReadInt();
                        int subType = buffer.ReadInt();
                        int iconID = buffer.ReadInt();
                        int quality = buffer.ReadInt();
                        bool locked = buffer.ReadBool();
                        AbsoluteRP.Windows.Inventory.EquipmentPage.equippedItems[slotIndex] = new ItemDefinition
                        {
                            name = name,
                            description = description,
                            type = type,
                            subtype = subType,
                            iconID = iconID,
                            quality = quality,
                            locked = locked,
                            slot = slotIndex,
                        };
                    }
                    Plugin.PluginLog.Info($"[Equipment] Loaded {slotCount} equipped items");
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Error($"HandleEquipment Error: {ex.Message}"); }
        }

        public static void HandleTargetEquipment(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet id
                    int slotCount = buffer.ReadInt();
                    AbsoluteRP.Windows.Inventory.EquipmentPage.targetEquippedItems.Clear();
                    for (int i = 0; i < slotCount; i++)
                    {
                        int slotIndex = buffer.ReadInt();
                        string name = buffer.ReadString();
                        string description = buffer.ReadString();
                        int type = buffer.ReadInt();
                        int subType = buffer.ReadInt();
                        int iconID = buffer.ReadInt();
                        int quality = buffer.ReadInt();
                        bool locked = buffer.ReadBool();
                        AbsoluteRP.Windows.Inventory.EquipmentPage.targetEquippedItems[slotIndex] = new ItemDefinition
                        {
                            name = name,
                            description = description,
                            type = type,
                            subtype = subType,
                            iconID = iconID,
                            quality = quality,
                            locked = locked,
                            slot = slotIndex,
                        };
                    }
                    Plugin.PluginLog.Info($"[Equipment] Loaded {slotCount} target equipped items");
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Error($"HandleTargetEquipment Error: {ex.Message}"); }
        }
    }
}
