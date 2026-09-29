using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Something that can be given abilities (the player absorbing a mob's ability, a quest reward...).</summary>
public interface IAbilityReceiver
{
    /// <summary>Could this grant be accepted right now?</summary>
    bool CanReceive(AbilityGrant grant);

    /// <summary>Accepts the grant into a slot. Returns false if it was refused.</summary>
    bool Receive(AbilityGrant grant, out int slotIndex);
}

/// <summary>
/// Runs abilities for one character: slots, cooldowns, charges, costs, casting, aim tracking, telegraphs,
/// interruption and animation hooks, all updated in one Update with no coroutines. The player
/// (<see cref="PlayerAbilityController"/>) and mobs (<see cref="MobAbilityController"/>) derive from it; it can also
/// be used as is on traps, turrets or bosses driven by your own scripts via <see cref="TryCast(int, CastRequest)"/>.
/// </summary>
[DefaultExecutionOrder(-10)]
public class AbilityCaster : MonoBehaviour, IAbilityReceiver
{
    [Header("Abilities")]
    [Tooltip("The abilities this character can use. Each slot has its own cooldown, charges and modifiers.")]
    [SerializeField] protected List<AbilitySlot> slots = new List<AbilitySlot>();

    [Tooltip("Maximum number of slots (absorbed abilities add new slots up to this number). 0 = no limit.")]
    [SerializeField, Min(0)] protected int maxSlots = 0;

    [Header("Casting")]
    [Tooltip("Where projectiles and beams come out (hand, mouth, staff tip). Empty = in front of the chest.")]
    [SerializeField] protected Transform castPoint;

    [Tooltip("Animator that receives the abilities' cast/release triggers. Empty = found automatically.")]
    [SerializeField] protected Animator animator;

    [Tooltip("Minimum seconds between the start of two abilities (any slot).")]
    [SerializeField, Min(0f)] protected float globalCooldown = 0f;

    [Tooltip("Degrees per second used to face the aim while casting.")]
    [SerializeField, Min(0f)] protected float turnSpeed = 720f;

    [Tooltip("Draw telegraphs (ground warnings) for this character's abilities.")]
    [SerializeField] protected bool showTelegraphs = true;

    [Tooltip("Log casts, failures and interruptions to the Console.")]
    [SerializeField] protected bool debugLog = false;

    // ------------------------------------------------------------------ runtime
    public CombatEntity Entity { get; private set; }
    public IReadOnlyList<AbilitySlot> Slots => slots;

    /// <summary>
    /// Modifiers applied to EVERY ability of this character on top of each slot's own (traits such as "+15% ability
    /// damage" or "-10% cooldowns", buffs). Set with <see cref="SetCharacterModifiers"/>.
    /// </summary>
    public AbilityModifierSet CharacterModifiers { get; private set; }

    /// <summary>Changes whenever <see cref="CharacterModifiers"/> is replaced (slots re-read their stats).</summary>
    public int CharacterModifiersVersion { get; private set; }

    /// <summary>Replaces the character-wide ability modifiers (null = none).</summary>
    public void SetCharacterModifiers(AbilityModifierSet modifiers)
    {
        CharacterModifiers = modifiers;
        CharacterModifiersVersion++;
    }
    public int SlotCount => slots.Count;
    public Animator Animator => animator;

    /// <summary>The most recent cast still in its Casting or Launching phase (null if none).</summary>
    public AbilityCastInstance CurrentCast { get; private set; }

    /// <summary>Movement speed multiplier imposed by the abilities being cast (1 = free).</summary>
    public float MoveSpeedMultiplier { get; private set; } = 1f;

    public float LastCastStartTime { get; private set; } = -999f;

    /// <summary>A slot changed phase (slot, previous, new).</summary>
    public event Action<AbilitySlot, AbilityPhase, AbilityPhase> SlotPhaseChanged;
    /// <summary>A slot's ability was set, replaced or cleared (slot index).</summary>
    public event Action<int> SlotChanged;
    public event Action<AbilityCastInstance> CastStarted;
    public event Action<AbilityCastInstance> CastReleased;
    public event Action<AbilityCastInstance, CastInterruptReason> CastInterrupted;
    /// <summary>A cast attempt failed (slot index, reason) - e.g. show "Not enough mana".</summary>
    public event Action<int, CastFailReason> CastFailed;
    /// <summary>An ability was received (absorbed) into a slot.</summary>
    public event Action<AbilityGrant, int> AbilityReceived;

    private ManaManager mana;
    private StaminaManager stamina;
    private bool initialized;

    private static readonly List<ResolvedShape> shapeBuffer = new List<ResolvedShape>(8);

    // ------------------------------------------------------------------ lifecycle
    protected virtual void Awake()
    {
        Initialize();
    }

    /// <summary>Caches components and prepares the slots (safe to call more than once).</summary>
    public void Initialize()
    {
        if (initialized)
            return;
        initialized = true;

        Entity = CombatEntity.Resolve(gameObject);
        if (Entity == null)
            Entity = CombatEntity.GetOrAdd(gameObject);
        Entity.Caster = this;
        Entity.Damaged += OnEntityDamaged;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (Entity.Status is PlayerStatusController player)
        {
            mana = player.ManaManager;
            stamina = player.StaminaManager;
        }
        if (mana == null) mana = GetComponentInChildren<ManaManager>();
        if (stamina == null) stamina = GetComponentInChildren<StaminaManager>();

        if (slots == null)
            slots = new List<AbilitySlot>();
        for (int i = 0; i < slots.Count; i++)
            PrepareSlot(i);
        OnInitialized();
    }

    /// <summary>Hook for derived casters: add slots (converted legacy abilities, defaults) here.</summary>
    protected virtual void OnInitialized() { }

    protected virtual void OnDestroy()
    {
        if (Entity != null)
        {
            Entity.Damaged -= OnEntityDamaged;
            if (Entity.Caster == this)
                Entity.Caster = null;
        }
        InterruptAll(CastInterruptReason.Disabled);
    }

    protected virtual void OnDisable()
    {
        InterruptAll(CastInterruptReason.Disabled);
        MoveSpeedMultiplier = 1f;
        if (Entity != null)
            Entity.CastMoveMultiplier = 1f;
    }

    private void PrepareSlot(int i)
    {
        AbilitySlot s = slots[i];
        if (s == null)
        {
            s = new AbilitySlot();
            slots[i] = s;
        }
        s.Index = i;
        s.Owner = this;
        s.PhaseChanged -= OnSlotPhaseChanged;
        s.PhaseChanged += OnSlotPhaseChanged;
        if (s.modifiers == null)
            s.modifiers = new AbilityModifierSet();
        s.InvalidateStats();
        if (s.ability != null && Application.isPlaying)
            s.ability.Prewarm();
    }

    private void OnSlotPhaseChanged(AbilitySlot slot, AbilityPhase from, AbilityPhase to) => SlotPhaseChanged?.Invoke(slot, from, to);

    // ------------------------------------------------------------------ slots
    public AbilitySlot GetSlot(int index) => index >= 0 && index < slots.Count ? slots[index] : null;

    public int IndexOf(AbilityDefinition ability)
    {
        if (ability == null)
            return -1;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && slots[i].ability == ability)
                return i;
        }
        return -1;
    }

    /// <summary>Adds a new slot and returns its index.</summary>
    public int AddSlot(AbilityDefinition ability, AbilityModifierSet modifiers = null)
    {
        Initialize();
        slots.Add(new AbilitySlot(ability, modifiers));
        int i = slots.Count - 1;
        PrepareSlot(i);
        SlotChanged?.Invoke(i);
        return i;
    }

    /// <summary>Puts an ability into a slot (interrupting whatever the slot was doing).</summary>
    public void SetSlot(int index, AbilityDefinition ability, AbilityModifierSet modifiers = null)
    {
        Initialize();
        if (index < 0)
            return;
        while (slots.Count <= index)
        {
            slots.Add(new AbilitySlot());
            PrepareSlot(slots.Count - 1);
        }
        AbilitySlot s = slots[index];
        if (s.cast != null)
            InterruptSlot(s, CastInterruptReason.Replaced);
        s.Grant = null;
        s.label = "";
        s.Set(ability, modifiers);
        if (ability != null)
            ability.Prewarm();
        SlotChanged?.Invoke(index);
    }

    /// <summary>Empties a slot (keeps the other slots' indices).</summary>
    public void ClearSlot(int index) => SetSlot(index, null, null);

    public void ResetAllCooldowns()
    {
        for (int i = 0; i < slots.Count; i++)
            slots[i].ResetCooldown();
    }

    /// <summary>Shortens the cooldown of slot <paramref name="index"/> by <paramref name="seconds"/> (see <see cref="AbilitySlot.ReduceCooldown"/>).</summary>
    public void ReduceCooldown(int index, float seconds) => GetSlot(index)?.ReduceCooldown(seconds);

    /// <summary>Shortens every slot's cooldown by <paramref name="seconds"/> (e.g. a "cooldowns -2s on kill" trait).</summary>
    public void ReduceAllCooldowns(float seconds)
    {
        if (seconds <= 0f)
            return;
        for (int i = 0; i < slots.Count; i++)
            slots[i]?.ReduceCooldown(seconds);
    }

    /// <summary>
    /// Cancels every ability still winding up (Casting phase). Released abilities are not affected. Uses the
    /// abilities' own interrupt rules (refund, charge given back, cooldown on interrupt). Returns true if any was cancelled.
    /// </summary>
    public bool CancelCasting()
    {
        bool any = false;
        for (int i = 0; i < slots.Count; i++)
        {
            AbilitySlot s = slots[i];
            if (s != null && s.cast != null && s.Phase == AbilityPhase.Casting)
            {
                InterruptSlot(s, CastInterruptReason.Manual);
                any = true;
            }
        }
        return any;
    }

    /// <summary>True if any slot is in a phase that blocks the others.</summary>
    public bool IsBusy => BusyExcept(null);

    /// <summary>True while any slot is casting or launching.</summary>
    public bool IsCasting
    {
        get
        {
            for (int i = 0; i < slots.Count; i++)
            {
                AbilityPhase p = slots[i].Phase;
                if (p == AbilityPhase.Casting || p == AbilityPhase.Launching)
                    return true;
            }
            return false;
        }
    }

    private bool BusyExcept(AbilitySlot except)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            AbilitySlot s = slots[i];
            if (s == except || s.ability == null)
                continue;
            if (s.ability.BlocksOthersDuring(s.Phase))
                return true;
        }
        return false;
    }

    /// <summary>Where projectiles and beams leave the caster.</summary>
    public virtual Vector3 CastPointPosition
    {
        get
        {
            if (castPoint != null)
                return castPoint.position;
            if (Entity != null)
                return Entity.BasePosition + Vector3.up * (Entity.Height * 0.65f) + transform.forward * (Entity.Radius * 0.8f);
            return transform.position + Vector3.up;
        }
    }

    /// <summary>The target used when a cast request does not name one (mobs: their current target).</summary>
    protected virtual CombatEntity DefaultTarget => null;

    /// <summary>Mobs: can the player absorb this character's abilities? (see <see cref="AbilityAbsorption"/>).</summary>
    public virtual bool CanBeAbsorbedFrom => false;

    /// <summary>Mobs: multiplies the absorb chance of this character's abilities.</summary>
    public virtual float AbsorbChanceMultiplier => 1f;

    // ------------------------------------------------------------------ casting
    public bool TryCast(int index, CastRequest request) => TryCast(index, request, out _);

    public bool TryCast(AbilityDefinition ability, CastRequest request) => TryCast(IndexOf(ability), request, out _);

    /// <summary>Starts the ability in slot <paramref name="index"/>. Returns false (with the reason) if it cannot.</summary>
    public bool TryCast(int index, CastRequest request, out CastFailReason reason)
    {
        Initialize();
        reason = CanCast(index, request);
        if (reason != CastFailReason.None)
        {
            RaiseCastFailed(index, reason);
            return false;
        }
        StartCast(slots[index], request);
        return true;
    }

    /// <summary>Reports a failed cast attempt (<see cref="CastFailed"/>), e.g. for a "Not enough mana" message.</summary>
    protected void RaiseCastFailed(int index, CastFailReason reason)
    {
        if (debugLog)
            Debug.Log($"[{name}] cannot cast slot {index}: {reason}", this);
        CastFailed?.Invoke(index, reason);
    }

    /// <summary>Checks whether slot <paramref name="index"/> could be cast with this request.</summary>
    public CastFailReason CanCast(int index, CastRequest request)
    {
        CastFailReason ready = CheckSlotReady(index);
        if (ready != CastFailReason.None)
            return ready;

        AbilitySlot s = slots[index];
        AbilityDefinition def = s.ability;
        AbilityStats stats = s.Stats;
        CombatEntity target = request.target != null ? request.target : DefaultTarget;
        AbilityTargeting t = def.targeting;
        if (t.mode == AbilityTargetingMode.Unit)
        {
            if (target == null || !target.IsAlive || !CombatRelations.Passes(t.targetFilter, Entity, target))
                return CastFailReason.NoTarget;
        }

        bool checkRange = target != null && request.target != null && (t.mode == AbilityTargetingMode.Unit || (t.mode == AbilityTargetingMode.Point && !request.hasPoint));
        if (checkRange)
        {
            float d = CombatQuery.FlatDistance(Entity.Position, target.Position);
            if (d - target.Radius > def.Range(stats) + 0.25f)
                return CastFailReason.OutOfRange;
            if (t.minRange > 0f && d < def.MinRange(stats))
                return CastFailReason.TooClose;
        }
        if (t.requireLineOfSight && target != null && t.mode != AbilityTargetingMode.Self && !CombatQuery.HasLineOfSight(CastPointPosition, target.AimPosition))
            return CastFailReason.NoLineOfSight;

        return CheckExtra(s, request);
    }

    /// <summary>
    /// The part of <see cref="CanCast"/> that does not depend on where the ability is aimed: the slot, the caster's
    /// state (dead, stunned, silenced), phase, cooldown, global cooldown, other abilities blocking it and resources.
    /// None = it could start now if the aim allows. Used to queue inputs pressed slightly too early.
    /// </summary>
    public CastFailReason CheckSlotReady(int index)
    {
        AbilitySlot s = GetSlot(index);
        if (s == null || s.ability == null)
            return CastFailReason.NoAbility;
        if (!s.enabled || !isActiveAndEnabled)
            return CastFailReason.Disabled;
        if (Entity == null)
            return CastFailReason.Disabled;
        if (Entity.IsDead)
            return CastFailReason.Dead;
        if (Entity.IsStunned)
            return CastFailReason.Stunned;
        if (Entity.IsSilenced)
            return CastFailReason.Silenced;
        if (s.Phase != AbilityPhase.Ready && s.Phase != AbilityPhase.InCooldown)
            return CastFailReason.Busy;
        if (!s.IsReady)
            return CastFailReason.OnCooldown;
        if (Time.time < LastCastStartTime + globalCooldown)
            return CastFailReason.OnCooldown;
        if (BusyExcept(s))
            return CastFailReason.Busy;
        if (!HasResources(s.ability, s.Stats))
            return CastFailReason.NotEnoughResource;
        return CastFailReason.None;
    }

    /// <summary>True for failures that go away by themselves shortly (cooldown, global cooldown, another ability busy).</summary>
    public static bool IsTimingReason(CastFailReason reason) => reason == CastFailReason.OnCooldown || reason == CastFailReason.Busy;

    /// <summary>Extra conditions for derived casters (return None to allow).</summary>
    protected virtual CastFailReason CheckExtra(AbilitySlot slot, in CastRequest request) => CastFailReason.None;

    private void StartCast(AbilitySlot s, in CastRequest request)
    {
        float now = Time.time;
        AbilityDefinition def = s.ability;
        var cast = new AbilityCastInstance(this, Entity, s, def, s.modifiers);
        cast.Target = request.target != null ? request.target : DefaultTarget;
        InitializeAim(cast, request);

        PayCost(cast);
        s.charges = s.Charges - 1;
        s.lastUseTime = now;
        s.aiReadyTime = now + def.ai.extraAICooldown;
        s.cast = cast;
        LastCastStartTime = now;
        CurrentCast = cast;

        if (def.sharedCooldown > 0f)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != s)
                    slots[i].lockedUntil = Mathf.Max(slots[i].lockedUntil, now + def.sharedCooldown);
            }
        }

        float castTime = def.CastTime(s.Stats);
        cast.StartTime = now;
        cast.ReleaseTime = now + castTime;
        cast.Phase = AbilityPhase.Casting;

        AbilityPresentation pres = def.presentation;
        AnimatorParameterCache.SetTrigger(animator, pres.castTrigger);
        AnimatorParameterCache.SetBool(animator, pres.castingBool, true);
        if (pres.castVfx != null)
            cast.castVfx = AbilityPool.Spawn(pres.castVfx, CastPointPosition, transform.rotation, castPoint != null ? castPoint : transform);
        if (pres.castSound != null)
            AbilityPool.PlaySound(pres.castSound, transform.position, pres.volume);

        if (debugLog)
            Debug.Log($"[{name}] cast {def.DisplayName} (cast {castTime:0.##}s) at {(cast.Target != null ? cast.Target.name : cast.AimPoint.ToString())}", this);

        s.SetPhase(AbilityPhase.Casting, castTime);
        CreateTelegraphs(cast, castTime);
        OnCastStarted(cast);
        CastStarted?.Invoke(cast);
        CombatEvents.RaiseCastStarted(cast);
        if (def.HasTag(AbilityTag.Melee))
            CombatEvents.RaiseMeleeSwing(Entity);

        if (castTime <= 0f)
            Release(s);
    }

    /// <summary>Hook for derived casters when a cast starts.</summary>
    protected virtual void OnCastStarted(AbilityCastInstance cast) { }

    /// <summary>Hook for derived casters when a cast is released.</summary>
    protected virtual void OnCastReleased(AbilityCastInstance cast) { }

    /// <summary>Hook for derived casters when a cast finishes normally (end of the Active phase).</summary>
    protected virtual void OnCastFinished(AbilityCastInstance cast) { }

    // ------------------------------------------------------------------ aim
    /// <summary>Sets the initial target point and direction of a cast from the request.</summary>
    protected virtual void InitializeAim(AbilityCastInstance c, in CastRequest r)
    {
        AbilityDefinition def = c.Definition;
        AbilityTargeting t = def.targeting;
        Vector3 from = Entity.Position;
        Vector3 forward = CombatQuery.FlatDirection(Vector3.zero, transform.forward, Vector3.forward);
        float range = def.Range(c.Stats);

        switch (t.mode)
        {
            case AbilityTargetingMode.Self:
                c.AimPoint = Entity.BasePosition;
                if (r.hasDirection)
                    c.AimDirection = CombatQuery.FlatDirection(Vector3.zero, r.direction, forward);
                else if (r.hasPoint)
                    c.AimDirection = CombatQuery.FlatDirection(from, r.point, forward);
                else if (c.Target != null)
                    c.AimDirection = CombatQuery.FlatDirection(from, c.Target.Position, forward);
                else
                    c.AimDirection = forward;
                return;

            case AbilityTargetingMode.Unit:
                if (c.Target != null)
                {
                    ComputeDesiredAim(c, c.Target, out Vector3 up, out Vector3 ud);
                    c.AimPoint = up;
                    c.AimDirection = ud;
                    return;
                }
                break;

            case AbilityTargetingMode.Point:
                if (r.hasPoint)
                {
                    c.AimPoint = ClampAimPoint(def, c.Stats, from, r.point);
                    c.AimDirection = CombatQuery.FlatDirection(from, c.AimPoint, forward);
                    return;
                }
                if (c.Target != null)
                {
                    ComputeDesiredAim(c, c.Target, out Vector3 pp, out Vector3 pd);
                    c.AimPoint = pp;
                    c.AimDirection = pd;
                    return;
                }
                break;

            case AbilityTargetingMode.Direction:
                if (r.hasDirection)
                {
                    c.AimDirection = CombatQuery.FlatDirection(Vector3.zero, r.direction, forward);
                    c.AimPoint = r.hasPoint ? ClampAimPoint(def, c.Stats, from, r.point) : SnapIfNeeded(def, from + c.AimDirection * range);
                    return;
                }
                if (r.hasPoint)
                {
                    c.AimDirection = CombatQuery.FlatDirection(from, r.point, forward);
                    c.AimPoint = ClampAimPoint(def, c.Stats, from, r.point);
                    return;
                }
                if (c.Target != null)
                {
                    ComputeDesiredAim(c, c.Target, out Vector3 dp, out Vector3 dd);
                    c.AimPoint = dp;
                    c.AimDirection = dd;
                    return;
                }
                break;
        }

        c.AimDirection = forward;
        c.AimPoint = SnapIfNeeded(def, from + forward * Mathf.Max(1f, range));
    }

    /// <summary>Updates the aim while casting according to the ability's Aim Lock.</summary>
    protected virtual void UpdateAim(AbilityCastInstance c, float dt)
    {
        AbilityTargeting t = c.Definition.targeting;
        switch (t.aimLock)
        {
            case AbilityAimLock.LockAtCastStart:
                return;

            case AbilityAimLock.FollowCaster:
            {
                Vector3 forward = CombatQuery.FlatDirection(Vector3.zero, transform.forward, c.AimDirection);
                float dist = CombatQuery.FlatDistance(c.CasterPosition, c.AimPoint);
                c.AimDirection = forward;
                c.AimPoint = SnapIfNeeded(c.Definition, Entity.Position + forward * dist);
                return;
            }

            default:
            {
                if (c.Target == null || !c.Target.IsAlive)
                    return;
                ComputeDesiredAim(c, c.Target, out Vector3 point, out Vector3 dir);
                TurnAimToward(c, point, dir, dt);
                return;
            }
        }
    }

    /// <summary>Moves the aim toward a desired point/direction, limited by the ability's Aim Turn Rate.</summary>
    protected void TurnAimToward(AbilityCastInstance c, Vector3 point, Vector3 dir, float dt)
    {
        float rate = c.Definition.targeting.aimTurnRate;
        if (rate <= 0f)
        {
            c.AimDirection = dir;
            c.AimPoint = point;
            return;
        }
        c.AimDirection = Vector3.RotateTowards(c.AimDirection, dir, rate * Mathf.Deg2Rad * dt, 0f);
        c.AimDirection.y = 0f;
        c.AimDirection = c.AimDirection.sqrMagnitude > 1e-6f ? c.AimDirection.normalized : dir;
        float dist = Mathf.Max(1f, CombatQuery.FlatDistance(c.CasterPosition, c.AimPoint));
        c.AimPoint = Vector3.MoveTowards(c.AimPoint, point, rate * Mathf.Deg2Rad * dist * dt);
    }

    /// <summary>Where to aim at <paramref name="target"/>, leading it by the ability's Lead Target setting.</summary>
    protected void ComputeDesiredAim(AbilityCastInstance c, CombatEntity target, out Vector3 point, out Vector3 dir)
    {
        AbilityDefinition def = c.Definition;
        Vector3 from = Entity != null ? Entity.Position : c.CasterPosition;
        Vector3 p = target.Position;
        float lead = def.targeting.leadTarget;
        if (lead > 0f)
        {
            float t = ImpactDelay(c, CombatQuery.FlatDistance(from, p));
            p = Vector3.Lerp(p, CombatQuery.Predict(target, t), lead);
        }
        point = ClampAimPoint(def, c.Stats, from, p);
        dir = CombatQuery.FlatDirection(from, p, c.AimDirection);
    }

    /// <summary>Seconds from now until the ability would reach something <paramref name="distance"/> away.</summary>
    public static float ImpactDelay(AbilityCastInstance c, float distance)
    {
        float t = c.Released ? 0f : Mathf.Max(0f, c.ReleaseTime - Time.time);
        AbilityDefinition def = c.Definition;
        float best = float.MaxValue;
        for (int i = 0; i < def.actions.Count; i++)
        {
            CastAction a = def.actions[i];
            if (a == null)
                continue;
            float travel = a is ITravellingAction ta ? ta.TravelTime(c.Stats, distance) : 0f;
            best = Mathf.Min(best, a.delay + travel);
        }
        return t + (best == float.MaxValue ? 0f : best);
    }

    /// <summary>Keeps an aimed point inside [Min Range, Range] from <paramref name="from"/> and on the ground.</summary>
    public static Vector3 ClampAimPoint(AbilityDefinition def, in AbilityStats s, Vector3 from, Vector3 point)
    {
        Vector3 d = point - from;
        d.y = 0f;
        float dist = d.magnitude;
        float max = def.Range(s);
        float min = def.MinRange(s);
        if (def.targeting.mode != AbilityTargetingMode.Self && max > 0f)
        {
            if (dist > max)
                point = from + d / dist * max + Vector3.up * (point.y - from.y);
            else if (dist < min && dist > 1e-3f)
                point = from + d / dist * min + Vector3.up * (point.y - from.y);
        }
        return SnapIfNeeded(def, point);
    }

    private static Vector3 SnapIfNeeded(AbilityDefinition def, Vector3 p) => def.targeting.snapToGround ? CombatQuery.SnapToGround(p) : p;

    /// <summary>Turns the character toward <paramref name="direction"/> (flat). Derived casters may use their own movement.</summary>
    protected virtual void FaceDirection(Vector3 direction, float dt)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 1e-6f)
            return;
        Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = turnSpeed <= 0f ? target : Quaternion.RotateTowards(transform.rotation, target, turnSpeed * dt);
    }

    // ------------------------------------------------------------------ update
    protected virtual void Update()
    {
        float now = Time.time;
        float dt = Time.deltaTime;
        float move = 1f;
        AbilityCastInstance newest = null;

        for (int i = 0; i < slots.Count; i++)
        {
            AbilitySlot s = slots[i];
            if (s == null)
                continue;
            s.TickCharges(now);

            switch (s.Phase)
            {
                case AbilityPhase.Casting:
                    UpdateCasting(s, dt, now);
                    break;
                case AbilityPhase.Launching:
                    if (s.cast != null && s.cast.Definition.targeting.aimLock == AbilityAimLock.FollowCaster)
                        UpdateAim(s.cast, dt);
                    if (now >= s.phaseEnd)
                        EnterActive(s);
                    break;
                case AbilityPhase.Active:
                    if (now >= s.phaseEnd)
                        FinishCast(s);
                    break;
                case AbilityPhase.InCooldown:
                    if (s.Charges > 0 && now >= s.lockedUntil)
                        s.SetPhase(AbilityPhase.Ready, 0f);
                    break;
            }

            AbilityPhase p = s.Phase;
            if ((p == AbilityPhase.Casting || p == AbilityPhase.Launching) && s.cast != null && s.ability != null)
            {
                AbilityDefinition def = s.ability;
                if (def.movementWhileCasting == CasterMovementRule.Stop)
                    move = 0f;
                else if (def.movementWhileCasting == CasterMovementRule.Slowed)
                    move = Mathf.Min(move, def.castMoveSpeedMultiplier);
                if (def.targeting.faceAim && def.targeting.aimLock != AbilityAimLock.FollowCaster)
                    FaceDirection(s.cast.AimDirection, dt);
                if (newest == null || s.cast.StartTime > newest.StartTime)
                    newest = s.cast;
            }
        }

        CurrentCast = newest;
        MoveSpeedMultiplier = move;
        if (Entity != null)
            Entity.CastMoveMultiplier = move;
    }

    private void UpdateCasting(AbilitySlot s, float dt, float now)
    {
        AbilityCastInstance c = s.cast;
        if (c == null)
        {
            s.SetPhase(AbilityPhase.Ready, 0f);
            return;
        }
        AbilityDefinition def = s.ability;
        AbilityTargeting t = def.targeting;

        if (t.mode == AbilityTargetingMode.Unit && (c.Target == null || !c.Target.IsAlive))
        {
            InterruptSlot(s, CastInterruptReason.TargetLost);
            return;
        }
        if (t.cancelRangeMargin > 0f && c.Target != null &&
            CombatQuery.FlatDistance(Entity.Position, c.Target.Position) - c.Target.Radius > def.Range(c.Stats) + t.cancelRangeMargin)
        {
            InterruptSlot(s, CastInterruptReason.TargetLost);
            return;
        }

        UpdateAim(c, dt);
        UpdateTelegraphs(c, s.PhaseProgress);

        if (now >= s.phaseEnd)
            Release(s);
    }

    private void Release(AbilitySlot s)
    {
        AbilityCastInstance c = s.cast;
        if (c == null)
            return;
        AbilityDefinition def = s.ability;
        c.Released = true;
        c.ReleaseTime = Time.time;
        c.Phase = AbilityPhase.Launching;
        c.ClearTelegraphsAndHazards(false);
        ReleaseCastVfx(c);

        AbilityPresentation pres = def.presentation;
        AnimatorParameterCache.SetTrigger(animator, pres.releaseTrigger);
        if (pres.releaseVfx != null)
            AbilityPool.PlayVfx(pres.releaseVfx, CastPointPosition, c.AimRotation);
        if (pres.releaseSound != null)
            AbilityPool.PlaySound(pres.releaseSound, transform.position, pres.volume);
        c.EmitNoise(transform.position, 0.9f);

        s.SetPhase(AbilityPhase.Launching, def.LaunchDuration(c.Stats));
        OnCastReleased(c);
        CastReleased?.Invoke(c);
        CombatEvents.RaiseCastReleased(c);

        List<CastAction> actions = def.actions;
        for (int i = 0; i < actions.Count; i++)
        {
            CastAction a = actions[i];
            if (a == null)
                continue;
            if (a.delay <= 0f)
                ExecuteAction(c, a, this);
            else
                AbilityRuntime.Schedule(a.delay, () => ExecuteAction(c, a, this), c);
        }
    }

    private static void ExecuteAction(AbilityCastInstance c, CastAction a, UnityEngine.Object context)
    {
        if (c.Interrupted)
            return;
        try
        {
            a.Execute(c);
        }
        catch (Exception e)
        {
            Debug.LogException(e, context);
        }
    }

    private void EnterActive(AbilitySlot s)
    {
        if (s.cast != null)
            s.cast.Phase = AbilityPhase.Active;
        s.SetPhase(AbilityPhase.Active, s.ability != null ? s.ability.activeTime : 0f);
    }

    private void FinishCast(AbilitySlot s)
    {
        AbilityCastInstance c = s.cast;
        s.cast = null;
        float now = Time.time;
        if (c != null)
        {
            c.Phase = AbilityPhase.InCooldown;
            c.ClearTelegraphsAndHazards(false);
            ReleaseCastVfx(c);
            AnimatorParameterCache.SetBool(animator, c.Definition.presentation.castingBool, false);
            OnCastFinished(c);
        }
        s.StartRecharge(now);
        if (s.Charges > 0 && now >= s.lockedUntil)
            s.SetPhase(AbilityPhase.Ready, 0f);
        else
            s.SetPhase(AbilityPhase.InCooldown, s.CooldownRemaining);
    }

    // ------------------------------------------------------------------ interruption
    /// <summary>
    /// Interrupts casts that allow this reason (stun/silence need Interrupted By Control, damage needs Interrupted By
    /// Damage). <paramref name="force"/> interrupts regardless. Returns true if anything was interrupted.
    /// </summary>
    public bool Interrupt(CastInterruptReason reason, bool force = false)
    {
        bool any = false;
        for (int i = 0; i < slots.Count; i++)
        {
            AbilitySlot s = slots[i];
            if (s == null || s.cast == null || s.ability == null)
                continue;
            AbilityPhase p = s.Phase;
            if (p != AbilityPhase.Casting && p != AbilityPhase.Launching)
                continue;
            if (!force && !Allows(s.ability, reason, p))
                continue;
            InterruptSlot(s, reason);
            any = true;
        }
        return any;
    }

    /// <summary>Interrupts every cast in progress.</summary>
    public void InterruptAll(CastInterruptReason reason) => Interrupt(reason, true);

    private static bool Allows(AbilityDefinition def, CastInterruptReason reason, AbilityPhase phase)
    {
        switch (reason)
        {
            case CastInterruptReason.Manual:
            case CastInterruptReason.Death:
            case CastInterruptReason.Disabled:
            case CastInterruptReason.Replaced:
                return true;
            case CastInterruptReason.Stun:
            case CastInterruptReason.Silence:
                return def.interruptedByControl;
            case CastInterruptReason.Damage:
                return def.interruptedByDamage && phase == AbilityPhase.Casting;
            case CastInterruptReason.TargetLost:
                return phase == AbilityPhase.Casting;
            default:
                return true;
        }
    }

    /// <summary>Cancels the cast of one slot.</summary>
    public void InterruptSlot(AbilitySlot s, CastInterruptReason reason)
    {
        AbilityCastInstance c = s != null ? s.cast : null;
        if (c == null)
            return;
        float now = Time.time;
        AbilityDefinition def = s.ability;
        bool beforeRelease = !c.Released;

        c.Interrupted = true;
        c.InterruptReason = reason;
        AbilityRuntime.CancelAll(c);
        c.ClearTelegraphsAndHazards(true);
        ReleaseCastVfx(c);
        if (def != null)
            AnimatorParameterCache.SetBool(animator, def.presentation.castingBool, false);

        s.cast = null;
        c.Phase = AbilityPhase.InCooldown;
        if (beforeRelease && def != null)
        {
            if (def.refundCostOnInterrupt)
                Refund(c);
            s.charges = Mathf.Min(s.MaxCharges, s.Charges + 1);
            s.lockedUntil = Mathf.Max(s.lockedUntil, now + def.Cooldown(c.Stats) * def.cooldownOnInterrupt);
        }
        else
        {
            s.StartRecharge(now);
        }

        if (s.Charges > 0 && now >= s.lockedUntil)
            s.SetPhase(AbilityPhase.Ready, 0f);
        else
            s.SetPhase(AbilityPhase.InCooldown, s.CooldownRemaining);

        if (debugLog)
            Debug.Log($"[{name}] {c} interrupted: {reason}", this);
        CastInterrupted?.Invoke(c, reason);
        CombatEvents.RaiseCastInterrupted(c, reason);
        if (CurrentCast == c)
            CurrentCast = null;
    }

    private void OnEntityDamaged(DamageInfo info)
    {
        if (info.isPeriodic || info.amount <= 0f)
            return;
        float max = Entity != null ? Entity.MaxHealth : 0f;
        for (int i = 0; i < slots.Count; i++)
        {
            AbilitySlot s = slots[i];
            AbilityCastInstance c = s != null ? s.cast : null;
            if (c == null || s.Phase != AbilityPhase.Casting)
                continue;
            c.DamageTaken += info.amount;
            AbilityDefinition def = s.ability;
            if (!def.interruptedByDamage)
                continue;
            float threshold = def.interruptDamageThreshold * max;
            if (info.amount >= threshold)
                InterruptSlot(s, CastInterruptReason.Damage);
        }
    }

    private void ReleaseCastVfx(AbilityCastInstance c)
    {
        if (c.castVfx != null)
        {
            AbilityPool.Release(c.castVfx);
            c.castVfx = null;
        }
    }

    // ------------------------------------------------------------------ telegraphs
    private void CreateTelegraphs(AbilityCastInstance c, float castTime)
    {
        AbilityDefinition def = c.Definition;
        bool show = castTime > 0.05f && showTelegraphs && def.presentation.showTelegraph && TelegraphVisible();
        Color color = TelegraphColor(def);
        float severity = c.Severity;

        List<CastAction> actions = def.actions;
        for (int i = 0; i < actions.Count; i++)
        {
            CastAction a = actions[i];
            if (a == null)
                continue;
            shapeBuffer.Clear();
            if (!a.GetTelegraphShapes(c, shapeBuffer))
                continue;
            float from = c.ReleaseTime + a.delay;
            float until = from + a.HazardDuration(def, c.Stats);
            for (int k = 0; k < shapeBuffer.Count; k++)
            {
                ResolvedShape shape = shapeBuffer[k];
                c.AddHazard(shape, from, until, severity, a.HazardFilter);
                if (show)
                    c.telegraphs.Add(AbilityTelegraph.Show(shape, color));
            }
        }
        shapeBuffer.Clear();
    }

    private void UpdateTelegraphs(AbilityCastInstance c, float progress)
    {
        bool tracking = c.Definition.targeting.aimLock != AbilityAimLock.LockAtCastStart;
        if (tracking)
        {
            int index = 0;
            List<CastAction> actions = c.Definition.actions;
            for (int i = 0; i < actions.Count; i++)
            {
                CastAction a = actions[i];
                if (a == null)
                    continue;
                shapeBuffer.Clear();
                if (!a.GetTelegraphShapes(c, shapeBuffer))
                    continue;
                for (int k = 0; k < shapeBuffer.Count; k++, index++)
                {
                    ResolvedShape shape = shapeBuffer[k];
                    if (index < c.hazards.Count && c.hazards[index] != null)
                        c.hazards[index].shape = shape;
                    if (index < c.telegraphs.Count && c.telegraphs[index] != null)
                        c.telegraphs[index].SetShape(shape);
                }
            }
            shapeBuffer.Clear();
        }
        for (int i = 0; i < c.telegraphs.Count; i++)
            c.telegraphs[i]?.SetProgress(progress);
    }

    /// <summary>Should this caster's telegraphs be drawn?</summary>
    protected virtual bool TelegraphVisible()
    {
        if (Entity == null)
            return false;
        if (Entity.Kind == CombatEntity.EntityKind.Player)
            return true;
        return CombatSettings.Instance.showEnemyTelegraphs;
    }

    protected virtual Color TelegraphColor(AbilityDefinition def)
    {
        if (def.presentation.telegraphColor.a > 0.01f)
            return def.presentation.telegraphColor;
        CombatSettings settings = CombatSettings.Instance;
        if (Entity == null)
            return settings.enemyTelegraphColor;
        if (Entity.Kind == CombatEntity.EntityKind.Player)
            return settings.aimPreviewColor;
        CombatEntity player = CombatEntity.NearestPlayer(Entity.Position, out _);
        if (player != null && CombatRelations.Get(Entity, player) != CombatRelation.Enemy)
            return settings.allyTelegraphColor;
        return settings.enemyTelegraphColor;
    }

    // ------------------------------------------------------------------ resources
    protected virtual bool HasResources(AbilityDefinition def, in AbilityStats s)
    {
        float m = def.manaCost * s.cost, st = def.staminaCost * s.cost, hp = def.healthCost * s.cost;
        if (m > 0f && mana != null && !mana.HasEnougCurrentValue(m))
            return false;
        if (st > 0f && stamina != null && !stamina.HasEnougCurrentValue(st))
            return false;
        if (hp > 0f && Entity != null && Entity.Health != null && Entity.Health.CurrentValue <= hp)
            return false;
        return true;
    }

    protected virtual void PayCost(AbilityCastInstance c)
    {
        AbilityDefinition def = c.Definition;
        AbilityStats s = c.Stats;
        float m = def.manaCost * s.cost, st = def.staminaCost * s.cost, hp = def.healthCost * s.cost;
        if (m > 0f && mana != null)
        {
            mana.ConsumeMana(m);
            c.paidMana = m;
        }
        if (st > 0f && stamina != null)
        {
            stamina.ConsumeStamina(st);
            c.paidStamina = st;
        }
        if (hp > 0f && Entity != null && Entity.Health != null)
        {
            Entity.Health.ConsumeHP(hp, true);
            c.paidHealth = hp;
        }
    }

    protected virtual void Refund(AbilityCastInstance c)
    {
        if (c.paidMana > 0f && mana != null) mana.RestoreMana(c.paidMana);
        if (c.paidStamina > 0f && stamina != null) stamina.AddCurrentValue(c.paidStamina);
        if (c.paidHealth > 0f && Entity != null && Entity.Health != null) Entity.Health.AddCurrentValue(c.paidHealth);
        c.paidMana = c.paidStamina = c.paidHealth = 0f;
    }

    // ------------------------------------------------------------------ receiving abilities (absorption)
    public virtual bool CanReceive(AbilityGrant grant) => grant != null && grant.ability != null && isActiveAndEnabled && Entity != null && Entity.IsAlive;

    public virtual bool Receive(AbilityGrant grant, out int slotIndex)
    {
        slotIndex = -1;
        if (!CanReceive(grant))
            return false;
        Initialize();
        AbsorptionSettings settings = AbsorptionSettings.Instance;

        int existing = IndexOf(grant.ability);
        if (existing >= 0)
        {
            AbilitySlot s = slots[existing];
            switch (settings.duplicatePolicy)
            {
                case DuplicateAbsorbPolicy.KeepExisting:
                    return false;
                case DuplicateAbsorbPolicy.ReplaceIfStronger:
                    if (AbilityGrant.Power(grant.ability, grant.modifiers) <= AbilityGrant.Power(s.ability, s.modifiers))
                        return false;
                    GrantInto(existing, grant);
                    slotIndex = existing;
                    return true;
                case DuplicateAbsorbPolicy.AlwaysReplace:
                    GrantInto(existing, grant);
                    slotIndex = existing;
                    return true;
            }
            // AddAnotherSlot falls through.
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && slots[i].IsEmpty && CanUseSlotForGrant(i))
            {
                GrantInto(i, grant);
                slotIndex = i;
                return true;
            }
        }

        if (CanAddSlots && (maxSlots <= 0 || slots.Count < maxSlots))
        {
            int i = AddSlot(null);
            GrantInto(i, grant);
            slotIndex = i;
            return true;
        }

        int replace = -1;
        switch (settings.fullSlotsPolicy)
        {
            case FullSlotsPolicy.ReplaceOldestAbsorbed:
            {
                float oldest = float.MaxValue;
                for (int i = 0; i < slots.Count; i++)
                {
                    AbilitySlot s = slots[i];
                    if (s?.Grant != null && s.Grant.receivedTime < oldest && s.cast == null && CanUseSlotForGrant(i))
                    {
                        oldest = s.Grant.receivedTime;
                        replace = i;
                    }
                }
                break;
            }
            case FullSlotsPolicy.ReplaceLastSlot:
                for (int i = slots.Count - 1; i >= 0; i--)
                {
                    if (CanUseSlotForGrant(i))
                    {
                        replace = i;
                        break;
                    }
                }
                break;
        }
        if (replace < 0)
            return false;
        GrantInto(replace, grant);
        slotIndex = replace;
        return true;
    }

    /// <summary>Can new slots be created for absorbed abilities? (the player maps slots to input bindings, so it cannot).</summary>
    protected virtual bool CanAddSlots => true;

    /// <summary>Can slot <paramref name="index"/> hold an absorbed ability?</summary>
    protected virtual bool CanUseSlotForGrant(int index) => true;

    /// <summary>Puts a grant into a slot (used by absorption and by save restoring).</summary>
    protected void GrantInto(int index, AbilityGrant grant)
    {
        SetSlot(index, grant.ability, grant.modifiers);
        AbilitySlot s = slots[index];
        grant.receivedTime = Time.time;
        s.Grant = grant;
        s.label = grant.displayName ?? "";
        AbilityReceived?.Invoke(grant, index);
    }

    // ------------------------------------------------------------------ standalone execution
    /// <summary>
    /// Runs an ability's actions immediately without a caster component: traps, explosive barrels, on-death
    /// effects, items. <paramref name="source"/> (may be null) is used for teams and damage attribution.
    /// </summary>
    public static AbilityCastInstance ExecuteInstant(AbilityDefinition ability, CombatEntity source, Vector3 position, Vector3 direction, CombatEntity target = null, AbilityModifierSet modifiers = null)
    {
        if (ability == null)
            return null;
        var c = new AbilityCastInstance(null, source, null, ability, modifiers)
        {
            Target = target,
            AimPoint = target != null ? target.BasePosition : position,
            AimDirection = CombatQuery.FlatDirection(Vector3.zero, direction, Vector3.forward),
        };
        c.Released = true;
        c.ReleaseTime = Time.time;
        c.Phase = AbilityPhase.Launching;
        List<CastAction> actions = ability.actions;
        for (int i = 0; i < actions.Count; i++)
        {
            CastAction a = actions[i];
            if (a == null)
                continue;
            if (a.delay <= 0f)
                ExecuteAction(c, a, ability);
            else
                AbilityRuntime.Schedule(a.delay, () => ExecuteAction(c, a, ability), c);
        }
        return c;
    }

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        if (slots == null)
            return;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && slots[i].modifiers == null)
                slots[i].modifiers = new AbilityModifierSet();
            slots[i]?.InvalidateStats();
        }
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (slots == null)
            return;
        Vector3 p = transform.position;
        for (int i = 0; i < slots.Count; i++)
        {
            AbilitySlot s = slots[i];
            if (s?.ability == null)
                continue;
            AbilityStats st = s.Stats;
            float reach = s.ability.MaxReach(st);
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.35f);
            DrawCircle(p, reach);
            float min = s.ability.MinRange(st);
            if (min > 0f)
            {
                Gizmos.color = new Color(1f, 1f, 0.2f, 0.35f);
                DrawCircle(p, min);
            }
        }
    }

    private static void DrawCircle(Vector3 c, float r)
    {
        if (r <= 0f)
            return;
        Vector3 prev = c + new Vector3(r, 0.05f, 0f);
        for (int i = 1; i <= 48; i++)
        {
            float a = i / 48f * Mathf.PI * 2f;
            Vector3 next = c + new Vector3(Mathf.Cos(a) * r, 0.05f, Mathf.Sin(a) * r);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}

/// <summary>Actions whose effect travels (projectiles, surges): lets the AI lead moving targets.</summary>
public interface ITravellingAction
{
    /// <summary>Seconds to travel <paramref name="distance"/> metres.</summary>
    float TravelTime(in AbilityStats stats, float distance);
}
