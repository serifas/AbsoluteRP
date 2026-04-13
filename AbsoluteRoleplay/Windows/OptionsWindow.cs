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
    // Plugin settings window — tooltip visibility toggles, compass config, theme colors, backup paths
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
        public OptionsWindow() : base("OPTIONS")
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

            // Each preset: (name, accent color, border, font, isDefault)
            var presets = new (string Name, System.Numerics.Vector4 Accent, System.Numerics.Vector4 Border, System.Numerics.Vector4 Font)[]
            {
                ("Aether",      new(0.28f, 0.42f, 0.78f, 1f), new(0.16f, 0.16f, 0.22f, 0.65f), new(0.82f, 0.83f, 0.88f, 1f)),
                ("Void",        new(0.48f, 0.26f, 0.72f, 1f), new(0.25f, 0.18f, 0.38f, 0.65f), new(0.78f, 0.75f, 0.88f, 1f)),
                ("Dynamis",     new(0.40f, 0.82f, 0.65f, 1f), new(0.16f, 0.30f, 0.24f, 0.65f), new(0.78f, 0.90f, 0.84f, 1f)),
                ("Maelstrom",       new(0.70f, 0.18f, 0.22f, 1f), new(0.30f, 0.14f, 0.14f, 0.65f), new(0.85f, 0.78f, 0.78f, 1f)),
                ("Twin Adders",      new(0.78f, 0.62f, 0.18f, 1f), new(0.30f, 0.24f, 0.10f, 0.65f), new(0.90f, 0.84f, 0.68f, 1f)),
                ("Immortal Flames",  new(0.75f, 0.42f, 0.14f, 1f), new(0.30f, 0.20f, 0.14f, 0.65f), new(0.88f, 0.82f, 0.75f, 1f)),
                ("Temple Knights",   new(0.14f, 0.48f, 0.72f, 1f), new(0.14f, 0.22f, 0.30f, 0.65f), new(0.75f, 0.82f, 0.90f, 1f)),
            };

            // Detect which preset is currently active by matching accent color
            var currentAccent = config.ThemeAccent ?? ThemeManager.DefaultAccent;
            int activePreset = -1;
            if (config.ThemeAccent == null && config.ThemeBorder == null && config.ThemeFont == null)
                activePreset = 0; // Aetherial Sea (default)
            else
            {
                for (int i = 0; i < presets.Length; i++)
                {
                    var pa = presets[i].Accent;
                    if (Math.Abs(currentAccent.X - pa.X) < 0.02f &&
                        Math.Abs(currentAccent.Y - pa.Y) < 0.02f &&
                        Math.Abs(currentAccent.Z - pa.Z) < 0.02f)
                    {
                        activePreset = i;
                        break;
                    }
                }
            }

            // Draw preset buttons — colored by their accent, white outline when active
           
            for (int i = 0; i < presets.Length; i++)
            {
                //pop down at the fourth button
                if(i != 3)
                {
                    ImGui.SameLine();
                }

                var p = presets[i];
                bool isActive = (activePreset == i);
                var darkAccent = new System.Numerics.Vector4(p.Accent.X * 0.6f, p.Accent.Y * 0.6f, p.Accent.Z * 0.6f, 1f);

                // Button background = darkened accent, hover = full accent
                ImGui.PushStyleColor(ImGuiCol.Button, darkAccent);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, p.Accent);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, p.Accent);
                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1, 1, 1, 1));
                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 20f);
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(16, 6));

                // White outline for the active preset
                if (isActive)
                {
                    ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 2f);
                    ImGui.PushStyleColor(ImGuiCol.Border, new System.Numerics.Vector4(1, 1, 1, 1));
                }

                bool clicked = ImGui.Button(p.Name);

                if (isActive)
                {
                    ImGui.PopStyleColor(1);
                    ImGui.PopStyleVar(1);
                }

                ImGui.PopStyleVar(2);
                ImGui.PopStyleColor(4);

                if (clicked)
                {
                    if (i == 0)
                    {
                        // Default — clear overrides
                        config.ThemeBorder = null;
                        config.ThemeBackground = null;
                        config.ThemeAccent = null;
                        config.ThemeFont = null;
                    }
                    else
                    {
                        config.ThemeBorder = p.Border;
                        config.ThemeBackground = new System.Numerics.Vector4(0f, 0f, 0f, 0.969f);
                        config.ThemeAccent = p.Accent;
                        config.ThemeFont = p.Font;
                    }
                    config.Save();
                }
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
