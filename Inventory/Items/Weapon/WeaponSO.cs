using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// A weapon (or tool). Held in the hand from the hotbar, it attacks with one <see cref="AttackAction"/> per input
/// (Normal, Light, Heavy, Special, Alternate); each can chain variations, be charged, hit an area, apply status
/// effects and run behaviours (cast an ability, lunge...). Fixed input strings (<see cref="ComboSequence"/>) and
/// conditional branches (<see cref="ComboTree"/>) add combos. While wielded it can give passive effects and traits
/// (<see cref="EquipmentEffect"/>). Everything is data: new weapons need no code.
/// </summary>
[CreateAssetMenu(fileName = "Weapon", menuName = "Scriptable Objects/Item/Weapon")]
public class WeaponSO : ItemSO
{
    [Header("Weapon Attributes")]
    [Tooltip("Weapon family (Sword, Axe, Bow...). Equipment bonuses such as '+15% damage with swords' and 'While wielding' conditions use it.")]
    [SerializeField] private WeaponCategory weaponCategory = WeaponCategory.None;
    [Tooltip("What the weapon can harvest as a tool (trees need an Axe, ore a PickAxe...).")]
    [SerializeField] protected ToolType toolType;
    [Tooltip("Lowest damage of a hit (before attack multipliers, stats and the target's defense).")]
    [SerializeField] private float minDamage;
    [Tooltip("Highest damage of a hit.")]
    [SerializeField] private float maxDamage;
    [Tooltip("Damage multiplier of critical hits.")]
    [SerializeField] private float criticalDamageMultiplier = 1.0f;
    [Tooltip("Chance (0-1) of a critical hit.")]
    [SerializeField] private float criticalChance;
    [Tooltip("Distance (metres) targets are pushed back by a hit (0 = none). Each attack can scale it.")]
    [SerializeField] private float knockBack;
    [Tooltip("Multiplies the speed of every attack (0 or 1 = normal speed, 1.2 = 20% faster).")]
    [SerializeField] private float attackSpeed;
    [Tooltip("Attribute (Combat Stats) that increases this weapon's damage.")]
    [SerializeField] private WeaponScaling scaling = WeaponScaling.None;

    [Header("Animation System")]
    [Tooltip("Animations of this weapon (idle, attacks...). Optional: each attack can also have its own clip.")]
    [SerializeField] private WeaponAnimationSet animationSet;
    [Tooltip("Use the Animator Controller of the animation set while this weapon is held.")]
    [SerializeField] private bool useCustomAnimatorController = false;

    [Header("Weapon Actions")]
    [Tooltip("Normal action is required. Light, Heavy, Special and Alternate are optional.")]
    [SerializeField] private AttackAction normalAction;
    [Tooltip("Optional quick attack on its own input.")]
    [SerializeField] private AttackAction lightAction;
    [Tooltip("Optional strong attack (templates make it a charged attack: hold to charge, release to strike).")]
    [SerializeField] private AttackAction heavyAction;
    [Tooltip("Optional special move (a lunge, a poisoned strike, a nova...).")]
    [SerializeField] private AttackAction specialAction;
    [Tooltip("Secondary attack (right click / alternate input): a shield bash, a thrust, a charged shot...")]
    [SerializeField] private AttackAction alternateAction;

    [Header("Combo System")]
    [Tooltip("Fixed input strings that perform a special attack. Leave empty to skip.")]
    [SerializeField] private List<ComboSequence> comboSequences = new List<ComboSequence>();
    [Tooltip("Conditional branches (finishers, follow-ups). Optional.")]
    [SerializeField] private ComboTree comboTree;

    [Header("While Wielded")]
    [Tooltip("Effects on the WIELDER while the weapon is in the hand: stats, resistances, passive behaviours, on-hit procs (a 20% chance to burn), " +
             "abilities on keys... A 'Traits/Grant Traits' effect here gives traits to the PLAYER: they act on everything the player does " +
             "(movement, abilities, stats) and count as the player's traits.")]
    [SerializeReference, SubclassSelector] private List<EquipmentEffect> passiveEffects = new List<EquipmentEffect>();
    [Tooltip("Also give the Weapon Traits to the player while the weapon is in the hand (the same as listing them in a Grant Traits passive effect).")]
    [SerializeField] private bool applyTraitsToWielder = false;

    [Header("Weapon Traits")]
    [Tooltip("Traits OF THE WEAPON (not of the player). Their classic effects change only this weapon's attacks: damage, attack speed, " +
             "stamina cost, elemental multipliers, lifesteal, slow, on_hit_*... They also count for the attacks' Required / Enhancement " +
             "Traits. The player does not get them unless 'Apply Traits To Wielder' is on.")]
    [SerializeField] private List<Trait> weaponTraits = new List<Trait>();
    [Tooltip("Put the debuff parts of the Weapon Traits (their negative stat changes) on every character hit.")]
    [SerializeField] private bool applyTraitsToEnemy = false;
    [Tooltip("Scales how much the Weapon Traits change the weapon (1 = as written, 2 = twice the change).")]
    [SerializeField] private float traitEffectMultiplier = 1.0f;

    [Header("Elemental Properties")]
    [Tooltip("Element of the weapon's hits (attacks can override it).")]
    [SerializeField] private ElementType elementType = ElementType.None;
    [Tooltip("How much each elemental hit counts toward 'Elemental Charge' combo conditions.")]
    [SerializeField] private float elementalBuildupRate = 1.0f;

    [Header("Weapon Range (hint)")]
    [Tooltip("Closest distance the wielder likes to fight at (metres). A hint for AI and your scripts; it does not stop attacks from hitting closer.")]
    [SerializeField] private float minRange;
    [Tooltip("How far the weapon reaches (metres), as a HINT: drawn as a blue circle in the Scene gizmo and the Attack Preview, and read by " +
             "AI / your scripts. It does NOT decide what is hit - each attack's Hit Shape does. 'Set From Attacks' in the inspector fills it.")]
    [SerializeField] private float maxRange;

    [Header("Tool Attributes")]
    [Tooltip("Damage dealt to collectables (trees, rocks) of the matching tool type.")]
    [SerializeField] private float toolDamage;

    [Header("Attack Cast")]
    [Tooltip("OLD hit detection, kept for existing weapons: a physics overlap (sphere, box, capsule, ray) at the weapon HAND that finds " +
             "colliders on Target Layers. It has no timing or shape per attack. New weapons: leave Target Layers empty and give each attack a " +
             "Hit Shape. Used by attacks whose Hit Detection is Weapon Cast, or Auto when Target Layers is set.")]
    [SerializeField] public AttackCast attackCast;

    [Header("Hit Volumes (Weapon Blade hit detection)")]
    [Tooltip("The parts of the weapon that hit, for attacks whose Hit Detection is Weapon Blade: capsules, spheres or boxes on " +
             "the model in the hand that follow the animation. E.g. a sword: one Blade capsule 0.15 → 1.1 m; a hammer: a Head " +
             "box and a Handle capsule; a shield: a box. Attacks pick them by name.")]
    [SerializeField] private List<WeaponBlade> hitVolumes = new List<WeaponBlade>();
    [Tooltip("Single blade used when Hit Volumes is empty (older setups).")]
    [SerializeField] private WeaponBlade blade = new WeaponBlade();

    [Header("Audio")]
    [Tooltip("Played when an attack STARTS (the swing), for every attack that has no Attack Sound of its own. Weapons do not use the " +
             "Use Feedback sound; the sound of a hit landing is each attack's Hit Sound.")]
    [SerializeField] private AudioClip attackSound;
    [Tooltip("Played when the weapon is put in the hand.")]
    [SerializeField] private AudioClip equipSound;
    [Tooltip("Played when the weapon leaves the hand.")]
    [SerializeField] private AudioClip unequipSound;

    // Properties
    public ToolType ToolType => toolType;
    public WeaponCategory Category => weaponCategory;
    public WeaponScaling Scaling => scaling;
    public WeaponAnimationSet AnimationSet => animationSet;
    public bool UseCustomAnimatorController => useCustomAnimatorController;
    public AttackAction NormalAction => normalAction;
    public AttackAction LightAction => lightAction;
    public AttackAction HeavyAction => heavyAction;
    public AttackAction SpecialAction => specialAction;
    public AttackAction AlternateAction => alternateAction;
    public List<ComboSequence> ComboSequences => comboSequences;
    public ComboTree ComboTree => comboTree;
    public List<EquipmentEffect> PassiveEffects => passiveEffects ?? (passiveEffects = new List<EquipmentEffect>());
    public bool ApplyTraitsToWielder => applyTraitsToWielder;
    public List<Trait> WeaponTraits => weaponTraits;
    public bool ApplyTraitsToEnemy => applyTraitsToEnemy;
    public float TraitEffectMultiplier => traitEffectMultiplier;
    public ElementType ElementType => elementType;
    public float ElementalBuildupRate => elementalBuildupRate;
    public AttackCast AttackCast => attackCast;
    /// <summary>The first hit volume (the single blade of older setups).</summary>
    public WeaponBlade Blade => hitVolumes != null && hitVolumes.Count > 0 && hitVolumes[0] != null ? hitVolumes[0] : (blade ?? (blade = new WeaponBlade()));

    /// <summary>Every hit volume (the single Blade when the list is empty).</summary>
    public IReadOnlyList<WeaponBlade> HitVolumes
    {
        get
        {
            if (hitVolumes != null && hitVolumes.Count > 0)
                return hitVolumes;
            singleVolume[0] = blade ?? (blade = new WeaponBlade());
            return singleVolume;
        }
    }
    private readonly WeaponBlade[] singleVolume = new WeaponBlade[1];

    /// <summary>The hit volumes an attack uses (its Blade Volumes by name; all when it names none). Results cleared first.</summary>
    public void ActiveVolumes(AttackComponent attack, List<WeaponBlade> results)
    {
        results.Clear();
        IReadOnlyList<WeaponBlade> all = HitVolumes;
        List<string> names = attack != null ? attack.bladeVolumes : null;
        bool any = names != null && names.Exists(n => !string.IsNullOrWhiteSpace(n));
        for (int i = 0; i < all.Count; i++)
        {
            WeaponBlade v = all[i];
            if (v == null) continue;
            if (!any || names.Exists(n => string.Equals(n?.Trim(), v.name, System.StringComparison.OrdinalIgnoreCase)))
                results.Add(v);
        }
    }

    /// <summary>A hit volume by name (null when none has it).</summary>
    public WeaponBlade FindVolume(string volumeName)
    {
        if (string.IsNullOrWhiteSpace(volumeName)) return null;
        IReadOnlyList<WeaponBlade> all = HitVolumes;
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null && string.Equals(all[i].name, volumeName.Trim(), System.StringComparison.OrdinalIgnoreCase))
                return all[i];
        return null;
    }
    public float MinDamage => minDamage;
    public float MaxDamage => maxDamage;
    public float CriticalChance => criticalChance;
    public float CriticalDamageMultiplier => criticalDamageMultiplier;
    public float KnockBack => knockBack;
    public float AttackSpeed => attackSpeed;
    /// <summary>The attack speed multiplier (the Attack Speed field, 0 read as 1).</summary>
    public float AttackSpeedMultiplier => attackSpeed > 0f ? attackSpeed : 1f;
    public float ToolDamage => toolDamage;
    public AudioClip AttackSound => attackSound;
    public AudioClip EquipSound => equipSound;
    public AudioClip UnequipSound => unequipSound;
    public float MinRange { get => minRange; set => minRange = value; }
    public float MaxRange { get => maxRange; set => maxRange = value; }

    /// <summary>Replaces the action of an input (templates, tools).</summary>
    public void SetAction(AttackType type, AttackAction action)
    {
        if (action != null)
            action.actionType = type;
        switch (type)
        {
            case AttackType.Normal: normalAction = action; break;
            case AttackType.Light: lightAction = action; break;
            case AttackType.Heavy: heavyAction = action; break;
            case AttackType.Special: specialAction = action; break;
            case AttackType.Alternate: alternateAction = action; break;
        }
    }

    /// <summary>Sets the weapon's base numbers (templates, tools).</summary>
    public void SetStats(WeaponCategory category, float min, float max, float critChance, float critMultiplier, float knockback, WeaponScaling scalingAttribute)
    {
        weaponCategory = category;
        minDamage = Mathf.Max(0f, min);
        maxDamage = Mathf.Max(minDamage, max);
        criticalChance = Mathf.Clamp01(critChance);
        criticalDamageMultiplier = Mathf.Max(1f, critMultiplier);
        knockBack = Mathf.Max(0f, knockback);
        scaling = scalingAttribute;
        itemType = ItemType.Weapon;
    }

    /// <summary>True when the Attack Cast can detect anything (it has target layers).</summary>
    public bool HasUsableAttackCast => attackCast != null && attackCast.targetLayers.value != 0;

    protected override void OnValidate()
    {
        base.OnValidate();
        // Keep each slot's action type in sync with its slot (safe fix; never deletes data).
        SyncActionType(normalAction, AttackType.Normal);
        SyncActionType(lightAction, AttackType.Light);
        SyncActionType(heavyAction, AttackType.Heavy);
        SyncActionType(specialAction, AttackType.Special);
        SyncActionType(alternateAction, AttackType.Alternate);
        if (maxDamage < minDamage)
            maxDamage = minDamage;
    }

    private static void SyncActionType(AttackAction action, AttackType expected)
    {
        if (action != null && action.actionType != expected)
            action.actionType = expected;
    }

    // Combat Methods
    /// <summary>The action for an input, or null when the weapon has none for it.</summary>
    public AttackAction GetAction(AttackType attackType)
    {
        switch (attackType)
        {
            case AttackType.Normal: return normalAction; // required: always used when present
            case AttackType.Light: return IsUsable(lightAction) ? lightAction : null;
            case AttackType.Heavy: return IsUsable(heavyAction) ? heavyAction : null;
            case AttackType.Special: return IsUsable(specialAction) ? specialAction : null;
            case AttackType.Alternate: return IsUsable(alternateAction) ? alternateAction : null;
            default: return null;
        }
    }

    /// <summary>
    /// Unity fills every action field with a default action, so an optional action (Light, Heavy, Special,
    /// Alternate) counts as set up only once it has a name, an animation, effects, behaviours or variations.
    /// </summary>
    public static bool IsUsable(AttackAction a) =>
        a != null && (!string.IsNullOrEmpty(a.actionName) || a.AnimationClip != null || a.Effects.Count > 0 ||
                      (a.onHitEffects != null && a.onHitEffects.Count > 0) || (a.behaviours != null && a.behaviours.Count > 0) ||
                      a.GetVariationCount() > 0);

    public bool HasAction(AttackType attackType)
    {
        return GetAction(attackType) != null;
    }

    public AttackVariation GetActionVariation(AttackType attackType, int variationIndex)
    {
        var baseAction = GetAction(attackType);
        return baseAction?.GetVariation(variationIndex);
    }

    /// <summary>Exact match of a whole input list (old behaviour).</summary>
    public ComboSequence GetMatchingComboSequence(AttackType[] sequence)
    {
        if (comboSequences == null || comboSequences.Count == 0) return null;
        return comboSequences.FirstOrDefault(combo => combo != null && combo.IsValid() && combo.IsSequenceMatch(sequence));
    }

    /// <summary>The longest combo whose sequence ends the given inputs (null when none matches).</summary>
    public ComboSequence GetComboEndingWith(IReadOnlyList<AttackType> inputs)
    {
        if (comboSequences == null) return null;
        ComboSequence best = null;
        foreach (ComboSequence c in comboSequences)
        {
            if (c == null || !c.IsValid() || !c.MatchesEnd(inputs))
                continue;
            if (best == null || c.requiredSequence.Length > best.requiredSequence.Length)
                best = c;
        }
        return best;
    }

    public bool HasCombos() => (comboSequences != null && comboSequences.Any(c => c != null && c.IsValid())) || comboTree != null;

    /// <summary>A random damage roll between Min and Max Damage, with the weapon's critical chance.</summary>
    public float CalculateDamage(AttackAction action = null)
    {
        float baseDamage = Random.Range(minDamage, maxDamage);
        float critMultiplier = Random.value <= criticalChance ? criticalDamageMultiplier : 1.0f;
        return baseDamage * critMultiplier;
    }

    // ------------------------------------------------------------------ equipment
    public override void CollectEquipEffects(List<EquipmentEffect> into)
    {
        if (passiveEffects != null)
            foreach (EquipmentEffect e in passiveEffects)
                if (e != null) into.Add(e);
        if (applyTraitsToWielder && weaponTraits != null && weaponTraits.Any(t => t != null))
            into.Add(new GrantTraitsEffect(weaponTraits.Where(t => t != null)));
    }

    public override void AppendTooltip(List<string> lines)
    {
        string cat = weaponCategory != WeaponCategory.None ? weaponCategory.ToString() : "Weapon";
        lines.Add($"{cat}: {minDamage:0.#}-{maxDamage:0.#} damage" + (elementType != ElementType.None ? $" ({elementType})" : ""));
        if (criticalChance > 0f)
            lines.Add($"{criticalChance * 100f:0}% critical chance (x{criticalDamageMultiplier:0.##})");
        if (scaling != WeaponScaling.None)
            lines.Add($"Scales with {scaling}");
        foreach (AttackType t in System.Enum.GetValues(typeof(AttackType)))
        {
            AttackAction a = GetAction(t);
            if (a != null)
                lines.Add($"{t}: {a.DisplayName}" + (a.GetVariationCount() > 0 ? $" (chain of {a.GetVariationCount() + 1})" : "") + (a.charge != null && a.charge.enabled ? " - hold to charge" : ""));
        }
        if (comboSequences != null)
            foreach (ComboSequence c in comboSequences)
                if (c != null && c.IsValid())
                    lines.Add($"Combo {c.comboName}: {c.SequenceText}");
        base.AppendTooltip(lines);
    }

    public override void ValidateItem(List<string> errors, List<string> warnings)
    {
        base.ValidateItem(errors, warnings);
        if (itemType != ItemType.Weapon)
            warnings.Add($"Item Type is '{itemType}'; weapons normally use 'Weapon'.");
        if (GetAction(AttackType.Normal) == null)
            errors.Add("The weapon has no Normal action: the primary attack input does nothing.");
        if (maxDamage <= 0f && toolDamage <= 0f)
            warnings.Add("Min/Max Damage are 0: attacks only do what their effects do.");
        if (criticalChance < 0f || criticalChance > 1f)
            errors.Add("Critical Chance must be between 0 and 1.");

        foreach (AttackType t in System.Enum.GetValues(typeof(AttackType)))
        {
            AttackAction a = GetAction(t);
            if (a == null) continue;
            a.Validate($"{t} action", errors, warnings);
            bool usesCast = a.hitDetection == HitDetectionMode.WeaponCast;
            if (usesCast && !HasUsableAttackCast)
                errors.Add($"{t} action uses the Weapon Cast but the Attack Cast has no Target Layers, so it never hits.");
            if (a.hitDetection == HitDetectionMode.WeaponBlade && prefab == null)
                warnings.Add($"{t} action uses the Weapon Blade but the weapon has no Prefab: the volumes are measured from the hand.");
            if (a.bladeVolumes != null)
                foreach (string n in a.bladeVolumes)
                    if (!string.IsNullOrWhiteSpace(n) && FindVolume(n) == null)
                        errors.Add($"{t} action uses the hit volume '{n}', which the weapon does not have.");
            if (a.impact != null && a.impact.enabled && !string.IsNullOrWhiteSpace(a.impact.contactVolume) && FindVolume(a.impact.contactVolume) == null)
                errors.Add($"{t} action's Impact uses the hit volume '{a.impact.contactVolume}', which the weapon does not have.");
        }
        if (comboSequences != null)
            foreach (ComboSequence c in comboSequences)
                c?.Validate("Combo", errors, warnings);
        if (comboTree != null)
            comboTree.Validate(errors, warnings);
        EquipmentEffect.ValidateAll(passiveEffects, "While Wielded", errors, warnings);
        if (applyTraitsToWielder && (weaponTraits == null || weaponTraits.Count == 0))
            warnings.Add("'Apply Traits To Wielder' is on but the weapon has no traits.");
    }

    // ------------------------------------------------------------------ classic trait effects
    // Weapon traits' old string effects ("damage", "attackspeed", "staminacost", "elemental_fire"...) are read here.
    public bool HasTrait(Trait trait)
    {
        return trait != null && weaponTraits != null && weaponTraits.Contains(trait);
    }

    public Trait GetTraitByReference(Trait traitToFind)
    {
        return weaponTraits.Find(t => t == traitToFind);
    }

    public float CalculateTraitModifiedValue(float baseValue, string targetStat, TraitEffectType effectType)
    {
        float modifiedValue = baseValue;
        bool any = false;

        foreach (var trait in weaponTraits)
        {
            if (trait == null || trait.effects == null) continue;

            foreach (var effect in trait.effects)
            {
                if (effect == null || effect.effectType != effectType || !string.Equals(effect.targetStat, targetStat, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                switch (effect.effectType)
                {
                    case TraitEffectType.StatMultiplier:
                    case TraitEffectType.ConsumptionRate:
                        modifiedValue *= effect.value;
                        any = true;
                        break;
                    case TraitEffectType.StatAddition:
                        modifiedValue += effect.value;
                        any = true;
                        break;
                }
            }
        }

        // The trait multiplier scales only what the traits changed (it used to scale the untouched value too).
        return any ? baseValue + (modifiedValue - baseValue) * traitEffectMultiplier : baseValue;
    }

    public float CalculateTraitModifiedDamage(float baseDamage)
    {
        return CalculateTraitModifiedValue(baseDamage, "damage", TraitEffectType.StatMultiplier);
    }

    public float CalculateTraitModifiedStaminaCost(float baseCost)
    {
        float modifiedCost = CalculateTraitModifiedValue(baseCost, "stamina", TraitEffectType.ConsumptionRate);
        modifiedCost = CalculateTraitModifiedValue(modifiedCost, "staminacost", TraitEffectType.ConsumptionRate);
        return modifiedCost;
    }

    public float CalculateTraitModifiedSpeed(float baseSpeed)
    {
        float modifiedSpeed = CalculateTraitModifiedValue(baseSpeed, "attackspeed", TraitEffectType.StatMultiplier);
        modifiedSpeed = CalculateTraitModifiedValue(modifiedSpeed, "speed", TraitEffectType.StatMultiplier);
        return modifiedSpeed;
    }

    /// <summary>
    /// Old API: used to change the action's stamina cost and speed and add effects to it PERMANENTLY (the asset changed
    /// with every attack). The weapon controller now applies weapon traits per attack without touching the asset.
    /// </summary>
    [System.Obsolete("Weapon traits are applied per attack by the WeaponController (the asset is no longer modified).")]
    public void ApplyWeaponTraitsToAttack(AttackAction action, PlayerStatusController player) { }

    /// <summary>
    /// Classic "Special" trait effects of the weapon traits turned into attack effects (lifesteal, slow) for one attack.
    /// </summary>
    public void CollectSpecialTraitEffects(List<AttackActionEffect> into)
    {
        foreach (var trait in weaponTraits)
        {
            if (trait == null || trait.effects == null) continue;
            foreach (var traitEffect in trait.effects)
            {
                if (traitEffect == null || traitEffect.effectType != TraitEffectType.Special || string.IsNullOrEmpty(traitEffect.targetStat))
                    continue;
                var attackEffect = new AttackActionEffect
                {
                    effectName = trait.Name + "_Effect",
                    amount = traitEffect.value * traitEffectMultiplier,
                    enemyEffect = applyTraitsToEnemy,
                    isProcedural = false
                };
                switch (traitEffect.targetStat.ToLowerInvariant())
                {
                    case "lifesteal":
                    case "vampiric":
                        attackEffect.effectType = AttackEffectType.Hp;
                        attackEffect.enemyEffect = false; // heals the wielder
                        break;
                    case "slow":
                        attackEffect.effectType = AttackEffectType.Speed;
                        attackEffect.amount = -traitEffect.value * traitEffectMultiplier;
                        attackEffect.timeBuffEffect = 3f;
                        attackEffect.enemyEffect = true;
                        break;
                    default:
                        continue;
                }
                into.Add(attackEffect);
            }
        }
    }

    // Get elemental damage multiplier from traits
    public float GetElementalDamageMultiplier(ElementType attackElement, ElementType targetElement)
    {
        float multiplier = 1.0f;
        string matchup = $"{attackElement.ToString().ToLowerInvariant()}_vs_{targetElement.ToString().ToLowerInvariant()}";
        string general = $"elemental_{attackElement.ToString().ToLowerInvariant()}";

        foreach (var trait in weaponTraits)
        {
            if (trait == null || trait.effects == null) continue;
            foreach (var effect in trait.effects)
            {
                if (effect == null || effect.effectType != TraitEffectType.StatMultiplier || string.IsNullOrEmpty(effect.targetStat))
                    continue;
                string key = effect.targetStat.ToLowerInvariant();
                if (key == matchup || key == general)
                    multiplier *= effect.value;
            }
        }
        return multiplier;
    }

    // ------------------------------------------------------------------ classic effect application
    /// <summary>Applies an attack's classic effects to a target (and the collectable tool damage).</summary>
    public void ApplyEffectsToTarget(GameObject target, GameObject playerObject, IAttackComponent attackComponent = null)
    {
        if (target == null || playerObject == null) return;

        PlayerStatusController statusController = playerObject.GetComponentInParent<PlayerStatusController>();

        CollectableItem collectableItem = target.GetComponentInParent<CollectableItem>();
        if (collectableItem != null)
        {
            if (collectableItem.toolTypeRequired == toolType)
                collectableItem.TakeDamage(toolDamage);
            return;
        }

        var effectsToApply = attackComponent?.Effects;
        BaseStatusController targetController = target.GetComponentInParent<BaseStatusController>();
        if (targetController != null && effectsToApply != null && effectsToApply.Count > 0)
            ApplyEffectsToController(targetController, statusController, effectsToApply);

        if (applyTraitsToEnemy && targetController != null && targetController != statusController)
            ApplyWeaponTraitsToTarget(targetController);
    }

    private void ApplyWeaponTraitsToTarget(BaseStatusController targetController)
    {
        foreach (var trait in weaponTraits)
        {
            if (trait == null || trait.effects == null) continue;
            foreach (var effect in trait.effects)
                if (effect != null && ShouldApplyAsDebuff(effect))
                    ApplyTraitEffectAsDebuff(trait, effect, targetController);
        }
    }

    private bool ShouldApplyAsDebuff(TraitEffect effect)
    {
        string key = effect.targetStat != null ? effect.targetStat.ToLowerInvariant() : "";
        return (effect.effectType == TraitEffectType.StatMultiplier && effect.value < 1.0f) ||
               (effect.effectType == TraitEffectType.StatAddition && effect.value < 0) ||
               key.Contains("debuff") || key.Contains("slow") || key.Contains("weakness");
    }

    private void ApplyTraitEffectAsDebuff(Trait trait, TraitEffect traitEffect, BaseStatusController targetController)
    {
        string key = traitEffect.targetStat != null ? traitEffect.targetStat.ToLowerInvariant() : "";
        if (key != "speed" && key != "movementspeed" && !key.Contains("slow"))
            return; // only speed debuffs have a mapping
        var debuffEffect = new AttackEffect
        {
            effectName = trait.Name + "_Debuff",
            effectType = AttackEffectType.Speed,
            amount = traitEffect.effectType == TraitEffectType.StatMultiplier ? -(1f - traitEffect.value) : -Mathf.Abs(traitEffect.value),
            timeBuffEffect = 3f
        };
        targetController.ApplyEffect(debuffEffect, debuffEffect.amount * traitEffectMultiplier, debuffEffect.timeBuffEffect, 0);
    }

    // Backward compatibility overloads
    public void ApplyEffectsToTarget(GameObject target, GameObject playerObject, AttackAction action = null, AttackVariation variation = null)
    {
        IAttackComponent component = variation ?? (IAttackComponent)action;
        ApplyEffectsToTarget(target, playerObject, component);
    }

    public void ApplyEffectsToTarget(GameObject target, GameObject playerObject, AttackAction action = null)
    {
        ApplyEffectsToTarget(target, playerObject, (IAttackComponent)action);
    }

    private void ApplyEffectsToController<T>(T targetController, PlayerStatusController statusController = null, List<AttackActionEffect> effects = null)
        where T : BaseStatusController
    {
        if (effects == null) return;

        foreach (var effect in effects)
        {
            if (effect == null || Random.value > effect.probabilityToApply)
                continue;
            if (effect.enemyEffect == false && statusController != null)
                ApplyEffect(effect, statusController);
            else
                ApplyEffect(effect, targetController);
        }
    }

    public void ApplyEffect<T>(AttackEffect effect, T statusController)
        where T : BaseStatusController
    {
        float amount = GenericMethods.GetRandomValue(effect.amount, effect.randomAmount, effect.minAmount, effect.maxAmount);
        float critMultiplier = Random.value <= effect.criticalChance ? effect.criticalDamageMultiplier : 1.0f;
        amount *= critMultiplier;

        float timeBuffEffect = GenericMethods.GetRandomValue(effect.timeBuffEffect, effect.randomTimeBuffEffect, effect.minTimeBuffEffect, effect.maxTimeBuffEffect);
        float tickCooldown = GenericMethods.GetRandomValue(effect.tickCooldown, effect.randomTickCooldown, effect.minTickCooldown, effect.maxTickCooldown);

        statusController.ApplyEffect(effect, amount, timeBuffEffect, tickCooldown);
    }

    // ------------------------------------------------------------------ use
    public override void UseItem(GameObject player, PlayerStatusController statusController)
    {
        UseItem(player, statusController, player != null ? player.GetComponentInChildren<WeaponController>() : null, AttackType.Normal);
    }

    public override void UseItem(GameObject player, PlayerStatusController statusController = null, WeaponController weaponController = null, AttackType attackType = AttackType.Normal)
    {
        UseItem(player, statusController, weaponController, attackType, null);
    }

    /// <summary>Attacks with this weapon (equipping it first when needed).</summary>
    public void UseItem(GameObject player, PlayerStatusController statusController, WeaponController weaponController, AttackType attackType, InventoryItem inventoryItem)
    {
        if (weaponController == null)
        {
            Debug.LogError($"[Weapon] {Name}: a WeaponController is required to attack.", this);
            return;
        }

        if (weaponController.EquippedWeapon != this || (inventoryItem != null && weaponController.HeldItem != inventoryItem))
            weaponController.EquipWeapon(this, inventoryItem);

        weaponController.PerformAttack(player, attackType);
    }

    protected override void ApplyItemAnimation(GameObject player)
    {
        // Weapons handle their animations through the WeaponController and PlayerAnimationController
    }

    public AnimationClip GetIdleAnimation() => animationSet?.idleAnimation;
    public AnimationClip GetEquipAnimation() => animationSet?.equipAnimation;
    public AnimationClip GetUnequipAnimation() => animationSet?.unequipAnimation;

    public RuntimeAnimatorController GetAnimatorController()
    {
        return useCustomAnimatorController ? animationSet?.animatorController : null;
    }
}
