using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace AbsoluteRP.RsUI;

/// Palette + sizing constants shared by every window. Colors mirror the companion web app so the plugin reads as the same product. Every value that ImGui expects as a packed uint (background fills, borders, text) is exposed through U - the sibling Vector4 field is what you push into style vars when ImGui asks for a float color instead.
public static class RsTheme
{
    // The palette is live: the four colours in Settings > Theme (border, background, accent, font) drive it through Apply(), and every other swatch is derived from those so the whole UI shifts together.
    public static readonly Vector4 DefaultBackground = FromHex(0x0b0b0f);
    public static readonly Vector4 DefaultAccent     = FromHex(0x5865F2);
    public static readonly Vector4 DefaultFont       = FromHex(0xFFFFFF);
    public static readonly Vector4 DefaultBorder     = FromHex(0x40415A);

    // Backgrounds
    public static Vector4 BgPrimary   = FromHex(0x0b0b0f);
    public static Vector4 BgSecondary = FromHex(0x14141f);
    // Panel/collapsible surface - same color, slightly translucent so the profile background image (or later, video) shows through.
    public static Vector4 PanelSurface = new(0x14 / 255f, 0x14 / 255f, 0x1f / 255f, 0.82f);
    public static Vector4 BgTertiary  = FromHex(0x0d0d12);
    public static Vector4 BgHover     = FromHex(0x0b0b0f);

    // Foregrounds
    public static Vector4 TextPrimary   = FromHex(0xFFFFFF);
    public static Vector4 TextSecondary = FromHex(0xB9BBBE);
    public static Vector4 TextMuted     = FromHex(0x8E9297);
    public static Vector4 TextLink      = FromHex(0x5865F2);

    // Accents
    public static Vector4 AccentPrimary = FromHex(0x5865F2);
    public static readonly Vector4 AccentDanger  = FromHex(0xED4245);
    public static readonly Vector4 AccentSuccess = FromHex(0x3BA55D);
    public static readonly Vector4 AccentWarning = FromHex(0xF0A500);

    // Border / separator
    public static Vector4 Border       = FromHex(0x40415A);
    public static Vector4 BorderStrong = FromHex(0x5A5D80);

    private static Vector4? _aBorder, _aBg, _aAccent, _aFont;
    private static bool _applied;

    // Called every frame before the windows draw; cheap when nothing changed.
    public static void Apply(Vector4? border, Vector4? background, Vector4? accent, Vector4? font)
    {
        if (_applied && border == _aBorder && background == _aBg && accent == _aAccent && font == _aFont) return;
        _applied = true; _aBorder = border; _aBg = background; _aAccent = accent; _aFont = font;

        static Vector4 Shift(Vector4 c, Vector4 from, Vector4 to) => new(
            Math.Clamp(c.X + (to.X - from.X), 0f, 1f), Math.Clamp(c.Y + (to.Y - from.Y), 0f, 1f), Math.Clamp(c.Z + (to.Z - from.Z), 0f, 1f), 1f);
        static Vector4 Mul(Vector4 c, Vector4 k) => new(Math.Clamp(c.X * k.X, 0f, 1f), Math.Clamp(c.Y * k.Y, 0f, 1f), Math.Clamp(c.Z * k.Z, 0f, 1f), 1f);

        // Backgrounds keep their spacing from the base colour.
        var bg = background ?? DefaultBackground;
        BgPrimary   = bg;
        BgSecondary = Shift(bg, DefaultBackground, FromHex(0x14141f));
        BgTertiary  = Shift(bg, DefaultBackground, FromHex(0x0d0d12));
        BgHover     = bg;
        PanelSurface = new Vector4(BgSecondary.X, BgSecondary.Y, BgSecondary.Z, 0.82f);

        // Text tones are the font colour dimmed by the stock ratios.
        var f = font ?? DefaultFont;
        TextPrimary   = f;
        TextSecondary = Mul(f, FromHex(0xB9BBBE));
        TextMuted     = Mul(f, FromHex(0x8E9297));

        var ac = accent ?? DefaultAccent;
        AccentPrimary = ac;
        TextLink      = ac;

        var bd = border ?? DefaultBorder;
        Border       = bd;
        BorderStrong = Shift(bd, DefaultBorder, FromHex(0x5A5D80));
    }

    // Chrome sizing Design-time (unscaled) values live in the private consts below. Callers read the public properties, which multiply by Dalamud's current font scale (ImGui.GetIO().FontGlobalScale) so the UI is sharp at every game resolution and font-size preference.
    private const float BaseTitleBarHeight  = 32f;
    private const float BaseBorderThickness = 1f;
    private const float BaseCornerRadius    = 6f;
    private const float BaseContentPadding  = 12f;
    private const float BaseEdgeInset       = 15f;

    public static float TitleBarHeight  => S(BaseTitleBarHeight);
    /// Border thickness scales with the font, floored at 1 px so it never disappears on small scales.
    public static float BorderThickness => Math.Max(1f, S(BaseBorderThickness));
    public static float CornerRadius    => S(BaseCornerRadius);
    public static float ContentPadding  => S(BaseContentPadding);
    /// Gap between the window's outer border and the body region so content doesn't sit flush against the frame.
    public static float EdgeInset       => S(BaseEdgeInset);

    // Panel-specific inset Panels want a touch more inner padding than a bare window's body 16 px instead of 12 - so widgets inside a panel don't butt up against the panel's own border. Callers can override per-panel via the `innerPadding` parameter on BeginPanel.
    private const float BasePanelInnerPadding = 16f;
    /// Default inner padding for BeginPanel - how far a panel's contents sit from its border.
    public static float PanelInnerPadding => S(BasePanelInnerPadding);

    // Font-scale helpers
    /// Current Dalamud font scale - 1.0 at default settings.
    public static float Scale => ImGui.GetIO().FontGlobalScale;

    /// Scale a pixel value by Scale.
    public static float S(float v) => v * Scale;

    /// Scale a (width, height) pair by Scale.
    public static Vector2 S(float x, float y)
    {
        var k = Scale;
        return new Vector2(x * k, y * k);
    }

    /// Scale an existing vector by Scale.
    public static Vector2 S(Vector2 v) => v * Scale;

    /// ImGui packs colors as ABGR uints for its draw list APIs. Every swatch above is available here as a packed uint too - same name, same color, ready for DrawList.AddRectFilled and friends.
    public static class U
    {
        public static uint BgPrimary => Pack(RsTheme.BgPrimary);
        public static uint BgSecondary => Pack(RsTheme.BgSecondary);
        public static uint BgTertiary => Pack(RsTheme.BgTertiary);
        public static uint BgHover => Pack(RsTheme.BgHover);
        public static uint TextPrimary => Pack(RsTheme.TextPrimary);
        public static uint TextSecondary => Pack(RsTheme.TextSecondary);
        public static uint TextMuted => Pack(RsTheme.TextMuted);
        public static uint TextLink => Pack(RsTheme.TextLink);
        public static uint AccentPrimary => Pack(RsTheme.AccentPrimary);
        public static uint AccentDanger => Pack(RsTheme.AccentDanger);
        public static uint AccentSuccess => Pack(RsTheme.AccentSuccess);
        public static uint AccentWarning => Pack(RsTheme.AccentWarning);
        public static uint Border => Pack(RsTheme.Border);
        public static uint BorderStrong => Pack(RsTheme.BorderStrong);
    }

    /// Multiply the alpha of an existing color - used by the window's fade animation to attenuate every draw-list call in one place without allocating a new palette per frame.
    public static uint WithAlpha(uint packed, float alpha01)
    {
        var a = (uint)Math.Clamp((int)(alpha01 * 255f + 0.5f), 0, 255);
        return (packed & 0x00FFFFFFu) | (a << 24);
    }

    private static Vector4 FromHex(uint rgb)
    {
        return new Vector4(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >>  8) & 0xFF) / 255f,
            ( rgb        & 0xFF) / 255f,
            1f);
    }

    private static uint Pack(Vector4 c)
    {
        static uint B(float x) => (uint)Math.Clamp((int)(x * 255f + 0.5f), 0, 255);
        // ImGui uses IM_COL32(r, g, b, a) which packs to 0xAABBGGRR.
        return B(c.X) | (B(c.Y) << 8) | (B(c.Z) << 16) | (B(c.W) << 24);
    }
}
