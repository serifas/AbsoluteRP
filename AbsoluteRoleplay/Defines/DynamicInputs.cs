using AbsoluteRoleplay.Windows.Profiles;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
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
        public int type { set; get; }
        public string text { set; get; }
        public Vector4 color { set; get; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public bool locked { set; get; }
        public bool modifying { set; get; }
        public bool canceled { set; get; } = false;
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
        private static int? pendingLayoutID = null;
        private static int? pendingElementID = null;
        public static SortedList<int, Layout> layouts = new SortedList<int, Layout>();
        private static Vector2 dragOffset = Vector2.Zero;
        private static int? draggingElementID = null;
        public static bool Lockstatus = true; 
        private static bool showDeleteConfirmationPopup = false;
        public static void AddTextElement(int layoutID, int type, int elementID, Plugin plugin)
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
            if (layouts[layoutID].textVals[elementID] == null )
            {
                layouts[layoutID].textVals[elementID] = new TextElement
                {
                    id = elementID,
                    type = type,
                    text = $"Text Element {elementID}",
                    color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f), // Default to white color
                    PosX = 100, // Default position
                    PosY = 100,
                    locked = true,
                    modifying = false
                };
            }

            // Retrieve the specific text element
            var textElement = layouts[layoutID].textVals[elementID];

            // Handle dragging logic
            Vector2 mousePos = ImGui.GetMousePos();

            if (draggingElementID == elementID)
            {
                if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    textElement.PosX = mousePos.X - dragOffset.X;
                    textElement.PosY = mousePos.Y - dragOffset.Y;
                }
                else if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    draggingElementID = null;
                    plugin.logger.Error($"Element {textElement.id} dropped at position: {textElement.PosX}, {textElement.PosY}");
                }
            }

            // Check if the element is in "editable" or "display" mode
            if (textElement.modifying == true) // Editable mode
            {
                string text = textElement.text;
                Vector4 color = textElement.color;

                // Set the font color for the InputText
                ImGui.PushStyleColor(ImGuiCol.Text, color);
                ImGui.PushItemWidth(ImGui.GetWindowSize().X / 2.5f);

                // Render the InputText field with the custom text color
                if (type == 0)
                {
                    ImGui.SetCursorPos(new Vector2(textElement.PosX, textElement.PosY));
                    if (ImGui.InputText($"##Text Input {layoutID}_{elementID}", ref text, 200))
                    {
                        textElement.text = text; // Update text if it was changed
                    }
                }
                if (type == 1)
                {
                    if (ImGui.InputTextMultiline($"##Text Input {layoutID}_{elementID}", ref text, 200, new Vector2(ImGui.GetWindowSize().X / 2.5f, 120)))
                    {
                        textElement.text = text; // Update text if it was changed
                    }
                }
                ImGui.PopItemWidth();
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
                    textElement.modifying = false; // Change mode to display
                }


                ImGui.SameLine();

                // Render the "Delete" button
                if (ImGui.Button($"Delete##{layoutID}_{elementID}"))
                {
                    // Open the confirmation popup
                    showDeleteConfirmationPopup = true;
                    pendingLayoutID = layoutID;
                    pendingElementID = elementID;
                    ImGui.OpenPopup("Delete Confirmation");
                }

                // Call the popup rendering logic
                RenderDeleteConfirmationPopup(() =>
                {
                    // Logic to delete the element
                    textElement.canceled = true;
                    plugin.logger.Error($"Deleted Element {elementID} from Layout {layoutID}");
                });
            }
            else
            {
                // Render the element at its position
                ImGui.SetCursorPos(new Vector2(textElement.PosX, textElement.PosY));

                // Render the colored text safely as unformatted text
                ImGui.PushStyleColor(ImGuiCol.Text, textElement.color);
                ImGui.TextUnformatted(textElement.text); // Render raw text
                ImGui.PopStyleColor();

                ImGui.SameLine();

                // Render the "Edit" button
                if (ImGui.Button($"Edit##{layoutID}_{elementID}"))
                {
                    textElement.modifying = true; // Change mode back to editable
                }

                ImGui.SameLine();

                if(textElement.locked == false)
                {
                    if (ImGui.Button($"Lock##{layoutID}_{elementID}") && draggingElementID == null)
                    {
                        Lockstatus = true;
                        textElement.locked = true;
                    }
                    ImGui.SameLine();
                    if(ImGui.Button($"Move##{layoutID}_{elementID}"))
                    {
                    }
                    if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && draggingElementID == null)
                    {
                        draggingElementID = elementID; // Start dragging
                        dragOffset = mousePos - new Vector2(textElement.PosX, textElement.PosY);
                        plugin.logger.Error($"Started dragging Element {textElement.id}");
                    }
                }
                else
                {
                    // Render the "Drag" button
                    if (ImGui.Button($"Unlock##{layoutID}_{elementID}") && draggingElementID == null)
                    {
                        Lockstatus = false;
                        textElement.locked = false;
                    }
                } 
               
            }
        }

        public static void RenderDeleteConfirmationPopup(Action onConfirm)
        {
            if (pendingLayoutID.HasValue && pendingElementID.HasValue)
            {
                if (ImGui.BeginPopupModal("Delete Confirmation", ref showDeleteConfirmationPopup, ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGui.Text("Are you sure you want to delete this element?");
                    ImGui.Separator();

                    // Confirm button
                    if (ImGui.Button("Yes"))
                    {
                        onConfirm?.Invoke(); // Call the provided action to delete the element
                        pendingLayoutID = null;
                        pendingElementID = null;
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.SameLine();

                    // Cancel button
                    if (ImGui.Button("No"))
                    {
                        pendingLayoutID = null;
                        pendingElementID = null;
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.EndPopup();
                }
            }
        }







    }
}
