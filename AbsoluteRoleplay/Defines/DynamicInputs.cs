using Dalamud.Interface.Textures.TextureWraps;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AbsoluteRoleplay.Defines
{
    internal class LayoutItems
    {
        private static string[] textValues = new string[0];


        public static SortedList<int, object> fieldValues = new SortedList<int, object>();
        public static void AddText()
        {
            string[] TextVals = new string[textValues.Length + 1];
            Array.Copy(textValues, TextVals, textValues.Length);

            textValues[TextVals.Length - 1] = "";
            textValues = TextVals;

            ImGui.InputText("##" + textValues, ref textValues[0], 500);
            fieldValues.Add(fieldValues.Count + 1, textValues[0]);
        }
        public static void AddColoredText()
        {

        }
        public static void AddTextMultiline()
        {

        }
        public static void AddImage()
        {

        }
        public static void AddGallery()
        {

        }
        public static void AddIcon()
        {

        }
        public static void AddAlignment()
        {

        }
        public static void AddTooltip()
        {

        }
    }
}
