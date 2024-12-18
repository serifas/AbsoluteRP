using AbsoluteRoleplay.Windows.Ect;
using AbsoluteRoleplay.Windows.MainPanel.Views.Account;
using AbsoluteRoleplay.Windows.Profiles;
using Dalamud.Interface.Textures.TextureWraps;
using ImGuiNET;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRoleplay.Helpers
{
    internal class ItemGrid
    {
        private const int GridSize = 10; // 10x10 grid for 200 slots
        private const int TotalSlots = GridSize * GridSize;

        private static bool isButtonBeingHeld;
        public static void DrawGrid(Plugin plugin, Dictionary<int, Defines.Item> slotContents, bool isTrade)
        {
            int hoveredSlotIndex = -1;

            // Draw the main grid
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

                    try
                    {
                        if (slotContents.ContainsKey(slotIndex) && slotContents[slotIndex].name != string.Empty)
                        {
                            ImGui.Image(WindowOperations.RenderIconAsync(plugin, slotContents[slotIndex].iconID).Result.ImGuiHandle, cellSize);
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

                    if (ImGui.IsItemHovered() && slotContents.ContainsKey(slotIndex) && slotContents[slotIndex].name != string.Empty)
                    {
                        hoveredSlotIndex = slotIndex;
                    }

                    if (ImGui.BeginDragDropSource())
                    {
                        unsafe
                        {
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
                            if (payload.NativePtr != null)
                            {
                                int sourceSlotIndex = *(int*)payload.Data.ToPointer();
                                if (slotContents.ContainsKey(slotIndex) && slotContents.ContainsKey(sourceSlotIndex))
                                {
                                    var temp = slotContents[slotIndex];
                                    slotContents[slotIndex] = slotContents[sourceSlotIndex];
                                    slotContents[sourceSlotIndex] = temp;
                                    slotContents[slotIndex].slot = slotIndex;
                                    List<Defines.Item> newItemList = new List<Defines.Item>();
                                    for (int i = 0; i < slotContents.Count; i++)
                                    {
                                        if (slotContents[i].name != string.Empty)
                                        {
                                            newItemList.Add(new Defines.Item
                                            {
                                                name = slotContents[i].name,
                                                description = slotContents[i].description,
                                                type = slotContents[i].type,
                                                subtype = slotContents[i].subtype,
                                                iconID = slotContents[i].iconID,
                                                slot = slotContents[i].slot,
                                            });
                                        }
                                    }

                                    DataSender.SendItemOrder(ProfileWindow.currentProfile, newItemList);
                                }
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

            if (hoveredSlotIndex >= 0 && slotContents.ContainsKey(hoveredSlotIndex) && slotContents[hoveredSlotIndex] != null)
            {
                var hoveredItem = slotContents[hoveredSlotIndex];
                ItemTooltip.item = hoveredItem;
                plugin.ItemTooltip.IsOpen = true;
            }
            else
            {
                plugin.ItemTooltip.IsOpen = false;
            }

            // Draw the trade grid if isTrade is true
            if (isTrade)
            {
                ImGui.Separator();
                const int tradeGridWidth = 10;
                const int tradeGridHeight = 2;
                int tradeStartIndex = GridSize * GridSize; // Offset for trade grid slots

                for (int y = 0; y < tradeGridHeight; y++)
                {
                    for (int x = 0; x < tradeGridWidth; x++)
                    {
                        int slotIndex = tradeStartIndex + y * tradeGridWidth + x;

                        ImGui.PushID(slotIndex);
                        ImGui.BeginGroup();

                        Vector2 cellPos = ImGui.GetCursorScreenPos();
                        Vector2 cellSize = new Vector2(50, 50);

                        ImGui.GetWindowDrawList().AddRectFilled(
                            cellPos,
                            cellPos + cellSize,
                            ImGui.GetColorU32(new Vector4(0.2f, 0.2f, 0.2f, 1.0f))
                        );

                        try
                        {
                            if (slotContents.ContainsKey(slotIndex) && slotContents[slotIndex].name != string.Empty)
                            {
                                ImGui.Image(WindowOperations.RenderIconAsync(plugin, slotContents[slotIndex].iconID).Result.ImGuiHandle, cellSize);
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

                        if (ImGui.BeginDragDropSource())
                        {
                            unsafe
                            {
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
                                if (payload.NativePtr != null)
                                {
                                    int sourceSlotIndex = *(int*)payload.Data.ToPointer();
                                    if (slotContents.ContainsKey(slotIndex) && slotContents.ContainsKey(sourceSlotIndex))
                                    {
                                        var temp = slotContents[slotIndex];
                                        slotContents[slotIndex] = slotContents[sourceSlotIndex];
                                        slotContents[sourceSlotIndex] = temp;
                                        slotContents[slotIndex].slot = slotIndex;
                                    }
                                }
                            }
                            ImGui.EndDragDropTarget();
                        }

                        ImGui.EndGroup();
                        ImGui.PopID();

                        if (x < tradeGridWidth - 1)
                        {
                            ImGui.SameLine();
                        }
                    }
                }
            }
        }

    }
}
