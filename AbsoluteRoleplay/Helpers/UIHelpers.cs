using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;
using Networking;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
namespace AbsoluteRP.Helpers
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
        public static void DrawExtraNav(Navigation navigation, ref int selectedNavIndex)
        {
            Func<bool>[] navButtons;
            float buttonSize = ImGui.GetIO().FontGlobalScale * 45; // Height of each navigation button
            int pressedIndex = -1;
            navButtons = navigation.textureIDs.Select((icon, idx) =>
               (Func<bool>)(() =>
               {
                   bool pressed = false;
                   if (navigation.show[idx])
                   {
                       ImGui.SetCursorPosY(ImGui.GetWindowSize().Y - buttonSize * 1.2f);
                       pressed = AbsoluteRP.Helpers.CustomLayouts.TransparentImageButton(
                           icon,
                           new Vector2(buttonSize, buttonSize),
                           navigation.names[idx]
                       );
                       if (pressed)
                       {
                           pressedIndex = idx;
                           navigation.actions[idx]?.Invoke();
                       }
                       ImGui.SameLine();
                   }

                   return pressed;
               })
           ).ToArray();
            for (int i = 0; i < navButtons.Length; i++)
            {
                ImGui.PushID(i);
                if (navButtons[i].Invoke())
                    selectedNavIndex = i;
                ImGui.PopID();
            }
            if (pressedIndex != -1)
                selectedNavIndex = pressedIndex;
        }
        public static void DrawSideNavigation(string uniqueID, ref int selectedNavIndex, ImGuiWindowFlags flags, Navigation navigation)
        {
            ImGui.Begin(uniqueID, flags);


            Func<bool>[] navButtons;
            int pressedIndex = -1;

            float buttonSize = ImGui.GetIO().FontGlobalScale * 45; // Height of each navigation button

            navButtons = navigation.textureIDs.Select((icon, idx) =>
                (Func<bool>)(() =>
                {
                    bool pressed = AbsoluteRP.Helpers.CustomLayouts.TransparentImageButton(
                        icon,
                        new Vector2(buttonSize, buttonSize),
                        navigation.names[idx]
                    );
                    if (pressed)
                    {
                        pressedIndex = idx;
                        navigation.actions[idx]?.Invoke();
                    }
                    return pressed;
                })
            ).ToArray();
            for (int i = 0; i < navButtons.Length; i++)
            {
                ImGui.PushID(i);
                if (navButtons[i].Invoke())
                    selectedNavIndex = i;
                ImGui.PopID();
            }

            if (pressedIndex != -1)
                selectedNavIndex = pressedIndex;
            ImGui.End();
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
