using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static Dalamud.Interface.Utility.Raii.ImRaii;

namespace AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes
{
    internal class Info
    {
        private static bool viewable = true;

        public static void RenderInfoLayout(int index, string uniqueID, InfoLayout layout)
        {
            /*
            ImGui.Checkbox($"Viewable##Viewable{layout.id}", ref viewable);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("If checked, this tab will be viewable by others.\nIf unchecked, it will not be displayed.");
            }
            */
            string content = layout.text;
            if(ImGui.InputTextMultiline($"##InfoContent {index}_{uniqueID}" , ref content, 5000000, new Vector2(ImGui.GetWindowSize().X - 20, ImGui.GetWindowSize().Y /2)))
            {
               layout.text = content; 
            }
        }


    }
}
