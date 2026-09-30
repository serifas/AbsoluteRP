using System;
using System.Collections.Generic;
using System.Numerics;
using AbsoluteRP.RsUI;
using Dalamud.Bindings.ImGui;

namespace AbsoluteRP.Immersive.Themes;

// Renders an FxDef around a rect (or, for avatars, the circle inside it). Everything is stateless and deterministic from time + a seed, so it needs no per-frame bookkeeping and looks the same for every viewer. Vocabulary (see FxDef): Shape says where things grow from, Direction which way they go, Kind what they are. The 0..2 sliders scale the built-in look, so "1" on everything reproduces the shipped effects.
public static class FxRenderer
{
    private static float S(float v) => RsTheme.S(v);
    private static uint Col(Vector4 c, float a) => ImmersiveMode.Col(c, a);
    private static float Hash(float n) { var s = MathF.Sin(n * 127.1f + 311.7f) * 43758.5453f; return s - MathF.Floor(s); }
    private static float Seed(string key) { unchecked { uint h = 2166136261; foreach (var c in key) { h ^= c; h *= 16777619; } return (h % 1000) * 0.37f; } }
    private static Vector2 Dir(float ang) => new(MathF.Cos(ang), MathF.Sin(ang));
    private static readonly Vector4 Core = new(0.95f, 0.98f, 1f, 1f);

    // Geometry the shape resolves to.
    private struct Geo
    {
        public Vector2 Min, Max, Center;
        public float W, H, R;       // R = inscribed radius (circle) or half the shorter side
        public bool Circle;
    }

    // A spawn point on the shape plus the shape's outward normal there.
    private static void SpawnOn(in Geo g, FxShape shape, float u, float v, out Vector2 p, out Vector2 nrm)
    {
        switch (shape)
        {
            case FxShape.Ring when g.Circle:
                {
                    var ang = u * MathF.PI * 2f;
                    nrm = Dir(ang);
                    p = g.Center + nrm * g.R;
                    break;
                }
            case FxShape.Bottom:
                p = new Vector2(g.Min.X + u * g.W, g.Max.Y); nrm = new Vector2(0, 1); break;
            case FxShape.Top:
                p = new Vector2(g.Min.X + u * g.W, g.Min.Y); nrm = new Vector2(0, -1); break;
            case FxShape.Center:
                {
                    var ang = u * MathF.PI * 2f;
                    nrm = Dir(ang);
                    p = g.Center + nrm * (v * S(4f));
                    break;
                }
            default: // Edge
                {
                    if (g.Circle) { var ang = u * MathF.PI * 2f; nrm = Dir(ang); p = g.Center + nrm * g.R; break; }
                    var per = 2f * (g.W + g.H);
                    var d = u * per;
                    if (d < g.W) { p = new Vector2(g.Min.X + d, g.Min.Y); nrm = new Vector2(0, -1); }
                    else if (d < g.W + g.H) { p = new Vector2(g.Max.X, g.Min.Y + d - g.W); nrm = new Vector2(1, 0); }
                    else if (d < 2 * g.W + g.H) { p = new Vector2(g.Max.X - (d - g.W - g.H), g.Max.Y); nrm = new Vector2(0, 1); }
                    else { p = new Vector2(g.Min.X, g.Max.Y - (d - 2 * g.W - g.H)); nrm = new Vector2(-1, 0); }
                    break;
                }
        }
    }

    // Point at distance `d` along the perimeter of the rect grown by `grow` on every side, plus that edge's outward normal.
    private static void OnRectPerimeter(in Geo g, float grow, float d, out Vector2 p, out Vector2 nrm)
    {
        var min = g.Min - new Vector2(grow); var max = g.Max + new Vector2(grow);
        var w = max.X - min.X; var h = max.Y - min.Y;
        var per = 2f * (w + h);
        d = ((d % per) + per) % per;
        if (d < w) { p = new Vector2(min.X + d, min.Y); nrm = new Vector2(0, -1); }
        else if (d < w + h) { p = new Vector2(max.X, min.Y + d - w); nrm = new Vector2(1, 0); }
        else if (d < 2 * w + h) { p = new Vector2(max.X - (d - w - h), max.Y); nrm = new Vector2(0, 1); }
        else { p = new Vector2(min.X, max.Y - (d - 2 * w - h)); nrm = new Vector2(-1, 0); }
    }

    private static float RectPerimeter(in Geo g, float grow) => 2f * (g.W + g.H + 4f * grow);

    // Strokes the part of a grown rectangle's perimeter from d0 to d0+len.
    private static void StrokeRectRun(ImDrawListPtr dl, in Geo g, float grow, float d0, float len, uint col, float thick)
    {
        const int steps = 12;
        var prev = Vector2.Zero;
        for (int i = 0; i <= steps; i++)
        {
            OnRectPerimeter(g, grow, d0 + len * i / steps, out var p, out _);
            if (i > 0)
            {
                // Corners: if the run turned a corner, route through it.
                dl.AddLine(prev, p, col, thick);
            }
            prev = p;
        }
    }

    private static Vector2 Travel(FxDirection dir, Vector2 nrm)
    {
        return dir switch
        {
            FxDirection.Up      => new Vector2(0, -1),
            FxDirection.Down    => new Vector2(0, 1),
            FxDirection.Left    => new Vector2(-1, 0),
            FxDirection.Right   => new Vector2(1, 0),
            FxDirection.Outward => nrm,
            FxDirection.Inward  => -nrm,
            _                   => Vector2.Zero,
        };
    }

    // Base count for particle-like kinds: scales with the shape's length.
    private static int CountFor(in Geo g, FxShape shape, float amount, int per, int min, int cap)
    {
        float len = shape switch
        {
            FxShape.Ring   => MathF.PI * 2f * g.R,
            FxShape.Center => S(120f),
            FxShape.Edge   => g.Circle ? MathF.PI * 2f * g.R : 2f * (g.W + g.H),
            _              => g.W,
        };
        var n = (int)((min + len / S(per)) * amount);
        return Math.Clamp(n, 0, cap);
    }

    public static void Draw(ImDrawListPtr dl, FxDef fx, Vector2 min, Vector2 max, Vector4 accent, float alpha, string key, bool circle)
    {
        if (alpha <= 0.01f || fx.Strength <= 0.01f) return;
        var w = max.X - min.X; var h = max.Y - min.Y;
        if (w < 2f || h < 2f) return;
        var isCircle = fx.Outline switch { FxOutline.Circle => true, FxOutline.Square => false, _ => circle };
        var g = new Geo
        {
            Min = min, Max = max, W = w, H = h, Center = (min + max) * 0.5f,
            R = MathF.Min(w, h) * 0.5f, Circle = isCircle,
        };
        var color = fx.UseAccent ? accent : fx.Color.V;
        var a = alpha * fx.Strength;
        var seed = Seed(key + fx.Id);
        var t = ImmersiveMode.Time * MathF.Max(0.05f, fx.Speed);
        switch (fx.Kind)
        {
            case FxKind.Particles: DrawParticles(dl, fx, g, color, a, seed, t); break;
            case FxKind.Spinner:   DrawSpinner(dl, fx, g, color, a, t); break;
            case FxKind.Glow:      DrawGlow(dl, fx, g, color, a, t); break;
            case FxKind.Sweep:     DrawSweep(dl, fx, g, color, a, seed, t); break;
            case FxKind.Shimmer:   DrawShimmer(dl, fx, g, color, a, seed, t); break;
            case FxKind.Rays:      DrawRays(dl, fx, g, color, a, seed, t); break;
        }
    }

    // particles
    private static void DrawParticles(ImDrawListPtr dl, FxDef fx, in Geo g, Vector4 color, float a, float seed, float t)
    {
        var count = CountFor(g, fx.Shape, fx.Amount, 28, 6, 90);
        var reach = S(26f) * MathF.Max(0.05f, fx.Spread);
        var orbit = fx.Direction == FxDirection.Clockwise || fx.Direction == FxDirection.Counterclockwise;
        var still = fx.Direction == FxDirection.Still;
        var hot = Vector4.Lerp(color, Core, 0.55f);
        for (int i = 0; i < count; i++)
        {
            var r0 = Hash(seed + i);
            var r1 = Hash(seed + i * 2.3f + 5f);
            var r2 = Hash(seed + i * 4.1f + 9f);
            var r3 = Hash(seed + i * 1.7f + 21f);
            var life = (t * (0.18f + r1 * 0.22f) + r0) % 1f;
            SpawnOn(g, fx.Shape, r3, r2, out var p0, out var nrm);
            Vector2 p, motion;
            if (orbit)
            {
                var sign = fx.Direction == FxDirection.Clockwise ? 1f : -1f;
                var ang0 = MathF.Atan2(p0.Y - g.Center.Y, p0.X - g.Center.X);
                var rad = fx.Shape == FxShape.Center ? reach * (0.3f + r2 * 0.7f) : Vector2.Distance(p0, g.Center) + S(4f) + r2 * reach * 0.4f;
                var ang = ang0 + sign * (life * 1.6f * MathF.Max(0.05f, fx.Spread) + t * 0.1f);
                p = g.Center + Dir(ang) * rad;
                motion = Dir(ang + sign * MathF.PI * 0.5f);
            }
            else if (still)
            {
                p = p0 + new Vector2(MathF.Sin(t * 1.3f + i) * S(2f), MathF.Cos(t * 1.1f + i * 0.7f) * S(2f));
                motion = new Vector2(0, -1);
            }
            else
            {
                motion = Travel(fx.Direction, nrm);
                var sway = new Vector2(-motion.Y, motion.X) * MathF.Sin(t * 1.7f + i) * S(3f);
                p = p0 + motion * reach * (0.5f + r2 * 0.9f) * life + sway;
            }
            var fade = still ? 0.5f + 0.5f * MathF.Sin(life * MathF.PI * 2f) : MathF.Sin(life * MathF.PI);
            var flick = 0.7f + 0.3f * MathF.Sin(t * 6f + i);
            var pa = fade * flick * a * (0.5f + r1 * 0.5f);
            if (pa < 0.02f) continue;
            var size = S(1.2f + r1 * 1.8f) * MathF.Max(0.1f, fx.Size);
            DrawBit(dl, fx.Particle, p, motion, size, color, hot, pa, fx.Glow, seed + i, t);
        }
    }

    private static void DrawBit(ImDrawListPtr dl, FxParticle shape, Vector2 p, Vector2 motion, float size, Vector4 color, Vector4 hot, float a, bool glow, float seed, float t)
    {
        if (glow) dl.AddCircleFilled(p, size * 3.4f, Col(color, a * 0.16f), 10);
        switch (shape)
        {
            case FxParticle.Dot:
                dl.AddCircleFilled(p, size, Col(hot, a), 8);
                break;
            case FxParticle.Mote:
                dl.AddCircleFilled(p, size * 2f, Col(color, a * 0.35f), 10);
                dl.AddCircleFilled(p, size * 0.9f, Col(Core, a), 8);
                break;
            case FxParticle.Ring:
                dl.AddCircle(p, size * 1.8f, Col(color, a), 12, MathF.Max(1f, size * 0.5f));
                break;
            case FxParticle.Spark:
                {
                    if (motion.LengthSquared() < 0.01f) motion = new Vector2(0, -1);
                    var len = size * 4f;
                    dl.AddLine(p - motion * len, p + motion * len * 0.4f, Col(color, a * 0.6f), MathF.Max(1f, size * 0.7f));
                    dl.AddLine(p - motion * len * 0.4f, p + motion * len * 0.4f, Col(Core, a), MathF.Max(1f, size * 0.5f));
                    break;
                }
            case FxParticle.Shard:
                {
                    if (motion.LengthSquared() < 0.01f) motion = new Vector2(0, -1);
                    var side = new Vector2(-motion.Y, motion.X);
                    var len = size * 3f;
                    dl.AddTriangleFilled(p + motion * len, p - motion * len * 0.6f + side * size, p - motion * len * 0.6f - side * size, Col(color, a));
                    dl.AddTriangleFilled(p + motion * len * 0.5f, p - motion * len * 0.3f + side * size * 0.4f, p - motion * len * 0.3f - side * size * 0.4f, Col(hot, a));
                    break;
                }
            default: // Ember: an irregular glowing chip that tumbles
                {
                    var rot = t * (0.8f + Hash(seed) * 1.2f) + seed;
                    Span<Vector2> q = stackalloc Vector2[4];
                    for (int k = 0; k < 4; k++)
                    {
                        var ang = rot + k * MathF.PI * 0.5f;
                        var rr = size * (1.1f + Hash(seed + k * 7.3f) * 1.4f);
                        q[k] = p + Dir(ang) * rr;
                    }
                    dl.AddQuadFilled(q[0], q[1], q[2], q[3], Col(color, a * 0.9f));
                    dl.AddCircleFilled(p, size * 0.6f, Col(hot, a), 6);
                    break;
                }
        }
    }

    // spinner
    private static void DrawSpinner(ImDrawListPtr dl, FxDef fx, in Geo g, Vector4 color, float a, float t)
    {
        var rings = Math.Clamp(fx.Rings, 1, 6);
        var segs = Math.Clamp(fx.Segments, 1, 12);
        var cover = Math.Clamp(fx.Coverage, 0.05f, 1f);
        var gap = S(6f) * MathF.Max(0.2f, fx.Spread);
        var thick = MathF.Max(1f, S(1.5f) * fx.Size);
        var sign = fx.Direction == FxDirection.Counterclockwise ? -1f : 1f;
        var baseR = fx.Shape == FxShape.Center ? S(6f) : g.R + S(3f);
        if (!g.Circle && fx.Shape != FxShape.Center)
        {
            // Square outline: dashes chase around offset rectangles.
            var grow0 = S(3f);
            if (fx.Glow)
                for (int i = rings; i >= 1; i--)
                    dl.AddRectFilled(g.Min - new Vector2(grow0 + gap * i), g.Max + new Vector2(grow0 + gap * i), Col(color, 0.035f * a * fx.Amount), gap);
            for (int k = 0; k < rings; k++)
            {
                var grow = grow0 + gap * (k + 1);
                var per = RectPerimeter(g, grow);
                var run = per / segs;
                var rot = t * 0.6f * sign * (k % 2 == 0 ? 1f : -0.6f) * (1f + k * 0.15f);
                var offset = rot / (MathF.PI * 2f) * per;   // same angular speed as the circle
                var ka = (k == 0 ? 0.85f : 0.4f) * a;
                for (int i = 0; i < segs; i++)
                    StrokeRectRun(dl, g, grow, offset + i * run, run * cover, Col(color, ka), k == 0 ? thick : thick * 0.7f);
            }
            if (fx.Ticks)
            {
                var n = Math.Clamp((int)(24 * fx.Amount), 6, 72);
                var grow = grow0 + gap * (rings + 1) - gap * 0.3f;
                var per = RectPerimeter(g, grow);
                var offset = t * 0.09f * sign / (MathF.PI * 2f) * per;
                for (int i = 0; i < n; i++)
                {
                    OnRectPerimeter(g, grow, offset + per * i / n, out var p0, out var nrm);
                    var len = (i % 6 == 0) ? S(5f) : S(2f);
                    dl.AddLine(p0, p0 + nrm * len, Col(color, 0.35f * a), 1f);
                }
            }
            return;
        }
        if (fx.Glow)
            for (int i = rings; i >= 1; i--)
                dl.AddCircleFilled(g.Center, baseR + gap * i, Col(color, 0.035f * a * fx.Amount), 48);
        for (int k = 0; k < rings; k++)
        {
            var rad = baseR + gap * (k + 1);
            // Alternate rings counter-rotate and slow down, like the Allagan look.
            var rot = t * 0.6f * sign * (k % 2 == 0 ? 1f : -0.6f) * (1f + k * 0.15f);
            var ka = (k == 0 ? 0.85f : 0.4f) * a;
            var per = MathF.PI * 2f / segs;
            var arc = per * cover;
            for (int i = 0; i < segs; i++)
            {
                var a0 = rot + i * per;
                dl.PathArcTo(g.Center, rad, a0, a0 + arc, 24);
                dl.PathStroke(Col(color, ka), ImDrawFlags.None, k == 0 ? thick : thick * 0.7f);
            }
        }
        if (fx.Ticks)
        {
            var n = Math.Clamp((int)(24 * fx.Amount), 6, 72);
            var rr = baseR + gap * (rings + 1) - gap * 0.3f;
            var rot = t * 0.09f * sign;
            for (int i = 0; i < n; i++)
            {
                var ang = i * (MathF.PI * 2f / n) + rot;
                var len = (i % 6 == 0) ? S(5f) : S(2f);
                var d = Dir(ang);
                dl.AddLine(g.Center + d * rr, g.Center + d * (rr + len), Col(color, 0.35f * a), 1f);
            }
        }
    }

    // glow
    private static void DrawGlow(ImDrawListPtr dl, FxDef fx, in Geo g, Vector4 color, float a, float t)
    {
        var layers = Math.Clamp((int)(8 * fx.Amount), 2, 14);
        var spread = S(24f) * MathF.Max(0.1f, fx.Spread);
        var pulse = 0.8f + 0.2f * MathF.Sin(t * 1.4f);
        if (((fx.Shape == FxShape.Ring || fx.Shape == FxShape.Edge) && g.Circle) || fx.Shape == FxShape.Center)
        {
            var baseR = fx.Shape == FxShape.Center ? 0f : g.R;
            for (int i = layers; i >= 1; i--)
            {
                var wob = 0.75f + 0.25f * MathF.Sin(t * (1.1f + i * 0.17f) + i * 1.9f);
                dl.AddCircleFilled(g.Center, baseR + spread * i / layers * wob, Col(color, 0.045f * wob * pulse * a * fx.Size), 56);
            }
        }
        else
        {
            var min = g.Min; var max = g.Max;
            if (fx.Shape == FxShape.Bottom) min.Y = max.Y - S(4f);
            if (fx.Shape == FxShape.Top) max.Y = min.Y + S(4f);
            for (int i = layers; i >= 1; i--)
            {
                var s = spread * i / layers;
                var la = 0.5f * (1f - (i - 1) / (float)layers) * 0.5f * pulse * a * fx.Size;
                dl.AddRectFilled(min - new Vector2(s), max + new Vector2(s), Col(color, la * 0.5f), s);
            }
        }
    }

    // sweep
    private static void DrawSweep(ImDrawListPtr dl, FxDef fx, in Geo g, Vector4 color, float a, float seed, float t)
    {
        var period = 3.6f;
        var band = S(28f) * MathF.Max(0.1f, fx.Size);
        var count = Math.Clamp((int)MathF.Round(fx.Amount), 1, 4);
        var vertical = fx.Direction == FxDirection.Up || fx.Direction == FxDirection.Down || fx.Direction == FxDirection.Still
                    || fx.Direction == FxDirection.Outward || fx.Direction == FxDirection.Inward
                    || fx.Direction == FxDirection.Clockwise || fx.Direction == FxDirection.Counterclockwise;
        var reverse = fx.Direction == FxDirection.Up || fx.Direction == FxDirection.Left;
        var strength = 0.12f * a * MathF.Max(0.1f, fx.Strength);
        dl.PushClipRect(g.Min, g.Max, true);
        for (int k = 0; k < count; k++)
        {
            var phase = ((t + seed * 0.1f + k * period / count) % period) / period;
            if (reverse) phase = 1f - phase;
            if (vertical)
            {
                var y0 = g.Min.Y - band + phase * (g.H + band * 2f);
                var mid = y0 + band * 0.5f;
                dl.AddRectFilledMultiColor(new Vector2(g.Min.X, y0), new Vector2(g.Max.X, mid), Col(color, 0f), Col(color, 0f), Col(color, strength), Col(color, strength));
                dl.AddRectFilledMultiColor(new Vector2(g.Min.X, mid), new Vector2(g.Max.X, y0 + band), Col(color, strength), Col(color, strength), Col(color, 0f), Col(color, 0f));
            }
            else
            {
                var x0 = g.Min.X - band + phase * (g.W + band * 2f);
                var mid = x0 + band * 0.5f;
                dl.AddRectFilledMultiColor(new Vector2(x0, g.Min.Y), new Vector2(mid, g.Max.Y), Col(color, 0f), Col(color, strength), Col(color, strength), Col(color, 0f));
                dl.AddRectFilledMultiColor(new Vector2(mid, g.Min.Y), new Vector2(x0 + band, g.Max.Y), Col(color, strength), Col(color, 0f), Col(color, 0f), Col(color, strength));
            }
        }
        dl.PopClipRect();
    }

    // shimmer
    private static void DrawShimmer(ImDrawListPtr dl, FxDef fx, in Geo g, Vector4 color, float a, float seed, float t)
    {
        var count = Math.Clamp((int)MathF.Round(2 * fx.Amount), 1, 6);
        var sign = fx.Direction == FxDirection.Counterclockwise || fx.Direction == FxDirection.Left || fx.Direction == FxDirection.Up ? -1f : 1f;
        var size = MathF.Max(1f, S(3f) * fx.Size);
        if ((fx.Shape == FxShape.Ring || fx.Shape == FxShape.Edge) && g.Circle)
        {
            var rad = g.R + S(2f);
            for (int k = 0; k < count; k++)
            {
                var aa = t * 0.7f * sign + k * (MathF.PI * 2f / count) + seed;
                var sw = 0.5f + 0.5f * MathF.Sin(t * 1.6f + k);
                var arc = 0.9f * MathF.Max(0.2f, fx.Spread);
                if (fx.Glow) { dl.PathArcTo(g.Center, rad, aa, aa + arc, 20); dl.PathStroke(Col(color, 0.25f * sw * a), ImDrawFlags.None, size * 2.5f); }
                dl.PathArcTo(g.Center, rad, aa, aa + arc, 20);
                dl.PathStroke(Col(Core, 0.45f * sw * a), ImDrawFlags.None, size);
            }
            return;
        }
        var per = 2f * (g.W + g.H);
        for (int k = 0; k < count; k++)
        {
            var d = ((t * 60f * sign + k * per / count + seed * 10f) % per + per) % per;
            SpawnOn(g, FxShape.Edge, d / per, 0f, out var p, out _);
            if (fx.Glow) dl.AddCircleFilled(p, size * 2.4f * MathF.Max(0.3f, fx.Spread), Col(color, 0.18f * a), 12);
            dl.AddCircleFilled(p, size * 0.85f, Col(Core, 0.9f * a), 10);
        }
    }

    // rays
    private static void DrawRays(ImDrawListPtr dl, FxDef fx, in Geo g, Vector4 color, float a, float seed, float t)
    {
        var n = Math.Clamp((int)(14 * fx.Amount), 3, 48);
        var len = S(30f) * MathF.Max(0.1f, fx.Spread);
        var thick = MathF.Max(1f, S(1.2f) * fx.Size);
        var sign = fx.Direction == FxDirection.Counterclockwise ? -1f : fx.Direction == FxDirection.Clockwise ? 1f : 0f;
        var inward = fx.Direction == FxDirection.Inward;
        var baseR = fx.Shape == FxShape.Center ? 0f : g.R + S(2f);
        var rot = t * 0.15f * sign;
        for (int i = 0; i < n; i++)
        {
            var r0 = Hash(seed + i * 3.1f);
            var ang = i * (MathF.PI * 2f / n) + rot + r0 * 0.15f;
            var flick = 0.35f + 0.65f * MathF.Max(0f, MathF.Sin(t * (1.5f + r0 * 2f) + r0 * 20f));
            var l = len * (0.4f + r0 * 0.8f) * flick;
            var d = Dir(ang);
            Vector2 p0, p1;
            if (fx.Shape == FxShape.Bottom || fx.Shape == FxShape.Top)
            {
                var up = fx.Shape == FxShape.Top ? -1f : 1f;
                var x = g.Min.X + (i + 0.5f) / n * g.W;
                p0 = new Vector2(x, fx.Shape == FxShape.Top ? g.Min.Y : g.Max.Y);
                p1 = p0 + new Vector2(0, (inward ? -up : up) * l);
            }
            else if (!g.Circle && fx.Shape != FxShape.Center)
            {
                // Square outline: rays leave the rectangle's edges straight out.
                var per = RectPerimeter(g, S(2f));
                OnRectPerimeter(g, S(2f), per * (i + 0.5f) / n + rot / (MathF.PI * 2f) * per, out p0, out var nrm);
                p1 = inward ? p0 - nrm * l : p0 + nrm * l;
            }
            else
            {
                p0 = g.Center + d * baseR;
                p1 = inward ? g.Center + d * MathF.Max(0f, baseR - l) : p0 + d * l;
            }
            if (fx.Glow) dl.AddLine(p0, p1, Col(color, 0.18f * flick * a), thick * 3f);
            dl.AddLine(p0, p1, Col(Vector4.Lerp(color, Core, 0.4f), 0.55f * flick * a), thick);
        }
    }
}
