using AbsoluteRP.Windows.Listings;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRP.Windows.Social.Views
{
    internal class Communities
    {
        public static void LoadCommunities()
        {
         /*   using (ImRaii.Table("Personal Listings", 2, ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Profile", ImGuiTableColumnFlags.WidthFixed, 200);
                ImGui.TableSetupColumn("Controls", ImGuiTableColumnFlags.WidthStretch);

                foreach (var group in SocialWindow.groups)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    if (listing.avatar.Handle != null && listing.avatar.Handle != IntPtr.Zero)
                    {
                        ImGui.Image(listing.avatar.Handle, new Vector2(100, 100));
                    }
                    ImGui.TextColored(listing.color, listing.name);

                    if (ImGui.Button($"View##{listing.id}"))
                    {
                        Plugin.plugin.OpenTargetWindow();
                        TargetProfileWindow.RequestingProfile = true;
                        TargetProfileWindow.ResetAllData();
                        DataSender.FetchProfile(Plugin.character, false, -1, string.Empty, string.Empty, listing.id);
                    }
                    ImGui.SameLine();
                    if (ImGui.Button($"Bookmark##{listing.id}"))
                    {
                        DataSender.BookmarkPlayer(Plugin.character, string.Empty, string.Empty, listing.id);
                    }
                    ImGui.TableSetColumnIndex(1);
                    //description here
                }
            }
         */
        }
    }
}
