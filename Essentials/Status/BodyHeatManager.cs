using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// <b>Body temperature</b> on a 0-100 scale (about 1.6 points per °C; 50 = the Reference Temperature, 18 °C).
/// <list type="bullet">
/// <item>The body moves toward the air temperature it feels (the weather sets it through Player Weather Effects; any
/// script with <see cref="SetEnvironmentalTemperature"/> or <see cref="SetAirTemperature"/>).</item>
/// <item>Each character has an <b>ideal temperature</b> and a <b>comfortable range</b> around it - its race's or class's
/// (Character Archetype ▸ Survival ▸ Temperature), else the defaults here. A desert race is comfortable where a human is
/// too hot, and chilly where a human is fine.</item>
/// <item><b>Heat / Cold Resistance</b> (combat stats from races, classes, traits, equipment, abilities, buffs) reduce how far
/// the body follows the heat / the cold: 50% = half the effect, <b>100% = immune</b> (the body stays at its ideal and no
/// effect of that side applies). Negative = a weakness.</item>
/// <item>Outside the comfortable range the <see cref="effects"/> apply (Chilly, Freezing, Hypothermia; Warm, Overheated,
/// Heatstroke by default): debuffs, health loss, faster hunger in the cold and thirst in the heat, shivering / fainting.
/// A race / class can have its own effects.</item>
/// </list>
/// The weather's own temperature statuses (Chilled, Heatstroke) are skipped for characters with this component, so cold
/// and heat are never penalised twice (Player Weather Effects ▸ Body Heat Replaces Temperature Rules).
/// </summary>
public class BodyHeatManager : StatusManager
{
    [Header("Scale (°C ↔ points)")]
    [Tooltip("Air temperature (°C) that is 50 points on the body-heat scale. Player Weather Effects sets it to its Comfort Temperature.")]
    [SerializeField] private float referenceCelsius = 18f;
    [Tooltip("Body-heat points per °C. Player Weather Effects sets it to its Points Per Degree.")]
    [SerializeField, Min(0.1f)] private float pointsPerDegree = 1.6f;

    [Header("Ideal temperature (default; a race / class profile replaces it)")]
    [Tooltip("Air temperature (°C) the body feels best at.")]
    [SerializeField] private float idealCelsius = 18f;
    [Tooltip("The body is comfortable this many °C around the ideal.")]
    [SerializeField, Min(0f)] private float toleranceCelsius = 8f;
    [Tooltip("Use the race / class temperature profile of the character (Character Identity) when it has one.")]
    [SerializeField] private bool useArchetypeProfile = true;

    [Header("Change")]
    [Tooltip("Body-heat points per second the body moves toward the felt temperature.")]
    [SerializeField, Min(0f)] private float temperatureChangeRate = 1f;
    [Tooltip("The air temperature the body moves toward (points). Set by the weather or your scripts.")]
    [SerializeField] private float environmentalTemperature = 50f;
    [Tooltip("While nothing sets the air temperature (no weather: the City, dungeons, interiors) the body rests at its own " +
             "ideal temperature, so a desert race is not cold in a weatherless scene. Off = it moves toward Environmental Temperature.")]
    [SerializeField] private bool neutralWithoutEnvironment = true;

    [Header("Effects (default; a race / class profile can replace them)")]
    [Tooltip("What happens outside the comfortable range. One effect of each side at a time: the furthest one reached.")]
    [SerializeField] private List<TemperatureTier> effects = DefaultEffects();

    [Header("UI Settings")]
    [Tooltip("Show the bar even while comfortable.")]
    [SerializeField] private bool showUIWhenNormal = false;

    private bool started;
    private List<CoroutineInfo> bodyHeatEffectRoutines = new List<CoroutineInfo>();
    private ConsequenceRunner runner;
    private int tierIndex = -1;
    private CombatEntity entity;
    private CharacterIdentity identity;
    private TemperatureProfile profile;
    private bool profileDirty = true;
    private bool hasEnvironment;
    private float nextIdentityCheck;

    public TemperatureLevel CurrentTemperatureLevel => GetTemperatureLevel();
    public float EnvironmentalTemperature
    {
        get => environmentalTemperature;
        set
        {
            if (IsReplica)
                return; // the server's temperature arrives through replication
            environmentalTemperature = value;
        }
    }
    public List<CoroutineInfo> BodyHeatEffectRoutines { get => bodyHeatEffectRoutines; set => bodyHeatEffectRoutines = value; }

    public enum TemperatureLevel
    {
        ExtremelyCold,
        Cold,
        Normal,
        Hot,
        ExtremelyHot
    }

    // ------------------------------------------------------------------ scale & profile
    /// <summary>°C of a body-heat value.</summary>
    public float ToCelsius(float points) => referenceCelsius + (points - 50f) / pointsPerDegree;
    /// <summary>Body-heat points of a °C value.</summary>
    public float ToPoints(float celsius) => 50f + (celsius - referenceCelsius) * pointsPerDegree;
    /// <summary>The body temperature as °C of air it corresponds to.</summary>
    public float CurrentCelsius => ToCelsius(currentValue);
    /// <summary>The air temperature it moves toward, in °C.</summary>
    public float EnvironmentCelsius => ToCelsius(environmentalTemperature);

    /// <summary>The ideal temperature in use (°C): the race / class profile's, else the default.</summary>
    public float IdealCelsius => Profile != null && Profile.customTemperature ? Profile.idealCelsius : idealCelsius;
    /// <summary>Comfortable °C around the ideal.</summary>
    public float ToleranceCelsius => Profile != null && Profile.customTemperature ? Profile.toleranceCelsius : toleranceCelsius;
    public float IdealPoints => ToPoints(IdealCelsius);
    public float ComfortLowPoints => ToPoints(IdealCelsius - ToleranceCelsius);
    public float ComfortHighPoints => ToPoints(IdealCelsius + ToleranceCelsius);

    /// <summary>The effects in use (the profile's when it has its own).</summary>
    public IReadOnlyList<TemperatureTier> Effects => Profile != null && Profile.customEffects && Profile.effects != null ? Profile.effects : effects;

    /// <summary>Does this component apply cold / heat effects (the weather's temperature statuses are then skipped)?</summary>
    public bool HandlesTemperatureEffects => isActiveAndEnabled && Effects.Count > 0;

    /// <summary>Cold Resistance (percent, -100..100; 100 = immune).</summary>
    public float ColdResistance => Stat(CombatStatType.ColdResistance);
    /// <summary>Heat Resistance (percent, -100..100; 100 = immune).</summary>
    public float HeatResistance => Stat(CombatStatType.HeatResistance);
    public bool ImmuneToCold => ColdResistance >= 99.99f;
    public bool ImmuneToHeat => HeatResistance >= 99.99f;

    private float Stat(CombatStatType t)
    {
        CombatEntity e = Entity;
        return e != null && e.Stats != null ? Mathf.Clamp(e.Stats.Get(t), -100f, 100f) : 0f;
    }

    private CombatEntity Entity => entity != null ? entity : (entity = GetComponentInParent<CombatEntity>() ?? GetComponentInChildren<CombatEntity>());

    /// <summary>The race / class temperature profile in use (null = the defaults).</summary>
    public TemperatureProfile Profile
    {
        get
        {
            if (profileDirty) FindProfile();
            return profile;
        }
    }

    private void FindProfile()
    {
        profileDirty = false;
        profile = null;
        if (!useArchetypeProfile) return;
        if (identity == null)
        {
            // No identity yet (added later, or none): look for it again every 2 seconds.
            if (Time.time < nextIdentityCheck)
            {
                profileDirty = true;
                return;
            }
            nextIdentityCheck = Time.time + 2f;
            identity = CharacterIdentity.For(this, false);
            if (identity != null) identity.Changed += () => profileDirty = true;
        }
        if (identity == null)
        {
            profileDirty = true;
            return;
        }
        IList<CharacterArchetype> list = identity.Archetypes;
        if (list == null) return;
        // Races first (biology), then classes, then the rest.
        foreach (ArchetypeKind kind in new[] { ArchetypeKind.Race, ArchetypeKind.Class, ArchetypeKind.Background, ArchetypeKind.Other })
            foreach (CharacterArchetype a in list)
                if (a != null && a.kind == kind && a.temperature != null && a.temperature.IsUsed)
                {
                    profile = a.temperature;
                    return;
                }
    }

    /// <summary>Uses another °C ↔ points scale (Player Weather Effects passes its own).</summary>
    public void SetScale(float referenceTemperature, float pointsPerCelsius)
    {
        referenceCelsius = referenceTemperature;
        pointsPerDegree = Mathf.Max(0.1f, pointsPerCelsius);
    }

    private void Awake()
    {
        runner = new ConsequenceRunner(this, "Temperature");
    }

    private void Start()
    {
        started = true;
        // A client's copy keeps the server's value (the server simulates body heat).
        if (!IsReplica)
            currentValue = IdealPoints;
        UpdateUIVisibility();
    }

    private void OnDisable() => runner?.Clear();

    protected override void OnReplicaChanged(bool on)
    {
        runner?.Clear();
        tierIndex = -1;
    }

    /// <summary>Client copies: the environmental temperature the server uses (tooltips, UI). Ignored where the rules run.</summary>
    public void ApplyReplicatedEnvironment(float temperature)
    {
        if (!IsReplica || float.IsNaN(temperature) || float.IsInfinity(temperature))
            return;
        environmentalTemperature = temperature;
    }

    private void Update()
    {
        UpdateBodyHeatBar();
        if (IsReplica)
            return;
        float dt = Time.deltaTime;
        CombatEntity e = Entity;
        if (e != null && e.IsDead)
        {
            runner.Clear();
            tierIndex = -1;
            return;
        }

        // The body follows the felt temperature: resistance keeps it closer to the ideal (immune = stays there).
        float ideal = IdealPoints;
        float env = hasEnvironment || !neutralWithoutEnvironment ? environmentalTemperature : ideal;
        float resist = env < ideal ? ColdResistance : HeatResistance;
        float target = ideal + (env - ideal) * Mathf.Clamp(1f - resist / 100f, 0f, 2f);
        float speed = temperatureChangeRate * (Profile != null && Profile.customTemperature ? Profile.adaptSpeed : 1f);
        currentValue = Mathf.Clamp(Mathf.MoveTowards(currentValue, target, speed * dt), 0f, maxValue);
        // Immunity also holds against direct changes (a frost breath on a fire elemental).
        if (ImmuneToCold && currentValue < ideal) currentValue = Mathf.MoveTowards(currentValue, ideal, Mathf.Max(speed, 5f) * dt);
        if (ImmuneToHeat && currentValue > ideal) currentValue = Mathf.MoveTowards(currentValue, ideal, Mathf.Max(speed, 5f) * dt);

        int tier = FindTier();
        if (tier != tierIndex)
        {
            tierIndex = tier;
            IReadOnlyList<TemperatureTier> list = Effects;
            runner.Set(tier >= 0 && tier < list.Count ? list[tier].effect : null);
        }
        runner.Tick(dt);
    }

    /// <summary>The furthest effect reached (-1 = comfortable).</summary>
    private int FindTier()
    {
        IReadOnlyList<TemperatureTier> list = Effects;
        float low = ComfortLowPoints, high = ComfortHighPoints;
        bool cold = currentValue < low && !ImmuneToCold;
        bool hot = currentValue > high && !ImmuneToHeat;
        if (!cold && !hot) return -1;
        float beyond = cold ? (low - currentValue) / pointsPerDegree : (currentValue - high) / pointsPerDegree;
        TemperatureSide side = cold ? TemperatureSide.Cold : TemperatureSide.Hot;
        int best = -1;
        float bestAt = -1f;
        for (int i = 0; i < list.Count; i++)
        {
            TemperatureTier t = list[i];
            if (t == null || t.side != side || beyond < t.degreesBeyondComfort) continue;
            if (t.degreesBeyondComfort > bestAt) { bestAt = t.degreesBeyondComfort; best = i; }
        }
        return best;
    }

    /// <summary>The running temperature effect (null = comfortable).</summary>
    public TemperatureTier CurrentEffect
    {
        get
        {
            IReadOnlyList<TemperatureTier> list = Effects;
            return tierIndex >= 0 && tierIndex < list.Count ? list[tierIndex] : null;
        }
    }

    public void UpdateBodyHeatBar()
    {
        UpdateBar(currentValue, maxValue, uiImage);
        currentValue = Mathf.Clamp(currentValue, 0, maxValue);
        UpdateUIVisibility();
    }

    private void UpdateUIVisibility()
    {
        if (uiImage == null) return;
        TemperatureLevel level = GetTemperatureLevel();
        bool shouldShowUI = showUIWhenNormal || level != TemperatureLevel.Normal;
        if (uiImage.gameObject.activeSelf != shouldShowUI)
            uiImage.gameObject.SetActive(shouldShowUI);
    }

    public TemperatureLevel GetTemperatureLevel()
    {
        float low = ComfortLowPoints, high = ComfortHighPoints;
        if (currentValue >= low && currentValue <= high) return TemperatureLevel.Normal;
        bool cold = currentValue < low;
        int tier = FindTier();
        if (tier < 0) return cold ? TemperatureLevel.Cold : TemperatureLevel.Hot; // outside, but no effect (immune or no tier)
        // The first effect of a side is "Cold / Hot", deeper ones "Extremely".
        IReadOnlyList<TemperatureTier> list = Effects;
        float at = list[tier].degreesBeyondComfort;
        bool deeper = false;
        foreach (TemperatureTier t in list)
            if (t != null && t.side == list[tier].side && t.degreesBeyondComfort < at) { deeper = true; break; }
        if (cold) return deeper ? TemperatureLevel.ExtremelyCold : TemperatureLevel.Cold;
        return deeper ? TemperatureLevel.ExtremelyHot : TemperatureLevel.Hot;
    }

    /// <summary>Changes the body temperature directly by <paramref name="amount"/> points (not resisted).</summary>
    public void ModifyBodyHeat(float amount)
    {
        if (IsReplica) return;
        currentValue = Mathf.Clamp(currentValue + amount, 0, maxValue);
    }

    /// <summary>
    /// Changes the body temperature by <paramref name="amount"/> points like the weather or an ability would: a change
    /// away from the ideal is reduced by Cold / Heat Resistance (immune = nothing), a change back toward it is not.
    /// </summary>
    public void ApplyHeatChange(float amount)
    {
        if (IsReplica || amount == 0f) return;
        float ideal = IdealPoints;
        bool away = amount < 0f ? currentValue <= ideal : currentValue >= ideal;
        if (away)
        {
            float resist = amount < 0f ? ColdResistance : HeatResistance;
            amount *= Mathf.Clamp(1f - resist / 100f, 0f, 2f);
        }
        currentValue = Mathf.Clamp(currentValue + amount, 0, maxValue);
    }

    /// <summary>Sets the air temperature the body moves toward, in body-heat points (50 = the Reference Temperature).</summary>
    public void SetEnvironmentalTemperature(float temperature)
    {
        if (IsReplica) return;
        environmentalTemperature = Mathf.Clamp(temperature, 0, maxValue);
        hasEnvironment = true;
    }

    /// <summary>Nothing sets the air temperature any more (left the weather): the body returns to its ideal temperature.</summary>
    public void ClearEnvironment()
    {
        if (IsReplica) return;
        hasEnvironment = false;
        environmentalTemperature = IdealPoints;
    }

    /// <summary>Is something (the weather, a script) setting the air temperature?</summary>
    public bool HasEnvironment => hasEnvironment;

    /// <summary>Sets the air temperature the body moves toward, in °C.</summary>
    public void SetAirTemperature(float celsius) => SetEnvironmentalTemperature(ToPoints(celsius));

    public override void ResetForRespawn(float fraction)
    {
        if (IsReplica) return;
        currentValue = IdealPoints; // a respawn brings the body back to its ideal temperature
        runner?.Clear();
        tierIndex = -1;
    }

    protected override void UpdateStatus() => UpdateBar();
    protected override float AddFactor(float amount) => ApplyFactor(amount, incrementFactor);
    protected override float RemoveFactor(float amount) => ApplyFactor(amount, decrementFactor);

    // Effect methods
    public void AddBodyHeatEffect(string effectName, float heatAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(BodyHeatEffectRoutines, effectName, heatAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyBodyHeatEffectRoutine);

    public void RemoveBodyHeatEffect(string effectName = null, Coroutine effectRoutine = null)
        => RemoveEffect(BodyHeatEffectRoutines, effectName, effectRoutine);

    public void StopAllBodyHeatEffects()
        => StopAllEffects(BodyHeatEffectRoutines);

    public void StopAllBodyHeatEffectsByType(bool isBuff = true)
        => StopAllEffectsByType(BodyHeatEffectRoutines, isBuff);

    public IEnumerator ApplyBodyHeatEffectRoutine(string effectName, float heatAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            heatAmount,
            timeBuffEffect,
            tickCooldown,
            isProcedural,
            isStackable,
            perTick => ApplyHeatChange(perTick),
            total => ApplyHeatChange(total),
            _ => currentValue = Mathf.Clamp(currentValue, 0, maxValue)
        );
    }

    // ------------------------------------------------------------------ defaults

    /// <summary>The default temperature effects (copied into new components and new race profiles).</summary>
    public static List<TemperatureTier> DefaultEffects() => new List<TemperatureTier>
    {
        new TemperatureTier
        {
            side = TemperatureSide.Cold, degreesBeyondComfort = 0f,
            effect = new StatusConsequence("Chilly") { element = ElementType.Ice, hungerDrain = 1.3f,
                characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MoveSpeed, -5f), TraitModifier.Percent(TraitStat.StaminaRegen, -10f) } },
        },
        new TemperatureTier
        {
            side = TemperatureSide.Cold, degreesBeyondComfort = 8f,
            effect = new StatusConsequence("Freezing") { element = ElementType.Ice, hungerDrain = 1.6f, healthPerSecond = -0.5f,
                stunChancePerMinute = 0.15f, stunSeconds = 1f,
                combatStats = new List<CombatStatModifier> { new CombatStatModifier(CombatStatType.AttackSpeed, -10f) },
                characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MoveSpeed, -15f), TraitModifier.Percent(TraitStat.StaminaRegen, -25f) } },
        },
        new TemperatureTier
        {
            side = TemperatureSide.Cold, degreesBeyondComfort = 16f,
            effect = new StatusConsequence("Hypothermia") { element = ElementType.Ice, hungerDrain = 2f, healthPerSecond = -2f,
                stunChancePerMinute = 0.3f, stunSeconds = 1.5f,
                combatStats = new List<CombatStatModifier> { new CombatStatModifier(CombatStatType.AttackSpeed, -20f) },
                characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MoveSpeed, -30f), TraitModifier.Percent(TraitStat.StaminaRegen, -40f) } },
        },
        new TemperatureTier
        {
            side = TemperatureSide.Hot, degreesBeyondComfort = 0f,
            effect = new StatusConsequence("Warm") { element = ElementType.Fire, thirstDrain = 1.4f,
                characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.StaminaCost, 10f) } },
        },
        new TemperatureTier
        {
            side = TemperatureSide.Hot, degreesBeyondComfort = 8f,
            effect = new StatusConsequence("Overheated") { element = ElementType.Fire, thirstDrain = 1.8f, healthPerSecond = -0.5f,
                characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.StaminaRegen, -20f), TraitModifier.Percent(TraitStat.StaminaCost, 20f) } },
        },
        new TemperatureTier
        {
            side = TemperatureSide.Hot, degreesBeyondComfort = 16f,
            effect = new StatusConsequence("Heat Exhaustion") { element = ElementType.Fire, thirstDrain = 2.5f, healthPerSecond = -2f,
                stunChancePerMinute = 0.3f, stunSeconds = 2f,
                characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MoveSpeed, -20f), TraitModifier.Percent(TraitStat.StaminaRegen, -35f) } },
        },
    };

    [ContextMenu("Reset Effects To Defaults")]
    private void ResetEffects() => effects = DefaultEffects();
}
