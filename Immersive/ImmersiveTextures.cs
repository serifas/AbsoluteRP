using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dalamud.Interface.Textures.TextureWraps;

namespace AbsoluteRP.Immersive;

// Lazy loader for the texture files under UI/immersive. Themes ask for a texture every frame; the first request kicks off a background decode and the theme falls back to its procedural fill until the GPU upload lands. `keyWhite` turns a white studio background into transparency - the parchment scan is a JPEG (no alpha), so the torn silhouette is recovered from luminance: pure white -> clear, paper tones -> opaque, with a short ramp so the fringe stays anti-aliased.
public static class ImmersiveTextures
{
    private static readonly Dictionary<string, IDalamudTextureWrap?> _cache = new();
    private static readonly HashSet<string> _loading = new();
    private static readonly object _lock = new();

    public static IDalamudTextureWrap? Get(string relPath, bool keyWhite = false)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(relPath, out var tex)) return tex;
            if (_loading.Contains(relPath)) return null;
            _loading.Add(relPath);
        }
        _ = LoadAsync(relPath, keyWhite);
        return null;
    }

    private static async Task LoadAsync(string relPath, bool keyWhite)
    {
        IDalamudTextureWrap? result = null;
        try
        {
            var dir = Plugin.PluginInterface?.AssemblyLocation.Directory?.FullName;
            if (string.IsNullOrEmpty(dir)) return;
            var full = Path.Combine(dir, relPath);
            if (!File.Exists(full))
            {
                Plugin.PluginLog?.Warning($"[ImmersiveTextures] missing {full}");
                return;
            }
            byte[] pngBytes = await Task.Run(() => keyWhite ? KeyWhiteToPng(full) : File.ReadAllBytes(full));
            result = await Plugin.TextureProvider.CreateFromImageAsync(pngBytes);
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.Warning($"[ImmersiveTextures] {relPath}: {ex.Message}");
        }
        finally
        {
            lock (_lock)
            {
                _cache[relPath] = result;
                _loading.Remove(relPath);
            }
        }
    }

    // Decode with System.Drawing, rewrite alpha from luminance, re-encode as PNG so the texture provider gets a real alpha channel.
    private static byte[] KeyWhiteToPng(string path)
    {
        using var src = new System.Drawing.Bitmap(path);
        using var dst = new System.Drawing.Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var rect = new System.Drawing.Rectangle(0, 0, src.Width, src.Height);
        var sd = src.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var dd = dst.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            unsafe
            {
                for (int y = 0; y < src.Height; y++)
                {
                    var s = (byte*)sd.Scan0 + y * sd.Stride;
                    var d = (byte*)dd.Scan0 + y * dd.Stride;
                    for (int x = 0; x < src.Width; x++)
                    {
                        byte b = s[x * 4 + 0], g = s[x * 4 + 1], r = s[x * 4 + 2];
                        int m = Math.Min(r, Math.Min(g, b));
                        // 255 -> 0 alpha; <= 228 -> fully opaque.
                        float a = Math.Clamp((246f - m) / 18f, 0f, 1f);
                        d[x * 4 + 0] = b; d[x * 4 + 1] = g; d[x * 4 + 2] = r;
                        d[x * 4 + 3] = (byte)(a * 255f + 0.5f);
                    }
                }
            }
        }
        finally
        {
            src.UnlockBits(sd);
            dst.UnlockBits(dd);
        }
        using var ms = new MemoryStream();
        dst.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }

    public static void DisposeAll()
    {
        try { AbsoluteRP.Immersive.Themes.ThemeAssets.DisposeAll(); } catch { }
        lock (_lock)
        {
            foreach (var t in _cache.Values)
            {
                try { t?.Dispose(); } catch { }
            }
            _cache.Clear();
            _loading.Clear();
        }
    }
}
