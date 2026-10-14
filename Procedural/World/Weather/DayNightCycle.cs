using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// The day and night of the world: a clock (hours 0-24) that turns the sun, darkens the light and the ambience at night,
/// tells the Weather System the hour (misty mornings, stormy afternoons, cold nights) and is what spawn rules read
/// (<see cref="WorldTime"/>: mobs, portals and loot that only appear by day or by night).
/// <para>Put one in a scene that stays loaded (the City, or the Bootstrap scene in a network session); it survives scene
/// changes (Persistent); a second one removes itself. Network sessions: the server's clock is the only real one and is
/// sent to every client every few seconds (NetWorldChunks ▸ Day Clock), wherever their player is; clients keep counting
/// smoothly between updates.</para>
/// </summary>
[AddComponentMenu("SimpleMovements/Day Night Cycle")]
[DisallowMultipleComponent]
public class DayNightCycle : MonoBehaviour
{
    [Header("Clock")]
    [Tooltip("Hour when the game starts (0-24).")]
    [Range(0f, 24f)] public float startHour = 8f;
    [Tooltip("Real minutes a whole day (24 hours) lasts.")]
    [Min(0.5f)] public float dayLengthMinutes = 24f;
    [Tooltip("Nights pass this many times faster than days (2 = the night is over in half the time). 1 = same speed.")]
    [Min(0.1f)] public float nightSpeed = 1f;
    [Tooltip("Hour the sun rises.")]
    [Range(0f, 24f)] public float sunriseHour = 6f;
    [Tooltip("Hour the sun sets.")]
    [Range(0f, 24f)] public float sunsetHour = 19f;
    [Tooltip("Stop the clock (the hour stays as it is).")]
    public bool paused;
    [Tooltip("Keep this object (and the clock) when scenes change.")]
    public bool persistent = true;

    [Header("Sun and light")]
    [Tooltip("The sun (a directional light). Empty = the scene's sun (Lighting settings), else the brightest directional light.")]
    public Light sun;
    [Tooltip("Turn the sun across the sky with the hour (it rises in the east at sunrise, sets in the west at sunset).")]
    public bool rotateSun = true;
    [Tooltip("Compass direction (degrees) the sun's path faces at noon: 180 = the sun is in the south at noon.")]
    [Range(0f, 360f)] public float sunAzimuth = 180f;
    [Tooltip("Highest the sun climbs at noon (degrees above the horizon).")]
    [Range(10f, 90f)] public float noonElevation = 65f;
    [Tooltip("Darken the sun and the ambient light at night.")]
    public bool controlLight = true;
    [Tooltip("Ambient light at night, as a fraction of the day's (0.15 = a dark night, 0.4 = a moonlit one).")]
    [Range(0f, 1f)] public float nightAmbient = 0.18f;
    [Tooltip("Sun colour at sunrise and sunset.")]
    public Color twilightColor = new Color(1f, 0.6f, 0.35f);

    /// <summary>The clock (the most recently enabled one).</summary>
    public static DayNightCycle Instance { get; private set; }

    /// <summary>The hour changed into another part of the day (previous, now).</summary>
    public event System.Action<DayPhase, DayPhase> PhaseChanged;

    private float hour;
    private DayPhase phase;
    private Light foundSun;
    private Scene foundSunScene;
    private bool sunSearched;

    /// <summary>A scene's own light, as it was before the night darkened it (each scene has its own lighting settings).</summary>
    private sealed class SceneLight
    {
        public Light Sun;
        public float Intensity, AmbientIntensity;
        public Color Color, Ambient, Sky, Equator, Ground;
        public AmbientMode AmbientMode;
    }

    private readonly Dictionary<Scene, SceneLight> sceneLights = new Dictionary<Scene, SceneLight>();
    private SceneLight lit;

    /// <summary>Hour of the day (0-24).</summary>
    public float Hour
    {
        get => hour;
        set => hour = Mathf.Repeat(value, 24f);
    }

    /// <summary>The part of the day now.</summary>
    public DayPhase Phase => phase;

    /// <summary>How much daylight there is (0 = night, 1 = full day), soft around sunrise and sunset.</summary>
    public float Daylight
    {
        get
        {
            float h = hour;
            float rise = Smooth(h, sunriseHour - 0.75f, sunriseHour + 0.75f);
            float set = 1f - Smooth(h, sunsetHour - 0.75f, sunsetHour + 0.75f);
            return Mathf.Clamp01(sunriseHour < sunsetHour ? Mathf.Min(rise, set) : Mathf.Max(rise, set));
        }
    }

    private static float Smooth(float h, float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, h));

    private static float networkHour = -1f;
    private static float networkHourAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        networkHour = -1f;
    }

    /// <summary>
    /// Network clients: the server's hour (sent by the World relay every few seconds, wherever the player is). The
    /// client's clock follows it.
    /// </summary>
    public static void ApplyNetworkHour(float serverHour)
    {
        if (float.IsNaN(serverHour) || float.IsInfinity(serverHour))
            return;
        networkHour = Mathf.Repeat(serverHour, 24f);
        networkHourAt = Time.unscaledTime;
    }

    private void Awake()
    {
        // One clock per game: a second Day Night Cycle (the same scene loaded again, another scene with one) removes itself.
#if UNITY_6000_4_OR_NEWER
        DayNightCycle[] all = FindObjectsByType<DayNightCycle>();
#else
        DayNightCycle[] all = FindObjectsByType<DayNightCycle>(FindObjectsInactive.Exclude);
#endif
        foreach (DayNightCycle other in all)
        {
            if (other != this && other.persistent && other.gameObject.scene.name == "DontDestroyOnLoad")
            {
                Debug.Log($"[DayNightCycle] '{name}': a Day Night Cycle is already running ('{other.name}'); this one is removed.", this);
                enabled = false;
                Destroy(this);
                return;
            }
        }
        hour = Mathf.Repeat(startHour, 24f);
        phase = WorldTime.PhaseAt(hour, sunriseHour, sunsetHour);
        if (persistent && transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    private void OnEnable() => Instance = this;

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;
        RestoreLight();
    }

    private void Update()
    {
        WeatherSystem weather = WeatherSystem.Instance;
        if (GameNet.NetAuthority.IsClientOnly)
        {
            // A client: the server's hour, from the clock relay (everywhere) or else with the weather (in the World).
            float server = -1f;
            if (networkHour >= 0f)
            {
                float rate = 24f / (dayLengthMinutes * 60f) * (WorldTime.IsDayAt(networkHour, sunriseHour, sunsetHour) ? 1f : nightSpeed);
                server = paused ? networkHour : Mathf.Repeat(networkHour + rate * (Time.unscaledTime - networkHourAt), 24f);
            }
            else if (weather != null && weather.TimeOfDay >= 0f)
                server = weather.TimeOfDay;
            if (server >= 0f && Mathf.Abs(Mathf.DeltaAngle(server * 15f, hour * 15f)) > 0.75f)
                hour = server;
        }
        if (!paused)
        {
            float speed = 24f / (dayLengthMinutes * 60f) * (WorldTime.IsDayAt(hour, sunriseHour, sunsetHour) ? 1f : nightSpeed);
            hour = Mathf.Repeat(hour + speed * Time.deltaTime, 24f);
        }
        // The server (and single player) tell the weather the hour: it travels to the clients from there.
        if (weather != null && GameNet.NetAuthority.IsServer)
            weather.TimeOfDay = hour;

        DayPhase now = WorldTime.PhaseAt(hour, sunriseHour, sunsetHour);
        if (now != phase)
        {
            DayPhase was = phase;
            phase = now;
            PhaseChanged?.Invoke(was, now);
        }
        if (!GameNet.NetAuthority.IsHeadless)
            UpdateSky(weather);
    }

    private void UpdateSky(WeatherSystem weather)
    {
        Light s = Sun();
        if (s != null && rotateSun)
        {
            // 0 at sunrise, 180 at sunset; below the horizon at night.
            float dayLength = Mathf.Repeat(sunsetHour - sunriseHour, 24f);
            float t = Mathf.Repeat(hour - sunriseHour, 24f);
            float angle = t <= dayLength ? t / Mathf.Max(0.1f, dayLength) * 180f : 180f + (t - dayLength) / Mathf.Max(0.1f, 24f - dayLength) * 180f;
            float elevation = Mathf.Sin(angle * Mathf.Deg2Rad) * noonElevation;
            float azimuth = sunAzimuth - 90f + angle; // east at sunrise, west at sunset
            s.transform.rotation = Quaternion.Euler(elevation, azimuth, 0f);
        }
        // Inside a dungeon there is no sky: its own lighting stays as it is.
        bool indoors = ProceduralDungeon.DungeonSession.InDungeon;
        float daylight = indoors ? 1f : Daylight;
        // A weather system in charge of the light takes the daylight into its own dimming.
        if (weather != null)
        {
            weather.Daylight = daylight;
            weather.NightAmbient = nightAmbient;
            weather.TwilightTint = indoors ? Color.white : TwilightTint(daylight);
            if (weather.LightingEffectsActive)
            {
                lit = null;
                return;
            }
        }
        if (!controlLight || s == null || indoors)
        {
            RestoreLight();
            return;
        }
        SceneLight b = Capture(s);
        float twilight = 1f - Mathf.Abs(daylight * 2f - 1f);
        s.intensity = b.Intensity * daylight;
        s.color = Color.Lerp(b.Color, b.Color * twilightColor, twilight);
        float ambient = Mathf.Lerp(nightAmbient, 1f, daylight);
        if (b.AmbientMode == AmbientMode.Skybox)
            RenderSettings.ambientIntensity = b.AmbientIntensity * ambient;
        else
        {
            RenderSettings.ambientLight = b.Ambient * ambient;
            RenderSettings.ambientSkyColor = b.Sky * ambient;
            RenderSettings.ambientEquatorColor = b.Equator * ambient;
            RenderSettings.ambientGroundColor = b.Ground * ambient;
        }
    }

    private Color TwilightTint(float daylight)
    {
        float twilight = 1f - Mathf.Abs(daylight * 2f - 1f);
        return Color.Lerp(Color.white, twilightColor, twilight);
    }

    private Light Sun()
    {
        if (sun != null) return sun;
        if (RenderSettings.sun != null) return RenderSettings.sun; // the active scene's sun
        Scene active = SceneManager.GetActiveScene();
        if (sunSearched && foundSunScene == active && (foundSun == null || foundSun.isActiveAndEnabled)) return foundSun;
        foundSun = null;
        foundSunScene = active;
        sunSearched = true;
        {
#if UNITY_6000_4_OR_NEWER
            Light[] lights = FindObjectsByType<Light>();
#else
            Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
#endif
            float best = -1f;
            foreach (Light l in lights)
                if (l != null && l.type == LightType.Directional && l.intensity > best) { best = l.intensity; foundSun = l; }
        }
        return foundSun;
    }

    /// <summary>
    /// The active scene's own light before the night changed it, captured the first time (lighting settings belong to
    /// the active scene, so the City, the World and every other scene keep their own values and are never darkened twice).
    /// </summary>
    private SceneLight Capture(Light s)
    {
        Scene active = SceneManager.GetActiveScene();
        if (!sceneLights.TryGetValue(active, out SceneLight b) || b.Sun != s)
        {
            if (b != null && b.Sun != null && b.Sun != s) { b.Sun.intensity = b.Intensity; b.Sun.color = b.Color; }
            b = new SceneLight
            {
                Sun = s,
                Intensity = s.intensity,
                Color = s.color,
                AmbientMode = RenderSettings.ambientMode,
                AmbientIntensity = RenderSettings.ambientIntensity,
                Ambient = RenderSettings.ambientLight,
                Sky = RenderSettings.ambientSkyColor,
                Equator = RenderSettings.ambientEquatorColor,
                Ground = RenderSettings.ambientGroundColor,
            };
            sceneLights[active] = b;
        }
        lit = b;
        return b;
    }

    /// <summary>
    /// The Weather System takes over the active scene's light: it gets the light as it was before the night darkened it
    /// (from then on it applies <see cref="WeatherSystem.Daylight"/> itself).
    /// </summary>
    public void HandOverLight(Light weatherSun, ref float sunIntensity, ref Color sunColor, ref float ambientIntensity,
        ref Color ambient, ref Color sky, ref Color equator, ref Color ground)
    {
        if (!sceneLights.TryGetValue(SceneManager.GetActiveScene(), out SceneLight b))
            return;
        if (weatherSun != null && weatherSun == b.Sun)
        {
            sunIntensity = b.Intensity;
            sunColor = b.Color;
        }
        if (b.AmbientMode == RenderSettings.ambientMode)
        {
            ambientIntensity = b.AmbientIntensity;
            ambient = b.Ambient;
            sky = b.Sky;
            equator = b.Equator;
            ground = b.Ground;
        }
        if (lit == b)
            lit = null;
    }

    /// <summary>Gives the active scene back the light it had (the component turned off, a dungeon, Control Light off).</summary>
    private void RestoreLight()
    {
        if (lit == null) return;
        SceneLight b = lit;
        lit = null;
        if (b.Sun != null) { b.Sun.intensity = b.Intensity; b.Sun.color = b.Color; }
        if (!sceneLights.TryGetValue(SceneManager.GetActiveScene(), out SceneLight mine) || mine != b)
            return; // another scene's settings: they are back when it is active again
        RenderSettings.ambientIntensity = b.AmbientIntensity;
        RenderSettings.ambientLight = b.Ambient;
        RenderSettings.ambientSkyColor = b.Sky;
        RenderSettings.ambientEquatorColor = b.Equator;
        RenderSettings.ambientGroundColor = b.Ground;
    }
}
