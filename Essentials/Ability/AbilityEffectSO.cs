using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static AbilityEffectSO;
using static AbilityStateMachine;




[CreateAssetMenu(fileName = "Ability", menuName = "Scriptable Objects/Ability/Ability")]
public class AbilityEffectSO : AbilitySO
{

    [SerializeField] public List<AttackEffect> effects;
    [Tooltip("A GameObject with ApplyEffect script Monobehaviour")][SerializeField] public GameObject fakeInstancerApplyEffects;
    [SerializeField] public GameObject particle;
    [Tooltip("Particle change the size according to the AttackCast Collider")][SerializeField] public bool particleShouldChangeSize;
    [Tooltip("A Child Particle from the Particle Object change the size according to the AttackCast Collider")][SerializeField] public bool subParticleShouldChangeSize;
    [Tooltip("Caster receives damage/debuff from it's own ability")][SerializeField] public bool casterIsImune;
    [Tooltip("Caster will receibe the buffs even outside the AttackCast Collider")][SerializeField] public bool isSelfTargetOrCasterReceivesBeneffitsBuffsEvenFromFarAway;
    [Tooltip("If the ability can damage/debuff more than one player within the AttackCast area")][SerializeField] public bool multiAreaEffect;
    [SerializeField] public bool canBeHitMoreThanOnce;
    [Tooltip("If the ability can damage/debuff more than one player within the AttackCast area does it have a max amount of targets")][SerializeField] public bool hasMaxHitPerCollider;
    // [SerializeField] public float maxVictimsPerCollider;
    //private ApplyEffects applyEffects = new ApplyEffects();

    [System.Serializable]
    public class StateAvailability
    {
        public EAbilityState state;
        public bool available;
    }


    [SerializeField]
    private List<StateAvailability> _stateAvailability = new List<StateAvailability>();
    private Dictionary<EAbilityState, bool> stateAvailabilityDict = new Dictionary<EAbilityState, bool>();

    public Dictionary<EAbilityState, bool> StateAvailabilityDict { get => stateAvailabilityDict; set => stateAvailabilityDict = value; }

    private void OnValidate()
    {
        UpdateStateAvailabilityDict();
    }

    private void UpdateStateAvailabilityDict()
    {
        StateAvailabilityDict.Clear();
        foreach (var entry in _stateAvailability)
        {
            if (!StateAvailabilityDict.ContainsKey(entry.state))
            {
                StateAvailabilityDict.Add(entry.state, entry.available);
            }
        }
    }



    public  void AbnormalUse(Transform targetxTransform, AttackEffect effect)
    {
        foreach (var attackCast in effect.attackCast)
        {

            Collider[] targets = attackCast.DetectObjects(targetxTransform);
            if (targets != null && targets.Length > 0)
            {
                foreach (Collider targetCollider in targets)
                {
                    GameObject target = targetCollider.gameObject;

                //if (target == targetTransform.gameObject) continue;
                if (target != null)
                ApplyEffectsToController(target, effect);
                }
            }
        }
    }
    public override void Use(GameObject affectedTarget, AttackEffect effect)
    {
        ApplyEffectsToController(affectedTarget, effect);
    }
    public override void Use(Transform targetTransform, AttackEffect effect, List<AttackCast> attackCast, bool singleTarget = false, GameObject includedTarget = null, GameObject excludedTarget = null)
    {
        if (includedTarget != null)
            ApplyEffectsToController(includedTarget, effect);

        if (numberOfTargets > 1)
        {
            foreach (var eachAttackCast in attackCast)
            {
                Collider[] targets = eachAttackCast.DetectObjects(targetTransform);
                if (targets == null || targets.Length == 0) return;

                int currentVictims = 1;
                foreach (Collider targetCollider in targets)
                {
                    if (hasMaxHitPerCollider && currentVictims > effect.maxHitTimes) break;
                    GameObject target = targetCollider.gameObject;
                    if (target != null && (excludedTarget == null || target != excludedTarget) && (includedTarget == null || target == includedTarget))
                    {
                        ApplyEffectsToController(target, effect);
                        if (singleTarget) break;
                    }
                }
            }
        }
        else
        {
            if (includedTarget != null)
                ApplyEffectsToController(includedTarget, effect);

            Collider[] targets = attackCast.First<AttackCast>().DetectObjects(targetTransform);
            if (targets == null || targets.Length == 0) return;

            foreach (Collider targetCollider in targets)
            {
                GameObject target = targetCollider.gameObject;
                if (target != null && (excludedTarget == null || target != excludedTarget) && (includedTarget == null || target == includedTarget))
                {
                    ApplyEffectsToController(target, effect);
                    if (singleTarget) break;
                }
            }
        }
    }
    public GameObject CheckContactCollider(Transform targetTransform, AttackCast attackCast, GameObject launcher = null)
    {
        Collider[] targets = attackCast.DetectObjects(targetTransform);
        if (targets != null && targets.Length > 0)
        {
            foreach (Collider targetCollider in targets)
            {
                GameObject target = targetCollider.gameObject;

                //if (target == targetTransform.gameObject) continue;
                if (launcher != null && launcher != target || launcher == null && target != null && target.CompareTag("Player") || target.CompareTag("Mob"))
                    return target;
                
            }
        }
        return null;

    }


    public void ApplyEffectsToController(GameObject targetGameObject, AttackEffect effect)
    {
        if (UnityEngine.Random.value <= effect.probabilityToApply)
            ApplyEffect(effect, targetGameObject.GetComponent<MonoBehaviour>());
        //if (effect.enemyEffect == false)
        //{
        //    ApplyEffect(effect, targetGameObject.GetComponent<MonoBehaviour>());
        //}
        //else
        //{
        //    ApplyEffect(effect, targetGameObject.GetComponent<MonoBehaviour>());
        //}
    }

    public void ApplyEffect<T>(AttackEffect effect, T statusController) where T : MonoBehaviour
    {
        float amount = GenericMethods.GetRandomValue(effect.amount, effect.randomAmount, effect.minAmount, effect.maxAmount);
        float criticalMultiplier = (UnityEngine.Random.value <= effect.criticalChance) ? effect.criticalDamageMultiplier : 1.0f;
        amount *= criticalMultiplier;
        float timeBuffEffect = GenericMethods.GetRandomValue(effect.timeBuffEffect, effect.randomTimeBuffEffect, effect.minTimeBuffEffect, effect.maxTimeBuffEffect);
        float tickCooldown = GenericMethods.GetRandomValue(effect.tickCooldown, effect.randomTickCooldown, effect.minTickCooldown, effect.maxTickCooldown);
        try
        {
            PlayerStatusController playerController = statusController.GetComponent<PlayerStatusController>();
            if (playerController != null)
                ApplyEffectToPlayer(effect, playerController, amount, timeBuffEffect, tickCooldown);

            MobStatusController mobController = statusController.GetComponent<MobStatusController>();
            if (mobController != null)
                ApplyEffectToMob(effect, mobController, amount, timeBuffEffect, tickCooldown);
        }
        catch (Exception ex)
        {
            // Handle the exception
            Debug.LogError("An error occurred while applying the effect: " + ex.Message);
        }

    }

    private void ApplyEffectToPlayer(AttackEffect effect, PlayerStatusController playerController, float amount, float timeBuffEffect, float tickCooldown)
    {
        switch (effect.effectType)
        {
            case AttackEffectType.Stamina:
                if (effect.timeBuffEffect == 0) playerController.StaminaManager.AddCurrentValue(effect.amount);
                else playerController.StaminaManager.AddStaminaEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.Hp:
                if (effect.timeBuffEffect == 0) playerController.HpManager.AddCurrentValue(effect.amount);
                else playerController.HpManager.AddHpEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);    
                break;
            case AttackEffectType.Food:
                if (effect.timeBuffEffect == 0) playerController.HungerManager.AddCurrentValue(effect.amount);
                else playerController.HungerManager.AddFoodEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.Drink:
                if (effect.timeBuffEffect == 0) playerController.ThirstManager.AddCurrentValue(effect.amount);
                else playerController.ThirstManager.AddDrinkEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
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
                if (effect.timeBuffEffect == 0) mobController.HealthManager.AddCurrentValue(effect.amount);
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
}