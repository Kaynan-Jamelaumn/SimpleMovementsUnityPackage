
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


[CreateAssetMenu(fileName = "Weapon", menuName = "Scriptable Objects/Item/Weapon")]
public class WeaponSO : ItemSO
{
    [SerializeField] protected ToolType toolType;
    [SerializeField] private float minDamage;
    [SerializeField] private float maxDamage;
    [SerializeField] private float criticalDamageMultiplier= 1.0f;
    [SerializeField] private float criticalChance;
    [SerializeField] private float knockBack;
    [SerializeField] private float attackSpeed;
    [SerializeField] private float minRange;
    [SerializeField] private float maxRange;
    [SerializeField] private float toolDamage;
    [Header("Attack Effects")]
    [SerializeField] private List<AttackEffect> effects;
    [SerializeField] public AttackCast attackCast;
    private HashSet<Collider> detectedColliders = new HashSet<Collider>();
    //[SerializeField] private ApplyEffects applyEffects = new ApplyEffects();
    [SerializeField] private AudioClip attackSound;

    public List<AttackEffect> Effects
    {
        get { return effects; }
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


    private void PerformAttack(GameObject playerObject)
    {
        PlayerAnimationModel playerAnimationModel = playerObject.GetComponent<PlayerAnimationModel>();
        if (playerAnimationModel.IsAttacking == true) return;
        playerAnimationModel.IsAttacking = true;


        // Clear the list of detected colliders before starting a new check
        detectedColliders.Clear();

        // Start collision detection
        StartCollisionDetection(playerObject);
    }

    private void StartCollisionDetection(GameObject playerObject)
    {
        // Start collision detection in the WeaponController
        WeaponController weaponControllexr = playerObject.GetComponent<WeaponController>();
        if (weaponControllexr != null)
            weaponControllexr.StartCollisionDetection(this, playerObject);

    }

    public void PerformCollisionDetection(Transform handGameObject, GameObject playerObject)
    {
        //Transform attackPosition = playerObject.transform; // You may need to adjust this position according to the weapon's position
        Collider[] colliders = attackCast.DetectObjects(handGameObject);
        if (colliders.Length > 0 && attackSound)
            playerObject.GetComponent<Player>().PlayerAudioSource.PlayOneShot(attackSound);
        foreach (Collider collider in colliders)
        {

        // Check if the collider has been detected before
        if (!detectedColliders.Contains(collider))
        {
            // If it hasn't been detected before, add it to the list of detected colliders
            detectedColliders.Add(collider);

            GameObject target = collider.gameObject;
            // Check if the target is the player character
            if (target == playerObject || target == null) continue;
                ApplyEffectsToTarget(target, playerObject);
        }
        }
    }

    private void ApplyEffectsToTarget(GameObject target, GameObject playerObject)
    {
        // Continue with the existing logic to check the type of target and perform corresponding actions
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
    private void ApplyEffectsToController<T>(T TargetThatGotHitStatusController, PlayerStatusController statusController = null) where T : MonoBehaviour
    {
        foreach (var effect in effects)
        {
            if (Random.value <= effect.probabilityToApply)
            {
                if (effect.enemyEffect == false)
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
    public void ApplyEffect<T>(AttackEffect effect, T statusController) where T : MonoBehaviour
    {
        float amount = GenericMethods.GetRandomValue(effect.amount, effect.randomAmount, effect.minAmount, effect.maxAmount);
        float criticalMultiplier = (Random.value <= effect.criticalChance) ? effect.criticalDamageMultiplier : 1.0f;
        amount *= criticalMultiplier;
        float timeBuffEffect = GenericMethods.GetRandomValue(effect.timeBuffEffect, effect.randomTimeBuffEffect, effect.minTimeBuffEffect, effect.maxTimeBuffEffect);
        float tickCooldown = GenericMethods.GetRandomValue(effect.tickCooldown, effect.randomTickCooldown, effect.minTickCooldown, effect.maxTickCooldown);
        PlayerStatusController playerController = statusController.GetComponent<PlayerStatusController>();
        if (playerController != null)
            ApplyEffectToPlayer(effect, playerController, amount, timeBuffEffect, tickCooldown);
        MobStatusController mobController = statusController.GetComponent<MobStatusController>();
        if (mobController != null)
            ApplyEffectToMob(effect, mobController, amount, timeBuffEffect, tickCooldown);
    }
    private void ApplyEffectToPlayer(AttackEffect effect, PlayerStatusController playerController, float amount, float timeBuffEffect, float tickCooldown)
    {
            switch (effect.effectType)
        {
            case AttackEffectType.Stamina:
                if (effect.timeBuffEffect == 0) playerController.StaminaManager.AddStamina(effect.amount);
                else playerController.StaminaManager.AddStaminaEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.Hp:
                if (effect.timeBuffEffect == 0) playerController.HpManager.AddHp(effect.amount);
                else playerController.HpManager.AddHpEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.Food:
                if (effect.timeBuffEffect == 0) playerController.FoodManager.AddFood(effect.amount);
                else playerController.FoodManager.AddFoodEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.Drink:
                if (effect.timeBuffEffect == 0) playerController.DrinkManager.AddDrink(effect.amount);
                else playerController.DrinkManager.AddDrinkEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.Weight:
                if (effect.timeBuffEffect == 0) playerController.WeightManager.AddWeight(effect.amount);
                else playerController.WeightManager.AddWeightEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.HpHealFactor:
                playerController.HpManager.AddHpHealFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.HpDamageFactor:
                playerController.HpManager.AddHpDamageFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.StaminaHealFactor:
                playerController.StaminaManager.AddStaminaHealFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.StaminaDamageFactor:
                playerController.StaminaManager.AddStaminaDamageFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.StaminaRegeneration:
                playerController.StaminaManager.AddStaminaRegenEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.HpRegeneration:
                playerController.HpManager.AddHpRegenEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
        }
    }

    private void ApplyEffectToMob(AttackEffect effect, MobStatusController mobController, float amount, float timeBuffEffect, float tickCooldown)
    {

        switch (effect.effectType)
        {
            case AttackEffectType.Hp:
                if (effect.timeBuffEffect == 0) mobController.HealthManager.AddHp(effect.amount);
                else mobController.HealthManager.AddHpEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.HpHealFactor:
                mobController.HealthManager.AddHpHealFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.HpDamageFactor:
                mobController.HealthManager.AddHpDamageFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.HpRegeneration:
                mobController.HealthManager.AddHpRegenEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
        }
    }
    public override void UseItem(GameObject player, PlayerStatusController statusController = null)
    {
        base.UseItem(player, null);
        PerformAttack(player);
    }

}
