using System;
using System.Collections.Concurrent;
using UnityEngine;

/// <summary>
/// Oceans are a world-scale geographic feature, decided by a very low-frequency, domain-warped
/// "continent" field - never by terrain simply being low. A large inland low area therefore stays land
/// (or becomes a lake, see <see cref="LakeGenerator"/>) instead of silently turning into sea.
///
/// The field also shapes the terrain around it: a beach that ramps up from sea level on the land side,
/// a continental shelf that drops to the deep seafloor on the ocean side, a gentle inland rise so land
/// generally climbs away from coasts (which is what gives rivers a tendency to drain toward the sea),
/// and occasional islands out in open water. Everything is continuous across the coastline, and a pure
/// function of world position, so neighboring chunks always agree on where the coast is.
///
/// Coasts vary along their length: a slow "coast character" field makes some stretches sandy beaches,
/// others rocky shores and others tall, tiered cliffs (with deeper water and boulders at their foot),
/// and off the most rugged cliff coasts the occasional sea stack rises out of the water.
/// </summary>
public static class OceanGenerator
{
    private const float WarpSaltX = 911.3f;
    private const float WarpSaltY = 1733.9f;
    private const float ContinentSalt = 2477.1f;
    private const float CoastDetailSalt = 2819.3f;
    private const float IslandSalt = 3121.7f;
    private const float CoastCharacterSalt = 3517.3f;
    private const float CliffSalt = 3907.9f;
    private const float RockSalt = 4211.1f;
    private const float CoastDetailStrength = 0.3f;
    // How far (in continent-noise units) from the threshold the large-scale component has to be before
    // coastline detail stops being able to flip land/ocean.
    private const float CoastDetailBand = 0.1f;

    /// <summary>
    /// Signed "how far inland" value in continent-noise units: positive = land, negative = ocean, 0 = the
    /// coastline. Multiply by 1/<see cref="WaterSettings.ContinentGradient"/> for a rough world distance.
    /// </summary>
    public static float LandSide(WaterSettings s, float x, float y)
    {
        float scale = s.ContinentScale;
        float warpScale = scale * 0.3f;
        float warpStrength = scale * 0.1f;

        // Warping the lookup position turns smooth noise blobs into irregular coastlines with bays and
        // headlands instead of round, obviously-noise-shaped continents.
        float warpX = WaterGenerator.Fbm(x / warpScale, y / warpScale, s.Seed, WarpSaltX, 3) * warpStrength;
        float warpY = WaterGenerator.Fbm(x / warpScale, y / warpScale, s.Seed, WarpSaltY, 3) * warpStrength;
        float wx = x + warpX;
        float wy = y + warpY;

        // Large-scale continents/ocean basins, plus finer detail that roughens the coastline.
        float continent = WaterGenerator.Fbm(wx / scale, wy / scale, s.Seed, ContinentSalt, 2);
        float detail = WaterGenerator.Fbm(wx / (scale * 0.35f), wy / (scale * 0.35f), s.Seed, CoastDetailSalt, 3);

        if (s.SpawnLandRadius > 0f)
        {
            // Keeps the area around the world origin (the usual spawn point) on land.
            float r2 = (x * x + y * y) / (s.SpawnLandRadius * s.SpawnLandRadius);
            continent += 0.8f * Mathf.Exp(-r2);
        }

        float side = continent + CoastDetailStrength * detail - s.OceanThreshold;

        // Once the large-scale component is clearly inland (or clearly offshore), it alone decides:
        // detail can only reshape the coastline near an actual coast. Without this, detail noise punches
        // small isolated "seas" into the middle of continents - which would read as lakes, not ocean.
        // Piecewise-linear, so the field stays continuous.
        float largeScale = continent - s.OceanThreshold;
        if (largeScale > CoastDetailBand)
            side += (largeScale - CoastDetailBand) * 2f;
        else if (largeScale < -CoastDetailBand)
            side += (largeScale + CoastDetailBand) * 2f;

        return side;
    }

    /// <summary>
    /// 0 = beach, 1 = cliff, in between = rocky shore. Varies slowly along the coast, so beaches, rocky
    /// stretches and cliffs alternate; <see cref="WaterSettings.CliffFrequency"/> sets how much is cliff.
    /// </summary>
    public static float CoastCharacter(WaterSettings s, float x, float y)
    {
        if (s.CliffFrequency <= 0f)
            return 0f;
        float n = 0.5f + 1.7f * WaterGenerator.Fbm(x / 520f, y / 520f, s.Seed, CoastCharacterSalt, 3);
        float threshold = 1f - s.CliffFrequency;
        return WaterGenerator.SmoothStep01((n - threshold + 0.2f) / 0.4f);
    }

    /// <summary>Height of the cliff tops along this stretch of coast (varies along the coast).</summary>
    public static float CliffTop(WaterSettings s, float x, float y)
    {
        return s.CliffHeight * (0.55f + 0.45f * (0.5f + 0.7f * WaterGenerator.Fbm(x / 260f, y / 260f, s.Seed, CliffSalt, 2)));
    }

    /// <summary>
    /// Shapes biome-blended land height around coastlines and replaces it with seafloor in the ocean.
    /// </summary>
    /// <param name="landHeight">Biome-blended land height at this position (including baseElevation).</param>
    /// <param name="landRelief">The noise-only part of <paramref name="landHeight"/> (baseElevation removed), reused as seafloor/island detail.</param>
    /// <param name="landSide">Output: see <see cref="LandSide"/>.</param>
    public static float ShapeHeight(WaterSettings s, float x, float y, float landHeight, float landRelief, out float landSide)
    {
        landSide = LandSide(s, x, y);
        return ShapeHeight(s, x, y, landHeight, landRelief, landSide, float.NaN);
    }

    /// <summary>
    /// As above, with <see cref="LandSide"/> already known, and optionally the seafloor shape of an ocean
    /// biome (<paramref name="seafloorRelief"/>, relative to the normal seafloor; NaN = none, in which case
    /// the land relief is reused as gentle seafloor undulation).
    /// </summary>
    public static float ShapeHeight(WaterSettings s, float x, float y, float landHeight, float landRelief, float side, float seafloorRelief)
    {
        float g = s.ContinentGradient;
        float character = CoastCharacter(s, x, y);

        if (side >= 0f)
        {
            float inland = landHeight + s.InlandRise * WaterGenerator.SmoothStep01(side / (s.InlandRiseDistance * g));
            float coastal = s.SeaLevel + s.BeachHeight * WaterGenerator.SmoothStep01(side / (s.BeachWidth * g));
            float blendWidth = s.CoastBlendWidth;
            if (character > 0f)
            {
                coastal = Mathf.Lerp(coastal, CliffProfile(s, x, y, side / g), character);
                // Cliff tops stay a while before the land takes over.
                blendWidth *= 1f + 1.5f * character;
            }
            return Mathf.Lerp(coastal, inland, WaterGenerator.SmoothStep01(side / (blendWidth * g)));
        }

        float offshore = -side;
        float distance = offshore / g;
        // Deep water comes close to cliff coasts; beaches get the full, gently shelving seabed.
        float shelfWidth = s.ShelfWidth * (1f - 0.75f * character);
        float shelf = WaterGenerator.SmoothStep01(offshore / (shelfWidth * g));
        float floor;
        if (float.IsNaN(seafloorRelief))
        {
            float undulation = Mathf.Clamp(landRelief * 0.15f, -s.OceanDepth * 0.3f, s.OceanDepth * 0.3f);
            floor = s.SeaLevel - (s.OceanDepth - undulation) * shelf;
        }
        else
        {
            // An ocean biome's seafloor shape, faded in away from the shore; never breaks the surface.
            floor = s.SeaLevel - s.OceanDepth * shelf
                    + seafloorRelief * WaterGenerator.SmoothStep01(offshore / (0.5f * s.ShelfWidth * g));
            floor = Mathf.Min(floor, s.SeaLevel - 0.6f * WaterGenerator.SmoothStep01(distance / 3f));
        }

        if (character > 0f)
        {
            // Boulders and rocky shallows at the foot of rocky shores and cliffs.
            float rocks = WaterGenerator.SmoothStep01((0.5f + 0.7f * WaterGenerator.Fbm(x / 7f, y / 7f, s.Seed, RockSalt, 2) - 0.55f) / 0.3f);
            float nearShore = WaterGenerator.SmoothStep01(distance / 4f) * (1f - WaterGenerator.SmoothStep01(distance / 45f));
            if (nearShore > 0f)
                floor = Mathf.Min(floor + rocks * 3.5f * character * nearShore, Mathf.Max(floor, s.SeaLevel - 0.4f));
        }

        if (s.IslandPeakHeight > 0f && !float.IsInfinity(s.IslandThreshold))
        {
            float islandNoise = WaterGenerator.Fbm(x / s.IslandScale, y / s.IslandScale, s.Seed, IslandSalt, 3);
            // Fades in away from the mainland shore, so islands stand out in open water rather than
            // welding themselves onto the coast (and so the coastline stays continuous at side == 0).
            float island = WaterGenerator.SmoothStep01((islandNoise - s.IslandThreshold) / 0.25f)
                           * WaterGenerator.SmoothStep01(offshore / (s.CoastBlendWidth * g));
            if (island > 0f)
            {
                float top = s.SeaLevel + s.IslandPeakHeight + Mathf.Max(0f, landRelief) * 0.35f;
                floor = Mathf.Lerp(floor, top, island);
            }
        }

        if (s.StackChance > 0f && distance < SeaStacks.MaxOffshore + 60f)
            floor = SeaStacks.Apply(s, x, y, floor);

        return floor;
    }

    /// <summary>
    /// Land height of a cliff coast at <paramref name="distance"/> world units inland. Along the coast the
    /// cliff changes height, swings in and out (headlands and coves), is cut by notches and ravines that
    /// drop toward the sea (natural ways up and down), and varies from sheer faces to steep slopes. Across
    /// it: a rocky foot at the waterline, a scree apron, then one to three rock faces separated by uneven
    /// ledges, with rough rock on the faces.
    /// </summary>
    private static float CliffProfile(WaterSettings s, float x, float y, float distance)
    {
        float top = CliffTop(s, x, y) * (0.75f + 0.5f * Noise01(s, x / 70f, y / 70f, CliffSalt + 5f));

        // Notches/ravines: narrow bands across the coast where the cliff drops most of the way down.
        float notch = Mathf.Clamp01(1f - Mathf.Abs(WaterGenerator.Fbm(x / 45f, y / 45f, s.Seed, CliffSalt + 71f, 2)) * 7f);
        top *= 1f - 0.75f * notch * notch;

        int tiers = 1 + Mathf.Clamp(Mathf.FloorToInt(s.CliffTerraces * 2.99f * Noise01(s, x / 150f, y / 150f, CliffSalt + 11f)), 0, 2);
        float edge = 2f + 16f * Noise01(s, x / 90f, y / 90f, CliffSalt + 23f);
        float inland = distance - edge;
        float face = 2.5f + 6f * Noise01(s, x / 50f, y / 50f, CliffSalt + 29f);
        float ledge = 6f + 12f * Noise01(s, x / 60f, y / 60f, CliffSalt + 37f);

        float rise = 0f;
        for (int t = 0; t < tiers; t++)
            rise += (top / tiers) * WaterGenerator.SmoothStep01((inland - t * (face + ledge)) / face);

        // Scree apron at the foot of the lowest face.
        rise += 0.12f * top * WaterGenerator.SmoothStep01((inland + 8f) / 8f) * (1f - WaterGenerator.SmoothStep01(inland / face));

        // Rough rock on and just above the faces.
        float faces = WaterGenerator.SmoothStep01((inland + 4f) / (face + 4f)) * (1f - WaterGenerator.SmoothStep01((inland - tiers * (face + ledge)) / 12f));
        float rough = 0.1f * top * WaterGenerator.Fbm(x / 7f, y / 7f, s.Seed, CliffSalt + 51f, 2) * faces;

        float foot = 0.8f * WaterGenerator.SmoothStep01(distance / 2f);
        return s.SeaLevel + foot + rise + rough;
    }

    /// <summary>Noise mapped to roughly 0-1 (clamped).</summary>
    private static float Noise01(WaterSettings s, float x, float y, float salt)
    {
        return Mathf.Clamp01(0.5f + 1.2f * WaterGenerator.Fbm(x, y, s.Seed, salt, 2));
    }
}

/// <summary>
/// Sea stacks: isolated rock towers standing in the sea off rugged cliff coasts, the remains of old
/// headlands. Placed sparsely on a grid (at most one small group per cell, only where the coast is cliff
/// and the water is a short way offshore), each with its own height, width, fluted irregular outline,
/// weathered stepped top and a rocky apron just under the water.
/// </summary>
public static class SeaStacks
{
    public const float MaxOffshore = 90f;

    private sealed class Stack
    {
        public Vector2 Center;
        public float Radius;
        public float Top;
        public float H1, H2, P1, P2;
        public int Salt;
    }

    private static readonly Stack[] None = new Stack[0];
    private static readonly ConcurrentDictionary<long, Lazy<Stack[]>> Cache = new ConcurrentDictionary<long, Lazy<Stack[]>>();

    public static void ClearCache()
    {
        Cache.Clear();
    }

    public static float Apply(WaterSettings s, float x, float y, float floor)
    {
        float spacing = s.StackSpacing;
        int cx = Mathf.FloorToInt(x / spacing);
        int cy = Mathf.FloorToInt(y / spacing);
        float height = floor;

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                Stack[] stacks = Get(new Vector2Int(cx + dx, cy + dy), s);
                for (int i = 0; i < stacks.Length; i++)
                {
                    Stack stack = stacks[i];
                    float ox = x - stack.Center.x, oy = y - stack.Center.y;
                    float r = Mathf.Sqrt(ox * ox + oy * oy);
                    if (r > stack.Radius * 1.9f)
                        continue;

                    float cos = r > 1e-4f ? ox / r : 1f;
                    float sin = r > 1e-4f ? oy / r : 0f;
                    float angle = Mathf.Atan2(oy, ox);
                    float wobble = 1f + stack.H1 * Mathf.Sin(2f * angle + stack.P1) + stack.H2 * Mathf.Sin(3f * angle + stack.P2)
                                   + 0.12f * WaterGenerator.Fbm(cos * 2.5f + stack.Salt * 0.01f, sin * 2.5f, s.Seed, stack.Salt, 2);
                    float rho = r / (stack.Radius * Mathf.Max(0.4f, wobble));

                    // Rocky apron just under the water around the base.
                    height = Mathf.Max(height, s.SeaLevel - 1.2f - (s.OceanDepth + 10f) * WaterGenerator.SmoothStep01((rho - 1f) / 0.85f));

                    // Near-vertical sides; a weathered top with a couple of stepped shelves.
                    float body = 1f - WaterGenerator.SmoothStep01((rho - 0.8f) / 0.22f);
                    if (body <= 0f)
                        continue;
                    float above = stack.Top - s.SeaLevel;
                    float shelves = Mathf.Floor(Mathf.Clamp01(0.5f + 0.8f * WaterGenerator.Fbm(x / 7f, y / 7f, s.Seed, stack.Salt + 3, 2)) * 3f) / 3f;
                    float top = stack.Top - above * 0.12f * shelves + 0.6f * WaterGenerator.Fbm(x / 3f, y / 3f, s.Seed, stack.Salt + 7, 2);
                    height = Mathf.Max(height, s.SeaLevel - 2f + (top - s.SeaLevel + 2f) * body);
                }
            }
        }

        return height;
    }

    private static Stack[] Get(Vector2Int cell, WaterSettings s)
    {
        long key = WaterGenerator.CellKey(cell);
        if (!Cache.TryGetValue(key, out Lazy<Stack[]> lazy))
            lazy = Cache.GetOrAdd(key, new Lazy<Stack[]>(() => Evaluate(cell, s), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication));
        return lazy.Value;
    }

    private static Stack[] Evaluate(Vector2Int cell, WaterSettings s)
    {
        var random = new System.Random(WaterGenerator.Hash(cell.x, cell.y, s.Seed, 0x57AC));
        float spacing = s.StackSpacing;
        Vector2 site = new Vector2((cell.x + 0.2f + 0.6f * (float)random.NextDouble()) * spacing,
                                   (cell.y + 0.2f + 0.6f * (float)random.NextDouble()) * spacing);
        float g = s.ContinentGradient;
        float target = 12f + (MaxOffshore - 20f) * (float)random.NextDouble();

        // Move the site across the coast until it is `target` units offshore (two Newton steps on the
        // continent field), so any cell a cliff coast passes through can get a stack group.
        Vector2 start = site;
        for (int iteration = 0; iteration < 2; iteration++)
        {
            float side = OceanGenerator.LandSide(s, site.x, site.y);
            const float h = 5f;
            Vector2 gradient = new Vector2(
                OceanGenerator.LandSide(s, site.x + h, site.y) - OceanGenerator.LandSide(s, site.x - h, site.y),
                OceanGenerator.LandSide(s, site.x, site.y + h) - OceanGenerator.LandSide(s, site.x, site.y - h)) / (2f * h);
            float g2 = gradient.sqrMagnitude;
            if (g2 < 1e-14f)
                return None;
            site += gradient * ((-target * g - side) / g2);
        }
        if ((site - start).sqrMagnitude > 0.75f * spacing * 0.75f * spacing)
            return None;
        float offshore = -OceanGenerator.LandSide(s, site.x, site.y) / g;
        if (offshore < 10f || offshore > MaxOffshore)
            return None;

        float character = OceanGenerator.CoastCharacter(s, site.x, site.y);
        if (character < 0.45f || random.NextDouble() > s.StackChance * character)
            return None;

        int count = 1 + (random.NextDouble() < 0.45 ? 1 : 0) + (random.NextDouble() < 0.15 ? 1 : 0);
        float cliffTop = OceanGenerator.CliffTop(s, site.x, site.y);
        var stacks = new System.Collections.Generic.List<Stack>(count);
        for (int i = 0; i < count; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float offset = i == 0 ? 0f : 9f + 12f * (float)random.NextDouble();
            Vector2 center = site + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * offset;
            float radius = 4f + 9f * (float)random.NextDouble();
            // Each stack stays clear of the shore.
            if (-OceanGenerator.LandSide(s, center.x, center.y) / g < radius + 4f)
                continue;
            float height = Mathf.Max(4f, (0.45f + 0.55f * (float)random.NextDouble()) * Mathf.Min(s.StackMaxHeight, cliffTop * 1.15f));
            stacks.Add(new Stack
            {
                Center = center,
                Radius = radius,
                Top = s.SeaLevel + height,
                H1 = 0.08f + 0.14f * (float)random.NextDouble(),
                H2 = 0.04f + 0.1f * (float)random.NextDouble(),
                P1 = (float)random.NextDouble() * 6.283f,
                P2 = (float)random.NextDouble() * 6.283f,
                Salt = random.Next(1, 100000),
            });
        }
        return stacks.ToArray();
    }
}
