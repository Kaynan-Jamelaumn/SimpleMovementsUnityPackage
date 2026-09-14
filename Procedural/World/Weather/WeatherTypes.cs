using System;
using UnityEngine;

/// <summary>
/// A kind of weather (see <see cref="WeatherSystem"/>). Several can happen at once - rain with fog, a thunderstorm
/// with hail and strong wind - and <see cref="WeatherState.Type"/> names the one that stands out.
/// </summary>
public enum WeatherType
{
    Clear,
    Cloudy,
    Windy,
    Mist,
    Fog,
    Rain,
    Thunderstorm,
    Hail,
    Snow,
    Blizzard,
    Sandstorm,
    HeatWave,
    Tornado,
}

/// <summary>
/// The weather at one place and time: how strong each kind of weather is (0 = none, 1 = as strong as it gets),
/// plus the wind and the temperature. Computed by <see cref="WeatherModel"/>; <see cref="WeatherSystem.Current"/> is
/// the one the viewer is in, changing smoothly.
/// </summary>
[Serializable]
public struct WeatherState
{
    /// <summary>The kind of weather that stands out (see <see cref="Classify"/>).</summary>
    public WeatherType Type;
    /// <summary>Cloud cover (0-1).</summary>
    public float Clouds;
    public float Rain;
    public float Snow;
    public float Hail;
    /// <summary>Thunderstorm strength: how often lightning strikes.</summary>
    public float Thunder;
    /// <summary>Sand and dust in the air (sandstorms).</summary>
    public float Dust;
    public float Fog;
    /// <summary>Low mist over the ground.</summary>
    public float Mist;
    /// <summary>Heat wave strength.</summary>
    public float Heat;
    /// <summary>How likely and strong a tornado is nearby.</summary>
    public float Tornado;
    /// <summary>Wind speed, world units per second.</summary>
    public float WindSpeed;
    /// <summary>Direction the wind blows toward (world X, world Z), unit length.</summary>
    public Vector2 WindDirection;
    /// <summary>Air temperature, degrees Celsius.</summary>
    public float Temperature;
    /// <summary>Air humidity (0-1).</summary>
    public float Humidity;

    public bool IsRaining => Rain > 0.05f;
    public bool IsSnowing => Snow > 0.05f;
    public bool IsHailing => Hail > 0.05f;
    public bool IsStormy => Thunder > 0.15f;
    public bool IsFreezing => Temperature <= 0f;
    /// <summary>The strongest precipitation of any kind (0-1).</summary>
    public float Precipitation => Mathf.Max(Rain, Mathf.Max(Snow, Hail));
    /// <summary>The wind as a world-space vector (units per second).</summary>
    public Vector3 Wind => new Vector3(WindDirection.x, 0f, WindDirection.y) * WindSpeed;

    /// <summary>About how far one can see (world units): far in clear air, a few tens of units in thick fog, a blizzard or a sandstorm.</summary>
    public float Visibility
    {
        get
        {
            float haze = Mathf.Max(Fog, Mathf.Max(Dust, Mathf.Max(0.35f * Mist, Mathf.Max(0.6f * Snow, Mathf.Max(0.3f * Rain, 0.1f * Heat)))));
            return Mathf.Lerp(5000f, 25f, Mathf.Pow(Mathf.Clamp01(haze), 0.5f));
        }
    }

    /// <summary>
    /// The kind of weather that stands out: the most dramatic one that is clearly present (a tornado over the
    /// storm it comes from, a blizzard over plain snow...), else fog, mist or wind, else clouds or a clear sky.
    /// </summary>
    public WeatherType Classify()
    {
        if (Tornado > 0.35f) return WeatherType.Tornado;
        if (Snow > 0.35f && WindSpeed > 14f) return WeatherType.Blizzard;
        if (Dust > 0.3f) return WeatherType.Sandstorm;
        if (Hail > 0.3f) return WeatherType.Hail;
        if (Thunder > 0.3f) return WeatherType.Thunderstorm;
        if (Snow > 0.2f) return WeatherType.Snow;
        if (Rain > 0.2f) return WeatherType.Rain;
        if (Heat > 0.35f) return WeatherType.HeatWave;
        if (Fog > 0.35f) return WeatherType.Fog;
        if (Mist > 0.35f) return WeatherType.Mist;
        if (WindSpeed > 11f) return WeatherType.Windy;
        if (Clouds > 0.6f) return WeatherType.Cloudy;
        return WeatherType.Clear;
    }

    /// <summary>
    /// Moves every value of <paramref name="from"/> toward <paramref name="to"/> by the fraction
    /// <paramref name="t"/> (0-1), the wind direction turning the short way. <see cref="Type"/> is re-classified.
    /// </summary>
    public static WeatherState Blend(WeatherState from, WeatherState to, float t)
    {
        t = Mathf.Clamp01(t);
        var result = new WeatherState
        {
            Clouds = Mathf.Lerp(from.Clouds, to.Clouds, t),
            Rain = Mathf.Lerp(from.Rain, to.Rain, t),
            Snow = Mathf.Lerp(from.Snow, to.Snow, t),
            Hail = Mathf.Lerp(from.Hail, to.Hail, t),
            Thunder = Mathf.Lerp(from.Thunder, to.Thunder, t),
            Dust = Mathf.Lerp(from.Dust, to.Dust, t),
            Fog = Mathf.Lerp(from.Fog, to.Fog, t),
            Mist = Mathf.Lerp(from.Mist, to.Mist, t),
            Heat = Mathf.Lerp(from.Heat, to.Heat, t),
            Tornado = Mathf.Lerp(from.Tornado, to.Tornado, t),
            WindSpeed = Mathf.Lerp(from.WindSpeed, to.WindSpeed, t),
            Temperature = Mathf.Lerp(from.Temperature, to.Temperature, t),
            Humidity = Mathf.Lerp(from.Humidity, to.Humidity, t),
        };
        Vector2 a = from.WindDirection.sqrMagnitude > 1e-6f ? from.WindDirection.normalized : to.WindDirection;
        Vector2 b = to.WindDirection.sqrMagnitude > 1e-6f ? to.WindDirection.normalized : a;
        float angle = Mathf.LerpAngle(Mathf.Atan2(a.y, a.x) * Mathf.Rad2Deg, Mathf.Atan2(b.y, b.x) * Mathf.Rad2Deg, t) * Mathf.Deg2Rad;
        result.WindDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        result.Type = result.Classify();
        return result;
    }

    /// <summary>A state showing one kind of weather at the given strength (for <see cref="WeatherSystem.ForceWeather"/>).</summary>
    public static WeatherState Preset(WeatherType type, float strength, Vector2 windDirection, float temperature)
    {
        float s = Mathf.Clamp01(strength);
        var state = new WeatherState { WindDirection = windDirection.sqrMagnitude > 1e-6f ? windDirection.normalized : Vector2.right, Temperature = temperature, Humidity = 0.5f, WindSpeed = 3f };
        switch (type)
        {
            case WeatherType.Cloudy: state.Clouds = 0.8f * s + 0.2f; break;
            case WeatherType.Windy: state.Clouds = 0.3f; state.WindSpeed = 10f + 8f * s; break;
            case WeatherType.Mist: state.Mist = s; state.Humidity = 0.9f; state.WindSpeed = 1f; break;
            case WeatherType.Fog: state.Fog = s; state.Clouds = 0.6f; state.Humidity = 0.95f; state.WindSpeed = 1f; break;
            case WeatherType.Rain: state.Clouds = 1f; state.Rain = s; state.Humidity = 0.9f; state.WindSpeed = 4f + 4f * s; state.Temperature = Mathf.Max(temperature, 8f); break;
            case WeatherType.Thunderstorm: state.Clouds = 1f; state.Rain = Mathf.Max(0.7f, s); state.Thunder = s; state.Humidity = 0.95f; state.WindSpeed = 8f + 8f * s; state.Temperature = Mathf.Max(temperature, 16f); break;
            case WeatherType.Hail: state.Clouds = 1f; state.Rain = 0.5f; state.Thunder = 0.6f * s; state.Hail = s; state.WindSpeed = 8f + 6f * s; state.Temperature = Mathf.Clamp(temperature, 4f, 14f); break;
            case WeatherType.Snow: state.Clouds = 1f; state.Snow = s; state.Humidity = 0.8f; state.WindSpeed = 2f + 3f * s; state.Temperature = Mathf.Min(temperature, -3f); break;
            case WeatherType.Blizzard: state.Clouds = 1f; state.Snow = Mathf.Max(0.8f, s); state.Fog = 0.5f * s; state.WindSpeed = 14f + 8f * s; state.Temperature = Mathf.Min(temperature, -12f); break;
            case WeatherType.Sandstorm: state.Clouds = 0.3f; state.Dust = s; state.WindSpeed = 12f + 10f * s; state.Humidity = 0.1f; state.Temperature = Mathf.Max(temperature, 28f); break;
            case WeatherType.HeatWave: state.Heat = s; state.Humidity = 0.2f; state.WindSpeed = 1.5f; state.Temperature = Mathf.Max(temperature, 36f + 6f * s); break;
            case WeatherType.Tornado: state.Clouds = 1f; state.Rain = 0.6f; state.Thunder = 0.8f; state.Tornado = Mathf.Max(0.5f, s); state.WindSpeed = 14f + 8f * s; state.Temperature = Mathf.Max(temperature, 20f); break;
        }
        state.Type = type;
        return state;
    }
}

/// <summary>
/// How often each kind of weather happens in a biome, relative to what its climate gives (1 = as the climate
/// says, 0 = never, up to 3 = much more often). The climate itself - the biome's Ideal Temperature and Moisture,
/// the world's climate fields and the altitude - already decides most of it: dry, hot biomes get sandstorms and heat
/// waves, cold ones snow and blizzards, wet ones rain and fog. These fine-tune it per biome.
/// </summary>
[Serializable]
public class BiomeWeather
{
    [Tooltip("How often it rains, snows or hails at all (all precipitation).")]
    [Range(0f, 3f)] public float precipitation = 1f;
    [Tooltip("Rain (where it is warm enough).")]
    [Range(0f, 3f)] public float rain = 1f;
    [Tooltip("Thunderstorms: heavy rain, strong gusts and lightning (warm, wet air).")]
    [Range(0f, 3f)] public float thunderstorms = 1f;
    [Tooltip("Hail during thunderstorms.")]
    [Range(0f, 3f)] public float hail = 1f;
    [Tooltip("Snow (where it is cold enough).")]
    [Range(0f, 3f)] public float snow = 1f;
    [Tooltip("Blizzards: snow with strong wind and low visibility.")]
    [Range(0f, 3f)] public float blizzards = 1f;
    [Tooltip("Sandstorms and dust storms (dry, warm and windy).")]
    [Range(0f, 3f)] public float sandstorms = 1f;
    [Tooltip("Heat waves (hot and clear).")]
    [Range(0f, 3f)] public float heatWaves = 1f;
    [Tooltip("Wind strength.")]
    [Range(0f, 3f)] public float wind = 1f;
    [Tooltip("Fog (humid, calm air).")]
    [Range(0f, 3f)] public float fog = 1f;
    [Tooltip("Low mist over the ground.")]
    [Range(0f, 3f)] public float mist = 1f;
    [Tooltip("Tornadoes (from strong thunderstorms over open, flat land).")]
    [Range(0f, 3f)] public float tornadoes = 1f;

    /// <summary>Adds these multipliers, weighted, to a blend of several biomes' multipliers.</summary>
    public void AddTo(ref WeatherFactors factors, float weight)
    {
        factors.Precipitation += precipitation * weight;
        factors.Rain += rain * weight;
        factors.Thunderstorms += thunderstorms * weight;
        factors.Hail += hail * weight;
        factors.Snow += snow * weight;
        factors.Blizzards += blizzards * weight;
        factors.Sandstorms += sandstorms * weight;
        factors.HeatWaves += heatWaves * weight;
        factors.Wind += wind * weight;
        factors.Fog += fog * weight;
        factors.Mist += mist * weight;
        factors.Tornadoes += tornadoes * weight;
        factors.Weight += weight;
    }

    /// <summary>Sets every multiplier for a kind of biome (see <see cref="BiomeWeatherPreset"/>).</summary>
    public void ApplyPreset(BiomeWeatherPreset preset)
    {
        switch (preset)
        {
            case BiomeWeatherPreset.Temperate: Set(1f, 1.1f, 0.8f, 0.8f, 1f, 0.6f, 0f, 0.6f, 1f, 1.2f, 1.2f, 0.3f); break;
            case BiomeWeatherPreset.Grassland: Set(0.9f, 1f, 1.4f, 1.2f, 1f, 1f, 0.4f, 1f, 1.3f, 0.7f, 0.9f, 1.5f); break;
            case BiomeWeatherPreset.Desert: Set(0.25f, 0.6f, 0.5f, 0.3f, 0.2f, 0.2f, 2.5f, 2.5f, 1.3f, 0.1f, 0.1f, 0.2f); break;
            case BiomeWeatherPreset.TundraAndSnow: Set(1f, 0.5f, 0.1f, 0.2f, 2f, 2f, 0f, 0f, 1.4f, 0.8f, 0.6f, 0f); break;
            case BiomeWeatherPreset.Rainforest: Set(2f, 1.6f, 1.8f, 0.5f, 0.3f, 0f, 0f, 0.4f, 0.7f, 1.4f, 2f, 0.3f); break;
            case BiomeWeatherPreset.SwampAndWetland: Set(1.4f, 1.3f, 1f, 0.4f, 0.8f, 0.3f, 0f, 0.8f, 0.6f, 2.5f, 3f, 0.3f); break;
            case BiomeWeatherPreset.Mountains: Set(1.3f, 1f, 1.2f, 1.3f, 1.6f, 1.6f, 0f, 0.2f, 1.8f, 1.3f, 0.6f, 0f); break;
            case BiomeWeatherPreset.Coast: Set(1.2f, 1.2f, 1.2f, 0.6f, 0.8f, 0.8f, 0.2f, 0.5f, 1.6f, 1.8f, 1f, 0.4f); break;
            case BiomeWeatherPreset.AlwaysCalm: Set(0.3f, 1f, 0f, 0f, 1f, 0f, 0f, 0f, 0.5f, 0.3f, 0.3f, 0f); break;
            default: Set(1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f); break;
        }
    }

    /// <summary>Sets every multiplier at once.</summary>
    public void Set(float precipitation, float rain, float thunderstorms, float hail, float snow, float blizzards,
        float sandstorms, float heatWaves, float wind, float fog, float mist, float tornadoes)
    {
        this.precipitation = precipitation;
        this.rain = rain;
        this.thunderstorms = thunderstorms;
        this.hail = hail;
        this.snow = snow;
        this.blizzards = blizzards;
        this.sandstorms = sandstorms;
        this.heatWaves = heatWaves;
        this.wind = wind;
        this.fog = fog;
        this.mist = mist;
        this.tornadoes = tornadoes;
    }
}

/// <summary>Ready-made <see cref="BiomeWeather"/> settings for common kinds of biome.</summary>
public enum BiomeWeatherPreset
{
    /// <summary>Everything 1: only the climate decides.</summary>
    FromClimate,
    /// <summary>Rain, fog, some storms.</summary>
    Temperate,
    /// <summary>Storms, wind and (rarely) tornadoes.</summary>
    Grassland,
    /// <summary>Sandstorms and heat waves, little rain.</summary>
    Desert,
    /// <summary>Snow and blizzards.</summary>
    TundraAndSnow,
    /// <summary>Heavy rain, storms, mist.</summary>
    Rainforest,
    /// <summary>Fog, mist and rain.</summary>
    SwampAndWetland,
    /// <summary>Wind, snow and sudden storms.</summary>
    Mountains,
    /// <summary>Wind, fog and storms.</summary>
    Coast,
    /// <summary>Hardly any weather events.</summary>
    AlwaysCalm,
}

/// <summary>A blend of several biomes' <see cref="BiomeWeather"/> multipliers (see <see cref="Normalized"/>).</summary>
public struct WeatherFactors
{
    public float Precipitation, Rain, Thunderstorms, Hail, Snow, Blizzards, Sandstorms, HeatWaves, Wind, Fog, Mist, Tornadoes;
    public float Weight;

    /// <summary>Every multiplier 1: weather exactly as the climate gives it.</summary>
    public static WeatherFactors Neutral => new WeatherFactors
    {
        Precipitation = 1f,
        Rain = 1f,
        Thunderstorms = 1f,
        Hail = 1f,
        Snow = 1f,
        Blizzards = 1f,
        Sandstorms = 1f,
        HeatWaves = 1f,
        Wind = 1f,
        Fog = 1f,
        Mist = 1f,
        Tornadoes = 1f,
        Weight = 1f,
    };

    /// <summary>The weighted average of what was added (neutral when nothing was).</summary>
    public WeatherFactors Normalized()
    {
        if (Weight <= 1e-6f)
            return Neutral;
        float k = 1f / Weight;
        return new WeatherFactors
        {
            Precipitation = Precipitation * k,
            Rain = Rain * k,
            Thunderstorms = Thunderstorms * k,
            Hail = Hail * k,
            Snow = Snow * k,
            Blizzards = Blizzards * k,
            Sandstorms = Sandstorms * k,
            HeatWaves = HeatWaves * k,
            Wind = Wind * k,
            Fog = Fog * k,
            Mist = Mist * k,
            Tornadoes = Tornadoes * k,
            Weight = 1f,
        };
    }
}
