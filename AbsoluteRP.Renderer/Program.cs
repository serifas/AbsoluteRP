using System;

namespace AbsoluteRP.Video.Renderer;

internal static class Program
{
    private static void StartParentWatchdog()
    {
        try
        {
            // Parent process id is the process that spawned us.
            var parentPid = System.Environment.ProcessId; // fallback
            try { parentPid = GetParentPid(); } catch { }
            if (parentPid <= 0) return;

            System.Diagnostics.Process? parent = null;
            try { parent = System.Diagnostics.Process.GetProcessById(parentPid); }
            catch { return; }

            var t = new System.Threading.Thread(() =>
            {
                try
                {
                    parent.WaitForExit();
                }
                catch { }
                // Parent gone — exit hard so the OS reclaims the .exe.
                try { System.Console.Error.WriteLine("renderer: parent gone, exiting"); } catch { }
                System.Environment.Exit(0);
            })
            { IsBackground = true, Name = "ParentWatchdog" };
            t.Start();
        }
        catch { }
    }

    // Query the parent PID via NtQueryInformationProcess.
    [System.Runtime.InteropServices.DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        System.IntPtr handle, int infoClass,
        ref PROCESS_BASIC_INFORMATION info, int size, out int returned);
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct PROCESS_BASIC_INFORMATION
    {
        public System.IntPtr Reserved1;
        public System.IntPtr PebBaseAddress;
        public System.IntPtr Reserved2a;
        public System.IntPtr Reserved2b;
        public System.IntPtr UniqueProcessId;
        public System.IntPtr InheritedFromUniqueProcessId;
    }
    private static int GetParentPid()
    {
        var pbi = new PROCESS_BASIC_INFORMATION();
        var status = NtQueryInformationProcess(
            System.Diagnostics.Process.GetCurrentProcess().Handle,
            0, ref pbi, System.Runtime.InteropServices.Marshal.SizeOf(pbi), out _);
        if (status != 0) return -1;
        return pbi.InheritedFromUniqueProcessId.ToInt32();
    }

    [STAThread]
    private static int Main(string[] args)
    {
        // Parse the args we care about
        var parsed = RendererArgs.Parse(args);
        if (parsed == null)
        {
            Console.Error.WriteLine(
                "AbsoluteRP.Renderer: missing or invalid args. Expected " +
                "--pipe <name> --adapter-luid-low <n> --adapter-luid-high <n> " +
                "--width <n> --height <n> --url <url>");
            return 2;
        }

        // Watchdog: if the parent process (the plugin host) dies without sending Shutdown 
        StartParentWatchdog();

        try
        {
            var host = new RendererHost(parsed);
            host.Run();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("AbsoluteRP.Renderer fatal: " + ex);
            return 1;
        }
        return 0;
    }
}

internal sealed class RendererArgs
{
    public string PipeName { get; init; } = string.Empty;
    public uint AdapterLuidLow { get; init; }
    public int AdapterLuidHigh { get; init; }
    public int Width { get; init; } = 1280;
    public int Height { get; init; } = 720;
    public string Url { get; init; } = "about:blank";
    public string? KeepAliveEvent { get; init; }
    public int InitialVolume { get; init; } = 100;

    public static RendererArgs? Parse(string[] args)
    {
        string? pipe = null;
        string? url = null;
        long? low = null;
        long? high = null;
        int width = 1280;
        int height = 720;
        string? keepAlive = null;
        int volume = 100;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pipe" when i + 1 < args.Length:
                    pipe = args[++i]; break;
                case "--adapter-luid-low" when i + 1 < args.Length:
                    low = long.Parse(args[++i]); break;
                case "--adapter-luid-high" when i + 1 < args.Length:
                    high = long.Parse(args[++i]); break;
                case "--width" when i + 1 < args.Length:
                    width = int.Parse(args[++i]); break;
                case "--height" when i + 1 < args.Length:
                    height = int.Parse(args[++i]); break;
                case "--url" when i + 1 < args.Length:
                    url = args[++i]; break;
                case "--keep-alive" when i + 1 < args.Length:
                    keepAlive = args[++i]; break;
                case "--volume" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out var v))
                        volume = Math.Clamp(v, 0, 100);
                    break;
            }
        }

        if (pipe == null || low == null || high == null || url == null) return null;
        return new RendererArgs
        {
            PipeName = pipe,
            AdapterLuidLow = unchecked((uint)low.Value),
            AdapterLuidHigh = unchecked((int)high.Value),
            Width = width,
            Height = height,
            Url = url,
            KeepAliveEvent = keepAlive,
            InitialVolume = volume,
        };
    }
}
