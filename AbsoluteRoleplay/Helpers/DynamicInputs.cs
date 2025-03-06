using AbsoluteRoleplay.Windows.Profiles;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System.ComponentModel;
using System.Numerics;

namespace AbsoluteRoleplay.Helpers
{
    public class Layout
    {
        internal bool loadEmptyElement;

        public int id { get; set; }
        public string name { get; set; }
        public List<LayoutElement> elements { get; set; } = new List<LayoutElement>(); 
        public TreeNode RootNode { get; set; } = new TreeNode("Root", true); // Each layout has its own tree

        public bool loadTextElement { set; get; }
        public bool loadTextMultilineElement { set; get; }
        public bool loadImageElement { set; get; }
        public bool loadStatusElement { set; get; }
        internal bool loadIconElement { set; get; }
        internal bool loadFolderElement { set; get; }
    }

    public class DateTimeElement
    {
        public int selectedStartYear { get; set; }
        public int selectedEndYear { get; set; }
        public int selectedStartMonth { get; set; }
        public int selectedEndMonth { get; set; }
        public int selectedStartDay { get; set; }
        public int selectedEndDay { get; set; }
        public int selectedStartHour { get; set; }
        public int selectedEndHour { get; set; }
        public int selectedStartMinute { get; set; }
        public int selectedEndMinute { get; set; }
        public int selectedStartAmPm { get; set; }
        public int selectedEndAmPm { get; set; }
        public int selectedStartTimezone { get; set; }
        public int selectedEndTimezone { get; set; }
    }
    public class LayoutElement
    {

        public int layoutID { get; set; }
        public bool IsFolder { get; set; }
        public bool Lockstatus { get; set; } = true;
        public bool EditStatus { get; set; } = false;
        public int id { set; get; }
        public string name { set; get; }
        public bool isBeingRenamed { get; set; } = false; // New flag for renaming
        public bool locked { get; set; } = true;
        public float PosX { get; set; }
        public float PosY { get; set; }
        public bool added { get; set; } = false;
        public bool modifying { set; get; }
        public bool canceled { set; get; } = false;
        public bool dragging { set; get; }
        public bool resizing { set; get; } = false;
        public byte[] tooltipBGBytes { set; get; }
        public IDalamudTextureWrap tooltipBG { set; get; } = null;
        public string tooltipTitle { set; get; } = string.Empty;
        public string tooltipDescription { set; get; } = string.Empty;
        public int type { set; get; }
        public Vector2 dragOffset { get; set; }
        TreeNode parent { get; set; } = null;

    }
    public class FolderElement : LayoutElement
    {
        internal bool loaded { get; set; }

        public int id { set; get; }
        public string text { set; get; }
        public TreeNode parent { get; set; }
    }
    public class EmptyElement : LayoutElement
    {
        internal bool loaded { get; set; }

        public int id { set; get; }
        public string text { set; get; }
        public Vector4 color { set; get; }
        public TreeNode parent { get; set; }
    }
    public class IconElement : LayoutElement
    {
        internal bool loaded { get; set; }

        public enum IconState
        {
            Displaying,
            Modifying
        }
        public IDalamudTextureWrap icon { get; set; }
        public IconState State { get; set; } = IconState.Displaying; // Default state
        public TreeNode parent { get; set; }
    }
    public class TextElement : LayoutElement
    {
        internal bool loaded { get; set; }

        public int type { set; get; }
        public string text { set; get; }
        public Vector4 color { set; get; }
        public TreeNode parent { get; set; }
    }
    public class ImageElement : LayoutElement
    {

        internal bool loaded { get; set; } = false;

        public byte[] bytes { get; set; }
        public IDalamudTextureWrap textureWrap { set; get; } = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
        public string tooltip { get; set; }
        public float width { get; set; }
        public float height { get; set; }
        public bool initialized { get; set; }
        public bool proprotionalEditing { set; get; } = true;
        public bool hasTooltip { get; set; }
        internal bool maximizable { get; set; }
        public TreeNode parent { get; set; }

    }

    internal class DynamicInputs
    {
        public static SortedList<int, object> fieldValues = new SortedList<int, object>();
        public static SortedList<int, Layout> layouts = new SortedList<int, Layout>();
        private static int? draggingTextElementID = null;
        private static int? draggingImageElementID = null;
        private static int? draggingIconElementID = null;
        private static Vector2 lastMousePosition;
        private static FileDialogManager _fileDialogManager;
        private enum ResizeEdge { None, BottomRight, Bottom, Right }
        private static ResizeEdge currentEdge = ResizeEdge.None;


        private static void LoadIconSelection(Plugin plugin, IconElement currentIcon)
        {
            ImGui.Begin("ICONS", ImGuiWindowFlags.AlwaysAutoResize);
            if (!WindowOperations.iconsLoaded)
            {
                WindowOperations.LoadIconsLazy(plugin); // Load a small batch of icons
            }

            WindowOperations.RenderIcons(plugin, false, currentIcon);
            ImGui.End();
        }
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
                    layoutID = layoutID,
                    name = "Text Element",
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

            var mousePos = ImGui.GetMousePos();
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
        public static void RenderElements(Layout currentLayout, bool locked, Plugin plugin)
        {
            _fileDialogManager = new FileDialogManager();
            // Render existing text elements in the layout
            if (currentLayout.elements != null)
            {
                for (var i = 0; i < currentLayout.elements.Count; i++)
                {
                    if (currentLayout.elements[i] is FolderElement folderElement)
                    {
                        if (folderElement != null && !folderElement.loaded)
                        {
                            folderElement.loaded = true;
                            TreeManager.AddNode(folderElement.parent, currentLayout.elements[i], currentLayout, folderElement.name, true);
                        }
                    }
                    if (currentLayout.elements[i] is EmptyElement emptyElement)
                    {
                        if (emptyElement != null && !emptyElement.loaded)
                        {
                            emptyElement.loaded = true;
                            TreeManager.AddNode(emptyElement.parent, emptyElement, currentLayout, emptyElement.name, false);
                        }
                    }
                    if (currentLayout.elements[i] is ImageElement imageElement)
                    {
                        if (imageElement != null)
                        {
                            AddImageElement(currentLayout.id, imageElement.id, plugin);                            
                        }
                        if (!imageElement.loaded)
                        {
                            imageElement.loaded = true;
                            TreeManager.AddNode(imageElement.parent, imageElement, currentLayout, imageElement.name, false);
                        }
                    }
                    if (currentLayout.elements[i] is TextElement textElement)
                    {
                        if (textElement != null)
                        {
                            AddTextElement(currentLayout.id, textElement.type, textElement.id, plugin);
                        }
                        if (!textElement.loaded)
                        {
                            textElement.loaded = true;
                            TreeManager.AddNode(textElement.parent, textElement, currentLayout, textElement.name, false);
                        }
                    }
                    if (currentLayout.elements[i] is IconElement iconElement)
                    {
                        if (iconElement != null)
                        {
                            AddIconElement(currentLayout.id, iconElement.id, plugin);
                        }
                        if (!iconElement.loaded)
                        {
                            iconElement.loaded = true;
                            TreeManager.AddNode(iconElement.parent, iconElement, currentLayout, iconElement.name, false);
                        }
                    }
                }
            }
        }

        public static void ChangeOrderUp(int layoutID, int elementID, Plugin plugin)
        {
            var layout = layouts[layoutID]; // Access the layout by ID
            var index = layout.elements.FindIndex(e => e.id == elementID); // Find the index of the element

            if (index > 0) // Ensure there's an element above to swap with
            {
                // Swap the current element with the one above
                var temp = layout.elements[index];
                layout.elements[index] = layout.elements[index - 1];
                layout.elements[index - 1] = temp;

                plugin.logger.Debug($"Moved element {elementID} up in layout {layoutID}");
            }
            else
            {
                plugin.logger.Debug($"Element {elementID} is already at the top of layout {layoutID}");
            }
        }

        public static void ChangeOrderDown(int layoutID, int elementID, Plugin plugin)
        {
            var layout = layouts[layoutID]; // Access the layout by ID
            var index = layout.elements.FindIndex(e => e.id == elementID); // Find the index of the element

            if (index >= 0 && index < layout.elements.Count - 1) // Ensure there's an element below to swap with
            {
                // Swap the current element with the one below
                var temp = layout.elements[index];
                layout.elements[index] = layout.elements[index + 1];
                layout.elements[index + 1] = temp;

                plugin.logger.Debug($"Moved element {elementID} down in layout {layoutID}");
            }
            else
            {
                plugin.logger.Debug($"Element {elementID} is already at the bottom of layout {layoutID}");
            }
        }

        public static void AddIconElement(int layoutID, int elementID, Plugin plugin)
        {
            if (!layouts.ContainsKey(layoutID))
            {
                layouts[layoutID] = new Layout
                {
                    id = layoutID,
                    elements = new List<LayoutElement>()
                };
            }

            // Retrieve or create the icon element
            var iconElement = layouts[layoutID].elements.OfType<IconElement>().FirstOrDefault(e => e.id == elementID);
            if (iconElement == null || iconElement.canceled)
            {
                if (iconElement != null && iconElement.canceled)
                    return;

                iconElement = new IconElement
                {
                    id = elementID,
                    icon = UI.UICommonImage(UI.CommonImageTypes.blank),
                    PosX = 100, // Default position
                    PosY = 100,
                    layoutID = layoutID,
                    State = IconElement.IconState.Displaying // Default to displaying
                };
                layouts[layoutID].elements.Add(iconElement);
            }

            var mousePos = ImGui.GetMousePos();

            // Handle dragging logic
            // Handle dragging only when unlocked
            if (iconElement.locked == false && iconElement.dragging == true && ImGui.IsWindowFocused())
            {
                if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    iconElement.PosX = mousePos.X - iconElement.dragOffset.X;
                    iconElement.PosY = mousePos.Y - iconElement.dragOffset.Y;
                }
                else if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    iconElement.dragging = false; // Stop dragging on release
                }
            }
            else if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                iconElement.dragging = true; // Start dragging
                iconElement.dragOffset = ImGui.GetMousePos() - new Vector2(iconElement.PosX, iconElement.PosY);
            }
            ImGui.SetCursorPos(new Vector2(iconElement.PosX, iconElement.PosY));
            ImGui.Image(iconElement.icon.ImGuiHandle, new Vector2(ImGui.GetIO().FontGlobalScale * 35));
            
            
            if (iconElement.modifying == true)
            {
                LoadIconSelection(plugin, iconElement);
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
                    PosX = 0,
                    PosY = 0,
                    layoutID = layoutID,
                    locked = true,
                    modifying = false,
                    canceled = false,
                    initialized = false,
                    textureWrap = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab)

                };
                layouts[layoutID].elements.Add(imageElement);
            }

            var mousePos = ImGui.GetMousePos();

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
                // Editable logic
                RenderEditableImageElement(layoutID, plugin, imageElement);
            }
            else
            {
                // Display logic
                RenderDisplayImageElement(layoutID, plugin, imageElement);
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

            if (!textElement.locked && ImGui.IsWindowFocused())
            {               
                // Render "Lock" button
                if (textElement.dragging && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    var windowPos = ImGui.GetWindowPos();
                    var mousePos = ImGui.GetMousePos();
                    textElement.PosX = mousePos.X - textElement.dragOffset.X - windowPos.X;
                    textElement.PosY = mousePos.Y - textElement.dragOffset.Y - windowPos.Y;
                }
                else if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    textElement.dragging = true; // Start dragging
                    var windowPos = ImGui.GetWindowPos();
                    textElement.dragOffset = ImGui.GetMousePos() - new Vector2(textElement.PosX, textElement.PosY) - windowPos;
                }
                else if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    textElement.dragging = false; // Stop dragging on release
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

            var text = textElement.text;
            var color = textElement.color;

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
                ImGui.PopItemWidth();
                ImGui.PopStyleColor();

            }
            catch (Exception ex)
            {
                plugin.logger.Error($"RenderEditableTextElement: Exception occurred - {ex.Message}");
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
           
        }



        private static void RenderDisplayImageElement(int layoutID, Plugin plugin, ImageElement imageElement)
        {

            ImGui.SetCursorPos(new Vector2(imageElement.PosX, imageElement.PosY + 100));
            if (imageElement.textureWrap != null)
            {
                ImGui.Image(imageElement.textureWrap.ImGuiHandle, new Vector2(imageElement.width, imageElement.height));
            }
            if (!imageElement.locked && ImGui.IsWindowFocused())
            {
                if (imageElement.dragging && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    var windowPos = ImGui.GetWindowPos();
                    var mousePos = ImGui.GetMousePos();
                    imageElement.PosX = mousePos.X - imageElement.dragOffset.X - windowPos.X;
                    imageElement.PosY = mousePos.Y - imageElement.dragOffset.Y - windowPos.Y;
                }
                else if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    imageElement.dragging = true; // Start dragging
                    var windowPos = ImGui.GetWindowPos();
                    imageElement.dragOffset = ImGui.GetMousePos() - new Vector2(imageElement.PosX, imageElement.PosY) - windowPos;
                }
                else if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    imageElement.dragging = false; // Stop dragging on release
                }
            }
        }

        public static void DrawImageWithScaling(ImageElement imageElement)
        {
            var cursorPos = ImGui.GetMousePos();
            var edgeThreshold = 10.0f;

            ImGui.SetCursorPos(new Vector2(imageElement.PosX, imageElement.PosY + 100));
            if (imageElement.textureWrap != null)
            {
                ImGui.Image(imageElement.textureWrap.ImGuiHandle, new Vector2(imageElement.width, imageElement.height));
            }

            var imageMin = ImGui.GetItemRectMin();
            var imageMax = ImGui.GetItemRectMax();

            // Edge detection
            if (!imageElement.resizing)
            {
                currentEdge = ResizeEdge.None;

                if (cursorPos.X >= imageMax.X - edgeThreshold && cursorPos.X <= imageMax.X &&
                    cursorPos.Y >= imageMax.Y - edgeThreshold && cursorPos.Y <= imageMax.Y)
                {
                    currentEdge = ResizeEdge.BottomRight;
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNWSE);
                }
                else if (cursorPos.X >= imageMax.X - edgeThreshold && cursorPos.X <= imageMax.X)
                {
                    currentEdge = ResizeEdge.Right;
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
                }
                else if (cursorPos.Y >= imageMax.Y - edgeThreshold && cursorPos.Y <= imageMax.Y)
                {
                    currentEdge = ResizeEdge.Bottom;
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNS);
                }

                Plugin.plugin.logger.Error($"Detected edge: {currentEdge}");
            }

            // Click to start resizing
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                Plugin.plugin.logger.Error("Mouse Click Detected!");
            }

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && currentEdge != ResizeEdge.None)
            {
                Plugin.plugin.logger.Error($"Starting resize! Edge: {currentEdge}");
                imageElement.resizing = true;
                imageElement.locked = true;
                lastMousePosition = cursorPos;
            }

            // Continue resizing
            if (imageElement.resizing)
            {
                imageElement.locked = true;
                Plugin.plugin.logger.Error("Resizing in progress...");

                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    Plugin.plugin.logger.Error("Resizing ended.");
                    imageElement.resizing = false;
                    imageElement.locked = false;
                }
                else
                {
                    var currentMousePosition = ImGui.GetMousePos();
                    var dragDelta = currentMousePosition - lastMousePosition;

                    var aspectRatio = imageElement.width / imageElement.height;

                    if (imageElement.proprotionalEditing)
                    {
                        if (currentEdge == ResizeEdge.BottomRight || currentEdge == ResizeEdge.Right || currentEdge == ResizeEdge.Bottom)
                        {
                            if (Math.Abs(dragDelta.X) > Math.Abs(dragDelta.Y))
                            {
                                var newWidth = Math.Max(50, imageElement.width + dragDelta.X);
                                imageElement.height = newWidth / aspectRatio;
                                imageElement.width = newWidth;
                            }
                            else
                            {
                                var newHeight = Math.Max(50, imageElement.height + dragDelta.Y);
                                imageElement.width = newHeight * aspectRatio;
                                imageElement.height = newHeight;
                            }
                        }
                    }
                    else
                    {
                        if (currentEdge == ResizeEdge.BottomRight || currentEdge == ResizeEdge.Right)
                        {
                            imageElement.width = Math.Max(50, imageElement.width + dragDelta.X);
                        }
                        if (currentEdge == ResizeEdge.BottomRight || currentEdge == ResizeEdge.Bottom)
                        {
                            imageElement.height = Math.Max(50, imageElement.height + dragDelta.Y);
                        }
                    }

                    lastMousePosition = currentMousePosition;
                    Plugin.plugin.logger.Error($"Resizing - New size: {imageElement.width}x{imageElement.height}");
                }
            }
        }




        private static void RenderEditableImageElement(int layoutID, Plugin plugin, ImageElement imageElement)
        {
            _fileDialogManager.Draw(); //file dialog mainly for avatar atm. galleries later possibly.
            var width = imageElement.width;
            var height = imageElement.height;
            var hasTooltip = imageElement.hasTooltip;
            var maximizable = imageElement.maximizable;
            var tooltip = imageElement.tooltip;
            var proportionalEditing = imageElement.proprotionalEditing;

            ImGui.SetCursorPos(new Vector2(imageElement.PosX, imageElement.PosY));
            DrawImageWithScaling(imageElement);
            ImGui.SetCursorPos(new Vector2(imageElement.PosX, imageElement.PosY + ImGui.GetIO().FontDefault.FontSize * 8));

            imageElement.EditStatus = true;
            ImGui.SameLine();
            if (Misc.DrawButton($"Upload##{layoutID}_{imageElement.id}"))
            {
                imageElement.initialized = false;
                UploadImage(plugin, ProfileWindow._fileDialogManager, imageElement);
            }

            ImGui.SameLine();
            // Render the "Submit" button
            if (Misc.DrawButton($"Submit##{layoutID}_{imageElement.id}"))
            {
                imageElement.modifying = false; // Change mode to display
            }
            ImGui.SetCursorPos(new Vector2(imageElement.PosX, imageElement.PosY + ImGui.GetIO().FontDefault.FontSize * 10));
            if (ImGui.Checkbox($"Maximizable##{layoutID}_{imageElement.id}", ref maximizable))
            {
                imageElement.maximizable = maximizable;
            }
            ImGui.SameLine();
            if (ImGui.Checkbox($"Proportional Scaling##{layoutID}_{imageElement.id}", ref proportionalEditing))
            {
                imageElement.proprotionalEditing = proportionalEditing;
            }
            if (!imageElement.locked && !imageElement.resizing && ImGui.IsWindowFocused())
            {
                // Handle dragging only when unlocked
                if (imageElement.dragging && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    var windowPos = ImGui.GetWindowPos();
                    var mousePos = ImGui.GetMousePos();
                    imageElement.PosX = mousePos.X - imageElement.dragOffset.X - windowPos.X;
                    imageElement.PosY = mousePos.Y - imageElement.dragOffset.Y - windowPos.Y;
                }
                else if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    imageElement.dragging = true; // Start dragging
                    var windowPos = ImGui.GetWindowPos();
                    imageElement.dragOffset = ImGui.GetMousePos() - new Vector2(imageElement.PosX, imageElement.PosY) - windowPos;
                }
                else if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    imageElement.dragging = false; // Stop dragging on release
                }
            }

        }
        public static void ResetToLockedState(int layoutID, bool resetDragging = true)
        {
            var layout = layouts[layoutID];

            foreach (var element in layout.elements)
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
        public static bool AnyLockedOrScaling(Layout layout)
        {
            return layout.elements != null && layout.elements.Any(h => h.locked || h.resizing);
        }
      
        public static bool CheckEditState(int layoutID)
        {
            var layout = layouts[layoutID];
            if (layout != null)
            {
                if (layout.elements.Count > 0)
                {
                    return layout.elements.All(e => e.modifying);
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
        public static bool CheckScaleState(int layoutID)
        {
            var layout = layouts[layoutID];
            if (layout != null)
            {
                if (layout.elements.Count > 0)
                {
                    return layout.elements.Any(e => e is ImageElement && e.resizing);
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
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
                if (imagePath == string.Empty || image == null)
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
