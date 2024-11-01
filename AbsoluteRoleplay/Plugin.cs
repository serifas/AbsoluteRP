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
using AbsoluteRoleplay.Windows.Profiles;
using Dalamud.Game.ClientState.Objects.Types;
using System.Diagnostics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Conditions;
using ImGuiNET;
//using AbsoluteRoleplay.Windows.Chat;
namespace AbsoluteRoleplay
{
    public partial class Plugin : IDalamudPlugin
    {
        public static IGameObject? LastMouseOverTarget;
        public float tooltipAlpha;
        public static Plugin plugin;
        public string username = "";
        public string password = "";
        public string playername = "";
        public string playerworld = "";
        private const string CommandName = "/arp";
        public static ImGuiViewportPtr viewport = ImGui.GetMainViewport();

        public float screenWidth = viewport.WorkSize.X;
        public float screenHeight = viewport.WorkSize.Y;
        //WIP
        private const string ChatToggleCommand = "/arpchat";

        public bool loginAttempted = false;
        private IDtrBar dtrBar;
        private IDtrBarEntry? statusBarEntry;
        private IDtrBarEntry? connectionsBarEntry;
        private IDtrBarEntry? chatBarEntry;
        public static bool BarAdded = false;
        internal static float timer = 0f;

        [PluginService] internal static IDataManager DataManager { get; private set; } = null;
        [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null;
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
        private Stopwatch _fadeTimer;
        private Stopwatch _uiSpeedTimer;
        private double _uiSpeed;
        public static bool tooltipLoaded = false;
        public static Plugin? Ui { get; private set; }
        private readonly WindowSystem WindowSystem = new("Absolute Roleplay");
        //Windows
        private OptionsWindow OptionsWindow { get; init; }
        private NotesWindow NotesWindow { get; init; }
        private VerificationWindow VerificationWindow { get; init; }
        public AlertWindow AlertWindow { get; init; }
        private RestorationWindow RestorationWindow { get; init; }
        public ARPTooltipWindow TooltipWindow { get; init; }
        private ReportWindow ReportWindow { get; init; }
        private MainPanel MainPanel { get; init; }
        private ProfileWindow ProfileWindow { get; init; }
        private BookmarksWindow BookmarksWindow { get; init; }
        private TargetWindow TargetWindow { get; init; }
        private ImagePreview ImagePreview { get; init; }
        private TOS TermsWindow { get; init; }
        private ConnectionsWindow ConnectionsWindow { get; init; }

        //logger for printing errors and such
        public Logger logger = new Logger();


        public float BlinkInterval = 0.5f;
        public bool newConnection;
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
            IDataManager dataManager,
            IObjectTable objectTable,
            IDtrBar dtrBar
            )
        {
            // Wrap the original service
            this.dtrBar = dtrBar;
            DataManager = dataManager;
            ObjectTable = objectTable;
            PluginInterface = pluginInterface;
            CommandManager = commandManager;
            ClientState = clientState;
            TargetManager = targetManager;
            TextureProvider = textureProvider;
            Condition = condition;
            ContextMenu = contextMenu;
            Framework = framework;
            ClientTCP.plugin = this;
            //unhandeled exception handeling - probably not really needed anymore.
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += UnobservedTaskExceptionHandler;

            //assing our Configuration var
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            //need this to interact with the plugin from the datareceiver.
            DataReceiver.plugin = this;
            DataSender.plugin = this;
            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "opens the plugin window."
            });
            //WIP

            _fadeTimer = new Stopwatch();
            _uiSpeedTimer = Stopwatch.StartNew();

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
            TooltipWindow = new ARPTooltipWindow(this);
            NotesWindow = new NotesWindow(this);
            AlertWindow = new AlertWindow(this);
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
            WindowSystem.AddWindow(TooltipWindow);
            WindowSystem.AddWindow(NotesWindow);
            WindowSystem.AddWindow(AlertWindow);

            //don't know why this is needed but it is (I legit passed it to the window above.)
            ConnectionsWindow.plugin = this;

            // Subscribe to condition change events
            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
            PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
            if (IsOnline())
            {
                OnLogin();
            }
            ContextMenu!.OnMenuOpened += this.OnMenuOpened;
            ClientState.Logout += OnLogout;
            ClientState.Login += OnLogin;
            Framework.Update += Update;
            MainPanel.pluginInstance = this;
            plugin = this;
        }
        public void DrawTooltipInfo(IGameObject? mouseOverTarget)
        {
            if (Configuration.tooltip_Enabled && !InCombatLock())
            {
                if (mouseOverTarget.ObjectKind == ObjectKind.Player)
                {
                    IPlayerCharacter playerTarget = (IPlayerCharacter)mouseOverTarget;
                    if (tooltipLoaded == false)
                    {
                        tooltipLoaded = true;
                        DataSender.SendRequestPlayerTooltip(playerTarget.Name.TextValue.ToString(), playerTarget.HomeWorld.GameData.Name.ToString());
                    }
                }
            }

        }
        public void OpenAndLoadProfileWindow()
        {

            ProfileWindow.TabOpen[TabValue.Bio] = true;
            ProfileWindow.TabOpen[TabValue.Hooks] = true;
            ProfileWindow.TabOpen[TabValue.Story] = true;
            ProfileWindow.TabOpen[TabValue.OOC] = true;
            ProfileWindow.TabOpen[TabValue.Gallery] = true;
            ProfileWindow.ResetOnChangeOrRemoval();
            OpenProfileWindow();
            DataSender.FetchProfiles();
            DataSender.FetchProfile(ProfileWindow.currentProfile);
        }

        private unsafe void OnMenuOpened(IMenuOpenedArgs args)
        {
            var ctx = AgentContext.Instance();
            if (args.AgentPtr != (nint)ctx)
            {
                return;
            }

            if (ctx->TargetObjectId.ObjectId != 0xE000_0000)
            {
                this.ObjectContext(args, ctx->TargetObjectId.ObjectId);
                return;
            }

            var world = ctx->TargetHomeWorldId;
            if (world == 0)
            {
                return;
            }

            var name = SeString.Parse(ctx->TargetName.AsSpan()).TextValue;
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            var worldname = DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.World>()?.GetRow((uint)world)?.Name.ToString();
            args.AddMenuItem(new MenuItem
            {
                Name = "Bookmark Absolute RP Profile",
                PrefixColor = 56,
                Prefix = SeIconChar.BoxedPlus,
                OnClicked = _ => {
                    DataSender.BookmarkPlayer(name, worldname);

                },
            });
            args.AddMenuItem(new MenuItem
            {
                Name = "View Absolute RP Profile",
                PrefixColor = 56,
                Prefix = SeIconChar.BoxedQuestionMark,
                OnClicked = _ => {

                    DataSender.RequestTargetProfileByCharacter(name, worldname);
                },
            });
        }

        private void ObjectContext(IMenuOpenedArgs args, uint objectId)
        {
            var obj = ObjectTable.SearchById(objectId);
            if (obj is not IPlayerCharacter chara)
            {
                return;
            }

            args.AddMenuItem(new MenuItem
            {
                Name = "Bookmark Absolute RP Profile",
                PrefixColor = 56,
                Prefix = SeIconChar.BoxedPlus,
                OnClicked = _ => {
                    DataSender.BookmarkPlayer(chara.Name.ToString(), chara.HomeWorld.GameData.Name.ToString());
                },
            });
            args.AddMenuItem(new MenuItem
            {
                Name = "View Absolute RP Profile",
                PrefixColor = 56,
                Prefix = SeIconChar.BoxedQuestionMark,
                OnClicked = _ => {

                    DataSender.RequestTargetProfileByCharacter(chara.Name.ToString(), chara.HomeWorld.GameData.Name.ToString());
                },
            });
        }


        public async void LoadConnection()
        {
            ClientHandleData.InitializePackets();
            Connect();
            //update the statusBarEntry with out connection status
            UpdateStatus();
            //check for existing connection requests
            CheckConnectionsRequestStatus();
        }
        public async void Connect()
        {
            LoadStatusBarEntry();
            if (IsOnline())
            {
                if (!ClientTCP.IsConnected())
                {
                    ClientTCP.AttemptConnect();
                }
            }
        }

        private void OnLogout()
        {
            //remove our bar entries
            connectionsBarEntry = null;
            statusBarEntry = null;
            //set status text
            MainPanel.status = "Logged Out";
            MainPanel.statusColor = new Vector4(255, 0, 0, 255);
            //remove the current windows and switch back to login window.
            MainPanel.switchUI();
            MainPanel.login = MainPanel.CurrentElement();
            loginAttempted = false;
            playername = string.Empty;
            playerworld = string.Empty;
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

        /// <summary>
        /// 
        /// 




        /// </summary>
        /// <param name="args"></param>


        private void BookmarkProfile(IMenuItemClickedArgs args)
        {
            if (IsOnline()) //once again may not need this
            {
                //fetch target player once more
                var targetPlayer = TargetManager.Target as IPlayerCharacter;
                //send a bookmark message to the server
                DataSender.BookmarkPlayer(targetPlayer.Name.ToString(), targetPlayer.HomeWorld.GameData.Name.ToString());
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
        public void LoadConnectionsBarEntry(float deltaTime)
        {
            timer += deltaTime;
            float pulse = ((int)(timer / BlinkInterval) % 2 == 0) ? 14 : 0; // Alternate between 0 and 14 (red) every BlinkInterval

            var entry = dtrBar.Get("AbsoluteConnection");
            connectionsBarEntry = entry;
            connectionsBarEntry.Tooltip = "Absolute Roleplay - New Connections Request";
            ConnectionsWindow.currentListing = 2;
            entry.OnClick = () => DataSender.RequestConnections(username, password);
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
            if (connectionsBarEntry != null)
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
            ContextMenu.OnMenuOpened -= OnMenuOpened;
            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUI;
            PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUI;
            ClientState.Logout -= OnLogout;
            ClientState.Login -= OnLogin;
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
            TooltipWindow?.Dispose();
            ReportWindow?.Dispose();
            ConnectionsWindow?.Dispose();
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
        public void OnLogin()
        {
            playername = ClientState.LocalPlayer.Name.ToString();
            playerworld = ClientState.LocalPlayer.HomeWorld.GameData.Name.ToString();
            if (IsOnline() == true)
            {
                LoadConnection();
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
        public void OpenARPTooltip() => TooltipWindow.IsOpen = true;
        public void CloseARPTooltip() => TooltipWindow.IsOpen = false;
        public void OpenProfileNotes() => NotesWindow.IsOpen = true;
        public void OpenAlertWindow()
        {

            AlertWindow.increment = true;
            AlertWindow.length = 0;
            AlertWindow.IsOpen = true;
        }
        public void CloseAlertWIndow() => AlertWindow.IsOpen = false;

        internal async void UpdateStatus()
        {
            try
            {

                Vector4 connectionStatusColor = ClientTCP.GetConnectionStatusAsync(ClientTCP.clientSocket).Result.Item1;
                string connectionStatus = ClientTCP.GetConnectionStatusAsync(ClientTCP.clientSocket).Result.Item2;
                MainPanel.serverStatus = connectionStatus;
                MainPanel.serverStatusColor = connectionStatusColor;
                if (ClientState.IsLoggedIn && ClientState.LocalPlayer != null)
                {
                    //set dtr bar entry for connection status to our current server connection status
                    statusBarEntry.Tooltip = new SeStringBuilder().AddText($"Absolute Roleplay").Build();
                }
               
            }
            catch (Exception ex)
            {
                logger.Error("Error updating status: " + ex.ToString());
            }
        }
        public void Update(IFramework framework)
        {
            if (!loginAttempted && MainPanel.serverStatus == "Connected")
            {
                username = Configuration.username;
                password = Configuration.password;
                playername = ClientState.LocalPlayer.Name.ToString();
                playerworld = ClientState.LocalPlayer.HomeWorld.GameData.Name.ToString();
                DataSender.Login(username, password, playername, playerworld);
                loginAttempted = true;
            }
            
            if (TargetManager.MouseOverTarget != null)
            {
                DrawTooltipInfo(TargetManager.MouseOverTarget);
            }
            if(TargetManager.Target != null)
            {
                DrawTooltipInfo(TargetManager.Target);
            }
            if(TargetManager.Target == null && TargetManager.MouseOverTarget == null)
            {
                TooltipWindow.IsOpen = false;
                tooltipLoaded = false;
            }
            if(TargetManager.Target != TargetManager.PreviousTarget && TargetManager.Target != null)
            {
                DrawTooltipInfo(TargetManager.Target);
            }
        }
        public Vector2 CalculateTooltipPos()
        {
            float positionX = plugin.Configuration.hPos;
            float positionY = plugin.Configuration.vPos;
            bool correctedPos = false;
            if (positionX > plugin.screenWidth - ImGui.GetWindowSize().X)
            {
                positionX = plugin.screenWidth - ImGui.GetWindowSize().X;
            }
            if (positionY > plugin.screenHeight - ImGui.GetWindowSize().Y)
            {
                positionY = plugin.screenHeight - ImGui.GetWindowSize().Y;
            }
            return new Vector2(positionX, positionY);
        }
        public bool InCombatLock()
        {

            if (ClientState.LocalPlayer.StatusFlags.HasFlag(StatusFlags.InCombat)  && Configuration.tooltip_HideInCombat == true)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
   
}
