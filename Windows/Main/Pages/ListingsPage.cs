using AbsoluteRP.Windows.Listings;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AbsoluteRP.RsUI.Pages;

// Thin host: embeds the legacy ListingsWindow content in the hub.
public sealed class ListingsPage : IPage
{
    public string Id => "listings";
    public string Title => "Listings";
    public FontAwesomeIcon Icon => FontAwesomeIcon.ListAlt;

    public void Draw()
    {
        var window = ListingsWindow.Instance;
        if (window == null)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.Text("Log in to load listings.");
            ImGui.PopStyleColor();
            return;
        }

        window.DrawContent();
    }

    public void OnSelected()
    {
        try { ListingsWindow.Instance?.OnOpen(); }
        catch (System.Exception ex) { Plugin.PluginLog.Debug($"ListingsPage OnSelected: {ex.Message}"); }
    }

    public void OnDeselected()
    {
        try { ListingsWindow.Instance?.OnClose(); }
        catch (System.Exception ex) { Plugin.PluginLog.Debug($"ListingsPage OnDeselected: {ex.Message}"); }
    }
}
