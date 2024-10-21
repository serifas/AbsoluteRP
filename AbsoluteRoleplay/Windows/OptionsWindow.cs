using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using System;
using Dalamud.Interface.GameFonts;
using AbsoluteRoleplay.Helpers;
namespace AbsoluteRoleplay.Windows
{
    public class OptionsWindow : Window, IDisposable
    {
        private float _modVersionWidth;
        public static Plugin pg;
        public static bool showTargetOptions;
        public static bool showKofi;
        public static bool showDisc;
        public static bool showWIP;
        public static bool closeAfterConnection;
        public OptionsWindow(Plugin plugin) : base(
       "OPTIONS", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(300, 180),
                MaximumSize = new Vector2(300, 180)
            };
            pg = plugin;
            showWIP = plugin.Configuration.showWIP;
            showKofi = plugin.Configuration.showKofi;
            showDisc = plugin.Configuration.showDisc;
           closeAfterConnection = plugin.Configuration.closeAfterConnection;
        }
        public override void Draw()
        {
            Misc.SetTitle(pg, false, "Options");
            //okay that's done.
            ImGui.Spacing();
            //now for some simple toggles
            if (ImGui.Checkbox("Show Ko-fi Button", ref showKofi))
            {
                pg.Configuration.showKofi = showKofi;
                pg.Configuration.Save();
            }
            if (ImGui.Checkbox("Show Discord Button.", ref showDisc))
            {
                pg.Configuration.showDisc = showDisc;
                pg.Configuration.Save();
            }
            if (ImGui.Checkbox("Close plugin after new connectoin", ref closeAfterConnection))
            {
                pg.Configuration.closeAfterConnection = closeAfterConnection;
                pg.Configuration.Save();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Good to set for annoying behavior (Mainly just nice for closing the window on login)");
            }
        }
        public void Dispose()
        {

        }
    }

}
