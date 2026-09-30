using AbsoluteRP.Social;
using Networking;
using System;
using System.Collections.Generic;
using System.Text;
using AbsoluteRP;
using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI.Pages;
using AbsoluteRP.Windows.Moderator;
using AbsoluteRP.Windows.Profiles;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Social.Views;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using JetBrains.Annotations;
using System.Data;
using System.Diagnostics;
using System.Numerics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Lumina.Data.Parsing.Layer.LayerCommon;

namespace AbsoluteRP.Network
{
    internal class SocialFeed_DS
    {
        // commentId > 0 reports a single reply on the post instead of the post itself.
        public static async Task ReportPost(int postId, string reason, string details, int commentId = 0)
        {
            if (!ClientTCP.IsConnected() || postId <= 0) return;
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.CReportSocialPost);
                buffer.WriteString(Plugin.plugin.Configuration.account.accountKey);
                buffer.WriteInt(postId);
                buffer.WriteString(reason ?? string.Empty);
                buffer.WriteString(details ?? string.Empty);
                buffer.WriteInt(commentId);   // trailing, older servers ignore it
                await ClientTCP.SendDataAsync(buffer.ToArray());
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.ReportPost: " + ex.Message); }
        }



        // filterPath: "" = anywhere, else "NA" / "NA/Crystal" / "NA/Crystal/Balmung".
        public static async Task Fetch(SocialCategory cat, string viewerDC, string viewerWorld,
                                        int offset = 0, int limit = 50, string filterPath = "")
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.CFetchSocialFeed);
                buffer.WriteString(Plugin.plugin.Configuration.account.accountKey);
                buffer.WriteByte((byte)cat);
                buffer.WriteString(viewerDC ?? string.Empty);
                buffer.WriteString(viewerWorld ?? string.Empty);
                buffer.WriteInt(offset);
                buffer.WriteInt(limit);
                buffer.WriteString(filterPath ?? string.Empty);   // trailing place filter
                await ClientTCP.SendDataAsync(buffer.ToArray());
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.Fetch: " + ex.Message); }
        }

        public static async Task ToggleFollow(int targetUserID, bool follow)
        {
            if (!ClientTCP.IsConnected() || targetUserID <= 0) return;
            // Optimistic update - the SSocialFollowChanged ack will confirm.
            if (follow) SocialFeed.FollowedUserIds.Add(targetUserID);
            else SocialFeed.FollowedUserIds.Remove(targetUserID);
            SocialFeed.Version++;
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.CToggleSocialFollow);
                buffer.WriteString(Plugin.plugin.Configuration.account.accountKey);
                buffer.WriteInt(targetUserID);
                buffer.WriteBool(follow);
                await ClientTCP.SendDataAsync(buffer.ToArray());
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.ToggleFollow: " + ex.Message); }
        }

        public static async Task ToggleBookmark(int postId, bool bookmark)
        {
            if (!ClientTCP.IsConnected() || postId <= 0) return;
            if (bookmark) SocialFeed.BookmarkedPostIds.Add(postId);
            else SocialFeed.BookmarkedPostIds.Remove(postId);
            SocialFeed.Version++;
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.CToggleSocialBookmark);
                buffer.WriteString(Plugin.plugin.Configuration.account.accountKey);
                buffer.WriteInt(postId);
                buffer.WriteBool(bookmark);
                await ClientTCP.SendDataAsync(buffer.ToArray());
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.ToggleBookmark: " + ex.Message); }
        }

        public static async Task FetchBookmarks(int offset = 0, int limit = 50)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.CFetchSocialBookmarks);
                buffer.WriteString(Plugin.plugin.Configuration.account.accountKey);
                buffer.WriteInt(offset);
                buffer.WriteInt(limit);
                await ClientTCP.SendDataAsync(buffer.ToArray());
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.FetchBookmarks: " + ex.Message); }
        }

        public static async Task FetchFollows()
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.CFetchSocialFollows);
                buffer.WriteString(Plugin.plugin.Configuration.account.accountKey);
                await ClientTCP.SendDataAsync(buffer.ToArray());
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.FetchFollows: " + ex.Message); }
        }

        public static async Task FetchNotifications(bool unseenOnly, int limit = 100)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.CFetchSocialNotifications);
                buffer.WriteString(Plugin.plugin.Configuration.account.accountKey);
                buffer.WriteBool(unseenOnly);
                buffer.WriteInt(limit);
                await ClientTCP.SendDataAsync(buffer.ToArray());
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.FetchNotifications: " + ex.Message); }
        }
        public static async Task SendPost(SocialPost post)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.CSendSocialPost);
                buffer.WriteString(Plugin.plugin.Configuration.account.accountKey);
                buffer.WriteInt(post.Id);
                buffer.WriteByte((byte)post.Category);
                buffer.WriteByte((byte)post.ScopeType);
                buffer.WriteString(post.ScopeValue ?? string.Empty);
                buffer.WriteString(post.Title ?? string.Empty);
                buffer.WriteString(post.Body ?? string.Empty);
                buffer.WriteLong(post.ExpiresAt);
                await ClientTCP.SendDataAsync(buffer.ToArray());
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.SendPost: " + ex.Message); }
        }

        public static async Task DeletePost(int postId)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.CDeleteSocialPost);
                buffer.WriteString(Plugin.plugin.Configuration.account.accountKey);
                buffer.WriteInt(postId);
                await ClientTCP.SendDataAsync(buffer.ToArray());
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SocialFeed.DeletePost: " + ex.Message); }
        }

        // Asks the server for the posts authored by a specific user, for the profile popup. Response arrives as ServerPackets.SendPostsByAuthor.
        public static async void FetchPostsByAuthor(int authorUserId, int limit = 50, int offset = 0)
        {
            if (!ClientTCP.IsConnected() || authorUserId <= 0) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.FetchPostsByAuthor);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteInt(authorUserId);
                    buffer.WriteInt(limit);
                    buffer.WriteInt(offset);
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("FetchPostsByAuthor error: " + ex.Message); }
        }
    }
}
