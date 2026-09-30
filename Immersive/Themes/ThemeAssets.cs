using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using AbsoluteRP.RsUI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace AbsoluteRP.Immersive.Themes;

// Images carried inside a theme document (ThemeAsset, base64 PNG) and everything that turns them into pixels: Texture(asset) GPU texture for an asset, cached Masked(srcKey, src, mask) a source image with the mask's alpha multiplied in (CPU composite, cached) Draw(dl, doc, ImageRef) stretch / cover / contain / 9-slice Import(path) file -> downscaled PNG asset Masking is done on the CPU because ImGui has no stencil: the avatar, gallery tiles and background images are static bitmaps, so a composite per (image, mask) pair is cheap and cached.
public static class ThemeAssets
{
    public const int MaxEdge = 2048;                 // imports are downscaled to this on the long side
    public const int MaxAssetBytes = 8 * 1024 * 1024; // per image after downscaling
    public const int MaxAssets = 40;

    // A pseudo-asset: the viewed profile's own background (image or video), so a panel or Image element can show it in place.
    public const string ProfileBackgroundId = "profile:background";

    private static readonly Dictionary<string, IDalamudTextureWrap?> _tex = new();
    private static readonly HashSet<string> _loading = new();
    private static readonly object _lock = new();

    private static string Key(ThemeAsset a) => "asset:" + a.Id + ":" + (a.Png?.Length ?? 0);

    // textures
    public static IDalamudTextureWrap? Texture(ThemeAsset? a)
    {
        if (a == null || string.IsNullOrEmpty(a.Png)) return null;
        var key = Key(a);
        if (TryCached(key, out var cached)) return cached;
        _ = LoadAsync(key, () => Convert.FromBase64String(a.Png));
        return null;
    }

    // `src` is any encoded image (png/jpg) - the avatar bytes, a gallery image, or another asset. The mask's alpha (or luminance when the mask has no alpha) is resampled to the source size and multiplied into it.
    public static IDalamudTextureWrap? Masked(string srcKey, byte[]? src, ThemeAsset? mask)
    {
        if (src == null || src.Length == 0 || mask == null || string.IsNullOrEmpty(mask.Png)) return null;
        var key = "masked:" + srcKey + ":" + src.Length + ":" + Key(mask);
        if (TryCached(key, out var cached)) return cached;
        var maskPng = mask.Png;
        _ = LoadAsync(key, () => Composite(src, Convert.FromBase64String(maskPng)));
        return null;
    }

    // For masking a source that changes every frame (the profile's video background): an overlay built ONCE from the mask - white, opaque where the mask is transparent and clear where it is opaque. It is drawn over the frame tinted with the panel colour, so masking a video costs one extra textured quad per frame and no per-frame CPU work.
    public static IDalamudTextureWrap? InverseMask(ThemeAsset? mask)
    {
        if (mask == null || string.IsNullOrEmpty(mask.Png)) return null;
        var key = "invmask:" + Key(mask);
        if (TryCached(key, out var cached)) return cached;
        var maskPng = mask.Png;
        _ = LoadAsync(key, () => Invert(Convert.FromBase64String(maskPng)));
        return null;
    }

    // Cache hit -> the texture (may be null for a failed load). Miss -> false, and the caller starts a load unless one is already running. Every hit stamps the frame so unused entries (old undo states, removed/replaced assets, other people's avatars) can be evicted.
    private static readonly Dictionary<string, int> _lastUse = new();
    private static int _lastSweep;
    private const int EvictAfterFrames = 1800;   // ~30 s at 60 fps
    private static bool _shutdown;

    private static bool TryCached(string key, out IDalamudTextureWrap? tex)
    {
        int now;
        try { now = ImGui.GetFrameCount(); } catch { now = 0; }
        lock (_lock)
        {
            Sweep(now);
            if (_tex.TryGetValue(key, out tex)) { _lastUse[key] = now; return true; }
            tex = null;
            // Already loading: report "hit" with null so no second load starts.
            return !_loading.Add(key);
        }
    }

    // Under _lock. Evicted textures were not drawn for EvictAfterFrames, but still go through the graveyard (never dispose synchronously).
    private static void Sweep(int now)
    {
        if (now - _lastSweep < 120) return;
        _lastSweep = now;
        List<string>? dead = null;
        foreach (var kv in _lastUse)
            if (now - kv.Value > EvictAfterFrames) (dead ??= new()).Add(kv.Key);
        if (dead == null) return;
        foreach (var k in dead)
        {
            _lastUse.Remove(k);
            if (_tex.Remove(k, out var t)) AbsoluteRP.Helpers.TextureGraveyard.Enqueue(t);
        }
    }

    private static async Task LoadAsync(string key, Func<byte[]> produce)
    {
        IDalamudTextureWrap? result = null;
        try
        {
            var png = await Task.Run(produce);
            result = await Plugin.TextureProvider.CreateFromImageAsync(png);
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.Warning($"[ThemeAssets] {key}: {ex.Message}");
        }
        finally
        {
            // Publish atomically, once complete. A key is only loaded once while cached, so there is never a previous texture to replace; if one somehow exists, retire it rather than dispose it.
            lock (_lock)
            {
                _loading.Remove(key);
                if (_shutdown) { try { result?.Dispose(); } catch { } }
                else
                {
                    if (_tex.TryGetValue(key, out var prev) && prev != null && !ReferenceEquals(prev, result))
                        AbsoluteRP.Helpers.TextureGraveyard.Enqueue(prev);
                    _tex[key] = result;
                    try { _lastUse[key] = ImGui.GetFrameCount(); } catch { _lastUse[key] = 0; }
                }
            }
        }
    }

    public static void DisposeAll()
    {
        lock (_lock)
        {
            _shutdown = true;
            // Plugin unload path: the graveyard is flushed at the end of Plugin.Dispose, so routing through it is still safe here.
            foreach (var t in _tex.Values) AbsoluteRP.Helpers.TextureGraveyard.Enqueue(t);
            _tex.Clear();
            _lastUse.Clear();
            _loading.Clear();
        }
    }

    // CPU compositing
    private static byte[] Composite(byte[] srcBytes, byte[] maskBytes)
    {
        using var srcIn = new System.Drawing.Bitmap(new MemoryStream(srcBytes));
        using var src = new System.Drawing.Bitmap(srcIn.Width, srcIn.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = System.Drawing.Graphics.FromImage(src)) g.DrawImage(srcIn, 0, 0, srcIn.Width, srcIn.Height);
        using var maskIn = new System.Drawing.Bitmap(new MemoryStream(maskBytes));
        using var mask = new System.Drawing.Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = System.Drawing.Graphics.FromImage(mask))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.Clear(System.Drawing.Color.Transparent);
            g.DrawImage(maskIn, 0, 0, src.Width, src.Height);
        }
        var rect = new System.Drawing.Rectangle(0, 0, src.Width, src.Height);
        var sd = src.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var md = mask.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        // Masks with no transparency at all are read by brightness instead.
        bool maskHasAlpha = false;
        unsafe
        {
            for (int y = 0; y < src.Height && !maskHasAlpha; y++)
            {
                var mp = (byte*)md.Scan0 + y * md.Stride;
                for (int x = 0; x < src.Width; x++) if (mp[x * 4 + 3] < 250) { maskHasAlpha = true; break; }
            }
            for (int y = 0; y < src.Height; y++)
            {
                var sp = (byte*)sd.Scan0 + y * sd.Stride;
                var mp = (byte*)md.Scan0 + y * md.Stride;
                for (int x = 0; x < src.Width; x++)
                {
                    var m = maskHasAlpha ? mp[x * 4 + 3] : (byte)((mp[x * 4] * 114 + mp[x * 4 + 1] * 587 + mp[x * 4 + 2] * 299) / 1000);
                    sp[x * 4 + 3] = (byte)(sp[x * 4 + 3] * m / 255);
                }
            }
        }
        src.UnlockBits(sd);
        mask.UnlockBits(md);
        using var ms = new MemoryStream();
        src.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }

    private static byte[] Invert(byte[] maskBytes)
    {
        using var maskIn = new System.Drawing.Bitmap(new MemoryStream(maskBytes));
        using var bmp = new System.Drawing.Bitmap(maskIn.Width, maskIn.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = System.Drawing.Graphics.FromImage(bmp)) { g.Clear(System.Drawing.Color.Transparent); g.DrawImage(maskIn, 0, 0, maskIn.Width, maskIn.Height); }
        var rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
        var d = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        bool hasAlpha = false;
        unsafe
        {
            for (int y = 0; y < bmp.Height && !hasAlpha; y++)
            {
                var p = (byte*)d.Scan0 + y * d.Stride;
                for (int x = 0; x < bmp.Width; x++) if (p[x * 4 + 3] < 250) { hasAlpha = true; break; }
            }
            for (int y = 0; y < bmp.Height; y++)
            {
                var p = (byte*)d.Scan0 + y * d.Stride;
                for (int x = 0; x < bmp.Width; x++)
                {
                    var m = hasAlpha ? p[x * 4 + 3] : (byte)((p[x * 4] * 114 + p[x * 4 + 1] * 587 + p[x * 4 + 2] * 299) / 1000);
                    p[x * 4] = 255; p[x * 4 + 1] = 255; p[x * 4 + 2] = 255; p[x * 4 + 3] = (byte)(255 - m);
                }
            }
        }
        bmp.UnlockBits(d);
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }

    // import
    public static ThemeAsset? Import(string path, out string error)
    {
        error = "";
        try
        {
            using var img = new System.Drawing.Bitmap(path);
            var scale = MathF.Min(1f, MaxEdge / (float)Math.Max(img.Width, img.Height));
            var w = Math.Max(1, (int)(img.Width * scale)); var h = Math.Max(1, (int)(img.Height * scale));
            using var dst = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = System.Drawing.Graphics.FromImage(dst))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.Clear(System.Drawing.Color.Transparent);
                g.DrawImage(img, 0, 0, w, h);
            }
            using var ms = new MemoryStream();
            dst.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            var bytes = ms.ToArray();
            if (bytes.Length > MaxAssetBytes) { error = $"Image is too large after downscaling ({bytes.Length / 1024} KB; limit {MaxAssetBytes / 1024} KB). Try a simpler or smaller image."; return null; }
            return new ThemeAsset { Name = Path.GetFileNameWithoutExtension(path), Png = Convert.ToBase64String(bytes), Width = w, Height = h };
        }
        catch (Exception ex) { error = "Couldn't read that image: " + ex.Message; return null; }
    }

    // drawing
    public static ThemeAsset? Find(ThemeDocument? doc, string? id)
        => string.IsNullOrEmpty(id) ? null : doc?.Assets?.Find(a => a.Id == id);

    // Draws an ImageRef into [min,max]. Returns false when the texture isn't ready yet (caller may draw a fallback).
    public static bool Draw(ImDrawListPtr dl, ThemeDocument? doc, ImageRef? r, Vector2 min, Vector2 max, float alpha, IDalamudTextureWrap? textureOverride = null, ThemeAsset? overlayMask = null, Vector4 overlayColor = default)
    {
        if (r == null || string.IsNullOrEmpty(r.Asset)) return false;
        ImTextureID handle; int texW, texH;
        if (r.Asset == ProfileBackgroundId)
        {
            // Whatever the viewed profile uses as its backdrop right now: a video frame or the still image.
            try { handle = AbsoluteRP.Windows.Profiles.ProfileTypeWindows.TargetProfileWindow.GetBackdropTexture(out texW, out texH); }
            catch { return false; }
            if (texW <= 0 || texH <= 0) return false;
            if (overlayMask != null)
            {
                // Hold the frame back until the overlay is ready, so the unmasked video never flashes.
                var inv = InverseMask(overlayMask);
                if (inv == null || inv.Handle == IntPtr.Zero) return false;
                if (!DrawTexture(dl, handle, texW, texH, r, min, max, alpha, out var dMin, out var dMax)) return false;
                var ocol = ImmersiveMode.Col(overlayColor, alpha);
                var oround = RsTheme.S(r.Rounding);
                if (oround > 0f) dl.AddImageRounded(inv.Handle, dMin, dMax, Vector2.Zero, Vector2.One, ocol, oround);
                else dl.AddImage(inv.Handle, dMin, dMax, Vector2.Zero, Vector2.One, ocol);
                return true;
            }
        }
        else
        {
            var asset = Find(doc, r.Asset);
            var tex = textureOverride ?? Texture(asset);
            if (tex == null || tex.Handle == IntPtr.Zero || asset == null) return false;
            handle = tex.Handle; texW = asset.Width; texH = asset.Height;
        }
        return DrawTexture(dl, handle, texW, texH, r, min, max, alpha, out _, out _);
    }

    private static bool DrawTexture(ImDrawListPtr dl, ImTextureID handle, int texW, int texH, ImageRef r, Vector2 min, Vector2 max, float alpha, out Vector2 dMin, out Vector2 dMax)
    {
        var inset = RsTheme.S(r.Inset);
        min += new Vector2(inset); max -= new Vector2(inset);
        dMin = min; dMax = max;
        if (max.X - min.X < 1f || max.Y - min.Y < 1f) return true;
        var tint = r.Tint != null ? r.Tint.V : Vector4.One;
        var col = ImmersiveMode.Col(tint, alpha * r.Alpha);
        var round = RsTheme.S(r.Rounding);
        if (r.Border > 0f)
        {
            NineSlice(dl, handle, min, max, RsTheme.S(r.Border), texW, texH, col);
            return true;
        }
        var uv0 = Vector2.Zero; var uv1 = Vector2.One;
        var w = max.X - min.X; var h = max.Y - min.Y;
        var ia = texW / (float)Math.Max(1, texH);
        var ra = w / h;
        switch (r.Fit)
        {
            case ImageFit.Cover:
                if (ia > ra) { var f = ra / ia; uv0.X = (1f - f) * 0.5f; uv1.X = 1f - uv0.X; }
                else { var f = ia / ra; uv0.Y = (1f - f) * 0.5f; uv1.Y = 1f - uv0.Y; }
                break;
            case ImageFit.Contain:
                if (ia > ra) { var nh = w / ia; var c = (min.Y + max.Y) * 0.5f; min.Y = c - nh * 0.5f; max.Y = c + nh * 0.5f; }
                else { var nw = h * ia; var c = (min.X + max.X) * 0.5f; min.X = c - nw * 0.5f; max.X = c + nw * 0.5f; }
                break;
        }
        dMin = min; dMax = max;
        if (round > 0f) dl.AddImageRounded(handle, min, max, uv0, uv1, col, round);
        else dl.AddImage(handle, min, max, uv0, uv1, col);
        return true;
    }

    // Classic 9-slice: corners keep their pixel size, edges stretch along one axis, the centre stretches both ways. `border` is the on-screen corner size; the texture border is the same fraction of the image.
    private static void NineSlice(ImDrawListPtr dl, ImTextureID tex, Vector2 min, Vector2 max, float border, int texW, int texH, uint col)
    {
        var w = max.X - min.X; var h = max.Y - min.Y;
        var b = MathF.Min(border, MathF.Min(w, h) * 0.5f);
        // Texture-space border: the same number of source pixels as the on-screen border, capped just under half the image.
        var tbx = MathF.Min(0.49f, MathF.Max(0.05f, border / (float)texW));
        var tby = MathF.Min(0.49f, MathF.Max(0.05f, border / (float)texH));
        float[] xs = { min.X, min.X + b, max.X - b, max.X };
        float[] ys = { min.Y, min.Y + b, max.Y - b, max.Y };
        float[] us = { 0f, tbx, 1f - tbx, 1f };
        float[] vs = { 0f, tby, 1f - tby, 1f };
        for (int j = 0; j < 3; j++)
            for (int i = 0; i < 3; i++)
            {
                if (xs[i + 1] <= xs[i] || ys[j + 1] <= ys[j]) continue;
                dl.AddImage(tex, new Vector2(xs[i], ys[j]), new Vector2(xs[i + 1], ys[j + 1]), new Vector2(us[i], vs[j]), new Vector2(us[i + 1], vs[j + 1]), col);
            }
    }
}
