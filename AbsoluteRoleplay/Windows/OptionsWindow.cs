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
        public static bool showWeb;
        public Configuration Configuration;
        public static FileDialogManager _fileDialogManager; //for avatars only at the moment
        public static bool autoLogIn;
        public static string[] alertPositions = { "Bottom Left", "Bottom Right", "Top Left", "Top Right", "Center" };
        public static bool ChangeDataPath = false;
        private bool showCompass;
        private bool showCompassInCombat;
        private bool showCompassInDuty;
        private bool showCompassInPvP;
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
            showWeb = Plugin.plugin.Configuration.showWeb;
            autoLogin = Plugin.plugin.Configuration.autoLogin;
            showCompass = Plugin.plugin.Configuration.showCompass;
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
                    if (ImGui.Checkbox("Show Website Button.", ref showWeb))
                    {
                        Plugin.plugin.Configuration.showWeb = showWeb;
                        Plugin.plugin.Configuration.Save();
                    }
                
                    //DrawAlertOptions();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Compass"))
                {
                    showCompass = Plugin.plugin.Configuration.showCompass;
                    if (ImGui.Checkbox("Show Compass", ref showCompass))
                    {
                        Plugin.plugin.Configuration.showCompass = showCompass;
                        Plugin.plugin.Configuration.Save();
                    }
                    showCompassInCombat = Plugin.plugin.Configuration.showCompassInCombat;
                    if (ImGui.Checkbox("Show Compass in Combat", ref showCompassInCombat))
                    {
                        Plugin.plugin.Configuration.showCompassInCombat = showCompassInCombat;
                        Plugin.plugin.Configuration.Save();
                    }
                    showCompassInDuty = Plugin.plugin.Configuration.showCompassInDuty;
                    if (ImGui.Checkbox("Show Compass in Duty", ref showCompassInDuty))
                    {
                        Plugin.plugin.Configuration.showCompassInDuty = showCompassInDuty;
                        Plugin.plugin.Configuration.Save();
                    }
                    showCompassInPvP = Plugin.plugin.Configuration.showCompassInPvP;
                    if (ImGui.Checkbox("Show Compass in PvP", ref showCompassInPvP))
                    {
                        Plugin.plugin.Configuration.showCompassInPvP = showCompassInPvP;
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
                    if(ThemeManager.GhostButton("Change Path"))
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
                        if (ImGui.Checkbox("Draggable (Follows cursor)", ref movable))
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

                if (ImGui.BeginTabItem("Theme"))
                {
                    DrawThemeTab();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("OptionsWindow Draw Debug: " + ex.Message);
            }
        }
        private void DrawThemeTab()
        {
            var config = Plugin.plugin.Configuration;

            ThemeManager.SectionHeader("Theme Customization");
            ThemeManager.SubtitleText("Customize the look of all Absolute Roleplay windows. Changes preview live.");
            ImGui.Spacing();

            ThemeManager.GradientSeparator();

            // Color pickers in a card
            if (ThemeManager.BeginCard("##colorpickers", new System.Numerics.Vector2(0, 160)))
            {
                ImGui.Spacing();

                // Border color
                var border = config.ThemeBorder ?? ThemeManager.DefaultBorder;
                if (ThemeManager.DrawColorPicker("Border", ref border, ThemeManager.DefaultBorder))
                {
                    config.ThemeBorder = border;
                    config.Save();
                }

                // Background color
                var bg = config.ThemeBackground ?? ThemeManager.DefaultBackground;
                if (ThemeManager.DrawColorPicker("Background", ref bg, ThemeManager.DefaultBackground))
                {
                    config.ThemeBackground = bg;
                    config.Save();
                }

                // Accent color
                var accent = config.ThemeAccent ?? ThemeManager.DefaultAccent;
                if (ThemeManager.DrawColorPicker("Accent", ref accent, ThemeManager.DefaultAccent))
                {
                    config.ThemeAccent = accent;
                    config.Save();
                }

                // Font color
                var font = config.ThemeFont ?? ThemeManager.DefaultFont;
                if (ThemeManager.DrawColorPicker("Font Color", ref font, ThemeManager.DefaultFont))
                {
                    config.ThemeFont = font;
                    config.Save();
                }

                ImGui.Spacing();
            }
            ThemeManager.EndCard();

            ImGui.Spacing();
            ThemeManager.SectionHeader("Presets");
            ImGui.Spacing();

            // Preset buttons using pill style
            if (ThemeManager.PillButton("Default Dark"))
            {
                config.ThemeBorder = null;
                config.ThemeBackground = null;
                config.ThemeAccent = null;
                config.ThemeFont = null;
                config.Save();
            }

            ImGui.SameLine();
            if (ThemeManager.PillButton("Deep Purple", primary: false))
            {
                config.ThemeBorder = new System.Numerics.Vector4(0.35f, 0.25f, 0.50f, 0.65f);
                config.ThemeBackground = new System.Numerics.Vector4(0f, 0f, 0f, 0.969f);
                config.ThemeAccent = new System.Numerics.Vector4(0.60f, 0.35f, 0.85f, 1.00f);
                config.ThemeFont = new System.Numerics.Vector4(0.88f, 0.85f, 0.95f, 1.00f);
                config.Save();
            }

            ImGui.SameLine();
            if (ThemeManager.PillButton("Crimson", primary: false))
            {
                config.ThemeBorder = new System.Numerics.Vector4(0.40f, 0.20f, 0.20f, 0.65f);
                config.ThemeBackground = new System.Numerics.Vector4(0f, 0f, 0f, 0.969f);
                config.ThemeAccent = new System.Numerics.Vector4(0.85f, 0.25f, 0.30f, 1.00f);
                config.ThemeFont = new System.Numerics.Vector4(0.95f, 0.88f, 0.88f, 1.00f);
                config.Save();
            }

            if (ThemeManager.PillButton("Ocean", primary: false))
            {
                config.ThemeBorder = new System.Numerics.Vector4(0.20f, 0.30f, 0.40f, 0.65f);
                config.ThemeBackground = new System.Numerics.Vector4(0f, 0f, 0f, 0.969f);
                config.ThemeAccent = new System.Numerics.Vector4(0.20f, 0.60f, 0.85f, 1.00f);
                config.ThemeFont = new System.Numerics.Vector4(0.85f, 0.92f, 0.98f, 1.00f);
                config.Save();
            }

            ImGui.SameLine();
            if (ThemeManager.PillButton("Emerald", primary: false))
            {
                config.ThemeBorder = new System.Numerics.Vector4(0.20f, 0.35f, 0.25f, 0.65f);
                config.ThemeBackground = new System.Numerics.Vector4(0f, 0f, 0f, 0.969f);
                config.ThemeAccent = new System.Numerics.Vector4(0.25f, 0.75f, 0.45f, 1.00f);
                config.ThemeFont = new System.Numerics.Vector4(0.88f, 0.95f, 0.90f, 1.00f);
                config.Save();
            }

            ImGui.SameLine();
            if (ThemeManager.PillButton("Sunset", primary: false))
            {
                config.ThemeBorder = new System.Numerics.Vector4(0.40f, 0.28f, 0.20f, 0.65f);
                config.ThemeBackground = new System.Numerics.Vector4(0f, 0f, 0f, 0.969f);
                config.ThemeAccent = new System.Numerics.Vector4(0.90f, 0.55f, 0.20f, 1.00f);
                config.ThemeFont = new System.Numerics.Vector4(0.98f, 0.92f, 0.85f, 1.00f);
                config.Save();
            }

            if (ThemeManager.PillButton("Rose Gold", primary: false))
            {
                config.ThemeBorder = new System.Numerics.Vector4(0.38f, 0.25f, 0.30f, 0.65f);
                config.ThemeBackground = new System.Numerics.Vector4(0f, 0f, 0f, 0.969f);
                config.ThemeAccent = new System.Numerics.Vector4(0.85f, 0.45f, 0.55f, 1.00f);
                config.ThemeFont = new System.Numerics.Vector4(0.96f, 0.90f, 0.91f, 1.00f);
                config.Save();
            }

            ImGui.SameLine();
            if (ThemeManager.PillButton("Midnight", primary: false))
            {
                config.ThemeBorder = new System.Numerics.Vector4(0.18f, 0.18f, 0.28f, 0.65f);
                config.ThemeBackground = new System.Numerics.Vector4(0f, 0f, 0f, 0.969f);
                config.ThemeAccent = new System.Numerics.Vector4(0.45f, 0.45f, 0.90f, 1.00f);
                config.ThemeFont = new System.Numerics.Vector4(0.82f, 0.82f, 0.95f, 1.00f);
                config.Save();
            }

            ImGui.SameLine();
            if (ThemeManager.PillButton("Frost", primary: false))
            {
                config.ThemeBorder = new System.Numerics.Vector4(0.30f, 0.38f, 0.42f, 0.65f);
                config.ThemeBackground = new System.Numerics.Vector4(0f, 0f, 0f, 0.969f);
                config.ThemeAccent = new System.Numerics.Vector4(0.50f, 0.80f, 0.95f, 1.00f);
                config.ThemeFont = new System.Numerics.Vector4(0.88f, 0.94f, 0.98f, 1.00f);
                config.Save();
            }

            ImGui.Spacing();
            ThemeManager.GradientSeparator();

            // Widget preview section
            ThemeManager.SectionHeader("Preview");
            ImGui.Spacing();

            if (ThemeManager.BeginCard("##preview", new System.Numerics.Vector2(0, 140)))
            {
                // Show sample widgets so user can preview theme in real time
                ThemeManager.StatusDot("Online", ThemeManager.Success);
                ImGui.SameLine();
                ImGui.Dummy(new System.Numerics.Vector2(12, 0));
                ImGui.SameLine();
                ThemeManager.StatusDot("Away", ThemeManager.Warning);
                ImGui.SameLine();
                ImGui.Dummy(new System.Numerics.Vector2(12, 0));
                ImGui.SameLine();
                ThemeManager.StatusDot("Offline", ThemeManager.FontDim);

                ImGui.Spacing();

                ThemeManager.Badge("Verified");
                ImGui.SameLine();
                ThemeManager.Badge("Premium", ThemeManager.Warning);
                ImGui.SameLine();
                ThemeManager.Badge("New", ThemeManager.Success);

                ImGui.Spacing();

                ThemeManager.StyledProgressBar(0.65f, new System.Numerics.Vector2(-1, 16), "65% Complete");

                ImGui.Spacing();

                ThemeManager.PillButton("Primary");
                ImGui.SameLine();
                ThemeManager.GhostButton("Secondary");
                ImGui.SameLine();
                ThemeManager.DangerButton("Delete");
            }
            ThemeManager.EndCard();
        }

        public void Dispose()
        {

        }
      
    }

}
