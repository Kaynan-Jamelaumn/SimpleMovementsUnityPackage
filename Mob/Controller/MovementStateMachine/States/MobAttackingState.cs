using UnityEngine;

/// <summary>
/// Carries out the attack the brain chose: closes the distance / backs up / finds line of sight if needed (with a
/// time limit), faces the target, casts, then stays committed through the wind-up, launch and recovery of the
/// ability (the recovery is the player's window to punish). Releases the attack token when done.
/// </summary>
public class MobAttackingState : MobMovementState
{
    private MobAttackPlan plan;
    private bool castStarted;
    private float deadline;
    private int slotIndex;

    public MobAttackingState(MobMovementContext context, MobMovementStateMachine.EMobMovementState estate) : base(context, estate) { }

    public override bool IsCommitted
    {
        get
        {
            if (!castStarted)
                return false;
            AbilitySlot slot = Context.Abilities != null ? Context.Abilities.GetSlot(slotIndex) : null;
            if (slot == null)
                return false;
            AbilityPhase p = slot.Phase;
            return p == AbilityPhase.Casting || p == AbilityPhase.Launching || p == AbilityPhase.Active;
        }
    }

    public override bool CanCancelForDodge
    {
        get
        {
            AbilitySlot slot = Context.Abilities != null ? Context.Abilities.GetSlot(slotIndex) : null;
            return slot != null && slot.Phase == AbilityPhase.Casting;
        }
    }

    protected override void OnEnter()
    {
        castStarted = false;
        slotIndex = -1;
        if (Context.Abilities == null || !Brain.TryGetPlan(out plan) || plan.target == null)
        {
            Brain.NotifyAttackFinished(false, false);
            Finish();
            return;
        }
        slotIndex = plan.slot;
        deadline = Time.time + (plan.melee ? 2.5f : 3.5f);
    }

    protected override void OnExit()
    {
        if (castStarted)
        {
            // Leaving mid-wind-up (dodge): cancel the cast.
            AbilitySlot slot = Context.Abilities.GetSlot(slotIndex);
            if (slot != null && slot.Phase == AbilityPhase.Casting && !Self.IsDead)
                Context.Abilities.InterruptSlot(slot, CastInterruptReason.Manual);
        }
        if (!IsFinished)
            Brain.NotifyAttackFinished(castStarted, plan.melee);
    }

    protected override void OnUpdate(float dt)
    {
        if (castStarted)
        {
            UpdateCasting();
            return;
        }

        CombatEntity t = plan.target;
        if (t == null || !t.IsAlive || Brain.Target != t || Time.time > deadline)
        {
            Done(false);
            return;
        }

        float dist = CombatQuery.FlatDistance(Position, t.Position);
        if (dist > plan.maxRange)
        {
            Vector3 goal = CombatQuery.Predict(t, 0.3f);
            Motor.MoveTo(goal, MobMoveMode.Run, Mathf.Max(0.2f, plan.maxRange * 0.7f));
            FaceTarget();
            return;
        }
        if (dist < plan.minRange - 0.25f)
        {
            Vector3 away = CombatQuery.FlatDirection(t.Position, Position, -Context.Transform.forward);
            if (Motor.SamplePoint(Position + away * (plan.minRange - dist + 1f), 2f, out Vector3 back))
                Motor.MoveTo(back, MobMoveMode.Run, 0.2f);
            FaceTarget();
            return;
        }
        if (plan.needsLineOfSight && !Context.Perception.HasLineOfSightTo(t))
        {
            if (Motor.TryFindLineOfSightPoint(t, plan.minRange, plan.maxRange, out Vector3 los))
                Motor.MoveTo(los, MobMoveMode.Run, 0.3f);
            else
                Done(false);
            return;
        }

        // In position: cast.
        AbilitySlot slot = Context.Abilities.GetSlot(slotIndex);
        if (slot == null || slot.ability == null)
        {
            Done(false);
            return;
        }
        if (slot.ability.movementWhileCasting == CasterMovementRule.Stop)
            Motor.Stop();
        Motor.SnapFacing(Vector3.Lerp(Position + Context.Transform.forward, t.Position, 0.7f));
        if (Context.Abilities.TryCast(slotIndex, CastRequest.AtTarget(t), out CastFailReason reason))
        {
            castStarted = true;
            Context.Log($"uses {slot.ability.DisplayName} on {t.name}");
            return;
        }
        Context.Log($"could not use {slot.ability.DisplayName}: {reason}");
        Done(false);
    }

    private void UpdateCasting()
    {
        AbilitySlot slot = Context.Abilities.GetSlot(slotIndex);
        if (slot == null)
        {
            Done(true);
            return;
        }
        AbilityPhase phase = slot.Phase;
        if (phase == AbilityPhase.Casting || phase == AbilityPhase.Launching || phase == AbilityPhase.Active)
        {
            AbilityDefinition def = slot.ability;
            if (def != null && def.movementWhileCasting == CasterMovementRule.Stop && phase != AbilityPhase.Active)
                Motor.Stop();
            else if (def != null && def.movementWhileCasting != CasterMovementRule.Stop && Brain.Target != null && phase == AbilityPhase.Casting)
                FaceTarget();
            return;
        }
        Done(true);
    }

    private void Done(bool performed)
    {
        Brain.NotifyAttackFinished(performed, plan.melee);
        Finish();
    }
}
