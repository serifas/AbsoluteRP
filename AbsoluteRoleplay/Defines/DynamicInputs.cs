using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AbsoluteRoleplay.Defines
{
    public class Layout
    {
        public int id { get; set; }
        public TextElement[] textVals { get; set; }
        public ImageElement[] imageVals { get; set; }
        public StatusElement[] statusVals { get; set; }
        public IconElement[] iconVals { get; set; }
        public ProgressElement[] progressVals { get; set; }
    }
    public class TextElement
    {
        public int id { set; get; }
        public string text { set; get; }
        public Vector4 color { set; get; }
        public float PosX { get; set; }
        public float PosY { get; set; }
    }
    public class ImageElement
    {
        public int id { get; set; }
        public string url { get; set; }
        public byte[] bytes { get; set; }
        public string tooltip { get; set; }
        public bool nsfw { get; set; }
        public bool triggering { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public float PosX { get; set; }
        public float PosY { get; set; }
    }
    public class StatusElement
    {
        int id { set; get; }
        int statusIconId { set; get; }
        public string name { set; get; }
        public string tooltip { set; get; }
        public float PosX { get; set; }
        public float PosY { get; set; }
    }
    public class IconElement
    {
        public int id { set; get; }
        public int iconId { set; get; }
        public string name { set; get; }
        public string tooltip { set; get; }
        public float PosX { get; set; }
        public float PosY { get; set; }
    }
    public class ProgressElement
    {
        public int id { set; get; }
        public string name { set; get; }
        public float max { set; get; }
        public float progress { set; get; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float width { set; get; }
        public float height { set; get; }
    }
    public class LayoutNavigationElement
    {
        public int id { set; get; }
        public string name { set; get; }
        public string tooltip { set; get; }
        public float PosX { get; set; }
        public float PosY { get; set; }
    }
    internal class LayoutItems
    {
        public static SortedList<int, object> fieldValues = new SortedList<int, object>();

        public static SortedList<int, Layout> layouts = new SortedList<int, Layout>();
        public static void AddTextElement(int layoutID, int elementID)
        {
            // Ensure the layout exists
            if (!layouts.ContainsKey(layoutID))
            {
                layouts[layoutID] = new Layout
                {
                    id = layoutID,
                    textVals = new TextElement[10] // Initialize with a default size
                };
            }

            // Ensure textVals array exists
            if (layouts[layoutID].textVals == null)
            {
                layouts[layoutID].textVals = new TextElement[10]; // Initialize with a default size
            }

            // Ensure the specific element exists
            if (layouts[layoutID].textVals[elementID] == null)
            {
                layouts[layoutID].textVals[elementID] = new TextElement
                {
                    id = elementID,
                    text = $"Text Element {elementID}",
                    color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f), // Default to white color
                    PosX = 0,
                    PosY = 0
                };
            }

            // Retrieve the specific text element
            var textElement = layouts[layoutID].textVals[elementID];

            // Check if the element is in "editable" or "display" mode
            if (textElement.PosX == 0) // Using PosX as a flag for simplicity
            {
                string text = textElement.text;
                Vector4 color = textElement.color;

                // Set the font color for the InputText
                ImGui.PushStyleColor(ImGuiCol.Text, color);

                // Render the InputText field with the custom text color
                if (ImGui.InputText($"##Text Input {layoutID}_{elementID}", ref text, 200))
                {
                    textElement.text = text; // Update text if it was changed
                }

                ImGui.PopStyleColor(); // Restore the original font color

                ImGui.SameLine();

                // Render the color selector with no numeric inputs
                if (ImGui.ColorEdit4($"##Text Input Color {layoutID}_{elementID}", ref color, ImGuiColorEditFlags.NoInputs))
                {
                    textElement.color = color; // Update color if it was changed
                }

                ImGui.SameLine();

                // Render the "Submit" button
                if (ImGui.Button($"Submit##{layoutID}_{elementID}"))
                {
                    textElement.PosX = 1; // Change mode to display
                }
            }
            else
            {
                // Render the colored text safely as unformatted text
                ImGui.PushStyleColor(ImGuiCol.Text, textElement.color);
                ImGui.TextUnformatted(textElement.text); // Render raw text
                ImGui.PopStyleColor();

                ImGui.SameLine();

                // Render the "Edit" button
                if (ImGui.Button($"Edit##{layoutID}_{elementID}"))
                {
                    textElement.PosX = 0; // Change mode back to editable
                }
            }
        }




    }
}
