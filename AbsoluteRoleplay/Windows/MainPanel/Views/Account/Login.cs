using Dalamud.Bindings.ImGui;
using Networking;

namespace AbsoluteRoleplay.Windows.MainPanel.Views.Account
{
    internal class Login
    {
        public static void LoadLogin()
        {
            string username = Plugin.plugin.Configuration.username;
            string password = Plugin.plugin.Configuration.password;   
            var centeredX = MainPanel.centeredX;
            var ButtonSize = MainPanel.ButtonSize;

            if(ImGui.InputText("##username", ref username, 100, ImGuiInputTextFlags.None))
            {
                Plugin.plugin.Configuration.username = username;
            }
            if(ImGui.InputText("##password", ref password, 100, ImGuiInputTextFlags.Password))
            {
                Plugin.plugin.Configuration.password = password;
            }   
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
