using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using System;
using Dalamud.Interface.GameFonts;
using AbsoluteRoleplay.Helpers;
using Dalamud.Interface.Utility.Raii;
using Networking;
using System.Collections.Generic;
using Dalamud.Interface.Textures.TextureWraps;
using ImGuiScene;
namespace AbsoluteRoleplay.Windows.Chat
{
    public class ChatWindow : Window, IDisposable
    {
        public static Plugin pg;
        public static string chatInput = "";
        public static bool shouldScrollToEnd;
        private float previousScrollY = 0.0f;
        private bool isAtBottom = true;
        public static List<Tuple<string, IDalamudTextureWrap, string>> messages = new List<Tuple<string, IDalamudTextureWrap, string>>();
        public ChatWindow(Plugin plugin) : base(
       "CHAT")
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(300, 180),
                MaximumSize = new Vector2(500, 800)
            };
        }


        public override void Draw()
        {

            // Calculate the height for the chat content and input areas
            var windowHeight = ImGui.GetWindowHeight();
            var inputTextHeight = ImGui.GetTextLineHeightWithSpacing() + 8; // Adding some padding
            var contentHeight = windowHeight - inputTextHeight - 50; // Adjust for padding and separator

            // Begin child window for chat content
            ImGui.BeginChild("Chat Content", new Vector2(0, contentHeight), true);

            // Get the current scroll position and the maximum scroll position
            float scrollY = ImGui.GetScrollY();
            float scrollMaxY = ImGui.GetScrollMaxY();

            // Check if the user scrolled up
            if (scrollY < previousScrollY)
            {
                shouldScrollToEnd = false;
            }

            // Draw the chat content
            DrawChat();

            // Recalculate scroll positions after adding content
            scrollY = ImGui.GetScrollY();
            scrollMaxY = ImGui.GetScrollMaxY();

            // Check if the user is at the bottom of the scroll
            isAtBottom = scrollMaxY - scrollY < 1.0f;

            // Scroll to the end if shouldScrollToEnd is true
            if (shouldScrollToEnd)
            {
                ImGui.SetScrollY(ImGui.GetScrollMaxY());
                shouldScrollToEnd = false; // Reset the flag after scrolling
            }

            // Set shouldScrollToEnd to true if the user was at the bottom
            if (isAtBottom)
            {
                shouldScrollToEnd = true;
            }

            // Update the previous scroll position
            previousScrollY = scrollY;

            ImGui.EndChild(); // End child window for chat content

            // Separator line
            ImGui.Separator();

            // Begin child window for input text at the bottom
            ImGui.BeginChild("Chat Input", new Vector2(0, inputTextHeight), false);

            ImGui.InputText("Message", ref chatInput, 5000);

            // Check if the input text field is focused and the enter key is pressed
            if (ImGui.IsItemFocused() && ImGui.IsKeyPressed(ImGuiKey.Enter))
            {
                // Handle enter key pressed while input text is focused
                DataSender.SendChatMessage(Plugin.ClientState.LocalPlayer.Name.ToString(), Plugin.ClientState.LocalPlayer.HomeWorld.GameData.Name.ToString(), chatInput);
                chatInput = string.Empty; // Clear the input after sending

                // Set the flag to scroll to the end only if the user is at the bottom
                if (isAtBottom)
                {
                    shouldScrollToEnd = true;
                }
                ImGui.SetKeyboardFocusHere(-1);
            }

            ImGui.EndChild(); // End child window for chat input

        }

        public static void DrawChat()
        {
            for (int i = 0; i < messages.Count; i++)
            {
                string profileName = messages[i].Item1;
                string msg = messages[i].Item3;
                ImGui.Image(messages[i].Item2.ImGuiHandle, new System.Numerics.Vector2(40, 40));
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.22f, 0.69f, 1.0f, 1.0f), profileName + ":");
                ImGui.SameLine();
                ImGui.TextWrapped(msg);
            }
        }

        public void Dispose()
        {
        }
    }

}
