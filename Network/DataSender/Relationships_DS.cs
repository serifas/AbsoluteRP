using AbsoluteRP.Defines;
using Networking;
using System;

namespace AbsoluteRP.Network
{
    // Profile link (relationship bond) requests.
    internal class Relationships_DS
    {
        private static ByteBuffer Head(ClientPackets packet, Character character)
        {
            var buffer = new ByteBuffer();
            buffer.WriteInt((int)packet);
            buffer.WriteString(Plugin.plugin?.Configuration?.account?.accountKey ?? string.Empty);
            buffer.WriteString(character.characterName ?? string.Empty);
            buffer.WriteString(character.characterWorld ?? string.Empty);
            return buffer;
        }

        // Ask the owner of another profile to link with one of ours. `title` is the free-text position ("Father", "FC Leader"). targetProfileIndex may be -1 when only the profile id is known; the server resolves it from the id.
        public static async void SendRelationshipRequest(Character character, int senderProfileIndex, string targetName, string targetWorld,
                                                         int targetProfileIndex, string title, string relation, int targetProfileID = 0)
        {
            if (character == null || !ClientTCP.IsConnected()) return;
            try
            {
                using var buffer = Head(ClientPackets.CSendRelationshipRequest, character);
                buffer.WriteInt(senderProfileIndex);
                buffer.WriteString(targetName ?? string.Empty);
                buffer.WriteString(targetWorld ?? string.Empty);
                buffer.WriteInt(targetProfileIndex);
                buffer.WriteString(title ?? string.Empty);
                buffer.WriteString(relation ?? string.Empty);
                buffer.WriteInt(targetProfileID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("SendRelationshipRequest: " + ex.Message); }
        }

        public static async void RespondToRelationshipRequest(Character character, int bondID, int newStatus)
        {
            if (character == null || !ClientTCP.IsConnected()) return;
            try
            {
                using var buffer = Head(ClientPackets.CRespondToRelationshipRequest, character);
                buffer.WriteInt(bondID);
                buffer.WriteInt(newStatus);
                await ClientTCP.SendDataAsync(buffer.ToArray());
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("RespondToRelationshipRequest: " + ex.Message); }
        }

        public static async void RemoveRelationship(Character character, int bondID)
        {
            if (character == null || !ClientTCP.IsConnected()) return;
            try
            {
                using var buffer = Head(ClientPackets.CRemoveRelationship, character);
                buffer.WriteInt(bondID);
                await ClientTCP.SendDataAsync(buffer.ToArray());
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("RemoveRelationship: " + ex.Message); }
        }

        public static async void RequestProfileAvatar(int accountId, int profileIndex)
        {
            if (!ClientTCP.IsConnected() || accountId <= 0) return;
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteInt((int)ClientPackets.CRequestProfileAvatar);
                buffer.WriteString(Plugin.plugin?.Configuration?.account?.accountKey ?? string.Empty);
                buffer.WriteInt(accountId);
                buffer.WriteInt(profileIndex);
                await ClientTCP.SendDataAsync(buffer.ToArray());
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("RequestProfileAvatar: " + ex.Message); }
        }

        public static async void FetchRelationships(Character character)
        {
            if (character == null || !ClientTCP.IsConnected()) return;
            try
            {
                using var buffer = Head(ClientPackets.CFetchRelationships, character);
                await ClientTCP.SendDataAsync(buffer.ToArray());
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("FetchRelationships: " + ex.Message); }
        }
    }
}
