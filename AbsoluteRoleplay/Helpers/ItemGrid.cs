using AbsoluteRP.Windows.Ect;
using AbsoluteRP.Windows.MainPanel.Views.Account;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Networking;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace AbsoluteRP.Helpers
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

        // Drag state
        public static int? dragSourceSlot = null;
        public static Dictionary<int, ItemDefinition> dragSourceDict = null;
        public static int? dragTargetSlot = null;
        public static Dictionary<int, ItemDefinition> dragTargetDict = null;
        public static bool isDragging = false;
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
        public static unsafe void DrawGrid(Plugin plugin, InventoryLayout layout, string targetPlayerName, string targetPlayerWorld, bool isTrade)
        {
            const int TradeGridWidth = 10;
            float windowWidth = ImGui.GetWindowWidth();
            float windowHeight = ImGui.GetWindowHeight();
            float reservedHeight = isTrade ? 180 : 40;
            float availableWidth = windowWidth - 20;
            float availableHeight = windowHeight - reservedHeight;
            float cellSize = MathF.Min(availableWidth / GridSize, availableHeight / GridSize);
            Vector2 iconCellSize = new Vector2(cellSize, cellSize);

            // --- TRADE GRID (TOP) ---
            if (isTrade)
            {
                if (layout.tradeSlotContents == null)
                    layout.tradeSlotContents = new Dictionary<int, ItemDefinition>();
                if (layout.traderSlotContents == null)
                    layout.traderSlotContents = new Dictionary<int, ItemDefinition>();

                ImGui.Dummy(new Vector2(0, 10));
                ImGui.Text("Sending");
                ImGui.Dummy(new Vector2(0, 10));

                ImGui.BeginGroup();
                for (int x = 0; x < TradeGridWidth; x++)
                {
                    int slotIndex = x;
                    ImGui.PushID($"send_{slotIndex}");
                    ImGui.BeginGroup();

                    Vector2 cellPos = ImGui.GetCursorScreenPos();

                    ImGui.GetWindowDrawList().AddRectFilled(
                        cellPos,
                        cellPos + iconCellSize,
                        ImGui.GetColorU32(new Vector4(0.2f, 0.2f, 0.5f, 1.0f))
                    );

                    bool hasTradeItem = layout.tradeSlotContents.ContainsKey(slotIndex) && layout.tradeSlotContents[slotIndex].name != string.Empty;
                    if (hasTradeItem)
                    {
                        int iconID = layout.tradeSlotContents[slotIndex].iconID;
                        if (IconCache.TryGetValue(iconID, out var texture) && texture != null && texture.Handle != IntPtr.Zero)
                            ImGui.Image(texture.Handle, iconCellSize);
                        else
                        {
                            ImGui.Image(UI.UICommonImage(UI.CommonImageTypes.blankPictureTab).Handle, iconCellSize);
                            _ = PreloadIconAsync(plugin, iconID);
                        }
                    }

                    ImGui.SetCursorScreenPos(cellPos);
                    ImGui.InvisibleButton($"##send_slot{slotIndex}", iconCellSize);

                    // Tooltip
                    if (ImGui.IsItemHovered() && hasTradeItem)
                    {
                        ImGui.BeginTooltip();
                        Misc.RenderHtmlColoredTextInline(layout.tradeSlotContents[slotIndex].name, 400);
                        ImGui.Spacing();
                        Misc.RenderHtmlElements(layout.tradeSlotContents[slotIndex].description, false, true, true, true, null, true);
                        ImGui.EndTooltip();
                    }

                    // Begin drag source for trade slot
                    if (hasTradeItem && ImGui.BeginDragDropSource())
                    {
                        DraggedItemSlot = slotIndex;
                        DraggedSlotContents = layout.tradeSlotContents;
                        Span<byte> payloadSpan = stackalloc byte[sizeof(int)];
                        BitConverter.TryWriteBytes(payloadSpan, slotIndex);
                        ImGui.SetDragDropPayload("SLOT_MOVE", payloadSpan, ImGuiCond.Always);
                        ImGui.Text($"Dragging Trade Slot {slotIndex}");
                        ImGui.EndDragDropSource();
                    }

                    // Accept drag from inventory
                    if (ImGui.BeginDragDropTarget())
                    {
                        var payload = ImGui.AcceptDragDropPayload("SLOT_MOVE");
                        if (!payload.IsNull && payload.Data != ImGuiPayloadPtr.Null && DraggedItemSlot.HasValue && DraggedSlotContents != null)
                        {
                            Span<byte> buffer = new Span<byte>((void*)payload.Data, sizeof(int));
                            int sourceSlotIndex = BitConverter.ToInt32(buffer);
                            if (DraggedSlotContents.ContainsKey(sourceSlotIndex))
                            {
                                var draggedItem = DraggedSlotContents[sourceSlotIndex];
                                if (DraggedSlotContents == layout.inventorySlotContents)
                                {
                                    bool tradeChanged = false;
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
                        ImGui.EndDragDropTarget();
                    }

                    // Context menu for trade slot
                    if (isTrade && hasTradeItem && ImGui.IsItemClicked(ImGuiMouseButton.Right))
                    {
                        ImGui.OpenPopup($"##tradeContextMenu{slotIndex}");
                    }
                    if (isTrade && ImGui.BeginPopup($"##tradeContextMenu{slotIndex}"))
                    {
                        if (ImGui.MenuItem("Remove from Trade"))
                        {
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

                    ImGui.GetWindowDrawList().AddRect(
                        cellPos,
                        cellPos + iconCellSize,
                        ImGui.GetColorU32(new Vector4(0.0f, 0.5f, 0.2f, 1.0f)), 2.0f
                    );

                    bool hasRecvItem = layout.traderSlotContents != null &&
                                       layout.traderSlotContents.ContainsKey(slotIndex) &&
                                       layout.traderSlotContents[slotIndex].name != string.Empty;
                    if (hasRecvItem)
                    {
                        int iconID = layout.traderSlotContents[slotIndex].iconID;
                        if (IconCache.TryGetValue(iconID, out var texture) && texture != null && texture.Handle != IntPtr.Zero)
                            ImGui.Image(texture.Handle, iconCellSize);
                        else
                        {
                            ImGui.Image(UI.UICommonImage(UI.CommonImageTypes.blankPictureTab).Handle, iconCellSize);
                            _ = PreloadIconAsync(plugin, iconID);
                        }
                    }

                    ImGui.SetCursorScreenPos(cellPos);
                    ImGui.InvisibleButton($"##recv_slot{slotIndex}", iconCellSize);

                    if (ImGui.IsItemHovered() && hasRecvItem)
                    {
                        ImGui.BeginTooltip();
                        Misc.RenderHtmlColoredTextInline(layout.traderSlotContents[slotIndex].name, 400);
                        ImGui.Spacing();
                        Misc.RenderHtmlElements(layout.traderSlotContents[slotIndex].description, false, true, true, true, null, true);
                        ImGui.EndTooltip();
                    }

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
            for (int y = 0; y < GridSize; y++)
            {
                for (int x = 0; x < GridSize; x++)
                {
                    int slotIndex = y * GridSize + x;

                    ImGui.PushID(slotIndex);
                    ImGui.BeginGroup();

                    Vector2 cellPos = ImGui.GetCursorScreenPos();

                    ImGui.GetWindowDrawList().AddRectFilled(
                        cellPos,
                        cellPos + iconCellSize,
                        ImGui.GetColorU32(new Vector4(0.3f, 0.3f, 0.3f, 1.0f))
                    );

                    bool hasInvItem = layout.inventorySlotContents.ContainsKey(slotIndex) && layout.inventorySlotContents[slotIndex].name != string.Empty;
                    if (hasInvItem)
                    {
                        int iconID = layout.inventorySlotContents[slotIndex].iconID;
                        if (IconCache.TryGetValue(iconID, out var texture) && texture != null && texture.Handle != IntPtr.Zero)
                            ImGui.Image(texture.Handle, iconCellSize);
                        else
                        {
                            ImGui.Image(UI.UICommonImage(UI.CommonImageTypes.blankPictureTab).Handle, iconCellSize);
                            _ = PreloadIconAsync(plugin, iconID);
                        }
                    }

                    ImGui.SetCursorScreenPos(cellPos);
                    ImGui.InvisibleButton($"##slot{slotIndex}", iconCellSize);

                    if (ImGui.IsItemHovered() && hasInvItem)
                    {
                        ImGui.BeginTooltip();
                        int iconID = layout.inventorySlotContents[slotIndex].iconID;
                        if (IconCache.TryGetValue(iconID, out var texture) && texture != null && texture.Handle != IntPtr.Zero)
                            ImGui.Image(texture.Handle, iconCellSize);
                        else
                        {
                            ImGui.Image(UI.UICommonImage(UI.CommonImageTypes.blankPictureTab).Handle, iconCellSize);
                            _ = PreloadIconAsync(plugin, iconID);
                        }
                        Misc.RenderHtmlColoredTextInline(layout.inventorySlotContents[slotIndex].name, 500);
                        ImGui.Separator();
                        ImGui.Spacing();
                        Misc.RenderHtmlElements(layout.inventorySlotContents[slotIndex].description, false, true, true, true, null, true);
                        ImGui.EndTooltip();
                    }

                    if (!isTrade)
                    {
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
                        DraggedItemSlot = slotIndex;
                        DraggedSlotContents = layout.inventorySlotContents;
                        Span<byte> payloadSpan = stackalloc byte[sizeof(int)];
                        BitConverter.TryWriteBytes(payloadSpan, slotIndex);
                        ImGui.SetDragDropPayload("SLOT_MOVE", payloadSpan, ImGuiCond.Always);
                        ImGui.Text($"Dragging Slot {slotIndex}");
                        ImGui.EndDragDropSource();
                    }

                    if (ImGui.BeginDragDropTarget())
                    {
                        var payload = ImGui.AcceptDragDropPayload("SLOT_MOVE");
                        if (!payload.IsNull && payload.Data != ImGuiPayloadPtr.Null && DraggedItemSlot.HasValue && DraggedSlotContents != null)
                        {
                            Span<byte> buffer = new Span<byte>((void*)payload.Data, sizeof(int));
                            int sourceSlotIndex = BitConverter.ToInt32(buffer);
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
