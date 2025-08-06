using Dalamud.Bindings.ImGui;
using Networking;

namespace AbsoluteRoleplay.Windows.MainPanel.Views.Account
{
    internal class Forgot
    {
        public static string restorationEmail = string.Empty;
        public static void LoadForgot(Plugin pluginInstance)
        {
            var centeredX = MainPanel.centeredX;
            var ButtonSize = MainPanel.ButtonSize;
            Misc.DrawCenteredInput(centeredX, ButtonSize, "##RegisteredEmail", $"Email", ref restorationEmail, 100, ImGuiInputTextFlags.None);
            if (Misc.DrawCenteredButton(centeredX, ButtonSize, "Submit Request"))
            {
                if (pluginInstance.IsOnline())
                {
                    DataSender.SendRestorationRequest(restorationEmail);
                }
            }
            if (Misc.DrawCenteredButton(centeredX, ButtonSize, "Back"))
            {
                MainPanel.login = MainPanel.CurrentElement();
            }
        }
    }
}
