using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Common.Math;
using Networking;

namespace AbsoluteRP.Windows.Profiles
{
    public class ReportWindow : Window, IDisposable
    {
        public static string reportCharacterName;
        public static string reportCharacterWorld;
        public static string reportInfo = string.Empty;
        public static string reportStatus = string.Empty;
        public ReportWindow() : base(
       "REPORT USER PROFILE", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(100, 100),
                MaximumSize = new Vector2(1200, 950)
            };


        }
        public override void Draw()
        {
            if (Plugin.plugin.IsOnline())
            {
                ImGui.TextColored(new Vector4(100, 0, 0, 100), reportStatus);
                ImGui.Text("Reason for report");
                ImGui.InputTextMultiline("##info", ref reportInfo, 5000, new Vector2(ImGui.GetWindowSize().X - 20, ImGui.GetWindowSize().Y - ImGui.GetWindowSize().Y / 2));

                if (ImGui.Button("Report!"))
                {
                    if (reportInfo.Length > 15)
                    {
                        //report the currently viewed profile to the moderators
                        DataSender.ReportProfile(Plugin.plugin.username, reportCharacterName, reportCharacterWorld, reportInfo);
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
