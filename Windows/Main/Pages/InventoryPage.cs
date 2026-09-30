using AbsoluteRP.Windows.Inventory;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AbsoluteRP.RsUI.Pages;

// Thin host that renders the inventory window's RsUI content inside the hub
public sealed class InventoryPage : IPage
{
    public string Id => "inventory";
    public string Title => "Inventory";
    public FontAwesomeIcon Icon => FontAwesomeIcon.BoxOpen;

    public void Draw()
    {
        if (InventoryWindow.Instance == null)
        {
            InvUI.Notice("Log in to load inventory.", RsTheme.AccentWarning);
            return;
        }

        InventoryWindow.Instance.DrawContent();
    }

    public void OnSelected()
    {
        try { InventoryWindow.Instance?.OnOpen(); }
        catch (System.Exception ex) { Plugin.PluginLog.Debug($"InventoryPage OnSelected: {ex.Message}"); }
    }

    public void OnDeselected()
    {
        try { InventoryWindow.Instance?.OnClose(); }
        catch (System.Exception ex) { Plugin.PluginLog.Debug($"InventoryPage OnDeselected: {ex.Message}"); }
    }
}
