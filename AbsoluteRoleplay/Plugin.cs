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
using AbsoluteRoleplay.Windows.Chat;
namespace AbsoluteRoleplay
{
    public partial class Plugin : IDalamudPlugin
    {
        public static Plugin plugin;
        public string username;
        private const string CommandName = "/arp";
        //WIP
        //private const string ChatToggleCommand = "/arpchat";
      
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
        //Windows
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
       // private ChatWindow ChatWindow { get; init; }

        //logger for printing errors and such
        public Logger logger = new Logger();


        public float BlinkInterval = 0.5f;
        public bool newConnection;
        public bool ControlsLogin = false;
        private bool shouldCheckTarget = true;
        private bool isWindowOpen;


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
                HelpMessage = "opens the plugin window (or use the dice button by your map)."
            }); 
            //WIP
            /*
            CommandManager.AddHandler(ChatToggleCommand, new CommandInfo(OnChatCommand)
            {
                HelpMessage = "opens the chat window."
            });*/
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
            //ChatWindow = new ChatWindow(this);

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
           // WindowSystem.AddWindow(ChatWindow);

            //don't know why this is needed but it is (I legit passed it to the window above.)
            ConnectionsWindow.plugin = this;

            // Subscribe to condition change events
            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
            PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
            ContextMenu.OnMenuOpened += AddContextMenu;

            ClientState.Logout += Logout;
            ClientState.Login += LoadConnection;
            MainPanel.plugin = this;
            Framework.Update += OnUpdate;
            //if online and present in game
            if (ClientState.IsLoggedIn && clientState.LocalPlayer != null)
            {
                //load our connection
                LoadConnection();
            }
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
            LoadStatusBarEntry();
            //LoadChatBarEntry();
            if (IsOnline())
            {
                if (!ClientTCP.IsConnected())
                {
                    ClientTCP.AttemptConnect();
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

        private void ViewProfile(IMenuItemClickedArgs args)
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
                    DataSender.RequestTargetProfile(characterName, characterWorld, Configuration.username);
                }

            }
            catch (Exception ex)
            {
                logger.Error("Error when viewing profile from context " + ex.ToString());
            }
        }
        private void BookmarkProfile(IMenuItemClickedArgs args)
        {
            if (IsOnline()) //once again may not need this
            {
                //fetch target player once more
                var targetPlayer = TargetManager.Target as IPlayerCharacter;
                //send a bookmark message to the server
                DataSender.BookmarkPlayer(Configuration.username.ToString(), targetPlayer.Name.ToString(), targetPlayer.HomeWorld.GameData.Name.ToString());
            }
        }

        //server connection status dtrBarEntry
        public void LoadStatusBarEntry()
        {
            var entry = dtrBar.Get("AbsoluteRoleplay");
            statusBarEntry = entry;
            string icon = "\uE03E"; //dice icon
            statusBarEntry.Text = icon; //set text to icon
            //set base tooltip value
            statusBarEntry.Tooltip = "Absolute Roleplay";                
            //assign on click to toggle the main ui
            entry.OnClick = () => ToggleMainUI();
        }
        /*
        public void LoadChatBarEntry()
        {
            var entry = dtrBar.Get("AbsoluteChat");
            chatBarEntry = entry;
            string icon = "\uE0BB"; //link icon
            chatBarEntry.Text = icon; //set text to icon
            //set base tooltip value
            chatBarEntry.Tooltip = "Absolute Roleplay - Chat Messages";
            //assign on click to toggle the main ui
            entry.OnClick = () => ToggleChatUI();
        }
        */
        //used to alert people of incoming connection requests
        public void LoadConnectionsBarEntry(float deltaTime)
        {
            timer += deltaTime;
            float pulse = ((int)(timer / BlinkInterval) % 2 == 0) ? 14 : 0; // Alternate between 0 and 14 (red) every BlinkInterval

            var entry = dtrBar.Get("AbsoluteConnection");
            connectionsBarEntry = entry;
            connectionsBarEntry.Tooltip = "Absolute Roleplay - New Connections Request";
            ConnectionsWindow.currentListing = 2;
            entry.OnClick = () => DataSender.RequestConnections(Configuration.username.ToString(), ClientState.LocalPlayer.Name.ToString(), ClientState.LocalPlayer.HomeWorld.GameData.Name.ToString());                 
            
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
            ClientState.Login -= LoadConnection;
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
            //ChatWindow?.Dispose();
            Misc.Jupiter?.Dispose();
            Imaging.RemoveAllImages(this); //delete all images downloaded by the plugin namely the gallery
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
            if (IsOnline() == true && ClientTCP.IsConnected() == true && ControlsLogin == false)
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
        /*
        private void OnChatCommand(string command, string arguments)
        {
            ToggleChatUI();
        }*/
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
       // public void ToggleChatUI() => ChatWindow.Toggle();

        internal async void UpdateStatus()
        {
            try
            {

                string connectionStatus = await ClientTCP.GetConnectionStatusAsync(ClientTCP.clientSocket);
                MainPanel.serverStatus = connectionStatus;
                if (ClientState.IsLoggedIn && ClientState.LocalPlayer != null)
                {
                    //set dtr bar entry for connection status to our current server connection status
                    statusBarEntry.Tooltip = new SeStringBuilder().AddText($"Absolute Roleplay: {connectionStatus}").Build();
                }

            }
            catch (Exception ex)
            {
                logger.Error("Error updating status: " + ex.ToString());
            }
        }
    }
   
}
