using System;
using UnityEngine;

/// <summary>
/// The physical shape of a hit: what the attack is meant to do to a body. A sword's swing slashes, a spear thrust
/// pierces, a mace bludgeons, a wolf bites, a bear claws, a troll's stomp crushes. Independent of the
/// <see cref="DamageType"/> (how it is resisted) and the <see cref="ElementType"/> (fire, ice...): a flaming sword
/// slashes AND burns. Injuries read it (slashes bleed, blunt blows break bones), and characters can resist each form
/// (<see cref="InjuryResistances"/>, the Slash / Pierce / Blunt Damage Taken character stats).
/// Serialized as numbers: only add new forms at the END.
/// </summary>
public enum DamageForm
{
    /// <summary>Not said: a weapon's category decides (swords slash, maces are blunt), else formless (magic, elements, poison).</summary>
    None,
    /// <summary>A cutting edge: swords, axes, a sweeping blade. Bleeds, severs limbs.</summary>
    Slash,
    /// <summary>A point: spears, daggers' stabs, arrows, bolts, horns, stingers. Deep punctures that get infected.</summary>
    Pierce,
    /// <summary>A blunt impact: maces, hammers, staves, fists, shields, a push or a kick. Bruises, breaks bones, concussions.</summary>
    Blunt,
    /// <summary>Teeth: torn, ragged wounds that bleed and often get infected.</summary>
    Bite,
    /// <summary>Claws and talons: several shallow cuts (scratches and lacerations).</summary>
    Claw,
    /// <summary>A heavy weight: a giant's stomp, a falling rock, a crushing grip, a hard fall. Breaks bones, crushes hands and feet.</summary>
    Crush,
}

/// <summary>Several <see cref="DamageForm"/>s, for filters (resistances, conditions).</summary>
[Flags]
public enum DamageFormMask
{
    None = 0,
    /// <summary>Hits without a form (magic, elements, falls without a form, older scripts).</summary>
    Formless = 1 << 0,
    Slash = 1 << 1,
    Pierce = 1 << 2,
    Blunt = 1 << 3,
    Bite = 1 << 4,
    Claw = 1 << 5,
    Crush = 1 << 6,
    /// <summary>Every hit with a form (not the formless ones).</summary>
    Physical = Slash | Pierce | Blunt | Bite | Claw | Crush,
    All = Formless | Physical,
}

/// <summary>A chance multiplier for each <see cref="DamageForm"/> (1 = unchanged, 0 = never, 2 = twice as likely).</summary>
[Serializable]
public class DamageFormChances
{
    [Tooltip("Hits without a form: weapons without a category, abilities and scripts that set none (unknown physical hits: 1 keeps " +
             "the usual chance), spells and elements.")]
    [Min(0f)] public float formless = 1f;
    [Tooltip("Swords, axes, blades.")]
    [Min(0f)] public float slash = 1f;
    [Tooltip("Spears, stabs, arrows, bolts, horns.")]
    [Min(0f)] public float pierce = 1f;
    [Tooltip("Maces, hammers, staves, fists, shields, pushes and kicks.")]
    [Min(0f)] public float blunt = 1f;
    [Tooltip("Teeth.")]
    [Min(0f)] public float bite = 1f;
    [Tooltip("Claws and talons.")]
    [Min(0f)] public float claw = 1f;
    [Tooltip("Stomps, falling rocks, crushing grips, hard falls.")]
    [Min(0f)] public float crush = 1f;

    public DamageFormChances() { }

    public DamageFormChances(float formless, float slash, float pierce, float blunt, float bite, float claw, float crush)
    {
        this.formless = formless;
        this.slash = slash;
        this.pierce = pierce;
        this.blunt = blunt;
        this.bite = bite;
        this.claw = claw;
        this.crush = crush;
    }

    /// <summary>The multiplier of <paramref name="form"/>.</summary>
    public float For(DamageForm form)
    {
        switch (form)
        {
            case DamageForm.Slash: return slash;
            case DamageForm.Pierce: return pierce;
            case DamageForm.Blunt: return blunt;
            case DamageForm.Bite: return bite;
            case DamageForm.Claw: return claw;
            case DamageForm.Crush: return crush;
            default: return formless;
        }
    }

    /// <summary>True when every form is 1 (it changes nothing).</summary>
    public bool IsNeutral =>
        Mathf.Approximately(formless, 1f) && Mathf.Approximately(slash, 1f) && Mathf.Approximately(pierce, 1f) && Mathf.Approximately(blunt, 1f) &&
        Mathf.Approximately(bite, 1f) && Mathf.Approximately(claw, 1f) && Mathf.Approximately(crush, 1f);

    /// <summary>True when every form is 0 (nothing ever passes).</summary>
    public bool IsZero => formless <= 0f && slash <= 0f && pierce <= 0f && blunt <= 0f && bite <= 0f && claw <= 0f && crush <= 0f;

    /// <summary>"Slash ×1.5, Blunt ×0.2" (only the forms that are not 1).</summary>
    public string Describe()
    {
        var parts = new System.Collections.Generic.List<string>();
        void Add(string n, float v) { if (!Mathf.Approximately(v, 1f)) parts.Add($"{n} ×{v:0.##}"); }
        Add("Formless", formless);
        Add("Slash", slash);
        Add("Pierce", pierce);
        Add("Blunt", blunt);
        Add("Bite", bite);
        Add("Claw", claw);
        Add("Crush", crush);
        return parts.Count == 0 ? "the same for every form" : string.Join(", ", parts);
    }

    public DamageFormChances Clone() => (DamageFormChances)MemberwiseClone();
}

/// <summary>Helpers for <see cref="DamageForm"/>: the form of a weapon category, of a hit, families, masks and names.</summary>
public static class DamageForms
{
    /// <summary>The forms in order (without None).</summary>
    public static readonly DamageForm[] All =
    {
        DamageForm.Slash, DamageForm.Pierce, DamageForm.Blunt, DamageForm.Bite, DamageForm.Claw, DamageForm.Crush,
    };

    /// <summary>
    /// The usual form of a weapon category: swords and axes slash, spears, daggers, bows and thrown weapons pierce, maces,
    /// hammers, staves, fists and shields are blunt. Wands, tools and Other have none (set it on the weapon).
    /// </summary>
    public static DamageForm ForCategory(WeaponCategory category)
    {
        switch (category)
        {
            case WeaponCategory.Sword:
            case WeaponCategory.Greatsword:
            case WeaponCategory.Axe:
                return DamageForm.Slash;
            case WeaponCategory.Spear:
            case WeaponCategory.Dagger:
            case WeaponCategory.Bow:
            case WeaponCategory.Crossbow:
            case WeaponCategory.Thrown:
                return DamageForm.Pierce;
            case WeaponCategory.Mace:
            case WeaponCategory.Hammer:
            case WeaponCategory.Staff:
            case WeaponCategory.Fist:
            case WeaponCategory.Shield:
                return DamageForm.Blunt;
            default:
                return DamageForm.None;
        }
    }

    /// <summary>
    /// The form of a hit: the one it says, else its weapon's (the weapon's own setting, else its category's), else None.
    /// </summary>
    public static DamageForm Resolve(in DamageInfo info)
    {
        if (info.form != DamageForm.None)
            return info.form;
        return info.weapon != null ? info.weapon.DamageForm : DamageForm.None;
    }

    /// <summary>
    /// The family a form is resisted as: claws are cuts (Slash), bites are punctures (Pierce), crushing is Blunt.
    /// The three Damage Taken character stats resist the families.
    /// </summary>
    public static DamageForm Family(DamageForm form)
    {
        switch (form)
        {
            case DamageForm.Claw: return DamageForm.Slash;
            case DamageForm.Bite: return DamageForm.Pierce;
            case DamageForm.Crush: return DamageForm.Blunt;
            default: return form;
        }
    }

    /// <summary>The flag of a form (None = Formless).</summary>
    public static DamageFormMask MaskOf(DamageForm form)
    {
        switch (form)
        {
            case DamageForm.Slash: return DamageFormMask.Slash;
            case DamageForm.Pierce: return DamageFormMask.Pierce;
            case DamageForm.Blunt: return DamageFormMask.Blunt;
            case DamageForm.Bite: return DamageFormMask.Bite;
            case DamageForm.Claw: return DamageFormMask.Claw;
            case DamageForm.Crush: return DamageFormMask.Crush;
            default: return DamageFormMask.Formless;
        }
    }

    /// <summary>Does <paramref name="mask"/> include <paramref name="form"/>?</summary>
    public static bool Contains(DamageFormMask mask, DamageForm form) => (mask & MaskOf(form)) != 0;

    /// <summary>"slash", "pierce"... ("" for None).</summary>
    public static string Word(DamageForm form) => form == DamageForm.None ? "" : form.ToString().ToLowerInvariant();

    /// <summary>"slashing", "piercing", "blunt", "biting", "clawing", "crushing" ("" for None).</summary>
    public static string Adjective(DamageForm form)
    {
        switch (form)
        {
            case DamageForm.Slash: return "slashing";
            case DamageForm.Pierce: return "piercing";
            case DamageForm.Blunt: return "blunt";
            case DamageForm.Bite: return "biting";
            case DamageForm.Claw: return "clawing";
            case DamageForm.Crush: return "crushing";
            default: return "";
        }
    }

    /// <summary>"Slash, Blunt" / "every form" / "formless hits".</summary>
    public static string Describe(DamageFormMask mask)
    {
        if ((mask & DamageFormMask.All) == DamageFormMask.All)
            return "every hit";
        if ((mask & DamageFormMask.Physical) == DamageFormMask.Physical && (mask & DamageFormMask.Formless) == 0)
            return "every physical form";
        if (mask == DamageFormMask.None)
            return "nothing";
        var parts = new System.Collections.Generic.List<string>();
        if ((mask & DamageFormMask.Formless) != 0) parts.Add("formless");
        foreach (DamageForm f in All)
            if ((mask & MaskOf(f)) != 0)
                parts.Add(Word(f));
        return string.Join(", ", parts);
    }
}
