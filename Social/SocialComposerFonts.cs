// Font manager for the SocialComposer WYSIWYG rich-text editor. Loads a handful of Windows system font families (each with Regular/Bold/. Italic/Bold-Italic .ttfs) through Dalamud's font atlas so composer runs can push a real italic/bold face - something the default game font (Axis) cannot provide. Handles are built lazily and disposed with the plugin. Push() is fail-soft: missing files or an uninitialized atlas produce a no-op scope rather than an exception, so callers can always wrap draws.

using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Interface.ManagedFontAtlas;

namespace AbsoluteRP.Social;

public enum FontStyle { Regular, Bold, Italic, BoldItalic }

public sealed class FontFamilyEntry
{
    public string DisplayName = string.Empty;
    public string Id = string.Empty;
}

public static class SocialComposerFonts
{
    private sealed class FamilyDef
    {
        public string Id = string.Empty;
        public string DisplayName = string.Empty;
        public string? Regular;
        public string? Bold;
        public string? Italic;
        public string? BoldItalic;
    }

    // Static family catalog. Files under C:\Windows\Fonts\ - the plugin is. Windows-only so this is a safe fixed path.
    private static readonly FamilyDef[] Catalog =
    {
        new() { Id = "segoe",   DisplayName = "Segoe UI",        Regular = "segoeui.ttf", Bold = "segoeuib.ttf", Italic = "segoeuii.ttf", BoldItalic = "segoeuiz.ttf" },
        new() { Id = "georgia", DisplayName = "Georgia",         Regular = "georgia.ttf", Bold = "georgiab.ttf", Italic = "georgiai.ttf", BoldItalic = "georgiaz.ttf" },
        new() { Id = "arial",   DisplayName = "Arial",           Regular = "arial.ttf",   Bold = "arialbd.ttf",  Italic = "ariali.ttf",   BoldItalic = "arialbi.ttf"  },
        new() { Id = "times",   DisplayName = "Times New Roman", Regular = "times.ttf",   Bold = "timesbd.ttf",  Italic = "timesi.ttf",   BoldItalic = "timesbi.ttf"  },
        new() { Id = "courier", DisplayName = "Courier New",     Regular = "cour.ttf",    Bold = "courbd.ttf",   Italic = "couri.ttf",    BoldItalic = "courbi.ttf"   },
    };

    private static readonly object InitLock = new();
    private static bool initialized;
    private static bool disposed;

    private static readonly List<FontFamilyEntry> families = new();
    private static readonly Dictionary<(string family, FontStyle style), IFontHandle> handles = new();
    private static string? defaultFamilyId;

    public static IReadOnlyList<FontFamilyEntry> Families => families;

    public static void Initialize()
    {
        if (initialized) return;
        lock (InitLock)
        {
            if (initialized) return;

            IFontAtlas? atlas;
            try { atlas = Plugin.PluginInterface?.UiBuilder?.FontAtlas; }
            catch (Exception ex)
            {
                Plugin.PluginLog?.Debug($"[SocialComposerFonts] atlas access failed: {ex.Message}");
                atlas = null;
            }

            if (atlas == null)
            {
                // No atlas yet - leave uninitialized so a later Initialize can retry.
                return;
            }

            const string fontsRoot = @"C:\Windows\Fonts\";
            var basePx = 16f;
            try
            {
                var spec = Plugin.PluginInterface?.UiBuilder?.DefaultFontSpec?.SizePx;
                if (spec.HasValue && spec.Value > 0f) basePx = spec.Value;
            }
            catch { /* fall through with 16px default */ }

            foreach (var fam in Catalog)
            {
                var regPath = Combine(fontsRoot, fam.Regular);
                if (regPath == null || !File.Exists(regPath))
                {
                    Plugin.PluginLog?.Debug($"[SocialComposerFonts] family '{fam.Id}' skipped, no regular face at '{regPath}'");
                    continue;
                }

                var boldPath   = ExistsOrNull(Combine(fontsRoot, fam.Bold));
                var italicPath = ExistsOrNull(Combine(fontsRoot, fam.Italic));
                var boldItPath = ExistsOrNull(Combine(fontsRoot, fam.BoldItalic));

                if (!TryBuild(atlas, basePx, regPath, out var regHandle) || regHandle == null)
                {
                    Plugin.PluginLog?.Debug($"[SocialComposerFonts] family '{fam.Id}' regular build failed");
                    continue;
                }

                families.Add(new FontFamilyEntry { Id = fam.Id, DisplayName = fam.DisplayName });
                defaultFamilyId ??= fam.Id;

                handles[(fam.Id, FontStyle.Regular)] = regHandle;

                // Fall back to Regular for any missing style so a Push always resolves.
                handles[(fam.Id, FontStyle.Bold)]       = BuildOrFallback(atlas, basePx, boldPath,   regHandle, fam.Id, "Bold");
                handles[(fam.Id, FontStyle.Italic)]     = BuildOrFallback(atlas, basePx, italicPath, regHandle, fam.Id, "Italic");
                handles[(fam.Id, FontStyle.BoldItalic)] = BuildOrFallback(atlas, basePx, boldItPath, regHandle, fam.Id, "BoldItalic");
            }

            initialized = true;
        }
    }

    private static IFontHandle BuildOrFallback(IFontAtlas atlas, float px, string? path, IFontHandle regular, string famId, string styleName)
    {
        if (path != null && TryBuild(atlas, px, path, out var h) && h != null) return h;
        Plugin.PluginLog?.Debug($"[SocialComposerFonts] family '{famId}' missing {styleName}, falling back to Regular");
        return regular;
    }

    private static bool TryBuild(IFontAtlas atlas, float px, string path, out IFontHandle? handle)
    {
        try
        {
            handle = atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
                tk.AddFontFromFile(path, new SafeFontConfig { SizePx = px })));
            return true;
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.Debug($"[SocialComposerFonts] build failed for '{path}': {ex.Message}");
            handle = null;
            return false;
        }
    }

    private static string? Combine(string root, string? file)
        => string.IsNullOrEmpty(file) ? null : Path.Combine(root, file);

    private static string? ExistsOrNull(string? path)
        => (path != null && File.Exists(path)) ? path : null;

    public static PushScope Push(string familyId, FontStyle style)
    {
        if (disposed) return default;
        if (!initialized)
        {
            try { Initialize(); }
            catch (Exception ex)
            {
                Plugin.PluginLog?.Debug($"[SocialComposerFonts] on-demand init failed: {ex.Message}");
                return default;
            }
        }

        var h = Resolve(familyId, style);
        if (h == null) return default;

        try
        {
            return new PushScope(h.Push());
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.Debug($"[SocialComposerFonts] push failed for '{familyId}'/{style}: {ex.Message}");
            return default;
        }
    }

    private static IFontHandle? Resolve(string familyId, FontStyle style)
    {
        if (!string.IsNullOrEmpty(familyId))
        {
            if (handles.TryGetValue((familyId, style), out var exact)) return exact;
            if (handles.TryGetValue((familyId, FontStyle.Regular), out var reg)) return reg;
        }
        if (defaultFamilyId != null && handles.TryGetValue((defaultFamilyId, FontStyle.Regular), out var def)) return def;
        return null;
    }

    public static void Dispose()
    {
        lock (InitLock)
        {
            if (disposed) return;
            disposed = true;

            // Handles can alias each other (missing styles fall back to Regular);
            // dedupe references before Dispose so we don't double-dispose one handle.
            var seen = new HashSet<IFontHandle>();
            foreach (var kv in handles)
            {
                if (kv.Value == null) continue;
                if (!seen.Add(kv.Value)) continue;
                try { kv.Value.Dispose(); }
                catch (Exception ex) { Plugin.PluginLog?.Debug($"[SocialComposerFonts] dispose failed: {ex.Message}"); }
            }
            handles.Clear();
            families.Clear();
            defaultFamilyId = null;
            initialized = false;
        }
    }

    public readonly struct PushScope : IDisposable
    {
        private readonly IDisposable? scope;
        internal PushScope(IDisposable? scope) { this.scope = scope; }
        public bool Pushed => scope != null;
        public void Dispose()
        {
            if (scope == null) return;
            try { scope.Dispose(); }
            catch (Exception ex) { Plugin.PluginLog?.Debug($"[SocialComposerFonts] scope dispose failed: {ex.Message}"); }
        }
    }
}
