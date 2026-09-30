using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;

namespace AbsoluteRP.Immersive.Themes;

// The theme document. Everything a user-made (or forked built-in) theme is, serialised as JSON: which material renders the chrome, the palette, style knobs, VFX controls, and the layout - a list of panels, each with a list of elements laid out top-to-bottom. Positions and sizes are fractions of the main viewport's work area (0..1) so a theme designed at 1440p lays out the same at 1080p. The editor drags panels around in those units. This is the wire format too: the server stores and returns the JSON verbatim (see ThemeNetwork for the packet contract).

// Standard is appended last: material values are persisted in saved themes.
public enum ThemeMaterial { Allagan, Nymian, Sharlayan, Voidtouched, Aether, Standard }

public enum PanelAnchor { TopLeft, TopRight, BottomLeft, BottomRight, Center }

// How a child panel hangs off its parent: inside the parent's rect, or glued to the OUTSIDE of one of its edges (sliding along that edge).
public enum PanelAttach { Inside, Above, Below, LeftOf, RightOf }

public enum ElementType
{
    Avatar,        // circular portrait with the material's ring treatment
    Title,         // profile title + "name - world"
    Readouts,      // access / sections / equipment rows
    SectionNav,    // tab strip; NavStyle picks the flavour
    SectionBody,   // the selected tab's content (scrolling)
    Image,         // one of the theme's own images
    Controls,      // notes / equipment / report buttons
    LikeButton,
    CloseButton,
    LinkReadout,   // "LINK ACTIVE"-style status line
    Text,          // custom static text
    Divider,
    Spacer,
    LoadingProgress,   // loading scene: whose profile + progress bars
    TooltipInfo,       // tooltip scene: the tooltip's name / race / age... lines
    ControlsRow,       // the notes / link / like / equipment icon row (Size = icon px)
}

public enum NavStyle { Horizontal, Vertical, Index, Shards }

public enum TextAlign { Left, Center, Right }

public sealed class ThemeColor
{
    public float R { get; set; } = 1f;
    public float G { get; set; } = 1f;
    public float B { get; set; } = 1f;
    public float A { get; set; } = 1f;

    public ThemeColor() { }
    public ThemeColor(Vector4 v) { R = v.X; G = v.Y; B = v.Z; A = v.W; }
    public ThemeColor(float r, float g, float b, float a = 1f) { R = r; G = g; B = b; A = a; }

    [JsonIgnore] public Vector4 V => new(R, G, B, A);
    public static ThemeColor From(Vector4 v) => new(v);
    public ThemeColor Clone() => new(R, G, B, A);
}

public sealed class ThemePalette
{
    public ThemeColor Accent     { get; set; } = new(0.42f, 0.86f, 1.00f);
    public ThemeColor AccentSoft { get; set; } = new(0.30f, 0.62f, 0.80f);
    public ThemeColor Text       { get; set; } = new(0.88f, 0.96f, 1.00f);
    public ThemeColor Muted      { get; set; } = new(0.52f, 0.68f, 0.78f);
    public ThemeColor Danger     { get; set; } = new(1.00f, 0.42f, 0.46f);
    public ThemeColor SurfaceTop { get; set; } = new(0.03f, 0.08f, 0.13f);
    public ThemeColor SurfaceBot { get; set; } = new(0.01f, 0.03f, 0.06f);

    public ThemePalette Clone() => JsonConvert.DeserializeObject<ThemePalette>(JsonConvert.SerializeObject(this))!;
}

public sealed class ThemeStyle
{
    public float Rounding       { get; set; } = 0f;     // px at 1.0 scale
    public float ContentInset   { get; set; } = 16f;
    public float HeaderOffsetY  { get; set; } = 0f;
    public float FillAlpha      { get; set; } = 0.86f;
    public float ScrimAlpha     { get; set; } = 0.34f;
    public float VeilImageAlpha { get; set; } = 0.62f;
    public float FloatAmount    { get; set; } = 1f;
    public bool  TechAccents    { get; set; } = true;
    public bool  ElementGlow    { get; set; } = false;
    public string Entrance      { get; set; } = "Slide";   // PanelEntrance name
    public string Exit          { get; set; } = "";        // PanelEntrance name; empty = same as Entrance
    public float  OpenSpeed     { get; set; } = 1f;        // 1 = normal; 2 = twice as fast
    public float  CloseSpeed    { get; set; } = 1f;
    public string LinkLabel     { get; set; } = "LINK ACTIVE";
    public string CloseLabel    { get; set; } = "DISCONNECT";
}

// Every switch the editor exposes for effects. Materials consult the flags that apply to them and ignore the rest; Intensity scales the alpha of everything decorative so a theme can be "quiet".
public sealed class ThemeVfx
{
    public float Intensity     { get; set; } = 1f;
    public float Speed         { get; set; } = 1f;      // animation rate of the material's effects
    public bool  UseEffectColor { get; set; } = false;  // tint the material's effects and chrome
    public ThemeColor EffectColor { get; set; } = new(0.42f, 0.86f, 1f);
    public float ParallaxDepth { get; set; } = 1f;
    public bool ScanLines  { get; set; } = true;   // Allagan
    public bool Sweep      { get; set; } = true;   // Allagan
    public bool Traces     { get; set; } = true;   // Allagan
    public bool Grid       { get; set; } = true;   // Allagan veil
    public bool Veins      { get; set; } = true;   // Nymian
    public bool Meander    { get; set; } = true;   // Nymian
    public bool Cracks     { get; set; } = true;   // Sharlayan
    public bool InnerFade  { get; set; } = true;   // Sharlayan
    public bool Embers     { get; set; } = true;   // Voidtouched veil
    public bool Fissures   { get; set; } = true;   // Voidtouched
    public bool Glitch     { get; set; } = true;   // Voidtouched twitch
    public bool Dissolve   { get; set; } = true;   // Voidtouched burn in/out
    public bool Ribbons    { get; set; } = true;   // Aether veil
    public bool Halo       { get; set; } = true;   // Aether
    public bool EdgeMotes  { get; set; } = true;   // Aether
    public bool VeilMotes  { get; set; } = true;   // Nymian / Sharlayan / Aether dust
    public bool Vignette   { get; set; } = true;
}

// Edge treatments an element can wear regardless of the theme's material.
public enum EdgeMask { None, Torn, Scorched, Brackets, Jagged }

// images Pictures the author adds to the theme (PNG, base64, downscaled on import). They can be a panel or element BACKGROUND, a FRAME laid over the rim (optionally 9-sliced so corners keep their shape), a MASK whose transparency cuts the shape of an avatar / gallery image / background, or a free-standing Image element.
public enum ImageFit { Stretch, Cover, Contain }

public sealed class ThemeAsset
{
    public string Id     { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name   { get; set; } = "Image";
    public string Png    { get; set; } = "";     // base64 PNG
    public int Width     { get; set; }
    public int Height    { get; set; }
    [JsonIgnore] public int Bytes => (int)(Png.Length * 0.75f);
}

// How an asset is used somewhere.
public sealed class ImageRef
{
    public string Asset   { get; set; } = "";    // ThemeAsset.Id
    public float Alpha    { get; set; } = 1f;
    public ImageFit Fit   { get; set; } = ImageFit.Cover;
    public float Border   { get; set; } = 0f;    // px: > 0 = 9-slice with corners this big (frames)
    public float Inset    { get; set; } = 0f;    // px in from the rect
    public float Rounding { get; set; } = 0f;    // px corner rounding (non-9-slice)
    public ThemeColor? Tint { get; set; } = null;
    [JsonIgnore] public bool IsSet => !string.IsNullOrEmpty(Asset);
    public ImageRef Clone() => JsonConvert.DeserializeObject<ImageRef>(JsonConvert.SerializeObject(this))!;
}

// user-made effects A deliberately small vocabulary: WHAT it is (Kind), WHERE it grows from (Shape), WHICH WAY it goes (Direction), what each bit looks like (Particle) and a handful of 0..2 sliders. Everything the shipped effects do - rising embers, edge motes, the spinning avatar rings, halos, sweeps - can be rebuilt from these, and FxPresets ships them ready-made so most people just pick one and nudge a slider.
public enum FxKind
{
    Particles,   // little things that spawn on the shape and travel
    Spinner,     // rotating segmented rings around the shape
    Glow,        // a breathing halo hugging the shape
    Sweep,       // a soft band of light passing across the element
    Shimmer,     // a bright spot that runs along the rim
    Rays,        // flickering lines radiating from the shape
}

public enum FxShape
{
    Ring,    // the circle inscribed in the element (avatars)
    Edge,    // the element's outline
    Bottom,  // its bottom edge
    Top,     // its top edge
    Center,  // its middle point
}

public enum FxDirection
{
    Up, Down, Left, Right,
    Outward,           // away from the shape's centre
    Inward,            // toward it
    Clockwise, Counterclockwise,
    Still,             // stays put and just twinkles
}

public enum FxParticle { Dot, Spark, Ember, Shard, Mote, Ring }

// The outline an Edge / Ring effect follows: the element's own (a circle on avatars, a rectangle elsewhere), or forced.
public enum FxOutline { Auto, Circle, Square }

public sealed class FxDef
{
    public string Id   { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "New effect";
    public FxKind Kind           { get; set; } = FxKind.Particles;
    public FxShape Shape         { get; set; } = FxShape.Bottom;
    public FxDirection Direction { get; set; } = FxDirection.Up;
    public FxParticle Particle   { get; set; } = FxParticle.Ember;
    public FxOutline Outline     { get; set; } = FxOutline.Auto;
    public bool UseAccent        { get; set; } = true;     // colour follows the theme accent
    public ThemeColor Color      { get; set; } = new(0.6f, 0.3f, 1f);
    // All 0..2, 1 = the built-in look.
    public float Amount   { get; set; } = 1f;   // how many / how many rings
    public float Speed    { get; set; } = 1f;
    public float Size     { get; set; } = 1f;   // particle / band / line size
    public float Spread   { get; set; } = 1f;   // how far it travels or reaches
    public float Strength { get; set; } = 1f;   // brightness
    public bool Glow      { get; set; } = true; // soft halo around each bit
    // Spinner only.
    public int Rings      { get; set; } = 2;
    public int Segments   { get; set; } = 3;
    public float Coverage { get; set; } = 0.6f; // how much of each ring is drawn (0..1)
    public bool Ticks     { get; set; } = true; // dial marks outside the rings

    [JsonIgnore] public bool IsPreset => Id.StartsWith("preset:", StringComparison.Ordinal);

    public FxDef Clone()
    {
        var c = JsonConvert.DeserializeObject<FxDef>(JsonConvert.SerializeObject(this))!;
        c.Id = Guid.NewGuid().ToString("N")[..8];
        return c;
    }
}

// Per-element look overrides. Anything left at its default falls back to the theme, so a style only changes what the author touched.
public sealed class ElementStyle
{
    public bool OverrideAccent { get; set; } = false;
    public ThemeColor Accent   { get; set; } = new(0.42f, 0.86f, 1.00f);
    public bool OverrideText   { get; set; } = false;
    public ThemeColor Text     { get; set; } = new(0.92f, 0.96f, 1.00f);
    public float Alpha         { get; set; } = 1f;      // 0..1 opacity of the element
    public bool Glow           { get; set; } = false;   // soft accent halo behind it
    public bool Chrome         { get; set; } = false;   // its own material plate behind it
    public float Padding       { get; set; } = 6f;      // px around content when Chrome/Glow
    // Which material paints the plate (null = the theme's own).
    public ThemeMaterial? PlateMaterial { get; set; } = null;
    // Edge mask painted over the element's rim after its content.
    public EdgeMask Mask       { get; set; } = EdgeMask.None;
    // Effects layered on the element.
    public bool EdgeMotes      { get; set; } = false;   // Aether motes shed from the rim
    public bool Embers         { get; set; } = false;   // purple-ish embers rising off the bottom
    public bool ScanLines      { get; set; } = false;   // Allagan scan lines inside
    public bool Sweep          { get; set; } = false;   // Allagan scan sweep inside
    public bool Shimmer        { get; set; } = false;   // wandering bright arc along the rim
    // Custom / preset effects layered on the element, by FxDef id ("preset:<name>" or the id of one of the document's Effects).
    public List<string> Fx     { get; set; } = new();
    // Theme images on this element.
    public ImageRef? Background { get; set; }   // behind the content
    public ImageRef? Frame      { get; set; }   // over the rim (9-slice friendly)
    public ImageRef? MaskImage  { get; set; }   // cuts the avatar / image (its transparency = the shape)

    public ElementStyle Clone() => JsonConvert.DeserializeObject<ElementStyle>(JsonConvert.SerializeObject(this))!;
}

public sealed class ElementDef
{
    public ElementType Type { get; set; } = ElementType.Text;
    // Placement. Flow elements stack top-to-bottom in panel order; a Free element sits at X/Y with size W/H, all fractions of the panel's inner (inset) area, and can be dragged in the editor.
    public bool Free   { get; set; } = false;
    public float X     { get; set; } = 0f;
    public float Y     { get; set; } = 0f;
    public float W     { get; set; } = 0.5f;
    public float H     { get; set; } = 0.2f;
    // Uniform scale for text and images (1 = theme default).
    public float Scale { get; set; } = 1f;
    // Locked elements can't be picked, dragged or resized in the scene (still editable from the inspector list).
    public bool Locked { get; set; } = false;
    public ElementStyle? Style { get; set; }
    // Per-page looks: a style that applies only while a given dossier page (section tab, by name) is showing. Falls back to Style otherwise.
    public Dictionary<string, ElementStyle> PageStyles { get; set; } = new();
    public ElementStyle? StyleFor(string? page)
        => !string.IsNullOrEmpty(page) && PageStyles != null && PageStyles.TryGetValue(page, out var s) ? s : Style;
    // Generic knobs; which ones matter depends on Type.
    public string Text      { get; set; } = "";          // Text / LinkReadout override
    public TextAlign Align  { get; set; } = TextAlign.Center;
    public float Size       { get; set; } = 1f;          // Avatar: diameter px at 1.0 scale (0 = auto); Text: font scale; Spacer: px; Divider: unused
    public NavStyle Nav     { get; set; } = NavStyle.Horizontal;
    public bool Inline      { get; set; } = false;       // Controls: row instead of stack
    public bool Fill        { get; set; } = false;       // SectionBody: take remaining height (default true for body)
    public bool ShowNumbers { get; set; } = true;        // SectionNav numbering
    public float Height     { get; set; } = 0f;          // fixed height override px (0 = natural)
    public bool ThemeRing   { get; set; } = true;        // Avatar: draw the theme's own ring / spinner treatment
    public ImageRef? Image  { get; set; }                // Image element: what to show

    // Stable identity within the panel (pin targets refer to it).
    public string Id        { get; set; } = NewId();
    // Pin ordering: placed relative to another element of the same panel, from that element's rendered rect this frame. None = free / flow as usual.
    public string PinTo     { get; set; } = "";
    public PinSide Pin      { get; set; } = PinSide.None;
    public float PinGap     { get; set; } = 6f;           // px at 1.0 scale
    public PinAlign PinAlign { get; set; } = PinAlign.Center;
    // Pinned Below: stretch down to the bottom of the panel's inner area.
    public bool FillRemaining { get; set; } = false;
    [JsonIgnore] public bool HasPin => Pin != PinSide.None && !string.IsNullOrEmpty(PinTo) && PinTo != Id;

    public static string NewId() => Guid.NewGuid().ToString("N")[..8];

    public ElementDef Clone() => JsonConvert.DeserializeObject<ElementDef>(JsonConvert.SerializeObject(this))!;
}

public enum PinSide { None, Below, Above, LeftOf, RightOf }
public enum PinAlign { Start, Center, End }

// Resolves pin ordering for one panel's element list. Shared by the HUD, the editor and the thumbnails so they all agree.
public static class PinLayout
{
    // `pinned[i]` is true when element i has a pin whose target exists in the list and which is not part of a cycle (those fall back to free placement). The returned order lists every index with each pinned element after the element it is pinned to.
    public static List<int> Order(IList<ElementDef> els, out bool[] pinned)
    {
        var n = els.Count;
        pinned = new bool[n];
        var idx = new Dictionary<string, int>();
        for (int i = 0; i < n; i++)
            if (!string.IsNullOrEmpty(els[i].Id)) idx.TryAdd(els[i].Id, i);
        var target = new int[n];
        for (int i = 0; i < n; i++)
            target[i] = els[i].HasPin && idx.TryGetValue(els[i].PinTo, out var t) && t != i ? t : -1;
        for (int i = 0; i < n; i++)
        {
            if (target[i] < 0) continue;
            var c = target[i]; var steps = 0; var cyc = false;
            while (c >= 0 && steps++ <= n) { if (c == i) { cyc = true; break; } c = target[c]; }
            pinned[i] = !cyc;
        }
        var order = new List<int>(n);
        var seen = new bool[n];
        var pin = pinned;
        void Visit(int i)
        {
            if (seen[i]) return;
            seen[i] = true;
            if (pin[i]) Visit(target[i]);
            order.Add(i);
        }
        for (int i = 0; i < n; i++) Visit(i);
        return order;
    }

    // Ids that some valid pin points at.
    public static HashSet<string> Targets(IList<ElementDef> els, bool[] pinned)
    {
        var s = new HashSet<string>();
        for (int i = 0; i < els.Count; i++) if (pinned[i]) s.Add(els[i].PinTo);
        return s;
    }

    // Top-left of an element of `size` pinned to the target rect.
    public static Vector2 Place(ElementDef e, Vector2 tMin, Vector2 tMax, Vector2 size, float gap)
    {
        float Along(float a0, float a1, float len) => e.PinAlign switch
        {
            PinAlign.Start => a0,
            PinAlign.End => a1 - len,
            _ => (a0 + a1 - len) * 0.5f,
        };
        return e.Pin switch
        {
            PinSide.Below   => new Vector2(Along(tMin.X, tMax.X, size.X), tMax.Y + gap),
            PinSide.Above   => new Vector2(Along(tMin.X, tMax.X, size.X), tMin.Y - gap - size.Y),
            PinSide.LeftOf  => new Vector2(tMin.X - gap - size.X, Along(tMin.Y, tMax.Y, size.Y)),
            PinSide.RightOf => new Vector2(tMax.X + gap, Along(tMin.Y, tMax.Y, size.Y)),
            _ => tMin,
        };
    }

    public static bool Fills(ElementDef e) => e.Pin == PinSide.Below && e.FillRemaining;

    // Would pinning `e` to `target` create a cycle?
    public static bool DependsOn(IList<ElementDef> els, ElementDef target, ElementDef e)
    {
        var cur = target; var guard = 0;
        while (cur != null && guard++ <= els.Count)
        {
            if (cur == e) return true;
            if (!cur.HasPin) return false;
            var id = cur.PinTo;
            cur = els.FirstOrDefault(x => x.Id == id);
        }
        return false;
    }
}

public sealed class PanelDef
{
    public string Id     { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name   { get; set; } = "Panel";
    public string Header { get; set; } = "";        // empty = no header
    public bool Chrome   { get; set; } = true;      // draw the material's chrome
    public bool Solid    { get; set; } = false;     // opaque surface plate under the chrome (unless a profile background shows)
    public PanelAnchor Anchor { get; set; } = PanelAnchor.TopLeft;
    // Fractions of the viewport work area. X/Y are the panel's anchor corner offset from the anchor edge (so TopRight X=0.02 means 2% in from the right edge).
    public float X { get; set; } = 0.55f;
    public float Y { get; set; } = 0.10f;
    public float W { get; set; } = 0.30f;
    public float H { get; set; } = 0.60f;
    public float Depth { get; set; } = 0.5f;         // parallax depth 0..1
    public float Delay { get; set; } = 0f;           // entrance stagger seconds
    public bool  Bob   { get; set; } = true;
    public bool  Locked { get; set; } = false;       // not draggable / resizable in the scene
    // Parenting: when set to another panel's Id, X/Y/W/H become fractions of THAT panel's rect (anchor still applies, relative to the parent) and the panel rides along with its parent - moving, floating and parallaxing with it. Children always draw above their parent.
    public string Parent { get; set; } = "";
    // With a parent: Inside uses X/Y/W/H as fractions of the parent's rect (anchor applies). An edge attachment glues the panel to the outside of that edge, Gap px away; X (top/bottom) or Y (left/right) slides it along the edge, W/H stay fractions of the parent's size.
    public PanelAttach Attach { get; set; } = PanelAttach.Inside;
    public float Gap { get; set; } = 8f;
    // Draw the child underneath its parent instead of on top.
    public bool BehindParent { get; set; } = false;
    // The controls panel (notes / equipment / like / report / close). Its content is fixed but it can be placed, sized, parented and styled like any panel. It can't be deleted: reviewers require the controls to be visible before a theme is accepted.
    public const string ControlsId = "controls";
    [JsonIgnore] public bool IsControls => Id == ControlsId;
    public string CloseLabel { get; set; } = "";     // controls panel: blank = theme's
    // Theme images on the panel.
    public ImageRef? Background { get; set; }   // over the material fill, under the content
    public ImageRef? Frame      { get; set; }   // over everything, along the rim
    public ImageRef? MaskImage  { get; set; }   // cuts the background image
    // Stacking order among siblings (higher = in front). Ties go to the larger panel first so smaller ones layer over it.
    public int Layer { get; set; } = 0;
    public List<ElementDef> Elements { get; set; } = new();

    public PanelDef Clone()
    {
        var c = JsonConvert.DeserializeObject<PanelDef>(JsonConvert.SerializeObject(this))!;
        c.Id = Guid.NewGuid().ToString("N")[..8];
        return c;
    }
}

public sealed class ThemeDocument
{
    public const int CurrentVersion = 1;

    // Identity. ServerId is 0 until the theme exists on the server; LocalId is stable for drafts on this machine.
    public int    ServerId    { get; set; } = 0;
    public int    ServerVersion { get; set; } = 0;   // which published version this copy is (0 = unknown / local)
    public string LocalId     { get; set; } = Guid.NewGuid().ToString("N");
    public int    Version     { get; set; } = CurrentVersion;
    public string Name        { get; set; } = "New Theme";
    public string Description { get; set; } = "";
    public string Author      { get; set; } = "";
    public bool   Official    { get; set; } = false;   // shipped with the plugin
    public string BasedOn     { get; set; } = "";      // material / built-in it was forked from
    public int    BasedOnId   { get; set; } = 0;       // gallery theme it was duplicated from (0 = none)
    // Publishing choices. Both kinds go through review; only Gallery themes can be installed by others. AllowRemix: may others publish variants?
    public string Visibility  { get; set; } = "gallery";   // "gallery" | "private"
    public bool   AllowRemix  { get; set; } = true;
    // Set on a duplicate of a theme whose author forbids public variants: it can be edited and worn, never published to the gallery.
    public bool   RemixLocked { get; set; } = false;
    [JsonIgnore] public bool IsPrivate => Visibility == "private";

    public ThemeMaterial Material { get; set; } = ThemeMaterial.Allagan;
    public ThemePalette  Palette  { get; set; } = new();
    public ThemeStyle    Style    { get; set; } = new();
    public ThemeVfx      Vfx      { get; set; } = new();
    // The profile layout. `Panels` normally means this list; while the editor works on another scene (EditScene 1 = loading screen, 2 = tooltip) it points at that scene's list instead, so the same tools apply. Only the profile list is serialised under "Panels".
    [JsonProperty("Panels")] private List<PanelDef> _panels = new();
    [JsonIgnore] public List<PanelDef> ProfilePanels { get => _panels; set => _panels = value ?? new(); }
    [JsonIgnore] public int EditScene { get; set; } = 0;
    [JsonIgnore] public List<PanelDef> Panels
    {
        get => EditScene == 1 ? LoadingPanels : EditScene == 2 ? TooltipPanels : _panels;
        set { if (EditScene == 1) LoadingPanels = value ?? new(); else if (EditScene == 2) TooltipPanels = value ?? new(); else _panels = value ?? new(); }
    }
    // Empty = the material's built-in loading screen / tooltip.
    public List<PanelDef> LoadingPanels { get; set; } = new();
    public List<PanelDef> TooltipPanels { get; set; } = new();
    [JsonIgnore] public bool HasLoadingScene => LoadingPanels != null && LoadingPanels.Count > 0;
    [JsonIgnore] public bool HasTooltipScene => TooltipPanels != null && TooltipPanels.Count > 0;

    // Fresh single-panel starting points for the extra scenes.
    public static List<PanelDef> DefaultLoadingScene() => new()
    {
        new PanelDef
        {
            Name = "Loading", Header = "", Anchor = PanelAnchor.Center, X = 0.5f, Y = 0.5f, W = 0.28f, H = 0.30f, Bob = false,
            Elements = { new ElementDef { Type = ElementType.Avatar, Size = 72f }, new ElementDef { Type = ElementType.Title, Size = 1.3f }, new ElementDef { Type = ElementType.LoadingProgress } },
        },
    };
    public static List<PanelDef> DefaultTooltipScene() => new()
    {
        new PanelDef
        {
            Name = "Tooltip", Header = "", Anchor = PanelAnchor.TopLeft, X = 0f, Y = 0f, W = 1f, H = 1f, Bob = false,
            Elements = { new ElementDef { Type = ElementType.Avatar, Size = 80f }, new ElementDef { Type = ElementType.Title, Size = 1.25f }, new ElementDef { Type = ElementType.Divider }, new ElementDef { Type = ElementType.TooltipInfo } },
        },
    };
    // Effects the author made (or copied from a preset and tweaked).
    public List<FxDef>    Effects { get; set; } = new();
    // Images the author added, and how gallery pictures are dressed.
    public List<ThemeAsset> Assets { get; set; } = new();
    public ImageRef? GalleryMask  { get; set; }
    public ImageRef? GalleryFrame { get; set; }
    public ImageRef? AvatarMask   { get; set; }   // default avatar cut (an element's own Mask overrides)
    public ImageRef? AvatarFrame  { get; set; }

    // Drops references to images that no longer exist.
    public void ScrubImageRefs()
    {
        bool Ok(ImageRef? r) => r == null || !r.IsSet || Assets.Exists(a => a.Id == r.Asset);
        ImageRef? Fix(ImageRef? r) => Ok(r) ? r : null;
        GalleryMask = Fix(GalleryMask); GalleryFrame = Fix(GalleryFrame); AvatarMask = Fix(AvatarMask); AvatarFrame = Fix(AvatarFrame);
        foreach (var p in AllScenePanels())
        {
            p.Background = Fix(p.Background); p.Frame = Fix(p.Frame); p.MaskImage = Fix(p.MaskImage);
            foreach (var e in p.Elements)
            {
                e.Image = Fix(e.Image);
                if (e.Style != null) { e.Style.Background = Fix(e.Style.Background); e.Style.Frame = Fix(e.Style.Frame); e.Style.MaskImage = Fix(e.Style.MaskImage); }
            }
        }
    }

    // Bookkeeping (not sent to the server).
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented);

    public static ThemeDocument? FromJson(string json)
    {
        try { return JsonConvert.DeserializeObject<ThemeDocument>(json)?.Normalize(); }
        catch { return null; }
    }

    // The section body (the dossier) always carries the profile controls - notes, equipment, like, report and close - so a viewer can never be left without them. Older documents placed those as separate elements; they are dropped here, and a body is added if the document has none.
    public static readonly ElementType[] LegacyControlTypes = { ElementType.Controls, ElementType.LikeButton, ElementType.CloseButton };

    public ThemeDocument Normalize()
    {
        LoadingPanels ??= new(); TooltipPanels ??= new();
        // Every element gets an Id unique within its panel (pins refer to it).
        foreach (var p in AllScenePanels())
        {
            var ids = new HashSet<string>();
            foreach (var e in p.Elements)
                while (string.IsNullOrEmpty(e.Id) || !ids.Add(e.Id)) e.Id = ElementDef.NewId();
        }
        foreach (var p in ProfilePanels) p.Elements.RemoveAll(e => Array.IndexOf(LegacyControlTypes, e.Type) >= 0);
        if (!ProfilePanels.Any(p => p.Elements.Any(e => e.Type == ElementType.SectionBody)))
        {
            var host = ProfilePanels.OrderByDescending(p => p.W * p.H).FirstOrDefault();
            if (host == null)
            {
                host = new PanelDef { Name = "Dossier", Header = "$section", Anchor = PanelAnchor.TopRight, X = 0.05f, Y = 0.12f, W = 0.34f, H = 0.7f };
                ProfilePanels.Add(host);
            }
            host.Elements.Add(new ElementDef { Type = ElementType.SectionBody, Fill = true });
        }
        // The controls panel is added once by default; an author may remove it (the theme then won't pass review) and add it back later. (Superseded) Profile controls - close, report, notes, link, like, equipment - are drawn by the HUD at fixed spots on the main panel, so document-driven controls panels and link readouts are dropped.
        ProfilePanels.RemoveAll(p => p.IsControls);
        foreach (var p in ProfilePanels) p.Elements.RemoveAll(e => e.Type == ElementType.LinkReadout);
        return this;
    }

    // Set when the author deliberately deleted the controls panel, so it isn't silently re-added on the next load.
    public bool ControlsRemoved { get; set; } = false;
    public bool HasControls => true;   // static HUD controls on every theme

    // The container: the root panel holding the section body. Its size is fixed by the HUD; every other panel is placed and clamped inside it. Always a ROOT panel: a nested section body promotes its root ancestor, otherwise the ancestor would wait on main while main waits on it.
    [JsonIgnore] public PanelDef? MainPanel
    {
        get
        {
            var p = DossierPanel ?? ProfilePanels.FirstOrDefault(q => !q.IsControls && string.IsNullOrEmpty(q.Parent))
                ?? ProfilePanels.FirstOrDefault(q => !q.IsControls);
            var guard = 0;
            while (p != null && !string.IsNullOrEmpty(p.Parent) && guard++ < 64)
            {
                var up = ProfilePanels.FirstOrDefault(q => q.Id == p.Parent);
                if (up == null || up.IsControls) break;
                p = up;
            }
            return p;
        }
    }
    // A base layout: one root container (plus optional children inside it). Older multi-panel documents (nav / identity / dossier side by side) are shown as the Standard layout in the theme's colours instead.
    [JsonIgnore] public bool IsSinglePanel
        => ProfilePanels.Count(p => string.IsNullOrEmpty(p.Parent) && !p.IsControls) == 1;

    // Rebuilds the profile scene as the single-panel base layout, keeping palette, style, effects and assets.
    public void ConvertToBaseLayout()
    {
        ProfilePanels = BuiltinDocuments.Standard();
        Normalize();
    }

    public IEnumerable<PanelDef> AllScenePanels() => ProfilePanels.Concat(LoadingPanels ?? new()).Concat(TooltipPanels ?? new());

    public PanelDef AddControlsPanel()
    {
        // Default spot: hanging just below the dossier, full width.
        var dossier = DossierPanel;
        var p = new PanelDef
        {
            Id = PanelDef.ControlsId, Name = "Controls", Header = "", Chrome = true, Bob = false,
            Parent = dossier?.Id ?? "", Anchor = PanelAnchor.TopLeft, Attach = dossier != null ? PanelAttach.Below : PanelAttach.Inside,
            X = 0f, Y = dossier != null ? 0f : 0.75f, W = dossier != null ? 1f : 0.3f, H = dossier != null ? 0.18f : 0.14f,
        };
        ProfilePanels.Add(p);
        ControlsRemoved = false;
        return p;
    }

    // The panel the controls hang off: the one holding the section body.
    public PanelDef? DossierPanel => ProfilePanels.FirstOrDefault(p => !p.IsControls && p.Elements.Any(e => e.Type == ElementType.SectionBody));

    public bool HasSectionBody => ProfilePanels.Any(p => p.Elements.Any(e => e.Type == ElementType.SectionBody));
    public int SectionBodyCount => ProfilePanels.Sum(p => p.Elements.Count(e => e.Type == ElementType.SectionBody));

    public ThemeDocument Clone()
    {
        var c = FromJson(ToJson())!;
        c.LocalId = Guid.NewGuid().ToString("N");
        c.ServerId = 0;
        c.ServerVersion = 0;
        c.Official = false;
        return c;
    }

    // A copy of someone else's published theme, honouring their remix rule.
    public ThemeDocument Remix(int sourceServerId, bool sourceAllowsRemix)
    {
        var c = Clone();
        c.BasedOnId = sourceServerId;
        c.RemixLocked = !sourceAllowsRemix;
        if (c.RemixLocked) c.Visibility = "private";
        return c;
    }

    // Stable string reference used in config / profile settings. builtin:<index> one of the shipped materials in their native layout local:<localId> a draft or installed copy on this machine server:<id> a gallery theme (fetched + cached)
    public string Ref => ServerId > 0 ? "server:" + ServerId : "local:" + LocalId;
}
