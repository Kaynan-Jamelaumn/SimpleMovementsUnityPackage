using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseAbilityHolder
{
    public enum AbilityState
    {
        Ready,
        Casting,
        Launching,
        Active,
        InCooldown
    }
    [SerializeField] public float activeTime;
    [SerializeField] public float cooldownTime;
    [SerializeField] public AbilityState abilityState = AbilityState.Ready;
    [SerializeField] public List<AttackCast> attackCast;
    [SerializeField] public GameObject particle;
    [Tooltip("chosen target to be targeted by the ability")][SerializeField] public Transform targetTransform;
}
