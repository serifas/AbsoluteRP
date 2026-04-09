using AbsoluteRP.Defines;

namespace AbsoluteRP.Defines
{
    public enum EquipmentSlot
    {
        Head = 0,
        Body = 1,
        Hands = 2,
        Legs = 3,
        Feet = 4,
        MainHand = 5,
        OffHand = 6,
        Earring = 7,
        Necklace = 8,
        Bracelet = 9,
        Ring1 = 10,
        Ring2 = 11,
        Soulstone = 12
    }

    public static class EquipmentSlotInfo
    {
        public const int SlotCount = 13;

        public static readonly string[] SlotNames = new string[]
        {
            "Head",
            "Body",
            "Hands",
            "Legs",
            "Feet",
            "Main Hand",
            "Off Hand",
            "Earring",
            "Necklace",
            "Bracelet",
            "Ring",
            "Ring",
            "Soulstone"
        };

        /// <summary>
        /// Returns allowed InventoryItemType values for a given equipment slot.
        /// -1 means any type is allowed.
        /// </summary>
        public static int[] GetAllowedTypes(EquipmentSlot slot)
        {
            // All slots accept any item type — this is a roleplay system, not game mechanics
            return new[] { -1 };
        }

        /// <summary>
        /// Check if an item type is allowed in a given slot.
        /// </summary>
        public static bool IsTypeAllowed(EquipmentSlot slot, int itemType)
        {
            return true;
        }
    }
}
