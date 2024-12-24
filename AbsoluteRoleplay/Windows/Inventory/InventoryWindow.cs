using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Dalamud.Utility;
using Networking;
using OtterGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Textures.TextureWraps;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AbsoluteRoleplay.Windows.Profiles;
using FFXIVClientStructs.FFXIV.Common.Lua;
using Lumina.Excel.Sheets;
using System.ComponentModel;

namespace AbsoluteRoleplay.Windows.Inventory
{
    //changed
    public class InventoryWindow : Window, IDisposable
    {
        public enum InvTabItem
        {
            Consumeable = 0,
            Quest = 1,
            Armor = 2,
            Weapon = 3,
            Material = 4,
            Container = 5,
            Script = 6,
            Key = 7,           
        }
        public static string loading; //loading status string for loading the profile gallery mainly
        public static float percentage = 0f; //loading base value
        private Plugin plugin;
        public Configuration configuration;
        public static SortedList<InvTabItem, bool> TabOpen = new SortedList<InvTabItem, bool>(); //what part of the profile we have open
        public static bool ExistingProfile = false;
        public static float loaderInd = -1; //used for the gallery loading bar
        public static bool activeProfile;
        public static int currentProfile = 0;
        public static List<Tuple<int, string, bool>> ProfileBaseData = new List<Tuple<int, string, bool>>();
        public InventoryWindow(Plugin plugin) : base(
       "INVENTORY", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {

                MinimumSize = new Vector2(300, 300),
                MaximumSize = new Vector2(950, 950)
            };

            this.plugin = plugin;
            configuration = plugin.Configuration;
        }
        public override void OnOpen()
        {
            InvTab.InitInventory();
            ProfileBaseData.Clear();
            TabOpen.Clear();
            foreach (InvTabItem tab in Enum.GetValues(typeof(InvTabItem)))
            {
                TabOpen.Add(tab, false); //set all tabs to be closed by default
            }
        }

        public override void Draw()
        {
            if (percentage == loaderInd + 1)
            {
                if (plugin.IsOnline())
                {
                    //if we have loaded all
                    //the data received from the server and we are logged in game

                    ImGui.SameLine();
                    if (ProfileBaseData.Count > 0 && ExistingProfile == true)
                    {
                        DrawInventory();
                    }
                    if (ProfileBaseData.Count <= 0)
                    {
                        ExistingProfile = false;
                        ImGui.TextColored(new Vector4(255, 0, 0, 255), "You must have a profile to use it's inventory.");
                    }
                    else
                    {
                        ExistingProfile = true;
                    }
                }
            }
            else
            {

                Misc.StartLoader(loaderInd, percentage, "Loading Item", ImGui.GetWindowSize());
            }
        }
        public async void DrawInventory()
        {         
            ImGui.Spacing();
            ImGui.BeginTabBar("InventoryNavigation");
            if (ImGui.BeginTabItem("Armor")) { ClearUI(); TabOpen[InvTabItem.Armor] = true; ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Weapons")) { ClearUI(); TabOpen[InvTabItem.Weapon] = true; ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Materials")) { ClearUI(); TabOpen[InvTabItem.Material] = true; ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Consumeables")) { ClearUI(); TabOpen[InvTabItem.Consumeable] = true; ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Quests")) { ClearUI(); TabOpen[InvTabItem.Quest] = true; ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Containers")) { ClearUI(); TabOpen[InvTabItem.Container] = true; ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Scripts")) { ClearUI(); TabOpen[InvTabItem.Script] = true; ImGui.EndTabItem(); }
            ImGui.EndTabBar();

            using var InventoryTab = ImRaii.Child("INVENTORY");
            if (InventoryTab)
            {
                var firstTrueKey = TabOpen.FirstOrDefault(kv => kv.Value).Key;

                if (TabOpen[InvTabItem.Armor])
                {                        
                    await InvTab.LoadInventoryTabAsync(plugin, InvTabItem.Armor);
                }
                if (TabOpen[InvTabItem.Weapon])
                {
                    await InvTab.LoadInventoryTabAsync(plugin, InvTabItem.Weapon);
                }
                if (TabOpen[InvTabItem.Material])
                {

                    await InvTab.LoadInventoryTabAsync(plugin, InvTabItem.Material);
                }
                if (TabOpen[InvTabItem.Consumeable])
                {

                    await InvTab.LoadInventoryTabAsync(plugin, InvTabItem.Consumeable);
                }
                if (TabOpen[InvTabItem.Quest])
                {

                    await InvTab.LoadInventoryTabAsync(plugin, InvTabItem.Quest);
                }
                if (TabOpen[InvTabItem.Container])
                {

                    await InvTab.LoadInventoryTabAsync(plugin, InvTabItem.Container);
                }
                if (TabOpen[InvTabItem.Key])
                {

                    await InvTab.LoadInventoryTabAsync(plugin, InvTabItem.Key);
                }
                if (TabOpen[InvTabItem.Script])
                {

                    await InvTab.LoadInventoryTabAsync(plugin, InvTabItem.Script);
                }
            }
        }

        //reset our tabs and go back to base ui with no tab selected
        public static void ClearUI()
        {
            TabOpen[InvTabItem.Consumeable] = false;
            TabOpen[InvTabItem.Quest] = false;
            TabOpen[InvTabItem.Armor] = false;
            TabOpen[InvTabItem.Weapon] = false;
            TabOpen[InvTabItem.Material] = false;
            TabOpen[InvTabItem.Container] = false;
            TabOpen[InvTabItem.Script] = false;
            TabOpen[InvTabItem.Key] = false;
        }
      
        public void Dispose()
        {
           
        }
   
    }
}


