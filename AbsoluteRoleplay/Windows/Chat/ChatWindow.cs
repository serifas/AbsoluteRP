/*using Dalamud.Interface.Windowing;
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
using OtterGui.Text.EndObjects;
using Dalamud.Interface.Utility;
using Dalamud.Interface;
using OtterGui;
using OtterGui.Log;

namespace AbsoluteRoleplay.Windows.Chat
{
    public class ChatWindow : Window, IDisposable
    {
        public static Plugin pg;
        public static string chatInput = "";
        public static bool shouldScrollToEnd;
        private float previousScrollY = 0.0f;
        private string groupName = string.Empty;
        private bool isAtBottom = true;
        public static List<Tuple<string, IDalamudTextureWrap, string>> messages = new List<Tuple<string, IDalamudTextureWrap, string>>();
        private List<string> groups = new List<string>();

        public ChatWindow(Plugin plugin) : base("CHAT")
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new FFXIVVector2(300, 180),
                MaximumSize = new FFXIVVector2(1000, 500)
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
                    for(int i = 0; i < groups.Count; i++)
                    {
                        string name = groups[i];

                        // Main content of the Hierarchy child
                        if (ImGui.TreeNode(name))
                        {
                            ImGui.BulletText("Bottom level name");
                            ImGui.TreePop();
                        }
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
                    ImGui.TextUnformatted("Message");
                    ImGui.SameLine();
                    ImGui.InputText("##Message", ref chatInput, 5000);

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
                    ImGui.SameLine();
                    ImGui.SetCursorPosX(windowSize.X - 50);
                    // Add some vertical padding if needed
                    DrawAddGroupBtn(25);
                }
            }
        }

        private void DrawAddGroupBtn(float width)
        {
            const string newNamePopupAdd = "##NewNameAdd";

            if (ImGui.Button(FontAwesomeIcon.Plus.ToIconString(), Vector2.UnitX * width))
                ImGui.OpenPopup(newNamePopupAdd);
            using var font = ImRaii.PushFont(UiBuilder.DefaultFont);
            ImGuiUtil.HoverTooltip("Add New");

            if (!OpenNameField(newNamePopupAdd, out var newName))
                return;

            if (OnAdd(newName))
            {
                groups.Add(newName);
                foreach (string groupName in groups)
                {
                    pg.logger.Error(groupName);   
                }
            }
        }
        protected virtual bool OnAdd(string name) => throw new NotImplementedException();

        private bool OpenNameField(string popupName, out string newName)
        {
            newName = string.Empty;
            if (ImGuiUtil.OpenNameField(popupName, ref groupName))
            {
                newName = groupName;
                groupName = string.Empty;
                return true;
            }

            return false;
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

*/
