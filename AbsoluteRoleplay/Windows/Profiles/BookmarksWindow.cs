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
        public static SortedList<string, string> profiles = new SortedList<string, string>();
        private IDalamudPluginInterface pg;
        public static bool DisableBookmarkSelection = false;
        internal static List<Tuple<int, string>> profileList = new System.Collections.Generic.List<Tuple<int, string>>();

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
            float childWidth = (windowSize.X - padding * 3) / 2; // Divide width between two children with padding
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
                        for (var i = 1; i < profiles.Count; i++)
                        {
                            if (DisableBookmarkSelection)
                                ImGui.BeginDisabled();

                            if (ImGui.Button(profiles.Keys[i] + " @ " + profiles.Values[i]))
                            {
                                ReportWindow.reportCharacterName = profiles.Keys[i];
                                ReportWindow.reportCharacterWorld = profiles.Values[i];
                                TargetWindow.characterNameVal = profiles.Keys[i];
                                TargetWindow.characterWorldVal = profiles.Values[i];
                                DataSender.RequestTargetProfiles(profiles.Keys[i], profiles.Values[i]);
                            }

                            ImGui.SameLine();

                            using (ImRaii.Disabled(!Plugin.CtrlPressed()))
                            {
                                if (ImGui.Button("Remove##Removal" + i))
                                {
                                    DataSender.RemoveBookmarkedPlayer(profiles.Keys[i], profiles.Values[i]);
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
            ImGui.SameLine();
            // Start grouping for Profiles Available section
            ImGui.BeginGroup();
            ImGui.Text("Profiles");

            using (var availableProfiles = ImRaii.Child("Profiles Available", childSize, true))
            {
                if (availableProfiles)
                {
                    if (plugin.IsOnline())
                    {
                        for (var i = 0; i < profileList.Count; i++)
                        {
                            string name = profileList[i].Item2;
                            if (name == string.Empty)
                            {
                                name = "Unknown";
                            }
                            if (ImGui.Button(profileList[i].Item2))
                            {
                                DataSender.RequestTargetProfile(profileList[i].Item1);
                            }
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
}
