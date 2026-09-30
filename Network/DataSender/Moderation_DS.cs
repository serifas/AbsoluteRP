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
    // Reports and moderator actions. Split out of DataSender.
    internal class Moderation_DS
    {
        // Submits a profile report to moderators with the reporter's info and reason
        public static async void ReportProfile(Character character, string reporterAccount, string playerName, string playerWorld, string reportInfo)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CReportProfile);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteString(playerName);
                        buffer.WriteString(playerWorld);
                        buffer.WriteString(reporterAccount);
                        buffer.WriteString(reportInfo);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in ReportProfile: " + ex.ToString());
                }
            }

        }

        internal static async void SubmitModeratorAction(Character character, int capturedAuthor, string capturedMessage, string moderatorMessage, string moderatorNotes, ModeratorAction currentAction)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendModeratorAction);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(capturedAuthor);
                        buffer.WriteString(capturedMessage);
                        buffer.WriteString(moderatorMessage);
                        buffer.WriteString(moderatorNotes);
                        buffer.WriteInt((int)currentAction);

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendProfileItems: " + ex.ToString());
                }
            }
        }
    }
}
