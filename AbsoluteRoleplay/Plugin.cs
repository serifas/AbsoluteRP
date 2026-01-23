using AbsoluteRP.Helpers;
using AbsoluteRP.Windows;
using AbsoluteRP.Windows.Ect;
using AbsoluteRP.Windows.Listings;
using AbsoluteRP.Windows.MainPanel;
using AbsoluteRP.Windows.Moderator;
using AbsoluteRP.Windows.Profiles;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Social.Views;
using AbsoluteRP.Windows.Social.Views.Groups;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.ContextMenu;
using MenuItem = Dalamud.Game.Gui.ContextMenu.MenuItem;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Networking;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace AbsoluteRP
{
    public partial class Plugin : IDalamudPlugin
    {
        private bool windowsInitialized = false;
        private float playersInRangeTimer = 0f;
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
        private const string FauxNameCommand = "/arp-identify-as";
        public static Defines.Character character { get; set; } = null;

        public bool loginAttempted = false;
        private IDtrBarEntry? statusBarEntry;
        private IDtrBarEntry? connectionsBarEntry;
        private IDtrBarEntry? chatBarEntry;
        private IDtrBarEntry? groupInviteBarEntry;
        public static bool BarAdded = false;
        internal static float timer = 0f;
        public static UiBuilder builder;
        public static IGameGui GameGUI;
        public static HashSet<string> viewedPlayers = new HashSet<string>();


        [Signature("40 53 55 57 41 56 48 81 EC ?? ?? ?? ?? 48 8B 84 24", DetourName = nameof(UpdateNameplate))]
        private Hook<NameplateDelegate>? nameplateHook;

        private unsafe delegate void* NameplateDelegate(
            RaptureAtkModule* raptureAtkModule,
            RaptureAtkModule.NamePlateInfo* namePlateInfo,
            NumberArrayData* numArray,
            StringArrayData* stringArray,
            BattleChara* battleChara,
            int numArrayIndex,
            int stringArrayIndex);



        [PluginService] internal static IDataManager DataManager { get; private set; } = null;
        [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null;
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null;
        [PluginService] internal static IFramework Framework { get; private set; } = null;
        [PluginService] internal static ICondition Condition { get; private set; } = null;
        [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null;
        [PluginService] internal static IClientState ClientState { get; private set; } = null;
        [PluginService] internal static IPlayerState PlayerState { get; private set; } = null;
        [PluginService] internal static ITargetManager TargetManager { get; private set; } = null;
        [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null;
        [PluginService] internal static IChatGui chatgui { get; private set; } = null;
        [PluginService] internal static IDtrBar dtrBar { get; private set; } = null!;
        [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;
        [PluginService] public static ICommandManager Commands { get; private set; } = null!;
        [PluginService] public static IDataManager Data { get; private set; } = null!;
        [PluginService] public static IObjectTable Objects { get; private set; } = null!;
        [PluginService] public static ITargetManager Targets { get; private set; } = null!;
        [PluginService] public static IGameConfig GameConfig { get; private set; } = null!;
        [PluginService] public static IGameInteropProvider HookProvider { get; private set; } = null!;
        [PluginService] public static IPluginLog Log { get; private set; } = null!;
        [PluginService] public static IGameGui GameGui { get; private set; } = null!;
        [PluginService] public static IChatGui Chat { get; private set; } = null!;
        [PluginService] public static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
        [PluginService] public static INamePlateGui NamePlateGui { get; private set; } = null!;

        [LibraryImport("user32")]
        internal static partial short GetKeyState(int nVirtKey);
        public static bool CtrlPressed() => (GetKeyState(0xA2) & 0x8000) != 0 || (GetKeyState(0xA3) & 0x8000) != 0;
        public Configuration Configuration { get; init; }
        public static bool tooltipLoaded = false;
        public static Plugin? Ui { get; private set; }
        internal readonly WindowSystem WindowSystem = new("Absolute Roleplay");
        public OptionsWindow? OptionsWindow { get; private set; }
        public NotesWindow? NotesWindow { get; private set; }
        private ModPanel? ModeratorPanel { get; set; }
        private SocialWindow? SocialWindow { get; set; }
        public ARPTooltipWindow? TooltipWindow { get; private set; }
        private ReportWindow? ReportWindow { get; set; }
        private MainPanel? MainPanel { get; set; }
        private ImportantNotice? ImportantNoticeWindow { get; set; }
        private ProfileWindow? ProfileWindow { get; set; }
        private ARPChatWindow? ArpChatWindow { get; set; }
        private TargetProfileWindow? TargetWindow { get; set; }
        private ImagePreview? ImagePreview { get; set; }
        private TOS? TermsWindow { get; set; }
        private TradeWindow? TradeWindow { get; set; }
        private SystemsWindow? SystemsWindow { get; set; }
        public static GroupInviteNotification? groupInviteNotification { get; set; }
        private ViewLikesWindow? ViewLikesWindow { get; set; }
        private LikeDetailsWindow? LikeDetailsWindow { get; set; }
        private ListingsWindow? ListingsWindow { get; set; }
        // YouTubePlayerWindow is a WebView2 Forms window that runs on its own thread

        public float BlinkInterval = 0.5f;
        public bool newConnection = false;
        private bool isWindowOpen;
        public static bool tooltipShown;

        public static Dictionary<int, IDalamudTextureWrap> staticTextures = new Dictionary<int, IDalamudTextureWrap>();

        private bool needsAsyncInit = true;
        private string displayName = string.Empty;

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
            CommandManager.AddHandler(FauxNameCommand, new CommandInfo(SetFauxNameCommand)
            {
                HelpMessage = "Sets a faux name for your in game character."
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
            Plugin.HookProvider.InitializeFromAttributes(this);

            // Enable the nameplate hook for faux names
            nameplateHook?.Enable();

            needsAsyncInit = true;

            // Initialize CEF dependency manager for YouTube video playback
            CefDependencyManager.Initialize();

            LoadConnection();
            
        }
        public static List<IPlayerCharacter> VisiblePlayers()
        {
            var localPlayer = ObjectTable.LocalPlayer;
            List<IPlayerCharacter> nearbyPlayers = ObjectTable
                .Where(obj => obj is IPlayerCharacter pc && pc != localPlayer)
                .Cast<IPlayerCharacter>()
                .Where(pc => Vector3.Distance(pc.Position, localPlayer.Position) <= 1000)
                .ToList();
            return nearbyPlayers;
        }
        public static string GetNameForPlate(
        string originalName,
        int objectIndex,
        ulong localContentId,
        IDictionary<ulong, (string, uint)> identifyAs,
        string? altCode = null)
        {
            string? overrideName = null;


            if (objectIndex == 0 && identifyAs.TryGetValue(localContentId, out var identifyAsTuple))
            {
                overrideName = identifyAsTuple.Item1;
                return overrideName;
            }
            else
            {
                return originalName;
            }
        }
        private void SetFauxNameCommand(string command, string arguments)
        {
            displayName = arguments ?? string.Empty;

            // Update local configuration immediately for self
            if (PlayerState.ContentId != 0)
            {
                if (string.IsNullOrEmpty(displayName))
                {
                    // Clear the faux name
                    Configuration.IdentifyAs.Remove(PlayerState.ContentId);
                    Configuration.fauxName = string.Empty;
                }
                else
                {
                    Configuration.IdentifyAs[PlayerState.ContentId] = (displayName, 0);
                    Configuration.fauxName = displayName;
                }
                Configuration.Save();
                PluginLog.Information($"Faux name set locally to: {displayName}");
            }

            // Also broadcast to other players and server
            DataSender.SendFauxNameBroadcast(character, displayName, !string.IsNullOrEmpty(displayName), VisiblePlayers());
            DataSender.SendFauxNameBroadcast(character, displayName, !string.IsNullOrEmpty(displayName), new List<IPlayerCharacter>() { ClientState.LocalPlayer });
        }

        public static void SetFauxName(string displayName, string playername, string playerworld)
        {

                // Ensure all access to Dalamud client state happens on the framework/main thread.
                try
                {
                    Framework.RunOnFrameworkThread(() =>
                    {
                        try
                        {
                            var localPlayer = ObjectTable.LocalPlayer;
                            if (localPlayer == null)
                                return;

                            var localName = localPlayer.Name.ToString();
                            var localWorld = localPlayer.HomeWorld.Value.Name.ToString();

                            if (playername == localName && playerworld == localWorld)
                            {
                                // Update config for the character's faux name
                                if (PlayerState.ContentId != 0)
                                {
                                    plugin.Configuration.IdentifyAs[PlayerState.ContentId] = (displayName, 0);
                                    plugin.Configuration.Save();
                                }

                                plugin.Configuration.fauxName = displayName;
                                plugin.Configuration.Save();
                            }
                        }
                        catch (Exception ex)
                        {
                            PluginLog.Debug($"Exception in SetFauxName inner: {ex}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    PluginLog.Debug($"Exception scheduling SetFauxName on framework thread: {ex}");
                }
            
            
        }
        private static readonly Lazy<Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.World>?> WorldSheetLazy =
        new(() => Plugin.DataManager?.GetExcelSheet<Lumina.Excel.Sheets.World>());

        private unsafe void* UpdateNameplate(
        RaptureAtkModule* raptureAtkModule,
        RaptureAtkModule.NamePlateInfo* namePlateInfo,
        NumberArrayData* numArray,
        StringArrayData* stringArray,
        BattleChara* battleChara,
        int numArrayIndex,
        int stringArrayIndex)
        {
           
            try
            {
                // Validate all pointers before use
                if (nameplateHook == null ||
                    raptureAtkModule == null ||
                    namePlateInfo == null ||
                    numArray == null ||
                    stringArray == null ||
                    battleChara == null)
                {
                    return null;
                }

                var r = nameplateHook.Original(raptureAtkModule, namePlateInfo, numArray, stringArray, battleChara, numArrayIndex, stringArrayIndex);

                if (!InNameCombatLock() && !InNameDutyLock() && !InNamePvPLock() && Configuration.showFauxNames)
                {
                    var gameObject = &battleChara->Character.GameObject;
                    if (gameObject == null)
                        return r;

                    if (gameObject->ObjectKind == ObjectKind.Pc)
                    {
                        var chara = &battleChara->Character;
                        if (chara == null)
                            return r;

                        // Defensive: Ensure Name array is not null and has expected length
                        Span<byte> nameSpan;
                        try
                        {
                            nameSpan = MemoryMarshal.CreateSpan(ref chara->Name[0], 32);
                        }
                        catch
                        {
                            return r;
                        }

                        int nameLength = nameSpan.IndexOf((byte)0);
                        if (nameLength < 0 || nameLength > nameSpan.Length)
                            nameLength = nameSpan.Length;

                        string name;
                        try
                        {
                            name = Encoding.UTF8.GetString(nameSpan.Slice(0, nameLength));
                        }
                        catch
                        {
                            name = string.Empty;
                        }

                        var homeWorld = chara->HomeWorld;
                        var objectIndex = gameObject->ObjectIndex;

                        // Defensive: Get world name safely, using cached sheet
                        string worldName = string.Empty;
                        try
                        {
                            var worldSheet = WorldSheetLazy.Value;
                            var worldRow = worldSheet?.GetRowOrDefault((uint)homeWorld);
                            if (worldRow != null)
                            {
                                // Replace 'Name' with the actual property if needed
                                worldName = worldRow.Value.Name.ToString();
                            }
                        }
                        catch
                        {
                            worldName = string.Empty;
                        }

                        // Get local player and check range
                        var localPlayer = ObjectTable.LocalPlayer;
                        float distance = float.MaxValue;
                        if (localPlayer != null)
                        {
                            try
                            {
                                var targetPos = new Vector3(gameObject->Position.X, gameObject->Position.Y, gameObject->Position.Z);
                                distance = Vector3.Distance(localPlayer.Position, targetPos);
                            }
                            catch
                            {
                                distance = float.MaxValue;
                            }
                        }

                        if (distance <= 5000)
                        {
                            // Take a thread-safe snapshot of playerDataMap
                            List<PlayerData> playerDataSnapshot;
                            lock (PlayerInteractions.playerDataMap)
                            {
                                playerDataSnapshot = PlayerInteractions.playerDataMap.ToList();
                            }

                            var playerData = playerDataSnapshot
                                .FirstOrDefault(pd =>
                                    string.Equals(pd.playername?.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(pd.worldname?.Trim(), worldName.Trim(), StringComparison.OrdinalIgnoreCase));

                            if (playerData != null && playerData.fauxStatus && !string.IsNullOrEmpty(playerData.fauxName))
                            {
                                string displayName = GetNameForPlate(
                                    playerData.fauxName,
                                    objectIndex,
                                    Plugin.PlayerState.ContentId,
                                    Plugin.plugin.Configuration.IdentifyAs
                                );
                                namePlateInfo->Name.SetString(displayName);
                                namePlateInfo->IsDirty = true;
                                return r;
                            }
                        }

                        // For self, check IdentifyAs
                        if (objectIndex == 0)
                        {
                            if (Plugin.plugin.Configuration.IdentifyAs.TryGetValue(Plugin.PlayerState.ContentId, out var identifyAs))
                            {
                                string displayName = GetNameForPlate(
                                    name,
                                    objectIndex,
                                    Plugin.PlayerState.ContentId,
                                    Plugin.plugin.Configuration.IdentifyAs
                                );
                                namePlateInfo->Name.SetString(displayName);
                                namePlateInfo->IsDirty = true;
                            }
                        }
                        else
                        {
                            // Fallback: set real name
                            namePlateInfo->Name.SetString(name);
                            namePlateInfo->IsDirty = true;
                        }
                    }
                }
                return r;
            }
            catch (Exception ex)
            {
                PluginLog.Debug($"Exception in UpdateNameplate: {ex}");
                return null;
            }
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
                Prefix = SeIconChar.BoxedLetterB,
                OnClicked = _ => {
                    DataSender.BookmarkPlayer(Plugin.character, name, worldname, -1);
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


            args.AddMenuItem(new MenuItem
            {
                Name = "Invite to Group",
                PrefixColor = 56,
                Prefix = SeIconChar.BoxedPlus,
                OnClicked = _ => {
                    GroupInviteDialog.Open(name, worldname);
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
                Prefix = SeIconChar.BoxedLetterB,
                OnClicked = _ => {
                    DataSender.BookmarkPlayer(Plugin.character, chara.Name.ToString(), chara.HomeWorld.Value.Name.ToString(), -1);
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
            args.AddMenuItem(new MenuItem
            {
                Name = "Invite to Group",
                PrefixColor = 56,
                Prefix = SeIconChar.BoxedPlus,
                OnClicked = _ => {
                    GroupInviteDialog.Open(chara.Name.ToString(), chara.HomeWorld.Value.Name.ToString());
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
            // Note: Don't call CheckConnectionsRequestStatus() here
            // The status bar will be updated by ReceiveConnectionsRequest when
            // the server sends a pending connection request notification
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
            MainPanel.switchUI();
            loginAttempted = false;
            playername = string.Empty;
            playerworld = string.Empty;
            newConnection = false; // Reset connection request flag on logout
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
                DataSender.BookmarkPlayer(character, targetPlayer.Name.ToString(), targetPlayer.HomeWorld.Value.Name.ToString(), -1);
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
        /*
        public void LoadConnectionsBarEntry(float deltaTime)
        {
            timer += deltaTime;
            float pulse = ((int)(timer / BlinkInterval) % 2 == 0) ? 14 : 0;

            var entry = dtrBar.Get("AbsoluteConnection");
            connectionsBarEntry = entry;
            connectionsBarEntry.Tooltip = "Absolute Roleplay - New Connections Request";
            Connections.currentListing = 2;
            entry.OnClick = _ => DataSender.RequestConnections(Plugin.character);
            SeStringBuilder statusString = new SeStringBuilder();
            statusString.AddUiGlow((ushort)pulse);
            statusString.AddText("\uE070");
            statusString.AddUiGlow(0);
            SeString str = statusString.BuiltString;
            connectionsBarEntry.Text = str;
        }*/

        public void UnloadConnectionsBar()
        {
            if (connectionsBarEntry != null)
            {
                connectionsBarEntry.Remove();
                connectionsBarEntry = null;
            }
        }

        public void LoadGroupInviteBarEntry(float deltaTime)
        {
            int inviteCount = GroupInviteNotification.GetPendingInviteCount();

            if (inviteCount > 0)
            {
                timer += deltaTime;
                float pulse = ((int)(timer / BlinkInterval) % 2 == 0) ? 14 : 0;

                var entry = dtrBar.Get("AbsoluteGroupInvite");
                groupInviteBarEntry = entry;
                groupInviteBarEntry.Tooltip = $"Absolute Roleplay - {inviteCount} pending group invite{(inviteCount > 1 ? "s" : "")}";
                entry.OnClick = _ => GroupInviteNotification.ShowNextInvite();
                SeStringBuilder statusString = new SeStringBuilder();
                statusString.AddUiGlow((ushort)pulse);
                statusString.AddText("\uE06F"); // Group/users icon
                if (inviteCount > 1)
                {
                    statusString.AddText($" {inviteCount}");
                }
                statusString.AddUiGlow(0);
                SeString str = statusString.BuiltString;
                groupInviteBarEntry.Text = str;
            }
            else
            {
                UnloadGroupInviteBar();
            }
        }

        public void UnloadGroupInviteBar()
        {
            if (groupInviteBarEntry != null)
            {
                groupInviteBarEntry?.Remove();
                groupInviteBarEntry = null;
            }
        }

        public void Dispose()
        {
            WindowSystem?.RemoveAllWindows();
            statusBarEntry?.Remove();
            statusBarEntry = null;
            connectionsBarEntry?.Remove();
            connectionsBarEntry = null;
            groupInviteBarEntry?.Remove();
            groupInviteBarEntry = null;
            CommandManager.RemoveHandler(CommandName);
            CommandManager.RemoveHandler(FauxNameCommand);
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
            TooltipWindow?.Dispose();
            ReportWindow?.Dispose();
            SocialWindow?.Dispose();
            ModeratorPanel?.Dispose();
            SystemsWindow?.Dispose();
            nameplateHook?.Disable();
            nameplateHook?.Dispose();
            nameplateHook = null;
            YouTubePlayerWindow.ClosePlayer(); // Close WebView2 Forms window if open
            Misc.CleanupAudioPlayers(); // Stop and dispose all audio players
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
        /*
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
        }*/

        public void CheckGroupInviteStatus()
        {
            TimeSpan deltaTimeSpan = Framework.UpdateDelta;
            float deltaTime = (float)deltaTimeSpan.TotalSeconds;
            LoadGroupInviteBarEntry(deltaTime);
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

        public static bool IsOnline()
        {
            if (ClientState == null || ObjectTable == null)
                return false;

            try
            {
                var localPlayer = ObjectTable.LocalPlayer;
                if (localPlayer != null)
                {
                    plugin.playername = localPlayer.Name.ToString();
                    plugin.playerworld = localPlayer.HomeWorld.Value.Name.ToString() ?? string.Empty;
                    return true;
                }
            }
            catch (Exception ex)
            {
                PluginLog.Debug($"IsOnline() exception: {ex}");
            }
            return false;
        }

        private bool avatarTextureSpawned = false;
        private bool wasSocialWindowOpen = false;
        private bool wasProfileWindowOpen = false;
        private bool wasTargetWindowOpen = false;

        private void DrawUI()
        {
            try
            {
                // Check if SocialWindow was just closed and cleanup audio
                bool isSocialWindowOpen = SocialWindow?.IsOpen ?? false;
                if (wasSocialWindowOpen && !isSocialWindowOpen)
                {
                    Misc.CleanupAudioPlayers();
                }
                wasSocialWindowOpen = isSocialWindowOpen;

                // Check if ProfileWindow was just closed and cleanup audio
                bool isProfileWindowOpen = ProfileWindow?.IsOpen ?? false;
                if (wasProfileWindowOpen && !isProfileWindowOpen)
                {
                    Misc.CleanupAudioPlayers();
                }
                wasProfileWindowOpen = isProfileWindowOpen;

                // Check if TargetWindow (TargetProfileWindow) was just closed and cleanup audio
                bool isTargetWindowOpen = TargetWindow?.IsOpen ?? false;
                if (wasTargetWindowOpen && !isTargetWindowOpen)
                {
                    Misc.CleanupAudioPlayers();
                }
                wasTargetWindowOpen = isTargetWindowOpen;

                WindowSystem.Draw();

                PlayerInteractions.DrawCompass();

                // Draw group invite dialog
                GroupInviteDialog.Draw();

                // Draw group join request dialog
                GroupJoinRequestDialog.Draw();

                // Update DTR bar entries
                CheckGroupInviteStatus();
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
            DataSender.FetchProfiles(Plugin.character);
        }

        public void OpenMainPanel()
        {
            _ = LoadWindow(MainPanel, false);
            DataSender.FetchProfiles(Plugin.character);
        }

        public void OpenTermsWindow() => TermsWindow.IsOpen = true;
        public void OpenImagePreview() => ImagePreview.IsOpen = true;
        public void OpenModeratorPanel() => ModeratorPanel.IsOpen = true;
        public void OpenProfileWindow() => ProfileWindow.IsOpen = true;
        public void CloseProfileWindow() => ProfileWindow.IsOpen = false;
        public void OpenTargetWindow() => TargetWindow.IsOpen = true;
        public void OpenReportWindow() => ReportWindow.IsOpen = true;
        public void OpenOptionsWindow() => OptionsWindow.IsOpen = true;
        public void OpenARPTooltip() => TooltipWindow.IsOpen = true;
        public void CloseARPTooltip() => TooltipWindow.IsOpen = false;
        public void OpenProfileNotes() => NotesWindow.IsOpen = true;
        public void OpenSocialWindow() => SocialWindow.IsOpen = true;
        public void ToggleChatWindow() => ArpChatWindow.IsOpen = true;
        public void OpenImportantNoticeWindow() => ImportantNoticeWindow.IsOpen = true;
        public void OpenTradeWindow() => TradeWindow.IsOpen = true;
        public void CloseTradeWindow() => TradeWindow.IsOpen = false;
        public void ToggleSystemsWindow() => SystemsWindow.Toggle();
        public void ToggleViewLikesWindow() => ViewLikesWindow.Toggle();
        public void OpenListingsWindow() => ListingsWindow.IsOpen = true;
        public void OpenLikeDetailsWindow(ProfileData profile)
        {
            LikeDetailsWindow.SetProfile(profile);
            LikeDetailsWindow.IsOpen = true;
        }

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
                ImportantNoticeWindow = new ImportantNotice();
                TargetWindow = new TargetProfileWindow();
                ModeratorPanel = new ModPanel();
                ArpChatWindow = new ARPChatWindow(chatgui);
                ReportWindow = new ReportWindow();
                TooltipWindow = new ARPTooltipWindow();
                NotesWindow = new NotesWindow();
                SocialWindow = new SocialWindow();
                TradeWindow = new TradeWindow();
                SystemsWindow = new SystemsWindow();
                groupInviteNotification = new GroupInviteNotification();
                ViewLikesWindow = new ViewLikesWindow(this);
                LikeDetailsWindow = new LikeDetailsWindow();
                ListingsWindow = new ListingsWindow();
                // YouTubePlayerWindow creates itself when OpenVideo is called

                WindowSystem.AddWindow(OptionsWindow);
                WindowSystem.AddWindow(MainPanel);
                WindowSystem.AddWindow(TermsWindow);
                WindowSystem.AddWindow(ProfileWindow);
                WindowSystem.AddWindow(ImagePreview);
                WindowSystem.AddWindow(TargetWindow);
                WindowSystem.AddWindow(ModeratorPanel);
                WindowSystem.AddWindow(ReportWindow);
                WindowSystem.AddWindow(TooltipWindow);
                WindowSystem.AddWindow(NotesWindow);
                WindowSystem.AddWindow(SocialWindow);
                WindowSystem.AddWindow(ArpChatWindow);
                WindowSystem.AddWindow(ImportantNoticeWindow);
                WindowSystem.AddWindow(TradeWindow);
                WindowSystem.AddWindow(SystemsWindow);
                WindowSystem.AddWindow(groupInviteNotification);
                WindowSystem.AddWindow(ViewLikesWindow);
                WindowSystem.AddWindow(LikeDetailsWindow);
                WindowSystem.AddWindow(ListingsWindow);

                LoadStatusBarEntry();
                chatgui.ChatMessage += ArpChatWindow.OnChatMessage;
                
            }

            if (IsOnline() &&
                (character == null
                 || character.characterName != ObjectTable.LocalPlayer.Name.ToString()
                 || character.characterWorld != ObjectTable.LocalPlayer.HomeWorld.Value.Name.ToString()))
            {
                character = Plugin.plugin.Configuration?.characters?.FirstOrDefault(
                    x => x?.characterName == ObjectTable.LocalPlayer.Name.ToString()
                      && x?.characterWorld == ObjectTable.LocalPlayer.HomeWorld.Value.Name.ToString());
            }

            if (IsOnline())
            {
                playersInRangeTimer += (float)Framework.UpdateDelta.TotalSeconds;
                if (playersInRangeTimer >= 20f)
                {
                    if(MainPanel.loggedIn == true)
                    {
                        playersInRangeTimer = 0f;         
                        DataSender.RequestCompassFromList(character, VisiblePlayers());
                        var visible = VisiblePlayers();

                        var playersInRange = VisiblePlayers();
                        DataSender.RequestCompassFromList(character, playersInRange);

                        // Build stable keys for current players and prune viewedPlayers of those who left
                        var currentKeys = new HashSet<string>(playersInRange.Select(p => $"{p.Name}@{p.HomeWorld.Value.Name}"));
                        viewedPlayers.RemoveWhere(k => !currentKeys.Contains(k));

                        foreach (var player in playersInRange)
                        {
                            try
                            {
                                var playerKey = $"{player.Name}@{player.HomeWorld.Value.Name}";

                                if (viewedPlayers.Contains(playerKey))
                                    continue;

                                // Preconditions: we must have a valid local character, connection and a configured faux name
                                if (character == null)
                                {
                                    continue;
                                }
                                if (!ClientTCP.IsConnected())
                                {
                                    continue;
                                }

                                // Mark seen and send broadcast only for this newly-seen player (single-item list)
                                viewedPlayers.Add(playerKey);

                                // If we have a faux name set, broadcast it to this newly-seen player
                                if (Configuration.IdentifyAs.TryGetValue(PlayerState.ContentId, out var identifyAs) &&
                                    !string.IsNullOrEmpty(identifyAs.Item1))
                                {
                                    DataSender.SendFauxNameBroadcast(character, identifyAs.Item1, true, new List<IPlayerCharacter> { player });
                                }
                            }
                            catch (Exception ex)
                            {
                                PluginLog.Debug($"Error handling visible player in Update: {ex}");
                            }
                        }
                    }
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
                if (InTooltipCombatLock() || InTooltipDutyLock() || InTooltipPvpLock())
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

        public bool InTooltipDutyLock()
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
        public bool InTooltipCombatLock()
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
        public bool InTooltipPvpLock()
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
        public static bool InCompassCombatLock()
        {
            return Condition[ConditionFlag.InCombat] && Plugin.plugin.Configuration.showCompassInCombat == false;
        }
        public bool IsCompassPvpLock()
        {
            // Disable compass in PvP if showCompassInPvP is false
            return ClientState.IsPvP && Configuration.showCompassInPvP == false;
        }

        public bool IsCompassDutyLock()
        {
            // Disable compass in Duty if showCompassInDuty is false
            return Condition[ConditionFlag.BoundByDuty] && Configuration.showCompassInDuty == false;
        }
        public bool InNameCombatLock()
        {
            return Condition[ConditionFlag.InCombat] && Configuration.displayFauxNamesInCombat == false;
        }
        public bool InNamePvPLock()
        {
            // Disable compass in PvP if showCompassInPvP is false
            return ClientState.IsPvP && Configuration.displayFauxNamesInPvP == false;
        }

        public bool InNameDutyLock()
        {
            // Disable compass in Duty if showCompassInDuty is false
            return Condition[ConditionFlag.BoundByDuty] && Configuration.displayFauxnamesInDuty == false;
        }

    }
}