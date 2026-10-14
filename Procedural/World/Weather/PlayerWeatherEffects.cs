using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>A status bar a weather condition can drain or fill.</summary>
public enum WeatherStatusBar
{
    Health,
    Stamina,
    Mana,
    Hunger,
    Thirst,
    Oxygen,
    Sanity,
    Sleep,
    BodyHeat,
}

/// <summary>Drains (negative) or fills (positive) one status bar per second while a weather condition lasts.</summary>
[Serializable]
public class WeatherStatusRate
{
    [Tooltip("Which bar changes.")]
    public WeatherStatusBar bar = WeatherStatusBar.Thirst;
    [Tooltip("Points per second while the condition lasts: negative drains (heat makes you thirsty), positive fills.")]
    public float perSecond = -0.5f;
}

/// <summary>A player event of a weather condition (the player's root object is passed).</summary>
[Serializable]
public class WeatherPlayerEvent : UnityEvent<GameObject> { }

/// <summary>What a <see cref="WeatherExposureRule"/> looks at.</summary>
public enum WeatherMeasure
{
    /// <summary>Rain (0-1).</summary>
    Rain,
    /// <summary>Snowfall (0-1).</summary>
    Snow,
    /// <summary>Hail (0-1).</summary>
    Hail,
    /// <summary>Thunderstorm strength (0-1).</summary>
    Thunder,
    /// <summary>Sand and dust in the air (0-1).</summary>
    Dust,
    /// <summary>Fog (0-1).</summary>
    Fog,
    /// <summary>Heat wave (0-1).</summary>
    Heat,
    /// <summary>Tornado strength (0-1).</summary>
    Tornado,
    /// <summary>Wind speed in m/s (Threshold is in m/s: 14 = a strong wind).</summary>
    WindSpeed,
    /// <summary>The air is at or below Threshold °C.</summary>
    ColderThan,
    /// <summary>The air is at or above Threshold °C.</summary>
    HotterThan,
    /// <summary>The kind of weather is one of Weathers (Blizzard, Sandstorm...).</summary>
    WeatherIs,
}

/// <summary>
/// One way the weather affects players: when <see cref="measure"/> passes <see cref="threshold"/> where the player
/// stands, the player gets a status (stats, resistances, damage over time, body heat) that lasts while it is true
/// (and <see cref="lingerSeconds"/> after).
/// </summary>
[Serializable]
public class WeatherExposureRule
{
    [Tooltip("Off = this rule is ignored.")]
    public bool enabled = true;
    [Tooltip("Status name shown on the status icons (Soaked, Chilled, Heatstroke...).")]
    public string label = "Weather";
    [Tooltip("Shown as a debuff (cleanses remove it until the weather applies it again).")]
    public bool debuff = true;
    [Tooltip("Element of the status (for element-limited stats and the icon colour).")]
    public ElementType element = ElementType.None;

    [Header("When")]
    [Tooltip("What the rule looks at in the weather at the player's position.")]
    public WeatherMeasure measure = WeatherMeasure.Rain;
    [Tooltip("Rain/Snow/Hail/Thunder/Dust/Fog/Heat/Tornado: 0-1 amount it starts at. Wind Speed: m/s. Colder Than / Hotter Than: °C.")]
    public float threshold = 0.35f;
    [Tooltip("Weather Is: the kinds of weather that apply.")]
    public List<WeatherType> weathers = new List<WeatherType>();
    [Tooltip("Only under the open sky: a roof, a cave or a tree canopy above the player protects (rain, snow, hail, sun, sand). " +
             "Off for cold or wind, which reach you anyway.")]
    public bool needsOpenSky = true;
    [Tooltip("Not while the player's head is under water (rain, sand, heat...).")]
    public bool notUnderwater = true;
    [Tooltip("Stronger weather = a stronger status (Strength At Threshold when it just starts, full strength at Full Strength At).")]
    public bool scaleWithIntensity = true;
    [Tooltip("With Scale With Intensity: how strong the status is when the weather just reaches the threshold (0.5 = half).")]
    [Range(0f, 1f)] public float strengthAtThreshold = 0.5f;
    [Tooltip("With Scale With Intensity: set where the status reaches full strength yourself (below). Off = automatic: amount 1, " +
             "twice the wind threshold, 15 °C colder or 10 °C hotter than the threshold.")]
    public bool customFullStrength;
    [Tooltip("The value (same units as Threshold) at which the status is at full strength.")]
    public float fullStrengthAt = 1f;
    [Tooltip("Seconds the status stays after the weather stops (wet clothes dry slowly). 0 = it ends with the weather.")]
    [Min(0f)] public float lingerSeconds;

    [Header("What it does")]
    [Tooltip("Combat stats changed while it lasts.")]
    public List<CombatStatModifier> combatStats = new List<CombatStatModifier>();
    [Tooltip("Elemental resistances changed while it lasts (Soaked: weaker to Lightning, stronger against Fire).")]
    public List<ElementalResistance> resistances = new List<ElementalResistance>();
    [Tooltip("Character stats changed while it lasts (Move Speed, Stamina Regen, Stamina Cost, Health Regen...).")]
    public List<TraitModifier> characterStats = new List<TraitModifier>();
    [Tooltip("Damage per second while it lasts (hail, sand, frostbite). 0 = none.")]
    [Min(0f)] public float damagePerSecond;
    [Tooltip("How the damage is resisted (Physical by Defense, Magical by Magic Resistance, True by neither).")]
    public DamageType damageType = DamageType.Physical;
    [Tooltip("Element of the damage (Ice for frostbite, Earth for sand...).")]
    public ElementType damageElement = ElementType.None;
    [Tooltip("Shifts the air temperature the player's Body Heat moves toward, in body-heat points (0-100 scale; 50 is " +
             "normal): Soaked -8 makes the rain feel colder. 0 = none.")]
    public float bodyHeatShift;
    [Tooltip("Status bars drained or filled per second while it lasts (Thirst -0.5 in a heat wave, Stamina -1 in a blizzard...). " +
             "Scaled by the strength like everything else.")]
    public List<WeatherStatusRate> statusRates = new List<WeatherStatusRate>();
    [Tooltip("Optional effect attached to the player while it lasts (dripping water, frost breath...).")]
    public GameObject attachedVfx;

    [Header("Events (where the game rules run: offline or on the server)")]
    [Tooltip("The condition started on a player (the player's root object). Hook your own effects here: a sound, a quest, an achievement.")]
    public WeatherPlayerEvent onStart = new WeatherPlayerEvent();
    [Tooltip("The condition ended on a player.")]
    public WeatherPlayerEvent onEnd = new WeatherPlayerEvent();

    /// <summary>Is the rule true for <paramref name="w"/>, and how strongly (0-1)?</summary>
    public bool Matches(in WeatherState w, out float intensity)
    {
        intensity = 0f;
        switch (measure)
        {
            case WeatherMeasure.Rain: return Amount(w.Rain, out intensity);
            case WeatherMeasure.Snow: return Amount(w.Snow, out intensity);
            case WeatherMeasure.Hail: return Amount(w.Hail, out intensity);
            case WeatherMeasure.Thunder: return Amount(w.Thunder, out intensity);
            case WeatherMeasure.Dust: return Amount(w.Dust, out intensity);
            case WeatherMeasure.Fog: return Amount(w.Fog, out intensity);
            case WeatherMeasure.Heat: return Amount(w.Heat, out intensity);
            case WeatherMeasure.Tornado: return Amount(w.Tornado, out intensity);
            case WeatherMeasure.WindSpeed:
                if (w.WindSpeed < threshold) return false;
                intensity = Mathf.InverseLerp(threshold, FullAt(Mathf.Max(threshold * 2f, threshold + 1f)), w.WindSpeed);
                return true;
            case WeatherMeasure.ColderThan:
                if (w.Temperature > threshold) return false;
                intensity = Mathf.InverseLerp(threshold, FullAt(threshold - 15f), w.Temperature);
                return true;
            case WeatherMeasure.HotterThan:
                if (w.Temperature < threshold) return false;
                intensity = Mathf.InverseLerp(threshold, FullAt(threshold + 10f), w.Temperature);
                return true;
            case WeatherMeasure.WeatherIs:
                if (weathers == null || !weathers.Contains(w.Classify())) return false;
                intensity = 1f;
                return true;
            default:
                return false;
        }
    }

    private bool Amount(float value, out float intensity)
    {
        float t = Mathf.Clamp(threshold, 0.001f, 0.999f);
        intensity = Mathf.InverseLerp(t, FullAt(1f), value);
        return value >= t;
    }

    private float FullAt(float automatic) => customFullStrength ? fullStrengthAt : automatic;

    /// <summary>The status strength for an intensity (1 = as authored).</summary>
    public float StrengthFor(float intensity) => scaleWithIntensity ? Mathf.Lerp(strengthAtThreshold, 1f, Mathf.Clamp01(intensity)) : 1f;

    /// <summary>A copy of this rule (the defaults are copied into new assets).</summary>
    public WeatherExposureRule Clone()
    {
        var c = (WeatherExposureRule)MemberwiseClone();
        c.weathers = new List<WeatherType>(weathers ?? new List<WeatherType>());
        c.combatStats = new List<CombatStatModifier>(combatStats ?? new List<CombatStatModifier>());
        c.resistances = new List<ElementalResistance>(resistances ?? new List<ElementalResistance>());
        c.characterStats = new List<TraitModifier>(characterStats ?? new List<TraitModifier>());
        c.statusRates = new List<WeatherStatusRate>(statusRates ?? new List<WeatherStatusRate>());
        c.onStart = new WeatherPlayerEvent();
        c.onEnd = new WeatherPlayerEvent();
        return c;
    }
}

/// <summary>
/// The weather affects the players: every player gets the weather <b>where that player stands</b> (not the weather
/// of the host or of the camera), and from it statuses like Soaked, Chilled, Frostbite, Heatstroke, Sandblasted and
/// Windswept (see <see cref="rules"/>), the air temperature their Body Heat moves toward, and optionally lightning.
/// <para>Put it next to the Weather System (Tools ▸ SimpleMovements ▸ Weather ▸ Add Player Weather Effects). It runs
/// where the game rules run: offline, or on the server/host for every connected player (statuses, stats, damage and
/// body heat then reach the clients through the existing replication). Clients only see and hear the weather.</para>
/// <para>Only players in the Weather System's world count: players in the City, in a dungeon or travelling get
/// nothing (and their statuses end). A roof, cave or canopy above the player shelters them from rules that need the
/// open sky; a head under water skips rules marked Not Underwater.</para>
/// </summary>
[AddComponentMenu("SimpleMovements/Player Weather Effects")]
[DisallowMultipleComponent]
public class PlayerWeatherEffects : MonoBehaviour
{
    [Header("General")]
    [Tooltip("The weather to read. Empty = the Weather System on this object, else the scene's.")]
    [SerializeField] private WeatherSystem weatherSystem;
    [Tooltip("Seconds between checks of each player (spread out between players).")]
    [Min(0.25f)] public float checkInterval = 2f;
    [Tooltip("Statuses last this many seconds and are renewed while the weather goes on, so they end soon after it stops " +
             "even when nothing removes them (more than Check Interval).")]
    [Min(1f)] public float statusDuration = 8f;

    [Header("Shelter")]
    [Tooltip("Layers that shelter a player from above (roofs, rocks, the terrain of a cave, trees). Exclude the player layer.")]
    public LayerMask shelterLayers = ~0;
    [Tooltip("How far above the head to look for a roof (meters).")]
    [Min(1f)] public float shelterCheckHeight = 40f;

    [Header("Body Heat")]
    [Tooltip("Set the air temperature each player's Body Heat moves toward from the weather (°C turned into the 0-100 body-heat " +
             "scale, with wind chill and wetness). Off = nothing changes the Body Heat's environment temperature.")]
    public bool driveBodyHeat = true;
    [Tooltip("Air temperature (°C) that feels normal (body heat 50).")]
    public float comfortTemperature = 18f;
    [Tooltip("Body-heat points per °C away from Comfort Temperature (1.6: 5 °C = cold at 30, -7 °C = extremely cold at 10, " +
             "31 °C = hot at 70, 43 °C = extremely hot at 90).")]
    [Min(0.1f)] public float pointsPerDegree = 1.6f;
    [Tooltip("Degrees colder per m/s of wind when the air is below Comfort Temperature (wind chill), under the open sky.")]
    [Min(0f)] public float windChillPerMeterPerSecond = 0.35f;
    [Tooltip("Shelter brings the felt temperature this much of the way back to Comfort Temperature (0.3 = a roof keeps some cold out).")]
    [Range(0f, 1f)] public float shelterComfort = 0.3f;

    [Header("Lightning")]
    [Tooltip("Lightning can strike players standing under the open sky in a thunderstorm.")]
    public bool lightningStrikes;
    [Tooltip("Chance per minute that a player under the open sky in the strongest thunderstorm is struck (scaled by the storm).")]
    [Range(0f, 1f)] public float strikeChancePerMinute = 0.03f;
    [Tooltip("Damage of a strike (Lightning element, Magical).")]
    [Min(0f)] public float strikeDamage = 40f;

    [Header("Conditions")]
    [Tooltip("Weather Condition assets (Create ▸ SimpleMovements ▸ Weather ▸ Weather Condition, or Tools ▸ SimpleMovements ▸ " +
             "Weather ▸ Create Default Weather Condition Assets): each one is a condition you edit in its own asset and can " +
             "share between scenes.")]
    public List<WeatherCondition> conditions = new List<WeatherCondition>();
    [Tooltip("Conditions written here directly (the defaults: Soaked, Chilled, Frostbite, Heatstroke, Sandblasted, Windswept, " +
             "Pelted by Hail). Edit, remove or add any; both lists are used.")]
    public List<WeatherExposureRule> rules = DefaultRules();

    [Header("Debug")]
    public bool logChanges;

    /// <summary>A player started or stopped being affected by a rule (player, rule, now affected).</summary>
    public event Action<CombatEntity, WeatherExposureRule, bool> StatusChanged;

    /// <summary>A player was struck by lightning (player, damage dealt).</summary>
    public event Action<CombatEntity, float> PlayerStruck;

    private sealed class Tracked
    {
        public CombatEntity Entity;
        public PlayerMovementModel Movement;
        public BodyHeatManager BodyHeat;
        public CharacterController Controller;
        public PlayerStatusController Status;
        public float NextCheck;
        public bool HeatManaged;
        public bool Seen;
        /// <summary>Rules on this player right now, with the strength they were applied at.</summary>
        public readonly Dictionary<WeatherExposureRule, float> Active = new Dictionary<WeatherExposureRule, float>();
        public WeatherState LastWeather;
        public bool Sheltered;
    }

    private readonly Dictionary<CombatEntity, Tracked> tracked = new Dictionary<CombatEntity, Tracked>();
    private readonly List<CombatEntity> gone = new List<CombatEntity>();
    private int spread;

    /// <summary>The weather this component reads.</summary>
    public WeatherSystem Weather
    {
        get
        {
            if (weatherSystem == null)
                weatherSystem = GetComponent<WeatherSystem>();
            return weatherSystem != null ? weatherSystem : WeatherSystem.Instance;
        }
    }

    /// <summary>The weather where <paramref name="player"/> stands, as this component last saw it (false if not tracked).</summary>
    public bool TryGetPlayerWeather(CombatEntity player, out WeatherState weather, out bool sheltered)
    {
        weather = default;
        sheltered = false;
        if (player == null || !tracked.TryGetValue(player, out Tracked t))
            return false;
        weather = t.LastWeather;
        sheltered = t.Sheltered;
        return true;
    }

    private void OnDisable()
    {
        foreach (Tracked t in tracked.Values)
            Release(t);
        tracked.Clear();
    }

    private void Update()
    {
        WeatherSystem weather = Weather;
        if (weather == null)
            return;
        foreach (Tracked t in tracked.Values)
            t.Seen = false;

        float now = Time.time;
        IReadOnlyList<CombatEntity> players = CombatEntity.Players;
        for (int i = 0; i < players.Count; i++)
        {
            CombatEntity p = players[i];
            if (p == null || !GameNet.NetAuthority.RunsRules(p.gameObject))
                continue; // a client's copy: the server decides its statuses
            if (!tracked.TryGetValue(p, out Tracked t))
            {
                t = new Tracked
                {
                    Entity = p,
                    Movement = p.GetComponentInChildren<PlayerMovementModel>(),
                    BodyHeat = p.GetComponentInChildren<BodyHeatManager>(),
                    Controller = p.GetComponentInChildren<CharacterController>(),
                    NextCheck = now + (spread++ % 8) * checkInterval / 8f,
                };
                tracked[p] = t;
            }
            t.Seen = true;
            if (now < t.NextCheck)
                continue;
            t.NextCheck = now + checkInterval;
            Check(t, weather);
        }

        // Players that left (disconnected, destroyed).
        gone.Clear();
        foreach (KeyValuePair<CombatEntity, Tracked> kv in tracked)
            if (!kv.Value.Seen)
                gone.Add(kv.Key);
        foreach (CombatEntity e in gone)
        {
            if (tracked.TryGetValue(e, out Tracked t))
                Release(t);
            tracked.Remove(e);
        }
    }

    private void Check(Tracked t, WeatherSystem weather)
    {
        CombatEntity p = t.Entity;
        if (p.IsDead || !InWeatherWorld(p, weather))
        {
            // The City, a dungeon, travelling, dead: no weather (statuses end, body heat back to normal).
            Release(t);
            return;
        }

        Vector3 position = p.Position;
        WeatherState w = weather.WeatherAt(position);
        bool underwater = t.Movement != null && t.Movement.HeadUnderwater;
        bool sheltered = IsSheltered(t, position);
        t.LastWeather = w;
        t.Sheltered = sheltered;

        float heatShift = 0f;
        CollectRules();
        foreach (WeatherExposureRule rule in ruleBuffer)
        {
            if (!rule.enabled)
            {
                if (t.Active.ContainsKey(rule))
                    Remove(t, rule, true);
                continue;
            }
            bool exposed = (!rule.needsOpenSky || !sheltered) && (!rule.notUnderwater || !underwater);
            if (exposed && rule.Matches(w, out float intensity))
            {
                float strength = rule.StrengthFor(intensity);
                Apply(t, rule, strength);
                heatShift += rule.bodyHeatShift * strength;
                if (rule.damagePerSecond > 0f)
                    Damage(p, rule.damagePerSecond * checkInterval * strength, rule.damageType, rule.damageElement, position);
                ApplyStatusRates(t, rule, strength);
            }
            else if (t.Active.ContainsKey(rule))
            {
                Remove(t, rule, rule.lingerSeconds <= 0f);
            }
        }

        if (driveBodyHeat && t.BodyHeat != null)
        {
            t.BodyHeat.SetEnvironmentalTemperature(BodyHeatFor(w, sheltered, underwater) + heatShift);
            t.HeatManaged = true;
        }

        if (lightningStrikes && !sheltered && !underwater && w.Thunder > 0.3f && strikeDamage > 0f)
        {
            float chance = strikeChancePerMinute * w.Thunder * checkInterval / 60f;
            if (UnityEngine.Random.value < chance)
            {
                weather.StrikeLightningAt(position);
                float dealt = Damage(p, strikeDamage, DamageType.Magical, ElementType.Lightning, position);
                PlayerStruck?.Invoke(p, dealt);
                if (logChanges)
                    Debug.Log($"[PlayerWeatherEffects] lightning struck {p.name} for {dealt:0}", p);
            }
        }
    }

    /// <summary>The body-heat environment (0-100, 50 = normal) for the weather a player feels.</summary>
    public float BodyHeatFor(in WeatherState w, bool sheltered, bool underwater)
    {
        float felt = w.Temperature;
        if (!sheltered && !underwater && felt < comfortTemperature)
            felt -= w.WindSpeed * windChillPerMeterPerSecond;
        if (sheltered)
            felt = Mathf.Lerp(felt, comfortTemperature, shelterComfort);
        return Mathf.Clamp(50f + (felt - comfortTemperature) * pointsPerDegree, 0f, 100f);
    }

    private bool InWeatherWorld(CombatEntity p, WeatherSystem weather)
    {
        // A player in another realm (the City, a dungeon scene) is not under this weather.
        if (!MobRealms.IsInRealm(weather.gameObject.scene, p.gameObject))
            return false;
        // Offline the World pauses while the player is in a dungeon.
        if (!GameNet.NetAuthority.IsNetworked && WorldSpawnRegistry.Paused)
            return false;
        return true;
    }

    private bool IsSheltered(Tracked t, Vector3 position)
    {
        float head = t.Controller != null ? t.Controller.center.y * t.Controller.transform.lossyScale.y +
                                            t.Controller.height * 0.5f * t.Controller.transform.lossyScale.y : 2f;
        Vector3 from = position + Vector3.up * (head + 0.25f);
        PhysicsScene physics = ScenePhysics.Of(t.Entity.gameObject);
        return physics.Raycast(from, Vector3.up, out RaycastHit hit, shelterCheckHeight, shelterLayers, QueryTriggerInteraction.Ignore)
               && !hit.transform.IsChildOf(t.Entity.transform);
    }

    private readonly List<WeatherExposureRule> ruleBuffer = new List<WeatherExposureRule>();

    /// <summary>Every condition in use: the assets' and the ones written on this component.</summary>
    private void CollectRules()
    {
        ruleBuffer.Clear();
        if (conditions != null)
            foreach (WeatherCondition c in conditions)
                if (c != null && c.rule != null && !ruleBuffer.Contains(c.rule))
                    ruleBuffer.Add(c.rule);
        if (rules != null)
            foreach (WeatherExposureRule r in rules)
                if (r != null && !ruleBuffer.Contains(r))
                    ruleBuffer.Add(r);
    }

    /// <summary>The conditions in use (assets first, then the ones on this component).</summary>
    public IReadOnlyList<WeatherExposureRule> AllRules
    {
        get
        {
            CollectRules();
            return ruleBuffer;
        }
    }

    private void ApplyStatusRates(Tracked t, WeatherExposureRule rule, float strength)
    {
        if (rule.statusRates == null || rule.statusRates.Count == 0)
            return;
        if (t.Status == null)
            t.Status = t.Entity.GetComponentInChildren<PlayerStatusController>();
        if (t.Status == null)
            return;
        foreach (WeatherStatusRate r in rule.statusRates)
        {
            if (r == null || Mathf.Approximately(r.perSecond, 0f))
                continue;
            float amount = r.perSecond * checkInterval * strength;
            if (r.bar == WeatherStatusBar.BodyHeat)
            {
                if (t.Status.BodyHeatManager != null)
                    t.Status.BodyHeatManager.ModifyBodyHeat(amount);
                continue;
            }
            StatusManager bar = Bar(t.Status, r.bar);
            if (bar == null)
                continue;
            if (amount > 0f)
                bar.AddCurrentValue(amount);
            else if (r.bar == WeatherStatusBar.Health && bar is HealthManager hp)
                hp.ConsumeHP(-amount);
            else
                bar.SetCurrentValue(bar.CurrentValue + amount);
        }
    }

    private static StatusManager Bar(PlayerStatusController s, WeatherStatusBar bar)
    {
        switch (bar)
        {
            case WeatherStatusBar.Health: return s.HpManager;
            case WeatherStatusBar.Stamina: return s.StaminaManager;
            case WeatherStatusBar.Mana: return s.ManaManager;
            case WeatherStatusBar.Hunger: return s.HungerManager;
            case WeatherStatusBar.Thirst: return s.ThirstManager;
            case WeatherStatusBar.Oxygen: return s.OxygenManager;
            case WeatherStatusBar.Sanity: return s.SanityManager;
            case WeatherStatusBar.Sleep: return s.SleepManager;
            default: return null;
        }
    }

    private static GameObject RootOf(CombatEntity e)
    {
        if (e == null)
            return null;
        GameObject root = PlayerLocator.MovableRoot(e);
        return root != null ? root : e.gameObject;
    }

    private void Apply(Tracked t, WeatherExposureRule rule, float strength)
    {
        TimedStatModifiers mods = TimedStatModifiers.For(t.Entity);
        float duration = Mathf.Max(statusDuration, checkInterval * 2f) + rule.lingerSeconds;
        bool had = t.Active.TryGetValue(rule, out float was);
        // Renew only when it is half run out or its strength changed: no needless re-applying every check.
        if (had && Mathf.Abs(was - strength) < 0.15f && mods != null)
        {
            foreach (TimedStatModifiers.Active a in mods.All)
                if (Equals(a.key, rule) && a.Remaining > duration * 0.5f)
                    return;
        }
        mods?.Apply(rule, rule.label, null, rule.debuff, rule.element, duration, strength,
            rule.combatStats, rule.resistances, rule.characterStats, EffectStacking.Refresh, 1, rule.attachedVfx);
        t.Active[rule] = strength;
        if (!had)
        {
            StatusChanged?.Invoke(t.Entity, rule, true);
            try { rule.onStart?.Invoke(RootOf(t.Entity)); }
            catch (Exception e) { Debug.LogException(e, this); }
            if (logChanges)
                Debug.Log($"[PlayerWeatherEffects] {t.Entity.name}: {rule.label} (strength {strength:0.00})", t.Entity);
        }
    }

    private void Remove(Tracked t, WeatherExposureRule rule, bool now)
    {
        t.Active.Remove(rule);
        if (now)
        {
            TimedStatModifiers mods = t.Entity != null ? t.Entity.GetComponent<TimedStatModifiers>() : null;
            mods?.RemoveAll(a => Equals(a.key, rule));
        }
        // With a linger time the status simply runs out by itself.
        StatusChanged?.Invoke(t.Entity, rule, false);
        try { rule.onEnd?.Invoke(RootOf(t.Entity)); }
        catch (Exception e) { Debug.LogException(e, this); }
        if (logChanges && t.Entity != null)
            Debug.Log($"[PlayerWeatherEffects] {t.Entity.name}: {rule.label} ends{(now ? "" : $" in {rule.lingerSeconds:0} s")}", t.Entity);
    }

    /// <summary>The player left the weather (or the component turned off): statuses end, body heat back to normal.</summary>
    private void Release(Tracked t)
    {
        if (t.Active.Count > 0)
        {
            var rulesOn = new List<WeatherExposureRule>(t.Active.Keys);
            foreach (WeatherExposureRule rule in rulesOn)
                Remove(t, rule, true);
        }
        if (t.HeatManaged && t.BodyHeat != null)
            t.BodyHeat.SetEnvironmentalTemperature(50f);
        t.HeatManaged = false;
    }

    private static float Damage(CombatEntity p, float amount, DamageType type, ElementType element, Vector3 at)
    {
        if (amount <= 0f || p == null)
            return 0f;
        return p.ApplyDamage(new DamageInfo
        {
            amount = amount,
            source = null,
            target = p,
            point = at,
            direction = Vector3.down,
            isPeriodic = true,
            type = type,
            element = element,
        });
    }

    /// <summary>The default rules: a sensible set of weather statuses to start from.</summary>
    public static List<WeatherExposureRule> DefaultRules()
    {
        return new List<WeatherExposureRule>
        {
            new WeatherExposureRule
            {
                label = "Soaked", element = ElementType.Water, measure = WeatherMeasure.Rain, threshold = 0.35f,
                needsOpenSky = true, lingerSeconds = 20f, bodyHeatShift = -8f,
                resistances = new List<ElementalResistance> { new ElementalResistance(ElementType.Lightning, -20f), new ElementalResistance(ElementType.Fire, 15f) },
                characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.StaminaRegen, -10f) },
            },
            new WeatherExposureRule
            {
                label = "Chilled", element = ElementType.Ice, measure = WeatherMeasure.ColderThan, threshold = 2f,
                needsOpenSky = false, notUnderwater = false,
                characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MoveSpeed, -8f), TraitModifier.Percent(TraitStat.StaminaRegen, -10f) },
            },
            new WeatherExposureRule
            {
                label = "Frostbite", element = ElementType.Ice, measure = WeatherMeasure.WeatherIs,
                weathers = new List<WeatherType> { WeatherType.Blizzard }, needsOpenSky = true, bodyHeatShift = -10f,
                damagePerSecond = 0.5f, damageType = DamageType.Magical, damageElement = ElementType.Ice,
                characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MoveSpeed, -15f) },
            },
            new WeatherExposureRule
            {
                label = "Heatstroke", element = ElementType.Fire, measure = WeatherMeasure.HotterThan, threshold = 35f,
                needsOpenSky = true, lingerSeconds = 10f, bodyHeatShift = 5f,
                characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.StaminaRegen, -20f), TraitModifier.Percent(TraitStat.StaminaCost, 15f) },
                statusRates = new List<WeatherStatusRate> { new WeatherStatusRate { bar = WeatherStatusBar.Thirst, perSecond = -0.4f } },
            },
            new WeatherExposureRule
            {
                label = "Sandblasted", element = ElementType.Earth, measure = WeatherMeasure.Dust, threshold = 0.5f,
                needsOpenSky = true, damagePerSecond = 0.3f, damageType = DamageType.Physical, damageElement = ElementType.Earth,
                characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MoveSpeed, -10f) },
            },
            new WeatherExposureRule
            {
                label = "Windswept", element = ElementType.Wind, measure = WeatherMeasure.WindSpeed, threshold = 14f,
                needsOpenSky = true,
                characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MoveSpeed, -10f), TraitModifier.Percent(TraitStat.ProjectileSpeed, -15f) },
            },
            new WeatherExposureRule
            {
                label = "Pelted by Hail", measure = WeatherMeasure.Hail, threshold = 0.4f, needsOpenSky = true,
                damagePerSecond = 1f, damageType = DamageType.Physical,
            },
        };
    }

    [ContextMenu("Reset Conditions To Defaults")]
    private void ResetConditionsToDefaults()
    {
        rules = DefaultRules();
    }

    private void OnValidate()
    {
        statusDuration = Mathf.Max(statusDuration, checkInterval * 2f);
    }
}
