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
    // Profile fetch/create/status requests. Split out of DataSender.
    internal class Profiles_DS
    {
        public static async void SendProfileAccessUpdate(Character character, string username, string localName, string localServer, string connectionName, string connectionWorld, int status)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SSendProfileAccessUpdate);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteString(connectionName);
                        buffer.WriteString(connectionWorld);
                        buffer.WriteInt(status);
                        Plugin.PluginLog.Debug($"Sending tooltipData access update: {localName} on {localServer} to {connectionName} on {connectionWorld} with status {status}");
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in Login: " + ex.ToString());
                }
            }
        }

        // Resets loading animation tweens before fetching new profile data
        public static void ResetAllData()
        {
            try
            {
                // Reset loader tweens for target tooltipData loading
                Misc.ResetLoaderTween("tabs");
                Misc.ResetLoaderTween("gallery");

            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("TargetProfileWindow ResetAllData Debug: " + ex.Message);
            }
        }

        // Requests a profile from the server. If self=true, fetches the player's own profile for editing; otherwise fetches another player's profile for viewing. Resets loading counters and UI state before sending the request so the loading indicators start fresh.
        public static async void FetchProfile(Character character, bool self, int profileIndex, string targetName, string targetWorld, int profileID, bool acknowledgedWarning = false)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {

                    // Always reset loader tweens and counters before loading
                    ResetAllData();


                    if (!self)
                    {
                        // Reset target tooltipData loading counters
                        Profiles_DR.loadedTargetTabsCount = 0;
                        Profiles_DR.tabsTargetCount = 0;
                        Profiles_DR.loadedTargetGalleryImages = 0;
                        Profiles_DR.TargetGalleryImagesToLoad = 0;
                        Profiles_DR.TargetHasNoProfile = false;
                        TargetProfileWindow.AccessDenied = false;
                        Profiles_DR.targetTabCountReceived = false;


                        // Reset target tooltipData tabs. Only proceed if the target window is in a default state
                        // Starts the cancel / timeout tracking; a skipped send then times out instead of hanging.
                        TargetProfileWindow.BeginFetch();
                        if (!TargetProfileWindow.IsDefault() && !acknowledgedWarning)
                            return;
                        // Remembered so an accepted content warning can re-ask for the same profile.
                        TargetProfileWindow.LastFetch = (profileIndex, targetName ?? string.Empty, targetWorld ?? string.Empty, profileID);
                        // Name the profile being opened, so the loading screen never shows whoever was viewed last (or the viewer's own name). A new profile starts with empty notes; the server sends this viewer's notes for it if there are any.
                        NotesWindow.profileNotes = string.Empty;
                        NotesWindow.characterIndex = profileID > 0 ? profileID : 0;
                        TargetProfileWindow.characterName = targetName ?? string.Empty;
                        TargetProfileWindow.characterWorld = targetWorld ?? string.Empty;
                    }
                    else
                    {
                        // Reset self tooltipData loading counters
                        Profiles_DR.loadedTabsCount = 0;
                        Profiles_DR.tabsCount = 0;
                        Profiles_DR.tabCountReceived = false;
                        Profiles_DR.loadedGalleryImages = 0;
                        Profiles_DR.GalleryImagesToLoad = 0;
                        AbsoluteRP.Windows.Inventory.InventoryWindow.ClearTabs();
                        ProfilesPage.Fetching = true;
                        ProfilesPage.fetchStartedTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                    }

                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CFetchProfile);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteString(targetName);
                        buffer.WriteString(targetWorld);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(profileID);
                        buffer.WriteBool(self); // Indicate if this is a self tooltipData fetch
                        buffer.WriteBool(acknowledgedWarning);   // the viewer agreed to the content warning
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchProfile: " + ex.ToString());
                }
            }
            else if (!self)
            {
                // Offline: nothing will answer, so show the request panel rather than a loader.
                TargetProfileWindow.CancelFetch();
                TargetProfileWindow.AccessDenied = true;
            }
        }

        public static async void FetchProfilesByAccountTag(Character character, string tagName)
        {
            if (!ClientTCP.IsConnected()) return;
            if (string.IsNullOrWhiteSpace(tagName)) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CFetchProfilesByAccountTag);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteString(tagName);
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("Debug in FetchProfilesByAccountTag: " + ex.ToString());
            }
        }

        // Creates a new profile on the server with the given title and type
        public static async void CreateProfile(Character character, string profileTitle, int profileType, int index)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CCreateProfile);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(index);
                        buffer.WriteInt(profileType);
                        buffer.WriteString(profileTitle);

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in CreateProfile: " + ex.ToString());
                }
            }
        }

        // Adds another player's profile to the current user's bookmarks
        public static async void BookmarkPlayer(Character character, string playerName, string playerWorld, int profileID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSendPlayerBookmark);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteString(playerName);
                        buffer.WriteString(playerWorld);
                        buffer.WriteInt(profileID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in BookmarkProfile: " + ex.ToString());
                }
            }

        }

        public static async void RemoveBookmarkedPlayer(Character character, string playerName, int profileID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    Bookmarks.profileList.Clear();
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSendRemovePlayerBookmark);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(profileID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in RemoveBookmarkedPlayer: " + ex.ToString());
                }
            }
        }

        public static async void RequestBookmarks(Character character)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSendBookmarkRequest);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in RequestBookmarks: " + ex.ToString());
                }
            }

        }

        public static async void SaveProfileConfiguration(Character character, bool showProfilePublicly, int profileIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendProfileConfiguration);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteBool(showProfilePublicly);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in sending user configuration: " + ex.ToString());
                }
            }
        }

        public static async void RequestTargetProfile(Character character, int profileID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SRequestTargetProfile);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);

                        buffer.WriteInt(profileID);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                        NotesWindow.characterIndex = profileID;
                        NotesWindow.profileNotes = string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SubmitProfileBio: " + ex.ToString());
                }
            }

        }

        public static async void AddProfileNotes(Character character, int characterIndex, string notes)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {

                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSendProfileNotes);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(characterIndex);
                        buffer.WriteString(notes);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in AddProfileNotes: " + ex.ToString());
                }
            }
        }

        // Saves profile display settings: active status, tooltip visibility, title, accent color, avatar/background images, expansion spoiler flags, and content warnings.
        internal static async Task SetProfileStatus(Character character, bool status, bool tooltipStatus, int profileIndex, string profileTitle, Vector4 color, byte[] avatarBytes, byte[] backgroundBytes, bool spoilerARR, bool spoilerHW, bool spoilerSB, bool spoilerSHB, bool spoilerEW, bool spoilerDT, bool NSFW, bool TRIGGERING, bool equipmentPublic = false, int immersiveTheme = 0)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SSendProfileStatus);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteString(profileTitle);
                        buffer.WriteFloat(color.X);
                        buffer.WriteFloat(color.Y);
                        buffer.WriteFloat(color.Z);
                        buffer.WriteFloat(color.W);
                        buffer.WriteInt(avatarBytes.Length);
                        buffer.WriteBytes(avatarBytes);
                        buffer.WriteInt(backgroundBytes.Length);
                        buffer.WriteBytes(backgroundBytes);
                        buffer.WriteBool(status);
                        buffer.WriteBool(tooltipStatus);
                        buffer.WriteBool(spoilerARR);
                        buffer.WriteBool(spoilerHW);
                        buffer.WriteBool(spoilerSB);
                        buffer.WriteBool(spoilerSHB);
                        buffer.WriteBool(spoilerEW);
                        buffer.WriteBool(spoilerDT);
                        buffer.WriteBool(NSFW);
                        buffer.WriteBool(TRIGGERING);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteBool(equipmentPublic);
                        // Immersive HUD theme the owner wants viewers to see (0 = viewer's choice). Trailing so servers that predate it simply never read it.
                        buffer.WriteInt(immersiveTheme);

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SetProfileStatus: " + ex.ToString());
                }
            }
        }

        internal static async void SendRequestPlayerTooltip(Character character, string playerName, string playerWorld)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SRequestTooltip);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteString(playerName);
                        buffer.WriteString(playerWorld);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    //removed cus of log spam
                }
            }
        }

        // Tooltip request by a specific profile id
        internal static async void SendRequestTooltipByProfileId(Character character, int profileId)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.SRequestTooltipByProfileId);
                buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(profileId);
                await ClientTCP.SendDataAsync(buffer.ToArray());
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("SendRequestTooltipByProfileId failed: " + ex.Message);
            }
        }

        internal static async void DeleteProfile(Character character, int profileIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SDeleteProfile);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(profileIndex);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in DeleteProfile: " + ex.ToString());
                }
            }
        }

        // Requests the list of all profiles owned by the current character
        internal static async void FetchProfiles(Character character)
        {

            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SFetchProfiles);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in FetchProfiles: " + ex.ToString());
                }
            }
        }

        internal static async void SetProfileAsTooltip(Character character, bool isPrivate, string playername, string playerworld, int profileIndex, bool status)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SetAsTooltip);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteString(playername);
                        buffer.WriteString(playerworld);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteBool(status);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SetProfileAsTooltip: " + ex.ToString());
                }
            }
        }

        internal static async void RequestTargetProfileByCharacter(Character character, string name, string worldname)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.RequestTargetProfileByCharacter);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteString(name);
                        buffer.WriteString(worldname);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in RequestTargetProfileByCharacter: " + ex.ToString());
                }
            }
        }

        internal static async void PreviewProfile(Character character, int currentProfile)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.PreviewProfile);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(currentProfile);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in PreviewProfile: " + ex.ToString());
                }
            }
        }
    }
}
