using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Listings;
using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace AbsoluteRP.Windows.Systems.Rules
{
    internal class Rules
    {
        public static void DrawRulesEditor()
        {
            var system = SystemsWindow.currentSystem;
            if (system == null) return;

            ThemeManager.SectionHeader("System Rules");
            ImGui.Spacing();
            ThemeManager.SubtitleText("Define rules for your RP system. Players will see these when using your system.");
            ImGui.Spacing();

            string rulesText = system.rules ?? "";
            if (ImGui.InputTextMultiline("##RulesEditor", ref rulesText, 10000,
                new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 40)))
            {
                system.rules = rulesText;
            }
        }
    }
}
