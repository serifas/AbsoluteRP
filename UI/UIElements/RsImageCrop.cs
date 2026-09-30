using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace AbsoluteRP.RsUI;

// Optional crop step shown after an image is picked in RsFileDialog. The user drags out the area they want (free-form, any size or shape of rectangle) or keeps the whole image. A crop is written to a temp PNG and that path is handed to whoever asked for the image, so every image upload in the plugin gets this without knowing about it.
public static class RsImageCrop
{
    private static bool _open;
    private static string _path = string.Empty;
    private static Action<bool, string>? _callback;
    private static IDalamudTextureWrap? _tex;
    private static bool _loading;
    private static int _srcW, _srcH;
    private static string _error = string.Empty;

    // Selection in source-image pixels, kept normalised (_selA = min, _selB = max).
    private static Vector2 _selA, _selB;
    private static bool _hasSel, _dragging;

    // Interaction state.
    private enum DragMode { None, Create, Move, Resize }
    private const int HL = 1, HR = 2, HT = 4, HB = 8;   // handle edge bits
    private const float MinSel = 8f;
    private static DragMode _mode;
    private static int _handle;
    private static Vector2 _dragStart, _startA, _startB;
    private static float _aspect;        // width / height; 0 = free
    private static float _openAspect;    // aspect requested by the caller

    public static bool IsOpen => _open;

    // Animated or unreadable files skip the crop step.
    public static bool CanCrop(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".bmp";   // gif stays animated, webp is not readable here
    }

    // aspect: optional locked width/height ratio (e.g. 1 for a square avatar); 0 = free-form.
    public static void Open(string path, Action<bool, string>? callback, float aspect = 0f)
    {
        DisposeTexture();
        _path = path; _callback = callback; _open = true;
        _hasSel = false; _dragging = false; _mode = DragMode.None; _error = string.Empty;
        _openAspect = aspect > 0f ? aspect : 0f; _aspect = _openAspect;
        _srcW = _srcH = 0;
        _loading = true;
        _ = LoadAsync(path);
    }

    private static async Task LoadAsync(string path)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(path);
            try
            {
                using var ms = new MemoryStream(bytes);
                using var img = System.Drawing.Image.FromStream(ms);
                _srcW = img.Width; _srcH = img.Height;
            }
            catch { _srcW = _srcH = 0; }
            byte[] preview;
            try { preview = AbsoluteRP.Helpers.Imaging.ScaleImageBytes(bytes, 1400, 1400); } catch { preview = bytes; }
            var tex = await Plugin.TextureProvider.CreateFromImageAsync(preview);
            if (path != _path || !_open) { try { tex?.Dispose(); } catch { } return; }
            _tex = tex;
            if (_srcW <= 0 || _srcH <= 0) { _srcW = tex.Width; _srcH = tex.Height; }
        }
        catch (Exception ex)
        {
            _error = "Could not open that image: " + ex.Message;
        }
        finally { _loading = false; }
    }

    private static void DisposeTexture()
    {
        AbsoluteRP.Helpers.TextureGraveyard.Enqueue(_tex);   // it was drawn this frame
        _tex = null;
    }

    private static void Finish(bool ok, string path)
    {
        _open = false;
        var cb = _callback; _callback = null;
        DisposeTexture();
        try { cb?.Invoke(ok, path); } catch (Exception ex) { Plugin.PluginLog.Debug("RsImageCrop callback: " + ex.Message); }
    }

    public static void Draw()
    {
        if (!_open) return;
        var openState = true;
        ImGui.OpenPopup("##rs_image_crop");
        var view = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(view.WorkPos + view.WorkSize * 0.5f, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(RsTheme.S(760f), RsTheme.S(600f)), ImGuiCond.Appearing);
        if (!ImGui.BeginPopupModal("##rs_image_crop", ref openState, ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollWithMouse))
        {
            if (!openState) Finish(false, string.Empty);
            return;
        }
        try
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
            ImGui.TextUnformatted("Crop image");
            ImGui.PopStyleColor();
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextWrapped("Drag on the picture to mark the area you want to keep. Drag inside it to move it, drag its edges or corners to resize, scroll to scale it, or keep the whole image.");
            ImGui.PopStyleColor();
            ImGui.Separator();

            var footerH = RsTheme.S(54f);
            var area = ImGui.GetContentRegionAvail() - new Vector2(0f, footerH);
            area.Y = MathF.Max(RsTheme.S(120f), area.Y);
            var origin = ImGui.GetCursorScreenPos();
            var dl = ImGui.GetWindowDrawList();
            dl.AddRectFilled(origin, origin + area, ImGui.GetColorU32(RsTheme.BgTertiary), RsTheme.S(6f));

            if (!string.IsNullOrEmpty(_error))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentDanger);
                ImGui.TextWrapped(_error);
                ImGui.PopStyleColor();
                ImGui.Dummy(area - new Vector2(0f, ImGui.GetTextLineHeight() * 2f));
            }
            else if (_tex == null || _tex.Handle == IntPtr.Zero || _srcW <= 0)
            {
                ImGui.Dummy(area);
                var msg = _loading ? "Loading…" : "No preview";
                var ms = ImGui.CalcTextSize(msg);
                dl.AddText(origin + (area - ms) * 0.5f, ImGui.GetColorU32(RsTheme.TextMuted), msg);
            }
            else
            {
                // Fit the image inside the area.
                var scale = MathF.Min(area.X / _srcW, area.Y / _srcH);
                var shown = new Vector2(_srcW, _srcH) * scale;
                var imgMin = origin + (area - shown) * 0.5f;
                var imgMax = imgMin + shown;
                dl.AddImage(_tex.Handle, imgMin, imgMax);

                ImGui.SetCursorScreenPos(imgMin);
                ImGui.InvisibleButton("##rs_crop_canvas", shown);
                HandleInput(imgMin, scale);

                if (_dragging || _hasSel)
                {
                    var a = _selA; var b = _selB;
                    var sMin = imgMin + a * scale; var sMax = imgMin + b * scale;
                    // Dim everything outside the selection.
                    var dim = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.55f));
                    dl.AddRectFilled(imgMin, new Vector2(imgMax.X, sMin.Y), dim);
                    dl.AddRectFilled(new Vector2(imgMin.X, sMax.Y), imgMax, dim);
                    dl.AddRectFilled(new Vector2(imgMin.X, sMin.Y), new Vector2(sMin.X, sMax.Y), dim);
                    dl.AddRectFilled(new Vector2(sMax.X, sMin.Y), new Vector2(imgMax.X, sMax.Y), dim);
                    dl.AddRect(sMin, sMax, ImGui.GetColorU32(RsTheme.AccentPrimary), 0f, ImDrawFlags.None, 2f);
                    // Rule-of-thirds guides.
                    var g = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.25f));
                    for (int i = 1; i <= 2; i++)
                    {
                        var x = sMin.X + (sMax.X - sMin.X) * i / 3f; var y = sMin.Y + (sMax.Y - sMin.Y) * i / 3f;
                        dl.AddLine(new Vector2(x, sMin.Y), new Vector2(x, sMax.Y), g, 1f);
                        dl.AddLine(new Vector2(sMin.X, y), new Vector2(sMax.X, y), g, 1f);
                    }
                    // Resize handles once a selection exists.
                    if (_hasSel && _mode != DragMode.Create)
                    {
                        var hs = new Vector2(RsTheme.S(4f));
                        var hc = ImGui.GetColorU32(RsTheme.AccentPrimary);
                        var mid = (sMin + sMax) * 0.5f;
                        foreach (var hp in new[] { sMin, new Vector2(mid.X, sMin.Y), new Vector2(sMax.X, sMin.Y), new Vector2(sMax.X, mid.Y),
                                                   sMax, new Vector2(mid.X, sMax.Y), new Vector2(sMin.X, sMax.Y), new Vector2(sMin.X, mid.Y) })
                        {
                            dl.AddRectFilled(hp - hs, hp + hs, hc);
                            dl.AddRect(hp - hs, hp + hs, 0xFFFFFFFF, 0f, ImDrawFlags.None, 1f);
                        }
                    }
                    var dims = $"{(int)(b.X - a.X)} × {(int)(b.Y - a.Y)}";
                    dl.AddText(sMin + new Vector2(6f, 4f), 0xFFFFFFFF, dims);
                }
                ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + area.Y));
            }

            ImGui.Spacing();
            var btnW = RsTheme.S(150f);
            if (RsElements.Button("Cancel##rs_crop_cancel", RsElements.ButtonVariant.Ghost, new Vector2(RsTheme.S(100f), 0f))) { Finish(false, string.Empty); return; }
            ImGui.SameLine();
            var noSel = !_hasSel;
            if (noSel) ImGui.BeginDisabled();
            if (RsElements.Button("Reset##rs_crop_reset", RsElements.ButtonVariant.Ghost, new Vector2(RsTheme.S(90f), 0f)))
            { _hasSel = false; _dragging = false; _mode = DragMode.None; _aspect = _openAspect; }
            if (noSel) ImGui.EndDisabled();
            if (_openAspect <= 0f)
            {
                ImGui.SameLine();
                var locked = _aspect > 0f;
                if (ImGui.Checkbox("Lock shape##rs_crop_lock", ref locked))
                    _aspect = locked ? (_hasSel ? (_selB.X - _selA.X) / MathF.Max(1f, _selB.Y - _selA.Y) : 1f) : 0f;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Keep the current width-to-height ratio while resizing (square if nothing is selected yet).");
            }
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(0f, ImGui.GetContentRegionAvail().X - btnW * 2f - RsTheme.S(12f)));
            if (RsElements.Button("Use whole image##rs_crop_whole", RsElements.ButtonVariant.Ghost, new Vector2(btnW, 0f))) { Finish(true, _path); return; }
            ImGui.SameLine();
            if (!_hasSel) ImGui.BeginDisabled();
            if (RsElements.Button("Crop & use##rs_crop_apply", RsElements.ButtonVariant.Primary, new Vector2(btnW, 0f)))
            {
                var outPath = SaveCrop();
                if (outPath != null) { Finish(true, outPath); return; }
            }
            if (!_hasSel) ImGui.EndDisabled();
        }
        catch (Exception ex) { Plugin.PluginLog.Debug("RsImageCrop draw: " + ex.Message); }
        finally { ImGui.EndPopup(); }
        if (!openState) Finish(false, string.Empty);
    }

    // Mouse handling for the canvas item that was just submitted.
    private static void HandleInput(Vector2 imgMin, float scale)
    {
        var io = ImGui.GetIO();
        var mouse = io.MousePos;
        var raw = (mouse - imgMin) / scale;                       // unclamped source position
        var lim = new Vector2(_srcW, _srcH);
        var src = Vector2.Clamp(raw, Vector2.Zero, lim);
        var hovered = ImGui.IsItemHovered();

        // What is under the mouse (screen-space tolerance so handles stay grabbable at any zoom).
        int hoverHandle = 0; bool inside = false;
        if (_hasSel && _mode == DragMode.None)
        {
            var sMin = imgMin + _selA * scale; var sMax = imgMin + _selB * scale;
            var tol = RsTheme.S(7f);
            if (mouse.X >= sMin.X - tol && mouse.X <= sMax.X + tol && mouse.Y >= sMin.Y - tol && mouse.Y <= sMax.Y + tol)
            {
                if (MathF.Abs(mouse.X - sMin.X) <= tol) hoverHandle |= HL; else if (MathF.Abs(mouse.X - sMax.X) <= tol) hoverHandle |= HR;
                if (MathF.Abs(mouse.Y - sMin.Y) <= tol) hoverHandle |= HT; else if (MathF.Abs(mouse.Y - sMax.Y) <= tol) hoverHandle |= HB;
                inside = mouse.X > sMin.X && mouse.X < sMax.X && mouse.Y > sMin.Y && mouse.Y < sMax.Y;
            }
        }

        if (ImGui.IsItemActivated())
        {
            _dragStart = raw; _startA = _selA; _startB = _selB; _dragging = true;
            if (_hasSel && hoverHandle != 0) { _mode = DragMode.Resize; _handle = hoverHandle; }
            else if (_hasSel && inside) _mode = DragMode.Move;
            else { _mode = DragMode.Create; _dragStart = src; _selA = _selB = src; _hasSel = false; }
        }

        if (_mode != DragMode.None)
        {
            if (_mode == DragMode.Create) AnchoredRect(_dragStart, src);
            else if (_mode == DragMode.Move)
            {
                var size = _startB - _startA;
                var a = Vector2.Clamp(_startA + (raw - _dragStart), Vector2.Zero, lim - size);
                _selA = a; _selB = a + size;
            }
            else ApplyResize(src);

            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                if (_mode == DragMode.Create) _hasSel = _selB.X - _selA.X >= MinSel && _selB.Y - _selA.Y >= MinSel;
                _mode = DragMode.None; _dragging = false;
            }
        }
        else if (_hasSel && hovered && io.MouseWheel != 0f) ApplyWheel(io.MouseWheel);

        // Cursor feedback.
        var h = _mode == DragMode.Resize ? _handle : (_mode == DragMode.None && hovered ? hoverHandle : 0);
        if (h != 0)
        {
            bool horiz = (h & (HL | HR)) != 0, vert = (h & (HT | HB)) != 0;
            if (horiz && vert)
                ImGui.SetMouseCursor(((h & HL) != 0) == ((h & HT) != 0) ? ImGuiMouseCursor.ResizeNwse : ImGuiMouseCursor.ResizeNesw);
            else ImGui.SetMouseCursor(horiz ? ImGuiMouseCursor.ResizeEw : ImGuiMouseCursor.ResizeNs);
        }
        else if (_mode == DragMode.Move || (_mode == DragMode.None && inside && hovered)) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
    }

    private static void SetSel(Vector2 a, Vector2 b) { _selA = Vector2.Min(a, b); _selB = Vector2.Max(a, b); }

    // Rectangle from a fixed anchor towards p, honouring the locked aspect and the image bounds.
    private static void AnchoredRect(Vector2 anchor, Vector2 p)
    {
        if (_aspect <= 0f) { SetSel(anchor, p); return; }
        float dx = p.X >= anchor.X ? 1f : -1f, dy = p.Y >= anchor.Y ? 1f : -1f;
        float w = MathF.Abs(p.X - anchor.X), h = MathF.Abs(p.Y - anchor.Y);
        if (w / MathF.Max(h, 0.001f) > _aspect) h = w / _aspect; else w = h * _aspect;
        float wMax = dx > 0 ? _srcW - anchor.X : anchor.X, hMax = dy > 0 ? _srcH - anchor.Y : anchor.Y;
        var k = MathF.Min(1f, MathF.Min(wMax / MathF.Max(w, 0.001f), hMax / MathF.Max(h, 0.001f)));
        SetSel(anchor, anchor + new Vector2(w * k * dx, h * k * dy));
    }

    private static void ApplyResize(Vector2 src)
    {
        var a = _startA; var b = _startB;
        bool corner = (_handle & (HL | HR)) != 0 && (_handle & (HT | HB)) != 0;
        if (_aspect > 0f && corner)
        {
            var anchor = new Vector2((_handle & HL) != 0 ? b.X : a.X, (_handle & HT) != 0 ? b.Y : a.Y);
            // Keep the drag on the far side of the anchor so the handle can not flip through it.
            var p = new Vector2(
                (_handle & HL) != 0 ? MathF.Min(src.X, anchor.X - MinSel) : MathF.Max(src.X, anchor.X + MinSel),
                (_handle & HT) != 0 ? MathF.Min(src.Y, anchor.Y - MinSel) : MathF.Max(src.Y, anchor.Y + MinSel));
            AnchoredRect(anchor, p);
            return;
        }
        if ((_handle & HL) != 0) a.X = MathF.Min(src.X, b.X - MinSel);
        if ((_handle & HR) != 0) b.X = MathF.Max(src.X, a.X + MinSel);
        if ((_handle & HT) != 0) a.Y = MathF.Min(src.Y, b.Y - MinSel);
        if ((_handle & HB) != 0) b.Y = MathF.Max(src.Y, a.Y + MinSel);
        if (_aspect > 0f)
        {
            // Edge drag with a locked shape: the other axis grows about its centre.
            var c = (_startA + _startB) * 0.5f;
            if ((_handle & (HL | HR)) != 0)
            {
                var w = b.X - a.X;
                var hh = MathF.Min(w / _aspect, 2f * MathF.Min(c.Y, _srcH - c.Y)); w = hh * _aspect;
                if ((_handle & HL) != 0) a.X = b.X - w; else b.X = a.X + w;
                a.Y = c.Y - hh * 0.5f; b.Y = c.Y + hh * 0.5f;
            }
            else
            {
                var hh = b.Y - a.Y;
                var w = MathF.Min(hh * _aspect, 2f * MathF.Min(c.X, _srcW - c.X)); hh = w / _aspect;
                if ((_handle & HT) != 0) a.Y = b.Y - hh; else b.Y = a.Y + hh;
                a.X = c.X - w * 0.5f; b.X = c.X + w * 0.5f;
            }
        }
        var lim = new Vector2(_srcW, _srcH);
        SetSel(Vector2.Clamp(a, Vector2.Zero, lim), Vector2.Clamp(b, Vector2.Zero, lim));
    }

    // Scale about the centre (scroll up = tighter crop), keeping shape and staying inside the image.
    private static void ApplyWheel(float wheel)
    {
        var lim = new Vector2(_srcW, _srcH);
        var c = (_selA + _selB) * 0.5f;
        var size = (_selB - _selA) * MathF.Pow(1.1f, -wheel);
        size *= MathF.Min(1f, MathF.Min(_srcW / size.X, _srcH / size.Y));
        size *= MathF.Max(1f, MathF.Max(MinSel / size.X, MinSel / size.Y));
        size = Vector2.Min(size, lim);
        var a = Vector2.Clamp(c - size * 0.5f, Vector2.Zero, lim - size);
        _selA = a; _selB = a + size;
    }

    private static string? SaveCrop()
    {
        try
        {
            var x = (int)MathF.Round(_selA.X); var y = (int)MathF.Round(_selA.Y);
            var w = (int)MathF.Round(_selB.X - _selA.X); var h = (int)MathF.Round(_selB.Y - _selA.Y);
            using var src = new System.Drawing.Bitmap(_path);
            x = Math.Clamp(x, 0, src.Width - 1); y = Math.Clamp(y, 0, src.Height - 1);
            w = Math.Clamp(w, 1, src.Width - x); h = Math.Clamp(h, 1, src.Height - y);
            using var cut = src.Clone(new System.Drawing.Rectangle(x, y, w, h), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var dir = Path.Combine(Path.GetTempPath(), "AbsoluteRP", "crops");
            Directory.CreateDirectory(dir);
            // Keep the folder small: drop crops older than a day.
            try { foreach (var f in Directory.GetFiles(dir)) if (File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddDays(-1)) File.Delete(f); } catch { }
            var outPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(_path) + "_crop_" + DateTime.UtcNow.Ticks + ".png");
            cut.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
            return outPath;
        }
        catch (Exception ex)
        {
            _error = "Could not crop that image: " + ex.Message;
            return null;
        }
    }
}
