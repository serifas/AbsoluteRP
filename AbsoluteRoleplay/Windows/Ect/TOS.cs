using Dalamud.Interface.Windowing;
using AbsoluteRP.Helpers;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Colors;
using AbsoluteRP.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
namespace AbsoluteRP.Windows.Ect
{
    public class TOS : Window, IDisposable
    {
        private float _modVersionWidth;
        public static string verificationKey = string.Empty;
        public static string verificationStatus = string.Empty;
        public static Vector4 verificationCol = new Vector4(1, 1, 1, 1);
        public static string ToS1, ToS2, Rules1, Rules2;

        public bool Agreed = false;
        internal Version version;

        public TOS() : base(
        "TERMS OF SERVICE")
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(200, 200),
                MaximumSize = new Vector2(1200, 1200)
            };
            Task.Run(() =>
            {

                //get our online tos and rules
                ToS1 = ReadTOS("https://raw.githubusercontent.com/serifas/Absolute-Roleplay/main/TOS1.txt");
                ToS2 = ReadTOS("https://raw.githubusercontent.com/serifas/Absolute-Roleplay/main/TOS2.txt");
                Rules1 = ReadTOS("https://raw.githubusercontent.com/serifas/Absolute-Roleplay/main/Rules1.txt");
                Rules2 = ReadTOS("https://raw.githubusercontent.com/serifas/Absolute-Roleplay/main/Rules2.txt");
            } );
        }
        public override async void Draw()
        {
            try
            {
                //draw TOS
                Misc.SetTitle(Plugin.plugin, true, "Terms of Service", ImGuiColors.TankBlue);
                ImGui.TextWrapped(ToS1);
                ImGui.TextWrapped(ToS2);
                //draw rules
                Misc.SetTitle(Plugin.plugin, true, "Rules", ImGuiColors.TankBlue);
                ImGui.TextWrapped(Rules1);
                ImGui.TextWrapped(Rules2);

                var windowSize = ImGui.GetWindowSize();


                var buttonSize = ImGui.CalcTextSize("I Agree") + new Vector2(30, 30);
                float xPos = (windowSize.X - buttonSize.X) / 2;
                ImGui.SetCursorPosX(xPos);
                ImGui.Checkbox("I Agree##Agree", ref Agreed);

                using (ImRaii.Disabled(!Agreed))
                {
                    ImGui.SetCursorPosX(xPos);
                    if (ImGui.Button("Submit"))
                    {
                        Plugin.plugin.Configuration.TOSVersion = version;
                        Plugin.plugin.Configuration.Save();
                        Plugin.plugin.LoadConnection();
                        this.IsOpen = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("TOS Draw Debug: " + ex.Message);
            }
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
