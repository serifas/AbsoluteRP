using AbsoluteRoleplay.Windows.Ect;
using AbsoluteRoleplay.Windows.MainPanel.Views.Account;
using AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Interface.Textures.TextureWraps;
using ImGuiNET;
using Networking;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace AbsoluteRoleplay.Helpers
{
    internal class ItemGrid
    {
        private const int GridSize = 10; // 10x10 grid for 200 slots
        private const int TotalSlots = GridSize * GridSize;

        // Global drag-and-drop state
        private static int? DraggedItemSlot = null;
        private static Dictionary<int, ItemDefinition> DraggedSlotContents = null;

        public static void DrawGrid(Plugin plugin, InventoryLayout layout, string targetPlayerName, string targetPlayerWorld, bool isTrade)
        {
            int hoveredSlotIndex = -1;
            ItemDefinition hoveredItem = null;
            bool hoveredIsTrade = false;

            // Only reorder inventorySlotContents if needed (optional, can be removed if not required)
            if (layout.inventorySlotContents.Count > 0)
            {
                var items = layout.inventorySlotContents.Values.ToList();
                layout.inventorySlotContents.Clear();
                foreach (var item in items)
                {
                    layout.inventorySlotContents[item.slot] = item;
                }
            }

            // --- SPLIT TRADE GRID (TOP) ---
            const int TradeGridWidth = 10;
            if (isTrade)
            {
                if (layout.tradeSlotContents == null)
                    layout.tradeSlotContents = new Dictionary<int, ItemDefinition>();
                if (layout.traderSlotContents == null)
                    layout.traderSlotContents = new Dictionary<int, ItemDefinition>();

                ImGui.Dummy(new Vector2(0, 10));
                float windowWidth = ImGui.GetWindowWidth();

                // --- "Sending" label ---
                ImGui.Text("Sending");


                ImGui.Dummy(new Vector2(0, 10));

                // --- "Sending" grid (left) ---
                ImGui.BeginGroup();
                for (int x = 0; x < TradeGridWidth; x++)
                {
                    int slotIndex = x;

                    ImGui.PushID($"send_{slotIndex}");
                    ImGui.BeginGroup();

                    Vector2 cellPos = ImGui.GetCursorScreenPos();
                    Vector2 cellSize = new Vector2(50, 50);

                    ImGui.GetWindowDrawList().AddRectFilled(
                        cellPos,
                        cellPos + cellSize,
                        ImGui.GetColorU32(new Vector4(0.2f, 0.2f, 0.5f, 1.0f))
                    );

                    bool hasTradeItem = layout.tradeSlotContents.ContainsKey(slotIndex) && layout.tradeSlotContents[slotIndex].name != string.Empty;
                    if (hasTradeItem)
                    {
                        ImGui.Image(WindowOperations.RenderIconAsync(plugin, layout.tradeSlotContents[slotIndex].iconID).Result.ImGuiHandle, cellSize);
                    }

                    ImGui.SetCursorScreenPos(cellPos);
                    ImGui.InvisibleButton($"##send_slot{slotIndex}", cellSize);

                    // Tooltip hover for trade grid
                    if (ImGui.IsItemHovered() && hasTradeItem)
                    {
                        ImGui.BeginTooltip();
                        Misc.RenderHtmlColoredTextInline(layout.traderSlotContents[slotIndex].name);
                        ImGui.Spacing();
                        Misc.RenderHtmlColoredTextInline(layout.traderSlotContents[slotIndex].description);
                        ImGui.EndTooltip();
                    }

                    // Begin drag source for trade slot
                    if (hasTradeItem && ImGui.BeginDragDropSource())
                    {
                        unsafe
                        {
                            DraggedItemSlot = slotIndex;
                            DraggedSlotContents = layout.tradeSlotContents;
                            int payloadData = slotIndex;
                            ImGui.SetDragDropPayload("SLOT_MOVE", new IntPtr(&payloadData), sizeof(int));
                        }
                        ImGui.Text($"Dragging Trade Slot {slotIndex}");
                        ImGui.EndDragDropSource();
                    }

                    // Accept drag from inventory
                    if (ImGui.BeginDragDropTarget())
                    {
                        var payload = ImGui.AcceptDragDropPayload("SLOT_MOVE");
                        unsafe
                        {
                            if (payload.NativePtr != null && DraggedItemSlot.HasValue && DraggedSlotContents != null)
                            {
                                int sourceSlotIndex = *(int*)payload.Data.ToPointer();
                                if (DraggedSlotContents.ContainsKey(sourceSlotIndex))
                                {
                                    var draggedItem = DraggedSlotContents[sourceSlotIndex];

                                    // Only allow from inventory to trade
                                    if (DraggedSlotContents == layout.inventorySlotContents)
                                    {
                                        bool tradeChanged = false;
                                        // If the target trade slot has an item, swap them
                                        if (hasTradeItem)
                                        {
                                            var targetItem = layout.tradeSlotContents[slotIndex];
                                            layout.tradeSlotContents[slotIndex] = draggedItem;
                                            layout.tradeSlotContents[slotIndex].slot = slotIndex;
                                            layout.inventorySlotContents[sourceSlotIndex] = targetItem;
                                            layout.inventorySlotContents[sourceSlotIndex].slot = sourceSlotIndex;
                                            tradeChanged = true;
                                        }
                                        else
                                        {
                                            // Normal move if the target slot is empty
                                            layout.tradeSlotContents[slotIndex] = draggedItem;
                                            layout.tradeSlotContents[slotIndex].slot = slotIndex;
                                            layout.inventorySlotContents.Remove(sourceSlotIndex);
                                            tradeChanged = true;
                                        }
                                        if (tradeChanged)
                                        {
                                            DataSender.SendTradeUpdate(ProfileWindow.profileIndex, targetPlayerName, targetPlayerWorld, layout, layout.tradeSlotContents.Values.ToList());
                                        }
                                    }

                                    DraggedItemSlot = null;
                                    DraggedSlotContents = null;
                                }
                            }
                        }
                        ImGui.EndDragDropTarget();
                    }

                    // --- Right-click context menu for trade slot (only in trade mode) ---
                    if (isTrade && hasTradeItem && ImGui.IsItemClicked(ImGuiMouseButton.Right))
                    {
                        ImGui.OpenPopup($"##tradeContextMenu{slotIndex}");
                    }
                    if (isTrade && ImGui.BeginPopup($"##tradeContextMenu{slotIndex}"))
                    {
                        if (ImGui.MenuItem("Remove from Trade"))
                        {
                            // Find first available inventory slot
                            int firstEmpty = -1;
                            for (int i = 0; i < TotalSlots; i++)
                            {
                                if (!layout.inventorySlotContents.ContainsKey(i) || layout.inventorySlotContents[i].name == string.Empty)
                                {
                                    firstEmpty = i;
                                    break;
                                }
                            }
                            if (firstEmpty != -1)
                            {
                                var item = layout.tradeSlotContents[slotIndex];
                                item.slot = firstEmpty;
                                layout.inventorySlotContents[firstEmpty] = item;
                                layout.tradeSlotContents.Remove(slotIndex);
                                DataSender.SendTradeUpdate(ProfileWindow.profileIndex, targetPlayerName, targetPlayerWorld, layout, layout.tradeSlotContents.Values.ToList());
                            }
                        }
                        ImGui.EndPopup();
                    }

                    ImGui.EndGroup();
                    ImGui.PopID();

                    if (x < TradeGridWidth - 1)
                        ImGui.SameLine();
                }
                ImGui.EndGroup();

                ImGui.Text("Receiving");

                ImGui.BeginGroup();
                for (int x = 0; x < TradeGridWidth; x++)
                {
                    int slotIndex = x;

                    ImGui.PushID($"recv_{slotIndex}");
                    ImGui.BeginGroup();

                    Vector2 cellPos = ImGui.GetCursorScreenPos();
                    Vector2 cellSize = new Vector2(50, 50);

                    // Draw only a border (not a filled rect)
                    ImGui.GetWindowDrawList().AddRect(
                        cellPos,
                        cellPos + cellSize,
                        ImGui.GetColorU32(new Vector4(0.0f, 0.5f, 0.2f, 1.0f)), 2.0f // Border thickness
                    );

                    bool hasRecvItem = layout.traderSlotContents != null &&
                                       layout.traderSlotContents.ContainsKey(slotIndex) &&
                                       layout.traderSlotContents[slotIndex].name != string.Empty;
                    if (hasRecvItem)
                    {
                        ImGui.Image(WindowOperations.RenderIconAsync(plugin, layout.traderSlotContents[slotIndex].iconID).Result.ImGuiHandle, cellSize);
                    }

                    ImGui.SetCursorScreenPos(cellPos);
                    ImGui.InvisibleButton($"##recv_slot{slotIndex}", cellSize);

                    // Tooltip hover for receiving grid
                    if (ImGui.IsItemHovered() && hasRecvItem)
                    {
                        ImGui.BeginTooltip();
                        Misc.RenderHtmlColoredTextInline(layout.traderSlotContents[slotIndex].name);
                        ImGui.Spacing();
                        Misc.RenderHtmlColoredTextInline(layout.traderSlotContents[slotIndex].description);
                        ImGui.EndTooltip();
                    }

                    // --- NO drag source, NO drag target, NO context menu for receiving grid ---

                    ImGui.EndGroup();
                    ImGui.PopID();

                    if (x < TradeGridWidth - 1)
                        ImGui.SameLine();
                }
                ImGui.EndGroup();

                ImGui.Dummy(new Vector2(0, 10));
                ImGui.Text("Inventory");
                ImGui.Dummy(new Vector2(0, 10));
            }

            // --- INVENTORY GRID (BOTTOM) ---
            // Always render inventory grid, regardless of isTrade
            for (int y = 0; y < GridSize; y++)
            {
                for (int x = 0; x < GridSize; x++)
                {
                    int slotIndex = y * GridSize + x;

                    ImGui.PushID(slotIndex);
                    ImGui.BeginGroup();

                    Vector2 cellPos = ImGui.GetCursorScreenPos();
                    Vector2 cellSize = new Vector2(50, 50);

                    ImGui.GetWindowDrawList().AddRectFilled(
                        cellPos,
                        cellPos + cellSize,
                        ImGui.GetColorU32(new Vector4(0.3f, 0.3f, 0.3f, 1.0f))
                    );

                    bool hasInvItem = layout.inventorySlotContents.ContainsKey(slotIndex) && layout.inventorySlotContents[slotIndex].name != string.Empty;
                    try
                    {
                        if (hasInvItem)
                        {
                            ImGui.Image(WindowOperations.RenderIconAsync(plugin, layout.inventorySlotContents[slotIndex].iconID).Result.ImGuiHandle, cellSize);
                        }
                    }
                    catch (Exception ex)
                    {
                        plugin.logger.Error($"Failed to render icon for slotIndex {slotIndex}: {ex.Message}");
                    }

                    ImGui.SetCursorScreenPos(cellPos);

                    if (ImGui.InvisibleButton($"##slot{slotIndex}", cellSize))
                    {
                        // Handle slot click
                    }

                    // Tooltip hover for inventory grid
                    if (ImGui.IsItemHovered() && hasInvItem)
                    {
                        ImGui.BeginTooltip();
                        ImGui.Image(WindowOperations.RenderIconAsync(plugin, layout.inventorySlotContents[slotIndex].iconID).Result.ImGuiHandle, cellSize);

                        Misc.RenderHtmlColoredTextInline(layout.inventorySlotContents[slotIndex].name);
                        ImGui.Separator();
                        ImGui.Spacing();
                        Misc.RenderHtmlColoredTextInline(layout.inventorySlotContents[slotIndex].description);
                        ImGui.EndTooltip();
                    }

                    // Context menu for inventory grid
                    if (!isTrade)
                    {
                        // Standard context menu (Delete, Duplicate, Edit)
                        if (ImGui.IsItemClicked(ImGuiMouseButton.Right) && hasInvItem)
                        {
                            ImGui.OpenPopup($"##contextMenu{slotIndex}");
                        }
                        if (ImGui.BeginPopup($"##contextMenu{slotIndex}"))
                        {
                            if (ImGui.MenuItem("Delete"))
                            {
                                layout.inventorySlotContents.Remove(slotIndex);
                            }
                            if (ImGui.MenuItem("Duplicate"))
                            {
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

                                ItemDefinition itemToDuplicate = layout.inventorySlotContents[slotIndex];
                                layout.inventorySlotContents[firstEmptySlotIndex] = new ItemDefinition
                                {
                                    name = itemToDuplicate.name,
                                    description = itemToDuplicate.description,
                                    type = itemToDuplicate.type,
                                    subtype = itemToDuplicate.subtype,
                                    iconID = itemToDuplicate.iconID,
                                    slot = firstEmptySlotIndex,
                                    quality = itemToDuplicate.quality,
                                    iconTexture = itemToDuplicate.iconTexture
                                };
                            }
                            ImGui.EndPopup();
                        }
                    }
                    else if (isTrade && hasInvItem && ImGui.IsItemClicked(ImGuiMouseButton.Right))
                    {
                        ImGui.OpenPopup($"##contextMenu{slotIndex}");
                    }
                    if (isTrade && ImGui.BeginPopup($"##contextMenu{slotIndex}"))
                    {
                        if (ImGui.MenuItem("Trade"))
                        {
                            // Find first available trade slot
                            int firstEmpty = -1;
                            for (int t = 0; t < TradeGridWidth; t++)
                            {
                                if (!layout.tradeSlotContents.ContainsKey(t) || layout.tradeSlotContents[t].name == string.Empty)
                                {
                                    firstEmpty = t;
                                    break;
                                }
                            }
                            if (firstEmpty != -1)
                            {
                                var item = layout.inventorySlotContents[slotIndex];
                                item.slot = firstEmpty;
                                layout.tradeSlotContents[firstEmpty] = item;
                                layout.inventorySlotContents.Remove(slotIndex);
                                DataSender.SendTradeUpdate(ProfileWindow.profileIndex, targetPlayerName, targetPlayerWorld, layout, layout.tradeSlotContents.Values.ToList());
                            }
                        }
                        ImGui.EndPopup();
                    }

                    // Begin Drag Source
                    if (hasInvItem && ImGui.BeginDragDropSource())
                    {
                        unsafe
                        {
                            DraggedItemSlot = slotIndex;
                            DraggedSlotContents = layout.inventorySlotContents;
                            int payloadData = slotIndex;
                            ImGui.SetDragDropPayload("SLOT_MOVE", new IntPtr(&payloadData), sizeof(int));
                        }
                        ImGui.Text($"Dragging Slot {slotIndex}");
                        ImGui.EndDragDropSource();
                    }

                    if (ImGui.BeginDragDropTarget())
                    {
                        var payload = ImGui.AcceptDragDropPayload("SLOT_MOVE");
                        unsafe
                        {
                            if (payload.NativePtr != null && DraggedItemSlot.HasValue && DraggedSlotContents != null)
                            {
                                int sourceSlotIndex = *(int*)payload.Data.ToPointer();
                                var draggedItem = DraggedSlotContents[sourceSlotIndex];

                                // Drag from inventory to inventory (already handled)
                                if (DraggedSlotContents == layout.inventorySlotContents && layout.inventorySlotContents.ContainsKey(sourceSlotIndex))
                                {
                                    if (layout.inventorySlotContents.ContainsKey(slotIndex))
                                    {
                                        var targetItem = layout.inventorySlotContents[slotIndex];
                                        layout.inventorySlotContents[slotIndex] = draggedItem;
                                        layout.inventorySlotContents[slotIndex].slot = slotIndex;
                                        layout.inventorySlotContents[sourceSlotIndex] = targetItem;
                                        layout.inventorySlotContents[sourceSlotIndex].slot = sourceSlotIndex;
                                    }
                                    else
                                    {
                                        layout.inventorySlotContents[slotIndex] = draggedItem;
                                        layout.inventorySlotContents[slotIndex].slot = slotIndex;
                                        layout.inventorySlotContents.Remove(sourceSlotIndex);
                                    }
                                    if (!isTrade)
                                    {
                                        List<ItemDefinition> newItemList = new List<ItemDefinition>();
                                        for (int i = 0; i < TotalSlots; i++)
                                        {
                                            if (layout.inventorySlotContents.ContainsKey(i) && layout.inventorySlotContents[i].name != string.Empty)
                                            {
                                                newItemList.Add(layout.inventorySlotContents[i]);
                                            }
                                        }
                                        DataSender.SendItemOrder(ProfileWindow.profileIndex, layout, newItemList);
                                    }
                                }
                                // Drag from trade to inventory
                                else if (DraggedSlotContents == layout.tradeSlotContents && layout.tradeSlotContents.ContainsKey(sourceSlotIndex))
                                {
                                    bool tradeChanged = false;
                                    if (layout.inventorySlotContents.ContainsKey(slotIndex) && layout.inventorySlotContents[slotIndex].name != string.Empty)
                                    {
                                        var targetItem = layout.inventorySlotContents[slotIndex];
                                        layout.inventorySlotContents[slotIndex] = draggedItem;
                                        layout.inventorySlotContents[slotIndex].slot = slotIndex;
                                        layout.tradeSlotContents[sourceSlotIndex] = targetItem;
                                        layout.tradeSlotContents[sourceSlotIndex].slot = sourceSlotIndex;
                                        tradeChanged = true;
                                    }
                                    else // Move to empty inventory slot
                                    {
                                        layout.inventorySlotContents[slotIndex] = draggedItem;
                                        layout.inventorySlotContents[slotIndex].slot = slotIndex;
                                        layout.tradeSlotContents.Remove(sourceSlotIndex);
                                        tradeChanged = true;
                                    }
                                    if (tradeChanged)
                                    {
                                        DataSender.SendTradeUpdate(ProfileWindow.profileIndex, targetPlayerName, targetPlayerWorld, layout, layout.tradeSlotContents.Values.ToList());
                                    }
                                }

                                DraggedItemSlot = null;
                                DraggedSlotContents = null;
                            }
                        }
                        ImGui.EndDragDropTarget();
                    }

                    ImGui.EndGroup();
                    ImGui.PopID();

                    if (x < GridSize - 1)
                    {
                        ImGui.SameLine();
                    }
                }
            }
        }
    }
}
