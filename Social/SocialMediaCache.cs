using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Interface.Textures.TextureWraps;

namespace AbsoluteRP.Social;

// URL -> texture cache for images embedded in social posts. Fetch is fire-and-forget; the first render kicks off a background download and subsequent frames read the cached wrap once it lands.
public static class SocialMediaCache
{
    private static readonly ConcurrentDictionary<string, IDalamudTextureWrap?> _textures = new();
    private static readonly ConcurrentDictionary<string, byte>                 _loading  = new();
    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(20),
    };

    public static IDalamudTextureWrap? Get(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (_textures.TryGetValue(url!, out var wrap)) return wrap;
        // Not seen before - start a background load, return null this frame.
        if (_loading.TryAdd(url!, 0)) _ = LoadAsync(url!);
        return null;
    }

    private static async Task LoadAsync(string url)
    {
        try
        {
            var bytes = await _http.GetByteArrayAsync(url);
            if (bytes == null || bytes.Length < 12) throw new Exception("empty response");
            var tex = await Plugin.TextureProvider.CreateFromImageAsync(bytes);
            _textures[url] = tex;
        }
        catch (Exception ex)
        {
            // Cache the failure as null so we don't loop retrying.
            _textures[url] = null;
            Plugin.PluginLog?.Debug($"SocialMediaCache.Load({url}): {ex.Message}");
        }
        finally
        {
            _loading.TryRemove(url, out _);
        }
    }

    public static void Clear()
    {
        foreach (var kv in _textures)
        {
            try { kv.Value?.Dispose(); } catch { }
        }
        _textures.Clear();
        _loading.Clear();
    }
}
