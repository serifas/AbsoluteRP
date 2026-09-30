using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

namespace Rolspeace.VFX
{
    // Burn / dissolve effects. The fractal-noise front is ported from the original AbsoluteRoleplay WindowShimmer close-burn: a cached value noise grid over the rect, a threshold that sweeps through it, and a glowing, flickering band of cells right at the threshold edge. Color is caller supplied so the same effect reads as fire (orange) or void corruption (purple). Everything is deterministic from `key` + time so panels don't shuffle between frames.
    public static class BurnFx
    {
        private static readonly Stopwatch clock = Stopwatch.StartNew();
        private static float Now => (float)clock.Elapsed.TotalSeconds;

        private const float NoiseScale = 0.060f;
        private const int Octaves = 4;
        private const float StridePx = 8.5f;
        private const float CircleRadius = 6.5f;
        public const float EdgeBand = 0.085f;

        // noise grid cache
        private sealed class GridEntry
        {
            public float[,] Grid = new float[1, 1];
            public int Cx, Cy;
            public float Sx, Sy;
        }
        private static readonly Dictionary<string, GridEntry> cache = new();

        private static int SeedOf(string key)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (var c in key) { h ^= c; h *= 16777619; }
                return (int)h;
            }
        }

        private static GridEntry Grid(string key, Vector2 size)
        {
            int cx = Math.Max(2, (int)MathF.Ceiling(size.X / StridePx) + 1);
            int cy = Math.Max(2, (int)MathF.Ceiling(size.Y / StridePx) + 1);
            if (cache.TryGetValue(key, out var e) && e.Cx == cx && e.Cy == cy) return e;
            int seed = SeedOf(key);
            var g = new float[cy, cx];
            for (int j = 0; j < cy; j++)
                for (int i = 0; i < cx; i++)
                    g[j, i] = FractalNoise(i * NoiseScale, j * NoiseScale, seed, Octaves);
            e = new GridEntry { Grid = g, Cx = cx, Cy = cy, Sx = size.X / (cx - 1), Sy = size.Y / (cy - 1) };
            cache[key] = e;
            return e;
        }

        // progress 1 = fully intact, 0 = fully consumed.
        public static float ThresholdFor(float progress)
            => 1.05f - 1.22f * (1f - Math.Clamp(progress, 0f, 1f));

        // Paints the surface as noise-jittered discs for every cell that is still intact (n <= threshold). Used instead of a solid fill while a panel materialises / dissolves.
        public static void DrawDissolveFill(ImDrawListPtr dl, Vector2 min, Vector2 max, string key, float threshold, uint fillCol)
        {
            var size = max - min;
            if (size.X < 10f || size.Y < 10f) return;
            var e = Grid(key, size);
            int seed = SeedOf(key);
            dl.PushClipRect(min, max, true);
            for (int j = 0; j < e.Cy; j++)
            {
                for (int i = 0; i < e.Cx; i++)
                {
                    if (e.Grid[j, i] > threshold) continue;
                    float jx = (Hash01(i, j, seed + 11) - 0.5f) * 3f;
                    float jy = (Hash01(i, j, seed + 13) - 0.5f) * 3f;
                    dl.AddCircleFilled(new Vector2(min.X + i * e.Sx + jx, min.Y + j * e.Sy + jy), CircleRadius * 1.55f, fillCol, 12);
                }
            }
            dl.PopClipRect();
        }

        // The glowing front: cells just past the threshold, hottest at the edge and cooling over `band`.
        public static void DrawBurnFront(ImDrawListPtr dl, Vector2 min, Vector2 max, string key, float threshold, Vector4 tint, float intensity, float band = EdgeBand)
        {
            var size = max - min;
            if (size.X < 10f || size.Y < 10f || intensity <= 0.01f) return;
            var e = Grid(key, size);
            int seed = SeedOf(key);
            float time = Now;
            var glowC = Lerp(tint, new Vector4(0f, 0f, 0f, 1f), 0.15f);
            var midC  = Lerp(tint, Vector4.One, 0.22f);
            var hotC  = Lerp(tint, Vector4.One, 0.62f);
            var topC  = Lerp(tint, Vector4.One, 0.86f);

            dl.PushClipRect(min - new Vector2(CircleRadius * 3f), max + new Vector2(CircleRadius * 3f), true);
            for (int j = 0; j < e.Cy; j++)
            {
                for (int i = 0; i < e.Cx; i++)
                {
                    float n = e.Grid[j, i];
                    if (n <= threshold) continue;
                    float age = n - threshold;
                    if (age >= band) continue;

                    float jx = (Hash01(i, j, seed + 11) - 0.5f) * 3f;
                    float jy = (Hash01(i, j, seed + 13) - 0.5f) * 3f;
                    var p = new Vector2(min.X + i * e.Sx + jx, min.Y + j * e.Sy + jy);

                    float et = age / band;
                    float f1 = 0.60f + 0.40f * MathF.Sin(time * 14f + i * 1.73f + j * 2.31f);
                    float f2 = 0.70f + 0.30f * MathF.Sin(time * 22f + i * 3.11f + j * 1.47f);

                    dl.AddCircleFilled(p, CircleRadius * 2.6f,  Col(glowC, 0.60f * (1f - et * 0.6f) * f1 * intensity), 14);
                    dl.AddCircleFilled(p, CircleRadius * 1.55f, Col(midC,  0.90f * (1f - et * 0.5f) * f2 * intensity), 12);
                    if (et < 0.45f)
                        dl.AddCircleFilled(p, CircleRadius * 0.85f, Col(hotC, 0.95f * (1f - et / 0.45f) * f1 * intensity), 10);
                    if (et < 0.18f)
                        dl.AddCircleFilled(p, CircleRadius * 0.40f, Col(topC, 0.95f * intensity), 8);
                }
            }
            dl.PopClipRect();
        }

        // A burn front that never consumes anything: the threshold drifts slowly so a glowing crack wanders across the rect.
        public static void DrawSmoulder(ImDrawListPtr dl, Vector2 min, Vector2 max, string key, Vector4 tint, float intensity)
        {
            float seed = (SeedOf(key) & 0xFFFF) * 0.001f;
            float thr = 0.34f + 0.26f * (0.5f + 0.5f * MathF.Sin(Now * 0.22f + seed));
            DrawBurnFront(dl, min, max, key, thr, tint, intensity, EdgeBand * 0.55f);
        }

        // Charred rim on all four edges with noise-driven depth.
        public static void DrawEdgeBurn(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 color, string key, float intensity, float depth = 22f)
        {
            var w = max.X - min.X;
            var h = max.Y - min.Y;
            if (w < 8f || h < 8f) return;
            var t = Now;
            var seed = (SeedOf(key) & 0xFFFF) * 0.01f;
            var dark = new Vector4(0.02f, 0f, 0.03f, 1f);
            var none = Col(color, 0f);

            dl.PushClipRect(min, max, true);
            int segs = Math.Max(8, (int)(w / 18f));
            for (int i = 0; i < segs; i++)
            {
                var x0 = min.X + w * i / segs;
                var x1 = min.X + w * (i + 1) / segs;
                var n = Noise(i * 0.7f, t, seed);
                var d = depth * (0.45f + 0.55f * (n * 0.5f + 0.5f));
                var a = intensity * (0.28f + 0.22f * (n * 0.5f + 0.5f));
                dl.AddRectFilledMultiColor(new Vector2(x0, max.Y - d), new Vector2(x1, max.Y), none, none, Col(color, a), Col(color, a));
                dl.AddRectFilledMultiColor(new Vector2(x0, max.Y - d * 0.35f), new Vector2(x1, max.Y), Col(dark, 0f), Col(dark, 0f), Col(dark, 0.55f * intensity), Col(dark, 0.55f * intensity));
                var n2 = Noise(i * 0.7f + 40f, t * 0.8f, seed);
                var d2 = depth * 0.5f * (0.4f + 0.6f * (n2 * 0.5f + 0.5f));
                dl.AddRectFilledMultiColor(new Vector2(x0, min.Y), new Vector2(x1, min.Y + d2), Col(color, a * 0.5f), Col(color, a * 0.5f), none, none);
            }
            int vsegs = Math.Max(6, (int)(h / 18f));
            for (int i = 0; i < vsegs; i++)
            {
                var y0 = min.Y + h * i / vsegs;
                var y1 = min.Y + h * (i + 1) / vsegs;
                var n = Noise(i * 0.7f + 80f, t * 0.9f, seed);
                var d = depth * 0.6f * (0.4f + 0.6f * (n * 0.5f + 0.5f));
                var a = intensity * (0.18f + 0.18f * (n * 0.5f + 0.5f));
                dl.AddRectFilledMultiColor(new Vector2(min.X, y0), new Vector2(min.X + d, y1), Col(color, a), none, none, Col(color, a));
                dl.AddRectFilledMultiColor(new Vector2(max.X - d, y0), new Vector2(max.X, y1), none, Col(color, a), Col(color, a), none);
            }
            dl.PopClipRect();
        }

        // Embers lifting from the bottom edge and drifting upward.
        public static void DrawEmbers(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 color, string key, float intensity, int count = 26)
        {
            var w = max.X - min.X;
            var h = max.Y - min.Y;
            if (w < 8f || h < 8f || intensity <= 0.01f) return;
            var t = Now;
            var seed = (SeedOf(key) & 0xFFFF) * 0.01f;
            var core = Lerp(color, Vector4.One, 0.5f);
            for (int i = 0; i < count; i++)
            {
                var r0 = Hash(i + seed);
                var r1 = Hash(i * 3.1f + seed + 7f);
                var r2 = Hash(i * 5.7f + seed + 13f);
                var speed = 0.10f + r1 * 0.14f;
                var life = (t * speed + r0) % 1f;
                var rise = h * (0.35f + r2 * 0.55f);
                var x = min.X + w * r0 + MathF.Sin(t * (0.8f + r2) + i) * (6f + r1 * 10f) * life;
                var y = max.Y - Scale(4f) - life * rise;
                var fade = MathF.Sin(life * MathF.PI);
                var a = fade * intensity * (0.5f + r1 * 0.5f);
                var size = Scale(1.2f + r2 * 2.2f) * (1f - life * 0.5f);
                if (a <= 0.01f) continue;
                dl.AddCircleFilled(new Vector2(x, y), size * 3.2f, Col(color, a * 0.18f), 10);
                dl.AddCircleFilled(new Vector2(x, y), size, Col(core, a), 8);
            }
        }

        // Full-height rising embers with mixed shapes - round motes, diamonds, small shards and short streaks - each with its own speed, wobble and flicker. Intended for a screen-sized rect.
        public static void DrawRisingEmbers(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 color, string key, float intensity, int count = 70)
        {
            var w = max.X - min.X;
            var h = max.Y - min.Y;
            if (w < 8f || h < 8f || intensity <= 0.01f) return;
            var t = Now;
            var seed = (SeedOf(key) & 0xFFFF) * 0.01f;
            var core = Lerp(color, Vector4.One, 0.55f);
            var deep = Lerp(color, new Vector4(0.2f, 0f, 0.3f, 1f), 0.4f);

            for (int i = 0; i < count; i++)
            {
                var r0 = Hash(i + seed);
                var r1 = Hash(i * 3.1f + seed + 7f);
                var r2 = Hash(i * 5.7f + seed + 13f);
                var r3 = Hash(i * 2.3f + seed + 29f);
                var speed = 0.035f + r1 * 0.06f;               // full rises per second
                var life = (t * speed + r0) % 1f;
                var rise = h * (0.55f + r2 * 0.5f);
                var wob = MathF.Sin(t * (0.6f + r2 * 0.8f) + i * 1.7f) * (8f + r1 * 22f);
                var x = min.X + w * r0 + wob;
                var y = max.Y + Scale(10f) - life * rise;
                if (y < min.Y - Scale(20f)) continue;
                var fade = MathF.Sin(MathF.Min(1f, life * 1.15f) * MathF.PI);
                var flick = 0.7f + 0.3f * MathF.Sin(t * (9f + r3 * 8f) + i);
                var a = fade * flick * intensity * (0.45f + r1 * 0.55f);
                if (a <= 0.01f) continue;
                var size = Scale(1.5f + r2 * 3.2f) * (1f - life * 0.35f);
                var p = new Vector2(x, y);
                var shape = i % 4;
                // Soft halo for every shape.
                dl.AddCircleFilled(p, size * 3.4f, Col(color, a * 0.16f), 10);
                switch (shape)
                {
                    case 0: // mote
                        dl.AddCircleFilled(p, size, Col(core, a), 8);
                        break;
                    case 1: // diamond
                        dl.AddQuadFilled(p + new Vector2(0, -size * 1.4f), p + new Vector2(size, 0), p + new Vector2(0, size * 1.4f), p + new Vector2(-size, 0), Col(core, a));
                        break;
                    case 2: // shard - thin rotating triangle
                        {
                            var ang = t * (0.8f + r3) + i;
                            var d = new Vector2(MathF.Cos(ang), MathF.Sin(ang));
                            var n = new Vector2(-d.Y, d.X);
                            dl.AddTriangleFilled(p + d * size * 2.2f, p - d * size * 1.2f + n * size * 0.8f, p - d * size * 1.2f - n * size * 0.8f, Col(deep, a));
                            dl.AddTriangleFilled(p + d * size * 1.4f, p - d * size * 0.6f + n * size * 0.45f, p - d * size * 0.6f - n * size * 0.45f, Col(core, a));
                        }
                        break;
                    default: // streak - short vertical trail
                        dl.AddLine(p + new Vector2(0, size * 3f), p - new Vector2(0, size * 1.5f), Col(color, a * 0.6f), size * 0.9f);
                        dl.AddCircleFilled(p - new Vector2(0, size * 1.5f), size * 0.8f, Col(core, a), 8);
                        break;
                }
            }
        }

        public static void Forget(string key) => cache.Remove(key);

        // math
        private static uint Col(Vector4 c, float a)
            => ImGui.ColorConvertFloat4ToU32(new Vector4(c.X, c.Y, c.Z, Math.Clamp(a, 0f, 1f)));

        private static Vector4 Lerp(Vector4 a, Vector4 b, float t)
            => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, 1f);

        private static float Scale(float v)
        {
            try { return v * ImGui.GetIO().FontGlobalScale; } catch { return v; }
        }

        private static float Hash(float n)
        {
            var s = MathF.Sin(n * 127.1f + 311.7f) * 43758.5453f;
            return s - MathF.Floor(s);
        }

        private static float Noise(float x, float t, float seed)
            => MathF.Sin(x * 1.7f + seed + t * 0.9f) * 0.5f
             + MathF.Sin(x * 3.9f + seed * 1.3f - t * 1.4f) * 0.3f
             + MathF.Sin(x * 8.3f + seed * 2.1f + t * 2.2f) * 0.2f;

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint n = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 1274126177u;
                n = (n ^ (n >> 13)) * 1274126177u;
                n = n ^ (n >> 16);
                return (n & 0x00FFFFFFu) / (float)0x00FFFFFF;
            }
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            int xi = (int)MathF.Floor(x);
            int yi = (int)MathF.Floor(y);
            float xf = x - xi, yf = y - yi;
            float tx = xf * xf * (3f - 2f * xf);
            float ty = yf * yf * (3f - 2f * yf);
            float v00 = Hash01(xi, yi, seed), v10 = Hash01(xi + 1, yi, seed);
            float v01 = Hash01(xi, yi + 1, seed), v11 = Hash01(xi + 1, yi + 1, seed);
            float vx0 = v00 + (v10 - v00) * tx;
            float vx1 = v01 + (v11 - v01) * tx;
            return vx0 + (vx1 - vx0) * ty;
        }

        private static float FractalNoise(float x, float y, int seed, int octaves)
        {
            float total = 0f, amp = 1f, freq = 1f, maxAmp = 0f;
            for (int i = 0; i < octaves; i++)
            {
                total += ValueNoise(x * freq, y * freq, seed + i * 73) * amp;
                maxAmp += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return total / maxAmp;
        }
    }
}
