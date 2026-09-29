using System.Collections.Generic;
using UnityEngine;

/// <summary>How a mob regards another character.</summary>
public enum MobRelationKind
{
    /// <summary>Ignored.</summary>
    Neutral,
    /// <summary>Same side.</summary>
    Ally,
    /// <summary>Someone to fight (prey, hostile player, attacker).</summary>
    Enemy,
    /// <summary>Someone to run from (predator, feared type).</summary>
    Threat,
}

/// <summary>What a mob knows about one character.</summary>
public sealed class MobMemoryEntry
{
    public CombatEntity entity;
    /// <summary>0-1. Reaching 1 means the character has been noticed (detected).</summary>
    public float awareness;
    /// <summary>Noticed and still remembered.</summary>
    public bool detected;
    /// <summary>Seen during the last perception update.</summary>
    public bool visible;
    public Vector3 lastKnownPosition;
    public Vector3 lastKnownVelocity;
    public float lastSeenTime = -999f;
    /// <summary>Last time it was seen, heard or felt (damage).</summary>
    public float lastSensedTime = -999f;
    public float detectedTime = -999f;
    public MobRelationKind relation;
    /// <summary>Flat distance at the last perception update.</summary>
    public float distance = float.MaxValue;

    public bool IsHostile => relation == MobRelationKind.Enemy || relation == MobRelationKind.Threat;
    public float TimeSinceSeen => Time.time - lastSeenTime;
    public float TimeSinceSensed => Time.time - lastSensedTime;

    internal void Reset(CombatEntity e)
    {
        entity = e;
        awareness = 0f;
        detected = false;
        visible = false;
        lastKnownPosition = e != null ? e.Position : Vector3.zero;
        lastKnownVelocity = Vector3.zero;
        lastSeenTime = -999f;
        lastSensedTime = -999f;
        detectedTime = -999f;
        relation = MobRelationKind.Neutral;
        distance = float.MaxValue;
    }
}

/// <summary>A mob's memory of characters, places to investigate and recent danger.</summary>
public sealed class MobMemory
{
    private readonly List<MobMemoryEntry> entries = new List<MobMemoryEntry>(8);
    private readonly Dictionary<CombatEntity, MobMemoryEntry> map = new Dictionary<CombatEntity, MobMemoryEntry>(8);
    private static readonly Stack<MobMemoryEntry> pool = new Stack<MobMemoryEntry>(64);

    public IReadOnlyList<MobMemoryEntry> Entries => entries;

    public bool HasInvestigatePoint { get; private set; }
    public Vector3 InvestigatePoint { get; private set; }
    /// <summary>0-1: how alarming the thing to investigate is (running vs walking there).</summary>
    public float InvestigateUrgency { get; private set; }
    public float InvestigateSetTime { get; private set; }

    public float LastDamagedTime = -999f;
    public Vector3 LastDamageDirection;
    private readonly float[] recentHits = new float[8];
    private int recentHitIndex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => pool.Clear();

    public MobMemoryEntry Get(CombatEntity e)
    {
        if (e == null)
            return null;
        map.TryGetValue(e, out MobMemoryEntry entry);
        return entry;
    }

    public MobMemoryEntry GetOrAdd(CombatEntity e)
    {
        MobMemoryEntry entry = Get(e);
        if (entry != null || e == null)
            return entry;
        entry = pool.Count > 0 ? pool.Pop() : new MobMemoryEntry();
        entry.Reset(e);
        entries.Add(entry);
        map[e] = entry;
        return entry;
    }

    public void Forget(CombatEntity e)
    {
        MobMemoryEntry entry = Get(e);
        if (entry == null)
            return;
        map.Remove(e);
        entries.Remove(entry);
        entry.entity = null;
        pool.Push(entry);
    }

    public void Clear()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            entries[i].entity = null;
            pool.Push(entries[i]);
        }
        entries.Clear();
        map.Clear();
        ClearInvestigate();
    }

    /// <summary>Drops dead, destroyed and long-forgotten characters.</summary>
    public void Prune(float now, float memoryDuration)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            MobMemoryEntry e = entries[i];
            bool gone = e.entity == null || !e.entity.IsAlive;
            bool forgotten = !e.detected && e.awareness <= 0f && now - e.lastSensedTime > memoryDuration * 2f;
            if (gone || forgotten)
            {
                if (e.entity != null)
                    map.Remove(e.entity);
                else
                    RemoveNullKeys();
                entries.RemoveAt(i);
                e.entity = null;
                pool.Push(e);
            }
        }
    }

    private void RemoveNullKeys()
    {
        List<CombatEntity> dead = null;
        foreach (KeyValuePair<CombatEntity, MobMemoryEntry> kv in map)
        {
            if (kv.Key == null)
                (dead ??= new List<CombatEntity>()).Add(kv.Key);
        }
        if (dead != null)
            foreach (CombatEntity k in dead)
                map.Remove(k);
    }

    public void SetInvestigate(Vector3 point, float urgency)
    {
        // Keep a more urgent point unless the new one is also urgent.
        if (HasInvestigatePoint && urgency < InvestigateUrgency - 0.2f && Time.time - InvestigateSetTime < 4f)
            return;
        HasInvestigatePoint = true;
        InvestigatePoint = point;
        InvestigateUrgency = Mathf.Clamp01(urgency);
        InvestigateSetTime = Time.time;
    }

    public void ClearInvestigate()
    {
        HasInvestigatePoint = false;
        InvestigateUrgency = 0f;
    }

    public void RecordHit(Vector3 fromDirection)
    {
        LastDamagedTime = Time.time;
        LastDamageDirection = fromDirection;
        recentHits[recentHitIndex] = Time.time;
        recentHitIndex = (recentHitIndex + 1) % recentHits.Length;
    }

    /// <summary>Hits taken in the last <paramref name="seconds"/>.</summary>
    public int HitsWithin(float seconds)
    {
        int n = 0;
        float t = Time.time - seconds;
        for (int i = 0; i < recentHits.Length; i++)
            if (recentHits[i] > 0f && recentHits[i] >= t) n++;
        return n;
    }
}

/// <summary>
/// A mob's senses: sight (range, field of view, line of sight, sneaking), close-range awareness, hearing (noises
/// from footsteps, fights and abilities), pain (who hurt it) and allies' calls for help. Builds up awareness
/// gradually so players can sneak past, and feeds the <see cref="MobBrain"/>.
/// </summary>
public sealed class MobPerception
{
    private readonly MobMovementContext ctx;
    private float lastTick = -1f;
    private bool subscribed;

    public MobPerception(MobMovementContext context)
    {
        ctx = context;
    }

    public void Subscribe()
    {
        if (subscribed)
            return;
        subscribed = true;
        CombatEvents.Noise += OnNoise;
        CombatEvents.HelpRequested += OnHelpRequested;
        CombatEvents.MeleeSwingStarted += OnMeleeSwing;
        CombatEvents.CastStarted += OnCastStarted;
        if (ctx.Entity != null)
            ctx.Entity.Damaged += OnDamaged;
    }

    public void Unsubscribe()
    {
        if (!subscribed)
            return;
        subscribed = false;
        CombatEvents.Noise -= OnNoise;
        CombatEvents.HelpRequested -= OnHelpRequested;
        CombatEvents.MeleeSwingStarted -= OnMeleeSwing;
        CombatEvents.CastStarted -= OnCastStarted;
        if (ctx.Entity != null)
            ctx.Entity.Damaged -= OnDamaged;
    }

    /// <summary>Position of the mob's eyes.</summary>
    public Vector3 EyePosition
    {
        get
        {
            CombatEntity e = ctx.Entity;
            return e.BasePosition + Vector3.up * ctx.Profile.EffectiveEyeHeight(e.Height);
        }
    }

    /// <summary>Can the mob see <paramref name="target"/> right now (range, field of view, walls, sneaking)?</summary>
    public bool CanSee(CombatEntity target, out float distance, out bool inCloseRange)
    {
        MobProfile p = ctx.Profile;
        Vector3 eye = EyePosition;
        Vector3 to = target.AimPosition - eye;
        distance = new Vector2(to.x, to.z).magnitude;
        inCloseRange = distance <= p.closeSenseRadius + target.Radius;
        float range = p.sightRange * (target.IsSneaking ? p.sneakDetectionMultiplier : 1f);
        if (!inCloseRange)
        {
            if (distance > range + target.Radius)
                return false;
            if (p.fieldOfView < 359f)
            {
                Vector3 fwd = ctx.Transform.forward;
                fwd.y = 0f;
                Vector3 flat = new Vector3(to.x, 0f, to.z);
                if (flat.sqrMagnitude > 1e-4f && Vector3.Angle(fwd, flat) > p.fieldOfView * 0.5f)
                    return false;
            }
        }
        // Up close characters are felt even behind thin obstacles.
        if (inCloseRange && distance < 1.5f)
            return true;
        return CombatQuery.HasLineOfSight(eye, target.AimPosition) || CombatQuery.HasLineOfSight(eye, target.Center);
    }

    /// <summary>Line of sight from the eyes to the target's body (ignores range and field of view).</summary>
    public bool HasLineOfSightTo(CombatEntity target) =>
        target != null && (CombatQuery.HasLineOfSight(EyePosition, target.AimPosition) || CombatQuery.HasLineOfSight(EyePosition, target.Center));

    /// <summary>Updates what the mob sees. Called by the scheduler (a few times per second).</summary>
    public void Tick(float now)
    {
        float dt = lastTick < 0f ? 0.2f : Mathf.Clamp(now - lastTick, 0.01f, 1f);
        lastTick = now;

        MobProfile p = ctx.Profile;
        MobMemory memory = ctx.Memory;
        CombatEntity self = ctx.Entity;
        Vector3 pos = self.Position;
        float scanRange = Mathf.Max(p.sightRange, p.fleeFromPlayersWithin, p.closeSenseRadius) + 2f;
        float scanSqr = scanRange * scanRange;

        // Mark everyone as not visible; entries seen this tick are set below.
        IReadOnlyList<MobMemoryEntry> entries = memory.Entries;
        for (int i = 0; i < entries.Count; i++)
            entries[i].visible = false;

        IReadOnlyList<CombatEntity> all = CombatEntity.All;
        for (int i = 0; i < all.Count; i++)
        {
            CombatEntity other = all[i];
            if (other == null || other == self || !other.IsAlive)
                continue;
            Vector3 d = other.Position - pos;
            float sqr = d.x * d.x + d.z * d.z;
            MobMemoryEntry known = memory.Get(other);
            if (sqr > scanSqr && known == null)
                continue;

            float flatDist = Mathf.Sqrt(sqr);
            MobRelationKind relation = ctx.Brain.Classify(other, flatDist);
            if (relation == MobRelationKind.Neutral || relation == MobRelationKind.Ally)
            {
                if (known != null)
                {
                    known.relation = relation;
                    known.distance = flatDist;
                }
                continue;
            }

            MobMemoryEntry entry = known ?? memory.GetOrAdd(other);
            entry.relation = relation;
            entry.distance = flatDist;
            if (sqr > scanSqr)
                continue;

            if (!CanSee(other, out float seeDist, out bool close))
                continue;

            entry.visible = true;
            entry.lastSeenTime = now;
            entry.lastSensedTime = now;
            entry.lastKnownPosition = other.Position;
            entry.lastKnownVelocity = other.Velocity;

            if (!entry.detected)
            {
                float proximity = Mathf.Clamp(1.5f - seeDist / Mathf.Max(1f, p.sightRange), 0.3f, 1.5f);
                float gain = p.awarenessGainRate * dt * proximity * (close ? 2f : 1f);
                if (other.IsSneaking) gain *= 0.5f;
                if (new Vector2(entry.lastKnownVelocity.x, entry.lastKnownVelocity.z).sqrMagnitude > 16f) gain *= 1.3f;
                if (relation == MobRelationKind.Threat) gain *= 1.5f; // prey is jumpy
                entry.awareness = Mathf.Min(1f, entry.awareness + gain);
                if (entry.awareness >= 1f)
                    Detect(entry, now);
                else if (entry.awareness >= 0.35f)
                    memory.SetInvestigate(other.Position, entry.awareness * 0.5f);
            }
        }

        // Forgetting.
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            MobMemoryEntry e = entries[i];
            if (e.visible)
                continue;
            float since = now - e.lastSensedTime;
            if (since > p.memoryDuration)
            {
                e.detected = false;
                e.awareness = Mathf.Max(0f, e.awareness - dt * 0.25f);
            }
            else if (!e.detected)
            {
                e.awareness = Mathf.Max(0f, e.awareness - dt * 0.1f);
            }
        }
        memory.Prune(now, p.memoryDuration);
    }

    private void Detect(MobMemoryEntry entry, float now)
    {
        bool first = !entry.detected;
        entry.awareness = 1f;
        entry.detected = true;
        entry.lastSensedTime = now;
        if (first)
        {
            entry.detectedTime = now;
            ctx.Brain.OnDetected(entry);
        }
    }

    /// <summary>Makes the mob fully aware of <paramref name="other"/> at a known position (damage, ally alert).</summary>
    public MobMemoryEntry ForceAware(CombatEntity other, Vector3 position)
    {
        if (other == null || other == ctx.Entity || !other.IsAlive)
            return null;
        MobMemoryEntry entry = ctx.Memory.GetOrAdd(other);
        entry.relation = ctx.Brain.Classify(other, CombatQuery.FlatDistance(ctx.Entity.Position, other.Position));
        entry.lastKnownPosition = position;
        entry.lastKnownVelocity = other.Velocity;
        Detect(entry, Time.time);
        return entry;
    }

    // ------------------------------------------------------------------ events
    private void OnDamaged(DamageInfo info)
    {
        if (ctx.Entity == null || ctx.Entity.IsDead)
            return;
        Vector3 from = info.direction.sqrMagnitude > 1e-4f ? -info.direction : (info.source != null ? info.source.Position - ctx.Entity.Position : ctx.Transform.forward);
        ctx.Memory.RecordHit(from);
        if (info.source != null && info.source != ctx.Entity)
        {
            ForceAware(info.source, info.source.Position);
        }
        else if (!info.isPeriodic)
        {
            // Hurt by something unseen: look where it came from.
            ctx.Memory.SetInvestigate(ctx.Entity.Position + from.normalized * 8f, 1f);
        }
        ctx.Brain.OnDamaged(info);
    }

    private void OnNoise(NoiseEvent n)
    {
        if (ctx.Entity == null || ctx.Entity.IsDead || n.source == ctx.Entity)
            return;
        float hearing = ctx.Profile.hearingMultiplier;
        if (hearing <= 0f || !ctx.Scheduler.Awake)
            return;
        float range = n.radius * hearing;
        Vector3 d = n.position - ctx.Entity.Position;
        if (d.sqrMagnitude > range * range)
            return;

        if (n.source != null)
        {
            MobRelationKind rel = ctx.Brain.Classify(n.source, d.magnitude);
            if (rel == MobRelationKind.Ally || rel == MobRelationKind.Neutral)
            {
                // Fights of allies are worth a look.
                if (rel == MobRelationKind.Ally && n.intensity >= 0.7f)
                    ctx.Memory.SetInvestigate(n.position, 0.6f);
                return;
            }
            MobMemoryEntry e = ctx.Memory.GetOrAdd(n.source);
            e.relation = rel;
            e.lastKnownPosition = n.position;
            e.lastSensedTime = Time.time;
            float closeness = 1f - Mathf.Clamp01(d.magnitude / range);
            e.awareness = Mathf.Min(1f, e.awareness + n.intensity * (0.35f + closeness * 0.5f));
            if (e.awareness >= 1f)
                Detect(e, Time.time);
            else
                ctx.Memory.SetInvestigate(n.position, n.intensity);
            return;
        }
        if (n.intensity >= 0.25f)
            ctx.Memory.SetInvestigate(n.position, n.intensity);
    }

    private void OnHelpRequested(CombatEntity caller, CombatEntity target)
    {
        if (caller == null || target == null || caller == ctx.Entity || ctx.Entity == null || ctx.Entity.IsDead)
            return;
        float radius = ctx.Profile.respondToHelpRadius;
        if (radius <= 0f || caller.Team != ctx.Entity.Team)
            return;
        if ((caller.Position - ctx.Entity.Position).sqrMagnitude > radius * radius)
            return;
        ctx.Brain.OnHelpRequested(caller, target);
    }

    private void OnMeleeSwing(CombatEntity attacker)
    {
        if (attacker == null || attacker == ctx.Entity || ctx.Entity == null || ctx.Entity.IsDead)
            return;
        if ((attacker.Position - ctx.Entity.Position).sqrMagnitude > 25f)
            return;
        ctx.Brain.OnMeleeSwing(attacker);
    }

    private void OnCastStarted(AbilityCastInstance cast)
    {
        if (cast == null || cast.CasterEntity == ctx.Entity || ctx.Entity == null || ctx.Entity.IsDead)
            return;
        if ((cast.CasterPosition - ctx.Entity.Position).sqrMagnitude > 40f * 40f)
            return;
        ctx.Brain.RequestDangerCheck();
        // Hostile casters reveal themselves.
        if (cast.CasterEntity != null && ctx.Brain.Classify(cast.CasterEntity, 0f) == MobRelationKind.Enemy)
        {
            MobMemoryEntry e = ctx.Memory.Get(cast.CasterEntity);
            if (e != null)
                e.lastKnownPosition = cast.CasterEntity.Position;
        }
    }
}
