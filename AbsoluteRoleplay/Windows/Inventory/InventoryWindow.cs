using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using InventoryTab;
using Networking;
using ProfileInventory = AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.Inventory;
using Vector2 = System.Numerics.Vector2;

namespace AbsoluteRP.Windows.Inventory
{
    public class InventoryWindow : Window, IDisposable
    {
        // Inventory tabs received from server
        public static List<CustomTab> inventoryTabs = new List<CustomTab>();

        // Tab management
        private static int? draggedTabIndex = null;
        private static bool tabsReordered = false;
        private static List<int> initialTabOrder = new List<int>();

        // New tab creation
        private bool showCreateTabPopup = false;
        private string newTabName = string.Empty;

        // Delete confirmation
        private static bool showDeleteConfirmation = false;
        private static int tabToDeleteIndex = -1;


        // Profile selection
        private static int selectedProfileIndex = -1;
        private static bool hasAutoFetched = false;

        public InventoryWindow() : base(
            "INVENTORY",
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(400, 400),
                MaximumSize = new Vector2(800, 900)
            };
        }

        public void Dispose() { }

        public override void Draw()
        {
            if (!Plugin.IsOnline())
                return;

            // Auto-fetch first profile's inventory when the window first opens
            if (!hasAutoFetched && ProfileWindow.profiles != null && ProfileWindow.profiles.Count > 0)
            {
                hasAutoFetched = true;
                selectedProfileIndex = ProfileWindow.profileIndex;
                // Only fetch if we don't already have tabs (they may have been loaded during profile fetch)
                if (inventoryTabs.Count == 0)
                {
                    DataSender.FetchProfile(Plugin.character, true, selectedProfileIndex, Plugin.plugin.playername, Plugin.plugin.playerworld, -1);
                }
            }

            // Profile selector dropdown
            DrawProfileSelector();

            ThemeManager.GradientSeparator();

            DrawInventoryTabs();
        }

        private void DrawProfileSelector()
        {
            try
            {
                var profiles = ProfileWindow.profiles;
                if (profiles == null || profiles.Count == 0)
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1), "No profiles available. Open your profile window first.");
                    return;
                }

                // Sync selected index with ProfileWindow on first draw or if out of range
                if (selectedProfileIndex < 0 || selectedProfileIndex >= profiles.Count)
                {
                    selectedProfileIndex = ProfileWindow.profileIndex;
                    if (selectedProfileIndex < 0 || selectedProfileIndex >= profiles.Count)
                        selectedProfileIndex = 0;
                }

                string currentName = selectedProfileIndex >= 0 && selectedProfileIndex < profiles.Count
                    ? (profiles[selectedProfileIndex].title ?? "New Profile")
                    : "Select Profile";
                if (string.IsNullOrEmpty(currentName)) currentName = "New Profile";

                ImGui.Text("Profile:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 100);
                using (var combo = ImRaii.Combo("##InvProfileSelect", currentName))
                {
                    if (combo)
                    {
                        for (int i = 0; i < profiles.Count; i++)
                        {
                            string label = profiles[i].title;
                            if (string.IsNullOrEmpty(label)) label = "New Profile";

                            if (ImGui.Selectable(label + "##invprof" + i, i == selectedProfileIndex))
                            {
                                if (i != selectedProfileIndex)
                                {
                                    selectedProfileIndex = i;
                                    ProfileWindow.profileIndex = i;
                                    ProfileWindow.CurrentProfile = profiles[i];

                                    // Clear and re-fetch inventory for this profile
                                    ClearTabs();
                                    ProfileWindow.Fetching = true;
                                    DataSender.FetchProfile(Plugin.character, true, i, Plugin.plugin.playername, Plugin.plugin.playerworld, -1);
                                }
                            }
                        }
                    }
                }
                ImGui.SameLine();
                if (ThemeManager.GhostButton("Equipment"))
                {
                    DataSender.SendFetchEquipment(Plugin.character, ProfileWindow.profileIndex);
                    Plugin.plugin.OpenEquipmentWindow();
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("InventoryWindow DrawProfileSelector Debug: " + ex.Message);
            }
        }

        private void DrawInventoryTabs()
        {
            // Save button at top
            if (ThemeManager.PillButton("Save Inventory"))
            {
                SaveAllInventory();
            }

            // If no inventory tabs, show create prompt
            if (inventoryTabs.Count == 0)
            {
                ImGui.Spacing();
                ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1), "No inventory tabs found for this profile.");
                ImGui.Spacing();
                ImGui.Text("Create your first inventory tab:");
                ImGui.InputText("##FirstInvTabName", ref newTabName, 100);
                ImGui.SameLine();
                if (ThemeManager.PillButton("Create Tab") && !string.IsNullOrWhiteSpace(newTabName))
                {
                    int nextIndex = GetNextTabIndex();
                    _ = CreateInventoryTab(newTabName, nextIndex);
                    newTabName = string.Empty;
                }
                return;
            }

            ImGui.Spacing();

            using (var tabBar = ImRaii.TabBar("InventoryNavigation"))
            {
                if (tabBar)
                {
                    for (int i = 0; i < inventoryTabs.Count; i++)
                    {
                        var tab = inventoryTabs[i];
                        bool isOpen = tab.IsOpen;
                        string uniqueId = $"{tab.Name}##{i}";

                        using (var tabItem = ImRaii.TabItem(uniqueId, ref isOpen))
                        {
                            // Drag-and-drop reordering on tab header
                            if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.SourceNoHoldToOpenOthers))
                            {
                                draggedTabIndex = i;
                                Span<byte> payloadSpan = stackalloc byte[sizeof(int)];
                                BitConverter.TryWriteBytes(payloadSpan, i);
                                ImGui.SetDragDropPayload("INV_TAB_REORDER", payloadSpan, ImGuiCond.Always);
                                ImGui.Text(tab.Name);
                                ImGui.EndDragDropSource();
                            }
                            if (ImGui.BeginDragDropTarget())
                            {
                                var payload = ImGui.AcceptDragDropPayload("INV_TAB_REORDER");
                                if (!payload.IsNull && draggedTabIndex.HasValue)
                                {
                                    int srcIdx = draggedTabIndex.Value;
                                    int dstIdx = i;
                                    if (srcIdx != dstIdx)
                                    {
                                        (inventoryTabs[srcIdx], inventoryTabs[dstIdx]) =
                                            (inventoryTabs[dstIdx], inventoryTabs[srcIdx]);

                                        if (srcIdx < initialTabOrder.Count && dstIdx < initialTabOrder.Count)
                                        {
                                            (initialTabOrder[srcIdx], initialTabOrder[dstIdx]) =
                                                (initialTabOrder[dstIdx], initialTabOrder[srcIdx]);
                                        }

                                        tabsReordered = true;
                                    }
                                    draggedTabIndex = null;
                                }
                                ImGui.EndDragDropTarget();
                            }

                            if (tabItem)
                            {
                                if (tab.Layout is InventoryLayout invLayout)
                                {
                                    ProfileInventory.RenderInventoryLayout(i, uniqueId, invLayout);
                                }
                            }
                        }

                        tab.IsOpen = isOpen;

                        // Handle tab close (X button clicked)
                        if (!isOpen)
                        {
                            tab.IsOpen = true; // reopen temporarily
                            tabToDeleteIndex = i;
                            showDeleteConfirmation = true;
                        }
                    }

                    // "+" button for creating new tab
                    if (ImGui.TabItemButton("  +  ##AddInvTab", ImGuiTabItemFlags.NoCloseWithMiddleMouseButton))
                    {
                        showCreateTabPopup = true;
                        newTabName = "";
                    }
                }
            }

            // Open popups outside the tab bar scope so ImGui IDs match
            if (showCreateTabPopup)
                ImGui.OpenPopup("New Inventory Tab");

            if (showCreateTabPopup)
            {
                bool showPopup = showCreateTabPopup;
                using (var popup = ImRaii.PopupModal("New Inventory Tab", ref showPopup, ImGuiWindowFlags.AlwaysAutoResize))
                {
                    showCreateTabPopup = showPopup;
                    if (popup)
                    {
                        ImGui.Text("Enter a name for the inventory tab:");
                        ImGui.InputText("##InvTabName", ref newTabName, 100);

                        if (ThemeManager.PillButton("Create") && !string.IsNullOrWhiteSpace(newTabName))
                        {
                            int newIndex = GetNextTabIndex();
                            _ = CreateInventoryTab(newTabName, newIndex);
                            showCreateTabPopup = false;
                            ImGui.CloseCurrentPopup();
                        }
                        ImGui.SameLine();
                        if (ThemeManager.GhostButton("Cancel"))
                        {
                            showCreateTabPopup = false;
                            ImGui.CloseCurrentPopup();
                        }
                    }
                }
            }

            // Delete confirmation popup (also outside tab bar scope)
            if (showDeleteConfirmation)
                ImGui.OpenPopup("Delete Inventory Tab");

            if (showDeleteConfirmation)
            {
                bool showPopup = showDeleteConfirmation;
                using (var popup = ImRaii.PopupModal("Delete Inventory Tab", ref showPopup, ImGuiWindowFlags.AlwaysAutoResize))
                {
                    showDeleteConfirmation = showPopup;
                    if (popup)
                    {
                        string tabName = (tabToDeleteIndex >= 0 && tabToDeleteIndex < inventoryTabs.Count)
                            ? inventoryTabs[tabToDeleteIndex].Name : "this tab";
                        ImGui.Text($"Are you sure you want to delete \"{tabName}\"?");
                        ImGui.TextColored(new System.Numerics.Vector4(1, 0.3f, 0.3f, 1), "This will permanently delete all items in this tab.");
                        ImGui.Spacing();
                        if (ThemeManager.DangerButton("Delete"))
                        {
                            DeleteInventoryTab(tabToDeleteIndex);
                            showDeleteConfirmation = false;
                            tabToDeleteIndex = -1;
                            ImGui.CloseCurrentPopup();
                        }
                        ImGui.SameLine();
                        if (ThemeManager.GhostButton("Cancel"))
                        {
                            showDeleteConfirmation = false;
                            tabToDeleteIndex = -1;
                            ImGui.CloseCurrentPopup();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the next available tab_index for the current profile.
        /// Must be higher than any existing tab (profile tabs + inventory tabs share the same index space).
        /// </summary>
        private static int GetNextTabIndex()
        {
            int maxIndex = -1;

            // Check profile tabs
            if (ProfileWindow.CurrentProfile?.customTabs != null)
            {
                foreach (var tab in ProfileWindow.CurrentProfile.customTabs)
                {
                    if (tab.Layout != null)
                    {
                        int idx = tab.Layout switch
                        {
                            BioLayout b => b.tabIndex,
                            DetailsLayout d => d.tabIndex,
                            GalleryLayout g => g.tabIndex,
                            InfoLayout inf => inf.tabIndex,
                            StoryLayout s => s.tabIndex,
                            InventoryLayout inv => inv.tabIndex,
                            TreeLayout tr => tr.tabIndex,
                            _ => -1
                        };
                        if (idx > maxIndex) maxIndex = idx;
                    }
                }
            }

            // Check inventory tabs
            foreach (var tab in inventoryTabs)
            {
                if (tab.Layout is InventoryLayout inv && inv.tabIndex > maxIndex)
                    maxIndex = inv.tabIndex;
            }

            return maxIndex + 1;
        }

        private static async Task CreateInventoryTab(string tabName, int index)
        {
            try
            {
                int profIdx = ProfileWindow.profileIndex;
                Plugin.PluginLog.Debug($"CreateInventoryTab: name='{tabName}', index={index}, profileIndex={profIdx}");
                await DataSender.CreateTab(Plugin.character, tabName, (int)LayoutTypes.Inventory, profIdx, index);
                // Server will send back the new tab via SendInventoryTab -> ReceiveInventoryTab
                // which adds it to inventoryTabs automatically.
                // If it doesn't arrive within a reasonable time, re-fetch the profile.
                await Task.Delay(1000);
                if (inventoryTabs.Count <= index)
                {
                    // Tab didn't arrive, re-fetch
                    Plugin.PluginLog.Debug("CreateInventoryTab: Tab not received, re-fetching profile");
                    ClearTabs();
                    DataSender.FetchProfile(Plugin.character, true, profIdx, Plugin.plugin.playername, Plugin.plugin.playerworld, -1);
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"CreateInventoryTab error: {ex.Message}");
            }
        }

        private static void DeleteInventoryTab(int tabListIndex)
        {
            try
            {
                if (tabListIndex < 0 || tabListIndex >= inventoryTabs.Count)
                    return;

                var tab = inventoryTabs[tabListIndex];
                if (tab.Layout is InventoryLayout invLayout)
                {
                    int tabIndex = invLayout.tabIndex;
                    int profIdx = ProfileWindow.profileIndex;
                    Plugin.PluginLog.Debug($"DeleteInventoryTab: tabIndex={tabIndex}, profileIndex={profIdx}");
                    DataSender.DeleteTab(Plugin.character, profIdx, tabIndex, (int)LayoutTypes.Inventory);
                    inventoryTabs.RemoveAt(tabListIndex);
                    OnTabsLoaded();
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"DeleteInventoryTab error: {ex.Message}");
            }
        }


        private void SaveAllInventory()
        {
            var character = Plugin.character;
            int profIdx = ProfileWindow.profileIndex;

            // Save tab reorder if tabs were rearranged
            if (tabsReordered)
            {
                var indexChanges = new List<(int oldIndex, int newIndex)>();
                for (int newIdx = 0; newIdx < inventoryTabs.Count; newIdx++)
                {
                    int oldIdx = -1;
                    if (inventoryTabs[newIdx].Layout is InventoryLayout invLayout)
                        oldIdx = invLayout.tabIndex;
                    if (oldIdx == -1) oldIdx = newIdx;
                    if (oldIdx != newIdx)
                        indexChanges.Add((oldIdx, newIdx));
                }
                if (indexChanges.Count > 0)
                    DataSender.SendTabReorder(character, profIdx, indexChanges);
                tabsReordered = false;
            }

            // Update tabIndex to current position and save each tab's items
            for (int i = 0; i < inventoryTabs.Count; i++)
            {
                if (inventoryTabs[i].Layout is InventoryLayout invLayout)
                {
                    invLayout.tabIndex = i;
                    var items = invLayout.inventorySlotContents.Values.ToList();
                    DataSender.SendItemOrder(character, profIdx, invLayout, items);
                }
            }
        }

        /// <summary>
        /// Called when inventory tabs are loaded from the server.
        /// Rebuilds the initial tab order tracking.
        /// </summary>
        public static void OnTabsLoaded()
        {
            initialTabOrder.Clear();
            tabsReordered = false;
            draggedTabIndex = null;
            for (int i = 0; i < inventoryTabs.Count; i++)
            {
                initialTabOrder.Add(i);
            }

            // Also sync trade window inventory tab list
            SyncTradeWindowTabs();
        }

        /// <summary>
        /// Syncs inventory tabs to the trade window for tab selection.
        /// </summary>
        public static void SyncTradeWindowTabs()
        {
            AbsoluteRP.Windows.Profiles.TradeWindow.inventoryTabs.Clear();
            AbsoluteRP.Windows.Profiles.TradeWindow.inventories.Clear();
            for (int i = 0; i < inventoryTabs.Count; i++)
            {
                if (inventoryTabs[i].Layout is InventoryLayout invLayout)
                {
                    AbsoluteRP.Windows.Profiles.TradeWindow.inventoryTabs.Add(
                        Tuple.Create(invLayout.tabIndex, invLayout.id, invLayout.tabName ?? invLayout.name ?? $"Inventory {i + 1}"));
                    AbsoluteRP.Windows.Profiles.TradeWindow.inventories.Add(invLayout);
                }
            }
        }

        /// <summary>
        /// Clears all inventory tabs (called on logout or profile change).
        /// </summary>
        public static void ClearTabs()
        {
            inventoryTabs.Clear();
            initialTabOrder.Clear();
            tabsReordered = false;
            draggedTabIndex = null;
            // Don't reset hasAutoFetched here — it's reset when the window reopens
            // or when profiles change via the dropdown
        }
    }
}
