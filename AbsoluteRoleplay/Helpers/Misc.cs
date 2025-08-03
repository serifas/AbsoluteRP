using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.ImGuiFontChooserDialog;
using Dalamud.Interface.Internal;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Storage.Assets;
using FFXIVClientStructs.FFXIV.Common.Lua;
using ImGuiNET;
using Lumina.Excel.Sheets;
using Microsoft.Extensions.Configuration;
using OtterGui;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
namespace AbsoluteRoleplay
{
    public class Misc
    {
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
        private static Vector2 ParseImageSize(string sizeAttr)
        {
            if (string.IsNullOrEmpty(sizeAttr)) return new Vector2(200, 200);
            var parts = sizeAttr.Split(',');
            if (parts.Length == 2 &&
                float.TryParse(parts[0], out float w) &&
                float.TryParse(parts[1], out float h))
                return new Vector2(w, h);
            return new Vector2(200, 200);
        }

        private static float ParseFontSize(string sizeAttr)
        {
            if (string.IsNullOrEmpty(sizeAttr)) return ImGui.GetFontSize();
            if (float.TryParse(sizeAttr, out float sz)) return sz;
            return ImGui.GetFontSize();
        }
        public static void RenderHtmlElements(string text, bool url, bool image, bool color, float? overrideWrapWidth = null)
        {
            float wrapWidth = overrideWrapWidth ?? (ImGui.GetWindowSize().X - 50);

            // Regex to match <sameline><img>...</img></sameline> blocks
            var samelineImgRegex = new Regex(@"<sameline>\s*<(img|image)>(.*?)</\1>\s*</sameline>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            int lastIndex = 0;
            var matches = samelineImgRegex.Matches(text);

            if (matches.Count == 0)
            {
                RenderHtmlElementsNoSameline(text, url, image, color, wrapWidth, true);
                return;
            }

            // Render the text before the first <sameline> block directly, so it respects ImGui.SameLine()
            if (matches.Count > 0 && matches[0].Index > 0)
            {
                string beforeFirst = text.Substring(0, matches[0].Index);
                if (!string.IsNullOrEmpty(beforeFirst))
                    RenderHtmlElementsNoSameline(beforeFirst, url, image, color, wrapWidth, true);
                lastIndex = matches[0].Index;
            }

            bool firstTable = true;
            foreach (Match match in matches)
            {
                int imgStart = match.Index;
                string imgUrl = match.Groups[2].Value.Trim();

                // Render the <sameline> image block in a table
                if (ImGui.BeginTable("TextImageTable" + imgStart, 2, ImGuiTableFlags.None))
                {
                    ImGui.TableNextColumn();
                    // If there is text between lastIndex and imgStart, render it
                    if (imgStart > lastIndex)
                    {
                        string between = text.Substring(lastIndex, imgStart - lastIndex);
                        RenderHtmlElementsNoSameline(between, url, image, color, wrapWidth, firstTable);
                        firstTable = false;
                    }

                    ImGui.TableNextColumn();
                    RenderHtmlElementsNoTable($"<img>{imgUrl}</img>", url, image, color, wrapWidth, false);
                    ImGui.EndTable();
                }

                lastIndex = match.Index + match.Length;
            }

            // Render any text after the last <sameline> block
            if (lastIndex < text.Length)
            {
                string afterImg = text.Substring(lastIndex);
                RenderHtmlElementsNoSameline(afterImg, url, image, color, wrapWidth, false);
            }
        }

        private static void RenderHtmlElementsNoSameline(string text, bool url, bool image, bool color, float wrapWidth, bool isFirstSegment)
        {
            var tableRegex = new Regex(@"<table>(.*?)</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            int lastIndex = 0;
            var tableMatches = tableRegex.Matches(text);

            if (tableMatches.Count == 0)
            {
                RenderHtmlElementsNoTable(text, url, image, color, wrapWidth, isFirstSegment);
                return;
            }

            bool firstTable = isFirstSegment;
            foreach (Match tableMatch in tableMatches)
            {
                int tableStart = tableMatch.Index;
                if (tableStart > lastIndex)
                {
                    string beforeTable = text.Substring(lastIndex, tableStart - lastIndex);
                    RenderHtmlElementsNoTable(beforeTable, url, image, color, wrapWidth, firstTable);
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

                        // Only push down if the first segment is text
                        var scaleBlockRegex = new Regex(@"<scale\s*=\s*""([\d\.]+)""\s*>(.*?)</scale>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        var imgRegex = new Regex(@"<(img|image)>(.*?)</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        var colorRegex = new Regex(@"<color\s+hex\s*=\s*([A-Fa-f0-9]{6})\s*>(.*?)</color>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        var urlRegex = new Regex(@"<url>(.*?)</url>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        var tooltipRegex = new Regex(@"<tooltip>(.*?)</tooltip>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

                        int firstTagIdx = colText.Length;
                        string firstType = "text"; // Default to text if no tag found

                        var scaleMatch = scaleBlockRegex.Match(colText, 0);
                        var imgMatch = imgRegex.Match(colText, 0);
                        var colorMatch = colorRegex.Match(colText, 0);
                        var urlMatch = urlRegex.Match(colText, 0);
                        var tooltipMatch = tooltipRegex.Match(colText, 0);

                        if (scaleMatch.Success && scaleMatch.Index < firstTagIdx) { firstTagIdx = scaleMatch.Index; firstType = "scale"; }
                        if (imgMatch.Success && imgMatch.Index < firstTagIdx) { firstTagIdx = imgMatch.Index; firstType = "img"; }
                        if (colorMatch.Success && colorMatch.Index < firstTagIdx) { firstTagIdx = colorMatch.Index; firstType = "color"; }
                        if (urlMatch.Success && urlMatch.Index < firstTagIdx) { firstTagIdx = urlMatch.Index; firstType = "url"; }
                        if (tooltipMatch.Success && tooltipMatch.Index < firstTagIdx) { firstTagIdx = tooltipMatch.Index; firstType = "tooltip"; }


                        ImGui.BeginGroup();
                        RenderHtmlElementsNoTable(colText, url, image, color, wrapWidth / columnCount, true);
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
                RenderHtmlElementsNoTable(afterTable, url, image, color, wrapWidth, false);
            }
        }

        private static void RenderHtmlElementsNoTable(string text, bool url, bool image, bool color, float wrapWidth, bool isFirstSegment)
        {
            var scaleBlockRegex = new Regex(@"<scale\s*=\s*""([\d\.]+)""\s*>(.*?)</scale>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var imgRegex = new Regex(@"<(img|image)>(.*?)</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var colorRegex = new Regex(@"<color\s+hex\s*=\s*([A-Fa-f0-9]{6})\s*>(.*?)</color>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var urlRegex = new Regex(@"<url>(.*?)</url>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var tooltipRegex = new Regex(@"<tooltip>(.*?)</tooltip>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            var segments = new List<(string type, string content, float scale, string colorHex, string url)>();
            int idx = 0;
            while (idx < text.Length)
            {
                var scaleMatch = scaleBlockRegex.Match(text, idx);
                var imgMatch = imgRegex.Match(text, idx);
                var colorMatch = colorRegex.Match(text, idx);
                var urlMatch = urlRegex.Match(text, idx);
                var tooltipMatch = tooltipRegex.Match(text, idx);

                int nextTagIdx = text.Length;
                string nextType = null;
                Match nextMatch = null;
                if (scaleMatch.Success && scaleMatch.Index < nextTagIdx) { nextTagIdx = scaleMatch.Index; nextType = "scale"; nextMatch = scaleMatch; }
                if (imgMatch.Success && imgMatch.Index < nextTagIdx) { nextTagIdx = imgMatch.Index; nextType = "img"; nextMatch = imgMatch; }
                if (colorMatch.Success && colorMatch.Index < nextTagIdx) { nextTagIdx = colorMatch.Index; nextType = "color"; nextMatch = colorMatch; }
                if (urlMatch.Success && urlMatch.Index < nextTagIdx) { nextTagIdx = urlMatch.Index; nextType = "url"; nextMatch = urlMatch; }
                if (tooltipMatch.Success && tooltipMatch.Index < nextTagIdx) { nextTagIdx = tooltipMatch.Index; nextType = "tooltip"; nextMatch = tooltipMatch; }

                if (nextTagIdx > idx)
                {
                    string plainText = text.Substring(idx, nextTagIdx - idx);
                    if (!string.IsNullOrEmpty(plainText))
                        segments.Add(("text", plainText, 1.0f, null, null));
                }

                if (nextMatch == null)
                    break;

                if (nextType == "scale")
                {
                    float scale = 1.0f;
                    float.TryParse(nextMatch.Groups[1].Value, out scale);
                    string scaleContent = nextMatch.Groups[2].Value;
                    int scaleIdx = 0;
                    while (scaleIdx < scaleContent.Length)
                    {
                        var imgMatch2 = imgRegex.Match(scaleContent, scaleIdx);
                        var colorMatch2 = colorRegex.Match(scaleContent, scaleIdx);
                        var urlMatch2 = urlRegex.Match(scaleContent, scaleIdx);
                        var tooltipMatch2 = tooltipRegex.Match(scaleContent, scaleIdx);

                        int nextTagIdx2 = scaleContent.Length;
                        string nextType2 = null;
                        Match nextMatch2 = null;
                        if (imgMatch2.Success && imgMatch2.Index < nextTagIdx2) { nextTagIdx2 = imgMatch2.Index; nextType2 = "img"; nextMatch2 = imgMatch2; }
                        if (colorMatch2.Success && colorMatch2.Index < nextTagIdx2) { nextTagIdx2 = colorMatch2.Index; nextType2 = "color"; nextMatch2 = colorMatch2; }
                        if (urlMatch2.Success && urlMatch2.Index < nextTagIdx2) { nextTagIdx2 = urlMatch2.Index; nextType2 = "url"; nextMatch2 = urlMatch2; }
                        if (tooltipMatch2.Success && tooltipMatch2.Index < nextTagIdx2) { nextTagIdx2 = tooltipMatch2.Index; nextType2 = "tooltip"; nextMatch2 = tooltipMatch2; }

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
                            segments.Add(("img", imgUrl, scale, null, null));
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
            }

            string pendingTooltip = null;
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                bool itemRendered = false;

                if (seg.type == "img" && image)
                {
                    string imgUrl = seg.content;
                    float scale = seg.scale;

                    if (_imageCache.TryGetValue(imgUrl, out var texture) && texture != null && texture.ImGuiHandle != IntPtr.Zero)
                    {
                        Vector2 imgSize = new Vector2(texture.Width, texture.Height) * scale;

                        // Limit image size to 40% of window width/height
                        Vector2 windowSize = ImGui.GetWindowSize();
                        float maxWidth = windowSize.X * 0.4f;
                        float maxHeight = windowSize.Y * 0.4f;

                        float widthScale = imgSize.X > maxWidth ? maxWidth / imgSize.X : 1f;
                        float heightScale = imgSize.Y > maxHeight ? maxHeight / imgSize.Y : 1f;
                        float finalScale = Math.Min(widthScale, heightScale);

                        imgSize *= finalScale;

                        ImGui.Image(texture.ImGuiHandle, imgSize);
                    }
                    else
                    {
                        if (!_imagesLoading.Contains(imgUrl))
                        {
                            _imagesLoading.Add(imgUrl);
                            System.Threading.Tasks.Task.Run(() =>
                            {
                                try
                                {
                                    using (var webClient = new System.Net.WebClient())
                                    {
                                        var imageBytes = webClient.DownloadData(imgUrl);
                                        var tex = Plugin.TextureProvider.CreateFromImageAsync(imageBytes).Result;
                                        if (tex != null && tex.ImGuiHandle != IntPtr.Zero)
                                        {
                                            _imageCache[imgUrl] = tex;
                                        }
                                        else
                                        {
                                            _imageCache[imgUrl] = null;
                                        }
                                    }
                                }
                                catch
                                {
                                    _imageCache[imgUrl] = null;
                                }
                                finally
                                {
                                    _imagesLoading.Remove(imgUrl);
                                }
                            });
                        }
                        Vector2 windowSize = ImGui.GetWindowSize();
                        Vector2 placeholderSize = new Vector2(
                            Math.Min(200 * scale, windowSize.X * 0.4f),
                            Math.Min(200 * scale, windowSize.Y * 0.4f)
                        );
                        ImGui.TextColored(new Vector4(1, 1, 0, 1), "[Loading image...]");
                    }
                    itemRendered = true;
                }
                else if (seg.type == "color" && color && seg.colorHex != null)
                {
                    if (TryParseHexColor(seg.colorHex, out Vector4 colorVal))
                    {
                        // Wrap colored text
                        string wrapped = WrapTextToFit(seg.content, wrapWidth);
                        foreach (var line in wrapped.Split('\n'))
                        {
                            ImGui.TextColored(colorVal, line);
                        }
                    }
                    else
                    {
                        string wrapped = WrapTextToFit(seg.content, wrapWidth);
                        foreach (var line in wrapped.Split('\n'))
                        {
                            ImGui.TextUnformatted(line);
                        }
                    }
                    itemRendered = true;
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
                            try
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = seg.url,
                                    UseShellExecute = true
                                });
                            }
                            catch { }
                        }
                    }
                    itemRendered = true;
                }
                else if (seg.type == "text")
                {
                    string wrapped = WrapTextToFit(seg.content, wrapWidth);
                    foreach (var line in wrapped.Split('\n'))
                    {
                        ImGui.TextUnformatted(line);
                    }
                    itemRendered = true;
                }
                else if (seg.type == "tooltip")
                {
                    pendingTooltip = seg.content;
                    continue; // Don't render anything for tooltip segment
                }

                // Tooltip logic unchanged
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
        public static void DrawCenteredInput(float center, Vector2 size, string label, string hint, ref string input, uint length, ImGuiInputTextFlags flags)
        {
            var currentCursorY = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(new Vector2(center, currentCursorY));
            ImGui.PushItemWidth(size.X);
            ImGui.InputTextWithHint(label, hint, ref input, length, flags);
            ImGui.PopItemWidth();
        }
        public static bool DrawXCenteredInput(string label, string id, ref string input, uint length)
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
            var centeredInput = ImGui.InputText("##ID" + id, ref input, length);
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
            if(center == true)
            {
                var size = ImGui.CalcTextSize(title);

                var windowSize = ImGui.GetWindowSize();

                // Set the cursor position to center the button horizontally
                float xPos = (windowSize.X - size.X -15) / 2; // Center horizontally
                ImGui.SetCursorPosX(xPos);
            }
            ImGuiUtil.DrawTextButton(title, Vector2.Zero, 0);

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

                // ... rest of your code ...
            }
            catch (Exception ex)
            {
                Plugin.plugin.logger.Error("TargetProfileWindow ResetAllData Error: " + ex.Message);
            }
        }
        private static readonly Dictionary<string, LoaderTweenState> loaderTweens = new();
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

    }
}
