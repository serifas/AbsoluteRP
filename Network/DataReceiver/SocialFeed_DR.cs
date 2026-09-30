using AbsoluteRP.Social;
using AbsoluteRP.Windows.Social.Views;
using Networking;
using System;
using System.Collections.Generic;
using System.Text;
using static AbsoluteRP.Social.SocialFeed;

namespace AbsoluteRP.Network
{
    internal class SocialFeed_DR
    {
        public static void HandleSendSocialFeed(byte[] data)
        {
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                _ = buffer.ReadInt();                          // packetID
                var cat = (SocialCategory)buffer.ReadByte();
                var offset = buffer.ReadInt();
                var count = buffer.ReadInt();

                var list = new List<SocialPost>(count);
                for (int i = 0; i < count; i++) list.Add(SocialFeed.ReadPost(buffer));

                // Phase 1 replaces the whole bucket on fetch. Pagination (offset > 0) appends instead so infinite-scroll works when the UI wires it up.
                var key = SocialFeed.BucketKey(cat, SocialScope.All, string.Empty);
                if (offset == 0 || !SocialFeed._byBucket.ContainsKey(key))
                    SocialFeed._byBucket[key] = list;
                else
                    SocialFeed._byBucket[key].AddRange(list);
                SocialFeed._lastFetchedMs[key] = SocialFeed.NowMs();
                SocialFeed.Version++;
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.HandleSendSocialFeed: " + ex.Message); }
        }

        public static void HandleSocialPostSaved(byte[] data)
        {
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                _ = buffer.ReadInt(); // packetID
                var post = SocialFeed.ReadPost(buffer);
                bool isCreate = buffer.ReadByte() == 1;

                // Splice into the "all-scope" bucket for the post's category so the composer's result shows without a full refetch.
                var key = SocialFeed.BucketKey(post.Category, SocialScope.All, string.Empty);
                if (!SocialFeed._byBucket.TryGetValue(key, out var list))
                {
                    list = new List<SocialPost>();
                    SocialFeed._byBucket[key] = list;
                }
                if (isCreate)
                {
                    list.Insert(0, post);
                }
                else
                {
                    var idx = list.FindIndex(p => p.Id == post.Id);
                    if (idx >= 0) list[idx] = post;
                    else list.Insert(0, post);
                }
                SocialFeed.Version++;
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.HandleSocialPostSaved: " + ex.Message); }
        }

        public static void HandleSocialPostDeleted(byte[] data)
        {
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                _ = buffer.ReadInt(); // packetID
                var id = buffer.ReadInt();
                foreach (var list in SocialFeed._byBucket.Values)
                    list.RemoveAll(p => p.Id == id);
                SocialFeed.Bookmarks.RemoveAll(p => p.Id == id);
                SocialFeed.BookmarkedPostIds.Remove(id);
                SocialFeed.Notifications.RemoveAll(n => n.PostID == id);
                SocialFeed.Version++;
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.HandleSocialPostDeleted: " + ex.Message); }
        }

        public static void HandleSendSocialBookmarks(byte[] data)
        {
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                _ = buffer.ReadInt();          // packetID
                var offset = buffer.ReadInt();
                var count = buffer.ReadInt();
                if (offset == 0) { SocialFeed.Bookmarks.Clear(); SocialFeed.BookmarkedPostIds.Clear(); }
                for (int i = 0; i < count; i++)
                {
                    var p = SocialFeed.ReadPost(buffer);
                    SocialFeed.Bookmarks.Add(p);
                    SocialFeed.BookmarkedPostIds.Add(p.Id);
                }
                SocialFeed.Version++;
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.HandleSendSocialBookmarks: " + ex.Message); }
        }

        public static void HandleSendSocialFollows(byte[] data)
        {
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                _ = buffer.ReadInt();
                var count = buffer.ReadInt();
                SocialFeed.Follows.Clear();
                SocialFeed.FollowedUserIds.Clear();
                for (int i = 0; i < count; i++)
                {
                    var f = new SocialFollow
                    {
                        TargetUserID = buffer.ReadInt(),
                        TargetName = buffer.ReadString(),
                    };
                    SocialFeed.Follows.Add(f);
                    SocialFeed.FollowedUserIds.Add(f.TargetUserID);
                }
                SocialFeed.Version++;
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.HandleSendSocialFollows: " + ex.Message); }
        }

        public static void HandleSocialFollowChanged(byte[] data)
        {
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                _ = buffer.ReadInt();
                var target = buffer.ReadInt();
                var isFollowing = buffer.ReadBool();
                if (isFollowing) SocialFeed.FollowedUserIds.Add(target);
                else SocialFeed.FollowedUserIds.Remove(target);
                if (!isFollowing) SocialFeed.Follows.RemoveAll(f => f.TargetUserID == target);
                SocialFeed.Version++;
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.HandleSocialFollowChanged: " + ex.Message); }
        }

        public static void HandleSocialBookmarkChanged(byte[] data)
        {
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                _ = buffer.ReadInt();
                var postId = buffer.ReadInt();
                var isBookmarked = buffer.ReadBool();
                if (isBookmarked) SocialFeed.BookmarkedPostIds.Add(postId);
                else { SocialFeed.BookmarkedPostIds.Remove(postId); SocialFeed.Bookmarks.RemoveAll(p => p.Id == postId); }
                SocialFeed.Version++;
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.HandleSocialBookmarkChanged: " + ex.Message); }
        }

        public static void HandleSendSocialNotifications(byte[] data)
        {
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                _ = buffer.ReadInt();
                var count = buffer.ReadInt();
                SocialFeed.Notifications.Clear();
                for (int i = 0; i < count; i++) SocialFeed.Notifications.Add(ReadNotification(buffer));
                SocialFeed.Version++;
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.HandleSendSocialNotifications: " + ex.Message); }
        }

        public static void HandleSocialNotificationPush(byte[] data)
        {
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                _ = buffer.ReadInt();
                var n = ReadNotification(buffer);
                // Prepend so the newest sits at the top of the popup.
                SocialFeed.Notifications.Insert(0, n);
                SocialFeed.Version++;
                // Fire a chat toast so the user notices even without opening the popup. The DTR bell badge will also tick up on its own.
                try
                {
                    var msg = $"[ARP] {n.SourceName} posted \"{n.PostTitle}\"";
                    Plugin.Chat?.Print(msg);
                }
                catch { /* chat print failing is non-fatal */ }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.HandleSocialNotificationPush: " + ex.Message); }
        }

        private static SocialNotification ReadNotification(ByteBuffer buffer)
            => new SocialNotification
            {
                Id = buffer.ReadInt(),
                SourceUserID = buffer.ReadInt(),
                SourceName = buffer.ReadString(),
                PostID = buffer.ReadInt(),
                PostTitle = buffer.ReadString(),
                PostCategory = (SocialCategory)buffer.ReadByte(),
                CreatedAt = buffer.ReadLong(),
                SeenAt = buffer.ReadLong(),
            };
        public static void HandleSendSocialComments(byte[] data)
        {
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                _ = buffer.ReadInt();          // packetID
                var postId = buffer.ReadInt();
                var offset = buffer.ReadInt();
                var count = buffer.ReadInt();
                var list = new List<SocialComment>(count);
                for (int i = 0; i < count; i++) list.Add(SocialFeed.ReadComment(buffer));
                if (offset == 0 || !SocialFeed._commentsByPost.ContainsKey(postId))
                    SocialFeed._commentsByPost[postId] = list;
                else
                    _commentsByPost[postId].AddRange(list);
                SocialFeed.Version++;
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.HandleSendSocialComments: " + ex.Message); }
        }
        public static void HandleSocialCommentSaved(byte[] data)
        {
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                _ = buffer.ReadInt();
                var c = ReadComment(buffer);
                bool isCreate = buffer.ReadByte() == 1;
                if (!_commentsByPost.TryGetValue(c.PostID, out var list))
                {
                    list = new List<SocialComment>();
                    _commentsByPost[c.PostID] = list;
                }
                if (isCreate) list.Add(c);
                else
                {
                    var idx = list.FindIndex(x => x.Id == c.Id);
                    if (idx >= 0) list[idx] = c;
                    else list.Add(c);
                }
                SocialFeed.Version++;
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.HandleSocialCommentSaved: " + ex.Message); }
        }

        public static void HandleSocialCommentDeleted(byte[] data)
        {
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                _ = buffer.ReadInt();
                var postId = buffer.ReadInt();
                var commentId = buffer.ReadInt();
                if (_commentsByPost.TryGetValue(postId, out var list))
                    list.RemoveAll(c => c.Id == commentId);
                SocialFeed.Version++;
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.HandleSocialCommentDeleted: " + ex.Message); }
        }
        public static void HandleSocialLikeChanged(byte[] data)
        {
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                _ = buffer.ReadInt();          // packetID
                var postId = buffer.ReadInt();
                var isLiked = buffer.ReadBool();
                var newCount = buffer.ReadInt();
                SocialFeed.MutatePost(postId, p => { p.ViewerLiked = isLiked; p.LikeCount = newCount; });
                SocialFeed.Version++;
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.HandleSocialLikeChanged: " + ex.Message); }
        }
        public static void HandlePostReported(byte[] data)
        {
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                _ = buffer.ReadInt();
                var postId = buffer.ReadInt();
                var ok = buffer.ReadBool();
                var msg = buffer.ReadString();
                var commentId = buffer.Length() >= 4 ? buffer.ReadInt() : 0;
                if (ok && commentId > 0) SocialFeed.ReportedCommentIds.Add(commentId);
                else if (ok) ReportedPostIds.Add(postId);
                ReportOk = ok; ReportMessage = msg ?? string.Empty; ReportMessageAt = NowMs();
                SocialFeed.Version++;
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.HandlePostReported: " + ex.Message); }
        }
        public static void HandleSocialRepostChanged(byte[] data)
        {
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                _ = buffer.ReadInt();
                var postId = buffer.ReadInt();
                var isReposted = buffer.ReadBool();
                var newCount = buffer.ReadInt();
                MutatePost(postId, p => { p.ViewerReposted = isReposted; p.RepostCount = newCount; });
                SocialFeed.Version++;
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.HandleSocialRepostChanged: " + ex.Message); }
        }
        public static void HandleSocialMediaUploaded(byte[] data)
        {
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                _ = buffer.ReadInt();
                var kind = (MediaKind)buffer.ReadByte();
                var url = buffer.ReadString();
                var error = buffer.ReadString();
                var cb = PendingUploadCallback;
                PendingUploadCallback = null;
                cb?.Invoke(kind, url ?? string.Empty, error ?? string.Empty);
                SocialFeed.Version++;
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.HandleSocialMediaUploaded: " + ex.Message); }
        }
        public static void HandleSendPostsByAuthor(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet ID
                    int authorUserId = buffer.ReadInt();
                    int count = buffer.ReadInt();
                    var list = new List<AbsoluteRP.Social.SocialPost>(count > 0 ? count : 0);
                    for (int i = 0; i < count; i++)
                        list.Add(AbsoluteRP.Social.SocialFeed.ReadPostFromBuffer(buffer));
                    AbsoluteRP.Social.SocialFeed.AuthorPostsCache[authorUserId] = list;
                    AbsoluteRP.Social.SocialFeed.RaiseAuthorPostsUpdated(authorUserId);
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("HandleSendPostsByAuthor Error: " + ex.Message);
            }
        }
    }
}
