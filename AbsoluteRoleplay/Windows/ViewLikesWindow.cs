using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using System.Numerics;
using AbsoluteRP.Helpers;
using Networking;
using System.Linq;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;

namespace AbsoluteRP.Windows
{
    public class ViewLikesWindow : Window
    {
        private Plugin plugin;
        private bool fetchedData = false;

        public ViewLikesWindow(Plugin plugin) : base("Profile Likes##viewLikes")
        {
            this.plugin = plugin;
            Size = new Vector2(500, 600);
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        public override void OnOpen()
        {
            // Fetch both profiles and like counts when window opens
            if (Plugin.character != null && !fetchedData)
            {
                DataSender.FetchProfiles(Plugin.character);
                DataSender.FetchProfileLikeCounts(Plugin.character);
                fetchedData = true;
            }
        }

        public override void OnClose()
        {
            // Reset fetch flag so data is refreshed next time window opens
            fetchedData = false;
        }

        public override void Draw()
        {
            ImGui.Text("Your Profiles and Their Likes:");
            ImGui.Separator();
            ImGui.Spacing();

            // Show loading state while profiles are being fetched
            if (ProfileWindow.profiles == null || ProfileWindow.profiles.Count == 0)
            {
                if (fetchedData)
                {
                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Loading profiles...");
                }
                else
                {
                    ImGui.TextColored(new Vector4(1f, 0.5f, 0.5f, 1f), "You have no profiles yet.");
                }
                return;
            }

            ImGui.BeginChild("ProfilesScroll", new Vector2(0, 0), true);

            foreach (var profile in ProfileWindow.profiles)
            {
                ImGui.PushID(profile.id);
                ImGui.BeginGroup();

                // Draw avatar
                if (profile.avatar != null)
                {
                    ImGui.Image(profile.avatar.Handle, new Vector2(64, 64));
                }
                else
                {
                    ImGui.Dummy(new Vector2(64, 64));
                }

                ImGui.SameLine();

                ImGui.BeginGroup();
                // Display profile title instead of player name and server
                string profileTitle = !string.IsNullOrEmpty(profile.title) ? profile.title : "Unnamed Profile";
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), profileTitle);

                // Get like count
                int likeCount = 0;
                if (DataReceiver.profileLikeCounts.TryGetValue(profile.id, out int count))
                {
                    likeCount = count;
                }

                ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), $"♥ {likeCount} likes");

                if (ImGui.Button($"View Likes##{profile.id}"))
                {
                    DataSender.FetchProfileLikes(profile.id);
                    plugin.OpenLikeDetailsWindow(profile);
                }

                ImGui.EndGroup();
                ImGui.EndGroup();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.PopID();
            }

            ImGui.EndChild();
        }
    }
}
