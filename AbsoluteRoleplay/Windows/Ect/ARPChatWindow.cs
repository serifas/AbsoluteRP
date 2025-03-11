using AbsoluteRoleplay.Defines;
using AbsoluteRoleplay.Windows.Moderator;
using Dalamud.Game.Text;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Storage.Assets;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using ImGuiNET;
using Microsoft.Extensions.Configuration;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRoleplay.Windows.Ect
{
    internal class ARPChatWindow : Window, IDisposable
    {
        public string messageInput = string.Empty;
        public static List<ChatMessage> messages = new List<ChatMessage>();
        public static Plugin pg;
        public static IChatGui chatgui; 
        public IDalamudTextureWrap viewProfileImg = UI.UICommonImage(UI.CommonImageTypes.profileCreateProfile);
        public IDalamudTextureWrap bookmarkProfileImg = UI.UICommonImage(UI.CommonImageTypes.profileBookmarkProfile);

        public ARPChatWindow(Plugin plugin, IChatGui chatGui) : base(
       "CHAT", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(100, 100),
                MaximumSize = new Vector2(2000, 2000)
            };
            pg = plugin;
            chatgui = chatGui;
        }
        private bool isScrolledToBottom = true; // Flag to track if the user is at the bottom
        private float previousScrollY = 0f; // To track the previous scroll position
        private bool canSend = false;
     
        public override void Draw()
        {

            if (ImGui.BeginChild("Chat", new Vector2(ImGui.GetWindowSize().X, ImGui.GetWindowSize().Y - 150)))
            {

                // Create a table with 2 columns
                using (var table = ImRaii.Table("ChatTable", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV))
                {
                    if (table != null)
                    {
                        float size = ImGui.GetIO().FontGlobalScale;
                        // Set up columns, first column is 200px wide
                        ImGui.TableSetupColumn("Avatar & Author", ImGuiTableColumnFlags.WidthFixed, size * 150);
                        ImGui.TableSetupColumn("Controls", ImGuiTableColumnFlags.WidthFixed, size * 80);
                        ImGui.TableSetupColumn("Message", ImGuiTableColumnFlags.None, 0.0f); // The second column will take the remaining space

                        // Table header (optional)
                        ImGui.TableHeadersRow();

                        // Render rows
                        for (int i = 0; i < messages.Count; i++)
                        {
                            // First column: Avatar and Author Name
                            Vector2 aviSize = new Vector2(messages[i].avatar.Width / 2 * size, messages[i].avatar.Height / 2 * size);
                            Vector2 btnSize = new Vector2(viewProfileImg.Width / 4 * size, viewProfileImg.Height / 5 * size);
                            if (messages[i].authorProfileID == 0)
                            {
                                aviSize = aviSize / 3f;
                            }
                            if (i > 0)//if there are any messages
                            {

                                ImGui.TableNextColumn();
                                if (messages[i].authorName != messages[i - 1].authorName) //if the last author does not have the same name as the current author
                                {
                                    ImGui.Image(messages[i].avatar.ImGuiHandle, aviSize);
                                    ImGui.TextUnformatted(messages[i].authorName);
                                    if (DataReceiver.permissions.rank >= (int)Rank.Moderator)
                                    {
                                        // Moderate Button
                                        if (ImGui.Button($"Moderate##{i}"))
                                        {

                                        }

                                    }

                                    ImGui.TableNextColumn();
                                    //set the controls in the next column if the author is not 0
                                    if (messages[i].authorProfileID != 0)
                                    {
                                        ImGui.PushID("##Message" + i);
                                        if (ImGui.ImageButton(viewProfileImg.ImGuiHandle, btnSize))
                                        {
                                            DataSender.RequestTargetProfile(messages[i].authorProfileID);
                                        }
                                        if (ImGui.ImageButton(bookmarkProfileImg.ImGuiHandle, btnSize))
                                        {
                                            DataSender.BookmarkPlayer(messages[i].name, messages[i].world);
                                        }
                                        ImGui.PopID();
                                    }
                                }
                                else
                                {
                                    ImGui.TextUnformatted(messages[i].authorName);                                   
                                    ImGui.TableNextColumn();
                                }
                            }
                            else
                            {
                                ImGui.TableNextColumn();
                                ImGui.Image(messages[i].avatar.ImGuiHandle, aviSize);
                                ImGui.TextUnformatted(messages[i].authorName);
                                if (DataReceiver.permissions.rank >= (int)Rank.Moderator)
                                {
                                    // Moderate Button
                                    if (ImGui.Button($"Moderate##{i}"))
                                    {
                                        ModPanel.capturedAuthor = messages[i].authorUserID;
                                        ModPanel.capturedMessage = messages[i].message;
                                        
                                        pg.OpenModeratorPanel();
                                    }

                                }

                              
                                ImGui.TableNextColumn();
                                if(messages[i].authorProfileID != 0)
                                {
                                    ImGui.PushID("##Message" + i);
                                    if (ImGui.ImageButton(viewProfileImg.ImGuiHandle, btnSize))
                                    {
                                        DataSender.RequestTargetProfile(messages[i].authorProfileID);
                                    }
                                    if (ImGui.ImageButton(bookmarkProfileImg.ImGuiHandle, btnSize))
                                    {
                                        DataSender.BookmarkPlayer(messages[i].name, messages[i].world);
                                    }
                                    ImGui.PopID();

                                }
                            }
                            // Second column: Message
                            ImGui.TableNextColumn();
                            if (messages[i].isAnnouncement)
                            {
                                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0.5f,0, 1));
                                ImGuiHelpers.SafeTextWrapped(messages[i].message);
                                ImGui.PopStyleColor();
                            }
                            else
                            {
                                ImGuiHelpers.SafeTextWrapped(messages[i].message);
                            }                                
                        }
                    }
                    if (ImGui.GetScrollY() == ImGui.GetScrollMaxY())
                    {
                        ImGui.SetScrollHereY(0f); // Scroll to the bottom
                    }
                }
                ImGui.EndChild();
            }
            bool isAnnouncement = false;
            if (messageInput.StartsWith("/announce"))
            {
                isAnnouncement = true;
            }
            // Input field and send button
            ImGui.SetCursorPosY(ImGui.GetWindowSize().Y - 50);
            ImGui.Text("Message:");
            ImGui.SameLine();
            ImGui.InputText("##message",ref messageInput, 5000);
            if (ImGui.IsItemFocused())
            {
                canSend = true;
            }
            else
            {
                canSend = false;
            }
            if (ImGui.IsKeyPressed(ImGuiKey.Enter))
            {
                if (canSend == true)
                {
                    SendMessage(messageInput, isAnnouncement);
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Send"))
            {
                SendMessage(messageInput, isAnnouncement);
            }
           
        }
          
           
        
        public void SendMessage(string input, bool announcement)
        {
            DataSender.SendARPChatMessage(input, announcement);
            messageInput = string.Empty;
        }








        public void Dispose()
        {
            for (int i = 0; i < messages.Count; i++)
            {
                messages[i].avatar?.Dispose();  
            }
        }
    }
    public  class ChatMessage
    {
        public int authorProfileID {  get; set; }
        public string name { get; set; }
        public string world { get; set; }
        public string authorName { get; set; }
        public  IDalamudTextureWrap avatar {  get; set; }
        public  string message { get; set; }
        public bool isAnnouncement { get; set; }
        public int authorUserID { get; internal set; }
    }
}
