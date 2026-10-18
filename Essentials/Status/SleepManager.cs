using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Where (or how) a character sleeps: decides the sleep quality.</summary>
public enum SleepSpotKind
{
    /// <summary>On the bare ground (the Sleep input anywhere).</summary>
    Ground,
    /// <summary>A bedroll or a tent.</summary>
    Bedroll,
    /// <summary>A bench, a chair, a hay pile.</summary>
    Bench,
    /// <summary>A real bed.</summary>
    Bed,
    /// <summary>The spot's own quality.</summary>
    Custom,
}

/// <summary>How good a night's sleep is in one place: recovery speed, what happens meanwhile, and how you feel on waking.</summary>
[Serializable]
public class SleepQuality
{
    [Tooltip("Name (shown when falling asleep: \"Sleeping (Bed)\").")]
    public string name = "Bed";
    [Tooltip("Rest recovered per minute is multiplied by this (1 = the Sleep Manager's Recovery Per Minute; 0.5 = half as fast).")]
    [Min(0f)] public float recoveryMultiplier = 1f;
    [Tooltip("Health per minute while sleeping here (0 = none).")]
    public float healthPerMinute;
    [Tooltip("Sanity per minute while sleeping here (positive = soothing, negative = nightmares).")]
    public float sanityPerMinute;
    [Tooltip("Hunger drains this many times as fast while sleeping here (0.5 = half).")]
    [Min(0f)] public float hungerDrain = 0.5f;
    [Tooltip("Thirst drains this many times as fast while sleeping here.")]
    [Min(0f)] public float thirstDrain = 0.5f;

    [Header("On waking up")]
    [Tooltip("Status given when waking up (a buff after a good bed, a debuff after the ground). Only its name, debuff flag " +
             "and stats are used. Empty name and no stats = nothing.")]
    public StatusConsequence wakeEffect = new StatusConsequence("", false);
    [Tooltip("Seconds the wake-up status lasts (0 = none).")]
    [Min(0f)] public float wakeEffectSeconds;
    [Tooltip("Only after sleeping at least this many seconds.")]
    [Min(0f)] public float minSleepForWakeEffect = 10f;
    [Tooltip("Only when rest is at least this percent on waking (a Well Rested buff needs a full night). 0 = always.")]
    [Range(0f, 100f)] public float wakeEffectNeedsRestAbove;

    public SleepQuality() { }

    public SleepQuality(string name, float recovery) { this.name = name; recoveryMultiplier = recovery; }

    public SleepQuality Clone()
    {
        var c = (SleepQuality)MemberwiseClone();
        c.wakeEffect = wakeEffect != null ? wakeEffect.Clone() : new StatusConsequence("", false);
        return c;
    }
}

/// <summary>A stage of tiredness: what happens while rest is below <see cref="belowPercent"/>.</summary>
[Serializable]
public class RestStage
{
    [Tooltip("The stage starts when rest is below this percent of the max.")]
    [Range(0f, 100f)] public float belowPercent = 50f;
    [Tooltip("What it does (status icon, stats, per-second effects, fainting).")]
    public StatusConsequence effect = new StatusConsequence("Tired");
}

/// <summary>
/// <b>Rest</b> (sleep need): 100 = fully rested, 0 = exhausted.
/// <list type="bullet">
/// <item>Awake: rest drops by <see cref="awakeLossPerMinute"/> per minute (Decrement Factor, a percent, changes it).</item>
/// <item>Tiredness <see cref="stages"/> (Tired, Very Tired, Exhausted by default) apply their debuffs as rest drops.</item>
/// <item>At <see cref="collapseAtPercent"/> the character collapses asleep where it stands (a poor sleep).</item>
/// <item>Asleep: rest comes back at <see cref="sleepRecoveryPerMinute"/> x the spot's quality (Increment Factor and the
/// Rest Recovery stat raise it). A bed sleeps best, a bench or bedroll less, the ground worst; each quality has its own
/// effects while sleeping and on waking (Well Rested, Stiff Back, Groggy...).</item>
/// <item>Potions and food with a Sleep effect restore rest directly.</item>
/// </list>
/// Sleep with a <see cref="SleepSpot"/> (beds, benches: Interact) or anywhere with the Sleep input (<see cref="SleepController"/>).
/// While asleep the character cannot move or act; damage wakes it. Network sessions: the server decides.
/// </summary>
public class SleepManager : StatusManager
{
    [Header("Rest")]
    [Tooltip("Start fully rested.")]
    [SerializeField] private bool startFull = true;
    [Tooltip("Rest drops while awake.")]
    [SerializeField] private bool drainWhileAwake = true;
    [Tooltip("Rest lost per minute while awake (before Decrement Factor). 1.5 = a full bar lasts about 66 minutes.")]
    [SerializeField, Min(0f)] private float awakeLossPerMinute = 1.5f;
    [Tooltip("Rest recovered per minute while sleeping, before the spot's quality, Increment Factor and the Rest Recovery stat. 25 = 4 minutes from empty to full in a bed.")]
    [SerializeField, Min(0f)] private float sleepRecoveryPerMinute = 25f;

    [Header("Tiredness")]
    [Tooltip("Stages of tiredness, each with its own effects. The lowest stage reached applies (only one at a time).")]
    [SerializeField] private List<RestStage> stages = DefaultStages();

    [Header("Collapse")]
    [Tooltip("At this rest percent the character collapses asleep where it stands (0 = when empty). Below 0 = never.")]
    [SerializeField, Range(-1f, 50f)] private float collapseAtPercent = 0f;
    [Tooltip("A collapsed character sleeps at least until rest reaches this percent.")]
    [SerializeField, Range(1f, 100f)] private float collapseWakeAtPercent = 30f;
    [Tooltip("Sleep quality of a collapse.")]
    [SerializeField] private SleepQuality collapseQuality = DefaultQuality(SleepQualityPreset.Collapse);

    [Header("Falling asleep / waking")]
    [Tooltip("Cannot fall asleep when rest is above this percent (\"You are not tired\").")]
    [SerializeField, Range(0f, 100f)] private float cannotSleepAbovePercent = 95f;
    [Tooltip("Cannot fall asleep within this many seconds of taking damage (in combat). 0 = always.")]
    [SerializeField, Min(0f)] private float noSleepAfterDamageSeconds = 8f;
    [Tooltip("Wake up by itself when rest is full.")]
    [SerializeField] private bool wakeWhenRested = true;
    [Tooltip("Taking damage wakes the character.")]
    [SerializeField] private bool wakeOnDamage = true;

    [Header("Sleep qualities (by kind of spot)")]
    [SerializeField] private SleepQuality groundQuality = DefaultQuality(SleepQualityPreset.Ground);
    [SerializeField] private SleepQuality bedrollQuality = DefaultQuality(SleepQualityPreset.Bedroll);
    [SerializeField] private SleepQuality benchQuality = DefaultQuality(SleepQualityPreset.Bench);
    [SerializeField] private SleepQuality bedQuality = DefaultQuality(SleepQualityPreset.Bed);

    [Header("UI Settings")]
    [Tooltip("Show the rest bar even when rested (off: it appears once the first tiredness stage is near, or while sleeping).")]
    [SerializeField] private bool showUIWhenRested = false;

    [Header("State (read only)")]
    [SerializeField] private bool isSleeping = false;
    [SerializeField] private bool hasCollapsed = false;

    private bool started;
    private List<CoroutineInfo> sleepEffectRoutines = new List<CoroutineInfo>();
    private ConsequenceRunner stageRunner;
    private SleepQuality quality;
    private SleepSpot spot;
    private float sleepStart;
    private float lastDamage = -999f;
    private CombatEntity entity;
    private AvailabilityStateMachine availability;
    private readonly object sleepKey = new object();
    private float baseIncrement;

    public bool IsSleeping => isSleeping;
    public bool HasCollapsed => hasCollapsed;
    public SleepinessLevel CurrentSleepinessLevel => GetSleepinessLevel();
    public List<CoroutineInfo> SleepEffectRoutines { get => sleepEffectRoutines; set => sleepEffectRoutines = value; }
    /// <summary>The quality of the current sleep (null when awake).</summary>
    public SleepQuality CurrentQuality => isSleeping ? quality : null;
    /// <summary>The spot slept in (null on the ground or awake).</summary>
    public SleepSpot CurrentSpot => spot;
    /// <summary>Seconds asleep.</summary>
    public float SleepingFor => isSleeping ? Time.time - sleepStart : 0f;
    /// <summary>The tiredness stage reached (-1 = rested).</summary>
    public int StageIndex { get; private set; } = -1;
    /// <summary>The tiredness stages.</summary>
    public IReadOnlyList<RestStage> Stages => stages;

    /// <summary>Fell asleep (quality, spot or null) / woke up.</summary>
    public event Action<SleepQuality, SleepSpot> FellAsleep;
    public event Action WokeUp;

    public enum SleepinessLevel
    {
        Rested,
        SlightlySleepy,
        ModeratelySleepy,
        ExtremelySleepy
    }

    private enum SleepQualityPreset { Ground, Bedroll, Bench, Bed, Collapse }

    private void Awake()
    {
        stageRunner = new ConsequenceRunner(this, "Tiredness");
        baseIncrement = incrementValue;
    }

    private CombatEntity Entity
    {
        get
        {
            if (entity == null)
            {
                entity = GetComponentInParent<CombatEntity>() ?? GetComponentInChildren<CombatEntity>();
                if (entity != null) entity.Damaged += OnDamaged;
            }
            return entity;
        }
    }

    private AvailabilityStateMachine Availability
    {
        get
        {
            if (availability == null)
                availability = GetComponentInParent<AvailabilityStateMachine>() ?? GetComponentInChildren<AvailabilityStateMachine>();
            return availability;
        }
    }

    private void Start()
    {
        started = true;
        // A client's copy keeps the server's value (the server simulates rest).
        if (!IsReplica && startFull)
            currentValue = maxValue;
        _ = Entity;
        UpdateUIVisibility();
    }

    private void OnDestroy()
    {
        if (entity != null) entity.Damaged -= OnDamaged;
        stageRunner?.Clear();
    }

    private void OnDisable()
    {
        if (isSleeping && !IsReplica)
            StopSleeping();
        stageRunner?.Clear();
    }

    protected override void OnReplicaChanged(bool on)
    {
        stageRunner?.Clear();
        StageIndex = -1;
    }

    /// <summary>Client copies: shows the server's sleeping / collapsed state (and locks the owner's movement while asleep).</summary>
    public void ApplyReplicatedState(bool sleeping, bool collapsed)
    {
        if (!IsReplica)
            return;
        bool was = isSleeping;
        isSleeping = sleeping;
        hasCollapsed = collapsed;
        if (was != sleeping)
        {
            if (sleeping) sleepStart = Time.time;
            Availability?.SetIncapacitated(sleepKey, sleeping);
            if (sleeping) FellAsleep?.Invoke(null, null);
            else WokeUp?.Invoke();
        }
    }

    private void Update()
    {
        UpdateSleepBar();
        if (IsReplica)
            return; // rest is simulated by the server
        float dt = Time.deltaTime;
        CombatEntity e = Entity;
        bool down = e != null && (e.IsDead || e.IsUnconscious);
        if (down)
        {
            if (isSleeping) StopSleeping();
            stageRunner.Clear();
            StageIndex = -1;
            return;
        }

        if (isSleeping)
        {
            SleepTick(dt);
        }
        else
        {
            if (drainWhileAwake && awakeLossPerMinute > 0f)
            {
                currentValue = Mathf.Max(0f, currentValue - RemoveFactor(awakeLossPerMinute) * RateMultiplier * dt / 60f);
                isConsuming = true;
            }
            if (collapseAtPercent >= 0f && Percent <= collapseAtPercent)
                Collapse();
        }

        // Tiredness stages (none while asleep: the sleep's own effects apply).
        int stage = isSleeping ? -1 : FindStage();
        if (stage != StageIndex)
        {
            StageIndex = stage;
            stageRunner.Set(stage >= 0 ? stages[stage].effect : null);
        }
        stageRunner.Tick(dt);
    }

    private float Percent => maxValue > 0f ? currentValue / maxValue * 100f : 100f;

    private int FindStage()
    {
        int best = -1;
        float bestBelow = float.MaxValue;
        float p = Percent;
        for (int i = 0; i < stages.Count; i++)
        {
            RestStage s = stages[i];
            if (s == null || p >= s.belowPercent) continue;
            if (s.belowPercent < bestBelow) { bestBelow = s.belowPercent; best = i; }
        }
        return best;
    }

    private void SleepTick(float dt)
    {
        SleepQuality q = quality ?? groundQuality;
        float bonus = 1f;
        CombatEntity e = Entity;
        if (e != null && e.Stats != null) bonus = Mathf.Max(0f, 1f + e.Stats.Get(CombatStatType.RestRecovery) / 100f);
        // Equipment Sleep Regeneration changes Increment Value: that change is extra rest per minute.
        float perMinute = Mathf.Max(0f, sleepRecoveryPerMinute + (incrementValue - baseIncrement));
        float gain = AddFactor(perMinute) * q.recoveryMultiplier * bonus * dt / 60f;
        currentValue = Mathf.Min(maxValue, currentValue + gain);
        isRegenerating = gain > 0f;
        isConsuming = false;

        if (e != null)
        {
            if (q.healthPerMinute > 0f) e.ApplyHeal(q.healthPerMinute * dt / 60f);
            else if (q.healthPerMinute < 0f) StatusDamage.Apply(e, -q.healthPerMinute * dt / 60f);
        }
        if (q.sanityPerMinute != 0f)
        {
            SanityManager sanity = GetComponent<SanityManager>() ?? GetComponentInParent<SanityManager>();
            if (sanity != null) sanity.ChangeSanity(q.sanityPerMinute * dt / 60f);
        }

        bool collapsedStillTired = hasCollapsed && Percent < collapseWakeAtPercent;
        if (wakeWhenRested && currentValue >= maxValue - 0.001f && !collapsedStillTired)
            StopSleeping();
        else if (hasCollapsed && !wakeWhenRested && Percent >= collapseWakeAtPercent)
            StopSleeping();
    }

    private void OnDamaged(DamageInfo info)
    {
        lastDamage = Time.time;
        if (wakeOnDamage && isSleeping && !IsReplica && info.amount > 0f)
            StopSleeping();
    }

    public void UpdateSleepBar()
    {
        UpdateBar(currentValue, maxValue, uiImage);
        currentValue = Mathf.Clamp(currentValue, 0, maxValue);
        UpdateUIVisibility();
    }

    private void UpdateUIVisibility()
    {
        if (uiImage == null) return;
        float first = 0f;
        foreach (RestStage s in stages) if (s != null) first = Mathf.Max(first, s.belowPercent);
        bool shouldShowUI = showUIWhenRested || isSleeping || Percent < first + 15f;
        if (uiImage.gameObject.activeSelf != shouldShowUI)
            uiImage.gameObject.SetActive(shouldShowUI);
    }

    /// <summary>How tired the character is (from the tiredness stage reached).</summary>
    public SleepinessLevel GetSleepinessLevel()
    {
        int stage = FindStage();
        if (stage < 0) return SleepinessLevel.Rested;
        // Rank the stage among the stages (by threshold): 1st = slightly, 2nd = moderately, 3rd+ = extremely.
        int rank = 0;
        float below = stages[stage].belowPercent;
        foreach (RestStage s in stages) if (s != null && s.belowPercent > below) rank++;
        return rank == 0 ? SleepinessLevel.SlightlySleepy : rank == 1 ? SleepinessLevel.ModeratelySleepy : SleepinessLevel.ExtremelySleepy;
    }

    // ------------------------------------------------------------------ sleeping

    /// <summary>The quality of a kind of spot.</summary>
    public SleepQuality QualityFor(SleepSpotKind kind)
    {
        switch (kind)
        {
            case SleepSpotKind.Bed: return bedQuality;
            case SleepSpotKind.Bench: return benchQuality;
            case SleepSpotKind.Bedroll: return bedrollQuality;
            default: return groundQuality;
        }
    }

    /// <summary>Can the character fall asleep now? <paramref name="reason"/> says why not.</summary>
    public bool CanSleepNow(out string reason)
    {
        reason = "";
        CombatEntity e = Entity;
        if (isSleeping) { reason = "Already sleeping"; return false; }
        if (e != null && (e.IsDead || e.IsUnconscious)) { reason = "Cannot sleep now"; return false; }
        if (Percent > cannotSleepAbovePercent) { reason = "You are not tired"; return false; }
        if (noSleepAfterDamageSeconds > 0f && Time.time - lastDamage < noSleepAfterDamageSeconds) { reason = "You are in danger"; return false; }
        return true;
    }

    /// <summary>Falls asleep on the ground (the Sleep input; on a client's copy use PlayerStatusController.StartSleeping).</summary>
    public void StartSleeping() => StartSleeping(groundQuality, null);

    /// <summary>Falls asleep with the quality of <paramref name="kind"/>.</summary>
    public void StartSleeping(SleepSpotKind kind) => StartSleeping(QualityFor(kind), null);

    /// <summary>
    /// Falls asleep with <paramref name="sleepQuality"/> (in <paramref name="atSpot"/>, which is then occupied). Returns
    /// false when it cannot (see <see cref="CanSleepNow"/>) or on a client's copy (ask through PlayerStatusController).
    /// </summary>
    public bool StartSleeping(SleepQuality sleepQuality, SleepSpot atSpot)
    {
        if (IsReplica) return false; // ask the server (PlayerStatusController.StartSleeping sends the request)
        if (!CanSleepNow(out string reason))
        {
            if (!string.IsNullOrEmpty(reason)) Debug.Log($"[Sleep] {name}: {reason}.", this);
            return false;
        }
        return BeginSleep(sleepQuality ?? groundQuality, atSpot, false);
    }

    private bool BeginSleep(SleepQuality q, SleepSpot atSpot, bool collapsed)
    {
        if (atSpot != null && !atSpot.Occupy(Entity))
            return false;
        isSleeping = true;
        hasCollapsed = collapsed;
        isConsuming = false;
        quality = q;
        spot = atSpot;
        sleepStart = Time.time;
        StageIndex = -1;
        stageRunner.Clear();
        Availability?.SetIncapacitated(sleepKey, true);
        CombatEntity e = Entity;
        if (e != null && e.Caster != null) e.Caster.Interrupt(CastInterruptReason.Stun, true);
        PlayerStatusController status = GetComponent<PlayerStatusController>() ?? GetComponentInParent<PlayerStatusController>();
        if (status != null)
        {
            status.HungerManager?.SetRateMultiplier(sleepKey, q.hungerDrain);
            status.ThirstManager?.SetRateMultiplier(sleepKey, q.thirstDrain);
        }
        FellAsleep?.Invoke(q, atSpot);
        return true;
    }

    /// <summary>Wakes up (on a client's copy use PlayerStatusController.StopSleeping).</summary>
    public void StopSleeping()
    {
        if (IsReplica) return;
        if (!isSleeping) return;

        SleepQuality q = quality;
        float slept = Time.time - sleepStart;
        isSleeping = false;
        hasCollapsed = false;
        isRegenerating = false;
        Availability?.SetIncapacitated(sleepKey, false);
        if (spot != null) spot.Release(Entity);
        spot = null;
        quality = null;
        PlayerStatusController status = GetComponent<PlayerStatusController>() ?? GetComponentInParent<PlayerStatusController>();
        if (status != null)
        {
            status.HungerManager?.ClearRateMultiplier(sleepKey);
            status.ThirstManager?.ClearRateMultiplier(sleepKey);
        }
        CombatEntity e = Entity;
        if (q != null && e != null && !e.IsDead && !e.IsUnconscious)
            ApplyWakeEffect(q, slept, e);
        WokeUp?.Invoke();
    }

    private void ApplyWakeEffect(SleepQuality q, float slept, CombatEntity e)
    {
        StatusConsequence w = q.wakeEffect;
        if (w == null || q.wakeEffectSeconds <= 0f || slept < q.minSleepForWakeEffect || Percent < q.wakeEffectNeedsRestAbove)
            return;
        bool hasStats = (w.combatStats != null && w.combatStats.Count > 0) || (w.characterStats != null && w.characterStats.Count > 0);
        if (string.IsNullOrEmpty(w.label) && !hasStats)
            return;
        TimedStatModifiers.For(e)?.Apply(q, string.IsNullOrEmpty(w.label) ? q.name : w.label, null, w.debuff, w.element,
            q.wakeEffectSeconds, 1f, w.combatStats, null, w.characterStats, EffectStacking.Refresh);
    }

    private void Collapse()
    {
        if (isSleeping) return;
        CombatEntity e = Entity;
        if (e != null && (e.IsDead || e.IsUnconscious)) return;
        Debug.Log($"[Sleep] {name} collapsed from exhaustion.", this);
        BeginSleep(collapseQuality ?? groundQuality, null, true);
    }

    /// <summary>Restores <paramref name="amount"/> rest (a potion, coffee). Negative = makes the character more tired.</summary>
    public void Restore(float amount)
    {
        if (IsReplica || amount == 0f) return;
        if (amount > 0f) AddCurrentValue(amount);
        else currentValue = Mathf.Max(0f, currentValue - RemoveFactor(-amount));
    }

    public override void ResetForRespawn(float fraction)
    {
        if (isSleeping) StopSleeping();
        base.ResetForRespawn(fraction);
        stageRunner?.Clear();
        StageIndex = -1;
    }

    protected override void UpdateStatus() => UpdateBar();
    protected override float AddFactor(float amount) => ApplyFactor(amount, incrementFactor);
    protected override float RemoveFactor(float amount) => ApplyFactor(amount, decrementFactor);

    // Effect methods (potions over time: positive restores rest)
    public void AddSleepEffect(string effectName, float sleepAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(SleepEffectRoutines, effectName, sleepAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplySleepEffectRoutine);

    public void RemoveSleepEffect(string effectName = null, Coroutine effectRoutine = null)
        => RemoveEffect(SleepEffectRoutines, effectName, effectRoutine);

    public void StopAllSleepEffects()
        => StopAllEffects(SleepEffectRoutines);

    public void StopAllSleepEffectsByType(bool isBuff = true)
        => StopAllEffectsByType(SleepEffectRoutines, isBuff);

    public IEnumerator ApplySleepEffectRoutine(string effectName, float sleepAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            sleepAmount,
            timeBuffEffect,
            tickCooldown,
            isProcedural,
            isStackable,
            perTick => Restore(perTick),
            total => Restore(total),
            _ => currentValue = Mathf.Clamp(currentValue, 0, maxValue)
        );
    }

    // ------------------------------------------------------------------ defaults

    private static List<RestStage> DefaultStages() => new List<RestStage>
    {
        new RestStage
        {
            belowPercent = 50f,
            effect = new StatusConsequence("Tired")
            {
                characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MoveSpeed, -5f), TraitModifier.Percent(TraitStat.StaminaRegen, -10f) },
            },
        },
        new RestStage
        {
            belowPercent = 25f,
            effect = new StatusConsequence("Very Tired")
            {
                characterStats = new List<TraitModifier>
                {
                    TraitModifier.Percent(TraitStat.MoveSpeed, -10f), TraitModifier.Percent(TraitStat.StaminaRegen, -25f),
                    TraitModifier.Percent(TraitStat.AbilityCastTime, 10f),
                },
                sanityPerSecond = -0.03f,
            },
        },
        new RestStage
        {
            belowPercent = 10f,
            effect = new StatusConsequence("Exhausted")
            {
                characterStats = new List<TraitModifier>
                {
                    TraitModifier.Percent(TraitStat.MoveSpeed, -20f), TraitModifier.Percent(TraitStat.StaminaRegen, -40f),
                    TraitModifier.Percent(TraitStat.AbilityCastTime, 20f),
                },
                combatStats = new List<CombatStatModifier> { new CombatStatModifier(CombatStatType.WeaponDamage, -10f) },
                sanityPerSecond = -0.1f,
                stunChancePerMinute = 0.25f,
                stunSeconds = 2f,
            },
        },
    };

    private static SleepQuality DefaultQuality(SleepQualityPreset preset)
    {
        switch (preset)
        {
            case SleepQualityPreset.Bed:
                return new SleepQuality("Bed", 1f)
                {
                    healthPerMinute = 12f, sanityPerMinute = 4f, hungerDrain = 0.5f, thirstDrain = 0.5f,
                    wakeEffect = new StatusConsequence("Well Rested", false)
                    {
                        characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.StaminaRegen, 10f), TraitModifier.Percent(TraitStat.MoveSpeed, 3f) },
                    },
                    wakeEffectSeconds = 300f, wakeEffectNeedsRestAbove = 90f,
                };
            case SleepQualityPreset.Bedroll:
                return new SleepQuality("Bedroll", 0.75f)
                {
                    sanityPerMinute = 1f, hungerDrain = 0.6f, thirstDrain = 0.6f,
                    wakeEffect = new StatusConsequence("Slept Rough")
                    {
                        characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.StaminaRegen, -5f) },
                    },
                    wakeEffectSeconds = 90f,
                };
            case SleepQualityPreset.Bench:
                return new SleepQuality("Bench", 0.6f)
                {
                    hungerDrain = 0.6f, thirstDrain = 0.6f,
                    wakeEffect = new StatusConsequence("Sore")
                    {
                        characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.StaminaRegen, -8f) },
                    },
                    wakeEffectSeconds = 120f,
                };
            case SleepQualityPreset.Collapse:
                return new SleepQuality("Collapsed", 0.3f)
                {
                    sanityPerMinute = -3f, hungerDrain = 0.7f, thirstDrain = 0.7f,
                    wakeEffect = new StatusConsequence("Groggy")
                    {
                        characterStats = new List<TraitModifier>
                        {
                            TraitModifier.Percent(TraitStat.MoveSpeed, -15f), TraitModifier.Percent(TraitStat.StaminaRegen, -20f),
                            TraitModifier.Percent(TraitStat.AbilityCastTime, 10f),
                        },
                    },
                    wakeEffectSeconds = 240f, minSleepForWakeEffect = 0f,
                };
            default:
                return new SleepQuality("Ground", 0.45f)
                {
                    sanityPerMinute = -1f, hungerDrain = 0.6f, thirstDrain = 0.6f,
                    wakeEffect = new StatusConsequence("Stiff Back")
                    {
                        characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.StaminaRegen, -10f), TraitModifier.Percent(TraitStat.MoveSpeed, -5f) },
                    },
                    wakeEffectSeconds = 180f,
                };
        }
    }

    [ContextMenu("Reset Stages And Qualities To Defaults")]
    private void ResetDefaults()
    {
        stages = DefaultStages();
        groundQuality = DefaultQuality(SleepQualityPreset.Ground);
        bedrollQuality = DefaultQuality(SleepQualityPreset.Bedroll);
        benchQuality = DefaultQuality(SleepQualityPreset.Bench);
        bedQuality = DefaultQuality(SleepQualityPreset.Bed);
        collapseQuality = DefaultQuality(SleepQualityPreset.Collapse);
    }
}
