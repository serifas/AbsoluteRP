using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVVector2 = FFXIVClientStructs.FFXIV.Common.Math.Vector2;
using FFXIVVector4 = FFXIVClientStructs.FFXIV.Common.Math.Vector4;
using ImGuiNET;
using System;
using System.Collections.Generic;
using Dalamud.Interface.Textures.TextureWraps;
using Networking;
using System.Numerics;
using Dalamud.Interface.Utility.Raii;

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

        public ChatWindow(Plugin plugin) : base("CHAT")
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new FFXIVVector2(300, 180),
                MaximumSize = new FFXIVVector2(1000, 700)
            };
        }

        public override void Draw()
        {
            var windowSize = ImGui.GetWindowSize();
            var windowWidth = windowSize.X;
            var windowHeight = windowSize.Y;

            var inputTextHeight = ImGui.GetTextLineHeightWithSpacing() + 8; // Adding some padding
            var contentHeight = windowHeight - inputTextHeight - 50; // Adjust for padding and separator

            // Begin child window for chat content
            using (var chatContent = ImRaii.Child("Chat Content", new Vector2(windowWidth * 0.7f - 20, contentHeight), true))
            {
                if (chatContent)
                {
                    float scrollY = ImGui.GetScrollY();
                    float scrollMaxY = ImGui.GetScrollMaxY();

                    if (scrollY < previousScrollY)
                    {
                        shouldScrollToEnd = false;
                    }

                    DrawChat();

                    scrollY = ImGui.GetScrollY();
                    scrollMaxY = ImGui.GetScrollMaxY();

                    isAtBottom = scrollMaxY - scrollY < 1.0f;

                    if (shouldScrollToEnd)
                    {
                        ImGui.SetScrollY(ImGui.GetScrollMaxY());
                        shouldScrollToEnd = false;
                    }

                    if (isAtBottom)
                    {
                        shouldScrollToEnd = true;
                    }

                    previousScrollY = scrollY;
                }
            }

            ImGui.SameLine();
            var treeWidth = windowWidth - (windowWidth * 0.7f) - ImGui.GetStyle().ItemSpacing.X;

            // Begin child window for the tree node on the right-hand side
            using (var treeChild = ImRaii.Child("Hierarchy", new Vector2(treeWidth, contentHeight), true))
            {
                if (treeChild)
                {
                    if (ImGui.TreeNode("Top level item"))
                    {
                        ImGui.BulletText("Bottom level name");
                        ImGui.TreePop();
                    }
                }
            }


            // Separator line
            ImGui.Separator();

            // Begin child window for input text at the bottom
            using (var chatInputChild = ImRaii.Child("Chat Input", new Vector2(0, inputTextHeight), false))
            {
                if (chatInputChild)
                {
                    ImGui.InputText("Message", ref chatInput, 5000);

                    if (ImGui.IsItemFocused() && ImGui.IsKeyPressed(ImGuiKey.Enter))
                    {
                        DataSender.SendChatMessage(Plugin.ClientState.LocalPlayer.Name.ToString(), Plugin.ClientState.LocalPlayer.HomeWorld.GameData.Name.ToString(), chatInput);
                        chatInput = string.Empty;

                        if (isAtBottom)
                        {
                            shouldScrollToEnd = true;
                        }
                        ImGui.SetKeyboardFocusHere(-1);
                    }
                }
            }
        }

        public static void DrawChat()
        {
            for (int i = 0; i < messages.Count; i++)
            {
                string profileName = messages[i].Item1;
                string msg = messages[i].Item3;
                ImGui.Image(messages[i].Item2.ImGuiHandle, new Vector2(40, 40));

                if (ImGui.BeginPopupContextItem("RightClickContext" + i))
                {
                    if (ImGui.MenuItem("Message"))
                    {
                    }
                    if (ImGui.MenuItem("Invite to Group"))
                    {
                    }
                    ImGui.EndPopup();
                }

                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    ImGui.OpenPopup("RightClickContext" + i);
                }

                ImGui.SameLine();

                Vector4 normalColor = new Vector4(0.22f, 0.69f, 1.0f, 1.0f);
                Vector4 hoverColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                if (ImGui.IsItemHovered())
                {
                    ImGui.TextColored(hoverColor, profileName + ":");
                }
                else
                {
                    ImGui.TextColored(normalColor, profileName + ":");
                }

                ImGui.SameLine();
                ImGui.TextWrapped(msg);
            }
        }

        public void Dispose()
        {
        }
    }
}
