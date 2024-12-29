using Dalamud.Plugin.Services;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRoleplay.Windows.Profiles
{
    internal class CustomLayout
    {
        public static bool isTab2Open = true;
        public static void CreateTab(Plugin plugin, bool TabStatus, int type, int index, string TabName)
        {
            // Generate a unique ID for the tab
            string uniqueId = $"{type}_{index}";

            // Begin the tab item with the unique ID
            if (ImGui.BeginTabItem($"{TabName}##{uniqueId}"))
            {
                ProfileWindow.ClearUI();
                TabStatus = true;
                // Draw a circular close button
                ImGui.SameLine(); // Place the button inline with the tab
                ImDrawListPtr drawList = ImGui.GetWindowDrawList();

                // Calculate the button's center and radius
                Vector2 buttonCenter = ImGui.GetCursorScreenPos() + new Vector2(12, 12); // Adjust for position
                float radius = 10.0f;

                // Draw circle background for the close button
                drawList.AddCircleFilled(buttonCenter, radius, ImGui.GetColorU32(ImGuiCol.Button));

                // Draw the "X" symbol inside the circle
                drawList.AddLine(buttonCenter - new Vector2(5, 5), buttonCenter + new Vector2(5, 5), ImGui.GetColorU32(ImGuiCol.Text), 2.0f);
                drawList.AddLine(buttonCenter - new Vector2(5, -5), buttonCenter + new Vector2(5, -5), ImGui.GetColorU32(ImGuiCol.Text), 2.0f);

                // Detect click on the circular close button
                if (ImGui.IsMouseHoveringRect(buttonCenter - new Vector2(radius, radius), buttonCenter + new Vector2(radius, radius)) &&
                    ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    // Log the closure action
                    plugin.logger.Error("We closed it");
                }

                ImGui.EndTabItem();
            }
        }


    }
}
