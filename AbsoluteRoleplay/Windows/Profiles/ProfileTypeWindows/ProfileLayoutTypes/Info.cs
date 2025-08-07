using AbsoluteRoleplay.Windows.MainPanel.Views.Account;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes
{
    internal class Info
    {
        public static void RenderInfoLayout(int index, string uniqueID, InfoLayout layout)
        {
            string content = layout.text;
            if(ImGui.InputTextMultiline($"##InfoContent {index}_{uniqueID}" , ref content, 50000, new Vector2(ImGui.GetWindowSize().X - 20, ImGui.GetWindowSize().Y /2)))
            {
               layout.text = content; 
            }
        }


    }
}
