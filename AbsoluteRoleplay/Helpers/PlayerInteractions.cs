using Networking;
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
using Dalamud.Bindings.ImGui;

namespace AbsoluteRoleplay.Helpers
{
    public class FloorTextureInstance
    {
        public Vector3 WorldPosition;
        public uint TerritoryType;
        public IDalamudTextureWrap Texture;
        public float WorldSize;
    }
    public class PlayerInteractions
    {
        public static Plugin plugin;
        public static List<PlayerData> playerDataMap = new List<PlayerData>();
     
        public static PlayerData? GetConnectedPlayer(string playername, string playerworld)
        {
            // Use LINQ to find the first matching player in the list
            return playerDataMap
                .FirstOrDefault(p => p.playername == playername && p.worldname == playerworld);
        }
     
        public static void GetConnectionsInRange(float range, IPlayerCharacter localPlayer, Action<IPlayerCharacter, PlayerData>? action = null)
        {
            foreach (var obj in Plugin.ObjectTable)
            {
                if (obj is IPlayerCharacter player && player != localPlayer)
                {
                    var connectedPlayer = GetConnectedPlayer(player.Name.ToString(), player.HomeWorld.Value.Name.ToString());
                    if (connectedPlayer != null)
                    {
                        float distance = Vector3.Distance(localPlayer.Position, player.Position);
                        if (distance <= range)
                        {
                            action?.Invoke(player, connectedPlayer);
                        }
                    }
                }
            }
        }
        public static void DrawDynamicCompass(
            float centerX, float centerY, float compassWidth, float compassHeight, float characterYawRadians)
        {
     
            if (compassWidth <= 0 || compassHeight <= 0)
                return;

            var directions = new[] {
        ("S", 0f),
        ("W", 3 * MathF.PI / 2),
        ("N", MathF.PI),
        ("E", MathF.PI / 2)
    };

            float yaw = NormalizeAngle(characterYawRadians);

            var drawList = ImGui.GetBackgroundDrawList();
            Vector2 lineStart = new Vector2(centerX - compassWidth / 2, centerY);
            Vector2 lineEnd = new Vector2(centerX + compassWidth / 2, centerY);
            drawList.AddLine(lineStart, lineEnd, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), 2.0f);

            float maxOffset = compassWidth / 2 - 20;

            // Draw compass letters
            foreach (var (label, angle) in directions)
            {
                float diff = NormalizeAngle(yaw - angle);
                if (diff > MathF.PI) diff -= 2 * MathF.PI;

                float norm = diff / MathF.PI;
                float labelX = centerX + norm * maxOffset;
                float labelY = centerY - compassHeight / 2;
                float alpha = 1.0f - MathF.Abs(norm) * 0.7f;

                drawList.AddText(
                    new Vector2(labelX, labelY),
                    ImGui.GetColorU32(new Vector4(1, 1, 1, alpha)),
                    label
                );
            }

            // Draw radar dots for connected players
            DrawRadarDots(centerX, centerY, compassWidth, compassHeight, yaw);
        }

        // Draw blue dots for each connected player in range
        private static void DrawRadarDots(
            float centerX, float centerY, float compassWidth, float compassHeight, float localYaw)
        {
            var drawList = ImGui.GetBackgroundDrawList();
            var localPlayer = Plugin.ClientState.LocalPlayer;
            if (localPlayer == null) return;

            float maxOffset = compassWidth / 2 - 20;
            float dotY = centerY; // Place dots on the compass line

            foreach (var obj in Plugin.ObjectTable)
            {
                if (obj is IPlayerCharacter player && player != localPlayer)
                {
                    var connectedPlayer = GetConnectedPlayer(player.Name.ToString(), player.HomeWorld.Value.Name.ToString());
                    if (connectedPlayer == null) continue;

                    // Calculate direction vector from local to target
                    Vector3 toTarget = player.Position - localPlayer.Position;
                    if (toTarget.LengthSquared() < 0.01f) continue; // Skip if same position

                    // Get angle to target in world space
                    float angleToTarget = MathF.Atan2(toTarget.X, toTarget.Z); // Z is forward in FFXIV

                    // Calculate relative angle to local yaw
                    float diff = NormalizeAngle(angleToTarget - localYaw);
                    if (diff > MathF.PI) diff -= 2 * MathF.PI;

                    // Map to compass position
                    float norm = diff / MathF.PI;
                    float dotX = centerX + norm * maxOffset;

                    // Draw blue dot
                    drawList.AddCircleFilled(
                        new Vector2(dotX, dotY),
                        6.0f,
                        ImGui.GetColorU32(new Vector4(0.2f, 0.6f, 1.0f, 1.0f)), // Blue
                        12
                    );
                }
            }
        }
        // Helper to normalize angle to [0, 2PI)
        private static float NormalizeAngle(float angle)
        {
            while (angle < 0) angle += 2 * MathF.PI;
            while (angle >= 2 * MathF.PI) angle -= 2 * MathF.PI;
            return angle;
        }
       
    }
}
public class PlayerData
{
    public string playername { get; set; }
    public string worldname { get; set; }
}


