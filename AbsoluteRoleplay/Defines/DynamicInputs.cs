using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Windows.Ect;
using AbsoluteRoleplay.Windows.MainPanel.Views.Account;
using AbsoluteRoleplay.Windows.Profiles;
using AbsoluteRoleplay.Windows.Profiles.ProfileTabs;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
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
        public List<LayoutElement> elements { get; set; } = new List<LayoutElement>();
        public bool loadTextElement { set; get; }
        public bool loadTextMultilineElement { set; get; }
        public bool loadImageElement { set; get; }
    }
    public class LayoutElement
    {
        public int id { set; get; }
        public bool locked { get; set; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public bool modifying { set; get; }
        public bool canceled { set; get; } = false;
        public bool dragging { set; get; }
        public Vector2 dragOffset { get; set; }
    }
    public class TextElement : LayoutElement
    {
        public int index { set; get; }
        public int type { set; get; }
        public string text { set; get; }
        public Vector4 color { set; get; }
    }
    public class ImageElement : LayoutElement
    {
        public int index { set; get; }
        public string url { get; set; }
        public byte[] bytes { get; set; }
        public IDalamudTextureWrap textureWrap { set; get; }
        public string tooltip { get; set; }
        public bool nsfw { get; set; }
        public bool triggering { get; set; }
        public int width { get; set; }
        public int height { get; set; }
    }
    public class StatusElement
    {
        int statusIconId { set; get; }
        public string name { set; get; }
        public string tooltip { set; get; }
        public float PosX { get; set; }
        public float PosY { get; set; }
    }
    public class IconElement
    {
        public int iconId { set; get; }
        public string name { set; get; }
        public string tooltip { set; get; }
        public float PosX { get; set; }
        public float PosY { get; set; }
    }
    public class ProgressElement
    {
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
        private static int? draggingTextElementID = null;
        private static int? draggingImageElementID = null;

        public static bool Lockstatus = true; 
        private static bool showDeleteConfirmationPopup = false;
        public static void AddTextElement(int layoutID, int type, int elementID, Plugin plugin)
        {
            if (!layouts.ContainsKey(layoutID))
            {
                layouts[layoutID] = new Layout
                {
                    id = layoutID,
                    elements = new List<LayoutElement>()
                };
            }

            var textElement = layouts[layoutID].elements.OfType<TextElement>().FirstOrDefault(e => e.id == elementID);

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
                layouts[layoutID].elements.Add(textElement);
            }

            Vector2 mousePos = ImGui.GetMousePos();

            if (draggingTextElementID == elementID)
            {
                if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    textElement.PosX = mousePos.X - textElement.dragOffset.X;
                    textElement.PosY = mousePos.Y - textElement.dragOffset.Y;
                }
                else if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    draggingTextElementID = null;
                    plugin.logger.Error($"Text Element {textElement.id} dropped at position: {textElement.PosX}, {textElement.PosY}");
                }
            }

            if (textElement.modifying)
            {
                ResetToLockedState(layoutID);
                // Editable logic
                RenderEditableTextElement(layoutID, elementID, plugin, textElement);
            }
            else
            {
                // Display logic
                RenderDisplayTextElement(layoutID, elementID, plugin, textElement);
            }
        }
        public void ReorderElement(Layout currentLayout, int elementID, int newOrder)
        {
            var element = currentLayout.elements.FirstOrDefault(e => e.id == elementID);
            if (element != null)
            {
                currentLayout.elements.Remove(element);
                currentLayout.elements.Insert(newOrder, element);
            }
        }

        public static void RenderElements(Layout currentLayout, Plugin plugin)
        {
            // Render existing text elements in the layout
            if (currentLayout.elements != null)
            {
                for (int i = 0; i < currentLayout.elements.OfType<TextElement>().Count(); i++)
                {
                    TextElement textElement = currentLayout.elements.OfType<TextElement>().ToArray()[i];
                    if (textElement != null)
                    {
                        DynamicInputs.AddTextElement(currentLayout.id, textElement.type, textElement.id, plugin);
                    }
                }
                for (int i = 0; i < currentLayout.elements.OfType<ImageElement>().Count(); i++)
                {
                    ImageElement imageElement = currentLayout.elements.OfType<ImageElement>().ToArray()[i];
                    if (imageElement != null)
                    {
                        DynamicInputs.AddImageElement(currentLayout.id, imageElement.id, plugin);

                    }
                }
            }



            // Handle adding a new text element
            if (currentLayout.loadTextElement)
            {
                int newId = currentLayout.elements.Any() ? currentLayout.elements.Max(e => e.id) + 1 : 0;
                currentLayout.loadTextElement = false;

                currentLayout.elements.Add(new TextElement
                {
                    id = newId,
                    text = $"Text Element {newId}",
                    type = 0,
                    color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f) // Default to white color
                });
            }

            if (currentLayout.loadTextMultilineElement)
            {
                int newId = currentLayout.elements.Any() ? currentLayout.elements.Max(e => e.id) + 1 : 0;
                currentLayout.loadTextMultilineElement = false;

                currentLayout.elements.Add(new TextElement
                {
                    id = newId,
                    text = $"Text Multiline Element {newId}",
                    type = 1,
                    color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f) // Default to white color
                });
            }

            // Handle adding a new image element
            if (currentLayout.loadImageElement)
            {
                int newId = currentLayout.elements.Any() ? currentLayout.elements.Max(e => e.id) + 1 : 0;
                currentLayout.loadImageElement = false;

                currentLayout.elements.Add(new ImageElement
                {
                    id = newId,
                    url = "",
                    bytes = null,
                    tooltip = "",
                    nsfw = false,
                    triggering = false,
                    width = 100,  // Default width
                    height = 100, // Default height
                    PosX = 100,   // Default position
                    PosY = 100
                });
            }

        }

        public static void RenderAddElementButton(Layout layout)
        {
            int currentElementCount = layout.elements.Count;
            if (ImGui.Button("Add Element"))
            {
                ImGui.OpenPopup("AddElementPopup"); // Open the popup when the button is clicked
            }

            if (ImGui.BeginPopup("AddElementPopup")) // Render the popup
            {
                ImGui.Text("Select Element Type");
                ImGui.Separator();

                // Option: Text Element
                if (ImGui.Selectable("Text Element"))
                {
                    currentElementCount++;
                    layout.loadTextElement = true;
                    //AddElement("Text Element");
                    ImGui.CloseCurrentPopup(); // Close the popup after selection
                }
                if (ImGui.Selectable("Multiline Text Element"))
                {
                    currentElementCount++;
                    layout.loadTextMultilineElement = true;
                    //AddElement("Text Element");
                    ImGui.CloseCurrentPopup(); // Close the popup after selection
                }

                // Option: Image Element
                if (ImGui.Selectable("Image Element"))
                {
                    currentElementCount++;
                    layout.loadImageElement = true;
                    //AddElement("Image Element");
                    ImGui.CloseCurrentPopup();
                }

                // Option: Status Element
                if (ImGui.Selectable("Status Element"))
                {
                    //AddElement("Status Element");
                    ImGui.CloseCurrentPopup();
                }

                // Option: Icon Element
                if (ImGui.Selectable("Icon Element"))
                {
                    //AddElement("Icon Element");
                    ImGui.CloseCurrentPopup();
                }

                // Option: Progress Element
                if (ImGui.Selectable("Progress Element"))
                {
                    // AddElement("Progress Element");
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }

      

        public static void AddImageElement(int layoutID, int elementID, Plugin plugin)
        {
            if (!layouts.ContainsKey(layoutID))
            {
                layouts[layoutID] = new Layout
                {
                    id = layoutID,
                    elements = new List<LayoutElement>()
                };
            }
         
            var imageElement = layouts[layoutID].elements.OfType<ImageElement>().FirstOrDefault(e => e.id == elementID);
            
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
                layouts[layoutID].elements.Add(imageElement);
            }

            Vector2 mousePos = ImGui.GetMousePos();

            if (draggingImageElementID == elementID)
            {
                if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    imageElement.PosX = mousePos.X - imageElement.dragOffset.X;
                    imageElement.PosY = mousePos.Y - imageElement.dragOffset.Y;
                }
                else if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    draggingImageElementID = null;
                    plugin.logger.Error($"Image Element {imageElement.id} dropped at position: {imageElement.PosX}, {imageElement.PosY}");
                }
            }

            if (imageElement.modifying)
            {
                ResetToLockedState(layoutID);
                // Editable logic
                RenderEditableImageElement(layoutID, plugin, imageElement);
            }
            else
            {
                // Display logic
                RenderDisplayImageElement(layoutID, plugin, imageElement);
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
            // Render the text at its position
            ImGui.SetCursorPos(new Vector2(textElement.PosX, textElement.PosY));
            ImGui.TextUnformatted(textElement.text);

            ImGui.SameLine();

            // Render the "Edit" button
            if (ImGui.Button($"Edit##{layoutID}_{elementID}"))
            {
                textElement.modifying = true; // Enter editing mode
            }

            ImGui.SameLine();

            if (textElement.locked)
            {
                // Render "Unlock" button
                if (ImGui.Button($"Unlock##{layoutID}_{elementID}"))
                {
                    textElement.locked = false; // Unlock the element
                    CheckLockState(layoutID);
                }
            }
            else
            {
                // Render "Lock" button
                if (ImGui.Button($"Lock##{layoutID}_{elementID}"))
                {
                    textElement.locked = true; // Lock the element
                    textElement.dragging = false; // Stop dragging if locked
                    CheckLockState(layoutID);
                }

                // Handle dragging only when unlocked
                if (textElement.dragging)
                {
                    if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                    {
                        Vector2 mousePos = ImGui.GetMousePos();
                        textElement.PosX = mousePos.X - textElement.dragOffset.X;
                        textElement.PosY = mousePos.Y - textElement.dragOffset.Y;
                    }
                    else if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                    {
                        textElement.dragging = false; // Stop dragging on release
                    }
                }
                else if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    textElement.dragging = true; // Start dragging
                    textElement.dragOffset = ImGui.GetMousePos() - new Vector2(textElement.PosX, textElement.PosY);
                }
            }
        }








        private static void RenderEditableTextElement(int layoutID, int elementID, Plugin plugin, TextElement textElement)
        {
            if (textElement == null || textElement.text == null)
            {
                plugin.logger.Error($"RenderEditableTextElement: Null TextElement or Text for LayoutID {layoutID}, ElementID {elementID}");
                return; // Prevent crashes
            }

            string text = textElement.text;
            Vector4 color = textElement.color;

            try
            {
                ImGui.PushStyleColor(ImGuiCol.Text, color);
                ImGui.PushItemWidth(ImGui.GetWindowSize().X / 2.5f);

                if (textElement.type == 0)
                {
                    ImGui.SetCursorPos(new Vector2(textElement.PosX, textElement.PosY));
                    if (ImGui.InputText($"##Text Input {layoutID}_{elementID}", ref text, 200))
                    {
                        textElement.text = text;
                    }
                }
                else if (textElement.type == 1)
                {
                    ImGui.SetCursorPos(new Vector2(textElement.PosX, textElement.PosY));
                    if (ImGui.InputTextMultiline($"##Text Input {layoutID}_{elementID}", ref text, 200, new Vector2(ImGui.GetWindowSize().X / 2.5f, 120)))
                    {
                        textElement.text = text;
                    }
                }
            }
            catch (Exception ex)
            {
                plugin.logger.Error($"RenderEditableTextElement: Exception occurred - {ex.Message}");
            }
            finally
            {
                ImGui.PopItemWidth();
                ImGui.PopStyleColor();
            }

            ImGui.SameLine();
            if (ImGui.ColorEdit4($"##Text Input Color {layoutID}_{elementID}", ref color, ImGuiColorEditFlags.NoInputs))
            {
                textElement.color = color;
            }

            ImGui.SameLine();
            if (ImGui.Button($"Submit##{layoutID}_{elementID}"))
            {
                textElement.modifying = false;
            }

            ImGui.SameLine();
            if (ImGui.Button($"Delete##{layoutID}_{elementID}"))
            {
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
                    var elementToCancel = layout.elements.OfType<TextElement>().FirstOrDefault(e => e.id == elementID);
                    if (elementToCancel != null)
                    {
                        elementToCancel.canceled = true;
                        plugin.logger.Error($"Marked Text Element {elementID} as canceled in Layout {layoutID}");
                    }
                }
            });
        }


        private static void RenderDisplayImageElement(int layoutID, Plugin plugin, ImageElement imageElement)
        {
            // Render the image at its position
            ImGui.SetCursorPos(new Vector2(imageElement.PosX, imageElement.PosY));
            ImGui.Image(imageElement.textureWrap.ImGuiHandle, new Vector2(imageElement.width, imageElement.height));

            ImGui.SameLine();

            // Render the "Edit" button
            if (ImGui.Button($"Edit##{layoutID}_{imageElement.id}"))
            {
                imageElement.modifying = true; // Enter editing mode
            }

            ImGui.SameLine();

            if (imageElement.locked)
            {
                // Render "Unlock" button
                if (ImGui.Button($"Unlock##{layoutID}_{imageElement.id}"))
                {
                    imageElement.locked = false; // Unlock the element
                    CheckLockState(layoutID);
                }
            }
            else
            {
                // Render "Lock" button
                if (ImGui.Button($"Lock##{layoutID}_{imageElement.id}"))
                {
                    imageElement.locked = true; // Lock the element
                    imageElement.dragging = false; // Stop dragging if locked
                    CheckLockState(layoutID);
                }

                // Handle dragging only when unlocked
                if (imageElement.dragging)
                {
                    if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                    {
                        Vector2 mousePos = ImGui.GetMousePos();
                        imageElement.PosX = mousePos.X - imageElement.dragOffset.X;
                        imageElement.PosY = mousePos.Y - imageElement.dragOffset.Y;
                    }
                    else if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                    {
                        imageElement.dragging = false; // Stop dragging on release
                    }
                }
                else if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    imageElement.dragging = true; // Start dragging
                    imageElement.dragOffset = ImGui.GetMousePos() - new Vector2(imageElement.PosX, imageElement.PosY);
                }
            }
        }







        private static void RenderEditableImageElement(int layoutID, Plugin plugin, ImageElement imageElement)
        {
            string url = imageElement.url;
            float width = imageElement.width;
            float height = imageElement.height;

            ImGui.SetCursorPos(new Vector2(imageElement.PosX, imageElement.PosY));
            ImGui.PushItemWidth(ImGui.GetWindowSize().X / 2.5f);
            // Render the URL input
            if (ImGui.InputText($"URL:##{layoutID}_{imageElement.id}", ref url, 2000))
            {
                imageElement.url = url;
            }

            ImGui.SameLine();

            // Render the image
            ImGui.Image(imageElement.textureWrap.ImGuiHandle, new Vector2(width, height));

            ImGui.SameLine();

            // Render the "Submit" button
            if (ImGui.Button($"Submit##{layoutID}_{imageElement.id}"))
            {
                imageElement.textureWrap = Imaging.DownloadImage(url); // Update the texture
                imageElement.modifying = false; // Change mode to display
            }

            ImGui.SameLine();

            // Render the "Delete" button
            if (ImGui.Button($"Delete##{layoutID}_{imageElement.id}"))
            {
                // Open the confirmation popup
                showDeleteConfirmationPopup = true;
                pendingLayoutID = layoutID;
                pendingElementID = imageElement.id;
                ImGui.OpenPopup("Delete Confirmation");
            }

            RenderDeleteConfirmationPopup(() =>
            {
                if (layouts.ContainsKey(layoutID))
                {
                    var layout = layouts[layoutID];

                    // Mark the element as canceled
                    var elementToCancel = layout.elements.OfType<ImageElement>().FirstOrDefault(e => e.id == imageElement.id);
                    if (elementToCancel != null)
                    {
                        elementToCancel.canceled = true;
                        plugin.logger.Error($"Marked Image Element {imageElement.id} as canceled in Layout {layoutID}");
                    }
                    else
                    {
                        plugin.logger.Error($"Image Element {imageElement.id} not found in Layout {layoutID}");
                    }
                }
                else
                {
                    plugin.logger.Error($"Layout {layoutID} does not exist. Cannot mark Image Element {imageElement.id} as canceled.");
                }
            });

        }
        public static void ResetToLockedState(int layoutID, bool resetDragging = true)
        {
            var layout = layouts[layoutID];

            foreach (LayoutElement element in layout.elements)
            {
                if (!element.locked)
                    element.locked = true; // Only lock unlocked elements
            }

            // Reset dragging states only if specified
            if (resetDragging)
            {
                draggingTextElementID = null;
                draggingImageElementID = null;
            }
        }
        public static void CheckLockState(int layoutID)
        {
            var layout = layouts[layoutID];
            Lockstatus = layout.elements.All(e => e.locked);
        }
    }
}
