using System;
using System.Collections.Generic;
using System.Numerics;
using AbsoluteRP.RsUI;
using Dalamud.Bindings.ImGui;
using Rolspeace.VFX;

namespace AbsoluteRP.Immersive.Themes;

// Per-element decoration: a plate in any material, edge masks, and effects, all independent of the theme the panel wears. Two passes: DrawBack goes behind the element's content, DrawFront over its rim.
public static class ElementFx
{
    private static readonly Dictionary<(ThemeMaterial, string), ImmersiveTheme> _plates = new();

    // A material instance tinted with the active theme's palette, so an Allagan plate inside a Sharlayan theme still uses the theme colours.
    private static ImmersiveTheme Plate(ThemeMaterial m, ImmersiveTheme host)
    {
        var key = (m, host.Document?.LocalId ?? host.Name);
        if (!_plates.TryGetValue(key, out var t))
        {
            t = ImmersiveThemes.CreateMaterial(m);
            _plates[key] = t;
        }
        t.PaletteOverride = host.PaletteOverride ?? host.DefaultPalette;
        t.StyleOverride = host.StyleOverride;
        t.Vfx = host.Vfx;
        return t;
    }

    private static float S(float v) => RsTheme.S(v);
    private static uint Col(Vector4 c, float a) => ImmersiveMode.Col(c, a);
    private static float Hash(float n) { var s = MathF.Sin(n * 127.1f + 311.7f) * 43758.5453f; return s - MathF.Floor(s); }
    private static float Seed(string key) { unchecked { uint h = 2166136261; foreach (var c in key) { h ^= c; h *= 16777619; } return (h % 1000) * 0.37f; } }

    public static void DrawBack(ImDrawListPtr dl, Vector2 min, Vector2 max, ElementStyle st, ImmersiveTheme host, float alpha, string key)
    {
        var accent = st.OverrideAccent ? st.Accent.V : host.Accent;
        var a = alpha * st.Alpha;
        var round = host.Rounding;
        if (st.Glow)
            ImmersiveTheme.DrawHalo(dl, min, max, accent, 0.30f * a, S(22f), round, 6);
        if (st.Chrome)
        {
            var plate = st.PlateMaterial.HasValue ? Plate(st.PlateMaterial.Value, host) : host;
            ImmersiveMode.PanelReveal = 1f;
            ImmersiveMode.PanelFullSize = max - min;
            plate.DrawPanelChrome(dl, min, max, null, a, false, "el_" + key);
        }
        if (st.Background != null && st.Background.IsSet)
        {
            dl.PushClipRect(min, max, true);
            ThemeAssets.Draw(dl, host.Document, st.Background, min, max, a);
            dl.PopClipRect();
        }
        if (st.ScanLines || st.Sweep)
        {
            dl.PushClipRect(min, max, true);
            if (st.ScanLines)
            {
                var gap = MathF.Max(3f, S(4f));
                var scan = Col(accent, 0.06f * a);
                for (float y = min.Y + 1f; y < max.Y; y += gap)
                    dl.AddLine(new Vector2(min.X, y), new Vector2(max.X, y), scan, 1f);
            }
            if (st.Sweep)
            {
                var h = max.Y - min.Y;
                var period = 3.6f;
                var phase = ((ImmersiveMode.Time + min.X * 0.003f) % period) / period;
                var bandH = S(28f);
                var bandY = min.Y - bandH + phase * (h + bandH * 2f);
                var mid = bandY + bandH * 0.5f;
                dl.AddRectFilledMultiColor(new Vector2(min.X, bandY), new Vector2(max.X, mid), Col(accent, 0f), Col(accent, 0f), Col(accent, 0.12f * a), Col(accent, 0.12f * a));
                dl.AddRectFilledMultiColor(new Vector2(min.X, mid), new Vector2(max.X, bandY + bandH), Col(accent, 0.12f * a), Col(accent, 0.12f * a), Col(accent, 0f), Col(accent, 0f));
            }
            dl.PopClipRect();
        }
    }

    public static void DrawFront(ImDrawListPtr dl, Vector2 min, Vector2 max, ElementStyle st, ImmersiveTheme host, float alpha, string key)
    {
        var accent = st.OverrideAccent ? st.Accent.V : host.Accent;
        var a = alpha * st.Alpha;
        var seed = Seed(key);
        var w = max.X - min.X; var h = max.Y - min.Y;
        var depth = MathF.Min(S(10f), MathF.Min(w, h) * 0.15f);

        switch (st.Mask)
        {
            case EdgeMask.Torn:
                {
                    var paper = new Vector4(0.95f, 0.90f, 0.78f, 1f);
                    var brown = new Vector4(0.35f, 0.25f, 0.12f, 1f);
                    ImmersiveTheme.DrawRaggedEdges(dl, min, max, depth, seed, Col(paper, 0.98f * a), Col(brown, 0.45f * a), 1f);
                }
                break;
            case EdgeMask.Scorched:
                ImmersiveTheme.DrawRaggedEdges(dl, min, max, depth, seed, Col(new Vector4(0.02f, 0f, 0.03f, 1f), 0.95f * a), Col(accent, 0.6f * a), 1.2f);
                break;
            case EdgeMask.Brackets:
                dl.AddRect(min, max, Col(accent, 0.45f * a), 0f, ImDrawFlags.None, 1f);
                ImmersiveMode.DrawCornerBrackets(dl, min, max, S(14f), 5f, Col(accent, 0.2f * a));
                ImmersiveMode.DrawCornerBrackets(dl, min, max, S(14f), 2f, Col(accent, 0.95f * a));
                break;
            case EdgeMask.Jagged:
                {
                    var col = Col(accent, 0.85f * a);
                    var j = S(1.8f);
                    void Edge(Vector2 p0, Vector2 p1, float s)
                    {
                        const int n = 18;
                        var prev = p0;
                        for (int i = 1; i <= n; i++)
                        {
                            var t = i / (float)n;
                            var p = p0 + (p1 - p0) * t;
                            if (i < n)
                            {
                                var dir = Vector2.Normalize(p1 - p0);
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
                break;
        }

        if (st.Shimmer)
        {
            var t = ImmersiveMode.Time;
            var per = 2f * (w + h);
            for (int k = 0; k < 2; k++)
            {
                var d = ((t * 60f + k * per * 0.5f + seed * 10f) % per);
                Vector2 p;
                if (d < w) p = new Vector2(min.X + d, min.Y);
                else if (d < w + h) p = new Vector2(max.X, min.Y + d - w);
                else if (d < 2 * w + h) p = new Vector2(max.X - (d - w - h), max.Y);
                else p = new Vector2(min.X, max.Y - (d - 2 * w - h));
                dl.AddCircleFilled(p, S(7f), Col(accent, 0.18f * a), 12);
                dl.AddCircleFilled(p, S(2.5f), Col(new Vector4(0.95f, 0.98f, 1f, 1f), 0.9f * a), 10);
            }
        }

        if (st.EdgeMotes)
        {
            var core = new Vector4(0.92f, 0.97f, 1f, 1f);
            int count = 10 + (int)((w + h) / S(60f));
            var T = ImmersiveMode.Time;
            for (int i = 0; i < count; i++)
            {
                var r0 = Hash(seed + i); var r1 = Hash(seed + i * 2.3f + 5f); var r2 = Hash(seed + i * 4.1f + 9f);
                var life = (T * (0.2f + r1 * 0.2f) + r0) % 1f;
                var d = Hash(seed + i * 1.7f + 21f) * 2f * (w + h);
                Vector2 p0, nrm;
                if (d < w) { p0 = new Vector2(min.X + d, min.Y); nrm = new Vector2(0, -1); }
                else if (d < w + h) { p0 = new Vector2(max.X, min.Y + d - w); nrm = new Vector2(1, 0); }
                else if (d < 2 * w + h) { p0 = new Vector2(max.X - (d - w - h), max.Y); nrm = new Vector2(0, 1); }
                else { p0 = new Vector2(min.X, max.Y - (d - 2 * w - h)); nrm = new Vector2(-1, 0); }
                var p = p0 + nrm * S(14f + r2 * 30f) * life + new Vector2(MathF.Sin(T * 1.7f + i) * S(3f), -life * S(16f));
                var ma = MathF.Sin(life * MathF.PI) * (0.65f + 0.35f * MathF.Sin(T * 6f + i)) * a;
                if (ma < 0.02f) continue;
                var size = S(1.1f + r1 * 1.8f);
                dl.AddCircleFilled(p, size * 3.2f, Col(accent, ma * 0.18f), 8);
                dl.AddCircleFilled(p, size, Col(core, ma), 8);
            }
        }

        if (st.Embers)
            BurnFx.DrawEmbers(dl, min, max, accent, "el_" + key, 0.8f * a, 12 + (int)(w / S(40f)));

        DrawCustom(dl, min, max, st, host, alpha, key, circle: false);
        if (st.Frame != null && st.Frame.IsSet)
            ThemeAssets.Draw(dl, host.Document, st.Frame, min, max, a);
    }

    // User / preset effects assigned to the element. `circle` = treat the rect as the bounds of a circle (avatars) so Ring-shaped effects sit on the portrait's rim.
    public static void DrawCustom(ImDrawListPtr dl, Vector2 min, Vector2 max, ElementStyle st, ImmersiveTheme host, float alpha, string key, bool circle)
    {
        if (st.Fx == null || st.Fx.Count == 0) return;
        var accent = st.OverrideAccent ? st.Accent.V : host.Accent;
        var a = alpha * st.Alpha * MathF.Max(0f, host.Vfx?.Intensity ?? 1f);
        if (a <= 0.01f) return;
        for (int i = 0; i < st.Fx.Count; i++)
        {
            var fx = FxPresets.Resolve(host.Document, st.Fx[i]);
            if (fx == null) continue;
            FxRenderer.Draw(dl, fx, min, max, accent, a, key + "/" + i, circle);
        }
    }
}
