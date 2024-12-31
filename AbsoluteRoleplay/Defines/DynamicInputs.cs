using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Windows.Ect;
using AbsoluteRoleplay.Windows.Profiles;
using AbsoluteRoleplay.Windows.Profiles.ProfileTabs;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Networking;
using OtterGui.Text.EndObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.GroupPoseModule;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AbsoluteRoleplay.Defines
{
    public class Layout
    {
        public int id { get; set; }
        public List<TextElement> textVals { get; set; }
        public List<ImageElement> imageVals { get; set; }
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
        public IDalamudTextureWrap textureWrap { set; get; }
        public string tooltip { get; set; }
        public bool nsfw { get; set; }
        public bool triggering { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public bool locked { set; get; }
        public bool modifying { set; get; }
        public bool canceled { set; get; }
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
    internal class DynamicInputs
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
            if (!layouts.ContainsKey(layoutID))
            {
                layouts[layoutID] = new Layout
                {
                    id = layoutID,
                    textVals = new List<TextElement>(),
                    imageVals = new List<ImageElement>()
                };
            }

            var textElement = layouts[layoutID].textVals.FirstOrDefault(e => e.id == elementID);

            if (textElement == null || textElement.canceled)
            {
                if (textElement != null && textElement.canceled)
                    return;

                textElement = new TextElement
                {
                    id = elementID,
                    type = type,
                    text = $"Text Element {elementID}",
                    color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f),
                    PosX = 100,
                    PosY = 100,
                    locked = true,
                    modifying = false,
                    canceled = false
                };
                layouts[layoutID].textVals.Add(textElement);
            }

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
                    plugin.logger.Error($"Text Element {textElement.id} dropped at position: {textElement.PosX}, {textElement.PosY}");
                }
            }

            if (textElement.modifying)
            {
                // Editable logic
                RenderEditableTextElement(layoutID, elementID, plugin, textElement);
            }
            else
            {
                // Display logic
                RenderDisplayTextElement(layoutID, elementID, plugin, textElement);
            }
        }


        public static void AddImageElement(int layoutID, int elementID, Plugin plugin)
        {
            if (!layouts.ContainsKey(layoutID))
            {
                layouts[layoutID] = new Layout
                {
                    id = layoutID,
                    textVals = new List<TextElement>(),
                    imageVals = new List<ImageElement>()
                };
            }
         
            var imageElement = layouts[layoutID].imageVals.FirstOrDefault(e => e.id == elementID);
            
            if (imageElement == null || imageElement.canceled)
            {
                if (imageElement != null && imageElement.canceled)
                    return;

                imageElement = new ImageElement
                {
                    id = elementID,
                    url = string.Empty,
                    bytes = new byte[0],
                    textureWrap = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab),
                    tooltip = string.Empty,
                    nsfw = false,
                    triggering = false,
                    width = 100,
                    height = 100,
                    PosX = 100,
                    PosY = 100,
                    locked = true,
                    modifying = false,
                    canceled = false
                };
                layouts[layoutID].imageVals.Add(imageElement);
            }

            Vector2 mousePos = ImGui.GetMousePos();

            if (draggingElementID == elementID)
            {
                if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    imageElement.PosX = mousePos.X - dragOffset.X;
                    imageElement.PosY = mousePos.Y - dragOffset.Y;
                }
                else if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    draggingElementID = null;
                    plugin.logger.Error($"Image Element {imageElement.id} dropped at position: {imageElement.PosX}, {imageElement.PosY}");
                }
            }

            if (imageElement.modifying)
            {
                // Editable logic
                RenderEditableImageElement(layoutID, elementID, plugin, imageElement);
            }
            else
            {
                // Display logic
                RenderDisplayImageElement(layoutID, elementID, plugin, imageElement);
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

        private static void RenderDisplayTextElement(int layoutID, int elementID, Plugin plugin, TextElement textElement)
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

            if (textElement.locked == false)
            {
                if (ImGui.Button($"Lock##{layoutID}_{elementID}"))
                {
                    textElement.locked = true;
                }
                if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    draggingElementID = elementID; // Start dragging
                    dragOffset = ImGui.GetMousePos() - new Vector2(textElement.PosX, textElement.PosY);
                    plugin.logger.Error($"Started dragging Element {textElement.id}");
                }
            }
            else
            {
                // Render the "Unlock" button
                if (ImGui.Button($"Unlock##{layoutID}_{elementID}"))
                {
                    textElement.locked = false;
                }
            }
        }
        private static void RenderEditableTextElement(int layoutID, int elementID, Plugin plugin, TextElement textElement)
        {
            string text = textElement.text;
            Vector4 color = textElement.color;

            // Set the font color for the InputText
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.PushItemWidth(ImGui.GetWindowSize().X / 2.5f);

            // Render the InputText field with the custom text color
            if (textElement.type == 0)
            {
                ImGui.SetCursorPos(new Vector2(textElement.PosX, textElement.PosY));
                if (ImGui.InputText($"##Text Input {layoutID}_{elementID}", ref text, 200))
                {
                    textElement.text = text; // Update text if it was changed
                }
            }
            if (textElement.type == 1)
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
            RenderDeleteConfirmationPopup(() =>
            {
                if (layouts.ContainsKey(layoutID))
                {
                    var layout = layouts[layoutID];

                    // Mark the element as canceled
                    var elementToCancel = layout.textVals.FirstOrDefault(e => e.id == elementID);
                    if (elementToCancel != null)
                    {
                        elementToCancel.canceled = true;
                        plugin.logger.Error($"Marked Text Element {elementID} as canceled in Layout {layoutID}");
                    }
                    else
                    {
                        plugin.logger.Error($"Text Element {elementID} not found in Layout {layoutID}");
                    }
                }
                else
                {
                    plugin.logger.Error($"Layout {layoutID} does not exist. Cannot mark Text Element {elementID} as canceled.");
                }
            });

        }
        private static void RenderDisplayImageElement(int layoutID, int elementID, Plugin plugin, ImageElement imageElement)
        {
            // Render the image at its position
            ImGui.SetCursorPos(new Vector2(imageElement.PosX, imageElement.PosY));
            ImGui.Image(imageElement.textureWrap.ImGuiHandle, new Vector2(imageElement.width, imageElement.height));

            ImGui.SameLine();

            // Render the "Edit" button
            if (ImGui.Button($"Edit##{layoutID}_{elementID}"))
            {
                imageElement.modifying = true; // Change mode back to editable
            }

            ImGui.SameLine();

            if (imageElement.locked == false)
            {
                if (ImGui.Button($"Lock##{layoutID}_{elementID}") && draggingElementID == null)
                {
                    imageElement.locked = true;
                }
                if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && draggingElementID == null)
                {
                    draggingElementID = elementID; // Start dragging
                    dragOffset = ImGui.GetMousePos() - new Vector2(imageElement.PosX, imageElement.PosY);
                    plugin.logger.Error($"Started dragging Image Element {imageElement.id}");
                }
            }
            else
            {
                // Render the "Unlock" button
                if (ImGui.Button($"Unlock##{layoutID}_{elementID}") && draggingElementID == null)
                {
                    imageElement.locked = false;
                }
            }
        }
        private static void RenderEditableImageElement(int layoutID, int elementID, Plugin plugin, ImageElement imageElement)
        {
            string url = imageElement.url;
            float width = imageElement.width;
            float height = imageElement.height;

            // Render the URL input
            if (ImGui.InputText($"URL:##{layoutID}_{elementID}", ref url, 2000))
            {
                imageElement.url = url;
            }

            ImGui.SameLine();

            // Render the image
            ImGui.Image(imageElement.textureWrap.ImGuiHandle, new Vector2(width, height));

            ImGui.SameLine();

            // Render the "Submit" button
            if (ImGui.Button($"Submit##{layoutID}_{elementID}"))
            {
                imageElement.textureWrap = Imaging.DownloadImage(url); // Update the texture
                imageElement.modifying = false; // Change mode to display
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

            RenderDeleteConfirmationPopup(() =>
            {
                if (layouts.ContainsKey(layoutID))
                {
                    var layout = layouts[layoutID];

                    // Mark the element as canceled
                    var elementToCancel = layout.imageVals.FirstOrDefault(e => e.id == elementID);
                    if (elementToCancel != null)
                    {
                        elementToCancel.canceled = true;
                        plugin.logger.Error($"Marked Image Element {elementID} as canceled in Layout {layoutID}");
                    }
                    else
                    {
                        plugin.logger.Error($"Image Element {elementID} not found in Layout {layoutID}");
                    }
                }
                else
                {
                    plugin.logger.Error($"Layout {layoutID} does not exist. Cannot mark Image Element {elementID} as canceled.");
                }
            });

        }






    }
}
