using AbsoluteRP;
using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI;
using AbsoluteRP.Windows.Inventory;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Networking;
using System.Numerics;
using AbsoluteRP.RsUI.Pages;
using AbsoluteRP.Network;

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

        // Cached option lists for the RsElements dropdowns (built once).
        private static List<string>? typeNames;
        private static List<string?>? typeTooltips;
        private static List<string>? qualityNames;

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

        private static void EnsureOptionLists()
        {
            if (typeNames == null)
            {
                typeNames = new List<string>();
                typeTooltips = new List<string?>();
                foreach (var (text, desc, _) in Items.InventoryTypes)
                {
                    typeNames.Add(text);
                    typeTooltips.Add(string.IsNullOrEmpty(desc) ? null : desc);
                }
            }
            qualityNames ??= new List<string>(Items.ItemQualityTypes);
        }

        private static int OccupiedCount(InventoryLayout layout)
        {
            int n = 0;
            foreach (var kv in layout.inventorySlotContents)
                if (InvUI.IsOccupied(kv.Value)) n++;
            return n;
        }

        public static async Task LoadInventoryTabAsync(Plugin plugin, InventoryLayout layout)
        {
            if (isEditingItem)
            {
                DrawItemEditor(plugin, layout);
            }
            else if (itemCreation)
            {
                LoadItemCreation(plugin, layout);
            }
            else
            {
                // Toolbar: create button + slot usage.
                if (RsElements.Button("Create Item##inv_create_btn", RsElements.ButtonVariant.Primary))
                {
                    itemCreation = true;
                }
                ImGui.SameLine();
                var lineH = ImGui.GetItemRectSize().Y;
                var textH = ImGui.GetTextLineHeight();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + Math.Max(0f, (lineH - textH) * 0.5f));
                InvUI.Muted($"{OccupiedCount(layout)} / {TotalSlots} slots");
                InvUI.Gap(6f);

                InvGrid.DrawGrid(plugin, layout, string.Empty, string.Empty, false);
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

        // Icon preview tile with the "change icon" button beside it.
        private static bool DrawIconPreview(string id, IDalamudTextureWrap? tex, int quality, string buttonLabel)
        {
            var sz = InvUI.S(64f);
            var start = ImGui.GetCursorScreenPos();
            var draw = ImGui.GetWindowDrawList();
            InvUI.DrawTile(draw, start, start + new Vector2(sz, sz), tex, quality, false, false, 1f);
            if (tex == null || tex.Handle == IntPtr.Zero)
            {
                using (RsIcons.Push())
                {
                    var glyph = FontAwesomeIcon.Image.ToIconString();
                    var g = ImGui.CalcTextSize(glyph);
                    draw.AddText(start + (new Vector2(sz, sz) - g) * 0.5f, RsTheme.U.TextMuted, glyph);
                }
            }
            ImGui.Dummy(new Vector2(sz, sz));
            ImGui.SameLine();
            bool clicked;
            ImGui.BeginGroup();
            try
            {
                ImGui.Dummy(new Vector2(0f, InvUI.S(6f)));
                clicked = RsElements.Button(buttonLabel + "##" + id, RsElements.ButtonVariant.Secondary);
                InvUI.Muted("Pick any game icon for this item.");
            }
            finally { ImGui.EndGroup(); }
            return clicked;
        }

        // Shared icon browser wrapper (the browser itself is a stock ImGui widget).
        private static bool DrawIconBrowser(Plugin plugin, ref IDalamudTextureWrap iconRef, string doneId)
        {
            if (!WindowOperations.iconsLoaded)
            {
                WindowOperations.LoadIconsLazy(plugin);
            }
            InvUI.SectionLabel("Choose an icon");
            InvUI.PushFrameStyle();
            try
            {
                WindowOperations.RenderIcons(plugin, true, false, null, null, ref iconRef);
            }
            finally { InvUI.PopFrameStyle(); }
            InvUI.Gap(6f);
            return RsElements.Button("Use this icon##" + doneId, RsElements.ButtonVariant.Primary);
        }

        private static void DrawItemEditor(Plugin plugin, InventoryLayout layout)
        {
            if (!RsElements.BeginPanel("inv_edit_" + layout.id, "Edit item", fitContentsX: false, fitContentsY: true))
            {
                RsElements.EndPanel();
                return;
            }
            try
            {
                if (!editIconBrowserOpen)
                {
                    if (DrawIconPreview("edit_icon", editItemIcon, editItemQuality, "Change icon"))
                    {
                        editIconBrowserOpen = true;
                    }
                    InvUI.Gap(4f);

                    InvUI.SectionLabel("Name");
                    RsElements.InputText("inv_edit_name", ref editItemName, 100, "Item name");
                    InvUI.SectionLabel("Description");
                    RsElements.InputTextArea("inv_edit_desc", ref editItemDescription, 5000, "Describe the item…",
                        new Vector2(Math.Max(InvUI.S(120f), RsElements.AvailContentWidth()), ImGui.GetTextLineHeight() * 6f + InvUI.S(16f)));

                    InvUI.Gap(8f);
                    if (RsElements.Button("Save Changes##inv_edit_save", RsElements.ButtonVariant.Primary))
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
                            ProfileTabs_DS.SendItemOrder(Plugin.character, AbsoluteRP.RsUI.Pages.ProfilesPage.profileIndex, layout, newItemList);
                        }
                        isEditingItem = false;
                    }
                    ImGui.SameLine();
                    if (RsElements.Button("Cancel##inv_edit_cancel", RsElements.ButtonVariant.Ghost))
                    {
                        isEditingItem = false;
                    }
                }
                else
                {
                    IDalamudTextureWrap Icon = editItemIcon;
                    if (DrawIconBrowser(plugin, ref Icon, "inv_edit_icon_done"))
                    {
                        editIconBrowserOpen = false;
                    }
                }
            }
            finally { RsElements.EndPanel(); }
        }


        private static void LoadItemCreation(Plugin plugin, InventoryLayout layout)
        {
            EnsureOptionLists();

            if (!RsElements.BeginPanel("inv_create_" + layout.id, "New item", fitContentsX: false, fitContentsY: true))
            {
                RsElements.EndPanel();
                return;
            }
            try
            {
                if (!isIconBrowserOpen)
                {
                    if (icon == null || icon.Handle == IntPtr.Zero)
                        InvUI.Notice("No icon selected or icon failed to load.", RsTheme.AccentWarning);

                    if (DrawIconPreview("create_icon", icon, selectedItemQuality, "Change icon"))
                    {
                        isIconBrowserOpen = true;
                    }
                    InvUI.Gap(4f);

                    InvUI.SectionLabel("Name");
                    RsElements.InputText("inv_new_name", ref itemName, 100, "Item name");
                    InvUI.SectionLabel("Description");
                    RsElements.InputTextArea("inv_new_desc", ref itemDescription, 5000, "Describe the item…",
                        new Vector2(Math.Max(InvUI.S(120f), RsElements.AvailContentWidth()), ImGui.GetTextLineHeight() * 6f + InvUI.S(16f)));

                    InvUI.SectionLabel("Item type");
                    AddItemCategorySelection(plugin);
                    if (itemSubType != null && itemSubType.Length > 0)
                    {
                        InvUI.SectionLabel("Subtype");
                        AddItemSubtypeSelection(itemSubType);
                    }
                    InvUI.SectionLabel("Quality");
                    AddItemQualitySelection();
                    ImGui.SameLine();
                    var lineH = ImGui.GetItemRectSize().Y;
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + Math.Max(0f, (lineH - ImGui.GetTextLineHeight()) * 0.5f));
                    InvUI.Badge(InvUI.QualityName(selectedItemQuality), InvUI.QualityColor(selectedItemQuality));

                    InvUI.Gap(6f);
                    RsElements.Checkbox("Lock item (cannot be edited after creation)##inv_new_lock", ref createItemLocked);

                    InvUI.Gap(8f);
                    if (RsElements.Button("Create##inv_new_create", RsElements.ButtonVariant.Primary))
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
                        if (firstEmptySlotIndex != -1)
                        {
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
                            ProfileTabs_DS.SendItemCreation(Plugin.character, AbsoluteRP.RsUI.Pages.ProfilesPage.profileIndex, layout.tabIndex, itemName, itemDescription, selectedItemType, selectedSubType, createItemIconID, selectedItemQuality, createItemLocked);

                            // Reset creation fields
                            itemName = string.Empty;
                            itemDescription = string.Empty;
                            createItemLocked = false;
                        }
                    }
                    ImGui.SameLine();
                    if (RsElements.Button("Cancel##inv_new_cancel", RsElements.ButtonVariant.Ghost))
                    {
                        itemCreation = false;
                    }
                }
                else
                {
                    IDalamudTextureWrap Icon = icon;
                    if (DrawIconBrowser(plugin, ref Icon, "inv_new_icon_done"))
                    {
                        isIconBrowserOpen = false;
                    }
                }
            }
            finally { RsElements.EndPanel(); }
        }

        public static void AddItemCategorySelection(Plugin plugin)
        {
            EnsureOptionLists();
            if (selectedItemType < 0 || selectedItemType >= Items.InventoryTypes.Length)
                selectedItemType = 0;
            int idx = selectedItemType;
            if (RsElements.Dropdown("inv_item_type", ref idx, typeNames!, typeTooltips!, 220f))
            {
                if (idx >= 0 && idx < Items.InventoryTypes.Length)
                {
                    selectedItemType = idx;
                    itemSubType = Items.InventoryTypes[idx].Item3;
                }
            }
        }

        public static void AddItemSubtypeSelection(string[] subtype)
        {
            if (selectedSubType >= subtype.Length)
            {
                selectedSubType = 0;
            }
            int idx = selectedSubType;
            if (RsElements.Dropdown("inv_item_subtype", ref idx, subtype, 220f))
            {
                if (idx >= 0 && idx < subtype.Length)
                    selectedSubType = idx;
            }
        }
        public static void AddItemQualitySelection()
        {
            EnsureOptionLists();
            if (selectedItemQuality >= Items.ItemQualityTypes.Length)
            {
                selectedItemQuality = 0;
            }
            int idx = selectedItemQuality;
            if (RsElements.Dropdown("inv_item_quality", ref idx, qualityNames!, 220f))
            {
                if (idx >= 0 && idx < Items.ItemQualityTypes.Length)
                    selectedItemQuality = idx;
            }
        }
    }
}
