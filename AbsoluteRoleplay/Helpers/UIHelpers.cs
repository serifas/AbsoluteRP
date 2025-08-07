using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace AbsoluteRoleplay.Helpers
{
    public static class Logger
    {
        private static readonly object _lock = new();
        private static string? _logFilePath;

        private static string LogFilePath
        {
            get
            {
                if (_logFilePath == null)
                {
                    // Use the plugin's folder if available, otherwise fallback to current directory
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    _logFilePath = Path.Combine(baseDir, "AbsoluteRoleplay.log");
                }
                return _logFilePath;
            }
        }

        public static void Error(string message)
        {
            try
            {
                var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {message}{Environment.NewLine}";
                lock (_lock)
                {
                    File.AppendAllText(LogFilePath, logLine);
                }
            }
            catch
            {
                // Swallow exceptions to avoid recursive logging failures
            }
        }
    }
    public static class UIHelpers
    {
        public static float GlobalScale
        {
            get
            {
                // Use ImGui's font global scale, or replace with your own config if needed
                return ImGui.GetIO().FontGlobalScale;
            }
        }
        public static bool DrawTextButton(string text, Vector2 size, ImGuiButtonFlags flags = ImGuiButtonFlags.None)
        {
            return ImGui.Button(text, size);
        }
        public static void SelectableHelpMarker(string description)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
                ImGui.TextUnformatted(description);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
        }
    }
    public static class EnumerableExtensions
    {
        public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> source)
        {
            int index = 0;
            foreach (var item in source)
            {
                yield return (item, index++);
            }
        }
    }
}
