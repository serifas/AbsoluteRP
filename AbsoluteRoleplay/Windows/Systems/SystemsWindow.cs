using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Ect;
using AbsoluteRP.Windows.NavLayouts;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using AbsoluteRP.Windows.Social.Views;
using AbsoluteRP.Windows.Systems;
using AbsoluteRP.Windows.Systems.Stats;
using AbsoluteRP.Windows.Systems.Combat;
using AbsoluteRP.Windows.Systems.Rules;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Common.Lua;
using Networking;
using Serilog.Filters;
using System.Numerics;
using System.Reflection;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode.Delegates;

namespace AbsoluteRP.Windows.Listings
{
    // Main RP Systems editor window — create and manage tabletop-style RP systems with
    // stats, resources, combat config, skill classes, skill trees, and rules.
    // Systems can be shared via share codes so other players can join and create character sheets.
    internal class SystemsWindow : Window, IDisposable
    {
        public static Configuration configuration;

        public static int currentSystemIndex = -1;
        public static int systemNavIndex = 0;
        public static bool drawStatLayout = false;
        public static SystemData? currentSystem = null;
        public static List<SystemData> systemData = new List<SystemData>();
        public static bool uiSelected = false;
        private static bool fetchedSystems = false;

        // Top-level mode: 0=View Systems, 1=Manage Systems
        public static int systemMode = 1;

        // Delete confirmation
        private static bool showDeleteConfirm = false;

        // File dialog for banner/logo uploads
        private static Dalamud.Interface.ImGuiFileDialog.FileDialogManager systemFileDialog = new Dalamud.Interface.ImGuiFileDialog.FileDialogManager();

        // Right-side roster panel
        public static bool showRosterPanel = false;

        // Collapsible settings
        private static bool settingsExpanded = false;

        // Section tabs: 0=Stats, 1=Classes, 2=Combat, 3=Rules, 4=Roster
        public static int systemSectionIndex = 0;
        private static readonly string[] SectionNames = { "Stats", "Classes", "Combat", "Rules", "Roster" };
        public SystemsWindow() : base(
            "SYSTEMS")
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(200, 400),
                MaximumSize = new Vector2(1000, 1000)
            };

            configuration = Plugin.plugin.Configuration;
        }

        public override void Draw()
        {

            if (ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows)
                && ImGui.IsMouseClicked(ImGuiMouseButton.Left)
                && !ImGui.IsAnyItemActive()
                && !ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId)
                && !uiSelected)
            {
                ImGui.SetWindowFocus("SystemNavigation");
                ImGui.SetWindowFocus("SYSTEMS");
                if (showRosterPanel)
                    ImGui.SetWindowFocus("System Roster##RosterPanel");
            }
            var hovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows);
            var clicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left);
            var anyActive = ImGui.IsAnyItemActive();
            var alreadyFocused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
            var focusRequested = hovered && clicked && !anyActive && !alreadyFocused;
            // Main panel position/size
            Vector2 mainPanelPos = ImGui.GetWindowPos();
            Vector2 mainPanelSize = ImGui.GetWindowSize();

            // Navigation panel
            float headerHeight = 48f;
            float buttonSize = ImGui.GetIO().FontGlobalScale * 45;
            int buttonCount = 5;
            float navHeight = buttonSize * buttonCount * 1.2f;

            // Fetch systems from server on first draw (regardless of which tab is active)
            if (!fetchedSystems && Plugin.character != null)
            {
                fetchedSystems = true;
                Networking.DataSender.FetchMySystems(Plugin.character);
            }

            // Top-level mode tabs
            if (ImGui.BeginTabBar("##SystemModeBar"))
            {
                if (ImGui.BeginTabItem("View Systems"))
                {
                    systemMode = 0;
                    ImGui.Spacing();
                    AbsoluteRP.Windows.Systems.ViewSystems.ViewSystems.DrawViewSystems();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Manage Systems"))
                {
                    systemMode = 1;
                    ImGui.Spacing();
                    DrawSystemCreation();
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }

            // Draw file dialogs (must be outside tab items)
            SystemExportImport.DrawFileDialogs();
            systemFileDialog.Draw();

            ImGui.SetNextWindowPos(new Vector2(mainPanelPos.X - buttonSize * 1.5f, mainPanelPos.Y + headerHeight), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(buttonSize * 1.5f, navHeight), ImGuiCond.Always);

            ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar;

            // Right-side roster panel
            if (showRosterPanel && AbsoluteRP.Windows.Systems.ViewSystems.ViewSystems.selectedSystem != null)
            {
                float rosterWidth = mainPanelSize.X * 0.6f;
                float rosterMinWidth = 300f;
                if (rosterWidth < rosterMinWidth) rosterWidth = rosterMinWidth;

                ImGui.SetNextWindowPos(new Vector2(mainPanelPos.X + mainPanelSize.X + 4, mainPanelPos.Y), ImGuiCond.Always);
                ImGui.SetNextWindowSize(new Vector2(rosterWidth, mainPanelSize.Y), ImGuiCond.Always);

                ImGuiWindowFlags rosterFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;
                bool rosterOpen = showRosterPanel;
                if (ImGui.Begin("System Roster##RosterPanel", ref rosterOpen, rosterFlags))
                {
                    // When roster panel is clicked, bring main window and nav to front too
                    if (ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows)
                        && ImGui.IsMouseClicked(ImGuiMouseButton.Left)
                        && !ImGui.IsAnyItemActive())
                    {
                        ImGui.SetWindowFocus("SystemNavigation");
                        ImGui.SetWindowFocus("SYSTEMS");
                        ImGui.SetWindowFocus("System Roster##RosterPanel");
                    }

                    var sys = AbsoluteRP.Windows.Systems.ViewSystems.ViewSystems.selectedSystem;
                    AbsoluteRP.Windows.Systems.Roster.Roster.DrawPublicRoster(sys);
                }
                ImGui.End();
                showRosterPanel = rosterOpen;
            }
        }

        public static void DrawSystemCreation()
        {
            // Create new system
            if (ThemeManager.PillButton("Create System"))
            {
                var newSystem = new SystemData() { name = "New System", description = string.Empty, StatsData = new SortedList<int, StatData>() };
                systemData.Add(newSystem);
                currentSystemIndex = systemData.Count - 1;
                currentSystem = newSystem;
                drawStatLayout = true;
                systemSectionIndex = 0;
                Stats.currentStatIndex = -1;
                Stats.selectedStat = null;

                // Send to server to get an ID
                if (Plugin.character != null)
                    Networking.DataSender.CreateSystem(Plugin.character, newSystem.name, newSystem.description);
            }

            DrawSystemSelection();

            if (currentSystem == null) return;

            ImGui.Spacing();

            // Horizontal section tab bar (always visible when a system is selected)
            if (ImGui.BeginTabBar("##SystemSections"))
            {
                for (int i = 0; i < SectionNames.Length; i++)
                {
                    bool selected = systemSectionIndex == i;
                    if (ImGui.BeginTabItem(SectionNames[i]))
                    {
                        systemSectionIndex = i;
                        drawStatLayout = (i == 0);
                        ImGui.EndTabItem();
                    }
                }
                ImGui.EndTabBar();
            }

            ImGui.Spacing();
            ThemeManager.GradientSeparator();
            ImGui.Spacing();

            // Draw the selected section
            switch (systemSectionIndex)
            {
                case 0:
                    Stats.DrawStatCreation();
                    break;
                case 1:
                    AbsoluteRP.Windows.Systems.Skills.Skills.DrawSkillsEditor();
                    break;
                case 2:
                    AbsoluteRP.Windows.Systems.Combat.Combat.DrawCombatConfig();
                    break;
                case 3:
                    AbsoluteRP.Windows.Systems.Rules.Rules.DrawRulesEditor();
                    break;
                case 4:
                    AbsoluteRP.Windows.Systems.Roster.Roster.DrawRoster();
                    break;
            }
        }

        /// <summary>
        /// Saves all system data (stats, combat, classes, skills, rules) to the server.
        /// </summary>
        public static void SaveAllSystemData()
        {
            var system = currentSystem;
            if (system == null || system.id <= 0 || Plugin.character == null) return;

            var character = Plugin.character;
            _ = SaveAllSystemDataAsync(character, system);
        }

        private static async System.Threading.Tasks.Task SaveAllSystemDataAsync(AbsoluteRP.Defines.Character character, SystemData system)
        {
            // Save sequentially so stat IDs are settled before combat config references them
            await Networking.DataSender.UpdateSystemSettings(character, system.id, system.name, system.basePointsAvailable, system.requireApproval, system.rules, system.restrictResourceModification);
            await Networking.DataSender.SaveSystemStats(character, system.id, system.StatsData);
            await Networking.DataSender.SaveCombatConfig(character, system.id, system.CombatConfig, system.Resources);
            await Networking.DataSender.SaveSkillClasses(character, system.id, system.SkillClasses);
            await Networking.DataSender.SaveSkills(character, system.id, system.Skills, system.SkillConnections);
        }


        public static void DrawSystemSelection()
        {
            if (systemData.Count == 0)
                return;

            // Defensive: ensure index is valid
            if (currentSystemIndex < 0 || currentSystemIndex >= systemData.Count)
            {
                currentSystemIndex = 0;
                currentSystem = systemData[0];
            }

            string currentLabel = !string.IsNullOrEmpty(systemData[currentSystemIndex].name)
                ? systemData[currentSystemIndex].name
                : "Select a system";

            if (ImGui.BeginCombo("##Systems", currentLabel))
            {
                uiSelected = true;
                for (int idx = 0; idx < systemData.Count; idx++)
                {
                    string label = string.IsNullOrEmpty(systemData[idx].name) ? "New System" : systemData[idx].name;
                    bool isSelected = idx == currentSystemIndex;
                    if (ImGui.Selectable(label + $"##{idx}", isSelected))
                    {
                        currentSystemIndex = idx;
                        currentSystem = systemData[idx];
                        drawStatLayout = true;
                        Stats.currentStatIndex = -1;
                        Stats.selectedStat = null;

                        // Fetch full system data from server when selecting a different system
                        if (Plugin.character != null && systemData[idx].id > 0)
                            Networking.DataSender.FetchSystem(Plugin.character, systemData[idx].id);
                    }
                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            // Delete system button
            if (currentSystem != null && currentSystem.id > 0)
            {
                ImGui.SameLine();
                if (ThemeManager.DangerButton("Delete##delSystem"))
                {
                    showDeleteConfirm = true;
                    ImGui.OpenPopup("##DeleteSystemConfirm");
                }
            }

            // Delete confirmation popup
            if (ImGui.BeginPopupModal("##DeleteSystemConfirm", ref showDeleteConfirm, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text($"Delete system \"{currentSystem?.name}\"?");
                ImGui.Spacing();
                ImGui.TextColored(new System.Numerics.Vector4(1, 0.4f, 0.4f, 1),
                    "This will permanently delete the system and all its data.");
                ImGui.TextColored(new System.Numerics.Vector4(1, 0.4f, 0.4f, 1),
                    "This action cannot be undone.");
                ImGui.Spacing();
                ImGui.Spacing();

                bool ctrlHeld = ImGui.GetIO().KeyCtrl;
                if (!ctrlHeld) ImGui.BeginDisabled();
                if (ThemeManager.DangerButton("Confirm Delete##confirmDel"))
                {
                    if (currentSystem != null && Plugin.character != null)
                    {
                        Networking.DataSender.DeleteSystem(Plugin.character, currentSystem.id);
                        systemData.RemoveAt(currentSystemIndex);

                        // Also remove from View Systems
                        var viewList = AbsoluteRP.Windows.Systems.ViewSystems.ViewSystems.availableSystems;
                        viewList.RemoveAll(s => s.id == currentSystem.id);

                        if (systemData.Count > 0)
                        {
                            currentSystemIndex = 0;
                            currentSystem = systemData[0];
                        }
                        else
                        {
                            currentSystemIndex = -1;
                            currentSystem = null;
                        }
                    }
                    showDeleteConfirm = false;
                    ImGui.CloseCurrentPopup();
                }
                if (!ctrlHeld) ImGui.EndDisabled();

                if (!ctrlHeld)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(ThemeManager.FontMuted, "Hold CTRL to enable");
                }

                ImGui.SameLine();
                if (ThemeManager.GhostButton("Cancel##cancelDel"))
                {
                    showDeleteConfirm = false;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }

            if (systemData.Count == 0 || currentSystemIndex < 0 || currentSystemIndex >= systemData.Count)
                return;

            string systemName = systemData[currentSystemIndex].name;
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 160);
            if (ImGui.InputTextWithHint("##SystemName", "Enter system name...", ref systemName))
            {
                systemData[currentSystemIndex].name = systemName;
                if (currentSystem != null)
                    currentSystem.name = systemName;
            }
            ImGui.SameLine();
            if (currentSystem != null && currentSystem.id > 0)
            {
                if (ThemeManager.PillButton("Save All##saveAll"))
                    SaveAllSystemData();
            }
            else if (currentSystem != null && currentSystem.id <= 0)
            {
                ImGui.TextColored(ThemeManager.FontMuted, "Creating...");
            }

            // Collapsible settings area
            if (currentSystem != null && ImGui.CollapsingHeader("System Settings"))
            {
                // Share code
                if (!string.IsNullOrEmpty(currentSystem.shareCode))
                {
                    ImGui.Text("Share Code:");
                    ImGui.SameLine();
                    ImGui.TextColored(ThemeManager.Accent, currentSystem.shareCode);
                    ImGui.SameLine();
                    if (ThemeManager.GhostButton("Copy##copyCode"))
                        ImGui.SetClipboardText(currentSystem.shareCode);
                }

                // Require approval toggle
                bool reqApproval = currentSystem.requireApproval;
                if (ImGui.Checkbox("Require approval for character sheets", ref reqApproval))
                    currentSystem.requireApproval = reqApproval;

                // Restrict resource modification toggle
                bool restrictRes = currentSystem.restrictResourceModification;
                if (ImGui.Checkbox("Restrict resource modification to owner only", ref restrictRes))
                    currentSystem.restrictResourceModification = restrictRes;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Enable this if you do not want anyone to modify their own HP or resources.\nThe system owner can always modify resources regardless.");

            // Banner / Logo upload
            if (currentSystem.id > 0)
            {
                ImGui.Spacing();
                // Banner preview
                if (currentSystem.bannerTexture != null && currentSystem.bannerTexture.Handle != IntPtr.Zero)
                {
                    float availW = ImGui.GetContentRegionAvail().X;
                    float imgW = currentSystem.bannerTexture.Width;
                    float imgH = currentSystem.bannerTexture.Height;
                    float aspect = imgH / imgW;
                    float displayW = availW;
                    float displayH = displayW * aspect;
                    float maxH = 150f;
                    if (displayH > maxH) { displayH = maxH; displayW = displayH / aspect; }
                    ImGui.Image(currentSystem.bannerTexture.Handle, new Vector2(displayW, displayH));
                }
                if (ThemeManager.GhostButton("Set Banner##setBanner"))
                {
                    systemFileDialog.OpenFileDialog("Select Banner", ".png,.jpg,.jpeg", (ok, paths) =>
                    {
                        if (ok && paths.Count > 0)
                        {
                            try
                            {
                                byte[] bytes = System.IO.File.ReadAllBytes(paths[0]);
                                currentSystem.bannerBytes = bytes;
                                _ = System.Threading.Tasks.Task.Run(async () =>
                                { try { currentSystem.bannerTexture = await Plugin.TextureProvider.CreateFromImageAsync(bytes); } catch { } });
                                if (Plugin.character != null)
                                    Networking.DataSender.UploadSystemImage(Plugin.character, currentSystem.id, 0, bytes);
                            }
                            catch { }
                        }
                    }, 1, null, false);
                }
                // Logo preview
                if (currentSystem.logoTexture != null && currentSystem.logoTexture.Handle != IntPtr.Zero)
                {
                    ImGui.Image(currentSystem.logoTexture.Handle, new Vector2(32, 32));
                    ImGui.SameLine();
                }
                if (ThemeManager.GhostButton("Set Logo##setLogo"))
                {
                    systemFileDialog.OpenFileDialog("Select Logo", ".png,.jpg,.jpeg", (ok, paths) =>
                    {
                        if (ok && paths.Count > 0)
                        {
                            try
                            {
                                byte[] bytes = System.IO.File.ReadAllBytes(paths[0]);
                                currentSystem.logoBytes = bytes;
                                _ = System.Threading.Tasks.Task.Run(async () =>
                                { try { currentSystem.logoTexture = await Plugin.TextureProvider.CreateFromImageAsync(bytes); } catch { } });
                                if (Plugin.character != null)
                                    Networking.DataSender.UploadSystemImage(Plugin.character, currentSystem.id, 1, bytes);
                            }
                            catch { }
                        }
                    }, 1, null, false);
                }
            }

            ImGui.Spacing();

            // Export / Import buttons
            if (currentSystem.id > 0)
            {
                if (ThemeManager.GhostButton("Export System##export"))
                    SystemExportImport.ExportSystem(currentSystem);
                ImGui.SameLine();
            }
            if (ThemeManager.GhostButton("Import System##import"))
                SystemExportImport.ImportSystem();

            SystemExportImport.DrawStatusMessage();
            } // End collapsible System Settings
        }
     
        public void Dispose()
        {
        }
    }
}