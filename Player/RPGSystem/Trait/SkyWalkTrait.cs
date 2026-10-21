using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What <see cref="SkyWalkTrait"/> spends while the character walks on air.</summary>
public enum SkyWalkCost
{
    Mana,
    Stamina,
    /// <summary>Mana while there is some, then stamina.</summary>
    ManaThenStamina,
    /// <summary>Free.</summary>
    Nothing,
}

/// <summary>
/// Sky Walk: the character can walk on air. In the air it stops falling and keeps its height; holding Jump rises,
/// holding Crouch goes down. Every second in the air costs mana or stamina; when it runs out the character falls and,
/// given by a power, the power ends.
/// </summary>
[Serializable, AbilityMenu("Movement/Sky Walk", "Walk on air: no falling, Jump rises, Crouch descends; costs mana or stamina per second in the air.", 4)]
public class SkyWalkTrait : TraitBehaviour, IPowerEndsItself
{
    [Tooltip("What walking on air costs.")]
    public SkyWalkCost cost = SkyWalkCost.Mana;
    [Tooltip("Mana or stamina per second in the air.")]
    [Min(0f)] public float costPerSecond = 8f;
    [Tooltip("Metres per second up while holding Jump.")]
    [Min(0f)] public float riseSpeed = 3f;
    [Tooltip("Metres per second down while holding Crouch.")]
    [Min(0f)] public float descendSpeed = 4f;
    [Tooltip("Ends the power (when given by one) as soon as the mana or stamina runs out.")]
    public bool endWhenEmpty = true;

    [NonSerialized] private bool walking, empty, suspended;

    public override bool IsActive => true;
    public override bool IsMovementSimulation => true;
    protected override bool ReadsStrengthLive => true;

    public bool PowerFinished => endWhenEmpty && empty;

    public override string Describe() =>
        "walk on air (Jump rises, Crouch descends)" + (cost == SkyWalkCost.Nothing || costPerSecond <= 0f ? "" : $", {costPerSecond:0.#} {CostName}/s");

    public override string LiveStatus => empty ? "out of " + CostName : walking ? "walking on air" : "ready";

    private string CostName => cost == SkyWalkCost.Stamina ? "stamina" : cost == SkyWalkCost.ManaThenStamina ? "mana/stamina" : "mana";

    public override void Tick(TraitContext ctx, float dt)
    {
        walking = false;
        if (ctx.Movement == null)
            return;
        if (ctx.IsGrounded || !ctx.CanMove || empty)
        {
            Release(ctx);
            return;
        }
        if (!Pay(ctx, costPerSecond * dt / Mathf.Max(0.1f, multiplier)))
        {
            empty = true;
            Release(ctx);
            return;
        }
        walking = true;
        suspended = true;
        ctx.Movement.SuspendGravity = true;
        float v = ctx.Movement.VerticalVelocity;
        if (ctx.JumpHeld)
            ctx.Movement.VerticalVelocity = Mathf.Max(v, riseSpeed);
        else if (ctx.CrouchHeld)
            ctx.Movement.VerticalVelocity = -descendSpeed;
        else if (v <= 0f)
            ctx.Movement.VerticalVelocity = 0f; // keep the height (a jump still goes up to its top first)
        else
            ctx.Movement.VerticalVelocity = Mathf.Max(0f, v - 9.81f * dt);
    }

    private bool Pay(TraitContext ctx, float amount)
    {
        if (cost == SkyWalkCost.Nothing || amount <= 0f)
            return true;
        ManaManager mana = ctx.Status != null ? ctx.Status.ManaManager : null;
        bool useMana = cost == SkyWalkCost.Mana || cost == SkyWalkCost.ManaThenStamina;
        if (useMana && mana != null && mana.HasEnougCurrentValue(amount))
        {
            mana.ConsumeMana(amount);
            return true;
        }
        if (cost == SkyWalkCost.Mana)
            return mana == null; // no mana manager at all: free
        return ctx.TryConsumeStamina(amount);
    }

    private void Release(TraitContext ctx)
    {
        if (!suspended || ctx.Movement == null)
            return;
        suspended = false;
        ctx.Movement.SuspendGravity = false;
    }

    public override void OnRemoved(TraitContext ctx) => Release(ctx);

    public override void Validate(List<string> errors, List<string> warnings)
    {
        if (cost != SkyWalkCost.Nothing && costPerSecond <= 0f)
            warnings.Add("Sky Walk costs nothing per second: it can last forever.");
    }
}
