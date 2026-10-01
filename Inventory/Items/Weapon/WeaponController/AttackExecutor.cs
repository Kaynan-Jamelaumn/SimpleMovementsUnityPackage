using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs weapon attacks: picks the attack for an input (combo branch, combo sequence or the next step of the chain),
/// charges it while the input is held, then plays its timeline - startup, active (hits), recovery - and lets a
/// buffered next attack cancel the recovery from the attack's Cancel Point. Speed, cost and damage follow the weapon,
/// the attack, the combo, the charge, the wielder's traits and <see cref="CombatStats"/>. Hits go through the combat
/// system (<see cref="CombatEntity.ApplyDamage"/>), so defense, resistances, aggro, kill credit and every on-hit
/// reaction see them. Nothing in the weapon assets is ever modified.
/// </summary>
public class AttackExecutor
{
    public enum Phase { Idle, Charging, Startup, Active, Recovery }

    private sealed class Running
    {
        public AttackComponent component;
        public AttackAction action;
        public AttackVariation variation;
        public AttackType input;
        public ComboSystem.ComboChoice choice;
        public GameObject player;
        public TraitManager traits;
        public CombatStats stats;
        public bool enhanced;
        public float speed = 1f;
        public float startup, active, recovery;
        public float phaseTime;
        public float chargeHeld;
        public float chargeRatio;
        public bool fullChargeFired;
        public GameObject chargeVfx;
        public WeaponHitResolver.AttackNumbers numbers;
        public AttackContext ctx;
        public readonly List<AttackActionEffect> classicEffects = new List<AttackActionEffect>();
        public readonly Dictionary<CombatEntity, float> hitTimes = new Dictionary<CombatEntity, float>(ReferenceComparer<CombatEntity>.Instance);
        public readonly HashSet<UnityEngine.Object> hitObjects = new HashSet<UnityEngine.Object>();
        public HitSettings onHit;
        public AbilityModifierSet onHitModifiers;
        public bool harmful;
        // Weapon Blade: the held model, its markers, and last frame's blade (the sweep starts there)
        public Transform bladeModel;
        public readonly List<VolumeRun> volumes = new List<VolumeRun>(2);
        public VolumeRun contact;
        // Impact (ground slam)
        public bool impactDone;
        public bool tipHasPrev;
        public Vector3 tipPrev;
        public readonly HashSet<CombatEntity> impactHits = new HashSet<CombatEntity>(ReferenceComparer<CombatEntity>.Instance);
        // Movement restrictions applied (restored exactly at the end)
        public bool moveApplied;
        public float previousCastMove = 1f;
        public float speedDelta;
    }

    /// <summary>One hit volume during an attack: its markers on the held model and last frame's pose (the sweep starts there).</summary>
    private sealed class VolumeRun
    {
        public WeaponBlade volume;
        public Transform startMark, endMark;
        public bool hasPrev;
        public VolumePose prev;
    }

    private static readonly List<WeaponBlade> volumeBuffer = new List<WeaponBlade>(4);

    private readonly WeaponController controller;
    private Phase phase = Phase.Idle;
    private Running run;

    private static readonly List<CombatEntity> entityBuffer = new List<CombatEntity>(16);
    private static readonly Collider[] colliderBuffer = new Collider[32];
    private static readonly RaycastHit[] rayBuffer = new RaycastHit[16];

    // Dependencies
    private ComboSystem comboSystem;
    private VariationSystem variationSystem;
    private InputBufferSystem inputBufferSystem;
    private AttackAnimationHandler animationHandler;
    private WeaponEffectsManager effectsManager;

    public AttackExecutor(WeaponController controller)
    {
        this.controller = controller;
    }

    public void SetDependencies(ComboSystem comboSystem, VariationSystem variationSystem,
        InputBufferSystem inputBufferSystem, AttackAnimationHandler animationHandler,
        WeaponEffectsManager effectsManager)
    {
        this.comboSystem = comboSystem;
        this.variationSystem = variationSystem;
        this.inputBufferSystem = inputBufferSystem;
        this.animationHandler = animationHandler;
        this.effectsManager = effectsManager;
    }

    // ------------------------------------------------------------------ state
    public Phase CurrentPhase => phase;
    public bool IsAttacking => phase != Phase.Idle;
    public bool IsCharging => phase == Phase.Charging;
    public AttackAction CurrentAttackAction => run?.action;
    public AttackVariation CurrentAttackVariation => run?.variation;
    public IAttackComponent CurrentAttackComponent => run?.component;
    public AttackType? CurrentInput => run != null ? run.input : (AttackType?)null;
    /// <summary>Charge (0-1) of the attack being charged or performed.</summary>
    public float ChargeRatio => run == null ? 0f : phase == Phase.Charging ? run.component.charge.Ratio(run.chargeHeld) : run.chargeRatio;
    /// <summary>Seconds the current charge has been held.</summary>
    public float ChargeHeldTime => run != null && phase == Phase.Charging ? run.chargeHeld : 0f;

    /// <summary>Can a new attack start now (idle, or in the recovery past the cancel point)?</summary>
    public bool CanStartNext
    {
        get
        {
            if (phase == Phase.Idle) return true;
            if (phase != Phase.Recovery || run == null) return false;
            return run.phaseTime >= run.recovery * Mathf.Clamp01(run.component.cancelPoint);
        }
    }

    // ------------------------------------------------------------------ input
    /// <summary>Old entry point: an attack for <paramref name="attackType"/> as a tap (charged attacks start uncharged).</summary>
    public void PerformAttack(GameObject player, AttackType attackType) => BeginInput(player, attackType, false, true);

    /// <summary>
    /// The input of <paramref name="attackType"/> was pressed. Starts the attack (or its charge) now, or buffers it
    /// while the current attack cannot be cancelled yet. Returns true if something started.
    /// </summary>
    public bool BeginInput(GameObject player, AttackType attackType, bool fromBuffer = false, bool alreadyReleased = false)
    {
        WeaponSO weapon = controller.EquippedWeapon;
        if (weapon == null)
            return false;
        if (player == null)
            player = controller.PlayerObject;

        if (phase == Phase.Charging)
            return false; // one attack at a time; the charging input decides
        if (!CanStartNext)
        {
            if (!fromBuffer && controller.EnableInputBuffer)
                inputBufferSystem.BufferInput(attackType, player);
            return false;
        }
        if (!controller.CanActNow())
        {
            controller.LogDebug("Cannot attack now (stunned, dead or casting).");
            return false;
        }

        // Pick the attack.
        ComboSystem.ComboChoice choice = comboSystem.Choose(player, attackType);
        AttackAction action;
        AttackVariation variation = null;
        if (choice.IsValid)
            action = choice.action;
        else
            (action, variation) = variationSystem.GetAttackActionWithVariation(attackType);
        if (action == null)
        {
            controller.LogDebug($"The weapon has no {attackType} attack.");
            return false;
        }

        TraitManager traits = controller.Traits;
        if (!action.CanPerformWithTraits(traits, weapon))
        {
            controller.LogDebug($"{action.DisplayName}: trait requirements not met.");
            return false;
        }

        AttackComponent component = variation != null ? (AttackComponent)variation : action;
        var r = new Running
        {
            component = component,
            action = action,
            variation = variation,
            input = attackType,
            choice = choice,
            player = player,
            traits = traits,
            stats = controller.Stats,
            enhanced = action.HasEnhancementTrait(traits, weapon, out _),
        };

        float cost = StaminaCost(r, weapon);
        if (!controller.HasStamina(cost))
        {
            controller.LogDebug($"Not enough stamina for {component.DisplayName} ({cost:0.#}).");
            controller.RaiseAttackFailed(attackType, "stamina");
            return false;
        }
        if (controller.IsHeldItemBroken)
        {
            controller.LogDebug("The weapon is broken (durability 0).");
            controller.RaiseAttackFailed(attackType, "broken");
            return false;
        }

        // A new attack cancels the recovery of the previous one.
        if (phase == Phase.Recovery)
            End(false);

        ChargeSettings charge = component.charge;
        if (charge != null && charge.enabled)
        {
            if (alreadyReleased)
            {
                // A tap on a chargeable attack.
                if (charge.earlyRelease == ChargeSettings.EarlyRelease.Cancel && charge.minChargeTime > 0f)
                    return false;
                Start(r, 0f);
                return true;
            }
            BeginCharge(r);
            return true;
        }
        Start(r, 0f);
        return true;
    }

    /// <summary>The input of <paramref name="attackType"/> was released: a charging attack is performed.</summary>
    public void ReleaseInput(AttackType attackType)
    {
        inputBufferSystem.BufferRelease(attackType);
        if (phase != Phase.Charging || run == null || run.input != attackType)
            return;
        ReleaseCharge();
    }

    /// <summary>Stops the attack being charged (nothing happens, nothing is spent).</summary>
    public void CancelCharge()
    {
        if (phase != Phase.Charging || run == null)
            return;
        StopChargeVisuals(run);
        RestoreMovement(run);
        animationHandler.StopChargeAnimation();
        phase = Phase.Idle;
        run = null;
    }

    private void BeginCharge(Running r)
    {
        run = r;
        phase = Phase.Charging;
        r.chargeHeld = 0f;
        r.ctx = BuildContext(r);
        ApplyMovement(r, r.component.charge.moveSpeedWhileCharging, false);
        animationHandler.PlayChargeAnimation(r.component.charge.chargeAnimation);
        if (r.component.charge.chargingVfx != null && controller.HandTransform != null)
            r.chargeVfx = UnityEngine.Object.Instantiate(r.component.charge.chargingVfx, controller.HandTransform, false);
        RunBehaviours(r, AttackMoment.ChargeStart);
        controller.LogDebug($"Charging {r.component.DisplayName}");
    }

    private void ReleaseCharge()
    {
        Running r = run;
        ChargeSettings charge = r.component.charge;
        float ratio = charge.Ratio(r.chargeHeld);
        bool early = r.chargeHeld < charge.minChargeTime;
        StopChargeVisuals(r);
        RestoreMovement(r);
        animationHandler.StopChargeAnimation();
        if (early && charge.earlyRelease == ChargeSettings.EarlyRelease.Cancel)
        {
            phase = Phase.Idle;
            run = null;
            controller.LogDebug("Charge released too early: cancelled.");
            return;
        }
        Start(r, early ? 0f : ratio);
    }

    private void StopChargeVisuals(Running r)
    {
        if (r.chargeVfx != null)
            UnityEngine.Object.Destroy(r.chargeVfx);
        r.chargeVfx = null;
    }

    // ------------------------------------------------------------------ attack start
    private float StaminaCost(Running r, WeaponSO weapon)
    {
        float cost = r.component.staminaCost * LegacyStaminaFactor(r.traits);
        cost = weapon.CalculateTraitModifiedStaminaCost(cost);
        if (r.enhanced) cost *= r.action.EnhancedStaminaCostMultiplier;
        if (r.stats != null) cost *= r.stats.AttackStaminaMultiplier(weapon.Category);
        return Mathf.Max(0f, cost);
    }

    private AttackContext BuildContext(Running r)
    {
        var ctx = new AttackContext
        {
            Controller = controller,
            Weapon = controller.EquippedWeapon,
            Attack = r.component,
            Input = r.input,
            Attacker = controller.Entity,
            AttackerObject = r.player != null ? r.player : controller.PlayerObject,
            Hand = controller.HandTransform,
        };
        ctx.DealWeaponDamage = (target, fraction) => DealWeaponDamage(r, target, fraction, target != null ? target.Center : Vector3.zero, false);
        return ctx;
    }

    private void Start(Running r, float chargeRatio)
    {
        WeaponSO weapon = controller.EquippedWeapon;
        run = r;
        r.chargeRatio = chargeRatio;
        if (r.ctx == null)
            r.ctx = BuildContext(r);

        // Cost and wear
        controller.ConsumeStamina(StaminaCost(r, weapon));
        controller.ConsumeDurability();

        // Combo and chain bookkeeping
        comboSystem.RegisterAttack(r.player, r.input, r.choice);
        if (!r.choice.IsValid)
            variationSystem.UpdateVariationState(r.input, r.action);

        // Numbers
        AttackComponent c = r.component;
        ChargeSettings charge = c.charge;
        bool charged = charge != null && charge.enabled;
        float damage = c.damageMultiplier * r.choice.damageMultiplier * comboSystem.GetCurrentComboDamageMultiplier();
        if (charged) damage *= charge.DamageMultiplier(chargeRatio);
        if (r.enhanced) damage *= r.action.EnhancedDamageMultiplier;
        r.numbers = new WeaponHitResolver.AttackNumbers
        {
            damageMultiplier = damage,
            critChanceBonus = c.criticalChanceBonus + r.choice.critChanceBonus + (r.stats != null ? r.stats.CritChanceBonus(weapon.Category) : 0f),
            critDamageBonus = r.stats != null ? r.stats.CritDamageBonus(weapon.Category) : 0f,
            element = c.elementOverride != ElementType.None ? c.elementOverride : weapon.ElementType,
            damageType = c.damageType,
        };
        r.ctx.ChargeRatio = chargeRatio;
        r.ctx.DamageMultiplier = damage;
        r.ctx.AreaMultiplier = charged ? charge.AreaMultiplier(chargeRatio) : 1f;

        // Speed and timeline
        float speed = Mathf.Max(0.05f, c.animationSpeed) * weapon.AttackSpeedMultiplier * LegacySpeedMultiplier(r, weapon);
        if (r.stats != null) speed *= r.stats.AttackSpeedMultiplier(weapon.Category);
        if (r.enhanced) speed *= Mathf.Max(0.05f, r.action.EnhancedAnimationSpeedMultiplier);
        r.speed = Mathf.Max(0.05f, speed);
        r.startup = c.StartupFrames / r.speed;
        r.active = c.ActiveFrames / r.speed;
        r.recovery = c.RecoveryFrames / r.speed;
        r.phaseTime = 0f;

        // Classic effects of this attack (the attack's, the enhancement's, the weapon traits' specials)
        r.classicEffects.Clear();
        r.classicEffects.AddRange(c.Effects);
        if (r.enhanced) r.classicEffects.AddRange(r.action.EnhancementEffects);
        weapon.CollectSpecialTraitEffects(r.classicEffects);

        // Harmful attacks (damage, knockback, harmful effects) are the ones friendly fire can turn on friends.
        r.harmful = (c.DealsWeaponDamage && weapon.MaxDamage > 0f) || c.EffectsDealDamage() || c.knockbackMultiplier * weapon.KnockBack > 0.01f ||
                    HasHarmful(c.onHitEffects);
        // Weapon Blade: the held model and the hit volumes this attack uses (and the one that touches the ground)
        r.bladeModel = controller.HeldModel;
        r.volumes.Clear();
        r.contact = null;
        weapon.ActiveVolumes(c, volumeBuffer);
        foreach (WeaponBlade v in volumeBuffer)
            r.volumes.Add(MakeRun(r, v));
        if (c.impact != null && c.impact.enabled)
        {
            WeaponBlade named = weapon.FindVolume(c.impact.contactVolume);
            r.contact = named != null ? (r.volumes.Find(x => x.volume == named) ?? MakeRun(r, named))
                : r.volumes.Count > 0 ? r.volumes[0] : MakeRun(r, weapon.Blade);
        }

        if (c.onHitEffects != null && c.onHitEffects.Count > 0)
        {
            r.onHit = new HitSettings { filter = TargetFilter.All, blockedByObstacles = false, effects = c.onHitEffects, rules = c.targetRules };
            r.onHitModifiers = new AbilityModifierSet { label = "Weapon attack", damageMultiplier = damage, areaMultiplier = r.ctx.AreaMultiplier };
        }

        phase = Phase.Startup;
        float total = r.startup + r.active + r.recovery;
        animationHandler.TriggerAttackAnimation(c, r.input, total, r.speed);
        effectsManager.StartTrail(c);
        ApplyMovement(r, c.LockMovement ? 0f : c.MovementSpeedMultiplier, true);
        ApplyForwardMovement(r);
        if (c.hitDetection != HitDetectionMode.None && controller.Entity != null)
            CombatEvents.RaiseMeleeSwing(controller.Entity);

        controller.LogDebug($"Attack {c.DisplayName} ({r.input}) speed x{r.speed:0.##}, damage x{damage:0.##}{(charged ? $", charge {chargeRatio:P0}" : "")}");
        RunBehaviours(r, AttackMoment.Start);
        controller.RaiseAttackStarted(r.input, c);

        if (r.startup <= 0f)
            EnterActive(r);
    }

    /// <summary>Old string trait effects of the wielder that change stamina costs ("staminacost" consumption rates).</summary>
    private static float LegacyStaminaFactor(TraitManager traits)
    {
        float m = 1f;
        if (traits == null)
            return m;
        IReadOnlyList<Trait> list = traits.Traits;
        for (int i = 0; i < list.Count; i++)
        {
            Trait t = list[i];
            if (t == null || t.effects == null) continue;
            foreach (TraitEffect e in t.effects)
                if (e != null && e.effectType == TraitEffectType.ConsumptionRate && string.Equals(e.targetStat, "staminacost", StringComparison.OrdinalIgnoreCase))
                    m *= e.value;
        }
        return Mathf.Max(0f, m);
    }

    /// <summary>Old string trait effects that change attack speed ("attackspeed"/"speed" multipliers of the weapon and the wielder).</summary>
    private static float LegacySpeedMultiplier(Running r, WeaponSO weapon)
    {
        float m = weapon.CalculateTraitModifiedSpeed(1f);
        if (r.traits != null)
        {
            IReadOnlyList<Trait> list = r.traits.Traits;
            for (int i = 0; i < list.Count; i++)
            {
                Trait t = list[i];
                if (t == null || t.effects == null) continue;
                foreach (TraitEffect e in t.effects)
                    if (e != null && e.effectType == TraitEffectType.StatMultiplier && string.Equals(e.targetStat, "attackspeed", StringComparison.OrdinalIgnoreCase))
                        m *= e.value;
            }
        }
        return Mathf.Max(0.05f, m);
    }

    // ------------------------------------------------------------------ tick
    public void Tick(float dt)
    {
        if (run == null || phase == Phase.Idle)
            return;
        if (controller.EquippedWeapon == null)
        {
            Cancel();
            return;
        }
        Running r = run;
        switch (phase)
        {
            case Phase.Charging:
                TickCharge(r, dt);
                break;
            case Phase.Startup:
                r.phaseTime += dt;
                if (r.phaseTime >= r.startup)
                    EnterActive(r);
                break;
            case Phase.Active:
                r.phaseTime += dt;
                DetectHits(r);
                if (run == r && phase == Phase.Active)
                    CheckImpact(r, false);
                if (run == r && phase == Phase.Active && r.phaseTime >= r.active)
                {
                    CheckImpact(r, true);
                    if (run != r) break;
                    RunBehaviours(r, AttackMoment.ActiveEnd);
                    phase = Phase.Recovery;
                    r.phaseTime = 0f;
                }
                break;
            case Phase.Recovery:
                r.phaseTime += dt;
                if (r.phaseTime >= r.recovery)
                    End(false);
                break;
        }
    }

    private void TickCharge(Running r, float dt)
    {
        ChargeSettings charge = r.component.charge;
        r.chargeHeld += dt;
        if (charge.staminaPerSecond > 0f)
        {
            float drain = charge.staminaPerSecond * dt;
            if (!controller.HasStamina(drain))
            {
                ReleaseCharge(); // out of stamina: the attack goes off with what was charged
                return;
            }
            controller.ConsumeStamina(drain);
        }
        if (!r.fullChargeFired && r.chargeHeld >= charge.maxChargeTime)
        {
            r.fullChargeFired = true;
            if (charge.fullChargeVfx != null)
                AbilityPool.PlayVfx(charge.fullChargeVfx, controller.HandTransform != null ? controller.HandTransform.position : r.ctx.Origin + Vector3.up, Quaternion.identity, 0f, 1f, controller.HandTransform);
            controller.PlaySound(charge.fullChargeSound);
            r.ctx.ChargeRatio = 1f;
            RunBehaviours(r, AttackMoment.FullCharge);
            if (charge.autoReleaseAtFull)
                ReleaseCharge();
        }
    }

    private void EnterActive(Running r)
    {
        phase = Phase.Active;
        r.phaseTime = 0f;
        RunBehaviours(r, AttackMoment.ActiveStart);
        DetectHits(r); // a zero-length active phase still hits once
        if (run == r && phase == Phase.Active && r.component.impact != null && r.component.impact.enabled &&
            r.component.impact.trigger == ImpactSettings.Trigger.ActiveStart)
            DoImpact(r, GroundBelow(r, WeaponTip(r)), Vector3.up);
        if (run == r && r.active <= 0f && phase == Phase.Active)
        {
            CheckImpact(r, true);
            RunBehaviours(r, AttackMoment.ActiveEnd);
            phase = Phase.Recovery;
            r.phaseTime = 0f;
        }
    }

    // ------------------------------------------------------------------ hits
    private HitDetectionMode ResolveDetection(AttackComponent c, WeaponSO weapon)
    {
        if (c.hitDetection != HitDetectionMode.Auto)
            return c.hitDetection;
        return weapon.HasUsableAttackCast ? HitDetectionMode.WeaponCast : HitDetectionMode.HitShape;
    }

    private void DetectHits(Running r)
    {
        WeaponSO weapon = controller.EquippedWeapon;
        AttackComponent c = r.component;
        switch (ResolveDetection(c, weapon))
        {
            case HitDetectionMode.HitShape:
            {
                if (c.hitShape == null) return;
                Vector3 origin = r.ctx.Origin;
                Quaternion rot = Quaternion.LookRotation(r.ctx.Forward, Vector3.up);
                ResolvedShape shape = ResolvedShape.Resolve(c.hitShape, origin, rot, r.ctx.AreaMultiplier);
                CombatQuery.Overlap(shape, entityBuffer);
                if (c.maxTargets > 0 && entityBuffer.Count > 1)
                    CombatQuery.SortByDistance(entityBuffer, origin);
                for (int i = 0; i < entityBuffer.Count && run == r; i++)
                    TryHit(r, entityBuffer[i], entityBuffer[i].Center);
                entityBuffer.Clear();
                if (run == r)
                    HitObjectsInShape(r, shape, c.hitShape.Reach(r.ctx.AreaMultiplier));
                break;
            }
            case HitDetectionMode.WeaponBlade:
                DetectBlade(r, weapon);
                break;
            case HitDetectionMode.WeaponCast:
            {
                if (weapon.attackCast == null) return;
                Transform from = controller.HandTransform != null ? controller.HandTransform : controller.transform;
                Collider[] hits = weapon.attackCast.DetectObjects(from);
                for (int i = 0; i < hits.Length && run == r; i++)
                {
                    Collider col = hits[i];
                    if (col == null || IsOwnCollider(col)) continue;
                    CombatEntity e = CombatEntity.Resolve(col);
                    Vector3 point = col.bounds.center;
                    if (e != null) TryHit(r, e, point);
                    else TryHitObject(r, col, point);
                }
                break;
            }
        }
    }

    private bool IsOwnCollider(Collider col)
    {
        Transform root = controller.PlayerObject != null ? controller.PlayerObject.transform : controller.transform;
        return col.transform == root || col.transform.IsChildOf(root);
    }

    private static bool HasHarmful(List<AbilityEffect> effects)
    {
        if (effects == null) return false;
        foreach (AbilityEffect e in effects)
            if (e != null && e.IsHarmful && e.recipient == EffectRecipient.HitTarget) return true;
        return false;
    }

    // ------------------------------------------------------------------ weapon blade (hit volumes)
    private VolumeRun MakeRun(Running r, WeaponBlade v) => new VolumeRun
    {
        volume = v,
        startMark = WeaponBlade.FindMarker(r.bladeModel, v.startMarker),
        endMark = WeaponBlade.FindMarker(r.bladeModel, v.endMarker),
    };

    /// <summary>Where a volume is this frame (false when there is no hand / model).</summary>
    private bool VolumeNow(Running r, VolumeRun vr, out VolumePose pose)
    {
        Transform model = r.bladeModel != null ? r.bladeModel : controller.HandTransform;
        if (model == null || vr == null || vr.volume == null)
        {
            pose = default;
            return false;
        }
        pose = vr.volume.GetPose(model, vr.startMark, vr.endMark);
        return true;
    }

    /// <summary>
    /// Weapon Blade: hits what each active volume of the weapon touches, sweeping it from last frame's pose to this
    /// frame's (several sub-steps), so a fast swing still hits everything it passes through.
    /// </summary>
    private void DetectBlade(Running r, WeaponSO weapon)
    {
        float scale = r.ctx.AreaMultiplier;
        IReadOnlyList<CombatEntity> all = CombatEntity.All;
        for (int v = 0; v < r.volumes.Count && run == r; v++)
        {
            VolumeRun vr = r.volumes[v];
            if (!VolumeNow(r, vr, out VolumePose now))
                continue;
            WeaponBlade vol = vr.volume;
            VolumePose before = vr.hasPrev ? vr.prev : now;
            float size = vol.shape == WeaponBlade.VolumeShape.Box ? vol.HalfExtents(scale).magnitude : vol.radius * scale;
            float travel = Mathf.Max((now.a - before.a).magnitude, (now.b - before.b).magnitude);
            int steps = Mathf.Clamp(Mathf.CeilToInt(travel / Mathf.Max(0.05f, size * 0.75f)), 1, 16);

            for (int s = vr.hasPrev ? 1 : 0; s <= steps && run == r; s++)
            {
                VolumePose p = VolumePose.Lerp(before, now, s / (float)steps);
                Vector3 mid = (p.a + p.b) * 0.5f;
                float half = (p.b - p.a).magnitude * 0.5f + size;

                for (int i = 0; i < all.Count && run == r; i++)
                {
                    CombatEntity e = all[i];
                    if (e == null || !e.IsAlive) continue;
                    float reach = half + e.Radius + e.Height;
                    if ((e.Center - mid).sqrMagnitude > reach * reach) continue;
                    Vector3 bottom = e.BasePosition + Vector3.up * e.Radius;
                    Vector3 top = e.BasePosition + Vector3.up * Mathf.Max(e.Radius, e.Height - e.Radius);
                    if (vol.Touches(p, scale, bottom, top, e.Radius, out Vector3 point))
                        TryHit(r, e, point);
                }

                // Props and tools (trees, rocks) the volume passes through
                int n = vol.Overlap(p, scale, colliderBuffer);
                for (int i = 0; i < n && run == r; i++)
                {
                    Collider col = colliderBuffer[i];
                    if (col == null || IsOwnCollider(col) || CombatEntity.Resolve(col) != null) continue;
                    TryHitObject(r, col, col.ClosestPoint(mid));
                }
            }
            vr.prev = now;
            vr.hasPrev = true;
        }
    }

    // ------------------------------------------------------------------ impact (ground slam)
    /// <summary>
    /// The point that strikes the ground: the lowest point of the Impact's contact volume (e.g. the hammer's head),
    /// else in front of the attacker.
    /// </summary>
    private Vector3 WeaponTip(Running r)
    {
        if (r.contact != null && VolumeNow(r, r.contact, out VolumePose pose))
            return r.contact.volume.LowestPoint(pose, r.ctx.AreaMultiplier);
        return r.ctx.Origin + r.ctx.Forward * 1f + Vector3.up;
    }

    private int GroundMask(ImpactSettings imp) => imp.groundLayers.value != 0 ? imp.groundLayers.value : CombatSettings.Instance.groundLayers.value;

    /// <summary>The ground right below <paramref name="p"/> (or <paramref name="p"/> itself when there is none).</summary>
    private Vector3 GroundBelow(Running r, Vector3 p)
    {
        ImpactSettings imp = r.component.impact;
        int mask = imp != null ? GroundMask(imp) : CombatSettings.Instance.groundLayers.value;
        int n = Physics.RaycastNonAlloc(p + Vector3.up * 0.5f, Vector3.down, rayBuffer, 4f, mask, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 point = new Vector3(p.x, r.ctx.Origin.y, p.z);
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = rayBuffer[i];
            if (h.collider == null || IsOwnCollider(h.collider) || CombatEntity.Resolve(h.collider) != null) continue;
            if (h.distance < best) { best = h.distance; point = h.point; }
        }
        return point;
    }

    /// <summary>Checks whether the weapon reached the ground this frame; <paramref name="activeEnding"/>: the active phase is ending.</summary>
    private void CheckImpact(Running r, bool activeEnding)
    {
        ImpactSettings imp = r.component.impact;
        if (imp == null || !imp.enabled || r.impactDone)
            return;
        Vector3 tip = WeaponTip(r);
        bool contactMode = imp.trigger == ImpactSettings.Trigger.GroundContact || imp.trigger == ImpactSettings.Trigger.GroundContactOrActiveEnd;
        if (contactMode)
        {
            Vector3 from = r.tipHasPrev ? r.tipPrev : tip + Vector3.up * 0.3f;
            r.tipPrev = tip;
            r.tipHasPrev = true;
            if (GroundContact(r, imp, from, tip, out Vector3 point, out Vector3 normal))
            {
                DoImpact(r, point, normal);
                return;
            }
        }
        if (activeEnding && (imp.trigger == ImpactSettings.Trigger.ActiveEnd || imp.trigger == ImpactSettings.Trigger.GroundContactOrActiveEnd))
            DoImpact(r, GroundBelow(r, tip), Vector3.up);
    }

    /// <summary>Did the tip, moving from <paramref name="from"/> to <paramref name="to"/>, reach the ground?</summary>
    private bool GroundContact(Running r, ImpactSettings imp, Vector3 from, Vector3 to, out Vector3 point, out Vector3 normal)
    {
        point = to;
        normal = Vector3.up;
        int mask = GroundMask(imp);
        Vector3 move = to - from;
        float dist = move.magnitude;
        int n = dist > 1e-4f
            ? Physics.SphereCastNonAlloc(from, imp.contactTolerance, move / dist, rayBuffer, dist, mask, QueryTriggerInteraction.Ignore)
            : 0;
        float best = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = rayBuffer[i];
            if (h.collider == null || IsOwnCollider(h.collider) || CombatEntity.Resolve(h.collider) != null) continue;
            if (h.distance <= 0f && h.point == Vector3.zero) continue; // started inside: handled by the check below
            if (h.distance < best) { best = h.distance; point = h.point; normal = h.normal; found = true; }
        }
        if (found)
            return true;
        // Already touching (resting on or inside the ground)
        n = Physics.RaycastNonAlloc(to + Vector3.up * 0.25f, Vector3.down, rayBuffer, 0.25f + imp.contactTolerance, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = rayBuffer[i];
            if (h.collider == null || IsOwnCollider(h.collider) || CombatEntity.Resolve(h.collider) != null) continue;
            point = h.point;
            normal = h.normal;
            return true;
        }
        return false;
    }

    /// <summary>The impact: its area around <paramref name="point"/> hits every valid character inside it.</summary>
    private void DoImpact(Running r, Vector3 point, Vector3 normal)
    {
        ImpactSettings imp = r.component.impact;
        if (imp == null || r.impactDone || imp.area == null)
            return;
        r.impactDone = true;
        CombatEntity attacker = controller.Entity;
        float scale = r.ctx.AreaMultiplier;
        Quaternion rot = Quaternion.LookRotation(r.ctx.Forward, Vector3.up);
        ResolvedShape shape = ResolvedShape.Resolve(imp.area, point, rot, scale);
        float reach = Mathf.Max(0.1f, imp.area.Reach(scale));

        r.ctx.ImpactPoint = point;
        r.ctx.ImpactNormal = normal;
        if (imp.vfx != null)
            AbilityPool.PlayVfx(imp.vfx, point, Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.LookRotation(r.ctx.Forward), 0f, scale);
        if (imp.sound != null)
            AbilityPool.PlaySound(imp.sound, point, 1f);

        CombatQuery.Overlap(shape, entityBuffer);
        if (imp.maxTargets > 0 && entityBuffer.Count > 1)
            CombatQuery.SortByDistance(entityBuffer, point);
        var hitSettings = imp.effects != null && imp.effects.Count > 0
            ? new HitSettings { filter = TargetFilter.All, blockedByObstacles = false, effects = imp.effects, rules = imp.rules }
            : null;
        AbilityCastInstance cast = null;
        int count = 0;
        for (int i = 0; i < entityBuffer.Count && run == r; i++)
        {
            CombatEntity e = entityBuffer[i];
            if (e == null || !e.IsAlive) continue;
            CombatRelation rel = attacker != null ? CombatRelations.Get(attacker, e) : CombatRelation.Neutral;
            if (attacker == null && r.ctx.AttackerObject != null && e.transform.IsChildOf(r.ctx.AttackerObject.transform)) continue;
            if (!CombatTargeting.CanHit(imp.filter, imp.rules, rel, e, true)) continue;
            if (!imp.alsoHitsSwingTargets && r.hitTimes.ContainsKey(e)) continue;
            if (!r.impactHits.Add(e)) continue;
            if (imp.maxTargets > 0 && ++count > imp.maxTargets) break;

            float d = new Vector2(e.Position.x - point.x, e.Position.z - point.z).magnitude;
            float strength = Mathf.Lerp(1f, imp.edgeMultiplier, Mathf.Clamp01(d / reach));
            if (imp.damageMultiplier > 0f && (attacker == null || CombatTargeting.CanHarm(rel, imp.rules)))
                DealWeaponDamage(r, e, imp.damageMultiplier * strength, e.Center, false);
            if (hitSettings != null && e.IsAlive)
            {
                if (cast == null)
                {
                    cast = new AbilityCastInstance(null, attacker, null, null, r.onHitModifiers);
                    if (attacker == null)
                        cast.SetOrigin(point, point + Vector3.up, rot);
                }
                cast.ApplyHit(hitSettings, e, point, e.Position - point, strength);
            }
            r.ctx.LastHitTarget = e;
            r.ctx.LastHitPoint = e.Center;
        }
        entityBuffer.Clear();
        if (run == r)
            HitObjectsInShape(r, shape, reach);
        controller.LogDebug($"Impact of {r.component.DisplayName} at {point} ({r.impactHits.Count} hit)");
        if (run == r)
            RunBehaviours(r, AttackMoment.Impact);
    }

    /// <summary>Tools and props (trees, rocks, crates) inside the attack's shape.</summary>
    private void HitObjectsInShape(Running r, in ResolvedShape shape, float reach)
    {
        int n = Physics.OverlapSphereNonAlloc(shape.origin + Vector3.up, Mathf.Max(0.5f, reach), colliderBuffer, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            Collider col = colliderBuffer[i];
            if (col == null || IsOwnCollider(col) || CombatEntity.Resolve(col) != null)
                continue;
            Vector3 p = col.bounds.ClosestPoint(shape.origin + Vector3.up);
            Vector3 flat = new Vector3(p.x, shape.origin.y, p.z);
            if (!shape.ContainsPoint(flat + Vector3.up * 0.01f) && !shape.ContainsPoint(p))
                continue;
            TryHitObject(r, col, p);
        }
    }

    private void TryHitObject(Running r, Collider col, Vector3 point)
    {
        IWeaponHittable hittable = col.GetComponentInParent<IWeaponHittable>();
        if (hittable != null)
        {
            var key = hittable as UnityEngine.Object;
            if (key == null || !r.hitObjects.Add(key))
                return;
            WeaponSO weapon = controller.EquippedWeapon;
            var info = new WeaponHitInfo
            {
                weapon = weapon,
                attacker = controller.Entity,
                attackerObject = r.ctx.AttackerObject,
                point = point,
                direction = r.ctx.Forward,
                damage = (weapon.MinDamage + weapon.MaxDamage) * 0.5f * r.numbers.damageMultiplier,
            };
            if (hittable.OnWeaponHit(info))
                effectsManager.PlayHitEffects(r.component, point);
            return;
        }

        // Older targets without a combat entity: only the classic effects (as before).
        BaseStatusController status = col.GetComponentInParent<BaseStatusController>();
        if (status != null && r.hitObjects.Add(status))
        {
            WeaponHitResolver.ApplyClassicEffects(r.classicEffects, controller.EquippedWeapon, controller.Entity, r.ctx.AttackerObject,
                null, status.gameObject, r.numbers.damageMultiplier, r.numbers.damageType, r.numbers.element, point, r.ctx.Forward);
            effectsManager.PlayHitEffects(r.component, point);
        }
    }

    private void TryHit(Running r, CombatEntity target, Vector3 point)
    {
        CombatEntity attacker = controller.Entity;
        if (target == null || target == attacker || !target.IsAlive)
            return;
        if (attacker != null)
        {
            // Relation → filter (+ friendly fire for harmful attacks) → target rules (kinds, factions)
            if (!CombatTargeting.CanHit(r.component.hitFilter, r.component.targetRules, CombatRelations.Get(attacker, target), target, r.harmful))
                return;
        }
        else if (r.ctx.AttackerObject != null && (target.transform == r.ctx.AttackerObject.transform || target.transform.IsChildOf(r.ctx.AttackerObject.transform)))
        {
            return;
        }

        float now = Time.time;
        if (r.hitTimes.TryGetValue(target, out float last))
        {
            if (r.component.rehitInterval <= 0f || now - last < r.component.rehitInterval)
                return;
        }
        else if (r.component.maxTargets > 0 && r.hitTimes.Count >= r.component.maxTargets)
        {
            return;
        }
        r.hitTimes[target] = now;
        ProcessHit(r, target, point);
    }

    private void ProcessHit(Running r, CombatEntity target, Vector3 point)
    {
        WeaponSO weapon = controller.EquippedWeapon;
        CombatEntity attacker = controller.Entity;
        Vector3 dir = CombatQuery.FlatDirection(r.ctx.Origin, target.Position, r.ctx.Forward);
        r.ctx.HitCount++;
        // A party member / ally the filter selected (a support attack) only gets the helpful parts, unless friendly fire.
        bool canHarm = attacker == null || CombatTargeting.CanHarm(CombatRelations.Get(attacker, target), r.component.targetRules);

        float dealt = 0f;
        if (canHarm && r.component.DealsWeaponDamage && weapon.MaxDamage > 0f)
            dealt = DealWeaponDamage(r, target, 1f, point, true);

        // Knockback
        float knock = weapon.KnockBack * r.component.knockbackMultiplier * (r.stats != null ? r.stats.KnockbackMultiplier(weapon.Category) : 1f);
        if (canHarm && knock > 0.01f && target.IsAlive)
            target.ApplyDisplacement(dir * knock, Mathf.Clamp(0.1f + knock * 0.04f, 0.1f, 0.4f), 0f, false, attacker);

        // Classic effects (older weapons deal their damage here) and weapon trait debuffs
        if (canHarm)
        {
            WeaponHitResolver.ApplyClassicEffects(r.classicEffects, weapon, attacker, r.ctx.AttackerObject, target, target.gameObject,
                r.numbers.damageMultiplier, r.numbers.damageType, r.numbers.element, point, dir);
            if (weapon.ApplyTraitsToEnemy && target.IsAlive)
                weapon.ApplyEffectsToTarget(target.gameObject, r.ctx.AttackerObject, (IAttackComponent)null);
        }

        // On-hit status effects (burn, stun, slow, life steal...)
        if (r.onHit != null && target.IsAlive)
        {
            var cast = new AbilityCastInstance(null, attacker, null, null, r.onHitModifiers);
            if (attacker == null)
                cast.SetOrigin(r.ctx.Origin, r.ctx.Origin + Vector3.up, Quaternion.LookRotation(r.ctx.Forward));
            cast.ApplyHit(r.onHit, target, r.ctx.Origin, dir);
        }

        // Old string trait reactions (lifesteal, aoe, on_hit_*)
        if (canHarm)
            WeaponHitResolver.ApplyLegacyTraitReactions(weapon, r.traits, attacker, r.ctx.AttackerObject, target, target.gameObject, dealt, r.classicEffects);

        // Elements: reactions and visuals
        if (r.numbers.element != ElementType.None)
        {
            ElementalSystem es = ElementalSystem.Instance;
            if (es != null)
            {
                ElementType targetElement = WeaponHitResolver.GetTargetElement(target.gameObject);
                if (targetElement != ElementType.None)
                    es.TriggerElementalReaction(r.numbers.element, targetElement, point, r.ctx.AttackerObject, target.gameObject);
                effectsManager.PlayElementParticles(es.GetElementParticles(r.numbers.element), point);
            }
        }

        effectsManager.PlayHitEffects(r.component, point);
        if (r.component.hitVfx != null)
            AbilityPool.PlayVfx(r.component.hitVfx, point, Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? -dir : Vector3.back), 0f);
        if (r.component.hitSound != null)
            AbilityPool.PlaySound(r.component.hitSound, point, 1f);

        comboSystem.RegisterHit(target.gameObject, dealt, r.numbers.element != ElementType.None);
        r.ctx.LastHitTarget = target;
        r.ctx.LastHitPoint = point;
        RunBehaviours(r, AttackMoment.EachHit);
        controller.RaiseAttackHit(target, dealt);
        controller.LogDebug($"Hit {target.name} for {dealt:0.#}");
    }

    /// <summary>Deals a fraction of this attack's weapon damage to a character. Returns the damage dealt.</summary>
    private float DealWeaponDamage(Running r, CombatEntity target, float fraction, Vector3 point, bool direct)
    {
        WeaponSO weapon = controller.EquippedWeapon;
        if (target == null || !target.IsAlive || weapon == null || fraction <= 0f)
            return 0f;
        CombatEntity attacker = controller.Entity;
        ElementType targetElement = r.numbers.element != ElementType.None ? WeaponHitResolver.GetTargetElement(target.gameObject) : ElementType.None;
        float dmg = WeaponHitResolver.RollDamage(weapon, r.numbers, r.traits, r.stats, targetElement, out bool crit) * fraction;
        if (dmg <= 0f)
            return 0f;
        Vector3 dir = CombatQuery.FlatDirection(r.ctx.Origin, target.Position, r.ctx.Forward);
        return target.ApplyDamage(new DamageInfo
        {
            amount = dmg,
            source = attacker,
            target = target,
            point = point,
            direction = dir,
            isCritical = crit,
            type = r.numbers.damageType,
            element = r.numbers.element,
            weapon = weapon,
        });
    }

    // ------------------------------------------------------------------ movement
    private void ApplyMovement(Running r, float multiplier, bool replace)
    {
        if (replace)
            RestoreMovement(r);
        multiplier = Mathf.Clamp(multiplier, 0f, 1f);
        if (Mathf.Approximately(multiplier, 1f))
            return;
        CombatEntity e = controller.Entity;
        if (e != null)
        {
            r.previousCastMove = e.CastMoveMultiplier;
            e.CastMoveMultiplier = Mathf.Min(r.previousCastMove, multiplier);
        }
        SpeedManager sm = controller.SpeedManager;
        if (sm != null)
        {
            float before = sm.Speed;
            sm.ModifySpeed(before * (multiplier - 1f));
            r.speedDelta = sm.Speed - before;
        }
        r.moveApplied = true;
    }

    private void RestoreMovement(Running r)
    {
        if (r == null || !r.moveApplied)
            return;
        CombatEntity e = controller.Entity;
        if (e != null)
            e.CastMoveMultiplier = r.previousCastMove;
        SpeedManager sm = controller.SpeedManager;
        if (sm != null && !Mathf.Approximately(r.speedDelta, 0f))
            sm.ModifySpeed(-r.speedDelta);
        r.speedDelta = 0f;
        r.moveApplied = false;
    }

    private void ApplyForwardMovement(Running r)
    {
        Vector3 local = r.component.ForwardMovement;
        if (local == Vector3.zero || controller.Entity == null)
            return;
        float duration = Mathf.Max(0.05f, r.startup + r.active);
        Quaternion rot = Quaternion.LookRotation(r.ctx.Forward, Vector3.up);
        Vector3 offset = rot * new Vector3(local.x, 0f, local.z);
        ForcedMovement fm = ForcedMovement.GetOrAdd(controller.Entity);
        if (fm != null && !fm.IsActive)
            fm.Begin(offset, duration, Mathf.Max(0f, local.y), true);
    }

    // ------------------------------------------------------------------ behaviours
    private void RunBehaviours(Running r, AttackMoment moment)
    {
        List<AttackBehaviour> list = r.component.behaviours;
        if (list == null || list.Count == 0)
            return;
        for (int i = 0; i < list.Count; i++)
        {
            AttackBehaviour b = list[i];
            if (b == null || b.when != moment || !b.ShouldRun(r.ctx))
                continue;
            try { b.Execute(r.ctx); }
            catch (Exception e) { Debug.LogException(e, controller); }
        }
    }

    // ------------------------------------------------------------------ end
    /// <summary>Ends the current attack. <paramref name="interrupted"/>: skip its End behaviours and cut the animation.</summary>
    private void End(bool interrupted)
    {
        Running r = run;
        if (r == null)
        {
            phase = Phase.Idle;
            return;
        }
        RestoreMovement(r);
        StopChargeVisuals(r);
        effectsManager.StopTrail();
        if (!interrupted && phase != Phase.Charging)
            RunBehaviours(r, AttackMoment.End);
        if (interrupted)
            animationHandler.ForceEnd();
        phase = Phase.Idle;
        run = null;
        controller.RaiseAttackEnded(r.input, r.component, interrupted);
    }

    /// <summary>Interrupts whatever is running (unequip, stun, disable).</summary>
    public void Cancel()
    {
        if (phase == Phase.Charging)
        {
            CancelCharge();
            return;
        }
        if (phase != Phase.Idle)
            End(true);
    }

    public void EndAttack() => End(false);

    public void ResetAttackState()
    {
        Cancel();
        phase = Phase.Idle;
        run = null;
    }

    /// <summary>Kept for the old API (attacks no longer use coroutines).</summary>
    public void CleanupAllCoroutines() => Cancel();

#if UNITY_EDITOR
    private readonly List<Vector3> gizmoOutline = new List<Vector3>(48);
    private readonly List<Vector3> gizmoInner = new List<Vector3>(48);
#endif

    public void DrawDebugInfo(Transform handTransform)
    {
#if UNITY_EDITOR
        if (run == null || handTransform == null) return;
        string info = $"Attack: {run.component.DisplayName} ({run.input})\n" +
                      $"Phase: {phase} {run.phaseTime:0.00}s\n" +
                      $"Speed x{run.speed:0.##}  Damage x{run.numbers.damageMultiplier:0.##}\n" +
                      $"Charge: {ChargeRatio:P0}  Hits: {run.hitTimes.Count}";
        UnityEditor.Handles.Label(handTransform.position + Vector3.up * 2f, info);
        if (run.volumes.Count > 0 && run.ctx != null && run.component.hitDetection == HitDetectionMode.WeaponBlade)
        {
            // The weapon's hit volumes where they are now
            Gizmos.color = phase == Phase.Active ? new Color(1f, 0.2f, 0.2f, 0.9f) : new Color(1f, 1f, 0.2f, 0.5f);
            foreach (VolumeRun vr in run.volumes)
            {
                if (!VolumeNow(run, vr, out VolumePose pose)) continue;
                float scale = run.ctx.AreaMultiplier;
                switch (vr.volume.shape)
                {
                    case WeaponBlade.VolumeShape.Sphere:
                        Gizmos.DrawWireSphere(pose.a, vr.volume.radius * scale);
                        break;
                    case WeaponBlade.VolumeShape.Box:
                        Matrix4x4 old = Gizmos.matrix;
                        Gizmos.matrix = Matrix4x4.TRS(pose.a, pose.rotation, Vector3.one);
                        Gizmos.DrawWireCube(Vector3.zero, vr.volume.HalfExtents(scale) * 2f);
                        Gizmos.matrix = old;
                        break;
                    default:
                        Gizmos.DrawWireSphere(pose.a, vr.volume.radius * scale);
                        Gizmos.DrawWireSphere(pose.b, vr.volume.radius * scale);
                        Gizmos.DrawLine(pose.a, pose.b);
                        break;
                }
            }
        }
        else if (run.component.hitShape != null && run.ctx != null)
        {
            ResolvedShape s = ResolvedShape.Resolve(run.component.hitShape, run.ctx.Origin, Quaternion.LookRotation(run.ctx.Forward), run.ctx.AreaMultiplier);
            Gizmos.color = phase == Phase.Active ? new Color(1f, 0.2f, 0.2f, 0.8f) : new Color(1f, 1f, 0.2f, 0.4f);
            // The real outline of the area (it used to be a sphere of the shape's reach)
            s.GetOutline(gizmoOutline, gizmoInner, 40, 0.05f);
            for (int i = 0; i < gizmoOutline.Count - 1; i++)
                Gizmos.DrawLine(gizmoOutline[i], gizmoOutline[i + 1]);
            for (int i = 0; i < gizmoInner.Count - 1; i++)
                Gizmos.DrawLine(gizmoInner[i], gizmoInner[i + 1]);
        }
#endif
    }
}
