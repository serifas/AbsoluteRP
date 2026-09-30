using AbsoluteRP.Windows.Listings;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using System.Numerics;

namespace AbsoluteRP.RsUI.Pages;

// Thin host: embeds the SystemsWindow content in the hub.
public sealed class SystemsPage : IPage
{
    public string Id => "systems";
    public string Title => "Systems";
    public FontAwesomeIcon Icon => FontAwesomeIcon.Dice;

    public void Draw()
    {
        // Header, matching the other hub pages.
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
        ImGui.TextUnformatted("Systems");
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        ImGui.TextUnformatted("   Build tabletop-style RP systems, or join one with a character sheet.");
        ImGui.PopStyleColor();
        ImGui.Dummy(new Vector2(0f, RsTheme.S(6f)));

        var window = SystemsWindow.Instance;
        if (window == null)
        {
            var open = RsElements.BeginPanel("systems_logged_out", null, fitContentsY: true);
            try
            {
                if (open)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                    ImGui.TextUnformatted("Log in to load systems.");
                    ImGui.PopStyleColor();
                }
            }
            finally { RsElements.EndPanel(); }
            return;
        }

        window.DrawContent();
    }

    public void OnSelected()
    {
        try { SystemsWindow.Instance?.OnOpen(); }
        catch (System.Exception ex) { Plugin.PluginLog.Debug($"SystemsPage OnSelected: {ex.Message}"); }
    }

    public void OnDeselected()
    {
        try { SystemsWindow.Instance?.OnClose(); }
        catch (System.Exception ex) { Plugin.PluginLog.Debug($"SystemsPage OnDeselected: {ex.Message}"); }
    }
}
