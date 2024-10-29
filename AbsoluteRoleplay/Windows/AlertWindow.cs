using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using System;
using Networking;

namespace AbsoluteRoleplay.Windows
{
    public class AlertWindow : Window, IDisposable
    {
        public static Plugin pg;
        public static string alertStatus = string.Empty;
        public static Vector4 alertColor = new Vector4(0, 0, 0, 0);
        public AlertWindow(Plugin plugin) : base(
       "ALERT", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(400, 100),
                MaximumSize = new Vector2(400, 100)
            };
            pg = plugin;

        }
        public override void Draw()
        {
            if (pg.IsOnline())
            {
                Misc.SetCenter(pg, alertStatus);
                ImGui.TextColored(alertColor, alertStatus);
            }

        }
        public void Dispose()
        {
        }
    }

}
