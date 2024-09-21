using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Dalamud.Utility;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Networking;
using Dalamud.Interface.Internal;
using OtterGui;
using System.Linq;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Textures.TextureWraps;
using System.Security.Cryptography;

namespace AbsoluteRoleplay.Windows.Profiles
{
    //TODO
    /*
    //changed
    public class EventCreationWindow : Window, IDisposable
    {
        private Plugin plugin;
        private IDalamudPluginInterface pg;
        private FileDialogManager _fileDialogManager;
        public Configuration configuration;

        public EventCreationWindow(Plugin plugin) : base(
       "EVENT CREATION", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {

                MinimumSize = new Vector2(600, 400),
                MaximumSize = new Vector2(750, 950)
            };

            this.plugin = plugin;
            pg = Plugin.PluginInterface;
            configuration = plugin.Configuration;
            _fileDialogManager = new FileDialogManager();
        }
        public override void OnOpen()
        {
            
        }
        //method to check if we have loaded our data received from the server
        public static bool AllLoaded()
        {
            return false;
        }
        public override void Draw()
        {
           
        }
        public void Dispose()
        {

        }
       
    }*/
}


