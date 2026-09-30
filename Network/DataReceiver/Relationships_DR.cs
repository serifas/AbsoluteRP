using AbsoluteRP.Helpers;
using Networking;
using System;
using System.Collections.Generic;

namespace AbsoluteRP.Network
{
    // Profile link (relationship bond) packets.
    internal class Relationships_DR
    {
        private static RelationshipBond ReadBond(ByteBuffer buffer) => new RelationshipBond
        {
            BondID            = buffer.ReadInt(),
            OwnerAccountID    = buffer.ReadInt(),
            OwnerProfileIndex = buffer.ReadInt(),
            PeerAccountID     = buffer.ReadInt(),
            PeerProfileIndex  = buffer.ReadInt(),
            PeerName          = buffer.ReadString(),
            PeerWorld         = buffer.ReadString(),
            PeerProfileTitle  = buffer.ReadString(),
            Title             = buffer.ReadString(),
            Relation          = buffer.ReadString(),
            Status            = buffer.ReadInt(),
            RequestedByUs     = buffer.ReadInt() != 0,
        };

        public static void HandleRelationshipsList(byte[] data)
        {
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt();
                int count = buffer.ReadInt();
                if (count < 0 || count > 4096) count = 0;
                var list = new List<RelationshipBond>(count);
                for (int i = 0; i < count; i++) list.Add(ReadBond(buffer));
                RelationshipManager.ReplaceAll(list);
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("HandleRelationshipsList: " + ex.Message); }
        }

        public static void HandleRelationshipUpdate(byte[] data)
        {
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt();
                int op = buffer.ReadInt();
                if (op == 1) { RelationshipManager.Remove(buffer.ReadInt()); return; }
                RelationshipManager.Upsert(ReadBond(buffer));
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("HandleRelationshipUpdate: " + ex.Message); }
        }

        public static void HandleProfileAvatar(byte[] data)
        {
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt();
                int accountId = buffer.ReadInt();
                int profileIndex = buffer.ReadInt();
                int len = buffer.ReadInt();
                var bytes = len > 0 && len < 20 * 1024 * 1024 ? buffer.ReadBytes(len) : Array.Empty<byte>();
                System.Numerics.Vector4? colour = null;
                if (buffer.Length() >= 16)
                {
                    var c = new System.Numerics.Vector4(buffer.ReadFloat(), buffer.ReadFloat(), buffer.ReadFloat(), buffer.ReadFloat());
                    if (c.W > 0.01f) colour = c;
                }
                ProfileAvatars.Received(accountId, profileIndex, bytes, colour);
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("HandleProfileAvatar: " + ex.Message); }
        }

        public static void HandleIncomingRelationshipRequest(byte[] data)
        {
            try
            {
                using var buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt();
                var bond = ReadBond(buffer);
                RelationshipManager.Upsert(bond);
                try
                {
                    Plugin.chatgui?.Print($"[AbsoluteRP] {bond.PeerName} asked to link profiles with you as \"{bond.Title}\". Open a Relationship tab on your profile to answer.");
                }
                catch { }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("HandleIncomingRelationshipRequest: " + ex.Message); }
        }
    }
}
