using AbsoluteRoleplay.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AbsoluteRoleplay.Helpers
{
    internal static class BackupLoader
    {
        public static BioLayout LoadBioLayout(string input)
        {
            BioLayout layout = new BioLayout();
            layout.tabIndex = ParseInt(GetTagValue(input, "tabIndex"));
            layout.tabName = GetTagValue(input, "tabName");
            layout.name = GetTagValue(input, "name");
            layout.race = GetTagValue(input, "race");
            layout.gender = GetTagValue(input, "gender");
            layout.age = GetTagValue(input, "age");
            layout.height = GetTagValue(input, "height");
            layout.weight = GetTagValue(input, "weight");
            layout.afg = GetTagValue(input, "afg");
            layout.alignment = ParseInt(GetTagValue(input, "alignment"));
            layout.personality_1 = ParseInt(GetTagValue(input, "personality_1"));
            layout.personality_2 = ParseInt(GetTagValue(input, "personality_2"));
            layout.personality_3 = ParseInt(GetTagValue(input, "personality_3"));

            // Descriptors
            layout.descriptors = new List<descriptor>();
            string descBlock = GetBlock(input, "descriptors");
            foreach (var desc in GetBlocks(descBlock, "index"))
            {
                var d = new descriptor();
                d.index = ParseInt(desc);
                d.name = GetTagValue(descBlock, "name");
                d.description = GetTagValue(descBlock, "description");
                layout.descriptors.Add(d);
            }

            // Fields
            layout.fields = new List<field>();
            string fieldsBlock = GetBlock(input, "fields");
            foreach (var fieldBlock in GetBlocks(fieldsBlock, "index"))
            {
                var f = new field();
                f.index = ParseInt(fieldBlock);
                f.name = GetTagValue(fieldsBlock, "name");
                f.description = GetTagValue(fieldsBlock, "description");
                layout.fields.Add(f);
            }

            // Traits
            layout.traits = new List<trait>();
            string traitsBlock = GetBlock(input, "traits");
            foreach (var traitBlock in GetBlocks(traitsBlock, "index"))
            {
                var t = new trait();
                t.index = ParseInt(traitBlock);
                t.name = GetTagValue(traitsBlock, "name");
                t.description = GetTagValue(traitsBlock, "description");
                t.iconID = ParseInt(GetTagValue(traitsBlock, "iconID"));
                layout.traits.Add(t);
            }
            return layout;
        }

        public static DetailsLayout LoadDetailsLayout(string input)
        {
            DetailsLayout layout = new DetailsLayout();
            layout.tabIndex = ParseInt(GetTagValue(input, "tabIndex"));
            layout.tabName = GetTagValue(input, "tabName");
            layout.details = new List<Detail>();
            foreach (var detailBlock in GetBlocks(input, "id"))
            {
                var d = new Detail();
                d.id = ParseInt(detailBlock);
                d.name = GetTagValue(input, "name");
                d.content = GetTagValue(input, "content");
                layout.details.Add(d);
            }
            return layout;
        }

        public static GalleryLayout LoadGalleryLayout(string input)
        {
            GalleryLayout layout = new GalleryLayout();
            layout.tabIndex = ParseInt(GetTagValue(input, "tabIndex"));
            layout.tabName = GetTagValue(input, "tabName");
            layout.images = new List<ProfileGalleryImage>();
            foreach (var imgBlock in GetBlocks(input, "image"))
            {
                var img = new ProfileGalleryImage();
                img.index = ParseInt(GetTagValue(imgBlock, "id"));
                img.nsfw = ParseBool(GetTagValue(imgBlock, "nsfw"));
                img.trigger = ParseBool(GetTagValue(imgBlock, "trigger"));
                img.url = GetTagValue(imgBlock, "url");
                var bytesStr = GetTagValue(imgBlock, "bytes");
                img.imageBytes = string.IsNullOrEmpty(bytesStr) ? Array.Empty<byte>() : Convert.FromBase64String(bytesStr);
                img.tooltip = GetTagValue(imgBlock, "tooltip");
                layout.images.Add(img);
            }
            return layout;
        }

        public static InfoLayout LoadInfoLayout(string input)
        {
            InfoLayout layout = new InfoLayout();
            layout.tabIndex = ParseInt(GetTagValue(input, "tabIndex"));
            layout.tabName = GetTagValue(input, "tabName");
            layout.text = GetTagValue(input, "text");
            return layout;
        }

        public static StoryLayout LoadStoryLayout(string input)
        {
            StoryLayout layout = new StoryLayout();
            layout.tabIndex = ParseInt(GetTagValue(input, "tabIndex"));
            layout.tabName = GetTagValue(input, "tabName");
            layout.chapters = new List<StoryChapter>();
            foreach (var chapterBlock in GetBlocks(input, "chapter"))
            {
                var c = new StoryChapter();
                c.id = ParseInt(GetTagValue(chapterBlock, "id"));
                c.title = GetTagValue(chapterBlock, "title");
                c.content = GetTagValue(chapterBlock, "content");
                layout.chapters.Add(c);
            }
            return layout;
        }

        public static InventoryLayout LoadInventoryLayout(string input)
        {
            InventoryLayout layout = new InventoryLayout();
            layout.tabIndex = ParseInt(GetTagValue(input, "tabIndex"));
            layout.tabName = GetTagValue(input, "tabName");
            layout.inventorySlotContents = new Dictionary<int, ItemDefinition>();
            foreach (var itemBlock in GetBlocks(input, "item"))
            {
                var item = new ItemDefinition();
                item.name = GetTagValue(itemBlock, "name");
                item.description = GetTagValue(itemBlock, "description");
                item.type = int.Parse(GetTagValue(itemBlock, "type"));
                item.subtype = int.Parse(GetTagValue(itemBlock, "subType"));
                item.quality = int.Parse(GetTagValue(itemBlock, "quality"));
                item.iconID = ParseInt(GetTagValue(itemBlock, "iconID"));
                item.slot = ParseInt(GetTagValue(itemBlock, "slot"));
                layout.inventorySlotContents[item.slot] = item;
            }
            return layout;
        }

        public static TreeLayout LoadTreeLayout(string input)
        {
            TreeLayout layout = new TreeLayout();
            layout.tabIndex = ParseInt(GetTagValue(input, "tabIndex"));
            layout.tabName = GetTagValue(input, "tabName");

            // Relationships
            layout.relationships = new List<Relationship>();
            string relationshipsBlock = GetBlock(input, "relationships");
            foreach (string relBlock in GetBlocks(relationshipsBlock, "relationship"))
            {
                var rel = new Relationship();
                string slotStr = GetTagValue(relBlock, "slot");
                if (!string.IsNullOrEmpty(slotStr))
                {
                    var parts = slotStr.Split(',');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                        rel.Slot = (x, y);
                }
                rel.Name = GetTagValue(relBlock, "name");
                rel.Description = GetTagValue(relBlock, "description");
                rel.IconID = ParseInt(GetTagValue(relBlock, "iconID"));
                rel.LineColor = ParseVector4(GetTagValue(relBlock, "lineColor"));
                rel.LineThickness = ParseFloat(GetTagValue(relBlock, "lineThickness"));
                layout.relationships.Add(rel);
            }

            // Paths
            layout.Paths = new List<List<(int x, int y)>>();
            string pathsBlock = GetBlock(input, "paths");
            foreach (string pathBlock in GetBlocks(pathsBlock, "path"))
            {
                var path = new List<(int x, int y)>();
                string pathContent = pathBlock.Trim('<', '>', '/');
                foreach (var pair in pathContent.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    var coords = pair.Split(',');
                    if (coords.Length == 2 && int.TryParse(coords[0], out int x) && int.TryParse(coords[1], out int y))
                        path.Add((x, y));
                }
                layout.Paths.Add(path);
            }

            // PathConnections
            layout.PathConnections = new List<List<((int x, int y) from, (int x, int y) to)>>();
            string pathConnsBlock = GetBlock(input, "pathConnections");
            foreach (string connsBlock in GetBlocks(pathConnsBlock, "connections"))
            {
                var conns = new List<((int x, int y) from, (int x, int y) to)>();
                string connsContent = connsBlock.Trim('<', '>', '/');
                foreach (var connPair in connsContent.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = connPair.Split('-', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        var fromCoords = parts[0].Split(',');
                        var toCoords = parts[1].Split(',');
                        if (fromCoords.Length == 2 && toCoords.Length == 2 &&
                            int.TryParse(fromCoords[0], out int fx) && int.TryParse(fromCoords[1], out int fy) &&
                            int.TryParse(toCoords[0], out int tx) && int.TryParse(toCoords[1], out int ty))
                        {
                            conns.Add(((fx, fy), (tx, ty)));
                        }
                    }
                }
                layout.PathConnections.Add(conns);
            }
            return layout;
        }

        // --- Helper Methods ---

        private static string GetTagValue(string input, string tag)
        {
            var match = Regex.Match(input, $"<{tag}>(.*?)</{tag}>", RegexOptions.Singleline);
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        private static string GetBlock(string input, string tag)
        {
            var match = Regex.Match(input, $"<{tag}>(.*?)</{tag}>", RegexOptions.Singleline);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static List<string> GetBlocks(string input, string tag)
        {
            var matches = Regex.Matches(input, $"<{tag}>(.*?)</{tag}>", RegexOptions.Singleline);
            var blocks = new List<string>();
            foreach (Match m in matches)
                blocks.Add(m.Groups[1].Value);
            return blocks;
        }

        private static int ParseInt(string s) => int.TryParse(s, out var v) ? v : 0;
        private static float ParseFloat(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
        private static bool ParseBool(string s) => bool.TryParse(s, out var v) && v;
        private static System.Numerics.Vector4 ParseVector4(string s)
        {
            var parts = s.Split(',');
            if (parts.Length == 4 &&
                float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z) &&
                float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float w))
                return new System.Numerics.Vector4(x, y, z, w);
            return new System.Numerics.Vector4(1, 1, 1, 1);
        }
    }
}