using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Networking;
using OtterGui.Raii;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace AbsoluteRoleplay.Windows.Listings
{
    internal class ListingsWindow : Window, IDisposable
    {
        public Plugin plugin;
        public static Configuration configuration;
        public static Vector2 buttonScale;
        public static List<Listing> listings = new List<Listing>();
        public static string loading; //loading status string for loading the profile gallery mainly
        public static float percentage = 0f; //loading base value
        public static int loaderInd = 0;
        private FileDialogManager _fileDialogManager; //for banners only at the moment
        public static IDalamudTextureWrap banner;
        public static byte[] bannerBytes;

        public bool DrawListingCreation { get; private set; }

        public ListingsWindow(Plugin plugin) : base(
       "LISTINGS", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {

                MinimumSize = new Vector2(200, 400),
                MaximumSize = new Vector2(1000, 1000)
            };

            this.plugin = plugin;
            configuration = plugin.Configuration;
            _fileDialogManager = new FileDialogManager();
        }
        public override void OnOpen()
        {
            if (bannerBytes == null)
            {
                //set the avatar to the avatar_holder.png by default
                if (Plugin.PluginInterface is { AssemblyLocation.Directory.FullName: { } path })
                {
                    bannerBytes = File.ReadAllBytes(Path.Combine(path, "UI/common/blank.png"));
                }
            }
            banner = Plugin.TextureProvider.CreateFromImageAsync(bannerBytes).Result;
            buttonScale = new Vector2(ImGui.GetIO().FontGlobalScale / 0.015f, ImGui.GetIO().FontGlobalScale / 0.030f);
            buttonScale = new Vector2(ImGui.GetIO().FontGlobalScale / 0.015f, ImGui.GetIO().FontGlobalScale / 0.030f);
        }
        public override void Draw()
        {
           
                _fileDialogManager.Draw();

            if (ListingCreation.editBanner == true)
            {
                ListingCreation.editBanner = false;
                EditBanner();
            }
            using var listing = ImRaii.Child("LISTING");
            if (listing)
            {
               
                ImGui.Columns(2, "layoutColumns", false);
                ImGui.SetColumnWidth(0, buttonScale.X * 2 + 30);
                ImGui.Text("Manage Listing");
                for (var i = 0; i < UI.ListingNavigationVals.Length; i++)
                {
                    var (id, navBtnName, image) = UI.ListingNavigationVals[i];


                    if (ImGui.CollapsingHeader(navBtnName))
                    {

                        for (int l = 0; l < listings.Count; l++)
                        {
                            if (id == listings[l].type)
                            {
                                
                            }
                        }
                    }
                }
                ImGui.Spacing();
                ImGui.SetCursorPos(new Vector2(0, ImGui.GetWindowHeight() - 50));
                if (ImGui.Button("Create Listing"))
                {
                    ListingCreation.listings.Clear();
                    ListingCreation.ResetPages();
                    ListingCreation.ResetElements();
                    ListingCreation.StartListingCreation = true;
                    ListingCreation.DrawInfoCreation = true;
                    DrawListingCreation = true;
                }
                ImGui.NextColumn();
                if (DrawListingCreation)
                {
                    ListingCreation.DrawListingsCreation();
                }
            

            }
        }
        public void Dispose() 
        { 
        }   
        public void LoadListings(int type)
        {

        }


        public void EditBanner()
        {
            _fileDialogManager.OpenFileDialog("Select Image", "Image{.png,.jpg}", (s, f) =>
            {
                if (!s)
                    return;
                var imagePath = f[0].ToString();
                bannerBytes = File.ReadAllBytes(imagePath);
                banner = Plugin.TextureProvider.CreateFromImageAsync(bannerBytes).Result;
            }, 0, null, configuration.AlwaysOpenDefaultImport);
        }
    }
}
