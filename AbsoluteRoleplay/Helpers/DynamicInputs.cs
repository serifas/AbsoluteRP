using AbsoluteRoleplay.Windows.Profiles;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System.ComponentModel;
using System.Numerics;

namespace AbsoluteRoleplay.Helpers
{

    public class IconElement
    {
        internal bool loaded { get; set; }
        public enum IconState
        {
            Displaying,
            Modifying
        }
        public IDalamudTextureWrap icon { get; set; }
        public IconState State { get; set; } = IconState.Displaying; // Default state
    }
}
