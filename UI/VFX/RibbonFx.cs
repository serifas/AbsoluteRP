using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

namespace Rolspeace.VFX
{
    
    public static class RibbonFx
    {
        private static readonly Stopwatch clock = Stopwatch.StartNew();
        private static readonly Dictionary<string, float> keyOffsets = new();

        private static double Now => clock.Elapsed.TotalSeconds;

        private static float OffsetFor(string key)
        {
            if (keyOffsets.TryGetValue(key, out var o)) return o;
            int h = key.GetHashCode();
            float off = (float)(((uint)h % 10000) / 10000.0 * MathF.PI * 2f);
            keyOffsets[key] = off;
            return off;
        }

        private static float Noise2(float x, float T, float seed)
        {
            return
                  MathF.Sin(x * 1.000f + seed * 1.70f) * MathF.Cos(T * 1.05f + seed * 0.50f) * 0.40f
                + MathF.Cos(x * 2.310f + seed * 3.11f) * MathF.Sin(T * 1.45f + seed * 1.10f) * 0.30f
                + MathF.Sin(x * 4.770f + seed * 5.93f) * MathF.Cos(T * 1.95f + seed * 1.70f) * 0.18f
                + MathF.Cos(x * 9.130f + seed * 11.3f) * MathF.Sin(T * 2.50f + seed * 2.30f) * 0.10f
                // Right-to-left scroll: (x + T*v) makes the wave travel in -x direction.
                + MathF.Sin((x + T * 0.30f) * 1.300f + seed * 4.70f) * 0.14f;
        }

        public static void DrawRibbonsAround(Vector2 min, Vector2 max, Vector4 color, string key, float intensity)
        {
            float w = max.X - min.X;
            float h = max.Y - min.Y;
            if (w < 10f || h < 6f) return;

            ImDrawListPtr dl;
            try { dl = ImGui.GetForegroundDrawList(ImGui.GetMainViewport()); }
            catch { return; }

            float now = (float)Now;
            float baseOffset = OffsetFor(key);

            int R = intensity > 0.8f ? 3 : 2;
            const int N = 48;

            // Ribbon area is now constrained inside the button.
            float xMargin = 0f;
            float yMargin = 0f;
            float yTop = min.Y - yMargin;
            float yRange = (max.Y + yMargin) - yTop;
            float startX = min.X - xMargin;
            float spanX = w + xMargin * 2f;

            // Per-ribbon parameters
            Span<float> baseYs = stackalloc float[R];
            Span<float> amps = stackalloc float[R];
            Span<float> times = stackalloc float[R];
            Span<float> seeds = stackalloc float[R];
            for (int r = 0; r < R; r++)
            {
                baseYs[r] = yTop + yRange * (0.38f + r * (0.24f / MathF.Max(1, R - 1)));
                // Relative to button height so swings stay within rect.
                amps[r]   = MathF.Max(6f, yRange * 0.30f) + r * 2f;
                // Slower per-ribbon time rates.
                times[r]  = now * (0.70f + r * 0.07f);
                seeds[r]  = baseOffset + r * 7.31f;
            }

            // Sample ribbon points
            Span<Vector2> pts = stackalloc Vector2[R * N];
            for (int r = 0; r < R; r++)
            {
                float T = times[r];
                float sd = seeds[r];
                for (int i = 0; i < N; i++)
                {
                    float t = i / (float)(N - 1);
                    float xSpace = t * 3.2f;
                    float yDisp = Noise2(xSpace, T, sd) * amps[r];
                    float ampMod = 1f + Noise2(xSpace * 0.5f, T * 0.7f, sd + 23.7f) * 0.70f;
                    float xWob = Noise2(xSpace * 0.6f, T * 0.5f, sd + 91.5f) * 3.5f;
                    float x = startX + spanX * t + xWob;
                    float y = baseYs[r] + yDisp * ampMod;
                    pts[r * N + i] = new Vector2(x, y);
                }
            }

            // clip every ribbon-related draw to the button
            dl.PushClipRect(min, max, true);

            // Fill between ribbon pairs
            for (int r = 0; r < R - 1; r++)
            {
                for (int i = 0; i < N - 1; i++)
                {
                    var a = pts[r * N + i];
                    var b = pts[r * N + i + 1];
                    var c = pts[(r + 1) * N + i + 1];
                    var d = pts[(r + 1) * N + i];

                    float distAvg = (MathF.Abs(a.Y - d.Y) + MathF.Abs(b.Y - c.Y)) * 0.5f;
                    float closeness = MathF.Exp(-distAvg / MathF.Max(6f, h * 0.20f));

                    float tt = (i + 0.5f) / (N - 1);
                    float taper = MathF.Sin(tt * MathF.PI);

                    float fillAlpha = (0.04f + closeness * 0.42f) * taper * intensity * color.W;
                    if (fillAlpha < 0.004f) continue;
                    if (fillAlpha > 0.55f) fillAlpha = 0.55f;

                    float lift = closeness * 0.45f;
                    var fillColor = new Vector4(
                        MathF.Min(1f, color.X + lift),
                        MathF.Min(1f, color.Y + lift),
                        MathF.Min(1f, color.Z + lift),
                        fillAlpha);
                    uint c32 = ImGui.ColorConvertFloat4ToU32(fillColor);
                    dl.AddTriangleFilled(a, b, c, c32);
                    dl.AddTriangleFilled(a, c, d, c32);
                }
            }

            // Ribbon glow lines on top of the gradient
            for (int r = 0; r < R; r++)
            {
                DrawRibbonGlow(dl, pts.Slice(r * N, N), color, intensity);
            }

            dl.PopClipRect();
        }

        public static void DrawBackgroundRibbonsDiagonal(Vector2 min, Vector2 max, Vector4 color, string key, float intensity = 1f)
        {
            ImDrawListPtr dl;
            try { dl = ImGui.GetWindowDrawList(); }
            catch { return; }
            DrawBackgroundRibbonsDiagonal(dl, min, max, color, key, intensity);
        }

        // Same effect on a caller-supplied draw list (background / foreground lists, or a window other than the current one).
        public static void DrawBackgroundRibbonsDiagonal(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 color, string key, float intensity = 1f)
        {
            float w = max.X - min.X;
            float h = max.Y - min.Y;
            if (w < 40f || h < 40f) return;

            var center = (min + max) * 0.5f;
            float L = MathF.Sqrt(w * w + h * h);  // window diagonal length

            const float cosA = -0.7071f;
            const float sinA =  0.7071f;

            float now = (float)Now;
            float baseOffset = OffsetFor(key);

            // Ribbon count scales with the diagonal so big windows get more currents.
            int R = (int)MathF.Min(7f, MathF.Max(3f, L / 110f));
            const int N = 80;

            // Per-ribbon parameters in rotated space.
            Span<float> baseVs = stackalloc float[R];
            Span<float> amps   = stackalloc float[R];
            Span<float> times  = stackalloc float[R];
            Span<float> seeds  = stackalloc float[R];
            for (int r = 0; r < R; r++)
            {
                float frac = R == 1 ? 0.5f : r / (float)(R - 1);
                baseVs[r] = (frac - 0.5f) * L * 0.85f;
                amps[r]   = MathF.Max(40f, L * 0.10f) + r * 4f;
                times[r]  = now * (0.55f + r * 0.06f);
                seeds[r]  = baseOffset + r * 11.17f;
            }

            // Sample each ribbon along the flow axis
            Span<Vector2> pts = stackalloc Vector2[R * N];
            for (int r = 0; r < R; r++)
            {
                float T  = times[r];
                float sd = seeds[r];
                for (int i = 0; i < N; i++)
                {
                    float t = i / (float)(N - 1);
                    float extent = L * 0.7f;
                    float u = -extent + t * extent * 2f;
                    float uSpace = t * 5.5f;

                    float vDisp  = Noise2(uSpace, T, sd) * amps[r];
                    float ampMod = 1f + Noise2(uSpace * 0.5f, T * 0.7f, sd + 23.7f) * 0.70f;
                    float uWob   = Noise2(uSpace * 0.6f, T * 0.5f, sd + 91.5f) * 6f;

                    float vFinal = baseVs[r] + vDisp * ampMod;
                    float uFinal = u + uWob;

                    float x = center.X + cosA * uFinal - sinA * vFinal;
                    float y = center.Y + sinA * uFinal + cosA * vFinal;
                    pts[r * N + i] = new Vector2(x, y);
                }
            }

            // Clip to the window.
            dl.PushClipRect(min, max, true);

            // Gradient fill between adjacent ribbon pairs.
            for (int r = 0; r < R - 1; r++)
            {
                for (int i = 0; i < N - 1; i++)
                {
                    var a = pts[r * N + i];
                    var b = pts[r * N + i + 1];
                    var c = pts[(r + 1) * N + i + 1];
                    var d = pts[(r + 1) * N + i];

                    float dist = (Vector2.Distance(a, d) + Vector2.Distance(b, c)) * 0.5f;
                    float closeness = MathF.Exp(-dist / MathF.Max(30f, L * 0.08f));

                    float tt = (i + 0.5f) / (N - 1);
                    float taper = MathF.Sin(tt * MathF.PI);

                    float fillAlpha = (0.03f + closeness * 0.32f) * taper * intensity * color.W;
                    if (fillAlpha < 0.003f) continue;
                    if (fillAlpha > 0.42f) fillAlpha = 0.42f;

                    float lift = closeness * 0.42f;
                    var fillColor = new Vector4(
                        MathF.Min(1f, color.X + lift),
                        MathF.Min(1f, color.Y + lift),
                        MathF.Min(1f, color.Z + lift),
                        fillAlpha);
                    uint c32 = ImGui.ColorConvertFloat4ToU32(fillColor);
                    dl.AddTriangleFilled(a, b, c, c32);
                    dl.AddTriangleFilled(a, c, d, c32);
                }
            }

            for (int r = 0; r < R; r++)
            {
                DrawRibbonGlowBackground(dl, pts.Slice(r * N, N), color, intensity);
            }

            dl.PopClipRect();
        }

        private static void DrawRibbonGlowBackground(ImDrawListPtr dl, Span<Vector2> pts, Vector4 color, float intensity)
        {
            // BG glow slightly dimmed since content draws over it
            var halo = ColorWithAlpha(color, color.W * 0.14f * intensity);
            var mid  = ColorWithAlpha(color, color.W * 0.30f * intensity);
            var core = ColorBoosted(color, 0.18f, MathF.Min(0.75f, color.W * 0.62f * intensity));
            DrawTaperedPolyline(dl, pts, halo, 9.0f);
            DrawTaperedPolyline(dl, pts, mid,  4.0f);
            DrawTaperedPolyline(dl, pts, core, 1.4f);
        }

        public static void DrawButtonTextWithSoftGlow(Vector2 min, Vector2 max, string label, Vector4 textColor)
        {
            if (string.IsNullOrEmpty(label)) return;

            ImDrawListPtr dl;
            try { dl = ImGui.GetForegroundDrawList(ImGui.GetMainViewport()); }
            catch { return; }

            string visible = label;
            int hashIdx = visible.IndexOf("##", StringComparison.Ordinal);
            if (hashIdx >= 0) visible = visible.Substring(0, hashIdx);
            if (visible.Length == 0) return;

            var textSize = ImGui.CalcTextSize(visible);
            float w = max.X - min.X;
            float h = max.Y - min.Y;
            var textPos = new Vector2(
                min.X + (w - textSize.X) * 0.5f,
                min.Y + (h - textSize.Y) * 0.5f);


            const int cushionLayers = 10;
            for (int i = 0; i < cushionLayers; i++)
            {
                float expand = i * 5f;
                var bpMin = min - new Vector2(expand, expand);
                var bpMax = max + new Vector2(expand, expand);

                float a = 0.22f * MathF.Exp(-i * 0.40f);
                var bg = new Vector4(0f, 0f, 0f, a);
                dl.AddRectFilled(bpMin, bpMax, ImGui.ColorConvertFloat4ToU32(bg), 10f);
            }

            // Eight cardinal/intercardinal direcions per shadow ring.
            const float k = 0.7071f;
            Span<Vector2> dirs = stackalloc Vector2[8]
            {
                new( 1,  0), new( k,  k), new( 0,  1), new(-k,  k),
                new(-1,  0), new(-k, -k), new( 0, -1), new( k, -k),
            };

            // Concentric shadow rings
            Span<float> radii  = stackalloc float[] { 0.7f,  1.6f,  2.8f,  4.5f,  7.0f, 10.0f, 13.0f };
            Span<float> alphas = stackalloc float[] { 0.68f, 0.46f, 0.30f, 0.19f, 0.10f, 0.05f, 0.025f };

            for (int ringIdx = 0; ringIdx < radii.Length; ringIdx++)
            {
                var shadowColor = new Vector4(0f, 0f, 0f, alphas[ringIdx]);
                uint shadow = ImGui.ColorConvertFloat4ToU32(shadowColor);
                float r = radii[ringIdx];
                for (int i = 0; i < 8; i++)
                {
                    dl.AddText(textPos + dirs[i] * r, shadow, visible);
                }
            }

            // Crisp main text on top.
            uint fg = ImGui.ColorConvertFloat4ToU32(textColor);
            dl.AddText(textPos, fg, visible);
        }

        private static void DrawRibbonGlow(ImDrawListPtr dl, Span<Vector2> pts, Vector4 color, float intensity)
        {
            var halo = ColorWithAlpha(color, color.W * 0.18f * intensity);
            var mid  = ColorWithAlpha(color, color.W * 0.38f * intensity);
            var core = ColorBoosted(color, 0.22f, MathF.Min(1f, color.W * 0.92f * intensity));
            DrawTaperedPolyline(dl, pts, halo, 7.5f);
            DrawTaperedPolyline(dl, pts, mid,  3.5f);
            DrawTaperedPolyline(dl, pts, core, 1.2f);
        }

        private static Vector4 ColorWithAlpha(Vector4 c, float a)
            => new(c.X, c.Y, c.Z, MathF.Max(0f, MathF.Min(1f, a)));

        private static Vector4 ColorBoosted(Vector4 c, float lift, float a)
            => new(MathF.Min(1f, c.X + lift),
                   MathF.Min(1f, c.Y + lift),
                   MathF.Min(1f, c.Z + lift),
                   MathF.Max(0f, MathF.Min(1f, a)));

        private static void DrawTaperedPolyline(ImDrawListPtr dl, Span<Vector2> pts, Vector4 col, float thickness)
        {
            int N = pts.Length;
            for (int i = 0; i < N - 1; i++)
            {
                float t = (i + 0.5f) / (N - 1);
                float taper = MathF.Sin(t * MathF.PI);
                if (taper < 0.02f) continue;
                var c = col; c.W *= taper;
                dl.AddLine(pts[i], pts[i + 1], ImGui.ColorConvertFloat4ToU32(c), thickness);
            }
        }
    }
}
