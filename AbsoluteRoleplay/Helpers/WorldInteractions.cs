using AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Internal.UiDebug2.Browsing;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using CharacterCopyFlags = FFXIVClientStructs.FFXIV.Client.Game.Character.CharacterSetupContainer.CopyFlags;
using ClientObjectManager = FFXIVClientStructs.FFXIV.Client.Game.Object.ClientObjectManager;
using NativeCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using StructsObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;

namespace AbsoluteRoleplay.Helpers
{
    public enum SpawnFlags
    {
        None = 0,
        ReserveCompanionSlot = 1 << 0,
        CopyPosition = 1 << 1,
        IsProp = 1 << 2,
        IsEffect = 1 << 3,
        SetDefaultAppearance = 1 << 4,

        Prop = IsProp | SetDefaultAppearance | CopyPosition,
        Effect = IsEffect | SetDefaultAppearance | CopyPosition,
        Default = CopyPosition,
    }

    internal static class WorldInteractions
    {
        private static readonly Dictionary<ushort, SpawnFlags> _createdIndexes = [];
        public static List<FloorTextureInstance> FloorTextures = new();
        public unsafe static void SetName(this ref StructsObject gameObject, string name)
        {
            for (int x = 0; x < name.Length; x++)
            {
                gameObject.Name[x] = (byte)name[x];
            }
            gameObject.Name[name.Length] = 0;
        }
        public static string ToBrioName(this int i)
        {
            string result = i.ToString();

            if (!result.Contains(' '))
                return "Brio " + result;

            return result;
        }
        public unsafe static void CalculateAndSetName(this ref StructsObject gameObject, int index) => gameObject.SetName(index.ToBrioName());
        public unsafe static void CalculateAndSetName(this IGameObject gameObject, int index) => gameObject.Native()->CalculateAndSetName(index);
        private static readonly IObjectTable _objectTable;
        public unsafe static StructsObject* Native(this IGameObject go)
        {
            return (StructsObject*)go.Address;
        }
        private static ICharacter NewQuestNpc([MaybeNullWhen(false)] out ICharacter outCharacter, SpawnFlags flags)
        {
            outCharacter = null;


            unsafe
            {
                var com = ClientObjectManager.Instance();
                uint idCheck = com->CreateBattleCharacter(param: (byte)(flags.HasFlag(SpawnFlags.ReserveCompanionSlot) ? 1 : 0));
                if (idCheck == 0xffffffff)
                {
                    return null;
                }
                ushort newId = (ushort)idCheck;

                _createdIndexes.Add(newId, flags);

                var newObject = com->GetObjectByIndex(newId);
                if (newObject == null) return null;

                var newPlayer = (NativeCharacter*)newObject;

                newObject->CalculateAndSetName(newId); // Brio One etc

                var character = _objectTable.CreateObjectReference((nint)newObject);
                if (character is null or not ICharacter)
                    return null;

                outCharacter = (ICharacter)character;
            }
            

            return outCharacter;
        }

        public static unsafe bool CloneCharacter(ICharacter sourceCharacter, [MaybeNullWhen(false)] out ICharacter outCharacter, SpawnFlags flags = SpawnFlags.Default, bool disableSpawnCompanion = false)
        {
            outCharacter = null;

            CharacterCopyFlags copyFlags = CharacterCopyFlags.WeaponHiding;

            if (flags.HasFlag(SpawnFlags.CopyPosition))
                copyFlags |= CharacterCopyFlags.Position;


            if (NewQuestNpc(out outCharacter, flags) != null)
            {

                var sourceNative = sourceCharacter.Native();
                var targetNative = outCharacter.Native();

                targetNative = sourceNative;

                targetNative->DefaultPosition = sourceNative->DefaultPosition;
                targetNative->Position = sourceNative->Position;
                targetNative->Rotation = sourceNative->Rotation;
                targetNative->DefaultRotation = sourceNative->DefaultRotation;  

                return true;
            }

            return false;
        }
        public static void SpawnTextureOnNpc(IDalamudTextureWrap texture, float worldSize = 2.0f)
        {
            var localPlayer = Plugin.ClientState.LocalPlayer;
            if (localPlayer == null) return;

            var instance = new FloorTextureInstance
            {
                WorldPosition = localPlayer.Position,
                TerritoryType = Plugin.ClientState.TerritoryType,
                Texture = texture,
                WorldSize = worldSize
            };
            FloorTextures.Add(instance);
        }
        public static bool IsNpcVisible(Vector3 playerPos, float playerYaw, Vector3 npcPos, IObjectTable objectTable, float fovRadians = (float)(Math.PI / 2))
        {
            // FOV check
            Vector3 toNpc = npcPos - playerPos;
            Vector2 toNpc2D = new Vector2(toNpc.X, toNpc.Z);
            if (toNpc2D.LengthSquared() < 0.0001f)
                return true;
            toNpc2D = Vector2.Normalize(toNpc2D);
            Vector2 forward = new Vector2(MathF.Sin(playerYaw), MathF.Cos(playerYaw));
            float dot = Vector2.Dot(forward, toNpc2D);
            dot = Math.Clamp(dot, -1f, 1f);
            float angle = MathF.Acos(dot);
            if (angle > fovRadians / 2f)
                return false;

            // On screen check
            if (!Plugin.GameGUI.WorldToScreen(npcPos, out Vector2 screenPos))
                return false;

            // (Optional) Occlusion check
            Vector3 dirToNpc = Vector3.Normalize(toNpc);
            float distanceToNpc = toNpc.Length();
            foreach (var obj in objectTable)
            {
                if (obj is IGameObject gameObj && gameObj.Position != playerPos && gameObj.Position != npcPos)
                {
                    Vector3 toObj = gameObj.Position - playerPos;
                    float proj = Vector3.Dot(toObj, dirToNpc);
                    if (proj > 0 && proj < distanceToNpc)
                    {
                        Vector3 closestPoint = playerPos + dirToNpc * proj;
                        float perpDist = (gameObj.Position - closestPoint).Length();
                        if (perpDist < 0.5f) // Adjust radius as needed
                            return false;
                    }
                }
            }

            return true;
        }
        public static void DrawSquareAbovePlayer(IPlayerCharacter player, PlayerData playerData)
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
        public static void DrawAllFloorTexturesSpinning(float rotationRadians, IDalamudTextureWrap texture)
        {
            uint currentTerritory = Plugin.ClientState.TerritoryType;
            foreach (var instance in FloorTextures)
            {
                if (instance.TerritoryType == currentTerritory)
                {
                    DrawTextureFlatOnFloorSpinning(
                        texture,
                        instance.WorldPosition,
                        instance.WorldSize,
                        rotationRadians
                    );
                }
            }
        }
        public static void DrawAllFloorTextures()
        {
            uint currentTerritory = Plugin.ClientState.TerritoryType;
            foreach (var instance in FloorTextures)
            {
                if (instance.TerritoryType == currentTerritory)
                {
                    DrawTextureFlatOnFloor(instance.Texture, instance.WorldPosition, instance.WorldSize);
                }
            }
        }
        public static void DrawTextureFlatOnFloor(IDalamudTextureWrap texture, Vector3 centerWorldPos, float worldSize = 2.0f)
        {
            float halfSize = worldSize / 2f;
            Vector3[] worldCorners = new Vector3[]
            {
        centerWorldPos + new Vector3(-halfSize, 0, -halfSize),
        centerWorldPos + new Vector3( halfSize, 0, -halfSize),
        centerWorldPos + new Vector3( halfSize, 0,  halfSize),
        centerWorldPos + new Vector3(-halfSize, 0,  halfSize),
            };

            Vector2[] screenCorners = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                if (!Plugin.GameGUI.WorldToScreen(worldCorners[i], out screenCorners[i]))
                    return;
            }

            var drawList = ImGui.GetBackgroundDrawList();
            drawList.AddImageQuad(
                texture.Handle,
                screenCorners[0],
                screenCorners[1],
                screenCorners[2],
                screenCorners[3]
            );
        }
        public static void DrawTextureFlatOnFloorSpinning(
      IDalamudTextureWrap texture,
      Vector3 centerWorldPos,
      float worldSize = 2.0f,
      float rotationRadians = 0f)
        {
            // Calculate four corners of a square on the ground (XZ plane), rotated
            float halfSize = worldSize / 2f;
            Vector2[] baseCorners = new Vector2[]
            {
        new Vector2(-halfSize, -halfSize), // bottom left
        new Vector2( halfSize, -halfSize), // bottom right
        new Vector2( halfSize,  halfSize), // top right
        new Vector2(-halfSize,  halfSize), // top left
            };

            Vector3[] worldCorners = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                // Rotate in XZ plane
                float x = baseCorners[i].X;
                float z = baseCorners[i].Y;
                float cos = MathF.Cos(rotationRadians);
                float sin = MathF.Sin(rotationRadians);
                float rx = x * cos - z * sin;
                float rz = x * sin + z * cos;
                worldCorners[i] = centerWorldPos + new Vector3(rx, 0, rz);
            }

            Vector2[] screenCorners = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                if (!Plugin.GameGUI.WorldToScreen(worldCorners[i], out screenCorners[i]))
                    return; // If any corner is off-screen, skip drawing
            }

            var drawList = ImGui.GetBackgroundDrawList();
            drawList.AddImageQuad(
                texture.Handle,
                screenCorners[0], // bottom left
                screenCorners[1], // bottom right
                screenCorners[2], // top right
                screenCorners[3]  // top left
            );
        }
        public static List<IPlayerCharacter> GetPlayersInRange(float range, IPlayerCharacter localPlayer)
        {
            var result = new List<IPlayerCharacter>();
            foreach (var obj in Plugin.ObjectTable)
            {
                if (obj is IPlayerCharacter player && player != localPlayer)
                {
                    float distance = Vector3.Distance(localPlayer.Position, player.Position);
                    if (distance <= range)
                    {
                        result.Add(player);
                    }
                }
            }
            return result;
        }
        public static void DrawTextureOnFloor(IDalamudTextureWrap texture, Vector3 worldPosition, float worldSize = 2.0f)
        {
            // Project the world position to screen coordinates
            if (!Plugin.GameGUI.WorldToScreen(worldPosition, out Vector2 screenPos))
                return;

            var localPlayer = Plugin.ClientState.LocalPlayer;
            if (localPlayer == null)
                return;

            // Calculate distance from player to texture position
            float distance = Vector3.Distance(localPlayer.Position, worldPosition);

            // Perspective scaling: closer = bigger, farther = smaller
            // You can tweak the scaling formula as needed
            float scale = MathF.Max(0.5f, 8.0f / (distance + 2.0f));
            float textureWidth = texture.Width * scale;
            float textureHeight = texture.Height * scale;

            // Optionally, rotate the texture so it always faces up (flat on the ground)
            // ImGui does not support texture rotation directly, but you can simulate it by drawing a quad
            // For simplicity, we'll just draw the texture centered at the screen position

            var drawList = ImGui.GetBackgroundDrawList();
            Vector2 topLeft = new Vector2(screenPos.X - textureWidth / 2, screenPos.Y - textureHeight / 2);
            Vector2 bottomRight = new Vector2(screenPos.X + textureWidth / 2, screenPos.Y + textureHeight / 2);

            drawList.AddImage(texture.Handle, topLeft, bottomRight);
        }
        /*
        public static void DrawSquaresOnPlayersInRange(float range, IPlayerCharacter localPlayer)
        {
            var players = GetPlayersInRange(range, localPlayer);
            foreach (var player in players)
            {
                var playerData = GetConnectedPlayer(player.Name.ToString(), player.HomeWorld.Value.Name.ToString());
                if (playerData != null)
                {
                    DrawSquareAbovePlayer(player, playerData);
                }
            }
        }
        */
     
    }
}
