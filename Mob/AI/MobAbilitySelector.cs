using System.Collections.Generic;
using UnityEngine;

/// <summary>An ability the mob decided to use, and what it needs to use it.</summary>
public struct MobAttackPlan
{
    public int slot;
    public AbilityDefinition ability;
    public CombatEntity target;
    /// <summary>Distance band (metres, centre to centre) the ability works in.</summary>
    public float minRange, maxRange;
    public bool needsLineOfSight;
    public bool melee;
    public float score;
    public float createdTime;
    public bool Valid => ability != null && slot >= 0;
}

/// <summary>
/// Picks the best ability for the situation: in range, with line of sight, predicted to hit (shapes, projectiles,
/// areas with enough targets), suited to the moment (gap closer when far, escape when in danger, control on a
/// casting target, heal when hurt, opener at the start of a fight) and weighted by the ability's AI priority, the
/// slot's AI weight and a bit of randomness.
/// </summary>
public sealed class MobAbilitySelector
{
    private readonly MobMovementContext ctx;
    private readonly List<CombatEntity> buffer = new List<CombatEntity>(16);
    private int usesThisFight;

    public MobAbilitySelector(MobMovementContext context)
    {
        ctx = context;
    }

    /// <summary>Call when a fight starts (resets the opener logic).</summary>
    public void OnCombatStart() => usesThisFight = 0;

    /// <summary>Call when an ability was used.</summary>
    public void OnUsed() => usesThisFight++;

    /// <summary>Distance band where the slot's ability can hit the target.</summary>
    public void GetRange(AbilitySlot slot, CombatEntity target, out float min, out float max)
    {
        AbilityDefinition def = slot.ability;
        AbilityStats s = slot.Stats;
        float bodies = (target != null ? target.Radius : 0.5f) + ctx.Entity.Radius * 0.5f;
        min = def.MinRange(s);
        if (def.ai.preferredMinDistance > 0f)
            min = Mathf.Max(min, def.ai.preferredMinDistance);
        max = def.MaxReach(s) + bodies * 0.5f;
        if (def.ai.preferredMaxDistance > 0f)
            max = Mathf.Min(max, def.ai.preferredMaxDistance + bodies);
        if (def.targeting.mode == AbilityTargetingMode.Unit || def.targeting.mode == AbilityTargetingMode.Point)
            max = Mathf.Min(max, def.Range(s) + bodies);
        if (max < min)
            max = min + 0.5f;
    }

    /// <summary>Is this ability a melee ability (short reach)?</summary>
    public static bool IsMelee(AbilitySlot slot)
    {
        AbilityDefinition d = slot.ability;
        if (d.HasTag(AbilityTag.Melee))
            return true;
        if (d.HasTag(AbilityTag.Ranged) || d.HasTag(AbilityTag.Projectile))
            return false;
        return d.MaxReach(slot.Stats) <= 4.5f;
    }

    /// <summary>
    /// Chooses an ability to use on <paramref name="target"/> now. Returns false if nothing is worth using.
    /// </summary>
    public bool TrySelect(CombatEntity target, out MobAttackPlan plan)
    {
        plan = default;
        MobAbilityController caster = ctx.Abilities;
        if (caster == null || target == null || !target.IsAlive || !ctx.Entity.CanCast || caster.IsBusy)
            return false;

        MobProfile profile = ctx.Profile;
        MobBrain brain = ctx.Brain;
        float now = Time.time;
        Vector3 pos = ctx.Entity.Position;
        float dist = CombatQuery.FlatDistance(pos, target.Position);
        float ownHealth = ctx.Entity.HealthRatio;
        float targetHealth = target.HealthRatio;
        bool inDanger = brain.InDanger;
        bool targetCasting = target.Caster != null && target.Caster.IsCasting;
        bool targetControlled = target.IsStunned || target.IsRooted;
        bool hasLos = brain.TargetVisible;
        float threshold = Mathf.Lerp(0.45f, 0.12f, profile.aggressiveness);

        float bestScore = 0f;
        IReadOnlyList<AbilitySlot> slots = caster.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            AbilitySlot slot = slots[i];
            if (slot == null || slot.ability == null || !slot.enabled || slot.aiWeight <= 0f || !slot.IsReady)
                continue;
            AbilityDefinition def = slot.ability;
            AbilityAIHints ai = def.ai;
            if (ai.priority <= 0f || now < slot.aiReadyTime)
                continue;
            if (ownHealth > ai.useBelowOwnHealth + 1e-3f || targetHealth > ai.useBelowTargetHealth + 1e-3f)
                continue;
            if (ai.avoidAllyOverlap && MobCombatCoordinator.CastingSameAbility(def, ctx.Entity) > 0)
                continue;
            bool selfCentered = def.targeting.mode == AbilityTargetingMode.Self;
            bool support = def.HasTag(AbilityTag.Support) || def.HasTag(AbilityTag.Buff) || def.HasTag(AbilityTag.Summon);

            GetRange(slot, target, out float min, out float max);
            bool melee = IsMelee(slot);
            bool inRange = dist >= min - 0.25f && dist <= max;

            // Must be usable from here (support abilities do not need the target in range).
            if (!support && !def.HasTag(AbilityTag.Escape) && !inRange)
                continue;

            bool needsLos = def.targeting.requireLineOfSight && !selfCentered;
            if (needsLos && !hasLos && !support)
                continue;

            // Would it hit?
            AbilityStats stats = slot.Stats;
            int hits = 1;
            if (!support && !def.HasTag(AbilityTag.Escape))
            {
                CastPreview preview = BuildPreview(def, stats, target);
                bool anyHit = false;
                hits = 0;
                for (int a = 0; a < def.actions.Count; a++)
                {
                    CastAction action = def.actions[a];
                    if (action == null)
                        continue;
                    if (action.WouldHit(preview))
                    {
                        anyHit = true;
                        hits = Mathf.Max(hits, action.CountHits(preview, buffer));
                    }
                }
                if (ai.requireTargetInArea && !anyHit)
                    continue;
                if (hits < ai.minTargets)
                    continue;
                hits = Mathf.Max(1, hits);
            }

            // Value.
            float dmg = def.EstimateDamage(stats);
            float ctl = def.EstimateControl(stats);
            float cd = Mathf.Max(1f, def.Cooldown(stats) + def.CastTime(stats));
            float value = 1f + (dmg + ctl * 12f) / (15f + dmg * 0.25f) + cd * 0.04f; // big cooldown abilities are "special"
            float score = ai.priority * slot.aiWeight * value;

            if (hits > 1)
                score *= 1f + (hits - 1) * 0.5f;

            // Situation.
            float prefMid = (min + max) * 0.5f;
            if (!support)
                score *= dist <= max && dist >= min ? 1.2f : 0.7f;
            if (def.HasTag(AbilityTag.GapCloser))
                score *= dist > brain.PreferredMax + 2f ? 2.2f : 0.35f;
            if (def.HasTag(AbilityTag.Escape))
                score *= inDanger || dist < brain.PreferredMin * 0.6f ? 3f : 0.05f;
            if (ai.defensive)
                score *= inDanger || ownHealth < 0.5f ? 2.2f : 0.3f;
            if (def.HasTag(AbilityTag.Control) || ctl > 0f)
            {
                if (targetCasting) score *= 2.2f;         // interrupt!
                if (targetControlled) score *= 0.35f;    // do not waste control on a controlled target
            }
            if (def.HasTag(AbilityTag.Summon))
                score *= SummonAction.SummonRegistry.CountFor(ctx.Entity) >= 3 ? 0.1f : 1.4f;
            if (def.HasTag(AbilityTag.Support) && ai.useBelowOwnHealth < 1f)
                score *= 1.5f + (1f - ownHealth);
            if (ai.opener && usesThisFight == 0)
                score *= 2.5f;
            if (melee && dist <= max)
                score *= 1.1f;
            if (Mathf.Abs(dist - prefMid) < (max - min) * 0.25f)
                score *= 1.1f;

            // Hazard: do not stand still casting inside danger unless it is quick.
            if (inDanger && def.movementWhileCasting == CasterMovementRule.Stop && def.CastTime(stats) > 0.4f && !def.HasTag(AbilityTag.Escape))
                score *= 0.4f;

            if (profile.abilityRandomness > 0f)
                score *= Random.Range(1f - profile.abilityRandomness, 1f + profile.abilityRandomness);

            if (score > bestScore)
            {
                bestScore = score;
                plan = new MobAttackPlan
                {
                    slot = i,
                    ability = def,
                    target = target,
                    minRange = min,
                    maxRange = max,
                    needsLineOfSight = needsLos,
                    melee = melee,
                    score = score,
                    createdTime = now,
                };
            }
        }

        return plan.Valid && bestScore >= threshold;
    }

    /// <summary>A hypothetical cast at the target's predicted position when the ability would land.</summary>
    public CastPreview BuildPreview(AbilityDefinition def, in AbilityStats stats, CombatEntity target)
    {
        CombatEntity self = ctx.Entity;
        Vector3 from = self.Position;
        float travel = 0f;
        float dist = CombatQuery.FlatDistance(from, target.Position);
        for (int a = 0; a < def.actions.Count; a++)
        {
            if (def.actions[a] is ITravellingAction t)
            {
                travel = t.TravelTime(stats, dist) + def.actions[a].delay;
                break;
            }
            if (def.actions[a] != null)
                travel = Mathf.Max(travel, def.actions[a].delay);
        }
        float lead = def.targeting.aimLock == AbilityAimLock.LockAtCastStart ? 1f : def.targeting.leadTarget;
        float t0 = def.CastTime(stats) + travel;
        // Locked telegraphs: the target will try to leave; assume it keeps moving (mobs learn nothing about dodges).
        Vector3 predicted = Vector3.Lerp(target.Position, CombatQuery.Predict(target, t0), Mathf.Clamp01(lead));
        Vector3 aimPoint = AbilityCaster.ClampAimPoint(def, stats, from, predicted);
        Quaternion rot = Quaternion.LookRotation(CombatQuery.FlatDirection(from, predicted, self.Forward), Vector3.up);
        return new CastPreview
        {
            definition = def,
            stats = stats,
            caster = self,
            casterPosition = self.BasePosition,
            aimRotation = rot,
            aimPoint = aimPoint,
            target = target,
            targetVolume = new TargetVolume(new Vector3(predicted.x, target.BasePosition.y, predicted.z), target.Radius, target.Height),
        };
    }

    /// <summary>
    /// The distance band the mob should keep from its target, derived from its abilities (weighted by priority)
    /// unless the profile sets it.
    /// </summary>
    public void ComputePreferredRange(out float min, out float max, out bool rangedRole)
    {
        MobProfile p = ctx.Profile;
        float bestMeleeReach = 0f;
        float rangedMin = 0f, rangedMax = 0f, rangedWeight = 0f, meleeWeight = 0f;
        MobAbilityController caster = ctx.Abilities;
        if (caster != null)
        {
            IReadOnlyList<AbilitySlot> slots = caster.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                AbilitySlot s = slots[i];
                if (s?.ability == null || !s.enabled || s.aiWeight <= 0f)
                    continue;
                AbilityDefinition d = s.ability;
                if (d.HasTag(AbilityTag.Support) || d.HasTag(AbilityTag.Summon) || d.HasTag(AbilityTag.Escape) || d.HasTag(AbilityTag.Defensive))
                    continue;
                GetRange(s, null, out float mn, out float mx);
                float w = d.ai.priority * s.aiWeight * (1f + d.EstimateDamage(s.Stats) * 0.05f);
                if (IsMelee(s))
                {
                    bestMeleeReach = Mathf.Max(bestMeleeReach, mx);
                    meleeWeight += w;
                }
                else
                {
                    rangedMin += Mathf.Max(mn, mx * 0.35f) * w;
                    rangedMax += mx * 0.8f * w;
                    rangedWeight += w;
                }
            }
        }

        MobCombatStyle style = p.combatStyle;
        if (style == MobCombatStyle.Auto)
            style = rangedWeight > meleeWeight * 1.2f ? MobCombatStyle.Ranged : MobCombatStyle.Melee;
        rangedRole = style == MobCombatStyle.Ranged || style == MobCombatStyle.Kiter;

        if (rangedRole && rangedWeight > 0f)
        {
            min = rangedMin / rangedWeight;
            max = Mathf.Max(min + 2f, rangedMax / rangedWeight);
        }
        else
        {
            float reach = bestMeleeReach > 0f ? bestMeleeReach : 2f;
            min = 0f;
            max = reach;
        }
        if (p.preferredMinRange > 0f) min = p.preferredMinRange;
        if (p.preferredMaxRange > 0f) max = Mathf.Max(min + 0.5f, p.preferredMaxRange);
    }
}
