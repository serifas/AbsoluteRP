using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using InventoryTab;
using Networking;
using AbsoluteRP.RsUI.Pages;
using ProfileInventory = AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.Inventory;
using Vector2 = System.Numerics.Vector2;
using AbsoluteRP.Network;
using AbsoluteRP.RsUI;
using Dalamud.Interface;

namespace AbsoluteRP.Windows.Inventory
{
    public class InventoryWindow : Window, IDisposable
    {
        // Singleton reference so the hub page can render this window's content
        public static InventoryWindow? Instance;

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
            Instance = this;
        }

        public void Dispose() { }

        public override void OnClose()
        {
            hasAutoFetched = false;
        }

        public override void Draw()
        {
            DrawContent();
        }

        // Shared draw body, also hosted by the hub's Inventory page
        public void DrawContent()
        {
            if (!Plugin.IsOnline())
                return;

            // Auto-fetch inventory when window opens and tabs haven't been loaded yet
            if (!hasAutoFetched && Plugin.character != null && ProfilesPage.profiles != null && ProfilesPage.profiles.Count > 0)
            {
                hasAutoFetched = true;
                selectedProfileIndex = ProfilesPage.profileIndex;
                if (selectedProfileIndex < 0) selectedProfileIndex = 0;
                Profiles_DS.FetchProfile(Plugin.character, true, selectedProfileIndex, Plugin.plugin.playername, Plugin.plugin.playerworld, -1);
            }

            // Profile selector + actions row
            DrawProfileSelector();

            InvUI.Divider();

            DrawInventoryTabs();
        }

        private void DrawProfileSelector()
        {
            try
            {
                var profiles = ProfilesPage.profiles;
                if (profiles == null || profiles.Count == 0)
                {
                    InvUI.Notice("No profiles available. Open your profile window first.", RsTheme.AccentWarning);
                    return;
                }

                // Sync selected index with ProfileWindow on first draw or if out of range
                if (selectedProfileIndex < 0 || selectedProfileIndex >= profiles.Count)
                {
                    selectedProfileIndex = ProfilesPage.profileIndex;
                    if (selectedProfileIndex < 0 || selectedProfileIndex >= profiles.Count)
                        selectedProfileIndex = 0;
                }

                if (profileNames == null)
                    profileNames = new List<string>(profiles.Count);
                else
                    profileNames.Clear();
                for (int i = 0; i < profiles.Count; i++)
                {
                    string label = profiles[i].title;
                    if (string.IsNullOrEmpty(label)) label = "New Profile";
                    profileNames.Add(label);
                }

                // Label + dropdown, with Equipment / Save on the right.
                var equipW = RsElements.MeasureButtonWidth("Equipment");
                var saveW = RsElements.MeasureButtonWidth("Save Inventory");
                var gap = InvUI.S(8f);
                var avail = RsElements.AvailContentWidth();
                var labelW = ImGui.CalcTextSize("Profile").X + gap;
                var ddWidth = Math.Max(InvUI.S(140f), avail - labelW - equipW - saveW - gap * 2f);

                var rowTop = ImGui.GetCursorPosY();
                var ddH = ImGui.GetTextLineHeight() + InvUI.S(8f) * 2f;
                ImGui.SetCursorPosY(rowTop + Math.Max(0f, (ddH - ImGui.GetTextLineHeight()) * 0.5f));
                InvUI.Muted("Profile");
                ImGui.SameLine(0f, gap);
                ImGui.SetCursorPosY(rowTop);

                int pick = selectedProfileIndex;
                // Dropdown scales its width itself, so hand it the design-space value.
                if (RsElements.Dropdown("inv_profile", ref pick, profileNames, ddWidth / Math.Max(0.01f, RsTheme.Scale)))
                {
                    if (pick >= 0 && pick < profiles.Count && pick != selectedProfileIndex)
                    {
                        selectedProfileIndex = pick;
                        ProfilesPage.profileIndex = pick;
                        ProfilesPage.CurrentProfile = profiles[pick];

                        // Clear and re-fetch inventory for this profile
                        ClearTabs();
                        ProfilesPage.Fetching = true;
                        Profiles_DS.FetchProfile(Plugin.character, true, pick, Plugin.plugin.playername, Plugin.plugin.playerworld, -1);
                    }
                }
                ImGui.SameLine(0f, gap);
                ImGui.SetCursorPosY(rowTop);
                if (RsElements.Button("Equipment##inv_equipment", RsElements.ButtonVariant.Secondary))
                {
                    Equipment_DS.SendFetchEquipment(Plugin.character, ProfilesPage.profileIndex);
                    Plugin.plugin.OpenEquipmentWindow();
                }
                ImGui.SameLine(0f, gap);
                ImGui.SetCursorPosY(rowTop);
                if (RsElements.Button("Save Inventory##inv_save", RsElements.ButtonVariant.Primary))
                {
                    SaveAllInventory();
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("InventoryWindow DrawProfileSelector Debug: " + ex.Message);
            }
        }

        private static List<string>? profileNames;
        private static int selectedTab = 0;
        private static readonly List<RsElements.NavItem> navItems = new();

        private void DrawInventoryTabs()
        {
            // If no inventory tabs, show create prompt
            if (inventoryTabs.Count == 0)
            {
                InvUI.Gap(4f);
                if (!RsElements.BeginPanel("inv_first_tab", "No inventory tabs yet", fitContentsX: false, fitContentsY: true))
                {
                    RsElements.EndPanel();
                    return;
                }
                try
                {
                    InvUI.Wrapped("This profile has no inventory tabs. Create your first one to start adding items.", RsTheme.TextSecondary);
                    InvUI.SectionLabel("Tab name");
                    var createW = RsElements.MeasureButtonWidth("Create Tab");
                    var inputW = Math.Max(InvUI.S(120f), RsElements.AvailContentWidth() - createW - InvUI.S(8f));
                    RsElements.InputText("inv_first_tab_name", ref newTabName, 100, "e.g. Belongings", inputW / Math.Max(0.01f, RsTheme.Scale));
                    ImGui.SameLine(0f, InvUI.S(8f));
                    if (RsElements.Button("Create Tab##inv_first_create", RsElements.ButtonVariant.Primary) && !string.IsNullOrWhiteSpace(newTabName))
                    {
                        int nextIndex = GetNextTabIndex();
                        _ = CreateInventoryTab(newTabName, nextIndex);
                        newTabName = string.Empty;
                    }
                }
                finally { RsElements.EndPanel(); }
                return;
            }

            InvUI.Gap(2f);

            if (selectedTab < 0 || selectedTab >= inventoryTabs.Count) selectedTab = 0;

            navItems.Clear();
            for (int i = 0; i < inventoryTabs.Count; i++)
            {
                var name = inventoryTabs[i].Name;
                if (string.IsNullOrEmpty(name)) name = $"Inventory {i + 1}";
                navItems.Add(new RsElements.NavItem(FontAwesomeIcon.BoxOpen, name));
            }

            // Tab strip with a "+" beside it for new tabs.
            var stripStart = ImGui.GetCursorScreenPos();
            int picked = selectedTab;
            RsElements.NavigationMenu("inv_tabs", ref picked, navItems,
                onClose: i => { tabToDeleteIndex = i; showDeleteConfirmation = true; },
                onReorder: MoveTab);
            if (picked >= 0 && picked < inventoryTabs.Count) selectedTab = picked;
            var stripEnd = ImGui.GetItemRectMax();
            var afterStrip = ImGui.GetCursorScreenPos();
            var stripH = Math.Max(0f, afterStrip.Y - stripStart.Y);

            var plusSize = 28f;
            var plusPx = InvUI.S(plusSize);
            ImGui.SetCursorScreenPos(new Vector2(stripEnd.X + InvUI.S(6f), stripStart.Y + Math.Max(0f, (stripH - plusPx) * 0.5f)));
            if (RsElements.IconButton(FontAwesomeIcon.Plus, "inv_add_tab", RsElements.ButtonVariant.Ghost, plusSize))
            {
                showCreateTabPopup = true;
                newTabName = "";
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("New inventory tab");
            ImGui.SetCursorScreenPos(new Vector2(stripStart.X, stripStart.Y + Math.Max(stripH, plusPx)));

            // Selected tab body
            if (selectedTab >= 0 && selectedTab < inventoryTabs.Count)
            {
                var tab = inventoryTabs[selectedTab];
                tab.IsOpen = true;
                string uniqueId = $"{tab.Name}##{selectedTab}";
                if (tab.Layout is InventoryLayout invLayout)
                {
                    ProfileInventory.RenderInventoryLayout(selectedTab, uniqueId, invLayout);
                }
            }

            // Open popups outside the tab strip scope so ImGui IDs match
            if (showCreateTabPopup)
                ImGui.OpenPopup("New Inventory Tab");

            if (showCreateTabPopup)
            {
                bool showPopup = showCreateTabPopup;
                if (InvUI.BeginModal("New Inventory Tab", ref showPopup))
                {
                    try
                    {
                        showCreateTabPopup = showPopup;
                        InvUI.Muted("Enter a name for the inventory tab");
                        RsElements.InputText("inv_tab_name", ref newTabName, 100, "Tab name", 300f);
                        InvUI.Gap(4f);

                        if (RsElements.Button("Create##inv_tab_create", RsElements.ButtonVariant.Primary) && !string.IsNullOrWhiteSpace(newTabName))
                        {
                            int newIndex = GetNextTabIndex();
                            _ = CreateInventoryTab(newTabName, newIndex);
                            showCreateTabPopup = false;
                            ImGui.CloseCurrentPopup();
                        }
                        ImGui.SameLine();
                        if (RsElements.Button("Cancel##inv_tab_cancel", RsElements.ButtonVariant.Ghost))
                        {
                            showCreateTabPopup = false;
                            ImGui.CloseCurrentPopup();
                        }
                    }
                    finally { InvUI.EndModal(); }
                }
                else
                {
                    showCreateTabPopup = showPopup;
                }
            }

            // Delete confirmation popup (also outside tab strip scope)
            if (showDeleteConfirmation)
                ImGui.OpenPopup("Delete Inventory Tab");

            if (showDeleteConfirmation)
            {
                bool showPopup = showDeleteConfirmation;
                if (InvUI.BeginModal("Delete Inventory Tab", ref showPopup))
                {
                    try
                    {
                        showDeleteConfirmation = showPopup;
                        string tabName = (tabToDeleteIndex >= 0 && tabToDeleteIndex < inventoryTabs.Count)
                            ? inventoryTabs[tabToDeleteIndex].Name : "this tab";
                        InvUI.Primary($"Are you sure you want to delete \"{tabName}\"?");
                        InvUI.Text("This will permanently delete all items in this tab.", RsTheme.AccentDanger);
                        InvUI.Gap(4f);
                        if (RsElements.Button("Delete##inv_tab_delete", RsElements.ButtonVariant.Danger))
                        {
                            DeleteInventoryTab(tabToDeleteIndex);
                            showDeleteConfirmation = false;
                            tabToDeleteIndex = -1;
                            ImGui.CloseCurrentPopup();
                        }
                        ImGui.SameLine();
                        if (RsElements.Button("Cancel##inv_tab_delete_cancel", RsElements.ButtonVariant.Ghost))
                        {
                            showDeleteConfirmation = false;
                            tabToDeleteIndex = -1;
                            ImGui.CloseCurrentPopup();
                        }
                    }
                    finally { InvUI.EndModal(); }
                }
                else
                {
                    showDeleteConfirmation = showPopup;
                }
            }
        }

        // Drag-reorder from the tab strip: move a tab to a new position and keep the initial-order bookkeeping in step so Save can diff it.
        private static void MoveTab(int from, int to)
        {
            if (from < 0 || from >= inventoryTabs.Count) return;
            if (to < 0 || to >= inventoryTabs.Count) return;
            if (from == to) return;

            var tab = inventoryTabs[from];
            inventoryTabs.RemoveAt(from);
            inventoryTabs.Insert(to, tab);

            if (from < initialTabOrder.Count && to < initialTabOrder.Count)
            {
                var o = initialTabOrder[from];
                initialTabOrder.RemoveAt(from);
                initialTabOrder.Insert(to, o);
            }

            tabsReordered = true;
            selectedTab = to;
        }

        /// Gets the next available tab_index for the current profile. Must be higher than any existing tab (profile tabs + inventory tabs share the same index space).
        private static int GetNextTabIndex()
        {
            int maxIndex = -1;

            // Check profile tabs
            if (ProfilesPage.CurrentProfile?.customTabs != null)
            {
                foreach (var tab in ProfilesPage.CurrentProfile.customTabs)
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
                int profIdx = ProfilesPage.profileIndex;
                Plugin.PluginLog.Debug($"CreateInventoryTab: name='{tabName}', index={index}, profileIndex={profIdx}");
                await ProfileTabs_DS.CreateTab(Plugin.character, tabName, (int)LayoutTypes.Inventory, profIdx, index);
                // Server will send back the new tab via SendInventoryTab -> ReceiveInventoryTab which adds it to inventoryTabs automatically. If it doesn't arrive within a reasonable time, re-fetch the profile.
                await Task.Delay(1000);
                if (inventoryTabs.Count <= index)
                {
                    // Tab didn't arrive, re-fetch
                    Plugin.PluginLog.Debug("CreateInventoryTab: Tab not received, re-fetching profile");
                    ClearTabs();
                    Profiles_DS.FetchProfile(Plugin.character, true, profIdx, Plugin.plugin.playername, Plugin.plugin.playerworld, -1);
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
                    int profIdx = ProfilesPage.profileIndex;
                    Plugin.PluginLog.Debug($"DeleteInventoryTab: tabIndex={tabIndex}, profileIndex={profIdx}");
                    ProfileTabs_DS.DeleteTab(Plugin.character, profIdx, tabIndex, (int)LayoutTypes.Inventory);
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
            int profIdx = ProfilesPage.profileIndex;

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
                    ProfileTabs_DS.SendTabReorder(character, profIdx, indexChanges);
                tabsReordered = false;
            }

            // Update tabIndex to current position and save each tab's items
            for (int i = 0; i < inventoryTabs.Count; i++)
            {
                if (inventoryTabs[i].Layout is InventoryLayout invLayout)
                {
                    invLayout.tabIndex = i;
                    var items = invLayout.inventorySlotContents.Values.ToList();
                    ProfileTabs_DS.SendItemOrder(character, profIdx, invLayout, items);
                }
            }
        }

        /// Called when inventory tabs are loaded from the server. Rebuilds the initial tab order tracking.
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

        /// Syncs inventory tabs to the trade window for tab selection.
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

        /// Clears all inventory tabs (called on logout or profile change).
        public static void ClearTabs()
        {
            inventoryTabs.Clear();
            initialTabOrder.Clear();
            tabsReordered = false;
            draggedTabIndex = null;
            // Don't reset hasAutoFetched here - it's reset when the window reopens or when profiles change via the dropdown
        }
    }
}
