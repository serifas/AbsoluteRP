using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace AbsoluteRP.RsUI;

// Global ImGui restyle for the legacy window pass. Push() before WindowSystem.Draw, Pop() in a finally - every stock widget then picks up the RsTheme palette without touching the window files.
public static class RsGlobalStyle
{
    // Pop() must mirror Push() exactly - update these when adding entries.
    private const int ColorCount = 46;
    private const int StyleVarCount = 11;

    public static void Push()
    {
        var transparent = new Vector4(0f, 0f, 0f, 0f);

        // Windows and popups
        ImGui.PushStyleColor(ImGuiCol.WindowBg, RsTheme.BgPrimary);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, transparent);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, RsTheme.BgSecondary);
        ImGui.PushStyleColor(ImGuiCol.Border, RsTheme.Border);
        ImGui.PushStyleColor(ImGuiCol.BorderShadow, transparent);

        // Title bar
        ImGui.PushStyleColor(ImGuiCol.TitleBg, RsTheme.BgSecondary);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, Brighten(RsTheme.BgSecondary, 0.06f));
        ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, RsTheme.BgSecondary);
        ImGui.PushStyleColor(ImGuiCol.MenuBarBg, RsTheme.BgSecondary);

        // Scrollbar
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, RsTheme.BgPrimary);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, Brighten(RsTheme.BgSecondary, 0.08f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, Brighten(RsTheme.BgSecondary, 0.12f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, RsTheme.AccentPrimary);

        // Frames - inputs, combos, checkboxes
        ImGui.PushStyleColor(ImGuiCol.FrameBg, RsTheme.BgTertiary);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, RsTheme.BgHover);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Brighten(RsTheme.BgHover, 0.06f));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, RsTheme.AccentPrimary);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, RsTheme.AccentPrimary);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, Brighten(RsTheme.AccentPrimary, 0.10f));

        // Buttons
        ImGui.PushStyleColor(ImGuiCol.Button, RsTheme.AccentPrimary);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Brighten(RsTheme.AccentPrimary, 0.08f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Brighten(RsTheme.AccentPrimary, 0.12f));

        // Headers - selectables, collapsing headers
        ImGui.PushStyleColor(ImGuiCol.Header, Fade(RsTheme.AccentPrimary, 0.25f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Fade(RsTheme.AccentPrimary, 0.35f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, Fade(RsTheme.AccentPrimary, 0.45f));

        // Separators and resize grips
        ImGui.PushStyleColor(ImGuiCol.Separator, RsTheme.Border);
        ImGui.PushStyleColor(ImGuiCol.SeparatorHovered, RsTheme.BorderStrong);
        ImGui.PushStyleColor(ImGuiCol.SeparatorActive, RsTheme.AccentPrimary);
        ImGui.PushStyleColor(ImGuiCol.ResizeGrip, Fade(RsTheme.AccentPrimary, 0.20f));
        ImGui.PushStyleColor(ImGuiCol.ResizeGripHovered, Fade(RsTheme.AccentPrimary, 0.60f));
        ImGui.PushStyleColor(ImGuiCol.ResizeGripActive, RsTheme.AccentPrimary);

        // Tabs
        ImGui.PushStyleColor(ImGuiCol.Tab, RsTheme.BgSecondary);
        ImGui.PushStyleColor(ImGuiCol.TabHovered, Brighten(RsTheme.AccentPrimary, 0.08f));
        ImGui.PushStyleColor(ImGuiCol.TabActive, RsTheme.AccentPrimary);
        ImGui.PushStyleColor(ImGuiCol.TabUnfocused, RsTheme.BgSecondary);
        ImGui.PushStyleColor(ImGuiCol.TabUnfocusedActive, Fade(RsTheme.AccentPrimary, 0.60f));

        // Text
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, RsTheme.TextMuted);

        // Tables
        ImGui.PushStyleColor(ImGuiCol.TableHeaderBg, RsTheme.BgSecondary);
        ImGui.PushStyleColor(ImGuiCol.TableBorderStrong, RsTheme.BorderStrong);
        ImGui.PushStyleColor(ImGuiCol.TableBorderLight, Fade(RsTheme.Border, 0.50f));
        ImGui.PushStyleColor(ImGuiCol.TableRowBg, transparent);
        ImGui.PushStyleColor(ImGuiCol.TableRowBgAlt, new Vector4(1f, 1f, 1f, 0.02f));

        // Plots and nav
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, RsTheme.AccentPrimary);
        ImGui.PushStyleColor(ImGuiCol.PlotHistogramHovered, Brighten(RsTheme.AccentPrimary, 0.10f));
        ImGui.PushStyleColor(ImGuiCol.NavHighlight, RsTheme.AccentPrimary);

        // Rounding and spacing
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, RsTheme.CornerRadius);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, RsTheme.CornerRadius);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, RsTheme.CornerRadius);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, RsTheme.CornerRadius);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, RsTheme.CornerRadius);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, RsTheme.CornerRadius);
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, RsTheme.CornerRadius);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, RsTheme.BorderThickness);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, RsTheme.BorderThickness);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, RsTheme.S(10f, 8f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, RsTheme.S(8f, 6f));
    }

    public static void Pop()
    {
        ImGui.PopStyleVar(StyleVarCount);
        ImGui.PopStyleColor(ColorCount);
    }

    // Lerp toward white for hover/active ramps.
    private static Vector4 Brighten(Vector4 c, float t)
    {
        return new Vector4(
            c.X + (1f - c.X) * t,
            c.Y + (1f - c.Y) * t,
            c.Z + (1f - c.Z) * t,
            c.W);
    }

    // Same color, different alpha - used for tinted fills.
    private static Vector4 Fade(Vector4 c, float alpha)
    {
        return new Vector4(c.X, c.Y, c.Z, alpha);
    }
}
