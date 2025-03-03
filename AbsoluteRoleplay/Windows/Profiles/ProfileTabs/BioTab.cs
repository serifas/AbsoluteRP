using AbsoluteRoleplay.Helpers;
using Dalamud.Interface.Textures.TextureWraps;
using ImGuiNET;
using OtterGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRoleplay.Windows.Profiles.ProfileTabs
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

        public IconElement icon { get; set; } = new IconElement { icon = UI.UICommonImage(UI.CommonImageTypes.blank) };
    }
    internal class BioTab
    {
        public static byte[] avatarBytes;
        public static string[] bioFieldsArr = new string[7]; //fields such as name, race, gender and so on
        public static int currentPersonality_1, currentPersonality_2, currentPersonality_3 = (int)UI.Personalities.None;
        public static int currentAlignment = (int)UI.Personalities.None;
        public static List<trait> personalities = new List<trait>();
        public static List<descriptor> descriptors = new List<descriptor>();
        public static List<field> fields = new List<field>();

        private static bool firstLoad = true; 
        private static bool showStatusIconWindow = false;
        private static bool showBasicCharacterInfo = true;
        private static bool showCharacterDetails = true;
        private static bool showCharacterTraits = true;
        public static void LoadBioTab(Plugin plugin)
        {
            //display for avatar
            ImGui.Spacing();


            if (ImGui.CollapsingHeader("Basic Info", ImGuiTreeNodeFlags.None))
            {
                for (var i = 0; i < UI.BioFieldVals.Length; i++)
                {
                    var BioField = UI.BioFieldVals[i];
                    //if our input type is single line 
                    if (BioField.Item4 == UI.InputTypes.single)
                    {
                        ImGui.Text(BioField.Item1);
                        ImGui.SameLine();
                        //add the input text for the field
                        ImGui.InputTextWithHint(BioField.Item2, BioField.Item3, ref bioFieldsArr[i], 100);
                    }
                }
            }
            if (ImGui.CollapsingHeader("Custom Info", ImGuiTreeNodeFlags.None))
            {
                if (ImGui.Button("Add Field##CustomDescriptor"))
                {
                    descriptors.Add(new descriptor()
                    {
                        index = descriptors.Count,
                        name = "Name",
                        description = "Description"
                    });
                }
                foreach (var descriptor in descriptors.ToList()) // Iterate over a copy of the list
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
                        descriptors.RemoveAll(p => p.index == descriptor.index);
                    }
                }
            }
            if (ImGui.CollapsingHeader("Details", ImGuiTreeNodeFlags.None))
            {
                //text must be multiline so add the multiline field/fields
                var biofield = UI.BioFieldVals[(int)UI.BioFieldTypes.afg];
                ImGui.Text(biofield.Item1);
                ImGui.InputTextMultiline(biofield.Item2, ref bioFieldsArr[(int)UI.BioFieldTypes.afg], 3100, new Vector2(ImGui.GetWindowSize().X - 20, ImGui.GetWindowSize().Y / 5));

            }
            //simple for loop to get through our bio text fields
            if (ImGui.CollapsingHeader("Custom Details", ImGuiTreeNodeFlags.None))
            {
                if (ImGui.Button("Add Field##CustomFieldBtn"))
                {
                    fields.Add(new ProfileTabs.field()
                    {
                        index = fields.Count,
                        name = "Name",
                        description = "Description"
                    });
                }

                foreach (var field in fields.ToList()) // Iterate over a copy of the list
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
                        fields.RemoveAll(p => p.index == field.index);
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
                AddAlignmentSelection(); //add alignment combo selection

                ImGui.Spacing();

                ImGui.TextColored(new Vector4(1, 1, 1, 1), "TRAITS:");
                //add personality combos
                AddPersonalitySelection_1();
                AddPersonalitySelection_2();
                AddPersonalitySelection_3();
            }
            if (ImGui.CollapsingHeader("Custom Traits", ImGuiTreeNodeFlags.None))
            {
                if (ImGui.Button("Add Custom Trait##AddPersonality"))
                {
                    personalities.Add(new trait()
                    {
                        index = personalities.Count,
                        name = "Name",
                        description = "Description"
                    });
                }
                for (int i =0; i < personalities.Count; i++)
                {
                    LoadCustomPersonality(plugin, personalities[i]);
                }
                
            }
        }
        public static void LoadCustomPersonality(Plugin plugin, trait personality)
        {
            string name = personality.name;
            string description = personality.description;
            float iconHeight = ImGui.GetIO().FontGlobalScale * personality.icon.icon.Height;
            float iconWidth = ImGui.GetIO().FontGlobalScale * personality.icon.icon.Width;
            ImGui.Image(personality.icon.icon.ImGuiHandle, new Vector2(iconWidth, iconHeight));
            ImGui.SameLine();
            if (ImGui.Button($"Set Icon##{personality.index}"))
            {
                foreach(trait p in personalities)
                {
                    p.modifying = false;
                }
                personality.modifying = true;
            }
            ImGui.SameLine();
            if (ImGui.Button($"Remove##RemovePersonality{personality.index}"))
            {
                personalities.RemoveAll(p => p.index == personality.index);
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
        public static void AddPersonalitySelection_1()
        {
            var (text, desc) = UI.PersonalityValues[currentPersonality_1];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Personality Feature #1", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;
            if (ImGui.Selectable("None", currentPersonality_1 == 26))
                currentPersonality_1 = 26;
            ImGuiUtil.SelectableHelpMarker("Undefined");

            foreach (var ((newText, newDesc), idx) in UI.PersonalityValues.WithIndex())
            {
                if (idx != (int)UI.Personalities.None)
                {
                    if (ImGui.Selectable(newText, idx == currentPersonality_1))
                        currentPersonality_1 = idx;

                    ImGuiUtil.SelectableHelpMarker(newDesc);
                }
            }
        }
        public static void AddPersonalitySelection_2()
        {
            var (text, desc) = UI.PersonalityValues[currentPersonality_2];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Personality Feature #2", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;

            if (ImGui.Selectable("None", currentPersonality_2 == 26))
                currentPersonality_2 = 26;
            ImGuiUtil.SelectableHelpMarker("Undefined");
            foreach (var ((newText, newDesc), idx) in UI.PersonalityValues.WithIndex())
            {
                if (idx != (int)UI.Personalities.None)
                {
                    if (ImGui.Selectable(newText, idx == currentPersonality_2))
                        currentPersonality_2 = idx;

                    ImGuiUtil.SelectableHelpMarker(newDesc);
                }
            }
        }
        public static void AddPersonalitySelection_3()
        {
            var (text, desc) = UI.PersonalityValues[currentPersonality_3];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Personality Feature #3", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;

            if (ImGui.Selectable("None", currentPersonality_3 == 26))
                currentPersonality_3 = 26;
            ImGuiUtil.SelectableHelpMarker("Undefined");
            foreach (var ((newText, newDesc), idx) in UI.PersonalityValues.WithIndex())
            {
                if (idx != (int)UI.Personalities.None)
                {
                    if (ImGui.Selectable(newText, idx == currentPersonality_3))
                        currentPersonality_3 = idx;

                    ImGuiUtil.SelectableHelpMarker(newDesc);
                }
            }
        }
        public static void AddAlignmentSelection()
        {
            var (text, desc) = UI.AlignmentVals[currentAlignment];
            using var combo = OtterGui.Raii.ImRaii.Combo("##Alignment", text);
            ImGuiUtil.HoverTooltip(desc);
            if (!combo)
                return;
            if (ImGui.Selectable("None", currentAlignment == 9))
                currentAlignment = 9;
            ImGuiUtil.SelectableHelpMarker("Undefined");
            foreach (var ((newText, newDesc), idx) in UI.AlignmentVals.WithIndex())
            {
                if (idx != 9)
                {
                    if (ImGui.Selectable(newText, idx == currentAlignment))
                        currentAlignment = idx;

                    ImGuiUtil.SelectableHelpMarker(newDesc);
                }

            }
        }
    }
}
