using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

namespace AbsoluteRP.Windows.Inventory
{
    // Visual helpers shared by the Inventory / Equipment / Trade screens so they read like the rest of the RsUI pages: themed tiles, quality colours, muted labels, themed tooltips and popups, plus the small "new item" pop-in animation. Everything here is purely visual - no inventory state lives in this class.
    internal static class InvUI
    {
        public static float S(float v) => RsTheme.S(v);
        public static Vector2 S(float x, float y) => RsTheme.S(x, y);
        public static uint U(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);
        public static Vector4 Fade(Vector4 c, float a) => new(c.X, c.Y, c.Z, a);
        public static Vector4 Mix(Vector4 a, Vector4 b, float t) => new(
            a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, a.W + (b.W - a.W) * t);
        public static Vector4 Brighten(Vector4 c, float amt) =>
            new(Math.Min(1f, c.X + amt), Math.Min(1f, c.Y + amt), Math.Min(1f, c.Z + amt), c.W);

        private static bool AnimationsEnabled
        {
            get
            {
                try { return Plugin.plugin?.Configuration?.AnimationsEnabled ?? true; }
                catch { return true; }
            }
        }

        // Quality / type

        // Quality tier -> colour. Common uses the plain text colour; the themed tiers map onto the accent palette, the remaining two keep their traditional hues so they still read as "aetherial"/"relic".
        public static Vector4 QualityColor(int quality) => quality switch
        {
            0 => RsTheme.TextPrimary,
            2 => RsTheme.AccentSuccess,
            3 => RsTheme.AccentPrimary,
            _ => Items.ItemQualityColors(quality),
        };

        public static string QualityName(int quality)
            => quality >= 0 && quality < Items.ItemQualityTypes.Length ? Items.ItemQualityTypes[quality] : "Common";

        public static string TypeName(int type)
            => type >= 0 && type < Items.InventoryTypes.Length ? Items.InventoryTypes[type].Item1 : string.Empty;

        public static string SubtypeName(int type, int subtype)
        {
            if (type < 0 || type >= Items.InventoryTypes.Length) return string.Empty;
            var subs = Items.InventoryTypes[type].Item3;
            if (subs == null || subtype < 0 || subtype >= subs.Length) return string.Empty;
            return subs[subtype].Trim();
        }

        // Text

        public static void Text(string text, Vector4 color)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            try { ImGui.TextUnformatted(text ?? string.Empty); }
            finally { ImGui.PopStyleColor(); }
        }

        public static void Primary(string text) => Text(text, RsTheme.TextPrimary);
        public static void Secondary(string text) => Text(text, RsTheme.TextSecondary);
        public static void Muted(string text) => Text(text, RsTheme.TextMuted);

        public static void Wrapped(string text, Vector4 color)
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + Math.Max(S(40f), RsElements.AvailContentWidth()));
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            try { ImGui.TextUnformatted(text ?? string.Empty); }
            finally
            {
                ImGui.PopStyleColor();
                ImGui.PopTextWrapPos();
            }
        }

        // Upper-case muted label introducing a group of controls.
        public static void SectionLabel(string text)
        {
            ImGui.Dummy(new Vector2(0f, S(2f)));
            Text((text ?? string.Empty).ToUpperInvariant(), RsTheme.TextMuted);
            ImGui.Dummy(new Vector2(0f, S(2f)));
        }

        public static void Gap(float px = 8f) => ImGui.Dummy(new Vector2(0f, S(px)));

        public static void Divider()
        {
            Gap(4f);
            var start = ImGui.GetCursorScreenPos();
            var w = Math.Max(S(20f), RsElements.AvailContentWidth());
            ImGui.GetWindowDrawList().AddLine(start, start + new Vector2(w, 0f), RsTheme.U.Border, RsTheme.BorderThickness);
            ImGui.Dummy(new Vector2(w, S(5f)));
        }

        // Tinted pill with coloured text (quality chip, status chip).
        public static void Badge(string text, Vector4 color)
        {
            var pad = S(8f, 2f);
            var ts = ImGui.CalcTextSize(text);
            var size = ts + pad * 2f;
            var p = ImGui.GetCursorScreenPos();
            var draw = ImGui.GetWindowDrawList();
            draw.AddRectFilled(p, p + size, U(Fade(color, 0.16f)), size.Y * 0.5f);
            draw.AddRect(p, p + size, U(Fade(color, 0.45f)), size.Y * 0.5f, ImDrawFlags.None, RsTheme.BorderThickness);
            draw.AddText(p + pad, U(color), text);
            ImGui.Dummy(size);
        }

        // Muted inline notice with a coloured leading bar (empty states, warnings).
        public static void Notice(string text, Vector4 color)
        {
            var start = ImGui.GetCursorScreenPos();
            var draw = ImGui.GetWindowDrawList();
            var bar = S(3f);
            var pad = S(10f);
            ImGui.Indent(bar + pad);
            try { Wrapped(text, RsTheme.TextSecondary); }
            finally { ImGui.Unindent(bar + pad); }
            var end = ImGui.GetItemRectMax();
            draw.AddRectFilled(start, new Vector2(start.X + bar, end.Y), U(color), bar * 0.5f);
        }

        // Frame / popup styling

        private const int FrameColorCount = 13;
        private const int FrameVarCount = 3;

        // Themes stock ImGui widgets (tab bars, inputs, image buttons) that we can't replace - used around the shared icon browser.
        public static void PushFrameStyle()
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, RsTheme.BgTertiary);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Brighten(RsTheme.BgTertiary, 0.04f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Brighten(RsTheme.BgTertiary, 0.07f));
            ImGui.PushStyleColor(ImGuiCol.Button, RsTheme.BgSecondary);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Brighten(RsTheme.BgSecondary, 0.06f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, RsTheme.AccentPrimary);
            ImGui.PushStyleColor(ImGuiCol.Border, RsTheme.Border);
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
            ImGui.PushStyleColor(ImGuiCol.TextSelectedBg, Fade(RsTheme.AccentPrimary, 0.45f));
            ImGui.PushStyleColor(ImGuiCol.Tab, RsTheme.BgTertiary);
            ImGui.PushStyleColor(ImGuiCol.TabHovered, Fade(RsTheme.AccentPrimary, 0.35f));
            ImGui.PushStyleColor(ImGuiCol.TabActive, Fade(RsTheme.AccentPrimary, 0.55f));
            ImGui.PushStyleColor(ImGuiCol.Separator, RsTheme.Border);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, S(6f));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, RsTheme.BorderThickness);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, S(8f, 6f));
        }

        public static void PopFrameStyle()
        {
            ImGui.PopStyleVar(FrameVarCount);
            ImGui.PopStyleColor(FrameColorCount);
        }

        private const int PopupColorCount = 6;
        private const int PopupVarCount = 7;

        private static void PushPopupStyle()
        {
            ImGui.PushStyleColor(ImGuiCol.PopupBg, RsTheme.BgSecondary);
            ImGui.PushStyleColor(ImGuiCol.WindowBg, RsTheme.BgSecondary);
            ImGui.PushStyleColor(ImGuiCol.Border, RsTheme.Border);
            ImGui.PushStyleColor(ImGuiCol.TitleBg, RsTheme.BgSecondary);
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, Brighten(RsTheme.BgSecondary, 0.04f));
            ImGui.PushStyleColor(ImGuiCol.ModalWindowDimBg, new Vector4(0f, 0f, 0f, 0.55f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, S(18f, 16f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, S(8f));
            ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, S(8f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, RsTheme.BorderThickness);
            ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, RsTheme.BorderThickness);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, S(8f, 8f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowTitleAlign, new Vector2(0f, 0.5f));
        }

        private static void PopPopupStyle()
        {
            ImGui.PopStyleVar(PopupVarCount);
            ImGui.PopStyleColor(PopupColorCount);
        }

        // Themed BeginPopupModal. Only call EndModal when this returned true.
        public static bool BeginModal(string name, ref bool open)
        {
            PushPopupStyle();
            bool visible;
            try { visible = ImGui.BeginPopupModal(name, ref open, ImGuiWindowFlags.AlwaysAutoResize); }
            catch { PopPopupStyle(); throw; }
            if (!visible) PopPopupStyle();
            return visible;
        }

        public static void EndModal()
        {
            ImGui.EndPopup();
            PopPopupStyle();
        }

        // Themed context popup. Only call EndPopup when this returned true.
        public static bool BeginPopup(string name)
        {
            PushPopupStyle();
            bool visible;
            try { visible = ImGui.BeginPopup(name); }
            catch { PopPopupStyle(); throw; }
            if (!visible) PopPopupStyle();
            return visible;
        }

        public static void EndPopup()
        {
            ImGui.EndPopup();
            PopPopupStyle();
        }

        // Context-menu row rendered as a hoverable pill instead of the stock MenuItem.
        public static bool MenuItem(string label, Vector4? color = null, bool enabled = true)
        {
            var display = label;
            var hash = display.IndexOf("##", StringComparison.Ordinal);
            if (hash >= 0) display = display.Substring(0, hash);

            var pad = S(10f, 6f);
            var w = Math.Max(S(140f), ImGui.GetContentRegionAvail().X);
            var h = ImGui.GetTextLineHeight() + pad.Y * 2f;
            var start = ImGui.GetCursorScreenPos();
            var draw = ImGui.GetWindowDrawList();

            bool clicked = false;
            bool hovered = false;
            if (enabled)
            {
                clicked = ImGui.InvisibleButton("##inv_mi_" + label, new Vector2(w, h));
                hovered = ImGui.IsItemHovered();
            }
            else
            {
                ImGui.Dummy(new Vector2(w, h));
            }

            if (hovered)
                draw.AddRectFilled(start, start + new Vector2(w, h), U(Fade(RsTheme.AccentPrimary, 0.22f)), S(6f));
            var col = !enabled ? RsTheme.TextMuted : (color ?? RsTheme.TextPrimary);
            draw.AddText(start + pad, U(col), display);
            return clicked;
        }

        // Tooltip with themed background / padding. Always pair with EndTooltip.
        public static void BeginTooltip()
        {
            ImGui.PushStyleColor(ImGuiCol.PopupBg, RsTheme.BgSecondary);
            ImGui.PushStyleColor(ImGuiCol.Border, RsTheme.Border);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, S(12f, 10f));
            ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, S(6f));
            ImGui.BeginTooltip();
        }

        public static void EndTooltip()
        {
            ImGui.EndTooltip();
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(2);
        }

        // Full item tooltip: icon, name (HTML colour aware), type line, quality chip and the description body.
        public static void ItemTooltip(ItemDefinition item, IDalamudTextureWrap? texture, float iconSize = 48f)
        {
            if (item == null) return;
            BeginTooltip();
            try
            {
                var sz = S(iconSize);
                var start = ImGui.GetCursorScreenPos();
                var draw = ImGui.GetWindowDrawList();
                DrawTile(draw, start, start + new Vector2(sz, sz), texture, item.quality, false, false, 1f);
                ImGui.Dummy(new Vector2(sz, sz));
                ImGui.SameLine();
                ImGui.BeginGroup();
                try
                {
                    Misc.RenderHtmlColoredTextInline(string.IsNullOrEmpty(item.name) ? "Unknown" : item.name, 500);
                    var typeName = TypeName(item.type);
                    var subName = SubtypeName(item.type, item.subtype);
                    var line = string.IsNullOrEmpty(subName) ? typeName : $"{typeName} · {subName}";
                    if (!string.IsNullOrEmpty(line)) Muted(line);
                    Badge(QualityName(item.quality), QualityColor(item.quality));
                    if (item.locked)
                    {
                        ImGui.SameLine();
                        Badge("Locked", RsTheme.TextMuted);
                    }
                }
                finally { ImGui.EndGroup(); }

                if (!string.IsNullOrEmpty(item.description))
                {
                    Divider();
                    Misc.RenderHtmlElements(item.description, false, true, true, true, null, true);
                }
            }
            finally { EndTooltip(); }
        }

        // Tiles

        // Rounded item tile: surface, optional icon, quality strip, hover / selection ring and the pop-in animation (spawn < 1). spawn 0..1 linear progress of the "new item" animation, 1 = idle.
        public static void DrawTile(ImDrawListPtr draw, Vector2 min, Vector2 max, IDalamudTextureWrap? texture,
                                    int quality, bool hovered, bool selected, float spawn,
                                    Vector4? surface = null, Vector4? ring = null)
        {
            var r = S(6f);
            var size = max - min;
            var bg = surface ?? RsTheme.BgTertiary;
            if (hovered) bg = Brighten(bg, 0.05f);
            draw.AddRectFilled(min, max, U(bg), r);

            bool hasIcon = texture != null && texture.Handle != IntPtr.Zero;
            bool animating = spawn < 1f;
            float scale = 1f;
            float glow = 0f;
            if (animating)
            {
                scale = Anim.Lerp(0.5f, 1f, Anim.Apply(spawn, Anim.Ease.OutBack));
                glow = 1f - Anim.Apply(spawn, Anim.Ease.OutCubic);
                // Soft accent wash on the tile while the item lands.
                draw.AddRectFilled(min, max, U(Fade(RsTheme.AccentPrimary, 0.28f * glow)), r);
            }

            if (hasIcon)
            {
                var inset = S(3f);
                var iconMin = min + new Vector2(inset, inset);
                var iconMax = max - new Vector2(inset, inset);
                if (scale != 1f)
                {
                    var c = (iconMin + iconMax) * 0.5f;
                    var half = (iconMax - iconMin) * 0.5f * scale;
                    iconMin = c - half;
                    iconMax = c + half;
                }
                var tint = U(new Vector4(1f, 1f, 1f, animating ? Math.Clamp(spawn * 2f, 0.15f, 1f) : 1f));
                draw.AddImageRounded(texture!.Handle, iconMin, iconMax, Vector2.Zero, Vector2.One, tint, Math.Max(1f, r - inset));
            }

            // Quality strip along the bottom edge for anything above Common.
            if (hasIcon && quality > 0)
            {
                var strip = Math.Max(2f, S(3f));
                var qc = QualityColor(quality);
                draw.AddRectFilled(new Vector2(min.X + r * 0.5f, max.Y - strip - S(2f)),
                                   new Vector2(max.X - r * 0.5f, max.Y - S(2f)),
                                   U(Fade(qc, 0.95f)), strip * 0.5f);
            }

            // Border / rings.
            var borderCol = ring ?? (selected ? RsTheme.AccentPrimary : hovered ? RsTheme.BorderStrong : RsTheme.Border);
            var thick = selected ? RsTheme.BorderThickness * 2f : RsTheme.BorderThickness;
            draw.AddRect(min, max, U(borderCol), r, ImDrawFlags.None, thick);
            if (hovered && !selected)
                draw.AddRect(min - new Vector2(1f, 1f), max + new Vector2(1f, 1f), U(Fade(RsTheme.AccentPrimary, 0.35f)), r + 1f, ImDrawFlags.None, RsTheme.BorderThickness);

            if (animating && glow > 0.01f)
            {
                // Expanding glow ring that fades out as the pop settles.
                var grow = S(2f + 8f * (1f - glow));
                draw.AddRect(min - new Vector2(grow, grow), max + new Vector2(grow, grow),
                             U(Fade(RsTheme.AccentPrimary, 0.9f * glow)), r + grow, ImDrawFlags.None, Math.Max(1f, S(2f)));
                draw.AddRect(min - new Vector2(grow * 0.5f, grow * 0.5f), max + new Vector2(grow * 0.5f, grow * 0.5f),
                             U(Fade(RsTheme.TextPrimary, 0.5f * glow)), r + grow * 0.5f, ImDrawFlags.None, 1f);
            }
        }

        // Small caption centred under a tile.
        public static void TileCaption(ImDrawListPtr draw, Vector2 tileMin, float tileW, string text, Vector4 color, float maxWidth)
        {
            if (string.IsNullOrEmpty(text)) return;
            var ts = ImGui.CalcTextSize(text);
            if (ts.X > maxWidth) return;
            var x = tileMin.X + (tileW - ts.X) * 0.5f;
            draw.AddText(new Vector2(x, tileMin.Y), U(color), text);
        }

        // "New item" spawn tracking Each item list (an inventory tab, a trade row, the equipment set) registers its occupied slots once per frame through Track(). Any slot whose item signature was not present in the previous frame - and whose signature count went up, so a plain move between slots doesn't count - starts a short pop-in animation keyed by (list, slot). Progress() reads that animation back while drawing.

        private const float SpawnDuration = 0.55f;
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static double Now => Clock.Elapsed.TotalSeconds;

        private sealed class ListSnap
        {
            public Dictionary<int, int> SlotSig = new();
            public Dictionary<int, int> Counts = new();
            public Dictionary<int, int> NextSlotSig = new();
            public Dictionary<int, int> NextCounts = new();
            public int LastFrame;
            public bool Primed;
        }

        private static readonly Dictionary<string, ListSnap> Snaps = new();
        private static readonly Dictionary<(string, int), double> Spawns = new();
        private static readonly List<(string, int)> SpawnScratch = new();
        private static int lastPruneFrame = -1;

        private static int Signature(ItemDefinition item)
            => HashCode.Combine(item.name, item.iconID, item.quality, item.type, item.subtype);

        public static bool IsOccupied(ItemDefinition? item) => item != null && !string.IsNullOrEmpty(item.name);

        // Register the current contents of a list. Call once per frame per list, before drawing its tiles. Cheap: two small dictionaries per list are rebuilt in place, no per-frame allocations beyond growth.
        public static void Track(string listKey, Dictionary<int, ItemDefinition>? items)
        {
            if (items == null) return;
            int frame = ImGui.GetFrameCount();
            if (!Snaps.TryGetValue(listKey, out var snap))
                Snaps[listKey] = snap = new ListSnap();
            if (snap.LastFrame == frame) return; // already tracked this frame
            snap.LastFrame = frame;

            var cur = snap.NextSlotSig;
            var curCounts = snap.NextCounts;
            cur.Clear();
            curCounts.Clear();
            foreach (var kv in items)
            {
                if (!IsOccupied(kv.Value)) continue;
                var sig = Signature(kv.Value);
                cur[kv.Key] = sig;
                curCounts[sig] = curCounts.TryGetValue(sig, out var n) ? n + 1 : 1;
            }

            if (snap.Primed && AnimationsEnabled)
            {
                foreach (var kv in cur)
                {
                    if (snap.SlotSig.TryGetValue(kv.Key, out var prevSig) && prevSig == kv.Value) continue;
                    snap.Counts.TryGetValue(kv.Value, out var prevCount);
                    if (curCounts[kv.Value] > prevCount)
                        Spawns[(listKey, kv.Key)] = Now;
                }
            }

            // Swap buffers.
            snap.NextSlotSig = snap.SlotSig;
            snap.NextCounts = snap.Counts;
            snap.SlotSig = cur;
            snap.Counts = curCounts;
            snap.Primed = true;

            // Occasionally drop finished animations so the table stays tiny.
            if (Spawns.Count > 0 && frame != lastPruneFrame && (frame & 31) == 0)
            {
                lastPruneFrame = frame;
                SpawnScratch.Clear();
                foreach (var kv in Spawns)
                    if (Now - kv.Value > SpawnDuration) SpawnScratch.Add(kv.Key);
                foreach (var k in SpawnScratch) Spawns.Remove(k);
            }
        }

        // Forget a list (e.g. when the trade window closes) so its next appearance is treated as a baseline rather than a burst of new items.
        public static void Forget(string listKey)
        {
            Snaps.Remove(listKey);
        }

        // Linear 0..1 progress of the pop-in for a slot; 1 when idle.
        public static float Progress(string listKey, int slot)
        {
            if (Spawns.Count == 0) return 1f;
            if (!Spawns.TryGetValue((listKey, slot), out var start)) return 1f;
            var t = (float)((Now - start) / SpawnDuration);
            if (t >= 1f)
            {
                Spawns.Remove((listKey, slot));
                return 1f;
            }
            return t < 0f ? 0f : t;
        }

        // Manually flag a slot as freshly arrived (used when a list is replaced wholesale but we still know which slot is the new one).
        public static void Flag(string listKey, int slot)
        {
            if (!AnimationsEnabled) return;
            Spawns[(listKey, slot)] = Now;
        }
    }
}
