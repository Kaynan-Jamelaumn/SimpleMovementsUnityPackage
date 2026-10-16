using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Ear shape a race gives (placeholder tips on the head, or your own ear prefab / blend shape).</summary>
public enum EarShape
{
    Human,
    Pointed,
    LongPointed,
}

/// <summary>A blend shape set on every skinned mesh of the model that has it ("Muscular", "Ears_Pointed"...).</summary>
[Serializable]
public class NamedBlendShape
{
    [Tooltip("Blend shape name, as in the mesh.")]
    public string shape;
    [Range(0f, 100f)] public float weight = 100f;
}

/// <summary>A prefab attached to a bone of the model (elf ears, horns, tusks, a tail...).</summary>
[Serializable]
public class AppearanceAttachment
{
    public GameObject prefab;
    public HumanBodyBones bone = HumanBodyBones.Head;
    public Vector3 position;
    public Vector3 rotation;
    public Vector3 scale = Vector3.one;
}

/// <summary>
/// How a race (or a class, a background) looks, on top of the player's own choices: build (width), ears, beard rules,
/// skin tones, blend shapes and attached parts. Height comes from the archetype's Height Range. Examples:
/// <list type="bullet">
/// <item>Dwarf: height 1.2-1.45 m, Body Width 1.2, Always Beard, Default Beard "Long Beard".</item>
/// <item>Elf: height 1.75-2.0 m, Body Width 0.92, Ears Pointed (or an ear prefab / an "Ears_Pointed" blend shape).</item>
/// <item>Barbarian: height 1.85-2.1 m, Body Width 1.18, blend shape "Muscular" 100.</item>
/// </list>
/// </summary>
[Serializable]
public class RaceAppearance
{
    [Tooltip("Width and depth of the body on top of the height (1 = as modelled, 1.15 = broad and muscular, 0.9 = slender).")]
    [Range(0.6f, 1.6f)] public float bodyWidth = 1f;
    [Tooltip("Ear shape. Pointed / Long Pointed add placeholder tips unless Attachments or Blend Shapes give real ears.")]
    public EarShape ears = EarShape.Human;
    [Tooltip("Every character of this race has a beard (No beard is not offered).")]
    public bool alwaysBeard;
    [Tooltip("Female characters can have a beard too.")]
    public bool beardOnAnyBody;
    [Tooltip("Beard style picked for a new character of this race (index in the beard list, -1 = keep the player's choice).")]
    public int defaultBeard = -1;
    [Tooltip("Hairstyle picked for a new character of this race (-1 = keep the player's choice).")]
    public int defaultHair = -1;
    [Tooltip("Skin colours offered for this race (empty = the usual palette). Green for orcs, grey for dark elves...")]
    public Color[] skinTones = new Color[0];
    [Tooltip("Blend shapes set on the model for this race (Muscular, Ears_Pointed...).")]
    public List<NamedBlendShape> blendShapes = new List<NamedBlendShape>();
    [Tooltip("Prefabs attached to bones for this race (ear meshes, horns, tusks...).")]
    public List<AppearanceAttachment> attachments = new List<AppearanceAttachment>();

    public bool IsDefault =>
        Mathf.Approximately(bodyWidth, 1f) && ears == EarShape.Human && !alwaysBeard && !beardOnAnyBody && defaultBeard < 0 && defaultHair < 0 &&
        (skinTones == null || skinTones.Length == 0) && (blendShapes == null || blendShapes.Count == 0) && (attachments == null || attachments.Count == 0);

    /// <summary>A short description for the creation screen.</summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (bodyWidth > 1.03f) parts.Add(bodyWidth > 1.12f ? "broad, heavy build" : "sturdy build");
        else if (bodyWidth < 0.97f) parts.Add("slender build");
        if (ears == EarShape.Pointed) parts.Add("pointed ears");
        if (ears == EarShape.LongPointed) parts.Add("long pointed ears");
        if (alwaysBeard) parts.Add(beardOnAnyBody ? "everyone is bearded" : "always bearded");
        else if (beardOnAnyBody) parts.Add("beards for everyone");
        if (skinTones != null && skinTones.Length > 0) parts.Add("own skin tones");
        return string.Join(", ", parts);
    }
}

/// <summary>The appearance rules of a character: its race's and class's <see cref="RaceAppearance"/> combined.</summary>
public class AppearanceRules
{
    public float bodyWidth = 1f;
    public EarShape ears = EarShape.Human;
    public bool alwaysBeard;
    public bool beardOnAnyBody;
    public int defaultBeard = -1;
    public int defaultHair = -1;
    public Color[] skinTones = new Color[0];
    public readonly List<NamedBlendShape> blendShapes = new List<NamedBlendShape>();
    public readonly List<AppearanceAttachment> attachments = new List<AppearanceAttachment>();

    public static readonly AppearanceRules None = new AppearanceRules();

    /// <summary>The rules of these archetypes (widths multiply; the race's palette and defaults win).</summary>
    public static AppearanceRules From(IList<CharacterArchetype> archetypes)
    {
        var r = new AppearanceRules();
        if (archetypes == null)
            return r;
        foreach (CharacterArchetype a in archetypes)
        {
            RaceAppearance ap = a != null ? a.appearance : null;
            if (ap == null)
                continue;
            r.bodyWidth *= Mathf.Clamp(ap.bodyWidth, 0.6f, 1.6f);
            if (ap.ears > r.ears) r.ears = ap.ears;
            r.alwaysBeard |= ap.alwaysBeard;
            r.beardOnAnyBody |= ap.beardOnAnyBody;
            if (r.defaultBeard < 0) r.defaultBeard = ap.defaultBeard;
            if (r.defaultHair < 0) r.defaultHair = ap.defaultHair;
            if ((r.skinTones == null || r.skinTones.Length == 0) && ap.skinTones != null && ap.skinTones.Length > 0) r.skinTones = ap.skinTones;
            if (ap.blendShapes != null) r.blendShapes.AddRange(ap.blendShapes.FindAll(b => b != null && !string.IsNullOrEmpty(b.shape)));
            if (ap.attachments != null) r.attachments.AddRange(ap.attachments.FindAll(x => x != null && x.prefab != null));
        }
        r.bodyWidth = Mathf.Clamp(r.bodyWidth, 0.6f, 1.6f);
        return r;
    }

    /// <summary>The skin colours to offer.</summary>
    public Color[] SkinPalette => skinTones != null && skinTones.Length > 0 ? skinTones : CharacterAppearanceData.SkinTones;

    /// <summary>Real ears are set up (an attachment or a blend shape): no placeholder tips.</summary>
    public bool HasRealEars => attachments.Count > 0 || blendShapes.Exists(b => b.shape.ToLowerInvariant().Contains("ear"));
}
