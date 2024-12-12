using AbsoluteRoleplay.Windows.Profiles.ProfileTabs;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Lumina.Data.Files;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AbsoluteRoleplay.Helpers
{
    internal class WindowOperations
    {
        public static Plugin plugin;
        public static int maxIconId = 300000;
        public static readonly List<(uint IconId, IDalamudTextureWrap Icon)> loadedIcons = new();
        public static IDalamudTextureWrap? selectedIcon;
        public static readonly ITextureProvider textureProvider;
        public static int? selectedIconId = null; // To store the selected icon ID
        internal static bool iconsLoaded = false;
        public static bool isLoadingIcons = false;
        public static uint nextIconToLoad = 0; // Tracks the next icon to load
        public static int currentPage = 0;
        public static int iconsLoadedPerFrame = 10;
        private const int iconsPerPage = 100;

        public static Dictionary<string, List<(uint IconId, IDalamudTextureWrap Texture)>> categorizedIcons =
                new Dictionary<string, List<(uint, IDalamudTextureWrap)>>
                {
                    { "Items", new List<(uint, IDalamudTextureWrap)>() },
                    { "Spells", new List<(uint, IDalamudTextureWrap)>() },
                    { "Actions", new List<(uint, IDalamudTextureWrap)>() },
                    { "Emotes", new List<(uint, IDalamudTextureWrap)>() },
                };
        public static string currentCategory = "Items";

        public static void DrawTooltipInfo(IGameObject? mouseOverTarget)
        {
            if (plugin.Configuration.tooltip_Enabled && !Plugin.ClientState.IsGPosing)
            {
                if (mouseOverTarget.ObjectKind == ObjectKind.Player)
                {

                    //Hitboxes.DrawTooltipHitbox(player, GameGUI, 0.200f);
                    IPlayerCharacter playerTarget = (IPlayerCharacter)mouseOverTarget;
                    Plugin.tooltipLoaded = false;
                    DataSender.SendRequestPlayerTooltip(playerTarget.Name.TextValue.ToString(), playerTarget.HomeWorld.Value.Name.ToString());
                }
            }

        }
        public Vector2 CalculateTooltipPos()
        {
            float positionX = plugin.Configuration.hPos;
            float positionY = plugin.Configuration.vPos;
            bool correctedPos = false;
            if (positionX > plugin.screenWidth - ImGui.GetWindowSize().X)
            {
                positionX = plugin.screenWidth - ImGui.GetWindowSize().X;
            }
            if (positionY > plugin.screenHeight - ImGui.GetWindowSize().Y)
            {
                positionY = plugin.screenHeight - ImGui.GetWindowSize().Y;
            }
            return new Vector2(positionX, positionY);
        }
        public static void LoadIconsLazy(Plugin plugin)
        {
            int loadedThisFrame = 0;

            while (nextIconToLoad <= maxIconId && loadedThisFrame < iconsLoadedPerFrame)
            {
                try
                {
                    var icon = Plugin.DataManager.GameData.GetIcon((uint)nextIconToLoad);
                    if (icon != null && !string.IsNullOrEmpty(icon.FilePath))
                    {
                        var texFile = Plugin.DataManager.GetFile<TexFile>(icon.FilePath);
                        var texture = LoadTextureAsync(icon.FilePath).Result;

                        if (texture != null && texture.Width > 0 && texture.Height > 0)
                        {
                            lock (categorizedIcons)
                            {
                                // Categorize icons correctly
                                if (IsItemIcon(nextIconToLoad))
                                {
                                    categorizedIcons["Items"].Add(((uint)nextIconToLoad, texture));
                                    plugin.logger.Debug($"Added icon {nextIconToLoad} to Items.");
                                }
                                else if (IsSpellIcon(nextIconToLoad))
                                {
                                    categorizedIcons["Spells"].Add(((uint)nextIconToLoad, texture));
                                    plugin.logger.Debug($"Added icon {nextIconToLoad} to Spells.");
                                }
                                else if (IsSpellIcon(nextIconToLoad))
                                {
                                    categorizedIcons["Actions"].Add(((uint)nextIconToLoad, texture));
                                    plugin.logger.Debug($"Added icon {nextIconToLoad} to Actions.");
                                }
                                else if (IsEmoteIcon(nextIconToLoad))
                                {
                                    categorizedIcons["Emotes"].Add(((uint)nextIconToLoad, texture));
                                    plugin.logger.Debug($"Added icon {nextIconToLoad} to Emotes.");
                                }
                                else
                                {
                                    plugin.logger.Debug($"Icon {nextIconToLoad} does not match any category.");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    plugin.logger.Error($"Error loading icon {nextIconToLoad}");
                }

                nextIconToLoad++;
                loadedThisFrame++;
            }

            if (nextIconToLoad > maxIconId)
            {
                isLoadingIcons = false;
                plugin.logger.Debug("Finished loading all icons.");
            }
        }


        // Helper methods to determine type
        public static bool IsItemIcon(uint iconId)
        {
            var itemSheet = Plugin.DataManager.Excel.GetSheet<Item>();
            return itemSheet?.FirstOrDefault(item => item.Icon == iconId) != null;
        }

        public static bool IsSpellIcon(uint iconId)
        {
            var actionSheet = Plugin.DataManager.Excel.GetSheet<Lumina.Excel.Sheets.Action>();
            return actionSheet?.FirstOrDefault(action => action.Icon == iconId) != null;
        }


        public static bool IsEmoteIcon(uint iconId)
        {
            var emoteSheet = Plugin.DataManager.Excel.GetSheet<Emote>();
            return emoteSheet?.FirstOrDefault(emote => emote.Icon == iconId) != null;
        }


        public static async Task<IDalamudTextureWrap?> LoadTextureAsync(string gameTexturePath)
        {
            try
            {
                // Ensure the path is not null or empty
                if (string.IsNullOrEmpty(gameTexturePath))
                {
                    plugin.logger.Debug("Game texture path is null or empty.");
                    return null;
                }

                // Attempt to load the texture
                var texFile = Plugin.DataManager.GetFile<TexFile>(gameTexturePath);
                if (texFile == null)
                {
                    plugin.logger.Debug($"TexFile not found for path: {gameTexturePath}");
                    return null;
                }

                // Create and return the texture
                var texture = Plugin.TextureProvider.CreateFromTexFile(texFile);
                return texture;
            }
            catch (Exception ex)
            {
                plugin.logger.Error( $"Failed to load texture from path: {gameTexturePath + " " + ex}");
                return null;
            }
        }
        public static void RenderIcons(Plugin plugin)
        {
            // Begin rendering the tab bar for categories
           
            // Check if the selected category has icons
            if (!categorizedIcons.ContainsKey(currentCategory) || categorizedIcons[currentCategory].Count == 0)
            {
                ImGui.Text($"No icons available for category: {currentCategory}");
                return;
            }

            // Render icons for the current category and page
            var icons = categorizedIcons[currentCategory];
            int startIndex = currentPage * iconsPerPage;
            int endIndex = Math.Min(startIndex + iconsPerPage, icons.Count);

            const int iconsPerRow = 10;
            float iconSize = 40f;
            int count = 0;

            for (int i = startIndex; i < endIndex; i++)
            {
                    
                var (iconId, texture) = icons[i];

                ImGui.PushID((int)iconId);
                if (ImGui.ImageButton(texture.ImGuiHandle, new Vector2(iconSize, iconSize)))
                {
                    selectedIcon = texture; // Handle icon click
                    InventoryTab.createItemIconID = iconId;
                }
                ImGui.PopID();

                count++;
                if (count % iconsPerRow != 0)
                {
                    ImGui.SameLine();
                }
            }

            // Pagination controls
            if (currentPage > 0 && ImGui.Button("Back"))
            {
                currentPage--;
            }
            if (currentPage > 0)
            {
                ImGui.SameLine();
            }
            if (endIndex < icons.Count && ImGui.Button("Next"))
            {
                currentPage++;
            }

            // Display the selected icon, if any
            if (selectedIcon != null)
            {
                ImGui.Text("Selected Icon:");
                ImGui.SameLine();
                if (ImGui.Button("Set Icon"))
                {
                    InventoryTab.icon = selectedIcon;
                    InventoryTab.isIconBrowserOpen = false;
                }
                ImGui.Image(selectedIcon.ImGuiHandle, new Vector2(iconSize, iconSize));
            }
        }

        private static Dictionary<int, IDalamudTextureWrap> loadedTextures = new();

        public static async Task<IDalamudTextureWrap> RenderIconAsync(Plugin plugin, int iconID)
        {
            if (loadedTextures.ContainsKey(iconID))
            {
                return loadedTextures[iconID];
            }

            try
            {
                if (iconID <= 0)
                {
                    return UI.UICommonImage(UI.CommonImageTypes.blank);
                }

                var icon = Plugin.DataManager.GameData.GetIcon((uint)iconID);
                if (icon != null && !string.IsNullOrEmpty(icon.FilePath))
                {
                    var texture = await LoadTextureAsync(icon.FilePath);
                    if (texture != null)
                    {
                        loadedTextures[iconID] = texture;
                        return texture;
                    }
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"RenderIconAsync: Failed to load icon for ID {iconID}. Exception: {ex}");
            }

            return UI.UICommonImage(UI.CommonImageTypes.blank);
        }

        public static IDalamudTextureWrap RenderIcon(int iconId, Plugin plugin)
        {
            try
            {
                // Check if the icon exists in the categorized icons
                if (!categorizedIcons.ContainsKey(currentCategory))
                {
                    plugin.logger.Error($"Category {currentCategory} does not exist for icon ID {iconId}.");
                    return UI.UICommonImage(UI.CommonImageTypes.blank); // Fallback texture
                }

                // Find the icon within the current category
                var icons = categorizedIcons[currentCategory];
                foreach (var (storedIconId, texture) in icons)
                {
                    if (storedIconId == iconId)
                    {
                        return texture; // Return the matching texture
                    }
                }

                // If the icon ID is not found, log and return a fallback
                plugin.logger.Error($"Icon ID {iconId} not found in category {currentCategory}.");
                return UI.UICommonImage(UI.CommonImageTypes.blank); // Fallback texture
            }
            catch (Exception ex)
            {
                // Log any errors and return a fallback texture
                plugin.logger.Error($"Error rendering icon ID {iconId}: {ex.Message}\n{ex.StackTrace}");
                return UI.UICommonImage(UI.CommonImageTypes.blank);
            }
        }

        /* public static void RenderIcons()
         {
             const int iconsPerPage = 100; // Adjust based on window size
             const int iconsPerRow = 10;
             float iconSize = 40f;

             int startIndex = currentPage * iconsPerPage;
             int endIndex = Math.Min(startIndex + iconsPerPage, loadedIcons.Count);

             int count = 0;
             for (int i = startIndex; i < endIndex; i++)
             {
                 var (iconId, texture) = loadedIcons[i];
                 if (texture == null || texture.Width == 0 || texture.Height == 0)
                 {
                     continue;
                 }

                 ImGui.PushID((int)iconId);

                 if (ImGui.ImageButton(texture.ImGuiHandle, new Vector2(iconSize, iconSize)))
                 {
                     selectedIcon = texture;
                 }

                 ImGui.PopID();
                 count++;

                 if (count % iconsPerRow != 0)
                 {
                     ImGui.SameLine();
                 }
             }

             // Pagination controls
             if (ImGui.Button("Previous") && currentPage > 0)
             {
                 currentPage--;
             }
             ImGui.SameLine();
             if (ImGui.Button("Next") && endIndex < loadedIcons.Count)
             {
                 currentPage++;
             }
         }*/




    }
}
