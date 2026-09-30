using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI;
using AbsoluteRP.Windows.Listings;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AbsoluteRP.RsUI.Pages;
using AbsoluteRP.Network;

namespace AbsoluteRP.Windows.Systems.ViewSystems
{
    // Player-facing view of an RP system - join wizard (pick class, allocate stats, select skills), skill tree viewer with tier unlocking, and character sheet display.
    internal class ViewSystems
    {
        // Wizard steps: 0=System, 1=Profile, 2=Class+Skills, 3=Stats, 4=Review
        private static int wizardStep = 0;

        // System selection
        private static string importCode = "";
        public static List<SystemData> availableSystems = new List<SystemData>();
        private static int selectedSystemIndex = -1;
        public static SystemData selectedSystem = null;

        // Profile selection
        private static int selectedProfileIndex = -1;

        // Class selection
        private static int selectedClassIndex = -1;
        private static int hoveredClassIndex = -1;
        private static int previewTreeIndex = 0;

        // Skill selection (tree-based, done during class step) Maps skillId -> current tier (0 = not learned, 1+ = tiers invested)
        private static Dictionary<int, int> skillTiers = new Dictionary<int, int>();
        private static List<int> selectedSkills = new List<int>(); // kept for submission (skills with tier >= 1)
        private static int skillPointsUsed = 0;
        // Rolled creation (system setting): results of the one-time rolls.
        private static bool statsRolled = false;
        private static bool skillsRolled = false;
        private static int rolledSkillPoints = 0;
        private static readonly Random rollRng = new Random();

        private static int RollDice(SystemData sys)
        {
            int count = Math.Max(1, sys?.CombatConfig?.diceCount ?? 1);
            int sides = Math.Max(2, sys?.CombatConfig?.diceType ?? 20);
            int sum = 0;
            for (int i = 0; i < count; i++) sum += rollRng.Next(1, sides + 1);
            return sum;
        }
        private static string DiceLabel(SystemData sys)
            => $"{Math.Max(1, sys?.CombatConfig?.diceCount ?? 1)}d{Math.Max(2, sys?.CombatConfig?.diceType ?? 20)}";

        // The class's skill budget: rolled when the system says so.
        private static int SkillBudget(SystemData sys, SkillClassData cls)
            => sys != null && sys.rollForSkills ? (skillsRolled ? rolledSkillPoints : 0) : cls.initialSkillPoints;

        // Stat assignment
        private static Dictionary<int, int> statAllocations = new Dictionary<int, int>();

        // Submission result
        private static string submitMessage = "";
        private static bool submitSuccess = false;

        // Roster view toggle
        private static bool viewingRoster = false;

        // System info popup
        private static bool showSystemInfo = false;
        private static SystemData infoSystem = null;

        // Revision mode - when > 0, we're revising an existing sheet instead of creating new
        private static int revisingSheetId = 0;

        // Point assignment mode
        private static bool assigningPoints = false;
        private static CharacterSheetData assigningSheet = null;
        private static Dictionary<int, int> assignStatAllocations = new Dictionary<int, int>();
        private static Dictionary<int, int> assignSkillTiers = new Dictionary<int, int>();
        private static List<int> assignSelectedSkills = new List<int>();
        private static int assignSkillPointsUsed = 0;
        private static int assignTab = 0; // index into the visible assign tabs

        // Stat radar chart animation
        private static List<float> previousRadii = new List<float>();
        private static List<float> targetRadii = new List<float>();
        private static float morphProgress = 1f;
        private static DateTime morphStartTime = DateTime.MinValue;
        private const float MorphDuration = 0.3f;

        // Skill tree grid constants
        private const int PreviewGridCols = 5;
        private const int PreviewGridRows = 8;

        private static readonly string[] StepNames = { "System", "Profile", "Rules", "Class & Skills", "Stats", "Review" };

        public static void DrawViewSystems()
        {
            WindowOperations.LoadIconsLazy(Plugin.plugin);

            // Point assignment mode takes over the UI
            if (assigningPoints && assigningSheet != null && selectedSystem != null)
            {
                DrawPointAssignment();
                return;
            }

            SysUI.StepIndicator(StepNames, wizardStep);
            SysUI.Gap(8f);

            switch (wizardStep)
            {
                case 0: DrawSystemSelection(); break;
                case 1: DrawProfileSelection(); break;
                case 2: DrawRulesStep(); break;
                case 3: DrawClassAndSkillSelection(); break;
                case 4: DrawStatAssignment(); break;
                case 5: DrawReviewAndCreate(); break;
            }
        }

        // Wizard nav row: [Back] ........ [Next]. Returns 1 if Back clicked, 2 if Next clicked, 0 otherwise.
        private static int DrawWizardNav(string backLabel, string? nextLabel)
        {
            int result = 0;
            if (RsElements.Button(backLabel, RsElements.ButtonVariant.Ghost))
                result = 1;
            if (nextLabel != null)
            {
                ImGui.SameLine();
                SysUI.AlignRight(RsElements.MeasureButtonWidth(nextLabel));
                if (RsElements.Button(nextLabel, RsElements.ButtonVariant.Primary) && result == 0)
                    result = 2;
            }
            SysUI.Gap(8f);
            return result;
        }

        private static void CropUv(float imgW, float imgH, float slotW, float slotH, out Vector2 uv0, out Vector2 uv1)
        {
            // Center-crop UVs to avoid stretching
            float imgAspect = imgW / Math.Max(1f, imgH);
            float slotAspect = slotW / Math.Max(1f, slotH);
            uv0 = Vector2.Zero; uv1 = Vector2.One;
            if (imgAspect > slotAspect)
            {
                // Image is wider than slot - crop sides
                float visibleFrac = slotAspect / imgAspect;
                float offset = (1f - visibleFrac) / 2f;
                uv0 = new Vector2(offset, 0);
                uv1 = new Vector2(1f - offset, 1);
            }
            else
            {
                // Image is taller than slot - crop top/bottom
                float visibleFrac = imgAspect / slotAspect;
                float offset = (1f - visibleFrac) / 2f;
                uv0 = new Vector2(0, offset);
                uv1 = new Vector2(1, 1f - offset);
            }
        }

        private static string Truncate(string text, float maxW)
        {
            text ??= string.Empty;
            if (ImGui.CalcTextSize(text).X <= maxW) return text;
            for (int n = text.Length - 1; n > 0; n--)
            {
                var t = text.Substring(0, n) + "...";
                if (ImGui.CalcTextSize(t).X <= maxW) return t;
            }
            return "...";
        }

        // Step 0: System Selection
        private static void DrawSystemSelection()
        {
            bool importOpen = RsElements.BeginPanel("sys_view_import", "Import a System", fitContentsY: true);
            try
            {
                if (importOpen)
                {
                    SysUI.MutedWrapped("Got a share code from a system owner? Paste it here to add their system to your list.");
                    SysUI.Gap(4f);
                    float importW = RsElements.MeasureButtonWidth("Import");
                    float codeW = Math.Min(SysUI.S(260f), Math.Max(SysUI.S(120f), RsElements.AvailContentWidth() - importW - SysUI.S(8f))) / RsTheme.Scale;
                    RsElements.InputText("importCode", ref importCode, 32, "Share code", codeW);
                    ImGui.SameLine(0f, SysUI.S(8f));
                    if (RsElements.Button("Import##importSystem", RsElements.ButtonVariant.Primary))
                    {
                        if (!string.IsNullOrWhiteSpace(importCode) && Plugin.character != null)
                            AbsoluteRP.Network.Systems_DS.ImportSystemByCode(Plugin.character, importCode.Trim());
                    }
                }
            }
            finally { RsElements.EndPanel(); }

            SysUI.Gap(12f);

            if (availableSystems.Count == 0)
            {
                SysUI.MutedWrapped("No systems available. Enter a share code above or create one in Manage Systems.");
                return;
            }

            SysUI.SectionLabel($"Available Systems ({availableSystems.Count})");
            SysUI.Gap(2f);

            // System cards
            float btnH = ImGui.GetTextLineHeight() + SysUI.S(18f);
            float bannerHeight = SysUI.S(64f);
            float pad = SysUI.S(12f);
            float btnAreaHeight = btnH + pad;
            float cardWidth = SysUI.S(290f);
            float lineH = ImGui.GetTextLineHeight();
            float cardHeight = bannerHeight + SysUI.S(10f) + lineH + SysUI.S(4f) + lineH + SysUI.S(10f) + btnAreaHeight;
            float cardSpacing = SysUI.S(12f);
            float windowWidth = RsElements.AvailContentWidth();
            int cardCols = Math.Max(1, (int)((windowWidth + cardSpacing) / (cardWidth + cardSpacing)));

            var drawList = ImGui.GetWindowDrawList();
            Vector2 cardOrigin = ImGui.GetCursorScreenPos();
            float rounding = SysUI.S(8f);

            for (int i = 0; i < availableSystems.Count; i++)
            {
                var sys = availableSystems[i];
                int col = i % cardCols;
                int row = i / cardCols;
                bool isSelected = selectedSystem != null && selectedSystem.id == sys.id;

                Vector2 cardPos = cardOrigin + new Vector2(col * (cardWidth + cardSpacing), row * (cardHeight + cardSpacing));
                Vector2 cardEnd = cardPos + new Vector2(cardWidth, cardHeight);
                bool hovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows) && ImGui.IsMouseHoveringRect(cardPos, cardEnd);

                // Card background
                SysUI.DrawCard(drawList, cardPos, cardEnd, hovered, isSelected);

                // Banner (top of card)
                Vector2 bannerMin = cardPos + new Vector2(1f, 1f);
                Vector2 bannerMax = cardPos + new Vector2(cardWidth - 1f, bannerHeight);
                if (sys.bannerTexture != null && sys.bannerTexture.Handle != IntPtr.Zero)
                {
                    CropUv(sys.bannerTexture.Width, sys.bannerTexture.Height, cardWidth, bannerHeight, out var uv0, out var uv1);
                    drawList.AddImageRounded(sys.bannerTexture.Handle, bannerMin, bannerMax,
                        uv0, uv1, 0xFFFFFFFF, rounding, ImDrawFlags.RoundCornersTop);
                }
                else
                {
                    // Soft accent gradient when there is no banner
                    uint top = SysUI.U(SysUI.Mix(RsTheme.BgSecondary, RsTheme.AccentPrimary, 0.35f));
                    uint bottom = SysUI.U(RsTheme.BgSecondary);
                    drawList.AddRectFilled(bannerMin, bannerMax, bottom, rounding, ImDrawFlags.RoundCornersTop);
                    drawList.AddRectFilledMultiColor(bannerMin + new Vector2(0f, rounding), bannerMax, top, top, bottom, bottom);
                    drawList.AddRectFilled(bannerMin, new Vector2(bannerMax.X, bannerMin.Y + rounding), top, rounding, ImDrawFlags.RoundCornersTop);
                }
                drawList.AddLine(new Vector2(cardPos.X, cardPos.Y + bannerHeight), new Vector2(cardEnd.X, cardPos.Y + bannerHeight), RsTheme.U.Border, RsTheme.BorderThickness);

                // Logo (overlapping banner/content boundary)
                float logoSize = SysUI.S(40f);
                Vector2 logoPos = cardPos + new Vector2(pad, bannerHeight - logoSize * 0.6f);
                bool hasLogo = sys.logoTexture != null && sys.logoTexture.Handle != IntPtr.Zero;
                if (hasLogo)
                {
                    var lc = logoPos + new Vector2(logoSize / 2, logoSize / 2);
                    drawList.AddCircleFilled(lc, logoSize / 2 + SysUI.S(2f), SysUI.U(RsTheme.BgSecondary), 32);
                    drawList.AddImageRounded(sys.logoTexture.Handle, logoPos, logoPos + new Vector2(logoSize, logoSize),
                        new Vector2(0, 0), new Vector2(1, 1), 0xFFFFFFFF, logoSize / 2);
                    drawList.AddCircle(lc, logoSize / 2 + SysUI.S(1f),
                        isSelected ? RsTheme.U.AccentPrimary : RsTheme.U.BorderStrong, 32, SysUI.S(2f));
                }

                // System name
                float textStartX = hasLogo ? logoPos.X + logoSize + SysUI.S(8f) : cardPos.X + pad;
                float nameY = cardPos.Y + bannerHeight + SysUI.S(8f);
                string nameText = Truncate(sys.name, cardEnd.X - pad - textStartX);
                drawList.AddText(new Vector2(textStartX, nameY), RsTheme.U.TextPrimary, nameText);

                // Description (truncated)
                float descY = nameY + lineH + SysUI.S(6f);
                if (!string.IsNullOrEmpty(sys.description))
                {
                    string desc = sys.description.Replace('\n', ' ');
                    desc = Truncate(desc, cardWidth - pad * 2f);
                    drawList.AddText(new Vector2(cardPos.X + pad, descY), RsTheme.U.TextMuted, desc);
                }
                else
                {
                    drawList.AddText(new Vector2(cardPos.X + pad, descY), RsTheme.U.TextMuted,
                        $"{sys.StatsData.Count} stats  ·  {sys.SkillClasses.Count} classes");
                }

                // Check for unspent points
                string myCharName = Plugin.character?.characterName ?? "";
                var mySheet = Roster.Roster.sheets.FirstOrDefault(s => s.characterName == myCharName && s.systemId == sys.id && s.status == 1);
                if (mySheet == null)
                    mySheet = Roster.Roster.sheets.FirstOrDefault(s => s.characterName == myCharName && s.status == 1);
                int unspentStat = mySheet != null ? mySheet.bonusStatPoints : 0;
                int unspentSkill = mySheet != null ? mySheet.bonusSkillPoints : 0;

                // Notification dot if points available
                if (unspentStat > 0 || unspentSkill > 0)
                {
                    Vector2 dotPos = cardPos + new Vector2(cardWidth - SysUI.S(12f), SysUI.S(12f));
                    drawList.AddCircleFilled(dotPos, SysUI.S(7f), SysUI.U(SysUI.Fade(RsTheme.AccentWarning, 0.35f)));
                    drawList.AddCircleFilled(dotPos, SysUI.S(5f), RsTheme.U.AccentWarning);
                }

                // Click to select (top area only - leave room for buttons at bottom)
                ImGui.SetCursorScreenPos(cardPos);
                if (ImGui.InvisibleButton($"##sysCard_{sys.id}", new Vector2(cardWidth, cardHeight - btnAreaHeight)))
                {
                    selectedSystemIndex = i;
                    selectedSystem = sys;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

                // Buttons at bottom of card
                float btnY = cardEnd.Y - btnAreaHeight;
                float btnX = cardPos.X + pad;
                float iconBtn = btnH / RsTheme.Scale;
                ImGui.SetCursorScreenPos(new Vector2(btnX, btnY));
                ImGui.PushID($"##cardBtns_{sys.id}");
                try
                {
                    if (RsElements.Button("Create##create", RsElements.ButtonVariant.Primary))
                    {
                        selectedSystemIndex = i;
                        selectedSystem = sys;
                        selectedProfileIndex = -1;
                        profileAvatarsFetched = false;
                        if (sys.SkillClasses.Count == 0 && sys.id > 0 && Plugin.character != null)
                            AbsoluteRP.Network.Systems_DS.FetchSystem(Plugin.character, sys.id);
                        wizardStep = 1;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Create a character sheet for this system");

                    ImGui.SameLine(0f, SysUI.S(6f));
                    if (RsElements.IconButton(FontAwesomeIcon.Users, $"roster_{sys.id}", RsElements.ButtonVariant.Secondary, iconBtn))
                    {
                        selectedSystemIndex = i;
                        selectedSystem = sys;
                        SystemsWindow.showRosterPanel = !SystemsWindow.showRosterPanel;
                        if (SystemsWindow.showRosterPanel)
                        {
                            Roster.Roster.ResetForSystem();
                            if (sys.SkillClasses.Count == 0 && sys.id > 0 && Plugin.character != null)
                                AbsoluteRP.Network.Systems_DS.FetchSystem(Plugin.character, sys.id);
                        }
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Roster");

                    // Info button
                    ImGui.SameLine(0f, SysUI.S(6f));
                    if (RsElements.IconButton(FontAwesomeIcon.InfoCircle, $"info_{sys.id}", RsElements.ButtonVariant.Secondary, iconBtn))
                    {
                        infoSystem = sys;
                        showSystemInfo = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("System info");

                    // Leave button for non-owners
                    bool isOwner = SystemsWindow.systemData.Exists(s => s.id == sys.id);
                    if (!isOwner)
                    {
                        ImGui.SameLine(0f, SysUI.S(6f));
                        if (RsElements.Button("Leave##leave", RsElements.ButtonVariant.Danger))
                        {
                            // Leave on server
                            if (Plugin.character != null)
                                AbsoluteRP.Network.Systems_DS.LeaveSystem(Plugin.character, sys.id);
                            availableSystems.RemoveAll(s => s.id == sys.id);
                            if (selectedSystem != null && selectedSystem.id == sys.id)
                            {
                                selectedSystem = null;
                                selectedSystemIndex = -1;
                                SystemsWindow.showRosterPanel = false;
                            }
                        }
                    }
                    else
                    {
                        // Owner tag on the right of the button row
                        var tag = "Owner";
                        var ts = ImGui.CalcTextSize(tag);
                        var chipPad = SysUI.S(8f, 2f);
                        var chipSize = ts + chipPad * 2f;
                        var chipMin = new Vector2(cardEnd.X - pad - chipSize.X, btnY + (btnH - chipSize.Y) * 0.5f);
                        drawList.AddRectFilled(chipMin, chipMin + chipSize, SysUI.U(SysUI.Fade(RsTheme.AccentPrimary, 0.16f)), chipSize.Y * 0.5f);
                        drawList.AddText(chipMin + chipPad, RsTheme.U.AccentPrimary, tag);
                    }
                }
                finally { ImGui.PopID(); }
            }

            // Reserve space
            int cardRows = (availableSystems.Count + cardCols - 1) / cardCols;
            ImGui.SetCursorScreenPos(cardOrigin);
            ImGui.Dummy(new Vector2(Math.Min(windowWidth, cardCols * (cardWidth + cardSpacing)), cardRows * (cardHeight + cardSpacing)));

            // System info window
            if (showSystemInfo && infoSystem != null)
                DrawSystemInfoWindow();

            // Show unspent points info for selected system
            if (selectedSystem != null)
            {
                string myName = Plugin.character?.characterName ?? "";
                var myApprovedSheet = Roster.Roster.sheets.FirstOrDefault(s => s.characterName == myName && s.status == 1);
                if (myApprovedSheet != null && (myApprovedSheet.bonusStatPoints > 0 || myApprovedSheet.bonusSkillPoints > 0))
                {
                    SysUI.Gap(6f);
                    bool ptsOpen = RsElements.BeginPanel("sys_view_unspent", "Unspent Points", fitContentsY: true);
                    try
                    {
                        if (ptsOpen)
                        {
                            if (myApprovedSheet.bonusStatPoints > 0)
                                SysUI.Text($"{myApprovedSheet.bonusStatPoints} stat points available", RsTheme.AccentWarning);
                            if (myApprovedSheet.bonusSkillPoints > 0)
                                SysUI.Text($"{myApprovedSheet.bonusSkillPoints} skill points available", RsTheme.AccentWarning);
                            SysUI.Gap(6f);
                            if (RsElements.Button("Assign Points##assignPts", RsElements.ButtonVariant.Primary))
                            {
                                assigningPoints = true;
                                assigningSheet = myApprovedSheet;
                                assignTab = 0;
                                assignStatAllocations.Clear();
                                foreach (var kvp in selectedSystem.StatsData)
                                    assignStatAllocations[kvp.Key] = Roster.Roster.GetSheetStatValue(selectedSystem, myApprovedSheet, kvp.Value.id);
                                assignSelectedSkills = new List<int>(myApprovedSheet.learnedSkills);
                                assignSkillTiers.Clear();
                                foreach (var skId in myApprovedSheet.learnedSkills)
                                {
                                    var sk = selectedSystem.Skills.FirstOrDefault(s => s.id == skId);
                                    assignSkillTiers[skId] = sk != null ? sk.maxTiers : 1;
                                }
                                assignSkillPointsUsed = assignSkillTiers.Values.Sum();
                                assignPrevRadii.Clear();
                                assignTargetRadii.Clear();
                                if (selectedSystem.SkillClasses.Count == 0 && selectedSystem.id > 0 && Plugin.character != null)
                                    AbsoluteRP.Network.Systems_DS.FetchSystem(Plugin.character, selectedSystem.id);
                            }
                        }
                    }
                    finally { RsElements.EndPanel(); }
                }
            }

            // Show rules if a system is selected
            if (selectedSystem != null && !string.IsNullOrEmpty(selectedSystem.rules))
            {
                SysUI.Gap(10f);
                bool rulesOpen = RsElements.BeginPanel("sys_view_rules_preview", $"Rules — {selectedSystem.name}", fitContentsY: true);
                try
                {
                    if (rulesOpen)
                        SysUI.SecondaryWrapped(selectedSystem.rules);
                }
                finally { RsElements.EndPanel(); }
            }

            // Show user's own submissions for this system
            if (selectedSystem != null && Roster.Roster.sheets.Count > 0)
            {
                // Filter to current user's sheets (match by character name)
                string myName = Plugin.character?.characterName ?? "";
                var mySheets = Roster.Roster.sheets.Where(s => s.characterName == myName).ToList();
                if (mySheets.Count > 0)
                {
                    SysUI.Gap(10f);
                    bool subsOpen = RsElements.BeginPanel("sys_view_submissions", "Your Submissions", fitContentsY: true);
                    try
                    {
                        if (subsOpen)
                            DrawMySubmissions(mySheets);
                    }
                    finally { RsElements.EndPanel(); }
                }
            }
        }

        private static void DrawMySubmissions(List<CharacterSheetData> mySheets)
        {
            string[] statusLabels = { "Pending", "Approved", "Declined", "Revision Requested" };
            bool first = true;
            foreach (var sheet in mySheets)
            {
                if (!first) SysUI.Divider();
                first = false;

                int st = Math.Clamp(sheet.status, 0, 3);
                SysUI.Badge(statusLabels[st], SysUI.StatusColor(st));
                ImGui.SameLine(0f, SysUI.S(10f));
                string className = "No Class";
                if (sheet.classId >= 0)
                {
                    var cls = selectedSystem.SkillClasses.FirstOrDefault(c => c.id == sheet.classId);
                    if (cls != null) className = cls.name;
                }
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + SysUI.S(2f));
                SysUI.Primary($"{className} - Lv.{sheet.level}");

                if (sheet.status == 3)
                {
                    if (!string.IsNullOrEmpty(sheet.revisionReason))
                    {
                        SysUI.Gap(2f);
                        SysUI.Wrapped($"Reason: {sheet.revisionReason}", RsTheme.TextSecondary);
                    }
                    SysUI.Gap(4f);
                    if (RsElements.Button($"Revise##revise{sheet.id}", RsElements.ButtonVariant.Primary))
                    {
                        // Pre-fill wizard with existing sheet data for revision
                        revisingSheetId = sheet.id;
                        profileAvatarsFetched = false;

                        // Find matching class index
                        selectedClassIndex = -1;
                        for (int ci = 0; ci < selectedSystem.SkillClasses.Count; ci++)
                        {
                            if (selectedSystem.SkillClasses[ci].id == sheet.classId)
                            { selectedClassIndex = ci; break; }
                        }

                        // Pre-fill stats - convert from stat ID keys to sort index keys
                        statAllocations.Clear();
                        foreach (var kvp in selectedSystem.StatsData)
                        {
                            int statId = kvp.Value.id;
                            int sortKey = kvp.Key;
                            statAllocations[sortKey] = sheet.statValues.ContainsKey(statId) ? sheet.statValues[statId] : 0;
                        }

                        // Init radar chart for pre-filled values
                        int statCount = selectedSystem.StatsData.Count;
                        int budget = selectedSystem.basePointsAvailable > 0 ? selectedSystem.basePointsAvailable : 1;
                        previousRadii = new List<float>(statCount);
                        targetRadii = new List<float>(statCount);
                        foreach (var kvp in selectedSystem.StatsData)
                        {
                            int statId = kvp.Value.id;
                            int val = sheet.statValues.ContainsKey(statId) ? sheet.statValues[statId] : 0;
                            float linear = (float)val / budget;
                            float radius = linear >= 0 ? MathF.Sqrt(linear) : 0f;
                            previousRadii.Add(radius);
                            targetRadii.Add(radius);
                        }
                        morphProgress = 1f;

                        // Pre-fill skills with correct tier values
                        selectedSkills = new List<int>(sheet.learnedSkills);
                        skillTiers.Clear();
                        skillPointsUsed = 0;
                        foreach (var skId in sheet.learnedSkills)
                        {
                            // Find the skill to get its maxTiers
                            var skill = selectedSystem.Skills.FirstOrDefault(s => s.id == skId);
                            int tiers = skill != null ? skill.maxTiers : 1;
                            skillTiers[skId] = tiers; // Assume maxed since we don't store per-tier data
                            skillPointsUsed += tiers;
                        }

                        if (selectedSystem.SkillClasses.Count == 0 && selectedSystem.id > 0 && Plugin.character != null)
                            AbsoluteRP.Network.Systems_DS.FetchSystem(Plugin.character, selectedSystem.id);

                        // Skip to rules step (profile already selected)
                        wizardStep = 2;
                    }
                }
            }
        }

        private static void InfoLine(string text) => SysUI.SecondaryWrapped(text);

        private static void DrawSystemInfoWindow()
        {
            ImGui.SetNextWindowSize(new Vector2(SysUI.S(460f), SysUI.S(520f)), ImGuiCond.FirstUseEver);
            SysUI.PushWindowStyle();
            try
            {
                if (ImGui.Begin($"System Info: {infoSystem.name}##SystemInfoWindow", ref showSystemInfo))
                {
                    var sys = infoSystem;

                    // Banner
                    if (sys.bannerTexture != null && sys.bannerTexture.Handle != IntPtr.Zero)
                    {
                        float bw = ImGui.GetContentRegionAvail().X;
                        float bh = SysUI.S(90f);
                        CropUv(sys.bannerTexture.Width, sys.bannerTexture.Height, bw, bh, out var uv0, out var uv1);
                        var p = ImGui.GetCursorScreenPos();
                        ImGui.GetWindowDrawList().AddImageRounded(sys.bannerTexture.Handle, p, p + new Vector2(bw, bh), uv0, uv1, 0xFFFFFFFF, SysUI.S(8f));
                        ImGui.Dummy(new Vector2(bw, bh));
                        SysUI.Gap(6f);
                    }

                    // Header
                    SysUI.Heading(sys.name);

                    // Description
                    if (!string.IsNullOrEmpty(sys.description))
                    {
                        InfoLine(sys.description);
                        SysUI.Divider();
                    }

                    // System overview
                    SysUI.SectionLabel("Overview");
                    InfoLine("To join this system, you'll create a character sheet by choosing a profile, selecting a class, distributing stat points, and picking skills from the available talent trees. Once submitted, the system owner will review and approve your sheet.");
                    SysUI.Gap(6f);

                    // Stats Section
                    int statCount = sys.StatsData.Count;
                    if (statCount > 0)
                    {
                        SysUI.Divider();
                        SysUI.SectionLabel($"Stats ({statCount})");
                        InfoLine($"You will receive {sys.basePointsAvailable} stat points to distribute across the following attributes when creating your character:");
                        SysUI.Gap(6f);

                        foreach (var stat in sys.StatsData.Values)
                        {
                            SysUI.Swatch(stat.color, 4f);
                            ImGui.SameLine(0f, SysUI.S(8f));
                            SysUI.Accent(stat.name);
                            ImGui.Indent(SysUI.S(12f));
                            if (!string.IsNullOrEmpty(stat.description))
                                InfoLine(stat.description);
                            SysUI.Muted($"Range: {stat.baseMin} - {stat.baseMax}");
                            if (!stat.canAddPoints) SysUI.Muted("Cannot add points");
                            if (!stat.canRemovePoints) SysUI.Muted("Cannot remove points");
                            if (stat.canGoNegative) SysUI.Muted("Can go negative");
                            ImGui.Unindent(SysUI.S(12f));
                            SysUI.Gap(4f);
                        }
                    }

                    // Classes Section
                    if (sys.SkillClasses.Count > 0)
                    {
                        SysUI.Divider();
                        SysUI.SectionLabel($"Classes ({sys.SkillClasses.Count})");
                        InfoLine("When creating your character, you will choose one of the following classes. Each class defines your available skill trees, passive abilities, and starting skill points.");
                        SysUI.Gap(6f);

                        foreach (var cls in sys.SkillClasses)
                        {
                            // Class icon + name
                            if (cls.iconTexture != null && cls.iconTexture.Handle != IntPtr.Zero)
                            {
                                ImGui.Image(cls.iconTexture.Handle, new Vector2(SysUI.S(24f), SysUI.S(24f)));
                                ImGui.SameLine(0f, SysUI.S(8f));
                                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (SysUI.S(24f) - ImGui.GetTextLineHeight()) * 0.5f);
                            }
                            SysUI.Accent(cls.name);

                            ImGui.Indent(SysUI.S(12f));

                            if (!string.IsNullOrEmpty(cls.description))
                                InfoLine(cls.description);

                            // Skill points
                            if (cls.initialSkillPoints > 0)
                                SysUI.Muted($"Skill Points: {cls.initialSkillPoints}");
                            else
                                SysUI.Muted("Skill Points: Unlimited");

                            // Skill trees
                            if (cls.SkillTrees.Count > 0)
                            {
                                string treeNames = string.Join(", ", cls.SkillTrees.Select(t => t.name));
                                SysUI.MutedWrapped($"Skill Trees: {treeNames}");
                            }

                            // Skills count
                            int classSkillCount = sys.Skills.Count(s => s.classId == cls.id && s.isCastable);
                            int passiveCount = sys.Skills.Count(s => s.classId == cls.id && !s.isCastable);
                            if (classSkillCount > 0 || passiveCount > 0)
                            {
                                string skillInfo = "";
                                if (classSkillCount > 0) skillInfo += $"{classSkillCount} active skills";
                                if (passiveCount > 0) skillInfo += (skillInfo.Length > 0 ? ", " : "") + $"{passiveCount} passives";
                                SysUI.Muted(skillInfo);
                            }

                            ImGui.Unindent(SysUI.S(12f));
                            SysUI.Gap(6f);
                        }
                    }

                    // Combat Section
                    if (sys.CombatConfig.healthEnabled || sys.Resources.Count > 0)
                    {
                        SysUI.Divider();
                        SysUI.SectionLabel("Combat & Resources");
                    }

                    if (sys.CombatConfig.healthEnabled)
                    {
                        SysUI.Accent("Health");
                        ImGui.Indent(SysUI.S(12f));
                        InfoLine($"Base HP: {sys.CombatConfig.healthBase}  |  Max HP: {sys.CombatConfig.healthMax}");
                        if (sys.CombatConfig.healthLinkedStatId >= 0)
                        {
                            var linkedStat = sys.StatsData.Values.FirstOrDefault(s => s.id == sys.CombatConfig.healthLinkedStatId);
                            if (linkedStat != null)
                                InfoLine($"HP scales with {linkedStat.name} (x{sys.CombatConfig.healthStatMultiplier:F1} multiplier)");
                        }
                        if (sys.CombatConfig.healthRegenAmount > 0)
                            InfoLine($"Regenerates {sys.CombatConfig.healthRegenAmount} HP every {sys.CombatConfig.healthRegenEveryNTurns} turn(s)");
                        ImGui.Unindent(SysUI.S(12f));
                        SysUI.Gap(4f);

                        SysUI.Accent("Dice");
                        ImGui.Indent(SysUI.S(12f));
                        string diceStr = $"{sys.CombatConfig.diceCount}d{sys.CombatConfig.diceType}";
                        if (sys.CombatConfig.diceModifier > 0) diceStr += $"+{sys.CombatConfig.diceModifier}";
                        else if (sys.CombatConfig.diceModifier < 0) diceStr += $"{sys.CombatConfig.diceModifier}";
                        InfoLine($"Roll: {diceStr}  |  Turns per round: {sys.CombatConfig.turnCount}");
                        ImGui.Unindent(SysUI.S(12f));
                        SysUI.Gap(4f);
                    }

                    // Resources
                    if (sys.Resources.Count > 0)
                    {
                        SysUI.Accent("Resources");
                        ImGui.Indent(SysUI.S(12f));
                        foreach (var res in sys.Resources)
                        {
                            SysUI.Swatch(res.color, 4f);
                            ImGui.SameLine(0f, SysUI.S(8f));
                            string resInfo = $"{res.name}: {res.baseValue} / {res.maxValue}";
                            if (res.linkedStatId >= 0)
                            {
                                var linkedStat = sys.StatsData.Values.FirstOrDefault(s => s.id == res.linkedStatId);
                                if (linkedStat != null)
                                    resInfo += $"  (scales with {linkedStat.name} x{res.statMultiplier:F1})";
                            }
                            SysUI.Primary(resInfo);
                            if (res.regenAmount > 0)
                                SysUI.Muted($"    Regenerates {res.regenAmount} every {res.regenEveryNTurns} turn(s)");
                        }
                        ImGui.Unindent(SysUI.S(12f));
                        SysUI.Gap(4f);
                    }

                    // Settings Section
                    SysUI.Divider();
                    SysUI.SectionLabel("Settings");

                    if (sys.requireApproval)
                    {
                        SysUI.Badge("Approval Required", RsTheme.AccentWarning);
                        InfoLine("Your character sheet must be reviewed and approved by the system owner before you can participate. You will be notified when your sheet is approved or if revisions are requested.");
                    }
                    else
                    {
                        SysUI.Badge("Auto-Approved", RsTheme.AccentSuccess);
                        InfoLine("Character sheets are automatically approved upon submission. You can start participating immediately.");
                    }
                    SysUI.Gap(6f);

                    if (sys.restrictResourceModification)
                    {
                        SysUI.Badge("Resources Restricted", RsTheme.AccentWarning);
                        InfoLine("Only the system owner can modify HP and resource values during gameplay. Players cannot adjust their own values.");
                    }
                    else
                    {
                        SysUI.Badge("Player-Managed Resources", RsTheme.AccentPrimary);
                        InfoLine("Players can freely modify their own HP and resource values during gameplay.");
                    }

                    // Rules Section
                    if (!string.IsNullOrEmpty(sys.rules))
                    {
                        SysUI.Divider();
                        SysUI.SectionLabel("Rules");
                        InfoLine(sys.rules);
                    }

                    SysUI.Gap(10f);
                    if (RsElements.Button("Close##closeInfo", RsElements.ButtonVariant.Secondary))
                        showSystemInfo = false;
                }
                ImGui.End();
            }
            finally { SysUI.PopWindowStyle(); }
        }

        // Profile avatar fetch tracking
        private static bool profileAvatarsFetched = false;

        // Step 1: Profile Selection
        private static void DrawProfileSelection()
        {
            if (selectedSystem == null) { wizardStep = 0; return; }

            bool canNext = selectedProfileIndex >= 0 && selectedProfileIndex < (ProfilesPage.profiles?.Count ?? 0);
            int nav = DrawWizardNav("< Back##backToSystem", canNext ? "Next##nextStep2Top" : null);
            if (nav == 1) { wizardStep = 0; return; }
            if (nav == 2) wizardStep = 2;

            var profiles = ProfilesPage.profiles;

            // Avatars come with the profile list itself (Profiles_DR.ReceiveProfiles).

            bool open = RsElements.BeginPanel("sys_view_profiles", "Choose Your Profile", fitContentsY: true);
            try
            {
                if (!open) return;

                if (profiles == null || profiles.Count == 0)
                {
                    SysUI.Muted("No profiles found. Create a profile first.");
                    return;
                }

                SysUI.MutedWrapped($"Pick the profile that will represent your character in {selectedSystem.name}.");
                SysUI.Gap(6f);

                float rowW = RsElements.AvailContentWidth();
                float rowH = SysUI.S(46f);
                float avatar = SysUI.S(32f);
                var draw = ImGui.GetWindowDrawList();

                for (int i = 0; i < profiles.Count; i++)
                {
                    var prof = profiles[i];
                    bool isSelected = i == selectedProfileIndex;

                    ImGui.PushID($"prof_{i}");
                    try
                    {
                        var min = ImGui.GetCursorScreenPos();
                        var max = min + new Vector2(rowW, rowH);
                        bool hovered = ImGui.IsMouseHoveringRect(min, max) && ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows);
                        SysUI.DrawCard(draw, min, max, hovered, isSelected, 6f);

                        float x = min.X + SysUI.S(10f);
                        var ac = new Vector2(x + avatar / 2, min.Y + rowH / 2);
                        if (prof.avatar != null && prof.avatar.Handle != IntPtr.Zero)
                        {
                            draw.AddImageRounded(prof.avatar.Handle, ac - new Vector2(avatar / 2, avatar / 2), ac + new Vector2(avatar / 2, avatar / 2),
                                Vector2.Zero, Vector2.One, 0xFFFFFFFF, avatar / 2);
                            draw.AddCircle(ac, avatar / 2 + SysUI.S(1f), SysUI.U(prof.titleColor), 32, SysUI.S(1.5f));
                        }
                        else
                        {
                            draw.AddCircleFilled(ac, avatar / 2, SysUI.U(RsTheme.BgTertiary), 32);
                            draw.AddCircle(ac, avatar / 2, RsTheme.U.Border, 32, SysUI.S(1f));
                        }

                        string label = !string.IsNullOrEmpty(prof.title) ? prof.title : $"Profile {i + 1}";
                        var tp = new Vector2(x + avatar + SysUI.S(12f), min.Y + (rowH - ImGui.GetTextLineHeight()) / 2);
                        if (!string.IsNullOrEmpty(prof.playerName))
                        {
                            draw.AddText(tp, RsTheme.U.TextMuted, prof.playerName);
                            tp.X += ImGui.CalcTextSize(prof.playerName).X + SysUI.S(8f);
                        }
                        draw.AddText(tp, isSelected ? RsTheme.U.TextPrimary : RsTheme.U.TextSecondary, label);

                        if (isSelected)
                        {
                            var check = "Selected";
                            var cs = ImGui.CalcTextSize(check);
                            draw.AddText(new Vector2(max.X - SysUI.S(12f) - cs.X, min.Y + (rowH - cs.Y) / 2), RsTheme.U.AccentPrimary, check);
                        }

                        if (ImGui.InvisibleButton("##profRow", new Vector2(rowW, rowH)))
                            selectedProfileIndex = i;
                        if (ImGui.IsItemHovered())
                            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                        ImGui.Dummy(new Vector2(0f, SysUI.S(2f)));
                    }
                    finally { ImGui.PopID(); }
                }
            }
            finally { RsElements.EndPanel(); }
        }

        // Shared stat allocation row: swatch, name, (?) tooltip, [-] value [+]. Returns -1 if minus clicked, +1 if plus clicked, 0 otherwise.
        private static int DrawStatAllocRow(StatData stat, int val, bool canDec, bool canInc, bool showRangeInTooltip)
        {
            int result = 0;
            float btn = SysUI.S(28f);
            float valueW = SysUI.S(40f);
            float gap = SysUI.S(6f);
            float clusterW = btn * 2f + valueW + gap * 2f;
            float rowStartX = ImGui.GetCursorPosX();
            float rowW = ImGui.GetContentRegionAvail().X;
            float rowY = ImGui.GetCursorPosY();
            float textOff = (btn - ImGui.GetTextLineHeight()) * 0.5f;

            ImGui.SetCursorPosY(rowY + SysUI.S(4f));
            SysUI.Swatch(stat.color, 4f, 20f);
            ImGui.SameLine(0f, SysUI.S(8f));
            ImGui.SetCursorPosY(rowY + textOff);
            SysUI.Primary(stat.name);
            // Info tooltip with description
            if (!string.IsNullOrEmpty(stat.description))
            {
                ImGui.SameLine(0f, SysUI.S(6f));
                ImGui.SetCursorPosY(rowY + textOff);
                SysUI.Muted("(?)");
                if (ImGui.IsItemHovered())
                {
                    SysUI.BeginTooltip();
                    ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20f);
                    SysUI.Accent(stat.name);
                    ImGui.TextWrapped(stat.description);
                    if (showRangeInTooltip)
                        SysUI.Muted($"Range: {stat.baseMin} - {stat.baseMax}");
                    ImGui.PopTextWrapPos();
                    SysUI.EndTooltip();
                }
            }

            ImGui.SameLine(rowStartX + Math.Max(0f, rowW - clusterW));
            ImGui.SetCursorPosY(rowY);
            if (SysUI.IconButton(FontAwesomeIcon.Minus, "dec", canDec, RsElements.ButtonVariant.Secondary, 28f))
                result = -1;

            ImGui.SameLine(0f, gap);
            var vs = val.ToString();
            var vsz = ImGui.CalcTextSize(vs);
            var p = ImGui.GetCursorScreenPos();
            ImGui.GetWindowDrawList().AddText(p + new Vector2((valueW - vsz.X) * 0.5f, (btn - vsz.Y) * 0.5f), RsTheme.U.TextPrimary, vs);
            ImGui.Dummy(new Vector2(valueW, btn));

            ImGui.SameLine(0f, gap);
            if (SysUI.IconButton(FontAwesomeIcon.Plus, "inc", canInc, RsElements.ButtonVariant.Secondary, 28f))
                result = 1;

            SysUI.Gap(4f);
            return result;
        }

        // Point Assignment Mode
        private static void DrawPointAssignment()
        {
            var sheet = assigningSheet;
            var system = selectedSystem;

            if (RsElements.Button("< Back##backFromAssign", RsElements.ButtonVariant.Ghost))
            {
                assigningPoints = false;
                assigningSheet = null;
                return;
            }

            SysUI.Gap(6f);
            SysUI.Heading("Assign Bonus Points", $"Spend the bonus points the owner of {system.name} has granted you.");

            // Visible tabs: 0 = stats, 1 = skills
            var kinds = new List<int>(2);
            var items = new List<RsElements.NavItem>(2);
            if (sheet.bonusStatPoints > 0) { kinds.Add(0); items.Add(new(FontAwesomeIcon.SlidersH, $"Stats ({sheet.bonusStatPoints} pts)")); }
            if (sheet.bonusSkillPoints > 0) { kinds.Add(1); items.Add(new(FontAwesomeIcon.ProjectDiagram, $"Skills ({sheet.bonusSkillPoints} pts)")); }
            if (kinds.Count == 0)
            {
                SysUI.Muted("No bonus points left to assign.");
                return;
            }
            if (assignTab < 0 || assignTab >= kinds.Count) assignTab = 0;
            RsElements.NavigationMenu("sys_assign_tabs", ref assignTab, items);
            SysUI.Gap(10f);

            if (kinds[assignTab] == 0)
            {
                // Calculate budget: original points already spent + bonus points
                int originalSpent = 0;
                foreach (var kvp in system.StatsData)
                    originalSpent += Roster.Roster.GetSheetStatValue(system, sheet, kvp.Value.id);
                int totalBudget = originalSpent + sheet.bonusStatPoints;
                int currentSpent = assignStatAllocations.Values.Sum();
                int remaining = totalBudget - currentSpent;

                bool open = RsElements.BeginPanel("sys_assign_stats", null, fitContentsY: true);
                try
                {
                    if (open)
                    {
                        SysUI.Muted($"{originalSpent} spent + {sheet.bonusStatPoints} bonus = {totalBudget} total stat points");
                        SysUI.Gap(4f);
                        Vector4 spColor = remaining > 0 ? RsTheme.AccentPrimary : remaining == 0 ? RsTheme.TextMuted : RsTheme.AccentDanger;
                        SysUI.Badge($"{remaining} points remaining", spColor);
                        SysUI.Divider();

                        // Two-column layout: controls left, radar right
                        float panelWidth = RsElements.AvailContentWidth();
                        float controlsWidth = panelWidth * 0.48f;
                        float chartWidth = panelWidth * 0.48f;
                        float colH = Math.Max(SysUI.S(300f), system.StatsData.Count * SysUI.S(36f) + SysUI.S(10f));
                        bool changed = false;

                        if (ImGui.BeginChild("##assignStatControls", new Vector2(controlsWidth, colH), false))
                        {
                            foreach (var kvp in system.StatsData)
                            {
                                var stat = kvp.Value;
                                int key = kvp.Key;
                                if (!assignStatAllocations.ContainsKey(key))
                                    assignStatAllocations[key] = Roster.Roster.GetSheetStatValue(system, sheet, stat.id);
                                int val = assignStatAllocations[key];

                                ImGui.PushID($"astat_{key}");
                                try
                                {
                                    // Can't go below 0 (or baseMin)
                                    bool canDec = stat.canRemovePoints && (stat.canGoNegative || val > stat.baseMin);
                                    bool canInc = stat.canAddPoints && val < stat.baseMax && remaining > 0;
                                    int d = DrawStatAllocRow(stat, val, canDec, canInc, false);
                                    if (d != 0) { assignStatAllocations[key] = val + d; changed = true; }
                                }
                                finally { ImGui.PopID(); }
                            }
                        }
                        ImGui.EndChild();

                        // Trigger radar animation on change
                        if (changed) TriggerAssignRadarAnimation(system, totalBudget);

                        // Radar chart on the right
                        ImGui.SameLine(0f, panelWidth * 0.04f);
                        if (ImGui.BeginChild("##assignRadar", new Vector2(chartWidth, colH), false))
                            DrawAssignRadarChart(system, totalBudget);
                        ImGui.EndChild();
                    }
                }
                finally { RsElements.EndPanel(); }

                SysUI.Gap(10f);
                if (RsElements.Button("Save Stats##saveAssignStats", RsElements.ButtonVariant.Primary))
                {
                    if (Plugin.character != null)
                    {
                        var statsByStatId = new Dictionary<int, int>();
                        foreach (var kvp in assignStatAllocations)
                        {
                            if (system.StatsData.ContainsKey(kvp.Key))
                                statsByStatId[system.StatsData[kvp.Key].id] = kvp.Value;
                        }
                        int newPointsUsed = currentSpent - originalSpent;
                        sheet.statValues = statsByStatId;
                        sheet.bonusStatPoints = Math.Max(0, sheet.bonusStatPoints - newPointsUsed);
                        // Update existing sheet (not create new)
                        AbsoluteRP.Network.Systems_DS.UpdateCharacterSheet(Plugin.character, sheet.id, statsByStatId, sheet.learnedSkills);
                        AbsoluteRP.Network.Systems_DS.UpdateSheetLevelPoints(Plugin.character, sheet.id, sheet.level, sheet.bonusSkillPoints, sheet.bonusStatPoints);
                        assigningPoints = false;
                        assigningSheet = null;
                    }
                }
            }
            else
            {
                // Skill assignment tab
                int originalSkillCount = sheet.learnedSkills.Count;
                int newSkillsAdded = assignSkillPointsUsed - originalSkillCount;
                int remainingSkill = sheet.bonusSkillPoints - Math.Max(0, newSkillsAdded);

                SysUI.Badge($"Bonus Skill Points: {remainingSkill} / {sheet.bonusSkillPoints}",
                    remainingSkill > 0 ? RsTheme.AccentPrimary : RsTheme.TextMuted);
                SysUI.Gap(8f);

                if (sheet.classId >= 0)
                {
                    var cls = system.SkillClasses.FirstOrDefault(c => c.id == sheet.classId);
                    if (cls != null)
                    {
                        // Reuse the skill tree picker but with assign state Temporarily swap the global state
                        var savedSkills = selectedSkills;
                        var savedTiers = skillTiers;
                        var savedUsed = skillPointsUsed;
                        selectedSkills = assignSelectedSkills;
                        skillTiers = assignSkillTiers;
                        skillPointsUsed = assignSkillPointsUsed;

                        // Override max points to include bonus
                        int totalSkillBudget = cls.initialSkillPoints + sheet.bonusSkillPoints;
                        var tempCls = new SkillClassData
                        {
                            id = cls.id, name = cls.name, description = cls.description,
                            sortOrder = cls.sortOrder, iconId = cls.iconId,
                            initialSkillPoints = totalSkillBudget,
                            SkillTrees = cls.SkillTrees,
                        };
                        try
                        {
                            bool open = RsElements.BeginPanel("sys_assign_skills", null, fitContentsY: true);
                            try
                            {
                                if (open)
                                    DrawSkillTreePicker(system, tempCls);
                            }
                            finally { RsElements.EndPanel(); }
                        }
                        finally
                        {
                            // Save back
                            assignSelectedSkills = selectedSkills;
                            assignSkillTiers = skillTiers;
                            assignSkillPointsUsed = skillPointsUsed;
                            selectedSkills = savedSkills;
                            skillTiers = savedTiers;
                            skillPointsUsed = savedUsed;
                        }

                        SysUI.Gap(10f);
                        if (RsElements.Button("Save Skills##saveAssignSkills", RsElements.ButtonVariant.Primary))
                        {
                            if (Plugin.character != null)
                            {
                                sheet.learnedSkills = new List<int>(assignSelectedSkills);
                                int skillsAdded = assignSelectedSkills.Count - originalSkillCount;
                                sheet.bonusSkillPoints = Math.Max(0, sheet.bonusSkillPoints - Math.Max(0, skillsAdded));
                                // Update existing sheet (not create new)
                                AbsoluteRP.Network.Systems_DS.UpdateCharacterSheet(Plugin.character, sheet.id, sheet.statValues, assignSelectedSkills);
                                AbsoluteRP.Network.Systems_DS.UpdateSheetLevelPoints(Plugin.character, sheet.id, sheet.level, sheet.bonusSkillPoints, sheet.bonusStatPoints);
                                assigningPoints = false;
                                assigningSheet = null;
                            }
                        }
                    }
                }
                else
                {
                    SysUI.Muted("No class assigned.");
                }
            }
        }

        // Assign Mode Radar Chart
        private static List<float> assignPrevRadii = new List<float>();
        private static List<float> assignTargetRadii = new List<float>();
        private static float assignMorphProgress = 1f;
        private static DateTime assignMorphStart = DateTime.MinValue;

        private static void TriggerAssignRadarAnimation(SystemData system, int totalBudget)
        {
            int count = system.StatsData.Count;
            if (count == 0) return;
            assignPrevRadii = GetAssignInterpolatedRadii(count);
            assignTargetRadii = new List<float>(count);
            foreach (var kvp in system.StatsData)
            {
                int key = kvp.Key;
                int val = assignStatAllocations.ContainsKey(key) ? assignStatAllocations[key] : 0;
                float linear = totalBudget > 0 ? (float)val / totalBudget : 0;
                assignTargetRadii.Add(linear >= 0 ? MathF.Sqrt(linear) : 0f);
            }
            assignMorphProgress = 0f;
            assignMorphStart = DateTime.Now;
        }

        private static List<float> GetAssignInterpolatedRadii(int count)
        {
            if (assignPrevRadii.Count != count || assignTargetRadii.Count != count)
                return new List<float>(Enumerable.Repeat(0.05f, count));
            float t = Math.Clamp(assignMorphProgress, 0f, 1f);
            t = 1f - (1f - t) * (1f - t);
            var result = new List<float>(count);
            for (int i = 0; i < count; i++)
                result.Add(assignPrevRadii[i] + (assignTargetRadii[i] - assignPrevRadii[i]) * t);
            return result;
        }

        // Shared radar renderer: rings, axes, coloured labels, filled data polygon.
        private static void DrawRadar(IList<StatData> stats, IList<float> radii, float availWidth)
        {
            int count = stats.Count;
            if (count < 2) return;

            float chartRadius = Math.Min(availWidth * 0.40f, SysUI.S(140f));
            Vector2 cursorStart = ImGui.GetCursorScreenPos();
            Vector2 center = cursorStart + new Vector2(availWidth / 2, chartRadius + SysUI.S(20f));
            var drawList = ImGui.GetWindowDrawList();
            float angleStep = 2f * MathF.PI / count;
            float startAngle = -MathF.PI / 2f;

            uint ringColor = SysUI.U(SysUI.Fade(RsTheme.Border, 0.7f));
            for (int ring = 1; ring <= 4; ring++)
            {
                float r = chartRadius * ring / 4f;
                var ringPoints = new Vector2[count];
                for (int i = 0; i < count; i++)
                    ringPoints[i] = center + new Vector2(MathF.Cos(startAngle + i * angleStep), MathF.Sin(startAngle + i * angleStep)) * r;
                drawList.AddPolyline(ref ringPoints[0], ringPoints.Length, ringColor, ImDrawFlags.Closed, 1f);
            }

            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + i * angleStep;
                Vector2 axisEnd = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * chartRadius;
                drawList.AddLine(center, axisEnd, ringColor, 1f);
                Vector2 labelPos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (chartRadius + SysUI.S(14f));
                string label = stats[i].name;
                var textSize = ImGui.CalcTextSize(label);
                drawList.AddText(labelPos - textSize / 2, ImGui.ColorConvertFloat4ToU32(stats[i].color), label);
            }

            var dataPoints = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float rr = i < radii.Count ? radii[i] : 0f;
                float r = Math.Max(rr, 0.05f) * chartRadius;
                float angle = startAngle + i * angleStep;
                dataPoints[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * r;
            }

            uint fillColor = SysUI.U(SysUI.Fade(RsTheme.AccentPrimary, 0.2f));
            for (int i = 0; i < count; i++)
                drawList.AddTriangleFilled(center, dataPoints[i], dataPoints[(i + 1) % count], fillColor);

            drawList.AddPolyline(ref dataPoints[0], dataPoints.Length, SysUI.U(SysUI.Fade(RsTheme.AccentPrimary, 0.8f)), ImDrawFlags.Closed, SysUI.S(2.5f));

            uint dotColor = RsTheme.U.AccentPrimary;
            for (int i = 0; i < count; i++)
                if (i < radii.Count && radii[i] > 0.01f) drawList.AddCircleFilled(dataPoints[i], SysUI.S(4f), dotColor);

            ImGui.SetCursorScreenPos(cursorStart);
            ImGui.Dummy(new Vector2(availWidth, chartRadius * 2 + SysUI.S(50f)));
        }

        private static void DrawAssignRadarChart(SystemData system, int totalBudget)
        {
            int count = system.StatsData.Count;
            if (count < 2) return;

            if (assignMorphProgress < 1f)
            {
                float elapsed = (float)(DateTime.Now - assignMorphStart).TotalSeconds;
                assignMorphProgress = Math.Clamp(elapsed / MorphDuration, 0f, 1f);
            }

            // Init if needed
            if (assignTargetRadii.Count != count)
            {
                assignPrevRadii = new List<float>(count);
                assignTargetRadii = new List<float>(count);
                foreach (var kvp in system.StatsData)
                {
                    int key = kvp.Key;
                    int val = assignStatAllocations.ContainsKey(key) ? assignStatAllocations[key] : 0;
                    float linear = totalBudget > 0 ? (float)val / totalBudget : 0;
                    float r = linear >= 0 ? MathF.Sqrt(linear) : 0f;
                    assignPrevRadii.Add(r);
                    assignTargetRadii.Add(r);
                }
                assignMorphProgress = 1f;
            }

            DrawRadar(system.StatsData.Values, GetAssignInterpolatedRadii(count), ImGui.GetContentRegionAvail().X);
        }

        // Step 2: Rules
        private static void DrawRulesStep()
        {
            if (selectedSystem == null) { wizardStep = 0; return; }

            int nav = DrawWizardNav("< Back##backToProfile", "Continue##continueFromRulesTop");
            if (nav == 1) { wizardStep = 1; return; }
            if (nav == 2)
            {
                selectedClassIndex = -1;
                hoveredClassIndex = -1;
                selectedSkills.Clear();
                skillTiers.Clear();
                skillPointsUsed = 0;
                skillsRolled = false; rolledSkillPoints = 0;
                previewTreeIndex = 0;
                if (selectedSystem.SkillClasses.Count == 0 && selectedSystem.id > 0 && Plugin.character != null)
                    AbsoluteRP.Network.Systems_DS.FetchSystem(Plugin.character, selectedSystem.id);
                wizardStep = 3;
            }

            bool open = RsElements.BeginPanel("sys_view_rules", "System Rules", fitContentsY: true);
            try
            {
                if (open)
                {
                    if (!string.IsNullOrEmpty(selectedSystem.rules))
                        SysUI.SecondaryWrapped(selectedSystem.rules);
                    else
                        SysUI.Muted("This system has no rules defined.");
                }
            }
            finally { RsElements.EndPanel(); }
        }

        // Step 3: Class + Skill Selection
        private static void DrawClassAndSkillSelection()
        {
            if (selectedSystem == null) { wizardStep = 0; return; }

            int nav = DrawWizardNav("< Back##backToRules", selectedClassIndex >= 0 ? "Next: Assign Stats##nextStep3Top" : null);
            if (nav == 1) { wizardStep = 2; return; }
            if (nav == 2)
            {
                InitStatAllocations();
                wizardStep = 4;
            }

            var classes = selectedSystem.SkillClasses;
            if (classes.Count == 0)
            {
                bool emptyOpen = RsElements.BeginPanel("sys_view_noclass", "Choose Your Class", fitContentsY: true);
                try
                {
                    if (emptyOpen)
                    {
                        SysUI.Muted("This system has no classes defined.");
                        SysUI.Gap(6f);
                        if (RsElements.Button("Skip to Stats##skipClass", RsElements.ButtonVariant.Primary))
                        {
                            selectedClassIndex = -1;
                            InitStatAllocations();
                            wizardStep = 4;
                        }
                    }
                }
                finally { RsElements.EndPanel(); }
                return;
            }

            SysUI.SectionLabel("Choose Your Class");

            // Class icon grid
            float iconSize = SysUI.S(64f);
            float spacing = SysUI.S(14f);
            float labelH = ImGui.GetTextLineHeight() + SysUI.S(4f);
            float totalW = ImGui.GetContentRegionAvail().X;
            float availWidth = totalW * 0.58f;
            int cols = Math.Max(1, (int)((availWidth + spacing) / (iconSize + spacing)));

            // Left panel: class icons + skill tree
            if (ImGui.BeginChild("##classGrid", new Vector2(availWidth, 0), false))
            {
                var drawList = ImGui.GetWindowDrawList();
                Vector2 cursor = ImGui.GetCursorScreenPos() + new Vector2(SysUI.S(2f), SysUI.S(2f));

                for (int i = 0; i < classes.Count; i++)
                {
                    var cls = classes[i];
                    int col = i % cols;
                    int row = i / cols;
                    Vector2 cellMin = cursor + new Vector2(col * (iconSize + spacing), row * (iconSize + labelH + spacing));
                    Vector2 center = cellMin + new Vector2(iconSize / 2, iconSize / 2);
                    float octRadius = iconSize / 2 - SysUI.S(2f);
                    var octPoints = Skills.Skills.GetOctagonPoints(center, octRadius);

                    bool isSelected = i == selectedClassIndex;
                    bool hovered = ImGui.IsMouseHoveringRect(center - new Vector2(octRadius, octRadius), center + new Vector2(octRadius, octRadius))
                        && ImGui.IsWindowHovered();

                    if (isSelected)
                        Skills.Skills.DrawFilledOctagon(drawList, Skills.Skills.GetOctagonPoints(center, octRadius + SysUI.S(4f)),
                            SysUI.U(SysUI.Fade(RsTheme.AccentPrimary, 0.25f)));

                    if (cls.iconTexture != null && cls.iconTexture.Handle != IntPtr.Zero)
                    {
                        Skills.Skills.DrawOctagonImage(drawList, cls.iconTexture, center, octRadius, octPoints, 0xFFFFFFFF);
                    }
                    else
                    {
                        uint fillColor = isSelected ? RsTheme.U.AccentPrimary : SysUI.U(hovered ? SysUI.Brighten(RsTheme.BgTertiary, 0.04f) : RsTheme.BgTertiary);
                        Skills.Skills.DrawFilledOctagon(drawList, octPoints, fillColor);
                        string initial = string.IsNullOrEmpty(cls.name) ? "?" : cls.name.Substring(0, 1).ToUpperInvariant();
                        var textSize = ImGui.CalcTextSize(initial);
                        drawList.AddText(center - textSize / 2, RsTheme.U.TextPrimary, initial);
                    }

                    uint borderColor = isSelected ? RsTheme.U.AccentPrimary
                        : hovered ? RsTheme.U.BorderStrong : SysUI.U(SysUI.Fade(RsTheme.AccentPrimary, 0.45f));
                    drawList.AddPolyline(ref octPoints[0], octPoints.Length, borderColor, ImDrawFlags.Closed, isSelected ? SysUI.S(3f) : SysUI.S(1.5f));

                    // Name under the icon
                    string nm = Truncate(cls.name, iconSize + spacing - SysUI.S(4f));
                    var ns = ImGui.CalcTextSize(nm);
                    drawList.AddText(new Vector2(center.X - ns.X / 2, cellMin.Y + iconSize + SysUI.S(3f)),
                        isSelected ? RsTheme.U.TextPrimary : RsTheme.U.TextSecondary, nm);

                    ImGui.SetCursorScreenPos(center - new Vector2(octRadius, octRadius));
                    if (ImGui.InvisibleButton($"##cls_{i}", new Vector2(octRadius * 2, octRadius * 2)))
                    {
                        if (selectedClassIndex != i)
                        {
                            selectedClassIndex = i;
                            previewTreeIndex = 0;
                            selectedSkills.Clear();
                            skillPointsUsed = 0;
                            skillsRolled = false; rolledSkillPoints = 0;
                        }
                    }
                    if (ImGui.IsItemHovered())
                    {
                        hoveredClassIndex = i;
                        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                        SysUI.BeginTooltip();
                        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20f);
                        SysUI.Accent(cls.name);
                        if (!string.IsNullOrEmpty(cls.description))
                            ImGui.TextWrapped(cls.description);
                        if (cls.initialSkillPoints > 0)
                            SysUI.Secondary($"Skill Points: {cls.initialSkillPoints}");
                        ImGui.PopTextWrapPos();
                        SysUI.EndTooltip();
                    }
                }

                // Reserve space for class icons
                int totalRows = (classes.Count + cols - 1) / cols;
                ImGui.SetCursorScreenPos(cursor - new Vector2(SysUI.S(2f), SysUI.S(2f)));
                ImGui.Dummy(new Vector2(Math.Max(1f, cols * (iconSize + spacing) - spacing) + SysUI.S(4f), totalRows * (iconSize + labelH + spacing) + SysUI.S(4f)));

                // Skill tree selection (pick skills from tree)
                if (selectedClassIndex >= 0 && selectedClassIndex < classes.Count)
                {
                    var cls = classes[selectedClassIndex];
                    int maxSkillPoints = SkillBudget(selectedSystem, cls);

                    SysUI.Divider();

                    if (selectedSystem.rollForSkills && !skillsRolled)
                    {
                        SysUI.MutedWrapped($"This system rolls skill points: roll {DiceLabel(selectedSystem)} once"
                            + (cls.initialSkillPoints > 0 ? $" (at most {cls.initialSkillPoints})." : "."));
                        SysUI.Gap(4f);
                        if (RsElements.Button("Roll skill points##sys_roll_skills_btn", RsElements.ButtonVariant.Primary))
                        {
                            int r = RollDice(selectedSystem);
                            if (cls.initialSkillPoints > 0) r = Math.Min(r, cls.initialSkillPoints);
                            rolledSkillPoints = Math.Max(1, r);
                            skillsRolled = true;
                        }
                    }
                    else
                    {
                        if (maxSkillPoints > 0)
                        {
                            int remaining = maxSkillPoints - skillPointsUsed;
                            SysUI.Badge($"Skill Points: {remaining} / {maxSkillPoints}",
                                remaining > 0 ? RsTheme.AccentPrimary : RsTheme.TextMuted);
                            SysUI.Gap(6f);
                        }

                        DrawSkillTreePicker(selectedSystem, cls);
                    }
                }
            }
            ImGui.EndChild();

            // Right panel: passives
            ImGui.SameLine(0f, SysUI.S(12f));
            int previewIdx = selectedClassIndex >= 0 ? selectedClassIndex : hoveredClassIndex;
            string passTitle = previewIdx >= 0 && previewIdx < classes.Count ? $"{classes[previewIdx].name} - Passives" : "Passives";
            bool passOpen = RsElements.BeginPanel("sys_view_passives", passTitle, Vector2.Zero);
            try
            {
                if (passOpen)
                {
                    if (previewIdx >= 0 && previewIdx < classes.Count)
                    {
                        var cls = classes[previewIdx];

                        if (!string.IsNullOrEmpty(cls.description))
                        {
                            SysUI.SecondaryWrapped(cls.description);
                            SysUI.Divider();
                        }

                        var passives = selectedSystem.Skills.Where(s => s.classId == cls.id && !s.isCastable).ToList();
                        if (passives.Count == 0)
                        {
                            SysUI.Muted("No passives.");
                        }
                        else
                        {
                            foreach (var passive in passives)
                            {
                                if (passive.iconTexture != null && passive.iconTexture.Handle != IntPtr.Zero)
                                {
                                    ImGui.Image(passive.iconTexture.Handle, new Vector2(SysUI.S(24f), SysUI.S(24f)));
                                    ImGui.SameLine(0f, SysUI.S(8f));
                                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (SysUI.S(24f) - ImGui.GetTextLineHeight()) * 0.5f);
                                }
                                SysUI.Primary(passive.name);
                                if (!string.IsNullOrEmpty(passive.description))
                                {
                                    ImGui.Indent(SysUI.S(12f));
                                    SysUI.MutedWrapped(passive.description);
                                    ImGui.Unindent(SysUI.S(12f));
                                }
                                SysUI.Gap(6f);
                            }
                        }
                    }
                    else
                    {
                        SysUI.MutedWrapped("Hover or select a class to see its passives.");
                    }
                }
            }
            finally { RsElements.EndPanel(); }
        }

        // Skill Tree Picker (interactive, used during class selection)
        /// Helper: get current tier invested in a skill (0 if none).
        private static int GetSkillTier(int skillId) => skillTiers.ContainsKey(skillId) ? skillTiers[skillId] : 0;

        /// Helper: check if a skill is fully maxed (all tiers invested).
        private static bool IsSkillMaxed(SkillData skill) => GetSkillTier(skill.id) >= skill.maxTiers;

        /// Check if all prerequisites for a skill are satisfied (parent skills fully maxed).
        private static bool ArePrereqsMet(SystemData system, int skillId)
        {
            var prereqs = system.SkillConnections.Where(c => c.toSkillId == skillId).ToList();
            if (prereqs.Count == 0) return true;
            foreach (var prereq in prereqs)
            {
                var parentSkill = system.Skills.FirstOrDefault(s => s.id == prereq.fromSkillId);
                if (parentSkill == null) return false;
                if (GetSkillTier(parentSkill.id) < parentSkill.maxTiers) return false;
            }
            return true;
        }

        /// Rebuild the selectedSkills list from skillTiers (skills with tier >= 1).
        private static void RebuildSelectedSkills()
        {
            selectedSkills.Clear();
            foreach (var kvp in skillTiers)
            {
                if (kvp.Value > 0)
                    selectedSkills.Add(kvp.Key);
            }
        }

        private static void DrawSkillTreePicker(SystemData system, SkillClassData cls)
        {
            int classId = cls.id;
            int maxSkillPoints = SkillBudget(system, cls);

            // Tree tabs
            if (cls.SkillTrees.Count > 0)
            {
                if (previewTreeIndex >= cls.SkillTrees.Count)
                    previewTreeIndex = 0;

                var treeItems = new List<RsElements.NavItem>(cls.SkillTrees.Count);
                for (int t = 0; t < cls.SkillTrees.Count; t++)
                    treeItems.Add(new RsElements.NavItem(FontAwesomeIcon.ProjectDiagram,
                        string.IsNullOrEmpty(cls.SkillTrees[t].name) ? $"Tree {t + 1}" : cls.SkillTrees[t].name));
                RsElements.NavigationMenu($"sys_picker_tree_nav_{cls.id}", ref previewTreeIndex, treeItems);
                SysUI.Gap(6f);
            }

            SysUI.Muted("Left-click: add tier  |  Right-click: remove tier");
            SysUI.Gap(4f);

            float gridWidth = RsElements.AvailContentWidth();
            float cellSize = Math.Min((gridWidth - SysUI.S(20f)) / PreviewGridCols, SysUI.S(64f));
            float octRadius = cellSize * 0.35f;

            var drawList = ImGui.GetWindowDrawList();
            Vector2 origin = ImGui.GetCursorScreenPos();

            var treeSkills = system.Skills.Where(s => s.classId == classId && s.treeIndex == previewTreeIndex && s.gridX >= 0).ToList();

            // Draw connections
            foreach (var conn in system.SkillConnections)
            {
                var fromSkill = treeSkills.FirstOrDefault(s => s.id == conn.fromSkillId);
                var toSkill = treeSkills.FirstOrDefault(s => s.id == conn.toSkillId);
                if (fromSkill != null && toSkill != null)
                {
                    // Color connection based on whether parent is maxed
                    bool parentMaxed = IsSkillMaxed(fromSkill);
                    uint lineColor = SysUI.U(SysUI.Fade(RsTheme.AccentPrimary, parentMaxed ? 0.85f : 0.3f));

                    Vector2 from = origin + new Vector2(fromSkill.gridX * cellSize + cellSize / 2, fromSkill.gridY * cellSize + cellSize / 2);
                    Vector2 to = origin + new Vector2(toSkill.gridX * cellSize + cellSize / 2, toSkill.gridY * cellSize + cellSize / 2);
                    drawList.AddLine(from, to, lineColor, SysUI.S(2f));

                    Vector2 mid = (from + to) / 2;
                    Vector2 dir = Vector2.Normalize(to - from);
                    Vector2 perp = new Vector2(-dir.Y, dir.X);
                    float arrowSize = SysUI.S(5f);
                    drawList.AddTriangleFilled(mid + dir * arrowSize, mid - dir * arrowSize + perp * arrowSize, mid - dir * arrowSize - perp * arrowSize, lineColor);
                }
            }

            // Draw skill nodes
            for (int y = 0; y < PreviewGridRows; y++)
            {
                for (int x = 0; x < PreviewGridCols; x++)
                {
                    Vector2 center = origin + new Vector2(x * cellSize + cellSize / 2, y * cellSize + cellSize / 2);
                    var skill = treeSkills.FirstOrDefault(s => s.gridX == x && s.gridY == y);
                    var octPoints = Skills.Skills.GetOctagonPoints(center, octRadius);

                    if (skill != null)
                    {
                        int currentTier = GetSkillTier(skill.id);
                        bool hasAnyTier = currentTier > 0;
                        bool isMaxed = currentTier >= skill.maxTiers;

                        // Brightness based on tier progress
                        float alpha = hasAnyTier ? (0.5f + 0.5f * ((float)currentTier / skill.maxTiers)) : 0.3f;

                        if (skill.iconTexture != null && skill.iconTexture.Handle != IntPtr.Zero)
                        {
                            var tintColor = new Vector4(alpha, alpha, alpha, 1f);
                            Skills.Skills.DrawOctagonImage(drawList, skill.iconTexture, center, octRadius, octPoints, ImGui.ColorConvertFloat4ToU32(tintColor));
                        }
                        else
                        {
                            uint fillColor = isMaxed
                                ? RsTheme.U.AccentPrimary
                                : hasAnyTier
                                    ? SysUI.U(SysUI.Mix(RsTheme.BgTertiary, RsTheme.AccentPrimary, 0.55f))
                                    : SysUI.U(RsTheme.BgTertiary);
                            Skills.Skills.DrawFilledOctagon(drawList, octPoints, fillColor);
                            string label = skill.name.Length > 6 ? skill.name[..6] + ".." : skill.name;
                            var textSize = ImGui.CalcTextSize(label);
                            uint textCol = hasAnyTier ? RsTheme.U.TextPrimary : RsTheme.U.TextMuted;
                            drawList.AddText(center - textSize / 2, textCol, label);
                        }

                        uint borderColor = isMaxed
                            ? RsTheme.U.AccentPrimary
                            : hasAnyTier
                                ? SysUI.U(SysUI.Fade(RsTheme.AccentPrimary, 0.6f))
                                : SysUI.U(SysUI.Fade(RsTheme.Border, 0.9f));
                        drawList.AddPolyline(ref octPoints[0], octPoints.Length, borderColor, ImDrawFlags.Closed, isMaxed ? SysUI.S(2.5f) : SysUI.S(1.5f));

                        // Tier counter (bottom-right) - always show for multi-tier skills
                        if (skill.maxTiers > 1)
                        {
                            string tierLabel = $"{currentTier}/{skill.maxTiers}";
                            var tierSize = ImGui.CalcTextSize(tierLabel);
                            Vector2 tierPos = center + new Vector2(octRadius * 0.5f - tierSize.X / 2, octRadius * 0.55f);
                            // Black shadow
                            drawList.AddText(tierPos + new Vector2(1, 1), 0xFF000000, tierLabel);
                            // Coloured text: success if maxed, warning if partial, muted if none
                            uint tierCol = isMaxed ? RsTheme.U.AccentSuccess : hasAnyTier ? RsTheme.U.AccentWarning : RsTheme.U.TextMuted;
                            drawList.AddText(tierPos, tierCol, tierLabel);
                        }

                        // Click handling - invisible button covers the octagon
                        ImGui.SetCursorScreenPos(center - new Vector2(octRadius, octRadius));
                        ImGui.InvisibleButton($"##pick_{x}_{y}", new Vector2(octRadius * 2, octRadius * 2));

                        // Left-click: add a tier
                        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                        {
                            if (!isMaxed && (maxSkillPoints <= 0 || skillPointsUsed < maxSkillPoints))
                            {
                                // Check prerequisites: parent skills must be fully maxed
                                if (ArePrereqsMet(system, skill.id))
                                {
                                    skillTiers[skill.id] = currentTier + 1;
                                    skillPointsUsed++;
                                    RebuildSelectedSkills();
                                }
                            }
                        }

                        // Right-click: remove a tier
                        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                        {
                            if (currentTier > 0)
                            {
                                // Check if any child skill depends on this being maxed
                                bool canRemove = true;
                                if (currentTier == skill.maxTiers) // going from maxed to not-maxed
                                {
                                    var children = system.SkillConnections.Where(c => c.fromSkillId == skill.id).ToList();
                                    foreach (var child in children)
                                    {
                                        if (GetSkillTier(child.toSkillId) > 0)
                                        { canRemove = false; break; }
                                    }
                                }

                                if (canRemove)
                                {
                                    skillTiers[skill.id] = currentTier - 1;
                                    skillPointsUsed--;
                                    if (skillTiers[skill.id] <= 0)
                                        skillTiers.Remove(skill.id);
                                    RebuildSelectedSkills();
                                }
                            }
                        }

                        // Tooltip
                        if (ImGui.IsItemHovered())
                        {
                            SysUI.BeginTooltip();
                            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20f);
                            SysUI.Accent(skill.name);
                            if (skill.maxTiers > 1)
                                SysUI.Secondary($"Tier: {currentTier} / {skill.maxTiers}");
                            if (!string.IsNullOrEmpty(skill.description))
                                ImGui.TextWrapped(skill.description);
                            if (skill.cooldownTurns > 0)
                                SysUI.Secondary($"Cooldown: {skill.cooldownTurns} turns");
                            if (skill.resourceCost > 0)
                                SysUI.Secondary($"Cost: {skill.resourceCost}");
                            if (!isMaxed)
                            {
                                bool met = ArePrereqsMet(system, skill.id);
                                var prereqs = system.SkillConnections.Where(c => c.toSkillId == skill.id).ToList();
                                if (prereqs.Count > 0)
                                {
                                    SysUI.Text(met ? "Prerequisites met" : "Prerequisites not met — parent skills must be maxed",
                                        met ? RsTheme.AccentSuccess : RsTheme.AccentDanger);
                                }
                            }
                            else
                            {
                                SysUI.Text("Maxed!", RsTheme.AccentSuccess);
                            }
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
            ImGui.Dummy(new Vector2(PreviewGridCols * cellSize, PreviewGridRows * cellSize + SysUI.S(10f)));
            if (treeSkills.Count == 0)
                SysUI.Muted("No skills in this tree.");
        }

        // Step 3: Stat Assignment
        private static void InitStatAllocations()
        {
            statAllocations.Clear();
            statsRolled = false;
            if (selectedSystem == null) return;
            foreach (var kvp in selectedSystem.StatsData)
                statAllocations[kvp.Key] = 0;
            int count = selectedSystem.StatsData.Count;
            float startSize = 0.05f;
            previousRadii = new List<float>(count);
            targetRadii = new List<float>(count);
            for (int i = 0; i < count; i++)
            {
                previousRadii.Add(startSize);
                targetRadii.Add(startSize);
            }
            morphProgress = 1f;
        }

        private static void TriggerRadarAnimation()
        {
            if (selectedSystem == null) return;
            var stats = selectedSystem.StatsData;
            int count = stats.Count;
            if (count == 0) return;
            previousRadii = GetCurrentInterpolatedRadii();
            int budget = selectedSystem.basePointsAvailable > 0 ? selectedSystem.basePointsAvailable : 1;
            targetRadii = new List<float>(count);
            foreach (var kvp in stats)
            {
                int key = kvp.Key;
                int val = statAllocations.ContainsKey(key) ? statAllocations[key] : 0;
                float linear = (float)val / budget;
                float radius = linear >= 0 ? MathF.Sqrt(linear) : 0f;
                targetRadii.Add(radius);
            }
            morphProgress = 0f;
            morphStartTime = DateTime.Now;
        }

        private static List<float> GetCurrentInterpolatedRadii()
        {
            if (selectedSystem == null) return new List<float>();
            int count = selectedSystem.StatsData.Count;
            if (previousRadii.Count != count || targetRadii.Count != count)
                return new List<float>(new float[count]);
            float t = Math.Clamp(morphProgress, 0f, 1f);
            t = 1f - (1f - t) * (1f - t);
            var result = new List<float>(count);
            for (int i = 0; i < count; i++)
                result.Add(previousRadii[i] + (targetRadii[i] - previousRadii[i]) * t);
            return result;
        }

        private static void DrawStatAssignment()
        {
            if (selectedSystem == null) { wizardStep = 0; return; }

            int nav = DrawWizardNav("< Back##backToClass", "Next: Review##nextStep4Top");
            if (nav == 1) { wizardStep = 3; return; }
            if (nav == 2) wizardStep = 5;

            int totalBudget = selectedSystem.basePointsAvailable;
            int spent = statAllocations.Values.Sum();
            int remaining = totalBudget - spent;
            bool rolled = selectedSystem.rollForStats;

            var stats = selectedSystem.StatsData;
            bool changed = false;

            bool open = RsElements.BeginPanel("sys_view_stats", rolled ? "Roll Your Stats" : "Assign Stat Points", fitContentsY: true);
            try
            {
                if (open)
                {
                    if (rolled)
                    {
                        SysUI.MutedWrapped($"This system rolls stats: each one is rolled once with {DiceLabel(selectedSystem)} and clamped to its range.");
                        SysUI.Gap(4f);
                        using (Dalamud.Interface.Utility.Raii.ImRaii.Disabled(statsRolled || stats.Count == 0))
                        {
                            if (RsElements.Button((statsRolled ? "Rolled" : "Roll stats") + "##sys_roll_stats_btn", RsElements.ButtonVariant.Primary))
                            {
                                foreach (var kvp in stats)
                                {
                                    var st = kvp.Value;
                                    int r = RollDice(selectedSystem);
                                    if (st.baseMax > st.baseMin) r = Math.Clamp(r, st.baseMin, st.baseMax);
                                    else if (st.baseMax > 0) r = Math.Min(r, st.baseMax);
                                    statAllocations[kvp.Key] = r;
                                }
                                statsRolled = true;
                                changed = true;
                            }
                        }
                        if (!statsRolled && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip("One roll per character — no re-rolls.");
                    }
                    else
                    {
                        Vector4 remainColor = remaining > 0 ? RsTheme.AccentPrimary : remaining == 0 ? RsTheme.TextMuted : RsTheme.AccentDanger;
                        SysUI.Badge($"Points Remaining: {remaining} / {totalBudget}", remainColor);
                    }
                    SysUI.Divider();

                    float panelWidth = RsElements.AvailContentWidth();
                    float controlsWidth = panelWidth * 0.48f;
                    float chartWidth = panelWidth * 0.48f;
                    float colH = Math.Max(SysUI.S(380f), stats.Count * SysUI.S(36f) + SysUI.S(10f));

                    if (ImGui.BeginChild("##statControls", new Vector2(controlsWidth, colH), false))
                    {
                        if (stats.Count == 0)
                            SysUI.Muted("This system has no stats.");
                        foreach (var kvp in stats)
                        {
                            var stat = kvp.Value;
                            int key = kvp.Key;
                            if (!statAllocations.ContainsKey(key))
                                statAllocations[key] = 0;

                            int val = statAllocations[key];

                            ImGui.PushID($"stat_{key}");
                            try
                            {
                                bool canRemove = !rolled && stat.canRemovePoints && (stat.canGoNegative || val > stat.baseMin);
                                bool canAdd = !rolled && stat.canAddPoints && val < stat.baseMax && remaining > 0;
                                int d = DrawStatAllocRow(stat, val, canRemove, canAdd, true);
                                if (d != 0)
                                {
                                    statAllocations[key] = val + d;
                                    changed = true;
                                }
                            }
                            finally { ImGui.PopID(); }
                        }
                    }
                    ImGui.EndChild();

                    if (changed) TriggerRadarAnimation();

                    ImGui.SameLine(0f, panelWidth * 0.04f);
                    if (ImGui.BeginChild("##radarChart", new Vector2(chartWidth, colH), false))
                    {
                        DrawRadarChart(stats, ImGui.GetContentRegionAvail().X);
                        DrawResourceBars(selectedSystem, stats);
                    }
                    ImGui.EndChild();
                }
            }
            finally { RsElements.EndPanel(); }
        }

        // Step 5: Review & Create
        private static void DrawReviewAndCreate()
        {
            if (selectedSystem == null) { wizardStep = 0; return; }

            int nav = DrawWizardNav("< Back##backToStats", null);
            if (nav == 1) { wizardStep = 4; return; }

            var profiles = ProfilesPage.profiles;

            bool open = RsElements.BeginPanel("sys_view_review", "Review Your Character", fitContentsY: true);
            try
            {
                if (open)
                {
                    // Profile
                    if (profiles != null && selectedProfileIndex >= 0 && selectedProfileIndex < profiles.Count)
                    {
                        var prof = profiles[selectedProfileIndex];
                        if (prof.avatar != null && prof.avatar.Handle != IntPtr.Zero)
                        {
                            float avSize = SysUI.S(56f);
                            float centeredX = (RsElements.AvailContentWidth() - avSize) / 2;
                            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + centeredX);
                            Helpers.Anim.DrawCircleAvatarInline(prof.avatar.Handle, avSize, prof.titleColor, borderThickness: 2f);
                        }
                        string profLabel = !string.IsNullOrEmpty(prof.title) ? prof.title : prof.playerName;
                        if (!string.IsNullOrEmpty(profLabel))
                        {
                            var textSize = ImGui.CalcTextSize(profLabel);
                            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (RsElements.AvailContentWidth() - textSize.X) / 2));
                            SysUI.Primary(profLabel);
                        }
                        SysUI.Gap(6f);
                    }

                    float labelW = SysUI.S(70f);
                    float rowX = ImGui.GetCursorPosX();
                    SysUI.Muted("System");
                    ImGui.SameLine(rowX + labelW);
                    SysUI.Accent(selectedSystem.name);

                    if (selectedClassIndex >= 0 && selectedClassIndex < selectedSystem.SkillClasses.Count)
                    {
                        var cls = selectedSystem.SkillClasses[selectedClassIndex];
                        SysUI.Muted("Class");
                        ImGui.SameLine(rowX + labelW);
                        SysUI.Accent(cls.name);
                    }

                    SysUI.Divider();

                    SysUI.SectionLabel("Stats");
                    float rowW = RsElements.AvailContentWidth();
                    foreach (var kvp in selectedSystem.StatsData)
                    {
                        if (statAllocations.ContainsKey(kvp.Key))
                        {
                            float sx = ImGui.GetCursorPosX();
                            SysUI.Swatch(kvp.Value.color, 4f);
                            ImGui.SameLine(0f, SysUI.S(8f));
                            SysUI.Primary(kvp.Value.name);
                            string v = statAllocations[kvp.Key].ToString();
                            ImGui.SameLine(sx + rowW - ImGui.CalcTextSize(v).X);
                            SysUI.Accent(v);
                        }
                    }

                    SysUI.Gap(6f);

                    SysUI.SectionLabel("Skills");
                    if (selectedSkills.Count == 0)
                    {
                        SysUI.Muted("None selected.");
                    }
                    else
                    {
                        foreach (var skillId in selectedSkills)
                        {
                            var skill = selectedSystem.Skills.FirstOrDefault(s => s.id == skillId);
                            if (skill == null) continue;
                            SysUI.Swatch(RsTheme.AccentPrimary, 4f);
                            ImGui.SameLine(0f, SysUI.S(8f));
                            SysUI.Primary(skill.name);
                        }
                    }
                }
            }
            finally { RsElements.EndPanel(); }

            SysUI.Gap(10f);

            if (!string.IsNullOrEmpty(submitMessage))
            {
                SysUI.Wrapped(submitMessage, submitSuccess ? RsTheme.AccentSuccess : RsTheme.AccentDanger);
                SysUI.Gap(6f);
            }

            // Check if user already has a sheet for this system
            string myCharName = Plugin.character?.characterName ?? "";
            var existingSheet = Roster.Roster.sheets.FirstOrDefault(s => s.characterName == myCharName && s.status != 2);
            bool isRevision = revisingSheetId > 0;
            bool hasExisting = existingSheet != null && !isRevision;

            if (hasExisting)
            {
                SysUI.Muted("You already have a sheet for this system.");
            }
            else
            {
                string submitLabel = isRevision ? "Resubmit Revised Sheet##submit" : "Submit Character Sheet##submit";
                if (RsElements.Button(submitLabel, RsElements.ButtonVariant.Primary))
                {
                    if (Plugin.character != null)
                    {
                        int classId = selectedClassIndex >= 0 && selectedClassIndex < selectedSystem.SkillClasses.Count
                            ? selectedSystem.SkillClasses[selectedClassIndex].id : -1;
                        int profileId = -1;
                        if (profiles != null && selectedProfileIndex >= 0 && selectedProfileIndex < profiles.Count)
                            profileId = profiles[selectedProfileIndex].id;
                        var statsByStatId = new Dictionary<int, int>();
                        foreach (var kvp in statAllocations)
                        {
                            if (selectedSystem.StatsData.ContainsKey(kvp.Key))
                            {
                                int statId = selectedSystem.StatsData[kvp.Key].id;
                                statsByStatId[statId] = kvp.Value;
                            }
                        }

                        if (isRevision && revisingSheetId > 0)
                        {
                            // Update existing sheet instead of creating new
                            AbsoluteRP.Network.Systems_DS.UpdateCharacterSheet(Plugin.character, revisingSheetId, statsByStatId, selectedSkills);
                            // Reset status to pending for re-approval
                            AbsoluteRP.Network.Systems_DS.RespondToSheet(Plugin.character, revisingSheetId, 0, "");
                        }
                        else
                        {
                            // New submission
                            AbsoluteRP.Network.Systems_DS.SubmitCharacterSheet(Plugin.character, selectedSystem.id, classId,
                                statsByStatId, selectedSkills, profileId);
                        }
                        revisingSheetId = 0;
                    }
                }

                if (isRevision)
                {
                    SysUI.Gap(4f);
                    SysUI.Text("Revising your existing submission for re-approval.", RsTheme.AccentPrimary);
                }
            }

            if (selectedSystem.requireApproval)
            {
                SysUI.Gap(4f);
                SysUI.Muted("This system requires owner approval. Your sheet will be reviewed.");
            }

            // Return button (always visible - allows going back to system list after submission)
            SysUI.Gap(10f);
            if (RsElements.Button("Return to Systems##returnToSystems", RsElements.ButtonVariant.Secondary))
            {
                wizardStep = 0;
                submitMessage = "";
            }
        }

        // Radar Chart
        private static void DrawRadarChart(SortedList<int, StatData> stats, float availWidth)
        {
            int count = stats.Count;
            if (count < 2) return;

            if (morphProgress < 1f)
            {
                float elapsed = (float)(DateTime.Now - morphStartTime).TotalSeconds;
                morphProgress = Math.Clamp(elapsed / MorphDuration, 0f, 1f);
            }

            var radii = GetCurrentInterpolatedRadii();
            if (radii.Count != count)
                radii = new List<float>(new float[count]);

            DrawRadar(stats.Values, radii, availWidth);
        }

        // Resource Bars
        private static void DrawResourceBars(SystemData system, SortedList<int, StatData> stats)
        {
            var linkedResources = new List<(string name, Vector4 color, int baseVal, int maxVal, int currentVal, int bonusVal)>();

            var combat = system.CombatConfig;
            if (combat.healthEnabled && combat.healthLinkedStatId >= 0)
            {
                int statVal = GetLinkedStatValue(stats, combat.healthLinkedStatId);
                int bonus = (int)(statVal * combat.healthStatMultiplier);
                int current = combat.healthBase + bonus;
                int max = combat.healthMax > 0 ? combat.healthMax : current;
                linkedResources.Add(("Health", RsTheme.AccentDanger, combat.healthBase, max, current, bonus));
            }

            foreach (var r in system.Resources)
            {
                if (r.linkedStatId >= 0)
                {
                    int statVal = GetLinkedStatValue(stats, r.linkedStatId);
                    int bonus = (int)(statVal * r.statMultiplier);
                    int current = r.baseValue + bonus;
                    int max = r.maxValue > 0 ? r.maxValue : current;
                    linkedResources.Add((r.name, r.color, r.baseValue, max, current, bonus));
                }
            }

            if (linkedResources.Count == 0) return;

            SysUI.Divider();
            SysUI.SectionLabel("Resources");

            foreach (var (name, color, baseVal, maxVal, currentVal, bonusVal) in linkedResources)
            {
                string label = bonusVal != 0
                    ? $"{name}: {baseVal} + {bonusVal} = {currentVal} / {maxVal}"
                    : $"{name}: {currentVal} / {maxVal}";
                SysUI.Secondary(label);

                float baseFill = maxVal > 0 ? Math.Clamp((float)baseVal / maxVal, 0f, 1f) : 0f;
                float totalFill = maxVal > 0 ? Math.Clamp((float)currentVal / maxVal, 0f, 1f) : 0f;
                SysUI.Bar(totalFill, color, string.Empty, 14f, baseFill, SysUI.Fade(color, 0.4f));
                SysUI.Gap(4f);
            }
        }

        private static int GetLinkedStatValue(SortedList<int, StatData> stats, int linkedStatId)
        {
            foreach (var kvp in stats)
            {
                if (kvp.Value.id == linkedStatId)
                {
                    int key = kvp.Key;
                    return statAllocations.ContainsKey(key) ? statAllocations[key] : 0;
                }
            }
            return 0;
        }

        // Callbacks
        public static void ClearAssignmentState()
        {
            assigningPoints = false;
            assigningSheet = null;
            assignStatAllocations.Clear();
            assignSkillTiers.Clear();
            assignSelectedSkills.Clear();
            assignSkillPointsUsed = 0;
            assignPrevRadii.Clear();
            assignTargetRadii.Clear();
        }

        public static void OnSubmitResult(bool success, string message)
        {
            submitSuccess = success;
            submitMessage = message;
        }

        public static void OnPublicSystemReceived(SystemData system)
        {
            var existing = availableSystems.FindIndex(s => s.id == system.id);
            if (existing >= 0)
                availableSystems[existing] = system;
            else
                availableSystems.Add(system);
        }
    }
}
