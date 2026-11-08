using System;
using UnityEngine;

/// <summary>What water from a source carries into the soil (per millimetre poured or rained).</summary>
[Serializable]
public struct WaterQuality
{
    [Tooltip("Dissolved minerals (0-1): hard water adds calcium and magnesium and slowly raises the pH.")]
    [Range(0f, 1f)] public float minerals;
    [Tooltip("Salt in the water (dS/m): it builds up in soil that does not drain.")]
    [Range(0f, 10f)] public float salinity;
    [Tooltip("Dirt and germs (0-1): they feed the diseases living in the soil.")]
    [Range(0f, 1f)] public float impurity;
    [Tooltip("Magical essence (0-1).")]
    [Range(0f, 1f)] public float magic;
    [Tooltip("The water's own pH: soil watered with it drifts towards it, very slowly.")]
    [Range(3f, 10f)] public float ph;
    [Tooltip("Nitrogen it carries (ppm added per mm): alchemically enriched water feeds a little.")]
    [Range(0f, 5f)] public float nitrogen;

    public WaterQuality(float minerals, float salinity, float impurity, float magic, float ph, float nitrogen = 0f)
    {
        this.minerals = minerals; this.salinity = salinity; this.impurity = impurity; this.magic = magic; this.ph = ph; this.nitrogen = nitrogen;
    }

    public static WaterQuality Default(WaterSourceKind k)
    {
        switch (k)
        {
            case WaterSourceKind.Rain: return new WaterQuality(0f, 0f, 0f, 0f, 5.6f);
            case WaterSourceKind.River: return new WaterQuality(0.3f, 0.25f, 0.25f, 0f, 7.2f);
            case WaterSourceKind.Well: return new WaterQuality(0.65f, 0.5f, 0.05f, 0f, 7.7f);
            case WaterSourceKind.Lake: return new WaterQuality(0.25f, 0.35f, 0.2f, 0f, 7.4f);
            case WaterSourceKind.Swamp: return new WaterQuality(0.2f, 1.2f, 0.8f, 0.02f, 5.3f);
            case WaterSourceKind.Spring: return new WaterQuality(0.5f, 0.1f, 0f, 0.03f, 7.0f);
            case WaterSourceKind.Alchemical: return new WaterQuality(0.05f, 0f, 0f, 0.1f, 6.5f, 0.6f);
            case WaterSourceKind.Magical: return new WaterQuality(0.1f, 0f, 0f, 0.7f, 6.8f);
            default: return new WaterQuality(0.2f, 0.2f, 0.1f, 0f, 7f);
        }
    }
}

/// <summary>The weather of one season in the city's climate.</summary>
[Serializable]
public class AgriSeasonClimate
{
    [Tooltip("Mean air temperature, °C.")]
    public float meanTemperature = 14f;
    [Tooltip("Difference between the warmest afternoon and the coldest night of a clear day, °C.")]
    public float dailySwing = 9f;
    [Tooltip("Chance that it rains on a given day (0-1).")]
    [Range(0f, 1f)] public float rainChance = 0.35f;
    [Tooltip("Typical rain while it rains, mm per hour.")]
    public float rainIntensity = 2.5f;
    [Tooltip("Usual air humidity (0-1).")]
    [Range(0f, 1f)] public float humidity = 0.6f;
    [Tooltip("Usual cloud cover (0-1).")]
    [Range(0f, 1f)] public float clouds = 0.4f;
    [Tooltip("Chance a heat wave (summer) or a cold snap (other seasons) starts on a given day.")]
    [Range(0f, 1f)] public float extremeChance = 0.06f;
    [Tooltip("Typical wind, m/s.")]
    public float wind = 3f;
    [Tooltip("Chance that a rainy day is a storm (heavy rain and wind).")]
    [Range(0f, 1f)] public float stormChance = 0.1f;

    public AgriSeasonClimate() { }

    public AgriSeasonClimate(float mean, float swing, float rain, float intensity, float humidity, float clouds, float extreme, float wind, float storm)
    {
        meanTemperature = mean; dailySwing = swing; rainChance = rain; rainIntensity = intensity; this.humidity = humidity;
        this.clouds = clouds; extremeChance = extreme; this.wind = wind; stormChance = storm;
    }
}

/// <summary>
/// The climate of places without a Weather System (the city): four seasons of weather, worked out the same on every
/// machine for any day and hour (rain, frosts, heat waves, storms), so plants can catch up over days they were not seen.
/// </summary>
[Serializable]
public class AgriClimate
{
    public AgriSeasonClimate spring = new AgriSeasonClimate(15f, 9f, 0.35f, 2.2f, 0.62f, 0.42f, 0.07f, 3.5f, 0.06f);
    public AgriSeasonClimate summer = new AgriSeasonClimate(24f, 10f, 0.22f, 3.5f, 0.55f, 0.28f, 0.1f, 2.5f, 0.25f);
    public AgriSeasonClimate autumn = new AgriSeasonClimate(15f, 8f, 0.4f, 2.6f, 0.7f, 0.5f, 0.06f, 4.5f, 0.12f);
    public AgriSeasonClimate winter = new AgriSeasonClimate(3f, 6f, 0.3f, 1.6f, 0.8f, 0.62f, 0.12f, 4f, 0.05f);
    [Tooltip("Mixes the weather of the days: the same seed gives the same weather.")]
    public int seed = 1337;
    [Tooltip("Day-to-day randomness of the temperature, °C.")]
    public float temperatureNoise = 3f;

    public AgriSeasonClimate Of(Season s)
    {
        switch (s)
        {
            case Season.Spring: return spring ??= new AgriSeasonClimate();
            case Season.Summer: return summer ??= new AgriSeasonClimate();
            case Season.Autumn: return autumn ??= new AgriSeasonClimate();
            default: return winter ??= new AgriSeasonClimate();
        }
    }
}

/// <summary>
/// The rules of agriculture: time and growth speed, the calendar, the city's climate, the soil's chemistry, pests,
/// pollination and genetics, skills, the work, saving. Held by the <see cref="AgricultureDatabase"/>.
/// </summary>
[Serializable]
public class AgriSettings
{
    [Header("Time")]
    [Tooltip("Multiplies every growth and maturing duration's speed (2 = twice as fast).")]
    [Min(0.01f)] public float growthSpeed = 1f;
    [Tooltip("Game hours between two updates of a plant. Plants catch up in steps when time jumps (sleeping, being away).")]
    [Range(0.25f, 6f)] public float tickHours = 1f;
    [Tooltip("Longest step of a catch-up, game hours.")]
    [Range(0.5f, 24f)] public float maxStepHours = 3f;
    [Tooltip("Most plants updated in one frame, over every field: catching up after days away is spread over frames.")]
    [Range(1, 2000)] public int plantsPerFrame = 64;
    [Tooltip("Game hours between two samples of the weather over a field.")]
    [Range(0.05f, 2f)] public float environmentStepHours = 0.25f;
    [Tooltip("Real minutes in a game day when no Day Night Cycle is in the game (otherwise it decides).")]
    [Min(0.5f)] public float dayLengthMinutes = 24f;

    [Header("Sleeping gardens")]
    [Tooltip("A garden with no player near sleeps: it is saved and not simulated; when someone comes near it is brought up to date with the weather that fell meanwhile, hour by hour (where the rules run: offline or the server).")]
    public bool sleepWhenAlone = true;
    [Tooltip("Metres from a garden's outline within which a player wakes it. Keep it above the fields' Draw Distance, so a garden is awake before anyone sees it.")]
    [Min(1f)] public float wakeRadius = 120f;
    [Tooltip("Metres from a garden's outline: with no player this near for Sleep Delay, it falls asleep.")]
    [Min(1f)] public float sleepRadius = 150f;
    [Tooltip("Real seconds with no player near before a garden falls asleep.")]
    [Range(0f, 600f)] public float sleepDelaySeconds = 20f;
    [Tooltip("A sleeping garden is also freed from memory (loaded again from its save when it wakes). Needs Save Fields; gardens that are not saved stay in memory.")]
    public bool unloadWhenAsleep = true;
    [Tooltip("Game hours a sleeping garden may lag behind before it is brought up to date in the background: an abandoned garden keeps living, and waking is quick.")]
    [Min(1f)] public float maintenanceLagHours = 72f;
    [Tooltip("Real milliseconds a frame for bringing gardens up to date (waking ones first, then the background).")]
    [Range(0.25f, 20f)] public float catchUpMillisPerFrame = 2f;

    [Header("Catching up")]
    [Tooltip("A garden with no living plant (empty, or only the dead) catches up in long steps: its soil is worked out only when something touches it, so only the weather is replayed - the same weather, sample by sample, and the same result.")]
    public bool fastForwardBareGardens = true;
    [Tooltip("While a garden catches up, a plant looks at its soil and its neighbours again only every Quiet Hours while nothing they could change happens (no rain, no work on the soil, no pests or diseases near, its stage and roots the same, its water well within what it likes). The weather, frost and ripening still come every step, so the result stays the same.")]
    public bool quietSteps = true;
    [Tooltip("Longest a plant goes on with what it last saw of its soil and neighbours during a quiet stretch, game hours.")]
    [Range(1f, 24f)] public float quietHours = 6f;
    [Tooltip("Each soil cell keeps what it can feed at its pH and its water figures until they change, so plants sharing cells do not work them out again.")]
    public bool cacheCellValues = true;

    [Header("Calendar")]
    [Tooltip("Game days in a season (four seasons make a year).")]
    [Range(1, 120)] public int daysPerSeason = 12;
    [Tooltip("Game days from one new moon to the next (each of the four phases lasts a quarter of it).")]
    [Range(4, 60)] public int lunarCycleDays = 8;
    [Tooltip("The season of the first day.")]
    public Season startSeason = Season.Spring;
    [Tooltip("The hour of the first day when no Day Night Cycle exists.")]
    [Range(0f, 24f)] public float startHour = 8f;

    [Header("Climate")]
    [Tooltip("The weather of places without a Weather System (the city).")]
    public AgriClimate climate = new AgriClimate();
    [Tooltip("Fields in a scene with a Weather System use its weather instead.")]
    public bool useWorldWeather = true;
    [Tooltip("With the world's weather, add the seasons' temperature differences (the Weather System has no seasons).")]
    public bool seasonsOnWorldWeather = true;

    [Header("Soil")]
    [Tooltip("Multiplies how fast the soil dries.")]
    [Min(0f)] public float evaporation = 1f;
    [Tooltip("Multiplies how much nitrogen and salts drain away with the water.")]
    [Min(0f)] public float leaching = 1f;
    [Tooltip("Multiplies how fast organic matter and slow fertilizers release their nutrients.")]
    [Min(0f)] public float mineralization = 1f;

    [Header("Plants")]
    [Tooltip("Multiplies the water plants use.")]
    [Min(0f)] public float waterUse = 1f;
    [Tooltip("Multiplies the nutrients plants take from the soil.")]
    [Min(0f)] public float nutrientUse = 1f;
    [Tooltip("Multiplies the damage of drought, frost, heat, salt, pests and diseases.")]
    [Min(0f)] public float stressDamage = 1f;
    [Tooltip("Game days a dead plant stays before it is gone.")]
    [Min(0f)] public float deadPlantDays = 3f;

    [Header("Pests and diseases")]
    [Min(0f)] public float pestPressure = 1f;
    [Min(0f)] public float diseasePressure = 1f;
    [Tooltip("Multiplies how fast pests and diseases spread from plant to plant.")]
    [Min(0f)] public float spread = 1f;

    [Header("Pollination and genetics")]
    [Tooltip("Multiplies the work of bees and other pollinators.")]
    [Min(0f)] public float pollinators = 1f;
    [Tooltip("Chance a seed's trait mutates one step (per trait).")]
    [Range(0f, 0.5f)] public float mutationChance = 0.04f;
    [Tooltip("Chance a seed carries a rare trait.")]
    [Range(0f, 0.1f)] public float rareTraitChance = 0.003f;

    [Header("Skills")]
    [Range(1, 200)] public int maxLevel = 50;
    [Min(1f)] public float xpFirstLevel = 20f;
    [Range(1f, 1.5f)] public float xpGrowth = 1.08f;
    [Tooltip("Multiplies all the experience gained.")]
    [Min(0f)] public float xpMultiplier = 1f;
    [Tooltip("Share of the agricultural experience also given to the character's Experience Manager.")]
    [Range(0f, 1f)] public float characterXpShare = 0.2f;

    [Header("Work")]
    [Tooltip("How far from the player the hands reach, metres.")]
    [Range(0.5f, 6f)] public float workRange = 2.4f;
    [Tooltip("Longest a hold (digging, pouring) counts, seconds.")]
    [Range(1f, 30f)] public float maxHoldSeconds = 12f;

    [Header("Saving")]
    [Tooltip("Fields keep their soil and plants on disk (where the rules run: offline or the server).")]
    public bool saveFields = true;
    [Tooltip("Real minutes between two saves of a changed field.")]
    [Min(0.1f)] public float autosaveMinutes = 2f;

    [Header("Water")]
    [Tooltip("What each kind of water carries (indexed by Water Source Kind).")]
    public WaterQuality[] waterQuality = DefaultWater();

    public static AgriSettings Defaults() => new AgriSettings();

    public static WaterQuality[] DefaultWater()
    {
        var w = new WaterQuality[8];
        for (int i = 0; i < w.Length; i++)
            w[i] = WaterQuality.Default((WaterSourceKind)i);
        return w;
    }

    public WaterQuality Water(WaterSourceKind k)
    {
        int i = (int)k;
        return waterQuality != null && i >= 0 && i < waterQuality.Length ? waterQuality[i] : WaterQuality.Default(k);
    }

    public int YearDays => Math.Max(4, daysPerSeason * 4);

    // ---------------------------------------------------------------- skills
    public float XpToNext(int level) => xpFirstLevel * (float)Math.Pow(xpGrowth, Math.Max(0, level));

    public int Level(float xp)
    {
        int level = 0;
        float need = XpToNext(0);
        while (level < maxLevel && xp >= need)
        {
            xp -= need;
            level++;
            need = XpToNext(level);
        }
        return level;
    }

    /// <summary>0-1 of the way to the next level.</summary>
    public float Progress(float xp)
    {
        int level = 0;
        float need = XpToNext(0);
        while (level < maxLevel && xp >= need)
        {
            xp -= need;
            level++;
            need = XpToNext(level);
        }
        return level >= maxLevel ? 1f : Mathf.Clamp01(xp / need);
    }
}
