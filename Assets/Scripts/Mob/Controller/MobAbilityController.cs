using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
public class MobAbilityController : BaseAbilityController<AbilityHolder>
{
    [SerializeField] private MobActionsController mobActionController;
    //private Transform oldTransform;
    [SerializeField] private List<AbilityHolder> abilities;

    private void Awake()
    {
        if (!targetTransform) targetTransform = transform;
        if (!mobActionController) transform.GetComponent<MobActionsController>();
        foreach (var ability in abilities)
        {
            if (ability.particle) ability.abilityEffect.particle = ability.particle;


            if (!ability.targetTransform) ability.targetTransform = targetTransform;
        }

    }
    private void Start()
    {
        ValidateAssignments();
    }
    private void Update()
    {
        foreach (var ability in abilities)
        {
            if (mobActionController.CurrentPlayerTarget == null || ability.abilityState != AbilityHolder.AbilityState.Ready)
                continue;

            if (ability.abilityEffect.numberOfTargets > 1) // multi target abilities with  feet target spawn
            {
                List<Transform> targets = mobActionController.AvailableTargets();
                for (int i = 1; i != ability.abilityEffect.numberOfTargets; i++)
                {
                    _ = SetAbilityActions(ability, targets[i], ability.attackCast[i]);
                }
                //foreach (var attackCast in ability.attackCast) SetAbilityActions(ability, mobActionController.AvailableTarget(), attackCast);
            }
           // else if(ability.abilityEffect.numberOfLaunches > 1)//launchable abilities with multi bullets
             //   StartCoroutine(LaunchAbilities(ability, mobActionController.CurrentPlayerTarget.transform));
                
            else // single cast abilities/ one bullet launch
                _ = SetAbilityActions(ability, mobActionController.CurrentPlayerTarget.transform, ability.attackCast.First<AttackCast>());
        }
    }
    private void OnDrawGizmos()
    {
        foreach (var ability in abilities)
            foreach (var mobAttackEffect in ability.abilityEffect.effects)
                foreach (var attackCast in mobAttackEffect.attackCast)
                    attackCast.DrawGizmos(targetTransform);

    }

    private void ValidateAssignments()
    {
        Assert.IsNotNull(targetTransform, "Target Transform is not assigned in MobAbilityController.");
        Assert.IsNotNull(mobActionController, "MobActionsController is not assigned in MobAbilityController.");
    }
}