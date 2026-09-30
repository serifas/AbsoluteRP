using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI.Pages;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility.Raii;
using Networking;
using System.Collections.Generic;
using System.Numerics;

namespace AbsoluteRP.RsUI;

// The one and only hub. Side panel picks the page, body cross-fades between them.
public sealed class MainWindow : RsWindow
{
    public static MainWindow? Instance;

    private readonly List<IPage> _pages;
    private readonly List<RsElements.NavItem> _navItems;
    private int _selectedIndex;

    private int _prevIndex = -1;
    private float _t; // 0..1, linear. 0..0.5 = prev slides out, 0.5..1 = current slides in.
    private const float TransitionSeconds = 0.5f;

    public MainWindow() : base(id: "arp_main", title: "AbsoluteRP")
    {
        Instance = this;
        _pages = new List<IPage>
        {
            new AccountPage(),
            new ProfilesPage(),
            new SocialPage(),
            new SystemsPage(),
            new QuestsPage(),
            // Listings live in the social page now.
            new InventoryPage(),
            new ThemesPage(),
            new SettingsPage(),
        };

        _navItems = new List<RsElements.NavItem>();
        foreach (var p in _pages)
            _navItems.Add(new RsElements.NavItem(p.Icon, p.Title));

        Size = new Vector2(760, 520);
        Position = new Vector2(280, 200);
        SizeConstraints = new Dalamud.Interface.Windowing.WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 380),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        SidePanelWidth = 210f;
        IsSidePanelOpen = true;
    }
   

    internal override void TickAnimation(float deltaSeconds)
    {
        // Side panel only appears once the user has an account. Sync each frame so importing or removing the account flips it right away.
        var acct = Plugin.plugin?.Configuration?.account;
        HasSidePanel = acct != null
                       && !string.IsNullOrEmpty(acct.accountKey)
                       && !string.IsNullOrEmpty(acct.accountName);
        base.TickAnimation(deltaSeconds);
    }

    protected override void DrawSidePanel()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
        ImGui.Text("AbsoluteRP");
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        ImGui.Text("v0.1  (early)");
        ImGui.PopStyleColor();
        ImGui.Spacing();

        var picked = _selectedIndex;
        if (RsElements.NavigationMenu("arp_nav", ref picked, _navItems, RsElements.NavOrientation.Vertical))
        {
            if (picked != _selectedIndex)
                SwitchTo(picked);
        }
    }

    protected override void DrawBody()
    {
        // Pump the themed file dialog from inside the hub's Begin/End scope so BeginPopupModal actually has a parent window context. Works no matter which page opened the dialog (Profile hero, Gallery, Group logo/background, Listings banners, etc.).
        try { RsFileDialog.Draw(); }
        catch (System.Exception ex) { Plugin.PluginLog.Debug("RsFileDialog.Draw: " + ex.Message); }

        var dt = ImGui.GetIO().DeltaTime;
        if (_prevIndex >= 0 && _t < 1f)
        {
            _t = System.Math.Min(1f, _t + dt / TransitionSeconds);
            if (_t >= 1f) _prevIndex = -1;
        }

        var start = ImGui.GetCursorScreenPos();
        var slide = System.Math.Max(RsTheme.S(120f), ImGui.GetContentRegionAvail().Y);

        // Only ever draw one page per frame. Alpha-fading pages was leaking when the page's own draw code push/pops unbalanced style vars, which turned every unrelated ImGui window transparent. Slide alone communicates the transition; no PushStyleVar(Alpha) here.
        Vector2 bottomRight;
        if (_prevIndex >= 0 && _t < 0.5f)
        {
            var lt = _t * 2f;
            var e = 1f - (1f - lt) * (1f - lt);
            ImGui.SetCursorScreenPos(start);
            bottomRight = DrawPage(_pages[_prevIndex], -slide * e);
        }
        else if (_prevIndex >= 0 && _t < 1f)
        {
            var lt = (_t - 0.5f) * 2f;
            var e = 1f - (1f - lt) * (1f - lt);
            ImGui.SetCursorScreenPos(start);
            bottomRight = DrawPage(_pages[_selectedIndex], slide * (1f - e));
        }
        else
        {
            ImGui.SetCursorScreenPos(start);
            bottomRight = DrawPage(_pages[_selectedIndex], 0f);
        }

        ImGui.SetCursorScreenPos(new Vector2(start.X, bottomRight.Y));
    }

    private static Vector2 DrawPage(IPage page, float yOffset)
    {
        var origin = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + yOffset));
        ImGui.BeginGroup();
        try { page.Draw(); }
        finally { ImGui.EndGroup(); }
        return ImGui.GetItemRectMax();
    }

    private void SwitchTo(int newIndex)
    {
        if (newIndex == _selectedIndex) return;
        _pages[_selectedIndex].OnDeselected();
        _prevIndex = _selectedIndex;
        _selectedIndex = newIndex;
        _t = 0f;
        _pages[_selectedIndex].OnSelected();
    }

    // Open the hub and jump straight to a page by its Id.
    public void OpenPage(string pageId)
    {
        IsOpen = true;
        for (var i = 0; i < _pages.Count; i++)
        {
            if (_pages[i].Id == pageId)
            {
                SwitchTo(i);
                return;
            }
        }
        Plugin.PluginLog.Debug($"MainWindow.OpenPage: unknown page id '{pageId}'");
    }

    // Dalamud fires these when IsOpen flips; forward to the visible page so hosted legacy windows get their fetch/reset lifecycle.
    public override void OnOpen()
    {
        base.OnOpen();
        _pages[_selectedIndex].OnSelected();
    }

    public override void OnClose()
    {
        base.OnClose();
        _pages[_selectedIndex].OnDeselected();
    }
}
