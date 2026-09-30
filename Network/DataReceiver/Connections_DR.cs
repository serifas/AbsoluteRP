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
    // Connection and nearby-player packets. Split out of DataReceiver.
    internal class Connections_DR
    {
        internal static void ReceiveConnectedPlayers(byte[] data)
        {
            try
            {
                List<PlayerData> connectedPlayers = new List<PlayerData>();
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int connectionsCount = buffer.ReadInt();

                    for (int i = 0; i < connectionsCount; i++)
                    {
                        string playerName = buffer.ReadString();
                        string playerWorld = buffer.ReadString();
                        bool customName = buffer.ReadBool();
                        string profileName = buffer.ReadString();
                        PlayerData playerData = new PlayerData() { playername = playerName, worldname = playerWorld, profileName = profileName, customName = customName };
                        connectedPlayers.Add(playerData);
                    }
                    PlayerInteractions.playerDataMap = connectedPlayers;
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveConnections message: {ex}");
            }
        }

        // Receives the full connections list (friends, pending requests, blocks).
        // Sorts each connection into the appropriate bucket (connected, sent, received, blocked)
        // based on the status field and whether the current user is the requester or receiver.
        internal static void ReceiveConnections(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int connectionsCount = buffer.ReadInt();
                    Connections.connetedProfileList.Clear();
                    Connections.sentProfileRequests.Clear();
                    Connections.receivedProfileRequests.Clear();
                    Connections.blockedProfileRequests.Clear();
                    Connections.All.Clear();
                    for (int i = 0; i < connectionsCount; i++)
                    {
                        string requesterName = buffer.ReadString();
                        string requesterWorld = buffer.ReadString();
                        string receiverName = buffer.ReadString();
                        string receiverWorld = buffer.ReadString();
                        int status = buffer.ReadInt();
                        bool isReceiver = buffer.ReadBool();
                        // Newer servers add the other account's id and username.
                        int peerId = buffer.Length() >= 4 ? buffer.ReadInt() : 0;
                        string peerUser = buffer.Length() >= 4 ? buffer.ReadString() : string.Empty;
                        if (!string.IsNullOrEmpty(isReceiver ? requesterName : receiverName))
                            Connections.All.Add(new Connections.Entry
                            {
                                Name = isReceiver ? requesterName : receiverName, World = isReceiver ? requesterWorld : receiverWorld,
                                UserId = peerId, Username = peerUser ?? string.Empty, Status = status, IsReceiver = isReceiver,
                            });
                        Tuple<string, string> requester = Tuple.Create(requesterName, requesterWorld);
                        Tuple<string, string> receiver = Tuple.Create(receiverName, receiverWorld);
                        if (isReceiver)
                        {
                            if (status == (int)UI.ConnectionStatus.pending)
                            {
                                Connections.receivedProfileRequests.Add(requester);
                            }
                            if (status == (int)UI.ConnectionStatus.accepted)
                            {
                                PlayerData playerData = new PlayerData() { playername = requesterName, worldname = requesterWorld };
                                PlayerInteractions.playerDataMap.Add(playerData);
                                Connections.connetedProfileList.Add(requester);
                            }
                            if (status == (int)UI.ConnectionStatus.blocked)
                            {
                                Connections.blockedProfileRequests.Add(requester);
                            }
                            if (status == (int)UI.ConnectionStatus.refused)
                            {
                                if (Connections.receivedProfileRequests.Contains(requester))
                                {
                                    Connections.receivedProfileRequests.Remove(requester);
                                }
                            }
                        }
                        else if (!isReceiver)
                        {
                            if (status == (int)UI.ConnectionStatus.pending)
                            {
                                Connections.sentProfileRequests.Add(receiver);
                            }
                            if (status == (int)UI.ConnectionStatus.accepted)
                            {
                                PlayerData playerData = new PlayerData() { playername = receiverName, worldname = receiverWorld };
                                PlayerInteractions.playerDataMap.Add(playerData);
                                Connections.connetedProfileList.Add(receiver);
                            }
                            if (status == (int)UI.ConnectionStatus.blocked)
                            {
                                Connections.blockedProfileRequests.Add(receiver);
                            }
                            if (status == (int)UI.ConnectionStatus.refused)
                            {
                                // ConnectionsWindow.sentProfileRequests.Add(receiver);
                            }
                        }
                    }

                    Connections.LastReceivedMs = Environment.TickCount64;
                    Plugin.plugin.newConnection = false;
                   // Plugin.plugin.CheckConnectionsRequestStatus();

                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveConnections message: {ex}");
            }
        }

        internal static void ReceiveConnectionsRequest(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    Plugin.PluginLog.Info("[ReceiveConnectionsRequest] Server notified of pending connection request - showing DTR bar");
                    Plugin.plugin.newConnection = true;
                  // Plugin.plugin.CheckConnectionsRequestStatus();

                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveConnectionsRequest message: {ex}");
            }
        }

        internal static void ReceivePlayerSyncData(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    string name = buffer.ReadString();
                    int modDataCount = buffer.ReadInt();

                    for (int i = 0; i < modDataCount; i++)
                    {
                        int byteLen = buffer.ReadInt();
                        byte[] bytes = buffer.ReadBytes(byteLen);

                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveConnectionsRequest message: {ex}");
            }
        }

        public static void ReceiveConnectedPlayersInMap(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int connectionsCount = buffer.ReadInt();
                    for (int i = 0; i < connectionsCount; i++)
                    {
                        bool active = buffer.ReadBool();
                        string fauxName = buffer.ReadString();
                        string playerName = buffer.ReadString();
                        string playerWorld = buffer.ReadString();

                        if (active)
                        {
                            PlayerData playerData = new PlayerData
                            {
                                playername = playerName,
                                worldname = playerWorld,
                                fauxName = fauxName,
                            };
                            PlayerInteractions.playerDataMap.Add(playerData);
                        }



                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveConnectedPlayersInMap message: {ex}");
            }
        }
    }
}
