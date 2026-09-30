using AbsoluteRP;
using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI.Pages;
using AbsoluteRP.Windows.Moderator;
using AbsoluteRP.Windows.Profiles;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Social.Views;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Numerics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Lumina.Data.Parsing.Layer.LayerCommon;
using Networking;

namespace AbsoluteRP.Network
{
    // Profile tab and layout submissions (plus batch buffer builders). Split out of DataSender.
    internal class ProfileTabs_DS
    {
        // Uploads a gallery tab's images to the server. Each image includes its URL, raw bytes, tooltip text, NSFW/trigger flags, and display index.
        public static async Task SubmitGalleryLayout(Character character, int profileIndex, GalleryLayout layout)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {

                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSendGallery);
                        buffer.WriteInt(layout.tabIndex);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(layout.images.Count);

                        for (int i = 0; i < layout.images.Count; i++)
                        {
                            buffer.WriteString(layout.images[i].url);
                            buffer.WriteInt(layout.images[i].imageBytes.Length);
                            buffer.WriteBytes(layout.images[i].imageBytes);
                            buffer.WriteString(layout.images[i].tooltip);
                            buffer.WriteBool(layout.images[i].nsfw);
                            buffer.WriteBool(layout.images[i].trigger);
                            buffer.WriteInt(layout.images[i].index);
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendGalleryImage: " + ex.ToString());
                }
            }
        }

        public static async void RemoveGalleryImage(Character character, int profileIndex, int index, int tabIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSendGalleryRemoveRequest);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(index);
                        buffer.WriteInt(tabIndex);
                        Plugin.PluginLog.Debug(index.ToString());
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendGalleryImage: " + ex.ToString());
                }
            }
        }

        // Uploads a story tab's chapters (title + content for each chapter) to the server
        public static async Task SubmitStoryLayout(Character character, int profileIndex, StoryLayout layout)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CSendStory);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(layout.tabIndex);
                        buffer.WriteInt(layout.chapters.Count);

                        buffer.WriteString(layout.name);
                        for (int i = 0; i < layout.chapters.Count; i++)
                        {
                            buffer.WriteString(layout.chapters[i].title);
                            buffer.WriteString(layout.chapters[i].content);
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendStory: " + ex.ToString());
                }
            }
        }

        // Saves a profile's Bio tab data: character traits, alignment, personality, custom fields, descriptors, and trait icons.
        public static async Task SubmitProfileBio(Character character, int profileIndex, BioLayout layout)
        {

            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CCreateProfileBio);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteBool(layout.isTooltip);
                        buffer.WriteInt(layout.tabIndex);
                        buffer.WriteString(layout.name);
                        buffer.WriteString(layout.race);
                        buffer.WriteString(layout.gender);
                        buffer.WriteString(layout.age);
                        buffer.WriteString(layout.height);
                        buffer.WriteString(layout.weight);
                        buffer.WriteString(layout.afg);
                        buffer.WriteInt(layout.alignment);
                        buffer.WriteInt(layout.personality_1);
                        buffer.WriteInt(layout.personality_2);
                        buffer.WriteInt(layout.personality_3);
                        buffer.WriteInt(layout.fields.Count);
                        buffer.WriteInt(layout.descriptors.Count);
                        buffer.WriteInt(layout.traits.Count);
                        for (int i = 0; i < layout.fields.Count; i++)
                        {
                            buffer.WriteString(layout.fields[i].name);
                            buffer.WriteString(layout.fields[i].description);
                        }
                        for (int i = 0; i < layout.descriptors.Count; i++)
                        {
                            buffer.WriteString(layout.descriptors[i].name);
                            buffer.WriteString(layout.descriptors[i].description);
                        }
                        for (int i = 0; i < layout.traits.Count; i++)
                        {
                            buffer.WriteString(layout.traits[i].name);
                            buffer.WriteString(layout.traits[i].description);
                            buffer.WriteInt(layout.traits[i].iconID);
                        }



                        await ClientTCP.SendDataAsync(buffer.ToArray());

                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SubmitProfileBio: " + ex.ToString());
                }
            }

        }

        public static async Task SubmitProfileDetails(Character character, int profileIndex, DetailsLayout layout)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendProfileDetails);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);

                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(layout.tabIndex);
                        buffer.WriteInt(layout.details.Count);
                        for (int i = 0; i < layout.details.Count; i++)
                        {
                            buffer.WriteInt(layout.details[i].id);
                            buffer.WriteString(layout.details[i].name);
                            buffer.WriteString(layout.details[i].content);
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendHooks: " + ex.ToString());
                }
            }

        }

        internal static async Task SubmitInfoLayout(Character character, int currentProfile, InfoLayout layout)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SSendInfo);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(currentProfile);
                        buffer.WriteInt(layout.tabIndex);
                        buffer.WriteString(layout.text);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendInfoLayout: " + ex.ToString());
                }
            }
        }

        // Creates a new inventory item with the given properties (name, description, icon, rarity, etc.)
        internal static async void SendItemCreation(Character character, int currentProfile, int tabIndex, string itemName, string itemDescription, int selectedItemType, int itemSubType, uint createItemIconID, int itemQuality, bool locked)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        Plugin.PluginLog.Debug("tooltipData = " + currentProfile);
                        buffer.WriteInt((int)ClientPackets.CreateItem);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(currentProfile);
                        buffer.WriteInt(tabIndex);
                        buffer.WriteString(itemName);
                        buffer.WriteString(itemDescription);
                        buffer.WriteInt(selectedItemType);
                        buffer.WriteInt(itemSubType);
                        buffer.WriteInt((int)createItemIconID);
                        buffer.WriteInt(itemQuality);
                        buffer.WriteBool(locked);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendItemCreation: " + ex.ToString());
                }
            }
        }

        internal static async void SendItemOrder(Character character, int profileIndex, InventoryLayout layout, List<ItemDefinition> slotContents)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SortItems);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(layout.tabIndex);
                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(slotContents.Count);
                        for (int i = 0; i < slotContents.Count; i++)
                        {
                            buffer.WriteString(slotContents[i].name);
                            buffer.WriteString(slotContents[i].description);
                            buffer.WriteInt(slotContents[i].type);
                            buffer.WriteInt(slotContents[i].subtype);
                            buffer.WriteInt(slotContents[i].iconID);
                            buffer.WriteInt(slotContents[i].slot);
                            buffer.WriteInt(slotContents[i].quality);
                            buffer.WriteBool(slotContents[i].locked);
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendItemOrder: " + ex.ToString());
                }
            }
        }

        internal static async void FetchProfileItems(Character character, int profileIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.FetchProfileItems);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendProfileItems: " + ex.ToString());
                }
            }
        }

        internal static async Task CreateTab(Character character, string name, int type, int profileIndex, int index)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CreateTab);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(profileIndex);
                        buffer.WriteString(name);
                        buffer.WriteInt(type);
                        buffer.WriteInt(index);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in Tab Creation: " + ex.ToString());
                }
            }
        }

        internal static async void CreateProfileBio(Character character, int index, int tabIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.CreateBio);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(index);
                        buffer.WriteInt(tabIndex);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in Bio Creation: " + ex.ToString());
                }
            }
        }

        internal static async void DeleteTab(Character character, int profileIndex, int tabIndex, int tab_type)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.DeleteTab);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(tabIndex);
                        buffer.WriteInt(tab_type);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in Bio Creation: " + ex.ToString());
                }
            }
        }

        internal static async void SubmitDynamicLayout(Character character, int profileIndex, DynamicLayout dynamicLayout)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    List<string> nodes = new List<string>();
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendDynamicTab);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(dynamicLayout.tabIndex);
                        var nonCanceledChildren = dynamicLayout.RootNode.Children
                      .Where(n => !n.relatedElement.canceled)
                      .ToList();

                        buffer.WriteInt(nonCanceledChildren.Count);

                        foreach (var node in nonCanceledChildren)
                        {
                            bool nullName = node.Name == null;

                            buffer.WriteBool(nullName);
                            if (!nullName)
                            {
                                WriteLayoutNodeData(buffer, node);
                            }
                        }


                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Debug in SendDynamicData: {ex}");
                }
            }
        }

        internal static void WriteLayoutNodeData(ByteBuffer buffer, LayoutTreeNode node)
        {
            try
            {
                buffer.WriteInt(node.relatedElement.type);
                buffer.WriteInt(node.ID);
                Plugin.PluginLog.Debug($"WriteInt: node.ID = {node.ID}");
                Plugin.PluginLog.Debug($"WriteString: node.Name = '{node.Name ?? "NULL"}'");
                buffer.WriteString(node.Name);
                buffer.WriteBool(node.IsFolder);
                buffer.WriteInt(node.Parent != null ? node.Parent.ID : -1);
                int layoutElementType = node.relatedElement.type;

                if (layoutElementType == (int)LayoutElementTypes.Folder)
                {
                    FolderElement folderElement = (FolderElement)node.relatedElement;
                    buffer.WriteInt(folderElement.id);
                    Plugin.PluginLog.Debug(folderElement.id + " " + node.ID);
                }
                if (layoutElementType == (int)LayoutElementTypes.Text)
                {
                    TextElement textElement = (TextElement)node.relatedElement;
                    buffer.WriteInt(textElement.id);
                    buffer.WriteInt(textElement.type);
                    buffer.WriteInt(textElement.subType);
                    buffer.WriteFloat(textElement.width);
                    buffer.WriteFloat(textElement.height);
                    buffer.WriteFloat(textElement.PosX);
                    buffer.WriteFloat(textElement.PosY);
                    buffer.WriteString(textElement.text);
                }
                if (layoutElementType == (int)LayoutElementTypes.Image)
                {
                    ImageElement imageElement = (ImageElement)node.relatedElement;
                    buffer.WriteInt(imageElement.id);
                    buffer.WriteInt(imageElement.type);
                    buffer.WriteInt(imageElement.bytes.Length); // <-- use imageBytes
                    buffer.WriteBytes(imageElement.bytes);      // <-- use imageBytes
                    buffer.WriteFloat(imageElement.width);
                    buffer.WriteFloat(imageElement.height);
                    buffer.WriteFloat(imageElement.PosX);
                    buffer.WriteFloat(imageElement.PosY);
                    buffer.WriteBool(imageElement.proprotionalEditing);
                    buffer.WriteBool(imageElement.hasTooltip);
                    buffer.WriteString(imageElement.tooltip);
                    buffer.WriteBool(imageElement.maximizable);
                }
                if (layoutElementType == (int)LayoutElementTypes.Icon)
                {
                    IconElement iconElement = (IconElement)node.relatedElement;
                    buffer.WriteInt(iconElement.id);
                    buffer.WriteInt(iconElement.type);
                    buffer.WriteInt(iconElement.iconID);
                    buffer.WriteFloat(iconElement.PosX);
                    buffer.WriteFloat(iconElement.PosY);

                }
                if (layoutElementType == (int)LayoutElementTypes.Empty)
                {
                    EmptyElement empty = (EmptyElement)node.relatedElement;
                    buffer.WriteInt(empty.id);
                    buffer.WriteString(empty.name);
                }
                var nonCanceledChildren = node.Children
            .Where(n => !n.relatedElement.canceled)
            .ToList();

                buffer.WriteInt(nonCanceledChildren.Count);
                foreach (var child in nonCanceledChildren)
                {
                    WriteLayoutNodeData(buffer, child);
                }
                Plugin.PluginLog.Debug($"Wrote node: {node.Name} with ID: {node.ID} and Type: {layoutElementType}");
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug writing layout node data: {ex}");
            }
        }

        internal static async void SendInventorySelection(Character character, int index, int tabID)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SInventorySelection);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(tabID);
                        buffer.WriteInt(index);
                        Plugin.PluginLog.Debug("Index=" + index);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendInventorySelection: " + ex.ToString());
                }
            }
        }

        internal static async void SendDeleteItem(Character character, int profileIndex, InventoryLayout layout, int slotIndex)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendItemDeletion);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(layout.tabIndex);
                        buffer.WriteInt(layout.id);
                        buffer.WriteInt(slotIndex);
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SendInventorySelection: " + ex.ToString());
                }
            }
        }

        internal static async Task SubmitTreeLayout(Character character, int profileIndex, TreeLayout treeLayout)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendTreeLayout);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(treeLayout.tabIndex);

                        // Serialize Paths
                        buffer.WriteInt(treeLayout.Paths.Count);
                        foreach (var path in treeLayout.Paths)
                        {
                            buffer.WriteInt(path.Count);
                            foreach (var slot in path)
                            {
                                buffer.WriteInt(slot.x);
                                buffer.WriteInt(slot.y);
                            }
                        }

                        // Serialize PathConnections
                        buffer.WriteInt(treeLayout.PathConnections.Count);
                        foreach (var pathConnections in treeLayout.PathConnections)
                        {
                            buffer.WriteInt(pathConnections.Count);
                            foreach (var conn in pathConnections)
                            {
                                bool all0 = conn.from.x == 0 && conn.from.y == 0 && conn.to.x == 0 && conn.to.y == 0;
                                if (all0)
                                {
                                    buffer.WriteBool(true);
                                }
                                else
                                {
                                    buffer.WriteBool(false);
                                }

                                buffer.WriteInt(conn.from.x);
                                buffer.WriteInt(conn.from.y);
                                buffer.WriteInt(conn.to.x);
                                buffer.WriteInt(conn.to.y);
                            }
                        }

                        // Serialize Relationships (nodes)
                        buffer.WriteInt(treeLayout.relationships.Count);
                        foreach (var rel in treeLayout.relationships)
                        {
                            buffer.WriteString(rel.Name ?? "");

                            buffer.WriteString(rel.Description ?? "");

                            buffer.WriteInt(rel.IconID);

                            buffer.WriteBool(rel.active);
                            buffer.WriteBool(rel.Slot.HasValue);
                            if (rel.Slot.HasValue)
                            {
                                buffer.WriteInt(rel.Slot.Value.x);
                                buffer.WriteInt(rel.Slot.Value.y);
                                Plugin.PluginLog.Debug($"[PreSend] Slot: {rel.Slot.Value.x}, {rel.Slot.Value.y}");
                            }

                            // Serialize Links
                            buffer.WriteInt(rel.Links?.Count ?? 0);
                            if (rel.Links != null)
                            {
                                foreach (var link in rel.Links)
                                {
                                    buffer.WriteInt(link.From.x);
                                    buffer.WriteInt(link.From.y);
                                    buffer.WriteInt(link.To.x);
                                    buffer.WriteInt(link.To.y);
                                }
                            }
                            buffer.WriteInt(rel.NodeKind);
                            buffer.WriteInt(rel.BondID);
                            buffer.WriteInt(rel.BondPeerAccountID);
                            buffer.WriteInt(rel.BondPeerProfileIndex);
                            buffer.WriteString(rel.BondPeerName ?? string.Empty);
                            buffer.WriteString(rel.BondPeerWorld ?? string.Empty);
                            buffer.WriteString(rel.BondTitle ?? string.Empty);
                            buffer.WriteString(rel.BondRelation ?? string.Empty);
                        }

                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SubmitTreeLayout: " + ex.ToString());
                }
            }
        }

        internal static async Task SubmitInventoryLayout(Character character, int profileIndex, InventoryLayout inventoryLayout)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendInventoryLayout);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(inventoryLayout.tabIndex);
                        buffer.WriteInt(inventoryLayout.inventorySlotContents.Count);
                        foreach (var item in inventoryLayout.inventorySlotContents.Values)
                        {
                            buffer.WriteString(item.name);
                            buffer.WriteString(item.description);
                            buffer.WriteInt(item.type);
                            buffer.WriteInt(item.subtype);
                            buffer.WriteInt(item.iconID);
                            buffer.WriteInt(item.slot);
                            buffer.WriteInt(item.quality);
                            Plugin.PluginLog.Debug($"Inventory Slot {item.slot}: {item.name}");
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SubmitTreeLayout: " + ex.ToString());
                }
            }
        }

        // Buffer Builders for Batch Sending. These build the raw byte[] for each packet without sending, so multiple packets can be batched into a single TCP write via ClientTCP.SendBatchAsync.

        public static byte[] BuildCreateTabBuffer(Character character, string name, int type, int profileIndex, int index)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CreateTab);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(profileIndex);
                    buffer.WriteString(name);
                    buffer.WriteInt(type);
                    buffer.WriteInt(index);
                    return buffer.ToArray();
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("Debug in BuildCreateTabBuffer: " + ex.ToString());
                return null;
            }
        }

        public static byte[] BuildProfileBioBuffer(Character character, int profileIndex, BioLayout layout)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CCreateProfileBio);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(profileIndex);
                    buffer.WriteBool(layout.isTooltip);
                    buffer.WriteInt(layout.tabIndex);
                    buffer.WriteString(layout.name);
                    buffer.WriteString(layout.race);
                    buffer.WriteString(layout.gender);
                    buffer.WriteString(layout.age);
                    buffer.WriteString(layout.height);
                    buffer.WriteString(layout.weight);
                    buffer.WriteString(layout.afg);
                    buffer.WriteInt(layout.alignment);
                    buffer.WriteInt(layout.personality_1);
                    buffer.WriteInt(layout.personality_2);
                    buffer.WriteInt(layout.personality_3);
                    buffer.WriteInt(layout.fields.Count);
                    buffer.WriteInt(layout.descriptors.Count);
                    buffer.WriteInt(layout.traits.Count);
                    for (int i = 0; i < layout.fields.Count; i++)
                    {
                        buffer.WriteString(layout.fields[i].name);
                        buffer.WriteString(layout.fields[i].description);
                    }
                    for (int i = 0; i < layout.descriptors.Count; i++)
                    {
                        buffer.WriteString(layout.descriptors[i].name);
                        buffer.WriteString(layout.descriptors[i].description);
                    }
                    for (int i = 0; i < layout.traits.Count; i++)
                    {
                        buffer.WriteString(layout.traits[i].name);
                        buffer.WriteString(layout.traits[i].description);
                        buffer.WriteInt(layout.traits[i].iconID);
                    }
                    return buffer.ToArray();
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("Debug in BuildProfileBioBuffer: " + ex.ToString());
                return null;
            }
        }

        public static byte[] BuildProfileDetailsBuffer(Character character, int profileIndex, DetailsLayout layout)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.SendProfileDetails);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(profileIndex);
                    buffer.WriteInt(layout.tabIndex);
                    buffer.WriteInt(layout.details.Count);
                    for (int i = 0; i < layout.details.Count; i++)
                    {
                        buffer.WriteInt(layout.details[i].id);
                        buffer.WriteString(layout.details[i].name);
                        buffer.WriteString(layout.details[i].content);
                    }
                    return buffer.ToArray();
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("Debug in BuildProfileDetailsBuffer: " + ex.ToString());
                return null;
            }
        }

        public static byte[] BuildGalleryLayoutBuffer(Character character, int profileIndex, GalleryLayout layout)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CSendGallery);
                    buffer.WriteInt(layout.tabIndex);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(profileIndex);
                    buffer.WriteInt(layout.images.Count);
                    for (int i = 0; i < layout.images.Count; i++)
                    {
                        buffer.WriteString(layout.images[i].url);
                        buffer.WriteInt(layout.images[i].imageBytes.Length);
                        buffer.WriteBytes(layout.images[i].imageBytes);
                        buffer.WriteString(layout.images[i].tooltip);
                        buffer.WriteBool(layout.images[i].nsfw);
                        buffer.WriteBool(layout.images[i].trigger);
                        buffer.WriteInt(layout.images[i].index);
                    }
                    return buffer.ToArray();
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("Debug in BuildGalleryLayoutBuffer: " + ex.ToString());
                return null;
            }
        }

        public static byte[] BuildStoryLayoutBuffer(Character character, int profileIndex, StoryLayout layout)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CSendStory);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(profileIndex);
                    buffer.WriteInt(layout.tabIndex);
                    buffer.WriteInt(layout.chapters.Count);
                    buffer.WriteString(layout.name);
                    for (int i = 0; i < layout.chapters.Count; i++)
                    {
                        buffer.WriteString(layout.chapters[i].title);
                        buffer.WriteString(layout.chapters[i].content);
                    }
                    return buffer.ToArray();
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("Debug in BuildStoryLayoutBuffer: " + ex.ToString());
                return null;
            }
        }

        public static byte[] BuildInfoLayoutBuffer(Character character, int profileIndex, InfoLayout layout)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.SSendInfo);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(profileIndex);
                    buffer.WriteInt(layout.tabIndex);
                    buffer.WriteString(layout.text);
                    return buffer.ToArray();
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("Debug in BuildInfoLayoutBuffer: " + ex.ToString());
                return null;
            }
        }

        public static byte[] BuildInventoryLayoutBuffer(Character character, int profileIndex, InventoryLayout inventoryLayout)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.SendInventoryLayout);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(profileIndex);
                    buffer.WriteInt(inventoryLayout.tabIndex);
                    buffer.WriteInt(inventoryLayout.inventorySlotContents.Count);
                    foreach (var item in inventoryLayout.inventorySlotContents.Values)
                    {
                        buffer.WriteString(item.name);
                        buffer.WriteString(item.description);
                        buffer.WriteInt(item.type);
                        buffer.WriteInt(item.subtype);
                        buffer.WriteInt(item.iconID);
                        buffer.WriteInt(item.slot);
                        buffer.WriteInt(item.quality);
                    }
                    return buffer.ToArray();
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("Debug in BuildInventoryLayoutBuffer: " + ex.ToString());
                return null;
            }
        }

        public static byte[] BuildTreeLayoutBuffer(Character character, int profileIndex, TreeLayout treeLayout)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.SendTreeLayout);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(profileIndex);
                    buffer.WriteInt(treeLayout.tabIndex);
                    buffer.WriteInt(treeLayout.Paths.Count);
                    foreach (var path in treeLayout.Paths)
                    {
                        buffer.WriteInt(path.Count);
                        foreach (var slot in path)
                        {
                            buffer.WriteInt(slot.x);
                            buffer.WriteInt(slot.y);
                        }
                    }
                    buffer.WriteInt(treeLayout.PathConnections.Count);
                    foreach (var pathConnections in treeLayout.PathConnections)
                    {
                        buffer.WriteInt(pathConnections.Count);
                        foreach (var conn in pathConnections)
                        {
                            bool all0 = conn.from.x == 0 && conn.from.y == 0 && conn.to.x == 0 && conn.to.y == 0;
                            buffer.WriteBool(all0);
                            buffer.WriteInt(conn.from.x);
                            buffer.WriteInt(conn.from.y);
                            buffer.WriteInt(conn.to.x);
                            buffer.WriteInt(conn.to.y);
                        }
                    }
                    buffer.WriteInt(treeLayout.relationships.Count);
                    foreach (var rel in treeLayout.relationships)
                    {
                        buffer.WriteString(rel.Name ?? "");
                        buffer.WriteString(rel.Description ?? "");
                        buffer.WriteInt(rel.IconID);
                        buffer.WriteBool(rel.active);
                        buffer.WriteBool(rel.Slot.HasValue);
                        if (rel.Slot.HasValue)
                        {
                            buffer.WriteInt(rel.Slot.Value.x);
                            buffer.WriteInt(rel.Slot.Value.y);
                        }
                        buffer.WriteInt(rel.Links?.Count ?? 0);
                        if (rel.Links != null)
                        {
                            foreach (var link in rel.Links)
                            {
                                buffer.WriteInt(link.From.x);
                                buffer.WriteInt(link.From.y);
                                buffer.WriteInt(link.To.x);
                                buffer.WriteInt(link.To.y);
                            }
                        }
                        buffer.WriteInt(rel.NodeKind);
                        buffer.WriteInt(rel.BondID);
                        buffer.WriteInt(rel.BondPeerAccountID);
                        buffer.WriteInt(rel.BondPeerProfileIndex);
                        buffer.WriteString(rel.BondPeerName ?? string.Empty);
                        buffer.WriteString(rel.BondPeerWorld ?? string.Empty);
                        buffer.WriteString(rel.BondTitle ?? string.Empty);
                        buffer.WriteString(rel.BondRelation ?? string.Empty);
                    }
                    return buffer.ToArray();
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("Debug in BuildTreeLayoutBuffer: " + ex.ToString());
                return null;
            }
        }

        // End Buffer Builders

        internal static async Task SendTabReorder(Character character, int profileIndex, List<(int oldIndex, int newIndex)> indexChanges)
        {
            if (ClientTCP.IsConnected())
            {
                try
                {
                    using (var buffer = new ByteBuffer())
                    {
                        buffer.WriteInt((int)ClientPackets.SendTabReorder);
                        buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                        buffer.WriteString(character.characterKey);


                        buffer.WriteInt(profileIndex);
                        buffer.WriteInt(indexChanges.Count);
                        foreach (var (oldIdx, newIdx) in indexChanges)
                        {
                            buffer.WriteInt(oldIdx);
                            buffer.WriteInt(newIdx);
                        }
                        await ClientTCP.SendDataAsync(buffer.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Debug("Debug in SubmitTreeLayout: " + ex.ToString());
                }
            }
        }
    }
}
