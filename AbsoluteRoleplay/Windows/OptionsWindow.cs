using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using System;
using Dalamud.Interface.GameFonts;
using AbsoluteRoleplay.Helpers;
namespace AbsoluteRoleplay.Windows
{
    public class OptionsWindow : Window, IDisposable
    {
        private float _modVersionWidth;
        public static Plugin plugin;
        public static bool showTargetOptions;
        public static bool showKofi;
        public static bool showDisc;
        public static bool showWIP;
        public Configuration Configuration;
        public static bool closeAfterConnection;

        public OptionsWindow(Plugin plugin) : base(
       "OPTIONS", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(300, 180),
                MaximumSize = new Vector2(800, 800)
            };
            OptionsWindow.plugin = plugin;
            showWIP = plugin.Configuration.showWIP;
            showKofi = plugin.Configuration.showKofi;
            showDisc = plugin.Configuration.showDisc;
        }
        public override void Draw()
        {
            Misc.SetTitle(plugin, false, "Options");
            //okay that's done.
            ImGui.Spacing();
            //now for some simple toggles
            Configuration = plugin.Configuration;


            ImGui.BeginTabBar("MouseTargetTooltipOptions");
            if (ImGui.BeginTabItem("General"))
            {
                if (ImGui.Checkbox("Show Ko-fi Button", ref showKofi))
                {
                    plugin.Configuration.showKofi = showKofi;
                    plugin.Configuration.Save();
                }
                if (ImGui.Checkbox("Show Discord Button.", ref showDisc))
                {
                    plugin.Configuration.showDisc = showDisc;
                    plugin.Configuration.Save();
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
                   
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.Begin("FadStartTooltip", ImGuiWindowFlags.Tooltip | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar);
                        ImGui.Text("Number of seconds to start fading");
                        ImGui.End();
                    }
                  
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.Begin("FadeFinsihTooltip", ImGuiWindowFlags.Tooltip | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar);
                        ImGui.Text("Number of seconds for fade to finish");
                        ImGui.End();
                    }
                }
                if(ImGui.CollapsingHeader("Display"))
                {
                    var showTooltip = Configuration.tooltip_Enabled;
                    if (ImGui.Checkbox("Show Tooltip", ref showTooltip))
                    {
                        Configuration.tooltip_Enabled = showTooltip;
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
                        plugin.Configuration.tooltip_showRace = showRace;
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
                }

                ImGui.EndTabItem();
            }
           
            ImGui.EndTabBar();

        }
        public void Dispose()
        {

        }
    }

}
