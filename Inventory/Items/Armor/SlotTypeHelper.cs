// Helper class for slot type conversions - Updated for new armor types
public static class SlotTypeHelper
{
    // Convert ItemType to SlotType
    public static SlotType ItemTypeToSlotType(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.Helmet => SlotType.Helmet,
            ItemType.Armor => SlotType.Armor,
            ItemType.Leggings => SlotType.Leggings,
            ItemType.Boots => SlotType.Boots,
            ItemType.Gloves => SlotType.Gloves,
            ItemType.Shield => SlotType.Shield,
            ItemType.Ring => SlotType.Ring,
            ItemType.Trinket => SlotType.Trinket,
            ItemType.Cloak => SlotType.Cloak,
            ItemType.Belt => SlotType.Belt,
            ItemType.Shoulders => SlotType.Shoulders,
            ItemType.Bracers => SlotType.Wrist,
            ItemType.Amulet => SlotType.Amulet,
            ItemType.Potion => SlotType.Potion,
            ItemType.Food => SlotType.Food,
            _ => SlotType.Common
        };
    }

    // Convert SlotType to ItemType
    public static ItemType SlotTypeToItemType(SlotType slotType)
    {
        return slotType switch
        {
            SlotType.Helmet => ItemType.Helmet,
            SlotType.Armor => ItemType.Armor,
            SlotType.Leggings => ItemType.Leggings,
            SlotType.Boots => ItemType.Boots,
            SlotType.Gloves => ItemType.Gloves,
            SlotType.Shield => ItemType.Shield,
            SlotType.Ring => ItemType.Ring,
            SlotType.Trinket => ItemType.Trinket,
            SlotType.Cloak => ItemType.Cloak,
            SlotType.Belt => ItemType.Belt,
            SlotType.Shoulders => ItemType.Shoulders,
            SlotType.Wrist => ItemType.Bracers,
            SlotType.Amulet => ItemType.Amulet,
            SlotType.Potion => ItemType.Potion,
            SlotType.Food => ItemType.Food,
            _ => ItemType.Armor // Default fallback
        };
    }

    // Convert ArmorSlotType to SlotType
    public static SlotType ArmorSlotTypeToSlotType(ArmorSlotType armorSlotType)
    {
        return armorSlotType switch
        {
            ArmorSlotType.Helmet => SlotType.Helmet,
            ArmorSlotType.Chestplate => SlotType.Armor,
            ArmorSlotType.Leggings => SlotType.Leggings,
            ArmorSlotType.Boots => SlotType.Boots,
            ArmorSlotType.Gloves => SlotType.Gloves,
            ArmorSlotType.Shield => SlotType.Shield,
            ArmorSlotType.Ring => SlotType.Ring,
            ArmorSlotType.Trinket => SlotType.Trinket,
            ArmorSlotType.Cloak => SlotType.Cloak,
            ArmorSlotType.Belt => SlotType.Belt,
            ArmorSlotType.Shoulders => SlotType.Shoulders,
            ArmorSlotType.Bracers => SlotType.Wrist,
            ArmorSlotType.Amulet => SlotType.Amulet,
            _ => SlotType.Common
        };
    }

    // Convert SlotType to ArmorSlotType
    public static ArmorSlotType SlotTypeToArmorSlotType(SlotType slotType)
    {
        return slotType switch
        {
            SlotType.Helmet => ArmorSlotType.Helmet,
            SlotType.Armor => ArmorSlotType.Chestplate,
            SlotType.Leggings => ArmorSlotType.Leggings,
            SlotType.Boots => ArmorSlotType.Boots,
            SlotType.Gloves => ArmorSlotType.Gloves,
            SlotType.Shield => ArmorSlotType.Shield,
            SlotType.Ring => ArmorSlotType.Ring,
            SlotType.Trinket => ArmorSlotType.Trinket,
            SlotType.Cloak => ArmorSlotType.Cloak,
            SlotType.Belt => ArmorSlotType.Belt,
            SlotType.Shoulders => ArmorSlotType.Shoulders,
            SlotType.Wrist => ArmorSlotType.Bracers,
            SlotType.Amulet => ArmorSlotType.Amulet,
            _ => ArmorSlotType.Chestplate // Default fallback
        };
    }

    // Check if a slot type is an equipment slot
    public static bool IsEquipmentSlot(SlotType slotType)
    {
        return slotType != SlotType.Common &&
               slotType != SlotType.Potion &&
               slotType != SlotType.Food;
    }

    // Check if a slot type is an armor slot
    public static bool IsArmorSlot(SlotType slotType)
    {
        return slotType switch
        {
            SlotType.Helmet or SlotType.Armor or SlotType.Leggings or
            SlotType.Boots or SlotType.Gloves or SlotType.Shield or
            SlotType.Ring or SlotType.Trinket or SlotType.Cloak or
            SlotType.Belt or SlotType.Shoulders or SlotType.Wrist or
            SlotType.Amulet => true,
            _ => false
        };
    }

    // Get display name for slot type
    public static string GetDisplayName(SlotType slotType)
    {
        return slotType switch
        {
            SlotType.Common => "Inventory",
            SlotType.Potion => "Potion",
            SlotType.Food => "Food",
            SlotType.Helmet => "Helmet",
            SlotType.Armor => "Chestplate",
            SlotType.Leggings => "Leggings",
            SlotType.Boots => "Boots",
            SlotType.Gloves => "Gloves",
            SlotType.Shield => "Shield",
            SlotType.Ring => "Ring",
            SlotType.Trinket => "Trinket",
            SlotType.Cloak => "Cloak",
            SlotType.Belt => "Belt",
            SlotType.Shoulders => "Shoulders",
            SlotType.Wrist => "Bracers",
            SlotType.Amulet => "Amulet",
            _ => slotType.ToString()
        };
    }

    // Get display name for armor slot type
    public static string GetDisplayName(ArmorSlotType armorSlotType)
    {
        return armorSlotType switch
        {
            ArmorSlotType.Helmet => "Helmet",
            ArmorSlotType.Chestplate => "Chestplate",
            ArmorSlotType.Leggings => "Leggings",
            ArmorSlotType.Boots => "Boots",
            ArmorSlotType.Gloves => "Gloves",
            ArmorSlotType.Shield => "Shield",
            ArmorSlotType.Ring => "Ring",
            ArmorSlotType.Trinket => "Trinket",
            ArmorSlotType.Cloak => "Cloak",
            ArmorSlotType.Belt => "Belt",
            ArmorSlotType.Shoulders => "Shoulders",
            ArmorSlotType.Bracers => "Bracers",
            ArmorSlotType.Amulet => "Amulet",
            _ => armorSlotType.ToString()
        };
    }

    // Get all armor slot types
    public static SlotType[] GetArmorSlotTypes()
    {
        return new SlotType[]
        {
            SlotType.Helmet,
            SlotType.Armor,
            SlotType.Leggings,
            SlotType.Boots,
            SlotType.Gloves,
            SlotType.Shield,
            SlotType.Ring,
            SlotType.Trinket,
            SlotType.Cloak,
            SlotType.Belt,
            SlotType.Shoulders,
            SlotType.Wrist,
            SlotType.Amulet
        };
    }

    // Get all armor slot types as ArmorSlotType enum
    public static ArmorSlotType[] GetAllArmorSlotTypes()
    {
        return new ArmorSlotType[]
        {
            ArmorSlotType.Helmet,
            ArmorSlotType.Chestplate,
            ArmorSlotType.Leggings,
            ArmorSlotType.Boots,
            ArmorSlotType.Gloves,
            ArmorSlotType.Shield,
            ArmorSlotType.Ring,
            ArmorSlotType.Trinket,
            ArmorSlotType.Cloak,
            ArmorSlotType.Belt,
            ArmorSlotType.Shoulders,
            ArmorSlotType.Bracers,
            ArmorSlotType.Amulet
        };
    }

    // Check if two slot types are compatible for armor swapping
    public static bool AreCompatibleArmorSlots(SlotType slotType1, SlotType slotType2)
    {
        // Both must be armor slots
        if (!IsArmorSlot(slotType1) || !IsArmorSlot(slotType2)) return false;

        // Same slot types are always compatible
        if (slotType1 == slotType2) return true;

        // Common slots can accept any armor
        if (slotType1 == SlotType.Common || slotType2 == SlotType.Common) return true;

        return false;
    }

    // Get armor slot priority for equipment optimization
    public static int GetArmorSlotPriority(ArmorSlotType armorSlotType)
    {
        return armorSlotType switch
        {
            ArmorSlotType.Chestplate => 1,  // Highest priority
            ArmorSlotType.Helmet => 2,
            ArmorSlotType.Leggings => 3,
            ArmorSlotType.Boots => 4,
            ArmorSlotType.Gloves => 5,
            ArmorSlotType.Shield => 6,
            ArmorSlotType.Shoulders => 7,
            ArmorSlotType.Belt => 8,
            ArmorSlotType.Cloak => 9,
            ArmorSlotType.Bracers => 10,
            ArmorSlotType.Ring => 11,
            ArmorSlotType.Trinket => 12,
            ArmorSlotType.Amulet => 13,     // Lowest priority
            _ => 99
        };
    }
}