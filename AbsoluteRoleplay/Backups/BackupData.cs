using AbsoluteRP; // brings ProfileData, CustomTab, layouts, etc.
using AbsoluteRP.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AbsoluteRP.Backups
{
    internal class BackupData
    {

        public static async Task ExportProfileToJsonAsync(ProfileData profile, string filePath)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            var dto = MapProfileToDto(profile);

            var options = CreateSerializerOptions();
            var json = JsonSerializer.Serialize(dto, options);

            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            try
            {
                Plugin.PluginLog?.Debug($"ExportProfileToJsonAsync: writing backup to '{filePath}'. Exists={File.Exists(filePath)}. NewLength={json?.Length ?? 0}");

                // Remove existing file first to avoid any platform/IO edge-case append behavior
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                        Plugin.PluginLog?.Debug($"ExportProfileToJsonAsync: deleted existing file '{filePath}' before write.");
                    }
                    catch (Exception exDel)
                    {
                        Plugin.PluginLog?.Debug($"ExportProfileToJsonAsync: failed deleting existing file '{filePath}': {exDel.Message}");
                        // proceed to attempt write anyway
                    }
                }

                await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);
                Plugin.PluginLog?.Debug($"ExportProfileToJsonAsync: write complete for '{filePath}'");
            }
            catch (Exception ex)
            {
                Plugin.PluginLog?.Debug($"ExportProfileToJsonAsync: failed writing '{filePath}': {ex.Message}");
                throw;
            }
        }
        public static async Task<ProfileData> ImportProfileFromJsonAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            string json;
            var trimmed = filePath.Trim();

            // Tolerate BOM or invisible leading chars
            trimmed = trimmed.TrimStart('\uFEFF', '\u200B');

            // If caller passed JSON text instead of a path, deserialize directly.
            var looksLikeJson = trimmed.Length > 0 && (trimmed[0] == '{' || trimmed[0] == '[');
            if (looksLikeJson)
            {
                json = trimmed;
            }
            else
            {
                // Normalize common wrappers and trim quotes/whitespace
                filePath = trimmed;
                if (filePath.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    try { filePath = new Uri(filePath).LocalPath; } catch { /* fall back to raw string */ }
                }
                filePath = filePath.Trim('"');

                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(filePath);
                }
                catch (PathTooLongException ptlx)
                {
                    throw new PathTooLongException($"The specified backup path is too long: consider using a shorter path, a UNC long-path prefix (\\\\?\\), or pass the JSON content directly to ImportProfileFromJsonAsync.", ptlx);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Invalid backup file path '{filePath}': {ex.Message}", nameof(filePath), ex);
                }

                if (!File.Exists(fullPath))
                    throw new FileNotFoundException($"Backup file not found at '{fullPath}'. Current working directory: '{Directory.GetCurrentDirectory()}'", fullPath);

                json = await File.ReadAllTextAsync(fullPath).ConfigureAwait(false);
            }

            // Be lenient about naming / enums
            var options = CreateSerializerOptions();
            // Make primary deserialization resilient to case differences in incoming JSON.
            options.PropertyNameCaseInsensitive = true;
            try { options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()); } catch { }

            // Primary attempt: deserialize to the BackupData.ProfileDto shape (export format)
            ProfileDto dto = null;
            try
            {
                dto = JsonSerializer.Deserialize<ProfileDto>(json, options);
            }
            catch (Exception ex)
            {
                dto = null; // fallback below
                try { Plugin.PluginLog?.Debug($"ImportProfileFromJsonAsync: primary deserialization threw: {ex.Message}"); } catch { }
            }

            // Diagnostic logging -- helps detect why tabs are missing
            try
            {
                Plugin.PluginLog?.Debug($"ImportProfileFromJsonAsync: dto present={dto != null}");
                Plugin.PluginLog?.Debug($"ImportProfileFromJsonAsync: json length={json?.Length ?? 0}");
                if (dto != null)
                {
                    Plugin.PluginLog?.Debug($"ImportProfileFromJsonAsync: dto.customTabs count={dto.customTabs?.Count ?? 0}");
                    if (dto.customTabs != null)
                    {
                        for (var i = 0; i < dto.customTabs.Count; i++)
                        {
                            var t = dto.customTabs[i];
                            var layoutRuntimeType = t.Layout == null ? "null" : t.Layout.GetType().FullName;
                            Plugin.PluginLog?.Debug($"ImportProfileFromJsonAsync: tab[{i}] name='{t.Name}' layoutType={t.layoutType} layoutRuntimeType={layoutRuntimeType}");
                            // If layout is a JsonElement, log a short preview to help debug missing fields
                            if (t.Layout is JsonElement je)
                            {
                                try
                                {
                                    var preview = je.ToString();
                                    if (preview != null && preview.Length > 400) preview = preview.Substring(0, 400) + "...";
                                    Plugin.PluginLog?.Debug($"ImportProfileFromJsonAsync: tab[{i}] layout preview: {preview}");
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch { }

            if (dto != null && dto.customTabs != null && dto.customTabs.Count > 0)
            {
                try { System.Diagnostics.Debug.WriteLine($"ImportProfileFromJsonAsync: loaded ProfileDto index={dto.index} title='{dto.title}' tabs={dto.customTabs.Count}"); } catch { }
                return MapDtoToProfile(dto);
            }

            // Fallback: inspect JSON shape (ProfilesCache format or similar)
            System.Text.Json.Nodes.JsonNode rootNode;
            try
            {
                rootNode = System.Text.Json.Nodes.JsonNode.Parse(json);
            }
            catch (System.Text.Json.JsonException jex)
            {
                throw new InvalidOperationException($"Failed to parse profile JSON (not BackupData DTO and not cache-format): invalid JSON. Length={json?.Length ?? 0}", jex);
            }

            if (rootNode is not System.Text.Json.Nodes.JsonObject rootObj)
                throw new InvalidOperationException("Profile JSON is not an object; cannot import.");

            // Diagnostic: list top-level keys without using .Keys
            try
            {
                var keys = string.Join(", ", rootObj.Select(kv => kv.Key));
                Plugin.PluginLog?.Debug($"ImportProfileFromJsonAsync: fallback parse keys: {keys}");
            }
            catch { }

            var tabsNode = rootObj["customTabs"] as System.Text.Json.Nodes.JsonArray;
            var profileFolderName = rootObj["profileFolder"]?.GetValue<string>();

            if ((tabsNode != null && tabsNode.Count > 0) || !string.IsNullOrEmpty(profileFolderName))
            {
                var profile = new ProfileData
                {
                    index = rootObj["index"]?.GetValue<int>() ?? -1,
                    title = rootObj["title"]?.GetValue<string>() ?? rootObj["profileTitle"]?.GetValue<string>() ?? string.Empty,
                    titleColor = rootObj["titleColor"] is System.Text.Json.Nodes.JsonArray tc && tc.Count >= 4
                        ? new System.Numerics.Vector4(tc[0].GetValue<float>(), tc[1].GetValue<float>(), tc[2].GetValue<float>(), tc[3].GetValue<float>())
                        : new System.Numerics.Vector4(1, 1, 1, 1),
                    isPrivate = rootObj["isPrivate"]?.GetValue<bool>() ?? false,
                    isActive = rootObj["isActive"]?.GetValue<bool>() ?? false,
                    customTabs = new List<CustomTab>()
                };

                try
                {
                    if (rootObj["avatarBytes"] != null)
                    {
                        var b64 = rootObj["avatarBytes"]?.GetValue<string>();
                        if (!string.IsNullOrEmpty(b64)) profile.avatarBytes = Convert.FromBase64String(b64);
                    }
                }
                catch { }

                try
                {
                    if (rootObj["backgroundBytes"] != null)
                    {
                        var b64 = rootObj["backgroundBytes"]?.GetValue<string>();
                        if (!string.IsNullOrEmpty(b64)) profile.backgroundBytes = Convert.FromBase64String(b64);
                    }
                }
                catch { }

                // Resolve probable profileFolderPath without calling ResolveBasePath()
                string profileFolderPath = null;
                if (!string.IsNullOrEmpty(profileFolderName))
                {
                    // 1) If import was from a file, check sibling folder
                    try
                    {
                        if (!trimmed.StartsWith("{") && !trimmed.StartsWith("["))
                        {
                            var fp = Path.GetFullPath(filePath.Trim('"'));
                            var sibling = Path.Combine(Path.GetDirectoryName(fp) ?? string.Empty, profileFolderName);
                            if (Directory.Exists(sibling)) profileFolderPath = sibling;
                        }
                    }
                    catch { }

                    // 2) Try plugin config dir ARPData/Profiles if available
                    if (profileFolderPath == null)
                    {
                        try
                        {
                            var pluginConfigDir = Plugin.PluginInterface?.GetPluginConfigDirectory();
                            if (!string.IsNullOrWhiteSpace(pluginConfigDir))
                            {
                                var cand = Path.Combine(pluginConfigDir, "ARPData", "Profiles", profileFolderName);
                                if (Directory.Exists(cand)) profileFolderPath = cand;
                            }
                        }
                        catch { }
                    }

                    // 3) Try assembly/AppContext fallback similar to ProfilesCache
                    if (profileFolderPath == null)
                    {
                        try
                        {
                            var asmDir = Plugin.PluginInterface?.GetType()?.Assembly?.Location;
                            var baseDir = AppContext.BaseDirectory ?? Path.GetDirectoryName(asmDir) ?? ".";
                            var cand = Path.Combine(baseDir, "ARPData", "Profiles", profileFolderName);
                            if (Directory.Exists(cand)) profileFolderPath = cand;
                        }
                        catch { }
                    }
                }
                else
                {
                    // If no explicit profileFolderName, but import came from a file, try sibling folder named after file (common ProfilesCache layout)
                    try
                    {
                        if (!trimmed.StartsWith("{") && !trimmed.StartsWith("[]"))
                        {
                            var fp = Path.GetFullPath(filePath.Trim('"'));
                            var baseName = Path.GetFileNameWithoutExtension(fp);
                            var candidate = Path.Combine(Path.GetDirectoryName(fp) ?? string.Empty, baseName);
                            if (Directory.Exists(candidate)) profileFolderPath = candidate;
                        }
                    }
                    catch { }
                }

                // Reconstruct tabs
                if (tabsNode != null)
                {
                    foreach (var tnode in tabsNode)
                    {
                        if (tnode is not System.Text.Json.Nodes.JsonObject tobj) continue;
                        var tab = new CustomTab
                        {
                            Name = tobj["name"]?.GetValue<string>() ?? string.Empty,
                            ID = tobj["id"]?.GetValue<int>() ?? 0,
                            IsOpen = tobj["isOpen"]?.GetValue<bool>() ?? false,
                            type = tobj["type"]?.GetValue<int>() ?? 0
                        };

                        var layoutFile = tobj["layoutFile"]?.GetValue<string>();
                        string layoutText = null;
                        if (!string.IsNullOrEmpty(layoutFile) && !string.IsNullOrEmpty(profileFolderPath))
                        {
                            var layoutPath = Path.Combine(profileFolderPath, layoutFile);
                            if (File.Exists(layoutPath))
                            {
                                try { layoutText = File.ReadAllText(layoutPath); } catch { layoutText = null; }
                            }
                        }

                        if (layoutText == null && tobj["layoutData"] != null)
                            layoutText = tobj["layoutData"]?.ToJsonString(options);

                        if (string.IsNullOrEmpty(layoutText))
                        {
                            profile.customTabs.Add(tab);
                            continue;
                        }

                        System.Text.Json.Nodes.JsonNode layoutNode = null;
                        try { layoutNode = System.Text.Json.Nodes.JsonNode.Parse(layoutText); } catch { layoutNode = null; }

                        CustomLayout layoutInstance = null;

                        try
                        {
                            if (layoutNode is System.Text.Json.Nodes.JsonObject lobj)
                            {
                                // Heuristics to choose layout type by distinctive properties
                                if (lobj["images"] is System.Text.Json.Nodes.JsonArray)
                                {
                                    var gDto = JsonSerializer.Deserialize<GalleryLayoutDto>(lobj.ToJsonString(options), options);
                                    if (gDto != null)
                                    {
                                        var gallery = new GalleryLayout
                                        {
                                            tabIndex = gDto.tabIndex,
                                            tabName = gDto.tabName,
                                            images = gDto.images?.Select(img => new ProfileGalleryImage
                                            {
                                                index = img.index,
                                                url = img.url,
                                                tooltip = img.tooltip,
                                                nsfw = img.nsfw,
                                                trigger = img.trigger,
                                                imageBytes = img.imageBytes ?? Array.Empty<byte>(),
                                                image = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab),
                                                thumbnail = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab)
                                            }).ToList() ?? new()
                                        };
                                        gallery.layoutType = LayoutTypes.Gallery;
                                        layoutInstance = gallery;
                                    }
                                }
                                else if (lobj["details"] is System.Text.Json.Nodes.JsonArray)
                                {
                                    var dDto = JsonSerializer.Deserialize<DetailsLayoutDto>(lobj.ToJsonString(options), options);
                                    if (dDto != null)
                                    {
                                        var d = new DetailsLayout
                                        {
                                            tabIndex = dDto.tabIndex,
                                            tabName = dDto.tabName,
                                            details = dDto.details?.Select(x => new Detail { id = x.id, name = x.name, content = x.content }).ToList() ?? new()
                                        };
                                        d.layoutType = LayoutTypes.Details;
                                        layoutInstance = d;
                                    }
                                }
                                // Info layout detection (text)
                                else if (lobj["text"] != null)
                                {
                                    var iDto = JsonSerializer.Deserialize<InfoLayoutDto>(lobj.ToJsonString(options), options);
                                    if (iDto != null)
                                    {
                                        var i = new InfoLayout { tabIndex = iDto.tabIndex, tabName = iDto.tabName, text = iDto.text };
                                        i.layoutType = LayoutTypes.Info;
                                        layoutInstance = i;
                                    }
                                }
                                // Story layout (chapters)
                                else if (lobj["chapters"] is System.Text.Json.Nodes.JsonArray)
                                {
                                    var sDto = JsonSerializer.Deserialize<StoryLayoutDto>(lobj.ToJsonString(options), options);
                                    if (sDto != null)
                                    {
                                        var s = new StoryLayout { tabIndex = sDto.tabIndex, tabName = sDto.tabName, chapters = sDto.chapters?.Select(c => new StoryChapter { id = c.id, title = c.title, content = c.content }).ToList() ?? new() };
                                        s.layoutType = LayoutTypes.Story;
                                        layoutInstance = s;
                                    }
                                }
                                // BIO layout detection (must be checked before TreeLayout detection)
                                else if (lobj["name"] != null || lobj["descriptors"] is System.Text.Json.Nodes.JsonArray || lobj["fields"] is System.Text.Json.Nodes.JsonArray || lobj["traits"] is System.Text.Json.Nodes.JsonArray)
                                {
                                    var bDto = JsonSerializer.Deserialize<BioLayoutDto>(lobj.ToJsonString(options), options);
                                    if (bDto != null)
                                    {
                                        var b = new BioLayout
                                        {
                                            tabIndex = bDto.tabIndex,
                                            tabName = bDto.tabName,
                                            name = bDto.name,
                                            race = bDto.race,
                                            gender = bDto.gender,
                                            age = bDto.age,
                                            height = bDto.height,
                                            weight = bDto.weight,
                                            afg = bDto.afg,
                                            alignment = bDto.alignment,
                                            personality_1 = bDto.personality_1,
                                            personality_2 = bDto.personality_2,
                                            personality_3 = bDto.personality_3,
                                            descriptors = bDto.descriptors?.Select(d => new descriptor { index = d.index, name = d.name, description = d.description }).ToList() ?? new(),
                                            traits = bDto.traits?.Select(t => new trait
                                            {
                                                iconID = t.iconID,
                                                index = t.index,
                                                name = t.name,
                                                description = t.description,
                                                modifying = t.modifying,
                                                icon = t.iconElement != null ? new IconElement { iconID = t.iconElement.iconID } : new IconElement()
                                            }).ToList() ?? new(),
                                            fields = bDto.fields?.Select(f => new field { index = f.index, name = f.name, description = f.description }).ToList() ?? new()
                                        };
                                        b.layoutType = LayoutTypes.Bio;
                                        layoutInstance = b;
                                    }
                                }
                                // Tree / Relationship layout (relationships or paths present)
                                else if (lobj["relationships"] is System.Text.Json.Nodes.JsonArray || lobj["paths"] is System.Text.Json.Nodes.JsonArray)
                                {
                                    var tDto = JsonSerializer.Deserialize<TreeLayoutDto>(lobj.ToJsonString(options), options);
                                    if (tDto != null)
                                    {
                                        var t = new TreeLayout
                                        {
                                            tabIndex = tDto.tabIndex,
                                            tabName = tDto.tabName,
                                            relationships = tDto.relationships?.Select(r => new Relationship
                                            {
                                                Name = r.Name,
                                                NameColor = r.NameColor,
                                                Description = r.Description,
                                                DescriptionColor = r.DescriptionColor,
                                                IconID = r.IconID,
                                                IconTexture = WindowOperations.RenderIconAsync(Plugin.plugin, r.IconID).GetAwaiter().GetResult(),
                                                LineColor = r.LineColor,
                                                LineThickness = r.LineThickness,
                                                active = r.active,
                                                Links = r.Links?.Select(l => new RelationshipLink { From = (l.from.x, l.from.y), To = (l.to.x, l.to.y) }).ToList() ?? new(),
                                                Slot = r.slot != null ? ((int, int)?)((r.slot.x, r.slot.y)) : null
                                            }).ToList() ?? new(),
                                            Connections = tDto.Connections?.Select(c => (from: (c.from.x, c.from.y), to: (c.to.x, c.to.y))).ToList() ?? new(),
                                            Paths = tDto.Paths?.Select(p => p.Select(pos => (pos.x, pos.y)).ToList()).ToList() ?? new(),
                                            PathConnections = tDto.PathConnections?.Select(pc => pc.Select(conn => (from: (conn.from.x, conn.from.y), to: (conn.to.x, conn.to.y))).ToList()).ToList() ?? new()
                                        };
                                        t.layoutType = LayoutTypes.Relationship;

                                        // Schedule relationship icon texture loads for any IconID present
                                        if (t.relationships != null)
                                        {
                                            foreach (var rel in t.relationships)
                                            {
                                                if (rel != null && rel.IconID > 0)
                                                {
                                                    var id = rel.IconID;
                                                    try { Plugin.PluginLog?.Debug($"ImportProfileFromJsonAsync: scheduling relationship icon load id={id} for tab='{tab.Name}'"); } catch { }
                                                    _ = WindowOperations.RenderIconAsync(Plugin.plugin, id)
                                                        .ContinueWith(tt =>
                                                        {
                                                            try
                                                            {
                                                                var tex = tt.Result;
                                                                if (tex != null && tex.Handle != IntPtr.Zero)
                                                                {
                                                                    try
                                                                    {
                                                                        Plugin.Framework.RunOnFrameworkThread(() =>
                                                                        {
                                                                            try { rel.IconTexture = tex; } catch { }
                                                                        });
                                                                    }
                                                                    catch
                                                                    {
                                                                        try { rel.IconTexture = tex; } catch { }
                                                                    }
                                                                }
                                                            }
                                                            catch { }
                                                        }, TaskScheduler.Default);
                                                }
                                            }
                                        }

                                        layoutInstance = t;
                                    }
                                }
                                else if (lobj["inventorySlotContents"] != null)
                                {
                                    var invDto = JsonSerializer.Deserialize<InventoryLayoutDto>(lobj.ToJsonString(options), options);
                                    if (invDto != null)
                                    {
                                        var inv = new InventoryLayout { tabIndex = invDto.tabIndex, tabName = invDto.tabName, inventorySlotContents = new Dictionary<int, ItemDefinition>() };
                                        if (invDto.inventorySlotContents != null)
                                        {
                                            foreach (var kv in invDto.inventorySlotContents)
                                            {
                                                inv.inventorySlotContents[kv.Key] = new ItemDefinition
                                                {
                                                    name = kv.Value.name,
                                                    description = kv.Value.description,
                                                    type = kv.Value.type,
                                                    subtype = kv.Value.subtype,
                                                    iconID = kv.Value.iconID,
                                                    slot = kv.Value.slot,
                                                    quality = kv.Value.quality,
                                                    iconTexture = null
                                                };
                                            }
                                        }
                                        inv.layoutType = LayoutTypes.Inventory;
                                        layoutInstance = inv;
                                    }
                                }
                                else
                                {
                                    var genDto = JsonSerializer.Deserialize<GenericLayoutDto>(lobj.ToJsonString(options), options);
                                    if (genDto != null)
                                    {
                                        var gen = new CustomLayout { id = genDto.id, name = genDto.name, layoutType = genDto.layoutType, viewable = genDto.viewable };
                                        layoutInstance = gen;
                                    }
                                }
                            }
                        }
                        catch (Exception exLayout)
                        {
                            try { Plugin.PluginLog?.Debug($"ImportProfileFromJsonAsync: layout parse failed for tab '{tab.Name}': {exLayout.Message}"); } catch { }
                        }

                        tab.Layout = layoutInstance;
                        profile.customTabs.Add(tab);
                    }
                }

                return profile;
            }

            // Nothing matched: log and try returning empty-mapped dto if present
            try { Plugin.PluginLog?.Debug($"ImportProfileFromJsonAsync: no tabs found in JSON. DTO present: {dto != null}"); } catch { }
            if (dto != null) return MapDtoToProfile(dto);

            throw new InvalidOperationException("ImportProfileFromJsonAsync: backup JSON did not match any known profile formats (no customTabs found).");
        }
        // ----- Serialization options & helpers -----
        private static JsonSerializerOptions CreateSerializerOptions()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
            // Custom converters used by DTOs
            options.Converters.Add(new Vector4Converter());
            return options;
        }

        private static ProfileDto MapProfileToDto(ProfileData profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            var dto = new ProfileDto
            {
                index = profile.index,
                SHOW_ON_COMPASS = profile.SHOW_ON_COMPASS,
                NSFW = profile.NSFW,
                TRIGGERING = profile.TRIGGERING,
                SpoilerARR = profile.SpoilerARR,
                SpoilerHW = profile.SpoilerHW,
                SpoilerSB = profile.SpoilerSB,
                SpoilerSHB = profile.SpoilerSHB,
                SpoilerEW = profile.SpoilerEW,
                SpoilerDT = profile.SpoilerDT,
                avatarBytes = profile.avatarBytes,
                backgroundBytes = profile.backgroundBytes,
                title = profile.title,
                titleColor = profile.titleColor,
                isPrivate = profile.isPrivate,
                isActive = profile.isActive,
                OOC = profile.OOC,
                customTabs = new List<CustomTabDto>()
            };

            foreach (var tab in profile.customTabs ?? new List<CustomTab>())
            {
                var inferredLayoutType = tab.Layout switch
                {
                    BioLayout => LayoutTypes.Bio,
                    DetailsLayout => LayoutTypes.Details,
                    GalleryLayout => LayoutTypes.Gallery,
                    InfoLayout => LayoutTypes.Info,
                    StoryLayout => LayoutTypes.Story,
                    InventoryLayout => LayoutTypes.Inventory,
                    TreeLayout => LayoutTypes.Relationship,
                    _ => tab.Layout?.layoutType ?? LayoutTypes.Info
                };

                var tabDto = new CustomTabDto
                {
                    ID = tab.ID,
                    Name = tab.Name,
                    IsOpen = tab.IsOpen,
                    type = tab.type,
                    layoutType = inferredLayoutType
                };

                switch (tab.Layout)
                {
                    case BioLayout b:
                        tabDto.Layout = new BioLayoutDto
                        {
                            tabIndex = b.tabIndex,
                            tabName = b.tabName,
                            name = b.name,
                            race = b.race,
                            gender = b.gender,
                            age = b.age,
                            height = b.height,
                            weight = b.weight,
                            afg = b.afg,
                            alignment = b.alignment,
                            personality_1 = b.personality_1,
                            personality_2 = b.personality_2,
                            personality_3 = b.personality_3,
                            descriptors = b.descriptors?.Select(d => new DescriptorDto { index = d.index, name = d.name, description = d.description }).ToList() ?? new(),
                            traits = b.traits?.Select(t => new TraitDto
                            {
                                iconID = t.iconID,
                                index = t.index,
                                name = t.name,
                                description = t.description,
                                modifying = t.modifying,
                                iconElement = t.icon != null ? new IconElementDto { iconID = t.icon.iconID } : null

                            }).ToList() ?? new(),
                            fields = b.fields?.Select(f => new FieldDto { index = f.index, name = f.name, description = f.description }).ToList() ?? new()
                        };
                        break;

                    case DetailsLayout d:
                        tabDto.Layout = new DetailsLayoutDto
                        {
                            tabIndex = d.tabIndex,
                            tabName = d.tabName,
                            details = d.details?.Select(x => new DetailDto { id = x.id, name = x.name, content = x.content }).ToList() ?? new()
                        };
                        break;

                    case GalleryLayout g:
                        tabDto.Layout = new GalleryLayoutDto
                        {
                            tabIndex = g.tabIndex,
                            tabName = g.tabName,
                            images = g.images?.Select(img => new GalleryImageDto
                            {
                                index = img.index,
                                url = img.url,
                                tooltip = img.tooltip,
                                nsfw = img.nsfw,
                                trigger = img.trigger,
                                imageBytes = img.imageBytes
                            }).ToList() ?? new()
                        };
                        break;

                    case InfoLayout i:
                        tabDto.Layout = new InfoLayoutDto
                        {
                            tabIndex = i.tabIndex,
                            tabName = i.tabName,
                            text = i.text
                        };
                        break;

                    case StoryLayout s:
                        tabDto.Layout = new StoryLayoutDto
                        {
                            tabIndex = s.tabIndex,
                            tabName = s.tabName,
                            chapters = s.chapters?.Select(c => new StoryChapterDto { id = c.id, title = c.title, content = c.content }).ToList() ?? new()
                        };
                        break;

                    case InventoryLayout inv:
                        tabDto.Layout = new InventoryLayoutDto
                        {
                            tabIndex = inv.tabIndex,
                            tabName = inv.tabName,
                            inventorySlotContents = inv.inventorySlotContents?.ToDictionary(
                                kv => kv.Key,
                                kv => new ItemDefDto
                                {
                                    name = kv.Value.name,
                                    description = kv.Value.description,
                                    type = kv.Value.type,
                                    subtype = kv.Value.subtype,
                                    iconID = kv.Value.iconID,
                                    slot = kv.Value.slot,
                                    quality = kv.Value.quality,
                                }) ?? new()
                        };
                        break;

                    case TreeLayout tr:
                        tabDto.Layout = new TreeLayoutDto
                        {
                            tabIndex = tr.tabIndex,
                            tabName = tr.tabName,
                            relationships = tr.relationships?.Select(r => new RelationshipDto
                            {
                                Name = r.Name,
                                NameColor = r.NameColor,
                                Description = r.Description,
                                DescriptionColor = r.DescriptionColor,
                                IconID = r.IconID,
                                LineColor = r.LineColor,
                                LineThickness = r.LineThickness,
                                active = r.active,
                                Links = r.Links?.Select(l => new RelationshipLinkDto
                                {
                                    from = new CoordDto { x = l.From.x, y = l.From.y },
                                    to = new CoordDto { x = l.To.x, y = l.To.y }
                                }).ToList() ?? new(),
                                slot = r.Slot.HasValue ? new CoordDto { x = r.Slot.Value.x, y = r.Slot.Value.y } : null
                            }).ToList() ?? new(),
                            Connections = tr.Connections?.Select(conn => new TupleCoordDto
                            {
                                from = new CoordDto { x = conn.from.x, y = conn.from.y },
                                to = new CoordDto { x = conn.to.x, y = conn.to.y }
                            }).ToList() ?? new(),
                            Paths = tr.Paths?.Select(p => p.Select(pos => new CoordDto { x = pos.x, y = pos.y }).ToList()).ToList() ?? new(),
                            PathConnections = tr.PathConnections?.Select(pc => pc.Select(conn => new TupleCoordDto
                            {
                                from = new CoordDto { x = conn.from.x, y = conn.from.y },
                                to = new CoordDto { x = conn.to.x, y = conn.to.y }
                            }).ToList()).ToList() ?? new()
                        };
                        break;

                    default:
                        tabDto.Layout = new GenericLayoutDto
                        {
                            id = tab.Layout?.id ?? 0,
                            name = tab.Layout?.name ?? string.Empty,
                            layoutType = tab.Layout?.layoutType ?? inferredLayoutType,
                            viewable = tab.Layout?.viewable ?? true
                        };
                        break;
                }

                dto.customTabs.Add(tabDto);
            }

            return dto;
        }

        // Convert DTO -> runtime ProfileData (textures left null; bytes restored)
        private static ProfileData MapDtoToProfile(ProfileDto dto)
        {
            var profile = new ProfileData
            {
                index = dto.index,
                SHOW_ON_COMPASS = dto.SHOW_ON_COMPASS,
                NSFW = dto.NSFW,
                TRIGGERING = dto.TRIGGERING,
                SpoilerARR = dto.SpoilerARR,
                SpoilerHW = dto.SpoilerHW,
                SpoilerSB = dto.SpoilerSB,
                SpoilerSHB = dto.SpoilerSHB,
                SpoilerEW = dto.SpoilerEW,
                SpoilerDT = dto.SpoilerDT,
                avatarBytes = dto.avatarBytes ?? new byte[0],
                backgroundBytes = dto.backgroundBytes ?? new byte[0],
                title = dto.title,
                titleColor = dto.titleColor,
                isPrivate = dto.isPrivate,
                isActive = dto.isActive,
                OOC = dto.OOC,
                customTabs = new List<CustomTab>()
            };

            var options = CreateSerializerOptions(); // used when Layout is a JsonElement after deserialization

            foreach (var tabDto in dto.customTabs ?? new List<CustomTabDto>())
            {
                var tab = new CustomTab
                {
                    ID = tabDto.ID,
                    Name = tabDto.Name ?? string.Empty,
                    IsOpen = tabDto.IsOpen,
                    type = tabDto.type
                };

                // Layout may be already a concrete DTO (in-memory) or a JsonElement (when deserialized from JSON).
                // Try JsonElement deserialization first (the common case when importing from backup JSON).
                bool layoutAssigned = false;
                if (tabDto.Layout is JsonElement je && je.ValueKind != JsonValueKind.Null && je.ValueKind != JsonValueKind.Undefined)
                {
                    try
                    {
                        switch (tabDto.layoutType)
                        {
                            case LayoutTypes.Bio:
                                var bDto = je.Deserialize<BioLayoutDto>(options);
                                if (bDto != null)
                                {
                                    var b = new BioLayout
                                    {
                                        tabIndex = bDto.tabIndex,
                                        tabName = bDto.tabName,
                                        name = bDto.name,
                                        race = bDto.race,
                                        gender = bDto.gender,
                                        age = bDto.age,
                                        height = bDto.height,
                                        weight = bDto.weight,
                                        afg = bDto.afg,
                                        alignment = bDto.alignment,
                                        personality_1 = bDto.personality_1,
                                        personality_2 = bDto.personality_2,
                                        personality_3 = bDto.personality_3,
                                        descriptors = bDto.descriptors?.Select(d => new descriptor { index = d.index, name = d.name, description = d.description }).ToList() ?? new(),
                                        // Prefer iconElement.iconID if set, otherwise fall back to trait.iconID.
                                        traits = bDto.traits?.Select(t =>
                                        {
                                            var resolvedIconId = (t.iconElement != null && t.iconElement.iconID > 0) ? t.iconElement.iconID : t.iconID;
                                            return new trait
                                            {
                                                iconID = t.iconID,
                                                index = t.index,
                                                name = t.name,
                                                description = t.description,
                                                modifying = t.modifying,
                                                icon = new IconElement { iconID = resolvedIconId, icon = UI.UICommonImage(UI.CommonImageTypes.blank) }
                                            };
                                        }).ToList() ?? new(),
                                        fields = bDto.fields?.Select(f => new field { index = f.index, name = f.name, description = f.description }).ToList() ?? new()
                                    };
                                    b.layoutType = LayoutTypes.Bio;

                                    // schedule icon loads using the resolved IconElement.iconID
                                    if (b.traits != null)
                                    {
                                        foreach (var tr in b.traits)
                                        {
                                            if (tr?.icon != null && tr.icon.iconID > 0)
                                            {
                                                var id = tr.icon.iconID;
                                                try { Plugin.PluginLog?.Debug($"ImportProfileFromJsonAsync: scheduling trait icon load id={id} for trait index={tr.index}"); } catch { }

                                                _ = WindowOperations.RenderIconAsync(Plugin.plugin, id)
                                                    .ContinueWith(t =>
                                                    {
                                                        try
                                                        {
                                                            var tex = t.Result;
                                                            if (tex != null && tex.Handle != IntPtr.Zero)
                                                            {
                                                                try
                                                                {
                                                                    // Ensure assignment happens on the framework/main thread so UI will render it
                                                                    Plugin.Framework.RunOnFrameworkThread(() =>
                                                                    {
                                                                        try
                                                                        {
                                                                            tr.icon.icon = tex;
                                                                        }
                                                                        catch { /* swallow to avoid breaking import */ }
                                                                    });
                                                                }
                                                                catch (Exception exRun)
                                                                {
                                                                    try { Plugin.PluginLog?.Debug($"ImportProfileFromJsonAsync: framework assignment failed for icon {id}: {exRun.Message}"); } catch { }
                                                                    // Best-effort fallback (may be unsafe on some builds)
                                                                    try { tr.icon.icon = tex; } catch { }
                                                                }
                                                                try { Plugin.PluginLog?.Debug($"ImportProfileFromJsonAsync: trait icon loaded id={id} for trait index={tr.index}"); } catch { }
                                                            }
                                                            else
                                                            {
                                                                try { Plugin.PluginLog?.Debug($"ImportProfileFromJsonAsync: trait icon load returned null for id={id}"); } catch { }
                                                            }
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            try { Plugin.PluginLog?.Debug($"ImportProfileFromJsonAsync: failed loading trait icon {id}: {ex.Message}"); } catch { }
                                                        }
                                                    }, TaskScheduler.Default);
                                            }
                                        }
                                    }

                                    tab.Layout = b;
                                    layoutAssigned = true;
                                }
                                break;

                            case LayoutTypes.Details:
                                var dDto = je.Deserialize<DetailsLayoutDto>(options);
                                if (dDto != null)
                                {
                                    var d = new DetailsLayout
                                    {
                                        tabIndex = dDto.tabIndex,
                                        tabName = dDto.tabName,
                                        details = dDto.details?.Select(x => new Detail { id = x.id, name = x.name, content = x.content }).ToList() ?? new()
                                    };
                                    d.layoutType = LayoutTypes.Details;
                                    tab.Layout = d;
                                    layoutAssigned = true;
                                }
                                break;

                            case LayoutTypes.Gallery:
                                var gDto = je.Deserialize<GalleryLayoutDto>(options);
                                if (gDto != null)
                                {
                                    var g = new GalleryLayout
                                    {
                                        tabIndex = gDto.tabIndex,
                                        tabName = gDto.tabName,
                                        images = gDto.images?.Select(img => new ProfileGalleryImage
                                        {
                                            index = img.index,
                                            url = img.url,
                                            tooltip = img.tooltip,
                                            nsfw = img.nsfw,
                                            trigger = img.trigger,
                                            imageBytes = img.imageBytes ?? new byte[0],
                                            image = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab),
                                            thumbnail = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab)
                                        }).ToList() ?? new()
                                    };
                                    g.layoutType = LayoutTypes.Gallery;
                                    tab.Layout = g;
                                    layoutAssigned = true;
                                }
                                break;

                            case LayoutTypes.Info:
                                var iDto = je.Deserialize<InfoLayoutDto>(options);
                                if (iDto != null)
                                {
                                    var i = new InfoLayout { tabIndex = iDto.tabIndex, tabName = iDto.tabName, text = iDto.text };
                                    i.layoutType = LayoutTypes.Info;
                                    tab.Layout = i;
                                    layoutAssigned = true;
                                }
                                break;

                            case LayoutTypes.Story:
                                var sDto = je.Deserialize<StoryLayoutDto>(options);
                                if (sDto != null)
                                {
                                    var s = new StoryLayout { tabIndex = sDto.tabIndex, tabName = sDto.tabName, chapters = sDto.chapters?.Select(c => new StoryChapter { id = c.id, title = c.title, content = c.content }).ToList() ?? new() };
                                    s.layoutType = LayoutTypes.Story;
                                    tab.Layout = s;
                                    layoutAssigned = true;
                                }
                                break;

                            case LayoutTypes.Inventory:
                                var invDto = je.Deserialize<InventoryLayoutDto>(options);
                                if (invDto != null)
                                {
                                    var inv = new InventoryLayout { tabIndex = invDto.tabIndex, tabName = invDto.tabName, inventorySlotContents = new Dictionary<int, ItemDefinition>() };
                                    if (invDto.inventorySlotContents != null)
                                    {
                                        foreach (var kv in invDto.inventorySlotContents)
                                        {
                                            inv.inventorySlotContents[kv.Key] = new ItemDefinition
                                            {
                                                name = kv.Value.name,
                                                description = kv.Value.description,
                                                type = kv.Value.type,
                                                subtype = kv.Value.subtype,
                                                iconID = kv.Value.iconID,
                                                slot = kv.Value.slot,
                                                quality = kv.Value.quality,
                                                iconTexture = null
                                            };
                                        }
                                    }
                                    inv.layoutType = LayoutTypes.Inventory;
                                    tab.Layout = inv;
                                    layoutAssigned = true;
                                }
                                break;

                            case LayoutTypes.Relationship:
                                var tDto = je.Deserialize<TreeLayoutDto>(options);
                                if (tDto != null)
                                {
                                    var t = new TreeLayout
                                    {
                                        tabIndex = tDto.tabIndex,
                                        tabName = tDto.tabName,
                                        relationships = tDto.relationships?.Select(r => new Relationship
                                        {
                                            Name = r.Name,
                                            NameColor = r.NameColor,
                                            Description = r.Description,
                                            DescriptionColor = r.DescriptionColor,
                                            IconID = r.IconID,
                                            LineColor = r.LineColor,
                                            LineThickness = r.LineThickness,
                                            active = r.active,
                                            Links = r.Links?.Select(l => new RelationshipLink { From = (l.from.x, l.from.y), To = (l.to.x, l.to.y) }).ToList() ?? new(),
                                            Slot = r.slot != null ? ((int, int)?)((r.slot.x, r.slot.y)) : null
                                        }).ToList() ?? new(),
                                        Connections = tDto.Connections?.Select(c => (from: (c.from.x, c.from.y), to: (c.to.x, c.to.y))).ToList() ?? new(),
                                        Paths = tDto.Paths?.Select(p => p.Select(pos => (pos.x, pos.y)).ToList()).ToList() ?? new(),
                                        PathConnections = tDto.PathConnections?.Select(pc => pc.Select(conn => (from: (conn.from.x, conn.from.y), to: (conn.to.x, conn.to.y))).ToList()).ToList() ?? new()
                                    };
                                    t.layoutType = LayoutTypes.Relationship;

                                    // schedule relationship icon loads for any IconID present
                                    if (t.relationships != null)
                                    {
                                        foreach (var rel in t.relationships)
                                        {
                                            if (rel != null && rel.IconID > 0)
                                            {
                                                var id = rel.IconID;
                                                try { Plugin.PluginLog?.Debug($"ImportProfileFromJsonAsync: scheduling relationship icon load id={id} for tab='{tab.Name}'"); } catch { }
                                                _ = WindowOperations.RenderIconAsync(Plugin.plugin, id)
                                                    .ContinueWith(tt =>
                                                    {
                                                        try
                                                        {
                                                            var tex = tt.Result;
                                                            if (tex != null && tex.Handle != IntPtr.Zero)
                                                            {
                                                                try
                                                                {
                                                                    Plugin.Framework.RunOnFrameworkThread(() =>
                                                                    {
                                                                        try { rel.IconTexture = tex; } catch { }
                                                                    });
                                                                }
                                                                catch
                                                                {
                                                                    try { rel.IconTexture = tex; } catch { }
                                                                }
                                                            }
                                                        }
                                                        catch { }
                                                    }, TaskScheduler.Default);
                                            }
                                        }
                                    }

                                    tab.Layout = t;
                                    layoutAssigned = true;
                                }
                                break;

                            default:
                                var genDto = je.Deserialize<GenericLayoutDto>(options);
                                if (genDto != null)
                                {
                                    var gen = new CustomLayout { id = genDto.id, name = genDto.name, layoutType = genDto.layoutType, viewable = genDto.viewable };
                                    tab.Layout = gen;
                                    layoutAssigned = true;
                                }
                                break;
                        }
                    }
                    catch
                    {
                        // swallow and fall through to attempt other heuristics below
                    }
                }

                // If we didn't assign a layout above (e.g. Layout was already a concrete DTO object in-memory
                // or deserialization-by-layoutType failed), attempt the original switch using concrete DTO instances
                if (!layoutAssigned)
                {
                    switch (tabDto.Layout)
                    {
                        case BioLayoutDto bDto:
                            var b2 = new BioLayout
                            {
                                tabIndex = bDto.tabIndex,
                                tabName = bDto.tabName,
                                name = bDto.name,
                                race = bDto.race,
                                gender = bDto.gender,
                                age = bDto.age,
                                height = bDto.height,
                                weight = bDto.weight,
                                afg = bDto.afg,
                                alignment = bDto.alignment,
                                personality_1 = bDto.personality_1,
                                personality_2 = bDto.personality_2,
                                personality_3 = bDto.personality_3,
                                descriptors = bDto.descriptors?.Select(d => new descriptor { index = d.index, name = d.name, description = d.description }).ToList() ?? new(),
                                traits = bDto.traits?.Select(t =>
                                {
                                    var resolvedIconId = (t.iconElement != null && t.iconElement.iconID > 0) ? t.iconElement.iconID : t.iconID;
                                    return new trait
                                    {
                                        iconID = t.iconID,
                                        index = t.index,
                                        name = t.name,
                                        description = t.description,
                                        modifying = t.modifying,
                                        icon = new IconElement { iconID = resolvedIconId, icon = UI.UICommonImage(UI.CommonImageTypes.blank) }
                                    };
                                }).ToList() ?? new(),
                                fields = bDto.fields?.Select(f => new field { index = f.index, name = f.name, description = f.description }).ToList() ?? new()
                            };
                            b2.layoutType = LayoutTypes.Bio;

                            if (b2.traits != null)
                            {
                                foreach (var tr in b2.traits)
                                {
                                    if (tr?.icon != null && tr.icon.iconID > 0)
                                    {
                                        var id = tr.icon.iconID;
                                        try { Plugin.PluginLog?.Debug($"ImportProfileFromJsonAsync: scheduling trait icon load id={id} for trait index={tr.index}"); } catch { }

                                        _ = WindowOperations.RenderIconAsync(Plugin.plugin, id)
                                            .ContinueWith(t =>
                                            {
                                                try
                                                {
                                                    var tex = t.Result;
                                                    if (tex != null && tex.Handle != IntPtr.Zero)
                                                    {
                                                        try
                                                        {
                                                            Plugin.Framework.RunOnFrameworkThread(() =>
                                                            {
                                                                try
                                                                {
                                                                    tr.icon.icon = tex;
                                                                }
                                                                catch { }
                                                            });
                                                        }
                                                        catch (Exception exRun)
                                                        {
                                                            try { Plugin.PluginLog?.Debug($"ImportProfileFromJsonAsync: framework assignment failed for icon {id}: {exRun.Message}"); } catch { }
                                                            try { tr.icon.icon = tex; } catch { }
                                                        }
                                                        try { Plugin.PluginLog?.Debug($"ImportProfileFromJsonAsync: trait icon loaded id={id} for trait index={tr.index}"); } catch { }
                                                    }
                                                    else
                                                    {
                                                        try { Plugin.PluginLog?.Debug($"ImportProfileFromJsonAsync: trait icon load returned null for id={id}"); } catch { }
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    try { Plugin.PluginLog?.Debug($"ImportProfileFromJsonAsync: failed loading trait icon {id}: {ex.Message}"); } catch { }
                                                }
                                            }, TaskScheduler.Default);
                                    }
                                }
                            }

                            b2.layoutType = LayoutTypes.Bio;
                            tab.Layout = b2;
                            break;

                        case DetailsLayoutDto dDto:
                            var d = new DetailsLayout
                            {
                                tabIndex = dDto.tabIndex,
                                tabName = dDto.tabName,
                                details = dDto.details?.Select(x => new Detail { id = x.id, name = x.name, content = x.content }).ToList() ?? new()
                            };
                            d.layoutType = LayoutTypes.Details;
                            tab.Layout = d;
                            break;

                        case GalleryLayoutDto gDto:
                            var g = new GalleryLayout
                            {
                                tabIndex = gDto.tabIndex,
                                tabName = gDto.tabName,
                                images = gDto.images?.Select(img => new ProfileGalleryImage
                                {
                                    index = img.index,
                                    url = img.url,
                                    tooltip = img.tooltip,
                                    nsfw = img.nsfw,
                                    trigger = img.trigger,
                                    imageBytes = img.imageBytes ?? new byte[0],
                                    image = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab),
                                    thumbnail = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab)
                                }).ToList() ?? new()
                            };
                            g.layoutType = LayoutTypes.Gallery;
                            tab.Layout = g;
                            break;

                        case InfoLayoutDto iDto:
                            var i = new InfoLayout { tabIndex = iDto.tabIndex, tabName = iDto.tabName, text = iDto.text };
                            i.layoutType = LayoutTypes.Info;
                            tab.Layout = i;
                            break;

                        case StoryLayoutDto sDto:
                            var s = new StoryLayout { tabIndex = sDto.tabIndex, tabName = sDto.tabName, chapters = sDto.chapters?.Select(c => new StoryChapter { id = c.id, title = c.title, content = c.content }).ToList() ?? new() };
                            s.layoutType = LayoutTypes.Story;
                            tab.Layout = s;
                            break;

                        case InventoryLayoutDto invDto:
                            var inv = new InventoryLayout { tabIndex = invDto.tabIndex, tabName = invDto.tabName, inventorySlotContents = new Dictionary<int, ItemDefinition>() };
                            if (invDto.inventorySlotContents != null)
                            {
                                foreach (var kv in invDto.inventorySlotContents)
                                {
                                    inv.inventorySlotContents[kv.Key] = new ItemDefinition
                                    {
                                        name = kv.Value.name,
                                        description = kv.Value.description,
                                        type = kv.Value.type,
                                        subtype = kv.Value.subtype,
                                        iconID = kv.Value.iconID,
                                        slot = kv.Value.slot,
                                        quality = kv.Value.quality,
                                        iconTexture = null
                                    };
                                }
                            }
                            inv.layoutType = LayoutTypes.Inventory;
                            tab.Layout = inv;
                            break;

                        case TreeLayoutDto tDto:
                            var t = new TreeLayout
                            {
                                tabIndex = tDto.tabIndex,
                                tabName = tDto.tabName,
                                relationships = tDto.relationships?.Select(r => new Relationship
                                {
                                    Name = r.Name,
                                    NameColor = r.NameColor,
                                    Description = r.Description,
                                    DescriptionColor = r.DescriptionColor,
                                    IconID = r.IconID,
                                    LineColor = r.LineColor,
                                    LineThickness = r.LineThickness,
                                    active = r.active,
                                    Links = r.Links?.Select(l => new RelationshipLink { From = (l.from.x, l.from.y), To = (l.to.x, l.to.y) }).ToList() ?? new(),
                                    Slot = r.slot != null ? ((int, int)?)((r.slot.x, r.slot.y)) : null
                                }).ToList() ?? new(),
                                Connections = tDto.Connections?.Select(c => (from: (c.from.x, c.from.y), to: (c.to.x, c.to.y))).ToList() ?? new(),
                                Paths = tDto.Paths?.Select(p => p.Select(pos => (pos.x, pos.y)).ToList()).ToList() ?? new(),
                                PathConnections = tDto.PathConnections?.Select(pc => pc.Select(conn => (from: (conn.from.x, conn.from.y), to: (conn.to.x, conn.to.y))).ToList()).ToList() ?? new()
                            };

                            t.layoutType = LayoutTypes.Relationship;

                            // schedule relationship icon loads for any IconID present
                            if (t.relationships != null)
                            {
                                foreach (var rel in t.relationships)
                                {
                                    if (rel != null && rel.IconID > 0)
                                    {
                                        var id = rel.IconID;
                                        try { Plugin.PluginLog?.Debug($"ImportProfileFromJsonAsync: scheduling relationship icon load id={id} for tab='{tab.Name}'"); } catch { }
                                        _ = WindowOperations.RenderIconAsync(Plugin.plugin, id)
                                            .ContinueWith(tt =>
                                            {
                                                try
                                                {
                                                    var tex = tt.Result;
                                                    if (tex != null && tex.Handle != IntPtr.Zero)
                                                    {
                                                        try
                                                        {
                                                            Plugin.Framework.RunOnFrameworkThread(() =>
                                                            {
                                                                try { rel.IconTexture = tex; } catch { }
                                                            });
                                                        }
                                                        catch
                                                        {
                                                            try { rel.IconTexture = tex; } catch { }
                                                        }
                                                    }
                                                }
                                                catch { }
                                            }, TaskScheduler.Default);
                                    }
                                }
                            }

                            t.layoutType = LayoutTypes.Relationship;
                            tab.Layout = t;
                            break;

                        case GenericLayoutDto genc:
                            var gen = new CustomLayout { id = genc.id, name = genc.name, layoutType = genc.layoutType, viewable = genc.viewable };
                            tab.Layout = gen;
                            break;

                        default:
                            // unknown layout DTO type => leave as simple base layout
                            tab.Layout = new CustomLayout { id = 0, name = tabDto.Name ?? string.Empty, layoutType = tabDto.layoutType, viewable = true };
                            break;
                    }
                }

                profile.customTabs.Add(tab);
            }

            return profile;
        }

        // ----- DTO definitions -----
        private class ProfileDto
        {
            public int index { get; set; }
            public bool SHOW_ON_COMPASS { get; set; }
            public bool NSFW { get; set; }
            public bool TRIGGERING { get; set; }
            public bool SpoilerARR { get; set; }
            public bool SpoilerHW { get; set; }
            public bool SpoilerSB { get; set; }
            public bool SpoilerSHB { get; set; }
            public bool SpoilerEW { get; set; }
            public bool SpoilerDT { get; set; }
            public byte[] avatarBytes { get; set; }
            public byte[] backgroundBytes { get; set; }
            public string title { get; set; }
            public Vector4 titleColor { get; set; }
            public bool isPrivate { get; set; }
            public bool isActive { get; set; }
            public List<CustomTabDto> customTabs { get; set; }
            public string OOC { get; set; }
        }

        private class CustomTabDto
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public bool IsOpen { get; set; }
            public int type { get; set; }
            public LayoutTypes layoutType { get; set; }
            public object Layout { get; set; } // keep object so it can be a concrete DTO or a JsonElement at runtime
        }
        // Generic DTO (fallback)
        private class GenericLayoutDto
        {
            public int id { get; set; }
            public string name { get; set; }
            public LayoutTypes layoutType { get; set; }
            public bool viewable { get; set; }
        }

        private class BioLayoutDto
        {
            public int tabIndex { get; set; }
            public string tabName { get; set; }
            public string name { get; set; }
            public string race { get; set; }
            public string gender { get; set; }
            public string age { get; set; }
            public string height { get; set; }
            public string weight { get; set; }
            public string afg { get; set; }
            public int alignment { get; set; }
            public int personality_1 { get; set; }
            public int personality_2 { get; set; }
            public int personality_3 { get; set; }
            public List<DescriptorDto> descriptors { get; set; }
            public List<TraitDto> traits { get; set; }
            public List<FieldDto> fields { get; set; }
        }

        private class DescriptorDto { public int index { get; set; } public string name { get; set; } public string description { get; set; } }
        private class FieldDto { public int index { get; set; } public string name { get; set; } public string description { get; set; } }
        private class TraitDto { public int iconID { get; set; } public int index { get; set; } public string name { get; set; } public string description { get; set; } public bool modifying { get; set; } public IconElementDto iconElement { get; set; } }
        private class IconElementDto { public int iconID { get; set; } }

        private class DetailsLayoutDto
        {
            public int tabIndex { get; set; }
            public string tabName { get; set; }
            public List<DetailDto> details { get; set; }
        }
        private class DetailDto { public int id { get; set; } public string name { get; set; } public string content { get; set; } }

        private class GalleryLayoutDto
        {
            public int tabIndex { get; set; }
            public string tabName { get; set; }
            public List<GalleryImageDto> images { get; set; }
        }
        private class GalleryImageDto { public int index { get; set; } public string url { get; set; } public string tooltip { get; set; } public bool nsfw { get; set; } public bool trigger { get; set; } public byte[] imageBytes { get; set; } }

        private class InfoLayoutDto { public int tabIndex { get; set; } public string tabName { get; set; } public string text { get; set; } }

        private class StoryLayoutDto
        {
            public int tabIndex { get; set; }
            public string tabName { get; set; }
            public List<StoryChapterDto> chapters { get; set; }
        }
        private class StoryChapterDto { public int id { get; set; } public string title { get; set; } public string content { get; set; } }

        private class InventoryLayoutDto
        {
            public int tabIndex { get; set; }
            public string tabName { get; set; }
            public Dictionary<int, ItemDefDto> inventorySlotContents { get; set; }
        }
        private class ItemDefDto { public string name { get; set; } public string description { get; set; } public int type { get; set; } public int subtype { get; set; } public int iconID { get; set; } public int slot { get; set; } public int quality { get; set; } }

        private class TreeLayoutDto
        {
            public int tabIndex { get; set; }
            public string tabName { get; set; }
            public List<RelationshipDto> relationships { get; set; }
            public List<TupleCoordDto> Connections { get; set; }
            public List<List<CoordDto>> Paths { get; set; }                 // changed: list of lists of coords
            public List<List<TupleCoordDto>> PathConnections { get; set; } // list of lists of connection pairs
        }
        private class RelationshipDto
        {
            public string Name { get; set; }
            public Vector4 NameColor { get; set; }
            public string Description { get; set; }
            public Vector4 DescriptionColor { get; set; }
            public int IconID { get; set; }
            public Vector4 LineColor { get; set; }
            public float LineThickness { get; set; }
            public bool active { get; set; }
            public List<RelationshipLinkDto> Links { get; set; }
            public CoordDto slot { get; set; } // added: serialize relationship slot coordinates
        }
        private class RelationshipLinkDto { public CoordDto from { get; set; } public CoordDto to { get; set; } }
        private class CoordDto { public int x { get; set; } public int y { get; set; } }
        private class TupleCoordDto { public CoordDto from { get; set; } public CoordDto to { get; set; } }

        // ----- Small converters -----
        private class Vector4Converter : JsonConverter<Vector4>
        {
            public override Vector4 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject) return Vector4.Zero;
                float x = 0, y = 0, z = 0, w = 0;
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject) break;
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        var prop = reader.GetString();
                        reader.Read();
                        switch (prop)
                        {
                            case "x": x = (float)reader.GetDouble(); break;
                            case "y": y = (float)reader.GetDouble(); break;
                            case "z": z = (float)reader.GetDouble(); break;
                            case "w": w = (float)reader.GetDouble(); break;
                        }
                    }
                }
                return new Vector4(x, y, z, w);
            }

            public override void Write(Utf8JsonWriter writer, Vector4 value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteNumber("x", value.X);
                writer.WriteNumber("y", value.Y);
                writer.WriteNumber("z", value.Z);
                writer.WriteNumber("w", value.W);
                writer.WriteEndObject();
            }
        }
    }
}