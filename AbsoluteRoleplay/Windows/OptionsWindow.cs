using AbsoluteRP.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Common.Math;
using System;
namespace AbsoluteRP.Windows
{
    public class OptionsWindow : Window, IDisposable
    {
        public static bool showTargetOptions;
        public static bool autoLogin;
        public static bool showKofi;
        public static bool showPatreon;
        public static bool showDisc;
        public Configuration Configuration;
        public static FileDialogManager _fileDialogManager; //for avatars only at the moment
        public static bool autoLogIn;
        public static string[] alertPositions = { "Bottom Left", "Bottom Right", "Top Left", "Top Right", "Center" };
        public static bool ChangeDataPath = false;
        private bool showCompass;
        private bool showCompassInCombat;

        public OptionsWindow() : base("OPTIONS", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(300, 180),
                MaximumSize = new Vector2(800, 800)
            };
            showKofi = Plugin.plugin.Configuration.showKofi;
            showPatreon = Plugin.plugin.Configuration.showPatreon;
            showDisc = Plugin.plugin.Configuration.showDisc;
            autoLogin = Plugin.plugin.Configuration.autoLogin;
           // showCompass = Plugin.plugin.Configuration.showCompass;
            _fileDialogManager = new FileDialogManager();
        }
        public override void Draw()
        {
            try
            {
                _fileDialogManager.Draw();
                if (ChangeDataPath)
                {
                    Plugin.PluginLog.Debug("Dialog Opening");
                    _fileDialogManager.OpenFolderDialog("Select Data Save Path", (b, path) =>
                    {
                        if(path != null && b)
                        {
                            Plugin.plugin.Configuration.dataSavePath = path;
                            Plugin.plugin.Configuration.Save();
                            Plugin.PluginLog.Debug($"Data save path changed to: {path}");
                        }
                        else
                        {
                            Plugin.PluginLog.Debug("Data save path change cancelled.");
                        }
                    });
                    ChangeDataPath = false; // Prevent repeated dialog opening
                }
                Misc.SetTitle(Plugin.plugin, false, "Options", ImGuiColors.TankBlue);
                //okay that's done.
                ImGui.Spacing();
                //now for some simple toggles
                Configuration = Plugin.plugin.Configuration;



                ImGui.BeginTabBar("MouseTargetTooltipOptions");

                if (ImGui.BeginTabItem("General"))
                {
                    if (ImGui.Checkbox("Show Ko-fi Button", ref showKofi))
                    {
                        Plugin.plugin.Configuration.showKofi = showKofi;
                        Plugin.plugin.Configuration.Save();
                    }
                    if (ImGui.Checkbox("Show Patreon Button.", ref showPatreon))
                    {
                        Plugin.plugin.Configuration.showPatreon = showPatreon;
                        Plugin.plugin.Configuration.Save();
                    }
                    if (ImGui.Checkbox("Show Discord Button.", ref showDisc))
                    {
                        Plugin.plugin.Configuration.showDisc = showDisc;
                        Plugin.plugin.Configuration.Save();
                    }
                    //DrawAlertOptions();
                    ImGui.EndTabItem();
                }
                
                if (ImGui.BeginTabItem("Compass"))
                {
                    if (ImGui.Checkbox("Show Compass", ref showCompass))
                    {
                        Plugin.plugin.Configuration.showCompass = showCompass;
                        Plugin.plugin.Configuration.Save();
                    }
                    if (ImGui.Checkbox("Show Compass in Combat", ref showCompassInCombat))
                    {
                        Plugin.plugin.Configuration.showCompassInCombat = showCompassInCombat;
                        Plugin.plugin.Configuration.Save();
                    }

                    //DrawAlertOptions();
                    ImGui.EndTabItem();
                }
                
                
                if (ImGui.BeginTabItem("Data"))
                {
                    bool autoBackup = Plugin.plugin.Configuration.AutobackupEnabled;
                    if(ImGui.Checkbox("##AutoBackup", ref autoBackup))
                    {
                        Plugin.plugin.Configuration.AutobackupEnabled = autoBackup;
                        Plugin.plugin.Configuration.Save();
                    }
                    if(ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Enable or disable automatic local backup. Disabling will save you disk space but possibly result in data loss. \n (Your data is still stored on the server.) ");
                    }
                    ImGui.SameLine();
                    ImGui.Text("Enable Auto Backup");
                    string customPath = Plugin.plugin.Configuration.dataSavePath ?? string.Empty;
                    ImGui.Text($"Auto Backup Dir: {customPath}" );
                    if(ImGui.Button("Change Path"))
                    {
                        ChangeDataPath = true;
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("This is the path where your automatic character backup data will be saved.");
                    }
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Player Tooltips"))
                {
                    if (ImGui.CollapsingHeader("Behavior"))
                    {
                        var movable = Configuration.tooltip_draggable;
                        if (ImGui.Checkbox("Draggable", ref movable))
                        {
                            Configuration.tooltip_draggable = movable;
                            Configuration.Save();
                        }
                        var locked = Configuration.tooltip_LockOnClick;
                        if (ImGui.Checkbox("Disable Dragging on Target Select", ref locked))
                        {
                            Configuration.tooltip_LockOnClick = locked;
                            Configuration.Save();
                        }

                        DrawTooltipPositionSliders();
                    }
                    if (ImGui.CollapsingHeader("Display"))
                    {
                        var showTooltip = Configuration.tooltip_Enabled;
                        if (ImGui.Checkbox("Show Tooltip", ref showTooltip))
                        {
                            Configuration.tooltip_Enabled = showTooltip;
                            Configuration.Save();
                        }
                        var hideTooltipInCombat = Configuration.tooltip_HideInCombat;
                        if (ImGui.Checkbox("Hide in Combat", ref hideTooltipInCombat))
                        {
                            Configuration.tooltip_HideInCombat = hideTooltipInCombat;
                            Configuration.Save();
                        }
                        ImGui.SameLine();
                        var hideTooltipInDuty = Configuration.tooltip_DutyDisabled;
                        if (ImGui.Checkbox("Hide in Duty", ref hideTooltipInDuty))
                        {
                            Configuration.tooltip_DutyDisabled = hideTooltipInDuty;
                            Configuration.Save();
                        }
                        ImGui.SameLine();
                        var hideTooltipInPvP = Configuration.tooltip_PvPDisabled;
                        if (ImGui.Checkbox("Hide in PvP", ref hideTooltipInPvP))
                        {
                            Configuration.tooltip_PvPDisabled = hideTooltipInPvP;
                            Configuration.Save();
                        }

                        var showAvatar = Configuration.tooltip_showAvatar;
                        if (ImGui.Checkbox("Show Avatar", ref showAvatar))
                        {
                            Configuration.tooltip_showAvatar = showAvatar;
                            Configuration.Save();
                        }
                        var showName = Configuration.tooltip_showName;
                        if (ImGui.Checkbox("Show Name", ref showName))
                        {
                            Configuration.tooltip_showName = showName;
                            Configuration.Save();
                        }
                        ImGui.SameLine();
                        var showRace = Configuration.tooltip_showRace;
                        if (ImGui.Checkbox("Show Race", ref showRace))
                        {
                            Plugin.plugin.Configuration.tooltip_showRace = showRace;
                            Configuration.Save();
                        }
                        ImGui.SameLine();
                        var showGender = Configuration.tooltip_showGender;
                        if (ImGui.Checkbox("Show Gender", ref showGender))
                        {
                            Configuration.tooltip_showGender = showGender;
                            Configuration.Save();
                        }
                        ImGui.SameLine();
                        var showAge = Configuration.tooltip_showAge;
                        if (ImGui.Checkbox("Show Age", ref showAge))
                        {
                            Configuration.tooltip_showAge = showAge;
                            Configuration.Save();
                        }
                        var showHeight = Configuration.tooltip_showHeight;
                        if (ImGui.Checkbox("Show Height", ref showHeight))
                        {
                            Configuration.tooltip_showHeight = showHeight;
                            Configuration.Save();
                        }
                        ImGui.SameLine();
                        var showWeight = Configuration.tooltip_showWeight;
                        if (ImGui.Checkbox("Show Weight", ref showWeight))
                        {
                            Configuration.tooltip_showWeight = showWeight;
                            Configuration.Save();
                        }
                        var showAlignment = Configuration.tooltip_showAlignment;
                        if (ImGui.Checkbox("Show Alignment", ref showAlignment))
                        {
                            Configuration.tooltip_showAlignment = showAlignment;
                            Configuration.Save();
                        }
                        ImGui.SameLine();
                        var showPersonality = Configuration.tooltip_showPersonalityTraits;
                        if (ImGui.Checkbox("Show Personality", ref showPersonality))
                        {
                            Configuration.tooltip_showPersonalityTraits = showPersonality;
                            Configuration.Save();
                        }
                        var showCustomTraits = Configuration.tooltip_showCustomTraits;  
                        if (ImGui.Checkbox("Show Custom Traits", ref showCustomTraits))
                        {
                            Configuration.tooltip_showCustomTraits = showCustomTraits;
                            Configuration.Save();
                        }
                    }

                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("OptionsWindow Draw Debug: " + ex.Message);
            }
        }
        public void Dispose()
        {

        }
        public void DrawTooltipPositionSliders()
        {
            var hPos = Configuration.hPos;
            var vPos = Configuration.vPos;

            var viewport = ImGui.GetMainViewport();
            float maxHVal = viewport.WorkSize.X - 100;
            float maxVVal = viewport.WorkSize.Y - 100;

            ImGui.Text("Static position: (Only works if draggable is disabled)");
            if (ImGui.SliderFloat("Horizontal Position", ref hPos, 0, maxHVal))
            {
                Configuration.hPos = hPos;
                Configuration.Save();
            }
            if (ImGui.SliderFloat("Vertical Position", ref vPos, 0, maxVVal))
            {
                Configuration.vPos = vPos;
                Configuration.Save();
            }
        }
    }

}
