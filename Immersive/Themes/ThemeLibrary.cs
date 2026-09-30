using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AbsoluteRP.Immersive.Themes;

// On-disk store for theme documents: <config>/immersive_themes/drafts/<localId>.json the user's own work <config>/immersive_themes/installed/<serverId>.json gallery themes they installed <config>/immersive_themes/cache/<serverId>.json themes fetched to view someone's profile Everything is loaded once and kept in memory; writes go straight to disk so a crash never loses a draft.
public static class ThemeLibrary
{
    private static bool _loaded;
    private static readonly List<ThemeDocument> _drafts = new();
    private static readonly List<ThemeDocument> _installed = new();
    private static readonly Dictionary<int, ThemeDocument> _cache = new();
    private static readonly HashSet<int> _requested = new();

    public static IReadOnlyList<ThemeDocument> Drafts    { get { EnsureLoaded(); return _drafts; } }
    public static IReadOnlyList<ThemeDocument> Installed { get { EnsureLoaded(); return _installed; } }

    private static string Root
    {
        get
        {
            var dir = Plugin.PluginInterface?.ConfigDirectory?.FullName ?? Path.GetTempPath();
            return Path.Combine(dir, "immersive_themes");
        }
    }
    private static string DraftsDir    => Path.Combine(Root, "drafts");
    private static string InstalledDir => Path.Combine(Root, "installed");
    private static string CacheDir     => Path.Combine(Root, "cache");

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            Directory.CreateDirectory(DraftsDir);
            Directory.CreateDirectory(InstalledDir);
            Directory.CreateDirectory(CacheDir);
            foreach (var f in Directory.GetFiles(DraftsDir, "*.json"))
            {
                var d = ThemeDocument.FromJson(File.ReadAllText(f));
                if (d != null) _drafts.Add(d);
            }
            foreach (var f in Directory.GetFiles(InstalledDir, "*.json"))
            {
                var d = ThemeDocument.FromJson(File.ReadAllText(f));
                if (d != null && d.ServerId > 0) _installed.Add(d);
            }
            foreach (var f in Directory.GetFiles(CacheDir, "*.json"))
            {
                var d = ThemeDocument.FromJson(File.ReadAllText(f));
                if (d != null && d.ServerId > 0) _cache[d.ServerId] = d;
            }
            _drafts.Sort((a, b) => b.UpdatedUtc.CompareTo(a.UpdatedUtc));
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.Warning("[ThemeLibrary] load: " + ex.Message);
        }
    }

    public static ThemeDocument? GetLocal(string localId)
    {
        EnsureLoaded();
        return _drafts.FirstOrDefault(d => d.LocalId == localId)
            ?? _installed.FirstOrDefault(d => d.LocalId == localId);
    }

    public static ThemeDocument? GetServer(int serverId)
    {
        EnsureLoaded();
        return _installed.FirstOrDefault(d => d.ServerId == serverId)
            ?? (_cache.TryGetValue(serverId, out var c) ? c : null);
    }

    public static bool IsInstalled(int serverId)
    {
        EnsureLoaded();
        return _installed.Any(d => d.ServerId == serverId);
    }

    // drafts
    public static void SaveDraft(ThemeDocument doc)
    {
        EnsureLoaded();
        doc.UpdatedUtc = DateTime.UtcNow;
        var idx = _drafts.FindIndex(d => d.LocalId == doc.LocalId);
        if (idx >= 0) _drafts[idx] = doc; else _drafts.Insert(0, doc);
        ImmersiveThemes.InvalidateDocument(doc);
        try
        {
            Directory.CreateDirectory(DraftsDir);
            File.WriteAllText(Path.Combine(DraftsDir, doc.LocalId + ".json"), doc.ToJson());
        }
        catch (Exception ex) { Plugin.PluginLog?.Warning("[ThemeLibrary] save draft: " + ex.Message); }
    }

    public static void DeleteDraft(ThemeDocument doc)
    {
        EnsureLoaded();
        _drafts.RemoveAll(d => d.LocalId == doc.LocalId);
        ImmersiveThemes.InvalidateDocument(doc);
        try { var p = Path.Combine(DraftsDir, doc.LocalId + ".json"); if (File.Exists(p)) File.Delete(p); } catch { }
    }

    // installed
    public static void SaveInstalled(ThemeDocument doc)
    {
        EnsureLoaded();
        if (doc.ServerId <= 0) return;
        _installed.RemoveAll(d => d.ServerId == doc.ServerId);
        _installed.Insert(0, doc);
        ImmersiveThemes.InvalidateDocument(doc);
        try
        {
            Directory.CreateDirectory(InstalledDir);
            File.WriteAllText(Path.Combine(InstalledDir, doc.ServerId + ".json"), doc.ToJson());
        }
        catch (Exception ex) { Plugin.PluginLog?.Warning("[ThemeLibrary] save installed: " + ex.Message); }
    }

    public static void Uninstall(int serverId)
    {
        EnsureLoaded();
        var doc = _installed.FirstOrDefault(d => d.ServerId == serverId);
        _installed.RemoveAll(d => d.ServerId == serverId);
        if (doc != null) ImmersiveThemes.InvalidateDocument(doc);
        try { var p = Path.Combine(InstalledDir, serverId + ".json"); if (File.Exists(p)) File.Delete(p); } catch { }
    }

    // cache (themes of profiles we viewed)
    public static void CacheServerTheme(ThemeDocument doc)
    {
        EnsureLoaded();
        if (doc.ServerId <= 0) return;
        _cache[doc.ServerId] = doc;
        _requested.Remove(doc.ServerId);
        ImmersiveThemes.InvalidateDocument(doc);
        try
        {
            Directory.CreateDirectory(CacheDir);
            File.WriteAllText(Path.Combine(CacheDir, doc.ServerId + ".json"), doc.ToJson());
        }
        catch { }
    }

    // Ask the server for a theme we don't have yet (once per session per id).
    public static void RequestServerTheme(int serverId)
    {
        if (serverId <= 0 || _requested.Contains(serverId)) return;
        _requested.Add(serverId);
        try { ThemeNetwork.RequestTheme(serverId); }
        catch (Exception ex) { Plugin.PluginLog?.Debug("[ThemeLibrary] request: " + ex.Message); }
    }

    public static void ForgetRequest(int serverId) => _requested.Remove(serverId);
}
