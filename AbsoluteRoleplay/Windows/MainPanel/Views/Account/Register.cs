using Dalamud.Bindings.ImGui;
using Networking;
using System.Numerics;

namespace AbsoluteRoleplay.Windows.MainPanel.Views.Account
{
    internal class Register
    {
        public static string registerUser = string.Empty;
        public static string registerPassword = string.Empty;
        public static string registerVerPassword = string.Empty;
        public static string email = string.Empty;
        //registration agreement
        public static bool AgreeTOS = false;
        public static bool Agree18 = false;
        public static void LoadRegistration(Plugin pluginInstance)
        {
            var centeredX = MainPanel.centeredX;
            var ButtonSize = MainPanel.ButtonSize;
            Misc.DrawCenteredInput(centeredX, ButtonSize, "##username", $"Username", ref registerUser, 100, ImGuiInputTextFlags.None);
            Misc.DrawCenteredInput(centeredX, ButtonSize, "##passver", $"Password", ref registerPassword, 100, ImGuiInputTextFlags.Password);
            Misc.DrawCenteredInput(centeredX, ButtonSize, "##regpassver", $"Verify Password", ref registerVerPassword, 100, ImGuiInputTextFlags.Password);
            Misc.DrawCenteredInput(centeredX, ButtonSize, "##email", $"Email", ref email, 100, ImGuiInputTextFlags.None);
            var Pos18 = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(new Vector2(centeredX, Pos18));
            ImGui.Checkbox("I am atleast 18 years of age", ref Agree18);
            var agreePos = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(new Vector2(centeredX, agreePos));
            ImGui.Checkbox("I agree to the TOS.", ref AgreeTOS);
            if (Misc.DrawCenteredButton(centeredX, ButtonSize, "View ToS & Rules"))
            {
                pluginInstance.OpenTermsWindow();
            }
            if (Agree18 == true && AgreeTOS == true)
            {
                if (Misc.DrawCenteredButton(centeredX, ButtonSize, "Register Account"))
                {
                    if (registerPassword == registerVerPassword)
                    {
                        if (pluginInstance.IsOnline())
                        {
                            MainPanel.SaveLoginPreferences(registerUser.ToString(), registerPassword.ToString());
                            DataSender.Register(registerUser.ToString(), registerPassword, email);
                        }
                    }
                    else
                    {
                        MainPanel.status = "Passwords do not match.";
                        MainPanel.statusColor = new Vector4(255, 0, 0, 255);
                    }

                }
            }
            if (Misc.DrawCenteredButton(centeredX, ButtonSize, "Back"))
            {
                MainPanel.login = MainPanel.CurrentElement();
            }

        }
    }
}
