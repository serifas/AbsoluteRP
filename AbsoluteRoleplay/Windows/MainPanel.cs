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
    private bool viewProfile, viewSystems, viewEvents, viewConnections;
    public static bool login = true;
    public static bool forgot = false;
    public static bool register = false;
    public static bool viewMainWindow = false;
    //registration agreement
    public bool AgreeTOS = false;
    public bool Agree18 = false;
    public bool Remember = false;
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
                                 venueImage, eventImage, venueBookmarkImage, eventBookmarkImage,
                                 //systems
                                 combatImage, statSystemImage,
                                 reconnectImage;
    public static Plugin pluginInstance;
    public static bool LoggedIN = false;

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
        // can't ref a property, so use a local copy
        if (login == true)
        {
            ImGui.InputTextWithHint("##username", $"Username", ref this.username, 100);
            ImGui.InputTextWithHint("##password", $"Password", ref this.password, 100, ImGuiInputTextFlags.Password);

            if (ImGui.Button("Login"))
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
            if (ImGui.Button("Forgot"))
            {
                forgot = CurrentElement();
            }
            ImGui.SameLine();
            if (ImGui.Button("Register"))
            {
                register = CurrentElement();
            }
            if (pluginInstance.Configuration.showKofi == true)
            {
                if (ImGui.ImageButton(kofiBtnImg.ImGuiHandle, new Vector2(172, 27)))
                {
                    Util.OpenLink("https://ko-fi.com/infiniteroleplay");
                }
            }
            if (pluginInstance.Configuration.showDisc == true)
            {
                if (ImGui.ImageButton(discoBtn.ImGuiHandle, new Vector2(172, 27)))
                {
                    Util.OpenLink("https://discord.gg/absolute-roleplay");
                }
            }


        }
        if (forgot == true)
        {
            ImGui.InputTextWithHint("##RegisteredEmail", $"Email", ref this.restorationEmail, 100);
            if (ImGui.Button("Submit Request"))
            {
                if (pluginInstance.IsOnline())
                {
                    DataSender.SendRestorationRequest(this.restorationEmail);
                }
            }

            if (ImGui.Button("Back"))
            {
                login = CurrentElement();
            }

        }
        if (register == true)
        {

            ImGui.InputTextWithHint("##username", $"Username", ref registerUser, 100);
            ImGui.InputTextWithHint("##passver", $"Password", ref registerPassword, 100, ImGuiInputTextFlags.Password);
            ImGui.InputTextWithHint("##regpassver", $"Verify Password", ref this.registerVerPassword, 100, ImGuiInputTextFlags.Password);
            ImGui.InputTextWithHint("##email", $"Email", ref this.email, 100);
            ImGui.Checkbox("I am atleast 18 years of age", ref Agree18);
            ImGui.Checkbox("I agree to the TOS.", ref AgreeTOS);
            if (ImGui.Button("View ToS & Rules"))
            {
                pluginInstance.OpenTermsWindow();
            }
            if (Agree18 == true && AgreeTOS == true)
            {
                if (ImGui.Button("Register Account"))
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
            if (ImGui.Button("Back"))
            {
                login = CurrentElement();
            }

        }
        if (viewMainWindow == true)
        {
            viewMainWindow = CurrentElement();
            
            #region PROFILES
            if (ImGui.ImageButton(this.profileSectionImage.ImGuiHandle, new Vector2(100, 50)))
            {
                viewProfile = CurrentElement();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Profiles");
                #endregion
            }
            ImGui.SameLine();
            if (ImGui.ImageButton(this.connectionsSectionImage.ImGuiHandle, new Vector2(100, 50)))
            {
                DataSender.RequestConnections(pluginInstance.username.ToString(), pluginInstance.playername, pluginInstance.playerworld);

            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Connections");
            }
            using (OtterGui.Raii.ImRaii.Disabled(true))
            {
                if (ImGui.ImageButton(this.eventsSectionImage.ImGuiHandle, new Vector2(100, 50)))
                {
                    //  viewConnections = true;
                    // viewMainWindow = false;

                }

            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Events - WIP");
            }



            ImGui.SameLine();

            using (OtterGui.Raii.ImRaii.Disabled(true))
            {
                if (ImGui.ImageButton(this.systemsSectionImage.ImGuiHandle, new Vector2(100, 50)))
                {
                    //  viewConnections = true;
                    // viewMainWindow = false;

                }

            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("Systems - WIP");
            }


            if (ImGui.Button("Options", new Vector2(225, 25)))
            {
                pluginInstance.OpenOptionsWindow();
            }
            if (ImGui.Button("Logout", new Vector2(225, 25)))
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
            if (ImGui.ImageButton(this.profileImage.ImGuiHandle, new Vector2(100, 50)))
            {
                if (pluginInstance.IsOnline())
                {
                    ProfileWindow.TabOpen[TabValue.Bio] = true;
                    ProfileWindow.TabOpen[TabValue.Hooks] = true;
                    ProfileWindow.TabOpen[TabValue.Story] = true;
                    ProfileWindow.TabOpen[TabValue.OOC] = true;
                    ProfileWindow.TabOpen[TabValue.Gallery] = true;
                    pluginInstance.OpenProfileWindow();
                    //FETCH USER AND PASS ASEWLL
                    DataSender.FetchProfile(pluginInstance.playername, pluginInstance.playerworld);
                    ProfileWindow.ClearUI();
                }
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Manage your profile");
            }
            ImGui.SameLine();
            if (ImGui.ImageButton(this.profileBookmarkImage.ImGuiHandle, new Vector2(100, 50)))
            {
                if (pluginInstance.IsOnline())
                {
                    DataSender.RequestBookmarks(pluginInstance.username);
                }
               
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("View profile bookmarks");
            }
            using (OtterGui.Raii.ImRaii.Disabled(true))
            {
                if (ImGui.ImageButton(this.npcImage.ImGuiHandle, new Vector2(100, 50)))
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
                if (ImGui.ImageButton(this.npcBookmarkImage.ImGuiHandle, new Vector2(100, 50)))
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


        if (viewProfile == true || viewSystems == true || viewEvents == true || viewConnections == true)
        {
            if (ImGui.Button("Back"))
            {
                viewMainWindow = CurrentElement();
            }
        }
        ImGui.TextColored(serverStatusColor, serverStatus);
        ImGui.SameLine();
        if (ImGui.ImageButton(reconnectImage.ImGuiHandle, new Vector2(18, 18)))
        {
            ClientTCP.AttemptConnect();
            pluginInstance.UpdateStatus();
        }
        if (ImGui.Checkbox("Auto Me Login", ref Remember))
        {
            pluginInstance.Configuration.autologin = Remember;
            pluginInstance.Configuration.Save();
        }
        ImGui.TextColored(statusColor, status);

        
    }
    public bool CurrentElement()
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
    public static void AttemptLogin()
    {
        if(ClientTCP.IsConnected() && pluginInstance.Configuration.username != string.Empty && pluginInstance.Configuration.password != string.Empty)
        {
            DataSender.Login(pluginInstance.Configuration.username, pluginInstance.Configuration.password, pluginInstance.playername, pluginInstance.playerworld);
        }
    }
    public void switchUI()
    {
        viewProfile = false;
        viewSystems = false;
        viewEvents = false;
        viewConnections = false;
    }

}
