using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI;
using AbsoluteRP.Windows.Listings;
using AbsoluteRP.Windows.Systems;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AbsoluteRP.Network;

namespace AbsoluteRP.Windows.Systems.Roster
{
    // System roster viewer - shows all character sheets submitted to a system, with approval/decline controls for system owners and field value editing.
    internal class Roster
    {
        public static List<CharacterSheetData> sheets = new List<CharacterSheetData>();
        private static int filterStatus = -1;
        private static bool fetchedRoster = false;

        // Detail view
        private static CharacterSheetData viewingSheet = null;
        private static int detailTreeIndex = 0;
        private static int detailTab = 0; // 0=Stats, 1=Skills, 2=Resources

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

        // Confirm popups for revoke/ban
        private static bool showRevokeConfirm = false;
        private static int revokeSheetId = -1;
        private static bool showBanConfirm = false;
        private static int banConfirmSystemId = -1;
        private static string banConfirmName = "";
        private static string banConfirmWorld = "";

        // Card grid constants (design px, scaled at draw time)
        private const float CardWidth = 180f;
        private const float CardHeight = 214f;
        private const float CardSpacing = 12f;

        private static readonly string[] StatusNames = { "Pending", "Approved", "Declined", "Revision" };

        private static readonly List<RsElements.NavItem> DetailNavItems = new()
        {
            new(FontAwesomeIcon.SlidersH, "Stats"),
            new(FontAwesomeIcon.ProjectDiagram, "Skills"),
            new(FontAwesomeIcon.Heart, "Resources"),
        };

        // Grid constants for skill tree display
        private const int GridCols = 5;
        private const int GridRows = 8;

        /// Owner view - manage all sheets (shown in Manage Systems > Roster tab)
        public static void DrawRoster()
        {
            var system = SystemsWindow.currentSystem;
            if (system == null || system.id <= 0)
            {
                SysUI.Muted("Select a system to view its roster.");
                return;
            }

            // Detail view
            if (viewingSheet != null)
            {
                DrawSheetDetail(system, true);
                return;
            }

            FetchRosterIfNeeded(system.id);

            // Filter tabs: All, one per status (with counts), Bans
            var navItems = new List<RsElements.NavItem>(StatusNames.Length + 2)
            {
                new(FontAwesomeIcon.ListUl, "All"),
            };
            FontAwesomeIcon[] statusIcons = { FontAwesomeIcon.Bell, FontAwesomeIcon.Star, FontAwesomeIcon.Times, FontAwesomeIcon.Retweet };
            for (int i = 0; i < StatusNames.Length; i++)
            {
                int c = sheets.Count(s => s.status == i);
                navItems.Add(new(statusIcons[i], c > 0 ? $"{StatusNames[i]} ({c})" : StatusNames[i]));
            }
            navItems.Add(new(FontAwesomeIcon.Gavel, $"Bans ({bans.Count})"));

            int navSel = filterStatus == 99 ? StatusNames.Length + 1 : filterStatus < 0 ? 0 : filterStatus + 1;
            if (navSel < 0 || navSel >= navItems.Count) navSel = 0;
            RsElements.NavigationMenu("sys_roster_filter", ref navSel, navItems);
            filterStatus = navSel == 0 ? -1 : navSel == StatusNames.Length + 1 ? 99 : navSel - 1;
            SysUI.Gap(10f);

            // Bans view
            if (filterStatus == 99)
            {
                DrawBansTab(system);
                DrawRevisionPopup();
                return;
            }

            var filtered = filterStatus < 0 ? sheets : sheets.Where(s => s.status == filterStatus).ToList();
            // Deduplicate: one entry per character per status view
            filtered = DeduplicateSheets(filtered);
            DrawCardGrid(filtered, system, true);

            DrawRevisionPopup();
        }

        /// Public view - only approved members (shown in View Systems after acceptance)
        public static void DrawPublicRoster(SystemData system)
        {
            if (system == null) return;

            if (viewingSheet != null)
            {
                DrawSheetDetail(system, false);
                return;
            }

            FetchRosterIfNeeded(system.id);

            // Deduplicate: one entry per character, prefer approved
            var approved = DeduplicateSheets(sheets.Where(s => s.status == 1).ToList());
            if (approved.Count == 0)
            {
                SysUI.Muted("No active members yet.");
                return;
            }

            SysUI.SectionLabel($"Active Members ({approved.Count})");
            SysUI.Gap(2f);
            DrawCardGrid(approved, system, false);
        }

        private static void FetchRosterIfNeeded(int systemId)
        {
            if (!fetchedRoster)
            {
                fetchedRoster = true;
                if (Plugin.character != null)
                    AbsoluteRP.Network.Systems_DS.FetchSystemRoster(Plugin.character, systemId);
            }

            if (RsElements.Button("Refresh##refreshRoster", RsElements.ButtonVariant.Secondary))
            {
                if (Plugin.character != null)
                    AbsoluteRP.Network.Systems_DS.FetchSystemRoster(Plugin.character, systemId);
            }
            SysUI.Gap(8f);
        }

        private static string Truncate(string text, float maxW)
        {
            text ??= string.Empty;
            if (ImGui.CalcTextSize(text).X <= maxW) return text;
            for (int n = text.Length - 1; n > 0; n--)
            {
                var t = text.Substring(0, n) + "..";
                if (ImGui.CalcTextSize(t).X <= maxW) return t;
            }
            return "..";
        }

        // Card Grid
        private static void DrawCardGrid(List<CharacterSheetData> list, SystemData system, bool isOwner)
        {
            if (list.Count == 0)
            {
                SysUI.Muted("No sheets to display.");
                return;
            }

            float cardW = SysUI.S(CardWidth);
            float cardH = SysUI.S(CardHeight);
            float spacing = SysUI.S(CardSpacing);

            // Grid flows in the page (no inner scroll child) so it scrolls with the window like other tabs.
            {
                float windowWidth = ImGui.GetContentRegionAvail().X;
                int columns = Math.Max(1, (int)((windowWidth + spacing) / (cardW + spacing)));
                int rows = (list.Count + columns - 1) / columns;

                var drawList = ImGui.GetWindowDrawList();
                Vector2 origin = ImGui.GetCursorScreenPos();

                for (int i = 0; i < list.Count; i++)
                {
                    var sheet = list[i];
                    int col = i % columns;
                    int row = i / columns;

                    float x = origin.X + col * (cardW + spacing);
                    float y = origin.Y + row * (cardH + spacing);
                    Vector2 cardPos = new Vector2(x, y);
                    Vector2 cardEnd = cardPos + new Vector2(cardW, cardH);
                    bool hovered = ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(cardPos, cardEnd);

                    // Card background
                    SysUI.DrawCard(drawList, cardPos, cardEnd, hovered, false);

                    // Status accent strip at top
                    int status = Math.Clamp(sheet.status, 0, StatusNames.Length - 1);
                    var statusCol = SysUI.StatusColor(status);
                    drawList.AddRectFilled(cardPos, cardPos + new Vector2(cardW, SysUI.S(4f)), SysUI.U(statusCol), SysUI.S(8f), ImDrawFlags.RoundCornersTop);

                    // Avatar (centered, circular area)
                    float avatarSize = SysUI.S(64f);
                    Vector2 avatarCenter = cardPos + new Vector2(cardW / 2, SysUI.S(22f) + avatarSize / 2);
                    if (sheet.profileAvatar != null && sheet.profileAvatar.Handle != IntPtr.Zero)
                    {
                        Vector2 imgMin = avatarCenter - new Vector2(avatarSize / 2, avatarSize / 2);
                        Vector2 imgMax = avatarCenter + new Vector2(avatarSize / 2, avatarSize / 2);
                        drawList.AddImageRounded(sheet.profileAvatar.Handle, imgMin, imgMax,
                            new Vector2(0, 0), new Vector2(1, 1), 0xFFFFFFFF, avatarSize / 2);
                        drawList.AddCircle(avatarCenter, avatarSize / 2 + SysUI.S(1f), SysUI.U(SysUI.Fade(statusCol, 0.8f)), 32, SysUI.S(2f));
                    }
                    else
                    {
                        drawList.AddCircleFilled(avatarCenter, avatarSize / 2, SysUI.U(RsTheme.BgTertiary), 32);
                        drawList.AddCircle(avatarCenter, avatarSize / 2, RsTheme.U.Border, 32, SysUI.S(1.5f));
                        string initial = string.IsNullOrEmpty(sheet.characterName) ? "?" : sheet.characterName.Substring(0, 1).ToUpperInvariant();
                        var iSize = ImGui.CalcTextSize(initial);
                        drawList.AddText(avatarCenter - iSize / 2, RsTheme.U.TextMuted, initial);
                    }

                    float textY = avatarCenter.Y + avatarSize / 2 + SysUI.S(10f);
                    float maxTextW = cardW - SysUI.S(16f);
                    float lineH = ImGui.GetTextLineHeight();

                    // Name
                    string displayName = Truncate(!string.IsNullOrEmpty(sheet.profileName) ? sheet.profileName : sheet.characterName, maxTextW);
                    var nameSize = ImGui.CalcTextSize(displayName);
                    drawList.AddText(new Vector2(cardPos.X + (cardW - nameSize.X) / 2, textY), RsTheme.U.TextPrimary, displayName);

                    // Class name
                    string className = "No Class";
                    if (sheet.classId >= 0)
                    {
                        var cls = system.SkillClasses.FirstOrDefault(c => c.id == sheet.classId);
                        if (cls != null) className = cls.name;
                    }
                    className = Truncate(className, maxTextW);
                    var classSize = ImGui.CalcTextSize(className);
                    drawList.AddText(new Vector2(cardPos.X + (cardW - classSize.X) / 2, textY + lineH + SysUI.S(4f)),
                        RsTheme.U.AccentPrimary, className);

                    // Level
                    string lvlText = $"Lv. {sheet.level}";
                    var lvlSize = ImGui.CalcTextSize(lvlText);
                    drawList.AddText(new Vector2(cardPos.X + (cardW - lvlSize.X) / 2, textY + (lineH + SysUI.S(4f)) * 2f),
                        RsTheme.U.TextMuted, lvlText);

                    // Status chip (bottom)
                    string st = StatusNames[status];
                    var stSize = ImGui.CalcTextSize(st);
                    var chipPad = SysUI.S(8f, 2f);
                    var chipSize = stSize + chipPad * 2f;
                    var chipMin = new Vector2(cardPos.X + (cardW - chipSize.X) / 2, cardEnd.Y - chipSize.Y - SysUI.S(10f));
                    drawList.AddRectFilled(chipMin, chipMin + chipSize, SysUI.U(SysUI.Fade(statusCol, 0.16f)), chipSize.Y * 0.5f);
                    drawList.AddText(chipMin + chipPad, SysUI.U(statusCol), st);

                    // Invisible button for click
                    ImGui.SetCursorScreenPos(cardPos);
                    if (ImGui.InvisibleButton($"##card_{sheet.id}", new Vector2(cardW, cardH)))
                    {
                        viewingSheet = sheet;
                        detailTreeIndex = 0;
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                // Reserve scroll space
                ImGui.SetCursorScreenPos(origin);
                ImGui.Dummy(new Vector2(columns * (cardW + spacing) - spacing, rows * (cardH + spacing)));
            }
        }

        // Sheet Detail View
        /// Get stat value from a sheet, trying both stat ID and sort index as keys.
        public static int GetSheetStatValue(SystemData system, CharacterSheetData sheet, int statId)
        {
            // Try by stat ID first (new format)
            if (sheet.statValues.ContainsKey(statId))
                return sheet.statValues[statId];

            // Fallback: try by sort index (old format - key matches position in StatsData)
            int idx = 0;
            foreach (var kvp in system.StatsData)
            {
                if (kvp.Value.id == statId)
                {
                    if (sheet.statValues.ContainsKey(idx))
                        return sheet.statValues[idx];
                    if (sheet.statValues.ContainsKey(kvp.Key))
                        return sheet.statValues[kvp.Key];
                    break;
                }
                idx++;
            }
            return 0;
        }

        /// Get all stat values as a list of (StatData, value) pairs, handling both key formats.
        private static List<(StatData stat, int value)> GetSheetStats(SystemData system, CharacterSheetData sheet)
        {
            var result = new List<(StatData, int)>();
            foreach (var kvp in system.StatsData)
            {
                int val = GetSheetStatValue(system, sheet, kvp.Value.id);
                result.Add((kvp.Value, val));
            }
            return result;
        }

        private static void DrawSheetDetail(SystemData system, bool isOwner)
        {
            if (RsElements.Button("< Back to Roster##backRoster", RsElements.ButtonVariant.Ghost))
            {
                viewingSheet = null;
                return;
            }

            SysUI.Gap(8f);

            var sheet = viewingSheet;
            int status = Math.Clamp(sheet.status, 0, StatusNames.Length - 1);

            // Identity card
            bool idOpen = RsElements.BeginPanel("sys_sheet_identity", null, fitContentsY: true);
            try
            {
                if (idOpen)
                {
                    if (sheet.profileAvatar != null && sheet.profileAvatar.Handle != IntPtr.Zero)
                    {
                        float avSize = SysUI.S(80f);
                        float centeredX = (RsElements.AvailContentWidth() - avSize) / 2;
                        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + centeredX);
                        AbsoluteRP.Helpers.Anim.DrawCircleAvatarInline(sheet.profileAvatar.Handle, avSize, RsTheme.AccentPrimary, borderThickness: 2.5f);
                        SysUI.Gap(4f);
                    }

                    string displayName = !string.IsNullOrEmpty(sheet.profileName) ? sheet.profileName : sheet.characterName;
                    CenterText(displayName, RsTheme.TextPrimary);
                    CenterText(sheet.characterWorld, RsTheme.TextMuted);

                    SysUI.Gap(4f);

                    // Class name + icon
                    if (sheet.classId >= 0)
                    {
                        var cls = system.SkillClasses.FirstOrDefault(c => c.id == sheet.classId);
                        if (cls != null)
                        {
                            float iconSz = SysUI.S(22f);
                            bool hasIcon = cls.iconTexture != null && cls.iconTexture.Handle != IntPtr.Zero;
                            float rowWidth = ImGui.CalcTextSize(cls.name).X + (hasIcon ? iconSz + SysUI.S(6f) : 0f);

                            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (RsElements.AvailContentWidth() - rowWidth) / 2));
                            if (hasIcon)
                            {
                                ImGui.Image(cls.iconTexture.Handle, new Vector2(iconSz, iconSz));
                                ImGui.SameLine(0f, SysUI.S(6f));
                                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (iconSz - ImGui.GetTextLineHeight()) * 0.5f);
                            }
                            SysUI.Accent(cls.name);
                        }
                    }

                    // Level + status
                    string lvlText = $"Level {sheet.level}";
                    string stText = StatusNames[status];
                    var chipPad = SysUI.S(8f, 2f);
                    float rowW = ImGui.CalcTextSize(lvlText).X + SysUI.S(10f) + ImGui.CalcTextSize(stText).X + chipPad.X * 2f;
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (RsElements.AvailContentWidth() - rowW) / 2));
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + chipPad.Y);
                    SysUI.Secondary(lvlText);
                    ImGui.SameLine(0f, SysUI.S(10f));
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() - chipPad.Y);
                    SysUI.Badge(stText, SysUI.StatusColor(status));

                    SysUI.Gap(8f);

                    // View Profile button
                    float btnWidth = RsElements.MeasureButtonWidth("View Profile");
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (RsElements.AvailContentWidth() - btnWidth) / 2));
                    if (RsElements.Button("View Profile##viewProf", RsElements.ButtonVariant.Secondary))
                    {
                        Plugin.plugin.OpenTargetWindow();
                        TargetProfileWindow.characterName = sheet.characterName;
                        TargetProfileWindow.characterWorld = sheet.characterWorld;
                        TargetProfileWindow.RequestingProfile = true;
                        TargetProfileWindow.ResetAllData();
                        AbsoluteRP.Network.Profiles_DS.FetchProfile(Plugin.character, false, -1,
                            sheet.characterName, sheet.characterWorld, -1);
                    }
                }
            }
            finally { RsElements.EndPanel(); }

            // Owner controls: level + bonus skill points
            if (isOwner)
            {
                SysUI.Gap(10f);
                bool mOpen = RsElements.BeginPanel("sys_sheet_manage", "Manage Sheet", fitContentsY: true);
                try
                {
                    if (mOpen)
                        DrawOwnerControls(system, sheet, status);
                }
                finally { RsElements.EndPanel(); }
            }

            SysUI.Gap(12f);

            // Tabbed Stats / Skills view
            if (detailTab < 0 || detailTab > 2) detailTab = 0;
            RsElements.NavigationMenu("sys_sheet_tabs", ref detailTab, DetailNavItems);
            SysUI.Gap(10f);

            bool tOpen = RsElements.BeginPanel("sys_sheet_tab_body", null, fitContentsY: true);
            try
            {
                if (tOpen)
                {
                    switch (detailTab)
                    {
                        case 0:
                            if (sheet.statValues.Count > 0 && system.StatsData.Count >= 2)
                            {
                                // Radar chart
                                DrawSheetRadarChart(system, sheet);
                                SysUI.Gap(6f);
                            }

                            // Stat values list
                            var statPairs = GetSheetStats(system, sheet);
                            if (statPairs.Count == 0)
                                SysUI.Muted("This system has no stats.");
                            float rowW = RsElements.AvailContentWidth();
                            foreach (var (stat, val) in statPairs)
                            {
                                var rowStart = ImGui.GetCursorPosX();
                                SysUI.Swatch(stat.color, 4f);
                                ImGui.SameLine(0f, SysUI.S(8f));
                                SysUI.Primary(stat.name);
                                string v = val.ToString();
                                ImGui.SameLine(rowStart + rowW - ImGui.CalcTextSize(v).X);
                                SysUI.Accent(v);
                            }
                            break;

                        case 1:
                            if (sheet.classId >= 0)
                            {
                                var cls = system.SkillClasses.FirstOrDefault(c => c.id == sheet.classId);
                                if (cls != null)
                                    DrawSheetSkillTree(system, cls, sheet);
                                else
                                    SysUI.Muted("Class not found.");
                            }
                            else
                            {
                                SysUI.Muted("No class assigned.");
                            }
                            break;

                        case 2:
                            DrawSheetResources(system, sheet, isOwner);
                            break;
                    }
                }
            }
            finally { RsElements.EndPanel(); }
        }

        private static void CenterText(string text, Vector4 color)
        {
            var w = ImGui.CalcTextSize(text ?? string.Empty).X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (RsElements.AvailContentWidth() - w) / 2));
            SysUI.Text(text, color);
        }

        private static void DrawOwnerControls(SystemData system, CharacterSheetData sheet, int status)
        {
            float gap = SysUI.S(16f);

            SysUI.SectionLabel("Progression");
            using (SysUI.Field("Level"))
            {
                int lvl = sheet.level;
                if (SysUI.InputInt("sheetLvl", ref lvl))
                    sheet.level = Math.Max(1, lvl);
            }
            ImGui.SameLine(0f, gap);
            using (SysUI.Field("Bonus Skill Points"))
            {
                int bsp = sheet.bonusSkillPoints;
                if (SysUI.InputInt("sheetBSP", ref bsp))
                    sheet.bonusSkillPoints = Math.Max(0, bsp);
            }
            ImGui.SameLine(0f, gap);
            using (SysUI.Field("Bonus Stat Points"))
            {
                int bstp = sheet.bonusStatPoints;
                if (SysUI.InputInt("sheetBSTP", ref bstp))
                    sheet.bonusStatPoints = Math.Max(0, bstp);
            }
            ImGui.SameLine(0f, gap);
            using (SysUI.Field(" "))
            {
                if (RsElements.Button("Save##saveLvlPts", RsElements.ButtonVariant.Primary))
                {
                    if (Plugin.character != null)
                        AbsoluteRP.Network.Systems_DS.UpdateSheetLevelPoints(Plugin.character, sheet.id, sheet.level, sheet.bonusSkillPoints, sheet.bonusStatPoints);
                }
            }

            SysUI.Divider();

            // Status actions
            SysUI.SectionLabel("Status");
            SysUI.Badge(StatusNames[status], SysUI.StatusColor(status));
            SysUI.Gap(6f);

            if (sheet.status == 0)
            {
                if (RsElements.Button("Approve", RsElements.ButtonVariant.Success))
                {
                    if (Plugin.character != null)
                        AbsoluteRP.Network.Systems_DS.RespondToSheet(Plugin.character, sheet.id, 1, "");
                }
                ImGui.SameLine(0f, SysUI.S(8f));
                if (RsElements.Button("Decline", RsElements.ButtonVariant.Danger))
                {
                    if (Plugin.character != null)
                        AbsoluteRP.Network.Systems_DS.RespondToSheet(Plugin.character, sheet.id, 2, "");
                }
                ImGui.SameLine(0f, SysUI.S(8f));
                if (RsElements.Button("Revision", RsElements.ButtonVariant.Secondary))
                {
                    revisionSheetId = sheet.id;
                    revisionReason = "";
                    showRevisionPopup = true;
                    ImGui.OpenPopup("##RevisionPopup");
                }
                ImGui.SameLine(0f, SysUI.S(8f));
            }
            else if (sheet.status == 1)
            {
                if (RsElements.Button("Revoke", RsElements.ButtonVariant.Danger))
                {
                    showRevokeConfirm = true;
                    revokeSheetId = sheet.id;
                    ImGui.OpenPopup("##RevokeConfirm");
                }
                ImGui.SameLine(0f, SysUI.S(8f));
            }

            // Ban button
            if (RsElements.Button("Ban from System##banSheet", RsElements.ButtonVariant.Danger))
            {
                showBanConfirm = true;
                banConfirmSystemId = system.id;
                banConfirmName = sheet.characterName;
                banConfirmWorld = sheet.characterWorld;
                ImGui.OpenPopup("##BanConfirm");
            }

            // Revoke confirmation popup
            if (SysUI.BeginModal("##RevokeConfirm", ref showRevokeConfirm))
            {
                SysUI.Primary("Are you sure you want to revoke this user's access?");
                SysUI.Gap(4f);
                SysUI.Text("This will decline their character sheet.", RsTheme.AccentDanger);
                SysUI.Gap(8f);

                bool ctrlHeld = ImGui.GetIO().KeyCtrl;
                if (SysUI.Button("Confirm Revoke##confirmRevoke", RsElements.ButtonVariant.Danger, ctrlHeld))
                {
                    if (Plugin.character != null && revokeSheetId > 0)
                        AbsoluteRP.Network.Systems_DS.RespondToSheet(Plugin.character, revokeSheetId, 2, "");
                    showRevokeConfirm = false;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (RsElements.Button("Cancel##cancelRevoke", RsElements.ButtonVariant.Secondary))
                {
                    showRevokeConfirm = false;
                    ImGui.CloseCurrentPopup();
                }
                if (!ctrlHeld)
                    SysUI.Muted("Hold CTRL to enable");
                SysUI.EndModal();
            }

            // Ban confirmation popup
            if (SysUI.BeginModal("##BanConfirm", ref showBanConfirm))
            {
                SysUI.Primary($"Are you sure you want to ban {banConfirmName}?");
                SysUI.Gap(4f);
                SysUI.Text("This will ban them from the system and decline their sheet.", RsTheme.AccentDanger);
                SysUI.Text("They will not be able to submit new sheets until unbanned.", RsTheme.AccentDanger);
                SysUI.Gap(8f);

                bool ctrlHeld2 = ImGui.GetIO().KeyCtrl;
                if (SysUI.Button("Confirm Ban##confirmBan", RsElements.ButtonVariant.Danger, ctrlHeld2))
                {
                    if (Plugin.character != null && banConfirmSystemId > 0)
                        AbsoluteRP.Network.Systems_DS.BanFromSystem(Plugin.character, banConfirmSystemId,
                            banConfirmName, banConfirmWorld, "Banned by system owner");
                    showBanConfirm = false;
                    viewingSheet = null;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (RsElements.Button("Cancel##cancelBan", RsElements.ButtonVariant.Secondary))
                {
                    showBanConfirm = false;
                    ImGui.CloseCurrentPopup();
                }
                if (!ctrlHeld2)
                    SysUI.Muted("Hold CTRL to enable");
                SysUI.EndModal();
            }

            DrawRevisionPopup();
        }

        // Resources for Sheet Detail
        private static void DrawSheetResources(SystemData system, CharacterSheetData sheet, bool isOwner)
        {
            bool canEdit = isOwner || !system.restrictResourceModification;

            // Health
            var combat = system.CombatConfig;
            if (combat.healthEnabled)
            {
                int hp = sheet.currentHealth;
                int maxHp = combat.healthMax;

                // Calculate bonus from linked stat
                if (combat.healthLinkedStatId >= 0)
                {
                    int statVal = GetSheetStatValue(system, sheet, combat.healthLinkedStatId);
                    maxHp = combat.healthBase + (int)(statVal * combat.healthStatMultiplier);
                    if (combat.healthMax > 0 && maxHp > combat.healthMax) maxHp = combat.healthMax;
                }

                // HP bar
                SysUI.SectionLabel("Health");
                float fill = maxHp > 0 ? Math.Clamp((float)hp / maxHp, 0f, 1f) : 0f;
                SysUI.Bar(fill, RsTheme.AccentDanger, $"{hp} / {maxHp}", 20f);
                SysUI.Gap(4f);

                // Edit controls
                if (canEdit)
                {
                    if (SysUI.InputInt("sheetHP", ref hp))
                        sheet.currentHealth = Math.Clamp(hp, 0, maxHp);
                    ImGui.SameLine(0f, SysUI.S(8f));
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + SysUI.S(8f));
                    SysUI.Muted($"/ {maxHp}");
                }
                SysUI.Gap(8f);
            }

            // Resources
            if (system.Resources.Count > 0)
            {
                SysUI.SectionLabel("Resources");

                foreach (var res in system.Resources)
                {
                    int resId = res.id;
                    int currentVal = sheet.resourceValues.ContainsKey(resId) ? sheet.resourceValues[resId] : res.baseValue;
                    int maxVal = res.maxValue;

                    // Calculate bonus from linked stat
                    if (res.linkedStatId >= 0)
                    {
                        int statVal = GetSheetStatValue(system, sheet, res.linkedStatId);
                        maxVal = res.baseValue + (int)(statVal * res.statMultiplier);
                        if (res.maxValue > 0 && maxVal > res.maxValue) maxVal = res.maxValue;
                    }

                    ImGui.PushID($"res_{resId}");
                    try
                    {
                        // Resource bar
                        SysUI.Secondary(res.name);
                        float rFill = maxVal > 0 ? Math.Clamp((float)currentVal / maxVal, 0f, 1f) : 0f;
                        SysUI.Bar(rFill, res.color, $"{currentVal} / {maxVal}", 18f);
                        SysUI.Gap(4f);

                        // Edit controls
                        if (canEdit)
                        {
                            int val = currentVal;
                            if (SysUI.InputInt("resVal", ref val))
                            {
                                val = Math.Clamp(val, 0, maxVal);
                                sheet.resourceValues[resId] = val;
                            }
                            ImGui.SameLine(0f, SysUI.S(8f));
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + SysUI.S(8f));
                            SysUI.Muted($"/ {maxVal}");
                        }
                    }
                    finally { ImGui.PopID(); }
                    SysUI.Gap(8f);
                }
            }

            if (!combat.healthEnabled && system.Resources.Count == 0)
            {
                SysUI.Muted("No resources configured for this system.");
                return;
            }

            // Save button
            if (canEdit)
            {
                SysUI.Gap(4f);
                if (RsElements.Button("Save Resources##saveRes", RsElements.ButtonVariant.Primary))
                {
                    if (Plugin.character != null)
                        AbsoluteRP.Network.Systems_DS.UpdateSheetResources(Plugin.character, sheet.id, sheet.currentHealth, sheet.resourceValues);
                }
            }
        }

        // Radar Chart for Sheet Stats
        private static void DrawSheetRadarChart(SystemData system, CharacterSheetData sheet)
        {
            int count = system.StatsData.Count;
            if (count < 2) return;

            float availWidth = RsElements.AvailContentWidth();
            float chartRadius = Math.Min(availWidth * 0.4f, SysUI.S(120f));
            Vector2 cursorStart = ImGui.GetCursorScreenPos();
            Vector2 center = cursorStart + new Vector2(availWidth / 2, chartRadius + SysUI.S(18f));
            var drawList = ImGui.GetWindowDrawList();
            float angleStep = 2f * MathF.PI / count;
            float startAngle = -MathF.PI / 2f;

            // Guide rings
            uint ringColor = SysUI.U(SysUI.Fade(RsTheme.Border, 0.7f));
            for (int ring = 1; ring <= 4; ring++)
            {
                float r = chartRadius * ring / 4f;
                var ringPoints = new Vector2[count];
                for (int i = 0; i < count; i++)
                {
                    float angle = startAngle + i * angleStep;
                    ringPoints[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * r;
                }
                drawList.AddPolyline(ref ringPoints[0], ringPoints.Length, ringColor, ImDrawFlags.Closed, 1f);
            }

            // Axis lines + labels
            int idx = 0;
            int budget = system.basePointsAvailable > 0 ? system.basePointsAvailable : 1;
            var dataPoints = new Vector2[count];

            foreach (var kvp in system.StatsData)
            {
                float angle = startAngle + idx * angleStep;
                Vector2 axisEnd = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * chartRadius;
                drawList.AddLine(center, axisEnd, ringColor, 1f);

                // Label
                Vector2 labelPos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (chartRadius + SysUI.S(12f));
                var textSize = ImGui.CalcTextSize(kvp.Value.name);
                uint labelColor = ImGui.ColorConvertFloat4ToU32(kvp.Value.color);
                drawList.AddText(labelPos - textSize / 2, labelColor, kvp.Value.name);

                // Data point
                int val = GetSheetStatValue(system, sheet, kvp.Value.id);
                float ratio = MathF.Sqrt(Math.Clamp((float)val / budget, 0f, 1f));
                float r2 = Math.Max(ratio, 0.05f) * chartRadius;
                dataPoints[idx] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * r2;
                idx++;
            }

            // Filled polygon
            uint fillColor = SysUI.U(SysUI.Fade(RsTheme.AccentPrimary, 0.2f));
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                drawList.AddTriangleFilled(center, dataPoints[i], dataPoints[next], fillColor);
            }

            // Border
            drawList.AddPolyline(ref dataPoints[0], dataPoints.Length, SysUI.U(SysUI.Fade(RsTheme.AccentPrimary, 0.8f)), ImDrawFlags.Closed, SysUI.S(2.5f));

            // Dots
            uint dotColor = RsTheme.U.AccentPrimary;
            for (int i = 0; i < count; i++)
                drawList.AddCircleFilled(dataPoints[i], SysUI.S(3f), dotColor);

            ImGui.SetCursorScreenPos(cursorStart);
            ImGui.Dummy(new Vector2(availWidth, chartRadius * 2 + SysUI.S(40f)));
        }

        // Skill Tree for Sheet Detail
        private static void DrawSheetSkillTree(SystemData system, SkillClassData cls, CharacterSheetData sheet)
        {
            int classId = cls.id;

            // Tree tabs
            if (cls.SkillTrees.Count > 0)
            {
                if (detailTreeIndex >= cls.SkillTrees.Count)
                    detailTreeIndex = 0;

                var treeItems = new List<RsElements.NavItem>(cls.SkillTrees.Count);
                for (int t = 0; t < cls.SkillTrees.Count; t++)
                    treeItems.Add(new RsElements.NavItem(FontAwesomeIcon.ProjectDiagram,
                        string.IsNullOrEmpty(cls.SkillTrees[t].name) ? $"Tree {t + 1}" : cls.SkillTrees[t].name));
                RsElements.NavigationMenu($"sys_detail_tree_nav_{cls.id}", ref detailTreeIndex, treeItems);
                SysUI.Gap(8f);
            }

            float gridWidth = RsElements.AvailContentWidth();
            float cellSize = Math.Min((gridWidth - SysUI.S(20f)) / GridCols, SysUI.S(56f));
            float octRadius = cellSize * 0.35f;

            var drawList = ImGui.GetWindowDrawList();
            Vector2 origin = ImGui.GetCursorScreenPos();
            uint accent = RsTheme.U.AccentPrimary;
            uint accentMuted = SysUI.U(SysUI.Fade(RsTheme.AccentPrimary, 0.45f));

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
                    drawList.AddLine(from, to, accentMuted, SysUI.S(2f));
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
                        // Learned skills are assumed maxed tier (since you must max to unlock children)
                        int currentTier = isLearned ? skill.maxTiers : 0;
                        float alpha = isLearned ? 1.0f : 0.3f;

                        if (skill.iconTexture != null && skill.iconTexture.Handle != IntPtr.Zero)
                        {
                            var tint = new Vector4(alpha, alpha, alpha, 1f);
                            Skills.Skills.DrawOctagonImage(drawList, skill.iconTexture, center, octRadius, octPoints, ImGui.ColorConvertFloat4ToU32(tint));
                        }
                        else
                        {
                            uint fillColor = isLearned ? accent : SysUI.U(SysUI.Fade(RsTheme.BgTertiary, 0.9f));
                            Skills.Skills.DrawFilledOctagon(drawList, octPoints, fillColor);
                            string label = skill.name.Length > 5 ? skill.name[..5] + ".." : skill.name;
                            var textSize = ImGui.CalcTextSize(label);
                            uint textCol = isLearned ? RsTheme.U.TextPrimary : RsTheme.U.TextMuted;
                            drawList.AddText(center - textSize / 2, textCol, label);
                        }

                        uint borderColor = isLearned ? accent : SysUI.U(SysUI.Fade(RsTheme.Border, 0.8f));
                        drawList.AddPolyline(ref octPoints[0], octPoints.Length, borderColor, ImDrawFlags.Closed, isLearned ? SysUI.S(2.5f) : SysUI.S(1f));

                        // Tier counter (bottom-right) for multi-tier skills
                        if (skill.maxTiers > 1)
                        {
                            string tierLabel = $"{currentTier}/{skill.maxTiers}";
                            var tierSize = ImGui.CalcTextSize(tierLabel);
                            Vector2 tierPos = center + new Vector2(octRadius * 0.5f - tierSize.X / 2, octRadius * 0.55f);
                            drawList.AddText(tierPos + new Vector2(1, 1), 0xFF000000, tierLabel);
                            uint tierCol = isLearned ? RsTheme.U.AccentSuccess : RsTheme.U.TextMuted;
                            drawList.AddText(tierPos, tierCol, tierLabel);
                        }

                        // Tooltip
                        ImGui.SetCursorScreenPos(center - new Vector2(octRadius, octRadius));
                        ImGui.InvisibleButton($"##dt_{x}_{y}", new Vector2(octRadius * 2, octRadius * 2));
                        if (ImGui.IsItemHovered())
                        {
                            SysUI.BeginTooltip();
                            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20f);
                            SysUI.Text(skill.name, isLearned ? RsTheme.AccentPrimary : RsTheme.TextMuted);
                            if (skill.maxTiers > 1)
                                SysUI.Secondary($"Tier: {currentTier} / {skill.maxTiers}");
                            if (!string.IsNullOrEmpty(skill.description))
                                ImGui.TextWrapped(skill.description);
                            if (skill.cooldownTurns > 0)
                                SysUI.Secondary($"Cooldown: {skill.cooldownTurns} turns");
                            if (skill.resourceCost > 0)
                                SysUI.Secondary($"Cost: {skill.resourceCost}");
                            SysUI.Text(isLearned ? (skill.maxTiers > 1 ? "Maxed" : "Learned") : "Not learned",
                                isLearned ? RsTheme.AccentSuccess : RsTheme.TextMuted);
                            ImGui.PopTextWrapPos();
                            SysUI.EndTooltip();
                        }
                    }
                    else
                    {
                        uint outlineColor = SysUI.U(SysUI.Fade(RsTheme.Border, 0.35f));
                        drawList.AddPolyline(ref octPoints[0], octPoints.Length, outlineColor, ImDrawFlags.Closed, 1f);
                    }
                }
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(GridCols * cellSize, GridRows * cellSize + SysUI.S(10f)));
        }

        private static void DrawRevisionPopup()
        {
            if (!showRevisionPopup) return;

            ImGui.SetNextWindowSize(new Vector2(SysUI.S(360f), 0f), ImGuiCond.FirstUseEver);
            if (SysUI.BeginPopup("##RevisionPopup"))
            {
                SysUI.SectionLabel("Reason for revision");
                RsElements.InputTextArea("revisionReason", ref revisionReason, 500, "What should they change?", new Vector2(SysUI.S(320f), SysUI.S(90f)));
                SysUI.Gap(6f);
                if (RsElements.Button("Send##sendRevision", RsElements.ButtonVariant.Primary))
                {
                    if (Plugin.character != null && revisionSheetId > 0)
                    {
                        AbsoluteRP.Network.Systems_DS.RespondToSheet(Plugin.character, revisionSheetId, 3, revisionReason);
                        showRevisionPopup = false;
                        ImGui.CloseCurrentPopup();
                    }
                }
                ImGui.SameLine();
                if (RsElements.Button("Cancel##cancelRevision", RsElements.ButtonVariant.Secondary))
                {
                    showRevisionPopup = false;
                    ImGui.CloseCurrentPopup();
                }
                SysUI.EndPopup();
            }
        }

        // Callbacks
        /// Deduplicate sheets: keep only the latest entry (highest id) per character name+world.
        private static List<CharacterSheetData> DeduplicateSheets(List<CharacterSheetData> input)
        {
            var seen = new Dictionary<string, CharacterSheetData>();
            foreach (var sheet in input)
            {
                string key = $"{sheet.characterName}@{sheet.characterWorld}";
                if (!seen.ContainsKey(key) || sheet.id > seen[key].id)
                    seen[key] = sheet;
            }
            return seen.Values.ToList();
        }

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

        // Bans Tab
        private static void DrawBansTab(SystemData system)
        {
            if (!fetchedBans && Plugin.character != null && system.id > 0)
            {
                fetchedBans = true;
                AbsoluteRP.Network.Systems_DS.FetchSystemBans(Plugin.character, system.id);
            }

            // Add ban form
            bool formOpen = RsElements.BeginPanel("sys_ban_form", "Ban a Player", fitContentsY: true);
            try
            {
                if (formOpen)
                {
                    float gap = SysUI.S(8f);
                    float banW = RsElements.MeasureButtonWidth("Ban");
                    float avail = RsElements.AvailContentWidth() - banW - gap * 3f;
                    float nameW = Math.Max(SysUI.S(100f), avail * 0.35f) / RsTheme.Scale;
                    float worldW = Math.Max(SysUI.S(80f), avail * 0.25f) / RsTheme.Scale;
                    float reasonW = Math.Max(SysUI.S(100f), avail * 0.40f) / RsTheme.Scale;

                    RsElements.InputText("banName", ref banCharName, 64, "Character Name", nameW);
                    ImGui.SameLine(0f, gap);
                    RsElements.InputText("banWorld", ref banCharWorld, 64, "World", worldW);
                    ImGui.SameLine(0f, gap);
                    RsElements.InputText("banReason", ref banReason, 200, "Reason (optional)", reasonW);
                    ImGui.SameLine(0f, gap);
                    if (RsElements.Button("Ban##banUser", RsElements.ButtonVariant.Danger))
                    {
                        if (!string.IsNullOrWhiteSpace(banCharName) && !string.IsNullOrWhiteSpace(banCharWorld) && Plugin.character != null)
                        {
                            AbsoluteRP.Network.Systems_DS.BanFromSystem(Plugin.character, system.id, banCharName.Trim(), banCharWorld.Trim(), banReason.Trim());
                            banCharName = "";
                            banCharWorld = "";
                            banReason = "";
                        }
                    }
                }
            }
            finally { RsElements.EndPanel(); }

            SysUI.Gap(10f);

            // Ban list
            bool listOpen = RsElements.BeginPanel("sys_ban_list", $"Banned Players ({bans.Count})", fitContentsY: true);
            try
            {
                if (listOpen)
                {
                    if (bans.Count == 0)
                    {
                        SysUI.Muted("No bans.");
                    }

                    float rowW = RsElements.AvailContentWidth();
                    float unbanW = RsElements.MeasureButtonWidth("Unban");
                    float rowH = ImGui.GetTextLineHeight() + SysUI.S(18f);
                    for (int i = 0; i < bans.Count; i++)
                    {
                        var ban = bans[i];
                        ImGui.PushID($"ban_{ban.id}");
                        try
                        {
                            if (i > 0) SysUI.Divider();
                            float rowX = ImGui.GetCursorPosX();
                            float rowY = ImGui.GetCursorPosY();
                            ImGui.SetCursorPosY(rowY + (rowH - ImGui.GetTextLineHeight()) * 0.5f);
                            SysUI.Text(ban.name, RsTheme.AccentDanger);
                            ImGui.SameLine(0f, SysUI.S(6f));
                            SysUI.Muted($"@ {ban.world}");
                            if (!string.IsNullOrEmpty(ban.reason))
                            {
                                ImGui.SameLine(0f, SysUI.S(10f));
                                SysUI.Secondary($"— {ban.reason}");
                            }
                            ImGui.SameLine(rowX + rowW - unbanW);
                            ImGui.SetCursorPosY(rowY);
                            if (RsElements.Button("Unban##unban", RsElements.ButtonVariant.Secondary))
                            {
                                if (Plugin.character != null)
                                    AbsoluteRP.Network.Systems_DS.UnbanFromSystem(Plugin.character, system.id, ban.id);
                            }
                        }
                        finally { ImGui.PopID(); }
                    }
                }
            }
            finally { RsElements.EndPanel(); }
        }
    }
}
