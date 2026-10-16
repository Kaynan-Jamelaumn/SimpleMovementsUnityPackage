using UnityEngine;

/// <summary>
/// Drives the Animator parameters of the character's general state, which nothing set before:
/// <list type="bullet">
/// <item><c>IsGrounded</c>, <c>IsFalling</c> (in the air and going down), <c>AirTime</c> (seconds off the ground);</item>
/// <item><c>JumpTrigger</c> when a jump starts, <c>LandTrigger</c> when the character lands after being in the air;</item>
/// <item><c>IsStunned</c> (cannot move but alive), <c>IsDead</c>, <c>DeathTrigger</c> once on death, <c>HitTrigger</c> on
/// every hit that did damage.</item>
/// </list>
/// Works on every machine that shows the character: the movement states run on every copy in a network session, and the
/// hits and deaths come from the presentation events (<see cref="CombatEntity.DamageFeedback"/>,
/// <see cref="CombatEntity.ReplicatedLifeChanged"/>). Parameters the Animator does not have are skipped.
/// </summary>
public class CharacterStateAnimationHandler
{
    private readonly PlayerAnimationModel model;
    private readonly PlayerMovementModel movementModel;
    private readonly MovementStateMachine machine;
    private readonly PlayerMovementController controller;
    private readonly AvailabilityStateMachine availability;
    private readonly CombatEntity entity;

    private float airTime;
    private bool wasAirborne;
    private bool deadShown;

    /// <summary>Seconds in the air before a landing counts (small hops over steps do not play a landing).</summary>
    public float MinAirTimeForLanding = 0.25f;

    /// <summary>
    /// A jump clip shorter than the flight used to end in mid-air (the Animator went back to standing or walking before
    /// the character landed). Its last airborne frame is now held until the character touches the ground, then the
    /// rest of the clip (the landing) plays. A Fall / In Air state of your own (named "Fall", "Falling", "InAir",
    /// "Air" or tagged "Air" / "Fall") is left alone. Falling off a ledge holds the same pose.
    /// </summary>
    public bool HoldJumpPoseInAir = true;

    /// <summary>Where the jump clip is held (0-1 of its length): its last frame that still looks airborne.</summary>
    public float JumpHoldAt = 0.85f;

    private int jumpState;        // the Animator state the jump plays in (found after the first jump)
    private int stateAtJump;      // the state before it
    private float sinceJump = -1f;
    private bool holding;
    private float holdTime;

    public CharacterStateAnimationHandler(PlayerAnimationModel model, PlayerMovementModel movementModel, MovementStateMachine machine)
    {
        this.model = model;
        this.movementModel = movementModel;
        this.machine = machine;
        Component anchor = movementModel != null ? (Component)movementModel : model;
        if (anchor != null)
        {
            controller = anchor.GetComponentInParent<PlayerMovementController>();
            if (controller == null) controller = anchor.GetComponentInChildren<PlayerMovementController>(true);
            availability = anchor.GetComponentInParent<AvailabilityStateMachine>();
            if (availability == null) availability = anchor.GetComponentInChildren<AvailabilityStateMachine>(true);
            entity = CombatEntity.Resolve(anchor.gameObject);
        }
    }

    /// <summary>Subscribes to the movement's state changes and the character's hits and deaths.</summary>
    public void Hook(bool on)
    {
        if (machine != null)
        {
            if (on) machine.StateChanged += OnStateChanged;
            else machine.StateChanged -= OnStateChanged;
        }
        if (entity != null)
        {
            if (on)
            {
                entity.DamageFeedback += OnHit;
                entity.Died += OnDied;
                entity.Revived += OnRevived;
                entity.ReplicatedLifeChanged += OnReplicatedLife;
            }
            else
            {
                entity.DamageFeedback -= OnHit;
                entity.Died -= OnDied;
                entity.Revived -= OnRevived;
                entity.ReplicatedLifeChanged -= OnReplicatedLife;
            }
        }
    }

    /// <summary>Every frame: the grounded / falling / stunned parameters.</summary>
    public void Tick(float dt)
    {
        if (model == null)
            return;
        bool swimming = machine != null && machine.CurrentState != null && machine.CurrentState.StateKey == MovementStateMachine.EMovementState.Swimming;
        bool grounded = swimming || controller == null || controller.IsGrounded();
        bool airborne = !grounded;
        if (airborne)
            airTime += dt;
        else
        {
            if (wasAirborne && airTime >= MinAirTimeForLanding)
                model.SetParameterSafe("LandTrigger", true);
            airTime = 0f;
        }
        wasAirborne = airborne;
        HoldJump(airborne, dt);

        float vertical = movementModel != null ? movementModel.VerticalVelocity : 0f;
        model.SetParameterSafe("IsGrounded", grounded);
        model.SetParameterSafe("IsFalling", airborne && vertical < -0.5f);
        model.SetParameterSafe("AirTime", airTime);

        bool dead = entity != null && entity.IsDead;
        bool stunned = !dead && availability != null && !availability.CanMove();
        model.SetParameterSafe("IsStunned", stunned);
        model.SetParameterSafe("IsDead", dead);
        if (dead && !deadShown)
            ShowDeath();
        else if (!dead)
            deadShown = false;
    }

    private void OnStateChanged(MovementStateMachine.EMovementState from, MovementStateMachine.EMovementState to)
    {
        if (model != null && to == MovementStateMachine.EMovementState.Jumping)
        {
            model.SetParameterSafe("JumpTrigger", true);
            Animator anim = model.Anim;
            stateAtJump = anim != null && anim.isActiveAndEnabled ? anim.GetCurrentAnimatorStateInfo(0).fullPathHash : 0;
            sinceJump = 0f;
        }
    }

    /// <summary>Keeps the jump's airborne pose while the character is in the air (see <see cref="HoldJumpPoseInAir"/>).</summary>
    private void HoldJump(bool airborne, float dt)
    {
        Animator anim = model.Anim;
        if (!HoldJumpPoseInAir || anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null || anim.layerCount == 0)
            return;
        bool dead = entity != null && entity.IsDead;
        AnimatorStateInfo cur = anim.GetCurrentAnimatorStateInfo(0);

        // Find the jump's state: the one the Animator goes to just after the jump started.
        if (sinceJump >= 0f)
        {
            sinceJump += dt;
            // A jump is a one-shot clip: a looping state reached first (idle, a run blend) is not it.
            if (anim.IsInTransition(0))
            {
                AnimatorStateInfo next = anim.GetNextAnimatorStateInfo(0);
                if (next.fullPathHash != stateAtJump && !next.loop) { jumpState = next.fullPathHash; sinceJump = -1f; }
            }
            else if (cur.fullPathHash != stateAtJump && !cur.loop) { jumpState = cur.fullPathHash; sinceJump = -1f; }
            if (sinceJump > 0.5f) sinceJump = -1f; // the jump has no state of its own
        }

        // Only a plain jump or fall: a dash, a roll, swimming or an attack in the air play their own animations.
        MovementStateMachine.EMovementState state = machine != null && machine.CurrentState != null ? machine.CurrentState.StateKey : MovementStateMachine.EMovementState.Jumping;
        bool ownAnimation = state == MovementStateMachine.EMovementState.Dashing || state == MovementStateMachine.EMovementState.Rolling ||
                            state == MovementStateMachine.EMovementState.Swimming || model.IsAttacking || model.IsDashing || model.IsRolling;
        if (!airborne || dead || jumpState == 0 || ownAnimation)
        {
            holding = false;
            return;
        }
        if (holding)
        {
            anim.Play(jumpState, 0, holdTime);
            return;
        }
        if (cur.fullPathHash == jumpState)
        {
            if (cur.loop) return; // a looping air state needs no help
            bool leaving = anim.IsInTransition(0) && !IsAirState(anim.GetNextAnimatorStateInfo(0)) && anim.GetNextAnimatorStateInfo(0).fullPathHash != jumpState;
            if (leaving || cur.normalizedTime >= JumpHoldAt)
            {
                holding = true;
                holdTime = Mathf.Min(cur.normalizedTime, JumpHoldAt);
                anim.Play(jumpState, 0, holdTime);
            }
            return;
        }
        // Left the jump (or never entered it: walked off a ledge) while still in the air, into a ground state.
        if (IsAirState(cur) || (anim.IsInTransition(0) && IsAirState(anim.GetNextAnimatorStateInfo(0))))
            return;
        if (airTime > 0.3f)
        {
            holding = true;
            holdTime = JumpHoldAt;
            anim.CrossFade(jumpState, 0.15f, 0, holdTime);
        }
    }

    private static bool IsAirState(AnimatorStateInfo s) =>
        s.IsTag("Air") || s.IsTag("Fall") || s.IsTag("InAir") || s.IsName("Fall") || s.IsName("Falling") || s.IsName("InAir") ||
        s.IsName("In Air") || s.IsName("Air") || s.IsName("Fall Loop") || s.IsName("Falling Loop") || s.IsName("FallLoop") || s.IsName("Airborne");

    private void OnHit(DamageInfo info)
    {
        if (model == null || entity == null || entity.IsDead || info.amount <= 0f)
            return;
        model.SetParameterSafe("HitTrigger", true);
    }

    private void OnDied(CombatEntity killer) => ShowDeath();

    private void OnRevived() => deadShown = false;

    private void OnReplicatedLife(bool dead)
    {
        if (dead) ShowDeath();
        else deadShown = false;
    }

    private void ShowDeath()
    {
        if (deadShown || model == null)
            return;
        deadShown = true;
        model.SetParameterSafe("IsDead", true);
        model.SetParameterSafe("DeathTrigger", true);
    }
}
