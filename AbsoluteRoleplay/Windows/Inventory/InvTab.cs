using AbsoluteRoleplay.Defines;
using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Windows.Ect;
using AbsoluteRoleplay.Windows.MainPanel.Views.Account;
using AbsoluteRoleplay.Windows.Profiles;
using Dalamud.Interface;
using Dalamud.Interface.Internal.Windows.Data.Widgets;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Lumina.Data.Files;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using Networking;
using OtterGui;
using OtterGui.Text.EndObjects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AbsoluteRoleplay.Windows.Inventory
{
    public enum InvTabItem
    {
        Consumeable = 0,
        Quest = 1,
        Armor = 2,
        Weapon = 3,
        Material = 4,
        Container = 5,
        Script = 6,
        Key = 7,
    }
    internal class InvTab
    {
        private const int GridSize = 10; // 10x10 grid for 200 slots
        private const int TotalSlots = GridSize * GridSize;
        public static Dictionary<int, Defines.ItemDefinition> consumeableSlotContents = new(); // Slot contents, indexed by slot number
        public static Dictionary<int, Defines.ItemDefinition> questSlotContents = new(); // Slot contents, indexed by slot number
        public static Dictionary<int, Defines.ItemDefinition> armorSlotContents = new(); // Slot contents, indexed by slot number
        public static Dictionary<int, Defines.ItemDefinition> weaponSlotContents = new(); // Slot contents, indexed by slot number
        public static Dictionary<int, Defines.ItemDefinition> containerSlotContents = new(); // Slot contents, indexed by slot number
        public static Dictionary<int, Defines.ItemDefinition> scriptSlotContents = new(); // Slot contents, indexed by slot number
        public static Dictionary<int, Defines.ItemDefinition> keySlotContents = new(); // Slot contents, indexed by slot number
        public static List<Dictionary<int, Defines.ItemDefinition>> inventorySlotContents = new List<Dictionary<int, Defines.ItemDefinition>>();
        public static bool isIconBrowserOpen;
        public static string itemName = string.Empty;
        public static string itemDescription = string.Empty;
        public static IDalamudTextureWrap icon;
        private static bool itemCreation;
        public static int selectedItemType = 0;
        private static string[] itemSubType = Items.InventoryTypes[0].Item3;
        private static int selectedSubType = 0;
        public static uint createItemIconID = 0;
        public static int selectedItemQuality = 0;
        public static void InitInventory()
        {
            icon = UI.UICommonImage(UI.CommonImageTypes.blank);
            if (icon == null)
            {
                throw new InvalidOperationException("Failed to initialize icon.");
            }
            inventorySlotContents = new List<Dictionary<int, Defines.ItemDefinition>> { consumeableSlotContents, questSlotContents, armorSlotContents, weaponSlotContents, containerSlotContents, scriptSlotContents, keySlotContents};
           
            consumeableSlotContents.Clear();
            questSlotContents.Clear();
            armorSlotContents.Clear();
            weaponSlotContents.Clear();
            containerSlotContents.Clear();
            scriptSlotContents.Clear();
            keySlotContents.Clear();
            for (var i = 0; i < TotalSlots; i++)
            {
                for (int j = 0; j < inventorySlotContents.Count; j++)
                {
                    inventorySlotContents[j][i] = new Defines.ItemDefinition
                    {
                        name = string.Empty,
                        description = string.Empty,
                        type = 0,
                        subtype = 0,
                        iconID = 0,
                        slot = i
                    };
                }
              
            }

        }

        public static async Task LoadInventoryTabAsync(Plugin plugin, InventoryWindow.InvTabItem invType)
        {
            if (ImGui.Button("CreateItem"))
            {
                itemCreation = true;
            }
            if (itemCreation == true)
            {
                LoadItemCreation(plugin);
            }
            else
            {
                ItemGrid.DrawGrid(plugin, inventorySlotContents[(int)invType], false);
            }
        }


        private static void LoadItemCreation(Plugin plugin)
        {
            ImGui.Image(icon.ImGuiHandle, new Vector2(50, 50));

            if (!isIconBrowserOpen)
            {
                if (ImGui.Button("Change Icon"))
                {
                    isIconBrowserOpen = true;
                }
                ImGui.Text("Name:");
                ImGui.InputTextWithHint("##Name", "Item Name", ref itemName, 100);
                ImGui.Text("Description:");
                ImGui.InputTextMultiline("##Description", ref itemDescription, 5000, new Vector2(ImGui.GetWindowSize().X - 20, ImGui.GetWindowSize().Y / 5));
                ImGui.Text("Item Type");
                AddItemCategorySelection(plugin);
                if (itemSubType != null)
                {
                    AddItemSubtypeSelection(itemSubType);
                }
                AddItemQualitySelection();
                if (ImGui.Button("Create"))
                {
                    itemCreation = false;
                    DataSender.SendItemCreation(ProfileWindow.currentProfileIndex, itemName, itemDescription, selectedItemType, selectedSubType, createItemIconID, selectedItemQuality);
                }
            }
            else
            {
                if (!WindowOperations.iconsLoaded)
                {
                    WindowOperations.LoadIconsLazy(plugin); // Load a small batch of icons
                }

                WindowOperations.RenderIcons(plugin, true, null);
            }
        }

        public static void AddItemCategorySelection(Plugin plugin)
        {
            var (text, desc, subType) = Items.InventoryTypes[selectedItemType];
            using var combo = OtterGui.Raii.ImRaii.Combo("##ItemCategory", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;

            foreach (var ((newText, newDesc, newSubtype), idx) in Items.InventoryTypes.WithIndex())
            {
                if (ImGui.Selectable(newText, idx == selectedItemType))
                {
                    selectedItemType = idx;
                    itemSubType = Items.InventoryTypes[idx].Item3;
                }
                ImGuiUtil.SelectableHelpMarker(newDesc);
            }
        }

        public static void AddItemSubtypeSelection(string[] subtype)
        {
            if (selectedSubType >= subtype.Length)
            {
                selectedSubType = 0;
            }
            var text = subtype[selectedSubType];
            using var combo = OtterGui.Raii.ImRaii.Combo("##ItemSubtype", text);
            if (!combo)
                return;

            foreach (var (val, idx) in subtype.WithIndex())
            {
                if (ImGui.Selectable(val, idx == selectedSubType))
                {
                    selectedSubType = idx;
                }
            }
        }
        public static void AddItemQualitySelection()
        {
            if (selectedItemQuality >= Items.ItemQualityTypes.Length)
            {
                selectedItemQuality = 0;
            }
            var text = Items.ItemQualityTypes[selectedItemQuality];
            using var combo = OtterGui.Raii.ImRaii.Combo("##ItemQualityType", text);
            if (!combo)
                return;

            foreach (var (val, idx) in Items.ItemQualityTypes.WithIndex())
            {
                if (ImGui.Selectable(val, idx == selectedItemQuality))
                {
                    selectedItemQuality = idx;
                }
            }
        }
    }
}
