using AbsoluteRP.Windows.Profiles;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using JetBrains.Annotations;
using Lumina.Excel.Sheets;
using Networking;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using static FFXIVClientStructs.FFXIV.Client.UI.UIModule.Delegates;
using static Lumina.Data.Parsing.Layer.LayerCommon;

namespace AbsoluteRP.Helpers
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
        public static Vector2 CompassDragPosition = Vector2.Zero;
        public static bool CompassDragInitialized = false;

        public static bool wasDraggingCompass = false;
        public static List<PlayerData> playerDataMap = new List<PlayerData>();
        public static void SetFauxNameForPlayer(string name, string world, string fauxName)
        {
            var normalizedName = name.Trim();
            var normalizedWorld = world.Trim();

            var playerData = playerDataMap.FirstOrDefault(p =>
                string.Equals(p.playername?.Trim(), normalizedName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.worldname?.Trim(), normalizedWorld, StringComparison.OrdinalIgnoreCase));

            if (playerData != null)
            {
                playerData.fauxName = fauxName;
                playerData.fauxStatus = !string.IsNullOrEmpty(fauxName);
            }
        }

        // This method should be added to PlayerInteractions
   
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
 
        public static void DrawCompass()
        {
            if (Plugin.InCompassCombatLock())
            {
                return;
            }
            // Draw compass overlay if enabled and player is present
            if (Plugin.plugin.Configuration != null
                && Plugin.plugin.Configuration.showCompass
                && Plugin.IsOnline())
            {
                var viewport = ImGui.GetMainViewport();

                ImGui.SetNextWindowBgAlpha(0.0f);
                ImGui.SetNextWindowPos(new Vector2(0, 0), ImGuiCond.Always);
                ImGui.Begin("##CompassOverlay",
                    ImGuiWindowFlags.NoTitleBar
                    | ImGuiWindowFlags.NoResize
                    | ImGuiWindowFlags.NoMove
                    | ImGuiWindowFlags.NoScrollbar
                    | ImGuiWindowFlags.NoScrollWithMouse
                    | ImGuiWindowFlags.NoInputs
                    | ImGuiWindowFlags.NoFocusOnAppearing);

                // --- DRAG WINDOW LOGIC ---
                var dragBoxSize = new Vector2(400, 40);

                if (!PlayerInteractions.CompassDragInitialized)
                {
                    if (Plugin.plugin.Configuration.CompassPosX != 0f || Plugin.plugin.Configuration.CompassPosY != 0f)
                        PlayerInteractions.CompassDragPosition = new Vector2(Plugin.plugin.Configuration.CompassPosX, Plugin.plugin.Configuration.CompassPosY);
                    else
                        PlayerInteractions.CompassDragPosition = ImGui.GetMainViewport().GetCenter() with { Y = 300 };
                    PlayerInteractions.CompassDragInitialized = true;
                }

                ImGui.SetNextWindowPos(PlayerInteractions.CompassDragPosition - dragBoxSize / 2, ImGuiCond.Always);
                ImGui.SetNextWindowSize(dragBoxSize, ImGuiCond.Always);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8.0f);
                ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.001f);

                if (ImGui.Begin("##CompassDragWindow", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings))
                {
                    bool isDragging = ImGui.IsWindowHovered() && ImGui.IsMouseDragging(ImGuiMouseButton.Left);
                    if (isDragging)
                    {
                        PlayerInteractions.CompassDragPosition += ImGui.GetIO().MouseDelta;
                        PlayerInteractions.wasDraggingCompass = true;
                    }
                    else if (PlayerInteractions.wasDraggingCompass)
                    {
                        Plugin.plugin.Configuration.CompassPosX = PlayerInteractions.CompassDragPosition.X;
                        Plugin.plugin.Configuration.CompassPosY = PlayerInteractions.CompassDragPosition.Y;
                        Plugin.plugin.Configuration.Save();
                        PlayerInteractions.wasDraggingCompass = false;
                    }
                }
                ImGui.End();
                ImGui.PopStyleVar(2);

                // --- DRAW COMPASS USING DRAG WINDOW CENTER ---
                PlayerInteractions.DrawDynamicCompass(
                    PlayerInteractions.CompassDragPosition.X,
                    PlayerInteractions.CompassDragPosition.Y,
                    dragBoxSize.X,
                    dragBoxSize.Y,
                    Plugin.ClientState.LocalPlayer.Rotation
                );

                ImGui.End();
            }
        }
        public static void DrawDynamicCompass(
           float centerX, float centerY, float compassWidth, float compassHeight, float characterYawRadians)
        {
            var drawList = ImGui.GetBackgroundDrawList();

            Vector2 bgStart = new Vector2(centerX - compassWidth / 2, centerY - compassHeight / 2);
            Vector2 bgEnd = new Vector2(centerX + compassWidth / 2, centerY + compassHeight / 2);

            uint bgColorEdge = ImGui.GetColorU32(new Vector4(0, 0, 0, 0f));
            uint bgColorCenter = ImGui.GetColorU32(new Vector4(0, 0, 0, 0.2f));

            // Make the solid center section wider
            float centerSolidWidth = compassWidth * 0.5f; // 50% of compass width is solid, adjust as needed
            float leftSolidX = centerX - centerSolidWidth / 2;
            float rightSolidX = centerX + centerSolidWidth / 2;

            // Draw left gradient
            drawList.AddRectFilledMultiColor(
                bgStart,
                new Vector2(leftSolidX, bgEnd.Y),
                bgColorEdge, bgColorCenter, bgColorCenter, bgColorEdge
            );
            // Draw solid center
            drawList.AddRectFilled(
                new Vector2(leftSolidX, bgStart.Y),
                new Vector2(rightSolidX, bgEnd.Y),
                bgColorCenter
            );
            // Draw right gradient
            drawList.AddRectFilledMultiColor(
                new Vector2(rightSolidX, bgStart.Y),
                bgEnd,
                bgColorCenter, bgColorEdge, bgColorEdge, bgColorCenter
            );
            // --- TOP GLOW ---
            float glowThickness = 8f;
            Vector2 glowTopStart = new Vector2(centerX - compassWidth / 2, centerY - compassHeight / 2 - glowThickness);
            Vector2 glowTopEnd = new Vector2(centerX + compassWidth / 2, centerY - compassHeight / 2);

            uint glowColorEdge = ImGui.GetColorU32(new Vector4(0.2f, 0.6f, 1.0f, 0.0f));
            uint glowColorCenter = ImGui.GetColorU32(new Vector4(0.2f, 0.6f, 1.0f, 0.35f));

            drawList.AddRectFilledMultiColor(
                glowTopStart,
                new Vector2(centerX, glowTopEnd.Y),
                glowColorEdge, glowColorCenter, glowColorCenter, glowColorEdge
            );
            drawList.AddRectFilledMultiColor(
                new Vector2(centerX, glowTopStart.Y),
                glowTopEnd,
                glowColorCenter, glowColorEdge, glowColorEdge, glowColorCenter
            );

            // --- BOTTOM GLOW ---
            Vector2 glowBotStart = new Vector2(centerX - compassWidth / 2, centerY + compassHeight / 4f);
            Vector2 glowBotEnd = new Vector2(centerX + compassWidth / 2, centerY + compassHeight / 4f + glowThickness);

            drawList.AddRectFilledMultiColor(
                glowBotStart,
                new Vector2(centerX, glowBotEnd.Y),
                glowColorEdge, glowColorCenter, glowColorCenter, glowColorEdge
            );
            drawList.AddRectFilledMultiColor(
                new Vector2(centerX, glowBotStart.Y),
                glowBotEnd,
                glowColorCenter, glowColorEdge, glowColorEdge, glowColorCenter
            );

            if (compassWidth <= 0 || compassHeight <= 0)
                return;

            // --- Compass line ---
            Vector2 lineStart = new Vector2(centerX - compassWidth / 2, centerY);
            Vector2 lineEnd = new Vector2(centerX + compassWidth / 2, centerY);
            drawList.AddLine(lineStart, lineEnd, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), 2.0f);

            float maxOffset = compassWidth / 2 - 20;

            // Draw compass letters
            var directions = new[] {
        ("S", 0f),
        ("W", 3 * MathF.PI / 2),
        ("N", MathF.PI),
        ("E", MathF.PI / 2)
    };

            float yaw = NormalizeAngle(characterYawRadians);

            foreach (var (label, angle) in directions)
            {
                float diff = NormalizeAngle(yaw - angle);
                if (diff > MathF.PI) diff -= 2 * MathF.PI;

                float norm = diff / MathF.PI;
                float labelX = centerX + norm * maxOffset;
                float labelY = centerY - compassHeight / 2;
                float alpha = 1.0f - MathF.Abs(norm) * 0.7f;

                // Draw shadow (offset by 2 pixels)
                drawList.AddText(
                    new Vector2(labelX + 2, labelY + 2),
                    ImGui.GetColorU32(new Vector4(0, 0, 0, alpha * 0.7f)), // black, semi-transparent
                    label
                );
                // Draw main letter
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
                    float dotX = centerX - norm * maxOffset;

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
    public string profileName { get; set; }
    public bool customName { get; set; }
    public string fauxName { get; set; }
    public bool fauxStatus { get; set; }
}



