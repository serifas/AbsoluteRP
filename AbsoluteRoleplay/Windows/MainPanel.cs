using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Dalamud.Interface.ImGuiFileDialog;
using Networking;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Internal;
using Dalamud.Utility;
using Dalamud.Configuration;
using Microsoft.VisualBasic;
using AbsoluteRoleplay;
using System.Diagnostics;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Textures.TextureWraps;
using AbsoluteRoleplay.Windows.Profiles;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.GroupPoseModule;
using System.Security.Cryptography;
using static FFXIVClientStructs.FFXIV.Client.Game.SatisfactionSupplyManager;
using OtterGui.OtterGuiInternal.Enums;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkUIColorHolder.Delegates;
using AbsoluteRoleplay.Windows.Profiles.ProfileTabs;
namespace AbsoluteRoleplay.Windows;

public class MainPanel : Window, IDisposable
{
    //input field strings
    public string username = string.Empty;
    public string password = string.Empty;
    public string registerUser = string.Empty;
    public string registerPassword = string.Empty;
    public string registerVerPassword = string.Empty;
    public string email = string.Empty;
    public string restorationEmail = string.Empty;
    //window state toggles
    private static bool viewProfile, viewSystems, viewEvents, viewConnections;
    public static bool login = true;
    public static bool forgot = false;
    public static bool register = false;
    public static bool viewMainWindow = false;
    //width and height values scaling the elements
    public static int width = 0, height = 0;
    //registration agreement
    public bool AgreeTOS = false;
    public bool Agree18 = false;
    public bool Remember = false;
    public bool AutoLogin = true;
    //duh
    //server status label stuff
    public static string serverStatus = "Connection Status...";
    public static Vector4 serverStatusColor = new Vector4(255, 255, 255, 255);
    public static string status = "";
    public static Vector4 statusColor = new Vector4(255, 255, 255, 255);
    //button images
    private IDalamudTextureWrap kofiBtnImg, discoBtn, profileSectionImage, eventsSectionImage, systemsSectionImage, connectionsSectionImage,
                                 //profiles
                                 profileImage, npcImage, profileBookmarkImage, npcBookmarkImage,
                                 //events and venues
                                 listingImage, manageListingsImage, viewListingsImage,
                                 //systems
                                 combatImage, statSystemImage,
                                 reconnectImage;
    public static Plugin pluginInstance;
    public static bool LoggedIN = false;
    public static Vector2 ButtonSize = new Vector2();
    public static float centeredX = 0f;
    public MainPanel(Plugin plugin) : base(
        "ABSOLUTE ROLEPLAY",
        ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.Size = new Vector2(250, 340);
        this.SizeCondition = ImGuiCond.Always;
        pluginInstance = plugin;
        this.username = plugin.Configuration.username;
        this.password = plugin.Configuration.password;
       

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
    }
 
    public void Dispose()
    {
       
    }
    public override void Draw()
    {
        float paddingX = ImGui.GetWindowSize().X / 12;
        float buttonWidth = ImGui.GetWindowSize().X / 2 - paddingX;
        float buttonHeight = ImGui.GetWindowSize().Y / 6f;

        ButtonSize = new Vector2(ImGui.GetWindowSize().X / 2, ImGui.GetWindowSize().Y / 20);
        centeredX = (ImGui.GetWindowSize().X - ButtonSize.X) / 2.0f;
        // can't ref a property, so use a local copy
        if (login == true)
        {

            Misc.DrawCenteredInput(centeredX, ButtonSize, "##username", $"Username", ref this.username, 100, ImGuiInputTextFlags.None);
            Misc.DrawCenteredInput(centeredX, ButtonSize, "##password", $"Password", ref this.password, 100, ImGuiInputTextFlags.Password);
            if (Misc.DrawCenteredButton(centeredX, ButtonSize, "Login"))
            {
                if (pluginInstance.IsOnline() && ClientTCP.IsConnected() == true)
                {
                    SaveLoginPreferences(this.username.ToString(), this.password.ToString());
                    DataSender.Login(this.username, this.password, pluginInstance.playername, pluginInstance.playerworld);
                }
            }
            ImGui.SameLine();
            if(ImGui.Checkbox("Remember Me", ref Remember)){
                SaveLoginPreferences(this.username.ToString(), this.password.ToString());
            }

            if (Misc.DrawCenteredButton(centeredX, ButtonSize,"Forgot"))
            {
                forgot = CurrentElement();
            }
            ImGui.SameLine();
            if (ImGui.Button("Register"))
            {
                register = CurrentElement();
            }
           


        }
        if (forgot == true)
        {
            Misc.DrawCenteredInput(centeredX, ButtonSize, "##RegisteredEmail", $"Email", ref this.restorationEmail, 100, ImGuiInputTextFlags.None);
            if (Misc.DrawCenteredButton(centeredX, ButtonSize, "Submit Request"))
            {
                if (pluginInstance.IsOnline())
                {
                    DataSender.SendRestorationRequest(this.restorationEmail);
                }
            }

            if (Misc.DrawCenteredButton(centeredX, ButtonSize, "Back"))
            {
                login = CurrentElement();
            }

        }
        if (register == true)
        {

            Misc.DrawCenteredInput(centeredX, ButtonSize, "##username", $"Username", ref registerUser, 100, ImGuiInputTextFlags.None);
            Misc.DrawCenteredInput(centeredX, ButtonSize, "##passver", $"Password", ref registerPassword, 100, ImGuiInputTextFlags.Password);
            Misc.DrawCenteredInput(centeredX, ButtonSize, "##regpassver", $"Verify Password", ref this.registerVerPassword, 100, ImGuiInputTextFlags.Password);
            Misc.DrawCenteredInput(centeredX, ButtonSize, "##email", $"Email", ref this.email, 100, ImGuiInputTextFlags.None);
            var Pos18 = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(new Vector2(centeredX, Pos18));
            ImGui.Checkbox("I am atleast 18 years of age", ref Agree18);
            var agreePos = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(new Vector2(centeredX, agreePos));
            ImGui.Checkbox("I agree to the TOS.", ref AgreeTOS);
            if (Misc.DrawCenteredButton(centeredX, ButtonSize, "View ToS & Rules"))
            {
                pluginInstance.OpenTermsWindow();
            }
            if (Agree18 == true && AgreeTOS == true)
            {
                if (Misc.DrawCenteredButton(centeredX, ButtonSize, "Register Account"))
                {
                    if (registerPassword == registerVerPassword)
                    {
                        if (pluginInstance.IsOnline())
                        {
                            SaveLoginPreferences(registerUser, registerPassword);
                            pluginInstance.username = registerUser.ToString();
                            DataSender.Register(registerUser.ToString(), registerPassword, email);
                        }
                    }
                    else
                    {
                        status = "Passwords do not match.";
                        statusColor = new Vector4(255, 0, 0, 255);
                    }

                }
            }
            if (Misc.DrawCenteredButton(centeredX, ButtonSize, "Back"))
            {
                login = CurrentElement();
            }

        }
        if (viewMainWindow == true)
        {
            viewMainWindow = CurrentElement();
            
            #region PROFILES
            if (ImGui.ImageButton(this.profileSectionImage.ImGuiHandle, new Vector2(buttonWidth, buttonHeight)))
            {
                viewProfile = CurrentElement();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Profiles");
                #endregion
            }
            ImGui.SameLine();
            if (ImGui.ImageButton(this.connectionsSectionImage.ImGuiHandle, new Vector2(buttonWidth, buttonHeight)))
            {
                DataSender.RequestConnections(pluginInstance.username.ToString(), pluginInstance.password.ToString());

            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Connections");
            }
            using (OtterGui.Raii.ImRaii.Disabled(true))
            {
                if (ImGui.ImageButton(this.eventsSectionImage.ImGuiHandle, new Vector2(buttonWidth, buttonHeight)))
                {
                    pluginInstance.OpenListingsWindow();
                }
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Listings - WIP ");
            }


            ImGui.SameLine();

            using (OtterGui.Raii.ImRaii.Disabled(true))
            {
                if (ImGui.ImageButton(this.systemsSectionImage.ImGuiHandle, new Vector2(buttonWidth, buttonHeight)))
                {
                    //  viewConnections = true;
                    // viewMainWindow = false;

                }

            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Systems - WIP");
            }


            var optionPos = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(new Vector2(buttonWidth / 10, optionPos));
            if (ImGui.Button("Options", new Vector2(buttonWidth * 2f, buttonHeight / 2.5f)))
            {
                pluginInstance.OpenOptionsWindow();
            }
            var logoutPos = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(new Vector2(buttonWidth / 10, logoutPos));
            if (ImGui.Button("Logout", new Vector2(buttonWidth * 2f, buttonHeight / 2.5f)))
            {
                pluginInstance.newConnection = false;
                pluginInstance.CloseAllWindows();
                pluginInstance.OpenMainPanel();               
                login = CurrentElement();
                status = "Logged Out";
                statusColor = new Vector4(255, 0, 0, 255);
            }
        }
        if (viewProfile == true)
        {
            if (ImGui.ImageButton(this.profileImage.ImGuiHandle, new Vector2(buttonWidth, buttonHeight)))
            {
                if (pluginInstance.IsOnline())
                {
                    StoryTab.storyTitle = string.Empty;
                    ProfileWindow.oocInfo = string.Empty;
                    pluginInstance.OpenAndLoadProfileWindow();
                }
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Manage your profile");
            }
            ImGui.SameLine();
            if (ImGui.ImageButton(this.profileBookmarkImage.ImGuiHandle, new Vector2(buttonWidth, buttonHeight)))
            {
                if (pluginInstance.IsOnline())
                {
                    DataSender.RequestBookmarks();
                    pluginInstance.OpenBookmarksWindow();
                }
               
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("View profile bookmarks");
            }
            using (OtterGui.Raii.ImRaii.Disabled(true))
            {
                if (ImGui.ImageButton(this.npcImage.ImGuiHandle, new Vector2(buttonWidth, buttonHeight)))
                {
                    //  viewConnections = true;
                    // viewMainWindow = false;

                }

            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Manage NPCs - WIP");
            }



            ImGui.SameLine();

            using (OtterGui.Raii.ImRaii.Disabled(true))
            {
                if (ImGui.ImageButton(this.npcBookmarkImage.ImGuiHandle, new Vector2(buttonWidth, buttonHeight)))
                {
                    //  viewConnections = true;
                    // viewMainWindow = false;

                }

            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("View NPC bookmarks - WIP");
            }

        }
        
        if (pluginInstance.Configuration.showKofi)
        {
            if (viewMainWindow == false && viewProfile == false)
            {

                var currentCursorY = ImGui.GetCursorPosY();
                ImGui.SetCursorPos(new Vector2(centeredX, currentCursorY));
                if (ImGui.ImageButton(kofiBtnImg.ImGuiHandle, ButtonSize))
                {
                    Util.OpenLink("https://ko-fi.com/infiniteroleplay");
                }
            }
            else
            {
                var kofiPos = ImGui.GetCursorPosY();
                ImGui.SetCursorPos(new Vector2(buttonWidth / 10, kofiPos));
                if (ImGui.ImageButton(kofiBtnImg.ImGuiHandle, new Vector2(buttonWidth * 1.95f, buttonHeight / 2.5f)))
                {
                    Util.OpenLink("https://ko-fi.com/infiniteroleplay");
                }
            }
        }
        if (pluginInstance.Configuration.showDisc == true)
        {
            if (viewMainWindow == false && viewProfile == false)
            {

                var discPos = ImGui.GetCursorPosY();
                ImGui.SetCursorPos(new Vector2(centeredX, discPos));
                if (ImGui.ImageButton(discoBtn.ImGuiHandle, ButtonSize))
                {
                    Util.OpenLink("https://discord.gg/hWprwTUwqj");
                }
            }
            else
            {
                var discPos = ImGui.GetCursorPosY();
                ImGui.SetCursorPos(new Vector2(buttonWidth / 10, discPos));
                if (ImGui.ImageButton(discoBtn.ImGuiHandle, new Vector2(buttonWidth * 1.95f, buttonHeight / 2.5f)))
                {
                    Util.OpenLink("https://discord.gg/hWprwTUwqj");
                }
            }
        }

        if (viewProfile == true || viewSystems == true || viewEvents == true || viewConnections == true)
        {
            if (ImGui.Button("Back"))
            {
                viewMainWindow = CurrentElement();
            }
        }

        if (viewMainWindow == false && viewProfile == false)
        {
            var serverStatusPosY = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(new Vector2(centeredX, serverStatusPosY));
        }
        ImGui.TextColored(serverStatusColor, serverStatus);
        ImGui.SameLine();
        if (ImGui.ImageButton(reconnectImage.ImGuiHandle, new Vector2(buttonHeight / 3.5f, buttonHeight / 3.5f)))
        {
            ClientTCP.AttemptConnect();
            pluginInstance.UpdateStatus();
        }
        if (viewMainWindow == false && viewProfile == false)
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
        viewConnections = false;
        viewMainWindow = false;
        return true;
    }
    public void SaveLoginPreferences(string username, string password)
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
    }

}
