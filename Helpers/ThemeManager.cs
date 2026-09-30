using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using AbsoluteRP.RsUI;

namespace AbsoluteRP.Helpers;

// Shared theming layer. Push/pop around WindowSystem.Draw() to theme every window at once; the widget helpers below render in the RsUI design system.
public static class ThemeManager
{
    public static readonly Vector4 DefaultBorder = RsTheme.DefaultBorder;
    public static readonly Vector4 DefaultBackground = RsTheme.DefaultBackground;
    public static readonly Vector4 DefaultAccent = RsTheme.DefaultAccent;
    public static readonly Vector4 DefaultFont = RsTheme.DefaultFont;

    // Derived colors, refreshed each PushTheme.
    public static Vector4 Border { get; private set; } = RsTheme.Border;
    public static Vector4 Background { get; private set; } = RsTheme.BgPrimary;
    public static Vector4 Accent { get; private set; } = RsTheme.AccentPrimary;
    public static Vector4 Font { get; private set; } = RsTheme.TextPrimary;
    public static Vector4 BgLight { get; private set; } = RsTheme.BgSecondary;
    public static Vector4 BgLighter { get; private set; } = RsTheme.BgSecondary;
    public static Vector4 BgDark { get; private set; } = RsTheme.BgTertiary;
    public static Vector4 AccentHover { get; private set; } = RsTheme.AccentPrimary;
    public static Vector4 AccentActive { get; private set; } = RsTheme.AccentPrimary;
    public static Vector4 AccentMuted { get; private set; } = RsTheme.AccentPrimary;
    public static Vector4 AccentSubtle { get; private set; } = RsTheme.AccentPrimary;
    public static Vector4 FontDim { get; private set; } = RsTheme.TextSecondary;
    public static Vector4 FontMuted { get; private set; } = RsTheme.TextMuted;
    public static Vector4 Success { get; private set; } = RsTheme.AccentSuccess;
    public static Vector4 Warning { get; private set; } = RsTheme.AccentWarning;
    public static Vector4 Error { get; private set; } = RsTheme.AccentDanger;

    private static int pushedColors;
    private static int pushedStyles;

    // Track total pushes including from widgets, so PopTheme can clean up everything.
    private static int totalColorPushes = 0;
    private static int totalStylePushes = 0;

    public static void TrackColorPush(int count = 1) { totalColorPushes += count; }
    public static void TrackColorPop(int count = 1) { totalColorPushes -= count; }
    public static void TrackStylePush(int count = 1) { totalStylePushes += count; }
    public static void TrackStylePop(int count = 1) { totalStylePushes -= count; }
    private static Vector4 FromHex(uint rgb)
    {
        return new Vector4(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f,
            1f);
    }
    public static void PushTheme(Configuration config)
    {
        totalColorPushes = 0;
        totalStylePushes = 0;

        // The same four colours drive the RsUI palette every window draws with.
        RsTheme.Apply(config.ThemeBorder, config.ThemeBackground, config.ThemeAccent, config.ThemeFont);

        // User overrides still apply; defaults are the RsUI palette.
        Border = config.ThemeBorder ?? RsTheme.Border;
        Background = config.ThemeBackground ?? RsTheme.BgPrimary;
        Accent = config.ThemeAccent ?? RsTheme.AccentPrimary;
        Font = config.ThemeFont ?? RsTheme.TextPrimary;

        // Derived tones: exact RsUI swatches when unset, arithmetic when overridden.
        var customBg = config.ThemeBackground.HasValue;
        var customFont = config.ThemeFont.HasValue;
        BgLight = customBg ? Lighten(Background, 0.055f) : RsTheme.BgSecondary;
        BgLighter = customBg ? Lighten(Background, 0.11f) : Lighten(RsTheme.BgSecondary, 0.05f);
        BgDark = customBg ? Darken(Background, 0.035f) : RsTheme.BgTertiary;
        AccentHover = Lighten(Accent, 0.06f);
        AccentActive = Darken(Accent, 0.06f);
        AccentMuted = new Vector4(Accent.X, Accent.Y, Accent.Z, 0.40f);
        AccentSubtle = new Vector4(Accent.X, Accent.Y, Accent.Z, 0.16f);
        FontDim = customFont
            ? new Vector4(Font.X * 0.72f, Font.Y * 0.72f, Font.Z * 0.72f, Font.W)
            : RsTheme.TextSecondary;
        FontMuted = customFont
            ? new Vector4(Font.X * 0.55f, Font.Y * 0.55f, Font.Z * 0.55f, Font.W)
            : RsTheme.TextMuted;
        Success = RsTheme.AccentSuccess;
        Warning = RsTheme.AccentWarning;
        Error = RsTheme.AccentDanger;

        pushedColors = 0;

        // Window
        PushColor(ImGuiCol.WindowBg, Background);
        PushColor(ImGuiCol.ChildBg, new Vector4(0, 0, 0, 0));
        PushColor(ImGuiCol.PopupBg, BgLight);
        PushColor(ImGuiCol.Border, Border);
        PushColor(ImGuiCol.BorderShadow, new Vector4(0, 0, 0, 0));

        // Title bar
        PushColor(ImGuiCol.TitleBg, BgDark);
        PushColor(ImGuiCol.TitleBgActive, BgLight);
        PushColor(ImGuiCol.TitleBgCollapsed, BgDark);

        // Menu bar / scrollbar
        PushColor(ImGuiCol.MenuBarBg, BgLight);
        PushColor(ImGuiCol.ScrollbarBg, new Vector4(Background.X, Background.Y, Background.Z, 0.20f));
        PushColor(ImGuiCol.ScrollbarGrab, BgLighter);
        PushColor(ImGuiCol.ScrollbarGrabHovered, Lighten(BgLighter, 0.06f));
        PushColor(ImGuiCol.ScrollbarGrabActive, Accent);

        // Frames (inputs, checkboxes, combos) - RsUI fields are BgTertiary with a border
        PushColor(ImGuiCol.FrameBg, BgDark);
        PushColor(ImGuiCol.FrameBgHovered, BgLight);
        PushColor(ImGuiCol.FrameBgActive, BgLight);

        // Buttons - RsUI primary: accent fill, brighten on hover, sink on press
        PushColor(ImGuiCol.Button, Accent);
        PushColor(ImGuiCol.ButtonHovered, AccentHover);
        PushColor(ImGuiCol.ButtonActive, AccentActive);

        // Headers / collapsing
        PushColor(ImGuiCol.Header, AccentSubtle);
        PushColor(ImGuiCol.HeaderHovered, AccentMuted);
        PushColor(ImGuiCol.HeaderActive, AccentActive);

        // Tabs
        PushColor(ImGuiCol.Tab, BgDark);
        PushColor(ImGuiCol.TabHovered, AccentMuted);
        PushColor(ImGuiCol.TabActive, Accent);
        PushColor(ImGuiCol.TabUnfocused, Darken(BgDark, 0.02f));
        PushColor(ImGuiCol.TabUnfocusedActive, AccentMuted);

        // Separators
        PushColor(ImGuiCol.Separator, Border);
        PushColor(ImGuiCol.SeparatorHovered, Accent);
        PushColor(ImGuiCol.SeparatorActive, AccentActive);

        // Resize grip
        PushColor(ImGuiCol.ResizeGrip, AccentMuted);
        PushColor(ImGuiCol.ResizeGripHovered, Accent);
        PushColor(ImGuiCol.ResizeGripActive, AccentActive);

        // Check / slider
        PushColor(ImGuiCol.CheckMark, Accent);
        PushColor(ImGuiCol.SliderGrab, Accent);
        PushColor(ImGuiCol.SliderGrabActive, AccentHover);

        // Text
        PushColor(ImGuiCol.Text, Font);
        PushColor(ImGuiCol.TextDisabled, FontMuted);

        // Tables
        PushColor(ImGuiCol.TableHeaderBg, BgLight);
        PushColor(ImGuiCol.TableBorderStrong, Border);
        PushColor(ImGuiCol.TableBorderLight, new Vector4(Border.X, Border.Y, Border.Z, Border.W * 0.4f));
        PushColor(ImGuiCol.TableRowBg, new Vector4(0, 0, 0, 0));
        PushColor(ImGuiCol.TableRowBgAlt, new Vector4(1, 1, 1, 0.018f));

        // Progress / plot
        PushColor(ImGuiCol.PlotHistogram, Accent);
        PushColor(ImGuiCol.PlotHistogramHovered, AccentHover);

        // Nav
        PushColor(ImGuiCol.NavHighlight, Accent);

        // Styles - sized through RsTheme so everything scales with the font
        pushedStyles = 0;
        PushStyle(ImGuiStyleVar.WindowRounding, RsTheme.S(8f));
        PushStyle(ImGuiStyleVar.ChildRounding, RsTheme.CornerRadius);
        PushStyle(ImGuiStyleVar.FrameRounding, RsTheme.CornerRadius);
        PushStyle(ImGuiStyleVar.PopupRounding, RsTheme.CornerRadius);
        PushStyle(ImGuiStyleVar.ScrollbarRounding, RsTheme.S(8f));
        PushStyle(ImGuiStyleVar.GrabRounding, RsTheme.CornerRadius);
        PushStyle(ImGuiStyleVar.TabRounding, RsTheme.CornerRadius);
        PushStyleVec(ImGuiStyleVar.WindowPadding, new Vector2(RsTheme.ContentPadding, RsTheme.ContentPadding));
        PushStyleVec(ImGuiStyleVar.FramePadding, RsTheme.S(10f, 6f));
        PushStyleVec(ImGuiStyleVar.ItemSpacing, RsTheme.S(8f, 7f));
        PushStyleVec(ImGuiStyleVar.ItemInnerSpacing, RsTheme.S(6f, 4f));
        PushStyle(ImGuiStyleVar.WindowBorderSize, RsTheme.BorderThickness);
        PushStyle(ImGuiStyleVar.FrameBorderSize, RsTheme.BorderThickness);
        PushStyle(ImGuiStyleVar.ScrollbarSize, RsTheme.S(10f));
        PushStyle(ImGuiStyleVar.IndentSpacing, RsTheme.S(16f));
    }

    public static void PopTheme()
    {
        // Pop all tracked pushes - theme-level plus any widget-level leaks.
        int colorsToRemove = Math.Max(pushedColors, totalColorPushes);
        int stylesToRemove = Math.Max(pushedStyles, totalStylePushes);

        for (int i = 0; i < colorsToRemove; i++)
        {
            try { ImGui.PopStyleColor(1); }
            catch { break; }
        }
        for (int i = 0; i < stylesToRemove; i++)
        {
            try { ImGui.PopStyleVar(1); }
            catch { break; }
        }

        pushedColors = 0;
        pushedStyles = 0;
        totalColorPushes = 0;
        totalStylePushes = 0;
    }

    // Widgets

    // Section title: primary text with a Border underline, like RsUI panel titles.
    public static void SectionHeader(string text)
    {
        ImGui.TextColored(RsTheme.TextPrimary, text);
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var y = pos.Y + RsTheme.S(2f);
        dl.AddLine(new Vector2(pos.X, y), new Vector2(pos.X + width, y), RsTheme.U.Border, RsTheme.BorderThickness);
        ImGui.Dummy(new Vector2(0, RsTheme.S(7f)));
    }

    // Thin divider fading out to the right, drawn in the RsUI border color.
    public static void GradientSeparator()
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;

        var left = RsTheme.U.Border;
        var right = RsTheme.WithAlpha(RsTheme.U.Border, 0f);

        dl.AddRectFilledMultiColor(
            pos,
            new Vector2(pos.X + width, pos.Y + RsTheme.BorderThickness),
            left, right, right, left);

        ImGui.Dummy(new Vector2(0, RsTheme.S(6f)));
    }

    // Card container, now an RsUI panel. Callers must still pair with EndCard.
    public static bool BeginCard(string id, Vector2 size = default)
    {
        if (size == default)
            size = new Vector2(ImGui.GetContentRegionAvail().X, 0);
        // 12px inner padding keeps the old card's content area.
        return RsElements.BeginPanel(id, null, size, innerPadding: 12f);
    }

    public static void EndCard()
    {
        RsElements.EndPanel();
    }

    public static bool PillButton(string label, Vector2 size = default, bool primary = true)
    {
        var variant = primary ? RsElements.ButtonVariant.Primary : RsElements.ButtonVariant.Secondary;
        return RsElements.Button(label, variant, ResolveButtonSize(size));
    }

    public static bool GhostButton(string label, Vector2 size = default)
    {
        return RsElements.Button(label, RsElements.ButtonVariant.Ghost, ResolveButtonSize(size));
    }

    public static bool DangerButton(string label, Vector2 size = default)
    {
        return RsElements.Button(label, RsElements.ButtonVariant.Danger, ResolveButtonSize(size));
    }

    // Kept on ImGui.InputText so SetNextItemWidth and visible labels still work;
    // chrome restyled to match RsElements.InputText.
    public static bool StyledInput(string label, ref string value, int maxLength = 256)
    {
        var pos = ImGui.GetCursorScreenPos();
        var width = ImGui.CalcItemWidth();

        ImGui.PushStyleColor(ImGuiCol.FrameBg, RsTheme.BgTertiary);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Lighten(RsTheme.BgTertiary, 0.03f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, RsTheme.BgTertiary);
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, RsTheme.CornerRadius);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, RsTheme.S(10f, 8f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);

        bool changed = ImGui.InputText(label, ref value, maxLength);

        // RsUI-style outline; accent while focused.
        var h = ImGui.GetItemRectSize().Y;
        var active = ImGui.IsItemActive();
        ImGui.GetWindowDrawList().AddRect(
            pos, new Vector2(pos.X + width, pos.Y + h),
            active ? RsTheme.U.AccentPrimary : RsTheme.U.Border,
            RsTheme.CornerRadius, ImDrawFlags.None,
            RsTheme.BorderThickness + (active ? 1f : 0f));

        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(4);

        return changed;
    }

    // Tag / status chip.
    public static void Badge(string text, Vector4? color = null)
    {
        var badgeColor = color ?? RsTheme.AccentPrimary;
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var textSize = ImGui.CalcTextSize(text);
        float padX = RsTheme.S(8f), padY = RsTheme.S(3f);
        float rounding = RsTheme.S(10f);

        var min = new Vector2(pos.X, pos.Y);
        var max = new Vector2(pos.X + textSize.X + padX * 2, pos.Y + textSize.Y + padY * 2);

        dl.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(new Vector4(badgeColor.X, badgeColor.Y, badgeColor.Z, 0.20f)), rounding);
        dl.AddRect(min, max, ImGui.ColorConvertFloat4ToU32(new Vector4(badgeColor.X, badgeColor.Y, badgeColor.Z, 0.50f)), rounding, ImDrawFlags.None, RsTheme.BorderThickness);
        dl.AddText(new Vector2(min.X + padX, min.Y + padY), ImGui.ColorConvertFloat4ToU32(badgeColor), text);

        ImGui.Dummy(new Vector2(max.X - min.X + RsTheme.S(4f), max.Y - min.Y));
    }

    public static void StyledProgressBar(float fraction, Vector2 size, string overlay = null, Vector4? barColor = null)
    {
        var col = barColor ?? RsTheme.AccentPrimary;
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, col);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, RsTheme.BgTertiary);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, RsTheme.CornerRadius);

        ImGui.ProgressBar(fraction, size, overlay ?? $"{(int)(fraction * 100)}%");

        ImGui.PopStyleVar(1);
        ImGui.PopStyleColor(2);
    }

    // Colored presence dot with soft glow; callers pass Success/Error etc.
    public static void StatusDot(string text, Vector4 dotColor)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        float radius = RsTheme.S(4f);
        float textHeight = ImGui.GetTextLineHeight();
        float cy = pos.Y + textHeight / 2f;

        dl.AddCircleFilled(new Vector2(pos.X + radius + 1, cy), radius, ImGui.ColorConvertFloat4ToU32(dotColor));
        dl.AddCircleFilled(new Vector2(pos.X + radius + 1, cy), radius + RsTheme.S(2f),
            ImGui.ColorConvertFloat4ToU32(new Vector4(dotColor.X, dotColor.Y, dotColor.Z, 0.20f)));

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + radius * 2 + RsTheme.S(8f));
        ImGui.Text(text);
    }

    public static void HoverCard(string text)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.PushStyleColor(ImGuiCol.PopupBg, RsTheme.BgSecondary);
            ImGui.PushStyleColor(ImGuiCol.Border, RsTheme.Border);
            ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, RsTheme.CornerRadius);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(RsTheme.ContentPadding, RsTheme.S(8f)));

            ImGui.BeginTooltip();
            ImGui.TextWrapped(text);
            ImGui.EndTooltip();

            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(2);
        }
    }

    // Kept on ImGui.Combo so SetNextItemWidth and visible labels still work.
    public static bool StyledCombo(string label, ref int currentItem, string[] items)
    {
        ImGui.PushStyleColor(ImGuiCol.FrameBg, RsTheme.BgTertiary);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Lighten(RsTheme.BgTertiary, 0.03f));
        ImGui.PushStyleColor(ImGuiCol.PopupBg, RsTheme.BgSecondary);
        ImGui.PushStyleColor(ImGuiCol.Border, RsTheme.Border);
        ImGui.PushStyleColor(ImGuiCol.Header, RsTheme.AccentPrimary);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, RsTheme.BgHover);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, RsTheme.AccentPrimary);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, RsTheme.CornerRadius);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, RsTheme.CornerRadius);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, RsTheme.S(10f, 6f));

        var result = ImGui.Combo(label, ref currentItem, items, items.Length);

        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(7);
        return result;
    }

    public static void SubtitleText(string text)
    {
        ImGui.TextColored(FontDim, text);
    }

    public static void AccentText(string text)
    {
        ImGui.TextColored(Accent, text);
    }

    public static bool DrawColorPicker(string label, ref Vector4 color, Vector4 defaultColor)
    {
        var changed = ImGui.ColorEdit4(label, ref color,
            ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreviewHalf);
        ImGui.SameLine();
        if (ImGui.SmallButton($"Reset##{label}"))
        {
            color = defaultColor;
            changed = true;
        }
        return changed;
    }

    // Internals

    // ImGui.Button treats negative sizes as align-to-edge; RsElements.Button treats <= 0 as auto-size, so translate before delegating.
    private static Vector2 ResolveButtonSize(Vector2 size)
    {
        if (size.X < 0f || size.Y < 0f)
        {
            var avail = ImGui.GetContentRegionAvail();
            if (size.X < 0f) size.X = Math.Max(4f, avail.X + size.X);
            if (size.Y < 0f) size.Y = Math.Max(4f, avail.Y + size.Y);
        }
        return size;
    }

    private static void PushColor(ImGuiCol col, Vector4 c) { ImGui.PushStyleColor(col, c); pushedColors++; totalColorPushes++; }
    private static void PushStyle(ImGuiStyleVar v, float val) { ImGui.PushStyleVar(v, val); pushedStyles++; totalStylePushes++; }
    private static void PushStyleVec(ImGuiStyleVar v, Vector2 val) { ImGui.PushStyleVar(v, val); pushedStyles++; totalStylePushes++; }

    public static Vector4 Lighten(Vector4 c, float a)
        => new(Math.Min(c.X + a, 1f), Math.Min(c.Y + a, 1f), Math.Min(c.Z + a, 1f), c.W);

    public static Vector4 Darken(Vector4 c, float a)
        => new(Math.Max(c.X - a, 0f), Math.Max(c.Y - a, 0f), Math.Max(c.Z - a, 0f), c.W);
}
