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
    internal class BioTab
    {
        public static byte[] avatarBytes;
        public static IDalamudTextureWrap currentAvatarImg;
        public static bool editAvatar = false;
        public static string[] bioFieldsArr = new string[7]; //fields such as name, race, gender and so on
        public static int currentPersonality_1, currentPersonality_2, currentPersonality_3 = (int)UI.Personalities.None;
        public static int currentAlignment = (int)UI.Personalities.None;
        public static void LoadBioTab()
        {
            //display for avatar
            ImGui.Image(currentAvatarImg.ImGuiHandle, new Vector2(ImGui.GetIO().FontGlobalScale / 0.015f));

            if (ImGui.Button("Edit Avatar"))
            {
                editAvatar = true; //used to open the file dialog
            }
            ImGui.Spacing();
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
            //text must be multiline so add the multiline field/fields
            var biofield = UI.BioFieldVals[(int)UI.BioFieldTypes.afg];
            ImGui.Text(biofield.Item1);
            ImGui.InputTextMultiline(biofield.Item2, ref bioFieldsArr[(int)UI.BioFieldTypes.afg], 3100, new Vector2(ImGui.GetWindowSize().X - 20, ImGui.GetWindowSize().Y / 5));
            //simple for loop to get through our bio text fields


            ImGui.Spacing();
            ImGui.Spacing();

            ImGui.TextColored(new Vector4(1, 1, 1, 1), "ALIGNMENT:");
            AddAlignmentSelection(); //add alignment combo selection

            ImGui.Spacing();

            ImGui.TextColored(new Vector4(1, 1, 1, 1), "PERSONALITY TRAITS:");
            //add personality combos
            AddPersonalitySelection_1();
            AddPersonalitySelection_2();
            AddPersonalitySelection_3();

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
