using Dalamud.Interface.Colors;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using ImGuiScene;
using OtterGui.Raii;
using OtterGui;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dalamud.Interface.GameFonts;
using Dalamud.Game.Gui.Dtr;
using Microsoft.VisualBasic;
using Networking;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.Havok;
using System.Text.RegularExpressions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface.Utility;
using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Windows.Ect;

namespace AbsoluteRoleplay.Windows.Profiles
{
    public class TradeWindow : Window, IDisposable
    {
        private Plugin plugin;
        private IDalamudPluginInterface pg;
        public static bool DisableBookmarkSelection = false;
        internal static List<Bookmark> profileList = new List<Bookmark>();
        public static Dictionary<int, Defines.Item> slotContents = new(); // Slot contents, indexed by slot number
        private const int GridSize = 10; // 10x10 grid for 200 slots
        private const int TotalSlots = GridSize * GridSize;

        public TradeWindow(Plugin plugin) : base(
       "TRADE", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(300, 300),
                MaximumSize = new Vector2(1200, 800)
            };
            this.plugin = plugin;
        }
        public override void Draw()
        {
           // ItemGrid.DrawGrid()
        }



        public void Dispose()
        {

        }
    }
   
}
