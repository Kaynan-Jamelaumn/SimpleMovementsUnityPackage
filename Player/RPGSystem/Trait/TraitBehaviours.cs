using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Everything a trait behaviour can reach on the player: status managers, movement, abilities, combat entity and
/// input. Created once per <see cref="TraitManager"/>.
/// </summary>
public sealed class TraitContext
{
    public TraitManager Manager { get; }
    public Transform Root { get; }
    public PlayerStatusController Status { get; private set; }
    public PlayerMovementModel Movement { get; private set; }
    public PlayerMovementController MovementController { get; private set; }
    public MovementStateMachine MovementMachine { get; private set; }
    public CharacterController Controller { get; private set; }
    public PlayerAbilityController Abilities { get; private set; }
    public AvailabilityStateMachine Availability { get; private set; }
    public PlayerInput Input { get; }

    private CombatEntity entity;

    public TraitContext(TraitManager manager, PlayerInput input)
    {
        Manager = manager;
        // The player's root (the object with the PlayerStatusController), not the scene object it may sit under.
        PlayerStatusController status = manager.GetComponentInParent<PlayerStatusController>();
        Root = status != null ? status.transform : manager.transform.root;
        Input = input;
        Refresh();
    }

    /// <summary>Finds the player's components (again).</summary>
    public void Refresh()
    {
        Status = Find<PlayerStatusController>();
        Movement = Status != null && Status.MovementModel != null ? Status.MovementModel : Find<PlayerMovementModel>();
        MovementController = Find<PlayerMovementController>();
        MovementMachine = Find<MovementStateMachine>();
        Controller = Movement != null && Movement.Controller != null ? Movement.Controller : Find<CharacterController>();
        Abilities = Find<PlayerAbilityController>();
        Availability = Find<AvailabilityStateMachine>();
    }

    private T Find<T>() where T : Component
    {
        T c = Manager.GetComponent<T>();
        if (c == null) c = Manager.GetComponentInParent<T>();
        if (c == null) c = Root.GetComponentInChildren<T>(true);
        return c;
    }

    /// <summary>The player's combat entity (created by the ability system; looked up again until found).</summary>
    public CombatEntity Entity
    {
        get
        {
            if (entity == null)
                entity = Abilities != null && Abilities.Entity != null ? Abilities.Entity : Root.GetComponentInChildren<CombatEntity>(true);
            return entity;
        }
    }

    public Transform Body => Movement != null && Movement.PlayerTransform != null ? Movement.PlayerTransform : Root;

    public bool IsGrounded => MovementController != null ? MovementController.IsGrounded() : Controller == null || Controller.isGrounded;
    public bool JumpPressed => Input != null && Input.Player.Jump.triggered;
    public bool JumpHeld => Input != null && Input.Player.Jump.IsPressed();
    public Vector2 MoveInput => Input != null ? Input.Player.Movement.ReadValue<Vector2>() : Vector2.zero;
    public bool CanMove => Availability == null || Availability.CanMove();
    public bool CanAct => Availability == null || Availability.CanAct();

    /// <summary>True while one of the player's abilities is winding up or launching.</summary>
    public bool IsCasting => Abilities != null && Abilities.IsCasting;

    public MovementStateMachine.EMovementState? MovementState =>
        MovementMachine != null && MovementMachine.CurrentState != null ? MovementMachine.CurrentState.StateKey : (MovementStateMachine.EMovementState?)null;

    /// <summary>Is there enough stamina (always true when the movement model does not consume stamina)?</summary>
    public bool HasStamina(float amount)
    {
        if (amount <= 0f || Status == null || Status.StaminaManager == null || (Movement != null && !Movement.ShouldConsumeStamina))
            return true;
        return Status.StaminaManager.HasEnougCurrentValue(amount);
    }

    /// <summary>Spends stamina if the player has it. Returns false (and spends nothing) otherwise.</summary>
    public bool TryConsumeStamina(float amount)
    {
        if (!HasStamina(amount))
            return false;
        if (amount > 0f && Status != null && Status.StaminaManager != null && (Movement == null || Movement.ShouldConsumeStamina))
            Status.StaminaManager.ConsumeStamina(amount);
        return true;
    }

    public float HealthRatio => Entity != null ? Entity.HealthRatio :
        Status != null && Status.HpManager != null && Status.HpManager.MaxValue > 0f ? Status.HpManager.CurrentValue / Status.HpManager.MaxValue : 1f;

    /// <summary>Current / max mana (1 when the player has no mana manager).</summary>
    public float ManaRatio
    {
        get
        {
            ManaManager m = Status != null ? Status.ManaManager : null;
            return m != null && m.MaxValue > 0f ? m.CurrentValue / m.MaxValue : 1f;
        }
    }

    /// <summary>Has the player taken or dealt damage in the last <paramref name="window"/> seconds?</summary>
    public bool InCombat(float window)
    {
        CombatEntity e = Entity;
        if (e == null)
            return false;
        float now = Time.time;
        return now - e.LastDamagedTime < window || now - e.LastDealtDamageTime < window;
    }
}

/// <summary>
/// An active or triggered part of a trait: a movement skill (double jump, wall climb, glide), an ability on a key
/// (barrier), a reaction (second wind, life steal, cheat death, thorns...) or modifiers that apply only in some
/// situations. Configured on the Trait asset; every player gets its own runtime copy.
/// </summary>
[Serializable]
public abstract class TraitBehaviour
{
    /// <summary>Strength multiplier (armor set enhancements).</summary>
    [NonSerialized] public float multiplier = 1f;

    /// <summary>True when the player triggers it (a key or a jump); false for automatic reactions.</summary>
    public virtual bool IsActive => false;

    public abstract string Describe();
    public virtual void OnAdded(TraitContext ctx) { }
    public virtual void OnRemoved(TraitContext ctx) { }
    public virtual void Tick(TraitContext ctx, float dt) { }

    /// <summary>Activates it from a script or UI button (active abilities). Returns true if something happened.</summary>
    public virtual bool TryActivate(TraitContext ctx) => false;

    /// <summary>What it is doing right now (shown in the Trait Manager inspector while playing).</summary>
    public virtual string LiveStatus => null;

    public virtual void Validate(List<string> errors, List<string> warnings) { }

    /// <summary>A per-player copy (configuration is shared, runtime state is not).</summary>
    public virtual TraitBehaviour CreateRuntimeCopy() => (TraitBehaviour)MemberwiseClone();

    /// <summary>
    /// True for behaviours that read <see cref="multiplier"/> every time they act, so a strength change needs no
    /// restart (their state - cooldowns, jumps used - is kept).
    /// </summary>
    protected virtual bool ReadsStrengthLive => false;

    /// <summary>
    /// The trait's strength changed (armor set enhancement). Return true if the change was applied in place; false
    /// (the default for behaviours that cache the strength) and the behaviour is replaced by a fresh copy.
    /// </summary>
    public virtual bool OnStrengthChanged(TraitContext ctx, float newMultiplier)
    {
        if (!ReadsStrengthLive)
            return false;
        multiplier = newMultiplier;
        return true;
    }

    public string MenuName => AbilityTypeNames.Nice(GetType());
}

// ====================================================================== movement
[Serializable, AbilityMenu("Movement/Double Jump", "Jump again in the air (one or more extra jumps).", 0)]
public class DoubleJumpTrait : TraitBehaviour
{
    [Tooltip("Extra jumps in the air before landing (1 = double jump, 2 = triple jump).")]
    [Min(1)] public int extraJumps = 1;
    [Tooltip("Strength of an air jump compared to a normal jump.")]
    [Range(0.2f, 2f)] public float jumpForceMultiplier = 0.9f;
    [Tooltip("Stamina per air jump (only when the Movement Model consumes stamina).")]
    [Min(0f)] public float staminaCost = 5f;
    [Tooltip("Seconds after the first jump before an air jump is allowed (stops one press from using both jumps).")]
    [Min(0f)] public float minTimeAfterJump = 0.12f;

    [NonSerialized] private int used;

    public override bool IsActive => true;
    protected override bool ReadsStrengthLive => true;
    public override string Describe() => extraJumps == 1 ? "Double jump: jump again in the air." : $"Jump {extraJumps} extra times in the air.";
    public override string LiveStatus => $"{Mathf.Max(0, extraJumps - used)}/{extraJumps} air jumps left";

    public override void Tick(TraitContext ctx, float dt)
    {
        if (ctx.Movement == null)
            return;
        if (ctx.IsGrounded)
        {
            used = 0;
            return;
        }
        if (!ctx.JumpPressed || used >= extraJumps || !ctx.CanMove)
            return;
        if (ctx.MovementMachine != null)
        {
            // Only from the Jumping state and not on the same press that started the jump.
            if (ctx.MovementState != MovementStateMachine.EMovementState.Jumping || ctx.MovementMachine.TimeInState < minTimeAfterJump)
                return;
        }
        if (!ctx.TryConsumeStamina(staminaCost))
            return;
        ctx.Movement.SuspendGravity = false;
        ctx.Movement.VerticalVelocity = ctx.Movement.JumpForce * jumpForceMultiplier * Mathf.Max(0.1f, multiplier);
        used++;
    }

    public override void Validate(List<string> errors, List<string> warnings)
    {
        if (extraJumps > 3)
            warnings.Add($"{extraJumps} extra jumps is a lot; players can climb almost anything.");
    }
}

[Serializable, AbilityMenu("Movement/Wall Climb", "Climb walls by holding Jump (or forward) while facing them.", 1)]
public class WallClimbTrait : TraitBehaviour
{
    [Tooltip("Climbing speed (metres per second).")]
    [Min(0.1f)] public float climbSpeed = 3f;
    [Tooltip("Stamina per second while climbing (only when the Movement Model consumes stamina).")]
    [Min(0f)] public float staminaPerSecond = 8f;
    [Tooltip("Longest climb before sliding off (seconds). 0 = no limit. Resets on landing.")]
    [Min(0f)] public float maxClimbTime = 3f;
    [Tooltip("ON: hold Jump to climb. OFF: push forward (movement input) into the wall.")]
    public bool holdJumpToClimb = true;
    [Tooltip("How far in front of the chest a wall is detected (metres).")]
    [Min(0.1f)] public float detectDistance = 0.8f;
    [Tooltip("Height of the detection ray above the feet (metres).")]
    [Min(0f)] public float chestHeight = 1f;
    [Tooltip("Surfaces that can be climbed.")]
    public LayerMask wallLayers = ~0;
    [Tooltip("How far from vertical a surface can be and still count as a wall (degrees).")]
    [Range(0f, 60f)] public float maxWallTilt = 30f;
    [Tooltip("Hop over the top of the wall (fraction of the jump force) when the wall ends while climbing.")]
    [Range(0f, 1.5f)] public float mantleBoost = 0.6f;

    [NonSerialized] private bool climbing;
    [NonSerialized] private float climbTime;

    public override bool IsActive => true;
    protected override bool ReadsStrengthLive => true;
    public override string Describe() =>
        $"Climb walls at {climbSpeed:0.#} m/s by {(holdJumpToClimb ? "holding Jump" : "pushing into them")}" + (maxClimbTime > 0f ? $" (up to {maxClimbTime:0.#}s)." : ".");
    public override string LiveStatus => climbing ? $"climbing ({climbTime:0.0}s)" : "ready";

    public override void Tick(TraitContext ctx, float dt)
    {
        if (ctx.Movement == null || ctx.Controller == null)
            return;
        bool grounded = ctx.IsGrounded;
        if (grounded)
            climbTime = 0f;

        bool wants = (holdJumpToClimb ? ctx.JumpHeld : ctx.MoveInput.y > 0.5f) && ctx.CanMove;
        bool hasWall = FindWall(ctx, out RaycastHit hit);
        bool timeLeft = maxClimbTime <= 0f || climbTime < maxClimbTime * Mathf.Max(0.1f, multiplier);

        if (!grounded && wants && hasWall && timeLeft && ctx.TryConsumeStamina(staminaPerSecond * dt))
        {
            climbing = true;
            ctx.Movement.SuspendGravity = true;
            ctx.Movement.VerticalVelocity = 0f;
            ctx.Controller.Move((Vector3.up * climbSpeed - hit.normal * 0.5f) * dt);
            climbTime += dt;
            return;
        }

        if (climbing)
        {
            climbing = false;
            ctx.Movement.SuspendGravity = false;
            if (!hasWall && wants && !grounded)
            {
                // Reached the top: hop over the edge.
                ctx.Movement.VerticalVelocity = ctx.Movement.JumpForce * mantleBoost;
                ctx.Controller.Move(ctx.Body.forward * 0.3f);
            }
        }
    }

    private bool FindWall(TraitContext ctx, out RaycastHit hit)
    {
        Transform body = ctx.Body;
        Vector3 forward = body.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-4f)
        {
            hit = default;
            return false;
        }
        Vector3 origin = body.position + Vector3.up * chestHeight;
        if (!Physics.Raycast(origin, forward.normalized, out hit, detectDistance, wallLayers, QueryTriggerInteraction.Ignore))
            return false;
        if (hit.transform.IsChildOf(ctx.Root))
            return false;
        return Mathf.Abs(hit.normal.y) <= Mathf.Sin(maxWallTilt * Mathf.Deg2Rad);
    }

    public override void OnRemoved(TraitContext ctx)
    {
        if (climbing && ctx.Movement != null)
            ctx.Movement.SuspendGravity = false;
        climbing = false;
    }
}

[Serializable, AbilityMenu("Movement/Glide", "Fall slowly while holding Jump in the air.", 2)]
public class GlideTrait : TraitBehaviour
{
    [Tooltip("Fastest fall speed while gliding (metres per second).")]
    [Min(0.1f)] public float maxFallSpeed = 2f;
    [Tooltip("Stamina per second while gliding (only when the Movement Model consumes stamina).")]
    [Min(0f)] public float staminaPerSecond = 0f;

    [NonSerialized] private bool gliding;

    public override bool IsActive => true;
    protected override bool ReadsStrengthLive => true;
    public override string Describe() => $"Hold Jump in the air to glide (fall at most {maxFallSpeed:0.#} m/s).";
    public override string LiveStatus => gliding ? "gliding" : "ready";

    public override void Tick(TraitContext ctx, float dt)
    {
        gliding = false;
        if (ctx.Movement == null || ctx.IsGrounded || !ctx.JumpHeld || !ctx.CanMove)
            return;
        float limit = -maxFallSpeed / Mathf.Max(0.1f, multiplier);
        if (ctx.Movement.VerticalVelocity >= limit)
            return;
        if (!ctx.TryConsumeStamina(staminaPerSecond * dt))
            return;
        ctx.Movement.VerticalVelocity = limit;
        gliding = true;
    }
}

// ====================================================================== abilities
[Serializable, AbilityMenu("Ability/Active Ability On A Key", "Gives the player an ability (a barrier, a blink, a heal...) cast with its own key.", 0)]
public class ActiveAbilityTrait : TraitBehaviour
{
    [Tooltip("The ability this trait gives (an Ability Definition asset). Presets create one for you.")]
    public AbilityDefinition ability;
    [Tooltip("Changes applied on top of the ability for this trait.")]
    public AbilityModifierSet modifiers = new AbilityModifierSet();
    [Tooltip("The key (input action) that casts it. Empty = only scripts/UI can use it (TraitManager.TryActivate).")]
    public InputActionReference input;

    [NonSerialized] private int slot = -1;
    [NonSerialized] private PlayerAbilityController owner;

    public override bool IsActive => true;
    public override string Describe() => ability != null
        ? $"Active: {ability.DisplayName}" + (input != null && input.action != null ? $" ({input.action.name})" : "") + (string.IsNullOrEmpty(ability.description) ? "." : $" - {ability.description}")
        : "Active ability (none assigned).";

    public override string LiveStatus
    {
        get
        {
            AbilitySlot s = owner != null ? owner.GetSlot(slot) : null;
            return s == null ? "no ability slot (needs a PlayerAbilityController)" : $"{s.Phase}, cooldown {s.CooldownRemaining:0.0}s";
        }
    }

    /// <summary>The slot of the player's ability controller this trait's ability lives in (-1 = none).</summary>
    public int SlotIndex => slot;

    private AbilityModifierSet BuildModifiers()
    {
        AbilityModifierSet mods = modifiers != null ? modifiers.Clone() : new AbilityModifierSet();
        if (!Mathf.Approximately(multiplier, 1f))
            mods = mods.CombinedWith(new AbilityModifierSet { damageMultiplier = multiplier, healMultiplier = multiplier, durationMultiplier = multiplier });
        return mods;
    }

    public override void OnAdded(TraitContext ctx)
    {
        owner = ctx.Abilities;
        if (owner == null || ability == null)
        {
            if (ability != null)
                Debug.LogWarning($"[Traits] '{ability.DisplayName}' needs a PlayerAbilityController on the player.", ctx.Manager);
            return;
        }
        // Reuses a free extra slot, so gaining and losing the trait never grows the slot list.
        slot = owner.AcquireExtraSlot(ability, BuildModifiers());
        if (input != null && input.action != null)
            input.action.Enable();
    }

    public override void Tick(TraitContext ctx, float dt)
    {
        if (owner == null || slot < 0 || input == null || input.action == null)
            return;
        if (input.action.triggered && ctx.CanAct)
            owner.TryCastFromInput(slot);
    }

    public override bool TryActivate(TraitContext ctx) => owner != null && slot >= 0 && ctx.CanAct && owner.TryCastFromInput(slot);

    /// <summary>A strength change only updates the slot's modifiers: its cooldown and charges are kept.</summary>
    public override bool OnStrengthChanged(TraitContext ctx, float newMultiplier)
    {
        multiplier = newMultiplier;
        AbilitySlot s = owner != null ? owner.GetSlot(slot) : null;
        if (s != null && s.ability == ability)
        {
            s.modifiers = BuildModifiers();
            s.InvalidateStats();
        }
        return true;
    }

    public override void OnRemoved(TraitContext ctx)
    {
        if (owner != null && slot >= 0)
            owner.ReleaseExtraSlot(slot);
        slot = -1;
    }

    public override void Validate(List<string> errors, List<string> warnings)
    {
        if (ability == null)
            errors.Add("Active Ability On A Key: no ability assigned.");
        if (input == null)
            warnings.Add("Active Ability On A Key: no input, so only scripts or UI (TraitManager.TryActivate) can use it.");
    }

    public override TraitBehaviour CreateRuntimeCopy()
    {
        var copy = (ActiveAbilityTrait)base.CreateRuntimeCopy();
        copy.slot = -1;
        copy.owner = null;
        return copy;
    }
}

// ====================================================================== reactions
[Serializable, AbilityMenu("Triggered/Second Wind", "When health drops low, heal instantly (with a cooldown).", 0)]
public class SecondWindTrait : TraitBehaviour
{
    [Tooltip("Triggers when health falls below this fraction of max health.")]
    [Range(0.05f, 0.9f)] public float healthThreshold = 0.25f;
    [Tooltip("Heal as a percentage of max health.")]
    [Range(1f, 100f)] public float healPercent = 30f;
    [Tooltip("Seconds of invulnerability when it triggers (0 = none).")]
    [Min(0f)] public float invulnerableSeconds = 1f;
    [Tooltip("Seconds before it can trigger again.")]
    [Min(1f)] public float cooldown = 90f;

    [NonSerialized] private float readyTime;
    [NonSerialized] private CombatEntity subscribed;
    [NonSerialized] private TraitContext context;

    protected override bool ReadsStrengthLive => true;
    public override string Describe() => $"Below {healthThreshold * 100f:0}% health: heal {healPercent:0}% of max health" +
                                         (invulnerableSeconds > 0f ? $" and become invulnerable for {invulnerableSeconds:0.#}s" : "") + $" (every {cooldown:0}s).";
    public override string LiveStatus => Time.time >= readyTime ? "ready" : $"cooldown {readyTime - Time.time:0}s";

    public override void OnAdded(TraitContext ctx)
    {
        context = ctx;
        readyTime = 0f;
    }

    public override void Tick(TraitContext ctx, float dt)
    {
        CombatEntity e = ctx.Entity;
        if (e != null && subscribed != e)
        {
            if (subscribed != null) subscribed.Damaged -= OnDamaged;
            subscribed = e;
            e.Damaged += OnDamaged;
        }
    }

    private void OnDamaged(DamageInfo info)
    {
        if (context == null || Time.time < readyTime || subscribed == null || !subscribed.IsAlive)
            return;
        if (subscribed.HealthRatio >= healthThreshold)
            return;
        readyTime = Time.time + cooldown;
        subscribed.ApplyHeal(subscribed.MaxHealth * healPercent / 100f * Mathf.Max(0.1f, multiplier), subscribed);
        if (invulnerableSeconds > 0f)
            subscribed.SetInvulnerable(invulnerableSeconds);
    }

    public override void OnRemoved(TraitContext ctx)
    {
        if (subscribed != null)
            subscribed.Damaged -= OnDamaged;
        subscribed = null;
        context = null;
    }

    public override TraitBehaviour CreateRuntimeCopy()
    {
        var copy = (SecondWindTrait)base.CreateRuntimeCopy();
        copy.subscribed = null;
        copy.context = null;
        return copy;
    }
}

[Serializable, AbilityMenu("Triggered/Life Steal", "Heal for a part of the damage the player deals.", 1)]
public class LifeStealTrait : TraitBehaviour
{
    [Tooltip("Percentage of damage dealt returned as healing.")]
    [Range(0.5f, 100f)] public float percent = 8f;

    [NonSerialized] private TraitContext context;

    protected override bool ReadsStrengthLive => true;
    public override string Describe() => $"Heal for {percent:0.#}% of the damage you deal.";

    public override void OnAdded(TraitContext ctx)
    {
        context = ctx;
        CombatEvents.Damaged += OnDamaged;
    }

    private void OnDamaged(DamageInfo info)
    {
        CombatEntity self = context != null ? context.Entity : null;
        if (self == null || info.source != self || info.target == self || info.amount <= 0f || !self.IsAlive)
            return;
        self.ApplyHeal(info.amount * percent / 100f * Mathf.Max(0.1f, multiplier), self);
    }

    public override void OnRemoved(TraitContext ctx)
    {
        CombatEvents.Damaged -= OnDamaged;
        context = null;
    }
}

[Serializable, AbilityMenu("Triggered/Cheat Death", "Survive a killing blow once in a while: keep some health and become invulnerable for a moment.", 2)]
public class CheatDeathTrait : TraitBehaviour
{
    [Tooltip("Health left after surviving, as a percentage of max health.")]
    [Range(1f, 100f)] public float healthLeftPercent = 20f;
    [Tooltip("Seconds of invulnerability after surviving (0 = none).")]
    [Min(0f)] public float invulnerableSeconds = 2f;
    [Tooltip("Seconds before it can save the character again.")]
    [Min(1f)] public float cooldown = 300f;

    [NonSerialized] private float readyTime;
    [NonSerialized] private CombatEntity subscribed;

    protected override bool ReadsStrengthLive => true;
    public override string Describe() => $"Survive a killing blow with {healthLeftPercent:0}% health" +
                                         (invulnerableSeconds > 0f ? $" and {invulnerableSeconds:0.#}s of invulnerability" : "") + $" (every {cooldown:0}s).";
    public override string LiveStatus => Time.time >= readyTime ? "ready" : $"cooldown {readyTime - Time.time:0}s";

    public override void OnAdded(TraitContext ctx)
    {
        readyTime = 0f;
        Subscribe(ctx.Entity);
    }

    public override void Tick(TraitContext ctx, float dt)
    {
        // The combat entity can appear after the trait (it is created by the ability system).
        if (subscribed == null)
            Subscribe(ctx.Entity);
    }

    private void Subscribe(CombatEntity e)
    {
        if (e == null || e == subscribed)
            return;
        if (subscribed != null)
            subscribed.LethalDamage -= OnLethalDamage;
        subscribed = e;
        e.LethalDamage += OnLethalDamage;
    }

    private bool OnLethalDamage(DamageInfo info)
    {
        if (subscribed == null || subscribed.Health == null || Time.time < readyTime)
            return false;
        readyTime = Time.time + cooldown;
        float target = subscribed.MaxHealth * Mathf.Clamp01(healthLeftPercent * Mathf.Max(0.1f, multiplier) / 100f);
        subscribed.Health.AddCurrentValue(Mathf.Max(1f, target) - subscribed.Health.CurrentValue);
        if (invulnerableSeconds > 0f)
            subscribed.SetInvulnerable(invulnerableSeconds);
        return true;
    }

    public override void OnRemoved(TraitContext ctx)
    {
        if (subscribed != null)
            subscribed.LethalDamage -= OnLethalDamage;
        subscribed = null;
    }

    public override TraitBehaviour CreateRuntimeCopy()
    {
        var copy = (CheatDeathTrait)base.CreateRuntimeCopy();
        copy.subscribed = null;
        return copy;
    }
}

[Serializable, AbilityMenu("Triggered/Thorns", "Send part of the damage taken back to the enemy that dealt it.", 3)]
public class ThornsTrait : TraitBehaviour
{
    [Tooltip("Percentage of the damage taken sent back to the attacker.")]
    [Range(1f, 200f)] public float percent = 20f;
    [Tooltip("Only attackers within this distance are hurt (metres). 0 = any distance (arrows and spells too).")]
    [Min(0f)] public float maxDistance = 4f;
    [Tooltip("Also reflect damage over time (poison, burning).")]
    public bool includeDamageOverTime = false;

    [NonSerialized] private CombatEntity subscribed;

    protected override bool ReadsStrengthLive => true;
    public override string Describe() => $"Enemies that hurt you{(maxDistance > 0f ? $" within {maxDistance:0.#} m" : "")} take {percent:0}% of that damage back.";

    public override void OnAdded(TraitContext ctx) => Subscribe(ctx.Entity);

    public override void Tick(TraitContext ctx, float dt)
    {
        if (subscribed == null)
            Subscribe(ctx.Entity);
    }

    private void Subscribe(CombatEntity e)
    {
        if (e == null || e == subscribed)
            return;
        if (subscribed != null)
            subscribed.Damaged -= OnDamaged;
        subscribed = e;
        e.Damaged += OnDamaged;
    }

    private void OnDamaged(DamageInfo info)
    {
        CombatEntity self = subscribed;
        CombatEntity attacker = info.source;
        if (self == null || attacker == null || attacker == self || info.isReflected || info.amount <= 0f || !attacker.IsAlive)
            return;
        if (info.isPeriodic && !includeDamageOverTime)
            return;
        if (CombatRelations.Get(self, attacker) != CombatRelation.Enemy)
            return;
        if (maxDistance > 0f && CombatQuery.FlatDistance(self.Position, attacker.Position) > maxDistance + attacker.Radius)
            return;
        Vector3 dir = attacker.Center - self.Center;
        attacker.ApplyDamage(new DamageInfo
        {
            amount = info.amount * percent / 100f * Mathf.Max(0.1f, multiplier),
            source = self,
            target = attacker,
            point = attacker.Center,
            direction = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.zero,
            isReflected = true,
        });
    }

    public override void OnRemoved(TraitContext ctx)
    {
        if (subscribed != null)
            subscribed.Damaged -= OnDamaged;
        subscribed = null;
    }

    public override TraitBehaviour CreateRuntimeCopy()
    {
        var copy = (ThornsTrait)base.CreateRuntimeCopy();
        copy.subscribed = null;
        return copy;
    }
}

[Serializable, AbilityMenu("Triggered/Regenerate Out Of Combat", "Recover health (and mana or stamina) after a few seconds without fighting.", 4)]
public class OutOfCombatRegenTrait : TraitBehaviour
{
    [Tooltip("Seconds without taking or dealing damage before it starts.")]
    [Min(0f)] public float delay = 6f;
    [Tooltip("Health restored per second, as a percentage of max health.")]
    [Range(0f, 20f)] public float healthPercentPerSecond = 2f;
    [Tooltip("Mana restored per second, as a percentage of max mana.")]
    [Range(0f, 20f)] public float manaPercentPerSecond = 0f;
    [Tooltip("Stamina restored per second, as a percentage of max stamina.")]
    [Range(0f, 20f)] public float staminaPercentPerSecond = 0f;

    [NonSerialized] private bool regenerating;
    [NonSerialized] private float pending;

    protected override bool ReadsStrengthLive => true;
    public override string Describe()
    {
        var parts = new List<string>();
        if (healthPercentPerSecond > 0f) parts.Add($"{healthPercentPerSecond:0.#}% health");
        if (manaPercentPerSecond > 0f) parts.Add($"{manaPercentPerSecond:0.#}% mana");
        if (staminaPercentPerSecond > 0f) parts.Add($"{staminaPercentPerSecond:0.#}% stamina");
        return $"After {delay:0.#}s out of combat, recover {(parts.Count > 0 ? string.Join(", ", parts) : "nothing")} per second.";
    }
    public override string LiveStatus => regenerating ? "regenerating" : "in combat";

    public override void Tick(TraitContext ctx, float dt)
    {
        CombatEntity e = ctx.Entity;
        regenerating = e != null && e.IsAlive && !ctx.InCombat(delay);
        if (!regenerating)
        {
            pending = 0f;
            return;
        }
        // Applied four times per second instead of every frame (fewer heal events for UI and listeners).
        pending += dt;
        if (pending < 0.25f)
            return;
        float seconds = pending;
        pending = 0f;
        float strength = Mathf.Max(0.1f, multiplier);
        if (healthPercentPerSecond > 0f && e.HealthRatio < 1f)
            e.ApplyHeal(e.MaxHealth * healthPercentPerSecond / 100f * seconds * strength, e);
        ManaManager mana = ctx.Status != null ? ctx.Status.ManaManager : null;
        if (manaPercentPerSecond > 0f && mana != null && mana.CurrentValue < mana.MaxValue)
            mana.RestoreMana(mana.MaxValue * manaPercentPerSecond / 100f * seconds * strength);
        StaminaManager stamina = ctx.Status != null ? ctx.Status.StaminaManager : null;
        if (staminaPercentPerSecond > 0f && stamina != null && stamina.CurrentValue < stamina.MaxValue)
            stamina.AddCurrentValue(stamina.MaxValue * staminaPercentPerSecond / 100f * seconds * strength);
    }
}

[Serializable, AbilityMenu("Triggered/On Kill", "Rewards for each enemy the player kills: health, mana, stamina and shorter cooldowns.", 5)]
public class OnKillRewardsTrait : TraitBehaviour
{
    [Tooltip("Health restored per kill, as a percentage of max health.")]
    [Range(0f, 100f)] public float healPercent = 5f;
    [Tooltip("Mana restored per kill.")]
    [Min(0f)] public float mana = 0f;
    [Tooltip("Stamina restored per kill.")]
    [Min(0f)] public float stamina = 10f;
    [Tooltip("Seconds taken off every ability cooldown per kill.")]
    [Min(0f)] public float cooldownReduction = 0f;
    [Tooltip("Only kills of enemies count (not allies or neutral creatures).")]
    public bool enemiesOnly = true;

    [NonSerialized] private TraitContext context;
    [NonSerialized] private int kills;

    protected override bool ReadsStrengthLive => true;
    public override string Describe()
    {
        var parts = new List<string>();
        if (healPercent > 0f) parts.Add($"heal {healPercent:0.#}% health");
        if (mana > 0f) parts.Add($"+{mana:0} mana");
        if (stamina > 0f) parts.Add($"+{stamina:0} stamina");
        if (cooldownReduction > 0f) parts.Add($"ability cooldowns -{cooldownReduction:0.#}s");
        return $"On kill: {(parts.Count > 0 ? string.Join(", ", parts) : "nothing")}.";
    }
    public override string LiveStatus => $"{kills} kill(s) rewarded";

    public override void OnAdded(TraitContext ctx)
    {
        context = ctx;
        CombatEvents.Killed += OnKilled;
    }

    private void OnKilled(CombatEntity victim, CombatEntity killer)
    {
        CombatEntity self = context != null ? context.Entity : null;
        if (self == null || killer != self || victim == null || victim == self || !self.IsAlive)
            return;
        if (enemiesOnly && CombatRelations.Get(self, victim) != CombatRelation.Enemy)
            return;
        kills++;
        float strength = Mathf.Max(0.1f, multiplier);
        if (healPercent > 0f)
            self.ApplyHeal(self.MaxHealth * healPercent / 100f * strength, self);
        if (mana > 0f && context.Status != null && context.Status.ManaManager != null)
            context.Status.ManaManager.RestoreMana(mana * strength);
        if (stamina > 0f && context.Status != null && context.Status.StaminaManager != null)
            context.Status.StaminaManager.AddCurrentValue(stamina * strength);
        if (cooldownReduction > 0f && context.Abilities != null)
            context.Abilities.ReduceAllCooldowns(cooldownReduction * strength);
    }

    public override void OnRemoved(TraitContext ctx)
    {
        CombatEvents.Killed -= OnKilled;
        context = null;
    }
}

// ====================================================================== conditional modifiers
[Serializable, AbilityMenu("Conditional/Modifiers While...", "Extra modifiers that apply only in a situation: low health, in the air, sprinting, in combat...", 0)]
public class ConditionalModifiersTrait : TraitBehaviour
{
    public enum Condition
    {
        HealthBelow,
        HealthAbove,
        StaminaBelow,
        InTheAir,
        OnTheGround,
        Sprinting,
        Crouching,
        StandingStill,
        Moving,
        // Added later (kept at the end so existing assets keep their values)
        ManaBelow,
        InCombat,
        OutOfCombat,
        Casting,
    }

    [Tooltip("When the modifiers below apply.")]
    public Condition condition = Condition.HealthBelow;
    [Tooltip("Health/Stamina/Mana conditions: the fraction of the maximum (0.3 = 30%). Ignored by the others.")]
    [Range(0f, 1f)] public float threshold = 0.3f;
    [Tooltip("In/Out Of Combat: seconds since the player last took or dealt damage.")]
    [Min(0.5f)] public float combatWindow = 5f;
    [Tooltip("Keeps the modifiers this many seconds after the condition stops, so a condition that flickers (on the ground / in the air on bumpy terrain) does not switch them every frame.")]
    [Min(0f)] public float lingerSeconds = 0.25f;
    [Tooltip("Modifiers active while the condition holds.")]
    public List<TraitModifier> modifiers = new List<TraitModifier> { TraitModifier.Percent(TraitStat.DamageTaken, -30f) };

    [NonSerialized] private TraitManager.AppliedModifiers applied;
    [NonSerialized] private float lastHeld = -999f;

    public override string Describe()
    {
        var parts = new List<string>();
        foreach (TraitModifier m in modifiers)
            if (m != null) parts.Add(m.Describe(multiplier));
        return $"While {ConditionText()}: {string.Join(", ", parts)}.";
    }

    public override string LiveStatus => applied != null ? "ACTIVE" : "inactive";

    private string ConditionText()
    {
        switch (condition)
        {
            case Condition.HealthBelow: return $"health is below {threshold * 100f:0}%";
            case Condition.HealthAbove: return $"health is above {threshold * 100f:0}%";
            case Condition.StaminaBelow: return $"stamina is below {threshold * 100f:0}%";
            case Condition.ManaBelow: return $"mana is below {threshold * 100f:0}%";
            case Condition.InTheAir: return "in the air";
            case Condition.OnTheGround: return "on the ground";
            case Condition.Sprinting: return "sprinting";
            case Condition.Crouching: return "crouching";
            case Condition.StandingStill: return "standing still";
            case Condition.InCombat: return "in combat";
            case Condition.OutOfCombat: return "out of combat";
            case Condition.Casting: return "casting an ability";
            default: return "moving";
        }
    }

    private bool Holds(TraitContext ctx)
    {
        switch (condition)
        {
            case Condition.HealthBelow: return ctx.HealthRatio < threshold;
            case Condition.HealthAbove: return ctx.HealthRatio > threshold;
            case Condition.StaminaBelow:
                StaminaManager st = ctx.Status != null ? ctx.Status.StaminaManager : null;
                return st != null && st.MaxValue > 0f && st.CurrentValue / st.MaxValue < threshold;
            case Condition.ManaBelow: return ctx.ManaRatio < threshold;
            case Condition.InTheAir: return !ctx.IsGrounded;
            case Condition.OnTheGround: return ctx.IsGrounded;
            case Condition.Sprinting: return ctx.MovementState == MovementStateMachine.EMovementState.Running;
            case Condition.Crouching: return ctx.MovementState == MovementStateMachine.EMovementState.Crouching;
            case Condition.StandingStill: return ctx.MoveInput.sqrMagnitude < 0.01f;
            case Condition.InCombat: return ctx.InCombat(combatWindow);
            case Condition.OutOfCombat: return !ctx.InCombat(combatWindow);
            case Condition.Casting: return ctx.IsCasting;
            default: return ctx.MoveInput.sqrMagnitude >= 0.01f;
        }
    }

    public override void Tick(TraitContext ctx, float dt)
    {
        if (Holds(ctx))
        {
            lastHeld = Time.time;
            if (applied == null)
                applied = ctx.Manager.ApplyModifiers(modifiers, multiplier, "Conditional");
        }
        else if (applied != null && Time.time - lastHeld >= lingerSeconds)
        {
            ctx.Manager.RevertModifiers(applied);
            applied = null;
        }
    }

    /// <summary>A strength change re-applies the active modifiers at the new strength (exactly reverted first).</summary>
    public override bool OnStrengthChanged(TraitContext ctx, float newMultiplier)
    {
        multiplier = newMultiplier;
        if (applied != null)
        {
            ctx.Manager.RevertModifiers(applied);
            applied = ctx.Manager.ApplyModifiers(modifiers, multiplier, "Conditional");
        }
        return true;
    }

    public override void OnRemoved(TraitContext ctx)
    {
        if (applied != null)
            ctx.Manager.RevertModifiers(applied);
        applied = null;
    }

    public override void Validate(List<string> errors, List<string> warnings)
    {
        if (modifiers.Count == 0)
            warnings.Add("Modifiers While...: no modifiers.");
    }

    public override TraitBehaviour CreateRuntimeCopy()
    {
        var copy = (ConditionalModifiersTrait)base.CreateRuntimeCopy();
        copy.applied = null;
        copy.lastHeld = -999f;
        return copy;
    }
}
