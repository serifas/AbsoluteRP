using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Numerics;
using System.Security.Cryptography;
using AbsoluteRP;

namespace AbsoluteRP.Caching
{
    public static class ProfilesCache
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new Vector4Converter()
            }
        };

        private static string GetCacheDirectory()
        {
            var basePath = Plugin.PluginInterface?.AssemblyLocation?.Directory?.FullName;
            if (string.IsNullOrEmpty(basePath))
            {
                Plugin.PluginLog?.Debug("[ProfilesCache] Could not determine assembly path.");
                return null;
            }

            var cacheDir = Path.Combine(basePath, "cache", "profiles");
            Directory.CreateDirectory(cacheDir);
            return cacheDir;
        }

        private static string SanitizeFileNameSegment(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "unknown";
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(input.Length);
            foreach (var c in input)
            {
                if (invalid.Contains(c)) sb.Append('_');
                else if (char.IsWhiteSpace(c)) sb.Append('_');
                else sb.Append(c);
            }
            // collapse repeated underscores
            var result = sb.ToString();
            while (result.Contains("__")) result = result.Replace("__", "_");
            return result;
        }

        /// <summary>
        /// Save a ProfileData instance to a JSON cache file.
        /// By default writes the legacy filename pattern:
        ///   profile_{index}.json or profile_{timestamp}.json
        /// If playerName and playerWorld are provided the file will additionally be written
        /// with the player segments included so it can be loaded by name/world:
        ///   profile_{index}_{playerName}_{playerWorld}.json
        ///   profile_{playerName}_{playerWorld}_{timestamp}.json
        /// </summary>
        public static bool SaveProfileCache(ProfileData profile, string playerName = null, string playerWorld = null)
        {
            try
            {
                if (profile == null) throw new ArgumentNullException(nameof(profile));

                var dto = ProfileDto.FromProfile(profile);

                var cacheDir = GetCacheDirectory();
                if (cacheDir == null) return false;

                // Legacy / primary filename
                string baseFilename = profile.index > 0
                    ? $"profile_{profile.index}.json"
                    : $"profile_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.json";

                var baseFullPath = Path.Combine(cacheDir, baseFilename);
                var json = JsonSerializer.Serialize(dto, JsonOptions);
                File.WriteAllText(baseFullPath, json);

                Plugin.PluginLog?.Debug($"[ProfilesCache] Saved profile cache to {baseFullPath}");

                // If player name/world provided, also write a name-based filename so it can be found by player+world search
                if (!string.IsNullOrWhiteSpace(playerName) && !string.IsNullOrWhiteSpace(playerWorld))
                {
                    var nameSeg = SanitizeFileNameSegment(playerName);
                    var worldSeg = SanitizeFileNameSegment(playerWorld);

                    string nameFilename;
                    if (profile.index > 0)
                        nameFilename = $"profile_{profile.index}_{nameSeg}_{worldSeg}.json";
                    else
                        nameFilename = $"profile_{nameSeg}_{worldSeg}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.json";

                    var nameFullPath = Path.Combine(cacheDir, nameFilename);
                    File.WriteAllText(nameFullPath, json);
                    Plugin.PluginLog?.Debug($"[ProfilesCache] Saved profile cache (name/world) to {nameFullPath}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Plugin.PluginLog?.Debug($"[ProfilesCache] Failed saving profile cache: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Try to load a cached profile by index. Returns null if file not found or deserialization fails.
        /// </summary>
        public static ProfileData? LoadProfileCache(int index)
        {
            try
            {
                var cacheDir = GetCacheDirectory();
                if (cacheDir == null) return null;

                var fullPath = Path.Combine(cacheDir, $"profile_{index}.json");
                if (!File.Exists(fullPath)) return null;

                var json = File.ReadAllText(fullPath);
                var dto = JsonSerializer.Deserialize<ProfileDto>(json, JsonOptions);
                if (dto == null) return null;

                return dto.ToProfile();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog?.Debug($"[ProfilesCache] Failed loading profile cache: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Try to load a cached profile by playerName and playerWorld.
        /// Will search cache files for filenames containing the sanitized playerName and playerWorld.
        /// If multiple matches are found the most recently written file is returned.
        /// </summary>
        public static ProfileData? LoadProfileCache(string playerName, string playerWorld)
        {
            try
            {
                var cacheDir = GetCacheDirectory();
                if (cacheDir == null) return null;

                var nameSeg = SanitizeFileNameSegment(playerName);
                var worldSeg = SanitizeFileNameSegment(playerWorld);

                // Look for files that contain both segments in the filename (case-insensitive)
                var files = Directory.EnumerateFiles(cacheDir, "*.json", SearchOption.TopDirectoryOnly)
                    .Where(f =>
                    {
                        var fn = Path.GetFileNameWithoutExtension(f);
                        return fn != null
                            && fn.IndexOf(nameSeg, StringComparison.OrdinalIgnoreCase) >= 0
                            && fn.IndexOf(worldSeg, StringComparison.OrdinalIgnoreCase) >= 0;
                    })
                    .ToList();

                if (files.Count == 0) return null;

                // prefer the newest file (by last write time)
                var chosen = files.OrderByDescending(File.GetLastWriteTimeUtc).First();

                var json = File.ReadAllText(chosen);
                var dto = JsonSerializer.Deserialize<ProfileDto>(json, JsonOptions);
                if (dto == null) return null;

                Plugin.PluginLog?.Debug($"[ProfilesCache] Loaded profile cache from {chosen}");
                return dto.ToProfile();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog?.Debug($"[ProfilesCache] Failed loading profile cache by name/world: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Check whether a cached profile file exists for the given player name and world.
        /// Returns true when at least one cache file contains both sanitized name and world segments.
        /// </summary>
        public static bool CacheExistsForPlayer(string playerName, string playerWorld)
        {
            try
            {
                var cacheDir = GetCacheDirectory();
                if (cacheDir == null) return false;

                var nameSeg = SanitizeFileNameSegment(playerName);
                var worldSeg = SanitizeFileNameSegment(playerWorld);

                return Directory.EnumerateFiles(cacheDir, "*.json", SearchOption.TopDirectoryOnly)
                    .Any(f =>
                    {
                        var fn = Path.GetFileNameWithoutExtension(f);
                        return !string.IsNullOrEmpty(fn)
                            && fn.IndexOf(nameSeg, StringComparison.OrdinalIgnoreCase) >= 0
                            && fn.IndexOf(worldSeg, StringComparison.OrdinalIgnoreCase) >= 0;
                    });
            }
            catch (Exception ex)
            {
                Plugin.PluginLog?.Debug($"[ProfilesCache] CacheExistsForPlayer failed: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Compare two ProfileData instances for equality of cached-relevant values.
        /// Uses the same DTO used for serialization so runtime-only fields (textures, caches) are ignored.
        /// Returns true if profiles are equivalent (no changes), false otherwise.
        /// </summary>
        public static bool ProfilesEqual(ProfileData a, ProfileData b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;

            try
            {
                var dtoA = ProfileDto.FromProfile(a);
                var dtoB = ProfileDto.FromProfile(b);

                // Canonical JSON comparison using the same options as caching.
                var jsonA = JsonSerializer.Serialize(dtoA, JsonOptions);
                var jsonB = JsonSerializer.Serialize(dtoB, JsonOptions);

                return string.Equals(jsonA, jsonB, StringComparison.Ordinal);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog?.Debug($"[ProfilesCache] ProfilesEqual failed: {ex}");
                return false;
            }
        }

        // --- DTOs and helpers: convert ProfileData into a JSON friendly structure (no texture-wrap or runtime-only objects) ---

        private class ProfileDto
        {
            public int Index { get; set; }
            public bool ShowOnCompass { get; set; }
            public bool Nsfw { get; set; }
            public bool Triggering { get; set; }
            public bool SpoilerARR { get; set; }
            public bool SpoilerHW { get; set; }
            public bool SpoilerSB { get; set; }
            public bool SpoilerSHB { get; set; }
            public bool SpoilerEW { get; set; }
            public bool SpoilerDT { get; set; }

            public byte[] AvatarBytes { get; set; } = Array.Empty<byte>();
            public byte[] BackgroundBytes { get; set; } = Array.Empty<byte>();

            public string Title { get; set; } = string.Empty;
            public Vector4 TitleColor { get; set; } = new Vector4(1, 1, 1, 1);

            public bool IsPrivate { get; set; }
            public bool IsActive { get; set; }

            public string OOC { get; set; } = string.Empty;

            public List<CustomTabDto> CustomTabs { get; set; } = new();

            public static ProfileDto FromProfile(ProfileData p)
            {
                var dto = new ProfileDto
                {
                    Index = p.index,
                    ShowOnCompass = p.SHOW_ON_COMPASS,
                    Nsfw = p.NSFW,
                    Triggering = p.TRIGGERING,
                    SpoilerARR = p.SpoilerARR,
                    SpoilerHW = p.SpoilerHW,
                    SpoilerSB = p.SpoilerSB,
                    SpoilerSHB = p.SpoilerSHB,
                    SpoilerEW = p.SpoilerEW,
                    SpoilerDT = p.SpoilerDT,
                    AvatarBytes = p.avatarBytes ?? Array.Empty<byte>(),
                    BackgroundBytes = p.backgroundBytes ?? Array.Empty<byte>(),
                    Title = p.title ?? string.Empty,
                    TitleColor = p.titleColor,
                    IsPrivate = p.isPrivate,
                    IsActive = p.isActive,
                    OOC = p.OOC ?? string.Empty,
                };

                if (p.customTabs != null)
                {
                    foreach (var tab in p.customTabs)
                        dto.CustomTabs.Add(CustomTabDto.FromCustomTab(tab));
                }

                return dto;
            }

            public ProfileData ToProfile()
            {
                var p = new ProfileData
                {
                    index = Index,
                    SHOW_ON_COMPASS = ShowOnCompass,
                    NSFW = Nsfw,
                    TRIGGERING = Triggering,
                    SpoilerARR = SpoilerARR,
                    SpoilerHW = SpoilerHW,
                    SpoilerSB = SpoilerSB,
                    SpoilerSHB = SpoilerSHB,
                    SpoilerEW = SpoilerEW,
                    SpoilerDT = SpoilerDT,
                    avatarBytes = AvatarBytes ?? Array.Empty<byte>(),
                    backgroundBytes = BackgroundBytes ?? Array.Empty<byte>(),
                    title = Title,
                    titleColor = TitleColor,
                    isPrivate = IsPrivate,
                    isActive = IsActive,
                    OOC = OOC ?? string.Empty,
                    customTabs = new List<CustomTab>()
                };

                if (CustomTabs != null)
                {
                    foreach (var t in CustomTabs)
                        p.customTabs.Add(t.ToCustomTab());
                }

                return p;
            }
        }

        private class CustomTabDto
        {
            public int ID { get; set; }
            public string Name { get; set; } = string.Empty;
            public bool IsOpen { get; set; }
            public bool ShowPopup { get; set; }
            public int Type { get; set; }
            public CustomLayoutDto? Layout { get; set; }

            public static CustomTabDto FromCustomTab(CustomTab t)
            {
                return new CustomTabDto
                {
                    ID = t.ID,
                    Name = t.Name ?? string.Empty,
                    IsOpen = t.IsOpen,
                    ShowPopup = t.ShowPopup,
                    Type = t.type,
                    Layout = CustomLayoutDto.FromLayout(t.Layout)
                };
            }

            public CustomTab ToCustomTab()
            {
                return new CustomTab
                {
                    ID = ID,
                    Name = Name ?? string.Empty,
                    IsOpen = IsOpen,
                    ShowPopup = ShowPopup,
                    type = Type,
                    Layout = Layout?.ToLayout()
                };
            }
        }

        private class CustomLayoutDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string LayoutType { get; set; } = string.Empty;
            // Polymorphic, include a few commonly used layout payloads
            public BioLayoutDto? Bio { get; set; }
            public GalleryLayoutDto? Gallery { get; set; }
            public RosterLayoutDto? Roster { get; set; }
            public InfoLayoutDto? Info { get; set; }
            public DetailsLayoutDto? Details { get; set; }

            public static CustomLayoutDto? FromLayout(CustomLayout? layout)
            {
                if (layout == null) return null;
                var dto = new CustomLayoutDto
                {
                    Id = layout.id,
                    Name = layout.name ?? string.Empty,
                    LayoutType = layout.layoutType.ToString()
                };

                switch (layout)
                {
                    case BioLayout b:
                        dto.Bio = BioLayoutDto.FromBio(b);
                        break;
                    case GalleryLayout g:
                        dto.Gallery = GalleryLayoutDto.FromGallery(g);
                        break;
                    case RosterLayout r:
                        dto.Roster = RosterLayoutDto.FromRoster(r);
                        break;
                    case InfoLayout i:
                        dto.Info = InfoLayoutDto.FromInfo(i);
                        break;
                    case DetailsLayout d:
                        dto.Details = DetailsLayoutDto.FromDetails(d);
                        break;
                }

                return dto;
            }

            public CustomLayout? ToLayout()
            {
                // Re-create minimal layout instances; texture/runtime-only fields will remain default/null
                if (Enum.TryParse<LayoutTypes>(LayoutType, out var lt))
                {
                    switch (lt)
                    {
                        case LayoutTypes.Bio:
                            var bio = new BioLayout
                            {
                                id = Id,
                                name = Name,
                                layoutType = lt
                            };
                            if (Bio != null)
                            {
                                bio.name = Bio.Name ?? string.Empty;
                                bio.race = Bio.Race ?? string.Empty;
                                bio.gender = Bio.Gender ?? string.Empty;
                                bio.age = Bio.Age ?? string.Empty;
                                bio.height = Bio.Height ?? string.Empty;
                                bio.weight = Bio.Weight ?? string.Empty;
                                bio.afg = Bio.Afg ?? string.Empty;
                                bio.alignment = Bio.Alignment;
                                bio.personality_1 = Bio.Personality1;
                                bio.personality_2 = Bio.Personality2;
                                bio.personality_3 = Bio.Personality3;
                                bio.descriptors = Bio.Descriptors?.ConvertAll(d => new descriptor { index = d.Index, name = d.Name, description = d.Description }) ?? new List<descriptor>();
                                bio.traits = Bio.Traits?.ConvertAll(t => new trait { index = t.Index, name = t.Name, description = t.Description, iconID = t.IconID }) ?? new List<trait>();
                                bio.fields = Bio.Fields?.ConvertAll(f => new field { index = f.Index, name = f.Name, description = f.Description }) ?? new List<field>();
                            }
                            return bio;

                        case LayoutTypes.Gallery:
                            var gallery = new GalleryLayout { id = Id, name = Name, layoutType = lt };
                            if (Gallery?.Images != null)
                            {
                                gallery.images = Gallery.Images.ConvertAll(i => new ProfileGalleryImage
                                {
                                    index = i.Index,
                                    url = i.Url ?? string.Empty,
                                    tooltip = i.Tooltip ?? string.Empty,
                                    nsfw = i.Nsfw,
                                    trigger = i.Trigger,
                                    imageBytes = i.ImageBytes ?? Array.Empty<byte>()
                                });
                            }
                            return gallery;

                        case LayoutTypes.Roster:
                            var roster = new RosterLayout { id = Id, name = Name, layoutType = lt };
                            if (Roster?.Members != null)
                            {
                                roster.members = Roster.Members.ConvertAll(mi => new ProfileData { index = mi });
                            }
                            if (Roster?.Affiliates != null)
                            {
                                roster.affiliates = Roster.Affiliates.ConvertAll(ai => new ProfileData { index = ai });
                            }
                            return roster;

                        case LayoutTypes.Info:
                            var info = new InfoLayout { id = Id, name = Name, layoutType = lt };
                            if (Info != null) info.text = Info.Text ?? string.Empty;
                            return info;

                        case LayoutTypes.Details:
                            var details = new DetailsLayout { id = Id, name = Name, layoutType = lt };
                            if (Details?.DetailsList != null)
                                details.details = Details.DetailsList.ConvertAll(d => new Detail { id = d.Id, name = d.Name ?? string.Empty, content = d.Content ?? string.Empty });
                            return details;

                        default:
                            return new CustomLayout { id = Id, name = Name, layoutType = lt };
                    }
                }

                return null;
            }
        }

        private class BioLayoutDto
        {
            public string Name { get; set; } = string.Empty;
            public string Race { get; set; } = string.Empty;
            public string Gender { get; set; } = string.Empty;
            public string Age { get; set; } = string.Empty;
            public string Height { get; set; } = string.Empty;
            public string Weight { get; set; } = string.Empty;
            public string Afg { get; set; } = string.Empty;
            public int Alignment { get; set; }
            public int Personality1 { get; set; }
            public int Personality2 { get; set; }
            public int Personality3 { get; set; }

            public List<SimpleDescriptorDto> Descriptors { get; set; } = new();
            public List<SimpleTraitDto> Traits { get; set; } = new();
            public List<SimpleFieldDto> Fields { get; set; } = new();

            public static BioLayoutDto FromBio(BioLayout b)
            {
                var dto = new BioLayoutDto
                {
                    Name = b.name ?? string.Empty,
                    Race = b.race ?? string.Empty,
                    Gender = b.gender ?? string.Empty,
                    Age = b.age ?? string.Empty,
                    Height = b.height ?? string.Empty,
                    Weight = b.weight ?? string.Empty,
                    Afg = b.afg ?? string.Empty,
                    Alignment = b.alignment,
                    Personality1 = b.personality_1,
                    Personality2 = b.personality_2,
                    Personality3 = b.personality_3
                };

                if (b.descriptors != null)
                    dto.Descriptors.AddRange(b.descriptors.ConvertAll(d => new SimpleDescriptorDto { Index = d.index, Name = d.name ?? string.Empty, Description = d.description ?? string.Empty }));

                if (b.traits != null)
                    dto.Traits.AddRange(b.traits.ConvertAll(t => new SimpleTraitDto { Index = t.index, Name = t.name ?? string.Empty, Description = t.description ?? string.Empty, IconID = t.iconID }));

                if (b.fields != null)
                    dto.Fields.AddRange(b.fields.ConvertAll(f => new SimpleFieldDto { Index = f.index, Name = f.name ?? string.Empty, Description = f.description ?? string.Empty }));

                return dto;
            }
        }

        private class GalleryLayoutDto
        {
            public List<GalleryImageDto> Images { get; set; } = new();

            public static GalleryLayoutDto FromGallery(GalleryLayout g)
            {
                var dto = new GalleryLayoutDto();
                if (g.images != null)
                {
                    foreach (var i in g.images)
                        dto.Images.Add(new GalleryImageDto
                        {
                            Index = i.index,
                            Url = i.url ?? string.Empty,
                            Tooltip = i.tooltip ?? string.Empty,
                            Nsfw = i.nsfw,
                            Trigger = i.trigger,
                            ImageBytes = i.imageBytes ?? Array.Empty<byte>()
                        });
                }
                return dto;
            }
        }

        private class RosterLayoutDto
        {
            public List<int> Members { get; set; } = new();
            public List<int> Affiliates { get; set; } = new();

            public static RosterLayoutDto FromRoster(RosterLayout r)
            {
                var dto = new RosterLayoutDto();
                if (r.members != null) dto.Members.AddRange(r.members.ConvertAll(m => m.index));
                if (r.affiliates != null) dto.Affiliates.AddRange(r.affiliates.ConvertAll(a => a.index));
                return dto;
            }
        }

        private class InfoLayoutDto
        {
            public string Text { get; set; } = string.Empty;
            public static InfoLayoutDto FromInfo(InfoLayout i) => new InfoLayoutDto { Text = i.text ?? string.Empty };
        }

        private class DetailsLayoutDto
        {
            public List<DetailDto> DetailsList { get; set; } = new();
            public static DetailsLayoutDto FromDetails(DetailsLayout d)
            {
                var dto = new DetailsLayoutDto();
                if (d.details != null)
                    dto.DetailsList.AddRange(d.details.ConvertAll(x => new DetailDto { Id = x.id, Name = x.name ?? string.Empty, Content = x.content ?? string.Empty }));
                return dto;
            }
        }

        private class SimpleDescriptorDto { public int Index { get; set; } public string Name { get; set; } = string.Empty; public string Description { get; set; } = string.Empty; }
        private class SimpleTraitDto { public int Index { get; set; } public string Name { get; set; } = string.Empty; public string Description { get; set; } = string.Empty; public int IconID { get; set; } }
        private class SimpleFieldDto { public int Index { get; set; } public string Name { get; set; } = string.Empty; public string Description { get; set; } = string.Empty; }

        private class GalleryImageDto { public int Index { get; set; } public string Url { get; set; } = string.Empty; public string Tooltip { get; set; } = string.Empty; public bool Nsfw { get; set; } public bool Trigger { get; set; } public byte[] ImageBytes { get; set; } = Array.Empty<byte>(); }
        private class DetailDto { public int Id { get; set; } public string Name { get; set; } = string.Empty; public string Content { get; set; } = string.Empty; }

        // --- Json converter for Vector4 ---
        private class Vector4Converter : JsonConverter<Vector4>
        {
            public override Vector4 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartArray) return new Vector4(1, 1, 1, 1);
                var vals = new List<float>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType == JsonTokenType.Number && reader.TryGetSingle(out var f)) vals.Add(f);
                }
                if (vals.Count >= 4) return new Vector4(vals[0], vals[1], vals[2], vals[3]);
                if (vals.Count == 3) return new Vector4(vals[0], vals[1], vals[2], 1f);
                return new Vector4(1, 1, 1, 1);
            }

            public override void Write(Utf8JsonWriter writer, Vector4 value, JsonSerializerOptions options)
            {
                writer.WriteStartArray();
                writer.WriteNumberValue(value.X);
                writer.WriteNumberValue(value.Y);
                writer.WriteNumberValue(value.Z);
                writer.WriteNumberValue(value.W);
                writer.WriteEndArray();
            }
        }
    }
}