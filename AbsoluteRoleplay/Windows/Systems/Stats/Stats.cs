using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Listings;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRP.Windows.Systems.Stats
{
    internal class Stats
    {
        private static List<Vector2> previousPolygonPoints = new();
        private static List<Vector2> targetPolygonPoints = new();
        private static float morphProgress = 1f; // 1 = done, 0 = start
        private static float morphDuration = 0.3f; // seconds
        private static DateTime morphStartTime = DateTime.MinValue;
        public static int currentStatIndex = -1;
        public static StatData? selectedStat = null;
        public static int statCount = -1;

        // Monotonic counter for temporary stat IDs (avoids collisions when stats share default id=-1)
        private static int nextTempStatId = -1;
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
        public static void DrawStatCreation()
        {
            if (SystemsWindow.currentSystem == null)
                return;

            var system = SystemsWindow.currentSystem;
            var stats = system.StatsData;

            // Base points available (system-level setting)
            int bpa = system.basePointsAvailable;
            ImGui.Text("Base Points Available:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt("##basePointsAvail", ref bpa))
                system.basePointsAvailable = Math.Max(0, bpa);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Total stat points players can distribute when creating a character sheet.");

            ImGui.Spacing();

            // Calculate center
            var windowPos = ImGui.GetWindowPos();
            var windowSize = ImGui.GetWindowSize();
            var center = windowPos + windowSize / 2f;

            // --- Stat Selection and Add ---
            DrawStatSelection(stats);
            if (stats.Count > 0)
                ImGui.SameLine();

            // --- Add Stat ---
            if (ThemeManager.PillButton("Add Stat##upStat"))
            {
                // Store previous polygon points before change
                previousPolygonPoints = CalculatePolygonPoints(center, 100, stats.Count);
                int nextKey = stats.Count == 0 ? 0 : stats.Keys.Max() + 1;
                stats.Add(nextKey, new StatData() { id = nextTempStatId--, name = "New Stat", description = string.Empty, color = new Vector4(1, 1, 1, 1) });
                currentStatIndex = stats.Count - 1;
                selectedStat = stats.Values[currentStatIndex];
                // Store target polygon points after change
                targetPolygonPoints = CalculatePolygonPoints(center, 100, stats.Count);

                // Resample previous to match target count for smooth morph
                previousPolygonPoints = ResamplePolygonPoints(previousPolygonPoints, targetPolygonPoints.Count);

                morphProgress = 0f;
                morphStartTime = DateTime.Now;
            }

            // --- Remove Stat ---
            if (stats.Count != 0 && currentStatIndex >= 0 && currentStatIndex < stats.Count)
            {
                selectedStat = stats.Values[currentStatIndex];
                string statName = selectedStat.name;
                Vector4 statColor = selectedStat.color;

                if (ImGui.InputTextWithHint($"###currentStat", "Stat Name", ref statName))
                {
                    selectedStat.name = statName;
                }
                ImGui.SameLine();
                if (ImGui.ColorEdit4($"###currentStatColor", ref statColor, ImGuiColorEditFlags.NoInputs))
                {
                    selectedStat.color = statColor;
                }
                ImGui.SameLine();
                if (ThemeManager.DangerButton($"Remove Stat##removeStat{currentStatIndex}"))
                {
                    // Store previous polygon points before change
                    previousPolygonPoints = CalculatePolygonPoints(center, 100, stats.Count);
                    int keyToRemove = stats.Keys.ElementAt(currentStatIndex);
                    stats.Remove(keyToRemove);
                    if (stats.Count == 0)
                    {
                        currentStatIndex = -1;
                        selectedStat = null;
                    }
                    else if (currentStatIndex >= stats.Count)
                    {
                        currentStatIndex = stats.Count - 1;
                        selectedStat = stats.Values[currentStatIndex];
                    }
                    // Store target polygon points after change
                    targetPolygonPoints = CalculatePolygonPoints(center, 100, stats.Count);
                    previousPolygonPoints = ResamplePolygonPoints(previousPolygonPoints, targetPolygonPoints.Count);
                    morphProgress = 0f;
                    morphStartTime = DateTime.Now;
                }

                // --- Stat Configuration ---
                if (selectedStat != null)
                {
                    ImGui.Spacing();

                    // Description
                    string desc = selectedStat.description ?? "";
                    ImGui.Text("Description:");
                    if (ImGui.InputTextMultiline($"###statDesc{currentStatIndex}", ref desc, 500, new Vector2(ImGui.GetContentRegionAvail().X, 50)))
                        selectedStat.description = desc;

                    ImGui.Spacing();

                    // Min/Max values
                    int bMin = selectedStat.baseMin;
                    int bMax = selectedStat.baseMax;
                    ImGui.SetNextItemWidth(100);
                    if (ImGui.InputInt("Min Value##statMin", ref bMin)) selectedStat.baseMin = bMin;
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(100);
                    if (ImGui.InputInt("Max Value##statMax", ref bMax)) selectedStat.baseMax = bMax;

                    ImGui.Spacing();

                    // Point rules
                    bool canAdd = selectedStat.canAddPoints;
                    bool canRemove = selectedStat.canRemovePoints;
                    bool canNeg = selectedStat.canGoNegative;
                    if (ImGui.Checkbox("Can Add Points", ref canAdd)) selectedStat.canAddPoints = canAdd;
                    ImGui.SameLine();
                    if (ImGui.Checkbox("Can Remove Points", ref canRemove)) selectedStat.canRemovePoints = canRemove;
                    ImGui.SameLine();
                    if (ImGui.Checkbox("Can Go Negative", ref canNeg)) selectedStat.canGoNegative = canNeg;

                    if (selectedStat.canGoNegative)
                    {
                        ImGui.Indent();
                        bool negGives = selectedStat.negativeGivesPoint;
                        if (ImGui.Checkbox("Negative Gives Extra Spendable Point", ref negGives))
                            selectedStat.negativeGivesPoint = negGives;
                        ImGui.Unindent();
                    }

                    ImGui.Spacing();
                    ThemeManager.GradientSeparator();
                }

            }

            // Save button
            ImGui.Spacing();
            if (ThemeManager.PillButton("Save Stats##saveStats", new Vector2(140, 32)))
            {
                if (system != null && system.id > 0 && Plugin.character != null)
                {
                    Networking.DataSender.SaveSystemStats(Plugin.character, system.id, system.StatsData);
                }
            }
        }
        private static List<Vector2> ResamplePolygonPoints(List<Vector2> original, int targetCount)
        {
            var result = new List<Vector2>();
            if (original.Count == 0 || targetCount < 3)
                return result;

            float totalLength = 0f;
            var lengths = new List<float>();
            for (int i = 0; i < original.Count; i++)
            {
                float len = Vector2.Distance(original[i], original[(i + 1) % original.Count]);
                lengths.Add(len);
                totalLength += len;
            }

            for (int i = 0; i < targetCount; i++)
            {
                float t = (float)i / targetCount * totalLength;
                float acc = 0f;
                int seg = 0;
                while (seg < lengths.Count && acc + lengths[seg] < t)
                {
                    acc += lengths[seg];
                    seg++;
                }
                float segT = (t - acc) / lengths[seg];
                Vector2 p1 = original[seg];
                Vector2 p2 = original[(seg + 1) % original.Count];
                result.Add(Vector2.Lerp(p1, p2, segT));
            }
            return result;
        }
        private static void DrawStatSelection(SortedList<int, StatData> stats)
        {
            if (stats.Count == 0)
                return;

            string currentLabel = (currentStatIndex >= 0 && currentStatIndex < stats.Count)
                ? stats.Values[currentStatIndex].name
                : "Select a stat";

            if (ImGui.BeginCombo("##StatSelection", currentLabel))
            {
                for (int idx = 0; idx < stats.Count; idx++)
                {
                    var stat = stats.Values[idx];
                    bool isSelected = idx == currentStatIndex;
                    if (ImGui.Selectable(stat.name + $"##{idx}", isSelected))
                    {
                        currentStatIndex = idx;
                        selectedStat = stat;
                    }
                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }
        }

    }
}
