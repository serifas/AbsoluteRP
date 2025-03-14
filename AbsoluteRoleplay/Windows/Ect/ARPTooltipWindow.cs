using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using static Dalamud.Interface.Windowing.Window;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ImGuiFileDialog;
using ImGuiNET;
using static Lumina.Data.Files.ScdFile;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Gui;
using Dalamud.Game.ClientState;
using Networking;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Component.GUI;
using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Windows.Profiles.ProfileTabs;
using Dalamud.Interface.Utility;

namespace AbsoluteRoleplay.Windows.Ect
{
    public class ARPTooltipWindow : Window, IDisposable
    {
        public static bool isAdmin;
        public Configuration config;
        public static PlayerProfile profile;
        public string msg;
        public Vector2 windowPos;
        public bool openedProfile = false;
        public bool openedTargetProfile = false;
        public static IDalamudTextureWrap alignmentImg;
        public static IDalamudTextureWrap personality_1Img;
        public static IDalamudTextureWrap personality_2Img;
        public static IDalamudTextureWrap personality_3Img;
        public static IDalamudTextureWrap AlignmentImg;

        public Plugin plugin;
        internal static bool hasAlignment = false;
        internal static bool showPersonality1 = false;
        internal static bool showPersonality2 = false;
        internal static bool showPersonality3 = false;
        internal static bool showPersonalities = false;

        public static List<field> fields = new List<field>();
        public static List<descriptor> descriptors = new List<descriptor>();
        public static List<trait> personalities = new List<trait>();    
        public ARPTooltipWindow(Plugin plugin) : base(
       "TOOLTIP", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoNav |
                                              ImGuiWindowFlags.NoMouseInputs
                                              | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse |
                                              ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoFocusOnAppearing)
        {
            config = plugin.Configuration;
            this.plugin = plugin;
        }

        public override void Draw()
        {

            if (profile.title != string.Empty && profile.title != "New Profile") Misc.SetTitle(plugin, false, profile.title, profile.titleColor);
            if (config.tooltip_showAvatar) ImGui.Image(profile.avatar.ImGuiHandle, new Vector2(100, 100));
            if (config.tooltip_showName && profile.Name != string.Empty) ImGui.Text($"NAME: {profile.Name}");
            if (config.tooltip_showRace && profile.Race != string.Empty) ImGui.Text($"RACE: {profile.Race}");
            if (config.tooltip_showGender && profile.Gender != string.Empty) ImGui.Text($"GENDER: {profile.Gender}");
            if (config.tooltip_showAge && profile.Age != string.Empty) ImGui.Text($"AGE: {profile.Age}");
            if (config.tooltip_showHeight && profile.Height != string.Empty) ImGui.Text($"HEIGHT: {profile.Height}");
            if (config.tooltip_showWeight && profile.Weight != string.Empty) ImGui.Text($"WEIGHT: {profile.Weight}");
            foreach (descriptor descriptor in descriptors)
            {
                ImGui.Spacing();
                ImGui.Text(descriptor.name.ToUpper() + ": " + descriptor.description);
            }
            if (config.tooltip_showAlignment)
            {
                if (hasAlignment == true)
                {
                    ImGui.Text("ALIGNMENT:");
                    ImGui.Image(AlignmentImg.ImGuiHandle, new Vector2(32, 32));
                    ImGui.SameLine();
                    ImGui.Text(UI.AlignmentName(profile.Alignment));
                }
            }
            if (config.tooltip_showPersonalityTraits)
            {
                if (showPersonalities == true)
                {
                    ImGui.Text("TRAITS:");
                    if (showPersonality1 == true)
                    {
                        ImGui.Image(personality_1Img.ImGuiHandle, new Vector2(32, 42));
                        ImGui.SameLine();
                        ImGui.Text(UI.PersonalityNames(profile.Personality_1));
                    }
                    if (showPersonality2 == true)
                    {
                        ImGui.Image(personality_2Img.ImGuiHandle, new Vector2(32, 42));
                        ImGui.SameLine();
                        ImGui.Text(UI.PersonalityNames(profile.Personality_2));
                    }
                    if (showPersonality3 == true)
                    {
                        ImGui.Image(personality_3Img.ImGuiHandle, new Vector2(32, 42));
                        ImGui.SameLine();
                        ImGui.Text(UI.PersonalityNames(profile.Personality_3));
                    }


                }
                foreach (trait personality in personalities)
                {
                    ImGui.Image(personality.icon.icon.ImGuiHandle, new Vector2(32, 42));
                    ImGui.SameLine();
                    ImGui.Text(personality.name);
                }
            }
            if (config.tooltip_draggable)
            {
                if (Plugin.lockedtarget == false)
                {
                    windowPos = ImGui.GetMousePos();
                    ImGui.SetWindowPos(windowPos);
                }
            }
            else
            {
                var operations = new WindowOperations();
                var position = operations.CalculateTooltipPos();
                ImGui.SetWindowPos(position);
            }

        }





        public void Dispose()
        {

        }


    }
}
