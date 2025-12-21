using AbsoluteRP.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRP.Windows.Social.Views.SubViews
{
    internal class GroupManager
    {
        public static List<ProfileData> profiles = new List<ProfileData>();
        public static ProfileData groupLeaderProfile = new ProfileData();
        public static ProfileData groupProfile = new ProfileData();
        public static FileDialogManager _fileDialogManager;
        public static int profileIndex;
        public static int leadProfileIndex;
        public static void ManageGroup(Group group)
        {
            if (_fileDialogManager == null)
                _fileDialogManager = new Dalamud.Interface.ImGuiFileDialog.FileDialogManager();

            _fileDialogManager.Draw();
            Misc.DrawCenteredImage(group.logo, new System.Numerics.Vector2(ImGui.GetIO().FontGlobalScale * 50, ImGui.GetIO().FontGlobalScale * 50), false);

            if (Misc.DrawCenteredButton("Change Logo"))
            {
                Misc.EditGroupImage(Plugin.plugin, _fileDialogManager, group, true, false, 0);
            }

            ImGui.Text("Group Leader");
            AddGroupLeaderSelection();
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Select the profile that will be represented as the leader of this group");
            }
            ImGui.Text("Group Profile");
            AddGroupProfileSelection();
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Select the profile that will represent the group");
            }
            
            if (Misc.DrawCenteredButton("Save Settings"))
            {
                DataSender.SetGroupValues(Plugin.character, group, true, leadProfileIndex, profileIndex);
            }
        }

        public static void AddGroupProfileSelection()
        {
            try
            {
                List<string> profileNames = new List<string>();
                for (int i = 0; i < profiles.Count; i++)
                {
                    profileNames.Add(profiles[i].title);
                }
                string[] ProfileNames = new string[profileNames.Count];
                ProfileNames = profileNames.ToArray();
                var profileName = ProfileNames[profileIndex];

                using var combo = ImRaii.Combo("##GroupProfile", profileName);
                if (!combo)
                    return;
                foreach (var (newText, idx) in ProfileNames.WithIndex())
                {
                    if (profiles.Count > 0)
                    {
                        var label = newText;
                        if (label == string.Empty)
                        {
                            label = "New Profile";
                        }
                        if (newText != string.Empty)
                        {
                            if (ImGui.Selectable(label + "##" + idx, idx == profileIndex))
                            {
                                groupProfile = profiles[idx];
                                profileIndex = idx;
                            }
                            UIHelpers.SelectableHelpMarker("Select to edit tooltipData");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("ProfileWindow AddProfileSelection Debug: " + ex.Message);
            }
        }
        public static void AddGroupLeaderSelection()
        {
            try
            {
                List<string> profileNames = new List<string>();
                for (int i = 0; i < profiles.Count; i++)
                {
                    profileNames.Add(profiles[i].title);
                }
                string[] ProfileNames = new string[profileNames.Count];
                ProfileNames = profileNames.ToArray();
                var profileName = ProfileNames[leadProfileIndex];

                using var combo = ImRaii.Combo("##GroupLeader", profileName);
                if (!combo)
                    return;
                foreach (var (newText, idx) in ProfileNames.WithIndex())
                {
                    if (profiles.Count > 0)
                    {
                        var label = newText;
                        if (label == string.Empty)
                        {
                            label = "New Profile";
                        }
                        if (newText != string.Empty)
                        {
                            if (ImGui.Selectable(label + "##" + idx, idx == leadProfileIndex))
                            {
                                groupLeaderProfile = profiles[idx];
                                leadProfileIndex = idx;
                            }
                            UIHelpers.SelectableHelpMarker("Select to edit tooltipData");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("ProfileWindow AddProfileSelection Debug: " + ex.Message);
            }
        }
    }
}
