using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Networking;
using AbsoluteRP.RsUI.Pages;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;
using AbsoluteRP.Network;
using AbsoluteRP.RsUI;
using Dalamud.Interface;

namespace AbsoluteRP.Windows.Inventory
{
    public static class EquipmentPage
    {
        // Own equipment (editable)
        public static Dictionary<int, ItemDefinition> equippedItems = new Dictionary<int, ItemDefinition>();

        // Target equipment (read-only)
        public static Dictionary<int, ItemDefinition> targetEquippedItems = new Dictionary<int, ItemDefinition>();

        // Icon cache (shared with ItemGrid)
        private static readonly Dictionary<int, IDalamudTextureWrap> IconCache = new();
        private static readonly HashSet<int> LoadingIcons = new();

        // Drag state
        private static int? dragSourceSlot = null;
        private static bool dragFromEquipment = false;

        // Layout constants
        private const float SlotSize = 48f;
        private const float SlotSpacingV = 22f; // Vertical space between slots (room for label text)
        private const float SlotSpacingH = 12f; // Horizontal space between slots

        /// Renders the editable equipment page for own profile.
        public static void RenderEquipmentPage(Plugin plugin, bool editable)
        {
            int frame = ImGui.GetFrameCount();
            if (frame != contextFrame)
            {
                if (!contextOpenThisFrame) contextSlot = -1;
                contextOpenThisFrame = false;
                contextFrame = frame;
            }

            var items = editable ? equippedItems : targetEquippedItems;
            string listKey = editable ? "equip_own" : "equip_target";
            InvUI.Track(listKey, items);

            InvUI.SectionLabel("Equipment");
            InvUI.Divider();

            float slotSize = InvUI.S(SlotSize);
            float windowWidth = Math.Max(InvUI.S(200f), RsElements.AvailContentWidth());
            float slotCellHeight = slotSize + InvUI.S(SlotSpacingV); // Each slot cell: icon + label + gap
            float slotCellWidth = slotSize + InvUI.S(SlotSpacingH);
            float centerGap = windowWidth - slotCellWidth * 2;
            if (centerGap < 60) centerGap = 60;

            // Layout: Left column (5 slots): Head, Body, Hands, Legs, Feet Right column (5 slots): Earring, Necklace, Bracelet, Ring1, Ring2 Bottom row (3 slots): MainHand, OffHand, Soulstone

            Vector2 startPos = ImGui.GetCursorScreenPos();
            var drawList = ImGui.GetWindowDrawList();

            // Left column - Armor
            EquipmentSlot[] leftSlots = { EquipmentSlot.Head, EquipmentSlot.Body, EquipmentSlot.Hands, EquipmentSlot.Legs, EquipmentSlot.Feet };
            for (int i = 0; i < leftSlots.Length; i++)
            {
                Vector2 pos = new Vector2(startPos.X + InvUI.S(SlotSpacingH), startPos.Y + i * slotCellHeight);
                DrawEquipmentSlot(plugin, drawList, pos, leftSlots[i], items, editable, listKey, slotSize);
            }

            // Right column - Accessories
            EquipmentSlot[] rightSlots = { EquipmentSlot.Earring, EquipmentSlot.Necklace, EquipmentSlot.Bracelet, EquipmentSlot.Ring1, EquipmentSlot.Ring2 };
            float rightX = startPos.X + windowWidth - slotSize - InvUI.S(SlotSpacingH);
            for (int i = 0; i < rightSlots.Length; i++)
            {
                Vector2 pos = new Vector2(rightX, startPos.Y + i * slotCellHeight);
                DrawEquipmentSlot(plugin, drawList, pos, rightSlots[i], items, editable, listKey, slotSize);
            }

            // Faint silhouette panel between the columns so the layout reads as a paper doll.
            {
                float colsH = 5 * slotCellHeight - InvUI.S(SlotSpacingV);
                var dollMin = new Vector2(startPos.X + slotCellWidth + InvUI.S(SlotSpacingH), startPos.Y);
                var dollMax = new Vector2(rightX - InvUI.S(SlotSpacingH), startPos.Y + colsH);
                if (dollMax.X - dollMin.X > InvUI.S(24f))
                {
                    drawList.AddRectFilled(dollMin, dollMax, InvUI.U(InvUI.Fade(RsTheme.BgSecondary, 0.55f)), InvUI.S(8f));
                    drawList.AddRect(dollMin, dollMax, InvUI.U(InvUI.Fade(RsTheme.Border, 0.6f)), InvUI.S(8f), ImDrawFlags.None, RsTheme.BorderThickness);
                    var glyph = FontAwesomeIcon.UserShield.ToIconString();
                    using (RsIcons.Push())
                    {
                        var g = ImGui.CalcTextSize(glyph);
                        drawList.AddText((dollMin + dollMax) * 0.5f - g * 0.5f, InvUI.U(InvUI.Fade(RsTheme.TextMuted, 0.35f)), glyph);
                    }
                }
            }

            // Advance cursor past the columns
            float columnsHeight = 5 * slotCellHeight;
            ImGui.SetCursorScreenPos(new Vector2(startPos.X, startPos.Y + columnsHeight + InvUI.S(SlotSpacingV) * 0.5f));

            // Bottom row - Weapons + Soul Stone (extra gap between each)
            float bottomExtraGap = InvUI.S(25f);
            EquipmentSlot[] bottomSlots = { EquipmentSlot.MainHand, EquipmentSlot.OffHand, EquipmentSlot.Soulstone };
            float bottomRowWidth = bottomSlots.Length * slotCellWidth + (bottomSlots.Length - 1) * bottomExtraGap;
            float bottomStartX = startPos.X + (windowWidth - bottomRowWidth) / 2;
            Vector2 bottomStartPos = ImGui.GetCursorScreenPos();
            for (int i = 0; i < bottomSlots.Length; i++)
            {
                Vector2 pos = new Vector2(bottomStartX + i * (slotCellWidth + bottomExtraGap), bottomStartPos.Y);
                DrawEquipmentSlot(plugin, drawList, pos, bottomSlots[i], items, editable, listKey, slotSize);
            }

            // Advance cursor past the bottom row
            ImGui.SetCursorScreenPos(new Vector2(startPos.X, bottomStartPos.Y + slotCellHeight + InvUI.S(SlotSpacingV) * 0.5f));

            if (editable)
            {
                if (RsElements.Button("Save Equipment##equip_save", RsElements.ButtonVariant.Primary))
                {
                    SaveEquipment();
                }
                InvUI.Gap(4f);
                InvUI.Wrapped("Drag items from your inventory onto a slot, or right-click an item and choose Equip.", RsTheme.TextMuted);
            }
        }

        /// Renders a read-only equipment view for target profile.
        public static void RenderEquipmentPreview(Plugin plugin)
        {
            RenderEquipmentPage(plugin, false);
        }

        private static void DrawEquipmentSlot(Plugin plugin, ImDrawListPtr drawList, Vector2 pos, EquipmentSlot slot, Dictionary<int, ItemDefinition> items, bool editable, string listKey, float slotSize)
        {
            int slotIndex = (int)slot;
            string slotName = EquipmentSlotInfo.SlotNames[slotIndex];
            string uniqueId = $"##EquipSlot_{slotIndex}";
            Vector2 tileMax = new Vector2(pos.X + slotSize, pos.Y + slotSize);

            ImGui.SetCursorScreenPos(pos);

            // Slot button (drawn on top of the tile so hover/drag work)
            ImGui.InvisibleButton(uniqueId, new Vector2(slotSize, slotSize));
            bool hovered = ImGui.IsItemHovered();

            bool hasItem = items.TryGetValue(slotIndex, out ItemDefinition item) && item != null;
            var texture = hasItem ? GetOrLoadIcon(plugin, item.iconID) : null;
            if (hasItem && texture == null && item.iconTexture != null && item.iconTexture.Handle != IntPtr.Zero)
                texture = item.iconTexture;

            bool selected = editable && contextSlot == slotIndex;
            InvUI.DrawTile(drawList, pos, tileMax, texture, hasItem ? item.quality : 0, hovered, selected,
                           hasItem ? InvUI.Progress(listKey, slotIndex) : 1f,
                           surface: hasItem ? null : InvUI.Fade(RsTheme.BgSecondary, 0.9f),
                           ring: hasItem || hovered ? (Vector4?)null : InvUI.Fade(RsTheme.Border, 0.7f));

            // Render tooltip / interactions if item equipped
            if (hasItem)
            {
                // Tooltip on hover - same format as the inventory tooltips
                if (hovered)
                    InvUI.ItemTooltip(item, texture, SlotSize);

                // Right-click to unequip (editable mode only)
                if (editable && ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    ImGui.OpenPopup($"EquipContext_{slotIndex}");
                    contextSlot = slotIndex;
                }
                if (editable)
                {
                    if (InvUI.BeginPopup($"EquipContext_{slotIndex}"))
                    {
                        try
                        {
                            contextOpenThisFrame = true;
                            InvUI.Text(item.name ?? slotName, InvUI.QualityColor(item.quality));
                            InvUI.Muted(slotName);
                            InvUI.Divider();
                            if (InvUI.MenuItem("Unequip"))
                            {
                                UnequipItem(slotIndex);
                                ImGui.CloseCurrentPopup();
                            }
                        }
                        finally { InvUI.EndPopup(); }
                    }
                }

                // Drag source for equipped items (to move back to inventory)
                if (editable && ImGui.BeginDragDropSource())
                {
                    try
                    {
                        dragSourceSlot = slotIndex;
                        dragFromEquipment = true;
                        Span<byte> payloadSpan = stackalloc byte[sizeof(int)];
                        BitConverter.TryWriteBytes(payloadSpan, slotIndex);
                        ImGui.SetDragDropPayload("EQUIP_SLOT", payloadSpan, ImGuiCond.Always);
                        InvUI.Text($"Unequip {item.name}", InvUI.QualityColor(item.quality));
                    }
                    finally { ImGui.EndDragDropSource(); }
                }
            }
            else
            {
                // Empty slot - show slot name
                if (hovered)
                {
                    InvUI.BeginTooltip();
                    try
                    {
                        InvUI.Primary(slotName);
                        InvUI.Muted("Empty");
                    }
                    finally { InvUI.EndTooltip(); }
                }
            }

            // Drop target for inventory items
            if (editable && ImGui.BeginDragDropTarget())
            {
                try
                {
                    var payload = ImGui.AcceptDragDropPayload("SLOT_MOVE");
                    if (!payload.IsNull && ItemGrid.DraggedItemSlot.HasValue && ItemGrid.DraggedSlotContents != null)
                    {
                        int srcSlot = ItemGrid.DraggedItemSlot.Value;
                        if (ItemGrid.DraggedSlotContents.TryGetValue(srcSlot, out ItemDefinition draggedItem))
                        {
                            if (EquipmentSlotInfo.IsTypeAllowed(slot, draggedItem.type))
                            {
                                // Equip: remove from inventory, add to equipment
                                EquipItemFromInventory(slotIndex, srcSlot, draggedItem, ItemGrid.DraggedSlotContents);
                            }
                        }
                        ItemGrid.DraggedItemSlot = null;
                        ItemGrid.DraggedSlotContents = null;
                    }
                }
                finally { ImGui.EndDragDropTarget(); }
            }

            // Slot label below
            InvUI.TileCaption(drawList, new Vector2(pos.X, pos.Y + slotSize + InvUI.S(3f)), slotSize, slotName,
                              hovered ? RsTheme.TextSecondary : RsTheme.TextMuted, slotSize + InvUI.S(35f) * 2f);
        }

        // Slot whose context menu is open (selection ring) - reset each frame unless a popup reported itself open.
        private static int contextSlot = -1;
        private static bool contextOpenThisFrame = false;
        private static int contextFrame = -1;

        private static FontAwesomeIcon SlotGlyph(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.Head => FontAwesomeIcon.HatCowboy,
            EquipmentSlot.Body => FontAwesomeIcon.Tshirt,
            EquipmentSlot.Hands => FontAwesomeIcon.HandPaper,
            EquipmentSlot.Legs => FontAwesomeIcon.Walking,
            EquipmentSlot.Feet => FontAwesomeIcon.ShoePrints,
            EquipmentSlot.Earring => FontAwesomeIcon.Gem,
            EquipmentSlot.Necklace => FontAwesomeIcon.Link,
            EquipmentSlot.Bracelet => FontAwesomeIcon.Circle,
            EquipmentSlot.Ring1 => FontAwesomeIcon.Ring,
            EquipmentSlot.Ring2 => FontAwesomeIcon.Ring,
            EquipmentSlot.MainHand => FontAwesomeIcon.Khanda,
            EquipmentSlot.OffHand => FontAwesomeIcon.ShieldAlt,
            EquipmentSlot.Soulstone => FontAwesomeIcon.Fire,
            _ => FontAwesomeIcon.Square,
        };

        private static void EquipItemFromInventory(int equipSlot, int invSlot, ItemDefinition item, Dictionary<int, ItemDefinition> inventoryDict)
        {
            // If something is already equipped in this slot, swap it back to inventory
            if (equippedItems.TryGetValue(equipSlot, out ItemDefinition existingItem))
            {
                inventoryDict[invSlot] = existingItem;
            }
            else
            {
                inventoryDict.Remove(invSlot);
            }

            // Equip the item
            equippedItems[equipSlot] = item;

            // Auto-save both equipment and the affected inventory tab
            SaveEquipmentAndInventory(inventoryDict);
        }

        /// Called from ItemGrid context menu to equip an item from inventory. Finds the first matching equipment slot for the item type.
        public static void EquipFromContextMenu(InventoryLayout layout, int inventorySlot)
        {
            if (!layout.inventorySlotContents.TryGetValue(inventorySlot, out ItemDefinition item))
                return;

            // Find the first compatible empty slot, or first compatible slot if all occupied
            int targetSlot = -1;
            int firstCompatible = -1;
            for (int s = 0; s < EquipmentSlotInfo.SlotCount; s++)
            {
                if (EquipmentSlotInfo.IsTypeAllowed((EquipmentSlot)s, item.type))
                {
                    if (firstCompatible == -1)
                        firstCompatible = s;
                    if (!equippedItems.ContainsKey(s))
                    {
                        targetSlot = s;
                        break;
                    }
                }
            }
            // If no empty compatible slot, use the first compatible one (swap)
            if (targetSlot == -1)
                targetSlot = firstCompatible;
            if (targetSlot == -1)
                return; // No compatible slot at all

            EquipItemFromInventory(targetSlot, inventorySlot, item, layout.inventorySlotContents);
        }

        private static void UnequipItem(int equipSlot)
        {
            if (!equippedItems.TryGetValue(equipSlot, out ItemDefinition item))
                return;

            // Find the first active inventory tab and first empty slot
            var invTabs = InventoryWindow.inventoryTabs;
            if (invTabs.Count > 0 && invTabs[0].Layout is InventoryLayout firstInv)
            {
                // Find first empty slot (0-199)
                for (int s = 0; s < 200; s++)
                {
                    if (!firstInv.inventorySlotContents.ContainsKey(s))
                    {
                        firstInv.inventorySlotContents[s] = item;
                        equippedItems.Remove(equipSlot);

                        // Auto-save both equipment and the inventory tab
                        SaveEquipmentAndInventory(firstInv.inventorySlotContents);
                        return;
                    }
                }
            }

            // If no inventory space, just remove from equipment anyway
            equippedItems.Remove(equipSlot);
            SaveEquipment();
        }

        private static void SaveEquipment()
        {
            Equipment_DS.SendSaveEquipment(Plugin.character, ProfilesPage.profileIndex, equippedItems);
        }

        /// Saves equipment to server and also persists the affected inventory tab. Called automatically on equip/unequip so items are never lost.
        private static void SaveEquipmentAndInventory(Dictionary<int, ItemDefinition> affectedInventory)
        {
            // Save equipment
            Equipment_DS.SendSaveEquipment(Plugin.character, ProfilesPage.profileIndex, equippedItems);

            // Find and save the inventory tab that contains this dictionary
            foreach (var tab in InventoryWindow.inventoryTabs)
            {
                if (tab.Layout is InventoryLayout invLayout && invLayout.inventorySlotContents == affectedInventory)
                {
                    var items = invLayout.inventorySlotContents.Values.ToList();
                    ProfileTabs_DS.SendItemOrder(Plugin.character, ProfilesPage.profileIndex, invLayout, items);
                    break;
                }
            }
        }

        private static IDalamudTextureWrap GetOrLoadIcon(Plugin plugin, int iconID)
        {
            if (iconID <= 0) return null;
            if (IconCache.TryGetValue(iconID, out var cached) && cached != null)
                return cached;
            if (!LoadingIcons.Contains(iconID))
            {
                LoadingIcons.Add(iconID);
                Task.Run(async () =>
                {
                    try
                    {
                        var texture = await WindowOperations.RenderIconAsync(plugin, iconID);
                        if (texture != null)
                            IconCache[iconID] = texture;
                    }
                    catch { }
                    finally { LoadingIcons.Remove(iconID); }
                });
            }
            return null;
        }

        public static void ClearEquipment()
        {
            equippedItems.Clear();
        }

        public static void ClearTargetEquipment()
        {
            targetEquippedItems.Clear();
        }
    }
}
