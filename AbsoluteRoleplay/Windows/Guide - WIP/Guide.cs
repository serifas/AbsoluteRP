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
    public class Guide : Window, IDisposable
    {
        private float _modVersionWidth;
        public static string verificationKey = string.Empty;
        public static string verificationStatus = string.Empty;
        public static Vector4 verificationCol = new Vector4(1, 1, 1, 1);
        public static string GuideLayout;

        public bool Agreed = false;
        internal Version version;

        public Guide() : base(
        "Player Guide")
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(200, 200),
                MaximumSize = new Vector2(1200, 1200)
            };
            Task.Run(() =>
            {
                //get our online tos and rules
                GuideLayout = ReadGuide("https://raw.githubusercontent.com/serifas/AbsoluteRP/main/Guide.txt");
            });
        }
        public override async void Draw()
        {
            try
            {
                
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("TOS Draw Debug: " + ex.Message);
            }
        }
        public void Dispose()
        {

        }

        static string ReadGuide(string url)
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
