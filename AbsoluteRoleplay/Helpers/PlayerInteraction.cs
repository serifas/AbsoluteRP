using Networking;
using ImGuiNET;
using JetBrains.Annotations;
using System.Drawing.Imaging;
using Dalamud.Interface.Utility;
using AbsoluteRoleplay.Windows.Profiles;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Game.ClientState.Objects.Types;
using System;
using System.Numerics;
using Dalamud.Plugin.Services;
using System.Drawing;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Windowing;
using static FFXIVClientStructs.FFXIV.Client.UI.UIModule.Delegates;
using Lumina.Excel.Sheets;
using static Lumina.Data.Parsing.Layer.LayerCommon;

namespace AbsoluteRoleplay.Helpers
{
    public class PlayerInteraction
    {
        public static Plugin plugin;

        public static List<PlayerData> playerDataMap = new List<PlayerData>();
       
        public static PlayerData? GetConnectedPlayer(string playername, string playerworld)
        {
            // Use LINQ to find the first matching player in the list
            return playerDataMap
                .FirstOrDefault(p => p.playername == playername && p.worldname == playerworld);
        }
        public static void GetConnectionsInRange(float range, IPlayerCharacter localPlayer)
        {
            foreach (var obj in Plugin.ObjectTable)
            {
                // Filter only player objects and exclude the local player
                if (obj is IPlayerCharacter player)
                {
                    var connectedPlayer = GetConnectedPlayer(player.Name.ToString(), player.HomeWorld.Value.Name.ToString());
                    if (connectedPlayer != null)
                    {
                        // Calculate distance between the player and the local player
                        float distance = Vector3.Distance(localPlayer.Position, player.Position);
                        if (distance <= range)
                        {
                             DrawSquareAbovePlayer(player, connectedPlayer);
                        } 
                    }
                }
            }
        }

        private static void DrawSquareAbovePlayer(IPlayerCharacter player, PlayerData playerData)
        {
            // Settings
            float nametagOffset = 2.0f; // Approximate height offset for nametag in world space
            float widthToHeightRatio = 0.5f; // Proportion of box width to height
            uint color = ImGui.GetColorU32(new Vector4(1, 0, 0, 1)); // Red color

            // Get player's base position and nametag position in world space
            var playerBasePosition = player.Position;
            var playerNametagPosition = playerBasePosition + new Vector3(0, nametagOffset, 0);

            // Convert base and nametag positions to screen coordinates
            if (Plugin.GameGUI.WorldToScreen(playerBasePosition, out Vector2 screenBottom) &&
                Plugin.GameGUI.WorldToScreen(playerNametagPosition, out Vector2 screenTop))
            {
                // Calculate dynamic height based on screen distance between nametag and base
                float boxHeight = screenBottom.Y - screenTop.Y;
                float boxWidth = boxHeight * widthToHeightRatio;

                // Calculate rectangle boundaries based on screenTop (nametag) and screenBottom (base)
                var topLeft = new Vector2(screenBottom.X - boxWidth / 2, screenTop.Y);
                var bottomRight = new Vector2(screenBottom.X + boxWidth / 2, screenBottom.Y);

                // Draw the rectangle directly on the background draw list
                var drawList = ImGui.GetBackgroundDrawList();
                drawList.AddRectFilled(topLeft, bottomRight, color);

                // Check if the mouse is hovering over the rectangle
                if (ImGui.IsMouseHoveringRect(topLeft, bottomRight))
                {
                   // plugin.Logger.Error("Mouse over " + player.Name.ToString());
                }
            }
        }


    }



}
public class PlayerData
{
    public string playername { get; set; }
    public string worldname { get; set; }
    public bool loadingDisplayed { get; set; } = false;
}


