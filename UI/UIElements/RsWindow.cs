using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using System.Numerics;

namespace AbsoluteRP.RsUI;

/// Base for every RsUI window. Derives from Dalamud's Window so lifecycle plumbing (Begin/End, focus, z-order, IsOpen tracking) lives inside the framework and does not interfere with sibling plugin windows. EVERY pixel of chrome is still ours - Dalamud calls our Draw once per frame with the window already open, and we paint background, border, title bar, close button, and body via the draw list ourselves. No default title bar. No default border. Animations (opacity, later slide/scale) are still driven by our own RsAnimator ticked by RsWindowSystem.
public abstract class RsWindow : Window
{
    /// Stable id used by ImGui. Identical to Dalamud's window name.
    public string Id => WindowName;

    /// Text shown in the title bar. Cheap alias so subclasses don't have to remember which name is which.
    public string Title { get => WindowName; set => WindowName = value; }

    /// Fade-in/out driver. 0 = invisible, 1 = fully opaque.
    public RsAnimator Opacity { get; } = new(0f);

    /// How long the fade animation runs.
    public float FadeDurationSeconds { get; set; } = 0.14f;

    /// When true, a collapsible side panel is rendered on the left of the body, toggled by an arrow button in the title bar's upper-left. Subclasses override DrawSidePanel to fill it.
    public bool HasSidePanel { get; protected set; }

    /// Target width of the side panel when fully open.
    public float SidePanelWidth { get; protected set; } = 220f;

    /// Slide-in animator, 0 = fully collapsed, 1 = fully open. Uses EaseOutQuint so the panel takes off fast and then visibly coasts into its resting position - the long tail is what sells the motion as professional rather than snapping to a stop.
    public RsAnimator SidePanelOpenness { get; } = new(0f) { Curve = EaseCurve.EaseOutQuint };

    /// How long the panel slide animation runs. Longer than the fade because the ease-out-quint curve puts most of its motion up front and needs runway to show off the coast at the end.
    public float SidePanelSlideSeconds { get; set; } = 0.38f;

    /// Current toggle state - the animator eases toward this.
    public bool IsSidePanelOpen { get; set; }

    /// Collapsed into its title bar (like a collapsed ImGui window). Toggled by the minimize button or a double-click on the title bar.
    public bool IsCollapsed { get; set; }
    private Vector2 _expandedSize;            // size to restore when un-collapsing
    private WindowSizeConstraints? _expandedConstraints;

    protected RsWindow(string id, string title) : base(id + "##" + id,
        // Bake ALL suppression + safety flags into Dalamud's base window construction so its own Begin/End uses them.
          ImGuiWindowFlags.NoTitleBar
        | ImGuiWindowFlags.NoCollapse
        | ImGuiWindowFlags.NoScrollbar
        | ImGuiWindowFlags.NoScrollWithMouse
        | ImGuiWindowFlags.NoSavedSettings
        | ImGuiWindowFlags.NoFocusOnAppearing
        | ImGuiWindowFlags.NoBackground)
    {
        // WindowName is what ImGui hashes for the window id; keep the visible title separately via Title=...
        WindowName = title + "##" + id;

        // Dalamud's default sizing hints; sizing constraints are enforced by Dalamud so they don't leak between windows.
        Size = new Vector2(480, 340);
        SizeCondition = ImGuiCond.FirstUseEver;
        Position = new Vector2(300, 250);
        PositionCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(320, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        // WindowPadding zero - we paint chrome ourselves and want our draw calls flush against the window's edges. Scoped by Dalamud's Begin/End so no leak.
        DisableWindowSounds = true;
    }

    /// Subclasses fill the interior here - everything below the title bar.
    protected abstract void DrawBody();

    /// Fill the side panel. Only called when HasSidePanel is true and the panel has any visible width. Runs inside a child window sized to the current animated panel width.
    protected virtual void DrawSidePanel() { }

    /// Dalamud calls this once per frame while IsOpen is true. We paint every pixel ourselves via the draw list so no ImGui style state is mutated at the plugin boundary.
    public override void Draw()
    {
        var alpha = Opacity.Current;
        var winPos = ImGui.GetWindowPos();
        var winSize = ImGui.GetWindowSize();
        var draw = ImGui.GetWindowDrawList();

        if (IsCollapsed)
        {
            // Only the title bar remains; the body is not drawn at all.
            if (winSize.Y > RsTheme.TitleBarHeight + 1f)
            {
                ImGui.SetWindowSize(new Vector2(winSize.X, RsTheme.TitleBarHeight));
                winSize.Y = RsTheme.TitleBarHeight;
            }
            draw.AddRectFilled(winPos, winPos + winSize, RsTheme.WithAlpha(RsTheme.U.BgSecondary, alpha), RsTheme.CornerRadius);
            draw.AddRect(winPos, winPos + winSize, RsTheme.WithAlpha(RsTheme.U.Border, alpha), RsTheme.CornerRadius, ImDrawFlags.None, RsTheme.BorderThickness);
            DrawTitleBar(winPos, winSize, alpha, draw);
            return;
        }

        draw.AddRectFilled(
            winPos,
            winPos + winSize,
            RsTheme.WithAlpha(RsTheme.U.BgPrimary, alpha),
            RsTheme.CornerRadius);
        draw.AddRect(
            winPos,
            winPos + winSize,
            RsTheme.WithAlpha(RsTheme.U.Border, alpha),
            RsTheme.CornerRadius,
            ImDrawFlags.None,
            RsTheme.BorderThickness);

        DrawTitleBar(winPos, winSize, alpha, draw);
        DrawContent(winSize);
    }

    /// Called by RsWindowSystem before Dalamud's window pump; used to advance the fade animator so it stays in sync with the actual open/close state.
    internal virtual void TickAnimation(float deltaSeconds)
    {
        Opacity.AnimateTo(IsOpen ? 1f : 0f, FadeDurationSeconds);
        Opacity.Tick(deltaSeconds);
        SyncCollapsedConstraints();

        if (HasSidePanel)
        {
            SidePanelOpenness.AnimateTo(IsSidePanelOpen ? 1f : 0f, SidePanelSlideSeconds);
            SidePanelOpenness.Tick(deltaSeconds);
        }
    }

    private bool _constraintsCollapsed;
    private void SyncCollapsedConstraints()
    {
        if (IsCollapsed == _constraintsCollapsed) return;
        _constraintsCollapsed = IsCollapsed;
        if (IsCollapsed)
        {
            _expandedConstraints = SizeConstraints;
            var w = _expandedConstraints?.MinimumSize.X ?? 320f;
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(w, RsTheme.TitleBarHeight),
                MaximumSize = new Vector2(float.MaxValue, RsTheme.TitleBarHeight),
            };
        }
        else
        {
            SizeConstraints = _expandedConstraints;
            if (_expandedSize.Y > RsTheme.TitleBarHeight) { Size = _expandedSize; SizeCondition = ImGuiCond.Always; _restoreSize = true; }
        }
    }
    private bool _restoreSize;

    /// Collapse / expand, remembering the expanded size.
    public void ToggleCollapsed()
    {
        if (!IsCollapsed) _expandedSize = ImGui.GetWindowSize();
        IsCollapsed = !IsCollapsed;
    }

    /// Custom title bar strip: fill, drag hitbox, title text, minimize + close buttons.
    private void DrawTitleBar(Vector2 winPos, Vector2 winSize, float alpha, ImDrawListPtr draw)
    {
        // The size restore is a one-frame SetNextWindowSize; back to the user's control after.
        if (_restoreSize) { _restoreSize = false; SizeCondition = ImGuiCond.FirstUseEver; }
        var barBottom = new Vector2(winPos.X + winSize.X, winPos.Y + RsTheme.TitleBarHeight);
        draw.AddRectFilled(
            winPos, barBottom,
            RsTheme.WithAlpha(RsTheme.U.BgSecondary, alpha),
            RsTheme.CornerRadius,
            ImDrawFlags.RoundCornersTop);
        draw.AddLine(
            new Vector2(winPos.X, barBottom.Y),
            new Vector2(barBottom.X, barBottom.Y),
            RsTheme.WithAlpha(RsTheme.U.Border, alpha),
            1f);

        // Optional side-panel toggle sits in the upper-left, flush inside the title bar. Title text shifts right when the toggle is drawn.
        var titleLeft = winPos.X + RsTheme.ContentPadding;
        if (HasSidePanel)
        {
            titleLeft = DrawSidePanelToggle(winPos, alpha, draw) + RsTheme.S(6f);
        }

        var titleSize = ImGui.CalcTextSize(Title);
        draw.AddText(
            new Vector2(titleLeft,
                        winPos.Y + (RsTheme.TitleBarHeight - titleSize.Y) * 0.5f),
            RsTheme.WithAlpha(RsTheme.U.TextPrimary, alpha),
            Title);

        // Close button - a real ImGui.Button for input, glyph painted on top.
        var btnSize = RsTheme.S(22f);
        var btnMin = new Vector2(
            winPos.X + winSize.X - btnSize - RsTheme.S(6f),
            winPos.Y + (RsTheme.TitleBarHeight - btnSize) * 0.5f);
        ImGui.SetCursorScreenPos(btnMin);
        ImGui.PushStyleColor(ImGuiCol.Button, 0u);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, RsTheme.WithAlpha(RsTheme.U.AccentDanger, alpha * 0.85f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, RsTheme.WithAlpha(RsTheme.U.AccentDanger, alpha));
        if (ImGui.Button("##rs_close_" + FontAwesomeIcon.WindowClose, new Vector2(btnSize, btnSize)))
        {
            IsOpen = false;
        }
        ImGui.PopStyleColor(3);
        var glyph = "X";
        var glyphSize = ImGui.CalcTextSize(glyph);
        draw.AddText(
            new Vector2(btnMin.X + (btnSize - glyphSize.X) * 0.5f,
                        btnMin.Y + (btnSize - glyphSize.Y) * 0.5f),
            RsTheme.WithAlpha(RsTheme.U.TextPrimary, alpha),
            glyph);

        // Minimize button, left of close: collapses the window into its title bar.
        var minMin = new Vector2(btnMin.X - btnSize - RsTheme.S(2f), btnMin.Y);
        ImGui.SetCursorScreenPos(minMin);
        ImGui.PushStyleColor(ImGuiCol.Button, 0u);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, RsTheme.WithAlpha(RsTheme.U.BgHover, alpha));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, RsTheme.WithAlpha(RsTheme.U.BgTertiary, alpha));
        if (ImGui.Button("##rs_min_" + Id, new Vector2(btnSize, btnSize))) ToggleCollapsed();
        ImGui.PopStyleColor(3);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(IsCollapsed ? "Expand" : "Minimize");
        {
            // "-" when expanded, "" (restore) when collapsed.
            var cy = minMin.Y + btnSize * 0.5f;
            var col = RsTheme.WithAlpha(RsTheme.U.TextPrimary, alpha);
            if (IsCollapsed)
                draw.AddRect(minMin + new Vector2(btnSize * 0.3f, btnSize * 0.3f), minMin + new Vector2(btnSize * 0.7f, btnSize * 0.7f), col, 1f, ImDrawFlags.None, 1.5f);
            else
                draw.AddLine(new Vector2(minMin.X + btnSize * 0.3f, cy), new Vector2(minMin.X + btnSize * 0.7f, cy), col, 1.5f);
        }

        // Drag strip covers everything left of the buttons, but starts after the side-panel toggle so its hitbox doesn't eat clicks meant for the toggle (last-added item wins in ImGui).
        var dragLeftX = HasSidePanel ? titleLeft - RsTheme.S(6f) : winPos.X;
        var dragMin = new Vector2(dragLeftX, winPos.Y);
        var dragMax = new Vector2(minMin.X - RsTheme.S(4f), winPos.Y + RsTheme.TitleBarHeight);
        ImGui.SetCursorScreenPos(dragMin);
        ImGui.InvisibleButton("##rs_drag_" + Id, dragMax - dragMin);
        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) ToggleCollapsed();
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            var d = ImGui.GetIO().MouseDelta;
            if (d != Vector2.Zero)
                ImGui.SetWindowPos(winPos + d);
        }
    }

    /// Chevron button in the upper-left of the title bar that toggles the side panel. Returns the button's right edge X in screen space so the caller can position the title text after it.
    private float DrawSidePanelToggle(Vector2 winPos, float alpha, ImDrawListPtr draw)
    {
        var btnSize = RsTheme.S(22f);
        var btnMin = new Vector2(
            winPos.X + RsTheme.S(6f),
            winPos.Y + (RsTheme.TitleBarHeight - btnSize) * 0.5f);

        ImGui.SetCursorScreenPos(btnMin);
        ImGui.PushStyleColor(ImGuiCol.Button, 0u);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, RsTheme.WithAlpha(RsTheme.U.BgHover, alpha));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, RsTheme.WithAlpha(RsTheme.U.BgTertiary, alpha));
        if (ImGui.Button("##rs_panel_toggle_" + Id, new Vector2(btnSize, btnSize)))
        {
            IsSidePanelOpen = !IsSidePanelOpen;
        }
        ImGui.PopStyleColor(3);

        // Chevron flips based on state: right when closed (points into where the panel will slide out), left when open (hint to close).
        var glyph = (IsSidePanelOpen
            ? FontAwesomeIcon.ChevronLeft
            : FontAwesomeIcon.ChevronRight).ToIconString();
        Vector2 glyphSize;
        using (RsIcons.Push())
        {
            glyphSize = ImGui.CalcTextSize(glyph);
        }
        var glyphPos = new Vector2(
            btnMin.X + (btnSize - glyphSize.X) * 0.5f,
            btnMin.Y + (btnSize - glyphSize.Y) * 0.5f);
        using (RsIcons.Push())
        {
            draw.AddText(glyphPos, RsTheme.WithAlpha(RsTheme.U.TextPrimary, alpha), glyph);
        }
        return btnMin.X + btnSize;
    }

    /// Body region below the title bar. Runs subclass DrawBody inside a padded child.
    private void DrawContent(Vector2 winSize)
    {
        // Inset the body region from the window's outer border on the left, right, and bottom so content doesn't touch the frame. The title bar itself paints edge-to-edge; only the body inset.
        var edge = RsTheme.EdgeInset;
        var bodyPos = new Vector2(edge, RsTheme.TitleBarHeight);
        var bodySize = new Vector2(
            winSize.X - edge * 2f,
            winSize.Y - RsTheme.TitleBarHeight - edge);

        // Reserve a horizontal slice for the animated side panel. When fully collapsed the slice is 0 px and the main body takes the whole width; when fully open the slice is SidePanelWidth.
        var panelSlice = 0f;
        if (HasSidePanel)
        {
            panelSlice = SidePanelWidth * SidePanelOpenness.Current;
        }

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(RsTheme.ContentPadding, RsTheme.ContentPadding));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, 0u);
        try
        {
            if (HasSidePanel && panelSlice > 1f)
            {
                DrawSidePanel();
            }

            var mainPos = new Vector2(bodyPos.X + panelSlice, bodyPos.Y);
            var mainSize = new Vector2(bodySize.X - panelSlice, bodySize.Y);
            ImGui.SetCursorPos(mainPos);
            if (ImGui.BeginChild("##rs_body_" + Id, mainSize, false, ImGuiWindowFlags.None))
            {

                DrawBody();
            }
            ImGui.EndChild();
        }
        finally
        {
            ImGui.PopStyleColor();
            ImGui.PopStyleVar();
        }
    }

    /// Draws the side panel background and hosts DrawSidePanel.
    private void DrawSidePanelChild(Vector2 bodyPos, Vector2 bodySize, float panelSlice)
    {
        var panelSize = new Vector2(panelSlice, bodySize.Y);
        var winPos = ImGui.GetWindowPos();
        var draw = ImGui.GetWindowDrawList();
        var alpha = Opacity.Current;

        // Solid slab so text underneath the sliding panel is fully hidden during the animation. Divider on the right separates it from the main body.
        var panelMin = winPos + bodyPos;
        var panelMax = panelMin + panelSize;
        draw.AddRectFilled(
            panelMin, panelMax,
            RsTheme.WithAlpha(RsTheme.U.BgSecondary, alpha));
        draw.AddLine(
            new Vector2(panelMax.X, panelMin.Y),
            new Vector2(panelMax.X, panelMax.Y),
            RsTheme.WithAlpha(RsTheme.U.Border, alpha),
            1f);

        ImGui.SetCursorPos(bodyPos);
        if (ImGui.BeginChild("##rs_panel_" + Id, panelSize, false, ImGuiWindowFlags.None))
        {
            DrawSidePanel();
        }
        ImGui.EndChild();
    }
}
