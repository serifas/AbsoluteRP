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
        }
        public void Dispose()
        {

        }
    }

}
