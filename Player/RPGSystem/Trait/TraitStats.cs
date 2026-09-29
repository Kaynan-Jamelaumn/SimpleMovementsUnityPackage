using System;
using UnityEngine;

/// <summary>What a trait modifier changes.</summary>
public enum TraitStat
{
    // Health
    MaxHealth,
    HealthRegen,
    HealingReceived,
    DamageTaken,
    // Stamina & mana
    MaxStamina,
    StaminaRegen,
    StaminaCost,
    MaxMana,
    ManaRegen,
    // Movement
    MoveSpeed,
    SprintSpeed,
    CrouchSpeed,
    JumpForce,
    CarryWeight,
    // Combat
    WeaponDamage,
    AbilityDamage,
    AbilityHealing,
    AbilityCooldown,
    AbilityCastTime,
    AbilityArea,
    AbilityRange,
    ControlDealt,
    ControlTaken,
    // Added later (kept at the end so existing assets keep their values)
    AbilityCost,
    AbilityDuration,
    ProjectileSpeed,
    AbilityCharges,
    ExtraProjectiles,
}

/// <summary>How a modifier's value is read.</summary>
public enum TraitModifierMode
{
    /// <summary>Adds the value in the stat's own units (+20 max health, +0.5 m/s).</summary>
    Flat,
    /// <summary>Changes the stat by a percentage (+20 = 20% more, -15 = 15% less).</summary>
    Percent,
}

/// <summary>One passive change a trait makes, e.g. "+20% Max Health" or "-15% Damage Taken".</summary>
[Serializable]
public class TraitModifier
{
    [Tooltip("What this modifier changes. Hover the value for its units.")]
    public TraitStat stat = TraitStat.MaxHealth;

    [Tooltip("Flat: add the value in the stat's units. Percent: +20 = 20% more, -15 = 15% less. Some stats only support one mode (the inspector shows which).")]
    public TraitModifierMode mode = TraitModifierMode.Percent;

    [Tooltip("Amount. Positive increases the stat, negative decreases it (for Damage Taken, Stamina Cost, cooldowns and cast time, negative is GOOD).")]
    public float value = 10f;

    public TraitModifier() { }

    public TraitModifier(TraitStat stat, TraitModifierMode mode, float value)
    {
        this.stat = stat;
        this.mode = mode;
        this.value = value;
    }

    public static TraitModifier Percent(TraitStat stat, float value) => new TraitModifier(stat, TraitModifierMode.Percent, value);
    public static TraitModifier Flat(TraitStat stat, float value) => new TraitModifier(stat, TraitModifierMode.Flat, value);

    /// <summary>The mode actually used (stats that support only one mode force it).</summary>
    public TraitModifierMode EffectiveMode => TraitStats.Get(stat).EffectiveMode(mode);

    /// <summary>"+20% Max Health", "-15% Damage Taken", "+1.5 Health Regen".</summary>
    public string Describe(float multiplier = 1f)
    {
        TraitStats.StatInfo info = TraitStats.Get(stat);
        float v = value * multiplier;
        string sign = v >= 0f ? "+" : "";
        return EffectiveMode == TraitModifierMode.Percent
            ? $"{sign}{v:0.#}% {info.name}"
            : $"{sign}{v:0.##} {info.name}{(string.IsNullOrEmpty(info.unit) ? "" : " " + info.unit)}";
    }

    /// <summary>True if this modifier helps the character (used to spot mislabelled positive/negative traits).</summary>
    public bool IsBeneficial => TraitStats.Get(stat).higherIsBetter ? value > 0f : value < 0f;
}

/// <summary>Names, units and descriptions of the trait stats (used by the inspector and descriptions).</summary>
public static class TraitStats
{
    public enum Modes { Both, FlatOnly, PercentOnly }

    public readonly struct StatInfo
    {
        public readonly string name;
        public readonly string unit;
        public readonly Modes modes;
        public readonly bool higherIsBetter;
        public readonly string description;

        public StatInfo(string name, string unit, Modes modes, bool higherIsBetter, string description)
        {
            this.name = name;
            this.unit = unit;
            this.modes = modes;
            this.higherIsBetter = higherIsBetter;
            this.description = description;
        }

        public TraitModifierMode EffectiveMode(TraitModifierMode wanted) =>
            modes == Modes.FlatOnly ? TraitModifierMode.Flat : modes == Modes.PercentOnly ? TraitModifierMode.Percent : wanted;
    }

    public static StatInfo Get(TraitStat stat)
    {
        switch (stat)
        {
            case TraitStat.MaxHealth: return new StatInfo("Max Health", "HP", Modes.Both, true, "Maximum health (Health Manager ▸ Max Value).");
            case TraitStat.HealthRegen: return new StatInfo("Health Regen", "HP/tick", Modes.FlatOnly, true, "Health regenerated per regeneration tick.");
            case TraitStat.HealingReceived: return new StatInfo("Healing Received", "", Modes.PercentOnly, true, "All healing the character receives (potions, abilities, regen).");
            case TraitStat.DamageTaken: return new StatInfo("Damage Taken", "", Modes.PercentOnly, false, "All damage the character takes. Use a NEGATIVE value for damage reduction (-15 = takes 15% less).");
            case TraitStat.MaxStamina: return new StatInfo("Max Stamina", "", Modes.Both, true, "Maximum stamina.");
            case TraitStat.StaminaRegen: return new StatInfo("Stamina Regen", "/tick", Modes.FlatOnly, true, "Stamina regenerated per tick.");
            case TraitStat.StaminaCost: return new StatInfo("Stamina Cost", "", Modes.PercentOnly, false, "Stamina spent on sprinting, jumping, dashing, rolling... Negative = cheaper.");
            case TraitStat.MaxMana: return new StatInfo("Max Mana", "", Modes.Both, true, "Maximum mana.");
            case TraitStat.ManaRegen: return new StatInfo("Mana Regen", "/tick", Modes.FlatOnly, true, "Mana regenerated per tick.");
            case TraitStat.MoveSpeed: return new StatInfo("Move Speed", "m/s", Modes.Both, true, "Base movement speed (Speed Manager ▸ Base Speed).");
            case TraitStat.SprintSpeed: return new StatInfo("Sprint Speed", "", Modes.PercentOnly, true, "Speed while running (the running multiplier).");
            case TraitStat.CrouchSpeed: return new StatInfo("Crouch Speed", "", Modes.PercentOnly, true, "Speed while crouching (the crouching multiplier).");
            case TraitStat.JumpForce: return new StatInfo("Jump Force", "", Modes.Both, true, "Jump strength (Player Movement Model ▸ Jump Force).");
            case TraitStat.CarryWeight: return new StatInfo("Carry Weight", "kg", Modes.Both, true, "Maximum carried weight before being slowed.");
            case TraitStat.WeaponDamage: return new StatInfo("Weapon Damage", "", Modes.PercentOnly, true, "Damage of weapon attacks.");
            case TraitStat.AbilityDamage: return new StatInfo("Ability Damage", "", Modes.PercentOnly, true, "Damage of the character's abilities (direct and over time).");
            case TraitStat.AbilityHealing: return new StatInfo("Ability Healing", "", Modes.PercentOnly, true, "Healing done by the character's abilities.");
            case TraitStat.AbilityCooldown: return new StatInfo("Ability Cooldown", "", Modes.PercentOnly, false, "Ability cooldowns. Negative = shorter (-15 = 15% faster).");
            case TraitStat.AbilityCastTime: return new StatInfo("Ability Cast Time", "", Modes.PercentOnly, false, "Ability wind-up time. Negative = faster casting.");
            case TraitStat.AbilityArea: return new StatInfo("Ability Area", "", Modes.PercentOnly, true, "Size of ability hit areas.");
            case TraitStat.AbilityRange: return new StatInfo("Ability Range", "", Modes.PercentOnly, true, "Range of abilities.");
            case TraitStat.ControlDealt: return new StatInfo("Control Duration Dealt", "", Modes.PercentOnly, true, "How long the character's stuns, roots, slows and silences last on others.");
            case TraitStat.ControlTaken: return new StatInfo("Control Duration Taken", "", Modes.PercentOnly, false, "How long stuns, roots, slows and silences last on this character. Negative = shorter (tenacity).");
            case TraitStat.AbilityCost: return new StatInfo("Ability Cost", "", Modes.PercentOnly, false, "Mana, stamina and health the character's abilities cost. Negative = cheaper.");
            case TraitStat.AbilityDuration: return new StatInfo("Ability Duration", "", Modes.PercentOnly, true, "How long the character's zones, walls, buffs and other lasting effects stay.");
            case TraitStat.ProjectileSpeed: return new StatInfo("Projectile Speed", "", Modes.PercentOnly, true, "Speed of the character's projectiles.");
            case TraitStat.AbilityCharges: return new StatInfo("Ability Charges", "charges", Modes.FlatOnly, true, "Extra charges for every ability (+1 = one more use before the cooldown).");
            case TraitStat.ExtraProjectiles: return new StatInfo("Extra Projectiles", "projectiles", Modes.FlatOnly, true, "Extra projectiles per volley for every projectile ability.");
            default: return new StatInfo(stat.ToString(), "", Modes.Both, true, "");
        }
    }

    /// <summary>A sensible starting value for a new modifier of this stat (+1 for counts and regen, +/-10% otherwise).</summary>
    public static float DefaultValue(TraitStat stat)
    {
        StatInfo info = Get(stat);
        if (info.modes == Modes.FlatOnly)
            return 1f;
        return info.higherIsBetter ? 10f : -10f;
    }

    /// <summary>
    /// Stats that are not written into a manager once but combined from every active modifier (ability multipliers,
    /// control duration, weapon damage). Percent ones multiply (+20% and +10% = x1.32); flat ones add up.
    /// </summary>
    public static bool IsAggregate(TraitStat stat)
    {
        switch (stat)
        {
            case TraitStat.WeaponDamage:
            case TraitStat.AbilityDamage:
            case TraitStat.AbilityHealing:
            case TraitStat.AbilityCooldown:
            case TraitStat.AbilityCastTime:
            case TraitStat.AbilityArea:
            case TraitStat.AbilityRange:
            case TraitStat.ControlDealt:
            case TraitStat.ControlTaken:
            case TraitStat.AbilityCost:
            case TraitStat.AbilityDuration:
            case TraitStat.ProjectileSpeed:
            case TraitStat.AbilityCharges:
            case TraitStat.ExtraProjectiles:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// The TraitStat an old string-based TraitEffect targets ("health", "stamina", "speed", "defense"...), or null when
    /// it is handled elsewhere (weapons read "damage", "element_*", "lifesteal"... directly).
    /// </summary>
    public static bool TryMapLegacy(TraitEffect effect, out TraitStat stat, out TraitModifierMode mode, out float value)
    {
        stat = TraitStat.MaxHealth;
        mode = TraitModifierMode.Flat;
        value = 0f;
        if (effect == null || string.IsNullOrEmpty(effect.targetStat))
            return false;
        string key = effect.targetStat.Trim().ToLowerInvariant();
        float v = effect.value;
        switch (effect.effectType)
        {
            case TraitEffectType.StatMultiplier: mode = TraitModifierMode.Percent; value = (v - 1f) * 100f; break;
            case TraitEffectType.StatAddition: mode = TraitModifierMode.Flat; value = v; break;
            case TraitEffectType.RegenerationRate: mode = TraitModifierMode.Flat; value = v; break;
            case TraitEffectType.ConsumptionRate: mode = TraitModifierMode.Percent; value = v; break;
            case TraitEffectType.ResistanceBonus: mode = TraitModifierMode.Percent; value = -v; break;
            default: return false;
        }
        bool regen = effect.effectType == TraitEffectType.RegenerationRate;
        switch (key)
        {
            case "health": case "hp":
                stat = regen ? TraitStat.HealthRegen : TraitStat.MaxHealth; return true;
            case "stamina":
                stat = regen ? TraitStat.StaminaRegen : effect.effectType == TraitEffectType.ConsumptionRate ? TraitStat.StaminaCost : TraitStat.MaxStamina; return true;
            case "mana":
                stat = regen ? TraitStat.ManaRegen : TraitStat.MaxMana; return true;
            case "speed":
                stat = TraitStat.MoveSpeed; return !regen;
            case "defense": case "armor": case "resistance":
                stat = TraitStat.DamageTaken; mode = TraitModifierMode.Percent;
                value = effect.effectType == TraitEffectType.StatMultiplier ? -(v - 1f) * 100f : -Mathf.Abs(v);
                return true;
            case "healing": case "heal":
                stat = TraitStat.HealingReceived; mode = TraitModifierMode.Percent; return !regen;
            case "jump":
                stat = TraitStat.JumpForce; return !regen;
            case "weight": case "carryweight":
                stat = TraitStat.CarryWeight; return !regen;
            case "damage": case "attack":
                // Weapons read these effects themselves; abilities get the multiplier through the trait manager.
                stat = TraitStat.AbilityDamage; mode = TraitModifierMode.Percent;
                return effect.effectType == TraitEffectType.StatMultiplier;
            case "cooldown":
                stat = TraitStat.AbilityCooldown; mode = TraitModifierMode.Percent; return !regen;
            default:
                return false; // "damage", "attack", "element_*", "lifesteal", "aoe", "*on_hit*"... are read by the weapon system
        }
    }

    /// <summary>Legacy target stats that the weapon system reads itself (no warning when the trait manager skips them).</summary>
    public static bool IsWeaponLegacyKey(string targetStat)
    {
        if (string.IsNullOrEmpty(targetStat))
            return false;
        string k = targetStat.ToLowerInvariant();
        return k == "damage" || k == "attack" || k == "lifesteal" || k == "vampiric" || k == "aoe" || k == "explosive" ||
               k.StartsWith("element_") || k.StartsWith("elemental_") || k.Contains("on_hit");
    }
}
