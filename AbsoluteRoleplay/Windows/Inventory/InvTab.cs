using AbsoluteRoleplay;
using AbsoluteRoleplay.Defines;
using AbsoluteRoleplay.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace InventoryTab
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
        public static Dictionary<int, ItemDefinition> consumeableSlotContents = new(); // Slot contents, indexed by slot number
        public static Dictionary<int, ItemDefinition> questSlotContents = new(); // Slot contents, indexed by slot number
        public static Dictionary<int, ItemDefinition> armorSlotContents = new(); // Slot contents, indexed by slot number
        public static Dictionary<int, ItemDefinition> weaponSlotContents = new(); // Slot contents, indexed by slot number
        public static Dictionary<int, ItemDefinition> containerSlotContents = new(); // Slot contents, indexed by slot number
        public static Dictionary<int, ItemDefinition> scriptSlotContents = new(); // Slot contents, indexed by slot number
        public static Dictionary<int, ItemDefinition> keySlotContents = new(); // Slot contents, indexed by slot number
        public static List<Dictionary<int, ItemDefinition>> inventorySlotContents = new List<Dictionary<int, ItemDefinition>>();
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
            inventorySlotContents = new List<Dictionary<int, ItemDefinition>> { consumeableSlotContents, questSlotContents, armorSlotContents, weaponSlotContents, containerSlotContents, scriptSlotContents, keySlotContents };

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
                    inventorySlotContents[j][i] = new ItemDefinition
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

        public static async Task LoadInventoryTabAsync(Plugin plugin, InventoryLayout layout)
        {
            if (ImGui.Button("CreateItem"))
            {
                itemCreation = true;
            }
            if (itemCreation == true)
            {
                LoadItemCreation(plugin, layout);
            }
            else
            {
                ItemGrid.DrawGrid(plugin, layout, string.Empty, string.Empty, false);
            }
        }


        private static void LoadItemCreation(Plugin plugin, InventoryLayout layout)
        {
            if (icon != null && icon.Handle != null)
            {
                ImGui.Image(icon.Handle, new Vector2(50, 50));
            }
            else
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "No icon selected or icon failed to load.");
            }

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
                    // Find the first empty slot (not present or has empty name)
                    int firstEmptySlotIndex = -1;
                    for (int i = 0; i < TotalSlots; i++)
                    {
                        if (!layout.inventorySlotContents.ContainsKey(i) || string.IsNullOrEmpty(layout.inventorySlotContents[i].name))
                        {
                            firstEmptySlotIndex = i;
                            break;
                        }
                    }
                    if (firstEmptySlotIndex == -1)
                        return; // No empty slot found

                    layout.inventorySlotContents[firstEmptySlotIndex] = new ItemDefinition
                    {
                        name = itemName,
                        description = itemDescription,
                        type = selectedItemType,
                        subtype = selectedSubType,
                        iconID = (int)createItemIconID,
                        slot = firstEmptySlotIndex,
                        quality = selectedItemQuality,
                        iconTexture = icon
                    };
                }
            }
            else
            {
                // Render icon browser
                if (!WindowOperations.iconsLoaded)
                {
                    WindowOperations.LoadIconsLazy(plugin);
                }
                IDalamudTextureWrap Icon = icon;
                WindowOperations.RenderIcons(plugin, true, false, null, null, ref Icon);

                // Add a close button for the browser
                if (ImGui.Button("Close Icon Browser"))
                {
                    isIconBrowserOpen = false;
                }
            }
        }

        public static void AddItemCategorySelection(Plugin plugin)
        {
            var (text, desc, subType) = Items.InventoryTypes[selectedItemType];
            using var combo = ImRaii.Combo("##ItemCategory", text);
            if (!combo)
                return;

            foreach (var ((newText, newDesc, newSubtype), idx) in Items.InventoryTypes.WithIndex())
            {
                if (ImGui.Selectable(newText, idx == selectedItemType))
                {
                    selectedItemType = idx;
                    itemSubType = Items.InventoryTypes[idx].Item3;
                }
                ImGuiHelpers.SelectableHelpMarker(newDesc);
            }
        }

        public static void AddItemSubtypeSelection(string[] subtype)
        {
            if (selectedSubType >= subtype.Length)
            {
                selectedSubType = 0;
            }
            var text = subtype[selectedSubType];
            using var combo = ImRaii.Combo("##ItemSubtype", text);
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
            using var combo = ImRaii.Combo("##ItemQualityType", text);
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
