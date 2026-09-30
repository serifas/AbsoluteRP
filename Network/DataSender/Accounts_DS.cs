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
    // Login, verification and account profile requests. Split out of DataSender.
    internal class Accounts_DS
    {
        public static int userID;   // set by the server in the StatusMessage response

        // Asks the server to check if a Lodestone URL is valid for character verification. Used during initial character linking and account restoration flows.
        internal static async void CheckLodestoneEntry(string lodeSUrl, bool restoration)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendCheckLodestoneEntry);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(lodeSUrl);
                        buffer.WriteBool(restoration);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in CreateUserTag: " + ex.ToString());
                }
            }
        }

        // Registers a new account tag (username) with the server
        internal static async void CreateUserTag(string tagName)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendUserTagCreation);
                        buffer.WriteString(tagName);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in CreateUserTag: " + ex.ToString());
                }
            }
        }

        internal static async void SubmitLodestoneURL(string lodeSUrl, string account_tag, bool restoration)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendLodestoneURL);
                        buffer.WriteString(account_tag);
                        buffer.WriteString(lodeSUrl);
                        buffer.WriteBool(restoration);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in CreateUserTag: " + ex.ToString());
                }
            }
        }

        internal static async void UnlinkAccount()
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.UnlinkAccount);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in CreateUserTag: " + ex.ToString());
                }
            }
        }

        // Authenticates with the server using the stored account key. Sends the plugin version so the server can enforce compatibility.
        internal static async void SendLogin()
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CLogin);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString("0.2.26");
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in CreateUserTag: " + ex.ToString());
                }
            }
        }

        // ==============================
        // Account Profile (display name / avatar / gender / age / RP prefs)
        // ==============================

        // Pushes the local account profile to the server so it persists and becomes visible to other players who fetch it.
        public static async void SendAccountProfile(AbsoluteRP.Defines.AccountProfile p)
        {
            if (!ClientTCP.IsConnected() || p == null) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.SendAccountProfile);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(p.displayName ?? string.Empty);
                    buffer.WriteString(p.avatarUrl ?? string.Empty);
                    buffer.WriteString(p.gender ?? string.Empty);
                    buffer.WriteInt(p.age ?? 0);
                    var prefs = p.rpPreferences ?? new System.Collections.Generic.List<string>();
                    buffer.WriteInt(prefs.Count);
                    for (int i = 0; i < prefs.Count; i++)
                        buffer.WriteString(prefs[i] ?? string.Empty);
                    // v2: bio + headerUrl appended at the end for backward-compat - an older server just ignores them.
                    buffer.WriteString(p.bio ?? string.Empty);
                    buffer.WriteString(p.headerUrl ?? string.Empty);
                    // v3: header framing (offsetX, offsetY, zoom) as ints scaled by 1000 so we don't need a WriteFloat helper.
                    buffer.WriteInt((int)(Math.Clamp(p.headerOffsetX, 0f, 1f) * 1000f));
                    buffer.WriteInt((int)(Math.Clamp(p.headerOffsetY, 0f, 1f) * 1000f));
                    buffer.WriteInt((int)(Math.Clamp(p.headerZoom, 1f, 5f) * 1000f));
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SendAccountProfile error: " + ex.Message); }
        }

        // Requests an account profile from the server. Pass 0 to load the caller's own profile (server infers ownership from the auth key).
        public static async void RequestAccountProfile(int userId)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.RequestAccountProfile);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteInt(userId);
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("RequestAccountProfile error: " + ex.Message); }
        }
    }
}
