using AbsoluteRoleplay.Windows.Profiles;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;


namespace AbsoluteRoleplay.Helpers
{
    public class TreeManager
    {
        public static TreeNode Root { get; } = new TreeNode("Root", true);
        public static TreeNode? nodeToDelete = null;
        public static TreeNode? nodeToRename = null;
        private static TreeNode? draggedNode = null;
        private static string renameBuffer = "";
        private static TreeNode? DraggedNode = null;
        private static TreeNode? DraggedNodeParent = null;
        internal static bool clearTree = false;
        public static int? pendingLayoutID = null;
        public static int? pendingElementID = null;
        public static bool showDeleteConfirmationPopup = false;
        public static bool Move;
        private static bool pendingDeletePopup; 
        private static List<TreeNode> nodesToDelete = new List<TreeNode>();
        public static IDalamudTextureWrap editTex = UI.UICommonImage(UI.CommonImageTypes.edit); 
        public static uint transparentColor = ImGui.GetColorU32(new System.Numerics.Vector4(0.0f, 0.0f, 0.0f, 0.0f)); 
        public static Plugin plugin { get; set; }
        public static void AddNode(TreeNode parent, LayoutElement relation, Layout layout, string name, bool isFolder)
        {
            if (relation == null)
            {
                plugin.logger.Error($"AddNode called with null element for '{name}'");
                return;
            }

            if (parent == null)
            {
                plugin.logger.Error($"Parent node is null for '{name}', assigning to root.");
                parent = layout.RootNode; // Ensure a valid parent
            }

            // Prevent duplicate nodes
            if (parent.Children.Any(child => child.Name == name))
            {
                plugin.logger.Error($"A node with name '{name}' already exists under '{parent.Name}'");
                return;
            }
            TreeNode node = new TreeNode(name, isFolder, parent);
            node.layoutID = layout.id;
            node.relatedElement = relation;
            parent.AddChild(node);
        }
        
        public static void RenderTree(Layout layout)
        {
            using (var table = ImRaii.Table("ElementsTable", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV))
            {
                if (table != null)
                {
                    // Set up columns, first column is 200px wide
                    ImGui.TableSetupColumn("##Elements", ImGuiTableColumnFlags.WidthStretch, 0f);
                    ImGui.TableSetupColumn("##Controls", ImGuiTableColumnFlags.WidthStretch, 0f);
                    
                    RenderNode(layout.RootNode, layout);
                    if (nodesToDelete.Count > 0)
                    {
                        plugin.logger.Error($"Processing {nodesToDelete.Count} node deletions.");

                        foreach (var node in nodesToDelete)
                        {
                            if (node.Parent != null)
                            {
                                node.Parent.Children.Remove(node);
                                plugin.logger.Error($"Deleted '{node.Name}' from '{node.Parent.Name}'.");
                            }
                        }
                        nodesToDelete.Clear(); // Clear the list after deleting
                    }
                    if (QueuedMove.HasValue)
                    {
                        var (node, newParent) = QueuedMove.Value;

                        if (node != null && newParent != null)
                        {
                            node.Parent?.Children.Remove(node); // Remove from old parent
                            newParent.AddChild(node);
                            node.Parent = newParent;

                            plugin.logger.Error($"DEBUG: Successfully moved '{node.Name}' to '{newParent.Name}'.");
                        }

                        QueuedMove = null; // Clear queued move after execution
                    }
                }
            }

        }

        private static void RenderNode(TreeNode node, Layout layout)
        {           
            ImGui.PushID(node.ID);
            var drawList = ImGui.GetWindowDrawList();
            ImGui.TableNextColumn();
            // Handle Renaming
            if (node.IsBeingRenamed)
            {
                if (ImGui.InputText($"##rename_{node.ID}", ref renameBuffer, 100, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    node.Name = renameBuffer;
                    node.IsBeingRenamed = false;
                }
                ImGui.TableNextColumn();

                drawList.AddRectFilled(new Vector2(ImGui.GetIO().FontGlobalScale * 15), new Vector2(ImGui.GetIO().FontGlobalScale * 15), transparentColor);
            }
            else
            {
                // Render folders or files
                ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnArrow;
                if (!node.IsFolder) flags |= ImGuiTreeNodeFlags.Leaf;

                bool opened = node.IsFolder ? ImGui.TreeNodeEx(node.Name, flags) : ImGui.Selectable(node.Name);

                //add edit button
                // Right-Click Context Menu
                HandleContextMenu(node, layout);

                // Drag & Drop Logic
                HandleDragDrop(node);

                ImGui.TableNextColumn();
                if (!IsEditable(node, layout.RootNode))
                {
                    drawList.AddRectFilled(new Vector2(ImGui.GetIO().FontGlobalScale * 15), new Vector2(ImGui.GetIO().FontGlobalScale * 15), transparentColor);
                }
                else
                {

                    if (ImGui.ImageButton(node.editBtn.ImGuiHandle, new Vector2(ImGui.GetIO().FontGlobalScale * 15)))
                    {
                        if (node.relatedElement != null)  // Prevents null reference crash
                        {
                            ProfileWindow.currentElement = node.relatedElement;
                            node.relatedElement.modifying = true;
                        }
                    }
                    if(ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Edit Element");
                    }
                    ImGui.SameLine();

                    if(node.relatedElement.locked == true)
                    {
                        if (ImGui.ImageButton(node.moveBtn.ImGuiHandle, new Vector2(ImGui.GetIO().FontGlobalScale * 15)))
                        {
                            node.relatedElement.locked = false;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Move Element");
                        }
                    }
                    else
                    {
                        if (ImGui.ImageButton(node.moveCancelBtn.ImGuiHandle, new Vector2(ImGui.GetIO().FontGlobalScale * 15)))
                        {
                            node.relatedElement.locked = true;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Stop Moving Element");
                        }
                    }
                       

                }
                if (opened && node.IsFolder)
                {
                    foreach (var child in node.Children)
                    {
                        RenderNode(child, layout);
                    }
                }
            }
            RenderDeleteConfirmationPopup(() =>
            {
                if (nodeToDelete != null && nodeToDelete.relatedElement != null)
                {
                    List<TreeNode> allChildren = new List<TreeNode>();

                    //Collect all children before modifying the tree
                    void CollectChildren(TreeNode node)
                    {
                        foreach (var child in node.Children)
                        {
                            allChildren.Add(child);
                            CollectChildren(child); //Recursively collect all descendants
                        }
                    }

                    CollectChildren(nodeToDelete);

                    //Delete all children first
                    foreach (var child in allChildren)
                    {
                        plugin.logger.Error($"Deleting child '{child.Name}'");
                        DeleteNode(child, layout);
                        child.relatedElement.canceled = true;
                    }

                    //Now delete the main node
                    plugin.logger.Error($"Deleting '{nodeToDelete.Name}'");
                    DeleteNode(nodeToDelete, layout);
                    nodeToDelete.relatedElement.canceled = true;
                    nodeToDelete = null;
                }

            });

            ImGui.PopID();
            if (showTooltipPopup)
            {
                LoadTooltipCreation(plugin, layout, node.relatedElement);
            }
        }

        private static bool IsEditable(TreeNode node, TreeNode root)
        {
            if(node.relatedElement is FolderElement || node ==root || node.relatedElement is EmptyElement)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private static void HandleContextMenu(TreeNode parentNode, Layout layout)
        {

            if (ImGui.BeginPopupContextItem())
            {

                if (parentNode.relatedElement is FolderElement || parentNode == layout.RootNode)
                {
                    ImGui.Text("===CREATE===");
                    ImGui.Separator();
                    
                    if (ImGui.MenuItem("New Folder"))
                    { 
                        var newId = layout.elements.Any() ? layout.elements.Max(e => e.id) + 1 : 0;
                        layout.elements.Add(new FolderElement
                        {
                            id = newId,
                            name = $"New Folder {newId}",
                            parent = parentNode,
                        });
                    }

                    if (ImGui.MenuItem("New Empty"))
                    {
                        var newId = layout.elements.Any() ? layout.elements.Max(e => e.id) + 1 : 0;
                        layout.elements.Add(new EmptyElement
                        {
                            id = newId,
                            name = $"====EMPTY====",
                            parent = parentNode,
                        });
                    }
                    if (ImGui.MenuItem("New Text"))
                    {
                        var newId = layout.elements.Any() ? layout.elements.Max(e => e.id) + 1 : 0;
                        layout.elements.Add(new TextElement
                        {
                            id = newId,
                            name = $"Text Element {newId}",
                            text = $"Text Element {newId}",
                            type = 0,
                            color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f), // Default to white color
                            parent = parentNode,
                        });
                    }
                    if (ImGui.MenuItem("New Multiline Text"))
                    {
                        var newId = layout.elements.Any() ? layout.elements.Max(e => e.id) + 1 : 0;
                        layout.elements.Add(new TextElement
                        {
                            id = newId,
                            name = $"Multiline Element {newId}",
                            text = $"Text Multiline Element {newId}",
                            type = 1,
                            color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f),
                            parent = parentNode,
                        });
                    }
                    if (ImGui.MenuItem("New Image"))
                    {
                        var newId = layout.elements.Any() ? layout.elements.Max(e => e.id) + 1 : 0;
                        layout.elements.Add(new ImageElement
                        {
                            id = newId,
                            name = $"Image Element {newId}",
                            tooltip = "",
                            width = 100,  // Default width
                            height = 100, // Default height
                            PosX = 100,   // Default position
                            PosY = 100,
                            parent = parentNode,
                        });
                    }
                    if (ImGui.MenuItem("New Icon"))
                    {
                        var newId = layout.elements.Any() ? layout.elements.Max(e => e.id) + 1 : 0;
                        layout.loadIconElement = false;
                        layout.elements.Add(new IconElement
                        {
                            id = newId,
                            name = $"Icon Element {newId}",
                            icon = UI.UICommonImage(UI.CommonImageTypes.blank),
                            parent = parentNode,

                        });
                    }
                }
                ImGui.Separator();
                if (parentNode.relatedElement is not FolderElement && parentNode != layout.RootNode)
                {
                    if (ImGui.MenuItem("Set Tooltip"))
                    {
                        if (parentNode.relatedElement != null && !showTooltipPopup)  // Prevents null reference crash
                        {
                            showTooltipPopup = true;
                            parentNode.relatedElement.tooltipTitle = string.Empty;
                            parentNode.relatedElement.tooltipDescription = string.Empty;
                        }
                    }
                }
                if (parentNode.relatedElement is not FolderElement && parentNode != layout.RootNode)
                {
                    if (ImGui.MenuItem("Edit"))
                    {
                        if (parentNode.relatedElement != null)  // Prevents null reference crash
                        {
                            ProfileWindow.currentElement = parentNode.relatedElement;
                            parentNode.relatedElement.modifying = true;
                        }
                    }
                }
                if (parentNode != layout.RootNode)
                {
                    if (ImGui.MenuItem("Rename"))
                    {
                        nodeToRename = parentNode;
                        renameBuffer = parentNode.Name;
                        parentNode.IsBeingRenamed = true;
                    }
                    if (ImGui.MenuItem("Delete"))
                    {
                        if (parentNode.relatedElement != null)  // Prevents null reference crash
                        {
                            plugin.logger.Error("Delete button clicked - Setting delete confirmation flag.");

                            pendingDeletePopup = true;  // Ensure popup opens in the next frame
                            showDeleteConfirmationPopup = true; // Keep the popup state active
                            pendingLayoutID = layout.id;
                            pendingElementID = parentNode.relatedElement.id;
                            nodeToDelete = parentNode; // Store the node to be deleted
                        }
                    }
                }




                ImGui.EndPopup();
            }
        }
       
        public static void LoadTooltipCreation(Plugin plugin, Layout layout, LayoutElement element)
        {
            ImGui.Begin("Tooltip", ref showTooltipPopup, ImGuiWindowFlags.AlwaysAutoResize);
            ImGui.Text("Title");
            ImGui.Text("Description");
            ImGui.End();
        }




        public static void RenderDeleteConfirmationPopup(Action onConfirm)
        {
            if (pendingDeletePopup)
            {
                ImGui.OpenPopup("Delete Confirmation");
                pendingDeletePopup = false;
            }

            if (ImGui.BeginPopupModal("Delete Confirmation", ref showDeleteConfirmationPopup, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text("Are you sure you want to delete this element?");
                if(nodeToDelete.relatedElement is FolderElement)
                {                    
                    ImGui.TextColored(new Vector4(1,0,0,1), "The folder will be deleted with all the elements within it");
                }
                ImGui.Separator();

                if (ImGui.Button("Yes"))
                {
                    if (nodeToDelete != null && nodeToDelete.relatedElement != null)
                    {
                        plugin.logger.Error($"Queuing '{nodeToDelete.Name}' for deletion.");

                        // Queue node for deletion instead of removing immediately
                        nodesToDelete.Add(nodeToDelete);
                        if (nodeToDelete.relatedElement is FolderElement)
                        {
                            RemoveChildrenOfFolder(nodeToDelete);
                        }

                        nodeToDelete.relatedElement.canceled = true;
                        nodeToDelete = null;
                    }

                    showDeleteConfirmationPopup = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();

                if (ImGui.Button("No"))
                {
                    plugin.logger.Error("Delete canceled.");
                    showDeleteConfirmationPopup = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }


        public static void RemoveChildrenOfFolder(TreeNode folder)
        {            
            foreach (TreeNode node in folder.Children)
            {
               
                if (node.relatedElement != null)
                {
                    if(node.relatedElement is FolderElement)
                    {
                        RemoveChildrenOfFolder(node);
                    }
                    node.relatedElement.canceled = true;
                }

            }
        }


        private static bool IsAncestor(TreeNode potentialParent, TreeNode child)
        {
            TreeNode? current = child;

            while (current != null)
            {
                if (current == potentialParent)
                    return true; // Found a loop, prevent the move

                current = current.Parent; // Move up the tree
            }

            return false;
        }
        private static unsafe void HandleDragDrop(TreeNode node)
        {
            if (node == null)
            {
                plugin.logger.Error("node is null in HandleDragDrop!");
                return;
            }
           
            if (ImGui.BeginDragDropSource())
            {
                DraggedNode = node;
                DraggedNodeParent = node.Parent;

                unsafe
                {
                    int nodeId = node.ID;
                    ImGui.SetDragDropPayload("TREE_NODE_MOVE", new IntPtr(&nodeId), sizeof(int));
                }

                ImGui.Text($"Dragging '{node.Name}'");
                ImGui.EndDragDropSource();
                plugin.logger.Error($"DEBUG: Started dragging '{DraggedNode.Name}'.");
            }

            if (node.IsFolder && ImGui.BeginDragDropTarget())
            {
                ImGuiPayloadPtr payload = ImGui.AcceptDragDropPayload("TREE_NODE_MOVE");

                if (payload.NativePtr == null)
                {
                    plugin.logger.Error("DEBUG: Drag & Drop payload is NOT valid.");
                    ImGui.EndDragDropTarget();
                    return;
                }

                unsafe
                {
                    int sourceNodeId = *(int*)payload.Data.ToPointer();

                    if (DraggedNode == null)
                    {
                        plugin.logger.Error("ERROR: 'DraggedNode' is null when trying to move!");
                        ImGui.EndDragDropTarget();
                        return;
                    }

                    plugin.logger.Error($"DEBUG: Dropping '{DraggedNode.Name}' into '{node.Name}'.");
                    MoveDraggedNode(node);
                }

                ImGui.EndDragDropTarget();
            }
        }








        private static void MoveDraggedNode(TreeNode newParent)
        {
            if (DraggedNode == null)
            {
                plugin.logger.Error("ERROR: 'DraggedNode' is null in MoveDraggedNode!");
                return;
            }

            if (newParent == null|| IsAncestor(DraggedNode, newParent))
            {
                plugin.logger.Error("ERROR: 'newParent' is null in MoveDraggedNode!");
                return;
            }

            // 🚨 Prevent moving into itself or redundant moves
            if (DraggedNode == newParent || DraggedNode.Parent == newParent)
            {
                plugin.logger.Error($"DEBUG: Skipping move - '{DraggedNode.Name}' is already in '{newParent.Name}'.");
                DraggedNode = null;
                return;
            }

            // 🚀 Queue the move to avoid modifying the tree while rendering
            QueuedMove = (DraggedNode, newParent);
            plugin.logger.Error($"DEBUG: Queued '{DraggedNode.Name}' to move into '{newParent.Name}' after rendering.");

            DraggedNode = null;
        }

        private static (TreeNode? node, TreeNode? newParent)? QueuedMove = null;
        private static bool pendingTooltipPopup = false;

        public static int TooltipLayoutId { get; private set; }

        private static LayoutElement tooltipNode;
        private static int tooltipNodeId;
        private static int nodeToRenameLayout;
        private static bool showTooltipPopup;

        public static void DeleteNode(TreeNode node, Layout layout)
        {
            if (node == null) return;

            plugin.logger.Error($"[DEBUG] Queuing '{node.Name}' for deletion.");

            // ✅ Remove from parent before queuing for deletion
            if (node.Parent != null)
            {
                node.Parent.Children.Remove(node);
                plugin.logger.Error($"✅ '{node.Name}' removed from '{node.Parent.Name}'");
            }

            // ✅ Remove from layout elements
            layout.elements.Remove(node.relatedElement);

            // ✅ Queue for safe deletion in 'RenderTree'
            nodesToDelete.Add(node);
        }


        private static bool RemoveFromParent(TreeNode parent, TreeNode nodeToRemove)
        {
            if (parent.Children.Remove(nodeToRemove))
                return true;

            foreach (var child in parent.Children)
            {
                if (child.IsFolder && RemoveFromParent(child, nodeToRemove))
                    return true;
            }
            return false;
        }
        public static void UploadImage(Plugin plugin, FileDialogManager _fileDialogManager, IDalamudTextureWrap tex, byte[] bytes)
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
                    tex = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
                }
                else
                {
                    bytes = imageBytes;
                    tex = Plugin.TextureProvider.CreateFromImageAsync(Imaging.ScaleImageBytes(imageBytes, 500, 500)).Result;
                }

            }, 0, null, plugin.Configuration.AlwaysOpenDefaultImport);

        }
    }

    public class TreeNode
    {
        public string Name { get; set; }
        public bool IsFolder { get; set; }
        public List<TreeNode> Children { get; } = new List<TreeNode>();
        public TreeNode? Parent { get; set; } // Needed for Drag & Drop
        public LayoutElement relatedElement { get; set; } 
        public int ID { get; } // Unique ID for ImGui elements
        public bool IsBeingRenamed { get; set; } = false;
        public IDalamudTextureWrap editBtn = UI.UICommonImage(UI.CommonImageTypes.edit);
        public IDalamudTextureWrap moveBtn = UI.UICommonImage(UI.CommonImageTypes.move);
        public IDalamudTextureWrap moveCancelBtn = UI.UICommonImage(UI.CommonImageTypes.move_cancel);
        public int layoutID { get; set; }

        private static int nextID = 0;

        public TreeNode(string name, bool isFolder, TreeNode? parent = null)
        {
            Name = name;
            IsFolder = isFolder;
            Parent = parent;
            ID = nextID++;
        }

        public void AddChild(TreeNode child)
        {
            child.Parent = this;
            Children.Add(child);
        }
    }
   
}
