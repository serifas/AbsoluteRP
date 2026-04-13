using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace AbsoluteRP.Helpers;

/// <summary>
/// Manages ImGui theming for AbsoluteRP. Push/pop around WindowSystem.Draw()
/// to theme every window at once. Also provides custom widget helpers for
/// a modern, web-like UI feel.
/// </summary>
public static class ThemeManager
{
    // ── Default theme palette ──────────────────────────────────────────
    public static readonly Vector4 DefaultBorder = new(0.16f, 0.16f, 0.22f, 0.65f);
    public static readonly Vector4 DefaultBackground = new(0f, 0f, 0f, 0.969f);
    public static readonly Vector4 DefaultAccent = new(0.28f, 0.42f, 0.78f, 1.00f);
    public static readonly Vector4 DefaultFont = new(0.82f, 0.83f, 0.88f, 1.00f);

    // ── Cached derived colors (updated each PushTheme) ─────────────────
    public static Vector4 Border { get; private set; }
    public static Vector4 Background { get; private set; }
    public static Vector4 Accent { get; private set; }
    public static Vector4 Font { get; private set; }
    public static Vector4 BgLight { get; private set; }
    public static Vector4 BgLighter { get; private set; }
    public static Vector4 BgDark { get; private set; }
    public static Vector4 AccentHover { get; private set; }
    public static Vector4 AccentActive { get; private set; }
    public static Vector4 AccentMuted { get; private set; }
    public static Vector4 AccentSubtle { get; private set; }
    public static Vector4 FontDim { get; private set; }
    public static Vector4 FontMuted { get; private set; }
    public static Vector4 Success { get; private set; }
    public static Vector4 Warning { get; private set; }
    public static Vector4 Error { get; private set; }

    private static int pushedColors;
    private static int pushedStyles;

    // ────────────────────────────────────────────────────────────────────
    //  Core push / pop
    // ────────────────────────────────────────────────────────────────────

    // Track total pushes including from widgets, so PopTheme can clean up everything
    private static int totalColorPushes = 0;
    private static int totalStylePushes = 0;

    /// <summary>Call from widget Push methods to track leaked pushes</summary>
    public static void TrackColorPush(int count = 1) { totalColorPushes += count; }
    public static void TrackColorPop(int count = 1) { totalColorPushes -= count; }
    public static void TrackStylePush(int count = 1) { totalStylePushes += count; }
    public static void TrackStylePop(int count = 1) { totalStylePushes -= count; }

    public static void PushTheme(Configuration config)
    {
        // Reset total tracking at start of each frame
        totalColorPushes = 0;
        totalStylePushes = 0;

        // Read user colors (or defaults)
        Border = config.ThemeBorder ?? DefaultBorder;
        Background = config.ThemeBackground ?? DefaultBackground;
        Accent = config.ThemeAccent ?? DefaultAccent;
        Font = config.ThemeFont ?? DefaultFont;

        // Derive palette
        BgLight = Lighten(Background, 0.055f);
        BgLighter = Lighten(Background, 0.11f);
        BgDark = Darken(Background, 0.035f);
        AccentHover = Lighten(Accent, 0.12f);
        AccentActive = Darken(Accent, 0.10f);
        AccentMuted = new Vector4(Accent.X, Accent.Y, Accent.Z, 0.40f);
        AccentSubtle = new Vector4(Accent.X, Accent.Y, Accent.Z, 0.18f);
        FontDim = new Vector4(Font.X * 0.55f, Font.Y * 0.55f, Font.Z * 0.55f, Font.W);
        FontMuted = new Vector4(Font.X * 0.40f, Font.Y * 0.40f, Font.Z * 0.40f, Font.W);
        Success = new Vector4(0.30f, 0.85f, 0.45f, 1.00f);
        Warning = new Vector4(0.95f, 0.75f, 0.25f, 1.00f);
        Error = new Vector4(0.90f, 0.30f, 0.30f, 1.00f);

        pushedColors = 0;

        // ── Window ──
        PushColor(ImGuiCol.WindowBg, Background);
        PushColor(ImGuiCol.ChildBg, new Vector4(0, 0, 0, 0));
        PushColor(ImGuiCol.PopupBg, Lighten(Background, 0.04f));
        PushColor(ImGuiCol.Border, Border);
        PushColor(ImGuiCol.BorderShadow, new Vector4(0, 0, 0, 0));

        // ── Title bar ──
        PushColor(ImGuiCol.TitleBg, BgDark);
        PushColor(ImGuiCol.TitleBgActive, Darken(Accent, 0.30f));
        PushColor(ImGuiCol.TitleBgCollapsed, BgDark);

        // ── Scrollbar ──
        PushColor(ImGuiCol.MenuBarBg, BgLight);
        PushColor(ImGuiCol.ScrollbarBg, new Vector4(Background.X, Background.Y, Background.Z, 0.20f));
        PushColor(ImGuiCol.ScrollbarGrab, Lighten(BgLighter, 0.03f));
        PushColor(ImGuiCol.ScrollbarGrabHovered, Lighten(BgLighter, 0.10f));
        PushColor(ImGuiCol.ScrollbarGrabActive, Accent);

        // ── Frame (inputs, checkboxes, combos) ──
        PushColor(ImGuiCol.FrameBg, BgLight);
        PushColor(ImGuiCol.FrameBgHovered, BgLighter);
        PushColor(ImGuiCol.FrameBgActive, Darken(Accent, 0.28f));

        // ── Buttons ──
        PushColor(ImGuiCol.Button, Darken(Accent, 0.18f));
        PushColor(ImGuiCol.ButtonHovered, Accent);
        PushColor(ImGuiCol.ButtonActive, AccentActive);

        // ── Headers / collapsing ──
        PushColor(ImGuiCol.Header, AccentSubtle);
        PushColor(ImGuiCol.HeaderHovered, AccentMuted);
        PushColor(ImGuiCol.HeaderActive, AccentActive);

        // ── Tabs ──
        PushColor(ImGuiCol.Tab, BgLight);
        PushColor(ImGuiCol.TabHovered, Accent);
        PushColor(ImGuiCol.TabActive, Darken(Accent, 0.08f));

        // ── Separator ──
        PushColor(ImGuiCol.Separator, new Vector4(Border.X, Border.Y, Border.Z, Border.W * 0.5f));
        PushColor(ImGuiCol.SeparatorHovered, Accent);
        PushColor(ImGuiCol.SeparatorActive, AccentActive);

        // ── Resize grip ──
        PushColor(ImGuiCol.ResizeGrip, AccentMuted);
        PushColor(ImGuiCol.ResizeGripHovered, Accent);
        PushColor(ImGuiCol.ResizeGripActive, AccentActive);

        // ── Check / slider ──
        PushColor(ImGuiCol.CheckMark, Accent);
        PushColor(ImGuiCol.SliderGrab, Accent);
        PushColor(ImGuiCol.SliderGrabActive, AccentHover);

        // ── Text ──
        PushColor(ImGuiCol.Text, Font);
        PushColor(ImGuiCol.TextDisabled, FontDim);

        // ── Table ──
        PushColor(ImGuiCol.TableHeaderBg, BgLight);
        PushColor(ImGuiCol.TableBorderStrong, Border);
        PushColor(ImGuiCol.TableBorderLight, new Vector4(Border.X, Border.Y, Border.Z, Border.W * 0.4f));
        PushColor(ImGuiCol.TableRowBg, new Vector4(0, 0, 0, 0));
        PushColor(ImGuiCol.TableRowBgAlt, new Vector4(1, 1, 1, 0.018f));

        // ── Progress / plot ──
        PushColor(ImGuiCol.PlotHistogram, Accent);
        PushColor(ImGuiCol.PlotHistogramHovered, AccentHover);

        // ── Nav ──
        PushColor(ImGuiCol.NavHighlight, Accent);

        // ── Styles ──
        pushedStyles = 0;
        PushStyle(ImGuiStyleVar.WindowRounding, 8.0f);
        PushStyle(ImGuiStyleVar.ChildRounding, 6.0f);
        PushStyle(ImGuiStyleVar.FrameRounding, 6.0f);
        PushStyle(ImGuiStyleVar.PopupRounding, 6.0f);
        PushStyle(ImGuiStyleVar.ScrollbarRounding, 8.0f);
        PushStyle(ImGuiStyleVar.GrabRounding, 6.0f);
        PushStyle(ImGuiStyleVar.TabRounding, 6.0f);
        PushStyleVec(ImGuiStyleVar.WindowPadding, new Vector2(12, 12));
        PushStyleVec(ImGuiStyleVar.FramePadding, new Vector2(8, 5));
        PushStyleVec(ImGuiStyleVar.ItemSpacing, new Vector2(8, 7));
        PushStyleVec(ImGuiStyleVar.ItemInnerSpacing, new Vector2(6, 4));
        PushStyle(ImGuiStyleVar.WindowBorderSize, 1.0f);
        PushStyle(ImGuiStyleVar.FrameBorderSize, 0.0f);
        PushStyle(ImGuiStyleVar.ScrollbarSize, 10.0f);
        PushStyle(ImGuiStyleVar.IndentSpacing, 16.0f);
    }

    public static void PopTheme()
    {
        // Pop all tracked pushes — theme-level + any widget-level leaks from exceptions
        int colorsToRemove = Math.Max(pushedColors, totalColorPushes);
        int stylesToRemove = Math.Max(pushedStyles, totalStylePushes);

        // Pop in a loop to handle any leaked widget pushes
        // If we try to pop more than exists, ImGui will throw — catch and stop
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

    // ────────────────────────────────────────────────────────────────────
    //  Custom widgets — modern web-like UI components
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Draws a styled section header with an accent-colored left bar and label.
    /// Like a modern card section title.
    /// </summary>
    public static void SectionHeader(string text)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        float barWidth = 3f;
        float textHeight = ImGui.GetTextLineHeight();
        float padding = 4f;

        // Accent bar
        dl.AddRectFilled(
            pos,
            new Vector2(pos.X + barWidth, pos.Y + textHeight + padding * 2),
            ImGui.ColorConvertFloat4ToU32(Accent),
            2f);

        // Text
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + barWidth + 8f);
        ImGui.TextColored(Font, text);
        ImGui.Spacing();
    }

    /// <summary>
    /// Draws a subtle horizontal divider with gradient fade.
    /// </summary>
    public static void GradientSeparator()
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;

        var leftColor = ImGui.ColorConvertFloat4ToU32(AccentMuted);
        var rightColor = ImGui.ColorConvertFloat4ToU32(new Vector4(Accent.X, Accent.Y, Accent.Z, 0.0f));

        dl.AddRectFilledMultiColor(
            pos,
            new Vector2(pos.X + width, pos.Y + 1f),
            leftColor, rightColor, rightColor, leftColor);

        ImGui.Dummy(new Vector2(0, 6f));
    }

    /// <summary>
    /// Begins a card-like container with subtle background, border, and rounded corners.
    /// Returns true if the child region is visible. Must call EndCard() after.
    /// </summary>
    public static bool BeginCard(string id, Vector2 size = default)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();

        if (size == default)
            size = new Vector2(ImGui.GetContentRegionAvail().X, 0);

        // Draw card background
        var cardBg = Lighten(Background, 0.03f);
        var cardBorder = new Vector4(Border.X, Border.Y, Border.Z, Border.W * 0.6f);

        ImGui.PushStyleColor(ImGuiCol.ChildBg, cardBg); totalColorPushes++;
        ImGui.PushStyleColor(ImGuiCol.Border, cardBorder); totalColorPushes++;
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 8f); totalStylePushes++;
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1f); totalStylePushes++;

        return ImGui.BeginChild(id, size, true);
    }

    public static void EndCard()
    {
        ImGui.EndChild();
        ImGui.PopStyleVar(2); totalStylePushes -= 2;
        ImGui.PopStyleColor(2); totalColorPushes -= 2;
    }

    /// <summary>
    /// Draws a modern pill-shaped button. Returns true if clicked.
    /// </summary>
    public static bool PillButton(string label, Vector2 size = default, bool primary = true)
    {
        var bg = primary ? Darken(Accent, 0.10f) : BgLighter;
        var hovered = primary ? Accent : Lighten(BgLighter, 0.06f);
        var active = primary ? AccentActive : Lighten(BgLighter, 0.12f);
        var textCol = primary ? new Vector4(1, 1, 1, 1) : Font;

        ImGui.PushStyleColor(ImGuiCol.Button, bg);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hovered);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, active);
        ImGui.PushStyleColor(ImGuiCol.Text, textCol);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 20f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(16, 6));

        var result = ImGui.Button(label, size);

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(4);

        return result;
    }

    /// <summary>
    /// Draws an outlined (ghost) button — transparent bg with accent border.
    /// </summary>
    public static bool GhostButton(string label, Vector2 size = default)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, AccentSubtle);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, AccentMuted);
        ImGui.PushStyleColor(ImGuiCol.Text, Accent);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.PushStyleColor(ImGuiCol.Border, Accent);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(12, 6));

        var result = ImGui.Button(label, size);

        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(5);

        return result;
    }

    /// <summary>
    /// Draws a danger (destructive action) button in error/red.
    /// </summary>
    public static bool DangerButton(string label, Vector2 size = default)
    {
        var bg = Darken(Error, 0.15f);
        ImGui.PushStyleColor(ImGuiCol.Button, bg);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Error);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Darken(Error, 0.10f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 1, 1, 1));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(12, 6));

        var result = ImGui.Button(label, size);

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(4);

        return result;
    }

    /// <summary>
    /// Draws a styled text input with placeholder text and a subtle underline accent.
    /// </summary>
    public static bool StyledInput(string label, ref string value, int maxLength = 256)
    {
        ImGui.PushStyleColor(ImGuiCol.FrameBg, BgLight);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, BgLighter);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Darken(Accent, 0.30f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 7));

        bool changed = ImGui.InputText(label, ref value, maxLength);

        // Draw subtle accent underline on active
        if (ImGui.IsItemActive())
        {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            ImGui.GetWindowDrawList().AddLine(
                new Vector2(min.X + 2, max.Y),
                new Vector2(max.X - 2, max.Y),
                ImGui.ColorConvertFloat4ToU32(Accent), 2f);
        }

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(3);

        return changed;
    }

    /// <summary>
    /// Draws a badge/chip with text (like a tag or status indicator).
    /// </summary>
    public static void Badge(string text, Vector4? color = null)
    {
        var badgeColor = color ?? Accent;
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var textSize = ImGui.CalcTextSize(text);
        float padX = 8f, padY = 3f;
        float rounding = 10f;

        var min = new Vector2(pos.X, pos.Y);
        var max = new Vector2(pos.X + textSize.X + padX * 2, pos.Y + textSize.Y + padY * 2);

        dl.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(new Vector4(badgeColor.X, badgeColor.Y, badgeColor.Z, 0.20f)), rounding);
        dl.AddRect(min, max, ImGui.ColorConvertFloat4ToU32(new Vector4(badgeColor.X, badgeColor.Y, badgeColor.Z, 0.50f)), rounding, ImDrawFlags.None, 1f);
        dl.AddText(new Vector2(min.X + padX, min.Y + padY), ImGui.ColorConvertFloat4ToU32(badgeColor), text);

        ImGui.Dummy(new Vector2(max.X - min.X + 4, max.Y - min.Y));
    }

    /// <summary>
    /// Draws a modern styled progress bar with rounded ends and label.
    /// </summary>
    public static void StyledProgressBar(float fraction, Vector2 size, string overlay = null, Vector4? barColor = null)
    {
        var col = barColor ?? Accent;
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, col);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, BgLight);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 8f);

        ImGui.ProgressBar(fraction, size, overlay ?? $"{(int)(fraction * 100)}%");

        ImGui.PopStyleVar(1);
        ImGui.PopStyleColor(2);
    }

    /// <summary>
    /// Draws a status dot (online/offline/away indicator) followed by text.
    /// </summary>
    public static void StatusDot(string text, Vector4 dotColor)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        float radius = 4f;
        float textHeight = ImGui.GetTextLineHeight();
        float cy = pos.Y + textHeight / 2f;

        dl.AddCircleFilled(new Vector2(pos.X + radius + 1, cy), radius, ImGui.ColorConvertFloat4ToU32(dotColor));
        // Glow
        dl.AddCircleFilled(new Vector2(pos.X + radius + 1, cy), radius + 2f,
            ImGui.ColorConvertFloat4ToU32(new Vector4(dotColor.X, dotColor.Y, dotColor.Z, 0.20f)));

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + radius * 2 + 8f);
        ImGui.Text(text);
    }

    /// <summary>
    /// Draws a tooltip-style info card when hovering over the previous item.
    /// </summary>
    public static void HoverCard(string text)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.PushStyleColor(ImGuiCol.PopupBg, Lighten(Background, 0.06f));
            ImGui.PushStyleColor(ImGuiCol.Border, Border);
            ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 8f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 8));

            ImGui.BeginTooltip();
            ImGui.TextWrapped(text);
            ImGui.EndTooltip();

            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(2);
        }
    }

    /// <summary>
    /// Draws a modern styled combo/dropdown.
    /// </summary>
    public static bool StyledCombo(string label, ref int currentItem, string[] items)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 6));

        var result = ImGui.Combo(label, ref currentItem, items, items.Length);

        ImGui.PopStyleVar(3);
        return result;
    }

    /// <summary>
    /// Helper to draw muted/dimmed subtitle text.
    /// </summary>
    public static void SubtitleText(string text)
    {
        ImGui.TextColored(FontDim, text);
    }

    /// <summary>
    /// Helper to draw accent-colored text.
    /// </summary>
    public static void AccentText(string text)
    {
        ImGui.TextColored(Accent, text);
    }

    // ────────────────────────────────────────────────────────────────────
    //  Color picker for theme settings
    // ────────────────────────────────────────────────────────────────────

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

    // ────────────────────────────────────────────────────────────────────
    //  Internals
    // ────────────────────────────────────────────────────────────────────

    private static void PushColor(ImGuiCol col, Vector4 c) { ImGui.PushStyleColor(col, c); pushedColors++; totalColorPushes++; }
    private static void PushStyle(ImGuiStyleVar v, float val) { ImGui.PushStyleVar(v, val); pushedStyles++; totalStylePushes++; }
    private static void PushStyleVec(ImGuiStyleVar v, Vector2 val) { ImGui.PushStyleVar(v, val); pushedStyles++; totalStylePushes++; }

    public static Vector4 Lighten(Vector4 c, float a)
        => new(Math.Min(c.X + a, 1f), Math.Min(c.Y + a, 1f), Math.Min(c.Z + a, 1f), c.W);

    public static Vector4 Darken(Vector4 c, float a)
        => new(Math.Max(c.X - a, 0f), Math.Max(c.Y - a, 0f), Math.Max(c.Z - a, 0f), c.W);
}
