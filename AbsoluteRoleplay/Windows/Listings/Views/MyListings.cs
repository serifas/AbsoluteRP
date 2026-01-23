using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Networking;
using System;
using System.Numerics;

namespace AbsoluteRP.Windows.Listings.Views
{
    internal static class MyListings
    {
        private static bool confirmDelete = false;
        private static int deleteTargetId = -1;

        public static void Draw()
        {
            ImGui.TextColored(new Vector4(1, 0.8f, 0.3f, 1), "My Listings");
            ImGui.Separator();

            if (ImGui.Button("Refresh"))
            {
                if (ClientTCP.IsConnected())
                {
                    DataSender.RequestOwnedListings(Plugin.character, 0);
                }
            }

            ImGui.Spacing();

            var listings = ListingsWindow.myListings;

            if (listings == null || listings.Count == 0)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "You haven't created any listings yet.");
                ImGui.Spacing();
                if (ImGui.Button("Create Your First Listing"))
                {
                    ListingsWindow.isEditMode = false;
                    ListingsWindow.editingListing = null;
                    ListingEditor.ResetForm();
                    ListingsWindow.currentView = ListingsWindow.VIEW_CREATE;
                }
                return;
            }

            ImGui.Text($"You have {listings.Count} listing(s)");
            ImGui.Spacing();

            // Listings table
            using var table = ImRaii.Table("MyListingsTable", 3, ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg);
            if (!table)
                return;

            ImGui.TableSetupColumn("Listing", ImGuiTableColumnFlags.WidthFixed, 200);
            ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthStretch);

            foreach (var listing in listings)
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);

                // Avatar/Banner
                if (listing.avatar != null && listing.avatar.Handle != nint.Zero)
                {
                    ImGui.Image(listing.avatar.Handle, new Vector2(80, 80));
                }

                // Name with color
                ImGui.TextColored(listing.color, listing.name);

                // Category
                if (listing.category >= 0 && UI.ListingCategoryVals != null && listing.category < UI.ListingCategoryVals.Length)
                {
                    var (catText, _) = UI.ListingCategoryVals[listing.category];
                    ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), catText);
                }

                ImGui.TableSetColumnIndex(1);

                // Status indicator
                // TODO: Add actual status from server (active, pending, expired, etc.)
                ImGui.TextColored(new Vector4(0.3f, 1, 0.3f, 1), "Active");

                // Date info
                if (!string.IsNullOrEmpty(listing.startDate))
                {
                    ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), $"Starts: {listing.startDate}");
                }
                if (!string.IsNullOrEmpty(listing.endDate))
                {
                    ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), $"Ends: {listing.endDate}");
                }

                ImGui.TableSetColumnIndex(2);

                // Action buttons
                if (ImGui.Button($"View##{listing.id}"))
                {
                    ListingsWindow.OpenListingDetail(listing);
                }

                ImGui.SameLine();

                if (ImGui.Button($"Edit##{listing.id}"))
                {
                    ListingsWindow.EditListing(listing);
                }

                ImGui.SameLine();

                // Delete with confirmation
                if (confirmDelete && deleteTargetId == listing.id)
                {
                    ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 1), "Confirm?");
                    ImGui.SameLine();
                    if (ImGui.Button($"Yes##{listing.id}"))
                    {
                        // TODO: Call delete API
                        // DataSender.DeleteListing(Plugin.character, listing.id);
                        confirmDelete = false;
                        deleteTargetId = -1;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button($"No##{listing.id}"))
                    {
                        confirmDelete = false;
                        deleteTargetId = -1;
                    }
                }
                else
                {
                    if (ImGui.Button($"Delete##{listing.id}"))
                    {
                        confirmDelete = true;
                        deleteTargetId = listing.id;
                    }
                }
            }
        }
    }
}
