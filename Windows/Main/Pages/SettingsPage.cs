using AbsoluteRP.Windows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AbsoluteRP.RsUI.Pages;

// Thin host for the legacy OptionsWindow content.
public sealed class SettingsPage : IPage
{
    public string Id => "settings";
    public string Title => "Settings";
    public FontAwesomeIcon Icon => FontAwesomeIcon.Cog;

    public void Draw()
    {
        if (OptionsWindow.Instance == null)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted("Loading options...");
            ImGui.PopStyleColor();
            return;
        }

        OptionsWindow.Instance.DrawContent();
    }

    public void OnSelected()
    {
        try { OptionsWindow.Instance?.OnOpen(); }
        catch (System.Exception ex) { Plugin.PluginLog.Debug($"SettingsPage OnSelected: {ex.Message}"); }
    }

    public void OnDeselected()
    {
        try { OptionsWindow.Instance?.OnClose(); }
        catch (System.Exception ex) { Plugin.PluginLog.Debug($"SettingsPage OnDeselected: {ex.Message}"); }
    }
}
