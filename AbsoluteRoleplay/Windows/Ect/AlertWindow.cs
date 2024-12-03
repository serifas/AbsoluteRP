using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using System;
using Networking;
using System.Security.Cryptography;

namespace AbsoluteRoleplay.Windows.Ect
{

    public class AlertWindow : Window, IDisposable
    {
        public static Plugin pg;
        public static string alertStatus = string.Empty;
        public static float length;
        public bool increment = true;
        public static Vector4 alertColor = new Vector4(0, 0, 0, 0);

        public AlertWindow(Plugin plugin) : base(
       "ALERT", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize)
        {

            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(400, 80),
                MaximumSize = new Vector2(400, 80)
            };
            pg = plugin;
            length = 0;
            increment = true;
        }
        public override void Draw()
        {
            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(new Vector2(viewport.WorkPos.X + viewport.WorkSize.X - 300, viewport.WorkPos.Y + viewport.WorkSize.Y - 200), ImGuiCond.Always); // Adjust position based on window size
            ImGui.TextColored(alertColor, alertStatus);
            length++;
            if (length > 400 && increment == true)
            {
                increment = false;
                pg.CloseAlertWIndow();
            }
            // Add other UI elements here
            Misc.AddIncrementBar(ImGui.GetWindowDrawList(), length, alertColor);

        }
        public void Update()
        {

        }
        public void Dispose()
        {

        }
    }

}
