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

        // Validate optional actions
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

        // Check if weapon is already equipped
        if (weaponController.EquippedWeapon != this)
        {
            Debug.Log("Equipping weapon first");
            weaponController.EquipWeapon(this);
        }

        // Check durability from the inventory item
        if (inventoryItem.durability <= 0)
        {
            Debug.LogWarning("Weapon durability is 0, cannot attack");
            return;
        }

        Debug.Log($"Attempting to perform attack with type: {attackType}");

        // Reduce durability from the inventory item
        inventoryItem.durability -= DurabilityReductionPerUse;

        // Perform the attack
        weaponController.PerformAttack(player, attackType);
    }

    // Override to prevent base item animation from being applied
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