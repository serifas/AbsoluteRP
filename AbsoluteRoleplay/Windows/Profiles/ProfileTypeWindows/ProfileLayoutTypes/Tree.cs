using AbsoluteRP.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using static AbsoluteRP.UI;
using static TreeLayout;
namespace AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes
{
    internal class Tree
    {
        private static (int x, int y)? editingSlot = null;
        private static string editingName = string.Empty;
        private static string editingDescription = string.Empty;
        public static int currentAlignment = (int)Alignments.None;
        public static bool loadTooltip = false;
        public static bool defaultTooltip = true;
        // Add this field to your class to track tooltip visibility and content
        public static bool showRelationshipInputWindow = false;
        private static Vector4 nameColor = new Vector4(1, 1, 1, 1);
        private static Vector4 descriptionColor = new Vector4(1, 1, 1, 1);
        private static (int x, int y)? lastTooltipSlot = null;
        private static bool firstLoad = true;

        private static bool learnedToggle = true; // Default: not learned
        private static bool viewable = true;

        public static bool IconSelection { get; private set; }

        public static void RenderTreeLayout(int index, bool self, string id, TreeLayout layout, string name, Vector4 titleColor)
        {
            /*
            if (self)
            {
                ImGui.Checkbox($"Viewable##Viewable{layout.id}", ref viewable);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("If checked, this tab will be viewable by others.\nIf unchecked, it will not be displayed.");
                }
            }*/

            ImGui.Spacing();

            if (!self)
            {
                Misc.SetTitle(Plugin.plugin, true, name, titleColor);
                ImGui.Spacing();
                ImGui.Separator();
            }
            const float containerPadding = 25f;
            var drawList = ImGui.GetWindowDrawList();
            Vector2 containerSize = ImGui.GetContentRegionAvail() - new Vector2(containerPadding * 2, containerPadding * 2);
            const int gridSizeX = 5;
            const int gridSizeY = 8;
            const float baseRadius = 25f;
            float fontScale = ImGui.GetIO().FontGlobalScale;
            float scaleReduction = 0.7f;

            float radius = baseRadius * fontScale * scaleReduction;
            float ringRadius = (baseRadius + 10) * fontScale * scaleReduction;
            float ringThickness = 7.0f * fontScale * scaleReduction;
            float iconScale = 0.84f;
            float iconSize = ringRadius * 2 * iconScale;

            float ringMargin = ringRadius * 2;
            float availableWidth = containerSize.X - ringMargin;
            float availableHeight = containerSize.Y - ringMargin;
            float spacingX = MathF.Max(100f, availableWidth / (gridSizeX - 1));
            float spacingY = MathF.Max(100f, availableHeight / (gridSizeY - 1));
            float gridWidth = (gridSizeX - 1) * spacingX + ringRadius * 2;
            float gridHeight = (gridSizeY - 1) * spacingY + ringRadius * 2;
            float offsetX = MathF.Max(0, (containerSize.X - gridWidth) / 2f);
            float offsetY = MathF.Max(0, (containerSize.Y - gridHeight) / 2f);

            Vector2 startPos = ImGui.GetCursorScreenPos() + new Vector2(offsetX, offsetY);
            int centerX = gridSizeX / 2;
            int centerY = gridSizeY / 2;
            float scaleX = spacingX / 100f;
            float scaleY = spacingY / 100f;
            float scale = MathF.Min(scaleX, scaleY);
            var centerSlot = (centerX, centerY);

            // Defensive: Ensure layout lists are initialized
            layout.Paths ??= new List<List<(int x, int y)>>();
            layout.PathConnections ??= new List<List<((int x, int y) from, (int x, int y) to)>>();
            layout.relationships ??= new List<Relationship>();

            // Initialize paths if empty
            if (layout.Paths.Count == 0)
            {
                layout.Paths.Add(new List<(int x, int y)> { centerSlot });
                layout.PathConnections.Add(new List<((int x, int y) from, (int x, int y) to)>());
                layout.SelectedSlot = centerSlot;
                layout.PreviousSlot = null;
                layout.CurrentPathIndex = 0;
            }

            // Get all first circles directly outside the center
            var firstLayer = new List<(int x, int y)>();
            foreach (var (dx, dy) in new[] {
        (-1, 0), (1, 0), (0, -1), (0, 1),
        (-1, -1), (1, -1), (-1, 1), (1, 1)
    })
            {
                firstLayer.Add((centerX + dx, centerY + dy));
            }

            // Enabled slots: center, first layer, and all slots in all paths
            var enabledSlots = new HashSet<(int x, int y)>(firstLayer) { centerSlot };
            foreach (var path in layout.Paths)
                foreach (var slot in path)
                    enabledSlots.Add(slot);

            // If a slot is selected, enable its adjacent slots
            if (layout.SelectedSlot.HasValue)
            {
                var sel = layout.SelectedSlot.Value;
                foreach (var (dx, dy) in new[] {
            (-1, 0), (1, 0), (0, -1), (0, 1),
            (-1, -1), (1, -1), (-1, 1), (1, 1)
        })
                {
                    enabledSlots.Add((sel.x + dx, sel.y + dy));
                }
            }

            // Track hovered break target for red line preview
            (int x, int y)? hoveredBreakTarget = null;
            (int x, int y)? hoveredBreakSource = null;

            // --- Draw all connections for all paths, with red preview if hovered for break ---
            foreach (var pathConnections in layout.PathConnections)
            {
                foreach (var conn in pathConnections)
                {
                    Vector2 fromCenter = startPos + new Vector2(conn.from.x * spacingX + ringRadius, conn.from.y * spacingY + ringRadius);
                    Vector2 toCenter = startPos + new Vector2(conn.to.x * spacingX + ringRadius, conn.to.y * spacingY + ringRadius);
                    // Draw red if hovered for break
                    if (layout.CurrentAction == RelationshipAction.Break
                        && hoveredBreakTarget.HasValue
                        && hoveredBreakSource.HasValue
                        && conn.from == hoveredBreakSource.Value
                        && conn.to == hoveredBreakTarget.Value)
                    {
                        drawList.AddLine(fromCenter, toCenter, ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.2f, 0.2f, 1f)), 6f);
                    }
                    else
                    {
                        var relFrom = layout.relationships.FirstOrDefault(r => r.Slot.HasValue && r.Slot.Value == conn.from);
                        var relTo = layout.relationships.FirstOrDefault(r => r.Slot.HasValue && r.Slot.Value == conn.to);
                        bool fromLearned = relFrom?.active ?? false;
                        bool toLearned = relTo?.active ?? false;

                        Vector4 lineColor = (fromLearned && toLearned)
                            ? new Vector4(1f, 1f, 0.3f, 1f)
                            : new Vector4(0.5f, 0.5f, 0.5f, 1f);

                        drawList.AddLine(fromCenter, toCenter, ImGui.ColorConvertFloat4ToU32(lineColor), 6f);
                    }
                }

                if (showRelationshipInputWindow)
                {
                    ImGui.SetNextWindowSize(new Vector2(400, 250), ImGuiCond.FirstUseEver);

                    // 1. Determine which slot/node is being edited
                    var editSlot = layout.ActionSourceSlot ?? layout.SelectedSlot;
                    if (editSlot == null)
                    {
                        ImGui.Text("No node selected.");
                        ImGui.End();
                        return;
                    }

                    // 2. Find or create the relationship for this slot
                    var rel = layout.relationships.FirstOrDefault(r => r.Slot.HasValue && r.Slot.Value == editSlot.Value);
                    bool isNew = false;
                    if (rel == null)
                    {
                        rel = new Relationship
                        {
                            Slot = editSlot.Value,
                            Name = string.Empty,
                            NameColor = new Vector4(1, 1, 1, 1),
                            Description = string.Empty,
                            DescriptionColor = new Vector4(1, 1, 1, 1),
                            IconID = 0,
                            IconTexture = null,
                            ImageTexture = null,
                            Links = new List<RelationshipLink>(),
                            LineColor = new Vector4(1, 1, 0.3f, 1),
                            LineThickness = 6f
                        };
                        isNew = true;
                    }
                    else
                    {
                        // Always update the slot to match the current editSlot
                        rel.Slot = editSlot.Value;
                    }
                    if (self)
                    {
                        if (ImGui.Begin("Create", ref showRelationshipInputWindow, ImGuiWindowFlags.AlwaysAutoResize))
                        {
                            var SlotEdit = layout.ActionSourceSlot ?? layout.SelectedSlot;
                            if (SlotEdit == null)
                            {
                                ImGui.Text("No node selected.");
                                ImGui.End();
                                return;
                            }

                            // Always update the slot to match the current edit slot
                            rel.Slot = SlotEdit.Value;

                            // Only update editingName/Description if the slot changed or window just opened
                            if (editingSlot != SlotEdit)
                            {
                                editingName = rel?.Name ?? string.Empty;
                                editingDescription = rel?.Description ?? string.Empty;
                                learnedToggle = rel?.active ?? false; // <-- Set toggle from relationship
                                editingSlot = SlotEdit;
                            }

                            // Now use editingName/Description for ImGui input
                            ImGui.Text("NAME:");
                            if (ImGui.InputText("##RelationshipName", ref editingName, 200))
                            {
                                if (rel != null) rel.Name = editingName;
                            }
                            ImGui.SameLine();
                            ImGui.Checkbox("Active", ref learnedToggle);
                            ImGui.Text("DESCRIPTION:");
                            if (ImGui.InputTextMultiline("##RelationshipDescription", ref editingDescription, 400, new Vector2(350, 60)))
                            {
                                if (rel != null) rel.Description = editingDescription;
                            }
                            ImGui.Text("ICON:");
                            if (ImGui.Button("Choose Icon"))
                            {
                                IconSelection = true;
                            }
                            if (IconSelection)
                            {
                                if (!WindowOperations.iconsLoaded)
                                    WindowOperations.LoadIconsLazy(Plugin.plugin);

                                IDalamudTextureWrap relIcon = rel.IconTexture;
                                WindowOperations.RenderIcons(Plugin.plugin, false, true, null, rel, ref relIcon);
                                rel.IconTexture = relIcon;
                            }
                            // Defensive: Only draw if texture is valid
                            if (rel.IconTexture != null && rel.IconTexture.Handle != IntPtr.Zero)
                            {
                                ImGui.SameLine();
                                ImGui.Image(rel.IconTexture.Handle, new Vector2(32, 32));
                            }

                            if (ImGui.Button("Accept"))
                            {
                                // Always update the rel object with the latest editing buffer and slot
                                rel.Name = editingName;
                                rel.Description = editingDescription;
                                rel.Slot = SlotEdit.Value;
                                Plugin.PluginLog.Debug($"[Tree] Accept: Setting rel.Slot to {SlotEdit.Value.x}, {SlotEdit.Value.y}");

                                // Remove any existing relationship at this slot (avoid duplicates)
                                layout.relationships.RemoveAll(r => r.Slot.HasValue && r.Slot.Value == SlotEdit.Value);

                                // Add the updated/new relationship
                                layout.relationships.Add(rel);

                                WindowOperations.SetIcon = false;
                                WindowOperations.selectedIcon = null;
                                showRelationshipInputWindow = false;

                                // Clear editing state
                                editingSlot = null;
                                editingName = string.Empty;
                                editingDescription = string.Empty;
                                rel.active = learnedToggle;
                            }
                            ImGui.SameLine();
                            if (ImGui.Button("Cancel"))
                            {
                                showRelationshipInputWindow = false;
                            }

                            ImGui.Separator();
                            ImGui.Text("Preview:");
                            if (rel.IconTexture != null && rel.IconTexture.Handle != IntPtr.Zero)
                            {
                                ImGui.SameLine();
                                if (learnedToggle)
                                {
                                    // Normal color
                                    ImGui.Image(rel.IconTexture.Handle, new Vector2(32, 32));
                                }
                                else
                                {
                                    // Desaturate: use grayscale tint
                                    var pos = ImGui.GetCursorScreenPos();
                                    var size = new Vector2(32, 32);
                                    // Grayscale tint (luminance weights)
                                    Vector4 gray = new Vector4(0.299f, 0.587f, 0.114f, 1f);
                                    drawList.AddImage(rel.IconTexture.Handle, pos, pos + size, Vector2.Zero, Vector2.One, ImGui.ColorConvertFloat4ToU32(gray));
                                    ImGui.Dummy(size); // Reserve space
                                }
                            }
                            Misc.RenderHtmlElements(editingName, false, false, true, true, ImGui.CalcTextSize(editingName));
                            Misc.RenderHtmlElements(editingDescription, false, false, true, true, ImGui.CalcTextSize(editingDescription));

                            ImGui.End();
                        }
                    }
                }
            }

            // --- Draw circles and handle selection ---
            for (int y = 0; y < gridSizeY; y++)
            {
                for (int x = 0; x < gridSizeX; x++)
                {
                    Vector2 nodeCenter = startPos + new Vector2(x * spacingX + ringRadius, y * spacingY + ringRadius);

                    float previewRadius = radius * 0.85f; // Match icon size
                    uint previewColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.5f, 0.5f, 0.5f, 0.2f)); // Grey, more transparent

                    string btnId = $"Circle_{x}_{y}_{id}";

                    bool enabled = enabledSlots.Contains((x, y));
                    bool selected = layout.Paths.Any(path => path.Contains((x, y)));

                    // Find relationship at this slot
                    var relAtSlot = layout.relationships.FirstOrDefault(r => r.Slot.HasValue && r.Slot.Value == (x, y));
                    bool isAssigned = relAtSlot != null;

                    // If self is false and not assigned, make invisible
                    if (!self && !isAssigned)
                    {
                        // Draw a fully transparent node background and ring
                        drawList.AddCircleFilled(nodeCenter, previewRadius, ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0)), 32);
                        drawList.AddCircle(nodeCenter, ringRadius, ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0)), 64, ringThickness);
                        continue;
                    }

                    // Draw the node background (normal color)
                    drawList.AddCircleFilled(nodeCenter, previewRadius, previewColor, 32);

                    // Set up hitbox for ImGui interaction
                    ImGui.SetCursorScreenPos(nodeCenter - new Vector2(radius, radius));
                    bool clicked = ImGui.InvisibleButton(btnId, new Vector2(radius * 2, radius * 2));

                    uint hoverColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 1f, 0.2f, 0.4f)); // Green, semi-transparent

                    if (ImGui.IsItemHovered())
                    {
                        drawList.AddCircleFilled(nodeCenter, previewRadius, hoverColor, 32);
                        ImGui.BeginTooltip();

                        bool isEditing = showRelationshipInputWindow
                            && editingSlot != null
                            && editingSlot.Value == (x, y);

                        if (isEditing)
                        {
                            Misc.RenderHtmlElements(editingName, false, true, true, true, null, true);
                            ImGui.Separator();
                            Misc.RenderHtmlElements(editingDescription, false, true, true, true, null, true);
                        }
                        else if (relAtSlot != null)
                        {
                            Misc.RenderHtmlElements(relAtSlot.Name, false, true, true, true, null, true);
                            ImGui.Separator();
                            Misc.RenderHtmlElements(relAtSlot.Description, false, true, true, true, null, true);
                        }
                        else
                        {
                            ImGui.Text("Empty Node");
                            ImGui.Separator();
                            ImGui.Text("Click to assign");
                        }

                        ImGui.EndTooltip();
                    }

                    // Draw icon or image if a relationship exists at this slot
                    if (relAtSlot != null)
                    {
                        var tex = relAtSlot.ImageTexture ?? relAtSlot.IconTexture;
                        if (tex != null && tex.Handle != IntPtr.Zero)
                        {
                            Vector2 iconMin = nodeCenter - new Vector2(iconSize / 2, iconSize / 2);
                            Vector2 iconMax = nodeCenter + new Vector2(iconSize / 2, iconSize / 2);
                            if (relAtSlot.active)
                            {
                                drawList.AddImage(tex.Handle, iconMin, iconMax);
                            }
                            else
                            {
                                Vector4 gray = new Vector4(1f, 0.2f, 0.2f, 1f); // or 0.3f for a darker gray
                                drawList.AddImage(tex.Handle, iconMin, iconMax, Vector2.Zero, Vector2.One, ImGui.ColorConvertFloat4ToU32(gray));
                            }
                            // Draw a fixed-size white ring around the icon
                            drawList.AddCircle(nodeCenter, ringRadius, ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f)), 64, ringThickness);
                        }
                    }

                    // Draw outer circle visual (scales)
                    drawList.AddCircle(nodeCenter, radius + 10 * scale, ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 0.3f, 0.7f)), 32, 8f);

                    // Handle create/break/remove popup
                    if (clicked && layout.CurrentAction == RelationshipAction.None)
                    {
                        layout.ActionSourceSlot = (x, y);
                        ImGui.OpenPopup(btnId + "_action");
                    }
                    if (self)
                    {
                        if (ImGui.BeginPopup(btnId + "_action"))
                        {
                            if (ImGui.Button("Create"))
                            {
                                showRelationshipInputWindow = true;
                                layout.CurrentAction = RelationshipAction.None;
                                ImGui.CloseCurrentPopup();
                            }
                            ImGui.SameLine();
                            var currentConnections = layout.PathConnections[layout.CurrentPathIndex];
                            bool hasConnection = currentConnections.Any(conn => conn.from == (x, y) || conn.to == (x, y));
                            var hasRelationship = layout.relationships.Any(r => r.Slot.HasValue && r.Slot.Value == (x, y));
                            if (hasRelationship)
                            {
                                if (ImGui.Button("Connect"))
                                {
                                    layout.CurrentAction = RelationshipAction.Create;
                                    layout.ActionSourceSlot = (x, y);
                                    ImGui.CloseCurrentPopup();
                                }
                                ImGui.SameLine();

                                if (!hasConnection)
                                    ImGui.BeginDisabled();
                                if (ImGui.Button("Break"))
                                {
                                    layout.CurrentAction = RelationshipAction.Break;
                                    ImGui.CloseCurrentPopup();
                                }
                                if (!hasConnection)
                                    ImGui.EndDisabled();

                                ImGui.SameLine();
                                if (ImGui.Button("Remove"))
                                {
                                    var currentPath = layout.Paths[layout.CurrentPathIndex];
                                    var toRemove = (x, y);
                                    layout.PathConnections[layout.CurrentPathIndex].RemoveAll(conn => conn.from == toRemove || conn.to == toRemove);
                                    currentPath.Remove(toRemove);
                                    layout.relationships.RemoveAll(r => r.Slot.HasValue && r.Slot.Value == toRemove);
                                    if (layout.SelectedSlot == toRemove)
                                        layout.SelectedSlot = null;
                                    if (layout.ActionSourceSlot == toRemove)
                                        layout.ActionSourceSlot = null;
                                    layout.CurrentAction = RelationshipAction.None;
                                    ImGui.CloseCurrentPopup();
                                }
                            }
                            else
                            {
                                ImGui.BeginDisabled();
                                ImGui.Button("Connect");
                                ImGui.Button("Break");
                                ImGui.SameLine();
                                ImGui.Button("Remove");
                                ImGui.EndDisabled();
                            }
                            ImGui.EndPopup();
                        }
                    }

                    // --- Create mode: add connection to adjacent circle ---
                    if (layout.CurrentAction == RelationshipAction.Create && layout.ActionSourceSlot.HasValue)
                    {
                        var src = layout.ActionSourceSlot.Value;
                        var currentPath = layout.Paths[layout.CurrentPathIndex];

                        if ((x, y) != src)
                        {
                            Vector2 fromCenter = startPos + new Vector2(src.x * spacingX + ringRadius, src.y * spacingY + ringRadius);
                            if (ImGui.IsItemHovered())
                            {
                                drawList.AddLine(fromCenter, nodeCenter, ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 1f, 0.2f, 1f)), 6f);
                            }
                            if (ImGui.IsItemClicked())
                            {
                                currentPath.Add((x, y));
                                layout.PathConnections[layout.CurrentPathIndex].Add((src, (x, y)));
                                layout.SelectedSlot = (x, y);
                                layout.PreviousSlot = src;
                                layout.CurrentAction = RelationshipAction.None;
                                layout.ActionSourceSlot = null;
                            }
                        }
                    }
                    if (layout.CurrentAction == RelationshipAction.Break && layout.ActionSourceSlot.HasValue)
                    {
                        var src = layout.ActionSourceSlot.Value;
                        var currentPathConnections = layout.PathConnections[layout.CurrentPathIndex];

                        foreach (var conn in currentPathConnections.ToList())
                        {
                            if (conn.from == src || conn.to == src)
                            {
                                var adj = conn.from == src ? conn.to : conn.from;
                                if (adj == (x, y))
                                {
                                    drawList.AddCircle(
                                        nodeCenter,
                                        radius + 12,
                                        ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.3f, 0.3f, 0.7f)),
                                        32,
                                        4f
                                    );

                                    if (ImGui.IsItemHovered())
                                    {
                                        Vector2 fromCenter = startPos + new Vector2(src.x * spacingX + ringRadius, src.y * spacingY + ringRadius);
                                        Vector2 toCenter = startPos + new Vector2(adj.x * spacingX + ringRadius, adj.y * spacingY + ringRadius);
                                        drawList.AddLine(fromCenter, toCenter, ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.2f, 0.2f, 1f)), 6f);
                                    }

                                    if (ImGui.IsItemClicked())
                                    {
                                        currentPathConnections.Remove(conn);

                                        bool hasRelationship = layout.relationships.Any(r => r.Slot.HasValue && r.Slot.Value == adj);
                                        bool isConnectedElsewhere = layout.PathConnections.Any(pathConns =>
                                            pathConns.Any(c => (c.from == adj || c.to == adj)));

                                        if (!hasRelationship && !isConnectedElsewhere)
                                            layout.Paths[layout.CurrentPathIndex].Remove(adj);

                                        layout.SelectedSlot = src;
                                        layout.CurrentAction = RelationshipAction.None;
                                        layout.ActionSourceSlot = null;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // Blends an icon and a mask (both RGBA byte arrays) into a new RGBA byte array

        public static Bitmap ApplyCircularMask(Bitmap icon, Bitmap mask)
        {
            if (icon.Width != mask.Width || icon.Height != mask.Height)
                throw new ArgumentException("Icon and mask must be the same size.");

            var result = new Bitmap(icon.Width, icon.Height, PixelFormat.Format32bppArgb);

            for (int y = 0; y < icon.Height; y++)
            {
                for (int x = 0; x < icon.Width; x++)
                {
                    Color iconPixel = icon.GetPixel(x, y);
                    Color maskPixel = mask.GetPixel(x, y);

                    // Multiply alpha channels
                    int a = iconPixel.A * maskPixel.A / 255;
                    Color outPixel = Color.FromArgb(a, iconPixel.R, iconPixel.G, iconPixel.B);
                    result.SetPixel(x, y, outPixel);
                }
            }
            return result;
        }
    }
}
