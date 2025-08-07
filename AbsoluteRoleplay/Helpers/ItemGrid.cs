using AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Networking;
using System.Numerics;
using System.Runtime.InteropServices;
namespace AbsoluteRoleplay.Helpers
{
    internal class ItemGrid
    {
        private const int GridSize = 10; // 10x10 grid for 200 slots
        private const int TotalSlots = GridSize * GridSize;

        // Global drag-and-drop state
        private static int? DraggedItemSlot = null;
        private static Dictionary<int, ItemDefinition> DraggedSlotContents = null;

        private static readonly Dictionary<int, IDalamudTextureWrap> IconCache = new();
        private static readonly HashSet<int> LoadingIcons = new();

        private static async Task PreloadIconAsync(Plugin plugin, int iconID)
        {
            if (IconCache.ContainsKey(iconID) || LoadingIcons.Contains(iconID))
                return;

            LoadingIcons.Add(iconID);
            var texture = await WindowOperations.RenderIconAsync(plugin, iconID);
            if (texture != null)
                IconCache[iconID] = texture;
            LoadingIcons.Remove(iconID);
        }
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

            // --- Calculate dynamic cell size for grid ---
            float windowWidth = Dalamud.Bindings.ImGui.ImGui.GetWindowWidth();
            float windowHeight =Dalamud.Bindings.ImGui.ImGui.GetWindowHeight();

            // Reserve some space for labels/buttons above the grid
            float reservedHeight = 0;
            if (isTrade)
                reservedHeight = 180; // Adjust as needed for trade UI
            else
                reservedHeight = 40; // Adjust as needed for inventory-only UI

            // Calculate the available area for the grid
            float availableWidth = windowWidth - 20; // Padding
            float availableHeight = windowHeight - reservedHeight;

            // Calculate the cell size so the grid fits and remains square
            float cellSize = MathF.Min(availableWidth / GridSize, availableHeight / GridSize);
            Vector2 iconCellSize = new Vector2(cellSize, cellSize);

            // --- SPLIT TRADE GRID (TOP) ---
            const int TradeGridWidth = 10;
            if (isTrade)
            {
                if (layout.tradeSlotContents == null)
                    layout.tradeSlotContents = new Dictionary<int, ItemDefinition>();
                if (layout.traderSlotContents == null)
                    layout.traderSlotContents = new Dictionary<int, ItemDefinition>();

                Dalamud.Bindings.ImGui.ImGui.Dummy(new Vector2(0, 10));
                Dalamud.Bindings.ImGui.ImGui.Text("Sending");
                Dalamud.Bindings.ImGui.ImGui.Dummy(new Vector2(0, 10));

                // --- "Sending" grid (left) ---
                Dalamud.Bindings.ImGui.ImGui.BeginGroup();
                for (int x = 0; x < TradeGridWidth; x++)
                {
                    int slotIndex = x;

                    Dalamud.Bindings.ImGui.ImGui.PushID($"send_{slotIndex}");
                    Dalamud.Bindings.ImGui.ImGui.BeginGroup();

                    Vector2 cellPos = Dalamud.Bindings.ImGui.ImGui.GetCursorScreenPos();

                    Dalamud.Bindings.ImGui.ImGui.GetWindowDrawList().AddRectFilled(
                        cellPos,
                        cellPos + iconCellSize,
                        Dalamud.Bindings.ImGui.ImGui.GetColorU32(new Vector4(0.2f, 0.2f, 0.5f, 1.0f))
                    );

                    bool hasTradeItem = layout.tradeSlotContents.ContainsKey(slotIndex) && layout.tradeSlotContents[slotIndex].name != string.Empty;
                    if (hasTradeItem)
                    {
                        int iconID = layout.tradeSlotContents[slotIndex].iconID;
                        if (IconCache.TryGetValue(iconID, out var texture) && texture != null && texture.Handle != IntPtr.Zero)
                        {
                            Dalamud.Bindings.ImGui.ImGui.Image(texture.Handle, iconCellSize);
                        }
                        else
                        {
                            Dalamud.Bindings.ImGui.ImGui.Image(UI.UICommonImage(UI.CommonImageTypes.blankPictureTab).Handle, iconCellSize);
                            _ = PreloadIconAsync(plugin, iconID);
                        }
                    }

                    Dalamud.Bindings.ImGui.ImGui.SetCursorScreenPos(cellPos);
                    Dalamud.Bindings.ImGui.ImGui.InvisibleButton($"##send_slot{slotIndex}", iconCellSize);

                    // Tooltip hover for trade grid
                    if (Dalamud.Bindings.ImGui.ImGui.IsItemHovered() && hasTradeItem)
                    {
                        Dalamud.Bindings.ImGui.ImGui.BeginTooltip();
                        Misc.RenderHtmlColoredTextInline(layout.traderSlotContents[slotIndex].name, 400);
                        Dalamud.Bindings.ImGui.ImGui.Spacing();
                        Misc.RenderHtmlElements(layout.traderSlotContents[slotIndex].description, false, true, true, true, null, true);
                        Dalamud.Bindings.ImGui.ImGui.EndTooltip();
                    }

                    // Begin drag source for trade slot
                    if (hasTradeItem && Dalamud.Bindings.ImGui.ImGui.BeginDragDropSource())
                    {
                        unsafe
                        {
                            DraggedItemSlot = slotIndex;
                            DraggedSlotContents = layout.tradeSlotContents;
                            int payloadData = slotIndex;
                            ImGui.SetDragDropPayload("SLOT_MOVE", MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref payloadData, 1)), ImGuiCond.Always);
                        }
                        Dalamud.Bindings.ImGui.ImGui.Text($"Dragging Trade Slot {slotIndex}");
                        Dalamud.Bindings.ImGui.ImGui.EndDragDropSource();
                    }

                    // Accept drag from inventory
                    if (Dalamud.Bindings.ImGui.ImGui.BeginDragDropTarget())
                    {
                        var payload = Dalamud.Bindings.ImGui.ImGui.AcceptDragDropPayload("SLOT_MOVE");
                        unsafe
                        {
                            if (payload.IsNull && DraggedItemSlot.HasValue && DraggedSlotContents != null)
                            {
                                int sourceSlotIndex = *(int*)payload.Data;
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
                        Dalamud.Bindings.ImGui.ImGui.EndDragDropTarget();
                    }

                    // --- Right-click context menu for trade slot (only in trade mode) ---
                    if(Dalamud.Bindings.ImGui.ImGui.IsItemClicked(Dalamud.Bindings.ImGui.ImGuiMouseButton.Right))
                    {
                        Dalamud.Bindings.ImGui.ImGui.OpenPopup($"##tradeContextMenu{slotIndex}");
                    }
                    if (isTrade && Dalamud.Bindings.ImGui.ImGui.BeginPopup($"##tradeContextMenu{slotIndex}"))
                    {
                        if (Dalamud.Bindings.ImGui.ImGui.MenuItem("Remove from Trade"))
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
                        Dalamud.Bindings.ImGui.ImGui.EndPopup();
                    }

                    Dalamud.Bindings.ImGui.ImGui.EndGroup();
                    Dalamud.Bindings.ImGui.ImGui.PopID();

                    if (x < TradeGridWidth - 1)
                        Dalamud.Bindings.ImGui.ImGui.SameLine();
                }
                Dalamud.Bindings.ImGui.ImGui.EndGroup();

                Dalamud.Bindings.ImGui.ImGui.Text("Receiving");

                Dalamud.Bindings.ImGui.ImGui.BeginGroup();
                for (int x = 0; x < TradeGridWidth; x++)
                {
                    int slotIndex = x;

                    Dalamud.Bindings.ImGui.ImGui.PushID($"recv_{slotIndex}");
                    Dalamud.Bindings.ImGui.ImGui.BeginGroup();

                    Vector2 cellPos = Dalamud.Bindings.ImGui.ImGui.GetCursorScreenPos();

                    Dalamud.Bindings.ImGui.ImGui.GetWindowDrawList().AddRect(
                        cellPos,
                        cellPos + iconCellSize,
                        Dalamud.Bindings.ImGui.ImGui.GetColorU32(new Vector4(0.0f, 0.5f, 0.2f, 1.0f)), 2.0f // Border thickness
                    );

                    bool hasRecvItem = layout.traderSlotContents != null &&
                                       layout.traderSlotContents.ContainsKey(slotIndex) &&
                                       layout.traderSlotContents[slotIndex].name != string.Empty;
                    if (hasRecvItem)
                    {
                        int iconID = layout.traderSlotContents[slotIndex].iconID;
                        if (IconCache.TryGetValue(iconID, out var texture) && texture != null && texture.Handle != IntPtr.Zero)
                        {
                            Dalamud.Bindings.ImGui.ImGui.Image(texture.Handle, iconCellSize);
                        }
                        else
                        {
                            Dalamud.Bindings.ImGui.ImGui.Image(UI.UICommonImage(UI.CommonImageTypes.blankPictureTab).Handle, iconCellSize);
                            _ = PreloadIconAsync(plugin, iconID);
                        }
                    }

                    Dalamud.Bindings.ImGui.ImGui.SetCursorScreenPos(cellPos);
                    Dalamud.Bindings.ImGui.ImGui.InvisibleButton($"##recv_slot{slotIndex}", iconCellSize);

                    // Tooltip hover for receiving grid
                    if (Dalamud.Bindings.ImGui.ImGui.IsItemHovered() && hasRecvItem)
                    {
                        Dalamud.Bindings.ImGui.ImGui.BeginTooltip();
                        Misc.RenderHtmlColoredTextInline(layout.traderSlotContents[slotIndex].name,  400);
                        Dalamud.Bindings.ImGui.ImGui.Spacing();
                        Misc.RenderHtmlElements(layout.traderSlotContents[slotIndex].description, false, true, true, true, null, true);
                        Dalamud.Bindings.ImGui.ImGui.EndTooltip();
                    }

                    Dalamud.Bindings.ImGui.ImGui.EndGroup();
                    Dalamud.Bindings.ImGui.ImGui.PopID();

                    if (x < TradeGridWidth - 1)
                        Dalamud.Bindings.ImGui.ImGui.SameLine();
                }
                Dalamud.Bindings.ImGui.ImGui.EndGroup();

                Dalamud.Bindings.ImGui.ImGui.Dummy(new Vector2(0, 10));
                Dalamud.Bindings.ImGui.ImGui.Text("Inventory");
                Dalamud.Bindings.ImGui.ImGui.Dummy(new Vector2(0, 10));
            }

            // --- INVENTORY GRID (BOTTOM) ---
            // Always render inventory grid, regardless of isTrade
            for (int y = 0; y < GridSize; y++)
            {
                for (int x = 0; x < GridSize; x++)
                {
                    int slotIndex = y * GridSize + x;

                    Dalamud.Bindings.ImGui.ImGui.PushID(slotIndex);
                    Dalamud.Bindings.ImGui.ImGui.BeginGroup();

                    Vector2 cellPos = Dalamud.Bindings.ImGui.ImGui.GetCursorScreenPos();

                    Dalamud.Bindings.ImGui.ImGui.GetWindowDrawList().AddRectFilled(
                        cellPos,
                        cellPos + iconCellSize,
                        Dalamud.Bindings.ImGui.ImGui.GetColorU32(new Vector4(0.3f, 0.3f, 0.3f, 1.0f))
                    );

                    bool hasInvItem = layout.inventorySlotContents.ContainsKey(slotIndex) && layout.inventorySlotContents[slotIndex].name != string.Empty;
                    try
                    {
                        if (hasInvItem)
                        {
                            int iconID = layout.inventorySlotContents[slotIndex].iconID;
                            if (IconCache.TryGetValue(iconID, out var texture) && texture != null && texture.Handle != IntPtr.Zero)
                            {
                                Dalamud.Bindings.ImGui.ImGui.Image(texture.Handle, iconCellSize);
                            }
                            else
                            {
                                Dalamud.Bindings.ImGui.ImGui.Image(UI.UICommonImage(UI.CommonImageTypes.blankPictureTab).Handle, iconCellSize);
                                _ = PreloadIconAsync(plugin, iconID);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to render icon for slotIndex {slotIndex}: {ex.Message}");
                    }

                    Dalamud.Bindings.ImGui.ImGui.SetCursorScreenPos(cellPos);

                    if (Dalamud.Bindings.ImGui.ImGui.InvisibleButton($"##slot{slotIndex}", iconCellSize))
                    {
                        // Handle slot click
                    }

                    // Tooltip hover for inventory grid
                    if (Dalamud.Bindings.ImGui.ImGui.IsItemHovered() && hasInvItem)
                    {
                        Dalamud.Bindings.ImGui.ImGui.BeginTooltip();

                        int iconID = layout.inventorySlotContents[slotIndex].iconID;
                        if (IconCache.TryGetValue(iconID, out var texture) && texture != null && texture.Handle != IntPtr.Zero)
                        {
                            Dalamud.Bindings.ImGui.ImGui.Image(texture.Handle, iconCellSize);
                        }
                        else
                        {
                            Dalamud.Bindings.ImGui.ImGui.Image(UI.UICommonImage(UI.CommonImageTypes.blankPictureTab).Handle, iconCellSize);
                            _ = PreloadIconAsync(plugin, iconID);
                        }

                        Misc.RenderHtmlColoredTextInline(layout.inventorySlotContents[slotIndex].name, 500);
                        Dalamud.Bindings.ImGui.ImGui.Separator();
                        Dalamud.Bindings.ImGui.ImGui.Spacing();
                        Misc.RenderHtmlElements(layout.inventorySlotContents[slotIndex].description, false, true, true, true, null, true);
                        Dalamud.Bindings.ImGui.ImGui.EndTooltip();
                    }

                    // Context menu for inventory grid
                    if (!isTrade)
                    {
                        
                        if (Dalamud.Bindings.ImGui.ImGui.IsItemClicked(Dalamud.Bindings.ImGui.ImGuiMouseButton.Right) && hasInvItem)
                        {
                            Dalamud.Bindings.ImGui.ImGui.OpenPopup($"##contextMenu{slotIndex}");
                        }
                        if (Dalamud.Bindings.ImGui.ImGui.BeginPopup($"##contextMenu{slotIndex}"))
                        {
                            if (Dalamud.Bindings.ImGui.ImGui.MenuItem("Delete"))
                            {
                                layout.inventorySlotContents.Remove(slotIndex);
                            }
                            if (Dalamud.Bindings.ImGui.ImGui.MenuItem("Duplicate"))
                            {
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
                                    return;

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
                            Dalamud.Bindings.ImGui.ImGui.EndPopup();
                        }
                    }
                    else if (isTrade && hasInvItem && Dalamud.Bindings.ImGui.ImGui.IsItemClicked(Dalamud.Bindings.ImGui.ImGuiMouseButton.Right))
                    {
                        Dalamud.Bindings.ImGui.ImGui.OpenPopup($"##contextMenu{slotIndex}");
                    }
                    if (isTrade && Dalamud.Bindings.ImGui.ImGui.BeginPopup($"##contextMenu{slotIndex}"))
                    {
                        if (Dalamud.Bindings.ImGui.ImGui.MenuItem("Trade"))
                        {
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
                        Dalamud.Bindings.ImGui.ImGui.EndPopup();
                    }

                    // Begin Drag Source
                    if (hasInvItem && Dalamud.Bindings.ImGui.ImGui.BeginDragDropSource())
                    {
                        DraggedItemSlot = slotIndex;
                        DraggedSlotContents = layout.inventorySlotContents; 
                        int payloadData = slotIndex;
                        ImGui.SetDragDropPayload("SLOT_MOVE", MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref payloadData, 1)), ImGuiCond.Always);
                        Dalamud.Bindings.ImGui.ImGui.Text($"Dragging Slot {slotIndex}");
                        Dalamud.Bindings.ImGui.ImGui.EndDragDropSource();
                    }

                    if (Dalamud.Bindings.ImGui.ImGui.BeginDragDropTarget())
                    {
                        var payload = Dalamud.Bindings.ImGui.ImGui.AcceptDragDropPayload("SLOT_MOVE");
                        unsafe
                        {
                            if (payload.IsNull && DraggedItemSlot.HasValue && DraggedSlotContents != null)
                            {
                                int sourceSlotIndex = *(int*)payload.Data;
                                var draggedItem = DraggedSlotContents[sourceSlotIndex];

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
                                    else
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
                        Dalamud.Bindings.ImGui.ImGui.EndDragDropTarget();
                    }

                    Dalamud.Bindings.ImGui.ImGui.EndGroup();
                    Dalamud.Bindings.ImGui.ImGui.PopID();

                    if (x < GridSize - 1)
                    {
                        Dalamud.Bindings.ImGui.ImGui.SameLine();
                    }
                }
            }
        }
    }
}
