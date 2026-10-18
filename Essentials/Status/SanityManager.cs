using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>A stage of lost sanity: what happens while sanity is below <see cref="belowPercent"/>.</summary>
[Serializable]
public class SanityStage
{
    [Tooltip("The stage starts when sanity is below this percent of the max.")]
    [Range(0f, 100f)] public float belowPercent = 40f;
    [Tooltip("What it does (status icon, stats, health loss, panic attacks).")]
    public StatusConsequence effect = new StatusConsequence("Paranoid");
    [Tooltip("Strength of the screen effect at this stage for the local player (0 = none, 1 = strongest).")]
    [Range(0f, 1f)] public float screenEffect = 0.3f;
}

/// <summary>
/// <b>Sanity</b>: 100 = clear mind, 0 = broken.
/// <para><b>What lowers it</b> (all configurable, every loss reduced by the <b>Sanity Resistance</b> stat - 100 = immune - and
/// changed by Decrement Factor, a percent):</para>
/// <list type="bullet">
/// <item>Continuous sources: <see cref="SanityAura"/> on mobs, cursed places or darkness; equipment with a negative Sanity
/// Regeneration (a cursed amulet drains it every second); any script via <see cref="SetSource"/>.</item>
/// <item>Hits: mobs with <see cref="SanityOnHit"/> (a ghost's touch, a horror's scream), abilities with the Sanity effect.</item>
/// <item>Consumables with a negative Sanity effect (strange mushrooms), being exhausted or freezing (their stages), sleeping
/// on the ground or collapsing.</item>
/// </list>
/// <para><b>What restores it</b>: consumables / potions (Sanity, Sanity Regeneration), abilities and skills (Sanity effect
/// with a positive amount), positive auras (a campfire, a shrine), sleeping in a bed, equipment with Sanity Regeneration, and a
/// slow passive recovery up to <see cref="passiveRecoveryUpToPercent"/> while nothing drains it.</para>
/// <para><b>Consequences</b>: the <see cref="stages"/> (Uneasy, Paranoid, Hysteric, Broken Mind by default): slower, weaker,
/// clumsier casting, more damage taken, panic attacks (short stuns), health loss at the bottom - plus a screen effect and
/// whispers for the local player (<see cref="SanityScreenEffect"/>). Network sessions: the server decides.</para>
/// </summary>
public class SanityManager : StatusManager
{
    [Header("Sanity")]
    [Tooltip("Start with a clear mind.")]
    [SerializeField] private bool startFull = true;
    [Tooltip("Sanity recovered per second while no source drains it (equipment Sanity Regeneration adds to it).")]
    [SerializeField] private float passiveRecoveryPerSecond = 0.05f;
    [Tooltip("The passive recovery stops at this percent: above it, only rest, items and abilities restore sanity.")]
    [SerializeField, Range(0f, 100f)] private float passiveRecoveryUpToPercent = 70f;
    [Tooltip("No passive recovery while any source drains sanity.")]
    [SerializeField] private bool noRecoveryWhileDrained = true;

    [Header("Stages")]
    [Tooltip("Stages of lost sanity, each with its own effects. The lowest stage reached applies (only one at a time).")]
    [SerializeField] private List<SanityStage> stages = DefaultStages();

    [Header("Events (where the game rules run)")]
    [Tooltip("The stage changed (index, -1 = stable).")]
    public UnityEvent<int> onStageChanged = new UnityEvent<int>();

    private bool started;
    private List<CoroutineInfo> sanityEffectRoutines = new List<CoroutineInfo>();
    private ConsequenceRunner stageRunner;
    private float baseIncrement;
    private readonly Dictionary<object, float> sources = new Dictionary<object, float>();
    private readonly Dictionary<object, string> sourceNames = new Dictionary<object, string>();
    private CombatEntity entity;

    public SanityLevel CurrentSanityLevel => GetSanityLevel();
    public List<CoroutineInfo> SanityEffectRoutines { get => sanityEffectRoutines; set => sanityEffectRoutines = value; }
    /// <summary>The stage reached (-1 = stable).</summary>
    public int StageIndex { get; private set; } = -1;
    public IReadOnlyList<SanityStage> Stages => stages;
    /// <summary>Screen-effect strength of the current stage (0-1), for the local player's screen.</summary>
    public float ScreenEffect => StageIndex >= 0 && StageIndex < stages.Count && stages[StageIndex] != null ? stages[StageIndex].screenEffect : 0f;
    /// <summary>Sum of the continuous sources right now (per second; negative = draining).</summary>
    public float SourcesPerSecond { get; private set; }
    /// <summary>Sanity lost or gained per second right now, everything included (inspectors).</summary>
    public float NetPerSecond { get; private set; }
    /// <summary>A loss was applied (amount after resistance, source or null).</summary>
    public event Action<float, CombatEntity> SanityLost;

    public enum SanityLevel
    {
        Stable,
        Stressed,
        Unstable,
        Critical
    }

    private void Awake()
    {
        stageRunner = new ConsequenceRunner(this, "Sanity");
        baseIncrement = incrementValue;
    }

    private CombatEntity Entity => entity != null ? entity : (entity = GetComponentInParent<CombatEntity>() ?? GetComponentInChildren<CombatEntity>());

    private void Start()
    {
        started = true;
        // A client's copy keeps the server's value (the server simulates sanity).
        if (IsReplica)
            return;
        if (startFull)
            currentValue = maxValue;
    }

    private void OnDisable() => stageRunner?.Clear();

    protected override void OnReplicaChanged(bool on)
    {
        stageRunner?.Clear();
        if (on) StageIndex = -1;
    }

    private void Update()
    {
        // The stage is also found on a client's copy (its screen effect); its rules run where the game runs.
        int stage = FindStage();
        if (IsReplica)
        {
            StageIndex = stage;
            return;
        }
        float dt = Time.deltaTime;
        CombatEntity e = Entity;
        if (e != null && e.IsDead)
        {
            stageRunner.Clear();
            StageIndex = -1;
            return;
        }

        // Continuous sources (auras, scripts) and equipment (Sanity Regeneration = the change of Increment Value).
        float sum = 0f;
        foreach (float v in sources.Values) sum += v;
        float equipment = incrementValue - baseIncrement;
        SourcesPerSecond = sum;
        float before = currentValue;
        float change = sum + equipment;
        if (change < 0f) Lose(-change * dt, null, false);
        else if (change > 0f) Gain(change * dt);

        bool drained = sum < 0f || equipment < 0f;
        if (passiveRecoveryPerSecond > 0f && (!noRecoveryWhileDrained || !drained) && Percent < passiveRecoveryUpToPercent)
            currentValue = Mathf.Min(maxValue * passiveRecoveryUpToPercent / 100f, currentValue + AddFactor(passiveRecoveryPerSecond) * dt);
        isRegenerating = currentValue > before;
        isConsuming = currentValue < before;
        NetPerSecond = dt > 0f ? (currentValue - before) / dt : 0f;

        if (stage != StageIndex)
        {
            StageIndex = stage;
            stageRunner.Set(stage >= 0 ? stages[stage].effect : null);
            onStageChanged?.Invoke(stage);
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
            SanityStage s = stages[i];
            if (s == null || p >= s.belowPercent) continue;
            if (s.belowPercent < bestBelow) { bestBelow = s.belowPercent; best = i; }
        }
        return best;
    }

    public void UpdateSanityBar()
    {
        UpdateBar(currentValue, maxValue, uiImage);
        currentValue = Mathf.Clamp(currentValue, 0, maxValue);
    }

    public SanityLevel GetSanityLevel()
    {
        int stage = FindStage();
        if (stage < 0) return SanityLevel.Stable;
        int rank = 0;
        float below = stages[stage].belowPercent;
        foreach (SanityStage s in stages) if (s != null && s.belowPercent > below) rank++;
        int count = 0;
        foreach (SanityStage s in stages) if (s != null) count++;
        if (rank >= count - 1) return SanityLevel.Critical;
        if (rank >= count - 2 && count > 2) return SanityLevel.Unstable;
        return SanityLevel.Stressed;
    }

    // ------------------------------------------------------------------ changing sanity

    /// <summary>
    /// Changes sanity: negative = a loss (reduced by Sanity Resistance and changed by Decrement Factor), positive = a
    /// restore (changed by Increment Factor). <paramref name="source"/> is who caused it (may be null).
    /// </summary>
    public void ChangeSanity(float amount, CombatEntity source = null)
    {
        if (IsReplica || amount == 0f)
            return;
        if (amount < 0f) Lose(-amount, source, true);
        else Gain(amount);
    }

    /// <summary>Removes <paramref name="amount"/> sanity (after resistance).</summary>
    public void ConsumeSanity(float amount) => ChangeSanity(-Mathf.Abs(amount));

    /// <summary>Restores <paramref name="amount"/> sanity.</summary>
    public void RestoreSanity(float amount) => ChangeSanity(Mathf.Abs(amount));

    /// <summary>Consumables, legacy effects: negative amounts are losses (resisted), positive amounts restores.</summary>
    public override void AddCurrentValue(float amount) => ChangeSanity(amount);

    /// <summary>Sanity Resistance of the character (percent, 100 = immune).</summary>
    public float Resistance
    {
        get
        {
            CombatEntity e = Entity;
            return e != null && e.Stats != null ? Mathf.Clamp(e.Stats.Get(CombatStatType.SanityResistance), -100f, 100f) : 0f;
        }
    }

    private void Lose(float amount, CombatEntity source, bool notify)
    {
        if (amount <= 0f || currentValue <= 0f)
            return;
        float taken = RemoveFactor(amount) * (1f - Resistance / 100f);
        if (taken <= 0f)
            return;
        currentValue = Mathf.Max(0f, currentValue - taken);
        if (notify) SanityLost?.Invoke(taken, source);
    }

    private void Gain(float amount)
    {
        if (amount <= 0f)
            return;
        currentValue = Mathf.Min(maxValue, currentValue + AddFactor(amount));
    }

    /// <summary>
    /// Sets a continuous source: <paramref name="perSecond"/> sanity per second (negative drains) while it is set. The
    /// same <paramref name="key"/> replaces its value; <see cref="ClearSource"/> removes it (auras do it when you leave).
    /// </summary>
    public void SetSource(object key, float perSecond, string label = null)
    {
        if (key == null) return;
        if (perSecond == 0f) { ClearSource(key); return; }
        sources[key] = perSecond;
        if (!string.IsNullOrEmpty(label)) sourceNames[key] = label;
    }

    /// <summary>Removes the continuous source of <paramref name="key"/>.</summary>
    public void ClearSource(object key)
    {
        if (key == null) return;
        sources.Remove(key);
        sourceNames.Remove(key);
    }

    /// <summary>The continuous sources right now ("Wraith -0.5/s"), for inspectors.</summary>
    public IEnumerable<string> DescribeSources()
    {
        foreach (KeyValuePair<object, float> kv in sources)
        {
            string n = sourceNames.TryGetValue(kv.Key, out string s) ? s : kv.Key is UnityEngine.Object o && o != null ? o.name : kv.Key.ToString();
            yield return $"{n} {kv.Value:+0.##;-0.##}/s";
        }
    }

    public override void ResetForRespawn(float fraction)
    {
        base.ResetForRespawn(fraction);
        stageRunner?.Clear();
        StageIndex = -1;
    }

    protected override void UpdateStatus() => UpdateSanityBar();
    protected override float AddFactor(float amount) => ApplyFactor(amount, incrementFactor);
    protected override float RemoveFactor(float amount) => ApplyFactor(amount, decrementFactor);

    // Effect methods
    public void AddSanityEffect(string effectName, float sanityAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(SanityEffectRoutines, effectName, sanityAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplySanityEffectRoutine);

    public void AddSanityHealFactorEffect(string effectName, float sanityHealFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(SanityEffectRoutines, effectName, sanityHealFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplySanityHealFactorEffectRoutine);

    public void AddSanityDamageFactorEffect(string effectName, float sanityDamageFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(SanityEffectRoutines, effectName, sanityDamageFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplySanityDamageFactorEffectRoutine);

    public void RemoveSanityEffect(string effectName = null, Coroutine effectRoutine = null)
        => RemoveEffect(SanityEffectRoutines, effectName, effectRoutine);

    public void StopAllSanityEffects()
        => StopAllEffects(SanityEffectRoutines);

    public void StopAllSanityEffectsByType(bool isBuff = true)
        => StopAllEffectsByType(SanityEffectRoutines, isBuff);

    public IEnumerator ApplySanityEffectRoutine(string effectName, float sanityAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            sanityAmount,
            timeBuffEffect,
            tickCooldown,
            isProcedural,
            isStackable,
            perTick => ChangeSanity(perTick),
            total => ChangeSanity(total),
            _ => currentValue = Mathf.Clamp(currentValue, 0f, maxValue)
        );
    }

    public IEnumerator ApplySanityHealFactorEffectRoutine(string effectName, float sanityHealFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        // Added while it lasts and taken away exactly when it ends, is replaced or removed.
        => ModifierEffect(effectName, sanityHealFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, v => incrementFactor += v);

    public IEnumerator ApplySanityDamageFactorEffectRoutine(string effectName, float sanityDamageFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => ModifierEffect(effectName, sanityDamageFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, v => decrementFactor += v);

    // ------------------------------------------------------------------ defaults

    private static List<SanityStage> DefaultStages() => new List<SanityStage>
    {
        new SanityStage
        {
            belowPercent = 70f, screenEffect = 0.12f,
            effect = new StatusConsequence("Uneasy")
            {
                combatStats = new List<CombatStatModifier> { new CombatStatModifier(CombatStatType.CriticalChance, -2f) },
            },
        },
        new SanityStage
        {
            belowPercent = 40f, screenEffect = 0.3f,
            effect = new StatusConsequence("Paranoid")
            {
                combatStats = new List<CombatStatModifier> { new CombatStatModifier(CombatStatType.CriticalChance, -5f) },
                characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.StaminaRegen, -15f), TraitModifier.Percent(TraitStat.AbilityCastTime, 10f) },
            },
        },
        new SanityStage
        {
            belowPercent = 20f, screenEffect = 0.6f,
            effect = new StatusConsequence("Hysteric")
            {
                combatStats = new List<CombatStatModifier> { new CombatStatModifier(CombatStatType.WeaponDamage, -10f) },
                characterStats = new List<TraitModifier>
                {
                    TraitModifier.Percent(TraitStat.MoveSpeed, -10f), TraitModifier.Percent(TraitStat.AbilityCastTime, 20f),
                    TraitModifier.Percent(TraitStat.DamageTaken, 10f),
                },
                hungerDrain = 1.2f,
                stunChancePerMinute = 0.3f, stunSeconds = 1.5f,
            },
        },
        new SanityStage
        {
            belowPercent = 5f, screenEffect = 1f,
            effect = new StatusConsequence("Broken Mind")
            {
                combatStats = new List<CombatStatModifier> { new CombatStatModifier(CombatStatType.WeaponDamage, -20f) },
                characterStats = new List<TraitModifier>
                {
                    TraitModifier.Percent(TraitStat.MoveSpeed, -20f), TraitModifier.Percent(TraitStat.AbilityCastTime, 30f),
                    TraitModifier.Percent(TraitStat.DamageTaken, 25f),
                },
                healthPerSecond = -0.5f,
                stunChancePerMinute = 0.5f, stunSeconds = 2f,
            },
        },
    };

    [ContextMenu("Reset Stages To Defaults")]
    private void ResetStages() => stages = DefaultStages();
}
