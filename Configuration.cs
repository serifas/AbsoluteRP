using AbsoluteRP.Defines;
using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Numerics;
using System.Security.Principal;

namespace AbsoluteRP;

// Persistent plugin settings - Dalamud serializes this to JSON on disk. Everything here survives plugin reloads and game restarts.
[Serializable]
public class Configuration : IPluginConfiguration
{
    // Server account credentials (populated on login or import)
    public Account account { get; set; } = new Account()
    {
        accountKey = string.Empty,
        accountName = string.Empty
    };

    // All FFXIV characters linked to this account (each verified via Lodestone)
    public List<Character> characters { get; set; } = new List<Character>();

    public int uiScale { get; set; }
    public int Version { get; set; } = 0;
    public Version TOSVersion { get; set; } = new Version("0.0.0.0"); // last TOS version the user agreed to
    public bool AlwaysOpenDefaultImport { get; set; } = false;
    public bool TutorialsEnabled { get; set; } = true;
    public bool ProfileTutorialCompleted { get; set; } = false;

    // Not used yet, vfx disabled for now
    public bool VfxEnabled { get; set; } = true;
    // same with this
    public System.Numerics.Vector4 VfxColor { get; set; } = new(0.55f, 0.78f, 1f, 1f);
    // and this
    public int VfxPreset { get; set; } = 0;
    public bool AnimationsEnabled { get; set; } = true;
    // Immersive mode - parallax + 3D-styled nav on profile views, plus (planned) a character-inspect duplicate to the side of the profile. Always on: the hologram HUD is the profile view. (Kept as a property so old configs and call sites keep working.)
    public bool ImmersiveModeEnabled { get => true; set { } }
    // Which immersive HUD theme this user prefers (index into. ImmersiveThemes.All): 0 Allagan, 1 Nymian, 2 Sharlayan, 3 Voidtouched, 4 Aether, 5 Standard. 0 with an empty ImmersiveThemeRef means "never chose", which shows the Standard default.
    public int ImmersiveTheme { get; set; } = 0;
    // Game integration switches.
    public bool ShowDtrEntries { get; set; } = true;        // server-info bar entries (status, connections, bell, invites)
    public bool MenuViewProfile { get; set; } = true;       // right-click a player: View profile
    public bool MenuBookmarkProfile { get; set; } = true;   //                        Bookmark profile
    public bool MenuInviteToGroup { get; set; } = true;     //                        Invite to group
    public bool MenuOpenTrade { get; set; } = true;         //                        Open ARP trade
    // Where the viewer dragged the profile layout (pixels at scale 1, from its default spot) and how they resized the dossier panel.
    public float ImmersiveLayoutOffsetX { get; set; } = 0f;
    public float ImmersiveLayoutOffsetY { get; set; } = 0f;
    public float ImmersiveDossierScaleX { get; set; } = 1f;
    public float ImmersiveDossierScaleY { get; set; } = 1f;
    // Preferred theme as a reference ("builtin:0", "local:<id>", "server:<id>"). Takes precedence over ImmersiveTheme when set.
    public string ImmersiveThemeRef { get; set; } = "";
    // When true, a profile owner's chosen theme overrides the viewer's preference while viewing that profile. Always on: a profile's own theme wins; the viewer's pick below is the fallback for profiles that leave theirs on "viewer's choice".
    public bool ImmersiveUseProfileTheme { get => true; set { } }
    public string username { get; set; } = "";
    public string password { get; set; } = "";
    public bool rememberInformation { get; set; }

    // Visibility toggles for external link buttons on the main panel
    public bool showKofi { get; set; } = true;
    public bool showDisc { get; set; } = true;
    public bool showWeb { get; set; } = true;
    public bool showPatreon { get; set; } = true;

    // Tooltip settings - control what info shows when hovering over other players
    public bool tooltip_DutyDisabled { get; set; } = true;   // hide tooltip while in duties
    public bool tooltip_PvPDisabled { get; set; } = true;    // hide tooltip while in PvP
    public bool tooltip_Enabled { get; set; } = true;        // master toggle
    public bool tooltip_showAvatar { get; set; } = true;
    public bool tooltip_showName { get; set; } = true;
    public bool tooltip_showPersonalityTraits { get; set; } = true;
    public bool tooltip_showAlignment { get; set; } = true;
    public bool tooltip_showAge { get; set; } = true;
    public bool tooltip_showRace { get; set; } = true;
    public bool tooltip_showGender { get; set; } = true;
    public bool tooltip_showHeight { get; set; } = true;
    public bool tooltip_showWeight { get; set; } = true;
    public bool tooltip_showCustomTraits { get; set; } = true;
    public bool tooltip_ShowCustomDescriptors { get; set; } = true;
    public bool tooltip_draggable { get; set; } = true;      // can the user drag the tooltip around
    public int tooltip_PosX { get; set; }
    public int tooltip_PosY { get; set; }
    public bool tooltip_LockOnClick { get; set; } = false;   // pin tooltip in place when clicked
    public bool tooltip_HideInCombat { get; set; }

    // Hitbox overlay position offsets
    public float hPos {  get; set; }
    public float vPos { get; set; }

    // User-created icon categories for organizing custom icons
    public List<string> customIconCategory { get; set; } = new List<string>();

    // Whether to automatically log in when the plugin loads
    public bool autoLogin { get; set; }

    // Bookmarked icons the user has saved for quick access
    public List<IconData> iconBookmarks { get; set; } = new List<IconData>();

    // Chat groups the user is part of (each group has a name and channel list)
    public List<ARPChatGroups> ARPChatGroupMemberships { get; set; } = new List<ARPChatGroups>();

    // Default path for saving backup/export data
    public static string defaultDataPath = string.Empty;

    // Dalamud plugin interface ref - not serialized, set at runtime via Initialize()
    [NonSerialized]
    private IDalamudPluginInterface? PluginInterface;

    // Compass overlay settings
    public bool showCompass { get; set; } = false;
    internal bool showCompassInPvP { get; set; } = false;
    internal bool showCompassInDuty { get; set; } = false;
    public bool showCompassInCombat { get; set; } = false;

    // Automatic backup on profile save
    public bool AutobackupEnabled { get; set; } = true;
    public string dataSavePath { get; set; }

    // Compass display options
    public float fontSize { get; set; } = 35;
    public float CompassPosX { get; set; } = 0f;
    public float CompassPosY { get; set; } = 0f;

    // Theme color overrides - null means use the default theme colors
    public Vector4? ThemeBorder { get; set; }
    public Vector4? ThemeBackground { get; set; }
    public Vector4? ThemeAccent { get; set; }
    public Vector4? ThemeFont { get; set; }

    // Tracks which NSFW channels the user has agreed to view (persisted so they don't re-prompt)
    public HashSet<int> agreedNsfwChannelIds { get; set; } = new HashSet<int>();

    // Called once by Dalamud after deserializing the config from disk
    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        PluginInterface = pluginInterface;
    }

    // Writes the current config state to disk as JSON
    public void Save()
    {
        PluginInterface!.SavePluginConfig(this);
    }
}
