using AbsoluteRP.Defines;

namespace AbsoluteRP.Defines
{
    // RP equipment slots — mirrors FFXIV gear slots but used for roleplay items,
    // not actual game gear. Players can equip RP items to these slots on their profile.
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

    // Helper methods and display names for equipment slots.
    // Both Ring slots share the same display name "Ring".
    public static class EquipmentSlotInfo
    {
        public const int SlotCount = 13; // total number of equipment slots

        // Human-readable names for each slot (indexed by EquipmentSlot enum value)
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
