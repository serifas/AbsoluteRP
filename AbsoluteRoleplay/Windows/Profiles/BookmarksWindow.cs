using Dalamud.Interface.Colors;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using ImGuiScene;
using OtterGui.Raii;
using OtterGui;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dalamud.Interface.GameFonts;
using Dalamud.Game.Gui.Dtr;
using Microsoft.VisualBasic;
using Networking;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.Havok;
using System.Text.RegularExpressions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface.Utility;
using AbsoluteRoleplay.Helpers;

namespace AbsoluteRoleplay.Windows.Profiles
{
    public class BookmarksWindow : Window, IDisposable
    {
        private Plugin plugin;
        private IDalamudPluginInterface pg;
        public static bool DisableBookmarkSelection = false;
        internal static List<Bookmark> profileList = new List<Bookmark>();

        public BookmarksWindow(Plugin plugin) : base(
       "PROFILE LIST", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(300, 300),
                MaximumSize = new Vector2(800, 800)
            };
            this.plugin = plugin;
        }
        public override void Draw()
        {
            Vector2 windowSize = ImGui.GetWindowSize();
            float padding = 10f; // Padding between the two columns
            float childWidth = (windowSize.X - padding * 3); // Divide width between two children with padding
            float childHeight = windowSize.Y - 80; // Subtract space for top text
            Vector2 childSize = new Vector2(childWidth, childHeight);
            // Start grouping for Bookmarks section
            ImGui.BeginGroup();
            ImGui.Text("Bookmarks");

            using (var profileTable = ImRaii.Child("Profiles", childSize, true))
            {
                if (profileTable)
                {
                    if (plugin.IsOnline())
                    {
                        for (var i = 0; i < profileList.Count; i++)
                        {
                            if (DisableBookmarkSelection)
                                ImGui.BeginDisabled();

                            if (ImGui.Button(profileList[i].ProfileName))
                            {
                                ReportWindow.reportCharacterName = profileList[i].PlayerName;
                                ReportWindow.reportCharacterWorld = profileList[i].PlayerWorld;
                                TargetWindow.characterNameVal = profileList[i].PlayerName;
                                TargetWindow.characterWorldVal = profileList[i].PlayerWorld;
                                DataSender.RequestTargetProfile(profileList[i].profileIndex);
                            }

                            ImGui.SameLine();

                            using (ImRaii.Disabled(!Plugin.CtrlPressed()))
                            {
                                if (ImGui.Button("Remove##Removal" + i))
                                {
                                    DataSender.RemoveBookmarkedPlayer(profileList[i].PlayerName, profileList[i].profileIndex);
                                }
                            }

                            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            {
                                ImGui.SetTooltip("Ctrl Click to Enable");
                            }

                            if (DisableBookmarkSelection)
                                ImGui.EndDisabled();
                        }

                    }
                }
            }
            ImGui.EndGroup();
            // Position cursor to the right of the Profiles Available section for Bookmarks

          
        }



        public void Dispose()
        {

        }
    }
    public class Bookmark
    {
        public int profileIndex { get; set; }
        public string ProfileName { get; set; }
        public string PlayerName { get; set; }
        public string PlayerWorld { get; set; }
    }
}
