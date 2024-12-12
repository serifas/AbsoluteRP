using Dalamud.Interface.Textures.TextureWraps;
using static AbsoluteRoleplay.Defines.Items;

namespace AbsoluteRoleplay.Defines
{

    public  class Item
    {
        public string name { get; set; }
        public string description { get; set; }
        public int type { get; set; }
        public int subtype { get; set; }
        public int iconID { get; set; }
        public int slot {  get; set; }
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
            Key = 7,
        }
        public enum ConsumableType
        {
            Potion = 0,
            Elixer = 1,
            Drink = 2,
            Food = 5,
            Aetheric = 6,
        }
        public enum MaterialType
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
            Dagger = 2,
            Flail = 3,
            Mace = 4,
            Mallet = 5,
            Staff = 6,
            Sword = 7,
            Greatsword = 8,
            Axe = 9,
            Greataxe = 10,
            Chakrams = 11,
            Lance = 12,
            Brush = 13,
            Astroglobe = 14,
            Nouliths = 15,
            Gunblade = 16,
            Warscythe = 17,
            Bow = 18,
            Firearm = 19, 
            Shield = 20,
            Pallet = 21,
            Wand = 22,
            Thrown = 23,
        }

        public static readonly (string, string, string[]?)[] InventoryTypes =
        {
            ("Consumable", "• Is consumed after specified amount of uses.", new string[]
                {
                    "Potion",
                    "Elixer ",
                    "Tonic",
                    "Drink",
                    "Food",
                    "Aetheric",
                }),
            ("Armor",     "• Can be equipped in an armor slot.", new string[]
                {
                   "Bone",
                   "Cloth ",
                   "Leather",
                   "Mail",
                   "Plate",
                   "Crystal",
                   "Magitek",
                   "Allagan",
                   "Runic",
                }),
            ("Weapon", "• Can be equipped in a weapon slot.", new string[]
            {
                    "Fist",
                    "Tome",
                    "Dagger",
                    "Flail",
                    "Mace",
                    "Mallet",
                    "Staff",
                    "Sword",
                    "Greatsword",
                    "Axe",
                    "Greataxe",
                    "Chakrams",
                    "Lance",
                    "Brush",
                    "Astroglobe",
                    "Nouliths",
                    "Gunblade",
                    "Warscythe",
                    "Bow",
                    "Firearm",
                    "Shield",
                    "Pallet",
                    "Wand",
                    "Thrown",

                }),
            ("Material", "• Can be used to craft an item.", new string[]   
            {
                   "Bone",
                   "Cloth ",
                   "Leather",
                   "Metal",
                   "Stone",
                   "Crystal",
                   "Magitek",
                   "Allagan",
                   "Rune",
            }),
        };

       
    }
}
