using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using AbsoluteRP.RsUI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AbsoluteRP.Social;

// WYSIWYG rich-text composer used by SocialPage for authoring posts. Text, images, and videos live in a single flat segment list; the caret, selection, formatting toggles, and BBCode serialization all operate on that list. We draw everything through the window's draw list rather than wrapping ImGui.InputText so styled runs, inline media, and per-char selection highlights can share the same layout.
public static class SocialComposer
{
    // Public state
    public static bool IsFocused { get; private set; }

    // Model
    private enum SegmentKind { Text, Image, Video }

    public enum TextAlign { Left, Center, Right }

    private struct Segment
    {
        public SegmentKind Kind;
        public char        Ch;
        public string      Url;
        public bool        Bold;
        public bool        Italic;
        public bool        Underline;
        public uint        ColorRgba;
        public int         SizePt;
        // Alignment applies to whole paragraphs (line-separated blocks for text; the single-segment "line" for media). Every segment in a paragraph carries the same value; ApplyAlign propagates across the current paragraph range.
        public TextAlign   Align;
        // Font family id from SocialComposerFonts.Families. Empty string means "default" (whatever SocialComposerFonts falls back to).
        public string      FontFamily;
    }

    private struct PendingStyle
    {
        public bool      Bold;
        public bool      Italic;
        public bool      Underline;
        public uint      ColorRgba;
        public int       SizePt;
        public TextAlign Align;
        public string    FontFamily;
    }

    private static readonly List<Segment> _doc = new();
    private static int  _cursor;
    private static int  _selStart = -1;
    private static int  _selEnd   = -1;
    private static PendingStyle _pendingStyle;

    // Focus / drag state.
    private static uint _widgetId;
    private static bool _wantFocus;
    private static bool _dragging;
    private static int  _dragAnchor;
    private static double _lastClickTime;
    private static Vector2 _lastClickPos;

    // Rolling buffer for the hidden capture InputText - ImGui only fills its character queue reliably when an InputText is the active widget, so we host one behind the composer and consume its typed chars via a filter callback that returns 1 (discard). The buffer itself never shows and never grows: the callback writes into our doc directly.
    private static string _captureBuf = string.Empty;
    // 2-frame counter: on click frame we set to 2, DrawKeyboardCapture decrements each frame and calls SetKeyboardFocusHere while > 0. Two attempts because ImGui usually resolves the focus flag on the. NEXT frame (when the freshly-clicked InvisibleButton is no longer fighting for active-widget status), and we want a redundant grab in case the first tick was absorbed by that conflict.
    private static int    _captureFocusFrames;
    // Last _cursor value we auto-scrolled for. Comparing lets us fire
    // the scroll adjust on any caret movement (arrow keys, click, edit)
    // without doing the work every idle frame.
    private static int    _lastScrollCursor = -1;
    // Last _cursor value we synced pending-style from - mirrors the caret's context into pending so toolbar toggles reflect the current position, without clobbering a manual toggle that hasn't been "committed" by moving the caret.
    private static int    _lastPendingSyncCursor = -1;

    // Per-frame layout. Rebuilt every draw so hit-testing, caret, and selection all see the same geometry.
    private struct GlyphBox
    {
        public int     Index;      // segment index in _doc
        public float   X;
        public float   Y;
        public float   W;
        public float   H;
        public bool    IsMedia;
        public bool    IsNewline;
    }
    private struct VisualLine
    {
        public int   FirstGlyph;
        public int   Count;
        public float Y;
        public float Height;
    }

    private static readonly List<GlyphBox>   _glyphs = new();
    private static readonly List<VisualLine> _lines  = new();

    // Public API

    public static void Focus() => _wantFocus = true;

    public static void Clear()
    {
        _doc.Clear();
        _cursor = 0;
        _selStart = _selEnd = -1;
        _pendingStyle = default;
    }

    public static void InsertImage(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        DeleteSelectionIfAny();
        _doc.Insert(_cursor, new Segment { Kind = SegmentKind.Image, Url = url, Align = _pendingStyle.Align });
        _cursor++;
    }

    public static void InsertVideo(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        DeleteSelectionIfAny();
        _doc.Insert(_cursor, new Segment { Kind = SegmentKind.Video, Url = url, Align = _pendingStyle.Align });
        _cursor++;
    }

    // Wraps the selection (or inserts an empty pair at the caret) in literal tag text, e.g. [spoiler]...[/spoiler]. Tag chars carry no inline styling so the serialized BBCode keeps them outside style runs; the caret lands inside an empty pair / after the closing tag.
    public static void WrapSelection(string open, string close)
    {
        int a, b;
        bool sel = HasSelection();
        if (sel) (a, b) = OrderedSelection();
        else     (a, b) = (_cursor, _cursor);
        a = Math.Clamp(a, 0, _doc.Count); b = Math.Clamp(b, 0, _doc.Count);
        TextAlign AlignAt(int idx) => _doc.Count == 0 ? _pendingStyle.Align
            : _doc[Math.Clamp(idx, 0, _doc.Count - 1)].Align;
        var alB = AlignAt(b > a ? b - 1 : b);
        var alA = AlignAt(a);
        for (int k = 0; k < close.Length; k++)
            _doc.Insert(b + k, new Segment { Kind = SegmentKind.Text, Ch = close[k], Align = alB, FontFamily = string.Empty });
        for (int k = 0; k < open.Length; k++)
            _doc.Insert(a + k, new Segment { Kind = SegmentKind.Text, Ch = open[k], Align = alA, FontFamily = string.Empty });
        _selStart = _selEnd = -1;
        _cursor = sel ? b + open.Length + close.Length : a + open.Length;
        _wantFocus = true;
    }

    public static void ToggleBold()      => ToggleFlag(s => s.Bold,      (ref Segment s, bool v) => s.Bold = v,      ref _pendingStyle.Bold);
    public static void ToggleItalic()    => ToggleFlag(s => s.Italic,    (ref Segment s, bool v) => s.Italic = v,    ref _pendingStyle.Italic);
    public static void ToggleUnderline() => ToggleFlag(s => s.Underline, (ref Segment s, bool v) => s.Underline = v, ref _pendingStyle.Underline);

    // Apply an alignment across the paragraph the cursor is in (or all paragraphs touched by the current selection). A paragraph is a run between newlines / media boundaries; every segment in one shares the same Align. Media segments each count as their own paragraph. The pending-style Align is DELIBERATELY not touched here - that would carry the new alignment onto every future line the user types after pressing Enter, which is exactly the "leaks into lines below" behaviour we want to avoid.
    public static void ApplyAlign(TextAlign align)
    {
        int a, b;
        if (HasSelection()) (a, b) = OrderedSelection();
        else                (a, b) = (_cursor, _cursor);
        // Expand backward to previous paragraph boundary.
        while (a > 0)
        {
            var prev = _doc[a - 1];
            if (prev.Kind != SegmentKind.Text || prev.Ch == '\n') break;
            a--;
        }
        // Expand forward to next paragraph boundary.
        while (b < _doc.Count)
        {
            var s = _doc[b];
            b++;
            if (s.Kind != SegmentKind.Text || s.Ch == '\n') break;
        }
        for (int i = a; i < b; i++)
        {
            var s = _doc[i]; s.Align = align; _doc[i] = s;
        }
    }

    // Effective-style queries used by the toolbar to highlight the currently-active buttons. Returns the value that WOULD apply to a freshly-typed char, or (for selections) the majority value.
    public static bool      CurrentBold()      => CurrentBool(s => s.Bold, _pendingStyle.Bold);
    public static bool      CurrentItalic()    => CurrentBool(s => s.Italic, _pendingStyle.Italic);
    public static bool      CurrentUnderline() => CurrentBool(s => s.Underline, _pendingStyle.Underline);
    public static TextAlign CurrentAlign()
    {
        if (HasSelection())
        {
            var idx = OrderedSelection().Item1;
            if (idx > 0 && idx <= _doc.Count) return _doc[idx - 1].Align;
        }
        return _pendingStyle.Align;
    }
    public static int CurrentSize()
    {
        if (HasSelection())
        {
            var (a, b) = OrderedSelection();
            for (int i = a; i < b; i++)
                if (_doc[i].Kind == SegmentKind.Text && _doc[i].SizePt > 0) return _doc[i].SizePt;
        }
        return _pendingStyle.SizePt > 0 ? _pendingStyle.SizePt : 12;
    }

    public static void ApplyFontFamily(string familyId)
    {
        var f = familyId ?? string.Empty;
        _pendingStyle.FontFamily = f;
        if (HasSelection())
        {
            var (a, b) = OrderedSelection();
            for (int i = a; i < b; i++)
            {
                if (_doc[i].Kind != SegmentKind.Text) continue;
                var s = _doc[i]; s.FontFamily = f; _doc[i] = s;
            }
        }
    }

    public static string CurrentFontFamily()
    {
        if (HasSelection())
        {
            var (a, b) = OrderedSelection();
            for (int i = a; i < b; i++)
                if (_doc[i].Kind == SegmentKind.Text && !string.IsNullOrEmpty(_doc[i].FontFamily))
                    return _doc[i].FontFamily;
        }
        return _pendingStyle.FontFamily ?? string.Empty;
    }

    private static bool CurrentBool(Func<Segment, bool> read, bool pending)
    {
        if (HasSelection())
        {
            var (a, b) = OrderedSelection();
            for (int i = a; i < b; i++)
                if (_doc[i].Kind == SegmentKind.Text && !read(_doc[i])) return false;
            return b > a;
        }
        // For an empty selection we return pending - the per-frame sync in Draw copies the prev char's state INTO pending when the caret moves, so pending is always the authoritative "what will apply next". This also honours a manual toggle the user just made (which flips pending directly).
        return pending;
    }

    public static void ApplyColor(uint rgba)
    {
        if (HasSelection())
        {
            var (a, b) = OrderedSelection();
            for (int i = a; i < b; i++)
            {
                if (_doc[i].Kind != SegmentKind.Text) continue;
                var s = _doc[i]; s.ColorRgba = rgba; _doc[i] = s;
            }
        }
        else _pendingStyle.ColorRgba = rgba;
    }

    public static void ApplySize(int pt)
    {
        if (HasSelection())
        {
            var (a, b) = OrderedSelection();
            for (int i = a; i < b; i++)
            {
                if (_doc[i].Kind != SegmentKind.Text) continue;
                var s = _doc[i]; s.SizePt = pt; _doc[i] = s;
            }
        }
        else _pendingStyle.SizePt = pt;
    }

    public static void ClearFormatting()
    {
        if (HasSelection())
        {
            var (a, b) = OrderedSelection();
            for (int i = a; i < b; i++)
            {
                if (_doc[i].Kind != SegmentKind.Text) continue;
                var s = _doc[i];
                s.Bold = s.Italic = s.Underline = false;
                s.ColorRgba = 0;
                s.SizePt    = 0;
                _doc[i] = s;
            }
        }
        else _pendingStyle = default;
    }

    // BBCode serialization

    public static string ToBBCode()
    {
        var sb = new StringBuilder();
        int i = 0;
        while (i < _doc.Count)
        {
            var seg = _doc[i];
            if (seg.Kind == SegmentKind.Image)
            {
                if (sb.Length > 0 && sb[sb.Length - 1] != '\n') sb.Append('\n');
                AppendAlignOpen(sb, seg.Align);
                sb.Append("[img]").Append(seg.Url).Append("[/img]");
                AppendAlignClose(sb, seg.Align);
                sb.Append('\n');
                i++;
                continue;
            }
            if (seg.Kind == SegmentKind.Video)
            {
                if (sb.Length > 0 && sb[sb.Length - 1] != '\n') sb.Append('\n');
                AppendAlignOpen(sb, seg.Align);
                sb.Append("[video]").Append(seg.Url).Append("[/video]");
                AppendAlignClose(sb, seg.Align);
                sb.Append('\n');
                i++;
                continue;
            }

            // Coalesce consecutive text segments sharing style.
            int j = i;
            while (j < _doc.Count && _doc[j].Kind == SegmentKind.Text && SameStyle(_doc[j], seg))
                j++;

            var open = new StringBuilder();
            var close = new StringBuilder();
            AppendAlignOpen(open, seg.Align);
            if (seg.Align != TextAlign.Left) close.Insert(0, "[/align]");
            if (!string.IsNullOrEmpty(seg.FontFamily))
            {
                open.Append("[font=").Append(seg.FontFamily).Append(']');
                close.Insert(0, "[/font]");
            }
            if (seg.SizePt > 0) { open.Append("[size=").Append(seg.SizePt).Append(']'); close.Insert(0, "[/size]"); }
            if (seg.ColorRgba != 0)
            {
                var r = (seg.ColorRgba >> 24) & 0xFF;
                var g = (seg.ColorRgba >> 16) & 0xFF;
                var b = (seg.ColorRgba >>  8) & 0xFF;
                open.Append("[color=#").Append(r.ToString("X2")).Append(g.ToString("X2")).Append(b.ToString("X2")).Append(']');
                close.Insert(0, "[/color]");
            }
            if (seg.Underline) { open.Append("[u]"); close.Insert(0, "[/u]"); }
            if (seg.Italic)    { open.Append("[i]"); close.Insert(0, "[/i]"); }
            if (seg.Bold)      { open.Append("[b]"); close.Insert(0, "[/b]"); }

            sb.Append(open);
            for (int k = i; k < j; k++) sb.Append(_doc[k].Ch);
            sb.Append(close);
            i = j;
        }
        return sb.ToString();
    }

    public static void LoadFromBBCode(string src)
    {
        Clear();
        if (string.IsNullOrEmpty(src)) return;

        bool bold = false, italic = false, underline = false;
        uint color = 0;
        int  size  = 0;
        var  align = TextAlign.Left;
        var  font  = string.Empty;

        int i = 0;
        while (i < src.Length)
        {
            char c = src[i];
            if (c == '[')
            {
                int close = src.IndexOf(']', i + 1);
                if (close > 0)
                {
                    var raw   = src.Substring(i + 1, close - i - 1).Trim();
                    var lower = raw.ToLowerInvariant();
                    bool isClose = lower.StartsWith("/");
                    string name;
                    string? val = null;
                    if (isClose) name = lower.Substring(1).Trim();
                    else
                    {
                        var eq = lower.IndexOf('=');
                        if (eq > 0) { name = lower.Substring(0, eq).Trim(); val = raw.Substring(eq + 1).Trim(); }
                        else        { name = lower; }
                    }

                    if (name == "img" || name == "video")
                    {
                        if (!isClose)
                        {
                            var endTag = "[/" + name + "]";
                            var endIdx = src.IndexOf(endTag, close + 1, StringComparison.OrdinalIgnoreCase);
                            if (endIdx > 0)
                            {
                                var url = src.Substring(close + 1, endIdx - close - 1).Trim();
                                _doc.Add(new Segment { Kind = name == "img" ? SegmentKind.Image : SegmentKind.Video, Url = url, Align = align });
                                i = endIdx + endTag.Length;
                                continue;
                            }
                        }
                    }
                    else if (name is "b" or "i" or "u" or "color" or "size" or "align" or "font")
                    {
                        switch (name)
                        {
                            case "b": bold      = !isClose; break;
                            case "i": italic    = !isClose; break;
                            case "u": underline = !isClose; break;
                            case "color":
                                if (isClose) color = 0;
                                else color = ParseHexColor(val);
                                break;
                            case "size":
                                if (isClose) size = 0;
                                else if (int.TryParse(val, out var pt)) size = Math.Clamp(pt, 8, 48);
                                break;
                            case "align":
                                if (isClose) align = TextAlign.Left;
                                else align = val == "center" ? TextAlign.Center
                                            : val == "right"  ? TextAlign.Right
                                                              : TextAlign.Left;
                                break;
                            case "font":
                                font = isClose ? string.Empty : (val ?? string.Empty);
                                break;
                        }
                        i = close + 1;
                        continue;
                    }
                }
            }

            // Fall through: literal char.
            _doc.Add(new Segment
            {
                Kind = SegmentKind.Text,
                Ch = c,
                Bold = bold, Italic = italic, Underline = underline,
                ColorRgba = color, SizePt = size, Align = align, FontFamily = font,
            });
            i++;
        }
        _cursor = _doc.Count;
    }

    // Draw

    public static bool Draw(string id, Vector2 size)
    {
        var outerOrigin = ImGui.GetCursorScreenPos();
        var outerMin    = outerOrigin;
        var outerMax    = outerOrigin + size;

        _widgetId = ImGui.GetID("##rs_composer_" + id);

        // Chrome drawn on the parent's draw list so it never gets clipped by the child window that hosts the scrollable content.
        var parentDl = ImGui.GetWindowDrawList();
        var focusGlow = IsFocused ? 1f : 0f;
        var border    = LerpV4(RsTheme.Border, RsTheme.AccentPrimary, focusGlow);
        parentDl.AddRectFilled(outerMin, outerMax, Pack(RsTheme.BgTertiary), RsTheme.S(6f));
        parentDl.AddRect(outerMin, outerMax, Pack(border), RsTheme.S(6f), ImDrawFlags.None, RsTheme.BorderThickness + focusGlow);

        // Scroll host. Zero the child's own WindowPadding so we can place the content start ourselves via SetCursorScreenPos below - that way the "1 blank line + 2 char indent" spec holds regardless of any theme padding defaults, and the padding is measured in the parent's font metrics (which is what the layout will use too).
        var pad          = RsTheme.S(10f);
        var charW        = MathF.Max(4f, ImGui.CalcTextSize("M").X);
        var lineH        = ImGui.GetTextLineHeightWithSpacing();
        var padLeft      = MathF.Max(pad, charW * 2f);
        var padTop       = MathF.Max(pad, lineH);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,   new Vector2(0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg,            new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg,        new Vector4(0f, 0f, 0f, 0f));
        var beganChild = ImGui.BeginChild("##rs_composer_body_" + id, size, false,
                                          ImGuiWindowFlags.NoBackground);
        var changed = false;
        if (beganChild)
        {
            var dl         = ImGui.GetWindowDrawList();
            // Explicit inset: one full blank line above, two char widths in from the left. WindowPadding was zeroed above so this is the authoritative content start.
            var childOrig  = ImGui.GetCursorScreenPos();
            var contentMin = new Vector2(childOrig.X + padLeft, childOrig.Y + padTop);
            ImGui.SetCursorScreenPos(contentMin);
            var wrapWidth  = MathF.Max(1f, size.X - padLeft - pad);

            BuildLayout(contentMin, wrapWidth);

            // Reserve the full content height so the child window's scrollbar can reach the bottom of the doc.
            var lastLine = _lines.Count > 0 ? _lines[_lines.Count - 1] : default;
            var contentH = _lines.Count > 0
                ? (lastLine.Y + lastLine.Height - contentMin.Y)
                : ImGui.GetTextLineHeight();
            var hitSize  = new Vector2(wrapWidth, MathF.Max(contentH, ImGui.GetContentRegionAvail().Y));

            // Hit surface inside the child so hover + click both honour the current scroll offset - clicks translate to caret hits in the same absolute-screen-coords the layout used.
            ImGui.SetCursorScreenPos(contentMin);
            var hit = ImGui.InvisibleButton("##rs_composer_hit_" + id, hitSize);
            ImGui.SetItemAllowOverlap();
            var hovered = ImGui.IsItemHovered();

            if (_wantFocus) { IsFocused = true; _wantFocus = false; _captureFocusFrames = 2; }
            var pressedThisFrame = hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
            var clicked = hit || pressedThisFrame;
            if (clicked)
            {
                IsFocused = true;
                _captureFocusFrames = 2;
                var mp = ImGui.GetMousePos();
                var newCursor = HitTest(mp);
                var t = ImGui.GetTime();
                var doubleClick = ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)
                    || (t - _lastClickTime < 0.35 && (mp - _lastClickPos).LengthSquared() < 16f);
                if (doubleClick)
                {
                    SelectWordAt(newCursor);
                    _dragging = false;
                }
                else
                {
                    _cursor = newCursor;
                    _selStart = _selEnd = _cursor;
                    _dragging = true;
                    _dragAnchor = _cursor;
                }
                _lastClickTime = t;
                _lastClickPos  = mp;
            }
            else if (IsFocused && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !hovered)
            {
                IsFocused = false;
                _dragging = false;
            }

            if (_dragging)
            {
                if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    var mp = ImGui.GetMousePos();
                    var c  = HitTest(mp);
                    _cursor  = c;
                    _selStart = Math.Min(_dragAnchor, c);
                    _selEnd   = Math.Max(_dragAnchor, c);
                }
                else _dragging = false;
            }

            if (IsFocused) changed = HandleInput();
            if (changed) BuildLayout(contentMin, wrapWidth);

            // Sync the pending style bits to whatever segment the caret is sitting on. This way Current* queries reflect the caret's actual context so the toolbar toggles always show the true state - click into bold text and the B button lights up automatically. Only runs when the caret moved, to avoid clobbering a manual toggle the user just made.
            if (IsFocused && !HasSelection() && _cursor != _lastPendingSyncCursor)
            {
                _lastPendingSyncCursor = _cursor;
                if (_cursor > 0 && _cursor <= _doc.Count)
                {
                    var prev = _doc[_cursor - 1];
                    if (prev.Kind == SegmentKind.Text)
                    {
                        _pendingStyle.Bold       = prev.Bold;
                        _pendingStyle.Italic     = prev.Italic;
                        _pendingStyle.Underline  = prev.Underline;
                        _pendingStyle.ColorRgba  = prev.ColorRgba;
                        _pendingStyle.SizePt     = prev.SizePt;
                        _pendingStyle.Align      = prev.Align;
                        _pendingStyle.FontFamily = prev.FontFamily ?? string.Empty;
                    }
                }
            }

            // Selection highlight.
            if (HasSelection())
            {
                var (a, b) = OrderedSelection();
                var accent = RsTheme.AccentPrimary;
                var selCol = Pack(new Vector4(accent.X, accent.Y, accent.Z, 0.35f));
                for (int i = 0; i < _glyphs.Count; i++)
                {
                    var g = _glyphs[i];
                    if (g.Index < a || g.Index >= b) continue;
                    if (g.IsNewline)
                        dl.AddRectFilled(new Vector2(g.X, g.Y), new Vector2(g.X + RsTheme.S(4f), g.Y + g.H), selCol);
                    else
                        dl.AddRectFilled(new Vector2(g.X, g.Y), new Vector2(g.X + g.W, g.Y + g.H), selCol);
                }
            }

            DrawGlyphs(dl);

            if (_doc.Count == 0)
                dl.AddText(contentMin, Pack(RsTheme.TextMuted), "Write your post...");

            if (IsFocused && !HasSelection())
            {
                var blink = ((int)(ImGui.GetTime() * 1000.0 / 500.0)) % 2 == 0;
                if (blink)
                {
                    var (cx, cy, ch) = CaretPosition(contentMin);
                    // Nudge 1px right so the 2px caret line stays fully inside the visible content area when the cursor is at column 0 - otherwise anti-aliasing puts half the stroke in the padding gutter and it reads as invisible.
                    dl.AddLine(new Vector2(cx + 1f, cy), new Vector2(cx + 1f, cy + ch),
                               Pack(RsTheme.AccentPrimary), RsTheme.S(2f));
                }
            }

            // Auto-scroll to keep the caret in view. Fires on ANY caret movement - typing, arrow keys, click positioning - so the last line is always fully reachable rather than clipped one row short at the bottom.
            if (IsFocused && _cursor != _lastScrollCursor)
            {
                _lastScrollCursor = _cursor;
                var (_, caretY, caretH) = CaretPosition(contentMin);
                var viewMinY = ImGui.GetWindowPos().Y;
                var viewMaxY = viewMinY + ImGui.GetWindowSize().Y;
                if (caretY < viewMinY + pad)
                    ImGui.SetScrollY(ImGui.GetScrollY() - (viewMinY + pad - caretY));
                else if (caretY + caretH > viewMaxY - pad)
                    ImGui.SetScrollY(ImGui.GetScrollY() + (caretY + caretH - (viewMaxY - pad)));
            }
        }
        ImGui.EndChild();
        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar();

        // Sink InputText for keyboard capture, drawn OUTSIDE the child so its own layout doesn't push the child around.
        DrawKeyboardCapture(id);
        return changed;
    }

    // Layout

    private static void BuildLayout(Vector2 contentMin, float wrapWidth)
    {
        _glyphs.Clear();
        _lines.Clear();

        var fontScale = ImGui.GetIO().FontGlobalScale;
        var lineH     = ImGui.GetTextLineHeightWithSpacing();
        var baseH     = ImGui.GetTextLineHeight();
        float x = contentMin.X;
        float y = contentMin.Y;
        int   lineStart = 0;
        float lineHeight = baseH;

        void FlushLine()
        {
            // Baseline pass: align smaller glyphs so their visual baseline sits on the same row as the tallest glyph's baseline. The full line-height box overshoots - text renders inside a box that's roughly [ascent + descent] tall but the baseline is near the ascent from the top, not the bottom. AscentRatio is that fraction: shift = (maxH - h) * AscentRatio lands the small glyph so its baseline == big glyph's baseline.
            var count0 = _glyphs.Count - lineStart;
            if (count0 > 0)
            {
                float maxH = 0f;
                for (int gi = lineStart; gi < _glyphs.Count; gi++)
                {
                    var gh = _glyphs[gi].H;
                    if (gh > maxH) maxH = gh;
                }
                var fnt = ImGui.GetFont();
                float ascentRatio;
                try
                {
                    var asc = fnt.Ascent;
                    var desc = MathF.Abs(fnt.Descent);
                    var total = asc + desc;
                    ascentRatio = total > 0.001f ? asc / total : 0.78f;
                }
                catch { ascentRatio = 0.78f; }
                for (int gi = lineStart; gi < _glyphs.Count; gi++)
                {
                    var g = _glyphs[gi];
                    if (g.IsMedia) continue;
                    var dy = (maxH - g.H) * ascentRatio;
                    if (dy > 0f) { g.Y += dy; _glyphs[gi] = g; }
                }
            }
            // Post-adjust every text glyph on the line by the paragraph's alignment. Uses the first text glyph's Align on the line as the paragraph value - since ApplyAlign propagates it across the whole paragraph, this is consistent.
            var count = _glyphs.Count - lineStart;
            if (count > 0)
            {
                TextAlign lineAlign = TextAlign.Left;
                for (int gi = lineStart; gi < _glyphs.Count; gi++)
                {
                    var gseg = _doc[_glyphs[gi].Index];
                    if (gseg.Kind == SegmentKind.Text) { lineAlign = gseg.Align; break; }
                }
                if (lineAlign != TextAlign.Left && count > 0 && !_glyphs[lineStart].IsMedia)
                {
                    var lastG    = _glyphs[_glyphs.Count - 1];
                    var lineRight = lastG.X + lastG.W;
                    var used      = lineRight - contentMin.X;
                    var free      = wrapWidth - used;
                    var shift     = lineAlign == TextAlign.Center ? free * 0.5f : free;
                    if (shift > 0f)
                    {
                        for (int gi = lineStart; gi < _glyphs.Count; gi++)
                        {
                            var g = _glyphs[gi];
                            g.X += shift;
                            _glyphs[gi] = g;
                        }
                    }
                }
            }
            _lines.Add(new VisualLine
            {
                FirstGlyph = lineStart,
                Count      = _glyphs.Count - lineStart,
                Y          = y,
                Height     = lineHeight,
            });
            y         += lineHeight + (lineH - baseH);
            x         = contentMin.X;
            lineStart = _glyphs.Count;
            lineHeight = baseH;
        }

        for (int i = 0; i < _doc.Count; i++)
        {
            var seg = _doc[i];
            if (seg.Kind == SegmentKind.Image || seg.Kind == SegmentKind.Video)
            {
                // Media takes its own line.
                if (_glyphs.Count > lineStart) FlushLine();

                float boxW, boxH;
                if (seg.Kind == SegmentKind.Video)
                {
                    boxW = MathF.Min(wrapWidth, 300f * fontScale);
                    boxH = 140f * fontScale;
                }
                else
                {
                    boxW = MathF.Min(wrapWidth, 300f * fontScale);
                    boxH = 200f * fontScale;
                    var tex = SocialMediaCache.Get(seg.Url);
                    if (tex != null && tex.Width > 0)
                    {
                        var scale = boxW / tex.Width;
                        if (scale > 1f) scale = 1f;
                        boxH = tex.Height * scale;
                    }
                }
                float mx;
                switch (seg.Align)
                {
                    case TextAlign.Left:   mx = contentMin.X; break;
                    case TextAlign.Right:  mx = contentMin.X + (wrapWidth - boxW); break;
                    default:               mx = contentMin.X + (wrapWidth - boxW) * 0.5f; break;
                }
                _glyphs.Add(new GlyphBox
                {
                    Index = i, X = mx, Y = y, W = boxW, H = boxH, IsMedia = true,
                });
                lineHeight = MathF.Max(lineHeight, boxH);
                FlushLine();
                continue;
            }

            // Text.
            char c = seg.Ch;
            if (c == '\n')
            {
                _glyphs.Add(new GlyphBox
                {
                    Index = i, X = x, Y = y, W = 0f, H = lineHeight, IsNewline = true,
                });
                FlushLine();
                continue;
            }

            var sizeMul = SizeScale(seg.SizePt);
            float w;
            {
                var s = c.ToString();
                // Measure with the segment's font active so wider glyphs (Georgia, monospace, etc.) get the right hit boxes.
                using (PushSegmentFont(seg))
                {
                    w = ImGui.CalcTextSize(s).X * sizeMul;
                }
            }
            if (seg.Bold) w += 1f;
            var glyphH = baseH * sizeMul;

            if (x - contentMin.X + w > wrapWidth && _glyphs.Count > lineStart)
                FlushLine();

            _glyphs.Add(new GlyphBox
            {
                Index = i, X = x, Y = y, W = w, H = glyphH, IsMedia = false,
            });
            x += w;
            lineHeight = MathF.Max(lineHeight, glyphH);
        }
        // Trailing line (also covers empty doc). Route through FlushLine so alignment shifting applies here too.
        FlushLine();
    }

    private static void DrawGlyphs(ImDrawListPtr dl)
    {
        for (int li = 0; li < _lines.Count; li++)
        {
            var line = _lines[li];
            // Group consecutive text glyphs sharing style into runs.
            int i = 0;
            while (i < line.Count)
            {
                var g = _glyphs[line.FirstGlyph + i];
                if (g.IsMedia)
                {
                    DrawMedia(dl, g);
                    i++;
                    continue;
                }
                if (g.IsNewline) { i++; continue; }

                int j = i + 1;
                var s0 = _doc[g.Index];
                while (j < line.Count)
                {
                    var gj = _glyphs[line.FirstGlyph + j];
                    if (gj.IsMedia || gj.IsNewline) break;
                    var sj = _doc[gj.Index];
                    if (!SameStyle(s0, sj)) break;
                    j++;
                }

                // Build the run text.
                var run = new StringBuilder();
                for (int k = i; k < j; k++)
                {
                    var gk = _glyphs[line.FirstGlyph + k];
                    run.Append(_doc[gk.Index].Ch);
                }
                var runText = run.ToString();

                var color = s0.ColorRgba != 0
                    ? RgbaToPack(s0.ColorRgba)
                    : Pack(RsTheme.TextPrimary);

                var pos = new Vector2(g.X, g.Y);
                var runScale = SizeScale(s0.SizePt);
                // Push the segment's family+style font for both measurement fallbacks and rendering. When a real italic face is active, the tint hack below stays off; if push failed (no font loaded), keep the italic tint so users still see SOME differentiation.
                using (var fs = PushSegmentFont(s0))
                {
                    var effectiveColor = color;
                    if (s0.Italic && !fs.Pushed)
                    {
                        var v = UnpackToVec(effectiveColor);
                        v = new Vector4(v.X * 0.92f, v.Y * 0.98f, MathF.Min(1f, v.Z * 1.08f), v.W);
                        effectiveColor = Pack(v);
                    }
                    if (runScale != 1f)
                    {
                        var fnt   = ImGui.GetFont();
                        var pxSz  = ImGui.GetFontSize() * runScale;
                        dl.AddText(fnt, pxSz, pos, effectiveColor, runText, 0f);
                        if (s0.Bold && !fs.Pushed)
                            dl.AddText(fnt, pxSz, new Vector2(pos.X + 1f, pos.Y), effectiveColor, runText, 0f);
                    }
                    else
                    {
                        dl.AddText(pos, effectiveColor, runText);
                        if (s0.Bold && !fs.Pushed)
                            dl.AddText(new Vector2(pos.X + 1f, pos.Y), effectiveColor, runText);
                    }
                    if (s0.Underline)
                    {
                        var lastG = _glyphs[line.FirstGlyph + j - 1];
                        var y2    = pos.Y + g.H - 1f;
                        dl.AddLine(new Vector2(pos.X, y2), new Vector2(lastG.X + lastG.W, y2), effectiveColor, 1f);
                    }
                }
                i = j;
            }
        }
    }

    private static void DrawMedia(ImDrawListPtr dl, GlyphBox g)
    {
        var seg = _doc[g.Index];
        var min = new Vector2(g.X, g.Y);
        var max = new Vector2(g.X + g.W, g.Y + g.H);
        var radius = RsTheme.S(6f);

        if (seg.Kind == SegmentKind.Image)
        {
            var tex = SocialMediaCache.Get(seg.Url);
            if (tex != null)
            {
                dl.AddImage(tex.Handle, min, max);
                dl.AddRect(min, max, Pack(RsTheme.Border), radius, ImDrawFlags.None, RsTheme.BorderThickness);
            }
            else
            {
                dl.AddRectFilled(min, max, Pack(RsTheme.BgSecondary), radius);
                dl.AddRect(min, max, Pack(RsTheme.Border), radius, ImDrawFlags.None, RsTheme.BorderThickness);
                var label = "loading image...";
                var ls = ImGui.CalcTextSize(label);
                dl.AddText(new Vector2(min.X + (g.W - ls.X) * 0.5f, min.Y + (g.H - ls.Y) * 0.5f),
                           Pack(RsTheme.TextMuted), label);
            }
            return;
        }

        // Video.
        dl.AddRectFilled(min, max, Pack(new Vector4(0f, 0f, 0f, 1f)), radius);
        if (SocialVideoThumbCache.TryGet(seg.Url, out var thumb, out var vw, out var vh) && vw > 0 && vh > 0)
        {
            var scale = MathF.Min(g.W / vw, g.H / vh);
            var dw = vw * scale;
            var dh = vh * scale;
            var iMin = new Vector2(min.X + (g.W - dw) * 0.5f, min.Y + (g.H - dh) * 0.5f);
            var iMax = new Vector2(iMin.X + dw, iMin.Y + dh);
            dl.AddImage(new ImTextureID(thumb), iMin, iMax);
        }
        else
        {
            var label = "loading preview...";
            var ls = ImGui.CalcTextSize(label);
            dl.AddText(new Vector2(min.X + (g.W - ls.X) * 0.5f, min.Y + (g.H - ls.Y) * 0.5f),
                       Pack(RsTheme.TextMuted), label);
        }
        dl.AddRect(min, max, Pack(RsTheme.Border), radius, ImDrawFlags.None, RsTheme.BorderThickness);

        // Play badge.
        var badgeR = RsTheme.S(20f);
        var badgeC = new Vector2(min.X + g.W * 0.5f, min.Y + g.H * 0.5f);
        dl.AddCircleFilled(badgeC, badgeR, Pack(new Vector4(0f, 0f, 0f, 0.55f)));
        using (RsIcons.Push())
        {
            var glyph = FontAwesomeIcon.Play.ToIconString();
            var gs = ImGui.CalcTextSize(glyph);
            dl.AddText(new Vector2(badgeC.X - gs.X * 0.5f + RsTheme.S(2f), badgeC.Y - gs.Y * 0.5f),
                       Pack(RsTheme.TextPrimary), glyph);
        }
    }

    // Input

    private static bool HandleInput()
    {
        var io      = ImGui.GetIO();
        var ctrl    = io.KeyCtrl;
        var shift   = io.KeyShift;
        var changed = false;

        // Character input arrives via DrawKeyboardCapture's InputText. CharFilter callback (which invokes InsertChar directly), NOT through io.InputQueueCharacters - the sink widget consumes chars before they reach the queue. `changed` isn't set here because the callback runs mid-frame and layout will rebuild next frame regardless; keyboard events for edit commands (backspace etc.) continue to route through the block below.

        if (ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter))
        {
            InsertChar('\n');
            changed = true;
            // Single-line sink InputText defocuses itself on Enter; grab focus back next frame so the caret keeps blinking and the. NEXT keystroke isn't dropped.
            _captureFocusFrames = 2;
        }
        if (ImGui.IsKeyPressed(ImGuiKey.Backspace))
        {
            if (HasSelection()) { DeleteSelectionIfAny(); changed = true; }
            else if (_cursor > 0)
            {
                _doc.RemoveAt(_cursor - 1);
                _cursor--;
                changed = true;
            }
        }
        if (ImGui.IsKeyPressed(ImGuiKey.Delete))
        {
            if (HasSelection()) { DeleteSelectionIfAny(); changed = true; }
            else if (_cursor < _doc.Count)
            {
                _doc.RemoveAt(_cursor);
                changed = true;
            }
        }
        if (ctrl && ImGui.IsKeyPressed(ImGuiKey.A))
        {
            _selStart = 0;
            _selEnd   = _doc.Count;
            _cursor   = _doc.Count;
        }
        if (ctrl && ImGui.IsKeyPressed(ImGuiKey.C) && HasSelection())
        {
            ImGui.SetClipboardText(SelectionToPlainText());
        }
        if (ctrl && ImGui.IsKeyPressed(ImGuiKey.X) && HasSelection())
        {
            ImGui.SetClipboardText(SelectionToPlainText());
            DeleteSelectionIfAny();
            changed = true;
        }
        if (ctrl && ImGui.IsKeyPressed(ImGuiKey.V))
        {
            var txt = ImGui.GetClipboardText();
            if (!string.IsNullOrEmpty(txt))
            {
                DeleteSelectionIfAny();
                foreach (var ch in txt) InsertChar(ch);
                changed = true;
            }
        }

        // Cursor movement.
        void MoveCursor(int newCursor)
        {
            newCursor = Math.Clamp(newCursor, 0, _doc.Count);
            if (shift)
            {
                if (!HasSelection()) { _selStart = _cursor; _selEnd = _cursor; }
                if (newCursor < _selStart)      { _selStart = newCursor; }
                else if (newCursor > _selEnd)   { _selEnd   = newCursor; }
                else
                {
                    // Shrink from whichever side the cursor was on.
                    if (_cursor == _selStart) _selStart = newCursor;
                    else                      _selEnd   = newCursor;
                }
                if (_selStart > _selEnd) (_selStart, _selEnd) = (_selEnd, _selStart);
            }
            else _selStart = _selEnd = -1;
            _cursor = newCursor;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.LeftArrow))  MoveCursor(_cursor - 1);
        if (ImGui.IsKeyPressed(ImGuiKey.RightArrow)) MoveCursor(_cursor + 1);
        if (ImGui.IsKeyPressed(ImGuiKey.UpArrow))    MoveCursor(CursorVerticalMove(-1));
        if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))  MoveCursor(CursorVerticalMove(+1));
        if (ImGui.IsKeyPressed(ImGuiKey.Home))       MoveCursor(LineEdge(_cursor, false));
        if (ImGui.IsKeyPressed(ImGuiKey.End))        MoveCursor(LineEdge(_cursor, true));

        return changed;
    }

    // Renders a keyboard-only sink InputText immediately below the composer rect. Zero-height, transparent, but still a real active widget so. ImGui routes typed chars through its CharFilter callback (which we consume + discard). This is the ONLY reliable way to receive text input without ImGui.InputText's own visible rendering.
    private static readonly ImGui.ImGuiInputTextCallbackDelegate _captureCallback = CaptureCallback;
    private static void DrawKeyboardCapture(string id)
    {
        var startPos = ImGui.GetCursorScreenPos();
        // Push the sink offscreen visually - full transparency + a clip rect on itself keeps it out of view but leaves it interactive.
        ImGui.PushStyleColor(ImGuiCol.FrameBg,        new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive,  new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.Text,           new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.Border,         new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,  new Vector2(0f, 0f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);

        if (_captureFocusFrames > 0)
        {
            ImGui.SetKeyboardFocusHere();
            _captureFocusFrames--;
        }
        ImGui.SetNextItemWidth(1f);
        var flags = ImGuiInputTextFlags.CallbackCharFilter
                  | ImGuiInputTextFlags.NoUndoRedo
                  | ImGuiInputTextFlags.AutoSelectAll;
        ImGui.InputText("##rs_composer_kbd_" + id, ref _captureBuf, 1, flags, _captureCallback);
        // If the sink InputText lost focus and we're not mid-request for one, drop our own focus state so the caret stops blinking.
        if (IsFocused && !ImGui.IsItemActive() && _captureFocusFrames == 0)
            IsFocused = false;

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(5);
    }

    private static unsafe int CaptureCallback(ref ImGuiInputTextCallbackData data)
    {
        // CallbackCharFilter: EventChar holds the typed char. Insert it into our doc and return 1 to have ImGui discard the char from the sink InputText's buffer (so the buffer never grows).
        if ((data.EventFlag & ImGuiInputTextFlags.CallbackCharFilter) != 0)
        {
            var c = (char)data.EventChar;
            if (c >= 32 || c == '\n' || c == '\t')
                InsertChar(c);
            return 1;
        }
        return 0;
    }

    private static void InsertChar(char c)
    {
        DeleteSelectionIfAny();
        // Alignment comes from the paragraph we're typing INTO. If the char immediately before the caret is a newline (or there is no prev at all) we're starting a fresh paragraph and default to. Left so the previous line's alignment doesn't spill over.
        var align = TextAlign.Left;
        if (_cursor > 0 && _cursor <= _doc.Count)
        {
            var prev = _doc[_cursor - 1];
            if (prev.Kind == SegmentKind.Text && prev.Ch != '\n') align = prev.Align;
        }
        _doc.Insert(_cursor, new Segment
        {
            Kind      = SegmentKind.Text,
            Ch        = c,
            Bold      = _pendingStyle.Bold,
            Italic    = _pendingStyle.Italic,
            Underline = _pendingStyle.Underline,
            ColorRgba = _pendingStyle.ColorRgba,
            SizePt    = _pendingStyle.SizePt,
            Align     = align,
            FontFamily = _pendingStyle.FontFamily ?? string.Empty,
        });
        _cursor++;
    }

    // Selection helpers

    private static bool HasSelection() => _selStart >= 0 && _selEnd > _selStart;

    private static (int, int) OrderedSelection() => (Math.Min(_selStart, _selEnd), Math.Max(_selStart, _selEnd));

    private static void DeleteSelectionIfAny()
    {
        if (!HasSelection()) return;
        var (a, b) = OrderedSelection();
        _doc.RemoveRange(a, b - a);
        _cursor = a;
        _selStart = _selEnd = -1;
    }

    private static string SelectionToPlainText()
    {
        var (a, b) = OrderedSelection();
        var sb = new StringBuilder();
        for (int i = a; i < b; i++)
        {
            var s = _doc[i];
            if (s.Kind == SegmentKind.Text) sb.Append(s.Ch);
            else if (s.Kind == SegmentKind.Image) sb.Append("[img]").Append(s.Url).Append("[/img]");
            else if (s.Kind == SegmentKind.Video) sb.Append("[video]").Append(s.Url).Append("[/video]");
        }
        return sb.ToString();
    }

    private static void SelectWordAt(int cursor)
    {
        if (_doc.Count == 0) return;
        int i = Math.Clamp(cursor, 0, _doc.Count - 1);
        if (_doc[i].Kind != SegmentKind.Text) { _selStart = i; _selEnd = i + 1; _cursor = i + 1; return; }
        int a = i, b = i + 1;
        while (a > 0 && _doc[a - 1].Kind == SegmentKind.Text && !char.IsWhiteSpace(_doc[a - 1].Ch)) a--;
        while (b < _doc.Count && _doc[b].Kind == SegmentKind.Text && !char.IsWhiteSpace(_doc[b].Ch)) b++;
        _selStart = a;
        _selEnd   = b;
        _cursor   = b;
    }

    private static bool SameStyle(Segment a, Segment b)
        => a.Bold == b.Bold && a.Italic == b.Italic && a.Underline == b.Underline
        && a.ColorRgba == b.ColorRgba && a.SizePt == b.SizePt
        && a.Align == b.Align
        && string.Equals(a.FontFamily ?? string.Empty, b.FontFamily ?? string.Empty, StringComparison.Ordinal);

    // Push a segment's family+style font handle for the duration of a using block. Style folds the segment's Bold/Italic bits into the. FontStyle enum so a Bold+Italic run gets the BoldItalic face if that variant loaded successfully.
    private static SocialComposerFonts.PushScope PushSegmentFont(Segment s)
    {
        var style = (s.Bold, s.Italic) switch
        {
            (true,  true ) => FontStyle.BoldItalic,
            (true,  false) => FontStyle.Bold,
            (false, true ) => FontStyle.Italic,
            _              => FontStyle.Regular,
        };
        return SocialComposerFonts.Push(s.FontFamily ?? string.Empty, style);
    }

    private static void AppendAlignOpen(StringBuilder sb, TextAlign a)
    {
        if (a == TextAlign.Center) sb.Append("[align=center]");
        else if (a == TextAlign.Right) sb.Append("[align=right]");
    }
    private static void AppendAlignClose(StringBuilder sb, TextAlign a)
    {
        if (a != TextAlign.Left) sb.Append("[/align]");
    }

    private delegate void SetFlag(ref Segment s, bool v);
    private static void ToggleFlag(Func<Segment, bool> get, SetFlag set, ref bool pending)
    {
        if (HasSelection())
        {
            var (a, b) = OrderedSelection();
            bool allOn = true;
            for (int i = a; i < b; i++)
            {
                if (_doc[i].Kind != SegmentKind.Text) continue;
                if (!get(_doc[i])) { allOn = false; break; }
            }
            var target = !allOn;
            for (int i = a; i < b; i++)
            {
                if (_doc[i].Kind != SegmentKind.Text) continue;
                var s = _doc[i]; set(ref s, target); _doc[i] = s;
            }
        }
        else pending = !pending;
    }

    // Cursor geometry

    private static (float X, float Y, float H) CaretPosition(Vector2 contentMin)
    {
        // Caret X sits before the glyph whose Index == _cursor (or after the last one if the cursor is at end).
        if (_glyphs.Count == 0)
            return (contentMin.X, contentMin.Y, ImGui.GetTextLineHeight());

        // Search for the glyph representing this cursor position.
        for (int li = 0; li < _lines.Count; li++)
        {
            var line = _lines[li];
            for (int k = 0; k < line.Count; k++)
            {
                var g = _glyphs[line.FirstGlyph + k];
                if (g.Index == _cursor)
                    return (g.X, g.Y, g.H);
            }
        }

        // Cursor is past the last glyph.
        var last = _glyphs[_glyphs.Count - 1];
        if (last.IsNewline)
        {
            var lastLine = _lines[_lines.Count - 1];
            return (contentMin.X, lastLine.Y, lastLine.Height);
        }
        return (last.X + last.W, last.Y, last.H);
    }

    private static int HitTest(Vector2 mp)
    {
        if (_glyphs.Count == 0) return 0;
        // Pick the line whose vertical band contains the mouse (clamped).
        int lineIdx = 0;
        for (int i = 0; i < _lines.Count; i++)
        {
            var ln = _lines[i];
            if (mp.Y < ln.Y + ln.Height) { lineIdx = i; break; }
            lineIdx = i;
        }
        var line = _lines[lineIdx];
        if (line.Count == 0)
        {
            // Empty line - cursor goes at first glyph of next line, or end.
            if (line.FirstGlyph < _glyphs.Count) return _glyphs[line.FirstGlyph].Index;
            return _doc.Count;
        }
        // Pick the glyph closest to mouse X.
        int best = line.FirstGlyph;
        for (int k = 0; k < line.Count; k++)
        {
            var g = _glyphs[line.FirstGlyph + k];
            if (mp.X < g.X + g.W * 0.5f) return g.Index;
            best = line.FirstGlyph + k;
        }
        // Past the last glyph on this line - position after it.
        var bg = _glyphs[best];
        return bg.Index + 1;
    }

    private static int CursorVerticalMove(int direction)
    {
        // Locate current caret's line + preferred X.
        float caretX = 0f;
        int   caretLine = 0;
        bool  found = false;
        for (int li = 0; li < _lines.Count && !found; li++)
        {
            var line = _lines[li];
            for (int k = 0; k < line.Count; k++)
            {
                var g = _glyphs[line.FirstGlyph + k];
                if (g.Index == _cursor) { caretX = g.X; caretLine = li; found = true; break; }
            }
            if (!found && li == _lines.Count - 1)
            {
                // Cursor after last glyph.
                if (_glyphs.Count > 0)
                {
                    var last = _glyphs[_glyphs.Count - 1];
                    caretX = last.IsNewline ? 0f : last.X + last.W;
                }
                caretLine = _lines.Count - 1;
            }
        }
        int newLine = Math.Clamp(caretLine + direction, 0, _lines.Count - 1);
        if (newLine == caretLine) return _cursor;
        var nl = _lines[newLine];
        if (nl.Count == 0)
        {
            if (nl.FirstGlyph < _glyphs.Count) return _glyphs[nl.FirstGlyph].Index;
            return _doc.Count;
        }
        // Find glyph whose X range contains caretX.
        for (int k = 0; k < nl.Count; k++)
        {
            var g = _glyphs[nl.FirstGlyph + k];
            if (caretX < g.X + g.W * 0.5f) return g.Index;
        }
        var lastG = _glyphs[nl.FirstGlyph + nl.Count - 1];
        return lastG.Index + 1;
    }

    private static int LineEdge(int cursor, bool end)
    {
        // Find line the cursor is on.
        for (int li = 0; li < _lines.Count; li++)
        {
            var line = _lines[li];
            bool onThis = false;
            if (line.Count == 0)
            {
                if (line.FirstGlyph >= _glyphs.Count && cursor == _doc.Count) onThis = true;
            }
            else
            {
                var first = _glyphs[line.FirstGlyph];
                var last  = _glyphs[line.FirstGlyph + line.Count - 1];
                if (cursor >= first.Index && cursor <= last.Index + 1) onThis = true;
            }
            if (!onThis) continue;

            if (line.Count == 0) return cursor;
            var g0 = _glyphs[line.FirstGlyph];
            var gN = _glyphs[line.FirstGlyph + line.Count - 1];
            if (end)
            {
                // Skip trailing newline glyph when picking end.
                if (gN.IsNewline) return gN.Index;
                return gN.Index + 1;
            }
            return g0.Index;
        }
        return cursor;
    }

    // Color / packing helpers

    private static uint Pack(Vector4 c)
    {
        static uint B(float x) => (uint)Math.Clamp((int)(x * 255f + 0.5f), 0, 255);
        return B(c.X) | (B(c.Y) << 8) | (B(c.Z) << 16) | (B(c.W) << 24);
    }

    private static Vector4 UnpackToVec(uint c)
        => new(( c        & 0xFF) / 255f,
               ((c >> 8)  & 0xFF) / 255f,
               ((c >> 16) & 0xFF) / 255f,
               ((c >> 24) & 0xFF) / 255f);

    // Baseline point size assumption. Dalamud's default UI font sits ~13-14 pixels tall which we call "12pt" for the composer's purposes; anything a user picks scales linearly from there.
    private const float BaselinePt = 12f;
    private static float SizeScale(int sizePt)
        => sizePt <= 0 ? 1f : Math.Clamp(sizePt / BaselinePt, 0.5f, 4f);

    // Segment stores color as 0xRRGGBBAA. Convert to ImGui's packed. ABGR uint for the draw list.
    private static uint RgbaToPack(uint rgba)
    {
        uint r = (rgba >> 24) & 0xFF;
        uint g = (rgba >> 16) & 0xFF;
        uint b = (rgba >>  8) & 0xFF;
        uint a =  rgba        & 0xFF;
        if (a == 0) a = 255;
        return r | (g << 8) | (b << 16) | (a << 24);
    }

    private static uint ParseHexColor(string? val)
    {
        if (string.IsNullOrEmpty(val)) return 0;
        var s = val.Trim();
        if (s.StartsWith("#")) s = s.Substring(1);
        if (s.Length != 6 && s.Length != 8) return 0;
        try
        {
            byte r = Convert.ToByte(s.Substring(0, 2), 16);
            byte g = Convert.ToByte(s.Substring(2, 2), 16);
            byte b = Convert.ToByte(s.Substring(4, 2), 16);
            byte a = s.Length == 8 ? Convert.ToByte(s.Substring(6, 2), 16) : (byte)255;
            return ((uint)r << 24) | ((uint)g << 16) | ((uint)b << 8) | a;
        }
        catch { return 0; }
    }

    private static Vector4 LerpV4(Vector4 a, Vector4 b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Vector4(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Z + (b.Z - a.Z) * t,
            a.W + (b.W - a.W) * t);
    }
}
