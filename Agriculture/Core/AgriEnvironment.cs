using System;
using UnityEngine;

/// <summary>The weather over a field at one moment.</summary>
public struct AgriWeather
{
    /// <summary>Air temperature, °C.</summary>
    public float temperature;
    /// <summary>Air humidity, 0-1.</summary>
    public float humidity;
    /// <summary>Rain reaching the ground, mm per hour.</summary>
    public float rain;
    /// <summary>Cloud cover, 0-1.</summary>
    public float clouds;
    /// <summary>Wind, m/s.</summary>
    public float wind;
    /// <summary>Sunlight, 0-1 (1 = a clear noon in full sun).</summary>
    public float light;
    /// <summary>Is the sun up.</summary>
    public bool day;
    /// <summary>A storm (heavy rain and strong wind).</summary>
    public bool storm;

    public bool Raining => rain > 0.1f;
    public bool Freezing => temperature <= 0f;
}

/// <summary>
/// Where the weather comes from: the city's climate (<see cref="AgriClimate"/>, worked out for any hour) or the
/// world's Weather System (the runtime fills <see cref="AgriWeather"/> from it). Also the sun's light and the drying
/// power of the air.
/// </summary>
public static class AgriEnvironment
{
    /// <summary>Sunlight 0-1 at an hour of a day: up between sunrise and sunset, highest at noon, dimmed by clouds.</summary>
    public static float Sunlight(float hour, float sunrise, float sunset, float clouds)
    {
        if (hour <= sunrise || hour >= sunset || sunset <= sunrise) return 0f;
        float t = (hour - sunrise) / (sunset - sunrise);
        float height = (float)Math.Sin(Math.PI * t);
        return Mathf.Clamp01(height * 1.08f) * (1f - 0.75f * AgriMath.Clamp01(clouds));
    }

    /// <summary>
    /// How much water the air pulls from wet ground in an hour (mm): more when warm, dry, sunny and windy - about 3-5 mm a
    /// summer day, almost nothing on a cold, damp night.
    /// </summary>
    public static float Evaporation(in AgriWeather w)
    {
        float t = Mathf.Max(0f, w.temperature);
        return (0.012f + 0.0105f * t) * (1f - 0.65f * AgriMath.Clamp01(w.humidity)) * (0.25f + 0.75f * AgriMath.Clamp01(w.light) + (w.day ? 0.1f : 0f)) * (1f + 0.04f * Mathf.Max(0f, w.wind));
    }

    /// <summary>The mineralization clock: about 1 per hour at 20 °C, doubling every 10 °C, nothing near freezing.</summary>
    public static float Warmth(float soilTemperature)
    {
        if (soilTemperature <= 2f) return 0f;
        return Mathf.Min(2.5f, (float)Math.Pow(2.0, (soilTemperature - 20f) / 10f)) * AgriMath.Smooth(2f, 6f, soilTemperature);
    }

    /// <summary>
    /// The city climate's weather at a game time: the season's mean (blended across the seasons), the day's own
    /// randomness, heat waves and cold snaps, the warm afternoon and the cold night, rain spells and storms. The same
    /// seed and time always give the same weather.
    /// </summary>
    public static AgriWeather Climate(AgriSettings s, double time)
    {
        s ??= AgriSettings.Defaults();
        AgriClimate c = s.climate ?? new AgriClimate();
        var date = new AgriDate(time, s);
        int day = date.Day;
        float hour = date.Hour;
        int seed = c.seed;

        // The season's mean, blended smoothly towards the next and the previous season around the boundaries.
        AgriSeasonClimate now = c.Of(date.Season);
        AgriSeasonClimate next = c.Of((Season)(((int)date.Season + 1) % 4));
        AgriSeasonClimate prev = c.Of((Season)(((int)date.Season + 3) % 4));
        float p = date.SeasonProgress;
        float wNext = AgriMath.Smooth(0.5f, 1.2f, p) * 0.5f, wPrev = (1f - AgriMath.Smooth(-0.2f, 0.5f, p)) * 0.5f;
        float mean = now.meanTemperature * (1f - wNext - wPrev) + next.meanTemperature * wNext + prev.meanTemperature * wPrev;
        float humidityBase = now.humidity * (1f - wNext - wPrev) + next.humidity * wNext + prev.humidity * wPrev;

        // The day's own character, carried over a little from yesterday.
        float anomaly = (AgriMath.Gauss(seed, day, 11) * 0.65f + AgriMath.Gauss(seed, day - 1, 11) * 0.35f) * c.temperatureNoise;
        anomaly += Extreme(c, s, day, seed);
        bool rainy = AgriMath.Noise(seed, day, 23) < now.rainChance;
        bool storm = rainy && AgriMath.Noise(seed, day, 29) < now.stormChance;
        float clouds = AgriMath.Clamp01(now.clouds + AgriMath.Signed(seed, day, 31) * 0.25f + (rainy ? 0.35f : 0f));

        // The rain spell of the day: when it starts, how long, how hard.
        float rain = 0f;
        if (rainy)
        {
            float start = AgriMath.Noise(seed, day, 37) * 24f;
            float length = 2f + AgriMath.Noise(seed, day, 41) * (storm ? 4f : 8f);
            float h = hour < start ? hour + 24f : hour;
            if (h >= start && h <= start + length)
            {
                float k = AgriMath.Noise(seed, day, 43);
                rain = now.rainIntensity * (0.3f + 1.7f * k * k) * (storm ? 3.5f : 1f);
                float edge = Mathf.Min(h - start, start + length - h);
                rain *= AgriMath.Smooth(0f, 0.75f, edge);
            }
            // A spell that started yesterday evening and runs past midnight.
            float yStart = AgriMath.Noise(seed, day - 1, 37) * 24f, yLength = 2f + AgriMath.Noise(seed, day - 1, 41) * 8f;
            if (AgriMath.Noise(seed, day - 1, 23) < c.Of(new AgriDate(Math.Max(0.0, time - 24.0), s).Season).rainChance && hour + 24f <= yStart + yLength)
                rain = Mathf.Max(rain, now.rainIntensity * 0.6f);
        }

        float swing = now.dailySwing * (1f - 0.55f * clouds);
        float temperature = mean + anomaly + swing * 0.5f * (float)Math.Cos(2.0 * Math.PI * (hour - 15f) / 24f) - (rain > 0f ? 1.5f : 0f);
        float light = Sunlight(hour, date.Sunrise, date.Sunset, clouds);
        float humidity = AgriMath.Clamp01(humidityBase + (rain > 0f ? 0.3f : 0f) + (light <= 0f ? 0.12f : -0.08f * light) + AgriMath.Signed(seed, day, 47) * 0.08f);
        float wind = Mathf.Max(0f, now.wind * (0.6f + 0.8f * AgriMath.Noise(seed, day, 53)) * (storm ? 3.5f : 1f));
        return new AgriWeather { temperature = temperature, humidity = humidity, rain = rain, clouds = clouds, wind = wind, light = light, day = light > 0f, storm = storm && rain > 0f };
    }

    /// <summary>A heat wave (summer) or cold snap (other seasons) running over this day: degrees added.</summary>
    private static float Extreme(AgriClimate c, AgriSettings s, int day, int seed)
    {
        float sum = 0f;
        for (int back = 0; back < 3; back++)
        {
            int d = day - back;
            if (d < 0) break;
            var date = new AgriDate(d * 24.0 + 12.0, s);
            AgriSeasonClimate sc = c.Of(date.Season);
            if (AgriMath.Noise(seed, d, 61) < sc.extremeChance)
            {
                float strength = 5f + AgriMath.Noise(seed, d, 67) * 4f;
                sum += date.Season == Season.Summer ? strength : -strength - (date.Season == Season.Winter ? 3f : 0f);
                break;
            }
        }
        return sum;
    }

    /// <summary>The seasons' temperature differences added to a world Weather System's own (which has no seasons).</summary>
    public static float SeasonOffset(Season s)
    {
        switch (s)
        {
            case Season.Spring: return -2f;
            case Season.Summer: return 3f;
            case Season.Autumn: return -2.5f;
            default: return -9f;
        }
    }
}
