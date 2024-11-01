using Dalamud.Configuration;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Numerics;

namespace AbsoluteRoleplay;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public bool AlwaysOpenDefaultImport { get; set; } = false;
    public string username { get; set; } = "";
    public string password { get; set; } = "";
    public bool rememberInformation { get; set; }
    internal bool autologin { get; set; } = false;
    //Config options
    public bool showKofi { get; set; } = true;
    public bool showWIP { get; set; } = true;
    public bool showDisc { get; set; } = true;

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
    public bool tooltip_showHasBio { get; set; } = true;
    public bool tooltip_showHasHooks { get; set; }= true;
    public bool tooltip_showHasStory { get; set; } = true;
    public bool tooltip_showHasOOC { get; set; } = true;
    public bool tooltip_showHasGallery { get; set; } = true;
    public bool tooltip_draggable { get; set; } = true;
    public int tooltip_PosX { get; set; }
    public int tooltip_PosY { get; set; }
    public bool tooltip_LockOnClick { get; set; } = false;
    public bool tooltip_HideInCombat { get; set; }
    public int alert_position { get; set; } = (int)Defines.AlertPositions.BottomRight;

    public float hPos {  get; set; }
    public float vPos { get; set; }




    // the below exist just to make saving less cumbersome
    [NonSerialized]
    private IDalamudPluginInterface? PluginInterface;

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
