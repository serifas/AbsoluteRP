using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AbsoluteRP.Immersive;
using AbsoluteRP.Immersive.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using AbsoluteRP.Network;

namespace AbsoluteRP.RsUI.Pages;

// Themes hub: browse the gallery, manage installed themes, author your own (drafts / pending / approved), and - for moderators - review the queue. The editor itself lives in ThemeEditorWindow.
public sealed class ThemesPage : IPage
{
    public string Id => "themes";
    public string Title => "Themes";
    public FontAwesomeIcon Icon => FontAwesomeIcon.Palette;

    private int _tab;
    private static readonly List<RsElements.NavItem> _tabs = new()
    {
        new(FontAwesomeIcon.Store, "Gallery"),
        new(FontAwesomeIcon.Download, "Installed"),
        new(FontAwesomeIcon.PaintBrush, "My Themes"),
        new(FontAwesomeIcon.Gavel, "Review"),
    };

    private bool _modProbeSent;
    private int _newMaterial;
    private int _dupBuiltin;
    private readonly Dictionary<int, string> _rejectNotes = new();
    private int _pendingOpenServerId;

    public void OnSelected()
    {
        ThemeLibrary.EnsureLoaded();
        ThemeNetwork.RequestList("gallery");
        ThemeNetwork.RequestList("mine");
        ThemeNetwork.DocumentReceived -= OnDocument;
        ThemeNetwork.DocumentReceived += OnDocument;
        // Make sure the current character's profiles are loaded so the pickers (and previews) have real data to work with.
        EnsureCharacterSelected();
        EnsureProfilesFor(SelectedCharacter());
    }

    // profiles per verified character The server's profile list packet always fills the global ProfilesPage.profiles; we keep a copy per character so switching the filter here doesn't need a refetch every time.
    private static readonly Dictionary<string, List<AbsoluteRP.ProfileData>> _profilesByChar = new();
    private static string _fetchingKey = "";
    private static int _charIdx = -1;

    private static string CharKey(string name, string world) => name + "@" + world;
    private static string CharKey(AbsoluteRP.Defines.Character c) => CharKey(c.characterName, c.characterWorld);

    private static List<AbsoluteRP.Defines.Character> VerifiedCharacters()
        => Plugin.plugin?.Configuration?.characters?.Where(c => c != null && !string.IsNullOrEmpty(c.characterName) && !string.IsNullOrEmpty(c.characterKey)).ToList()
           ?? new List<AbsoluteRP.Defines.Character>();

    private static void EnsureCharacterSelected()
    {
        var chars = VerifiedCharacters();
        if (chars.Count == 0) { _charIdx = -1; return; }
        if (_charIdx < 0 || _charIdx >= chars.Count)
        {
            var me = Plugin.character;
            _charIdx = me != null ? Math.Max(0, chars.FindIndex(c => c.characterName == me.characterName && c.characterWorld == me.characterWorld)) : 0;
        }
    }

    private static AbsoluteRP.Defines.Character? SelectedCharacter()
    {
        var chars = VerifiedCharacters();
        return _charIdx >= 0 && _charIdx < chars.Count ? chars[_charIdx] : Plugin.character;
    }

    private static void EnsureProfilesFor(AbsoluteRP.Defines.Character? c)
    {
        if (c == null) return;
        var key = CharKey(c);
        if (_profilesByChar.ContainsKey(key) || _fetchingKey == key) return;
        _fetchingKey = key;
        AbsoluteRP.Network.Profiles_DS.FetchProfiles(c);
    }

    // Called from DataReceiver whenever a profile list arrives.
    public static void OnProfilesReceived(List<AbsoluteRP.ProfileData> list)
    {
        var copy = list.Where(p => p != null).Select(p => new AbsoluteRP.ProfileData
        {
            index = p.index, title = p.title, id = p.id, accountID = p.accountID,
            playerName = p.playerName, playerWorld = p.playerWorld, immersiveTheme = p.immersiveTheme,
        }).ToList();
        var first = copy.FirstOrDefault(p => !string.IsNullOrEmpty(p.playerName));
        var key = first != null ? CharKey(first.playerName, first.playerWorld) : _fetchingKey;
        if (string.IsNullOrEmpty(key)) return;
        _profilesByChar[key] = copy;
        if (_fetchingKey == key) _fetchingKey = "";
        _targetDefaulted = false;   // pick the current profile as the default target
    }

    // The player's current (active) profile is what viewers open, so it is the default target for "Use"; anything else has to be picked on purpose.
    private static bool _targetDefaulted;
    private static bool IsCurrent(AbsoluteRP.ProfileData p)
    {
        var cur = AbsoluteRP.RsUI.Pages.ProfilesPage.CurrentProfile;
        if (cur == null || p == null) return false;
        if (cur.id > 0 && p.id > 0) return cur.id == p.id;
        return cur.index == p.index && string.Equals(p.playerName, Plugin.plugin?.playername, StringComparison.OrdinalIgnoreCase);
    }
    private static void DefaultTarget(List<AbsoluteRP.ProfileData> profiles)
    {
        if (_targetDefaulted || profiles.Count == 0) return;
        _targetDefaulted = true;
        var i = profiles.FindIndex(IsCurrent);
        if (i >= 0) _target = i;
    }

    public static void OnThemeAssigned(string name, string world, int index, int theme)
    {
        if (_profilesByChar.TryGetValue(CharKey(name, world), out var list))
            foreach (var p in list) if (p.index == index) p.immersiveTheme = theme;
    }

    public void OnDeselected()
    {
        ThemeNetwork.DocumentReceived -= OnDocument;
    }

    private int _pendingPreviewServerId;

    private void OnDocument(ThemeDocument doc, string status, string notes)
    {
        if (_pendingOpenServerId != 0 && doc.ServerId == _pendingOpenServerId)
        {
            _pendingOpenServerId = 0;
            ThemeEditorWindow.Open(doc, status, notes);
        }
        if (_pendingPreviewServerId != 0 && doc.ServerId == _pendingPreviewServerId)
        {
            _pendingPreviewServerId = 0;
            ThemeEditorWindow.PreviewOnSelf(doc);
        }
    }

    public void Draw()
    {
        var cfg = Plugin.plugin.Configuration;

        // Header + create controls.
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
        ImGui.Text("Themes");
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        ImGui.TextUnformatted("   How other players see your profile in immersive mode — and how you see theirs.");
        ImGui.PopStyleColor();
        ImGui.Spacing();

        if (RsElements.BeginPanel("themes_create", "Create", fitContentsX: false, fitContentsY: true))
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Start from a material:");
            ImGui.SameLine();
            var mats = Enum.GetNames(typeof(ThemeMaterial));
            RsElements.Dropdown("themes_new_mat", ref _newMaterial, mats, RsTheme.S(150f));
            ImGui.SameLine();
            if (RsElements.Button("Create theme", RsElements.ButtonVariant.Primary))
                ThemeEditorWindow.Open(BuiltinDocuments.Blank((ThemeMaterial)_newMaterial));

            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Or duplicate a built-in:");
            ImGui.SameLine();
            RsElements.Dropdown("themes_dup_builtin", ref _dupBuiltin, ImmersiveThemes.Names, RsTheme.S(150f));
            ImGui.SameLine();
            if (RsElements.Button("Duplicate & edit", RsElements.ButtonVariant.Secondary))
                ThemeEditorWindow.Open(BuiltinDocuments.Create(_dupBuiltin));

            if (!cfg.ImmersiveModeEnabled)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentWarning);
                ImGui.TextWrapped("Immersive mode is off — themes only apply while it's on (Settings → Interface).");
                ImGui.PopStyleColor();
            }
        }
        RsElements.EndPanel();
        ImGui.Spacing();

        // The Review tab exists only for theme moderators: ask the server once (a pending list only comes back for them) and hide it otherwise.
        if (ThemeNetwork.IsThemeModerator == null && !_modProbeSent) { _modProbeSent = true; ThemeNetwork.RequestList("pending"); }
        var isMod = ThemeNetwork.IsThemeModerator == true;
        if (!isMod && _tab == 3) _tab = 0;
        var tabs = isMod ? _tabs : _tabs.GetRange(0, 3);
        var prev = _tab;
        RsElements.NavigationMenu("themes_tabs", ref _tab, tabs);
        if (_tab != prev)
        {
            if (_tab == 0) ThemeNetwork.RequestList("gallery");
            if (_tab == 2) ThemeNetwork.RequestList("mine");
            if (_tab == 3) { ThemeNetwork.RequestList("pending"); ThemeNetwork.RequestList("gallery"); }
        }
        ImGui.Spacing();

        DrawStatusLine();
        if (_tab <= 2) DrawTargetPicker();

        switch (_tab)
        {
            case 0: DrawGallery(cfg); break;
            case 1: DrawInstalled(cfg); break;
            case 2: DrawMine(cfg); break;
            case 3: if (ThemeNetwork.IsThemeModerator == true) DrawReview(); break;
        }
    }

    private static void DrawStatusLine()
    {
        if (string.IsNullOrEmpty(ThemeNetwork.LastMessage)) return;
        if ((DateTime.UtcNow - ThemeNetwork.LastMessageAt).TotalSeconds > 8f) return;
        ImGui.PushStyleColor(ImGuiCol.Text, ThemeNetwork.LastOk ? RsTheme.AccentSuccess : RsTheme.AccentDanger);
        ImGui.TextWrapped(ThemeNetwork.LastMessage);
        ImGui.PopStyleColor();
        ImGui.Spacing();
    }

    // apply target "Use" either sets what I see (my viewer theme) or what viewers see on one of my profiles. 0 = my view, otherwise the profile's index in ProfilesPage.profiles.
    private static int _target;   // 0 = my view; n = profiles[n-1]

    private static List<AbsoluteRP.ProfileData> MyProfiles()
    {
        var c = SelectedCharacter();
        if (c != null && _profilesByChar.TryGetValue(CharKey(c), out var list)) return list;
        return new List<AbsoluteRP.ProfileData>();
    }

    private static void DrawTargetPicker()
    {
        EnsureCharacterSelected();
        var chars = VerifiedCharacters();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Character");
        ImGui.SameLine();
        if (chars.Count == 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted("(no verified characters)");
            ImGui.PopStyleColor();
        }
        else
        {
            var cnames = chars.Select(c => c.characterName + " @ " + c.characterWorld).ToList();
            var ci = Math.Clamp(_charIdx, 0, cnames.Count - 1);
            if (RsElements.Dropdown("themes_char", ref ci, cnames, RsTheme.S(240f)))
            {
                _charIdx = ci; _target = 0; _targetDefaulted = false;
                EnsureProfilesFor(SelectedCharacter());
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Any of your verified characters. Their profiles load on demand.");
        }
        ImGui.SameLine();
        ImGui.TextUnformatted("  Profile");
        ImGui.SameLine();
        var profiles = MyProfiles();
        var c0 = SelectedCharacter();
        var loading = c0 != null && !_profilesByChar.ContainsKey(CharKey(c0));
        if (profiles.Count == 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted(loading ? "Loading profiles…" : "(this character has no profiles)");
            ImGui.PopStyleColor();
        }
        else
        {
            DefaultTarget(profiles);
            var names = profiles.Select(p => (string.IsNullOrWhiteSpace(p.title) ? "Untitled " + (p.index + 1) : p.title) + (IsCurrent(p) ? "  (current)" : "")).ToList();
            if (_target >= names.Count) _target = 0;
            RsElements.Dropdown("themes_target", ref _target, names, RsTheme.S(240f));
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("\"Use\" sets the theme viewers see when they open this profile. Each profile can have its own.\nWhat YOU see on other people's profiles is set in Settings.");
        }
        ImGui.Spacing();
    }

    private static AbsoluteRP.ProfileData? TargetProfile()
    {
        var profiles = MyProfiles();
        return _target >= 0 && _target < profiles.Count ? profiles[_target] : null;
    }

    // Wire id of a theme reference for profile assignment (0 if it can't be assigned - an unsaved draft).
    private static int ThemeIdOf(string themeRef)
    {
        if (themeRef.StartsWith("builtin:") && int.TryParse(themeRef[8..], out var bi)) return bi + 1;
        if (themeRef.StartsWith("server:") && int.TryParse(themeRef[7..], out var sid)) return sid;
        return 0;
    }

    // shared card
    private static string CurrentRef(Configuration cfg)
    {
        var tp = TargetProfile();
        if (tp == null) return "";
        var t = tp.immersiveTheme;
        if (t <= 0) return ImmersiveThemes.DefaultRef;   // nothing chosen yet: the default theme
        return t <= ImmersiveThemes.All.Length ? "builtin:" + (t - 1) : "server:" + t;
    }

    // "Use" always targets the selected character's selected profile; the viewer's own theme lives in Settings.
    private static void UseTheme(Configuration cfg, string themeRef)
    {
        var tp = TargetProfile();
        if (tp == null) { ThemeNetwork.LastMessage = "Pick a profile to apply the theme to."; ThemeNetwork.LastOk = false; ThemeNetwork.LastMessageAt = DateTime.UtcNow; return; }
        var id = ThemeIdOf(themeRef);
        if (id <= 0) return;
        var c = SelectedCharacter();
        ThemeNetwork.Assign(tp.index, id, tp.playerName ?? c?.characterName, tp.playerWorld ?? c?.characterWorld, tp.id);
        ThemeNetwork.LastMessage = "Applying to \"" + (string.IsNullOrWhiteSpace(tp.title) ? "Untitled " + (tp.index + 1) : tp.title) + "\"…";
        ThemeNetwork.LastOk = true; ThemeNetwork.LastMessageAt = DateTime.UtcNow;
    }

    // Thumbnail box: a 16:9 schematic of the theme, or a "fetching" note while the document is on its way from the server.
    private static void DrawThumb(ThemeDocument? doc, float w, float h, string emptyText = "Preview loading…")
    {
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(w, h);
        if (doc != null)
        {
            try { ThemeThumbnail.Draw(dl, doc, min, max); }
            catch (Exception ex) { Plugin.PluginLog?.Debug("[ThemesPage] thumb: " + ex.Message); }
        }
        else
        {
            dl.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(new Vector4(0.08f, 0.09f, 0.12f, 1f)), RsTheme.S(3f));
            var ts = ImGui.CalcTextSize(emptyText);
            dl.AddText(min + (max - min - ts) * 0.5f, ImGui.ColorConvertFloat4ToU32(RsTheme.TextMuted), emptyText);
        }
        dl.AddRect(min, max, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 0.1f)), RsTheme.S(3f));
        ImGui.Dummy(new Vector2(w, h));
    }

    // "v3 " picker: choose which approved version to keep installed. Picking one re-installs pinned to it; "Latest" follows whatever is current.
    private static void DrawVersionPicker(int themeId, ThemeDocument? have, bool installed)
    {
        var vs = ThemeNetwork.VersionsOf(themeId);
        var approved = vs?.Items.Where(v => v.Status == "approved").OrderByDescending(v => v.Version).ToList() ?? new List<ThemeNetwork.ThemeVersionInfo>();
        var haveV = have?.ServerVersion ?? 0;
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(haveV > 0 ? $"v{haveV}" + (vs != null && haveV == vs.CurrentVersion ? " (latest)" : "") : "");
        ImGui.PopStyleColor();
        if (approved.Count <= 1) { if (approved.Count == 1 && ImGui.IsItemHovered()) ImGui.SetTooltip("Only one published version so far."); return; }
        ImGui.SameLine();
        var names = new List<string> { "Latest (follow updates)" };
        names.AddRange(approved.Select(v => $"v{v.Version}" + (v.Version == vs!.CurrentVersion ? "  (latest)" : "") + (string.IsNullOrEmpty(v.Created) ? "" : "  " + v.Created)));
        var sel = 0;
        if (installed && haveV > 0 && haveV != vs!.CurrentVersion) sel = 1 + approved.FindIndex(v => v.Version == haveV);
        if (sel < 0) sel = 0;
        ImGui.SetNextItemWidth(RsTheme.S(150f));
        if (RsElements.Dropdown("ver_" + themeId, ref sel, names, RsTheme.S(150f)))
        {
            var pin = sel == 0 ? 0 : approved[sel - 1].Version;
            ThemeNetwork.Install(themeId, pin);
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Approved versions of this theme. Pick an older one to keep it, or Latest to follow the author's updates.");
    }

    private static ThemeDocument? ServerDoc(int serverId)
    {
        var d = ThemeLibrary.GetServer(serverId);
        if (d == null) ThemeLibrary.RequestServerTheme(serverId);
        return d;
    }

    private static bool BeginCard(string id, string name, string material, string byline, string description, string badge, Vector4? badgeColor, ThemeDocument? doc = null)
    {
        var mat = Enum.TryParse<ThemeMaterial>(material, true, out var m) ? ImmersiveThemes.CreateMaterial(m) : ImmersiveThemes.All[0];
        var w = MathF.Max(RsTheme.S(240f), (ImGui.GetContentRegionAvail().X - RsTheme.S(12f) * 2f) / 3f);
        var open = RsElements.BeginPanel("card_" + id, null, new Vector2(w, RsTheme.S(296f)), false, false);
        if (!open) return false;
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        // Material swatch strip.
        dl.AddRectFilledMultiColor(min, min + new Vector2(w - RsTheme.S(32f), RsTheme.S(8f)),
            ImmersiveMode.Col(mat.Accent, 1f), ImmersiveMode.Col(mat.AccentSoft, 1f), ImmersiveMode.Col(mat.SurfaceTop, 1f), ImmersiveMode.Col(mat.SurfaceTop, 1f));
        ImGui.Dummy(new Vector2(0, RsTheme.S(10f)));
        var tw = w - RsTheme.S(32f);
        DrawThumb(doc, tw, tw * 0.5f);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Layout preview: panels and elements to scale, in the theme's material.");
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
        ImGui.TextUnformatted(name);
        ImGui.PopStyleColor();
        if (!string.IsNullOrEmpty(badge))
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, badgeColor ?? RsTheme.AccentPrimary);
            ImGui.TextUnformatted("  " + badge);
            ImGui.PopStyleColor();
        }
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        ImGui.TextUnformatted(byline);
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextSecondary);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + w - RsTheme.S(40f));
        var desc = string.IsNullOrWhiteSpace(description) ? "—" : description;
        if (desc.Length > 110) desc = desc[..110] + "…";
        ImGui.TextUnformatted(desc);
        ImGui.PopTextWrapPos();
        ImGui.PopStyleColor();
        ImGui.Spacing();
        return true;
    }

    private static void EndCard() => RsElements.EndPanel();

    private static void CardRow(int index, int perRow = 3)
    {
        if (index % perRow != perRow - 1) ImGui.SameLine();
    }

    // Showcase carousel A large preview that walks through the built-ins and the gallery, auto-advancing unless the mouse is over it.
    private int _showIdx;
    private DateTime _showAt = DateTime.UtcNow;

    private void DrawShowcase()
    {
        var S = (Func<float, float>)RsTheme.S;
        var items = new List<(string name, string byline, ThemeDocument? doc)>();
        foreach (var b in ImmersiveThemes.DisplayOrder)
            items.Add((ImmersiveThemes.All[b].Name, b == ImmersiveThemes.DefaultIndex ? "Official · default" : "Official · built in", ThemeThumbnail.Builtin(b)));
        foreach (var s in ThemeNetwork.Gallery.ToList())
            items.Add((s.Name, $"by {s.Author} · {s.InstallCount} installs", ServerDoc(s.Id)));
        if (items.Count == 0) return;
        if (_showIdx >= items.Count) _showIdx = 0;

        var avail = ImGui.GetContentRegionAvail().X;
        var w = MathF.Min(avail, S(720f));
        var h = w * 0.42f;
        if (RsElements.BeginPanel("themes_showcase", null, new Vector2(w + S(24f), h + S(64f)), false, false))
        {
            var it = items[_showIdx];
            DrawThumb(it.doc, w, h, "Preview loading…");
            var hoveredThumb = ImGui.IsItemHovered();
            if (!hoveredThumb && (DateTime.UtcNow - _showAt).TotalSeconds > 6) { _showIdx = (_showIdx + 1) % items.Count; _showAt = DateTime.UtcNow; }
            if (RsElements.IconButton(FontAwesomeIcon.ChevronLeft, "show_prev", RsElements.ButtonVariant.Ghost, S(26f))) { _showIdx = (_showIdx - 1 + items.Count) % items.Count; _showAt = DateTime.UtcNow; }
            ImGui.SameLine();
            if (RsElements.IconButton(FontAwesomeIcon.ChevronRight, "show_next", RsElements.ButtonVariant.Ghost, S(26f))) { _showIdx = (_showIdx + 1) % items.Count; _showAt = DateTime.UtcNow; }
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
            ImGui.TextUnformatted(it.name);
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted($"  {it.byline}   ·   {_showIdx + 1} / {items.Count}");
            ImGui.PopStyleColor();
        }
        RsElements.EndPanel();
        ImGui.Spacing();
    }

    // Gallery
    private void DrawGallery(Configuration cfg)
    {
        if (RsElements.Button("Refresh", RsElements.ButtonVariant.Ghost)) ThemeNetwork.RequestList("gallery");
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        ImGui.TextUnformatted(ThemeNetwork.GalleryLoading ? "Loading…" : $"{ThemeNetwork.Gallery.Count} community themes");
        ImGui.PopStyleColor();
        ImGui.Spacing();

        DrawShowcase();

        var cur = CurrentRef(cfg);
        int i = 0;
        // Official built-ins first.
        foreach (var b in ImmersiveThemes.DisplayOrder)
        {
            var t = ImmersiveThemes.All[b];
            var r = "builtin:" + b;
            if (BeginCard("builtin" + b, t.Name, t.MaterialKind.ToString(), (b == ImmersiveThemes.DefaultIndex ? "Official · default" : "Official · built in"), t.Tagline, cur == r ? "● in use" : "", RsTheme.AccentSuccess, ThemeThumbnail.Builtin(b)))
            {
                if (cur != r && RsElements.Button("Use##b" + b, RsElements.ButtonVariant.Primary)) UseTheme(cfg, r);
                if (cur != r) ImGui.SameLine();
                if (RsElements.Button("Duplicate & edit##b" + b, RsElements.ButtonVariant.Ghost)) ThemeEditorWindow.Open(BuiltinDocuments.Create(b));
                EndCard();
            }
            else EndCard();
            CardRow(i++);
        }
        foreach (var s in ThemeNetwork.Gallery.ToList())
        {
            var r = "server:" + s.Id;
            var installed = s.InstalledByMe || ThemeLibrary.IsInstalled(s.Id);
            var badge = cur == r ? "● in use" : installed ? "installed" : "";
            if (BeginCard("g" + s.Id, s.Name, s.Material, $"by {s.Author} · {s.InstallCount} installs" + (s.Official ? " · official" : ""), s.Description, badge, cur == r ? RsTheme.AccentSuccess : RsTheme.AccentPrimary, ServerDoc(s.Id)))
            {
                if (!installed)
                {
                    if (RsElements.Button("Install##g" + s.Id, RsElements.ButtonVariant.Primary)) ThemeNetwork.Install(s.Id);
                }
                else
                {
                    if (cur != r && RsElements.Button("Use##g" + s.Id, RsElements.ButtonVariant.Primary)) UseTheme(cfg, r);
                    if (cur != r) ImGui.SameLine();
                    if (RsElements.Button("Uninstall##g" + s.Id, RsElements.ButtonVariant.Ghost)) ThemeNetwork.Uninstall(s.Id);
                    DrawVersionPicker(s.Id, ThemeLibrary.GetServer(s.Id), installed: true);
                }
                EndCard();
            }
            else EndCard();
            CardRow(i++);
        }
        if (ThemeNetwork.Gallery.Count == 0 && !ThemeNetwork.GalleryLoading)
        {
            ImGui.NewLine();
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextWrapped("No community themes yet. Make one and submit it for review — approved themes show up here for everyone.");
            ImGui.PopStyleColor();
        }
    }

    // Installed
    private void DrawInstalled(Configuration cfg)
    {
        var cur = CurrentRef(cfg);
        var list = ThemeLibrary.Installed.ToList();
        if (list.Count == 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextWrapped("Nothing installed from the gallery yet. Built-in themes are always available in Settings.");
            ImGui.PopStyleColor();
            return;
        }
        int i = 0;
        foreach (var d in list)
        {
            var r = d.Ref;
            if (BeginCard("i" + d.ServerId, d.Name, d.Material.ToString(), string.IsNullOrEmpty(d.Author) ? "installed" : "by " + d.Author, d.Description, cur == r ? "● in use" : "", RsTheme.AccentSuccess, d))
            {
                if (cur != r && RsElements.Button("Use##i" + d.ServerId, RsElements.ButtonVariant.Primary)) UseTheme(cfg, r);
                if (cur != r) ImGui.SameLine();
                DrawVersionPicker(d.ServerId, d, installed: true);
                {
                    var remote = ThemeNetwork.Gallery.FirstOrDefault(g => g.Id == d.ServerId) ?? ThemeNetwork.InstalledRemote.FirstOrDefault(g => g.Id == d.ServerId);
                    var allow = remote?.AllowRemix ?? d.AllowRemix;
                    if (RsElements.Button((allow ? "Duplicate & edit" : "Edit a private copy") + "##i" + d.ServerId, RsElements.ButtonVariant.Ghost))
                    {
                        var c = d.Remix(d.ServerId, allow); c.Name = d.Name + " (copy)"; c.BasedOn = d.Name;
                        ThemeEditorWindow.Open(c);
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip(allow
                        ? "Make your own version. You may publish it to the gallery."
                        : "The author doesn't allow public variants: your copy can be edited and worn, but only kept private.");
                }
                ImGui.SameLine();
                if (RsElements.Button("Uninstall##i" + d.ServerId, RsElements.ButtonVariant.Ghost)) ThemeNetwork.Uninstall(d.ServerId);
                EndCard();
            }
            else EndCard();
            CardRow(i++);
        }
    }

    // My themes
    private void DrawMine(Configuration cfg)
    {
        if (RsElements.Button("Refresh", RsElements.ButtonVariant.Ghost)) ThemeNetwork.RequestList("mine");
        ImGui.Spacing();

        var cur = CurrentRef(cfg);
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
        ImGui.TextUnformatted("Drafts on this computer");
        ImGui.PopStyleColor();
        ImGui.Spacing();
        var drafts = ThemeLibrary.Drafts.ToList();
        if (drafts.Count == 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted("No drafts. Create a theme above to start one.");
            ImGui.PopStyleColor();
        }
        int i = 0;
        foreach (var d in drafts)
        {
            var remote = d.ServerId > 0 ? ThemeNetwork.Mine.FirstOrDefault(m => m.Id == d.ServerId) : null;
            var status = remote?.Status ?? (d.ServerId > 0 ? "saved" : "local");
            var badge = status switch { "pending" => "pending review", "approved" => "approved", "rejected" => "rejected", "local" => "local draft", _ => "saved to server" };
            var badgeCol = status switch { "approved" => RsTheme.AccentSuccess, "rejected" => RsTheme.AccentDanger, "pending" => RsTheme.AccentWarning, _ => RsTheme.TextMuted };
            if (BeginCard("d" + d.LocalId, d.Name, d.Material.ToString(), "updated " + d.UpdatedUtc.ToLocalTime().ToString("g"), d.Description, badge, badgeCol))
            {
                if (RsElements.Button("Edit##d" + d.LocalId, RsElements.ButtonVariant.Primary)) ThemeEditorWindow.Open(d, status, remote?.Description ?? "");
                ImGui.SameLine();
                if (cur != d.Ref && d.ServerId > 0 && RsElements.Button("Use##d" + d.LocalId, RsElements.ButtonVariant.Secondary)) UseTheme(cfg, d.Ref);
                if (cur != d.Ref) ImGui.SameLine();
                if (RsElements.Button("Delete##d" + d.LocalId, RsElements.ButtonVariant.Ghost))
                {
                    if (d.ServerId > 0) ThemeNetwork.Delete(d.ServerId);
                    ThemeLibrary.DeleteDraft(d);
                }
                EndCard();
            }
            else EndCard();
            CardRow(i++);
        }

        ImGui.NewLine();
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
        ImGui.TextUnformatted("On the server");
        ImGui.PopStyleColor();
        ImGui.Spacing();
        var mine = ThemeNetwork.Mine.ToList();
        if (mine.Count == 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted(ThemeNetwork.MineLoading ? "Loading…" : "Nothing saved to the server yet. Use \"Save to server\" in the editor.");
            ImGui.PopStyleColor();
        }
        i = 0;
        foreach (var s in mine)
        {
            // Badge describes the newest version; note when an older one is live.
            var ls = s.LatestStatus ?? s.Status;
            var live = s.Status == "approved" && s.CurrentVersion > 0;
            var badge = ls switch { "pending" => $"v{s.LatestVersion} pending review", "approved" => $"v{s.LatestVersion} in gallery", "rejected" => $"v{s.LatestVersion} rejected", _ => $"v{s.LatestVersion} draft" };
            if (live && ls != "approved") badge += $" · v{s.CurrentVersion} in gallery";
            if (s.Visibility == "private") badge = badge.Replace("in gallery", "approved (private)");
            var badgeCol = ls switch { "approved" => RsTheme.AccentSuccess, "rejected" => RsTheme.AccentDanger, "pending" => RsTheme.AccentWarning, _ => RsTheme.TextMuted };
            if (BeginCard("m" + s.Id, s.Name, s.Material, $"#{s.Id} · {s.InstallCount} installs", s.Description, badge, badgeCol))
            {
                if (RsElements.Button("Edit##m" + s.Id, RsElements.ButtonVariant.Primary))
                {
                    var local = ThemeLibrary.Drafts.FirstOrDefault(d => d.ServerId == s.Id);
                    if (local != null) ThemeEditorWindow.Open(local, ls, "");
                    else { _pendingOpenServerId = s.Id; ThemeNetwork.RequestTheme(s.Id, -1); }
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(ls == "approved"
                    ? "Approved versions never change. Saving will create a new version; people keep the one they have until the new one is approved."
                    : "Edits the newest version.");
                ImGui.SameLine();
                if (ls == "draft" || ls == "rejected")
                {
                    if (RsElements.Button("Submit for review##m" + s.Id, RsElements.ButtonVariant.Secondary)) ThemeNetwork.Submit(s.Id);
                    ImGui.SameLine();
                }
                else if (ls == "pending")
                {
                    if (RsElements.Button("Withdraw##m" + s.Id, RsElements.ButtonVariant.Ghost)) ThemeNetwork.Withdraw(s.Id);
                    ImGui.SameLine();
                }
                if (RsElements.Button("Delete##m" + s.Id, RsElements.ButtonVariant.Ghost)) ThemeNetwork.Delete(s.Id);
                EndCard();
            }
            else EndCard();
            CardRow(i++);
        }
    }

    // Review queue (moderators)
    private void DrawReview()
    {
        if (RsElements.Button("Refresh", RsElements.ButtonVariant.Ghost)) { ThemeNetwork.RequestList("pending"); ThemeNetwork.RequestList("gallery"); }
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        ImGui.TextUnformatted(ThemeNetwork.PendingLoading ? "Loading…" : $"{ThemeNetwork.Pending.Count} awaiting review");
        ImGui.PopStyleColor();
        ImGui.Spacing();
        if (ThemeNetwork.Pending.Count == 0 && !ThemeNetwork.PendingLoading)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextWrapped("Nothing pending — or you don't have moderator permissions.");
            ImGui.PopStyleColor();
        }
        foreach (var s in ThemeNetwork.Pending.ToList())
        {
            if (RsElements.BeginPanel("rev_" + s.Id, $"#{s.Id}  {s.Name}  ·  v{s.LatestVersion}", fitContentsX: false, fitContentsY: true))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextUnformatted($"by {s.Author} (account {s.AuthorAccountId}) · {s.Material}" + (s.Visibility == "private" ? " · PRIVATE (only the author will wear it)" : " · for the gallery") + (s.CurrentVersion > 0 ? $" · v{s.CurrentVersion} currently approved" : " · first submission"));
                ImGui.PopStyleColor();
                ImGui.TextWrapped(string.IsNullOrWhiteSpace(s.Description) ? "(no description)" : s.Description);
                ImGui.Spacing();
                if (RsElements.Button("Preview on my profile##r" + s.Id, RsElements.ButtonVariant.Secondary))
                {
                    // Always the version under review, never the cached approved one.
                    _pendingPreviewServerId = s.Id;
                    ThemeNetwork.RequestTheme(s.Id, -1);
                    ThemeNetwork.LastMessage = "Fetching the submitted version…"; ThemeNetwork.LastMessageAt = DateTime.UtcNow; ThemeNetwork.LastOk = true;
                }
                ImGui.SameLine();
                if (RsElements.Button("Approve##r" + s.Id, RsElements.ButtonVariant.Primary)) ThemeNetwork.Review(s.Id, true, "");
                ImGui.SameLine();
                _rejectNotes.TryGetValue(s.Id, out var notes);
                notes ??= "";
                RsElements.InputText("rej_" + s.Id, ref notes, 300, "Reason for rejection", RsTheme.S(260f));
                _rejectNotes[s.Id] = notes;
                ImGui.SameLine();
                if (RsElements.Button("Reject##r" + s.Id, RsElements.ButtonVariant.Danger)) ThemeNetwork.Review(s.Id, false, notes);
            }
            RsElements.EndPanel();
            ImGui.Spacing();
        }

        // Accepted themes: pull one from the gallery if it turns out to break the rules. Removal is a rejection with notes, so the author sees why and can fix and resubmit.
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        ImGui.TextUnformatted($"In the gallery ({ThemeNetwork.Gallery.Count})");
        ImGui.PopStyleColor();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Approved themes everyone can install. Removing one takes it out of the gallery and sends your note to the author.");
        ImGui.Spacing();
        if (ThemeNetwork.Gallery.Count == 0 && !ThemeNetwork.GalleryLoading)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted("Nothing approved yet.");
            ImGui.PopStyleColor();
        }
        foreach (var s in ThemeNetwork.Gallery.ToList())
        {
            if (RsElements.BeginPanel("acc_" + s.Id, $"#{s.Id}  {s.Name}", fitContentsX: false, fitContentsY: true))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextUnformatted($"by {s.Author} (account {s.AuthorAccountId}) · {s.Material} · {s.InstallCount} installs" + (s.Official ? " · official" : ""));
                ImGui.PopStyleColor();
                if (RsElements.Button("Preview on my profile##a" + s.Id, RsElements.ButtonVariant.Secondary))
                {
                    var doc = ThemeLibrary.GetServer(s.Id);
                    if (doc != null) ThemeEditorWindow.PreviewOnSelf(doc);
                    else { _pendingOpenServerId = 0; ThemeNetwork.RequestTheme(s.Id); ThemeNetwork.LastMessage = "Fetching theme — click again in a moment."; ThemeNetwork.LastMessageAt = DateTime.UtcNow; ThemeNetwork.LastOk = true; }
                }
                ImGui.SameLine();
                _rejectNotes.TryGetValue(s.Id, out var notes);
                notes ??= "";
                RsElements.InputText("rem_" + s.Id, ref notes, 300, "Reason (sent to the author)", RsTheme.S(260f));
                _rejectNotes[s.Id] = notes;
                ImGui.SameLine();
                var canRemove = !string.IsNullOrWhiteSpace(notes);
                if (!canRemove) ImGui.BeginDisabled();
                if (RsElements.Button("Remove from gallery##a" + s.Id, RsElements.ButtonVariant.Danger)) ThemeNetwork.Review(s.Id, false, notes);
                if (!canRemove) { ImGui.EndDisabled(); if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip("Give the author a reason first."); }
            }
            RsElements.EndPanel();
            ImGui.Spacing();
        }
    }
}
