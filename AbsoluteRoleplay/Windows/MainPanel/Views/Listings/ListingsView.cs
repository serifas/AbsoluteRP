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

namespace AbsoluteRoleplay.Windows.MainPanel.Views.Listings
{
    internal class ListingsView
    {
        public static void LoadListingsView(Plugin pluginInstance)
        {
            var buttonWidth = MainPanel.buttonWidth;
            var buttonHeight = MainPanel.buttonHeight;
            //row 1
            if (ImGui.ImageButton(MainPanel.listingsEvent.ImGuiHandle, new Vector2(buttonWidth, buttonHeight)))
            {
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Manage your events");
            }
            ImGui.SameLine();
            if (ImGui.ImageButton(MainPanel.listingsCampaign.ImGuiHandle, new Vector2(buttonWidth, buttonHeight)))
            {

            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Manage your campaigns");
            }
            //row 2
            
            if (ImGui.ImageButton(MainPanel.listingsVenue.ImGuiHandle, new Vector2(buttonWidth, buttonHeight)))
            {

            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Manage your venues");
            }
            ImGui.SameLine();
            if (ImGui.ImageButton(MainPanel.listingsGroup.ImGuiHandle, new Vector2(buttonWidth, buttonHeight)))
            {

            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Manage your groups");
            }
            if (ImGui.ImageButton(MainPanel.listingsFC.ImGuiHandle, new Vector2(buttonWidth, buttonHeight)))
            {
            }
           
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Manage your FCs");
            }
            ImGui.SameLine();
            if (ImGui.ImageButton(MainPanel.listingsPersonal.ImGuiHandle, new Vector2(buttonWidth, buttonHeight)))
            {

            }
           
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Manage your personals");
            }
            //row 3
            if(ImGui.Button("View Public Listings", new Vector2(buttonWidth * 2f, buttonHeight / 2f)))
            {
                pluginInstance.OpenListingsWindow();
            }
        }
    }
}
