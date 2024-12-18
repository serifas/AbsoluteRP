using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using static Dalamud.Interface.Windowing.Window;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ImGuiFileDialog;
using ImGuiNET;
using static Lumina.Data.Files.ScdFile;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Gui;
using Dalamud.Game.ClientState;
using Networking;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Component.GUI;
using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Defines;
using Dalamud.Interface.Utility;

namespace AbsoluteRoleplay.Windows.Ect
{
    public class ItemTooltip: Window, IDisposable
    {
        public static bool isAdmin;
        public Configuration config;
        public Vector2 windowPos;
        public static Item item;

        public Plugin plugin;

        public ItemTooltip(Plugin plugin) : base(
       "ITEM TOOLTIP", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoNav |
                                              ImGuiWindowFlags.NoMouseInputs
                                              | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse |
                                              ImGuiWindowFlags.NoTitleBar)
        {
            config = plugin.Configuration;
            this.plugin = plugin;
        }

        public override void Draw()
        {
            ImGui.Image(WindowOperations.RenderIconAsync(plugin, item.iconID).Result.ImGuiHandle, new Vector2(32, 32));
            ImGui.Text(item.name);
            ImGui.Text(item.description);
          
            windowPos = ImGui.GetMousePos();
            ImGui.SetWindowPos(windowPos);         

        }





        public void Dispose()
        {

        }


    }
}
