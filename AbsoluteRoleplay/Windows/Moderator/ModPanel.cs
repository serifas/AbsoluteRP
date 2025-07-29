using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using System;
using Networking;
using Dalamud.Interface.Colors;
using OtterGui;
using AbsoluteRoleplay.Defines;
using OtterGui.Extensions;

namespace AbsoluteRoleplay.Windows.Moderator
{
    public class ModPanel : Window, IDisposable
    {

        public static Plugin pg;
        public static string moderatorNotes = string.Empty;
        public static string moderatorMessage = string.Empty;
        public static string capturedMessage = string.Empty;
        public static ModeratorAction currentAction = ModeratorAction.None;
        public static bool addNotes;
        public static bool confirmed = false;
        public static int capturedAuthor = 0;
        private bool submitted;
        public static string status = "Report Status";
        public static Vector4 statusColor = new Vector4(0, 0, 0, 0);

        public ModPanel(Plugin plugin) : base(
       "MOD PANEL")
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(100, 100),
                MaximumSize = new Vector2(1200, 950)
            };
            pg = plugin;
            pg.logger.Error(capturedAuthor.ToString());
          
        }
        public override void Draw()
        {
            try
            {
                if (pg.IsOnline())
                {
                    Misc.SetTitle(pg, true, "Moderator Panel", ImGuiColors.TankBlue);
                    DrawActionSelection();
                    ImGui.Text("Message to user:");
                    ImGui.InputTextMultiline("##message", ref moderatorMessage, 4000, new Vector2(ImGui.GetWindowSize().X - 20, ImGui.GetWindowSize().Y / 4));

                    if (ImGui.Button("Add Notes"))
                    {
                        addNotes = true;
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("This is only visible to moderators");
                    }
                    if (addNotes)
                    {
                        ImGui.Text("Moderator notes:");
                        ImGui.InputTextMultiline("##modnotes", ref moderatorNotes, 2000, new Vector2(ImGui.GetWindowSize().X - 20, ImGui.GetWindowSize().Y / 4));
                    }
                    Vector2 Size = ImGui.CalcTextSize("Submit");
                    float centeredX = (ImGui.GetWindowSize().X - Size.X) / 2.0f;
                    if (Misc.DrawCenteredButton(centeredX, new Vector2(Size.X + 2, Size.Y), "Submit"))
                    {
                        submitted = true;
                    }
                    if (submitted == true)
                    {
                        DrawConfirmation();
                    }
                    ImGui.TextColored(statusColor, status);

                }
            }
            catch (Exception ex)
            {
                Plugin.plugin.logger.Error("ModPanel Draw Error: " + ex.Message);
                status = "An error occurred while processing your request.";
                statusColor = new Vector4(1, 0, 0, 1); // Red color for error
            }
        }
        public static void DrawConfirmation()
        {
            ImGui.Checkbox("Confirm", ref confirmed);
            if (confirmed)
            {
                ImGui.SameLine();
                if(ImGui.Button("Confirm Submition"))
                {
                    DataSender.SubmitModeratorAction(capturedAuthor, capturedMessage, moderatorMessage, moderatorNotes, currentAction);
                    status = "Please stand by...";
                }
            }
        }
        public static void DrawActionSelection()
        {
            var (text, desc) = ModDefines.ModAccountActionVals[(int)currentAction];
            using var combo = OtterGui.Raii.ImRaii.Combo("Action Taken##Action", text);
            ImGuiUtil.HoverTooltip("Select an Action to take, (Actions are account wide).");
            if (!combo)
                return;
            foreach (var ((newText, newDesc), idx) in ModDefines.ModAccountActionVals.WithIndex())
            {
                if (idx != 9)
                {
                    if (ImGui.Selectable(newText, idx == (int)currentAction))
                        currentAction = (ModeratorAction)idx;
                        
                    ImGuiUtil.SelectableHelpMarker(newDesc);
                }

            }
        }
        public void Dispose()
        {
        }
    }

}
