using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Ect;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.Internal.Windows;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using Networking;
using AbsoluteRP.Helpers;

namespace AbsoluteRP.Windows.Profiles
{
    public class TradeWindow : Window, IDisposable
    {
        public static InventoryLayout inventoryLayout = new InventoryLayout();
        public static string tradeTargetName = string.Empty;
        public static string tradeTargetWorld = string.Empty;
        public static bool receiverReady;
        public static bool senderReady;
        public static string receiverStatus = "Awaiting Confirmation...";
        public static string senderStatus = "Awaiting Confirmation...";
        public static bool showConfirmTradePopup = false;
        public int selectedTab = 0;
        //tabIndex / tabID / tabName
        public static List<Tuple<int, int, string>> inventoryTabs = new List<Tuple<int,int, string>>();
        public static List<InventoryLayout> inventories = new List<InventoryLayout>();
        public TradeWindow() : base(
       "TRADE", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(300, 300),
                MaximumSize = new Vector2(1200, 800)
            };
        }
        public override void Draw()
        {
            if (!Plugin.IsOnline())
                return;

            Vector2 windowSize = ImGui.GetWindowSize();
            float padding = 10f;
            float childWidth = (windowSize.X - padding * 3);
            float childHeight = windowSize.Y - 200;
            Vector2 childSize = new Vector2(childWidth, childHeight);

            ImGui.BeginGroup();
            using (var tradeTable = ImRaii.Child("TradeItems", childSize, true))
            {
                if (tradeTable)
                {
                    ItemGrid.DrawGrid(Plugin.plugin, inventoryLayout, tradeTargetName, tradeTargetWorld, true);
                }
            }
            // Inventory tab selector for choosing which inventory to use / receive into
            ImGui.Text("Your Inventory:");
            AddTabSelection(false);
            ImGui.Spacing();

            if (Misc.DrawCenteredButton("Confirm Trade"))
            {
                showConfirmTradePopup = true;
                ImGui.OpenPopup("ConfirmTradePopup");
            }
            ImGui.SameLine();
            if (Misc.DrawCenteredButton("Cancel Trade"))
            {
                DataSender.SendTradeStatus(Plugin.character, -1, inventoryLayout, tradeTargetName, tradeTargetWorld, false, true);
            }

            // Confirmation Popup
            if (showConfirmTradePopup)
            {
                ImGui.SetNextWindowSize(new Vector2(400, 180), ImGuiCond.Always);
                if (ImGui.BeginPopupModal("ConfirmTradePopup", ref showConfirmTradePopup, ImGuiWindowFlags.AlwaysAutoResize))
                {
                    if (inventoryTabs.Count == 0)
                    {
                        ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 1), "You have no inventory tabs. Create an inventory tab on your profile first.");
                    }
                    else
                    {
                        ImGui.Text("Choose which inventory will receive traded items:");
                        ImGui.Spacing();
                        AddTabSelection(true);
                        ImGui.Spacing();

                        if (ThemeManager.PillButton("Confirm Trade", new Vector2(140, 0)))
                        {
                            DataSender.SendTradeStatus(Plugin.character, inventoryTabs[selectedTab].Item1, inventoryLayout, tradeTargetName, tradeTargetWorld, true, false);
                            showConfirmTradePopup = false;
                            ImGui.CloseCurrentPopup();
                        }
                        ImGui.SameLine();
                    }
                    if (ThemeManager.GhostButton("Cancel", new Vector2(120, 0)))
                    {
                        showConfirmTradePopup = false;
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }
            }

            ImGui.Text($"{Plugin.plugin.playername} | ");
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudWhite, receiverStatus);

            ImGui.Text($"{tradeTargetName} | ");
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudWhite, senderStatus);

            ImGui.EndGroup();
        }

        public void AddTabSelection(bool finishingTrade)
        {
            try
            {
                List<string> inventoryNames = new List<string>();
                for (int i = 0; i < inventoryTabs.Count; i++)
                {
                    inventoryNames.Add(inventoryTabs[i].Item3);
                }
                string[] InventoryNames = new string[inventoryNames.Count];
                InventoryNames = inventoryNames.ToArray();
                var inventoryName = InventoryNames[selectedTab];

                using var combo = ImRaii.Combo("##Inventory", inventoryName);
                if (!combo)
                    return;
                foreach (var (newText, idx) in InventoryNames.WithIndex())
                {
                    if (inventoryNames.Count > 0)
                    {
                        var label = newText;
                        if (label == string.Empty)
                        {
                            label = "Inventory Tab (Required)";
                        }
                        if (newText != string.Empty)
                        {
                            if (ImGui.Selectable(label + "##" + idx, idx == selectedTab))
                            {
                                selectedTab = idx;
                                if (!finishingTrade)
                                {
                                    DataSender.SendInventorySelection(Plugin.character, inventoryTabs[idx].Item1, inventoryTabs[idx].Item2);
                                }
                            }
                            Helpers.UIHelpers.SelectableHelpMarker("Select an inventory to send and receive items.");
                        }

                       

                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("ProfileWindow AddProfileSelection Debug: " + ex.Message);
            }
        }

        public void Dispose()
        {
            foreach(var inventory in inventoryLayout.tradeSlotContents)
            {
                WindowOperations.SafeDispose(inventory.Value?.iconTexture);
                inventory.Value.iconTexture = null;
            }
            inventoryLayout.tradeSlotContents.Clear();
            foreach(var inventory in inventoryLayout.inventorySlotContents)
            {
                WindowOperations.SafeDispose(inventory.Value?.iconTexture);            
                inventory.Value.iconTexture = null;
            }
            inventoryLayout.inventorySlotContents.Clear();
            foreach(var inventory in inventoryLayout.traderSlotContents)
            {
                WindowOperations.SafeDispose(inventory.Value?.iconTexture);
                inventory.Value.iconTexture = null;
            }
        }
    }
   
}
