using Dalamud.Interface.Textures.TextureWraps;
using Lumina.Data.Parsing;
using Lumina.Excel.Sheets;
using System.Numerics;
using System.Runtime.Intrinsics;
using static AbsoluteRP.Defines.Items;

namespace AbsoluteRP.Defines
{

   

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

        public enum ItemQuality
        {
            Common = 0,
            Aetherial = 1,
            Scarce = 2,
            Rare = 3,
            Relic = 4,
        }



        public static Vector4 ItemQualityColors(int itemQuality)
        {
            switch (itemQuality)
            {
                case 0: return new Vector4(255 / 255.0f, 255 / 255.0f, 255 / 255.0f, 255 / 255.0f); // Common
                case 1: return new Vector4(243 / 255.0f, 131 / 255.0f, 189 / 255.0f, 255 / 255.0f); // Aetherial
                case 2: return new Vector4(111 / 255.0f, 243 / 255.0f, 104 / 255.0f, 255 / 255.0f); // Scarce
                case 3: return new Vector4(44 / 255.0f, 109 / 255.0f, 222 / 255.0f, 255 / 255.0f);  // Rare
                case 4: return new Vector4(133 / 255.0f, 28 / 255.0f, 199 / 255.0f, 255 / 255.0f);  // Relic
                default: return new Vector4(255 / 255.0f, 255 / 255.0f, 255 / 255.0f, 255 / 255.0f); // Default
            }
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
        public static readonly string[] ItemQualityTypes =
        {
            "Common",
            "Aetherial",
            "Scarce",
            "Rare",
            "Relic"
        };

    }
}
