using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AbsoluteRP.Defines;
using Networking;

namespace AbsoluteRP.Social;

public enum SocialCategory : byte
{
    Event       = 0,
    Venue       = 1,
    Recruitment = 2,
    LFRP        = 3,
    All         = 255,
}

public enum SocialScope : byte
{
    All    = 0,
    DC     = 1,
    World  = 2,
    Region = 3,   // scope_value = short region code: NA / EU / JP / OCE
}

public sealed class SocialPost
{
    public int    Id;
    public int    AuthorUserID;
    public string AuthorName = string.Empty;
    public SocialCategory Category;
    public SocialScope    ScopeType;
    public string ScopeValue = string.Empty;
    public string Title      = string.Empty;
    public string Body       = string.Empty;
    public long   CreatedAt;
    public long   UpdatedAt;
    public long   ExpiresAt;   // 0 = no expiry
    // Counters + viewer flags, filled by the server per fetch.
    public int    LikeCount;
    public int    RepostCount;
    public int    CommentCount;
    public bool   ViewerLiked;
    public bool   ViewerReposted;
    // Set when this feed entry surfaced because someone reposted it (newest reposter).
    public int    RepostedByUserID;
    public string RepostedByName = string.Empty;
    public long   RepostedAt;
    // Feed ordering time: the repost time for a repost entry, else the post's own time.
    public long   ActivityAt => RepostedAt > CreatedAt ? RepostedAt : CreatedAt;
}

public sealed class SocialFollow
{
    public int    TargetUserID;
    public string TargetName = string.Empty;
}

public sealed class SocialNotification
{
    public int    Id;
    public int    SourceUserID;
    public string SourceName = string.Empty;
    public int    PostID;
    public string PostTitle = string.Empty;
    public SocialCategory PostCategory;
    public long   CreatedAt;
    public long   SeenAt;   // 0 = unseen
}

public sealed class SocialComment
{
    public int    Id;
    public int    PostID;
    public int    AuthorUserID;
    public string AuthorName = string.Empty;
    public string Body       = string.Empty;
    public long   CreatedAt;
    public long   UpdatedAt;
}

// In-memory cache + wire glue for the social feed. State lives here so the UI can just read Posts and respond to Version/LastLoadedAt bumps.
public static class SocialFeed
{
    // Replies this viewer already reported (so the flag greys out).
    public static readonly HashSet<int> ReportedCommentIds = new();
    // Keyed by (category, scopeType, scopeValue) so a viewer flipping between tabs / scopes doesn't have to keep refetching.
    public static readonly Dictionary<string, List<SocialPost>> _byBucket = new();
    public static readonly Dictionary<string, long> _lastFetchedMs = new();

    // Bookmark + follow state - HashSets for O(1) UI checks.
    public  static readonly HashSet<int>  BookmarkedPostIds = new();
    public  static readonly HashSet<int>  FollowedUserIds   = new();
    public  static readonly List<SocialPost> Bookmarks      = new();
    public  static readonly List<SocialFollow> Follows      = new();
    public  static readonly List<SocialNotification> Notifications = new();

    // Cache of posts authored by a specific user, keyed by userId. Filled by SocialFeed_DR.HandleSendPostsByAuthor and consumed by the user profile popup.
    public  static readonly Dictionary<int, List<SocialPost>> AuthorPostsCache = new();
    // Fires when the AuthorPostsCache entry for a user changes so the popup can rebuild without polling every frame.
    public  static event Action<int>? AuthorPostsUpdated;

    // Cross-window signal - the profile popup fires this when the user clicks a post row inside it. SocialPage picks it up on its next. Draw and opens the detail view. Kept as a one-shot pending value so late subscribers don't miss the click.
    public static SocialPost? PendingOpenPost { get; set; }
    // Internal helper so DataReceiver can raise the event without exposing the delegate field directly.
    internal static void RaiseAuthorPostsUpdated(int userId)
    {
        try { AuthorPostsUpdated?.Invoke(userId); }
        catch (Exception ex) { Plugin.PluginLog?.Debug("AuthorPostsUpdated subscriber threw: " + ex.Message); }
    }
    // Public wrapper for the private ReadPost so external handlers can decode a wire post without duplicating the field layout.
    internal static SocialPost ReadPostFromBuffer(ByteBuffer buffer) => ReadPost(buffer);

    // Comments keyed by post id - ordered oldest->newest as returned by the server. Full replacement on fetch, insert-at-end for pushes.
    public static readonly Dictionary<int, List<SocialComment>> _commentsByPost = new();

    public static List<SocialComment> GetCommentsFor(int postId)
        => _commentsByPost.TryGetValue(postId, out var list) ? list : new List<SocialComment>();

    // Bump whenever any cached bucket changes so the UI can invalidate hover/animation state without deep-comparing lists.
    public static long Version { get; set; }

    public static int UnseenNotificationCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < Notifications.Count; i++)
                if (Notifications[i].SeenAt == 0) n++;
            return n;
        }
    }

    public static string BucketKey(SocialCategory cat, SocialScope scope, string scopeValue)
        => $"{(int)cat}|{(int)scope}|{scopeValue ?? string.Empty}";

    public static List<SocialPost> GetCached(SocialCategory cat, SocialScope scope, string scopeValue)
    {
        var key = BucketKey(cat, scope, scopeValue);
        return _byBucket.TryGetValue(key, out var list) ? list : new List<SocialPost>();
    }

    public static long LastFetchedMs(SocialCategory cat, SocialScope scope, string scopeValue)
    {
        var key = BucketKey(cat, scope, scopeValue);
        return _lastFetchedMs.TryGetValue(key, out var ms) ? ms : 0L;
    }

    // senders

    

    // repost feedback: server error text (e.g. own post) + a one-shot "refetch the feed" flag after a successful toggle
    public static string RepostError = string.Empty;
    public static long   RepostErrorAt;
    public static bool   FeedRefreshRequested;

    // reports
    public static readonly HashSet<int> ReportedPostIds = new();
    public static string ReportMessage = string.Empty;
    public static bool   ReportOk;
    public static long   ReportMessageAt;

   

    public static async Task MarkNotificationsSeen(IEnumerable<int> ids)
    {
        if (!ClientTCP.IsConnected()) return;
        var list = ids?.ToList() ?? new List<int>();
        if (list.Count == 0) return;
        // Optimistic local mark so the badge drops right away.
        var now = NowMs();
        foreach (var n in Notifications)
            if (list.Contains(n.Id) && n.SeenAt == 0) n.SeenAt = now;
        Version++;
        try
        {
            using var buffer = new ByteBuffer();
            buffer.WriteInt((int)ClientPackets.CMarkSocialNotificationsSeen);
            buffer.WriteString(Plugin.plugin.Configuration.account.accountKey);
            buffer.WriteInt(list.Count);
            foreach (var id in list) buffer.WriteInt(id);
            await ClientTCP.SendDataAsync(buffer.ToArray());
        }
        catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.MarkNotificationsSeen: " + ex.Message); }
    }

    // receivers

    

    // comments

    public static async Task FetchComments(int postId, int offset = 0, int limit = 100)
    {
        if (!ClientTCP.IsConnected() || postId <= 0) return;
        try
        {
            using var buffer = new ByteBuffer();
            buffer.WriteInt((int)ClientPackets.CFetchSocialComments);
            buffer.WriteString(Plugin.plugin.Configuration.account.accountKey);
            buffer.WriteInt(postId);
            buffer.WriteInt(offset);
            buffer.WriteInt(limit);
            await ClientTCP.SendDataAsync(buffer.ToArray());
        }
        catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.FetchComments: " + ex.Message); }
    }

    public static async Task SendComment(SocialComment c)
    {
        if (!ClientTCP.IsConnected() || string.IsNullOrWhiteSpace(c.Body)) return;
        try
        {
            using var buffer = new ByteBuffer();
            buffer.WriteInt((int)ClientPackets.CSendSocialComment);
            buffer.WriteString(Plugin.plugin.Configuration.account.accountKey);
            buffer.WriteInt(c.Id);
            buffer.WriteInt(c.PostID);
            buffer.WriteString(c.Body);
            await ClientTCP.SendDataAsync(buffer.ToArray());
        }
        catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.SendComment: " + ex.Message); }
    }

    public static async Task DeleteComment(int commentId)
    {
        if (!ClientTCP.IsConnected() || commentId <= 0) return;
        try
        {
            using var buffer = new ByteBuffer();
            buffer.WriteInt((int)ClientPackets.CDeleteSocialComment);
            buffer.WriteString(Plugin.plugin.Configuration.account.accountKey);
            buffer.WriteInt(commentId);
            await ClientTCP.SendDataAsync(buffer.ToArray());
        }
        catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.DeleteComment: " + ex.Message); }
    }

   

    

    public static SocialComment ReadComment(ByteBuffer buffer)
        => new SocialComment
        {
            Id           = buffer.ReadInt(),
            PostID       = buffer.ReadInt(),
            AuthorUserID = buffer.ReadInt(),
            AuthorName   = buffer.ReadString(),
            Body         = buffer.ReadString(),
            CreatedAt    = buffer.ReadLong(),
            UpdatedAt    = buffer.ReadLong(),
        };

    // likes + reposts

    public static async Task ToggleLike(int postId, bool like)
    {
        if (!ClientTCP.IsConnected() || postId <= 0) return;
        // Optimistic - the ack packet corrects the count when it arrives.
        MutatePost(postId, p =>
        {
            if (like && !p.ViewerLiked)      { p.ViewerLiked = true;  p.LikeCount++; }
            else if (!like && p.ViewerLiked) { p.ViewerLiked = false; p.LikeCount = Math.Max(0, p.LikeCount - 1); }
        });
        Version++;
        try
        {
            using var buffer = new ByteBuffer();
            buffer.WriteInt((int)ClientPackets.CToggleSocialLike);
            buffer.WriteString(Plugin.plugin.Configuration.account.accountKey);
            buffer.WriteInt(postId);
            buffer.WriteBool(like);
            await ClientTCP.SendDataAsync(buffer.ToArray());
        }
        catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.ToggleLike: " + ex.Message); }
    }

    public static async Task ToggleRepost(int postId, bool repost)
    {
        if (!ClientTCP.IsConnected() || postId <= 0) return;
        MutatePost(postId, p =>
        {
            if (repost && !p.ViewerReposted)      { p.ViewerReposted = true;  p.RepostCount++; }
            else if (!repost && p.ViewerReposted) { p.ViewerReposted = false; p.RepostCount = Math.Max(0, p.RepostCount - 1); }
        });
        Version++;
        try
        {
            using var buffer = new ByteBuffer();
            buffer.WriteInt((int)ClientPackets.CToggleSocialRepost);
            buffer.WriteString(Plugin.plugin.Configuration.account.accountKey);
            buffer.WriteInt(postId);
            buffer.WriteBool(repost);
            await ClientTCP.SendDataAsync(buffer.ToArray());
        }
        catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.ToggleRepost: " + ex.Message); }
    }

    

    

    // media upload

    // 0 = image, 1 = video. Matches server-side kind byte.
    public enum MediaKind : byte { Image = 0, Video = 1, Audio = 2 }

    // One-at-a-time upload gate. The caller sets a callback before sending; the receiver invokes it with the server's URL (empty on failure) + error string. Composer wiring uses this to insert the returned URL into the post body when the upload lands.
    public static Action<MediaKind, string, string>? PendingUploadCallback;

    public static async Task UploadMedia(MediaKind kind, string extension, byte[] payload)
    {
        if (!ClientTCP.IsConnected()) return;
        if (payload == null || payload.Length == 0) return;
        try
        {
            using var buffer = new ByteBuffer();
            buffer.WriteInt((int)ClientPackets.CUploadSocialMedia);
            buffer.WriteString(Plugin.plugin.Configuration.account.accountKey);
            buffer.WriteByte((byte)kind);
            buffer.WriteString((extension ?? string.Empty).TrimStart('.').ToLowerInvariant());
            buffer.WriteInt(payload.Length);
            buffer.WriteBytes(payload);
            await ClientTCP.SendDataAsync(buffer.ToArray());
        }
        catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.UploadMedia: " + ex.Message); }
    }

    

    // Apply `mutate` to every cached copy of a post - feed buckets and bookmarks. The same post can live in multiple lists, so the ack needs to update every reference to keep the UI consistent.
    public static void MutatePost(int postId, Action<SocialPost> mutate)
    {
        foreach (var list in _byBucket.Values)
            for (int i = 0; i < list.Count; i++)
                if (list[i].Id == postId) mutate(list[i]);
        for (int i = 0; i < Bookmarks.Count; i++)
            if (Bookmarks[i].Id == postId) mutate(Bookmarks[i]);
    }

    // plumbing

    public static SocialPost ReadPost(ByteBuffer buffer)
        => new SocialPost
        {
            Id             = buffer.ReadInt(),
            AuthorUserID   = buffer.ReadInt(),
            AuthorName     = buffer.ReadString(),
            Category       = (SocialCategory)buffer.ReadByte(),
            ScopeType      = (SocialScope)buffer.ReadByte(),
            ScopeValue     = buffer.ReadString(),
            Title          = buffer.ReadString(),
            Body           = buffer.ReadString(),
            CreatedAt      = buffer.ReadLong(),
            UpdatedAt      = buffer.ReadLong(),
            ExpiresAt      = buffer.ReadLong(),
            LikeCount      = buffer.ReadInt(),
            RepostCount    = buffer.ReadInt(),
            CommentCount   = buffer.ReadInt(),
            ViewerLiked    = buffer.ReadByte() == 1,
            ViewerReposted = buffer.ReadByte() == 1,
        };

    public static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
