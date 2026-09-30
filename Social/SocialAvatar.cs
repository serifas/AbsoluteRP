using System;
using System.Collections.Generic;
using System.Numerics;
using AbsoluteRP.Network;
using AbsoluteRP.RsUI;
using Dalamud.Bindings.ImGui;

namespace AbsoluteRP.Social;

// Shared helpers for rendering circular avatars and lazily fetching other users' account profiles from the server. Both the composer, the account panel, and the social feed cards share this so avatars look identical everywhere and we don't spam duplicate profile requests.
internal static class SocialAvatar
{
    // Throttle map - key = userId, value = tick when we last asked the server for that profile. Prevents the feed from re-issuing the same request every frame while it waits for the server response.
    private static readonly Dictionary<int, long> _lastRequest = new();
    private const int RequestCooldownMs = 5000;

    // Ensures the given user's account profile is in DataReceiver's cache - if not, fires a RequestAccountProfile packet at most once per RequestCooldownMs. Safe to call every frame per post.
    public static AbsoluteRP.Defines.AccountProfile? EnsureProfile(int userId)
    {
        if (userId <= 0) return null;
        if (Accounts_DR.AccountProfileCache.TryGetValue(userId, out var cached))
            return cached;
        var now = Environment.TickCount64;
        if (_lastRequest.TryGetValue(userId, out var last) && now - last < RequestCooldownMs)
            return null;
        _lastRequest[userId] = now;
        try { AbsoluteRP.Network.Accounts_DS.RequestAccountProfile(userId); }
        catch (Exception ex) { Plugin.PluginLog?.Debug("EnsureProfile request: " + ex.Message); }
        return null;
    }

    // Draws a circular avatar of the given diameter at the ImGui cursor position and advances the cursor past it (like a normal widget). Uses ImDrawList.AddImageRounded with radius = diameter/2 for a real circular mask (same technique as Helpers.Anim.DrawCircleAvatarAt used on the profile page). `bgMaskColor` is kept for API compatibility but no longer needed with the real mask.
    public static void DrawCircle(string? avatarUrl, float diameter, uint bgMaskColor = 0u)
    {
        var dl     = ImGui.GetWindowDrawList();
        var min    = ImGui.GetCursorScreenPos();
        var max    = min + new Vector2(diameter, diameter);
        var center = (min + max) * 0.5f;
        var radius = diameter * 0.5f;

        // Backdrop so a missing avatar still reads as an intentional slot.
        dl.AddCircleFilled(center, radius,
            ImGui.ColorConvertFloat4ToU32(RsTheme.BgSecondary), 48);

        var tex = string.IsNullOrWhiteSpace(avatarUrl)
            ? null
            : SocialMediaCache.Get(avatarUrl);
        if (tex != null && tex.Width > 0)
        {
            var tint = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f));
            dl.AddImageRounded(tex.Handle, min, max,
                               new Vector2(0f, 0f), new Vector2(1f, 1f),
                               tint, radius);
        }
        // Border ring on top so the avatar reads as a discrete element.
        dl.AddCircle(center, radius,
            ImGui.ColorConvertFloat4ToU32(RsTheme.Border), 48, 1.5f);

        ImGui.Dummy(new Vector2(diameter, diameter));
    }
}
