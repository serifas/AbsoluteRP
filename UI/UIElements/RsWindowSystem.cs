using Dalamud.Interface.Windowing;

namespace AbsoluteRP.RsUI;

/// Owns every RsWindow the plugin exposes. Wraps Dalamud's WindowSystem so lifecycle plumbing (Begin/End, z-order, focus, sibling-window compatibility) is Dalamud's problem while every pixel of window CONTENT stays ours via Draw. We hook PluginInterface.UiBuilder.Draw to two callbacks: our own animation tick (so opacities advance every frame off one shared delta) and Dalamud's WindowSystem.Draw (which iterates each open window and calls its Draw method).
public sealed class RsWindowSystem : IDisposable
{
    private readonly WindowSystem _system = new("AbsoluteRP.RsUI");
    private readonly Dictionary<string, RsWindow> _byId = new(StringComparer.Ordinal);
    private readonly List<RsWindow> _windows = new();
    private long _lastTickTicks;

    /// Register a window. Idempotent by id.
    public T Add<T>(T window) where T : RsWindow
    {
        if (_byId.ContainsKey(window.Id)) return (T)_byId[window.Id];
        _byId[window.Id] = window;
        _windows.Add(window);
        _system.AddWindow(window);
        return window;
    }

    /// Look up a window by id.
    public RsWindow? Get(string id) => _byId.TryGetValue(id, out var w) ? w : null;

    /// Toggle a window's IsOpen. No-op if the id is unknown.
    public void Toggle(string id)
    {
        if (_byId.TryGetValue(id, out var w))
            w.IsOpen = !w.IsOpen;
    }

    public void Open(string id)
    {
        if (_byId.TryGetValue(id, out var w))
            w.IsOpen = true;
    }

    public void Close(string id)
    {
        if (_byId.TryGetValue(id, out var w))
            w.IsOpen = false;
    }

    /// Opens every registered window. Wired to Dalamud's OpenMainUi.
    public void OpenAll()
    {
        foreach (var w in _windows) w.IsOpen = true;
    }

    /// One frame. Ticks animators off a shared delta, then hands off to Dalamud's WindowSystem which drives Begin/End for each open window and calls its overridden Draw method (RsWindow.Draw).
    public void Draw()
    {
        var nowTicks = DateTime.UtcNow.Ticks;
        var delta = _lastTickTicks == 0
            ? 0f
            : (float)Math.Min(0.25, (nowTicks - _lastTickTicks) / (double)TimeSpan.TicksPerSecond);
        _lastTickTicks = nowTicks;

        foreach (var w in _windows) w.TickAnimation(delta);
        _system.Draw();
    }

    public void Dispose()
    {
        _system.RemoveAllWindows();
        _windows.Clear();
        _byId.Clear();
    }
}
