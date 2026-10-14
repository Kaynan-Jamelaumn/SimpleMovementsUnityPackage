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
            model.SetParameterSafe("JumpTrigger", true);
    }

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
