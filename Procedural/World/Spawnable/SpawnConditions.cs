using System.Collections.Generic;
using UnityEngine;

/// <summary>When in the day something may appear.</summary>
public enum SpawnTimeRule
{
    /// <summary>Any time (the time does not matter).</summary>
    Any,
    /// <summary>Only between sunrise and sunset.</summary>
    DayOnly,
    /// <summary>Only between sunset and sunrise.</summary>
    NightOnly,
    /// <summary>Only between From Hour and To Hour (22 to 4 = late night).</summary>
    Hours,
}

/// <summary>In which weather something may appear.</summary>
public enum SpawnWeatherRule
{
    /// <summary>Any weather (the weather does not matter).</summary>
    Any,
    /// <summary>Only while the weather at the spot is one of the listed ones.</summary>
    OnlyIn,
    /// <summary>Never while the weather at the spot is one of the listed ones.</summary>
    NeverIn,
}

/// <summary>
/// Time and weather rules for a mob, a world portal or a loot drop: only by day, only at night, only between some
/// hours, only in some weathers, never in some weathers - or none of that (the default, so nothing changes until a
/// rule is chosen).
/// <para>The weather is the weather <b>at the spot</b> (<see cref="WeatherSystem.WeatherAt"/>): a storm mob appears
/// where it storms, not everywhere because one player stands in a storm. The time is <see cref="WorldTime"/> (the
/// Day Night Cycle). Both are the server's in a network session (spawning runs there).</para>
/// <para>No Weather System in the scene = weather rules let everything through; no clock = time rules let everything
/// through, so a rule never blocks a scene that cannot answer it.</para>
/// </summary>
[System.Serializable]
public class SpawnConditions
{
    [Tooltip("When in the day it may appear: Any, only by Day, only at Night, or between From Hour and To Hour.")]
    public SpawnTimeRule time = SpawnTimeRule.Any;
    [Tooltip("With Hours: the first hour (0-24).")]
    [Range(0f, 24f)] public float fromHour = 20f;
    [Tooltip("With Hours: the hour it stops (0-24). Lower than From Hour = past midnight (20 to 4 = the night).")]
    [Range(0f, 24f)] public float toHour = 4f;

    [Tooltip("Weather: Any (doesn't matter), Only In the listed weathers, or Never In the listed weathers.")]
    public SpawnWeatherRule weather = SpawnWeatherRule.Any;
    [Tooltip("The weathers the rule above talks about (Rain, Thunderstorm, Snow, Blizzard, Fog...).")]
    public List<WeatherType> weathers = new List<WeatherType>();

    [Tooltip("Remove it again when the rule stops being true (a night mob or a storm portal goes away at dawn or when the " +
             "storm ends). Mobs go when no player is close to them; portals close at once. Off = what is out stays out.")]
    public bool despawnWhenUnmet;

    /// <summary>Is any rule set (otherwise everything is allowed and nothing is looked up)?</summary>
    public bool IsUsed => time != SpawnTimeRule.Any || (weather != SpawnWeatherRule.Any && weathers != null && weathers.Count > 0);

    /// <summary>Do the rules allow it at <paramref name="position"/> now (time and the weather there)?</summary>
    public bool Allows(Vector3 position)
    {
        if (!IsUsed)
            return true;
        return TimeAllows(WorldTime.Hour) && WeatherAllows(position);
    }

    /// <summary>Does the time rule allow <paramref name="hour"/> (-1 = no clock: allowed)?</summary>
    public bool TimeAllows(float hour)
    {
        if (time == SpawnTimeRule.Any || hour < 0f)
            return true;
        switch (time)
        {
            case SpawnTimeRule.DayOnly: return WorldTime.IsDayAt(hour, WorldTime.SunriseHour, WorldTime.SunsetHour);
            case SpawnTimeRule.NightOnly: return !WorldTime.IsDayAt(hour, WorldTime.SunriseHour, WorldTime.SunsetHour);
            case SpawnTimeRule.Hours: return WorldTime.InHours(hour, fromHour, toHour);
            default: return true;
        }
    }

    /// <summary>Does the weather rule allow the weather at <paramref name="position"/> (no Weather System: allowed)?</summary>
    public bool WeatherAllows(Vector3 position)
    {
        if (weather == SpawnWeatherRule.Any || weathers == null || weathers.Count == 0)
            return true;
        if (!SpawnWeather.TryTypeAt(position, out WeatherType type))
            return true;
        return WeatherAllows(type);
    }

    /// <summary>Does the weather rule allow <paramref name="type"/>?</summary>
    public bool WeatherAllows(WeatherType type)
    {
        if (weather == SpawnWeatherRule.Any || weathers == null || weathers.Count == 0)
            return true;
        bool listed = weathers.Contains(type);
        return weather == SpawnWeatherRule.OnlyIn ? listed : !listed;
    }

    /// <summary>The rules in words, for inspectors and logs ("night only, only in Rain/Thunderstorm").</summary>
    public string Describe()
    {
        if (!IsUsed)
            return "any time, any weather";
        string t = time switch
        {
            SpawnTimeRule.DayOnly => "day only",
            SpawnTimeRule.NightOnly => "night only",
            SpawnTimeRule.Hours => $"{fromHour:0.#}h-{toHour:0.#}h",
            _ => "any time",
        };
        string w = weather == SpawnWeatherRule.Any || weathers == null || weathers.Count == 0
            ? "any weather"
            : (weather == SpawnWeatherRule.OnlyIn ? "only in " : "never in ") + string.Join("/", weathers);
        return t + ", " + w;
    }

    /// <summary>Clamps values.</summary>
    public void Validate()
    {
        fromHour = Mathf.Clamp(fromHour, 0f, 24f);
        toHour = Mathf.Clamp(toHour, 0f, 24f);
    }
}

/// <summary>
/// The weather at a spot for spawn rules, cached per 32 m cell for two seconds (spawners ask for many spots and the
/// weather changes slowly).
/// </summary>
public static class SpawnWeather
{
    private const float CellSize = 32f;
    private const float CacheSeconds = 2f;
    private static readonly Dictionary<long, WeatherType> Cache = new Dictionary<long, WeatherType>();
    private static float cacheTime = -1f;
    private static WeatherSystem cacheOwner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Cache.Clear();
        cacheTime = -1f;
        cacheOwner = null;
    }

    /// <summary>The kind of weather at <paramref name="position"/>; false when the scene has no Weather System.</summary>
    public static bool TryTypeAt(Vector3 position, out WeatherType type)
    {
        type = WeatherType.Clear;
        WeatherSystem system = WeatherSystem.Instance;
        if (system == null)
            return false;
        float now = Time.time;
        if (system != cacheOwner || now - cacheTime > CacheSeconds || now < cacheTime || Cache.Count > 4096)
        {
            Cache.Clear();
            cacheTime = now;
            cacheOwner = system;
        }
        long key = ((long)Mathf.FloorToInt(position.x / CellSize) << 32) ^ (uint)Mathf.FloorToInt(position.z / CellSize);
        if (!Cache.TryGetValue(key, out type))
        {
            type = system.WeatherAt(position).Classify();
            Cache[key] = type;
        }
        return true;
    }
}
