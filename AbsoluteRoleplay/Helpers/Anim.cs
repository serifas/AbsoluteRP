using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

namespace AbsoluteRP.Helpers
{
    public static class Anim
    {
        public enum Ease
        {
            Linear,
            OutCubic,
            OutQuint,
            OutBack,
            OutElastic,
            InOutCubic,
        }

        private static readonly Stopwatch clock = Stopwatch.StartNew();
        private static readonly Dictionary<string, double> firstSeen = new();
        private static readonly Dictionary<string, string> resetKeys = new();

        private static double Now => clock.Elapsed.TotalSeconds;

        private static bool Enabled
        {
            get
            {
                try { return Plugin.plugin?.Configuration?.AnimationsEnabled ?? true; }
                catch { return true; }
            }
        }

        public static void ResetKey(string scope, string key)
        {
            if (resetKeys.TryGetValue(scope, out var prev) && prev == key) return;
            resetKeys[scope] = key;
            var prefix = scope + "/";
            var toRemove = new List<string>();
            foreach (var k in firstSeen.Keys)
                if (k.StartsWith(prefix, StringComparison.Ordinal))
                    toRemove.Add(k);
            foreach (var k in toRemove) firstSeen.Remove(k);
        }

        public static void Reset(string id)
        {
            firstSeen.Remove(id);
        }

        public static double Elapsed(string id, float delay = 0f)
        {
            if (!firstSeen.TryGetValue(id, out var start))
            {
                start = Now;
                firstSeen[id] = start;
            }
            var e = Now - start - delay;
            return e < 0 ? 0 : e;
        }

        public static float Progress(string id, float duration, float delay = 0f)
        {
            if (!Enabled) return 1f;
            if (duration <= 0f) return 1f;
            var e = Elapsed(id, delay);
            var p = (float)(e / duration);
            if (p < 0f) p = 0f;
            if (p > 1f) p = 1f;
            return p;
        }

        public static float Eased(string id, float duration, Ease ease, float delay = 0f)
            => Apply(Progress(id, duration, delay), ease);

        public static float Apply(float t, Ease ease)
        {
            switch (ease)
            {
                case Ease.OutCubic:
                    return 1f - MathF.Pow(1f - t, 3);
                case Ease.OutQuint:
                    return 1f - MathF.Pow(1f - t, 5);
                case Ease.OutBack:
                {
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    var x = 1f - t;
                    return 1f + c3 * x * x * x - c1 * x * x;
                }
                case Ease.OutElastic:
                {
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    const float c4 = (2f * MathF.PI) / 3f;
                    return MathF.Pow(2f, -10f * t) * MathF.Sin((t * 10f - 0.75f) * c4) + 1f;
                }
                case Ease.InOutCubic:
                    return t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3) / 2f;
                case Ease.Linear:
                default:
                    return t;
            }
        }

        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => new(Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t));
        public static Vector4 Lerp(Vector4 a, Vector4 b, float t)
            => new(Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t), Lerp(a.Z, b.Z, t), Lerp(a.W, b.W, t));

        public static void PushAlpha(string id, float duration, float delay = 0f, Ease ease = Ease.OutCubic)
        {
            var a = Eased(id, duration, ease, delay);
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, a);
        }

        public static void PopAlpha() => ImGui.PopStyleVar();

        public static float Scale(string id, float duration, float from = 0.2f, float to = 1.0f, float delay = 0f, Ease ease = Ease.OutBack)
            => Lerp(from, to, Eased(id, duration, ease, delay));

        public static float SlideOffsetY(string id, float duration, float distance = 18f, float delay = 0f, Ease ease = Ease.OutCubic)
            => Lerp(distance, 0f, Eased(id, duration, ease, delay));

        public static float GrowFromCenter(string id, float duration, float delay = 0f, Ease ease = Ease.OutQuint)
            => Eased(id, duration, ease, delay);

        public static bool IsComplete(string id, float duration, float delay = 0f)
            => Progress(id, duration, delay) >= 1f;

        public static void DrawGrowingDivider(string id, float duration = 0.55f, float delay = 0f, float height = 1.5f, Ease ease = Ease.OutQuint)
        {
            var dl = ImGui.GetWindowDrawList();
            var avail = ImGui.GetContentRegionAvail().X;
            var p = ImGui.GetCursorScreenPos();
            float t = Eased(id, duration, ease, delay);
            float halfMax = avail * 0.5f;
            float half = halfMax * t;
            float midX = p.X + halfMax;
            float topY = p.Y + 4f;
            float botY = topY + height;
            uint cMid = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.85f, 0.30f, 0.85f * t));
            uint cEnd = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.85f, 0.30f, 0.0f));
            dl.AddRectFilledMultiColor(
                new Vector2(midX - half, topY),
                new Vector2(midX, botY),
                cEnd, cMid, cMid, cEnd);
            dl.AddRectFilledMultiColor(
                new Vector2(midX, topY),
                new Vector2(midX + half, botY),
                cMid, cEnd, cEnd, cMid);
            ImGui.Dummy(new Vector2(avail, height + 8f));
        }

        public static void DrawScaledCenteredImage(string id, ImTextureID handle, Vector2 baseSize, float duration = 0.65f, float delay = 0f, Ease ease = Ease.OutBack, float fromScale = 0.25f)
        {
            float s = Scale(id, duration, fromScale, 1f, delay, ease);
            float alpha = Eased(id + ".a", duration, Ease.OutCubic, delay);
            var scaled = baseSize * s;
            var avail = ImGui.GetContentRegionAvail().X;
            float offsetX = (avail - scaled.X) * 0.5f;
            float pad = (baseSize.Y - scaled.Y) * 0.5f;
            ImGui.Dummy(new Vector2(0, pad));
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);
            ImGui.Image(handle, scaled, new Vector2(0, 0), new Vector2(1, 1), new Vector4(1, 1, 1, alpha));
            ImGui.Dummy(new Vector2(0, pad));
        }

        public static void DrawCircleAvatarAt(ImTextureID handle, Vector2 center, float radius, Vector4 borderColor, float borderThickness = 2f, float alpha = 1f)
        {
            if (radius <= 0f) return;
            if (handle.Handle == 0) return;
            var dl = ImGui.GetWindowDrawList();
            var min = new Vector2(center.X - radius, center.Y - radius);
            var max = new Vector2(center.X + radius, center.Y + radius);
            uint tint = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, alpha));
            dl.AddImageRounded(handle, min, max, new Vector2(0, 0), new Vector2(1, 1), tint, radius);
            if (borderThickness > 0f)
            {
                var glow = borderColor;
                glow.W *= alpha * 0.35f;
                dl.AddCircle(center, radius + borderThickness * 0.5f, ImGui.ColorConvertFloat4ToU32(glow), 64, borderThickness + 3f);
                var ring = borderColor;
                ring.W *= alpha;
                dl.AddCircle(center, radius - borderThickness * 0.5f, ImGui.ColorConvertFloat4ToU32(ring), 64, borderThickness);
            }
        }

        public static void DrawCircleAvatarInline(ImTextureID handle, float diameter, Vector4 borderColor, float borderThickness = 2f, float alpha = 1f)
        {
            var p = ImGui.GetCursorScreenPos();
            float radius = diameter * 0.5f;
            var center = new Vector2(p.X + radius, p.Y + radius);
            DrawCircleAvatarAt(handle, center, radius, borderColor, borderThickness, alpha);
            ImGui.Dummy(new Vector2(diameter, diameter));
        }

        public static void DrawScaledCenteredCircleAvatar(string id, ImTextureID handle, Vector2 baseSize, Vector4 borderColor, float duration = 0.65f, float delay = 0f, Ease ease = Ease.OutBack, float fromScale = 0.25f, float borderThickness = 2.5f)
        {
            float s = Scale(id, duration, fromScale, 1f, delay, ease);
            float alpha = Eased(id + ".a", duration, Ease.OutCubic, delay);
            float baseDiameter = MathF.Min(baseSize.X, baseSize.Y);
            float scaledDiameter = baseDiameter * s;
            float radius = scaledDiameter * 0.5f;

            var avail = ImGui.GetContentRegionAvail().X;
            float offsetX = (avail - scaledDiameter) * 0.5f;
            float pad = (baseDiameter - scaledDiameter) * 0.5f;
            ImGui.Dummy(new Vector2(0, pad));
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);

            var p = ImGui.GetCursorScreenPos();
            var center = new Vector2(p.X + radius, p.Y + radius);
            DrawCircleAvatarAt(handle, center, radius, borderColor, borderThickness, alpha);
            ImGui.Dummy(new Vector2(scaledDiameter, scaledDiameter));
            ImGui.Dummy(new Vector2(0, pad));
        }
    }
}
