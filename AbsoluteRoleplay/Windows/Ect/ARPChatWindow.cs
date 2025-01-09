using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Storage.Assets;
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
        Configuration configuration { get; set; }
        public ARPChatWindow(Plugin plugin) : base(
       "CHAT", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            configuration = plugin.Configuration;
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(100, 100),
                MaximumSize = new Vector2(2000, 2000)
            };
            pg = plugin;
        }
        private bool isScrolledToBottom = true; // Flag to track if the user is at the bottom
        private float previousScrollY = 0f; // To track the previous scroll position
        private bool canSend = false;

        public override void Draw()
        {
            if (ImGui.BeginChild("Chat", new Vector2(ImGui.GetWindowSize().X, ImGui.GetWindowSize().Y - 150)))
            {
                // Create a table with 2 columns
                using (var table = ImRaii.Table("ChatTable", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV))
                {
                    if (table != null)
                    {
                        // Set up columns, first column is 200px wide
                        ImGui.TableSetupColumn("Avatar & Author", ImGuiTableColumnFlags.WidthFixed, 200.0f);
                        ImGui.TableSetupColumn("Message", ImGuiTableColumnFlags.None, 0.0f); // The second column will take the remaining space

                        // Table header (optional)
                        ImGui.TableHeadersRow();

                        // Render rows
                        for (int i = 0; i < messages.Count; i++)
                        {
                            // First column: Avatar and Author Name
                            ImGui.TableNextColumn();
                            
                            if(i > 0)
                            {
                                if (messages[i].authorName != messages[i - 1].authorName)
                                {
                                    ImGui.Image(messages[i].avatar.ImGuiHandle, new Vector2(100, 100));
                                    ImGui.TextUnformatted(messages[i].authorName);
                                }
                            }
                            else
                            {
                                ImGui.Image(messages[i].avatar.ImGuiHandle, new Vector2(100, 100));
                                ImGui.TextUnformatted(messages[i].authorName);
                            }

                            // Second column: Message
                            ImGui.TableNextColumn();
                            ImGuiHelpers.SafeTextWrapped(messages[i].message);
                        }
                    }
                    if (ImGui.GetScrollY() == ImGui.GetScrollMaxY())
                    {
                        ImGui.SetScrollHereY(0f); // Scroll to the bottom
                    }
                }
                ImGui.EndChild();
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
            ImGui.SameLine();
            if (ImGui.Button("Send"))
            {
                SendMessage(messageInput);
            }
            if (ImGui.IsKeyPressed(ImGuiKey.Enter))
            {
                if (canSend == true)
                {
                    SendMessage(messageInput);
                }
            }
           
        }
        public void SendMessage(string input)
        {
            DataSender.SendARPChatMessage(input);
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
        public string authorName { get; set; }
        public  IDalamudTextureWrap avatar {  get; set; }
        public  string message { get; set; }
    }
}
