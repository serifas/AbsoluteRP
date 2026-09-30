using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;
using AbsoluteRP.RsUI.Pages;
using static AbsoluteRP.Misc;
using static AbsoluteRP.UI;
namespace AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes
{ 

    // Bio tab layout - the main character info page with name, race, gender, age, alignment, personality traits, custom descriptors, and custom fields. Both edit and view modes.
    internal class Bio
    {
        public static int currentAlignment = (int)Alignments.None;

        private static bool firstLoad = true;

        // Collapsible open-state for RsElements.BeginCollapsible.
        private static bool _openBasicInfo = true;
        private static bool _openCustomInfo;
        private static bool _openDetails;
        private static bool _openCustomDetails;
        private static bool _openTraits;
        private static bool _openCustomTraits;

        // Cached option-name lists for RsElements.Dropdown so we don't rebuild them each frame.
        private static IReadOnlyList<string>? _personalityOptions;
        private static IReadOnlyList<string>? _alignmentOptions;
        private static IReadOnlyList<string> PersonalityOptions() =>
            _personalityOptions ??= PersonalityValues.Select(v => v.Item1).ToList();
        private static IReadOnlyList<string> AlignmentOptions() =>
            _alignmentOptions ??= AlignmentVals.Select(v => v.Item1).ToList();

        private static ParsedNode UpdateParsed(ref ParsedNode cache, ref string lastValue, string currentValue)
        {
            if (lastValue != currentValue)
            {
                cache = Misc.ParseHtmlLayout(currentValue ?? string.Empty);
                lastValue = currentValue;
            }
            return cache;
        }
        public static void RenderBioPreview(BioLayout layout, string tabName, Vector4 titleColor)
        {
            try
            {
                Misc.SetTitle(Plugin.plugin, true, tabName, titleColor);
                var descriptors = layout.descriptors ?? new List<descriptor>();
                var fields = layout.fields ?? new List<field>();
                var traits = layout.traits ?? new List<trait>();

                float wrapWidth = ImGui.GetWindowSize().X - 50;
                float wrapHeight = ImGui.GetWindowSize().Y;

                // NAME
                if (!string.IsNullOrEmpty(layout.name) && layout.name != "New Profile")
                {
                    ImGui.Spacing();
                    ImGui.TextWrapped("NAME: ");
                    ImGui.SameLine();
                    Misc.RenderHtmlElements(layout.name ?? string.Empty, true, true, true, false, new Vector2(wrapWidth, wrapHeight));
                }
                // RACE
                if (!string.IsNullOrEmpty(layout.race))
                {
                    ImGui.TextWrapped("RACE: ");
                    ImGui.SameLine();
                    Misc.RenderHtmlElements(layout.race ?? string.Empty, true, true, true, false, new Vector2(wrapWidth, wrapHeight));
                }
                // GENDER
                if (!string.IsNullOrEmpty(layout.gender))
                {
                    ImGui.TextWrapped("GENDER: ");
                    ImGui.SameLine();
                    Misc.RenderHtmlElements(layout.gender ?? string.Empty, true, true, true, false, new Vector2(wrapWidth, wrapHeight));
                }
                // AGE
                if (!string.IsNullOrEmpty(layout.age))
                {
                    ImGui.TextWrapped("AGE: ");
                    ImGui.SameLine();
                    Misc.RenderHtmlElements(layout.age ?? string.Empty, true, true, true, false, new Vector2(wrapWidth, wrapHeight));
                }
                // HEIGHT
                if (!string.IsNullOrEmpty(layout.height))
                {
                    ImGui.TextWrapped("HEIGHT: ");
                    ImGui.SameLine();
                    Misc.RenderHtmlElements(layout.height ?? string.Empty, true, true, true, false, new Vector2(wrapWidth, wrapHeight));
                }
                // WEIGHT
                if (!string.IsNullOrEmpty(layout.weight))
                {
                    ImGui.TextWrapped("WEIGHT: ");
                    ImGui.SameLine();
                    Misc.RenderHtmlElements(layout.weight ?? string.Empty, true, true, true, false, new Vector2(wrapWidth, wrapHeight));
                }

                // DESCRIPTORS
                foreach (var descriptor in descriptors)
                {
                    if (descriptor == null) continue;
                    ImGui.Spacing();
                    Misc.RenderHtmlElements(descriptor.name ?? string.Empty, true, true, true, false, new Vector2(wrapWidth, wrapHeight));
                    ImGui.SameLine();
                    ImGui.TextWrapped(":");
                    ImGui.SameLine();
                    Misc.RenderHtmlElements(descriptor.description ?? string.Empty, true, true, true, false, new Vector2(wrapWidth, wrapHeight));
                }

                // AT FIRST GLANCE
                if (!string.IsNullOrEmpty(layout.afg))
                {
                    ImGui.TextWrapped("AT FIRST GLANCE: ");
                    Misc.RenderHtmlElements(layout.afg ?? string.Empty, true, true, true, false, new Vector2(wrapWidth, wrapHeight));
                }

                // ALIGNMENT
                if (layout.alignment != 9 && layout.alignment >= 0 && layout.alignment <= UI.AlignmentVals.Count())
                {
                    ImGui.Text("ALIGNMENT: ");
                    var icon = UI.AlignmentIcon(layout.alignment);
                    if (icon != null && icon.Handle != IntPtr.Zero)
                    {
                        try
                        {
                            ImGui.Image(icon.Handle, new Vector2(ImGui.GetIO().FontGlobalScale * 38));
                        }
                        catch (Exception ex)
                        {
                            Plugin.PluginLog.Debug($"RenderBioPreview: Failed to render alignment icon: {ex.Message}");
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
                    Misc.RenderHtmlElements((field.name ?? string.Empty).ToUpper() + ": ", true, true, true, false, new Vector2(400, wrapHeight));
                    Misc.RenderHtmlElements(field.description ?? string.Empty, true, true, true, false, new Vector2(wrapWidth, wrapHeight));
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
                                    Plugin.PluginLog.Debug($"RenderBioPreview: Failed to render personality icon: {ex.Message}");
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
                                        Plugin.PluginLog.Debug($"RenderBioPreview: Tooltip Debug: {ex.Message}");
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
                            Plugin.PluginLog.Debug($"RenderBioPreview: Failed to render trait icon: {ex.Message}");
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            try
                            {
                                Misc.RenderHtmlElements(personality.name ?? string.Empty, false, true, true, true, null, true);
                                Misc.RenderHtmlElements(personality.description ?? string.Empty, false, true, true, true, null, true);
                            }
                            catch (Exception ex)
                            {
                                Plugin.PluginLog.Debug($"RenderBioPreview: Trait tooltip Debug: {ex.Message}");
                            }
                            ImGui.EndTooltip();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"RenderBioPreview: Exception: {ex.Message}");
            }
        }
        public static void RenderBioLayout(int index, string id, BioLayout layout)
        {
            ImGui.Spacing();
            bool isTooltip = layout.isTooltip;
            if (RsElements.Checkbox($"Set as tooltip##Tooltip{layout.id}", ref isTooltip))
            {
                for(int i = 0; i < ProfilesPage.CurrentProfile.customTabs.Count; i++)
                {
                    if (ProfilesPage.CurrentProfile.customTabs[i].Layout is BioLayout biolayout)
                    {
                        biolayout.isTooltip = false;
                    }
                }
                layout.isTooltip = isTooltip;
            }
            Helpers.TutorialManager.Anchor(Helpers.ProfileTutorial.Anchor_BioSetAsTooltip);
            if(ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Set this bio as the tooltip of the tooltipData.\nOnly one tooltipData can be set as a tooltip at a time.");
            }
           /* ImGui.SameLine();
            bool viewable = layout.viewable;
            if(ImGui.Checkbox($"Viewable##Viewable{layout.id}", ref viewable))
            {
                layout.viewable = viewable;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("If checked, this tab will be viewable by others.\nIf unchecked, it will not be displayed.");
            }
           */

            bool basicOpen = RsElements.BeginCollapsible($"bio_basic_{index}", "Basic Info", ref _openBasicInfo);
            try
            {
                Helpers.TutorialManager.Anchor(Helpers.ProfileTutorial.Anchor_BioBasicInfo);
                if (basicOpen)
                {
                    string name = layout.name;
                    string race = layout.race;
                    string gender = layout.gender;
                    string age = layout.age;
                    string height = layout.height;
                    string weight = layout.weight;

                    ImGui.TextUnformatted("NAME:");
                    ImGui.SameLine();
                    if (RsElements.InputText($"##name_bio{index}", ref name, 100, "Character Name (The name or nickname of the character you are currently playing as)")) { layout.name = name; }
                    Helpers.TutorialManager.Anchor(Helpers.ProfileTutorial.Anchor_BioName);

                    ImGui.TextUnformatted("RACE:");
                    ImGui.SameLine();
                    if (RsElements.InputText($"##race_bio{index}", ref race, 100, "The IC Race of your character")) { layout.race = race; }
                    Helpers.TutorialManager.Anchor(Helpers.ProfileTutorial.Anchor_BioRace);
                    ImGui.TextUnformatted("GENDER:");
                    ImGui.SameLine();
                    if (RsElements.InputText($"##gender_bio{index}", ref gender, 100, "The IC gender of your character")) { layout.gender = gender; }
                    Helpers.TutorialManager.Anchor(Helpers.ProfileTutorial.Anchor_BioGender);
                    ImGui.TextUnformatted("AGE:");
                    ImGui.SameLine();
                    if (RsElements.InputText($"##age_bio{index}", ref age, 100, "Must be specified to post in nsfw. No nsfw if not 18+")) { layout.age = age; }
                    Helpers.TutorialManager.Anchor(Helpers.ProfileTutorial.Anchor_BioAge);
                    var heightMin = ImGui.GetCursorScreenPos();
                    ImGui.TextUnformatted("HEIGHT:");
                    ImGui.SameLine();
                    if (RsElements.InputText($"##height_bio{index}", ref height, 100, "Your OC's IC Height")) { layout.height = height; }
                    ImGui.TextUnformatted("WEIGHT:");
                    ImGui.SameLine();
                    if (RsElements.InputText($"##weight_bio{index}", ref weight, 100, "Your OC's IC Weight")) { layout.weight = weight; }
                    var heightMax = ImGui.GetItemRectMax();
                    Helpers.TutorialManager.AnchorRect(Helpers.ProfileTutorial.Anchor_BioHeight, heightMin, heightMax);
                }
            }
            finally { RsElements.EndCollapsible(); }
            bool customInfoOpen = RsElements.BeginCollapsible($"bio_custominfo_{index}", "Custom Info", ref _openCustomInfo);
            try
            {
                Helpers.TutorialManager.Anchor(Helpers.ProfileTutorial.Anchor_BioCustomInfo);
                if (customInfoOpen)
                {
                    if (RsElements.Button("Add Field##CustomDescriptor", RsElements.ButtonVariant.Primary))
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
                        // Reserve room for the trailing Remove button so wide description inputs can't push it off-panel.
                        float removeW  = RsElements.MeasureButtonWidth($"Remove##RemoveDescriptor{descriptor.index}");
                        float colonW   = ImGui.CalcTextSize(":").X;
                        float gap      = ImGui.GetStyle().ItemSpacing.X;
                        float avail    = RsElements.AvailContentWidth();
                        float nameW    = MathF.Max(RsTheme.S(80f), avail * 0.22f);
                        float descW    = MathF.Max(RsTheme.S(80f), avail - nameW - colonW - removeW - gap * 4f);
                        if (RsElements.InputText($"##DescriptorName{descriptor.index}", ref descriptorName, 75, width: nameW / RsTheme.Scale))
                        {
                            descriptor.name = descriptorName;
                        }
                        ImGui.SameLine();
                        ImGui.TextUnformatted(":");
                        ImGui.SameLine();
                        if (RsElements.InputText($"##DescriptorDescription{descriptor.index}", ref descriptorDescription, 500, width: descW / RsTheme.Scale))
                        {
                            descriptor.description = descriptorDescription;
                        }
                        ImGui.SameLine();
                        if (RsElements.Button($"Remove##RemoveDescriptor{descriptor.index}", RsElements.ButtonVariant.Danger))
                        {
                            layout.descriptors.RemoveAll(p => p.index == descriptor.index);
                        }
                    }
                }
            }
            finally { RsElements.EndCollapsible(); }
            bool detailsOpen = RsElements.BeginCollapsible($"bio_details_{index}", "Details", ref _openDetails);
            try
            {
                Helpers.TutorialManager.Anchor(Helpers.ProfileTutorial.Anchor_BioDetails);
                if (detailsOpen)
                {
                    string afg = layout.afg;
                    ImGui.TextUnformatted("AT FIRST GLANCE:");
                    if (RsElements.InputTextArea($"##afg_bio{index}", ref afg, 3100, size: new Vector2(RsElements.AvailContentWidth(), RsTheme.S(80f))))
                    {
                        layout.afg = afg;
                    }
                }
            }
            finally { RsElements.EndCollapsible(); }
            bool customDetailsOpen = RsElements.BeginCollapsible($"bio_customdetails_{index}", "Custom Details", ref _openCustomDetails);
            try
            {
                Helpers.TutorialManager.Anchor(Helpers.ProfileTutorial.Anchor_BioCustomDetails);
                if (customDetailsOpen)
                {
                    if (RsElements.Button("Add Field##CustomFieldBtn", RsElements.ButtonVariant.Primary))
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
                        float removeW = RsElements.MeasureButtonWidth($"Remove##RemoveField{field.index}");
                        float gap     = ImGui.GetStyle().ItemSpacing.X;
                        float avail   = RsElements.AvailContentWidth();
                        float nameW   = MathF.Max(RsTheme.S(120f), avail - removeW - gap * 2f);
                        if (RsElements.InputText($"##FieldName{field.index}", ref fieldName, 75, width: nameW / RsTheme.Scale))
                        {
                            field.name = fieldName;
                        }
                        ImGui.SameLine();
                        if (RsElements.Button($"Remove##RemoveField{field.index}", RsElements.ButtonVariant.Danger))
                        {
                            layout.fields.RemoveAll(p => p.index == field.index);
                        }
                        var innerW = RsElements.AvailContentWidth();
                        if (RsElements.InputTextArea($"##FieldDescription{field.index}", ref fieldDescription, 500, size: new Vector2(innerW, RsTheme.S(60f))))
                        {
                            field.description = fieldDescription;
                        }
                        ImGui.Dummy(new Vector2(0f, RsTheme.S(4f)));
                    }
                }
            }
            finally { RsElements.EndCollapsible(); }

            bool traitsOpen = RsElements.BeginCollapsible($"bio_traits_{index}", "Traits", ref _openTraits);
            try
            {
                Helpers.TutorialManager.Anchor(Helpers.ProfileTutorial.Anchor_BioTraits);
                if (traitsOpen)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                    ImGui.TextUnformatted("ALIGNMENT:");
                    ImGui.PopStyleColor();
                    AddAlignmentSelection(layout); //add alignment combo selection

                    ImGui.Spacing();

                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                    ImGui.TextUnformatted("TRAITS:");
                    ImGui.PopStyleColor();
                    // add personality combos
                    AddPersonalitySelection_1(layout);
                    AddPersonalitySelection_2(layout);
                    AddPersonalitySelection_3(layout);
                }
            }
            finally { RsElements.EndCollapsible(); }

            bool customTraitsOpen = RsElements.BeginCollapsible($"bio_customtraits_{index}", "Custom Traits", ref _openCustomTraits);
            try
            {
                Helpers.TutorialManager.Anchor(Helpers.ProfileTutorial.Anchor_BioCustomTraits);
                if (customTraitsOpen)
                {
                    if (RsElements.Button("Add Custom Trait##AddPersonality", RsElements.ButtonVariant.Primary))
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
            finally { RsElements.EndCollapsible(); }
        }
        public static void LoadCustomTraits(Plugin plugin, BioLayout layout, trait personality)
        {
            try
            {
                if (personality == null)
                {
                    Plugin.PluginLog?.Debug("LoadCustomTraits: trait is null, skipping.");
                    return;
                }

                // Ensure icon element exists
                if (personality.icon == null)
                {
                    personality.icon = new IconElement { iconID = 0, icon = UICommonImage(CommonImageTypes.blank) };
                }

                // Ensure the IDalamudTextureWrap exists
                if (personality.icon.icon == null || personality.icon.icon.Handle == IntPtr.Zero)
                {
                    // Try to set a sane fallback texture
                    var fallback = UICommonImage(CommonImageTypes.blank);
                    if (fallback != null)
                        personality.icon.icon = fallback;
                    else
                        Plugin.PluginLog?.Debug("LoadCustomTraits: fallback UICommonImage returned null.");
                }

                string name = personality.name ?? string.Empty;
                string description = personality.description ?? string.Empty;

                // Safe width/height calculation using a fallback size if texture missing
                float fontScale = ImGui.GetIO().FontGlobalScale;
                var tex = personality.icon.icon;
                float iconHeight = fontScale * (tex?.Height ?? 24);
                float iconWidth = fontScale * (tex?.Width ?? 24);

                if (tex != null && tex.Handle != IntPtr.Zero)
                {
                    try { ImGui.Image(tex.Handle, new Vector2(iconWidth, iconHeight)); } catch (Exception ex) { Plugin.PluginLog?.Debug($"LoadCustomTraits: Image draw failed: {ex.Message}"); }
                }
                else
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentDanger);
                    ImGui.TextUnformatted("Icon missing");
                    ImGui.PopStyleColor();
                }

                ImGui.SameLine();
                if (RsElements.Button($"Set Icon##{personality.index}", RsElements.ButtonVariant.Ghost))
                {
                    foreach (trait p in layout.traits)
                    {
                        p.modifying = false;
                    }
                    personality.modifying = true;
                }

                ImGui.SameLine();
                if (RsElements.Button($"Remove##RemovePersonality{personality.index}", RsElements.ButtonVariant.Danger))
                {
                    layout.traits.RemoveAll(p => p.index == personality.index);
                    return;
                }

                if (RsElements.InputText($"##Name{personality.index}", ref name, 75))
                {
                    personality.name = name;
                }
                if (RsElements.InputTextArea($"##Description{personality.index}", ref description, 500, size: new Vector2(RsElements.AvailContentWidth(), RsTheme.S(60f))))
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
            catch (Exception ex)
            {
                Plugin.PluginLog?.Debug($"LoadCustomTraits: unexpected exception: {ex}");
            }
        }
        private static void DrawPersonalityDropdown(string id, ref int selection)
        {
            var options = PersonalityOptions();
            int current = (selection >= 0 && selection < options.Count) ? selection : (int)Personalities.None;
            int previous = current;
            if (RsElements.Dropdown(id, ref current, options) && current != previous)
            {
                selection = current;
            }
            if (ImGui.IsItemHovered() && current >= 0 && current < PersonalityValues.Count())
            {
                ImGui.SetTooltip(PersonalityValues[current].Item2);
            }
        }
        public static void AddPersonalitySelection_1(BioLayout layout)
        {
            int sel = layout.personality_1;
            DrawPersonalityDropdown("##Personality Feature #1", ref sel);
            layout.personality_1 = sel;
        }
        public static void AddPersonalitySelection_2(BioLayout layout)
        {
            int sel = layout.personality_2;
            DrawPersonalityDropdown("##Personality Feature #2", ref sel);
            layout.personality_2 = sel;
        }
        public static void AddPersonalitySelection_3(BioLayout layout)
        {
            int sel = layout.personality_3;
            DrawPersonalityDropdown("##Personality Feature #3", ref sel);
            layout.personality_3 = sel;
        }
        public static void AddAlignmentSelection(BioLayout layout)
        {
            var options = AlignmentOptions();
            int sel = (layout.alignment >= 0 && layout.alignment < options.Count) ? layout.alignment : 9;
            int previous = sel;
            if (RsElements.Dropdown("##Alignment", ref sel, options) && sel != previous)
            {
                layout.alignment = sel;
            }
            if (ImGui.IsItemHovered() && sel >= 0 && sel < AlignmentVals.Count())
            {
                ImGui.SetTooltip(AlignmentVals[sel].Item2);
            }
        }
    }
}
