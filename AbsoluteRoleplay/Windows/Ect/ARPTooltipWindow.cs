using Dalamud.Interface.Windowing;
using System.Numerics;
using Dalamud.Interface.Textures.TextureWraps;
using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using Dalamud.Bindings.ImGui;

namespace AbsoluteRoleplay.Windows.Ect
{
    public class ARPTooltipWindow : Window, IDisposable
    {
        public static bool isAdmin;
        public Configuration config;
        public static ProfileData profile;
        public string msg;
        public Vector2 windowPos;
        public bool openedProfile = false;
        public bool openedTargetProfile = false;
        public static IDalamudTextureWrap alignmentImg;
        public static IDalamudTextureWrap personality_1Img;
        public static IDalamudTextureWrap personality_2Img;
        public static IDalamudTextureWrap personality_3Img;
        public static IDalamudTextureWrap AlignmentImg;

        internal static bool hasAlignment = false;
        internal static bool showPersonality1 = false;
        internal static bool showPersonality2 = false;
        internal static bool showPersonality3 = false;
        internal static bool showPersonalities = false;

        public static List<field> fields = new List<field>();
        public static List<descriptor> descriptors = new List<descriptor>();
        public static List<trait> personalities = new List<trait>();    
        public ARPTooltipWindow() : base(
       "TOOLTIP", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoNav
                                              | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse |
                                              ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoFocusOnAppearing)
        {

            SizeConstraints = new WindowSizeConstraints
            {

                MinimumSize = new Vector2(50, 50),
                MaximumSize = new Vector2(300, 1000)
            };
            config = Plugin.plugin.Configuration;

        }

        public override void Draw()
        {
            try
            {
                if (profile.title != string.Empty && profile.title != "New Profile") Misc.SetTitle(Plugin.plugin, false, profile.title, profile.titleColor);
                if (config.tooltip_showAvatar) ImGui.Image(profile.avatar.Handle, new Vector2(100, 100));
                if (config.tooltip_showName && profile.Name != string.Empty) ImGui.Text("NAME: "); ImGui.SameLine(); Misc.RenderHtmlColoredTextInline(profile.Name, 400);
                if (config.tooltip_showRace && profile.Race != string.Empty) ImGui.Text($"RACE: "); ImGui.SameLine(); Misc.RenderHtmlColoredTextInline(profile.Race, 400);
                if (config.tooltip_showGender && profile.Gender != string.Empty) ImGui.Text($"GENDER: "); ImGui.SameLine(); Misc.RenderHtmlColoredTextInline(profile.Gender, 400);
                if (config.tooltip_showAge && profile.Age != string.Empty) ImGui.Text($"AGE: "); ImGui.SameLine(); Misc.RenderHtmlColoredTextInline(profile.Age, 400);
                if (config.tooltip_showHeight && profile.Height != string.Empty) ImGui.Text($"HEIGHT:  "); ImGui.SameLine(); Misc.RenderHtmlColoredTextInline(profile.Height, 400);
                if (config.tooltip_showWeight && profile.Weight != string.Empty) ImGui.Text($"WEIGHT: "); ImGui.SameLine(); Misc.RenderHtmlColoredTextInline(profile.Weight, 400);
                if (config.tooltip_ShowCustomDescriptors && descriptors != null)
                {
                    foreach (descriptor descriptor in descriptors)
                    {
                        ImGui.Spacing();
                        ImGui.Text(descriptor.name?.ToUpper() + ": ");
                        ImGui.SameLine();
                        Misc.RenderHtmlColoredTextInline(descriptor.description ?? string.Empty, 400);
                    }
                }

                if (config.tooltip_showAlignment)
                {
                    if (hasAlignment && AlignmentImg != null)
                    {
                        ImGui.Text("ALIGNMENT:");
                        ImGui.Image(AlignmentImg.Handle, new Vector2(32, 32));
                        ImGui.SameLine();
                        Misc.RenderHtmlColoredTextInline(UI.AlignmentName(profile.Alignment), 400);
                    }
                }

                if (config.tooltip_showPersonalityTraits)
                {
                    if (showPersonalities)
                    {
                        ImGui.Text("TRAITS:");
                        if (showPersonality1 && personality_1Img != null)
                        {
                            ImGui.Image(personality_1Img.Handle, new Vector2(32, 42));
                            ImGui.SameLine();
                            Misc.RenderHtmlColoredTextInline(UI.PersonalityNames(profile.Personality_1), 400);
                        }
                        if (showPersonality2 && personality_2Img != null)
                        {
                            ImGui.Image(personality_2Img.Handle, new Vector2(32, 42));
                            ImGui.SameLine();
                            Misc.RenderHtmlColoredTextInline(UI.PersonalityNames(profile.Personality_2), 400);
                        }
                        if (showPersonality3 && personality_3Img != null)
                        {
                            ImGui.Image(personality_3Img.Handle, new Vector2(32, 42));
                            ImGui.SameLine();
                            Misc.RenderHtmlColoredTextInline(UI.PersonalityNames(profile.Personality_3), 400);
                        }
                    }
                    if (config.tooltip_showCustomTraits && personalities != null)
                    {
                        foreach (trait personality in personalities)
                        {
                            if (personality?.icon?.icon != null)
                                ImGui.Image(personality.icon.icon.Handle, new Vector2(32, 42));
                            ImGui.SameLine();
                            Misc.RenderHtmlColoredTextInline(personality.name ?? string.Empty, 400);
                        }
                    }
                }
                if (config.tooltip_draggable)
                {
                    if (Plugin.lockedtarget == false)
                    {
                        windowPos = new Vector2(ImGui.GetMousePos().X + 20, ImGui.GetMousePos().Y + 20);
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
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("ARPTooltipWindow Draw Debug: " + ex.Message);
            }
        }



        public void Dispose()
        {
            try
            {
                WindowOperations.SafeDispose(alignmentImg);
                alignmentImg = null;
                WindowOperations.SafeDispose(personality_1Img);
                personality_1Img = null;
                WindowOperations.SafeDispose(personality_2Img);
                personality_2Img = null;
                WindowOperations.SafeDispose(personality_3Img);
                personality_3Img = null;
                foreach (trait personality in personalities)
                {
                    WindowOperations.SafeDispose(personality.icon.icon);
                    personality.icon.icon = null;
                }
                // If you have other IDisposable fields, dispose them here with null checks
                // If you have lists of IDisposable, iterate and dispose each with null checks
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("ARPTooltipWindow Dispose Debug: " + ex.Message);
            }
        }


    }
}
