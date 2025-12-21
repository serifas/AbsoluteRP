using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Ect;
using AbsoluteRP.Windows.NavLayouts;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using AbsoluteRP.Windows.Social.Views;
using AbsoluteRP.Windows.Systems.Stats;
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
        public SystemsWindow() : base(
            "SYSTEMS", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
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

            DrawSystemCreation();

            ImGui.SetNextWindowPos(new Vector2(mainPanelPos.X - buttonSize * 1.2f, mainPanelPos.Y + headerHeight), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(buttonSize * 1.2f, navHeight), ImGuiCond.Always);

            ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar;
            Navigation nav = NavigationLayouts.SystemNavigation();
            UIHelpers.DrawSideNavigation("SYSTEMS", "SystemNavigation", ref systemNavIndex, flags, nav, focusRequested);
        }

        public static void DrawSystemCreation()
        {
            // Create new system
            if (ImGui.Button("Create System"))
            {
                systemData.Add(new SystemData() { name = "New System", description = string.Empty, StatsData = new SortedList<int, StatData>() });
                currentSystemIndex = systemData.Count - 1;
                currentSystem = systemData[currentSystemIndex];
                Stats.currentStatIndex = -1;
                Stats.selectedStat = null;
            }

            DrawSystemSelection();

            if (drawStatLayout && currentSystem != null)
            {
                Stats.DrawStatCreation();
            }
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
                    }
                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            string systemName = systemData[currentSystemIndex].name;
            if (ImGui.InputTextWithHint("##SystemName", "Enter system name...", ref systemName))
            {
                systemData[currentSystemIndex].name = systemName;
                if (currentSystem != null)
                    currentSystem.name = systemName;
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