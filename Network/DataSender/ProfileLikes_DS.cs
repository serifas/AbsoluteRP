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
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Numerics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Lumina.Data.Parsing.Layer.LayerCommon;
using Networking;

namespace AbsoluteRP.Network
{
    // Profile like requests. Split out of DataSender.
    internal class ProfileLikes_DS
    {
        public static async void LikeProfile(Character character, int profileID, string comment, int likeCount)
        {
            ProfileLikes_DR.lastLikeTargetId = profileID;
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.LikeProfile);
                buffer.WriteString(character.characterName);
                buffer.WriteString(character.characterWorld);
                buffer.WriteInt(profileID);
                buffer.WriteString(comment ?? string.Empty);
                buffer.WriteInt(likeCount);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"LikeProfile error: {ex.Message}");
            }
        }

        public static async void FetchLikesRemaining(Character character)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.FetchLikesRemaining);
                buffer.WriteString(character.characterName);
                buffer.WriteString(character.characterWorld);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"FetchLikesRemaining error: {ex.Message}");
            }
        }

        public static async void FetchProfileLikeCounts(Character character)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.FetchProfileLikeCounts);
                buffer.WriteString(character.characterName);
                buffer.WriteString(character.characterWorld);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"FetchProfileLikeCounts error: {ex.Message}");
            }
        }

        public static async void FetchProfileLikes(int profileID)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.FetchProfileLikes);
                buffer.WriteInt(profileID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"FetchProfileLikes error: {ex.Message}");
            }
        }
    }
}
