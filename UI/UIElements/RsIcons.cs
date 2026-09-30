using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AbsoluteRP.RsUI;

/// Helpers for drawing FontAwesome icons from Dalamud's bundled font. Dalamud already loads the FontAwesome Free Solid font and exposes it via IUiBuilder.IconFontHandle. Pushing that handle swaps the active ImGui font for the icon glyphs; every FontAwesomeIcon enum value is a Unicode codepoint that renders correctly under that font. Outside the push, calling code renders normal text as usual. Everything here is a thin wrapper so we never sprinkle Plugin.PluginInterface.UiBuilder.IconFontHandle.Push() throughout window code.
public static class RsIcons
{
    /// Push the icon font for the duration of a using block. Nest freely - everything inside the block renders in icon font. If the icon font isn't loaded yet (first frames after enable), this is a no-op scope so callers don't have to null-check.
    public static IconFontScope Push()
    {
        var handle = Plugin.PluginInterface.UiBuilder.IconFontHandle;
        if (handle is null || !handle.Available)
            return default;
        return new IconFontScope(handle.Push());
    }

    /// Disposable wrapper around Push that safely no-ops when the icon font hasn't finished loading. Dalamud's SimplePushedFont throws "Tried to pop a non-pushed font" if you dispose a push token whose underlying push was skipped, so we carry the inner token only when the handle was actually usable.
    public readonly struct IconFontScope : IDisposable
    {
        private readonly IDisposable? inner;
        public IconFontScope(IDisposable? inner) => this.inner = inner;
        public bool Pushed => inner is not null;
        public void Dispose() => inner?.Dispose();
    }

    /// Horizontal slack (px, unscaled) added to every icon's measured width. Many FontAwesome glyphs (PaperPlane, Trash, Envelope...) draw visually wider than their typographic advance - without this slack ImGui's RenderTextClipped inside buttons trims the right side, and icons drawn next to text visually overlap the label. Read through IconSlackScaled in draw code so it tracks Dalamud's font scaling.
    public const float IconSlack = 20f;

    /// Font-scaled version of IconSlack.
    public static float IconSlackScaled => RsTheme.S(IconSlack);

    /// Measure the icon in the icon font, padded with IconSlack so the returned size reserves enough room for the full glyph. Callers must NOT push the icon font before calling this - it pushes internally.
    public static Vector2 Measure(FontAwesomeIcon icon)
    {
        Vector2 raw;
        using (Push())
        {
            raw = ImGui.CalcTextSize(icon.ToIconString());
        }
        return new Vector2(raw.X + IconSlackScaled, raw.Y);
    }

    /// Draw a single icon as inline text. Uses SameLine() convention: leaves the cursor immediately after the glyph so you can call ImGui.SameLine(); ImGui.Text(...) to place a label.
    public static void Text(FontAwesomeIcon icon)
    {
        using (Push())
        {
            ImGui.Text(icon.ToIconString());
        }
    }

    /// Draw a colored icon. Uses PushStyleColor(ImGuiCol, Vector4) internally so the tint applies only to this glyph.
    public static void Text(FontAwesomeIcon icon, Vector4 color)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        using (Push())
        {
            ImGui.Text(icon.ToIconString());
        }
        ImGui.PopStyleColor();
    }

    /// Icon + space + text on one line. Convenience for the very common " Send" or " Direct Message" pattern where you want the icon glyph followed by regular-font text. Adds IconSlack to the gap so wide FA glyphs don't visually crowd the label.
    public static void Label(FontAwesomeIcon icon, string text)
    {
        Text(icon);
        ImGui.SameLine(0f, IconSlackScaled);
        ImGui.Text(text);
    }

    public static void Label(FontAwesomeIcon icon, Vector4 iconColor, string text)
    {
        Text(icon, iconColor);
        ImGui.SameLine(0f, IconSlackScaled);
        ImGui.Text(text);
    }

    /// Clickable icon-only button. Uses the icon codepoint as both the visible glyph and the ImGui id - pass a distinct id when the same icon appears multiple times in one window.
    public static bool Button(FontAwesomeIcon icon, string id = "", Vector2 size = default)
    {
        var iconStr = icon.ToIconString();
        var label = iconStr + "##" + (id.Length > 0 ? id : icon.ToString());
        using (Push())
        {
            if (size != default)
                return ImGui.Button(label, size);
            // Pass an explicit size that includes IconSlack, otherwise ImGui.Button's default sizing (advance + FramePadding) plus its RenderTextClipped will trim the right side of wide FA glyphs like PaperPlane / Trash.
            var raw = ImGui.CalcTextSize(iconStr);
            var pad = ImGui.GetStyle().FramePadding;
            var sz = new Vector2(raw.X + IconSlackScaled + pad.X * 2f, raw.Y + pad.Y * 2f);
            return ImGui.Button(label, sz);
        }
    }

    /// Clickable icon + text button on one line. The icon renders in icon font, the label in the current font - laid out inside a single invisible button hitbox so the whole thing is one click target.
    public static bool LabelButton(FontAwesomeIcon icon, string text, string? uniqueId = null)
    {
        // We render the button as a single ImGui.Button whose label is the icon glyph, then draw the text on top with SameLine. To make the hitbox cover both we size the button to (icon+text) width up-front.
        var iconStr = icon.ToIconString();
        Vector2 iconRaw;
        using (Push())
        {
            iconRaw = ImGui.CalcTextSize(iconStr);
        }
        // Reserve extra width so wide FA glyphs aren't clipped by the button's inner rect (RenderTextClipped inside ImGui.Button) and don't visually collide with the text.
        var iconSlot = new Vector2(iconRaw.X + IconSlackScaled, iconRaw.Y);
        var textSize = ImGui.CalcTextSize(text);
        var pad = ImGui.GetStyle().FramePadding;
        var total = new Vector2(
            pad.X * 2f + iconSlot.X + textSize.X,
            Math.Max(iconSlot.Y, textSize.Y) + pad.Y * 2f);

        var id = "##rs_icobtn_" + (uniqueId ?? (icon.ToString() + "_" + text));
        var start = ImGui.GetCursorScreenPos();
        var clicked = ImGui.Button(id, total);
        var draw = ImGui.GetWindowDrawList();

        // Draw the icon glyph inside the button's rect. Icon sits in the left iconSlot; text follows right after (no extra gap - the slack inside iconSlot already provides breathing room).
        var iconPos = new Vector2(start.X + pad.X, start.Y + (total.Y - iconRaw.Y) * 0.5f);
        var textPos = new Vector2(iconPos.X + iconSlot.X, start.Y + (total.Y - textSize.Y) * 0.5f);
        var textColor = RsTheme.WithAlpha(RsTheme.U.TextPrimary, 1f);
        using (Push())
        {
            draw.AddText(iconPos, textColor, iconStr);
        }
        draw.AddText(textPos, textColor, text);
        return clicked;
    }
}
