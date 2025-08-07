using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
namespace AbsoluteRoleplay.Helpers
{

    public static class UIHelpers
    {
        public static float GlobalScale
        {
            get
            {
                // Use ImGui's font global scale, or replace with your own config if needed
                return ImGui.GetIO().FontGlobalScale;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool DrawTextButton(string text, Vector2 size, uint buttonColor)
        {
            using var color = ImRaii.PushColor(ImGuiCol.Button, buttonColor)
                .Push(ImGuiCol.ButtonActive, buttonColor)
                .Push(ImGuiCol.ButtonHovered, buttonColor);
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
