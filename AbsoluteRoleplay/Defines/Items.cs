using Dalamud.Interface.Textures.TextureWraps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AbsoluteRoleplay.Defines.Items;
using static AbsoluteRoleplay.UI;

namespace AbsoluteRoleplay.Defines
{
    internal class InventoryItem
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public IDalamudTextureWrap Icon { get; set; }
        public static InventoryItemType inventoryType { get; set; }
    }

    internal class Items
    {
        public enum InventoryItemType
        {            
            Consumeable = 0,
            Quest = 1,
            Armor = 2,
            Weapon = 3,
            Material = 4,
            Container = 5,
            Script = 6,
            Pet = 7,
            Key = 8,
        }
        public enum ConsumableType
        {
            Potion = 0,
            Elixer = 1,
            Drink = 2,
            Food = 5,
            Rune = 6,
        }
        public enum ArmorType
        {
            Bone = 0,
            Cloth = 1,
            Leather = 2,
            Mail = 3,
            Plate = 4,
            Crystal = 5,
            Magitek = 6,
            Alagan = 7,
            Runic = 8,
        }
        public enum Weapon
        {
            Fist = 0,
            Tome = 1,
            Wand = 6,
            Dagger = 1,
            Flail = 1,
            Mace = 4,
            Mallet = 3,
            Staff = 4,
            Sword = 5,
            Greatsword = 6,
            Lance = 7,
            Brush = 8,
        }

        public static readonly (string, string)[] InventoryTypes =
        {   
            ("Consumable", "• Is consumed after specified amount of uses."),
            ("Quest ", "• Is an item used to progress or finish a quest"),
            ("Armor",     "• Can be equipped in an armor slot."),
            ("Weapon", "• Can be equipped in a weapon slot."),
            ("Material", "• Can be used to craft an item."),
            ("Container", "• Is used to hold any type of item, can be locked so it requires a key."),
            ("Script", "• An item that can be read, good for things like a message in a bottle."),
            ("Pet", "• A non summonable pet."),
            ("Key", "• A key for opening locked containers."),
        };
    }
}
