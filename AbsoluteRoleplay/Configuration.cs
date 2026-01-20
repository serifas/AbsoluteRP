using AbsoluteRP.Defines;
using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Numerics;
using System.Security.Principal;

namespace AbsoluteRP;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public Account account { get; set; } = new Account()
    {
        accountKey = string.Empty,
        accountName = string.Empty
    };
    public List<Character> characters { get; set; } = new List<Character>();
    public int uiScale { get; set; }
    public int Version { get; set; } = 0;
    public Version TOSVersion { get; set; } = new Version("0.0.0.0");
    public bool AlwaysOpenDefaultImport { get; set; } = false;
    public string username { get; set; } = "";
    public string password { get; set; } = "";
    public bool rememberInformation { get; set; }

    //Config options
    public bool showKofi { get; set; } = true;
    public bool showDisc { get; set; } = true;
    //public bool showWeb { get; set; } = true;
    public bool showPatreon { get; set; } = true;
    public bool tooltip_DutyDisabled { get; set; } = true;
    public bool tooltip_PvPDisabled { get; set; } = true;
    public bool tooltip_Enabled { get; set; } = true;
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
    public bool tooltip_draggable { get; set; } = true;
    public int tooltip_PosX { get; set; }
    public int tooltip_PosY { get; set; }
    public bool tooltip_LockOnClick { get; set; } = false;
    public bool tooltip_HideInCombat { get; set; }
    public float hPos {  get; set; }
    public float vPos { get; set; }
    public List<string> customIconCategory { get; set; } = new List<string>();
    public bool autoLogin { get; set; }
    public List<IconData> iconBookmarks { get; set; } = new List<IconData>();
    public List<ARPChatGroups> ARPChatGroupMemberships { get; set; } = new List<ARPChatGroups>();
    public static string defaultDataPath = string.Empty;
    // the below exist just to make saving less cumbersome
    [NonSerialized]
    private IDalamudPluginInterface? PluginInterface;
    public bool showCompass { get; set; } = false;
    internal bool showCompassInPvP { get; set; } = false;
    internal bool showCompassInDuty { get; set; } = false;
    public bool showCompassInCombat { get; set; } = false;
    public bool AutobackupEnabled { get; set; } = true; 
    public bool displayFauxNamesInCombat { get; set; } = false;
    public bool displayFauxNamesInPvP { get; set; } = false;
    public bool displayFauxnamesInDuty { get; set; } = false;
    public string dataSavePath { get; set; }
    public float fontSize { get; set; } = 35; 
    public float CompassPosX { get; set; } = 0f;
    public float CompassPosY { get; set; } = 0f;

    // NSFW channel agreements - persisted so users don't have to agree every session
    public HashSet<int> agreedNsfwChannelIds { get; set; } = new HashSet<int>();

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        PluginInterface = pluginInterface;
    }
    public void Save()
    {
        PluginInterface!.SavePluginConfig(this);
    }
}
