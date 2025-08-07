using AbsoluteRoleplay.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;
using static AbsoluteRoleplay.UI;
using AbsoluteRoleplay.Helpers;
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
        public static void RenderBioPreview(BioLayout layout, string tabName, Vector4 titleColor)
        {
            try
            {

                Misc.SetTitle(Plugin.plugin, true, tabName, titleColor);
                // Defensive: Ensure lists are not null
                var descriptors = layout.descriptors ?? new List<descriptor>();
                var fields = layout.fields ?? new List<field>();
                var traits = layout.traits ?? new List<trait>();

                // NAME
                if (!string.IsNullOrEmpty(layout.name) && layout.name != "New Profile")
                {
                    ImGui.Spacing();
                    ImGui.TextWrapped("NAME: ");
                    ImGui.SameLine();
                    Misc.RenderHtmlElements(layout.name, true, true, true, false);
                }
                // RACE
                if (!string.IsNullOrEmpty(layout.race))
                {
                    ImGui.TextWrapped("RACE: ");
                    ImGui.SameLine();
                    Misc.RenderHtmlElements(layout.race, true, true, true, false);
                }
                // GENDER
                if (!string.IsNullOrEmpty(layout.gender))
                {
                    ImGui.TextWrapped("GENDER: ");
                    ImGui.SameLine();
                    Misc.RenderHtmlElements(layout.gender, true, true, true, false);
                }
                // AGE
                if (!string.IsNullOrEmpty(layout.age))
                {
                    ImGui.TextWrapped("AGE: ");
                    ImGui.SameLine();
                    Misc.RenderHtmlElements(layout.age, true, true, true, false);
                }
                // HEIGHT
                if (!string.IsNullOrEmpty(layout.height))
                {
                    ImGui.TextWrapped("HEIGHT: ");
                    ImGui.SameLine();
                    Misc.RenderHtmlElements(layout.height, true, true, true, false);
                }
                // WEIGHT
                if (!string.IsNullOrEmpty(layout.weight))
                {
                    ImGui.TextWrapped("WEIGHT: ");
                    ImGui.SameLine();
                    Misc.RenderHtmlElements(layout.weight, true, true, true, false);
                }

                // DESCRIPTORS
                foreach (var descriptor in descriptors)
                {
                    if (descriptor == null) continue;
                    ImGui.Spacing();
                    Misc.RenderHtmlElements(descriptor.name ?? string.Empty, true, true, true, false);
                    ImGui.SameLine();
                    ImGui.TextWrapped(":");
                    ImGui.SameLine();
                    Misc.RenderHtmlElements(descriptor.description ?? string.Empty, true, true, true, false);
                }

                // AT FIRST GLANCE
                if (!string.IsNullOrEmpty(layout.afg))
                {
                    ImGui.TextWrapped("AT FIRST GLANCE: ");
                    Misc.RenderHtmlElements(layout.afg ?? string.Empty, true, true, true, false);
                }

                // ALIGNMENT
                if (layout.alignment != 9 && layout.alignment >= 0 && layout.alignment <= UI.AlignmentVals.Count())
                {
                    ImGui.Text("ALIGNMENT: ");
                    var icon = UI.AlignmentIcon(layout.alignment);
                    if (icon != null)
                    {
                        Logger.Error($"[RenderBioPreview] Alignment icon: {(icon == null ? "null" : icon.ToString())}, Handle: {icon.Handle}");
                    }
                    if (icon != null && icon.Handle != IntPtr.Zero)
                    {
                        try
                        {
                            ImGui.Image(icon.Handle, new Vector2(ImGui.GetIO().FontGlobalScale * 38));
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"RenderBioPreview: Failed to render alignment icon: {ex.Message}");
                        }
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

                // FIELDS
                foreach (var field in fields)
                {
                    if (field == null) continue;
                    ImGui.Spacing();
                    Misc.RenderHtmlColoredTextInline((field.name ?? string.Empty).ToUpper() + ": ", 400);
                    Misc.RenderHtmlElements(field.description ?? string.Empty, true, true, true, false);
                }

                Vector2 alignmentSize = new Vector2(ImGui.GetIO().FontGlobalScale * 25, ImGui.GetIO().FontGlobalScale * 32);
                // PERSONALITY TRAITS (icons)
                if ((layout.personality_1 != 26 || layout.personality_2 != 26 || layout.personality_3 != 26)
                    && UI.PersonalityValues != null && UI.PersonalityValues.Count() > 0)
                {
                    ImGui.Spacing();
                    ImGui.TextColored(new Vector4(1, 1, 1, 1), "TRAITS:");

                    int[] personalities = { layout.personality_1, layout.personality_2, layout.personality_3 };
                    using (var personalityTable = ImRaii.Table("personality_traits_table", 3))
                    {
                        if (personalityTable)
                        {
                            ImGui.TableSetupColumn("Personality 1", ImGuiTableColumnFlags.WidthFixed, ImGui.GetIO().FontGlobalScale * 25);
                            ImGui.TableSetupColumn("Personality 2", ImGuiTableColumnFlags.WidthFixed, ImGui.GetIO().FontGlobalScale * 25);
                            ImGui.TableSetupColumn("Personality 3", ImGuiTableColumnFlags.WidthFixed, ImGui.GetIO().FontGlobalScale * 25);

                            ImGui.TableNextRow();
                            for (int i = 0; i < personalities.Length; i++)
                            {
                                ImGui.TableNextColumn();
                                int personalityIdx = personalities[i];
                                if (personalityIdx == 26 || personalityIdx < 0 || personalityIdx >= UI.PersonalityValues.Count())
                                {
                                    ImGui.TextColored(new Vector4(1, 0, 0, 1), $"No trait");
                                    continue;
                                }

                                var icon = UI.PersonalityIcon(personalityIdx);
                                if (icon != null)
                                {
                                    Logger.Error($"[RenderBioPreview] Personality icon {i + 1}: {(icon == null ? "null" : icon.ToString())}, Handle: {icon.Handle}");
                                }
                                if (icon == null || icon.Handle == IntPtr.Zero)
                                {
                                    ImGui.TextColored(new Vector4(1, 0, 0, 1), $"Personality icon {i + 1} not loaded.");
                                    continue;
                                }
                                try
                                {
                                    ImGui.Image(icon.Handle, alignmentSize);
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error($"RenderBioPreview: Failed to render personality icon: {ex.Message}");
                                }
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.BeginTooltip();
                                    try
                                    {
                                        ImGui.Text(UI.PersonalityNames(personalityIdx));
                                        ImGui.TextUnformatted(UI.PersonalityValues[personalityIdx].Item2);
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Error($"RenderBioPreview: Tooltip error: {ex.Message}");
                                    }
                                    ImGui.EndTooltip();
                                }
                            }
                        }
                    }
                }

                ImGui.Spacing();

                // CUSTOM TRAITS TABLE
                using var table = ImRaii.Table("table_name", 3);
                if (table)
                {
                    ImGui.TableSetupColumn("Column 1", ImGuiTableColumnFlags.WidthFixed, ImGui.GetIO().FontGlobalScale * 25);
                    ImGui.TableSetupColumn("Column 2", ImGuiTableColumnFlags.WidthFixed, ImGui.GetIO().FontGlobalScale * 25);
                    ImGui.TableSetupColumn("Column 3", ImGuiTableColumnFlags.WidthFixed, ImGui.GetIO().FontGlobalScale * 25);
                    foreach (var personality in traits)
                    {
                        if (personality == null)
                        {
                            ImGui.TableNextColumn();
                            ImGui.TextColored(new Vector4(1, 0, 0, 1), "Trait missing.");
                            continue;
                        }
                        ImGui.TableNextColumn();
                        var traitIcon = personality.icon?.icon;
                        if (traitIcon != null)
                        {
                            Logger.Error($"[RenderBioPreview] Custom trait icon: {(traitIcon == null ? "null" : traitIcon.ToString())}, Handle: {traitIcon.Handle}");
                        }
                        if (traitIcon == null || traitIcon.Handle == IntPtr.Zero)
                        {
                            ImGui.TextColored(new Vector4(1, 0, 0, 1), "Personality icon not loaded.");
                            continue;
                        }
                        try
                        {
                            ImGui.Image(traitIcon.Handle, alignmentSize);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"RenderBioPreview: Failed to render trait icon: {ex.Message}");
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            try
                            {
                                Misc.RenderHtmlColoredTextInline(personality.name ?? string.Empty, 400);
                                Misc.RenderHtmlElements(personality.description ?? string.Empty, false, true, true, true, null, true);
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"RenderBioPreview: Trait tooltip error: {ex.Message}");
                            }
                            ImGui.EndTooltip();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"RenderBioPreview: Exception: {ex.Message}");
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
            if(personality.icon.icon.Handle == IntPtr.Zero)
            {
                personality.icon.icon = UICommonImage(CommonImageTypes.blank);
            }
            ImGui.Image(personality.icon.icon.Handle, new Vector2(iconWidth, iconHeight));
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
            using var combo = ImRaii.Combo("##Personality Feature #1", text);
            if (!combo)
                return;
            if (ImGui.Selectable("None", layout.personality_1 == 26))
                layout.personality_1 = 26;
            UIHelpers.SelectableHelpMarker("Undefined");

            foreach (var ((newText, newDesc), idx) in PersonalityValues.WithIndex())
            {
                if (idx != (int)Personalities.None)
                {
                    if (ImGui.Selectable(newText, idx == layout.personality_1))
                        layout.personality_1 = idx;

                    UIHelpers.SelectableHelpMarker(newDesc);
                }
            }
        }
        public static void AddPersonalitySelection_2(BioLayout layout)
        {
            var (text, desc) = PersonalityValues[layout.personality_2];
            using var combo = ImRaii.Combo("##Personality Feature #2", text);
            if(ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(desc);
            }
            if (!combo)
                return;

            if (ImGui.Selectable("None", layout.personality_2 == 26))
                layout.personality_2 = 26;
            UIHelpers.SelectableHelpMarker("Undefined");
            foreach (var ((newText, newDesc), idx) in PersonalityValues.WithIndex())
            {
                if (idx != (int)Personalities.None)
                {
                    if (ImGui.Selectable(newText, idx == layout.personality_2))
                        layout.personality_2 = idx;

                    UIHelpers.SelectableHelpMarker(newDesc);
                }
            }
        }
        public static void AddPersonalitySelection_3(BioLayout layout)
        {
            var (text, desc) = PersonalityValues[layout.personality_3];
            using var combo = ImRaii.Combo("##Personality Feature #3", text);
            if(ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(desc);
            }   
            if (!combo)
                return;

            if (ImGui.Selectable("None", layout.personality_3 == 26))
                layout.personality_3 = 26;
            UIHelpers.SelectableHelpMarker("Undefined");
            foreach (var ((newText, newDesc), idx) in PersonalityValues.WithIndex())
            {
                if (idx != (int)Personalities.None)
                {
                    if (ImGui.Selectable(newText, idx == layout.personality_3))
                        layout.personality_3 = idx;

                    UIHelpers.SelectableHelpMarker(newDesc);
                }
            }
        }
        public static void AddAlignmentSelection(BioLayout layout)
        {
            var (text, desc) = AlignmentVals[layout.alignment];
            using var combo = ImRaii.Combo("##Alignment", text);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(desc);
            }
            if (!combo)
                return;
            if (ImGui.Selectable("None", layout.alignment == 9))
                layout.alignment = 9;
            UIHelpers.SelectableHelpMarker("Undefined");
            foreach (var ((newText, newDesc), idx) in AlignmentVals.WithIndex())
            {
                if (idx != 9)
                {
                    if (ImGui.Selectable(newText, idx == layout.alignment))
                        layout.alignment = idx;

                    UIHelpers.SelectableHelpMarker(newDesc);
                }

            }
        }
    }
}
