using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Windows.Ect;
using AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows;
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
using AbsoluteRoleplay.Helpers;

namespace AbsoluteRoleplay.Windows.Profiles
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
            if (!Plugin.plugin.IsOnline())
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
            AddTabSelection(false);
            float centeredX = (ImGui.GetWindowSize().X - ImGui.CalcTextSize("Confirm Trade").X) / 2.5f;
            if (Misc.DrawCenteredButton(centeredX, new Vector2(150, 30), "Confirm Trade"))
            {
                if(inventoryLayout.tradeSlotContents.Count > 0)
                {
                    showConfirmTradePopup = true;
                    ImGui.OpenPopup("ConfirmTradePopup");
                }
                else
                {
                    DataSender.SendTradeStatus(-1 ,inventoryLayout, tradeTargetName, tradeTargetWorld, true, false);
                }
            }
            ImGui.SameLine();
            if (Misc.DrawCenteredButton(centeredX + 160, new Vector2(150, 30), "Cancel Trade"))
            {
                DataSender.SendTradeStatus(-1, inventoryLayout, tradeTargetName, tradeTargetWorld, false, true);
            }
            
            // Confirmation Popup
            if (showConfirmTradePopup)
            {
                ImGui.SetNextWindowSize(new Vector2(350, 120), ImGuiCond.Always);
                if (ImGui.BeginPopupModal("ConfirmTradePopup", ref showConfirmTradePopup, ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGui.Text("Please choose an inventory to receive the items.");
                    ImGui.Spacing();

                    if (ImGui.Button("Confirm", new Vector2(120, 0)))
                    {
                        DataSender.SendTradeStatus(inventoryTabs[selectedTab].Item1, inventoryLayout, tradeTargetName, tradeTargetWorld, true, false);
                        showConfirmTradePopup = false;
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Cancel", new Vector2(120, 0)))
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
                                    DataSender.SendInventorySelection(inventoryTabs[idx].Item1, inventoryTabs[idx].Item2);
                                }
                            }
                            Helpers.ImGuiHelpers.SelectableHelpMarker("Select an inventory to send and receive items.");
                        }

                       

                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.logger.Error("ProfileWindow AddProfileSelection Error: " + ex.Message);
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
