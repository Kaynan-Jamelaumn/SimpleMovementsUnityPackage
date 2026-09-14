using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The climate that decides the weather at one place (see <see cref="WeatherClimateSampler"/>).
/// </summary>
public struct WeatherClimate
{
    /// <summary>0 = coldest, 1 = hottest, after altitude (about -25 to 42 degrees C).</summary>
    public float Temperature;
    /// <summary>0 = driest, 1 = wettest.</summary>
    public float Moisture;
    /// <summary>Ground height above sea level (world units).</summary>
    public float Altitude;
    /// <summary>1 = open, flat land (plains, wetland, dunes), 0 = mountains - where tornadoes can form.</summary>
    public float Flatness;
    /// <summary>The biomes' own weather multipliers there (see <see cref="BiomeWeather"/>).</summary>
    public WeatherFactors Factors;
}

/// <summary>
/// The world's weather as a function of place and time - the same for every run with the same seed and settings,
/// so it can be looked up anywhere (by the <see cref="WeatherSystem"/> at the viewer, by the editor's weather map,
/// by game code wanting the weather somewhere else).
///
/// Weather systems - wide areas of cloud, rain and wind a few kilometres across - drift over the world with the
/// prevailing wind, growing and fading as they go; smaller storm cells, gusts, fog banks and heat build up and die
/// down inside them. What a system brings depends on the climate where it is: in wet places it rains often, in
/// dry ones rarely (a strong system raises a sandstorm instead where it is hot); where it is cold it snows, and with
/// strong wind a blizzard; warm, wet air builds thunderstorms, some with hail and, over flat open land, rarely a
/// tornado; calm, humid air makes fog and low mist; hot, clear weather becomes a heat wave. Each biome can make
/// any of these more or less likely (<see cref="BiomeWeather"/>).
/// </summary>
public sealed class WeatherModel
{
    /// <summary>Temperature range the 0-1 climate temperature maps to (degrees C).</summary>
    public const float ColdestCelsius = -25f;
    public const float HottestCelsius = 42f;

    public int Seed;
    /// <summary>Direction the prevailing wind blows toward (world X, world Z), unit length.</summary>
    public Vector2 Wind = new Vector2(1f, 0f);
    /// <summary>Typical size (world units) of a weather system.</summary>
    public float SystemSize = 2500f;
    /// <summary>How fast weather systems drift with the wind (world units per second of weather time).</summary>
    public float DriftSpeed = 4f;
    /// <summary>How often it rains or snows everywhere (1 = as the climate says).</summary>
    public float Precipitation = 1f;
    /// <summary>How strong the wind is everywhere (1 = normal).</summary>
    public float Windiness = 1f;
    /// <summary>Whether tornadoes can form.</summary>
    public bool Tornadoes = true;

    /// <summary>The weather at a position (terrain generator coordinates) at a weather time (seconds).</summary>
    /// <param name="dayTime">Hour of the day (0-24) when the game has a day cycle, or negative: mornings are misty, afternoons stormier, nights colder.</param>
    public WeatherState Evaluate(float x, float y, double time, WeatherClimate climate, float dayTime = -1f)
    {
        WeatherFactors f = climate.Factors;
        float T = Mathf.Clamp01(climate.Temperature);
        float M = Mathf.Clamp01(climate.Moisture);
        float size = Mathf.Max(100f, SystemSize);
        double drift = time * DriftSpeed;
        Vector2 across = new Vector2(-Wind.y, Wind.x);

        // Weather systems: a large pattern drifting with the wind and a second one drifting slower and at an angle,
        // so systems grow, merge and fade as they move instead of sliding past unchanged.
        float systems = 0.65f * Fbm01(x, y, drift, Wind, size, 2, 11)
            + 0.35f * Fbm01(x, y, drift * 0.6, Rotate(Wind, 35f), size * 0.7f, 1, 12);
        systems = Mathf.Clamp01((systems - 0.5f) * 2.4f + 0.5f);
        // Long wet and dry spells over whole regions.
        float regime = Noise01(x, y, drift * 0.15, across, size * 8f, 13);
        float convection = Noise01(x, y, drift * 1.2, Wind, size * 0.3f, 14);
        float gusts = Noise01(x, y, drift * 2.0, Wind, size * 0.2f, 15);
        float heatField = Noise01(x, y, drift * 0.3, Wind, size * 3f, 16);
        float fogField = Noise01(x, y, drift * 0.4, Wind, size * 0.8f, 17);
        float mistField = Noise01(x, y, drift * 0.5, Wind, size * 0.5f, 18);
        float hailField = Noise01(x, y, drift * 1.2, Wind, size * 0.25f, 19);
        float tornadoField = Noise01(x, y, drift * 1.5, Wind, size * 0.15f, 20);

        // Clouds and precipitation: wetter places (and wet spells) need a weaker system for it to rain.
        float threshold = Mathf.Lerp(0.8f, 0.5f, M) - 0.16f * (regime - 0.5f)
            - 0.12f * (Precipitation - 1f) - 0.1f * (f.Precipitation - 1f);
        float clouds = SmoothStep(threshold - 0.3f, threshold, systems);
        float precipitation = SmoothStep(threshold, threshold + 0.2f, systems)
            * Mathf.Clamp(f.Precipitation, 0f, 1.5f) * Mathf.Clamp(Precipitation, 0f, 1.5f);
        precipitation = Mathf.Clamp01(precipitation);

        // Day cycle: cooler nights, misty mornings, stormier afternoons.
        float morning = 0f, afternoon = 0f, night = 0f;
        if (dayTime >= 0f)
        {
            float hour = Mathf.Repeat(dayTime, 24f);
            morning = SmoothStep(4f, 6f, hour) * (1f - SmoothStep(8f, 10f, hour));
            afternoon = SmoothStep(12f, 14f, hour) * (1f - SmoothStep(18f, 20f, hour));
            night = 1f - SmoothStep(6f, 9f, hour) * (1f - SmoothStep(19f, 22f, hour));
        }

        // Rain or snow, by temperature (freezing is about 0.37).
        float snowShare = 1f - SmoothStep(0.33f, 0.4f, T - 0.02f * night);
        float rain = Mathf.Clamp01(precipitation * (1f - snowShare) * Mathf.Min(f.Rain, 2f));
        float snow = Mathf.Clamp01(precipitation * snowShare * Mathf.Min(f.Snow, 2f));

        // A biome multiplier above 1 doesn't only scale a kind of weather: it also widens the climate that allows
        // it, so e.g. a Desert preset on a mild biome still gets sandstorms and heat waves (just less often
        // than a hot, dry one). Below 1 it only scales it down. Snow always needs the cold.
        float thunderBoost = Boost(f.Thunderstorms);
        // Thunderstorms: storm cells inside the rain, where the air is warm and wet.
        float warmWet = SmoothStep(0.45f - 0.06f * thunderBoost, 0.62f - 0.06f * thunderBoost, T)
            * SmoothStep(0.3f - 0.08f * thunderBoost, 0.55f - 0.08f * thunderBoost, M);
        float thunder = Mathf.Clamp01(precipitation * SmoothStep(0.55f, 0.8f, convection) * warmWet * f.Thunderstorms * (1f + 0.3f * afternoon));
        float hail = Mathf.Clamp01(thunder * SmoothStep(0.6f, 0.85f, hailField) * (1f - SmoothStep(0.62f, 0.75f, T)) * f.Hail);

        // Wind: gusty everywhere, stronger in systems, storms and high up.
        float highUp = Mathf.Clamp01(climate.Altitude / 150f);
        float wind = 1.5f + 5f * gusts + 7f * clouds * systems + 4f * highUp + 9f * thunder;

        // Dry storms: where it is dry and warm, a strong system raises dust and sand instead of rain.
        float sandBoost = Boost(f.Sandstorms);
        float dry = 1f - SmoothStep(0.2f + 0.11f * sandBoost, 0.4f + 0.11f * sandBoost, M);
        float dust = Mathf.Clamp01(dry * SmoothStep(0.45f - 0.07f * sandBoost, 0.6f - 0.07f * sandBoost, T)
            * SmoothStep(0.72f, 0.9f, systems) * Mathf.Min(f.Sandstorms, 1.2f));
        wind += 14f * dust;
        // Blizzards: snow driven by strong gusts.
        wind += 10f * snow * SmoothStep(0.55f, 0.85f, gusts) * Mathf.Min(f.Blizzards, 2f);
        wind *= Mathf.Max(0f, Windiness) * Mathf.Max(0f, f.Wind);

        // Heat waves: hot, clear spells.
        float heatBoost = Boost(f.HeatWaves);
        float heat = Mathf.Clamp01(SmoothStep(0.7f - 0.07f * heatBoost, 0.84f - 0.07f * heatBoost, T + 0.2f * (heatField - 0.5f)) * (1f - clouds)
            * SmoothStep(0.55f - 0.02f * heatBoost, 0.75f - 0.02f * heatBoost, heatField) * Mathf.Min(f.HeatWaves, 1.2f) * (1f - 0.5f * night));
        // Fog: humid, calm air; low mist in the lowlands, most of all in the morning.
        float calm = 1f - SmoothStep(4f, 8f, wind);
        float fogBoost = Boost(f.Fog), mistBoost = Boost(f.Mist);
        float fog = Mathf.Clamp01(SmoothStep(0.45f - 0.1f * fogBoost, 0.75f - 0.1f * fogBoost, M) * calm * SmoothStep(0.52f - 0.08f * morning, 0.72f, fogField)
            * (1f - 0.5f * precipitation) * f.Fog);
        float lowland = 1f - SmoothStep(0.4f, 1f, climate.Altitude / 120f);
        float mist = Mathf.Clamp01(SmoothStep(0.4f - 0.1f * mistBoost, 0.7f - 0.1f * mistBoost, M) * (1f - SmoothStep(3f, 6.5f, wind))
            * SmoothStep(0.45f - 0.15f * morning, 0.65f, mistField) * lowland * f.Mist * (1f + 0.5f * morning));
        // Tornadoes: rare, from strong thunderstorms over warm, flat, open land.
        float tornado = Tornadoes
            ? Mathf.Clamp01(SmoothStep(0.3f, 0.7f, thunder) * SmoothStep(0.5f, 0.65f, T) * Mathf.Clamp01(climate.Flatness)
                * SmoothStep(0.72f, 0.88f, tornadoField) * f.Tornadoes * 2f)
            : 0f;
        wind += 10f * tornado;

        var state = new WeatherState
        {
            Clouds = Mathf.Clamp01(Mathf.Max(clouds, 0.8f * thunder)),
            Rain = rain,
            Snow = snow,
            Hail = hail,
            Thunder = thunder,
            Dust = dust,
            Fog = fog,
            Mist = mist,
            Heat = heat,
            Tornado = tornado,
            WindSpeed = wind,
            WindDirection = Rotate(Wind, 40f * (gusts - 0.5f)),
            Temperature = Mathf.Lerp(ColdestCelsius, HottestCelsius, T) - 4f * precipitation + 5f * heat - 4f * night,
            Humidity = Mathf.Clamp01(0.35f * M + 0.35f * clouds + 0.3f * Mathf.Max(precipitation, fog)),
        };
        state.Type = state.Classify();
        return state;
    }

    /// <summary>
    /// How often each kind of weather happens in a biome with default weather settings (fractions of the time,
    /// adding up to 1): its Ideal Temperature and Moisture, landform and <see cref="BiomeWeather"/>, sampled over a
    /// day of weather at a dozen places. For the editor's per-biome summary.
    /// </summary>
    public static float[] TypicalWeather(Biome biome, TerrainShapeMode shapeMode, int seed)
    {
        var counts = new float[System.Enum.GetValues(typeof(WeatherType)).Length];
        if (biome == null)
            return counts;
        var factors = new WeatherFactors();
        (biome.weather ?? new BiomeWeather()).AddTo(ref factors, 1f);
        LandformType landform = LandformGenerator.Effective(biome, shapeMode);
        bool high = landform == LandformType.Mountains || landform == LandformType.Glacial || landform == LandformType.Highlands || landform == LandformType.Plateau;
        float altitude = Mathf.Max(0f, biome.baseElevation) + (high ? 1.3f * biome.amplitude : 0.5f * biome.amplitude);
        float flatness = landform == LandformType.Plains || landform == LandformType.Wetland || landform == LandformType.Dunes ? 1f
            : landform == LandformType.Hills ? 0.6f
            : landform == LandformType.Classic ? 1f - Mathf.Clamp01((biome.amplitude - 6f) / 30f) : 0.1f;
        var climate = new WeatherClimate
        {
            Temperature = Mathf.Clamp01(biome.idealTemperature - 0.22f * Mathf.Clamp(altitude / 150f, 0f, 1.6f)),
            Moisture = biome.idealMoisture,
            Altitude = altitude,
            Flatness = flatness,
            Factors = factors.Normalized(),
        };
        var model = new WeatherModel { Seed = seed };
        int samples = 0;
        for (int place = 0; place < 12; place++)
        {
            float x = place * 7919f % 40000f, y = place * 3571f % 40000f;
            for (double time = 0; time < 24 * 3600; time += 300)
            {
                counts[(int)model.Evaluate(x, y, time, climate).Type]++;
                samples++;
            }
        }
        for (int k = 0; k < counts.Length; k++)
            counts[k] /= samples;
        return counts;
    }

    /// <summary>How far a biome multiplier is above 1 (0 to 2): how much it widens the climate its weather needs.</summary>
    private static float Boost(float multiplier)
    {
        return Mathf.Clamp(multiplier - 1f, 0f, 2f);
    }

    // ------------------------------------------------------------------ noise

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    /// <summary>One layer of noise (0-1) drifting along <paramref name="direction"/> by <paramref name="drift"/> world units.</summary>
    private float Noise01(float x, float y, double drift, Vector2 direction, float scale, int layer)
    {
        float u = (float)((x - direction.x * drift) / scale);
        float v = (float)((y - direction.y * drift) / scale);
        return 0.5f + 0.5f * Gradient(u, v, Seed * 7919 + layer * 104729);
    }

    private float Fbm01(float x, float y, double drift, Vector2 direction, float scale, int octaves, int layer)
    {
        float u = (float)((x - direction.x * drift) / scale);
        float v = (float)((y - direction.y * drift) / scale);
        float sum = 0f, norm = 0f, amplitude = 1f;
        for (int o = 0; o < octaves; o++)
        {
            sum += amplitude * Gradient(u, v, Seed * 7919 + layer * 104729 + o * 31);
            norm += amplitude;
            amplitude *= 0.5f;
            u = u * 2.03f + 17.1f;
            v = v * 2.03f + 5.3f;
        }
        return 0.5f + 0.5f * Mathf.Clamp(sum / norm * 1.25f, -1f, 1f);
    }

    /// <summary>2D gradient noise, about [-1, 1].</summary>
    private static float Gradient(float x, float y, int seed)
    {
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        float fx = x - ix, fy = y - iy;
        float a = Dot(Hash(ix, iy, seed), fx, fy);
        float b = Dot(Hash(ix + 1, iy, seed), fx - 1f, fy);
        float c = Dot(Hash(ix, iy + 1, seed), fx, fy - 1f);
        float d = Dot(Hash(ix + 1, iy + 1, seed), fx - 1f, fy - 1f);
        float ux = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f);
        float uy = fy * fy * fy * (fy * (fy * 6f - 15f) + 10f);
        return 1.4f * Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
    }

    private static uint Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u ^ (uint)seed * 0xCB1AB31Fu;
            h ^= h >> 13;
            h *= 0x5BD1E995u;
            h ^= h >> 15;
            return h;
        }
    }

    private static float Dot(uint hash, float x, float y)
    {
        float angle = (hash & 0xFFFF) * (Mathf.PI * 2f / 65536f);
        return Mathf.Cos(angle) * x + Mathf.Sin(angle) * y;
    }

    private static float SmoothStep(float from, float to, float value)
    {
        float t = Mathf.Clamp01((value - from) / Mathf.Max(1e-5f, to - from));
        return t * t * (3f - 2f * t);
    }
}

/// <summary>
/// Looks up the climate that decides the weather (<see cref="WeatherClimate"/>) from a terrain generator: the
/// biome at a position and around it (their Ideal Temperature, Ideal Moisture, landform and <see cref="BiomeWeather"/>,
/// the spot itself counting most, so the weather changes gradually across biome borders), the world's climate fields
/// (<see cref="ClimateGenerator"/>, with the terrain's rain shadows and coasts) and the height of the ground.
/// </summary>
public sealed class WeatherClimateSampler
{
    private readonly TerrainGenerator generator;
    private readonly TerrainHeightSampler sampler;
    private readonly TerrainClimate terrainClimate;
    private readonly int seed;
    private readonly float climateScale, seaLevel, coolingHeight, altitudeCooling, pointSpacing;
    private readonly TerrainShapeMode shapeMode;

    public WeatherClimateSampler(TerrainGenerator generator)
    {
        this.generator = generator;
        sampler = new TerrainHeightSampler(generator, generator.EnableWater ? WaterSettings.From(generator) : null);
        terrainClimate = generator.TerrainClimate;
        seed = generator.VoronoiSeed;
        climateScale = generator.ClimateNoiseScale;
        seaLevel = generator.SeaLevel;
        // The height over which it gets markedly colder: the snow line when there is one.
        coolingHeight = generator.SnowLineHeight > 0f ? Mathf.Max(20f, generator.SnowLineHeight) : 150f;
        altitudeCooling = Mathf.Clamp01(generator.AltitudeCooling);
        shapeMode = generator.TerrainShapeMode;
        pointSpacing = generator.VoronoiScale / Mathf.Sqrt(Mathf.Max(1, generator.NumVoronoiPoints));
    }

    public TerrainGenerator Generator => generator;

    /// <summary>
    /// The climate at a position (terrain generator coordinates), averaged over <paramref name="radius"/> world
    /// units. <paramref name="biomeInfluence"/>: how much the biomes' own climate counts against the world's
    /// climate fields (1 = only the biomes).
    /// </summary>
    public WeatherClimate Sample(float x, float y, float radius, float biomeInfluence)
    {
        float biomeTemperature = 0f, biomeMoisture = 0f, flatness = 0f, total = 0f;
        var factors = new WeatherFactors();
        // The spot itself counts for half, four points around it for the rest - no further than about a third of
        // the distance between biome points, so it stays the weather of this biome, only softened at its borders.
        radius = Mathf.Min(radius, 0.35f * pointSpacing);
        for (int k = 0; k < 5; k++)
        {
            float sx = x + (k == 1 ? radius : k == 2 ? -radius : 0f);
            float sy = y + (k == 3 ? radius : k == 4 ? -radius : 0f);
            float w = k == 0 ? 4f : 1f;
            // The biome actually there (the one whose ground and objects the player sees), not the terrain's
            // height blend, which mixes neighbors over wide bands.
            Biome biome = sampler.SampleBiome(sx, sy);
            if (biome == null)
                continue;
            biomeTemperature += biome.idealTemperature * w;
            biomeMoisture += biome.idealMoisture * w;
            flatness += Flatness(biome) * w;
            (biome.weather ?? new BiomeWeather()).AddTo(ref factors, w);
            total += w;
        }

        Vector2 position = new Vector2(x, y);
        float worldTemperature = ClimateGenerator.GetTemperature(position, seed, climateScale);
        float worldMoisture = ClimateGenerator.GetMoisture(position, seed, climateScale);
        if (terrainClimate != null)
            terrainClimate.Apply(position, ref worldTemperature, ref worldMoisture);

        float influence = total > 0f ? Mathf.Clamp01(biomeInfluence) : 0f;
        float temperature = Mathf.Lerp(worldTemperature, total > 0f ? biomeTemperature / total : worldTemperature, influence);
        float moisture = Mathf.Lerp(worldMoisture, total > 0f ? biomeMoisture / total : worldMoisture, influence);

        // Colder high up: at the snow line (or 150 units up without one) about 15 degrees colder at the default
        // Altitude Cooling, more above it.
        float altitude = Mathf.Max(0f, sampler.SampleBaseHeight(x, y) - seaLevel);
        temperature -= (0.12f + 0.2f * altitudeCooling) * Mathf.Clamp(altitude / coolingHeight, 0f, 1.6f);

        return new WeatherClimate
        {
            Temperature = Mathf.Clamp01(temperature),
            Moisture = Mathf.Clamp01(moisture),
            Altitude = altitude,
            Flatness = total > 0f ? flatness / total : 0.5f,
            Factors = factors.Normalized(),
        };
    }

    private float Flatness(Biome biome)
    {
        switch (LandformGenerator.Effective(biome, shapeMode))
        {
            case LandformType.Plains:
            case LandformType.Wetland:
            case LandformType.Dunes:
                return 1f;
            case LandformType.Hills:
                return 0.6f;
            case LandformType.Classic:
                return 1f - Mathf.Clamp01((biome.amplitude - 6f) / 30f);
            default:
                return 0.1f;
        }
    }
}
