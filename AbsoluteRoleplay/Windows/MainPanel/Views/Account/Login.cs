using Dalamud.Bindings.ImGui;
using Networking;
using System.Numerics;
using System.Reflection.Emit;
using static FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.VertexShader;

namespace AbsoluteRoleplay.Windows.MainPanel.Views.Account
{
    internal class Login
    {
        public static string username = Plugin.plugin.Configuration.username;
        public static string password = Plugin.plugin.Configuration.password;
        public static void LoadLogin()
        {
            var centeredX = MainPanel.centeredX;
            var ButtonSize = MainPanel.ButtonSize;

            var currentCursorY = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(new Vector2(centeredX, currentCursorY));
            ImGui.PushItemWidth(ButtonSize.X);
            if(ImGui.InputTextWithHint("##Username", "Username", ref username, 30, ImGuiInputTextFlags.None))
            {
                Plugin.plugin.Configuration.username = username;
            }
            if (ImGui.InputTextWithHint("##Password", "Password", ref password, 45, ImGuiInputTextFlags.Password))
            {
                Plugin.plugin.Configuration.password = password;
            }
            ImGui.PopItemWidth();

            if (Misc.DrawCenteredButton(centeredX, ButtonSize, "Login"))
            {
                if (Plugin.plugin.IsOnline() && ClientTCP.IsConnected() == true)
                {
                    MainPanel.SaveLoginPreferences(username.ToString(), password.ToString());
                    DataSender.Login();
                }
            }
            ImGui.SameLine();
            if (ImGui.Checkbox("Remember Me", ref MainPanel.Remember))
            {
                MainPanel.SaveLoginPreferences(username.ToString(), password.ToString());
            }

            if (Misc.DrawCenteredButton(centeredX, ButtonSize, "Forgot"))
            {
                MainPanel.forgot = MainPanel.CurrentElement();
            }
            ImGui.SameLine();
            if (ImGui.Button("Register"))
            {
                MainPanel.register = MainPanel.CurrentElement();
            }

        }
    }
}
