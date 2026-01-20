using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Drawing;
using System.Windows.Forms;

namespace AbsoluteRP.Windows.Ect
{
    /// <summary>
    /// A standalone Windows Forms window that hosts a WebView2 browser for YouTube video playback.
    /// This allows videos to be played directly without requiring Browsingway.
    /// </summary>
    public class YouTubePlayerWindow : Form
    {
        private WebView2 webView;
        private string videoId;
        private string videoUrl;
        private bool isFullscreen = false;
        private FormWindowState previousWindowState;
        private FormBorderStyle previousBorderStyle;
        private Rectangle previousBounds;

        private static YouTubePlayerWindow currentInstance;

        public YouTubePlayerWindow(string videoId, string originalUrl)
        {
            this.videoId = videoId;
            this.videoUrl = originalUrl;

            InitializeForm();
            InitializeWebView();
        }

        private void InitializeForm()
        {
            this.Text = "YouTube Player";
            this.Size = new Size(854, 530); // 16:9 + controls
            this.MinimumSize = new Size(400, 300);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.TopMost = true; // Keep on top of game

            // Handle form closing
            this.FormClosing += (s, e) =>
            {
                if (currentInstance == this)
                    currentInstance = null;

                webView?.Dispose();
            };

            // Re-assert TopMost when activated to ensure it stays on top
            this.Activated += (s, e) =>
            {
                if (!isFullscreen)
                {
                    this.TopMost = true;
                }
            };

            // Handle keyboard shortcuts
            this.KeyPreview = true;
            this.KeyDown += YouTubePlayerWindow_KeyDown;
        }

        private async void InitializeWebView()
        {
            webView = new WebView2
            {
                Dock = DockStyle.Fill
            };

            this.Controls.Add(webView);

            try
            {
                // Initialize WebView2 with a user data folder in temp
                var userDataFolder = Path.Combine(Path.GetTempPath(), "ARPYouTubePlayer");
                var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await webView.EnsureCoreWebView2Async(environment);

                // Configure settings
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

                // Get video title from page and set window title
                webView.CoreWebView2.DocumentTitleChanged += (s, e) =>
                {
                    var title = webView.CoreWebView2.DocumentTitle;
                    if (!string.IsNullOrEmpty(title) && title != "YouTube")
                    {
                        this.Invoke(() =>
                        {
                            var cleanTitle = title.Replace(" - YouTube", "");
                            this.Text = $"{cleanTitle}";
                        });
                    }
                };

                // Inject script to auto-click fullscreen button after page loads
                webView.CoreWebView2.NavigationCompleted += async (s, e) =>
                {
                    if (e.IsSuccess)
                    {
                        // Wait a bit for the player to initialize, then click fullscreen
                        await Task.Delay(1500);
                        try
                        {
                            // Try to click the fullscreen button on YouTube
                            await webView.CoreWebView2.ExecuteScriptAsync(@"
                                (function() {
                                    // Try YouTube's fullscreen button
                                    var fsButton = document.querySelector('.ytp-fullscreen-button');
                                    if (fsButton) {
                                        fsButton.click();
                                        return 'clicked fullscreen';
                                    }
                                    // Alternative: try using the YouTube player API if available
                                    var player = document.querySelector('video');
                                    if (player && player.requestFullscreen) {
                                        player.requestFullscreen();
                                        return 'requested fullscreen on video';
                                    }
                                    return 'no fullscreen button found';
                                })();
                            ");
                        }
                        catch { /* Ignore script errors */ }
                    }
                };

                // Navigate to YouTube watch page (more reliable than embed)
                // The embed player can give Error 153 due to autoplay/referrer restrictions
                string watchUrl = $"https://www.youtube.com/watch?v={videoId}";
                webView.CoreWebView2.Navigate(watchUrl);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"Failed to initialize WebView2: {ex.Message}");
                MessageBox.Show(
                    $"Failed to initialize video player.\n\nError: {ex.Message}\n\nMake sure WebView2 Runtime is installed.",
                    "YouTube Player Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                this.Close();
            }
        }

        private void YouTubePlayerWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                if (isFullscreen)
                    ToggleFullscreen();
                else
                    this.Close();
            }
            else if (e.KeyCode == Keys.F || e.KeyCode == Keys.F11)
            {
                ToggleFullscreen();
            }
        }

        private void ToggleFullscreen()
        {
            if (!isFullscreen)
            {
                // Save current state
                previousWindowState = this.WindowState;
                previousBorderStyle = this.FormBorderStyle;
                previousBounds = this.Bounds;

                // Go fullscreen
                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Maximized;
                isFullscreen = true;
            }
            else
            {
                // Restore previous state
                this.FormBorderStyle = previousBorderStyle;
                this.WindowState = previousWindowState;
                this.Bounds = previousBounds;
                isFullscreen = false;
            }
        }

        /// <summary>
        /// Opens a YouTube video in the player window.
        /// If a player is already open, it will be reused.
        /// </summary>
        public static void OpenVideo(string videoId, string originalUrl)
        {
            // Run on UI thread
            if (currentInstance != null && !currentInstance.IsDisposed)
            {
                // Reuse existing window
                currentInstance.Invoke(() =>
                {
                    currentInstance.videoId = videoId;
                    currentInstance.videoUrl = originalUrl;
                    string watchUrl = $"https://www.youtube.com/watch?v={videoId}";
                    currentInstance.webView?.CoreWebView2?.Navigate(watchUrl);
                    currentInstance.BringToFront();
                    currentInstance.Activate();
                });
            }
            else
            {
                // Create new window on a separate STA thread
                var thread = new Thread(() =>
                {
                    Application.EnableVisualStyles();
                    currentInstance = new YouTubePlayerWindow(videoId, originalUrl);
                    Application.Run(currentInstance);
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
            }
        }

        /// <summary>
        /// Closes any open player window.
        /// </summary>
        public static void ClosePlayer()
        {
            if (currentInstance != null && !currentInstance.IsDisposed)
            {
                currentInstance.Invoke(() => currentInstance.Close());
            }
        }

        /// <summary>
        /// Check if the player is currently open.
        /// </summary>
        public static bool IsPlayerOpen => currentInstance != null && !currentInstance.IsDisposed;
    }
}
