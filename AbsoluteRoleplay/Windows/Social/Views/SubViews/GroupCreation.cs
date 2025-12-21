using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Storage.Assets;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Networking;
using Serilog.Filters;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRP.Windows.Social.Views.SubViews
{
    internal class GroupCreation
    {
        // Small per-group edit buffer so edits persist across frames.
        public static Group group = null;
        public static bool uploadLogo = false;
        public static bool uploadBackground = false;
        public static FileDialogManager _fileDialogManager; 
        public static int leadProfileIndex = 0;
        public static int profileIndex = 0;
        public static int groupIndex = 0;
        public static List<ProfileData> profiles = new List<ProfileData>();
        public static ProfileData groupLeaderProfile = new ProfileData();
        public static ProfileData groupProfile = new ProfileData();
        public static void DrawGroupBaseEditor()
        {
            if (group == null) return;

            _fileDialogManager.Draw();
            string Name = group.name;
            string Description = group.description;
            IDalamudTextureWrap logo = group.logo;
            IDalamudTextureWrap BackgroundURL = group.background;
            Misc.DrawCenteredImage(logo, new Vector2(100, 100));
            bool Visible = group.visible;
            bool OpenInvite = group.openInvite;
            // Header
            ImGui.Text("Group Creator");
            ImGui.Separator();

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
            // Name
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Name");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##group_name", ref Name))
            {
                group.name = Name;
            }

            if (Misc.DrawCenteredButton("Upload Logo"))
            {
                Misc.EditGroupImage(Plugin.plugin, _fileDialogManager, group, true, false, 0);
            }
            if (Misc.DrawCenteredButton("Upload Background"))
            {
                Misc.EditGroupImage(Plugin.plugin, _fileDialogManager, group, false, true, 0);
            }
            // Description (multiline)
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Description");
            ImGui.SetNextItemWidth(-1);
            if(ImGui.InputTextMultiline("##group_description", ref Description, 4096, new Vector2(-1, 150)))
            {
                group.description = Description;
            }

            // Flags
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Visible");
            if (ImGui.Checkbox("##visible", ref Visible))
            {
                group.visible = Visible;
            }

            ImGui.AlignTextToFramePadding();
            ImGui.Text("Open Invite");
            if (ImGui.Checkbox("##openinvite", ref OpenInvite))
            {
                group.openInvite = OpenInvite;
            }

            // Buttons
            if (ImGui.Button("Save"))
            {
                DataSender.SetGroupValues(Plugin.character, group, false, leadProfileIndex, profileIndex);
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
