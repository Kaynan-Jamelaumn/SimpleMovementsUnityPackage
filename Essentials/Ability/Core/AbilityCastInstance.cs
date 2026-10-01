using System.Collections.Generic;
using UnityEngine;

/// <summary>What the caster wants to hit with a cast. Unset parts are filled in by the caster.</summary>
public struct CastRequest
{
    /// <summary>Target character (Unit abilities; also used to aim the others).</summary>
    public CombatEntity target;
    /// <summary>Aimed ground point (Point abilities).</summary>
    public Vector3 point;
    public bool hasPoint;
    /// <summary>Aim direction (Direction abilities).</summary>
    public Vector3 direction;
    public bool hasDirection;

    public static CastRequest AtTarget(CombatEntity target) => new CastRequest { target = target };

    public static CastRequest AtPoint(Vector3 point, CombatEntity target = null) =>
        new CastRequest { target = target, point = point, hasPoint = true };

    public static CastRequest InDirection(Vector3 direction, CombatEntity target = null) =>
        new CastRequest { target = target, direction = direction, hasDirection = true };

    public static CastRequest Self => default;
}

/// <summary>
/// Where an action is placed when it runs: position (feet level for ground anchors) and a yaw rotation facing the
/// aim.
/// </summary>
public struct ActionFrame
{
    public Vector3 position;
    public Quaternion rotation;
    public CombatEntity target;
    public Vector3 Forward => rotation * Vector3.forward;
}

/// <summary>
/// A hypothetical cast used by the AI to ask "if I cast this now at my target, would it hit?".
/// </summary>
public struct CastPreview
{
    public AbilityDefinition definition;
    public AbilityStats stats;
    public CombatEntity caster;
    /// <summary>Caster feet position.</summary>
    public Vector3 casterPosition;
    /// <summary>Yaw rotation toward the aim.</summary>
    public Quaternion aimRotation;
    /// <summary>Ground point aimed at (usually the predicted target position).</summary>
    public Vector3 aimPoint;
    public CombatEntity target;
    /// <summary>The target's body at the predicted time of impact.</summary>
    public TargetVolume targetVolume;

    public ActionFrame FrameFor(ActionAnchor anchor)
    {
        switch (anchor)
        {
            case ActionAnchor.AimPoint:
                return new ActionFrame { position = aimPoint, rotation = aimRotation, target = target };
            case ActionAnchor.TargetUnit:
                return new ActionFrame { position = target != null ? targetVolume.basePosition : aimPoint, rotation = aimRotation, target = target };
            default:
                return new ActionFrame { position = casterPosition, rotation = aimRotation, target = target };
        }
    }
}

/// <summary>
/// One use of an ability: who cast it, with what numbers, aimed where. Lives from the start of the cast until
/// every action it spawned is finished, so projectiles in flight still know their caster, team and damage even if
/// the caster dies.
/// </summary>
public sealed class AbilityCastInstance
{
    private static int nextId = 1;
    private static readonly List<CombatEntity> areaBuffer = new List<CombatEntity>(32);

    public readonly int Id;
    /// <summary>The caster component (null for standalone casts such as traps).</summary>
    public readonly AbilityCaster Caster;
    /// <summary>The casting character (may be destroyed while projectiles are still flying).</summary>
    public readonly CombatEntity CasterEntity;
    public readonly AbilitySlot Slot;
    public readonly AbilityDefinition Definition;
    public readonly AbilityStats Stats;
    public readonly AbilityModifierSet Modifiers;
    /// <summary>Team of the caster when the cast started (used if the caster is gone).</summary>
    public readonly string CasterTeam;
    public readonly CombatEntity.EntityKind CasterKind;
    public readonly int CasterParty;
    public readonly CombatFaction CasterFaction;

    public CombatEntity Target;
    public Vector3 AimPoint;
    /// <summary>Flat, normalized aim direction.</summary>
    public Vector3 AimDirection = Vector3.forward;
    /// <summary>
    /// The exact point the aim ray hit (not clamped to range or snapped to the ground). Players set it so projectiles
    /// fly toward what the crosshair is on; mobs aim at their target instead.
    /// </summary>
    public Vector3 AimTargetPoint;
    public bool HasAimTargetPoint;

    public AbilityPhase Phase { get; internal set; } = AbilityPhase.Casting;
    public float StartTime { get; internal set; }
    /// <summary>When the cast releases (planned while casting, actual afterwards).</summary>
    public float ReleaseTime { get; internal set; }
    public bool Released { get; internal set; }
    public bool Interrupted { get; internal set; }
    public CastInterruptReason InterruptReason { get; internal set; }
    /// <summary>Damage the caster took since the cast started (interrupt threshold).</summary>
    public float DamageTaken { get; internal set; }
    /// <summary>Resources paid, refunded if interrupted before release.</summary>
    internal float paidMana, paidStamina, paidHealth;

    internal readonly List<AbilityTelegraph> telegraphs = new List<AbilityTelegraph>(2);
    internal readonly List<HazardArea> hazards = new List<HazardArea>(2);
    internal GameObject castVfx;

    private Vector3 lastCasterPosition;
    private Vector3 lastCastPoint;
    private Quaternion lastCasterRotation = Quaternion.identity;

    public AbilityCastInstance(AbilityCaster caster, CombatEntity casterEntity, AbilitySlot slot, AbilityDefinition definition, AbilityModifierSet modifiers)
    {
        Id = nextId++;
        Caster = caster;
        CasterEntity = casterEntity;
        Slot = slot;
        Definition = definition;
        Modifiers = modifiers;
        Stats = slot != null ? slot.Stats : AbilityStats.From(modifiers);
        CasterTeam = casterEntity != null ? casterEntity.Team : "";
        CasterKind = casterEntity != null ? casterEntity.Kind : CombatEntity.EntityKind.Other;
        CasterParty = casterEntity != null ? casterEntity.PartyId : 0;
        CasterFaction = casterEntity != null ? casterEntity.Faction : null;
        StartTime = Time.time;
        if (casterEntity != null)
        {
            lastCasterPosition = casterEntity.BasePosition;
            lastCasterRotation = casterEntity.transform.rotation;
            AimDirection = CombatQuery.FlatDirection(Vector3.zero, casterEntity.Forward, Vector3.forward);
        }
        lastCastPoint = caster != null ? caster.CastPointPosition : lastCasterPosition + Vector3.up;
        AimPoint = lastCasterPosition + AimDirection * 2f;
    }

    /// <summary>Sets where the caster is (editor previews and casts without a caster entity).</summary>
    public void SetOrigin(Vector3 feet, Vector3 castPoint, Quaternion rotation)
    {
        lastCasterPosition = feet;
        lastCastPoint = castPoint;
        lastCasterRotation = rotation;
    }

    // ------------------------------------------------------------------ caster info (safe after death)
    public bool CasterAlive => CasterEntity != null && CasterEntity.IsAlive;

    /// <summary>Caster feet position (last known if the caster is gone).</summary>
    public Vector3 CasterPosition
    {
        get
        {
            if (CasterEntity != null)
                lastCasterPosition = CasterEntity.BasePosition;
            return lastCasterPosition;
        }
    }

    /// <summary>Where projectiles and beams leave the caster (hand, mouth, chest).</summary>
    public Vector3 CastPoint
    {
        get
        {
            if (Caster != null)
                lastCastPoint = Caster.CastPointPosition;
            return lastCastPoint;
        }
    }

    public Quaternion CasterRotation
    {
        get
        {
            if (CasterEntity != null)
                lastCasterRotation = CasterEntity.transform.rotation;
            return lastCasterRotation;
        }
    }

    public Quaternion AimRotation => Quaternion.LookRotation(AimDirection.sqrMagnitude > 1e-6f ? AimDirection : Vector3.forward, Vector3.up);

    public float Elapsed => Time.time - StartTime;

    // ------------------------------------------------------------------ relations
    /// <summary>Relation of <paramref name="other"/> to the caster, still correct after the caster died.</summary>
    public CombatRelation RelationTo(CombatEntity other)
    {
        if (other == null)
            return CombatRelation.Neutral;
        if (CasterEntity != null)
            return CombatRelations.Get(CasterEntity, other);
        // The caster is gone: the same order as CombatRelations.Get with what was known when it cast.
        if (CasterParty != 0 && other.PartyId == CasterParty)
            return CombatRelation.Party;
        if (CasterFaction != null && other.Faction != null)
        {
            FactionStance stance = CasterFaction.StanceTowards(other.Faction);
            if (stance == FactionStance.Ally) return CombatRelation.Ally;
            if (stance == FactionStance.Enemy) return CombatRelation.Enemy;
        }
        if (other.Team == CasterTeam)
            return CombatRelation.Ally;
        if (CasterKind == CombatEntity.EntityKind.Player || other.Kind == CombatEntity.EntityKind.Player)
            return CombatRelation.Enemy;
        return CombatRelation.Neutral;
    }

    public bool Passes(TargetFilter filter, CombatEntity other) => CombatRelations.Passes(filter, RelationTo(other));

    // ------------------------------------------------------------------ frames
    /// <summary>Where an action anchored at <paramref name="anchor"/> happens right now.</summary>
    public ActionFrame GetFrame(ActionAnchor anchor)
    {
        Quaternion rot = AimRotation;
        switch (anchor)
        {
            case ActionAnchor.AimPoint:
                return new ActionFrame { position = AimPoint, rotation = rot, target = Target };
            case ActionAnchor.TargetUnit:
                if (Target != null && Target.IsAlive)
                    return new ActionFrame { position = Target.BasePosition, rotation = rot, target = Target };
                return new ActionFrame { position = AimPoint, rotation = rot, target = Target };
            default:
                return new ActionFrame { position = CasterPosition, rotation = rot, target = Target };
        }
    }

    // ------------------------------------------------------------------ hits
    /// <summary>
    /// Applies <paramref name="hit"/> to one character if it passes the filter (and line of sight when required).
    /// Returns true if the character was hit.
    /// </summary>
    public bool ApplyHit(HitSettings hit, CombatEntity target, Vector3 origin, Vector3 direction, float multiplier = 1f)
    {
        if (hit == null || target == null || !target.IsAlive)
            return false;
        CombatRelation relation = RelationTo(target);
        if (!CombatTargeting.CanHit(hit.filter, hit.rules, relation, target, hit.IsHarmful))
            return false;
        if (hit.blockedByObstacles && !CombatQuery.HasLineOfSight(origin + Vector3.up * 0.6f, target.Center))
            return false;

        direction.y = 0f;
        if (direction.sqrMagnitude < 1e-6f)
            direction = CombatQuery.FlatDirection(origin, target.Position, AimDirection);
        else
            direction.Normalize();

        var ctx = new EffectContext
        {
            cast = this,
            caster = CasterEntity,
            hitEntity = target,
            origin = origin,
            point = target.Center,
            direction = direction,
            multiplier = multiplier,
            stats = Stats,
        };

        List<AbilityEffect> effects = hit.effects;
        for (int i = 0; i < effects.Count; i++)
        {
            AbilityEffect effect = effects[i];
            if (effect == null)
                continue;
            if (effect.chance < 1f && Random.value > effect.chance)
                continue;
            if (!CombatRelations.Passes(effect.onlyAffects, relation))
            {
                // Friendly fire pulled a party member / ally into a harmful hit: its harmful effects still apply to them.
                bool friendlyFire = effect.IsHarmful && (relation == CombatRelation.Party || relation == CombatRelation.Ally) &&
                                    (effect.onlyAffects & (TargetFilter.Enemies | TargetFilter.Neutral)) != 0 &&
                                    CombatTargeting.FriendlyFire(relation, hit.rules);
                if (!friendlyFire)
                    continue;
            }
            // Harm rule: damage, control, knockback and debuffs land on party members / allies only with friendly fire.
            if (effect.IsHarmful && effect.recipient == EffectRecipient.HitTarget && !CombatTargeting.CanHarm(relation, hit.rules))
                continue;
            if (effect.recipient == EffectRecipient.Caster)
            {
                if (CasterEntity == null || !CasterEntity.IsAlive)
                    continue;
                ctx.target = CasterEntity;
            }
            else
            {
                ctx.target = target;
            }
            effect.Apply(ref ctx);
            if (!target.IsAlive && effect.recipient == EffectRecipient.HitTarget)
                break;
        }

        if (hit.hitVfx != null)
            AbilityPool.PlayVfx(hit.hitVfx, target.Center, Quaternion.LookRotation(direction.sqrMagnitude > 1e-6f ? -direction : Vector3.back), 0f);
        if (hit.hitSound != null)
            AbilityPool.PlaySound(hit.hitSound, target.Center, Definition != null ? Definition.presentation.volume : 1f);
        return true;
    }

    /// <summary>
    /// Hits everyone inside <paramref name="shape"/> (closest first, up to Max Targets). Characters in
    /// <paramref name="alreadyHit"/> are skipped and newly hit ones are added to it. Returns how many were hit.
    /// </summary>
    public int HitArea(in ResolvedShape shape, HitSettings hit, Vector3 direction, HashSet<CombatEntity> alreadyHit = null)
    {
        if (hit == null)
            return 0;
        CombatQuery.Overlap(shape, areaBuffer);
        if (areaBuffer.Count == 0)
            return 0;
        Vector3 origin = shape.origin;
        if (hit.maxTargets > 0 && areaBuffer.Count > 1)
            CombatQuery.SortByDistance(areaBuffer, origin);

        float reach = hit.edgeMultiplier < 0.999f ? Mathf.Max(0.1f, shape.LocalReach()) : 0f;
        int count = 0;
        for (int i = 0; i < areaBuffer.Count; i++)
        {
            CombatEntity e = areaBuffer[i];
            if (alreadyHit != null && alreadyHit.Contains(e))
                continue;
            float mult = 1f;
            if (reach > 0f)
            {
                float t = Mathf.Clamp01(CombatQuery.FlatDistance(e.Position, origin) / reach);
                mult = Mathf.Lerp(1f, hit.edgeMultiplier, t);
            }
            Vector3 dir = shape.type == HitShapeType.Circle || shape.type == HitShapeType.Sphere || shape.type == HitShapeType.Ring || shape.type == HitShapeType.Cylinder
                ? CombatQuery.FlatDirection(origin, e.Position, direction)
                : direction;
            if (!ApplyHit(hit, e, origin, dir, mult))
                continue;
            alreadyHit?.Add(e);
            count++;
            if (hit.maxTargets > 0 && count >= hit.maxTargets)
                break;
        }
        areaBuffer.Clear();
        return count;
    }

    /// <summary>Makes a noise AI can hear, scaled by the ability's noise radius.</summary>
    public void EmitNoise(Vector3 position, float intensity = 0.8f)
    {
        if (Definition != null && Definition.presentation.noiseRadius > 0f)
            CombatEvents.EmitNoise(position, Definition.presentation.noiseRadius, CasterEntity, intensity);
    }

    /// <summary>Registers a danger area for AI dodging; removed automatically when the cast ends or is cancelled.</summary>
    public HazardArea AddHazard(in ResolvedShape shape, float activeFrom, float activeUntil, float severity, TargetFilter filter)
    {
        HazardArea h = HazardRegistry.AddShape(CasterEntity, filter, shape, activeFrom, activeUntil, severity);
        hazards.Add(h);
        return h;
    }

    internal void ClearTelegraphsAndHazards(bool removeHazards)
    {
        for (int i = 0; i < telegraphs.Count; i++)
            telegraphs[i]?.Release();
        telegraphs.Clear();
        if (removeHazards)
        {
            for (int i = 0; i < hazards.Count; i++)
                HazardRegistry.Remove(hazards[i]);
            hazards.Clear();
        }
    }

    /// <summary>How dangerous the ability is for AI dodging (0-1), from its damage and control.</summary>
    public float Severity
    {
        get
        {
            if (Definition == null)
                return 0.5f;
            float dmg = Definition.EstimateDamage(Stats);
            float ctl = Definition.EstimateControl(Stats);
            return Mathf.Clamp01(0.25f + dmg / 60f + ctl * 0.25f);
        }
    }

    public override string ToString() => $"{(Definition != null ? Definition.DisplayName : "?")} #{Id}";
}
