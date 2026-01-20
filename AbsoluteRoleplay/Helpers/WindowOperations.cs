using AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using InventoryTab;
using Lumina.Data.Files;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using Networking;
using System.Numerics;
namespace AbsoluteRP.Helpers
{
    internal class WindowOperations
    {
        public static int? selectedTreeIconId = null;
        public static int maxIconId = 300000;
        public static IDalamudTextureWrap? selectedIcon;
        public static readonly ITextureProvider textureProvider;
        internal static bool iconsLoaded = false;
        public static bool isLoadingIcons = false;
        public static uint nextIconToLoad = 0; // Tracks the next icon to load
        public static int currentPage = 0;
        public static int iconsLoadedPerFrame = 10;
        private const int iconsPerPage = 100;
        public static string iconSearchFilter = string.Empty;
        public static Dictionary<uint, string> IconIdToAbilityName = new(); 
        private static List<uint> itemIconIds = new();
        private static List<uint> actionIconIds = new();
        private static List<uint> emoteIconIds = new();
        private static int itemIconLoadIndex = 0;
        private static int actionIconLoadIndex = 0;
        private static int emoteIconLoadIndex = 0;
        private static int spellIconLoadIndex = 0;
        public static List<uint> LoadedIconIDs = new List<uint>();
        public static string customCategoryName = string.Empty;
        public static Dictionary<string, List<(uint IconId, IDalamudTextureWrap Texture)>> categorizedIcons =
                new Dictionary<string, List<(uint, IDalamudTextureWrap)>>
                {
                    { "Items", new List<(uint, IDalamudTextureWrap)>() },
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

        // Status icon loading - use actual Status sheet entries instead of arbitrary ID range
        private static List<uint> statusIconIds = new();
        private static int statusIconLoadIndex = 0;
        internal static bool statusIconsLoaded = false;
        public static bool isLoadingStatusIcons = false;
        public static Dictionary<uint, string> StatusIdToName = new();

        /// <summary>
        /// Resets the status icon loading state, allowing icons to be reloaded.
        /// Call this if you need to refresh the status icon cache.
        /// </summary>
        public static void ResetStatusIconLoading()
        {
            statusIconIds.Clear();
            statusIconLoadIndex = 0;
            statusIconsLoaded = false;
            isLoadingStatusIcons = false;
            StatusIdToName.Clear();
            categorizedStatusIcons["Buffs"].Clear();
            categorizedStatusIcons["Debuffs"].Clear();
            statusIconPage = 0;
            Plugin.PluginLog.Debug("Status icon loading state has been reset.");
        }

        public static void DrawTooltipInfo(IGameObject? mouseOverTarget)
        {
            if (Plugin.plugin.Configuration.tooltip_Enabled && !Plugin.ClientState.IsGPosing)
            {
                if (mouseOverTarget.ObjectKind == ObjectKind.Player)
                {

                    //Hitboxes.DrawTooltipHitbox(player, GameGUI, 0.200f);
                    IPlayerCharacter playerTarget = (IPlayerCharacter)mouseOverTarget;
                    Plugin.tooltipLoaded = false;
                    DataSender.SendRequestPlayerTooltip(Plugin.character, playerTarget.Name.TextValue.ToString(), playerTarget.HomeWorld.Value.Name.ToString());
                }
            }
        }
    
        public static void BuildCategoryIconIdLists()
        {
            itemIconIds.Clear();
            actionIconIds.Clear();
            emoteIconIds.Clear();
            var itemSheet = Plugin.DataManager.Excel.GetSheet<Lumina.Excel.Sheets.Item>();
            if (itemSheet != null)
                itemIconIds.AddRange(itemSheet.Where(i => i.Icon > 0).Select(i => (uint)i.Icon));


            var actionSheet = Plugin.DataManager.Excel.GetSheet<Lumina.Excel.Sheets.Action>();
            if (actionSheet != null)
                actionIconIds.AddRange(actionSheet.Where(a => a.Icon > 0).Select(a => (uint)a.Icon));

            var emoteSheet = Plugin.DataManager.Excel.GetSheet<Emote>();
            if (emoteSheet != null)
                emoteIconIds.AddRange(emoteSheet.Where(e => e.Icon > 0).Select(e => (uint)e.Icon));

        }
        public static void BuildIconAbilityNameMap()
        {
            if (IconIdToAbilityName.Count > 0)
                return; // Already built

            // Actions
            var actionSheet = Plugin.DataManager.Excel.GetSheet<Lumina.Excel.Sheets.Action>();
            // Spells (if you have a separate sheet, e.g., Spell or Action for spells)
            var spellSheet = Plugin.DataManager.Excel.GetSheet<Lumina.Excel.Sheets.Action>();
            if (spellSheet != null)
            {
                foreach (var spell in spellSheet)
                {
                    string spellName = null;
                    var nameProp = spell.Name;
                    var textValueProp = nameProp.GetType().GetProperty("TextValue");
                    if (textValueProp != null)
                        spellName = textValueProp.GetValue(nameProp) as string;
                    if (string.IsNullOrEmpty(spellName))
                    {
                        var valueProp = nameProp.GetType().GetProperty("Value");
                        if (valueProp != null)
                            spellName = valueProp.GetValue(nameProp) as string;
                    }
                    if (string.IsNullOrEmpty(spellName))
                        spellName = nameProp.ToString();

                    if (spell.Icon > 0 && !string.IsNullOrEmpty(spellName))
                        IconIdToAbilityName[(uint)spell.Icon] = spellName;
                }
            }

            // Items
            var itemSheet = Plugin.DataManager.Excel.GetSheet<Lumina.Excel.Sheets.Item>();
            if (itemSheet != null)
            {
                foreach (var item in itemSheet)
                {
                    string itemName = null;
                    var nameProp = item.Name;
                    var textValueProp = nameProp.GetType().GetProperty("TextValue");
                    if (textValueProp != null)
                        itemName = textValueProp.GetValue(nameProp) as string;
                    if (string.IsNullOrEmpty(itemName))
                    {
                        var valueProp = nameProp.GetType().GetProperty("Value");
                        if (valueProp != null)
                            itemName = valueProp.GetValue(nameProp) as string;
                    }
                    if (string.IsNullOrEmpty(itemName))
                        itemName = nameProp.ToString();

                    if (item.Icon > 0 && !string.IsNullOrEmpty(itemName))
                        IconIdToAbilityName[(uint)item.Icon] = itemName;
                }
            }

            // Spells (Actions again, but you may want to add other sheets if needed)

            // Emotes
            var emoteSheet = Plugin.DataManager.Excel.GetSheet<Emote>();
            if (emoteSheet != null)
            {
                foreach (var emote in emoteSheet)
                {
                    string emoteName = null;
                    var nameProp = emote.Name;
                    var textValueProp = nameProp.GetType().GetProperty("TextValue");
                    if (textValueProp != null)
                        emoteName = textValueProp.GetValue(nameProp) as string;
                    if (string.IsNullOrEmpty(emoteName))
                    {
                        var valueProp = nameProp.GetType().GetProperty("Value");
                        if (valueProp != null)
                            emoteName = valueProp.GetValue(nameProp) as string;
                    }
                    if (string.IsNullOrEmpty(emoteName))
                        emoteName = nameProp.ToString();

                    if (emote.Icon > 0 && !string.IsNullOrEmpty(emoteName))
                        IconIdToAbilityName[(uint)emote.Icon] = emoteName;
                }
            }
        }
        public static async void LoadIconsLazy(Plugin Plugin)
        {
            if (iconsLoaded || isLoadingIcons)
                return;

            isLoadingIcons = true;
            BuildIconAbilityNameMap();
            BuildCategoryIconIdLists();

            int loadedThisFrame = 0;

            // Try to load up to iconsLoadedPerFrame icons, round-robin across categories
            while (loadedThisFrame < iconsLoadedPerFrame)
            {
                bool loadedAny = false;

                if (itemIconLoadIndex < itemIconIds.Count)
                {
                    var iconId = itemIconIds[itemIconLoadIndex++];
                    var icon = Plugin.DataManager.GameData.GetIcon(iconId);


                    var existingIconID = categorizedIcons["Items"].FirstOrDefault(x => x.IconId == iconId);
                    if (icon != null && !string.IsNullOrEmpty(icon.FilePath) && !categorizedIcons["Items"].Contains(existingIconID))
                    {                       
                        var texture = await LoadTextureAsync(icon.FilePath);
                        if (texture != null && texture.Width > 0 && texture.Height > 0)
                            categorizedIcons["Items"].Add((iconId, texture));
                    }
                    loadedThisFrame++;
                    loadedAny = true;
                }

                if (loadedThisFrame < iconsLoadedPerFrame && actionIconLoadIndex < actionIconIds.Count)
                {
                    var iconId = actionIconIds[actionIconLoadIndex++];
                    var icon = Plugin.DataManager.GameData.GetIcon(iconId);


                    var existingIconID = categorizedIcons["Actions"].FirstOrDefault(x => x.IconId == iconId);
                    if (icon != null && !string.IsNullOrEmpty(icon.FilePath) && !categorizedIcons["Actions"].Contains(existingIconID))
                    {
                        var texture = await LoadTextureAsync(icon.FilePath);
                        if (texture != null && texture.Width > 0 && texture.Height > 0)
                            categorizedIcons["Actions"].Add((iconId, texture));
                    }
                    loadedThisFrame++;
                    loadedAny = true;
                }

                if (loadedThisFrame < iconsLoadedPerFrame && emoteIconLoadIndex < emoteIconIds.Count)
                {
                    var iconId = emoteIconIds[emoteIconLoadIndex++];
                    var icon = Plugin.DataManager.GameData.GetIcon(iconId);

                    var existingIconID = categorizedIcons["Emotes"].FirstOrDefault(x => x.IconId == iconId);
                    if (icon != null && !string.IsNullOrEmpty(icon.FilePath) && !categorizedIcons["Emotes"].Contains(existingIconID))
                    {
                        var texture = await LoadTextureAsync(icon.FilePath);
                        if (texture != null && texture.Width > 0 && texture.Height > 0)
                            categorizedIcons["Emotes"].Add((iconId, texture));
                    }
                    loadedThisFrame++;
                    loadedAny = true;
                }
                // If nothing loaded, break to avoid infinite loop
                if (!loadedAny)
                    break;
            }

            // Check if all categories are done
            if (itemIconLoadIndex >= itemIconIds.Count &&
                actionIconLoadIndex >= actionIconIds.Count &&
                emoteIconLoadIndex >= emoteIconIds.Count)
            {
                iconsLoaded = true;
                isLoadingIcons = false;
                Plugin.PluginLog.Debug("Finished loading all icons.");
            }
            else
            {
                isLoadingIcons = false; // Allow next frame to load more
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
        
    
        /// <summary>
        /// Builds the list of valid Status IDs and their names from the Excel sheet.
        /// This should be called once before loading icons.
        /// </summary>
        public static void BuildStatusIconIdList()
        {
            if (statusIconIds.Count > 0)
                return; // Already built

            statusIconIds.Clear();
            StatusIdToName.Clear();

            var statusSheet = Plugin.DataManager.Excel.GetSheet<Lumina.Excel.Sheets.Status>();
            if (statusSheet == null)
            {
                Plugin.PluginLog.Debug("Failed to get Status sheet from Excel data.");
                return;
            }

            foreach (var status in statusSheet)
            {
                // Only add entries with valid icons
                if (status.Icon > 0)
                {
                    statusIconIds.Add(status.RowId);

                    // Get the status name for tooltips/search
                    string statusName = null;
                    try
                    {
                        var nameProp = status.Name;
                        var textValueProp = nameProp.GetType().GetProperty("TextValue");
                        if (textValueProp != null)
                            statusName = textValueProp.GetValue(nameProp) as string;
                        if (string.IsNullOrEmpty(statusName))
                        {
                            var valueProp = nameProp.GetType().GetProperty("Value");
                            if (valueProp != null)
                                statusName = valueProp.GetValue(nameProp) as string;
                        }
                        if (string.IsNullOrEmpty(statusName))
                            statusName = nameProp.ToString();
                    }
                    catch
                    {
                        statusName = $"Status {status.RowId}";
                    }

                    if (!string.IsNullOrEmpty(statusName))
                        StatusIdToName[status.RowId] = statusName;
                }
            }

            Plugin.PluginLog.Debug($"Built status icon list with {statusIconIds.Count} valid entries.");
        }

        public static async void LoadStatusIconsLazy(Plugin Plugin)
        {
            if (statusIconsLoaded || isLoadingStatusIcons)
                return;

            isLoadingStatusIcons = true;

            // Build the list of valid status IDs if not already done
            if (statusIconIds.Count == 0)
            {
                BuildStatusIconIdList();
            }

            int loadedThisFrame = 0;
            const int iconsPerFrame = 20; // Increased from 10 for faster loading

            while (statusIconLoadIndex < statusIconIds.Count && loadedThisFrame < iconsPerFrame)
            {
                try
                {
                    var statusRowId = statusIconIds[statusIconLoadIndex];
                    var statusRow = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>()?.GetRow(statusRowId);

                    if (statusRow != null)
                    {
                        var statusIconID = statusRow.Value.Icon;

                        if (statusIconID > 0)
                        {
                            // Check if already loaded to avoid duplicates
                            var existingIcon = categorizedStatusIcons["Buffs"].FirstOrDefault(x => x.IconId == statusRowId);
                            if (existingIcon.Texture == null)
                            {
                                var icon = Plugin.DataManager.GameData.GetIcon(statusIconID);

                                if (icon != null && !string.IsNullOrEmpty(icon.FilePath))
                                {
                                    var texture = await LoadTextureAsync(icon.FilePath);

                                    if (texture != null && texture.Width > 0 && texture.Height > 0)
                                    {
                                        // Store the Status Row ID (not the icon file ID) for proper lookup later
                                        categorizedStatusIcons["Buffs"].Add((statusRowId, texture));
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log but continue loading other icons
                    Plugin.PluginLog.Debug($"Error loading status icon at index {statusIconLoadIndex}: {ex.Message}");
                }

                statusIconLoadIndex++;
                loadedThisFrame++;
            }

            // Check if all status icons are loaded
            if (statusIconLoadIndex >= statusIconIds.Count)
            {
                statusIconsLoaded = true;
                isLoadingStatusIcons = false;
                Plugin.PluginLog.Debug($"Finished loading all {categorizedStatusIcons["Buffs"].Count} status icons.");
            }
            else
            {
                isLoadingStatusIcons = false; // Allow next frame to load more
            }
        }


        public static async Task<IDalamudTextureWrap?> LoadTextureAsync(string gameTexturePath)
        {
            try
            {
                if (string.IsNullOrEmpty(gameTexturePath))
                {
                    Plugin.PluginLog.Debug("Game texture path is null or empty.");
                    return null;
                }

                // Attempt to load the texture file
                var texFile = Plugin.DataManager.GetFile<TexFile>(gameTexturePath);
                if (texFile == null)
                {
                    Plugin.PluginLog.Debug($"TexFile not found for path: {gameTexturePath}");
                    return null;
                }

                Plugin.PluginLog.Debug($"Successfully loaded TexFile for path: {gameTexturePath}");

                // Create and return the texture
                var texture = Plugin.TextureProvider.CreateFromTexFile(texFile);
                if (texture == null || texture.Handle == IntPtr.Zero)
                {
                    texture = UI.UICommonImage(UI.CommonImageTypes.blank);
                }
                return texture;
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Failed to load texture from path: {gameTexturePath}. Exception: {ex}");
                return null;
            }
        }


        public static void RenderIcons(Plugin Plugin, bool inventory, bool tree, IconElement icon, Relationship rel, ref IDalamudTextureWrap iconImage)
        {
            if (categorizedIcons == null)
            {
                ImGui.Text("Debug: categorizedIcons is null!");
                return;
            }

            // Category selector (tab bar)
            string[] categories = { "Items", "Actions", "Emotes"};
            if (ImGui.BeginTabBar("IconCategories"))
            {
                foreach (var cat in categories)
                {
                    if (ImGui.BeginTabItem(cat))
                    {
                        if (currentCategory != cat)
                        {
                            currentCategory = cat;
                            currentPage = 0; // Reset page when switching category
                        }
                        ImGui.EndTabItem();
                    }
                }
                ImGui.EndTabBar();
            }

            ImGui.InputText("Search Ability Name", ref iconSearchFilter, 100);

            // Get all loaded icons for current category
            var loadedIcons = categorizedIcons.ContainsKey(currentCategory)
                ? categorizedIcons[currentCategory]
                : new List<(uint IconId, IDalamudTextureWrap Texture)>();

            // Filter all loaded icons
            var filteredIcons = loadedIcons
                .Where(pair =>
                    string.IsNullOrEmpty(iconSearchFilter) ||
                    (IconIdToAbilityName.TryGetValue(pair.IconId, out var name) &&
                     !string.IsNullOrEmpty(name) &&
                     name.Contains(iconSearchFilter, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // Show loading indicator if not all icons are loaded
            if (!iconsLoaded)
            {
                ImGui.TextColored(new Vector4(1, 1, 0, 1), "Loading icons... (this will update as icons load)");
            }

            const int iconsPerRow = 10;
            const int iconsPerPage = 50; // 10x5 grid
            float iconSize = 40f;

            // Paginate the filtered icons
            int startIndex = currentPage * iconsPerPage;
            int endIndex = Math.Min(startIndex + iconsPerPage, filteredIcons.Count);

            int count = 0;
            int selectedIconId = 0;

            for (int i = startIndex; i < endIndex; i++)
            {
                if (filteredIcons[i].Texture != null || !LoadedIconIDs.Contains(filteredIcons[i].IconId))
                {

                    var (iconId, texture) = filteredIcons[i];

                if (texture != null && texture.Handle != IntPtr.Zero)
                {
                    ImGui.PushID((int)iconId);
                    bool clicked = ImGui.ImageButton(texture.Handle, new Vector2(iconSize, iconSize));
                    // Tooltip logic
                    if (ImGui.IsItemHovered())
                    {
                        if (IconIdToAbilityName.TryGetValue(iconId, out var name) && !string.IsNullOrEmpty(name))
                        {
                            ImGui.SetTooltip(name);
                        }
                    }
                    if (clicked)
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

                }
                count++;
                if (count % iconsPerRow != 0)
                {
                    ImGui.SameLine();
                }
                else
                {
                    ImGui.NewLine();
                }
            }

            // Pagination controls
            ImGui.Separator();
            if (currentPage > 0 && ImGui.Button("Back"))
            {
                currentPage--;
            }
            ImGui.SameLine();
            ImGui.Text($"Page {currentPage + 1} / {Math.Max(1, (filteredIcons.Count + iconsPerPage - 1) / iconsPerPage)}");
            ImGui.SameLine();
            if (endIndex < filteredIcons.Count && ImGui.Button("Next"))
            {
                currentPage++;
            }

            // Display the selected icon, if any
            if (selectedIcon != null && selectedIcon.Handle != IntPtr.Zero)
            {
                ImGui.Text("Selected Icon:");
                ImGui.SameLine();
                if (tree && selectedTreeIconId.HasValue && rel != null)
                {
                    rel.IconID = selectedTreeIconId.Value; // Always use the persistent value
                    rel.IconTexture = selectedIcon;
                    iconImage = selectedIcon;
                }
                if (inventory)
                {
                    InvTab.icon = selectedIcon;
                    if (icon != null)
                        icon.iconID = selectedIconId; // Only set if icon is not null
                }
                else if (!inventory && !tree)
                {
                    if (icon == null)
                    {
                        ImGui.Text("Debug: icon parameter is null!");
                    }
                    else
                    {
                        icon.icon = selectedIcon;
                        icon.iconID = selectedIconId;
                        icon.modifying = false;
                    }
                }
                if (selectedIcon != null && selectedIcon.Handle != IntPtr.Zero)
                {
                    ImGui.Image(selectedIcon.Handle, new Vector2(iconSize, iconSize));
                }
            }
        }
        public static string statusIconSearchFilter = string.Empty;
        public static int statusIconPage = 0;

        public static void RenderStatusIcons(Plugin Plugin, IconElement icon, trait personality = null)
        {
            // Show loading indicator if icons are still loading
            if (!statusIconsLoaded)
            {
                int loaded = categorizedStatusIcons["Buffs"].Count;
                int total = statusIconIds.Count > 0 ? statusIconIds.Count : 1;
                ImGui.TextColored(new Vector4(1, 1, 0, 1), $"Loading status icons... ({loaded}/{total})");
            }

            // Search filter
            ImGui.InputText("Search Status", ref statusIconSearchFilter, 100);

            // Check if the selected category has status icons
            if (!categorizedStatusIcons.ContainsKey(currentStatusCategory) || categorizedStatusIcons[currentStatusCategory].Count == 0)
            {
                if (!statusIconsLoaded)
                {
                    ImGui.Text("Please wait while icons load...");
                }
                else
                {
                    ImGui.Text($"No status icons available for category: {currentStatusCategory}");
                }
                return;
            }

            // Filter icons based on search
            var allIcons = categorizedStatusIcons[currentStatusCategory];
            var filteredIcons = allIcons
                .Where(pair =>
                    string.IsNullOrEmpty(statusIconSearchFilter) ||
                    (StatusIdToName.TryGetValue(pair.IconId, out var name) &&
                     !string.IsNullOrEmpty(name) &&
                     name.Contains(statusIconSearchFilter, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // Render status icons for the current page
            const int iconsPerPageStatus = 100;
            int startIndex = statusIconPage * iconsPerPageStatus;
            int endIndex = Math.Min(startIndex + iconsPerPageStatus, filteredIcons.Count);

            const int iconsPerRow = 10;
            int count = 0;
            for (int i = startIndex; i < endIndex; i++)
            {
                var (statusIconId, texture) = filteredIcons[i];

                float iconHeight = ImGui.GetIO().FontGlobalScale * texture.Height;
                float iconWidth = ImGui.GetIO().FontGlobalScale * texture.Width;
                if (texture != null && texture.Handle != IntPtr.Zero)
                {
                    ImGui.PushID((int)statusIconId);

                    if (ImGui.ImageButton(texture.Handle, new Vector2(iconWidth, iconHeight)))
                    {
                        selectedStatusIcon = texture;
                        selectedStatusIconID = (int)statusIconId;
                    }

                    // Show tooltip with status name
                    if (ImGui.IsItemHovered())
                    {
                        if (StatusIdToName.TryGetValue(statusIconId, out var name) && !string.IsNullOrEmpty(name))
                        {
                            ImGui.SetTooltip(name);
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

            ImGui.NewLine();

            // Pagination controls
            ImGui.Separator();
            if (statusIconPage > 0 && ImGui.Button("Back"))
            {
                statusIconPage--;
            }
            ImGui.SameLine();
            int totalPages = Math.Max(1, (filteredIcons.Count + iconsPerPageStatus - 1) / iconsPerPageStatus);
            ImGui.Text($"Page {statusIconPage + 1} / {totalPages} ({filteredIcons.Count} icons)");
            ImGui.SameLine();
            if (endIndex < filteredIcons.Count && ImGui.Button("Next"))
            {
                statusIconPage++;
            }

            // Display the selected status icon, if any
            if (selectedStatusIcon != null)
            {
                ImGui.Separator();
                ImGui.Text("Selected Status Icon:");
                ImGui.SameLine();
                if (ImGui.Button("Set Icon"))
                {
                    if (personality != null)
                    {
                        personality.icon.icon = selectedStatusIcon;
                        personality.modifying = false;
                        personality.iconID = selectedStatusIconID;
                    }
                }

                // Show selected icon name
                if (StatusIdToName.TryGetValue((uint)selectedStatusIconID, out var selectedName))
                {
                    ImGui.SameLine();
                    ImGui.Text($"({selectedName})");
                }

                float height = ImGui.GetIO().FontGlobalScale * selectedStatusIcon.Height;
                float width = ImGui.GetIO().FontGlobalScale * selectedStatusIcon.Width;
                if (selectedStatusIcon != null && selectedStatusIcon.Handle != IntPtr.Zero)
                {
                    ImGui.Image(selectedStatusIcon.Handle, new Vector2(width, height));
                }
            }
        }

        public static async Task<IDalamudTextureWrap> RenderStatusIconAsync(Plugin Plugin, int statusEffectID)
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
                            if (texture != null && texture.Handle != IntPtr.Zero)
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
                Plugin.PluginLog.Debug($"RenderStatusIconAsync: Failed to load status effect icon for ID {statusEffectID}. Exception: {ex}");
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
                    Example: Plugin.PluginLog.Debug($"Dispose failed: {ex}");
                }
            }
            // If obj is null or not IDisposable, do nothing (safe)
        }
        public static Dictionary<int, IDalamudTextureWrap> loadedTextures = new();

        public static async Task<IDalamudTextureWrap> RenderIconAsync(Plugin Plugin, int iconID)
        {
            if (loadedTextures.ContainsKey(iconID))
            {
                var existing = loadedTextures[iconID];
                if (existing == null || existing.Handle == IntPtr.Zero)
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
                if (icon == null || string.IsNullOrEmpty(icon.FilePath))
                {
                    Plugin.PluginLog.Debug($"RenderIconAsync: icon metadata missing for id={iconID}");
                    return UI.UICommonImage(UI.CommonImageTypes.blank);
                }

                // Ensure texture creation runs on the game's framework/main thread.
                var tcs = new TaskCompletionSource<IDalamudTextureWrap>(TaskCreationOptions.RunContinuationsAsynchronously);
                try
                {
                    // Schedule synchronous creation on the framework thread
                    Plugin.Framework.RunOnFrameworkThread(() =>
                    {
                        try
                        {
                            var texFile = Plugin.DataManager.GetFile<TexFile>(icon.FilePath);
                            if (texFile == null)
                            {
                                Plugin.PluginLog.Debug($"RenderIconAsync: TexFile not found for path: {icon.FilePath} (id={iconID})");
                                tcs.SetResult(UI.UICommonImage(UI.CommonImageTypes.blank));
                                return;
                            }

                            var tex = Plugin.TextureProvider.CreateFromTexFile(texFile);
                            tcs.SetResult(tex ?? UI.UICommonImage(UI.CommonImageTypes.blank));
                        }
                        catch (Exception exInner)
                        {
                            Plugin.PluginLog.Debug($"RenderIconAsync: exception creating texture on framework thread for id={iconID}: {exInner}");
                            tcs.SetResult(UI.UICommonImage(UI.CommonImageTypes.blank));
                        }
                    });
                }
                catch (Exception exSchedule)
                {
                    Plugin.PluginLog.Debug($"RenderIconAsync: scheduling texture creation failed for id={iconID}: {exSchedule}");
                    return UI.UICommonImage(UI.CommonImageTypes.blank);
                }

                var texture = await tcs.Task.ConfigureAwait(false);

                if (texture != null && texture.Handle != IntPtr.Zero)
                {
                    loadedTextures[iconID] = texture;
                    Plugin.PluginLog.Debug($"RenderIconAsync: loaded texture for id={iconID}");
                    return texture;
                }

                Plugin.PluginLog.Debug($"RenderIconAsync: texture load returned null/invalid for id={iconID}");
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"RenderIconAsync: Failed to load icon for ID {iconID}. Exception: {ex}");
            }

            return UI.UICommonImage(UI.CommonImageTypes.blank);
        }


        public static Dictionary<int, IDalamudTextureWrap> loadedStatusEffectTextures = new();
        public static IDalamudTextureWrap selectedStatusIcon;

        public static int selectedStatusIconID { get; private set; }
        public static bool SetIcon { get; set; }

        public static async Task<IDalamudTextureWrap> RenderStatusEffectIconAsync(Plugin Plugin, int statusEffectID)
        {
            if (loadedStatusEffectTextures.ContainsKey(statusEffectID))
            {
                return loadedStatusEffectTextures[statusEffectID];
            }

            try
            {
                if (statusEffectID <= 0)
                {
                    Plugin.PluginLog.Debug("Invalid status effect ID, returning blank icon.");
                    return UI.UICommonImage(UI.CommonImageTypes.blank);
                }

                var statusEffect = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>()?.GetRow((uint)statusEffectID);
                if (statusEffect == null )
                {
                    Plugin.PluginLog.Debug($"No status effect found for ID {statusEffectID}, returning blank icon.");
                    return UI.UICommonImage(UI.CommonImageTypes.blank);
                }

                Plugin.PluginLog.Debug($"Loading status effect icon for ID {statusEffectID} with Icon ID: {statusEffect.Value.Icon}");

                var statusIconID = (uint)statusEffect.Value.Icon;
                var icon = Plugin.DataManager.GameData.GetIcon(statusIconID);

                if (icon != null && !string.IsNullOrEmpty(icon.FilePath))
                {
                    Plugin.PluginLog.Debug($"Loading icon from path: {icon.FilePath}");
                    var texture = await LoadTextureAsync(icon.FilePath);
                    if (texture != null && texture.Handle != IntPtr.Zero)
                    {
                        loadedStatusEffectTextures[(int)statusIconID] = texture;
                        return texture;
                    }
                    else
                    {
                        Plugin.PluginLog.Debug($"Failed to load texture for status effect icon {statusIconID}. FilePath: {icon.FilePath}");
                    }
                }
                else
                {
                    Plugin.PluginLog.Debug($"Invalid icon data for status effect ID {statusEffectID}. Icon ID: {statusIconID}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"RenderStatusEffectIconAsync: Failed to load status effect icon for ID {statusEffectID}. Exception: {ex.Message}");
            }

            return UI.UICommonImage(UI.CommonImageTypes.blank);
        }





    }
}
