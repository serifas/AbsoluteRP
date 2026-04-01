using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRP.Helpers
{
    internal class CustomLayouts
    {
        public static ImFontPtr HeaderFont;
        public static void DrawCustomWindow(
                string windowName,
                float headerHeight,
                Vector4 headerTextColor,
                string headerText,
                ref int selectedNavIndex,
                float navWidth = 64f,
                float navButtonHeight = 64f,
                float navRounding = 18f,
                Func<bool>[] navButtons = null,
                Action onOpen = null
            )
        {
            ImGuiWindowFlags flags = ImGuiWindowFlags.NoBackground;
            var windowOpen = ImGui.Begin(windowName, flags);
            try
            {
                if (!windowOpen) return;

                onOpen?.Invoke();
                var drawList = ImGui.GetWindowDrawList();
                var min = ImGui.GetWindowPos();
                var size = ImGui.GetWindowSize();
                var max = new Vector2(min.X + size.X, min.Y + size.Y);

                // Use theme colors
                var bgColor = ThemeManager.Background;
                var borderColor = ThemeManager.Accent;
                float outlineThickness = 1.5f;
                float rounding = navRounding;

                // Draw main window background with rounded corners and accent outline
                drawList.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(bgColor), rounding);
                drawList.AddRect(min, max, ImGui.ColorConvertFloat4ToU32(new Vector4(borderColor.X, borderColor.Y, borderColor.Z, 0.5f)), rounding, 0, outlineThickness);

                // Draw header with subtle gradient effect
                var headerMax = new Vector2(min.X + size.X, min.Y + headerHeight);
                var headerBg = ThemeManager.Darken(ThemeManager.Background, 0.02f);
                drawList.AddRectFilled(min, headerMax, ImGui.ColorConvertFloat4ToU32(headerBg), rounding, ImDrawFlags.RoundCornersTop);

                // Header bottom separator — subtle accent line
                drawList.AddLine(
                    new Vector2(min.X + 8, headerMax.Y),
                    new Vector2(headerMax.X - 8, headerMax.Y),
                    ImGui.ColorConvertFloat4ToU32(ThemeManager.AccentMuted), 1f);

                // Center and draw header text with custom font
                if (!HeaderFont.Equals(default) && !string.IsNullOrEmpty(headerText))
                {
                    ImGui.PushFont(HeaderFont);

                    var textSize = ImGui.CalcTextSize(headerText);
                    var textPos = new Vector2(
                        min.X + (size.X - textSize.X) / 2,
                        min.Y + (headerHeight - textSize.Y) / 2
                    );
                    drawList.AddText(textPos, ImGui.ColorConvertFloat4ToU32(headerTextColor), headerText);

                    ImGui.PopFont();
                }

                // Draw vertical navigation if buttons are provided
                if (navButtons != null && navButtons.Length > 0)
                {
                    selectedNavIndex = DrawVerticalImageButtonNavigation(navButtons, selectedNavIndex, navWidth, navButtonHeight, navRounding);

                    // Move cursor to the right of the navigation bar for further UI
                    ImGui.SetCursorScreenPos(new Vector2(min.X + navWidth + 8f, min.Y + headerHeight + 8f));
                }
                else
                {
                    // Move cursor below header if no navigation
                    ImGui.SetCursorScreenPos(new Vector2(min.X, min.Y + headerHeight + 8f));
                }
            }
            finally
            {
                ImGui.End();
            }
        }

        public static int DrawVerticalImageButtonNavigation(
      Func<bool>[] imageButtons, int selectedIndex,
      float navWidth = 64f, float buttonHeight = 64f, float rounding = 18f)
        {
            float scale = ImGui.GetIO().FontGlobalScale;
            if (scale == 0f) scale = 1f;

            float navWidthScaled = navWidth * scale;
            float buttonHeightScaled = buttonHeight * scale;
            float roundingScaled = rounding * scale;

            int newSelectedIndex = selectedIndex;
            var windowPos = ImGui.GetWindowPos();
            var navStart = new Vector2(windowPos.X, windowPos.Y);
            var navEnd = new Vector2(windowPos.X + navWidthScaled, windowPos.Y + imageButtons.Length * buttonHeightScaled);

            // Nav panel background — uses theme
            var navBg = ThemeManager.Lighten(ThemeManager.Background, 0.04f);
            ImGui.GetWindowDrawList().AddRectFilled(navStart, navEnd,
                ImGui.ColorConvertFloat4ToU32(navBg),
                roundingScaled, ImDrawFlags.RoundCornersLeft);

            ImGui.SetCursorScreenPos(navStart);

            for (int i = 0; i < imageButtons.Length; i++)
            {
                var btnPos = new Vector2(windowPos.X, windowPos.Y + i * buttonHeightScaled);
                var btnEnd = new Vector2(windowPos.X + navWidthScaled, windowPos.Y + (i + 1) * buttonHeightScaled);

                if (i == selectedIndex)
                {
                    // Selected nav item — accent color with slight transparency
                    ImGui.GetWindowDrawList().AddRectFilled(
                        btnPos, btnEnd,
                        ImGui.ColorConvertFloat4ToU32(ThemeManager.Darken(ThemeManager.Accent, 0.08f)),
                        roundingScaled, ImDrawFlags.RoundCornersLeft);

                    // Active indicator bar on the right edge
                    ImGui.GetWindowDrawList().AddRectFilled(
                        new Vector2(btnEnd.X - 3f, btnPos.Y + 6f),
                        new Vector2(btnEnd.X, btnEnd.Y - 6f),
                        ImGui.ColorConvertFloat4ToU32(ThemeManager.Accent),
                        2f);
                }
                else
                {
                    // Hover effect
                    var mousePos = ImGui.GetMousePos();
                    if (mousePos.X >= btnPos.X && mousePos.X <= btnEnd.X &&
                        mousePos.Y >= btnPos.Y && mousePos.Y <= btnEnd.Y)
                    {
                        ImGui.GetWindowDrawList().AddRectFilled(
                            btnPos, btnEnd,
                            ImGui.ColorConvertFloat4ToU32(ThemeManager.AccentSubtle),
                            roundingScaled, ImDrawFlags.RoundCornersLeft);
                    }
                }

                ImGui.SetCursorScreenPos(btnPos);
                ImGui.PushID(i);
                if (imageButtons[i].Invoke())
                {
                    newSelectedIndex = i;
                }
                ImGui.PopID();
            }

            ImGui.SetCursorScreenPos(new Vector2(windowPos.X + navWidthScaled + 8f, windowPos.Y));
            return newSelectedIndex;
        }

        public static bool TransparentImageButton(ImTextureID textureId, Vector2 size, string tooltip = null)
        {
            Vector2 pos = ImGui.GetCursorScreenPos();
            bool pressed = ImGui.InvisibleButton($"##imgbtn_{textureId}_{pos.X}_{pos.Y}", size);

            // Brighter on hover, with smooth accent tint
            Vector4 tint;
            if (ImGui.IsItemActive())
                tint = new Vector4(ThemeManager.Accent.X * 0.8f + 0.2f, ThemeManager.Accent.Y * 0.8f + 0.2f, ThemeManager.Accent.Z * 0.8f + 0.2f, 1.0f);
            else if (ImGui.IsItemHovered())
                tint = new Vector4(1, 1, 1, 1.0f);
            else
                tint = new Vector4(0.85f, 0.85f, 0.85f, 0.75f);

            ImGui.GetWindowDrawList().AddImage(
                textureId,
                pos,
                new Vector2(pos.X + size.X, pos.Y + size.Y),
                Vector2.Zero,
                Vector2.One,
                ImGui.ColorConvertFloat4ToU32(tint)
            );

            if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
                ImGui.SetTooltip(tooltip);

            return pressed;
        }
    }
}
