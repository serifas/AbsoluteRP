using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI;
using AbsoluteRP.Windows.Listings;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AbsoluteRP.Network;

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

        // Connection mode: when set, next skill click creates a parent->child link
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

        private static readonly List<RsElements.NavItem> ClassSubNavItems = new()
        {
            new(FontAwesomeIcon.InfoCircle, "Details"),
            new(FontAwesomeIcon.ProjectDiagram, "Skill Trees"),
            new(FontAwesomeIcon.Star, "Default Passives"),
        };

        public static void DrawSkillsEditor()
        {
            var system = SystemsWindow.currentSystem;
            if (system == null) return;

            WindowOperations.LoadIconsLazy(Plugin.plugin);

            // Class Selector
            bool clsOpen = RsElements.BeginPanel("sys_skill_classes", "Classes", fitContentsY: true);
            try
            {
                if (clsOpen)
                {
                    DrawClassSelector(system);

                    if (selectedClassIndex < 0 || selectedClassIndex >= system.SkillClasses.Count)
                    {
                        SysUI.Gap(6f);
                        SysUI.MutedWrapped(system.SkillClasses.Count == 0
                            ? "No classes yet. Use \"+ Class\" to create one to begin."
                            : "Select or create a class to begin.");
                    }
                }
            }
            finally { RsElements.EndPanel(); }

            if (selectedClassIndex < 0 || selectedClassIndex >= system.SkillClasses.Count)
            {
                DrawIconPickerPopup(system);
                return;
            }

            var cls = system.SkillClasses[selectedClassIndex];

            SysUI.Gap(12f);

            // Class Sub-Tabs
            if (classSubTab < 0 || classSubTab > 2) classSubTab = 0;
            RsElements.NavigationMenu("sys_class_subnav", ref classSubTab, ClassSubNavItems);
            SysUI.Gap(10f);

            switch (classSubTab)
            {
                case 0: DrawClassDetails(system, cls); break;
                case 1: DrawSkillTreesSection(system, cls); break;
                case 2: DrawPassivesSection(system, cls); break;
            }

            // Icon picker popup (shared)
            DrawIconPickerPopup(system);
        }

        // Class icon picker state
        private static bool showClassIconPicker = false;

        // Class Details Sub-Tab
        private static void DrawClassDetails(SystemData system, SkillClassData cls)
        {
            bool open = RsElements.BeginPanel("sys_class_details", string.IsNullOrEmpty(cls.name) ? "Class" : cls.name, fitContentsY: true);
            try
            {
                if (open)
                {
                    // Class icon
                    SysUI.SectionLabel("Class Icon");
                    float iconSz = ImGui.GetTextLineHeight() + SysUI.S(18f);
                    if (cls.iconTexture != null && cls.iconTexture.Handle != IntPtr.Zero)
                    {
                        var p = ImGui.GetCursorScreenPos();
                        var dl = ImGui.GetWindowDrawList();
                        var c = p + new Vector2(iconSz, iconSz) * 0.5f;
                        var oct = GetOctagonPoints(c, iconSz * 0.5f);
                        DrawOctagonImage(dl, cls.iconTexture, c, iconSz * 0.5f, oct, 0xFFFFFFFF);
                        dl.AddPolyline(ref oct[0], oct.Length, RsTheme.U.BorderStrong, ImDrawFlags.Closed, SysUI.S(1.5f));
                        ImGui.Dummy(new Vector2(iconSz, iconSz));
                        ImGui.SameLine(0f, SysUI.S(10f));
                    }
                    if (RsElements.Button("Change Icon##classIcon", RsElements.ButtonVariant.Secondary))
                        showClassIconPicker = true;
                    if (cls.iconId > 0)
                    {
                        ImGui.SameLine(0f, SysUI.S(8f));
                        if (RsElements.Button("Clear##clearClassIcon", RsElements.ButtonVariant.Ghost))
                        {
                            cls.iconId = 0;
                            cls.iconTexture = null;
                        }
                    }

                    SysUI.Gap(8f);

                    // Class name
                    SysUI.SectionLabel("Class Name");
                    string cname = cls.name;
                    if (RsElements.InputText("className", ref cname, 64, "Class name"))
                        cls.name = cname;

                    SysUI.Gap(8f);

                    // Class description
                    SysUI.SectionLabel("Description (visible to players)");
                    string cdesc = cls.description ?? "";
                    var areaSize = new Vector2(RsElements.AvailContentWidth(), SysUI.S(90f));
                    if (RsElements.InputTextArea("classDesc", ref cdesc, 2000, "Describe this class...", areaSize))
                        cls.description = cdesc;

                    SysUI.Gap(8f);

                    // Initial skill points
                    using (SysUI.Field("Initial Skill Points"))
                    {
                        int isp = cls.initialSkillPoints;
                        if (SysUI.InputInt("initSkillPtsDetail", ref isp))
                            cls.initialSkillPoints = Math.Max(0, isp);
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Initial skill points players receive for this class.\n0 = unlimited (all skills available).");
                    }
                    SysUI.Muted("0 = unlimited (all skills available)");
                }
            }
            finally { RsElements.EndPanel(); }

            SysUI.Gap(10f);

            // Save button
            if (RsElements.Button("Save Class##saveClass", RsElements.ButtonVariant.Primary))
            {
                if (system.id > 0 && Plugin.character != null)
                {
                    AbsoluteRP.Network.Systems_DS.SaveSkillClasses(Plugin.character, system.id, system.SkillClasses);
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
            SysUI.PushWindowStyle();
            try
            {
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
            finally { SysUI.PopWindowStyle(); }
        }

        // Skill Trees Sub-Tab
        private static void DrawSkillTreesSection(SystemData system, SkillClassData cls)
        {
            // Tree selector tabs
            DrawTreeSelector(system);

            SysUI.Gap(10f);

            // Split: left = grid, right = detail panel
            float panelWidth = ImGui.GetContentRegionAvail().X;
            float gapX = SysUI.S(12f);
            float detailWidth = SysUI.S(280f);
            float gridWidth = panelWidth - detailWidth - gapX;
            if (gridWidth < SysUI.S(300f)) { gridWidth = panelWidth; detailWidth = 0; }

            float saveRowH = ImGui.GetTextLineHeight() + SysUI.S(18f) + SysUI.S(16f);
            float childH = Math.Max(SysUI.S(320f), ImGui.GetContentRegionAvail().Y - saveRowH);

            // Grid on a solid surface so the octagon icons read cleanly.
            ImGui.PushStyleColor(ImGuiCol.ChildBg, RsTheme.BgSecondary);
            ImGui.PushStyleColor(ImGuiCol.Border, RsTheme.Border);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, SysUI.S(8f));
            ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, RsTheme.BorderThickness);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, SysUI.S(12f, 12f));
            try
            {
                if (ImGui.BeginChild("##SkillGrid", new Vector2(gridWidth, childH), true))
                {
                    DrawSkillGrid(system, ImGui.GetContentRegionAvail().X);
                }
                ImGui.EndChild();
            }
            finally
            {
                ImGui.PopStyleVar(3);
                ImGui.PopStyleColor(2);
            }

            if (detailWidth > 0)
            {
                ImGui.SameLine(0f, gapX);
                bool detailOpen = RsElements.BeginPanel("sys_skill_detail", "Skill Details", new Vector2(detailWidth, childH));
                try
                {
                    if (detailOpen)
                        DrawSkillDetailPanel(system);
                }
                finally { RsElements.EndPanel(); }
            }

            // Save
            SysUI.Gap(10f);
            if (RsElements.Button("Save Skills##saveSkills", RsElements.ButtonVariant.Primary))
            {
                if (system.id > 0 && Plugin.character != null)
                {
                    AbsoluteRP.Network.Systems_DS.SaveSkillClasses(Plugin.character, system.id, system.SkillClasses);
                    AbsoluteRP.Network.Systems_DS.SaveSkills(Plugin.character, system.id, system.Skills, system.SkillConnections);
                }
            }
        }

        // Default Passives Sub-Tab
        private static void DrawPassivesSection(SystemData system, SkillClassData cls)
        {
            int classId = cls.id;
            var passives = system.Skills.Where(s => s.classId == classId && !s.isCastable && s.gridX == -1).ToList();

            bool open = RsElements.BeginPanel("sys_class_passives", "Default Passives", fitContentsY: true);
            try
            {
                if (open)
                {
                    SysUI.MutedWrapped("Passives that all members of this class start with.");
                    SysUI.Gap(6f);

                    if (RsElements.Button("+ Add Passive##addPassive", RsElements.ButtonVariant.Primary))
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

                    SysUI.Gap(8f);

                    for (int i = 0; i < passives.Count; i++)
                    {
                        var p = passives[i];
                        bool removeNow = false;
                        ImGui.PushID($"passive_{i}");
                        try
                        {
                            var draw = ImGui.GetWindowDrawList();
                            var cardMin = ImGui.GetCursorScreenPos();
                            float cardW = RsElements.AvailContentWidth();
                            float inset = SysUI.S(12f);
                            float innerW = Math.Max(SysUI.S(120f), cardW - inset * 2f);

                            ImGui.SetCursorScreenPos(cardMin + new Vector2(inset, SysUI.S(10f)));
                            ImGui.BeginGroup();
                            try
                            {
                                float delW = SysUI.S(28f);
                                string pname = p.name;
                                if (RsElements.InputText("passName", ref pname, 64, "Passive name", (innerW - delW - SysUI.S(8f)) / RsTheme.Scale))
                                    p.name = pname;

                                ImGui.SameLine(0f, SysUI.S(8f));
                                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (ImGui.GetTextLineHeight() + SysUI.S(16f) - delW) * 0.5f);
                                if (RsElements.IconButton(FontAwesomeIcon.Trash, "delPassive", RsElements.ButtonVariant.Danger, 28f))
                                    removeNow = true;
                                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Remove passive");

                                SysUI.Gap(4f);
                                string pdesc = p.description ?? "";
                                if (RsElements.InputTextArea("passDesc", ref pdesc, 500, "What does this passive do?", new Vector2(innerW, SysUI.S(60f))))
                                    p.description = pdesc;
                            }
                            finally { ImGui.EndGroup(); }

                            var groupMax = ImGui.GetItemRectMax();
                            var cardMax = new Vector2(cardMin.X + cardW, groupMax.Y + SysUI.S(10f));
                            draw.AddRect(cardMin, cardMax, RsTheme.U.Border, SysUI.S(8f), ImDrawFlags.None, RsTheme.BorderThickness);
                            ImGui.SetCursorScreenPos(new Vector2(cardMin.X, cardMax.Y));
                            ImGui.Dummy(new Vector2(cardW, SysUI.S(8f)));
                        }
                        finally { ImGui.PopID(); }

                        if (removeNow)
                            system.Skills.Remove(p);
                    }

                    if (passives.Count == 0)
                        SysUI.Muted("No passives yet.");
                }
            }
            finally { RsElements.EndPanel(); }

            // Save
            SysUI.Gap(10f);
            if (RsElements.Button("Save Passives##savePassives", RsElements.ButtonVariant.Primary))
            {
                if (system.id > 0 && Plugin.character != null)
                {
                    AbsoluteRP.Network.Systems_DS.SaveSkills(Plugin.character, system.id, system.Skills, system.SkillConnections);
                }
            }
        }

        // Class Selector
        private static void DrawClassSelector(SystemData system)
        {
            float gap = SysUI.S(12f);

            var options = new List<string>(system.SkillClasses.Count + 1) { "All Skills" };
            for (int i = 0; i < system.SkillClasses.Count; i++)
                options.Add(system.SkillClasses[i].name);

            using (SysUI.Field("Skill Class"))
            {
                int sel = (selectedClassIndex >= 0 && selectedClassIndex < system.SkillClasses.Count) ? selectedClassIndex + 1 : 0;
                if (RsElements.Dropdown("SkillClass", ref sel, options, 220f))
                {
                    if (sel <= 0)
                    {
                        selectedClassIndex = -1;
                        selectedTreeIndex = 0;
                    }
                    else
                    {
                        selectedClassIndex = sel - 1;
                        selectedTreeIndex = 0;
                        selectedSlot = null;
                        selectedSkillIndex = -1;
                        classSubTab = 0;
                        editClassDesc = "";
                        connectingFromSkillId = null;
                    }
                }
            }

            bool hasClass = selectedClassIndex >= 0 && selectedClassIndex < system.SkillClasses.Count;
            if (hasClass)
            {
                ImGui.SameLine(0f, gap);
                var cls = system.SkillClasses[selectedClassIndex];
                using (SysUI.Field("Skill Points"))
                {
                    int isp = cls.initialSkillPoints;
                    if (SysUI.InputInt("initSkillPts", ref isp))
                        cls.initialSkillPoints = Math.Max(0, isp);
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Initial skill points players can spend on this class's skill trees.");
                }
            }

            ImGui.SameLine(0f, gap);
            using (SysUI.Field(" "))
            {
                if (RsElements.Button("+ Class##addClass", RsElements.ButtonVariant.Primary))
                    ImGui.OpenPopup("##NewClassPopup");

                if (hasClass)
                {
                    ImGui.SameLine(0f, SysUI.S(8f));
                    if (RsElements.Button("Delete##delClass", RsElements.ButtonVariant.Danger))
                        ImGui.OpenPopup("ConfirmDeleteClass##confirmDelClass");
                }
            }

            if (SysUI.BeginPopup("##NewClassPopup"))
            {
                SysUI.SectionLabel("New Class");
                RsElements.InputText("newClassName", ref newClassName, 64, "Class name", 200f);
                SysUI.Gap(4f);
                if (RsElements.Button("Create##createClass", RsElements.ButtonVariant.Primary) && !string.IsNullOrWhiteSpace(newClassName))
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
                SysUI.EndPopup();
            }

            if (hasClass && SysUI.BeginModal("ConfirmDeleteClass##confirmDelClass", ref _deleteClassPopupOpen))
            {
                bool deleted = false;
                var className = system.SkillClasses[selectedClassIndex].name;
                SysUI.Primary($"Are you sure you want to delete class \"{className}\"?");
                SysUI.Text("All skills and connections in this class will be removed.", RsTheme.AccentDanger);
                SysUI.Gap(6f);

                if (RsElements.Button("Delete##confirmDelClassBtn", RsElements.ButtonVariant.Danger))
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
                    deleted = true;
                }
                if (!deleted)
                {
                    ImGui.SameLine();
                    if (RsElements.Button("Cancel##cancelDelClass", RsElements.ButtonVariant.Secondary))
                    {
                        ImGui.CloseCurrentPopup();
                    }
                }
                SysUI.EndModal();
            }
        }

        // Tree Selector (multiple trees per class)
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

            // Toolbar: tree actions
            SysUI.SectionLabel($"Skill Trees ({cls.SkillTrees.Count})");
            if (RsElements.Button("+ New Tree##addTree", RsElements.ButtonVariant.Secondary))
                openAddTreePopup = true;

            // Delete tree button (only if more than 1)
            if (cls.SkillTrees.Count > 1 && selectedTreeIndex >= 0 && selectedTreeIndex < cls.SkillTrees.Count)
            {
                ImGui.SameLine(0f, SysUI.S(8f));
                if (RsElements.Button("Delete Tree##delTree", RsElements.ButtonVariant.Danger))
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

            SysUI.Gap(8f);

            // Tree tab strip
            var treeItems = new List<RsElements.NavItem>(cls.SkillTrees.Count);
            for (int t = 0; t < cls.SkillTrees.Count; t++)
                treeItems.Add(new RsElements.NavItem(FontAwesomeIcon.ProjectDiagram,
                    string.IsNullOrEmpty(cls.SkillTrees[t].name) ? $"Tree {t + 1}" : cls.SkillTrees[t].name));
            int treeSel = selectedTreeIndex;
            if (RsElements.NavigationMenu($"sys_tree_nav_{cls.id}", ref treeSel, treeItems))
            {
                if (selectedTreeIndex != treeSel)
                {
                    selectedTreeIndex = treeSel;
                    selectedSlot = null;
                    selectedSkillIndex = -1;
                }
            }

            // Open popup outside the tab strip
            if (openAddTreePopup)
            {
                ImGui.OpenPopup("##NewTreePopup");
                openAddTreePopup = false;
            }

            // New tree popup
            if (SysUI.BeginPopup("##NewTreePopup"))
            {
                SysUI.SectionLabel("New Tree");
                RsElements.InputText("newTreeName", ref newTreeName, 64, "Tree name", 200f);
                SysUI.Gap(4f);
                if (RsElements.Button("Create##createTree", RsElements.ButtonVariant.Primary) && !string.IsNullOrWhiteSpace(newTreeName))
                {
                    cls.SkillTrees.Add(new SkillTreeData { name = newTreeName.Trim(), sortOrder = cls.SkillTrees.Count });
                    selectedTreeIndex = cls.SkillTrees.Count - 1;
                    selectedSlot = null;
                    selectedSkillIndex = -1;
                    newTreeName = "";
                    ImGui.CloseCurrentPopup();
                }
                SysUI.EndPopup();
            }
        }

        // Skill Grid
        private static void DrawSkillGrid(SystemData system, float gridWidth)
        {
            var drawList = ImGui.GetWindowDrawList();
            Vector2 origin = ImGui.GetCursorScreenPos();

            float cellSize = Math.Min((gridWidth - SysUI.S(8f)) / GridCols, SysUI.S(70f));
            float octRadius = cellSize * 0.35f;

            uint accent = RsTheme.U.AccentPrimary;
            uint accentMuted = SysUI.U(SysUI.Fade(RsTheme.AccentPrimary, 0.45f));

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
                    drawList.AddLine(from, to, accentMuted, SysUI.S(2f));

                    // Arrow head at midpoint pointing toward child
                    Vector2 mid = (from + to) / 2;
                    Vector2 dir = Vector2.Normalize(to - from);
                    Vector2 perp = new Vector2(-dir.Y, dir.X);
                    float arrowSize = SysUI.S(6f);
                    drawList.AddTriangleFilled(
                        mid + dir * arrowSize,
                        mid - dir * arrowSize + perp * arrowSize,
                        mid - dir * arrowSize - perp * arrowSize,
                        accentMuted);

                    // Required points label
                    if (conn.requiredPoints > 1)
                    {
                        string reqText = $"{conn.requiredPoints}";
                        var textSize = ImGui.CalcTextSize(reqText);
                        drawList.AddText(mid - textSize / 2 + new Vector2(0, -SysUI.S(10f)), RsTheme.U.TextPrimary, reqText);
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
                    drawList.AddLine(from, mousePos, SysUI.U(SysUI.Fade(RsTheme.AccentWarning, 0.7f)), SysUI.S(2f));
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
                        uint fillColor = isSelected ? accent : SysUI.U(RsTheme.BgTertiary);

                        if (skill.iconTexture != null && skill.iconTexture.Handle != IntPtr.Zero)
                        {
                            // Icon clipped to the octagon
                            DrawOctagonImage(drawList, skill.iconTexture, center, octRadius, octPoints, 0xFFFFFFFF);
                        }
                        else
                        {
                            // No icon - filled octagon with name text
                            DrawFilledOctagon(drawList, octPoints, fillColor);
                            string label = skill.name.Length > 6 ? skill.name[..6] + ".." : skill.name;
                            var textSize = ImGui.CalcTextSize(label);
                            drawList.AddText(center - textSize / 2, RsTheme.U.TextPrimary, label);
                        }

                        // Octagon border (always on top)
                        uint borderColor = isSelected ? accent : accentMuted;
                        drawList.AddPolyline(ref octPoints[0], octPoints.Length, borderColor, ImDrawFlags.Closed, isSelected ? SysUI.S(3f) : SysUI.S(2f));

                        // Tier indicator (top-right corner) - white text with black shadow
                        if (skill.maxTiers > 1)
                        {
                            string tierLabel = $"T{skill.maxTiers}";
                            Vector2 tierPos = center + new Vector2(octRadius * 0.3f, -octRadius * 0.9f);
                            DrawOutlinedText(drawList, tierLabel, tierPos, 0xFFFFFFFF, 0xFF000000);
                        }

                        // Passive indicator (bottom-right) - white text with black shadow
                        if (!skill.isCastable)
                        {
                            Vector2 passivePos = center + new Vector2(octRadius * 0.5f, octRadius * 0.4f);
                            DrawOutlinedText(drawList, "P", passivePos, 0xFFFFFFFF, 0xFF000000);
                        }
                    }
                    else
                    {
                        // Empty slot
                        uint outlineColor = isSelected ? accent : SysUI.U(SysUI.Fade(RsTheme.Border, 0.6f));
                        drawList.AddPolyline(ref octPoints[0], octPoints.Length, outlineColor, ImDrawFlags.Closed, SysUI.S(1f));
                    }

                    // Click detection
                    ImGui.SetCursorScreenPos(center - new Vector2(octRadius, octRadius));
                    if (ImGui.InvisibleButton($"##slot_{x}_{y}", new Vector2(octRadius * 2, octRadius * 2)))
                    {
                        if (connectingFromSkillId.HasValue && hasSkill)
                        {
                            // Complete connection: parent -> this child
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

                    // Hover ring on empty slots so the grid feels clickable
                    if (!hasSkill && ImGui.IsItemHovered())
                        drawList.AddPolyline(ref octPoints[0], octPoints.Length, accentMuted, ImDrawFlags.Closed, SysUI.S(1.5f));

                    // Tooltip (matches Tree tab formatting)
                    if (hasSkill && ImGui.IsItemHovered())
                    {
                        SysUI.BeginTooltip();
                        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20f);

                        // Name (bold-style via accent text)
                        Misc.RenderHtmlElements(skill.name, false, true, true, true, null, true);
                        ImGui.Separator();

                        // Description
                        if (!string.IsNullOrEmpty(skill.description))
                        {
                            Misc.RenderHtmlElements(skill.description, false, true, true, true, null, true);
                            ImGui.Spacing();
                        }

                        // Type + stats
                        SysUI.Text(skill.isCastable ? "Castable" : "Passive", skill.isCastable ? RsTheme.AccentPrimary : RsTheme.TextMuted);
                        if (skill.maxTiers > 1)
                        {
                            ImGui.SameLine();
                            SysUI.Secondary($"| Tiers: {skill.maxTiers}");
                        }
                        if (skill.cooldownTurns > 0) SysUI.Secondary($"Cooldown: {skill.cooldownTurns} turns");
                        if (skill.resourceCost > 0) SysUI.Secondary($"Cost: {skill.resourceCost}");

                        // Parents
                        var parents = system.SkillConnections.Where(c => c.toSkillId == skill.id).ToList();
                        if (parents.Count > 0)
                        {
                            ImGui.Separator();
                            SysUI.Muted("Requires:");
                            foreach (var p in parents)
                            {
                                var parentSkill = system.Skills.FirstOrDefault(s => s.id == p.fromSkillId);
                                if (parentSkill != null)
                                    SysUI.Secondary($"  {parentSkill.name} ({p.requiredPoints} pts)");
                            }
                        }

                        ImGui.PopTextWrapPos();
                        SysUI.EndTooltip();
                    }
                }
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(GridCols * cellSize, GridRows * cellSize));
        }

        // Detail Panel
        private static void DrawSkillDetailPanel(SystemData system)
        {
            if (selectedSkillIndex < 0 || selectedSkillIndex >= system.Skills.Count)
            {
                SysUI.MutedWrapped("Click a grid slot to create or select a skill.");
                return;
            }

            var skill = system.Skills[selectedSkillIndex];
            float fullW = RsElements.AvailContentWidth();

            // Icon preview + pick button
            SysUI.SectionLabel("Icon");
            float iconSz = ImGui.GetTextLineHeight() + SysUI.S(18f);
            if (skill.iconTexture != null && skill.iconTexture.Handle != IntPtr.Zero)
            {
                var p = ImGui.GetCursorScreenPos();
                var dl = ImGui.GetWindowDrawList();
                var c = p + new Vector2(iconSz, iconSz) * 0.5f;
                var oct = GetOctagonPoints(c, iconSz * 0.5f);
                DrawOctagonImage(dl, skill.iconTexture, c, iconSz * 0.5f, oct, 0xFFFFFFFF);
                dl.AddPolyline(ref oct[0], oct.Length, RsTheme.U.BorderStrong, ImDrawFlags.Closed, SysUI.S(1.5f));
                ImGui.Dummy(new Vector2(iconSz, iconSz));
                ImGui.SameLine(0f, SysUI.S(8f));
            }
            if (RsElements.Button("Choose Icon##pickIcon", RsElements.ButtonVariant.Secondary))
            {
                showIconPicker = true;
            }
            if (skill.iconId > 0)
            {
                ImGui.SameLine(0f, SysUI.S(8f));
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + SysUI.S(9f));
                SysUI.Muted($"#{skill.iconId}");
            }
            SysUI.Gap(6f);

            // Name
            SysUI.SectionLabel("Name");
            if (RsElements.InputText("skillName", ref editName, 64, "Skill name"))
                skill.name = editName;

            SysUI.Gap(4f);

            // Description
            SysUI.SectionLabel("Description");
            if (RsElements.InputTextArea("skillDesc", ref editDesc, 500, "What does it do?", new Vector2(fullW, SysUI.S(70f))))
                skill.description = editDesc;

            SysUI.Gap(6f);

            // Castable / Passive
            bool castable = editCastable;
            if (RsElements.Toggle("skillCast", ref castable, castable ? "Castable" : "Castable (Passive)"))
            {
                editCastable = castable;
                skill.isCastable = castable;
            }

            if (skill.isCastable)
            {
                SysUI.Gap(6f);
                using (SysUI.Field("Cooldown (turns)"))
                {
                    if (SysUI.InputInt("skillCD", ref editCooldown))
                    {
                        editCooldown = Math.Max(0, editCooldown);
                        skill.cooldownTurns = editCooldown;
                    }
                }
            }

            SysUI.Divider();

            // Resource
            SysUI.SectionLabel("Uses Resource");
            if (editResourceId >= 0)
            {
                var res = system.Resources.FirstOrDefault(r => r.id == editResourceId);
                if (res == null) editResourceId = -1;
            }
            var resOptions = new List<string>(system.Resources.Count + 1) { "None" };
            int resSel = 0;
            for (int i = 0; i < system.Resources.Count; i++)
            {
                resOptions.Add(system.Resources[i].name);
                if (editResourceId >= 0 && system.Resources[i].id == editResourceId && resSel == 0) resSel = i + 1;
            }
            if (RsElements.Dropdown("skillRes", ref resSel, resOptions))
            {
                if (resSel <= 0)
                {
                    editResourceId = -1;
                    skill.resourceId = -1;
                }
                else
                {
                    var r = system.Resources[resSel - 1];
                    editResourceId = r.id;
                    skill.resourceId = r.id;
                }
            }

            if (editResourceId >= 0)
            {
                SysUI.Gap(6f);
                using (SysUI.Field("Resource Cost"))
                {
                    if (SysUI.InputInt("skillResCost", ref editResourceCost))
                    {
                        editResourceCost = Math.Max(0, editResourceCost);
                        skill.resourceCost = editResourceCost;
                    }
                }
            }

            SysUI.Gap(6f);

            // Tiers (ranks/levels for this skill)
            using (SysUI.Field("Max Tiers"))
            {
                if (SysUI.InputInt("skillTiers", ref editMaxTiers))
                {
                    editMaxTiers = Math.Clamp(editMaxTiers, 1, 10);
                    skill.maxTiers = editMaxTiers;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("How many times this skill can be ranked up.\n1 = single rank, 2+ = tiered skill.");
            }

            SysUI.Divider();

            // Connect to child button
            SysUI.SectionLabel("Connections");
            if (connectingFromSkillId.HasValue && connectingFromSkillId.Value == skill.id)
            {
                SysUI.Text("Click another skill to connect...", RsTheme.AccentWarning);
                if (RsElements.Button("Cancel##cancelConnect", RsElements.ButtonVariant.Secondary, new Vector2(fullW, 0)))
                    connectingFromSkillId = null;
            }
            else
            {
                if (RsElements.Button("Connect to Child##connect", RsElements.ButtonVariant.Primary, new Vector2(fullW, 0)))
                    connectingFromSkillId = skill.id;
            }

            // Show existing connections FROM this skill
            var childConns = system.SkillConnections.Where(c => c.fromSkillId == skill.id).ToList();
            if (childConns.Count > 0)
            {
                SysUI.Gap(6f);
                SysUI.Muted("Children");
                foreach (var conn in childConns)
                {
                    var child = system.Skills.FirstOrDefault(s => s.id == conn.toSkillId);
                    if (child == null) continue;
                    ImGui.PushID($"conn_{conn.fromSkillId}_{conn.toSkillId}");
                    try
                    {
                        float rowH = ImGui.GetTextLineHeight() + SysUI.S(16f);
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (rowH - ImGui.GetTextLineHeight()) * 0.5f);
                        SysUI.Secondary($"-> {child.name}");
                        ImGui.SameLine(0f, SysUI.S(8f));
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - (rowH - ImGui.GetTextLineHeight()) * 0.5f);
                        int req = conn.requiredPoints;
                        if (SysUI.InputInt("connReq", ref req, 90f))
                            conn.requiredPoints = Math.Max(1, req);
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Points required in this skill before child unlocks");
                        ImGui.SameLine(0f, SysUI.S(6f));
                        if (RsElements.IconButton(FontAwesomeIcon.Times, "delConn", RsElements.ButtonVariant.Danger, 28f))
                            system.SkillConnections.Remove(conn);
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Remove connection");
                    }
                    finally { ImGui.PopID(); }
                }
            }

            // Show parent connections TO this skill
            var parentConns = system.SkillConnections.Where(c => c.toSkillId == skill.id).ToList();
            if (parentConns.Count > 0)
            {
                SysUI.Gap(6f);
                SysUI.Muted("Requires");
                foreach (var conn in parentConns)
                {
                    var parent = system.Skills.FirstOrDefault(s => s.id == conn.fromSkillId);
                    if (parent == null) continue;
                    SysUI.Secondary($"  {parent.name} ({conn.requiredPoints} pts)");
                }
            }

            SysUI.Divider();

            if (RsElements.Button("Delete Skill##delSkill", RsElements.ButtonVariant.Danger, new Vector2(fullW, 0)))
            {
                system.SkillConnections.RemoveAll(c => c.fromSkillId == skill.id || c.toSkillId == skill.id);
                system.Skills.RemoveAt(selectedSkillIndex);
                selectedSkillIndex = -1;
                selectedSlot = null;
                connectingFromSkillId = null;
            }
        }

        // Icon Picker Popup
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
            SysUI.PushWindowStyle();
            try
            {
                // End() must run whether or not Begin() returned true.
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
                }
                ImGui.End();
            }
            finally { SysUI.PopWindowStyle(); }
        }

        // Helpers

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

        /// Draws a texture clipped to an octagon by emitting one textured triangle per octagon edge (as degenerate quads). Unlike MaskSquareToOctagon this needs no background colour, so it works on any surface.
        public static void DrawOctagonImage(ImDrawListPtr drawList, IDalamudTextureWrap texture, Vector2 center, float radius, Vector2[] oct, uint tint)
        {
            if (texture == null || oct.Length < 3) return;
            Vector2 Uv(Vector2 p) => new Vector2(
                Math.Clamp((p.X - center.X) / (radius * 2f) + 0.5f, 0f, 1f),
                Math.Clamp((p.Y - center.Y) / (radius * 2f) + 0.5f, 0f, 1f));
            var uvC = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < oct.Length; i++)
            {
                var a = oct[i];
                var b = oct[(i + 1) % oct.Length];
                var uvA = Uv(a);
                var uvB = Uv(b);
                drawList.AddImageQuad(texture.Handle, center, a, b, b, uvC, uvA, uvB, uvB, tint);
            }
        }

        /// Masks a square image into an octagon by covering all area outside the octagon with the background color. Draws a triangle fan from each square corner to its two nearest octagon vertices, plus fills the edge strips between. (Kept for compatibility; the Systems screens now use DrawOctagonImage.)
        public static void MaskSquareToOctagon(ImDrawListPtr drawList, Vector2 center, float radius, Vector2[] oct)
        {
            if (oct.Length < 8) return;
            uint bg = RsTheme.U.BgSecondary;

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

            drawList.AddTriangleFilled(oct[0], tm, tr, bg);
            drawList.AddTriangleFilled(oct[0], tr, oct[1], bg);
            drawList.AddTriangleFilled(oct[1], tr, rm, bg);

            drawList.AddTriangleFilled(oct[2], rm, br, bg);
            drawList.AddTriangleFilled(oct[2], br, oct[3], bg);
            drawList.AddTriangleFilled(oct[3], br, bm, bg);

            drawList.AddTriangleFilled(oct[4], bm, bl, bg);
            drawList.AddTriangleFilled(oct[4], bl, oct[5], bg);
            drawList.AddTriangleFilled(oct[5], bl, lm, bg);

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
