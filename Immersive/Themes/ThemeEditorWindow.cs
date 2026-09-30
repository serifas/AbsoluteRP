using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AbsoluteRP.RsUI;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.ImGuiFileDialog;
using Networking;
using AbsoluteRP.Network;

namespace AbsoluteRP.Immersive.Themes;

// The theme editor: a canvas that mirrors the game viewport where panels are dragged and resized, an inspector for the selected panel and its elements, and tabs for palette / style / effects / publishing. Live preview pushes the document into ImmersiveThemes.PreviewOverride so the real HUD renders it while you view a profile.
public sealed class ThemeEditorWindow : Window
{
    public static ThemeEditorWindow? Instance;

    private ThemeDocument? _doc;
    private string _serverStatus = "";
    private string _reviewerNotes = "";
    private int _tab;
    // Which scene the Layout tab edits: 0 profile, 1 loading screen, 2 tooltip.
    private int _scene;
    private static readonly List<RsElements.NavItem> _sceneTabs = new()
    {
        new(FontAwesomeIcon.IdCard, "Profile"),
        new(FontAwesomeIcon.Spinner, "Loading screen"),
        new(FontAwesomeIcon.CommentDots, "Tooltip"),
    };
    private static readonly List<RsElements.NavItem> _tabs = new()
    {
        new(FontAwesomeIcon.ThLarge, "Layout"),
        new(FontAwesomeIcon.Palette, "Style"),
        new(FontAwesomeIcon.Magic, "Effects"),
        new(FontAwesomeIcon.Image, "Images"),
        new(FontAwesomeIcon.CloudUploadAlt, "Publish"),
    };

    private PanelDef? _sel;
    private int _selEl = -1;
    private int _addElType;
    // Element kinds an author can place. The profile controls are part of the section body and can't be placed on their own.
    private static readonly List<ElementType> AddableTypes = Enum.GetValues<ElementType>()
        .Where(t => Array.IndexOf(ThemeDocument.LegacyControlTypes, t) < 0).ToList();
    private bool _live;
    private bool _dragging, _resizing;
    private Vector2 _dragOffset;
    private string _status = "";
    private DateTime _statusAt;

    public ThemeEditorWindow() : base("Theme Editor##arp_theme_editor")
    {
        Instance = this;
        Size = new Vector2(1040, 680);
        SizeCondition = ImGuiCond.FirstUseEver;
        // The canvas draws its preview as child windows, so the editor can be raised and lowered like any other window.
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(760, 480), MaximumSize = new Vector2(4000, 3000) };
        ThemeNetwork.Saved += OnSaved;
    }

    public static void Open(ThemeDocument doc, string serverStatus = "", string reviewerNotes = "")
    {
        if (Instance == null) return;
        Instance._doc = doc.Normalize();
        // Every theme is a base (single-container) layout now: an older multi-panel document is rebuilt on the Standard layout, keeping its palette, style, effects and images.
        Instance._serverStatus = serverStatus ?? "";
        Instance._reviewerNotes = reviewerNotes ?? "";
        Instance._sel = doc.Panels.FirstOrDefault();
        Instance._selEl = -1;
        Instance._tab = 0;
        Instance._live = true;   // live preview on by default
        Instance.ResetHistory();
        ImmersiveHud.PreviewSession++;
        Instance.IsOpen = true;
        Instance.BringToFront();
    }

    public static void PreviewOnSelf(ThemeDocument doc)
    {
        ImmersiveThemes.PreviewOverride = ImmersiveThemes.FromDocument(doc);
        OpenOwnProfile();
    }

    private static void OpenOwnProfile()
    {
        var p = Plugin.plugin;
        if (p == null || Plugin.character == null) return;
        if (!p.Configuration.ImmersiveModeEnabled)
        {
            p.Configuration.ImmersiveModeEnabled = true;
            p.Configuration.Save();
        }
        p.OpenTargetWindow();
        TargetProfileWindow.characterName = p.playername;
        TargetProfileWindow.characterWorld = p.playerworld;
        TargetProfileWindow.RequestingProfile = true;
        TargetProfileWindow.ResetAllData();
        Profiles_DS.FetchProfile(Plugin.character, false, -1, p.playername, p.playerworld, -1);
    }

    public override void OnClose()
    {
        _live = false;
        ImmersiveThemes.PreviewOverride = null;
        base.OnClose();
    }

    private void OnSaved(int id, bool ok, string message)
    {
        if (!ok || _doc == null) return;
        if (_doc.ServerId == 0 && id > 0) _doc.ServerId = id;
        ThemeLibrary.SaveDraft(_doc);
        ThemeNetwork.RequestList("mine");
        Status(message);
    }

    private void Status(string s) { _status = s; _statusAt = DateTime.UtcNow; }

    private void MarkDirty()
    {
        if (_doc == null) return;
        _doc.UpdatedUtc = DateTime.UtcNow;
        ImmersiveThemes.InvalidateDocument(_doc);
    }

    // undo / redo The document is snapshotted as JSON whenever it changed and the change has settled (no drag, resize or active input), so a drag or a typed word becomes one step rather than one per frame.
    private readonly List<string> _undo = new();
    private readonly List<string> _redo = new();
    private string? _committed;           // the document as last recorded
    private DateTime _committedAt;
    private const int MaxHistory = 100;
    private static readonly Newtonsoft.Json.JsonSerializerSettings RestoreSettings = new()
    {
        ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace,
    };

    private void ResetHistory()
    {
        _undo.Clear(); _redo.Clear();
        _committed = _doc?.ToJson();
        _committedAt = _doc?.UpdatedUtc ?? DateTime.MinValue;
    }

    // Once per frame: record a settled change as an undo step.
    private void TrackHistory()
    {
        if (_doc == null) return;
        if (_committed == null) { ResetHistory(); return; }
        if (_doc.UpdatedUtc == _committedAt) return;
        if (_dragging || _resizing || ImGui.IsAnyItemActive() || ImGui.IsMouseDown(ImGuiMouseButton.Left)) return;
        var now = _doc.ToJson();
        if (now == _committed) { _committedAt = _doc.UpdatedUtc; return; }
        _undo.Add(_committed);
        if (_undo.Count > MaxHistory) _undo.RemoveAt(0);
        _redo.Clear();
        _committed = now;
        _committedAt = _doc.UpdatedUtc;
    }

    private void Restore(string json)
    {
        if (_doc == null) return;
        try
        {
            // Populate in place so the library and preview keep the same object.
            Newtonsoft.Json.JsonConvert.PopulateObject(json, _doc, RestoreSettings);
            _doc.LoadingPanels ??= new(); _doc.TooltipPanels ??= new();
            _sel = null; _selEl = -1; _fxCandidate = null;
            _doc.UpdatedUtc = DateTime.UtcNow;
            ImmersiveThemes.InvalidateDocument(_doc);
            _committed = json;
            _committedAt = _doc.UpdatedUtc;
        }
        catch (Exception ex) { Status("Couldn't restore: " + ex.Message); }
    }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Undo()
    {
        if (_doc == null || _undo.Count == 0) return;
        TrackHistory();                       // settle anything pending first
        var target = _undo[^1]; _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(_committed ?? _doc.ToJson());
        Restore(target);
        Status("Undone.");
    }

    public void Redo()
    {
        if (_doc == null || _redo.Count == 0) return;
        var target = _redo[^1]; _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(_committed ?? _doc.ToJson());
        Restore(target);
        Status("Redone.");
    }

    // Ctrl+Z / Ctrl+Shift+Z (or Ctrl+Y) while the editor is focused and no text field is being edited (those keep their own undo).
    private void HandleHistoryKeys()
    {
        if (!ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows)) return;
        if (ImGui.IsAnyItemActive()) return;
        var io = ImGui.GetIO();
        if (!io.KeyCtrl) return;
        if (ImGui.IsKeyPressed(ImGuiKey.Z, false))
        {
            if (io.KeyShift) Redo(); else Undo();
        }
        else if (ImGui.IsKeyPressed(ImGuiKey.Y, false)) Redo();
    }

    public override void Draw()
    {
        if (_doc == null)
        {
            ImGui.TextUnformatted("No theme loaded. Open one from the Themes tab.");
            return;
        }
        var S = (Func<float, float>)RsTheme.S;

        // Live preview: keep the override pointed at the current revision.
        if (_live) ImmersiveThemes.PreviewOverride = ImmersiveThemes.FromDocument(_doc);
        else if (ImmersiveThemes.PreviewOverride?.Document == _doc) ImmersiveThemes.PreviewOverride = null;

        TrackHistory();
        HandleHistoryKeys();

        // toolbar
        var name = _doc.Name;
        ImGui.AlignTextToFramePadding();
        if (RsElements.InputText("ed_name", ref name, 120, "Theme name", S(220f))) { _doc.Name = name; MarkDirty(); }
        ImGui.SameLine();
        if (RsElements.Button("Save draft", RsElements.ButtonVariant.Secondary)) { ThemeLibrary.SaveDraft(_doc); Status("Draft saved."); }
        ImGui.SameLine();
        if (RsElements.Button(_doc.ServerId > 0 ? "Update on server" : "Save to server", RsElements.ButtonVariant.Primary))
        {
            ThemeLibrary.SaveDraft(_doc);
            ThemeNetwork.Save(_doc);
            Status("Sending…");
        }
        ImGui.SameLine();
        if (_doc.ServerId > 0)
        {
            if (RsElements.Button("Submit for review", RsElements.ButtonVariant.Secondary)) { ThemeNetwork.Submit(_doc.ServerId); Status("Submitted."); }
            ImGui.SameLine();
        }
        using (Dalamud.Interface.Utility.Raii.ImRaii.Disabled(!CanUndo))
        {
            if (RsElements.IconButton(FontAwesomeIcon.Undo, "ed_undo", RsElements.ButtonVariant.Ghost, 26f)) Undo();
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(CanUndo ? $"Undo (Ctrl+Z) — {_undo.Count} step{(_undo.Count == 1 ? "" : "s")}" : "Nothing to undo (Ctrl+Z)");
        ImGui.SameLine(0f, S(2f));
        using (Dalamud.Interface.Utility.Raii.ImRaii.Disabled(!CanRedo))
        {
            if (RsElements.IconButton(FontAwesomeIcon.Redo, "ed_redo", RsElements.ButtonVariant.Ghost, 26f)) Redo();
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(CanRedo ? $"Redo (Ctrl+Shift+Z) — {_redo.Count} step{(_redo.Count == 1 ? "" : "s")}" : "Nothing to redo (Ctrl+Shift+Z)");
        ImGui.SameLine();
        var live = _live;
        if (RsElements.Toggle("ed_live", ref live, "Live preview")) _live = live;
        ImGui.SameLine();
        if (RsElements.Button("View on my profile", RsElements.ButtonVariant.Ghost)) { _live = true; OpenOwnProfile(); }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Opens your own profile in immersive mode with this theme applied.");
        ImGui.SameLine();
        if (RsElements.Button("Close profile view", RsElements.ButtonVariant.Ghost))
        {
            // Escape hatch: dismisses the full-screen HUD and any pending content warning so nothing can stay stuck over the screen.
            TargetProfileWindow.warning = false;
            Plugin.plugin.CloseTargetWindow();
        }

        var msg = (DateTime.UtcNow - _statusAt).TotalSeconds < 6 ? _status
                : (DateTime.UtcNow - ThemeNetwork.LastMessageAt).TotalSeconds < 6 ? ThemeNetwork.LastMessage : "";
        if (!string.IsNullOrEmpty(msg))
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted("  " + msg);
            ImGui.PopStyleColor();
        }

        RsElements.NavigationMenu("ed_tabs", ref _tab, _tabs);
        ImGui.Spacing();

        switch (_tab)
        {
            case 0: DrawLayoutTab(); break;
            case 1: DrawStyleTab(); break;
            case 2: DrawEffectsTab(); break;
            case 3: DrawImagesTab(); break;
            case 4: DrawPublishTab(); break;
        }
        _fileDialog.Draw();
    }

    // Images tab: the theme's own pictures
    private readonly AbsoluteRP.RsUI.RsFileDialogManager _fileDialog = new();
    private int _assetSel = -1;
    private string _assetError = "";

    private void DrawImagesTab()
    {
        var S = (Func<float, float>)RsTheme.S;
        var doc = _doc!;
        ImGui.BeginChild("ed_images", ImGui.GetContentRegionAvail(), false);
        if (RsElements.BeginPanel("ed_img_list", "Images in this theme", fitContentsX: false, fitContentsY: true))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextWrapped("Add PNG or JPG pictures and use them as panel or element backgrounds, frames over the rim, masks that cut the shape of avatars, gallery pictures and backgrounds, or free-standing Image elements. Pictures travel inside the theme: they are downscaled to 2048 px and each must stay under 8 MB, so a theme full of large pictures takes longer to install.");
            ImGui.PopStyleColor();
            var total = doc.Assets.Sum(a => a.Bytes);
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted($"{doc.Assets.Count} / {ThemeAssets.MaxAssets} images · {total / 1024} KB");
            ImGui.PopStyleColor();
            if (doc.Assets.Count < ThemeAssets.MaxAssets && RsElements.Button("+ Add image…", RsElements.ButtonVariant.Primary))
            {
                _fileDialog.OpenFileDialog("Add image to theme", "Images{.png,.jpg,.jpeg,.webp,.bmp}", (ok, files) =>
                {
                    if (!ok || files == null) return;
                    foreach (var f in files)
                    {
                        var a = ThemeAssets.Import(f, out var err);
                        if (a == null) { _assetError = err; continue; }
                        if (_doc!.Assets.Count >= ThemeAssets.MaxAssets) { _assetError = "Image limit reached."; break; }
                        _doc.Assets.Add(a); _assetSel = _doc.Assets.Count - 1; _assetError = ""; MarkDirty();
                    }
                }, 8, null, true);
            }
            Hint("Transparent PNGs work best for frames and masks. A mask's transparent parts are cut away; if it has no transparency, dark areas are cut instead.");
            if (!string.IsNullOrEmpty(_assetError))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentDanger);
                ImGui.TextWrapped(_assetError);
                ImGui.PopStyleColor();
            }
            ImGui.Spacing();

            // Grid of thumbnails.
            var cell = S(110f);
            var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (cell + S(10f))));
            for (int i = 0; i < doc.Assets.Count; i++)
            {
                var a = doc.Assets[i];
                if (i % perRow != 0) ImGui.SameLine();
                ImGui.BeginGroup();
                var pos = ImGui.GetCursorScreenPos();
                var dl = ImGui.GetWindowDrawList();
                dl.AddRectFilled(pos, pos + new Vector2(cell), ImGui.ColorConvertFloat4ToU32(new Vector4(0.10f, 0.11f, 0.14f, 1f)), S(3f));
                // checker so transparency is visible
                for (int cy = 0; cy < 6; cy++) for (int cx = 0; cx < 6; cx++)
                    if ((cx + cy) % 2 == 0) dl.AddRectFilled(pos + new Vector2(cx, cy) * (cell / 6f), pos + new Vector2(cx + 1, cy + 1) * (cell / 6f), ImGui.ColorConvertFloat4ToU32(new Vector4(0.16f, 0.17f, 0.2f, 1f)));
                var tex = ThemeAssets.Texture(a);
                if (tex != null && tex.Handle != IntPtr.Zero)
                {
                    var ia = a.Width / (float)Math.Max(1, a.Height);
                    var w = ia >= 1f ? cell - S(8f) : (cell - S(8f)) * ia;
                    var h = ia >= 1f ? (cell - S(8f)) / ia : cell - S(8f);
                    var o = pos + new Vector2((cell - w) * 0.5f, (cell - h) * 0.5f);
                    dl.AddImage(tex.Handle, o, o + new Vector2(w, h));
                }
                if (ImGui.InvisibleButton("##asset" + a.Id, new Vector2(cell))) _assetSel = i;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{a.Name}\n{a.Width}×{a.Height} · {a.Bytes / 1024} KB");
                dl.AddRect(pos, pos + new Vector2(cell), ImGui.ColorConvertFloat4ToU32(i == _assetSel ? RsTheme.AccentPrimary : new Vector4(1, 1, 1, 0.12f)), S(3f), ImDrawFlags.None, i == _assetSel ? 2f : 1f);
                var nm = a.Name.Length > 14 ? a.Name[..14] + "…" : a.Name;
                ImGui.PushStyleColor(ImGuiCol.Text, i == _assetSel ? RsTheme.TextPrimary : RsTheme.TextMuted);
                ImGui.TextUnformatted(nm);
                ImGui.PopStyleColor();
                ImGui.EndGroup();
            }
        }
        RsElements.EndPanel();
        ImGui.Spacing();

        if (_assetSel >= 0 && _assetSel < doc.Assets.Count)
        {
            var a = doc.Assets[_assetSel];
            if (RsElements.BeginPanel("ed_img_sel", "Selected image", fitContentsX: false, fitContentsY: true))
            {
                var n = a.Name;
                if (RsElements.InputText("ed_img_name", ref n, 40, "Name", W(0.95f))) { a.Name = n; MarkDirty(); }
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextUnformatted($"{a.Width}×{a.Height} · {a.Bytes / 1024} KB");
                var uses = CountUses(doc, a.Id);
                ImGui.TextUnformatted(uses == 0 ? "Not used yet" : $"Used in {uses} place{(uses == 1 ? "" : "s")}");
                ImGui.PopStyleColor();
                if (RsElements.Button("Remove image", RsElements.ButtonVariant.Danger))
                {
                    doc.Assets.RemoveAt(_assetSel); doc.ScrubImageRefs(); _assetSel = Math.Min(_assetSel, doc.Assets.Count - 1); MarkDirty();
                }
                Hint("Removes it from the theme and clears everywhere it was used.");
            }
            RsElements.EndPanel();
            ImGui.Spacing();
        }

        if (RsElements.BeginPanel("ed_img_theme", "Theme-wide pictures", fitContentsX: false, fitContentsY: true))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextWrapped("Applied to every gallery picture and (unless an avatar element says otherwise) the portrait.");
            ImGui.PopStyleColor();
            var gm = doc.GalleryMask; if (DrawImageRef("gal_mask", "Gallery picture mask", ref gm, ImageRefKind.Mask)) { doc.GalleryMask = gm; MarkDirty(); }
            var gf = doc.GalleryFrame; if (DrawImageRef("gal_frame", "Gallery picture frame", ref gf, ImageRefKind.Frame)) { doc.GalleryFrame = gf; MarkDirty(); }
            var am = doc.AvatarMask; if (DrawImageRef("av_mask", "Avatar mask", ref am, ImageRefKind.Mask)) { doc.AvatarMask = am; MarkDirty(); }
            var af = doc.AvatarFrame; if (DrawImageRef("av_frame", "Avatar frame", ref af, ImageRefKind.Frame)) { doc.AvatarFrame = af; MarkDirty(); }
        }
        RsElements.EndPanel();
        ImGui.EndChild();
    }

    private static int CountUses(ThemeDocument doc, string id)
    {
        int n = 0;
        void C(ImageRef? r) { if (r != null && r.Asset == id) n++; }
        C(doc.GalleryMask); C(doc.GalleryFrame); C(doc.AvatarMask); C(doc.AvatarFrame);
        foreach (var p in doc.Panels)
        {
            C(p.Background); C(p.Frame); C(p.MaskImage);
            foreach (var e in p.Elements) { C(e.Image); if (e.Style != null) { C(e.Style.Background); C(e.Style.Frame); C(e.Style.MaskImage); } }
        }
        return n;
    }

    private enum ImageRefKind { Background, Frame, Mask, Picture }

    // One picture slot: which image, plus the knobs that make sense for that use. Returns true when anything changed.
    private bool DrawImageRef(string id, string label, ref ImageRef? r, ImageRefKind kind)
    {
        var S = (Func<float, float>)RsTheme.S;
        var doc = _doc!;
        var changed = false;
        var allowProfileBg = kind == ImageRefKind.Background || kind == ImageRefKind.Picture;
        var names = new List<string> { "(none)" };
        if (allowProfileBg) names.Add("Profile background (image / video)");
        var offset = names.Count;   // index of the first theme image
        names.AddRange(doc.Assets.Select(a => a.Name));
        var cur = 0;
        if (r != null && r.IsSet)
        {
            if (r.Asset == ThemeAssets.ProfileBackgroundId) cur = allowProfileBg ? 1 : 0;
            else { var wanted = r.Asset; var i = doc.Assets.FindIndex(a => a.Id == wanted); cur = i < 0 ? 0 : i + offset; }
        }
        ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted(label); ImGui.SameLine(S(150f));
        if (RsElements.Dropdown("imgref_" + id, ref cur, names, W(0.55f)))
        {
            if (cur == 0) r = null;
            else if (allowProfileBg && cur == 1)
            {
                r ??= new ImageRef();
                r.Asset = ThemeAssets.ProfileBackgroundId;
                r.Fit = ImageFit.Cover;
            }
            else
            {
                r ??= new ImageRef();
                r.Asset = doc.Assets[cur - offset].Id;
                if (kind == ImageRefKind.Frame && r.Border <= 0f) r.Border = 24f;
                if (kind == ImageRefKind.Background || kind == ImageRefKind.Mask) r.Fit = ImageFit.Stretch;
            }
            changed = true;
        }
        string tip;
        switch (kind)
        {
            case ImageRefKind.Background: tip = "Drawn behind the content, stretched or cropped to fit. \"Profile background\" shows the viewed profile's own backdrop, video included."; break;
            case ImageRefKind.Frame: tip = "Drawn over the rim. Use Corner size to 9-slice it so the corners keep their shape while the edges stretch."; break;
            case ImageRefKind.Mask: tip = "Its transparent parts are cut away from the picture underneath. No transparency? Dark areas are cut instead."; break;
            default: tip = "The picture to show."; break;
        }
        Hint(tip);
        if (doc.Assets.Count == 0) { ImGui.SameLine(); ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted); ImGui.TextUnformatted("add images in the Images tab"); ImGui.PopStyleColor(); }
        if (r == null || !r.IsSet) return changed;
        ImGui.Indent(S(12f));
        ImGui.SetNextItemWidth(W());
        var al = r.Alpha; if (ImGui.SliderFloat("Opacity##" + id, ref al, 0.05f, 1f, "%.2f")) { r.Alpha = al; changed = true; }
        if (kind != ImageRefKind.Mask)
        {
            if (kind == ImageRefKind.Frame)
            {
                ImGui.SetNextItemWidth(W());
                var b = r.Border; if (ImGui.SliderFloat("Corner size##" + id, ref b, 0f, 120f, "%.0f px")) { r.Border = b; changed = true; }
                Hint("0 stretches the whole picture. Above 0, the corners stay this size and only the edges stretch (9-slice).");
            }
            if (r.Border <= 0f)
            {
                var fit = (int)r.Fit;
                ImGui.SetNextItemWidth(W());
                if (RsElements.Dropdown("fit_" + id, ref fit, new[] { "Stretch", "Cover (crop)", "Contain (fit inside)" }, S(180f))) { r.Fit = (ImageFit)fit; changed = true; }
                ImGui.SetNextItemWidth(W());
                var ro = r.Rounding; if (ImGui.SliderFloat("Rounding##" + id, ref ro, 0f, 60f, "%.0f px")) { r.Rounding = ro; changed = true; }
            }
            ImGui.SetNextItemWidth(W());
            var ins = r.Inset; if (ImGui.SliderFloat("Inset##" + id, ref ins, -40f, 40f, "%.0f px")) { r.Inset = ins; changed = true; }
            Hint("Shrinks (or, negative, grows) the picture's box.");
            var tinted = r.Tint != null;
            if (RsElements.Toggle("tint_" + id, ref tinted, "Tint")) { r.Tint = tinted ? new ThemeColor(1f, 1f, 1f, 1f) : null; changed = true; }
            if (r.Tint != null)
            {
                ImGui.SameLine();
                var v = r.Tint.V;
                ImGui.SetNextItemWidth(S(150f));
                if (ImGui.ColorEdit4("##tintc_" + id, ref v, ImGuiColorEditFlags.NoInputs)) { r.Tint = new ThemeColor(v); changed = true; }
            }
        }
        ImGui.Unindent(S(12f));
        return changed;
    }

    // Layout tab: canvas + inspector
    private void DrawLayoutTab()
    {
        var S = (Func<float, float>)RsTheme.S;
        var doc = _doc!;
        // Scene strip: which of the profile's windows the tools apply to.
        var before = _scene;
        RsElements.NavigationMenu("ed_scene", ref _scene, _sceneTabs);
        if (_scene != before)
        {
            _sel = null; _selEl = -1; _fxCandidate = null;
            _dragging = _resizing = _draggingEl = _resizingEl = false;
            TargetProfileWindow.ShowEquipmentInspect = false;
        }
        if (_scene == 0)
        {
            // Which dossier page is shown and edited. An element can carry a different style per page (see the inspector).
            var names = PageNames();
            if (names.Count > 0)
            {
                ImGui.SameLine(0f, S(18f));
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted("Dossier page:");
                ImGui.SameLine();
                if (_previewTab >= names.Count) _previewTab = 0;
                // Any pick (the current page included) returns the preview to that page's content, leaving the equipment / inventory view.
                var ddWasOpen = ImGui.IsPopupOpen("##rs_dd_pop_ed_prev_tab");
                var prevTab = _previewTab;
                var ddChanged = RsElements.Dropdown("ed_prev_tab", ref _previewTab, names, S(170f));
                if (ddChanged || _previewTab != prevTab || (ddWasOpen && !ImGui.IsPopupOpen("##rs_dd_pop_ed_prev_tab")))
                    TargetProfileWindow.ShowEquipmentInspect = false;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("The preview shows this page. Styles marked \"only on this page\" in the inspector apply to it alone.");
            }
        }
        ImGui.Spacing();
        doc.EditScene = _scene;
        try { DrawLayoutTabInner(); }
        finally { doc.EditScene = 0; }
    }

    private void DrawLayoutTabInner()
    {
        var S = (Func<float, float>)RsTheme.S;
        var doc = _doc!;
        if (_scene != 0 && doc.Panels.Count == 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextWrapped(_scene == 1
                ? "This theme uses the material's built-in loading screen. Start a custom one to design what shows while a profile loads (name, progress, anything you like)."
                : "This theme uses the material's built-in player tooltip. Start a custom one to lay out the hover tooltip yourself.");
            ImGui.PopStyleColor();
            if (RsElements.Button(_scene == 1 ? "Start a custom loading screen" : "Start a custom tooltip", RsElements.ButtonVariant.Primary))
            {
                if (_scene == 1) doc.LoadingPanels = ThemeDocument.DefaultLoadingScene(); else doc.TooltipPanels = ThemeDocument.DefaultTooltipScene();
                _sel = doc.Panels.FirstOrDefault(); _selEl = -1; MarkDirty();
            }
            return;
        }
        if (_scene != 0)
        {
            if (RsElements.Button(_scene == 1 ? "Use the built-in loading screen instead" : "Use the built-in tooltip instead", RsElements.ButtonVariant.Ghost))
            {
                doc.Panels.Clear(); _sel = null; _selEl = -1; MarkDirty();
                return;
            }
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted(_scene == 1 ? "Loading screen: the whole screen is the canvas." : "Tooltip: the canvas is the tooltip's box. It sizes itself to fit its contents.");
            ImGui.PopStyleColor();
        }
        var avail = ImGui.GetContentRegionAvail();
        var inspW = S(400f);
        ImGui.BeginChild("ed_canvas_child", new Vector2(avail.X - inspW - S(8f), avail.Y), false);
        DrawCanvas();
        ImGui.EndChild();
        ImGui.SameLine();
        ImGui.BeginChild("ed_inspector", new Vector2(inspW, avail.Y), false);
        DrawInspector();
        ImGui.EndChild();
    }

    private Vector2 _canvasOrigin, _canvasSize;

    private Vector2 PanelMin(PanelDef p, Vector2 size)
    {
        var cw = _canvasSize.X; var ch = _canvasSize.Y; var o = _canvasOrigin;
        return p.Anchor switch
        {
            PanelAnchor.TopRight    => new Vector2(o.X + cw - p.X * cw - size.X, o.Y + p.Y * ch),
            PanelAnchor.BottomLeft  => new Vector2(o.X + p.X * cw, o.Y + ch - p.Y * ch - size.Y),
            PanelAnchor.BottomRight => new Vector2(o.X + cw - p.X * cw - size.X, o.Y + ch - p.Y * ch - size.Y),
            PanelAnchor.Center      => new Vector2(o.X + p.X * cw - size.X * 0.5f, o.Y + p.Y * ch - size.Y * 0.5f),
            _                       => new Vector2(o.X + p.X * cw, o.Y + p.Y * ch),
        };
    }

    private void SetPanelRect(PanelDef p, Vector2 min, Vector2 size, Vector2 refMin, Vector2 refSize)
    {
        var cw = MathF.Max(1f, refSize.X); var ch = MathF.Max(1f, refSize.Y); var o = refMin;
        float Snap(float v) => MathF.Round(v / 0.005f) * 0.005f;
        // The main panel stays pinned top-right but its SIZE is the author's to set. Root panels move freely on the canvas; Inside children stay in their parent; edge-attached children slide along the edge.
        if (_scene == 0 && _doc != null && _doc.MainPanel == p)
        {
            p.W = Snap(Math.Clamp(size.X / cw, 0.25f, 1f));
            p.H = Snap(Math.Clamp(size.Y / ch, 0.25f, 1f));
            MarkDirty();
            return;
        }
        var attached = !string.IsNullOrEmpty(p.Parent) && p.Attach != PanelAttach.Inside;
        if (attached)
        {
            // Glued to an edge: size is free (up to 2x the parent), the drag only slides along the edge.
            size.X = Math.Clamp(size.X, RsTheme.S(24f), cw * 2f);
            size.Y = Math.Clamp(size.Y, RsTheme.S(16f), ch * 2f);
            p.W = Snap(size.X / cw); p.H = Snap(size.Y / ch);
            if (p.Attach == PanelAttach.Above || p.Attach == PanelAttach.Below)
                p.X = Snap(Math.Clamp((min.X - o.X) / cw, -0.5f, 1.5f));
            else
                p.Y = Snap(Math.Clamp((min.Y - o.Y) / ch, -0.5f, 1.5f));
            MarkDirty();
            return;
        }
        size.X = Math.Clamp(size.X, MathF.Min(RsTheme.S(24f), cw), cw);
        size.Y = Math.Clamp(size.Y, MathF.Min(RsTheme.S(16f), ch), ch);
        min.X = Math.Clamp(min.X, o.X, MathF.Max(o.X, o.X + cw - size.X));
        min.Y = Math.Clamp(min.Y, o.Y, MathF.Max(o.Y, o.Y + ch - size.Y));
        var fx = (min.X - o.X) / cw; var fy = (min.Y - o.Y) / ch;
        var W = size.X / cw; var H = size.Y / ch;
        p.W = Snap(W); p.H = Snap(H);
        switch (p.Anchor)
        {
            case PanelAnchor.TopRight:    p.X = Snap(1f - fx - W); p.Y = Snap(fy); break;
            case PanelAnchor.BottomLeft:  p.X = Snap(fx); p.Y = Snap(1f - fy - H); break;
            case PanelAnchor.BottomRight: p.X = Snap(1f - fx - W); p.Y = Snap(1f - fy - H); break;
            case PanelAnchor.Center:      p.X = Snap(fx + W * 0.5f); p.Y = Snap(fy + H * 0.5f); break;
            default:                      p.X = Snap(fx); p.Y = Snap(fy); break;
        }
        MarkDirty();
    }

    private int _previewTab;
    private static List<string> PageNames()
        => (BuildPreviewProfile().customTabs ?? new List<CustomTab>()).Where(t => t != null && !string.IsNullOrEmpty(t.Name)).Select(t => t.Name).ToList();
    private string? CurrentPage() { var n = PageNames(); return _previewTab >= 0 && _previewTab < n.Count ? n[_previewTab] : null; }

    // Sample content for the live preview: the user's own profile as it is in the Profiles page, so what they see is what others will.
    private static ProfileData BuildPreviewProfile()
    {
        var cur = AbsoluteRP.RsUI.Pages.ProfilesPage.CurrentProfile;
        var avatar = AbsoluteRP.RsUI.Pages.ProfilesPage.currentAvatarImg;
        if (avatar == null || avatar.Handle == IntPtr.Zero) avatar = UI.UICommonImage(UI.CommonImageTypes.avatarHolder);
        return new ProfileData
        {
            title = string.IsNullOrWhiteSpace(cur?.title) ? "Your Title" : cur!.title,
            titleColor = cur?.titleColor ?? new Vector4(1f, 1f, 1f, 1f),
            customTabs = cur?.customTabs ?? new List<CustomTab>(),
            avatar = avatar,
            avatarBytes = cur?.avatarBytes ?? Array.Empty<byte>(),   // lets mask images cut the portrait in the preview
            background = AbsoluteRP.RsUI.Pages.ProfilesPage.backgroundImage,
            equipmentPublic = cur?.equipmentPublic ?? false,
            accountID = 0,
        };
    }

    private void DrawCanvas()
    {
        var S = (Func<float, float>)RsTheme.S;
        var doc = _doc!;
        var avail = ImGui.GetContentRegionAvail();
        var vp = ImGui.GetMainViewport().WorkSize;
        var aspect = vp.X > 0 ? vp.Y / vp.X : 0.5625f;
        // The tooltip scene edits the tooltip's own box, sized to its contents.
        var tipBox = _scene == 2 ? ImmersiveHud.TooltipBoxSize(doc, BuildPreviewProfile(), sample: true) : Vector2.Zero;
        if (_scene == 2) aspect = tipBox.Y / MathF.Max(1f, tipBox.X);

        // Header row: hint + section picker (the preview's tabs can't be clicked, since clicks select elements instead).
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        ImGui.TextUnformatted("Click a panel to select it, then click its elements · drag to move · drag the corner to resize · drag the name tag to move a covered panel · Alt+click picks panels only");
        ImGui.PopStyleColor();
        var pd = BuildPreviewProfile();
        avail = ImGui.GetContentRegionAvail();

        // Fill the area beside the inspector (positions are fractions of the canvas, so the layout stretches with it like a backdrop does). The canvas keeps the game's screen shape (letterboxed in the space beside the inspector): panel positions are fractions of it, so a canvas of a different shape would place them differently in game.
        var cw = MathF.Max(S(160f), avail.X - S(4f));
        var ch = MathF.Max(S(120f), avail.Y - S(8f));
        if (_scene == 2) cw = MathF.Min(cw, tipBox.X * 1.6f);   // keep the tooltip box near its real size
        if (cw * aspect > ch) cw = MathF.Max(S(160f), ch / MathF.Max(0.01f, aspect)); else ch = MathF.Max(S(120f), cw * aspect);
        var o = ImGui.GetCursorScreenPos();
        var slack = ImGui.GetContentRegionAvail();
        o.X += MathF.Max(0f, (slack.X - S(4f) - cw) * 0.5f);
        o.Y += MathF.Max(0f, (slack.Y - S(8f) - ch) * 0.5f);
        ImGui.SetCursorScreenPos(o);
        _canvasOrigin = o;
        _canvasSize = new Vector2(cw, ch);
        var max = o + _canvasSize;
        var dl = ImGui.GetWindowDrawList();
        var theme = ImmersiveThemes.FromDocument(doc);

        // Backdrop: a stand-in for the game with the theme's veil on top.
        dl.AddRectFilledMultiColor(o, max,
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.16f, 0.18f, 0.22f, 1f)), ImGui.ColorConvertFloat4ToU32(new Vector4(0.12f, 0.14f, 0.18f, 1f)),
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.08f, 0.09f, 0.11f, 1f)), ImGui.ColorConvertFloat4ToU32(new Vector4(0.10f, 0.11f, 0.13f, 1f)));
        dl.AddRectFilled(o, max, ImmersiveMode.Col(theme.Scrim, theme.ScrimAlpha));
        dl.PushClipRect(o, max, true);
        try { theme.DrawVeilDecor(dl, o, max, 0.9f); } catch { }
        dl.PopClipRect();
        dl.AddRect(o, max, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 0.12f)));

        ImGui.InvisibleButton("##ed_canvas", _canvasSize);
        var mouse = ImGui.GetMousePos();
        // Hover by geometry + "the editor (or one of its children, the preview panels included) is the hovered window", so a scrolling section body or a nested child can't swallow the pick.
        var hovered = mouse.X >= o.X && mouse.Y >= o.Y && mouse.X <= max.X && mouse.Y <= max.Y
                   && ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);

        // Live render of the actual theme into the canvas. A candidate effect rides along on the selected element for the render only.
        ElementStyle? tryStyle = null;
        if (_fxCandidate != null && _sel != null && _selEl >= 0 && _selEl < _sel.Elements.Count && _sel.Elements[_selEl].Style != null)
        {
            tryStyle = _sel.Elements[_selEl].Style;
            tryStyle!.Fx ??= new List<string>();
            tryStyle.Fx.Add(_fxCandidate);
        }
        try
        {
            ImmersiveHud.DrawPreview(doc, o, _canvasSize, pd,
                Plugin.plugin?.playername ?? "Player", Plugin.plugin?.playerworld ?? "World", _previewTab, _scene == 2 ? cw / MathF.Max(1f, tipBox.X) : cw / MathF.Max(1f, vp.X));
        }
        finally
        {
            if (tryStyle != null) tryStyle.Fx.RemoveAt(tryStyle.Fx.Count - 1);
        }

        // hit-testing on the recorded rects Two-stage selection: a click picks the panel visually on top at the mouse (draw order decides); once a panel is selected, clicks inside it pick its elements. Panels are considered in DRAW order so whatever is in front wins.
        var byId = doc.Panels.ToDictionary(p => "doc_" + p.Id, p => p);
        var order = new Dictionary<PanelDef, int>();
        { int n = 0; foreach (var p in ImmersiveHud.DrawOrder(doc)) order[p] = n++; }
        ImmersiveHud.EditRect? hoverPanel = null; int hoverPanelOrder = -1;
        ImmersiveHud.EditRect? hoverEl = null; float hoverElArea = float.MaxValue;
        ImmersiveHud.EditRect? selPanelRect = null, selElRect = null;
        static bool Contains(ImmersiveHud.EditRect r, Vector2 m) => m.X >= r.Min.X && m.X <= r.Max.X && m.Y >= r.Min.Y && m.Y <= r.Max.Y;
        var altPick = ImGui.GetIO().KeyAlt;   // Alt: panels only, never elements
        foreach (var r in ImmersiveHud.EditRects)
        {
            if (!byId.TryGetValue(r.PanelId, out var pdef)) continue;
            if (r.ElementIndex < 0)
            {
                if (pdef == _sel) selPanelRect = r;
                var ord = order.TryGetValue(pdef, out var oi) ? oi : -1;
                if (!pdef.Locked && hovered && Contains(r, mouse) && ord > hoverPanelOrder) { hoverPanelOrder = ord; hoverPanel = r; }
            }
            else if (pdef == _sel && r.ElementIndex == _selEl) selElRect = r;
        }
        // Elements are only pickable inside the panel that is already selected (and only when it is the one on top under the mouse).
        if (hoverPanel != null && !altPick && _sel != null && byId[hoverPanel.PanelId] == _sel)
        {
            foreach (var r in ImmersiveHud.EditRects)
            {
                if (r.ElementIndex < 0 || r.PanelId != hoverPanel.PanelId) continue;
                var locked = r.ElementIndex < _sel.Elements.Count && _sel.Elements[r.ElementIndex].Locked;
                var area = (r.Max.X - r.Min.X) * (r.Max.Y - r.Min.Y);
                if (!locked && Contains(r, mouse) && area < hoverElArea) { hoverElArea = area; hoverEl = r; }
            }
        }

        // A generous grip: the corner square plus a margin around it, so it's easy to hit even when neighbouring elements overlap.
        var handle = S(16f);
        var grip = S(8f);
        bool InHandle(ImmersiveHud.EditRect r) =>
            mouse.X >= r.Max.X - handle - grip && mouse.Y >= r.Max.Y - handle - grip && mouse.X <= r.Max.X + grip && mouse.Y <= r.Max.Y + grip;
        var selElLocked = _sel != null && (_sel.Locked || (_selEl >= 0 && _selEl < _sel.Elements.Count && _sel.Elements[_selEl].Locked));
        var selPanelLocked = _sel != null && _sel.Locked;
        // The selection's grips always win over whatever is under them - that's what makes resizing possible over other panels/elements.
        var onElGrip = hovered && selElRect != null && _sel != null && _selEl >= 0 && _selEl < _sel.Elements.Count && !selElLocked && InHandle(selElRect);
        var onPanelGrip = hovered && !onElGrip && selPanelRect != null && !selPanelLocked && InHandle(selPanelRect);
        // The selected panel's name tag doubles as a drag handle, so a panel whose surface is fully covered by elements can still be moved.
        var chipMin = Vector2.Zero; var chipMax = Vector2.Zero;
        if (selPanelRect != null && _sel != null)
        {
            var nameSz0 = ImGui.CalcTextSize((_sel.Locked ? "[locked] " : "") + (_sel.Name ?? ""));
            chipMin = selPanelRect.Min - new Vector2(0, nameSz0.Y + S(4f));
            chipMax = selPanelRect.Min + new Vector2(nameSz0.X + S(10f), 0f);
        }
        var onChip = hovered && selPanelRect != null && !selPanelLocked
                  && mouse.X >= chipMin.X && mouse.Y >= chipMin.Y && mouse.X <= chipMax.X && mouse.Y <= chipMax.Y;
        if (onElGrip || onPanelGrip) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNwse);
        else if (onChip) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var beforeSel = _sel; var beforeEl = _selEl;
            if (onElGrip)
            {
                // Grabbing the corner of ANY selected element (a filling section body included) makes it a free, panel-relative box at its current spot and starts resizing it.
                var e = _sel!.Elements[_selEl];
                if (!e.Free && selPanelRect != null && selPanelRect.InnerSize.X > 1f && selPanelRect.InnerSize.Y > 1f)
                {
                    var im = selPanelRect.InnerMin; var isz = selPanelRect.InnerSize;
                    e.Free = true;
                    e.X = Math.Clamp((selElRect!.Min.X - im.X) / isz.X, 0f, 1f);
                    e.Y = Math.Clamp((selElRect.Min.Y - im.Y) / isz.Y, 0f, 1f);
                    e.W = Math.Clamp((selElRect.Max.X - selElRect.Min.X) / isz.X, 0.02f, 1f);
                    e.H = Math.Clamp((selElRect.Max.Y - selElRect.Min.Y) / isz.Y, 0.02f, 1f);
                    MarkDirty();
                }
                _resizingEl = true;
                _resizeStartMouse = mouse;
                _resizeStartSize = selElRect!.Max - selElRect.Min;
            }
            else if (onPanelGrip)
            {
                _resizing = true;
                _resizeStartMouse = mouse;
                _resizeStartSize = selPanelRect!.Max - selPanelRect.Min;
            }
            else if (onChip)
            {
                _selEl = -1;
                _dragging = true;
                _dragOffset = mouse - selPanelRect!.Min;
            }
            else if (hoverEl != null)
            {
                _selEl = hoverEl.ElementIndex;
                _draggingEl = true;
                _dragOffset = mouse - hoverEl.Min;
                _dragStart = mouse;
                _dragConverted = false;
            }
            else if (hoverPanel != null)
            {
                // First click on a panel selects it (and starts a drag); its elements become pickable from then on.
                _sel = byId[hoverPanel.PanelId]; _selEl = -1;
                _dragging = true;
                _dragOffset = mouse - hoverPanel.Min;
            }
            else { _sel = null; _selEl = -1; }
            if (_sel != beforeSel || _selEl != beforeEl) { _fxCandidate = null; _fxAddPick = 0; }
        }
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            // Dropped next to another element's edge: pin to it on that side.
            if (_draggingEl && _pinCand != null && _sel != null && _selEl >= 0 && _selEl < _sel.Elements.Count && _pinCand.Index < _sel.Elements.Count)
            {
                var e = _sel.Elements[_selEl];
                var t = _sel.Elements[_pinCand.Index];
                if (string.IsNullOrEmpty(t.Id) || _sel.Elements.Count(x => x.Id == t.Id) > 1) t.Id = ElementDef.NewId();
                e.Free = true;
                e.PinTo = t.Id; e.Pin = _pinCand.Side; e.PinGap = _pinCand.Gap; e.PinAlign = _pinCand.Align;
                if (e.Pin != PinSide.Below) e.FillRemaining = false;
                MarkDirty();
            }
            _pinCand = null;
            _dragging = false; _resizing = false; _draggingEl = false; _resizingEl = false;
        }

        // The click above may have changed the selection - re-derive the selected rects so the drag logic and outlines match it.
        selPanelRect = null; selElRect = null;
        if (_sel != null)
        {
            foreach (var r in ImmersiveHud.EditRects)
            {
                if (!byId.TryGetValue(r.PanelId, out var pdef) || pdef != _sel) continue;
                if (r.ElementIndex < 0) selPanelRect = r;
                else if (r.ElementIndex == _selEl) selElRect = r;
            }
        }

        if (_sel != null && selPanelRect != null)
        {
            var psize = selPanelRect.Max - selPanelRect.Min;
            // Children are laid out inside their parent's rect as drawn.
            var refMin = o; var refSize = _canvasSize;
            // Root panels are placed on the whole canvas (the scaled viewport); only a panel with an explicit Parent is placed relative to it.
            var parent = ImmersiveHud.ParentOf(doc, _sel);
            var parentRect = parent != null ? ImmersiveHud.EditRects.FirstOrDefault(r => r.PanelId == "doc_" + parent.Id && r.ElementIndex < 0) : null;
            if (parentRect != null) { refMin = parentRect.Min; refSize = parentRect.Max - parentRect.Min; }
            if (_dragging && !_sel.Locked) SetPanelRect(_sel, mouse - _dragOffset, psize, refMin, refSize);
            else if (_resizing && !_sel.Locked) SetPanelRect(_sel, selPanelRect.Min, _resizeStartSize + (mouse - _resizeStartMouse), refMin, refSize);
            else if ((_draggingEl || _resizingEl) && _selEl >= 0 && _selEl < _sel.Elements.Count && selElRect != null && !_sel.Elements[_selEl].Locked && !_sel.Locked)
            {
                var e = _sel.Elements[_selEl];
                var im = selPanelRect.InnerMin; var isz = selPanelRect.InnerSize;
                if (isz.X > 1f && isz.Y > 1f)
                {
                    // Dragging a pinned element unpins it, keeping where it was.
                    if (_draggingEl && e.HasPin && !_dragConverted && Vector2.Distance(mouse, _dragStart) > S(6f))
                    {
                        BakeRect(e, selElRect, im, isz);
                        e.Pin = PinSide.None; e.PinTo = ""; e.FillRemaining = false;
                        _dragConverted = true;
                    }
                    // Dragging a flow element far enough turns it into a free element at the spot it was, so it can be placed anywhere.
                    if (_draggingEl && !e.Free && !_dragConverted && Vector2.Distance(mouse, _dragStart) > S(6f))
                    {
                        e.Free = true;
                        e.X = Math.Clamp((selElRect.Min.X - im.X) / isz.X, 0f, 1f);
                        e.Y = Math.Clamp((selElRect.Min.Y - im.Y) / isz.Y, 0f, 1f);
                        e.W = Math.Clamp((selElRect.Max.X - selElRect.Min.X) / isz.X, 0.02f, 1f);
                        e.H = Math.Clamp((selElRect.Max.Y - selElRect.Min.Y) / isz.Y, 0.02f, 1f);
                        _dragConverted = true;
                    }
                    if (e.Free)
                    {
                        if (_draggingEl && (_dragConverted || Vector2.Distance(mouse, _dragStart) > S(2f)))
                        {
                            var nm = mouse - _dragOffset;
                            e.X = Math.Clamp((nm.X - im.X) / isz.X, 0f, MathF.Max(0f, 1f - e.W));
                            e.Y = Math.Clamp((nm.Y - im.Y) / isz.Y, 0f, MathF.Max(0f, 1f - e.H));
                        }
                        else if (_resizingEl)
                        {
                            var ns = _resizeStartSize + (mouse - _resizeStartMouse);
                            e.W = Math.Clamp(ns.X / isz.X, 0.02f, e.HasPin ? 1f : MathF.Max(0.02f, 1f - e.X));
                            e.H = Math.Clamp(ns.Y / isz.Y, 0.02f, e.HasPin ? 1f : MathF.Max(0.02f, 1f - e.Y));
                        }
                        e.X = MathF.Round(e.X / 0.005f) * 0.005f; e.Y = MathF.Round(e.Y / 0.005f) * 0.005f;
                        e.W = MathF.Round(e.W / 0.005f) * 0.005f; e.H = MathF.Round(e.H / 0.005f) * 0.005f;
                        MarkDirty();
                    }
                    // Drop-to-pin: while dragging, the nearest edge of another element (within a few px, overlapping along the other axis) becomes the candidate. Hold Shift to place freely.
                    _pinCand = null;
                    if (_draggingEl && e.Free && (_dragConverted || Vector2.Distance(mouse, _dragStart) > S(6f)) && !e.HasPin && !ImGui.GetIO().KeyShift)
                    {
                        var dMin = mouse - _dragOffset; var dMax = dMin + (selElRect.Max - selElRect.Min);
                        var best = S(14f);
                        foreach (var r in ImmersiveHud.EditRects)
                        {
                            if (r.PanelId != selPanelRect.PanelId || r.ElementIndex < 0 || r.ElementIndex == _selEl || r.ElementIndex >= _sel.Elements.Count) continue;
                            var t = _sel.Elements[r.ElementIndex];
                            if (PinLayout.DependsOn(_sel.Elements, t, e)) continue;
                            var hOver = dMin.X < r.Max.X && dMax.X > r.Min.X;
                            var vOver = dMin.Y < r.Max.Y && dMax.Y > r.Min.Y;
                            void Try(PinSide side, float d, bool overlap)
                            {
                                if (!overlap || MathF.Abs(d) >= best) return;
                                best = MathF.Abs(d);
                                var vertical = side == PinSide.Below || side == PinSide.Above;
                                float a0 = vertical ? dMin.X : dMin.Y, a1 = vertical ? dMax.X : dMax.Y;
                                float t0 = vertical ? r.Min.X : r.Min.Y, t1 = vertical ? r.Max.X : r.Max.Y;
                                var ds = MathF.Abs(a0 - t0); var de = MathF.Abs(a1 - t1); var dc = MathF.Abs((a0 + a1) - (t0 + t1)) * 0.5f;
                                var align = dc <= ds && dc <= de ? PinAlign.Center : ds <= de ? PinAlign.Start : PinAlign.End;
                                _pinCand = new PinCandidate { Index = r.ElementIndex, Side = side, Gap = MathF.Round(MathF.Max(0f, d) / MathF.Max(0.01f, S(1f))), Align = align, TMin = r.Min, TMax = r.Max };
                            }
                            Try(PinSide.Below, dMin.Y - r.Max.Y, hOver);
                            Try(PinSide.Above, r.Min.Y - dMax.Y, hOver);
                            Try(PinSide.RightOf, dMin.X - r.Max.X, vOver);
                            Try(PinSide.LeftOf, r.Min.X - dMax.X, vOver);
                        }
                    }
                }
            }
        }

        // selection / hover outlines over the live render Drawn in a transparent overlay child of the editor, created after the preview children so it paints above them - but, unlike the global foreground list, it stays underneath any window that is in front of the editor.
        var afterCanvas = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(o - new Vector2(S(2f)));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0f, 0f, 0f, 0f));
        ImGui.BeginChild("##ed_overlay", _canvasSize + new Vector2(S(4f)), false,
            ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings);
        var fg = ImGui.GetWindowDrawList();
        fg.PushClipRect(o - new Vector2(S(2f)), max + new Vector2(S(2f)), true);
        var accent = theme.Accent;
        var white = new Vector4(1f, 1f, 1f, 1f);
        if (hoverPanel != null && (selPanelRect == null || hoverPanel.PanelId != selPanelRect.PanelId))
            fg.AddRect(hoverPanel.Min, hoverPanel.Max, ImmersiveMode.Col(white, 0.35f), 0f, ImDrawFlags.None, 1f);
        if (selPanelRect != null && _sel != null)
        {
            fg.AddRect(selPanelRect.Min - new Vector2(1f), selPanelRect.Max + new Vector2(1f), ImmersiveMode.Col(white, 0.9f), 0f, ImDrawFlags.None, 2f);
            if (!_sel.Locked) fg.AddRectFilled(selPanelRect.Max - new Vector2(handle), selPanelRect.Max, ImmersiveMode.Col(white, 0.9f), 2f);
            var nameSz = ImGui.CalcTextSize((_sel.Locked ? "[locked] " : "") + (_sel.Name ?? ""));
            fg.AddRectFilled(selPanelRect.Min - new Vector2(0, nameSz.Y + S(4f)), selPanelRect.Min + new Vector2(nameSz.X + S(10f), 0f), ImmersiveMode.Col(white, 0.9f), 2f);
            fg.AddText(selPanelRect.Min - new Vector2(-S(5f), nameSz.Y + S(2f)), ImGui.ColorConvertFloat4ToU32(new Vector4(0.05f, 0.05f, 0.08f, 1f)), (_sel.Locked ? "[locked] " : "") + _sel.Name);
        }
        DrawPinOverlay(fg, selPanelRect, accent);
        if (hoverEl != null && (selElRect == null || hoverEl != selElRect))
            fg.AddRect(hoverEl.Min, hoverEl.Max, ImmersiveMode.Col(accent, 0.7f), 0f, ImDrawFlags.None, 1f);
        if (selElRect != null && _sel != null && _selEl >= 0 && _selEl < _sel.Elements.Count)
        {
            var e = _sel.Elements[_selEl];
            fg.AddRectFilled(selElRect.Min, selElRect.Max, ImmersiveMode.Col(accent, 0.08f));
            fg.AddRect(selElRect.Min, selElRect.Max, ImmersiveMode.Col(accent, 1f), 0f, ImDrawFlags.None, 2f);
            if (!e.Locked && !_sel.Locked) fg.AddRectFilled(selElRect.Max - new Vector2(handle), selElRect.Max, ImmersiveMode.Col(accent, 1f), 2f);
            var lbl = (e.Locked ? "[locked] " : "") + ElementLabel(e) + (e.HasPin ? "  (pinned)" : "") + (e.Free || e.Locked || e.HasPin ? "" : "  (in flow — drag or resize to place freely)");
            var lsz = ImGui.CalcTextSize(lbl);
            var lp = new Vector2(selElRect.Min.X, selElRect.Max.Y + S(2f));
            if (lp.Y + lsz.Y > max.Y) lp.Y = selElRect.Min.Y - lsz.Y - S(2f);
            fg.AddRectFilled(lp, lp + lsz + new Vector2(S(8f), S(2f)), ImmersiveMode.Col(accent, 0.95f), 2f);
            fg.AddText(lp + new Vector2(S(4f), 1f), ImGui.ColorConvertFloat4ToU32(new Vector4(0.05f, 0.05f, 0.08f, 1f)), lbl);
        }
        fg.PopClipRect();
        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.SetCursorScreenPos(afterCanvas);
    }

    private Vector2 _dragStart;
    private bool _dragConverted;

    // Inspector: pin this element relative to another one in the panel.
    private void DrawPinSection(PanelDef sel, ElementDef e)
    {
        var S = (Func<float, float>)RsTheme.S;
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        ImGui.TextUnformatted("Pin ordering");
        ImGui.PopStyleColor();
        var others = sel.Elements.Where(x => x != e).ToList();
        var names = new List<string> { "(none: free placement)" };
        foreach (var x in others)
        {
            var dup = PinLayout.DependsOn(sel.Elements, x, e) ? "  (pinned to this)" : "";
            var lbl = ElementLabel(x);
            var nth = others.TakeWhile(y => y != x).Count(y => ElementLabel(y) == lbl);
            names.Add(lbl + (nth > 0 ? $" #{nth + 1}" : "") + dup);
        }
        var cur = e.HasPin ? others.FindIndex(x => x.Id == e.PinTo) + 1 : 0;
        if (cur < 0) cur = 0;
        PanelRects(sel, out var pr, out var er);
        if (RsElements.Dropdown("ed_pinto", ref cur, names, S(230f)))
        {
            if (cur == 0) { e.Pin = PinSide.None; e.PinTo = ""; e.FillRemaining = false; }
            else
            {
                var t = others[cur - 1];
                if (PinLayout.DependsOn(sel.Elements, t, e)) { /* would form a cycle: refused */ }
                else
                {
                    // Keep a sane fallback spot, then pin.
                    if (!e.Free && pr != null && er != null && pr.InnerSize.X > 1f && pr.InnerSize.Y > 1f) BakeRect(e, er, pr.InnerMin, pr.InnerSize);
                    e.Free = true;
                    if (string.IsNullOrEmpty(t.Id) || sel.Elements.Count(x => x.Id == t.Id) > 1) t.Id = ElementDef.NewId();
                    e.PinTo = t.Id;
                    if (e.Pin == PinSide.None) e.Pin = PinSide.Below;
                }
            }
            MarkDirty();
        }
        ImGui.SameLine(); ImGui.TextUnformatted("Pin to");
        if (!string.IsNullOrEmpty(e.PinTo) && e.Pin != PinSide.None && !sel.Elements.Any(x => x != e && x.Id == e.PinTo))
            Hint("The pinned-to element is gone; this element uses its free position.");
        if (!e.HasPin) return;

        var side = Math.Max(0, (int)e.Pin - 1);
        if (RsElements.Dropdown("ed_pinside", ref side, new[] { "Below", "Above", "Left of", "Right of" }, S(150f)))
        {
            e.Pin = (PinSide)(side + 1);
            if (e.Pin != PinSide.Below) e.FillRemaining = false;
            MarkDirty();
        }
        ImGui.SameLine(); ImGui.TextUnformatted("Side");
        var al = (int)e.PinAlign;
        var alNames = e.Pin == PinSide.Below || e.Pin == PinSide.Above ? new[] { "Left", "Centre", "Right" } : new[] { "Top", "Middle", "Bottom" };
        if (RsElements.Dropdown("ed_pinalign", ref al, alNames, S(150f))) { e.PinAlign = (PinAlign)al; MarkDirty(); }
        ImGui.SameLine(); ImGui.TextUnformatted("Align");
        var gap = e.PinGap;
        ImGui.SetNextItemWidth(W());
        if (ImGui.SliderFloat("Gap##pingap", ref gap, 0f, 64f, "%.0f px")) { e.PinGap = gap; MarkDirty(); }
        if (e.Pin == PinSide.Below)
        {
            var fill = e.FillRemaining;
            if (RsElements.Toggle("ed_pinfill", ref fill, "Fill the rest of the panel's height")) { e.FillRemaining = fill; MarkDirty(); }
        }
        if (RsElements.Button("Unpin (keep position)", RsElements.ButtonVariant.Secondary))
        {
            if (pr != null && er != null && pr.InnerSize.X > 1f && pr.InnerSize.Y > 1f) BakeRect(e, er, pr.InnerMin, pr.InnerSize);
            e.Pin = PinSide.None; e.PinTo = ""; e.FillRemaining = false;
            MarkDirty();
        }
        Hint("Its position follows the other element's rendered size. Dragging it in the scene unpins it; drop it next to another element's edge to pin it there (hold Shift to just place it).");
    }

    private void PanelRects(PanelDef p, out ImmersiveHud.EditRect? panel, out ImmersiveHud.EditRect? el)
    {
        panel = ImmersiveHud.EditRects.LastOrDefault(r => r.PanelId == "doc_" + p.Id && r.ElementIndex < 0);
        el = ImmersiveHud.EditRects.LastOrDefault(r => r.PanelId == "doc_" + p.Id && r.ElementIndex == _selEl);
    }

    // Drop-to-pin candidate while dragging an element.
    private sealed class PinCandidate
    {
        public int Index; public PinSide Side; public float Gap; public PinAlign Align;
        public Vector2 TMin, TMax;
    }
    private PinCandidate? _pinCand;

    // Writes an element's rendered rect back into its free X/Y/W/H.
    private static void BakeRect(ElementDef e, ImmersiveHud.EditRect r, Vector2 im, Vector2 isz)
    {
        e.Free = true;
        e.X = Math.Clamp((r.Min.X - im.X) / isz.X, 0f, 1f);
        e.Y = Math.Clamp((r.Min.Y - im.Y) / isz.Y, 0f, 1f);
        e.W = Math.Clamp((r.Max.X - r.Min.X) / isz.X, 0.02f, 1f);
        e.H = Math.Clamp((r.Max.Y - r.Min.Y) / isz.Y, 0.02f, 1f);
    }

    private static string PinSideLabel(PinSide s) => s switch
    {
        PinSide.Below => "below", PinSide.Above => "above", PinSide.LeftOf => "left of", PinSide.RightOf => "right of", _ => "",
    };

    // Link lines from each pinned element of the selected panel to its target, plus the drop-to-pin highlight.
    private void DrawPinOverlay(ImDrawListPtr fg, ImmersiveHud.EditRect? selPanelRect, Vector4 accent)
    {
        var S = (Func<float, float>)RsTheme.S;
        if (_sel == null || selPanelRect == null) return;
        var els = _sel.Elements;
        var rects = new Dictionary<int, ImmersiveHud.EditRect>();
        foreach (var r in ImmersiveHud.EditRects)
            if (r.PanelId == selPanelRect.PanelId && r.ElementIndex >= 0) rects[r.ElementIndex] = r;
        PinLayout.Order(els, out var pinned);
        var col = ImmersiveMode.Col(accent, 0.75f);
        for (int i = 0; i < els.Count; i++)
        {
            if (!pinned[i] || !rects.TryGetValue(i, out var own)) continue;
            var ti = els.FindIndex(x => x.Id == els[i].PinTo);
            if (ti < 0 || !rects.TryGetValue(ti, out var tr)) continue;
            Vector2 a, b;
            switch (els[i].Pin)
            {
                case PinSide.Above:   a = new((tr.Min.X + tr.Max.X) * 0.5f, tr.Min.Y); b = new((own.Min.X + own.Max.X) * 0.5f, own.Max.Y); break;
                case PinSide.LeftOf:  a = new(tr.Min.X, (tr.Min.Y + tr.Max.Y) * 0.5f); b = new(own.Max.X, (own.Min.Y + own.Max.Y) * 0.5f); break;
                case PinSide.RightOf: a = new(tr.Max.X, (tr.Min.Y + tr.Max.Y) * 0.5f); b = new(own.Min.X, (own.Min.Y + own.Max.Y) * 0.5f); break;
                default:              a = new((tr.Min.X + tr.Max.X) * 0.5f, tr.Max.Y); b = new((own.Min.X + own.Max.X) * 0.5f, own.Min.Y); break;
            }
            fg.AddLine(a, b, col, 1.5f);
            fg.AddCircleFilled(a, S(2.5f), col, 10);
            // Link glyph: two interlocking rings at the pinned end.
            var g = S(3.5f);
            fg.AddCircle(b + new Vector2(-g * 0.6f, 0f), g, col, 12, 1.5f);
            fg.AddCircle(b + new Vector2(g * 0.6f, 0f), g, col, 12, 1.5f);
        }
        if (_pinCand != null && _draggingEl)
        {
            var c = _pinCand;
            Vector2 a, b;
            switch (c.Side)
            {
                case PinSide.Above:   a = c.TMin; b = new(c.TMax.X, c.TMin.Y); break;
                case PinSide.LeftOf:  a = c.TMin; b = new(c.TMin.X, c.TMax.Y); break;
                case PinSide.RightOf: a = new(c.TMax.X, c.TMin.Y); b = c.TMax; break;
                default:              a = new(c.TMin.X, c.TMax.Y); b = c.TMax; break;
            }
            fg.AddRect(c.TMin, c.TMax, ImmersiveMode.Col(accent, 0.5f), 0f, ImDrawFlags.None, 1f);
            fg.AddLine(a, b, ImmersiveMode.Col(new Vector4(1f, 0.85f, 0.3f, 1f), 1f), S(3f));
            var name = c.Index < els.Count ? ElementLabel(els[c.Index]) : "";
            var lbl = $"Pin {PinSideLabel(c.Side)} {name} ({c.Align})  —  Shift: don't pin";
            var sz = ImGui.CalcTextSize(lbl);
            var lp = (a + b) * 0.5f - new Vector2(sz.X * 0.5f, sz.Y + S(6f));
            fg.AddRectFilled(lp - new Vector2(S(4f), 1f), lp + sz + new Vector2(S(4f), 1f), ImmersiveMode.Col(new Vector4(0.05f, 0.05f, 0.08f, 1f), 0.9f), 2f);
            fg.AddText(lp, ImmersiveMode.Col(new Vector4(1f, 0.85f, 0.3f, 1f), 1f), lbl);
        }
    }
    private Vector2 _resizeStartMouse, _resizeStartSize;

    private bool _draggingEl, _resizingEl;

    private static string ElementLabel(ElementDef e) => e.Type switch
    {
        ElementType.SectionNav => $"Section nav ({e.Nav})",
        ElementType.SectionBody => "Section body" + (e.Fill ? " (fill)" : ""),
        ElementType.Controls => e.Inline ? "Controls (row)" : "Controls",
        ElementType.ControlsRow => "Profile icons (notes / link / like)",
        ElementType.Text => "Text: " + (string.IsNullOrEmpty(e.Text) ? "(empty)" : e.Text.Length > 18 ? e.Text[..18] + "…" : e.Text),
        ElementType.Spacer => $"Spacer {e.Size:0}px",
        ElementType.Image => "Image" + (e.Image != null && e.Image.IsSet ? "" : " (none picked)"),
        _ => e.Type.ToString(),
    };

    private void DrawInspector()
    {
        var S = (Func<float, float>)RsTheme.S;
        var doc = _doc!;

        // Panels list.
        if (RsElements.BeginPanel("ed_panels", "Panels", fitContentsX: false, fitContentsY: true))
        {
            for (int i = 0; i < doc.Panels.Count; i++)
            {
                var p = doc.Panels[i];
                var child = ImmersiveHud.ParentOf(doc, p) != null;
                var label = $"{(child ? "    > " : "")}{(p.Locked ? "[L] " : "")}{p.Name}{(p.IsControls ? "  (controls)" : "")}";
                if (ImGui.Selectable($"{label}##pl{p.Id}", p == _sel)) { _sel = p; _selEl = -1; }
            }
            if (RsElements.Button("+ Add panel", RsElements.ButtonVariant.Secondary))
            {
                var np = new PanelDef { Name = "Panel " + (doc.Panels.Count + 1), Header = "Panel", Anchor = PanelAnchor.TopRight, X = 0.05f, Y = 0.15f, W = 0.22f, H = 0.30f };
                np.Elements.Add(new ElementDef { Type = ElementType.Text, Text = "New panel", Size = 1f });
                doc.Panels.Add(np); _sel = np; _selEl = -1; MarkDirty();
            }
            if (!doc.HasControls && _scene == 0)
            {
                ImGui.SameLine();
                if (RsElements.Button("+ Add controls panel", RsElements.ButtonVariant.Secondary)) { _sel = doc.AddControlsPanel(); _selEl = -1; MarkDirty(); }
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentWarning);
                ImGui.TextWrapped("No Controls panel: viewers can't open notes, like, report or close this profile, and the theme will not be accepted into the gallery.");
                ImGui.PopStyleColor();
            }
            if (_sel != null)
            {
                ImGui.SameLine();
                if (!_sel.IsControls && RsElements.Button("Duplicate", RsElements.ButtonVariant.Ghost)) { var c = _sel.Clone(); c.Name += " copy"; c.X += 0.02f; c.Y += 0.02f; doc.Panels.Add(c); _sel = c; MarkDirty(); }
                ImGui.SameLine();
                var holdsOnlyBody = _sel.Elements.Any(x => x.Type == ElementType.SectionBody)
                                 && doc.SectionBodyCount == _sel.Elements.Count(x => x.Type == ElementType.SectionBody);
                if (holdsOnlyBody)
                {
                    ImGui.BeginDisabled();
                    RsElements.Button("Delete", RsElements.ButtonVariant.Danger);
                    ImGui.EndDisabled();
                    Hint("This panel holds the only section body. Add a section body to another panel before deleting this one.");
                }
                else if (RsElements.Button("Delete", RsElements.ButtonVariant.Danger))
                {
                    foreach (var c in doc.Panels.Where(q => q.Parent == _sel.Id)) c.Parent = _sel.Parent;
                    if (_sel.IsControls) doc.ControlsRemoved = true;
                    doc.Panels.Remove(_sel); _sel = doc.Panels.FirstOrDefault(); _selEl = -1; MarkDirty();
                }
            }
        }
        RsElements.EndPanel();
        ImGui.Spacing();

        if (_sel == null) return;
        var sel = _sel;

        if (RsElements.BeginPanel("ed_panel_props", sel.IsControls ? "Controls panel" : "Panel", fitContentsX: false, fitContentsY: true))
        {
            if (sel.IsControls)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentWarning);
                ImGui.TextWrapped("Notes, Equipment, Like, Report and Close. Place and style it however you like, but keep it visible: themes that hide these controls are not accepted into the gallery.");
                ImGui.PopStyleColor();
                var cl = sel.CloseLabel;
                if (RsElements.InputText("ed_ctl_close", ref cl, 32, "Close button label (blank = theme's)", W(0.95f))) { sel.CloseLabel = cl; MarkDirty(); }
            }
            var n = sel.Name;
            if (RsElements.InputText("ed_pname", ref n, 40, "Name", W(0.95f))) { sel.Name = n; MarkDirty(); }
            var h = sel.Header;
            if (RsElements.InputText("ed_pheader", ref h, 40, "Header (blank = none, $section = tab name)", W(0.95f))) { sel.Header = h; MarkDirty(); }
            var plock = sel.Locked;
            if (RsElements.Toggle("ed_plock", ref plock, "Lock panel (can't be picked or moved in the scene)")) { sel.Locked = plock; MarkDirty(); }
            var chrome = sel.Chrome;
            if (RsElements.Toggle("ed_pchrome", ref chrome, "Draw panel chrome")) { sel.Chrome = chrome; MarkDirty(); }
            var solidBg = sel.Solid;
            if (RsElements.Toggle("ed_psolid", ref solidBg, "Solid background")) { sel.Solid = solidBg; MarkDirty(); }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("An opaque plate in the theme's surface colour under the chrome." + (char)10 + "A profile background image or video still shows through when one is set.");
            var bob = sel.Bob;
            if (RsElements.Toggle("ed_pbob", ref bob, "Idle float")) { sel.Bob = bob; MarkDirty(); }
            var anchor = (int)sel.Anchor;
            ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted("Anchor"); ImGui.SameLine();
            if (RsElements.Dropdown("ed_panchor", ref anchor, Enum.GetNames(typeof(PanelAnchor)), S(150f))) { sel.Anchor = (PanelAnchor)anchor; MarkDirty(); }
            // Parent: the panel this one is laid out inside of (and rides along with). Its own descendants are excluded to avoid loops.
            {
                var descendants = new HashSet<string>();
                void Collect(string id) { foreach (var c in doc.Panels.Where(q => q.Parent == id).ToList()) if (descendants.Add(c.Id)) Collect(c.Id); }
                Collect(sel.Id);
                var candidates = doc.Panels.Where(q => q != sel && !q.IsControls && !descendants.Contains(q.Id)).ToList();
                var names = new List<string> { "(none: the screen)" };
                names.AddRange(candidates.Select(q => q.Name));
                var cur = 0;
                for (int i = 0; i < candidates.Count; i++) if (candidates[i].Id == sel.Parent) cur = i + 1;
                ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted("Parent"); ImGui.SameLine();
                if (RsElements.Dropdown("ed_pparent", ref cur, names, S(190f)))
                {
                    // Keep the panel where it is on screen when re-parenting.
                    var rect = ImmersiveHud.EditRects.FirstOrDefault(r => r.PanelId == "doc_" + sel.Id && r.ElementIndex < 0);
                    sel.Parent = cur == 0 ? "" : candidates[cur - 1].Id;
                    sel.Attach = PanelAttach.Inside;
                    if (rect != null)
                    {
                        var refMin = _canvasOrigin; var refSize = _canvasSize;
                        if (cur > 0)
                        {
                            var prect = ImmersiveHud.EditRects.FirstOrDefault(r => r.PanelId == "doc_" + candidates[cur - 1].Id && r.ElementIndex < 0);
                            if (prect != null) { refMin = prect.Min; refSize = prect.Max - prect.Min; }
                        }
                        SetPanelRect(sel, rect.Min, rect.Max - rect.Min, refMin, refSize);
                    }
                    MarkDirty();
                }
                if (!string.IsNullOrEmpty(sel.Parent))
                {
                    var at = (int)sel.Attach;
                    ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted("Attach"); ImGui.SameLine();
                    if (RsElements.Dropdown("ed_pattach", ref at, new[] { "Inside the parent", "Above (top edge)", "Below (bottom edge)", "Left of it", "Right of it" }, S(190f)))
                    {
                        var was = sel.Attach;
                        sel.Attach = (PanelAttach)at;
                        // Sensible defaults when switching to an edge: full length along it, flush with its start.
                        if (was == PanelAttach.Inside && sel.Attach != PanelAttach.Inside)
                        {
                            if (sel.Attach == PanelAttach.Above || sel.Attach == PanelAttach.Below) { sel.X = 0f; sel.W = 1f; }
                            else { sel.Y = 0f; sel.H = 1f; }
                        }
                        MarkDirty();
                    }
                    Hint("Inside: laid out within the parent's box.\nEdges: glued to the OUTSIDE of that edge and slides along it. Drag in the scene to slide, drag the corner to resize.");
                    if (sel.Attach != PanelAttach.Inside)
                    {
                        ImGui.SetNextItemWidth(S(190f));
                        var gp = sel.Gap; if (ImGui.SliderFloat("Gap##ed", ref gp, -20f, 60f, "%.0f px")) { sel.Gap = gp; MarkDirty(); }
                        Hint("Space between the parent's edge and this panel. Negative overlaps.");
                    }
                    var behind = sel.BehindParent;
                    if (RsElements.Toggle("ed_pbehind", ref behind, "Draw behind the parent")) { sel.BehindParent = behind; MarkDirty(); }
                    Hint("Off: this panel sits on top of its parent. On: it tucks underneath, so overlapping parts are covered by the parent.");
                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                    ImGui.TextWrapped("Moves with its parent.");
                    ImGui.PopStyleColor();
                }
            }
            // Layer: stacking order among panels sharing the same parent.
            {
                ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted($"Layer {sel.Layer}"); ImGui.SameLine();
                if (RsElements.Button("Bring forward", RsElements.ButtonVariant.Ghost)) { sel.Layer++; MarkDirty(); }
                ImGui.SameLine();
                if (RsElements.Button("Send back", RsElements.ButtonVariant.Ghost)) { sel.Layer--; MarkDirty(); }
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextWrapped("Higher layers draw in front of lower ones among panels sharing a parent.");
                ImGui.PopStyleColor();
            }
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextWrapped("Move and resize the panel by dragging it in the scene.");
            ImGui.PopStyleColor();
            ImGui.SetNextItemWidth(W());
            var d = sel.Depth; if (ImGui.SliderFloat("Parallax depth##ed", ref d, 0f, 1f, "%.2f")) { sel.Depth = d; MarkDirty(); }
            ImGui.SetNextItemWidth(W());
            var dl = sel.Delay; if (ImGui.SliderFloat("Entrance delay##ed", ref dl, 0f, 1f, "%.2fs")) { sel.Delay = dl; MarkDirty(); }
            ImGui.Separator();
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted("Pictures");
            ImGui.PopStyleColor();
            var pbg = sel.Background; if (DrawImageRef("p_bg_" + sel.Id, "Background", ref pbg, ImageRefKind.Background)) { sel.Background = pbg; MarkDirty(); }
            if (sel.Background != null && sel.Background.IsSet)
            {
                var pmk = sel.MaskImage; if (DrawImageRef("p_mk_" + sel.Id, "Background mask", ref pmk, ImageRefKind.Mask)) { sel.MaskImage = pmk; MarkDirty(); }
            }
            var pfr = sel.Frame; if (DrawImageRef("p_fr_" + sel.Id, "Frame", ref pfr, ImageRefKind.Frame)) { sel.Frame = pfr; MarkDirty(); }
        }
        RsElements.EndPanel();
        ImGui.Spacing();

        if (sel.IsControls) return;   // its content is fixed
        if (RsElements.BeginPanel("ed_elements", "Elements (top to bottom)", fitContentsX: false, fitContentsY: true))
        {
            for (int i = 0; i < sel.Elements.Count; i++)
            {
                var e = sel.Elements[i];
                if (ImGui.Selectable($"{(e.Locked ? "[L] " : "")}{ElementLabel(e)}##el{i}", i == _selEl, ImGuiSelectableFlags.None, new Vector2(S(190f), 0))) _selEl = i;
                ImGui.SameLine();
                if (RsElements.IconButton(FontAwesomeIcon.ArrowUp, "elup" + i, RsElements.ButtonVariant.Ghost, S(22f)) && i > 0) { (sel.Elements[i - 1], sel.Elements[i]) = (sel.Elements[i], sel.Elements[i - 1]); if (_selEl == i) _selEl = i - 1; MarkDirty(); }
                ImGui.SameLine();
                if (RsElements.IconButton(FontAwesomeIcon.ArrowDown, "eldn" + i, RsElements.ButtonVariant.Ghost, S(22f)) && i < sel.Elements.Count - 1) { (sel.Elements[i + 1], sel.Elements[i]) = (sel.Elements[i], sel.Elements[i + 1]); if (_selEl == i) _selEl = i + 1; MarkDirty(); }
                ImGui.SameLine();
                var lastBody = e.Type == ElementType.SectionBody && doc.SectionBodyCount <= 1;
                if (lastBody)
                {
                    ImGui.BeginDisabled();
                    RsElements.IconButton(FontAwesomeIcon.Times, "eldel" + i, RsElements.ButtonVariant.Ghost, S(22f));
                    ImGui.EndDisabled();
                    Hint("Every theme needs a section body: it shows the profile, and the Controls panel hangs off it. Add one to another panel first to move it.");
                }
                else if (RsElements.IconButton(FontAwesomeIcon.Times, "eldel" + i, RsElements.ButtonVariant.Ghost, S(22f))) { sel.Elements.RemoveAt(i); if (_selEl >= sel.Elements.Count) _selEl = sel.Elements.Count - 1; MarkDirty(); break; }
            }
            ImGui.Spacing();
            var addTypes = _scene switch
            {
                1 => AddableTypes.Where(x => x is ElementType.Avatar or ElementType.Title or ElementType.LoadingProgress or ElementType.Text or ElementType.Divider or ElementType.Spacer or ElementType.Image or ElementType.LinkReadout).ToList(),
                2 => AddableTypes.Where(x => x is ElementType.Avatar or ElementType.Title or ElementType.TooltipInfo or ElementType.Text or ElementType.Divider or ElementType.Spacer or ElementType.Image).ToList(),
                _ => AddableTypes.Where(x => x != ElementType.LoadingProgress && x != ElementType.TooltipInfo && x != ElementType.LinkReadout).ToList(),
            };
            if (_addElType >= addTypes.Count) _addElType = 0;
            RsElements.Dropdown("ed_addel", ref _addElType, addTypes.Select(t => t.ToString()).ToList(), S(170f));
            ImGui.SameLine();
            if (RsElements.Button("Add", RsElements.ButtonVariant.Secondary))
            {
                var e = new ElementDef { Type = addTypes[_addElType] };
                if (e.Type == ElementType.SectionBody) e.Fill = true;
                if (e.Type == ElementType.Text) e.Text = "Text";
                if (e.Type == ElementType.Spacer) e.Size = 12f;
                if (e.Type == ElementType.Avatar) e.Size = 120f;      // px diameter
                if (e.Type == ElementType.ControlsRow) { e.Size = 28f; e.Free = true; e.X = 0.1f; e.Y = 0.2f; e.W = 0.8f; e.H = 0.05f; }
                if (e.Type == ElementType.Image) { e.Height = 140f; e.W = 0.5f; e.H = 0.3f; }
                sel.Elements.Add(e); _selEl = sel.Elements.Count - 1; MarkDirty();
            }

            if (_selEl >= 0 && _selEl < sel.Elements.Count)
            {
                ImGui.Separator();
                var e = sel.Elements[_selEl];
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextUnformatted(e.Type.ToString());
                ImGui.PopStyleColor();

                // Placement + scale.
                var lockE = e.Locked;
                if (RsElements.Toggle("ed_ellock", ref lockE, "Lock element (can't be picked or moved in the scene)")) { e.Locked = lockE; MarkDirty(); }
                var free = e.Free;
                if (RsElements.Toggle("ed_elfree", ref free, e.Free ? "Placed freely (drag / resize it in the scene)" : "In the panel's flow (drag it to place freely)"))
                {
                    e.Free = free;
                    if (free && e.W <= 0.02f) { e.W = 0.5f; e.H = 0.15f; }
                    // Portraits and pictures need room to be seen.
                    if (free && (e.Type == ElementType.Avatar || e.Type == ElementType.Image) && e.H < 0.25f) { e.H = 0.3f; e.W = MathF.Max(e.W, 0.4f); }
                    MarkDirty();
                }
                if (e.Free)
                {
                    // Exact placement (fractions of the panel's inner area).
                    var xy = new Vector2(e.X, e.Y);
                    ImGui.SetNextItemWidth(W());
                    if (ImGui.SliderFloat2("Position##elxy", ref xy, 0f, 1f, "%.3f")) { e.X = xy.X; e.Y = xy.Y; MarkDirty(); }
                    var wh = new Vector2(e.W, e.H);
                    ImGui.SetNextItemWidth(W());
                    if (ImGui.SliderFloat2("Size##elwh", ref wh, 0.02f, 1f, "%.3f")) { e.W = wh.X; e.H = wh.Y; MarkDirty(); }
                }
                ImGui.SetNextItemWidth(W());
                var sc = e.Scale <= 0f ? 1f : e.Scale;
                if (ImGui.SliderFloat("Scale##elsc", ref sc, 0.3f, 3f, "%.2f")) { e.Scale = sc; MarkDirty(); }
                Hint("Scales the element's text, portrait or icons.");
                DrawPinSection(sel, e);

                // Per-element style.
                var hasStyle = e.Style != null;
                if (RsElements.Toggle("ed_elstyle", ref hasStyle, "Custom style for this element"))
                {
                    e.Style = hasStyle ? new ElementStyle { Accent = _doc!.Palette.Accent.Clone(), Text = _doc.Palette.Text.Clone() } : null;
                    MarkDirty();
                }
                // A different style just for the dossier page picked at the top.
                ElementStyle? pageSt = null;
                var page = _scene == 0 ? CurrentPage() : null;
                if (page != null)
                {
                    e.PageStyles ??= new Dictionary<string, ElementStyle>();
                    var hasPage = e.PageStyles.ContainsKey(page);
                    if (RsElements.Toggle("ed_elpagestyle", ref hasPage, $"Different style only on page \"{page}\""))
                    {
                        if (hasPage) e.PageStyles[page] = e.Style?.Clone() ?? new ElementStyle { Accent = _doc!.Palette.Accent.Clone(), Text = _doc.Palette.Text.Clone() };
                        else e.PageStyles.Remove(page);
                        MarkDirty();
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Starts from the element's style. Pick another page at the top to give it its own look too.");
                    if (hasPage) pageSt = e.PageStyles[page];
                }
                if (pageSt != null)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentWarning);
                    ImGui.TextUnformatted($"Editing the style for page \"{page}\"");
                    ImGui.PopStyleColor();
                }
                var stEdit = pageSt ?? e.Style;
                if (stEdit != null)
                {
                    var st = stEdit;
                    var oa = st.OverrideAccent;
                    if (RsElements.Toggle("ed_st_oa", ref oa, "Accent colour")) { st.OverrideAccent = oa; MarkDirty(); }
                    if (st.OverrideAccent)
                    {
                        var v = st.Accent.V;
                        ImGui.SetNextItemWidth(W());
                        if (ImGui.ColorEdit4("##st_accent", ref v, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar)) { st.Accent = new ThemeColor(v); MarkDirty(); }
                    }
                    var ot = st.OverrideText;
                    if (RsElements.Toggle("ed_st_ot", ref ot, "Text colour")) { st.OverrideText = ot; MarkDirty(); }
                    if (st.OverrideText)
                    {
                        var v = st.Text.V;
                        ImGui.SetNextItemWidth(W());
                        if (ImGui.ColorEdit4("##st_text", ref v, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar)) { st.Text = new ThemeColor(v); MarkDirty(); }
                    }
                    ImGui.SetNextItemWidth(W());
                    var al = st.Alpha; if (ImGui.SliderFloat("Opacity##st", ref al, 0.05f, 1f, "%.2f")) { st.Alpha = al; MarkDirty(); }
                    var gl = st.Glow; if (RsElements.Toggle("ed_st_glow", ref gl, "Glow behind element")) { st.Glow = gl; MarkDirty(); }
                    var ch = st.Chrome; if (RsElements.Toggle("ed_st_chrome", ref ch, "Material plate behind element")) { st.Chrome = ch; MarkDirty(); }
                    if (st.Chrome)
                    {
                        var plateNames = new List<string> { "Theme material" };
                        plateNames.AddRange(Enum.GetNames(typeof(ThemeMaterial)));
                        var pi = st.PlateMaterial.HasValue ? (int)st.PlateMaterial.Value + 1 : 0;
                        ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted("Plate"); ImGui.SameLine();
                        if (RsElements.Dropdown("ed_st_plate", ref pi, plateNames, S(160f))) { st.PlateMaterial = pi == 0 ? null : (ThemeMaterial)(pi - 1); MarkDirty(); }
                    }
                    ImGui.SetNextItemWidth(W());
                    var pd = st.Padding; if (ImGui.SliderFloat("Decoration padding##st", ref pd, 0f, 30f, "%.0f px")) { st.Padding = pd; MarkDirty(); }
                    var mi = (int)st.Mask;
                    ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted("Edge mask"); ImGui.SameLine();
                    if (RsElements.Dropdown("ed_st_mask", ref mi, Enum.GetNames(typeof(EdgeMask)), S(150f))) { st.Mask = (EdgeMask)mi; MarkDirty(); }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Torn = ragged parchment cut · Scorched = burnt rim · Brackets = Allagan corners · Jagged = void edge");
                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                    ImGui.TextUnformatted("Effects");
                    ImGui.PopStyleColor();
                    var fx = st.ScanLines; if (RsElements.Toggle("ed_st_scan", ref fx, "Scan lines")) { st.ScanLines = fx; MarkDirty(); }
                    fx = st.Sweep;     if (RsElements.Toggle("ed_st_sweep", ref fx, "Scan sweep")) { st.Sweep = fx; MarkDirty(); }
                    fx = st.Shimmer;   if (RsElements.Toggle("ed_st_shim", ref fx, "Rim shimmer")) { st.Shimmer = fx; MarkDirty(); }
                    fx = st.EdgeMotes; if (RsElements.Toggle("ed_st_motes", ref fx, "Edge motes")) { st.EdgeMotes = fx; MarkDirty(); }
                    fx = st.Embers;    if (RsElements.Toggle("ed_st_embers", ref fx, "Rising embers")) { st.Embers = fx; MarkDirty(); }
                    DrawElementFxList(st);
                    ImGui.Spacing();
                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                    ImGui.TextUnformatted("Pictures");
                    ImGui.PopStyleColor();
                    var ebg = st.Background; if (DrawImageRef("e_bg_" + _selEl, "Background", ref ebg, ImageRefKind.Background)) { st.Background = ebg; MarkDirty(); }
                    var efr = st.Frame; if (DrawImageRef("e_fr_" + _selEl, "Frame", ref efr, ImageRefKind.Frame)) { st.Frame = efr; MarkDirty(); }
                    if (e.Type == ElementType.Avatar || e.Type == ElementType.Image)
                    {
                        var emk = st.MaskImage; if (DrawImageRef("e_mk_" + _selEl, "Mask", ref emk, ImageRefKind.Mask)) { st.MaskImage = emk; MarkDirty(); }
                        if (e.Type == ElementType.Avatar) Hint("Cuts the portrait to this shape. Overrides the theme-wide avatar mask from the Images tab.");
                    }
                }
                ImGui.Separator();
                switch (e.Type)
                {
                    case ElementType.Avatar:
                        {
                            ImGui.SetNextItemWidth(W());
                            var sz = e.Size; if (ImGui.SliderFloat("Diameter (0 = auto)##el", ref sz, 0f, 300f, "%.0f px")) { e.Size = sz; MarkDirty(); }
                            var ring = e.ThemeRing;
                            if (RsElements.Toggle("ed_av_ring", ref ring, "Theme's own ring around the portrait")) { e.ThemeRing = ring; MarkDirty(); }
                            Hint("On: the material's built-in treatment (Allagan spinning rings, Nymian bezel, Aether shimmer...).\nOff: a bare portrait — add your own from Custom style > Effects, e.g. the \"Orbit rings\" preset.");
                        }
                        break;
                    case ElementType.Title:
                        {
                            ImGui.SetNextItemWidth(W());
                            var sz = e.Size <= 0 ? 1.45f : e.Size; if (ImGui.SliderFloat("Title scale##el", ref sz, 0.8f, 2.5f, "%.2f")) { e.Size = sz; MarkDirty(); }
                        }
                        break;
                    case ElementType.SectionNav:
                        {
                            var nav = (int)e.Nav;
                            ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted("Style"); ImGui.SameLine();
                            if (RsElements.Dropdown("ed_nav", ref nav, Enum.GetNames(typeof(NavStyle)), S(150f))) { e.Nav = (NavStyle)nav; MarkDirty(); }
                            ImGui.SetNextItemWidth(W());
                            var hgt = e.Height; if (ImGui.SliderFloat("Height (vertical; 0 = auto)##el", ref hgt, 0f, 600f, "%.0f px")) { e.Height = hgt; MarkDirty(); }
                        }
                        break;
                    case ElementType.SectionBody:
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                            ImGui.TextWrapped("Shows the selected section. The Controls panel (notes, like, report, close) hangs below whichever panel holds this.");
                            ImGui.PopStyleColor();
                            var fill = e.Fill; if (RsElements.Toggle("ed_fill", ref fill, "Fill remaining height")) { e.Fill = fill; MarkDirty(); }
                            if (!e.Fill)
                            {
                                ImGui.SetNextItemWidth(W());
                                var hgt = e.Height; if (ImGui.SliderFloat("Height##el", ref hgt, 60f, 1200f, "%.0f px")) { e.Height = hgt; MarkDirty(); }
                            }
                        }
                        break;
                    case ElementType.ControlsRow:
                        {
                            ImGui.SetNextItemWidth(W());
                            var sz = e.Size <= 0 ? 28f : e.Size; if (ImGui.SliderFloat("Icon size##el", ref sz, 14f, 64f, "%.0f px")) { e.Size = sz; MarkDirty(); }
                            Hint("Notes / link / like / equipment. If you delete this element, the row is drawn at its default spot on the main panel.");
                        }
                        break;
                    case ElementType.Controls:
                        {
                            var inline = e.Inline; if (RsElements.Toggle("ed_inline", ref inline, "Single row")) { e.Inline = inline; MarkDirty(); }
                        }
                        break;
                    case ElementType.CloseButton:
                        {
                            var t = e.Text; if (RsElements.InputText("ed_eltext", ref t, 32, "Button label (blank = theme's close label)", W(0.95f))) { e.Text = t; MarkDirty(); }
                        }
                        break;
                    case ElementType.LinkReadout:
                    case ElementType.Text:
                        {
                            var t = e.Text; if (RsElements.InputText("ed_eltext", ref t, 200, e.Type == ElementType.Text ? "Text" : "Label (blank = theme default)", W(0.95f))) { e.Text = t; MarkDirty(); }
                            var al = (int)e.Align;
                            ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted("Align"); ImGui.SameLine();
                            if (RsElements.Dropdown("ed_align", ref al, Enum.GetNames(typeof(TextAlign)), S(120f))) { e.Align = (TextAlign)al; MarkDirty(); }
                            if (e.Type == ElementType.Text)
                            {
                                ImGui.SetNextItemWidth(W());
                                var sz = e.Size <= 0 ? 1f : e.Size; if (ImGui.SliderFloat("Scale##el", ref sz, 0.7f, 2.5f, "%.2f")) { e.Size = sz; MarkDirty(); }
                            }
                        }
                        break;
                    case ElementType.Spacer:
                        {
                            ImGui.SetNextItemWidth(W());
                            var sz = e.Size; if (ImGui.SliderFloat("Height##el", ref sz, 0f, 200f, "%.0f px")) { e.Size = sz; MarkDirty(); }
                        }
                        break;
                    case ElementType.Image:
                        {
                            var img = e.Image; if (DrawImageRef("el_img_" + _selEl, "Picture", ref img, ImageRefKind.Picture)) { e.Image = img; MarkDirty(); }
                            if (!e.Free)
                            {
                                ImGui.SetNextItemWidth(W());
                                var hgt = e.Height; if (ImGui.SliderFloat("Height (0 = keep aspect)##el", ref hgt, 0f, 600f, "%.0f px")) { e.Height = hgt; MarkDirty(); }
                            }
                            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                            ImGui.TextWrapped("Give it a mask under Custom style > Pictures to cut its shape.");
                            ImGui.PopStyleColor();
                        }
                        break;
                }
            }
        }
        RsElements.EndPanel();
    }

    // Style tab
    private void DrawStyleTab()
    {
        var S = (Func<float, float>)RsTheme.S;
        var doc = _doc!;
        ImGui.BeginChild("ed_style", ImGui.GetContentRegionAvail(), false);

        if (RsElements.BeginPanel("ed_material", "Material", fitContentsX: false, fitContentsY: true))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextWrapped("The material is how panels are painted — glass, stone, parchment, tattered void, aether. Everything else here tints and tunes it.");
            ImGui.PopStyleColor();
            var m = (int)doc.Material;
            if (RsElements.Dropdown("ed_mat", ref m, Enum.GetNames(typeof(ThemeMaterial)), S(180f))) { doc.Material = (ThemeMaterial)m; MarkDirty(); }
            ImGui.SameLine();
            if (RsElements.Button("Reset colours & style to this material", RsElements.ButtonVariant.Ghost))
            {
                var mat = ImmersiveThemes.CreateMaterial(doc.Material);
                doc.Palette = mat.DefaultPalette; doc.Style = mat.DefaultStyle; MarkDirty();
            }
        }
        RsElements.EndPanel();
        ImGui.Spacing();

        if (RsElements.BeginPanel("ed_palette", "Palette", fitContentsX: false, fitContentsY: true))
        {
            void Color(string label, ThemeColor c)
            {
                var v = c.V;
                ImGui.SetNextItemWidth(S(260f));
                if (ImGui.ColorEdit4(label + "##pal", ref v, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
                { c.R = v.X; c.G = v.Y; c.B = v.Z; c.A = v.W; MarkDirty(); }
            }
            Color("Accent", doc.Palette.Accent);
            Color("Accent (soft)", doc.Palette.AccentSoft);
            Color("Text", doc.Palette.Text);
            Color("Muted text", doc.Palette.Muted);
            Color("Danger", doc.Palette.Danger);
            Color("Surface (top)", doc.Palette.SurfaceTop);
            Color("Surface (bottom)", doc.Palette.SurfaceBot);
        }
        RsElements.EndPanel();
        ImGui.Spacing();

        if (RsElements.BeginPanel("ed_stylek", "Style", fitContentsX: false, fitContentsY: true))
        {
            var st = doc.Style;
            float f;
            ImGui.SetNextItemWidth(W()); f = st.Rounding;       if (ImGui.SliderFloat("Corner rounding", ref f, 0f, 30f, "%.0f px")) { st.Rounding = f; MarkDirty(); }
            ImGui.SetNextItemWidth(W()); f = st.ContentInset;   if (ImGui.SliderFloat("Content inset", ref f, 8f, 64f, "%.0f px")) { st.ContentInset = f; MarkDirty(); }
            ImGui.SetNextItemWidth(W()); f = st.HeaderOffsetY;  if (ImGui.SliderFloat("Header drop", ref f, 0f, 30f, "%.0f px")) { st.HeaderOffsetY = f; MarkDirty(); }
            ImGui.SetNextItemWidth(W()); f = st.FillAlpha;      if (ImGui.SliderFloat("Panel opacity", ref f, 0.3f, 1f, "%.2f")) { st.FillAlpha = f; MarkDirty(); }
            ImGui.SetNextItemWidth(W()); f = st.ScrimAlpha;     if (ImGui.SliderFloat("Screen dim", ref f, 0f, 0.9f, "%.2f")) { st.ScrimAlpha = f; MarkDirty(); }
            ImGui.SetNextItemWidth(W()); f = st.VeilImageAlpha; if (ImGui.SliderFloat("Background image opacity", ref f, 0f, 1f, "%.2f")) { st.VeilImageAlpha = f; MarkDirty(); }
            ImGui.SetNextItemWidth(W()); f = st.FloatAmount;    if (ImGui.SliderFloat("Idle float", ref f, 0f, 3f, "%.1f")) { st.FloatAmount = f; MarkDirty(); }
            var b = st.TechAccents; if (RsElements.Toggle("ed_tech", ref b, "Tech accents (numbered tabs, scanner dot)")) { st.TechAccents = b; MarkDirty(); }
            b = st.ElementGlow;     if (RsElements.Toggle("ed_glow", ref b, "Glow on buttons and avatar")) { st.ElementGlow = b; MarkDirty(); }
            var entrances = Enum.GetNames(typeof(PanelEntrance));
            var ei = Math.Max(0, Array.IndexOf(entrances, st.Entrance));
            ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted("Entrance"); ImGui.SameLine();
            if (RsElements.Dropdown("ed_entrance", ref ei, entrances, S(150f))) { st.Entrance = entrances[ei]; MarkDirty(); }
            var exits = new List<string> { "Same as entrance" }; exits.AddRange(entrances);
            var xi = Math.Max(0, string.IsNullOrEmpty(st.Exit) ? 0 : Array.IndexOf(entrances, st.Exit) + 1);
            ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted("Closing"); ImGui.SameLine();
            if (RsElements.Dropdown("ed_exit", ref xi, exits, S(150f))) { st.Exit = xi == 0 ? "" : entrances[xi - 1]; MarkDirty(); }
            ImGui.SetNextItemWidth(W()); f = st.OpenSpeed;  if (ImGui.SliderFloat("Opening speed", ref f, 0.25f, 4f, "%.2fx")) { st.OpenSpeed = f; MarkDirty(); }
            ImGui.SetNextItemWidth(W()); f = st.CloseSpeed; if (ImGui.SliderFloat("Closing speed", ref f, 0.25f, 4f, "%.2fx")) { st.CloseSpeed = f; MarkDirty(); }
            Hint("How the panels arrive and leave, and how quickly. Closing defaults to the entrance played backwards.");
            var s = st.LinkLabel;  if (RsElements.InputText("ed_link", ref s, 32, "Status label (e.g. LINK ACTIVE)", W(0.95f))) { st.LinkLabel = s; MarkDirty(); }
            s = st.CloseLabel;     if (RsElements.InputText("ed_close", ref s, 32, "Close label (e.g. DISCONNECT)", W(0.95f))) { st.CloseLabel = s; MarkDirty(); }
        }
        RsElements.EndPanel();
        ImGui.EndChild();
    }

    // Effects tab Widget width that leaves room for the label to its right, whatever the inspector's width is (labels used to run off the edge).
    private static float W(float frac = 0.58f)
        => MathF.Max(RsTheme.S(110f), ImGui.GetContentRegionAvail().X * frac);

    private static void Hint(string text)
    {
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImmersiveMode.Tooltip(text);
    }

    private int _fxSel = -1;
    private int _fxAddPick;

    // Names offered when assigning an effect to an element: presets first (marked), then the theme's own.
    private List<(string id, string name)> FxChoices()
    {
        var list = new List<(string, string)>();
        foreach (var p in FxPresets.All) list.Add((p.Id, "\u2605 " + p.Name));
        foreach (var f in _doc!.Effects) list.Add((f.Id, f.Name));
        return list;
    }

    private void DrawElementFxList(ElementStyle st)
    {
        var S = (Func<float, float>)RsTheme.S;
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        ImGui.TextUnformatted("Custom effects");
        ImGui.PopStyleColor();
        Hint("Presets and effects you made in the Effects tab, layered on this element in order.");
        st.Fx ??= new List<string>();
        for (int i = 0; i < st.Fx.Count; i++)
        {
            var name = FxPresets.NameOf(_doc, st.Fx[i]);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("  " + name);
            ImGui.SameLine();
            if (RsElements.IconButton(FontAwesomeIcon.Times, "fxrm" + i, RsElements.ButtonVariant.Ghost, S(22f))) { st.Fx.RemoveAt(i); MarkDirty(); break; }
            Hint("Remove this effect from the element.");
        }
        if (st.Fx.Count == 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted("  (none yet)");
            ImGui.PopStyleColor();
        }
        // Pick a candidate: it previews live on the element in the canvas until "Add" commits it (or the selection changes).
        var choices = FxChoices();
        if (choices.Count == 0) return;
        var names = new List<string> { "(choose an effect to preview)" };
        names.AddRange(choices.Select(c => c.name));
        if (_fxAddPick >= names.Count) _fxAddPick = 0;
        if (RsElements.Dropdown("ed_fx_add", ref _fxAddPick, names, S(240f)))
            _fxCandidate = _fxAddPick > 0 ? choices[_fxAddPick - 1].id : null;
        Hint("Pick one to see it on the element, then press Add. ★ = built-in preset; the rest are from the Effects tab.");
        if (_fxCandidate != null)
        {
            if (RsElements.Button("Add##fxadd", RsElements.ButtonVariant.Primary)) { st.Fx.Add(_fxCandidate); _fxCandidate = null; _fxAddPick = 0; MarkDirty(); }
            ImGui.SameLine();
            if (RsElements.Button("Cancel##fxadd", RsElements.ButtonVariant.Ghost)) { _fxCandidate = null; _fxAddPick = 0; }
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted("Previewing on the element");
            ImGui.PopStyleColor();
        }
    }

    // Effect being tried on the selected element (not yet in the document).
    private string? _fxCandidate;

    private void DrawCustomEffects()
    {
        var S = (Func<float, float>)RsTheme.S;
        var doc = _doc!;
        if (RsElements.BeginPanel("ed_fx_custom", "Your effects", fitContentsX: false, fitContentsY: true))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextWrapped("Start from a preset, give it a name, and nudge the sliders. Assign it to any element from the Layout tab (select the element, turn on Custom style, then Custom effects).");
            ImGui.PopStyleColor();

            // List + create.
            for (int i = 0; i < doc.Effects.Count; i++)
            {
                var f = doc.Effects[i];
                if (ImGui.Selectable($"{f.Name}##fxl{f.Id}", i == _fxSel)) _fxSel = i;
            }
            if (RsElements.Button("+ Create effect", RsElements.ButtonVariant.Secondary)) ImGui.OpenPopup("ed_fx_templates");
            Hint("Pick a template to start from. You can rename and tweak everything afterwards.");
            if (ImGui.BeginPopup("ed_fx_templates"))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextUnformatted("Start from a template");
                ImGui.PopStyleColor();
                ImGui.Separator();
                if (ImGui.Selectable("Blank"))
                {
                    doc.Effects.Add(new FxDef { Name = "New effect" }); _fxSel = doc.Effects.Count - 1; MarkDirty();
                    ImGui.CloseCurrentPopup();
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Everything at 1, nothing chosen yet.");
                foreach (var t in FxPresets.All)
                {
                    if (ImGui.Selectable(t.Name + "##tpl" + t.Id))
                    {
                        var c = t.Clone(); c.Name = t.Name;
                        doc.Effects.Add(c); _fxSel = doc.Effects.Count - 1; MarkDirty();
                        ImGui.CloseCurrentPopup();
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip(FxPresets.Describe(t.Kind) + "\nGrows from: " + t.Shape + " · " + t.Direction);
                }
                ImGui.EndPopup();
            }
            if (_fxSel >= 0 && _fxSel < doc.Effects.Count)
            {
                ImGui.SameLine();
                if (RsElements.Button("Duplicate", RsElements.ButtonVariant.Ghost)) { var c = doc.Effects[_fxSel].Clone(); c.Name += " copy"; doc.Effects.Add(c); _fxSel = doc.Effects.Count - 1; MarkDirty(); }
                ImGui.SameLine();
                if (RsElements.Button("Delete", RsElements.ButtonVariant.Danger))
                {
                    var id = doc.Effects[_fxSel].Id;
                    foreach (var pn in doc.Panels) foreach (var el in pn.Elements) el.Style?.Fx?.RemoveAll(x => x == id);
                    doc.Effects.RemoveAt(_fxSel); _fxSel = Math.Min(_fxSel, doc.Effects.Count - 1); MarkDirty();
                }
                Hint("Also removes it from every element that uses it.");
            }
        }
        RsElements.EndPanel();
        ImGui.Spacing();

        if (_fxSel < 0 || _fxSel >= doc.Effects.Count) return;
        var fx = doc.Effects[_fxSel];
        if (RsElements.BeginPanel("ed_fx_edit", "Edit effect", fitContentsX: false, fitContentsY: true))
        {
            // Two columns: controls | live preview.
            var previewW = S(260f);
            var avail = ImGui.GetContentRegionAvail();
            ImGui.BeginChild("ed_fx_ctl", new Vector2(MathF.Max(S(200f), avail.X - previewW - S(12f)), 0), false, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar);
            var name = fx.Name;
            if (RsElements.InputText("ed_fx_name", ref name, 40, "Name", S(240f))) { fx.Name = name; MarkDirty(); }

            int k = (int)fx.Kind;
            ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted("What"); ImGui.SameLine(S(90f));
            if (RsElements.Dropdown("ed_fx_kind", ref k, Enum.GetNames(typeof(FxKind)), S(170f))) { fx.Kind = (FxKind)k; MarkDirty(); }
            Hint(FxPresets.Describe(fx.Kind));

            int sh = (int)fx.Shape;
            ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted("Grows from"); ImGui.SameLine(S(90f));
            if (RsElements.Dropdown("ed_fx_shape", ref sh, Enum.GetNames(typeof(FxShape)), S(170f))) { fx.Shape = (FxShape)sh; MarkDirty(); }
            Hint("The base shape the effect starts on.\n" + FxPresets.Describe(fx.Shape));

            int d = (int)fx.Direction;
            ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted("Direction"); ImGui.SameLine(S(90f));
            if (RsElements.Dropdown("ed_fx_dir", ref d, Enum.GetNames(typeof(FxDirection)), S(170f))) { fx.Direction = (FxDirection)d; MarkDirty(); }
            Hint("Which way it moves.\n" + FxPresets.Describe(fx.Direction));

            if (fx.Shape == FxShape.Edge || fx.Shape == FxShape.Ring)
            {
                int ol = (int)fx.Outline;
                ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted("Border"); ImGui.SameLine(S(90f));
                if (RsElements.Dropdown("ed_fx_outline", ref ol, new[] { "Auto", "Circle", "Square" }, S(170f))) { fx.Outline = (FxOutline)ol; MarkDirty(); }
                Hint("The outline the effect follows.\n" + FxPresets.Describe(fx.Outline));
            }
            if (fx.Kind == FxKind.Particles)
            {
                int pt = (int)fx.Particle;
                ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted("Bits"); ImGui.SameLine(S(90f));
                if (RsElements.Dropdown("ed_fx_part", ref pt, Enum.GetNames(typeof(FxParticle)), S(170f))) { fx.Particle = (FxParticle)pt; MarkDirty(); }
                Hint("The look of each particle.\n" + FxPresets.Describe(fx.Particle));
            }

            var ua = fx.UseAccent;
            if (RsElements.Toggle("ed_fx_accent", ref ua, "Use the theme's accent colour")) { fx.UseAccent = ua; MarkDirty(); }
            Hint("On: follows the theme accent (and any per-element accent override). Off: pick a colour.");
            if (!fx.UseAccent)
            {
                var cv = fx.Color.V;
                ImGui.SetNextItemWidth(W());
                if (ImGui.ColorEdit4("Colour##fx", ref cv, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar)) { fx.Color = new ThemeColor(cv); MarkDirty(); }
            }

            void Slider(string label, string tip, Func<float> get, Action<float> set, float lo = 0f, float hi = 2f)
            {
                ImGui.SetNextItemWidth(W());
                var val = get();
                if (ImGui.SliderFloat(label + "##fx", ref val, lo, hi, "%.2f")) { set(val); MarkDirty(); }
                Hint(tip);
            }
            Slider("Amount", "How many bits, rings, rays or bands. 1 = the built-in look.", () => fx.Amount, x => fx.Amount = x);
            Slider("Speed", "How fast it moves, spins or pulses.", () => fx.Speed, x => fx.Speed = x, 0.1f, 3f);
            Slider("Size", "How big each bit, line or band is.", () => fx.Size, x => fx.Size = x, 0.2f, 3f);
            Slider("Reach", "How far it travels or spreads out from the shape.", () => fx.Spread, x => fx.Spread = x, 0.1f, 3f);
            Slider("Brightness", "Overall strength. Lower for something subtle.", () => fx.Strength, x => fx.Strength = x, 0.05f, 2f);
            var gl = fx.Glow;
            if (RsElements.Toggle("ed_fx_glow", ref gl, "Soft glow")) { fx.Glow = gl; MarkDirty(); }
            Hint("A soft halo around each bit or line.");

            if (fx.Kind == FxKind.Spinner)
            {
                ImGui.Separator();
                ImGui.SetNextItemWidth(W());
                var rings = fx.Rings; if (ImGui.SliderInt("Rings##fx", ref rings, 1, 6)) { fx.Rings = rings; MarkDirty(); }
                Hint("How many rings around the shape. Every other ring turns the opposite way.");
                ImGui.SetNextItemWidth(W());
                var segs = fx.Segments; if (ImGui.SliderInt("Segments##fx", ref segs, 1, 12)) { fx.Segments = segs; MarkDirty(); }
                Hint("How many pieces each ring is broken into.");
                Slider("Coverage", "How much of each ring is drawn: 1 = a solid ring, 0.5 = half gaps.", () => fx.Coverage, x => fx.Coverage = x, 0.05f, 1f);
                var ticks = fx.Ticks; if (RsElements.Toggle("ed_fx_ticks", ref ticks, "Dial marks")) { fx.Ticks = ticks; MarkDirty(); }
                Hint("Small tick marks outside the rings, slowly turning.");
            }
            ImGui.EndChild();

            ImGui.SameLine();
            DrawFxPreview(fx, previewW);
        }
        RsElements.EndPanel();
        ImGui.Spacing();
    }

    // A small stage: a circle or rect stand-in for an element, with the effect running live around it.
    private void DrawFxPreview(FxDef fx, float w)
    {
        var S = (Func<float, float>)RsTheme.S;
        var h = S(200f);
        var o = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        var theme = ImmersiveThemes.FromDocument(_doc!);
        dl.AddRectFilled(o, o + new Vector2(w, h), ImGui.ColorConvertFloat4ToU32(new Vector4(0.08f, 0.09f, 0.12f, 1f)), S(4f));
        dl.AddRect(o, o + new Vector2(w, h), ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 0.1f)), S(4f));
        dl.PushClipRect(o, o + new Vector2(w, h), true);
        var circle = fx.Outline == FxOutline.Circle || (fx.Outline == FxOutline.Auto && fx.Shape == FxShape.Ring);
        Vector2 min, max;
        if (circle)
        {
            var r = S(44f);
            var c = o + new Vector2(w, h) * 0.5f;
            min = c - new Vector2(r); max = c + new Vector2(r);
            dl.AddCircleFilled(c, r, ImmersiveMode.Col(theme.AccentSoft, 0.25f), 48);
            dl.AddCircle(c, r, ImmersiveMode.Col(theme.Accent, 0.6f), 48, 1.5f);
        }
        else
        {
            min = o + new Vector2(w * 0.22f, h * 0.3f); max = o + new Vector2(w * 0.78f, h * 0.7f);
            dl.AddRectFilled(min, max, ImmersiveMode.Col(theme.AccentSoft, 0.2f));
            dl.AddRect(min, max, ImmersiveMode.Col(theme.Accent, 0.6f));
        }
        try { FxRenderer.Draw(dl, fx, min, max, theme.Accent, MathF.Max(0f, _doc!.Vfx.Intensity), "fxprev", circle); } catch { }
        dl.PopClipRect();
        ImGui.Dummy(new Vector2(w, h));
        Hint("Live preview on a stand-in element. Ring-shaped effects show on a circle, like an avatar.");
    }

    private void DrawEffectsTab()
    {
        var S = (Func<float, float>)RsTheme.S;
        var v = _doc!.Vfx;
        ImGui.BeginChild("ed_fx", ImGui.GetContentRegionAvail(), false);
        DrawCustomEffects();
        if (RsElements.BeginPanel("ed_fx_global", "Overall", fitContentsX: false, fitContentsY: true))
        {
            float f;
            ImGui.SetNextItemWidth(W()); f = v.Intensity;     if (ImGui.SliderFloat("Effect intensity", ref f, 0f, 1.5f, "%.2f")) { v.Intensity = f; MarkDirty(); }
            Hint("Scales every decorative effect at once, yours included. 0 turns them all off.");
            ImGui.SetNextItemWidth(W()); f = v.Speed;         if (ImGui.SliderFloat("Effect speed", ref f, 0.1f, 3f, "%.2fx")) { v.Speed = f; MarkDirty(); }
            Hint("How fast the material's effects move (sweeps, embers, ribbons, glitches...).");
            var uc = v.UseEffectColor; if (RsElements.Toggle("fx_usecol", ref uc, "Effect colour")) { v.UseEffectColor = uc; MarkDirty(); }
            if (v.UseEffectColor)
            {
                ImGui.SameLine();
                var cv = v.EffectColor.V;
                if (ImGui.ColorEdit4("##fx_col", ref cv, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar)) { v.EffectColor = new ThemeColor(cv); MarkDirty(); }
            }
            Hint("Paints the material's effects and panel chrome in this colour instead of the palette accent.");
            ImGui.SetNextItemWidth(W()); f = v.ParallaxDepth; if (ImGui.SliderFloat("Mouse parallax", ref f, 0f, 2f, "%.2f")) { v.ParallaxDepth = f; MarkDirty(); }
            Hint("How much panels shift as the mouse moves, for a sense of depth.");
            var b = v.Vignette; if (RsElements.Toggle("fx_vig", ref b, "Screen vignette")) { v.Vignette = b; MarkDirty(); }
            Hint("Darkens the screen edges behind the profile.");
            b = v.VeilMotes;    if (RsElements.Toggle("fx_motes", ref b, "Background dust / motes")) { v.VeilMotes = b; MarkDirty(); }
            Hint("Slow drifting specks over the whole backdrop.");
        }
        RsElements.EndPanel();
        ImGui.Spacing();

        void Group(string id, string title, params (string key, string label, Func<bool> get, Action<bool> set)[] items)
        {
            if (RsElements.BeginPanel(id, title, fitContentsX: false, fitContentsY: true))
            {
                foreach (var it in items)
                {
                    var b = it.get();
                    if (RsElements.Toggle(it.key, ref b, it.label)) { it.set(b); MarkDirty(); }
                }
            }
            RsElements.EndPanel();
            ImGui.Spacing();
        }
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        ImGui.TextUnformatted("Material effects (each material only uses its own)");
        ImGui.PopStyleColor();
        Group("fx_allagan", "Allagan",
            ("fx_scan", "Scan lines", () => v.ScanLines, x => v.ScanLines = x),
            ("fx_sweep", "Scan sweep", () => v.Sweep, x => v.Sweep = x),
            ("fx_traces", "Circuit traces", () => v.Traces, x => v.Traces = x),
            ("fx_grid", "Background grid + horizon", () => v.Grid, x => v.Grid = x));
        Group("fx_nymian", "Nymian",
            ("fx_veins", "Marble veining", () => v.Veins, x => v.Veins = x),
            ("fx_meander", "Meander bands, dentils, fluting", () => v.Meander, x => v.Meander = x));
        Group("fx_sharlayan", "Sharlayan",
            ("fx_cracks", "Cracks", () => v.Cracks, x => v.Cracks = x),
            ("fx_fade", "Inner parchment fade", () => v.InnerFade, x => v.InnerFade = x));
        Group("fx_void", "Voidtouched",
            ("fx_embers", "Rising embers", () => v.Embers, x => v.Embers = x),
            ("fx_fiss", "Glowing fissures", () => v.Fissures, x => v.Fissures = x),
            ("fx_glitch", "Glitch twitches", () => v.Glitch, x => v.Glitch = x),
            ("fx_dissolve", "Burn in / burn out", () => v.Dissolve, x => v.Dissolve = x));
        Group("fx_aether", "Aether",
            ("fx_ribbons", "Ribbon currents", () => v.Ribbons, x => v.Ribbons = x),
            ("fx_halo", "Panel halo", () => v.Halo, x => v.Halo = x),
            ("fx_edge", "Edge motes", () => v.EdgeMotes, x => v.EdgeMotes = x));
        ImGui.EndChild();
    }

    // Publish tab
    private void DrawPublishTab()
    {
        var S = (Func<float, float>)RsTheme.S;
        var doc = _doc!;
        if (RsElements.BeginPanel("ed_pub", "Details", fitContentsX: false, fitContentsY: true))
        {
            var n = doc.Name;
            if (RsElements.InputText("ed_pub_name", ref n, 120, "Name", S(360f))) { doc.Name = n; MarkDirty(); }
            var d = doc.Description ?? "";
            if (RsElements.InputTextArea("ed_pub_desc", ref d, 1000, "Describe your theme for the gallery", new Vector2(S(520f), S(90f)))) { doc.Description = d; MarkDirty(); }
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted("Who can use it");
            ImGui.PopStyleColor();
            var vis = doc.IsPrivate ? 1 : 0;
            if (doc.RemixLocked)
            {
                vis = 1;
                ImGui.BeginDisabled();
                RsElements.Dropdown("ed_pub_vis", ref vis, new[] { "Everyone (gallery)", "Only me (private)" }, S(220f));
                ImGui.EndDisabled();
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentWarning);
                ImGui.TextWrapped("Based on a theme whose author doesn't allow public variants, so this copy stays private. You can still edit it and wear it once it passes review.");
                ImGui.PopStyleColor();
                if (!doc.IsPrivate) { doc.Visibility = "private"; MarkDirty(); }
            }
            else
            {
                if (RsElements.Dropdown("ed_pub_vis", ref vis, new[] { "Everyone (gallery)", "Only me (private)" }, S(220f))) { doc.Visibility = vis == 1 ? "private" : "gallery"; MarkDirty(); }
                Hint("Gallery: after approval anyone can install it. Private: after approval only you can wear it on your profiles; nobody else sees it in the gallery. Both are reviewed.");
                if (!doc.IsPrivate)
                {
                    var rx = doc.AllowRemix;
                    if (RsElements.Toggle("ed_pub_remix", ref rx, "Let others publish their own variants")) { doc.AllowRemix = rx; MarkDirty(); }
                    Hint("On: people can duplicate your theme and put their version in the gallery. Off: they can still duplicate it for personal use, but their copy can never be published.");
                }
            }
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted(doc.ServerId > 0 ? $"Server id #{doc.ServerId}" + (doc.ServerVersion > 0 ? $" · editing v{doc.ServerVersion}" : "") + (string.IsNullOrEmpty(_serverStatus) ? "" : $" · status: {_serverStatus}") : "Not on the server yet.");
            if (!string.IsNullOrEmpty(doc.BasedOn)) ImGui.TextUnformatted("Based on " + doc.BasedOn);
            ImGui.PopStyleColor();
            if (!string.IsNullOrEmpty(_reviewerNotes))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentWarning);
                ImGui.TextWrapped("Reviewer notes: " + _reviewerNotes);
                ImGui.PopStyleColor();
            }
        }
        RsElements.EndPanel();
        ImGui.Spacing();
        if (RsElements.BeginPanel("ed_pub_actions", "Publish", fitContentsX: false, fitContentsY: true))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextSecondary);
            ImGui.PushStyleColor(ImGuiCol.Text, doc.HasControls ? RsTheme.AccentWarning : RsTheme.AccentDanger);
            ImGui.TextWrapped(doc.HasControls
                ? "Close, Report, Notes, Link, Like and Equipment are placed by the viewer itself on the main panel of every theme. To be accepted into the gallery, don't cover them with other panels or elements."
                : "This theme has no Controls panel (Notes, Equipment, Like, Report and Close). It will be rejected from the gallery until one is added: Layout tab > + Add controls panel.");
            ImGui.PopStyleColor();
            ImGui.Spacing();
            ImGui.TextWrapped("1. Save draft keeps a copy on this computer.  2. Save to server stores it under your account (you can wear it on your own profile right away).  3. Submit for review puts it in the moderation queue — once approved it appears in the gallery for everyone to install.");
            ImGui.Spacing();
            ImGui.TextWrapped("Versions: an approved version is never changed. Saving over one creates a new version that goes through review on its own; until it is approved, everyone keeps the version they have. Once approved it becomes the latest, and people can still pick any older approved version from the gallery.");
            ImGui.PopStyleColor();
            ImGui.Spacing();
            if (RsElements.Button("Save draft", RsElements.ButtonVariant.Secondary)) { ThemeLibrary.SaveDraft(doc); Status("Draft saved."); }
            ImGui.SameLine();
            if (RsElements.Button(doc.ServerId > 0 ? "Update on server" : "Save to server", RsElements.ButtonVariant.Primary)) { ThemeLibrary.SaveDraft(doc); ThemeNetwork.Save(doc); }
            if (doc.ServerId > 0)
            {
                ImGui.SameLine();
                if (RsElements.Button("Submit for review", RsElements.ButtonVariant.Primary)) ThemeNetwork.Submit(doc.ServerId);
                ImGui.SameLine();
                if (RsElements.Button("Use on my profile", RsElements.ButtonVariant.Ghost))
                {
                    var cfg = Plugin.plugin.Configuration;
                    cfg.ImmersiveThemeRef = doc.Ref; cfg.Save();
                    Status("Set as your viewing theme. To show it to others, pick it in your profile's Immersive theme dropdown.");
                }
            }
        }
        RsElements.EndPanel();
    }
}
