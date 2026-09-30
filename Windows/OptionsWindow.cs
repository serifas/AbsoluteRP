using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;
using System.Text;
using AbsoluteRP.Network;

namespace AbsoluteRP.Windows
{
    // Plugin settings. Section nav on top, slides between sections, RsUI panels + toggles throughout.
    public sealed class OptionsWindow : RsWindow, IDisposable
    {
        public static OptionsWindow? Instance;
        public static AbsoluteRP.RsUI.RsFileDialogManager _fileDialogManager = new();
        public static bool ChangeDataPath;

        private int _section;
        private int _prevSection = -1;
        private float _t = 1f;
        private const float TransitionSeconds = 0.32f;

        private bool _openBehavior = false;
        private bool _openVisibility = false;
        private bool _openFields = false;
        private bool _openCompassRestrictions = true;

        private readonly RsElements.NavItem[] _sections =
        {
            new(FontAwesomeIcon.IdCard,        "Profile"),
            new(FontAwesomeIcon.SlidersH,      "General"),
            new(FontAwesomeIcon.LocationArrow, "Compass"),
            new(FontAwesomeIcon.Database,      "Data"),
            new(FontAwesomeIcon.CommentDots,   "Tooltips"),
            new(FontAwesomeIcon.PaintBrush,    "Theme"),
        };
        // Public so other windows (the account panel's Edit Profile button) can jump the user straight to this section.
        public const int SectionProfile = 0;

        // Profile-tab local state - kept flat so it survives section switches without needing per-section state objects.
        private int _profileAgeInput;
        private string? _profileAgeBuf;
        private bool    _profileUploading;
        private string  _profileUploadStatus = string.Empty;
        private bool    _headerUploading;
        private string  _headerUploadStatus = string.Empty;
        // RP preference tag choices exposed in the Profile section.
        private static readonly string[] _rpPrefChoices =
        {
            "Multi-paragraph", "Paragraph", "Semi-paragraph", "Casual",
            "Long-term", "Short-term", "One-shot",
            "SFW only", "NSFW OK",
            "In-character always", "OOC-friendly",
            "Story-driven", "Combat-focused", "Slice-of-life",
        };

        private static readonly (string Name, Vector4 Accent, Vector4 Border, Vector4 Font)[] _presets =
        {
            ("Aether",          new(0.28f, 0.42f, 0.78f, 1f), new(0.16f, 0.16f, 0.22f, 0.65f), new(0.82f, 0.83f, 0.88f, 1f)),
            ("Void",            new(0.48f, 0.26f, 0.72f, 1f), new(0.25f, 0.18f, 0.38f, 0.65f), new(0.78f, 0.75f, 0.88f, 1f)),
            ("Dynamis",         new(0.40f, 0.82f, 0.65f, 1f), new(0.16f, 0.30f, 0.24f, 0.65f), new(0.78f, 0.90f, 0.84f, 1f)),
            ("Maelstrom",       new(0.70f, 0.18f, 0.22f, 1f), new(0.30f, 0.14f, 0.14f, 0.65f), new(0.85f, 0.78f, 0.78f, 1f)),
            ("Twin Adders",     new(0.78f, 0.62f, 0.18f, 1f), new(0.30f, 0.24f, 0.10f, 0.65f), new(0.90f, 0.84f, 0.68f, 1f)),
            ("Immortal Flames", new(0.75f, 0.42f, 0.14f, 1f), new(0.30f, 0.20f, 0.14f, 0.65f), new(0.88f, 0.82f, 0.75f, 1f)),
            ("Temple Knights",  new(0.14f, 0.48f, 0.72f, 1f), new(0.14f, 0.22f, 0.30f, 0.65f), new(0.75f, 0.82f, 0.90f, 1f)),
        };

        public OptionsWindow() : base("arp_options", "Settings")
        {
            Size = new Vector2(680, 560);
            Position = new Vector2(320, 220);
            SizeCondition = ImGuiCond.FirstUseEver;
            PositionCondition = ImGuiCond.FirstUseEver;
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(520, 400),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
            };
            Instance = this;
        }

        protected override void DrawBody() => DrawContent();

        public void DrawContent()
        {
            FlushSave();   // anything toggled last frame
            try
            {
                _fileDialogManager.Draw();
                // Pump the header-crop popup so it renders on top of the settings window whenever an upload just completed.
                AbsoluteRP.Social.SocialHeaderCrop.Draw();
                if (ChangeDataPath)
                {
                    _fileDialogManager.OpenFolderDialog("Select Data Save Path", (ok, path) =>
                    {
                        if (ok && !string.IsNullOrEmpty(path))
                        {
                            Plugin.plugin.Configuration.dataSavePath = path;
                            Plugin.plugin.Configuration.Save();
                        }
                    });
                    ChangeDataPath = false;
                }

                var picked = _section;
                if (RsElements.NavigationMenu("opts_sections", ref picked, _sections))
                {
                    if (picked != _section)
                    {
                        _prevSection = _section;
                        _section = picked;
                        _t = 0f;
                    }
                }

                ImGui.Spacing();

                var dt = ImGui.GetIO().DeltaTime;
                if (_prevSection >= 0 && _t < 1f)
                {
                    _t = Math.Min(1f, _t + dt / TransitionSeconds);
                    if (_t >= 1f) _prevSection = -1;
                }

                var start = ImGui.GetCursorScreenPos();
                var slide = Math.Max(RsTheme.S(80f), ImGui.GetContentRegionAvail().X);

                if (_prevSection >= 0 && _t < 0.5f)
                {
                    var lt = _t * 2f;
                    var e = 1f - (1f - lt) * (1f - lt);
                    ImGui.SetCursorScreenPos(start);
                    DrawSlice(_prevSection, -slide * e, 1f - e);
                }

                float alpha, xOff;
                if (_prevSection < 0)      { alpha = 1f; xOff = 0f; }
                else if (_t < 0.5f)        { alpha = 0f; xOff = slide; }
                else
                {
                    var lt = (_t - 0.5f) * 2f;
                    var e = 1f - (1f - lt) * (1f - lt);
                    alpha = e; xOff = slide * (1f - e);
                }

                ImGui.SetCursorScreenPos(start);
                DrawSlice(_section, xOff, alpha);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("OptionsWindow: " + ex.Message);
            }
        }

        private void DrawSlice(int section, float xOffset, float alpha)
        {
            var origin = ImGui.GetCursorScreenPos();
            ImGui.SetCursorScreenPos(new Vector2(origin.X + xOffset, origin.Y));
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, Math.Clamp(alpha, 0f, 1f));
            ImGui.BeginGroup();
            try
            {
                switch (section)
                {
                    case 0: DrawProfile(); break;
                    case 1: DrawGeneral(); break;
                    case 2: DrawCompass(); break;
                    case 3: DrawData(); break;
                    case 4: DrawTooltips(); break;
                    case 5: DrawTheme(); break;
                }
            }
            finally
            {
                ImGui.EndGroup();
                ImGui.PopStyleVar();
            }
        }

        private static bool _saveRequested;
        private static void FlushSave()
        {
            if (!_saveRequested) return;
            _saveRequested = false;
            try { Plugin.plugin.Configuration.Save(); } catch (Exception ex) { Plugin.PluginLog.Debug("Options save failed: " + ex.Message); }
        }

        private static bool Bind(string id, string label, ref bool value)
        {
            if (RsElements.Toggle(id, ref value, label))
            {
                // The caller writes the new value into the config only after this returns, so saving here stored the OLD value. Flag it and save once the assignment has happened.
                _saveRequested = true;
                return true;
            }
            return false;
        }

        private void DrawGeneral()
        {
            var cfg = Plugin.plugin.Configuration;

            if (RsElements.BeginPanel("opt_integration", "Game integration", fitContentsX: false, fitContentsY: true))
            {
                var dtr = cfg.ShowDtrEntries; if (Bind("opt_dtr", "Show entries in the server-info bar", ref dtr)) cfg.ShowDtrEntries = dtr;
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextUnformatted("Right-click menu on players:");
                ImGui.PopStyleColor();
                var mv = cfg.MenuViewProfile;     if (Bind("opt_menu_view", "View profile", ref mv)) cfg.MenuViewProfile = mv;
                var mb = cfg.MenuBookmarkProfile; if (Bind("opt_menu_bm", "Bookmark profile", ref mb)) cfg.MenuBookmarkProfile = mb;
                var mi = cfg.MenuInviteToGroup;   if (Bind("opt_menu_inv", "Invite to group", ref mi)) cfg.MenuInviteToGroup = mi;
                var mt = cfg.MenuOpenTrade;       if (Bind("opt_menu_trade", "Open ARP trade", ref mt)) cfg.MenuOpenTrade = mt;
            }
            RsElements.EndPanel();
            ImGui.Spacing();

            if (RsElements.BeginPanel("opt_links", "Support Links", fitContentsX: false, fitContentsY: true))
            {
                var k = cfg.showKofi;    if (Bind("opt_kofi",    "Show Ko-fi button",    ref k)) cfg.showKofi    = k;
                var p = cfg.showPatreon; if (Bind("opt_patreon", "Show Patreon button",  ref p)) cfg.showPatreon = p;
                var d = cfg.showDisc;    if (Bind("opt_disc",    "Show Discord button",  ref d)) cfg.showDisc    = d;
                var w = cfg.showWeb;     if (Bind("opt_web",     "Show Website button",  ref w)) cfg.showWeb     = w;
            }
            RsElements.EndPanel();

            ImGui.Spacing();

            if (RsElements.BeginPanel("opt_interface", "Interface", fitContentsX: false, fitContentsY: true))
            {
                var a = cfg.AnimationsEnabled;
                if (Bind("opt_anim", "Window animations (fades, slides, avatar zoom)", ref a)) cfg.AnimationsEnabled = a;

                var t = cfg.TutorialsEnabled;
                if (Bind("opt_tut", "Show guided tutorials in profile and plugin windows", ref t)) cfg.TutorialsEnabled = t;

                {
                    // Profile themes are chosen per profile by their owner (Themes tab); only the layout placement is a viewer-side setting.
                    ImGui.Spacing();
                    if (RsElements.Button("Reset profile position & size##opt_immersive_reset", RsElements.ButtonVariant.Ghost))
                        AbsoluteRP.Immersive.ImmersiveHud.ResetLayoutPlacement();
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Drag any profile panel to move the whole layout, and drag the corner grip\nof the main panel to resize it. This puts both back to the default.");
                }
            }
            RsElements.EndPanel();
        }

        private void DrawCompass()
        {
            var cfg = Plugin.plugin.Configuration;

            if (RsElements.BeginPanel("opt_cmp", "Compass", fitContentsX: false, fitContentsY: true))
            {
                var c = cfg.showCompass;
                if (Bind("opt_show_cmp", "Show compass overlay", ref c)) cfg.showCompass = c;
            }
            RsElements.EndPanel();

            ImGui.Spacing();

            if (RsElements.BeginCollapsible("opt_cmp_when", "When to show", ref _openCompassRestrictions))
            {
                var combat = cfg.showCompassInCombat;
                if (Bind("opt_cmp_combat", "Show in combat", ref combat)) cfg.showCompassInCombat = combat;

                var duty = cfg.showCompassInDuty;
                if (Bind("opt_cmp_duty", "Show in duty", ref duty)) cfg.showCompassInDuty = duty;

                var pvp = cfg.showCompassInPvP;
                if (Bind("opt_cmp_pvp", "Show in PvP", ref pvp)) cfg.showCompassInPvP = pvp;
            }
            RsElements.EndCollapsible();
        }

        private void DrawData()
        {
            var cfg = Plugin.plugin.Configuration;

            if (RsElements.BeginPanel("opt_backup", "Automatic Backup", fitContentsX: false, fitContentsY: true))
            {
                var b = cfg.AutobackupEnabled;
                if (Bind("opt_autobkp", "Save a local copy of my character data", ref b)) cfg.AutobackupEnabled = b;

                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextUnformatted("Backup folder");
                ImGui.PopStyleColor();

                var path = cfg.dataSavePath;
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextSecondary);
                ImGui.TextWrapped(string.IsNullOrEmpty(path) ? "(default plugin folder)" : path);
                ImGui.PopStyleColor();

                ImGui.Spacing();
                if (RsElements.Button("Change folder", RsElements.ButtonVariant.Ghost))
                    ChangeDataPath = true;

                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextWrapped("Your data always lives on the server. Local backups just save disk space if disabled.");
                ImGui.PopStyleColor();
            }
            RsElements.EndPanel();
        }

        private void DrawTooltips()
        {
            var cfg = Plugin.plugin.Configuration;

            if (RsElements.BeginCollapsible("opt_tt_behavior", "Behavior", ref _openBehavior))
            {
                var drag = cfg.tooltip_draggable;
                if (Bind("opt_tt_drag", "Follows the cursor", ref drag)) cfg.tooltip_draggable = drag;

                var lockClick = cfg.tooltip_LockOnClick;
                if (Bind("opt_tt_lock", "Freeze in place when a target is selected", ref lockClick)) cfg.tooltip_LockOnClick = lockClick;
            }
            RsElements.EndCollapsible();

            ImGui.Spacing();

            if (RsElements.BeginCollapsible("opt_tt_visibility", "Visibility", ref _openVisibility))
            {
                var on = cfg.tooltip_Enabled;
                if (Bind("opt_tt_on", "Show tooltip", ref on)) cfg.tooltip_Enabled = on;

                var hc = cfg.tooltip_HideInCombat;
                if (Bind("opt_tt_hc", "Hide in combat", ref hc)) cfg.tooltip_HideInCombat = hc;

                var hd = cfg.tooltip_DutyDisabled;
                if (Bind("opt_tt_hd", "Hide in duty", ref hd)) cfg.tooltip_DutyDisabled = hd;

                var hp = cfg.tooltip_PvPDisabled;
                if (Bind("opt_tt_hp", "Hide in PvP", ref hp)) cfg.tooltip_PvPDisabled = hp;
            }
            RsElements.EndCollapsible();

            ImGui.Spacing();

            if (RsElements.BeginCollapsible("opt_tt_fields", "Fields to display", ref _openFields))
            {
                var av = cfg.tooltip_showAvatar; if (Bind("opt_ttf_av", "Avatar", ref av)) cfg.tooltip_showAvatar = av;
                var nm = cfg.tooltip_showName;   if (Bind("opt_ttf_nm", "Name", ref nm)) cfg.tooltip_showName = nm;
                var ra = cfg.tooltip_showRace;   if (Bind("opt_ttf_ra", "Race", ref ra)) cfg.tooltip_showRace = ra;
                var ge = cfg.tooltip_showGender; if (Bind("opt_ttf_ge", "Gender", ref ge)) cfg.tooltip_showGender = ge;
                var ag = cfg.tooltip_showAge;    if (Bind("opt_ttf_ag", "Age", ref ag)) cfg.tooltip_showAge = ag;
                var he = cfg.tooltip_showHeight; if (Bind("opt_ttf_he", "Height", ref he)) cfg.tooltip_showHeight = he;
                var wt = cfg.tooltip_showWeight; if (Bind("opt_ttf_wt", "Weight", ref wt)) cfg.tooltip_showWeight = wt;
                var al = cfg.tooltip_showAlignment; if (Bind("opt_ttf_al", "Alignment", ref al)) cfg.tooltip_showAlignment = al;
                var pe = cfg.tooltip_showPersonalityTraits; if (Bind("opt_ttf_pe", "Personality traits", ref pe)) cfg.tooltip_showPersonalityTraits = pe;
                var ct = cfg.tooltip_showCustomTraits; if (Bind("opt_ttf_ct", "Custom traits", ref ct)) cfg.tooltip_showCustomTraits = ct;
            }
            RsElements.EndCollapsible();
        }

        private void DrawTheme()
        {
            var cfg = Plugin.plugin.Configuration;

            if (RsElements.BeginPanel("opt_th_colors", "Colors", fitContentsX: false, fitContentsY: true))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextWrapped("Changes preview live across every plugin window.");
                ImGui.PopStyleColor();
                ImGui.Spacing();

                var border = cfg.ThemeBorder ?? ThemeManager.DefaultBorder;
                if (ThemeManager.DrawColorPicker("Border", ref border, ThemeManager.DefaultBorder))
                { cfg.ThemeBorder = border; cfg.Save(); }

                var bg = cfg.ThemeBackground ?? ThemeManager.DefaultBackground;
                if (ThemeManager.DrawColorPicker("Background", ref bg, ThemeManager.DefaultBackground))
                { cfg.ThemeBackground = bg; cfg.Save(); }

                var accent = cfg.ThemeAccent ?? ThemeManager.DefaultAccent;
                if (ThemeManager.DrawColorPicker("Accent", ref accent, ThemeManager.DefaultAccent))
                { cfg.ThemeAccent = accent; cfg.Save(); }

                var font = cfg.ThemeFont ?? ThemeManager.DefaultFont;
                if (ThemeManager.DrawColorPicker("Font", ref font, ThemeManager.DefaultFont))
                { cfg.ThemeFont = font; cfg.Save(); }
            }
            RsElements.EndPanel();

            ImGui.Spacing();

            if (RsElements.BeginPanel("opt_th_presets", "Presets", fitContentsX: false, fitContentsY: true))
            {
                var active = ActivePreset(cfg);
                var chipW = RsTheme.S(140f);
                var chipH = RsTheme.S(34f);
                var gap = RsTheme.S(8f);
                var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (chipW + gap)));

                for (int i = 0; i < _presets.Length; i++)
                {
                    if (i % perRow != 0) ImGui.SameLine(0f, gap);
                    DrawPresetChip(i, _presets[i], i == active, chipW, chipH);
                }
            }
            RsElements.EndPanel();

            ImGui.Spacing();

            if (RsElements.BeginPanel("opt_th_preview", "Preview", fitContentsX: false, fitContentsY: true))
            {
                ThemeManager.StatusDot("Online", ThemeManager.Success);
                ImGui.SameLine(0f, RsTheme.S(16f));
                ThemeManager.StatusDot("Away", ThemeManager.Warning);
                ImGui.SameLine(0f, RsTheme.S(16f));
                ThemeManager.StatusDot("Offline", ThemeManager.FontDim);

                ImGui.Spacing();

                ThemeManager.Badge("Verified");
                ImGui.SameLine();
                ThemeManager.Badge("Premium", ThemeManager.Warning);
                ImGui.SameLine();
                ThemeManager.Badge("New", ThemeManager.Success);

                ImGui.Spacing();
                ThemeManager.StyledProgressBar(0.65f, new Vector2(-1, RsTheme.S(16f)), "65% Complete");
                ImGui.Spacing();

                if (RsElements.Button("Primary")) { }
                ImGui.SameLine();
                if (RsElements.Button("Secondary", RsElements.ButtonVariant.Secondary)) { }
                ImGui.SameLine();
                if (RsElements.Button("Ghost", RsElements.ButtonVariant.Ghost)) { }
                ImGui.SameLine();
                if (RsElements.Button("Delete", RsElements.ButtonVariant.Danger)) { }
            }
            RsElements.EndPanel();
        }

        private static int ActivePreset(Configuration cfg)
        {
            if (cfg.ThemeAccent == null && cfg.ThemeBorder == null && cfg.ThemeFont == null) return 0;
            var a = cfg.ThemeAccent ?? ThemeManager.DefaultAccent;
            for (int i = 0; i < _presets.Length; i++)
            {
                var pa = _presets[i].Accent;
                if (Math.Abs(a.X - pa.X) < 0.02f &&
                    Math.Abs(a.Y - pa.Y) < 0.02f &&
                    Math.Abs(a.Z - pa.Z) < 0.02f)
                    return i;
            }
            return -1;
        }

        private static void DrawPresetChip(int i, (string Name, Vector4 Accent, Vector4 Border, Vector4 Font) p, bool active, float w, float h)
        {
            var draw = ImGui.GetWindowDrawList();
            var min = ImGui.GetCursorScreenPos();
            var max = new Vector2(min.X + w, min.Y + h);

            ImGui.SetCursorScreenPos(min);
            var clicked = ImGui.InvisibleButton("##opt_preset_" + i, new Vector2(w, h));
            var hovered = ImGui.IsItemHovered();

            var dim = new Vector4(p.Accent.X * 0.55f, p.Accent.Y * 0.55f, p.Accent.Z * 0.55f, 1f);
            var fill = ImGui.ColorConvertFloat4ToU32(hovered ? p.Accent : dim);
            var border = active
                ? ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.9f))
                : ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.35f));

            var radius = RsTheme.S(6f);
            draw.AddRectFilled(min, max, fill, radius);
            draw.AddRect(min, max, border, radius, ImDrawFlags.None, active ? RsTheme.S(2f) : RsTheme.S(1f));

            var textSize = ImGui.CalcTextSize(p.Name);
            var textPos = new Vector2(min.X + (w - textSize.X) * 0.5f, min.Y + (h - textSize.Y) * 0.5f);
            draw.AddText(textPos, ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f)), p.Name);

            if (clicked)
            {
                var cfg = Plugin.plugin.Configuration;
                if (i == 0)
                {
                    cfg.ThemeBorder = null;
                    cfg.ThemeBackground = null;
                    cfg.ThemeAccent = null;
                    cfg.ThemeFont = null;
                }
                else
                {
                    cfg.ThemeBorder = p.Border;
                    cfg.ThemeBackground = new Vector4(0f, 0f, 0f, 0.969f);
                    cfg.ThemeAccent = p.Accent;
                    cfg.ThemeFont = p.Font;
                }
                cfg.Save();
            }
        }

        // Called by AccountWindow's "Edit account profile" button. Just changes the visible section; the caller is responsible for making sure the Settings window/page is on screen.
        public void SelectSection(int section)
        {
            if (section < 0 || section >= _sections.Length) return;
            if (section == _section) return;
            _prevSection = _section;
            _section = section;
            _t = 0f;
        }

        private void DrawProfile()
        {
            var cfg = Plugin.plugin?.Configuration;
            var acct = cfg?.account;
            if (acct == null)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextWrapped("Log in with an account first — the profile is bound to the account.");
                ImGui.PopStyleColor();
                return;
            }
            acct.profile ??= new AbsoluteRP.Defines.AccountProfile();
            var p = acct.profile;

            if (RsElements.BeginPanel("prof_id", "Identity", fitContentsX: false, fitContentsY: true))
            {
                var previewSz = RsTheme.S(96f);
                var dl        = ImGui.GetWindowDrawList();
                var avatarMin = ImGui.GetCursorScreenPos();
                var avatarMax = avatarMin + new Vector2(previewSz, previewSz);
                var radius    = RsTheme.S(8f);

                dl.AddRectFilled(avatarMin, avatarMax,
                    ImGui.ColorConvertFloat4ToU32(RsTheme.BgSecondary), radius);
                var tex = string.IsNullOrWhiteSpace(p.avatarUrl)
                    ? null
                    : AbsoluteRP.Social.SocialMediaCache.Get(p.avatarUrl);
                if (tex != null && tex.Width > 0)
                {
                    dl.AddImage(tex.Handle, avatarMin, avatarMax);
                }
                else if (string.IsNullOrWhiteSpace(p.avatarUrl))
                {
                    var hint  = "No avatar";
                    var hintS = ImGui.CalcTextSize(hint);
                    dl.AddText(new Vector2(
                        avatarMin.X + (previewSz - hintS.X) * 0.5f,
                        avatarMin.Y + (previewSz - hintS.Y) * 0.5f),
                        ImGui.ColorConvertFloat4ToU32(RsTheme.TextMuted), hint);
                }
                dl.AddRect(avatarMin, avatarMax,
                    ImGui.ColorConvertFloat4ToU32(RsTheme.Border), radius);

                // Reserve the avatar's footprint so the next widgets line up to its right on the same row.
                ImGui.Dummy(new Vector2(previewSz, previewSz));
                ImGui.SameLine();
                ImGui.BeginGroup();
                var dn = p.displayName ?? string.Empty;
                if (RsElements.InputText("prof_dn", ref dn, 60,
                    placeholder: "Display name", width: 260f))
                {
                    p.displayName = dn;
                    cfg.Save();
                    try { AbsoluteRP.Network.Accounts_DS.SendAccountProfile(p); } catch (Exception ex) { Plugin.PluginLog?.Debug("SendAccountProfile (displayName): " + ex.Message); }
                }
                if (_profileUploading) ImGui.BeginDisabled();
                if (RsElements.Button(
                    string.IsNullOrWhiteSpace(p.avatarUrl) ? "Upload avatar" : "Change avatar",
                    RsElements.ButtonVariant.Ghost))
                {
                    StartAvatarUpload();
                }
                if (_profileUploading) ImGui.EndDisabled();
                if (!string.IsNullOrEmpty(_profileUploadStatus))
                {
                    ImGui.SameLine();
                    var col = _profileUploadStatus.StartsWith("Uploaded")
                        ? RsTheme.AccentSuccess
                        : (_profileUploadStatus.StartsWith("Uploading") ? RsTheme.TextMuted : RsTheme.AccentDanger);
                    ImGui.PushStyleColor(ImGuiCol.Text, col);
                    ImGui.TextUnformatted(_profileUploadStatus);
                    ImGui.PopStyleColor();
                }
                ImGui.EndGroup();
            }
            RsElements.EndPanel();

            ImGui.Spacing();

            if (RsElements.BeginPanel("prof_about", "About", fitContentsX: false, fitContentsY: true))
            {
                var g = p.gender ?? string.Empty;
                if (RsElements.InputText("prof_gender", ref g, 40,
                    placeholder: "Gender", width: 200f))
                {
                    p.gender = g;
                    cfg.Save();
                    try { AbsoluteRP.Network.Accounts_DS.SendAccountProfile(p); } catch (Exception ex) { Plugin.PluginLog?.Debug("SendAccountProfile (gender): " + ex.Message); }
                }
                ImGui.SameLine();

                // Age: RsElements InputText with a string buffer that parses back to an int, so it matches the rest of the theme instead of the raw ImGui.InputInt frame.
                if (_profileAgeBuf == null)
                    _profileAgeBuf = p.age.HasValue ? p.age.Value.ToString() : string.Empty;
                if (RsElements.InputText("prof_age", ref _profileAgeBuf, 4,
                    placeholder: "Age", width: 100f))
                {
                    // Strip non-digits and clamp.
                    var digitsOnly = new StringBuilder();
                    for (int i = 0; i < _profileAgeBuf.Length && digitsOnly.Length < 3; i++)
                        if (char.IsDigit(_profileAgeBuf[i])) digitsOnly.Append(_profileAgeBuf[i]);
                    _profileAgeBuf = digitsOnly.ToString();
                    p.age = int.TryParse(_profileAgeBuf, out var parsed) && parsed > 0 ? parsed : (int?)null;
                    cfg.Save();
                    try { AbsoluteRP.Network.Accounts_DS.SendAccountProfile(p); } catch (Exception ex) { Plugin.PluginLog?.Debug("SendAccountProfile (age): " + ex.Message); }
                }
            }
            RsElements.EndPanel();

            ImGui.Spacing();

            if (RsElements.BeginPanel("prof_rp", "RP preferences", fitContentsX: false, fitContentsY: true))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextWrapped("Pick any that apply — shown on your profile so others know how you like to RP.");
                ImGui.PopStyleColor();
                ImGui.Spacing();

                // Two-column checkbox layout - auto-wraps based on width.
                var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / RsTheme.S(190f)));
                for (int i = 0; i < _rpPrefChoices.Length; i++)
                {
                    var tag = _rpPrefChoices[i];
                    var on  = p.rpPreferences?.Contains(tag) == true;
                    var was = on;
                    if (RsElements.Checkbox(tag + "##rp_" + i, ref on))
                    {
                        p.rpPreferences ??= new List<string>();
                        if (on && !was) p.rpPreferences.Add(tag);
                        else if (!on && was) p.rpPreferences.Remove(tag);
                        cfg.Save();
                        try { AbsoluteRP.Network.Accounts_DS.SendAccountProfile(p); } catch (Exception ex) { Plugin.PluginLog?.Debug("SendAccountProfile (rpPref): " + ex.Message); }
                    }
                    if ((i + 1) % perRow != 0 && i + 1 < _rpPrefChoices.Length) ImGui.SameLine();
                }
            }
            RsElements.EndPanel();

            ImGui.Spacing();

            if (RsElements.BeginPanel("prof_bio", "Bio + header", fitContentsX: false, fitContentsY: true))
            {
                // Non-interactive header preview: shows exactly what other users will see. Framing is edited via the crop popup - opened automatically after upload, or via "Re-crop".
                var availW  = ImGui.GetContentRegionAvail().X;
                var headerH = availW * 0.28f;
                var dl      = ImGui.GetWindowDrawList();
                var hMin    = ImGui.GetCursorScreenPos();
                var hMax    = new Vector2(hMin.X + availW, hMin.Y + headerH);

                dl.AddRectFilled(hMin, hMax,
                    ImGui.ColorConvertFloat4ToU32(RsTheme.BgSecondary), RsTheme.S(6f));
                if (!string.IsNullOrWhiteSpace(p.headerUrl))
                {
                    var htex = AbsoluteRP.Social.SocialMediaCache.Get(p.headerUrl);
                    if (htex != null && htex.Width > 0)
                    {
                        // Aspect-preserving crop via the shared helper so the image keeps its shape here too.
                        var (uv0, uv1) = AbsoluteRP.Social.SocialHeaderCrop.ComputeUv(
                            htex.Width, htex.Height, availW, headerH,
                            p.headerOffsetX, p.headerOffsetY, p.headerZoom);
                        dl.AddImageRounded(htex.Handle, hMin, hMax, uv0, uv1,
                            ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f)),
                            RsTheme.S(6f));
                    }
                }
                dl.AddRect(hMin, hMax,
                    ImGui.ColorConvertFloat4ToU32(RsTheme.Border), RsTheme.S(6f));
                ImGui.Dummy(new Vector2(availW, headerH + RsTheme.S(6f)));

                if (!string.IsNullOrWhiteSpace(p.headerUrl))
                {
                    if (RsElements.Button("Re-crop##hdr_recrop", RsElements.ButtonVariant.Ghost))
                    {
                        AbsoluteRP.Social.SocialHeaderCrop.Open(p.headerUrl, (ox, oy, z) =>
                        {
                            p.headerOffsetX = ox;
                            p.headerOffsetY = oy;
                            p.headerZoom    = z;
                            cfg.Save();
                            try { AbsoluteRP.Network.Accounts_DS.SendAccountProfile(p); }
                            catch (Exception ex) { Plugin.PluginLog?.Debug("SendAccountProfile (headerFrame): " + ex.Message); }
                        });
                    }
                    ImGui.SameLine();
                    if (p.headerOffsetX != 0f || p.headerOffsetY != 0f || p.headerZoom != 1f)
                    {
                        if (RsElements.Button("Reset framing##hdr_reset", RsElements.ButtonVariant.Ghost))
                        {
                            p.headerOffsetX = 0f;
                            p.headerOffsetY = 0f;
                            p.headerZoom    = 1f;
                            cfg.Save();
                            try { AbsoluteRP.Network.Accounts_DS.SendAccountProfile(p); }
                            catch (Exception ex) { Plugin.PluginLog?.Debug("SendAccountProfile (headerReset): " + ex.Message); }
                        }
                        ImGui.SameLine();
                    }
                }

                if (_headerUploading) ImGui.BeginDisabled();
                if (RsElements.Button(
                    string.IsNullOrWhiteSpace(p.headerUrl) ? "Upload header" : "Change header",
                    RsElements.ButtonVariant.Ghost))
                {
                    StartHeaderUpload();
                }
                if (_headerUploading) ImGui.EndDisabled();
                if (!string.IsNullOrEmpty(_headerUploadStatus))
                {
                    ImGui.SameLine();
                    var col = _headerUploadStatus.StartsWith("Uploaded")
                        ? RsTheme.AccentSuccess
                        : (_headerUploadStatus.StartsWith("Uploading") ? RsTheme.TextMuted : RsTheme.AccentDanger);
                    ImGui.PushStyleColor(ImGuiCol.Text, col);
                    ImGui.TextUnformatted(_headerUploadStatus);
                    ImGui.PopStyleColor();
                }

                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextUnformatted("Bio (BBCode formatting supported)");
                ImGui.PopStyleColor();
                var bio = p.bio ?? string.Empty;
                if (RsElements.InputTextArea("prof_bio_body", ref bio, 4000,
                    placeholder: "Tell people about yourself…",
                    size: new Vector2(availW, RsTheme.S(140f))))
                {
                    p.bio = bio;
                    cfg.Save();
                    try { AbsoluteRP.Network.Accounts_DS.SendAccountProfile(p); }
                    catch (Exception ex) { Plugin.PluginLog?.Debug("SendAccountProfile (bio): " + ex.Message); }
                }
            }
            RsElements.EndPanel();
        }

        // Avatar upload - reuses the existing social-media upload path so the file lands in the same server-side asset store as post images. On success the returned URL is written into the account profile and persisted, so the avatar displays immediately.
        private void StartAvatarUpload()
        {
            if (_profileUploading) return;
            var cfg = Plugin.plugin?.Configuration;
            if (cfg?.account == null) return;

            var exts = new[] { "png", "jpg", "jpeg", "webp", "gif" };
            AbsoluteRP.RsUI.RsFileDialog.OpenImagePicker("Pick an avatar image", (ok, path) =>
            {
                if (!ok || string.IsNullOrWhiteSpace(path)) return;
                byte[] bytes;
                string ext;
                try
                {
                    bytes = System.IO.File.ReadAllBytes(path);
                    ext   = System.IO.Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
                }
                catch (Exception ex)
                {
                    _profileUploadStatus = "Read failed: " + ex.Message;
                    return;
                }
                if (bytes.Length == 0)               { _profileUploadStatus = "Empty file";           return; }
                if (Array.IndexOf(exts, ext) < 0)    { _profileUploadStatus = "Unsupported file";     return; }

                _profileUploading    = true;
                _profileUploadStatus = "Uploading…";
                AbsoluteRP.Social.SocialFeed.PendingUploadCallback = (_, url, error) =>
                {
                    _profileUploading = false;
                    if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(url))
                    {
                        _profileUploadStatus = "Upload failed: " + (string.IsNullOrEmpty(error) ? "no url" : error);
                        return;
                    }
                    cfg.account.profile ??= new AbsoluteRP.Defines.AccountProfile();
                    cfg.account.profile.avatarUrl = url;
                    cfg.Save();
                    _profileUploadStatus = "Uploaded";
                    // Server sync: push the new avatar URL alongside the rest of the profile so other players see it.
                    try { AbsoluteRP.Network.Accounts_DS.SendAccountProfile(cfg.account.profile); }
                    catch (Exception ex) { Plugin.PluginLog?.Debug("Send avatar profile: " + ex.Message); }
                };
                _ = AbsoluteRP.Social.SocialFeed.UploadMedia(
                    AbsoluteRP.Social.SocialFeed.MediaKind.Image, ext, bytes);
            }, startPath: null, extensions: exts);
        }

        // Header banner upload - mirrors StartAvatarUpload but writes into headerUrl. Same file-picker + upload path. NOTE: image cropping / positioning UI (frame the header into a fixed aspect and let the user drag the picture inside it) is a planned follow-up - for now the whole picked image becomes the banner and the preview shows exactly how it will render.
        private void StartHeaderUpload()
        {
            if (_headerUploading) return;
            var cfg = Plugin.plugin?.Configuration;
            if (cfg?.account == null) return;

            var exts = new[] { "png", "jpg", "jpeg", "webp", "gif" };
            AbsoluteRP.RsUI.RsFileDialog.OpenImagePicker("Pick a header image", (ok, path) =>
            {
                if (!ok || string.IsNullOrWhiteSpace(path)) return;
                byte[] bytes;
                string ext;
                try
                {
                    bytes = System.IO.File.ReadAllBytes(path);
                    ext   = System.IO.Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
                }
                catch (Exception ex) { _headerUploadStatus = "Read failed: " + ex.Message; return; }
                if (bytes.Length == 0)            { _headerUploadStatus = "Empty file";          return; }
                if (Array.IndexOf(exts, ext) < 0) { _headerUploadStatus = "Unsupported file";    return; }

                _headerUploading    = true;
                _headerUploadStatus = "Uploading…";
                AbsoluteRP.Social.SocialFeed.PendingUploadCallback = (_, url, error) =>
                {
                    _headerUploading = false;
                    if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(url))
                    {
                        _headerUploadStatus = "Upload failed: " + (string.IsNullOrEmpty(error) ? "no url" : error);
                        return;
                    }
                    cfg.account.profile ??= new AbsoluteRP.Defines.AccountProfile();
                    cfg.account.profile.headerUrl     = url;
                    cfg.account.profile.headerOffsetX = 0f;
                    cfg.account.profile.headerOffsetY = 0f;
                    cfg.account.profile.headerZoom    = 1f;
                    cfg.Save();
                    _headerUploadStatus = "Uploaded — position your header";
                    try { AbsoluteRP.Network.Accounts_DS.SendAccountProfile(cfg.account.profile); }
                    catch (Exception ex) { Plugin.PluginLog?.Debug("Send header profile: " + ex.Message); }
                    // Prompt the user to frame the freshly-uploaded image.
                    AbsoluteRP.Social.SocialHeaderCrop.Open(url, (ox, oy, z) =>
                    {
                        cfg.account.profile.headerOffsetX = ox;
                        cfg.account.profile.headerOffsetY = oy;
                        cfg.account.profile.headerZoom    = z;
                        cfg.Save();
                        try { AbsoluteRP.Network.Accounts_DS.SendAccountProfile(cfg.account.profile); }
                        catch (Exception ex) { Plugin.PluginLog?.Debug("Send header profile (crop): " + ex.Message); }
                    });
                };
                _ = AbsoluteRP.Social.SocialFeed.UploadMedia(
                    AbsoluteRP.Social.SocialFeed.MediaKind.Image, ext, bytes);
            }, startPath: null, extensions: exts);
        }

        public void Dispose() { }
    }
}
