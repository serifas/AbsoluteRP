using Dalamud.Interface.Colors;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using ImGuiScene;
using AbsoluteRoleplay;
using OtterGui.Raii;
using OtterGui;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dalamud.Interface.GameFonts;
using Dalamud.Game.Gui.Dtr;
using Microsoft.VisualBasic;
using Networking;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Internal;

namespace AbsoluteRoleplay.Windows
{
    public class ReportWindow : Window, IDisposable
    {
        public static string reportCharacterName;
        public static string reportCharacterWorld;
        public static string reportInfo = string.Empty;
        public static string reportStatus = string.Empty;
        public static Plugin pg;
        public ReportWindow(Plugin plugin) : base(
       "REPORT USER PROFILE", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(100, 100),
                MaximumSize = new Vector2(1200, 950)
            };
            
            pg = plugin;

        }
        public override void Draw()
        {
            if (pg.IsOnline())
            {
                ImGui.TextColored(new Vector4(100, 0, 0, 100), reportStatus);
                ImGui.Text("Reason for report");
                ImGui.InputTextMultiline("##info", ref reportInfo, 5000, new Vector2(ImGui.GetWindowSize().X - 20, ImGui.GetWindowSize().Y - ImGui.GetWindowSize().Y / 2));
           
                if (ImGui.Button("Report!"))
                {
                    if (reportInfo.Length > 15)
                    {
                        //report the currently viewed profile to the moderators
                        DataSender.ReportProfile(pg.username, reportCharacterName, reportCharacterWorld, reportInfo);
                    }
                    else
                    {
                        reportStatus = "Please give a reason for the report.";
                    }
                }
            }
           
        }
        public void Dispose()
        {
            reportInfo = string.Empty;
            reportCharacterName = string.Empty;
            reportCharacterWorld = string.Empty;
        }
    }

}
