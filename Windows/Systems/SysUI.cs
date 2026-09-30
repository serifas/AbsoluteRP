using AbsoluteRP.RsUI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace AbsoluteRP.Windows.Systems
{
    // Small layout / styling helpers shared by the Systems screens so they read the same as the rest of the RsUI pages: muted section labels, stacked form fields, themed number inputs, badges, bars, step indicator and themed popups. Everything here is purely visual.
    internal static class SysUI
    {
        public static float S(float v) => RsTheme.S(v);
        public static Vector2 S(float x, float y) => RsTheme.S(x, y);

        public static Vector4 Fade(Vector4 c, float a) => new(c.X, c.Y, c.Z, a);

        public static Vector4 Mix(Vector4 a, Vector4 b, float t) => new(
            a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, a.W + (b.W - a.W) * t);

        public static Vector4 Brighten(Vector4 c, float amt) =>
            new(Math.Min(1f, c.X + amt), Math.Min(1f, c.Y + amt), Math.Min(1f, c.Z + amt), c.W);

        public static uint U(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);

        // Status palette used by roster / submissions (Pending, Approved, Declined, Revision).
        public static Vector4 StatusColor(int status) => status switch
        {
            0 => RsTheme.AccentWarning,
            1 => RsTheme.AccentSuccess,
            2 => RsTheme.AccentDanger,
            _ => RsTheme.AccentPrimary,
        };

        // Scopes

        public readonly struct ColorScope : IDisposable
        {
            private readonly int _count;
            public ColorScope(int count) { _count = count; }
            public void Dispose() { if (_count > 0) ImGui.PopStyleColor(_count); }
        }

        public static ColorScope TextColor(Vector4 color)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            return new ColorScope(1);
        }

        public readonly struct GroupScope : IDisposable
        {
            public void Dispose() => ImGui.EndGroup();
        }

        // Stacked form field: muted label above whatever is drawn inside the scope. using (SysUI.Field("Base HP")) { SysUI.InputInt(...); }
        public static GroupScope Field(string label)
        {
            ImGui.BeginGroup();
            Muted(label);
            return new GroupScope();
        }

        // Text

        public static void Text(string text, Vector4 color)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            try { ImGui.TextUnformatted(text ?? string.Empty); }
            finally { ImGui.PopStyleColor(); }
        }

        public static void Wrapped(string text, Vector4 color)
        {
            // Wrap at the inset edge of the enclosing RsUI panel, not the window edge.
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + Math.Max(S(40f), RsElements.AvailContentWidth()));
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            try { ImGui.TextUnformatted(text ?? string.Empty); }
            finally
            {
                ImGui.PopStyleColor();
                ImGui.PopTextWrapPos();
            }
        }

        public static void Primary(string text) => Text(text, RsTheme.TextPrimary);
        public static void Secondary(string text) => Text(text, RsTheme.TextSecondary);
        public static void Muted(string text) => Text(text, RsTheme.TextMuted);
        public static void Accent(string text) => Text(text, RsTheme.AccentPrimary);
        public static void MutedWrapped(string text) => Wrapped(text, RsTheme.TextMuted);
        public static void SecondaryWrapped(string text) => Wrapped(text, RsTheme.TextSecondary);

        // Upper-case muted label that introduces a group of controls.
        public static void SectionLabel(string text)
        {
            ImGui.Dummy(new Vector2(0f, S(2f)));
            Text((text ?? string.Empty).ToUpperInvariant(), RsTheme.TextMuted);
            ImGui.Dummy(new Vector2(0f, S(2f)));
        }

        // Page / card heading with an optional muted subtitle beneath.
        public static void Heading(string title, string? subtitle = null)
        {
            Primary(title);
            if (!string.IsNullOrEmpty(subtitle))
                MutedWrapped(subtitle);
            Gap(6f);
        }

        // Centered single line of text across the available width.
        public static void Centered(string text, Vector4 color)
        {
            var avail = ImGui.GetContentRegionAvail().X;
            var w = ImGui.CalcTextSize(text ?? string.Empty).X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (avail - w) * 0.5f));
            Text(text, color);
        }

        public static void Gap(float px = 10f) => ImGui.Dummy(new Vector2(0f, S(px)));

        // Thin themed divider across the usable width.
        public static void Divider()
        {
            Gap(4f);
            var start = ImGui.GetCursorScreenPos();
            var w = RsElements.AvailContentWidth();
            ImGui.GetWindowDrawList().AddLine(start, start + new Vector2(w, 0f), RsTheme.U.Border, RsTheme.BorderThickness);
            ImGui.Dummy(new Vector2(w, S(5f)));
        }

        // Right-align the next item of the given width on the current line.
        public static void AlignRight(float itemWidth)
        {
            var avail = RsElements.AvailContentWidth();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, avail - itemWidth));
        }

        // Inputs

        private const int FrameColorCount = 9;
        private const int FrameVarCount = 3;

        public static void PushFrameStyle()
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, RsTheme.BgTertiary);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Brighten(RsTheme.BgTertiary, 0.04f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Brighten(RsTheme.BgTertiary, 0.07f));
            ImGui.PushStyleColor(ImGuiCol.Button, RsTheme.BgSecondary);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Brighten(RsTheme.BgSecondary, 0.06f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, RsTheme.AccentPrimary);
            ImGui.PushStyleColor(ImGuiCol.Border, RsTheme.Border);
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
            ImGui.PushStyleColor(ImGuiCol.TextSelectedBg, Fade(RsTheme.AccentPrimary, 0.45f));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, S(6f));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, RsTheme.BorderThickness);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, S(10f, 8f));
        }

        public static void PopFrameStyle()
        {
            ImGui.PopStyleVar(FrameVarCount);
            ImGui.PopStyleColor(FrameColorCount);
        }

        // Themed integer input with +/- step buttons. Width is design px.
        public static bool InputInt(string id, ref int value, float width = 120f)
        {
            PushFrameStyle();
            try
            {
                ImGui.SetNextItemWidth(S(width));
                return ImGui.InputInt("##" + id, ref value);
            }
            finally { PopFrameStyle(); }
        }

        public static bool InputFloat(string id, ref float value, float width = 140f)
        {
            PushFrameStyle();
            try
            {
                ImGui.SetNextItemWidth(S(width));
                return ImGui.InputFloat("##" + id, ref value, 0.1f, 1.0f, "%.2f");
            }
            finally { PopFrameStyle(); }
        }

        public static bool ColorPicker(string id, ref Vector4 color)
        {
            PushFrameStyle();
            try { return ImGui.ColorEdit4("##" + id, ref color, ImGuiColorEditFlags.NoInputs); }
            finally { PopFrameStyle(); }
        }

        // Buttons

        // RsElements.Button with a disabled look (flat, muted, not clickable) since the draw-list chrome ignores ImGui's disabled alpha.
        public static bool Button(string label, RsElements.ButtonVariant variant = RsElements.ButtonVariant.Primary,
                                  bool enabled = true, Vector2 size = default)
        {
            if (enabled) return RsElements.Button(label, variant, size);

            var display = label;
            var hash = display.IndexOf("##", StringComparison.Ordinal);
            if (hash >= 0) display = display.Substring(0, hash);
            var textSize = ImGui.CalcTextSize(display);
            var pad = S(18f, 9f);
            if (size.X <= 0f) size.X = textSize.X + pad.X * 2f;
            if (size.Y <= 0f) size.Y = textSize.Y + pad.Y * 2f;

            var start = ImGui.GetCursorScreenPos();
            var draw = ImGui.GetWindowDrawList();
            draw.AddRectFilled(start, start + size, U(RsTheme.BgTertiary), S(8f));
            draw.AddRect(start, start + size, U(RsTheme.Border), S(8f), ImDrawFlags.None, RsTheme.BorderThickness);
            draw.AddText(start + (size - textSize) * 0.5f, U(RsTheme.TextMuted), display);
            ImGui.Dummy(size);
            return false;
        }

        public static bool IconButton(FontAwesomeIcon icon, string id, bool enabled = true,
                                      RsElements.ButtonVariant variant = RsElements.ButtonVariant.Secondary, float size = 28f)
        {
            if (enabled) return RsElements.IconButton(icon, id, variant, size);

            var sz = S(size);
            var start = ImGui.GetCursorScreenPos();
            var draw = ImGui.GetWindowDrawList();
            draw.AddRectFilled(start, start + new Vector2(sz, sz), U(RsTheme.BgTertiary), S(8f));
            draw.AddRect(start, start + new Vector2(sz, sz), U(RsTheme.Border), S(8f), ImDrawFlags.None, RsTheme.BorderThickness);
            var glyph = icon.ToIconString();
            using (RsIcons.Push())
            {
                var g = ImGui.CalcTextSize(glyph);
                draw.AddText(start + (new Vector2(sz, sz) - g) * 0.5f, U(RsTheme.TextMuted), glyph);
            }
            ImGui.Dummy(new Vector2(sz, sz));
            return false;
        }

        // Decorations

        // Small rounded colour swatch that sits on a text line.
        public static void Swatch(Vector4 color, float width = 10f, float height = 0f)
        {
            var w = S(width);
            var h = height > 0f ? S(height) : ImGui.GetTextLineHeight();
            var p = ImGui.GetCursorScreenPos();
            ImGui.GetWindowDrawList().AddRectFilled(p, p + new Vector2(w, h), U(color), S(3f));
            ImGui.Dummy(new Vector2(w, h));
        }

        // Tinted pill with coloured text, e.g. a status chip.
        public static void Badge(string text, Vector4 color)
        {
            var pad = S(8f, 2f);
            var ts = ImGui.CalcTextSize(text);
            var size = ts + pad * 2f;
            var p = ImGui.GetCursorScreenPos();
            var draw = ImGui.GetWindowDrawList();
            draw.AddRectFilled(p, p + size, U(Fade(color, 0.16f)), size.Y * 0.5f);
            draw.AddRect(p, p + size, U(Fade(color, 0.45f)), size.Y * 0.5f, ImDrawFlags.None, RsTheme.BorderThickness);
            draw.AddText(p + pad, U(color), text);
            ImGui.Dummy(size);
        }

        // Rounded progress bar with centred label. baseFill/baseColor draws a dimmer first segment (used for "base + bonus" resource bars).
        public static void Bar(float fill, Vector4 color, string label, float height = 18f,
                               float baseFill = -1f, Vector4? baseColor = null)
        {
            var w = Math.Max(S(40f), RsElements.AvailContentWidth());
            var h = S(height);
            var p = ImGui.GetCursorScreenPos();
            var draw = ImGui.GetWindowDrawList();
            var r = h * 0.5f;
            draw.AddRectFilled(p, p + new Vector2(w, h), U(RsTheme.BgTertiary), r);

            fill = Math.Clamp(fill, 0f, 1f);
            if (baseFill >= 0f)
            {
                baseFill = Math.Clamp(baseFill, 0f, 1f);
                if (baseFill > 0f)
                    draw.AddRectFilled(p, p + new Vector2(w * baseFill, h), U(baseColor ?? Fade(color, 0.4f)), r);
                if (fill > baseFill)
                    draw.AddRectFilled(p + new Vector2(w * baseFill, 0f), p + new Vector2(w * fill, h), U(color), r);
            }
            else if (fill > 0f)
            {
                draw.AddRectFilled(p, p + new Vector2(Math.Max(w * fill, h), h), U(color), r);
            }

            draw.AddRect(p, p + new Vector2(w, h), U(RsTheme.Border), r, ImDrawFlags.None, RsTheme.BorderThickness);
            if (!string.IsNullOrEmpty(label))
            {
                var ts = ImGui.CalcTextSize(label);
                var tp = p + new Vector2((w - ts.X) * 0.5f, (h - ts.Y) * 0.5f);
                draw.AddText(tp + new Vector2(1f, 1f), 0xAA000000, label);
                draw.AddText(tp, RsTheme.U.TextPrimary, label);
            }
            ImGui.Dummy(new Vector2(w, h));
        }

        // Card background + border for hand-laid-out tiles.
        public static void DrawCard(ImDrawListPtr draw, Vector2 min, Vector2 max, bool hovered, bool selected, float rounding = 8f)
        {
            var r = S(rounding);
            var bg = selected
                ? Mix(RsTheme.PanelSurface, Fade(RsTheme.AccentPrimary, 1f), 0.14f)
                : hovered ? Brighten(RsTheme.PanelSurface, 0.03f) : RsTheme.PanelSurface;
            draw.AddRectFilled(min, max, U(bg), r);
            var border = selected ? RsTheme.AccentPrimary : hovered ? RsTheme.BorderStrong : RsTheme.Border;
            draw.AddRect(min, max, U(border), r, ImDrawFlags.None, selected ? RsTheme.BorderThickness * 2f : RsTheme.BorderThickness);
        }

        // Numbered step tracker for wizards.
        public static void StepIndicator(IReadOnlyList<string> steps, int current)
        {
            if (steps.Count == 0) return;
            var draw = ImGui.GetWindowDrawList();
            var start = ImGui.GetCursorScreenPos();
            var w = Math.Max(S(200f), RsElements.AvailContentWidth());
            var seg = w / steps.Count;
            var dot = S(22f);
            var lineH = ImGui.GetTextLineHeight();
            var cy = start.Y + dot * 0.5f;

            // Connectors first so the dots sit on top.
            for (int i = 0; i < steps.Count - 1; i++)
            {
                var x1 = start.X + seg * i + seg * 0.5f + dot * 0.5f + S(4f);
                var x2 = start.X + seg * (i + 1) + seg * 0.5f - dot * 0.5f - S(4f);
                if (x2 <= x1) continue;
                var col = i < current ? RsTheme.AccentPrimary : RsTheme.Border;
                draw.AddLine(new Vector2(x1, cy), new Vector2(x2, cy), U(col), Math.Max(1f, S(2f)));
            }

            for (int i = 0; i < steps.Count; i++)
            {
                var cx = start.X + seg * i + seg * 0.5f;
                var c = new Vector2(cx, cy);
                bool done = i < current, now = i == current;
                if (now)
                {
                    draw.AddCircleFilled(c, dot * 0.5f + S(3f), U(Fade(RsTheme.AccentPrimary, 0.25f)));
                    draw.AddCircleFilled(c, dot * 0.5f, U(RsTheme.AccentPrimary));
                }
                else if (done)
                {
                    draw.AddCircleFilled(c, dot * 0.5f, U(Fade(RsTheme.AccentPrimary, 0.35f)));
                    draw.AddCircle(c, dot * 0.5f, U(RsTheme.AccentPrimary), 0, RsTheme.BorderThickness);
                }
                else
                {
                    draw.AddCircleFilled(c, dot * 0.5f, U(RsTheme.BgTertiary));
                    draw.AddCircle(c, dot * 0.5f, U(RsTheme.Border), 0, RsTheme.BorderThickness);
                }

                var num = (i + 1).ToString();
                var ns = ImGui.CalcTextSize(num);
                draw.AddText(c - ns * 0.5f, U(now || done ? RsTheme.TextPrimary : RsTheme.TextMuted), num);

                var label = steps[i];
                var ls = ImGui.CalcTextSize(label);
                var lp = new Vector2(cx - ls.X * 0.5f, start.Y + dot + S(6f));
                var lc = now ? RsTheme.TextPrimary : done ? RsTheme.TextSecondary : RsTheme.TextMuted;
                draw.AddText(lp, U(lc), label);
            }

            ImGui.Dummy(new Vector2(w, dot + S(6f) + lineH + S(4f)));
        }

        // Popups / windows

        private const int PopupColorCount = 6;
        private const int PopupVarCount = 7;

        private static void PushPopupStyle()
        {
            ImGui.PushStyleColor(ImGuiCol.PopupBg, RsTheme.BgSecondary);
            ImGui.PushStyleColor(ImGuiCol.WindowBg, RsTheme.BgSecondary);
            ImGui.PushStyleColor(ImGuiCol.Border, RsTheme.Border);
            ImGui.PushStyleColor(ImGuiCol.TitleBg, RsTheme.BgSecondary);
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, Brighten(RsTheme.BgSecondary, 0.04f));
            ImGui.PushStyleColor(ImGuiCol.ModalWindowDimBg, new Vector4(0f, 0f, 0f, 0.55f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, S(18f, 16f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, S(8f));
            ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, S(8f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, RsTheme.BorderThickness);
            ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, RsTheme.BorderThickness);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, S(8f, 8f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowTitleAlign, new Vector2(0f, 0.5f));
        }

        private static void PopPopupStyle()
        {
            ImGui.PopStyleVar(PopupVarCount);
            ImGui.PopStyleColor(PopupColorCount);
        }

        // Themed BeginPopupModal. Only call EndModal when this returned true.
        public static bool BeginModal(string name, ref bool open)
        {
            PushPopupStyle();
            bool visible;
            try { visible = ImGui.BeginPopupModal(name, ref open, ImGuiWindowFlags.AlwaysAutoResize); }
            catch { PopPopupStyle(); throw; }
            if (!visible) PopPopupStyle();
            return visible;
        }

        public static void EndModal()
        {
            ImGui.EndPopup();
            PopPopupStyle();
        }

        // Themed BeginPopup. Only call EndPopup when this returned true.
        public static bool BeginPopup(string name)
        {
            PushPopupStyle();
            bool visible;
            try { visible = ImGui.BeginPopup(name); }
            catch { PopPopupStyle(); throw; }
            if (!visible) PopPopupStyle();
            return visible;
        }

        public static void EndPopup()
        {
            ImGui.EndPopup();
            PopPopupStyle();
        }

        private const int WindowColorCount = 5;
        private const int WindowVarCount = 3;

        // Push before ImGui.Begin of a floating Systems window, pop after its End.
        public static void PushWindowStyle()
        {
            ImGui.PushStyleColor(ImGuiCol.WindowBg, RsTheme.BgPrimary);
            ImGui.PushStyleColor(ImGuiCol.TitleBg, RsTheme.BgSecondary);
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, Brighten(RsTheme.BgSecondary, 0.04f));
            ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, RsTheme.BgSecondary);
            ImGui.PushStyleColor(ImGuiCol.Border, RsTheme.Border);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, S(16f, 14f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, S(8f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, RsTheme.BorderThickness);
        }

        public static void PopWindowStyle()
        {
            ImGui.PopStyleVar(WindowVarCount);
            ImGui.PopStyleColor(WindowColorCount);
        }

        // Tooltip with themed background / padding. Always pair with SysUI.EndTooltip.
        public static void BeginTooltip()
        {
            ImGui.PushStyleColor(ImGuiCol.PopupBg, RsTheme.BgSecondary);
            ImGui.PushStyleColor(ImGuiCol.Border, RsTheme.Border);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, S(12f, 10f));
            ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, S(6f));
            ImGui.BeginTooltip();
        }

        public static void EndTooltip()
        {
            ImGui.EndTooltip();
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(2);
        }
    }
}
