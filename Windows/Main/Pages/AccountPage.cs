using AbsoluteRP.Windows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AbsoluteRP.RsUI.Pages;

// Thin host for the legacy MainPanel content (login, account create/import/export, character linking).
public sealed class AccountPage : IPage
{
    public string Id => "account";
    public string Title => "Account";
    public FontAwesomeIcon Icon => FontAwesomeIcon.User;

    public void Draw()
    {
        if (AccountWindow.Instance == null)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted("Connecting...");
            ImGui.PopStyleColor();
            return;
        }

        AccountWindow.Instance.DrawMainUI();
    }

    public void OnSelected()
    {
        try { AccountWindow.Instance?.OnOpen(); }
        catch (System.Exception ex) { Plugin.PluginLog.Debug($"AccountPage OnSelected: {ex.Message}"); }
    }

    public void OnDeselected()
    {
        try { AccountWindow.Instance?.OnClose(); }
        catch (System.Exception ex) { Plugin.PluginLog.Debug($"AccountPage OnDeselected: {ex.Message}"); }
    }
}
