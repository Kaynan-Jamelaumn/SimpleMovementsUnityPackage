using UnityEngine;

/// <summary>
/// Makes the climate react to the large-scale terrain, on top of <see cref="ClimateGenerator"/>'s noise fields:
/// <list type="bullet">
/// <item>Rain shadow: moist air carried by the prevailing wind rains out as it rises over a mountain belt, so the
/// windward side gets wetter and the land behind the range (downwind) gets drier - often desert or steppe.</item>
/// <item>Altitude cooling: mountain belts, and continental interiors that rise inland, are colder.</item>
/// <item>Coastal moisture: land near the sea is wetter, deep continental interiors drier.</item>
/// </list>
/// It is built only from fields that don't depend on which biome is where - the mountain-belt field that
/// places mountain landforms (<see cref="LandformGenerator.MountainBelt"/>) and the continent field
/// (<see cref="OceanGenerator.LandSide"/>) - because biomes are chosen from the climate: using actual terrain
/// heights here would be circular. Deterministic: depends only on position, seed and settings.
/// </summary>
public sealed class TerrainClimate
{
    // Rain shadow: how far upwind (in belt spacings) a range still dries the air, and how it is sampled.
    // Steps well below a belt's width, so a range is never stepped over (that leaves stripes).
    private const int UpwindSamples = 15;
    private const float UpwindStep = 0.02f;
    private const int AheadSamples = 4;
    private const float ShadowDrying = 0.55f;
    private const float WindwardWetting = 0.3f;
    // Altitude cooling at the heart of a mountain belt (0-1 temperature scale), for Altitude Cooling = 1.
    private const float BeltCooling = 0.4f;
    // Extra cooling of a fully risen continental interior, relative to the snow line.
    private const float InlandCooling = 0.2f;
    // Coastal moisture: wetter within CoastReach of the sea, drier deep inland.
    private const float CoastWetting = 0.2f;
    private const float InteriorDrying = 0.15f;
    /// <summary>Spacing (world units) of the lattice <see cref="MoistureGrid"/> samples the shift on.</summary>
    public const int GridStep = 16;

    public int Seed;
    public float BeltScale;
    /// <summary>0-1: how closely mountain belts follow real mountains (0 when landforms or belts are off).</summary>
    public float BeltInfluence;
    /// <summary>Unit vector the prevailing wind blows toward (world X, world Z).</summary>
    public Vector2 Wind = new Vector2(1f, 0f);
    public float RainShadow;
    public float AltitudeCooling;
    public float CoastalMoisture;
    /// <summary>Water settings when oceans are on (for the continent field), otherwise null.</summary>
    public WaterSettings Oceans;
    public float SnowLineHeight = 90f;

    /// <summary>The terrain climate for a generator's settings, or null when it is turned off.</summary>
    public static TerrainClimate From(TerrainGenerator tg)
    {
        if (!tg.TerrainAwareClimate)
            return null;

        bool belts = tg.MountainBeltStrength > 0f && HasMountainLandforms(tg);
        WaterSettings oceans = tg.EnableWater && tg.EnableOceans ? WaterSettings.From(tg) : null;
        var climate = new TerrainClimate
        {
            Seed = tg.VoronoiSeed,
            BeltScale = tg.MountainBeltScale,
            // At the default belt strength (0.5) mountain landforms already sit almost only in the belts.
            BeltInfluence = belts ? Mathf.Clamp01(tg.MountainBeltStrength * 2f) : 0f,
            Wind = WindDirection(tg.PrevailingWindAngle),
            RainShadow = Mathf.Clamp01(tg.RainShadowStrength),
            AltitudeCooling = Mathf.Clamp01(tg.AltitudeCooling),
            CoastalMoisture = Mathf.Clamp01(tg.CoastalMoisture),
            Oceans = oceans,
            SnowLineHeight = Mathf.Max(1f, tg.SnowLineHeight),
        };
        return climate.HasEffect ? climate : null;
    }

    /// <summary>
    /// True when some land biome gets a mountain-type landform - only then do the mountain belts gather real
    /// mountains (see <see cref="LandformGenerator.PlacementAffinity"/>), so only then can they cast rain shadows.
    /// </summary>
    private static bool HasMountainLandforms(TerrainGenerator tg)
    {
        if (tg.TerrainShapeMode == TerrainShapeMode.ClassicOnly || tg.BiomeDefinitions == null)
            return false;
        foreach (BiomeInstance definition in tg.BiomeDefinitions)
        {
            Biome biome = definition != null ? definition.BiomePrefab : null;
            if (biome == null || biome.placement != BiomePlacement.Land)
                continue;
            LandformType landform = LandformGenerator.Effective(biome, tg.TerrainShapeMode);
            if (landform == LandformType.Mountains || landform == LandformType.Glacial || landform == LandformType.Plateau)
                return true;
        }
        return false;
    }

    /// <summary>Direction the wind blows toward: 0 degrees = +X (east), 90 = +Z (north).</summary>
    public static Vector2 WindDirection(float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    /// <summary>False when every effect is zero (nothing would change).</summary>
    public bool HasEffect =>
        (BeltInfluence > 0f && (RainShadow > 0f || AltitudeCooling > 0f))
        || (Oceans != null && (CoastalMoisture > 0f || AltitudeCooling > 0f));

    /// <summary>Climate at a position with the terrain's influence applied (both 0-1).</summary>
    public void Apply(Vector2 position, ref float temperature, ref float moisture)
    {
        Shifts(position, out float temperatureShift, out float moistureShift);
        temperature = Mathf.Clamp01(temperature + temperatureShift);
        moisture = Mathf.Clamp01(moisture + moistureShift);
    }

    /// <summary>How much the terrain raises (+) or lowers (-) temperature and moisture at a position.</summary>
    public void Shifts(Vector2 position, out float temperature, out float moisture)
    {
        temperature = 0f;
        moisture = 0f;

        if (BeltInfluence > 0f)
        {
            float here = Belt(position);
            temperature -= AltitudeCooling * BeltCooling * here;

            if (RainShadow > 0f)
            {
                // Highest range upwind (fading with distance): the air reaching this point has crossed it.
                float step = BeltScale * UpwindStep;
                float upwind = 0f;
                for (int k = 1; k <= UpwindSamples; k++)
                {
                    float fade = 1f - 0.5f * (k - 1) / UpwindSamples;
                    upwind = Mathf.Max(upwind, Belt(position - Wind * (step * k)) * fade);
                }

                // Rising toward a range with nothing blocking the air before it: the wet, windward side.
                float ahead = here;
                for (int k = 1; k <= AheadSamples; k++)
                    ahead = Mathf.Max(ahead, Belt(position + Wind * (step * k)));

                float shadow = Mathf.Max(0f, upwind - here);
                float windward = Mathf.Max(0f, ahead - upwind);
                moisture += RainShadow * (WindwardWetting * windward - ShadowDrying * shadow);
            }
        }

        if (Oceans != null)
        {
            float side = OceanGenerator.LandSide(Oceans, position.x, position.y);
            float inlandDistance = side / Oceans.ContinentGradient;

            if (AltitudeCooling > 0f && Oceans.InlandRise > 0f)
            {
                float rise = Oceans.InlandRise * WaterGenerator.SmoothStep01(inlandDistance / Oceans.InlandRiseDistance);
                temperature -= AltitudeCooling * InlandCooling * Mathf.Clamp01(rise / SnowLineHeight);
            }

            if (CoastalMoisture > 0f && inlandDistance > 0f)
            {
                float reach = Mathf.Max(50f, Oceans.ContinentScale * 0.12f);
                float coast = 1f - WaterGenerator.SmoothStep01(inlandDistance / reach);
                float interior = WaterGenerator.SmoothStep01((inlandDistance - reach) / (3f * reach));
                moisture += CoastalMoisture * (CoastWetting * coast - InteriorDrying * interior);
            }
        }
    }

    private float Belt(Vector2 position)
    {
        return LandformGenerator.MountainBelt(position, Seed, BeltScale) * BeltInfluence;
    }

    /// <summary>
    /// The moisture shift over an area on a world-aligned lattice (every <see cref="GridStep"/> units), for cheap
    /// per-cell lookups: the shift varies over hundreds of units, so bilinear interpolation between lattice points
    /// is indistinguishable from evaluating every cell. The lattice is aligned to world coordinates, so any two
    /// areas get exactly the same value for the same cell.
    /// </summary>
    public Grid MoistureGrid(int originX, int originY, int size)
    {
        int x0 = FloorDiv(originX, GridStep);
        int y0 = FloorDiv(originY, GridStep);
        int x1 = FloorDiv(originX + size, GridStep) + 1;
        int y1 = FloorDiv(originY + size, GridStep) + 1;
        var grid = new Grid(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        for (int j = 0; j < grid.Height; j++)
            for (int i = 0; i < grid.Width; i++)
            {
                Shifts(new Vector2((x0 + i) * GridStep, (y0 + j) * GridStep), out _, out float moisture);
                grid.Values[j * grid.Width + i] = moisture;
            }
        return grid;
    }

    public sealed class Grid
    {
        public readonly int X0, Y0, Width, Height;
        public readonly float[] Values;

        public Grid(int x0, int y0, int width, int height)
        {
            X0 = x0; Y0 = y0; Width = width; Height = height;
            Values = new float[width * height];
        }

        /// <summary>Bilinear lookup at an integer world cell.</summary>
        public float At(int worldX, int worldY)
        {
            int cx = FloorDiv(worldX, GridStep), cy = FloorDiv(worldY, GridStep);
            float fx = (worldX - cx * GridStep) / (float)GridStep;
            float fy = (worldY - cy * GridStep) / (float)GridStep;
            int i = cx - X0, j = cy - Y0;
            float a = Values[j * Width + i], b = Values[j * Width + i + 1];
            float c = Values[(j + 1) * Width + i], d = Values[(j + 1) * Width + i + 1];
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
    }

    private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);
}
