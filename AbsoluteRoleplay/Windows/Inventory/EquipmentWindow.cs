using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Networking;
using Vector2 = System.Numerics.Vector2;

namespace AbsoluteRP.Windows.Inventory
{
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

            EquipmentPage.RenderEquipmentPage(Plugin.plugin, true);
        }
    }
}
