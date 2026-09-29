using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns the old <see cref="AbilityEffectSO"/> assets (and <see cref="AbilityHolder"/> entries) into
/// <see cref="AbilityDefinition"/>s so existing prefabs keep working with the new system. Conversions are cached;
/// the editor menu "Tools > Abilities > Convert Selected Legacy Abilities" saves them as real assets you can tune.
/// </summary>
public static class LegacyAbilityConverter
{
    private struct Key : System.IEquatable<Key>
    {
        public AbilityEffectSO legacy;
        public bool forMob;
        public GameObject particle;
        public bool Equals(Key o) => ReferenceEquals(legacy, o.legacy) && forMob == o.forMob && ReferenceEquals(particle, o.particle);
        public override bool Equals(object obj) => obj is Key k && Equals(k);
        public override int GetHashCode() =>
            (RefHash(legacy) * 397) ^ (forMob ? 1 : 0) ^ (RefHash(particle) * 31);
        private static int RefHash(object o) => o == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
    }

    private static readonly Dictionary<Key, AbilityDefinition> cache = new Dictionary<Key, AbilityDefinition>();
    private static readonly Dictionary<string, AbilityDefinition> byId = new Dictionary<string, AbilityDefinition>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        cache.Clear();
        byId.Clear();
    }

    /// <summary>A previously converted ability with this id (null if none).</summary>
    public static AbilityDefinition FindConverted(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        byId.TryGetValue(id, out AbilityDefinition d);
        return d;
    }

    /// <summary>Converts a holder (legacy ability + its attack casts and particle) for a mob or the player.</summary>
    public static AbilityDefinition Convert(AbilityHolder holder, bool forMob)
    {
        if (holder == null || holder.abilityEffect == null)
            return null;
        return Convert(holder.abilityEffect, forMob, holder.attackCast, holder.particle);
    }

    /// <summary>Converts a legacy ability (cached: the same inputs return the same definition).</summary>
    public static AbilityDefinition Convert(AbilityEffectSO legacy, bool forMob, IList<AttackCast> holderCasts = null, GameObject holderParticle = null)
    {
        if (legacy == null)
            return null;
        var key = new Key { legacy = legacy, forMob = forMob, particle = holderParticle != null ? holderParticle : null };
        if (cache.TryGetValue(key, out AbilityDefinition existing) && existing != null)
            return existing;

        AbilityDefinition def = CreateDefinition(legacy, forMob, holderCasts, holderParticle);
        string id = "legacy:" + legacy.name + (forMob ? ":mob" : ":player");
        def.InitializeRuntime(id, legacy.name, legacy);
        cache[key] = def;
        byId[id] = def;
        return def;
    }

    /// <summary>
    /// Builds a new (unsaved) definition mirroring the legacy ability. Used at runtime and by the editor converter
    /// (which then saves it as an asset).
    /// </summary>
    public static AbilityDefinition CreateDefinition(AbilityEffectSO legacy, bool forMob, IList<AttackCast> holderCasts = null, GameObject holderParticle = null)
    {
        var def = ScriptableObject.CreateInstance<AbilityDefinition>();
        def.name = legacy.name;
        def.SetDisplayName(legacy.name);
        def.SetLegacySource(legacy);
        def.description = "Converted from the legacy ability '" + legacy.name + "'.";

        // ---- timing
        def.castTime = Mathf.Max(0f, legacy.castDuration);
        def.launchTime = 0f;
        def.activeTime = Mathf.Max(0f, legacy.duration);
        def.cooldown = Mathf.Max(0f, legacy.coolDown);
        def.movementWhileCasting = CasterMovementRule.Free;
        def.interruptedByControl = true;
        def.blocksOtherAbilities = BlockMask(legacy);

        // ---- shapes
        List<AttackCast> casts = CollectCasts(legacy, holderCasts);
        AttackCast primary = casts.Count > 0 ? casts[0] : null;
        GameObject particle = holderParticle != null ? holderParticle : legacy.particle;
        float launch = Mathf.Max(0f, legacy.finalLaunchTime);

        // ---- targeting
        AbilityTargeting t = def.targeting;
        t.leadTarget = 0f;
        t.aimTurnRate = 0f;
        t.requireLineOfSight = forMob;
        t.faceAim = true;
        float playerRange = legacy is PlayerAbilitySO pso ? pso.MaxRange : 0f;
        t.playerConfirmsTarget = !forMob && legacy.doesAbilityNeedsConfirmationClickToLaunch;

        var enemyEffects = new List<AbilityEffect>();
        var buffEffects = new List<AbilityEffect>();
        int maxTargets = 0;
        if (legacy.effects != null)
        {
            for (int i = 0; i < legacy.effects.Count; i++)
            {
                AttackEffect e = legacy.effects[i];
                if (e == null)
                    continue;
                StatEffect s = StatEffect.FromLegacy(e);
                if (e.enemyEffect)
                {
                    enemyEffects.Add(s);
                    if (legacy.hasMaxHitPerCollider && e.maxHitTimes > 0)
                        maxTargets = maxTargets == 0 ? e.maxHitTimes : Mathf.Min(maxTargets, e.maxHitTimes);
                }
                else
                {
                    s.onlyAffects = TargetFilter.Self | TargetFilter.Allies;
                    buffEffects.Add(s);
                }
            }
        }
        if (!legacy.multiAreaEffect)
            maxTargets = 1;

        // ---- self-only ability
        if (legacy.singleTargetSelfTarget)
        {
            t.mode = AbilityTargetingMode.Self;
            t.aimLock = AbilityAimLock.FollowCaster;
            var all = new List<AbilityEffect>(buffEffects);
            for (int i = 0; i < enemyEffects.Count; i++)
            {
                enemyEffects[i].onlyAffects = TargetFilter.Self;
                all.Add(enemyEffects[i]);
            }
            def.actions.Add(new DirectEffectAction
            {
                applyTo = DirectEffectAction.Recipient.Caster,
                delay = launch,
                hit = new HitSettings { filter = TargetFilter.Self, effects = all, blockedByObstacles = false },
                vfx = particle,
            });
            def.tags = AbilityTag.Buff;
            FinishAbsorption(def, forMob);
            return def;
        }

        // ---- projectile
        if (legacy.shouldLaunch)
        {
            float speed = Mathf.Max(0.5f, legacy.speed);
            float dist = speed * Mathf.Max(0.1f, legacy.lifeSpan);
            t.mode = AbilityTargetingMode.Direction;
            t.aimLock = AbilityAimLock.LockAtCastStart;
            t.range = forMob ? dist * 0.9f : (playerRange > 0f ? playerRange : dist);
            t.leadTarget = forMob ? 0.5f : 0f;
            var proj = new ProjectileAction
            {
                prefab = particle,
                speed = speed,
                maxDistance = dist,
                radius = primary != null ? Mathf.Clamp(CastRadius(primary), 0.05f, 3f) : 0.3f,
                aimAtTargetHeight = !legacy.isGroundFixedPosition,
                count = Mathf.Max(1, legacy.numberOfTargets),
                pattern = ProjectilePattern.Spread,
                spreadAngle = legacy.numberOfTargets > 1 ? 25f : 0f,
                explosionRadius = legacy.multiAreaEffect && primary != null ? CastRadius(primary) : 0f,
                explodeAtEnd = true,
                hit = new HitSettings { filter = TargetFilter.Enemies, effects = enemyEffects, maxTargets = maxTargets },
            };
            def.actions.Add(proj);
            AddBuffs(def, buffEffects, legacy, launch, particle);
            def.tags = AbilityTag.Ranged | AbilityTag.Projectile | (legacy.multiAreaEffect ? AbilityTag.Area : AbilityTag.None);
            def.ai.requireTargetInArea = false;
            FinishAbsorption(def, forMob);
            return def;
        }

        // ---- area abilities
        ActionAnchor anchor;
        if (forMob)
        {
            // Legacy mob abilities land on the player's position.
            t.mode = AbilityTargetingMode.Unit;
            t.range = 12f;
            if (legacy.isPermanentTarget)
            {
                anchor = ActionAnchor.TargetUnit;
                t.aimLock = AbilityAimLock.TrackUntilRelease;
            }
            else if (legacy.shouldMarkAtCast)
            {
                anchor = ActionAnchor.AimPoint;
                t.aimLock = AbilityAimLock.LockAtCastStart;
            }
            else
            {
                anchor = ActionAnchor.AimPoint;
                t.aimLock = AbilityAimLock.TrackUntilRelease;
            }
        }
        else if (legacy.isAbilityTargetSpawnDecidedUponMouseClick)
        {
            t.mode = AbilityTargetingMode.Point;
            t.range = playerRange > 0f ? playerRange : 15f;
            t.aimLock = AbilityAimLock.LockAtCastStart;
            anchor = ActionAnchor.AimPoint;
        }
        else
        {
            // Legacy player abilities happen around the player.
            t.mode = AbilityTargetingMode.Self;
            t.range = playerRange > 0f ? playerRange : 0f;
            if (legacy.shouldMarkAtCast && !legacy.isPermanentTarget && !legacy.isFixedPosition)
            {
                anchor = ActionAnchor.AimPoint;
                t.aimLock = AbilityAimLock.LockAtCastStart;
            }
            else
            {
                anchor = ActionAnchor.Caster;
                t.aimLock = AbilityAimLock.FollowCaster;
            }
        }

        int areas = Mathf.Max(1, Mathf.Min(casts.Count, legacy.numberOfTargets));
        for (int i = 0; i < areas; i++)
        {
            AttackCast c = i < casts.Count ? casts[i] : primary;
            HitShape shape = c != null ? ToShape(c) : HitShape.SphereShape(2.5f);
            def.actions.Add(new AreaHitAction
            {
                anchor = anchor,
                delay = launch,
                shape = shape,
                hit = new HitSettings { filter = TargetFilter.Enemies, effects = i == 0 ? enemyEffects : CloneEffects(enemyEffects), maxTargets = maxTargets, blockedByObstacles = false },
                vfx = i == 0 ? particle : null,
                scaleVfxWithArea = legacy.particleShouldChangeSize,
            });
        }
        AddBuffs(def, buffEffects, legacy, launch, null);
        def.tags = AbilityTag.Area;
        def.ai.requireTargetInArea = !forMob;
        FinishAbsorption(def, forMob);
        return def;
    }

    private static void AddBuffs(AbilityDefinition def, List<AbilityEffect> buffs, AbilityEffectSO legacy, float delay, GameObject particle)
    {
        if (buffs.Count == 0)
            return;
        if (legacy.casterReceivesBeneffitsBuffsEvenFromFarAway || legacy.shouldLaunch)
        {
            def.actions.Add(new DirectEffectAction
            {
                applyTo = DirectEffectAction.Recipient.Caster,
                delay = delay,
                hit = new HitSettings { filter = TargetFilter.Self, effects = buffs, blockedByObstacles = false },
            });
            return;
        }
        // Buffs apply to allies inside the first area.
        AreaHitAction area = def.FindAction<AreaHitAction>();
        if (area != null)
        {
            area.hit.filter |= TargetFilter.Self | TargetFilter.Allies;
            area.hit.effects.AddRange(buffs);
        }
    }

    private static void FinishAbsorption(AbilityDefinition def, bool forMob)
    {
        def.absorption.canBeAbsorbed = forMob;
        def.ai.priority = 1f;
    }

    private static List<AbilityEffect> CloneEffects(List<AbilityEffect> list)
    {
        var copy = new List<AbilityEffect>(list.Count);
        for (int i = 0; i < list.Count; i++)
            copy.Add(list[i]?.Clone());
        return copy;
    }

    private static List<AttackCast> CollectCasts(AbilityEffectSO legacy, IList<AttackCast> holderCasts)
    {
        var list = new List<AttackCast>();
        if (holderCasts != null)
        {
            for (int i = 0; i < holderCasts.Count; i++)
                if (holderCasts[i] != null) list.Add(holderCasts[i]);
        }
        if (list.Count == 0 && legacy.effects != null)
        {
            for (int i = 0; i < legacy.effects.Count; i++)
            {
                AttackEffect e = legacy.effects[i];
                if (e?.attackCast == null)
                    continue;
                for (int k = 0; k < e.attackCast.Count; k++)
                    if (e.attackCast[k] != null) list.Add(e.attackCast[k]);
                if (list.Count > 0)
                    break;
            }
        }
        return list;
    }

    private static AbilityPhaseMask BlockMask(AbilityEffectSO legacy)
    {
        AbilityPhaseMask mask = AbilityPhaseMask.None;
        try
        {
            legacy.PopulateStateAvailabilityList();
            legacy.UpdateStateAvailabilityDict();
            Dictionary<AbilityStateMachine.EAbilityState, bool> d = legacy.StateAvailabilityDict;
            if (d.TryGetValue(AbilityStateMachine.EAbilityState.Casting, out bool c) && !c) mask |= AbilityPhaseMask.Casting;
            if (d.TryGetValue(AbilityStateMachine.EAbilityState.Launching, out bool l) && !l) mask |= AbilityPhaseMask.Launching;
            if (d.TryGetValue(AbilityStateMachine.EAbilityState.Active, out bool a) && !a) mask |= AbilityPhaseMask.Active;
        }
        catch (System.Exception)
        {
            // Availability data is optional.
        }
        return mask;
    }

    private static float CastRadius(AttackCast c)
    {
        if (c.castType == CastBase.CastType.Box && c.boxSize != Vector3.zero)
            return Mathf.Max(c.boxSize.x, c.boxSize.z) * 0.5f;
        return Mathf.Max(0.05f, c.castSize);
    }

    /// <summary>The new hit shape equivalent to a legacy attack cast.</summary>
    public static HitShape ToShape(AttackCast c)
    {
        switch (c.castType)
        {
            case CastBase.CastType.Box:
            {
                Vector3 size = c.boxSize != Vector3.zero ? c.boxSize : Vector3.one * Mathf.Max(0.5f, c.castSize);
                return new HitShape
                {
                    type = HitShapeType.Rectangle,
                    width = Mathf.Max(0.05f, size.x),
                    length = Mathf.Max(0.05f, size.z),
                    height = Mathf.Max(0.1f, size.y),
                    baseOffset = c.customOrigin.y - size.y * 0.5f,
                    startAtOrigin = false,
                    offset = new Vector3(c.customOrigin.x, 0f, c.customOrigin.z),
                    yaw = Mathf.DeltaAngle(0f, c.customAngle.y),
                };
            }
            case CastBase.CastType.Capsule:
                return new HitShape
                {
                    type = HitShapeType.Line,
                    length = Mathf.Max(0.1f, c.castSize),
                    width = Mathf.Max(0.1f, c.castSize),
                    startAtOrigin = true,
                    offset = c.customOrigin,
                };
            case CastBase.CastType.Ray:
                return new HitShape
                {
                    type = HitShapeType.Line,
                    length = Mathf.Max(0.1f, c.castSize),
                    width = 0.3f,
                    startAtOrigin = true,
                    offset = c.customOrigin,
                };
            default:
                return new HitShape
                {
                    type = HitShapeType.Sphere,
                    radius = Mathf.Max(0.05f, c.castSize),
                    offset = c.customOrigin,
                };
        }
    }
}
