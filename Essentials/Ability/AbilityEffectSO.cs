using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static AbilityStateMachine;

/// <summary>
/// Legacy ability asset. Still supported: player slots and mobs convert it automatically to an
/// <see cref="AbilityDefinition"/> at runtime (see <see cref="LegacyAbilityConverter"/>), and
/// Tools > Abilities > Convert Selected Legacy Abilities turns it into a real Ability Definition asset you can extend with
/// the new shapes, projectiles, surges, walls, summons and AI settings.
/// </summary>
[CreateAssetMenu(fileName = "Ability", menuName = "Scriptable Objects/Ability/Ability (Legacy)")]
public class AbilityEffectSO : AbilitySO
{
    [SerializeField] public List<AttackEffect> effects;
    [Tooltip("A GameObject with ApplyEffect script Monobehaviour")][SerializeField] public GameObject fakeInstancerApplyEffects;
    [SerializeField] public GameObject particle;
    [Tooltip("Particle change the size according to the AttackCast Collider")][SerializeField] public bool particleShouldChangeSize;
    [Tooltip("A Child Particle from the Particle Object change the size according to the AttackCast Collider")][SerializeField] public bool subParticleShouldChangeSize;
    public bool onlyPrincipalParticle;
    [Tooltip("ON: the caster is never hit by its own ability (it is excluded from the area).")][SerializeField] public bool casterReceivePenalties;
    [Tooltip("Buffs (non-enemy effects) are applied to the caster even if it is outside the area.")][SerializeField] public bool casterReceivesBeneffitsBuffsEvenFromFarAway;
    [Tooltip("If the ability can damage/debuff more than one target within the AttackCast area")][SerializeField] public bool multiAreaEffect;
    [SerializeField] public bool canBeHitMoreThanOnce;
    [Tooltip("If the ability can damage/debuff more than one target within the AttackCast area, is there a maximum number of targets")][SerializeField] public bool hasMaxHitPerCollider;
    [Tooltip("The ability is only activated after a confirmation click")][SerializeField] public bool doesAbilityNeedsConfirmationClickToLaunch;
    [Tooltip("The ability spawns at the point clicked with the mouse")][SerializeField] public bool isAbilityTargetSpawnDecidedUponMouseClick;

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

    /// <summary>This ability converted to the new system (cached).</summary>
    public AbilityDefinition ToDefinition(bool forMob = false) => LegacyAbilityConverter.Convert(this, forMob);

    public void PopulateStateAvailabilityList()
    {
        EAbilityState[] allStates = (EAbilityState[])System.Enum.GetValues(typeof(EAbilityState));

        foreach (var state in allStates)
        {
            if (!_stateAvailability.Any(entry => entry.state == state))
            {
                _stateAvailability.Add(new StateAvailability
                {
                    state = state,
                    available = true
                });
            }
        }

        _stateAvailability.RemoveAll(entry => !System.Enum.IsDefined(typeof(EAbilityState), entry.state));
    }

    public void UpdateStateAvailabilityDict()
    {
        StateAvailabilityDict.Clear();
        foreach (var entry in _stateAvailability)
            StateAvailabilityDict[entry.state] = entry.available;
    }

    public void AbnormalUse(Transform targetxTransform, AttackEffect effect)
    {
        foreach (var attackCast in effect.attackCast)
        {
            Collider[] targets = attackCast.DetectObjects(targetxTransform);
            if (targets == null)
                continue;
            foreach (Collider targetCollider in targets)
            {
                if (targetCollider != null)
                    ApplyEffectsToController(targetCollider.gameObject, effect);
            }
        }
    }

    public override void Use(GameObject affectedTarget, AttackEffect effect)
    {
        ApplyEffectsToController(affectedTarget, effect);
    }

    public override void Use(Transform targetTransform, AttackEffect effect, List<AttackCast> attackCast, bool singleTarget = false, GameObject includedTarget = null, GameObject excludedTarget = null)
    {
        if (attackCast == null || attackCast.Count == 0)
        {
            if (includedTarget != null)
                ApplyEffectsToController(includedTarget, effect);
            return;
        }

        if (includedTarget != null)
        {
            ApplyEffectsToController(includedTarget, effect);
            if (singleTarget)
                return;
        }

        int casts = numberOfTargets > 1 ? attackCast.Count : 1;
        for (int c = 0; c < casts; c++)
        {
            AttackCast eachAttackCast = attackCast[c];
            if (eachAttackCast == null)
                continue;
            Collider[] targets = eachAttackCast.DetectObjects(targetTransform);
            if (targets == null || targets.Length == 0)
                continue;

            int victims = 0;
            var seen = new HashSet<GameObject>();
            foreach (Collider targetCollider in targets)
            {
                if (hasMaxHitPerCollider && effect.maxHitTimes > 0 && victims >= effect.maxHitTimes)
                    break;
                if (targetCollider == null)
                    continue;
                BaseStatusController status = targetCollider.GetComponentInParent<BaseStatusController>();
                GameObject target = status != null ? status.gameObject : targetCollider.gameObject;
                if (!seen.Add(target))
                    continue; // several colliders of the same character
                if (excludedTarget != null && target == excludedTarget)
                    continue;
                if (includedTarget != null && target == includedTarget)
                    continue; // already applied above
                ApplyEffectsToController(target, effect);
                victims++;
                if (singleTarget)
                    break;
            }
        }
    }

    public GameObject CheckContactCollider(Transform targetTransform, AttackCast attackCast, GameObject launcher = null)
    {
        Collider[] targets = attackCast.DetectObjects(targetTransform);
        if (targets == null)
            return null;
        foreach (Collider targetCollider in targets)
        {
            if (targetCollider == null)
                continue;
            GameObject target = targetCollider.gameObject;
            if (launcher != null)
            {
                if (target != launcher && !target.transform.IsChildOf(launcher.transform))
                    return target;
            }
            else if (CombatEntity.Resolve(targetCollider) != null)
            {
                return target; // any character (player or mob) - recognised by its Combat Entity, not by tags
            }
        }
        return null;
    }

    public void ApplyEffectsToController(GameObject targetGameObject, AttackEffect effect)
    {
        if (targetGameObject == null || effect == null)
            return;
        if (UnityEngine.Random.value <= effect.probabilityToApply)
            ApplyEffect(effect, targetGameObject.GetComponent<MonoBehaviour>());
    }

    /// <summary>
    /// Applies one effect with its random amount, critical hit, duration and tick values, through the target's status
    /// controller (which supports every effect type: HP, stamina, mana, speed, factors, regeneration...).
    /// </summary>
    public void ApplyEffect<T>(AttackEffect effect, T statusController) where T : MonoBehaviour
    {
        if (statusController == null || effect == null)
            return;
        float amount = GenericMethods.GetRandomValue(effect.amount, effect.randomAmount, effect.minAmount, effect.maxAmount);
        float criticalMultiplier = (UnityEngine.Random.value <= effect.criticalChance) ? effect.criticalDamageMultiplier : 1.0f;
        amount *= criticalMultiplier;
        float timeBuffEffect = GenericMethods.GetRandomValue(effect.timeBuffEffect, effect.randomTimeBuffEffect, effect.minTimeBuffEffect, effect.maxTimeBuffEffect);
        float tickCooldown = GenericMethods.GetRandomValue(effect.tickCooldown, effect.randomTickCooldown, effect.minTickCooldown, effect.maxTickCooldown);

        BaseStatusController status = statusController as BaseStatusController;
        if (status == null)
            status = statusController.GetComponentInParent<BaseStatusController>();
        if (status == null)
            return;

        try
        {
            status.ApplyEffect(effect, amount, timeBuffEffect, tickCooldown);
        }
        catch (Exception ex)
        {
            Debug.LogError($"An error occurred while applying the effect '{effect.effectName}' to {status.name}: {ex.Message}", status);
        }
    }
}
