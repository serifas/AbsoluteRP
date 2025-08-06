using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
namespace AbsoluteRoleplay.Helpers
{
    public static class ImGuiHelpers
    {
        public static float GlobalScale
        {
            get
            {
                // Use ImGui's font global scale, or replace with your own config if needed
                return ImGui.GetIO().FontGlobalScale;
            }
        }
        public static bool DrawTextButton(string text, Vector2 size, ImGuiButtonFlags flags = ImGuiButtonFlags.None)
        {
            return ImGui.Button(text, size);
        }
        public static void SelectableHelpMarker(string description)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
                ImGui.TextUnformatted(description);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
        }
    }
    public static class EnumerableExtensions
    {
        public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> source)
        {
            int index = 0;
            foreach (var item in source)
            {
                yield return (item, index++);
            }
        }
    }
}
