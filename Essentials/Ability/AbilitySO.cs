﻿using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class AbilitySO : ScriptableObject
{
    [SerializeField] private new string name;
    [Tooltip("The ability only affects the user and it's unavaidoble")][SerializeField] public bool singleTargetSelfTarget;
    [Tooltip("number of AttackCasts to go to different places/player")][SerializeField] public int numberOfTargets = 1;
    
    [Header("Launchable Ability")]
    [Tooltip("if it can launches like a fireball magic")][SerializeField] public bool shouldLaunch;
    [Tooltip("speed it moves")][SerializeField] public float speed = 1f;
    [Tooltip("time until the ability disappears")][SerializeField] public float lifeSpan = 1f;
    [Tooltip("ability doesnt move in Y axis")][SerializeField] public bool isGroundFixedPosition;


    [Header("Ability Times")]
    [SerializeField] public float duration = 0.25f;
    [SerializeField] public float coolDown = 4f;
    [SerializeField] public float castDuration = 1f;
    [SerializeField] public float finalLaunchTime = 1f;

    [Header("Cast Positions")]

    [Tooltip("the attack position is the player/mob current position (transform)")][SerializeField] public bool isFixedPosition;
    [Tooltip("the attack follows the caster (player/mob) only while casting")][SerializeField] public bool isPartialPermanentTargetWhileCasting;
    [Tooltip("when ability launches caster is caugh upon (true= it's)")][SerializeField] public bool isPermanentTarget;
    [Tooltip("the attack cast, marks the player in it's last position")][SerializeField] public bool shouldMarkAtCast;
    public virtual void Use(GameObject affectedTarget,AttackEffect effect) {}
    public virtual void Use(Transform targetTransform, AttackEffect effect, List<AttackCast> attackCast, bool singleTarget = false, GameObject includedTarget = null, GameObject excludedTarget = null) { }


}