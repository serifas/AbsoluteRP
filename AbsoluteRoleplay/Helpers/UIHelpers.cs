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
        public static void DrawPolygonFromPoints(List<Vector2> points, List<Vector4> colors, float thickness = 2.0f, int gradientSteps = 20)
        {
            if (points == null || points.Count < 3)
                return;

            var drawList = ImGui.GetWindowDrawList();
            int count = points.Count;

            for (int i = 0; i < count; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % count];
                var c1 = colors[i % colors.Count];
                var c2 = colors[(i + 1) % colors.Count];

                // Draw gradient by interpolating between c1 and c2
                for (int s = 0; s < gradientSteps; s++)
                {
                    float t1 = (float)s / gradientSteps;
                    float t2 = (float)(s + 1) / gradientSteps;
                    Vector2 segStart = Vector2.Lerp(p1, p2, t1);
                    Vector2 segEnd = Vector2.Lerp(p1, p2, t2);
                    Vector4 segColor = Vector4.Lerp(c1, c2, t1);
                    uint col = ImGui.ColorConvertFloat4ToU32(segColor);
                    drawList.AddLine(segStart, segEnd, col, thickness);
                }
            }
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
                       pressed = CustomLayouts.TransparentImageButton(
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
        public static IReadOnlyList<Vector2> DrawPolygon(Vector2 center, float radius, IReadOnlyList<Vector4> colors, float thickness = 2.0f, int gradientSteps = 20)
        {
            int corners = colors.Count;
            if (corners < 3) return Array.Empty<Vector2>();

            var drawList = ImGui.GetWindowDrawList();

            List<Vector2> points = new();
            float angleStep = 2.0f * MathF.PI / corners;
            float startAngle = -MathF.PI / 2; // Top

            for (int i = 0; i < corners; i++)
            {
                float angle = startAngle + i * angleStep;
                points.Add(new Vector2(
                    center.X + MathF.Cos(angle) * radius,
                    center.Y + MathF.Sin(angle) * radius
                ));
            }

            // Draw gradient edges
            for (int i = 0; i < corners; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % corners];
                var c1 = colors[i];
                var c2 = colors[(i + 1) % corners];

                // Draw gradient by interpolating between c1 and c2
                for (int s = 0; s < gradientSteps; s++)
                {
                    float t1 = (float)s / gradientSteps;
                    float t2 = (float)(s + 1) / gradientSteps;
                    Vector2 segStart = Vector2.Lerp(p1, p2, t1);
                    Vector2 segEnd = Vector2.Lerp(p1, p2, t2);
                    Vector4 segColor = Vector4.Lerp(c1, c2, t1);
                    uint col = ImGui.ColorConvertFloat4ToU32(segColor);
                    drawList.AddLine(segStart, segEnd, col, thickness);
                }
            }

            return points;
        }
        public static void DrawInlineNavigation(Navigation navigation, ref int selectedNavIndex, bool horizontal = false, float? buttonSizeOverride = null)
        {
            if (navigation == null || navigation.textureIDs == null)
                return;

            float buttonSize = buttonSizeOverride ?? ImGui.GetIO().FontGlobalScale * 45f;
            int pressedIndex = -1;
            int count = navigation.textureIDs.Length;

            for (int i = 0; i < count; i++)
            {
                // honor per-button visibility if provided
                if (navigation.show != null && i < navigation.show.Length && !navigation.show[i])
                    continue;

                ImGui.PushID(i);

                var label = (navigation.names != null && i < navigation.names.Length) ? navigation.names[i] : null;
                bool pressed = CustomLayouts.TransparentImageButton(navigation.textureIDs[i], new Vector2(buttonSize, buttonSize), label);

                if (pressed)
                {
                    pressedIndex = i;
                    // invoke action if present
                    if (navigation.actions != null && i < navigation.actions.Length)
                        navigation.actions[i]?.Invoke();
                        
                    selectedNavIndex = i;
                }

                ImGui.PopID();

                // if horizontal layout requested, place subsequent buttons on same line (only if another visible button follows)
                if (horizontal)
                {
                    bool nextVisible = false;
                    for (int j = i + 1; j < count; j++)
                    {
                        if (navigation.show != null && j < navigation.show.Length && !navigation.show[j])
                            continue;
                        nextVisible = true;
                        break;
                    }
                    if (nextVisible)
                        ImGui.SameLine();
                }
            }

            if (pressedIndex != -1)
                selectedNavIndex = pressedIndex;
        }
        // DrawSideNavigation: added requestFocus parameter and apply focus immediately after Begin.
        public static void DrawSideNavigation(string uniqueParentID, string uniqueID, ref int selectedNavIndex, ImGuiWindowFlags flags, Navigation navigation)
            => DrawSideNavigation(uniqueParentID, uniqueID, ref selectedNavIndex, flags, navigation, false, null);


        // Focus-aware overload. Returns true if the nav window is focused after drawing.
        // focusTargetOnNavClick: if non-null, a click inside the nav window will set focus to that window name.
        public static bool DrawSideNavigation(string uniqueParentID, string uniqueID, ref int selectedNavIndex, ImGuiWindowFlags flags, Navigation navigation, bool requestFocus = false, string focusTargetOnNavClick = null)
        {
            var opened = ImGui.Begin(uniqueID, flags);
            var isFocused = false;

            if (opened)
            {
                // If user clicks inside the nav (and not interacting with an item), focus the parent/main window.
                if (!string.IsNullOrEmpty(uniqueParentID)
                    && ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows)
                    && ImGui.IsMouseClicked(ImGuiMouseButton.Left)
                    && !ImGui.IsAnyItemActive())
                {
                    ImGui.SetWindowFocus(uniqueParentID);
                }

                var buttonSize = ImGui.GetIO().FontGlobalScale * 45f;
                int pressedIndex = -1;

                var navButtons = navigation.textureIDs.Select((icon, idx) =>
                    (Func<bool>)(() =>
                    {
                        var pressed = CustomLayouts.TransparentImageButton(icon, new Vector2(buttonSize, buttonSize), navigation.names[idx]);
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

                isFocused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
            }

            ImGui.End();
            return isFocused;
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
