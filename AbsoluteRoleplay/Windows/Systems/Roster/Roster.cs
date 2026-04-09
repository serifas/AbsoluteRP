using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Listings;
using Dalamud.Bindings.ImGui;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AbsoluteRP.Windows.Systems.Roster
{
    internal class Roster
    {
        // Sheet list for current system
        public static List<CharacterSheetData> sheets = new List<CharacterSheetData>();
        private static int filterStatus = -1; // -1=All, 0=Pending, 1=Approved, 2=Declined, 3=Revision
        private static int selectedSheetIndex = -1;
        private static bool fetchedRoster = false;

        // Revision popup
        private static bool showRevisionPopup = false;
        private static int revisionSheetId = -1;
        private static string revisionReason = "";

        private static readonly string[] StatusNames = { "Pending", "Approved", "Declined", "Revision Requested" };
        private static readonly Vector4[] StatusColors = {
            new Vector4(1f, 0.8f, 0.2f, 1f),   // Pending - yellow
            new Vector4(0.3f, 1f, 0.3f, 1f),    // Approved - green
            new Vector4(1f, 0.3f, 0.3f, 1f),    // Declined - red
            new Vector4(0.6f, 0.6f, 1f, 1f),    // Revision - blue
        };

        public static void DrawRoster()
        {
            var system = SystemsWindow.currentSystem;
            if (system == null || system.id <= 0)
            {
                ImGui.TextColored(ThemeManager.FontMuted, "Select a system to view its roster.");
                return;
            }

            // Fetch roster on first draw
            if (!fetchedRoster)
            {
                fetchedRoster = true;
                if (Plugin.character != null)
                    Networking.DataSender.FetchSystemRoster(Plugin.character, system.id);
            }

            // Refresh button
            if (ThemeManager.PillButton("Refresh##refreshRoster", new Vector2(80, 26)))
            {
                if (Plugin.character != null)
                    Networking.DataSender.FetchSystemRoster(Plugin.character, system.id);
            }

            ImGui.Spacing();

            // Filter tabs
            if (ImGui.BeginTabBar("##RosterFilter"))
            {
                if (ImGui.BeginTabItem("All"))
                { filterStatus = -1; ImGui.EndTabItem(); }
                for (int i = 0; i < StatusNames.Length; i++)
                {
                    int count = sheets.Count(s => s.status == i);
                    string label = count > 0 ? $"{StatusNames[i]} ({count})" : StatusNames[i];
                    if (ImGui.BeginTabItem(label))
                    { filterStatus = i; ImGui.EndTabItem(); }
                }
                ImGui.EndTabBar();
            }

            ImGui.Spacing();

            // Filtered list
            var filtered = filterStatus < 0 ? sheets : sheets.Where(s => s.status == filterStatus).ToList();

            if (filtered.Count == 0)
            {
                ImGui.TextColored(ThemeManager.FontMuted, "No character sheets found.");
                return;
            }

            for (int i = 0; i < filtered.Count; i++)
            {
                var sheet = filtered[i];
                ImGui.PushID($"sheet_{sheet.id}");

                // Status badge
                int status = Math.Clamp(sheet.status, 0, StatusNames.Length - 1);
                ImGui.TextColored(StatusColors[status], $"[{StatusNames[status]}]");
                ImGui.SameLine();

                // Name + class
                string className = "No Class";
                if (sheet.classId >= 0 && system.SkillClasses.Count > 0)
                {
                    var cls = system.SkillClasses.FirstOrDefault(c => c.id == sheet.classId);
                    if (cls != null) className = cls.name;
                }
                bool expanded = ImGui.TreeNode($"{sheet.characterName} @ {sheet.characterWorld} — {className}##sheet{sheet.id}");

                if (expanded)
                {
                    // Stat values
                    if (sheet.statValues.Count > 0)
                    {
                        ImGui.Text("Stats:");
                        foreach (var sv in sheet.statValues)
                        {
                            var stat = system.StatsData.Values.FirstOrDefault(s => s.id == sv.Key);
                            string statName = stat != null ? stat.name : $"Stat {sv.Key}";
                            ImGui.BulletText($"{statName}: {sv.Value}");
                        }
                    }

                    // Skills
                    if (sheet.learnedSkills.Count > 0)
                    {
                        ImGui.Text("Skills:");
                        foreach (var skillId in sheet.learnedSkills)
                        {
                            var skill = system.Skills.FirstOrDefault(s => s.id == skillId);
                            ImGui.BulletText(skill != null ? skill.name : $"Skill {skillId}");
                        }
                    }

                    // Revision reason
                    if (sheet.status == 3 && !string.IsNullOrEmpty(sheet.revisionReason))
                    {
                        ImGui.Spacing();
                        ImGui.TextColored(StatusColors[3], $"Revision Reason: {sheet.revisionReason}");
                    }

                    ImGui.Spacing();

                    // Action buttons
                    if (sheet.status == 0) // Pending
                    {
                        if (ThemeManager.PillButton("Approve##approve", new Vector2(80, 26)))
                        {
                            if (Plugin.character != null)
                                Networking.DataSender.RespondToSheet(Plugin.character, sheet.id, 1, "");
                        }
                        ImGui.SameLine();
                        if (ThemeManager.DangerButton("Decline##decline", new Vector2(80, 26)))
                        {
                            if (Plugin.character != null)
                                Networking.DataSender.RespondToSheet(Plugin.character, sheet.id, 2, "");
                        }
                        ImGui.SameLine();
                        if (ThemeManager.GhostButton("Request Revision##revision", new Vector2(140, 26)))
                        {
                            revisionSheetId = sheet.id;
                            revisionReason = "";
                            showRevisionPopup = true;
                            ImGui.OpenPopup("##RevisionPopup");
                        }
                    }
                    else if (sheet.status == 1) // Approved
                    {
                        if (ThemeManager.DangerButton("Revoke##revoke", new Vector2(80, 26)))
                        {
                            if (Plugin.character != null)
                                Networking.DataSender.RespondToSheet(Plugin.character, sheet.id, 2, "");
                        }
                    }

                    ImGui.TreePop();
                }

                ImGui.PopID();
            }

            // Revision reason popup
            DrawRevisionPopup();
        }

        private static void DrawRevisionPopup()
        {
            if (!showRevisionPopup) return;

            ImGui.SetNextWindowSize(new Vector2(350, 180), ImGuiCond.FirstUseEver);
            if (ImGui.BeginPopup("##RevisionPopup"))
            {
                ImGui.Text("Reason for revision:");
                ImGui.InputTextMultiline("##revisionReason", ref revisionReason, 500,
                    new Vector2(300, 80));

                ImGui.Spacing();
                if (ThemeManager.PillButton("Send##sendRevision", new Vector2(80, 26)))
                {
                    if (Plugin.character != null && revisionSheetId > 0)
                    {
                        Networking.DataSender.RespondToSheet(Plugin.character, revisionSheetId, 3, revisionReason);
                        showRevisionPopup = false;
                        ImGui.CloseCurrentPopup();
                    }
                }
                ImGui.SameLine();
                if (ThemeManager.GhostButton("Cancel##cancelRevision", new Vector2(80, 26)))
                {
                    showRevisionPopup = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }

        // Called by DataReceiver when roster data arrives
        public static void OnRosterReceived(List<CharacterSheetData> newSheets)
        {
            sheets = newSheets;
            selectedSheetIndex = -1;
            fetchedRoster = true;
        }

        // Called by DataReceiver when a sheet response is processed
        public static void OnSheetResponseReceived(int sheetId, int newStatus)
        {
            var sheet = sheets.FirstOrDefault(s => s.id == sheetId);
            if (sheet != null)
                sheet.status = newStatus;
        }

        // Reset state when system changes
        public static void ResetForSystem()
        {
            sheets.Clear();
            selectedSheetIndex = -1;
            fetchedRoster = false;
        }
    }
}
