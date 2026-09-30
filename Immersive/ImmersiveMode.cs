using System;
using System.Diagnostics;
using System.Numerics;
using AbsoluteRP.RsUI;
using Dalamud.Bindings.ImGui;

namespace AbsoluteRP.Immersive;

// Shared primitives for the immersive HUD: the active theme's palette, a clock, smoothed mouse parallax, and theme-agnostic widgets (buttons, readout rows, progress). Panel and veil styling lives in ImmersiveThemes; ImmersiveHud composes everything into the view. Nothing here rotates ImGui content - the "3D" feel comes from separating the profile into panels that drift at different depths.
public static class ImmersiveMode
{
    public static bool IsActive
        => Plugin.plugin?.Configuration?.ImmersiveModeEnabled == true;

    public static ImmersiveTheme Theme => ImmersiveThemes.Active;

    // per-element overrides Set while a document element with its own style is being drawn; every shared widget reads colours through these accessors so an element can recolour itself without touching the theme.
    public static Vector4? AccentOverride;
    public static Vector4? TextOverride;
    public static Vector4 Accent     => AccentOverride ?? Theme.Accent;
    public static Vector4 TextColor  => TextOverride ?? Theme.Text;
    public static Vector4 MutedColor => TextOverride.HasValue
        ? new Vector4(TextOverride.Value.X * 0.75f, TextOverride.Value.Y * 0.75f, TextOverride.Value.Z * 0.75f, 1f)
        : Theme.Muted;

    private static int _elementPushes;
    public static void PushElementStyle(Themes.ElementStyle? style, float scale)
    {
        if (style != null)
        {
            if (style.OverrideAccent) AccentOverride = style.Accent.V;
            if (style.OverrideText) TextOverride = style.Text.V;
        }
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * (style?.Alpha ?? 1f));
        ImGui.SetWindowFontScale(MathF.Max(0.3f, scale));
        _elementPushes++;
    }

    public static void PopElementStyle()
    {
        if (_elementPushes <= 0) return;
        _elementPushes--;
        ImGui.SetWindowFontScale(1f);
        ImGui.PopStyleVar();
        AccentOverride = null;
        TextOverride = null;
    }

    // palette (forwarded from the active theme)
    public static Vector4 Holo      => Theme.Accent;
    public static Vector4 HoloSoft  => Theme.AccentSoft;
    public static Vector4 HoloText  => Theme.Text;
    public static Vector4 HoloMuted => Theme.Muted;
    public static Vector4 Danger    => Theme.Danger;
    public static Vector4 GlassTop  => Theme.SurfaceTop;
    public static Vector4 GlassBot  => Theme.SurfaceBot;

    public static uint Col(Vector4 c, float alpha)
        => ImGui.ColorConvertFloat4ToU32(new Vector4(c.X, c.Y, c.Z, Math.Clamp(c.W * alpha, 0f, 1f)));

    // clock
    private static readonly Stopwatch _clock = Stopwatch.StartNew();
    public static float Time => (float)_clock.Elapsed.TotalSeconds;

    // parallax
    private static Vector2 _parallax;
    public static Vector2 Parallax => _parallax;

    public static void UpdateParallax()
    {
        var vp = ImGui.GetMainViewport();
        var half = vp.WorkSize * 0.5f;
        if (half.X < 1f || half.Y < 1f) return;
        var center = vp.WorkPos + half;
        var mp = ImGui.GetMousePos();
        var target = Vector2.Zero;
        if (mp.X > -1e6f && mp.Y > -1e6f)
        {
            target = new Vector2(
                Math.Clamp((mp.X - center.X) / half.X, -1f, 1f),
                Math.Clamp((mp.Y - center.Y) / half.Y, -1f, 1f));
        }
        var dt = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        var k = 1f - MathF.Exp(-dt * 7f);
        _parallax += (target - _parallax) * k;
    }

    public static Vector2 ParallaxOffset(float depth)
    {
        var k = Theme.Vfx.ParallaxDepth;
        return new(-_parallax.X * RsTheme.S(18f) * depth * k, -_parallax.Y * RsTheme.S(10f) * depth * k);
    }

    public static float IdleBob(float phase)
        => MathF.Sin(Time * 0.9f + phase) * RsTheme.S(2.2f);

    // panel chrome
    public static float HeaderHeight => RsTheme.S(26f);

    // Set by ImmersiveHud.BeginPanel right before DrawPanelChrome so a theme can know how much of the panel is currently revealed (the scroll unroll) and what the panel's full size will be.
    public static float PanelReveal = 1f;
    public static Vector2 PanelFullSize;

    public static void DrawPanelChrome(ImDrawListPtr dl, Vector2 min, Vector2 max, string? label, float alpha, bool header = true, string key = "panel")
    {
        if (alpha <= 0.001f) return;
        if (max.X - min.X < 2f || max.Y - min.Y < 2f) return;
        Theme.DrawPanelChrome(dl, min, max, label, alpha, header, key);
    }

    public static void DrawCornerBrackets(ImDrawListPtr dl, Vector2 min, Vector2 max, float len, float thick, uint col)
    {
        dl.AddLine(min, new Vector2(min.X + len, min.Y), col, thick);
        dl.AddLine(min, new Vector2(min.X, min.Y + len), col, thick);
        dl.AddLine(new Vector2(max.X, min.Y), new Vector2(max.X - len, min.Y), col, thick);
        dl.AddLine(new Vector2(max.X, min.Y), new Vector2(max.X, min.Y + len), col, thick);
        dl.AddLine(max, new Vector2(max.X - len, max.Y), col, thick);
        dl.AddLine(max, new Vector2(max.X, max.Y - len), col, thick);
        dl.AddLine(new Vector2(min.X, max.Y), new Vector2(min.X + len, max.Y), col, thick);
        dl.AddLine(new Vector2(min.X, max.Y), new Vector2(min.X, max.Y - len), col, thick);
    }

    public static void DrawRule(ImDrawListPtr dl, float x0, float x1, float y, float alpha)
    {
        if (x1 <= x0) return;
        var c = Accent;
        var mid = (x0 + x1) * 0.5f;
        dl.AddRectFilledMultiColor(new Vector2(x0, y), new Vector2(mid, y + 1f), Col(c, 0f), Col(c, 0.6f * alpha), Col(c, 0.6f * alpha), Col(c, 0f));
        dl.AddRectFilledMultiColor(new Vector2(mid, y), new Vector2(x1, y + 1f), Col(c, 0.6f * alpha), Col(c, 0f), Col(c, 0f), Col(c, 0.6f * alpha));
    }

    // widgets Themed tooltip: ImGui's tooltip window with the theme's panel chrome painted behind the text.
    public static void Tooltip(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var theme = Theme;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(RsTheme.S(14f), RsTheme.S(10f)));
        ImGui.PushStyleColor(ImGuiCol.PopupBg, new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0f, 0f, 0f, 0f));
        ImGui.BeginTooltip();
        try
        {
            var dl = ImGui.GetWindowDrawList();
            var min = ImGui.GetWindowPos();
            var size = ImGui.GetWindowSize();
            if (size.X > 4f && size.Y > 4f)
            {
                // A compact version of the panel chrome: fill + rim only.
                var max = min + size;
                dl.AddRectFilled(min, max, Col(theme.PopupBg, 1f), MathF.Min(theme.Rounding, RsTheme.S(6f)));
                PanelReveal = 1f;
                PanelFullSize = size;
                theme.DrawPanelChrome(dl, min, max, null, 1f, false, "tooltip");
            }
            ImGui.PushTextWrapPos(RsTheme.S(320f));
            ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();
        }
        finally
        {
            ImGui.EndTooltip();
            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar();
        }
    }

    // Draws an glyph as two strokes, centred on `c`.
    public static void DrawCloseGlyph(ImDrawListPtr dl, Vector2 c, float r, uint col, float thick = 1.8f)
    {
        dl.AddLine(c + new Vector2(-r, -r), c + new Vector2(r, r), col, thick);
        dl.AddLine(c + new Vector2(r, -r), c + new Vector2(-r, r), col, thick);
    }

    public static bool HoloButton(string id, string label, float width, bool danger = false, bool active = false, bool closeIcon = false)
    {
        var theme = Theme;
        var h = RsTheme.S(30f);
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(width, h);
        var clicked = ImGui.InvisibleButton("##holo_" + id, size);
        var hovered = ImGui.IsItemHovered();
        var held = ImGui.IsItemActive();
        var dl = ImGui.GetWindowDrawList();
        var alpha = ImGui.GetStyle().Alpha;
        var tint = danger ? theme.Danger : ImmersiveMode.Accent;
        // One quiet fill; hover brightens the border and text rather than stacking another overlay on top.
        var fillA = held ? 0.20f : active ? 0.14f : 0.07f;
        var max = pos + size;
        var round = MathF.Min(theme.Rounding, RsTheme.S(6f));
        if (theme.ElementGlow && (hovered || active))
        {
            for (int i = 3; i >= 1; i--)
            {
                var s = RsTheme.S(3f) * i;
                dl.AddRectFilled(pos - new Vector2(s), max + new Vector2(s), Col(tint, 0.10f * alpha * (1f - (i - 1) / 3f)), round + s);
            }
        }
        dl.AddRectFilled(pos, max, Col(tint, fillA * alpha), round);
        dl.AddRect(pos, max, Col(tint, (hovered ? 0.95f : 0.40f) * alpha), round, ImDrawFlags.None, hovered ? 1.5f : 1f);
        dl.AddRectFilled(pos, new Vector2(pos.X + RsTheme.S(3f), max.Y), Col(tint, (hovered ? 1f : 0.7f) * alpha), round);
        var text = label.ToUpperInvariant();
        var tx = pos.X + RsTheme.S(14f);
        var textX = tx + RsTheme.S(14f);
        var maxTextW = max.X - RsTheme.S(8f) - textX;
        var sz = ImGui.CalcTextSize(text);
        // Never let a label spill past the plate: trim with an ellipsis.
        if (sz.X > maxTextW)
        {
            var shown = text;
            while (shown.Length > 2 && ImGui.CalcTextSize(shown + "…").X > maxTextW) shown = shown[..^1];
            text = shown + "…";
            sz = ImGui.CalcTextSize(text);
        }
        var ty = pos.Y + (h - sz.Y) * 0.5f;
        var markerCol = Col(tint, (hovered ? 1f : 0.85f) * alpha);
        if (closeIcon)
            DrawCloseGlyph(dl, new Vector2(tx + RsTheme.S(3f), pos.Y + h * 0.5f), RsTheme.S(4f), markerCol);
        else
            theme.DrawMarker(dl, new Vector2(tx + RsTheme.S(3f), pos.Y + h * 0.5f), RsTheme.S(3.2f), markerCol);
        var textCol = hovered ? new Vector4(MathF.Min(1f, ImmersiveMode.TextColor.X + 0.1f), MathF.Min(1f, ImmersiveMode.TextColor.Y + 0.1f), MathF.Min(1f, ImmersiveMode.TextColor.Z + 0.1f), 1f) : ImmersiveMode.TextColor;
        dl.AddText(new Vector2(textX, ty), Col(textCol, alpha), text);
        return clicked;
    }

    // `width` pins the row to an element's box; 0 = the window's free width.
    public static void ReadoutRow(string key, string value, Vector4? valueColor = null, float width = 0f)
    {
        var theme = Theme;
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var alpha = ImGui.GetStyle().Alpha;
        var avail = width > 0f ? width : ImGui.GetContentRegionAvail().X;
        var k = key.ToUpperInvariant();
        var ksz = ImGui.CalcTextSize(k);
        var vsz = ImGui.CalcTextSize(value);
        dl.AddText(pos, Col(ImmersiveMode.MutedColor, alpha), k);
        dl.AddText(new Vector2(pos.X + avail - vsz.X, pos.Y), Col(valueColor ?? ImmersiveMode.TextColor, alpha), value);
        var y = pos.Y + ksz.Y * 0.5f;
        var x0 = pos.X + ksz.X + RsTheme.S(8f);
        var x1 = pos.X + avail - vsz.X - RsTheme.S(8f);
        var step = RsTheme.S(5f);
        for (float x = x0; x < x1; x += step)
            dl.AddRectFilled(new Vector2(x, y), new Vector2(x + 1.5f, y + 1.5f), Col(ImmersiveMode.Accent, 0.35f * alpha));
        ImGui.Dummy(new Vector2(avail, ksz.Y + RsTheme.S(4f)));
    }

    public static void SegmentBar(float frac01, float width)
    {
        frac01 = Math.Clamp(frac01, 0f, 1f);
        var theme = Theme;
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var alpha = ImGui.GetStyle().Alpha;
        var h = RsTheme.S(10f);
        var segs = 24;
        var gap = RsTheme.S(2f);
        var segW = (width - gap * (segs - 1)) / segs;
        var lit = (int)MathF.Round(frac01 * segs);
        for (int i = 0; i < segs; i++)
        {
            var x = pos.X + i * (segW + gap);
            dl.AddRectFilled(new Vector2(x, pos.Y), new Vector2(x + segW, pos.Y + h), Col(ImmersiveMode.Accent, (i < lit ? 0.85f : 0.12f) * alpha));
        }
        ImGui.Dummy(new Vector2(width, h));
    }
}
