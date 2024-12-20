using AbsoluteRoleplay.Defines;
using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Windows.Ect;
using AbsoluteRoleplay.Windows.MainPanel.Views.Account;
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

namespace AbsoluteRoleplay.Windows.Profiles.ProfileTabs
{
    internal class InventoryTab
    {
        private const int GridSize = 10; // 10x10 grid for 200 slots
        private const int TotalSlots = GridSize * GridSize;
        public static Dictionary<int, Defines.Item> slotContents = new(); // Slot contents, indexed by slot number
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
            slotContents.Clear();
            for (int i = 0; i < TotalSlots; i++)
            {
                slotContents[i] = new Defines.Item
                {
                    name = string.Empty,
                    description = string.Empty,
                    type = 0,
                    subtype = 0,
                    iconID = 0,
                    slot = i,
                };
            }
          
        }

        public static async Task LoadInventoryTabAsync(Plugin plugin)
        {
            if (ImGui.Button("CreateItem"))
            {
                itemCreation = true;
            }
            if(itemCreation == true)
            {
               LoadItemCreation(plugin);
            }
            else
            {
                ItemGrid.DrawGrid(plugin, slotContents, false);
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
                    DataSender.SendItemCreation(ProfileWindow.currentProfile, itemName, itemDescription, selectedItemType, selectedSubType, createItemIconID, selectedItemQuality);
                }
            }
            else
            {
                if (!WindowOperations.iconsLoaded)
                {
                    WindowOperations.LoadIconsLazy(plugin); // Load a small batch of icons
                }

                WindowOperations.RenderIcons(plugin);
            }
        }

        public static void AddItemCategorySelection(Plugin plugin)
        {
            var (text, desc, subType) = Defines.Items.InventoryTypes[selectedItemType];
            using var combo = OtterGui.Raii.ImRaii.Combo("##ItemCategory", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;

            foreach (var ((newText, newDesc, newSubtype), idx) in Defines.Items.InventoryTypes.WithIndex())
            {
                if (ImGui.Selectable(newText, idx == selectedItemType))
                {
                    selectedItemType = idx;
                    itemSubType = Defines.Items.InventoryTypes[idx].Item3;
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
                if (ImGui.Selectable(val, idx==selectedItemQuality))
                {
                    selectedItemQuality = idx;
                }
            }
        }
    }
}
