using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Windows.MainPanel.Views.Account;
using AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using OtterGui;
using OtterGui.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static AbsoluteRoleplay.UI;
namespace AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes
{
    public class descriptor
    {
        public int index { get; set; }
        public string name { get; set; }
        public string description { get; set; }
    }
    public class field
    {
        public int index { get; set; }
        public string name { get; set; }
        public string description { get; set; }
    }
    public class trait
    {
        public int iconID { get; set; }

        public int index { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public bool modifying { get; set; } // Controls window visibility

        public IconElement icon { get; set; } = new IconElement { icon = UICommonImage(CommonImageTypes.blank) };
    }

    internal class Bio
    {
        public static int currentAlignment = (int)Alignments.None;

        private static bool firstLoad = true;
        public static void RenderBioPreview(BioLayout layout, Vector4 titleColor)
        {
            if (!string.IsNullOrEmpty(layout.name) && layout.name != "New Profile")
            {
                ImGui.Spacing();
                ImGuiHelpers.SafeTextWrapped("NAME:   ");
                ImGui.SameLine();
                Misc.RenderHtmlColoredTextInline(layout.name);
            }
            if (!string.IsNullOrEmpty(layout.race))
            {
                ImGui.Spacing();
                ImGuiHelpers.SafeTextWrapped("RACE:   ");
                ImGui.SameLine();
                Misc.RenderHtmlColoredTextInline(layout.race);
            }
            if (!string.IsNullOrEmpty(layout.gender))
            {
                ImGui.Spacing();
                ImGuiHelpers.SafeTextWrapped("GENDER:   ");
                ImGui.SameLine();
                Misc.RenderHtmlColoredTextInline(layout.gender);
            }
            if (!string.IsNullOrEmpty(layout.age))
            {
                ImGui.Spacing();
                ImGuiHelpers.SafeTextWrapped("AGE:   ");
                ImGui.SameLine();
                Misc.RenderHtmlColoredTextInline(layout.age);
            }
            if (!string.IsNullOrEmpty(layout.height))
            {
                ImGui.Spacing();
                ImGuiHelpers.SafeTextWrapped("HEIGHT:   ");
                ImGui.SameLine();
                Misc.RenderHtmlColoredTextInline(layout.height);
            }
            if (!string.IsNullOrEmpty(layout.weight))
            {
                ImGui.Spacing();
                ImGuiHelpers.SafeTextWrapped("WEIGHT:   ");
                ImGui.SameLine();
                Misc.RenderHtmlColoredTextInline(layout.weight);
            }
            foreach (var descriptor in layout.descriptors)
            {
                ImGui.Spacing();
                Misc.RenderHtmlColoredTextInline(descriptor.name.ToUpper());
                ImGui.SameLine();
                ImGuiHelpers.SafeTextWrapped(": ");
                ImGui.SameLine();
                Misc.RenderHtmlColoredTextInline(descriptor.description);
            }
            if (!string.IsNullOrEmpty(layout.afg))
            {
                ImGui.Spacing();
                ImGuiHelpers.SafeTextWrapped("AT FIRST GLANCE: ");
                Misc.RenderHtmlColoredTextInline(layout.afg);
            }
            if (layout.alignment != 9)
            {
                ImGui.Text("ALIGNMENT:");
                var icon = UI.AlignementIcon(layout.alignment);
                if (icon != null && icon.ImGuiHandle != IntPtr.Zero)
                {
                    ImGui.Image(icon.ImGuiHandle, new Vector2(ImGui.GetIO().FontGlobalScale * 38));
                    var alignmentVal = UI.AlignmentVals[layout.alignment];
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip($"{alignmentVal.Item1}\n{alignmentVal.Item2}");
                    }
                }
                else
                {
                    ImGui.TextColored(new Vector4(1, 0, 0, 1), "Alignment icon not loaded.");
                }
            }
            foreach (field field in layout.fields)
            {
                ImGui.Spacing();
                ImGuiHelpers.SafeTextWrapped(field.name.ToUpper() + ": ");
                Misc.RenderHtmlColoredTextInline(field.description);

            }
            Vector2 alignmentSize = new Vector2(ImGui.GetIO().FontGlobalScale * 25, ImGui.GetIO().FontGlobalScale * 32);
            if (layout.personality_1 != 26 || layout.personality_2 != 26 || layout.personality_3 != 26)
            {
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(1, 1, 1, 1), "TRAITS:");

                int[] personalities = { layout.personality_1, layout.personality_2, layout.personality_3 };
                for (int i = 0; i < personalities.Length; i++)
                {
                    int personalityIdx = personalities[i];
                    if (personalityIdx == 26)
                        continue;

                    var icon = UI.PersonalityIcon(personalityIdx);
                    if (icon == null || icon.ImGuiHandle == IntPtr.Zero)
                    {
                        ImGui.TextColored(new Vector4(1, 0, 0, 1), $"Personality icon {i + 1} not loaded.");
                        continue;
                    }
                    if (icon.ImGuiHandle != null && icon.ImGuiHandle != IntPtr.Zero)
                    {
                        ImGui.Image(icon.ImGuiHandle, alignmentSize);
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.SetTooltip(UI.PersonalityNames(personalityIdx));
                            Misc.RenderHtmlColoredTextInline(UI.PersonalityValues[personalityIdx].Item2);
                            ImGui.EndTooltip();
                        }
                    }
                    if (i < personalities.Length - 1)
                    ImGui.SameLine();
                }
            }
            ImGui.Spacing();
            using var table = ImRaii.Table("table_name", 3);
            if (table)
            {
                ImGui.TableSetupColumn("Column 1", ImGuiTableColumnFlags.WidthFixed, ImGui.GetIO().FontGlobalScale * 25);
                ImGui.TableSetupColumn("Column 2", ImGuiTableColumnFlags.WidthFixed, ImGui.GetIO().FontGlobalScale * 25);
                ImGui.TableSetupColumn("Column 3", ImGuiTableColumnFlags.WidthFixed, ImGui.GetIO().FontGlobalScale * 25);
                foreach (trait personality in layout.traits)
                {
                    ImGui.TableNextColumn();
                    if (personality.icon == null || personality.icon.icon == null || personality.icon.icon.ImGuiHandle == IntPtr.Zero)
                    {
                        ImGui.TextColored(new Vector4(1, 0, 0, 1), "Personality icon not loaded.");
                        continue;
                    }
                    if (personality.icon.icon.ImGuiHandle != null && personality.icon.icon.ImGuiHandle != IntPtr.Zero)
                    {
                        ImGui.Image(personality.icon.icon.ImGuiHandle, alignmentSize);
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            Misc.RenderHtmlColoredTextInline(personality.description);
                            Misc.RenderHtmlColoredTextInline(personality.description);
                            ImGui.EndTooltip();
                        }
                    }
                }
            }
        }
        public static void RenderBioLayout(int index, string id, BioLayout layout)
        {
            //display for avatar
            ImGui.Spacing();
            bool isTooltip = layout.isTooltip;
            if (ImGui.Checkbox($"Set as toolip##{id}", ref isTooltip))
            {
                for(int i = 0; i < ProfileWindow.CurrentProfile.customTabs.Count; i++)
                {
                    if (ProfileWindow.CurrentProfile.customTabs[i].Layout is BioLayout biolayout)
                    {
                        biolayout.isTooltip = false; // Reset all other tooltips to false
                    }
                }
                layout.isTooltip = isTooltip;
            }

            if (ImGui.CollapsingHeader("Basic Info", ImGuiTreeNodeFlags.None))
            {
                string name = layout.name;
                string race = layout.race;
                string gender = layout.gender;
                string age = layout.age;
                string height = layout.height;
                string weight = layout.weight;
                ImGui.Text("NAME:");
                ImGui.SameLine();
                if (ImGui.InputTextWithHint($"##name_bio{index}", "Character Name (The name or nickname of the character you are currently playing as)", ref name, 100)) { layout.name = name; }
                ImGui.Text("RACE:");
                ImGui.SameLine();
                if (ImGui.InputTextWithHint($"##race_bio{index}", "The IC Race of your character", ref race, 100)) { layout.race = race; }
                ImGui.Text("GENDER:");
                ImGui.SameLine();
                if (ImGui.InputTextWithHint($"##gender_bio{index}", "The IC gender of your character", ref gender, 100)) { layout.gender = gender; }
                ImGui.Text("AGE:");
                ImGui.SameLine();
                if (ImGui.InputTextWithHint($"##age_bio{index}", "Must be specified to post in nsfw. No nsfw if not 18+", ref age, 100)) { layout.age = age; }
                ImGui.Text("HEIGHT:");
                ImGui.SameLine();
                if (ImGui.InputTextWithHint($"##height_bio{index}", "Your OC's IC Height", ref height, 100)) { layout.height = height; }
                ImGui.Text("WEIGHT:");
                ImGui.SameLine();
                if (ImGui.InputTextWithHint($"##weight_bio{index}", "Your OC's IC Weight", ref weight, 100)) { layout.weight = weight; }
            }
            if (ImGui.CollapsingHeader("Custom Info", ImGuiTreeNodeFlags.None))
            {
                if (ImGui.Button("Add Field##CustomDescriptor"))
                {
                    layout.descriptors.Add(new descriptor()
                    {
                        index = layout.descriptors.Count,
                        name = "Name",
                        description = "Description"
                    });
                }
                foreach (var descriptor in layout.descriptors.ToList()) // Iterate over a copy of the list
                {
                    string descriptorName = descriptor.name;
                    string descriptorDescription = descriptor.description;
                    ImGui.PushItemWidth(ImGui.GetWindowSize().X / 8);
                    if (ImGui.InputText($"##DescriptorName{descriptor.index}", ref descriptorName, 75))
                    {
                        descriptor.name = descriptorName;
                    }
                    ImGui.PopItemWidth();
                    ImGui.SameLine();
                    ImGui.Text(":");
                    ImGui.SameLine();
                    if (ImGui.InputText($"##DescriptorDescription{descriptor.index}", ref descriptorDescription, 500))
                    {
                        descriptor.description = descriptorDescription;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button($"Remove##RemoveDescriptor{descriptor.index}"))
                    {
                        layout.descriptors.RemoveAll(p => p.index == descriptor.index);
                    }
                }
            }
            if (ImGui.CollapsingHeader("Details", ImGuiTreeNodeFlags.None))
            {
                string afg = layout.afg;
                ImGui.Text("AT FIRST GLANCE:");
                if (ImGui.InputTextMultiline($"##afg_bio{index}", ref afg, 3100, new Vector2(ImGui.GetWindowSize().X - 20, ImGui.GetWindowSize().Y / 15)))
                {
                    layout.afg = afg;
                }
            }
            //simple for loop to get through our bio text fields
            if (ImGui.CollapsingHeader("Custom Details", ImGuiTreeNodeFlags.None))
            {
                if (ImGui.Button("Add Field##CustomFieldBtn"))
                {
                    layout.fields.Add(new field()
                    {
                        index = layout.fields.Count,
                        name = "Name",
                        description = "Description"
                    });
                }

                foreach (var field in layout.fields.ToList()) // Iterate over a copy of the list
                {
                    string fieldName = field.name;
                    string fieldDescription = field.description;
                    if (ImGui.InputText($"##FieldName{field.index}", ref fieldName, 75))
                    {
                        field.name = fieldName;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button($"Remove##RemoveField{field.index}"))
                    {
                        layout.fields.RemoveAll(p => p.index == field.index);
                    }
                    if (ImGui.InputTextMultiline($"##FieldDescription{field.index}", ref fieldDescription, 500, new Vector2(ImGui.GetWindowSize().X - 20, ImGui.GetWindowSize().Y / 20)))
                    {
                        field.description = fieldDescription;
                    }
                }
            }

            if (ImGui.CollapsingHeader("Traits", ImGuiTreeNodeFlags.None))
            {
                ImGui.TextColored(new Vector4(1, 1, 1, 1), "ALIGNMENT:");
                AddAlignmentSelection(layout); //add alignment combo selection

                ImGui.Spacing();

                ImGui.TextColored(new Vector4(1, 1, 1, 1), "TRAITS:");
                //add personality combos
                AddPersonalitySelection_1(layout);
                AddPersonalitySelection_2(layout);
                AddPersonalitySelection_3(layout);
            }
            if (ImGui.CollapsingHeader("Custom Traits", ImGuiTreeNodeFlags.None))
            {
                if (ImGui.Button("Add Custom Trait##AddPersonality"))
                {
                    layout.traits.Add(new trait()
                    {
                        index = layout.traits.Count,
                        name = "Name",
                        description = "Description"
                    });
                }
                foreach (var trait in layout.traits.ToList()) // Iterate over a copy of the list
                {
                    LoadCustomTraits(Plugin.plugin, layout, trait);
                }
            }
        }
        public static void LoadCustomTraits(Plugin plugin, BioLayout layout, trait personality)
        {
            string name = personality.name;
            string description = personality.description;
            float iconHeight = ImGui.GetIO().FontGlobalScale * personality.icon.icon.Height;
            float iconWidth = ImGui.GetIO().FontGlobalScale * personality.icon.icon.Width;
            if(personality.icon.icon.ImGuiHandle == IntPtr.Zero)
            {
                personality.icon.icon = UICommonImage(CommonImageTypes.blank);
            }
            ImGui.Image(personality.icon.icon.ImGuiHandle, new Vector2(iconWidth, iconHeight));
            ImGui.SameLine();
            if (ImGui.Button($"Set Icon##{personality.index}"))
            {
                foreach (trait p in layout.traits)
                {
                    p.modifying = false;
                }
                personality.modifying = true;
            }
            ImGui.SameLine();
            if (ImGui.Button($"Remove##RemovePersonality{personality.index}"))
            {
                layout.traits.RemoveAll(p => p.index == personality.index);
            }
            if (ImGui.InputText($"##Name{personality.index}", ref name, 75))
            {
                personality.name = name;
            }
            if (ImGui.InputTextMultiline($"##Description{personality.index}", ref description, 500, new Vector2(ImGui.GetWindowSize().X - 20, ImGui.GetWindowSize().Y / 20)))
            {
                personality.description = description;
            }
            bool displayIconSelection = personality.modifying;
            if (displayIconSelection)
            {

                if (!WindowOperations.iconsLoaded)
                {
                    WindowOperations.LoadStatusIconsLazy(plugin); // Load a small batch of icons
                }
                ImGui.Begin($"Icons", ref displayIconSelection, ImGuiWindowFlags.None);
                if (firstLoad)
                {
                    firstLoad = false;
                    ImGui.SetWindowSize(new Vector2(500, 650));
                    ImGui.SetWindowPos(new Vector2(ImGui.GetMainViewport().Size.X / 2 - 250, ImGui.GetMainViewport().Size.Y / 2 - 350));
                }

                WindowOperations.RenderStatusIcons(plugin, personality.icon, personality);
                ImGui.End();

            }
            personality.modifying = displayIconSelection;
        }
        public static void AddPersonalitySelection_1(BioLayout layout)
        {
            var (text, desc) = PersonalityValues[layout.personality_1];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Personality Feature #1", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;
            if (ImGui.Selectable("None", layout.personality_1 == 26))
                layout.personality_1 = 26;
            ImGuiUtil.SelectableHelpMarker("Undefined");

            foreach (var ((newText, newDesc), idx) in PersonalityValues.WithIndex())
            {
                if (idx != (int)Personalities.None)
                {
                    if (ImGui.Selectable(newText, idx == layout.personality_1))
                        layout.personality_1 = idx;

                    ImGuiUtil.SelectableHelpMarker(newDesc);
                }
            }
        }
        public static void AddPersonalitySelection_2(BioLayout layout)
        {
            var (text, desc) = PersonalityValues[layout.personality_2];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Personality Feature #2", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;

            if (ImGui.Selectable("None", layout.personality_2 == 26))
                layout.personality_2 = 26;
            ImGuiUtil.SelectableHelpMarker("Undefined");
            foreach (var ((newText, newDesc), idx) in PersonalityValues.WithIndex())
            {
                if (idx != (int)Personalities.None)
                {
                    if (ImGui.Selectable(newText, idx == layout.personality_2))
                        layout.personality_2 = idx;

                    ImGuiUtil.SelectableHelpMarker(newDesc);
                }
            }
        }
        public static void AddPersonalitySelection_3(BioLayout layout)
        {
            var (text, desc) = PersonalityValues[layout.personality_3];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Personality Feature #3", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;

            if (ImGui.Selectable("None", layout.personality_3 == 26))
                layout.personality_3 = 26;
            ImGuiUtil.SelectableHelpMarker("Undefined");
            foreach (var ((newText, newDesc), idx) in PersonalityValues.WithIndex())
            {
                if (idx != (int)Personalities.None)
                {
                    if (ImGui.Selectable(newText, idx == layout.personality_3))
                        layout.personality_3 = idx;

                    ImGuiUtil.SelectableHelpMarker(newDesc);
                }
            }
        }
        public static void AddAlignmentSelection(BioLayout layout)
        {
            var (text, desc) = AlignmentVals[layout.alignment];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Alignment", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;
            if (ImGui.Selectable("None", layout.alignment == 9))
                layout.alignment = 9;
            ImGuiUtil.SelectableHelpMarker("Undefined");
            foreach (var ((newText, newDesc), idx) in AlignmentVals.WithIndex())
            {
                if (idx != 9)
                {
                    if (ImGui.Selectable(newText, idx == layout.alignment))
                        layout.alignment = idx;

                    ImGuiUtil.SelectableHelpMarker(newDesc);
                }

            }
        }
    }
}
