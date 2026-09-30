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
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Common.Lua;
using Networking;
using Serilog.Filters;
using System.Numerics;
using System.Reflection;
using AbsoluteRP.Network;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode.Delegates;

namespace AbsoluteRP.Windows.Listings
{
    // Main RP Systems editor window - create and manage tabletop-style RP systems with stats, resources, combat config, skill classes, skill trees, and rules. Systems can be shared via share codes so other players can join and create character sheets.
    internal class SystemsWindow : Window, IDisposable
    {
        // Set when the window is constructed so the RsUI hub can embed the same content.
        public static SystemsWindow? Instance;
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
        private static AbsoluteRP.RsUI.RsFileDialogManager systemFileDialog = new AbsoluteRP.RsUI.RsFileDialogManager();

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
            Instance = this;
        }

        public override void Draw() => DrawContent();

        // Full systems UI; callable from the standalone window or the RsUI hub page. Top-level nav: 0=View Systems, 1=Manage Systems. Starts on View, like the old tab bar did.
        private static int modeNavIndex = 0;
        private static readonly List<AbsoluteRP.RsUI.RsElements.NavItem> ModeNavItems = new()
        {
            new(FontAwesomeIcon.ThLarge, "View Systems"),
            new(FontAwesomeIcon.Cog, "Manage Systems"),
        };

        private static readonly List<AbsoluteRP.RsUI.RsElements.NavItem> SectionNavItems = new()
        {
            new(FontAwesomeIcon.SlidersH, SectionNames[0]),
            new(FontAwesomeIcon.IdCard, SectionNames[1]),
            new(FontAwesomeIcon.Dice, SectionNames[2]),
            new(FontAwesomeIcon.Scroll, SectionNames[3]),
            new(FontAwesomeIcon.Users, SectionNames[4]),
        };

        public void DrawContent()
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
                AbsoluteRP.Network.Systems_DS.FetchMySystems(Plugin.character);
            }

            // Top-level mode tabs
            AbsoluteRP.RsUI.RsElements.NavigationMenu("sys_mode_nav", ref modeNavIndex, ModeNavItems);
            systemMode = modeNavIndex;
            SysUI.Gap(12f);
            if (systemMode == 0)
                AbsoluteRP.Windows.Systems.ViewSystems.ViewSystems.DrawViewSystems();
            else
                DrawSystemCreation();

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
                SysUI.PushWindowStyle();
                try
                {
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
                }
                finally { SysUI.PopWindowStyle(); }
                showRosterPanel = rosterOpen;
            }
        }

        public static void DrawSystemCreation()
        {
            // System picker / create card
            bool headerOpen = AbsoluteRP.RsUI.RsElements.BeginPanel("sys_manage_header", null, fitContentsY: true);
            try
            {
                if (headerOpen)
                {
                    SysUI.SectionLabel("Your Systems");

                    // Create new system
                    if (systemData.Count == 0)
                        SysUI.MutedWrapped("You haven't created any systems yet. Create one to start building stats, classes and rules.");

                    DrawSystemSelection();

                    if (systemData.Count == 0)
                    {
                        if (AbsoluteRP.RsUI.RsElements.Button("Create System##createSystemEmpty", AbsoluteRP.RsUI.RsElements.ButtonVariant.Primary))
                            CreateNewSystem();
                    }
                }
            }
            finally { AbsoluteRP.RsUI.RsElements.EndPanel(); }

            if (currentSystem == null) return;

            SysUI.Gap(8f);
            DrawSystemSettings();
            SysUI.Gap(12f);

            // Section nav (always visible when a system is selected)
            AbsoluteRP.RsUI.RsElements.NavigationMenu("sys_section_nav", ref systemSectionIndex, SectionNavItems);
            if (systemSectionIndex < 0 || systemSectionIndex >= SectionNames.Length) systemSectionIndex = 0;
            drawStatLayout = (systemSectionIndex == 0);

            SysUI.Gap(12f);

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

        private static void CreateNewSystem()
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
                AbsoluteRP.Network.Systems_DS.CreateSystem(Plugin.character, newSystem.name, newSystem.description);
        }

        /// Saves all system data (stats, combat, classes, skills, rules) to the server.
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
            await AbsoluteRP.Network.Systems_DS.UpdateSystemSettings(character, system.id, system.name, system.basePointsAvailable, system.requireApproval, system.rules, system.restrictResourceModification, system.rollForStats, system.rollForSkills);
            await AbsoluteRP.Network.Systems_DS.SaveSystemStats(character, system.id, system.StatsData);
            await AbsoluteRP.Network.Systems_DS.SaveCombatConfig(character, system.id, system.CombatConfig, system.Resources);
            await AbsoluteRP.Network.Systems_DS.SaveSkillClasses(character, system.id, system.SkillClasses);
            await AbsoluteRP.Network.Systems_DS.SaveSkills(character, system.id, system.Skills, system.SkillConnections);
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

            var scale = AbsoluteRP.RsUI.RsTheme.Scale;
            var gap = SysUI.S(8f);

            // Picker row: [systems dropdown] [Delete] [Create System]
            var names = new List<string>(systemData.Count);
            for (int idx = 0; idx < systemData.Count; idx++)
                names.Add(string.IsNullOrEmpty(systemData[idx].name) ? "New System" : systemData[idx].name);

            bool canDelete = currentSystem != null && currentSystem.id > 0;
            float reserve = AbsoluteRP.RsUI.RsElements.MeasureButtonWidth("Create System") + gap;
            if (canDelete) reserve += AbsoluteRP.RsUI.RsElements.MeasureButtonWidth("Delete") + gap;
            float ddW = Math.Max(SysUI.S(140f), AbsoluteRP.RsUI.RsElements.AvailContentWidth() - reserve) / scale;

            int sel = currentSystemIndex;
            if (AbsoluteRP.RsUI.RsElements.Dropdown("sys_picker", ref sel, names, ddW))
            {
                if (sel >= 0 && sel < systemData.Count)
                {
                    currentSystemIndex = sel;
                    currentSystem = systemData[sel];
                    drawStatLayout = true;
                    Stats.currentStatIndex = -1;
                    Stats.selectedStat = null;

                    // Fetch full system data from server when selecting a different system
                    if (Plugin.character != null && systemData[sel].id > 0)
                        AbsoluteRP.Network.Systems_DS.FetchSystem(Plugin.character, systemData[sel].id);
                }
            }
            if (ImGui.IsPopupOpen("##rs_dd_pop_sys_picker"))
                uiSelected = true;

            // Delete system button
            if (canDelete)
            {
                ImGui.SameLine(0f, gap);
                if (AbsoluteRP.RsUI.RsElements.Button("Delete##delSystem", AbsoluteRP.RsUI.RsElements.ButtonVariant.Danger))
                {
                    showDeleteConfirm = true;
                    ImGui.OpenPopup("##DeleteSystemConfirm");
                }
            }

            ImGui.SameLine(0f, gap);
            if (AbsoluteRP.RsUI.RsElements.Button("Create System##createSystem", AbsoluteRP.RsUI.RsElements.ButtonVariant.Primary))
                CreateNewSystem();

            // Delete confirmation popup
            if (SysUI.BeginModal("##DeleteSystemConfirm", ref showDeleteConfirm))
            {
                SysUI.Primary($"Delete system \"{currentSystem?.name}\"?");
                SysUI.Gap(4f);
                SysUI.Text("This will permanently delete the system and all its data.", AbsoluteRP.RsUI.RsTheme.AccentDanger);
                SysUI.Text("This action cannot be undone.", AbsoluteRP.RsUI.RsTheme.AccentDanger);
                SysUI.Gap(8f);

                bool ctrlHeld = ImGui.GetIO().KeyCtrl;
                if (SysUI.Button("Confirm Delete##confirmDel", AbsoluteRP.RsUI.RsElements.ButtonVariant.Danger, ctrlHeld))
                {
                    if (currentSystem != null && Plugin.character != null)
                    {
                        AbsoluteRP.Network.Systems_DS.DeleteSystem(Plugin.character, currentSystem.id);
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

                ImGui.SameLine();
                if (AbsoluteRP.RsUI.RsElements.Button("Cancel##cancelDel", AbsoluteRP.RsUI.RsElements.ButtonVariant.Secondary))
                {
                    showDeleteConfirm = false;
                    ImGui.CloseCurrentPopup();
                }

                if (!ctrlHeld)
                    SysUI.Muted("Hold CTRL to enable");
                SysUI.EndModal();
            }

            if (systemData.Count == 0 || currentSystemIndex < 0 || currentSystemIndex >= systemData.Count)
                return;

            SysUI.Gap(10f);
            SysUI.Divider();
            SysUI.SectionLabel("System Name");

            // Name row: [name input] [Save All | Creating...]
            float saveW = AbsoluteRP.RsUI.RsElements.MeasureButtonWidth("Save All");
            float nameW = Math.Max(SysUI.S(120f), AbsoluteRP.RsUI.RsElements.AvailContentWidth() - saveW - gap) / scale;
            string systemName = systemData[currentSystemIndex].name;
            if (AbsoluteRP.RsUI.RsElements.InputText("sys_name", ref systemName, 256, "Enter system name...", nameW))
            {
                systemData[currentSystemIndex].name = systemName;
                if (currentSystem != null)
                    currentSystem.name = systemName;
            }
            ImGui.SameLine(0f, gap);
            if (currentSystem != null && currentSystem.id > 0)
            {
                if (AbsoluteRP.RsUI.RsElements.Button("Save All##saveAll", AbsoluteRP.RsUI.RsElements.ButtonVariant.Primary))
                    SaveAllSystemData();
            }
            else if (currentSystem != null && currentSystem.id <= 0)
            {
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + SysUI.S(8f));
                SysUI.Muted("Creating...");
            }

        }

        // Collapsible settings area (drawn below the picker card so the collapsible header can span the full width).
        private static void DrawSystemSettings()
        {
            if (currentSystem == null) return;
            if (systemData.Count == 0 || currentSystemIndex < 0 || currentSystemIndex >= systemData.Count) return;

            bool settingsOpen = AbsoluteRP.RsUI.RsElements.BeginCollapsible("sys_settings", "System Settings", ref settingsExpanded);
            try
            {
                if (settingsOpen)
                    DrawSystemSettingsBody();
            }
            finally { AbsoluteRP.RsUI.RsElements.EndCollapsible(); }
        }

        private static void DrawSystemSettingsBody()
        {
            if (currentSystem == null) return;

            // Share code
            if (!string.IsNullOrEmpty(currentSystem.shareCode))
            {
                SysUI.SectionLabel("Share Code");
                ImGui.AlignTextToFramePadding();
                SysUI.Accent(currentSystem.shareCode);
                ImGui.SameLine(0f, SysUI.S(10f));
                if (AbsoluteRP.RsUI.RsElements.Button("Copy##copyCode", AbsoluteRP.RsUI.RsElements.ButtonVariant.Secondary))
                    ImGui.SetClipboardText(currentSystem.shareCode);
                SysUI.Gap(6f);
            }

            SysUI.SectionLabel("Permissions");

            // Require approval toggle
            bool reqApproval = currentSystem.requireApproval;
            if (AbsoluteRP.RsUI.RsElements.Toggle("sys_req_approval", ref reqApproval, "Require approval for character sheets"))
                currentSystem.requireApproval = reqApproval;

            SysUI.Gap(4f);

            // Restrict resource modification toggle
            bool restrictRes = currentSystem.restrictResourceModification;
            if (AbsoluteRP.RsUI.RsElements.Toggle("sys_restrict_res", ref restrictRes, "Restrict resource modification to owner only"))
                currentSystem.restrictResourceModification = restrictRes;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Enable this if you do not want anyone to modify their own HP or resources.\nThe system owner can always modify resources regardless.");

            SysUI.Gap(4f);

            // Rolled character creation
            bool rollStats = currentSystem.rollForStats;
            if (AbsoluteRP.RsUI.RsElements.Toggle("sys_roll_stats", ref rollStats, "Roll for stats instead of allocating points"))
                currentSystem.rollForStats = rollStats;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Each stat is rolled once with the system's combat dice when a character is created,\nclamped to the stat's range. The point budget is not used.");

            bool rollSkills = currentSystem.rollForSkills;
            if (AbsoluteRP.RsUI.RsElements.Toggle("sys_roll_skills", ref rollSkills, "Roll for skill points instead of a fixed amount"))
                currentSystem.rollForSkills = rollSkills;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The class's starting skill points are rolled with the combat dice\n(capped at the class's skill point amount when one is set).");

            // Banner / Logo upload
            if (currentSystem.id > 0)
            {
                SysUI.Gap(8f);
                SysUI.SectionLabel("Branding");
                // Banner preview
                if (currentSystem.bannerTexture != null && currentSystem.bannerTexture.Handle != IntPtr.Zero)
                {
                    float availW = AbsoluteRP.RsUI.RsElements.AvailContentWidth();
                    float imgW = currentSystem.bannerTexture.Width;
                    float imgH = currentSystem.bannerTexture.Height;
                    float aspect = imgH / imgW;
                    float displayW = availW;
                    float displayH = displayW * aspect;
                    float maxH = SysUI.S(150f);
                    if (displayH > maxH) { displayH = maxH; displayW = displayH / aspect; }
                    var p = ImGui.GetCursorScreenPos();
                    ImGui.GetWindowDrawList().AddImageRounded(currentSystem.bannerTexture.Handle, p, p + new Vector2(displayW, displayH),
                        Vector2.Zero, Vector2.One, 0xFFFFFFFF, SysUI.S(6f));
                    ImGui.Dummy(new Vector2(displayW, displayH));
                    SysUI.Gap(4f);
                }
                if (AbsoluteRP.RsUI.RsElements.Button("Set Banner##setBanner", AbsoluteRP.RsUI.RsElements.ButtonVariant.Secondary))
                {
                    AbsoluteRP.RsUI.RsFileDialog.OpenImagePicker("Choose a system banner", (ok, path) =>
                    {
                        if (!ok || string.IsNullOrEmpty(path)) return;
                        try
                        {
                            byte[] bytes = System.IO.File.ReadAllBytes(path);
                            currentSystem.bannerBytes = bytes;
                            _ = System.Threading.Tasks.Task.Run(async () =>
                            { try { currentSystem.bannerTexture = await Plugin.TextureProvider.CreateFromImageAsync(bytes); } catch { } });
                            if (Plugin.character != null)
                                AbsoluteRP.Network.Systems_DS.UploadSystemImage(Plugin.character, currentSystem.id, 0, bytes);
                        }
                        catch { }
                    });
                }
                ImGui.SameLine(0f, SysUI.S(8f));
                // Logo preview
                if (currentSystem.logoTexture != null && currentSystem.logoTexture.Handle != IntPtr.Zero)
                {
                    float logo = ImGui.GetTextLineHeight() + SysUI.S(18f);
                    var p = ImGui.GetCursorScreenPos();
                    ImGui.GetWindowDrawList().AddImageRounded(currentSystem.logoTexture.Handle, p, p + new Vector2(logo, logo),
                        Vector2.Zero, Vector2.One, 0xFFFFFFFF, logo * 0.5f);
                    ImGui.Dummy(new Vector2(logo, logo));
                    ImGui.SameLine(0f, SysUI.S(8f));
                }
                if (AbsoluteRP.RsUI.RsElements.Button("Set Logo##setLogo", AbsoluteRP.RsUI.RsElements.ButtonVariant.Secondary))
                {
                    AbsoluteRP.RsUI.RsFileDialog.OpenImagePicker("Choose a system logo", (ok, path) =>
                    {
                        if (!ok || string.IsNullOrEmpty(path)) return;
                        try
                        {
                            byte[] bytes = System.IO.File.ReadAllBytes(path);
                            currentSystem.logoBytes = bytes;
                            _ = System.Threading.Tasks.Task.Run(async () =>
                            { try { currentSystem.logoTexture = await Plugin.TextureProvider.CreateFromImageAsync(bytes); } catch { } });
                            if (Plugin.character != null)
                                AbsoluteRP.Network.Systems_DS.UploadSystemImage(Plugin.character, currentSystem.id, 1, bytes);
                        }
                        catch { }
                    });
                }
            }

            SysUI.Gap(8f);
            SysUI.SectionLabel("Backup");

            // Export / Import buttons
            if (currentSystem.id > 0)
            {
                if (AbsoluteRP.RsUI.RsElements.Button("Export System##export", AbsoluteRP.RsUI.RsElements.ButtonVariant.Secondary))
                    SystemExportImport.ExportSystem(currentSystem);
                ImGui.SameLine(0f, SysUI.S(8f));
            }
            if (AbsoluteRP.RsUI.RsElements.Button("Import System##import", AbsoluteRP.RsUI.RsElements.ButtonVariant.Secondary))
                SystemExportImport.ImportSystem();

            SystemExportImport.DrawStatusMessage();
            SysUI.Gap(4f);
        }
     
        public void Dispose()
        {
        }
    }
}
