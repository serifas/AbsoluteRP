using Dalamud.Interface.GameFonts;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;
using AbsoluteRoleplay.Helpers;
using System;
using System.Net.Http;
using System.Numerics;
using Dalamud.Interface.Utility;
namespace AbsoluteRoleplay.Windows.Ect
{
    public class TOS : Window, IDisposable
    {
        private float _modVersionWidth;
        public static Plugin pg;
        public static string verificationKey = string.Empty;
        public static string verificationStatus = string.Empty;
        public static Vector4 verificationCol = new Vector4(1, 1, 1, 1);
        public static string ToS1, ToS2, Rules1, Rules2;
        public static bool load;
        public TOS(Plugin plugin) : base(
        "TERMS OF SERVICE")
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(200, 200),
                MaximumSize = new Vector2(1200, 1200)
            };
            pg = plugin;

            load = true;
            //get our online tos and rules
            ToS1 = ReadTOS("https://raw.githubusercontent.com/serifas/AbsoluteRoleplay/main/TOS1.txt");
            ToS2 = ReadTOS("https://raw.githubusercontent.com/serifas/AbsoluteRoleplay/main/TOS2.txt");
            Rules1 = ReadTOS("https://raw.githubusercontent.com/serifas/AbsoluteRoleplay/main/Rules1.txt");
            Rules2 = ReadTOS("https://raw.githubusercontent.com/serifas/AbsoluteRoleplay/main/Rules2.txt");
        }
        public override async void Draw()
        {
            //draw TOS
            Misc.SetTitle(pg, true, "Terms of Service");
            ImGuiHelpers.SafeTextWrapped(ToS1);
            ImGuiHelpers.SafeTextWrapped(ToS2);
            //draw rules
            Misc.SetTitle(pg, true, "Rules");
            ImGuiHelpers.SafeTextWrapped(Rules1);
            ImGuiHelpers.SafeTextWrapped(Rules2);
        }

        public void Dispose()
        {

        }

        static string ReadTOS(string url)
        {
            //simply reads the online file from the url
            using (var client = new HttpClient())
            {
                var response = client.GetAsync(url).Result;
                response.EnsureSuccessStatusCode();
                var result = response.Content.ReadAsStringAsync().Result;
                return result;
            }
        }
    }

}
