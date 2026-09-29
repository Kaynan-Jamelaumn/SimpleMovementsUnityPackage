using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ready-made traits: passive bonuses, drawbacks (negative cost), active movement skills (double jump, wall climb,
/// glide), abilities on a key (barrier, blink), reactions (second wind, vampiric, undying, thorns, on-kill rewards,
/// out-of-combat regeneration) and situational bonuses (last stand, battle focus, mana surge).
/// Used by Assets ▸ Create ▸ Scriptable Objects ▸ Trait From Preset... and the Trait / Trait Database inspectors.
/// </summary>
public static class TraitPresets
{
    public struct Preset
    {
        public string name;
        public string category;
        public string description;
        public Func<Trait> create;
    }

    private static readonly Color Good = new Color(0.45f, 0.8f, 0.45f);
    private static readonly Color Bad = new Color(0.9f, 0.4f, 0.35f);
    private static readonly Color ActiveColor = new Color(0.4f, 0.65f, 1f);
    private static readonly Color Triggered = new Color(0.75f, 0.5f, 0.95f);

    public static readonly Preset[] All =
    {
        // Passive bonuses
        P("Passive", "Tough", "+20% max health.", () => New("Tough", "Built to take a beating.", 2, TraitType.Physical, Good,
            TraitModifier.Percent(TraitStat.MaxHealth, 20f))),
        P("Passive", "Iron Skin", "-15% damage taken, -5% move speed.", () => New("Iron Skin", "Hardened skin shrugs off blows, at the cost of agility.", 3, TraitType.Physical, Good,
            TraitModifier.Percent(TraitStat.DamageTaken, -15f), TraitModifier.Percent(TraitStat.MoveSpeed, -5f))),
        P("Passive", "Swift", "+10% move speed, +10% sprint speed.", () => New("Swift", "Light on your feet.", 2, TraitType.Movement, Good,
            TraitModifier.Percent(TraitStat.MoveSpeed, 10f), TraitModifier.Percent(TraitStat.SprintSpeed, 10f))),
        P("Passive", "Marathon Runner", "+30% max stamina, -20% stamina cost.", () => New("Marathon Runner", "Runs for miles without tiring.", 2, TraitType.Survival, Good,
            TraitModifier.Percent(TraitStat.MaxStamina, 30f), TraitModifier.Percent(TraitStat.StaminaCost, -20f))),
        P("Passive", "Quick Healer", "+25% healing received, +1 health regen.", () => New("Quick Healer", "Wounds close faster than they should.", 2, TraitType.Survival, Good,
            TraitModifier.Percent(TraitStat.HealingReceived, 25f), TraitModifier.Flat(TraitStat.HealthRegen, 1f))),
        P("Passive", "Arcane Mind", "+25% max mana, +1 mana regen, +10% ability damage.", () => New("Arcane Mind", "Magic comes naturally.", 3, TraitType.Magic, Good,
            TraitModifier.Percent(TraitStat.MaxMana, 25f), TraitModifier.Flat(TraitStat.ManaRegen, 1f), TraitModifier.Percent(TraitStat.AbilityDamage, 10f))),
        P("Passive", "Focused Caster", "-15% ability cooldowns, -10% cast time.", () => New("Focused Caster", "Spells come quicker.", 3, TraitType.Magic, Good,
            TraitModifier.Percent(TraitStat.AbilityCooldown, -15f), TraitModifier.Percent(TraitStat.AbilityCastTime, -10f))),
        P("Passive", "Brawler", "+15% weapon damage.", () => New("Brawler", "Hits harder in close combat.", 2, TraitType.Combat, Good,
            TraitModifier.Percent(TraitStat.WeaponDamage, 15f))),
        P("Passive", "Pack Mule", "+30 carry weight.", () => New("Pack Mule", "Carries more without slowing down.", 1, TraitType.Physical, Good,
            TraitModifier.Flat(TraitStat.CarryWeight, 30f))),
        P("Passive", "Tenacious", "-30% stun/slow/root duration.", () => New("Tenacious", "Shakes off stuns and slows quickly.", 2, TraitType.Mental, Good,
            TraitModifier.Percent(TraitStat.ControlTaken, -30f))),
        P("Passive", "Springy Legs", "+20% jump force.", () => New("Springy Legs", "Jumps higher than most.", 1, TraitType.Movement, Good,
            TraitModifier.Percent(TraitStat.JumpForce, 20f))),
        P("Passive", "Glass Cannon", "+25% ability and weapon damage, -25% max health.", () => New("Glass Cannon", "Hits hard, breaks easily.", 1, TraitType.Combat, Good,
            TraitModifier.Percent(TraitStat.AbilityDamage, 25f), TraitModifier.Percent(TraitStat.WeaponDamage, 25f), TraitModifier.Percent(TraitStat.MaxHealth, -25f))),

        // Drawbacks (give points back)
        P("Drawback", "Frail", "-15% max health. Gives 2 points.", () => New("Frail", "A delicate constitution.", -2, TraitType.Physical, Bad,
            TraitModifier.Percent(TraitStat.MaxHealth, -15f))),
        P("Drawback", "Sluggish", "-10% move speed. Gives 2 points.", () => New("Sluggish", "Never in a hurry.", -2, TraitType.Movement, Bad,
            TraitModifier.Percent(TraitStat.MoveSpeed, -10f))),
        P("Drawback", "Glass Jaw", "+20% damage taken, +30% stun duration. Gives 3 points.", () => New("Glass Jaw", "Goes down easily.", -3, TraitType.Physical, Bad,
            TraitModifier.Percent(TraitStat.DamageTaken, 20f), TraitModifier.Percent(TraitStat.ControlTaken, 30f))),
        P("Drawback", "Short Winded", "-20% max stamina, +20% stamina cost. Gives 2 points.", () => New("Short Winded", "Tires quickly.", -2, TraitType.Survival, Bad,
            TraitModifier.Percent(TraitStat.MaxStamina, -20f), TraitModifier.Percent(TraitStat.StaminaCost, 20f))),

        // Active movement
        P("Active", "Double Jump", "Jump again in the air.", () => New("Double Jump", "Push off thin air for a second jump.", 3, TraitType.Movement, ActiveColor, new DoubleJumpTrait())),
        P("Active", "Wall Climber", "Climb walls by holding Jump while facing them.", () => New("Wall Climber", "Scales walls with ease.", 3, TraitType.Movement, ActiveColor, new WallClimbTrait())),
        P("Active", "Glider", "Fall slowly while holding Jump.", () => New("Glider", "Drifts down gently from any height.", 2, TraitType.Movement, ActiveColor, new GlideTrait())),
        P("Active", "Guardian Barrier", "An ability on a key: a stone ring around you + short invulnerability. Assign its input.", () => New("Guardian Barrier", "Call up a protective ring of stone.", 4, TraitType.Magic, ActiveColor,
            new ActiveAbilityTrait { ability = AbilityPresets.GuardianBarrier() })),
        P("Active", "Blink", "An ability on a key: teleport a short distance. Assign its input.", () => New("Blink", "Step through space.", 4, TraitType.Magic, ActiveColor,
            new ActiveAbilityTrait { ability = AbilityPresets.Blink() })),

        // Triggered & situational
        P("Triggered", "Second Wind", "Below 25% health: heal 30% and 1s invulnerability (90s cooldown).", () => New("Second Wind", "Refuses to go down.", 3, TraitType.Survival, Triggered, new SecondWindTrait())),
        P("Triggered", "Vampiric", "Abilities heal you for 8% of the damage they deal.", () => New("Vampiric", "Feeds on the pain of others.", 3, TraitType.Magic, Triggered, new LifeStealTrait())),
        P("Situational", "Last Stand", "Below 30% health: -40% damage taken, +20% damage.", () => New("Last Stand", "Most dangerous when cornered.", 2, TraitType.Combat, Triggered,
            new ConditionalModifiersTrait
            {
                condition = ConditionalModifiersTrait.Condition.HealthBelow, threshold = 0.3f,
                modifiers = new List<TraitModifier> { TraitModifier.Percent(TraitStat.DamageTaken, -40f), TraitModifier.Percent(TraitStat.AbilityDamage, 20f), TraitModifier.Percent(TraitStat.WeaponDamage, 20f) },
            })),
        P("Situational", "Adrenaline", "Below 50% health: +15% move speed.", () => New("Adrenaline", "Danger makes you faster.", 1, TraitType.Movement, Triggered,
            new ConditionalModifiersTrait
            {
                condition = ConditionalModifiersTrait.Condition.HealthBelow, threshold = 0.5f,
                modifiers = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MoveSpeed, 15f) },
            })),
        P("Situational", "Sky Hunter", "In the air: +25% ability damage.", () => New("Sky Hunter", "Strikes hardest from above.", 2, TraitType.Combat, Triggered,
            new ConditionalModifiersTrait
            {
                condition = ConditionalModifiersTrait.Condition.InTheAir,
                modifiers = new List<TraitModifier> { TraitModifier.Percent(TraitStat.AbilityDamage, 25f) },
            })),

        // Ability passives (new stats)
        P("Passive", "Efficient Caster", "-20% ability cost.", () => New("Efficient Caster", "Gets more out of every drop of mana.", 2, TraitType.Magic, Good,
            TraitModifier.Percent(TraitStat.AbilityCost, -20f))),
        P("Passive", "Lingering Magic", "+25% ability duration.", () => New("Lingering Magic", "Your spells linger longer.", 2, TraitType.Magic, Good,
            TraitModifier.Percent(TraitStat.AbilityDuration, 25f))),
        P("Passive", "Deep Reserves", "+1 charge for every ability.", () => New("Deep Reserves", "One more use before the cooldown.", 4, TraitType.Magic, Good,
            TraitModifier.Flat(TraitStat.AbilityCharges, 1f))),
        P("Passive", "Multishot", "+1 projectile per volley, -15% ability damage.", () => New("Multishot", "Fires an extra projectile, each a little weaker.", 4, TraitType.Combat, Good,
            TraitModifier.Flat(TraitStat.ExtraProjectiles, 1f), TraitModifier.Percent(TraitStat.AbilityDamage, -15f))),
        P("Passive", "Sharpshooter", "+30% projectile speed, +10% ability range.", () => New("Sharpshooter", "Shots fly fast and far.", 2, TraitType.Combat, Good,
            TraitModifier.Percent(TraitStat.ProjectileSpeed, 30f), TraitModifier.Percent(TraitStat.AbilityRange, 10f))),

        // Reactions (new behaviours)
        P("Triggered", "Undying", "Survive a killing blow with 20% health and 2s invulnerability (every 5 minutes).", () => New("Undying", "Death will have to wait.", 4, TraitType.Survival, Triggered, new CheatDeathTrait())),
        P("Triggered", "Thorny Hide", "Enemies that hit you in melee take 20% of the damage back.", () => New("Thorny Hide", "Hitting you hurts.", 2, TraitType.Physical, Triggered, new ThornsTrait())),
        P("Triggered", "Resilient", "After 6s out of combat, recover 2% health per second.", () => New("Resilient", "Bounces back between fights.", 2, TraitType.Survival, Triggered, new OutOfCombatRegenTrait())),
        P("Triggered", "Bloodthirsty", "Each kill heals 5% health and restores 10 stamina.", () => New("Bloodthirsty", "Every victory feeds you.", 2, TraitType.Combat, Triggered, new OnKillRewardsTrait())),
        P("Triggered", "Momentum", "Each kill takes 1.5s off every ability cooldown.", () => New("Momentum", "One fight flows into the next.", 3, TraitType.Magic, Triggered,
            new OnKillRewardsTrait { healPercent = 0f, stamina = 0f, cooldownReduction = 1.5f })),

        // Situational (new conditions)
        P("Situational", "Battle Focus", "In combat: +10% ability damage, -10% ability cooldowns.", () => New("Battle Focus", "Sharpens in the heat of battle.", 2, TraitType.Mental, Triggered,
            new ConditionalModifiersTrait
            {
                condition = ConditionalModifiersTrait.Condition.InCombat,
                modifiers = new List<TraitModifier> { TraitModifier.Percent(TraitStat.AbilityDamage, 10f), TraitModifier.Percent(TraitStat.AbilityCooldown, -10f) },
            })),
        P("Situational", "Mana Surge", "Below 25% mana: -30% ability cost.", () => New("Mana Surge", "Scrapes the last drops of power together.", 1, TraitType.Magic, Triggered,
            new ConditionalModifiersTrait
            {
                condition = ConditionalModifiersTrait.Condition.ManaBelow, threshold = 0.25f,
                modifiers = new List<TraitModifier> { TraitModifier.Percent(TraitStat.AbilityCost, -30f) },
            })),
        P("Situational", "Steady Caster", "While casting: -25% damage taken.", () => New("Steady Caster", "Braces while channelling power.", 2, TraitType.Mental, Triggered,
            new ConditionalModifiersTrait
            {
                condition = ConditionalModifiersTrait.Condition.Casting,
                modifiers = new List<TraitModifier> { TraitModifier.Percent(TraitStat.DamageTaken, -25f) },
            })),
    };

    private static Preset P(string category, string name, string description, Func<Trait> create) =>
        new Preset { category = category, name = name, description = description, create = create };

    private static Trait New(string name, string description, int cost, TraitType type, Color color, params object[] parts)
    {
        Trait t = ScriptableObject.CreateInstance<Trait>();
        t.name = name;
        t.traitName = name;
        t.description = description;
        t.cost = cost;
        t.type = type;
        t.traitColor = color;
        t.rarity = Mathf.Abs(cost) >= 4 ? TraitRarity.Rare : Mathf.Abs(cost) >= 3 ? TraitRarity.Uncommon : TraitRarity.Common;
        foreach (object p in parts)
        {
            if (p is TraitModifier m) t.modifiers.Add(m);
            else if (p is TraitBehaviour b) t.behaviours.Add(b);
        }
        return t;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Creates a preset as a Trait asset in <paramref name="folder"/>. Abilities the preset creates (barrier, blink)
    /// are saved as Ability Definition assets next to it first. Returns the saved trait.
    /// </summary>
    public static Trait CreateAsset(Preset preset, string folder)
    {
        Trait t = preset.create();
        SaveRuntimeAbilities(t, folder);
        string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath($"{folder}/{t.name}.asset");
        UnityEditor.AssetDatabase.CreateAsset(t, path);
        UnityEditor.AssetDatabase.SaveAssets();
        return t;
    }

    /// <summary>Saves any not-yet-saved ability of the trait's Active Ability behaviours as assets (so the trait can reference them).</summary>
    public static void SaveRuntimeAbilities(Trait t, string folder)
    {
        foreach (TraitBehaviour b in t.behaviours)
        {
            if (!(b is ActiveAbilityTrait a) || a.ability == null || UnityEditor.EditorUtility.IsPersistent(a.ability))
                continue;
            string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath($"{folder}/{a.ability.DisplayName} (Trait Ability).asset");
            UnityEditor.AssetDatabase.CreateAsset(a.ability, path);
            a.ability.SetId(UnityEditor.AssetDatabase.AssetPathToGUID(path));
            UnityEditor.EditorUtility.SetDirty(a.ability);
        }
    }
#endif

    /// <summary>Fills an existing trait from a preset (keeps its asset and name).</summary>
    public static void Apply(Trait target, Preset preset)
    {
        Trait src = preset.create();
        string keepName = target.traitName;
        target.description = src.description;
        target.cost = src.cost;
        target.type = src.type;
        target.rarity = src.rarity;
        target.traitColor = src.traitColor;
        target.modifiers = src.modifiers;
        target.behaviours = src.behaviours;
        if (string.IsNullOrEmpty(keepName))
            target.traitName = src.traitName;
        UnityEngine.Object.DestroyImmediate(src);
    }
}
