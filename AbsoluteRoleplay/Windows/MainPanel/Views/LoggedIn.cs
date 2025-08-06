using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Networking;
using System.Numerics;

namespace AbsoluteRoleplay.Windows.MainPanel.Views
{
    internal class LoggedIn
    {

        public static void LoadLoggedIn(Plugin pluginInstance)
        {
            MainPanel.loggedIn = MainPanel.CurrentElement();
            var buttonWidth = MainPanel.buttonWidth;
            var buttonHeight = MainPanel.buttonHeight;
           
            if (ImGui.ImageButton(MainPanel.profileSectionImage.Handle, new Vector2(buttonWidth, buttonHeight)))
            {
                if (pluginInstance.IsOnline())
                {
                    pluginInstance.OpenAndLoadProfileWindow(true, 0);
                }
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Manage your profiles");
            }
            ImGui.SameLine();
            if (ImGui.ImageButton(MainPanel.connectionsSectionImage.Handle, new Vector2(buttonWidth, buttonHeight)))
            {
                DataSender.RequestConnections(pluginInstance.username.ToString(), pluginInstance.password.ToString(), true);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Connections");
            }

            using (ImRaii.Disabled(false))
            {
                if (ImGui.ImageButton(MainPanel.eventsSectionImage.Handle, new Vector2(buttonWidth, buttonHeight)))
                {
                    Plugin.plugin.OpenListingsWindow();
                }
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Public Profiles");
            }
            ImGui.SameLine();

            using (ImRaii.Disabled(true))
            {
                if (ImGui.ImageButton(MainPanel.systemsSectionImage.Handle, new Vector2(buttonWidth, buttonHeight)))
                {

                }
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Systems - WIP");
            }

            var bookmarksPos = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(new Vector2(buttonWidth / 14, bookmarksPos));
            if (ImGui.Button("Bookmarks", new Vector2(buttonWidth * 2.18f, buttonHeight / 2f)))
            {
                if (pluginInstance.IsOnline())
                {
                    DataSender.RequestBookmarks();
                    pluginInstance.OpenBookmarksWindow();
                }

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
          
        }
    }
}
