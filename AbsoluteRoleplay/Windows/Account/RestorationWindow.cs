using Dalamud.Interface.Colors;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using Networking;
using Dalamud.Bindings.ImGui;
using AbsoluteRoleplay.Helpers;

namespace AbsoluteRoleplay.Windows.Account
{
    public class RestorationWindow : Window, IDisposable
    {
        public static string restorationKey = string.Empty;
        public static string restorationPass = string.Empty;
        public static string restorationPassConfirm = string.Empty;
        public static string restorationEmail = string.Empty;
        public static string restorationStatus = string.Empty;
        public static Vector4 restorationCol = new Vector4(1, 1, 1, 1);
        public RestorationWindow() : base(
       "RESTORATION", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(600, 300),
                MaximumSize = new Vector2(420, 350)
            };


        }
        public override void Draw()
        {
            try { 
            Misc.SetTitle(Plugin.plugin, true, "Account Restoration", ImGuiColors.TankBlue);
            //set everything back
            //okay that's done.
            ImGui.Text("We sent a restoration key to the email address provided. \nPlease enter the key with a new password below.");
            ImGui.Spacing();
            //now for some simple toggles
            ImGui.InputText("Restoration Key", ref restorationKey, 10);
            ImGui.InputText("New Password", ref restorationPass, 30, ImGuiInputTextFlags.Password);
            ImGui.InputText("Confirm New Password", ref restorationPassConfirm, 30, ImGuiInputTextFlags.Password);


            if (ImGui.Button("Submit"))
            {
                if (restorationKey != string.Empty && restorationPass != string.Empty && restorationPassConfirm != string.Empty)
                {
                    if (restorationPass == restorationPassConfirm)
                    {
                        if (Plugin.plugin.IsOnline())
                        {
                            //send the key with the new password to restore the account to settings the user knows
                            DataSender.SendRestoration(restorationEmail, restorationPass, restorationKey);
                        }
                    }
                    else
                    {
                        restorationCol = new Vector4(255, 0, 0, 255);
                        restorationStatus = "Passwords do not match.";
                    }


                }

            }
            ImGui.TextColored(restorationCol, restorationStatus);
            }catch(Exception e)
            {
                restorationStatus = "An Debug occurred";
                Plugin.PluginLog.Debug(e.Message);
                restorationCol = new Vector4(255, 0, 0, 255);
            }
        }
        public void Dispose()
        {

        }
    }

}
