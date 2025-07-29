using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using System;
using Dalamud.Interface.GameFonts;
using Networking;
using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Helpers;
using Dalamud.Interface.Colors;

namespace AbsoluteRoleplay.Windows.Account
{
    public class VerificationWindow : Window, IDisposable
    {
        public static Plugin pg;
        public static string verificationKey = string.Empty;
        public static string verificationStatus = string.Empty;
        public static Vector4 verificationCol = new Vector4(1, 1, 1, 1);
        public VerificationWindow(Plugin plugin) : base(
       "VERIFICATION", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(350, 200),
                MaximumSize = new Vector2(350, 200)
            };
            pg = plugin;

        }
        public override void Draw()
        {
            try
            {
                Misc.SetTitle(pg, false, "Verification", ImGuiColors.TankBlue);
                //okay that's done.
                ImGui.Text("We sent a verification key to the email provided. \nPlease provide it below...");
                ImGui.Spacing();
                //now for some simple toggles
                ImGui.InputText("Key", ref verificationKey, 10);
                if (ImGui.Button("Submit"))
                {
                    //if player is online in game
                    if (pg.IsOnline())
                    {
                        //submit our verification key for verification
                        DataSender.SendVerification(pg.username.ToString(), verificationKey);
                    }

                }
                ImGui.TextColored(verificationCol, verificationStatus);
            }
            catch (Exception ex)
            {
                Plugin.plugin.logger.Error("VerificationWindow Draw Error: " + ex.Message);
            }
        }
        public void Dispose()
        {

        }
    }

}
