using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AbsoluteRP.Windows.Ect
{
    /// <summary>
    /// Manages downloading and loading CefSharp dependencies at runtime.
    /// Similar to how Browsingway handles its CEF dependencies.
    /// </summary>
    public static class CefDependencyManager
    {
        // CefSharp.NETCore version with matching CEF binaries
        private const string CefVersion = "131.3.50";

        // Download URL - CefSharp.NETCore + CEF binaries package
        private static readonly string DownloadUrl = "https://github.com/serifas/Cefsharp/releases/download/init/cefsharp-netcore-131.3.50.zip";

        // SHA256 checksum
        private const string ExpectedChecksum = "DBEB34B2A677B154AF841FBD27522A0A0FDDE73674D95EAD823C7C5C81B22B53";

        private static string _dependencyPath;
        private static bool _dependenciesReady = false;
        private static bool _downloadInProgress = false;
        private static bool _downloadFailed = false;
        private static float _downloadProgress = 0f;
        private static string _statusMessage = "";
        private static bool _cefInitialized = false;
        private static bool _assemblyResolverRegistered = false;

        public static bool DependenciesReady => _dependenciesReady;
        public static bool DownloadInProgress => _downloadInProgress;
        public static bool DownloadFailed => _downloadFailed;
        public static float DownloadProgress => _downloadProgress;
        public static string StatusMessage => _statusMessage;
        public static string DependencyPath => _dependencyPath;
        public static bool CefInitialized => _cefInitialized;

        /// <summary>
        /// Initialize the dependency manager and check if dependencies are present
        /// </summary>
        public static void Initialize()
        {
            _dependencyPath = Path.Combine(
                Plugin.PluginInterface.ConfigDirectory.FullName,
                "dependencies",
                "cef");

            // Register assembly resolver early
            RegisterAssemblyResolver();

            // Check if dependencies are already present
            if (AreDependenciesPresent())
            {
                _dependenciesReady = true;
                _statusMessage = "CEF dependencies ready";
                Plugin.PluginLog.Info("CEF dependencies found and ready");
            }
            else
            {
                _statusMessage = "CEF dependencies not found";
                Plugin.PluginLog.Info("CEF dependencies not found at: " + _dependencyPath);
            }
        }

        /// <summary>
        /// Register assembly resolver for loading CefSharp from custom path
        /// </summary>
        private static void RegisterAssemblyResolver()
        {
            if (_assemblyResolverRegistered)
                return;

            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            _assemblyResolverRegistered = true;
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (string.IsNullOrEmpty(_dependencyPath))
                return null;

            var assemblyName = new AssemblyName(args.Name).Name;

            // Only handle CefSharp assemblies
            if (!assemblyName.StartsWith("CefSharp"))
                return null;

            var assemblyPath = Path.Combine(_dependencyPath, assemblyName + ".dll");

            if (File.Exists(assemblyPath))
            {
                try
                {
                    return Assembly.LoadFrom(assemblyPath);
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Error($"Failed to load assembly {assemblyName}: {ex.Message}");
                }
            }

            return null;
        }

        /// <summary>
        /// Check if the required CEF binaries are present
        /// </summary>
        private static bool AreDependenciesPresent()
        {
            if (!Directory.Exists(_dependencyPath))
                return false;

            // Check for key CEF files
            var requiredFiles = new[]
            {
                "libcef.dll",
                "CefSharp.dll",
                "CefSharp.Core.dll",
                "CefSharp.OffScreen.dll",
                "CefSharp.BrowserSubprocess.exe"
            };

            foreach (var file in requiredFiles)
            {
                var filePath = Path.Combine(_dependencyPath, file);
                if (!File.Exists(filePath))
                {
                    Plugin.PluginLog.Debug($"Missing CEF file: {file}");
                    return false;
                }
            }

            // Check version file
            var versionFile = Path.Combine(_dependencyPath, "VERSION");
            if (!File.Exists(versionFile))
            {
                Plugin.PluginLog.Debug("Missing VERSION file");
                return false;
            }

            var installedVersion = File.ReadAllText(versionFile).Trim();
            if (installedVersion != CefVersion)
            {
                Plugin.PluginLog.Debug($"Version mismatch: {installedVersion} != {CefVersion}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Start downloading CEF dependencies
        /// </summary>
        public static async Task DownloadDependenciesAsync()
        {
            if (_downloadInProgress || _dependenciesReady)
                return;

            _downloadInProgress = true;
            _downloadFailed = false;
            _downloadProgress = 0f;
            _statusMessage = "Starting download...";

            try
            {
                // Create temp directory for download
                var tempDir = Path.Combine(Path.GetTempPath(), "ARP_CefDownload_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(tempDir);
                var zipPath = Path.Combine(tempDir, "cefsharp.zip");

                Plugin.PluginLog.Info($"Downloading CEF from: {DownloadUrl}");

                // Download the zip file using HttpClient for better progress tracking
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromMinutes(10);

                    using (var response = await httpClient.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

                        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                        var canReportProgress = totalBytes != -1;

                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            var buffer = new byte[8192];
                            long totalBytesRead = 0;
                            int bytesRead;

                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead);
                                totalBytesRead += bytesRead;

                                if (canReportProgress)
                                {
                                    var progress = (double)totalBytesRead / totalBytes;
                                    _downloadProgress = (float)(progress * 0.8); // 80% for download
                                    var mbDownloaded = totalBytesRead / 1024.0 / 1024.0;
                                    var mbTotal = totalBytes / 1024.0 / 1024.0;
                                    _statusMessage = $"Downloading: {mbDownloaded:F1} / {mbTotal:F1} MB";
                                }
                            }
                        }
                    }
                }

                _statusMessage = "Verifying download...";
                _downloadProgress = 0.82f;

                // Verify checksum if provided
                if (!string.IsNullOrEmpty(ExpectedChecksum))
                {
                    var actualChecksum = ComputeChecksum(zipPath);
                    if (!actualChecksum.Equals(ExpectedChecksum, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new Exception($"Checksum verification failed.\nExpected: {ExpectedChecksum}\nActual: {actualChecksum}");
                    }
                }

                _statusMessage = "Extracting files...";
                _downloadProgress = 0.85f;

                // Remove old dependency directory if exists
                if (Directory.Exists(_dependencyPath))
                {
                    try
                    {
                        Directory.Delete(_dependencyPath, true);
                    }
                    catch (Exception ex)
                    {
                        Plugin.PluginLog.Warning($"Failed to delete old dependencies: {ex.Message}");
                    }
                }

                Directory.CreateDirectory(_dependencyPath);

                // Extract zip
                _statusMessage = "Extracting CEF binaries...";
                _downloadProgress = 0.9f;

                ZipFile.ExtractToDirectory(zipPath, _dependencyPath);

                // Write version file
                File.WriteAllText(Path.Combine(_dependencyPath, "VERSION"), CefVersion);

                _statusMessage = "Cleaning up...";
                _downloadProgress = 0.95f;

                // Cleanup temp files
                try
                {
                    File.Delete(zipPath);
                    Directory.Delete(tempDir, true);
                }
                catch { }

                _downloadProgress = 1f;
                _statusMessage = "CEF dependencies ready! Restart required.";
                _dependenciesReady = true;

                Plugin.PluginLog.Info("CEF dependencies downloaded and extracted successfully");
            }
            catch (Exception ex)
            {
                _downloadFailed = true;
                _statusMessage = $"Download failed: {ex.Message}";
                Plugin.PluginLog.Error($"Failed to download CEF dependencies: {ex}");
            }
            finally
            {
                _downloadInProgress = false;
            }
        }

        private static string ComputeChecksum(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
            }
        }

        /// <summary>
        /// Initialize CEF. Must be called after dependencies are ready.
        /// </summary>
        public static bool InitializeCef()
        {
            if (_cefInitialized)
                return true;

            if (!_dependenciesReady)
            {
                Plugin.PluginLog.Error("Cannot initialize CEF: dependencies not ready");
                return false;
            }

            try
            {
                // Set DLL search path to include CEF directory
                SetDllDirectory(_dependencyPath);

                // Add to PATH as well
                var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                if (!currentPath.Contains(_dependencyPath))
                {
                    Environment.SetEnvironmentVariable("PATH", _dependencyPath + ";" + currentPath);
                }

                _cefInitialized = true;
                Plugin.PluginLog.Info("CEF environment configured successfully");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"Failed to initialize CEF: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Shutdown CEF. Call when plugin unloads.
        /// </summary>
        public static void Shutdown()
        {
            // CEF shutdown is handled by the browser renderer
            _cefInitialized = false;
        }

        /// <summary>
        /// Clear downloaded CEF dependencies to force re-download.
        /// </summary>
        public static void ClearDependencies()
        {
            try
            {
                if (Directory.Exists(_dependencyPath))
                {
                    Directory.Delete(_dependencyPath, true);
                    Plugin.PluginLog.Info("CEF dependencies cleared");
                }

                // Reset state
                _dependenciesReady = false;
                _downloadFailed = false;
                _downloadProgress = 0f;
                _statusMessage = "";
                _cefInitialized = false;
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"Failed to clear dependencies: {ex.Message}");
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);
    }
}
