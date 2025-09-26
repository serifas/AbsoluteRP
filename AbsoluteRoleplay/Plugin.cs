using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows;
using AbsoluteRP.Windows.Ect;
using AbsoluteRP.Windows.Listings;
using AbsoluteRP.Windows.MainPanel;
using AbsoluteRP.Windows.MainPanel.Views;
using AbsoluteRP.Windows.Moderator;
using AbsoluteRP.Windows.Profiles;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AbsoluteRP
{
    public partial class Plugin : IDalamudPlugin
    {
        private bool windowsInitialized = false;
        private float fetchQuestsInRangeTimer = 0f;
        private int lastObjectTableCount = -1;
        public static IGameObject? LastMouseOverTarget;
        public float tooltipAlpha;
        public static Plugin plugin;
        public string username = string.Empty;
        public string password = string.Empty;
        public string playername = string.Empty;
        public bool connected = false;
        public string playerworld = string.Empty;
        private IntPtr lastTargetAddress = IntPtr.Zero;
        public static bool lockedtarget = false;
        private bool openItemTooltip;
        public bool loggedIn;
        public static bool justRegistered;
        public static bool firstopen = true;
        private bool pendingFetchConnections = false;
        private ushort pendingTerritory = 0;
        private const string CommandName = "/arp";
        public static Character character { get; set; } = null;

        public bool loginAttempted = false;
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
        [PluginService] internal static IDtrBar dtrBar { get; private set; } = null!;
        [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;

        [LibraryImport("user32")]
        internal static partial short GetKeyState(int nVirtKey);
        public static bool CtrlPressed() => (GetKeyState(0xA2) & 0x8000) != 0 || (GetKeyState(0xA3) & 0x8000) != 0;
        public Configuration Configuration { get; init; }
        public static bool tooltipLoaded = false;
        public static Plugin? Ui { get; private set; }
        private readonly WindowSystem WindowSystem = new("Absolute Roleplay");
        public OptionsWindow? OptionsWindow { get; private set; }
        public NotesWindow? NotesWindow { get; private set; }
        private ModPanel? ModeratorPanel { get; set; }
        private ListingsWindow? ListingWindow { get; set; }
        public ARPTooltipWindow? TooltipWindow { get; private set; }
        private ReportWindow? ReportWindow { get; set; }
        private MainPanel? MainPanel { get; set; }
        private ImportantNotice? ImportantNoticeWindow { get; set; }
        private ProfileWindow? ProfileWindow { get; set; }
        private BookmarksWindow? BookmarksWindow { get; set; }
        private ARPChatWindow? ArpChatWindow { get; set; }
        private TargetProfileWindow? TargetWindow { get; set; }
        private ImagePreview? ImagePreview { get; set; }
        private TOS? TermsWindow { get; set; }
        private TradeWindow? TradeWindow { get; set; }
        private ConnectionsWindow? ConnectionsWindow { get; set; }

        public float BlinkInterval = 0.5f;
        public bool newConnection;
        private bool isWindowOpen;
        public static bool tooltipShown;

        public static Dictionary<int, IDalamudTextureWrap> staticTextures = new Dictionary<int, IDalamudTextureWrap>();

        private bool needsAsyncInit = true;

        public Plugin()
        {
            plugin = this;

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += UnobservedTaskExceptionHandler;

            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            DataSender.plugin = this;
            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "opens the plugin window."
            });

            Configuration.Initialize(PluginInterface);

            if (string.IsNullOrEmpty(Configuration.dataSavePath))
            {
                Configuration.dataSavePath = $"{PluginInterface?.AssemblyLocation?.Directory?.FullName}\\ARPProfileData";
                Configuration.Save();
            }

            //PluginInterface.UiBuilder.Draw += DrawHitboxes;
            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
            PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
            ContextMenu!.OnMenuOpened += this.OnMenuOpened;
            ClientState.Logout += OnLogout;
            ClientState.Login += LoadConnection;
            ClientState.TerritoryChanged += FetchConnectionsInMap;
            Framework.Update += Update;

            needsAsyncInit = true;
        }

        private async Task InitializeAsync()
        {
            cachedVersion = await GetOnlineVersionAsync();
        }

        private void FetchConnectionsInMap(ushort obj)
        {
            pendingFetchConnections = true;
            pendingTerritory = obj;
        }

        public async Task<Version> GetOnlineVersionAsync()
        {
            try
            {
                using HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);

                string versionText = await client.GetStringAsync("https://raw.githubusercontent.com/serifas/Absolute-Roleplay/refs/heads/main/Version.txt");

                if (Version.TryParse(versionText.Trim(), out Version version))
                {
                    return version;
                }
                else
                {
                    PluginLog.Debug($"Failed to parse version from response: {versionText}");
                    return new Version(0, 0, 0, 0);
                }
            }
            catch (TaskCanceledException)
            {
                PluginLog.Debug("Request timed out while fetching the online version.");
                return new Version(0, 0, 0, 0);
            }
            catch (HttpRequestException ex)
            {
                PluginLog.Debug($"HTTP Debug while fetching version: {ex.Message}");
                return new Version(0, 0, 0, 0);
            }
            catch (Exception ex)
            {
                PluginLog.Debug($"Unexpected Debug in GetOnlineVersionAsync: {ex}");
                return new Version(0, 0, 0, 0);
            }
        }

        public void OpenAndLoadProfileWindow(bool self, int index)
        {
            if (self)
            {
                OpenProfileWindow();
                DataSender.FetchProfiles(Plugin.character);
            }
            else
            {
                TargetProfileWindow.RequestingProfile = true;
                OpenTargetWindow();
            }
            DataSender.FetchProfile(Plugin.character, self, index, plugin.playername, plugin.playerworld, -1);
        }

        private unsafe void OnMenuOpened(IMenuOpenedArgs args)
        {
            var ctx = AgentContext.Instance();
            if (args.AgentPtr != (nint)ctx)
            {
                return;
            }
            var obj = ObjectTable.SearchById(ctx->TargetObjectId.ObjectId);

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
                    DataSender.BookmarkPlayer(Plugin.character, name, worldname);
                },
            });
            args.AddMenuItem(new MenuItem
            {
                Name = "View Absolute RP Profile",
                PrefixColor = 56,
                Prefix = SeIconChar.BoxedQuestionMark,
                OnClicked = _ => {
                    OpenTargetWindow();
                    TargetProfileWindow.characterName = name;
                    TargetProfileWindow.characterWorld = worldname;
                    TargetProfileWindow.RequestingProfile = true;
                    TargetProfileWindow.ResetAllData();
                    DataSender.FetchProfile(Plugin.character, false, -1, name, worldname, -1);
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
                Name = "Bookmark ARP Profile",
                PrefixColor = 56,
                Prefix = SeIconChar.BoxedPlus,
                OnClicked = _ => {
                    DataSender.BookmarkPlayer(Plugin.character, chara.Name.ToString(), chara.HomeWorld.Value.Name.ToString());
                },
            });
            args.AddMenuItem(new MenuItem
            {
                Name = "View ARP Profile",
                PrefixColor = 56,
                Prefix = SeIconChar.BoxedQuestionMark,
                OnClicked = _ => {
                    OpenTargetWindow();
                    TargetProfileWindow.characterName = chara.Name.ToString();
                    TargetProfileWindow.characterWorld = chara.HomeWorld.Value.Name.ToString();
                    TargetProfileWindow.RequestingProfile = true;
                    TargetProfileWindow.ResetAllData();
                    DataSender.FetchProfile(Plugin.character, false, -1, chara.Name.ToString(), chara.HomeWorld.Value.Name.ToString(), -1);
                },
            });
            /*
            args.AddMenuItem(new MenuItem
            {
                Name = "Trade ARP Items",
                PrefixColor = 56,
                Prefix = SeIconChar.BoxedLetterT,
                OnClicked = _ => {
                    DataSender.RequestTargetTrade(character, chara.Name.ToString(), chara.HomeWorld.Value.Name.ToString());
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
            _ = UpdateStatusAsync();
            CheckConnectionsRequestStatus();
            DataSender.SendLogin();
        }

        public void Connect()
        {
            if (!ClientTCP.IsConnected())
            {
                ClientTCP.AttemptConnect();
            }
        }

        public void DisconnectAndLogOut()
        {
            connectionsBarEntry = null;
            statusBarEntry = null;
            MainPanel.status = "Logged Out";
            MainPanel.statusColor = new Vector4(255, 0, 0, 255);
            MainPanel.switchUI();
            loginAttempted = false;
            playername = string.Empty;
            playerworld = string.Empty;
        }

        private void OnLogout(int type, int code)
        {
            DisconnectAndLogOut();
        }

        private void UnobservedTaskExceptionHandler(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
            Framework.RunOnFrameworkThread(() =>
            {
                PluginLog.Debug("Exception handled" + e.Exception.Message);
            });
        }

        public void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Framework.RunOnFrameworkThread(() =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    PluginLog.Debug($"Unhandled exception: {ex}");
                }
                else
                {
                    PluginLog.Debug($"Unhandled non-Exception object: {e.ExceptionObject}");
                }
            });
        }

        private void BookmarkProfile(IMenuItemClickedArgs args)
        {
            if (IsOnline())
            {
                var targetPlayer = TargetManager.Target as IPlayerCharacter;
                DataSender.BookmarkPlayer(character, targetPlayer.Name.ToString(), targetPlayer.HomeWorld.Value.Name.ToString());
            }
        }

        public void LoadStatusBarEntry()
        {
            var entry = dtrBar.Get("AbsoluteRP");
            statusBarEntry = entry;
            string icon = "\uE03E";
            statusBarEntry.Text = icon;
            statusBarEntry.Tooltip = "Absolute Roleplay";
            entry.OnClick = _ => ToggleMainUI();
        }

        public void LoadConnectionsBarEntry(float deltaTime)
        {
            timer += deltaTime;
            float pulse = ((int)(timer / BlinkInterval) % 2 == 0) ? 14 : 0;

            var entry = dtrBar.Get("AbsoluteConnection");
            connectionsBarEntry = entry;
            connectionsBarEntry.Tooltip = "Absolute Roleplay - New Connections Request";
            ConnectionsWindow.currentListing = 2;
            entry.OnClick = _ => DataSender.RequestConnections(Plugin.character);
            SeStringBuilder statusString = new SeStringBuilder();
            statusString.AddUiGlow((ushort)pulse);
            statusString.AddText("\uE070");
            statusString.AddUiGlow(0);
            SeString str = statusString.BuiltString;
            connectionsBarEntry.Text = str;
        }

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
            TooltipWindow?.Dispose();
            ReportWindow?.Dispose();
            ConnectionsWindow?.Dispose();
            ListingWindow?.Dispose();
            ModeratorPanel?.Dispose();
            Misc.Jupiter?.Dispose();
            foreach (IDalamudTextureWrap texture in UI.commonImageWraps.Values)
            {
                texture.Dispose();
            }
            foreach (IDalamudTextureWrap texture in UI.alignmentImageWraps.Values)
            {
                texture.Dispose();
            }
            foreach (IDalamudTextureWrap texture in UI.personalityImageWraps.Values)
            {
                texture.Dispose();
            }
        }

        public void CheckConnectionsRequestStatus()
        {
            TimeSpan deltaTimeSpan = Framework.UpdateDelta;
            float deltaTime = (float)deltaTimeSpan.TotalSeconds;
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
            if (ClientState == null || ObjectTable == null)
                return false;

            try
            {
                if (ClientState.IsLoggedIn)
                {
                    var localPlayer = ClientState.LocalPlayer;
                    if (localPlayer != null)
                    {
                        playername = localPlayer.Name.ToString();
                        playerworld = localPlayer.HomeWorld.Value.Name.ToString() ?? string.Empty;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Debug($"IsOnline() exception: {ex}");
            }
            return false;
        }

        private bool avatarTextureSpawned = false;
        private void DrawUI()
        {
            try
            {
                WindowSystem.Draw();


                // Draw compass overlay if enabled and player is present
                if (Configuration != null
                     && Configuration.showCompass
                     && IsOnline())
                 {
                     var viewport = ImGui.GetMainViewport(); // Only get this here, never store as static/field

                     ImGui.SetNextWindowBgAlpha(0.0f);
                     ImGui.SetNextWindowPos(new Vector2(0, 0), ImGuiCond.Always);

                     ImGui.Begin("##CompassOverlay",
                         ImGuiWindowFlags.NoTitleBar
                         | ImGuiWindowFlags.NoResize
                         | ImGuiWindowFlags.NoMove
                         | ImGuiWindowFlags.NoScrollbar
                         | ImGuiWindowFlags.NoScrollWithMouse
                         | ImGuiWindowFlags.NoInputs
                         | ImGuiWindowFlags.NoSavedSettings
                         | ImGuiWindowFlags.NoFocusOnAppearing);

                     PlayerInteractions.DrawDynamicCompass(
                         viewport.WorkSize.X / 2,
                         300,
                         400,
                         40,
                         ClientState.LocalPlayer.Rotation
                     );


                     ImGui.End();
                 }
                
            
            }
            catch (Exception ex)
            {
                PluginLog.Debug($"Exception in DrawUI: {ex}");
            }
        }

        public async Task LoadWindow(Window window, bool Toggle)
        {
            if (!ClientTCP.IsConnected())
            {
                LoadConnection();
            }
            if (window == null)
            {
                PluginLog.Debug("LoadWindow called with a null window.");
                return;
            }

            if (await IsToSVersionUpdated())
            {
                PluginLog.Debug($"Version matched, loading window: {window}");
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
                PluginLog.Debug("Version mismatch, opening Terms of Service window.");

                if (Configuration.TOSVersion == null)
                {
                    Configuration.TOSVersion = new Version(0, 0, 0, 0);
                }

                TermsWindow.version = await GetOnlineVersionAsync();
                TermsWindow.IsOpen = true;
            }
        }

        public void ToggleConfigUI() => OptionsWindow.IsOpen = true;

        public void ToggleMainUI()
        {
            _ = LoadWindow(MainPanel, true);
        }

        public void OpenMainPanel()
        {
            _ = LoadWindow(MainPanel, false);
        }

        public void OpenTermsWindow() => TermsWindow.IsOpen = true;
        public void OpenImagePreview() => ImagePreview.IsOpen = true;
        public void OpenModeratorPanel() => ModeratorPanel.IsOpen = true;
        public void OpenProfileWindow() => ProfileWindow.IsOpen = true;
        public void CloseProfileWindow() => ProfileWindow.IsOpen = false;
        public void OpenTargetWindow() => TargetWindow.IsOpen = true;
        public void OpenBookmarksWindow() => BookmarksWindow.IsOpen = true;
        public void OpenReportWindow() => ReportWindow.IsOpen = true;
        public void OpenOptionsWindow() => OptionsWindow.IsOpen = true;
        public void OpenConnectionsWindow() => ConnectionsWindow.IsOpen = true;
        public void OpenARPTooltip() => TooltipWindow.IsOpen = true;
        public void CloseARPTooltip() => TooltipWindow.IsOpen = false;
        public void OpenProfileNotes() => NotesWindow.IsOpen = true;
        public void OpenListingsWindow() => ListingWindow.IsOpen = true;
        public void ToggleChatWindow() => ArpChatWindow.IsOpen = true;
        public void OpenImportantNoticeWindow() => ImportantNoticeWindow.IsOpen = true;
        public void OpenTradeWindow() => TradeWindow.IsOpen = true;
        public void CloseTradeWindow() => TradeWindow.IsOpen = false;

        internal async Task UpdateStatusAsync()
        {
            try
            {
                var status = await ClientTCP.GetConnectionStatusAsync(ClientTCP.clientSocket);
                MainPanel.serverStatus = status.Item2;
                MainPanel.serverStatusColor = status.Item1;
            }
            catch (Exception ex)
            {
                PluginLog.Debug("Debug updating status: " + ex.ToString());
            }
        }
        public void Update(IFramework framework)
        {
            if (needsAsyncInit)
            {
                needsAsyncInit = false;
                _ = InitializeAsync();
            }

            if (!windowsInitialized && IsOnline())
            {
                windowsInitialized = true;

                OptionsWindow = new OptionsWindow();
                MainPanel = new MainPanel();
                TermsWindow = new TOS();
                ProfileWindow = new ProfileWindow();
                ImagePreview = new ImagePreview();
                BookmarksWindow = new BookmarksWindow();
                ImportantNoticeWindow = new ImportantNotice();
                TargetWindow = new TargetProfileWindow();
                ModeratorPanel = new ModPanel();
                ArpChatWindow = new ARPChatWindow(chatgui);
                ReportWindow = new ReportWindow();
                ConnectionsWindow = new ConnectionsWindow();
                TooltipWindow = new ARPTooltipWindow();
                NotesWindow = new NotesWindow();
                ListingWindow = new ListingsWindow();
                TradeWindow = new TradeWindow();

                WindowSystem.AddWindow(OptionsWindow);
                WindowSystem.AddWindow(MainPanel);
                WindowSystem.AddWindow(TermsWindow);
                WindowSystem.AddWindow(ProfileWindow);
                WindowSystem.AddWindow(ImagePreview);
                WindowSystem.AddWindow(BookmarksWindow);
                WindowSystem.AddWindow(TargetWindow);
                WindowSystem.AddWindow(ModeratorPanel);
                WindowSystem.AddWindow(ReportWindow);
                WindowSystem.AddWindow(ConnectionsWindow);
                WindowSystem.AddWindow(TooltipWindow);
                WindowSystem.AddWindow(NotesWindow);
                WindowSystem.AddWindow(ListingWindow);
                WindowSystem.AddWindow(ArpChatWindow);
                WindowSystem.AddWindow(ImportantNoticeWindow);
                WindowSystem.AddWindow(TradeWindow);

                LoadStatusBarEntry();
                chatgui.ChatMessage += ArpChatWindow.OnChatMessage;
                
            }

            if (IsOnline() &&
                (character == null
                 || character.characterName != ClientState.LocalPlayer.Name.ToString()
                 || character.characterWorld != ClientState.LocalPlayer.HomeWorld.Value.Name.ToString()))
            {
                character = Plugin.plugin.Configuration?.characters?.FirstOrDefault(
                    x => x?.characterName == ClientState.LocalPlayer.Name.ToString()
                      && x?.characterWorld == ClientState.LocalPlayer.HomeWorld.Value.Name.ToString());
            }
            if (Configuration.showCompass)
            {
                fetchQuestsInRangeTimer += (float)Framework.UpdateDelta.TotalSeconds;
                if (fetchQuestsInRangeTimer >= 25f)
                {
                    fetchQuestsInRangeTimer = 0f;
                    var localPlayer = ClientState.LocalPlayer;
                    List<IPlayerCharacter> nearbyPlayers = ObjectTable
                        .Where(obj => obj is IPlayerCharacter pc && pc != localPlayer)
                        .Cast<IPlayerCharacter>()
                        .Where(pc => Vector3.Distance(pc.Position, localPlayer.Position) <= 1000)
                        .ToList();
                    DataSender.RequestCompassFromList(character, nearbyPlayers);
                }
            }
            // var currentTarget can be null or not an IGameObject, so always check type before using ObjectKind
            var currentTarget = TargetManager.Target ?? TargetManager.MouseOverTarget;

            if (Configuration.tooltip_LockOnClick && TargetManager.Target != null)
            {
                lockedtarget = true;
            }
            else
            {
                lockedtarget = false;
            }

            if (currentTarget is IGameObject gameObject && gameObject.Address != lastTargetAddress)
            {
                if (InCombatLock() || InDutyLock() || InPvpLock())
                    return;

                WindowOperations.DrawTooltipInfo(gameObject);

                lastTargetAddress = gameObject.Address;
            }
            else if (
                TooltipWindow != null &&
                (
                    currentTarget == null ||
                    (currentTarget is IGameObject obj &&
                     obj.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player &&
                     TooltipWindow.IsOpen)
                )
            )
            {
                TooltipWindow.IsOpen = false;
                lastTargetAddress = IntPtr.Zero;
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