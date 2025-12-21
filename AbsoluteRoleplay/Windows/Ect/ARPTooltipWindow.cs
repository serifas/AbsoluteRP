using Dalamud.Interface.Windowing;
using System.Numerics;
using Dalamud.Interface.Textures.TextureWraps;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using Dalamud.Bindings.ImGui;

namespace AbsoluteRP.Windows.Ect
{
    public class ARPTooltipWindow : Window, IDisposable
    {
        public static bool isAdmin;
        public Configuration config;
        public static TooltipData tooltipData;
        public string msg;
        public Vector2 windowPos;
        public bool openedProfile = false;
        public bool openedTargetProfile = false;

        internal static bool hasAlignment = false;
        internal static bool showPersonality1 = false;
        internal static bool showPersonality2 = false;
        internal static bool showPersonality3 = false;
        internal static bool showPersonalities = false;
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
                if (tooltipData == null) return;

                if (!string.IsNullOrEmpty(tooltipData.title) && tooltipData.title != "New Profile")
                    Misc.SetTitle(Plugin.plugin, false, tooltipData.title, tooltipData.titleColor);

                if (config.tooltip_showAvatar && tooltipData.avatar != null)
                {
                    ImGui.Image(tooltipData.avatar.Handle, new Vector2(100, 100));
                }

                if (config.tooltip_showName && !string.IsNullOrEmpty(tooltipData.Name))
                {
                    ImGui.Text("NAME: ");
                    ImGui.SameLine();
                    Misc.RenderHtmlColoredTextInline(tooltipData.Name, 400);
                }

                if (config.tooltip_showRace && !string.IsNullOrEmpty(tooltipData.Race))
                {
                    ImGui.Text("RACE: ");
                    ImGui.SameLine();
                    Misc.RenderHtmlColoredTextInline(tooltipData.Race, 400);
                }

                if (config.tooltip_showGender && !string.IsNullOrEmpty(tooltipData.Gender))
                {
                    ImGui.Text("GENDER: ");
                    ImGui.SameLine();
                    Misc.RenderHtmlColoredTextInline(tooltipData.Gender, 400);
                }

                if (config.tooltip_showAge && !string.IsNullOrEmpty(tooltipData.Age))
                {
                    ImGui.Text("AGE: ");
                    ImGui.SameLine();
                    Misc.RenderHtmlColoredTextInline(tooltipData.Age, 400);
                }

                if (config.tooltip_showHeight && !string.IsNullOrEmpty(tooltipData.Height))
                {
                    ImGui.Text("HEIGHT: ");
                    ImGui.SameLine();
                    Misc.RenderHtmlColoredTextInline(tooltipData.Height, 400);
                }

                if (config.tooltip_showWeight && !string.IsNullOrEmpty(tooltipData.Weight))
                {
                    ImGui.Text("WEIGHT: ");
                    ImGui.SameLine();
                    Misc.RenderHtmlColoredTextInline(tooltipData.Weight, 400);
                }
                if (config.tooltip_ShowCustomDescriptors && tooltipData.descriptors != null)
                {
                    foreach (descriptor descriptor in tooltipData.descriptors)
                    {
                        ImGui.Spacing();
                        ImGui.Text(descriptor.name?.ToUpper() + ": ");
                        ImGui.SameLine();
                        Misc.RenderHtmlColoredTextInline(descriptor.description ?? string.Empty, 400);
                    }
                }

                if (config.tooltip_showAlignment)
                {
                    if (hasAlignment && tooltipData.alignmentImg != null)
                    {
                        ImGui.Text("ALIGNMENT:");
                        ImGui.Image(tooltipData.alignmentImg.Handle, new Vector2(32, 32));
                        ImGui.SameLine();
                        Misc.RenderHtmlColoredTextInline(UI.AlignmentName(tooltipData.Alignment), 400);
                    }
                }

                if (config.tooltip_showPersonalityTraits)
                {
                    if (showPersonalities)
                    {
                        ImGui.Text("TRAITS:");
                        if (showPersonality1 && tooltipData.personality_1Img != null)
                        {
                            ImGui.Image(tooltipData.personality_1Img.Handle, new Vector2(32, 42));
                            ImGui.SameLine();
                            Misc.RenderHtmlColoredTextInline(UI.PersonalityNames(tooltipData.Personality_1), 400);
                        }
                        if (showPersonality2 && tooltipData.personality_2Img != null)
                        {
                            ImGui.Image(tooltipData.personality_2Img.Handle, new Vector2(32, 42));
                            ImGui.SameLine();
                            Misc.RenderHtmlColoredTextInline(UI.PersonalityNames(tooltipData.Personality_2), 400);
                        }
                        if (showPersonality3 && tooltipData.personality_3Img != null)
                        {
                            ImGui.Image(tooltipData.personality_3Img.Handle, new Vector2(32, 42));
                            ImGui.SameLine();
                            Misc.RenderHtmlColoredTextInline(UI.PersonalityNames(tooltipData.Personality_3), 400);
                        }
                    }
                    if (config.tooltip_showCustomTraits && tooltipData.personalities != null)
                    {
                        foreach (trait personality in tooltipData.personalities)
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
                WindowOperations.SafeDispose(tooltipData.alignmentImg);
                tooltipData.alignmentImg = null;
                WindowOperations.SafeDispose(tooltipData.personality_1Img);
                tooltipData.personality_1Img = null;
                WindowOperations.SafeDispose(tooltipData.personality_2Img);
                tooltipData.personality_2Img = null;
                WindowOperations.SafeDispose(tooltipData.personality_3Img);
                tooltipData.personality_3Img = null;
                foreach (trait personality in tooltipData.personalities)
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
