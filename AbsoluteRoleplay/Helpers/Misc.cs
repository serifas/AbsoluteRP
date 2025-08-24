﻿using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using UIHelpers = AbsoluteRP.Helpers.UIHelpers;
namespace AbsoluteRP
{
    public class Misc
    {
        public class ParsedNode
        {
            public string Type; // "text", "img", "color", "url", "table", "column", etc.
            public string Content;
            public float Scale = 1.0f;
            public string ColorHex;
            public string Url;
            public List<ParsedNode> Children = new();
            public TextStyle Style = new();
        }
        public struct TextStyle
        {
            public bool Bold;
            public bool Italic;
            public bool Underline;
            public Vector4? Color;
            public int Scale;
        }
        const float minImageSize = 8f; // Minimum image width/height in pixels
        const int minFontSize = 12;    // Minimum font size
        public static Dictionary<int, ImFontPtr> FontSizes = new();

        private static Dictionary<string, IDalamudTextureWrap> _imageCache = new();
        private static HashSet<string> _imagesLoading = new();
        private static string previousInputText = "";
        private static float previousBoxWidth = 0f;
        private static string cachedWrappedText = ""; // Buffer for displaying wrapped text
        public static IFontHandle Jupiter;
        public static float _modVersionWidth;
        public static int loaderIndex = 0;
        private static Random random = new Random();

        public static List<ImFontPtr> availableFonts = new();
        public static ImFontPtr selectedFont;
        private static int selectedFontIndex = 0;
        private static bool allow;
        public static bool LoadUrl { get; set; } = false;
        public static string UrlToLoad { get; set; }
        public class LoaderTweenState
        {
            public float TweenedValue;
            public float TweenStartValue;
            public float TweenTargetValue;
            public float TweenStartTime;
            public float TweenDuration = 0.4f;
        }
        public static float ConvertToPercentage(float value)
        {
            // Clamp the value between 0 and 100
            value = Math.Max(0f, Math.Min(100f, value));

            // Return the percentage
            return value / 100f * 100f;
        }
    
        private static ImFontPtr GetFontForStyle(TextStyle style, int baseSize)
        {
            int key = baseSize;
            if (style.Bold && style.Italic)
                key = 300 + baseSize;
            else if (style.Bold)
                key = 100 + baseSize;
            else if (style.Italic)
                key = 200 + baseSize;

            if (FontSizes.TryGetValue(key, out var font) && !font.IsNull)
                return font;
            if (FontSizes.TryGetValue(baseSize, out var regularFont) && !regularFont.IsNull)
                return regularFont;
            return ImGui.GetFont();
        }

        private static void ParseStyledText(string text, TextStyle style, List<ParsedNode> nodes)
        {
            var tagRegex = new Regex(@"<(b|i|u)>(.*?)</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            int idx = 0;
            while (idx < text.Length)
            {
                var match = tagRegex.Match(text, idx);
                if (match.Success && match.Index == idx)
                {
                    var tag = match.Groups[1].Value.ToLower();
                    var inner = match.Groups[2].Value;
                    var newStyle = style;
                    if (tag == "b") newStyle.Bold = true;
                    else if (tag == "i") newStyle.Italic = true;
                    else if (tag == "u") newStyle.Underline = true;
                    ParseStyledText(inner, newStyle, nodes);
                    idx += match.Length;
                }
                else
                {
                    int nextTag = text.IndexOf('<', idx);
                    if (nextTag == -1) nextTag = text.Length;
                    string plain = text.Substring(idx, nextTag - idx);
                    if (!string.IsNullOrEmpty(plain))
                        nodes.Add(new ParsedNode { Type = "text", Content = plain, Style = style });
                    idx = nextTag;
                }
            }
        }
        private static float ParseFontSize(string sizeAttr)
        {
            if (string.IsNullOrEmpty(sizeAttr)) return ImGui.GetFontSize();
            if (float.TryParse(sizeAttr, out float sz)) return sz;
            return ImGui.GetFontSize();
        }
        public static ParsedNode ParseHtmlLayout(string text)
        {
            var root = new ParsedNode { Type = "root" };
            int idx = 0;
            while (idx < text.Length)
            {
                // ... existing tag parsing for nav, table, img, color, url, scale ...

                // Plain text and style tags
                int nextTagIdx = text.IndexOf('<', idx);
                if (nextTagIdx == -1) nextTagIdx = text.Length;
                string plainText = text.Substring(idx, nextTagIdx - idx);
                if (!string.IsNullOrWhiteSpace(plainText))
                    ParseStyledText(plainText, new TextStyle(), root.Children);
                idx = nextTagIdx;
            }
            return root;
        }
        // Add at the top of the Misc class:
        private static Dictionary<string, int> _tabIndices = new(); // Stores current tab index per unique tab group

        private static Dictionary<string, int> _navIndices = new(); // Stores current tab index per navigation group

        public static void RenderParsedLayout(ParsedNode node, float wrapWidth, float wrapHeight, bool url, bool image, bool color)
        {
            foreach (var child in node.Children)
            {
                switch (child.Type)
                {
                    case "nav":
                        // Only render current page (add your navigation logic here)
                        int pageIdx = 0;
                        if (child.Children.Count > pageIdx)
                            RenderParsedLayout(child.Children[pageIdx], wrapWidth, wrapHeight, url, image, color);
                        break;
                    case "page":
                        RenderParsedLayout(child.Children[0], wrapWidth, wrapHeight, url, image, color);
                        break;
                    case "table":
                        if (ImGui.BeginTable("CustomTable", child.Children.Count, ImGuiTableFlags.None))
                        {
                            ImGui.TableNextRow();
                            for (int col = 0; col < child.Children.Count; col++)
                            {
                                ImGui.TableSetColumnIndex(col);
                                ImGui.BeginGroup();
                                RenderParsedLayout(child.Children[col], wrapWidth / child.Children.Count, wrapHeight, url, image, color);
                                ImGui.EndGroup();
                            }
                            ImGui.EndTable();
                        }
                        break;
                    case "column":
                        foreach (var colChild in child.Children)
                            RenderParsedLayout(colChild, wrapWidth, wrapHeight, url, image, color);
                        break;
                    case "img":
                        if (image && _imageCache.TryGetValue(child.Content, out var texture) && texture != null && texture.Handle != IntPtr.Zero)
                        {
                            Vector2 imgSize = new Vector2(texture.Width, texture.Height);
                            imgSize.X = Math.Max(imgSize.X, minImageSize);
                            imgSize.Y = Math.Max(imgSize.Y, minImageSize);
                            ImGui.Image(texture.Handle, imgSize);
                        }
                        else
                        {
                            ImGui.TextColored(new Vector4(1, 1, 0, 1), "[Loading image or image failed!]");
                        }
                        break;
                    case "color":
                        if (color && TryParseHexColor(child.ColorHex, out Vector4 colorVal))
                            ImGui.TextColored(colorVal, child.Content);
                        else
                            ImGui.Text(child.Content);
                        break;
                    case "url":
                        if (url)
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.2f, 0.5f, 1f, 1f));
                            ImGui.Text(child.Content);
                            ImGui.PopStyleColor();
                        }
                        else
                        {
                            ImGui.Text(child.Content);
                        }
                        break;
                    case "scale":
                        ImGui.PushFont(FontSizes.TryGetValue((int)Math.Round(child.Scale), out var font) ? font : ImGui.GetFont());
                        foreach (var scaleChild in child.Children)
                            RenderParsedLayout(scaleChild, wrapWidth, wrapHeight, url, image, color);
                        ImGui.PopFont();
                        break;
                    case "text":
                        ImFontPtr Font = ImGui.GetFont();
                        if (child.Style.Bold && FontSizes.TryGetValue(18, out var boldFont)) Font = boldFont;
                        if (child.Style.Italic && FontSizes.TryGetValue(16, out var italicFont)) Font = italicFont;
                        if (child.Style.Scale > 0 && FontSizes.TryGetValue(child.Style.Scale, out var scaledFont)) Font = scaledFont;

                        ImGui.PushFont(Font);

                        if (child.Style.Color.HasValue)
                            ImGui.TextColored(child.Style.Color.Value, child.Content);
                        else
                            ImGui.TextWrapped(child.Content);

                        ImGui.PopFont();

                        if (child.Style.Underline)
                        {
                            var min = ImGui.GetItemRectMin();
                            var max = ImGui.GetItemRectMax();
                            var drawList = ImGui.GetWindowDrawList();
                            drawList.AddLine(
                                new Vector2(min.X, max.Y),
                                new Vector2(max.X, max.Y),
                                ImGui.GetColorU32(child.Style.Color ?? new Vector4(1, 1, 1, 1)),
                                2.0f
                            );
                        }
                        break;
                }
            }
        }

        public static void RenderHtmlElements(string text, bool url, bool image, bool color, bool isLimited, Vector2? overrideWrapSize = null, bool disableWordWrap = false)
        {
            Vector2 wrapSize = overrideWrapSize ?? (ImGui.GetWindowSize() - new Vector2(50, 0));
            float wrapWidth = wrapSize.X;
            float wrapHeight = wrapSize.Y;

            // Navigation and page support
            var navRegex = new Regex(@"<(navigation|nav)(?:\s+id\s*=\s*""([^""]+)"")?\s*>(.*?)</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var navMatches = navRegex.Matches(text);

            int lastIndex = 0;
            int navBlockCount = 0;
            foreach (Match navMatch in navMatches)
            {
                // Render content before this navigation block
                if (navMatch.Index > lastIndex)
                {
                    string beforeNav = text.Substring(lastIndex, navMatch.Index - lastIndex);
                    if (!string.IsNullOrWhiteSpace(beforeNav))
                        RenderHtmlElementsNoSameline(beforeNav, url, image, color, wrapWidth, wrapHeight, isLimited, true, disableWordWrap);
                }

                string navId = navMatch.Groups[2].Success && !string.IsNullOrWhiteSpace(navMatch.Groups[2].Value)
                    ? navMatch.Groups[2].Value
                    : $"nav_{navBlockCount}";

                string navContent = navMatch.Groups[3].Value;

                // Parse <page title="...">...</page> blocks inside navigation
                var pageRegex = new Regex(@"<page\s+title\s*=\s*""([^""]+)""\s*>(.*?)</page>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                var pageMatches = pageRegex.Matches(navContent);

                List<string> pageTitles = new();
                List<string> pageContents = new();

                foreach (Match pageMatch in pageMatches)
                {
                    pageTitles.Add(pageMatch.Groups[1].Value);
                    pageContents.Add(pageMatch.Groups[2].Value);
                }

                if (!_navIndices.ContainsKey(navId))
                    _navIndices[navId] = 0;
                int currentPage = _navIndices[navId];

                using (ImRaii.Child("Navigation_" + navId, new Vector2(ImGui.GetWindowSize().X, ImGui.GetIO().FontGlobalScale * 32), true))
                {
                    if (currentPage > 0)
                    {
                        if (ImGui.Button("《 "))
                        {
                            _navIndices[navId] = currentPage - 1;
                            currentPage = _navIndices[navId];
                        }
                    }

                    ImGui.SameLine();
                    if (pageTitles.Count > currentPage)
                    {
                        ImGui.SetCursorPosX(ImGui.GetWindowSize().X / 2 - ImGui.CalcTextSize(pageTitles[currentPage].ToUpper()).X / 2);
                        ImGui.TextUnformatted(pageTitles[currentPage].ToUpper());
                    }
                    ImGui.SameLine();
                    if (currentPage < pageContents.Count - 1)
                    {
                        Misc.RenderAlignmentToRight(" 》");
                        if (ImGui.Button(" 》"))
                        {
                            _navIndices[navId] = currentPage + 1;
                            currentPage = _navIndices[navId];
                        }
                    }
                }

                // Render current page content
                if (pageContents.Count > currentPage)
                {
                    RenderHtmlElementsNoSameline(pageContents[currentPage], url, image, color, wrapWidth, wrapHeight, isLimited, true, disableWordWrap);
                }

                lastIndex = navMatch.Index + navMatch.Length;
                navBlockCount++;
            }

            // Render any content after the last navigation block
            if (lastIndex < text.Length)
            {
                string afterNav = text.Substring(lastIndex);
                if (!string.IsNullOrWhiteSpace(afterNav))
                    RenderHtmlElementsNoSameline(afterNav, url, image, color, wrapWidth, wrapHeight, isLimited, true, disableWordWrap);
            }
        }
        private static bool TryParseHexColor(string hex, out Vector4 color)
        {
            color = new Vector4(1, 1, 1, 1);
            if (hex.StartsWith("#"))
                hex = hex.Substring(1);
            if (hex.Length != 6)
                return false;
            if (int.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out int r) &&
                int.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out int g) &&
                int.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out int b))
            {
                color = new Vector4(r / 255f, g / 255f, b / 255f, 1);
                return true;
            }
            return false;
        }
        // ... (other helpers unchanged) ...
        private static void RenderStyledText(string input, TextStyle style)
        {
            int idx = 0;
            int inputLen = input.Length;
            while (idx < inputLen)
            {
                int openTag = input.IndexOf('<', idx);
                if (openTag == -1 || openTag + 1 >= inputLen)
                {
                    // No more tags, render the rest
                    string plain = input.Substring(idx);
                    if (!string.IsNullOrEmpty(plain))
                    {
                        RenderStyledTextSegment(plain, style);
                    }
                    break;
                }

                // Render text before the tag
                if (openTag > idx)
                {
                    string plain = input.Substring(idx, openTag - idx);
                    if (!string.IsNullOrEmpty(plain))
                    {
                        RenderStyledTextSegment(plain, style);
                    }
                }

                // Try to parse a tag
                int closeTag = input.IndexOf('>', openTag + 1);
                if (closeTag == -1)
                {
                    // Malformed tag, render as plain text
                    RenderStyledTextSegment(input.Substring(openTag), style);
                    break;
                }

                string tagName = null;
                string colorHex = null;
                bool isColorTag = false;
                // Support <b>, <i>, <u>, <color hex=xxxxxx>
                if (input[openTag + 1] == 'b' && input.Substring(openTag, closeTag - openTag + 1).StartsWith("<b>"))
                    tagName = "b";
                else if (input[openTag + 1] == 'i' && input.Substring(openTag, closeTag - openTag + 1).StartsWith("<i>"))
                    tagName = "i";
                else if (input[openTag + 1] == 'u' && input.Substring(openTag, closeTag - openTag + 1).StartsWith("<u>"))
                    tagName = "u";
                else if (input.Substring(openTag, closeTag - openTag + 1).StartsWith("<color"))
                {
                    // Parse color hex
                    var hexMatch = Regex.Match(input.Substring(openTag, closeTag - openTag + 1), @"hex\s*=\s*([A-Fa-f0-9]{6})");
                    if (hexMatch.Success)
                    {
                        colorHex = hexMatch.Groups[1].Value;
                        isColorTag = true;
                        tagName = "color";
                    }
                }

                if (tagName == null)
                {
                    // Not a supported tag, render as plain text
                    RenderStyledTextSegment(input.Substring(openTag, closeTag - openTag + 1), style);
                    idx = closeTag + 1;
                    continue;
                }

                // Find closing tag
                string closeTagStr = $"</{tagName}>";
                int closeTagIdx = input.IndexOf(closeTagStr, closeTag + 1, StringComparison.OrdinalIgnoreCase);
                if (closeTagIdx == -1)
                {
                    // Malformed, treat as plain text
                    RenderStyledTextSegment(input.Substring(openTag, closeTag - openTag + 1), style);
                    idx = closeTag + 1;
                    continue;
                }

                // Get inner text
                int innerStart = closeTag + 1;
                int innerLen = closeTagIdx - innerStart;
                string inner = input.Substring(innerStart, innerLen);

                // Update style
                var newStyle = style;
                if (tagName == "b") newStyle.Bold = true;
                else if (tagName == "i") newStyle.Italic = true;
                else if (tagName == "u") newStyle.Underline = true;
                else if (isColorTag && TryParseHexColor(colorHex, out Vector4 colorVal)) newStyle.Color = colorVal;

                // Render inner text (no recursion, just loop)
                RenderStyledText(inner, newStyle);

                idx = closeTagIdx + closeTagStr.Length;
            }
        }

        // Helper to render a segment with style
        private static void RenderStyledTextSegment(string text, TextStyle style)
        {
            int styleFontSize = style.Scale > 0 ? style.Scale : minFontSize;
            if (styleFontSize < minFontSize) styleFontSize = minFontSize;
            ImFontPtr font = GetFontForStyle(style, styleFontSize);

            ImGui.PushFont(font);

            if (style.Color.HasValue)
                ImGui.TextColored(style.Color.Value, text);
            else
                ImGui.TextWrapped(text);

            ImGui.PopFont();

            if (style.Underline)
            {
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                var drawList = ImGui.GetWindowDrawList();
                drawList.AddLine(
                    new Vector2(min.X, max.Y),
                    new Vector2(max.X, max.Y),
                    ImGui.GetColorU32(style.Color ?? new Vector4(1, 1, 1, 1)),
                    2.0f
                );
            }
        }

        // No changes needed for RenderHtmlElementsNoSameline and RenderHtmlElementsNoTable unless you want to support nested tabs/pages inside tables or other elements.
        // If you do, you can add similar tab/page parsing logic to those functions as well.

        private static void RenderHtmlElementsNoSameline(string text, bool url, bool image, bool color, float wrapWidth, float wrapHeight, bool isFirstSegment, bool isLimited, bool disableWordWrap)
        {
            var tableRegex = new Regex(@"<table>(.*?)</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            int lastIndex = 0;
            var tableMatches = tableRegex.Matches(text);

            if (tableMatches.Count == 0)
            {
                RenderHtmlElementsNoTable(text, url, image, color, wrapWidth, wrapHeight, isLimited, isFirstSegment, disableWordWrap);
                return;
            }

            bool firstTable = isFirstSegment;
            foreach (Match tableMatch in tableMatches)
            {
                int tableStart = tableMatch.Index;
                if (tableStart > lastIndex)
                {
                    string beforeTable = text.Substring(lastIndex, tableStart - lastIndex);
                    RenderHtmlElementsNoTable(beforeTable, url, image, color, wrapWidth, wrapHeight, isLimited, firstTable, disableWordWrap);
                    firstTable = false;
                }

                string tableContent = tableMatch.Groups[1].Value;

                var columns = new List<(string content, string tooltip)>();
                int idx = 0;
                while (idx < tableContent.Length)
                {
                    int colStart = tableContent.IndexOf("<column>", idx, StringComparison.OrdinalIgnoreCase);
                    if (colStart == -1) break;
                    int colEnd = tableContent.IndexOf("</column>", colStart, StringComparison.OrdinalIgnoreCase);
                    if (colEnd == -1) break;
                    int contentStart = colStart + "<column>".Length;
                    string colContent = tableContent.Substring(contentStart, colEnd - contentStart).TrimStart('\r', '\n', ' ', '\t');
                    idx = colEnd + "</column>".Length;

                    // Check for tooltip immediately after column
                    string tooltip = null;
                    var tooltipMatch = new Regex(@"<tooltip>(.*?)</tooltip>", RegexOptions.Singleline | RegexOptions.IgnoreCase)
                        .Match(tableContent, idx);
                    if (tooltipMatch.Success && tooltipMatch.Index == idx)
                    {
                        tooltip = tooltipMatch.Groups[1].Value;
                        idx = tooltipMatch.Index + tooltipMatch.Length;
                    }

                    columns.Add((colContent, tooltip));
                }

                int columnCount = columns.Count;
                if (columnCount > 0 && ImGui.BeginTable("CustomTable" + tableMatch.Index, columnCount, ImGuiTableFlags.None))
                {
                    ImGui.TableNextRow();
                    for (int col = 0; col < columnCount; col++)
                    {
                        ImGui.TableSetColumnIndex(col);

                        var colText = columns[col].content;
                        var tooltip = columns[col].tooltip; 
                        ImGui.BeginGroup();
                        float columnWidth = wrapWidth / columnCount;
                        // Use NoSameline to allow nested tags (including <scale>, <img>, etc.)
                        RenderHtmlElementsNoSameline(colText, url, image, color, columnWidth, wrapHeight, isLimited, true, disableWordWrap);
                        ImGui.EndGroup();
                        if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.TextUnformatted(tooltip);
                            ImGui.EndTooltip();
                        }
                    }
                    ImGui.EndTable();
                }

                lastIndex = tableMatch.Index + tableMatch.Length;
            }

            if (lastIndex < text.Length)
            {
                string afterTable = text.Substring(lastIndex);
                RenderHtmlElementsNoTable(afterTable, url, image, color, wrapWidth, wrapHeight, isLimited, false, disableWordWrap);
            }
        }
        private static void RenderHtmlElementsNoTable(
            string text,
            bool url,
            bool image,
            bool color,
            float wrapWidth,
            float wrapHeight,
            bool isFirstSegment,
            bool isLimited,
            bool disableWordWrap)
        {
      
            var scaleBlockRegex = new Regex(@"<scale\s*=\s*""([\d\.]+)""\s*>(.*?)</scale>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var imgRegex = new Regex(@"<(img|image)>(.*?)</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var colorRegex = new Regex(@"<color\s+hex\s*=\s*([A-Fa-f0-9]{6})\s*>(.*?)</color>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var urlRegex = new Regex(@"<url>(.*?)</url>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var tooltipRegex = new Regex(@"<tooltip>(.*?)</tooltip>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var boldRegex = new Regex(@"<b>(.*?)</b>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var italicRegex = new Regex(@"<i>(.*?)</i>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var underlineRegex = new Regex(@"<u>(.*?)</u>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            var segments = new List<(string type, string content, float scale, string colorHex, string url)>();
            int idx = 0;
            while (idx < text.Length)
            {
                if (text.Substring(idx).StartsWith("</"))
                {
                    int closeIdx = text.IndexOf('>', idx);
                    if (closeIdx != -1)
                    {
                        idx = closeIdx + 1;
                        continue;
                    }
                }
                // Find the next tag
                var scaleMatch = scaleBlockRegex.Match(text, idx);
                var imgMatch = imgRegex.Match(text, idx);
                var colorMatch = colorRegex.Match(text, idx);
                var urlMatch = urlRegex.Match(text, idx);
                var tooltipMatch = tooltipRegex.Match(text, idx);
                var boldMatch = boldRegex.Match(text, idx);
                var italicMatch = italicRegex.Match(text, idx);
                var underlineMatch = underlineRegex.Match(text, idx);

                // Find the earliest tag
                int nextTagIdx = text.Length;
                string nextType = null;
                Match nextMatch = null;
                if (scaleMatch.Success && scaleMatch.Index < nextTagIdx) { nextTagIdx = scaleMatch.Index; nextType = "scale"; nextMatch = scaleMatch; }
                if (imgMatch.Success && imgMatch.Index < nextTagIdx) { nextTagIdx = imgMatch.Index; nextType = "img"; nextMatch = imgMatch; }
                if (colorMatch.Success && colorMatch.Index < nextTagIdx) { nextTagIdx = colorMatch.Index; nextType = "color"; nextMatch = colorMatch; }
                if (urlMatch.Success && urlMatch.Index < nextTagIdx) { nextTagIdx = urlMatch.Index; nextType = "url"; nextMatch = urlMatch; }
                if (tooltipMatch.Success && tooltipMatch.Index < nextTagIdx) { nextTagIdx = tooltipMatch.Index; nextType = "tooltip"; nextMatch = tooltipMatch; }
                if (boldMatch.Success && boldMatch.Index < nextTagIdx) { nextTagIdx = boldMatch.Index; nextType = "bold"; nextMatch = boldMatch; }
                if (italicMatch.Success && italicMatch.Index < nextTagIdx) { nextTagIdx = italicMatch.Index; nextType = "italic"; nextMatch = italicMatch; }
                if (underlineMatch.Success && underlineMatch.Index < nextTagIdx) { nextTagIdx = underlineMatch.Index; nextType = "underline"; nextMatch = underlineMatch; }

                // Add only the text before the tag
                if (nextTagIdx > idx)
                {
                    string plainText = text.Substring(idx, nextTagIdx - idx);
                    if (!string.IsNullOrEmpty(plainText))
                        segments.Add(("text", plainText, 1.0f, null, null));
                }

                if (nextMatch == null)
                    break;

                // Handle the tag and skip over it
                if (nextType == "scale")
                {
                    float scale = 1.0f;
                    float.TryParse(nextMatch.Groups[1].Value, out scale);
                    if (scale < minFontSize) scale = minFontSize;
                    string scaleContent = nextMatch.Groups[2].Value;
                    // Recursively parse scaleContent for tags
                    int scaleIdx = 0;
                    while (scaleIdx < scaleContent.Length)
                    {
                        var imgMatch2 = imgRegex.Match(scaleContent, scaleIdx);
                        var colorMatch2 = colorRegex.Match(scaleContent, scaleIdx);
                        var urlMatch2 = urlRegex.Match(scaleContent, scaleIdx);
                        var tooltipMatch2 = tooltipRegex.Match(scaleContent, scaleIdx);
                        var boldMatch2 = boldRegex.Match(scaleContent, scaleIdx);
                        var italicMatch2 = italicRegex.Match(scaleContent, scaleIdx);
                        var underlineMatch2 = underlineRegex.Match(scaleContent, scaleIdx);

                        int nextTagIdx2 = scaleContent.Length;
                        string nextType2 = null;
                        Match nextMatch2 = null;
                        if (imgMatch2.Success && imgMatch2.Index < nextTagIdx2) { nextTagIdx2 = imgMatch2.Index; nextType2 = "img"; nextMatch2 = imgMatch2; }
                        if (colorMatch2.Success && colorMatch2.Index < nextTagIdx2) { nextTagIdx2 = colorMatch2.Index; nextType2 = "color"; nextMatch2 = colorMatch2; }
                        if (urlMatch2.Success && urlMatch2.Index < nextTagIdx2) { nextTagIdx2 = urlMatch2.Index; nextType2 = "url"; nextMatch2 = urlMatch2; }
                        if (tooltipMatch2.Success && tooltipMatch2.Index < nextTagIdx2) { nextTagIdx2 = tooltipMatch2.Index; nextType2 = "tooltip"; nextMatch2 = tooltipMatch2; }
                        if (boldMatch2.Success && boldMatch2.Index < nextTagIdx2) { nextTagIdx2 = boldMatch2.Index; nextType2 = "bold"; nextMatch2 = boldMatch2; }
                        if (italicMatch2.Success && italicMatch2.Index < nextTagIdx2) { nextTagIdx2 = italicMatch2.Index; nextType2 = "italic"; nextMatch2 = italicMatch2; }
                        if (underlineMatch2.Success && underlineMatch2.Index < nextTagIdx2) { nextTagIdx2 = underlineMatch2.Index; nextType2 = "underline"; nextMatch2 = underlineMatch2; }

                        if (nextTagIdx2 > scaleIdx)
                        {
                            string plainText2 = scaleContent.Substring(scaleIdx, nextTagIdx2 - scaleIdx);
                            if (!string.IsNullOrEmpty(plainText2))
                                segments.Add(("text", plainText2, scale, null, null));
                        }

                        if (nextMatch2 == null)
                            break;

                        if (nextType2 == "img")
                        {
                            string imgUrl = nextMatch2.Groups[2].Value.Trim();
                            float imgScale = scale < 0.01f ? 0.01f : scale;
                            segments.Add(("img", imgUrl, imgScale, null, null));
                            scaleIdx = nextMatch2.Index + nextMatch2.Length;
                        }
                        else if (nextType2 == "color")
                        {
                            string colorContent = nextMatch2.Groups[2].Value;
                            string colorHex = nextMatch2.Groups[1].Value;
                            segments.Add(("color", colorContent, scale, colorHex, null));
                            scaleIdx = nextMatch2.Index + nextMatch2.Length;
                        }
                        else if (nextType2 == "url")
                        {
                            string urlContent = nextMatch2.Groups[1].Value;
                            segments.Add(("url", urlContent, scale, null, urlContent));
                            scaleIdx = nextMatch2.Index + nextMatch2.Length;
                        }
                        else if (nextType2 == "tooltip")
                        {
                            segments.Add(("tooltip", nextMatch2.Groups[1].Value, scale, null, null));
                            scaleIdx = nextMatch2.Index + nextMatch2.Length;
                        }
                        else if (nextType2 == "bold")
                        {
                            segments.Add(("bold", nextMatch2.Groups[1].Value, scale, null, null));
                            scaleIdx = nextMatch2.Index + nextMatch2.Length;
                        }
                        else if (nextType2 == "italic")
                        {
                            segments.Add(("italic", nextMatch2.Groups[1].Value, scale, null, null));
                            scaleIdx = nextMatch2.Index + nextMatch2.Length;
                        }
                        else if (nextType2 == "underline")
                        {
                            segments.Add(("underline", nextMatch2.Groups[1].Value, scale, null, null));
                            scaleIdx = nextMatch2.Index + nextMatch2.Length;
                        }
                    }
                    idx = nextMatch.Index + nextMatch.Length;
                }
                else if (nextType == "img")
                {
                    string imgUrl = nextMatch.Groups[2].Value.Trim();
                    segments.Add(("img", imgUrl, 1.0f, null, null));
                    idx = nextMatch.Index + nextMatch.Length;
                }
                else if (nextType == "color")
                {
                    string colorContent = nextMatch.Groups[2].Value;
                    string colorHex = nextMatch.Groups[1].Value;
                    segments.Add(("color", colorContent, 1.0f, colorHex, null));
                    idx = nextMatch.Index + nextMatch.Length;
                }
                else if (nextType == "url")
                {
                    string urlContent = nextMatch.Groups[1].Value;
                    segments.Add(("url", urlContent, 1.0f, null, urlContent));
                    idx = nextMatch.Index + nextMatch.Length;
                }
                else if (nextType == "tooltip")
                {
                    segments.Add(("tooltip", nextMatch.Groups[1].Value, 1.0f, null, null));
                    idx = nextMatch.Index + nextMatch.Length;
                }
                else if (nextType == "bold")
                {
                    segments.Add(("bold", nextMatch.Groups[1].Value, 1.0f, null, null));
                    idx = nextMatch.Index + nextMatch.Length;
                }
                else if (nextType == "italic")
                {
                    segments.Add(("italic", nextMatch.Groups[1].Value, 1.0f, null, null));
                    idx = nextMatch.Index + nextMatch.Length;
                }
                else if (nextType == "underline")
                {
                    segments.Add(("underline", nextMatch.Groups[1].Value, 1.0f, null, null));
                    idx = nextMatch.Index + nextMatch.Length;
                }
            }

            string pendingTooltip = null;
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];

                // Image rendering (clamped)
                if (seg.type == "img" && image)
                {
                    string imgUrl = seg.content;
                    float imageScale = seg.scale < 0.01f ? 0.01f : seg.scale;

                    if (_imageCache.TryGetValue(imgUrl, out var texture) &&
                        texture != null &&
                        texture.Handle != IntPtr.Zero &&
                        texture.Width > 0 &&
                        texture.Height > 0)
                    {
                        Vector2 imgSize = new Vector2(texture.Width, texture.Height) * imageScale;

                        // Additional scaling logic
                        if (disableWordWrap)
                        {
                            float maxWidth = 400f;
                            if (imgSize.X > maxWidth && imgSize.X > 0)
                            {
                                float scaleDown = maxWidth / imgSize.X;
                                imgSize *= scaleDown;
                            }
                        }
                        else if (isLimited)
                        {
                            float maxWidth = wrapWidth;
                            float maxHeight = wrapHeight;
                            if (imgSize.X > 0 && imgSize.Y > 0)
                            {
                                float widthScale = maxWidth / imgSize.X;
                                float heightScale = maxHeight / imgSize.Y;
                                float finalScale = Math.Min(widthScale, heightScale);
                                imgSize *= finalScale;
                            }
                        }
                        else
                        {
                            float maxWidth = wrapWidth;
                            if (imgSize.X > maxWidth && imgSize.X > 0)
                            {
                                float scaleDown = maxWidth / imgSize.X;
                                imgSize *= scaleDown;
                            }
                        }

                        // Clamp to minimum image size
                        imgSize.X = Math.Max(imgSize.X, minImageSize);
                        imgSize.Y = Math.Max(imgSize.Y, minImageSize);

                        if (imgSize.X > 0 && imgSize.Y > 0)
                        {
                            ImGui.Image(texture.Handle, imgSize);
                        }
                        else
                        {
                            ImGui.TextColored(new Vector4(1, 1, 0, 1), "[Image size invalid]");
                        }
                    }
                    else
                    {
                        if (!_imagesLoading.Contains(imgUrl))
                        {
                            _imagesLoading.Add(imgUrl);
                            // Use async void for fire-and-forget image loading
                            async void LoadImageAsync(string url)
                            {
                                try
                                {
                                    using (var webClient = new System.Net.WebClient())
                                    {
                                        var imageBytes = await webClient.DownloadDataTaskAsync(url);
                                        var tex = await Plugin.TextureProvider.CreateFromImageAsync(imageBytes);
                                        if (tex != null && tex.Handle != IntPtr.Zero &&
                                            tex.Width > 0 && tex.Height > 0)
                                        {
                                            _imageCache[url] = tex;
                                        }
                                        else
                                        {
                                            _imageCache[url] = null;
                                        }
                                    }
                                }
                                catch
                                {
                                    _imageCache[url] = null;
                                }
                                finally
                                {
                                    _imagesLoading.Remove(url);
                                }
                            }
                            LoadImageAsync(imgUrl);
                        }
                        ImGui.TextColored(new Vector4(1, 1, 0, 1), "[Loading image or image failed!]");
                    }
                    continue;
                }
   

                // Font rendering (clamped)
                if (seg.type == "text")
                {
                    int fontSize = (int)Math.Round(seg.scale);
                    if (fontSize < minFontSize) fontSize = minFontSize;
                    ImFontPtr font = null;
                    if (FontSizes.TryGetValue(fontSize, out font) && !font.IsNull)
                    {
                        ImGui.PushFont(font);
                    }

                    if (disableWordWrap)
                        ImGui.TextUnformatted(seg.content);
                    else
                        ImGui.TextWrapped(seg.content);

                    if (!font.IsNull)
                        ImGui.PopFont();

                    continue;
                }

                // Colored text
                if (seg.type == "color" && color && seg.colorHex != null)
                {
                    if (TryParseHexColor(seg.colorHex, out Vector4 colorVal))
                    {
                        if (disableWordWrap)
                            ImGui.TextColored(colorVal, seg.content);
                        else
                        {
                            string wrapped = WrapTextToFit(seg.content, wrapWidth);
                            foreach (var line in wrapped.Split('\n'))
                                ImGui.TextColored(colorVal, line);
                        }
                    }
                    else
                    {
                        if (disableWordWrap)
                            ImGui.TextUnformatted(seg.content);
                        else
                            ImGui.TextWrapped(seg.content);
                    }
                    continue;
                }

                else if (seg.type == "url" && url && seg.url != null)
                {
                    string wrapped = WrapTextToFit(seg.content, wrapWidth);
                    foreach (var line in wrapped.Split('\n'))
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.2f, 0.5f, 1f, 1f));
                        ImGui.Text(line);
                        ImGui.PopStyleColor();

                        if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                        {
                            Misc.LoadUrl = true;
                            Misc.UrlToLoad = seg.url;
                        }
                    }
                }
                else if (seg.type == "bold" || seg.type == "italic" || seg.type == "underline" || seg.type == "text")
                {
                    var style = new TextStyle
                    {
                        Bold = seg.type == "bold",
                        Italic = seg.type == "italic",
                        Underline = seg.type == "underline",
                        Scale = (int)Math.Round(seg.scale) // <-- Use scale here!
                    };
                    int fontSize = style.Scale > 0 ? style.Scale : minFontSize;
                    if (fontSize < minFontSize) fontSize = minFontSize;
                    ImFontPtr font = GetFontForStyle(style, fontSize);
                    ImGui.PushFont(font);

                    if (seg.colorHex != null && color && TryParseHexColor(seg.colorHex, out Vector4 colorVal))
                        ImGui.TextColored(colorVal, seg.content);
                    else
                        ImGui.TextWrapped(seg.content);

                    ImGui.PopFont();

                    if (style.Underline)
                    {
                        var min = ImGui.GetItemRectMin();
                        var max = ImGui.GetItemRectMax();
                        var drawList = ImGui.GetWindowDrawList();
                        drawList.AddLine(
                            new Vector2(min.X, max.Y),
                            new Vector2(max.X, max.Y),
                            ImGui.GetColorU32(new Vector4(1, 1, 1, 1)),
                            2.0f
                        );
                    }
                    continue;
                }

                // Tooltip
                if (seg.type == "tooltip")
                {
                    pendingTooltip = seg.content;
                    continue;
                }
            }
        }
        public static void RenderUrlModalPopup()
        {
            if (LoadUrl && !showUrlPopup && !string.IsNullOrEmpty(UrlToLoad))
            {
                ImGui.OpenPopup("Opening URL");
                showUrlPopup = true;
            }

            bool wasOpen = showUrlPopup;
            if (ImGui.BeginPopupModal("Opening URL", ref showUrlPopup, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text("Do you want to open this link?");
                ImGui.Checkbox("Allow Link", ref allow);

                if (allow)
                {
                    if (ImGui.Button("I Trust URL"))
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = UrlToLoad,
                                UseShellExecute = true
                            });
                        }
                        catch { }
                        UrlToLoad = string.Empty;
                        LoadUrl = false;
                        allow = false;
                        showUrlPopup = false;
                        ImGui.CloseCurrentPopup();
                    }
                }

                if (ImGui.Button("Cancel"))
                {
                    UrlToLoad = string.Empty;
                    LoadUrl = false;
                    allow = false;
                    showUrlPopup = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            // Handle closing via the "X" button
            if (wasOpen && !showUrlPopup)
            {
                UrlToLoad = string.Empty;
                LoadUrl = false;
                allow = false;
            }
        }
        private static void RenderHtmlTextSegment(string text, bool url, bool color, float wrapWidth)
        {
            var htmlDoc = new HtmlAgilityPack.HtmlDocument();
            htmlDoc.LoadHtml(WrapTextToFit(text, wrapWidth));

            bool nextSameLine = false;
            foreach (var node in htmlDoc.DocumentNode.ChildNodes)
            {
                string nodeText = node.InnerText.Replace("\r", "");
                string[] lines = nodeText.Split('\n');

                for (int i = 0; i < lines.Length; i++)
                {
                    if (nextSameLine)
                    {
                        ImGui.SameLine(0, 0);
                        nextSameLine = false;
                    }
                    if (node.Name == "color" && color && node.Attributes["hex"] != null)
                    {
                        var hexColor = node.Attributes["hex"].Value;
                        if (TryParseHexColor(hexColor, out Vector4 colorVal))
                            ImGui.TextColored(colorVal, lines[i]);
                        else
                            ImGui.TextUnformatted(lines[i]);
                    }
                    else if (node.Name == "url" && url)
                    {
                        string urlText = lines[i].Trim();
                        if (!string.IsNullOrWhiteSpace(urlText))
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.2f, 0.5f, 1f, 1f));
                            ImGui.Text(urlText);
                            ImGui.PopStyleColor();

                            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                            {
                                try
                                {
                                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                    {
                                        FileName = urlText,
                                        UseShellExecute = true
                                    });
                                }
                                catch { }
                            }
                        }
                    }
                    else if (node.Name == "sameline")
                    {
                        nextSameLine = true;
                        // Render the content of the sameline node inline
                        RenderHtmlTextSegment(node.InnerHtml, url, color, wrapWidth);
                    }
                    else
                    {
                        ImGui.TextUnformatted(lines[i]);
                    }
                }
            }
        }
        //sets position of content to center
        public static void RenderHtmlColoredTextInline(string text, float? overrideWrapWidth = null)
        {
            // Get the available width for wrapping (subtract a little for padding if needed)
            float wrapWidth = overrideWrapWidth ?? (ImGui.GetWindowSize().X - 10);

            string wrappedText = WrapTextToFit(text, wrapWidth);

            var htmlDoc = new HtmlAgilityPack.HtmlDocument();
            htmlDoc.LoadHtml(wrappedText);

            bool first = true;
            foreach (var node in htmlDoc.DocumentNode.ChildNodes)
            {
                string nodeText = node.InnerText.Replace("\r", "");
                string[] lines = nodeText.Split('\n');

                for (int i = 0; i < lines.Length; i++)
                {
                    // Only use SameLine if not the first segment and not after a line break
                    if (!first) ImGui.SameLine(0, 0);

                    if (node.Name == "color" && node.Attributes["hex"] != null)
                    {
                        var hexColor = node.Attributes["hex"].Value;
                        if (TryParseHexColor(hexColor, out Vector4 color))
                            ImGui.TextColored(color, lines[i]);
                        else
                            ImGui.TextUnformatted(lines[i]);
                    }
                    else
                    {
                        ImGui.TextUnformatted(lines[i]);
                    }

                    // If this is a line break, reset first so next segment starts a new line
                    first = (i < lines.Length - 1);
                }
            }
        }
    
        public static void SetCenter(Plugin plugin, string name)
        {
         
                int NameWidth = name.Length * 6;
                var decidingWidth = Math.Max(500, ImGui.GetWindowWidth());
                var offsetWidth = (decidingWidth - NameWidth) / 2;
                var offsetVersion = name.Length > 0
                    ? _modVersionWidth + ImGui.GetStyle().ItemSpacing.X + ImGui.GetStyle().WindowPadding.X
                    : 0;
                var offset = Math.Max(offsetWidth, offsetVersion);
                if (offset > 0)
                {
                    ImGui.SetCursorPosX(offset);
                }
        }
        public static string ExtractTextBetweenTags(string input, string tag)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(tag))
                return null;

            string pattern = $@"<{tag}>(.*?)</{tag}>";
            Match match = Regex.Match(input, pattern, RegexOptions.Singleline);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return null; // Return null if the tag is not found
        }
        public static void DrawCenteredInput(float center, Vector2 size, string label, string hint, ref string input, int length, ImGuiInputTextFlags flags)
        {
            var currentCursorY = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(new Vector2(center, currentCursorY));
            ImGui.PushItemWidth(size.X);
            ImGui.InputTextWithHint(label, hint, ref input, length, flags & (string.IsNullOrEmpty(input) ? ~ImGuiInputTextFlags.Password : ~ImGuiInputTextFlags.None));
            ImGui.PopItemWidth();
        }
        public static bool DrawXCenteredInput(string label, string id, ref string input, int length)
        {
            var size = ImGui.CalcTextSize(label).X + 400;

            var windowSize = ImGui.GetWindowSize();

            // Set the cursor position to center the button horizontally
            var currentCursorY = ImGui.GetCursorPosY();
            float centeredX = (ImGui.GetContentRegionAvail().X - size) / 2.0f;
            ImGui.SetCursorPos(new Vector2(centeredX, currentCursorY));
            ImGui.Text(label);
            ImGui.SameLine();
            ImGui.PushItemWidth(350);
            var centeredInput = ImGui.InputText("##ID" + id, ref input, length, ImGuiInputTextFlags.None);
            ImGui.PopItemWidth();
            return centeredInput;
        }
     
        public static void EditImage(Plugin plugin, FileDialogManager _fileDialogManager, GalleryLayout layout, bool avatar, bool background, int imageIndex)
        {
            _fileDialogManager.OpenFileDialog("Select Image", "Image{.png,.jpg}", (s, f) =>
            {
                if (!s)
                    return;
                var imagePath = f[0].ToString();
                var image = Path.GetFullPath(imagePath);
                var imageBytes = File.ReadAllBytes(image);
                if (avatar == true)
                {
                    ProfileWindow.avatarBytes = imageBytes;
                    ProfileWindow.currentAvatarImg = Plugin.TextureProvider.CreateFromImageAsync(Imaging.ScaleImageBytes( ProfileWindow.avatarBytes, 100,100)).Result;
                }
                else if(background == true)
                {
                    ProfileWindow.backgroundBytes = imageBytes;
                    ProfileWindow.backgroundImage = Plugin.TextureProvider.CreateFromImageAsync(Imaging.ScaleImageBytes(ProfileWindow.backgroundBytes, 1000, 1500)).Result;
                }
                else
                {
                    layout.images[imageIndex].imageBytes = imageBytes;
                    layout.images[imageIndex].thumbnail = Plugin.TextureProvider.CreateFromImageAsync(Imaging.ScaleImageBytes(imageBytes, 250, 250)).Result;
                    layout.images[imageIndex].image = Plugin.TextureProvider.CreateFromImageAsync(Imaging.ScaleImageBytes(imageBytes, 2000, 2000)).Result;
                }
            }, 0, null, plugin.Configuration.AlwaysOpenDefaultImport);

        }
      
        public static bool DrawCenteredButton(float center, Vector2 size, string label)
        {
            var currentCursorY = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(new Vector2(center, currentCursorY));
            ImGui.PushItemWidth(size.X);
            var button = DrawButton(label);
            ImGui.PopItemWidth();
            return button;
        }
        public static bool DrawButton(string label)
        {
            using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.3f, 0.3f, 0.3f, 1.0f))) // Light gray on hover
            {
                var button = ImGui.Button(label);
                return button;
            }
        }
        public static void DrawCenteredButtonTable(int rows, List<ProfileTab> profileTabs)
        {
            int columns = profileTabs.Count;
            // Get window size
            var windowSize = ImGui.GetWindowSize();

            // Define button size (width and height)
            var buttonSize = new System.Numerics.Vector2(100, 50);

            // Calculate total width of the button table (button width + padding between buttons)
            float totalTableWidth = (buttonSize.X * columns) + (ImGui.GetStyle().ItemSpacing.X * (columns - 1));

            // Calculate the X position to start drawing the table (centered horizontally)
            float startX = (windowSize.X - totalTableWidth) / 2;

            // Set cursor position to center the table horizontally
            ImGui.SetCursorPosX(startX);

            // Create a table layout for the buttons
            using (var table = ImRaii.Table("ButtonTable", columns))
            {
                if (table)
                {
                    int buttonIndex = 0;
                    for (int row = 0; row < rows; row++)
                    {
                        ImGui.TableNextRow(); // Move to the next row in the table

                        for (int column = 0; column < columns; column++)
                        {
                            ImGui.TableSetColumnIndex(column); // Move to the next column in the table

                            if (buttonIndex < profileTabs.Count)
                            {
                                // Draw a button
                                if (ImGui.Button(profileTabs[buttonIndex].name, buttonSize))
                                {
                                    profileTabs[buttonIndex].action();
                                    profileTabs[buttonIndex].showValue = true;
                                }
                                buttonIndex++;
                            }
                        }
                    }
                }
            }
        }


        //sets a title at the center of the window and resets the font back to default afterwards
        public static void SetTitle(Plugin plugin, bool center, string title, Vector4 borderColor)
        {
            Jupiter = Plugin.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Jupiter, 35));



            using var col = ImRaii.PushColor(ImGuiCol.Border, borderColor);
            using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 2 * ImGuiHelpers.GlobalScale);
            using var font = Jupiter.Push();
            if (center == true)
            {
                var size = ImGui.CalcTextSize(title);

                var windowSize = ImGui.GetWindowSize();

                // Set the cursor position to center the button horizontally
                float xPos = (windowSize.X - size.X - 15) / 2; // Center horizontally
                ImGui.SetCursorPosX(xPos);
            }
            UIHelpers.DrawTextButton(title, Vector2.Zero, 0);

            using var defInfFontDen = ImRaii.DefaultFont();
            using var defCol = ImRaii.DefaultColors();
            using var defStyle = ImRaii.DefaultStyle();
        }



        // Helper method to wrap text to fit within a specified width

        // WrapTextToFit now only returns the wrapped text without modifying the original input
        public static string WrapTextToFit(string inputText, float boxWidth)
        {
            if (inputText == previousInputText && boxWidth == previousBoxWidth)
                return cachedWrappedText;

            previousInputText = inputText;
            previousBoxWidth = boxWidth;

            var lines = inputText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            StringBuilder wrappedText = new StringBuilder();

            for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
            {
                var line = lines[lineIdx];

                // Preserve blank lines
                if (string.IsNullOrEmpty(line))
                {
                    wrappedText.AppendLine();
                    continue;
                }

                StringBuilder lineBuilder = new StringBuilder();
                float lineWidth = 0f;

                var words = line.Split(' ');
                bool isFirstWord = true;
                for (int w = 0; w < words.Length; w++)
                {
                    var word = words[w];
                    string wordWithSpace = isFirstWord ? word : " " + word;
                    float wordSize = ImGui.CalcTextSize(wordWithSpace).X;

                    if (lineWidth + wordSize > boxWidth && lineBuilder.Length > 0)
                    {
                        wrappedText.AppendLine(lineBuilder.ToString());
                        lineBuilder.Clear();
                        lineBuilder.Append(word);
                        lineWidth = ImGui.CalcTextSize(word).X;
                        isFirstWord = false; // After a wrap, next word is not first
                    }
                    else
                    {
                        lineBuilder.Append(wordWithSpace);
                        lineWidth += wordSize;
                        isFirstWord = false;
                    }
                }

                // Append the last part of the line (do not add extra newline if this is the last input line)
                if (lineBuilder.Length > 0)
                {
                    wrappedText.Append(lineBuilder.ToString());
                }

                // Only add a newline if this is not the last input line
                if (lineIdx < lines.Length - 1)
                    wrappedText.AppendLine();
            }

            cachedWrappedText = wrappedText.ToString();
            return cachedWrappedText;
        }
        //loader for ProfileWindow and TargetWindow
        public static void ResetLoaderTween(string key = "default")
        {
            loaderTweens.Remove(key);
        }
        public static void ResetAllData()
        {
            try
            {
                // ... existing code ...

                // Reset loader tweens for target profile loading
                Misc.ResetLoaderTween("tabs");
                Misc.ResetLoaderTween("gallery");
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("TargetProfileWindow ResetAllData Debug: " + ex.Message);
            }
        }
        private static readonly Dictionary<string, LoaderTweenState> loaderTweens = new();
        public static  bool showUrlPopup;


        public static bool IsLoaderTweening(string key = "default")
        {
            if (!loaderTweens.TryGetValue(key, out var tween))
                return false;
            return Math.Abs(tween.TweenedValue - tween.TweenTargetValue) > 0.001f;
        }
        public static void StartLoader(float value, float max, string loading, Vector2 scale, string key = "default")
        {
            value = Math.Max(0f, Math.Min(max, value));
            float now = (float)ImGui.GetTime();

            if (!loaderTweens.TryGetValue(key, out var tween))
            {
                tween = new LoaderTweenState
                {
                    TweenedValue = value,
                    TweenStartValue = value,
                    TweenTargetValue = value,
                    TweenStartTime = now
                };
                loaderTweens[key] = tween;
            }

            // If the target value changed, start a new tween
            if (Math.Abs(value - tween.TweenTargetValue) > 0.001f)
            {
                tween.TweenStartValue = tween.TweenedValue;
                tween.TweenTargetValue = value;
                tween.TweenStartTime = now;
            }

            // Calculate tween progress
            float t = Math.Min(1f, (now - tween.TweenStartTime) / tween.TweenDuration);
            t = t * t * (3f - 2f * t); // smoothstep
            tween.TweenedValue = tween.TweenStartValue + (tween.TweenTargetValue - tween.TweenStartValue) * t;

            ImGui.ProgressBar(tween.TweenedValue / max, new Vector2(scale.X - 20, ImGui.GetIO().FontGlobalScale * 20), "Loading " + loading);
        }
        public static byte[] ImageToByteArray(string imagePath)
        {
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException("Image file not found.", imagePath);
            }

            return File.ReadAllBytes(imagePath);
        }
        public static void AddIncrementBar(ImDrawListPtr drawList, float width, Vector4 color)
        {
            var cursorPos = ImGui.GetCursorScreenPos();
            // Set the rectangle size and color
            float height = 10;
            uint colorVal = ImGui.GetColorU32(color); // Solid blue color (RGBA)

            // Draw the outlined rectangle
            drawList.AddRectFilled(new Vector2(cursorPos.X, cursorPos.Y), new Vector2(cursorPos.X + width, cursorPos.Y + height), colorVal);
        }

        public static void RenderAlignmentToRight(string buttonText)
        {
            float windowWidth = ImGui.GetWindowSize().X;
            float scale = ImGui.GetIO().FontGlobalScale;

            // Calculate button width dynamically based on the label text and UI scale
            float buttonWidth = ImGui.CalcTextSize(buttonText).X + (20f * scale); // Add padding to match button appearance

            // Calculate position for right alignment, keeping it within bounds
            float buttonXPosition = Math.Max(0, windowWidth - buttonWidth);

            // Set cursor to the calculated position
            ImGui.SetCursorPosX(buttonXPosition);

        }
        internal static Vector2 CalculateTooltipScale(string tooltip, float wrapWidth = 400f)
        {
            // Helper to recursively parse and calculate size, now using WrapTextToFit for text segments
            Vector2 Parse(string text, float parentScale)
            {
                // Table support
                var tableRegex = new Regex(@"<table>(.*?)</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                var tableMatch = tableRegex.Match(text);
                if (tableMatch.Success)
                {
                    string tableContent = tableMatch.Groups[1].Value;
                    var columnRegex = new Regex(@"<column>(.*?)</column>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    var columnMatches = columnRegex.Matches(tableContent);

                    float totalWidth = 0f;
                    float maxHeight = 0f;
                    foreach (Match colMatch in columnMatches)
                    {
                        string colContent = colMatch.Groups[1].Value.Trim();
                        Vector2 colSize = Parse(colContent, parentScale);
                        totalWidth += colSize.X;
                        maxHeight = Math.Max(maxHeight, colSize.Y);
                    }
                    return new Vector2(totalWidth, maxHeight);
                }

                // Split by line breaks
                var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                float maxWidth = 0f;
                float totalHeight = 0f;
                foreach (var line in lines)
                {
                    Vector2 lineSize = Vector2.Zero;
                    int idx = 0;
                    while (idx < line.Length)
                    {
                        // Scale block
                        var scaleBlockRegex = new Regex(@"<scale\s*=\s*""([\d\.]+)""\s*>(.*?)</scale>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        var scaleMatch = scaleBlockRegex.Match(line, idx);
                        if (scaleMatch.Success && scaleMatch.Index == idx)
                        {
                            float scale = float.TryParse(scaleMatch.Groups[1].Value, out var s) ? s : 1.0f;
                            string scaleContent = scaleMatch.Groups[2].Value;
                            Vector2 scaledSize = Parse(scaleContent, parentScale * scale);
                            lineSize.X += scaledSize.X;
                            lineSize.Y = Math.Max(lineSize.Y, scaledSize.Y);
                            idx += scaleMatch.Length;
                            continue;
                        }

                        // Image
                        var imgRegex = new Regex(@"<(img|image)>(.*?)</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        var imgMatch = imgRegex.Match(line, idx);
                        if (imgMatch.Success && imgMatch.Index == idx)
                        {
                            string imgUrl = imgMatch.Groups[2].Value.Trim();
                            Vector2 imgSize = new Vector2(100, 100); // Default
                            if (_imageCache.TryGetValue(imgUrl, out var texture) && texture != null)
                                imgSize = new Vector2(texture.Width, texture.Height);
                            imgSize *= parentScale;
                            lineSize.X += imgSize.X;
                            lineSize.Y = Math.Max(lineSize.Y, imgSize.Y);
                            idx += imgMatch.Length;
                            continue;
                        }

                        // Color
                        var colorRegex = new Regex(@"<color\s+hex\s*=\s*([A-Fa-f0-9]{6})\s*>(.*?)</color>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        var colorMatch = colorRegex.Match(line, idx);
                        if (colorMatch.Success && colorMatch.Index == idx)
                        {
                            string colorContent = colorMatch.Groups[2].Value;
                            string wrapped = WrapTextToFit(colorContent, wrapWidth);
                            foreach (var wrappedLine in wrapped.Split('\n'))
                            {
                                Vector2 colorTextSize = ImGui.CalcTextSize(wrappedLine) * parentScale;
                                lineSize.X = Math.Max(lineSize.X, colorTextSize.X);
                                lineSize.Y += colorTextSize.Y;
                            }
                            idx += colorMatch.Length;
                            continue;
                        }

                        // URL
                        var urlRegex = new Regex(@"<url>(.*?)</url>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        var urlMatch = urlRegex.Match(line, idx);
                        if (urlMatch.Success && urlMatch.Index == idx)
                        {
                            string urlContent = urlMatch.Groups[1].Value;
                            string wrapped = WrapTextToFit(urlContent, wrapWidth);
                            foreach (var wrappedLine in wrapped.Split('\n'))
                            {
                                Vector2 urlTextSize = ImGui.CalcTextSize(wrappedLine) * parentScale;
                                lineSize.X = Math.Max(lineSize.X, urlTextSize.X);
                                lineSize.Y += urlTextSize.Y;
                            }
                            idx += urlMatch.Length;
                            continue;
                        }

                        // Plain text
                        int nextTagIdx = line.IndexOf('<', idx);
                        if (nextTagIdx == -1)
                            nextTagIdx = line.Length;
                        string plainText = line.Substring(idx, nextTagIdx - idx);
                        if (!string.IsNullOrWhiteSpace(plainText))
                        {
                            string wrapped = WrapTextToFit(plainText, wrapWidth);
                            foreach (var wrappedLine in wrapped.Split('\n'))
                            {
                                Vector2 textSize = ImGui.CalcTextSize(wrappedLine) * parentScale;
                                lineSize.X = Math.Max(lineSize.X, textSize.X);
                                lineSize.Y += textSize.Y;
                            }
                        }
                        idx = nextTagIdx;
                    }
                    maxWidth = Math.Max(maxWidth, lineSize.X);
                    totalHeight += lineSize.Y;
                }

                return new Vector2(maxWidth, totalHeight);
            }

            // Add some padding
            Vector2 size = Parse(tooltip, 1.0f);
            size.X += 16;
            size.Y += 16;
            return size;
        }
    }
}
