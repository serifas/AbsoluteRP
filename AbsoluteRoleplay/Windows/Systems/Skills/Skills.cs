using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Listings;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AbsoluteRP.Windows.Systems.Skills
{
    internal class Skills
    {
        // Grid
        private const int GridCols = 5;
        private const int GridRows = 8;
        private static (int x, int y)? selectedSlot = null;
        private static int selectedSkillIndex = -1;

        // Class / Tree selection
        private static int selectedClassIndex = -1;
        private static int selectedTreeIndex = 0;
        private static string newClassName = "";
        private static string newTreeName = "";

        // Skill editing
        private static string editName = "";
        private static string editDesc = "";
        private static bool editCastable = true;
        private static int editCooldown = 0;
        private static int editResourceId = -1;
        private static int editResourceCost = 0;
        private static int editIconId = 0;
        private static int editMaxTiers = 1;

        // Connection mode: when set, next skill click creates a parent→child link
        private static int? connectingFromSkillId = null;

        // Monotonic counter for temporary skill IDs (avoids collisions after deletions)
        private static int nextTempSkillId = -1;

        // Monotonic counter for temporary class IDs (avoids collisions between unsaved classes)
        private static int nextTempClassId = -1;

        // Icon picker
        private static bool showIconPicker = false;
        private static bool _deleteClassPopupOpen = true;

        // Class sub-view: 0=Details, 1=Skill Trees, 2=Passives
        private static int classSubTab = 0;
        private static string editClassDesc = "";

        public static void DrawSkillsEditor()
        {
            var system = SystemsWindow.currentSystem;
            if (system == null) return;

            WindowOperations.LoadIconsLazy(Plugin.plugin);

            float panelWidth = ImGui.GetContentRegionAvail().X;

            // ── Class Selector ──
            DrawClassSelector(system);

            if (selectedClassIndex < 0 || selectedClassIndex >= system.SkillClasses.Count)
            {
                ImGui.Spacing();
                ThemeManager.SubtitleText("Select or create a class to begin.");
                return;
            }

            var cls = system.SkillClasses[selectedClassIndex];

            ImGui.Spacing();

            // ── Class Sub-Tabs ──
            if (ImGui.BeginTabBar("##ClassSubTabs"))
            {
                if (ImGui.BeginTabItem("Details"))
                {
                    classSubTab = 0;
                    ImGui.Spacing();
                    DrawClassDetails(system, cls);
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Skill Trees"))
                {
                    classSubTab = 1;
                    ImGui.Spacing();
                    DrawSkillTreesSection(system, cls);
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Default Passives"))
                {
                    classSubTab = 2;
                    ImGui.Spacing();
                    DrawPassivesSection(system, cls);
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }

            // Icon picker popup (shared)
            DrawIconPickerPopup(system);
        }

        // Class icon picker state
        private static bool showClassIconPicker = false;

        // ── Class Details Sub-Tab ──
        private static void DrawClassDetails(SystemData system, SkillClassData cls)
        {
            ThemeManager.SectionHeader(cls.name);
            ImGui.Spacing();

            // Class icon
            ImGui.Text("Class Icon:");
            ImGui.SameLine();
            if (cls.iconTexture != null && cls.iconTexture.Handle != IntPtr.Zero)
            {
                ImGui.Image(cls.iconTexture.Handle, new Vector2(32, 32));
                ImGui.SameLine();
            }
            if (ThemeManager.PillButton("Change Icon##classIcon"))
                showClassIconPicker = true;
            if (cls.iconId > 0)
            {
                ImGui.SameLine();
                if (ThemeManager.GhostButton("Clear##clearClassIcon"))
                {
                    cls.iconId = 0;
                    cls.iconTexture = null;
                }
            }

            ImGui.Spacing();

            // Class name
            string cname = cls.name;
            ImGui.Text("Class Name:");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGui.InputText("##className", ref cname, 64))
                cls.name = cname;

            ImGui.Spacing();

            // Class description
            ImGui.Text("Description (visible to players):");
            string cdesc = cls.description ?? "";
            if (ImGui.InputTextMultiline("##classDesc", ref cdesc, 2000,
                new Vector2(ImGui.GetContentRegionAvail().X, 0)))
                cls.description = cdesc;

            ImGui.Spacing();

            // Initial skill points
            ImGui.Text("Initial Skill Points:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            int isp = cls.initialSkillPoints;
            if (ImGui.InputInt("##initSkillPtsDetail", ref isp))
                cls.initialSkillPoints = Math.Max(0, isp);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Initial skill points players receive for this class.\n0 = unlimited (all skills available).");

            ImGui.Spacing();
            ThemeManager.GradientSeparator();
            ImGui.Spacing();

            // Save button
            if (ThemeManager.PillButton("Save Class##saveClass"))
            {
                if (system.id > 0 && Plugin.character != null)
                {
                    Networking.DataSender.SaveSkillClasses(Plugin.character, system.id, system.SkillClasses);
                }
            }

            // Class icon picker popup
            DrawClassIconPickerPopup(cls);
        }

        private static void DrawClassIconPickerPopup(SkillClassData cls)
        {
            if (!showClassIconPicker) return;

            var scale = ImGui.GetIO().FontGlobalScale;
            ImGui.SetNextWindowSize(new Vector2(700 * scale, 600 * scale), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Class Icon Picker##classIconPicker", ref showClassIconPicker))
            {
                IDalamudTextureWrap dummyTex = null;
                WindowOperations.RenderIcons(Plugin.plugin, false, true, null, null, ref dummyTex);

                if (WindowOperations.selectedTreeIconId.HasValue && WindowOperations.selectedIcon != null)
                {
                    cls.iconId = (int)WindowOperations.selectedTreeIconId.Value;
                    cls.iconTexture = WindowOperations.selectedIcon;
                    WindowOperations.selectedTreeIconId = null;
                    WindowOperations.selectedIcon = null;
                    showClassIconPicker = false;
                }
            }
            ImGui.End();
        }

        // ── Skill Trees Sub-Tab ──
        private static void DrawSkillTreesSection(SystemData system, SkillClassData cls)
        {
            float panelWidth = ImGui.GetContentRegionAvail().X;

            // Tree selector tabs
            DrawTreeSelector(system);

            ImGui.Spacing();
            ThemeManager.GradientSeparator();
            ImGui.Spacing();

            // Split: left = grid, right = detail panel
            float detailWidth = 260f;
            float gridWidth = panelWidth - detailWidth - 16;
            if (gridWidth < 300) { gridWidth = panelWidth; detailWidth = 0; }

            if (ImGui.BeginChild("##SkillGrid", new Vector2(gridWidth, ImGui.GetContentRegionAvail().Y - 40), true))
            {
                DrawSkillGrid(system, gridWidth);
            }
            ImGui.EndChild();

            if (detailWidth > 0)
            {
                ImGui.SameLine();
                if (ImGui.BeginChild("##SkillDetail", new Vector2(detailWidth, ImGui.GetContentRegionAvail().Y - 40), true))
                {
                    DrawSkillDetailPanel(system);
                }
                ImGui.EndChild();
            }

            // Save
            ImGui.Spacing();
            if (ThemeManager.PillButton("Save Skills##saveSkills"))
            {
                if (system.id > 0 && Plugin.character != null)
                {
                    Networking.DataSender.SaveSkillClasses(Plugin.character, system.id, system.SkillClasses);
                    Networking.DataSender.SaveSkills(Plugin.character, system.id, system.Skills, system.SkillConnections);
                }
            }
        }

        // ── Default Passives Sub-Tab ──
        private static void DrawPassivesSection(SystemData system, SkillClassData cls)
        {
            ThemeManager.SectionHeader("Default Passives");
            ThemeManager.SubtitleText("Passives that all members of this class start with.");
            ImGui.Spacing();

            int classId = cls.id;
            var passives = system.Skills.Where(s => s.classId == classId && !s.isCastable && s.gridX == -1).ToList();

            if (ThemeManager.PillButton("+ Add Passive##addPassive"))
            {
                system.Skills.Add(new SkillData
                {
                    id = nextTempSkillId--,
                    name = "New Passive",
                    classId = classId,
                    isCastable = false,
                    gridX = -1, // -1 = not on any grid, it's a default passive
                    gridY = -1,
                    treeIndex = -1,
                });
            }

            ImGui.Spacing();

            for (int i = 0; i < passives.Count; i++)
            {
                var p = passives[i];
                ImGui.PushID($"passive_{i}");

                string pname = p.name;
                ImGui.SetNextItemWidth(180);
                if (ImGui.InputText("##passName", ref pname, 64))
                    p.name = pname;

                ImGui.SameLine();
                if (ThemeManager.DangerButton("X##delPassive"))
                {
                    system.Skills.Remove(p);
                    ImGui.PopID();
                    continue;
                }

                string pdesc = p.description ?? "";
                if (ImGui.InputTextMultiline("##passDesc", ref pdesc, 500,
                    new Vector2(ImGui.GetContentRegionAvail().X, 0)))
                    p.description = pdesc;

                ImGui.Spacing();
                ThemeManager.GradientSeparator();
                ImGui.Spacing();
                ImGui.PopID();
            }

            // Save
            ImGui.Spacing();
            if (ThemeManager.PillButton("Save Passives##savePassives"))
            {
                if (system.id > 0 && Plugin.character != null)
                {
                    Networking.DataSender.SaveSkills(Plugin.character, system.id, system.Skills, system.SkillConnections);
                }
            }
        }

        // ── Class Selector ──
        private static void DrawClassSelector(SystemData system)
        {
            ImGui.Text("Skill Class:");
            ImGui.SameLine();

            string classLabel = "All Skills";
            if (selectedClassIndex >= 0 && selectedClassIndex < system.SkillClasses.Count)
                classLabel = system.SkillClasses[selectedClassIndex].name;

            ImGui.SetNextItemWidth(180);
            if (ImGui.BeginCombo("##SkillClass", classLabel))
            {
                if (ImGui.Selectable("All Skills", selectedClassIndex < 0))
                {
                    selectedClassIndex = -1;
                    selectedTreeIndex = 0;
                }
                for (int i = 0; i < system.SkillClasses.Count; i++)
                {
                    if (ImGui.Selectable(system.SkillClasses[i].name + $"##{i}", i == selectedClassIndex))
                    {
                        selectedClassIndex = i;
                        selectedTreeIndex = 0;
                        selectedSlot = null;
                        selectedSkillIndex = -1;
                        classSubTab = 0;
                        editClassDesc = "";
                        connectingFromSkillId = null;
                    }
                }
                ImGui.EndCombo();
            }

            ImGui.SameLine();
            if (ThemeManager.PillButton("+ Class##addClass"))
                ImGui.OpenPopup("##NewClassPopup");

            if (ImGui.BeginPopup("##NewClassPopup"))
            {
                ImGui.Text("Class Name:");
                ImGui.SetNextItemWidth(150);
                ImGui.InputText("##newClassName", ref newClassName, 64);
                if (ThemeManager.PillButton("Create##createClass") && !string.IsNullOrWhiteSpace(newClassName))
                {
                    var newClass = new SkillClassData
                    {
                        id = nextTempClassId--,
                        name = newClassName.Trim(),
                        sortOrder = system.SkillClasses.Count,
                    };
                    // Add a default tree
                    newClass.SkillTrees.Add(new SkillTreeData { name = "Main Tree", sortOrder = 0 });
                    system.SkillClasses.Add(newClass);
                    selectedClassIndex = system.SkillClasses.Count - 1;
                    selectedTreeIndex = 0;
                    newClassName = "";
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }

            if (selectedClassIndex >= 0 && selectedClassIndex < system.SkillClasses.Count)
            {
                ImGui.SameLine();
                if (ThemeManager.DangerButton("X##delClass"))
                {
                    ImGui.OpenPopup("ConfirmDeleteClass##confirmDelClass");
                }

                if (ImGui.BeginPopupModal("ConfirmDeleteClass##confirmDelClass", ref _deleteClassPopupOpen, ImGuiWindowFlags.AlwaysAutoResize))
                {
                    var className = system.SkillClasses[selectedClassIndex].name;
                    ImGui.Text($"Are you sure you want to delete class \"{className}\"?");
                    ImGui.Text("All skills and connections in this class will be removed.");
                    ImGui.Spacing();

                    if (ThemeManager.DangerButton("Delete"))
                    {
                        int classId = system.SkillClasses[selectedClassIndex].id;
                        system.Skills.RemoveAll(s => s.classId == classId);
                        system.SkillConnections.RemoveAll(c =>
                            !system.Skills.Exists(s => s.id == c.fromSkillId) ||
                            !system.Skills.Exists(s => s.id == c.toSkillId));
                        system.SkillClasses.RemoveAt(selectedClassIndex);
                        selectedClassIndex = -1;
                        selectedTreeIndex = 0;
                        connectingFromSkillId = null;
                        ImGui.CloseCurrentPopup();
                        ImGui.EndPopup();
                        return;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Cancel"))
                    {
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }

                ImGui.SameLine();
                var cls = system.SkillClasses[selectedClassIndex];
                ImGui.Text("Skill Points:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(80);
                int isp = cls.initialSkillPoints;
                if (ImGui.InputInt("##initSkillPts", ref isp))
                    cls.initialSkillPoints = Math.Max(0, isp);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Initial skill points players can spend on this class's skill trees.");
            }
        }

        // ── Tree Selector (multiple trees per class) ──
        private static bool openAddTreePopup = false;

        private static void DrawTreeSelector(SystemData system)
        {
            if (selectedClassIndex < 0 || selectedClassIndex >= system.SkillClasses.Count) return;
            var cls = system.SkillClasses[selectedClassIndex];

            // Ensure at least one tree
            if (cls.SkillTrees.Count == 0)
                cls.SkillTrees.Add(new SkillTreeData { name = "Main Tree", sortOrder = 0 });

            // Clamp tree index to valid range
            if (selectedTreeIndex < 0 || selectedTreeIndex >= cls.SkillTrees.Count)
                selectedTreeIndex = 0;

            // Tree tab bar
            if (ImGui.BeginTabBar("##SkillTreeTabs"))
            {
                for (int t = 0; t < cls.SkillTrees.Count; t++)
                {
                    var tree = cls.SkillTrees[t];
                    if (ImGui.BeginTabItem(tree.name + $"##tree{t}"))
                    {
                        if (selectedTreeIndex != t)
                        {
                            selectedTreeIndex = t;
                            selectedSlot = null;
                            selectedSkillIndex = -1;
                        }
                        ImGui.EndTabItem();
                    }
                }

                // "+" tab button — use deferred flag to avoid popup-inside-tabbar issue
                if (ImGui.TabItemButton("+##addTree"))
                    openAddTreePopup = true;

                ImGui.EndTabBar();
            }

            // Open popup outside the tab bar context
            if (openAddTreePopup)
            {
                ImGui.OpenPopup("##NewTreePopup");
                openAddTreePopup = false;
            }

            // New tree popup
            if (ImGui.BeginPopup("##NewTreePopup"))
            {
                ImGui.Text("Tree Name:");
                ImGui.SetNextItemWidth(150);
                ImGui.InputText("##newTreeName", ref newTreeName, 64);
                if (ThemeManager.PillButton("Create##createTree") && !string.IsNullOrWhiteSpace(newTreeName))
                {
                    cls.SkillTrees.Add(new SkillTreeData { name = newTreeName.Trim(), sortOrder = cls.SkillTrees.Count });
                    selectedTreeIndex = cls.SkillTrees.Count - 1;
                    selectedSlot = null;
                    selectedSkillIndex = -1;
                    newTreeName = "";
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }

            // Delete tree button (only if more than 1)
            if (cls.SkillTrees.Count > 1 && selectedTreeIndex >= 0 && selectedTreeIndex < cls.SkillTrees.Count)
            {
                if (ThemeManager.DangerButton("Delete Tree##delTree"))
                {
                    int classId = cls.id;
                    system.Skills.RemoveAll(s => s.classId == classId && s.treeIndex == selectedTreeIndex);
                    // Shift down tree indices for skills on later trees
                    foreach (var skill in system.Skills.Where(s => s.classId == classId && s.treeIndex > selectedTreeIndex))
                        skill.treeIndex--;
                    cls.SkillTrees.RemoveAt(selectedTreeIndex);
                    if (selectedTreeIndex >= cls.SkillTrees.Count)
                        selectedTreeIndex = cls.SkillTrees.Count - 1;
                    selectedSlot = null;
                    selectedSkillIndex = -1;
                }
            }
        }

        // ── Skill Grid ──
        private static void DrawSkillGrid(SystemData system, float gridWidth)
        {
            var drawList = ImGui.GetWindowDrawList();
            Vector2 origin = ImGui.GetCursorScreenPos();

            float cellSize = Math.Min((gridWidth - 20) / GridCols, 70f);
            float octRadius = cellSize * 0.35f;
            float iconSize = octRadius * 1.6f;

            // Filter: class + tree
            int filterClassId = selectedClassIndex >= 0 && selectedClassIndex < system.SkillClasses.Count
                ? system.SkillClasses[selectedClassIndex].id : -999;

            // Draw connections with direction arrows and required points (filtered to current class + tree)
            foreach (var conn in system.SkillConnections)
            {
                var fromSkill = system.Skills.FirstOrDefault(s => s.id == conn.fromSkillId
                    && (filterClassId == -999 || s.classId == filterClassId)
                    && s.treeIndex == selectedTreeIndex);
                var toSkill = system.Skills.FirstOrDefault(s => s.id == conn.toSkillId
                    && (filterClassId == -999 || s.classId == filterClassId)
                    && s.treeIndex == selectedTreeIndex);
                if (fromSkill != null && toSkill != null)
                {
                    Vector2 from = origin + new Vector2(fromSkill.gridX * cellSize + cellSize / 2, fromSkill.gridY * cellSize + cellSize / 2);
                    Vector2 to = origin + new Vector2(toSkill.gridX * cellSize + cellSize / 2, toSkill.gridY * cellSize + cellSize / 2);
                    uint lineColor = ImGui.ColorConvertFloat4ToU32(ThemeManager.AccentMuted);
                    drawList.AddLine(from, to, lineColor, 2f);

                    // Arrow head at midpoint pointing toward child
                    Vector2 mid = (from + to) / 2;
                    Vector2 dir = Vector2.Normalize(to - from);
                    Vector2 perp = new Vector2(-dir.Y, dir.X);
                    float arrowSize = 6f;
                    drawList.AddTriangleFilled(
                        mid + dir * arrowSize,
                        mid - dir * arrowSize + perp * arrowSize,
                        mid - dir * arrowSize - perp * arrowSize,
                        lineColor);

                    // Required points label
                    if (conn.requiredPoints > 1)
                    {
                        string reqText = $"{conn.requiredPoints}";
                        var textSize = ImGui.CalcTextSize(reqText);
                        drawList.AddText(mid - textSize / 2 + new Vector2(0, -10),
                            ImGui.ColorConvertFloat4ToU32(ThemeManager.Font), reqText);
                    }
                }
            }

            // Draw "connecting from" preview line
            if (connectingFromSkillId.HasValue)
            {
                var fromSkill = system.Skills.FirstOrDefault(s => s.id == connectingFromSkillId.Value);
                if (fromSkill != null)
                {
                    Vector2 from = origin + new Vector2(fromSkill.gridX * cellSize + cellSize / 2, fromSkill.gridY * cellSize + cellSize / 2);
                    Vector2 mousePos = ImGui.GetMousePos();
                    drawList.AddLine(from, mousePos, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 0, 0.6f)), 2f);
                }
            }

            // Draw grid slots
            for (int y = 0; y < GridRows; y++)
            {
                for (int x = 0; x < GridCols; x++)
                {
                    Vector2 center = origin + new Vector2(x * cellSize + cellSize / 2, y * cellSize + cellSize / 2);

                    // Find skill at this position matching current class + tree
                    var skill = system.Skills.FirstOrDefault(s =>
                        s.gridX == x && s.gridY == y &&
                        (filterClassId == -999 || s.classId == filterClassId) &&
                        s.treeIndex == selectedTreeIndex);

                    bool isSelected = selectedSlot.HasValue && selectedSlot.Value.x == x && selectedSlot.Value.y == y;
                    bool hasSkill = skill != null;

                    var octPoints = GetOctagonPoints(center, octRadius);

                    if (hasSkill)
                    {
                        uint fillColor = isSelected
                            ? ImGui.ColorConvertFloat4ToU32(ThemeManager.Accent)
                            : ImGui.ColorConvertFloat4ToU32(ThemeManager.BgLighter);

                        if (skill.iconTexture != null && skill.iconTexture.Handle != IntPtr.Zero)
                        {
                            // Draw icon image filling the full octagon bounding box
                            Vector2 imgMin = center - new Vector2(octRadius, octRadius);
                            Vector2 imgMax = center + new Vector2(octRadius, octRadius);
                            drawList.AddImage(skill.iconTexture.Handle, imgMin, imgMax);

                            // Mask corners: draw filled triangles in the background color
                            // at each corner of the bounding square to cut it into an octagon
                            MaskSquareToOctagon(drawList, center, octRadius, octPoints);
                        }
                        else
                        {
                            // No icon — filled octagon with name text
                            DrawFilledOctagon(drawList, octPoints, fillColor);
                            string label = skill.name.Length > 6 ? skill.name[..6] + ".." : skill.name;
                            var textSize = ImGui.CalcTextSize(label);
                            drawList.AddText(center - textSize / 2, 0xFFFFFFFF, label);
                        }

                        // Octagon border (always on top)
                        uint borderColor = isSelected
                            ? ImGui.ColorConvertFloat4ToU32(ThemeManager.Accent)
                            : ImGui.ColorConvertFloat4ToU32(ThemeManager.AccentMuted);
                        drawList.AddPolyline(ref octPoints[0], octPoints.Length, borderColor, ImDrawFlags.Closed, 2.5f);

                        // Tier indicator (top-right corner) — white text with black shadow
                        if (skill.maxTiers > 1)
                        {
                            string tierLabel = $"T{skill.maxTiers}";
                            Vector2 tierPos = center + new Vector2(octRadius * 0.3f, -octRadius * 0.9f);
                            DrawOutlinedText(drawList, tierLabel, tierPos, 0xFFFFFFFF, 0xFF000000);
                        }

                        // Passive indicator (bottom-right) — white text with black shadow
                        if (!skill.isCastable)
                        {
                            Vector2 passivePos = center + new Vector2(octRadius * 0.5f, octRadius * 0.4f);
                            DrawOutlinedText(drawList, "P", passivePos, 0xFFFFFFFF, 0xFF000000);
                        }
                    }
                    else
                    {
                        // Empty slot
                        uint outlineColor = isSelected
                            ? ImGui.ColorConvertFloat4ToU32(ThemeManager.Accent)
                            : ImGui.ColorConvertFloat4ToU32(new Vector4(0.3f, 0.3f, 0.3f, 0.4f));
                        drawList.AddPolyline(ref octPoints[0], octPoints.Length, outlineColor, ImDrawFlags.Closed, 1f);
                    }

                    // Click detection
                    ImGui.SetCursorScreenPos(center - new Vector2(octRadius, octRadius));
                    if (ImGui.InvisibleButton($"##slot_{x}_{y}", new Vector2(octRadius * 2, octRadius * 2)))
                    {
                        if (connectingFromSkillId.HasValue && hasSkill)
                        {
                            // Complete connection: parent → this child
                            int fromId = connectingFromSkillId.Value;
                            int toId = skill.id;
                            if (fromId != toId && !system.SkillConnections.Any(c => c.fromSkillId == fromId && c.toSkillId == toId))
                            {
                                system.SkillConnections.Add(new SkillConnectionData
                                {
                                    fromSkillId = fromId,
                                    toSkillId = toId,
                                    requiredPoints = 1,
                                });
                            }
                            connectingFromSkillId = null;
                        }
                        else if (hasSkill)
                        {
                            selectedSlot = (x, y);
                            selectedSkillIndex = system.Skills.IndexOf(skill);
                            LoadSkillToEditor(skill);
                        }
                        else
                        {
                            // Cancel connection mode if clicking empty slot
                            connectingFromSkillId = null;

                            var newSkill = new SkillData
                            {
                                id = nextTempSkillId--,
                                name = "New Skill",
                                gridX = x,
                                gridY = y,
                                isCastable = true,
                                classId = filterClassId != -999 ? filterClassId : -1,
                                treeIndex = selectedTreeIndex,
                            };
                            system.Skills.Add(newSkill);
                            selectedSlot = (x, y);
                            selectedSkillIndex = system.Skills.Count - 1;
                            LoadSkillToEditor(newSkill);
                        }
                    }

                    // Tooltip (matches Tree tab formatting)
                    if (hasSkill && ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20f);
                        ImGui.PushTextWrapPos(250f);

                        // Name (bold-style via accent text)
                        Misc.RenderHtmlElements(skill.name, false, true, true, true, null, true);
                        ThemeManager.GradientSeparator();

                        // Description
                        if (!string.IsNullOrEmpty(skill.description))
                        {
                            Misc.RenderHtmlElements(skill.description, false, true, true, true, null, true);
                            ImGui.Spacing();
                        }

                        // Type + stats
                        ImGui.TextColored(skill.isCastable ? ThemeManager.Accent : ThemeManager.FontMuted,
                            skill.isCastable ? "Castable" : "Passive");
                        if (skill.maxTiers > 1)
                        {
                            ImGui.SameLine();
                            ImGui.Text($"| Tiers: {skill.maxTiers}");
                        }
                        if (skill.cooldownTurns > 0) ImGui.Text($"Cooldown: {skill.cooldownTurns} turns");
                        if (skill.resourceCost > 0) ImGui.Text($"Cost: {skill.resourceCost}");

                        // Parents
                        var parents = system.SkillConnections.Where(c => c.toSkillId == skill.id).ToList();
                        if (parents.Count > 0)
                        {
                            ThemeManager.GradientSeparator();
                            ImGui.Text("Requires:");
                            foreach (var p in parents)
                            {
                                var parentSkill = system.Skills.FirstOrDefault(s => s.id == p.fromSkillId);
                                if (parentSkill != null)
                                    ImGui.Text($"  {parentSkill.name} ({p.requiredPoints} pts)");
                            }
                        }

                        ImGui.PopTextWrapPos();
                        ImGui.PopTextWrapPos();
                            ImGui.EndTooltip();
                    }
                }
            }

            ImGui.Dummy(new Vector2(GridCols * cellSize, GridRows * cellSize));
        }

        // ── Detail Panel ──
        private static void DrawSkillDetailPanel(SystemData system)
        {
            if (selectedSkillIndex < 0 || selectedSkillIndex >= system.Skills.Count)
            {
                ThemeManager.SubtitleText("Click a grid slot to create or select a skill.");
                return;
            }

            var skill = system.Skills[selectedSkillIndex];
            ThemeManager.SectionHeader("Skill Details");
            ImGui.Spacing();

            // Icon preview + pick button
            ImGui.Text("Icon:");
            if (skill.iconTexture != null && skill.iconTexture.Handle != IntPtr.Zero)
            {
                ImGui.Image(skill.iconTexture.Handle, new Vector2(32, 32));
                ImGui.SameLine();
            }
            if (ThemeManager.GhostButton("Choose Icon##pickIcon"))
            {
                showIconPicker = true;
            }
            if (skill.iconId > 0)
            {
                ImGui.SameLine();
                ThemeManager.SubtitleText($"#{skill.iconId}");
            }
            ImGui.Spacing();

            // Name
            ImGui.Text("Name:");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGui.InputText("##skillName", ref editName, 64))
                skill.name = editName;

            // Description
            ImGui.Text("Description:");
            if (ImGui.InputTextMultiline("##skillDesc", ref editDesc, 500,
                new Vector2(ImGui.GetContentRegionAvail().X, 0)))
                skill.description = editDesc;

            ImGui.Spacing();

            // Castable / Passive
            bool castable = editCastable;
            if (ImGui.Checkbox("Castable##skillCast", ref castable))
            {
                editCastable = castable;
                skill.isCastable = castable;
            }
            if (!castable)
            {
                ImGui.SameLine();
                ImGui.TextColored(ThemeManager.FontMuted, "(Passive)");
            }

            if (skill.isCastable)
            {
                ImGui.Text("Cooldown (turns):");
                ImGui.SetNextItemWidth(80);
                if (ImGui.InputInt("##skillCD", ref editCooldown))
                {
                    editCooldown = Math.Max(0, editCooldown);
                    skill.cooldownTurns = editCooldown;
                }
            }

            ImGui.Spacing();
            ThemeManager.GradientSeparator();
            ImGui.Spacing();

            // Resource
            ImGui.Text("Uses Resource:");
            string resLabel = "None";
            if (editResourceId >= 0)
            {
                var res = system.Resources.FirstOrDefault(r => r.id == editResourceId);
                if (res != null) resLabel = res.name;
                else editResourceId = -1;
            }
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGui.BeginCombo("##skillRes", resLabel))
            {
                if (ImGui.Selectable("None", editResourceId < 0))
                {
                    editResourceId = -1;
                    skill.resourceId = -1;
                }
                foreach (var r in system.Resources)
                {
                    if (ImGui.Selectable(r.name + $"##{r.id}", editResourceId == r.id))
                    {
                        editResourceId = r.id;
                        skill.resourceId = r.id;
                    }
                }
                ImGui.EndCombo();
            }

            if (editResourceId >= 0)
            {
                ImGui.Text("Resource Cost:");
                ImGui.SetNextItemWidth(80);
                if (ImGui.InputInt("##skillResCost", ref editResourceCost))
                {
                    editResourceCost = Math.Max(0, editResourceCost);
                    skill.resourceCost = editResourceCost;
                }
            }

            ImGui.Spacing();
            ImGui.Spacing();

            // Tiers (ranks/levels for this skill)
            ImGui.Text("Max Tiers:");
            ImGui.SetNextItemWidth(80);
            if (ImGui.InputInt("##skillTiers", ref editMaxTiers))
            {
                editMaxTiers = Math.Clamp(editMaxTiers, 1, 10);
                skill.maxTiers = editMaxTiers;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("How many times this skill can be ranked up.\n1 = single rank, 2+ = tiered skill.");

            ImGui.Spacing();
            ThemeManager.GradientSeparator();
            ImGui.Spacing();

            // Connect to child button
            if (connectingFromSkillId.HasValue && connectingFromSkillId.Value == skill.id)
            {
                ImGui.TextColored(ThemeManager.Accent, "Click another skill to connect...");
                if (ThemeManager.GhostButton("Cancel##cancelConnect", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
                    connectingFromSkillId = null;
            }
            else
            {
                if (ThemeManager.PillButton("Connect to Child##connect", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
                    connectingFromSkillId = skill.id;
            }

            // Show existing connections FROM this skill
            var childConns = system.SkillConnections.Where(c => c.fromSkillId == skill.id).ToList();
            if (childConns.Count > 0)
            {
                ImGui.Spacing();
                ImGui.Text("Children:");
                foreach (var conn in childConns)
                {
                    var child = system.Skills.FirstOrDefault(s => s.id == conn.toSkillId);
                    if (child == null) continue;
                    ImGui.PushID($"conn_{conn.fromSkillId}_{conn.toSkillId}");
                    ImGui.Text($"  -> {child.name}");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(50);
                    int req = conn.requiredPoints;
                    if (ImGui.InputInt("pts##connReq", ref req))
                        conn.requiredPoints = Math.Max(1, req);
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Points required in this skill before child unlocks");
                    ImGui.SameLine();
                    if (ThemeManager.DangerButton("X##delConn"))
                        system.SkillConnections.Remove(conn);
                    ImGui.PopID();
                }
            }

            // Show parent connections TO this skill
            var parentConns = system.SkillConnections.Where(c => c.toSkillId == skill.id).ToList();
            if (parentConns.Count > 0)
            {
                ImGui.Spacing();
                ImGui.Text("Requires:");
                foreach (var conn in parentConns)
                {
                    var parent = system.Skills.FirstOrDefault(s => s.id == conn.fromSkillId);
                    if (parent == null) continue;
                    ImGui.Text($"  {parent.name} ({conn.requiredPoints} pts)");
                }
            }

            ImGui.Spacing();

            if (ThemeManager.DangerButton("Delete Skill##delSkill", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
            {
                system.SkillConnections.RemoveAll(c => c.fromSkillId == skill.id || c.toSkillId == skill.id);
                system.Skills.RemoveAt(selectedSkillIndex);
                selectedSkillIndex = -1;
                selectedSlot = null;
                connectingFromSkillId = null;
            }
        }

        // ── Icon Picker Popup ──
        private static void DrawIconPickerPopup(SystemData system)
        {
            if (!showIconPicker) return;
            if (selectedSkillIndex < 0 || selectedSkillIndex >= system.Skills.Count)
            {
                showIconPicker = false;
                return;
            }

            var iconScale = ImGui.GetIO().FontGlobalScale;
            ImGui.SetNextWindowSize(new Vector2(700 * iconScale, 600 * iconScale), ImGuiCond.Appearing);
            if (ImGui.Begin("Select Skill Icon##IconPicker", ref showIconPicker, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking))
            {
                var skill = system.Skills[selectedSkillIndex];

                // Render the icon picker
                IDalamudTextureWrap dummyTex = null;
                WindowOperations.RenderIcons(Plugin.plugin, false, true, null, null, ref dummyTex);

                // Check if an icon was selected via the tree icon picker
                if (WindowOperations.selectedTreeIconId.HasValue && WindowOperations.selectedIcon != null)
                {
                    skill.iconId = WindowOperations.selectedTreeIconId.Value;
                    skill.iconTexture = WindowOperations.selectedIcon;
                    editIconId = skill.iconId;
                    WindowOperations.selectedTreeIconId = null;
                    WindowOperations.selectedIcon = null;
                    showIconPicker = false;
                }

                ImGui.End();
            }
        }

        // ── Helpers ──

        private static void LoadSkillToEditor(SkillData skill)
        {
            editName = skill.name ?? "";
            editDesc = skill.description ?? "";
            editCastable = skill.isCastable;
            editCooldown = skill.cooldownTurns;
            editResourceId = skill.resourceId;
            editResourceCost = skill.resourceCost;
            editIconId = skill.iconId;
            editMaxTiers = skill.maxTiers;

            // Load icon texture if not loaded yet
            if (skill.iconId > 0 && (skill.iconTexture == null || skill.iconTexture.Handle == IntPtr.Zero))
            {
                _ = LoadSkillIconAsync(skill);
            }
        }

        private static async System.Threading.Tasks.Task LoadSkillIconAsync(SkillData skill)
        {
            try
            {
                var tex = await WindowOperations.RenderStatusIconAsync(Plugin.plugin, skill.iconId);
                if (tex != null)
                    skill.iconTexture = tex;
            }
            catch { }
        }

        private static void DrawOutlinedText(ImDrawListPtr drawList, string text, Vector2 pos, uint textColor, uint outlineColor)
        {
            // Draw black shadow/outline in 4 directions
            drawList.AddText(pos + new Vector2(-1, 0), outlineColor, text);
            drawList.AddText(pos + new Vector2(1, 0), outlineColor, text);
            drawList.AddText(pos + new Vector2(0, -1), outlineColor, text);
            drawList.AddText(pos + new Vector2(0, 1), outlineColor, text);
            // White text on top
            drawList.AddText(pos, textColor, text);
        }

        public static Vector2[] GetOctagonPoints(Vector2 center, float radius)
        {
            var pts = new Vector2[8];
            float startAngle = -MathF.PI / 2f;
            float step = MathF.PI * 2f / 8f;
            for (int i = 0; i < 8; i++)
            {
                float angle = startAngle + i * step;
                pts[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            }
            return pts;
        }

        /// <summary>
        /// Masks a square image into an octagon by covering all area outside the octagon
        /// with the background color. Draws a triangle fan from each square corner to its
        /// two nearest octagon vertices, plus fills the edge strips between.
        /// </summary>
        public static void MaskSquareToOctagon(ImDrawListPtr drawList, Vector2 center, float radius, Vector2[] oct)
        {
            if (oct.Length < 8) return;
            uint bg = ImGui.ColorConvertFloat4ToU32(ThemeManager.BgDark);

            float r = radius + 1f;
            // Square corners
            Vector2 tl = center + new Vector2(-r, -r);
            Vector2 tr = center + new Vector2(r, -r);
            Vector2 br = center + new Vector2(r, r);
            Vector2 bl = center + new Vector2(-r, r);
            // Square edge midpoints
            Vector2 tm = center + new Vector2(0, -r);
            Vector2 rm = center + new Vector2(r, 0);
            Vector2 bm = center + new Vector2(0, r);
            Vector2 lm = center + new Vector2(-r, 0);

            // Octagon vertices (point-up octagon, clockwise):
            // 0=top, 1=upper-right, 2=right, 3=lower-right, 4=bottom, 5=lower-left, 6=left, 7=upper-left

            // Top-right corner area (between oct[0]=top and oct[1]=upper-right, through tr corner)
            drawList.AddTriangleFilled(oct[0], tm, tr, bg);
            drawList.AddTriangleFilled(oct[0], tr, oct[1], bg);
            drawList.AddTriangleFilled(oct[1], tr, rm, bg);

            // Bottom-right corner area (between oct[2]=right and oct[3]=lower-right, through br corner)
            drawList.AddTriangleFilled(oct[2], rm, br, bg);
            drawList.AddTriangleFilled(oct[2], br, oct[3], bg);
            drawList.AddTriangleFilled(oct[3], br, bm, bg);

            // Bottom-left corner area (between oct[4]=bottom and oct[5]=lower-left, through bl corner)
            drawList.AddTriangleFilled(oct[4], bm, bl, bg);
            drawList.AddTriangleFilled(oct[4], bl, oct[5], bg);
            drawList.AddTriangleFilled(oct[5], bl, lm, bg);

            // Top-left corner area (between oct[6]=left and oct[7]=upper-left, through tl corner)
            drawList.AddTriangleFilled(oct[6], lm, tl, bg);
            drawList.AddTriangleFilled(oct[6], tl, oct[7], bg);
            drawList.AddTriangleFilled(oct[7], tl, tm, bg);
        }

        public static void DrawFilledOctagon(ImDrawListPtr drawList, Vector2[] points, uint color)
        {
            if (points.Length < 3) return;
            Vector2 center = Vector2.Zero;
            foreach (var p in points) center += p;
            center /= points.Length;
            for (int i = 0; i < points.Length; i++)
            {
                int next = (i + 1) % points.Length;
                drawList.AddTriangleFilled(center, points[i], points[next], color);
            }
        }
    }
}
