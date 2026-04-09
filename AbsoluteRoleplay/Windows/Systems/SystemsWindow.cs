using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Ect;
using AbsoluteRP.Windows.NavLayouts;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using AbsoluteRP.Windows.Social.Views;
using AbsoluteRP.Windows.Systems.Stats;
using AbsoluteRP.Windows.Systems.Combat;
using AbsoluteRP.Windows.Systems.Rules;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Common.Lua;
using Networking;
using Serilog.Filters;
using System.Numerics;
using System.Reflection;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode.Delegates;

namespace AbsoluteRP.Windows.Listings
{
    internal class SystemsWindow : Window, IDisposable
    {
        public static Configuration configuration;

        public static int currentSystemIndex = -1;
        public static int systemNavIndex = 0;
        public static bool drawStatLayout = false;
        public static SystemData? currentSystem = null;
        public static List<SystemData> systemData = new List<SystemData>();
        public static bool uiSelected = false;
        private static bool fetchedSystems = false;

        // Top-level mode: 0=View Systems, 1=Manage Systems
        public static int systemMode = 1;

        // Section tabs: 0=Stats, 1=Classes, 2=Combat, 3=Rules, 4=Roster
        public static int systemSectionIndex = 0;
        private static readonly string[] SectionNames = { "Stats", "Classes", "Combat", "Rules", "Roster" };
        public SystemsWindow() : base(
            "SYSTEMS")
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(200, 400),
                MaximumSize = new Vector2(1000, 1000)
            };

            configuration = Plugin.plugin.Configuration;
        }

        public override void Draw()
        {

            if (ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows)
                && ImGui.IsMouseClicked(ImGuiMouseButton.Left)
                && !ImGui.IsAnyItemActive()
                && !ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId)
                && !uiSelected)
            {
                ImGui.SetWindowFocus("SystemNavigation");
                ImGui.SetWindowFocus("SYSTEMS");
            }
            var hovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows);
            var clicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left);
            var anyActive = ImGui.IsAnyItemActive();
            var alreadyFocused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
            var focusRequested = hovered && clicked && !anyActive && !alreadyFocused;
            // Main panel position/size
            Vector2 mainPanelPos = ImGui.GetWindowPos();
            Vector2 mainPanelSize = ImGui.GetWindowSize();

            // Navigation panel
            float headerHeight = 48f;
            float buttonSize = ImGui.GetIO().FontGlobalScale * 45;
            int buttonCount = 5;
            float navHeight = buttonSize * buttonCount * 1.2f;

            // Top-level mode tabs
            if (ImGui.BeginTabBar("##SystemModeBar"))
            {
                if (ImGui.BeginTabItem("View Systems"))
                {
                    systemMode = 0;
                    ImGui.Spacing();
                    AbsoluteRP.Windows.Systems.ViewSystems.ViewSystems.DrawViewSystems();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Manage Systems"))
                {
                    systemMode = 1;
                    ImGui.Spacing();
                    DrawSystemCreation();
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }

            ImGui.SetNextWindowPos(new Vector2(mainPanelPos.X - buttonSize * 1.5f, mainPanelPos.Y + headerHeight), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(buttonSize * 1.5f, navHeight), ImGuiCond.Always);

            ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar;
            Navigation nav = NavigationLayouts.SystemNavigation();
            UIHelpers.DrawSideNavigation("SYSTEMS", "SystemNavigation", ref systemNavIndex, flags, nav, focusRequested);
        }

        public static void DrawSystemCreation()
        {
            // Fetch systems from server on first draw
            if (!fetchedSystems && Plugin.character != null)
            {
                fetchedSystems = true;
                Networking.DataSender.FetchMySystems(Plugin.character);
            }

            // Create new system
            if (ThemeManager.PillButton("Create System"))
            {
                var newSystem = new SystemData() { name = "New System", description = string.Empty, StatsData = new SortedList<int, StatData>() };
                systemData.Add(newSystem);
                currentSystemIndex = systemData.Count - 1;
                currentSystem = newSystem;
                drawStatLayout = true;
                systemSectionIndex = 0;
                Stats.currentStatIndex = -1;
                Stats.selectedStat = null;

                // Send to server to get an ID
                if (Plugin.character != null)
                    Networking.DataSender.CreateSystem(Plugin.character, newSystem.name, newSystem.description);
            }

            DrawSystemSelection();

            if (currentSystem == null) return;

            ImGui.Spacing();

            // Horizontal section tab bar (always visible when a system is selected)
            if (ImGui.BeginTabBar("##SystemSections"))
            {
                for (int i = 0; i < SectionNames.Length; i++)
                {
                    bool selected = systemSectionIndex == i;
                    if (ImGui.BeginTabItem(SectionNames[i]))
                    {
                        systemSectionIndex = i;
                        drawStatLayout = (i == 0);
                        ImGui.EndTabItem();
                    }
                }
                ImGui.EndTabBar();
            }

            ImGui.Spacing();
            ThemeManager.GradientSeparator();
            ImGui.Spacing();

            // Draw the selected section
            switch (systemSectionIndex)
            {
                case 0:
                    Stats.DrawStatCreation();
                    break;
                case 1:
                    AbsoluteRP.Windows.Systems.Skills.Skills.DrawSkillsEditor();
                    break;
                case 2:
                    AbsoluteRP.Windows.Systems.Combat.Combat.DrawCombatConfig();
                    break;
                case 3:
                    AbsoluteRP.Windows.Systems.Rules.Rules.DrawRulesEditor();
                    break;
                case 4:
                    AbsoluteRP.Windows.Systems.Roster.Roster.DrawRoster();
                    break;
            }
        }

        /// <summary>
        /// Saves all system data (stats, combat, classes, skills, rules) to the server.
        /// </summary>
        public static void SaveAllSystemData()
        {
            var system = currentSystem;
            if (system == null || system.id <= 0 || Plugin.character == null) return;

            var character = Plugin.character;
            Networking.DataSender.UpdateSystemSettings(character, system.id, system.name, system.basePointsAvailable, system.requireApproval, system.rules);
            Networking.DataSender.SaveSystemStats(character, system.id, system.StatsData);
            Networking.DataSender.SaveCombatConfig(character, system.id, system.CombatConfig, system.Resources);
            Networking.DataSender.SaveSkillClasses(character, system.id, system.SkillClasses);
            Networking.DataSender.SaveSkills(character, system.id, system.Skills, system.SkillConnections);
        }


        public static void DrawSystemSelection()
        {
            if (systemData.Count == 0)
                return;

            // Defensive: ensure index is valid
            if (currentSystemIndex < 0 || currentSystemIndex >= systemData.Count)
            {
                currentSystemIndex = 0;
                currentSystem = systemData[0];
            }

            string currentLabel = !string.IsNullOrEmpty(systemData[currentSystemIndex].name)
                ? systemData[currentSystemIndex].name
                : "Select a system";

            if (ImGui.BeginCombo("##Systems", currentLabel))
            {
                uiSelected = true;
                for (int idx = 0; idx < systemData.Count; idx++)
                {
                    string label = string.IsNullOrEmpty(systemData[idx].name) ? "New System" : systemData[idx].name;
                    bool isSelected = idx == currentSystemIndex;
                    if (ImGui.Selectable(label + $"##{idx}", isSelected))
                    {
                        currentSystemIndex = idx;
                        currentSystem = systemData[idx];
                        drawStatLayout = true;
                        Stats.currentStatIndex = -1;
                        Stats.selectedStat = null;

                        // Fetch full system data from server when selecting a different system
                        if (Plugin.character != null && systemData[idx].id > 0)
                            Networking.DataSender.FetchSystem(Plugin.character, systemData[idx].id);
                    }
                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            string systemName = systemData[currentSystemIndex].name;
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 160);
            if (ImGui.InputTextWithHint("##SystemName", "Enter system name...", ref systemName))
            {
                systemData[currentSystemIndex].name = systemName;
                if (currentSystem != null)
                    currentSystem.name = systemName;
            }
            ImGui.SameLine();
            if (currentSystem != null && currentSystem.id > 0)
            {
                if (ThemeManager.PillButton("Save All##saveAll", new Vector2(140, 0)))
                    SaveAllSystemData();
            }
            else if (currentSystem != null && currentSystem.id <= 0)
            {
                ImGui.TextColored(ThemeManager.FontMuted, "Creating...");
            }

            // Share code display
            if (currentSystem != null && !string.IsNullOrEmpty(currentSystem.shareCode))
            {
                ImGui.Text("Share Code:");
                ImGui.SameLine();
                ImGui.TextColored(ThemeManager.Accent, currentSystem.shareCode);
                ImGui.SameLine();
                if (ThemeManager.GhostButton("Copy##copyCode", new Vector2(50, 0)))
                    ImGui.SetClipboardText(currentSystem.shareCode);
            }

            // Require approval toggle
            if (currentSystem != null)
            {
                bool reqApproval = currentSystem.requireApproval;
                if (ImGui.Checkbox("Require approval for character sheets", ref reqApproval))
                    currentSystem.requireApproval = reqApproval;
            }
        }
        private static List<Vector2> CalculatePolygonPoints(Vector2 center, float radius, int count)
        {
            var points = new List<Vector2>();
            if (count == 0) return points;
            float angleStep = 2 * MathF.PI / count;
            float startAngle = -MathF.PI / 2; // Ensures first point is straight up
            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + i * angleStep;
                points.Add(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
            }
            return points;
        }

        private static List<Vector2> LerpPolygonPoints(List<Vector2> from, List<Vector2> to, float t)
        {
            var result = new List<Vector2>();
            int count = Math.Min(from.Count, to.Count);
            for (int i = 0; i < count; i++)
            {
                result.Add(Vector2.Lerp(from[i], to[i], t));
            }
            // If the new shape has more points, add them directly
            for (int i = count; i < to.Count; i++)
            {
                result.Add(to[i]);
            }
            return result;
        }
       

        public void Dispose()
        {
        }
    }
}