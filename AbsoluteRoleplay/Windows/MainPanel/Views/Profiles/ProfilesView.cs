using AbsoluteRoleplay.Windows.Profiles.ProfileTabs;
using AbsoluteRoleplay.Windows.Profiles;
using ImGuiNET;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace AbsoluteRoleplay.Windows.MainPanel.MainPanelTabs.LoggedInTabs
{
    internal class ProfilesView
    {
        public static void LoadProfilesView(Plugin pluginInstance)
        {
            var buttonWidth = MainPanel.buttonWidth;
            var buttonHeight = MainPanel.buttonHeight;
            if (ImGui.ImageButton(MainPanel.profileImage.ImGuiHandle, new Vector2(buttonWidth, buttonHeight)))
            {
                if (pluginInstance.IsOnline())
                {
                    StoryTab.storyTitle = string.Empty;
                    ProfileWindow.oocInfo = string.Empty;
                    pluginInstance.OpenAndLoadProfileWindow();
                }
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Manage your profiles");
            }
            ImGui.SameLine();
            if (ImGui.ImageButton(MainPanel.profileBookmarkImage.ImGuiHandle, new Vector2(buttonWidth, buttonHeight)))
            {
                if (pluginInstance.IsOnline())
                {
                    DataSender.RequestBookmarks();
                    pluginInstance.OpenBookmarksWindow();
                }

            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("View profile bookmarks");
            }
         
            if (ImGui.ImageButton(MainPanel.npcImage.ImGuiHandle, new Vector2(buttonWidth, buttonHeight)))
            {
                DataSender.FetchProfiles();
                DataSender.FetchProfile(ProfileWindow.currentProfile);
                pluginInstance.OpenInventoryWindow();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Manage Inventory");
            }



            ImGui.SameLine();

            using (OtterGui.Raii.ImRaii.Disabled(true))
            {
                if (ImGui.ImageButton(MainPanel.npcBookmarkImage.ImGuiHandle, new Vector2(buttonWidth, buttonHeight)))
                {
                    //  viewConnections = true;
                    // viewMainWindow = false;

                }

            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("View NPC bookmarks - WIP");
            }
        }
    }
}
