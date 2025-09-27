using AbsoluteRP.Defines;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Networking;
using System.Numerics;

namespace AbsoluteRP.Windows.MainPanel.Views
{
    internal class MainUI
    {
        public static List<Defines.Account> accounts = new List<Defines.Account>();
        public static void LoadMainUI(Plugin pluginInstance)
        {
            var buttonWidth = MainPanel.buttonWidth;
            var buttonHeight = MainPanel.buttonHeight;          
         
            using (ImRaii.Disabled(true))
            {
                if (ImGui.Button("Open ARP Chat", new Vector2(buttonWidth * 2.18f, buttonHeight / 2f)))
                {
                    pluginInstance.ToggleChatWindow();
                }
            }

            if (ImGui.Button("Options", new Vector2(buttonWidth * 2.18f, buttonHeight / 2f)))
            {
                pluginInstance.OpenOptionsWindow();
            }
        }
          
        
       
    }
}
