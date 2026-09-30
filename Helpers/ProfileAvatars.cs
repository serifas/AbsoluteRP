using System;
using System.Collections.Generic;
using Dalamud.Interface.Textures.TextureWraps;

namespace AbsoluteRP.Helpers
{
    // Avatars of other people's profiles, keyed by (account id, profile index). Asked from the server once, kept for the session.
    public static class ProfileAvatars
    {
        private static readonly Dictionary<(int, int), IDalamudTextureWrap?> _cache = new();
        private static readonly Dictionary<(int, int), long> _asked = new();
        private static readonly Dictionary<(int, int), System.Numerics.Vector4> _colours = new();

        // The profile's title / avatar-border colour, once known.
        public static System.Numerics.Vector4? ColourOf(int accountId, int profileIndex)
        {
            Get(accountId, profileIndex);
            return _colours.TryGetValue((accountId, profileIndex), out var c) ? c : null;
        }
        private const int RetryMs = 30000;

        // The texture when known; null while it is being fetched (or if the profile has none).
        public static IDalamudTextureWrap? Get(int accountId, int profileIndex)
        {
            if (accountId <= 0) return null;
            var key = (accountId, profileIndex);
            lock (_cache) { if (_cache.TryGetValue(key, out var tex)) return tex; }
            var now = Environment.TickCount64;
            if (_asked.TryGetValue(key, out var last) && now - last < RetryMs) return null;
            _asked[key] = now;
            try { AbsoluteRP.Network.Relationships_DS.RequestProfileAvatar(accountId, profileIndex); } catch { }
            return null;
        }

        public static void Received(int accountId, int profileIndex, byte[] bytes, System.Numerics.Vector4? colour = null)
        {
            var key = (accountId, profileIndex);
            if (colour.HasValue) _colours[key] = colour.Value;
            if (bytes == null || bytes.Length == 0) { Publish(key, null); return; }
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var scaled = Imaging.ScaleImageBytes(bytes, 160, 160);
                    var tex = await Plugin.TextureProvider.CreateFromImageAsync(scaled);
                    Publish(key, tex);
                }
                catch (Exception ex) { Plugin.PluginLog.Debug("ProfileAvatars decode: " + ex.Message); Publish(key, null); }
            });
        }

        // Called from a thread-pool thread: swap under the lock, and retire the previous avatar through the graveyard - it may be in this frame's draw list (a re-received avatar used to leak, and the unlocked Dictionary write raced the draw thread's reads).
        private static void Publish((int, int) key, IDalamudTextureWrap? tex)
        {
            IDalamudTextureWrap? old;
            lock (_cache)
            {
                _cache.TryGetValue(key, out old);
                _cache[key] = tex;
            }
            if (old != null && !ReferenceEquals(old, tex)) TextureGraveyard.Enqueue(old);
        }
    }
}
