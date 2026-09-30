using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI;
using AbsoluteRP.RsUI.Pages;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using InventoryTab;
using AbsoluteRP.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AbsoluteRP.Windows.Inventory
{
    // RsUI-styled replacement for Helpers/ItemGrid.DrawGrid. Same behaviour (10x10 slots, drag-and-drop between slots / trade rows / equipment, tooltips, context menus, server persistence) with the tiles drawn as rounded RsTheme surfaces and a pop-in animation for items that appear in a list. Drag state is shared through ItemGrid's statics so EquipmentPage keeps accepting drops from here.
    internal static class InvGrid
    {
        private const int GridSize = 10;
        private const int TotalSlots = GridSize * GridSize;
        private const int TradeGridWidth = 10;

        private static readonly Dictionary<int, IDalamudTextureWrap> IconCache = new();
        private static readonly HashSet<int> LoadingIcons = new();

        // Slot whose context menu is open, drawn with the selection ring.
        private static int contextSlot = -1;
        private static Dictionary<int, ItemDefinition>? contextDict;

        private static async Task PreloadIconAsync(Plugin plugin, int iconID)
        {
            if (iconID <= 0) return;
            if (IconCache.ContainsKey(iconID) || LoadingIcons.Contains(iconID))
                return;

            LoadingIcons.Add(iconID);
            try
            {
                var texture = await WindowOperations.RenderIconAsync(plugin, iconID);
                if (texture != null)
                    IconCache[iconID] = texture;
            }
            catch { }
            finally { LoadingIcons.Remove(iconID); }
        }

        private static IDalamudTextureWrap? GetIcon(Plugin plugin, ItemDefinition item)
        {
            if (item.iconTexture != null && item.iconTexture.Handle != IntPtr.Zero)
                return item.iconTexture;
            if (IconCache.TryGetValue(item.iconID, out var texture) && texture != null && texture.Handle != IntPtr.Zero)
                return texture;
            _ = PreloadIconAsync(plugin, item.iconID);
            return null;
        }

        private static bool Has(Dictionary<int, ItemDefinition>? dict, int slot)
            => dict != null && dict.TryGetValue(slot, out var it) && InvUI.IsOccupied(it);

        private static int FirstEmpty(Dictionary<int, ItemDefinition> dict, int count)
        {
            for (int i = 0; i < count; i++)
                if (!dict.ContainsKey(i) || string.IsNullOrEmpty(dict[i].name))
                    return i;
            return -1;
        }

        private static List<ItemDefinition> OrderedItems(Dictionary<int, ItemDefinition> dict)
        {
            var list = new List<ItemDefinition>();
            for (int i = 0; i < TotalSlots; i++)
                if (dict.ContainsKey(i) && !string.IsNullOrEmpty(dict[i].name))
                    list.Add(dict[i]);
            return list;
        }

        private static void SendTrade(InventoryLayout layout, string targetPlayerName, string targetPlayerWorld)
        {
            Trades_DS.SendTradeUpdate(Plugin.character, ProfilesPage.profileIndex, targetPlayerName, targetPlayerWorld, layout, layout.tradeSlotContents.Values.ToList());
        }

        private static void RowHeader(string text, Vector4 color)
        {
            var dot = InvUI.S(6f);
            var p = ImGui.GetCursorScreenPos();
            var lh = ImGui.GetTextLineHeight();
            ImGui.GetWindowDrawList().AddCircleFilled(new Vector2(p.X + dot, p.Y + lh * 0.5f), dot * 0.5f, InvUI.U(color));
            ImGui.SetCursorScreenPos(new Vector2(p.X + dot * 2f + InvUI.S(4f), p.Y));
            InvUI.Text(text.ToUpperInvariant(), RsTheme.TextMuted);
        }

        public static unsafe void DrawGrid(Plugin plugin, InventoryLayout layout, string targetPlayerName, string targetPlayerWorld, bool isTrade)
        {
            if (layout == null) return;
            if (layout.inventorySlotContents == null)
                layout.inventorySlotContents = new Dictionary<int, ItemDefinition>();

            float windowHeight = ImGui.GetWindowHeight();
            float reservedHeight = isTrade ? 180 : 40;
            float availableWidth = Math.Max(InvUI.S(160f), ImGui.GetContentRegionAvail().X);
            float availableHeight = windowHeight - reservedHeight;
            float cellSize = MathF.Min(availableWidth / GridSize, availableHeight / GridSize);
            if (cellSize < InvUI.S(20f)) cellSize = InvUI.S(20f);
            float gap = Math.Max(2f, InvUI.S(4f));
            float tile = cellSize - gap;
            Vector2 tileSize = new Vector2(tile, tile);

            string invKey = $"inv_{layout.id}_{layout.tabIndex}";
            InvUI.Track(invKey, layout.inventorySlotContents);

            var draw = ImGui.GetWindowDrawList();

            // Unless a context popup is currently open, nothing is "selected".
            bool contextStillOpen = false;

            // TRADE ROWS (TOP)
            if (isTrade)
            {
                if (layout.tradeSlotContents == null)
                    layout.tradeSlotContents = new Dictionary<int, ItemDefinition>();
                if (layout.traderSlotContents == null)
                    layout.traderSlotContents = new Dictionary<int, ItemDefinition>();

                const string sendKey = "trade_send";
                const string recvKey = "trade_recv";
                InvUI.Track(sendKey, layout.tradeSlotContents);
                InvUI.Track(recvKey, layout.traderSlotContents);

                InvUI.Gap(4f);
                RowHeader("Sending", RsTheme.AccentPrimary);
                InvUI.Gap(4f);

                Vector2 rowStart = ImGui.GetCursorScreenPos();
                for (int x = 0; x < TradeGridWidth; x++)
                {
                    int slotIndex = x;
                    Vector2 cellPos = new Vector2(rowStart.X + x * cellSize, rowStart.Y);
                    ImGui.PushID($"send_{slotIndex}");
                    try
                    {
                        bool hasTradeItem = Has(layout.tradeSlotContents, slotIndex);
                        var item = hasTradeItem ? layout.tradeSlotContents[slotIndex] : null;
                        var texture = item != null ? GetIcon(plugin, item) : null;

                        ImGui.SetCursorScreenPos(cellPos);
                        ImGui.InvisibleButton($"##send_slot{slotIndex}", tileSize);
                        bool hovered = ImGui.IsItemHovered();
                        bool selected = contextDict == layout.tradeSlotContents && contextSlot == slotIndex;

                        InvUI.DrawTile(draw, cellPos, cellPos + tileSize, texture, item?.quality ?? 0, hovered, selected,
                                       hasTradeItem ? InvUI.Progress(sendKey, slotIndex) : 1f,
                                       surface: InvUI.Mix(RsTheme.BgTertiary, RsTheme.AccentPrimary, 0.12f),
                                       ring: hovered || selected ? (Vector4?)null : InvUI.Fade(RsTheme.AccentPrimary, 0.35f));

                        if (hovered && hasTradeItem)
                            InvUI.ItemTooltip(item!, texture);

                        // Begin drag source for trade slot
                        if (hasTradeItem && ImGui.BeginDragDropSource())
                        {
                            try
                            {
                                ItemGrid.DraggedItemSlot = slotIndex;
                                ItemGrid.DraggedSlotContents = layout.tradeSlotContents;
                                Span<byte> payloadSpan = stackalloc byte[sizeof(int)];
                                BitConverter.TryWriteBytes(payloadSpan, slotIndex);
                                ImGui.SetDragDropPayload("SLOT_MOVE", payloadSpan, ImGuiCond.Always);
                                InvUI.Text(item!.name, InvUI.QualityColor(item.quality));
                            }
                            finally { ImGui.EndDragDropSource(); }
                        }

                        // Accept drag from inventory
                        if (ImGui.BeginDragDropTarget())
                        {
                            try
                            {
                                var payload = ImGui.AcceptDragDropPayload("SLOT_MOVE");
                                if (!payload.IsNull && payload.Data != ImGuiPayloadPtr.Null && ItemGrid.DraggedItemSlot.HasValue && ItemGrid.DraggedSlotContents != null)
                                {
                                    Span<byte> buffer = new Span<byte>((void*)payload.Data, sizeof(int));
                                    int sourceSlotIndex = BitConverter.ToInt32(buffer);
                                    if (ItemGrid.DraggedSlotContents.ContainsKey(sourceSlotIndex))
                                    {
                                        var draggedItem = ItemGrid.DraggedSlotContents[sourceSlotIndex];
                                        if (ItemGrid.DraggedSlotContents == layout.inventorySlotContents)
                                        {
                                            if (hasTradeItem)
                                            {
                                                var targetItem = layout.tradeSlotContents[slotIndex];
                                                layout.tradeSlotContents[slotIndex] = draggedItem;
                                                layout.tradeSlotContents[slotIndex].slot = slotIndex;
                                                layout.inventorySlotContents[sourceSlotIndex] = targetItem;
                                                layout.inventorySlotContents[sourceSlotIndex].slot = sourceSlotIndex;
                                            }
                                            else
                                            {
                                                layout.tradeSlotContents[slotIndex] = draggedItem;
                                                layout.tradeSlotContents[slotIndex].slot = slotIndex;
                                                layout.inventorySlotContents.Remove(sourceSlotIndex);
                                            }
                                            SendTrade(layout, targetPlayerName, targetPlayerWorld);
                                        }
                                        ItemGrid.DraggedItemSlot = null;
                                        ItemGrid.DraggedSlotContents = null;
                                    }
                                }
                            }
                            finally { ImGui.EndDragDropTarget(); }
                        }

                        // Context menu for trade slot
                        if (hasTradeItem && ImGui.IsItemClicked(ImGuiMouseButton.Right))
                        {
                            ImGui.OpenPopup($"##tradeContextMenu{slotIndex}");
                            contextSlot = slotIndex;
                            contextDict = layout.tradeSlotContents;
                        }
                        if (InvUI.BeginPopup($"##tradeContextMenu{slotIndex}"))
                        {
                            try
                            {
                                if (contextDict == layout.tradeSlotContents && contextSlot == slotIndex) contextStillOpen = true;
                                if (InvUI.MenuItem("Remove from Trade"))
                                {
                                    int firstEmpty = FirstEmpty(layout.inventorySlotContents, TotalSlots);
                                    if (firstEmpty != -1 && layout.tradeSlotContents.ContainsKey(slotIndex))
                                    {
                                        var it = layout.tradeSlotContents[slotIndex];
                                        it.slot = firstEmpty;
                                        layout.inventorySlotContents[firstEmpty] = it;
                                        layout.tradeSlotContents.Remove(slotIndex);
                                        SendTrade(layout, targetPlayerName, targetPlayerWorld);
                                    }
                                    ImGui.CloseCurrentPopup();
                                }
                            }
                            finally { InvUI.EndPopup(); }
                        }
                    }
                    finally { ImGui.PopID(); }
                }
                ImGui.SetCursorScreenPos(new Vector2(rowStart.X, rowStart.Y + cellSize));
                ImGui.Dummy(new Vector2(cellSize * TradeGridWidth, 0f));

                InvUI.Gap(6f);
                RowHeader("Receiving", RsTheme.AccentSuccess);
                InvUI.Gap(4f);

                rowStart = ImGui.GetCursorScreenPos();
                for (int x = 0; x < TradeGridWidth; x++)
                {
                    int slotIndex = x;
                    Vector2 cellPos = new Vector2(rowStart.X + x * cellSize, rowStart.Y);
                    ImGui.PushID($"recv_{slotIndex}");
                    try
                    {
                        bool hasRecvItem = Has(layout.traderSlotContents, slotIndex);
                        var item = hasRecvItem ? layout.traderSlotContents[slotIndex] : null;
                        var texture = item != null ? GetIcon(plugin, item) : null;

                        ImGui.SetCursorScreenPos(cellPos);
                        ImGui.InvisibleButton($"##recv_slot{slotIndex}", tileSize);
                        bool hovered = ImGui.IsItemHovered();

                        InvUI.DrawTile(draw, cellPos, cellPos + tileSize, texture, item?.quality ?? 0, hovered, false,
                                       hasRecvItem ? InvUI.Progress(recvKey, slotIndex) : 1f,
                                       surface: InvUI.Mix(RsTheme.BgTertiary, RsTheme.AccentSuccess, 0.10f),
                                       ring: hovered ? (Vector4?)null : InvUI.Fade(RsTheme.AccentSuccess, 0.35f));

                        if (hovered && hasRecvItem)
                            InvUI.ItemTooltip(item!, texture);
                    }
                    finally { ImGui.PopID(); }
                }
                ImGui.SetCursorScreenPos(new Vector2(rowStart.X, rowStart.Y + cellSize));
                ImGui.Dummy(new Vector2(cellSize * TradeGridWidth, 0f));

                InvUI.Gap(6f);
                RowHeader("Inventory", RsTheme.TextSecondary);
                InvUI.Gap(4f);
            }

            // INVENTORY GRID (BOTTOM)
            Vector2 gridStart = ImGui.GetCursorScreenPos();
            for (int y = 0; y < GridSize; y++)
            {
                for (int x = 0; x < GridSize; x++)
                {
                    int slotIndex = y * GridSize + x;
                    Vector2 cellPos = new Vector2(gridStart.X + x * cellSize, gridStart.Y + y * cellSize);

                    ImGui.PushID(slotIndex);
                    try
                    {
                        bool hasInvItem = Has(layout.inventorySlotContents, slotIndex);
                        var item = hasInvItem ? layout.inventorySlotContents[slotIndex] : null;
                        var texture = item != null ? GetIcon(plugin, item) : null;

                        ImGui.SetCursorScreenPos(cellPos);
                        ImGui.InvisibleButton($"##slot{slotIndex}", tileSize);
                        bool hovered = ImGui.IsItemHovered();
                        bool selected = contextDict == layout.inventorySlotContents && contextSlot == slotIndex;

                        InvUI.DrawTile(draw, cellPos, cellPos + tileSize, texture, item?.quality ?? 0, hovered, selected,
                                       hasInvItem ? InvUI.Progress(invKey, slotIndex) : 1f);

                        if (hovered && hasInvItem)
                            InvUI.ItemTooltip(item!, texture);

                        if (!isTrade)
                        {
                            if (ImGui.IsItemClicked(ImGuiMouseButton.Right) && hasInvItem)
                            {
                                ImGui.OpenPopup($"##contextMenu{slotIndex}");
                                contextSlot = slotIndex;
                                contextDict = layout.inventorySlotContents;
                            }
                            if (InvUI.BeginPopup($"##contextMenu{slotIndex}"))
                            {
                                try
                                {
                                    if (contextDict == layout.inventorySlotContents && contextSlot == slotIndex) contextStillOpen = true;
                                    bool isLocked = layout.inventorySlotContents.ContainsKey(slotIndex) && layout.inventorySlotContents[slotIndex].locked;
                                    if (!isLocked)
                                    {
                                        if (InvUI.MenuItem("Edit"))
                                        {
                                            InvTab.BeginEditItem(layout, slotIndex);
                                            ImGui.CloseCurrentPopup();
                                        }
                                    }
                                    else
                                    {
                                        InvUI.MenuItem("Locked — cannot edit", enabled: false);
                                    }
                                    if (InvUI.MenuItem("Duplicate"))
                                    {
                                        int firstEmptySlotIndex = FirstEmpty(layout.inventorySlotContents, TotalSlots);
                                        if (firstEmptySlotIndex != -1 && layout.inventorySlotContents.ContainsKey(slotIndex))
                                        {
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
                                                iconTexture = itemToDuplicate.iconTexture,
                                                locked = itemToDuplicate.locked
                                            };
                                        }
                                        ImGui.CloseCurrentPopup();
                                    }
                                    if (InvUI.MenuItem("Equip"))
                                    {
                                        EquipmentPage.EquipFromContextMenu(layout, slotIndex);
                                        ImGui.CloseCurrentPopup();
                                    }
                                    if (InvUI.MenuItem("Delete", RsTheme.AccentDanger))
                                    {
                                        layout.inventorySlotContents.Remove(slotIndex);
                                        // Persist deletion to server
                                        ProfileTabs_DS.SendItemOrder(Plugin.character, ProfilesPage.profileIndex, layout, OrderedItems(layout.inventorySlotContents));
                                        ImGui.CloseCurrentPopup();
                                    }
                                }
                                finally { InvUI.EndPopup(); }
                            }
                        }
                        else
                        {
                            if (hasInvItem && ImGui.IsItemClicked(ImGuiMouseButton.Right))
                            {
                                ImGui.OpenPopup($"##contextMenu{slotIndex}");
                                contextSlot = slotIndex;
                                contextDict = layout.inventorySlotContents;
                            }
                            if (InvUI.BeginPopup($"##contextMenu{slotIndex}"))
                            {
                                try
                                {
                                    if (contextDict == layout.inventorySlotContents && contextSlot == slotIndex) contextStillOpen = true;
                                    if (InvUI.MenuItem("Add to Trade"))
                                    {
                                        int firstEmpty = FirstEmpty(layout.tradeSlotContents, TradeGridWidth);
                                        if (firstEmpty != -1 && layout.inventorySlotContents.ContainsKey(slotIndex))
                                        {
                                            var it = layout.inventorySlotContents[slotIndex];
                                            it.slot = firstEmpty;
                                            layout.tradeSlotContents[firstEmpty] = it;
                                            layout.inventorySlotContents.Remove(slotIndex);
                                            SendTrade(layout, targetPlayerName, targetPlayerWorld);
                                        }
                                        ImGui.CloseCurrentPopup();
                                    }
                                }
                                finally { InvUI.EndPopup(); }
                            }
                        }

                        // Begin Drag Source
                        if (hasInvItem && ImGui.BeginDragDropSource())
                        {
                            try
                            {
                                ItemGrid.DraggedItemSlot = slotIndex;
                                ItemGrid.DraggedSlotContents = layout.inventorySlotContents;
                                Span<byte> payloadSpan = stackalloc byte[sizeof(int)];
                                BitConverter.TryWriteBytes(payloadSpan, slotIndex);
                                ImGui.SetDragDropPayload("SLOT_MOVE", payloadSpan, ImGuiCond.Always);
                                InvUI.Text(item!.name, InvUI.QualityColor(item.quality));
                            }
                            finally { ImGui.EndDragDropSource(); }
                        }

                        if (ImGui.BeginDragDropTarget())
                        {
                            try
                            {
                                var payload = ImGui.AcceptDragDropPayload("SLOT_MOVE");
                                if (!payload.IsNull && payload.Data != ImGuiPayloadPtr.Null && ItemGrid.DraggedItemSlot.HasValue && ItemGrid.DraggedSlotContents != null)
                                {
                                    Span<byte> buffer = new Span<byte>((void*)payload.Data, sizeof(int));
                                    int sourceSlotIndex = BitConverter.ToInt32(buffer);
                                    ItemGrid.DraggedSlotContents.TryGetValue(sourceSlotIndex, out var draggedItem);

                                    if (draggedItem != null && ItemGrid.DraggedSlotContents == layout.inventorySlotContents && layout.inventorySlotContents.ContainsKey(sourceSlotIndex))
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
                                            ProfileTabs_DS.SendItemOrder(Plugin.character, ProfilesPage.profileIndex, layout, OrderedItems(layout.inventorySlotContents));
                                        }
                                    }
                                    else if (draggedItem != null && ItemGrid.DraggedSlotContents == layout.tradeSlotContents && layout.tradeSlotContents.ContainsKey(sourceSlotIndex))
                                    {
                                        if (layout.inventorySlotContents.ContainsKey(slotIndex) && layout.inventorySlotContents[slotIndex].name != string.Empty)
                                        {
                                            var targetItem = layout.inventorySlotContents[slotIndex];
                                            layout.inventorySlotContents[slotIndex] = draggedItem;
                                            layout.inventorySlotContents[slotIndex].slot = slotIndex;
                                            layout.tradeSlotContents[sourceSlotIndex] = targetItem;
                                            layout.tradeSlotContents[sourceSlotIndex].slot = sourceSlotIndex;
                                        }
                                        else
                                        {
                                            layout.inventorySlotContents[slotIndex] = draggedItem;
                                            layout.inventorySlotContents[slotIndex].slot = slotIndex;
                                            layout.tradeSlotContents.Remove(sourceSlotIndex);
                                        }
                                        SendTrade(layout, targetPlayerName, targetPlayerWorld);
                                    }

                                    ItemGrid.DraggedItemSlot = null;
                                    ItemGrid.DraggedSlotContents = null;
                                }
                            }
                            finally { ImGui.EndDragDropTarget(); }
                        }
                    }
                    finally { ImGui.PopID(); }
                }
            }

            // Advance the cursor past the grid so anything drawn after lands beneath it.
            ImGui.SetCursorScreenPos(new Vector2(gridStart.X, gridStart.Y + cellSize * GridSize));
            ImGui.Dummy(new Vector2(cellSize * GridSize, 0f));

            if (!contextStillOpen)
            {
                contextSlot = -1;
                contextDict = null;
            }
        }
    }
}
