using System.Collections.Generic;
using UnityEngine;
using EState = MobMovementStateMachine.EMobMovementState;

/// <summary>What the mob is generally doing.</summary>
public enum MobMode
{
    Calm,
    Alert,
    Combat,
    Flee,
    Return,
    Disabled,
}

/// <summary>A dodge the brain decided on.</summary>
public struct MobDodgeRequest
{
    public bool valid;
    public Vector3 direction;
    public float distance;
    public float time;
}

/// <summary>
/// The mob's decision maker. A few times per second it picks a target (threat, distance, visibility, taunts,
/// assisting its summoner, hysteresis), decides whether to flee (predators, feared types, low health against its
/// courage), dodge (telegraphed attacks and weapon swings, after its reaction time), give up (leash, chase time),
/// investigate (noises, lost targets) or fight - and in a fight whether to attack (via the
/// <see cref="MobAbilitySelector"/>), chase, reposition or back off. The states carry the decisions out.
/// </summary>
public sealed class MobBrain : ICombatHostility
{
    private readonly MobMovementContext ctx;

    public MobMode Mode { get; private set; } = MobMode.Calm;
    public EState DesiredState { get; private set; } = EState.Idle;
    public CombatEntity Target { get; private set; }
    /// <summary>What the mob is running from.</summary>
    public CombatEntity Threat { get; private set; }
    public bool InDanger { get; private set; }
    public bool StandingInHazard { get; private set; }
    public float PreferredMin { get; private set; }
    public float PreferredMax { get; private set; } = 2f;
    public bool RangedRole { get; private set; }
    public MobCombatStyle Style { get; private set; } = MobCombatStyle.Melee;
    public bool Cornered => Time.time < corneredUntil;
    public bool Leashed { get; private set; }
    public float CombatStartTime { get; private set; }
    public float LastAttackEndTime { get; private set; } = -999f;
    public bool LastAttackWasMelee { get; private set; }
    public float LastDodgeTime { get; private set; } = -999f;
    public bool DecisionRequested => decisionRequested;

    private MobAttackPlan plan;
    private bool hasPlan;
    private MobDodgeRequest dodge;
    private bool decisionRequested = true;
    private bool dangerCheckRequested;
    private float corneredUntil;
    private float lastEngageTime;
    private float lastHelpCall = -999f;
    private CombatEntity helpTarget;
    private float helpUntil;
    private bool fleeRolled;
    private bool fleeCommitted;
    private float unreachableSince = -1f;
    private readonly HashSet<int> judgedHazards = new HashSet<int>();
    private static int classifyDepth;

    public MobBrain(MobMovementContext context)
    {
        ctx = context;
    }

    public MobMemoryEntry TargetEntry => Target != null ? ctx.Memory.Get(Target) : null;

    public bool TargetVisible
    {
        get
        {
            MobMemoryEntry e = TargetEntry;
            return e != null && e.visible;
        }
    }

    /// <summary>Flat distance to the target (its current position if visible, else where it was last seen).</summary>
    public float TargetDistance
    {
        get
        {
            if (Target == null)
                return float.MaxValue;
            MobMemoryEntry e = TargetEntry;
            Vector3 p = e == null || e.visible ? Target.Position : e.lastKnownPosition;
            return CombatQuery.FlatDistance(ctx.Entity.Position, p);
        }
    }

    public bool InCombat => Mode == MobMode.Combat;

    public void RequestDecision() => decisionRequested = true;
    public void RequestDangerCheck() => dangerCheckRequested = true;

    /// <summary>Recomputes the preferred fighting distance from the current abilities (call after abilities change).</summary>
    public void RecomputeRanges()
    {
        ctx.Selector.ComputePreferredRange(out float min, out float max, out bool ranged);
        PreferredMin = min;
        PreferredMax = Mathf.Max(0.8f, max);
        RangedRole = ranged;
        MobCombatStyle style = ctx.Profile.combatStyle;
        if (style == MobCombatStyle.Auto)
            style = ranged ? MobCombatStyle.Ranged : MobCombatStyle.Melee;
        Style = style;
    }

    // ------------------------------------------------------------------ relations
    /// <summary>How this mob regards <paramref name="other"/> (flat distance used for skittish reactions).</summary>
    public MobRelationKind Classify(CombatEntity other, float distance)
    {
        CombatEntity self = ctx.Entity;
        if (other == null || other == self || !other.IsAlive || self == null)
            return MobRelationKind.Neutral;
        if (other.Team == self.Team || CombatParties.SameParty(self, other))
            return MobRelationKind.Ally;
        // Factions: allied factions never fight; hostile ones fight on sight (below, after taunts and threats).
        FactionStance? stance = FactionStanceTowards(self, other);
        if (stance == FactionStance.Ally)
            return MobRelationKind.Ally;
        if (self.TauntedBy == other)
            return MobRelationKind.Enemy;

        MobProfile p = ctx.Profile;
        if (self.GetThreat(other, p.memoryDuration * 2f) > 0f || (other == helpTarget && Time.time < helpUntil))
            return ReactionToAttacker();

        // Summons fight their summoner's enemies.
        CombatEntity summoner = self.Summoner;
        if (summoner != null && summoner.IsAlive && classifyDepth < 3)
        {
            classifyDepth++;
            try
            {
                if (other == summoner.LastTarget || other.LastTarget == summoner || other.GetThreat(summoner) > 0f)
                    return MobRelationKind.Enemy;
                if (summoner.Kind == CombatEntity.EntityKind.Player)
                    return CombatRelations.Get(other, summoner) == CombatRelation.Enemy ? MobRelationKind.Enemy : MobRelationKind.Neutral;
                if (summoner.Mob != null && summoner.Hostility is Mob summonerMob && summonerMob.AI != null && summonerMob.AI.Context != null)
                {
                    MobRelationKind k = summonerMob.AI.Context.Brain.Classify(other, distance);
                    return k == MobRelationKind.Threat ? MobRelationKind.Enemy : k;
                }
            }
            finally
            {
                classifyDepth--;
            }
        }

        if (stance == FactionStance.Enemy)
            return ctx.Profile.whenAttacked == MobReaction.Flee && !Cornered ? MobRelationKind.Threat : MobRelationKind.Enemy;

        List<string> preys = ctx.MobReference.PreysReference;
        Mob otherMob = other.Mob;
        if (otherMob != null)
        {
            string myType = ctx.MobReference.type;
            bool iHunt = preys != null && !string.IsNullOrEmpty(otherMob.type) && preys.Contains(otherMob.type);
            bool huntsMe = otherMob.PreysReference != null && !string.IsNullOrEmpty(myType) && otherMob.PreysReference.Contains(myType);
            bool feared = p.fears != null && p.fears.Contains(otherMob.type);
            if (iHunt && !feared)
                return MobRelationKind.Enemy;
            if (huntsMe || feared)
                return Cornered ? MobRelationKind.Enemy : MobRelationKind.Threat;
            return MobRelationKind.Neutral;
        }

        bool playerSide = other.Kind == CombatEntity.EntityKind.Player || AbilityAbsorption.PlayerSide(other) != null;
        if (playerSide)
        {
            if (preys != null && preys.Contains("Player"))
                return MobRelationKind.Enemy;
            if (p.fleeFromPlayersWithin > 0f && other.Kind == CombatEntity.EntityKind.Player && distance <= p.fleeFromPlayersWithin)
                return Cornered ? MobRelationKind.Enemy : MobRelationKind.Threat;
        }
        return MobRelationKind.Neutral;
    }

    /// <summary>The faction stance between two characters (null when factions do not decide: none, or neutral).</summary>
    private static FactionStance? FactionStanceTowards(CombatEntity self, CombatEntity other)
    {
        CombatFaction a = self.Faction, b = other.Faction;
        if (a == null && b == null)
            return null;
        FactionStance s = a != null ? a.StanceTowards(b) : b.StanceTowards(null);
        return s == FactionStance.Neutral ? (FactionStance?)null : s;
    }

    private MobRelationKind ReactionToAttacker()
    {
        MobProfile p = ctx.Profile;
        switch (p.whenAttacked)
        {
            case MobReaction.Flee:
                return Cornered ? MobRelationKind.Enemy : MobRelationKind.Threat;
            case MobReaction.FightIfHealthy:
                return ctx.Entity.HealthRatio > p.fleeHealthThreshold || Cornered ? MobRelationKind.Enemy : MobRelationKind.Threat;
            default:
                return MobRelationKind.Enemy;
        }
    }

    /// <summary>Ability filters ask this: can this mob's abilities hurt <paramref name="other"/>?</summary>
    public bool IsHostileTo(CombatEntity other)
    {
        if (other == null)
            return false;
        if (other == Target)
            return true;
        MobRelationKind k = Classify(other, CombatQuery.FlatDistance(ctx.Entity.Position, other.Position));
        return k == MobRelationKind.Enemy || k == MobRelationKind.Threat;
    }

    /// <summary>Would this mob start (or keep) a fight with the character in <paramref name="e"/>?</summary>
    private bool WillingToFight(MobMemoryEntry e)
    {
        CombatEntity self = ctx.Entity;
        CombatEntity other = e.entity;
        MobProfile p = ctx.Profile;
        if (self.TauntedBy == other || Cornered)
            return true;
        if (self.Summoner != null)
            return true;
        if (self.GetThreat(other, p.memoryDuration * 2f) > 0f || (other == helpTarget && Time.time < helpUntil))
            return p.whenAttacked != MobReaction.Flee;
        switch (p.aggression)
        {
            case MobAggression.Aggressive:
                return true;
            case MobAggression.Territorial:
                return other == Target || CombatQuery.FlatDistance(other.Position, ctx.Home) <= p.territoryRadius;
            default:
                return false;
        }
    }

    // ------------------------------------------------------------------ main loop
    /// <summary>Runs danger checks and decisions (called by the state machine every frame; decisions are throttled).</summary>
    public void Tick(float now, bool decideNow)
    {
        if (ctx.Entity == null)
            return;
        if (dangerCheckRequested || decideNow)
            CheckDanger(now);
        if (decideNow || decisionRequested || ctx.Machine.CurrentStateFinished)
            Decide(now);
    }

    private void Set(MobMode mode, EState state)
    {
        Mode = mode;
        DesiredState = state;
    }

    private void Decide(float now)
    {
        decisionRequested = false;
        CombatEntity self = ctx.Entity;
        MobProfile p = ctx.Profile;
        EState current = ctx.Machine.CurrentStateKey;
        bool finished = ctx.Machine.CurrentStateFinished;

        if (self.IsDead)
        {
            Set(MobMode.Disabled, EState.Dead);
            return;
        }
        if (self.IsStunned)
        {
            DesiredState = EState.Stunned;
            return;
        }

        InDanger = StandingInHazard || self.HealthRatio < 0.3f || ctx.Memory.HitsWithin(2f) >= 3;

        // Evading home after a leash: nothing interrupts it.
        if (Leashed && p.evadeWhileReturning && Mode == MobMode.Return && !(current == EState.Returning && finished))
        {
            DesiredState = EState.Returning;
            return;
        }

        SelectTarget(now);
        UpdateThreat();

        if (ShouldFlee())
        {
            if (Mode == MobMode.Combat)
                ExitCombat(false);
            Set(MobMode.Flee, EState.Fleeing);
            return;
        }

        if (dodge.valid)
        {
            if (now - dodge.time > 0.6f || !ctx.Motor.CanMove)
            {
                dodge.valid = false;
            }
            else
            {
                DesiredState = EState.Dodging;
                return;
            }
        }

        if (Target != null)
        {
            if (Mode != MobMode.Combat)
                EnterCombat(now);
            DecideCombat(now, current, finished);
            return;
        }
        if (Mode == MobMode.Combat)
            ExitCombat(true);

        if (ctx.Memory.HasInvestigatePoint && p.aggression != MobAggression.Passive && ctx.Entity.Summoner == null)
        {
            if (current == EState.Investigate && finished)
                ctx.Memory.ClearInvestigate();
            else
            {
                Set(MobMode.Alert, EState.Investigate);
                return;
            }
        }

        // Summons stay with their summoner.
        CombatEntity summoner = self.Summoner;
        if (summoner != null && summoner.IsAlive)
        {
            float d = CombatQuery.FlatDistance(self.Position, summoner.Position);
            if (current == EState.Moving && !finished && d > 2.5f)
                Set(MobMode.Calm, EState.Moving);
            else
                Set(MobMode.Calm, d > 6f ? EState.Moving : EState.Idle);
            return;
        }

        if (current == EState.Returning && !finished && Mode == MobMode.Return)
        {
            DesiredState = EState.Returning;
            return;
        }
        if (NeedsToReturn(current, finished))
        {
            Set(MobMode.Return, EState.Returning);
            return;
        }

        DecideCalm(current, finished);
    }

    private void DecideCalm(EState current, bool finished)
    {
        Mode = MobMode.Calm;
        switch (current)
        {
            case EState.Idle:
                if (!finished)
                {
                    DesiredState = EState.Idle;
                    return;
                }
                if (ctx.MobReference.PatrolPointCount > 0)
                    DesiredState = EState.Patrol;
                else if (ctx.Profile.wanderRadius > 0.5f && Random.value < 0.75f)
                    DesiredState = EState.Moving;
                else
                    DesiredState = EState.Idle;
                return;
            case EState.Moving:
            case EState.Patrol:
                DesiredState = finished ? EState.Idle : current;
                return;
            default:
                DesiredState = EState.Idle;
                return;
        }
    }

    private bool NeedsToReturn(EState current, bool finished)
    {
        MobProfile p = ctx.Profile;
        if (current == EState.Returning && finished)
        {
            Leashed = false;
            return false;
        }
        if (Leashed)
            return true;
        if (!p.returnHomeAfterCombat)
            return false;
        float limit = Mathf.Max(4f, p.wanderRadius + 3f);
        if (ctx.MobReference.PatrolPointCount > 0)
            return false; // patrollers resume their route instead
        return CombatQuery.FlatDistance(ctx.Entity.Position, ctx.Home) > limit && (current == EState.Fleeing || current == EState.Investigate || current == EState.Chasing || current == EState.Combat || current == EState.Retreating || current == EState.Idle);
    }

    // ------------------------------------------------------------------ combat
    private void EnterCombat(float now)
    {
        Mode = MobMode.Combat;
        CombatStartTime = now;
        lastEngageTime = now;
        unreachableSince = -1f;
        ctx.Selector.OnCombatStart();
        MobCombatCoordinator.Engage(ctx, Target);
        ctx.Memory.ClearInvestigate();
        ctx.Animation.OnAlert();
        CallForHelp(Target);
        ctx.Log($"engages {Target.name}");
    }

    private void ExitCombat(bool considerReturn)
    {
        MobCombatCoordinator.Disengage(ctx);
        hasPlan = false;
        Mode = MobMode.Calm;
        if (considerReturn)
            ctx.Log("leaves combat");
    }

    private void DecideCombat(float now, EState current, bool finished)
    {
        MobProfile p = ctx.Profile;
        CombatEntity self = ctx.Entity;
        CombatEntity t = Target;
        MobMemoryEntry entry = TargetEntry;
        if (entry == null)
            entry = ctx.Perception.ForceAware(t, t.Position);

        float d = TargetDistance;

        // Leash: too far from home.
        if (p.leashDistance > 0f && self.Summoner == null && CombatQuery.FlatDistance(self.Position, ctx.Home) > p.leashDistance && d > PreferredMax + 1f)
        {
            GiveUp(true);
            return;
        }

        // Engagement bookkeeping for the chase timeout.
        if (d <= PreferredMax + 1.5f || now - ctx.Memory.LastDamagedTime < 3f || now - LastAttackEndTime < 3f)
            lastEngageTime = now;
        if (p.maxChaseTime > 0f && now - lastEngageTime > p.maxChaseTime)
        {
            GiveUp(false);
            return;
        }

        // Unreachable targets (on a roof, across water) - use ranged abilities or give up after a while.
        if (ctx.Motor.PathIncomplete && d > PreferredMax + 1f)
        {
            if (unreachableSince < 0f) unreachableSince = now;
        }
        else
        {
            unreachableSince = -1f;
        }

        // Lost sight.
        if (!entry.visible && now - entry.lastSeenTime > 1f)
        {
            if (now - entry.lastSensedTime > p.memoryDuration)
            {
                ctx.Memory.SetInvestigate(entry.lastKnownPosition, 0.8f);
                ClearTarget();
                ExitCombat(false);
                Set(MobMode.Alert, EState.Investigate);
                return;
            }
            DesiredState = EState.Chasing;
            return;
        }

        if (current == EState.Attacking && !finished)
        {
            DesiredState = EState.Attacking;
            return;
        }

        // Attack?
        bool ready = now - LastAttackEndTime >= p.minTimeBetweenAttacks && !(t.IsEvading && Random.value < 0.6f);
        if (ready && ctx.Selector.TrySelect(t, out MobAttackPlan candidate))
        {
            bool token = !p.useAttackTokens || self.Summoner != null || MobCombatCoordinator.RequestToken(ctx, t, candidate.melee);
            if (token)
            {
                plan = candidate;
                hasPlan = true;
                DesiredState = EState.Attacking;
                return;
            }
        }

        if (unreachableSince > 0f && now - unreachableSince > 6f && !HasUsableRangedAbility())
        {
            GiveUp(false);
            return;
        }

        // Positioning.
        bool keepsDistance = RangedRole || Style == MobCombatStyle.Kiter;
        if (!Cornered && keepsDistance && d < PreferredMin * 0.75f)
            DesiredState = EState.Retreating;
        else if (!Cornered && keepsDistance && ctx.Memory.HitsWithin(2f) >= 2 && d < PreferredMax)
            DesiredState = EState.Retreating;
        else if (Style == MobCombatStyle.Skirmisher && LastAttackWasMelee && now - LastAttackEndTime < 1.2f && current != EState.Retreating)
            DesiredState = EState.Retreating;
        else if (current == EState.Retreating && !finished)
            DesiredState = EState.Retreating;
        else if (d > PreferredMax + 2.5f)
            DesiredState = EState.Chasing;
        else
            DesiredState = EState.Combat;
    }

    private bool HasUsableRangedAbility()
    {
        MobAbilityController c = ctx.Abilities;
        if (c == null)
            return false;
        IReadOnlyList<AbilitySlot> slots = c.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            AbilitySlot s = slots[i];
            if (s?.ability != null && s.enabled && !MobAbilitySelector.IsMelee(s) && s.ability.MaxReach(s.Stats) >= TargetDistance)
                return true;
        }
        return false;
    }

    private void GiveUp(bool leash)
    {
        ctx.Log(leash ? "leashed - returning home" : "gives up the chase");
        CombatEntity old = Target;
        ClearTarget();
        ExitCombat(false);
        if (old != null)
            ctx.Memory.Forget(old);
        ctx.Entity.ClearThreat();
        helpTarget = null;
        Leashed = leash;
        if (leash && ctx.Profile.evadeWhileReturning)
            ctx.Entity.SetInvulnerable(60f);
        Set(MobMode.Return, EState.Returning);
    }

    /// <summary>Called by the Returning state when home is reached.</summary>
    public void OnReachedHome()
    {
        if (Leashed)
        {
            ctx.Entity.ClearInvulnerable();
            float heal = ctx.Profile.healOnReturn * ctx.Entity.MaxHealth;
            if (heal > 0f)
                ctx.Entity.ApplyHeal(heal);
        }
        Leashed = false;
        fleeRolled = false;
        fleeCommitted = false;
        RequestDecision();
    }

    // ------------------------------------------------------------------ targets
    private void SelectTarget(float now)
    {
        CombatEntity self = ctx.Entity;
        MobProfile p = ctx.Profile;

        CombatEntity taunter = self.TauntedBy;
        if (taunter != null)
        {
            ctx.Perception.ForceAware(taunter, taunter.Position);
            SetTarget(taunter);
            return;
        }

        CombatEntity best = null;
        float bestScore = 0f;
        CombatEntity summonerTarget = self.Summoner != null ? self.Summoner.LastTarget : null;
        IReadOnlyList<MobMemoryEntry> entries = ctx.Memory.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            MobMemoryEntry e = entries[i];
            CombatEntity other = e.entity;
            if (other == null || !other.IsAlive || !e.detected || e.relation != MobRelationKind.Enemy)
                continue;
            if (now - e.lastSensedTime > p.memoryDuration)
                continue;
            if (!WillingToFight(e))
                continue;

            float score = 1f;
            float threat = self.GetThreat(other, p.memoryDuration * 2f);
            score += Mathf.Min(3f, threat / Mathf.Max(5f, self.MaxHealth * 0.1f));
            score += (1f - Mathf.Clamp01(e.distance / Mathf.Max(1f, p.sightRange))) * 1.5f;
            if (e.visible) score += 0.5f;
            if (other == Target) score += 0.75f;
            if (other.Kind == CombatEntity.EntityKind.Player) score += 0.25f;
            if (other == summonerTarget) score += 2f;
            if (other.HealthRatio < 0.3f) score += 0.3f;
            if (other == Target && unreachableSince > 0f && now - unreachableSince > 2f && !HasUsableRangedAbility())
                score *= 0.3f;

            if (score > bestScore)
            {
                bestScore = score;
                best = other;
            }
        }
        SetTarget(best);
    }

    private void SetTarget(CombatEntity t)
    {
        if (t == Target)
            return;
        CombatEntity old = Target;
        Target = t;
        hasPlan = false;
        unreachableSince = -1f;
        if (t != null && Mode == MobMode.Combat)
            MobCombatCoordinator.Engage(ctx, t);
        ctx.MobReference.SyncLegacyTargets(Target, Threat);
        if (old != null && t != null)
            ctx.Log($"switches target {old.name} → {t.name}");
    }

    private void ClearTarget() => SetTarget(null);

    /// <summary>Forces a target (summons, scripts). The mob becomes fully aware of it.</summary>
    public void ForceTarget(CombatEntity t)
    {
        if (t == null || !t.IsAlive)
            return;
        ctx.Perception.ForceAware(t, t.Position);
        helpTarget = t;
        helpUntil = Time.time + 20f;
        SetTarget(t);
        RequestDecision();
    }

    private void UpdateThreat()
    {
        CombatEntity best = null;
        float bestDist = float.MaxValue;
        IReadOnlyList<MobMemoryEntry> entries = ctx.Memory.Entries;
        float now = Time.time;
        for (int i = 0; i < entries.Count; i++)
        {
            MobMemoryEntry e = entries[i];
            if (e.entity == null || !e.entity.IsAlive || e.relation != MobRelationKind.Threat)
                continue;
            if (!e.detected || now - e.lastSensedTime > 3f)
                continue;
            if (e.distance < bestDist)
            {
                bestDist = e.distance;
                best = e.entity;
            }
        }
        if (best != Threat)
        {
            Threat = best;
            ctx.MobReference.SyncLegacyTargets(Target, Threat);
        }
    }

    private bool ShouldFlee()
    {
        CombatEntity self = ctx.Entity;
        MobProfile p = ctx.Profile;
        if (self.Summoner != null || Cornered)
            return false;

        if (Threat != null && Threat != Target)
        {
            MobMemoryEntry e = ctx.Memory.Get(Threat);
            float safe = Mathf.Max(p.fleeDistance, p.fleeFromPlayersWithin * 1.5f);
            if (e != null && (e.visible || Time.time - e.lastSensedTime < 2f) && e.distance < safe)
                return true;
        }

        if (p.fleeHealthThreshold > 0f && self.HealthRatio <= p.fleeHealthThreshold)
        {
            if (!fleeRolled)
            {
                fleeRolled = true;
                fleeCommitted = Random.value > p.courage;
                if (fleeCommitted) ctx.Log("loses its nerve and flees");
            }
            if (fleeCommitted)
            {
                CombatEntity from = Target != null ? Target : self.RecentAttacker(10f);
                if (from != null)
                {
                    Threat = from;
                    MobMemoryEntry e = ctx.Memory.Get(from);
                    return e == null || e.visible || Time.time - e.lastSensedTime < 3f;
                }
            }
        }
        else if (self.HealthRatio > p.fleeHealthThreshold + 0.15f)
        {
            fleeRolled = false;
            fleeCommitted = false;
        }
        return false;
    }

    /// <summary>Called by the Fleeing state when there is nowhere left to run: the mob turns and fights.</summary>
    public void SetCornered(float seconds)
    {
        corneredUntil = Time.time + seconds;
        CombatEntity from = Threat != null ? Threat : ctx.Entity.RecentAttacker(10f);
        if (from != null)
            ForceTarget(from);
        ctx.Log("is cornered and fights back");
        RequestDecision();
    }

    // ------------------------------------------------------------------ danger
    private void CheckDanger(float now)
    {
        dangerCheckRequested = false;
        MobProfile p = ctx.Profile;
        CombatEntity self = ctx.Entity;
        StandingInHazard = false;
        if (!p.avoidHazards && p.dodgeChance <= 0f)
            return;

        float horizon = p.dodgeDuration + 0.8f;
        if (!HazardRegistry.Query(self, self.Position, self.Radius, self.Height, horizon + 1f, out HazardInfo info))
            return;

        HazardArea h = info.hazard;
        StandingInHazard = info.timeToImpact <= 0.1f;
        if (now - h.createdTime < p.reactionTime)
        {
            dangerCheckRequested = true; // look again next frame
            return;
        }
        if (judgedHazards.Count > 64)
            judgedHazards.Clear();
        if (!judgedHazards.Add(h.id))
            return;

        bool urgent = info.timeToImpact <= horizon;
        bool canDodge = now - LastDodgeTime >= p.dodgeCooldown && ctx.Motor.CanMove && !dodge.valid;
        if (canDodge && Random.value < p.dodgeChance * (urgent ? 1f : 0.6f) * Mathf.Lerp(0.5f, 1f, h.severity + 0.3f))
        {
            dodge = new MobDodgeRequest
            {
                valid = true,
                direction = info.escapeDirection,
                distance = Mathf.Max(p.dodgeDistance, info.escapeDistance + self.Radius + 0.4f),
                time = now,
            };
            RequestDecision();
        }
    }

    /// <summary>Somebody started a weapon swing nearby.</summary>
    public void OnMeleeSwing(CombatEntity attacker)
    {
        MobProfile p = ctx.Profile;
        if (p.meleeDodgeChance <= 0f || dodge.valid || Time.time - LastDodgeTime < p.dodgeCooldown)
            return;
        if (Classify(attacker, 0f) != MobRelationKind.Enemy && attacker != Target)
            return;
        Vector3 toMe = ctx.Entity.Position - attacker.Position;
        toMe.y = 0f;
        float dist = toMe.magnitude;
        if (dist > 3.5f + attacker.Radius || dist < 1e-3f)
            return;
        if (Vector3.Dot(attacker.Forward, toMe / dist) < 0.5f)
            return;
        if (Random.value >= p.meleeDodgeChance)
            return;
        Vector3 side = Vector3.Cross(Vector3.up, toMe / dist) * (Random.value < 0.5f ? 1f : -1f);
        dodge = new MobDodgeRequest
        {
            valid = true,
            direction = (toMe / dist + side * 0.6f).normalized,
            distance = p.dodgeDistance * 0.8f,
            time = Time.time,
        };
        RequestDecision();
    }

    public bool ConsumeDodge(out MobDodgeRequest request)
    {
        request = dodge;
        bool ok = dodge.valid;
        dodge.valid = false;
        return ok;
    }

    public void NotifyDodged()
    {
        LastDodgeTime = Time.time;
        RequestDecision();
    }

    // ------------------------------------------------------------------ attacks
    public bool TryGetPlan(out MobAttackPlan p)
    {
        p = plan;
        return hasPlan && plan.Valid;
    }

    public void NotifyAttackFinished(bool performed, bool melee)
    {
        hasPlan = false;
        LastAttackEndTime = Time.time;
        LastAttackWasMelee = performed && melee;
        MobCombatCoordinator.ReleaseToken(ctx);
        if (performed)
            ctx.Selector.OnUsed();
        RequestDecision();
    }

    // ------------------------------------------------------------------ events
    public void OnDetected(MobMemoryEntry entry)
    {
        if (entry.relation == MobRelationKind.Threat)
        {
            CallForHelp(entry.entity);
            RequestDecision();
            return;
        }
        if (entry.relation == MobRelationKind.Enemy && WillingToFight(entry))
            RequestDecision();
    }

    public void OnDamaged(DamageInfo info)
    {
        if (!info.isPeriodic)
            ctx.Animation.OnHit();
        if (info.source != null && info.source != ctx.Entity)
        {
            if (Target == null)
                RequestDecision();
            CallForHelp(info.source);
        }
        if (ctx.Entity.HealthRatio <= ctx.Profile.fleeHealthThreshold)
            RequestDecision();
    }

    public void OnHelpRequested(CombatEntity caller, CombatEntity target)
    {
        float callRadius = caller.Mob != null ? caller.Mob.Profile.callForHelpRadius : 15f;
        float radius = Mathf.Min(callRadius, ctx.Profile.respondToHelpRadius);
        if ((caller.Position - ctx.Entity.Position).sqrMagnitude > radius * radius)
            return;
        if (target == ctx.Entity || target.Team == ctx.Entity.Team)
            return;
        helpTarget = target;
        helpUntil = Time.time + 20f;
        ctx.Perception.ForceAware(target, target.Position);
        RequestDecision();
    }

    private void CallForHelp(CombatEntity target)
    {
        if (target == null || ctx.Profile.callForHelpRadius <= 0f || Time.time - lastHelpCall < 3f)
            return;
        lastHelpCall = Time.time;
        CombatEvents.RaiseHelpRequested(ctx.Entity, target);
    }

    /// <summary>A summon learns its summoner's target.</summary>
    public void OnSummoned(CombatEntity target)
    {
        if (target != null)
            ForceTarget(target);
        RequestDecision();
    }

    /// <summary>No player is anywhere near: stop fighting/moving and idle until woken up.</summary>
    public void EnterSleep()
    {
        if (Mode == MobMode.Combat)
            ExitCombat(false);
        dodge.valid = false;
        Set(MobMode.Calm, EState.Idle);
    }

    /// <summary>The mob died.</summary>
    public void ForceDead()
    {
        MobCombatCoordinator.Disengage(ctx);
        Target = null;
        Threat = null;
        hasPlan = false;
        dodge.valid = false;
        Set(MobMode.Disabled, EState.Dead);
    }

    // ------------------------------------------------------------------ transitions
    /// <summary>Which state the current state should hand over to (used by every state's GetNextState).</summary>
    public EState ResolveNext(MobMovementState state)
    {
        EState cur = state.StateKey;
        if (cur == EState.Dead)
            return EState.Dead;
        if (DesiredState == EState.Dead || ctx.Entity.IsDead)
            return EState.Dead;
        if (ctx.Entity.IsStunned)
            return EState.Stunned;
        if (cur == EState.Stunned && !state.IsFinished)
            return EState.Stunned;
        EState desired = DesiredState == EState.Stunned ? EState.Idle : DesiredState;
        if (state.IsCommitted)
        {
            if (desired == EState.Dodging && cur == EState.Attacking && ctx.Profile.dodgeCancelsAttacks && state.CanCancelForDodge)
                return EState.Dodging;
            return cur;
        }
        return desired;
    }
}
