using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AbsoluteRP.RsUI;
using Dalamud.Bindings.ImGui;

namespace AbsoluteRP.Immersive.Themes;

// A cheap schematic of a theme: its veil and real panel chrome laid out to scale in a small rect, with elements shown as blocks. Enough to judge the look and layout of a theme before installing it, without rendering an actual profile.
public static class ThemeThumbnail
{
    private static readonly Dictionary<int, ThemeDocument> _builtins = new();

    public static ThemeDocument Builtin(int index)
    {
        if (!_builtins.TryGetValue(index, out var d)) { d = BuiltinDocuments.Create(index); _builtins[index] = d; }
        return d;
    }

    public static void Draw(ImDrawListPtr dl, ThemeDocument doc, Vector2 min, Vector2 max, float alpha = 1f)
    {
        var w = max.X - min.X; var h = max.Y - min.Y;
        if (w < 8f || h < 8f) return;
        var theme = ImmersiveThemes.FromDocument(doc);
        var S = (Func<float, float>)RsTheme.S;
        // Materials push their own clip rects (with intersect off) for halos and torn edges, which would spill past the box; every draw command added from here on is clamped back to it afterwards.
        var cmdStart = dl.CmdBuffer.Size;
        // Clamp to the box AND whatever the window is already clipped to, so a card scrolled half out of view doesn't paint past the window.
        var clipMin = Vector2.Max(min, dl.GetClipRectMin());
        var clipMax = Vector2.Min(max, dl.GetClipRectMax());
        if (clipMax.X <= clipMin.X || clipMax.Y <= clipMin.Y) return;
        dl.PushClipRect(min, max, true);
        try
        {
            // Backdrop stand-in + the theme's veil.
            dl.AddRectFilledMultiColor(min, max,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.16f, 0.18f, 0.22f, alpha)), ImGui.ColorConvertFloat4ToU32(new Vector4(0.12f, 0.14f, 0.18f, alpha)),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.08f, 0.09f, 0.11f, alpha)), ImGui.ColorConvertFloat4ToU32(new Vector4(0.10f, 0.11f, 0.13f, alpha)));
            dl.AddRectFilled(min, max, ImmersiveMode.Col(theme.Scrim, theme.ScrimAlpha * alpha));
            try { theme.DrawVeilDecor(dl, min, max, 0.8f * alpha); } catch { }

            // Panels, to scale, in draw order; children follow parents.
            var drawn = new Dictionary<string, (Vector2 min, Vector2 size)>();
            var order = ImmersiveHud.DrawOrder(doc);
            // Two passes so behind-parent children can still find their parent.
            foreach (var p in order.Where(p => !p.IsControls))
                Layout(doc, p, min, new Vector2(w, h), drawn);
            var minInset = MathF.Min(theme.ContentInset, S(4f));
            foreach (var p in order)
            {
                if (!drawn.TryGetValue(p.Id, out var r)) continue;
                var pmin = r.min; var pmax = r.min + r.size;
                var header = !string.IsNullOrEmpty(p.Header);
                ImmersiveMode.PanelReveal = 1f;
                ImmersiveMode.PanelFullSize = r.size;
                if (p.Chrome)
                {
                    try { theme.DrawPanelChrome(dl, pmin, pmax, null, alpha, false, "thumb_" + doc.LocalId + p.Id); }
                    catch { dl.AddRectFilled(pmin, pmax, ImmersiveMode.Col(theme.SurfaceTop, 0.85f * alpha)); }
                }
                else dl.AddRect(pmin, pmax, ImmersiveMode.Col(theme.Accent, 0.25f * alpha));
                if (header)
                {
                    var hh = MathF.Max(2f, r.size.Y * 0.08f);
                    dl.AddRectFilled(pmin + new Vector2(minInset, minInset), new Vector2(pmax.X - minInset, pmin.Y + minInset + hh), ImmersiveMode.Col(theme.Accent, 0.55f * alpha), 1f);
                }
                // Elements as blocks: flow ones stacked, free ones placed.
                var inner = new Vector2(pmin.X + minInset, pmin.Y + minInset + (header ? r.size.Y * 0.11f : 0f));
                var innerSize = new Vector2(MathF.Max(2f, r.size.X - minInset * 2f), MathF.Max(2f, pmax.Y - minInset - inner.Y));
                if (p.IsControls)
                {
                    Blocks(dl, theme, inner, innerSize, new[] { 0.45f, 0.45f }, alpha, buttons: true);
                    continue;
                }
                var els = p.Elements;
                var pinOrder = PinLayout.Order(els, out var pinned);
                var targets = PinLayout.Targets(els, pinned);
                var rects = new Dictionary<string, (Vector2 min, Vector2 max)>();
                var flow = Enumerable.Range(0, els.Count).Where(i => !els[i].Free && !pinned[i]).ToList();
                if (flow.Count > 0)
                {
                    var weights = flow.Select(i => Weight(els[i])).ToArray();
                    var total = weights.Sum();
                    var y = inner.Y;
                    for (int k = 0; k < flow.Count; k++)
                    {
                        var eh = innerSize.Y * (weights[k] / total);
                        Block(dl, theme, els[flow[k]], new Vector2(inner.X, y), new Vector2(innerSize.X, eh), alpha);
                        rects[els[flow[k]].Id] = (new Vector2(inner.X, y), new Vector2(inner.X + innerSize.X, y + eh));
                        y += eh;
                    }
                }
                // Placed elements in pin order: pinned ones hang off their target's block.
                var innerMax = inner + innerSize;
                foreach (var i in pinOrder)
                {
                    var e = els[i];
                    if (!e.Free && !pinned[i]) continue;
                    var sz = new Vector2(e.W * innerSize.X, e.H * innerSize.Y);
                    if (pinned[i] || targets.Contains(e.Id)) sz.Y = MathF.Max(sz.Y, NaturalFrac(e) * innerSize.Y);
                    Vector2 pos;
                    if (pinned[i] && rects.TryGetValue(e.PinTo, out var tr))
                    {
                        pos = PinLayout.Place(e, tr.min, tr.max, sz, e.PinGap * innerSize.Y / 800f);
                        pos.X = Math.Clamp(pos.X, inner.X, MathF.Max(inner.X, innerMax.X - sz.X));
                        pos.Y = MathF.Max(inner.Y, pos.Y);
                        if (PinLayout.Fills(e)) sz.Y = MathF.Max(2f, innerMax.Y - pos.Y);
                    }
                    else pos = inner + new Vector2(e.X * innerSize.X, e.Y * innerSize.Y);
                    Block(dl, theme, e, pos, sz, alpha);
                    rects[e.Id] = (pos, pos + sz);
                }
            }
        }
        finally
        {
            ImmersiveMode.PanelReveal = 1f;
            dl.PopClipRect();
            ClampCommands(dl, cmdStart, clipMin, clipMax);
        }
    }

    // Intersects the clip rect of every command from `from` on with [min,max].
    private static unsafe void ClampCommands(ImDrawListPtr dl, int from, Vector2 min, Vector2 max)
    {
        var buf = dl.CmdBuffer;
        var data = (ImDrawCmd*)buf.Data;
        if (data == null) return;
        for (int i = Math.Max(0, from - 1); i < buf.Size; i++)
        {
            var c = data[i].ClipRect;
            // The command before our first push is ours only if ImGui reused an empty command for our clip (its rect is then exactly the box).
            if (i < from && !(MathF.Abs(c.X - min.X) < 0.5f && MathF.Abs(c.Y - min.Y) < 0.5f && MathF.Abs(c.Z - max.X) < 0.5f && MathF.Abs(c.W - max.Y) < 0.5f)) continue;
            // (min/max here are already the intersected clip bounds)
            data[i].ClipRect = new Vector4(MathF.Max(c.X, min.X), MathF.Max(c.Y, min.Y), MathF.Min(c.Z, max.X), MathF.Min(c.W, max.Y));
        }
    }

    // Mirrors the HUD: the main panel is pinned top-right at its own size; root panels sit on the viewport, children follow their parent.
    private static void Layout(ThemeDocument doc, PanelDef p, Vector2 vpMin, Vector2 vpSize, Dictionary<string, (Vector2 min, Vector2 size)> drawn)
    {
        var main = doc.MainPanel;
        var parent = ImmersiveHud.ParentOf(doc, p);
        Vector2 size, pos;
        if (p == main)
        {
            size = p.W > 0f && p.H > 0f
                ? new Vector2(Math.Clamp(p.W, 0.25f, 1f) * vpSize.X, Math.Clamp(p.H, 0.25f, 1f) * vpSize.Y)
                : new Vector2(vpSize.X * 0.44f, vpSize.Y * 0.86f);
            pos = new Vector2(vpMin.X + vpSize.X * (1f - 0.025f) - size.X, vpMin.Y + vpSize.Y * 0.07f);
        }
        else if (parent != null)
        {
            if (!drawn.TryGetValue(parent.Id, out var pr)) Layout(doc, parent, vpMin, vpSize, drawn);
            if (!drawn.TryGetValue(parent.Id, out pr)) return;
            size = new Vector2(MathF.Max(2f, p.W * pr.size.X), MathF.Max(2f, p.H * pr.size.Y));
            // Gap is in real pixels; scale it to the thumbnail.
            var scale = vpSize.X / MathF.Max(1f, ImGui.GetMainViewport().WorkSize.X);
            var saved = p.Gap; p.Gap = saved * scale;
            try { pos = ImmersiveHud.ParentOf(doc, p) != null ? ImmersiveHud.AttachedPos(p, size, pr.min, pr.size) : ImmersiveHud.AnchorPos(p, size, pr.min, pr.size); }
            finally { p.Gap = saved; }
        }
        else
        {
            size = new Vector2(MathF.Max(2f, p.W * vpSize.X), MathF.Max(2f, p.H * vpSize.Y));
            pos = ImmersiveHud.AnchorPos(p, size, vpMin, vpSize);
        }
        // Only panels placed INSIDE their parent are kept inside it.
        if (parent != null && p != main && p.Attach == PanelAttach.Inside && drawn.TryGetValue(parent.Id, out var cr))
        {
            size = Vector2.Min(size, cr.size);
            pos = Vector2.Clamp(pos, cr.min, cr.min + cr.size - size);
        }
        drawn[p.Id] = (pos, size);
    }

    // Rough natural height (fraction of the panel's inner height) of an element in a pin chain, mirroring what the HUD measures.
    private static float NaturalFrac(ElementDef e)
    {
        var sc = e.Scale <= 0f ? 1f : e.Scale;
        return e.Type switch
        {
            ElementType.Avatar => 0.16f * sc * (e.Size > 0f ? e.Size / 112f : 1.25f),
            ElementType.Title => 0.07f * sc * (e.Size > 0f ? e.Size / 1.45f : 1f),
            ElementType.ControlsRow => 0.045f * sc * (e.Size > 0f ? e.Size / 28f : 1f),
            ElementType.SectionNav => e.Nav == NavStyle.Horizontal ? 0.055f * sc : 0.2f,
            ElementType.Divider => 0.018f,
            ElementType.Text => 0.04f * sc * MathF.Max(0.5f, e.Size),
            _ => 0f,
        };
    }

    private static float Weight(ElementDef e) => e.Type switch
    {
        ElementType.Avatar => 3f,
        ElementType.Title => 1.2f,
        ElementType.Readouts => 1.5f,
        ElementType.SectionNav => e.Nav == NavStyle.Horizontal ? 0.8f : 3f,
        ElementType.SectionBody => 6f,
        ElementType.Divider => 0.2f,
        ElementType.Spacer => 0.3f,
        ElementType.LinkReadout => 0.6f,
        ElementType.Text => 0.6f * MathF.Max(0.5f, e.Size),
        _ => 0.6f,
    };

    private static void Block(ImDrawListPtr dl, ImmersiveTheme theme, ElementDef e, Vector2 pos, Vector2 size, float alpha)
    {
        if (size.X < 1f || size.Y < 1f) return;
        var pad = MathF.Min(size.Y * 0.15f, 1.5f);
        var a = pos + new Vector2(0, pad); var b = pos + size - new Vector2(0, pad);
        switch (e.Type)
        {
            case ElementType.Avatar:
                {
                    var r = MathF.Min(size.X, size.Y) * 0.42f;
                    var c = pos + size * 0.5f;
                    dl.AddCircleFilled(c, r, ImmersiveMode.Col(theme.AccentSoft, 0.5f * alpha), 24);
                    dl.AddCircle(c, r, ImmersiveMode.Col(theme.Accent, 0.9f * alpha), 24, 1f);
                    break;
                }
            case ElementType.Title:
                dl.AddRectFilled(new Vector2(pos.X + size.X * 0.2f, a.Y), new Vector2(pos.X + size.X * 0.8f, b.Y), ImmersiveMode.Col(theme.Text, 0.7f * alpha), 1f);
                break;
            case ElementType.SectionBody:
                for (float y = a.Y; y < b.Y - 1f; y += 3f)
                    dl.AddRectFilled(new Vector2(a.X, y), new Vector2(b.X - size.X * (0.1f + 0.3f * ((int)(y / 3f) % 3) / 3f), MathF.Min(y + 1.5f, b.Y)), ImmersiveMode.Col(theme.Text, 0.35f * alpha));
                break;
            case ElementType.SectionNav:
                {
                    var n = 3;
                    if (e.Nav == NavStyle.Horizontal)
                    {
                        var wv = size.X / n;
                        for (int i = 0; i < n; i++)
                            dl.AddRectFilled(new Vector2(pos.X + i * wv + 0.5f, a.Y), new Vector2(pos.X + (i + 1) * wv - 0.5f, b.Y), ImmersiveMode.Col(theme.Accent, (i == 0 ? 0.8f : 0.35f) * alpha), 1f);
                    }
                    else
                    {
                        var hv = size.Y / n;
                        for (int i = 0; i < n; i++)
                            dl.AddRectFilled(new Vector2(a.X, pos.Y + i * hv + 0.5f), new Vector2(b.X, pos.Y + (i + 1) * hv - 0.5f), ImmersiveMode.Col(theme.Accent, (i == 0 ? 0.8f : 0.35f) * alpha), 1f);
                    }
                    break;
                }
            case ElementType.Divider:
                dl.AddLine(new Vector2(a.X, (a.Y + b.Y) * 0.5f), new Vector2(b.X, (a.Y + b.Y) * 0.5f), ImmersiveMode.Col(theme.Accent, 0.6f * alpha), 1f);
                break;
            case ElementType.Spacer:
                break;
            default:
                dl.AddRectFilled(a, b, ImmersiveMode.Col(theme.Muted, 0.5f * alpha), 1f);
                break;
        }
    }

    private static void Blocks(ImDrawListPtr dl, ImmersiveTheme theme, Vector2 pos, Vector2 size, float[] rows, float alpha, bool buttons)
    {
        var y = pos.Y;
        foreach (var f in rows)
        {
            var hh = size.Y * f;
            dl.AddRect(new Vector2(pos.X, y + 1f), new Vector2(pos.X + size.X, y + hh - 1f), ImmersiveMode.Col(theme.Accent, 0.7f * alpha), 1f);
            y += hh;
        }
    }
}
