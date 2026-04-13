using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Networking;
using Vector2 = System.Numerics.Vector2;

namespace AbsoluteRP.Windows.Inventory
{
    // Window for viewing and managing RP equipment slots — players can equip RP items to gear slots
    public class EquipmentWindow : Window, IDisposable
    {
        public EquipmentWindow() : base(
            "EQUIPMENT",
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(300, 450),
                MaximumSize = new Vector2(500, 700)
            };
        }

        public void Dispose() { }

        public override void Draw()
        {
            if (!Plugin.IsOnline())
                return;

            // Toggle to let other players inspect your equipment — saves immediately on change
            bool equipPublic = ProfileWindow.CurrentProfile?.equipmentPublic ?? false;
            if (ImGui.Checkbox("Allow others to inspect my equipment", ref equipPublic))
            {
                if (ProfileWindow.CurrentProfile != null && Plugin.character != null)
                {
                    ProfileWindow.CurrentProfile.equipmentPublic = equipPublic;
                    var p = ProfileWindow.CurrentProfile;
                    _ = DataSender.SetProfileStatus(
                        Plugin.character, p.isPrivate, p.isActive, ProfileWindow.profileIndex,
                        p.title, p.titleColor, p.avatarBytes, p.backgroundBytes,
                        p.SpoilerARR, p.SpoilerHW, p.SpoilerSB, p.SpoilerSHB,
                        p.SpoilerEW, p.SpoilerDT, p.NSFW, p.TRIGGERING, equipPublic);
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("When enabled, other players can see your equipped items from your profile.");

            ImGui.Spacing();

            EquipmentPage.RenderEquipmentPage(Plugin.plugin, true);
        }
    }
}
