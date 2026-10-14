using System;
using UnityEngine;

/// <summary>
/// A one-shot reaction a mob plays. Where the AI runs it plays it by itself; client copies of a networked mob repeat
/// alerts and dodges the server reports, and flinch at the hits the server reports (<see cref="CombatEntity.ReplicatedDamage"/>).
/// </summary>
public enum MobAnimationEvent : byte
{
    /// <summary>Flinch after a hit.</summary>
    Hit = 1,
    /// <summary>Noticed an enemy and starts fighting.</summary>
    Alert = 2,
    /// <summary>Sidestep / roll.</summary>
    Dodge = 3,
}

/// <summary>
/// Drives a mob's Animator from what it is doing. Works with parameter-based controllers (Speed, IsMoving,
/// InCombat, MoveX/MoveY, Hit, Alert, Dodge, Stunned, Die - names set in the Mob Profile, missing ones are ignored)
/// and with the old controllers that have states named Idle / Moving / Chasing / Patrol (cross-faded by movement).
/// </summary>
/// <remarks>
/// On a client's copy of a networked mob (<see cref="IsReplica"/>) the AI does not run: the driver reads the state the
/// server sent (<see cref="ApplyReplicated"/>) and measures the speed from how the copy moves on screen, so the feet
/// match the movement the player actually sees. One-shot reactions arrive through <see cref="PlayReplicated"/>. This is
/// used instead of a NetworkAnimator: the damped parameters and legacy cross-fades are worked out on each machine, so
/// no animator traffic is sent and the animation stays smooth whatever the network update rate.
/// </remarks>
public sealed class MobAnimationDriver
{
    private readonly MobMovementContext ctx;
    private Animator animator;
    private int speedHash, speedPercentHash, movingHash, combatHash, moveXHash, moveYHash, stunnedHash;
    private int idleState, movingState, chasingState, patrolState, deathState;
    private bool hasIdle, hasMoving, hasChasing, hasPatrol, hasDeath;
    private int currentLegacy;
    private float legacyChangeTime;
    private float nextHitTime;
    private bool dead;
    private bool replica;
    private MobReplicaState replicated;
    private readonly MobMotionEstimator motion = new MobMotionEstimator();

    public MobAnimationDriver(MobMovementContext context)
    {
        ctx = context;
        SetAnimator(context.Anim);
    }

    public Animator Animator => animator;

    /// <summary>
    /// Raised when the mob plays an alert or a dodge by itself (server or single player) - the network layer repeats it
    /// on the clients. Not raised for hits (clients flinch at the hits the server reports) nor for reactions replayed with
    /// <see cref="PlayReplicated"/>.
    /// </summary>
    public event Action<MobAnimationEvent> Played;

    /// <summary>True while the driver shows a state received from the server instead of the local AI.</summary>
    public bool IsReplica => replica;

    /// <summary>The last state received from the server (replicas).</summary>
    public MobReplicaState Replicated => replicated;

    /// <summary>Switches between animating from the local AI and from the server's state (client copies).</summary>
    public void SetReplica(bool on)
    {
        replica = on;
        motion.Reset(ctx.Transform.position);
    }

    /// <summary>Stores the state the server sent (used while <see cref="IsReplica"/>).</summary>
    public void ApplyReplicated(MobReplicaState state) => replicated = state;

    /// <summary>Plays a one-shot reaction the server played (client copies). Does not raise <see cref="Played"/>.</summary>
    public void PlayReplicated(MobAnimationEvent e)
    {
        switch (e)
        {
            case MobAnimationEvent.Hit:
                PlayHit();
                break;
            case MobAnimationEvent.Alert:
                PlayAlert();
                break;
            case MobAnimationEvent.Dodge:
                PlayDodge();
                break;
        }
    }

    public void SetAnimator(Animator a)
    {
        animator = a;
        MobProfile p = ctx.Profile;
        speedHash = Animator.StringToHash(p.speedParameter ?? "");
        speedPercentHash = Animator.StringToHash(p.normalizedSpeedParameter ?? "");
        movingHash = Animator.StringToHash(p.movingBool ?? "");
        combatHash = Animator.StringToHash(p.inCombatBool ?? "");
        moveXHash = Animator.StringToHash(p.moveXParameter ?? "");
        moveYHash = Animator.StringToHash(p.moveYParameter ?? "");
        stunnedHash = Animator.StringToHash(p.stunnedBool ?? "");
        idleState = Animator.StringToHash("Idle");
        movingState = Animator.StringToHash("Moving");
        chasingState = Animator.StringToHash("Chasing");
        patrolState = Animator.StringToHash("Patrol");
        deathState = Animator.StringToHash("Death");
        RefreshStates();
    }

    private void RefreshStates()
    {
        hasIdle = AnimatorParameterCache.HasState(animator, 0, idleState);
        hasMoving = AnimatorParameterCache.HasState(animator, 0, movingState);
        hasChasing = AnimatorParameterCache.HasState(animator, 0, chasingState);
        hasPatrol = AnimatorParameterCache.HasState(animator, 0, patrolState);
        hasDeath = AnimatorParameterCache.HasState(animator, 0, deathState);
        if (!hasDeath)
        {
            deathState = Animator.StringToHash("Die");
            hasDeath = AnimatorParameterCache.HasState(animator, 0, deathState);
        }
    }

    /// <summary>Updates movement parameters (call every frame or every few frames far away).</summary>
    public void Tick(float dt)
    {
        if (animator == null || dead || !animator.isActiveAndEnabled)
            return;
        MobMotor motor = ctx.Motor;
        Vector3 v;
        float run;
        bool inCombat, stunned;
        if (replica)
        {
            // Client copy: the server sent the state; the speed is measured from the networked movement.
            v = motion.Step(ctx.Transform.position, dt);
            run = Mathf.Max(0.1f, replicated.runSpeed > 0.01f ? replicated.runSpeed : motor.RunSpeed);
            inCombat = replicated.Has(MobReplicaFlags.InCombat);
            stunned = replicated.Has(MobReplicaFlags.Stunned) || ctx.Entity.IsStunned;
        }
        else
        {
            v = motor.Velocity;
            run = Mathf.Max(0.1f, motor.RunSpeed);
            inCombat = ctx.Brain.InCombat;
            stunned = ctx.Entity.IsStunned;
        }
        float speed = new Vector2(v.x, v.z).magnitude;

        AnimatorParameterCache.SetFloat(animator, speedHash, speed, 0.08f);
        AnimatorParameterCache.SetFloat(animator, speedPercentHash, Mathf.Clamp01(speed / run), 0.08f);
        AnimatorParameterCache.SetBool(animator, movingHash, speed > 0.15f);
        AnimatorParameterCache.SetBool(animator, combatHash, inCombat);
        AnimatorParameterCache.SetBool(animator, stunnedHash, stunned);

        Vector3 local = ctx.Transform.InverseTransformDirection(v);
        AnimatorParameterCache.SetFloat(animator, moveXHash, Mathf.Clamp(local.x / run, -1f, 1f), 0.1f);
        AnimatorParameterCache.SetFloat(animator, moveYHash, Mathf.Clamp(local.z / run, -1f, 1f), 0.1f);

        if (ctx.Profile.crossFadeLegacyStates)
            UpdateLegacyState(speed, stunned);
    }

    private void UpdateLegacyState(float speed, bool stunned)
    {
        if (!hasIdle && !hasMoving && !hasChasing && !hasPatrol)
            return;
        int want;
        float walk = replica && replicated.walkSpeed > 0.01f ? replicated.walkSpeed : ctx.Motor.SpeedFor(MobMoveMode.Walk);
        MobMovementStateMachine.EMobMovementState state = replica ? replicated.State : ctx.Machine.CurrentStateKey;
        if (speed < 0.15f || stunned)
            want = hasIdle ? idleState : 0;
        else if (speed > walk * 1.25f && hasChasing)
            want = chasingState;
        else if (state == MobMovementStateMachine.EMobMovementState.Patrol && hasPatrol)
            want = patrolState;
        else
            want = hasMoving ? movingState : (hasChasing ? chasingState : 0);

        if (want == 0 || want == currentLegacy)
            return;
        if (Time.time - legacyChangeTime < 0.2f)
            return; // avoid flickering between states
        currentLegacy = want;
        legacyChangeTime = Time.time;
        animator.CrossFadeInFixedTime(want, ctx.Profile.crossFadeTime);
    }

    public void OnHit() => PlayHit();

    public void OnAlert()
    {
        PlayAlert();
        Played?.Invoke(MobAnimationEvent.Alert); // even without an animator here (a server may show nothing)
    }

    public void OnDodge()
    {
        PlayDodge();
        Played?.Invoke(MobAnimationEvent.Dodge);
    }

    private bool PlayHit()
    {
        if (animator == null || dead || Time.time < nextHitTime)
            return false;
        nextHitTime = Time.time + 0.4f;
        AnimatorParameterCache.SetTrigger(animator, ctx.Profile.hitTrigger);
        return true;
    }

    private void PlayAlert()
    {
        if (animator != null)
            AnimatorParameterCache.SetTrigger(animator, ctx.Profile.alertTrigger);
    }

    private void PlayDodge()
    {
        if (animator != null)
            AnimatorParameterCache.SetTrigger(animator, ctx.Profile.dodgeTrigger);
    }

    public void OnDeath()
    {
        if (animator == null || dead)
            return;
        dead = true;
        AnimatorParameterCache.SetBool(animator, movingHash, false);
        AnimatorParameterCache.SetFloat(animator, speedHash, 0f);
        if (AnimatorParameterCache.Has(animator, ctx.Profile.deathTrigger, AnimatorControllerParameterType.Trigger))
            AnimatorParameterCache.SetTrigger(animator, ctx.Profile.deathTrigger);
        else if (AnimatorParameterCache.Has(animator, ctx.Profile.deathTrigger, AnimatorControllerParameterType.Bool))
            AnimatorParameterCache.SetBool(animator, ctx.Profile.deathTrigger, true);
        else if (hasDeath)
            animator.CrossFadeInFixedTime(deathState, 0.15f);
    }

    public void OnRevive()
    {
        dead = false;
        currentLegacy = 0;
    }
}
