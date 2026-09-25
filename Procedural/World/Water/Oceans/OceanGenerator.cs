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
public static partial class OceanGenerator
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
}
