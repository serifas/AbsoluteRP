using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Networking;
using Dalamud.Utility;
using Dalamud.Interface.Textures.TextureWraps;
using AbsoluteRP.Windows.MainPanel.Views;
using Dalamud.Interface.Utility.Raii;
using AbsoluteRP.Helpers;
using Dalamud.Bindings.ImGui;
namespace AbsoluteRP.Windows.MainPanel;

public class MainPanel : Window, IDisposable
{
    //input field strings
    //window state toggles
    public static string tagName = string.Empty;
    private bool openTagPopup = false;
    private string tempTagName = string.Empty;
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
    public static bool LoggedIN = false;
    public static string lodeStoneKey = string.Empty;
    public static Vector2 ButtonSize = new Vector2();
    public static float centeredX = 0f;

    public MainPanel() : base(
        "ABSOLUTE ROLEPLAY",
        ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeCondition = ImGuiCond.Always;
        Remember = Plugin.plugin.Configuration.rememberInformation;
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
        kofiBtnImg = null;
        WindowOperations.SafeDispose(discoBtn);
        discoBtn = null;
        WindowOperations.SafeDispose(patreonBtn);
        patreonBtn = null;
        WindowOperations.SafeDispose(profileSectionImage);
        profileSectionImage = null;
        WindowOperations.SafeDispose(eventsSectionImage);
        eventsSectionImage = null;
        WindowOperations.SafeDispose(systemsSectionImage);
        systemsSectionImage = null;
        WindowOperations.SafeDispose(connectionsSectionImage);
        connectionsSectionImage = null;
        WindowOperations.SafeDispose(profileImage);
        profileImage = null;
        WindowOperations.SafeDispose(npcImage);
        npcImage = null;
        WindowOperations.SafeDispose(profileBookmarkImage);
        profileBookmarkImage = null;
        WindowOperations.SafeDispose(npcBookmarkImage);
        npcBookmarkImage = null;
        WindowOperations.SafeDispose(reconnectImage);
        reconnectImage = null;
        WindowOperations.SafeDispose(listingsEvent);
        listingsEvent = null;
        WindowOperations.SafeDispose(listingsCampaign);
        listingsCampaign = null;
        WindowOperations.SafeDispose(listingsFC);
        listingsFC = null;
        WindowOperations.SafeDispose(listingsGroup);
        listingsGroup = null;
        WindowOperations.SafeDispose(listingsVenue);
        listingsVenue = null;
        WindowOperations.SafeDispose(listingsPersonal);
        listingsPersonal = null;
        WindowOperations.SafeDispose(combatImage);
        combatImage = null;
        WindowOperations.SafeDispose(statSystemImage);
        statSystemImage = null;
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
            if (Plugin.plugin?.Configuration?.account != null &&
              !string.IsNullOrEmpty(Plugin.plugin.Configuration.account.accountKey) &&
              !string.IsNullOrEmpty(Plugin.plugin.Configuration.account.accountName))
            {
                Misc.SetTitle(Plugin.plugin, true, Plugin.plugin.Configuration.account.accountName, new Vector4(0, 1, 0, 0));
                using (ImRaii.Disabled(!Plugin.CtrlPressed()))
                {
                    if (ImGui.Button("Remove Account", new Vector2(buttonWidth * 2.14f, buttonHeight / 1.8f)))
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
                MainUI.LoadMainUI(Plugin.plugin);
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

         
            if (Plugin.plugin.Configuration.showKofi)
            {
                var currentCursorY = ImGui.GetCursorPosY();
                ImGui.SetCursorPos(new Vector2(buttonWidth / 14, currentCursorY));
                if (ImGui.ImageButton(kofiBtnImg.Handle, new Vector2(buttonWidth * 2.14f, buttonHeight / 1.8f)))
                {
                    Util.OpenLink("https://ko-fi.com/absoluteroleplay");
                }
            }
            if (Plugin.plugin.Configuration.showPatreon == true)
            {
                var patreonPos = ImGui.GetCursorPosY();
                ImGui.SetCursorPos(new Vector2(buttonWidth / 14, patreonPos));
                if (ImGui.ImageButton(patreonBtn.Handle, new Vector2(buttonWidth * 2.14f, buttonHeight / 1.8f)))
                {
                    Util.OpenLink("https://patreon.com/AbsoluteRP");
                }
            }
            if (Plugin.plugin.Configuration.showDisc == true)
            {
                var discPos = ImGui.GetCursorPosY();
                ImGui.SetCursorPos(new Vector2(buttonWidth / 14, discPos));
                if (ImGui.ImageButton(discoBtn.Handle, new Vector2(buttonWidth * 2.14f, buttonHeight / 1.8f)))
                {
                    Util.OpenLink("https://discord.gg/absolute-roleplay");
                }
            }
            if (loggedIn == false && viewProfile == false && viewListings == false)
            {
                var serverStatusPosY = ImGui.GetCursorPosY();
                ImGui.SetCursorPos(new Vector2(centeredX, serverStatusPosY));
            }
            float xpos = ImGui.GetCursorPosX();
            ImGui.SetCursorPosX(xpos + 10);
            ImGui.TextColored(serverStatusColor, serverStatus);
            ImGui.SameLine();

            if (!ClientTCP.Connected)
            {
                if (ImGui.ImageButton(reconnectImage.Handle, new Vector2(buttonHeight / 2.5f, buttonHeight / 2.5f)))
                {
                    ClientTCP.AttemptConnect();
                    Plugin.plugin.UpdateStatusAsync().GetAwaiter().GetResult();
                }
            }
            else
            {
                if (ImGui.ImageButton(reconnectImage.Handle, new Vector2(buttonHeight / 2.5f, buttonHeight / 2.5f)))
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
