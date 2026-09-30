using AbsoluteRP.RsUI;
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
using AbsoluteRP.Network;

namespace AbsoluteRP.Windows.Social.Views.SubViews
{
    // Group creation wizard - lets users set up a new group with name, description, logo, and visibility settings
    internal class GroupCreation
    {
        // Small per-group edit buffer so edits persist across frames.
        public static Group group = null;
        public static bool uploadLogo = false;
        public static bool uploadBackground = false;
        public static AbsoluteRP.RsUI.RsFileDialogManager _fileDialogManager; 
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
            string Name = group.name ?? string.Empty;
            string Description = group.description ?? string.Empty;
            bool Visible = group.visible;
            bool OpenInvite = group.openInvite;

            if (!RsElements.BeginPanel("grp_create_card", null, fitContentsX: false, fitContentsY: true, innerPadding: 14f))
            {
                RsElements.EndPanel();
                return;
            }
            try
            {
                // Logo + upload
                float logoSize = RsTheme.S(80f);
                GroupUi.Logo(group.logo, Name, logoSize, RsTheme.S(14f));
                ImGui.SameLine(0f, RsTheme.S(14f));
                ImGui.BeginGroup();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + RsTheme.S(6f));
                ImGui.TextUnformatted("Group Creator");
                GroupUi.Muted("Give your group a name, a logo and a description.");
                ImGui.Dummy(new Vector2(0f, RsTheme.S(6f)));
                if (RsElements.Button("Upload Logo##grp_create_logo", RsElements.ButtonVariant.Secondary))
                {
                    Misc.EditGroupImage(Plugin.plugin, _fileDialogManager, group, true, false, 0);
                }
                ImGui.EndGroup();

                ImGui.Dummy(new Vector2(0f, RsTheme.S(12f)));
                GroupUi.SectionLabel("Group leader");
                ImGui.SetNextItemWidth(RsElements.AvailContentWidth());
                AddGroupLeaderSelection();
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Select the profile that will be represented as the leader of this group");
                }

                ImGui.Dummy(new Vector2(0f, RsTheme.S(8f)));
                GroupUi.SectionLabel("Group profile");
                ImGui.SetNextItemWidth(RsElements.AvailContentWidth());
                AddGroupProfileSelection();
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Select the profile that will represent the group");
                }

                // Name
                ImGui.Dummy(new Vector2(0f, RsTheme.S(8f)));
                GroupUi.SectionLabel("Name");
                if (RsElements.InputText("grp_create_name", ref Name, 256, "Group name"))
                {
                    group.name = Name;
                }

                // Description (multiline)
                ImGui.Dummy(new Vector2(0f, RsTheme.S(8f)));
                GroupUi.SectionLabel("Description");
                if (RsElements.InputTextArea("grp_create_desc", ref Description, 4096, "What is this group about?", new Vector2(RsElements.AvailContentWidth(), RsTheme.S(150f))))
                {
                    group.description = Description;
                }

                // Flags
                ImGui.Dummy(new Vector2(0f, RsTheme.S(8f)));
                GroupUi.SectionLabel("Access");
                if (RsElements.Toggle("grp_create_visible", ref Visible, "Visible in group search"))
                {
                    group.visible = Visible;
                }
                ImGui.Dummy(new Vector2(0f, RsTheme.S(4f)));
                if (RsElements.Toggle("grp_create_open", ref OpenInvite, "Open invite (anyone can join)"))
                {
                    group.openInvite = OpenInvite;
                }

                // Buttons
                ImGui.Dummy(new Vector2(0f, RsTheme.S(12f)));
                if (RsElements.Button("Save##grp_create_save", RsElements.ButtonVariant.Primary))
                {
                    Groups_DS.SetGroupValues(Plugin.character, group, false, leadProfileIndex, profileIndex);
                }
            }
            finally
            {
                RsElements.EndPanel();
            }
        }
        public static void AddGroupProfileSelection()
        {
            try
            {
                if (profiles == null || profiles.Count == 0)
                {
                    ImGui.TextDisabled("No profiles available");
                    return;
                }

                List<string> profileNames = new List<string>();
                for (int i = 0; i < profiles.Count; i++)
                {
                    profileNames.Add(profiles[i].title);
                }
                string[] ProfileNames = profileNames.ToArray();

                // Ensure profileIndex is within bounds
                if (profileIndex < 0 || profileIndex >= ProfileNames.Length)
                {
                    profileIndex = 0;
                }

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
                if (profiles == null || profiles.Count == 0)
                {
                    ImGui.TextDisabled("No profiles available");
                    return;
                }

                List<string> profileNames = new List<string>();
                for (int i = 0; i < profiles.Count; i++)
                {
                    profileNames.Add(profiles[i].title);
                }
                string[] ProfileNames = profileNames.ToArray();

                // Ensure leadProfileIndex is within bounds
                if (leadProfileIndex < 0 || leadProfileIndex >= ProfileNames.Length)
                {
                    leadProfileIndex = 0;
                }

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
