using System;
using System.Collections.Generic;
using AbsoluteRP.Video;

namespace AbsoluteRP.Social;

// Per-URL video source for the social feed. Two independent tracks: PosterSession - a short-lived VideoPlayerSession that decodes exactly one frame, freezes (kills its own subprocess but keeps the D3D11. SRV live), and serves that SRV as a still thumbnail with zero ongoing cost. This is what the feed shows by default. InlineSession - an optional live-playback session, created when the user clicks the thumbnail. Same URL, fresh subprocess, unmuted. Its SRV shadows the poster while it exists. Both are cleaned up via the standard deferred-drain queue so the. NVIDIA driver has a full Present cycle to consume any queued draws against a retired SRV before we free it.
internal static class SocialVideoThumbCache
{
    private sealed class Entry
    {
        public VideoPlayerSession? Poster;
        public VideoPlayerSession? Inline;
        public long LastAccessedFrame;
        public bool PosterFrozen;  // stops us re-freezing every frame
    }

    // Cap concurrent poster sessions. Each one runs a libVLC subprocess for a fraction of a second until it captures + freezes, so this cap matters most during the initial decode burst.
    private const int MaxSessions       = 8;
    private const int EvictAfterFrames  = 240;  // ~4s @ 60fps of no reads
    private const int RetireDrainFrames = 2;

    private static readonly Dictionary<string, Entry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<VideoPlayerSession> _retired = new();
    private static long _frameCounter;

    // Called once per Draw frame from SocialPage before any DrawVideoBlock.
    private static int _beganImGuiFrame = -1;
    public static void BeginFrame()
    {
        // Once per frame no matter how many places ask (social page, profiles, chat).
        int f; try { f = Dalamud.Bindings.ImGui.ImGui.GetFrameCount(); } catch { f = -2; }
        if (f == _beganImGuiFrame) return;
        _beganImGuiFrame = f;
        _lastDrawnTicksMs = System.Environment.TickCount64;
        _frameCounter++;
        DrainRetired();
        StopInlineOffscreen();
        EvictStale();
    }

    // Same heartbeat pattern as SocialVideoPopup: framework-thread tick notices when SocialPage stops calling BeginFrame (page hidden / window closed) and retires every inline session so no audio keeps playing in the background. Posters stay under the LRU window.
    private static long _lastDrawnTicksMs;
    private const int  StaleThresholdMs = 250;
    public static void HeartbeatTick()
    {
        if (_cache.Count == 0 && _retired.Count == 0) return;
        var now = System.Environment.TickCount64;
        if (_lastDrawnTicksMs == 0) { _lastDrawnTicksMs = now; return; }
        if (now - _lastDrawnTicksMs > StaleThresholdMs)
        {
            foreach (var kv in _cache)
            {
                var e = kv.Value;
                if (e.Inline != null) { _retired.Add(e.Inline); e.Inline = null; }
            }
            DrainRetired();
        }
    }

    // Stop the inline (live-playback) session on any URL that wasn't asked for in the frame that just ended. That's how "go back to the feed" / "change tab" / "collapse post" all translate into "stop the audio" - the block simply isn't drawn any more, so its URL doesn't touch LastAccessedFrame, and we retire the inline immediately here. The frozen poster stays behind under the LRU window so a re-open still renders the still frame instantly.
    private static void StopInlineOffscreen()
    {
        if (_cache.Count == 0) return;
        // BeginFrame just incremented _frameCounter, so "drawn last frame" means LastAccessedFrame == _frameCounter - 1.
        var stillVisible = _frameCounter - 1;
        foreach (var kv in _cache)
        {
            var e = kv.Value;
            if (e.Inline != null && e.LastAccessedFrame != stillVisible)
            {
                _retired.Add(e.Inline);
                e.Inline = null;
            }
        }
    }

    // Returns the current SRV + dims for this URL, spinning up a new poster session if we don't have one yet. When an Inline session is active for this URL its SRV shadows the poster.
    public static bool TryGet(string url, out IntPtr texId, out int w, out int h)
    {
        texId = IntPtr.Zero; w = 0; h = 0;
        if (string.IsNullOrWhiteSpace(url)) return false;
        var entry = EnsureEntry(url);
        if (entry == null) return false;
        entry.LastAccessedFrame = _frameCounter;

        // Prefer inline (live playback) SRV when the user has clicked to play.
        var inline = entry.Inline;
        if (inline != null && !inline.RendererCrashed)
        {
            var iid = inline.CurrentTextureId;
            if (iid != IntPtr.Zero)
            {
                texId = iid;
                w = inline.Width;
                h = inline.Height;
                return true;
            }
            // Live session hasn't produced its first frame yet - fall through to the frozen poster so the box isn't blank while playback spins up.
        }

        var poster = entry.Poster;
        if (poster == null || poster.RendererCrashed) { Retire(url); return false; }
        var pid = poster.CurrentTextureId;
        if (pid == IntPtr.Zero) return false;
        texId = pid;
        w = poster.Width;
        h = poster.Height;

        // Freeze the poster the first time we see a valid SRV. From then on it costs zero CPU and holds a persistent still frame.
        if (!entry.PosterFrozen && !poster.IsFrozen)
        {
            entry.PosterFrozen = true;
            try { poster.Freeze(); }
            catch (Exception ex) { Plugin.PluginLog?.Debug("SocialVideoThumbCache freeze: " + ex.Message); }
        }
        return true;
    }

    // Ensure a live inline session exists for this URL. Retires the old one if a session was already running so callers can safely call this from a click handler without worrying about duplication.
    public static void StartInline(string url, int volume)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        var entry = EnsureEntry(url);
        if (entry == null) return;
        if (entry.Inline != null)
        {
            _retired.Add(entry.Inline);
            entry.Inline = null;
        }
        try
        {
            entry.Inline = new VideoPlayerSession(
                Plugin.PluginInterface, Plugin.PluginLog, url, initialVolume: Math.Clamp(volume, 0, 100));
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.Debug("SocialVideoThumbCache StartInline: " + ex.Message);
        }
    }

    // Retire the inline (live) session for a URL. Poster stays.
    public static void StopInline(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!_cache.TryGetValue(url, out var entry) || entry.Inline == null) return;
        _retired.Add(entry.Inline);
        entry.Inline = null;
    }

    public static bool IsInlinePlaying(string url)
        => !string.IsNullOrWhiteSpace(url)
           && _cache.TryGetValue(url, out var e)
           && e.Inline != null;

    // Returns the current inline (live) session for a URL, or null. Used for transport controls like toggle-pause on the inline block.
    public static VideoPlayerSession? GetInline(string url)
        => !string.IsNullOrWhiteSpace(url) && _cache.TryGetValue(url, out var e)
           ? e.Inline
           : null;

    private static Entry? EnsureEntry(string url)
    {
        if (_cache.TryGetValue(url, out var existing)) return existing;
        if (_cache.Count >= MaxSessions) EvictOldest();
        try
        {
            var poster = new VideoPlayerSession(
                Plugin.PluginInterface, Plugin.PluginLog, url, initialVolume: 0);
            var entry = new Entry { Poster = poster, LastAccessedFrame = _frameCounter };
            _cache[url] = entry;
            return entry;
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.Debug("SocialVideoThumbCache create: " + ex.Message);
            return null;
        }
    }

    private static void EvictStale()
    {
        if (_cache.Count == 0) return;
        List<string>? stale = null;
        foreach (var kv in _cache)
        {
            if (_frameCounter - kv.Value.LastAccessedFrame > EvictAfterFrames)
                (stale ??= new List<string>()).Add(kv.Key);
        }
        if (stale != null) for (int i = 0; i < stale.Count; i++) Retire(stale[i]);
    }

    private static void EvictOldest()
    {
        string? oldestKey = null;
        long oldest = long.MaxValue;
        foreach (var kv in _cache)
        {
            if (kv.Value.LastAccessedFrame < oldest)
            {
                oldest = kv.Value.LastAccessedFrame;
                oldestKey = kv.Key;
            }
        }
        if (oldestKey != null) Retire(oldestKey);
    }

    private static void Retire(string url)
    {
        if (!_cache.Remove(url, out var e)) return;
        if (e.Poster != null) _retired.Add(e.Poster);
        if (e.Inline != null) _retired.Add(e.Inline);
    }

    // Drain LAST frame's retirees, so the driver has consumed anything that referenced their SRVs before we free them.
    private static void DrainRetired()
    {
        if (_retired.Count == 0) return;
        for (int i = 0; i < _retired.Count; i++)
        {
            try { _retired[i].Dispose(); }
            catch (Exception ex) { Plugin.PluginLog?.Debug("SocialVideoThumbCache retire: " + ex.Message); }
        }
        _retired.Clear();
    }
}
