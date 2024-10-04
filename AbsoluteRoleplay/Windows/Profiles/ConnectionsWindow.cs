using Dalamud.Interface.Colors;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using ImGuiScene;
using OtterGui.Raii;
using OtterGui;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dalamud.Interface.GameFonts;
using Dalamud.Game.Gui.Dtr;
using Microsoft.VisualBasic;
using Networking;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.Havok;
using System.Text.RegularExpressions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface.Utility;
using AbsoluteRoleplay.Helpers;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AbsoluteRoleplay.Windows.Profiles
{
    public class ConnectionsWindow : Window, IDisposable
    {
        public Plugin plugin;
        public static List<Tuple<string, string>> receivedProfileRequests = new List<Tuple<string, string>>();
        public static List<Tuple<string, string>> sentProfileRequests = new List<Tuple<string, string>>();
        public static List<Tuple<string, string>> blockedProfileRequests = new List<Tuple<string, string>>();
        public static List<Tuple<string, string>> connetedProfileList = new List<Tuple<string, string>>();
        public static string username = "";
        public static string localPlayerName = "";
        public static string localPlayerWorld = "";
        public static int currentListing = 0;
        private IDalamudPluginInterface pg;
        public ConnectionsWindow(Plugin plugin) : base(
       "CONNECTIONS", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(300, 500),
                MaximumSize = new Vector2(500, 800)
            };
            this.plugin = plugin;
        }
        public override void Draw()
        {
            AddConnectionListingOptions();
            Vector2 windowSize = ImGui.GetWindowSize();
            var childSize = new Vector2(windowSize.X - 30, windowSize.Y - 80);
            localPlayerName = Plugin.ClientState.LocalPlayer.Name.ToString();
            localPlayerWorld = Plugin.ClientState.LocalPlayer.HomeWorld.GameData.Name.ToString();
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
                                DataSender.SendProfileAccessUpdate(username, localPlayerName, localPlayerWorld, requesterName, requesterWorld, (int)Defines.ConnectionStatus.refused);
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
                                DataSender.SendProfileAccessUpdate(username, localPlayerName, localPlayerWorld, requesterName, requesterWorld, (int)Defines.ConnectionStatus.accepted);
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
                                DataSender.SendProfileAccessUpdate(username, localPlayerName, localPlayerWorld, requesterName, requesterWorld, (int)Defines.ConnectionStatus.blocked);
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
                                DataSender.SendProfileAccessUpdate(username, localPlayerName, localPlayerWorld, connectionName, connectionWorld, (int)Defines.ConnectionStatus.removed);
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
                                DataSender.SendProfileAccessUpdate(username, localPlayerName, localPlayerWorld, connectionName, connectionWorld, (int)Defines.ConnectionStatus.blocked);
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
                                DataSender.SendProfileAccessUpdate(username, localPlayerName, localPlayerWorld, receiverName, receiverWorld, (int)Defines.ConnectionStatus.canceled);
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
                                DataSender.SendProfileAccessUpdate(username, localPlayerName, localPlayerWorld, blockedName, blockedWorld, (int)Defines.ConnectionStatus.removed);
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

        public void AddConnectionListingOptions()
        {
            var (text, desc) = Defines.ConnectionListingVals[currentListing];
            using var combo = ImRaii.Combo("##Connetions", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;

            foreach (var ((newText, newDesc), idx) in Defines.ConnectionListingVals.WithIndex())
            {
                if (ImGui.Selectable(newText, idx == currentListing))
                    currentListing = idx;

                ImGuiUtil.SelectableHelpMarker(newDesc);
            }
        }
        public void Dispose()
        {

        }
    }
}
