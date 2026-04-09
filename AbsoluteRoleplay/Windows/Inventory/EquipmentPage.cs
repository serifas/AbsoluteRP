using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Networking;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

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

        /// <summary>
        /// Renders the editable equipment page for own profile.
        /// </summary>
        public static void RenderEquipmentPage(Plugin plugin, bool editable)
        {
            var items = editable ? equippedItems : targetEquippedItems;

            ImGui.Text("Equipment");
            ThemeManager.GradientSeparator();

            float windowWidth = ImGui.GetContentRegionAvail().X;
            float slotCellHeight = SlotSize + SlotSpacingV; // Each slot cell: icon + label + gap
            float slotCellWidth = SlotSize + SlotSpacingH;
            float centerGap = windowWidth - slotCellWidth * 2;
            if (centerGap < 60) centerGap = 60;

            // Layout:
            // Left column (5 slots):  Head, Body, Hands, Legs, Feet
            // Right column (5 slots): Earring, Necklace, Bracelet, Ring1, Ring2
            // Bottom row (3 slots):   MainHand, OffHand, Soulstone

            Vector2 startPos = ImGui.GetCursorScreenPos();
            var drawList = ImGui.GetWindowDrawList();

            // Left column - Armor
            EquipmentSlot[] leftSlots = { EquipmentSlot.Head, EquipmentSlot.Body, EquipmentSlot.Hands, EquipmentSlot.Legs, EquipmentSlot.Feet };
            for (int i = 0; i < leftSlots.Length; i++)
            {
                Vector2 pos = new Vector2(startPos.X + SlotSpacingH, startPos.Y + i * slotCellHeight);
                DrawEquipmentSlot(plugin, drawList, pos, leftSlots[i], items, editable);
            }

            // Right column - Accessories
            EquipmentSlot[] rightSlots = { EquipmentSlot.Earring, EquipmentSlot.Necklace, EquipmentSlot.Bracelet, EquipmentSlot.Ring1, EquipmentSlot.Ring2 };
            float rightX = startPos.X + windowWidth - SlotSize - SlotSpacingH;
            for (int i = 0; i < rightSlots.Length; i++)
            {
                Vector2 pos = new Vector2(rightX, startPos.Y + i * slotCellHeight);
                DrawEquipmentSlot(plugin, drawList, pos, rightSlots[i], items, editable);
            }

            // Advance cursor past the columns
            float columnsHeight = 5 * slotCellHeight;
            ImGui.SetCursorScreenPos(new Vector2(startPos.X, startPos.Y + columnsHeight + SlotSpacingV));

            // Bottom row - Weapons + Soul Stone (extra 25px gap between each)
            float bottomExtraGap = 25f;
            EquipmentSlot[] bottomSlots = { EquipmentSlot.MainHand, EquipmentSlot.OffHand, EquipmentSlot.Soulstone };
            float bottomRowWidth = bottomSlots.Length * slotCellWidth + (bottomSlots.Length - 1) * bottomExtraGap;
            float bottomStartX = startPos.X + (windowWidth - bottomRowWidth) / 2;
            Vector2 bottomStartPos = ImGui.GetCursorScreenPos();
            for (int i = 0; i < bottomSlots.Length; i++)
            {
                Vector2 pos = new Vector2(bottomStartX + i * (slotCellWidth + bottomExtraGap), bottomStartPos.Y);
                DrawEquipmentSlot(plugin, drawList, pos, bottomSlots[i], items, editable);
            }

            // Advance cursor past the bottom row
            ImGui.SetCursorScreenPos(new Vector2(startPos.X, bottomStartPos.Y + slotCellHeight + SlotSpacingV));

            if (editable)
            {
                if (ThemeManager.PillButton("Save Equipment"))
                {
                    SaveEquipment();
                }
            }
        }

        /// <summary>
        /// Renders a read-only equipment view for target profile.
        /// </summary>
        public static void RenderEquipmentPreview(Plugin plugin)
        {
            RenderEquipmentPage(plugin, false);
        }

        private static void DrawEquipmentSlot(Plugin plugin, ImDrawListPtr drawList, Vector2 pos, EquipmentSlot slot, Dictionary<int, ItemDefinition> items, bool editable)
        {
            int slotIndex = (int)slot;
            string slotName = EquipmentSlotInfo.SlotNames[slotIndex];
            string uniqueId = $"##EquipSlot_{slotIndex}";

            ImGui.SetCursorScreenPos(pos);

            // Background rect
            uint bgColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.15f, 0.15f, 0.15f, 1.0f));
            uint borderColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.4f, 0.4f, 0.4f, 1.0f));
            drawList.AddRectFilled(pos, new Vector2(pos.X + SlotSize, pos.Y + SlotSize), bgColor);
            drawList.AddRect(pos, new Vector2(pos.X + SlotSize, pos.Y + SlotSize), borderColor);

            // Slot button
            ImGui.InvisibleButton(uniqueId, new Vector2(SlotSize, SlotSize));

            bool hasItem = items.TryGetValue(slotIndex, out ItemDefinition item);

            // Render icon if item equipped
            if (hasItem && item != null)
            {
                var texture = GetOrLoadIcon(plugin, item.iconID);
                if (texture != null)
                {
                    drawList.AddImage(texture.Handle, pos + new Vector2(2, 2), pos + new Vector2(SlotSize - 2, SlotSize - 2));
                }

                // Tooltip on hover — same format as ItemGrid inventory tooltips
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    var iconTexture = GetOrLoadIcon(plugin, item.iconID);
                    Vector2 iconSize = new Vector2(SlotSize, SlotSize);
                    if (iconTexture != null)
                        ImGui.Image(iconTexture.Handle, iconSize);
                    else
                        ImGui.Image(UI.UICommonImage(UI.CommonImageTypes.blankPictureTab).Handle, iconSize);
                    Misc.RenderHtmlColoredTextInline(item.name ?? "Unknown", 500);
                    ImGui.Separator();
                    ImGui.Spacing();
                    if (!string.IsNullOrEmpty(item.description))
                        Misc.RenderHtmlElements(item.description, false, true, true, true, null, true);
                    ImGui.EndTooltip();
                }

                // Right-click to unequip (editable mode only)
                if (editable && ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    ImGui.OpenPopup($"EquipContext_{slotIndex}");
                }
                if (editable)
                {
                    using (var popup = ImRaii.Popup($"EquipContext_{slotIndex}"))
                    {
                        if (popup)
                        {
                            if (ImGui.Selectable("Unequip"))
                            {
                                UnequipItem(slotIndex);
                            }
                        }
                    }
                }

                // Drag source for equipped items (to move back to inventory)
                if (editable && ImGui.BeginDragDropSource())
                {
                    dragSourceSlot = slotIndex;
                    dragFromEquipment = true;
                    Span<byte> payloadSpan = stackalloc byte[sizeof(int)];
                    BitConverter.TryWriteBytes(payloadSpan, slotIndex);
                    ImGui.SetDragDropPayload("EQUIP_SLOT", payloadSpan, ImGuiCond.Always);
                    ImGui.Text($"Unequip {item.name}");
                    ImGui.EndDragDropSource();
                }
            }
            else
            {
                // Empty slot - show slot name
                if (ImGui.IsItemHovered())
                {
                    using (var tooltip = ImRaii.Tooltip())
                    {
                        if (tooltip)
                        {
                            ImGui.Text(slotName);
                            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), "Empty");
                        }
                    }
                }
            }

            // Drop target for inventory items
            if (editable && ImGui.BeginDragDropTarget())
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
                ImGui.EndDragDropTarget();
            }
            int SlotPadding = 35;
            // Slot label below
            string label = slotName;
            Vector2 labelSize = ImGui.CalcTextSize(label);
            // Only draw label if it fits
            if (labelSize.X < SlotSize + SlotPadding * 2)
            {
                float labelX = pos.X + (SlotSize - labelSize.X) / 2;
                drawList.AddText(new Vector2(labelX, pos.Y + SlotSize + 1), ImGui.ColorConvertFloat4ToU32(new Vector4(0.6f, 0.6f, 0.6f, 1.0f)), label);
            }
        }

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

        /// <summary>
        /// Called from ItemGrid context menu to equip an item from inventory.
        /// Finds the first matching equipment slot for the item type.
        /// </summary>
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
            DataSender.SendSaveEquipment(Plugin.character, ProfileWindow.profileIndex, equippedItems);
        }

        /// <summary>
        /// Saves equipment to server and also persists the affected inventory tab.
        /// Called automatically on equip/unequip so items are never lost.
        /// </summary>
        private static void SaveEquipmentAndInventory(Dictionary<int, ItemDefinition> affectedInventory)
        {
            // Save equipment
            DataSender.SendSaveEquipment(Plugin.character, ProfileWindow.profileIndex, equippedItems);

            // Find and save the inventory tab that contains this dictionary
            foreach (var tab in InventoryWindow.inventoryTabs)
            {
                if (tab.Layout is InventoryLayout invLayout && invLayout.inventorySlotContents == affectedInventory)
                {
                    var items = invLayout.inventorySlotContents.Values.ToList();
                    DataSender.SendItemOrder(Plugin.character, ProfileWindow.profileIndex, invLayout, items);
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
