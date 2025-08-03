using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using System;
using Networking;
using Dalamud.Interface.Colors;

namespace AbsoluteRoleplay.Windows.Profiles
{
    public class NotesWindow : Window, IDisposable
    {
        public static string reportCharacterName;
        public static string reportCharacterWorld;
        public static string reportInfo = string.Empty;
        public static string reportStatus = string.Empty;
        public static string profileNotes = string.Empty;
        public static string characterNameVal;
        public static string characterWorldVal;
        public static int characterIndex;
        public static Plugin pg;
        public NotesWindow(Plugin plugin) : base(
       "NOTES", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
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
                Misc.SetTitle(pg, true, "Personal Notes", ImGuiColors.TankBlue);

                ImGui.Text("Here you can add personal notes about this player or profile");
                ImGui.InputTextMultiline(
                    "##info",
                    ref profileNotes,
                    1000,
                    new Vector2(ImGui.GetContentRegionAvail().X, 200) // 200 pixels tall, full width
                );
                if (ImGui.Button("Add Notes"))
                {
                    if (pg.IsOnline())
                    {
                        DataSender.AddProfileNotes(characterIndex, profileNotes);
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
