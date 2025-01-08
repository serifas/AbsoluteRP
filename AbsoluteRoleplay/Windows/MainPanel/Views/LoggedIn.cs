using ImGuiNET;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRoleplay.Windows.MainPanel.Views
{
    internal class LoggedIn
    {

        public static void LoadLoggedIn(Plugin pluginInstance)
        {
            MainPanel.loggedIn = MainPanel.CurrentElement();
            var buttonWidth = MainPanel.buttonWidth;
            var buttonHeight = MainPanel.buttonHeight;
            #region PROFILES
            if (ImGui.ImageButton(MainPanel.profileSectionImage.ImGuiHandle, new Vector2(buttonWidth, buttonHeight)))
            {
                MainPanel.viewProfile = MainPanel.CurrentElement();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Profiles");
            }
            #endregion
            ImGui.SameLine();
            if (ImGui.ImageButton(MainPanel.connectionsSectionImage.ImGuiHandle, new Vector2(buttonWidth, buttonHeight)))
            {
                DataSender.RequestConnections(pluginInstance.username.ToString(), pluginInstance.password.ToString());
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Connections");
            }

            if (ImGui.ImageButton(MainPanel.eventsSectionImage.ImGuiHandle, new Vector2(buttonWidth, buttonHeight)))
            {
                MainPanel.viewListings = MainPanel.CurrentElement();
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Listings");
            }

            ImGui.SameLine();

            using (OtterGui.Raii.ImRaii.Disabled(true))
            {
                if (ImGui.ImageButton(MainPanel.systemsSectionImage.ImGuiHandle, new Vector2(buttonWidth, buttonHeight)))
                {
                    // viewConnections = true;
                    // viewMainWindow = false;
                }
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Systems - WIP");
            }
            var chatPos = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(new Vector2(buttonWidth / 14, chatPos));
            if (ImGui.Button("Open ARP Chat", new Vector2(buttonWidth * 2.18f, buttonHeight / 2f)))
            {
                pluginInstance.ToggleChatWindow();
            }

            var optionPos = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(new Vector2(buttonWidth / 14, optionPos));
            if (ImGui.Button("Options", new Vector2(buttonWidth * 2.18f, buttonHeight / 2f)))
            {
                pluginInstance.OpenOptionsWindow();
            }
            var logoutPos = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(new Vector2(buttonWidth / 14, logoutPos));
            if (ImGui.Button("Logout", new Vector2(buttonWidth * 2.18f, buttonHeight / 2f)))
            {
                pluginInstance.newConnection = false;
                pluginInstance.CloseAllWindows();
                pluginInstance.OpenMainPanel();
                MainPanel.login = MainPanel.CurrentElement();
                MainPanel.status = "Logged Out";
                MainPanel.statusColor = new Vector4(255, 0, 0, 255);
            }
        }
    }
}
