using Dalamud.Bindings.ImGui;
using Networking;

namespace AbsoluteRoleplay.Windows.MainPanel.Views.Account
{
    internal class Login
    {
        public static string username = string.Empty;
        public static string password = string.Empty;
        public static void LoadLogin(Plugin pluginInstance)
        {
            var centeredX = MainPanel.centeredX;
            var ButtonSize = MainPanel.ButtonSize;

            Misc.DrawCenteredInput(centeredX, ButtonSize, "##username", $"Username", ref username, 100, ImGuiInputTextFlags.None);
            Misc.DrawCenteredInput(centeredX, ButtonSize, "##password", $"Password", ref password, 100, ImGuiInputTextFlags.Password);
            if (Misc.DrawCenteredButton(centeredX, ButtonSize, "Login"))
            {
                if (pluginInstance.IsOnline() && ClientTCP.IsConnected() == true)
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
