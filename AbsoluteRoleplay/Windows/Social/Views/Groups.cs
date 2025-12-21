using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.NavLayouts;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Social.Views.SubViews;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRP.Windows.Social.Views
{
    internal class Groups
    {
        public static Group currentGroup;
        public static bool openGroupCreation = false;

        public static List<Group> groups = new List<Group>();
        public static int selectedNavIndex = 0;
        public static bool createGroup = false;
        private static string createGroupName;
        public static bool manageGroup = false;
        public static bool setBack = false;

        public static void LoadGroupList()
        {
            // Create a 2-column table: left for navigation (fixed width), right for group content (stretch).
            using var table = ImRaii.Table("GroupsTable", 2, ImGuiTableFlags.BordersV);
            if (!table)
                return;

            // Column setup: first column fixed width for stable nav positioning
            ImGui.TableSetupColumn("Nav", ImGuiTableColumnFlags.WidthFixed, ImGui.GetIO().FontGlobalScale * 45);
            ImGui.TableSetupColumn("Content", ImGuiTableColumnFlags.WidthStretch);

            // Single row containing both columns
            ImGui.TableNextRow();

            // Left column: navigation (vertical)
            ImGui.TableSetColumnIndex(0);
            ImGui.BeginGroup();
            var nav = NavigationLayouts.GroupsNavigation(groups);
            float buttonSize = ImGui.GetIO().FontGlobalScale * 45f;
            UIHelpers.DrawInlineNavigation(nav, ref selectedNavIndex, false, buttonSize);
            createGroup = CustomLayouts.TransparentImageButton(UI.UICommonImage(UI.CommonImageTypes.create).Handle, new Vector2(buttonSize, buttonSize), "Create Group");
            if (createGroup)
            {
                // open the creation editor and ensure GroupCreation has an edit buffer and file dialog manager
                openGroupCreation = true;

                // Provide a fresh in-memory Group instance so DrawGroupBaseEditor() will render.
                GroupCreation.group = new Group
                {
                    name = string.Empty,
                    description = string.Empty,
                    visible = true,
                    openInvite = false,
                    ranks = new List<GroupRank>(),
                    members = new List<GroupMember>(),
                    bans = new List<GroupBans>(),
                    categories = new List<GroupCategory>(),
                    application = null
                };

                // Ensure file dialog manager exists (DrawGroupBaseEditor calls _fileDialogManager.Draw())
                if (GroupCreation._fileDialogManager == null)
                    GroupCreation._fileDialogManager = new Dalamud.Interface.ImGuiFileDialog.FileDialogManager();
                if (GroupManager._fileDialogManager == null)
                    GroupManager._fileDialogManager = new Dalamud.Interface.ImGuiFileDialog.FileDialogManager();
            }
            ImGui.EndGroup();

            // Right column: selected group details (stays in fixed column so nav position stable)
            ImGui.TableSetColumnIndex(1);
            ImGui.BeginGroup();
            if (currentGroup != null && !openGroupCreation && !manageGroup)
            {
                Misc.DrawCenteredImage(currentGroup.logo, new System.Numerics.Vector2(ImGui.GetIO().FontGlobalScale * 50, ImGui.GetIO().FontGlobalScale * 50), false);
                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetWindowSize().X - ImGui.GetIO().FontGlobalScale * 45);
                if (CustomLayouts.TransparentImageButton(UI.UICommonImage(UI.CommonImageTypes.socialGroupSettings).Handle, new Vector2(ImGui.GetIO().FontGlobalScale * 20, ImGui.GetIO().FontGlobalScale * 20)))
                {
                    manageGroup = true;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Manage Group");
                }
                Misc.SetTitle(Plugin.plugin, true, currentGroup.name, new Vector4(1, 1, 1, 1), ImGui.GetIO().FontGlobalScale * 22.5f);
                ImGui.Spacing();
                Misc.DrawCenteredWrappedText(currentGroup.description, true, false);

            }
            if (openGroupCreation)
            {
                GroupCreation.DrawGroupBaseEditor();
            }
            if (manageGroup)
            {
                GroupManager.ManageGroup(currentGroup);
            }
            ImGui.EndGroup();
        }
        public static void LoadGroup(Group group)
        {
            currentGroup = group;
        }
    }
}