using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.NavLayouts;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using Networking;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using AbsoluteRP.Network;
namespace AbsoluteRP.Windows;

// The main plugin window - login/registration, account import/export, character selection, server status display, and navigation buttons to all other windows (profile, social, etc.)
public class AccountWindow : Window, IDisposable
{
    // input field strings window state toggles
    public static string tagName = string.Empty;
    private bool openTagPopup = false;
    private bool showMainPanel = false; // Control main panel visibility
    private string tempTagName = string.Empty;
    private static AbsoluteRP.RsUI.RsFileDialogManager _fileDialogManager = new AbsoluteRP.RsUI.RsFileDialogManager();
    private string importStatus = string.Empty;
    private Vector4 importStatusColor = new Vector4(1, 1, 1, 1);
    private int selectedNavIndex = 0;
    private Func<bool>[] navButtons;
    public static bool viewProfile, viewSystems, viewEvents, viewConnections, viewListings;
    public static bool login = true;
    public static bool forgot = false;
    public static bool register = false;
    public static bool loggedIn = false;
    // width and height values scaling the elements
    public static int width = 0, height = 0;
    public static bool Remember = false;
    public bool AutoLogin = true;
    public static float paddingX = 0;
    private bool openRemoveAccountPopup = false;
    public static float buttonWidth = 0;
    public static float buttonHeight = 0;
    public static int navigationIndex = 0;
    public static int extraNavIndex = 0;
    // Server status label
    public static string serverStatus = "Connection Status...";
    public static Vector4 serverStatusColor = new Vector4(255, 255, 255, 255);
    public static string status = "";
    public static Vector4 statusColor = new Vector4(255, 255, 255, 255);
    // button images

    public static bool LoggedIN = false;
    public static string lodeStoneKey = string.Empty;
    public static Vector2 ButtonSize = new Vector2();
    public static float centeredX = 0f;

    // Faux name UI state
    private int selectedFauxNameIndex = 0;
    private bool openAddFauxNamePopup = false;
    private string newFauxNameInput = string.Empty;

    // Set so hub pages can host the panel content without the standalone window.
    public static AccountWindow? Instance;

    public AccountWindow() : base(
        "ABSOLUTE ROLEPLAY")
    {

        // Navigation Panel (pinned outside, only covers buttons)
        float headerHeight = 48f; // Height of your main panel's header/title bar
        float buttonSize = ImGui.GetIO().FontGlobalScale * 45; // Height of each navigation button
        int buttonCount = 5;      // Number of navigation buttons

        float navHeight = buttonSize * buttonCount * 1.05f;
        Vector2 Size = new Vector2(navHeight / 1.8f, navHeight * 1.02f);

        Instance = this;
    }
    public override void OnOpen()
    {
        Accounts_DS.SendLogin();
    }

    public void Dispose()
    {

    }
    // now loaded through account page
    public override void Draw()
    {/*
        if (ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows)
            && ImGui.IsMouseClicked(ImGuiMouseButton.Left)
            && !ImGui.IsAnyItemActive()
            && !ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId)
            && !openRemoveAccountPopup
            && !openTagPopup
            && !openAddFauxNamePopup)
        {
            ImGui.SetWindowFocus("MainPanelNavigation");
            ImGui.SetWindowFocus("ABSOLUTE ROLEPLAY");
        }

        // Diagnostic check before we attempt to request focus.
        var hovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows);
        var clicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        var anyActive = ImGui.IsAnyItemActive();
        var alreadyFocused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);

        var focusRequested = hovered && clicked && !anyActive && !alreadyFocused;
        // Get MainPanel window position and size
        Vector2 mainPanelPos = ImGui.GetWindowPos();
        Vector2 mainPanelSize = ImGui.GetWindowSize();

        // --- Navigation Panel (pinned outside, only covers buttons) ---
        float headerHeight = 48f; // Height of your main panel's header/title bar
        float buttonSize = ImGui.GetIO().FontGlobalScale * 45; // Height of each navigation button
        int buttonCount = 6;      // Number of navigation buttons

        float navHeight = buttonSize * buttonCount * 1.2f;
        DrawMainUI();

        Navigation extraNav = NavigationLayouts.ExtraNavigation();
        UIHelpers.DrawExtraNav(extraNav, ref extraNavIndex);
        // Position navigation window to the left of MainPanel, just below the header
        ImGui.SetNextWindowPos(new Vector2(mainPanelPos.X - buttonSize * 1.5f, mainPanelPos.Y + headerHeight), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(buttonSize * 1.5f, navHeight), ImGuiCond.Always);

        ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar;
        Navigation nav = NavigationLayouts.MainUINavigation();
        UIHelpers.DrawSideNavigation("ABSOLUTE ROLEPLAY", "MainPanelNavigation", ref navigationIndex, flags, nav, focusRequested);
        */
    }
    public void DrawMainUI()
    {
        try
        {
            ButtonSize = new Vector2(ImGui.GetIO().FontGlobalScale / 0.005f);
            buttonWidth = ImGui.GetWindowSize().X / 2.4f;
            buttonHeight = ButtonSize.Y / 5f;

            centeredX = (ImGui.GetWindowSize().X - ButtonSize.X) / 2.0f;



            if (Plugin.plugin?.Configuration?.account != null &&
              !string.IsNullOrEmpty(Plugin.plugin.Configuration.account.accountKey) &&
              !string.IsNullOrEmpty(Plugin.plugin.Configuration.account.accountName))
            {
                // Avatar at top center of the panel + display name preference.
                DrawAccountHeader(Plugin.plugin.Configuration.account);

                Misc.SetTitle(Plugin.plugin, true,
                    Plugin.plugin.Configuration.account.EffectiveDisplayName,
                    new Vector4(0, 1, 0, 0));

                // Edit account profile - jumps into Settings -> Profile tab.
                if (ThemeManager.GhostButton("Edit account profile", new Vector2(buttonWidth * 2.18f, buttonHeight / 1.4f)))
                {
                    try
                    {
                            AbsoluteRP.RsUI.MainWindow.Instance?.OpenPage("settings");
                            AbsoluteRP.Windows.OptionsWindow.Instance?.SelectSection(
                            AbsoluteRP.Windows.OptionsWindow.SectionProfile);
                    }
                    catch (Exception ex) { Plugin.PluginLog?.Debug("Edit profile nav: " + ex.Message); }
                }

                using (ImRaii.Disabled(!Plugin.CtrlPressed()))
                {
                    if (ThemeManager.DangerButton("Remove Account", new Vector2(buttonWidth * 2.18f, buttonHeight / 1.4f)))
                    {
                        openRemoveAccountPopup = true;
                        ImGui.OpenPopup("Remove Account?##rs_remove");
                    }
                }
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.SetTooltip("Hold Ctrl to enable");
                }
                // Faux Name Section
                ThemeManager.GradientSeparator();
              
                

               
                // Remove Account Confirmation Popup
                if (openRemoveAccountPopup)
                {
                    ImGui.SetNextWindowSize(new Vector2(AbsoluteRP.RsUI.RsTheme.S(400f), 0), ImGuiCond.Always);
                    if (AbsoluteRP.Windows.Inventory.InvUI.BeginModal("Remove Account?##rs_remove", ref openRemoveAccountPopup))
                    {
                        AbsoluteRP.Windows.Inventory.InvUI.SectionLabel("Remove this account");
                        ImGui.TextColored(ThemeManager.Error, "Are you sure you want to remove this account from this plugin?");
                        ImGui.Spacing();
                        using (ImRaii.Disabled(!Plugin.CtrlPressed()))
                        {
                            if (ThemeManager.DangerButton("Confirm", new Vector2(120, 0)))
                            {
                                Accounts_DS.UnlinkAccount();
                                Plugin.plugin.Configuration.account.accountName = string.Empty;
                                Plugin.plugin.Configuration.account.accountKey = string.Empty;
                                Plugin.plugin.Configuration.characters.Remove(
                                     Plugin.plugin.Configuration.characters.FirstOrDefault(
                                        x => x.characterName == Plugin.character.characterName &&
                                             x.characterWorld == Plugin.character.characterWorld));
                                Plugin.plugin.Configuration.Save();
                                openRemoveAccountPopup = false;
                                ImGui.CloseCurrentPopup();
                            }
                        }
                        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        {
                            ImGui.SetTooltip("Hold Ctrl to enable");
                        }
                        ImGui.SameLine();
                        if (ThemeManager.GhostButton("Cancel", new Vector2(120, 0)))
                        {
                            openRemoveAccountPopup = false;
                            ImGui.CloseCurrentPopup();
                        }
                        AbsoluteRP.Windows.Inventory.InvUI.EndModal();
                    }
                }
            }
            if (Plugin.plugin?.Configuration.account.accountKey != string.Empty && Plugin.plugin?.Configuration.account.accountName != string.Empty)
            {
                if (ThemeManager.PillButton("View Likes", new Vector2(buttonWidth * 2.18f, buttonHeight / 1.4f)))
                {
                    Plugin.plugin.ToggleViewLikesWindow();
                }

                if (ThemeManager.GhostButton("Export Account", new Vector2(buttonWidth * 2.18f, buttonHeight / 1.4f)))
                {
                    _fileDialogManager.SaveFileDialog("Export Account Data", "JSON{.json}",
                        $"AbsoluteRP_Plugin_{Plugin.plugin.Configuration.account.accountName}.json", ".json",
                        (success, filePath) =>
                        {
                            if (!success || string.IsNullOrEmpty(filePath))
                                return;

                            try
                            {
                                var exportData = new PluginExportData
                                {
                                    exportType = "AbsoluteRP_PluginExport",
                                    exportVersion = 1,
                                    exportDate = DateTime.UtcNow.ToString("o"),
                                    accountKey = Plugin.plugin.Configuration.account.accountKey,
                                    accountName = Plugin.plugin.Configuration.account.accountName,
                                    characters = Plugin.plugin.Configuration.characters.Select(c => new WebExportCharacter
                                    {
                                        characterName = c.characterName,
                                        characterWorld = c.characterWorld,
                                        characterKey = c.characterKey
                                    }).ToList()
                                };

                                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                                var jsonContent = JsonSerializer.Serialize(exportData, jsonOptions);
                                File.WriteAllText(filePath, jsonContent);

                                status = "Account data exported successfully!";
                                statusColor = new Vector4(0.3f, 1, 0.3f, 1);
                            }
                            catch (Exception ex)
                            {
                                status = $"Export failed: {ex.Message}";
                                statusColor = new Vector4(1, 0.3f, 0.3f, 1);
                                Plugin.PluginLog.Error($"Export account error: {ex}");
                            }
                        }, null, Plugin.plugin.Configuration.AlwaysOpenDefaultImport);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Export account data to import on the website");
                }

                if (ThemeManager.GhostButton("Options", new Vector2(buttonWidth * 2.18f, buttonHeight / 1.4f)))
                {
                    Plugin.plugin.OpenOptionsWindow();
                }



                ThemeManager.GradientSeparator();
            }
            else
            {
                if (ThemeManager.PillButton("Get Started!", new Vector2(buttonWidth * 2.14f, buttonHeight / 1.2f)))
                {
                    openTagPopup = true;
                    ImGui.OpenPopup("Register Account##rs_register");
                }

                // Import Account button - below Get Started
                if (ThemeManager.GhostButton("Import Account", new Vector2(buttonWidth * 2.14f, buttonHeight / 1.4f)))
                {
                    _fileDialogManager.OpenFileDialog("Import Account Data", "JSON{.json}", (success, files) =>
                    {
                        if (!success || files.Count == 0)
                            return;

                        try
                        {
                            var filePath = files[0].ToString();
                            if (!File.Exists(filePath))
                            {
                                importStatus = "File not found.";
                                importStatusColor = new Vector4(1, 0.3f, 0.3f, 1);
                                return;
                            }

                            var jsonContent = File.ReadAllText(filePath);

                            // Try WebExport format first, then PluginExport format
                            string importAccountKey = string.Empty;
                            string importAccountName = string.Empty;
                            List<WebExportCharacter>? importCharacters = null;

                            var webImport = JsonSerializer.Deserialize<WebExportData>(jsonContent);
                            if (webImport != null && webImport.exportType == "AbsoluteRP_WebExport")
                            {
                                importAccountKey = webImport.account.accountKey;
                                importAccountName = webImport.account.accountName;
                                importCharacters = webImport.characters;
                            }
                            else
                            {
                                // Try PluginExport format (from "Export for Website" button)
                                var pluginImport = JsonSerializer.Deserialize<PluginExportData>(jsonContent);
                                if (pluginImport != null && pluginImport.exportType == "AbsoluteRP_PluginExport")
                                {
                                    importAccountKey = pluginImport.accountKey;
                                    importAccountName = pluginImport.accountName;
                                    importCharacters = pluginImport.characters;
                                }
                            }

                            if (string.IsNullOrEmpty(importAccountKey))
                            {
                                importStatus = "Invalid file format.";
                                importStatusColor = new Vector4(1, 0.3f, 0.3f, 1);
                                return;
                            }

                            // Apply the imported data to the configuration
                            Plugin.plugin.Configuration.account.accountKey = importAccountKey;
                            Plugin.plugin.Configuration.account.accountName = importAccountName;

                            // Import characters
                            if (importCharacters != null)
                            {
                                foreach (var importChar in importCharacters)
                                {
                                    // Check if character already exists
                                    var existingChar = Plugin.plugin.Configuration.characters.FirstOrDefault(
                                        c => c.characterName == importChar.characterName &&
                                             c.characterWorld == importChar.characterWorld);

                                    if (existingChar == null)
                                    {
                                        Plugin.plugin.Configuration.characters.Add(new Character
                                        {
                                            characterName = importChar.characterName,
                                            characterWorld = importChar.characterWorld,
                                            characterKey = importChar.characterKey
                                        });
                                    }
                                    else
                                    {
                                        // Update existing character key
                                        existingChar.characterKey = importChar.characterKey;
                                    }
                                }
                            }

                            Plugin.plugin.Configuration.Save();

                            importStatus = "Account data loaded. Verifying with server...";
                            importStatusColor = new Vector4(1, 1, 0.3f, 1);

                            // Attempt to login with the imported credentials The server will verify the account exists - the login response handler will update the status accordingly
                            Accounts_DS.SendLogin();
                        }
                        catch (Exception ex)
                        {
                            importStatus = $"Import failed: {ex.Message}";
                            importStatusColor = new Vector4(1, 0.3f, 0.3f, 1);
                            Plugin.PluginLog.Error($"Import account error: {ex}");
                        }
                    }, 1, null, Plugin.plugin.Configuration.AlwaysOpenDefaultImport);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Import account data exported from the website");
                }

                // Show import status message
                if (!string.IsNullOrEmpty(importStatus))
                {
                    ImGui.TextColored(importStatusColor, importStatus);
                }
            }
            // Popup logic
            if (openTagPopup)
            {
                // Themed modal (same chrome as the inventory dialogs): no default ImGui title bar, RsUI inputs and buttons.
                ImGui.SetNextWindowSize(new Vector2(AbsoluteRP.RsUI.RsTheme.S(400f), 0), ImGuiCond.Always);
                if (AbsoluteRP.Windows.Inventory.InvUI.BeginModal("Register Account##rs_register", ref openTagPopup))
                {
                    AbsoluteRP.Windows.Inventory.InvUI.SectionLabel("Create your account");
                    ImGui.PushStyleColor(ImGuiCol.Text, AbsoluteRP.RsUI.RsTheme.TextSecondary);
                    ImGui.TextWrapped("Pick a unique account name. You'll receive an account key that logs you in here and on the website.");
                    ImGui.PopStyleColor();
                    ImGui.Spacing();
                    AbsoluteRP.RsUI.RsElements.InputText("rs_register_name", ref tempTagName, 25, "Account name", 360f);
                    ImGui.Spacing(); ImGui.Spacing();
                    var half = (ImGui.GetContentRegionAvail().X - AbsoluteRP.RsUI.RsTheme.S(8f)) * 0.5f / MathF.Max(0.01f, AbsoluteRP.RsUI.RsTheme.Scale);
                    using (ImRaii.Disabled(string.IsNullOrWhiteSpace(tempTagName)))
                    {
                        if (AbsoluteRP.RsUI.RsElements.Button("Create account##rs_register_ok", AbsoluteRP.RsUI.RsElements.ButtonVariant.Primary, new Vector2(half, 0)))
                        {
                            tagName = tempTagName.Trim();
                            Accounts_DS.CreateUserTag(tagName);
                            ImGui.CloseCurrentPopup();
                            openTagPopup = false;
                        }
                    }
                    ImGui.SameLine(0f, AbsoluteRP.RsUI.RsTheme.S(8f));
                    if (AbsoluteRP.RsUI.RsElements.Button("Cancel##rs_register_cancel", AbsoluteRP.RsUI.RsElements.ButtonVariant.Ghost, new Vector2(half, 0)))
                    {
                        ImGui.CloseCurrentPopup();
                        openTagPopup = false;
                    }
                    AbsoluteRP.Windows.Inventory.InvUI.EndModal();
                }
            }

            // Draw file dialog
            _fileDialogManager.Draw();



            float xpos = ImGui.GetCursorPosX();
            ImGui.SetCursorPosX(xpos + 4);
            // Use themed status dot for connection status
            var dotColor = ClientTCP.Connected ? ThemeManager.Success : ThemeManager.Error;
            ThemeManager.StatusDot(serverStatus, dotColor);
            ImGui.SameLine();

            if (!ClientTCP.Connected)
            {
                if (ImGui.ImageButton(UI.UICommonImage(UI.CommonImageTypes.reconnect).Handle, new Vector2(buttonHeight / 2.5f, buttonHeight / 2.5f)))
                {
                    ClientTCP.AttemptConnect();
                    Plugin.plugin.UpdateStatusAsync().GetAwaiter().GetResult();
                    Accounts_DS.SendLogin();
                }
            }
            else
            {
                if (ImGui.ImageButton(UI.UICommonImage(UI.CommonImageTypes.reconnect).Handle, new Vector2(buttonHeight / 2.5f, buttonHeight / 2.5f)))
                {
                    ClientTCP.Disconnect();
                    Plugin.plugin.UpdateStatusAsync().GetAwaiter().GetResult();
                }
            }
            if (loggedIn == false && viewProfile == false && viewListings == false)
            {
                var statusPosY = ImGui.GetCursorPosY();
                ImGui.SetCursorPos(new Vector2(centeredX, statusPosY));
            }
            ImGui.SetCursorPosX(xpos + 10);
            using (ImRaii.PushColor(ImGuiCol.Text, statusColor))
            {
                ImGui.TextWrapped(status);
            }

        }
        catch (Exception e)
        {
            Plugin.PluginLog.Debug("MainPanel Draw Debug: " + e.Message);
            Plugin.PluginLog.Debug(e.StackTrace);
        }

        Navigation extraNav = NavigationLayouts.ExtraNavigation();
        UIHelpers.DrawExtraNav(extraNav, ref extraNavIndex);
    }

    public void switchUI()
    {
        viewProfile = false;
        viewSystems = false;
        viewEvents = false;
        viewConnections = false;
        viewListings = false;
    }

    // Header block at the top of the logged-in account panel: circular avatar centered above the display/account name. Kept lightweight and uses the same SocialMediaCache path as the composer/feed so avatars share a texture pool with other social images.
    private static void DrawAccountHeader(AbsoluteRP.Defines.Account acct)
    {
        var avatarSz = AbsoluteRP.RsUI.RsTheme.S(88f);
        var avail    = ImGui.GetContentRegionAvail().X;
        // Center the avatar horizontally by nudging the cursor first.
        var origCursorX = ImGui.GetCursorPosX();
        ImGui.SetCursorPosX(origCursorX + MathF.Max(0f, (avail - avatarSz) * 0.5f));
        AbsoluteRP.Social.SocialAvatar.DrawCircle(
            acct.profile?.avatarUrl,
            avatarSz,
            ImGui.ColorConvertFloat4ToU32(AbsoluteRP.RsUI.RsTheme.BgPrimary));
        ImGui.Spacing();
    }
}
