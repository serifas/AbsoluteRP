using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace AbsoluteRP.Video;


public sealed class RendererProcess : IDisposable
{
    private readonly IPluginLog log;
    private readonly string exePath;
    private readonly string pipeName;
    private readonly uint luidLow;
    private readonly int luidHigh;
    private readonly int width;
    private readonly int height;
    private readonly string url;
    private readonly int initialVolume;
    private readonly EventWaitHandle keepAliveEvent;
    private readonly string keepAliveName;
    private Process? process;
    private IntPtr jobHandle = IntPtr.Zero;
    private bool disposed;

    // Windows Job Object interop. Assigning the child process to a job with JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE means Windows itself kills the subprocess the moment our last handle to the job closes - which happens on unload, on process crash, on FFXIV exit, and on any ungraceful teardown path where our Dispose() never runs. This is the same lifetime trick Chromium/VSCode use for their helper processes and it's the only defence against the orphan-renderer problem that survives every failure mode.
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(
        IntPtr hJob, int JobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long   PerProcessUserTimeLimit;
        public long   PerJobUserTimeLimit;
        public uint   LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint   ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint   PriorityClass;
        public uint   SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public ulong ReadTransferCount,  WriteTransferCount,  OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    public bool IsRunning => process != null && !process.HasExited;
    public bool LaunchFailed { get; private set; }
    public string? LaunchError { get; private set; }

    // Fires once when the renderer process exits for any reason
    public event Action? Exited;

    public RendererProcess(
        IDalamudPluginInterface pi,
        IPluginLog log,
        string pipeName,
        uint luidLow,
        int luidHigh,
        int width,
        int height,
        string url,
        int initialVolume = 100)
    {
        this.log = log;
        this.pipeName = pipeName;
        this.luidLow = luidLow;
        this.luidHigh = luidHigh;
        this.width = width;
        this.height = height;
        this.url = url;
        this.initialVolume = Math.Clamp(initialVolume, 0, 100);

        var dir = pi.AssemblyLocation.Directory?.FullName ?? AppContext.BaseDirectory;
        // Prefer a native exe when a local build produced one; the shipped build is a dll run by dotnet.
        exePath = Path.Combine(dir, "renderer", "AbsoluteRP.Renderer.exe");
        dllPath = Path.Combine(dir, "renderer", "AbsoluteRP.Renderer.dll");

        // Manual reset
        keepAliveName = "AbsoluteRP.KeepAlive." + Guid.NewGuid().ToString("N");
        keepAliveEvent = new EventWaitHandle(false, EventResetMode.ManualReset, keepAliveName);
    }

    private readonly string dllPath;

    private static string? FindDotnetHost()
    {
        try
        {
            var rt = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();  
            var d = new DirectoryInfo(rt);
            for (int i = 0; i < 4 && d != null; i++)
            {
                var candidate = Path.Combine(d.FullName, "dotnet.exe");
                if (File.Exists(candidate)) return candidate;
                d = d.Parent;
            }
        }
        catch { }
        var env = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrEmpty(env) && File.Exists(Path.Combine(env, "dotnet.exe"))) return Path.Combine(env, "dotnet.exe");
        return null;
    }

    public void Start()
    {
        string fileName; string prefixArgs = string.Empty;
        if (File.Exists(exePath)) fileName = exePath;
        else if (File.Exists(dllPath))
        {
            var host = FindDotnetHost();
            if (host == null)
            {
                LaunchFailed = true;
                LaunchError = "no dotnet host found to run " + dllPath;
                log.Warning("[AbsoluteRP] {E}", LaunchError);
                return;
            }
            fileName = host;
            prefixArgs = "\"" + dllPath + "\" ";
        }
        else
        {
            LaunchFailed = true;
            LaunchError = "renderer missing at " + dllPath;
            log.Warning("[AbsoluteRP] {E}", LaunchError);
            return;
        }

        var args = string.Join(' ',
            "--pipe", pipeName,
            "--adapter-luid-low", luidLow.ToString(),
            "--adapter-luid-high", luidHigh.ToString(),
            "--width", width.ToString(),
            "--height", height.ToString(),
            "--keep-alive", keepAliveName,
            "--volume", initialVolume.ToString(),
            "--url", "\"" + url + "\"");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = prefixArgs + args,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(dllPath) ?? AppContext.BaseDirectory,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            process = Process.Start(psi);
            if (process == null)
            {
                LaunchFailed = true;
                LaunchError = "Process.Start returned null";
                return;
            }
            process.EnableRaisingEvents = true;
            // Bind the subprocess to a kill-on-close job object. Any code path that fails to run Dispose() - plugin crash, force unload, FFXIV terminating - will still cause the kernel to reap the renderer when our job handle is closed by the OS.
            TryBindToJob(process);
            // child stderr/stdout debugged out to the Dalamud log
            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data)) log.Debug("[AbsoluteRP renderer] {L}", e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data)) log.Warning("[AbsoluteRP renderer] {L}", e.Data);
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            // Exited Process
            process.Exited += (_, _) =>
            {
                try { log.Debug("[AbsoluteRP] renderer process exited (code {C})", process?.ExitCode ?? -1); }
                catch { }
                try { Exited?.Invoke(); } catch { /* never let a subscriber exception escape */ }
            };
        }
        catch (Exception ex)
        {
            LaunchFailed = true;
            LaunchError = ex.Message;
            log.Warning(ex, "[AbsoluteRP] failed to launch renderer");
        }
    }

    private void TryBindToJob(Process p)
    {
        try
        {
            jobHandle = CreateJobObject(IntPtr.Zero, null);
            if (jobHandle == IntPtr.Zero)
            {
                log.Debug("[AbsoluteRP] CreateJobObject failed (last error {E})", Marshal.GetLastWin32Error());
                return;
            }
            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
                },
            };
            var size = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
            var buf  = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(info, buf, fDeleteOld: false);
                if (!SetInformationJobObject(jobHandle, JobObjectExtendedLimitInformation, buf, (uint)size))
                    log.Debug("[AbsoluteRP] SetInformationJobObject failed (last error {E})", Marshal.GetLastWin32Error());
            }
            finally { Marshal.FreeHGlobal(buf); }
            if (!AssignProcessToJobObject(jobHandle, p.Handle))
            {
                // FFXIV may already have the child in a non-nestable job (some anticheat setups). Fall back gracefully - the keep-alive event + Dispose still handle the common cases; only ungraceful teardown loses the safety net.
                log.Debug("[AbsoluteRP] AssignProcessToJobObject failed (last error {E}) — falling back to keep-alive",
                    Marshal.GetLastWin32Error());
                CloseHandle(jobHandle);
                jobHandle = IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            log.Debug("[AbsoluteRP] job-object bind threw: {E}", ex.Message);
            if (jobHandle != IntPtr.Zero)
            {
                try { CloseHandle(jobHandle); } catch { }
                jobHandle = IntPtr.Zero;
            }
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        // Signal the keep-alive event so the renderer's own loop starts cleanup on its side; we don't wait for it, though. The old. WaitForExit(2000) blocked the caller for up to 2 seconds per disposed session - on a tab-switch that retires many at once that summed to double-digit-second freezes.
        try { keepAliveEvent.Set(); } catch { }
        try
        {
            if (process != null && !process.HasExited)
            {
                // Give the graceful path a tiny window, then Kill. Kill is synchronous but doesn't block on process exit.
                if (!process.WaitForExit(50))
                {
                    try { process.Kill(); } catch { }
                }
            }
        }
        catch { }
        try { process?.Dispose(); } catch { }
        try { keepAliveEvent.Dispose(); } catch { }
        // Closing the job handle kills every process still in it - the kernel does the actual reaping asynchronously so this call returns immediately regardless of child state.
        if (jobHandle != IntPtr.Zero)
        {
            try { CloseHandle(jobHandle); } catch { }
            jobHandle = IntPtr.Zero;
        }
    }
}
