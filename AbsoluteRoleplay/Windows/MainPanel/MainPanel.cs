using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Networking;
using Dalamud.Utility;
using Dalamud.Interface.Textures.TextureWraps;
using AbsoluteRoleplay.Windows.MainPanel.MainPanelTabs.LoggedInTabs;
using AbsoluteRoleplay.Windows.MainPanel.Views.Account;
using AbsoluteRoleplay.Windows.MainPanel.Views;
using AbsoluteRoleplay.Windows.MainPanel.Views.Listings;
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
    public static IDalamudTextureWrap kofiBtnImg, discoBtn, profileSectionImage, eventsSectionImage, systemsSectionImage, connectionsSectionImage,
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
        var kofi = Defines.UICommonImage(Defines.CommonImageTypes.kofiBtn);
        var discod = Defines.UICommonImage(Defines.CommonImageTypes.discordBtn);
        var profileSectionImg = Defines.UICommonImage(Defines.CommonImageTypes.profileSection);
        var eventsImg = Defines.UICommonImage(Defines.CommonImageTypes.eventsSection);
        var systemsImg = Defines.UICommonImage(Defines.CommonImageTypes.systemsSection);
        var connectionsImg = Defines.UICommonImage(Defines.CommonImageTypes.connectionsSection);
        var profileImg = Defines.UICommonImage(Defines.CommonImageTypes.profileCreateProfile);
        var profileBookmarkImg = Defines.UICommonImage(Defines.CommonImageTypes.profileBookmarkProfile);
        var npcImg = Defines.UICommonImage(Defines.CommonImageTypes.profileCreateNPC);
        var npcBookmarkImg = Defines.UICommonImage(Defines.CommonImageTypes.profileBookmarkNPC);
        var reconnectImg = Defines.UICommonImage(Defines.CommonImageTypes.reconnect);
        //listings

        var listingsEventImg = Defines.UICommonImage(Defines.CommonImageTypes.listingsEventBig);
        var listingsCampaignImg = Defines.UICommonImage(Defines.CommonImageTypes.listingsCampaignBig);
        var listingsFCImg = Defines.UICommonImage(Defines.CommonImageTypes.listingsFCBig);
        var listingsGroupImg = Defines.UICommonImage(Defines.CommonImageTypes.listingsGroupBig);
        var listingsVenueImg = Defines.UICommonImage(Defines.CommonImageTypes.listingsVenueBig);
        var listingsPersonalImg = Defines.UICommonImage(Defines.CommonImageTypes.listingsPersonalBig);
        if (kofi != null) { kofiBtnImg = kofi; }
        if (discod != null) { discoBtn = discod; }
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

    }
    public override void Draw()
    {
        paddingX = ImGui.GetWindowSize().X / 12;
        ButtonSize = new Vector2(ImGui.GetIO().FontGlobalScale / 0.005f);
        buttonWidth = ButtonSize.X / 2 - paddingX;
        buttonHeight =ButtonSize.Y / 5f;

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
        if (viewProfile == true)
        {
           ProfilesView.LoadProfilesView(pluginInstance);
        }
        if(viewListings == true)
        {
            ListingsView.LoadListingsView(pluginInstance);
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
        if (pluginInstance.Configuration.showDisc == true)
        {
            var discPos = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(new Vector2(buttonWidth / 14, discPos));
            if (ImGui.ImageButton(discoBtn.ImGuiHandle, new Vector2(buttonWidth * 2.14f, buttonHeight / 1.8f)))
            {
                Util.OpenLink("https://discord.gg/hWprwTUwqj");
            }        
        }

        if (viewProfile == true || viewSystems == true || viewEvents == true || viewConnections == true || viewListings == true)
        {
            if (ImGui.Button("Back"))
            {
                loggedIn = CurrentElement();
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
        ImGui.PushStyleColor(ImGuiCol.Text, statusColor);
        ImGui.TextWrapped(status);
        ImGui.PopStyleColor();


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
