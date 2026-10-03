using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reusable attachment points on a character. <see cref="EquipmentVisuals"/> finds them on humanoid rigs (Animator bones)
/// or uses the transforms you assign; missing ones are created near where they belong. Serialized as numbers: append only.
/// </summary>
public enum AttachPoint
{
    RightHand,
    LeftHand,
    Back,
    BackLeft,
    BackRight,
    HipLeft,
    HipRight,
    Chest,
    Head,
    Hips,
    LeftForearm,
    RightForearm,
    LeftUpperArm,
    RightUpperArm,
    LeftUpperLeg,
    RightUpperLeg,
    LeftLowerLeg,
    RightLowerLeg,
    LeftFoot,
    RightFoot,
    Neck,
}

/// <summary>Where a weapon or shield goes when it is not in use. Serialized as numbers: append only.</summary>
public enum SheathPoint
{
    /// <summary>By category: one-handed blades and maces on the left hip, daggers on the right hip, big weapons, bows and shields on the back; tools and other items stay in hand.</summary>
    Auto,
    /// <summary>Never sheathed: it stays in the hand (the old behaviour).</summary>
    StayInHand,
    /// <summary>Disappears when not in use.</summary>
    Hidden,
    Back,
    BackLeft,
    BackRight,
    HipLeft,
    HipRight,
    Chest,
    /// <summary>A child transform of the character with the name given in Custom Socket.</summary>
    Custom,
}

/// <summary>How an armour piece's model is put on the character. Serialized as numbers: append only.</summary>
public enum ArmorAttachMode
{
    /// <summary>Skinned meshes follow the character's skeleton (bones matched by name); other models attach to the slot's bone.</summary>
    Auto,
    /// <summary>The model's skinned meshes are bound to the character's bones (clothes and armour made for the same skeleton).</summary>
    Skinned,
    /// <summary>The model is attached to one bone (helmets, pauldrons) - or one per side (Pair).</summary>
    Rigid,
    /// <summary>No model on the character.</summary>
    None,
}

/// <summary>Rigid armour pieces worn on both sides (gloves, boots, bracers, pauldrons). Serialized as numbers: append only.</summary>
public enum ArmorPairMode
{
    /// <summary>Gloves, boots, bracers and shoulders are pairs; the rest single.</summary>
    Auto,
    Single,
    /// <summary>One model on the left bone and a mirrored copy on the right bone.</summary>
    Pair,
}

/// <summary>
/// How a weapon, shield or held item shows on the character: its model, the pose in the off hand, and where it is
/// sheathed when not in use. On every item (Equipment Visuals); used by <see cref="EquipmentVisuals"/>.
/// </summary>
[Serializable]
public class ItemVisualSettings
{
    [Tooltip("Model shown on the character (in hand and sheathed). Empty = the item's Prefab.")]
    public GameObject model;
    [Tooltip("Where it goes when not in use. Auto: one-handed blades on the left hip, daggers on the right hip, big weapons, " +
             "bows and shields on the back; other items stay in hand. Stay In Hand: never sheathed. Hidden: disappears.")]
    public SheathPoint sheath = SheathPoint.Auto;
    [Tooltip("Custom sheath: the name of a child transform of the character (e.g. a 'QuiverSocket' you placed).")]
    public string customSocket = "";
    [Tooltip("Offset at the sheath point (metres, in the socket's space). Zero with Auto = a default for that point.")]
    public Vector3 sheathPosition;
    [Tooltip("Rotation at the sheath point (degrees). Zero with Auto = a default angle for that point.")]
    public Vector3 sheathRotation;
    [Tooltip("Use the pose below in the LEFT hand (off hand). Off = the item's hand pose (Position / Rotation) mirrored.")]
    public bool customOffHandPose = false;
    public Vector3 offHandPosition;
    public Vector3 offHandRotation = new Vector3(80f, 20f, 0f);
    [Tooltip("Shields in the off hand are held on the left forearm (off = in the left hand like a weapon).")]
    public bool shieldOnForearm = true;

    /// <summary>The model to show for <paramref name="item"/>.</summary>
    public GameObject ModelFor(ItemSO item) => model != null ? model : item != null ? item.Prefab : null;
}

/// <summary>How an armour piece's model is worn (Armor ▸ Armor Visuals); used by <see cref="EquipmentVisuals"/>.</summary>
[Serializable]
public class ArmorVisualSettings
{
    [Tooltip("Auto: skinned meshes are bound to the character's skeleton (bones matched by name); other models attach to the " +
             "slot's bone. Skinned / Rigid force one way. None: nothing is shown.")]
    public ArmorAttachMode attach = ArmorAttachMode.Auto;
    [Tooltip("Rigid models: the bone (by name) they attach to. Empty = the slot's point: Helmet → Head, Chestplate → Chest, " +
             "Boots → feet, Gloves → hands, Belt → Hips, Cloak → Back...")]
    public string bone = "";
    [Tooltip("Rigid models: one on each side (the second mirrored). Auto: gloves, boots, bracers and shoulders.")]
    public ArmorPairMode pair = ArmorPairMode.Auto;
    [Tooltip("Rigid models: offset from the bone (metres).")]
    public Vector3 position;
    [Tooltip("Rigid models: rotation from the bone (degrees).")]
    public Vector3 rotation;
    [Tooltip("Rigid models: scale.")]
    public Vector3 scale = Vector3.one;
    [Tooltip("Character renderers hidden while the piece is worn, by object name (hair under a helmet, the bare torso under a chestplate).")]
    public List<string> hideCharacterParts = new List<string>();

    /// <summary>Is the piece worn as a pair on this slot?</summary>
    public bool IsPair(ArmorSlotType slot)
    {
        switch (pair)
        {
            case ArmorPairMode.Pair: return true;
            case ArmorPairMode.Single: return false;
            default: return slot == ArmorSlotType.Gloves || slot == ArmorSlotType.Boots || slot == ArmorSlotType.Bracers || slot == ArmorSlotType.Shoulders;
        }
    }
}
