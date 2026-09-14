using System;
using UnityEngine;

/// <summary>
/// Weather for the generated world: rain, thunderstorms, hail, snow, blizzards, sandstorms, heat waves, wind,
/// fog, mist and tornadoes, following the biomes and their climate (see <see cref="WeatherModel"/> for how the
/// weather is decided and <see cref="BiomeWeather"/> to tune it per biome).
///
/// Put it on any GameObject in a scene with a <see cref="TerrainGenerator"/>. It follows the viewer (Endless
/// Terrain's viewer, else the main camera), looks up the weather there twice a second and changes smoothly toward
/// it, with effects made at runtime - no assets needed: particles for precipitation, dust, mist and debris, fog,
/// dimmer and tinted sunlight under clouds, lightning, a wind zone for trees, snow settling and ground getting wet on
/// the terrain, and optional sounds. Game code can read the weather (<see cref="Current"/>, <see cref="SampleWeather"/>,
/// <see cref="WeatherChanged"/>), force it (<see cref="ForceWeather"/>) and save its time (<see cref="WeatherTime"/>).
/// </summary>
[AddComponentMenu("SimpleMovements/Weather System")]
[DisallowMultipleComponent]
public partial class WeatherSystem : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("The terrain generator whose biomes and climate decide the weather. Empty = the one in the scene.")]
    [SerializeField] private TerrainGenerator terrainGenerator;
    [Tooltip("What the weather follows (the player or camera). Empty = Endless Terrain's viewer, else the main camera.")]
    [SerializeField] private Transform viewer;
    [Tooltip("The sun (a directional light) that clouds dim and storms flash. Empty = the scene's sun (Lighting settings), else the brightest directional light.")]
    [SerializeField] private Light sun;

    [Header("Weather")]
    [Tooltip("Typical size (world units) of a weather system - a region of clouds, rain and wind. Larger = weather covers wider areas and stays longer.")]
    [SerializeField] private float systemSize = 2500f;
    [Tooltip("How fast weather systems drift with the prevailing wind (world units per second). With the default size, weather at one place changes every several minutes - a shower, a clear spell, a storm passing. Lower = longer spells.")]
    [SerializeField] private float driftSpeed = 4f;
    [Tooltip("How fast weather time runs compared to game time: 2 = weather changes twice as fast.")]
    [SerializeField] private float weatherTimeScale = 1f;
    [Tooltip("How often it rains or snows everywhere: 1 = as the climate says, 0 = never, 2 = much more often.")]
    [SerializeField][Range(0f, 2f)] private float precipitation = 1f;
    [Tooltip("How strong the wind is everywhere: 1 = normal.")]
    [SerializeField][Range(0f, 2f)] private float windiness = 1f;
    [Tooltip("How much the biomes' own climate (Ideal Temperature / Ideal Moisture) decides the weather, against the world's climate fields. 1 = only the biomes: a desert biome is dry even where the climate fields are wet (use this when Natural Climate Placement is off).")]
    [SerializeField][Range(0f, 1f)] private float biomeInfluence = 0.75f;
    [Tooltip("The climate is averaged over this distance (world units) around the viewer, so the weather changes gradually when crossing into another biome.")]
    [SerializeField] private float climateSampleRadius = 60f;
    [Tooltip("Seconds the weather takes to change to what it is at the viewer's position (clouds rolling in, rain getting heavier...).")]
    [SerializeField] private float transitionSeconds = 12f;
    [Tooltip("Allow tornadoes (rare: strong thunderstorms over warm, flat, open land).")]
    [SerializeField] private bool allowTornadoes = true;
    [Tooltip("Weather time (seconds) when the scene starts. Different values start the world in different weather; the same value always gives the same weather.")]
    [SerializeField] private float startTime = 0f;
    [Tooltip("Hour of the day (0-24) for the weather's day cycle - misty mornings, stormier afternoons, colder nights. -1 = no day cycle. Set it from your day/night system through TimeOfDay.")]
    [SerializeField] private float timeOfDay = -1f;

    [Header("Effects")]
    [Tooltip("Radius (world units) around the viewer where rain, snow, hail, dust and mist particles fall.")]
    [SerializeField] private float effectRadius = 35f;
    [Tooltip("Most particles alive at once over all weather effects. Lower for weaker devices.")]
    [SerializeField] private int maxParticles = 8000;
    [Tooltip("Thicken the scene's fog in fog, mist, rain, snow, blizzards and sandstorms (tinted to match). Your own fog settings are kept and restored.")]
    [SerializeField] private bool fogEffects = true;
    [Tooltip("Fog density (exponential squared) in the thickest fog or sandstorm.")]
    [SerializeField] private float maxFogDensity = 0.03f;
    [Tooltip("Dim and tint the sun and ambient light under clouds, dust and heat, and flash them with lightning.")]
    [SerializeField] private bool lightingEffects = true;
    [Tooltip("Show lightning bolts during thunderstorms (flashes happen with Lighting Effects either way).")]
    [SerializeField] private bool lightningBolts = true;
    [Tooltip("Most lightning strikes per minute in the strongest thunderstorm.")]
    [SerializeField] private float maxLightningPerMinute = 8f;
    [Tooltip("Drive a Wind Zone (trees and wind-aware shaders sway with the weather's wind).")]
    [SerializeField] private bool windZone = true;
    [Tooltip("Snow settles on the terrain while it snows (and melts when warm), and the ground darkens and shines when it rains (the package's terrain shader).")]
    [SerializeField] private bool groundEffects = true;
    [Tooltip("How far from the viewer (world units) rain-wet ground and settled snow show at full strength; they fade out over another half of this. It's the weather where the viewer is, so far-off biomes don't share it. 0 = everywhere.")]
    [SerializeField] private float groundEffectRadius = 400f;
    [Tooltip("Permanent snow on the terrain above the Terrain Generator's Snow Line Height (the package's terrain shader).")]
    [SerializeField] private bool snowCaps = true;
    [Tooltip("Push rigidbodies around a tornado (swirling, pulling in and lifting). 0 = off.")]
    [SerializeField] private float tornadoForce = 40f;

    [Header("Materials (optional)")]
    [Tooltip("Particle material for rain. Empty = made at runtime (for builds, keep the render pipeline's particle shader included, or assign materials here).")]
    [SerializeField] private Material rainMaterial;
    [Tooltip("Particle material for snow and hail. Empty = made at runtime.")]
    [SerializeField] private Material snowMaterial;
    [Tooltip("Particle material for dust, mist and tornadoes (soft, round particles). Empty = made at runtime.")]
    [SerializeField] private Material dustMaterial;
    [Tooltip("Material for lightning bolts. Empty = made at runtime.")]
    [SerializeField] private Material lightningMaterial;

    [Header("Audio (optional)")]
    [Tooltip("Looping rain sound (volume follows the rain).")]
    [SerializeField] private AudioClip rainLoop;
    [Tooltip("Looping wind sound (volume follows the wind).")]
    [SerializeField] private AudioClip windLoop;
    [Tooltip("Looping hail sound.")]
    [SerializeField] private AudioClip hailLoop;
    [Tooltip("Looping sandstorm sound.")]
    [SerializeField] private AudioClip sandstormLoop;
    [Tooltip("Looping tornado roar (louder closer to it).")]
    [SerializeField] private AudioClip tornadoLoop;
    [Tooltip("Thunder sounds; one is played after each lightning strike, delayed by the strike's distance.")]
    [SerializeField] private AudioClip[] thunderClips;
    [Tooltip("Volume of all weather sounds.")]
    [SerializeField][Range(0f, 1f)] private float volume = 0.8f;

    /// <summary>How often the weather at the viewer is looked up (seconds).</summary>
    private const float SampleInterval = 0.5f;
    /// <summary>How long the standing-out weather must last before <see cref="WeatherChanged"/> is raised (seconds).</summary>
    private const float ChangeHold = 20f;
    /// <summary>The same between clear and cloudy only (clouds come and go often).</summary>
    private const float MinorChangeHold = 60f;

    private readonly WeatherModel model = new WeatherModel();
    private WeatherClimateSampler climateSampler;
    private TerrainGenerator samplerGenerator;
    private float samplerAge;
    private float sampleTimer;
    private WeatherState current, target;
    private bool hasState;
    private WeatherType announcedType;
    private WeatherType pendingType;
    private float pendingTime;
    private bool forced;
    private WeatherState forcedState;
    private float forcedUntil;
    private float groundWetness, snowCover;
    private double weatherTime;
    private Transform foundViewer;
    private float searchTimer;

    /// <summary>The weather system in the scene (the most recently enabled one).</summary>
    public static WeatherSystem Instance { get; private set; }

    /// <summary>
    /// Raised when the weather that stands out at the viewer changes (after it has lasted a few seconds):
    /// previous type, new type.
    /// </summary>
    public event Action<WeatherType, WeatherType> WeatherChanged;

    /// <summary>The weather at the viewer, changing smoothly.</summary>
    public WeatherState Current => current;
    /// <summary>The weather at the viewer's position right now, which <see cref="Current"/> is changing toward.</summary>
    public WeatherState Target => target;
    /// <summary>The kind of weather that stands out at the viewer (announced by <see cref="WeatherChanged"/>).</summary>
    public WeatherType CurrentType => announcedType;
    /// <summary>The wind at the viewer, world units per second.</summary>
    public Vector3 WindVelocity => current.Wind;
    /// <summary>How wet the ground is from rain (0-1), drying afterwards.</summary>
    public float GroundWetness => groundWetness;
    /// <summary>How much snow has settled (0-1), melting when warm.</summary>
    public float SnowCover => snowCover;
    /// <summary>True while <see cref="ForceWeather"/> overrides the weather.</summary>
    public bool IsForced => forced;

    /// <summary>
    /// Weather time in seconds. The weather is a function of place and weather time, so saving this and setting it
    /// again when loading brings back the same weather everywhere.
    /// </summary>
    public double WeatherTime
    {
        get => weatherTime;
        set => weatherTime = value;
    }

    /// <summary>Hour of the day (0-24) for the day cycle, or -1 for none (see the Time Of Day setting).</summary>
    public float TimeOfDay
    {
        get => timeOfDay;
        set => timeOfDay = value;
    }

    /// <summary>
    /// Hides precipitation, dust and mist particles (e.g. while the player is indoors or in a cave); fog, light,
    /// wind and sounds keep following the weather (lower their volume yourself if needed).
    /// </summary>
    public bool SuppressEffects { get; set; }

    /// <summary>The terrain generator the weather follows (found in the scene when not set).</summary>
    public TerrainGenerator Generator
    {
        get
        {
            if (terrainGenerator == null && searchTimer <= 0f)
            {
                terrainGenerator = FindAnyObjectByType<TerrainGenerator>();
                searchTimer = 1f;
            }
            return terrainGenerator;
        }
    }

    /// <summary>What the weather follows (see the Viewer setting).</summary>
    public Transform Viewer
    {
        get
        {
            if (viewer != null)
                return viewer;
            if (foundViewer == null && searchTimer <= 0f)
            {
                // Looked up at most once a second while nothing is found (scene searches are slow).
                searchTimer = 1f;
                EndlessTerrain endless = FindAnyObjectByType<EndlessTerrain>();
                if (endless != null && endless.viewer != null)
                    foundViewer = endless.viewer;
                else if (Camera.main != null)
                    foundViewer = Camera.main.transform;
            }
            return foundViewer;
        }
        set => viewer = value;
    }

    private void Awake()
    {
        weatherTime = startTime;
    }

    private void OnEnable()
    {
        Instance = this;
        hasState = false;
        searchTimer = 0f;
        CaptureSceneSettings();
    }

    private void OnDisable()
    {
        RestoreSceneSettings();
        DestroyEffects();
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        weatherTime += dt * Mathf.Max(0f, weatherTimeScale);
        searchTimer -= Time.unscaledDeltaTime;

        Transform follow = Viewer;
        Vector3 viewerPosition = follow != null ? follow.position : transform.position;

        sampleTimer -= dt;
        if (!hasState || sampleTimer <= 0f)
        {
            sampleTimer = SampleInterval;
            target = forced ? forcedState : SampleWeather(viewerPosition);
            if (!hasState)
            {
                current = target;
                announcedType = pendingType = current.Type;
                hasState = true;
            }
        }
        if (forced && forcedUntil > 0f && Time.time >= forcedUntil)
            ClearForcedWeather();

        current = WeatherState.Blend(current, target, dt / Mathf.Max(0.1f, transitionSeconds));
        TrackType(dt);
        Accumulate(dt);
        UpdateEffects(viewerPosition, dt);
    }

    /// <summary>
    /// The weather at any world position now (not smoothed): what the weather there is doing at the current
    /// weather time. Costs a few biome lookups; fine now and then, not for every frame of many objects.
    /// </summary>
    public WeatherState SampleWeather(Vector3 worldPosition)
    {
        return SampleWeather(worldPosition, weatherTime);
    }

    /// <summary>The weather at a world position at a weather time (e.g. <see cref="WeatherTime"/> + 600 for a forecast ten minutes ahead).</summary>
    public WeatherState SampleWeather(Vector3 worldPosition, double time)
    {
        WeatherClimateSampler climate = ClimateSampler();
        if (climate == null)
            return WeatherState.Preset(WeatherType.Clear, 1f, Vector2.right, 15f);
        float scale = Mathf.Approximately(climate.Generator.ScaleFactor, 0f) ? 1f : climate.Generator.ScaleFactor;
        float x = worldPosition.x / scale, y = worldPosition.z / scale;
        ConfigureModel(climate.Generator);
        return model.Evaluate(x, y, time, climate.Sample(x, y, climateSampleRadius, biomeInfluence), timeOfDay);
    }

    /// <summary>
    /// The climate that decides the weather at a world position (it doesn't change with time). With
    /// <see cref="Evaluate"/>, the fast way to look up the weather at the same place for many times (a forecast).
    /// </summary>
    public WeatherClimate SampleClimate(Vector3 worldPosition)
    {
        WeatherClimateSampler climate = ClimateSampler();
        if (climate == null)
            return new WeatherClimate { Temperature = 0.55f, Moisture = 0.5f, Flatness = 0.5f, Factors = WeatherFactors.Neutral };
        float scale = Mathf.Approximately(climate.Generator.ScaleFactor, 0f) ? 1f : climate.Generator.ScaleFactor;
        return climate.Sample(worldPosition.x / scale, worldPosition.z / scale, climateSampleRadius, biomeInfluence);
    }

    /// <summary>The weather at a world position with a known climate (<see cref="SampleClimate"/>) at a weather time.</summary>
    public WeatherState Evaluate(Vector3 worldPosition, double time, WeatherClimate climate)
    {
        TerrainGenerator generator = Generator;
        float scale = generator != null && !Mathf.Approximately(generator.ScaleFactor, 0f) ? generator.ScaleFactor : 1f;
        if (generator != null)
            ConfigureModel(generator);
        return model.Evaluate(worldPosition.x / scale, worldPosition.z / scale, time, climate, timeOfDay);
    }

    /// <summary>
    /// Overrides the weather with one kind at a strength (0-1), changing to it smoothly, for
    /// <paramref name="seconds"/> (0 = until <see cref="ClearForcedWeather"/>). For story events, tests and the inspector's buttons.
    /// </summary>
    public void ForceWeather(WeatherType type, float strength = 1f, float seconds = 0f)
    {
        Vector2 wind = current.WindDirection.sqrMagnitude > 1e-6f ? current.WindDirection : model.Wind;
        forcedState = WeatherState.Preset(type, strength, wind, hasState ? current.Temperature : 15f);
        forced = true;
        forcedUntil = seconds > 0f ? Time.time + seconds : 0f;
        target = forcedState;
        sampleTimer = SampleInterval;
    }

    /// <summary>Returns to the natural weather (see <see cref="ForceWeather"/>).</summary>
    public void ClearForcedWeather()
    {
        forced = false;
        forcedUntil = 0f;
        sampleTimer = 0f;
    }

    /// <summary>The climate lookup for the current generator (rebuilt now and then, so changed settings are picked up).</summary>
    private WeatherClimateSampler ClimateSampler()
    {
        TerrainGenerator generator = Generator;
        if (generator == null || generator.BiomeDefinitions == null || generator.BiomeDefinitions.Length == 0)
            return null;
        samplerAge += Time.unscaledDeltaTime;
        if (climateSampler == null || samplerGenerator != generator || samplerAge > 10f)
        {
            climateSampler = new WeatherClimateSampler(generator);
            samplerGenerator = generator;
            samplerAge = 0f;
        }
        return climateSampler;
    }

    private void ConfigureModel(TerrainGenerator generator)
    {
        model.Seed = generator.VoronoiSeed;
        model.Wind = TerrainClimate.WindDirection(generator.PrevailingWindAngle);
        model.SystemSize = Mathf.Max(200f, systemSize);
        model.DriftSpeed = Mathf.Max(0f, driftSpeed);
        model.Precipitation = precipitation;
        model.Windiness = windiness;
        model.Tornadoes = allowTornadoes;
    }

    /// <summary>Announces a change of the standing-out weather once it has held for a few seconds.</summary>
    private void TrackType(float dt)
    {
        WeatherType type = current.Type;
        if (type != pendingType)
        {
            pendingType = type;
            pendingTime = 0f;
        }
        pendingTime += dt;
        bool minor = (pendingType == WeatherType.Clear || pendingType == WeatherType.Cloudy)
            && (announcedType == WeatherType.Clear || announcedType == WeatherType.Cloudy);
        if (pendingType != announcedType && pendingTime >= (minor ? MinorChangeHold : ChangeHold))
        {
            WeatherType previous = announcedType;
            announcedType = pendingType;
            WeatherChanged?.Invoke(previous, announcedType);
        }
    }

    /// <summary>
    /// Ground wetness and settled snow: rain soaks the ground in about a minute and it dries over several; heavy
    /// snow covers it in about four minutes and melts when it is warm (faster in rain).
    /// </summary>
    private void Accumulate(float dt)
    {
        float warm = Mathf.Clamp01(current.Temperature / 10f);
        // Snow melts faster the warmer it is (walking from the tundra into a desert, it's gone within a minute or two).
        float melt = Mathf.Clamp(current.Temperature / 10f, 0f, 4f);
        groundWetness += dt * (current.Rain * 0.02f + current.Hail * 0.01f - (0.003f + 0.01f * current.Heat + 0.003f * warm) * (1f - current.Rain));
        groundWetness += dt * snowCover * warm * 0.004f;   // melting snow wets the ground
        snowCover += dt * (current.Snow * (current.Temperature <= 1f ? 0.004f : 0.001f) - melt * 0.004f - current.Rain * 0.006f);
        groundWetness = Mathf.Clamp01(groundWetness);
        snowCover = Mathf.Clamp01(snowCover);
    }
}
