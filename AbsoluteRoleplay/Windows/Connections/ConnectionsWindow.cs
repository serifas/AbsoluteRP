using AbsoluteRoleplay.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using Networking;

namespace AbsoluteRoleplay.Windows.Profiles
{
    public class ConnectionsWindow : Window, IDisposable
    {
        public static List<Tuple<string, string>> receivedProfileRequests = new List<Tuple<string, string>>();
        public static List<Tuple<string, string>> sentProfileRequests = new List<Tuple<string, string>>();
        public static List<Tuple<string, string>> blockedProfileRequests = new List<Tuple<string, string>>();
        public static List<Tuple<string, string>> connetedProfileList = new List<Tuple<string, string>>();
        public static string username = "";
        public static string localPlayerName = "";
        public static string localPlayerWorld = "";
        public static int currentListing = 0;
        private IDalamudPluginInterface pg;
        public ConnectionsWindow() : base(
       "CONNECTIONS", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(300, 300),
                MaximumSize = new Vector2(950, 950)
            };
        }
        public override void Draw()
        {
            try
            {
               
                AddConnectionListingOptions();
                Vector2 windowSize = ImGui.GetWindowSize();
                var childSize = new Vector2(windowSize.X - 30, windowSize.Y - 80);
                localPlayerName = Plugin.plugin.playername;
                localPlayerWorld = Plugin.plugin.playerworld;
                if (currentListing == 2)
                {
                    using var receivedRequestsTable = ImRaii.Child("ReceivedRequests", childSize, true);
                    if (receivedRequestsTable)
                    {
                        for (var i = 0; i < receivedProfileRequests.Count; i++)
                        {
                            var requesterName = receivedProfileRequests[i].Item1;
                            var requesterWorld = receivedProfileRequests[i].Item2;
                            ImGui.TextUnformatted(requesterName + " @ " + requesterWorld);
                            ImGui.SameLine();
                            using (ImRaii.Disabled(!Plugin.CtrlPressed()))
                            {
                                if (ImGui.Button("Decline##Decline" + i))
                                {
                                    DataSender.SendProfileAccessUpdate(username, localPlayerName, localPlayerWorld, requesterName, requesterWorld, (int)UI.ConnectionStatus.refused);
                                }
                            }
                            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            {
                                ImGui.SetTooltip("Ctrl Click to Enable");
                            }
                            ImGui.SameLine();
                            using (ImRaii.Disabled(!Plugin.CtrlPressed()))
                            {
                                if (ImGui.Button("Accept##Accept" + i))
                                {
                                    DataSender.SendProfileAccessUpdate(username, localPlayerName, localPlayerWorld, requesterName, requesterWorld, (int)UI.ConnectionStatus.accepted);
                                }
                            }
                            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            {
                                ImGui.SetTooltip("Ctrl Click to Enable");
                            }
                            ImGui.SameLine();
                            using (ImRaii.Disabled(!Plugin.CtrlPressed()))
                            {
                                if (ImGui.Button("Block##Block" + i))
                                {
                                    DataSender.SendProfileAccessUpdate(username, localPlayerName, localPlayerWorld, requesterName, requesterWorld, (int)UI.ConnectionStatus.blocked);
                                }
                            }
                            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            {
                                ImGui.SetTooltip("Ctrl Click to Enable");
                            }
                        }
                    }
                }
                if (currentListing == 0)
                {
                    using var connectedTable = ImRaii.Child("Connected", childSize, true);
                    if (connectedTable)
                    {

                        for (var i = 0; i < connetedProfileList.Count; i++)
                        {
                            var connectionName = connetedProfileList[i].Item1;
                            var connectionWorld = connetedProfileList[i].Item2;
                            ImGui.TextUnformatted(connectionName + " @ " + connectionWorld);
                            ImGui.SameLine();
                            using (ImRaii.Disabled(!Plugin.CtrlPressed()))
                            {
                                if (ImGui.Button("Remove##Remove" + i))
                                {
                                    DataSender.SendProfileAccessUpdate(username, localPlayerName, localPlayerWorld, connectionName, connectionWorld, (int)UI.ConnectionStatus.refused);
                                }
                            }
                            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            {
                                ImGui.SetTooltip("Ctrl Click to Enable");
                            }
                            ImGui.SameLine();
                            using (ImRaii.Disabled(!Plugin.CtrlPressed()))
                            {
                                if (ImGui.Button("Block##Block" + i))
                                {
                                    DataSender.SendProfileAccessUpdate(username, localPlayerName, localPlayerWorld, connectionName, connectionWorld, (int)UI.ConnectionStatus.blocked);
                                }
                            }
                            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            {
                                ImGui.SetTooltip("Ctrl Click to Enable");
                            }
                        }
                    }

                }
                if (currentListing == 1)
                {
                    using var sentRequestsTable = ImRaii.Child("SentRequests", childSize, true);
                    if (sentRequestsTable)
                    {

                        for (var i = 0; i < sentProfileRequests.Count; i++)
                        {

                            var receiverName = sentProfileRequests[i].Item1;
                            var receiverWorld = sentProfileRequests[i].Item2;
                            ImGui.TextUnformatted(receiverName + " @ " + receiverWorld);
                            ImGui.SameLine();
                            using (ImRaii.Disabled(!Plugin.CtrlPressed()))
                            {
                                if (ImGui.Button("Cancel##Cancel" + i))
                                {
                                    DataSender.SendProfileAccessUpdate(username, localPlayerName, localPlayerWorld, receiverName, receiverWorld, (int)UI.ConnectionStatus.refused);
                                }
                            }
                            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            {
                                ImGui.SetTooltip("Ctrl Click to Enable");
                            }
                        }
                    }
                }
                if (currentListing == 3)
                {
                    using var blockedRequestsTable = ImRaii.Child("BlockedRequests", childSize, true);
                    if (blockedRequestsTable)
                    {

                        for (var i = 0; i < blockedProfileRequests.Count; i++)
                        {
                            var blockedName = blockedProfileRequests[i].Item1;
                            var blockedWorld = blockedProfileRequests[i].Item2;
                            ImGui.TextUnformatted(blockedName + " @ " + blockedWorld);
                            ImGui.SameLine();
                            using (ImRaii.Disabled(!Plugin.CtrlPressed()))
                            {
                                if (ImGui.Button("Unblock##Unblock" + i))
                                {
                                    DataSender.SendProfileAccessUpdate(username, localPlayerName, localPlayerWorld, blockedName, blockedWorld, (int)UI.ConnectionStatus.refused);
                                }
                            }
                            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            {
                                ImGui.SetTooltip("Ctrl Click to Enable");
                            }
                        }
                    }


                }

            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("ConnectionsWindow Draw Debug: " + ex.Message);
            }
        }
        public void AddConnectionListingOptions()
        {
            var (text, desc) = UI.ConnectionListingVals[currentListing];
            using var combo = ImRaii.Combo("##Connetions", text);
            if (!combo)
                return;

            foreach (var ((newText, newDesc), idx) in UI.ConnectionListingVals.WithIndex())
            {
                if (ImGui.Selectable(newText, idx == currentListing))
                    currentListing = idx;

                UIHelpers.SelectableHelpMarker(newDesc);
            }
        }
        public void Dispose()
        {

        }
    }
}
