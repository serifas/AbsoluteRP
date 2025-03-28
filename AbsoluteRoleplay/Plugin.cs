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
using FFXIVClientStructs.FFXIV.Client.UI;
using static Lumina.Data.Parsing.Layer.LayerCommon;
using Dalamud.Interface.Utility;
using static FFXIVClientStructs.FFXIV.Client.UI.UIModule.Delegates;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System.Timers;
using Lumina.Excel.Sheets;
using AbsoluteRoleplay.Windows.Listings;
using AbsoluteRoleplay.Windows.Ect;
using AbsoluteRoleplay.Windows.Account;
using AbsoluteRoleplay.Windows.MainPanel;
using AbsoluteRoleplay.Windows.Profiles.ProfileTabs;
using AbsoluteRoleplay.Windows.Inventory;
using OtterGui.Filesystem;
using AbsoluteRoleplay.Windows.MainPanel.Views;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using AbsoluteRoleplay.Windows.Moderator;
//using AbsoluteRoleplay.Windows.Chat;
namespace AbsoluteRoleplay
{
    public partial class Plugin : IDalamudPlugin
    {
        public static IGameObject? LastMouseOverTarget;
        public float tooltipAlpha;
        public static Plugin plugin;
        public string username = string.Empty;
        public string password = string.Empty;
        public string playername = string.Empty;
        public string playerworld = string.Empty;
        private const string CommandName = "/arp";
        public static ImGuiViewportPtr viewport = ImGui.GetMainViewport();

        private IntPtr lastTargetAddress = IntPtr.Zero;
        public static bool lockedtarget = false;
        private bool openItemTooltip;
        public bool loggedIn;
        public static bool justRegistered;

        public float screenWidth = viewport.WorkSize.X;
        public float screenHeight = viewport.WorkSize.Y;
        //WIP
        private const string ChatToggleCommand = "/arpchat";
        public IPlayerCharacter playerCharacter;
        public bool loginAttempted = false;
        private IDtrBar dtrBar;
        private IDtrBarEntry? statusBarEntry;
        private IDtrBarEntry? connectionsBarEntry;
        private IDtrBarEntry? chatBarEntry;
        public static bool BarAdded = false;
        internal static float timer = 0f;
        public static UiBuilder builder;
        public static IGameGui GameGUI;
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
        [PluginService] internal static IChatGui chatgui { get; private set; } = null;

        [LibraryImport("user32")]
        internal static partial short GetKeyState(int nVirtKey);
        //used for making sure click happy people don't mess up their hard work
        public static bool CtrlPressed() => (GetKeyState(0xA2) & 0x8000) != 0 || (GetKeyState(0xA3) & 0x8000) != 0;
        public Configuration Configuration { get; init; }
        public static bool tooltipLoaded = false;
        public static Plugin? Ui { get; private set; }
        private readonly WindowSystem WindowSystem = new("Absolute Roleplay");
        //Windows
        public OptionsWindow OptionsWindow { get; init; }
        public NotesWindow NotesWindow { get; init; }
        private VerificationWindow VerificationWindow { get; init; }
        private RestorationWindow RestorationWindow { get; init; }
        private ModPanel ModeratorPanel { get; init; }
        private ListingsWindow ListingWindow { get; init; }
        public ARPTooltipWindow TooltipWindow { get; init; }
        private ReportWindow ReportWindow { get; init; }
        private MainPanel MainPanel { get; init; }
        private ImportantNotice ImportantNoticeWindow { get; init; }
        private ProfileWindow ProfileWindow { get; init; }
        private BookmarksWindow BookmarksWindow { get; init; }
        private ARPChatWindow ArpChatWindow { get; init; }
        private TargetWindow TargetWindow { get; init; }
        private ImagePreview ImagePreview { get; init; }
        public ItemTooltip ItemTooltip { get; init; }
        public InventoryWindow InventoryWindow { get; init; }
        private TOS TermsWindow { get; init; }
        private ConnectionsWindow ConnectionsWindow { get; init; }

        //logger for printing errors and such
        public Logger logger = new Logger();

        public float BlinkInterval = 0.5f;
        public bool newConnection;
        private bool shouldCheckTarget = true;
        private bool isWindowOpen;
        public static bool tooltipShown;

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
            IDtrBar dtrBar,
            IGameGui gameGui,
            IChatGui ChatGui
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
            GameGUI = gameGui;
            chatgui = ChatGui;
            ClientTCP.plugin = this;
            WindowOperations.plugin = this;
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
            //init our windows
            OptionsWindow = new OptionsWindow(this);
            MainPanel = new MainPanel(this);
            TermsWindow = new TOS(this);
            ProfileWindow = new ProfileWindow(this);
            ImagePreview = new ImagePreview(this);
            BookmarksWindow = new BookmarksWindow(this);
            ImportantNoticeWindow = new ImportantNotice(this);
            TargetWindow = new TargetWindow(this);
            VerificationWindow = new VerificationWindow(this);
            ModeratorPanel = new ModPanel(this);
            ArpChatWindow = new ARPChatWindow(this, chatgui);
            RestorationWindow = new RestorationWindow(this);
            ReportWindow = new ReportWindow(this);
            ConnectionsWindow = new ConnectionsWindow(this);
            TooltipWindow = new ARPTooltipWindow(this);
            NotesWindow = new NotesWindow(this);
            InventoryWindow = new InventoryWindow(this);
            ListingWindow = new ListingsWindow(this);
            ItemTooltip = new ItemTooltip(this);
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
            WindowSystem.AddWindow(ModeratorPanel);
            WindowSystem.AddWindow(RestorationWindow);
            WindowSystem.AddWindow(ReportWindow);
            WindowSystem.AddWindow(ConnectionsWindow);
            WindowSystem.AddWindow(TooltipWindow);
            WindowSystem.AddWindow(NotesWindow);
            WindowSystem.AddWindow(ListingWindow);
            WindowSystem.AddWindow(ItemTooltip);
            WindowSystem.AddWindow(InventoryWindow);
            WindowSystem.AddWindow(ArpChatWindow);
            WindowSystem.AddWindow(ImportantNoticeWindow);
            //don't know why this is needed but it is (I legit passed it to the window above.)
            ConnectionsWindow.plugin = this;

            // Subscribe to condition change events
            PluginInterface.UiBuilder.Draw += DrawUI;
            //PluginInterface.UiBuilder.Draw += DrawHitboxes;
            PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
            PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
            ContextMenu!.OnMenuOpened += this.OnMenuOpened;
            ClientState.Logout += OnLogout;
            ClientState.Login += LoadConnection;            
            Framework.Update += Update;
            MainPanel.pluginInstance = this;
            TreeManager.plugin = this;
            plugin = this;
        }

        public async Task<Version> GetOnlineVersionAsync()
        {
            try
            {
                using HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5); // Prevents hanging requests

                string versionText = await client.GetStringAsync("https://raw.githubusercontent.com/serifas/AbsoluteRoleplay/main/Version.txt");

                if (Version.TryParse(versionText.Trim(), out Version version))
                {
                    return version;
                }
                else
                {
                    logger.Error($"Failed to parse version from response: {versionText}");
                    return new Version(0, 0, 0, 0); // Default version
                }
            }
            catch (TaskCanceledException)
            {
                logger.Error("Request timed out while fetching the online version.");
                return new Version(0, 0, 0, 0); // Prevents crashes due to timeouts
            }
            catch (HttpRequestException ex)
            {
                logger.Error($"HTTP error while fetching version: {ex.Message}");
                return new Version(0, 0, 0, 0);
            }
            catch (Exception ex)
            {
                logger.Error($"Unexpected error in GetOnlineVersionAsync: {ex}");
                return new Version(0, 0, 0, 0);
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
            DataSender.FetchProfile(ProfileWindow.currentProfileIndex);
        }

        private unsafe void OnMenuOpened(IMenuOpenedArgs args)
        {
            var ctx = AgentContext.Instance();
            if (args.AgentPtr != (nint)ctx)
            {
                return;
            }

            var obj = ObjectTable.SearchById(ctx->TargetObjectId.ObjectId);
            // Check if the object kind is Companion or Other, which are often used for minions
            // Check if the object kind is Companion, which is often used for minions and pets
            
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
                var worldname = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().GetRowOrDefault((uint)world)?.Name.ToString();


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
            /*
            args.AddMenuItem(new MenuItem
            {
                Name = "Absolute RP Trade",
                PrefixColor = 56,
                Prefix = SeIconChar.Gil,
                OnClicked = _ => {
                    
                },
            });*/

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
                    DataSender.BookmarkPlayer(chara.Name.ToString(), chara.HomeWorld.Value.Name.ToString());
                },
            });
            args.AddMenuItem(new MenuItem
            {
                Name = "View Absolute RP Profile",
                PrefixColor = 56,
                Prefix = SeIconChar.BoxedQuestionMark,
                OnClicked = _ => {

                    DataSender.RequestTargetProfileByCharacter(chara.Name.ToString(), chara.HomeWorld.Value.Name.ToString());
                },
            });
            /*
            args.AddMenuItem(new MenuItem
            {
                Name = "Absolute RP Trade",
                PrefixColor = 56,
                Prefix = SeIconChar.Gil,
                OnClicked = _ => {

                   // DataSender.RequestTargetProfileByCharacter(chara.Name.ToString(), chara.HomeWorld.Value.Name.ToString());
                },
            });*/
        }
        private Version? cachedVersion = null;
        private bool isCheckingVersion = false;

        public async Task<bool> IsToSVersionUpdated()
        {
            if (!isCheckingVersion)
            {
                isCheckingVersion = true;
                cachedVersion = await GetOnlineVersionAsync();
                isCheckingVersion = false;
            }

            return cachedVersion != null && cachedVersion == Configuration.TOSVersion;
        }
        public void LoadConnection()
        {                     
            ClientHandleData.InitializePackets();
            Connect();
            //update the statusBarEntry with out connection status
            UpdateStatus();
            //check for existing connection requests
            CheckConnectionsRequestStatus();
        }
        public void Connect()
        {
            LoadStatusBarEntry();
            
            if (!ClientTCP.IsConnected())
            {
                ClientTCP.AttemptConnect();
            }
        }
        public void DisconnectAndLogOut()
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
        private void OnLogout(int type, int code)
        {
            DisconnectAndLogOut();
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

        


        private void BookmarkProfile(IMenuItemClickedArgs args)
        {
            if (IsOnline()) //once again may not need this
            {
                //fetch target player once more
                var targetPlayer = TargetManager.Target as IPlayerCharacter;
                //send a bookmark message to the server
                DataSender.BookmarkPlayer(targetPlayer.Name.ToString(), targetPlayer.HomeWorld.Value.Name.ToString());
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
        //used to alert people of incoming connection requests
        public void LoadConnectionsBarEntry(float deltaTime)
        {
            timer += deltaTime;
            float pulse = ((int)(timer / BlinkInterval) % 2 == 0) ? 14 : 0; // Alternate between 0 and 14 (red) every BlinkInterval

            var entry = dtrBar.Get("AbsoluteConnection");
            connectionsBarEntry = entry;
            connectionsBarEntry.Tooltip = "Absolute Roleplay - New Connections Request";
            ConnectionsWindow.currentListing = 2;
            entry.OnClick = () => DataSender.RequestConnections(username, password, true);
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
            ClientState.Login -= LoadConnection;
            // Dispose all windows
            OptionsWindow?.Dispose();
            MainPanel?.Dispose();
            TermsWindow?.Dispose();
            ImagePreview?.Dispose();
            ArpChatWindow?.Dispose();
            ProfileWindow?.Dispose();
            NotesWindow?.Dispose();
            ImportantNoticeWindow?.Dispose();
            TargetWindow?.Dispose();
            BookmarksWindow?.Dispose();
            VerificationWindow?.Dispose();
            RestorationWindow?.Dispose();
            TooltipWindow?.Dispose();
            ReportWindow?.Dispose();
            ConnectionsWindow?.Dispose();
            ListingWindow?.Dispose();
            InventoryWindow?.Dispose();
            ItemTooltip?.Dispose();
            ModeratorPanel?.Dispose();
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
                playername = ClientState.LocalPlayer.Name.ToString();
                playerworld = ClientState.LocalPlayer.HomeWorld.Value.Name.ToString();
                loggedIn = true;
            }
            return loggedIn; //return our logged in status
        }


        private void DrawUI()
        {
            WindowSystem.Draw();
            //   PlayerInteraction.GetConnectionsInRange(1000, ClientState.LocalPlayer);
        }
        public async System.Threading.Tasks.Task LoadWindow(Window window, bool Toggle)
        {
            if (!ClientTCP.IsConnected())
            {
                LoadConnection();
            }
            if (window == null)
            {
                logger.Error("LoadWindow called with a null window.");
                return;
            }

            if (await IsToSVersionUpdated()) // Now runs asynchronously
            {
                logger.Debug($"Version matched, loading window: {window}");
                if (Toggle)
                {
                    window.Toggle();
                }
                else
                {
                    window.IsOpen = true;
                }
            }
            else
            {
                logger.Debug("Version mismatch, opening Terms of Service window.");

                if (Configuration.TOSVersion == null)
                {
                    Configuration.TOSVersion = new Version(0, 0, 0, 0);
                }

                TermsWindow.version = await GetOnlineVersionAsync(); //Now runs asynchronously
                TermsWindow.IsOpen = true;
            }
        }


        public void ToggleConfigUI() => OptionsWindow.IsOpen = true;
        public void ToggleMainUI() => System.Threading.Tasks.Task.Run(()=>LoadWindow(MainPanel, true));

        public void OpenMainPanel() => System.Threading.Tasks.Task.Run(() => LoadWindow(MainPanel, false));
        public void OpenTermsWindow() => TermsWindow.IsOpen = true;
        public void OpenImagePreview() => ImagePreview.IsOpen = true;
        public void OpenModeratorPanel() => ModeratorPanel.IsOpen = true;
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
        public void OpenProfileNotes() => NotesWindow.IsOpen=true;
        public void OpenListingsWindow() => ListingWindow.IsOpen = true;
        public void OpenItemTooltip() => ItemTooltip.IsOpen = true;
        public void CloseItemTooltip() => ItemTooltip.IsOpen = false;
        public void OpenInventoryWindow() => InventoryWindow.IsOpen = true;
        public void ToggleChatWindow() => ArpChatWindow.IsOpen = true;
        public void OpenImportantNoticeWindow() => ImportantNoticeWindow.IsOpen = true;


        internal void UpdateStatus()
        {
            try
            {

                Vector4 connectionStatusColor = ClientTCP.GetConnectionStatusAsync(ClientTCP.clientSocket).Result.Item1;
                string connectionStatus = ClientTCP.GetConnectionStatusAsync(ClientTCP.clientSocket).Result.Item2;
                MainPanel.serverStatus = connectionStatus;
                MainPanel.serverStatusColor = connectionStatusColor;               
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
                if (IsOnline())
                {
                    username = Configuration.username;
                    password = Configuration.password;
                    playername = ClientState.LocalPlayer.Name.ToString();
                    playerworld = ClientState.LocalPlayer.HomeWorld.Value.Name.ToString();
                    if(Configuration.rememberInformation == true && username != string.Empty && password != string.Empty)
                    {
                        DataSender.Login(username, password, playername, playerworld);
                        loginAttempted = true;
                    }
                }
            }
            // Get the current target, prioritizing MouseOverTarget if available
            var currentTarget = TargetManager.Target ?? TargetManager.MouseOverTarget;
            if (Configuration.tooltip_LockOnClick && TargetManager.Target != null)
            {
                lockedtarget = true;
            }
            else
            {
                lockedtarget = false;
            }
            // Check if we have a new target by comparing addresses
            if (currentTarget is IGameObject gameObject && gameObject.Address != lastTargetAddress )
            {
                if (InCombatLock() || InDutyLock() || InPvpLock()) return;
               
                WindowOperations.DrawTooltipInfo(gameObject);

                lastTargetAddress = gameObject.Address;  // Update to the new target's address
            }
            // If there's no target and a tooltip was open, close it
            else if (currentTarget == null || currentTarget.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player && TooltipWindow.IsOpen )
            {
                TooltipWindow.IsOpen = false;
                lastTargetAddress = IntPtr.Zero;  // Reset the target address when no target
            }
        }      
        public bool InDutyLock()
        {
            if (Condition[ConditionFlag.BoundByDuty] && Configuration.tooltip_DutyDisabled == true)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public bool InCombatLock()
        {
            if (Condition[ConditionFlag.InCombat] && Configuration.tooltip_HideInCombat == true)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public bool InPvpLock()
        {
            if (ClientState.IsPvP && Configuration.tooltip_PvPDisabled == true)
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
