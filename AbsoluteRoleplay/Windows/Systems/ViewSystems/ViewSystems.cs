using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Listings;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AbsoluteRP.Windows.Systems.ViewSystems
{
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

        // Skill selection (tree-based, done during class step)
        // Maps skillId -> current tier (0 = not learned, 1+ = tiers invested)
        private static Dictionary<int, int> skillTiers = new Dictionary<int, int>();
        private static List<int> selectedSkills = new List<int>(); // kept for submission (skills with tier >= 1)
        private static int skillPointsUsed = 0;

        // Stat assignment
        private static Dictionary<int, int> statAllocations = new Dictionary<int, int>();

        // Submission result
        private static string submitMessage = "";
        private static bool submitSuccess = false;

        // Roster view toggle
        private static bool viewingRoster = false;

        // Revision mode — when > 0, we're revising an existing sheet instead of creating new
        private static int revisingSheetId = 0;

        // Point assignment mode
        private static bool assigningPoints = false;
        private static CharacterSheetData assigningSheet = null;
        private static Dictionary<int, int> assignStatAllocations = new Dictionary<int, int>();
        private static Dictionary<int, int> assignSkillTiers = new Dictionary<int, int>();
        private static List<int> assignSelectedSkills = new List<int>();
        private static int assignSkillPointsUsed = 0;

        // Stat radar chart animation
        private static List<float> previousRadii = new List<float>();
        private static List<float> targetRadii = new List<float>();
        private static float morphProgress = 1f;
        private static DateTime morphStartTime = DateTime.MinValue;
        private const float MorphDuration = 0.3f;

        // Skill tree grid constants
        private const int PreviewGridCols = 5;
        private const int PreviewGridRows = 8;

        public static void DrawViewSystems()
        {
            WindowOperations.LoadIconsLazy(Plugin.plugin);

            string[] stepNames = { "System", "Profile", "Rules", "Class & Skills", "Stats", "Review" };
            ImGui.Spacing();
            for (int i = 0; i < stepNames.Length; i++)
            {
                if (i > 0) ImGui.SameLine();
                bool isCurrent = i == wizardStep;
                bool isDone = i < wizardStep;
                Vector4 color = isCurrent ? ThemeManager.Accent : isDone ? ThemeManager.AccentMuted : ThemeManager.FontMuted;
                ImGui.TextColored(color, isDone ? $"[{stepNames[i]}]" : isCurrent ? $"> {stepNames[i]}" : stepNames[i]);
            }
            ImGui.Spacing();
            ThemeManager.GradientSeparator();
            ImGui.Spacing();

            // Point assignment mode takes over the UI
            if (assigningPoints && assigningSheet != null && selectedSystem != null)
            {
                DrawPointAssignment();
                return;
            }

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

        // ── Step 0: System Selection ──
        private static void DrawSystemSelection()
        {
            ThemeManager.SectionHeader("Import a System");
            ImGui.Spacing();

            ImGui.Text("Share Code:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150);
            ImGui.InputText("##importCode", ref importCode, 32);
            ImGui.SameLine();
            if (ThemeManager.PillButton("Import##importSystem"))
            {
                if (!string.IsNullOrWhiteSpace(importCode) && Plugin.character != null)
                    Networking.DataSender.ImportSystemByCode(Plugin.character, importCode.Trim());
            }

            ImGui.Spacing();
            ImGui.Spacing();

            if (availableSystems.Count == 0)
            {
                ImGui.TextColored(ThemeManager.FontMuted, "No systems available. Enter a share code above or create one in Manage Systems.");
                return;
            }

            ThemeManager.SectionHeader("Available Systems");
            ImGui.Spacing();

            // System cards
            float cardWidth = 280f;
            float cardHeight = 150f;
            float cardSpacing = 10f;
            float windowWidth = ImGui.GetContentRegionAvail().X;
            int cardCols = Math.Max(1, (int)(windowWidth / (cardWidth + cardSpacing)));

            var drawList = ImGui.GetWindowDrawList();
            Vector2 cardOrigin = ImGui.GetCursorScreenPos();

            for (int i = 0; i < availableSystems.Count; i++)
            {
                var sys = availableSystems[i];
                int col = i % cardCols;
                int row = i / cardCols;
                bool isSelected = selectedSystem != null && selectedSystem.id == sys.id;

                Vector2 cardPos = cardOrigin + new Vector2(col * (cardWidth + cardSpacing), row * (cardHeight + cardSpacing));
                Vector2 cardEnd = cardPos + new Vector2(cardWidth, cardHeight);

                // Card background
                uint bgColor = isSelected
                    ? ImGui.ColorConvertFloat4ToU32(new Vector4(ThemeManager.Accent.X * 0.2f, ThemeManager.Accent.Y * 0.2f, ThemeManager.Accent.Z * 0.2f, 1f))
                    : ImGui.ColorConvertFloat4ToU32(ThemeManager.BgLighter);
                drawList.AddRectFilled(cardPos, cardEnd, bgColor, 6f);

                // Banner (top half of card)
                float bannerHeight = 50f;
                if (sys.bannerTexture != null && sys.bannerTexture.Handle != IntPtr.Zero)
                {
                    // Center-crop UVs to avoid stretching
                    float imgAspect = (float)sys.bannerTexture.Width / sys.bannerTexture.Height;
                    float slotAspect = cardWidth / bannerHeight;
                    Vector2 uv0 = Vector2.Zero, uv1 = Vector2.One;
                    if (imgAspect > slotAspect)
                    {
                        // Image is wider than slot — crop sides
                        float visibleFrac = slotAspect / imgAspect;
                        float offset = (1f - visibleFrac) / 2f;
                        uv0 = new Vector2(offset, 0);
                        uv1 = new Vector2(1f - offset, 1);
                    }
                    else
                    {
                        // Image is taller than slot — crop top/bottom
                        float visibleFrac = imgAspect / slotAspect;
                        float offset = (1f - visibleFrac) / 2f;
                        uv0 = new Vector2(0, offset);
                        uv1 = new Vector2(1, 1f - offset);
                    }
                    drawList.AddImageRounded(sys.bannerTexture.Handle, cardPos, cardPos + new Vector2(cardWidth, bannerHeight),
                        uv0, uv1, 0xFFFFFFFF, 6f, ImDrawFlags.RoundCornersTop);
                }
                else
                {
                    uint bannerColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.15f, 0.15f, 0.2f, 1f));
                    drawList.AddRectFilled(cardPos, cardPos + new Vector2(cardWidth, bannerHeight), bannerColor, 6f, ImDrawFlags.RoundCornersTop);
                }

                // Logo (overlapping banner/content boundary)
                float logoSize = 36f;
                Vector2 logoPos = cardPos + new Vector2(10, bannerHeight - logoSize / 2);
                if (sys.logoTexture != null && sys.logoTexture.Handle != IntPtr.Zero)
                {
                    drawList.AddImageRounded(sys.logoTexture.Handle, logoPos, logoPos + new Vector2(logoSize, logoSize),
                        new Vector2(0, 0), new Vector2(1, 1), 0xFFFFFFFF, logoSize / 2);
                    drawList.AddCircle(logoPos + new Vector2(logoSize / 2, logoSize / 2), logoSize / 2 + 1,
                        ImGui.ColorConvertFloat4ToU32(ThemeManager.AccentMuted), 24, 2f);
                }

                // System name
                float textStartX = sys.logoTexture != null ? logoPos.X + logoSize + 8 : cardPos.X + 10;
                float nameY = cardPos.Y + bannerHeight + 6;
                drawList.AddText(new Vector2(textStartX, nameY), 0xFFFFFFFF, sys.name);

                // Description (truncated)
                if (!string.IsNullOrEmpty(sys.description))
                {
                    string desc = sys.description.Length > 60 ? sys.description[..57] + "..." : sys.description;
                    drawList.AddText(new Vector2(cardPos.X + 10, nameY + 18),
                        ImGui.ColorConvertFloat4ToU32(ThemeManager.FontMuted), desc);
                }

                // Alert badge for unspent points
                string myCharName = Plugin.character?.characterName ?? "";
                var mySheet = Roster.Roster.sheets.FirstOrDefault(s => s.characterName == myCharName && s.systemId == sys.id && s.status == 1);
                if (mySheet == null)
                    mySheet = Roster.Roster.sheets.FirstOrDefault(s => s.characterName == myCharName && s.status == 1);
                int unspentStat = mySheet != null ? mySheet.bonusStatPoints : 0;
                int unspentSkill = mySheet != null ? mySheet.bonusSkillPoints : 0;
                if (unspentStat > 0 || unspentSkill > 0)
                {
                    // Orange notification circle at top-right
                    Vector2 badgeCenter = cardPos + new Vector2(cardWidth - 14, 14);
                    uint badgeColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.6f, 0.1f, 1f));
                    drawList.AddCircleFilled(badgeCenter, 10, badgeColor);
                    string badgeText = $"{unspentStat + unspentSkill}";
                    var badgeTextSize = ImGui.CalcTextSize(badgeText);
                    drawList.AddText(badgeCenter - badgeTextSize / 2, 0xFFFFFFFF, badgeText);
                }

                // Border
                uint borderColor = isSelected
                    ? ImGui.ColorConvertFloat4ToU32(ThemeManager.Accent)
                    : ImGui.ColorConvertFloat4ToU32(new Vector4(0.3f, 0.3f, 0.3f, 0.4f));
                drawList.AddRect(cardPos, cardEnd, borderColor, 6f, ImDrawFlags.None, isSelected ? 2f : 1f);

                // Click to select (top area only - leave room for buttons at bottom)
                float btnAreaHeight = 28f;
                ImGui.SetCursorScreenPos(cardPos);
                if (ImGui.InvisibleButton($"##sysCard_{sys.id}", new Vector2(cardWidth, cardHeight - btnAreaHeight)))
                {
                    selectedSystemIndex = i;
                    selectedSystem = sys;
                }
                if (ImGui.IsItemHovered())
                {
                    uint hoverColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 0.05f));
                    drawList.AddRectFilled(cardPos, cardEnd, hoverColor, 6f);
                }

                // Buttons at bottom of card
                float btnY = cardEnd.Y - btnAreaHeight - 4;
                float btnX = cardPos.X + 6;
                ImGui.SetCursorScreenPos(new Vector2(btnX, btnY));
                ImGui.PushID($"##cardBtns_{sys.id}");
                if (ThemeManager.PillButton("Create##create"))
                {
                    selectedSystemIndex = i;
                    selectedSystem = sys;
                    selectedProfileIndex = -1;
                    profileAvatarsFetched = false;
                    if (sys.SkillClasses.Count == 0 && sys.id > 0 && Plugin.character != null)
                        Networking.DataSender.FetchSystem(Plugin.character, sys.id);
                    wizardStep = 1;
                }
                ImGui.SameLine();
                if (ThemeManager.GhostButton("Roster##roster"))
                {
                    selectedSystemIndex = i;
                    selectedSystem = sys;
                    SystemsWindow.showRosterPanel = !SystemsWindow.showRosterPanel;
                    if (SystemsWindow.showRosterPanel)
                    {
                        Roster.Roster.ResetForSystem();
                        if (sys.SkillClasses.Count == 0 && sys.id > 0 && Plugin.character != null)
                            Networking.DataSender.FetchSystem(Plugin.character, sys.id);
                    }
                }

                // Assign points button (when user has unspent points)
                if (unspentStat > 0 || unspentSkill > 0)
                {
                    ImGui.SameLine();
                    if (ThemeManager.PillButton("Assign##assign"))
                    {
                        selectedSystemIndex = i;
                        selectedSystem = sys;
                        assigningPoints = true;
                        assigningSheet = mySheet;
                        // Load current stats into assignment allocations
                        assignStatAllocations.Clear();
                        foreach (var kvp in sys.StatsData)
                            assignStatAllocations[kvp.Key] = Roster.Roster.GetSheetStatValue(sys, mySheet, kvp.Value.id);
                        // Load current skills
                        assignSelectedSkills = new List<int>(mySheet.learnedSkills);
                        assignSkillTiers.Clear();
                        foreach (var skId in mySheet.learnedSkills)
                        {
                            var sk = sys.Skills.FirstOrDefault(s => s.id == skId);
                            assignSkillTiers[skId] = sk != null ? sk.maxTiers : 1;
                        }
                        assignSkillPointsUsed = assignSkillTiers.Values.Sum();
                        if (sys.SkillClasses.Count == 0 && sys.id > 0 && Plugin.character != null)
                            Networking.DataSender.FetchSystem(Plugin.character, sys.id);
                    }
                }

                // Leave button for non-owners
                bool isOwner = SystemsWindow.systemData.Exists(s => s.id == sys.id);
                if (!isOwner)
                {
                    ImGui.SameLine();
                    if (ThemeManager.DangerButton("Leave##leave"))
                    {
                        availableSystems.RemoveAll(s => s.id == sys.id);
                        if (selectedSystem != null && selectedSystem.id == sys.id)
                        {
                            selectedSystem = null;
                            selectedSystemIndex = -1;
                            SystemsWindow.showRosterPanel = false;
                        }
                    }
                }
                ImGui.PopID();
            }

            // Reserve space
            int cardRows = (availableSystems.Count + cardCols - 1) / cardCols;
            ImGui.SetCursorScreenPos(cardOrigin + new Vector2(0, cardRows * (cardHeight + cardSpacing) + cardSpacing));

            // Show rules if a system is selected
            if (selectedSystem != null && !string.IsNullOrEmpty(selectedSystem.rules))
            {
                ImGui.Spacing();
                ThemeManager.SubtitleText("Rules:");
                ImGui.TextWrapped(selectedSystem.rules);
            }

            // Show user's own submissions for this system
            if (selectedSystem != null && Roster.Roster.sheets.Count > 0)
            {
                // Filter to current user's sheets (match by character name)
                string myName = Plugin.character?.characterName ?? "";
                var mySheets = Roster.Roster.sheets.Where(s => s.characterName == myName).ToList();
                if (mySheets.Count > 0)
                {
                    ImGui.Spacing();
                    ThemeManager.SubtitleText("Your Submissions:");
                    ImGui.Spacing();
                    foreach (var sheet in mySheets)
                    {
                        string[] statusLabels = { "Pending", "Approved", "Declined", "Revision Requested" };
                        Vector4[] statusCols = { new Vector4(1, 0.8f, 0.2f, 1), new Vector4(0.3f, 1, 0.3f, 1),
                            new Vector4(1, 0.3f, 0.3f, 1), new Vector4(0.6f, 0.6f, 1, 1) };
                        int st = Math.Clamp(sheet.status, 0, 3);
                        ImGui.TextColored(statusCols[st], $"[{statusLabels[st]}]");
                        ImGui.SameLine();
                        string className = "No Class";
                        if (sheet.classId >= 0)
                        {
                            var cls = selectedSystem.SkillClasses.FirstOrDefault(c => c.id == sheet.classId);
                            if (cls != null) className = cls.name;
                        }
                        ImGui.Text($"{className} - Lv.{sheet.level}");

                        if (sheet.status == 3)
                        {
                            if (!string.IsNullOrEmpty(sheet.revisionReason))
                            {
                                ImGui.Indent();
                                ImGui.TextColored(new Vector4(0.6f, 0.6f, 1, 1), $"Reason: {sheet.revisionReason}");
                                ImGui.Unindent();
                            }
                            ImGui.SameLine();
                            if (ThemeManager.PillButton($"Revise##revise{sheet.id}"))
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

                                // Pre-fill stats — convert from stat ID keys to sort index keys
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
                                    Networking.DataSender.FetchSystem(Plugin.character, selectedSystem.id);

                                // Skip to rules step (profile already selected)
                                wizardStep = 2;
                            }
                        }
                    }
                    ImGui.Spacing();
                }
            }

        }

        // Profile avatar fetch tracking
        private static bool profileAvatarsFetched = false;

        // ── Step 1: Profile Selection ──
        private static void DrawProfileSelection()
        {
            if (selectedSystem == null) { wizardStep = 0; return; }

            if (ThemeManager.GhostButton("< Back##backToSystem"))
            { wizardStep = 0; return; }

            ImGui.SameLine();
            ThemeManager.SectionHeader("Choose Your Profile");
            ImGui.Spacing();

            var profiles = ProfileWindow.profiles;

            // Trigger avatar loading for profiles that don't have one yet
            if (!profileAvatarsFetched && profiles.Count > 0 && Plugin.character != null)
            {
                profileAvatarsFetched = true;
                foreach (var prof in profiles)
                {
                    if (prof.avatar == null && prof.id > 0)
                    {
                        // Fetch the full profile which includes avatar bytes
                        Networking.DataSender.FetchProfile(Plugin.character, true, prof.index,
                            prof.playerName ?? "", prof.playerWorld ?? "", prof.id);
                    }
                }
            }
            if (profiles == null || profiles.Count == 0)
            {
                ImGui.TextColored(ThemeManager.FontMuted, "No profiles found. Create a profile first.");
                return;
            }

            for (int i = 0; i < profiles.Count; i++)
            {
                var prof = profiles[i];
                bool isSelected = i == selectedProfileIndex;

                ImGui.PushID($"prof_{i}");

                // Avatar thumbnail
                if (prof.avatar != null && prof.avatar.Handle != IntPtr.Zero)
                {
                    ImGui.Image(prof.avatar.Handle, new Vector2(32, 32));
                    ImGui.SameLine();
                }

                string label = !string.IsNullOrEmpty(prof.title) ? prof.title : $"Profile {i + 1}";
                if (!string.IsNullOrEmpty(prof.playerName))
                    label = $"{prof.playerName} - {label}";

                if (ImGui.Selectable(label, isSelected, ImGuiSelectableFlags.None, new Vector2(0, 32)))
                    selectedProfileIndex = i;

                ImGui.PopID();
            }

            ImGui.Spacing();
            if (selectedProfileIndex >= 0 && selectedProfileIndex < profiles.Count)
            {
                if (ThemeManager.PillButton("Next##nextStep2"))
                {
                    wizardStep = 2;
                }
            }
        }

        // ── Point Assignment Mode ──
        private static void DrawPointAssignment()
        {
            var sheet = assigningSheet;
            var system = selectedSystem;

            if (ThemeManager.GhostButton("< Back##backFromAssign"))
            {
                assigningPoints = false;
                assigningSheet = null;
                return;
            }

            ImGui.SameLine();
            ThemeManager.SectionHeader("Assign Bonus Points");
            ImGui.Spacing();

            if (ImGui.BeginTabBar("##AssignTabs"))
            {
                // Stat assignment tab
                if (sheet.bonusStatPoints > 0 && ImGui.BeginTabItem($"Stats ({sheet.bonusStatPoints} pts)"))
                {
                    ImGui.Spacing();
                    int originalSpent = 0;
                    foreach (var kvp in system.StatsData)
                        originalSpent += Roster.Roster.GetSheetStatValue(system, sheet, kvp.Value.id);
                    int currentSpent = assignStatAllocations.Values.Sum();
                    int newPointsUsed = currentSpent - originalSpent;
                    int remaining = sheet.bonusStatPoints - newPointsUsed;

                    ImGui.Text("Bonus Stat Points: ");
                    ImGui.SameLine();
                    Vector4 spColor = remaining > 0 ? ThemeManager.Accent : ThemeManager.FontMuted;
                    ImGui.TextColored(spColor, $"{remaining} / {sheet.bonusStatPoints}");
                    ImGui.Spacing();

                    foreach (var kvp in system.StatsData)
                    {
                        var stat = kvp.Value;
                        int key = kvp.Key;
                        if (!assignStatAllocations.ContainsKey(key))
                            assignStatAllocations[key] = 0;
                        int val = assignStatAllocations[key];

                        ImGui.PushID($"astat_{key}");
                        ImGui.ColorButton("##c", stat.color, ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoPicker, new Vector2(12, 24));
                        ImGui.SameLine();
                        ImGui.Text(stat.name);
                        ImGui.SameLine(200);

                        if (ThemeManager.GhostButton("-##dec"))
                        {
                            int origVal = Roster.Roster.GetSheetStatValue(system, sheet, stat.id);
                            if (val > origVal) // Can't go below original
                                assignStatAllocations[key] = val - 1;
                        }
                        ImGui.SameLine();
                        ImGui.Text($"{val}");
                        ImGui.SameLine();

                        bool canAdd = val < stat.baseMax && remaining > 0;
                        if (!canAdd) ImGui.BeginDisabled();
                        if (ThemeManager.GhostButton("+##inc"))
                            assignStatAllocations[key] = val + 1;
                        if (!canAdd) ImGui.EndDisabled();

                        ImGui.PopID();
                    }

                    ImGui.Spacing();
                    if (ThemeManager.PillButton("Save Stats##saveAssignStats"))
                    {
                        if (Plugin.character != null)
                        {
                            // Convert to stat ID keys
                            var statsByStatId = new Dictionary<int, int>();
                            foreach (var kvp in assignStatAllocations)
                            {
                                if (system.StatsData.ContainsKey(kvp.Key))
                                    statsByStatId[system.StatsData[kvp.Key].id] = kvp.Value;
                            }
                            // Update sheet locally
                            sheet.statValues = statsByStatId;
                            sheet.bonusStatPoints = Math.Max(0, sheet.bonusStatPoints - newPointsUsed);
                            // Save to server
                            Networking.DataSender.SubmitCharacterSheet(Plugin.character, system.id, sheet.classId,
                                statsByStatId, sheet.learnedSkills, sheet.profileId);
                            Networking.DataSender.UpdateSheetLevelPoints(Plugin.character, sheet.id, sheet.level, sheet.bonusSkillPoints, sheet.bonusStatPoints);
                        }
                    }
                    ImGui.EndTabItem();
                }

                // Skill assignment tab
                if (sheet.bonusSkillPoints > 0 && ImGui.BeginTabItem($"Skills ({sheet.bonusSkillPoints} pts)"))
                {
                    ImGui.Spacing();
                    int originalSkillCount = sheet.learnedSkills.Count;
                    int newSkillsAdded = assignSkillPointsUsed - originalSkillCount;
                    int remainingSkill = sheet.bonusSkillPoints - Math.Max(0, newSkillsAdded);

                    ImGui.Text("Bonus Skill Points: ");
                    ImGui.SameLine();
                    Vector4 skColor = remainingSkill > 0 ? ThemeManager.Accent : ThemeManager.FontMuted;
                    ImGui.TextColored(skColor, $"{remainingSkill} / {sheet.bonusSkillPoints}");
                    ImGui.Spacing();

                    if (sheet.classId >= 0)
                    {
                        var cls = system.SkillClasses.FirstOrDefault(c => c.id == sheet.classId);
                        if (cls != null)
                        {
                            // Reuse the skill tree picker but with assign state
                            // Temporarily swap the global state
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
                            DrawSkillTreePicker(system, tempCls);

                            // Save back
                            assignSelectedSkills = selectedSkills;
                            assignSkillTiers = skillTiers;
                            assignSkillPointsUsed = skillPointsUsed;
                            selectedSkills = savedSkills;
                            skillTiers = savedTiers;
                            skillPointsUsed = savedUsed;

                            ImGui.Spacing();
                            if (ThemeManager.PillButton("Save Skills##saveAssignSkills"))
                            {
                                if (Plugin.character != null)
                                {
                                    sheet.learnedSkills = new List<int>(assignSelectedSkills);
                                    int skillsAdded = assignSelectedSkills.Count - originalSkillCount;
                                    sheet.bonusSkillPoints = Math.Max(0, sheet.bonusSkillPoints - Math.Max(0, skillsAdded));
                                    Networking.DataSender.SubmitCharacterSheet(Plugin.character, system.id, sheet.classId,
                                        sheet.statValues, assignSelectedSkills, sheet.profileId);
                                    Networking.DataSender.UpdateSheetLevelPoints(Plugin.character, sheet.id, sheet.level, sheet.bonusSkillPoints, sheet.bonusStatPoints);
                                }
                            }
                        }
                    }
                    else
                    {
                        ImGui.TextColored(ThemeManager.FontMuted, "No class assigned.");
                    }
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }

        // ── Step 2: Rules ──
        private static void DrawRulesStep()
        {
            if (selectedSystem == null) { wizardStep = 0; return; }

            if (ThemeManager.GhostButton("< Back##backToProfile"))
            { wizardStep = 1; return; }

            ImGui.SameLine();
            ThemeManager.SectionHeader("System Rules");
            ImGui.Spacing();

            if (!string.IsNullOrEmpty(selectedSystem.rules))
            {
                ImGui.TextWrapped(selectedSystem.rules);
            }
            else
            {
                ImGui.TextColored(ThemeManager.FontMuted, "This system has no rules defined.");
            }

            ImGui.Spacing();
            ImGui.Spacing();

            if (ThemeManager.PillButton("Continue##continueFromRules"))
            {
                selectedClassIndex = -1;
                hoveredClassIndex = -1;
                selectedSkills.Clear();
                skillTiers.Clear();
                skillPointsUsed = 0;
                previewTreeIndex = 0;
                // Ensure full system data is loaded
                if (selectedSystem.SkillClasses.Count == 0 && selectedSystem.id > 0 && Plugin.character != null)
                    Networking.DataSender.FetchSystem(Plugin.character, selectedSystem.id);
                wizardStep = 3;
            }
        }

        // ── Step 3: Class + Skill Selection ──
        private static void DrawClassAndSkillSelection()
        {
            if (selectedSystem == null) { wizardStep = 0; return; }

            if (ThemeManager.GhostButton("< Back##backToRules"))
            { wizardStep = 2; return; }

            ImGui.SameLine();
            ThemeManager.SectionHeader("Choose Your Class");
            ImGui.Spacing();

            var classes = selectedSystem.SkillClasses;
            if (classes.Count == 0)
            {
                ImGui.TextColored(ThemeManager.FontMuted, "This system has no classes defined.");
                if (ThemeManager.PillButton("Skip to Stats##skipClass"))
                {
                    selectedClassIndex = -1;
                    InitStatAllocations();
                    wizardStep = 4;
                }
                return;
            }

            // Class icon grid
            float iconSize = 64f;
            float spacing = 12f;
            float availWidth = ImGui.GetContentRegionAvail().X * 0.55f;
            int cols = Math.Max(1, (int)(availWidth / (iconSize + spacing)));

            // Left panel: class icons + skill tree
            ImGui.BeginChild("##classGrid", new Vector2(availWidth, 0), false);
            var drawList = ImGui.GetWindowDrawList();
            Vector2 cursor = ImGui.GetCursorScreenPos();

            for (int i = 0; i < classes.Count; i++)
            {
                var cls = classes[i];
                int col = i % cols;
                int row = i / cols;
                Vector2 center = cursor + new Vector2(col * (iconSize + spacing) + iconSize / 2, row * (iconSize + spacing) + iconSize / 2);
                float octRadius = iconSize / 2 - 2;
                var octPoints = Skills.Skills.GetOctagonPoints(center, octRadius);

                bool isSelected = i == selectedClassIndex;

                if (cls.iconTexture != null && cls.iconTexture.Handle != IntPtr.Zero)
                {
                    Vector2 imgMin = center - new Vector2(octRadius, octRadius);
                    Vector2 imgMax = center + new Vector2(octRadius, octRadius);
                    drawList.AddImage(cls.iconTexture.Handle, imgMin, imgMax);
                    Skills.Skills.MaskSquareToOctagon(drawList, center, octRadius, octPoints);
                }
                else
                {
                    uint fillColor = isSelected
                        ? ImGui.ColorConvertFloat4ToU32(ThemeManager.Accent)
                        : ImGui.ColorConvertFloat4ToU32(ThemeManager.BgLighter);
                    Skills.Skills.DrawFilledOctagon(drawList, octPoints, fillColor);
                    string label = cls.name.Length > 5 ? cls.name[..5] + ".." : cls.name;
                    var textSize = ImGui.CalcTextSize(label);
                    drawList.AddText(center - textSize / 2, 0xFFFFFFFF, label);
                }

                uint borderColor = isSelected
                    ? ImGui.ColorConvertFloat4ToU32(ThemeManager.Accent)
                    : ImGui.ColorConvertFloat4ToU32(ThemeManager.AccentMuted);
                drawList.AddPolyline(ref octPoints[0], octPoints.Length, borderColor, ImDrawFlags.Closed, isSelected ? 3f : 1.5f);

                ImGui.SetCursorScreenPos(center - new Vector2(octRadius, octRadius));
                if (ImGui.InvisibleButton($"##cls_{i}", new Vector2(octRadius * 2, octRadius * 2)))
                {
                    if (selectedClassIndex != i)
                    {
                        selectedClassIndex = i;
                        previewTreeIndex = 0;
                        selectedSkills.Clear();
                        skillPointsUsed = 0;
                    }
                }
                if (ImGui.IsItemHovered())
                {
                    hoveredClassIndex = i;
                    ImGui.BeginTooltip();
                            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20f);
                    ImGui.TextColored(ThemeManager.Accent, cls.name);
                    if (!string.IsNullOrEmpty(cls.description))
                        ImGui.TextWrapped(cls.description);
                    if (cls.initialSkillPoints > 0)
                        ImGui.Text($"Skill Points: {cls.initialSkillPoints}");
                    ImGui.PopTextWrapPos();
                            ImGui.EndTooltip();
                }
            }

            // Reserve space for class icons
            int totalRows = (classes.Count + cols - 1) / cols;
            ImGui.SetCursorScreenPos(cursor + new Vector2(0, totalRows * (iconSize + spacing) + spacing));

            // Skill tree selection (pick skills from tree)
            if (selectedClassIndex >= 0 && selectedClassIndex < classes.Count)
            {
                var cls = classes[selectedClassIndex];
                int maxSkillPoints = cls.initialSkillPoints;

                ImGui.Spacing();
                ThemeManager.GradientSeparator();
                ImGui.Spacing();

                if (maxSkillPoints > 0)
                {
                    int remaining = maxSkillPoints - skillPointsUsed;
                    ImGui.Text("Skill Points: ");
                    ImGui.SameLine();
                    Vector4 spColor = remaining > 0 ? ThemeManager.Accent : ThemeManager.FontMuted;
                    ImGui.TextColored(spColor, $"{remaining} / {maxSkillPoints}");
                    ImGui.Spacing();
                }

                DrawSkillTreePicker(selectedSystem, cls);
            }

            ImGui.EndChild();

            // Right panel: passives
            ImGui.SameLine();
            ImGui.BeginChild("##passivePanel", Vector2.Zero, false);
            int previewIdx = selectedClassIndex >= 0 ? selectedClassIndex : hoveredClassIndex;
            if (previewIdx >= 0 && previewIdx < classes.Count)
            {
                var cls = classes[previewIdx];
                ThemeManager.SubtitleText($"{cls.name} - Passives");
                ImGui.Spacing();

                var passives = selectedSystem.Skills.Where(s => s.classId == cls.id && !s.isCastable).ToList();
                if (passives.Count == 0)
                {
                    ImGui.TextColored(ThemeManager.FontMuted, "No passives.");
                }
                else
                {
                    foreach (var passive in passives)
                    {
                        if (passive.iconTexture != null && passive.iconTexture.Handle != IntPtr.Zero)
                        {
                            ImGui.Image(passive.iconTexture.Handle, new Vector2(24, 24));
                            ImGui.SameLine();
                        }
                        ImGui.Text(passive.name);
                        if (!string.IsNullOrEmpty(passive.description))
                        {
                            ImGui.Indent(28);
                            ImGui.TextColored(ThemeManager.FontMuted, passive.description);
                            ImGui.Unindent(28);
                        }
                    }
                }
            }
            ImGui.EndChild();

            ImGui.Spacing();
            if (selectedClassIndex >= 0)
            {
                if (ThemeManager.PillButton("Next: Assign Stats##nextStep3"))
                {
                    InitStatAllocations();
                    wizardStep = 4;
                }
            }
        }

        // ── Skill Tree Picker (interactive, used during class selection) ──
        /// <summary>
        /// Helper: get current tier invested in a skill (0 if none).
        /// </summary>
        private static int GetSkillTier(int skillId) => skillTiers.ContainsKey(skillId) ? skillTiers[skillId] : 0;

        /// <summary>
        /// Helper: check if a skill is fully maxed (all tiers invested).
        /// </summary>
        private static bool IsSkillMaxed(SkillData skill) => GetSkillTier(skill.id) >= skill.maxTiers;

        /// <summary>
        /// Check if all prerequisites for a skill are satisfied (parent skills fully maxed).
        /// </summary>
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

        /// <summary>
        /// Rebuild the selectedSkills list from skillTiers (skills with tier >= 1).
        /// </summary>
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
            int maxSkillPoints = cls.initialSkillPoints;

            // Tree tabs
            if (cls.SkillTrees.Count > 0)
            {
                if (previewTreeIndex >= cls.SkillTrees.Count)
                    previewTreeIndex = 0;

                if (ImGui.BeginTabBar("##PickerTreeTabs"))
                {
                    for (int t = 0; t < cls.SkillTrees.Count; t++)
                    {
                        if (ImGui.BeginTabItem(cls.SkillTrees[t].name + $"##ptab{t}"))
                        {
                            previewTreeIndex = t;
                            ImGui.EndTabItem();
                        }
                    }
                    ImGui.EndTabBar();
                }
            }

            ImGui.TextColored(ThemeManager.FontMuted, "Left-click: add tier | Right-click: remove tier");
            ImGui.Spacing();

            float gridWidth = ImGui.GetContentRegionAvail().X;
            float cellSize = Math.Min((gridWidth - 20) / PreviewGridCols, 64f);
            float octRadius = cellSize * 0.35f;

            var drawList = ImGui.GetWindowDrawList();
            Vector2 origin = ImGui.GetCursorScreenPos();

            var treeSkills = system.Skills.Where(s => s.classId == classId && s.treeIndex == previewTreeIndex && s.isCastable).ToList();

            // Draw connections
            foreach (var conn in system.SkillConnections)
            {
                var fromSkill = treeSkills.FirstOrDefault(s => s.id == conn.fromSkillId);
                var toSkill = treeSkills.FirstOrDefault(s => s.id == conn.toSkillId);
                if (fromSkill != null && toSkill != null)
                {
                    // Color connection based on whether parent is maxed
                    bool parentMaxed = IsSkillMaxed(fromSkill);
                    Vector4 lineVec = parentMaxed ? ThemeManager.Accent : ThemeManager.AccentMuted;
                    lineVec.W = parentMaxed ? 0.8f : 0.4f;
                    uint lineColor = ImGui.ColorConvertFloat4ToU32(lineVec);

                    Vector2 from = origin + new Vector2(fromSkill.gridX * cellSize + cellSize / 2, fromSkill.gridY * cellSize + cellSize / 2);
                    Vector2 to = origin + new Vector2(toSkill.gridX * cellSize + cellSize / 2, toSkill.gridY * cellSize + cellSize / 2);
                    drawList.AddLine(from, to, lineColor, 2f);

                    Vector2 mid = (from + to) / 2;
                    Vector2 dir = Vector2.Normalize(to - from);
                    Vector2 perp = new Vector2(-dir.Y, dir.X);
                    float arrowSize = 5f;
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
                            Vector2 imgMin = center - new Vector2(octRadius, octRadius);
                            Vector2 imgMax = center + new Vector2(octRadius, octRadius);
                            var tintColor = new Vector4(alpha, alpha, alpha, 1f);
                            drawList.AddImage(skill.iconTexture.Handle, imgMin, imgMax,
                                new Vector2(0, 0), new Vector2(1, 1), ImGui.ColorConvertFloat4ToU32(tintColor));
                            Skills.Skills.MaskSquareToOctagon(drawList, center, octRadius, octPoints);
                        }
                        else
                        {
                            uint fillColor = isMaxed
                                ? ImGui.ColorConvertFloat4ToU32(ThemeManager.Accent)
                                : hasAnyTier
                                    ? ImGui.ColorConvertFloat4ToU32(new Vector4(ThemeManager.Accent.X * 0.6f, ThemeManager.Accent.Y * 0.6f, ThemeManager.Accent.Z * 0.6f, 0.8f))
                                    : ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 0.2f, 0.2f, 0.6f));
                            Skills.Skills.DrawFilledOctagon(drawList, octPoints, fillColor);
                            string label = skill.name.Length > 6 ? skill.name[..6] + ".." : skill.name;
                            var textSize = ImGui.CalcTextSize(label);
                            uint textCol = hasAnyTier ? 0xFFFFFFFF : 0x66FFFFFF;
                            drawList.AddText(center - textSize / 2, textCol, label);
                        }

                        uint borderColor = isMaxed
                            ? ImGui.ColorConvertFloat4ToU32(ThemeManager.Accent)
                            : hasAnyTier
                                ? ImGui.ColorConvertFloat4ToU32(new Vector4(ThemeManager.Accent.X, ThemeManager.Accent.Y, ThemeManager.Accent.Z, 0.6f))
                                : ImGui.ColorConvertFloat4ToU32(new Vector4(0.4f, 0.4f, 0.4f, 0.5f));
                        drawList.AddPolyline(ref octPoints[0], octPoints.Length, borderColor, ImDrawFlags.Closed, isMaxed ? 2.5f : 1.5f);

                        // Tier counter (bottom-right) — always show for multi-tier skills
                        if (skill.maxTiers > 1)
                        {
                            string tierLabel = $"{currentTier}/{skill.maxTiers}";
                            var tierSize = ImGui.CalcTextSize(tierLabel);
                            Vector2 tierPos = center + new Vector2(octRadius * 0.5f - tierSize.X / 2, octRadius * 0.55f);
                            // Black shadow
                            drawList.AddText(tierPos + new Vector2(1, 1), 0xFF000000, tierLabel);
                            // Colored text: green if maxed, yellow if partial, gray if none
                            uint tierCol = isMaxed ? 0xFF00FF00 : hasAnyTier ? 0xFF00CCFF : 0xFFAAAAAA;
                            drawList.AddText(tierPos, tierCol, tierLabel);
                        }

                        // Click handling — invisible button covers the octagon
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
                            ImGui.BeginTooltip();
                            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20f);
                            ImGui.TextColored(ThemeManager.Accent, skill.name);
                            if (skill.maxTiers > 1)
                                ImGui.Text($"Tier: {currentTier} / {skill.maxTiers}");
                            if (!string.IsNullOrEmpty(skill.description))
                                ImGui.TextWrapped(skill.description);
                            if (skill.cooldownTurns > 0)
                                ImGui.Text($"Cooldown: {skill.cooldownTurns} turns");
                            if (skill.resourceCost > 0)
                                ImGui.Text($"Cost: {skill.resourceCost}");
                            if (!isMaxed)
                            {
                                bool met = ArePrereqsMet(system, skill.id);
                                var prereqs = system.SkillConnections.Where(c => c.toSkillId == skill.id).ToList();
                                if (prereqs.Count > 0)
                                {
                                    ImGui.TextColored(met ? new Vector4(0.3f, 1f, 0.3f, 1f) : new Vector4(1f, 0.4f, 0.4f, 1f),
                                        met ? "Prerequisites met" : "Prerequisites not met — parent skills must be maxed");
                                }
                            }
                            else
                            {
                                ImGui.TextColored(new Vector4(0.3f, 1f, 0.3f, 1f), "Maxed!");
                            }
                            ImGui.PopTextWrapPos();
                            ImGui.EndTooltip();
                        }
                    }
                    else
                    {
                        uint outlineColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.3f, 0.3f, 0.3f, 0.2f));
                        drawList.AddPolyline(ref octPoints[0], octPoints.Length, outlineColor, ImDrawFlags.Closed, 1f);
                    }
                }
            }

            ImGui.SetCursorScreenPos(origin + new Vector2(0, PreviewGridRows * cellSize + 10));
            if (treeSkills.Count == 0)
                ImGui.TextColored(ThemeManager.FontMuted, "No skills in this tree.");
        }

        // ── Step 3: Stat Assignment ──
        private static void InitStatAllocations()
        {
            statAllocations.Clear();
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

            if (ThemeManager.GhostButton("< Back##backToClass"))
            { wizardStep = 3; return; }

            ImGui.SameLine();
            ThemeManager.SectionHeader("Assign Stat Points");
            ImGui.Spacing();

            int totalBudget = selectedSystem.basePointsAvailable;
            int spent = statAllocations.Values.Sum();
            int remaining = totalBudget - spent;

            ImGui.Text($"Points Remaining: ");
            ImGui.SameLine();
            Vector4 remainColor = remaining > 0 ? ThemeManager.Accent : remaining == 0 ? ThemeManager.FontMuted : new Vector4(1, 0.3f, 0.3f, 1);
            ImGui.TextColored(remainColor, $"{remaining} / {totalBudget}");
            ImGui.Spacing();
            ThemeManager.GradientSeparator();
            ImGui.Spacing();

            var stats = selectedSystem.StatsData;
            bool changed = false;

            float panelWidth = ImGui.GetContentRegionAvail().X;
            float controlsWidth = panelWidth * 0.45f;
            float chartWidth = panelWidth * 0.50f;

            ImGui.BeginChild("##statControls", new Vector2(controlsWidth, 0), false);
            foreach (var kvp in stats)
            {
                var stat = kvp.Value;
                int key = kvp.Key;
                if (!statAllocations.ContainsKey(key))
                    statAllocations[key] = 0;

                int val = statAllocations[key];

                ImGui.PushID($"stat_{key}");

                ImGui.ColorButton("##statColor", stat.color, ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoPicker, new Vector2(12, 24));
                ImGui.SameLine();
                ImGui.Text(stat.name);
                // Info tooltip with description
                if (!string.IsNullOrEmpty(stat.description))
                {
                    ImGui.SameLine();
                    ImGui.TextColored(ThemeManager.FontMuted, "(?)");
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20f);
                        ImGui.TextColored(ThemeManager.Accent, stat.name);
                        ImGui.TextWrapped(stat.description);
                        ImGui.Text($"Range: {stat.baseMin} - {stat.baseMax}");
                        ImGui.PopTextWrapPos();
                            ImGui.EndTooltip();
                    }
                }
                ImGui.SameLine(controlsWidth - 110);

                bool canRemove = stat.canRemovePoints && (stat.canGoNegative || val > stat.baseMin);
                if (!canRemove) ImGui.BeginDisabled();
                if (ThemeManager.GhostButton("-##dec"))
                {
                    statAllocations[key] = val - 1;
                    changed = true;
                }
                if (!canRemove) ImGui.EndDisabled();

                ImGui.SameLine();
                ImGui.Text($"{val}");
                ImGui.SameLine();

                bool canAdd = stat.canAddPoints && val < stat.baseMax && remaining > 0;
                if (!canAdd) ImGui.BeginDisabled();
                if (ThemeManager.GhostButton("+##inc"))
                {
                    statAllocations[key] = val + 1;
                    changed = true;
                }
                if (!canAdd) ImGui.EndDisabled();

                ImGui.PopID();
            }
            ImGui.EndChild();

            if (changed) TriggerRadarAnimation();

            ImGui.SameLine();
            ImGui.BeginChild("##radarChart", new Vector2(chartWidth, 0), false);
            DrawRadarChart(stats, chartWidth);
            DrawResourceBars(selectedSystem, stats);
            ImGui.EndChild();

            ImGui.Spacing();
            if (ThemeManager.PillButton("Next: Review##nextStep4"))
                wizardStep = 5;
        }

        // ── Step 5: Review & Create ──
        private static void DrawReviewAndCreate()
        {
            if (selectedSystem == null) { wizardStep = 0; return; }

            if (ThemeManager.GhostButton("< Back##backToStats"))
            { wizardStep = 4; return; }

            ImGui.SameLine();
            ThemeManager.SectionHeader("Review Your Character");
            ImGui.Spacing();

            // Profile
            var profiles = ProfileWindow.profiles;
            if (selectedProfileIndex >= 0 && selectedProfileIndex < profiles.Count)
            {
                var prof = profiles[selectedProfileIndex];
                if (prof.avatar != null && prof.avatar.Handle != IntPtr.Zero)
                {
                    float avSize = 48;
                    float centeredX = (ImGui.GetContentRegionAvail().X - avSize) / 2;
                    ImGui.SetCursorPosX(centeredX);
                    ImGui.Image(prof.avatar.Handle, new Vector2(avSize, avSize));
                }
                string profLabel = !string.IsNullOrEmpty(prof.title) ? prof.title : prof.playerName;
                if (!string.IsNullOrEmpty(profLabel))
                {
                    var textSize = ImGui.CalcTextSize(profLabel);
                    ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - textSize.X) / 2);
                    ImGui.TextColored(ThemeManager.Accent, profLabel);
                }
                ImGui.Spacing();
            }

            ImGui.Text("System: ");
            ImGui.SameLine();
            ImGui.TextColored(ThemeManager.Accent, selectedSystem.name);

            if (selectedClassIndex >= 0 && selectedClassIndex < selectedSystem.SkillClasses.Count)
            {
                var cls = selectedSystem.SkillClasses[selectedClassIndex];
                ImGui.Text("Class: ");
                ImGui.SameLine();
                ImGui.TextColored(ThemeManager.Accent, cls.name);
            }

            ImGui.Spacing();
            ThemeManager.GradientSeparator();
            ImGui.Spacing();

            ThemeManager.SubtitleText("Stats:");
            foreach (var kvp in selectedSystem.StatsData)
            {
                if (statAllocations.ContainsKey(kvp.Key))
                {
                    ImGui.ColorButton($"##rc{kvp.Key}", kvp.Value.color, ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoPicker, new Vector2(12, 18));
                    ImGui.SameLine();
                    ImGui.Text($"{kvp.Value.name}: {statAllocations[kvp.Key]}");
                }
            }

            ImGui.Spacing();

            ThemeManager.SubtitleText("Skills:");
            if (selectedSkills.Count == 0)
            {
                ImGui.TextColored(ThemeManager.FontMuted, "None selected.");
            }
            else
            {
                foreach (var skillId in selectedSkills)
                {
                    var skill = selectedSystem.Skills.FirstOrDefault(s => s.id == skillId);
                    if (skill != null) ImGui.BulletText(skill.name);
                }
            }

            ImGui.Spacing();
            ImGui.Spacing();

            if (!string.IsNullOrEmpty(submitMessage))
            {
                Vector4 msgColor = submitSuccess ? new Vector4(0.3f, 1f, 0.3f, 1f) : new Vector4(1f, 0.3f, 0.3f, 1f);
                ImGui.TextColored(msgColor, submitMessage);
                ImGui.Spacing();
            }

            string submitLabel = revisingSheetId > 0 ? "Resubmit Revised Sheet##submit" : "Submit Character Sheet##submit";
            if (ThemeManager.PillButton(submitLabel))
            {
                if (Plugin.character != null)
                {
                    int classId = selectedClassIndex >= 0 && selectedClassIndex < selectedSystem.SkillClasses.Count
                        ? selectedSystem.SkillClasses[selectedClassIndex].id : -1;
                    int profileId = -1;
                    if (selectedProfileIndex >= 0 && selectedProfileIndex < profiles.Count)
                        profileId = profiles[selectedProfileIndex].id;
                    // Convert stat allocations from SortedList keys to stat ID keys
                    var statsByStatId = new Dictionary<int, int>();
                    foreach (var kvp in statAllocations)
                    {
                        // kvp.Key is the SortedList key (sort index) — look up the stat by key
                        if (selectedSystem.StatsData.ContainsKey(kvp.Key))
                        {
                            int statId = selectedSystem.StatsData[kvp.Key].id;
                            statsByStatId[statId] = kvp.Value;
                        }
                    }
                    Networking.DataSender.SubmitCharacterSheet(Plugin.character, selectedSystem.id, classId,
                        statsByStatId, selectedSkills, profileId);
                    revisingSheetId = 0; // Reset revision mode
                }
            }

            if (revisingSheetId > 0)
            {
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 1f, 1f), "Revising an existing submission. Changes will be submitted as a new sheet.");
            }

            if (selectedSystem.requireApproval)
            {
                ImGui.Spacing();
                ImGui.TextColored(ThemeManager.FontMuted, "This system requires owner approval. Your sheet will be reviewed.");
            }
        }

        // ── Radar Chart ──
        private static void DrawRadarChart(SortedList<int, StatData> stats, float availWidth)
        {
            int count = stats.Count;
            if (count < 2) return;

            if (morphProgress < 1f)
            {
                float elapsed = (float)(DateTime.Now - morphStartTime).TotalSeconds;
                morphProgress = Math.Clamp(elapsed / MorphDuration, 0f, 1f);
            }

            float chartRadius = Math.Min(availWidth * 0.42f, 140f);
            Vector2 cursorStart = ImGui.GetCursorScreenPos();
            Vector2 center = cursorStart + new Vector2(availWidth / 2, chartRadius + 20);
            var drawList = ImGui.GetWindowDrawList();
            float angleStep = 2f * MathF.PI / count;
            float startAngle = -MathF.PI / 2f;

            uint ringColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.3f, 0.3f, 0.3f, 0.3f));
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

            int idx = 0;
            foreach (var kvp in stats)
            {
                float angle = startAngle + idx * angleStep;
                Vector2 axisEnd = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * chartRadius;
                drawList.AddLine(center, axisEnd, ringColor, 1f);
                Vector2 labelPos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (chartRadius + 14);
                string label = kvp.Value.name;
                var textSize = ImGui.CalcTextSize(label);
                uint labelColor = ImGui.ColorConvertFloat4ToU32(kvp.Value.color);
                drawList.AddText(labelPos - textSize / 2, labelColor, label);
                idx++;
            }

            var radii = GetCurrentInterpolatedRadii();
            if (radii.Count != count)
                radii = new List<float>(new float[count]);

            var dataPoints = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float r = Math.Max(radii[i], 0.05f) * chartRadius;
                float angle = startAngle + i * angleStep;
                dataPoints[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * r;
            }

            Vector4 fillVec = ThemeManager.Accent;
            fillVec.W = 0.2f;
            uint fillColor2 = ImGui.ColorConvertFloat4ToU32(fillVec);
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                drawList.AddTriangleFilled(center, dataPoints[i], dataPoints[next], fillColor2);
            }

            Vector4 borderVec = ThemeManager.Accent;
            borderVec.W = 0.8f;
            uint borderColor2 = ImGui.ColorConvertFloat4ToU32(borderVec);
            drawList.AddPolyline(ref dataPoints[0], dataPoints.Length, borderColor2, ImDrawFlags.Closed, 2.5f);

            uint dotColor = ImGui.ColorConvertFloat4ToU32(ThemeManager.Accent);
            for (int i = 0; i < count; i++)
            {
                if (radii[i] > 0.01f)
                    drawList.AddCircleFilled(dataPoints[i], 4f, dotColor);
            }

            ImGui.SetCursorScreenPos(cursorStart + new Vector2(0, chartRadius * 2 + 50));
        }

        // ── Resource Bars ──
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
                linkedResources.Add(("Health", new Vector4(0.8f, 0.2f, 0.2f, 1f), combat.healthBase, max, current, bonus));
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

            ImGui.Spacing();
            ThemeManager.GradientSeparator();
            ImGui.Spacing();
            ImGui.TextColored(ThemeManager.Accent, "Resources");
            ImGui.Spacing();

            float barWidth = ImGui.GetContentRegionAvail().X - 10;
            float barHeight = 18f;
            var drawList = ImGui.GetWindowDrawList();

            foreach (var (name, color, baseVal, maxVal, currentVal, bonusVal) in linkedResources)
            {
                string label = bonusVal != 0
                    ? $"{name}: {baseVal} + {bonusVal} = {currentVal} / {maxVal}"
                    : $"{name}: {currentVal} / {maxVal}";
                ImGui.Text(label);

                Vector2 barPos = ImGui.GetCursorScreenPos();
                uint bgColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.15f, 0.15f, 0.15f, 1f));
                drawList.AddRectFilled(barPos, barPos + new Vector2(barWidth, barHeight), bgColor, 4f);

                float baseFill = maxVal > 0 ? Math.Clamp((float)baseVal / maxVal, 0f, 1f) : 0f;
                Vector4 dimColor = color;
                dimColor.W = 0.4f;
                uint baseColor = ImGui.ColorConvertFloat4ToU32(dimColor);
                if (baseFill > 0)
                    drawList.AddRectFilled(barPos, barPos + new Vector2(barWidth * baseFill, barHeight), baseColor, 4f);

                float totalFill = maxVal > 0 ? Math.Clamp((float)currentVal / maxVal, 0f, 1f) : 0f;
                uint totalColor = ImGui.ColorConvertFloat4ToU32(color);
                if (totalFill > baseFill)
                    drawList.AddRectFilled(barPos + new Vector2(barWidth * baseFill, 0),
                        barPos + new Vector2(barWidth * totalFill, barHeight), totalColor, 4f);

                uint borderCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.4f, 0.4f, 0.4f, 0.6f));
                drawList.AddRect(barPos, barPos + new Vector2(barWidth, barHeight), borderCol, 4f);
                ImGui.SetCursorScreenPos(barPos + new Vector2(0, barHeight + 4));
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

        // ── Callbacks ──
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
