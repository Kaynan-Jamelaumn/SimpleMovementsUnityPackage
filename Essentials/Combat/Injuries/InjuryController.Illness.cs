using System;
using System.Collections.Generic;
using UnityEngine;

// Illnesses and what brings them: an illness is an injury type of the whole body (Systemic: a cold, sepsis, food
// poisoning) on the Whole Body part. They come from other injuries (complications: a dirty puncture gives tetanus), the
// body's state (exposures: being cold gives a cold), other characters (contagion), food (Consumable ▸ Illness Risks),
// abilities (Injury ▸ Inflict Injury) and scripts (Expose). Also here: immunity after an illness, how sleep and hunger
// change healing, hard landings, and what injuries do to the sight.
public partial class InjuryController
{
    private readonly Dictionary<string, float> immunity = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private readonly List<InjuryPartState> pickScratch = new List<InjuryPartState>(18);
    private InjuryVision vision;
    private float illnessClock;
    private int complicationDepth;
    private PlayerStatusController statusController;
    private float nextStatusSearch;
    private float bodyHealFactor = 1f, bodyWorsenFactor = 1f;
    private bool bodyCold, bodyFreezing, bodyOverheated, bodyStarving, bodyDehydrated, bodyExhausted, bodyAsleep;

    // Falls: the arc the character is in (where it left the ground, how high it got, how far it has dropped since).
    private bool fallHasLast, fallInAir, fallCredit, fallForgiven;
    private float fallLastY, fallBaseY, fallTopY, fallSmoothed, fallPeak, fallLastSpeed, fallingFor, fallStillFor, fallBrakedFor;
    private PlayerMovementModel moveModel;
    private PlayerMovementController moveController;
    private float nextMoveSearch;

    /// <summary>A running jump starts this much faster than a standing one (the Jumping state).</summary>
    private const float RunningJump = 1.75f;
    private const float RiseSpeed = 1.5f, FallSpeed = 1.5f;

    /// <summary>What the character's injuries do to its sight right now (nothing while the system is off).</summary>
    public InjuryVision Vision => Settings.enabled ? vision : default;

    /// <summary>Healing speed from the body's state right now (asleep: faster; starving or dehydrated: slower).</summary>
    public float BodyHealFactor => bodyHealFactor;

    /// <summary>How much sooner injuries get worse from the body's state right now (cold, starving, dehydrated).</summary>
    public float BodyWorsenFactor => bodyWorsenFactor;

    private PlayerStatusController BodyStatus
    {
        get
        {
            // Searched again now and then: the status controller may appear after this component.
            if (statusController == null && Time.unscaledTime >= nextStatusSearch)
            {
                nextStatusSearch = Time.unscaledTime + 2f;
                CombatEntity e = Entity;
                statusController = e != null ? e.Status as PlayerStatusController : null;
                if (statusController == null)
                    statusController = GetComponentInParent<PlayerStatusController>();
                if (statusController == null)
                    statusController = GetComponentInChildren<PlayerStatusController>();
            }
            return statusController;
        }
    }

    private void ResetIllnessState()
    {
        immunity.Clear();
        illnessClock = UnityEngine.Random.value * 2f;
        bodyHealFactor = bodyWorsenFactor = 1f;
        vision = default;
        fallHasLast = false;
        EndArc(0f);
    }

    // ------------------------------------------------------------------ immunity
    /// <summary>Is the character immune to <paramref name="type"/> right now (it healed from it a while ago)?</summary>
    public bool IsImmuneTo(InjuryTypeDefinition type)
    {
        if (type == null || immunity.Count == 0 || string.IsNullOrEmpty(type.id))
            return false;
        if (!immunity.TryGetValue(type.id, out float until))
            return false;
        if (Time.time < until)
            return true;
        immunity.Remove(type.id);
        return false;
    }

    /// <summary>Seconds of immunity to <paramref name="typeId"/> left (0 = none).</summary>
    public float ImmunityLeft(string typeId)
    {
        if (string.IsNullOrEmpty(typeId) || !immunity.TryGetValue(typeId.Trim(), out float until))
            return 0f;
        return Mathf.Max(0f, until - Time.time);
    }

    /// <summary>Makes the character immune to an injury type for a while (0 or less: ends the immunity).</summary>
    public void SetImmunity(string typeId, float seconds)
    {
        if (string.IsNullOrWhiteSpace(typeId) || !RulesHere)
            return;
        if (seconds > 0f)
            immunity[typeId.Trim()] = Time.time + seconds;
        else
            immunity.Remove(typeId.Trim());
    }

    private void GrantImmunity(InjuryTypeDefinition type)
    {
        if (type != null && type.immunitySeconds > 0f && !string.IsNullOrEmpty(type.id))
            immunity[type.id] = Time.time + type.immunitySeconds;
    }

    // ------------------------------------------------------------------ catching something
    /// <summary>
    /// Exposes the character to an injury type (usually an illness): with <paramref name="chance"/> (0-1, changed by the
    /// character's resistances to it) it starts at <paramref name="stage"/>. Immune characters, and those who already
    /// have it at that stage or worse, catch nothing. Food, traps and scripts call it; returns the injury when caught.
    /// </summary>
    public Injury Expose(string typeId, float chance = 1f, int stage = 0, CombatEntity cause = null) =>
        Expose(Settings.FindType(typeId), chance, stage, cause, null);

    /// <summary>
    /// See <see cref="Expose(string,float,int,CombatEntity)"/>. <paramref name="partKinds"/>: for injuries that are not
    /// illnesses, the kinds of parts it may land on (null or empty = the type's own, else any).
    /// </summary>
    public Injury Expose(InjuryTypeDefinition type, float chance = 1f, int stage = 0, CombatEntity cause = null, List<BodyPartKind> partKinds = null)
    {
        if (type == null || chance <= 0f || !RulesHere || !Settings.enabled || !enabled)
            return null;
        CombatEntity e = Entity;
        if (e != null && e.IsDead)
            return null;
        if (IsImmuneTo(type))
            return null;
        InjuryPartState part = type.systemic ? wholeBody : RandomPartFor(type, partKinds);
        if (part == null)
            return null;
        InjuryResistances res = Resistances;
        if (res != null)
            chance *= res.InjuryChanceMultiplier(type, part.Def, DamageForm.None);
        if (chance <= 0f || (chance < 1f && UnityEngine.Random.value >= chance))
            return null;
        return AddInjury(part, type, stage, cause, false);
    }

    /// <summary>
    /// A part an injury of <paramref name="type"/> may be on: of <paramref name="kinds"/>, else of its trigger's part
    /// kinds; not lost, not the whole body.
    /// </summary>
    private InjuryPartState RandomPartFor(InjuryTypeDefinition type, List<BodyPartKind> kinds = null)
    {
        pickScratch.Clear();
        if (kinds == null || kinds.Count == 0)
            kinds = type.trigger != null ? type.trigger.partKinds : null;
        for (int i = 0; i < parts.Count; i++)
        {
            InjuryPartState p = parts[i];
            if (p.Lost || p.IsWholeBody)
                continue;
            if (kinds != null && kinds.Count > 0 && !kinds.Contains(p.Def.kind))
                continue;
            pickScratch.Add(p);
        }
        InjuryPartState picked = pickScratch.Count > 0 ? pickScratch[UnityEngine.Random.Range(0, pickScratch.Count)] : null;
        pickScratch.Clear();
        return picked;
    }

    /// <summary>The other injuries <paramref name="injury"/> may cause now (see <see cref="InjuryComplication"/>).</summary>
    private void RollComplications(Injury injury, ComplicationWhen when, float minutes = 1f)
    {
        List<InjuryComplication> list = injury != null && injury.Type != null ? injury.Type.complications : null;
        if (list == null || list.Count == 0 || complicationDepth >= 3 || !Settings.enabled)
            return;
        complicationDepth++;
        try
        {
            for (int i = 0; i < list.Count; i++)
            {
                InjuryComplication c = list[i];
                if (c == null || c.when != when || c.chance <= 0f || injury.StageIndex < c.fromStage)
                    continue;
                InjuryTypeDefinition type = Settings.FindType(c.injuryType);
                if (type == null || type == injury.Type || IsImmuneTo(type))
                    continue;
                InjuryPartState part = type.systemic ? wholeBody : injury.Part;
                if (part == null || (part.Lost && !type.systemic))
                    continue;
                float chance = when == ComplicationWhen.WhileUntreated ? 1f - Mathf.Pow(1f - Mathf.Clamp01(c.chance), Mathf.Max(0f, minutes)) : c.chance;
                InjuryResistances res = Resistances;
                if (res != null)
                    chance *= res.InjuryChanceMultiplier(type, part.Def, DamageForm.None);
                if (chance <= 0f || (chance < 1f && UnityEngine.Random.value >= chance))
                    continue;
                Injury made = AddInjury(part, type, c.stage, injury.Cause, false);
                if (made != null && !made.Hidden)
                    Notify(injury.Part != null && !injury.Part.IsWholeBody
                        ? $"The {injury.Name.ToLowerInvariant()} on your {injury.Part.Name.ToLowerInvariant()} led to {made.Name.ToLowerInvariant()}."
                        : $"Your {injury.Name.ToLowerInvariant()} led to {made.Name.ToLowerInvariant()}.");
            }
        }
        finally
        {
            complicationDepth--;
        }
    }

    // ------------------------------------------------------------------ the slow check
    /// <summary>
    /// Every Check Seconds (rules side): reads the body's state, rolls the exposures it meets, the complications that run
    /// per minute, and passes catching illnesses to the characters near.
    /// </summary>
    private void TickIllness(float dt, InjurySettings s)
    {
        IllnessRules r = s.illness;
        if (r == null || !r.enabled)
        {
            bodyHealFactor = bodyWorsenFactor = 1f;
            return;
        }
        illnessClock += dt;
        float interval = Mathf.Max(1f, r.checkSeconds);
        if (illnessClock < interval)
            return;
        float minutes = illnessClock / 60f;
        illnessClock = 0f;
        CombatEntity e = Entity;
        if (e == null || e.IsDead)
            return;
        ReadBodyState(r);

        if (r.exposures != null)
        {
            for (int i = 0; i < r.exposures.Count; i++)
            {
                IllnessExposure ex = r.exposures[i];
                if (ex == null || ex.chancePerMinute <= 0f || !IsExposedTo(ex.source))
                    continue;
                InjuryTypeDefinition type = s.FindType(ex.illness);
                if (type == null)
                    continue;
                float chance = 1f - Mathf.Pow(1f - Mathf.Clamp01(ex.chancePerMinute), minutes);
                Injury made = Expose(type, chance, ex.stage, null, ex.partKinds);
                if (made != null && !made.Hidden)
                    Notify(made.IsIllness
                        ? $"You have come down with {made.Name.ToLowerInvariant()}."
                        : $"{made.Name} on your {made.Part.Name.ToLowerInvariant()}.");
            }
        }

        for (int p = 0; p < parts.Count; p++)
        {
            List<Injury> list = parts[p].injuries;
            for (int i = 0; i < list.Count; i++)
            {
                Injury inj = list[i];
                InjuryStage st = inj.Stage;
                if (st == null || inj.Type == null)
                    continue;
                if (!inj.Treated && !inj.WorseningStopped && inj.Type.complications != null && inj.Type.complications.Count > 0)
                    RollComplications(inj, ComplicationWhen.WhileUntreated, minutes);
                if (st.contagionPerMinute > 0f)
                    Spread(inj, st, minutes, r);
            }
        }
    }

    /// <summary>Reads hunger, thirst, rest and body heat (players), and what they do to healing and getting worse.</summary>
    private void ReadBodyState(IllnessRules r)
    {
        bodyCold = bodyFreezing = bodyOverheated = bodyStarving = bodyDehydrated = bodyExhausted = bodyAsleep = false;
        PlayerStatusController st = BodyStatus;
        if (st != null)
        {
            BodyHeatManager heat = st.BodyHeatManager;
            if (heat != null && heat.isActiveAndEnabled)
            {
                BodyHeatManager.TemperatureLevel level = heat.CurrentTemperatureLevel;
                bodyFreezing = level == BodyHeatManager.TemperatureLevel.ExtremelyCold;
                bodyCold = bodyFreezing || level == BodyHeatManager.TemperatureLevel.Cold;
                bodyOverheated = level == BodyHeatManager.TemperatureLevel.ExtremelyHot;
            }
            HungerManager hunger = st.HungerManager;
            if (hunger != null && hunger.isActiveAndEnabled)
                bodyStarving = hunger.IsEmpty;
            ThirstManager thirst = st.ThirstManager;
            if (thirst != null && thirst.isActiveAndEnabled)
                bodyDehydrated = thirst.IsEmpty;
            SleepManager sleep = st.SleepManager;
            if (sleep != null && sleep.isActiveAndEnabled)
            {
                bodyAsleep = sleep.IsSleeping;
                bodyExhausted = sleep.CurrentSleepinessLevel == SleepManager.SleepinessLevel.ExtremelySleepy;
            }
        }
        float heal = 1f, worsen = 1f;
        if (bodyAsleep)
            heal *= Mathf.Max(0f, r.sleepingHealMultiplier);
        if (bodyStarving || bodyDehydrated)
            heal *= Mathf.Clamp01(r.starvingHealMultiplier);
        if (bodyCold || bodyStarving || bodyDehydrated)
            worsen *= Mathf.Max(1f, r.weakenedWorseningMultiplier);
        if (Mathf.Abs(heal - bodyHealFactor) > 1e-3f || Mathf.Abs(worsen - bodyWorsenFactor) > 1e-3f)
        {
            bodyHealFactor = heal;
            bodyWorsenFactor = worsen;
            RefreshFactors();
        }
    }

    /// <summary>Is the character exposed to <paramref name="source"/> right now (as of the last check)?</summary>
    public bool IsExposedTo(ExposureSource source)
    {
        switch (source)
        {
            case ExposureSource.Cold: return bodyCold;
            case ExposureSource.Freezing: return bodyFreezing;
            case ExposureSource.Overheated: return bodyOverheated;
            case ExposureSource.Starving: return bodyStarving;
            case ExposureSource.Dehydrated: return bodyDehydrated;
            case ExposureSource.Exhausted: return bodyExhausted;
            case ExposureSource.Bleeding: return IsBleeding;
            case ExposureSource.OpenWound:
                for (int p = 0; p < parts.Count; p++)
                    for (int i = 0; i < parts[p].injuries.Count; i++)
                        if (parts[p].injuries[i].IsOpenWound)
                            return true;
                return false;
            default: return false;
        }
    }

    /// <summary>A catching illness passes to the characters near (their own resistances and immunity count).</summary>
    private void Spread(Injury injury, InjuryStage stage, float minutes, IllnessRules r)
    {
        CombatEntity e = Entity;
        if (r.contagionRadius <= 0f || e == null)
            return;
        float chance = 1f - Mathf.Pow(1f - Mathf.Clamp01(stage.contagionPerMinute), minutes);
        if (chance <= 0f)
            return;
        float range2 = r.contagionRadius * r.contagionRadius;
        Vector3 pos = e.Position;
        for (int i = 0; i < active.Count; i++)
        {
            InjuryController other = active[i];
            if (other == null || other == this || !other.RulesHere)
                continue;
            CombatEntity oe = other.Entity;
            if (oe == null || oe.IsDead || (oe.Position - pos).sqrMagnitude > range2)
                continue;
            Injury made = other.Expose(injury.Type, chance, 0, e);
            if (made != null && !made.Hidden)
                other.Notify($"You caught {made.Name.ToLowerInvariant()} from {OwnerName()}.");
        }
    }

    // ------------------------------------------------------------------ falls
    /// <summary>
    /// The highest jump the character can make right now (metres): its Jump Force with every trait, item, buff and size
    /// change, from a run. 0 when it has no movement model (a mob).
    /// </summary>
    public float OwnJumpHeight
    {
        get
        {
            FindMovement();
            return moveModel != null ? Mathf.Max(0f, moveModel.EstimatedJumpHeight) * RunningJump * RunningJump : 0f;
        }
    }

    /// <summary>
    /// Metres the character can drop without being hurt: the Fall rules' Safe Height and its Safe Fall Height stat
    /// (traits, items, buffs).
    /// </summary>
    public float SafeFallHeight
    {
        get
        {
            FallRules r = Settings.falls;
            InjuryResistances res = Resistances;
            return Mathf.Max(0.5f, (r != null ? r.safeHeight : 4f) + (res != null ? res.SafeFallHeightBonus() : 0f));
        }
    }

    /// <summary>
    /// Multiplies what a hard landing does to the character: its Fall Damage stat and its Injury Resistances' Fall Damage
    /// (0 = never hurt by a fall).
    /// </summary>
    public float FallDamageFactor
    {
        get
        {
            InjuryResistances res = Resistances;
            return res != null ? Mathf.Max(0f, res.FallDamageMultiplier()) : 1f;
        }
    }

    private void FindMovement()
    {
        // Searched again now and then: the movement may appear after this component.
        if (moveModel != null || Time.unscaledTime < nextMoveSearch)
            return;
        nextMoveSearch = Time.unscaledTime + 2f;
        CombatEntity e = Entity;
        Component root = e != null ? (Component)e : this;
        moveModel = root.GetComponentInParent<PlayerMovementModel>();
        if (moveModel == null)
            moveModel = root.GetComponentInChildren<PlayerMovementModel>();
        moveController = root.GetComponentInParent<PlayerMovementController>();
        if (moveController == null)
            moveController = root.GetComponentInChildren<PlayerMovementController>();
    }

    /// <summary>Is the character on the ground? False with <paramref name="known"/> false when nothing here can tell.</summary>
    private bool OnGround(out bool known)
    {
        FindMovement();
        known = moveController != null && moveController.isActiveAndEnabled && moveModel != null &&
                moveModel.PlayerShellObject != null && moveModel.Controller != null;
        return known && moveController.IsGrounded();
    }

    private void EndArc(float y)
    {
        fallInAir = fallCredit = fallForgiven = false;
        fallBaseY = fallTopY = y;
        fallingFor = fallStillFor = fallBrakedFor = fallPeak = fallLastSpeed = 0f;
    }

    /// <summary>
    /// Watches the character's height (players, rules side) and judges each landing by the metres dropped, so it works
    /// whatever gravity the movement uses:
    /// <list type="bullet">
    /// <item>the drop is counted from the top of the fall down to where it lands, less the height the character gained
    /// by jumping right before (up to its own highest jump): its own jump never hurts, however high;</item>
    /// <item>a descent that was braked in the air (a glider, a slow fall, a second jump) starts over from there, and one
    /// slower than a free fall over that height (a slope, stairs, a lift) is not a fall at all;</item>
    /// <item>a move forced by an ability, a dodge or a knockback, and the fall that follows it, do not count; neither do
    /// teleports, nor coming down in water deep enough.</item>
    /// </list>
    /// What is left above the safe height is a hard landing (<see cref="Land"/>).
    /// </summary>
    private void TickFalls(float dt, InjurySettings s)
    {
        FallRules r = s.falls;
        CombatEntity e = Entity;
        if (r == null || !r.enabled || e == null || dt <= 0f || e.Kind != CombatEntity.EntityKind.Player || e.IsDead)
        {
            fallHasLast = false;
            return;
        }
        float y = e.Position.y;
        if (!fallHasLast)
        {
            fallHasLast = true;
            fallLastY = y;
            fallSmoothed = 0f;
            EndArc(y);
            return;
        }
        float previousY = fallLastY;
        fallLastY = y;
        // A leap, a slam, a dodge or a knockback moves the body on purpose: neither it nor the fall after it is one.
        if (r.ignoreForcedMovement && (e.IsBeingDisplaced || e.IsEvading))
        {
            fallSmoothed = 0f;
            EndArc(y);
            fallInAir = true;
            fallForgiven = true;
            return;
        }
        // A jump in height the speed so far cannot explain: a teleport, not a fall.
        if (Mathf.Abs(y - previousY) > 3f && Mathf.Abs(y - previousY - fallSmoothed * dt) > Mathf.Max(3f, 40f * dt))
        {
            fallSmoothed = 0f;
            EndArc(y);
            return;
        }
        float vy = (y - previousY) / dt;
        fallSmoothed = Mathf.Lerp(fallSmoothed, vy, 1f - Mathf.Exp(-dt / 0.08f));

        bool known = false, grounded = false;
        if (fallInAir || vy > RiseSpeed || vy < -FallSpeed)
            grounded = OnGround(out known);

        if (vy > RiseSpeed && !grounded)
        {
            // Going up through the air: a jump (or a second one in mid-air, which ends the fall so far).
            if (!fallInAir)
            {
                fallInAir = true;
                fallBaseY = previousY;
                fallCredit = true;
            }
            fallTopY = y;
            fallingFor = fallStillFor = fallBrakedFor = fallPeak = fallLastSpeed = 0f;
            return;
        }
        if (vy < -FallSpeed && !grounded)
        {
            if (!fallInAir)
            {
                // Walked off an edge.
                fallInAir = true;
                fallBaseY = fallTopY = previousY;
                fallCredit = true;
            }
            if (fallingFor <= 0f)
            {
                fallTopY = Mathf.Max(fallTopY, previousY);
                fallPeak = 0f;
            }
            fallingFor += dt;
            fallStillFor = 0f;
            float speed = -fallSmoothed;
            fallPeak = Mathf.Max(fallPeak, speed);
            fallLastSpeed = speed;
            // Braked in the air (a glider opened, a slow fall), or coming down slowly all along: what was fallen
            // before no longer counts. (The first moment of a real fall is slow too, but never for this long.)
            if (speed < 4f)
            {
                fallBrakedFor += dt;
                if (fallBrakedFor >= (fallPeak > 6f ? 0.2f : 0.6f))
                {
                    fallTopY = y;
                    fallPeak = speed;
                    fallCredit = false;
                    fallBrakedFor = 0f;
                }
            }
            else
            {
                fallBrakedFor = 0f;
            }
            return;
        }
        if (!fallInAir)
            return;

        fallStillFor += dt;
        if (fallingFor <= 0f)
        {
            // It rose and now hangs at the top of the jump, or stands where it got to.
            fallTopY = Mathf.Max(fallTopY, y);
            if (grounded || fallStillFor >= (known ? 2f : 0.6f))
                EndArc(y);
            return;
        }
        // It was falling and no longer goes down.
        if (known && !grounded)
        {
            // Held in the air (gliding level, hovering, swimming): a soft stop, nothing fallen before counts.
            if (fallStillFor >= 0.2f)
            {
                fallTopY = y;
                fallCredit = false;
                fallingFor = fallStillFor = fallBrakedFor = fallPeak = fallLastSpeed = 0f;
            }
            return;
        }
        if (!known && fallStillFor < 0.1f)
            return; // a step between two position updates is not a landing

        float drop = fallTopY - y;
        float gained = fallCredit && r.ownJumpIsSafe ? Mathf.Clamp(fallTopY - fallBaseY, 0f, OwnJumpHeight * 1.1f + 0.1f) : 0f;
        float counted = drop - gained;
        float last = fallLastSpeed;
        float airtime = fallingFor;
        bool forgiven = fallForgiven;
        EndArc(y);
        if (forgiven || counted <= 0.5f || airtime < 0.1f)
            return;
        // Slower than a free fall over that height under the weakest gravity (a slope, stairs, a lift, a glide): no fall.
        if (last < 0.6f * Mathf.Sqrt(2f * 9.81f * counted))
            return;
        if (counted > SafeFallHeight && !LandedInWater(e, r.safeWaterDepth))
            Land(counted, r);
    }

    /// <summary>Did the character come down in water at least <paramref name="depth"/> deep (it breaks the fall)?</summary>
    private static bool LandedInWater(CombatEntity e, float depth)
    {
        if (depth <= 0f)
            return false;
        Vector3 feet = e.Position;
        if (!WaterQuery.TryGetWater(new Vector3(feet.x, feet.y + 0.05f, feet.z), e.gameObject, out float surface, out float bottom) || float.IsNaN(surface))
            return false;
        // The feet are in it, and there is enough of it between the surface and the bottom (a bottom not known: deep).
        return surface > feet.y && (float.IsNaN(bottom) || surface - bottom >= depth);
    }

    /// <summary>
    /// A hard landing after a drop of <paramref name="dropHeight"/> metres: what is above the character's safe height
    /// (<see cref="SafeFallHeight"/>) is a crushing hit on a foot or a leg (the other leg too from higher up) and costs
    /// health, both scaled by <see cref="FallDamageFactor"/>. Scripts may call it (a trap door, a throw).
    /// </summary>
    public void Land(float dropHeight, FallRules r = null)
    {
        r = r ?? Settings.falls;
        CombatEntity e = Entity;
        if (r == null || e == null || !RulesHere || e.IsDead || e.IsInvulnerable)
            return;
        float over = dropHeight - SafeFallHeight;
        float factor = FallDamageFactor;
        if (over <= 0f || factor <= 0f)
            return;
        if (activeTreatment != null && Settings.damageInterruptsTreatment)
            CancelTreatment("Interrupted by a fall.");
        float damage = over * r.damagePerMetre * factor;
        int legs = over >= r.bothLegsAbove ? 2 : 1;
        BodyPartSide side = UnityEngine.Random.value < 0.5f ? BodyPartSide.Left : BodyPartSide.Right;
        bool any = false;
        for (int k = 0; k < legs && damage > 0f; k++)
        {
            InjuryPartState part = LandingPart(r, side);
            side = side == BodyPartSide.Left ? BodyPartSide.Right : BodyPartSide.Left;
            if (part == null)
                continue;
            // The leg that takes the landing gets all of it, the other one a little less.
            ApplyHit(part, new DamageInfo
            {
                amount = k == 0 ? damage : damage * 0.6f, target = e, point = LimbPosition(part), direction = Vector3.up, type = DamageType.Physical,
                form = DamageForm.Crush, delivery = DamageDelivery.Unknown, aimedBodyPart = part.Id, unblockable = true, threatMultiplier = 0f,
            });
            any = true;
        }
        hitLateral = float.NaN;
        // What the fall costs in health: a share of the maximum (so a great height kills anyone), not a hit on a part
        // (the legs took theirs), and no armour softens it.
        float health = over * r.healthPercentPerMetre * 0.01f * e.MaxHealth * factor;
        if (health > 0f && !e.IsDead)
        {
            e.ApplyDamage(new DamageInfo
            {
                amount = health, source = null, target = e, point = e.Center, direction = Vector3.up, isPeriodic = true, unblockable = true,
                type = DamageType.True, delivery = DamageDelivery.Periodic, threatMultiplier = 0f,
            });
            any = true;
        }
        if (any)
            Notify("You landed hard.");
    }

    /// <summary>The part a landing hurts on <paramref name="side"/>: a foot more often than the leg above it.</summary>
    private InjuryPartState LandingPart(FallRules r, BodyPartSide side)
    {
        InjuryPartState picked = null;
        float total = 0f;
        for (int pass = 0; pass < 2 && picked == null; pass++)
        {
            total = 0f;
            for (int i = 0; i < parts.Count; i++)
            {
                InjuryPartState p = parts[i];
                if (p.Lost || p.IsWholeBody || r.parts == null || !r.parts.Contains(p.Def.kind))
                    continue;
                // The side asked for first; any side when that leg is gone.
                if (pass == 0 && p.Def.side != side && p.Def.side != BodyPartSide.Center)
                    continue;
                float w = p.Def.kind == BodyPartKind.Foot ? 3f : 1f;
                total += w;
                if (UnityEngine.Random.value * total < w)
                    picked = p;
            }
        }
        return picked;
    }
}
