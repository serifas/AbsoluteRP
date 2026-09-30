using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using System.Numerics;

namespace AbsoluteRP.RsUI;

/// Reusable, professionally-styled UI widgets - buttons, inputs, text areas, dropdowns, toggles, checkboxes, radios, and panels. Every element paints its own chrome via the draw list so the look is consistent across the plugin, while a real ImGui widget underneath (InvisibleButton, InputText, BeginCombo, ...) handles keyboard and mouse input. Interactions animate. Per-widget state is cached in a static dictionary keyed by ImGui id and eased toward its target with a simple time-based lerp - hover fades in, focus glow rises, toggle knobs slide. No allocations per frame.
public static class RsElements
{
    // Animation state cache

    private sealed class WidgetState
    {
        public float Hover;
        public float HoverTarget;
        public float Focus;
        public float FocusTarget;
        public float Press;
        public float PressTarget;
        /// Continuous 0..1 value for toggle knob / check fill / dropdown open.
        public float Value;
    }

    private static readonly Dictionary<uint, WidgetState> States = new();

    private static WidgetState Get(uint id)
    {
        if (!States.TryGetValue(id, out var s))
            States[id] = s = new WidgetState();
        return s;
    }

    /// Frame-rate-independent lerp toward target.
    private static float StepTo(float current, float target, float dt, float speed = 12f)
    {
        var t = Math.Clamp(dt * speed, 0f, 1f);
        return current + (target - current) * t;
    }

    // Color helpers

    private static Vector4 LerpV4(Vector4 a, Vector4 b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Vector4(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Z + (b.Z - a.Z) * t,
            a.W + (b.W - a.W) * t);
    }

    private static uint PackV4(Vector4 c)
    {
        static uint B(float x) => (uint)Math.Clamp((int)(x * 255f + 0.5f), 0, 255);
        return B(c.X) | (B(c.Y) << 8) | (B(c.Z) << 16) | (B(c.W) << 24);
    }

    private static Vector4 Brighten(Vector4 c, float amt) =>
        new(Math.Min(1f, c.X + amt), Math.Min(1f, c.Y + amt), Math.Min(1f, c.Z + amt), c.W);

    /// Strip ImGui's ##id suffix so it isn't drawn as text.
    private static string DisplayLabel(string raw)
    {
        var i = raw.IndexOf("##", StringComparison.Ordinal);
        return i < 0 ? raw : raw.Substring(0, i);
    }

    private static float   S(float v)                => RsTheme.S(v);
    private static Vector2 Sv(float x, float y)      => RsTheme.S(x, y);

    // Buttons

    public enum ButtonVariant
    {
        Primary,
        Secondary,
        Danger,
        Success,
        Ghost,
    }

    private static (Vector4 Bg, Vector4 Text, bool HasBorder) VariantColors(ButtonVariant v) => v switch
    {
        ButtonVariant.Primary   => (RsTheme.AccentPrimary,  RsTheme.TextPrimary,   false),
        ButtonVariant.Secondary => (RsTheme.BgTertiary,     RsTheme.TextPrimary,   false),
        ButtonVariant.Danger    => (RsTheme.AccentDanger,   RsTheme.TextPrimary,   false),
        ButtonVariant.Success   => (RsTheme.AccentSuccess,  RsTheme.TextPrimary,   false),
        ButtonVariant.Ghost     => (new Vector4(0, 0, 0, 0.8f), RsTheme.TextPrimary,  true),
        _                       => (RsTheme.AccentPrimary,  RsTheme.TextPrimary,   false),
    };

    // Predict the pixel width of a Button call with default padding - useful when you need to reserve space for a trailing button on a row of flex-sized inputs so they don't push it off the panel.
    public static float MeasureButtonWidth(string label)
    {
        var display = DisplayLabel(label);
        var textSize = ImGui.CalcTextSize(display);
        var pad = Sv(18f, 9f);
        return textSize.X + pad.X * 2f;
    }

    public static bool Button(string label, ButtonVariant variant = ButtonVariant.Primary, Vector2 size = default)
    {
        var display = DisplayLabel(label);
        var id = ImGui.GetID(label);
        var state = Get(id);
        var dt = ImGui.GetIO().DeltaTime;
        state.Hover = StepTo(state.Hover, state.HoverTarget, dt);
        state.Press = StepTo(state.Press, state.PressTarget, dt, 22f);

        var textSize = ImGui.CalcTextSize(display);
        var pad = Sv(18f, 9f);
        if (size.X <= 0f) size.X = textSize.X + pad.X * 2f;
        if (size.Y <= 0f) size.Y = textSize.Y + pad.Y * 2f;

        var start = ImGui.GetCursorScreenPos();
        var draw  = ImGui.GetWindowDrawList();

        var (baseBg, textColor, hasBorder) = VariantColors(variant);
        var hoverBg = variant == ButtonVariant.Ghost
            ? new Vector4(RsTheme.BgHover.X, RsTheme.BgHover.Y, RsTheme.BgHover.Z, 0.6f)
            : Brighten(baseBg, 0.06f);
        var pressBg = variant == ButtonVariant.Ghost
            ? new Vector4(RsTheme.BgTertiary.X, RsTheme.BgTertiary.Y, RsTheme.BgTertiary.Z, 0.8f)
            : Brighten(baseBg, -0.06f);

        var cur = LerpV4(baseBg, hoverBg, state.Hover);
        cur     = LerpV4(cur,    pressBg, state.Press);

        var sink = state.Press * S(1f);
        var rectMin = new Vector2(start.X, start.Y + sink);
        var rectMax = rectMin + size;

        var radius = S(8f);
        draw.AddRectFilled(rectMin, rectMax, PackV4(cur), radius);
        if (hasBorder)
            draw.AddRect(rectMin, rectMax, PackV4(LerpV4(RsTheme.Border, RsTheme.BorderStrong, state.Hover)), radius, ImDrawFlags.None, RsTheme.BorderThickness);

        var textPos = new Vector2(
            rectMin.X + (size.X - textSize.X) * 0.5f,
            rectMin.Y + (size.Y - textSize.Y) * 0.5f);
        draw.AddText(textPos, PackV4(textColor), display);

        ImGui.SetCursorScreenPos(start);
        var clicked = ImGui.InvisibleButton("##rs_btn_" + label, size);
        state.HoverTarget = ImGui.IsItemHovered() ? 1f : 0f;
        state.PressTarget = ImGui.IsItemActive()  ? 1f : 0f;
        return clicked;
    }

    public static bool IconButton(FontAwesomeIcon icon, string id, ButtonVariant variant = ButtonVariant.Ghost, float size = 32f)
    {
        var key = ImGui.GetID("rs_iconbtn_" + id);
        var state = Get(key);
        var dt = ImGui.GetIO().DeltaTime;
        state.Hover = StepTo(state.Hover, state.HoverTarget, dt);
        state.Press = StepTo(state.Press, state.PressTarget, dt, 22f);

        var sz = S(size);
        var start = ImGui.GetCursorScreenPos();
        var draw  = ImGui.GetWindowDrawList();
        var (baseBg, textColor, hasBorder) = VariantColors(variant);
        var hoverBg = variant == ButtonVariant.Ghost
            ? new Vector4(RsTheme.BgHover.X, RsTheme.BgHover.Y, RsTheme.BgHover.Z, 0.6f)
            : Brighten(baseBg, 0.06f);
        var pressBg = variant == ButtonVariant.Ghost
            ? new Vector4(RsTheme.BgTertiary.X, RsTheme.BgTertiary.Y, RsTheme.BgTertiary.Z, 0.8f)
            : Brighten(baseBg, -0.06f);
        var cur = LerpV4(baseBg, hoverBg, state.Hover);
        cur     = LerpV4(cur,    pressBg, state.Press);

        var sink = state.Press * S(1f);
        var rectMin = new Vector2(start.X, start.Y + sink);
        var rectMax = rectMin + new Vector2(sz, sz);
        var radius = S(8f);
        draw.AddRectFilled(rectMin, rectMax, PackV4(cur), radius);
        if (hasBorder)
            draw.AddRect(rectMin, rectMax, PackV4(LerpV4(RsTheme.Border, RsTheme.BorderStrong, state.Hover)), radius, ImDrawFlags.None, RsTheme.BorderThickness);

        var glyph = icon.ToIconString();
        Vector2 gSize;
        using (RsIcons.Push())
            gSize = ImGui.CalcTextSize(glyph);
        var gPos = new Vector2(
            rectMin.X + (sz - gSize.X) * 0.5f,
            rectMin.Y + (sz - gSize.Y) * 0.5f);
        using (RsIcons.Push())
            draw.AddText(gPos, PackV4(textColor), glyph);

        ImGui.SetCursorScreenPos(start);
        var clicked = ImGui.InvisibleButton("##rs_iconbtn_" + id, new Vector2(sz, sz));
        state.HoverTarget = ImGui.IsItemHovered() ? 1f : 0f;
        state.PressTarget = ImGui.IsItemActive()  ? 1f : 0f;
        return clicked;
    }


    public static bool InputText(string id, ref string value, int maxLength = 256, string placeholder = "", float width = 0f)
    {
        var key = ImGui.GetID("rs_input_" + id);
        var state = Get(key);
        var dt = ImGui.GetIO().DeltaTime;
        state.Focus = StepTo(state.Focus, state.FocusTarget, dt, 14f);
        state.Hover = StepTo(state.Hover, state.HoverTarget, dt);

        var draw = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var w = width > 0f
            ? S(width)
            : Math.Max(S(80f), ImGui.GetContentRegionAvail().X - CurrentRightReserve());
        var pad = Sv(10f, 8f);
        var h = ImGui.GetTextLineHeight() + pad.Y * 2f;
        var radius = S(6f);

        var bg = LerpV4(RsTheme.BgTertiary, RsTheme.BgHover, Math.Max(state.Hover * 0.6f, state.Focus * 0.4f));
        var border = LerpV4(RsTheme.Border, RsTheme.AccentPrimary, state.Focus);

        draw.AddRectFilled(start, start + new Vector2(w, h), PackV4(bg), radius);
        draw.AddRect(start, start + new Vector2(w, h), PackV4(border), radius, ImDrawFlags.None, RsTheme.BorderThickness + state.Focus);

        ImGui.PushStyleColor(ImGuiCol.FrameBg,        0u);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, 0u);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive,  0u);
        ImGui.PushStyleColor(ImGuiCol.Text,           PackV4(RsTheme.TextPrimary));
        ImGui.PushStyleColor(ImGuiCol.TextDisabled,   PackV4(RsTheme.TextMuted));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,  pad);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, radius);

        ImGui.SetNextItemWidth(w);
        ImGui.SetCursorScreenPos(start);
        bool changed;
        if (!string.IsNullOrEmpty(placeholder))
            changed = ImGui.InputTextWithHint("##rs_input_" + id, placeholder, ref value, maxLength);
        else
            changed = ImGui.InputText("##rs_input_" + id, ref value, maxLength);
        state.FocusTarget = ImGui.IsItemActive()  ? 1f : 0f;
        state.HoverTarget = ImGui.IsItemHovered() ? 1f : 0f;

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(5);
        return changed;
    }


    public static bool InputTextArea(string id, ref string value, int maxLength = 4096, string placeholder = "", Vector2 size = default)
    {
        var key = ImGui.GetID("rs_area_" + id);
        var state = Get(key);
        var dt = ImGui.GetIO().DeltaTime;
        state.Focus = StepTo(state.Focus, state.FocusTarget, dt, 14f);
        state.Hover = StepTo(state.Hover, state.HoverTarget, dt);

        var draw = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        if (size == default)
        {
            var w = Math.Max(S(120f), ImGui.GetContentRegionAvail().X - CurrentRightReserve());
            var h = ImGui.GetTextLineHeight() * 4f + S(16f);
            size = new Vector2(w, h);
        }
      

        var bg = LerpV4(RsTheme.BgTertiary, RsTheme.BgHover, Math.Max(state.Hover * 0.6f, state.Focus * 0.4f));
        var border = LerpV4(RsTheme.Border, RsTheme.AccentPrimary, state.Focus);
        var radius = S(6f);

        draw.AddRectFilled(start, start + size, PackV4(bg), radius);
        draw.AddRect(start, start + size, PackV4(border), radius, ImDrawFlags.None, RsTheme.BorderThickness + state.Focus);

        ImGui.PushStyleColor(ImGuiCol.FrameBg,        0u);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, 0u);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive,  0u);
        ImGui.PushStyleColor(ImGuiCol.Text,           PackV4(RsTheme.TextPrimary));
        ImGui.PushStyleColor(ImGuiCol.TextDisabled,   PackV4(RsTheme.TextMuted));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,  Sv(10f, 8f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, radius);

        ImGui.SetCursorScreenPos(start);
        bool changed;
        if (!string.IsNullOrEmpty(placeholder) && string.IsNullOrEmpty(value))
        {
            // ImGui.InputTextMultiline has no WithHint variant - paint the placeholder ourselves when the value is empty.
            changed = ImGui.InputTextMultiline("##rs_area_" + id, ref value, maxLength, size);
            var textPos = new Vector2(start.X + S(12f), start.Y + S(8f));
            draw.AddText(textPos, PackV4(RsTheme.TextMuted), placeholder);
        }
        else
        {
            changed = ImGui.InputTextMultiline("##rs_area_" + id, ref value, maxLength, size);
        }
        state.FocusTarget = ImGui.IsItemActive()  ? 1f : 0f;
        state.HoverTarget = ImGui.IsItemHovered() ? 1f : 0f;

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(5);
        return changed;
    }


    // Overload with per-item tooltips. Pass null in a slot to skip that item's tooltip.
    public static bool Dropdown(string id, ref int selected, IReadOnlyList<string> options,
                                IReadOnlyList<string?> tooltips, float width = 0f)
    {
        _dropdownTooltips = tooltips;
        try { return Dropdown(id, ref selected, options, width); }
        finally { _dropdownTooltips = null; }
    }

    [System.ThreadStatic] private static IReadOnlyList<string?>? _dropdownTooltips;

    public static bool Dropdown(string id, ref int selected, IReadOnlyList<string> options, float width = 0f)
    {
        var key = ImGui.GetID("rs_dd_" + id);
        var state = Get(key);
        var dt = ImGui.GetIO().DeltaTime;
        state.Hover = StepTo(state.Hover, state.HoverTarget, dt);

        // Trigger geometry.
        if (width <= 0f) width = Math.Max(S(120f), ImGui.GetContentRegionAvail().X - CurrentRightReserve());
        else             width = S(width);
        var pad = Sv(10f, 8f);
        var height = ImGui.GetTextLineHeight() + pad.Y * 2f;
        var start = ImGui.GetCursorScreenPos();
        var end = start + new Vector2(width, height);
        var draw = ImGui.GetWindowDrawList();
        var radius = S(6f);

        var popupId = "##rs_dd_pop_" + id;
        var isOpen = ImGui.IsPopupOpen(popupId);

        var bg = LerpV4(RsTheme.BgTertiary, RsTheme.BgHover, Math.Max(state.Hover, isOpen ? 0.6f : 0f));
        var border = LerpV4(RsTheme.Border, RsTheme.AccentPrimary, isOpen ? 1f : state.Hover * 0.6f);
        draw.AddRectFilled(start, end, PackV4(bg), radius);
        draw.AddRect(start, end, PackV4(border), radius, ImDrawFlags.None, RsTheme.BorderThickness);

        // Preview text.
        var preview = (selected >= 0 && selected < options.Count) ? options[selected] : "";
        var textPos = new Vector2(start.X + pad.X, start.Y + (height - ImGui.GetTextLineHeight()) * 0.5f);
        draw.AddText(textPos, PackV4(RsTheme.TextPrimary), preview);

        // Chevron on the right. FontAwesome ChevronDown flips to ChevronUp while open.
        var chev = (isOpen ? FontAwesomeIcon.ChevronUp : FontAwesomeIcon.ChevronDown).ToIconString();
        Vector2 chevSize;
        using (RsIcons.Push()) chevSize = ImGui.CalcTextSize(chev);
        var chevPos = new Vector2(end.X - pad.X - chevSize.X, start.Y + (height - chevSize.Y) * 0.5f);
        using (RsIcons.Push())
            draw.AddText(chevPos, PackV4(RsTheme.TextSecondary), chev);

        // Trigger click.
        ImGui.SetCursorScreenPos(start);
        if (ImGui.InvisibleButton("##rs_dd_hit_" + id, new Vector2(width, height)))
        {
            if (isOpen) ImGui.CloseCurrentPopup();
            else        ImGui.OpenPopup(popupId);
        }
        state.HoverTarget = ImGui.IsItemHovered() ? 1f : 0f;

        // Animate open progress. Snap to 0 when closed so re-opening restarts crisply from the top rather than picking up mid-way.
        state.Value = isOpen ? StepTo(state.Value, 1f, dt, 26f) : 0f;

        var changed = false;
        if (isOpen)
        {
            var itemPad   = S(12f);
            var itemH     = ImGui.GetTextLineHeight() + itemPad;
            var winPad    = Sv(4f, 4f);
            var itemSpace = new Vector2(0f, S(2f));
            // Cap the popup at MaxDropdownRows. When there are more items than the cap, the popup keeps that height and ImGui's own vertical scrollbar handles the overflow (styled below).
            const int MaxDropdownRows = 5;
            var visibleRows = Math.Min(options.Count, MaxDropdownRows);
            var visibleH    = visibleRows * (itemH + itemSpace.Y) - itemSpace.Y + winPad.Y * 2f;
            var slideMax  = S(10f);
            var slideY    = slideMax * (1f - state.Value);
            var popupX    = start.X;
            var popupY    = end.Y + S(4f) + slideY;

            ImGui.SetNextWindowPos(new Vector2(popupX, popupY));
            ImGui.SetNextWindowSize(new Vector2(width, visibleH));
            // Alpha fade - SetNextWindowBgAlpha covers just the popup bg
            ImGui.SetNextWindowBgAlpha(state.Value);
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, state.Value);

            ImGui.PushStyleColor(ImGuiCol.PopupBg,             PackV4(RsTheme.BgSecondary));
            ImGui.PushStyleColor(ImGuiCol.Border,              PackV4(RsTheme.Border));
            ImGui.PushStyleColor(ImGuiCol.Header,              PackV4(RsTheme.AccentPrimary));
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered,       PackV4(RsTheme.BgHover));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive,        PackV4(RsTheme.AccentPrimary));
            ImGui.PushStyleColor(ImGuiCol.Text,                PackV4(RsTheme.TextPrimary));
            // Match the overflow scrollbar to the panel look - otherwise the default grey rail clashes with the themed popup.
            ImGui.PushStyleColor(ImGuiCol.ScrollbarBg,          PackV4(new Vector4(0f, 0f, 0f, 0f)));
            ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab,        PackV4(RsTheme.Border));
            ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, PackV4(RsTheme.BgHover));
            ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive,  PackV4(RsTheme.AccentPrimary));
            ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding,     radius);
            ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize,   RsTheme.BorderThickness);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,     winPad);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,       itemSpace);
            ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize,     S(8f));
            ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, S(4f));

            if (ImGui.BeginPopup(popupId, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings))
            {
                var tips = _dropdownTooltips;
                for (var i = 0; i < options.Count; i++)
                {
                    var isSel = i == selected;
                    if (ImGui.Selectable(options[i] + "##rs_dd_opt_" + id + "_" + i, isSel, ImGuiSelectableFlags.None, new Vector2(0f, itemH)))
                    {
                        selected = i;
                        changed = true;
                    }
                    // Auto-scroll the current selection into view on open so "value already 20 items in" doesn't leave the user at the top of a scrolled list.
                    if (isSel)
                    {
                        ImGui.SetItemDefaultFocus();
                        if (state.Value < 0.15f) ImGui.SetScrollHereY(0.5f);
                    }
                    if (tips != null && i < tips.Count && !string.IsNullOrEmpty(tips[i]) && ImGui.IsItemHovered())
                        ImGui.SetTooltip(tips[i]);
                }
                ImGui.EndPopup();
            }

            ImGui.PopStyleVar(6);
            ImGui.PopStyleColor(10);
            ImGui.PopStyleVar();
        }
        return changed;
    }

    public static bool Toggle(string id, ref bool value, string? label = null)
    {
        var width  = S(44f);
        var height = S(22f);
        var key = ImGui.GetID("rs_tgl_" + id);
        var state = Get(key);
        var dt = ImGui.GetIO().DeltaTime;
        state.Value = StepTo(state.Value, value ? 1f : 0f, dt, 14f);
        state.Hover = StepTo(state.Hover, state.HoverTarget, dt);

        var start = ImGui.GetCursorScreenPos();
        var end = start + new Vector2(width, height);
        var draw = ImGui.GetWindowDrawList();

        var track = LerpV4(RsTheme.BgTertiary, RsTheme.AccentPrimary, state.Value);
        if (state.Hover > 0.01f)
            track = LerpV4(track, Brighten(track, 0.06f), state.Hover);
        draw.AddRectFilled(start, end, PackV4(track), height * 0.5f);

        var knobInset  = S(3f);
        var knobRadius = height * 0.5f - knobInset;
        var knobMinX = start.X + knobInset + knobRadius;
        var knobMaxX = end.X   - knobInset - knobRadius;
        var knobX = knobMinX + (knobMaxX - knobMinX) * state.Value;
        var knobY = start.Y + height * 0.5f;
        // subtle drop shadow under the knob
        draw.AddCircleFilled(new Vector2(knobX, knobY + S(1f)), knobRadius, PackV4(new Vector4(0, 0, 0, 0.35f)));
        draw.AddCircleFilled(new Vector2(knobX, knobY),         knobRadius, PackV4(new Vector4(1f, 1f, 1f, 1f)));

        ImGui.SetCursorScreenPos(start);
        var hit = new Vector2(width, height);
        var clicked = ImGui.InvisibleButton("##rs_tgl_hit_" + id, hit);
        if (clicked) value = !value;
        state.HoverTarget = ImGui.IsItemHovered() ? 1f : 0f;

        if (!string.IsNullOrEmpty(label))
        {
            ImGui.SameLine(0f, S(10f));
            // Vertically center the label with the switch track.
            var lineH = ImGui.GetTextLineHeight();
            var y = ImGui.GetCursorPosY();
            ImGui.SetCursorPosY(y + (height - lineH) * 0.5f);
            ImGui.PushStyleColor(ImGuiCol.Text, PackV4(RsTheme.TextPrimary));
            ImGui.TextUnformatted(DisplayLabel(label));
            ImGui.PopStyleColor();
        }
        return clicked;
    }

    public static bool Checkbox(string label, ref bool value)
    {
        var box = S(18f);
        var gap = S(8f);
        var key = ImGui.GetID("rs_chk_" + label);
        var state = Get(key);
        var dt = ImGui.GetIO().DeltaTime;
        state.Value = StepTo(state.Value, value ? 1f : 0f, dt, 16f);
        state.Hover = StepTo(state.Hover, state.HoverTarget, dt);

        var draw = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var boxMin = start;
        var boxMax = start + new Vector2(box, box);

        var bg = LerpV4(RsTheme.BgTertiary, RsTheme.AccentPrimary, state.Value);
        if (state.Hover > 0.01f && state.Value < 0.99f)
            bg = LerpV4(bg, RsTheme.BgHover, state.Hover);
        var border = LerpV4(RsTheme.Border, RsTheme.AccentPrimary, Math.Max(state.Value, state.Hover * 0.7f));

        var radius = S(4f);
        draw.AddRectFilled(boxMin, boxMax, PackV4(bg), radius);
        draw.AddRect(boxMin, boxMax, PackV4(border), radius, ImDrawFlags.None, RsTheme.BorderThickness);

        if (state.Value > 0.05f)
        {
            var checkColor = new Vector4(1f, 1f, 1f, state.Value);
            // Simple two-segment checkmark, scaled inside the box.
            var p1 = new Vector2(boxMin.X + box * 0.22f, boxMin.Y + box * 0.52f);
            var p2 = new Vector2(boxMin.X + box * 0.44f, boxMin.Y + box * 0.72f);
            var p3 = new Vector2(boxMin.X + box * 0.78f, boxMin.Y + box * 0.30f);
            draw.AddLine(p1, p2, PackV4(checkColor), S(2f));
            draw.AddLine(p2, p3, PackV4(checkColor), S(2f));
        }

        var display = DisplayLabel(label);
        var labelSize = ImGui.CalcTextSize(display);
        var textPos = new Vector2(boxMax.X + gap, boxMin.Y + (box - labelSize.Y) * 0.5f);
        draw.AddText(textPos, PackV4(RsTheme.TextPrimary), display);

        var hit = new Vector2(box + gap + labelSize.X, box);
        ImGui.SetCursorScreenPos(start);
        var clicked = ImGui.InvisibleButton("##rs_chk_hit_" + label, hit);
        if (clicked) value = !value;
        state.HoverTarget = ImGui.IsItemHovered() ? 1f : 0f;
        return clicked;
    }

    public static bool Radio(string label, ref int selected, int value)
    {
        var outer = S(18f);
        var gap   = S(8f);
        var key = ImGui.GetID("rs_radio_" + label);
        var state = Get(key);
        var dt = ImGui.GetIO().DeltaTime;
        state.Value = StepTo(state.Value, selected == value ? 1f : 0f, dt, 16f);
        state.Hover = StepTo(state.Hover, state.HoverTarget, dt);

        var draw = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var center = new Vector2(start.X + outer * 0.5f, start.Y + outer * 0.5f);

        var ring = LerpV4(RsTheme.Border, RsTheme.AccentPrimary, Math.Max(state.Value, state.Hover * 0.7f));
        var bg   = LerpV4(RsTheme.BgTertiary, RsTheme.BgHover,   state.Hover);
        draw.AddCircleFilled(center, outer * 0.5f, PackV4(bg));
        draw.AddCircle(center, outer * 0.5f, PackV4(ring), 0, S(1.5f));

        if (state.Value > 0.05f)
        {
            var inner = outer * 0.28f * state.Value;
            draw.AddCircleFilled(center, inner, PackV4(RsTheme.AccentPrimary));
        }

        var display = DisplayLabel(label);
        var labelSize = ImGui.CalcTextSize(display);
        var textPos = new Vector2(start.X + outer + gap, start.Y + (outer - labelSize.Y) * 0.5f);
        draw.AddText(textPos, PackV4(RsTheme.TextPrimary), display);

        var hit = new Vector2(outer + gap + labelSize.X, outer);
        ImGui.SetCursorScreenPos(start);
        var clicked = ImGui.InvisibleButton("##rs_radio_hit_" + label, hit);
        if (clicked && selected != value)
        {
            selected = value;
            return true;
        }
        state.HoverTarget = ImGui.IsItemHovered() ? 1f : 0f;
        return false;
    }

    public readonly struct NavItem
    {
        public readonly FontAwesomeIcon Icon;
        public readonly string Text;
        public NavItem(FontAwesomeIcon icon, string text)
        {
            Icon = icon;
            Text = text;
        }
    }


    public enum NavOrientation
    {
 
        Horizontal,

        Vertical,
    }
    private sealed class NavState
    {
        public float IndicatorPos;
        public float IndicatorSize;
        public bool  Initialized;
        // Drag-to-reorder: which item is held, where the press started, and whether it has moved far enough to count as a drag.
        public int     DragIndex = -1;
        public Vector2 DragStart;
        public bool    Dragging;
        public int     DropTarget = -1;
    }

    private static readonly Dictionary<uint, NavState> NavStates = new();

    private static NavState GetNav(uint id)
    {
        if (!NavStates.TryGetValue(id, out var s)) NavStates[id] = s = new NavState();
        return s;
    }

    private static bool DrawCloseGlyph(ImDrawListPtr draw, string id, int i, Vector2 min, float size, bool tabHovered)
    {
        var max = min + new Vector2(size, size);
        ImGui.SetItemAllowOverlap();
        ImGui.SetCursorScreenPos(min);
        var clicked = ImGui.InvisibleButton("##rs_nav_close_" + id + "_" + i, new Vector2(size, size));
        var hovered = ImGui.IsItemHovered();
        if (!(tabHovered || hovered)) return clicked;

        if (hovered)
            draw.AddCircleFilled((min + max) * 0.5f, size * 0.5f, PackV4(RsTheme.AccentDanger));
        var col = PackV4(hovered ? RsTheme.TextPrimary : RsTheme.TextSecondary);
        var inset = size * 0.30f;
        var t = System.Math.Max(1f, S(1.5f));
        draw.AddLine(new Vector2(min.X + inset, min.Y + inset), new Vector2(max.X - inset, max.Y - inset), col, t);
        draw.AddLine(new Vector2(max.X - inset, min.Y + inset), new Vector2(min.X + inset, max.Y - inset), col, t);
        return clicked;
    }

    private static bool DrawArrowGlyph(ImDrawListPtr draw, string id, int i, Vector2 min, float size, bool tabHovered, bool pointRight)
    {
        var max = min + new Vector2(size, size);
        ImGui.SetItemAllowOverlap();
        ImGui.SetCursorScreenPos(min);
        var clicked = ImGui.InvisibleButton("##rs_nav_arrow_" + id + "_" + i, new Vector2(size, size));
        var hovered = ImGui.IsItemHovered();
        if (!(tabHovered || hovered)) return clicked;

        if (hovered)
            draw.AddCircleFilled((min + max) * 0.5f, size * 0.5f, PackV4(RsTheme.BgHover));
        var col = PackV4(hovered ? RsTheme.TextPrimary : RsTheme.TextSecondary);
        var t = System.Math.Max(1f, S(1.5f));
        var midY = (min.Y + max.Y) * 0.5f;
        var inset = size * 0.28f;
        if (pointRight)
        {
            var tip = new Vector2(max.X - inset, midY);
            var top = new Vector2(min.X + inset, min.Y + inset);
            var bot = new Vector2(min.X + inset, max.Y - inset);
            draw.AddLine(top, tip, col, t);
            draw.AddLine(bot, tip, col, t);
        }
        else
        {
            var tip = new Vector2(min.X + inset, midY);
            var top = new Vector2(max.X - inset, min.Y + inset);
            var bot = new Vector2(max.X - inset, max.Y - inset);
            draw.AddLine(top, tip, col, t);
            draw.AddLine(bot, tip, col, t);
        }
        return clicked;
    }

    public static bool NavigationMenu(string id, ref int selected, IReadOnlyList<NavItem> items,
                                      NavOrientation orientation = NavOrientation.Horizontal,
                                      System.Action<int>? onClose = null,
                                      System.Action<int, int>? onReorder = null)
    {
        if (items.Count == 0) return false;

        var horizontal = orientation == NavOrientation.Horizontal;
        var pad        = Sv(14f, 10f);
        var iconGap    = S(8f);
        var itemGap    = S(4f);
        var indicator  = S(3f);
        var radius     = S(6f);
        var dt         = ImGui.GetIO().DeltaTime;
        var draw       = ImGui.GetWindowDrawList();
        var start      = ImGui.GetCursorScreenPos();
        var nav        = GetNav(ImGui.GetID("rs_nav_" + id));

        // Reserve room for the close X + optional reorder chevrons.
        var closeSize = S(14f);
        var slotButtons = onClose != null ? 1 : 0;   // reordering is by drag, so no chevrons
        var closeSlot = slotButtons > 0
            ? closeSize * slotButtons + S(4f) * (slotButtons - 1) + S(6f)
            : 0f;

        var sizes = new Vector2[items.Count];
        float maxW = 0f, maxH = 0f;
        for (var i = 0; i < items.Count; i++)
        {
            Vector2 iconSz;
            var glyph = items[i].Icon.ToIconString();
            using (RsIcons.Push()) iconSz = ImGui.CalcTextSize(glyph);
            var textSz = ImGui.CalcTextSize(items[i].Text);
            sizes[i] = new Vector2(
                iconSz.X + iconGap + textSz.X + pad.X * 2f + closeSlot,
                Math.Max(iconSz.Y, textSz.Y) + pad.Y * 2f);
            if (sizes[i].X > maxW) maxW = sizes[i].X;
            if (sizes[i].Y > maxH) maxH = sizes[i].Y;
        }

        var offsets = new float[items.Count];
        var flow = 0f;
        for (var i = 0; i < items.Count; i++)
        {
            offsets[i] = flow;
            flow += (horizontal ? sizes[i].X : maxH) + itemGap;
        }
        var totalMain  = flow - itemGap;
        var totalCross = horizontal ? maxH : maxW;

        var changed = false;
        var io = ImGui.GetIO();
        var dragThreshold = S(6f);
        // A held item that moved past the threshold is being dragged; work out which slot the pointer is over so the others can slide aside.
        if (onReorder != null && nav.DragIndex >= 0)
        {
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                if (nav.Dragging && nav.DropTarget >= 0 && nav.DropTarget != nav.DragIndex && nav.DragIndex < items.Count)
                    onReorder(nav.DragIndex, nav.DropTarget);
                nav.DragIndex = -1; nav.Dragging = false; nav.DropTarget = -1;
            }
            else
            {
                var d = io.MousePos - nav.DragStart;
                if (!nav.Dragging && (horizontal ? MathF.Abs(d.X) : MathF.Abs(d.Y)) > dragThreshold) nav.Dragging = true;
                if (nav.Dragging)
                {
                    var m = horizontal ? io.MousePos.X - start.X : io.MousePos.Y - start.Y;
                    var target = items.Count - 1;
                    for (var k = 0; k < items.Count; k++)
                    {
                        var mid = offsets[k] + (horizontal ? sizes[k].X : maxH) * 0.5f;
                        if (m < mid) { target = k; break; }
                    }
                    nav.DropTarget = Math.Clamp(target, 0, items.Count - 1);
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }
            }
        }
        if (nav.DragIndex >= items.Count) { nav.DragIndex = -1; nav.Dragging = false; }
        var dragging = nav.Dragging && nav.DragIndex >= 0;

        for (var i = 0; i < items.Count; i++)
        {
            var itemSize = horizontal
                ? new Vector2(sizes[i].X, maxH)
                : new Vector2(maxW,       maxH);
            var itemMin = horizontal
                ? new Vector2(start.X + offsets[i], start.Y)
                : new Vector2(start.X,              start.Y + offsets[i]);
            // While dragging, items between the held one and the drop slot slide over to show where it will land; the held one rides the pointer.
            if (dragging)
            {
                var span = horizontal ? sizes[nav.DragIndex].X + itemGap : maxH + itemGap;
                if (i == nav.DragIndex)
                {
                    var delta = io.MousePos - nav.DragStart;
                    itemMin += horizontal ? new Vector2(delta.X, 0f) : new Vector2(0f, delta.Y);
                }
                else if (nav.DropTarget > nav.DragIndex && i > nav.DragIndex && i <= nav.DropTarget)
                    itemMin -= horizontal ? new Vector2(span, 0f) : new Vector2(0f, span);
                else if (nav.DropTarget < nav.DragIndex && i >= nav.DropTarget && i < nav.DragIndex)
                    itemMin += horizontal ? new Vector2(span, 0f) : new Vector2(0f, span);
            }
            var itemMax = itemMin + itemSize;

            var iState = Get(ImGui.GetID("rs_nav_it_" + id + "_" + i));
            iState.Hover = StepTo(iState.Hover, iState.HoverTarget, dt);

            var isSel = i == selected;
            // Per-item selection animator, eased so the highlight doesn't snap.
            iState.Value = StepTo(iState.Value, isSel ? 1f : 0f, dt, 10f);

            // Every item - selected, hovered, or idle - gets the same tab shape: rounded corners on top, flat bottom flush with the content beneath. Horizontal nav bars are tabs; vertical nav bars are pills, so keep uniform rounding there.
            var cornerFlags = horizontal
                ? (ImDrawFlags.RoundCornersTopLeft | ImDrawFlags.RoundCornersTopRight)
                : ImDrawFlags.RoundCornersAll;

            // Base plate: black at 0.6 alpha under every tab so the strip reads as one solid nav rail even before hover.
            var basePlate = new Vector4(0f, 0f, 0f, 0.6f);
            draw.AddRectFilled(itemMin, itemMax, PackV4(basePlate), radius, cornerFlags);

            // Overlay: selected -> accent tint. Hover -> hover tint. Both layer on top of the base plate so the tab shape stays identical across states - only the color shifts.
            if (isSel || iState.Hover > 0.01f)
            {
                var selTint = new Vector4(RsTheme.AccentPrimary.X, RsTheme.AccentPrimary.Y, RsTheme.AccentPrimary.Z, 0.16f);
                var hovTint = new Vector4(RsTheme.BgHover.X,       RsTheme.BgHover.Y,       RsTheme.BgHover.Z,       0.55f);
                var bg = isSel
                    ? LerpV4(selTint, hovTint, iState.Hover * 0.35f)
                    : new Vector4(hovTint.X, hovTint.Y, hovTint.Z, hovTint.W * iState.Hover);
                draw.AddRectFilled(itemMin, itemMax, PackV4(bg), radius, cornerFlags);
            }

            // Icon + label.
            var glyph = items[i].Icon.ToIconString();
            Vector2 iconSz;
            using (RsIcons.Push()) iconSz = ImGui.CalcTextSize(glyph);
            var textSz = ImGui.CalcTextSize(items[i].Text);

            var color = isSel
                ? RsTheme.TextPrimary
                : LerpV4(RsTheme.TextSecondary, RsTheme.TextPrimary, iState.Hover);

            var iconPos = new Vector2(
                itemMin.X + pad.X,
                itemMin.Y + (itemSize.Y - iconSz.Y) * 0.5f);
            var textPos = new Vector2(
                iconPos.X + iconSz.X + iconGap,
                itemMin.Y + (itemSize.Y - textSz.Y) * 0.5f);
            using (RsIcons.Push())
                draw.AddText(iconPos, PackV4(RsTheme.TextPrimary), glyph);
            draw.AddText(textPos, PackV4(color), items[i].Text);

            if (dragging && i == nav.DragIndex)
                draw.AddRect(itemMin, itemMax, PackV4(RsTheme.AccentPrimary), radius, cornerFlags, 1.5f);

            ImGui.SetCursorScreenPos(itemMin);
            if (ImGui.InvisibleButton("##rs_nav_hit_" + id + "_" + i, itemSize))
            {
                // A release after a drag is a drop, not a click.
                if (selected != i && !nav.Dragging && nav.DragIndex < 0)
                {
                    selected = i;
                    changed = true;
                }
            }
            var tabHovered = ImGui.IsItemHovered();
            if (onReorder != null && ImGui.IsItemActivated() && nav.DragIndex < 0)
            {
                nav.DragIndex = i; nav.DragStart = io.MousePos; nav.Dragging = false; nav.DropTarget = i;
            }
            if (onReorder != null && tabHovered && !dragging && items.Count > 1) ImGui.SetTooltip("Drag to reorder");
            iState.HoverTarget = tabHovered ? 1f : 0f;

            if (onClose != null || onReorder != null)
            {
                var edgePad = S(5f);
                var slotY = itemMin.Y + edgePad;
                var rightX = itemMax.X - edgePad;

                if (onClose != null)
                {
                    var xMin = new Vector2(rightX - closeSize, slotY);
                    if (DrawCloseGlyph(draw, id, i, xMin, closeSize, tabHovered))
                        onClose(i);
                }
            }
        }

        // Sliding accent
        if (selected >= 0 && selected < items.Count)
        {
            var targetSize = horizontal ? sizes[selected].X : maxH;
            var targetPos  = offsets[selected];
            if (!nav.Initialized)
            {
                nav.IndicatorPos  = targetPos;
                nav.IndicatorSize = targetSize;
                nav.Initialized   = true;
            }
            else
            {
                nav.IndicatorPos  = StepTo(nav.IndicatorPos,  targetPos,  dt, 22f);
                nav.IndicatorSize = StepTo(nav.IndicatorSize, targetSize, dt, 22f);
            }

            var accent = PackV4(RsTheme.AccentPrimary);
            if (horizontal)
            {
                // Underline flush with the bottom
                var y  = start.Y + maxH - indicator;
                var x1 = start.X + nav.IndicatorPos;
                var x2 = x1 + nav.IndicatorSize;
                draw.AddRectFilled(new Vector2(x1, y), new Vector2(x2, y + indicator), accent, indicator * 0.5f);
            }
            else
            {
                // Vertical bar hugging the left edge.
                var x  = start.X;
                var y1 = start.Y + nav.IndicatorPos;
                var y2 = y1 + nav.IndicatorSize;
                draw.AddRectFilled(new Vector2(x, y1), new Vector2(x + indicator, y2), accent, indicator * 0.5f);
            }
        }
        var totalW = horizontal ? totalMain : totalCross;
        var totalH = horizontal ? totalCross : totalMain;
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + totalH));
        ImGui.Dummy(new Vector2(totalW, 0f));
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + totalH));

        return changed;
    }


    private sealed class CollapsibleState
    {
        public float Open;
        public float NaturalH;
    }

    private static readonly Dictionary<uint, CollapsibleState> CollapsibleStates = new();

    private static CollapsibleState GetCollapsible(uint id)
    {
        if (!CollapsibleStates.TryGetValue(id, out var s)) CollapsibleStates[id] = s = new CollapsibleState();
        return s;
    }

    // Push/pop stack for BeginCollapsible so EndCollapsible knows what to close.
    private readonly struct CollapsibleFrame
    {
        public readonly uint Key;
        public readonly Vector2 ContentStart;
        public readonly float AnimatedH;
        public readonly float AvailW;
        public readonly bool Rendering;
        public readonly float OuterX;   // parent-window X we were called at
        public readonly float Pad;      // inset used on both sides of the body
        public readonly bool UsedChild; // true when body was wrapped in BeginChild for real clipping
        public CollapsibleFrame(uint k, Vector2 s, float h, float w, bool r, float outerX, float pad, bool usedChild)
        { Key = k; ContentStart = s; AnimatedH = h; AvailW = w; Rendering = r; OuterX = outerX; Pad = pad; UsedChild = usedChild; }
    }

    private static readonly Stack<CollapsibleFrame> CollapsibleStack = new();

    // Right-side inset that widgets should reserve inside the current Rs container. Each Begin* pushes an ACCUMULATED total (previous + this container's pad), so a widget inside nested panels sees the full stacked inset (panel_pad + collapsible_pad + ...). Callers must Pop the same value at End* time.
    private static readonly Stack<float> ContentRightReserve = new();

    internal static float CurrentRightReserve()
        => ContentRightReserve.Count > 0 ? ContentRightReserve.Peek() : 0f;

    // Adds `delta` to the running total and pushes the new total.
    internal static void PushRightReserve(float delta)
        => ContentRightReserve.Push(CurrentRightReserve() + delta);

    internal static void PopRightReserve()
    {
        if (ContentRightReserve.Count > 0) ContentRightReserve.Pop();
    }

    // The horizontal space widgets can safely use before running past the inset right edge of the enclosing panel/collapsible. Callers that lay out their own rows (e.g. name + description + remove button) should use this instead of ImGui.GetContentRegionAvail() so the trailing widget stays inside the panel, not clipped.
    public static float AvailContentWidth()
        => Math.Max(0f, ImGui.GetContentRegionAvail().X - CurrentRightReserve());

    // Clickable header + eased-open body. Returns true if body should draw.
    public static bool BeginCollapsible(string id, string title, ref bool open)
    {
        var key = ImGui.GetID("rs_col_" + id);
        var state = GetCollapsible(key);
        var dt = ImGui.GetIO().DeltaTime;
        // First-ever open (no NaturalH yet) needs to render at full height to measure. Snap open so the animated pass doesn't run without a valid height - nothing to lerp toward until we've measured once.
        if (open && state.NaturalH < 0.5f) state.Open = 1f;
        state.Open = StepTo(state.Open, open ? 1f : 0f, dt, 14f);
        // StepTo is exponential decay - on close it goes 1 -> 0.8 -> 0.64 -> ..., never reaching 0. The tail frames render a thin sliver of content's top edge before the < 0.001 skip finally fires, which reads as a brief "flash-back" of the panel as it closes. Snap fully shut once the visible slice is smaller than something a person could distinguish from noise.
        if (!open && state.Open > 0f && state.NaturalH * state.Open < S(6f))
            state.Open = 0f;

        var draw = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        // Also inset the HEADER itself from the panel container's edges so it doesn't hug the border. Both sides.
        var headerInset = S(8f);
        var availW = Math.Max(S(120f), ImGui.GetContentRegionAvail().X - headerInset * 2f);
        var headerPad = Sv(10f, 8f);
        var headerH = ImGui.GetTextLineHeight() + headerPad.Y * 2f;
        var headerMin = new Vector2(start.X + headerInset, start.Y);
        var headerMax = new Vector2(headerMin.X + availW, start.Y + headerH);

        var hoverKey = ImGui.GetID("rs_col_head_" + id);
        var hState = Get(hoverKey);
        hState.Hover = StepTo(hState.Hover, hState.HoverTarget, dt);

        var bg = LerpV4(RsTheme.BgTertiary, RsTheme.BgHover, hState.Hover);
        draw.AddRectFilled(headerMin, headerMax, PackV4(bg), S(6f));

        // Chevron rotates by picking a different glyph. Cheap and works.
        var chevIcon = state.Open > 0.5f ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronRight;
        var chevStr = chevIcon.ToIconString();
        Vector2 chevSize;
        using (RsIcons.Push()) chevSize = ImGui.CalcTextSize(chevStr);
        var chevPos = new Vector2(headerMin.X + headerPad.X, start.Y + (headerH - chevSize.Y) * 0.5f);
        using (RsIcons.Push()) draw.AddText(chevPos, PackV4(RsTheme.TextSecondary), chevStr);

        var titleSize = ImGui.CalcTextSize(title);
        var titlePos = new Vector2(chevPos.X + chevSize.X + S(10f), start.Y + (headerH - titleSize.Y) * 0.5f);
        draw.AddText(titlePos, PackV4(RsTheme.TextPrimary), title);

        ImGui.SetCursorScreenPos(headerMin);
        if (ImGui.InvisibleButton("##rs_col_hit_" + id, new Vector2(availW, headerH)))
            open = !open;
        hState.HoverTarget = ImGui.IsItemHovered() ? 1f : 0f;

        // Inset the body on both sides.
        var pad = S(12f);
        var innerW = Math.Max(S(60f), availW - pad * 2f);
        var contentStart = new Vector2(start.X + pad, headerMax.Y + S(8f));

        // Fully closed AND animation done - skip the body entirely and park the cursor right below the header. No wasted space.
        if (!open && state.Open < 0.001f)
        {
            ImGui.SetCursorScreenPos(new Vector2(start.X, headerMax.Y + S(4f)));
            CollapsibleStack.Push(new CollapsibleFrame(key, contentStart, 0f, innerW, false, start.X, pad, false));
            return false;
        }

        // Grow / shrink animation. Content is laid out at its NATURAL height (measured on EndCollapsible into state.NaturalH), then one of two rendering paths keeps the visible box honest: Mid-animation -> wrap the body in an ImGui BeginChild sized to animatedH. Draw-list PushClipRect alone was not enough: widgets like InputTextMultiline create their OWN inner child window whose draw list ignores our clip, so their text used to bleed past the collapsing panel. A real BeginChild pushes ImGui's own window clip, which cascades into any grandchild windows those widgets spawn. Fully open -> skip the child. The full-height content lays out and BeginGroup measures the true natural height for the next animation cycle.
        var animatedH = state.NaturalH * state.Open;
        bool animating = state.Open > 0.001f && state.Open < 0.999f && state.NaturalH > 0.5f;

        // Lay out content at inset X. Indent shrinks GetContentRegionAvail on the left; ContentRightReserve (read by RsElements widgets) shrinks it on the right so trailing buttons stay inside.
        ImGui.SetCursorScreenPos(contentStart);

        if (animating)
        {
            // Child provides real clipping for any grandchild windows. No scrollbar / mouse-wheel - we're animating, not scrolling. NoBackground so the panel's own fill stays visible under it.
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
            ImGui.BeginChild(
                "##rs_col_body_" + id,
                new Vector2(innerW, animatedH),
                false,
                ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoScrollWithMouse
                | ImGuiWindowFlags.NoBackground);
            ImGui.PopStyleVar();
        }
        else
        {
            // Keep the visual clip for the same-frame edge cases (borders and hover rings on inset widgets that draw a hair past the right edge). Cheap and matches the old behavior.
            var clipMin = new Vector2(contentStart.X - pad, contentStart.Y);
            var clipMax = new Vector2(contentStart.X + innerW + pad, contentStart.Y + Math.Max(animatedH, 1f));
            ImGui.GetWindowDrawList().PushClipRect(clipMin, clipMax, true);
        }

        ImGui.Indent(pad);
        ImGui.PushItemWidth(Math.Max(S(60f), innerW - pad * 2f));
        PushRightReserve(pad);
        ImGui.BeginGroup();

        CollapsibleStack.Push(new CollapsibleFrame(key, contentStart, animatedH, innerW, true, start.X, pad, animating));
        return true;
    }

    public static void EndCollapsible()
    {
        if (CollapsibleStack.Count == 0) return;
        var frame = CollapsibleStack.Pop();

        if (frame.Rendering)
        {
            ImGui.EndGroup();
            // Group's rect gives us the true natural content height - stash it so next frame's animation lerps to the right target. Measurement still works inside a BeginChild: GetItemRectSize returns the group's laid-out size, not the clipped-visible slice.
            var groupSize = ImGui.GetItemRectSize();
            var natural = groupSize.Y + S(4f);
            var state = GetCollapsible(frame.Key);
            if (natural > 0.5f) state.NaturalH = natural;

            PopRightReserve();
            ImGui.PopItemWidth();
            ImGui.Unindent(frame.Pad);

            if (frame.UsedChild)
            {
                ImGui.EndChild();
            }
            else
            {
                ImGui.GetWindowDrawList().PopClipRect();
            }

            // Force the parent cursor to just past the ANIMATED (visible) portion, not the natural content height. That's what makes sibling widgets slide down smoothly as the panel grows in.
            ImGui.SetCursorScreenPos(new Vector2(
                frame.OuterX,
                frame.ContentStart.Y + frame.AnimatedH + S(4f)));
        }
        else
        {
            // Not rendering - just make sure cursor.X is back at outer X.
            var cur = ImGui.GetCursorScreenPos();
            ImGui.SetCursorScreenPos(new Vector2(frame.OuterX, cur.Y));
        }
    }
    private sealed class PanelFrame
    {
        public bool FitAxis;      // any axis fitting - takes the group+channels path
        public bool FitX;
        public bool FitY;
        public float FixedW;      // width to use when FitX is false
        public float FixedH;      // height to use when FitY is false
        public float Pad;         // inner padding actually used at Begin, so End matches
        public Vector2 Start;
        public bool HasTitle;
        public float TitleBottomY;
    }

    private static readonly Stack<PanelFrame> PanelStack = new();


    public static bool BeginPanel(string id, string? title = null, Vector2 size = default,
                                  bool fitContentsX = false, bool fitContentsY = false,
                                  float? innerPadding = null)
    {

        var pad = innerPadding.HasValue ? S(innerPadding.Value) : RsTheme.PanelInnerPadding;
        if (fitContentsX || fitContentsY)
        {
            var start = ImGui.GetCursorScreenPos();
            var avail = ImGui.GetContentRegionAvail();

            var draw = ImGui.GetWindowDrawList();
            draw.ChannelsSplit(2);
            draw.ChannelsSetCurrent(1); // content goes on the foreground channel

            var frame = new PanelFrame
            {
                FitAxis = true,
                FitX = fitContentsX,
                FitY = fitContentsY,
                Pad = pad,
                Start = start,
                FixedW = fitContentsX ? 0f : (size.X > 0f ? size.X : avail.X),
                FixedH = fitContentsY ? 0f : (size.Y > 0f ? size.Y : avail.Y),
            };

            ImGui.SetCursorScreenPos(new Vector2(start.X + pad, start.Y + pad));
            ImGui.BeginGroup();

            if (!string.IsNullOrEmpty(title))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, PackV4(RsTheme.TextPrimary));
                ImGui.TextUnformatted(DisplayLabel(title));
                ImGui.PopStyleColor();
                frame.HasTitle = true;
                frame.TitleBottomY = ImGui.GetItemRectMax().Y;

                ImGui.Dummy(new Vector2(0f, S(11f)));
            }

            // Reserve room on the right so widgets inside can subtract pad from GetContentRegionAvail. Fit-contents panels don't have a child window that naturally clips the region, so without this the content would run flush to the parent window's right edge.
            PushRightReserve(pad);

            PanelStack.Push(frame);
            return true;
        }

        // Fixed-size / fill child-window path. Slightly translucent so the profile background image/video shows through the panel surface.
        ImGui.PushStyleColor(ImGuiCol.ChildBg, PackV4(RsTheme.PanelSurface));
        ImGui.PushStyleColor(ImGuiCol.Border,  PackV4(RsTheme.Border));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding,   S(8f));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, RsTheme.BorderThickness);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,   new Vector2(pad, pad));

        var opened = ImGui.BeginChild("##rs_panel_" + id, size, true, ImGuiWindowFlags.None);
        if (opened && !string.IsNullOrEmpty(title))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, PackV4(RsTheme.TextPrimary));
            ImGui.TextUnformatted(DisplayLabel(title));
            ImGui.PopStyleColor();
            // Divider that spans the padded content region.
            var draw = ImGui.GetWindowDrawList();
            var y = ImGui.GetCursorScreenPos().Y + S(3f);
            var winPos = ImGui.GetWindowPos();
            var winW = ImGui.GetWindowWidth();
            draw.AddLine(
                new Vector2(winPos.X + pad, y),
                new Vector2(winPos.X + winW - pad, y),
                PackV4(RsTheme.Border), RsTheme.BorderThickness);
            ImGui.Dummy(new Vector2(0f, S(8f)));
        }

        // Even a fixed-size child that has WindowPadding on both sides needs the reserve on top so RsElements widgets know to leave room for a trailing item on the same line.
        PushRightReserve(pad);

        PanelStack.Push(new PanelFrame { FitAxis = false, Pad = pad });
        return opened;
    }

    public static void EndPanel()
    {
        var frame = PanelStack.Pop();
        // Balance the PushRightReserve done in BeginPanel.
        PopRightReserve();
        if (frame.FitAxis)
        {
            ImGui.EndGroup();
            var contentMax = ImGui.GetItemRectMax();
            var pad = frame.Pad;

            var panelMin = frame.Start;
            // Fit axes wrap to measured extent and pad
            var panelMax = new Vector2(
                frame.FitX ? contentMax.X + pad : frame.Start.X + frame.FixedW,
                frame.FitY ? contentMax.Y + pad : frame.Start.Y + frame.FixedH);

            var draw = ImGui.GetWindowDrawList();
            draw.ChannelsSetCurrent(0); // background channel - renders under the content

            var radius = S(8f);
            draw.AddRectFilled(panelMin, panelMax, PackV4(RsTheme.PanelSurface), radius);
            draw.AddRect(panelMin, panelMax, PackV4(RsTheme.Border), radius, ImDrawFlags.None, RsTheme.BorderThickness);
            if (frame.HasTitle)
            {
                var y = frame.TitleBottomY + S(3f);
                draw.AddLine(
                    new Vector2(panelMin.X + pad, y),
                    new Vector2(panelMax.X - pad, y),
                    PackV4(RsTheme.Border), RsTheme.BorderThickness);
            }

            draw.ChannelsMerge();

            ImGui.SetCursorScreenPos(new Vector2(panelMin.X, panelMax.Y));
            ImGui.Dummy(new Vector2(panelMax.X - panelMin.X, 0f));
            ImGui.SetCursorScreenPos(new Vector2(panelMin.X, panelMax.Y));
            return;
        }

        ImGui.EndChild();
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(2);
    }
}
