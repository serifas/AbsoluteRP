using System;
using System.Collections.Generic;
using System.Numerics;
using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI;
using AbsoluteRP.Windows.Inventory;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using System.Linq;
using AbsoluteRP.Immersive.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Networking;
using Rolspeace.VFX;
using AbsoluteRP.Network;

namespace AbsoluteRP.Immersive;

// Immersive presentation of another player's profile. The active theme decides the composition (HudLayout) as well as the skin, so each theme is a different piece of furniture, not a recolor: Cluster (Allagan) nav strip / identity column / dossier - a HUD. Tablet (Nymian) one carved slab: medallion breaking the top edge, stone tabs down the left, controls along the bottom. Tome (Sharlayan) two facing pages with index tabs off the right page; left page is the portrait, right the text. Shards (Voidtouched) an off-center dossier with the identity panel overlapping its corner, every tab its own fragment, controls hanging off the side. Motion differs too: cluster panels slide in from the right, the tablet rises and settles, the tome unfolds from the spine and turns pages, shards burn in at random and glitch. Entered from TargetProfileWindow.Draw() when the immersive setting is on - the classic window is untouched otherwise.
public static class ImmersiveHud
{
    private const ImGuiWindowFlags PanelFlags =
          ImGuiWindowFlags.NoTitleBar
        | ImGuiWindowFlags.NoResize
        | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoCollapse
        | ImGuiWindowFlags.NoScrollbar
        | ImGuiWindowFlags.NoScrollWithMouse
        | ImGuiWindowFlags.NoSavedSettings
        | ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoFocusOnAppearing
        // The HUD is a background layer: clicking a panel never lifts it above a normal window, and any normal window you click rises above the whole HUD. Without this every panel reorders itself independently against the editor / main window on each click.
        | ImGuiWindowFlags.NoBringToFrontOnFocus;

    private enum Motion { SlideRight, RiseUp, Unfold, Burn, Drop, Unroll, Drift }

    private static Motion ExitMotion() => MotionFor(_theme.Exit);
    private static Motion MotionFor(PanelEntrance e) => e switch
    {
        PanelEntrance.Rise   => Motion.RiseUp,
        PanelEntrance.Unroll => Motion.Unroll,
        PanelEntrance.Burn   => Motion.Burn,
        PanelEntrance.Drift  => Motion.Drift,
        _ => Motion.SlideRight,
    };
    private static Motion EntranceMotion() => _theme.Entrance switch
    {
        PanelEntrance.Rise   => Motion.RiseUp,
        PanelEntrance.Unroll => Motion.Unroll,
        PanelEntrance.Burn   => Motion.Burn,
        PanelEntrance.Drift  => Motion.Drift,
        _                    => Motion.SlideRight,
    };

    private static int _colorsPushed;
    private static int _varsPushed;

    // Frame state.
    private static Vector2 _vpPos, _vpSize;
    private static ImmersiveTheme _theme = ImmersiveThemes.Default;
    private static string _name = string.Empty, _world = string.Empty;
    private static string _lastPageId = string.Empty;

    // editor preview When EditMode is on, panels take no input, don't animate or drift, and every panel / element reports its screen rect so the editor can hit-test clicks and draw selection outlines over the live render.
    public static bool EditMode;
    // Bumped every time the editor opens so preview windows get new names and therefore appear on top of the editor window.
    public static int PreviewSession;
    public sealed class EditRect
    {
        public string PanelId = "";
        public int ElementIndex = -1;       // -1 = the panel itself
        public Vector2 Min, Max;
        public Vector2 InnerMin, InnerSize; // panels only: content area for free elements
    }
    public static readonly List<EditRect> EditRects = new();
    // Preview panels are scaled with a per-window font scale (never the global one, which leaked into whatever drew after the preview).
    private static float _previewScale = 1f;

    // A small close control in the top-right corner while a profile loads, so a load that never finishes can always be dismissed.
    // Small X in a loading card's corner; the viewport-corner button was easy to miss.
    private static void DrawCardClose(ImDrawListPtr dl, Vector2 min, Vector2 size, float a)
    {
        var S = (Func<float, float>)RsTheme.S;
        var keep = ImGui.GetCursorScreenPos();
        var b = S(22f);
        var pos = new Vector2(min.X + size.X - b - S(18f), min.Y + S(6f));
        ImGui.SetCursorScreenPos(pos);
        if (ImGui.InvisibleButton("##card_close", new Vector2(b, b))) RequestClose();
        var hov = ImGui.IsItemHovered();
        if (hov) ImGui.SetTooltip("Cancel");
        var col = ImmersiveMode.Col(hov ? ImmersiveMode.TextColor : _theme.Danger, (hov ? 1f : 0.75f) * a);
        if (hov) dl.AddRectFilled(pos, pos + new Vector2(b, b), ImmersiveMode.Col(_theme.Danger, 0.15f * a), S(4f));
        ImmersiveMode.DrawCloseGlyph(dl, pos + new Vector2(b, b) * 0.5f, S(5f), col);
        ImGui.SetCursorScreenPos(keep);
    }

    private static void DrawLoadingClose()
    {
        var S = (Func<float, float>)RsTheme.S;
        var size = new Vector2(S(150f), S(40f));
        var pos = new Vector2(_vpPos.X + _vpSize.X - size.X - S(24f), _vpPos.Y + S(24f));
        var a = BeginPanel("loading_close", null, pos, size, 0f, 0f, EntranceMotion(), header: false, chrome: false, bob: false);
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos();
        DrawCloseButton(dl, new Vector2(min.X + size.X - S(6f), min.Y + S(4f)), size.Y - S(8f), a, anchorRight: true);
        EndPanel();
    }

    // Draws one of a document's extra scenes (1 loading, 2 tooltip) with the ordinary document renderer, in the current viewport rect.
    private static void DrawScene(ThemeDocument doc, int scene, ProfileData pd)
    {
        var prev = doc.EditScene;
        doc.EditScene = scene;
        try { DrawDocument(doc, pd, new List<CustomTab>(), 0); }
        finally { doc.EditScene = prev; }
    }

    // The tooltip scene, anchored to a rect (the tooltip window's place on screen).
    private static bool _tooltipScene;   // true while a tooltip scene is drawing its panels
    public static void DrawTooltipScene(ThemeDocument doc, ImmersiveTheme theme, Vector2 rectPos, Vector2 rectSize, ProfileData pd)
    {
        _tooltipScene = true;
        var savedPos = _vpPos; var savedSize = _vpSize; var savedTheme = _theme;
        var savedName = _name; var savedWorld = _world;
        var savedProfile = TargetProfileWindow.profileData;
        try
        {
            _vpPos = rectPos; _vpSize = rectSize; _theme = theme;
            _name = pd.playerName ?? ""; _world = pd.playerWorld ?? "";
            TargetProfileWindow.profileData = pd;
            PushHudStyle();
            try { DrawScene(doc, 2, pd); }
            finally { PopHudStyle(); }
        }
        catch (Exception ex) { Plugin.PluginLog?.Debug("[ImmersiveHud] tooltip scene: " + ex.Message); }
        finally
        {
            _tooltipScene = false;
            TargetProfileWindow.profileData = savedProfile;
            _vpPos = savedPos; _vpSize = savedSize; _theme = savedTheme;
            _name = savedName; _world = savedWorld;
        }
    }

    private static TooltipData SampleTooltip() => new()
    {
        title = "Your Title", Name = "Character Name", Race = "Miqo'te", Gender = "Female", Age = "27", Height = "5'2\"", Weight = "Average",
        Alignment = 1, Personality_1 = 1, Personality_2 = 2,
    };

    // Renders `doc` into the rect [vpPos, vpPos+vpSize] at `scale` (a fraction of the real viewport) using `pd` as sample content.
    public static void DrawPreview(ThemeDocument doc, Vector2 vpPos, Vector2 vpSize, ProfileData pd, string name, string world, int sel, float scale)
    {
        var savedPos = _vpPos; var savedSize = _vpSize; var savedTheme = _theme;
        var savedName = _name; var savedWorld = _world;
        var savedProfile = TargetProfileWindow.profileData;
        var savedLayout = TargetProfileWindow.currentLayout;
        _previewScale = MathF.Max(0.35f, scale);
        EditMode = true;
        EditRects.Clear();
        try
        {
            _vpPos = vpPos; _vpSize = vpSize;
            _theme = ImmersiveThemes.FromDocument(doc);
            _name = name ?? ""; _world = world ?? "";
            TargetProfileWindow.profileData = pd;
            var tabs = (pd.customTabs ?? new List<CustomTab>()).Where(t => t != null && !string.IsNullOrEmpty(t.Name) && t.Layout != null).ToList();
            if (sel < 0 || sel >= tabs.Count) sel = 0;
            PushHudStyle();
            try { DrawDocument(doc, pd, tabs, sel); }
            finally { PopHudStyle(); }
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.Debug("[ImmersiveHud] preview: " + ex.Message);
        }
        finally
        {
            EditMode = false;
            _previewScale = 1f;
            TargetProfileWindow.profileData = savedProfile;
            TargetProfileWindow.currentLayout = savedLayout;
            _vpPos = savedPos; _vpSize = savedSize; _theme = savedTheme;
            _name = savedName; _world = savedWorld;
        }
    }

    // close sequence
    private const float CloseDurationBase = 0.9f;
    private static float CloseDuration => CloseDurationBase / MathF.Max(0.25f, _theme?.CloseSpeed ?? 1f);
    private static float? _closeStart;
    private static string _closeKey = string.Empty;

    public static void RequestClose()
    {
        if (_closeStart.HasValue) return;
        _closeStart = ImmersiveMode.Time;
    }

    private static float CloseFactor()
    {
        if (!_closeStart.HasValue) return 1f;
        var t = (ImmersiveMode.Time - _closeStart.Value) / CloseDuration;
        return 1f - Math.Clamp(t, 0f, 1f);
    }

    // entry
    public static void Draw()
    {
        if (!ImmersiveMode.IsActive) return;
        ImmersiveMode.UpdateParallax();

        if (_closeStart.HasValue && ImmersiveMode.Time - _closeStart.Value >= CloseDuration)
        {
            _closeStart = null;
            Plugin.plugin.CloseTargetWindow();
            return;
        }

        var vp = ImGui.GetMainViewport();
        _vpPos = vp.WorkPos;
        _vpSize = vp.WorkSize;
        if (_vpSize.X < 200f || _vpSize.Y < 200f) return;

        _theme = ImmersiveMode.Theme;
        var pd = TargetProfileWindow.profileData;
        _name = TargetProfileWindow.characterName ?? string.Empty;
        _world = TargetProfileWindow.characterWorld ?? string.Empty;
        var animKey = $"{_name}@{_world}/{pd?.title}/{TargetProfileWindow.ExistingProfile}/{_theme.Name}";
        Anim.ResetKey("hud", animKey);
        if (animKey != _closeKey) { _closeKey = animKey; _closeStart = null; }

        PushHudStyle();
        try
        {
            // Refused by the server: private (or no profile). Only then is the access panel shown; a profile that is merely still arriving, or is waiting behind its content warning, shows the loading screen.
            if (TargetProfileWindow.AccessDenied)
            {
                TargetProfileWindow.RequestingProfile = false;
                DrawVeil(default, 0, 0);
                DrawAccessPanel();
                return;
            }

            var stillLoading = TargetProfileWindow.IsLoading(out var tabsLoading, out var galleryLoading);
            if (!TargetProfileWindow.ExistingProfile || stillLoading)
            {
                DrawVeil(default, 0, 0);
                // A content warning arrives before anything else: it sits over the loading screen.
                TargetProfileWindow.DrawWarningPopup();
                if (_theme.Document != null && _theme.Document.HasLoadingScene)
                {
                    DrawScene(_theme.Document, 1, TargetProfileWindow.profileData ?? new ProfileData());
                    DrawLoadingClose();
                    return;
                }
                switch (_theme.NativeLayout)
                {
                    case HudLayout.Tablet: DrawLoadingTablet(tabsLoading, galleryLoading); break;
                    case HudLayout.Tome:   DrawLoadingTome(tabsLoading, galleryLoading); break;
                    case HudLayout.Shards: DrawLoadingShards(tabsLoading, galleryLoading); break;
                    case HudLayout.Standard: DrawLoadingStandard(tabsLoading, galleryLoading); break;
                    default:
                        if (_theme.ElementGlow) DrawLoadingAether(tabsLoading, galleryLoading);
                        else DrawLoadingCluster(tabsLoading, galleryLoading);
                        break;
                }
                DrawLoadingClose();
                return;
            }

            TargetProfileWindow.EnsureDefaultTextures();
            var bgTex = TargetProfileWindow.GetBackdropTexture(out var bgW, out var bgH);
            DrawVeil(bgTex, bgW, bgH);

            var tabs = TargetProfileWindow.GetVisibleTabs();
            var sel = TargetProfileWindow.SelectedTab;
            if (sel < 0 || sel >= tabs.Count) sel = 0;

            int picked = sel;
            UpdatePlacement();
            _applyPlacement = true;
            _dossierGrowsLeft = true; _dossierGrowsUp = false;
            // The viewer's size: the layout is drawn into a viewport stretched by the two factors and pinned to the top-right corner. Panel positions and sizes are fractions of that viewport, so the arrangement is kept while the contents stay their normal size.
            var savedVpPos = _vpPos; var savedVpSize = _vpSize;
            if (MathF.Abs(_layoutScale.X - 1f) > 0.001f || MathF.Abs(_layoutScale.Y - 1f) > 0.001f)
            {
                _vpSize = savedVpSize * _layoutScale;
                _vpPos = new Vector2(savedVpPos.X + savedVpSize.X - _vpSize.X, savedVpPos.Y);
            }
            _tabClicked = false;
            try
            {
            switch (_theme.Layout)
            {
                case HudLayout.Document: picked = DrawDocument(_theme.Document!, pd!, tabs, sel); break;
                case HudLayout.Tablet: picked = DrawTablet(pd!, tabs, sel); break;
                case HudLayout.Tome:   picked = DrawTome(pd!, tabs, sel); break;
                case HudLayout.Shards: picked = DrawShards(pd!, tabs, sel); break;
                case HudLayout.Standard: picked = DrawStandard(pd!, tabs, sel); break;
                default:               picked = DrawCluster(pd!, tabs, sel); break;
            }
            }
            finally
            {
                _vpPos = savedVpPos; _vpSize = savedVpSize;
                _applyPlacement = false;
            }
            // Clicking any tab (even the already-selected one) leaves the equipment/inventory inspect view and returns to the tab content.
            if (picked != TargetProfileWindow.SelectedTab || _tabClicked)
            {
                TargetProfileWindow.SelectedTab = picked;
                TargetProfileWindow.ShowEquipmentInspect = false;
            }

            TargetProfileWindow.DrawLikeDialog();
            AbsoluteRP.Windows.Profiles.RelationshipRequestPopup.Draw();
            TargetProfileWindow.ProcessDeferredOpens();
        }
        finally
        {
            PopHudStyle();
        }
    }

    // style
    private static void PushHudStyle()
    {
        var theme = _theme;
        var h = theme.Accent;
        void C(ImGuiCol id, Vector4 c) { ImGui.PushStyleColor(id, c); _colorsPushed++; }
        void V(ImGuiStyleVar id, float v) { ImGui.PushStyleVar(id, v); _varsPushed++; }
        void V2(ImGuiStyleVar id, Vector2 v) { ImGui.PushStyleVar(id, v); _varsPushed++; }

        C(ImGuiCol.Text,            theme.Text);
        C(ImGuiCol.TextDisabled,    theme.Muted);
        C(ImGuiCol.WindowBg,        new Vector4(0f, 0f, 0f, 0f));
        C(ImGuiCol.ChildBg,         new Vector4(0f, 0f, 0f, 0f));
        C(ImGuiCol.PopupBg,         theme.PopupBg);
        C(ImGuiCol.Border,          new Vector4(h.X, h.Y, h.Z, 0.35f));
        C(ImGuiCol.FrameBg,         new Vector4(h.X, h.Y, h.Z, 0.08f));
        C(ImGuiCol.FrameBgHovered,  new Vector4(h.X, h.Y, h.Z, 0.16f));
        C(ImGuiCol.FrameBgActive,   new Vector4(h.X, h.Y, h.Z, 0.24f));
        C(ImGuiCol.Button,          new Vector4(h.X, h.Y, h.Z, 0.10f));
        C(ImGuiCol.ButtonHovered,   new Vector4(h.X, h.Y, h.Z, 0.24f));
        C(ImGuiCol.ButtonActive,    new Vector4(h.X, h.Y, h.Z, 0.40f));
        C(ImGuiCol.Header,          new Vector4(h.X, h.Y, h.Z, 0.10f));
        C(ImGuiCol.HeaderHovered,   new Vector4(h.X, h.Y, h.Z, 0.20f));
        C(ImGuiCol.HeaderActive,    new Vector4(h.X, h.Y, h.Z, 0.32f));
        C(ImGuiCol.Separator,       new Vector4(h.X, h.Y, h.Z, 0.30f));
        C(ImGuiCol.ScrollbarBg,     new Vector4(0f, 0f, 0f, 0f));
        C(ImGuiCol.ScrollbarGrab,   new Vector4(h.X, h.Y, h.Z, 0.28f));
        C(ImGuiCol.ScrollbarGrabHovered, new Vector4(h.X, h.Y, h.Z, 0.45f));
        C(ImGuiCol.ScrollbarGrabActive,  new Vector4(h.X, h.Y, h.Z, 0.65f));
        C(ImGuiCol.CheckMark,       h);
        C(ImGuiCol.SliderGrab,      new Vector4(h.X, h.Y, h.Z, 0.7f));
        C(ImGuiCol.TextSelectedBg,  new Vector4(h.X, h.Y, h.Z, 0.30f));

        V(ImGuiStyleVar.WindowRounding, 0f);
        V(ImGuiStyleVar.PopupRounding, MathF.Min(theme.Rounding, RsTheme.S(6f)));
        V(ImGuiStyleVar.PopupBorderSize, 1f);
        V(ImGuiStyleVar.ChildRounding, 0f);
        V(ImGuiStyleVar.FrameRounding, MathF.Min(theme.Rounding, RsTheme.S(4f)));
        V(ImGuiStyleVar.WindowBorderSize, 0f);
        V(ImGuiStyleVar.ChildBorderSize, 0f);
        V(ImGuiStyleVar.ScrollbarSize, RsTheme.S(6f));
        V(ImGuiStyleVar.ScrollbarRounding, 0f);
        V2(ImGuiStyleVar.WindowPadding, new Vector2(RsTheme.S(16f), RsTheme.S(12f)));
    }

    private static void PopHudStyle()
    {
        if (_varsPushed > 0) ImGui.PopStyleVar(_varsPushed);
        if (_colorsPushed > 0) ImGui.PopStyleColor(_colorsPushed);
        _varsPushed = 0;
        _colorsPushed = 0;
    }

    // panel scaffolding Opens a floating panel window and paints its chrome. `motion` picks the entrance; returns the effective alpha (intro x close) with the same value already pushed as the widget alpha. viewer placement: drag the layout, resize the dossier Any panel drags the whole layout (one shared offset). The panel that hosts the section body has a corner grip that scales it.
    private static bool _applyPlacement;      // true only while a profile layout draws
    private static bool _layoutDragging, _dossierResizing;
    private static bool _placementLoaded;
    private static Vector2 _layoutOffset;      // screen pixels
    private static Vector2 _dossierScale = Vector2.One;   // kept at 1: the whole layout scales together now
    private static Vector2 _layoutScale = Vector2.One;   // width and height of the whole layout, separately
    private static bool _dossierGrowsLeft = true, _dossierGrowsUp;
    private const float DossierScaleMin = 0.3f, DossierScaleMax = 3f;   // the viewer decides how small or large the profile is

    private static void LoadPlacement()
    {
        if (_placementLoaded) return;
        var cfg = Plugin.plugin?.Configuration;
        if (cfg == null) return;
        _placementLoaded = true;
        _layoutOffset = new Vector2(cfg.ImmersiveLayoutOffsetX, cfg.ImmersiveLayoutOffsetY) * RsTheme.Scale;
        // The viewer's stretch of the whole container (contents keep their size).
        _layoutScale = new Vector2(
            Math.Clamp(cfg.ImmersiveDossierScaleX <= 0f ? 1f : cfg.ImmersiveDossierScaleX, DossierScaleMin, DossierScaleMax),
            Math.Clamp(cfg.ImmersiveDossierScaleY <= 0f ? 1f : cfg.ImmersiveDossierScaleY, DossierScaleMin, DossierScaleMax));
        _dossierScale = Vector2.One;
    }

    private static void SavePlacement()
    {
        var cfg = Plugin.plugin?.Configuration;
        if (cfg == null) return;
        var sc = MathF.Max(0.01f, RsTheme.Scale);
        cfg.ImmersiveLayoutOffsetX = _layoutOffset.X / sc; cfg.ImmersiveLayoutOffsetY = _layoutOffset.Y / sc;
        cfg.ImmersiveDossierScaleX = _layoutScale.X; cfg.ImmersiveDossierScaleY = _layoutScale.Y;
        cfg.Save();
    }

    public static void ResetLayoutPlacement()
    {
        _layoutOffset = Vector2.Zero; _dossierScale = Vector2.One; _layoutScale = Vector2.One; _placementLoaded = true;
        SavePlacement();
    }

    // Once per frame, before the layout draws.
    private static void UpdatePlacement()
    {
        LoadPlacement();
        var down = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        if (_layoutDragging)
        {
            if (down) _layoutOffset += ImGui.GetIO().MouseDelta;
            else { _layoutDragging = false; SavePlacement(); }
        }
        if (_dossierResizing && !down) { _dossierResizing = false; SavePlacement(); }
        // Keep the layout reachable.
        _layoutOffset.X = Math.Clamp(_layoutOffset.X, -_vpSize.X * 0.9f, _vpSize.X * 0.4f);
        _layoutOffset.Y = Math.Clamp(_layoutOffset.Y, -_vpSize.Y * 0.4f, _vpSize.Y * 0.8f);
    }

    // At the end of every panel: a press on empty panel space (not on a button, tab, image or scrollbar) starts dragging the whole layout.
    private static void HandlePanelDrag()
    {
        if (!_applyPlacement || _layoutDragging || _dossierResizing) return;
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left)) return;
        if (!ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows)) return;
        if (ImGui.IsAnyItemHovered() || ImGui.IsAnyItemActive()) return;
        _layoutDragging = true;
    }

    // Size of the panel hosting the section body, after the viewer's scale.
    private static Vector2 ScaleDossier(Vector2 size)
    {
        if (EditMode) return size;
        return new Vector2(MathF.Min(size.X * _dossierScale.X, _vpSize.X * 0.96f), MathF.Min(size.Y * _dossierScale.Y, _vpSize.Y * 0.96f));
    }

    // Corner grip on the current (dossier) panel - bottom-right, like any resizable window. Panels anchored to the right (or bottom) edge grow away from the grip, so the layout offset moves with the change and the opposite edge stays put under the cursor's drag.
    private static void DrawDossierGrip(float a)
    {
        if (EditMode || !_applyPlacement) return;
        var S = (Func<float, float>)RsTheme.S;
        var wMin = ImGui.GetWindowPos(); var wSize = ImGui.GetWindowSize(); var wMax = wMin + wSize;
        var g = S(16f);
        var gMin = new Vector2(wMax.X - g - S(2f), wMax.Y - g - S(2f));
        var keep = ImGui.GetCursorPos();
        ImGui.SetCursorScreenPos(gMin);
        ImGui.InvisibleButton("##hud_dossier_grip", new Vector2(g, g));
        var hov = ImGui.IsItemHovered(); var act = ImGui.IsItemActive();
        if (act)
        {
            _dossierResizing = true;
            var d = ImGui.GetIO().MouseDelta;
            // The drag stretches the whole layout's width and height separately. Panels keep their places relative to each other; their contents (text, buttons, avatars) keep their size.
            var baseSize = new Vector2(wSize.X / MathF.Max(0.01f, _layoutScale.X), wSize.Y / MathF.Max(0.01f, _layoutScale.Y));
            var oldScale = _layoutScale;
            _layoutScale.X = Math.Clamp(_layoutScale.X + d.X / MathF.Max(1f, baseSize.X), DossierScaleMin, DossierScaleMax);
            _layoutScale.Y = Math.Clamp(_layoutScale.Y + d.Y / MathF.Max(1f, baseSize.Y), DossierScaleMin, DossierScaleMax);
            // The layout is laid out from the right edge, so a wider panel would grow to the left. Shift it by the change so the left edge stays put and the right edge follows the grip under the cursor.
            _layoutOffset.X += (_layoutScale.X - oldScale.X) * baseSize.X;
        }
        if (hov || act)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNwse);
            if (hov && !act) ImmersiveMode.Tooltip("Drag to resize the profile (width and height)");
        }
        var dl = ImGui.GetWindowDrawList();
        var col = ImmersiveMode.Col(ImmersiveMode.Accent, (hov || act ? 0.95f : 0.45f) * a);
        var o = wMax - new Vector2(S(4f), S(4f));
        for (int i = 1; i <= 3; i++)
            dl.AddLine(o - new Vector2(i * S(4f), 0f), o - new Vector2(0f, i * S(4f)), col, 1.5f);
        ImGui.SetCursorPos(keep);
    }

    // Panel whose frame image is drawn at EndPanel (set by BeginPanel).
    private static PanelDef? _framePanel;
    private static Vector2 _frameMin, _frameMax;
    private static float _frameAlpha;

    private static float BeginPanel(string id, string? label, Vector2 pos, Vector2 size, float depth, float delay,
                                    Motion motion, bool header = true, bool chrome = true, bool bob = true, bool ride = false, PanelDef? def = null, bool backdrop = false)
    {
        var os = 1f / MathF.Max(0.25f, _theme.OpenSpeed);
        var intro = motion == Motion.RiseUp || motion == Motion.Drop
            ? Anim.Eased("hud/" + id, 0.75f * os, Anim.Ease.OutBack, delay * os)
            : Anim.Eased("hud/" + id, 0.62f * os, Anim.Ease.OutQuint, delay * os);
        var introA = Anim.Eased("hud/" + id + ".a", 0.5f * os, Anim.Ease.OutCubic, delay * os);
        var close = CloseFactor();
        // The closing motion is the theme's exit (the entrance backwards by default).
        var exit = EditMode ? motion : ExitMotion();
        if (exit == Motion.Unfold && motion != Motion.Unfold) exit = Motion.SlideRight;
        var t = introA * close;
        if (EditMode) { intro = 1f; close = 1f; t = 1f; }

        // `ride`: the position is already on a moving parent - add no parallax, jitter or float of our own so we stay glued to it.
        if (_applyPlacement && !EditMode && !ride) pos += _layoutOffset;
        var p = EditMode || ride ? pos : pos + ImmersiveMode.ParallaxOffset(depth) + _theme.PanelJitter(id);
        if (bob && !EditMode && !ride)
        {
            var phase = pos.X * 0.01f + pos.Y * 0.013f;
            p.Y += ImmersiveMode.IdleBob(phase) * depth * _theme.FloatAmount;
            if (_theme.FloatAmount > 1.5f)
                p.X += MathF.Sin(ImmersiveMode.Time * 0.6f + phase * 1.7f) * RsTheme.S(1.6f) * depth;
        }
        var sz = size;
        var reveal = 1f;
        switch (motion)
        {
            case Motion.Drift:
                p.X -= (1f - intro) * RsTheme.S(30f);
                p.Y += (1f - intro) * RsTheme.S(44f);
                break;
            case Motion.Unroll:
                // Height grows from the top edge; the theme draws the rolled remainder at the leading (bottom) edge.
                reveal *= intro;
                break;
            case Motion.SlideRight:
                p.X += (1f - intro) * RsTheme.S(56f);
                break;
            case Motion.RiseUp:
                p.Y += (1f - intro) * RsTheme.S(90f);
                break;
            case Motion.Drop:
                p.Y -= (1f - intro) * RsTheme.S(60f);
                break;
            case Motion.Unfold:
                reveal *= intro;
                break;
            case Motion.Burn:
                // No displacement; the theme uses alpha as the dissolve.
                break;
        }
        switch (exit)
        {
            case Motion.Drift:      p.Y -= (1f - close) * RsTheme.S(30f); break;
            case Motion.Unroll:
            case Motion.Unfold:     reveal *= close; break;
            case Motion.SlideRight: p.X += (1f - close) * RsTheme.S(24f); break;
            case Motion.RiseUp:     p.Y += (1f - close) * RsTheme.S(40f); break;
            case Motion.Drop:       p.Y -= (1f - close) * RsTheme.S(30f); break;
            case Motion.Burn:       break;
        }
        if (motion == Motion.Unfold || exit == Motion.Unfold)
        {
            // Width grows from the right edge (the spine side).
            var f = MathF.Max(0.02f, reveal);
            sz.X = MathF.Max(8f, size.X * f);
            p.X += size.X - sz.X;
            reveal = 1f;
        }
        else if (motion == Motion.Unroll || exit == Motion.Unroll)
        {
            reveal = MathF.Max(0.02f, reveal);
            sz.Y = MathF.Max(8f, size.Y * reveal);
        }
        else reveal = 1f;

        if (EditMode)
        {
            // Editor preview: panels are CHILD windows of the editor, so they draw with it - never over other windows, never buried under it, and modals are untouched.
            ImGui.SetCursorScreenPos(p);
            ImGui.BeginChild("##arp_hud_prev_" + id, sz, false,
                ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoSavedSettings);
            ImGui.SetWindowFontScale(_previewScale);
        }
        else
        {
            ImGui.SetNextWindowPos(p, ImGuiCond.Always);
            ImGui.SetNextWindowSize(sz, ImGuiCond.Always);
            var flags = close < 1f ? PanelFlags | ImGuiWindowFlags.NoInputs : PanelFlags;
            // A tooltip scene's panels never take input and never steal focus from the profile window; they also get their own window ids so they can't collide with (and lock) the profile's panels.
            if (_tooltipScene) flags |= ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus;
            // The panels are separate ImGui windows, which stack in the order they were first created, not the order they are drawn. When the layout's stacking order changes, the windows are recreated under a new generation so they come back in the right order. Reordering windows by rebuilding them is, apparently, the supported approach.
            ImGui.Begin((_tooltipScene ? "##arp_tip_" : "##arp_hud_") + id + "##g" + _stackGen, flags);
        }

        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos();
        var max = min + sz;
        if (EditMode) EditRects.Add(new EditRect { PanelId = id, Min = min, Max = max });
        ImmersiveMode.PanelReveal = reveal;
        ImmersiveMode.PanelFullSize = size;
        var withBackdrop = backdrop && HasBackdrop;
        if (withBackdrop)
        {
            DrawPanelBackdrop(dl, min, size, max, t);
            ImmersiveTheme.FillAlphaScale = BackdropFillScale;
            ImmersiveTheme.OverBackdrop = true;
        }
        // No picture behind the main panel (or the panel asks for a solid plate): the theme's surface colour, opaque, under the chrome.
        var solid = (backdrop && !HasBackdrop) || (def != null && def.Solid && !withBackdrop);
        if (solid) { ImmersiveTheme.ForceOpaque = true; dl.AddRectFilled(min, max, ImmersiveMode.Col(_theme.SurfaceTop, t), _theme.Rounding); }
        // The Burn entrance / exit (Voidtouched's dissolve) is drawn here so any theme can pick it: while a panel is
        // burning in or out, the material chrome is replaced by the dissolving fill and a glowing burn front.
        var burning = !EditMode && ((motion == Motion.Burn && intro < 0.985f) || (exit == Motion.Burn && close < 0.985f));
        try
        {
            if (chrome && burning)
            {
                var progress = MathF.Min(motion == Motion.Burn ? intro : 1f, exit == Motion.Burn ? close : 1f);
                var thr = Rolspeace.VFX.BurnFx.ThresholdFor(progress);
                var bkey = "hud_" + id;
                Rolspeace.VFX.BurnFx.DrawDissolveFill(dl, min, max, bkey, thr, ImmersiveMode.Col(_theme.SurfaceTop, _theme.FillAlpha * t));
                Rolspeace.VFX.BurnFx.DrawBurnFront(dl, min, max, bkey, thr, ImmersiveMode.Accent, _theme.Vfx.Intensity);
                if (header && !string.IsNullOrEmpty(label))
                {
                    var ls = ImGui.CalcTextSize(label);
                    dl.AddText(new Vector2(min.X + _theme.ContentInset, min.Y + MathF.Max(0f, (ImmersiveMode.HeaderHeight - ls.Y) * 0.5f)), ImmersiveMode.Col(ImmersiveMode.Accent, t * t), label);
                }
            }
            else if (chrome) ImmersiveMode.DrawPanelChrome(dl, min, max, label, t, header, "hud_" + id);
        }
        finally { ImmersiveTheme.FillAlphaScale = 1f; ImmersiveTheme.ForceOpaque = false; ImmersiveTheme.OverBackdrop = false; }
        // A chrome-less main panel still needs the picture dimmed.
        if (withBackdrop && !chrome) dl.AddRectFilled(min, max, ImmersiveMode.Col(_theme.SurfaceTop, _theme.FillAlpha * BackdropFillScale * t), _theme.Rounding);
        ImmersiveMode.PanelReveal = 1f;
        // Theme images: background over the fill (masked if asked), frame later at EndPanel so it sits above the content.
        _framePanel = null;
        if (def != null && _theme.Document != null)
        {
            if (def.Background != null && def.Background.IsSet)
            {
                var bgAsset = ThemeAssets.Find(_theme.Document, def.Background.Asset);
                var maskAsset = def.MaskImage != null && def.MaskImage.IsSet ? ThemeAssets.Find(_theme.Document, def.MaskImage.Asset) : null;
                var tex = maskAsset != null && bgAsset != null
                    ? ThemeAssets.Masked("asset:" + bgAsset.Id, SafeBase64(bgAsset.Png), maskAsset)
                    : null;
                dl.PushClipRect(min, max, true);
                // The profile's own background (possibly a video) is masked by an overlay in the panel colour, not by per-frame compositing.
                ThemeAssets.Draw(dl, _theme.Document, def.Background, min, max, t, maskAsset != null ? tex : null,
                    def.Background.Asset == ThemeAssets.ProfileBackgroundId ? maskAsset : null, _theme.SurfaceTop);
                dl.PopClipRect();
            }
            if (def.Frame != null && def.Frame.IsSet) { _framePanel = def; _frameMin = min; _frameMax = max; _frameAlpha = t; }
        }

        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, t);
        if (header)
            ImGui.SetCursorPos(new Vector2(_theme.ContentInset, ImmersiveMode.HeaderHeight + RsTheme.S(10f) + _theme.HeaderOffsetY));
        else
            ImGui.SetCursorPos(new Vector2(_theme.ContentInset, _theme.HeaderOffsetY + RsTheme.S(12f)));
        return t;
    }

    private static byte[]? SafeBase64(string b64)
    {
        try { return Convert.FromBase64String(b64); } catch { return null; }
    }

    private static void EndPanel()
    {
        if (_framePanel != null && _theme.Document != null)
        {
            var dl = ImGui.GetWindowDrawList();
            var pad = RsTheme.S(40f);
            dl.PushClipRect(_frameMin - new Vector2(pad), _frameMax + new Vector2(pad), false);
            ThemeAssets.Draw(dl, _theme.Document, _framePanel.Frame, _frameMin, _frameMax, _frameAlpha);
            dl.PopClipRect();
            _framePanel = null;
        }
        ImGui.PopStyleVar();
        if (!EditMode) HandlePanelDrag();
        if (EditMode) ImGui.EndChild(); else ImGui.End();
    }

    private static float Right => _vpPos.X + _vpSize.X - MathF.Max(RsTheme.S(24f), _vpSize.X * 0.025f);

    // veil The profile's background no longer covers the screen: it is kept for the main panel (see DrawPanelBackdrop), and the game stays visible around the layout with no dimming.
    private static ImTextureID _bgTex; private static int _bgW, _bgH;
    private static void DrawVeil(ImTextureID tex, int texW, int texH)
    {
        _bgTex = tex; _bgW = texW; _bgH = texH;
    }

    private static bool HasBackdrop => _bgW > 0 && _bgH > 0 && _bgTex.Handle != 0;
    // How much of the theme's surface colour stays over the picture.
    private const float BackdropFillScale = 0.5f;

    // The profile background, aspect-filled into the main panel. Drawn before the panel chrome, so the (thinned) surface fill dims it and every effect, border and element sits on top.
    private static void DrawPanelBackdrop(ImDrawListPtr dl, Vector2 min, Vector2 fullSize, Vector2 clipMax, float alpha)
    {
        var max = min + fullSize;
        TargetProfileWindow.AspectFillUv(_bgW, _bgH, fullSize.X, fullSize.Y, out var uMin, out var uMax);
        dl.PushClipRect(min, clipMax, true);
        var col = ImmersiveMode.Col(_theme.VeilImageTint, alpha);
        if (_theme.Rounding > 0.5f) dl.AddImageRounded(_bgTex, min, max, uMin, uMax, col, _theme.Rounding);
        else dl.AddImage(_bgTex, min, max, uMin, uMax, col);
        dl.PopClipRect();
    }

    // Layout: DOCUMENT (user themes / forked built-ins)
    private static Vector2 AnchorPos(PanelDef p, Vector2 size) => AnchorPos(p, size, _vpPos, _vpSize);

    // Position of a child glued to the outside of one of its parent's edges (see PanelAttach). Inside falls back to AnchorPos.
    public static Vector2 AttachedPos(PanelDef p, Vector2 size, Vector2 parentMin, Vector2 parentSize)
    {
        var gap = RsTheme.S(p.Gap);
        return p.Attach switch
        {
            PanelAttach.Above   => new Vector2(parentMin.X + p.X * parentSize.X, parentMin.Y - gap - size.Y),
            PanelAttach.Below   => new Vector2(parentMin.X + p.X * parentSize.X, parentMin.Y + parentSize.Y + gap),
            PanelAttach.LeftOf  => new Vector2(parentMin.X - gap - size.X, parentMin.Y + p.Y * parentSize.Y),
            PanelAttach.RightOf => new Vector2(parentMin.X + parentSize.X + gap, parentMin.Y + p.Y * parentSize.Y),
            _                   => AnchorPos(p, size, parentMin, parentSize),
        };
    }

    // Anchored position of a panel inside a reference rect (the viewport for root panels, the parent's rect for children).
    public static Vector2 AnchorPos(PanelDef p, Vector2 size, Vector2 refMin, Vector2 refSize)
    {
        var vw = refSize.X; var vh = refSize.Y;
        return p.Anchor switch
        {
            PanelAnchor.TopRight    => new Vector2(refMin.X + vw - p.X * vw - size.X, refMin.Y + p.Y * vh),
            PanelAnchor.BottomLeft  => new Vector2(refMin.X + p.X * vw, refMin.Y + vh - p.Y * vh - size.Y),
            PanelAnchor.BottomRight => new Vector2(refMin.X + vw - p.X * vw - size.X, refMin.Y + vh - p.Y * vh - size.Y),
            PanelAnchor.Center      => new Vector2(refMin.X + p.X * vw - size.X * 0.5f, refMin.Y + p.Y * vh - size.Y * 0.5f),
            _                       => new Vector2(refMin.X + p.X * vw, refMin.Y + p.Y * vh),
        };
    }

    // Draw order for a document: roots back-to-front by Layer (ties: larger first), each immediately followed by its children in the same order, so a child is always in front of its parent. Panels whose parent is missing (or would form a cycle) are treated as roots. Stacking state is kept per scene (profile HUD vs. tooltip scene): the two draw in the same frame with different panel orders, and sharing one signature made the generation bump every frame - every window was recreated each frame, so nothing could keep focus or a drag.
    private sealed class StackState { public int Gen; public int PrecreatedGen = -1; public string Sig = ""; public bool Dirty; }
    private static readonly StackState _hudStack = new(), _tipStack = new();
    private static StackState Stack => _tooltipScene ? _tipStack : _hudStack;
    private static int _stackGen { get => Stack.Gen; set => Stack.Gen = value; }
    private static int _precreatedGen { get => Stack.PrecreatedGen; set => Stack.PrecreatedGen = value; }
    private static bool _stackDirty { get => Stack.Dirty; set => Stack.Dirty = value; }   // a panel was skipped this frame and will be created late
    // Recreates the HUD windows when the intended stacking order differs from the one they were created in (layers changed in the editor, a child that appeared a frame late, a different document).
    private static void SyncStackingOrder(List<PanelDef> order)
    {
        var s = Stack;
        var sig = string.Join(",", order.Select(p => p.Id));
        if (sig == s.Sig && !s.Dirty) return;
        s.Sig = sig;
        s.Dirty = false;
        s.Gen++;
    }

    public static List<PanelDef> DrawOrder(ThemeDocument doc)
    {
        var byId = new Dictionary<string, PanelDef>();
        foreach (var p in doc.Panels) byId[p.Id] = p;
        var result = new List<PanelDef>(doc.Panels.Count);
        var visited = new HashSet<PanelDef>();
        IEnumerable<PanelDef> Sorted(IEnumerable<PanelDef> src) => src.OrderBy(p => p.Layer).ThenByDescending(p => p.W * p.H);
        void Emit(PanelDef p)
        {
            if (!visited.Add(p)) return;
            var kids = Sorted(doc.Panels.Where(c => c != p && ParentOf(doc, c) == p)).ToList();
            foreach (var c in kids.Where(c => c.BehindParent)) Emit(c);
            result.Add(p);
            foreach (var c in kids.Where(c => !c.BehindParent)) Emit(c);
        }
        foreach (var p in Sorted(doc.Panels.Where(p => ParentOf(doc, p) == null))) Emit(p);
        // Anything left is inside a parent cycle - draw it as a root.
        foreach (var p in Sorted(doc.Panels.Where(p => !visited.Contains(p)))) Emit(p);
        return result;
    }

    // Where each panel ended up last frame, so a child drawn BEHIND its parent (i.e. before it) can still follow it. One frame of lag on a smoothly moving parent is invisible.
    private static readonly Dictionary<string, (Vector2 min, Vector2 size)> _lastDrawn = new();
    // Each element's measured rect (doc/panel/elementId), for pins whose own height or target is only known from the previous frame.
    private static readonly Dictionary<string, (Vector2 min, Vector2 max)> _lastElRects = new();

    // The smallest height an element in a pin chain gets, from its size and scale, for the kinds whose drawing is otherwise clamped to their box.
    private static float PinNaturalHeight(ElementDef e)
    {
        var S = (Func<float, float>)RsTheme.S;
        var scale = e.Scale <= 0f ? 1f : e.Scale;
        return e.Type switch
        {
            ElementType.Avatar      => (e.Size > 0f ? S(e.Size) : S(140f)) * scale + S(24f),
            ElementType.ControlsRow => S(e.Size > 0f ? e.Size : 28f) * scale + S(8f),
            ElementType.SectionNav when e.Nav == NavStyle.Horizontal => S(34f) * scale + S(8f),
            _ => 0f,
        };
    }

    // Resolves the parent panel of `p` for drawing (null = root / broken).
    public static PanelDef? ParentOf(ThemeDocument doc, PanelDef p)
    {
        if (string.IsNullOrEmpty(p.Parent) || p.Parent == p.Id) return null;
        return doc.Panels.FirstOrDefault(q => q.Id == p.Parent && !q.IsControls);
    }

    // Rough height of the elements that follow a fill element, so the body knows how much to leave for them. The tooltip scene's box: a fixed width, and just tall enough for the content of its panels plus a little padding, so a tooltip can never be stretched into a huge window. Measured height of the TooltipInfo lines at `width` (wrapping included).
    private static float TooltipInfoHeight(ElementDef e, float width)
    {
        var S = (Func<float, float>)RsTheme.S;
        var td = EditMode || _measureSample || AbsoluteRP.Windows.Ect.ARPTooltipWindow.tooltipData == null ? SampleTooltip() : AbsoluteRP.Windows.Ect.ARPTooltipWindow.tooltipData;
        var cfg = Plugin.plugin?.Configuration;
        var fs = (e.Size > 0f ? e.Size : 1f) * (e.Scale <= 0f ? 1f : e.Scale);
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        float h = 0f;
        void Line(string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            var plain = System.Text.RegularExpressions.Regex.Replace(value, "<[^>]*>", "");
            var sz = ImGui.CalcTextSize(label + ": " + plain, false, MathF.Max(1f, width / MathF.Max(0.1f, fs)));
            // Wrapping on the value's narrower column can add a line.
            h += sz.Y * fs + spacing;
        }
        if (td != null)
        {
            if (cfg?.tooltip_showName ?? true) Line("NAME", td.Name);
            if (cfg?.tooltip_showRace ?? true) Line("RACE", td.Race);
            if (cfg?.tooltip_showGender ?? true) Line("GENDER", td.Gender);
            if (cfg?.tooltip_showAge ?? true) Line("AGE", td.Age);
            if (cfg?.tooltip_showHeight ?? true) Line("HEIGHT", td.Height);
            if (cfg?.tooltip_showWeight ?? true) Line("WEIGHT", td.Weight);
            if ((cfg?.tooltip_ShowCustomDescriptors ?? true) && td.descriptors != null)
                foreach (var d in td.descriptors) Line((d.name ?? "").ToUpperInvariant(), d.description ?? "");
            // Icon rows: alignment, personality traits, custom traits.
            var iconH = S(28f) * fs;
            var lineH = ImGui.GetTextLineHeight() * fs + spacing;
            if ((cfg?.tooltip_showAlignment ?? true) && td.Alignment > 0) h += lineH + iconH + spacing;
            if (cfg?.tooltip_showPersonalityTraits ?? true)
            {
                int n = (td.Personality_1 > 0 ? 1 : 0) + (td.Personality_2 > 0 ? 1 : 0) + (td.Personality_3 > 0 ? 1 : 0);
                if ((cfg?.tooltip_showCustomTraits ?? true) && td.personalities != null) n += td.personalities.Count;
                if (n > 0) h += lineH + n * (iconH + spacing);
            }
        }
        return h + S(12f);
    }

    private static bool _measureSample;
    public static Vector2 TooltipBoxSize(ThemeDocument doc, ProfileData? pd = null, bool sample = false)
    {
        _measureSample = sample;
        try { return TooltipBoxSizeCore(doc, pd); } finally { _measureSample = false; }
    }
    private static Vector2 TooltipBoxSizeCore(ThemeDocument doc, ProfileData? pd)
    {
        var S = (Func<float, float>)RsTheme.S;
        var w = S(300f);
        if (doc == null || !doc.HasTooltipScene) return new Vector2(w, S(320f));
        var saved = _theme;
        try
        {
            _theme = ImmersiveThemes.FromDocument(doc);
            var inset = _theme.ContentInset;
            float need = 0f;
            foreach (var p in doc.TooltipPanels)
            {
                if (!string.IsNullOrEmpty(p.Parent)) continue;
                var headerH = string.IsNullOrEmpty(p.Header) ? S(12f) : ImmersiveMode.HeaderHeight + S(10f);
                var content = TrailingHeight(p.Elements, 0, pd ?? new ProfileData()) + headerH + inset;
                need = MathF.Max(need, content / Math.Clamp(p.H, 0.2f, 1f));
            }
            var h = Math.Clamp(need + S(16f), S(80f), S(640f));
            return new Vector2(w, h);
        }
        finally { _theme = saved; }
    }

    private static float TrailingHeight(List<ElementDef> els, int from, ProfileData pd)
    {
        var S = (Func<float, float>)RsTheme.S;
        var line = ImGui.GetTextLineHeight();
        float h = 0f;
        for (int i = from; i < els.Count; i++)
        {
            var e = els[i];
            h += e.Type switch
            {
                ElementType.Controls    => e.Inline ? S(30f) + S(8f) : ControlsHeight(pd) + S(6f),
                ElementType.LikeButton  => TargetProfileWindow.IsOwnProfile ? 0f : S(36f) + line,
                ElementType.CloseButton => S(30f) + S(6f),
                ElementType.Divider     => S(12f) + S(4f),
                ElementType.Spacer      => S(e.Size),
                ElementType.Text        => line * MathF.Max(0.5f, e.Size) + S(6f),
                ElementType.LinkReadout => line + S(8f),
                ElementType.Readouts    => 3 * (line + S(4f)),
                ElementType.Title       => line * 2.6f + S(14f),
                ElementType.Avatar      => (e.Size > 0 ? S(e.Size) : S(140f)) + S(24f),
                ElementType.SectionNav  => S(44f),
                ElementType.ControlsRow => S(e.Size > 0f ? e.Size : 28f) + S(8f),
                ElementType.LoadingProgress => line * 2f + S(52f),
                ElementType.TooltipInfo => TooltipInfoHeight(e, MathF.Max(S(60f), S(300f) - _theme.ContentInset * 2f)),
                ElementType.Image       => e.Height > 0f ? S(e.Height) : S(120f),
                _ => 0f,
            };
        }
        return h;
    }

    // The main panel being drawn (gets the static icon row under its title).
    private static PanelDef? _iconRowPanel;
    private static bool _iconRowDrawn;
    private static bool _docHasControlsRow;

    // The container's fixed size (the Standard layout's panel). Base: the Standard panel; a document's main panel may set its own W/H (fractions of the viewport). The viewer's grip scales the viewport, so the container follows without its contents changing size.
    private static Vector2 MainContainerSize(PanelDef? main = null)
    {
        var std = new Vector2(MathF.Min(RsTheme.S(700f) * (EditMode ? _previewScale : _layoutScale.X), _vpSize.X * 0.44f), _vpSize.Y * 0.86f);
        if (main == null || main.W <= 0f || main.H <= 0f) return std;
        return new Vector2(
            Math.Clamp(main.W, 0.25f, 1f) * _vpSize.X,
            Math.Clamp(main.H, 0.25f, 1f) * _vpSize.Y);
    }
    // The dossier page being shown; elements may style themselves per page.
    private static string _currentPage = string.Empty;

    private static int DrawDocument(ThemeDocument doc, ProfileData pd, List<CustomTab> tabs, int sel)
    {
        var S = (Func<float, float>)RsTheme.S;
        var motion = EntranceMotion();
        var inspecting = TargetProfileWindow.ShowEquipmentInspect;
        var tab = tabs.Count > 0 ? tabs[sel] : null;
        var sectionLabel = inspecting ? "Equipment" : (tab?.Name ?? "Section");
        _currentPage = tab?.Name ?? string.Empty;
        int picked = sel;
        bool warningDrawn = false;

        // Back-to-front: layers, then parents before their children. A child is placed inside its parent's rect AS DRAWN this frame (so it follows the parent's float / parallax / entrance) and adds no motion of its own.
        var drawn = new Dictionary<string, (Vector2 min, Vector2 size)>();
        var docKey = (_tooltipScene ? "tip:" : "") + doc.LocalId + "/";
        var order = DrawOrder(doc);
        if (!EditMode)
        {
            SyncStackingOrder(order);
            // ImGui puts a NoBringToFrontOnFocus window at the BACK when it is created, so creation order is the reverse of stacking order. On a regeneration frame the windows are created here first, last layer first, so the draw order below ends up front-to-back the same way the editor shows it.
            if (_precreatedGen != _stackGen)
            {
                _precreatedGen = _stackGen;
                var prefix = _tooltipScene ? "##arp_tip_" : "##arp_hud_";
                for (int i = order.Count - 1; i >= 0; i--)
                {
                    ImGui.SetNextWindowSize(new Vector2(1f, 1f), ImGuiCond.Always);
                    ImGui.SetNextWindowPos(new Vector2(-10000f, -10000f), ImGuiCond.Always);
                    ImGui.Begin(prefix + order[i].Id + "##g" + _stackGen, PanelFlags | ImGuiWindowFlags.NoInputs);
                    ImGui.End();
                }
            }
        }
        // A theme that puts the profile background somewhere itself keeps its own placement.
        var docPlacesBackground = doc.Panels.Any(q => q.Background?.Asset == ThemeAssets.ProfileBackgroundId
            || q.Elements.Any(e => e.Image?.Asset == ThemeAssets.ProfileBackgroundId || e.Style?.Background?.Asset == ThemeAssets.ProfileBackgroundId));
        // Profile scene: the main panel (the one holding the section body) is pinned top-right at its own W/H. Root panels are placed on the (grip-scaled) viewport and move with the whole layout; only panels with an explicit Parent follow (and, when Inside, stay in) it.
        var profileScene = doc.EditScene == 0;
        var main = profileScene ? doc.MainPanel : null;
        // The notes / link / like / equipment row: a ControlsRow element when the document has one, otherwise the HUD's default spot.
        var docHasControlsRow = profileScene && doc.Panels.Any(q => q.Elements.Any(e => e.Type == ElementType.ControlsRow));
        _docHasControlsRow = docHasControlsRow;
        foreach (var p in order)
        {
            if (profileScene && p.IsControls) continue;   // controls are static, drawn by the HUD
            var parent = ParentOf(doc, p);
            var isMain = main != null && p == main;
            var host = isMain ? null : parent;
            Vector2 size, pos; bool ride = false; float delay = p.Delay;
            (Vector2 min, Vector2 size) prd = default;
            var haveParent = host != null && (drawn.TryGetValue(host.Id, out prd) || _lastDrawn.TryGetValue(docKey + host.Id, out prd));
            if (host != null && !haveParent) { if (!EditMode) _stackDirty = true; continue; }   // behind-parent child on the very first frame
            if (isMain)
            {
                size = MainContainerSize(p);
                pos = new Vector2(Right - size.X, _vpPos.Y + _vpSize.Y * 0.07f);
            }
            else if (haveParent)
            {
                size = new Vector2(MathF.Max(S(24f), p.W * prd.size.X), MathF.Max(S(16f), p.H * prd.size.Y));
                pos = AttachedPos(p, size, prd.min, prd.size);
                ride = true;
                delay += host!.Delay;
                // Only panels placed inside their parent are kept inside it.
                if (p.Attach == PanelAttach.Inside)
                {
                    size = Vector2.Min(size, prd.size);
                    pos = Vector2.Clamp(pos, prd.min, prd.min + prd.size - size);
                }
            }
            else
            {
                size = new Vector2(MathF.Max(S(40f), p.W * _vpSize.X), MathF.Max(S(24f), p.H * _vpSize.Y));
                pos = AnchorPos(p, size);
            }
            string? header = string.IsNullOrEmpty(p.Header) ? null
                           : p.Header.Equals("$section", StringComparison.OrdinalIgnoreCase) ? sectionLabel : p.Header;
            var a = BeginPanel("doc_" + p.Id, header, pos, size, p.Depth, delay, motion, header: header != null, chrome: p.Chrome, bob: p.Bob, ride: ride, def: p,
                backdrop: !p.IsControls && !(p.Background?.IsSet ?? false) && !docPlacesBackground && p.Elements.Any(e => e.Type == ElementType.SectionBody));
            drawn[p.Id] = (ImGui.GetWindowPos(), size);
            _lastDrawn[docKey + p.Id] = drawn[p.Id];
            var dl = ImGui.GetWindowDrawList();
            var min = ImGui.GetWindowPos();
            var inset = _theme.ContentInset;
            var innerW = MathF.Max(S(20f), size.X - inset * 2f);
            var contentTop = ImGui.GetCursorScreenPos().Y;
            var innerH = MathF.Max(S(20f), min.Y + size.Y - inset - contentTop);
            var els = p.Elements;
            if (EditMode)
            {
                var pr = EditRects.LastOrDefault(r => r.PanelId == "doc_" + p.Id && r.ElementIndex < 0);
                if (pr != null) { pr.InnerMin = new Vector2(min.X + inset, contentTop); pr.InnerSize = new Vector2(innerW, innerH); }
            }
            if (p.IsControls)
            {
                ImGui.SetCursorScreenPos(new Vector2(min.X + inset, contentTop));
                DrawDossierControls(pd, innerW, "ctl", string.IsNullOrWhiteSpace(p.CloseLabel) ? _theme.CloseLabel : p.CloseLabel);
                EndPanel();
                continue;
            }
            _iconRowPanel = isMain ? p : null;
            _iconRowDrawn = false;

            // Flow elements stack from the cursor; free elements are placed afterwards at absolute positions inside the inner area. Pin ordering: pinned elements are placed after (and relative to) their target's rendered rect; cycles / missing targets fall back.
            var pinOrder = PinLayout.Order(els, out var pinned);
            var pinTargets = PinLayout.Targets(els, pinned);
            var elRects = new Dictionary<string, (Vector2 min, Vector2 max)>();
            var elKeyBase = docKey + p.Id + "/";
            for (int i = 0; i < els.Count; i++)
            {
                var e = els[i];
                if (e.Free || pinned[i]) continue;
                var cur = ImGui.GetCursorScreenPos();
                var remaining = min.Y + size.Y - inset - cur.Y;
                if (remaining <= 0f) break;
                var avail = e.Type == ElementType.SectionBody && (e.Fill || e.Height <= 0f)
                    ? remaining - TrailingHeight(els, i + 1, pd)
                    : remaining;
                DrawElement(p, e, i, pd, tabs, sel, ref picked, ref warningDrawn, tab, inspecting,
                    min, size, innerW, avail, a, free: false);
                var fr = (new Vector2(min.X + inset, cur.Y), new Vector2(min.X + inset + innerW, MathF.Max(cur.Y + S(4f), ImGui.GetCursorScreenPos().Y)));
                elRects[e.Id] = fr; _lastElRects[elKeyBase + e.Id] = fr;
                var after = ImGui.GetCursorPos();
                if (after.X != inset) ImGui.SetCursorPos(new Vector2(inset, after.Y));
            }
            var innerMin = new Vector2(min.X + inset, contentTop);
            var innerMax = innerMin + new Vector2(innerW, innerH);
            foreach (var i in pinOrder)
            {
                var e = els[i];
                if (!e.Free && !pinned[i]) continue;
                var ew = MathF.Max(S(16f), Math.Clamp(e.W, 0.02f, 1f) * innerW);
                var eh = MathF.Max(S(10f), Math.Clamp(e.H, 0.02f, 1f) * innerH);
                // Pieces in a chain take at least their natural height, so a bigger scale / size pushes whatever hangs off them.
                if (pinned[i] || pinTargets.Contains(e.Id)) eh = MathF.Max(eh, PinNaturalHeight(e));
                float ex, ey;
                (Vector2 min, Vector2 max) tr = default;
                if (pinned[i] && (elRects.TryGetValue(e.PinTo, out tr) || _lastElRects.TryGetValue(elKeyBase + e.PinTo, out tr)))
                {
                    // Above needs its own height: last frame's measurement.
                    var ownH = e.Pin == PinSide.Above && _lastElRects.TryGetValue(elKeyBase + e.Id, out var own) ? own.max.Y - own.min.Y : eh;
                    var pinPos = PinLayout.Place(e, tr.min, tr.max, new Vector2(ew, ownH), S(MathF.Max(0f, e.PinGap)));
                    ex = Math.Clamp(pinPos.X, innerMin.X, MathF.Max(innerMin.X, innerMax.X - ew));
                    ey = MathF.Max(innerMin.Y, pinPos.Y);
                    if (PinLayout.Fills(e)) eh = MathF.Max(S(40f), innerMax.Y - ey);
                }
                else
                {
                    ex = innerMin.X + Math.Clamp(e.X, 0f, 1f) * innerW;
                    ey = innerMin.Y + Math.Clamp(e.Y, 0f, 1f) * innerH;
                }
                ImGui.SetCursorScreenPos(new Vector2(ex, ey));
                var rectsBefore = EditRects.Count;
                DrawElement(p, e, i, pd, tabs, sel, ref picked, ref warningDrawn, tab, inspecting,
                    min, size, ew, eh, a, free: true);
                // The measured rect: its box, or further if the content overflowed it.
                var endY = MathF.Max(ey + eh, ImGui.GetCursorScreenPos().Y);
                var rect = (new Vector2(ex, ey), new Vector2(ex + ew, endY));
                elRects[e.Id] = rect; _lastElRects[elKeyBase + e.Id] = rect;
                if (EditMode && pinned[i] && EditRects.Count > rectsBefore) EditRects[^1].Max = rect.Item2;
            }
            if (isMain)
            {
                // Static controls, the same on every theme.
                DrawCornerControls(dl, min, size.X, a);
                // No ControlsRow element anywhere: the row still appears (controls are never lost).
                if (!_iconRowDrawn && !docHasControlsRow)
                    DrawProfileIconRow(dl, min.X, size.X, min.Y + inset * 0.6f, pd, a);
            }
            _iconRowPanel = null;
            EndPanel();
        }
        return picked;
    }

    // Draws one element at the current cursor. `width` is the element's width; `availH` the height it may use (free elements: exactly their box; flow elements: what's left, less what follows).
    private static void DrawElement(PanelDef p, ElementDef e, int index, ProfileData pd, List<CustomTab> tabs, int sel,
        ref int picked, ref bool warningDrawn, CustomTab? tab, bool inspecting,
        Vector2 panelMin, Vector2 panelSize, float width, float availH, float a, bool free)
    {
        var S = (Func<float, float>)RsTheme.S;
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var scale = e.Scale <= 0f ? 1f : e.Scale;
        var style = e.StyleFor(_currentPage);

        // Element decoration behind the content. For flow elements the height is estimated; free elements know their box.
        Vector2 rMin = default, rMax = default;
        var decorated = style != null && (style.Chrome || style.Glow || style.ScanLines || style.Sweep
                        || style.Mask != EdgeMask.None || style.EdgeMotes || style.Embers || style.Shimmer
                        || (style.Fx != null && style.Fx.Count > 0 && e.Type != ElementType.Avatar)
                        || (style.Background != null && style.Background.IsSet)
                        || (style.Frame != null && style.Frame.IsSet && e.Type != ElementType.Avatar));
        var elKey = p.Id + "_" + index;
        if (decorated)
        {
            var estH = free ? availH : MathF.Min(availH, TrailingHeight(new List<ElementDef> { e }, 0, pd) * scale);
            var pad = S(style!.Padding);
            rMin = start - new Vector2(pad);
            rMax = start + new Vector2(width, estH) + new Vector2(pad);
            var clipPad = S(40f);
            dl.PushClipRect(rMin - new Vector2(clipPad), rMax + new Vector2(clipPad), false);
            ElementFx.DrawBack(dl, rMin, rMax, style, _theme, a, elKey);
            dl.PopClipRect();
        }

        ImmersiveMode.PushElementStyle(style, scale);
        try
        {
            var cur = ImGui.GetCursorScreenPos();
            var inset = _theme.ContentInset;
            // Centre reference for centred content: the element's own box when free, the panel when flowing.
            var cx0 = free ? cur.X : panelMin.X;
            var cw  = free ? width : panelSize.X;

            switch (e.Type)
            {
                case ElementType.Avatar:
                    {
                        var r = (e.Size > 0f ? S(e.Size) : MathF.Min(width, S(140f))) * 0.5f * scale;
                        var maxR = MathF.Max(S(10f), (availH - S(16f)) * 0.5f);
                        r = MathF.Min(r, MathF.Min(maxR, width * 0.5f));
                        var center = new Vector2(cx0 + cw * 0.5f, cur.Y + r + S(8f));
                        // A mask image cuts the portrait: the element's own, else the theme's default.
                        var avMask = style?.MaskImage != null && style.MaskImage.IsSet ? style.MaskImage : _theme.Document?.AvatarMask;
                        IDalamudTextureWrap? masked = null;
                        if (avMask != null && avMask.IsSet && pd?.avatarBytes != null && pd.avatarBytes.Length > 0)
                            masked = ThemeAssets.Masked("avatar:" + (pd.playerName ?? _name) + "@" + (pd.playerWorld ?? _world), pd.avatarBytes, ThemeAssets.Find(_theme.Document, avMask.Asset));
                        DrawAvatar(dl, center, r, pd, a, e.ThemeRing, masked);
                        var avFrame = style?.Frame != null && style.Frame.IsSet ? style.Frame : _theme.Document?.AvatarFrame;
                        if (avFrame != null && avFrame.IsSet)
                        {
                            var fpad = S(60f);
                            dl.PushClipRect(center - new Vector2(r + fpad), center + new Vector2(r + fpad), false);
                            ThemeAssets.Draw(dl, _theme.Document, avFrame, center - new Vector2(r), center + new Vector2(r), a);
                            dl.PopClipRect();
                        }
                        // Custom effects hug the portrait's circle, not the row.
                        if (style != null && style.Fx != null && style.Fx.Count > 0)
                        {
                            var clipPad = S(60f);
                            dl.PushClipRect(center - new Vector2(r + clipPad), center + new Vector2(r + clipPad), false);
                            ElementFx.DrawCustom(dl, center - new Vector2(r), center + new Vector2(r), style, _theme, a, elKey, circle: true);
                            dl.PopClipRect();
                        }
                        ImGui.Dummy(new Vector2(width, r * 2f + S(24f)));
                    }
                    break;
                case ElementType.Title:
                    DrawTitleBlock(dl, cx0, cw, width, pd, a, (e.Size > 0f ? e.Size : 1.45f) * scale);
                    ImGui.SetWindowFontScale(scale);
                    if (p == _iconRowPanel && !_iconRowDrawn && !_docHasControlsRow)
                    {
                        // The static notes / link / like / equipment row sits under the name.
                        DrawProfileIconRow(dl, panelMin.X, panelSize.X, ImGui.GetCursorScreenPos().Y - S(4f), pd, a);
                        _iconRowDrawn = true;
                    }
                    break;
                case ElementType.Readouts:
                    {
                        // Rows stay inside the element's box: its width, and its height when placed freely.
                        var box = new Vector2(width, availH);
                        dl.PushClipRect(cur, cur + box, true);
                        DrawReadouts(pd, tabs.Count, width);
                        dl.PopClipRect();
                        if (free) ImGui.SetCursorScreenPos(new Vector2(cur.X, cur.Y + availH));
                    }
                    break;
                case ElementType.SectionNav:
                    {
                        var x0 = cur.X; var xEnd = cur.X + width;
                        if (e.Nav == NavStyle.Vertical || e.Nav == NavStyle.Index)
                        {
                            var h = free ? availH : (e.Height > 0f ? S(e.Height) : MathF.Min(availH, tabs.Count * (S(42f) + S(8f))));
                            h = MathF.Max(S(30f), h);
                            var pk = DrawVerticalTabs(dl, cur, width, h, tabs, sel, a, roman: !_theme.TechAccents && _theme.MaterialKind != ThemeMaterial.Standard);
                            if (pk != sel) picked = pk;
                            ImGui.Dummy(new Vector2(width, h + S(6f)));
                        }
                        else
                        {
                            var rowH = S(34f) * scale;
                            var rows = MeasureTabRows(tabs, xEnd - x0, xEnd - x0);
                            var pk = DrawHorizontalTabsWrapped(dl, x0, xEnd, x0, xEnd, cur.Y, rowH, S(4f), tabs, sel, a);
                            if (pk != sel) picked = pk;
                            ImGui.SetCursorScreenPos(new Vector2(cur.X, cur.Y + rows * rowH + (rows - 1) * S(4f)));
                            ImGui.Dummy(new Vector2(width, S(8f)));
                        }
                    }
                    break;
                case ElementType.SectionBody:
                    {
                        if (!warningDrawn && !EditMode) { TargetProfileWindow.DrawWarningPopup(); warningDrawn = true; }
                        var h = free ? availH : (e.Height > 0f && !e.Fill ? S(e.Height) : availH);
                        h = MathF.Max(S(40f), h);
                        DrawBody(tab, inspecting, new Vector2(width, h), a);
                        ImGui.SetCursorScreenPos(new Vector2(cur.X, cur.Y + h));
                    }
                    break;
                case ElementType.ControlsRow:
                    {
                        // Notes / link / like / equipment, centred in the element's box.
                        var icon = (e.Size > 0f ? e.Size : 28f) * scale;
                        var y = DrawProfileIconRow(dl, cx0, cw, cur.Y, pd, a, icon);
                        _iconRowDrawn = true;
                        ImGui.SetCursorScreenPos(new Vector2(cur.X, free ? cur.Y + availH : y));
                        ImGui.Dummy(new Vector2(width, S(2f)));
                    }
                    break;
                case ElementType.Controls:
                case ElementType.LikeButton:
                case ElementType.CloseButton:
                    // Legacy: these live in the section body now.
                    break;
                case ElementType.LinkReadout when _theme.Document?.EditScene != 0:
                    {
                        var text = string.IsNullOrEmpty(e.Text) ? _theme.LinkLabel : e.Text.ToUpperInvariant();
                        var line = ImGui.GetTextLineHeight();
                        var tw = ImGui.CalcTextSize(text).X + S(18f);
                        var x = e.Align == TextAlign.Center ? cx0 + (cw - tw) * 0.5f : e.Align == TextAlign.Right ? cur.X + width - tw : cur.X;
                        var c = new Vector2(x + S(5f), cur.Y + line * 0.5f);
                        if (_theme.TechAccents)
                        {
                            var blink = 0.55f + 0.45f * MathF.Sin(ImmersiveMode.Time * 3.2f);
                            dl.AddCircleFilled(c, S(4f), ImmersiveMode.Col(ImmersiveMode.Accent, blink * a), 16);
                        }
                        else
                        {
                            var breathe = 0.7f + 0.3f * MathF.Sin(ImmersiveMode.Time * 1.3f);
                            dl.AddCircleFilled(c, S(8f) * breathe, ImmersiveMode.Col(ImmersiveMode.Accent, 0.18f * a), 20);
                            dl.AddCircleFilled(c, S(3.5f), ImmersiveMode.Col(ImmersiveMode.Accent, a), 16);
                        }
                        dl.AddText(new Vector2(x + S(18f), cur.Y), ImmersiveMode.Col(ImmersiveMode.MutedColor, a), text);
                        ImGui.Dummy(new Vector2(width, line + S(8f)));
                    }
                    break;
                case ElementType.Text:
                    {
                        var fs = (e.Size > 0f ? e.Size : 1f) * scale;
                        ImGui.SetWindowFontScale(fs);
                        var text = e.Text ?? "";
                        var sz = ImGui.CalcTextSize(text, false, width);
                        var x = e.Align == TextAlign.Center ? cx0 + (cw - sz.X) * 0.5f : e.Align == TextAlign.Right ? cur.X + width - sz.X : cur.X;
                        ImGui.SetCursorScreenPos(new Vector2(MathF.Max(cur.X, x), cur.Y));
                        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width);
                        ImGui.TextUnformatted(text);
                        ImGui.PopTextWrapPos();
                        ImGui.SetWindowFontScale(scale);
                        ImGui.SetCursorScreenPos(new Vector2(cur.X, ImGui.GetCursorScreenPos().Y));
                        ImGui.Dummy(new Vector2(width, S(6f)));
                    }
                    break;
                case ElementType.Divider:
                    RuleHere(dl, cur.X, cur.X + width, a);
                    break;
                case ElementType.LoadingProgress:
                    {
                        // "Loading..." plus a bar per thing still arriving. The editor shows sample values.
                        bool tabsL = EditMode, galL = EditMode; int td = 2, tt = 5, gd = 3, gt = 8;
                        if (!EditMode)
                        {
                            TargetProfileWindow.IsLoading(out tabsL, out galL);
                            td = Profiles_DR.loadedTargetTabsCount; tt = Profiles_DR.tabsTargetCount;
                            gd = Profiles_DR.loadedTargetGalleryImages; gt = Profiles_DR.TargetGalleryImagesToLoad;
                        }
                        var sub = "Loading" + new string('.', 1 + (int)(ImmersiveMode.Time * 2f) % 3);
                        var ss = ImGui.CalcTextSize("Loading...");
                        dl.AddText(new Vector2(cx0 + (cw - ss.X) * 0.5f, cur.Y), ImmersiveMode.Col(ImmersiveMode.MutedColor, a), sub);
                        var y = cur.Y + ss.Y + S(10f);
                        void Bar(string label, int done, int total)
                        {
                            var frac = total > 0 ? Math.Clamp(done / (float)total, 0f, 1f) : 0f;
                            dl.AddText(new Vector2(cur.X, y), ImmersiveMode.Col(ImmersiveMode.TextColor, a), label);
                            var right = $"{done} / {total}";
                            dl.AddText(new Vector2(cur.X + width - ImGui.CalcTextSize(right).X, y), ImmersiveMode.Col(ImmersiveMode.MutedColor, a), right);
                            y += ImGui.GetTextLineHeight() + S(4f);
                            var h = S(6f);
                            dl.AddRectFilled(new Vector2(cur.X, y), new Vector2(cur.X + width, y + h), ImmersiveMode.Col(ImmersiveMode.Accent, 0.18f * a), h * 0.5f);
                            if (frac > 0f) dl.AddRectFilled(new Vector2(cur.X, y), new Vector2(cur.X + width * frac, y + h), ImmersiveMode.Col(ImmersiveMode.Accent, a), h * 0.5f);
                            y += h + S(8f);
                        }
                        if (tabsL) Bar("Sections", td, tt);
                        if (galL) Bar("Images", gd, gt);
                        if (!tabsL && !galL) { dl.AddText(new Vector2(cur.X, y), ImmersiveMode.Col(ImmersiveMode.MutedColor, a), "Requesting…"); y += ImGui.GetTextLineHeight(); }
                        ImGui.Dummy(new Vector2(width, y - cur.Y + S(4f)));
                    }
                    break;
                case ElementType.TooltipInfo:
                    {
                        // The tooltip's lines, honouring the viewer's tooltip settings.
                        var td2 = EditMode ? SampleTooltip() : AbsoluteRP.Windows.Ect.ARPTooltipWindow.tooltipData;
                        var cfg = Plugin.plugin?.Configuration;
                        var fs = (e.Size > 0f ? e.Size : 1f) * scale;
                        ImGui.SetWindowFontScale(fs);
                        // A group pins every line (and every wrapped continuation) to the element's left edge instead of the window's.
                        var x0t = cur.X;
                        ImGui.BeginGroup();
                        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width);
                        void Line(string label, string value)
                        {
                            if (string.IsNullOrWhiteSpace(value)) return;
                            ImGui.SetCursorScreenPos(new Vector2(x0t, ImGui.GetCursorScreenPos().Y));
                            var lbl = label + ": ";
                            ImGui.PushStyleColor(ImGuiCol.Text, ImmersiveMode.MutedColor); ImGui.TextUnformatted(lbl); ImGui.PopStyleColor();
                            ImGui.SameLine(0f, 0f);
                            Misc.RenderHtmlColoredTextInline(value, MathF.Max(S(40f), width - ImGui.CalcTextSize(lbl).X));
                        }
                        if (td2 != null)
                        {
                            if (cfg?.tooltip_showName ?? true) Line("NAME", td2.Name);
                            if (cfg?.tooltip_showRace ?? true) Line("RACE", td2.Race);
                            if (cfg?.tooltip_showGender ?? true) Line("GENDER", td2.Gender);
                            if (cfg?.tooltip_showAge ?? true) Line("AGE", td2.Age);
                            if (cfg?.tooltip_showHeight ?? true) Line("HEIGHT", td2.Height);
                            if (cfg?.tooltip_showWeight ?? true) Line("WEIGHT", td2.Weight);
                            if ((cfg?.tooltip_ShowCustomDescriptors ?? true) && td2.descriptors != null)
                                foreach (var d in td2.descriptors) Line((d.name ?? "").ToUpperInvariant(), d.description ?? "");

                            // Icon + text rows (alignment, personality traits, custom traits), like the stock tooltip; icons scale with the element's font size.
                            var iconH = S(28f) * fs;
                            void IconLine(IDalamudTextureWrap? img, Vector2 imgSize, string text)
                            {
                                if (string.IsNullOrWhiteSpace(text) && img == null) return;
                                ImGui.SetCursorScreenPos(new Vector2(x0t, ImGui.GetCursorScreenPos().Y));
                                float used = 0f;
                                if (img != null && img.Handle != IntPtr.Zero)
                                {
                                    ImGui.Image(img.Handle, imgSize);
                                    ImGui.SameLine(0f, S(6f));
                                    used = imgSize.X + S(6f);
                                }
                                Misc.RenderHtmlColoredTextInline(text ?? "", MathF.Max(S(40f), width - used));
                            }
                            void Header(string label)
                            {
                                ImGui.SetCursorScreenPos(new Vector2(x0t, ImGui.GetCursorScreenPos().Y));
                                ImGui.PushStyleColor(ImGuiCol.Text, ImmersiveMode.MutedColor); ImGui.TextUnformatted(label + ":"); ImGui.PopStyleColor();
                            }
                            if ((cfg?.tooltip_showAlignment ?? true) && td2.Alignment > 0)
                            {
                                Header("ALIGNMENT");
                                IconLine(td2.alignmentImg, new Vector2(iconH, iconH), UI.AlignmentName(td2.Alignment));
                            }
                            if (cfg?.tooltip_showPersonalityTraits ?? true)
                            {
                                bool any = td2.Personality_1 > 0 || td2.Personality_2 > 0 || td2.Personality_3 > 0;
                                bool anyCustom = (cfg?.tooltip_showCustomTraits ?? true) && td2.personalities != null && td2.personalities.Count > 0;
                                if (any || anyCustom) Header("TRAITS");
                                if (td2.Personality_1 > 0) IconLine(td2.personality_1Img, new Vector2(iconH * 0.76f, iconH), UI.PersonalityNames(td2.Personality_1));
                                if (td2.Personality_2 > 0) IconLine(td2.personality_2Img, new Vector2(iconH * 0.76f, iconH), UI.PersonalityNames(td2.Personality_2));
                                if (td2.Personality_3 > 0) IconLine(td2.personality_3Img, new Vector2(iconH * 0.76f, iconH), UI.PersonalityNames(td2.Personality_3));
                                if (anyCustom)
                                    foreach (var tr in td2.personalities!)
                                        if (tr != null) IconLine(tr.icon?.icon, new Vector2(iconH * 0.76f, iconH), tr.name ?? "");
                            }
                        }
                        ImGui.PopTextWrapPos();
                        ImGui.EndGroup();
                        ImGui.SetWindowFontScale(scale);
                        ImGui.SetCursorScreenPos(new Vector2(cur.X, ImGui.GetCursorScreenPos().Y));
                        ImGui.Dummy(new Vector2(width, S(4f)));
                    }
                    break;
                case ElementType.Spacer:
                    ImGui.Dummy(new Vector2(width, S(MathF.Max(0f, e.Size))));
                    break;
                case ElementType.Image:
                    {
                        var asset = ThemeAssets.Find(_theme.Document, e.Image?.Asset);
                        var h = free ? availH : (e.Height > 0f ? S(e.Height) * scale
                                 : asset != null ? width * asset.Height / MathF.Max(1f, asset.Width)
                                 : e.Image?.Asset == ThemeAssets.ProfileBackgroundId ? width * 9f / 16f : S(120f));
                        h = MathF.Max(S(8f), MathF.Min(h, free ? availH : MathF.Max(S(8f), availH)));
                        var imin = cur; var imax = cur + new Vector2(width, h);
                        IDalamudTextureWrap? maskedImg = null;
                        if (style?.MaskImage != null && style.MaskImage.IsSet && asset != null)
                            maskedImg = ThemeAssets.Masked("asset:" + asset.Id, SafeBase64(asset.Png), ThemeAssets.Find(_theme.Document, style.MaskImage.Asset));
                        dl.PushClipRect(imin, imax, true);
                        var liveMask = e.Image?.Asset == ThemeAssets.ProfileBackgroundId && style?.MaskImage != null && style.MaskImage.IsSet
                            ? ThemeAssets.Find(_theme.Document, style.MaskImage.Asset) : null;
                        var drawn = ThemeAssets.Draw(dl, _theme.Document, e.Image, imin, imax, a, maskedImg, liveMask, _theme.SurfaceTop);
                        dl.PopClipRect();
                        if (!drawn && EditMode)
                        {
                            dl.AddRect(imin, imax, ImmersiveMode.Col(ImmersiveMode.Accent, 0.5f * a));
                            var tip = asset == null && e.Image?.Asset != ThemeAssets.ProfileBackgroundId ? "Image: pick one in the inspector" : "loading…";
                            dl.AddText(imin + new Vector2(S(6f)), ImmersiveMode.Col(ImmersiveMode.MutedColor, a), tip);
                        }
                        ImGui.Dummy(new Vector2(width, h));
                    }
                    break;
            }
        }
        finally
        {
            ImmersiveMode.PopElementStyle();
        }

        var endY = free ? start.Y + availH : MathF.Max(start.Y + S(10f), ImGui.GetCursorScreenPos().Y);
        if (EditMode)
            EditRects.Add(new EditRect { PanelId = "doc_" + p.Id, ElementIndex = index, Min = start, Max = new Vector2(start.X + width, endY) });

        // Masks and particle effects over the rim, drawn last so they sit above the content the way a torn edge or ember would. The rect is the element's real, measured one (the back pass could only estimate it for flow elements).
        if (decorated)
        {
            var pad = S(style!.Padding);
            rMin = start - new Vector2(pad);
            rMax = new Vector2(start.X + width, endY) + new Vector2(pad);
            var fdl = e.Type == ElementType.SectionBody && _bodyFrontDl.HasValue ? _bodyFrontDl.Value : dl;
            var clipPad = S(40f);
            fdl.PushClipRect(rMin - new Vector2(clipPad), rMax + new Vector2(clipPad), false);
            ElementFx.DrawFront(fdl, rMin, rMax, style!, _theme, a, elKey);
            fdl.PopClipRect();
        }
        _bodyFrontDl = null;
    }

    // Layout: CLUSTER (Allagan, Aether) Height the identity stack needs below the avatar (title, subtitle, rule, readouts, like) - used to size panels so nothing overflows.
    private static float IdentityTextHeight(ProfileData pd, bool withLike)
    {
        var S = (Func<float, float>)RsTheme.S;
        var line = ImGui.GetTextLineHeight();
        var h = line * 1.5f + S(2f) + line + S(10f);            // title + subtitle
        h += S(12f);                                             // rule
        h += (pd != null ? 3 : 2) * (line + S(4f));              // readouts
        if (withLike && !TargetProfileWindow.IsOwnProfile) h += S(6f) + S(30f) + line;
        return h;
    }

    private static float ControlsHeight(ProfileData pd)
    {
        var S = (Func<float, float>)RsTheme.S;
        int n = 2 + (pd != null && pd.equipmentPublic ? 1 : 0);
        return n * S(30f) + (n - 1) * S(6f);
    }

    // Standard: one plain panel Close and report sit in the upper right; the portrait and name are centred at the top with the notes / like icons under them; the section nav follows and the content fills the rest.
    private static int _likeCheckAsked;

    // A bare icon: lit in `lit` when `on`, muted otherwise.
    private static bool HudIcon(ImDrawListPtr dl, string id, Dalamud.Interface.FontAwesomeIcon icon, Vector2 pos, float size, bool on, Vector4 lit, float a, string tooltip)
    {
        ImGui.SetCursorScreenPos(pos);
        var clicked = ImGui.InvisibleButton("##hudicon_" + id, new Vector2(size, size));
        var hov = ImGui.IsItemHovered();
        if (hov) dl.AddRectFilled(pos, pos + new Vector2(size), ImmersiveMode.Col(RsTheme.BorderStrong, 0.35f * a), RsTheme.S(6f));
        var col = on ? lit : ImmersiveMode.MutedColor;
        if (hov && !on) col = ImmersiveMode.TextColor;
        var glyph = Dalamud.Interface.FontAwesomeExtensions.ToIconString(icon);
        using (RsIcons.Push())
        {
            var gs = ImGui.CalcTextSize(glyph);
            dl.AddText(pos + (new Vector2(size) - gs) * 0.5f, ImmersiveMode.Col(col, a), glyph);
        }
        if (hov && !string.IsNullOrEmpty(tooltip)) ImmersiveMode.Tooltip(tooltip);
        return clicked;
    }

    // The profile controls every theme gets, drawn by the HUD (never by a theme document): close in the main panel's upper-right corner, the report flag directly below it and slightly to the left.
    private static void DrawCornerControls(ImDrawListPtr dl, Vector2 min, float w, float a)
    {
        var S = (Func<float, float>)RsTheme.S;
        var icon = S(28f);
        var x1 = min.X + w - _theme.ContentInset;
        var cy = min.Y + _theme.ContentInset * 0.6f;
        if (HudIcon(dl, "close", Dalamud.Interface.FontAwesomeIcon.Times, new Vector2(x1 - icon, cy), icon, false, ImmersiveMode.TextColor, a, "Close")) RequestClose();
        if (!TargetProfileWindow.IsOwnProfile && HudIcon(dl, "report", Dalamud.Interface.FontAwesomeIcon.Flag, new Vector2(x1 - icon - S(8f), cy + icon + S(4f)), icon, true, _theme.Danger, a,
                "Report this profile for inappropriate use." + (char)10 + "(Repeat false reports may result in your account being banned.)"))
            TargetProfileWindow.OpenReport();
    }

    // Notes / link / like (/ equipment) as a centred icon row at `iy`. Returns the y just below the row (and any like result message).
    private static float DrawProfileIconRow(ImDrawListPtr dl, float panelX, float w, float iy, ProfileData? pd, float a, float iconPx = 28f)
    {
        var S = (Func<float, float>)RsTheme.S;
        var icon = S(MathF.Max(12f, iconPx));
        var own = TargetProfileWindow.IsOwnProfile;
        if (!EditMode && !own && pd != null && pd.id > 0 && !ProfileLikes_DR.viewerLiked.ContainsKey(pd.id) && _likeCheckAsked != pd.id)
        {
            _likeCheckAsked = pd.id;
            ProfileLikes_DR.viewerCheckProfileId = pd.id;
            ProfileLikes_DS.FetchProfileLikes(pd.id);
        }
        if (!EditMode && !own) TargetProfileWindow.EnsureLikesFetched();
        var hasNotes = !string.IsNullOrWhiteSpace(AbsoluteRP.Windows.Profiles.NotesWindow.profileNotes);
        var liked = pd != null && ProfileLikes_DR.viewerLiked.TryGetValue(pd.id, out var lk) && lk;
        var equip = pd != null && pd.equipmentPublic;
        int n = 1 + (own ? 1 : 2) + (equip ? 1 : 0);
        var gapI = S(10f);
        var rowW = n * icon + (n - 1) * gapI;
        var ix = panelX + (w - rowW) * 0.5f;
        if (HudIcon(dl, "notes", Dalamud.Interface.FontAwesomeIcon.StickyNote, new Vector2(ix, iy), icon, hasNotes, RsTheme.AccentWarning, a,
                hasNotes ? "Notes (you have notes on this profile)" : "Notes")) TargetProfileWindow.OpenNotes();
        ix += icon + gapI;
        if (HudIcon(dl, "link", Dalamud.Interface.FontAwesomeIcon.Link, new Vector2(ix, iy), icon, false, ImmersiveMode.Accent, a,
                own ? "Link this profile with another of yours" : "Link profiles (family, FC, rivals...)")) AbsoluteRP.Windows.Profiles.RelationshipRequestPopup.OpenForCurrentTarget();
        ix += icon + gapI;
        if (!own)
        {
            if (HudIcon(dl, "like", Dalamud.Interface.FontAwesomeIcon.Heart, new Vector2(ix, iy), icon, liked, _theme.Danger, a,
                    (liked ? "Liked" : "Like") + $"  ({ProfileLikes_DR.likesRemaining} left)")) TargetProfileWindow.RequestLikeDialog();
            ix += icon + gapI;
        }
        if (equip && HudIcon(dl, "equip", Dalamud.Interface.FontAwesomeIcon.Tshirt, new Vector2(ix, iy), icon, TargetProfileWindow.ShowEquipmentInspect, ImmersiveMode.Accent, a,
                "Inspect equipment")) TargetProfileWindow.ToggleEquipmentInspect();
        var y = iy + icon + S(6f);
        if (!EditMode && !string.IsNullOrEmpty(ProfileLikes_DR.likeResultMessage))
        {
            var msg = ProfileLikes_DR.likeResultMessage;
            var ms = ImGui.CalcTextSize(msg);
            dl.AddText(new Vector2(panelX + (w - ms.X) * 0.5f, y), ImmersiveMode.Col(ProfileLikes_DR.likeResultSuccess ? ImmersiveMode.Accent : _theme.Danger, a), msg);
            y += ms.Y + S(6f);
            if (ImGui.GetFrameCount() % 300 == 0) ProfileLikes_DR.likeResultMessage = string.Empty;
        }
        ImGui.SetCursorScreenPos(new Vector2(panelX + _theme.ContentInset, y));
        return y;
    }

    private static int DrawStandard(ProfileData pd, List<CustomTab> tabs, int sel)
    {
        var S = (Func<float, float>)RsTheme.S;
        var motion = EntranceMotion();
        var inset  = _theme.ContentInset;
        var stdSize = ScaleDossier(new Vector2(MathF.Min(S(700f) * _layoutScale.X, _vpSize.X * 0.44f), _vpSize.Y * 0.86f));
        var w      = stdSize.X;
        var h      = stdSize.Y;
        var left   = Right - w;
        var own    = TargetProfileWindow.IsOwnProfile;
        // The portrait straddles the panel's top edge, so the panel starts one radius lower to leave room for the upper half.
        var radius = MathF.Min(S(56f), h * 0.08f);
        var top    = _vpPos.Y + _vpSize.Y * 0.06f + radius;

        var a = BeginPanel("standard", null, new Vector2(left, top), new Vector2(w, h), 0.2f, 0.05f, motion, header: false, bob: false, backdrop: true);
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos();
        var innerW = w - inset * 2f;
        var x0 = min.X + inset;
        var x1 = min.X + w - inset;
        TargetProfileWindow.DrawWarningPopup();

        // Upper right: close, with the report flag just below it.
        var icon = S(28f);
        DrawCornerControls(dl, min, w, a);

        // Portrait centred on the top edge (drawn past the window's clip so the upper half shows), name beneath it.
        var avC = new Vector2(min.X + w * 0.5f, min.Y);
        dl.PushClipRect(new Vector2(min.X - radius, min.Y - radius - S(8f)), new Vector2(min.X + w + radius, min.Y + h), false);
        dl.AddCircleFilled(avC, radius + S(3f), ImmersiveMode.Col(_theme.SurfaceTop, a), 64);
        DrawAvatar(dl, avC, radius, pd, a, themeRing: false);
        dl.AddCircle(avC, radius + S(3f), ImmersiveMode.Col(RsTheme.BorderStrong, a), 64, RsTheme.BorderThickness);
        dl.PopClipRect();
        var y = min.Y + radius + S(10f);
        ImGui.SetCursorScreenPos(new Vector2(x0, y));
        DrawTitleBlock(dl, min.X, w, innerW - icon * 4f, pd, a, 1.45f);

        // Notes / link / like (/ equipment) as icons that light up.
        y = DrawProfileIconRow(dl, min.X, w, ImGui.GetCursorScreenPos().Y - S(4f), pd, a);

        // Navigation under the title.
        dl.AddLine(new Vector2(x0, y), new Vector2(x1, y), ImmersiveMode.Col(RsTheme.Border, a), 1f);
        y += S(8f);
        var rowH = S(32f);
        var rowGap = S(4f);
        int rows = Math.Max(1, MeasureTabRows(tabs, innerW, innerW));
        int picked = DrawHorizontalTabsWrapped(dl, x0, x1, x0, x1, y, rowH, rowGap, tabs, sel, a);
        y += rows * rowH + (rows - 1) * rowGap + S(8f);
        dl.AddLine(new Vector2(x0, y), new Vector2(x1, y), ImmersiveMode.Col(RsTheme.Border, a), 1f);
        y += S(10f);

        // Everything else: the selected section.
        ImGui.SetCursorScreenPos(new Vector2(x0, y));
        var inspecting = TargetProfileWindow.ShowEquipmentInspect;
        var tab = tabs.Count > 0 ? tabs[sel] : null;
        DrawBody(tab, inspecting, new Vector2(innerW, MathF.Max(S(60f), min.Y + h - inset - y)), a);
        EndPanel();
        return picked;
    }

    private static int DrawCluster(ProfileData pd, List<CustomTab> tabs, int sel)
    {
        var S = (Func<float, float>)RsTheme.S;
        var motion    = EntranceMotion();
        var gap       = S(16f);
        var inset     = _theme.ContentInset;
        var dossierW  = ScaleDossier(new Vector2(MathF.Min(S(640f) * _layoutScale.X, _vpSize.X * 0.40f), 1f)).X;
        var identityW = MathF.Min(S(280f) * _layoutScale.X, _vpSize.X * 0.19f);
        var totalW    = identityW + gap + dossierW;
        var right     = Right;
        var left      = right - totalW;
        var top       = _vpPos.Y + _vpSize.Y * 0.06f;
        var bottom    = _vpPos.Y + _vpSize.Y * 0.95f;

        // Nav rows: wrap tabs instead of dropping them.
        var rowH = S(34f);
        var navPad = S(8f);
        int navRows = MeasureTabRows(tabs, totalW - S(40f) - ImGui.CalcTextSize(_theme.LinkLabel).X - ImGui.CalcTextSize(_theme.CloseLabel).X - S(90f), totalW - S(40f));
        var navH = navPad * 2f + navRows * rowH + (navRows - 1) * S(4f);

        var panelsTop = top + navH + S(14f);
        var colH      = bottom - panelsTop;
        var headerH   = ImmersiveMode.HeaderHeight + S(10f) + _theme.HeaderOffsetY;
        var innerW    = identityW - inset * 2f;

        // Identity panel sized from its content (plus a safety margin so the last button never sits on the clip edge); the avatar gives ground first if the column is short.
        var margin    = S(14f);
        var dockH     = headerH + ControlsHeight(pd) + inset + margin;
        var textH     = IdentityTextHeight(pd, true) + margin;
        var availForId = colH - dockH - gap;
        var radius    = MathF.Min(innerW, S(140f)) * 0.5f;
        var idNeeded  = headerH + radius * 2f + S(52f) + textH + inset;
        if (idNeeded > availForId)
        {
            radius = MathF.Max(S(28f), (availForId - headerH - S(52f) - textH - inset) * 0.5f);
            idNeeded = headerH + radius * 2f + S(52f) + textH + inset;
        }
        var identityH = MathF.Min(idNeeded, availForId);
        var dockTop   = panelsTop + identityH + gap;

        // Dossier.
        {
            var dossierH = MathF.Min(colH * _dossierScale.Y, _vpPos.Y + _vpSize.Y * 0.99f - panelsTop);
            var inspecting = TargetProfileWindow.ShowEquipmentInspect;
            var tab = tabs.Count > 0 ? tabs[sel] : null;
            var label = inspecting ? "Equipment" : (tab?.Name ?? "Dossier");
            var headerText = _theme.TechAccents
                ? $"{label}   //   {(inspecting ? "EQ" : (sel + 1).ToString("00"))}"
                : label;
            var a = BeginPanel("dossier", headerText, new Vector2(left + identityW + gap, panelsTop), new Vector2(dossierW, dossierH), 0.55f, 0.08f, motion, backdrop: true);
            TargetProfileWindow.DrawWarningPopup();
            var bodyPos = ImGui.GetCursorPos();
            var bodySize = new Vector2(dossierW - inset * 2f, dossierH - bodyPos.Y - inset);
            DrawBody(tab, inspecting, bodySize, a);
            EndPanel();
        }

        // Identity.
        {
            var a = BeginPanel("identity", "Identity", new Vector2(left, panelsTop), new Vector2(identityW, identityH), 0.75f, 0.12f, motion);
            var dl = ImGui.GetWindowDrawList();
            var min = ImGui.GetWindowPos();
            var center = new Vector2(min.X + identityW * 0.5f, ImGui.GetCursorScreenPos().Y + radius + S(12f));
            DrawAvatar(dl, center, radius, pd, a);
            ImGui.Dummy(new Vector2(innerW, radius * 2f + S(40f)));
            DrawTitleBlock(dl, min.X, identityW, innerW, pd, a, 1.45f);
            RuleHere(dl, min.X + inset, min.X + identityW - inset, a);
            DrawReadouts(pd, tabs.Count);
            DrawLikeRow(pd, innerW);
            EndPanel();
        }

        // Controls dock.
        {
            BeginPanel("controls", "Controls", new Vector2(left, dockTop), new Vector2(identityW, dockH), 0.9f, 0.20f, motion);
            DrawControlsStacked(pd, innerW);
            EndPanel();
        }

        // Nav strip.
        int picked = sel;
        {
            var size = new Vector2(totalW, navH);
            var a = BeginPanel("nav", null, new Vector2(left, top), size, 0.35f, 0.05f, motion, header: false);
            var dl = ImGui.GetWindowDrawList();
            var min = ImGui.GetWindowPos();
            var max = min + size;
            var pad = MathF.Max(S(14f), inset - S(4f));

            var ly = min.Y + navPad + (rowH - ImGui.GetTextLineHeight()) * 0.5f;
            var dotC = new Vector2(min.X + pad + S(4f), ly + ImGui.GetTextLineHeight() * 0.5f);
            if (_theme.TechAccents)
            {
                var blink = 0.55f + 0.45f * MathF.Sin(ImmersiveMode.Time * 3.2f);
                dl.AddCircleFilled(dotC, S(4f), ImmersiveMode.Col(ImmersiveMode.Accent, blink * a), 16);
                dl.AddCircle(dotC, S(7f), ImmersiveMode.Col(ImmersiveMode.Accent, 0.35f * blink * a), 16, 1f);
            }
            else
            {
                var breathe = 0.7f + 0.3f * MathF.Sin(ImmersiveMode.Time * 1.3f);
                dl.AddCircleFilled(dotC, S(8f) * breathe, ImmersiveMode.Col(ImmersiveMode.Accent, 0.18f * a), 20);
                dl.AddCircleFilled(dotC, S(3.5f), ImmersiveMode.Col(ImmersiveMode.Accent, a), 16);
            }
            var readout = _theme.LinkLabel;
            dl.AddText(new Vector2(dotC.X + S(12f), ly), ImmersiveMode.Col(ImmersiveMode.MutedColor, a), readout);
            var readW = ImGui.CalcTextSize(readout).X + S(20f);

            var closeW = DrawCloseButton(dl, new Vector2(max.X - pad, min.Y + navPad), rowH, a, anchorRight: true);

            // First row sits between the readout and the close button; further rows use the full width.
            var x0 = min.X + pad + readW + S(18f);
            var xEndFirst = max.X - pad - closeW - S(18f);
            dl.AddLine(new Vector2(x0 - S(9f), min.Y + navPad), new Vector2(x0 - S(9f), min.Y + navPad + rowH), ImmersiveMode.Col(ImmersiveMode.Accent, 0.25f * a), 1f);
            picked = DrawHorizontalTabsWrapped(dl, x0, xEndFirst, min.X + pad, max.X - pad, min.Y + navPad, rowH, S(4f), tabs, sel, a);
            EndPanel();
        }
        return picked;
    }

    // How many rows the nav needs: first row has `firstW` available, subsequent rows `fullW`.
    private static int MeasureTabRows(List<CustomTab> tabs, float firstW, float fullW)
    {
        var S = (Func<float, float>)RsTheme.S;
        int rows = 1;
        float x = 0f, avail = MathF.Max(S(60f), firstW);
        for (int i = 0; i < tabs.Count; i++)
        {
            var w = TabWidth(tabs[i], i);
            if (x + w > avail && x > 0f) { rows++; x = 0f; avail = fullW; }
            x += w + S(4f);
        }
        return rows;
    }

    private static float TabWidth(CustomTab tab, int i)
    {
        var S = (Func<float, float>)RsTheme.S;
        var label = (tab.Name ?? string.Empty).ToUpperInvariant();
        var num = _theme.TechAccents ? ImGui.CalcTextSize((i + 1).ToString("00")).X + S(8f) : 0f;
        return num + ImGui.CalcTextSize(label).X + S(28f);
    }

    private static int DrawHorizontalTabsWrapped(ImDrawListPtr dl, float firstX, float firstEnd, float fullX, float fullEnd, float y, float rowH, float rowGap,
                                                 List<CustomTab> tabs, int sel, float a)
    {
        var S = (Func<float, float>)RsTheme.S;
        int picked = sel;
        float x = firstX, xEnd = firstEnd;
        for (int i = 0; i < tabs.Count; i++)
        {
            var w = TabWidth(tabs[i], i);
            if (x + w > xEnd && x > (xEnd == firstEnd ? firstX : fullX))
            {
                y += rowH + rowGap;
                x = fullX; xEnd = fullEnd;
            }
            var p = DrawHorizontalTabs(dl, x, xEnd, y, rowH, tabs, sel, a, onlyIndex: i);
            if (p != sel) picked = p;
            x += w + S(4f);
        }
        return picked;
    }

    // Layout: TABLET (Nymian)
    private static int DrawTablet(ProfileData pd, List<CustomTab> tabs, int sel)
    {
        var S = (Func<float, float>)RsTheme.S;
        var tabSize = ScaleDossier(new Vector2(MathF.Min(S(780f) * _layoutScale.X, _vpSize.X * 0.50f), _vpSize.Y * 0.82f));
        var tabW = tabSize.X;
        var tabH = tabSize.Y;
        var pos = new Vector2(Right - tabW, _vpPos.Y + (_vpSize.Y - tabH) * 0.5f + S(24f));
        var medR = S(58f);
        var inset = _theme.ContentInset;
        int picked = sel;

        var a = BeginPanel("tablet", null, pos, new Vector2(tabW, tabH), 0.12f, 0f, Motion.RiseUp, header: false, bob: false, backdrop: true);
        {
            var dl = ImGui.GetWindowDrawList();
            var min = ImGui.GetWindowPos();
            var max = min + new Vector2(tabW, tabH);
            TargetProfileWindow.DrawWarningPopup();

            // Close rune, top-right.
            DrawRuneClose(dl, new Vector2(max.X - S(30f), min.Y + S(30f)), S(14f), a);

            // Title block under the medallion.
            ImGui.SetCursorScreenPos(new Vector2(min.X + inset, min.Y + medR + S(22f)));
            DrawTitleBlock(dl, min.X, tabW, tabW - inset * 2f, pd, a, 1.6f);
            var ruleY = ImGui.GetCursorScreenPos().Y;
            ImmersiveMode.DrawRule(dl, min.X + tabW * 0.2f, max.X - tabW * 0.2f, ruleY, a);
            // Inscription line.
            {
                var s = $"{tabs.Count} INSCRIPTIONS   ·   {(pd.equipmentPublic ? "ARMOURY OPEN" : "ARMOURY SEALED")}";
                var sz = ImGui.CalcTextSize(s);
                dl.AddText(new Vector2(min.X + (tabW - sz.X) * 0.5f, ruleY + S(8f)), ImmersiveMode.Col(ImmersiveMode.MutedColor, a), s);
            }

            var contentTop = ruleY + S(34f);
            var controlsH = S(40f);
            var bottomY = max.Y - inset;
            var colH = bottomY - controlsH - S(14f) - contentTop;
            var navW = MathF.Min(S(170f), tabW * 0.26f);

            // Stone tabs, left column.
            picked = DrawVerticalTabs(dl, new Vector2(min.X + inset, contentTop), navW, colH, tabs, sel, a, roman: true);

            // Body.
            var bodyX = min.X + inset + navW + S(16f);
            // Carved groove separating nav and body.
            dl.AddLine(new Vector2(bodyX - S(8f), contentTop), new Vector2(bodyX - S(8f), contentTop + colH), ImmersiveMode.Col(new Vector4(0, 0, 0, 1), 0.45f * a), 2f);
            dl.AddLine(new Vector2(bodyX - S(7f), contentTop), new Vector2(bodyX - S(7f), contentTop + colH), ImmersiveMode.Col(Vector4.One, 0.08f * a), 1f);
            ImGui.SetCursorScreenPos(new Vector2(bodyX, contentTop));
            var tab = tabs.Count > 0 ? tabs[sel] : null;
            DrawBody(tab, TargetProfileWindow.ShowEquipmentInspect, new Vector2(max.X - inset - bodyX, colH), a);

            // Controls along the bottom.
            ImGui.SetCursorScreenPos(new Vector2(min.X + inset, bottomY - controlsH));
            DrawControlsInline(pd, tabW - inset * 2f);
        }
        EndPanel();

        // Medallion breaking the top edge - drawn after the slab so it sits on top; no chrome of its own.
        {
            var mSize = new Vector2((medR + S(28f)) * 2f);
            var mPos = new Vector2(pos.X + tabW * 0.5f - mSize.X * 0.5f, pos.Y + S(4f) - mSize.Y * 0.5f);
            var ma = BeginPanel("medallion", null, mPos, mSize, 0.12f, 0.18f, Motion.Drop, header: false, chrome: false, bob: false);
            var dl = ImGui.GetWindowDrawList();
            var c = ImGui.GetWindowPos() + mSize * 0.5f;
            // Stone backing disc so the medallion reads as set into the slab.
            dl.AddCircleFilled(c + new Vector2(0, S(4f)), medR + S(14f), ImmersiveMode.Col(new Vector4(0, 0, 0, 1), 0.45f * ma), 48);
            dl.AddCircleFilled(c, medR + S(12f), ImmersiveMode.Col(_theme.SurfaceTop, 0.98f * ma), 48);
            dl.AddCircle(c, medR + S(12f), ImmersiveMode.Col(_theme.AccentSoft, 0.9f * ma), 48, 1.5f);
            DrawAvatar(dl, c, medR, pd, ma);
            EndPanel();
        }
        return picked;
    }

    // Layout: TOME (Sharlayan)
    private static int DrawTome(ProfileData pd, List<CustomTab> tabs, int sel)
    {
        var S = (Func<float, float>)RsTheme.S;
        var indexW = S(112f);
        var pageW = MathF.Min(MathF.Min(S(400f) * _layoutScale.X, (_vpSize.X * 0.58f - indexW) * 0.5f), (_vpSize.X * 0.94f - indexW) * 0.5f);
        var pageH = ScaleDossier(new Vector2(1f, _vpSize.Y * 0.82f)).Y;
        var spine = S(6f);
        var rightX = Right - indexW - pageW;
        var leftX = rightX - spine - pageW;
        var y = _vpPos.Y + (_vpSize.Y - pageH) * 0.5f;
        var inset = _theme.ContentInset;
        int picked = sel;

        // Spine shadow on the background so both pages sit in it.
        {
            var close = CloseFactor();
            var t = Anim.Eased("hud/spine", 0.6f, Anim.Ease.OutCubic) * close;
            var bg = ImGui.GetBackgroundDrawList();
            var cx = rightX - spine * 0.5f;
            var w = S(46f);
            var dark = ImmersiveMode.Col(new Vector4(0.12f, 0.07f, 0.03f, 1f), 0.55f * t);
            var none = ImmersiveMode.Col(new Vector4(0.12f, 0.07f, 0.03f, 1f), 0f);
            bg.AddRectFilledMultiColor(new Vector2(cx - w, y), new Vector2(cx, y + pageH), none, dark, dark, none);
            bg.AddRectFilledMultiColor(new Vector2(cx, y), new Vector2(cx + w, y + pageH), dark, none, none, dark);
        }

        // Right page - the section text.
        {
            var inspecting = TargetProfileWindow.ShowEquipmentInspect;
            var tab = tabs.Count > 0 ? tabs[sel] : null;
            var label = inspecting ? "Equipment" : (tab?.Name ?? "Record");
            // The page id carries the section, so changing section is a fresh scroll unrolling in place of the old one.
            var pageId = "pageR_" + (inspecting ? "eq" : sel.ToString());
            if (pageId != _lastPageId)
            {
                // Every section change is a fresh unroll, even revisits.
                Anim.Reset("hud/" + pageId);
                Anim.Reset("hud/" + pageId + ".a");
                _lastPageId = pageId;
            }
            var a = BeginPanel(pageId, label, new Vector2(rightX, y), new Vector2(pageW, pageH), 0.3f, 0.10f, Motion.Unroll, bob: false, backdrop: true);
            TargetProfileWindow.DrawWarningPopup();
            var dl = ImGui.GetWindowDrawList();
            var min = ImGui.GetWindowPos();
            var bodyPos = ImGui.GetCursorPos();
            var bodySize = new Vector2(pageW - inset * 2f, pageH - bodyPos.Y - inset);
            DrawBody(tab, inspecting, bodySize, a);
            // Folio number, bottom-right.
            var folio = (sel + 1).ToString();
            var fs = ImGui.CalcTextSize(folio);
            dl.AddText(new Vector2(min.X + pageW - inset - fs.X, min.Y + pageH - inset - fs.Y + S(6f)), ImmersiveMode.Col(ImmersiveMode.MutedColor, a), folio);
            EndPanel();
        }

        // Left page - unrolls from the top.
        {
            var a = BeginPanel("pageL", null, new Vector2(leftX, y), new Vector2(pageW, pageH), 0.3f, 0f, Motion.Unroll, header: false, bob: false);
            var dl = ImGui.GetWindowDrawList();
            var min = ImGui.GetWindowPos();
            var curH = ImGui.GetWindowSize().Y;
            var innerW = pageW - inset * 2f;
            // Clip to the unrolled portion so content doesn't spill past the rolled edge.
            ImGui.PushClipRect(min, min + new Vector2(pageW, curH - S(6f)), true);
            var top = min.Y + inset + S(4f);
            var readout = _theme.LinkLabel;
            dl.AddText(new Vector2(min.X + inset, top), ImmersiveMode.Col(ImmersiveMode.MutedColor, a), readout);
            // Avatar shrinks so the whole page (down to Close Tome) fits.
            var fixedBelow = IdentityTextHeight(pd, true) + S(18f) + S(6f) + S(12f) + ControlsHeight(pd) + S(30f) + S(30f) + inset;
            var radius = MathF.Min(MathF.Min(innerW, S(150f)) * 0.5f, MathF.Max(S(28f), (pageH - inset - S(34f) - fixedBelow - S(18f)) * 0.5f));
            var center = new Vector2(min.X + pageW * 0.5f, top + S(30f) + radius);
            DrawAvatar(dl, center, radius, pd, a);
            ImGui.SetCursorScreenPos(new Vector2(min.X + inset, center.Y + radius + S(18f)));
            DrawTitleBlock(dl, min.X, pageW, innerW, pd, a, 1.5f);
            RuleHere(dl, min.X + inset, min.X + pageW - inset, a);
            DrawReadouts(pd, tabs.Count);
            ImGui.Dummy(new Vector2(innerW, S(6f)));
            RuleHere(dl, min.X + inset, min.X + pageW - inset, a);
            DrawControlsStacked(pd, innerW);
            DrawLikeRow(pd, innerW);
            // Close at the bottom of the page.
            ImGui.SetCursorScreenPos(new Vector2(min.X + inset, min.Y + pageH - inset - S(30f)));
            if (ImmersiveMode.HoloButton("tome_close", _theme.CloseLabel, innerW, danger: true, closeIcon: true))
                RequestClose();
            ImGui.PopClipRect();
            EndPanel();
        }

        // Index tabs hanging off the right page.
        if (tabs.Count > 0)
        {
            var gap = S(6f);
            var tabH = MathF.Max(S(22f), MathF.Min(S(34f), (pageH - S(140f) - gap * (tabs.Count - 1)) / tabs.Count));
            var winSize = new Vector2(indexW + S(8f), tabs.Count * (tabH + gap) + S(8f));
            var winPos = new Vector2(rightX + pageW - S(6f), y + S(64f));
            var a = BeginPanel("index", null, winPos, winSize, 0.3f, 0.18f, Motion.SlideRight, header: false, chrome: false, bob: false);
            var dl = ImGui.GetWindowDrawList();
            var min = ImGui.GetWindowPos();
            var ink = ImmersiveMode.TextColor;
            var paper = new Vector4(0.90f, 0.83f, 0.66f, 1f);
            for (int i = 0; i < tabs.Count; i++)
            {
                var label = tabs[i].Name ?? string.Empty;
                var tpos = new Vector2(min.X, min.Y + S(4f) + i * (tabH + gap));
                var active = i == sel;
                ImGui.SetCursorScreenPos(tpos);
                if (ImGui.InvisibleButton("##idx_" + i, new Vector2(indexW, tabH))) { picked = i; _tabClicked = true; }
                var hov = ImGui.IsItemHovered();
                var slideOut = Anim.Lerp(0f, S(6f), hov ? 1f : 0f);
                var slide = (active ? S(4f) : 0f) + slideOut;
                var r0 = new Vector2(tpos.X, tpos.Y);
                var r1 = new Vector2(tpos.X + indexW - S(10f) + slide, tpos.Y + tabH);
                dl.AddRectFilled(r0 + new Vector2(2f, 3f), r1 + new Vector2(2f, 3f), ImmersiveMode.Col(new Vector4(0, 0, 0, 1), 0.28f * a), S(3f));
                dl.AddRectFilled(r0, r1, ImmersiveMode.Col(active ? ImmersiveMode.Accent : paper, 0.97f * a), S(3f), ImDrawFlags.RoundCornersRight);
                dl.AddRect(r0, r1, ImmersiveMode.Col(ink, 0.55f * a), S(3f), ImDrawFlags.RoundCornersRight, 1f);
                var sz = ImGui.CalcTextSize(label);
                var maxTextW = indexW - S(26f);
                var shown = label;
                while (sz.X > maxTextW && shown.Length > 2) { shown = shown[..^1]; sz = ImGui.CalcTextSize(shown + "…"); }
                if (shown != label) shown += "…";
                dl.AddText(new Vector2(r0.X + S(12f), r0.Y + (tabH - sz.Y) * 0.5f), ImmersiveMode.Col(active ? paper : ink, a), shown);
            }
            EndPanel();
        }
        return picked;
    }

    // Layout: SHARDS (Voidtouched)
    private static int DrawShards(ProfileData pd, List<CustomTab> tabs, int sel)
    {
        var S = (Func<float, float>)RsTheme.S;
        var dW = ScaleDossier(new Vector2(MathF.Min(S(600f) * _layoutScale.X, _vpSize.X * 0.38f), 1f)).X;
        var dH = ScaleDossier(new Vector2(1f, _vpSize.Y * 0.66f)).Y;
        var dPos = new Vector2(Right - dW - S(24f), _vpPos.Y + _vpSize.Y * 0.16f);
        var inset = _theme.ContentInset;
        var idW = MathF.Min(S(250f) * _layoutScale.X, _vpSize.X * 0.17f);
        var idInnerW = idW - inset * 2f;
        var idRadius = MathF.Min(idInnerW, S(120f)) * 0.5f;
        var idH = ImmersiveMode.HeaderHeight + S(10f) + _theme.HeaderOffsetY + idRadius * 2f + S(36f) + IdentityTextHeight(pd, true) + inset + S(14f);
        var idSize = new Vector2(idW, idH);
        var idPos = new Vector2(dPos.X - idSize.X * 0.62f, MathF.Min(dPos.Y + dH - idSize.Y + S(70f), _vpPos.Y + _vpSize.Y - idSize.Y - S(16f)));
        int picked = sel;

        float Delay(string id) => (Hash(id) * 0.35f);

        // Dossier.
        {
            var inspecting = TargetProfileWindow.ShowEquipmentInspect;
            var tab = tabs.Count > 0 ? tabs[sel] : null;
            var label = inspecting ? "Equipment" : (tab?.Name ?? "Fragment");
            var a = BeginPanel("dossier", label, dPos, new Vector2(dW, dH), 0.5f, Delay("dossier"), Motion.Burn, backdrop: true);
            TargetProfileWindow.DrawWarningPopup();
            var bodyPos = ImGui.GetCursorPos();
            DrawBody(tab, inspecting, new Vector2(dW - inset * 2f, dH - bodyPos.Y - inset), a);
            EndPanel();
        }

        // Identity shard overlapping the dossier's corner.
        {
            var a = BeginPanel("identity", "Remnant", idPos, idSize, 0.8f, Delay("identity"), Motion.Burn);
            var dl = ImGui.GetWindowDrawList();
            var min = ImGui.GetWindowPos();
            var innerW = idInnerW;
            var radius = idRadius;
            var center = new Vector2(min.X + idSize.X * 0.5f, ImGui.GetCursorScreenPos().Y + radius + S(8f));
            DrawAvatar(dl, center, radius, pd, a);
            ImGui.Dummy(new Vector2(innerW, radius * 2f + S(28f)));
            DrawTitleBlock(dl, min.X, idSize.X, innerW, pd, a, 1.35f);
            RuleHere(dl, min.X + inset, min.X + idSize.X - inset, a);
            DrawReadouts(pd, tabs.Count);
            DrawLikeRow(pd, innerW);
            EndPanel();
        }

        // Tab shards staggered above the dossier, wrapping upward into a second row when there are many.
        {
            var x = dPos.X + S(6f);
            var shardH = S(22f) + inset * 1.4f;
            var baseY = dPos.Y - shardH - S(18f);
            var xLimit = dPos.X + dW + S(20f);
            for (int i = 0; i < tabs.Count; i++)
            {
                var label = (tabs[i].Name ?? string.Empty).ToUpperInvariant();
                var w = ImGui.CalcTextSize(label).X + inset * 2f + S(12f);
                if (x + w > xLimit && x > dPos.X + S(6f)) { x = dPos.X + S(6f); baseY -= shardH + S(16f); }
                var id = "tab" + i;
                var yOff = (Hash(id) - 0.5f) * S(18f);
                var a = BeginPanel(id, null, new Vector2(x, baseY + yOff), new Vector2(w, shardH), 0.6f + Hash(id + "d") * 0.3f, Delay(id), Motion.Burn, header: false);
                var dl = ImGui.GetWindowDrawList();
                var min = ImGui.GetWindowPos();
                ImGui.SetCursorScreenPos(min);
                if (ImGui.InvisibleButton("##shard_" + i, new Vector2(w, shardH))) { picked = i; _tabClicked = true; }
                var hov = ImGui.IsItemHovered();
                var active = i == sel;
                // Active: a lit underline inside the rim. Hover: text only.
                if (active)
                    dl.AddRectFilled(new Vector2(min.X + S(16f), min.Y + shardH - S(14f)), new Vector2(min.X + w - S(16f), min.Y + shardH - S(12f)), ImmersiveMode.Col(ImmersiveMode.Accent, a));
                var sz = ImGui.CalcTextSize(label);
                dl.AddText(new Vector2(min.X + (w - sz.X) * 0.5f, min.Y + (shardH - sz.Y) * 0.5f - S(2f)),
                    ImmersiveMode.Col(active || hov ? ImmersiveMode.TextColor : ImmersiveMode.MutedColor, a), label);
                EndPanel();
                x += w + S(12f);
            }
        }

        // Control fragments hanging off the left of the dossier.
        {
            var fx = dPos.X - S(178f);
            var fy = dPos.Y + S(24f);
            var fw = S(200f);
            var fh = S(30f) + inset * 2f;
            int n = 0;
            void Fragment(string id, string label, Action onClick, bool danger = false, bool active = false)
            {
                var a = BeginPanel("frag_" + id, null, new Vector2(fx - S(36f) + (Hash(id) - 0.5f) * S(10f), fy + n * (fh + S(8f))), new Vector2(fw, fh), 0.7f, Delay(id), Motion.Burn, header: false);
                ImGui.SetCursorPos(new Vector2(inset, inset));
                if (ImmersiveMode.HoloButton(id, label, fw - inset * 2f, danger, active)) onClick();
                EndPanel();
                n++;
            }
            Fragment("notes", "Notes", TargetProfileWindow.OpenNotes);
            if (pd.equipmentPublic)
                Fragment("equip", "Equipment", TargetProfileWindow.ToggleEquipmentInspect, active: TargetProfileWindow.ShowEquipmentInspect);
            Fragment("report", "Report", TargetProfileWindow.OpenReport, danger: true);
        }

        // Tether readout + sever, above the dossier's far corners. Both shards are padded by the inset so nothing touches the rim.
        {
            var shardH = S(28f) + inset * 2f;
            var shardY = dPos.Y - S(58f) - shardH - S(50f);
            var sevW = ImGui.CalcTextSize(_theme.CloseLabel).X + S(17f) + S(28f) + inset * 2f;
            var a = BeginPanel("sever", null, new Vector2(dPos.X + dW - sevW, shardY), new Vector2(sevW, shardH), 0.6f, Delay("sever"), Motion.Burn, header: false);
            var dl = ImGui.GetWindowDrawList();
            DrawCloseButton(dl, ImGui.GetWindowPos() + new Vector2(inset, inset), S(28f), a, anchorRight: false, width: sevW - inset * 2f);
            EndPanel();

            var tw = ImGui.CalcTextSize(_theme.LinkLabel).X + S(26f) + inset * 2f;
            var b = BeginPanel("tether", null, new Vector2(dPos.X, shardY), new Vector2(tw, shardH), 0.6f, Delay("tether"), Motion.Burn, header: false);
            var dl2 = ImGui.GetWindowDrawList();
            var m = ImGui.GetWindowPos();
            var fl = 0.6f + 0.4f * Hash(MathF.Floor(ImmersiveMode.Time * 12f).ToString());
            var cy = m.Y + shardH * 0.5f;
            dl2.AddCircleFilled(new Vector2(m.X + inset + S(4f), cy), S(3.5f), ImmersiveMode.Col(ImmersiveMode.Accent, fl * b), 12);
            dl2.AddText(new Vector2(m.X + inset + S(16f), cy - ImGui.GetTextLineHeight() * 0.5f), ImmersiveMode.Col(ImmersiveMode.MutedColor, b), _theme.LinkLabel);
            EndPanel();
        }
        return picked;
    }

    // Shared content pieces
    private static void DrawAvatar(ImDrawListPtr dl, Vector2 center, float radius, ProfileData pd, float a, bool themeRing = true, IDalamudTextureWrap? masked = null)
    {
        if (pd?.avatar == null || pd.avatar.Handle == IntPtr.Zero) return;
        // A masked portrait: the image already carries its own cut-out shape, so it's drawn square (no circular rounding) and the theme's ring is left off unless explicitly wanted.
        if (masked != null && masked.Handle != IntPtr.Zero)
        {
            var scale0 = Anim.Scale("hud/avatar", 0.7f, 0.4f, 1f, 0.25f, Anim.Ease.OutBack);
            var r0 = radius * scale0;
            if (r0 <= 1f) return;
            dl.AddImage(masked.Handle, center - new Vector2(r0), center + new Vector2(r0), Vector2.Zero, Vector2.One, ImmersiveMode.Col(Vector4.One, a));
            return;
        }
        var scale = Anim.Scale("hud/avatar", 0.7f, 0.4f, 1f, 0.25f, Anim.Ease.OutBack);
        var r = radius * scale;
        if (r <= 1f) return;
        var S = (Func<float, float>)RsTheme.S;
        var accent = ImmersiveMode.Accent;
        var tc = pd.titleColor;
        if (tc.W <= 0.01f) tc = accent;
        var spin = ImmersiveMode.Time * 0.6f;

        if (!themeRing)
        {
            // Bare portrait: the author supplies the ring treatment as an effect (or none at all).
            dl.AddImageRounded(pd.avatar.Handle, center - new Vector2(r), center + new Vector2(r), Vector2.Zero, Vector2.One, ImmersiveMode.Col(Vector4.One, a), r);
            dl.AddCircle(center, r + 1f, ImmersiveMode.Col(tc, 0.75f * a), 64, 1.5f);
            return;
        }

        switch (_theme.Layout)
        {
            case HudLayout.Tablet:
                // Set into stone: bronze bezel, four rivets.
                dl.AddCircleFilled(center, r + S(6f), ImmersiveMode.Col(new Vector4(0, 0, 0, 1), 0.5f * a), 48);
                dl.AddImageRounded(pd.avatar.Handle, center - new Vector2(r), center + new Vector2(r), Vector2.Zero, Vector2.One, ImmersiveMode.Col(Vector4.One, a), r);
                dl.AddCircle(center, r + S(3f), ImmersiveMode.Col(_theme.AccentSoft, 0.95f * a), 64, S(5f));
                dl.AddCircle(center, r + S(3f), ImmersiveMode.Col(accent, 0.55f * a), 64, 1.5f);
                for (int i = 0; i < 4; i++)
                {
                    var ang = i * MathF.PI * 0.5f + MathF.PI * 0.25f;
                    var p = center + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * (r + S(3f));
                    dl.AddCircleFilled(p, S(3.2f), ImmersiveMode.Col(accent, 0.9f * a), 12);
                }
                break;

            case HudLayout.Tome:
                // Inked portrait: sepia tint, double ink ring.
                dl.AddImageRounded(pd.avatar.Handle, center - new Vector2(r), center + new Vector2(r), Vector2.Zero, Vector2.One,
                    ImmersiveMode.Col(new Vector4(0.96f, 0.90f, 0.78f, 1f), a), r);
                dl.AddCircle(center, r + 1.5f, ImmersiveMode.Col(ImmersiveMode.TextColor, 0.85f * a), 64, 1.5f);
                dl.AddCircle(center, r + S(6f), ImmersiveMode.Col(ImmersiveMode.TextColor, 0.45f * a), 64, 1f);
                dl.AddCircle(center, r + S(10f), ImmersiveMode.Col(_theme.AccentSoft, 0.6f * a), 64, 1f);
                break;

            case HudLayout.Shards:
                // Jagged ring + embers.
                dl.AddCircleFilled(center, r + S(10f), ImmersiveMode.Col(accent, 0.06f * a), 48);
                dl.AddImageRounded(pd.avatar.Handle, center - new Vector2(r), center + new Vector2(r), Vector2.Zero, Vector2.One, ImmersiveMode.Col(Vector4.One, a), r);
                {
                    const int n = 40;
                    var prev = Vector2.Zero;
                    for (int i = 0; i <= n; i++)
                    {
                        var ang = i * MathF.PI * 2f / n;
                        var jit = (Hash("ring" + (i % n)) - 0.5f) * S(5f);
                        var p = center + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * (r + S(4f) + jit);
                        if (i > 0) dl.AddLine(prev, p, ImmersiveMode.Col(accent, 0.9f * a), 1.5f);
                        prev = p;
                    }
                }
                BurnFx.DrawEmbers(dl, center - new Vector2(r), center + new Vector2(r, r + S(6f)), accent, "avatar", 0.7f * a, 10);
                break;

            case var _ when _theme.ElementGlow:
                // Aether: no rings - a blue shimmer. Layered glow whose layers breathe out of phase, plus sparkles that wink around the rim.
                {
                    var t = ImmersiveMode.Time;
                    for (int i = 10; i >= 1; i--)
                    {
                        var wob = 0.75f + 0.25f * MathF.Sin(t * (1.1f + i * 0.17f) + i * 1.9f);
                        dl.AddCircleFilled(center, r + S(3f) * i * wob, ImmersiveMode.Col(accent, 0.045f * wob * a), 56);
                    }
                    dl.AddImageRounded(pd.avatar.Handle, center - new Vector2(r), center + new Vector2(r), Vector2.Zero, Vector2.One, ImmersiveMode.Col(Vector4.One, a), r);
                    // Shimmer: a soft bright arc that wanders around the rim.
                    var ang0 = t * 0.7f;
                    for (int k = 0; k < 3; k++)
                    {
                        var aa = ang0 + k * 2.1f;
                        var sw = 0.5f + 0.5f * MathF.Sin(t * 1.6f + k);
                        dl.PathArcTo(center, r + S(2f), aa, aa + 0.9f, 20);
                        dl.PathStroke(ImmersiveMode.Col(new Vector4(0.9f, 0.97f, 1f, 1f), 0.45f * sw * a), ImDrawFlags.None, S(3f));
                    }
                    var core = new Vector4(0.92f, 0.97f, 1f, 1f);
                    for (int i = 0; i < 16; i++)
                    {
                        var r0 = Hash("sp" + i); var r1 = Hash("sq" + i);
                        var life = (t * (0.35f + r1 * 0.4f) + r0) % 1f;
                        var ang = r0 * MathF.PI * 2f + t * 0.2f;
                        var dist = r + S(6f) + life * S(22f);
                        var p = center + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * dist;
                        var sa = MathF.Sin(life * MathF.PI) * a * (0.5f + r1 * 0.5f);
                        dl.AddCircleFilled(p, S(3.5f), ImmersiveMode.Col(accent, 0.2f * sa), 8);
                        dl.AddCircleFilled(p, S(1.3f), ImmersiveMode.Col(core, sa), 8);
                    }
                }
                break;

            default:
                // Allagan orbit rings.
                for (int i = 4; i >= 1; i--)
                    dl.AddCircleFilled(center, r + S(6f) * i, ImmersiveMode.Col(accent, 0.035f * a), 48);
                dl.AddImageRounded(pd.avatar.Handle, center - new Vector2(r), center + new Vector2(r), Vector2.Zero, Vector2.One, ImmersiveMode.Col(Vector4.One, a), r);
                dl.AddCircle(center, r + 1.5f, ImmersiveMode.Col(tc, 0.9f * a), 64, 2f);
                DrawArcRing(dl, center, r + S(9f), spin, 3, 0.9f, 1.5f, ImmersiveMode.Col(accent, 0.85f * a));
                DrawArcRing(dl, center, r + S(15f), -spin * 0.6f, 5, 0.35f, 1f, ImmersiveMode.Col(accent, 0.4f * a));
                for (int i = 0; i < 24; i++)
                {
                    var ang = i * (MathF.PI * 2f / 24f) + spin * 0.15f;
                    var rr = r + S(20f);
                    var len = (i % 6 == 0) ? S(5f) : S(2f);
                    var d = new Vector2(MathF.Cos(ang), MathF.Sin(ang));
                    dl.AddLine(center + d * rr, center + d * (rr + len), ImmersiveMode.Col(accent, 0.35f * a), 1f);
                }
                break;
        }
    }

    private static void DrawArcRing(ImDrawListPtr dl, Vector2 center, float radius, float rotation, int segments, float coverage, float thickness, uint col)
    {
        var per = MathF.PI * 2f / segments;
        var arc = per * coverage;
        for (int i = 0; i < segments; i++)
        {
            var a0 = rotation + i * per;
            dl.PathArcTo(center, radius, a0, a0 + arc, 24);
            dl.PathStroke(col, ImDrawFlags.None, thickness);
        }
    }

    // Title + "name - world", centered within [x0, x0+w]; advances the cursor.
    private static void DrawTitleBlock(ImDrawListPtr dl, float x0, float w, float innerW, ProfileData pd, float a, float fontScale)
    {
        var S = (Func<float, float>)RsTheme.S;
        var title = string.IsNullOrWhiteSpace(pd?.title) ? _name : pd!.title;
        var tcol = pd?.titleColor ?? ImmersiveMode.TextColor;
        if (tcol.W <= 0.01f || _theme.Layout == HudLayout.Tome) tcol = _theme.Layout == HudLayout.Tome ? ImmersiveMode.Accent : tcol;
        var fs = fontScale;
        ImGui.SetWindowFontScale(fs);
        var tsz = ImGui.CalcTextSize(title);
        if (tsz.X > innerW) { fs = MathF.Max(1f, fs * innerW / tsz.X); ImGui.SetWindowFontScale(fs); tsz = ImGui.CalcTextSize(title); }
        var tp = new Vector2(x0 + (w - tsz.X) * 0.5f, ImGui.GetCursorScreenPos().Y);
        if (_theme.Layout != HudLayout.Tome)
            dl.AddText(tp + new Vector2(0f, 1f), ImmersiveMode.Col(ImmersiveMode.Accent, 0.35f * a), title);
        dl.AddText(tp, ImmersiveMode.Col(tcol, a), title);
        ImGui.Dummy(new Vector2(innerW, tsz.Y + S(2f)));
        ImGui.SetWindowFontScale(1f);

        var sub = string.IsNullOrEmpty(_world) ? _name : $"{_name}  ·  {_world}";
        var ssz = ImGui.CalcTextSize(sub);
        var sp = new Vector2(x0 + (w - ssz.X) * 0.5f, ImGui.GetCursorScreenPos().Y);
        dl.AddText(sp, ImmersiveMode.Col(ImmersiveMode.MutedColor, a), sub);
        ImGui.Dummy(new Vector2(innerW, ssz.Y + S(10f)));
    }

    private static void RuleHere(ImDrawListPtr dl, float x0, float x1, float a)
    {
        var y = ImGui.GetCursorScreenPos().Y;
        ImmersiveMode.DrawRule(dl, x0, x1, y, a);
        ImGui.Dummy(new Vector2(x1 - x0, RsTheme.S(12f)));
    }

    private static void DrawReadouts(ProfileData pd, int sectionCount, float width = 0f)
    {
        ImmersiveMode.ReadoutRow("Access", "Granted", ImmersiveMode.Accent, width);
        ImmersiveMode.ReadoutRow("Sections", sectionCount.ToString(), null, width);
        if (pd != null)
            ImmersiveMode.ReadoutRow("Equipment", pd.equipmentPublic ? "Public" : "Private", pd.equipmentPublic ? ImmersiveMode.Accent : ImmersiveMode.MutedColor, width);
    }

    private static void DrawLikeRow(ProfileData pd, float width)
    {
        if (TargetProfileWindow.IsOwnProfile) return;
        TargetProfileWindow.EnsureLikesFetched();
        ImGui.Dummy(new Vector2(width, RsTheme.S(6f)));
        if (ImmersiveMode.HoloButton("like", $"♥  Like   ({ProfileLikes_DR.likesRemaining})", width, danger: true))
            TargetProfileWindow.RequestLikeDialog();
        if (!string.IsNullOrEmpty(ProfileLikes_DR.likeResultMessage))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ProfileLikes_DR.likeResultSuccess ? ImmersiveMode.Accent : _theme.Danger);
            ImGui.PushTextWrapPos(ImGui.GetCursorScreenPos().X + width);
            ImGui.TextUnformatted(ProfileLikes_DR.likeResultMessage);
            ImGui.PopTextWrapPos();
            ImGui.PopStyleColor();
            if (ImGui.GetFrameCount() % 300 == 0) ProfileLikes_DR.likeResultMessage = string.Empty;
        }
    }

    private static void DrawControlsStacked(ProfileData pd, float width)
    {
        var sp = RsTheme.S(6f);
        if (ImmersiveMode.HoloButton("notes", "Notes", width)) TargetProfileWindow.OpenNotes();
        if (ImGui.IsItemHovered()) AbsoluteRP.Immersive.ImmersiveMode.Tooltip("Add personal notes about this profile.");
        ImGui.Dummy(new Vector2(width, sp));
        if (pd != null && pd.equipmentPublic)
        {
            if (ImmersiveMode.HoloButton("equip", "Inspect Equipment", width, active: TargetProfileWindow.ShowEquipmentInspect))
                TargetProfileWindow.ToggleEquipmentInspect();
            if (ImGui.IsItemHovered()) AbsoluteRP.Immersive.ImmersiveMode.Tooltip("View this player's equipped RP items.");
            ImGui.Dummy(new Vector2(width, sp));
        }
        {
            if (ImmersiveMode.HoloButton("link", "Link Profiles", width)) AbsoluteRP.Windows.Profiles.RelationshipRequestPopup.OpenForCurrentTarget();
            if (ImGui.IsItemHovered()) AbsoluteRP.Immersive.ImmersiveMode.Tooltip(TargetProfileWindow.IsOwnProfile
                ? "Link this profile with another of your own profiles (family, alts...)."
                : "Ask this profile's owner to link with one of your profiles (family, FC, rivals...).");
            ImGui.Dummy(new Vector2(width, sp));
        }
        if (ImmersiveMode.HoloButton("report", "Report", width, danger: true)) TargetProfileWindow.OpenReport();
        if (ImGui.IsItemHovered()) AbsoluteRP.Immersive.ImmersiveMode.Tooltip("Report this profile for inappropriate use.\n(Repeat false reports may result in your account being banned.)");
    }

    // Height of the control strip every section body ends with.
    private static float DossierControlsHeight() => (RsTheme.S(30f) + RsTheme.S(8f)) * 2f;

    // Notes / Equipment / Like / Report on one row, the close button on the next. Always drawn with the body so the viewer can always act.
    private static void DrawDossierControls(ProfileData pd, float width, string key, string closeLabel)
    {
        var S = (Func<float, float>)RsTheme.S;
        var start = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + S(6f)));
        DrawControlsInline(pd, width);
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + S(30f) + S(8f) + S(2f)));
        if (ImmersiveMode.HoloButton("doc_close_" + key, closeLabel, width, danger: true, closeIcon: true))
            RequestClose();
        if (!string.IsNullOrEmpty(ProfileLikes_DR.likeResultMessage))
        {
            ImmersiveMode.Tooltip(ProfileLikes_DR.likeResultMessage);
            if (ImGui.GetFrameCount() % 300 == 0) ProfileLikes_DR.likeResultMessage = string.Empty;
        }
    }

    private static void DrawControlsInline(ProfileData pd, float width)
    {
        var gap = RsTheme.S(10f);
        var items = new List<(string id, string label, Action act, bool danger, bool active)>
        {
            ("notes", "Notes", TargetProfileWindow.OpenNotes, false, false),
        };
        if (pd != null && pd.equipmentPublic)
            items.Add(("equip", "Equipment", TargetProfileWindow.ToggleEquipmentInspect, false, TargetProfileWindow.ShowEquipmentInspect));
        if (!TargetProfileWindow.IsOwnProfile)
        {
            TargetProfileWindow.EnsureLikesFetched();
            items.Add(("like", $"♥ {ProfileLikes_DR.likesRemaining}", TargetProfileWindow.RequestLikeDialog, true, false));
        }
        items.Add(("link", "Link", AbsoluteRP.Windows.Profiles.RelationshipRequestPopup.OpenForCurrentTarget, false, false));
        items.Add(("report", "Report", TargetProfileWindow.OpenReport, true, false));
        var w = (width - gap * (items.Count - 1)) / items.Count;
        var start = ImGui.GetCursorScreenPos();
        for (int i = 0; i < items.Count; i++)
        {
            ImGui.SetCursorScreenPos(new Vector2(start.X + i * (w + gap), start.Y));
            var it = items[i];
            if (ImmersiveMode.HoloButton(it.id, it.label, w, it.danger, it.active)) it.act();
        }
    }

    // Close button drawn on `dl`; returns its width.
    private static float DrawCloseButton(ImDrawListPtr dl, Vector2 anchor, float h, float a, bool anchorRight, float? width = null)
    {
        var S = (Func<float, float>)RsTheme.S;
        var label = _theme.CloseLabel;
        var sz = ImGui.CalcTextSize(label);
        var glyph = S(9f) + S(8f);                       // plus gap
        var w = width ?? (sz.X + glyph + S(28f));
        var pos = anchorRight ? new Vector2(anchor.X - w, anchor.Y) : anchor;
        ImGui.SetCursorScreenPos(pos);
        if (ImGui.InvisibleButton("##hud_close", new Vector2(w, h))) RequestClose();
        var hov = ImGui.IsItemHovered();
        var c = _theme.Danger;
        var round = MathF.Min(_theme.Rounding, S(6f));
        dl.AddRectFilled(pos, pos + new Vector2(w, h), ImmersiveMode.Col(c, 0.08f * a), round);
        dl.AddRect(pos, pos + new Vector2(w, h), ImmersiveMode.Col(c, (hov ? 0.95f : 0.4f) * a), round, ImDrawFlags.None, hov ? 1.5f : 1f);
        var contentW = glyph + sz.X;
        var x0 = pos.X + (w - contentW) * 0.5f;
        var cy = pos.Y + h * 0.5f;
        var col = ImmersiveMode.Col(hov ? ImmersiveMode.TextColor : c, a);
        ImmersiveMode.DrawCloseGlyph(dl, new Vector2(x0 + S(4.5f), cy), S(4.5f), col);
        dl.AddText(new Vector2(x0 + glyph, pos.Y + (h - sz.Y) * 0.5f), col, label);
        return w;
    }

    // Round rune close button (tablet).
    private static void DrawRuneClose(ImDrawListPtr dl, Vector2 c, float r, float a)
    {
        ImGui.SetCursorScreenPos(c - new Vector2(r));
        if (ImGui.InvisibleButton("##rune_close", new Vector2(r * 2f))) RequestClose();
        var hov = ImGui.IsItemHovered();
        var stone = _theme.AccentSoft;
        dl.AddCircleFilled(c + new Vector2(0, 2f), r + 2f, ImmersiveMode.Col(new Vector4(0, 0, 0, 1), 0.4f * a), 24);
        dl.AddCircleFilled(c, r, ImmersiveMode.Col(hov ? _theme.Danger : _theme.SurfaceTop, (hov ? 0.55f : 0.95f) * a), 24);
        dl.AddCircle(c, r, ImmersiveMode.Col(stone, 0.9f * a), 24, 1.5f);
        var k = r * 0.42f;
        var col = ImmersiveMode.Col(hov ? ImmersiveMode.TextColor : _theme.Danger, a);
        dl.AddLine(c + new Vector2(-k, -k), c + new Vector2(k, k), col, 2f);
        dl.AddLine(c + new Vector2(k, -k), c + new Vector2(-k, k), col, 2f);
        if (hov) AbsoluteRP.Immersive.ImmersiveMode.Tooltip(_theme.CloseLabel);
    }

    // Horizontal numbered tabs (cluster nav). Set when a tab button was clicked this frame (lets re-clicking the selected tab close the equipment inspect view).
    private static bool _tabClicked;

    private static int DrawHorizontalTabs(ImDrawListPtr dl, float x, float xEnd, float y, float h, List<CustomTab> tabs, int sel, float a, int onlyIndex = -1)
    {
        var S = (Func<float, float>)RsTheme.S;
        int picked = sel;
        ImGui.PushClipRect(new Vector2(x - S(2f), y - S(8f)), new Vector2(xEnd + S(2f), y + h + S(8f)), true);
        for (int i = 0; i < tabs.Count; i++)
        {
            if (onlyIndex >= 0 && i != onlyIndex) continue;
            var tech = _theme.TechAccents;
            var label = (tabs[i].Name ?? string.Empty).ToUpperInvariant();
            var num = (i + 1).ToString("00");
            var numSz = tech ? ImGui.CalcTextSize(num) : Vector2.Zero;
            var numW = tech ? numSz.X + S(8f) : 0f;
            var lblSz = ImGui.CalcTextSize(label);
            var w = numW + lblSz.X + S(28f);
            if (onlyIndex < 0 && x + w > xEnd) break;
            var tpos = new Vector2(x, y);
            ImGui.SetCursorScreenPos(tpos);
            if (ImGui.InvisibleButton("##hud_tab_" + i, new Vector2(w, h))) { picked = i; _tabClicked = true; }
            var hov = ImGui.IsItemHovered();
            var active = i == sel;
            var tmax = tpos + new Vector2(w, h);
            if (tech)
            {
                if (active) dl.AddRectFilled(tpos, tmax, ImmersiveMode.Col(ImmersiveMode.Accent, 0.16f * a));
                if (active)
                {
                    dl.AddRectFilled(new Vector2(tpos.X, tmax.Y - S(2f)), tmax, ImmersiveMode.Col(ImmersiveMode.Accent, a));
                    dl.AddLine(tpos, tpos + new Vector2(0, S(8f)), ImmersiveMode.Col(ImmersiveMode.Accent, a), 2f);
                    dl.AddLine(new Vector2(tmax.X, tpos.Y), new Vector2(tmax.X, tpos.Y + S(8f)), ImmersiveMode.Col(ImmersiveMode.Accent, a), 2f);
                }
                else if (hov)
                    dl.AddRectFilled(new Vector2(tpos.X, tmax.Y - S(1f)), tmax, ImmersiveMode.Col(ImmersiveMode.Accent, 0.6f * a));
            }
            else
            {
                // Soft pill: active glows, hover just lifts the text.
                var round = h * 0.5f;
                if (active)
                {
                    if (_theme.ElementGlow)
                        for (int g = 3; g >= 1; g--)
                            dl.AddRectFilled(tpos - new Vector2(S(3f) * g), tmax + new Vector2(S(3f) * g), ImmersiveMode.Col(ImmersiveMode.Accent, 0.08f * a), round + S(3f) * g);
                    dl.AddRectFilled(tpos, tmax, ImmersiveMode.Col(ImmersiveMode.Accent, 0.18f * a), round);
                    dl.AddRect(tpos, tmax, ImmersiveMode.Col(ImmersiveMode.Accent, 0.7f * a), round, ImDrawFlags.None, 1f);
                }
                else if (hov)
                    dl.AddRect(tpos, tmax, ImmersiveMode.Col(ImmersiveMode.Accent, 0.35f * a), round, ImDrawFlags.None, 1f);
            }
            var ty = tpos.Y + (h - lblSz.Y) * 0.5f;
            if (tech)
                dl.AddText(new Vector2(tpos.X + S(14f), ty), ImmersiveMode.Col(active ? ImmersiveMode.Accent : ImmersiveMode.MutedColor, (active ? 1f : 0.8f) * a), num);
            dl.AddText(new Vector2(tpos.X + S(14f) + numW, ty), ImmersiveMode.Col(active || hov ? ImmersiveMode.TextColor : ImmersiveMode.MutedColor, a), label);
            x += w + S(4f);
        }
        ImGui.PopClipRect();
        return picked;
    }

    // Stacked plates (tablet side nav).
    private static int DrawVerticalTabs(ImDrawListPtr dl, Vector2 origin, float width, float availH, List<CustomTab> tabs, int sel, float a, bool roman)
    {
        var S = (Func<float, float>)RsTheme.S;
        int picked = sel;
        var gap = S(8f);
        // Compress plates so every section fits the column.
        var h = tabs.Count > 0 ? MathF.Min(S(42f), (availH - gap * (tabs.Count - 1)) / tabs.Count) : S(42f);
        h = MathF.Max(S(22f), h);
        var stone = _theme.AccentSoft;
        ImGui.PushClipRect(origin, origin + new Vector2(width, availH), true);
        for (int i = 0; i < tabs.Count; i++)
        {
            var y = origin.Y + i * (h + gap);
            if (y + h > origin.Y + availH + 1f) break;
            var label = tabs[i].Name ?? string.Empty;
            var pos = new Vector2(origin.X, y);
            ImGui.SetCursorScreenPos(pos);
            if (ImGui.InvisibleButton("##vtab_" + i, new Vector2(width, h))) { picked = i; _tabClicked = true; }
            var hov = ImGui.IsItemHovered();
            var active = i == sel;
            var max = pos + new Vector2(width, h);
            var round = S(6f);
            // Plate: recessed when active, raised otherwise; hover only brightens the bronze edge.
            dl.AddRectFilled(pos + new Vector2(0, 2f), max + new Vector2(0, 2f), ImmersiveMode.Col(new Vector4(0, 0, 0, 1), 0.35f * a), round);
            dl.AddRectFilled(pos, max, ImmersiveMode.Col(active ? _theme.SurfaceBot : _theme.SurfaceTop, 0.95f * a), round);
            if (active)
                dl.AddRectFilled(pos, max, ImmersiveMode.Col(ImmersiveMode.Accent, 0.14f * a), round);
            dl.AddRect(pos, max, ImmersiveMode.Col(stone, (hov ? 1f : 0.6f) * a), round, ImDrawFlags.None, hov ? 1.5f : 1f);
            if (active)
                dl.AddRectFilled(pos, new Vector2(pos.X + S(4f), max.Y), ImmersiveMode.Col(ImmersiveMode.Accent, a), round, ImDrawFlags.RoundCornersLeft);
            var num = roman ? Roman(i + 1) : (i + 1).ToString("00");
            var nsz = ImGui.CalcTextSize(num);
            var lsz = ImGui.CalcTextSize(label);
            var ty = pos.Y + (h - lsz.Y) * 0.5f;
            dl.AddText(new Vector2(pos.X + S(12f), ty), ImmersiveMode.Col(active ? ImmersiveMode.Accent : ImmersiveMode.MutedColor, a), num);
            ImGui.PushClipRect(pos, new Vector2(max.X - S(6f), max.Y), true);
            dl.AddText(new Vector2(pos.X + S(12f) + nsz.X + S(10f), ty), ImmersiveMode.Col(active || hov ? ImmersiveMode.TextColor : ImmersiveMode.MutedColor, a), label);
            ImGui.PopClipRect();
        }
        ImGui.PopClipRect();
        return picked;
    }

    private static string Roman(int n)
    {
        if (n <= 0 || n > 39) return n.ToString();
        var tens = new[] { "", "X", "XX", "XXX" };
        var ones = new[] { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };
        return tens[n / 10] + ones[n % 10];
    }

    // The scrolling tab body with per-theme transition. The section body's own draw list. Anything drawn on the panel's list after the body child renders UNDERNEATH the child, so rim effects on a body are drawn here instead (with an override clip, they can reach outside it).
    private static ImDrawListPtr? _bodyFrontDl;

    private static void DrawBody(CustomTab? tab, bool inspecting, Vector2 size, float a)
    {
        var S = (Func<float, float>)RsTheme.S;
        // The editor preview keeps its own keys so it never resets the live HUD's section animation (which made both flicker).
        var tabKey = $"{(EditMode ? "prev/" : "")}{_name}@{_world}/{(inspecting ? "__equipment" : tab?.Name)}";
        Anim.ResetKey(EditMode ? "hudTabPrev" : "hudTab", tabKey);
        var tt = Anim.Eased("hudTab/" + tabKey, 0.45f, Anim.Ease.OutQuint);

        var start = ImGui.GetCursorPos();
        var alphaMul = tt;
        switch (_theme.Layout)
        {
            case HudLayout.Cluster: start.Y += (1f - tt) * S(18f); break;
            case HudLayout.Tablet:  start.Y += (1f - Anim.Eased("hudTab/" + tabKey + ".b", 0.5f, Anim.Ease.OutBack)) * S(14f); break;
            case HudLayout.Tome:    alphaMul = 1f; break;   // the page itself unrolls
            case HudLayout.Shards:
                if (tt < 1f) alphaMul = 0.55f + 0.45f * Hash(MathF.Floor(ImmersiveMode.Time * 30f).ToString());
                break;
        }
        ImGui.SetCursorPos(start);

        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * alphaMul);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(S(4f), S(4f)));
        var open = ImGui.BeginChild("##hud_body", size, false, ImGuiWindowFlags.None);
        _bodyFrontDl = ImGui.GetWindowDrawList();
        try
        {
            if (open)
            {
                if (inspecting)
                {
                    try { EquipmentPage.RenderEquipmentPreview(Plugin.plugin); }
                    catch (Exception ex) { Plugin.PluginLog.Debug("[ImmersiveHud] equipment: " + ex.Message); }
                }
                else if (tab != null)
                {
                    TargetProfileWindow.currentLayout = tab.Layout as CustomLayout;
                    TargetProfileWindow.RenderTabBody(tab);
                }
                else
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, _theme.Danger);
                    ImGui.TextUnformatted("No data available for this profile.");
                    ImGui.PopStyleColor();
                }
                Misc.RenderUrlModalPopup();
            }
        }
        finally
        {
            ImGui.EndChild();
            ImGui.PopStyleVar(2);
        }
        // The viewer's corner grip: stretches the whole container (X and Y).
        DrawDossierGrip(a);

        // Edge fades + the tome's page-turn shadow sweep.
        var dl = ImGui.GetWindowDrawList();
        var bodyMin = ImGui.GetWindowPos() + start;
        var bodyMax = bodyMin + size;
        var fadeH = S(18f);
        var fadeCol = _theme.Layout == HudLayout.Tome ? new Vector4(0.90f, 0.84f, 0.70f, 1f) : _theme.SurfaceTop;
        var g0 = ImmersiveMode.Col(fadeCol, (_theme.Layout == HudLayout.Tome ? 0.55f : 0.85f) * a);
        var g1 = ImmersiveMode.Col(fadeCol, 0f);
        dl.AddRectFilledMultiColor(bodyMin, new Vector2(bodyMax.X, bodyMin.Y + fadeH), g0, g0, g1, g1);
        dl.AddRectFilledMultiColor(new Vector2(bodyMin.X, bodyMax.Y - fadeH), bodyMax, g1, g1, g0, g0);
    }

    // Loading screens
    private static float LoadFraction(bool tabsLoading, bool galleryLoading, out string status)
    {
        int done = 0, total = 0;
        if (Profiles_DR.tabsTargetCount > 0) { total += Profiles_DR.tabsTargetCount; done += Math.Min(Profiles_DR.loadedTargetTabsCount, Profiles_DR.tabsTargetCount); }
        if (Profiles_DR.TargetGalleryImagesToLoad > 0) { total += Profiles_DR.TargetGalleryImagesToLoad; done += Math.Min(Profiles_DR.loadedTargetGalleryImages, Profiles_DR.TargetGalleryImagesToLoad); }
        status = tabsLoading ? $"Sections {Profiles_DR.loadedTargetTabsCount} / {Profiles_DR.tabsTargetCount}"
               : galleryLoading ? $"Imagery {Profiles_DR.loadedTargetGalleryImages} / {Profiles_DR.TargetGalleryImagesToLoad}"
               : "Requesting";
        return total > 0 ? done / (float)total : -1f;
    }

    private static Vector2 CenterPanelPos(Vector2 size)
        => new(_vpPos.X + _vpSize.X * 0.70f - size.X * 0.5f, _vpPos.Y + (_vpSize.Y - size.Y) * 0.5f);

    private static string Target => string.IsNullOrEmpty(_world) ? _name : $"{_name} @ {_world}";

    // Standard: a plain card - whose profile, and flat progress bars.
    private static void DrawLoadingStandard(bool tabsLoading, bool galleryLoading)
    {
        var S = (Func<float, float>)RsTheme.S;
        var size = new Vector2(MathF.Min(S(380f), _vpSize.X * 0.4f), S(150f));
        var a = BeginPanel("loading_std", null, CenterPanelPos(size), size, 0f, 0f, EntranceMotion(), header: false, bob: false);
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos();
        DrawCardClose(dl, min, size, a);
        var inset = _theme.ContentInset;
        var innerW = size.X - inset * 2f;
        var y = min.Y + inset;

        var name = string.IsNullOrWhiteSpace(_name) ? "Profile" : Target;
        ImGui.SetWindowFontScale(1.2f);
        var ns = ImGui.CalcTextSize(name);
        dl.AddText(new Vector2(min.X + (size.X - ns.X) * 0.5f, y), ImmersiveMode.Col(RsTheme.TextPrimary, a), name);
        ImGui.SetWindowFontScale(1f);
        y += ns.Y + S(4f);
        var sub = "Loading" + new string('.', 1 + (int)(ImmersiveMode.Time * 2f) % 3);
        var ss = ImGui.CalcTextSize("Loading...");
        dl.AddText(new Vector2(min.X + (size.X - ss.X) * 0.5f, y), ImmersiveMode.Col(RsTheme.TextMuted, a), sub);
        y += ss.Y + S(14f);

        void Bar(string label, int done, int total)
        {
            var frac = total > 0 ? Math.Clamp(done / (float)total, 0f, 1f) : 0f;
            var right = $"{done} / {total}";
            dl.AddText(new Vector2(min.X + inset, y), ImmersiveMode.Col(RsTheme.TextSecondary, a), label);
            dl.AddText(new Vector2(min.X + inset + innerW - ImGui.CalcTextSize(right).X, y), ImmersiveMode.Col(RsTheme.TextMuted, a), right);
            y += ImGui.GetTextLineHeight() + S(4f);
            var h = S(6f);
            dl.AddRectFilled(new Vector2(min.X + inset, y), new Vector2(min.X + inset + innerW, y + h), ImmersiveMode.Col(RsTheme.BgTertiary, a), h * 0.5f);
            if (frac > 0f)
                dl.AddRectFilled(new Vector2(min.X + inset, y), new Vector2(min.X + inset + innerW * frac, y + h), ImmersiveMode.Col(RsTheme.AccentPrimary, a), h * 0.5f);
            y += h + S(10f);
        }
        if (tabsLoading) Bar("Sections", Profiles_DR.loadedTargetTabsCount, Profiles_DR.tabsTargetCount);
        if (galleryLoading) Bar("Images", Profiles_DR.loadedTargetGalleryImages, Profiles_DR.TargetGalleryImagesToLoad);
        EndPanel();
    }

    // Allagan: link handshake - spinner arcs + segment bars.
    private static void DrawLoadingCluster(bool tabsLoading, bool galleryLoading)
    {
        var S = (Func<float, float>)RsTheme.S;
        var size = new Vector2(MathF.Min(S(440f), _vpSize.X * 0.4f), S(210f));
        var a = BeginPanel("link", null, CenterPanelPos(size), size, 0.5f, 0f, EntranceMotion(), header: false);
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos();
        DrawCardClose(dl, min, size, a);
        var innerW = size.X - _theme.ContentInset * 2f;
        var c = new Vector2(min.X + size.X * 0.5f, ImGui.GetCursorScreenPos().Y + S(28f));
        DrawArcRing(dl, c, S(20f), ImmersiveMode.Time * 2.4f, 3, 0.6f, 2f, ImmersiveMode.Col(ImmersiveMode.Accent, a));
        DrawArcRing(dl, c, S(13f), -ImmersiveMode.Time * 1.6f, 2, 0.5f, 1.5f, ImmersiveMode.Col(ImmersiveMode.Accent, 0.5f * a));
        ImGui.Dummy(new Vector2(innerW, S(60f)));
        CenteredText(dl, min.X, size.X, Target, ImmersiveMode.TextColor, a);
        ImGui.Dummy(new Vector2(innerW, S(12f)));
        if (tabsLoading)
        {
            ImmersiveMode.ReadoutRow("Sections", $"{Profiles_DR.loadedTargetTabsCount} / {Profiles_DR.tabsTargetCount}");
            ImmersiveMode.SegmentBar(Profiles_DR.tabsTargetCount > 0 ? Profiles_DR.loadedTargetTabsCount / (float)Profiles_DR.tabsTargetCount : 0f, innerW);
            ImGui.Dummy(new Vector2(innerW, S(6f)));
        }
        if (galleryLoading)
        {
            ImmersiveMode.ReadoutRow("Imagery", $"{Profiles_DR.loadedTargetGalleryImages} / {Profiles_DR.TargetGalleryImagesToLoad}");
            ImmersiveMode.SegmentBar(Profiles_DR.TargetGalleryImagesToLoad > 0 ? Profiles_DR.loadedTargetGalleryImages / (float)Profiles_DR.TargetGalleryImagesToLoad : 0f, innerW);
        }
        if (!tabsLoading && !galleryLoading)
            ImmersiveMode.ReadoutRow("Status", "Requesting" + new string('.', 1 + (int)(ImmersiveMode.Time * 2f) % 3));
        EndPanel();
    }

    // Aether: an orb of light gathering motes as the link attunes.
    private static void DrawLoadingAether(bool tabsLoading, bool galleryLoading)
    {
        var S = (Func<float, float>)RsTheme.S;
        var size = new Vector2(S(400f), S(360f));
        var a = BeginPanel("attune_aether", null, CenterPanelPos(size), size, 0.5f, 0f, Motion.Drift, header: false);
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos();
        DrawCardClose(dl, min, size, a);
        var c = min + size * 0.5f - new Vector2(0, S(34f));
        var frac = LoadFraction(tabsLoading, galleryLoading, out var status);
        var f = frac < 0f ? 0.5f + 0.5f * MathF.Sin(ImmersiveMode.Time * 1.2f) : frac;
        var breathe = 0.85f + 0.15f * MathF.Sin(ImmersiveMode.Time * 2f);
        var core = new Vector4(0.92f, 0.97f, 1f, 1f);
        // Orb: soft layers that brighten and grow with progress.
        var R = S(46f + 26f * f) * breathe;
        for (int i = 8; i >= 1; i--)
            dl.AddCircleFilled(c, R * (0.5f + 0.5f * i / 8f) + S(20f), ImmersiveMode.Col(ImmersiveMode.Accent, 0.045f * a), 48);
        dl.AddCircleFilled(c, R * 0.55f, ImmersiveMode.Col(ImmersiveMode.Accent, 0.45f * a), 40);
        dl.AddCircleFilled(c, R * 0.28f, ImmersiveMode.Col(core, 0.9f * a), 32);
        // Motes spiralling in.
        for (int i = 0; i < 40; i++)
        {
            var r0 = Hash("am" + i); var r1 = Hash("bm" + i);
            var life = (ImmersiveMode.Time * (0.25f + r1 * 0.25f) + r0) % 1f;
            var ang = r0 * MathF.PI * 2f + life * 4f + ImmersiveMode.Time * 0.3f;
            var dist = S(150f) * (1f - life) + R * 0.4f;
            var p = c + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * dist;
            var ma = MathF.Sin(life * MathF.PI) * a * (0.4f + r1 * 0.6f);
            dl.AddCircleFilled(p, S(4f), ImmersiveMode.Col(ImmersiveMode.Accent, ma * 0.2f), 10);
            dl.AddCircleFilled(p, S(1.6f), ImmersiveMode.Col(core, ma), 8);
        }
        // Thin ring that closes as progress completes.
        if (frac >= 0f)
        {
            dl.PathArcTo(c, S(112f), -MathF.PI * 0.5f, -MathF.PI * 0.5f + MathF.PI * 2f * frac, 64);
            dl.PathStroke(ImmersiveMode.Col(ImmersiveMode.Accent, 0.7f * a), ImDrawFlags.None, S(2f));
        }
        ImGui.SetCursorScreenPos(new Vector2(min.X, c.Y + S(140f)));
        CenteredText(dl, min.X, size.X, "ATTUNING TO THE AETHER", ImmersiveMode.MutedColor, a);
        ImGui.Dummy(new Vector2(size.X, S(22f)));
        CenteredText(dl, min.X, size.X, Target, ImmersiveMode.TextColor, a);
        ImGui.Dummy(new Vector2(size.X, S(22f)));
        CenteredText(dl, min.X, size.X, status.ToUpperInvariant(), ImmersiveMode.MutedColor, a);
        EndPanel();
    }

    // Nymian: a rune ring carved clockwise as the ward attunes.
    private static void DrawLoadingTablet(bool tabsLoading, bool galleryLoading)
    {
        var S = (Func<float, float>)RsTheme.S;
        var size = new Vector2(S(380f), S(380f));
        var a = BeginPanel("attune", null, CenterPanelPos(size), size, 0.12f, 0f, Motion.RiseUp, header: false, bob: false);
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos();
        DrawCardClose(dl, min, size, a);
        var c = min + size * 0.5f - new Vector2(0, S(20f));
        var frac = LoadFraction(tabsLoading, galleryLoading, out var status);
        var R = S(96f);
        var stone = _theme.AccentSoft;
        // Groove.
        dl.AddCircle(c, R, ImmersiveMode.Col(new Vector4(0, 0, 0, 1), 0.5f * a), 72, S(10f));
        dl.AddCircle(c, R, ImmersiveMode.Col(stone, 0.35f * a), 72, S(8f));
        // Runes around the ring.
        for (int i = 0; i < 16; i++)
        {
            var ang = i * MathF.PI * 2f / 16f - MathF.PI * 0.5f;
            var p = c + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * (R + S(20f));
            var lit = frac < 0f ? (0.3f + 0.7f * MathF.Max(0f, MathF.Sin(ImmersiveMode.Time * 2f - i * 0.4f))) : (i / 16f <= frac ? 1f : 0.25f);
            var len = S(6f);
            var d = new Vector2(MathF.Cos(ang), MathF.Sin(ang));
            dl.AddLine(p - d * len, p + d * len, ImmersiveMode.Col(ImmersiveMode.Accent, lit * a), 2f);
            dl.AddLine(p - new Vector2(-d.Y, d.X) * len * 0.5f, p + new Vector2(-d.Y, d.X) * len * 0.5f, ImmersiveMode.Col(ImmersiveMode.Accent, lit * 0.6f * a), 1.5f);
        }
        // Progress arc glowing in the groove.
        var end = frac < 0f ? (ImmersiveMode.Time * 1.2f) % (MathF.PI * 2f) : MathF.PI * 2f * frac;
        var startAng = -MathF.PI * 0.5f + (frac < 0f ? end - 1.2f : 0f);
        var endAng = -MathF.PI * 0.5f + end;
        dl.PathArcTo(c, R, startAng, endAng, 64); dl.PathStroke(ImmersiveMode.Col(ImmersiveMode.Accent, 0.35f * a), ImDrawFlags.None, S(12f));
        dl.PathArcTo(c, R, startAng, endAng, 64); dl.PathStroke(ImmersiveMode.Col(ImmersiveMode.Accent, 0.95f * a), ImDrawFlags.None, S(4f));
        // Center glyph: the theme marker pulsing.
        var pulse = 0.6f + 0.4f * MathF.Sin(ImmersiveMode.Time * 2.5f);
        _theme.DrawMarker(dl, c, S(14f), ImmersiveMode.Col(ImmersiveMode.Accent, pulse * a));
        dl.AddCircle(c, S(24f), ImmersiveMode.Col(stone, 0.6f * a), 32, 1.5f);
        // Text.
        ImGui.SetCursorScreenPos(new Vector2(min.X, c.Y + R + S(40f)));
        CenteredText(dl, min.X, size.X, "ATTUNING WARD", ImmersiveMode.MutedColor, a);
        ImGui.Dummy(new Vector2(size.X, S(22f)));
        CenteredText(dl, min.X, size.X, Target, ImmersiveMode.TextColor, a);
        ImGui.Dummy(new Vector2(size.X, S(22f)));
        CenteredText(dl, min.X, size.X, status.ToUpperInvariant(), ImmersiveMode.MutedColor, a);
        EndPanel();
    }

    // Sharlayan: a quill transcribing lines onto the page.
    private static void DrawLoadingTome(bool tabsLoading, bool galleryLoading)
    {
        var S = (Func<float, float>)RsTheme.S;
        var size = new Vector2(MathF.Min(S(460f), _vpSize.X * 0.4f), S(300f));
        var a = BeginPanel("transcribe", "Transcribing record", CenterPanelPos(size), size, 0.3f, 0f, Motion.Unroll, bob: false);
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos();
        DrawCardClose(dl, min, size, a);
        var inset = _theme.ContentInset;
        var innerW = size.X - inset * 2f;
        var frac = LoadFraction(tabsLoading, galleryLoading, out var status);
        var ink = ImmersiveMode.TextColor;
        ImGui.Dummy(new Vector2(innerW, S(4f)));
        CenteredText(dl, min.X, size.X, Target, ImmersiveMode.Accent, a);
        ImGui.Dummy(new Vector2(innerW, S(26f)));

        // Ruled lines being written: overall progress fills line by line.
        int lines = 5;
        var lineGap = S(26f);
        var x0 = min.X + inset;
        var x1 = min.X + size.X - inset;
        var y0 = ImGui.GetCursorScreenPos().Y;
        var written = frac < 0f ? ((ImmersiveMode.Time * 0.35f) % 1f) * lines : frac * lines;
        for (int i = 0; i < lines; i++)
        {
            var y = y0 + i * lineGap;
            dl.AddLine(new Vector2(x0, y + S(8f)), new Vector2(x1, y + S(8f)), ImmersiveMode.Col(ink, 0.12f * a), 1f);
            var f = Math.Clamp(written - i, 0f, 1f);
            if (f <= 0f) continue;
            var segs = 40;
            var prev = new Vector2(x0, y);
            var seed = i * 7.3f;
            for (int s = 1; s <= (int)(segs * f); s++)
            {
                var t = s / (float)segs;
                var px = x0 + (x1 - x0) * t;
                var py = y + MathF.Sin(t * 41f + seed) * S(2.2f) + MathF.Sin(t * 13f + seed * 2f) * S(1.4f);
                var p = new Vector2(px, py);
                dl.AddLine(prev, p, ImmersiveMode.Col(ink, 0.85f * a), 1.6f);
                prev = p;
            }
            if (f < 1f)
            {
                // Quill tip: a small ink dot and a nib line trailing up-right.
                dl.AddCircleFilled(prev, S(2.2f), ImmersiveMode.Col(ink, a), 8);
                dl.AddLine(prev, prev + new Vector2(S(10f), -S(18f)), ImmersiveMode.Col(_theme.AccentSoft, 0.9f * a), 2f);
                dl.AddLine(prev + new Vector2(S(10f), -S(18f)), prev + new Vector2(S(16f), -S(30f)), ImmersiveMode.Col(_theme.AccentSoft, 0.6f * a), 1.5f);
            }
        }
        ImGui.SetCursorScreenPos(new Vector2(x0, y0 + lines * lineGap + S(10f)));
        ImmersiveMode.ReadoutRow("Entry", status);
        EndPanel();
    }

    // Voidtouched: a rift pulling itself together, embers everywhere.
    private static void DrawLoadingShards(bool tabsLoading, bool galleryLoading)
    {
        var S = (Func<float, float>)RsTheme.S;
        var size = new Vector2(S(400f), S(360f));
        var a = BeginPanel("rift", null, CenterPanelPos(size), size, 0.5f, 0f, Motion.Burn, header: false);
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos();
        DrawCardClose(dl, min, size, a);
        var c = min + size * 0.5f - new Vector2(0, S(30f));
        var frac = LoadFraction(tabsLoading, galleryLoading, out var status);
        var fl = 0.75f + 0.25f * Hash(MathF.Floor(ImmersiveMode.Time * 11f).ToString());
        // Jagged rotating rings.
        for (int ring = 0; ring < 3; ring++)
        {
            var R = S(40f + ring * 26f);
            var rot = ImmersiveMode.Time * (ring % 2 == 0 ? 0.5f : -0.35f) + ring;
            const int n = 48;
            var prev = Vector2.Zero;
            for (int i = 0; i <= n; i++)
            {
                var ang = i * MathF.PI * 2f / n + rot;
                var jit = (Hash($"rift{ring}_{i % n}") - 0.5f) * S(9f);
                var p = c + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * (R + jit);
                if (i > 0) dl.AddLine(prev, p, ImmersiveMode.Col(ImmersiveMode.Accent, (0.85f - ring * 0.22f) * fl * a), 1.5f);
                prev = p;
            }
        }
        // Progress: ring of burning cells.
        {
            var R = S(112f);
            const int cells = 28;
            var lit = frac < 0f ? (int)((ImmersiveMode.Time * 6f) % cells) : (int)MathF.Round(frac * cells);
            for (int i = 0; i < cells; i++)
            {
                var ang = i * MathF.PI * 2f / cells - MathF.PI * 0.5f;
                var p = c + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * R;
                var on = frac < 0f ? Math.Abs(((i - lit) % cells + cells) % cells) < 6 : i < lit;
                if (on)
                {
                    dl.AddCircleFilled(p, S(6f), ImmersiveMode.Col(ImmersiveMode.Accent, 0.25f * fl * a), 12);
                    dl.AddCircleFilled(p, S(2.6f), ImmersiveMode.Col(new Vector4(0.95f, 0.85f, 1f, 1f), 0.95f * fl * a), 10);
                }
                else
                    dl.AddCircleFilled(p, S(1.8f), ImmersiveMode.Col(ImmersiveMode.Accent, 0.22f * a), 8);
            }
        }
        // Core.
        dl.AddCircleFilled(c, S(18f) * fl, ImmersiveMode.Col(ImmersiveMode.Accent, 0.35f * a), 24);
        dl.AddCircleFilled(c, S(7f), ImmersiveMode.Col(new Vector4(0.97f, 0.9f, 1f, 1f), 0.95f * fl * a), 16);
        BurnFx.DrawEmbers(dl, c - new Vector2(S(120f)), c + new Vector2(S(120f)), ImmersiveMode.Accent, "rift", 0.9f * a, 30);
        // Text.
        ImGui.SetCursorScreenPos(new Vector2(min.X, c.Y + S(140f)));
        CenteredText(dl, min.X, size.X, "BINDING TETHER", ImmersiveMode.MutedColor, a * fl);
        ImGui.Dummy(new Vector2(size.X, S(22f)));
        CenteredText(dl, min.X, size.X, Target, ImmersiveMode.TextColor, a);
        ImGui.Dummy(new Vector2(size.X, S(22f)));
        CenteredText(dl, min.X, size.X, status.ToUpperInvariant(), ImmersiveMode.MutedColor, a);
        EndPanel();
    }

    private static void CenteredText(ImDrawListPtr dl, float x0, float w, string text, Vector4 col, float a)
    {
        var sz = ImGui.CalcTextSize(text);
        dl.AddText(new Vector2(x0 + (w - sz.X) * 0.5f, ImGui.GetCursorScreenPos().Y), ImmersiveMode.Col(col, a), text);
    }

    // access denied
    private static void DrawAccessPanel()
    {
        var S = (Func<float, float>)RsTheme.S;
        var size = new Vector2(MathF.Min(S(440f), _vpSize.X * 0.4f), S(220f));
        // Missing and private profiles look identical here, so nobody can tell whether a player has a profile without being accepted by them.
        var a = BeginPanel("access", "Access restricted", CenterPanelPos(size), size, 0.5f, 0f, EntranceMotion(), bob: false);
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos();
        var innerW = size.X - _theme.ContentInset * 2f;
        CenteredText(dl, min.X, size.X, Target, ImmersiveMode.TextColor, a);
        ImGui.Dummy(new Vector2(innerW, ImGui.GetTextLineHeight() + S(10f)));
        ImGui.PushStyleColor(ImGuiCol.Text, ImmersiveMode.MutedColor);
        ImGui.PushTextWrapPos(min.X + size.X - _theme.ContentInset);
        ImGui.TextUnformatted("This player either has no profile or has not granted you permission to view it. Request access and they can accept you if they have one.");
        ImGui.PopTextWrapPos();
        ImGui.PopStyleColor();
        ImGui.Dummy(new Vector2(innerW, S(10f)));
        if (ImmersiveMode.HoloButton("reqaccess", "Request Access", innerW))
        {
            Profiles_DS.SendProfileAccessUpdate(Plugin.character, Plugin.plugin.username, Plugin.plugin.playername, Plugin.plugin.playerworld,
                TargetProfileWindow.characterName, TargetProfileWindow.characterWorld, (int)UI.ConnectionStatus.pending);
        }
        ImGui.Dummy(new Vector2(innerW, S(6f)));
        if (ImmersiveMode.HoloButton("access_close", _theme.CloseLabel, innerW, danger: true, closeIcon: true))
            RequestClose();
        EndPanel();
    }

    // misc
    private static float Hash(string s)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (var c in s) { h ^= c; h *= 16777619; }
            return (h % 10007) / 10007f;
        }
    }
}
