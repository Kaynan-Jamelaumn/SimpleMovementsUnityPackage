using System;
using System.Collections.Generic;
using UnityEngine;

// ObjectPlacementEngine, part 2: judging one spot with this chunk's accurate terrain, water and climate, and
// fitting the object to it - orientation, scale and ground contact (see ObjectPlacementEngine.cs).
public static partial class ObjectPlacementEngine
{
    // Surface kinds by slope (degrees), see SurfaceMask.
    private const float FlatBelow = 10f, InclinedBelow = 35f, SteepBelow = 60f;

    /// <summary>
    /// Every non-relational rule for one spot, with this chunk's accurate data. Fills <see cref="Candidate.Soft"/>
    /// (the product of the soft preferences, 0-1) and the fitted position, rotation and scale. False = a hard rule failed.
    /// </summary>
    private static bool EvaluateSite(ChunkState state, PlacementType type, ref Candidate c)
    {
        PlacementEnvironment env = state.Env;
        BiomeObject def = type.Def;
        float soft = 1f;
        float x = c.X, z = c.Z;

        // --- Slope and surface
        float scale = Mathf.Lerp(Mathf.Min(def.scaleRange.x, def.scaleRange.y), Mathf.Max(def.scaleRange.x, def.scaleRange.y), Rand(type, c, StreamScale));
        scale = Mathf.Max(0.01f, scale);
        float footprint = type.Radius * scale;
        Vector3 normal = env.SurfaceNormal(x, z, 0f);
        float slope = env.SlopeDegrees(normal);
        if (slope > type.MaxSlope || slope < type.MinSlope || !SurfaceAllowed(def.slope.surfaces, slope))
            return Reject(state, Stage.Slope);
        if (def.slope.usePreferredSlope)
            soft *= RangePreference(slope, def.slope.preferredMinSlope, def.slope.preferredMaxSlope, Mathf.Max(0.1f, def.slope.slopeFalloff));

        // --- Water
        float ground = env.SurfaceHeight(x, z);
        WaterBodyType water = env.WaterAt(x, z);
        bool wet = water != WaterBodyType.None;
        WaterRules waterRules = def.water;
        bool inChosenWater = wet && InMask(waterRules.bodies, water);
        float waterSurface = wet ? env.WaterSurfaceAt(x, z) : float.NaN;
        float depth = wet && !float.IsNaN(waterSurface) ? Mathf.Max(0f, waterSurface - ground) : 0f;
        switch (waterRules.placement)
        {
            case WaterPlacement.DryLand:
                if (wet)
                    return Reject(state, Stage.Water);
                break;
            case WaterPlacement.NearWater:
                if (wet || env.WaterDistance(x, z, waterRules.bodies) > Mathf.Max(0.5f, waterRules.maxWaterDistance > 0f ? waterRules.maxWaterDistance : 10f))
                    return Reject(state, Stage.Water);
                break;
            case WaterPlacement.AwayFromWater:
                if (wet || env.WaterDistance(x, z, waterRules.bodies) < Mathf.Max(0.5f, waterRules.minWaterDistance > 0f ? waterRules.minWaterDistance : 10f))
                    return Reject(state, Stage.Water);
                break;
            case WaterPlacement.Shoreline:
                if (wet || env.WaterDistance(x, z, waterRules.bodies) > Mathf.Max(0.5f, waterRules.maxShoreDistance > 0f ? waterRules.maxShoreDistance : 3f))
                    return Reject(state, Stage.Water);
                break;
            case WaterPlacement.InWater:
                if (!inChosenWater)
                    return Reject(state, Stage.Water);
                if (depth < waterRules.minDepth || (waterRules.maxDepth > 0f && depth > waterRules.maxDepth))
                    return Reject(state, Stage.Water);
                float fromShore = env.ShoreDistance(x, z);
                if (fromShore < waterRules.minShoreDistance || (waterRules.maxShoreDistance > 0f && fromShore > waterRules.maxShoreDistance))
                    return Reject(state, Stage.Water);
                soft *= depth < waterRules.shallowDepth ? waterRules.shallowWeight : depth > waterRules.deepDepth ? waterRules.deepWeight : waterRules.mediumWeight;
                if (water == WaterBodyType.River && waterRules.avoidFastFlow > 0f)
                    soft *= 1f - waterRules.avoidFastFlow * Mathf.Clamp01((env.FlowAt(x, z).magnitude - 0.4f) / 2f);
                break;
        }
        if (waterRules.placement != WaterPlacement.InWater && waterRules.placement != WaterPlacement.Anywhere)
        {
            // Extra distance limits (on top of the placement mode's own).
            if (waterRules.minWaterDistance > 0f || waterRules.maxWaterDistance > 0f)
            {
                float distance = env.WaterDistance(x, z, waterRules.bodies);
                if (distance < waterRules.minWaterDistance || (waterRules.maxWaterDistance > 0f && distance > waterRules.maxWaterDistance))
                    return Reject(state, Stage.Water);
            }
        }
        if (soft <= 0f)
            return Reject(state, Stage.Water);

        // --- Altitude and the shape of the land
        AltitudeRules altitude = def.altitude;
        if (altitude.limitAltitude && (ground < altitude.minAltitude || ground > altitude.maxAltitude))
            return Reject(state, Stage.Altitude);
        Biome biome = c.Biome >= 0 ? state.Plan.Biomes[c.Biome] : null;
        if (biome != null && (altitude.limitRelativeAltitude || altitude.useBiomeHeightBand))
        {
            float band = biome.maxHeight - biome.minHeight;
            float relative = band > 0.001f ? (ground - biome.minHeight) / band : 0.5f;
            if (altitude.limitRelativeAltitude && (relative < altitude.minRelativeAltitude || relative > altitude.maxRelativeAltitude))
                return Reject(state, Stage.Altitude);
            if (altitude.useBiomeHeightBand)
                soft *= Gaussian((relative - 0.5f) * 2f, 1f);
        }
        if (def.useCustomHeightPreference)
        {
            float range = Mathf.Max(0.01f, def.preferredMaxHeight - def.preferredMinHeight);
            soft *= Gaussian((ground - def.preferredOptimalHeight) / (range * 0.5f), def.heightPreferenceStrength);
        }
        if (altitude.limitHeightAboveWater)
        {
            float level = env.NearestWaterLevel(x, z);
            float above = float.IsNaN(level) ? float.PositiveInfinity : ground - level;
            if (above < altitude.minHeightAboveWater || above > altitude.maxHeightAboveWater)
                return Reject(state, Stage.Altitude);
        }
        if (altitude.terrainPosition != TerrainPosition.Any || altitude.limitRelief || altitude.limitRoughness)
        {
            env.Relief(x, z, Mathf.Max(2f, altitude.reliefRadius), out float relief, out float roughness);
            if (altitude.limitRelief && (relief < altitude.minRelief || relief > altitude.maxRelief))
                return Reject(state, Stage.Altitude);
            if (altitude.limitRoughness && (roughness < altitude.minRoughness || roughness > altitude.maxRoughness))
                return Reject(state, Stage.Altitude);
            if (altitude.terrainPosition != TerrainPosition.Any)
            {
                float fit = PositionFit(altitude.terrainPosition, relief, roughness, slope);
                if (altitude.terrainPositionRequired)
                {
                    if (fit < 0.5f)
                        return Reject(state, Stage.Altitude);
                }
                else
                {
                    soft *= Mathf.Lerp(1f, fit, Mathf.Clamp01(altitude.positionStrength));
                }
            }
        }
        if (altitude.limitCurvature)
        {
            float curvature = env.Curvature(x, z);
            if (curvature < altitude.minCurvature || curvature > altitude.maxCurvature)
                return Reject(state, Stage.Altitude);
        }

        // --- Biome borders (hard rules, and the fade toward biomes this object doesn't grow in)
        if (type.UsesBorders && c.Biome >= 0 && state.Plan.Biomes[c.Biome].placement == BiomePlacement.Land)
        {
            float fade = BorderFactor(state, type, x, z, c.Biome);
            if (fade <= 0f)
                return Reject(state, Stage.Biome);
            soft *= fade;
        }

        // --- Climate and ground wetness
        ClimateRules climate = def.climate;
        if (climate.useMoisture || climate.useTemperature)
        {
            env.Climate(x, z, out float temperature, out float moisture);
            if (climate.useMoisture)
                soft *= RangeAllowance(moisture, climate.minMoisture, climate.maxMoisture, climate.softness);
            if (climate.useTemperature)
                soft *= RangeAllowance(temperature, climate.minTemperature, climate.maxTemperature, climate.softness);
        }
        if (climate.useGroundWetness)
            soft *= RangeAllowance(env.GroundWetness(x, z), climate.minWetness, climate.maxWetness, climate.softness);
        if (soft <= 0f)
            return Reject(state, Stage.Climate);

        // --- Distances to features
        foreach (FeatureDistanceRule rule in def.featureDistances)
        {
            if (rule == null)
                continue;
            float distance = FeatureDistance(env, rule, x, z);
            float max = rule.maxDistance > 0f ? rule.maxDistance : float.PositiveInfinity;
            if (rule.soft)
                soft *= RangePreference(distance, rule.minDistance, max, Mathf.Max(0.1f, rule.softFalloff));
            else if (distance < rule.minDistance || distance > max)
                return Reject(state, Stage.Feature);
        }
        if (soft <= 0f)
            return Reject(state, Stage.Feature);

        // --- Orientation, then fit to the ground
        Vector3 fitNormal = footprint > 1f ? env.SurfaceNormal(x, z, footprint) : normal;
        Quaternion rotation = Orientation(state, type, c, fitNormal, x, z);
        if (def.orientation.maxNormalDeviation < 180f && Vector3.Angle(rotation * Vector3.up, fitNormal) > def.orientation.maxNormalDeviation)
            return Reject(state, Stage.Orientation);

        if (!FitToGround(state, type, c, rotation, scale, ground, waterSurface, out Vector3 position, out float expectedMinY))
            return Reject(state, Stage.Ground);

        c.Soft = soft;
        c.Position = position;
        c.Rotation = rotation;
        c.Scale = scale;
        c.ExpectedMinY = expectedMinY;
        return true;
    }

    private static bool Reject(ChunkState state, Stage stage)
    {
        state.Reject(stage);
        return false;
    }

    private static bool InMask(WaterBodyMask mask, WaterBodyType type)
    {
        int bit = type >= WaterBodyType.Ocean && type <= WaterBodyType.River ? 1 << ((int)type - 1) : 0;
        return ((int)mask & bit) != 0;
    }

    private static bool SurfaceAllowed(SurfaceMask mask, float slope)
    {
        SurfaceMask kind = slope < FlatBelow ? SurfaceMask.Flat : slope < InclinedBelow ? SurfaceMask.Inclined : slope < SteepBelow ? SurfaceMask.Steep : SurfaceMask.Vertical;
        return (mask & kind) != 0;
    }

    /// <summary>1 inside [min, max], fading smoothly to 0 over <paramref name="falloff"/> outside it.</summary>
    private static float RangePreference(float value, float min, float max, float falloff)
    {
        if (value < min)
            return 1f - WaterGenerator.SmoothStep01((min - value) / falloff);
        if (value > max)
            return 1f - WaterGenerator.SmoothStep01((value - max) / falloff);
        return 1f;
    }

    /// <summary>Hard range when <paramref name="softness"/> is 0, otherwise <see cref="RangePreference"/>.</summary>
    private static float RangeAllowance(float value, float min, float max, float softness)
    {
        if (softness <= 0f)
            return value >= min && value <= max ? 1f : 0f;
        return RangePreference(value, min, max, softness);
    }

    private static float Gaussian(float normalizedDistance, float strength)
    {
        return Mathf.Exp(-normalizedDistance * normalizedDistance * Mathf.Max(0.01f, strength));
    }

    /// <summary>
    /// How well a spot matches a place in the land's shape (0-1), from its relief (height against the
    /// surroundings), roughness (for scale) and slope.
    /// </summary>
    private static float PositionFit(TerrainPosition position, float relief, float roughness, float slope)
    {
        // Relief measured against the local height variation, so ridges and valleys read the same in gentle and rugged land.
        float z = relief / Mathf.Max(0.5f, roughness);
        float sloped = WaterGenerator.SmoothStep01((slope - 6f) / 8f);
        switch (position)
        {
            case TerrainPosition.Valleys: return WaterGenerator.SmoothStep01((-z - 0.4f) / 0.8f);
            case TerrainPosition.Ridges: return WaterGenerator.SmoothStep01((z - 0.4f) / 0.8f);
            case TerrainPosition.LowerSlopes: return sloped * Band(z, -1f, -0.1f, 0.4f);
            case TerrainPosition.UpperSlopes: return sloped * Band(z, 0.1f, 1f, 0.4f);
            case TerrainPosition.Slopes: return sloped * Band(z, -0.8f, 0.8f, 0.4f);
            case TerrainPosition.Flats: return (1f - sloped) * Band(z, -0.5f, 0.5f, 0.4f);
            default: return 1f;
        }
    }

    private static float Band(float value, float min, float max, float soft)
    {
        return RangePreference(value, min, max, soft);
    }

    private static float FeatureDistance(PlacementEnvironment env, FeatureDistanceRule rule, float x, float z)
    {
        switch (rule.feature)
        {
            case PlacementFeature.AnyWater: return env.WaterDistance(x, z, WaterBodyMask.All);
            case PlacementFeature.Ocean: return env.WaterDistance(x, z, WaterBodyMask.Ocean);
            case PlacementFeature.Lake: return env.WaterDistance(x, z, WaterBodyMask.Lake);
            case PlacementFeature.Pond: return env.WaterDistance(x, z, WaterBodyMask.Pond);
            case PlacementFeature.River: return env.WaterDistance(x, z, WaterBodyMask.River);
            case PlacementFeature.Cliff: return env.CliffDistance(x, z);
            case PlacementFeature.Custom: return PlacementFeatures.Distance(rule.customTag, x, z);
            default: return PlacementFields.FarDistance;
        }
    }

    // ------------------------------------------------------------------ orientation

    private static Quaternion Orientation(ChunkState state, PlacementType type, Candidate c, Vector3 normal, float x, float z)
    {
        OrientationRules rules = type.Def.orientation;
        if (rules.mode == OrientationMode.FullyRandom)
            return RandomRotation(Rand(type, c, StreamQuatA), Rand(type, c, StreamQuatB), Rand(type, c, StreamQuatC));

        float baseYaw = 0f;
        switch (rules.mode)
        {
            case OrientationMode.AlongWaterFlow:
                Vector2 flow = state.Env.FlowAt(x, z);
                if (flow.sqrMagnitude < 1e-6f)
                    flow = state.Env.RiverDirection(x, z);
                if (flow.sqrMagnitude > 1e-6f)
                    baseYaw = Mathf.Atan2(flow.x, flow.y) * Mathf.Rad2Deg;
                break;
            case OrientationMode.FaceDownhill:
            case OrientationMode.AcrossSlope:
                Vector2 downhill = new Vector2(normal.x, normal.z);
                if (downhill.sqrMagnitude > 1e-8f)
                    baseYaw = Mathf.Atan2(downhill.x, downhill.y) * Mathf.Rad2Deg + (rules.mode == OrientationMode.AcrossSlope ? 90f : 0f);
                break;
        }
        float yaw = baseYaw + (Rand(type, c, StreamYaw) - 0.5f) * rules.yawJitter;
        Quaternion yawRotation = Quaternion.AngleAxis(yaw, Vector3.up);

        Vector3 up = Vector3.up;
        if (rules.mode != OrientationMode.Upright)
        {
            up = Vector3.Slerp(Vector3.up, normal, Mathf.Clamp01(rules.terrainAlignment));
            float tilt = Vector3.Angle(Vector3.up, up);
            if (tilt > rules.maxTilt && tilt > 0.01f)
                up = Vector3.Slerp(Vector3.up, up, rules.maxTilt / tilt);
        }
        if (rules.randomTilt > 0f)
        {
            float axisAngle = Rand(type, c, StreamTiltAxis) * Mathf.PI * 2f;
            Vector3 axis = new Vector3(Mathf.Cos(axisAngle), 0f, Mathf.Sin(axisAngle));
            up = Quaternion.AngleAxis(Rand(type, c, StreamTilt) * rules.randomTilt, axis) * up;
        }
        return Quaternion.FromToRotation(Vector3.up, up) * yawRotation;
    }

    /// <summary>A uniformly distributed rotation from three numbers in [0, 1) (Shoemake's method).</summary>
    private static Quaternion RandomRotation(float u1, float u2, float u3)
    {
        float a = Mathf.Sqrt(1f - u1), b = Mathf.Sqrt(u1);
        float t1 = 2f * Mathf.PI * u2, t2 = 2f * Mathf.PI * u3;
        return new Quaternion(a * Mathf.Sin(t1), a * Mathf.Cos(t1), b * Mathf.Sin(t2), b * Mathf.Cos(t2));
    }

    // ------------------------------------------------------------------ ground contact

    /// <summary>
    /// Puts the object's anchor (by default the bottom of its bounds, not its pivot) on the ground: the footprint's
    /// corners, edge midpoints and centre are rotated and scaled like the object, the terrain (or water surface) is
    /// sampled under each, and the height is chosen so the centre touches the ground (minus Sink Depth) within the
    /// allowed penetration and floating - rejected if no height satisfies both, i.e. the ground under the object is
    /// too uneven for it. Also checks that enough of the footprint is on valid ground.
    /// </summary>
    private static bool FitToGround(ChunkState state, PlacementType type, Candidate c, Quaternion rotation, float scale, float ground, float waterSurface,
        out Vector3 position, out float expectedMinY)
    {
        PlacementEnvironment env = state.Env;
        BiomeObject def = type.Def;
        GroundContactRules contact = def.ground;
        WaterRules waterRules = def.water;
        Vector3 s = type.Shape.RootScale * scale;
        position = new Vector3(c.X, ground, c.Z);
        expectedMinY = float.NaN;

        float anchorY;
        switch (contact.anchor)
        {
            case GroundAnchor.Pivot: anchorY = 0f; break;
            case GroundAnchor.CustomHeight: anchorY = contact.anchorHeight; break;
            default: anchorY = type.HasBox ? type.BoxMin.y : 0f; break;
        }

        // Footprint sample points in the prefab's space (at the anchor height).
        Vector3 centre;
        Vector3[] points = FootprintPoints(type, anchorY, contact, out centre);

        bool onSurface = waterRules.placement == WaterPlacement.InWater && waterRules.heightInWater == WaterHeightMode.OnSurface;
        bool submerged = waterRules.placement == WaterPlacement.InWater && waterRules.heightInWater == WaterHeightMode.Submerged;
        Vector3 centreOffset = rotation * Vector3.Scale(s, centre);

        if (submerged)
        {
            if (float.IsNaN(waterSurface))
                return false;
            float target = Mathf.Lerp(ground, waterSurface, Mathf.Clamp01(waterRules.submergedFraction));
            position = new Vector3(c.X - centreOffset.x, target - centreOffset.y + contact.verticalOffset, c.Z - centreOffset.z);
            return true;
        }

        float lowest = float.PositiveInfinity, highest = float.NegativeInfinity;
        int validPoints = 0;
        float centreTarget = float.NaN;
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 offset = rotation * Vector3.Scale(s, points[i]) - centreOffset;
            float px = c.X + offset.x, pz = c.Z + offset.z;
            float surface = onSurface ? env.WaterSurfaceAt(px, pz) : env.SurfaceHeight(px, pz);
            if (float.IsNaN(surface))
                surface = env.SurfaceHeight(px, pz);
            // The pivot height at which this point would exactly touch the ground.
            float touch = surface - offset.y - centreOffset.y;
            lowest = Mathf.Min(lowest, touch);
            highest = Mathf.Max(highest, touch);
            if (i == 0)
                centreTarget = touch;
            if (PointValid(state, type, px, pz))
                validPoints++;
        }

        if (validPoints < Mathf.CeilToInt(Mathf.Clamp01(contact.minValidFootprint) * points.Length - 1e-4f))
            return false;

        float sink = onSurface ? 0f : Mathf.Max(0f, contact.sinkDepth);
        float lo = highest - Mathf.Max(0f, contact.maxPenetration) - sink;
        float hi = lowest + Mathf.Max(0f, contact.maxFloating);
        if (lo > hi + 1e-4f)
            return false;
        float pivotY = Mathf.Clamp(centreTarget - sink, lo, hi) + contact.verticalOffset;

        position = new Vector3(c.X - centreOffset.x, pivotY, c.Z - centreOffset.z);
        if (contact.verifyAfterSpawn && type.Shape.HasRenderers && Vector3.Angle(rotation * Vector3.up, Vector3.up) < 5f)
            expectedMinY = pivotY + s.y * type.Shape.RendererMin.y;
        return true;
    }

    /// <summary>The footprint's centre (index 0), corners and edge midpoints at the anchor height, in the prefab's space.</summary>
    private static Vector3[] FootprintPoints(PlacementType type, float anchorY, GroundContactRules contact, out Vector3 centre)
    {
        if (type.HasBox && contact.footprint != FootprintSource.CustomRadius)
        {
            float x0 = type.BoxMin.x, x1 = type.BoxMax.x, z0 = type.BoxMin.z, z1 = type.BoxMax.z;
            float mx = (x0 + x1) * 0.5f, mz = (z0 + z1) * 0.5f;
            centre = new Vector3(mx, anchorY, mz);
            return new[]
            {
                centre,
                new Vector3(x0, anchorY, z0), new Vector3(x1, anchorY, z0), new Vector3(x0, anchorY, z1), new Vector3(x1, anchorY, z1),
                new Vector3(mx, anchorY, z0), new Vector3(mx, anchorY, z1), new Vector3(x0, anchorY, mz), new Vector3(x1, anchorY, mz),
            };
        }

        // A circle of the custom (or default) radius, in world units - converted to the prefab's space.
        Vector3 rootScale = type.Shape.RootScale;
        float rx = type.Radius / Mathf.Max(0.0001f, Mathf.Abs(rootScale.x)), rz = type.Radius / Mathf.Max(0.0001f, Mathf.Abs(rootScale.z));
        centre = new Vector3(0f, anchorY, 0f);
        var points = new Vector3[9];
        points[0] = centre;
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 0.25f;
            points[i + 1] = new Vector3(Mathf.Cos(a) * rx, anchorY, Mathf.Sin(a) * rz);
        }
        return points;
    }

    /// <summary>Whether a footprint point is on ground this object accepts (water side and, if required, biome).</summary>
    private static bool PointValid(ChunkState state, PlacementType type, float x, float z)
    {
        WaterPlacement mode = type.Def.water.placement;
        if (mode != WaterPlacement.Anywhere)
        {
            bool wet = state.Env.WaterAt(x, z) != WaterBodyType.None;
            if (mode == WaterPlacement.InWater ? !wet : wet)
                return false;
        }
        if (type.Def.biomes.footprintInBiome)
        {
            Biome biome = state.Env.BiomeAtCell(Mathf.FloorToInt(x), Mathf.FloorToInt(z));
            if (biome == null || !state.Plan.BiomeIndex.TryGetValue(biome, out int index) || !type.AllowedBiome[index])
                return false;
        }
        return true;
    }
}
