using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Listings;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AbsoluteRP.Windows.Systems.Combat
{
    internal class Combat
    {
        private static readonly string[] DiceTypeNames = { "d2 (Coin Flip)", "d4", "d6", "d8", "d10", "d12", "d20", "d100" };
        private static readonly int[] DiceTypeValues = { 2, 4, 6, 8, 10, 12, 20, 100 };
        private static int selectedDiceIndex = 6; // default d20
        private static int lastRollResult = 0;
        private static readonly Random rng = new Random();

        public static void DrawCombatConfig()
        {
            var system = SystemsWindow.currentSystem;
            if (system == null) return;
            var config = system.CombatConfig;

            float contentWidth = ImGui.GetContentRegionAvail().X;

            // ── Dice Configuration ──
            if (ImGui.CollapsingHeader("Dice Configuration", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();

                // Dice type
                ImGui.Text("Dice Type:");
                ImGui.SetNextItemWidth(200);
                int diceIdx = Array.IndexOf(DiceTypeValues, config.diceType);
                if (diceIdx < 0) diceIdx = 6;
                if (ImGui.Combo("##DiceType", ref diceIdx, DiceTypeNames, DiceTypeNames.Length))
                {
                    config.diceType = DiceTypeValues[diceIdx];
                    selectedDiceIndex = diceIdx;
                }

                // Dice count
                ImGui.Text("Number of Dice:");
                ImGui.SetNextItemWidth(100);
                int dc = config.diceCount;
                if (ImGui.InputInt("##DiceCount", ref dc))
                    config.diceCount = Math.Clamp(dc, 1, 10);

                // Modifier
                ImGui.Text("Flat Modifier:");
                ImGui.SetNextItemWidth(100);
                int dm = config.diceModifier;
                if (ImGui.InputInt("##DiceMod", ref dm))
                    config.diceModifier = dm;

                ImGui.Spacing();
                // Roll preview
                string rollDesc = $"{config.diceCount}d{config.diceType}";
                if (config.diceModifier > 0) rollDesc += $"+{config.diceModifier}";
                else if (config.diceModifier < 0) rollDesc += $"{config.diceModifier}";

                if (ThemeManager.GhostButton($"Roll {rollDesc}##RollPreview"))
                {
                    lastRollResult = 0;
                    for (int i = 0; i < config.diceCount; i++)
                        lastRollResult += rng.Next(1, config.diceType + 1);
                    lastRollResult += config.diceModifier;
                }
                if (lastRollResult != 0)
                {
                    ImGui.SameLine();
                    ThemeManager.AccentText($"Result: {lastRollResult}");
                }

                ImGui.Unindent();
                ImGui.Spacing();
            }

            // ── Health Configuration ──
            if (ImGui.CollapsingHeader("Health Configuration", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();

                bool he = config.healthEnabled;
                if (ImGui.Checkbox("Enable Health", ref he))
                    config.healthEnabled = he;

                if (config.healthEnabled)
                {
                    ImGui.SetNextItemWidth(100);
                    int hb = config.healthBase;
                    if (ImGui.InputInt("Base HP##hpBase", ref hb))
                        config.healthBase = Math.Max(1, hb);

                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(100);
                    int hm = config.healthMax;
                    if (ImGui.InputInt("Max HP##hpMax", ref hm))
                        config.healthMax = Math.Max(1, hm);

                    // Linked stat
                    ImGui.Text("HP Linked to Stat:");
                    config.healthLinkedStatId = DrawStatCombo("##HPLinkedStat", config.healthLinkedStatId, system);
                    if (config.healthLinkedStatId >= 0)
                    {
                        ImGui.Text("Stat Multiplier:");
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(150);
                        float hsm = config.healthStatMultiplier;
                        if (ImGui.InputFloat("##hpMult", ref hsm, 0.1f, 1.0f, "%.2f"))
                            config.healthStatMultiplier = hsm;
                    }

                    // Regen
                    ImGui.SetNextItemWidth(80);
                    int hra = config.healthRegenAmount;
                    if (ImGui.InputInt("HP Regen Amount##hpRegen", ref hra))
                        config.healthRegenAmount = Math.Max(0, hra);

                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(80);
                    int hrt = config.healthRegenEveryNTurns;
                    if (ImGui.InputInt("Every N Turns##hpRegenTurns", ref hrt))
                        config.healthRegenEveryNTurns = Math.Max(0, hrt);
                }

                ImGui.Unindent();
                ImGui.Spacing();
            }

            // ── Resources ──
            if (ImGui.CollapsingHeader("Resources", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();

                if (ThemeManager.PillButton("+ Add Resource##addRes"))
                {
                    system.Resources.Add(new ResourceData
                    {
                        name = "New Resource",
                        baseValue = 100,
                        maxValue = 100,
                        color = new Vector4(0.2f, 0.5f, 1f, 1f),
                    });
                }

                ImGui.Spacing();

                for (int i = 0; i < system.Resources.Count; i++)
                {
                    var r = system.Resources[i];
                    ImGui.PushID($"res_{i}");

                    // Name + delete
                    string rname = r.name;
                    ImGui.SetNextItemWidth(150);
                    if (ImGui.InputTextWithHint("##resName", "Resource Name", ref rname))
                        r.name = rname;
                    ImGui.SameLine();
                    Vector4 rcol = r.color;
                    if (ImGui.ColorEdit4("##resColor", ref rcol, ImGuiColorEditFlags.NoInputs))
                        r.color = rcol;
                    ImGui.SameLine();
                    if (ThemeManager.DangerButton("X##resDel"))
                    {
                        system.Resources.RemoveAt(i);
                        ImGui.PopID();
                        i--;
                        continue;
                    }

                    // Description
                    string rdesc = r.description ?? "";
                    if (ImGui.InputTextWithHint("##resDesc", "Description", ref rdesc))
                        r.description = rdesc;

                    // Base/Max
                    ImGui.SetNextItemWidth(80);
                    int rbv = r.baseValue;
                    if (ImGui.InputInt("Base##resBase", ref rbv))
                        r.baseValue = Math.Max(0, rbv);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(80);
                    int rmv = r.maxValue;
                    if (ImGui.InputInt("Max##resMax", ref rmv))
                        r.maxValue = Math.Max(1, rmv);

                    // Linked stat
                    ImGui.Text("Linked to Stat:");
                    r.linkedStatId = DrawStatCombo($"##resLinked{i}", r.linkedStatId, system);
                    if (r.linkedStatId >= 0)
                    {
                        ImGui.Text("Stat Multiplier:");
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(150);
                        float sm = r.statMultiplier;
                        if (ImGui.InputFloat($"##resMult{i}", ref sm, 0.1f, 1.0f, "%.2f"))
                            r.statMultiplier = sm;
                    }

                    // Regen
                    ImGui.SetNextItemWidth(80);
                    int rra = r.regenAmount;
                    if (ImGui.InputInt($"Regen Amount##resRegen{i}", ref rra))
                        r.regenAmount = Math.Max(0, rra);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(80);
                    int rrt = r.regenEveryNTurns;
                    if (ImGui.InputInt($"Every N Turns##resRegenT{i}", ref rrt))
                        r.regenEveryNTurns = Math.Max(0, rrt);

                    ImGui.PopID();
                    ImGui.Spacing();
                    ThemeManager.GradientSeparator();
                    ImGui.Spacing();
                }

                ImGui.Unindent();
                ImGui.Spacing();
            }

            // ── Turn Configuration ──
            if (ImGui.CollapsingHeader("Turn Configuration", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();
                ImGui.Text("Turns per Round:");
                ImGui.SetNextItemWidth(100);
                int tc = config.turnCount;
                if (ImGui.InputInt("##TurnCount", ref tc))
                    config.turnCount = Math.Max(1, tc);
                ImGui.Unindent();
                ImGui.Spacing();
            }

            // ── Save Button ──
            ImGui.Spacing();
            if (ThemeManager.PillButton("Save Combat Config##saveCombat"))
            {
                if (system.id > 0 && Plugin.character != null)
                {
                    Networking.DataSender.SaveCombatConfig(Plugin.character, system.id, system.CombatConfig, system.Resources);
                }
            }
        }

        /// <summary>
        /// Draws a combo box to select a stat from the current system. Returns the new statId.
        /// </summary>
        private static int DrawStatCombo(string label, int statId, SystemData system)
        {
            var stats = system.StatsData;
            string current = "None";
            if (statId >= 0)
            {
                var match = stats.Values.FirstOrDefault(s => s.id == statId);
                if (match != null) current = match.name;
                else { current = "None"; statId = -1; }
            }

            ImGui.SetNextItemWidth(150);
            if (ImGui.BeginCombo(label, current))
            {
                if (ImGui.Selectable("None", statId < 0))
                    statId = -1;
                for (int i = 0; i < stats.Count; i++)
                {
                    var s = stats.Values[i];
                    if (ImGui.Selectable(s.name + $"##{i}", statId == s.id))
                        statId = s.id;
                }
                ImGui.EndCombo();
            }
            return statId;
        }
    }
}
