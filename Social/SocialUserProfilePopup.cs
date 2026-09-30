using System;
using System.Collections.Generic;
using System.Numerics;
using AbsoluteRP.RsUI;
using Dalamud.Bindings.ImGui;
using AbsoluteRP.Network;

namespace AbsoluteRP.Social;

// User profile popup - opened by clicking a poster's avatar. Shows the header banner with the avatar overlapping the bottom-center, display name, RP-preferences pill row, BBCode-rendered bio, and - driven by packet 322 (FetchPostsByAuthor / SendPostsByAuthor) - a scrollable list of the user's authored posts.
public static class SocialUserProfilePopup
{
    private static int    _userId;
    private static bool   _open;

    // Throttle repeated FetchPostsByAuthor calls per userId, matching. SocialAvatar.EnsureProfile's 5s cooldown so an over-eager UI can't spam the server.
    private static readonly Dictionary<int, long> _lastPostsRequest = new();
    private const int PostsRequestCooldownMs = 5000;

    public static void Open(int userId)
    {
        if (userId <= 0) return;
        _userId = userId;
        _open   = true;
        // Warm the profile cache so the popup has data by first paint.
        SocialAvatar.EnsureProfile(userId);
        // Warm the authored-posts cache in parallel.
        EnsureAuthorPosts(userId);
    }

    // Fires a FetchPostsByAuthor at most once per PostsRequestCooldownMs per userId. Safe to call every frame - the cooldown map prevents any redundant traffic while a request is in flight.
    private static void EnsureAuthorPosts(int userId)
    {
        if (userId <= 0) return;
        var now = Environment.TickCount64;
        if (_lastPostsRequest.TryGetValue(userId, out var last) && now - last < PostsRequestCooldownMs)
            return;
        _lastPostsRequest[userId] = now;
        try { AbsoluteRP.Network.SocialFeed_DS.FetchPostsByAuthor(userId); }
        catch (Exception ex) { Plugin.PluginLog?.Debug("EnsureAuthorPosts request: " + ex.Message); }
    }

    public static bool IsOpen => _open;
    public static void Close() => _open = false;

    // Kept for existing call sites: the profile is no longer a popup.
    public static void Draw() { }

    // The account profile as a page inside the social panel: a back button, then the banner, identity and the author's posts.
    public static void DrawInline()
    {
        if (!_open) return;
        if (RsElements.Button("← Back##user_prof_back", RsElements.ButtonVariant.Ghost))
        {
            _open = false;
            return;
        }
        ImGui.Spacing();
        {
            var profile = SocialAvatar.EnsureProfile(_userId);
            var dl      = ImGui.GetWindowDrawList();
            var availW  = ImGui.GetContentRegionAvail().X;

            // Header banner strip.
            var headerH = MathF.Min(availW * 0.32f, RsTheme.S(220f));
            var hMin    = ImGui.GetCursorScreenPos();
            var hMax    = new Vector2(hMin.X + availW, hMin.Y + headerH);
            dl.AddRectFilled(hMin, hMax,
                ImGui.ColorConvertFloat4ToU32(RsTheme.BgSecondary), RsTheme.S(8f));
            if (!string.IsNullOrWhiteSpace(profile?.headerUrl))
            {
                var tex = SocialMediaCache.Get(profile!.headerUrl);
                if (tex != null && tex.Width > 0)
                {
                    // Aspect-preserving crop via SocialHeaderCrop.ComputeUv so the image keeps its shape regardless of the frame aspect ratio.
                    var (uv0, uv1) = SocialHeaderCrop.ComputeUv(
                        tex.Width, tex.Height, availW, headerH,
                        profile.headerOffsetX, profile.headerOffsetY, profile.headerZoom);
                    dl.AddImageRounded(tex.Handle, hMin, hMax, uv0, uv1,
                        ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f)),
                        RsTheme.S(8f));
                }
            }
            dl.AddRect(hMin, hMax,
                ImGui.ColorConvertFloat4ToU32(RsTheme.Border), RsTheme.S(8f));
            ImGui.Dummy(new Vector2(availW, headerH));

            // Avatar - centered, overlapping the bottom of the banner.
            var avatarSz = RsTheme.S(96f);
            var avatarCenter = new Vector2(hMin.X + availW * 0.5f, hMax.Y);
            var avatarMin    = new Vector2(avatarCenter.X - avatarSz * 0.5f,
                                            avatarCenter.Y - avatarSz * 0.5f);
            // Ring behind the avatar so it visibly overlaps the banner.
            dl.AddCircleFilled(avatarCenter, avatarSz * 0.5f + RsTheme.S(3f),
                ImGui.ColorConvertFloat4ToU32(RsTheme.BgPrimary), 48);
            var savePos = ImGui.GetCursorScreenPos();
            ImGui.SetCursorScreenPos(avatarMin);
            SocialAvatar.DrawCircle(profile?.avatarUrl, avatarSz,
                ImGui.ColorConvertFloat4ToU32(RsTheme.BgPrimary));
            ImGui.SetCursorScreenPos(savePos);
            ImGui.Dummy(new Vector2(availW, avatarSz * 0.5f + RsTheme.S(6f)));

            // Display name (centered).
            var displayName = !string.IsNullOrWhiteSpace(profile?.displayName)
                ? profile!.displayName : "unknown";
            var nameSz = ImGui.CalcTextSize(displayName);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(0f, (availW - nameSz.X) * 0.5f));
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
            ImGui.TextUnformatted(displayName);
            ImGui.PopStyleColor();

            // RP preferences - full pill row.
            if (profile?.rpPreferences != null && profile.rpPreferences.Count > 0)
            {
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                var label = "RP preferences";
                var lblSz = ImGui.CalcTextSize(label);
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(0f, (availW - lblSz.X) * 0.5f));
                ImGui.TextUnformatted(label);
                ImGui.PopStyleColor();
                ImGui.Spacing();
                // Centered flow-row of pills.
                DrawPillRow(profile.rpPreferences, availW);
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Bio - BBCode-formatted.
            if (!string.IsNullOrWhiteSpace(profile?.bio))
            {
                SocialMarkup.Render(profile!.bio);
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextUnformatted("(no bio yet)");
                ImGui.PopStyleColor();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Posts by this author - scrollable inside a fixed child so the popup itself stays a stable size regardless of history.
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
            ImGui.TextUnformatted("Posts");
            ImGui.PopStyleColor();
            DrawAuthorPosts(_userId, availW);

        }
    }


    private static void DrawAuthorPosts(int userId, float availW)
    {
        // Keep re-warming the cache while the popup is open - the throttle in EnsureAuthorPosts stops this from becoming a request storm.
        EnsureAuthorPosts(userId);

        var have = SocialFeed.AuthorPostsCache.TryGetValue(userId, out var posts);
        // Fill what is left of the page.
        var childH = MathF.Max(RsTheme.S(220f), ImGui.GetContentRegionAvail().Y - RsTheme.S(8f));
        ImGui.BeginChild("##arp_user_prof_posts", new Vector2(availW, childH),
            true, ImGuiWindowFlags.HorizontalScrollbar);
        try
        {
            if (!have)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextUnformatted("Loading posts…");
                ImGui.PopStyleColor();
                return;
            }
            if (posts == null || posts.Count == 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextUnformatted("(no posts yet)");
                ImGui.PopStyleColor();
                return;
            }
            var innerW = ImGui.GetContentRegionAvail().X;
            for (int i = 0; i < posts.Count; i++)
            {
                DrawCompactPostCard(posts[i], innerW);
                if (i < posts.Count - 1) ImGui.Spacing();
            }
        }
        finally
        {
            ImGui.EndChild();
        }
    }

    // Compact post card with real formatting - renders title + a clipped. SocialMarkup-formatted preview inside a fixed-height inner region so bbcode / fonts / align all display like the feed. The whole card is a clickable button; clicking sets SocialFeed.PendingOpenPost which SocialPage consumes to open the detail view, and this popup closes itself so the detail becomes the focus.
    private static void DrawCompactPostCard(SocialPost post, float availW)
    {
        var dl        = ImGui.GetWindowDrawList();
        var pad       = new Vector2(RsTheme.S(8f), RsTheme.S(6f));
        var min       = ImGui.GetCursorScreenPos();
        var previewH  = RsTheme.S(90f);

        // Title.
        ImGui.SetCursorScreenPos(new Vector2(min.X + pad.X, min.Y + pad.Y));
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
        ImGui.TextUnformatted(string.IsNullOrWhiteSpace(post.Title) ? "(no title)" : post.Title);
        ImGui.PopStyleColor();

        // Formatted body preview, clipped to previewH via a nested child.
        var previewMin = ImGui.GetCursorScreenPos();
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0f, 0f, 0f, 0f));
        if (ImGui.BeginChild("##compact_body_" + post.Id,
                              new Vector2(availW - pad.X * 2f, previewH),
                              false,
                              ImGuiWindowFlags.NoScrollbar
                            | ImGuiWindowFlags.NoScrollWithMouse
                            | ImGuiWindowFlags.NoInputs))
        {
            if (!string.IsNullOrEmpty(post.Body))
                SocialMarkup.Render(post.Body);
        }
        bool overflowed;
        try { overflowed = ImGui.GetScrollMaxY() > 0f; }
        catch { overflowed = false; }
        ImGui.EndChild();
        ImGui.PopStyleColor();

        if (overflowed)
        {
            // Fade the bottom of the body preview into the popup's bg.
            var fadeH   = MathF.Min(previewH * 0.55f, RsTheme.S(48f));
            var fadeTop = new Vector2(previewMin.X, previewMin.Y + previewH - fadeH);
            var fadeMax = new Vector2(previewMin.X + availW - pad.X * 2f, previewMin.Y + previewH);
            var bg      = RsTheme.BgPrimary;
            var top     = ImGui.ColorConvertFloat4ToU32(new Vector4(bg.X, bg.Y, bg.Z, 0f));
            var bot     = ImGui.ColorConvertFloat4ToU32(new Vector4(bg.X, bg.Y, bg.Z, 0.98f));
            dl.AddRectFilledMultiColor(fadeTop, fadeMax, top, top, bot, bot);
        }

        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        ImGui.TextUnformatted(RelativeTime(post.CreatedAt));
        ImGui.PopStyleColor();

        var endY = ImGui.GetCursorScreenPos().Y + pad.Y;
        var max  = new Vector2(min.X + availW, endY);

        // Whole-card click. Placed FIRST via SetCursorScreenPos back to min so the InvisibleButton covers the whole box; we already marked the nested body child NoInputs so it doesn't eat clicks.
        var cursorAfter = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(min);
        if (ImGui.InvisibleButton("##compact_hit_" + post.Id, new Vector2(availW, endY - min.Y)))
        {
            SocialFeed.PendingOpenPost = post;
            _open = false;   // close the profile popup so the detail is on top
        }
        var hovered = ImGui.IsItemHovered();
        if (hovered)
        {
            var over = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.05f));
            dl.AddRectFilled(min, max, over, RsTheme.S(6f));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
        dl.AddRect(min, max,
            ImGui.ColorConvertFloat4ToU32(hovered ? RsTheme.AccentPrimary : RsTheme.Border),
            RsTheme.S(6f));
        // Restore cursor to the bottom of the card so the next card sits below.
        ImGui.SetCursorScreenPos(new Vector2(min.X, endY));
    }

    private static string RelativeTime(long ms)
    {
        if (ms <= 0) return string.Empty;
        var delta = SocialFeed.NowMs() - ms;
        if (delta < 0) delta = 0;
        long s = delta / 1000;
        if (s < 60)     return s + "s ago";
        long m = s / 60;
        if (m < 60)     return m + "m ago";
        long h = m / 60;
        if (h < 24)     return h + "h ago";
        long d = h / 24;
        if (d < 30)     return d + "d ago";
        long mo = d / 30;
        if (mo < 12)    return mo + "mo ago";
        return (mo / 12) + "y ago";
    }

    private static void DrawPillRow(System.Collections.Generic.List<string> tags, float availW)
    {
        // Two-pass layout: measure each pill, group into rows so each row is centered horizontally, then emit.
        var dl     = ImGui.GetWindowDrawList();
        var pad    = new Vector2(RsTheme.S(8f), RsTheme.S(3f));
        var gap    = RsTheme.S(6f);
        var accent = RsTheme.AccentPrimary;
        int i = 0;
        while (i < tags.Count)
        {
            // Fit as many pills as possible on this row.
            float rowW = 0f;
            int   rowEnd = i;
            while (rowEnd < tags.Count)
            {
                var w = ImGui.CalcTextSize(tags[rowEnd]).X + pad.X * 2f;
                var addW = rowEnd == i ? w : w + gap;
                if (rowW + addW > availW && rowEnd > i) break;
                rowW += addW;
                rowEnd++;
            }
            // Center this row.
            var startX = ImGui.GetCursorPosX() + MathF.Max(0f, (availW - rowW) * 0.5f);
            ImGui.SetCursorPosX(startX);
            for (int k = i; k < rowEnd; k++)
            {
                var lbl = tags[k];
                var sz  = ImGui.CalcTextSize(lbl);
                var min = ImGui.GetCursorScreenPos();
                var max = min + new Vector2(sz.X + pad.X * 2f, sz.Y + pad.Y * 2f);
                var r   = (max.Y - min.Y) * 0.5f;
                dl.AddRectFilled(min, max,
                    ImGui.ColorConvertFloat4ToU32(new Vector4(accent.X, accent.Y, accent.Z, 0.18f)), r);
                dl.AddRect(min, max,
                    ImGui.ColorConvertFloat4ToU32(new Vector4(accent.X, accent.Y, accent.Z, 0.55f)), r);
                dl.AddText(new Vector2(min.X + pad.X, min.Y + pad.Y),
                    ImGui.ColorConvertFloat4ToU32(RsTheme.TextPrimary), lbl);
                ImGui.Dummy(new Vector2(sz.X + pad.X * 2f, sz.Y + pad.Y * 2f));
                if (k + 1 < rowEnd) ImGui.SameLine(0f, gap);
            }
            i = rowEnd;
            ImGui.Spacing();
        }
    }
}
