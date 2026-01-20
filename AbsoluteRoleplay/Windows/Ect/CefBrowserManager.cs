using Dalamud.Interface.Textures.TextureWraps;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AbsoluteRP.Windows.Ect
{
    /// <summary>
    /// Manages CEF browser instances for rendering web content to textures.
    /// Uses reflection to load CefSharp dynamically when dependencies are available.
    /// </summary>
    public class CefBrowserManager : IDisposable
    {
        private static CefBrowserManager _instance;
        private static bool _cefInitialized = false;
        private static readonly object _initLock = new object();

        // CefSharp types loaded via reflection
        private static Type _cefType;
        private static Type _cefSettingsType;
        private static Type _browserSettingsType;
        private static Type _chromiumWebBrowserType;

        // Browser instance (loaded dynamically)
        private object _browser;
        private int _width;
        private int _height;
        private string _currentUrl;

        // Texture rendering
        private IDalamudTextureWrap _texture;
        private byte[] _pixelBuffer;
        private bool _frameReady = false;
        private readonly object _frameLock = new object();
        private bool _isDisposed = false;

        public static CefBrowserManager Instance => _instance;
        public static bool IsCefAvailable => CefDependencyManager.DependenciesReady;
        public static bool IsCefInitialized => _cefInitialized;

        public bool IsReady => _browser != null && !_isDisposed;
        public IDalamudTextureWrap Texture => _texture;
        public bool HasNewFrame => _frameReady;

        public int Width => _width;
        public int Height => _height;

        /// <summary>
        /// Initialize CEF globally. Call once at plugin startup after dependencies are ready.
        /// </summary>
        public static bool InitializeCef()
        {
            if (_cefInitialized)
                return true;

            lock (_initLock)
            {
                if (_cefInitialized)
                    return true;

                if (!CefDependencyManager.DependenciesReady)
                {
                    Plugin.PluginLog.Error("CEF dependencies not ready");
                    return false;
                }

                try
                {
                    // Initialize CEF environment first
                    if (!CefDependencyManager.InitializeCef())
                        return false;

                    // Load CefSharp assemblies via reflection
                    var cefPath = CefDependencyManager.DependencyPath;

                    var cefSharpCorePath = Path.Combine(cefPath, "CefSharp.Core.dll");
                    var cefSharpPath = Path.Combine(cefPath, "CefSharp.dll");
                    var cefSharpOffScreenPath = Path.Combine(cefPath, "CefSharp.OffScreen.dll");

                    // Also need to load CefSharp.Core.Runtime for mixed-mode assembly
                    var cefSharpCoreRuntimePath = Path.Combine(cefPath, "CefSharp.Core.Runtime.dll");

                    // Load assemblies
                    Assembly cefSharpCore = null;
                    Assembly cefSharpCoreRuntime = null;
                    Assembly cefSharp = null;
                    Assembly cefSharpOffScreen = null;

                    // Load Core.Runtime first - it contains native code that Core depends on
                    if (File.Exists(cefSharpCoreRuntimePath))
                    {
                        try
                        {
                            cefSharpCoreRuntime = Assembly.LoadFrom(cefSharpCoreRuntimePath);
                            Plugin.PluginLog.Info($"CefSharp.Core.Runtime loaded successfully");
                        }
                        catch (Exception ex)
                        {
                            Plugin.PluginLog.Error($"Failed to load CefSharp.Core.Runtime: {ex.Message}");
                        }
                    }

                    if (File.Exists(cefSharpCorePath))
                    {
                        try
                        {
                            cefSharpCore = Assembly.LoadFrom(cefSharpCorePath);
                        }
                        catch (Exception ex)
                        {
                            Plugin.PluginLog.Error($"Failed to load CefSharp.Core: {ex.Message}");
                        }
                    }

                    if (File.Exists(cefSharpPath))
                    {
                        try
                        {
                            cefSharp = Assembly.LoadFrom(cefSharpPath);
                        }
                        catch (Exception ex)
                        {
                            Plugin.PluginLog.Error($"Failed to load CefSharp: {ex.Message}");
                        }
                    }

                    if (File.Exists(cefSharpOffScreenPath))
                    {
                        try
                        {
                            cefSharpOffScreen = Assembly.LoadFrom(cefSharpOffScreenPath);
                        }
                        catch (Exception ex)
                        {
                            Plugin.PluginLog.Error($"Failed to load CefSharp.OffScreen: {ex.Message}");
                        }
                    }

                    // Log what we loaded (using Warning so it shows in filtered logs)
                    Plugin.PluginLog.Warning($"CefSharp.Core.Runtime loaded: {cefSharpCoreRuntime != null}");
                    Plugin.PluginLog.Warning($"CefSharp.Core loaded: {cefSharpCore != null}");
                    Plugin.PluginLog.Warning($"CefSharp loaded: {cefSharp != null}");
                    Plugin.PluginLog.Warning($"CefSharp.OffScreen loaded: {cefSharpOffScreen != null}");

                    // Search for types in all loaded assemblies (include Core.Runtime)
                    var assemblies = new[] { cefSharpCoreRuntime, cefSharpCore, cefSharp, cefSharpOffScreen }.Where(a => a != null).ToArray();

                    foreach (var asm in assemblies)
                    {
                        if (_cefType == null)
                            _cefType = asm.GetType("CefSharp.Cef");
                        if (_cefSettingsType == null)
                            _cefSettingsType = asm.GetType("CefSharp.OffScreen.CefSettings"); // CefSettings is in OffScreen namespace
                        if (_browserSettingsType == null)
                            _browserSettingsType = asm.GetType("CefSharp.BrowserSettings");
                        if (_chromiumWebBrowserType == null)
                            _chromiumWebBrowserType = asm.GetType("CefSharp.OffScreen.ChromiumWebBrowser");
                    }

                    // Log what types we found
                    Plugin.PluginLog.Warning($"Cef type: {_cefType?.FullName ?? "NOT FOUND"}");
                    Plugin.PluginLog.Warning($"CefSettings type: {_cefSettingsType?.FullName ?? "NOT FOUND"}");
                    Plugin.PluginLog.Warning($"BrowserSettings type: {_browserSettingsType?.FullName ?? "NOT FOUND"}");
                    Plugin.PluginLog.Warning($"ChromiumWebBrowser type: {_chromiumWebBrowserType?.FullName ?? "NOT FOUND"}");

                    if (_cefType == null || _cefSettingsType == null || _chromiumWebBrowserType == null)
                    {
                        // List all types in loaded assemblies for debugging
                        foreach (var asm in assemblies)
                        {
                            Plugin.PluginLog.Info($"Types in {asm.GetName().Name}:");
                            try
                            {
                                foreach (var type in asm.GetExportedTypes().Take(20))
                                {
                                    Plugin.PluginLog.Info($"  - {type.FullName}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Plugin.PluginLog.Warning($"  Could not enumerate types: {ex.Message}");
                            }
                        }
                        Plugin.PluginLog.Error("Failed to load required CefSharp types");
                        return false;
                    }

                    // Check if already initialized
                    var isInitializedProp = _cefType.GetProperty("IsInitialized");
                    if (isInitializedProp != null)
                    {
                        var isInitValue = isInitializedProp.GetValue(null);
                        if (isInitValue is bool isInit && isInit)
                        {
                            _cefInitialized = true;
                            Plugin.PluginLog.Info("CEF was already initialized");
                            return true;
                        }
                    }

                    // Create CefSettings
                    var settings = Activator.CreateInstance(_cefSettingsType);

                    // Set settings properties
                    var cachePathProp = _cefSettingsType.GetProperty("CachePath");
                    var windowlessRenderingProp = _cefSettingsType.GetProperty("WindowlessRenderingEnabled");
                    var logSeverityProp = _cefSettingsType.GetProperty("LogSeverity");
                    var browserSubprocessPathProp = _cefSettingsType.GetProperty("BrowserSubprocessPath");

                    var cachePath = Path.Combine(CefDependencyManager.DependencyPath, "cache");
                    Directory.CreateDirectory(cachePath);

                    cachePathProp?.SetValue(settings, cachePath);
                    windowlessRenderingProp?.SetValue(settings, true);

                    // Set LogSeverity to Disable (enum value)
                    var logSeverityType = cefSharpCore.GetType("CefSharp.LogSeverity");
                    if (logSeverityType != null && logSeverityProp != null)
                    {
                        var disableValue = Enum.Parse(logSeverityType, "Disable");
                        logSeverityProp.SetValue(settings, disableValue);
                    }

                    // Set subprocess path
                    var subprocessPath = Path.Combine(CefDependencyManager.DependencyPath, "CefSharp.BrowserSubprocess.exe");
                    browserSubprocessPathProp?.SetValue(settings, subprocessPath);

                    // Initialize CEF
                    var initMethod = _cefType.GetMethod("Initialize", new[] { _cefSettingsType, typeof(bool), typeof(object) });
                    if (initMethod != null)
                    {
                        var result = initMethod.Invoke(null, new object[] { settings, true, null });
                        if (result is bool success && success)
                        {
                            _cefInitialized = true;
                            Plugin.PluginLog.Info("CEF initialized successfully");
                            return true;
                        }
                    }

                    // Try alternative Initialize overload
                    var initMethod2 = _cefType.GetMethod("Initialize", new[] { _cefSettingsType });
                    if (initMethod2 != null)
                    {
                        var result = initMethod2.Invoke(null, new object[] { settings });
                        if (result is bool success && success)
                        {
                            _cefInitialized = true;
                            Plugin.PluginLog.Info("CEF initialized successfully (alt method)");
                            return true;
                        }
                    }

                    Plugin.PluginLog.Error("Failed to initialize CEF");
                    return false;
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Error($"Failed to initialize CEF: {ex}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Shutdown CEF globally. Call at plugin unload.
        /// </summary>
        public static void ShutdownCef()
        {
            if (!_cefInitialized || _cefType == null)
                return;

            try
            {
                var shutdownMethod = _cefType.GetMethod("Shutdown");
                shutdownMethod?.Invoke(null, null);
                _cefInitialized = false;
                Plugin.PluginLog.Info("CEF shutdown successfully");
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"Failed to shutdown CEF: {ex.Message}");
            }
        }

        public CefBrowserManager(int width, int height)
        {
            _width = width;
            _height = height;
            _pixelBuffer = new byte[width * height * 4];
            _instance = this;
        }

        /// <summary>
        /// Create and navigate to a URL
        /// </summary>
        public bool Navigate(string url)
        {
            if (!_cefInitialized)
            {
                Plugin.PluginLog.Error("CEF not initialized");
                return false;
            }

            try
            {
                _currentUrl = url;

                // Create browser if not exists
                if (_browser == null)
                {
                    // Create BrowserSettings
                    object browserSettings = null;
                    if (_browserSettingsType != null)
                    {
                        browserSettings = Activator.CreateInstance(_browserSettingsType);
                        var frameRateProp = _browserSettingsType.GetProperty("WindowlessFrameRate");
                        frameRateProp?.SetValue(browserSettings, 30);
                    }

                    // Create ChromiumWebBrowser
                    var constructor = _chromiumWebBrowserType.GetConstructor(new[] { typeof(string), _browserSettingsType });
                    if (constructor != null)
                    {
                        _browser = constructor.Invoke(new object[] { url, browserSettings });
                    }
                    else
                    {
                        // Try simpler constructor
                        var simpleConstructor = _chromiumWebBrowserType.GetConstructor(new[] { typeof(string) });
                        if (simpleConstructor != null)
                        {
                            _browser = simpleConstructor.Invoke(new object[] { url });
                        }
                    }

                    if (_browser == null)
                    {
                        Plugin.PluginLog.Error("Failed to create browser instance");
                        return false;
                    }

                    // Set size
                    var sizeProp = _chromiumWebBrowserType.GetProperty("Size");
                    if (sizeProp != null)
                    {
                        sizeProp.SetValue(_browser, new System.Drawing.Size(_width, _height));
                    }

                    // Subscribe to Paint event
                    var paintEvent = _chromiumWebBrowserType.GetEvent("Paint");
                    if (paintEvent != null)
                    {
                        var handlerType = paintEvent.EventHandlerType;
                        var onPaintMethod = this.GetType().GetMethod("OnBrowserPaint", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (onPaintMethod != null)
                        {
                            var handler = Delegate.CreateDelegate(handlerType, this, onPaintMethod);
                            paintEvent.AddEventHandler(_browser, handler);
                        }
                    }

                    Plugin.PluginLog.Info($"Browser created and navigating to: {url}");
                }
                else
                {
                    // Navigate existing browser
                    var loadUrlMethod = _chromiumWebBrowserType.GetMethod("LoadUrl");
                    loadUrlMethod?.Invoke(_browser, new object[] { url });
                }

                return true;
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"Failed to navigate: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Paint event handler - receives rendered frame data
        /// </summary>
        private void OnBrowserPaint(object sender, object e)
        {
            if (_isDisposed)
                return;

            try
            {
                // Get paint event args properties via reflection
                var argsType = e.GetType();
                var widthProp = argsType.GetProperty("Width");
                var heightProp = argsType.GetProperty("Height");
                var bufferHandleProp = argsType.GetProperty("BufferHandle");

                if (widthProp == null || heightProp == null || bufferHandleProp == null)
                    return;

                int width = (int)widthProp.GetValue(e);
                int height = (int)heightProp.GetValue(e);
                IntPtr bufferHandle = (IntPtr)bufferHandleProp.GetValue(e);

                if (width <= 0 || height <= 0 || bufferHandle == IntPtr.Zero)
                    return;

                int requiredSize = width * height * 4;

                lock (_frameLock)
                {
                    if (_pixelBuffer == null || _pixelBuffer.Length != requiredSize)
                    {
                        _pixelBuffer = new byte[requiredSize];
                        _width = width;
                        _height = height;
                    }

                    // Copy pixel data (BGRA format from CEF)
                    Marshal.Copy(bufferHandle, _pixelBuffer, 0, requiredSize);
                    _frameReady = true;
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Warning($"Paint handler error: {ex.Message}");
            }
        }

        /// <summary>
        /// Update texture with latest frame. Call from render thread.
        /// </summary>
        public IDalamudTextureWrap UpdateTexture()
        {
            lock (_frameLock)
            {
                if (_frameReady && _pixelBuffer != null && _pixelBuffer.Length > 0)
                {
                    try
                    {
                        // Dispose old texture
                        _texture?.Dispose();

                        // Convert BGRA to RGBA
                        byte[] rgbaBuffer = new byte[_pixelBuffer.Length];
                        for (int i = 0; i < _pixelBuffer.Length; i += 4)
                        {
                            rgbaBuffer[i] = _pixelBuffer[i + 2];     // R (was B)
                            rgbaBuffer[i + 1] = _pixelBuffer[i + 1]; // G
                            rgbaBuffer[i + 2] = _pixelBuffer[i];     // B (was R)
                            rgbaBuffer[i + 3] = _pixelBuffer[i + 3]; // A
                        }

                        // Create texture
                        _texture = Plugin.TextureProvider.CreateFromRaw(
                            new Dalamud.Interface.Textures.RawImageSpecification(_width, _height, 28),
                            rgbaBuffer);

                        _frameReady = false;
                    }
                    catch (Exception ex)
                    {
                        Plugin.PluginLog.Warning($"Failed to create texture: {ex.Message}");
                    }
                }
            }

            return _texture;
        }

        /// <summary>
        /// Send mouse click to browser
        /// </summary>
        public void SendMouseClick(int x, int y)
        {
            if (_browser == null)
                return;

            try
            {
                // Get browser host
                var getBrowserHostMethod = _chromiumWebBrowserType.GetMethod("GetBrowserHost");
                var host = getBrowserHostMethod?.Invoke(_browser, null);

                if (host != null)
                {
                    var hostType = host.GetType();
                    var sendMouseClickMethod = hostType.GetMethod("SendMouseClickEvent");

                    if (sendMouseClickMethod != null)
                    {
                        // SendMouseClickEvent(int x, int y, MouseButtonType mouseButtonType, bool mouseUp, int clickCount, CefEventFlags modifiers)
                        sendMouseClickMethod.Invoke(host, new object[] { x, y, 0, false, 1, 0 }); // Mouse down
                        sendMouseClickMethod.Invoke(host, new object[] { x, y, 0, true, 1, 0 });  // Mouse up
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Warning($"Failed to send mouse click: {ex.Message}");
            }
        }

        /// <summary>
        /// Send mouse move to browser
        /// </summary>
        public void SendMouseMove(int x, int y)
        {
            if (_browser == null)
                return;

            try
            {
                var getBrowserHostMethod = _chromiumWebBrowserType.GetMethod("GetBrowserHost");
                var host = getBrowserHostMethod?.Invoke(_browser, null);

                if (host != null)
                {
                    var hostType = host.GetType();
                    var sendMouseMoveMethod = hostType.GetMethod("SendMouseMoveEvent");

                    if (sendMouseMoveMethod != null)
                    {
                        sendMouseMoveMethod.Invoke(host, new object[] { x, y, false, 0 });
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Warning($"Failed to send mouse move: {ex.Message}");
            }
        }

        /// <summary>
        /// Resize the browser
        /// </summary>
        public void Resize(int width, int height)
        {
            if (width == _width && height == _height)
                return;

            if (width < 100 || height < 100)
                return;

            _width = width;
            _height = height;

            if (_browser != null)
            {
                var sizeProp = _chromiumWebBrowserType?.GetProperty("Size");
                sizeProp?.SetValue(_browser, new System.Drawing.Size(width, height));
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            if (_browser != null)
            {
                try
                {
                    var disposeMethod = _chromiumWebBrowserType?.GetMethod("Dispose");
                    disposeMethod?.Invoke(_browser, null);
                }
                catch { }
                _browser = null;
            }

            _texture?.Dispose();
            _texture = null;

            if (_instance == this)
                _instance = null;

            Plugin.PluginLog.Info("CefBrowserManager disposed");
        }
    }
}
