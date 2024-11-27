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

namespace AbsoluteRoleplay.Helpers
{
    public class NpcInteraction
    {
        public static Plugin plugin;

        public static void GetNPCsInRange(float range, IPlayerCharacter localPlayer)
        {


            foreach (var obj in Plugin.ObjectTable)
            {

                // Filter only player objects and exclude the local player
                if (obj is IPlayerCharacter player)
                {
                    // Calculate distance between the player and the local player
                    float distance = Vector3.Distance(localPlayer.Position, player.Position);
                    if (distance <= range)
                    {
                        DrawSquareAbovePlayer(player);
                    }
                }
            }
        }
     
        private static void DrawSquareAbovePlayer(IPlayerCharacter player)
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
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                ImGuiHelpers.ForceNextWindowMainViewport();
                ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(0, 0));

                ImGui.Begin("Canvas",
                    ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoTitleBar |
                    ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoFocusOnAppearing);

                ImGui.SetWindowSize(ImGui.GetIO().DisplaySize);

                // Calculate dynamic height based on screen distance between nametag and base
                float boxHeight = screenBottom.Y - screenTop.Y;
                float boxWidth = boxHeight * widthToHeightRatio;

                // Calculate rectangle boundaries based on screenTop (nametag) and screenBottom (base)
                var topLeft = new Vector2(screenBottom.X - boxWidth / 2, screenTop.Y);
                var bottomRight = new Vector2(screenBottom.X + boxWidth / 2, screenBottom.Y);

                // Draw the rectangle spanning from nametag to player base
                ImGui.GetWindowDrawList().AddRectFilled(topLeft, bottomRight, color);

                // Check if the mouse is hovering over the rectangle
                bool isMouseOverBox = ImGui.IsMouseHoveringRect(topLeft, bottomRight);
                if (isMouseOverBox)
                {
                    // Perform any actions you want when the mouse is over the box
                    plugin.logger.Error("Mouse over" + player.Name.ToString());
                }

                ImGui.End();
                ImGui.PopStyleVar();
            }
        }

    }


}



