using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Networking;
using AbsoluteRP.RsUI.Pages;
using Vector2 = System.Numerics.Vector2;
using AbsoluteRP.Network;

namespace AbsoluteRP.Windows.Inventory
{
    // Window for viewing and managing RP equipment slots - players can equip RP items to gear slots
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

            // Toggle to let other players inspect your equipment - saves immediately on change
            bool equipPublic = ProfilesPage.CurrentProfile?.equipmentPublic ?? false;
            if (RsElements.Toggle("equip_public", ref equipPublic, "Allow others to inspect my equipment"))
            {
                if (ProfilesPage.CurrentProfile != null && Plugin.character != null)
                {
                    ProfilesPage.CurrentProfile.equipmentPublic = equipPublic;
                    var p = ProfilesPage.CurrentProfile;
                    _ = Profiles_DS.SetProfileStatus(
                        Plugin.character, p.isPrivate, p.isActive, ProfilesPage.profileIndex,
                        p.title, p.titleColor, p.avatarBytes, p.backgroundBytes,
                        p.SpoilerARR, p.SpoilerHW, p.SpoilerSB, p.SpoilerSHB,
                        p.SpoilerEW, p.SpoilerDT, p.NSFW, p.TRIGGERING, equipPublic);
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("When enabled, other players can see your equipped items from your profile.");
            InvUI.Muted(equipPublic ? "Visible on your profile." : "Only you can see this.");

            InvUI.Gap(6f);

            if (!RsElements.BeginPanel("equip_panel", null, fitContentsX: false, fitContentsY: true))
            {
                RsElements.EndPanel();
                return;
            }
            try
            {
                EquipmentPage.RenderEquipmentPage(Plugin.plugin, true);
            }
            finally { RsElements.EndPanel(); }
        }
    }
}
