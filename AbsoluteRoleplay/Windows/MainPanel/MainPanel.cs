using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.NavLayouts;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using Networking;
using System;
using System.Numerics;
namespace AbsoluteRP.Windows.MainPanel;

public class MainPanel : Window, IDisposable
{
    //input field strings
    //window state toggles
    public static string tagName = string.Empty;
    private bool openTagPopup = false;
    private bool showMainPanel = false; // Control main panel visibility
    private string tempTagName = string.Empty;
    private int selectedNavIndex = 0;
    private Func<bool>[] navButtons;
    public static bool viewProfile, viewSystems, viewEvents, viewConnections, viewListings;
    public static bool login = true;
    public static bool forgot = false;
    public static bool register = false;
    public static bool loggedIn = false;
    //width and height values scaling the elements
    public static int width = 0, height = 0;
    public static bool Remember = false;
    public bool AutoLogin = true;
    public static float paddingX = 0;
    private bool openRemoveAccountPopup = false;
    public static float buttonWidth = 0;
    public static float buttonHeight = 0;
    public static int navigationIndex = 0;
    public static int extraNavIndex = 0;
    //duh
    //server status label stuff
    public static string serverStatus = "Connection Status...";
    public static Vector4 serverStatusColor = new Vector4(255, 255, 255, 255);
    public static string status = "";
    public static Vector4 statusColor = new Vector4(255, 255, 255, 255);
    //button images

    public static bool LoggedIN = false;
    public static string lodeStoneKey = string.Empty;
    public static Vector2 ButtonSize = new Vector2();
    public static float centeredX = 0f;

    public MainPanel() : base(
        "ABSOLUTE ROLEPLAY")
    {

        // --- Navigation Panel (pinned outside, only covers buttons) ---
        float headerHeight = 48f; // Height of your main panel's header/title bar
        float buttonSize = ImGui.GetIO().FontGlobalScale * 45; // Height of each navigation button
        int buttonCount = 5;      // Number of navigation buttons

        float navHeight = buttonSize * buttonCount * 1.05f;
        Vector2 Size = new Vector2(navHeight / 1.8f, navHeight * 1.02f);


    }
    public override void OnOpen()
    {
        DataSender.SendLogin();
    }

    public void Dispose()
    {

    }
    public override void Draw()
    {
        if (ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows)
            && ImGui.IsMouseClicked(ImGuiMouseButton.Left)
            && !ImGui.IsAnyItemActive()
            && !openRemoveAccountPopup
            && !openTagPopup)
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

        // Draw your main panel content here
        // ... (your tab/content logic) ...

        // --- Navigation Panel (pinned outside, only covers buttons) ---
        float headerHeight = 48f; // Height of your main panel's header/title bar
        float buttonSize = ImGui.GetIO().FontGlobalScale * 45; // Height of each navigation button
        int buttonCount = 5;      // Number of navigation buttons

        float navHeight = buttonSize * buttonCount * 1.2f;
        DrawMainUI();

        Navigation extraNav = NavigationLayouts.ExtraNavigation();
        UIHelpers.DrawExtraNav(extraNav, ref extraNavIndex);
        // Position navigation window to the left of MainPanel, just below the header
        ImGui.SetNextWindowPos(new Vector2(mainPanelPos.X - buttonSize * 1.2f, mainPanelPos.Y + headerHeight), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(buttonSize * 1.2f, navHeight), ImGuiCond.Always);

        ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar;
        Navigation nav = NavigationLayouts.MainUINavigation();
        UIHelpers.DrawSideNavigation("ABSOLUTE ROLEPLAY", "MainPanelNavigation", ref navigationIndex, flags, nav, focusRequested);
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
                Misc.SetTitle(Plugin.plugin, true, Plugin.plugin.Configuration.account.accountName, new Vector4(0, 1, 0, 0));
                using (ImRaii.Disabled(!Plugin.CtrlPressed()))
                {
                    if (ImGui.Button("Remove Account", new Vector2(buttonWidth * 2.18f, buttonHeight / 2f)))
                    {
                        openRemoveAccountPopup = true;
                        ImGui.OpenPopup("Remove Account?");
                    }
                }
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.SetTooltip("Hold Ctrl to enable");
                }

                // Remove Account Confirmation Popup
                if (openRemoveAccountPopup)
                {
                    ImGui.SetNextWindowSize(new Vector2(350, 0), ImGuiCond.Always);
                    if (ImGui.BeginPopupModal("Remove Account?", ref openRemoveAccountPopup, ImGuiWindowFlags.AlwaysAutoResize))
                    {
                        ImGui.TextColored(new Vector4(1, 0.2f, 0.2f, 1), "Are you sure you want to remove this account?");
                        ImGui.Spacing();
                        using (ImRaii.Disabled(!Plugin.CtrlPressed()))
                        {
                            if (ImGui.Button("Confirm", new Vector2(120, 0)))
                            {
                                DataSender.UnlinkAccount();
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
                        if (ImGui.Button("Cancel", new Vector2(120, 0)))
                        {
                            openRemoveAccountPopup = false;
                            ImGui.CloseCurrentPopup();
                        }
                        ImGui.EndPopup();
                    }
                }
            }
            if (Plugin.plugin?.Configuration.account.accountKey != string.Empty && Plugin.plugin?.Configuration.account.accountName != string.Empty)
            {
                if (ImGui.Button("View Likes", new Vector2(buttonWidth * 2.18f, buttonHeight / 2f)))
                {
                    Plugin.plugin.ToggleViewLikesWindow();
                }

                if (ImGui.Button("Options", new Vector2(buttonWidth * 2.18f, buttonHeight / 2f)))
                {
                    Plugin.plugin.OpenOptionsWindow();
                }
            }
            else
            {
                if (ImGui.Button("Get Started!", new Vector2(buttonWidth * 2.14f, buttonHeight / 1.8f)))
                {
                    openTagPopup = true;
                    ImGui.OpenPopup("Register Account");
                }
            }
            // Popup logic
            if (openTagPopup)
            {
                ImGui.SetNextWindowSize(new Vector2(350, 0), ImGuiCond.Always);
                if (ImGui.BeginPopupModal("Register Account", ref openTagPopup, ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGui.Text("Please specify a unique user name");
                    ImGui.InputText("User Name", ref tempTagName, 25);

                    using (ImRaii.Disabled(tempTagName == string.Empty))
                    {

                        if (ImGui.Button("Okay", new Vector2(120, 0)))
                        {
                            tagName = tempTagName;
                            DataSender.CreateUserTag(tagName);
                            ImGui.CloseCurrentPopup();
                            openTagPopup = false;
                        }
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Cancel", new Vector2(120, 0)))
                    {
                        ImGui.CloseCurrentPopup();
                        openTagPopup = false;
                    }

                    ImGui.EndPopup();
                }
            }



            float xpos = ImGui.GetCursorPosX();
            ImGui.SetCursorPosX(xpos + 10);
            ImGui.TextColored(serverStatusColor, serverStatus);
            ImGui.SameLine();

            if (!ClientTCP.Connected)
            {
                if (ImGui.ImageButton(UI.UICommonImage(UI.CommonImageTypes.reconnect).Handle, new Vector2(buttonHeight / 2.5f, buttonHeight / 2.5f)))
                {
                    ClientTCP.AttemptConnect();
                    Plugin.plugin.UpdateStatusAsync().GetAwaiter().GetResult();
                    DataSender.SendLogin();
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
    }

    public void switchUI()
    {
        viewProfile = false;
        viewSystems = false;
        viewEvents = false;
        viewConnections = false;
        viewListings = false;
    }
}
