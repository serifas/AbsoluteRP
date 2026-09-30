using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;

namespace AbsoluteRP.Helpers
{
    // Textures must not be disposed in the same frame they were drawn: ImGui's draw list still points at them until the frame is rendered, and freeing a GPU resource the driver is about to read crashes inside the graphics driver (nvwgf2umx / d3d11). Anything that could have been drawn this frame goes here instead and is disposed a few frames later.
    public static class TextureGraveyard
    {
        private const int FramesToWait = 3;
        private static readonly List<(IDisposable item, int frame)> _pending = new();
        private static readonly object _lock = new();

        public static void Enqueue(IDisposable? item)
        {
            if (item == null) return;
            int frame;
            try { frame = ImGui.GetFrameCount(); } catch { frame = 0; }
            lock (_lock) _pending.Add((item, frame));
        }

        // Plugin unload: nothing will be drawn again, release everything now.
        public static void Flush()
        {
            List<IDisposable> all;
            lock (_lock) { all = new(); foreach (var p in _pending) all.Add(p.item); _pending.Clear(); }
            foreach (var d in all)
            {
                try { d.Dispose(); } catch (Exception ex) { Plugin.PluginLog?.Debug("TextureGraveyard flush: " + ex.Message); }
            }
        }

        // Once per frame from the plugin's draw loop.
        public static void Drain()
        {
            int now;
            try { now = ImGui.GetFrameCount(); } catch { return; }
            List<IDisposable>? ready = null;
            lock (_lock)
            {
                for (int i = _pending.Count - 1; i >= 0; i--)
                {
                    if (now - _pending[i].frame < FramesToWait) continue;
                    (ready ??= new()).Add(_pending[i].item);
                    _pending.RemoveAt(i);
                }
            }
            if (ready == null) return;
            foreach (var d in ready)
            {
                try { d.Dispose(); } catch (Exception ex) { Plugin.PluginLog?.Debug("TextureGraveyard: " + ex.Message); }
            }
        }
    }
}
