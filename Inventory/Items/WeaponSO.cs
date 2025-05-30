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
    Charged,
    Special
}

[System.Serializable]
public class AttackPattern
{
    [Header("Attack Configuration")]
    public AttackType Type;
    public string AttackName; // e.g., "Quick Slash", "Heavy Strike"

    [Header("Damage Settings")]
    public float BaseDamage;
    public bool hasFixedBaseDamage;
    public float MinDamage;
    public float MaxDamage;
    public bool hasDifferentCriticalChange;
    public float CriticalChance;

    [Header("Animation & Timing")]
    public AnimationClip AnimationClip; // Direct reference to animation clip
    public string AnimationTrigger; // Fallback trigger name
    public float AnimationSpeed = 1.0f;
    public float StartupFrames = 0.1f; // Time before attack becomes active
    public float ActiveFrames = 0.2f; // Duration of attack hitbox
    public float RecoveryFrames = 0.3f; // Time after attack before next action

    [Header("Combat Properties")]
    public float StaminaCost;
    public float ComboResetTime = 2.0f;
    public int ComboIndex; // Position in combo chain
    public bool CanComboInto = true; // Can this attack be comboed into?
    public bool EndsCombo = false; // Does this attack end the combo?

    [Header("Movement & Effects")]
    public float MovementSpeedMultiplier = 1.0f; // Speed multiplier during attack
    public bool LockMovement = false; // Completely lock movement during attack
    public Vector3 ForwardMovement = Vector3.zero; // Push player forward during attack

    [Header("Audio & Visual")]
    public AudioClip AttackSound;
    public ParticleSystem AttackParticles;
    public GameObject TrailEffect; // Weapon trail effect

    [Header("Input Buffer")]
    [Tooltip("Time window where this attack can be buffered before it becomes available")]
    public float InputBufferWindow = 0.2f;

    public float GetTotalDuration()
    {
        return (StartupFrames + ActiveFrames + RecoveryFrames) / AnimationSpeed;
    }

    public bool IsInActiveFrames(float normalizedTime)
    {
        float startTime = StartupFrames / GetTotalDuration();
        float endTime = (StartupFrames + ActiveFrames) / GetTotalDuration();
        return normalizedTime >= startTime && normalizedTime <= endTime;
    }

    public bool IsInRecoveryFrames(float normalizedTime)
    {
        float recoveryStart = (StartupFrames + ActiveFrames) / GetTotalDuration();
        return normalizedTime >= recoveryStart;
    }
}

[System.Serializable]
public class ComboSequence
{
    [Header("Combo Definition")]
    public string ComboName;
    public AttackType[] RequiredSequence; // e.g., [Light, Heavy, Light]
    public float MaxTimeBetweenAttacks = 2.0f;

    [Header("Combo Finisher")]
    public AttackPattern FinisherAttack; // Special attack when combo is completed
    public bool HasFinisher = true;

    [Header("Visual & Audio")]
    public ParticleSystem ComboFinisherParticles;
    public AudioClip ComboFinisherSound;

    [Header("Bonuses")]
    public float DamageMultiplier = 1.5f; // Damage bonus for combo finisher
    public float CriticalChanceBonus = 0.1f; // Additional crit chance
    public int ExperienceBonus = 10; // Bonus XP for completing combo

    public bool IsSequenceMatch(AttackType[] playerSequence)
    {
        if (playerSequence.Length != RequiredSequence.Length) return false;

        for (int i = 0; i < RequiredSequence.Length; i++)
        {
            if (playerSequence[i] != RequiredSequence[i]) return false;
        }
        return true;
    }
}

[System.Serializable]
public class WeaponAnimationSet
{
    [Header("Animation Set Info")]
    public string SetName;
    [Tooltip("Animator Controller for this weapon")]
    public RuntimeAnimatorController AnimatorController;

    [Header("Basic Animations")]
    public AnimationClip IdleAnimation;
    public AnimationClip WalkAnimation;
    public AnimationClip RunAnimation;
    public AnimationClip EquipAnimation;
    public AnimationClip UnequipAnimation;

    [Header("Combat Animations")]
    public List<AnimationClip> LightAttackAnimations = new List<AnimationClip>();
    public List<AnimationClip> HeavyAttackAnimations = new List<AnimationClip>();
    public List<AnimationClip> ChargedAttackAnimations = new List<AnimationClip>();
    public List<AnimationClip> SpecialAttackAnimations = new List<AnimationClip>();

    [Header("Combo Animations")]
    public List<AnimationClip> ComboFinisherAnimations = new List<AnimationClip>();

    public AnimationClip GetAttackAnimation(AttackType attackType, int index = 0)
    {
        List<AnimationClip> animations = attackType switch
        {
            AttackType.Light => LightAttackAnimations,
            AttackType.Heavy => HeavyAttackAnimations,
            AttackType.Charged => ChargedAttackAnimations,
            AttackType.Special => SpecialAttackAnimations,
            _ => LightAttackAnimations
        };

        if (animations.Count == 0) return null;
        return animations[index % animations.Count];
    }
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

    [Header("Attack Patterns")]
    [SerializeField] private List<AttackPattern> attackPatterns = new List<AttackPattern>();

    [Header("Combo System")]
    [SerializeField] private List<ComboSequence> comboSequences = new List<ComboSequence>();
    [SerializeField] private float globalComboWindow = 2.0f;
    [SerializeField] private int maxComboLength = 10;

    [Header("Weapon Range")]
    [SerializeField] private float minRange;
    [SerializeField] private float maxRange;

    [Header("Tool Attributes")]
    [SerializeField] private float toolDamage;

    [Header("Attack Effects")]
    [SerializeField] private List<AttackEffect> effects;
    [SerializeField] public AttackCast attackCast;

    [Header("Audio")]
    [SerializeField] private AudioClip attackSound;
    [SerializeField] private AudioClip equipSound;
    [SerializeField] private AudioClip unequipSound;

    // Properties
    public ToolType ToolType => toolType;
    public WeaponAnimationSet AnimationSet => animationSet;
    public bool UseCustomAnimatorController => useCustomAnimatorController;
    public List<AttackEffect> Effects => effects;
    public AudioClip AttackSound => attackSound;
    public AudioClip EquipSound => equipSound;
    public AudioClip UnequipSound => unequipSound;
    public List<AttackPattern> AttackPatterns => attackPatterns;
    public List<ComboSequence> ComboSequences => comboSequences;
    public AttackCast AttackCast => attackCast;
    public float GlobalComboWindow => globalComboWindow;
    public int MaxComboLength => maxComboLength;
    public float MinDamage => minDamage;
    public float MaxDamage => maxDamage;
    public float CriticalChance => criticalChance;
    public float CriticalDamageMultiplier => criticalDamageMultiplier;
    public float KnockBack => knockBack;
    public float AttackSpeed => attackSpeed;
    public float ToolDamage => toolDamage;

    public float MinRange { get => minRange; set => minRange = value; }
    public float MaxRange { get => maxRange; set => maxRange = value; }

    private HashSet<Collider> detectedColliders = new HashSet<Collider>();

    // Validation and Setup
    private void OnValidate()
    {
        ValidateAttackPatterns();
        SetupAnimationReferences();
    }

    private void ValidateAttackPatterns()
    {
        for (int i = 0; i < attackPatterns.Count; i++)
        {
            var pattern = attackPatterns[i];
            if (pattern.AnimationClip == null && string.IsNullOrEmpty(pattern.AnimationTrigger))
            {
                Debug.LogWarning($"Attack pattern {i} in {name} has no animation clip or trigger assigned!");
            }

            // Auto-assign animation from animation set if available
            if (pattern.AnimationClip == null && animationSet != null)
            {
                pattern.AnimationClip = animationSet.GetAttackAnimation(pattern.Type, pattern.ComboIndex);
            }
        }
    }

    private void SetupAnimationReferences()
    {
        if (animationSet == null) return;

        // Sync attack patterns with animation set
        foreach (var pattern in attackPatterns)
        {
            if (pattern.AnimationClip == null)
            {
                pattern.AnimationClip = animationSet.GetAttackAnimation(pattern.Type, pattern.ComboIndex);
            }
        }
    }

    // Combat Methods
    public AttackPattern GetAttackPattern(AttackType attackType, int comboIndex = 0)
    {
        return attackPatterns.FirstOrDefault(p => p.Type == attackType && p.ComboIndex == comboIndex);
    }

    public List<AttackPattern> GetAttackPatternsByType(AttackType attackType)
    {
        return attackPatterns.Where(p => p.Type == attackType).OrderBy(p => p.ComboIndex).ToList();
    }

    public ComboSequence GetMatchingComboSequence(AttackType[] sequence)
    {
        return comboSequences.FirstOrDefault(combo => combo.IsSequenceMatch(sequence));
    }

    public float CalculateDamage(AttackPattern pattern = null)
    {
        float baseDamage = pattern?.hasFixedBaseDamage == true ?
            pattern.BaseDamage :
            Random.Range(minDamage, maxDamage);

        if (pattern != null && !pattern.hasFixedBaseDamage)
        {
            baseDamage = Random.Range(pattern.MinDamage, pattern.MaxDamage);
        }

        float critChance = pattern?.hasDifferentCriticalChange == true ?
            pattern.CriticalChance :
            criticalChance;

        float critMultiplier = Random.value <= critChance ? criticalDamageMultiplier : 1.0f;
        return baseDamage * critMultiplier;
    }

    public void DealDamage(GameObject target, AttackPattern pattern = null)
    {
        float damage = CalculateDamage(pattern);
        // Apply damage to target (implementation depends on your damage system)
    }

    public void ApplyEffectsToTarget(GameObject target, GameObject playerObject, AttackPattern pattern = null)
    {
        PlayerStatusController statusController = playerObject.GetComponent<PlayerStatusController>();

        if (target.CompareTag("Player"))
        {
            PlayerStatusController otherPlayerController = target.GetComponent<PlayerStatusController>();
            if (otherPlayerController != null)
                ApplyEffectsToController(otherPlayerController, statusController, pattern);
        }
        else if (target.CompareTag("Mob"))
        {
            MobStatusController mobController = target.GetComponent<MobStatusController>();
            if (mobController != null)
                ApplyEffectsToController(mobController, statusController, pattern);
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

    private void ApplyEffectsToController<T>(T targetController, PlayerStatusController statusController = null, AttackPattern pattern = null)
        where T : BaseStatusController
    {
        foreach (var effect in effects)
        {
            if (Random.value <= effect.probabilityToApply)
            {
                if (effect.enemyEffect == false && statusController != null)
                    ApplyEffect(effect, statusController, pattern);
                else
                    ApplyEffect(effect, targetController, pattern);
            }
        }
    }

    public void ApplyEffect<T>(AttackEffect effect, T statusController, AttackPattern pattern = null)
        where T : BaseStatusController
    {
        float amount = GenericMethods.GetRandomValue(effect.amount, effect.randomAmount, effect.minAmount, effect.maxAmount);

        // Apply pattern-specific modifiers if available
        if (pattern != null)
        {
            float critChance = pattern.hasDifferentCriticalChange ? pattern.CriticalChance : effect.criticalChance;
            float critMultiplier = Random.value <= critChance ? effect.criticalDamageMultiplier : 1.0f;
            amount *= critMultiplier;
        }
        else
        {
            float critMultiplier = Random.value <= effect.criticalChance ? effect.criticalDamageMultiplier : 1.0f;
            amount *= critMultiplier;
        }

        float timeBuffEffect = GenericMethods.GetRandomValue(effect.timeBuffEffect, effect.randomTimeBuffEffect, effect.minTimeBuffEffect, effect.maxTimeBuffEffect);
        float tickCooldown = GenericMethods.GetRandomValue(effect.tickCooldown, effect.randomTickCooldown, effect.minTickCooldown, effect.maxTickCooldown);

        statusController.ApplyEffect(effect, amount, timeBuffEffect, tickCooldown);
    }

    // Override UseItem methods to handle weapon-specific logic
    public override void UseItem(GameObject player, PlayerStatusController statusController)
    {
        // Weapons should not use the base item effects (no InteractionEffects)
        // Instead, they should be equipped through the WeaponController
        Debug.LogWarning("Weapons should be used through UseItem with WeaponController parameter");
    }

    public override void UseItem(GameObject player, PlayerStatusController statusController = null, WeaponController weaponController = null, AttackType attackType = AttackType.Normal)
    {
        Debug.Log($"WeaponSO.UseItem called with attackType: {attackType}");

        // Don't call base.UseItem() to avoid using InteractionEffects
        if (weaponController != null)
        {
            // Check if weapon is already equipped
            if (weaponController.EquippedWeapon != this)
            {
                Debug.Log("Equipping weapon first");
                weaponController.EquipWeapon(this);
            }

            // Always try to perform attack when using the weapon
            if (durability <= 0)
            {
                Debug.LogWarning("Weapon durability is 0, cannot attack");
                return;
            }

            Debug.Log($"Attempting to perform attack with type: {attackType}");
            durability -= durabilityReductionPerUse;
            weaponController.PerformAttack(player, attackType);
        }
        else
        {
            Debug.LogError("WeaponController is required to use weapons!");
        }
    }
    // Override to prevent base item animation from being applied
    protected override void ApplyItemAnimation(GameObject player)
    {
        // Weapons handle their animations through the WeaponController and PlayerAnimationController
    }

    // Animation Helpers
    public AnimationClip GetIdleAnimation() => animationSet?.IdleAnimation;
    public AnimationClip GetEquipAnimation() => animationSet?.EquipAnimation;
    public AnimationClip GetUnequipAnimation() => animationSet?.UnequipAnimation;

    public RuntimeAnimatorController GetAnimatorController()
    {
        return useCustomAnimatorController ? animationSet?.AnimatorController : null;
    }
}