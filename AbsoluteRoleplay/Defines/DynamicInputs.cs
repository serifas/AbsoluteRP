using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Windows.Ect;
using AbsoluteRoleplay.Windows.MainPanel.Views.Account;
using AbsoluteRoleplay.Windows.Profiles;
using AbsoluteRoleplay.Windows.Profiles.ProfileTabs;
using Dalamud.Interface.ImGuiFileDialog;
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
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.GroupPoseModule;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AbsoluteRoleplay.Defines
{
    public class Layout
    {
        public int id { get; set; }
        public string name { get; set; }
        public List<LayoutElement> elements { get; set; } = new List<LayoutElement>();
        public bool loadTextElement { set; get; }
        public bool loadTextMultilineElement { set; get; }
        public bool loadImageElement { set; get; }
        public bool loadStatusElement { set; get; }
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
        public bool resizing { set; get; }
        public int type {  set; get; }
        public Vector2 dragOffset { get; set; }
    }
    public class TextElement : LayoutElement
    {
        public int type { set; get; }
        public string text { set; get; }
        public Vector4 color { set; get; }
    }
    public class ImageData
    {
        public byte[] bytes { set; get; }
        public ImageElement image { set; get; }
    }
    public class ImageElement : LayoutElement
    {
        public byte[] bytes { get; set; }
        public IDalamudTextureWrap textureWrap { set; get; }
        public string tooltip { get; set; }
        public float width { get; set; }
        public float height { get; set; }
        public bool initialized { get; set; }
        public bool proprotionalEditing { set; get; } = true;
        public bool hasTooltip { get; set; }
        internal bool maximizable { get; set; }

    }
    public class IconElement
    {
        public int iconId { set; get; }
        public string name { set; get; }
        public string tooltip { set; get; }
        public float PosX { get; set; }
        public float PosY { get; set; }
    }
    public class StatusElement : LayoutElement
    {
        public string name { set; get; }
        public IDalamudTextureWrap icon { set; get; }
        public float totalPositive {  set; get; }
        public float totalNegative { set; get; }
        public Vector4 positiveCol { set; get; }
        public Vector4 negitiveCol { set; get; }
        public float maxPositive { set; get; }
        public float maxNegitive { set; get; }
        public float progress { set; get; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float width { set; get; }
        public float height { set; get; }
        public bool hasTooltip {  set; get; }
        public string tooltip {  get; set; }
        public bool hasNegative {  set; get; }
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
        private static int? draggingStatusElementID = null;
        private static Vector2 lastMousePosition;
        private static FileDialogManager _fileDialogManager;
        private enum ResizeEdge { None, BottomRight, Bottom, Right }
        private static ResizeEdge currentEdge = ResizeEdge.None;

        public static bool Lockstatus = true;
        public static bool EditStatus = false;
        private static bool showDeleteConfirmationPopup = false;
        public static void AddTextElement(int layoutID, int type, int elementID, bool locked, Plugin plugin)
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
        /*
        public static void AddStatusElement(int layoutID, int elementID, bool locked, Plugin plugin)
        {
            if (!layouts.ContainsKey(layoutID))
            {
                layouts[layoutID] = new Layout
                {
                    id = layoutID,
                    elements = new List<LayoutElement>()
                };
            }

            var statusElement = layouts[layoutID].elements.OfType<StatusElement>().FirstOrDefault(e => e.id == elementID);

            if (statusElement == null || statusElement.canceled)
            {
                if (statusElement != null && statusElement.canceled)
                    return;

                statusElement = new StatusElement
                {
                    id = elementID,
                    icon = WindowOperations.RenderIconAsync(plugin, 1).Result,
                    maxPositive = 100,
                    maxNegitive = 100,
                    totalPositive = 200,
                    totalNegative = 200,
                    width = 200,
                    height = 20,
                    positiveCol = new Vector4(0, 255, 255, 255),
                    negitiveCol = new Vector4(255, 0, 255, 255),
                    PosX = 500,
                    PosY = 500,
                    hasNegative = true,
                    modifying = false,
                };
                layouts[layoutID].elements.Add(statusElement);
            }

            Vector2 mousePos = ImGui.GetMousePos();
            if (!locked)
            {

            }
            if (draggingStatusElementID == elementID)
            {
                if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    statusElement.PosX = mousePos.X - statusElement.dragOffset.X;
                    statusElement.PosY = mousePos.Y - statusElement.dragOffset.Y;
                }
                else if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    draggingStatusElementID = null;
                    plugin.logger.Error($"Text Element {statusElement.id} dropped at position: {statusElement.PosX}, {statusElement.PosY}");
                }
            }

            if (statusElement.modifying)
            {
                ResetToLockedState(layoutID);
                // Editable logic
                RenderEditableStatusElement(layoutID, elementID, plugin, statusElement);
            }
            else
            {
                // Display logic
                RenderDisplayStatusElement(layoutID, elementID, plugin, statusElement);
            }
        }*/

        public static void RenderElements(Layout currentLayout, bool locked, Plugin plugin)
        {
            _fileDialogManager = new FileDialogManager();
            // Render existing text elements in the layout
            if (currentLayout.elements != null)
            {
                for (int i = 0; i < currentLayout.elements.OfType<ImageElement>().Count(); i++)
                {
                    ImageElement imageElement = currentLayout.elements.OfType<ImageElement>().ToArray()[i];
                    if (imageElement != null)
                    {
                        AddImageElement(currentLayout.id, imageElement.id, plugin);

                    }
                }
                for (int i = 0; i < currentLayout.elements.OfType<TextElement>().Count(); i++)
                {
                    TextElement textElement = currentLayout.elements.OfType<TextElement>().ToArray()[i];
                    if (textElement != null)
                    {
                        AddTextElement(currentLayout.id, textElement.type, textElement.id, locked, plugin);
                    }
                }
                /*
                for (int i = 0; i < currentLayout.elements.OfType<StatusElement>().Count(); i++)
                {
                    StatusElement statusElement = currentLayout.elements.OfType<StatusElement>().ToArray()[i];
                    if (statusElement != null)
                    {
                        AddStatusElement(currentLayout.id, statusElement.id, locked, plugin);
                    }
                }*/
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
                    tooltip = "",
                    width = 100,  // Default width
                    height = 100, // Default height
                    PosX = 100,   // Default position
                    PosY = 100
                });
            }
            // Handle adding a new image element
            if (currentLayout.loadStatusElement)
            {
                int newId = currentLayout.elements.Any() ? currentLayout.elements.Max(e => e.id) + 1 : 0;
                currentLayout.loadStatusElement = false;

                currentLayout.elements.Add(new StatusElement
                {
                    id = newId,
                    icon = WindowOperations.RenderIconAsync(plugin, 1).Result,
                    maxPositive = 100,
                    maxNegitive = 100,
                    totalPositive = 200,
                    totalNegative = 200,
                    width = 200,
                    height = 20,
                    positiveCol = new Vector4(0, 255, 255, 255),
                    negitiveCol = new Vector4(255, 0, 255, 255),
                    PosX = 500,
                    PosY = 500,
                    hasNegative = true,
                    modifying = false,
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
                    tooltip = string.Empty,
                    PosX = 100,
                    PosY = 100,
                    locked = true,
                    modifying = false,
                    canceled = false,
                    initialized = false
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
                //ResetToLockedState(layoutID);
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
            ImGui.PushStyleColor(ImGuiCol.Text, textElement.color);
            ImGui.TextUnformatted(textElement.text);
            ImGui.PopStyleColor();
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
        /*
        //Status Elements
        private static void RenderDisplayStatusElement(int layoutID, int elementID, Plugin plugin, StatusElement statusElement)
        {
            // Get the current window position
            var windowPos = ImGui.GetWindowPos();
            var position = new Vector2(statusElement.PosX + windowPos.X, statusElement.PosY + windowPos.Y); // Adjust with window position
            var size = new Vector2(statusElement.width, statusElement.height);

            // Get the window's draw list
            var drawList = ImGui.GetWindowDrawList();

            // **Step 1**: Render the icon (this part doesn't change)
            if (statusElement.icon != null)
            {
                ImGui.SetCursorPos(new Vector2(position.X - 75, position.Y - 75)); // Adjust cursor to place icon
                ImGui.Image(statusElement.icon.ImGuiHandle, new Vector2(50, 50)); // Draw the icon with 50x50 size
            }

            // **Step 2**: Render the draggable shape, starting after the icon
            var barStartPosition = new Vector2(position.X + 50, position.Y); // Start after the icon

            // **Draw the shape relative to the window when locked**
            if (statusElement.locked)
            {
                // This will move with the window when locked
                drawList.AddRectFilled(barStartPosition, barStartPosition + new System.Numerics.Vector2(200, 20),
                    ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(0.0f, 0.0f, 1.0f, 1.0f))); // Blue for the locked shape
            }
            else
            {
                // **Allow dragging the shape when unlocked**
                var mousePos = ImGui.GetMousePos();

                // Start dragging when the mouse is clicked inside the shape
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
                    mousePos.X >= barStartPosition.X && mousePos.X <= barStartPosition.X + 200 &&
                    mousePos.Y >= barStartPosition.Y && mousePos.Y <= barStartPosition.Y + 20)
                {
                    statusElement.dragging = true;
                    statusElement.dragOffset = mousePos - barStartPosition; // Store offset for dragging
                }

                // If dragging, update position
                if (statusElement.dragging)
                {
                    if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                    {
                        statusElement.PosX = mousePos.X - statusElement.dragOffset.X;
                        statusElement.PosY = mousePos.Y - statusElement.dragOffset.Y;
                    }
                    else
                    {
                        statusElement.dragging = false; // Stop dragging on mouse release
                    }
                }

                // Draw the shape at the new position when dragging (will be independent of the window)
                drawList.AddRectFilled(new Vector2(statusElement.PosX, statusElement.PosY),
                    new Vector2(statusElement.PosX + 200, statusElement.PosY + 20),
                    ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(1.0f, 0.0f, 0.0f, 1.0f))); // Red for the unlocked shape
            }

            // **Step 3**: Render the "Edit" button and other UI elements after the shape
            ImGui.SetCursorPos(new Vector2(barStartPosition.X + 220, position.Y)); // Position text after the shapes
            ImGui.TextUnformatted(statusElement.name);

            // Render the "Edit" button
            ImGui.SameLine();
            if (ImGui.Button($"Edit##{layoutID}_{elementID}"))
            {
                statusElement.modifying = true; // Enter editing mode
            }

            // Render the "Lock" or "Unlock" button after the "Edit" button
            ImGui.SameLine();
            if (statusElement.locked)
            {
                if (ImGui.Button($"Unlock##{layoutID}_{elementID}"))
                {
                    statusElement.locked = false; // Unlock the element
                    CheckLockState(layoutID);
                }
            }
            else
            {
                if (ImGui.Button($"Lock##{layoutID}_{elementID}"))
                {
                    statusElement.locked = true; // Lock the element
                    statusElement.dragging = false; // Stop dragging if locked
                    CheckLockState(layoutID);
                }
            }
        }





        private static void RenderEditableStatusElement(int layoutID, int elementID, Plugin plugin, StatusElement statusElement)
        {
            if (statusElement == null)
            {
                plugin.logger.Error($"RenderEditableStatusElement: Null StatusElement for LayoutID {layoutID}, ElementID {elementID}");
                return; // Prevent crashes
            }


            try
            {
                var drawList = ImGui.GetWindowDrawList();
                
                float negativeWidth = Math.Abs(statusElement.totalNegative);
                float positiveWidth = statusElement.totalPositive; // Positive value
                float totalWidth = negativeWidth + positiveWidth;
                Vector2 position = new Vector2(statusElement.PosX, statusElement.PosY);


                Vector4 negativeColor = statusElement.negitiveCol;
                if (ImGui.ColorEdit4($"##Text Input Color {layoutID}_{elementID}", ref negativeColor, ImGuiColorEditFlags.NoInputs))
                {
                    statusElement.negitiveCol = negativeColor;
                }

                ImGui.PushStyleColor(ImGuiCol.Text, statusElement.negitiveCol);
                drawList.AddRectFilled(position - new System.Numerics.Vector2(negativeWidth, statusElement.height), position, ImGui.ColorConvertFloat4ToU32(statusElement.negitiveCol));
                ImGui.PushStyleColor(ImGuiCol.Text, statusElement.positiveCol);
                drawList.AddRectFilled(position - new System.Numerics.Vector2(negativeWidth, 0), position, ImGui.ColorConvertFloat4ToU32(statusElement.positiveCol));
                var whiteRectPosition = new System.Numerics.Vector2(position.X - 3, position.Y - 3); // Offset by 3px
                var whiteRectSize = new System.Numerics.Vector2(totalWidth + 6, statusElement.height + 6); // Add 3px to each side


            }
            catch (Exception ex)
            {
                plugin.logger.Error($"RenderEditableTextElement: Exception occurred - {ex.Message}");
            }
            finally
            {
                ImGui.PopStyleColor();
            }

            ImGui.SameLine();
            Vector4 color = statusElement.negitiveCol;
            if (ImGui.ColorEdit4($"##Text Input Color {layoutID}_{elementID}", ref color, ImGuiColorEditFlags.NoInputs))
            {
                statusElement.negitiveCol = color;
            }

            ImGui.SameLine();
            if (ImGui.Button($"Submit##{layoutID}_{elementID}"))
            {
                statusElement.modifying = false;
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
                    var elementToCancel = layout.elements.OfType<StatusElement>().FirstOrDefault(e => e.id == elementID);
                    if (elementToCancel != null)
                    {
                        elementToCancel.canceled = true;
                        plugin.logger.Error($"Marked Text Element {elementID} as canceled in Layout {layoutID}");
                    }
                }
            });
        }*/


        private static void RenderDisplayImageElement(int layoutID, Plugin plugin, ImageElement imageElement)
        {
            // Render the image at its position
            ImGui.SetCursorPos(new Vector2(imageElement.PosX, imageElement.PosY));    
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
            ImGui.SetCursorPos(new Vector2(imageElement.PosX, imageElement.PosY + 50));
            if (imageElement.textureWrap != null)
            {
                ImGui.Image(imageElement.textureWrap.ImGuiHandle, new Vector2(imageElement.width, imageElement.height));
            }
        }

        public static void DrawImageWithScaling(ImageElement imageElement)
        {
            Vector2 cursorPos = ImGui.GetMousePos();
            float edgeThreshold = 10.0f; // Sensitivity for edge detection

            // Display the image
            ImGui.SetCursorPos(new Vector2(imageElement.PosX, imageElement.PosY +100));
            if(imageElement.textureWrap != null)
            {
                ImGui.Image(imageElement.textureWrap.ImGuiHandle, new Vector2(imageElement.width, imageElement.height));
            }

            // Get the global bounds of the image
            Vector2 imageMin = ImGui.GetItemRectMin();
            Vector2 imageMax = ImGui.GetItemRectMax();

            // Detect edges or corners for resizing
            if (!imageElement.resizing)
            {
                currentEdge = ResizeEdge.None;

                if (cursorPos.X >= imageMax.X - edgeThreshold &&
                    cursorPos.X <= imageMax.X &&
                    cursorPos.Y >= imageMax.Y - edgeThreshold &&
                    cursorPos.Y <= imageMax.Y)
                {
                    currentEdge = ResizeEdge.BottomRight;
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNWSE); // Corner cursor
                }
                else if (cursorPos.X >= imageMax.X - edgeThreshold && cursorPos.X <= imageMax.X)
                {
                    currentEdge = ResizeEdge.Right;
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW); // Horizontal cursor
                }
                else if (cursorPos.Y >= imageMax.Y - edgeThreshold && cursorPos.Y <= imageMax.Y)
                {
                    currentEdge = ResizeEdge.Bottom;
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNS); // Vertical cursor
                }
                else
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Arrow); // Default cursor
                }
            }

            // Start resizing when the left mouse button is clicked
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && currentEdge != ResizeEdge.None)
            {
                imageElement.resizing = true;
                lastMousePosition = cursorPos;
            }

            // Continue resizing as long as the left mouse button is held
            if (imageElement.resizing)
            {
                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    imageElement.resizing = false;
                }
                else
                {
                    Vector2 currentMousePosition = ImGui.GetMousePos();
                    Vector2 dragDelta = currentMousePosition - lastMousePosition;

                    float aspectRatio = imageElement.width / imageElement.height;

                    // Apply resizing logic
                    if (imageElement.proprotionalEditing)
                    {
                        // Proportional scaling
                        if (currentEdge == ResizeEdge.BottomRight || currentEdge == ResizeEdge.Right || currentEdge == ResizeEdge.Bottom)
                        {
                            // Choose the dominant direction for scaling
                            if (Math.Abs(dragDelta.X) > Math.Abs(dragDelta.Y))
                            {
                                float newWidth = Math.Max(50, imageElement.width + dragDelta.X);
                                imageElement.height = newWidth / aspectRatio;
                                imageElement.width = newWidth;
                            }
                            else
                            {
                                float newHeight = Math.Max(50, imageElement.height + dragDelta.Y);
                                imageElement.width = newHeight * aspectRatio;
                                imageElement.height = newHeight;
                            }
                        }
                    }
                    else
                    {
                        // Non-proportional scaling
                        if (currentEdge == ResizeEdge.BottomRight || currentEdge == ResizeEdge.Right)
                        {
                            imageElement.width = Math.Max(50, imageElement.width + dragDelta.X);
                        }
                        if (currentEdge == ResizeEdge.BottomRight || currentEdge == ResizeEdge.Bottom)
                        {
                            imageElement.height = Math.Max(50, imageElement.height + dragDelta.Y);
                        }
                    }

                    // Update the last mouse position
                    lastMousePosition = currentMousePosition;
                }
            }
        }



        private static void RenderEditableImageElement( int layoutID, Plugin plugin, ImageElement imageElement)
        {
            _fileDialogManager.Draw(); //file dialog mainly for avatar atm. galleries later possibly.
            float width = imageElement.width;
            float height = imageElement.height;
            bool hasTooltip = imageElement.hasTooltip;
            bool maximizable = imageElement.maximizable;
            string tooltip = imageElement.tooltip;
            bool proportionalEditing = imageElement.proprotionalEditing;
            ImGui.SetCursorPos(new Vector2(imageElement.PosX, imageElement.PosY));
            EditStatus = true;
            ImGui.Text("Proportional Scaling:");
            ImGui.SameLine();
            if (ImGui.Checkbox($"##{layoutID}_{imageElement.id}", ref proportionalEditing))
            {
                imageElement.proprotionalEditing = proportionalEditing;
            }
            ImGui.SameLine();
            if (ImGui.Button($"Upload##{layoutID}_{imageElement.id}"))
            {
                imageElement.initialized = false;
                UploadImage(plugin, ProfileWindow._fileDialogManager, imageElement);
            }

            ImGui.SameLine();

            // Render the "Submit" button
            if(ImGui.Button($"Submit##{layoutID}_{imageElement.id}"))
            {
                EditStatus = false;
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

            ImGui.SetCursorPos(new Vector2(imageElement.PosX, imageElement.PosY + 50));
            if(ImGui.Checkbox($"Maximizable##{layoutID}_{imageElement.id}", ref maximizable))
            {
                imageElement.maximizable = maximizable;
            }
            if (ImGui.Checkbox($"Has Tooltip##{layoutID}_{imageElement.id}", ref hasTooltip))
            {
                imageElement.hasTooltip = hasTooltip;
            }
            if (imageElement.hasTooltip)
            {
                ImGui.SameLine();
                ImGui.PushItemWidth(300);
                if(ImGui.InputText($"Tooltip##{layoutID}_{imageElement.id}", ref tooltip, 200))
                {
                    imageElement.tooltip = tooltip;
                }
            }
            ImGui.SetCursorPos(new Vector2(imageElement.PosX, imageElement.PosY + 100));
            DrawImageWithScaling(imageElement);
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
                        EditStatus = false;
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
        public static void UploadImage(Plugin plugin, FileDialogManager _fileDialogManager, ImageElement imageElement)
        {
            _fileDialogManager.OpenFileDialog("Select Image", "Image{.png,.jpg}", (s, f) =>
            {
                if (!s)
                    return;
                var imagePath = f[0].ToString();
                var image = Path.GetFullPath(imagePath);
                var imageBytes = File.ReadAllBytes(image);
                if(imagePath == string.Empty || image == null)
                {
                    imageElement.textureWrap = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                    imageElement.width = imageElement.textureWrap.Width;
                    imageElement.height = imageElement.textureWrap.Height;
                }
                else
                {
                    imageElement.bytes = imageBytes;
                    imageElement.textureWrap = Plugin.TextureProvider.CreateFromImageAsync(Imaging.ScaleImageBytes(imageBytes, 1000, 1000)).Result;
                    imageElement.width = imageElement.textureWrap.Width;
                    imageElement.height = imageElement.textureWrap.Height;
                }
                
            }, 0, null, plugin.Configuration.AlwaysOpenDefaultImport);

        }
    }
}
