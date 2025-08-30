using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Common.Math;
using Networking;
using Dalamud.Interface.Colors;
using Dalamud.Bindings.ImGui;
using AbsoluteRP.Helpers;

namespace AbsoluteRP.Windows.Account
{
    public class VerificationWindow : Window, IDisposable
    {
        public static string verificationKey = string.Empty;
        public static string verificationStatus = string.Empty;
        public static Vector4 verificationCol = new Vector4(1, 1, 1, 1);
        public VerificationWindow() : base(
       "VERIFICATION", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(350, 200),
                MaximumSize = new Vector2(350, 200)
            };

        }
        public override void Draw()
        {
            try
            {
                Misc.SetTitle(Plugin.plugin, false, "Verification", ImGuiColors.TankBlue);
                //okay that's done.
                ImGui.Text("We sent a verification key to the email provided. \nPlease provide it below...");
                ImGui.Spacing();
                //now for some simple toggles
                ImGui.InputText("Key", ref verificationKey, 10);
                if (ImGui.Button("Submit"))
                {
                    //if player is online in game
                    if (Plugin.plugin.IsOnline())
                    {
                        //submit our verification key for verification
                        DataSender.SendVerification(Plugin.plugin.accountTag.ToString(), verificationKey);
                    }

                }
                ImGui.TextColored(verificationCol, verificationStatus);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("VerificationWindow Draw Debug: " + ex.Message);
            }
        }
        public void Dispose()
        {

        }
    }

}
