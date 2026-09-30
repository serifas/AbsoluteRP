using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI;
using AbsoluteRP.Windows.Listings;
using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;

namespace AbsoluteRP.Windows.Systems.Rules
{
    // Editor for RP system rules - a simple multiline text box where system creators define their rules
    internal class Rules
    {
        public static void DrawRulesEditor()
        {
            var system = SystemsWindow.currentSystem;
            if (system == null) return;

            bool open = RsElements.BeginPanel("sys_rules", "System Rules", fitContentsY: true);
            try
            {
                if (open)
                {
                    SysUI.MutedWrapped("Define rules for your RP system. Players will see these when using your system.");
                    SysUI.Gap(6f);

                    string rulesText = system.rules ?? "";
                    float height = Math.Max(SysUI.S(160f), ImGui.GetContentRegionAvail().Y - SysUI.S(56f));
                    var size = new Vector2(RsElements.AvailContentWidth(), height);
                    if (RsElements.InputTextArea("RulesEditor", ref rulesText, 10000, "Write your system's rules here...", size))
                    {
                        system.rules = rulesText;
                    }
                }
            }
            finally { RsElements.EndPanel(); }
        }
    }
}
