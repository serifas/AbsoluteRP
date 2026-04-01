using AbsoluteRP;
using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Networking;
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
    public static bool createItemLocked = false;

    // Edit state
    public static bool isEditingItem = false;
    public static int editingSlotIndex = -1;
    public static string editItemName = string.Empty;
    public static string editItemDescription = string.Empty;
    public static int editItemType = 0;
    public static int editItemSubType = 0;
    public static int editItemQuality = 0;
    public static uint editItemIconID = 0;
    public static IDalamudTextureWrap editItemIcon;
    public static bool editIconBrowserOpen = false;
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
            if (isEditingItem)
            {
                DrawItemEditor(plugin, layout);
            }
            else
            {
                if (ThemeManager.PillButton("CreateItem"))
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
        }

        public static void BeginEditItem(InventoryLayout layout, int slotIndex)
        {
            if (!layout.inventorySlotContents.ContainsKey(slotIndex)) return;
            var item = layout.inventorySlotContents[slotIndex];
            if (item.locked) return; // Cannot edit locked items

            isEditingItem = true;
            editingSlotIndex = slotIndex;
            editItemName = item.name;
            editItemDescription = item.description;
            editItemType = item.type;
            editItemSubType = item.subtype;
            editItemQuality = item.quality;
            editItemIconID = (uint)item.iconID;
            editItemIcon = item.iconTexture;
            editIconBrowserOpen = false;
        }

        private static void DrawItemEditor(Plugin plugin, InventoryLayout layout)
        {
            ThemeManager.SectionHeader("Edit Item");
            ImGui.Spacing();

            if (editItemIcon != null && editItemIcon.Handle != IntPtr.Zero)
            {
                ImGui.Image(editItemIcon.Handle, new Vector2(50, 50));
            }

            if (!editIconBrowserOpen)
            {
                if (ThemeManager.GhostButton("Change Icon"))
                {
                    editIconBrowserOpen = true;
                }
                ImGui.Text("Name:");
                ImGui.InputTextWithHint("##EditName", "Item Name", ref editItemName, 100);
                ImGui.Text("Description:");
                ImGui.InputTextMultiline("##EditDescription", ref editItemDescription, 5000, new Vector2(ImGui.GetWindowSize().X - 20, ImGui.GetWindowSize().Y / 5));

                if (ThemeManager.PillButton("Save Changes"))
                {
                    if (layout.inventorySlotContents.ContainsKey(editingSlotIndex))
                    {
                        var item = layout.inventorySlotContents[editingSlotIndex];
                        item.name = editItemName;
                        item.description = editItemDescription;
                        item.type = editItemType;
                        item.subtype = editItemSubType;
                        item.quality = editItemQuality;
                        item.iconID = (int)editItemIconID;
                        if (editItemIcon != null)
                            item.iconTexture = editItemIcon;

                        // Save to server via sort/resubmit
                        List<ItemDefinition> newItemList = new List<ItemDefinition>();
                        for (int i = 0; i < TotalSlots; i++)
                        {
                            if (layout.inventorySlotContents.ContainsKey(i) && !string.IsNullOrEmpty(layout.inventorySlotContents[i].name))
                            {
                                newItemList.Add(layout.inventorySlotContents[i]);
                            }
                        }
                        DataSender.SendItemOrder(Plugin.character, AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.profileIndex, layout, newItemList);
                    }
                    isEditingItem = false;
                }
                ImGui.SameLine();
                if (ThemeManager.GhostButton("Cancel"))
                {
                    isEditingItem = false;
                }
            }
            else
            {
                if (!WindowOperations.iconsLoaded)
                {
                    WindowOperations.LoadIconsLazy(plugin);
                }
                IDalamudTextureWrap Icon = editItemIcon;
                WindowOperations.RenderIcons(plugin, true, false, null, null, ref Icon);
                if (ThemeManager.GhostButton("Close Icon Browser"))
                {
                    editIconBrowserOpen = false;
                }
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
                if (ThemeManager.GhostButton("Change Icon"))
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
                ImGui.Checkbox("Lock Item (cannot be edited after creation)", ref createItemLocked);
                if (ThemeManager.PillButton("Create"))
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
                        iconTexture = icon,
                        locked = createItemLocked
                    };

                    // Persist to server
                    DataSender.SendItemCreation(Plugin.character, AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.profileIndex, layout.tabIndex, itemName, itemDescription, selectedItemType, selectedSubType, createItemIconID, selectedItemQuality, createItemLocked);

                    // Reset creation fields
                    itemName = string.Empty;
                    itemDescription = string.Empty;
                    createItemLocked = false;
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
                if (ThemeManager.GhostButton("Close Icon Browser"))
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
                UIHelpers.SelectableHelpMarker(newDesc);
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
