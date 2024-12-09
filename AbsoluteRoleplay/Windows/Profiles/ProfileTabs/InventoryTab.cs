using AbsoluteRoleplay.Defines;
using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Windows.MainPanel.Views.Account;
using Dalamud.Interface;
using Dalamud.Interface.Internal.Windows.Data.Widgets;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using ImGuiNET;
using ImGuiScene;
using Lumina.Data.Files;
using Lumina.Extensions;
using OtterGui;
using System;
using System.Collections.Generic;
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
        public static Dictionary<int, string> slotContents = new(); // Slot contents, indexed by slot number
        private static int? currentTabInitialized = null; // Tracks the currently initialized tab
        public static bool isIconBrowserOpen;
        public static string itemName = string.Empty;
        public static string itemDescription = string.Empty;
        public static IDalamudTextureWrap icon;
        private static bool itemCreation;
        public static int selectedItemType = 0;

        public static void InitInventory(int type)
        {
            icon = UI.UICommonImage(UI.CommonImageTypes.blank);
            for (int i = 0; i < TotalSlots; i++)
            {
                slotContents[i] = null; // Empty slots
            }
            //if base item inventory
            if (type == 0)
            {
                slotContents[0] = "Item1";
                slotContents[1] = "Item2";
                slotContents[15] = "Item3";
            }
            //if treasures item inventory
            if (type == 1)
            {
                slotContents[5] = "Item1";
                slotContents[20] = "Item2";
                slotContents[15] = "Item3";
            }
            //if quest item inventory
            if (type == 2)
            {
                slotContents[35] = "Item1";
                slotContents[15] = "Item2";
                slotContents[99]= "Item3";
            }
        }
        public unsafe static void LoadInventoryTab(Plugin plugin)
        {
            if(itemCreation == false)
            {
                if (ImGui.Button("Create Item"))
                {
                    itemCreation = true;
                }
            }
            
            if (itemCreation)
            {
                LoadItemCreation(plugin);
            }         
            else
            {

                ImGui.BeginTabBar("ProfileNavigation");

                // Items Tab
                if (ImGui.BeginTabItem("Items"))
                {
                    if (currentTabInitialized != 0)
                    {
                        InitInventory(0);
                        currentTabInitialized = 0;
                    }
                    ImGui.EndTabItem();
                }
                // Treasures Tab
                if (ImGui.BeginTabItem("Treasures"))
                {
                    if (currentTabInitialized != 1)
                    {
                        InitInventory(1);
                        currentTabInitialized = 1;
                    }
                    ImGui.EndTabItem();
                }
                // Quests Tab
                if (ImGui.BeginTabItem("Quests"))
                {
                    if (currentTabInitialized != 2)
                    {
                        InitInventory(2);
                        currentTabInitialized = 2;
                    }
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();

                // Render Inventory Grid
                for (int y = 0; y < GridSize; y++)
                {
                    for (int x = 0; x < GridSize; x++)
                    {
                        int slotIndex = y * GridSize + x;

                        ImGui.PushID(slotIndex);

                        // Render a placeholder button for empty slots
                        if (slotContents[slotIndex] == null)
                        {
                            ImGui.Button("##Empty", new Vector2(50, 50));
                        }
                        else
                        {
                            ImGui.Button(slotContents[slotIndex], new Vector2(50, 50));
                        }

                        // Start dragging
                        if (ImGui.BeginDragDropSource())
                        {
                            draggedSlot = slotIndex;
                            unsafe
                            {
                                ImGui.SetDragDropPayload("SLOT_MOVE", new IntPtr(&slotIndex), sizeof(int));
                            }
                            ImGui.Text(slotContents[slotIndex] ?? "<Empty>");
                            plugin.logger.Error(slotIndex.ToString());
                            ImGui.EndDragDropSource();
                        }

                        // Accept drop
                        if (ImGui.BeginDragDropTarget())
                        {
                            var payload = ImGui.AcceptDragDropPayload("SLOT_MOVE");
                            if (payload.NativePtr != null && payload.Data != IntPtr.Zero && draggedSlot.HasValue)
                            {
                                unsafe
                                {
                                    // Retrieve the source slot index
                                    int sourceSlotIndex = *(int*)payload.Data.ToPointer();

                                    // Move content from the source to the target slot
                                    slotContents[slotIndex] = slotContents[sourceSlotIndex];
                                    slotContents[sourceSlotIndex] = null;

                                    // Reset draggedSlot
                                    draggedSlot = null;
                                }
                            }
                            ImGui.EndDragDropTarget();
                        }

                        ImGui.PopID();

                        if (x < GridSize - 1)
                            ImGui.SameLine();
                    }
                }
            }
        }

        private static void LoadItemCreation(Plugin plugin)
        {
            ImGui.Image(icon.ImGuiHandle,new Vector2(50,50));
            if (ImGui.Button("Change Icon"))
            {
                isIconBrowserOpen = true;
            }
            ImGui.Text("Name:");
            ImGui.InputTextWithHint("##Name", "Item Name", ref itemName, 100);
            ImGui.Text("Description:");
            ImGui.InputTextMultiline("##Description", ref itemDescription, 5000, new Vector2(ImGui.GetWindowSize().X - 20, ImGui.GetWindowSize().Y / 5));
            ImGui.Text("Item Type");
            AddItemCategorySelection();
            if (ImGui.Button("Create"))
            {
                itemCreation = false;
            }
            if (isIconBrowserOpen)
            {
                if (!WindowOperations.iconsLoaded)
                {
                    WindowOperations.LoadIconsLazy(plugin); // Load a small batch of icons
                }

                WindowOperations.RenderIcons(plugin);
            }
        }


        public static void AddItemCategorySelection()
        {
            var (text, desc) = Items.InventoryTypes[selectedItemType];
            using var combo = OtterGui.Raii.ImRaii.Combo("##ItemCategory", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;

            foreach (var ((newText, newDesc), idx) in Items.InventoryTypes.WithIndex())
            {
                if (idx != (int)UI.Personalities.None)
                {
                    if (ImGui.Selectable(newText, idx == selectedItemType))
                        selectedItemType = idx;

                    ImGuiUtil.SelectableHelpMarker(newDesc);
                }
            }
        }

    }
}
