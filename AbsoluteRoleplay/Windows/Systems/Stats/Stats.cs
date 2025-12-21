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

            var stats = SystemsWindow.currentSystem.StatsData;

            // Calculate center
            var windowPos = ImGui.GetWindowPos();
            var windowSize = ImGui.GetWindowSize();
            var center = windowPos + windowSize / 2f;

            // --- Stat Selection and Add ---
            DrawStatSelection(stats);
            if (stats.Count > 0)
                ImGui.SameLine();

            // --- Add Stat ---
            if (ImGui.Button("Add Stat##upStat"))
            {
                // Store previous polygon points before change
                previousPolygonPoints = CalculatePolygonPoints(center, 100, stats.Count);
                int nextKey = stats.Count == 0 ? 0 : stats.Keys.Max() + 1;
                stats.Add(nextKey, new StatData() { name = "New Stat", description = string.Empty, color = new Vector4(1, 1, 1, 1) });
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
                if (ImGui.Button($"Remove Stat##removeStat{currentStatIndex}"))
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

                    // Resample previous to match target count for smooth morph
                    previousPolygonPoints = ResamplePolygonPoints(previousPolygonPoints, targetPolygonPoints.Count);

                    morphProgress = 0f;
                    morphStartTime = DateTime.Now;
                }
            }

            // Draw stat bars
            float statWidth = 120f;
            float statHeight = 10f;
            float spacing = 30f;
            float polygonRadius = 50f;
            float polygonPadding = 150f;
            float startY = center.Y + polygonRadius + polygonPadding;

            var statKeys = stats.Keys.ToList();
            for (int idx = 0; idx < statKeys.Count; idx++)
            {
                int key = statKeys[idx];
                var drawList = ImGui.GetWindowDrawList();
                string statBarName = stats[key].name;

                Vector2 rectCenter = new Vector2(center.X, startY + idx * (statHeight + spacing));
                Vector2 rectMin = rectCenter - new Vector2(statWidth / 2f, statHeight / 2f);
                Vector2 rectMax = rectCenter + new Vector2(statWidth / 2f, statHeight / 2f);

                uint color = ImGui.ColorConvertFloat4ToU32(stats[key].color);

                var textSize = ImGui.CalcTextSize(statBarName);
                Vector2 textPos = new Vector2(
                    rectCenter.X - textSize.X / 2f,
                    rectMin.Y - textSize.Y - 4f
                );
                drawList.AddText(textPos, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1)), statBarName);

                drawList.AddRectFilled(rectMin, rectMax, color);
            }

            // --- Polygon Morph Animation (Outline Only) ---
            var statColors = stats.Values.Select(s => s.color).ToList();
            if (stats.Count >= 3)
            {
                if (morphProgress < 1f && previousPolygonPoints.Count == targetPolygonPoints.Count && targetPolygonPoints.Count >= 3)
                {
                    morphProgress = (float)((DateTime.Now - morphStartTime).TotalSeconds / morphDuration);
                    morphProgress = Math.Clamp(morphProgress, 0f, 1f);
                    var animatedPoints = LerpPolygonPoints(previousPolygonPoints, targetPolygonPoints, morphProgress);
                    UIHelpers.DrawPolygonFromPoints(animatedPoints, statColors);
                    if (morphProgress >= 1f)
                    {
                        previousPolygonPoints.Clear();
                        targetPolygonPoints.Clear();
                    }
                }
                else
                {
                    // Draw static polygon outline
                    var staticPoints = CalculatePolygonPoints(center, 100, stats.Count);
                    UIHelpers.DrawPolygonFromPoints(staticPoints, statColors);
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
