using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
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

        // editor state for the node being created / edited
        private static int editingKind = (int)TreeNodeKind.Resource;
        private static int editingBondIdx = -1;
        private static bool requestsOpen = true;

        private static readonly (int dx, int dy)[] Neighbours =
            { (-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, -1), (-1, 1), (1, 1) };

        // Palette for the tree. Links to other profiles read warm, skills cool.
        private static Vector4 BondColor => new(0.95f, 0.55f, 0.70f, 1f);
        private static Vector4 SkillColor => RsTheme.AccentPrimary;
        private static uint U(Vector4 c, float a = 1f) => ImGui.ColorConvertFloat4ToU32(new Vector4(c.X, c.Y, c.Z, c.W * a));

        private static string BondLabel(RelationshipBond b)
        {
            var who = string.IsNullOrWhiteSpace(b.PeerProfileTitle) ? b.PeerName : $"{b.PeerProfileTitle} ({b.PeerName})";
            var state = b.Status == (int)RelationshipStatus.Accepted ? "" : b.Status == (int)RelationshipStatus.Pending ? "  · pending" : "  · declined";
            return $"{who} — {b.Title}{state}";
        }

        // Incoming / outgoing link requests, shown above your own tree.
        private static void DrawLinkRequests(string id)
        {
            RelationshipManager.EnsureFetched();
            var incoming = RelationshipManager.IncomingPending.ToList();
            var outgoing = RelationshipManager.OutgoingPending.ToList();
            var accepted = RelationshipManager.Accepted.Count();
            var title = $"Profile links  ·  {accepted} accepted" + (incoming.Count > 0 ? $"  ·  {incoming.Count} waiting for you" : "");
            if (!RsElements.BeginCollapsible("tree_links_" + id, title, ref requestsOpen)) { RsElements.EndCollapsible(); return; }
            try
            {
                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextWrapped("To link with someone, open their profile and use the link button there. Once they accept, add a relationship node and pick them.");
                ImGui.PopStyleColor();
                if (RsElements.Button("Refresh##tree_links_refresh_" + id, RsElements.ButtonVariant.Ghost)) RelationshipManager.EnsureFetched(true);

                foreach (var b in incoming)
                {
                    ImGui.Spacing();
                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextPrimary);
                    ImGui.TextWrapped($"{b.PeerName} wants to link as \"{b.Title}\"" + (string.IsNullOrWhiteSpace(b.Relation) ? "" : $" — {b.Relation}"));
                    ImGui.PopStyleColor();
                    if (RsElements.Button($"Accept##bond_acc_{b.BondID}", RsElements.ButtonVariant.Primary))
                        AbsoluteRP.Network.Relationships_DS.RespondToRelationshipRequest(Plugin.character, b.BondID, (int)RelationshipStatus.Accepted);
                    ImGui.SameLine();
                    if (RsElements.Button($"Decline##bond_dec_{b.BondID}", RsElements.ButtonVariant.Danger))
                        AbsoluteRP.Network.Relationships_DS.RespondToRelationshipRequest(Plugin.character, b.BondID, (int)RelationshipStatus.Declined);
                }
                foreach (var b in outgoing)
                {
                    ImGui.Spacing();
                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextSecondary);
                    ImGui.TextWrapped($"Waiting on {b.PeerName} — \"{b.Title}\"");
                    ImGui.PopStyleColor();
                    ImGui.SameLine();
                    if (RsElements.Button($"Cancel##bond_cancel_{b.BondID}", RsElements.ButtonVariant.Ghost))
                        AbsoluteRP.Network.Relationships_DS.RemoveRelationship(Plugin.character, b.BondID);
                }
            }
            finally { RsElements.EndCollapsible(); }
            ImGui.Spacing();
        }

        public static void RenderTreeLayout(int index, bool self, string id, TreeLayout layout, string name, Vector4 titleColor)
        {
            ImGui.Spacing();

            if (!self)
            {
                Misc.SetTitle(Plugin.plugin, true, name, titleColor);
                ImGui.Spacing();
                ThemeManager.GradientSeparator();
            }
            else
            {
                DrawLinkRequests(id);
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
            float iconRadius = ringRadius * 0.86f;

            float ringMargin = ringRadius * 2;
            float availableWidth = containerSize.X - ringMargin;
            float availableHeight = containerSize.Y - ringMargin;
            float spacingX = MathF.Max(100f, availableWidth / (gridSizeX - 1));
            float spacingY = MathF.Max(100f, availableHeight / (gridSizeY - 1));
            float gridWidth = (gridSizeX - 1) * spacingX + ringRadius * 2;
            float gridHeight = (gridSizeY - 1) * spacingY + ringRadius * 2;
            float offsetX = MathF.Max(0, (containerSize.X - gridWidth) / 2f);
            // The tree hangs from the top of the panel.
            float offsetY = 0f;

            Vector2 startPos = ImGui.GetCursorScreenPos() + new Vector2(offsetX, offsetY);
            int centerX = gridSizeX / 2;
            int centerY = 0;
            // Layouts saved before the root moved to the top keep their root slot.
            if (layout.Paths != null && layout.Paths.Count > 0 && layout.Paths[0].Count > 0)
            {
                centerX = layout.Paths[0][0].x;
                centerY = layout.Paths[0][0].y;
            }
            var centerSlot = (centerX, centerY);
            float lineThick = MathF.Max(2f, 3f * fontScale);
            Vector2 Center((int x, int y) s) => startPos + new Vector2(s.x * spacingX + ringRadius, s.y * spacingY + ringRadius);

            layout.Paths ??= new List<List<(int x, int y)>>();
            layout.PathConnections ??= new List<List<((int x, int y) from, (int x, int y) to)>>();
            layout.relationships ??= new List<Relationship>();

            if (layout.Paths.Count == 0)
            {
                layout.Paths.Add(new List<(int x, int y)> { centerSlot });
                layout.PathConnections.Add(new List<((int x, int y) from, (int x, int y) to)>());
                layout.SelectedSlot = centerSlot;
                layout.PreviousSlot = null;
                layout.CurrentPathIndex = 0;
            }
            while (layout.PathConnections.Count < layout.Paths.Count)
                layout.PathConnections.Add(new List<((int x, int y) from, (int x, int y) to)>());
            if (layout.CurrentPathIndex < 0 || layout.CurrentPathIndex >= layout.Paths.Count) layout.CurrentPathIndex = 0;

            // Enabled slots: the centre, its ring, every slot in a path, and the neighbours of the selected slot.
            var enabledSlots = new HashSet<(int x, int y)> { centerSlot };
            foreach (var (dx, dy) in Neighbours) enabledSlots.Add((centerX + dx, centerY + dy));
            foreach (var path in layout.Paths) foreach (var slot in path) enabledSlots.Add(slot);
            if (layout.SelectedSlot.HasValue)
                foreach (var (dx, dy) in Neighbours) enabledSlots.Add((layout.SelectedSlot.Value.x + dx, layout.SelectedSlot.Value.y + dy));

            Relationship RelAt((int x, int y) s) => layout.relationships.FirstOrDefault(r => r.Slot.HasValue && r.Slot.Value == s);

            // connections: a soft glow under a crisp line
            foreach (var pathConnections in layout.PathConnections)
            {
                foreach (var conn in pathConnections)
                {
                    var a = Center(conn.from); var b = Center(conn.to);
                    var relFrom = RelAt(conn.from); var relTo = RelAt(conn.to);
                    bool lit = (relFrom?.active ?? false) && (relTo?.active ?? false);
                    bool bondLine = (relFrom?.IsBond ?? false) || (relTo?.IsBond ?? false);
                    var col = lit ? (bondLine ? BondColor : SkillColor) : RsTheme.BorderStrong;
                    if (lit) drawList.AddLine(a, b, U(col, 0.22f), lineThick * 3.2f);
                    drawList.AddLine(a, b, U(col, lit ? 0.95f : 0.7f), lineThick);
                }
            }

            // node editor window
            if (self && showRelationshipInputWindow)
                DrawNodeEditor(layout, drawList);

            // nodes
            for (int y = 0; y < gridSizeY; y++)
            {
                for (int x = 0; x < gridSizeX; x++)
                {
                    Vector2 nodeCenter = Center((x, y));
                    string btnId = $"Circle_{x}_{y}_{id}";
                    bool enabled = enabledSlots.Contains((x, y));
                    var relAtSlot = RelAt((x, y));
                    bool isAssigned = relAtSlot != null;

                    // Viewers only see real nodes; owners see free slots they can use.
                    if (!isAssigned && (!self || !enabled)) continue;

                    ImGui.SetCursorScreenPos(nodeCenter - new Vector2(ringRadius, ringRadius));
                    bool clicked = ImGui.InvisibleButton(btnId, new Vector2(ringRadius * 2, ringRadius * 2));
                    bool hovered = ImGui.IsItemHovered();
                    float grow = hovered ? 1.08f : 1f;

                    if (!isAssigned)
                    {
                        // A free slot: a quiet dot, a plus when hovered.
                        drawList.AddCircleFilled(nodeCenter, radius * 0.55f * grow, U(RsTheme.BgTertiary, 0.85f), 32);
                        drawList.AddCircle(nodeCenter, radius * 0.55f * grow, U(RsTheme.Border, hovered ? 1f : 0.6f), 32, 1.5f);
                        if (hovered)
                        {
                            var arm = radius * 0.22f;
                            drawList.AddLine(nodeCenter - new Vector2(arm, 0), nodeCenter + new Vector2(arm, 0), U(RsTheme.TextSecondary), 2f);
                            drawList.AddLine(nodeCenter - new Vector2(0, arm), nodeCenter + new Vector2(0, arm), U(RsTheme.TextSecondary), 2f);
                            ImGui.SetTooltip("Empty slot — click to add a node");
                        }
                    }
                    else
                    {
                        // Link nodes wear the linked profile's own title colour when it has one.
                        var peerColour = relAtSlot.IsBond && relAtSlot.BondPeerAccountID > 0 ? ProfileAvatars.ColourOf(relAtSlot.BondPeerAccountID, relAtSlot.BondPeerProfileIndex) : null;
                        var accent = peerColour.HasValue ? new Vector4(peerColour.Value.X, peerColour.Value.Y, peerColour.Value.Z, 1f) : relAtSlot.IsBond ? BondColor : SkillColor;
                        bool on = relAtSlot.active;
                        float r = ringRadius * grow;
                        float ir = iconRadius * grow;

                        // Glow, plate, circular-masked icon, thin ring.
                        if (on) drawList.AddCircleFilled(nodeCenter, r * 1.22f, U(accent, hovered ? 0.22f : 0.13f), 48);
                        drawList.AddCircleFilled(nodeCenter, r, U(RsTheme.BgTertiary), 48);
                        var tex = NodeTexture(relAtSlot);
                        if (tex != null && tex.Handle != IntPtr.Zero)
                        {
                            var tint = on ? new Vector4(1f, 1f, 1f, 1f) : new Vector4(0.45f, 0.45f, 0.5f, 0.85f);
                            // Rounding = half the size turns the square icon into a disc.
                            drawList.AddImageRounded(tex.Handle, nodeCenter - new Vector2(ir), nodeCenter + new Vector2(ir),
                                Vector2.Zero, Vector2.One, ImGui.ColorConvertFloat4ToU32(tint), ir);
                        }
                        else
                        {
                            var glyph = (relAtSlot.IsBond ? Dalamud.Interface.FontAwesomeIcon.User : Dalamud.Interface.FontAwesomeIcon.Star).ToIconString();
                            using (RsIcons.Push())
                            {
                                var gs = ImGui.CalcTextSize(glyph);
                                drawList.AddText(nodeCenter - gs * 0.5f, U(on ? accent : RsTheme.TextMuted), glyph);
                            }
                        }
                        drawList.AddCircle(nodeCenter, r, U(on ? accent : RsTheme.BorderStrong, on ? 1f : 0.8f), 48, MathF.Max(1.5f, 2.2f * fontScale));

                        // Caption under the node: the name, and the position for links.
                        var caption = StripTags(relAtSlot.Name ?? string.Empty);
                        if (!string.IsNullOrWhiteSpace(caption))
                        {
                            float maxW = spacingX * 0.95f;
                            caption = FitText(caption, maxW);
                            var cs = ImGui.CalcTextSize(caption);
                            var cp = new Vector2(nodeCenter.X - cs.X * 0.5f, nodeCenter.Y + ringRadius + 4f * fontScale);
                            drawList.AddText(cp + new Vector2(0, 1), U(new Vector4(0, 0, 0, 1), 0.7f), caption);
                            drawList.AddText(cp, U(on ? (peerColour.HasValue ? accent : RsTheme.TextPrimary) : RsTheme.TextMuted), caption);
                            if (relAtSlot.IsBond && !string.IsNullOrWhiteSpace(relAtSlot.BondTitle))
                            {
                                var pos = FitText(relAtSlot.BondTitle, maxW);
                                var ps = ImGui.CalcTextSize(pos);
                                drawList.AddText(new Vector2(nodeCenter.X - ps.X * 0.5f, cp.Y + cs.Y), U(accent, 0.9f), pos);
                            }
                        }

                        if (hovered)
                        {
                            ImGui.BeginTooltip();
                            bool isEditing = showRelationshipInputWindow && editingSlot != null && editingSlot.Value == (x, y);
                            Misc.RenderHtmlElements(isEditing ? editingName : relAtSlot.Name, false, true, true, true, null, true);
                            if (relAtSlot.IsBond)
                            {
                                ImGui.PushStyleColor(ImGuiCol.Text, accent);
                                ImGui.TextUnformatted(string.IsNullOrWhiteSpace(relAtSlot.BondTitle) ? "Linked profile" : relAtSlot.BondTitle);
                                ImGui.PopStyleColor();
                                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                                ImGui.TextUnformatted($"{relAtSlot.BondPeerName} · {relAtSlot.BondPeerWorld}");
                                if (!self) ImGui.TextUnformatted("Click to open their profile");
                                ImGui.PopStyleColor();
                            }
                            var desc = isEditing ? editingDescription : relAtSlot.Description;
                            if (!string.IsNullOrWhiteSpace(desc))
                            {
                                ThemeManager.GradientSeparator();
                                Misc.RenderHtmlElements(desc, false, true, true, true, null, true);
                            }
                            ImGui.EndTooltip();
                        }

                        // Viewers can follow a link to the other profile.
                        if (clicked && !self && relAtSlot.IsBond && !string.IsNullOrWhiteSpace(relAtSlot.BondPeerName))
                        {
                            TargetProfileWindow.characterName = relAtSlot.BondPeerName;
                            TargetProfileWindow.characterWorld = relAtSlot.BondPeerWorld;
                            TargetProfileWindow.RequestingProfile = true;
                            TargetProfileWindow.ResetAllData();
                            Plugin.plugin.OpenTargetWindow();
                            AbsoluteRP.Network.Profiles_DS.FetchProfile(Plugin.character, false, relAtSlot.BondPeerProfileIndex, relAtSlot.BondPeerName, relAtSlot.BondPeerWorld, -1);
                        }
                    }

                    if (!self) continue;

                    // owner actions
                    if (clicked && layout.CurrentAction == RelationshipAction.None)
                    {
                        layout.ActionSourceSlot = (x, y);
                        ImGui.OpenPopup(btnId + "_action");
                    }
                    if (ImGui.BeginPopup(btnId + "_action"))
                    {
                        if (!isAssigned)
                        {
                            // Choose what the node is before the editor opens.
                            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                            ImGui.TextUnformatted("What is this node?");
                            ImGui.PopStyleColor();
                            if (RsElements.Button("Relationship##kind_bond", RsElements.ButtonVariant.Primary))
                            {
                                OpenEditor(layout, (x, y), (int)TreeNodeKind.RelationshipBond);
                                ImGui.CloseCurrentPopup();
                            }
                            if (ImGui.IsItemHovered()) ImGui.SetTooltip("A link to another profile (family tree, FC roster…).\nPick from the people you have a link with.");
                            ImGui.SameLine();
                            if (RsElements.Button("Skill / ability##kind_skill", RsElements.ButtonVariant.Ghost))
                            {
                                OpenEditor(layout, (x, y), (int)TreeNodeKind.Resource);
                                ImGui.CloseCurrentPopup();
                            }
                            if (ImGui.IsItemHovered()) ImGui.SetTooltip("A node you describe yourself: a skill, ability, title, anything.");
                        }
                        else
                        {
                            if (RsElements.Button("Edit", RsElements.ButtonVariant.Primary))
                            {
                                OpenEditor(layout, (x, y), relAtSlot.NodeKind);
                                ImGui.CloseCurrentPopup();
                            }
                            ImGui.SameLine();
                            var currentConnections = layout.PathConnections[layout.CurrentPathIndex];
                            bool hasConnection = currentConnections.Any(conn => conn.from == (x, y) || conn.to == (x, y));
                            if (RsElements.Button("Connect", RsElements.ButtonVariant.Ghost))
                            {
                                layout.CurrentAction = RelationshipAction.Create;
                                layout.ActionSourceSlot = (x, y);
                                ImGui.CloseCurrentPopup();
                            }
                            ImGui.SameLine();
                            if (!hasConnection) ImGui.BeginDisabled();
                            if (RsElements.Button("Break", RsElements.ButtonVariant.Danger))
                            {
                                layout.CurrentAction = RelationshipAction.Break;
                                ImGui.CloseCurrentPopup();
                            }
                            if (!hasConnection) ImGui.EndDisabled();
                            ImGui.SameLine();
                            if (RsElements.Button("Remove", RsElements.ButtonVariant.Danger))
                            {
                                var toRemove = (x, y);
                                foreach (var pc in layout.PathConnections) pc.RemoveAll(conn => conn.from == toRemove || conn.to == toRemove);
                                layout.Paths[layout.CurrentPathIndex].Remove(toRemove);
                                layout.relationships.RemoveAll(r => r.Slot.HasValue && r.Slot.Value == toRemove);
                                if (layout.SelectedSlot == toRemove) layout.SelectedSlot = null;
                                if (layout.ActionSourceSlot == toRemove) layout.ActionSourceSlot = null;
                                layout.CurrentAction = RelationshipAction.None;
                                ImGui.CloseCurrentPopup();
                            }
                        }
                        ImGui.EndPopup();
                    }

                    // Connect mode: pick the node to draw a line to.
                    if (layout.CurrentAction == RelationshipAction.Create && layout.ActionSourceSlot.HasValue)
                    {
                        var src = layout.ActionSourceSlot.Value;
                        if ((x, y) != src)
                        {
                            if (hovered) drawList.AddLine(Center(src), nodeCenter, U(RsTheme.AccentSuccess), lineThick);
                            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                            {
                                layout.Paths[layout.CurrentPathIndex].Add((x, y));
                                layout.PathConnections[layout.CurrentPathIndex].Add((src, (x, y)));
                                layout.SelectedSlot = (x, y);
                                layout.PreviousSlot = src;
                                layout.CurrentAction = RelationshipAction.None;
                                layout.ActionSourceSlot = null;
                            }
                        }
                    }
                    // Break mode: connected neighbours light up red.
                    if (layout.CurrentAction == RelationshipAction.Break && layout.ActionSourceSlot.HasValue)
                    {
                        var src = layout.ActionSourceSlot.Value;
                        var conns = layout.PathConnections[layout.CurrentPathIndex];
                        foreach (var conn in conns.ToList())
                        {
                            if (conn.from != src && conn.to != src) continue;
                            var adj = conn.from == src ? conn.to : conn.from;
                            if (adj != (x, y)) continue;
                            drawList.AddCircle(nodeCenter, ringRadius + 5f * fontScale, U(RsTheme.AccentDanger, 0.8f), 48, 2.5f);
                            if (hovered) drawList.AddLine(Center(src), Center(adj), U(RsTheme.AccentDanger), lineThick * 1.4f);
                            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                            {
                                conns.Remove(conn);
                                bool hasRelationship = RelAt(adj) != null;
                                bool connectedElsewhere = layout.PathConnections.Any(pc => pc.Any(c => c.from == adj || c.to == adj));
                                if (!hasRelationship && !connectedElsewhere) layout.Paths[layout.CurrentPathIndex].Remove(adj);
                                layout.SelectedSlot = src;
                                layout.CurrentAction = RelationshipAction.None;
                                layout.ActionSourceSlot = null;
                            }
                        }
                    }
                }
            }

            // Right-click leaves connect / break mode.
            if (self && layout.CurrentAction != RelationshipAction.None && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                layout.CurrentAction = RelationshipAction.None;
                layout.ActionSourceSlot = null;
            }

            // Reserve the grid's area so the page scrolls past it.
            ImGui.SetCursorScreenPos(startPos);
            ImGui.Dummy(new Vector2(gridWidth, gridHeight + ImGui.GetTextLineHeight() * 2.5f));
        }

        // A link node's picture: the linked account's avatar, else the chosen icon.
        private static IDalamudTextureWrap NodeTexture(Relationship rel)
        {
            if (rel.IsBond && rel.BondPeerAccountID > 0)
            {
                // The linked profile's own avatar first; the account's picture as a fallback.
                var pa = ProfileAvatars.Get(rel.BondPeerAccountID, rel.BondPeerProfileIndex);
                if (pa != null && pa.Handle != IntPtr.Zero) return pa;
                var ap = AbsoluteRP.Social.SocialAvatar.EnsureProfile(rel.BondPeerAccountID);
                if (!string.IsNullOrWhiteSpace(ap?.avatarUrl))
                {
                    var av = AbsoluteRP.Social.SocialMediaCache.Get(ap!.avatarUrl);
                    if (av != null && av.Width > 0) return av;
                }
            }
            return rel.ImageTexture ?? rel.IconTexture;
        }

        private static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? string.Empty, "<[^>]+>", string.Empty).Trim();

        private static string FitText(string text, float maxW)
        {
            if (ImGui.CalcTextSize(text).X <= maxW) return text;
            while (text.Length > 1 && ImGui.CalcTextSize(text + "…").X > maxW) text = text[..^1];
            return text + "…";
        }

        private static void OpenEditor(TreeLayout layout, (int x, int y) slot, int kind)
        {
            layout.ActionSourceSlot = slot;
            layout.CurrentAction = RelationshipAction.None;
            editingKind = kind;
            editingSlot = null;          // forces the buffers to reload for this slot
            editingBondIdx = -1;
            IconSelection = false;
            showRelationshipInputWindow = true;
            if (kind == (int)TreeNodeKind.RelationshipBond) RelationshipManager.EnsureFetched(true);
        }

        // The window a node is created / edited in. Its fields depend on the kind chosen beforehand.
        private static Relationship editingRel;
        private static void DrawNodeEditor(TreeLayout layout, ImDrawListPtr drawList)
        {
            var slotEdit = layout.ActionSourceSlot ?? layout.SelectedSlot;
            if (slotEdit == null) { showRelationshipInputWindow = false; return; }

            if (editingSlot != slotEdit)
            {
                var existing = layout.relationships.FirstOrDefault(r => r.Slot.HasValue && r.Slot.Value == slotEdit.Value);
                editingRel = existing ?? new Relationship { Slot = slotEdit.Value, IconID = 0, NodeKind = editingKind, active = true };
                editingName = editingRel.Name ?? string.Empty;
                editingDescription = editingRel.Description ?? string.Empty;
                learnedToggle = existing?.active ?? true;
                editingSlot = slotEdit;
                editingKind = existing?.NodeKind ?? editingKind;
            }
            var rel = editingRel;
            bool bond = editingKind == (int)TreeNodeKind.RelationshipBond;

            ImGui.SetNextWindowSize(new Vector2(RsTheme.S(420f), 0f), ImGuiCond.Always);
            var open = ImGui.Begin(bond ? "Relationship node###tree_node_editor" : "Skill / ability node###tree_node_editor", ref showRelationshipInputWindow, ImGuiWindowFlags.NoCollapse);
            try
            {
                if (!open) return;

                if (bond)
                {
                    var bonds = RelationshipManager.All.Where(b => b.Status == (int)RelationshipStatus.Accepted || (b.Status == (int)RelationshipStatus.Pending && b.RequestedByUs)).ToList();
                    ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                    ImGui.TextUnformatted("LINKED PROFILE:");
                    ImGui.PopStyleColor();
                    if (bonds.Count == 0)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentWarning);
                        ImGui.TextWrapped("You have no profile links yet. Open the other person's profile and use the link button to ask them; they appear here once requested.");
                        ImGui.PopStyleColor();
                    }
                    else
                    {
                        if (editingBondIdx < 0) editingBondIdx = Math.Max(0, bonds.FindIndex(b => b.BondID == rel.BondID));
                        var labels = bonds.Select(BondLabel).ToList();
                        editingBondIdx = Math.Clamp(editingBondIdx, 0, bonds.Count - 1);
                        var before = editingBondIdx;
                        RsElements.Dropdown("tree_bond_pick", ref editingBondIdx, labels, 380f);
                        var pick = bonds[editingBondIdx];
                        if (before != editingBondIdx || rel.BondID != pick.BondID)
                        {
                            rel.BondID = pick.BondID;
                            rel.BondPeerAccountID = pick.PeerAccountID;
                            rel.BondPeerProfileIndex = pick.PeerProfileIndex;
                            rel.BondPeerName = pick.PeerName;
                            rel.BondPeerWorld = pick.PeerWorld;
                            rel.BondTitle = pick.Title;
                            rel.BondRelation = pick.Relation;
                            if (string.IsNullOrWhiteSpace(editingName) || before != editingBondIdx)
                                editingName = string.IsNullOrWhiteSpace(pick.PeerProfileTitle) ? pick.PeerName : pick.PeerProfileTitle;
                        }
                        if (pick.Status != (int)RelationshipStatus.Accepted)
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.AccentWarning);
                            ImGui.TextWrapped("They haven't accepted yet. You can place the node now; it shows as a link once they accept.");
                            ImGui.PopStyleColor();
                        }
                    }
                }

                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextUnformatted(bond ? "NAME SHOWN:" : "NAME:");
                ImGui.PopStyleColor();
                var activeLabelW = ImGui.CalcTextSize("Active").X + ImGui.GetFrameHeight() + RsTheme.S(12f);
                var nameAvail = ImGui.GetContentRegionAvail().X - activeLabelW - RsTheme.S(8f);
                RsElements.InputText("##RelationshipName", ref editingName, 200, width: MathF.Max(RsTheme.S(120f), nameAvail) / RsTheme.Scale);
                ImGui.SameLine();
                RsElements.Checkbox("Active", ref learnedToggle);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Inactive nodes are dimmed and their lines stay grey.");

                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextUnformatted(bond ? "NOTES:" : "DESCRIPTION:");
                ImGui.PopStyleColor();
                RsElements.InputTextArea("##RelationshipDescription", ref editingDescription, 400, size: new Vector2(ImGui.GetContentRegionAvail().X, RsTheme.S(60f)));

                ImGui.PushStyleColor(ImGuiCol.Text, RsTheme.TextMuted);
                ImGui.TextUnformatted("ICON:");
                ImGui.PopStyleColor();
                if (RsElements.Button("Choose Icon", RsElements.ButtonVariant.Ghost)) IconSelection = true;
                if (IconSelection)
                {
                    if (!WindowOperations.iconsLoaded) WindowOperations.LoadIconsLazy(Plugin.plugin);
                    IDalamudTextureWrap relIcon = rel.IconTexture;
                    WindowOperations.RenderIcons(Plugin.plugin, false, true, null, rel, ref relIcon);
                    rel.IconTexture = relIcon;
                }
                var preview = NodeTexture(rel);
                if (preview != null && preview.Handle != IntPtr.Zero)
                {
                    ImGui.SameLine();
                    var p = ImGui.GetCursorScreenPos();
                    var s = RsTheme.S(32f);
                    ImGui.GetWindowDrawList().AddImageRounded(preview.Handle, p, p + new Vector2(s), Vector2.Zero, Vector2.One, 0xFFFFFFFF, s * 0.5f);
                    ImGui.Dummy(new Vector2(s));
                    if (bond && ImGui.IsItemHovered()) ImGui.SetTooltip("Link nodes show the linked account's avatar.");
                }

                ImGui.Spacing();
                bool canAccept = !bond || rel.BondID > 0;
                if (!canAccept) ImGui.BeginDisabled();
                if (RsElements.Button("Accept", RsElements.ButtonVariant.Primary))
                {
                    rel.Name = editingName;
                    rel.Description = editingDescription;
                    rel.Slot = slotEdit.Value;
                    rel.NodeKind = editingKind;
                    rel.active = learnedToggle;
                    if (!bond) { rel.BondID = -1; rel.BondPeerName = string.Empty; rel.BondPeerWorld = string.Empty; rel.BondTitle = string.Empty; rel.BondRelation = string.Empty; }
                    layout.relationships.RemoveAll(r => r.Slot.HasValue && r.Slot.Value == slotEdit.Value);
                    layout.relationships.Add(rel);
                    if (!layout.Paths[layout.CurrentPathIndex].Contains(slotEdit.Value)) layout.Paths[layout.CurrentPathIndex].Add(slotEdit.Value);
                    layout.SelectedSlot = slotEdit.Value;
                    WindowOperations.SetIcon = false;
                    WindowOperations.selectedIcon = null;
                    showRelationshipInputWindow = false;
                    editingSlot = null;
                    editingName = string.Empty;
                    editingDescription = string.Empty;
                }
                if (!canAccept) ImGui.EndDisabled();
                ImGui.SameLine();
                if (RsElements.Button("Cancel", RsElements.ButtonVariant.Ghost))
                {
                    showRelationshipInputWindow = false;
                    editingSlot = null;
                }
            }
            finally { ImGui.End(); }
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
