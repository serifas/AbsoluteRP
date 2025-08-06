using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using InventoryTab;
using System.Numerics;

namespace AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes
{
    //changed
    public class Inventory
    {
        public static string loading; //loading status string for loading the profile gallery mainly
        public static float percentage = 0f; //loading base value
        public static SortedList<InvTabItem, bool> TabOpen = new SortedList<InvTabItem, bool>(); //what part of the profile we have open
        public static bool ExistingProfile = false;
        public static float loaderInd = -1; //used for the gallery loading bar
        public static bool activeProfile;
        public static List<Tuple<int, string>> ProfileBaseData = new List<Tuple<int, string>>();
       
        public static void RenderInventoryLayout(int index, string uniqueID, InventoryLayout layout)
        {
            try
            {
                if (Plugin.plugin.IsOnline())
                {
                    //if we have loaded all
                    //the data received from the server and we are logged in game

                    ImGui.SameLine();
                    DrawInventory(index, uniqueID, layout);   
                }
            }catch(Exception ex)
            {
                Plugin.logger.Error("InventoryWindow Draw Error: " + ex.Message);
            }
        }
        public static async void DrawInventory(int index, string uniqueID, InventoryLayout layout)
        {         
            ImGui.Spacing();

            using var InventoryTab = ImRaii.Child($"INVENTORY{uniqueID}{layout.id}");
            if (InventoryTab)
            {         
                await InvTab.LoadInventoryTabAsync(Plugin.plugin, layout);
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
      

    }
}


