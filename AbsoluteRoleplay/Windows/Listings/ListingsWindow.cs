using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Listings.Views;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Networking;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace AbsoluteRP.Windows.Listings
{
    internal class ListingsWindow : Window, IDisposable
    {
        public static Configuration configuration;
        public static int currentView = 0;

        // View constants
        public const int VIEW_BROWSE = 0;
        public const int VIEW_MY_LISTINGS = 1;
        public const int VIEW_CREATE = 2;
        public const int VIEW_DETAIL = 3;

        // Loaded listings data
        public static List<Listing> searchResults = new List<Listing>();
        public static List<Listing> myListings = new List<Listing>();
        public static Listing selectedListing = null;

        // Edit mode flag
        public static bool isEditMode = false;
        public static Listing editingListing = null;

        public ListingsWindow() : base(
            "LISTINGS", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(600, 400),
                MaximumSize = new Vector2(1200, 800)
            };

            configuration = Plugin.plugin.Configuration;
        }

        public override void OnOpen()
        {
            if (ClientTCP.IsConnected())
            {
                // Load user's listings on open
                DataSender.RequestOwnedListings(Plugin.character, 0);
            }
        }

        public override void Draw()
        {
            try
            {
                DrawNavigation();
                ImGui.Separator();

                using (ImRaii.Child("ListingsContent", new Vector2(0, 0), false))
                {
                    switch (currentView)
                    {
                        case VIEW_BROWSE:
                            ListingBrowse.Draw();
                            break;
                        case VIEW_MY_LISTINGS:
                            MyListings.Draw();
                            break;
                        case VIEW_CREATE:
                            ListingEditor.Draw();
                            break;
                        case VIEW_DETAIL:
                            ListingDetail.Draw();
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"ListingsWindow.Draw error: {ex.Message}");
            }
        }

        private void DrawNavigation()
        {
            float buttonWidth = 100;

            if (ImGui.Button("Browse"))
            {
                currentView = VIEW_BROWSE;
            }
            ImGui.SameLine();

            if (ImGui.Button("My Listings"))
            {
                currentView = VIEW_MY_LISTINGS;
                if (ClientTCP.IsConnected())
                {
                    DataSender.RequestOwnedListings(Plugin.character, 0);
                }
            }
            ImGui.SameLine();

            if (ImGui.Button("+ Create New"))
            {
                isEditMode = false;
                editingListing = null;
                ListingEditor.ResetForm();
                currentView = VIEW_CREATE;
            }
        }

        public static void OpenListingDetail(Listing listing)
        {
            selectedListing = listing;
            currentView = VIEW_DETAIL;
        }

        public static void EditListing(Listing listing)
        {
            isEditMode = true;
            editingListing = listing;
            ListingEditor.LoadListing(listing);
            currentView = VIEW_CREATE;
        }

        public static void BackToBrowse()
        {
            currentView = VIEW_BROWSE;
            selectedListing = null;
        }

        public static void BackToMyListings()
        {
            currentView = VIEW_MY_LISTINGS;
            selectedListing = null;
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }
}
