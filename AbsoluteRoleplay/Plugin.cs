using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using AbsoluteRoleplay.Windows;
using Dalamud.Game.ClientState.Objects;
using System.Runtime.InteropServices;
using Networking;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text;
using System;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using System.Threading.Tasks;
using AbsoluteRoleplay.Helpers;
using System.Numerics;
using OtterGui.Log;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System.Threading.Channels;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using AbsoluteRoleplay.Windows.Profiles;
//using AbsoluteRoleplay.Windows.Chat;
namespace AbsoluteRoleplay
{
    public partial class Plugin : IDalamudPlugin
    {
        public static Plugin plugin;
        public string username = "";
        public string password = "";
        private const string CommandName = "/arp";
        //WIP
        private const string ChatToggleCommand = "/arpchat";
        
        public bool loggedIn;
        private IDtrBar dtrBar;
        private IDtrBarEntry? statusBarEntry;
        private IDtrBarEntry? connectionsBarEntry;
        private IDtrBarEntry? chatBarEntry;
        public static bool BarAdded = false;
        internal static  float timer = 0f;
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null;
        [PluginService] internal static IFramework Framework { get; private set; } = null;
        [PluginService] internal static ICondition Condition { get; private set; } = null;
        [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null;
        [PluginService] internal static IClientState ClientState { get; private set; } = null;
        [PluginService] internal static ITargetManager TargetManager { get; private set; } = null;
        [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null;

        [LibraryImport("user32")]

        internal static partial short GetKeyState(int nVirtKey);
        //used for making sure click happy people don't mess up their hard work
        public static bool CtrlPressed() => (GetKeyState(0xA2) & 0x8000) != 0 || (GetKeyState(0xA3) & 0x8000) != 0;
        public Configuration Configuration { get; init; }


        private readonly WindowSystem WindowSystem = new("Absolute Roleplay");
        private OptionsWindow OptionsWindow { get; init; }
        private VerificationWindow VerificationWindow { get; init; }
        private RestorationWindow RestorationWindow { get; init; }
        private ReportWindow ReportWindow { get; init; }
        private MainPanel MainPanel { get; init; }
        private ProfileWindow ProfileWindow { get; init; }
        private BookmarksWindow BookmarksWindow { get; init; }
        private TargetWindow TargetWindow { get; init; }
        private ImagePreview ImagePreview { get; init; }
        private TOS TermsWindow { get; init; }
        private ConnectionsWindow ConnectionsWindow { get; init; }
        public bool ConnectionLoaded = false;

        //logger for printing errors and such
        public Logger logger = new Logger();


        public float BlinkInterval = 0.5f;
        public bool newConnection;
        public bool ControlsLogin = false;
        private bool shouldCheckTarget = true;
        private bool isWindowOpen;
       // private bool chatLoaded = false;


        //initialize our plugin
        public Plugin(
            IDalamudPluginInterface pluginInterface,
            ICommandManager commandManager,
            ITextureProvider textureProvider,
            IClientState clientState,
            ITargetManager targetManager,
            IFramework framework,
            ICondition condition,
            IContextMenu contextMenu,
            IDtrBar dtrBar
            )
        {
            plugin = this;

            // Wrap the original service
            this.dtrBar = dtrBar;
            PluginInterface = pluginInterface;
            CommandManager = commandManager;
            ClientState = clientState;
            TargetManager = targetManager;
            TextureProvider = textureProvider;
            Condition = condition;
            ContextMenu = contextMenu;
            Framework = framework;

        //unhandeled exception handeling - probably not really needed anymore.
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += UnobservedTaskExceptionHandler;

            //assing our Configuration var
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            //need this to interact with the plugin from the datareceiver.
            DataReceiver.plugin = this;

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "opens the plugin window."
            }); 
            //WIP
            
            //init our windows
            OptionsWindow = new OptionsWindow(this);
            MainPanel = new MainPanel(this);
            TermsWindow = new TOS(this);
            ProfileWindow = new ProfileWindow(this);
            ImagePreview = new ImagePreview(this);
            BookmarksWindow = new BookmarksWindow(this);
            TargetWindow = new TargetWindow(this);
            VerificationWindow = new VerificationWindow(this);
            RestorationWindow = new RestorationWindow(this);
            ReportWindow = new ReportWindow(this);
            ConnectionsWindow = new ConnectionsWindow(this);

            Configuration.Initialize(PluginInterface);

            //add the windows to the windowsystem
            WindowSystem.AddWindow(OptionsWindow);
            WindowSystem.AddWindow(MainPanel);
            WindowSystem.AddWindow(TermsWindow);
            WindowSystem.AddWindow(ProfileWindow);
            WindowSystem.AddWindow(ImagePreview);
            WindowSystem.AddWindow(BookmarksWindow);
            WindowSystem.AddWindow(TargetWindow);
            WindowSystem.AddWindow(VerificationWindow);
            WindowSystem.AddWindow(RestorationWindow);
            WindowSystem.AddWindow(ReportWindow);
            WindowSystem.AddWindow(ConnectionsWindow);
            //don't know why this is needed but it is (I legit passed it to the window above.)
            ConnectionsWindow.plugin = this;

            // Subscribe to condition change events
            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
            PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
            ContextMenu.OnMenuOpened += AddContextMenu;

            ClientState.Logout += Logout;
            MainPanel.plugin = this;
            Framework.Update += OnUpdate;
        }
       
        public async void LoadConnection()
        {
            Connect();
            //update the statusBarEntry with out connection status
            UpdateStatus();
            //self explanitory
            ToggleMainUI();
            //check for existing connection requests
            CheckConnectionsRequestStatus();
        }
        public async void Connect()
        {
            LoadStatusBarEntry(); // Ensure the DTR bar is loaded before connecting

            if (IsOnline())
            {
                plugin.logger?.Error("Attempting WebSocket connection");

                bool isConnected = ClientHTTP.GetWebSocketState().Item1;
                if (isConnected)
                {
                    plugin.logger?.Error("WebSocket is already connected.");
                }
                else
                {
                    plugin.logger?.Error("WebSocket not connected. Attempting to connect...");

                    bool connectionResult = await ClientHTTP.ConnectWebSocketAsync();
                    if (connectionResult)
                    {
                        plugin.logger?.Error("WebSocket connection successfully established.");
                    }
                    else
                    {
                        plugin.logger?.Error("Failed to connect to WebSocket server.");
                    }
                }
            }
        }


        private void Logout()
        {
            //set our bool back to false to let update re instantiate our login attempt and dtrbar entries
            ControlsLogin = false;
            //remove our bar entries
            connectionsBarEntry = null;
            statusBarEntry = null;
            //set status text
            MainPanel.status = "Logged Out";
            MainPanel.statusColor = new Vector4(255, 0, 0, 255);
            //remove the current windows and switch back to login window.
            MainPanel.switchUI();
            MainPanel.login = true;
        }
        private void UnobservedTaskExceptionHandler(object sender, UnobservedTaskExceptionEventArgs e)
        {
            // Mark the exception as observed to prevent it from being thrown by the finalizer thread
            e.SetObserved();
            Framework.RunOnFrameworkThread(() =>
            {
               logger.Error("Exception handled" + e.Exception.Message);
            });
        }
        public void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Handle the unhandled exception here
            var exception = e.ExceptionObject as Exception;
            Framework.RunOnFrameworkThread(() =>
            {
                logger.Error("Exception handled" + exception.Message);
            });
        }
        public void AddContextMenu(IMenuOpenedArgs args)
        {
            if (IsOnline())
            {
                var targetPlayer = TargetManager.Target as IPlayerCharacter;
                if (args.AddonPtr == (nint)0 && targetPlayer != null && loggedIn == true)
                {
                    //if we are right clicking a player and are logged into hte plugin, add our contextMenu items.
                    MenuItem view = new MenuItem();
                    MenuItem bookmark = new MenuItem();
                    view.Name = "View Absolute Profile";
                    view.PrefixColor = 56;
                    view.Prefix = SeIconChar.BoxedQuestionMark;
                    bookmark.Name = "Bookmark Absolute Profile";
                    bookmark.PrefixColor = 56;
                    bookmark.Prefix = SeIconChar.BoxedPlus;
                    //assign on click actions
                    view.OnClicked += ViewProfile;
                    bookmark.OnClicked += BookmarkProfile;
                    //add the menu item
                    args.AddMenuItem(view);
                    args.AddMenuItem(bookmark);

                }
            }

        }

        private async void ViewProfile(IMenuItemClickedArgs args)
        {
            try
            {

                if (IsOnline()) //may not even need this, but whatever
                {
                    //get our current target player
                    var targetPlayer = TargetManager.Target as IPlayerCharacter;
                    //fetch the player name and home world name
                    string characterName = targetPlayer.Name.ToString();
                    string characterWorld = targetPlayer.HomeWorld.GameData.Name.ToString();
                    //set values for windows that need the name and home world aswell
                    ReportWindow.reportCharacterName = characterName;
                    ReportWindow.reportCharacterWorld = characterWorld;
                    TargetWindow.characterNameVal = characterName;
                    TargetWindow.characterWorldVal = characterWorld;
                    //reload our target window so we don't get the wrong info then open it
                    TargetWindow.ReloadTarget();
                    OpenTargetWindow();
                    //send a request to the server for the target profile info
                    DataSender.SendRequestTargetProfileAsync(characterName, characterWorld, plugin.username);
                }

            }
            catch (Exception ex)
            {
                logger.Error("Error when viewing profile from context " + ex.ToString());
            }
        }
        private async void BookmarkProfile(IMenuItemClickedArgs args)
        {
            if (IsOnline()) //once again may not need this
            {
                //fetch target player once more
                var targetPlayer = TargetManager.Target as IPlayerCharacter;
                //send a bookmark message to the server
                DataSender.SendPlayerBookmarkAsync(plugin.username.ToString(), targetPlayer.Name.ToString(), targetPlayer.HomeWorld.GameData.Name.ToString());
            }
        }

        //server connection status dtrBarEntry
        public void LoadStatusBarEntry()
        {
            // Ensure the status bar entry is only created once
            if (statusBarEntry == null)
            {
                var entry = dtrBar.Get("AbsoluteRoleplay");  // Get or create the status bar entry
                statusBarEntry = entry;  // Assign it to the global statusBarEntry variable

                string icon = "\uE03E";  // Example icon (Unicode for dice)
                statusBarEntry.Text = icon;  // Set the text to the icon
                statusBarEntry.Tooltip = "Absolute Roleplay";  // Set a tooltip
                statusBarEntry.OnClick = () => ToggleMainUI();  // Handle click events

                plugin.logger?.Error("DTR bar entry initialized.");
            }
            else
            {
                plugin.logger?.Error("statusBarEntry is already initialized.");
            }
        }

        //WIP
        /* public void LoadChatBarEntry()
         {

             var entry = dtrBar.Get("AbsoluteChat");
             chatBarEntry = entry;
             string icon = "\uE0BB"; //link icon
             chatBarEntry.Text = icon; //set text to icon
             //set base tooltip value
             chatBarEntry.Tooltip = "Absolute Roleplay - Chat Messages";
             //assign on click to toggle the main ui
         }
         */
        //used to alert people of incoming connection requests
        public async void LoadConnectionsBarEntry(float deltaTime)
        {
            timer += deltaTime;
            float pulse = ((int)(timer / BlinkInterval) % 2 == 0) ? 14 : 0; // Alternate between 0 and 14 (red) every BlinkInterval

            var entry = dtrBar.Get("AbsoluteConnection");
            connectionsBarEntry = entry;
            connectionsBarEntry.Tooltip = "Absolute Roleplay - New Connections Request";
            ConnectionsWindow.currentListing = 2; entry.OnClick = async () =>
            {
                // Await the async method here
                DataSender.SendConnectionsRequestAsync(
                    plugin.username.ToString(),
                    ClientState.LocalPlayer.Name.ToString(),
                    ClientState.LocalPlayer.HomeWorld.GameData.Name.ToString()
                );
            };

            SeStringBuilder statusString = new SeStringBuilder();
            statusString.AddUiGlow((ushort)pulse); // Apply pulsing glow
            statusString.AddText("\uE070"); //Boxed question mark (Mario brick)
            statusString.AddUiGlow(0);
            SeString str = statusString.BuiltString;
            connectionsBarEntry.Text = str;
        }

        //used for when we need to remove the connection request status
        public void UnloadConnectionsBar()
        {
            if(connectionsBarEntry != null)
            {
                connectionsBarEntry?.Remove();                
            }
        }
        public void Dispose()
        {
          
            WindowSystem?.RemoveAllWindows();
            statusBarEntry?.Remove();
            statusBarEntry = null;
            connectionsBarEntry?.Remove();
            connectionsBarEntry = null;
            CommandManager.RemoveHandler(CommandName);
            ContextMenu.OnMenuOpened -= AddContextMenu;
            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUI;
            PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUI;
            ClientState.Logout -= Logout;
            Framework.Update -= OnUpdate;
            // Dispose all windows
            OptionsWindow?.Dispose();
            MainPanel?.Dispose();
            TermsWindow?.Dispose();
            ImagePreview?.Dispose();
            ProfileWindow?.Dispose();
            TargetWindow?.Dispose();
            BookmarksWindow?.Dispose();
            VerificationWindow?.Dispose();
            RestorationWindow?.Dispose();
            ReportWindow?.Dispose();
            ConnectionsWindow?.Dispose();
            Misc.Jupiter?.Dispose();
            Imaging.RemoveAllImages(this); //delete all images downloaded by the plugin namely the gallery
            try
            {
                // If you want to disconnect in Dispose, avoid using 'async' here
                ClientHTTP.DisconnectWebSocketAsync().GetAwaiter().GetResult();  // Forcefully await the task
            }
            catch (Exception ex)
            {
                // Handle exceptions
                plugin.logger.Error($"Error in Dispose: {ex.Message}");
            }
        }
        public void CheckConnectionsRequestStatus()
        {
            TimeSpan deltaTimeSpan = Framework.UpdateDelta;
            float deltaTime = (float)deltaTimeSpan.TotalSeconds; // Convert deltaTime to seconds
            // If we receive a connection request
            if (newConnection == true)
            {
                LoadConnectionsBarEntry(deltaTime);
            }
            else
            {
                UnloadConnectionsBar();
            }
        }



        private void OnUpdate(IFramework framework)
        {
            bool connected = ClientHTTP.GetWebSocketState().Item1;
            if (IsOnline() == true && connected == false && ConnectionLoaded == false)
            {
                LoadConnection();
                ConnectionLoaded = true;
            }
            /* if(loggedIn == true && chatLoaded == false)
             {
                // LoadChatBarEntry();
                 chatLoaded = true;
             }*/
            if (IsOnline() == true && connected == true && ControlsLogin == false)
            {
                // Auto login when first opening the plugin or logging in
                MainPanel.AttemptLogin();
                ControlsLogin = true;
            }
        }
        private void OnCommand(string command, string args)
        {
            // in response to the slash command, just toggle the display status of our main ui
            ToggleMainUI();
        }
        
        public void CloseAllWindows()
        {
            foreach (Window window in WindowSystem.Windows)
            {
                if (window.IsOpen)
                {
                    window.Toggle();
                }
            }
        }
        public bool IsOnline()
        {
            bool loggedIn = false;
            //if player is online in game and player is present
            if (ClientState.IsLoggedIn == true && ClientState.LocalPlayer != null)
            {
                loggedIn = true;
            }
            return loggedIn; //return our logged in status
        }
    
        private void DrawUI() => WindowSystem.Draw();
        public void ToggleConfigUI() => OptionsWindow.Toggle();
        public void ToggleMainUI() => MainPanel.Toggle();
        public void OpenMainPanel() => MainPanel.IsOpen = true;
        public void OpenTermsWindow() => TermsWindow.IsOpen = true;
        public void OpenImagePreview() => ImagePreview.IsOpen = true;
        public void OpenProfileWindow() => ProfileWindow.IsOpen = true;
        public void CloseProfileWindow() => ProfileWindow.IsOpen = false;
        public void OpenTargetWindow() => TargetWindow.IsOpen = true;
        public void OpenBookmarksWindow() => BookmarksWindow.IsOpen = true;
        public void OpenVerificationWindow() => VerificationWindow.IsOpen = true;
        public void OpenRestorationWindow() => RestorationWindow.IsOpen = true;
        public void OpenReportWindow() => ReportWindow.IsOpen = true;
        public void OpenOptionsWindow() => OptionsWindow.IsOpen = true;
        public void OpenConnectionsWindow() => ConnectionsWindow.IsOpen = true;

        internal async void UpdateStatus()
        {
            try
            {
                // Check WebSocket state
                var (isConnected, connectionStatus) = ClientHTTP.GetWebSocketState();

                if (ClientState == null)
                {
                    plugin.logger?.Error("ClientState is null.");
                    return;
                }

                if (ClientState.LocalPlayer == null)
                {
                    plugin.logger?.Error("LocalPlayer is null.");
                    return;
                }

                // Update the UI with connection status
                MainPanel.serverStatus = connectionStatus;

                if (statusBarEntry != null)
                {
                    statusBarEntry.Tooltip = new SeStringBuilder().AddText($"Absolute Roleplay: {connectionStatus}").Build();
                    plugin.logger?.Error($"DTR bar entry updated: {connectionStatus}");
                }
                else
                {
                    plugin.logger?.Error("statusBarEntry is null.");
                }
            }
            catch (Exception ex)
            {
                plugin.logger?.Error($"Error updating status: {ex}");
            }
        }


    }

}
