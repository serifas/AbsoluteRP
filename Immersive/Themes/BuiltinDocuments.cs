using System.Collections.Generic;

namespace AbsoluteRP.Immersive.Themes;

// Document versions of the shipped themes. The built-ins render with their hand-tuned native layouts; these documents are what a user gets when they "Duplicate & edit" one " a faithful approximation expressed in the panel/element model so it can be changed freely.
public static class BuiltinDocuments
{
    public static ThemeDocument Create(int builtinIndex)
    {
        var mat = ImmersiveThemes.Get(builtinIndex);
        var doc = new ThemeDocument
        {
            Name = mat.Name + " (copy)",
            Description = mat.Tagline,
            Material = mat.MaterialKind,
            Palette = mat.DefaultPalette,
            Style = mat.DefaultStyle,
            Vfx = new ThemeVfx(),
            BasedOn = mat.Name,
        };
        // Built-ins all share the single-panel layout; the material only changes palette, surfaces and effects.
        doc.Panels = Standard();
        return doc.Normalize();
    }

    public static ThemeDocument Blank(ThemeMaterial material)
    {
        var mat = ImmersiveThemes.CreateMaterial(material);
        return new ThemeDocument
        {
            Name = "New Theme",
            Material = material,
            Palette = mat.DefaultPalette,
            Style = mat.DefaultStyle,
            Vfx = new ThemeVfx(),
            Panels = Standard(),
        }.Normalize();
    }

    private static ElementDef E(ElementType t, float size = 0f, bool fill = false, bool inline = false, NavStyle nav = NavStyle.Horizontal, string text = "", TextAlign align = TextAlign.Center)
        => new() { Type = t, Size = size, Fill = fill, Inline = inline, Nav = nav, Text = text, Align = align };

    // A freely placed element: x/y/w/h are fractions of the panel's inner area.
    private static ElementDef F(ElementType t, float x, float y, float w, float h, float size = 0f, bool fill = false, NavStyle nav = NavStyle.Horizontal)
        => new() { Type = t, Free = true, X = x, Y = y, W = w, H = h, Size = size, Fill = fill, Nav = nav };

    // Gives an element a fixed id and (optionally) pins it below another, centred.
    private static ElementDef Pin(ElementDef e, string id, string? below, float gap = 6f, bool fillRemaining = false)
    {
        e.Id = id;
        if (below != null) { e.PinTo = below; e.Pin = PinSide.Below; e.PinGap = gap; e.PinAlign = PinAlign.Center; e.FillRemaining = fillRemaining; }
        return e;
    }

    // Standard: one panel, portrait and name centred at the top, the section nav under them and the content below.
    public static List<PanelDef> Standard() => new()
    {
        new PanelDef
        {
            Name = "Profile", Header = "", Anchor = PanelAnchor.TopRight, X = 0.025f, Y = 0.06f, W = 0.44f, H = 0.89f, Depth = 0.2f, Bob = false, Solid = true,
            Elements =
            {
                // A pin chain: the avatar is placed freely at the top centre and everything else hangs below the element before it, so scaling the avatar or title pushes the rest down. X/Y are the fallback spots if a pin is removed. Heights are minimal: pinned pieces take their natural rendered height.
                Pin(F(ElementType.Avatar,      0.00f, 0.000f, 1.00f, 0.020f, size: 112f), "avatar", null),
                Pin(F(ElementType.Title,       0.00f, 0.150f, 1.00f, 0.020f, size: 1.45f), "title", "avatar", 0f),
                Pin(F(ElementType.ControlsRow, 0.10f, 0.218f, 0.80f, 0.020f, size: 28f), "icons", "title", 0f),
                Pin(F(ElementType.Divider,     0.00f, 0.266f, 1.00f, 0.018f), "div1", "icons", 4f),
                Pin(F(ElementType.SectionNav,  0.00f, 0.286f, 1.00f, 0.020f, nav: NavStyle.Horizontal), "nav", "div1", 2f),
                Pin(F(ElementType.Divider,     0.00f, 0.343f, 1.00f, 0.018f), "div2", "nav", 2f),
                Pin(F(ElementType.SectionBody, 0.00f, 0.363f, 1.00f, 0.637f, fill: true), "body", "div2", 2f, fillRemaining: true),
            },
        },
    };
}
