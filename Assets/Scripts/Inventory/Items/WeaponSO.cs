
using System.Collections.Generic;
using UnityEngine;

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
    public AttackType Type;
    public float BaseDamage;
    public bool hasFixedBaseDamage;
    public float MinDamage;
    public float MaxDamage;
    public bool hasDifferentCriticalChange;
    public float CriticalChange;
    public string AnimationTrigger;
    public float StaminaCost;
    public float ComboResetTime;
    //public int ComboIndex;
}



[CreateAssetMenu(fileName = "Weapon", menuName = "Scriptable Objects/Item/Weapon")]
public class WeaponSO : ItemSO
{
    // Weapon Attributes
    [Header("Weapon Attributes")]
    [SerializeField] protected ToolType toolType;
    [SerializeField] private float minDamage;
    [SerializeField] private float maxDamage;
    [SerializeField] private float criticalDamageMultiplier = 1.0f;
    [SerializeField] private float criticalChance;
    [SerializeField] private float knockBack;
    [SerializeField] private float attackSpeed;


    [Header("Attack Pattern")]
    [SerializeField] private List<AttackPattern> attackPatterns;

    // Weapon Range
    [Header("Weapon Range")]
    [SerializeField] private float minRange;
    [SerializeField] private float maxRange;

    // Tool-Specific Attributes
    [Header("Tool Attributes")]
    [SerializeField] private float toolDamage;

    // Attack Effects
    [Header("Attack Effects")]
    [SerializeField] private List<AttackEffect> effects;
    [SerializeField] public AttackCast attackCast;
    private HashSet<Collider> detectedColliders = new HashSet<Collider>();

    // Attack Sound
    [Header("Audio")]
    [SerializeField] private AudioClip attackSound;

    public List<AttackEffect> Effects
    {
        get { return effects; }
    }

    public AudioClip AttackSound
    {
        get => attackSound;
    }
    public List<AttackPattern> AttackPatterns
    {
        get => attackPatterns;
    }
    public AttackCast AttackCast
    {
        get => attackCast;
    }

    public ToolType ToolType
    {
        get => toolType;
    }

    public float MinDamage
    {
        get => minDamage;
    }

    public float MaxDamage
    {
        get => maxDamage;
    }

    public float CriticalDamageMultiplier
    {
        get => criticalDamageMultiplier;
    }

    public float CriticalChace
    {
        get => criticalChance;
    }

    public float KnockBack
    {
        get => knockBack;
    }

    public float AttackSpeed
    {
        get => attackSpeed;
    }

    public float MinRange
    {
        get => minRange;
    }

    public float MaxRange
    {
        get => maxRange;
    }

    public float ToolDamage
    {
        get => toolDamage;
    }

    public void DealDamage(GameObject target)
    {
        float damage = CalculateDamage();
        // Apply damage logic to the target (e.g., reduce health)
    }

    private float CalculateDamage()

    {
        float baseDamage = Random.Range(minDamage, maxDamage);
        float criticalMultiplier = (Random.value <= criticalChance) ? criticalDamageMultiplier : 1.0f;
        return baseDamage * criticalMultiplier;
    }
    private float CalculateAttackRange()
    {
        float range = Random.Range(minRange, maxRange);
        return range;
    }



    public void ApplyEffectsToTarget(GameObject target, GameObject playerObject)
    {
        // Get the player's status controller
        PlayerStatusController statusController = playerObject.GetComponent<PlayerStatusController>();

        if (target.CompareTag("Player")) // Example: Player target
        {
            PlayerStatusController otherPlayerController = target.GetComponent<PlayerStatusController>();
            if (otherPlayerController != null)
                ApplyEffectsToController(otherPlayerController, statusController);
            
        }
        else if (target.CompareTag("Mob")) // Example: Mob target
        {
            MobStatusController mobController = target.GetComponent<MobStatusController>();
            if (mobController != null)
                ApplyEffectsToController(mobController, statusController);
           
        }
        else if (target.CompareTag("Collectable")) // Example: Collectable item target
        {
            CollectableItem collectableItem = target.GetComponent<CollectableItem>();
            if (collectableItem != null)
            {
                // Optionally, perform actions specific to attacking a collectable item
                // E.g., collect the item, update player inventory, etc.
                switch (toolType)
                {
                    case ToolType.Scythe:
                        if (collectableItem.toolTypeRequired == ToolType.Scythe)
                        {
                            collectableItem.TakeDamage(toolDamage);
                        }
                        // Add scythe-specific actions (e.g., harvesting crops)
                        break;

                    case ToolType.Axe:
                        if (collectableItem.toolTypeRequired == ToolType.Axe)  
                            collectableItem.TakeDamage(toolDamage);
                        
                        // Add axe-specific actions (e.g., chopping down trees)
                        break;

                    case ToolType.PickAxe:
                        if (collectableItem.toolTypeRequired == ToolType.PickAxe)
                        {
                            collectableItem.TakeDamage(toolDamage);
                        }
                        // Add pickaxe-specific actions (e.g., mining rocks)
                        break;

                    case ToolType.Fishingrod:
                        if (collectableItem.toolTypeRequired == ToolType.Fishingrod)
                        {
                            collectableItem.TakeDamage(toolDamage);
                        }
                        // Add fishing rod-specific actions (e.g., fishing)
                        break;

                    case ToolType.None:
                        // Add actions for a generic weapon without a specific tool type
                        break;
                }
            }
        }
       
    }
    private void ApplyEffectsToController<T>(T TargetThatGotHitStatusController, PlayerStatusController statusController = null)
    where T : BaseStatusController
    {
        foreach (var effect in effects)
        {
            if (Random.value <= effect.probabilityToApply)
            {
                if (effect.enemyEffect == false && statusController != null) // Buff: Apply to the player
                {
                    ApplyEffect(effect, statusController);
                }
                else
                {
                    ApplyEffect(effect, TargetThatGotHitStatusController);
                }
            }
        }
    }

    public void ApplyEffect<T>(AttackEffect effect, T statusController) where T : BaseStatusController
    {
        float amount = GenericMethods.GetRandomValue(effect.amount, effect.randomAmount, effect.minAmount, effect.maxAmount);
        float criticalMultiplier = (Random.value <= effect.criticalChance) ? effect.criticalDamageMultiplier : 1.0f;
        amount *= criticalMultiplier;

        float timeBuffEffect = GenericMethods.GetRandomValue(effect.timeBuffEffect, effect.randomTimeBuffEffect, effect.minTimeBuffEffect, effect.maxTimeBuffEffect);
        float tickCooldown = GenericMethods.GetRandomValue(effect.tickCooldown, effect.randomTickCooldown, effect.minTickCooldown, effect.maxTickCooldown);

        statusController.ApplyEffect(effect, amount, timeBuffEffect, tickCooldown);
    }


    public override void UseItem(GameObject player, PlayerStatusController statusController = null, WeaponController weaponController = null, AttackType attackType = AttackType.Normal )
    {
        base.UseItem(player, null);
        if (weaponController != null)
        {
            weaponController.EquipWeapon(this);
            if (!weaponController.AnimController.model.IsAttacking)
            {
                if (durability <= 0) return;
                durability -= durabilityReductionPerUse;
                weaponController.PerformAttack(player, AttackType.Normal); 
            }

        }

    }


}
