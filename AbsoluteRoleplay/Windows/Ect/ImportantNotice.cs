using Dalamud.Interface.GameFonts;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using AbsoluteRoleplay.Helpers;
using System;
using System.Net.Http;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Colors;
using AbsoluteRoleplay.Windows.MainPanel.Views.Account;
using Dalamud.Bindings.ImGui;
namespace AbsoluteRoleplay.Windows.Ect
{
    public class ImportantNotice : Window, IDisposable
    {
        private float _modVersionWidth;
        public static string moderatorMessage = string.Empty;
        public static string messageTitle = string.Empty;

        public ImportantNotice() : base(
        "IMPORTANT NOTICE")
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(200, 200),
                MaximumSize = new Vector2(1200, 1200)
            };
        }
        public override async void Draw()
        {
            try
            {
                //draw TOS
                Misc.SetTitle(Plugin.plugin, true, messageTitle, ImGuiColors.DPSRed);
                ImGui.TextWrapped(moderatorMessage);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("ImportantNotice Draw Debug: " + ex.Message);
            }
        }
        public void Dispose()
        {

        }       
    }

}
