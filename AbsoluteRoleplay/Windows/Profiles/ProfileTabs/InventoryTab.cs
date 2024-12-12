using AbsoluteRoleplay.Defines;
using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Windows.Ect;
using AbsoluteRoleplay.Windows.MainPanel.Views.Account;
using Dalamud.Interface;
using Dalamud.Interface.Internal.Windows.Data.Widgets;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using ImGuiScene;
using Lumina.Data.Files;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using Networking;
using OtterGui;
using OtterGui.Text.EndObjects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AbsoluteRoleplay.Windows.Profiles.ProfileTabs
{
    internal class InventoryTab
    {
        private const int GridSize = 10; // 10x10 grid for 200 slots
        private const int TotalSlots = GridSize * GridSize;
        public static int? draggedSlot = null; // Slot currently being dragged
        public static Dictionary<int, Defines.Item> slotContents = new(); // Slot contents, indexed by slot number
        private static int? currentTabInitialized = null; // Tracks the currently initialized tab
        public static bool isIconBrowserOpen;
        public static string itemName = string.Empty;
        public static string itemDescription = string.Empty;
        public static IDalamudTextureWrap icon;
        private static bool itemCreation;
        public static int selectedItemType = 0;
        private static bool addsubtype = true;
        private static string[] itemSubType = Defines.Items.InventoryTypes[0].Item3;
        private static int selectedSubType = 0;
        public static uint createItemIconID = 0;
        private static int? draggingSlotIndex = null;
        private static Defines.Item draggingItem = null; // Holds the item being dragged

        public static IntPtr[] gridTextures = new IntPtr[100]; // Holds textures for each button
        public static IntPtr blankTexture; // The default blank texture handle
        public static int draggedIndex = -1; // The index of the dragged button
        public static IntPtr draggedTexture; // The texture being dragged
        public static bool isDragging = false; // Whether dragging is in progress
        public static Vector2 dragOffset; // Offset for the dragged image
        private static bool isButtonBeingHeld;

        public static void InitInventory(int type)
        {
            icon = UI.UICommonImage(UI.CommonImageTypes.blank);
            if (icon == null)
            {
                throw new InvalidOperationException("Failed to initialize icon.");
            }
            Defines.Item item = new Defines.Item
            {
                name = "Test",
                description = "Stupid Description",
                iconID = 10
            };

            slotContents.Clear();
            for (int i = 0; i < TotalSlots; i++)
            {
                slotContents[i] = null; // Empty slots
            }
            slotContents[0] = item;
            if (type == 1) // Treasures inventory
            {
                // Populate slots for treasures
            }
            else if (type == 2) // Quests inventory
            {
                // Populate slots for quests
            }
        }

        public static async Task LoadInventoryTabAsync(Plugin plugin)
        {
            int hoveredSlotIndex = -1;
            if (ImGui.Button("CreateItem"))
            {
                itemCreation = true;
            }
            if(itemCreation == true)
            {
               LoadItemCreation(plugin);
            }
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
                        ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 1.0f, 1.0f))
                    );
                    try
                    {                     

                        if (slotContents[slotIndex] != null)
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

                    if (ImGui.IsItemHovered() && slotContents.ContainsKey(slotIndex) && slotContents[slotIndex] != null)
                    {
                        hoveredSlotIndex = slotIndex;
                    }

                    if (ImGui.BeginDragDropSource())
                    {
                        draggedSlot = slotIndex;
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
        }


        private static void LoadItemCreation(Plugin plugin)
        {
            ImGui.Image(icon.ImGuiHandle, new Vector2(50, 50));

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

                if (ImGui.Button("Create"))
                {
                    itemCreation = false;
                    DataSender.SendItemCreation(ProfileWindow.currentProfile, itemName, itemDescription, selectedItemType, selectedSubType, createItemIconID);
                }
            }
            else
            {
                if (!WindowOperations.iconsLoaded)
                {
                    WindowOperations.LoadIconsLazy(plugin); // Load a small batch of icons
                }

                WindowOperations.RenderIcons(plugin);
            }
        }

        public static void AddItemCategorySelection(Plugin plugin)
        {
            var (text, desc, subType) = Defines.Items.InventoryTypes[selectedItemType];
            using var combo = OtterGui.Raii.ImRaii.Combo("##ItemCategory", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;

            foreach (var ((newText, newDesc, newSubtype), idx) in Defines.Items.InventoryTypes.WithIndex())
            {
                if (ImGui.Selectable(newText, idx == selectedItemType))
                {
                    selectedItemType = idx;
                    itemSubType = Defines.Items.InventoryTypes[idx].Item3;
                }
                ImGuiUtil.SelectableHelpMarker(newDesc);
            }
        }

        public static void AddItemSubtypeSelection(string[] subtype)
        {
            if (selectedSubType >= subtype.Length)
            {
                selectedSubType = 0;
            }
            var text = subtype[selectedSubType];
            using var combo = OtterGui.Raii.ImRaii.Combo("##ItemSubtype", text);
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
    }
}
