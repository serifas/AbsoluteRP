using System;
using System.Collections.Generic;
using System.Numerics;
using AbsoluteRP.RsUI;
using AbsoluteRP.Windows.Ect;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AbsoluteRP.Social;

// BBCode-style parser + ImGui renderer for social post bodies. Supported tags (case-insensitive): [b]...[/b] bold via underline emulation (ImGui text is single-weight; we use color-preserving double-draw with 1px offset as a "bold" cue). [i]...[/i] italic - same reason: no italic font, so we tint slightly (visual differentiation). [u]...[/u] underline - a real line drawn under the text. [color=#RRGGBB]...[/color] hex color override. [size=N]...[/size] text size in points (clamped 8..48). [img]https://...[/img] block-level image; click to open ImagePreview. [video]https://...[/video] block-level video card; click to play. Unknown tags are rendered as literal text so nothing "eats" the user's content silently.
public static class SocialMarkup
{
    private enum StyleFlags { None = 0, Bold = 1, Italic = 2, Underline = 4 }
    private enum ParaAlign  { Left = 0, Center = 1, Right = 2 }

    private readonly struct Style
    {
        public readonly Vector4 Color;
        public readonly float   Size;      // relative multiplier: 1.0f = default
        public readonly StyleFlags Flags;
        public readonly string    Font;    // SocialComposerFonts family id ("" = default)
        public readonly ParaAlign Align;   // paragraph-level alignment
        public Style(Vector4 c, float s, StyleFlags f, string font, ParaAlign align)
        { Color = c; Size = s; Flags = f; Font = font ?? string.Empty; Align = align; }
        public Style(Vector4 c, float s, StyleFlags f) : this(c, s, f, string.Empty, ParaAlign.Left) { }
    }

    // Public entry - reads `body` and emits ImGui commands. Wraps at the current content region width. Media tokens interrupt inline flow and draw on their own rows.
    public static void Render(string body, float wrapWidth = 0f, string? scope = null)
    {
        if (string.IsNullOrEmpty(body)) return;
        PruneSpoilers();
        RenderCore(body, wrapWidth, scope ?? ("h" + body.GetHashCode().ToString("X")), 0);
    }

    private static void RenderCore(string body, float wrapWidth, string scope, int depth)
    {
        if (string.IsNullOrEmpty(body)) return;
        if (wrapWidth <= 0f) wrapWidth = ImGui.GetContentRegionAvail().X;

        var baseStyle = new Style(RsTheme.TextPrimary, 1.0f, StyleFlags.None);
        var styleStack = new Stack<Style>();
        styleStack.Push(baseStyle);

        int i = 0;
        int spoilerIndex = 0;
        var textAccum = new System.Text.StringBuilder();

        void FlushText()
        {
            if (textAccum.Length == 0) return;
            var s = styleStack.Peek();
            EmitText(textAccum.ToString(), s, wrapWidth);
            textAccum.Clear();
        }

        while (i < body.Length)
        {
            char c = body[i];
            // [spoiler]...[/spoiler] / [spoiler=Label]...[/spoiler] - matched with nesting; unclosed falls through to literal text.
            if (c == '[' && TryReadSpoiler(body, i, out var spLabel, out var spInner, out var spEnd))
            {
                FlushText();
                DrawSpoiler(spLabel, spInner, wrapWidth, scope + "#" + spoilerIndex++, depth);
                i = spEnd;
                continue;
            }
            if (c == '[' && TryReadTag(body, i, out var tag, out var end))
            {
                FlushText();
                HandleTag(tag, styleStack, wrapWidth);
                i = end;
                continue;
            }
            textAccum.Append(c);
            i++;
        }
        FlushText();
    }

    // spoilers

    // Reveal state: key -> last frame the block was visible. Present = revealed. Entries not seen for a few frames (scrolled out, feed reloaded, window closed) are dropped so the spoiler re-hides.
    private static readonly Dictionary<string, int> _revealed = new();
    private static bool _clickClaimed;

    // True while a mouse press that started on a spoiler is still down (and on its release frame) - card-level click handlers check this so toggling a spoiler doesn't also open the post.
    public static bool ClickClaimed
    {
        get
        {
            if (!_clickClaimed) return false;
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left) || ImGui.IsMouseReleased(ImGuiMouseButton.Left)) return true;
            _clickClaimed = false;
            return false;
        }
    }

    // Hide every revealed spoiler (call on feed reload).
    public static void ResetSpoilers() => _revealed.Clear();

    private static readonly List<string> _pruneTmp = new();
    private static void PruneSpoilers()
    {
        if (_revealed.Count == 0) return;
        int frame = ImGui.GetFrameCount();
        _pruneTmp.Clear();
        foreach (var kv in _revealed) if (frame - kv.Value > 3) _pruneTmp.Add(kv.Key);
        foreach (var k in _pruneTmp) _revealed.Remove(k);
    }

    private static bool TryReadSpoilerOpen(string s, int start, out string? label, out int after)
    {
        label = null; after = start;
        if (start + 8 > s.Length || string.Compare(s, start, "[spoiler", 0, 8, StringComparison.OrdinalIgnoreCase) != 0) return false;
        int p = start + 8;
        if (p >= s.Length) return false;
        if (s[p] == ']') { after = p + 1; return true; }
        if (s[p] != '=') return false;
        int close = s.IndexOf(']', p + 1);
        if (close < 0) return false;
        label = s.Substring(p + 1, close - p - 1).Trim();
        after = close + 1;
        return true;
    }

    private static bool TryReadSpoiler(string s, int start, out string label, out string inner, out int end)
    {
        label = "Spoiler"; inner = string.Empty; end = start;
        if (!TryReadSpoilerOpen(s, start, out var lbl, out var contentStart)) return false;
        const string closeTag = "[/spoiler]";
        int depth = 1, p = contentStart;
        while (p < s.Length)
        {
            int nextOpen  = IndexOfIgnoreCase(s, "[spoiler", p);
            int nextClose = IndexOfIgnoreCase(s, closeTag, p);
            if (nextClose < 0) return false;
            if (nextOpen >= 0 && nextOpen < nextClose)
            {
                if (TryReadSpoilerOpen(s, nextOpen, out _, out var oa)) { depth++; p = oa; }
                else p = nextOpen + 1;
                continue;
            }
            depth--;
            if (depth == 0)
            {
                inner = s.Substring(contentStart, nextClose - contentStart);
                end   = nextClose + closeTag.Length;
                if (!string.IsNullOrWhiteSpace(lbl)) label = lbl!;
                return true;
            }
            p = nextClose + closeTag.Length;
        }
        return false;
    }

    // Manual hit test (works inside the NoInputs clip children used by the feed card preview).
    private static bool SpoilerHit(Vector2 min, Vector2 max, out bool hovered)
    {
        hovered = ImGui.IsMouseHoveringRect(min, max)
               && ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _clickClaimed = true;
            return true;
        }
        return false;
    }

    private static void DrawSpoiler(string label, string inner, float wrapWidth, string key, int depth)
    {
        var dl     = ImGui.GetWindowDrawList();
        var pad    = RsTheme.S(8f);
        var round  = RsTheme.S(6f);
        var accent = RsTheme.AccentPrimary;
        var border = ImGui.ColorConvertFloat4ToU32(new Vector4(accent.X, accent.Y, accent.Z, 0.55f));
        var bar    = ImGui.ColorConvertFloat4ToU32(accent);
        var muted  = ImGui.ColorConvertFloat4ToU32(RsTheme.TextMuted);
        var width  = MathF.Max(RsTheme.S(120f), wrapWidth);
        int frame  = ImGui.GetFrameCount();
        var icon   = FontAwesomeIcon.EyeSlash.ToIconString();
        float iconW;
        using (RsIcons.Push()) iconW = ImGui.CalcTextSize(icon).X;

        ImGui.Spacing();
        var min = ImGui.GetCursorScreenPos();
        var lineH = ImGui.GetTextLineHeight();
        bool revealed = _revealed.ContainsKey(key);

        void DrawHeader(Vector2 tp, float right, string hint, bool hovered)
        {
            using (RsIcons.Push()) dl.AddText(tp, muted, icon);
            dl.AddText(new Vector2(tp.X + iconW + RsTheme.S(6f), tp.Y), muted, label);
            var hs = ImGui.CalcTextSize(hint);
            dl.AddText(new Vector2(right - pad - hs.X, tp.Y),
                       ImGui.ColorConvertFloat4ToU32(hovered ? RsTheme.TextPrimary : RsTheme.TextMuted), hint);
        }

        if (!revealed)
        {
            // Obscured plate - inner content (incl. media) is never parsed, loaded or drawn while hidden.
            var size  = new Vector2(width, lineH * 2f + pad * 3f);
            var max   = min + size;
            bool clicked = SpoilerHit(min, max, out var hovered);
            var plate = RsTheme.BgSecondary;
            dl.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(new Vector4(plate.X * 0.7f, plate.Y * 0.7f, plate.Z * 0.7f, 0.96f)), round);
            // "Blurred text" smears.
            uint seed = (uint)key.GetHashCode();
            var tm = RsTheme.TextMuted;
            var smear = ImGui.ColorConvertFloat4ToU32(new Vector4(tm.X, tm.Y, tm.Z, hovered ? 0.16f : 0.10f));
            float y = min.Y + pad * 2f + lineH;
            float x = min.X + pad * 1.5f;
            float rowEnd = max.X - pad;
            for (int k = 0; k < 10 && x < rowEnd; k++)
            {
                seed = seed * 1664525u + 1013904223u;
                float w = RsTheme.S(22f + (seed >> 24) % 60);
                float x2 = MathF.Min(rowEnd, x + w);
                dl.AddRectFilled(new Vector2(x, y), new Vector2(x2, y + lineH * 0.55f), smear, lineH * 0.3f);
                x = x2 + RsTheme.S(6f);
            }
            dl.AddRect(min, max, hovered ? bar : border, round, ImDrawFlags.None, 1f);
            dl.AddRectFilled(min, new Vector2(min.X + RsTheme.S(3f), max.Y), bar, round, ImDrawFlags.RoundCornersLeft);
            DrawHeader(new Vector2(min.X + pad * 1.5f, min.Y + pad), max.X, "Click to reveal", hovered);

            ImGui.Dummy(size);
            if (clicked) _revealed[key] = frame;
        }
        else
        {
            // Header row (click to hide) + nested markup rendered normally.
            var hdrMax = new Vector2(min.X + width, min.Y + lineH + pad);
            bool clicked = SpoilerHit(min, hdrMax, out var hovered);
            DrawHeader(new Vector2(min.X + pad * 1.5f, min.Y + pad * 0.5f), hdrMax.X, "Click to hide", hovered);
            ImGui.Dummy(new Vector2(width, lineH + pad));

            var indent = pad * 1.5f;
            ImGui.Indent(indent);
            if (depth < 8) RenderCore(inner, MathF.Max(RsTheme.S(40f), width - indent - pad), key, depth + 1);
            else           ImGui.TextWrapped(inner);
            ImGui.Unindent(indent);
            ImGui.Dummy(new Vector2(1f, pad * 0.5f));
            var max = new Vector2(min.X + width, ImGui.GetCursorScreenPos().Y);
            dl.AddRect(min, max, border, round, ImDrawFlags.None, 1f);
            dl.AddRectFilled(min, new Vector2(min.X + RsTheme.S(3f), max.Y), bar, round, ImDrawFlags.RoundCornersLeft);

            if (clicked) _revealed.Remove(key);
            else if (ImGui.IsRectVisible(min, max)) _revealed[key] = frame;
        }
        ImGui.Spacing();
    }

    // tag handling

    private static bool TryReadTag(string s, int start, out ParsedTag tag, out int consumed)
    {
        tag = default;
        consumed = start;
        int close = s.IndexOf(']', start + 1);
        if (close < 0) return false;
        var inner = s.Substring(start + 1, close - start - 1).Trim();
        if (inner.Length == 0) return false;
        // Look for `[img]URL[/img]` / `[video]URL[/video]` - need matching close tag before we call this a real tag; otherwise render literal.
        var isClose = inner.StartsWith("/");
        var lower   = inner.ToLowerInvariant();
        string name;
        string? value = null;
        if (isClose)
        {
            name = lower.Substring(1).Trim();
        }
        else
        {
            var eq = lower.IndexOf('=');
            if (eq > 0)
            {
                name = lower.Substring(0, eq).Trim();
                value = inner.Substring(eq + 1).Trim();
            }
            else
            {
                name = lower.Trim();
            }
        }

        // Only accept the whitelisted names - otherwise treat as literal.
        if (!IsKnownTag(name)) return false;

        // Media tags carry their URL between open+close and need the whole block matched so we can extract it in one step.
        if ((name == "img" || name == "video") && !isClose)
        {
            var closeTag = "[/" + name + "]";
            var closeIdx = IndexOfIgnoreCase(s, closeTag, close + 1);
            if (closeIdx < 0) return false;
            var url = s.Substring(close + 1, closeIdx - close - 1).Trim();
            tag = new ParsedTag(name, url, isMedia: true, isClose: false);
            consumed = closeIdx + closeTag.Length;
            return true;
        }

        tag = new ParsedTag(name, value, isMedia: false, isClose: isClose);
        consumed = close + 1;
        return true;
    }

    private static bool IsKnownTag(string name) => name switch
    {
        "b" or "i" or "u" or "color" or "size" or "img" or "video" or "align" or "font" => true,
        _ => false,
    };

    private readonly struct ParsedTag
    {
        public readonly string  Name;
        public readonly string? Value;
        public readonly bool    IsMedia;
        public readonly bool    IsClose;
        public ParsedTag(string n, string? v, bool isMedia, bool isClose)
        { Name = n; Value = v; IsMedia = isMedia; IsClose = isClose; }
    }

    private static void HandleTag(ParsedTag tag, Stack<Style> styleStack, float wrapWidth)
    {
        if (tag.IsMedia)
        {
            if (tag.Name == "img")   DrawImageBlock(tag.Value  ?? string.Empty, wrapWidth);
            if (tag.Name == "video") DrawVideoBlock(tag.Value  ?? string.Empty, wrapWidth);
            return;
        }
        var current = styleStack.Peek();
        if (tag.IsClose)
        {
            if (styleStack.Count > 1) styleStack.Pop();
            return;
        }
        Style next = current;
        switch (tag.Name)
        {
            case "b": next = new Style(current.Color, current.Size, current.Flags | StyleFlags.Bold,       current.Font, current.Align); break;
            case "i": next = new Style(current.Color, current.Size, current.Flags | StyleFlags.Italic,     current.Font, current.Align); break;
            case "u": next = new Style(current.Color, current.Size, current.Flags | StyleFlags.Underline,  current.Font, current.Align); break;
            case "color":
                var col = ParseColor(tag.Value) ?? current.Color;
                next = new Style(col, current.Size, current.Flags, current.Font, current.Align);
                break;
            case "size":
                var sz = ParseSize(tag.Value) ?? current.Size;
                next = new Style(current.Color, sz, current.Flags, current.Font, current.Align);
                break;
            case "font":
                next = new Style(current.Color, current.Size, current.Flags,
                                 tag.Value ?? string.Empty, current.Align);
                break;
            case "align":
                var a = (tag.Value ?? "").ToLowerInvariant() switch
                {
                    "center" => ParaAlign.Center,
                    "right"  => ParaAlign.Right,
                    _        => ParaAlign.Left,
                };
                next = new Style(current.Color, current.Size, current.Flags, current.Font, a);
                break;
        }
        styleStack.Push(next);
    }

    // inline text

    private static void EmitText(string text, Style s, float wrapWidth)
    {
        // Push the per-run font family so [font=georgia]...[/font] actually changes typeface. Bold+Italic bits fold into the style enum so. Georgia-Bold or Segoe-BoldItalic loads if the variant exists.
        var fstyle = (((s.Flags & StyleFlags.Bold) != 0), ((s.Flags & StyleFlags.Italic) != 0)) switch
        {
            (true,  true ) => FontStyle.BoldItalic,
            (true,  false) => FontStyle.Bold,
            (false, true ) => FontStyle.Italic,
            _              => FontStyle.Regular,
        };
        using var fontScope = SocialComposerFonts.Push(s.Font ?? string.Empty, fstyle);

        ImGui.PushStyleColor(ImGuiCol.Text, s.Color);
        bool scaled = Math.Abs(s.Size - 1.0f) > 0.01f;
        if (scaled) ImGui.SetWindowFontScale(s.Size);

        // Italic tint fallback ONLY when we didn't get a real italic font - if the font scope pushed a true italic face, the glyphs already slant.
        bool italic = (s.Flags & StyleFlags.Italic) != 0;
        if (italic && !fontScope.Pushed)
        {
            var tinted = new Vector4(s.Color.X * 0.92f, s.Color.Y * 0.98f, s.Color.Z * 1.04f, s.Color.W);
            ImGui.PopStyleColor();
            ImGui.PushStyleColor(ImGuiCol.Text, tinted);
        }

        // Paragraph alignment: for center/right, measure the wrapped text and offset the whole block. ImGui's TextWrapped is left-only, so we split on '\n' and lay out each visual line ourselves - for long paragraphs this still respects wrapWidth via TextWrapped per-line but doesn't per-line-center wrapped continuations.
        if (s.Align == ParaAlign.Left)
        {
            var start = ImGui.GetCursorScreenPos();
            ImGui.PushTextWrapPos(ImGui.GetCursorPos().X + wrapWidth);
            ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();
            if ((s.Flags & StyleFlags.Bold) != 0 && !fontScope.Pushed)
            {
                var save = ImGui.GetCursorScreenPos();
                ImGui.SetCursorScreenPos(new Vector2(start.X + 1f, start.Y));
                ImGui.PushTextWrapPos(ImGui.GetCursorPos().X + wrapWidth);
                ImGui.TextUnformatted(text);
                ImGui.PopTextWrapPos();
                ImGui.SetCursorScreenPos(save);
            }
            if ((s.Flags & StyleFlags.Underline) != 0)
            {
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                var y   = max.Y - 1f;
                var col = ImGui.ColorConvertFloat4ToU32(s.Color);
                ImGui.GetWindowDrawList().AddLine(new Vector2(min.X, y), new Vector2(max.X, y), col, 1f);
            }
        }
        else
        {
            // Center/Right: measure each line and offset X manually. This means wrapped continuations don't re-center, but explicit \n paragraphs align correctly.
            var lines = text.Split('\n');
            var startX = ImGui.GetCursorPosX();
            foreach (var line in lines)
            {
                var sz = ImGui.CalcTextSize(line);
                float offset = s.Align == ParaAlign.Center
                    ? MathF.Max(0f, (wrapWidth - sz.X) * 0.5f)
                    : MathF.Max(0f, wrapWidth - sz.X);
                ImGui.SetCursorPosX(startX + offset);
                var lineStart = ImGui.GetCursorScreenPos();
                ImGui.TextUnformatted(line);
                if ((s.Flags & StyleFlags.Bold) != 0 && !fontScope.Pushed)
                {
                    var save = ImGui.GetCursorScreenPos();
                    ImGui.SetCursorScreenPos(new Vector2(lineStart.X + 1f, lineStart.Y));
                    ImGui.TextUnformatted(line);
                    ImGui.SetCursorScreenPos(save);
                }
                if ((s.Flags & StyleFlags.Underline) != 0)
                {
                    var min = ImGui.GetItemRectMin();
                    var max = ImGui.GetItemRectMax();
                    var y   = max.Y - 1f;
                    var col = ImGui.ColorConvertFloat4ToU32(s.Color);
                    ImGui.GetWindowDrawList().AddLine(new Vector2(min.X, y), new Vector2(max.X, y), col, 1f);
                }
            }
        }

        if (scaled) ImGui.SetWindowFontScale(1.0f);
        ImGui.PopStyleColor();
    }

    // block media

    private static void DrawImageBlock(string url, float wrapWidth)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        ImGui.Spacing();
        var tex = SocialMediaCache.Get(url);
        if (tex == null)
        {
            // Placeholder while loading (or on error).
            var ph = new Vector2(MathF.Min(wrapWidth, RsTheme.S(320f)), RsTheme.S(80f));
            var dl = ImGui.GetWindowDrawList();
            var min = ImGui.GetCursorScreenPos();
            var max = min + ph;
            dl.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(RsTheme.BgSecondary), RsTheme.S(6f));
            dl.AddRect(min, max, ImGui.ColorConvertFloat4ToU32(RsTheme.Border), RsTheme.S(6f));
            var label = "loading image…";
            var lbl = ImGui.CalcTextSize(label);
            dl.AddText(new Vector2(min.X + (ph.X - lbl.X) * 0.5f, min.Y + (ph.Y - lbl.Y) * 0.5f),
                        ImGui.ColorConvertFloat4ToU32(RsTheme.TextMuted), label);
            ImGui.Dummy(ph);
            ImGui.Spacing();
            return;
        }
        // Cap the thumbnail width to a font-scaled base so higher-DPI / larger-font setups get proportionally larger thumbnails without eating the whole card. Base 300px * FontGlobalScale, clamped to the current wrap width (post panel).
        var fontScale = ImGui.GetIO().FontGlobalScale;
        var maxW = MathF.Min(wrapWidth, 300f * fontScale);
        var scale = tex.Width > 0 ? maxW / tex.Width : 1f;
        if (scale > 1f) scale = 1f;
        var size = new Vector2(tex.Width * scale, tex.Height * scale);
        ImGui.Image(tex.Handle, size);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Click to enlarge");
        if (ImGui.IsItemClicked())
        {
            ImagePreview.width  = tex.Width;
            ImagePreview.height = tex.Height;
            ImagePreview.PreviewImage = tex;
            try { Plugin.plugin.OpenImagePreview(); } catch { }
        }
        ImGui.Spacing();
    }

    private static void DrawVideoBlock(string url, float wrapWidth)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        // An audio clip attached to a post: the mp3 player instead of a video card.
        if (AbsoluteRP.Helpers.GalleryMedia.KindOf(url) == 2)
        {
            ImGui.Spacing();
            AbsoluteRP.Misc.RenderAudioEmbed(url, MathF.Min(wrapWidth, AbsoluteRP.RsUI.RsTheme.S(420f)));
            ImGui.Spacing();
            return;
        }
        ImGui.Spacing();

        // Font-scaled sizing keeps thumbnails proportional across DPI.
        var fontScale = ImGui.GetIO().FontGlobalScale;
        var boxW = MathF.Min(wrapWidth, 300f * fontScale);
        var boxH = 140f * fontScale;
        var dl   = ImGui.GetWindowDrawList();
        var min  = ImGui.GetCursorScreenPos();
        var max  = min + new Vector2(boxW, boxH);

        var haveThumb = SocialVideoThumbCache.TryGet(url, out var thumbSrv, out var vw, out var vh);
        var inlineSession = SocialVideoThumbCache.GetInline(url);
        // "Playing" here means an inline session exists and it isn't paused.
        // A paused inline session still shows its last frame as the thumb;
        // clicking again resumes rather than tearing the session down.
        var hasInline = inlineSession != null;
        var isPlaying = hasInline && !inlineSession!.IsPaused;

        // Background + border.
        if (haveThumb && vw > 0 && vh > 0)
        {
            var scale  = MathF.Min(boxW / vw, boxH / vh);
            var drawW  = vw * scale;
            var drawH  = vh * scale;
            var offX   = (boxW - drawW) * 0.5f;
            var offY   = (boxH - drawH) * 0.5f;
            var iMin   = new Vector2(min.X + offX, min.Y + offY);
            var iMax   = new Vector2(iMin.X + drawW, iMin.Y + drawH);
            dl.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 1f)), RsTheme.S(6f));
            dl.AddImage(new Dalamud.Bindings.ImGui.ImTextureID(thumbSrv), iMin, iMax);
            dl.AddRect(min, max, ImGui.ColorConvertFloat4ToU32(RsTheme.Border), RsTheme.S(6f));
        }
        else
        {
            dl.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(new Vector4(0.05f, 0.05f, 0.06f, 1f)), RsTheme.S(6f));
            dl.AddRect      (min, max, ImGui.ColorConvertFloat4ToU32(RsTheme.Border),                       RsTheme.S(6f));
            var label = "Loading preview…";
            var ls    = ImGui.CalcTextSize(label);
            dl.AddText(new Vector2(min.X + (boxW - ls.X) * 0.5f, min.Y + (boxH * 0.5f + RsTheme.S(10f))),
                        ImGui.ColorConvertFloat4ToU32(RsTheme.TextMuted), label);
        }

        // Center play badge - only when not already playing inline.
        if (!isPlaying)
        {
            var badgeR = RsTheme.S(22f);
            var badgeC = new Vector2(min.X + boxW * 0.5f, min.Y + boxH * 0.5f);
            dl.AddCircleFilled(badgeC, badgeR, ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.55f)));
            using (AbsoluteRP.RsUI.RsIcons.Push())
            {
                var glyph = Dalamud.Interface.FontAwesomeIcon.Play.ToIconString();
                var gs    = ImGui.CalcTextSize(glyph);
                var gpos  = new Vector2(badgeC.X - gs.X * 0.5f + RsTheme.S(2f), badgeC.Y - gs.Y * 0.5f);
                dl.AddText(gpos, ImGui.ColorConvertFloat4ToU32(RsTheme.TextPrimary), glyph);
            }
        }

        // Precompute maximize button rect so we can filter main-click hits that land inside it.
        var btnSz  = RsTheme.S(24f);
        var pad    = RsTheme.S(6f);
        var btnMin = new Vector2(max.X - pad - btnSz, min.Y + pad);
        var btnMax = new Vector2(btnMin.X + btnSz, btnMin.Y + btnSz);

        // Main-area hit-rect FIRST, and mark it as overlap-allowed so the maximize button drawn later can also receive input.
        ImGui.SetCursorScreenPos(min);
        var mainClicked = ImGui.InvisibleButton("##vid_hit_" + url.GetHashCode(), new Vector2(boxW, boxH));
        ImGui.SetItemAllowOverlap();
        var mainHovered = ImGui.IsItemHovered();
        if (mainHovered)
        {
            var fname = TryGetFileNameFromUrl(url);
            ImGui.BeginTooltip();
            using (AbsoluteRP.RsUI.RsIcons.Push())
                ImGui.TextUnformatted(Dalamud.Interface.FontAwesomeIcon.Play.ToIconString());
            ImGui.SameLine();
            ImGui.TextUnformatted(string.IsNullOrEmpty(fname) ? "Video" : fname);
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted(
                isPlaying          ? "Click to pause" :
                hasInline          ? "Click to resume"
                                   : "Click to play");
            ImGui.TextUnformatted(url);
            ImGui.PopStyleColor();
            ImGui.EndTooltip();
        }

        // Top-right maximize button - opens the video in the full popup.
        dl.AddRectFilled(btnMin, btnMax, ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.55f)), RsTheme.S(4f));
        using (AbsoluteRP.RsUI.RsIcons.Push())
        {
            var g  = Dalamud.Interface.FontAwesomeIcon.Expand.ToIconString();
            var gs = ImGui.CalcTextSize(g);
            var gp = new Vector2(btnMin.X + (btnSz - gs.X) * 0.5f, btnMin.Y + (btnSz - gs.Y) * 0.5f);
            dl.AddText(gp, ImGui.ColorConvertFloat4ToU32(RsTheme.TextPrimary), g);
        }
        ImGui.SetCursorScreenPos(btnMin);
        var maxClicked = ImGui.InvisibleButton("##vid_max_" + url.GetHashCode(), new Vector2(btnSz, btnSz));
        var maxHovered = ImGui.IsItemHovered();
        if (maxHovered)
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted("Open in video window");
            ImGui.EndTooltip();
        }

        // Precedence: maximize wins its corner. If the maximize button was hovered/clicked, ignore the main click even if it fired (both fire simultaneously in an overlap-allowed setup).
        if (maxClicked)
        {
            // Stop inline (so we don't have two audio streams competing)
            // and open the full popup.
            if (hasInline) SocialVideoThumbCache.StopInline(url);
            SocialVideoPopup.Open(url);
        }
        else if (mainClicked && !maxHovered)
        {
            if (hasInline)
            {
                // Toggle pause on the existing inline session - leaves it in memory (still frame visible) so a follow-up click resumes instantly instead of respawning the subprocess.
                inlineSession!.TogglePlayPause();
            }
            else
            {
                // First click on the poster: spin up live playback.
                SocialVideoThumbCache.StartInline(url, volume: 80);
            }
        }

        // Advance the ImGui cursor past the box (the raw InvisibleButtons may have moved it back to `min + size` already, but be explicit so following markup lines up cleanly).
        ImGui.SetCursorScreenPos(new Vector2(min.X, max.Y));
        ImGui.Spacing();
    }

    private static string TryGetFileNameFromUrl(string url)
    {
        try
        {
            var q = url.IndexOfAny(new[] { '?', '#' });
            var path = q > 0 ? url.Substring(0, q) : url;
            var slash = path.LastIndexOf('/');
            return slash >= 0 && slash < path.Length - 1 ? path.Substring(slash + 1) : path;
        }
        catch { return string.Empty; }
    }

    // helpers

    private static Vector4? ParseColor(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        var s = value.Trim();
        if (s.StartsWith("#")) s = s.Substring(1);
        if (s.Length != 6 && s.Length != 8) return null;
        try
        {
            byte r = Convert.ToByte(s.Substring(0, 2), 16);
            byte g = Convert.ToByte(s.Substring(2, 2), 16);
            byte b = Convert.ToByte(s.Substring(4, 2), 16);
            byte a = s.Length == 8 ? Convert.ToByte(s.Substring(6, 2), 16) : (byte)255;
            return new Vector4(r / 255f, g / 255f, b / 255f, a / 255f);
        }
        catch { return null; }
    }

    private static float? ParseSize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (!int.TryParse(value, out var pt)) return null;
        pt = Math.Clamp(pt, 8, 48);
        // 16 is the default; scale is `pt/16` on top of the current font.
        return pt / 16f;
    }

    private static int IndexOfIgnoreCase(string haystack, string needle, int start)
        => haystack.IndexOf(needle, start, StringComparison.OrdinalIgnoreCase);
}
