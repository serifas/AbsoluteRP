using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI;
using AbsoluteRP.Windows.Listings;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AbsoluteRP.Network;

namespace AbsoluteRP.Windows.Systems.Combat
{
    // Combat configuration editor - dice settings, health pool, resources, and linked stat setup. Also contains the stat-linking dropdown logic used by both health and resource configs.
    internal class Combat
    {
        private static readonly string[] DiceTypeNames = { "d2 (Coin Flip)", "d4", "d6", "d8", "d10", "d12", "d20", "d100" };
        private static readonly int[] DiceTypeValues = { 2, 4, 6, 8, 10, 12, 20, 100 };
        private static int selectedDiceIndex = 6; // default d20
        private static int lastRollResult = 0;
        private static readonly Random rng = new Random();
        private static int _pendingResourceDeleteIndex = -1;
        private static bool _deleteResPopupOpen = true;

        // Section open state (all start open, like the old DefaultOpen headers)
        private static bool diceOpen = true;
        private static bool healthOpen = true;
        private static bool resourcesOpen = true;
        private static bool turnsOpen = true;

        public static void DrawCombatConfig()
        {
            var system = SystemsWindow.currentSystem;
            if (system == null) return;
            var config = system.CombatConfig;

            // Each section is a collapsible card; all start open like the old headers.
            // Dice Configuration
            bool dice = RsElements.BeginCollapsible("sys_combat_dice", "Dice Configuration", ref diceOpen);
            try { if (dice) DrawDiceSection(config); }
            finally { RsElements.EndCollapsible(); }
            SysUI.Gap(6f);

            // Health Configuration
            bool health = RsElements.BeginCollapsible("sys_combat_health", "Health Configuration", ref healthOpen);
            try { if (health) DrawHealthSection(config, system); }
            finally { RsElements.EndCollapsible(); }
            SysUI.Gap(6f);

            // Resources
            bool res = RsElements.BeginCollapsible("sys_combat_res", $"Resources ({system.Resources.Count})", ref resourcesOpen);
            try { if (res) DrawResourcesSection(system); }
            finally { RsElements.EndCollapsible(); }
            SysUI.Gap(6f);

            // Turn Configuration
            bool turns = RsElements.BeginCollapsible("sys_combat_turns", "Turn Configuration", ref turnsOpen);
            try
            {
                if (turns)
                {
                    using (SysUI.Field("Turns per Round"))
                    {
                        int tc = config.turnCount;
                        if (SysUI.InputInt("TurnCount", ref tc))
                            config.turnCount = Math.Max(1, tc);
                    }
                    SysUI.Gap(4f);
                }
            }
            finally { RsElements.EndCollapsible(); }

            // Save Button
            SysUI.Gap(10f);
            if (RsElements.Button("Save Combat Config##saveCombat", RsElements.ButtonVariant.Primary))
            {
                if (system.id > 0 && Plugin.character != null)
                {
                    AbsoluteRP.Network.Systems_DS.SaveCombatConfig(Plugin.character, system.id, system.CombatConfig, system.Resources);
                }
            }
        }

        private static void DrawDiceSection(CombatConfigData config)
        {
            float gap = SysUI.S(16f);

            // Dice type
            using (SysUI.Field("Dice Type"))
            {
                int diceIdx = Array.IndexOf(DiceTypeValues, config.diceType);
                if (diceIdx < 0) diceIdx = 6;
                if (RsElements.Dropdown("DiceType", ref diceIdx, DiceTypeNames, 180f))
                {
                    config.diceType = DiceTypeValues[diceIdx];
                    selectedDiceIndex = diceIdx;
                }
            }
            ImGui.SameLine(0f, gap);

            // Dice count
            using (SysUI.Field("Number of Dice"))
            {
                int dc = config.diceCount;
                if (SysUI.InputInt("DiceCount", ref dc))
                    config.diceCount = Math.Clamp(dc, 1, 10);
            }
            ImGui.SameLine(0f, gap);

            // Modifier
            using (SysUI.Field("Flat Modifier"))
            {
                int dm = config.diceModifier;
                if (SysUI.InputInt("DiceMod", ref dm))
                    config.diceModifier = dm;
            }

            SysUI.Gap(8f);
            // Roll preview
            string rollDesc = $"{config.diceCount}d{config.diceType}";
            if (config.diceModifier > 0) rollDesc += $"+{config.diceModifier}";
            else if (config.diceModifier < 0) rollDesc += $"{config.diceModifier}";

            if (RsElements.Button($"Roll {rollDesc}##RollPreview", RsElements.ButtonVariant.Secondary))
            {
                lastRollResult = 0;
                for (int i = 0; i < config.diceCount; i++)
                    lastRollResult += rng.Next(1, config.diceType + 1);
                lastRollResult += config.diceModifier;
            }
            if (lastRollResult != 0)
            {
                ImGui.SameLine(0f, SysUI.S(10f));
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + SysUI.S(6f));
                SysUI.Badge($"Result: {lastRollResult}", RsTheme.AccentPrimary);
            }
            SysUI.Gap(4f);
        }

        private static void DrawHealthSection(CombatConfigData config, SystemData system)
        {
            float gap = SysUI.S(16f);

            bool he = config.healthEnabled;
            if (RsElements.Toggle("sys_health_enabled", ref he, "Enable Health"))
                config.healthEnabled = he;

            if (config.healthEnabled)
            {
                SysUI.Gap(8f);
                using (SysUI.Field("Base HP"))
                {
                    int hb = config.healthBase;
                    if (SysUI.InputInt("hpBase", ref hb))
                        config.healthBase = Math.Max(1, hb);
                }
                ImGui.SameLine(0f, gap);
                using (SysUI.Field("Max HP"))
                {
                    int hm = config.healthMax;
                    if (SysUI.InputInt("hpMax", ref hm))
                        config.healthMax = Math.Max(1, hm);
                }

                SysUI.Gap(6f);

                // Linked stat
                using (SysUI.Field("HP Linked to Stat"))
                {
                    config.healthLinkedStatId = DrawStatCombo("HPLinkedStat", config.healthLinkedStatId, system);
                }
                if (config.healthLinkedStatId != NoLinkedStat)
                {
                    ImGui.SameLine(0f, gap);
                    using (SysUI.Field("Stat Multiplier"))
                    {
                        float hsm = config.healthStatMultiplier;
                        if (SysUI.InputFloat("hpMult", ref hsm, 150f))
                            config.healthStatMultiplier = hsm;
                    }
                }

                SysUI.Gap(6f);

                // Regen
                using (SysUI.Field("HP Regen Amount"))
                {
                    int hra = config.healthRegenAmount;
                    if (SysUI.InputInt("hpRegen", ref hra))
                        config.healthRegenAmount = Math.Max(0, hra);
                }
                ImGui.SameLine(0f, gap);
                using (SysUI.Field("Every N Turns"))
                {
                    int hrt = config.healthRegenEveryNTurns;
                    if (SysUI.InputInt("hpRegenTurns", ref hrt))
                        config.healthRegenEveryNTurns = Math.Max(0, hrt);
                }
            }
            SysUI.Gap(4f);
        }

        private static void DrawResourcesSection(SystemData system)
        {
            float gap = SysUI.S(16f);

            if (RsElements.Button("+ Add Resource##addRes", RsElements.ButtonVariant.Primary))
            {
                system.Resources.Add(new ResourceData
                {
                    name = "New Resource",
                    baseValue = 100,
                    maxValue = 100,
                    color = new Vector4(0.2f, 0.5f, 1f, 1f),
                });
            }

            if (system.Resources.Count == 0)
            {
                SysUI.Gap(4f);
                SysUI.MutedWrapped("No resources yet. Resources are pools like mana or stamina that skills can spend.");
            }

            SysUI.Gap(8f);

            for (int i = 0; i < system.Resources.Count; i++)
            {
                var r = system.Resources[i];
                bool removeNow = false;
                ImGui.PushID($"res_{i}");
                try
                {
                    // Row card: outlined block with a strip in the resource colour.
                    var draw = ImGui.GetWindowDrawList();
                    var cardMin = ImGui.GetCursorScreenPos();
                    float cardW = RsElements.AvailContentWidth();
                    float inset = SysUI.S(14f);
                    float innerW = Math.Max(SysUI.S(120f), cardW - inset * 2f);

                    ImGui.SetCursorScreenPos(cardMin + new Vector2(inset, SysUI.S(10f)));
                    ImGui.BeginGroup();
                    try
                    {
                        // Name + colour + delete
                        float delW = SysUI.S(28f);
                        float swatchW = ImGui.GetTextLineHeight() + SysUI.S(16f);
                        float nameW = Math.Max(SysUI.S(100f), innerW - delW - swatchW - SysUI.S(16f)) / RsTheme.Scale;
                        string rname = r.name;
                        if (RsElements.InputText("resName", ref rname, 256, "Resource Name", nameW))
                            r.name = rname;
                        ImGui.SameLine(0f, SysUI.S(8f));
                        Vector4 rcol = r.color;
                        if (SysUI.ColorPicker("resColor", ref rcol))
                            r.color = rcol;
                        ImGui.SameLine(0f, SysUI.S(8f));
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (ImGui.GetTextLineHeight() + SysUI.S(16f) - SysUI.S(28f)) * 0.5f);
                        if (RsElements.IconButton(FontAwesomeIcon.Trash, "resDel", RsElements.ButtonVariant.Danger, 28f))
                        {
                            _pendingResourceDeleteIndex = i;
                            ImGui.OpenPopup("ConfirmDeleteResource##confirmDelRes");
                        }
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Remove resource");

                        if (_pendingResourceDeleteIndex == i && SysUI.BeginModal("ConfirmDeleteResource##confirmDelRes", ref _deleteResPopupOpen))
                        {
                            SysUI.Primary($"Are you sure you want to remove resource \"{r.name}\"?");
                            SysUI.Gap(6f);

                            if (RsElements.Button("Remove", RsElements.ButtonVariant.Danger))
                            {
                                removeNow = true;
                                _pendingResourceDeleteIndex = -1;
                                ImGui.CloseCurrentPopup();
                            }
                            ImGui.SameLine();
                            if (RsElements.Button("Cancel##cancelDelRes", RsElements.ButtonVariant.Secondary))
                            {
                                _pendingResourceDeleteIndex = -1;
                                ImGui.CloseCurrentPopup();
                            }
                            SysUI.EndModal();
                        }

                        SysUI.Gap(4f);

                        // Description
                        string rdesc = r.description ?? "";
                        if (RsElements.InputText("resDesc", ref rdesc, 256, "Description", innerW / RsTheme.Scale))
                            r.description = rdesc;

                        SysUI.Gap(6f);

                        // Base/Max
                        using (SysUI.Field("Base"))
                        {
                            int rbv = r.baseValue;
                            if (SysUI.InputInt("resBase", ref rbv))
                                r.baseValue = Math.Max(0, rbv);
                        }
                        ImGui.SameLine(0f, gap);
                        using (SysUI.Field("Max"))
                        {
                            int rmv = r.maxValue;
                            if (SysUI.InputInt("resMax", ref rmv))
                                r.maxValue = Math.Max(1, rmv);
                        }

                        SysUI.Gap(6f);

                        // Linked stat
                        using (SysUI.Field("Linked to Stat"))
                        {
                            r.linkedStatId = DrawStatCombo($"resLinked{i}", r.linkedStatId, system);
                        }
                        if (r.linkedStatId != NoLinkedStat)
                        {
                            ImGui.SameLine(0f, gap);
                            using (SysUI.Field("Stat Multiplier"))
                            {
                                float sm = r.statMultiplier;
                                if (SysUI.InputFloat($"resMult{i}", ref sm, 150f))
                                    r.statMultiplier = sm;
                            }
                        }

                        SysUI.Gap(6f);

                        // Regen
                        using (SysUI.Field("Regen Amount"))
                        {
                            int rra = r.regenAmount;
                            if (SysUI.InputInt($"resRegen{i}", ref rra))
                                r.regenAmount = Math.Max(0, rra);
                        }
                        ImGui.SameLine(0f, gap);
                        using (SysUI.Field("Every N Turns"))
                        {
                            int rrt = r.regenEveryNTurns;
                            if (SysUI.InputInt($"resRegenT{i}", ref rrt))
                                r.regenEveryNTurns = Math.Max(0, rrt);
                        }
                    }
                    finally { ImGui.EndGroup(); }

                    var groupMax = ImGui.GetItemRectMax();
                    var cardMax = new Vector2(cardMin.X + cardW, groupMax.Y + SysUI.S(10f));
                    draw.AddRect(cardMin, cardMax, RsTheme.U.Border, SysUI.S(8f), ImDrawFlags.None, RsTheme.BorderThickness);
                    draw.AddRectFilled(cardMin + new Vector2(0f, SysUI.S(8f)), new Vector2(cardMin.X + SysUI.S(3f), cardMax.Y - SysUI.S(8f)),
                        SysUI.U(r.color), SysUI.S(2f));
                    ImGui.SetCursorScreenPos(new Vector2(cardMin.X, cardMax.Y));
                    ImGui.Dummy(new Vector2(cardW, SysUI.S(8f)));
                }
                finally { ImGui.PopID(); }

                if (removeNow)
                {
                    system.Resources.RemoveAt(i);
                    i--;
                }
            }
        }

        /// Draws a dropdown to select a stat from the current system. Returns the new statId.
        private const int NoLinkedStat = int.MinValue;

        private static int DrawStatCombo(string label, int statId, SystemData system)
        {
            var stats = system.StatsData;

            // Check if the statId matches any existing stat
            int sel = 0; // 0 = None
            if (statId != NoLinkedStat)
            {
                bool hasMatch = false;
                for (int i = 0; i < stats.Count; i++)
                {
                    if (stats.Values[i].id == statId) { sel = i + 1; hasMatch = true; break; }
                }
                if (!hasMatch) statId = NoLinkedStat;
            }

            var options = new List<string>(stats.Count + 1) { "None" };
            for (int i = 0; i < stats.Count; i++)
                options.Add(stats.Values[i].name);

            if (RsElements.Dropdown(label, ref sel, options, 180f))
            {
                statId = sel <= 0 ? NoLinkedStat : stats.Values[sel - 1].id;
            }
            return statId;
        }
    }
}
