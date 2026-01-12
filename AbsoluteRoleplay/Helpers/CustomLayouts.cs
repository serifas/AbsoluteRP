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

                // Colors
                Vector4 black = new Vector4(0f, 0f, 0f, 1f);
                Vector4 blueOutline = new Vector4(0.25f, 0.45f, 0.85f, 1f);
                float outlineThickness = 2.0f;
                float rounding = navRounding;

                // Draw main window background (black with rounded corners and blue outline)
                drawList.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(black), rounding);
                drawList.AddRect(min, max, ImGui.ColorConvertFloat4ToU32(blueOutline), rounding, 0, outlineThickness);

                // Draw header background (black with rounded top corners and blue outline)
                var headerMax = new Vector2(min.X + size.X, min.Y + headerHeight);
                drawList.AddRectFilled(min, headerMax, ImGui.ColorConvertFloat4ToU32(black), rounding, ImDrawFlags.RoundCornersTop);
                drawList.AddRect(min, headerMax, ImGui.ColorConvertFloat4ToU32(blueOutline), rounding, ImDrawFlags.RoundCornersTop, outlineThickness);

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

                // Your window content here...
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

            ImGui.GetWindowDrawList().AddRectFilled(navStart, navEnd,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.13f, 0.15f, 0.18f, 1.0f)),
                roundingScaled, ImDrawFlags.RoundCornersLeft);

            ImGui.SetCursorScreenPos(navStart);

            for (int i = 0; i < imageButtons.Length; i++)
            {
                var btnPos = new Vector2(windowPos.X, windowPos.Y + i * buttonHeightScaled);
                var btnEnd = new Vector2(windowPos.X + navWidthScaled, windowPos.Y + (i + 1) * buttonHeightScaled);

                if (i == selectedIndex)
                {
                    ImGui.GetWindowDrawList().AddRectFilled(
                        btnPos, btnEnd,
                        ImGui.ColorConvertFloat4ToU32(new Vector4(0.25f, 0.45f, 0.85f, 1.0f)),
                        roundingScaled, ImDrawFlags.RoundCornersLeft);
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

            Vector4 tint = (ImGui.IsItemHovered() || ImGui.IsItemActive())
                ? new Vector4(1,1,1, 1.0f)
                : new Vector4(1f, 1f, 1f, 0.8f);

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
