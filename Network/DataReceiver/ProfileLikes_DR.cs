using AbsoluteRP;
using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI.Pages;
using AbsoluteRP.Windows.Ect;
using AbsoluteRP.Windows.Listings;
using AbsoluteRP.Windows;
using AbsoluteRP.Windows.Moderator;
using AbsoluteRP.Windows.Profiles;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using AbsoluteRP.Windows.Social.Views;
using AbsoluteRP.Windows.Social.Views.Groups;
using AbsoluteRP.Windows.Social.Views.SubViews;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Common.Math;
using Serilog;
using System.Linq;
using System.Xml.Linq;
using Networking;

namespace AbsoluteRP.Network
{
    // Profile like packets. Split out of DataReceiver.
    internal class ProfileLikes_DR
    {
        // Profile Likes state
        public static int likesRemaining = 0;

        public static string likeResultMessage = string.Empty;
        public static bool likeResultSuccess = false;
        public static Dictionary<int, int> profileLikeCounts = new Dictionary<int, int>();
        public static List<ProfileLike> currentProfileLikes = new List<ProfileLike>();

        // Whether the local account has liked a given profile (profile id -> liked). Filled by a likes fetch the profile HUD asks for (viewerCheckProfileId), and set straight away when a like the viewer sends succeeds.
        public static Dictionary<int, bool> viewerLiked = new Dictionary<int, bool>();
        public static int viewerCheckProfileId = 0;
        public static int lastLikeTargetId = 0;

        public static void HandleLikesRemainingPacket(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID
                likesRemaining = buffer.ReadInt();
                Plugin.PluginLog.Debug($"Likes remaining: {likesRemaining}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleLikesRemainingPacket error: {ex.Message}");
            }
        }

        public static void HandleLikeResultPacket(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID
                likeResultSuccess = buffer.ReadBool();
                likeResultMessage = buffer.ReadString();
                if (likeResultSuccess && lastLikeTargetId > 0) viewerLiked[lastLikeTargetId] = true;
                Plugin.PluginLog.Debug($"Like result: {likeResultSuccess} - {likeResultMessage}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleLikeResultPacket error: {ex.Message}");
            }
        }

        public static void HandleProfileLikeCountsPacket(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID
                profileLikeCounts.Clear();
                int count = buffer.ReadInt();
                for (int i = 0; i < count; i++)
                {
                    int profileID = buffer.ReadInt();
                    int likeCount = buffer.ReadInt();
                    profileLikeCounts[profileID] = likeCount;
                }
                Plugin.PluginLog.Debug($"Received like counts for {count} profiles");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleProfileLikeCountsPacket error: {ex.Message}");
            }
        }

        public static void HandleProfileLikesPacket(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID
                int profileID = buffer.ReadInt();
                int count = buffer.ReadInt();
                // A fetch made only to learn whether the viewer liked this profile must not replace the list the likes window shows.
                bool viewerCheck = profileID > 0 && profileID == viewerCheckProfileId;
                if (viewerCheck) viewerCheckProfileId = 0;
                var target = viewerCheck ? new List<ProfileLike>() : currentProfileLikes;
                target.Clear();

                for (int i = 0; i < count; i++)
                {
                    ProfileLike like = new ProfileLike
                    {
                        likerUserID = buffer.ReadInt(),
                        likerProfileID = buffer.ReadInt(),
                        likerName = buffer.ReadString(),
                        comment = buffer.ReadString(),
                        likeCount = buffer.ReadInt(),
                        likedAt = buffer.ReadLong()
                    };

                    if (like.likerProfileID == -1)
                        like.likerProfileID = 0;

                    target.Add(like);
                }

                if (profileID > 0) viewerLiked[profileID] = target.Exists(l => l.likerUserID == Accounts_DS.userID);
                Plugin.PluginLog.Debug($"Received {count} likes for profile {profileID}");
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleProfileLikesPacket error: {ex.Message}");
            }
        }
    }
}
