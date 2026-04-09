using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Listings;
using AbsoluteRP.Windows.Systems;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
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
        public static List<CharacterSheetData> sheets = new List<CharacterSheetData>();
        private static int filterStatus = -1;
        private static bool fetchedRoster = false;

        // Detail view
        private static CharacterSheetData viewingSheet = null;
        private static int detailTreeIndex = 0;

        // Revision popup
        private static bool showRevisionPopup = false;
        private static int revisionSheetId = -1;
        private static string revisionReason = "";

        // Bans
        public static List<(int id, int userId, string name, string world, string reason, long bannedAt)> bans = new List<(int, int, string, string, string, long)>();
        private static bool fetchedBans = false;
        private static string banCharName = "";
        private static string banCharWorld = "";
        private static string banReason = "";

        // Card grid constants
        private const float CardWidth = 180f;
        private const float CardHeight = 200f;
        private const float CardSpacing = 10f;

        private static readonly string[] StatusNames = { "Pending", "Approved", "Declined", "Revision" };
        private static readonly Vector4[] StatusColors = {
            new Vector4(1f, 0.8f, 0.2f, 1f),
            new Vector4(0.3f, 1f, 0.3f, 1f),
            new Vector4(1f, 0.3f, 0.3f, 1f),
            new Vector4(0.6f, 0.6f, 1f, 1f),
        };

        // Grid constants for skill tree display
        private const int GridCols = 5;
        private const int GridRows = 8;

        /// <summary>
        /// Owner view — manage all sheets (shown in Manage Systems > Roster tab)
        /// </summary>
        public static void DrawRoster()
        {
            var system = SystemsWindow.currentSystem;
            if (system == null || system.id <= 0)
            {
                ImGui.TextColored(ThemeManager.FontMuted, "Select a system to view its roster.");
                return;
            }

            // Detail view
            if (viewingSheet != null)
            {
                DrawSheetDetail(system, true);
                return;
            }

            FetchRosterIfNeeded(system.id);

            // Filter tabs
            if (ImGui.BeginTabBar("##RosterFilter"))
            {
                if (ImGui.BeginTabItem("All"))
                { filterStatus = -1; ImGui.EndTabItem(); }
                for (int i = 0; i < StatusNames.Length; i++)
                {
                    int c = sheets.Count(s => s.status == i);
                    string label = c > 0 ? $"{StatusNames[i]} ({c})" : StatusNames[i];
                    if (ImGui.BeginTabItem(label))
                    { filterStatus = i; ImGui.EndTabItem(); }
                }
                if (ImGui.BeginTabItem($"Bans ({bans.Count})"))
                {
                    filterStatus = 99; // Special value for bans view
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
            ImGui.Spacing();

            // Bans view
            if (filterStatus == 99)
            {
                DrawBansTab(system);
                DrawRevisionPopup();
                return;
            }

            var filtered = filterStatus < 0 ? sheets : sheets.Where(s => s.status == filterStatus).ToList();
            DrawCardGrid(filtered, system, true);

            DrawRevisionPopup();
        }

        /// <summary>
        /// Public view — only approved members (shown in View Systems after acceptance)
        /// </summary>
        public static void DrawPublicRoster(SystemData system)
        {
            if (system == null) return;

            if (viewingSheet != null)
            {
                DrawSheetDetail(system, false);
                return;
            }

            FetchRosterIfNeeded(system.id);

            var approved = sheets.Where(s => s.status == 1).ToList();
            if (approved.Count == 0)
            {
                ImGui.TextColored(ThemeManager.FontMuted, "No active members yet.");
                return;
            }

            ThemeManager.SectionHeader("Active Members");
            ImGui.Spacing();
            DrawCardGrid(approved, system, false);
        }

        private static void FetchRosterIfNeeded(int systemId)
        {
            if (!fetchedRoster)
            {
                fetchedRoster = true;
                if (Plugin.character != null)
                    Networking.DataSender.FetchSystemRoster(Plugin.character, systemId);
            }

            if (ThemeManager.PillButton("Refresh##refreshRoster", new Vector2(80, 26)))
            {
                if (Plugin.character != null)
                    Networking.DataSender.FetchSystemRoster(Plugin.character, systemId);
            }
            ImGui.Spacing();
        }

        // ── Card Grid ──
        private static void DrawCardGrid(List<CharacterSheetData> list, SystemData system, bool isOwner)
        {
            if (list.Count == 0)
            {
                ImGui.TextColored(ThemeManager.FontMuted, "No sheets to display.");
                return;
            }

            float windowWidth = ImGui.GetContentRegionAvail().X;
            int columns = Math.Max(1, (int)(windowWidth / (CardWidth + CardSpacing)));
            int rows = (list.Count + columns - 1) / columns;

            ImGui.BeginChild("##rosterGrid", new Vector2(0, 0), false, ImGuiWindowFlags.AlwaysVerticalScrollbar);
            var drawList = ImGui.GetWindowDrawList();
            Vector2 origin = ImGui.GetCursorScreenPos();

            for (int i = 0; i < list.Count; i++)
            {
                var sheet = list[i];
                int col = i % columns;
                int row = i / columns;

                float x = origin.X + col * (CardWidth + CardSpacing);
                float y = origin.Y + row * (CardHeight + CardSpacing);
                Vector2 cardPos = new Vector2(x, y);
                Vector2 cardEnd = cardPos + new Vector2(CardWidth, CardHeight);

                // Card background
                uint bgColor = ImGui.ColorConvertFloat4ToU32(ThemeManager.BgLighter);
                drawList.AddRectFilled(cardPos, cardEnd, bgColor, 8f);

                // Status accent strip at top
                int status = Math.Clamp(sheet.status, 0, StatusColors.Length - 1);
                uint accentColor = ImGui.ColorConvertFloat4ToU32(StatusColors[status]);
                drawList.AddRectFilled(cardPos, cardPos + new Vector2(CardWidth, 4), accentColor, 8f);

                // Avatar (centered, circular area)
                float avatarSize = 64f;
                Vector2 avatarCenter = cardPos + new Vector2(CardWidth / 2, 40 + avatarSize / 2);
                if (sheet.profileAvatar != null && sheet.profileAvatar.Handle != IntPtr.Zero)
                {
                    Vector2 imgMin = avatarCenter - new Vector2(avatarSize / 2, avatarSize / 2);
                    Vector2 imgMax = avatarCenter + new Vector2(avatarSize / 2, avatarSize / 2);
                    drawList.AddImageRounded(sheet.profileAvatar.Handle, imgMin, imgMax,
                        new Vector2(0, 0), new Vector2(1, 1), 0xFFFFFFFF, avatarSize / 2);
                }
                else
                {
                    drawList.AddCircle(avatarCenter, avatarSize / 2, ImGui.ColorConvertFloat4ToU32(ThemeManager.AccentMuted), 32, 2f);
                }

                // Name
                string displayName = !string.IsNullOrEmpty(sheet.profileName) ? sheet.profileName : sheet.characterName;
                var nameSize = ImGui.CalcTextSize(displayName);
                float nameX = cardPos.X + (CardWidth - nameSize.X) / 2;
                drawList.AddText(new Vector2(nameX, cardPos.Y + 40 + avatarSize + 8), 0xFFFFFFFF, displayName);

                // Class name
                string className = "No Class";
                if (sheet.classId >= 0)
                {
                    var cls = system.SkillClasses.FirstOrDefault(c => c.id == sheet.classId);
                    if (cls != null) className = cls.name;
                }
                var classSize = ImGui.CalcTextSize(className);
                float classX = cardPos.X + (CardWidth - classSize.X) / 2;
                drawList.AddText(new Vector2(classX, cardPos.Y + 40 + avatarSize + 26),
                    ImGui.ColorConvertFloat4ToU32(ThemeManager.AccentMuted), className);

                // Level
                string lvlText = $"Lv. {sheet.level}";
                var lvlSize = ImGui.CalcTextSize(lvlText);
                drawList.AddText(new Vector2(cardPos.X + (CardWidth - lvlSize.X) / 2, cardPos.Y + 40 + avatarSize + 44),
                    ImGui.ColorConvertFloat4ToU32(ThemeManager.FontMuted), lvlText);

                // Card border
                uint borderColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.3f, 0.3f, 0.3f, 0.5f));
                drawList.AddRect(cardPos, cardEnd, borderColor, 8f);

                // Invisible button for click
                ImGui.SetCursorScreenPos(cardPos);
                if (ImGui.InvisibleButton($"##card_{sheet.id}", new Vector2(CardWidth, CardHeight)))
                {
                    viewingSheet = sheet;
                    detailTreeIndex = 0;
                }
                if (ImGui.IsItemHovered())
                {
                    // Hover highlight
                    uint hoverColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 0.05f));
                    drawList.AddRectFilled(cardPos, cardEnd, hoverColor, 8f);
                }
            }

            // Reserve scroll space
            ImGui.SetCursorScreenPos(origin + new Vector2(0, rows * (CardHeight + CardSpacing) + CardSpacing));
            ImGui.EndChild();
        }

        // ── Sheet Detail View ──
        private static void DrawSheetDetail(SystemData system, bool isOwner)
        {
            if (ThemeManager.GhostButton("< Back to Roster##backRoster", new Vector2(140, 26)))
            {
                viewingSheet = null;
                return;
            }

            ImGui.Spacing();

            var sheet = viewingSheet;

            // Avatar + name header (centered like profile window)
            if (sheet.profileAvatar != null && sheet.profileAvatar.Handle != IntPtr.Zero)
            {
                float avSize = 80;
                float centeredX = (ImGui.GetContentRegionAvail().X - avSize) / 2;
                ImGui.SetCursorPosX(centeredX);
                ImGui.Image(sheet.profileAvatar.Handle, new Vector2(avSize, avSize));
            }

            string displayName = !string.IsNullOrEmpty(sheet.profileName) ? sheet.profileName : sheet.characterName;
            var nameSize = ImGui.CalcTextSize(displayName);
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - nameSize.X) / 2);
            ImGui.TextColored(ThemeManager.Accent, displayName);

            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(sheet.characterWorld).X) / 2);
            ImGui.TextColored(ThemeManager.FontMuted, sheet.characterWorld);

            ImGui.Spacing();

            // Class name + icon
            if (sheet.classId >= 0)
            {
                var cls = system.SkillClasses.FirstOrDefault(c => c.id == sheet.classId);
                if (cls != null)
                {
                    float rowWidth = 0;
                    if (cls.iconTexture != null && cls.iconTexture.Handle != IntPtr.Zero)
                        rowWidth += 28;
                    rowWidth += ImGui.CalcTextSize(cls.name).X;

                    ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - rowWidth) / 2);
                    if (cls.iconTexture != null && cls.iconTexture.Handle != IntPtr.Zero)
                    {
                        ImGui.Image(cls.iconTexture.Handle, new Vector2(24, 24));
                        ImGui.SameLine();
                    }
                    ImGui.TextColored(ThemeManager.Accent, cls.name);
                }
            }

            // Level
            string lvlText = $"Level {sheet.level}";
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(lvlText).X) / 2);
            ImGui.Text(lvlText);

            ImGui.Spacing();

            // View Profile button
            float btnWidth = 120;
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - btnWidth) / 2);
            if (ThemeManager.GhostButton("View Profile##viewProf", new Vector2(btnWidth, 26)))
            {
                Plugin.plugin.OpenTargetWindow();
                TargetProfileWindow.characterName = sheet.characterName;
                TargetProfileWindow.characterWorld = sheet.characterWorld;
                TargetProfileWindow.RequestingProfile = true;
                TargetProfileWindow.ResetAllData();
                Networking.DataSender.FetchProfile(Plugin.character, false, -1,
                    sheet.characterName, sheet.characterWorld, -1);
            }

            ImGui.Spacing();
            ThemeManager.GradientSeparator();
            ImGui.Spacing();

            // Owner controls: level + bonus skill points
            if (isOwner)
            {
                ImGui.Text("Level:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(80);
                int lvl = sheet.level;
                if (ImGui.InputInt("##sheetLvl", ref lvl))
                    sheet.level = Math.Max(1, lvl);
                ImGui.SameLine();
                ImGui.Text("Bonus Skill Points:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(80);
                int bsp = sheet.bonusSkillPoints;
                if (ImGui.InputInt("##sheetBSP", ref bsp))
                    sheet.bonusSkillPoints = Math.Max(0, bsp);
                ImGui.SameLine();
                if (ThemeManager.PillButton("Save##saveLvlPts", new Vector2(60, 24)))
                {
                    if (Plugin.character != null)
                        Networking.DataSender.UpdateSheetLevelPoints(Plugin.character, sheet.id, sheet.level, sheet.bonusSkillPoints);
                }

                ImGui.Spacing();

                // Status actions
                int status = Math.Clamp(sheet.status, 0, StatusNames.Length - 1);
                ImGui.Text("Status: ");
                ImGui.SameLine();
                ImGui.TextColored(StatusColors[status], StatusNames[status]);

                if (sheet.status == 0)
                {
                    ImGui.SameLine();
                    if (ThemeManager.PillButton("Approve", new Vector2(70, 24)))
                    {
                        if (Plugin.character != null)
                            Networking.DataSender.RespondToSheet(Plugin.character, sheet.id, 1, "");
                    }
                    ImGui.SameLine();
                    if (ThemeManager.DangerButton("Decline", new Vector2(70, 24)))
                    {
                        if (Plugin.character != null)
                            Networking.DataSender.RespondToSheet(Plugin.character, sheet.id, 2, "");
                    }
                    ImGui.SameLine();
                    if (ThemeManager.GhostButton("Revision", new Vector2(70, 24)))
                    {
                        revisionSheetId = sheet.id;
                        revisionReason = "";
                        showRevisionPopup = true;
                        ImGui.OpenPopup("##RevisionPopup");
                    }
                }
                else if (sheet.status == 1)
                {
                    ImGui.SameLine();
                    if (ThemeManager.DangerButton("Revoke", new Vector2(70, 24)))
                    {
                        if (Plugin.character != null)
                            Networking.DataSender.RespondToSheet(Plugin.character, sheet.id, 2, "");
                    }
                }

                // Ban button
                ImGui.Spacing();
                if (ThemeManager.DangerButton("Ban from System##banSheet", new Vector2(140, 24)))
                {
                    if (Plugin.character != null)
                        Networking.DataSender.BanFromSystem(Plugin.character, system.id,
                            sheet.characterName, sheet.characterWorld, "Banned by system owner");
                    viewingSheet = null;
                    return;
                }

                ImGui.Spacing();
                ThemeManager.GradientSeparator();
                ImGui.Spacing();

                DrawRevisionPopup();
            }

            // Stats
            if (sheet.statValues.Count > 0)
            {
                ThemeManager.SubtitleText("Stats");
                ImGui.Spacing();
                foreach (var sv in sheet.statValues)
                {
                    var stat = system.StatsData.Values.FirstOrDefault(s => s.id == sv.Key);
                    if (stat != null)
                    {
                        ImGui.ColorButton($"##sc{sv.Key}", stat.color,
                            ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoPicker, new Vector2(12, 18));
                        ImGui.SameLine();
                        ImGui.Text($"{stat.name}: {sv.Value}");
                    }
                }
                ImGui.Spacing();
            }

            // Skill tree display
            if (sheet.classId >= 0)
            {
                var cls = system.SkillClasses.FirstOrDefault(c => c.id == sheet.classId);
                if (cls != null)
                {
                    ThemeManager.SubtitleText("Skills");
                    ImGui.Spacing();
                    DrawSheetSkillTree(system, cls, sheet);
                }
            }
        }

        // ── Skill Tree for Sheet Detail ──
        private static void DrawSheetSkillTree(SystemData system, SkillClassData cls, CharacterSheetData sheet)
        {
            int classId = cls.id;

            // Tree tabs
            if (cls.SkillTrees.Count > 0)
            {
                if (detailTreeIndex >= cls.SkillTrees.Count)
                    detailTreeIndex = 0;

                if (ImGui.BeginTabBar("##DetailTreeTabs"))
                {
                    for (int t = 0; t < cls.SkillTrees.Count; t++)
                    {
                        if (ImGui.BeginTabItem(cls.SkillTrees[t].name + $"##dtree{t}"))
                        {
                            detailTreeIndex = t;
                            ImGui.EndTabItem();
                        }
                    }
                    ImGui.EndTabBar();
                }
            }

            float gridWidth = ImGui.GetContentRegionAvail().X;
            float cellSize = Math.Min((gridWidth - 20) / GridCols, 56f);
            float octRadius = cellSize * 0.35f;

            var drawList = ImGui.GetWindowDrawList();
            Vector2 origin = ImGui.GetCursorScreenPos();

            var treeSkills = system.Skills.Where(s => s.classId == classId && s.treeIndex == detailTreeIndex && s.isCastable).ToList();

            // Draw connections
            foreach (var conn in system.SkillConnections)
            {
                var fromSkill = treeSkills.FirstOrDefault(s => s.id == conn.fromSkillId);
                var toSkill = treeSkills.FirstOrDefault(s => s.id == conn.toSkillId);
                if (fromSkill != null && toSkill != null)
                {
                    Vector2 from = origin + new Vector2(fromSkill.gridX * cellSize + cellSize / 2, fromSkill.gridY * cellSize + cellSize / 2);
                    Vector2 to = origin + new Vector2(toSkill.gridX * cellSize + cellSize / 2, toSkill.gridY * cellSize + cellSize / 2);
                    uint lineColor = ImGui.ColorConvertFloat4ToU32(ThemeManager.AccentMuted);
                    drawList.AddLine(from, to, lineColor, 2f);
                }
            }

            // Draw nodes
            for (int y = 0; y < GridRows; y++)
            {
                for (int x = 0; x < GridCols; x++)
                {
                    Vector2 center = origin + new Vector2(x * cellSize + cellSize / 2, y * cellSize + cellSize / 2);
                    var skill = treeSkills.FirstOrDefault(s => s.gridX == x && s.gridY == y);
                    var octPoints = Skills.Skills.GetOctagonPoints(center, octRadius);

                    if (skill != null)
                    {
                        bool isLearned = sheet.learnedSkills.Contains(skill.id);
                        float alpha = isLearned ? 1.0f : 0.3f;

                        if (skill.iconTexture != null && skill.iconTexture.Handle != IntPtr.Zero)
                        {
                            Vector2 imgMin = center - new Vector2(octRadius, octRadius);
                            Vector2 imgMax = center + new Vector2(octRadius, octRadius);
                            var tint = new Vector4(alpha, alpha, alpha, 1f);
                            drawList.AddImage(skill.iconTexture.Handle, imgMin, imgMax,
                                new Vector2(0, 0), new Vector2(1, 1), ImGui.ColorConvertFloat4ToU32(tint));
                            Skills.Skills.MaskSquareToOctagon(drawList, center, octRadius, octPoints);
                        }
                        else
                        {
                            uint fillColor = isLearned
                                ? ImGui.ColorConvertFloat4ToU32(ThemeManager.Accent)
                                : ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 0.2f, 0.2f, 0.5f));
                            Skills.Skills.DrawFilledOctagon(drawList, octPoints, fillColor);
                            string label = skill.name.Length > 5 ? skill.name[..5] + ".." : skill.name;
                            var textSize = ImGui.CalcTextSize(label);
                            uint textCol = isLearned ? 0xFFFFFFFF : 0x66FFFFFF;
                            drawList.AddText(center - textSize / 2, textCol, label);
                        }

                        uint borderColor = isLearned
                            ? ImGui.ColorConvertFloat4ToU32(ThemeManager.Accent)
                            : ImGui.ColorConvertFloat4ToU32(new Vector4(0.3f, 0.3f, 0.3f, 0.4f));
                        drawList.AddPolyline(ref octPoints[0], octPoints.Length, borderColor, ImDrawFlags.Closed, isLearned ? 2.5f : 1f);

                        // Tooltip
                        ImGui.SetCursorScreenPos(center - new Vector2(octRadius, octRadius));
                        ImGui.InvisibleButton($"##dt_{x}_{y}", new Vector2(octRadius * 2, octRadius * 2));
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.TextColored(isLearned ? ThemeManager.Accent : ThemeManager.FontMuted, skill.name);
                            if (!string.IsNullOrEmpty(skill.description))
                                ImGui.TextWrapped(skill.description);
                            ImGui.Text(isLearned ? "Learned" : "Not learned");
                            ImGui.EndTooltip();
                        }
                    }
                    else
                    {
                        uint outlineColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.3f, 0.3f, 0.3f, 0.15f));
                        drawList.AddPolyline(ref octPoints[0], octPoints.Length, outlineColor, ImDrawFlags.Closed, 1f);
                    }
                }
            }

            ImGui.SetCursorScreenPos(origin + new Vector2(0, GridRows * cellSize + 10));
        }

        private static void DrawRevisionPopup()
        {
            if (!showRevisionPopup) return;

            ImGui.SetNextWindowSize(new Vector2(350, 180), ImGuiCond.FirstUseEver);
            if (ImGui.BeginPopup("##RevisionPopup"))
            {
                ImGui.Text("Reason for revision:");
                ImGui.InputTextMultiline("##revisionReason", ref revisionReason, 500, new Vector2(300, 80));
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

        // ── Callbacks ──
        public static void OnRosterReceived(List<CharacterSheetData> newSheets)
        {
            sheets = newSheets;
            fetchedRoster = true;
            viewingSheet = null;
        }

        public static void OnSheetResponseReceived(int sheetId, int newStatus)
        {
            var sheet = sheets.FirstOrDefault(s => s.id == sheetId);
            if (sheet != null)
                sheet.status = newStatus;
        }

        public static void ResetForSystem()
        {
            sheets.Clear();
            bans.Clear();
            fetchedRoster = false;
            fetchedBans = false;
            viewingSheet = null;
        }

        public static void OnBansReceived(List<(int id, int userId, string name, string world, string reason, long bannedAt)> newBans)
        {
            bans = newBans;
            fetchedBans = true;
        }

        // ── Bans Tab ──
        private static void DrawBansTab(SystemData system)
        {
            if (!fetchedBans && Plugin.character != null && system.id > 0)
            {
                fetchedBans = true;
                Networking.DataSender.FetchSystemBans(Plugin.character, system.id);
            }

            // Add ban form
            ThemeManager.SubtitleText("Ban a Player");
            ImGui.SetNextItemWidth(150);
            ImGui.InputTextWithHint("##banName", "Character Name", ref banCharName, 64);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            ImGui.InputTextWithHint("##banWorld", "World", ref banCharWorld, 64);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150);
            ImGui.InputTextWithHint("##banReason", "Reason (optional)", ref banReason, 200);
            ImGui.SameLine();
            if (ThemeManager.DangerButton("Ban##banUser", new Vector2(60, 24)))
            {
                if (!string.IsNullOrWhiteSpace(banCharName) && !string.IsNullOrWhiteSpace(banCharWorld) && Plugin.character != null)
                {
                    Networking.DataSender.BanFromSystem(Plugin.character, system.id, banCharName.Trim(), banCharWorld.Trim(), banReason.Trim());
                    banCharName = "";
                    banCharWorld = "";
                    banReason = "";
                }
            }

            ImGui.Spacing();
            ThemeManager.GradientSeparator();
            ImGui.Spacing();

            // Ban list
            if (bans.Count == 0)
            {
                ImGui.TextColored(ThemeManager.FontMuted, "No bans.");
                return;
            }

            ThemeManager.SubtitleText("Banned Players");
            ImGui.Spacing();

            for (int i = 0; i < bans.Count; i++)
            {
                var ban = bans[i];
                ImGui.PushID($"ban_{ban.id}");

                ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 1), ban.name);
                ImGui.SameLine();
                ImGui.TextColored(ThemeManager.FontMuted, $"@ {ban.world}");
                if (!string.IsNullOrEmpty(ban.reason))
                {
                    ImGui.SameLine();
                    ImGui.TextColored(ThemeManager.FontMuted, $"— {ban.reason}");
                }
                ImGui.SameLine();
                if (ThemeManager.GhostButton("Unban##unban", new Vector2(60, 22)))
                {
                    if (Plugin.character != null)
                        Networking.DataSender.UnbanFromSystem(Plugin.character, system.id, ban.id);
                }

                ImGui.PopID();
            }
        }
    }
}
