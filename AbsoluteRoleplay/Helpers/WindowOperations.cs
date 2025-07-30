using AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using ImGuiNET;
using InventoryTab;
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
        public static int? selectedTreeIconId = null;
        public static Plugin plugin;
        public static int maxIconId = 300000;
        public static IDalamudTextureWrap? selectedIcon;
        public static readonly ITextureProvider textureProvider;
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

        public static Dictionary<string, List<(uint IconId, IDalamudTextureWrap Texture)>> categorizedStatusIcons =
              new Dictionary<string, List<(uint, IDalamudTextureWrap)>>
              {
                    { "Buffs", new List<(uint, IDalamudTextureWrap)>() },
                    { "Debuffs", new List<(uint, IDalamudTextureWrap)>() },
              };
        public static string currentCategory = "Items";
        public static string currentStatusCategory = "Buffs";

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
        public static async void LoadIconsLazy(Plugin plugin)
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
                        var texture = await LoadTextureAsync(icon.FilePath);

                        if (texture != null && texture.Width > 0 && texture.Height > 0)
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
                catch (Exception ex)
                {
                    plugin.logger.Error($"Error loading icon {nextIconToLoad}");
                }

                nextIconToLoad++;
                loadedThisFrame++;
            }

            if (nextIconToLoad > maxIconId)
            {
                iconsLoaded = true;
                isLoadingIcons = false;
                plugin.logger.Debug("Finished loading all icons.");
            }
        }


        // Helper methods to determine type
        public static bool IsItemIcon(uint iconId)
        {
            var itemSheet = Plugin.DataManager.Excel.GetSheet<Lumina.Excel.Sheets.Item>();
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


        public static bool IsStatusIcon(uint iconId)
        {
            var emoteSheet = Plugin.DataManager.Excel.GetSheet<Status>();
            return emoteSheet?.FirstOrDefault(emote => emote.Icon == iconId) != null;
        }
        
    
        public static async void LoadStatusIconsLazy(Plugin plugin)
        {
            int loadedThisFrame = 0;

            while (nextIconToLoad <= maxIconId && loadedThisFrame < iconsLoadedPerFrame)
            {
                try
                {
                    // Get the status row
                    var statusIcon = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>()?.GetRow((uint)nextIconToLoad);

                    if (statusIcon != null)
                    {
                        var statusIconID = statusIcon.Value.Icon;

                        // Ensure that the statusIconID is valid before attempting to load the icon
                        if (statusIconID > 0)
                        {
                            var icon = Plugin.DataManager.GameData.GetIcon(statusIconID);

                            // Ensure icon is valid and has a file path
                            if (icon != null && !string.IsNullOrEmpty(icon.FilePath))
                            {
                                // Log more info to debug potential issues with the icon data
                                plugin.logger.Debug($"Status ID {statusIcon.Value.RowId} has icon {statusIconID} with path: {icon.FilePath}");

                                // Load the texture asynchronously
                                var texture =  await LoadTextureAsync(icon.FilePath);

                                if (texture != null && texture.Width > 0 && texture.Height > 0)
                                {                                   
                                    // Add to categorized icons (e.g., Buffs or Debuffs)
                                    categorizedStatusIcons["Buffs"].Add(((uint)nextIconToLoad, texture));
                                    plugin.logger.Debug($"Added status icon {nextIconToLoad} to Buffs.");
                                }
                                else
                                {
                                    plugin.logger.Debug($"Failed to load texture for status icon {statusIconID}. File path was invalid or texture loading failed.");
                                }
                            }
                            else
                            {
                                plugin.logger.Debug($"Icon not found for Status ID {statusIcon.Value.RowId} with Icon ID {statusIconID}. Skipping.");
                            }
                        }
                        else
                        {
                            plugin.logger.Debug($"Invalid status icon ID {statusIconID} at Status ID {statusIcon.Value.RowId}. Skipping.");
                        }
                    }
                    else
                    {
                        plugin.logger.Debug($"No valid status row found for icon ID {nextIconToLoad}. Skipping.");
                    }
                }
                catch (Exception ex)
                {
                    return;
                }

                nextIconToLoad++;
                loadedThisFrame++;
            }

            if (nextIconToLoad > maxIconId)
            {
                iconsLoaded = true;
                isLoadingIcons = false;
                plugin.logger.Debug("Finished loading all status icons.");
            }
        }


        public static async Task<IDalamudTextureWrap?> LoadTextureAsync(string gameTexturePath)
        {
            try
            {
                if (string.IsNullOrEmpty(gameTexturePath))
                {
                    plugin.logger.Debug("Game texture path is null or empty.");
                    return null;
                }

                // Attempt to load the texture file
                var texFile = Plugin.DataManager.GetFile<TexFile>(gameTexturePath);
                if (texFile == null)
                {
                    plugin.logger.Debug($"TexFile not found for path: {gameTexturePath}");
                    return null;
                }

                plugin.logger.Debug($"Successfully loaded TexFile for path: {gameTexturePath}");

                // Create and return the texture
                var texture = Plugin.TextureProvider.CreateFromTexFile(texFile);
                if (texture == null || texture.ImGuiHandle == IntPtr.Zero)
                {
                    texture = UI.UICommonImage(UI.CommonImageTypes.blank);
                }
                return texture;
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"Failed to load texture from path: {gameTexturePath}. Exception: {ex}");
                return null;
            }
        }


        public static void RenderIcons(Plugin plugin, bool inventory, bool tree, IconElement icon, Relationship rel, ref IDalamudTextureWrap iconImage)
        {
            if (categorizedIcons == null)
            {
                ImGui.Text("Error: categorizedIcons is null!");
                return;
            }

            if (!categorizedIcons.ContainsKey(currentCategory) && categorizedIcons[currentCategory] == null && categorizedIcons[currentCategory].Count == 0)
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
            int selectedIconId = 0;
            for (int i = startIndex; i < endIndex; i++)
            {
                if (icons[i].Texture == null)
                {
                    ImGui.Text($"Error: icons[{i}] is null!");
                    continue;
                }

                var (iconId, texture) = icons[i];

                if (texture != null && texture.ImGuiHandle != IntPtr.Zero)
                { 
                    ImGui.PushID((int)iconId);
                    if (ImGui.ImageButton(texture.ImGuiHandle, new Vector2(iconSize, iconSize)))
                    {
                        selectedIcon = texture;
                        InvTab.createItemIconID = iconId;
                        selectedTreeIconId = (int)iconId; // Persist selection

                        if (tree && rel != null)
                        {
                            rel.IconID = selectedTreeIconId.Value;
                            iconImage = selectedIcon;
                        }
                    }
                    ImGui.PopID();
                }

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
            if (selectedIcon != null && selectedIcon.ImGuiHandle != IntPtr.Zero)
            {
                ImGui.Text("Selected Icon:");
                ImGui.SameLine();
                if (ImGui.Button("Set Icon"))
                {
                    SetIcon = true;
                }
                if (SetIcon)
                {
                    if (tree && selectedTreeIconId.HasValue && rel != null)
                    {
                        rel.IconID = selectedTreeIconId.Value; // Always use the persistent value

                        rel.IconTexture = selectedIcon;
                        iconImage = selectedIcon;
                    }
                    if (inventory)
                    {
                        InvTab.icon = selectedIcon;
                        InvTab.isIconBrowserOpen = false;
                        if (icon != null)
                            icon.iconID = selectedIconId; // Only set if icon is not null
                    }
                    else if (!inventory && !tree)
                    {
                        if (icon == null)
                        {
                            ImGui.Text("Error: icon parameter is null!");
                        }
                        else
                        {
                            icon.icon = selectedIcon;
                            icon.iconID = selectedIconId;
                            icon.modifying = false;
                        }
                    }
                }
                if (selectedIcon != null && selectedIcon.ImGuiHandle != IntPtr.Zero)
                {
                    ImGui.Image(selectedIcon.ImGuiHandle, new Vector2(iconSize, iconSize));
                }
            }
        }

        public static void RenderStatusIcons(Plugin plugin, IconElement icon, trait personality = null)
        {
            // Begin rendering the tab bar for status categories (you can adjust this as needed)
            ImGui.Text($"Current Category: {currentStatusCategory}");

            // Check if the selected category has status icons
            if (!categorizedStatusIcons.ContainsKey(currentStatusCategory) || categorizedStatusIcons[currentStatusCategory].Count == 0)
            {
                ImGui.Text($"No status icons available for category: {currentStatusCategory}");
                return;
            }

            // Render status icons for the current category and page
            var icons = categorizedStatusIcons[currentStatusCategory];
            int startIndex = currentPage * iconsPerPage;
            int endIndex = Math.Min(startIndex + iconsPerPage, icons.Count);

            const int iconsPerRow = 10;
            int count = 0;
            for (int i = startIndex; i < endIndex; i++)
            {
                var (statusIconId, texture) = icons[i];

                float iconHeight = ImGui.GetIO().FontGlobalScale * texture.Height;
                float iconWidth = ImGui.GetIO().FontGlobalScale * texture.Width;
                if(texture != null && texture.ImGuiHandle != IntPtr.Zero)
                {
                    ImGui.PushID((int)statusIconId);

                    if (ImGui.ImageButton(texture.ImGuiHandle, new Vector2(iconWidth, iconHeight)))
                    {
                        selectedStatusIcon = texture; // Handle status icon click
                        selectedStatusIconID = (int)statusIconId;
                    }
                    ImGui.PopID();
                }

                count++;
                if (count % iconsPerRow != 0)
                {
                    ImGui.SameLine();
                }
            }

            // Pagination controls for status icons
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

            // Display the selected status icon, if any
            if (selectedStatusIcon != null)
            {
                ImGui.Text("Selected Status Icon:");
                ImGui.SameLine();
                if (ImGui.Button("Set Icon"))
                {
                    if (personality != null)
                    {
                        if(personality.icon != null)
                        {
                            SafeDispose(personality.icon.icon);
                            personality.icon.icon = null;
                            personality.icon = null;
                        }
                        personality.icon.icon = selectedStatusIcon;
                        personality.modifying = false;
                        personality.iconID = selectedStatusIconID;
                    }
                }
                
                float height = ImGui.GetIO().FontGlobalScale * selectedStatusIcon.Height;
                float width = ImGui.GetIO().FontGlobalScale * selectedStatusIcon.Width;
                if(selectedStatusIcon != null && selectedStatusIcon.ImGuiHandle != IntPtr.Zero)
                {
                    ImGui.Image(selectedStatusIcon.ImGuiHandle, new Vector2(width, height));
                }
            }
        }

        public static async Task<IDalamudTextureWrap> RenderStatusIconAsync(Plugin plugin, int statusEffectID)
        {
            if (loadedStatusEffectTextures.ContainsKey(statusEffectID))
            {
                return loadedStatusEffectTextures[statusEffectID];
            }

            try
            {
                if (statusEffectID <= 0)
                {
                    return UI.UICommonImage(UI.CommonImageTypes.blank);
                }

                // Get the status effect from the Excel sheet
                var statusEffect = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>()?.GetRow((uint)statusEffectID);

                if (statusEffect != null)
                {
                    uint statusIconID = (uint)statusEffect.Value.Icon;
                    if (statusIconID > 0)
                    {
                        var icon = Plugin.DataManager.GameData.GetIcon(statusIconID);
                        if (icon != null && !string.IsNullOrEmpty(icon.FilePath))
                        {
                            var texture = await LoadTextureAsync(icon.FilePath);
                            if (texture != null && texture.ImGuiHandle != IntPtr.Zero)
                            {
                                loadedStatusEffectTextures[statusEffectID] = texture;
                                return texture;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"RenderStatusIconAsync: Failed to load status effect icon for ID {statusEffectID}. Exception: {ex}");
            }

            return UI.UICommonImage(UI.CommonImageTypes.blank);
        }
    
        public static void SafeDispose(object obj)
        {
            if (obj is IDisposable disposable)
            {
                try
                {
                    disposable?.Dispose();
                    disposable = null; // Clear reference to help GC
                }
                catch (Exception ex)
                {
                    // Optionally log the exception, or ignore
                    Example: Plugin.plugin?.logger?.Error($"Dispose failed: {ex}");
                }
            }
            // If obj is null or not IDisposable, do nothing (safe)
        }
        public static Dictionary<int, IDalamudTextureWrap> loadedTextures = new();

        public static async Task<IDalamudTextureWrap> RenderIconAsync(Plugin plugin, int iconID)
        {
            if (loadedTextures.ContainsKey(iconID))
            {
                var existing = loadedTextures[iconID];
                if (existing == null || existing.ImGuiHandle == IntPtr.Zero)
                {
                    loadedTextures.Remove(iconID);
                }
                else
                {
                    return existing;
                }
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
                    if (texture != null && texture.ImGuiHandle != IntPtr.Zero)
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


        public static Dictionary<int, IDalamudTextureWrap> loadedStatusEffectTextures = new();
        public static IDalamudTextureWrap selectedStatusIcon;

        public static int selectedStatusIconID { get; private set; }
        public static bool SetIcon { get; set; }

        public static async Task<IDalamudTextureWrap> RenderStatusEffectIconAsync(Plugin plugin, int statusEffectID)
        {
            if (loadedStatusEffectTextures.ContainsKey(statusEffectID))
            {
                return loadedStatusEffectTextures[statusEffectID];
            }

            try
            {
                if (statusEffectID <= 0)
                {
                    plugin.logger.Debug("Invalid status effect ID, returning blank icon.");
                    return UI.UICommonImage(UI.CommonImageTypes.blank);
                }

                var statusEffect = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>()?.GetRow((uint)statusEffectID);
                if (statusEffect == null )
                {
                    plugin.logger.Debug($"No status effect found for ID {statusEffectID}, returning blank icon.");
                    return UI.UICommonImage(UI.CommonImageTypes.blank);
                }

                plugin.logger.Debug($"Loading status effect icon for ID {statusEffectID} with Icon ID: {statusEffect.Value.Icon}");

                var statusIconID = (uint)statusEffect.Value.Icon;
                var icon = Plugin.DataManager.GameData.GetIcon(statusIconID);

                if (icon != null && !string.IsNullOrEmpty(icon.FilePath))
                {
                    plugin.logger.Debug($"Loading icon from path: {icon.FilePath}");
                    var texture = await LoadTextureAsync(icon.FilePath);
                    if (texture != null && texture.ImGuiHandle != IntPtr.Zero)
                    {
                        loadedStatusEffectTextures[(int)statusIconID] = texture;
                        return texture;
                    }
                    else
                    {
                        plugin.logger.Debug($"Failed to load texture for status effect icon {statusIconID}. FilePath: {icon.FilePath}");
                    }
                }
                else
                {
                    plugin.logger.Debug($"Invalid icon data for status effect ID {statusEffectID}. Icon ID: {statusIconID}");
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"RenderStatusEffectIconAsync: Failed to load status effect icon for ID {statusEffectID}. Exception: {ex.Message}");
            }

            return UI.UICommonImage(UI.CommonImageTypes.blank);
        }





    }
}
