using System;

/// <summary>
/// Parts of a character's body that worn armour can cover (and hide, so the body never shows through it): a helmet
/// hides the hair, boots the feet... The <see cref="CharacterAppearance"/> knows which renderers belong to each
/// region (Body Regions, or found by Scan Model). Serialized as numbers: append only.
/// </summary>
[Flags]
public enum BodyRegion
{
    None = 0,
    Hair = 1,
    Beard = 2,
    Face = 4,
    Ears = 8,
    Torso = 16,
    Arms = 32,
    Hands = 64,
    Legs = 128,
    Feet = 256,
    /// <summary>The neck (of the placeholder body: never hidden by armour by default, hidden by body models).</summary>
    Neck = 512,
}

/// <summary>Which body regions a worn piece hides. Serialized as numbers: append only.</summary>
public enum BodyRegionMode
{
    /// <summary>By slot: helmet → hair and ears, chestplate → torso, gloves → hands, leggings → legs, boots → feet.</summary>
    Auto,
    /// <summary>The regions ticked in Hide Regions.</summary>
    Custom,
    /// <summary>Nothing is hidden.</summary>
    None,
}

public static class BodyRegions
{
    /// <summary>What a slot covers by default.</summary>
    public static BodyRegion ForSlot(ArmorSlotType slot)
    {
        switch (slot)
        {
            case ArmorSlotType.Helmet: return BodyRegion.Hair | BodyRegion.Ears;
            case ArmorSlotType.Chestplate: return BodyRegion.Torso;
            case ArmorSlotType.Gloves: return BodyRegion.Hands;
            case ArmorSlotType.Leggings: return BodyRegion.Legs;
            case ArmorSlotType.Boots: return BodyRegion.Feet;
            default: return BodyRegion.None;
        }
    }

    /// <summary>The region a part of the model belongs to, from its name ("Chr_Torso_Male_01" → Torso), or None.</summary>
    public static BodyRegion FromName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return BodyRegion.None;
        string n = objectName.ToLowerInvariant();
        if (n.Contains("eyebrow") || n.Contains("brow")) return BodyRegion.Face;
        if (n.Contains("facialhair") || n.Contains("facial_hair") || n.Contains("beard") || n.Contains("moustache") || n.Contains("mustache") || n.Contains("goatee")) return BodyRegion.Beard;
        if (n.Contains("hair")) return BodyRegion.Hair;
        if (n.Contains("ear") && !n.Contains("wear") && !n.Contains("gear")) return BodyRegion.Ears;
        if (n.Contains("head") || n.Contains("face")) return BodyRegion.Face;
        if (n.Contains("hand") || n.Contains("finger")) return BodyRegion.Hands;
        if (n.Contains("foot") || n.Contains("feet") || n.Contains("toe") || n.Contains("shoe")) return BodyRegion.Feet;
        if (n.Contains("arm") || n.Contains("elbow") || n.Contains("shoulder")) return BodyRegion.Arms;
        if (n.Contains("leg") || n.Contains("hip") || n.Contains("knee") || n.Contains("pelvis")) return BodyRegion.Legs;
        if (n.Contains("torso") || n.Contains("chest") || n.Contains("spine") || n.Contains("body") || n.Contains("upper")) return BodyRegion.Torso;
        return BodyRegion.None;
    }
}
