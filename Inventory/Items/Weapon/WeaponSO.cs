using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public enum ToolType
{
    None,
    Scythe,
    Axe,
    PickAxe,
    Fishingrod,
}

public enum AttackType
{
    Normal,
    Light,
    Heavy,
    Special
}

[CreateAssetMenu(fileName = "Weapon", menuName = "Scriptable Objects/Item/Weapon")]
public class WeaponSO : ItemSO
{
    [Header("Weapon Attributes")]
    [SerializeField] protected ToolType toolType;
    [SerializeField] private float minDamage;
    [SerializeField] private float maxDamage;
    [SerializeField] private float criticalDamageMultiplier = 1.0f;
    [SerializeField] private float criticalChance;
    [SerializeField] private float knockBack;
    [SerializeField] private float attackSpeed;

    [Header("Animation System")]
    [SerializeField] private WeaponAnimationSet animationSet;
    [SerializeField] private bool useCustomAnimatorController = false;

    [Header("Weapon Actions")]
    [Tooltip("Normal action is required. Light, Heavy, and Special are optional.")]
    [SerializeField] private AttackAction normalAction;
    [SerializeField] private AttackAction lightAction;
    [SerializeField] private AttackAction heavyAction;
    [SerializeField] private AttackAction specialAction;

    [Header("Combo System")]
    [Tooltip("Define combo sequences. Leave empty to bypass combo logic.")]
    [SerializeField] private List<ComboSequence> comboSequences = new List<ComboSequence>();
    [SerializeField] private ComboTree comboTree;

    [Header("Weapon Traits")]
    [SerializeField] private List<Trait> weaponTraits = new List<Trait>();
    [SerializeField] private bool applyTraitsToEnemy = false;
    [SerializeField] private float traitEffectMultiplier = 1.0f;

    [Header("Elemental Properties")]
    [SerializeField] private ElementType elementType = ElementType.None;
    [SerializeField] private float elementalBuildupRate = 1.0f;

    [Header("Weapon Range")]
    [SerializeField] private float minRange;
    [SerializeField] private float maxRange;

    [Header("Tool Attributes")]
    [SerializeField] private float toolDamage;

    [Header("Attack Cast")]
    [SerializeField] public AttackCast attackCast;

    [Header("Audio")]
    [SerializeField] private AudioClip attackSound;
    [SerializeField] private AudioClip equipSound;
    [SerializeField] private AudioClip unequipSound;

    // Cache for action lookup
    private Dictionary<AttackType, AttackAction> actionCache;

    // Properties
    public ToolType ToolType => toolType;
    public WeaponAnimationSet AnimationSet => animationSet;
    public bool UseCustomAnimatorController => useCustomAnimatorController;
    public AttackAction NormalAction => normalAction;
    public AttackAction LightAction => lightAction;
    public AttackAction HeavyAction => heavyAction;
    public AttackAction SpecialAction => specialAction;
    public List<ComboSequence> ComboSequences => comboSequences;
    public ComboTree ComboTree => comboTree;
    public List<Trait> WeaponTraits => weaponTraits;
    public ElementType ElementType => elementType;
    public float ElementalBuildupRate => elementalBuildupRate;
    public AttackCast AttackCast => attackCast;
    public float MinDamage => minDamage;
    public float MaxDamage => maxDamage;
    public float CriticalChance => criticalChance;
    public float CriticalDamageMultiplier => criticalDamageMultiplier;
    public float KnockBack => knockBack;
    public float AttackSpeed => attackSpeed;
    public float ToolDamage => toolDamage;
    public AudioClip AttackSound => attackSound;
    public AudioClip EquipSound => equipSound;
    public AudioClip UnequipSound => unequipSound;
    public float MinRange { get => minRange; set => minRange = value; }
    public float MaxRange { get => maxRange; set => maxRange = value; }

    private void OnEnable()
    {
        BuildActionCache();
    }

    private void OnValidate()
    {
        ValidateActions();
        BuildActionCache();
    }

    private void BuildActionCache()
    {
        actionCache = new Dictionary<AttackType, AttackAction>
        {
            [AttackType.Normal] = normalAction,
            [AttackType.Light] = lightAction,
            [AttackType.Heavy] = heavyAction,
            [AttackType.Special] = specialAction
        };
    }

    private void ValidateActions()
    {
        if (normalAction == null)
        {
            Debug.LogWarning($"Weapon {name} must have a Normal action!");
        }
        else if (normalAction.actionType != AttackType.Normal)
        {
            normalAction.actionType = AttackType.Normal;
        }

        ValidateActionType(lightAction, AttackType.Light);
        ValidateActionType(heavyAction, AttackType.Heavy);
        ValidateActionType(specialAction, AttackType.Special);

        ValidateComboSequences();
    }

    private void ValidateActionType(AttackAction action, AttackType expectedType)
    {
        if (action != null && action.actionType != expectedType)
            action.actionType = expectedType;
    }

    private void ValidateComboSequences()
    {
        if (comboSequences == null) return;

        for (int i = comboSequences.Count - 1; i >= 0; i--)
        {
            var combo = comboSequences[i];
            if (!combo.IsValid())
            {
                Debug.LogWarning($"Invalid combo sequence at index {i} in weapon {name}. Removing...");
                comboSequences.RemoveAt(i);
            }
        }
    }

    // Combat Methods
    public AttackAction GetAction(AttackType attackType)
    {
        if (actionCache == null) BuildActionCache();
        return actionCache.TryGetValue(attackType, out var action) ? action : normalAction;
    }

    public bool HasAction(AttackType attackType)
    {
        return GetAction(attackType) != null;
    }

    public AttackVariation GetActionVariation(AttackType attackType, int variationIndex)
    {
        var baseAction = GetAction(attackType);
        return baseAction?.GetVariation(variationIndex);
    }

    public ComboSequence GetMatchingComboSequence(AttackType[] sequence)
    {
        if (comboSequences == null || comboSequences.Count == 0) return null;
        return comboSequences.FirstOrDefault(combo => combo.IsSequenceMatch(sequence));
    }

    public bool HasCombos() => comboSequences != null && comboSequences.Count > 0;

    public float CalculateDamage(AttackAction action = null)
    {
        float baseDamage = Random.Range(minDamage, maxDamage);
        float critMultiplier = Random.value <= criticalChance ? criticalDamageMultiplier : 1.0f;
        return baseDamage * critMultiplier;
    }

    // Trait-related methods - Fixed to be data-driven
    public bool HasTrait(Trait trait)
    {
        return weaponTraits.Contains(trait);
    }

    public Trait GetTraitByReference(Trait traitToFind)
    {
        return weaponTraits.Find(t => t == traitToFind);
    }

    public float CalculateTraitModifiedValue(float baseValue, string targetStat, TraitEffectType effectType)
    {
        float modifiedValue = baseValue;

        foreach (var trait in weaponTraits)
        {
            if (trait == null) continue;

            foreach (var effect in trait.effects)
            {
                if (effect.targetStat.ToLower() == targetStat.ToLower() && effect.effectType == effectType)
                {
                    switch (effect.effectType)
                    {
                        case TraitEffectType.StatMultiplier:
                            modifiedValue *= effect.value;
                            break;
                        case TraitEffectType.StatAddition:
                            modifiedValue += effect.value;
                            break;
                        case TraitEffectType.ConsumptionRate:
                            modifiedValue *= effect.value;
                            break;
                    }
                }
            }
        }

        return modifiedValue * traitEffectMultiplier;
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

    public void ApplyWeaponTraitsToAttack(AttackAction action, PlayerStatusController player)
    {
        if (action == null || player == null) return;

        // Apply trait modifiers to the action
        action.staminaCost = CalculateTraitModifiedStaminaCost(action.staminaCost);
        action.animationSpeed = CalculateTraitModifiedSpeed(action.animationSpeed);

        // Apply additional effects from traits
        foreach (var trait in weaponTraits)
        {
            if (trait == null) continue;

            // Check if trait has special effects to add to the action
            foreach (var effect in trait.effects)
            {
                if (effect.effectType == TraitEffectType.Special)
                {
                    // Special effects are added to the action's effect list
                    AddSpecialTraitEffectToAction(trait, effect, action);
                }
            }
        }
    }

    private void AddSpecialTraitEffectToAction(Trait trait, TraitEffect traitEffect, AttackAction action)
    {
        // Create attack effect from trait effect
        var attackEffect = new AttackActionEffect
        {
            effectName = trait.Name + "_Effect",
            amount = traitEffect.value,
            enemyEffect = applyTraitsToEnemy,
            isProcedural = false
        };

        // Map trait effect target stat to attack effect type
        switch (traitEffect.targetStat.ToLower())
        {
            case "lifesteal":
            case "vampiric":
                attackEffect.effectType = AttackEffectType.Hp;
                attackEffect.enemyEffect = false; // Life steal heals player
                break;

            case "slow":
                attackEffect.effectType = AttackEffectType.Speed;
                attackEffect.amount = -traitEffect.value; // Negative for slow
                attackEffect.timeBuffEffect = 3f;
                break;
                // Add more mappings as needed
        }

        action.Effects.Add(attackEffect);
    }

    // Get elemental damage multiplier from traits
    public float GetElementalDamageMultiplier(ElementType attackElement, ElementType targetElement)
    {
        float multiplier = 1.0f;

        foreach (var trait in weaponTraits)
        {
            if (trait == null) continue;

            foreach (var effect in trait.effects)
            {
                // Check for elemental damage modifiers in trait effects
                string statKey = $"{attackElement.ToString().ToLower()}_vs_{targetElement.ToString().ToLower()}";
                if (effect.targetStat.ToLower() == statKey ||
                    effect.targetStat.ToLower() == $"elemental_{attackElement.ToString().ToLower()}")
                {
                    if (effect.effectType == TraitEffectType.StatMultiplier)
                    {
                        multiplier *= effect.value;
                    }
                }
            }
        }

        return multiplier;
    }

    // Unified effect application method
    public void ApplyEffectsToTarget(GameObject target, GameObject playerObject, IAttackComponent attackComponent = null)
    {
        var effectsToApply = attackComponent?.Effects;
        if (effectsToApply == null || effectsToApply.Count == 0) return;

        PlayerStatusController statusController = playerObject.GetComponent<PlayerStatusController>();

        if (target.CompareTag("Player"))
        {
            PlayerStatusController otherPlayerController = target.GetComponent<PlayerStatusController>();
            if (otherPlayerController != null)
                ApplyEffectsToController(otherPlayerController, statusController, effectsToApply);
        }
        else if (target.CompareTag("Mob"))
        {
            MobStatusController mobController = target.GetComponent<MobStatusController>();
            if (mobController != null)
                ApplyEffectsToController(mobController, statusController, effectsToApply);
        }
        else if (target.CompareTag("Collectable"))
        {
            CollectableItem collectableItem = target.GetComponent<CollectableItem>();
            if (collectableItem != null && collectableItem.toolTypeRequired == toolType)
            {
                collectableItem.TakeDamage(toolDamage);
            }
        }

        // Apply weapon traits to enemy if enabled
        if (applyTraitsToEnemy && (target.CompareTag("Mob") || target.CompareTag("Player")))
        {
            ApplyWeaponTraitsToTarget(target);
        }
    }

    private void ApplyWeaponTraitsToTarget(GameObject target)
    {
        var targetController = target.GetComponent<BaseStatusController>();
        if (targetController == null) return;

        foreach (var trait in weaponTraits)
        {
            if (trait == null) continue;

            foreach (var effect in trait.effects)
            {
                // Apply debuff effects from traits
                if (ShouldApplyAsDebuff(effect))
                {
                    ApplyTraitEffectAsDebuff(trait, effect, targetController);
                }
            }
        }
    }

    private bool ShouldApplyAsDebuff(TraitEffect effect)
    {
        // Debuffs are effects that reduce stats or have negative values
        return (effect.effectType == TraitEffectType.StatMultiplier && effect.value < 1.0f) ||
               (effect.effectType == TraitEffectType.StatAddition && effect.value < 0) ||
               effect.targetStat.ToLower().Contains("debuff") ||
               effect.targetStat.ToLower().Contains("slow") ||
               effect.targetStat.ToLower().Contains("weakness");
    }

    private void ApplyTraitEffectAsDebuff(Trait trait, TraitEffect traitEffect, BaseStatusController targetController)
    {
        var debuffEffect = new AttackEffect
        {
            effectName = trait.Name + "_Debuff",
            amount = traitEffect.value,
            timeBuffEffect = 3f // Default debuff duration
        };

        // Map trait effect to attack effect type
        switch (traitEffect.targetStat.ToLower())
        {
            case "speed":
            case "movementspeed":
                debuffEffect.effectType = AttackEffectType.Speed;
                debuffEffect.amount = -(1f - traitEffect.value); // Convert multiplier to reduction
                break;
        }

        targetController.ApplyEffect(debuffEffect, debuffEffect.amount, debuffEffect.timeBuffEffect, 0);
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
            if (Random.value <= effect.probabilityToApply)
            {
                if (effect.enemyEffect == false && statusController != null)
                    ApplyEffect(effect, statusController);
                else
                    ApplyEffect(effect, targetController);
            }
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

    // UseItem methods
    public override void UseItem(GameObject player, PlayerStatusController statusController)
    {
        Debug.LogWarning("Weapons should be used through UseItem with WeaponController parameter and InventoryItem reference");
    }

    public override void UseItem(GameObject player, PlayerStatusController statusController = null, WeaponController weaponController = null, AttackType attackType = AttackType.Normal)
    {
        Debug.LogWarning("This overload is deprecated. Use the version with InventoryItem parameter for proper durability handling.");

        var inventoryManager = player.GetComponent<Player>()?.InventoryManager;
        if (inventoryManager != null)
        {
            UseItem(player, statusController, weaponController, attackType, null);
        }
    }

    public void UseItem(GameObject player, PlayerStatusController statusController, WeaponController weaponController, AttackType attackType, InventoryItem inventoryItem)
    {
        Debug.Log($"WeaponSO.UseItem called with attackType: {attackType}");

        if (weaponController == null)
        {
            Debug.LogError("WeaponController is required to use weapons!");
            return;
        }

        if (inventoryItem == null)
        {
            Debug.LogError("InventoryItem reference is required for proper durability handling!");
            return;
        }

        if (weaponController.EquippedWeapon != this)
        {
            Debug.Log("Equipping weapon first");
            weaponController.EquipWeapon(this);
        }

        if (inventoryItem.durability <= 0)
        {
            Debug.LogWarning("Weapon durability is 0, cannot attack");
            return;
        }

        Debug.Log($"Attempting to perform attack with type: {attackType}");

        inventoryItem.durability -= DurabilityReductionPerUse;

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