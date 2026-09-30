using System;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Plugin.Services;

namespace AbsoluteRP.Social;

// DTR (Data Template Bar) entry: bell glyph + unseen notification count. Clicking opens the social notifications popup on the SocialPage. Text is rebuilt from SocialFeed.Version so it only churns when state moves.
public static class SocialDtrBell
{
    private static IDtrBarEntry? _entry;
    private static long _seenVersion = -1;

    public static bool OpenPopupRequested;

    public static void Initialize()
    {
        try
        {
            _entry = Plugin.dtrBar.Get("AbsoluteRP.Social");
            _entry.OnClick = _ => OpenPopupRequested = true;
            _entry.Shown   = true;
            Refresh(force: true);
        }
        catch (Exception ex) { Plugin.PluginLog?.Debug("SocialDtrBell.Initialize: " + ex.Message); }
    }

    // Cheap per-frame poke - the SocialPage/plugin main loop calls this so the entry's text can reflect fresh notification counts without a subscription plumbing.
    public static void SetVisible(bool on)
    {
        if (_entry != null && _entry.Shown != on) _entry.Shown = on;
    }

    public static void Tick()
    {
        if (_entry == null) return;
        if (_seenVersion == SocialFeed.Version) return;
        Refresh(force: false);
    }

    private static void Refresh(bool force)
    {
        if (_entry == null) return;
        _seenVersion = SocialFeed.Version;
        int unseen = SocialFeed.UnseenNotificationCount;
        string bell = FontAwesomeIcon.Bell.ToIconString();
        // Use the plugin's FontAwesome font for the glyph, plain text for the count. Payload builder lets the count sit inside a separate. TextPayload so it can be colored later if desired.
        var text = unseen > 0
            ? new SeString(new IconPayload(BitmapFontIcon.NewAdventurer),
                            new TextPayload($" ARP {unseen}"))
            : new SeString(new TextPayload("ARP"));

        try { _entry.Text = text; }
        catch (Exception ex) { Plugin.PluginLog?.Debug("SocialDtrBell.Refresh: " + ex.Message); }

        if (force)
        {
            try { _entry.Tooltip = new SeString(new TextPayload("AbsoluteRP: click to open notifications")); }
            catch { }
        }
    }

    public static void Dispose()
    {
        try { _entry?.Remove(); }
        catch { }
        _entry = null;
    }
}
