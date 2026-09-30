using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI;
using AbsoluteRP.Windows.Listings;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using AbsoluteRP.Network;

namespace AbsoluteRP.Windows.Systems.Stats
{
    // Stat editor with animated polygon visualization - add/remove stats, adjust values, and see a radar chart that morphs smoothly between stat configurations.
    internal class Stats
    {
        private static List<Vector2> previousPolygonPoints = new();
        private static List<Vector2> targetPolygonPoints = new();
        private static float morphProgress = 1f; // 1 = done, 0 = start
        private static float morphDuration = 0.3f; // seconds
        private static DateTime morphStartTime = DateTime.MinValue;
        public static int currentStatIndex = -1;
        public static StatData? selectedStat = null;
        private static bool _deleteStatPopupOpen = true;
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

            // Calculate center
            var windowPos = ImGui.GetWindowPos();
            var windowSize = ImGui.GetWindowSize();
            var center = windowPos + windowSize / 2f;

            // Stat list card: budget, picker and add
            bool listOpen = RsElements.BeginPanel("sys_stats_list", "Stats", fitContentsY: true);
            try
            {
                if (listOpen)
                {
                    // Base points available (system-level setting)
                    using (SysUI.Field("Base Points Available"))
                    {
                        int bpa = system.basePointsAvailable;
                        if (SysUI.InputInt("basePointsAvail", ref bpa, 140f))
                            system.basePointsAvailable = Math.Max(0, bpa);
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Total stat points players can distribute when creating a character sheet.");
                    }

                    SysUI.Gap(8f);
                    SysUI.SectionLabel(stats.Count > 0 ? $"Edit Stat ({stats.Count})" : "Edit Stat");

                    // Stat Selection and Add
                    float addW = RsElements.MeasureButtonWidth("Add Stat");
                    if (stats.Count > 0)
                    {
                        float ddW = Math.Max(SysUI.S(120f), RsElements.AvailContentWidth() - addW - SysUI.S(8f)) / RsTheme.Scale;
                        DrawStatSelection(stats, ddW);
                        ImGui.SameLine(0f, SysUI.S(8f));
                    }
                    else
                    {
                        SysUI.MutedWrapped("No stats yet. Add one to start shaping your system.");
                    }

                    // Add Stat
                    if (RsElements.Button("Add Stat##upStat", RsElements.ButtonVariant.Primary))
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
                }
            }
            finally { RsElements.EndPanel(); }

            // Remove Stat / details
            if (stats.Count != 0 && currentStatIndex >= 0 && currentStatIndex < stats.Count)
            {
                selectedStat = stats.Values[currentStatIndex];

                SysUI.Gap(10f);
                bool detailOpen = RsElements.BeginPanel("sys_stat_detail", "Stat Details", fitContentsY: true);
                try
                {
                    if (detailOpen)
                        DrawStatDetails(stats, center);
                }
                finally { RsElements.EndPanel(); }
            }

            // Save button
            SysUI.Gap(10f);
            if (RsElements.Button("Save Stats##saveStats", RsElements.ButtonVariant.Primary))
            {
                if (system != null && system.id > 0 && Plugin.character != null)
                {
                    AbsoluteRP.Network.Systems_DS.SaveSystemStats(Plugin.character, system.id, system.StatsData);
                }
            }
        }

        private static void DrawStatDetails(SortedList<int, StatData> stats, Vector2 center)
        {
            string statName = selectedStat.name;
            Vector4 statColor = selectedStat.color;

            // Name row: [name] [colour] [Remove]
            SysUI.SectionLabel("Name & Colour");
            float removeW = RsElements.MeasureButtonWidth("Remove Stat");
            float swatchW = ImGui.GetTextLineHeight() + SysUI.S(16f) + SysUI.S(8f);
            float nameW = Math.Max(SysUI.S(100f), RsElements.AvailContentWidth() - removeW - swatchW - SysUI.S(16f)) / RsTheme.Scale;
            if (RsElements.InputText("currentStat", ref statName, 256, "Stat Name", nameW))
            {
                selectedStat.name = statName;
            }
            ImGui.SameLine(0f, SysUI.S(8f));
            if (SysUI.ColorPicker("currentStatColor", ref statColor))
            {
                selectedStat.color = statColor;
            }
            ImGui.SameLine(0f, SysUI.S(8f));
            if (RsElements.Button($"Remove Stat##removeStat{currentStatIndex}", RsElements.ButtonVariant.Danger))
            {
                ImGui.OpenPopup("ConfirmDeleteStat##confirmDelStat");
            }

            if (SysUI.BeginModal("ConfirmDeleteStat##confirmDelStat", ref _deleteStatPopupOpen))
            {
                SysUI.Primary($"Are you sure you want to remove stat \"{selectedStat.name}\"?");
                SysUI.Gap(6f);

                if (RsElements.Button("Remove", RsElements.ButtonVariant.Danger))
                {
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
                    targetPolygonPoints = CalculatePolygonPoints(center, 100, stats.Count);
                    previousPolygonPoints = ResamplePolygonPoints(previousPolygonPoints, targetPolygonPoints.Count);
                    morphProgress = 0f;
                    morphStartTime = DateTime.Now;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (RsElements.Button("Cancel##cancelDelStat", RsElements.ButtonVariant.Secondary))
                {
                    ImGui.CloseCurrentPopup();
                }
                SysUI.EndModal();
            }

            // Stat Configuration
            if (selectedStat == null) return;

            SysUI.Gap(8f);

            // Description
            SysUI.SectionLabel("Description");
            string desc = selectedStat.description ?? "";
            var areaSize = new Vector2(RsElements.AvailContentWidth(), SysUI.S(64f));
            if (RsElements.InputTextArea($"statDesc{currentStatIndex}", ref desc, 500, "What does this stat represent?", areaSize))
                selectedStat.description = desc;

            SysUI.Gap(8f);

            // Min/Max values
            SysUI.SectionLabel("Value Range");
            int bMin = selectedStat.baseMin;
            int bMax = selectedStat.baseMax;
            using (SysUI.Field("Min Value"))
            {
                if (SysUI.InputInt("statMin", ref bMin)) selectedStat.baseMin = bMin;
            }
            ImGui.SameLine(0f, SysUI.S(16f));
            using (SysUI.Field("Max Value"))
            {
                if (SysUI.InputInt("statMax", ref bMax)) selectedStat.baseMax = bMax;
            }

            SysUI.Gap(8f);

            // Point rules
            SysUI.SectionLabel("Point Rules");
            bool canAdd = selectedStat.canAddPoints;
            bool canRemove = selectedStat.canRemovePoints;
            bool canNeg = selectedStat.canGoNegative;
            if (RsElements.Checkbox("Can Add Points##statCanAdd", ref canAdd)) selectedStat.canAddPoints = canAdd;
            ImGui.SameLine(0f, SysUI.S(18f));
            if (RsElements.Checkbox("Can Remove Points##statCanRemove", ref canRemove)) selectedStat.canRemovePoints = canRemove;
            ImGui.SameLine(0f, SysUI.S(18f));
            if (RsElements.Checkbox("Can Go Negative##statCanNeg", ref canNeg)) selectedStat.canGoNegative = canNeg;

            if (selectedStat.canGoNegative)
            {
                SysUI.Gap(4f);
                ImGui.Indent(SysUI.S(26f));
                bool negGives = selectedStat.negativeGivesPoint;
                if (RsElements.Checkbox("Negative Gives Extra Spendable Point##statNegGives", ref negGives))
                    selectedStat.negativeGivesPoint = negGives;
                ImGui.Unindent(SysUI.S(26f));
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
        private static void DrawStatSelection(SortedList<int, StatData> stats, float width)
        {
            if (stats.Count == 0)
                return;

            var names = new List<string>(stats.Count);
            for (int idx = 0; idx < stats.Count; idx++)
                names.Add(string.IsNullOrEmpty(stats.Values[idx].name) ? $"Stat {idx + 1}" : stats.Values[idx].name);

            // -1 shows an empty trigger, like the old "Select a stat" preview.
            int sel = (currentStatIndex >= 0 && currentStatIndex < stats.Count) ? currentStatIndex : -1;
            if (RsElements.Dropdown("sys_stat_picker", ref sel, names, width))
            {
                if (sel >= 0 && sel < stats.Count)
                {
                    currentStatIndex = sel;
                    selectedStat = stats.Values[sel];
                }
            }
        }

    }
}
