using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Networking
{ 
    static class ClientHandleData
    {
        private static ByteBuffer playerBuffer;
        public static DataReceiver dr = new DataReceiver();
        public delegate void Packet(byte[] data);
        public static Dictionary<int, Packet> packets = new Dictionary<int, Packet>();

        //add our packets so we don't need to load them on the go.
        //should be added to start of client loading up
        public static void InitializePackets()
        {
            packets.Clear();
            packets.Add((int)ServerPackets.SWelcomeMessage, DataReceiver.HandleWelcomeMessage);
            packets.Add((int)ServerPackets.SRecLoginStatus, DataReceiver.StatusMessage);
            packets.Add((int)ServerPackets.SRecProfileBio, DataReceiver.ReceiveProfileBio);
            packets.Add((int)ServerPackets.SNoProfileBio, DataReceiver.NoProfileBio);
            packets.Add((int)ServerPackets.SNoProfile, DataReceiver.NoProfile);
            packets.Add((int)ServerPackets.SSendProfile, DataReceiver.ReceiveProfile);
            packets.Add((int)ServerPackets.SRecExistingProfile, DataReceiver.ExistingProfile);
            packets.Add((int)ServerPackets.SSendProfileHook, DataReceiver.ReceiveProfileHooks);
            packets.Add((int)ServerPackets.SSendNoProfileHooks, DataReceiver.NoProfileHooks);
            packets.Add((int)ServerPackets.SRecProfileStory, DataReceiver.ReceiveProfileStory);
            packets.Add((int)ServerPackets.SRecNoProfileStory, DataReceiver.NoProfileStory);
            packets.Add((int)ServerPackets.SSendNoProfileNotes, DataReceiver.NoProfileNotes);
            packets.Add((int)ServerPackets.SSendProfileNotes, DataReceiver.RecProfileNotes);
            packets.Add((int)ServerPackets.SRecNoProfileGallery, DataReceiver.ReceiveNoProfileGallery);
            packets.Add((int)ServerPackets.SRecBookmarks, DataReceiver.RecBookmarks);
            packets.Add((int)ServerPackets.CProfileReportedSuccessfully, DataReceiver.RecProfileReportedSuccessfully);
            packets.Add((int)ServerPackets.CProfileAlreadyReported, DataReceiver.RecProfileAlreadyReported);
            packets.Add((int)ServerPackets.SRecNoTargetProfile, DataReceiver.NoTargetProfile);
            packets.Add((int)ServerPackets.SRecTargetProfile, DataReceiver.ExistingTargetProfile);
            packets.Add((int)ServerPackets.SRecNoTargetBio, DataReceiver.NoTargetBio);
            packets.Add((int)ServerPackets.SRecTargetBio, DataReceiver.ReceiveTargetBio);
            packets.Add((int)ServerPackets.SRecNoTargetHooks, DataReceiver.NoTargetHooks);
            packets.Add((int)ServerPackets.SRecTargetHooks, DataReceiver.ReceiveTargetHooks);
            packets.Add((int)ServerPackets.SRecNoTargetStory, DataReceiver.NoTargetStory);
            packets.Add((int)ServerPackets.SRecTargetStory, DataReceiver.ReceiveTargetStory);
            packets.Add((int)ServerPackets.SRecProfileGallery, DataReceiver.ReceiveProfileGalleryImage);
            packets.Add((int)ServerPackets.SRecNoTargetGallery, DataReceiver.NoTargetGallery);
            packets.Add((int)ServerPackets.SRecTargetGallery, DataReceiver.ReceiveTargetGalleryImage);
            packets.Add((int)ServerPackets.SSendNoAuthorization, DataReceiver.ReceiveNoAuthorization);
            packets.Add((int)ServerPackets.SSendVerificationMessage, DataReceiver.ReceiveVerificationMessage);
            packets.Add((int)ServerPackets.SSendPasswordModificationForm, DataReceiver.ReceivePasswordModificationForm);
            packets.Add((int)ServerPackets.SSendNoOOCInfo, DataReceiver.ReceiveNoOOCInfo);
            packets.Add((int)ServerPackets.SSendOOC, DataReceiver.ReceiveProfileOOC);
            packets.Add((int)ServerPackets.SSendTargetOOC, DataReceiver.ReceiveTargetOOCInfo);
            packets.Add((int)ServerPackets.SSendNoTargetOOCInfo, DataReceiver.ReceiveNoTargetOOCInfo);
            packets.Add((int)ServerPackets.ReceiveConnections, DataReceiver.ReceiveConnections);
            packets.Add((int)ServerPackets.ReceiveNewConnectionRequest, DataReceiver.ReceiveConnectionsRequest);
            packets.Add((int)ServerPackets.RecieveTargetTooltip, DataReceiver.ReceiveTargetTooltip);
            packets.Add((int)ServerPackets.ReceiveProfiles, DataReceiver.ReceiveProfiles);
            packets.Add((int)ServerPackets.CreateItem, DataReceiver.ReceiveProfileItems);
            packets.Add((int)ServerPackets.ReceiveProfileSettings, DataReceiver.ReceiveProfileSettings);

            packets.Add((int)ServerPackets.ReceiveChatMessage, DataReceiver.ReceiveChatMessage);
            packets.Add((int)ServerPackets.ReceiveProfileWarning, DataReceiver.RecieveProfileWarning);
            // packets.Add((int)ServerPackets.ReceiveGroupMemberships, DataReceiver.ReceiveGroupMemberships);


            //simple message back from server, simply for verification that the user is connected
        }

        public static void HandleData(byte[] data)
        {
            // Clone the incoming data to avoid modifying the original buffer
            var buffer = (byte[])data.Clone();
            var pLength = 0;

            // Initialize the player buffer if it's null
            if (playerBuffer == null)
            {
                playerBuffer = new ByteBuffer();
            }

            // Write the cloned data into the player buffer
            playerBuffer.WriteBytes(buffer);

            // Check if the buffer is empty; clear and return if true
            if (playerBuffer.Count() == 0)
            {
                playerBuffer.Clear();
                return;
            }

            // Ensure there are at least 4 bytes (length header) in the buffer
            if (playerBuffer.Length() > 4)
            {
                // Read the packet length without removing it from the buffer
                pLength = playerBuffer.ReadInt(false);

                // If the length is invalid, clear the buffer and exit
                if (pLength <= 0)
                {
                    playerBuffer.Clear();
                    return;
                }
            }

            // Process the data packets while there are valid lengths and enough data in the buffer
            while (pLength > 0 && pLength <= playerBuffer.Length() - 4)
            {
                // Check again if there is enough data for the packet
                if (pLength <= playerBuffer.Length() - 4)
                {
                    // Consume the packet length header
                    playerBuffer.ReadInt();

                    // Read the actual data packet
                    data = playerBuffer.ReadBytes(pLength);

                    // Handle the extracted data packet
                    HandleDataPackets(data);
                }

                // Reset packet length and check for the next packet
                pLength = 0;

                // Ensure there are still enough bytes for another length header
                if (playerBuffer.Length() > 4)
                {
                    // Peek the next packet length without removing it
                    pLength = playerBuffer.ReadInt(false);

                    // If the length is invalid, clear the buffer and exit
                    if (pLength <= 0)
                    {
                        playerBuffer.Clear();
                        return;
                    }
                }
            }

            // If no valid packet remains, clear the buffer
            if (pLength <= 1)
            {
                playerBuffer.Clear();
            }
        }

        private static void HandleDataPackets(byte[] data)
        {
            var buffer = new ByteBuffer();
            buffer.WriteBytes(data);
            var packetID = buffer.ReadInt();
            buffer.Dispose();
            if (packets.TryGetValue(packetID, out var packet))
            {
                packet.Invoke(data);
            }
        }
    }
}
