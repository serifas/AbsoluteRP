using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Listings;
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
        // Skill tree preview
        private static int previewTreeIndex = 0;

        // Wizard steps
        private static int wizardStep = 0; // 0=System Select, 1=Class Select, 2=Stat Assign, 3=Skill Select, 4=Review

        // System selection
        private static string importCode = "";
        public static List<SystemData> availableSystems = new List<SystemData>();
        private static int selectedSystemIndex = -1;
        private static SystemData selectedSystem = null;

        // Class selection
        private static int selectedClassIndex = -1;
        private static int hoveredClassIndex = -1;

        // Stat assignment
        private static Dictionary<int, int> statAllocations = new Dictionary<int, int>();

        // Skill selection
        private static List<int> selectedSkills = new List<int>();

        // Submission result
        private static string submitMessage = "";
        private static bool submitSuccess = false;

        // Stat radar chart animation
        private static List<float> previousRadii = new List<float>();
        private static List<float> targetRadii = new List<float>();
        private static float morphProgress = 1f;
        private static DateTime morphStartTime = DateTime.MinValue;
        private const float MorphDuration = 0.3f;

        public static void DrawViewSystems()
        {
            WindowOperations.LoadIconsLazy(Plugin.plugin);

            // Step progress bar
            string[] stepNames = { "System", "Class", "Stats", "Skills", "Review" };
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

            switch (wizardStep)
            {
                case 0: DrawSystemSelection(); break;
                case 1: DrawClassSelection(); break;
                case 2: DrawStatAssignment(); break;
                case 3: DrawSkillSelection(); break;
                case 4: DrawReviewAndCreate(); break;
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
            if (ThemeManager.PillButton("Import##importSystem", new Vector2(80, 26)))
            {
                if (!string.IsNullOrWhiteSpace(importCode) && Plugin.character != null)
                {
                    Networking.DataSender.ImportSystemByCode(Plugin.character, importCode.Trim());
                }
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

            for (int i = 0; i < availableSystems.Count; i++)
            {
                var sys = availableSystems[i];
                bool isSelected = selectedSystem != null && selectedSystem.id == sys.id;
                if (ImGui.Selectable($"{sys.name}##{sys.id}", isSelected))
                {
                    selectedSystemIndex = i;
                    selectedSystem = sys;
                }
            }

            ImGui.Spacing();
            if (selectedSystem != null)
            {
                if (ThemeManager.PillButton("Next: Choose Class##nextStep", new Vector2(200, 32)))
                {
                    selectedClassIndex = -1;
                    hoveredClassIndex = -1;
                    wizardStep = 1;
                }
            }
        }

        // ── Step 1: Class Selection ──
        private static void DrawClassSelection()
        {
            if (selectedSystem == null) { wizardStep = 0; return; }

            if (ThemeManager.GhostButton("< Back##backToSystem", new Vector2(80, 26)))
            { wizardStep = 0; return; }

            ImGui.SameLine();
            ThemeManager.SectionHeader("Choose Your Class");
            ImGui.Spacing();

            var classes = selectedSystem.SkillClasses;
            if (classes.Count == 0)
            {
                ImGui.TextColored(ThemeManager.FontMuted, "This system has no classes defined.");
                if (ThemeManager.PillButton("Skip to Stats##skipClass", new Vector2(140, 28)))
                {
                    selectedClassIndex = -1;
                    InitStatAllocations();
                    wizardStep = 2;
                }
                return;
            }

            // Class icon grid
            float iconSize = 64f;
            float spacing = 12f;
            float availWidth = ImGui.GetContentRegionAvail().X * 0.55f;
            int cols = Math.Max(1, (int)(availWidth / (iconSize + spacing)));

            // Left panel: class icons
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
                bool isHovered = false;

                // Draw octagon
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

                // Invisible button for click/hover
                ImGui.SetCursorScreenPos(center - new Vector2(octRadius, octRadius));
                if (ImGui.InvisibleButton($"##cls_{i}", new Vector2(octRadius * 2, octRadius * 2)))
                {
                    selectedClassIndex = i;
                    previewTreeIndex = 0;
                }
                isHovered = ImGui.IsItemHovered();
                if (isHovered)
                    hoveredClassIndex = i;

                // Tooltip
                if (isHovered)
                {
                    ImGui.BeginTooltip();
                    ImGui.TextColored(ThemeManager.Accent, cls.name);
                    if (!string.IsNullOrEmpty(cls.description))
                        ImGui.TextWrapped(cls.description);
                    ImGui.EndTooltip();
                }
            }

            // Reserve space for the grid
            int totalRows = (classes.Count + cols - 1) / cols;
            ImGui.SetCursorScreenPos(cursor + new Vector2(0, totalRows * (iconSize + spacing) + spacing));

            // Skill tree preview if a class is selected
            if (selectedClassIndex >= 0 && selectedClassIndex < classes.Count)
            {
                ImGui.Spacing();
                ImGui.Spacing();
                ThemeManager.SubtitleText("Skill Trees:");
                ImGui.Spacing();
                DrawSkillTreePreview(selectedSystem, classes[selectedClassIndex]);
            }
            ImGui.EndChild();

            // Right panel: passives for selected/hovered class
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
                        // Passive icon (small octagon)
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
                if (ThemeManager.PillButton("Next: Assign Stats##nextStep2", new Vector2(200, 32)))
                {
                    InitStatAllocations();
                    wizardStep = 2;
                }
            }
        }

        // ── Skill Tree Preview Grid (read-only) ──
        private const int PreviewGridCols = 5;
        private const int PreviewGridRows = 8;

        private static void DrawSkillTreePreview(SystemData system, SkillClassData cls)
        {
            int classId = cls.id;

            // Tree tabs
            if (cls.SkillTrees.Count > 0)
            {
                if (previewTreeIndex >= cls.SkillTrees.Count)
                    previewTreeIndex = 0;

                if (ImGui.BeginTabBar("##PreviewTreeTabs"))
                {
                    for (int t = 0; t < cls.SkillTrees.Count; t++)
                    {
                        if (ImGui.BeginTabItem(cls.SkillTrees[t].name + $"##ptree{t}"))
                        {
                            previewTreeIndex = t;
                            ImGui.EndTabItem();
                        }
                    }
                    ImGui.EndTabBar();
                }
            }
            else
            {
                previewTreeIndex = 0;
            }

            // Draw grid
            float gridWidth = ImGui.GetContentRegionAvail().X;
            float cellSize = Math.Min((gridWidth - 20) / PreviewGridCols, 64f);
            float octRadius = cellSize * 0.35f;

            var drawList = ImGui.GetWindowDrawList();
            Vector2 origin = ImGui.GetCursorScreenPos();

            // Filter skills for this class + tree
            var treeSkills = system.Skills.Where(s => s.classId == classId && s.treeIndex == previewTreeIndex && s.isCastable).ToList();

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

                    // Arrow head
                    Vector2 mid = (from + to) / 2;
                    Vector2 dir = Vector2.Normalize(to - from);
                    Vector2 perp = new Vector2(-dir.Y, dir.X);
                    float arrowSize = 5f;
                    drawList.AddTriangleFilled(
                        mid + dir * arrowSize,
                        mid - dir * arrowSize + perp * arrowSize,
                        mid - dir * arrowSize - perp * arrowSize,
                        lineColor);
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
                        if (skill.iconTexture != null && skill.iconTexture.Handle != IntPtr.Zero)
                        {
                            Vector2 imgMin = center - new Vector2(octRadius, octRadius);
                            Vector2 imgMax = center + new Vector2(octRadius, octRadius);
                            drawList.AddImage(skill.iconTexture.Handle, imgMin, imgMax);
                            Skills.Skills.MaskSquareToOctagon(drawList, center, octRadius, octPoints);
                        }
                        else
                        {
                            uint fillColor = ImGui.ColorConvertFloat4ToU32(ThemeManager.BgLighter);
                            Skills.Skills.DrawFilledOctagon(drawList, octPoints, fillColor);
                            string label = skill.name.Length > 6 ? skill.name[..6] + ".." : skill.name;
                            var textSize = ImGui.CalcTextSize(label);
                            drawList.AddText(center - textSize / 2, 0xFFFFFFFF, label);
                        }

                        uint borderColor = ImGui.ColorConvertFloat4ToU32(ThemeManager.AccentMuted);
                        drawList.AddPolyline(ref octPoints[0], octPoints.Length, borderColor, ImDrawFlags.Closed, 1.5f);

                        // Tier indicator
                        if (skill.maxTiers > 1)
                        {
                            string tierLabel = $"T{skill.maxTiers}";
                            Vector2 tierPos = center + new Vector2(octRadius * 0.3f, -octRadius * 0.9f);
                            drawList.AddText(tierPos + new Vector2(1, 1), 0xFF000000, tierLabel);
                            drawList.AddText(tierPos, 0xFFFFFFFF, tierLabel);
                        }

                        // Tooltip on hover
                        ImGui.SetCursorScreenPos(center - new Vector2(octRadius, octRadius));
                        ImGui.InvisibleButton($"##prev_{x}_{y}", new Vector2(octRadius * 2, octRadius * 2));
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.TextColored(ThemeManager.Accent, skill.name);
                            if (!string.IsNullOrEmpty(skill.description))
                                ImGui.TextWrapped(skill.description);
                            if (skill.cooldownTurns > 0)
                                ImGui.Text($"Cooldown: {skill.cooldownTurns} turns");
                            if (skill.resourceCost > 0)
                                ImGui.Text($"Cost: {skill.resourceCost}");
                            ImGui.EndTooltip();
                        }
                    }
                    else
                    {
                        // Empty slot — faint outline
                        uint outlineColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.3f, 0.3f, 0.3f, 0.2f));
                        drawList.AddPolyline(ref octPoints[0], octPoints.Length, outlineColor, ImDrawFlags.Closed, 1f);
                    }
                }
            }

            // Reserve space for the grid
            ImGui.SetCursorScreenPos(origin + new Vector2(0, PreviewGridRows * cellSize + 10));

            if (treeSkills.Count == 0)
                ImGui.TextColored(ThemeManager.FontMuted, "No skills in this tree.");
        }

        // ── Step 2: Stat Assignment ──
        private static void InitStatAllocations()
        {
            statAllocations.Clear();
            if (selectedSystem == null) return;
            foreach (var kvp in selectedSystem.StatsData)
                statAllocations[kvp.Key] = 0;
            // Init radar chart to a small uniform shape
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

            // Snapshot current interpolated state as previous
            previousRadii = GetCurrentInterpolatedRadii();

            // Build new target radii based on allocations
            // Each stat scales so that spending ALL available points on it = 100% (reaches the corner)
            // Square root curve makes lower values more visible while max still hits the edge
            int budget = selectedSystem.basePointsAvailable > 0 ? selectedSystem.basePointsAvailable : 1;
            targetRadii = new List<float>(count);
            foreach (var kvp in stats)
            {
                int key = kvp.Key;
                int val = statAllocations.ContainsKey(key) ? statAllocations[key] : 0;
                float linear = (float)val / budget;
                targetRadii.Add(MathF.Sqrt(linear)); // sqrt curve: 20% → 45%, 50% → 71%, 100% → 100%
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
            // Smooth ease-out
            t = 1f - (1f - t) * (1f - t);
            var result = new List<float>(count);
            for (int i = 0; i < count; i++)
                result.Add(previousRadii[i] + (targetRadii[i] - previousRadii[i]) * t);
            return result;
        }

        private static void DrawStatAssignment()
        {
            if (selectedSystem == null) { wizardStep = 0; return; }

            if (ThemeManager.GhostButton("< Back##backToClass", new Vector2(80, 26)))
            { wizardStep = 1; return; }

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

            // Left side: stat controls
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
                ImGui.SameLine(controlsWidth - 110);

                // - button
                bool canRemove = stat.canRemovePoints && (stat.canGoNegative || val > stat.baseMin);
                if (!canRemove) ImGui.BeginDisabled();
                if (ThemeManager.GhostButton("-##dec", new Vector2(24, 24)))
                {
                    statAllocations[key] = val - 1;
                    changed = true;
                }
                if (!canRemove) ImGui.EndDisabled();

                ImGui.SameLine();
                ImGui.SetNextItemWidth(30);
                ImGui.Text($"{val}");
                ImGui.SameLine();

                // + button
                bool canAdd = stat.canAddPoints && val < stat.baseMax && remaining > 0;
                if (!canAdd) ImGui.BeginDisabled();
                if (ThemeManager.GhostButton("+##inc", new Vector2(24, 24)))
                {
                    statAllocations[key] = val + 1;
                    changed = true;
                }
                if (!canAdd) ImGui.EndDisabled();

                ImGui.PopID();
            }
            ImGui.EndChild();

            // Trigger animation on change
            if (changed)
                TriggerRadarAnimation();

            // Right side: radar chart
            ImGui.SameLine();
            ImGui.BeginChild("##radarChart", new Vector2(chartWidth, 0), false);
            DrawRadarChart(stats, chartWidth);
            DrawResourceBars(selectedSystem, stats);
            ImGui.EndChild();

            ImGui.Spacing();
            if (ThemeManager.PillButton("Next: Choose Skills##nextStep3", new Vector2(200, 32)))
            {
                selectedSkills.Clear();
                wizardStep = 3;
            }
        }

        private static void DrawRadarChart(SortedList<int, StatData> stats, float availWidth)
        {
            int count = stats.Count;
            if (count < 2) return;

            // Advance animation
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

            // Draw background rings (guides at 25%, 50%, 75%, 100%)
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

            // Draw axis lines from center to each vertex + stat labels
            int idx = 0;
            foreach (var kvp in stats)
            {
                float angle = startAngle + idx * angleStep;
                Vector2 axisEnd = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * chartRadius;
                drawList.AddLine(center, axisEnd, ringColor, 1f);

                // Label
                Vector2 labelPos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (chartRadius + 14);
                string label = kvp.Value.name;
                var textSize = ImGui.CalcTextSize(label);
                uint labelColor = ImGui.ColorConvertFloat4ToU32(kvp.Value.color);
                drawList.AddText(labelPos - textSize / 2, labelColor, label);
                idx++;
            }

            // Get interpolated radii for the filled shape
            var radii = GetCurrentInterpolatedRadii();
            if (radii.Count != count)
            {
                // Safety: init if mismatch
                radii = new List<float>(new float[count]);
            }

            // Build the data polygon
            var dataPoints = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float r = Math.Max(radii[i], 0.05f) * chartRadius; // Always at least the base size
                float angle = startAngle + i * angleStep;
                dataPoints[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * r;
            }
            bool hasAnyValue = true; // Always draw — starts as small shape

            if (hasAnyValue)
            {
                // Filled shape (semi-transparent accent)
                Vector4 fillVec = ThemeManager.Accent;
                fillVec.W = 0.2f;
                uint fillColor = ImGui.ColorConvertFloat4ToU32(fillVec);

                // Draw filled polygon using triangle fan
                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count;
                    drawList.AddTriangleFilled(center, dataPoints[i], dataPoints[next], fillColor);
                }

                // Border
                Vector4 borderVec = ThemeManager.Accent;
                borderVec.W = 0.8f;
                uint borderColor = ImGui.ColorConvertFloat4ToU32(borderVec);
                drawList.AddPolyline(ref dataPoints[0], dataPoints.Length, borderColor, ImDrawFlags.Closed, 2.5f);

                // Dots at each vertex
                uint dotColor = ImGui.ColorConvertFloat4ToU32(ThemeManager.Accent);
                for (int i = 0; i < count; i++)
                {
                    if (radii[i] > 0.01f)
                        drawList.AddCircleFilled(dataPoints[i], 4f, dotColor);
                }
            }

            // Reserve space
            ImGui.SetCursorScreenPos(cursorStart + new Vector2(0, chartRadius * 2 + 50));
        }

        private static void DrawResourceBars(SystemData system, SortedList<int, StatData> stats)
        {
            // Collect resources and health that have linked stats
            var linkedResources = new List<(string name, Vector4 color, int baseVal, int maxVal, int currentVal, int bonusVal)>();

            // Health
            var combat = system.CombatConfig;
            if (combat.healthEnabled && combat.healthLinkedStatId >= 0)
            {
                int statVal = GetLinkedStatValue(stats, combat.healthLinkedStatId);
                int bonus = (int)(statVal * combat.healthStatMultiplier);
                int current = combat.healthBase + bonus;
                int max = combat.healthMax > 0 ? combat.healthMax : current;
                linkedResources.Add(("Health", new Vector4(0.8f, 0.2f, 0.2f, 1f), combat.healthBase, max, current, bonus));
            }

            // Resources
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
                // Label
                string label = bonusVal != 0
                    ? $"{name}: {baseVal} + {bonusVal} = {currentVal} / {maxVal}"
                    : $"{name}: {currentVal} / {maxVal}";
                ImGui.Text(label);

                // Bar background
                Vector2 barPos = ImGui.GetCursorScreenPos();
                uint bgColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.15f, 0.15f, 0.15f, 1f));
                drawList.AddRectFilled(barPos, barPos + new Vector2(barWidth, barHeight), bgColor, 4f);

                // Base portion
                float baseFill = maxVal > 0 ? Math.Clamp((float)baseVal / maxVal, 0f, 1f) : 0f;
                Vector4 dimColor = color;
                dimColor.W = 0.4f;
                uint baseColor = ImGui.ColorConvertFloat4ToU32(dimColor);
                if (baseFill > 0)
                    drawList.AddRectFilled(barPos, barPos + new Vector2(barWidth * baseFill, barHeight), baseColor, 4f);

                // Total (base + bonus) portion
                float totalFill = maxVal > 0 ? Math.Clamp((float)currentVal / maxVal, 0f, 1f) : 0f;
                uint totalColor = ImGui.ColorConvertFloat4ToU32(color);
                if (totalFill > baseFill)
                    drawList.AddRectFilled(barPos + new Vector2(barWidth * baseFill, 0),
                        barPos + new Vector2(barWidth * totalFill, barHeight), totalColor, 4f);

                // Border
                uint borderColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.4f, 0.4f, 0.4f, 0.6f));
                drawList.AddRect(barPos, barPos + new Vector2(barWidth, barHeight), borderColor, 4f);

                // Reserve space
                ImGui.SetCursorScreenPos(barPos + new Vector2(0, barHeight + 4));
            }
        }

        private static int GetLinkedStatValue(SortedList<int, StatData> stats, int linkedStatId)
        {
            // Find the stat by its id and return the allocated value
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

        // ── Step 3: Skill Selection ──
        private static void DrawSkillSelection()
        {
            if (selectedSystem == null) { wizardStep = 0; return; }

            if (ThemeManager.GhostButton("< Back##backToStats", new Vector2(80, 26)))
            { wizardStep = 2; return; }

            ImGui.SameLine();
            ThemeManager.SectionHeader("Choose Skills");
            ImGui.Spacing();

            int classId = selectedClassIndex >= 0 && selectedClassIndex < selectedSystem.SkillClasses.Count
                ? selectedSystem.SkillClasses[selectedClassIndex].id : -999;

            var availableSkills = selectedSystem.Skills
                .Where(s => s.isCastable && (classId == -999 || s.classId == classId))
                .ToList();

            if (availableSkills.Count == 0)
            {
                ImGui.TextColored(ThemeManager.FontMuted, "No skills available to select.");
            }
            else
            {
                foreach (var skill in availableSkills)
                {
                    bool isSelected = selectedSkills.Contains(skill.id);
                    ImGui.PushID($"skill_{skill.id}");

                    if (skill.iconTexture != null && skill.iconTexture.Handle != IntPtr.Zero)
                    {
                        ImGui.Image(skill.iconTexture.Handle, new Vector2(24, 24));
                        ImGui.SameLine();
                    }

                    bool toggled = isSelected;
                    if (ImGui.Checkbox($"{skill.name}##sel", ref toggled))
                    {
                        if (toggled && !isSelected)
                        {
                            // Check prerequisites
                            bool prereqMet = true;
                            var prereqs = selectedSystem.SkillConnections.Where(c => c.toSkillId == skill.id).ToList();
                            foreach (var prereq in prereqs)
                            {
                                if (!selectedSkills.Contains(prereq.fromSkillId))
                                {
                                    prereqMet = false;
                                    break;
                                }
                            }
                            if (prereqMet)
                                selectedSkills.Add(skill.id);
                        }
                        else if (!toggled && isSelected)
                        {
                            selectedSkills.Remove(skill.id);
                            // Also remove skills that depend on this one
                            var dependents = selectedSystem.SkillConnections
                                .Where(c => c.fromSkillId == skill.id)
                                .Select(c => c.toSkillId)
                                .ToList();
                            selectedSkills.RemoveAll(s => dependents.Contains(s));
                        }
                    }

                    if (!string.IsNullOrEmpty(skill.description) && ImGui.IsItemHovered())
                        ImGui.SetTooltip(skill.description);

                    ImGui.PopID();
                }
            }

            ImGui.Spacing();
            if (ThemeManager.PillButton("Next: Review##nextStep4", new Vector2(200, 32)))
                wizardStep = 4;
        }

        // ── Step 4: Review & Create ──
        private static void DrawReviewAndCreate()
        {
            if (selectedSystem == null) { wizardStep = 0; return; }

            if (ThemeManager.GhostButton("< Back##backToSkills", new Vector2(80, 26)))
            { wizardStep = 3; return; }

            ImGui.SameLine();
            ThemeManager.SectionHeader("Review Your Character");
            ImGui.Spacing();

            // System
            ImGui.Text("System: ");
            ImGui.SameLine();
            ImGui.TextColored(ThemeManager.Accent, selectedSystem.name);

            // Class
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

            // Stats
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

            // Skills
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
                    if (skill != null)
                        ImGui.BulletText(skill.name);
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

            if (ThemeManager.PillButton("Submit Character Sheet##submit", new Vector2(250, 36)))
            {
                if (Plugin.character != null)
                {
                    int classId = selectedClassIndex >= 0 && selectedClassIndex < selectedSystem.SkillClasses.Count
                        ? selectedSystem.SkillClasses[selectedClassIndex].id : -1;
                    Networking.DataSender.SubmitCharacterSheet(Plugin.character, selectedSystem.id, classId,
                        statAllocations, selectedSkills);
                }
            }

            if (selectedSystem.requireApproval)
            {
                ImGui.Spacing();
                ImGui.TextColored(ThemeManager.FontMuted, "This system requires owner approval. Your sheet will be reviewed.");
            }
        }

        // Called from DataReceiver when sheet submission result arrives
        public static void OnSubmitResult(bool success, string message)
        {
            submitSuccess = success;
            submitMessage = message;
        }

        // Called from DataReceiver when public system data arrives
        public static void OnPublicSystemReceived(SystemData system)
        {
            // Add or update in available list
            var existing = availableSystems.FindIndex(s => s.id == system.id);
            if (existing >= 0)
                availableSystems[existing] = system;
            else
                availableSystems.Add(system);
        }
    }
}
