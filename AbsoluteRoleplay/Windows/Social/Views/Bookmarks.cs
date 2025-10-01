using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using Networking;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Profiles;

namespace AbsoluteRP.Windows.Social.Views
{
    public class Bookmarks
    {
        public static bool DisableBookmarkSelection = false;
        internal static List<Bookmark> profileList = new List<Bookmark>();

        public static void DrawBookmarksUI()
        {
            try
            {
                Vector2 windowSize = ImGui.GetWindowSize();
                float padding = 10f; // Padding between the two columns
                float childWidth = windowSize.X - padding * 3; // Divide width between two children with padding
                float childHeight = windowSize.Y - 80; // Subtract space for top text
                Vector2 childSize = new Vector2(childWidth, childHeight);
                // Start grouping for Bookmarks section
                ImGui.BeginGroup();
                using (var profileTable = ImRaii.Child("Profiles", childSize, true))
                {
                    if (profileTable)
                    {
                        if (Plugin.plugin.IsOnline())
                        {
                            for (var i = 0; i < profileList.Count; i++)
                            {
                                if (DisableBookmarkSelection)
                                    ImGui.BeginDisabled();

                                if (ImGui.Button(profileList[i].ProfileName))
                                {
                                    Plugin.plugin.OpenTargetWindow();
                                    ReportWindow.reportCharacterName = profileList[i].PlayerName;
                                    ReportWindow.reportCharacterWorld = profileList[i].PlayerWorld;
                                    TargetProfileWindow.characterName = profileList[i].PlayerName;
                                    TargetProfileWindow.characterWorld = profileList[i].PlayerWorld;
                                    TargetProfileWindow.RequestingProfile = true;
                                    TargetProfileWindow.ResetAllData();
                                    DataSender.FetchProfile(Plugin.character, false, -1, profileList[i].PlayerName, profileList[i].PlayerWorld, -1);
                                }

                                ImGui.SameLine();

                                using (ImRaii.Disabled(!Plugin.CtrlPressed()))
                                {
                                    if (ImGui.Button("Remove##Removal" + i))
                                    {
                                        DataSender.RemoveBookmarkedPlayer(Plugin.character, profileList[i].PlayerName, profileList[i].profileIndex);
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
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug in BookmarksWindow Draw: {ex.Message}");
            }
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
