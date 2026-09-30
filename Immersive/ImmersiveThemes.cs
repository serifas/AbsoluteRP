using System;
using System.Collections.Generic;
using System.Numerics;
using AbsoluteRP.Immersive.Themes;
using AbsoluteRP.RsUI;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Rolspeace.VFX;

namespace AbsoluteRP.Immersive;

public enum HudLayout
{
    Cluster,   // nav strip + identity column + dossier (Allagan, Aether)
    Tablet,    // one carved slab with a medallion and side tabs (Nymian)
    Tome,      // two facing pages with index tabs (Sharlayan)
    Shards,    // scattered, overlapping fragments (Voidtouched)
    Document,  // laid out from a ThemeDocument's panel list
    Standard,  // one plain panel: identity on top, nav, then the content
}

// How panels arrive / leave for themes that share a layout.
public enum PanelEntrance { Slide, Rise, Unroll, Burn, Drift }

// A theme is a MATERIAL (the drawlist routines that paint panels and the veil) plus a palette, style knobs and VFX switches. The five shipped materials carry their own defaults; a ThemeDocument can be attached to override palette/style/vfx and supply a custom layout, which is how user-made themes and forked built-ins work.
public abstract class ImmersiveTheme
{
    // document overrides
    public ThemeDocument? Document;
    public ThemePalette?  PaletteOverride;
    public ThemeStyle?    StyleOverride;
    public ThemeVfx       Vfx = new();

    // identity
    protected abstract string DefaultName { get; }
    protected abstract string DefaultTagline { get; }
    public string Name    => Document?.Name ?? DefaultName;
    public string Tagline => Document != null ? (string.IsNullOrEmpty(Document.Description) ? DefaultTagline : Document.Description) : DefaultTagline;
    public abstract ThemeMaterial MaterialKind { get; }

    // palette
    protected abstract Vector4 DefaultAccent { get; }
    protected abstract Vector4 DefaultAccentSoft { get; }
    protected abstract Vector4 DefaultText { get; }
    protected abstract Vector4 DefaultMuted { get; }
    protected abstract Vector4 DefaultDanger { get; }
    protected abstract Vector4 DefaultSurfaceTop { get; }
    protected abstract Vector4 DefaultSurfaceBot { get; }
    public Vector4 Accent     => PaletteOverride?.Accent.V     ?? DefaultAccent;
    public Vector4 AccentSoft => PaletteOverride?.AccentSoft.V ?? DefaultAccentSoft;
    public Vector4 Text       => PaletteOverride?.Text.V       ?? DefaultText;
    public Vector4 Muted      => PaletteOverride?.Muted.V      ?? DefaultMuted;
    public Vector4 Danger     => PaletteOverride?.Danger.V     ?? DefaultDanger;
    public Vector4 SurfaceTop => PaletteOverride?.SurfaceTop.V ?? DefaultSurfaceTop;
    public Vector4 SurfaceBot => PaletteOverride?.SurfaceBot.V ?? DefaultSurfaceBot;
    public ThemePalette DefaultPalette => new()
    {
        Accent = ThemeColor.From(DefaultAccent), AccentSoft = ThemeColor.From(DefaultAccentSoft),
        Text = ThemeColor.From(DefaultText), Muted = ThemeColor.From(DefaultMuted), Danger = ThemeColor.From(DefaultDanger),
        SurfaceTop = ThemeColor.From(DefaultSurfaceTop), SurfaceBot = ThemeColor.From(DefaultSurfaceBot),
    };

    // style
    protected virtual float DefaultRounding => 0f;
    protected virtual float DefaultFillAlpha => 0.86f;
    protected virtual float DefaultScrimAlpha => 0.34f;
    protected virtual float DefaultVeilImageAlpha => 0.62f;
    protected virtual float DefaultContentInset => S(16f);
    protected virtual float DefaultHeaderOffsetY => 0f;
    protected virtual float DefaultFloatAmount => 1f;
    protected virtual bool  DefaultTechAccents => true;
    protected virtual bool  DefaultElementGlow => false;
    protected virtual PanelEntrance DefaultEntrance => PanelEntrance.Slide;
    // Every built-in theme shares the single-panel layout and plain control labels; the materials differ in palette, surfaces and effects only.
    protected virtual string DefaultLinkLabel => "Link";
    protected virtual string DefaultCloseLabel => "Close";
    protected virtual HudLayout DefaultLayout => HudLayout.Standard;

    public float Rounding       => StyleOverride != null ? S(StyleOverride.Rounding) : DefaultRounding;
    // Thinned while the main panel draws over the profile's background image, so the surface colour dims the picture instead of hiding it.
    public static float FillAlphaScale = 1f;
    // The main panel with no background image behind it: fully solid.
    public static bool ForceOpaque = false;
    // The main panel is being drawn over the profile's background picture: textured materials must thin their own surface so the picture shows.
    public static bool OverBackdrop = false;
    public float FillAlpha      => ForceOpaque ? 1f : (StyleOverride?.FillAlpha ?? DefaultFillAlpha) * FillAlphaScale;
    public float ScrimAlpha     => StyleOverride?.ScrimAlpha ?? DefaultScrimAlpha;
    public float VeilImageAlpha => StyleOverride?.VeilImageAlpha ?? DefaultVeilImageAlpha;
    public float ContentInset   => StyleOverride != null ? S(StyleOverride.ContentInset) : DefaultContentInset;
    public float HeaderOffsetY  => StyleOverride != null ? S(StyleOverride.HeaderOffsetY) : DefaultHeaderOffsetY;
    public float FloatAmount    => StyleOverride?.FloatAmount ?? DefaultFloatAmount;
    public bool  TechAccents    => StyleOverride?.TechAccents ?? DefaultTechAccents;
    public bool  ElementGlow    => StyleOverride?.ElementGlow ?? DefaultElementGlow;
    public string LinkLabel     => string.IsNullOrEmpty(StyleOverride?.LinkLabel) ? DefaultLinkLabel : StyleOverride!.LinkLabel;
    public string CloseLabel    => string.IsNullOrEmpty(StyleOverride?.CloseLabel) ? DefaultCloseLabel : StyleOverride!.CloseLabel;
    public PanelEntrance Entrance
        => StyleOverride != null && Enum.TryParse<PanelEntrance>(StyleOverride.Entrance, true, out var e) ? e : DefaultEntrance;
    // Closing motion: the author's pick, else the entrance played backwards.
    public PanelEntrance Exit
        => StyleOverride != null && Enum.TryParse<PanelEntrance>(StyleOverride.Exit, true, out var e) ? e : Entrance;
    public float OpenSpeed  => Math.Clamp(StyleOverride?.OpenSpeed  ?? 1f, 0.25f, 4f);
    public float CloseSpeed => Math.Clamp(StyleOverride?.CloseSpeed ?? 1f, 0.25f, 4f);
    public ThemeStyle DefaultStyle => new()
    {
        Rounding = DefaultRounding / RsTheme.Scale, FillAlpha = DefaultFillAlpha, ScrimAlpha = DefaultScrimAlpha,
        VeilImageAlpha = DefaultVeilImageAlpha, ContentInset = DefaultContentInset / RsTheme.Scale,
        HeaderOffsetY = DefaultHeaderOffsetY / RsTheme.Scale, FloatAmount = DefaultFloatAmount,
        TechAccents = DefaultTechAccents, ElementGlow = DefaultElementGlow, Entrance = DefaultEntrance.ToString(),
        LinkLabel = DefaultLinkLabel, CloseLabel = DefaultCloseLabel,
    };

    // The composition ImmersiveHud builds: a document's panel list when one is attached, otherwise the material's native layout. Document layouts are drawn from the document only when they are base (single-container) layouts; legacy multi-panel documents fall back to the Standard layout wearing the document's palette / material / effects.
    public HudLayout Layout => Document != null && Document.Panels.Count > 0 ? HudLayout.Document : DefaultLayout;
    // Native layout - used for the loading / access screens even when a document supplies the main layout.
    public HudLayout NativeLayout => DefaultLayout;

    public virtual Vector4 Scrim => new(0.01f, 0.02f, 0.05f, 1f);
    public virtual Vector4 VeilImageTint => new(0.80f, 0.90f, 1.00f, 1f);
    public virtual Vector4 PopupBg => new(SurfaceTop.X, SurfaceTop.Y, SurfaceTop.Z, 0.96f);
    public virtual Vector2 PanelJitter(string id) => Vector2.Zero;

    // Decorative alpha multiplier from the VFX intensity slider.
    protected float I => Vfx.Intensity;
    // Colour the material paints its effects and chrome with: the palette accent unless the author set an effect colour.
    protected Vector4 FxAccent => Vfx.UseEffectColor ? Vfx.EffectColor.V : Accent;
    protected Vector4 FxAccentSoft => Vfx.UseEffectColor ? Vector4.Lerp(Vfx.EffectColor.V, SurfaceTop, 0.35f) : AccentSoft;

    // drawing hooks
    public abstract void DrawPanelChrome(ImDrawListPtr dl, Vector2 min, Vector2 max, string? label, float alpha, bool header, string key);
    public abstract void DrawVeilDecor(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha);
    public virtual void DrawPopBack(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha) { }
    public virtual void DrawPopFrame(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha)
    {
        dl.AddRect(min, max, Col(Accent, 0.9f * alpha), Rounding, ImDrawFlags.None, 1.5f);
        ImmersiveMode.DrawCornerBrackets(dl, min - new Vector2(4f), max + new Vector2(4f), S(14f), 2f, Col(Accent, alpha));
    }
    public virtual void DrawImageEdges(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha, string key) { }
    public virtual void DrawMarker(ImDrawListPtr dl, Vector2 center, float r, uint col)
        => dl.AddQuadFilled(center + new Vector2(0, -r), center + new Vector2(r, 0), center + new Vector2(0, r), center + new Vector2(-r, 0), col);

    // shared helpers
    protected static uint Col(Vector4 c, float a) => ImmersiveMode.Col(c, a);
    protected static float S(float v) => RsTheme.S(v);
    // Effect clock, scaled by the theme's effect speed.
    protected float T => ImmersiveMode.Time * MathF.Max(0.05f, Vfx.Speed);
    protected static float Hash(float n)
    {
        var s = MathF.Sin(n * 127.1f + 311.7f) * 43758.5453f;
        return s - MathF.Floor(s);
    }
    protected static float KeySeed(string key)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (var c in key) { h ^= c; h *= 16777619; }
            return (h % 1000) * 0.37f;
        }
    }

    protected void DrawHeader(ImDrawListPtr dl, Vector2 min, Vector2 max, string label, float alpha, Vector4 markerCol, Vector4 textCol, Vector4 ruleCol, bool doubleRule = false)
    {
        var pad = MathF.Max(S(14f), ContentInset - S(2f));
        var text = label.ToUpperInvariant();
        var sz = ImGui.CalcTextSize(text);
        var y = min.Y + (ImmersiveMode.HeaderHeight - sz.Y) * 0.5f + S(3f) + HeaderOffsetY;
        var mc = new Vector2(min.X + pad, y + sz.Y * 0.5f);
        var mr = S(3.5f);
        DrawMarker(dl, mc, mr, Col(markerCol, 0.95f * alpha));
        var tx = mc.X + mr + S(8f);
        dl.AddText(new Vector2(tx, y), Col(textCol, alpha), text);
        var lx0 = tx + sz.X + S(10f);
        var lx1 = max.X - pad;
        if (lx1 > lx0)
        {
            var ly = y + sz.Y * 0.5f;
            dl.AddRectFilledMultiColor(new Vector2(lx0, ly), new Vector2(lx1, ly + 1f),
                Col(ruleCol, 0.55f * alpha), Col(ruleCol, 0.05f * alpha), Col(ruleCol, 0.05f * alpha), Col(ruleCol, 0.55f * alpha));
            if (doubleRule)
                dl.AddRectFilledMultiColor(new Vector2(lx0, ly + 3f), new Vector2(lx1, ly + 4f),
                    Col(ruleCol, 0.30f * alpha), Col(ruleCol, 0.02f * alpha), Col(ruleCol, 0.02f * alpha), Col(ruleCol, 0.30f * alpha));
        }
    }

    protected void DrawGradientFill(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha)
    {
        if (Rounding > 0.5f)
        {
            dl.AddRectFilled(min, max, Col(SurfaceBot, FillAlpha * alpha), Rounding);
            var inset = new Vector2(Rounding, 0f);
            dl.AddRectFilledMultiColor(min + inset, new Vector2(max.X - Rounding, min.Y + (max.Y - min.Y) * 0.55f),
                Col(SurfaceTop, FillAlpha * alpha), Col(SurfaceTop, FillAlpha * alpha), Col(SurfaceTop, 0f), Col(SurfaceTop, 0f));
        }
        else
        {
            dl.AddRectFilledMultiColor(min, max,
                Col(SurfaceTop, FillAlpha * alpha), Col(SurfaceTop, FillAlpha * alpha),
                Col(SurfaceBot, MathF.Min(1f, FillAlpha * 1.06f) * alpha), Col(SurfaceBot, MathF.Min(1f, FillAlpha * 1.06f) * alpha));
        }
    }

    protected static void DrawSoftShadow(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha, float rounding, float offsetY = 6f)
    {
        for (int i = 4; i >= 1; i--)
        {
            var s = S(3f) * i;
            dl.AddRectFilled(min + new Vector2(-s, -s + S(offsetY)), max + new Vector2(s, s + S(offsetY)),
                Col(new Vector4(0f, 0f, 0f, 1f), 0.09f * alpha), rounding + s);
        }
    }

    public static void DrawHalo(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 color, float alpha, float spread, float rounding, int layers = 6)
    {
        for (int i = layers; i >= 1; i--)
        {
            var s = spread * i / layers;
            var a = alpha * (1f - (i - 1) / (float)layers) * 0.5f;
            dl.AddRectFilled(min - new Vector2(s), max + new Vector2(s), Col(color, a), rounding + s);
        }
    }

    protected static void DrawCrack(ImDrawListPtr dl, Vector2 start, Vector2 dir, float length, int segs, float alpha, float seed, bool branch,
                                    Vector4 ink, Vector4 lip, float lipAlpha, float thickness = 1.6f)
    {
        var p = start;
        var ang = MathF.Atan2(dir.Y, dir.X) + (Hash(seed) - 0.5f) * 0.9f;
        var baseAng = MathF.Atan2(dir.Y, dir.X);
        var segLen = length / segs;
        for (int i = 0; i < segs; i++)
        {
            ang += (Hash(seed + i * 1.7f) - 0.5f) * 1.1f;
            ang += (baseAng - ang) * 0.25f;
            var q = p + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * segLen * (0.7f + Hash(seed + i * 3.3f) * 0.6f);
            var fade = MathF.Pow(1f - i / (float)segs, 1.3f);
            var a = alpha * fade;
            if (a < 0.02f) break;
            var thick = thickness - thickness * 0.5f * (i / (float)segs);
            if (lipAlpha > 0f)
                dl.AddLine(p + new Vector2(0.8f, 0.8f), q + new Vector2(0.8f, 0.8f), Col(lip, lipAlpha * a), thick);
            dl.AddLine(p, q, Col(ink, a), thick);
            if (branch && (i == 3 || i == 6) && Hash(seed + i * 9.1f) > 0.45f)
            {
                var side = Hash(seed + i * 4.4f) > 0.5f ? 1f : -1f;
                var bAng = ang + side * (0.6f + Hash(seed + i) * 0.5f);
                DrawCrack(dl, q, new Vector2(MathF.Cos(bAng), MathF.Sin(bAng)), length * 0.35f, 5, a * 0.8f, seed + 31f + i, false, ink, lip, lipAlpha, thickness);
            }
            p = q;
        }
    }

    public static void DrawRaggedEdges(ImDrawListPtr dl, Vector2 min, Vector2 max, float depth, float seed, uint fill, uint? line, float lineThick)
    {
        var w = max.X - min.X; var h = max.Y - min.Y;
        if (w < depth * 3f || h < depth * 3f) return;
        int nTop = Math.Max(5, (int)(w / S(9f)));
        int nSide = Math.Max(5, (int)(h / S(9f)));
        var pts = new Vector2[2 * (nTop + nSide)];
        int k = 0;
        float Bite(float i, float es, float t)
        {
            var n = 0.5f + 0.5f * MathF.Sin(i * 2.3f + es) * 0.5f + (Hash(seed + es + i) - 0.5f) * 0.9f;
            var tear = Hash(seed + es * 5f + MathF.Floor(i / 3f)) > 0.8f ? 0.7f : 0f;
            var corner = MathF.Max(0f, 1f - MathF.Min(t, 1f - t) * 10f) * 0.6f;
            return Math.Clamp(n * 0.6f + tear + corner, 0.08f, 1f);
        }
        for (int i = 0; i < nTop; i++)  { var t = i / (float)nTop;  pts[k++] = new Vector2(min.X + w * t, min.Y + depth * Bite(i, 1f, t)); }
        for (int i = 0; i < nSide; i++) { var t = i / (float)nSide; pts[k++] = new Vector2(max.X - depth * Bite(i, 2f, t), min.Y + h * t); }
        for (int i = 0; i < nTop; i++)  { var t = i / (float)nTop;  pts[k++] = new Vector2(max.X - w * t, max.Y - depth * Bite(i, 3f, t)); }
        for (int i = 0; i < nSide; i++) { var t = i / (float)nSide; pts[k++] = new Vector2(min.X + depth * Bite(i, 4f, t), max.Y - h * t); }
        Vector2 Outer(Vector2 p)
        {
            var dl_ = MathF.Abs(p.X - min.X); var dr = MathF.Abs(p.X - max.X);
            var dt = MathF.Abs(p.Y - min.Y); var db = MathF.Abs(p.Y - max.Y);
            var m = MathF.Min(MathF.Min(dl_, dr), MathF.Min(dt, db));
            if (m == dt) return new Vector2(p.X, min.Y);
            if (m == db) return new Vector2(p.X, max.Y);
            if (m == dl_) return new Vector2(min.X, p.Y);
            return new Vector2(max.X, p.Y);
        }
        for (int i = 0; i < pts.Length; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Length];
            var oa = Outer(a); var ob = Outer(b);
            dl.AddQuadFilled(oa, ob, b, a, fill);
            if ((oa.X == min.X || oa.X == max.X) != (ob.X == min.X || ob.X == max.X))
            {
                var corner = new Vector2(oa.X == min.X || oa.X == max.X ? oa.X : ob.X, oa.Y == min.Y || oa.Y == max.Y ? oa.Y : ob.Y);
                dl.AddTriangleFilled(oa, corner, ob, fill);
            }
            if (line.HasValue) dl.AddLine(a, b, line.Value, lineThick);
        }
    }
}

// Allagan - high tech
public sealed class AllaganTheme : ImmersiveTheme
{
    protected override string DefaultName => "Allagan";
    protected override string DefaultTagline => "High-tech hologram interface";
    public override ThemeMaterial MaterialKind => ThemeMaterial.Allagan;
    protected override Vector4 DefaultAccent     => new(0.42f, 0.86f, 1.00f, 1f);
    protected override Vector4 DefaultAccentSoft => new(0.30f, 0.62f, 0.80f, 1f);
    protected override Vector4 DefaultText       => new(0.88f, 0.96f, 1.00f, 1f);
    protected override Vector4 DefaultMuted      => new(0.52f, 0.68f, 0.78f, 1f);
    protected override Vector4 DefaultDanger     => new(1.00f, 0.42f, 0.46f, 1f);
    protected override Vector4 DefaultSurfaceTop => new(0.03f, 0.08f, 0.13f, 1f);
    protected override Vector4 DefaultSurfaceBot => new(0.01f, 0.03f, 0.06f, 1f);

    public override void DrawPanelChrome(ImDrawListPtr dl, Vector2 min, Vector2 max, string? label, float alpha, bool header, string key)
    {
        var w = max.X - min.X; var h = max.Y - min.Y;
        dl.AddRectFilled(min - new Vector2(S(10f)), max + new Vector2(S(10f)), Col(FxAccent, 0.035f * alpha * I), S(6f));
        dl.AddRectFilled(min - new Vector2(S(4f)),  max + new Vector2(S(4f)),  Col(FxAccent, 0.06f * alpha * I),  S(3f));
        DrawGradientFill(dl, min, max, alpha);

        dl.PushClipRect(min, max, true);
        if (Vfx.ScanLines)
        {
            var gap = MathF.Max(3f, S(4f));
            var scan = Col(FxAccent, 0.045f * alpha * I);
            for (float y = min.Y + 1f; y < max.Y; y += gap)
                dl.AddLine(new Vector2(min.X, y), new Vector2(max.X, y), scan, 1f);
        }
        if (Vfx.Sweep)
        {
            var period = 4.2f;
            var phase = ((T + min.X * 0.002f) % period) / period;
            var bandH = S(36f);
            var bandY = min.Y - bandH + phase * (h + bandH * 2f);
            var bandMid = bandY + bandH * 0.5f;
            dl.AddRectFilledMultiColor(new Vector2(min.X, bandY), new Vector2(max.X, bandMid), Col(FxAccent, 0f), Col(FxAccent, 0f), Col(FxAccent, 0.09f * alpha * I), Col(FxAccent, 0.09f * alpha * I));
            dl.AddRectFilledMultiColor(new Vector2(min.X, bandMid), new Vector2(max.X, bandY + bandH), Col(FxAccent, 0.09f * alpha * I), Col(FxAccent, 0.09f * alpha * I), Col(FxAccent, 0f), Col(FxAccent, 0f));
        }
        dl.AddRectFilledMultiColor(min, new Vector2(max.X, min.Y + S(22f)), Col(Vector4.One, 0.06f * alpha), Col(Vector4.One, 0.06f * alpha), Col(Vector4.One, 0f), Col(Vector4.One, 0f));
        if (Vfx.Traces)
        {
            var seedT = KeySeed(key);
            for (int c = 0; c < 2; c++)
            {
                var origin = c == 0 ? new Vector2(max.X - S(10f), min.Y + ImmersiveMode.HeaderHeight + S(16f))
                                    : new Vector2(min.X + S(10f), max.Y - S(12f));
                var dirX = c == 0 ? -1f : 1f;
                var dirY = c == 0 ? 1f : -1f;
                for (int i = 0; i < 4; i++)
                {
                    var p = origin + new Vector2(0f, dirY * i * S(7f));
                    var run = S(24f + Hash(seedT + c * 10 + i) * 50f);
                    var drop = S(10f + Hash(seedT + c * 10 + i + 5f) * 26f);
                    var q = p + new Vector2(dirX * run, 0f);
                    var r = q + new Vector2(0f, dirY * drop);
                    var lit = 0.5f + 0.5f * MathF.Sin(T * 1.5f + i * 1.3f + c);
                    var cc = Col(FxAccent, (0.10f + 0.12f * lit) * alpha * I);
                    dl.AddLine(p, q, cc, 1f);
                    dl.AddLine(q, r, cc, 1f);
                    dl.AddRectFilled(r - new Vector2(1.5f), r + new Vector2(1.5f), Col(FxAccent, (0.3f + 0.5f * lit) * alpha * I));
                }
            }
        }
        dl.PopClipRect();

        dl.AddRect(min, max, Col(FxAccent, 0.42f * alpha), 0f, ImDrawFlags.None, 1f);
        dl.AddLine(new Vector2(min.X + 1f, min.Y + 1f), new Vector2(max.X - 1f, min.Y + 1f), Col(Vector4.One, 0.10f * alpha), 1f);
        ImmersiveMode.DrawCornerBrackets(dl, min, max, S(16f), 5f, Col(FxAccent, 0.22f * alpha * I));
        ImmersiveMode.DrawCornerBrackets(dl, min, max, S(16f), 2f, Col(FxAccent, 0.95f * alpha));

        if (header && !string.IsNullOrEmpty(label))
            DrawHeader(dl, min, max, label!, alpha, FxAccent, Muted, FxAccent);
    }

    public override void DrawVeilDecor(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha)
    {
        if (!Vfx.Grid) return;
        var size = max - min;
        var grid = Col(FxAccent, 0.045f * alpha * I);
        var step = S(64f);
        var gx = ImmersiveMode.ParallaxOffset(0.12f);
        for (float x = min.X + (gx.X % step); x < max.X; x += step) dl.AddLine(new Vector2(x, min.Y), new Vector2(x, max.Y), grid, 1f);
        for (float y = min.Y + (gx.Y % step); y < max.Y; y += step) dl.AddLine(new Vector2(min.X, y), new Vector2(max.X, y), grid, 1f);
        var hy = min.Y + size.Y * 0.5f;
        var pulseX = min.X + ((T * 90f) % (size.X + 200f)) - 100f;
        var none = Col(FxAccent, 0f);
        dl.AddLine(new Vector2(min.X, hy), new Vector2(max.X, hy), Col(FxAccent, 0.06f * alpha * I), 1f);
        dl.AddRectFilledMultiColor(new Vector2(pulseX - 80f, hy - 1f), new Vector2(pulseX, hy + 1f), none, Col(FxAccent, 0.5f * alpha * I), Col(FxAccent, 0.5f * alpha * I), none);
    }
}

// Nymian - ancient kingdom of scholars
public sealed class NymianTheme : ImmersiveTheme
{
    protected override string DefaultName => "Nymian";
    protected override string DefaultTagline => "Carved stone of an ancient scholars' kingdom";
    public override ThemeMaterial MaterialKind => ThemeMaterial.Nymian;
    protected override Vector4 DefaultAccent     => new(0.36f, 0.64f, 0.56f, 1f);
    protected override Vector4 DefaultAccentSoft => new(0.74f, 0.58f, 0.34f, 1f);
    protected override Vector4 DefaultText       => new(0.93f, 0.90f, 0.82f, 1f);
    protected override Vector4 DefaultMuted      => new(0.66f, 0.64f, 0.56f, 1f);
    protected override Vector4 DefaultDanger     => new(0.82f, 0.42f, 0.32f, 1f);
    protected override Vector4 DefaultSurfaceTop => new(0.21f, 0.21f, 0.19f, 1f);
    protected override Vector4 DefaultSurfaceBot => new(0.11f, 0.11f, 0.10f, 1f);
    protected override PanelEntrance DefaultEntrance => PanelEntrance.Rise;
    protected override float DefaultRounding => S(6f);
    protected override float DefaultFillAlpha => 0.95f;
    protected override float DefaultContentInset => S(32f);
    protected override float DefaultHeaderOffsetY => S(18f);
    protected override bool DefaultTechAccents => false;
    public override Vector4 Scrim => new(0.03f, 0.04f, 0.04f, 1f);
    protected override float DefaultScrimAlpha => 0.32f;
    public override Vector4 VeilImageTint => new(0.92f, 0.92f, 0.86f, 1f);

    private static readonly Vector4 Vein  = new(0.80f, 0.78f, 0.70f, 1f);
    private static readonly Vector4 Shade = new(0f, 0f, 0f, 1f);

    public override void DrawMarker(ImDrawListPtr dl, Vector2 center, float r, uint col)
        => dl.AddRectFilled(center - new Vector2(r * 0.8f), center + new Vector2(r * 0.8f), col);

    public override void DrawPanelChrome(ImDrawListPtr dl, Vector2 min, Vector2 max, string? label, float alpha, bool header, string key)
    {
        var w = max.X - min.X; var h = max.Y - min.Y;
        var seed = KeySeed(key);
        DrawSoftShadow(dl, min, max, alpha * 1.2f, Rounding, 8f);
        DrawGradientFill(dl, min, max, alpha);

        dl.PushClipRect(min, max, true);
        if (Vfx.Veins)
        {
            for (int v = 0; v < 6; v++)
            {
                var x = min.X + Hash(seed + v * 3.3f) * w;
                var y = min.Y + Hash(seed + v * 5.1f + 1f) * h;
                var ang = Hash(seed + v * 7.7f) * MathF.PI;
                var prev = new Vector2(x, y);
                for (int i = 0; i < 26; i++)
                {
                    ang += (Hash(seed + v * 11f + i) - 0.5f) * 0.9f;
                    var p = prev + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * S(14f);
                    dl.AddLine(prev, p, Col(Vein, 0.07f * alpha * I), 1f);
                    dl.AddLine(prev + new Vector2(1f, 1f), p + new Vector2(1f, 1f), Col(Shade, 0.10f * alpha * I), 1f);
                    prev = p;
                }
            }
            for (int i = 0; i < 46; i++)
            {
                var px = min.X + Hash(i + seed) * w;
                var py = min.Y + Hash(i * 2.3f + seed + 5f) * h;
                var r  = S(4f + Hash(i * 4.1f + seed) * 16f);
                var light = (i % 3) == 0;
                dl.AddCircleFilled(new Vector2(px, py), r, Col(light ? Vein : Shade, (light ? 0.03f : 0.06f) * alpha * I), 12);
            }
        }
        dl.AddLine(new Vector2(min.X + Rounding, min.Y + 1.5f), new Vector2(max.X - Rounding, min.Y + 1.5f), Col(Vein, 0.18f * alpha), 1.5f);
        dl.AddLine(new Vector2(min.X + 1.5f, min.Y + Rounding), new Vector2(min.X + 1.5f, max.Y - Rounding), Col(Vein, 0.12f * alpha), 1.5f);
        dl.AddLine(new Vector2(min.X + Rounding, max.Y - 1.5f), new Vector2(max.X - Rounding, max.Y - 1.5f), Col(Shade, 0.5f * alpha), 2f);
        dl.AddLine(new Vector2(max.X - 1.5f, min.Y + Rounding), new Vector2(max.X - 1.5f, max.Y - Rounding), Col(Shade, 0.4f * alpha), 2f);
        dl.AddRectFilledMultiColor(min, new Vector2(max.X, min.Y + h * 0.5f),
            Col(FxAccentSoft, 0.09f * alpha), Col(FxAccentSoft, 0.03f * alpha), Col(FxAccentSoft, 0f), Col(FxAccentSoft, 0f));
        dl.AddRectFilledMultiColor(new Vector2(min.X, max.Y - h * 0.25f), max,
            Col(Shade, 0f), Col(Shade, 0f), Col(Shade, 0.35f * alpha), Col(Shade, 0.35f * alpha));
        var fi = S(5f);
        dl.AddRect(min + new Vector2(fi), max - new Vector2(fi), Col(Shade, 0.55f * alpha), MathF.Max(0f, Rounding - fi), ImDrawFlags.None, 1f);
        dl.AddRect(min + new Vector2(fi + 1f), max - new Vector2(fi + 1f), Col(Vein, 0.10f * alpha), MathF.Max(0f, Rounding - fi), ImDrawFlags.None, 1f);
        if (Vfx.Meander && w > S(120f) && h > S(90f))
        {
            var bandInset = S(8f);
            DrawMeander(dl, new Vector2(min.X + ContentInset, min.Y + bandInset), max.X - ContentInset, S(9f), alpha);
            DrawMeander(dl, new Vector2(min.X + ContentInset, max.Y - bandInset - S(9f)), max.X - ContentInset, S(9f), alpha);
            var dy = min.Y + bandInset + S(12f);
            for (float dx = min.X + ContentInset; dx + S(4f) < max.X - ContentInset; dx += S(9f))
            {
                dl.AddRectFilled(new Vector2(dx, dy), new Vector2(dx + S(4f), dy + S(2.5f)), Col(FxAccentSoft, 0.35f * alpha));
                dl.AddRectFilled(new Vector2(dx, dy + S(2.5f)), new Vector2(dx + S(4f), dy + S(3.5f)), Col(Shade, 0.5f * alpha));
            }
            for (int g = 0; g < 3; g++)
            {
                var gx = S(10f) + g * S(4f);
                dl.AddLine(new Vector2(min.X + gx, min.Y + S(30f)), new Vector2(min.X + gx, max.Y - S(30f)), Col(Shade, 0.35f * alpha), 1f);
                dl.AddLine(new Vector2(min.X + gx + 1f, min.Y + S(30f)), new Vector2(min.X + gx + 1f, max.Y - S(30f)), Col(Vein, 0.07f * alpha), 1f);
                dl.AddLine(new Vector2(max.X - gx, min.Y + S(30f)), new Vector2(max.X - gx, max.Y - S(30f)), Col(Shade, 0.35f * alpha), 1f);
                dl.AddLine(new Vector2(max.X - gx + 1f, min.Y + S(30f)), new Vector2(max.X - gx + 1f, max.Y - S(30f)), Col(Vein, 0.07f * alpha), 1f);
            }
        }
        dl.PopClipRect();

        dl.AddRect(min, max, Col(FxAccentSoft, 0.75f * alpha), Rounding, ImDrawFlags.None, 1.5f);
        DrawDentils(dl, min, max, alpha);

        if (header && !string.IsNullOrEmpty(label))
            DrawHeader(dl, min, max, label!, alpha, FxAccentSoft, Muted, FxAccentSoft, doubleRule: true);
    }

    private void DrawMeander(ImDrawListPtr dl, Vector2 origin, float xEnd, float height, float alpha)
    {
        var u = height;
        var col = Col(FxAccentSoft, 0.55f * alpha);
        var dark = Col(Shade, 0.5f * alpha);
        var x = origin.X;
        var y0 = origin.Y; var y1 = origin.Y + height;
        dl.AddLine(new Vector2(origin.X, y1 + 1f), new Vector2(xEnd, y1 + 1f), dark, 1f);
        dl.AddLine(new Vector2(origin.X, y1), new Vector2(xEnd, y1), col, 1f);
        while (x + u * 2f <= xEnd)
        {
            dl.AddLine(new Vector2(x, y1), new Vector2(x, y0), col, 1f);
            dl.AddLine(new Vector2(x, y0), new Vector2(x + u * 1.5f, y0), col, 1f);
            dl.AddLine(new Vector2(x + u * 1.5f, y0), new Vector2(x + u * 1.5f, y0 + u * 0.55f), col, 1f);
            dl.AddLine(new Vector2(x + u * 1.5f, y0 + u * 0.55f), new Vector2(x + u * 0.6f, y0 + u * 0.55f), col, 1f);
            x += u * 2f;
        }
    }

    private void DrawDentils(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha)
    {
        var o = S(5f); var s = S(4f);
        var col = Col(FxAccentSoft, 0.85f * alpha);
        dl.AddRectFilled(new Vector2(min.X + o, min.Y + o), new Vector2(min.X + o + s, min.Y + o + s), col);
        dl.AddRectFilled(new Vector2(max.X - o - s, min.Y + o), new Vector2(max.X - o, min.Y + o + s), col);
        dl.AddRectFilled(new Vector2(max.X - o - s, max.Y - o - s), new Vector2(max.X - o, max.Y - o), col);
        dl.AddRectFilled(new Vector2(min.X + o, max.Y - o - s), new Vector2(min.X + o + s, max.Y - o), col);
    }

    public override void DrawPopBack(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha)
    {
        DrawSoftShadow(dl, min - new Vector2(S(8f)), max + new Vector2(S(8f)), alpha, S(3f), 6f);
        dl.AddRectFilled(min - new Vector2(S(8f)), max + new Vector2(S(8f)), Col(SurfaceTop, 0.97f * alpha), S(3f));
    }

    public override void DrawPopFrame(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha)
    {
        dl.AddRect(min - new Vector2(S(8f)), max + new Vector2(S(8f)), Col(FxAccentSoft, 0.9f * alpha), S(3f), ImDrawFlags.None, 1.5f);
        DrawDentils(dl, min - new Vector2(S(8f)), max + new Vector2(S(8f)), alpha);
    }

    public override void DrawVeilDecor(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha)
    {
        var size = max - min;
        var c = new Vector2(min.X + size.X * 0.72f, min.Y + size.Y * 0.42f);
        for (int i = 5; i >= 1; i--)
            dl.AddCircleFilled(c, S(110f) * i, Col(FxAccentSoft, 0.014f * alpha * I), 40);
        if (!Vfx.VeilMotes) return;
        for (int i = 0; i < 24; i++)
        {
            var r0 = Hash(i + 90f); var r1 = Hash(i * 1.9f + 91f); var r2 = Hash(i * 3.3f + 92f);
            var x = min.X + size.X * (0.45f + r0 * 0.55f) + MathF.Sin(T * 0.25f + i) * S(14f);
            var y = min.Y + size.Y * r1 + MathF.Cos(T * 0.18f + i * 1.3f) * S(10f);
            var a = (0.10f + r2 * 0.18f) * alpha * I;
            dl.AddCircleFilled(new Vector2(x, y), S(1f + r2 * 1.4f), Col(FxAccentSoft, a), 8);
        }
    }
}

// Sharlayan - parchment
public sealed class SharlayanTheme : ImmersiveTheme
{
    protected override string DefaultName => "Sharlayan";
    protected override string DefaultTagline => "Ink on parchment";
    public override ThemeMaterial MaterialKind => ThemeMaterial.Sharlayan;
    protected override Vector4 DefaultAccent     => new(0.56f, 0.17f, 0.15f, 1f);
    protected override Vector4 DefaultAccentSoft => new(0.78f, 0.62f, 0.30f, 1f);
    protected override Vector4 DefaultText       => new(0.20f, 0.14f, 0.09f, 1f);
    protected override Vector4 DefaultMuted      => new(0.46f, 0.37f, 0.27f, 1f);
    protected override Vector4 DefaultDanger     => new(0.62f, 0.12f, 0.10f, 1f);
    protected override Vector4 DefaultSurfaceTop => new(0.92f, 0.86f, 0.70f, 1f);
    protected override Vector4 DefaultSurfaceBot => new(0.85f, 0.76f, 0.58f, 1f);
    protected override PanelEntrance DefaultEntrance => PanelEntrance.Unroll;
    protected override float DefaultFillAlpha => 0.97f;
    protected override float DefaultContentInset => S(42f);
    protected override float DefaultHeaderOffsetY => S(18f);
    protected override bool DefaultTechAccents => false;
    public override Vector4 Scrim => new(0.12f, 0.08f, 0.04f, 1f);
    protected override float DefaultScrimAlpha => 0.36f;
    public override Vector4 VeilImageTint => new(1.00f, 0.92f, 0.78f, 1f);
    protected override float DefaultVeilImageAlpha => 0.58f;
    public override Vector4 PopupBg => new(0.90f, 0.84f, 0.68f, 0.98f);

    private static readonly Vector4 Brown = new(0.35f, 0.25f, 0.12f, 1f);
    private static readonly Vector4 Ink   = new(0.24f, 0.16f, 0.08f, 1f);
    private static readonly Vector4 Body  = new(0.95f, 0.90f, 0.78f, 1f);
    private const string SheetPath = "UI/immersive/parchement.jpg";

    public override void DrawPanelChrome(ImDrawListPtr dl, Vector2 min, Vector2 max, string? label, float alpha, bool header, string key)
    {
        var reveal = ImmersiveMode.PanelReveal;
        var fullSize = ImmersiveMode.PanelFullSize;
        if (fullSize.X < 1f || fullSize.Y < 1f) fullSize = max - min;
        var full = min + fullSize;
        var w = fullSize.X; var h = fullSize.Y;
        var seed = KeySeed(key);
        var bleed = S(10f);
        var sheetMin = min - new Vector2(bleed);
        var sheetMax = full + new Vector2(bleed);

        var rodPad = S(16f);
        dl.PushClipRect(new Vector2(sheetMin.X - rodPad, sheetMin.Y - rodPad), new Vector2(sheetMax.X + rodPad, max.Y + rodPad), false);

        DrawSoftShadow(dl, min, new Vector2(full.X, max.Y), alpha, 0f, 5f);

        var sheet = ImmersiveTextures.Get(SheetPath, keyWhite: true);
        if (sheet != null && sheet.Handle != IntPtr.Zero)
        {
            // Over a profile background the parchment becomes a translucent wash so the chosen picture shows through with a paper tint.
            var tint = Col(Vector4.One, (OverBackdrop ? FillAlpha : 1f) * alpha);
            if (w > h * 1.15f)
                dl.AddImageQuad(sheet.Handle,
                    sheetMin, new Vector2(sheetMax.X, sheetMin.Y), sheetMax, new Vector2(sheetMin.X, sheetMax.Y),
                    new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), tint);
            else
                dl.AddImage(sheet.Handle, sheetMin, sheetMax, Vector2.Zero, Vector2.One, tint);
        }
        else
        {
            DrawGradientFill(dl, min, full, alpha);
            dl.AddRect(min, full, Col(Text, 0.6f * alpha), 0f, ImDrawFlags.None, 1.5f);
        }
        max = full;

        if (Vfx.InnerFade)
        {
            var inset = ContentInset - S(6f);
            var feather = S(36f);
            var bMin = min + new Vector2(inset);
            var bMax = max - new Vector2(inset);
            var on = Col(Body, 0.58f * (OverBackdrop ? FillAlphaScale : 1f) * alpha);
            var off = Col(Body, 0f);
            if (bMax.X - bMin.X > feather * 2f && bMax.Y - bMin.Y > feather * 2f)
            {
                dl.AddRectFilled(bMin + new Vector2(feather), bMax - new Vector2(feather), on);
                dl.AddRectFilledMultiColor(new Vector2(bMin.X + feather, bMin.Y), new Vector2(bMax.X - feather, bMin.Y + feather), off, off, on, on);
                dl.AddRectFilledMultiColor(new Vector2(bMin.X + feather, bMax.Y - feather), new Vector2(bMax.X - feather, bMax.Y), on, on, off, off);
                dl.AddRectFilledMultiColor(new Vector2(bMin.X, bMin.Y + feather), new Vector2(bMin.X + feather, bMax.Y - feather), off, on, on, off);
                dl.AddRectFilledMultiColor(new Vector2(bMax.X - feather, bMin.Y + feather), new Vector2(bMax.X, bMax.Y - feather), on, off, off, on);
                CornerFade(dl, new Vector2(bMin.X, bMin.Y), new Vector2(1, 1), feather, on, off);
                CornerFade(dl, new Vector2(bMax.X, bMin.Y), new Vector2(-1, 1), feather, on, off);
                CornerFade(dl, new Vector2(bMax.X, bMax.Y), new Vector2(-1, -1), feather, on, off);
                CornerFade(dl, new Vector2(bMin.X, bMax.Y), new Vector2(1, -1), feather, on, off);
            }
        }

        if (Vfx.Cracks) DrawCracks(dl, min, max, alpha * I, seed);

        {
            var inset = ContentInset - S(10f);
            dl.AddRect(min + new Vector2(inset), max - new Vector2(inset), Col(FxAccentSoft, 0.30f * alpha), 0f, ImDrawFlags.None, 1f);
            for (int i = 0; i < 18; i++)
            {
                var edge = i % 4;
                var t = Hash(seed + i * 2.9f);
                var d = S(4f + Hash(seed + i * 1.3f + 3f) * (ContentInset - S(8f)));
                var p = edge switch
                {
                    0 => new Vector2(min.X + w * t, min.Y + d),
                    1 => new Vector2(min.X + w * t, max.Y - d),
                    2 => new Vector2(min.X + d, min.Y + h * t),
                    _ => new Vector2(max.X - d, min.Y + h * t),
                };
                dl.AddCircleFilled(p, S(0.8f + Hash(seed + i) * 1.4f), Col(Ink, 0.45f * alpha * I), 6);
            }
        }

        if (header && !string.IsNullOrEmpty(label))
            DrawHeader(dl, min, max, label!, alpha, FxAccentSoft, Muted, Text);

        if (reveal < 0.995f)
        {
            var revealedBottom = MathF.Min(min.Y + fullSize.Y * reveal, sheetMax.Y);
            DrawRoll(dl, sheetMin.X, sheetMax.X, revealedBottom, alpha, 1f - reveal);
        }
        dl.PopClipRect();
    }

    private void DrawRoll(ImDrawListPtr dl, float x0, float x1, float y, float alpha, float rolled)
    {
        var th = S(6f) + S(26f) * rolled;
        var top = y - th;
        var mid = y - th * 0.45f;
        var dark = Col(new Vector4(0.62f, 0.52f, 0.36f, 1f), 0.98f * alpha);
        var light = Col(new Vector4(0.95f, 0.89f, 0.74f, 1f), 0.98f * alpha);
        var edge = Col(new Vector4(0.45f, 0.35f, 0.20f, 1f), 0.98f * alpha);
        dl.AddRectFilledMultiColor(new Vector2(x0, top), new Vector2(x1, mid), edge, edge, light, light);
        dl.AddRectFilledMultiColor(new Vector2(x0, mid), new Vector2(x1, y), light, light, dark, dark);
        dl.AddLine(new Vector2(x0, top + th * 0.3f), new Vector2(x1, top + th * 0.3f), Col(Brown, 0.25f * alpha), 1f);
        dl.AddLine(new Vector2(x0, top + th * 0.72f), new Vector2(x1, top + th * 0.72f), Col(Brown, 0.18f * alpha), 1f);
        dl.AddLine(new Vector2(x0, y), new Vector2(x1, y), Col(Brown, 0.5f * alpha), 1f);
        dl.AddRectFilledMultiColor(new Vector2(x0, y), new Vector2(x1, y + S(14f)),
            Col(new Vector4(0, 0, 0, 1), 0.35f * alpha), Col(new Vector4(0, 0, 0, 1), 0.35f * alpha), Col(new Vector4(0, 0, 0, 1), 0f), Col(new Vector4(0, 0, 0, 1), 0f));
    }

    public override void DrawImageEdges(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha, string key)
    {
        var depth = MathF.Min(S(9f), (max.X - min.X) * 0.12f);
        DrawRaggedEdges(dl, min, max, depth, KeySeed(key), Col(Body, 0.98f * alpha), Col(Brown, 0.45f * alpha), 1f);
    }

    public override void DrawPopBack(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha)
    {
        dl.AddRectFilled(min - new Vector2(S(10f)), max + new Vector2(S(10f)), Col(Body, 0.97f * alpha));
        DrawSoftShadow(dl, min - new Vector2(S(10f)), max + new Vector2(S(10f)), alpha, 0f, 4f);
    }

    private static void CornerFade(ImDrawListPtr dl, Vector2 corner, Vector2 dir, float feather, uint on, uint off)
    {
        var inner = corner + dir * feather;
        var ax = new Vector2(corner.X + dir.X * feather, corner.Y);
        var ay = new Vector2(corner.X, corner.Y + dir.Y * feather);
        dl.PrimReserve(9, 5);
        var uv = ImGui.GetFontTexUvWhitePixel();
        dl.PrimWriteVtx(inner, uv, on);
        dl.PrimWriteVtx(ax, uv, off);
        dl.PrimWriteVtx(corner, uv, off);
        dl.PrimWriteVtx(ay, uv, off);
        dl.PrimWriteVtx(inner, uv, on);
        var b = (uint)(dl.VtxBuffer.Size - 5);
        dl.PrimWriteIdx((ushort)(b + 0)); dl.PrimWriteIdx((ushort)(b + 1)); dl.PrimWriteIdx((ushort)(b + 2));
        dl.PrimWriteIdx((ushort)(b + 0)); dl.PrimWriteIdx((ushort)(b + 2)); dl.PrimWriteIdx((ushort)(b + 3));
        dl.PrimWriteIdx((ushort)(b + 4)); dl.PrimWriteIdx((ushort)(b + 4)); dl.PrimWriteIdx((ushort)(b + 4));
    }

    private void DrawCracks(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha, float seed)
    {
        var w = max.X - min.X; var h = max.Y - min.Y;
        int perH = Math.Max(3, (int)(w / S(110f)));
        int perV = Math.Max(3, (int)(h / S(110f)));
        int c = 0;
        for (int e = 0; e < 4; e++)
        {
            int count = (e < 2) ? perH : perV;
            for (int i = 0; i < count; i++, c++)
            {
                var t = (i + 0.5f + (Hash(seed + c * 3.1f) - 0.5f) * 0.8f) / count;
                Vector2 start, inward;
                switch (e)
                {
                    case 0:  start = new Vector2(min.X + w * t, min.Y + S(2f)); inward = new Vector2(0, 1); break;
                    case 1:  start = new Vector2(min.X + w * t, max.Y - S(2f)); inward = new Vector2(0, -1); break;
                    case 2:  start = new Vector2(min.X + S(2f), min.Y + h * t); inward = new Vector2(1, 0); break;
                    default: start = new Vector2(max.X - S(2f), min.Y + h * t); inward = new Vector2(-1, 0); break;
                }
                var len = S(34f) + Hash(seed + c * 7.7f) * S(70f);
                DrawCrack(dl, start, inward, len, 8, 0.55f * alpha, seed + c * 13.3f, true, Ink, Vector4.One, 0.35f);
            }
        }
        for (int i = 0; i < 3; i++, c++)
        {
            var r = Hash(seed + c * 5.5f);
            Vector2 start, inward;
            if (r < 0.5f) { start = new Vector2(min.X + w * Hash(seed + c), min.Y + S(4f)); inward = new Vector2(0, 1); }
            else          { start = new Vector2(min.X + S(4f), min.Y + h * Hash(seed + c)); inward = new Vector2(1, 0); }
            var len = S(120f) + Hash(seed + c * 2.2f) * S(140f);
            DrawCrack(dl, start, inward, len, 14, 0.42f * alpha, seed + c * 17.1f, true, Ink, Vector4.One, 0.35f);
        }
    }

    public override void DrawPopFrame(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha)
        => DrawImageEdges(dl, min, max, alpha, "pop");

    public override void DrawVeilDecor(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha)
    {
        var size = max - min;
        var c = new Vector2(min.X + size.X * 0.72f, min.Y + size.Y * 0.45f);
        for (int i = 5; i >= 1; i--)
            dl.AddCircleFilled(c, S(100f) * i, Col(FxAccentSoft, 0.016f * alpha * I), 40);
        if (!Vfx.VeilMotes) return;
        for (int i = 0; i < 45; i++)
        {
            var r0 = Hash(i + 50f); var r1 = Hash(i * 1.9f + 11f); var r2 = Hash(i * 3.3f + 17f);
            var x = min.X + ((r0 * size.X + T * (6f + r1 * 10f)) % size.X);
            var y = min.Y + size.Y * r1 + MathF.Sin(T * 0.4f + i) * S(10f);
            var a = (0.18f + r2 * 0.25f) * (0.6f + 0.4f * MathF.Sin(T * 0.8f + i)) * alpha * I;
            dl.AddCircleFilled(new Vector2(x, y), S(1f + r2 * 1.6f), Col(FxAccentSoft, a), 8);
        }
    }
}

// Voidtouched - dark, grungy
public sealed class VoidtouchedTheme : ImmersiveTheme
{
    protected override string DefaultName => "Voidtouched";
    protected override string DefaultTagline => "Corrupted, smouldering void";
    public override ThemeMaterial MaterialKind => ThemeMaterial.Voidtouched;
    protected override Vector4 DefaultAccent     => new(0.68f, 0.32f, 0.96f, 1f);
    protected override Vector4 DefaultAccentSoft => new(0.42f, 0.16f, 0.62f, 1f);
    protected override Vector4 DefaultText       => new(0.88f, 0.82f, 0.95f, 1f);
    protected override Vector4 DefaultMuted      => new(0.56f, 0.48f, 0.68f, 1f);
    protected override Vector4 DefaultDanger     => new(0.98f, 0.28f, 0.48f, 1f);
    protected override Vector4 DefaultSurfaceTop => new(0.06f, 0.02f, 0.09f, 1f);
    protected override Vector4 DefaultSurfaceBot => new(0.01f, 0.00f, 0.03f, 1f);
    protected override PanelEntrance DefaultEntrance => PanelEntrance.Burn;
    protected override float DefaultFillAlpha => 0.92f;
    protected override float DefaultContentInset => S(32f);
    protected override float DefaultHeaderOffsetY => S(10f);
    public override Vector4 Scrim => new(0.02f, 0.00f, 0.04f, 1f);
    protected override float DefaultScrimAlpha => 0.42f;
    public override Vector4 VeilImageTint => new(0.85f, 0.72f, 1.00f, 1f);
    protected override float DefaultVeilImageAlpha => 0.58f;

    public override Vector2 PanelJitter(string id)
    {
        if (!Vfx.Glitch) return Vector2.Zero;
        var seed = KeySeed(id);
        var slot = MathF.Floor(T * 9f);
        var r = Hash(slot + seed);
        if (r < 0.94f) return Vector2.Zero;
        return new Vector2((Hash(slot * 1.3f + seed) - 0.5f) * S(7f), (Hash(slot * 2.1f + seed) - 0.5f) * S(4f));
    }

    public override void DrawMarker(ImDrawListPtr dl, Vector2 center, float r, uint col)
        => dl.AddTriangleFilled(center + new Vector2(-r, -r), center + new Vector2(r * 1.2f, 0f), center + new Vector2(-r, r), col);

    public override void DrawPanelChrome(ImDrawListPtr dl, Vector2 min, Vector2 max, string? label, float alpha, bool header, string key)
    {
        var w = max.X - min.X; var h = max.Y - min.Y;
        var seed = KeySeed(key);

        if (Vfx.Dissolve && alpha < 0.985f)
        {
            var thr = BurnFx.ThresholdFor(alpha);
            BurnFx.DrawDissolveFill(dl, min, max, key, thr, Col(SurfaceTop, FillAlpha));
            BurnFx.DrawBurnFront(dl, min, max, key, thr, FxAccent, I);
            DrawJaggedBorder(dl, min, max, alpha * alpha, seed);
            if (header && !string.IsNullOrEmpty(label))
                DrawHeader(dl, min, max, label!, alpha * alpha, FxAccent, Muted, FxAccent);
            return;
        }

        var tatter = S(14f);
        dl.PushClipRect(min - new Vector2(S(24f)), max + new Vector2(S(24f)), false);
        DrawSoftShadow(dl, min, max, alpha * 1.4f, 0f, 8f);
        var outline = DrawTatteredFill(dl, min, max, tatter, seed, alpha);
        DrawCharredRim(dl, outline, alpha);

        dl.PushClipRect(min, max, true);
        for (int i = 0; i < 34; i++)
        {
            var px = min.X + Hash(i + seed) * w;
            var py = min.Y + Hash(i * 2.7f + seed + 3f) * h;
            dl.AddCircleFilled(new Vector2(px, py), S(8f + Hash(i * 3.9f + seed) * 26f), Col(new Vector4(0f, 0f, 0f, 1f), 0.14f * alpha), 14);
        }
        for (int i = 0; i < 22; i++)
        {
            var px = min.X + Hash(i * 1.1f + seed + 20f) * w;
            var py = min.Y + Hash(i * 2.3f + seed + 21f) * h;
            var ang = Hash(i * 3.1f + seed) * MathF.PI;
            var len = S(8f + Hash(i + seed + 30f) * 30f);
            var d = new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * len;
            dl.AddLine(new Vector2(px, py), new Vector2(px, py) + d, Col((i & 1) == 0 ? FxAccent : Vector4.One, 0.06f * alpha), 1f);
        }
        var ch0 = Col(new Vector4(0f, 0f, 0f, 1f), 0.55f * alpha);
        var ch1 = Col(new Vector4(0f, 0f, 0f, 1f), 0f);
        dl.AddRectFilledMultiColor(new Vector2(min.X, max.Y - S(22f)), max, ch1, ch1, ch0, ch0);
        if (Vfx.Fissures)
        {
            var n = 3 + (int)((w + h) / S(220f));
            for (int i = 0; i < n; i++)
            {
                var edge = i % 4;
                var t = Hash(seed + i * 4.7f + 40f);
                Vector2 start, inward;
                switch (edge)
                {
                    case 0:  start = new Vector2(min.X + w * t, min.Y); inward = new Vector2(0, 1); break;
                    case 1:  start = new Vector2(min.X + w * t, max.Y); inward = new Vector2(0, -1); break;
                    case 2:  start = new Vector2(min.X, min.Y + h * t); inward = new Vector2(1, 0); break;
                    default: start = new Vector2(max.X, min.Y + h * t); inward = new Vector2(-1, 0); break;
                }
                var len = S(60f) + Hash(seed + i * 6.1f) * S(140f);
                DrawCrack(dl, start, inward, len, 12, 0.22f * alpha * I, seed + i * 19.3f, true, FxAccent, FxAccent, 0f, thickness: 5f);
                DrawCrack(dl, start, inward, len, 12, 0.85f * alpha * I, seed + i * 19.3f, true, new Vector4(0.92f, 0.82f, 1f, 1f), FxAccent, 0f, thickness: 1.2f);
            }
        }
        dl.PopClipRect();
        dl.PopClipRect();

        if (header && !string.IsNullOrEmpty(label))
            DrawHeader(dl, min, max, label!, alpha, FxAccent, Muted, FxAccent);
    }

    private Vector2[] DrawTatteredFill(ImDrawListPtr dl, Vector2 min, Vector2 max, float depth, float seed, float alpha)
    {
        var w = max.X - min.X; var h = max.Y - min.Y;
        // Small rects (tooltips) can be narrower than twice the bite depth.
        depth = MathF.Max(0f, MathF.Min(depth, MathF.Min(w, h) * 0.5f - 1f));
        var inner = new Vector2(min.X + depth, min.Y + depth);
        var innerMax = new Vector2(max.X - depth, max.Y - depth);
        var fillTop = Col(SurfaceTop, FillAlpha * alpha);
        var fillBot = Col(SurfaceBot, MathF.Min(1f, FillAlpha * 1.06f) * alpha);
        dl.AddRectFilledMultiColor(inner, innerMax, fillTop, fillTop, fillBot, fillBot);

        int nTop = Math.Max(6, (int)(w / S(11f)));
        int nSide = Math.Max(6, (int)(h / S(11f)));
        var pts = new Vector2[2 * (nTop + nSide)];
        int k = 0;
        float Bite(float i, float edgeSeed, float t)
        {
            var n = 0.5f + 0.5f * MathF.Sin(i * 1.9f + edgeSeed) * 0.6f + (Hash(seed + edgeSeed + i) - 0.5f) * 0.8f;
            var tear = Hash(seed + edgeSeed * 3f + MathF.Floor(i / 4f)) > 0.82f ? 0.9f : 0f;
            var cornerChew = MathF.Max(0f, 1f - MathF.Min(t, 1f - t) * 8f) * 0.5f;
            return Math.Clamp(n * 0.7f + tear + cornerChew, 0.05f, 1f);
        }
        for (int i = 0; i < nTop; i++)  { var t = i / (float)nTop;  pts[k++] = new Vector2(min.X + w * t, min.Y + depth * Bite(i, 1f, t)); }
        for (int i = 0; i < nSide; i++) { var t = i / (float)nSide; pts[k++] = new Vector2(max.X - depth * Bite(i, 2f, t), min.Y + h * t); }
        for (int i = 0; i < nTop; i++)  { var t = i / (float)nTop;  pts[k++] = new Vector2(max.X - w * t, max.Y - depth * Bite(i, 3f, t)); }
        for (int i = 0; i < nSide; i++) { var t = i / (float)nSide; pts[k++] = new Vector2(min.X + depth * Bite(i, 4f, t), max.Y - h * t); }

        Vector2 Project(Vector2 p)
            => new(Math.Clamp(p.X, inner.X, innerMax.X), Math.Clamp(p.Y, inner.Y, innerMax.Y));
        for (int i = 0; i < pts.Length; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Length];
            var ia = Project(a);
            var ib = Project(b);
            var ty = Math.Clamp(((a.Y + b.Y) * 0.5f - min.Y) / MathF.Max(1f, h), 0f, 1f);
            var c = Col(new Vector4(
                SurfaceTop.X + (SurfaceBot.X - SurfaceTop.X) * ty,
                SurfaceTop.Y + (SurfaceBot.Y - SurfaceTop.Y) * ty,
                SurfaceTop.Z + (SurfaceBot.Z - SurfaceTop.Z) * ty, 1f), FillAlpha * alpha);
            dl.AddQuadFilled(ia, ib, b, a, c);
            var sa = a + (ia - a) * 0.35f;
            var sb = b + (ib - b) * 0.35f;
            dl.AddQuadFilled(sa, sb, b, a, Col(new Vector4(0.02f, 0f, 0.03f, 1f), 0.75f * alpha));
        }
        return pts;
    }

    private void DrawCharredRim(ImDrawListPtr dl, Vector2[] pts, float alpha)
    {
        var pulse = 0.85f + 0.15f * MathF.Sin(T * 0.9f);
        var heat = Col(FxAccent, 0.26f * alpha * pulse * I);
        var heat2 = Col(FxAccent, 0.55f * alpha * pulse * I);
        var chr = Col(new Vector4(0.03f, 0f, 0.05f, 1f), 0.95f * alpha);
        for (int i = 0; i < pts.Length; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Length];
            dl.AddLine(a, b, heat, S(7f));
            dl.AddLine(a, b, heat2, S(2.5f));
            dl.AddLine(a, b, chr, 1.5f);
        }
        for (int i = 0; i < pts.Length; i += 5)
        {
            if (Hash(i * 3.7f + 11f) < 0.45f) continue;
            var p = pts[i];
            var f = 0.6f + 0.4f * MathF.Sin(T * 2.2f + i);
            dl.AddCircleFilled(p, S(4f), Col(FxAccent, 0.30f * f * alpha * I), 10);
            dl.AddCircleFilled(p, S(1.6f), Col(new Vector4(0.95f, 0.85f, 1f, 1f), 0.9f * f * alpha * I), 8);
        }
    }

    private void DrawJaggedBorder(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha, float seed)
    {
        var col = Col(FxAccent, 0.8f * alpha);
        var j = S(1.8f);
        void Edge(Vector2 a, Vector2 b, float s)
        {
            const int n = 22;
            var prev = a;
            for (int i = 1; i <= n; i++)
            {
                var t = i / (float)n;
                var p = a + (b - a) * t;
                if (i < n)
                {
                    var dir = Vector2.Normalize(b - a);
                    var nrm = new Vector2(-dir.Y, dir.X);
                    p += nrm * (Hash(i + s) - 0.5f) * 2f * j;
                }
                dl.AddLine(prev, p, col, 1.5f);
                prev = p;
            }
        }
        Edge(min, new Vector2(max.X, min.Y), seed);
        Edge(new Vector2(max.X, min.Y), max, seed + 1f);
        Edge(max, new Vector2(min.X, max.Y), seed + 2f);
        Edge(new Vector2(min.X, max.Y), min, seed + 3f);
    }

    private void DrawClaws(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha)
    {
        var col = Col(FxAccent, alpha);
        void Claw(Vector2 corner, Vector2 dir)
        {
            for (int i = 0; i < 3; i++)
            {
                var off = S(4f) * i;
                var start = corner + new Vector2(dir.X * off, dir.Y * (S(2f) + off * 0.4f));
                var len = S(16f) - i * S(3f);
                dl.AddLine(start, start + new Vector2(dir.X * len, dir.Y * len * 0.8f), col, 2f - i * 0.4f);
            }
        }
        Claw(min, new Vector2(1, 1));
        Claw(new Vector2(max.X, min.Y), new Vector2(-1, 1));
        Claw(max, new Vector2(-1, -1));
        Claw(new Vector2(min.X, max.Y), new Vector2(1, -1));
    }

    public override void DrawImageEdges(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha, string key)
    {
        var depth = MathF.Min(S(9f), (max.X - min.X) * 0.12f);
        DrawRaggedEdges(dl, min, max, depth, KeySeed(key), Col(new Vector4(0.02f, 0f, 0.03f, 1f), 0.95f * alpha), Col(FxAccent, 0.55f * alpha), 1.2f);
    }

    public override void DrawPopBack(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha)
    {
        for (int i = 4; i >= 1; i--)
            dl.AddRectFilled(min - new Vector2(S(4f) * i), max + new Vector2(S(4f) * i), Col(FxAccent, 0.07f * alpha), S(2f));
    }

    public override void DrawPopFrame(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha)
        => DrawImageEdges(dl, min, max, alpha, "pop");

    public override void DrawVeilDecor(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha)
    {
        if (!Vfx.Embers) return;
        BurnFx.DrawRisingEmbers(dl, min, max, FxAccent, "veil", 0.85f * alpha * I, 80);
    }
}

// Aether - luminous
public sealed class AetherTheme : ImmersiveTheme
{
    protected override string DefaultName => "Aether";
    protected override string DefaultTagline => "Luminous aetheric currents";
    public override ThemeMaterial MaterialKind => ThemeMaterial.Aether;
    protected override Vector4 DefaultAccent     => new(0.58f, 0.82f, 1.00f, 1f);
    protected override Vector4 DefaultAccentSoft => new(0.36f, 0.56f, 0.96f, 1f);
    protected override Vector4 DefaultText       => new(0.93f, 0.97f, 1.00f, 1f);
    protected override Vector4 DefaultMuted      => new(0.62f, 0.72f, 0.88f, 1f);
    protected override Vector4 DefaultDanger     => new(1.00f, 0.50f, 0.62f, 1f);
    protected override Vector4 DefaultSurfaceTop => new(0.05f, 0.09f, 0.20f, 1f);
    protected override Vector4 DefaultSurfaceBot => new(0.02f, 0.04f, 0.11f, 1f);
    protected override float DefaultRounding => S(12f);
    protected override float DefaultFillAlpha => 0.80f;
    protected override float DefaultContentInset => S(22f);
    protected override bool DefaultTechAccents => false;
    protected override bool DefaultElementGlow => true;
    protected override PanelEntrance DefaultEntrance => PanelEntrance.Drift;
    protected override float DefaultFloatAmount => 2.2f;
    public override Vector4 Scrim => new(0.02f, 0.04f, 0.10f, 1f);
    protected override float DefaultScrimAlpha => 0.30f;
    protected override float DefaultVeilImageAlpha => 0.60f;

    public override void DrawMarker(ImDrawListPtr dl, Vector2 center, float r, uint col)
    {
        dl.AddCircleFilled(center, r * 1.8f, ImmersiveMode.Col(FxAccent, 0.18f), 16);
        dl.AddCircleFilled(center, r * 0.75f, col, 16);
    }

    public override void DrawPanelChrome(ImDrawListPtr dl, Vector2 min, Vector2 max, string? label, float alpha, bool header, string key)
    {
        var breathe = 0.85f + 0.15f * MathF.Sin(T * 1.1f + min.X * 0.01f);
        var pad = S(70f);
        dl.PushClipRect(min - new Vector2(pad), max + new Vector2(pad), false);
        if (Vfx.Halo) DrawHalo(dl, min, max, FxAccent, 0.16f * alpha * breathe * I, S(34f), Rounding, 8);
        DrawGradientFill(dl, min, max, alpha);
        dl.PushClipRect(min, max, true);
        dl.AddRectFilledMultiColor(min, new Vector2(max.X, min.Y + S(30f)), Col(Vector4.One, 0.06f * alpha), Col(Vector4.One, 0.06f * alpha), Col(Vector4.One, 0f), Col(Vector4.One, 0f));
        dl.PopClipRect();
        dl.AddRect(min, max, Col(FxAccent, 0.55f * alpha * breathe), Rounding, ImDrawFlags.None, 1.5f);
        dl.AddRect(min + new Vector2(2f), max - new Vector2(2f), Col(FxAccent, 0.16f * alpha), MathF.Max(0f, Rounding - 2f), ImDrawFlags.None, 1f);
        if (Vfx.EdgeMotes) DrawEdgeMotes(dl, min, max, alpha * I, key);
        dl.PopClipRect();

        if (header && !string.IsNullOrEmpty(label))
        {
            DrawHeader(dl, min, max, label!, alpha * 0.35f, FxAccent, FxAccent, FxAccent);
            DrawHeader(dl, min, max, label!, alpha, FxAccent, Muted, FxAccent);
        }
    }

    private void DrawEdgeMotes(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha, string key)
    {
        var w = max.X - min.X; var h = max.Y - min.Y;
        var seed = KeySeed(key);
        int count = 18 + (int)((w + h) / S(60f));
        var core = new Vector4(0.90f, 0.97f, 1f, 1f);
        for (int i = 0; i < count; i++)
        {
            var r0 = Hash(seed + i);
            var r1 = Hash(seed + i * 2.3f + 5f);
            var r2 = Hash(seed + i * 4.1f + 9f);
            var speed = 0.18f + r1 * 0.22f;
            var life = (T * speed + r0) % 1f;
            var per = Hash(seed + i * 1.7f + 21f);
            Vector2 p0, nrm;
            var total = 2f * (w + h);
            var d = per * total;
            if (d < w)              { p0 = new Vector2(min.X + d, min.Y); nrm = new Vector2(0, -1); }
            else if (d < w + h)     { p0 = new Vector2(max.X, min.Y + (d - w)); nrm = new Vector2(1, 0); }
            else if (d < 2 * w + h) { p0 = new Vector2(max.X - (d - w - h), max.Y); nrm = new Vector2(0, 1); }
            else                    { p0 = new Vector2(min.X, max.Y - (d - 2 * w - h)); nrm = new Vector2(-1, 0); }
            var travel = S(18f + r2 * 40f);
            var p = p0 + nrm * travel * life + new Vector2(MathF.Sin(T * 1.7f + i) * S(4f), -life * S(22f));
            var fade = MathF.Sin(life * MathF.PI);
            var twinkle = 0.65f + 0.35f * MathF.Sin(T * (6f + r1 * 6f) + i);
            var a = fade * twinkle * alpha * (0.5f + r2 * 0.5f);
            if (a < 0.02f) continue;
            var size = S(1.2f + r1 * 2.2f);
            dl.AddCircleFilled(p, size * 3.5f, Col(FxAccent, a * 0.18f), 10);
            dl.AddCircleFilled(p, size, Col(core, a), 8);
        }
    }

    public override void DrawPopBack(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha)
        => DrawHalo(dl, min, max, FxAccent, 0.40f * alpha, S(28f), S(4f), 6);

    public override void DrawPopFrame(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha)
    {
        dl.AddRect(min, max, Col(FxAccent, 0.85f * alpha), S(4f), ImDrawFlags.None, 1.2f);
        var core = new Vector4(0.92f, 0.97f, 1f, 1f);
        var w = max.X - min.X; var h = max.Y - min.Y;
        for (int i = 0; i < 14; i++)
        {
            var r0 = Hash(i + 700f); var r1 = Hash(i * 1.7f + 701f);
            var life = (T * (0.3f + r1 * 0.3f) + r0) % 1f;
            var per = Hash(i * 2.9f + 702f) * 2f * (w + h);
            Vector2 p0, nrm;
            if (per < w) { p0 = new Vector2(min.X + per, min.Y); nrm = new Vector2(0, -1); }
            else if (per < w + h) { p0 = new Vector2(max.X, min.Y + per - w); nrm = new Vector2(1, 0); }
            else if (per < 2 * w + h) { p0 = new Vector2(max.X - (per - w - h), max.Y); nrm = new Vector2(0, 1); }
            else { p0 = new Vector2(min.X, max.Y - (per - 2 * w - h)); nrm = new Vector2(-1, 0); }
            var p = p0 + nrm * S(6f + 22f * life) + new Vector2(0, -life * S(10f));
            var a = MathF.Sin(life * MathF.PI) * alpha;
            dl.AddCircleFilled(p, S(4f), Col(FxAccent, 0.2f * a), 8);
            dl.AddCircleFilled(p, S(1.4f), Col(core, a), 8);
        }
    }

    public override void DrawVeilDecor(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha)
    {
        var size = max - min;
        if (Vfx.Ribbons)
        {
            var rMin = new Vector2(min.X + size.X * 0.30f, min.Y);
            RibbonFx.DrawBackgroundRibbonsDiagonal(dl, rMin, max, new Vector4(FxAccent.X, FxAccent.Y, FxAccent.Z, 0.55f * alpha * I), "aether_veil", 0.55f);
        }
        var c = new Vector2(min.X + size.X * 0.72f, min.Y + size.Y * 0.5f);
        for (int i = 6; i >= 1; i--)
            dl.AddCircleFilled(c, S(120f) * i, Col(FxAccent, 0.014f * alpha * I), 48);
        if (!Vfx.VeilMotes) return;
        for (int i = 0; i < 50; i++)
        {
            var r0 = Hash(i + 300f); var r1 = Hash(i * 1.7f + 301f); var r2 = Hash(i * 2.9f + 302f);
            var life = (T * (0.02f + r1 * 0.03f) + r0) % 1f;
            var x = min.X + size.X * (0.3f + r0 * 0.7f) + MathF.Sin(T * 0.4f + i) * S(16f);
            var y = max.Y - life * size.Y;
            var a = MathF.Sin(life * MathF.PI) * (0.2f + r2 * 0.4f) * alpha * I;
            dl.AddCircleFilled(new Vector2(x, y), S(1f + r2 * 2f), Col(FxAccent, a), 8);
        }
    }
}

// Standard - the plugin's own window look, unthemed The default until someone picks or makes another theme: the same flat surfaces, borders and accent the rest of the plugin's windows use, with no material effects, glow, float or tech readouts.
public sealed class StandardTheme : ImmersiveTheme
{
    protected override string DefaultName => "Standard";
    protected override string DefaultTagline => "The plain window look, no theming";
    public override ThemeMaterial MaterialKind => ThemeMaterial.Standard;
    protected override Vector4 DefaultAccent     => RsTheme.AccentPrimary;
    protected override Vector4 DefaultAccentSoft => RsTheme.BorderStrong;
    protected override Vector4 DefaultText       => RsTheme.TextPrimary;
    protected override Vector4 DefaultMuted      => RsTheme.TextMuted;
    protected override Vector4 DefaultDanger     => RsTheme.AccentDanger;
    protected override Vector4 DefaultSurfaceTop => RsTheme.BgSecondary;
    protected override Vector4 DefaultSurfaceBot => RsTheme.BgSecondary;

    protected override float DefaultRounding => RsTheme.CornerRadius;
    protected override float DefaultFillAlpha => 0.96f;
    protected override float DefaultFloatAmount => 0f;
    protected override bool  DefaultTechAccents => false;

    public override Vector4 Scrim => new(0f, 0f, 0f, 1f);
    public override Vector4 VeilImageTint => Vector4.One;

    public override void DrawPanelChrome(ImDrawListPtr dl, Vector2 min, Vector2 max, string? label, float alpha, bool header, string key)
    {
        dl.AddRectFilled(min, max, Col(SurfaceTop, FillAlpha * alpha), Rounding);
        dl.AddRect(min, max, Col(RsTheme.Border, alpha), Rounding, ImDrawFlags.None, RsTheme.BorderThickness);
        if (header && !string.IsNullOrEmpty(label))
        {
            var pad = MathF.Max(S(14f), ContentInset - S(2f));
            var sz = ImGui.CalcTextSize(label);
            var y = min.Y + (ImmersiveMode.HeaderHeight - sz.Y) * 0.5f + S(3f) + HeaderOffsetY;
            dl.AddText(new Vector2(min.X + pad, y), Col(Text, alpha), label);
            var ly = min.Y + ImmersiveMode.HeaderHeight + HeaderOffsetY;
            dl.AddLine(new Vector2(min.X + pad, ly), new Vector2(max.X - pad, ly), Col(RsTheme.Border, alpha), 1f);
        }
    }

    public override void DrawVeilDecor(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha) { }

    public override void DrawPopFrame(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha)
        => dl.AddRect(min, max, Col(RsTheme.BorderStrong, alpha), Rounding, ImDrawFlags.None, RsTheme.BorderThickness);

    public override void DrawMarker(ImDrawListPtr dl, Vector2 center, float r, uint col)
        => dl.AddCircleFilled(center, r, col);
}

// registry
public static class ImmersiveThemes
{
    // The shipped materials in their native layouts.
    public static readonly ImmersiveTheme[] All =
    {
        new AllaganTheme(),
        new NymianTheme(),
        new SharlayanTheme(),
        new VoidtouchedTheme(),
        new AetherTheme(),
        // New built-ins go at the end: "builtin:N" refs and profile theme ids (N + 1) are saved, so existing entries must keep their index.
        new StandardTheme(),
    };

    // What everyone sees until they pick or make something else.
    public const int DefaultIndex = 5;
    public static ImmersiveTheme Default => All[DefaultIndex];
    public const string DefaultRef = "builtin:5";

    // Gallery / dropdown order: the default first, then the materials.
    public static readonly int[] DisplayOrder = { 5, 0, 1, 2, 3, 4 };

    // The viewer's own theme: their saved pick, else the default. (A legacy numeric pick above 0 still counts; 0 was never distinguishable from "never chose".) Owners pick a theme per profile; a profile left on "viewer's choice" shows in the default theme (the viewer-side setting was removed).
    public static string ViewerRef(Configuration? cfg) => DefaultRef;
    public static ImmersiveTheme ViewerTheme(Configuration? cfg) => Resolve(ViewerRef(cfg));

    public static readonly string[] Names = Array.ConvertAll(All, t => t.Name);

    // Profile-owner choices for the wire: 0 = viewer's choice, 1..5 built-ins. Gallery themes use ids >= GalleryIdBase.
    public const int GalleryIdBase = 1000;
    public static readonly string[] ProfileChoiceNames = BuildProfileChoices();
    private static string[] BuildProfileChoices()
    {
        var arr = new string[All.Length + 1];
        arr[0] = "Viewer's choice";
        for (int i = 0; i < All.Length; i++) arr[i + 1] = All[i].Name;
        return arr;
    }

    public static ImmersiveTheme Get(int index) => All[Math.Clamp(index, 0, All.Length - 1)];

    public static ImmersiveTheme CreateMaterial(ThemeMaterial m) => m switch
    {
        ThemeMaterial.Nymian => new NymianTheme(),
        ThemeMaterial.Sharlayan => new SharlayanTheme(),
        ThemeMaterial.Voidtouched => new VoidtouchedTheme(),
        ThemeMaterial.Aether => new AetherTheme(),
        ThemeMaterial.Standard => new StandardTheme(),
        _ => new AllaganTheme(),
    };

    // Document-driven instances are cached per document revision so we don't rebuild a theme object every frame.
    private static readonly Dictionary<string, (long stamp, ImmersiveTheme theme)> _docCache = new();
    public static ImmersiveTheme FromDocument(ThemeDocument doc)
    {
        var key = doc.LocalId;
        var stamp = doc.UpdatedUtc.Ticks;
        if (_docCache.TryGetValue(key, out var e) && e.stamp == stamp) return e.theme;
        var t = CreateMaterial(doc.Material);
        t.Document = doc;
        t.PaletteOverride = doc.Palette;
        t.StyleOverride = doc.Style;
        t.Vfx = doc.Vfx ?? new ThemeVfx();
        _docCache[key] = (stamp, t);
        return t;
    }
    public static void InvalidateDocument(ThemeDocument doc) => _docCache.Remove(doc.LocalId);

    // Resolve a theme reference ("builtin:2", "local:<id>", "server:<id>").
    public static ImmersiveTheme Resolve(string? themeRef)
    {
        if (string.IsNullOrEmpty(themeRef)) return Default;
        var parts = themeRef.Split(':', 2);
        if (parts.Length != 2) return Default;
        switch (parts[0])
        {
            case "builtin":
                return int.TryParse(parts[1], out var bi) ? Get(bi) : Default;
            case "local":
                {
                    var doc = ThemeLibrary.GetLocal(parts[1]);
                    return doc != null ? FromDocument(doc) : Default;
                }
            case "server":
                {
                    if (!int.TryParse(parts[1], out var sid)) return Default;
                    var doc = ThemeLibrary.GetServer(sid);
                    return doc != null ? FromDocument(doc) : Default;
                }
        }
        return Default;
    }

    // The editor sets this to preview the theme being edited.
    public static ImmersiveTheme? PreviewOverride;

    // The theme in effect right now: the editor preview, else the profile owner's pick when they set one and the viewer allows it, else the viewer's own.
    public static ImmersiveTheme Active
    {
        get
        {
            if (PreviewOverride != null) return PreviewOverride;
            var cfg = Plugin.plugin?.Configuration;
            var mine = ViewerTheme(cfg);
            if (cfg == null || cfg.ImmersiveUseProfileTheme)
            {
                var pd = TargetProfileWindow.profileData;
                if (pd != null && TargetProfileWindow.ExistingProfile && pd.immersiveTheme > 0)
                {
                    if (pd.immersiveTheme >= GalleryIdBase)
                    {
                        var doc = ThemeLibrary.GetServer(pd.immersiveTheme);
                        if (doc != null) return FromDocument(doc);
                        ThemeLibrary.RequestServerTheme(pd.immersiveTheme);
                        return mine;
                    }
                    return Get(pd.immersiveTheme - 1);
                }
            }
            return mine;
        }
    }

    // Profile-owner choices: what a profile can be set to show viewers. 0 = viewer's choice, 1..N built-ins, then gallery ids the owner may wear (installed themes and their own saved themes).
    public static List<(string label, int value)> OwnerChoices()
    {
        var list = new List<(string, int)> { ("Viewer's choice", 0) };
        foreach (var i in DisplayOrder) list.Add((All[i].Name, i + 1));
        var seen = new HashSet<int>();
        foreach (var d in ThemeLibrary.Installed)
            if (d.ServerId > 0 && seen.Add(d.ServerId)) list.Add((d.Name + "  (installed)", d.ServerId));
        foreach (var d in ThemeLibrary.Drafts)
            if (d.ServerId > 0 && seen.Add(d.ServerId)) list.Add((d.Name + "  (mine)", d.ServerId));
        return list;
    }

    // Theme for a piece of UI that belongs to another player (their tooltip): their choice when set and allowed, else the viewer's own.
    public static ImmersiveTheme ForOwnerChoice(int ownerThemeId)
    {
        if (PreviewOverride != null) return PreviewOverride;
        var cfg = Plugin.plugin?.Configuration;
        var mine = ViewerTheme(cfg);
        if (cfg != null && !cfg.ImmersiveUseProfileTheme) return mine;
        if (ownerThemeId <= 0) return mine;
        if (ownerThemeId >= GalleryIdBase)
        {
            var doc = ThemeLibrary.GetServer(ownerThemeId);
            if (doc != null) return FromDocument(doc);
            ThemeLibrary.RequestServerTheme(ownerThemeId);
            return mine;
        }
        return Get(ownerThemeId - 1);
    }

    // Viewer-facing choices for the settings dropdown.
    public static List<(string label, string themeRef)> ViewerChoices()
    {
        var list = new List<(string, string)>();
        foreach (var i in DisplayOrder) list.Add((All[i].Name + (i == DefaultIndex ? "  (default)" : ""), "builtin:" + i));
        foreach (var d in ThemeLibrary.Installed) list.Add((d.Name + "  (installed)", d.Ref));
        foreach (var d in ThemeLibrary.Drafts) list.Add((d.Name + "  (draft)", d.Ref));
        return list;
    }
}
