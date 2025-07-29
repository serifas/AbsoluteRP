using Dalamud.Interface.GameFonts;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;
using AbsoluteRoleplay.Helpers;
using System;
using System.Net.Http;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Colors;
using AbsoluteRoleplay.Windows.MainPanel.Views.Account;
namespace AbsoluteRoleplay.Windows.Ect
{
    public class ImportantNotice : Window, IDisposable
    {
        private float _modVersionWidth;
        public static Plugin pg;
        public static string moderatorMessage = string.Empty;
        public static string messageTitle = string.Empty;

        public ImportantNotice(Plugin plugin) : base(
        "IMPORTANT NOTICE")
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(200, 200),
                MaximumSize = new Vector2(1200, 1200)
            };
            pg = plugin;
        }
        public override async void Draw()
        {
            try
            {
                //draw TOS
                Misc.SetTitle(pg, true, messageTitle, ImGuiColors.DPSRed);
                ImGuiHelpers.SafeTextWrapped(moderatorMessage);
            }
            catch (Exception ex)
            {
                Plugin.plugin.logger.Error("ImportantNotice Draw Error: " + ex.Message);
            }
        }
        public void Dispose()
        {

        }       
    }

}
