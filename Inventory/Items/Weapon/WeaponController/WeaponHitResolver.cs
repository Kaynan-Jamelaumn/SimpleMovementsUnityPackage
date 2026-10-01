using System.Collections.Generic;
using UnityEngine;

/// <summary>Something a weapon can hit that is not a character: a tree, a rock, a crate, a switch.</summary>
public interface IWeaponHittable
{
    /// <summary>Called once per attack that reaches it. Return true if it reacted (hit effects are played).</summary>
    bool OnWeaponHit(in WeaponHitInfo hit);
}

/// <summary>What hit an <see cref="IWeaponHittable"/>.</summary>
public struct WeaponHitInfo
{
    public WeaponSO weapon;
    public CombatEntity attacker;
    public GameObject attackerObject;
    public Vector3 point;
    public Vector3 direction;
    /// <summary>The damage the attack would deal to a character (before its defenses).</summary>
    public float damage;
}

/// <summary>Interface for elemental components (a target's own element, used by elemental matchups and reactions).</summary>
public interface IElemental
{
    ElementType ElementType { get; }
    float ElementalResistance { get; }
}

/// <summary>
/// The numbers of a weapon hit: damage roll, every multiplier (attack, combo, charge, stats, traits, elements),
/// critical hits, and the classic effects. Shared by the weapon controller and the attack behaviours.
/// </summary>
public static class WeaponHitResolver
{
    /// <summary>Everything that multiplies the weapon damage of one attack, resolved when the attack starts.</summary>
    public struct AttackNumbers
    {
        public float damageMultiplier;   // attack x combo x charge x branch x enhancement
        public float critChanceBonus;    // attack + combo + stats (0-1)
        public float critDamageBonus;    // stats
        public ElementType element;
        public DamageType damageType;
    }

    /// <summary>The weapon's damage of one hit before the target's defenses, and whether it was a critical hit.</summary>
    public static float RollDamage(WeaponSO weapon, in AttackNumbers n, TraitManager traits, CombatStats stats, ElementType targetElement, out bool critical)
    {
        float dmg = Random.Range(weapon.MinDamage, Mathf.Max(weapon.MinDamage, weapon.MaxDamage));

        // Classic string trait effects: weapon traits ("damage" multipliers) and the wielder's ("damage"/"attack").
        dmg = weapon.CalculateTraitModifiedDamage(dmg);
        dmg = ApplyLegacyPlayerDamage(dmg, traits);

        // Trait modifiers (Weapon Damage) and combat stats (Weapon Damage, the scaling attribute, Elemental Damage).
        if (traits != null)
            dmg *= traits.GetStatMultiplier(TraitStat.WeaponDamage);
        if (stats != null)
            dmg *= stats.WeaponDamageMultiplier(weapon.Category, weapon.Scaling, n.element != ElementType.None);

        dmg *= Mathf.Max(0f, n.damageMultiplier);

        if (n.element != ElementType.None)
            dmg *= ElementalMultiplier(weapon, traits, n.element, targetElement);

        float critChance = Mathf.Clamp01(weapon.CriticalChance + n.critChanceBonus);
        critical = critChance > 0f && Random.value < critChance;
        if (critical)
            dmg *= Mathf.Max(1f, weapon.CriticalDamageMultiplier) + n.critDamageBonus;
        return Mathf.Max(0f, dmg);
    }

    /// <summary>Old string trait effects of the wielder: "damage"/"attack" multipliers and additions.</summary>
    public static float ApplyLegacyPlayerDamage(float damage, TraitManager traits)
    {
        if (traits == null)
            return damage;
        IReadOnlyList<Trait> list = traits.Traits;
        for (int i = 0; i < list.Count; i++)
        {
            Trait trait = list[i];
            if (trait == null || trait.effects == null) continue;
            foreach (TraitEffect effect in trait.effects)
            {
                if (effect == null || string.IsNullOrEmpty(effect.targetStat)) continue;
                string key = effect.targetStat.ToLowerInvariant();
                if (key != "damage" && key != "attack") continue;
                if (effect.effectType == TraitEffectType.StatMultiplier) damage *= effect.value;
                else if (effect.effectType == TraitEffectType.StatAddition) damage += effect.value;
            }
        }
        return damage;
    }

    /// <summary>Elemental matchup: weapon traits, wielder traits ("fire_vs_ice", "elemental_fire_damage") and the Elemental System.</summary>
    public static float ElementalMultiplier(WeaponSO weapon, TraitManager traits, ElementType attack, ElementType target)
    {
        float m = weapon != null ? weapon.GetElementalDamageMultiplier(attack, target) : 1f;
        if (traits != null)
        {
            string matchup = $"{attack.ToString().ToLowerInvariant()}_vs_{target.ToString().ToLowerInvariant()}";
            string general = $"elemental_{attack.ToString().ToLowerInvariant()}_damage";
            IReadOnlyList<Trait> list = traits.Traits;
            for (int i = 0; i < list.Count; i++)
            {
                Trait trait = list[i];
                if (trait == null || trait.effects == null) continue;
                foreach (TraitEffect effect in trait.effects)
                {
                    if (effect == null || effect.effectType != TraitEffectType.StatMultiplier || string.IsNullOrEmpty(effect.targetStat)) continue;
                    string key = effect.targetStat.ToLowerInvariant();
                    if (key == matchup || key == general)
                        m *= effect.value;
                }
            }
        }
        ElementalSystem es = ElementalSystem.Instance;
        if (es != null && target != ElementType.None)
            m *= es.GetElementalDamageMultiplier(attack, target);
        return m;
    }

    /// <summary>The element of a target: an <see cref="IElemental"/> component, or an "element_*" trait effect.</summary>
    public static ElementType GetTargetElement(GameObject target)
    {
        if (target == null)
            return ElementType.None;
        IElemental elemental = target.GetComponentInParent<IElemental>();
        if (elemental != null)
            return elemental.ElementType;
        TraitManager tm = target.GetComponentInParent<TraitManager>();
        if (tm != null)
        {
            IReadOnlyList<Trait> list = tm.Traits;
            for (int i = 0; i < list.Count; i++)
            {
                Trait trait = list[i];
                if (trait == null || trait.effects == null) continue;
                foreach (TraitEffect effect in trait.effects)
                {
                    if (effect == null || string.IsNullOrEmpty(effect.targetStat)) continue;
                    string key = effect.targetStat.ToLowerInvariant();
                    if (key.StartsWith("element_") && System.Enum.TryParse(key.Substring(8), true, out ElementType e))
                        return e;
                }
            }
        }
        return ElementType.None;
    }

    /// <summary>
    /// Applies classic <see cref="AttackEffect"/>s of a hit. Instant health damage to an enemy (negative Hp, no
    /// duration) goes through the combat entity - defense, resistances, aggro, kill credit and on-hit reactions all
    /// see it - everything else goes to the status controllers as before.
    /// </summary>
    public static void ApplyClassicEffects(List<AttackActionEffect> effects, WeaponSO weapon, CombatEntity attacker, GameObject attackerObject,
        CombatEntity target, GameObject targetObject, float damageMultiplier, DamageType type, ElementType element, Vector3 point, Vector3 direction)
    {
        if (effects == null || effects.Count == 0)
            return;
        BaseStatusController self = attackerObject != null ? attackerObject.GetComponentInParent<BaseStatusController>() : null;
        BaseStatusController other = targetObject != null ? targetObject.GetComponentInParent<BaseStatusController>() : null;
        foreach (AttackActionEffect effect in effects)
        {
            if (effect == null || Random.value > effect.probabilityToApply)
                continue;
            BaseStatusController receiver = effect.enemyEffect ? other : self;
            float amount = GenericMethods.GetRandomValue(effect.amount, effect.randomAmount, effect.minAmount, effect.maxAmount);
            bool crit = Random.value <= effect.criticalChance;
            if (crit)
                amount *= effect.criticalDamageMultiplier;
            float duration = GenericMethods.GetRandomValue(effect.timeBuffEffect, effect.randomTimeBuffEffect, effect.minTimeBuffEffect, effect.maxTimeBuffEffect);
            float tick = GenericMethods.GetRandomValue(effect.tickCooldown, effect.randomTickCooldown, effect.minTickCooldown, effect.maxTickCooldown);

            if (effect.enemyEffect && effect.effectType == AttackEffectType.Hp && amount < 0f && duration <= 0f && target != null)
            {
                target.ApplyDamage(new DamageInfo
                {
                    amount = -amount * damageMultiplier,
                    source = attacker,
                    target = target,
                    point = point,
                    direction = direction,
                    isCritical = crit,
                    type = type,
                    element = element,
                    weapon = weapon,
                });
                continue;
            }
            if (receiver != null)
                receiver.ApplyEffect(effect, effect.effectType == AttackEffectType.Hp && amount < 0f ? amount * damageMultiplier : amount, duration, tick);
        }
    }

    /// <summary>
    /// Old string trait reactions on hit (weapon and wielder traits): "lifesteal"/"vampiric" heal a part of the
    /// damage, "aoe"/"explosive" splash the attack's classic effects around the target, "on_hit_slow" slows it.
    /// </summary>
    public static void ApplyLegacyTraitReactions(WeaponSO weapon, TraitManager traits, CombatEntity attacker, GameObject attackerObject,
        CombatEntity target, GameObject targetObject, float damage, List<AttackActionEffect> attackEffects)
    {
        if (weapon != null && weapon.WeaponTraits != null)
            foreach (Trait t in weapon.WeaponTraits)
                React(t, weapon, attacker, attackerObject, target, targetObject, damage, attackEffects);
        if (traits != null)
        {
            IReadOnlyList<Trait> list = traits.Traits;
            for (int i = 0; i < list.Count; i++)
                React(list[i], weapon, attacker, attackerObject, target, targetObject, damage, attackEffects);
        }
    }

    private static readonly List<CombatEntity> splashBuffer = new List<CombatEntity>(8);

    private static void React(Trait trait, WeaponSO weapon, CombatEntity attacker, GameObject attackerObject, CombatEntity target, GameObject targetObject, float damage, List<AttackActionEffect> attackEffects)
    {
        if (trait == null || trait.effects == null)
            return;
        foreach (TraitEffect effect in trait.effects)
        {
            if (effect == null || string.IsNullOrEmpty(effect.targetStat))
                continue;
            string key = effect.targetStat.ToLowerInvariant();
            if ((key == "lifesteal" || key == "vampiric") && effect.effectType != TraitEffectType.Special && damage > 0f)
            {
                if (attacker != null) attacker.ApplyHeal(damage * effect.value, attacker);
            }
            else if ((key == "aoe" || key == "explosive") && target != null && Random.value < effect.value)
            {
                splashBuffer.Clear();
                CombatQuery.InRadius(target.Position, 3f, attacker, TargetFilter.Enemies, splashBuffer);
                foreach (CombatEntity e in splashBuffer)
                {
                    if (e == target || e == attacker) continue;
                    ApplyClassicEffects(attackEffects, weapon, attacker, attackerObject, e, e.gameObject, 0.5f, DamageType.Physical, ElementType.None, e.Center, CombatQuery.FlatDirection(target.Position, e.Position, Vector3.forward));
                }
                splashBuffer.Clear();
            }
            else if (effect.effectType == TraitEffectType.Special && key.StartsWith("on_hit_") && targetObject != null)
            {
                BaseStatusController tc = targetObject.GetComponentInParent<BaseStatusController>();
                if (tc == null || key != "on_hit_slow") continue;
                var slow = new AttackEffect { effectName = trait.Name + "_slow", effectType = AttackEffectType.Speed, amount = -effect.value, timeBuffEffect = 3f };
                tc.ApplyEffect(slow, slow.amount, slow.timeBuffEffect, 0f);
            }
        }
    }
}
