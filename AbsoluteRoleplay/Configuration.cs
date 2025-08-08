using AbsoluteRoleplay.Defines;
using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Numerics;

namespace AbsoluteRoleplay;

[Serializable]
public class Configuration : IPluginConfiguration
{
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
    public bool showCompass { get; set; } = false;
    public List<string> customIconCategory { get; set; } = new List<string>();
    public bool autoLogin { get; set; }
    public List<IconData> iconBookmarks { get; set; } = new List<IconData>();
    public List<ChatChannelTabs> chatChannelTabs { get; set; } = new List<ChatChannelTabs>();
    public static string defaultDataPath = string.Empty;
    // the below exist just to make saving less cumbersome
    [NonSerialized]
    private IDalamudPluginInterface? PluginInterface;
    public bool AutobackupEnabled { get; set; } = true; 
    public string dataSavePath { get; set; }
    public float fontSize { get; set; } = 35;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        PluginInterface = pluginInterface;
    }
    public void Save()
    {
        PluginInterface!.SavePluginConfig(this);
    }
}
