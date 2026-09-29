using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Who an action hits and what happens to each character hit.</summary>
[Serializable]
public class HitSettings
{
    [Tooltip("Who can be hit, relative to the caster.")]
    public TargetFilter filter = TargetFilter.Enemies;

    [Tooltip("Maximum characters hit (closest first). 0 = no limit.")]
    [Min(0)] public int maxTargets = 0;

    [Tooltip("Walls between the centre of the hit and a character protect it.")]
    public bool blockedByObstacles = true;

    [Tooltip("Strength at the edge of the area compared to the centre (1 = same everywhere, 0.5 = half at the edge). Scales damage and displacement.")]
    [Range(0f, 1f)] public float edgeMultiplier = 1f;

    [Tooltip("What happens to each character hit, in order.")]
    [SerializeReference, SubclassSelector] public List<AbilityEffect> effects = new List<AbilityEffect>();

    [Tooltip("Effect spawned on each character hit.")]
    public GameObject hitVfx;
    [Tooltip("Sound played on each character hit.")]
    public AudioClip hitSound;

    public HitSettings() { }

    public HitSettings(TargetFilter filter, params AbilityEffect[] effects)
    {
        this.filter = filter;
        this.effects = new List<AbilityEffect>(effects);
    }

    public float EstimateDamage(in AbilityStats s)
    {
        float total = 0f;
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] != null)
                total += effects[i].EstimateDamage(s) * effects[i].chance;
        }
        return total;
    }

    public float EstimateControl(in AbilityStats s)
    {
        float total = 0f;
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] != null)
                total = Mathf.Max(total, effects[i].EstimateControl(s) * effects[i].chance);
        }
        return total;
    }

    public string Describe(in AbilityStats s)
    {
        if (effects.Count == 0)
            return "no effect";
        var parts = new List<string>(effects.Count);
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] != null)
                parts.Add(effects[i].Describe(s));
        }
        return string.Join(", ", parts);
    }

    public void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (filter == TargetFilter.None)
            errors.Add($"{owner}: Hit Filter is empty, nothing can be hit.");
        if (effects == null || effects.Count == 0)
        {
            warnings.Add($"{owner}: has no effects; hits will do nothing.");
            return;
        }
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] == null)
                errors.Add($"{owner}: effect #{i + 1} is empty. Pick a type from its dropdown or remove it.");
            else
                effects[i].Validate($"{owner} › {effects[i].MenuName}", errors, warnings);
        }
    }

    public void Prewarm()
    {
        if (hitVfx != null)
            AbilityPool.Prewarm(hitVfx, 2);
    }

    public HitSettings Clone()
    {
        var c = (HitSettings)MemberwiseClone();
        c.effects = new List<AbilityEffect>(effects.Count);
        for (int i = 0; i < effects.Count; i++)
            c.effects.Add(effects[i]?.Clone());
        return c;
    }
}

/// <summary>
/// One thing an ability does when released: hit an area, fire projectiles, raise a wall, summon, dash... Actions run
/// in order; each can have a delay. Write a new action by deriving from this class: it appears in the dropdown.
/// </summary>
[Serializable]
public abstract class CastAction
{
    [Tooltip("Seconds after release before this action runs (e.g. a slam 0.3 s after a leap starts).")]
    [Min(0f)] public float delay = 0f;

    [Tooltip("Where the action happens:\nCaster: at the caster, facing the aim.\nAim Point: at the aimed ground point.\nTarget Unit: on the target character (falls back to the aim point).")]
    public ActionAnchor anchor = ActionAnchor.Caster;

    public virtual string MenuName => AbilityTypeNames.Nice(GetType());

    /// <summary>Runs the action (release + delay).</summary>
    public abstract void Execute(AbilityCastInstance cast);

    /// <summary>Adds the areas to telegraph while casting (world space). Return false for none.</summary>
    public virtual bool GetTelegraphShapes(AbilityCastInstance cast, List<ResolvedShape> shapes) => false;

    /// <summary>How long after its delay this action keeps the ability in the Launching phase (volleys, dashes, beams).</summary>
    public virtual float LaunchDuration(AbilityDefinition def, in AbilityStats s) => 0f;

    /// <summary>Seconds the telegraphed area stays dangerous after the action runs (AI dodging).</summary>
    public virtual float HazardDuration(AbilityDefinition def, in AbilityStats s) => 0.35f;

    /// <summary>Who should fear this action's telegraph (AI dodging), relative to the caster.</summary>
    public virtual TargetFilter HazardFilter => TargetFilter.Enemies;

    /// <summary>Farthest distance from the caster this action can affect (AI).</summary>
    public virtual float Reach(AbilityDefinition def, in AbilityStats s) => anchor == ActionAnchor.Caster ? 0f : def.Range(s);

    /// <summary>Would this action hit the target of <paramref name="preview"/>? Used by the mob AI.</summary>
    public virtual bool WouldHit(in CastPreview preview) => false;

    /// <summary>How many enemies of the caster this action would hit in <paramref name="preview"/> (AI area value).</summary>
    public virtual int CountHits(in CastPreview preview, List<CombatEntity> buffer) => WouldHit(preview) ? 1 : 0;

    public virtual float EstimateDamage(in AbilityStats s) => 0f;
    public virtual float EstimateControl(in AbilityStats s) => 0f;
    public virtual string Describe(AbilityDefinition def, in AbilityStats s) => MenuName;

    public virtual void Validate(AbilityDefinition def, string owner, List<string> errors, List<string> warnings)
    {
        if (anchor != ActionAnchor.Caster && def.targeting.mode == AbilityTargetingMode.Self)
            warnings.Add($"{owner}: anchored at {anchor} but the ability targets Self; it will happen at the caster's aim point in front of it.");
    }

    /// <summary>Creates pooled instances of the action's prefabs ahead of time.</summary>
    public virtual void Prewarm() { }

    /// <summary>Shallow copy (derived classes clone their nested lists).</summary>
    public virtual CastAction Clone() => (CastAction)MemberwiseClone();

    protected static float Scaled(float value, float multiplier) => value * multiplier;

    /// <summary>Counts the enemies of the preview caster overlapping a resolved shape.</summary>
    protected static int CountInShape(in CastPreview p, in ResolvedShape shape, TargetFilter filter, List<CombatEntity> buffer)
    {
        CombatQuery.Overlap(shape, buffer);
        int n = 0;
        for (int i = 0; i < buffer.Count; i++)
        {
            if (p.caster == null || CombatRelations.Passes(filter, p.caster, buffer[i]))
                n++;
        }
        buffer.Clear();
        return n;
    }
}

// ====================================================================================================== area hit
[Serializable, AbilityMenu("Hit/Area Hit", "Hits everyone inside a shape: melee swings (cone), stomps and novas (circle), slashes (rectangle/line), explosions at a point.", 0)]
public class AreaHitAction : CastAction
{
    [Tooltip("Shape and size of the hit area, relative to the anchor facing the aim.")]
    public HitShape shape = HitShape.ConeShape(3f, 100f);

    [Tooltip("How many times it hits (spinning blades, repeated slams).")]
    [Min(1)] public int hitCount = 1;

    [Tooltip("Seconds between hits when Hit Count > 1.")]
    [Min(0.05f)] public float hitInterval = 0.3f;

    [Tooltip("With several hits: can the same character be hit every time?")]
    public bool canHitSameTargetAgain = true;

    [Tooltip("Repeated hits move with the caster (spins) instead of staying where the first one happened.")]
    public bool followCaster = true;

    public HitSettings hit = new HitSettings(TargetFilter.Enemies, new DamageEffect());

    [Tooltip("Effect played at the area on each hit.")]
    public GameObject vfx;

    [Tooltip("Scale the effect with the Area modifier.")]
    public bool scaleVfxWithArea = true;

    [Tooltip("Sound played on each hit.")]
    public AudioClip sound;

    public ResolvedShape Resolve(in ActionFrame frame, in AbilityStats s) => ResolvedShape.Resolve(shape, frame.position, frame.rotation, s.area);

    public override void Execute(AbilityCastInstance cast)
    {
        HashSet<CombatEntity> hitSet = !canHitSameTargetAgain && hitCount > 1 ? new HashSet<CombatEntity>() : null;
        HitOnce(cast, hitSet);
        if (hitCount > 1)
            AbilityRuntime.Add(new RepeatingAreaHit(this, cast, hitSet));
    }

    internal void HitOnce(AbilityCastInstance cast, HashSet<CombatEntity> hitSet, ActionFrame? fixedFrame = null)
    {
        ActionFrame frame = fixedFrame ?? cast.GetFrame(anchor);
        ResolvedShape r = Resolve(frame, cast.Stats);
        cast.HitArea(r, hit, frame.Forward, hitSet);
        if (vfx != null)
            AbilityPool.PlayVfx(vfx, r.origin, r.rotation, 0f, scaleVfxWithArea ? cast.Stats.area : 1f);
        if (sound != null)
            AbilityPool.PlaySound(sound, r.origin, cast.Definition.presentation.volume);
        cast.EmitNoise(r.origin);
    }

    public override bool GetTelegraphShapes(AbilityCastInstance cast, List<ResolvedShape> shapes)
    {
        shapes.Add(Resolve(cast.GetFrame(anchor), cast.Stats));
        return true;
    }

    public override float LaunchDuration(AbilityDefinition def, in AbilityStats s) => hitCount > 1 ? (hitCount - 1) * hitInterval : 0f;

    public override float HazardDuration(AbilityDefinition def, in AbilityStats s) => (hitCount - 1) * hitInterval + 0.35f;

    public override TargetFilter HazardFilter => hit.filter;

    public override float Reach(AbilityDefinition def, in AbilityStats s)
    {
        float r = shape.Reach(s.area);
        return anchor == ActionAnchor.Caster ? r : def.Range(s) + r;
    }

    public override bool WouldHit(in CastPreview p)
    {
        ResolvedShape r = ResolvedShape.Resolve(shape, p.FrameFor(anchor).position, p.aimRotation, p.stats.area);
        return r.Overlaps(p.targetVolume);
    }

    public override int CountHits(in CastPreview p, List<CombatEntity> buffer)
    {
        ResolvedShape r = ResolvedShape.Resolve(shape, p.FrameFor(anchor).position, p.aimRotation, p.stats.area);
        return CountInShape(p, r, hit.filter, buffer);
    }

    public override float EstimateDamage(in AbilityStats s) => hit.EstimateDamage(s) * hitCount;
    public override float EstimateControl(in AbilityStats s) => hit.EstimateControl(s);

    public override string Describe(AbilityDefinition def, in AbilityStats s)
    {
        string times = hitCount > 1 ? $" x{hitCount}" : "";
        return $"{shape.Describe(s.area)}{times}: {hit.Describe(s)}";
    }

    public override void Validate(AbilityDefinition def, string owner, List<string> errors, List<string> warnings)
    {
        base.Validate(def, owner, errors, warnings);
        shape.Validate(owner, errors);
        hit.Validate(owner, errors, warnings);
        if (anchor == ActionAnchor.Caster && shape.type != HitShapeType.Sphere && shape.baseOffset > 0.5f)
            warnings.Add($"{owner}: Base Offset {shape.baseOffset} starts above the caster's feet; short targets may be missed.");
    }

    public override void Prewarm()
    {
        if (vfx != null) AbilityPool.Prewarm(vfx, 1);
        hit.Prewarm();
    }

    public override CastAction Clone()
    {
        var c = (AreaHitAction)base.Clone();
        c.shape = shape.Clone();
        c.hit = hit.Clone();
        return c;
    }

    /// <summary>Repeats an area hit every interval (spins, pulsing slams).</summary>
    private sealed class RepeatingAreaHit : IAbilityRuntimeObject
    {
        private readonly AreaHitAction action;
        private readonly AbilityCastInstance cast;
        private readonly HashSet<CombatEntity> hitSet;
        private readonly ActionFrame firstFrame;
        private int remaining;
        private float timer;

        public RepeatingAreaHit(AreaHitAction action, AbilityCastInstance cast, HashSet<CombatEntity> hitSet)
        {
            this.action = action;
            this.cast = cast;
            this.hitSet = hitSet;
            firstFrame = cast.GetFrame(action.anchor);
            remaining = action.hitCount - 1;
            timer = action.hitInterval;
        }

        public bool Tick(float dt)
        {
            if (cast.Interrupted)
                return false;
            timer -= dt;
            if (timer > 0f)
                return true;
            timer += action.hitInterval;
            if (action.followCaster || action.anchor != ActionAnchor.Caster)
                action.HitOnce(cast, hitSet);
            else
                action.HitOnce(cast, hitSet, firstFrame);
            remaining--;
            return remaining > 0;
        }

        public void Dispose() { }
    }
}

// ====================================================================================================== direct
[Serializable, AbilityMenu("Hit/Direct Effect", "Applies the effects straight to the target and/or the caster with no area: bites, single-target heals, self buffs.", 1)]
public class DirectEffectAction : CastAction
{
    public enum Recipient
    {
        /// <summary>The cast's target character.</summary>
        Target,
        /// <summary>The caster itself.</summary>
        Caster,
        /// <summary>Both (effects still respect their own filters).</summary>
        TargetAndCaster,
    }

    [Tooltip("Who receives the effects.")]
    public Recipient applyTo = Recipient.Target;

    [Tooltip("The target must still be within the ability's range + this margin when released, otherwise it misses (melee bites can be dodged by stepping back).")]
    [Min(0f)] public float rangeTolerance = 1f;

    public HitSettings hit = new HitSettings(TargetFilter.Enemies, new DamageEffect());

    [Tooltip("Effect spawned on the recipient.")]
    public GameObject vfx;

    public DirectEffectAction()
    {
        anchor = ActionAnchor.TargetUnit;
    }

    public override void Execute(AbilityCastInstance cast)
    {
        if (applyTo != Recipient.Caster)
        {
            CombatEntity t = cast.Target;
            if (t != null && t.IsAlive)
            {
                float range = cast.Definition.Range(cast.Stats) + rangeTolerance + t.Radius;
                if (cast.Definition.targeting.mode == AbilityTargetingMode.Self || CombatQuery.FlatDistance(cast.CasterPosition, t.Position) <= range)
                {
                    cast.ApplyHit(hit, t, cast.CasterPosition, cast.AimDirection);
                    if (vfx != null)
                        AbilityPool.PlayVfx(vfx, t.Center, Quaternion.identity);
                }
            }
        }

        if (applyTo != Recipient.Target && cast.CasterEntity != null && cast.CasterEntity.IsAlive)
        {
            HitSettings selfHit = hit;
            cast.ApplyHit(selfHit, cast.CasterEntity, cast.CasterPosition, cast.AimDirection);
            if (vfx != null)
                AbilityPool.PlayVfx(vfx, cast.CasterEntity.Center, Quaternion.identity);
        }
        cast.EmitNoise(cast.CasterPosition, 0.6f);
    }

    public override float Reach(AbilityDefinition def, in AbilityStats s) => applyTo == Recipient.Caster ? 0f : def.Range(s) + rangeTolerance;

    public override bool WouldHit(in CastPreview p)
    {
        if (applyTo == Recipient.Caster)
            return true;
        float range = p.definition.Range(p.stats) + p.targetVolume.radius;
        return CombatQuery.FlatDistance(p.casterPosition, p.targetVolume.basePosition) <= range;
    }

    public override float EstimateDamage(in AbilityStats s) => applyTo == Recipient.Caster ? 0f : hit.EstimateDamage(s);
    public override float EstimateControl(in AbilityStats s) => applyTo == Recipient.Caster ? 0f : hit.EstimateControl(s);

    public override string Describe(AbilityDefinition def, in AbilityStats s)
    {
        string who = applyTo == Recipient.Caster ? "Self" : (applyTo == Recipient.Target ? "Target" : "Target and self");
        return $"{who}: {hit.Describe(s)}";
    }

    public override void Validate(AbilityDefinition def, string owner, List<string> errors, List<string> warnings)
    {
        hit.Validate(owner, errors, warnings);
        if (applyTo != Recipient.Caster && def.targeting.mode == AbilityTargetingMode.Self)
            warnings.Add($"{owner}: applies to the Target but the ability targets Self; set Targeting Mode to Unit.");
        if (applyTo == Recipient.Caster && (hit.filter & TargetFilter.Self) == 0)
            errors.Add($"{owner}: applies to the Caster but the Hit Filter does not include Self.");
    }

    public override void Prewarm()
    {
        if (vfx != null) AbilityPool.Prewarm(vfx, 1);
        hit.Prewarm();
    }

    public override CastAction Clone()
    {
        var c = (DirectEffectAction)base.Clone();
        c.hit = hit.Clone();
        return c;
    }
}
