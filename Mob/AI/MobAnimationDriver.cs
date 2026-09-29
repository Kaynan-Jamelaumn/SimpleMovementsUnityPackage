using UnityEngine;

/// <summary>
/// Drives a mob's Animator from what it is doing. Works with parameter-based controllers (Speed, IsMoving,
/// InCombat, MoveX/MoveY, Hit, Alert, Dodge, Stunned, Die - names set in the Mob Profile, missing ones are ignored)
/// and with the old controllers that have states named Idle / Moving / Chasing / Patrol (cross-faded by movement).
/// </summary>
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

    public MobAnimationDriver(MobMovementContext context)
    {
        ctx = context;
        SetAnimator(context.Anim);
    }

    public Animator Animator => animator;

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
        Vector3 v = motor.Velocity;
        float speed = new Vector2(v.x, v.z).magnitude;
        float run = Mathf.Max(0.1f, motor.RunSpeed);

        AnimatorParameterCache.SetFloat(animator, speedHash, speed, 0.08f);
        AnimatorParameterCache.SetFloat(animator, speedPercentHash, Mathf.Clamp01(speed / run), 0.08f);
        AnimatorParameterCache.SetBool(animator, movingHash, speed > 0.15f);
        AnimatorParameterCache.SetBool(animator, combatHash, ctx.Brain.InCombat);
        AnimatorParameterCache.SetBool(animator, stunnedHash, ctx.Entity.IsStunned);

        Vector3 local = ctx.Transform.InverseTransformDirection(v);
        AnimatorParameterCache.SetFloat(animator, moveXHash, Mathf.Clamp(local.x / run, -1f, 1f), 0.1f);
        AnimatorParameterCache.SetFloat(animator, moveYHash, Mathf.Clamp(local.z / run, -1f, 1f), 0.1f);

        if (ctx.Profile.crossFadeLegacyStates)
            UpdateLegacyState(speed);
    }

    private void UpdateLegacyState(float speed)
    {
        if (!hasIdle && !hasMoving && !hasChasing && !hasPatrol)
            return;
        int want;
        float walk = ctx.Motor.SpeedFor(MobMoveMode.Walk);
        if (speed < 0.15f || ctx.Entity.IsStunned)
            want = hasIdle ? idleState : 0;
        else if (speed > walk * 1.25f && hasChasing)
            want = chasingState;
        else if (ctx.Machine.CurrentStateKey == MobMovementStateMachine.EMobMovementState.Patrol && hasPatrol)
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

    public void OnHit()
    {
        if (animator == null || dead || Time.time < nextHitTime)
            return;
        nextHitTime = Time.time + 0.4f;
        AnimatorParameterCache.SetTrigger(animator, ctx.Profile.hitTrigger);
    }

    public void OnAlert() => AnimatorParameterCache.SetTrigger(animator, ctx.Profile.alertTrigger);

    public void OnDodge() => AnimatorParameterCache.SetTrigger(animator, ctx.Profile.dodgeTrigger);

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
