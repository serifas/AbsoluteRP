using AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRoleplay.Helpers
{
    internal class BackupWriter
    {
        public static void WriteTabContent(CustomTab tab, StreamWriter writer)
        {
            try
            {
                if (tab.Layout is BioLayout bioLayout)
                {
                    writer.WriteLine("<bioTab>");
                    writer.WriteLine($"<tabIndex>{bioLayout.tabIndex}</tabIndex>");
                    writer.WriteLine($"<tabName>{bioLayout.name}</tabName>");
                    WriteBioContent(bioLayout, writer);
                    writer.WriteLine("</bioTab>");
                }
                else if (tab.Layout is DetailsLayout detailsLayout)
                {
                    writer.WriteLine("<detailsTab>");
                    writer.WriteLine($"<tabIndex>{detailsLayout.tabIndex}</tabIndex>");
                    writer.WriteLine($"<tabName>{detailsLayout.name}</tabName>");
                    WriteDetailsContent(detailsLayout, writer);
                    writer.WriteLine("</detailsTab>");
                }
                else if (tab.Layout is GalleryLayout galleryLayout)
                {
                    writer.WriteLine("<galleryTab>");
                    writer.WriteLine($"<tabIndex>{galleryLayout.tabIndex}</tabIndex>");
                    writer.WriteLine($"<tabName>{galleryLayout.name}</tabName>");
                    WriteGalleryContent(galleryLayout, writer);
                    writer.WriteLine("</galleryTab>");
                }
                else if (tab.Layout is InfoLayout infoLayout)
                {
                    writer.WriteLine("<infoTab>");
                    writer.WriteLine($"<tabIndex>{infoLayout.tabIndex}</tabIndex>");
                    writer.WriteLine($"<tabName>{infoLayout.name}</tabName>");
                    WriteInfoContent(infoLayout, writer);
                    writer.WriteLine("</infoTab>");
                }
                else if (tab.Layout is StoryLayout storyLayout)
                {
                    writer.WriteLine("<storyTab>");
                    writer.WriteLine($"<tabIndex>{storyLayout.tabIndex}</tabIndex>");
                    writer.WriteLine($"<tabName>{storyLayout.name}</tabName>");
                    WriteStoryContent(storyLayout, writer);
                    writer.WriteLine("</storyTab>");
                }
                else if (tab.Layout is InventoryLayout inventoryLayout)
                {
                    writer.WriteLine("<inventoryTab>");
                    writer.WriteLine($"<tabIndex>{inventoryLayout.tabIndex}</tabIndex>");
                    writer.WriteLine($"<tabName>{inventoryLayout.name}</tabName>");
                    WriteInventoryContent(inventoryLayout, writer);
                    writer.WriteLine("</inventoryTab>");
                }
                else if (tab.Layout is TreeLayout treeLayout)
                {
                    writer.WriteLine("<treeTab>");
                    writer.WriteLine($"<tabIndex>{treeLayout.tabIndex}</tabIndex>");
                    writer.WriteLine($"<tabName>{treeLayout.name}</tabName>");
                    WriteTreeContent(treeLayout, writer);
                    writer.WriteLine("</treeTab>");
                }
                else
                {
                    throw new NotSupportedException($"Unsupported layout type: {tab.Layout.GetType().Name}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }
        }

        private static void WriteBioContent(BioLayout bioLayout, StreamWriter writer)
        {
            writer.WriteLine($"<name>{EscapeTagContent(bioLayout.name)}</name>");
            writer.WriteLine($"<race>{EscapeTagContent(bioLayout.race)}</race>");
            writer.WriteLine($"<gender>{EscapeTagContent(bioLayout.gender)}</gender>");
            writer.WriteLine($"<age>{EscapeTagContent(bioLayout.age)}</age>");
            writer.WriteLine($"<height>{EscapeTagContent(bioLayout.height)}</height>");
            writer.WriteLine($"<weight>{EscapeTagContent(bioLayout.weight)}</weight>");
            writer.WriteLine($"<afg>{EscapeTagContent(bioLayout.afg)}</afg>");
            writer.WriteLine($"<alignment>{EscapeTagContent(bioLayout.alignment.ToString())}</alignment>");
            writer.WriteLine($"<personality_1>{EscapeTagContent(bioLayout.personality_1.ToString())}</personality_1>");
            writer.WriteLine($"<personality_2>{EscapeTagContent(bioLayout.personality_2.ToString())}</personality_2>");
            writer.WriteLine($"<personality_3>{EscapeTagContent(bioLayout.personality_3.ToString())}</personality_3>");

            // Descriptors
            writer.WriteLine("<descriptors>");
            foreach (descriptor descriptor in bioLayout.descriptors)
            {
                writer.WriteLine("<descriptor>");
                writer.WriteLine($"<index>{EscapeTagContent(descriptor.index.ToString())}</index>");
                writer.WriteLine($"<name>{EscapeTagContent(descriptor.name)}</name>");
                writer.WriteLine($"<description>{EscapeTagContent(descriptor.description)}</description>");
                writer.WriteLine("</descriptor>");
            }
            writer.WriteLine("</descriptors>");

            // Fields
            writer.WriteLine("<fields>");
            foreach (field field in bioLayout.fields)
            {
                writer.WriteLine("<field>");
                writer.WriteLine($"<index>{EscapeTagContent(field.index.ToString())}</index>");
                writer.WriteLine($"<name>{EscapeTagContent(field.name)}</name>");
                writer.WriteLine($"<description>{EscapeTagContent(field.description)}</description>");
                writer.WriteLine("</field>");
            }
            writer.WriteLine("</fields>");

            // Traits
            writer.WriteLine("<traits>");
            foreach (trait trait in bioLayout.traits)
            {
                writer.WriteLine("<trait>");
                writer.WriteLine($"<index>{EscapeTagContent(trait.index.ToString())}</index>");
                writer.WriteLine($"<name>{EscapeTagContent(trait.name)}</name>");
                writer.WriteLine($"<description>{EscapeTagContent(trait.description)}</description>");
                writer.WriteLine($"<iconID>{EscapeTagContent(trait.iconID.ToString())}</iconID>");
                writer.WriteLine("</trait>");
            }
            writer.WriteLine("</traits>");
        }

        private static void WriteDetailsContent(DetailsLayout detailsLayout, StreamWriter writer)
        {
            foreach (Detail detail in detailsLayout.details)
            {
                writer.WriteLine("<detail>");
                writer.WriteLine($"<id>{EscapeTagContent(detail.id.ToString())}</id>");
                writer.WriteLine($"<name>{EscapeTagContent(detail.name)}</name>");
                // Write markup content directly, do not escape
                writer.WriteLine($"<content>{detail.content}</content>");
                writer.WriteLine("</detail>");
            }
        }

        private static void WriteGalleryContent(GalleryLayout galleryLayout, StreamWriter writer)
        {
            foreach (ProfileGalleryImage image in galleryLayout.images)
            {
                writer.WriteLine("<image>");
                writer.WriteLine($"<id>{EscapeTagContent(image.index.ToString())}</id>");
                writer.WriteLine($"<nsfw>{EscapeTagContent(image.nsfw.ToString())}</nsfw>");
                writer.WriteLine($"<trigger>{EscapeTagContent(image.trigger.ToString())}</trigger>");
                writer.WriteLine($"<url>{EscapeTagContent(image.url)}</url>");
                writer.WriteLine($"<bytes>{EscapeTagContent(Convert.ToBase64String(image.imageBytes))}</bytes>");
                writer.WriteLine($"<tooltip>{EscapeTagContent(image.tooltip)}</tooltip>");
                writer.WriteLine("</image>");
            }
        }

        private static void WriteInfoContent(InfoLayout infoLayout, StreamWriter writer)
        {
            // Write markup content directly, do not escape
            writer.WriteLine($"<text>{infoLayout.text}</text>");
        }

        private static void WriteStoryContent(StoryLayout storyLayout, StreamWriter writer)
        {
            foreach (StoryChapter chapter in storyLayout.chapters)
            {
                writer.WriteLine("<chapter>");
                writer.WriteLine($"<id>{EscapeTagContent(chapter.id.ToString())}</id>");
                writer.WriteLine($"<title>{EscapeTagContent(chapter.title)}</title>");
                // Write markup content directly, do not escape
                writer.WriteLine($"<content>{chapter.content}</content>");
                writer.WriteLine("</chapter>");
            }
        }

        private static void WriteInventoryContent(InventoryLayout inventoryLayout, StreamWriter writer)
        {
            foreach (ItemDefinition item in inventoryLayout.inventorySlotContents.Values)
            {
                writer.WriteLine("<item>");
                writer.WriteLine($"<name>{EscapeTagContent(item.name)}</name>");
                writer.WriteLine($"<description>{EscapeTagContent(item.description)}</description>");
                writer.WriteLine($"<type>{EscapeTagContent(item.type.ToString())}</type>");
                writer.WriteLine($"<subType>{EscapeTagContent(item.subtype.ToString())}</subType>");
                writer.WriteLine($"<quality>{EscapeTagContent(item.quality.ToString())}</quality>");
                writer.WriteLine($"<iconID>{EscapeTagContent(item.iconID.ToString())}</iconID>");
                writer.WriteLine($"<slot>{EscapeTagContent(item.slot.ToString())}</slot>");
                writer.WriteLine("</item>");
            }
        }

        private static void WriteTreeContent(TreeLayout treeLayout, StreamWriter writer)
        {
            writer.WriteLine("<relationships>");
            if (treeLayout.relationships != null)
            {
                foreach (var rel in treeLayout.relationships)
                {
                    writer.WriteLine("<relationship>");
                    if (rel.Slot.HasValue)
                        writer.WriteLine($"<slot>{rel.Slot.Value.x},{rel.Slot.Value.y}</slot>");
                    writer.WriteLine($"<name>{EscapeTagContent(rel.Name ?? string.Empty)}</name>");
                    writer.WriteLine($"<description>{EscapeTagContent(rel.Description ?? string.Empty)}</description>");
                    writer.WriteLine($"<iconID>{rel.IconID}</iconID>");
                    writer.WriteLine($"<lineColor>{rel.LineColor.X},{rel.LineColor.Y},{rel.LineColor.Z},{rel.LineColor.W}</lineColor>");
                    writer.WriteLine($"<lineThickness>{rel.LineThickness}</lineThickness>");
                    writer.WriteLine("</relationship>");
                }
            }
            writer.WriteLine("</relationships>");

            // Write paths
            writer.WriteLine("<paths>");
            if (treeLayout.Paths != null)
            {
                foreach (var path in treeLayout.Paths)
                {
                    writer.Write("<path>");
                    if (path != null && path.Count > 0)
                    {
                        for (int i = 0; i < path.Count; i++)
                        {
                            var slot = path[i];
                            writer.Write($"{slot.x},{slot.y}");
                            if (i < path.Count - 1)
                                writer.Write(";");
                        }
                    }
                    writer.WriteLine("</path>");
                }
            }
            writer.WriteLine("</paths>");

            // Write path connections
            writer.WriteLine("<pathConnections>");
            if (treeLayout.PathConnections != null)
            {
                foreach (var pathConns in treeLayout.PathConnections)
                {
                    writer.Write("<connections>");
                    if (pathConns != null && pathConns.Count > 0)
                    {
                        for (int i = 0; i < pathConns.Count; i++)
                        {
                            var conn = pathConns[i];
                            writer.Write($"{conn.from.x},{conn.from.y}-{conn.to.x},{conn.to.y}");
                            if (i < pathConns.Count - 1)
                                writer.Write(";");
                        }
                    }
                    writer.WriteLine("</connections>");
                }
            }
            writer.WriteLine("</pathConnections>");
        }

        // Only escape for plain text fields, not markup
        private static string EscapeTagContent(string content)
        {
            return content.Replace("\\", "\\\\")
                          .Replace("<", "\\<")
                          .Replace(">", "\\>");
        }
    }
}
