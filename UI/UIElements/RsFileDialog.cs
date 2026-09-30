using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;

namespace AbsoluteRP.RsUI;

// Themed file-picker with thumbnail previews for images. Call `RsFileDialog.OpenImagePicker(...)` to queue a dialog; call `RsFileDialog.Draw()` once per frame from a UI root (the hub Draw loop is a good place). Only one dialog is visible at a time - a second Open call replaces the first.
public static class RsFileDialog
{
    // Public API

    // Convenience for "pick a single image file". Callback fires with (success, path). On cancel: success == false, path == string.Empty.
    public static void OpenImagePicker(string title, Action<bool, string> onPicked,
                                       string? startPath = null,
                                       IReadOnlyList<string>? extensions = null)
    {
        Begin(Mode.OpenFile, title, extensions ?? DefaultImageExts, onPicked, startPath, string.Empty, string.Empty);
    }

    // Pick one existing file of the given extensions (empty = any file).
    public static void OpenFile(string title, IReadOnlyList<string>? extensions, Action<bool, string> onPicked, string? startPath = null)
        => Begin(Mode.OpenFile, title, extensions, onPicked, startPath, string.Empty, string.Empty);

    // Choose where to save: folder navigation plus a file name box. `defaultExtension` (".json") is appended when the name has none.
    public static void SaveFile(string title, string defaultFileName, string defaultExtension, Action<bool, string> onPicked, string? startPath = null)
        => Begin(Mode.SaveFile, title, string.IsNullOrEmpty(defaultExtension) ? null : new[] { defaultExtension }, onPicked, startPath, defaultFileName ?? string.Empty, defaultExtension ?? string.Empty);

    // Pick a folder: browse into it and confirm.
    public static void PickFolder(string title, Action<bool, string> onPicked, string? startPath = null)
        => Begin(Mode.PickFolder, title, null, onPicked, startPath, string.Empty, string.Empty);

    private enum Mode { OpenFile, SaveFile, PickFolder }
    private static Mode _mode = Mode.OpenFile;
    private static string _fileName = string.Empty;
    private static string _defaultExt = string.Empty;
    private static string _lastFolder = string.Empty;   // remembered between non-image dialogs
    private static int _drawnFrame = -1;

    private static void Begin(Mode mode, string title, IReadOnlyList<string>? extensions, Action<bool, string> onPicked, string? startPath, string fileName, string defaultExt)
    {
        // A dialog that is replaced tells its caller it was cancelled.
        if (_open) Close(false, null);
        _mode = mode;
        _extensions = new HashSet<string>((extensions ?? Array.Empty<string>())
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant()));
        _title = title; _callback = onPicked; _selected = null; _hoverPath = null; _search = string.Empty;
        _fileName = fileName; _defaultExt = defaultExt;

        var imagesOnly = _extensions.Count > 0 && _extensions.All(IsImageExt);
        var start = !string.IsNullOrEmpty(startPath) && Directory.Exists(startPath) ? startPath!
                  : !imagesOnly && Directory.Exists(_lastFolder) ? _lastFolder
                  : Environment.GetFolderPath(imagesOnly ? Environment.SpecialFolder.MyPictures : Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrEmpty(start) || !Directory.Exists(start))
            start = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _navBack.Clear();
        Navigate(start);
        _open = true;
    }

    private static bool IsImageExt(string ext) => Array.IndexOf(DefaultImageExts, ext) >= 0;

    // Must be called every frame from a UI root - safe to call when idle.
    public static void Draw()
    {
        if (!_open && !RsImageCrop.IsOpen) return;
        var frame = ImGui.GetFrameCount();
        if (frame == _drawnFrame) return;
        _drawnFrame = frame;
        // The crop step follows an image pick; it owns the screen while it is up.
        if (RsImageCrop.IsOpen) { RsImageCrop.Draw(); return; }

        var openState = _open;
        ImGui.OpenPopup("##rs_file_dialog");
        var view = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(view.WorkPos + view.WorkSize * 0.5f, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(RsTheme.S(720f), RsTheme.S(500f)), ImGuiCond.Appearing);

        if (!ImGui.BeginPopupModal("##rs_file_dialog", ref openState,
                ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse))
        {
            if (!openState) Close(false, null);
            return;
        }

        try
        {
            DrawHeader();
            ImGui.Separator();
            DrawBreadcrumbs();
            ImGui.Separator();
            ImGui.Spacing();
            DrawGrid();
            ImGui.Spacing();
            DrawFooter();
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Debug("RsFileDialog draw: " + ex.Message);
        }
        finally
        {
            ImGui.EndPopup();
        }

        // The X button on the modal - ImGui writes false into openState.
        if (!openState) Close(false, null);
    }

    // Internals: state

    private static bool _open;
    private static string _title = "Select file";
    private static Action<bool, string>? _callback;
    private static string _currentPath = string.Empty;
    private static string? _selected;
    private static string? _hoverPath;
    private static string _search = string.Empty;

    // Extension whitelist for the current dialog. Lower-case, dot-prefixed.
    private static HashSet<string> _extensions = new();

    private static readonly string[] DefaultImageExts = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };

    // One entry per file/folder in the current directory.
    private sealed class Entry
    {
        public string Path = string.Empty;
        public string Name = string.Empty;
        public bool   IsDirectory;
        public long   Size;
        public DateTime Modified;
    }
    private static List<Entry> _entries = new();

    // Path -> in-flight or ready thumbnail. `null` value = load pending. Bounded LRU-ish cap so we don't leak textures.
    private static readonly Dictionary<string, IDalamudTextureWrap?> _thumbs = new();
    private const int MaxCachedThumbs = 256;

    // Set of paths for which we've kicked off an async load already.
    private static readonly HashSet<string> _loadingThumbs = new();

    // Serializes background GDI+ scaling - parallel calls to System.Drawing.Bitmap / Graphics.DrawImage on different threads are the reason thumbnails silently fail after a folder change.
    private static readonly System.Threading.SemaphoreSlim _loadGate = new(1, 1);

    // Bumps every folder change so async loads that were queued for the old folder can bail out without stomping the cache.
    private static int _folderEpoch;

    // Recent-folders history for the up button.
    private static readonly Stack<string> _navBack = new();

    // Header / breadcrumbs / footer / grid

    private static void DrawHeader()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
        ImGui.TextUnformatted(_title);
        ImGui.PopStyleColor();

        ImGui.SameLine(0f, RsTheme.S(16f));
        var searchW = RsTheme.S(240f);
        var availX = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, availX - searchW));
        RsElements.InputText("rs_fd_search", ref _search, 128, placeholder: "Filter by name", width: 240f);
    }

    private static void DrawBreadcrumbs()
    {
        // <- Back
        var canBack = _navBack.Count > 0;
        if (!canBack) ImGui.BeginDisabled();
        if (RsElements.IconButton(FontAwesomeIcon.ArrowLeft, "rs_fd_back",
                                  RsElements.ButtonVariant.Ghost, size: 26f))
        {
            var prev = _navBack.Pop();
            LoadDirectory(prev);
        }
        if (!canBack) ImGui.EndDisabled();
        ImGui.SameLine(0f, RsTheme.S(4f));

        // up Up one
        var parent = TryGetParent(_currentPath);
        var canUp = parent != null;
        if (!canUp) ImGui.BeginDisabled();
        if (RsElements.IconButton(FontAwesomeIcon.ArrowUp, "rs_fd_up",
                                  RsElements.ButtonVariant.Ghost, size: 26f))
        {
            Navigate(parent!);
        }
        if (!canUp) ImGui.EndDisabled();
        ImGui.SameLine(0f, RsTheme.S(4f));

        if (RsElements.IconButton(FontAwesomeIcon.Home, "rs_fd_home",
                                  RsElements.ButtonVariant.Ghost, size: 26f))
        {
            Navigate(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }
        ImGui.SameLine(0f, RsTheme.S(4f));

        if (RsElements.IconButton(FontAwesomeIcon.Image, "rs_fd_pics",
                                  RsElements.ButtonVariant.Ghost, size: 26f))
        {
            var pics = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (Directory.Exists(pics)) Navigate(pics);
        }

        ImGui.SameLine(0f, RsTheme.S(4f));
        if (RsElements.IconButton(FontAwesomeIcon.Hdd, "rs_fd_drives", RsElements.ButtonVariant.Ghost, size: 26f))
            Navigate(DrivesPath);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Drives");

        ImGui.SameLine(0f, RsTheme.S(10f));
        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
        ImGui.TextUnformatted(_currentPath == DrivesPath ? "This PC" : _currentPath);
        ImGui.PopStyleColor();
    }

    private static void DrawGrid()
    {
        var avail = ImGui.GetContentRegionAvail();
        var reserveFooter = RsTheme.S(48f);
        var gridH = Math.Max(RsTheme.S(120f), avail.Y - reserveFooter);

        if (!ImGui.BeginChild("rs_fd_grid", new Vector2(0f, gridH), false))
        {
            ImGui.EndChild();
            return;
        }
        try
        {
            var thumb = RsTheme.S(96f);
            var pad   = RsTheme.S(10f);
            var cellW = thumb + pad * 2f;
            var cellH = thumb + pad * 2f + RsTheme.S(28f); // room for filename

            var maxX = ImGui.GetContentRegionAvail().X;
            var perRow = Math.Max(1, (int)Math.Floor(maxX / cellW));

            var draw = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();

            var visible = string.IsNullOrEmpty(_search)
                ? _entries
                : _entries.Where(e => e.Name.Contains(_search, StringComparison.OrdinalIgnoreCase)).ToList();

            for (var i = 0; i < visible.Count; i++)
            {
                var e = visible[i];
                var col = i % perRow;
                var row = i / perRow;
                var cellMin = new Vector2(origin.X + col * cellW, origin.Y + row * cellH);
                var cellMax = cellMin + new Vector2(cellW - RsTheme.S(4f), cellH - RsTheme.S(4f));

                var isSel   = _selected != null && string.Equals(_selected, e.Path, StringComparison.OrdinalIgnoreCase);
                var isHover = _hoverPath == e.Path;

                var bg = isSel
                    ? Lerp(RsTheme.BgTertiary, RsTheme.AccentPrimary, 0.25f)
                    : (isHover ? RsTheme.BgHover : RsTheme.BgTertiary);
                draw.AddRectFilled(cellMin, cellMax, Pack(bg), RsTheme.S(6f));
                var borderCol = isSel ? RsTheme.AccentPrimary : RsTheme.Border;
                draw.AddRect(cellMin, cellMax, Pack(borderCol), RsTheme.S(6f), ImDrawFlags.None, RsTheme.BorderThickness);

                // Thumbnail / icon area
                var thumbMin = new Vector2(cellMin.X + pad, cellMin.Y + pad);
                var thumbMax = thumbMin + new Vector2(thumb, thumb);
                if (e.IsDirectory)
                {
                    // Folder glyph centered
                    var glyph = FontAwesomeIcon.Folder.ToIconString();
                    Vector2 gs;
                    using (RsIcons.Push())
                    {
                        var scale = RsTheme.S(3.0f);
                        gs = ImGui.CalcTextSize(glyph) * scale;
                        var gp = new Vector2(
                            thumbMin.X + (thumb - gs.X) * 0.5f,
                            thumbMin.Y + (thumb - gs.Y) * 0.5f);
                        // Fallback: draw at 1x scale - CalcTextSize can be off with scale, but glyph draws fine centered.
                        var glyphSz = ImGui.CalcTextSize(glyph);
                        var glyphPos = new Vector2(
                            thumbMin.X + (thumb - glyphSz.X) * 0.5f,
                            thumbMin.Y + (thumb - glyphSz.Y) * 0.5f);
                        draw.AddText(glyphPos, Pack(RsTheme.AccentWarning), glyph);
                    }
                }
                else
                {
                    var tex = IsImageExt(Path.GetExtension(e.Path).ToLowerInvariant()) ? GetOrLoadThumb(e.Path) : null;
                    if (tex != null && tex.Handle != IntPtr.Zero)
                    {
                        // Aspect-fit into thumb box.
                        var texSize = new Vector2(tex.Width, tex.Height);
                        var scale = Math.Min(thumb / texSize.X, thumb / texSize.Y);
                        var sz = texSize * scale;
                        var imgMin = new Vector2(
                            thumbMin.X + (thumb - sz.X) * 0.5f,
                            thumbMin.Y + (thumb - sz.Y) * 0.5f);
                        draw.AddImage(tex.Handle, imgMin, imgMin + sz);
                    }
                    else
                    {
                        // Loading placeholder (or a plain file)
                        var glyph = (IsImageExt(Path.GetExtension(e.Path).ToLowerInvariant()) ? FontAwesomeIcon.Image : FontAwesomeIcon.FileAlt).ToIconString();
                        using (RsIcons.Push())
                        {
                            var gs = ImGui.CalcTextSize(glyph);
                            var gp = new Vector2(
                                thumbMin.X + (thumb - gs.X) * 0.5f,
                                thumbMin.Y + (thumb - gs.Y) * 0.5f);
                            draw.AddText(gp, Pack(RsTheme.TextMuted), glyph);
                        }
                    }
                }

                // Filename below thumbnail (truncated + centered)
                var label = TruncateForCell(e.Name, cellW - pad * 2f);
                var labelSize = ImGui.CalcTextSize(label);
                var labelPos = new Vector2(
                    cellMin.X + (cellW - labelSize.X) * 0.5f,
                    thumbMax.Y + RsTheme.S(4f));
                draw.AddText(labelPos, Pack(RsTheme.TextPrimary), label);

                // Hit-test overlay for click/hover.
                ImGui.SetCursorScreenPos(cellMin);
                if (ImGui.InvisibleButton("##rs_fd_cell_" + i, new Vector2(cellW - RsTheme.S(4f), cellH - RsTheme.S(4f))))
                {
                    if (e.IsDirectory)
                    {
                        Navigate(e.Path);
                    }
                    else
                    {
                        _selected = e.Path;
                        if (_mode == Mode.SaveFile) _fileName = e.Name;
                    }
                }
                if (ImGui.IsItemHovered())
                {
                    _hoverPath = e.Path;
                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && !e.IsDirectory)
                    {
                        if (_mode == Mode.SaveFile) { _fileName = e.Name; } else Close(true, e.Path);
                    }
                    if (!e.IsDirectory)
                        ImGui.SetTooltip($"{e.Name}\n{PrettySize(e.Size)}   {e.Modified:yyyy-MM-dd HH:mm}");
                }
            }

            var totalRows = (visible.Count + perRow - 1) / perRow;
            ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + totalRows * cellH));
        }
        finally
        {
            ImGui.EndChild();
        }
    }

    private static void DrawFooter()
    {
        var btnW = RsTheme.S(120f);
        var buttonsW = btnW * 2f + RsTheme.S(12f);
        string? result = null; bool canConfirm; string confirm;

        if (_mode == Mode.SaveFile)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted("File name");
            ImGui.PopStyleColor();
            ImGui.SameLine();
            var inputW = Math.Max(RsTheme.S(120f), ImGui.GetContentRegionAvail().X - buttonsW - RsTheme.S(16f));
            RsElements.InputText("rs_fd_name", ref _fileName, 200, placeholder: "name" + _defaultExt, width: inputW / Math.Max(0.01f, RsTheme.S(1f)));
            ImGui.SameLine();
            var name = (_fileName ?? string.Empty).Trim();
            var valid = name.Length > 0 && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && _currentPath != DrivesPath && Directory.Exists(_currentPath);
            if (valid)
            {
                if (!string.IsNullOrEmpty(_defaultExt) && !Path.HasExtension(name)) name += _defaultExt;
                result = Path.Combine(_currentPath, name);
            }
            canConfirm = valid;
            confirm = valid && File.Exists(result) ? "Overwrite" : "Save";
        }
        else if (_mode == Mode.PickFolder)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted(_currentPath == DrivesPath ? "Open a drive to pick a folder" : "Folder: " + _currentPath);
            ImGui.PopStyleColor();
            canConfirm = _currentPath != DrivesPath && Directory.Exists(_currentPath);
            result = _currentPath;
            confirm = "Select folder";
            ImGui.SameLine();
        }
        else
        {
            var picked = _selected;
            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
            ImGui.TextUnformatted(string.IsNullOrEmpty(picked) ? "No file selected" : "Selected: " + Path.GetFileName(picked));
            ImGui.PopStyleColor();
            canConfirm = !string.IsNullOrEmpty(picked) && File.Exists(picked);
            result = picked;
            confirm = "Select";
            ImGui.SameLine();
        }

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, ImGui.GetContentRegionAvail().X - buttonsW));
        if (RsElements.Button("Cancel##rs_fd_cancel", RsElements.ButtonVariant.Ghost, new Vector2(btnW, 0f)))
        {
            Close(false, null);
            return;
        }
        ImGui.SameLine();
        if (!canConfirm) ImGui.BeginDisabled();
        if (RsElements.Button(confirm + "##rs_fd_ok", RsElements.ButtonVariant.Primary, new Vector2(btnW, 0f)))
            Close(true, result);
        if (!canConfirm) ImGui.EndDisabled();
    }

    // Navigation

    private static void Navigate(string path)
    {
        if (!string.IsNullOrEmpty(_currentPath) && (_currentPath == DrivesPath || Directory.Exists(_currentPath)))
            _navBack.Push(_currentPath);
        LoadDirectory(path);
    }

    private static void LoadDirectory(string path)
    {
        _currentPath = path;
        _selected = null;
        _hoverPath = null;
        _entries.Clear();

        // Fresh folder = fresh thumbnail cache. Dispose textures from the previous folder so we don't leak, and clear the loading tracker so re-navigation to a folder re-loads its thumbs.
        _folderEpoch++;
        foreach (var kv in _thumbs)
        {
            AbsoluteRP.Helpers.TextureGraveyard.Enqueue(kv.Value);   // may have been drawn this frame
        }
        _thumbs.Clear();
        _loadingThumbs.Clear();
        if (path == DrivesPath)
        {
            try
            {
                foreach (var drv in DriveInfo.GetDrives())
                {
                    if (!drv.IsReady) continue;
                    _entries.Add(new Entry { Path = drv.RootDirectory.FullName, Name = drv.Name, IsDirectory = true });
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug("RsFileDialog drives: " + ex.Message); }
            return;
        }
        if (!(_extensions.Count > 0 && _extensions.All(IsImageExt))) _lastFolder = path;
        try
        {
            var di = new DirectoryInfo(path);
            foreach (var d in di.EnumerateDirectories())
            {
                if ((d.Attributes & FileAttributes.Hidden) != 0) continue;
                _entries.Add(new Entry
                {
                    Path = d.FullName,
                    Name = d.Name,
                    IsDirectory = true,
                    Modified = d.LastWriteTime,
                });
            }
            foreach (var f in di.EnumerateFiles())
            {
                if (_mode == Mode.PickFolder) break;
                if ((f.Attributes & FileAttributes.Hidden) != 0) continue;
                var ext = f.Extension.ToLowerInvariant();
                if (_extensions.Count > 0 && !_extensions.Contains(ext)) continue;
                _entries.Add(new Entry
                {
                    Path = f.FullName,
                    Name = f.Name,
                    IsDirectory = false,
                    Size = f.Length,
                    Modified = f.LastWriteTime,
                });
            }
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Debug($"RsFileDialog LoadDirectory('{path}'): {ex.Message}");
        }
    }

    private const string DrivesPath = "::drives";

    private static string? TryGetParent(string path)
    {
        if (path == DrivesPath) return null;
        try
        {
            var di = new DirectoryInfo(path);
            return di.Parent?.FullName ?? DrivesPath;   // above a drive root: the drive list
        }
        catch { return null; }
    }

    private static void Close(bool success, string? path)
    {
        _open = false;
        var cb = _callback;
        _callback = null;
        // Picking an image offers a crop first; the crop step calls back with either the original path or a cropped copy.
        if (success && _mode == Mode.OpenFile && !string.IsNullOrEmpty(path) && _extensions.Count > 0 && _extensions.All(IsImageExt) && RsImageCrop.CanCrop(path))
        {
            RsImageCrop.Open(path, cb);
            return;
        }
        try { cb?.Invoke(success, path ?? string.Empty); }
        catch (Exception ex) { Plugin.PluginLog.Debug("RsFileDialog callback: " + ex.Message); }
    }

    // Thumbnail loading

    private static IDalamudTextureWrap? GetOrLoadThumb(string path)
    {
        if (_thumbs.TryGetValue(path, out var t)) return t;

        // Not requested yet - kick off async load.
        if (_loadingThumbs.Add(path))
        {
            _thumbs[path] = null;
            var epoch = _folderEpoch;
            _ = LoadThumbAsync(path, epoch);

            // Bound the cache - only drop entries that ALREADY have textures, never a not-yet-loaded null slot (or the async load will race against an evicted key and never make it to the UI).
            if (_thumbs.Count > MaxCachedThumbs)
            {
                var toDrop = _thumbs
                    .Where(kv => kv.Value != null)
                    .Take(_thumbs.Count / 2)
                    .Select(kv => kv.Key)
                    .ToList();
                foreach (var k in toDrop)
                {
                    if (_thumbs.TryGetValue(k, out var tex) && tex != null)
                    {
                        AbsoluteRP.Helpers.TextureGraveyard.Enqueue(tex);
                    }
                    _thumbs.Remove(k);
                    _loadingThumbs.Remove(k);
                }
            }
        }
        return null;
    }

    private static async Task LoadThumbAsync(string path, int epoch)
    {
        // Serialize GDI+ work. System.Drawing.Bitmap / Graphics are not safe to run in parallel across threads and silently produce black or missing textures when hammered - which is exactly what a folder change was triggering (many loads at once).
        await _loadGate.WaitAsync();
        try
        {
            // Folder changed while we were queued - drop this load.
            if (epoch != _folderEpoch) return;

            var bytes = await File.ReadAllBytesAsync(path);
            byte[] scaled;
            try { scaled = AbsoluteRP.Helpers.Imaging.ScaleImageBytes(bytes, 128, 128); }
            catch { scaled = bytes; }
            var tex = await Plugin.TextureProvider.CreateFromImageAsync(scaled);

            // If the user navigated during the load, don't stash the texture - the cache was cleared and re-adding it would grow it without ever being displayed.
            if (epoch != _folderEpoch)
            {
                try { tex?.Dispose(); } catch { }
                return;
            }
            _thumbs[path] = tex;
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Debug($"RsFileDialog thumb '{path}': {ex.Message}");
            if (epoch == _folderEpoch) _thumbs[path] = null;
        }
        finally
        {
            _loadGate.Release();
        }
    }

    // Small helpers

    private static Vector4 Lerp(Vector4 a, Vector4 b, float t)
        => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, a.W + (b.W - a.W) * t);

    private static uint Pack(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);

    private static string PrettySize(long bytes)
    {
        if (bytes < 1024) return bytes + " B";
        if (bytes < 1024L * 1024) return (bytes / 1024f).ToString("0.0") + " KB";
        if (bytes < 1024L * 1024 * 1024) return (bytes / (1024f * 1024)).ToString("0.0") + " MB";
        return (bytes / (1024f * 1024 * 1024)).ToString("0.00") + " GB";
    }

    private static string TruncateForCell(string name, float maxWidth)
    {
        var full = ImGui.CalcTextSize(name).X;
        if (full <= maxWidth) return name;
        // Binary search for the longest prefix that fits with an ellipsis.
        var lo = 0; var hi = name.Length;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            var candidate = name[..mid] + "…";
            if (ImGui.CalcTextSize(candidate).X <= maxWidth) lo = mid;
            else hi = mid - 1;
        }
        return lo > 0 ? name[..lo] + "…" : name;
    }
}

// Drop-in replacement for Dalamud's FileDialogManager: same calls, but every dialog is the themed RsFileDialog. Draw()/Reset() are kept so existing windows compile; the dialog itself is drawn once per frame by the plugin.
public sealed class RsFileDialogManager
{
    private static List<string> ParseFilters(string? filters)
        => System.Text.RegularExpressions.Regex.Matches(filters ?? string.Empty, @"\.[A-Za-z0-9]+")
            .Select(m => m.Value.ToLowerInvariant()).Distinct().ToList();

    public void OpenFileDialog(string title, string filters, Action<bool, string> callback)
        => RsFileDialog.OpenFile(title, ParseFilters(filters), callback);

    public void OpenFileDialog(string title, string filters, Action<bool, string> callback, string? startPath, bool isModal = false)
        => RsFileDialog.OpenFile(title, ParseFilters(filters), callback, startPath);

    public void OpenFileDialog(string title, string filters, Action<bool, List<string>> callback, int selectionCountMax = 1, string? startPath = null, bool isModal = false)
        => RsFileDialog.OpenFile(title, ParseFilters(filters), (ok, path) => callback(ok && !string.IsNullOrEmpty(path), ok && !string.IsNullOrEmpty(path) ? new List<string> { path } : new List<string>()), startPath);

    public void SaveFileDialog(string title, string filters, string defaultFileName, string defaultExtension, Action<bool, string> callback, string? startPath = null, bool isModal = false)
        => RsFileDialog.SaveFile(title, defaultFileName, defaultExtension, callback, startPath);

    public void OpenFolderDialog(string title, Action<bool, string> callback, string? startPath = null, bool isModal = false)
        => RsFileDialog.PickFolder(title, callback, startPath);

    public void SaveFolderDialog(string title, string defaultFolderName, Action<bool, string> callback, string? startPath = null, bool isModal = false)
        => RsFileDialog.PickFolder(title, callback, startPath);

    public void Draw() => RsFileDialog.Draw();
    public void Reset() { }
}
