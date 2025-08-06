using AbsoluteRoleplay.Defines;
using AbsoluteRoleplay.Helpers;
using AbsoluteRoleplay.Windows.Moderator;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Networking;
using System.Numerics;


namespace AbsoluteRoleplay.Windows.Ect
{
    internal class ARPChatWindow : Window, IDisposable
    {
        public string messageInput = string.Empty;
        public static List<ChatMessage> messages = new List<ChatMessage>();
        public static IChatGui chatgui; 
        public IDalamudTextureWrap viewProfileImg = UI.UICommonImage(UI.CommonImageTypes.profileCreateProfile);
        public IDalamudTextureWrap bookmarkProfileImg = UI.UICommonImage(UI.CommonImageTypes.profileBookmarkProfile);
        private static readonly List<string> AllChannels = new()
        {
            "Say",
            "Shout",
            "Yell",
            "Tell",
            "Party",
            "Alliance",
            "FreeCompany",
            "Linkshell1",
            "Linkshell2",
            "Linkshell3",
            "Linkshell4",
            "Linkshell5",
            "Linkshell6",
            "Linkshell7",
            "Linkshell8",
            "CrossWorldLinkshell1",
            "CrossWorldLinkshell2",
            "CrossWorldLinkshell3",
            "CrossWorldLinkshell4",
            "CrossWorldLinkshell5",
            "PvPTeam",
            "NoviceNetwork",
        }; 
        private int selectedTab = 0;

        public ARPChatWindow(IChatGui chatGui) : base(
       "CHAT", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(100, 100),
                MaximumSize = new Vector2(2000, 2000)
            };
            chatgui = chatGui;
        }
        private bool canSend = false;
       
        public override void Draw()
        {
            try
            {
                List<ChatChannelTabs> channelTabs = Plugin.plugin.Configuration.chatChannelTabs;
                using (var child = ImRaii.Child("Chat", new Vector2(ImGui.GetWindowSize().X, ImGui.GetWindowSize().Y - 150)))
                {
                    if (child)
                    {
                        if (ImGui.BeginTabBar("ChannelTabs", ImGuiTabBarFlags.NoCloseWithMiddleMouseButton))
                        {

                            for (int i = 0; i < channelTabs.Count; i++)
                            {
                                if (ImGui.TabItemButton(channelTabs[i].name))
                                {
                                    for(int t = 0; t < channelTabs[i].includedChannels.Count; t++)
                                    {
                                        if (channelTabs[i].includedChannels[t] == 200)
                                        {

                                        }
                                    }
                                }
                            }
                            ImGui.EndTabBar();
                        }
                        if (selectedTab == 0) // ARP Channel
                        {
                            // Create a table with 2 columns
                            using (var table = ImRaii.Table("ChatTable", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV))
                            {
                                if (table != null)
                                {
                                    float size = ImGui.GetIO().FontGlobalScale;
                                    // Set up columns, first column is 200px wide
                                    ImGui.TableSetupColumn("Author", ImGuiTableColumnFlags.WidthFixed, size * 150);
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
                                                if( messages[i].avatar != null && messages[i].avatar.Handle != IntPtr.Zero)
                                                {                                                    // Display avatar and author name
                                                    ImGui.Image(messages[i].avatar.Handle, aviSize);
                                                }
                                                ImGui.TextUnformatted(messages[i].authorName);
                                                if (DataReceiver.permissions.rank >= (int)Rank.Moderator && messages[i].authorUserID != 0)
                                                {
                                                    // Moderate Button
                                                    if (ImGui.Button($"Moderate##{i}"))
                                                    {
                                                        ModPanel.capturedAuthor = messages[i].authorUserID;
                                                        ModPanel.capturedMessage = messages[i].message;

                                                        Plugin.plugin.OpenModeratorPanel();
                                                    }
                                                }

                                                ImGui.TableNextColumn();
                                                //set the controls in the next column if the author is not 0
                                                if (messages[i].authorProfileID != 0)
                                                {
                                                    ImGui.PushID("##Message" + i);

                                                    if (viewProfileImg != null && viewProfileImg.Handle != IntPtr.Zero)
                                                    {
                                                        if (ImGui.ImageButton(viewProfileImg.Handle, btnSize))
                                                        {
                                                            DataSender.RequestTargetProfile(messages[i].authorProfileID);
                                                        }
                                                    }
                                                    if(bookmarkProfileImg != null && viewProfileImg.Handle != IntPtr.Zero)
                                                    {

                                                    if (ImGui.ImageButton(bookmarkProfileImg.Handle, btnSize))
                                                    {
                                                        DataSender.BookmarkPlayer(messages[i].name, messages[i].world);
                                                        }
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
                                            if (messages[i].avatar != null && messages[i].avatar.Handle != IntPtr.Zero)
                                            {
                                                ImGui.Image(messages[i].avatar.Handle, aviSize);
                                            }   
                                            ImGui.TextUnformatted(messages[i].authorName);
                                            if (DataReceiver.permissions.rank >= (int)Rank.Moderator && messages[i].authorUserID != 0)
                                            {
                                                // Moderate Button
                                                if (ImGui.Button($"Moderate##{i}"))
                                                {
                                                    ModPanel.capturedAuthor = messages[i].authorUserID;
                                                    ModPanel.capturedMessage = messages[i].message;

                                                    Plugin.plugin.OpenModeratorPanel();
                                                }

                                            }


                                            ImGui.TableNextColumn();
                                            if (messages[i].authorProfileID != 0)
                                            {
                                                ImGui.PushID("##Message" + i);
                                                if(viewProfileImg != null && viewProfileImg.Handle != IntPtr.Zero)
                                                {
                                                    if (ImGui.ImageButton(viewProfileImg.Handle, btnSize))
                                                    {
                                                        DataSender.RequestTargetProfile(messages[i].authorProfileID);
                                                    }

                                                }
                                                if(bookmarkProfileImg != null && bookmarkProfileImg.Handle != IntPtr.Zero)
                                                {
                                                    if (ImGui.ImageButton(bookmarkProfileImg.Handle, btnSize))
                                                    {
                                                        DataSender.BookmarkPlayer(messages[i].name, messages[i].world);
                                                    }
                                                }
                                                ImGui.PopID();

                                            }
                                        }
                                        // Second column: Message
                                        ImGui.TableNextColumn();
                                        if (messages[i].isAnnouncement)
                                        {
                                            using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1, 0.5f, 0, 1)))
                                            {
                                                ImGui.TextWrapped(messages[i].message);
                                            }
                                        }
                                        else
                                        {
                                            ImGui.TextWrapped(messages[i].message);
                                        }
                                    }
                                }
                                if (ImGui.GetScrollY() == ImGui.GetScrollMaxY())
                                {
                                    ImGui.SetScrollHereY(0f); // Scroll to the bottom
                                }
                            }
                        }
                    }
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
                ImGui.InputText("##message", ref messageInput, 5000);
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
            catch (Exception ex)
            {
                Plugin.logger.Error("ARPChatWindow Draw Error: " + ex.Message);
                // Optionally, you can display an error message in the chat window
                chatgui.PrintError($"Error in ARPChatWindow: {ex.Message}");
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
                WindowOperations.SafeDispose(messages[i].avatar);
                messages[i].avatar = null;
            }
        }

        internal void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            // Example: Detect if message is sent on Linkshell1
            if (type == XivChatType.Ls1)
            {

            }
            // Example: Detect if message is sent on Party
            if (type == XivChatType.Party)
            {
              
            }
            // You can check for any channel using XivChatType enum
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
