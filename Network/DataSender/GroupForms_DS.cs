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
    // Group form channel requests. Split out of DataSender.
    internal class GroupForms_DS
    {
        public static async void CreateFormField(Character character, int channelId, string title, int fieldType, bool isOptional, int sortOrder)
        {
            try
            {
                Plugin.PluginLog.Info($"[CreateFormField] Sending: channelId={channelId}, title='{title}', fieldType={fieldType}, isOptional={isOptional}, sortOrder={sortOrder}");
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.CreateFormField);
                buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(channelId);
                buffer.WriteString(title);
                buffer.WriteInt(fieldType);
                buffer.WriteBool(isOptional);
                buffer.WriteInt(sortOrder);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
                Plugin.PluginLog.Info($"[CreateFormField] Packet sent successfully");
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"CreateFormField error: {ex.Message}");
            }
        }

        public static async void UpdateFormField(Character character, int fieldId, string title, int fieldType, bool isOptional, int sortOrder)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.UpdateFormField);
                buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(fieldId);
                buffer.WriteString(title);
                buffer.WriteInt(fieldType);
                buffer.WriteBool(isOptional);
                buffer.WriteInt(sortOrder);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"UpdateFormField error: {ex.Message}");
            }
        }

        public static async void DeleteFormField(Character character, int fieldId)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.DeleteFormField);
                buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(fieldId);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"DeleteFormField error: {ex.Message}");
            }
        }

        public static async void FetchFormFields(Character character, int channelId)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.FetchFormFields);
                buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(channelId);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"FetchFormFields error: {ex.Message}");
            }
        }

        public static async void SubmitForm(Character character, int channelId, int profileId, string profileName, List<(int fieldId, string value)> fieldValues)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.SubmitForm);
                buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(channelId);
                buffer.WriteInt(profileId);
                buffer.WriteString(profileName ?? "");
                buffer.WriteInt(fieldValues?.Count ?? 0);
                if (fieldValues != null)
                {
                    foreach (var (fieldId, value) in fieldValues)
                    {
                        buffer.WriteInt(fieldId);
                        buffer.WriteString(value ?? "");
                    }
                }
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"SubmitForm error: {ex.Message}");
            }
        }

        public static async void FetchFormSubmissions(Character character, int channelId)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.FetchFormSubmissions);
                buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(channelId);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"FetchFormSubmissions error: {ex.Message}");
            }
        }

        public static async void DeleteFormSubmission(Character character, int submissionId)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.DeleteFormSubmission);
                buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(submissionId);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"DeleteFormSubmission error: {ex.Message}");
            }
        }

        public static async void UpdateFormChannelSettings(Character character, int channelId, bool allowFormatTags)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.UpdateFormChannelSettings);
                buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                buffer.WriteString(character.characterKey);
                buffer.WriteInt(channelId);
                buffer.WriteBool(allowFormatTags);
                await ClientTCP.SendDataAsync(buffer.ToArray());
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"UpdateFormChannelSettings error: {ex.Message}");
            }
        }
    }
}
