using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Networking;
using Dalamud.Utility;
using Dalamud.Interface.Textures.TextureWraps;
using AbsoluteRoleplay.Windows.MainPanel.Views.Account;
using AbsoluteRoleplay.Windows.MainPanel.Views;
using Dalamud.Interface.Utility.Raii;
using AbsoluteRoleplay.Helpers;
namespace AbsoluteRoleplay.Windows.MainPanel;

public class MainPanel : Window, IDisposable
{
    //input field strings
    //window state toggles
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
    public static float buttonWidth = 0;
    public static float buttonHeight = 0;
    //duh
    //server status label stuff
    public static string serverStatus = "Connection Status...";
    public static Vector4 serverStatusColor = new Vector4(255, 255, 255, 255);
    public static string status = "";
    public static Vector4 statusColor = new Vector4(255, 255, 255, 255);
    //button images
    public static IDalamudTextureWrap kofiBtnImg, discoBtn, patreonBtn, profileSectionImage, eventsSectionImage, systemsSectionImage, connectionsSectionImage,
                                 //profiles
                                 profileImage, npcImage, profileBookmarkImage, npcBookmarkImage,
                                 //events and venues
                                 listingsEvent, listingsCampaign, listingsFC, listingsGroup, listingsVenue, listingsPersonal,
                                 //systems
                                 combatImage, statSystemImage,
                                 reconnectImage;
    public static Plugin pluginInstance;
    public static bool LoggedIN = false;
    public static Vector2 ButtonSize = new Vector2();
    public static float centeredX = 0f;
    public MainPanel(Plugin plugin) : base(
        "ABSOLUTE ROLEPLAY",
        ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeCondition = ImGuiCond.Always;
        pluginInstance = plugin;
        Login.username = plugin.Configuration.username;
        Login.password = plugin.Configuration.password;


        Remember = plugin.Configuration.rememberInformation;
    }
    public override void OnOpen()
    {
        var kofi = UI.UICommonImage(UI.CommonImageTypes.kofiBtn);
        var discod = UI.UICommonImage(UI.CommonImageTypes.discordBtn);
        var patreon = UI.UICommonImage(UI.CommonImageTypes.patreonBtn);
        var profileSectionImg = UI.UICommonImage(UI.CommonImageTypes.profileSection);
        var eventsImg = UI.UICommonImage(UI.CommonImageTypes.eventsSection);
        var systemsImg = UI.UICommonImage(UI.CommonImageTypes.systemsSection);
        var connectionsImg = UI.UICommonImage(UI.CommonImageTypes.connectionsSection);
        var profileImg = UI.UICommonImage(UI.CommonImageTypes.profileCreateProfile);
        var profileBookmarkImg = UI.UICommonImage(UI.CommonImageTypes.profileBookmarkProfile);
        var npcImg = UI.UICommonImage(UI.CommonImageTypes.profileCreateNPC);
        var npcBookmarkImg = UI.UICommonImage(UI.CommonImageTypes.profileBookmarkNPC);
        var reconnectImg = UI.UICommonImage(UI.CommonImageTypes.reconnect);
        //listings

        var listingsEventImg = UI.UICommonImage(UI.CommonImageTypes.listingsEventBig);
        var listingsCampaignImg = UI.UICommonImage(UI.CommonImageTypes.listingsCampaignBig);
        var listingsFCImg = UI.UICommonImage(UI.CommonImageTypes.listingsFCBig);
        var listingsGroupImg = UI.UICommonImage(UI.CommonImageTypes.listingsGroupBig);
        var listingsVenueImg = UI.UICommonImage(UI.CommonImageTypes.listingsVenueBig);
        var listingsPersonalImg = UI.UICommonImage(UI.CommonImageTypes.listingsPersonalBig);
        if (kofi != null) { kofiBtnImg = kofi; }
        if (discod != null) { discoBtn = discod; }
        if (patreon != null) { patreonBtn = patreon; }
        if (profileSectionImg != null) { profileSectionImage = profileSectionImg; }
        if (eventsImg != null) { eventsSectionImage = eventsImg; }
        if (systemsImg != null) { systemsSectionImage = systemsImg; }
        if (connectionsImg != null) { connectionsSectionImage = connectionsImg; }
        if (profileImg != null) { profileImage = profileImg; }
        if (profileBookmarkImg != null) { profileBookmarkImage = profileBookmarkImg; }
        if (npcImg != null) { npcImage = npcImg; }
        if (npcBookmarkImg != null) { npcBookmarkImage = npcBookmarkImg; }
        if (reconnectImg != null) { reconnectImage = reconnectImg; }
        //listings
        if (listingsEventImg != null) { listingsEvent = listingsEventImg; }
        if (listingsCampaignImg != null) { listingsCampaign = listingsCampaignImg; }
        if (listingsFCImg != null) { listingsFC = listingsFCImg; }
        if (listingsGroupImg != null) { listingsGroup = listingsGroupImg; }
        if (listingsVenueImg != null) { listingsVenue = listingsVenueImg; }
        if (listingsPersonalImg != null) { listingsPersonal = listingsPersonalImg; }
    }

    public void Dispose()
    {
        WindowOperations.SafeDispose(kofiBtnImg);
        WindowOperations.SafeDispose(discoBtn);
        WindowOperations.SafeDispose(patreonBtn);
        WindowOperations.SafeDispose(profileSectionImage);
        WindowOperations.SafeDispose(eventsSectionImage);
        WindowOperations.SafeDispose(systemsSectionImage);
        WindowOperations.SafeDispose(connectionsSectionImage);
        WindowOperations.SafeDispose(profileImage);
        WindowOperations.SafeDispose(npcImage);
        WindowOperations.SafeDispose(profileBookmarkImage);
        WindowOperations.SafeDispose(npcBookmarkImage);
        WindowOperations.SafeDispose(reconnectImage);
        WindowOperations.SafeDispose(listingsEvent);
        WindowOperations.SafeDispose(listingsCampaign);
        WindowOperations.SafeDispose(listingsFC);
        WindowOperations.SafeDispose(listingsGroup);
        WindowOperations.SafeDispose(listingsVenue);
        WindowOperations.SafeDispose(listingsPersonal);
        WindowOperations.SafeDispose(combatImage);  
        WindowOperations.SafeDispose(statSystemImage);
    }
    public override void Draw()
    {
        try
        {
            paddingX = ImGui.GetWindowSize().X / 12;
            ButtonSize = new Vector2(ImGui.GetIO().FontGlobalScale / 0.005f);
            buttonWidth = ButtonSize.X / 2 - paddingX;
            buttonHeight = ButtonSize.Y / 5f;

            centeredX = (ImGui.GetWindowSize().X - ButtonSize.X) / 2.0f;
            // can't ref a property, so use a local copy
            if (login == true)
            {
                Login.LoadLogin(pluginInstance);
            }
            if (forgot == true)
            {
                Forgot.LoadForgot(pluginInstance);
            }
            if (register == true)
            {
                Register.LoadRegistration(pluginInstance);
            }
            if (loggedIn == true)
            {
                LoggedIn.LoadLoggedIn(pluginInstance);
            }

            if (pluginInstance.Configuration.showKofi)
            {
                var currentCursorY = ImGui.GetCursorPosY();
                ImGui.SetCursorPos(new Vector2(buttonWidth / 14, currentCursorY));
                if (ImGui.ImageButton(kofiBtnImg.ImGuiHandle, new Vector2(buttonWidth * 2.14f, buttonHeight / 1.8f)))
                {
                    Util.OpenLink("https://ko-fi.com/absoluteroleplay");
                }
            }
            if (pluginInstance.Configuration.showPatreon == true)
            {
                var patreonPos = ImGui.GetCursorPosY();
                ImGui.SetCursorPos(new Vector2(buttonWidth / 14, patreonPos));
                if (ImGui.ImageButton(patreonBtn.ImGuiHandle, new Vector2(buttonWidth * 2.14f, buttonHeight / 1.8f)))
                {
                    Util.OpenLink("https://patreon.com/AbsoluteRoleplay");
                }
            }
            if (pluginInstance.Configuration.showDisc == true)
            {
                var discPos = ImGui.GetCursorPosY();
                ImGui.SetCursorPos(new Vector2(buttonWidth / 14, discPos));
                if (ImGui.ImageButton(discoBtn.ImGuiHandle, new Vector2(buttonWidth * 2.14f, buttonHeight / 1.8f)))
                {
                    Util.OpenLink("https://discord.gg/hWprwTUwqj");
                }
            }


            if (loggedIn == false && viewProfile == false && viewListings == false)
            {
                var serverStatusPosY = ImGui.GetCursorPosY();
                ImGui.SetCursorPos(new Vector2(centeredX, serverStatusPosY));
            }
            ImGui.TextColored(serverStatusColor, serverStatus);
            ImGui.SameLine();
            if (ImGui.ImageButton(reconnectImage.ImGuiHandle, new Vector2(buttonHeight / 2.5f, buttonHeight / 2.5f)))
            {
                ClientTCP.AttemptConnect();
                pluginInstance.UpdateStatus();
            }
            if (loggedIn == false && viewProfile == false && viewListings == false)
            {
                var statusPosY = ImGui.GetCursorPosY();
                ImGui.SetCursorPos(new Vector2(centeredX, statusPosY));
            }
            using (ImRaii.PushColor(ImGuiCol.Text, statusColor))
            {
                ImGui.TextWrapped(status);
            }
            ImGui.SameLine();
            if (loggedIn == true)
            {
                if (ImGui.Button("Logout", new Vector2(buttonWidth, buttonHeight / 2f)))
                {
                    pluginInstance.newConnection = false;
                    pluginInstance.CloseAllWindows();
                    pluginInstance.OpenMainPanel();
                    login = CurrentElement();
                    status = "Logged Out";
                    statusColor = new Vector4(255, 0, 0, 255);
                }
            }

        } catch (Exception e)
        {
            Plugin.plugin.logger.Error("MainPanel Draw Error: " + e.Message);
            Plugin.plugin.logger.Error(e.StackTrace);

        }

    }
    public static bool CurrentElement()
    {
        login = false;
        forgot = false;
        register = false;
        viewProfile = false;
        viewSystems = false;
        viewEvents = false;
        viewListings = false;
        viewConnections = false;
        loggedIn = false;
        return true;
    }
    public static void SaveLoginPreferences(string username, string password)
    {
        pluginInstance.Configuration.rememberInformation = Remember;
        if (pluginInstance.Configuration.rememberInformation == true)
        {
            pluginInstance.Configuration.username = username;
            pluginInstance.Configuration.password = password;
        }
        else
        {
            pluginInstance.Configuration.username = string.Empty;
            pluginInstance.Configuration.password = string.Empty;
        }
        pluginInstance.username = username;
        pluginInstance.password = password;
        pluginInstance.Configuration.Save();
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
