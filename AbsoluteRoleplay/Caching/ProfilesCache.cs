using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using Dalamud.Interface.Textures.TextureWraps;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace AbsoluteRP.Caching
{
    internal class ProfilesCache
    {
        const string ProfilesFolderName = "Profiles";
        const string IndexFileName = "profiles_index.json";

        public static List<ProfileData> personalProfiles = new List<ProfileData>();
        public static List<ProfileData> targetProfiles = new List<ProfileData>();

        public static void CacheProfile(bool personal, ProfileData profile, Account account = null)
        {
            try
            {
                // Replace any existing cached tooltipData with same server index so we don't accumulate stale entries
                if (personal)
                {
                    try { personalProfiles.RemoveAll(p => p.index == profile.index); } catch { }
                    personalProfiles.Add(profile);
                }
                else
                {
                    try { targetProfiles.RemoveAll(p => p.index == profile.index); } catch { }
                    targetProfiles.Add(profile);
                }

                SaveCachedProfile(personal, profile, account);
            }
            catch (Exception ex)
            {
                try { Plugin.PluginLog.Error($"ProfilesCache.CacheProfile failed: {ex}"); } catch { }
            }
        }
        private static string ResolveBasePath()
        {
            // 1) Prefer the plugin-specific config directory provided by Dalamud
            try
            {
                var pluginConfigDir = Plugin.PluginInterface?.GetPluginConfigDirectory();
                if (!string.IsNullOrWhiteSpace(pluginConfigDir))
                {
                    try
                    {
                        // Ensure plugin config directory exists
                        Directory.CreateDirectory(pluginConfigDir);

                        // Use an ARPData subfolder inside the plugin config directory
                        var arpDataPath = Path.Combine(pluginConfigDir, "ARPData");
                        Directory.CreateDirectory(arpDataPath);

                        try { Plugin.PluginLog.Error($"ProfilesCache: ResolveBasePath -> '{arpDataPath}' (GetPluginConfigDirectory)"); } catch { }
                        return arpDataPath;
                    }
                    catch (Exception ex)
                    {
                        try { Plugin.PluginLog.Error($"ProfilesCache: Failed creating ARPData in plugin config dir '{pluginConfigDir}': {ex}"); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                try { Plugin.PluginLog.Error($"ProfilesCache: GetPluginConfigDirectory() check failed: {ex}"); } catch { }
            }

            // 2) Fallback to assembly directory ARPData (existing behavior)
            try
            {
                if (Plugin.PluginInterface is { AssemblyLocation.Directory.FullName: { } path })
                {
                    var pluginDataPath = Path.Combine(path, "ARPData");
                    try { Directory.CreateDirectory(pluginDataPath); } catch { }
                    try { Plugin.PluginLog.Error($"ProfilesCache: ResolveBasePath -> '{pluginDataPath}' (assembly dir)"); } catch { }
                    return pluginDataPath;
                }
            }
            catch (Exception ex)
            {
                try { Plugin.PluginLog.Error($"ProfilesCache: ResolveBasePath assembly path check failed: {ex}"); } catch { }
            }

            // 3) Configured path (if set)
            try
            {
                var configured = Plugin.plugin?.Configuration?.dataSavePath;
                if (!string.IsNullOrWhiteSpace(configured))
                {
                    try { Directory.CreateDirectory(configured); } catch { }
                    try { Plugin.PluginLog.Error($"ProfilesCache: ResolveBasePath -> '{configured}' (configured)"); } catch { }
                    return configured;
                }
            }
            catch (Exception ex)
            {
                try { Plugin.PluginLog.Error($"ProfilesCache: ResolveBasePath configured path attempt failed: {ex}"); } catch { }
            }

            // 4) Final fallback
            var fallback = Path.Combine(AppContext.BaseDirectory ?? ".", "ARPProfileData");
            try { Directory.CreateDirectory(fallback); } catch { }
            try { Plugin.PluginLog.Error($"ProfilesCache: ResolveBasePath -> '{fallback}' (fallback)"); } catch { }
            return fallback;
        }
        private static string SanitizeFileName(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(input.Length);
            foreach (var c in input)
            {
                if (Array.IndexOf(invalid, c) >= 0) sb.Append('_'); else sb.Append(c);
            }
            var s = sb.ToString().Trim();
            if (s.Length > 100) s = s.Substring(0, 100);
            return s;
        }
        public static void PersistAllPersonalProfiles(Account account = null)
        {
            try
            {
                if (personalProfiles == null || personalProfiles.Count == 0)
                {
                    try { Plugin.PluginLog.Error("ProfilesCache: no personal profiles to persist."); } catch { }
                    return;
                }

                try { Plugin.PluginLog.Error($"ProfilesCache: PersistAllPersonalProfiles starting ({personalProfiles.Count} profiles)"); } catch { }

                // iterate a copy to avoid collection-modification issues
                var copy = personalProfiles.ToList();
                foreach (var profile in copy)
                {
                    if (profile == null)
                        continue;

                    try
                    {
                        // Use existing SaveCachedProfile logic (it handles writing layout files and image bins)
                        SaveCachedProfile(true, profile, account);
                        try { Plugin.PluginLog.Error($"ProfilesCache: Persisted tooltipData index={profile.index} title='{profile.title}'"); } catch { }
                    }
                    catch (Exception exProfile)
                    {
                        try { Plugin.PluginLog.Error($"ProfilesCache: Persist tooltipData index={profile.index} failed: {exProfile}"); } catch { }
                    }
                }

                try { Plugin.PluginLog.Error("ProfilesCache: PersistAllPersonalProfiles completed."); } catch { }
            }
            catch (Exception ex)
            {
                try { Plugin.PluginLog.Error($"ProfilesCache.PersistAllPersonalProfiles top-level failed: {ex}"); } catch { }
            }
        }
        private static void SaveCachedProfile(bool personal, ProfileData profile, Account account = null)
        {
            try
            {
                var basePath = ResolveBasePath();
                try { Directory.CreateDirectory(basePath); } catch (Exception ex) { try { Plugin.PluginLog.Error($"ProfilesCache: Could not ensure basePath '{basePath}': {ex}"); } catch { } }

                try { Plugin.PluginLog.Error($"ProfilesCache: saving to basePath='{basePath}'"); } catch { }
                var profilesPath = Path.Combine(basePath, ProfilesFolderName);
                try { Directory.CreateDirectory(profilesPath); } catch (Exception ex) { try { Plugin.PluginLog.Error($"ProfilesCache: Could not ensure profilesPath '{profilesPath}': {ex}"); } catch { } }

                var opts = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };

                // Build a compact JSON for the tooltipData (do not inline large binaries or complex customTabs layouts)
                var node = new JsonObject();

                // Core ProfileData fields
                node["index"] = profile?.index ?? -1;
                node["profileTitle"] = profile?.title ?? string.Empty;
                node["title"] = profile?.title ?? string.Empty;
                node["titleColor"] = new JsonArray(
                    profile?.titleColor.X ?? 1f,
                    profile?.titleColor.Y ?? 1f,
                    profile?.titleColor.Z ?? 1f,
                    profile?.titleColor.W ?? 1f
                );

                node["isPrivate"] = profile?.isPrivate ?? false;
                // both variants kept for compatibility
                node["isActive"] = profile?.isActive ?? false;
                // additional flags
                try
                {
                    node["showOnCompass"] = profile?.SHOW_ON_COMPASS ?? false;
                    node["nsfw"] = profile?.NSFW ?? false;
                    node["triggering"] = profile?.TRIGGERING ?? false;
                    node["spoilerARR"] = profile?.SpoilerARR ?? false;
                    node["spoilerHW"] = profile?.SpoilerHW ?? false;
                    node["spoilerSB"] = profile?.SpoilerSB ?? false;
                    node["spoilerSHB"] = profile?.SpoilerSHB ?? false;
                    node["spoilerEW"] = profile?.SpoilerEW ?? false;
                    node["spoilerDT"] = profile?.SpoilerDT ?? false;
                }
                catch { }

                // Try to capture avatar/background raw bytes (explicit properties)
                try
                {
                    if (profile != null)
                    {
                        if (profile.avatarBytes != null && profile.avatarBytes.Length > 0)
                            node["avatarBytes"] = Convert.ToBase64String(profile.avatarBytes);

                        if (profile.backgroundBytes != null && profile.backgroundBytes.Length > 0)
                            node["backgroundBytes"] = Convert.ToBase64String(profile.backgroundBytes);
                    }
                }
                catch { /* non-fatal */ }

                // Presence/size metadata (if textures available)
                try
                {
                    node["avatarPresent"] = profile?.avatar != null && profile.avatar.Handle != IntPtr.Zero;
                    if (node["avatarPresent"].GetValue<bool>() && profile.avatar != null)
                        node["avatarSize"] = new JsonArray(profile.avatar.Width, profile.avatar.Height);
                }
                catch { }

                try
                {
                    node["backgroundPresent"] = profile?.background != null && profile.background.Handle != IntPtr.Zero;
                    if (node["backgroundPresent"].GetValue<bool>() && profile.background != null)
                        node["backgroundSize"] = new JsonArray(profile.background.Width, profile.background.Height);
                }
                catch { }

                // Try to serialize simple lists/objects that may exist on ProfileData (best-effort)
                try { node["customTabs"] = null; } catch { }

                // Helper to safely write text files and log errors
                static void SafeWriteAllText(string path, string contents)
                {
                    try
                    {
                        File.WriteAllText(path, contents);
                        try { Plugin.PluginLog.Error($"ProfilesCache: Wrote file '{path}'"); } catch { }
                    }
                    catch (Exception ex)
                    {
                        try { Plugin.PluginLog.Error($"ProfilesCache: Failed writing file '{path}': {ex}"); } catch { }
                    }
                }

                // Store customTabs metadata and write full layout files + binary image files into a dedicated folder for this tooltipData
                try
                {
                    if (profile?.customTabs != null)
                    {
                        // Determine safe base filename for the tooltipData
                        var safeName = SanitizeFileName(profile?.title ?? $"profile_{profile?.index ?? -1}");
                        var filename = $"profile_{(profile?.index ?? -1)}_{safeName}_{Guid.NewGuid():N}.json";
                        var profileFilePath = Path.Combine(profilesPath, filename);

                        var profileFolderName = Path.GetFileNameWithoutExtension(filename);
                        var profileFolderPath = Path.Combine(profilesPath, profileFolderName);
                        try { Directory.CreateDirectory(profileFolderPath); } catch (Exception ex) { try { Plugin.PluginLog.Error($"ProfilesCache: Could not ensure profileFolderPath '{profileFolderPath}': {ex}"); } catch { } }

                        // We'll write each custom tab's layout to a separate file inside profileFolderPath
                        var tabsArray = new JsonArray();
                        for (int tIndex = 0; tIndex < profile.customTabs.Count; tIndex++)
                        {
                            var tab = profile.customTabs[tIndex];
                            var tabObj = new JsonObject
                            {
                                ["id"] = tab?.ID ?? tIndex
                            };

                            if (tab?.Layout != null)
                            {
                                var lt = tab.Layout.GetType();
                                tabObj["layoutType"] = lt.AssemblyQualifiedName ?? lt.FullName;

                                // Layout filename
                                var layoutFileName = $"tab_{tIndex}_layout.json";
                                tabObj["layoutFile"] = layoutFileName;

                                // Special handling: GalleryLayout -> ensure we persist image bytes (try to fetch if missing)
                                try
                                {
                                    if (tab.Layout is GalleryLayout gallery)
                                    {
                                        // Ensure images list initialized
                                        gallery.images = gallery.images ?? new List<ProfileGalleryImage>();

                                        // For each gallery image, if we don't have imageBytes try to fetch using the URL (best-effort)
                                        for (int imgIndex = 0; imgIndex < gallery.images.Count; imgIndex++)
                                        {
                                            var img = gallery.images[imgIndex];
                                            try
                                            {
                                                if ((img.imageBytes == null || img.imageBytes.Length == 0) && !string.IsNullOrWhiteSpace(img.url))
                                                {
                                                    try
                                                    {
                                                        // Best-effort synchronous fetch — fetch bytes and assign so cache persists them
                                                        var fetched = Imaging.FetchUrlImageBytes(img.url).GetAwaiter().GetResult();
                                                        if (fetched != null && fetched.Length > 0)
                                                        {
                                                            img.imageBytes = fetched;
                                                            try { Plugin.PluginLog.Error($"ProfilesCache: fetched image bytes for tooltipData {profile.index} tab {tIndex} img {imgIndex}"); } catch { }
                                                        }
                                                    }
                                                    catch (Exception exfetch)
                                                    {
                                                        try { Plugin.PluginLog.Error($"ProfilesCache: failed fetching image bytes for url '{img.url}': {exfetch}"); } catch { }
                                                    }
                                                }
                                            }
                                            catch { /* non-fatal per-image */ }
                                        }

                                        // Build a serializable layout node where large bytes are replaced by filenames
                                        var galleryNode = new JsonObject
                                        {
                                            ["tabName"] = gallery.tabName ?? string.Empty,
                                            ["tabIndex"] = gallery.tabIndex
                                        };
                                        var imgs = new JsonArray();
                                        for (int imgIndex = 0; imgIndex < (gallery.images?.Count ?? 0); imgIndex++)
                                        {
                                            var img = gallery.images[imgIndex];
                                            var imgObj = new JsonObject
                                            {
                                                ["index"] = img.index,
                                                ["url"] = img.url ?? string.Empty,
                                                ["tooltip"] = img.tooltip ?? string.Empty,
                                                ["nsfw"] = img.nsfw,
                                                ["trigger"] = img.trigger
                                            };

                                            // If imageBytes present -> write binary file
                                            try
                                            {
                                                if (img.imageBytes != null && img.imageBytes.Length > 0)
                                                {
                                                    var imgFileName = $"tab_{tIndex}_img_{imgIndex}.bin";
                                                    var imgPath = Path.Combine(profileFolderPath, imgFileName);
                                                    try
                                                    {
                                                        File.WriteAllBytes(imgPath, img.imageBytes);
                                                        imgObj["imageFile"] = imgFileName;
                                                        try { Plugin.PluginLog.Error($"ProfilesCache: Wrote image bin '{imgPath}'"); } catch { }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        try { Plugin.PluginLog.Error($"ProfilesCache: Failed writing image bin '{imgPath}': {ex}"); } catch { }
                                                    }
                                                }
                                            }
                                            catch { }

                                            imgs.Add(imgObj);
                                        }
                                        galleryNode["images"] = imgs;

                                        // write gallery layout file
                                        var layoutPath = Path.Combine(profileFolderPath, layoutFileName);
                                        SafeWriteAllText(layoutPath, galleryNode.ToJsonString(opts));
                                    }
                                    else
                                    {
                                        // For non-gallery layouts, attempt to serialize layout fully to file
                                        try
                                        {
                                            // Create a lightweight DTO copy to avoid serializing texture handles or other non-serializable runtime objects.
                                            // If layouts contain fields that can't be serialized, the SerializeToNode call below will fail and fallback will run.
                                            var layoutJson = JsonSerializer.SerializeToNode(tab.Layout, tab.Layout.GetType(), opts);
                                            var layoutPath = Path.Combine(profileFolderPath, layoutFileName);
                                            SafeWriteAllText(layoutPath, layoutJson?.ToJsonString(opts) ?? "{}");
                                        }
                                        catch
                                        {
                                            // fallback: attempt a generic serialization
                                            try
                                            {
                                                var layoutJson = JsonSerializer.SerializeToNode(tab.Layout, opts);
                                                var layoutPath = Path.Combine(profileFolderPath, layoutFileName);
                                                SafeWriteAllText(layoutPath, layoutJson?.ToJsonString(opts) ?? "{}");
                                            }
                                            catch (Exception ex)
                                            {
                                                try { Plugin.PluginLog.Error($"ProfilesCache: Failed serializing layout for tab '{tab?.Name}': {ex}"); } catch { }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    try { Plugin.PluginLog.Error($"ProfilesCache: Exception handling tab layout for '{tab?.Name}': {ex}"); } catch { }
                                }
                            }
                            else
                            {
                                tabObj["layoutType"] = string.Empty;
                                tabObj["layoutFile"] = null;
                            }

                            tabsArray.Add(tabObj);
                        }

                        node["customTabs"] = tabsArray;

                        // Include reference to tooltipData folder
                        node["profileFolder"] = profileFolderName;

                        // Attach account info for a personal tooltipData (if present)
                        if (personal && account != null)
                        {
                            var acct = new JsonObject
                            {
                                ["accountName"] = account.accountName ?? string.Empty,
                                ["accountKey"] = account.accountKey ?? string.Empty
                            };
                            try { acct["permissions"] = JsonSerializer.SerializeToNode(account.permissions, opts); } catch { }
                            node["account"] = acct;
                        }

                        // Write the single tooltipData file
                        try
                        {
                            SafeWriteAllText(profileFilePath, node.ToJsonString(opts));
                        }
                        catch (Exception ex)
                        {
                            try { Plugin.PluginLog.Error($"ProfilesCache: Failed to write tooltipData file '{profileFilePath}': {ex}"); } catch { }
                        }

                        // --- Update master index (profiles_index.json) ---
                        var indexPath = Path.Combine(ResolveBasePath(), IndexFileName);
                        JsonObject indexRoot;
                        if (File.Exists(indexPath))
                        {
                            try
                            {
                                var t = File.ReadAllText(indexPath);
                                indexRoot = JsonNode.Parse(t) as JsonObject ?? new JsonObject();
                            }
                            catch (Exception ex)
                            {
                                try { Plugin.PluginLog.Error($"ProfilesCache: Could not parse existing index file '{indexPath}': {ex}"); } catch { }
                                indexRoot = new JsonObject();
                            }
                        }
                        else indexRoot = new JsonObject();

                        if (indexRoot["personal"] == null) indexRoot["personal"] = new JsonArray();
                        if (indexRoot["viewed"] == null) indexRoot["viewed"] = new JsonArray();

                        var indexArray = (JsonArray)(personal ? indexRoot["personal"] : indexRoot["viewed"]);

                        // Remove previous entries for same tooltipData index if present
                        for (int i = indexArray.Count - 1; i >= 0; i--)
                        {
                            var e = indexArray[i] as JsonObject;
                            if (e == null) continue;
                            try
                            {
                                if (e["index"] != null && e["index"].GetValue<int>() == (profile?.index ?? -1)) indexArray.RemoveAt(i);
                            }
                            catch { }
                        }

                        // Add metadata entry
                        var meta = new JsonObject
                        {
                            ["file"] = filename,
                            ["index"] = profile?.index ?? -1,
                            ["title"] = profile?.title ?? string.Empty,
                            ["savedAtUtc"] = DateTime.UtcNow.ToString("o")
                        };
                        if (personal && account != null)
                        {
                            var a = new JsonObject { ["accountName"] = account.accountName ?? string.Empty };
                            meta["account"] = a;
                        }

                        indexArray.Add(meta);

                        // commit index
                        try
                        {
                            SafeWriteAllText(indexPath, indexRoot.ToJsonString(opts));
                        }
                        catch (Exception ex)
                        {
                            try { Plugin.PluginLog.Error($"ProfilesCache: Failed writing index file '{indexPath}': {ex}"); } catch { }
                        }

                        return; // we've already written file & index for this tooltipData branch
                    }
                }
                catch (Exception exTabs)
                {
                    try { Plugin.PluginLog.Error($"SaveCachedProfile(customTabs handling) failed: {exTabs}"); } catch { }
                }

                // If no customTabs (or previous branch failed), write a simple tooltipData file
                try
                {
                    var safeName2 = SanitizeFileName(profile?.title ?? "unknown");
                    var filename2 = $"profile_{(profile?.index ?? -1)}_{safeName2}_{Guid.NewGuid():N}.json";
                    var filePath = Path.Combine(ResolveBasePath(), ProfilesFolderName, filename2);
                    SafeWriteAllText(filePath, node.ToJsonString(opts));

                    // --- Update master index (profiles_index.json) ---
                    var indexPath2 = Path.Combine(ResolveBasePath(), IndexFileName);
                    JsonObject indexRoot2;
                    if (File.Exists(indexPath2))
                    {
                        try
                        {
                            var t = File.ReadAllText(indexPath2);
                            indexRoot2 = JsonNode.Parse(t) as JsonObject ?? new JsonObject();
                        }
                        catch { indexRoot2 = new JsonObject(); }
                    }
                    else indexRoot2 = new JsonObject();

                    if (indexRoot2["personal"] == null) indexRoot2["personal"] = new JsonArray();
                    if (indexRoot2["viewed"] == null) indexRoot2["viewed"] = new JsonArray();

                    var indexArray2 = (JsonArray)(personal ? indexRoot2["personal"] : indexRoot2["viewed"]);

                    // Remove previous entries for same tooltipData index if present
                    for (int i = indexArray2.Count - 1; i >= 0; i--)
                    {
                        var e = indexArray2[i] as JsonObject;
                        if (e == null) continue;
                        try
                        {
                            if (e["index"] != null && e["index"].GetValue<int>() == (profile?.index ?? -1)) indexArray2.RemoveAt(i);
                        }
                        catch { }
                    }

                    // Add metadata entry
                    var meta2 = new JsonObject
                    {
                        ["file"] = filename2,
                        ["index"] = profile?.index ?? -1,
                        ["title"] = profile?.title ?? string.Empty,
                        ["savedAtUtc"] = DateTime.UtcNow.ToString("o")
                    };
                    if (personal && account != null)
                    {
                        var a = new JsonObject { ["accountName"] = account.accountName ?? string.Empty };
                        meta2["account"] = a;
                    }

                    indexArray2.Add(meta2);

                    // commit index
                    try
                    {
                        SafeWriteAllText(indexPath2, indexRoot2.ToJsonString(opts));
                    }
                    catch (Exception exSimple)
                    {
                        try { Plugin.PluginLog.Error($"SaveCachedProfile(fallback) failed: {exSimple}"); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    try { Plugin.PluginLog.Error($"SaveCachedProfile failed: {ex}"); } catch { /* swallow */ }
                }
            }
            catch (Exception ex)
            {
                try { Plugin.PluginLog.Error($"SaveCachedProfile top-level failed: {ex}"); } catch { /* swallow */ }
            }
        }

        public static List<ProfileData> LoadCachedProfiles(bool personal)
        {
            var results = new List<ProfileData>();

            try
            {
                var basePath = ResolveBasePath();
                var indexPath = Path.Combine(basePath, IndexFileName);
                if (!File.Exists(indexPath)) return results;

                var text = File.ReadAllText(indexPath);
                var indexRoot = JsonNode.Parse(text) as JsonObject;
                if (indexRoot == null) return results;

                var listNode = personal ? indexRoot["personal"] as JsonArray : indexRoot["viewed"] as JsonArray;
                if (listNode == null) return results;

                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                foreach (var item in listNode)
                {
                    if (item is not JsonObject meta) continue;
                    var file = meta["file"]?.GetValue<string>();
                    if (string.IsNullOrEmpty(file)) continue;

                    var profilePath = Path.Combine(basePath, ProfilesFolderName, file);
                    if (!File.Exists(profilePath)) continue;

                    try
                    {
                        var profText = File.ReadAllText(profilePath);
                        var obj = JsonNode.Parse(profText) as JsonObject;
                        if (obj == null) continue;

                        var pd = new ProfileData();

                        // Basic ProfileData fields
                        pd.index = obj["index"]?.GetValue<int>() ?? -1;
                        pd.title = obj["title"]?.GetValue<string>() ?? string.Empty;

                        try
                        {
                            var tc = obj["titleColor"] as JsonArray;
                            if (tc != null && tc.Count >= 4) pd.titleColor = new System.Numerics.Vector4(tc[0].GetValue<float>(), tc[1].GetValue<float>(), tc[2].GetValue<float>(), tc[3].GetValue<float>());
                            else pd.titleColor = new System.Numerics.Vector4(1, 1, 1, 1);
                        }
                        catch { pd.titleColor = new System.Numerics.Vector4(1, 1, 1, 1); }

                        pd.isPrivate = obj["isPrivate"]?.GetValue<bool>() ?? false;
                        pd.isActive = obj["isActive"]?.GetValue<bool>() ?? pd.isActive;

                        // additional flags
                        try
                        {
                            pd.SHOW_ON_COMPASS = obj["showOnCompass"]?.GetValue<bool>() ?? pd.SHOW_ON_COMPASS;
                            pd.NSFW = obj["nsfw"]?.GetValue<bool>() ?? pd.NSFW;
                            pd.TRIGGERING = obj["triggering"]?.GetValue<bool>() ?? pd.TRIGGERING;
                            pd.SpoilerARR = obj["spoilerARR"]?.GetValue<bool>() ?? pd.SpoilerARR;
                            pd.SpoilerHW = obj["spoilerHW"]?.GetValue<bool>() ?? pd.SpoilerHW;
                            pd.SpoilerSB = obj["spoilerSB"]?.GetValue<bool>() ?? pd.SpoilerSB;
                            pd.SpoilerSHB = obj["spoilerSHB"]?.GetValue<bool>() ?? pd.SpoilerSHB;
                            pd.SpoilerEW = obj["spoilerEW"]?.GetValue<bool>() ?? pd.SpoilerEW;
                            pd.SpoilerDT = obj["spoilerDT"]?.GetValue<bool>() ?? pd.SpoilerDT;
                        }
                        catch { }

                        // Attempt to restore avatar/background bytes (if stored inline for compatibility)
                        try
                        {
                            if (obj["avatarBytes"] != null)
                            {
                                var b64 = obj["avatarBytes"].GetValue<string>();
                                if (!string.IsNullOrEmpty(b64))
                                {
                                    var bytes = Convert.FromBase64String(b64);
                                    pd.avatarBytes = bytes;
                                    if (Plugin.TextureProvider != null && bytes.Length > 0)
                                    {
                                        try
                                        {
                                            var tex = Plugin.TextureProvider.CreateFromImageAsync(bytes).Result;
                                            if (tex != null && tex.Handle != IntPtr.Zero) pd.avatar = tex;
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                        catch { }

                        try
                        {
                            if (obj["backgroundBytes"] != null)
                            {
                                var b64 = obj["backgroundBytes"].GetValue<string>();
                                if (!string.IsNullOrEmpty(b64))
                                {
                                    var bytes = Convert.FromBase64String(b64);
                                    pd.backgroundBytes = bytes;
                                    if (Plugin.TextureProvider != null && bytes.Length > 0)
                                    {
                                        try
                                        {
                                            var tex = Plugin.TextureProvider.CreateFromImageAsync(bytes).Result;
                                            if (tex != null && tex.Handle != IntPtr.Zero) pd.background = tex;
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                        catch { }

                        // customTabs handling: prefer profileFolder + per-tab layout files
                        pd.customTabs = new List<CustomTab>();
                        try
                        {
                            var tabs = obj["customTabs"] as JsonArray;
                            var profileFolderName = obj["profileFolder"]?.GetValue<string>();
                            var profileFolderPath = !string.IsNullOrEmpty(profileFolderName)
                                ? Path.Combine(ResolveBasePath(), ProfilesFolderName, profileFolderName)
                                : Path.Combine(ResolveBasePath(), ProfilesFolderName, Path.GetFileNameWithoutExtension(file));

                            if (tabs != null)
                            {
                                for (int t = 0; t < tabs.Count; t++)
                                {
                                    if (tabs[t] is not JsonObject tobj) continue;
                                    var tab = new CustomTab { Name = tobj["name"]?.GetValue<string>() ?? string.Empty };
                                    tab.ID = tobj["id"]?.GetValue<int>() ?? t;

                                    var layoutTypeName = tobj["layoutType"]?.GetValue<string>();
                                    var layoutFileName = tobj["layoutFile"]?.GetValue<string>();

                                    // Try to load layout from file if present
                                    if (!string.IsNullOrWhiteSpace(layoutFileName))
                                    {
                                        var layoutPath = Path.Combine(profileFolderPath, layoutFileName);
                                        if (File.Exists(layoutPath))
                                        {
                                            try
                                            {
                                                var layoutText = File.ReadAllText(layoutPath);
                                                // If layoutType indicates GalleryLayout, special-load images referenced by file names
                                                if (!string.IsNullOrWhiteSpace(layoutTypeName) &&
                                                    layoutTypeName.Contains("GalleryLayout"))
                                                {
                                                    var gnode = JsonNode.Parse(layoutText) as JsonObject;
                                                    if (gnode != null)
                                                    {
                                                        var gallery = new GalleryLayout
                                                        {
                                                            tabName = gnode["tabName"]?.GetValue<string>() ?? string.Empty,
                                                            tabIndex = gnode["tabIndex"]?.GetValue<int>() ?? 0,
                                                            images = new List<ProfileGalleryImage>()
                                                        };

                                                        if (gnode["images"] is JsonArray imgArr)
                                                        {
                                                            for (int imgIdx = 0; imgIdx < imgArr.Count; imgIdx++)
                                                            {
                                                                if (imgArr[imgIdx] is not JsonObject imgObj) continue;
                                                                var gi = new ProfileGalleryImage
                                                                {
                                                                    index = imgObj["index"]?.GetValue<int>() ?? -1,
                                                                    url = imgObj["url"]?.GetValue<string>() ?? string.Empty,
                                                                    tooltip = imgObj["tooltip"]?.GetValue<string>() ?? string.Empty,
                                                                    nsfw = imgObj["nsfw"]?.GetValue<bool>() ?? false,
                                                                    trigger = imgObj["trigger"]?.GetValue<bool>() ?? false,
                                                                    image = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab),
                                                                    thumbnail = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab),
                                                                    imageBytes = Array.Empty<byte>()
                                                                };

                                                                try
                                                                {
                                                                    var imgFile = imgObj["imageFile"]?.GetValue<string>();
                                                                    if (!string.IsNullOrEmpty(imgFile))
                                                                    {
                                                                        var imgPath = Path.Combine(profileFolderPath, imgFile);
                                                                        if (File.Exists(imgPath))
                                                                        {
                                                                            var bytes = File.ReadAllBytes(imgPath);
                                                                            gi.imageBytes = bytes;

                                                                            if (Plugin.TextureProvider != null && bytes.Length > 0)
                                                                            {
                                                                                try
                                                                                {
                                                                                    var tex = Plugin.TextureProvider.CreateFromImageAsync(bytes).Result;
                                                                                    if (tex != null && tex.Handle != IntPtr.Zero) gi.image = tex;
                                                                                }
                                                                                catch { }
                                                                            }

                                                                            // generate thumbnail from image bytes
                                                                            try
                                                                            {
                                                                                var thumbBytes = Imaging.ScaleImageBytes(bytes, 200, 200);
                                                                                if (thumbBytes != null && thumbBytes.Length > 0 && Plugin.TextureProvider != null)
                                                                                {
                                                                                    var ttex = Plugin.TextureProvider.CreateFromImageAsync(thumbBytes).Result;
                                                                                    if (ttex != null && ttex.Handle != IntPtr.Zero) gi.thumbnail = ttex;
                                                                                }
                                                                            }
                                                                            catch { }
                                                                        }
                                                                    }
                                                                }
                                                                catch { }

                                                                gallery.images.Add(gi);
                                                            }
                                                        }

                                                        tab.Layout = gallery;
                                                    }
                                                }
                                                else
                                                {
                                                    // Generic restore: try to resolve type then deserialize into it
                                                    Type ltype = null;
                                                    if (!string.IsNullOrWhiteSpace(layoutTypeName))
                                                        ltype = ResolveType(layoutTypeName);

                                                    if (ltype != null)
                                                    {
                                                        try
                                                        {
                                                            var des = JsonSerializer.Deserialize(layoutText, ltype, opts);
                                                            if (des is CustomLayout cl) tab.Layout = cl;
                                                            else if (des != null && typeof(CustomLayout).IsAssignableFrom(ltype)) tab.Layout = (CustomLayout)des;
                                                            else tab.Layout = null;
                                                        }
                                                        catch
                                                        {
                                                            // fallback: heuristics
                                                            object layoutObj = null;
                                                            var known = new[]
                                                            {
                                                                "AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.BioLayout",
                                                                "AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.DetailsLayout",
                                                                "AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.GalleryLayout",
                                                                "AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.InfoLayout",
                                                                "AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.StoryLayout",
                                                                "AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.TreeLayout",
                                                                "AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.InventoryLayout"
                                                            };
                                                            foreach (var tn2 in known)
                                                            {
                                                                var tt = ResolveType(tn2);
                                                                if (tt == null) continue;
                                                                try
                                                                {
                                                                    var des2 = JsonSerializer.Deserialize(layoutText, tt, opts);
                                                                    if (des2 != null && typeof(CustomLayout).IsAssignableFrom(tt))
                                                                    {
                                                                        layoutObj = (CustomLayout)des2;
                                                                        break;
                                                                    }
                                                                }
                                                                catch { layoutObj = null; }
                                                            }
                                                            tab.Layout = layoutObj as CustomLayout;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // Unknown layoutType -> heuristic
                                                        object layoutObj = null;
                                                        var known = new[]
                                                        {
                                                            "AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.BioLayout",
                                                            "AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.DetailsLayout",
                                                            "AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.GalleryLayout",
                                                            "AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.InfoLayout",
                                                            "AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.StoryLayout",
                                                            "AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.TreeLayout",
                                                            "AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.InventoryLayout"
                                                        };
                                                        foreach (var tn2 in known)
                                                        {
                                                            var tt = ResolveType(tn2);
                                                            if (tt == null) continue;
                                                            try
                                                            {
                                                                var des = JsonSerializer.Deserialize(layoutText, tt, opts);
                                                                if (des != null && typeof(CustomLayout).IsAssignableFrom(tt))
                                                                {
                                                                    layoutObj = (CustomLayout)des;
                                                                    break;
                                                                }
                                                            }
                                                            catch { layoutObj = null; }
                                                        }
                                                        tab.Layout = layoutObj as CustomLayout;
                                                    }
                                                }
                                            }
                                            catch { tab.Layout = null; }
                                        }
                                        else
                                        {
                                            // If layoutFile isn't present - attempt inline layoutData (backward compatibility)
                                            var layoutData = tobj["layoutData"];
                                            if (layoutData != null)
                                            {
                                                object layoutObj = null;
                                                var known = new[]
                                                {
                                                    "AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.BioLayout",
                                                    "AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.DetailsLayout",
                                                    "AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.GalleryLayout",
                                                    "AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.InfoLayout",
                                                    "AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.StoryLayout",
                                                    "AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.TreeLayout",
                                                    "AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes.InventoryLayout"
                                                };
                                                foreach (var tn2 in known)
                                                {
                                                    var tt = ResolveType(tn2);
                                                    if (tt == null) continue;
                                                    try
                                                    {
                                                        var des = JsonSerializer.Deserialize(layoutData.ToJsonString(), tt, opts);
                                                        if (des != null && typeof(CustomLayout).IsAssignableFrom(tt))
                                                        {
                                                            layoutObj = (CustomLayout)des;
                                                            break;
                                                        }
                                                    }
                                                    catch { layoutObj = null; }
                                                }
                                                tab.Layout = layoutObj as CustomLayout;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // no layout info
                                        tab.Layout = null;
                                    }

                                    pd.customTabs.Add(tab);
                                }
                            }
                        }
                        catch { pd.customTabs = pd.customTabs ?? new List<CustomTab>(); }

                        results.Add(pd);
                    }
                    catch (Exception exFile)
                    {
                        try { Plugin.PluginLog?.Error($"Failed loading tooltipData file '{file}': {exFile}"); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                try { Plugin.PluginLog?.Error($"LoadCachedProfiles failed: {ex}"); } catch { }
            }

            return results;
        }     // Compares scalar fields, colors, avatar/background bytes, and customTabs (including gallery image bytes).
        public static bool AreProfilesEquivalent(ProfileData a, ProfileData b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;

            // Scalars
            if (!string.Equals(a.title ?? string.Empty, b.title ?? string.Empty, StringComparison.Ordinal)) return false;
            if (!string.Equals(a.title ?? string.Empty, b.title ?? string.Empty, StringComparison.Ordinal)) return false;
            if (a.isPrivate != b.isPrivate) return false;
            if (a.isActive != b.isActive) return false;
            if (a.SHOW_ON_COMPASS != b.SHOW_ON_COMPASS) return false;
            if (a.NSFW != b.NSFW) return false;
            if (a.TRIGGERING != b.TRIGGERING) return false;
            if (a.SpoilerARR != b.SpoilerARR) return false;
            if (a.SpoilerHW != b.SpoilerHW) return false;
            if (a.SpoilerSB != b.SpoilerSB) return false;
            if (a.SpoilerSHB != b.SpoilerSHB) return false;
            if (a.SpoilerEW != b.SpoilerEW) return false;
            if (a.SpoilerDT != b.SpoilerDT) return false;

            // titleColor (exact compare)
            if (!a.titleColor.Equals(b.titleColor)) return false;

            // avatar/background bytes (prefer explicit bytes if available)
            if (!ByteArrayEquals(a.avatarBytes, b.avatarBytes)) return false;

            if (!ByteArrayEquals(a.backgroundBytes, b.backgroundBytes)) return false;

            // customTabs deep compare
            if (!CompareCustomTabs(a.customTabs, b.customTabs)) return false;

            // If you want to compare more fields (Name/Race/etc.), extend here.
            return true;
        }

        private static bool ByteArrayEquals(byte[] x, byte[] y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            if (x.Length != y.Length) return false;
            // Use a fast equality check
            return x.SequenceEqual(y);
        }

        private static bool CompareCustomTabs(List<CustomTab> aTabs, List<CustomTab> bTabs)
        {
            if (ReferenceEquals(aTabs, bTabs)) return true;
            if (aTabs == null && bTabs == null) return true;
            if (aTabs == null || bTabs == null) return false;
            if (aTabs.Count != bTabs.Count) return false;

            var opts = new JsonSerializerOptions { WriteIndented = false, PropertyNameCaseInsensitive = true };

            for (int i = 0; i < aTabs.Count; i++)
            {
                var at = aTabs[i];
                var bt = bTabs[i];

                if (at == null && bt == null) continue;
                if (at == null || bt == null) return false;

                if (!string.Equals(at.Name ?? string.Empty, bt.Name ?? string.Empty, StringComparison.Ordinal)) return false;
                if (at.ID != bt.ID) return false;

                // Compare layout types
                var atType = at.Layout?.GetType();
                var btType = bt.Layout?.GetType();
                if (atType == null && btType == null) continue;
                if (atType == null || btType == null) return false;
                if (atType.FullName != btType.FullName) return false;

                // For GalleryLayouts, compare image entries and image bytes
                if (at.Layout is GalleryLayout aGallery && bt.Layout is GalleryLayout bGallery)
                {
                    if (!string.Equals(aGallery.tabName ?? string.Empty, bGallery.tabName ?? string.Empty, StringComparison.Ordinal)) return false;
                    if (aGallery.tabIndex != bGallery.tabIndex) return false;

                    var aImgs = aGallery.images ?? new List<ProfileGalleryImage>();
                    var bImgs = bGallery.images ?? new List<ProfileGalleryImage>();
                    if (aImgs.Count != bImgs.Count) return false;

                    for (int j = 0; j < aImgs.Count; j++)
                    {
                        var ai = aImgs[j];
                        var bi = bImgs[j];
                        if (!string.Equals(ai.url ?? string.Empty, bi.url ?? string.Empty, StringComparison.Ordinal)) return false;
                        if (!string.Equals(ai.tooltip ?? string.Empty, bi.tooltip ?? string.Empty, StringComparison.Ordinal)) return false;
                        if (ai.nsfw != bi.nsfw) return false;
                        if (ai.trigger != bi.trigger) return false;

                        if (!ByteArrayEquals(ai.imageBytes, bi.imageBytes)) return false;

                        // If imageBytes not present on either side, try comparing texture handles where available
                        if ((ai.imageBytes == null || ai.imageBytes.Length == 0) && (bi.imageBytes == null || bi.imageBytes.Length == 0))
                        {
                            var ah = (object?)ai.image?.Handle;
                            var bh = (object?)bi.image?.Handle;
                            if (!object.Equals(ah, bh)) return false;
                        }
                    }

                    continue;
                }

                // For other layouts, attempt to compare by JSON serialization (best-effort)
                try
                {
                    var aJson = JsonSerializer.Serialize(at.Layout, at.Layout.GetType(), opts);
                    var bJson = JsonSerializer.Serialize(bt.Layout, bt.Layout.GetType(), opts);
                    if (!string.Equals(aJson, bJson, StringComparison.Ordinal)) return false;
                }
                catch
                {
                    // If serialization fails, fall back to type comparison only (already compared)
                    continue;
                }
            }

            return true;
        }

        // Resolve type helper
        private static Type ResolveType(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return null;
            var t = Type.GetType(fullName);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    t = asm.GetType(fullName, false, false);
                    if (t != null) return t;
                }
                catch { }
            }
            return null;
        }
    }
}