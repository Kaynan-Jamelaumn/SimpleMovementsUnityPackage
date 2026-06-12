using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A starting point for a biome object's rules (see the Biomes section's Presets menus).</summary>
public sealed class ObjectPreset
{
    public string Name;
    /// <summary>Menu path ("Plants/Grass").</summary>
    public string Menu;
    public Action<BiomeObject> Apply;
}

/// <summary>
/// Ready-made rule sets for common kinds of objects. Each starts from a fresh object (every rule "no constraint")
/// and sets only what defines that kind; tune the density and sizes to your prefabs afterwards.
/// </summary>
public static class ObjectPresets
{
    public static readonly List<ObjectPreset> All = new List<ObjectPreset>
    {
        new ObjectPreset { Name = "Forest Tree", Menu = "Trees/Forest Tree", Apply = o =>
        {
            o.probabilityToSpawn = 1.2f;
            Upright(o, 0.1f, 8f);
            o.slopeThreshold = 30f;
            Clusters(o, 6, 45f, 3f, 0.25f);
            o.clustering.growthRadius = 8f;
            o.clustering.growthPerNeighbour = 0.4f;
            o.spacing.softSpacing = 6f;
            o.scaleRange = new Vector2(0.8f, 1.25f);
            o.densityNoise.scale = 60f;
            o.placementPriority = 10;
        }},
        new ObjectPreset { Name = "Lone Tree", Menu = "Trees/Lone Tree (open land)", Apply = o =>
        {
            o.probabilityToSpawn = 0.05f;
            Upright(o, 0.1f, 8f);
            o.slopeThreshold = 20f;
            o.isClusterable = false;
            o.spacing.softSpacing = 25f;
            o.spacing.softSpacingStrength = 0.8f;
            o.scaleRange = new Vector2(0.9f, 1.4f);
            o.placementPriority = 10;
        }},
        new ObjectPreset { Name = "Palm / Beach Tree", Menu = "Trees/Palm (near the sea)", Apply = o =>
        {
            o.probabilityToSpawn = 0.4f;
            Upright(o, 0.2f, 15f);
            o.slopeThreshold = 25f;
            o.water.placement = WaterPlacement.NearWater;
            o.water.bodies = WaterBodyMask.Ocean;
            o.water.maxWaterDistance = 25f;
            Clusters(o, 4, 25f, 2f, 0.2f);
            o.scaleRange = new Vector2(0.85f, 1.2f);
            o.orientation.randomTilt = 6f;
        }},
        new ObjectPreset { Name = "Bush", Menu = "Plants/Bush", Apply = o =>
        {
            o.probabilityToSpawn = 2f;
            o.orientation.terrainAlignment = 0.5f;
            o.orientation.maxTilt = 25f;
            o.slopeThreshold = 35f;
            Clusters(o, 8, 20f, 2.5f, 0.3f);
            o.scaleRange = new Vector2(0.7f, 1.3f);
            o.relations.Add(new ObjectRelation { otherTag = "Tree", kind = RelationKind.CannotSpawnNear, radius = 1.5f });
        }},
        new ObjectPreset { Name = "Grass / Flowers", Menu = "Plants/Grass or Flowers", Apply = o =>
        {
            o.probabilityToSpawn = 15f;
            o.minSpacing = 0.6f;
            o.orientation.terrainAlignment = 0.8f;
            o.slopeThreshold = 40f;
            o.isClusterable = false;
            o.densityNoise.scale = 25f;
            o.densityNoise.strength = 0.8f;
            o.densityNoise.coverage = 0.6f;
            o.scaleRange = new Vector2(0.7f, 1.3f);
            o.ground.verifyAfterSpawn = false;
            o.ground.sinkDepth = 0.02f;
            o.placementPriority = -10;
        }},
        new ObjectPreset { Name = "Mushroom", Menu = "Plants/Mushroom (under trees)", Apply = o =>
        {
            o.probabilityToSpawn = 3f;
            o.minSpacing = 0.6f;
            o.orientation.terrainAlignment = 0.5f;
            o.isClusterable = false;
            o.relations.Add(new ObjectRelation { otherTag = "Tree", kind = RelationKind.RequiresNearby, radius = 4f });
            o.relations.Add(new ObjectRelation { otherTag = "Tree", kind = RelationKind.AttractedTo, radius = 4f, strength = 0.8f });
            o.climate.useGroundWetness = false;
            o.placementPriority = -5;
        }},
        new ObjectPreset { Name = "Cliff Plant", Menu = "Plants/Cliff Plant (steep faces)", Apply = o =>
        {
            o.probabilityToSpawn = 4f;
            o.minSpacing = 1f;
            o.slopeThreshold = 85f;
            o.slope.minSlope = 35f;
            o.slope.surfaces = SurfaceMask.Steep | SurfaceMask.Vertical;
            o.orientation.terrainAlignment = 1f;
            o.ground.maxPenetration = 0.3f;
            o.ground.maxFloating = 0.08f;
            o.isClusterable = false;
        }},
        new ObjectPreset { Name = "Reeds", Menu = "Water/Reeds (shoreline)", Apply = o =>
        {
            o.probabilityToSpawn = 8f;
            o.minSpacing = 0.8f;
            o.water.placement = WaterPlacement.Shoreline;
            o.water.bodies = WaterBodyMask.Lake | WaterBodyMask.Pond | WaterBodyMask.River;
            o.water.maxShoreDistance = 3f;
            Upright(o, 0f, 0f);
            Clusters(o, 10, 12f, 2f, 0.4f);
        }},
        new ObjectPreset { Name = "Water Lily", Menu = "Water/Water Lily (floating)", Apply = o =>
        {
            o.probabilityToSpawn = 5f;
            o.minSpacing = 1.2f;
            o.water.placement = WaterPlacement.InWater;
            o.water.bodies = WaterBodyMask.Lake | WaterBodyMask.Pond;
            o.water.heightInWater = WaterHeightMode.OnSurface;
            o.water.maxDepth = 3f;
            o.water.avoidFastFlow = 1f;
            Upright(o, 0f, 0f);
            Clusters(o, 8, 10f, 3f, 0.1f);
        }},
        new ObjectPreset { Name = "Seaweed / Kelp", Menu = "Water/Seaweed (on the bottom)", Apply = o =>
        {
            o.probabilityToSpawn = 3f;
            o.minSpacing = 1f;
            o.water.placement = WaterPlacement.InWater;
            o.water.bodies = WaterBodyMask.Ocean | WaterBodyMask.Lake | WaterBodyMask.River;
            o.water.heightInWater = WaterHeightMode.OnBottom;
            o.water.minDepth = 0.5f;
            o.orientation.mode = OrientationMode.AlongWaterFlow;
            o.orientation.yawJitter = 20f;
            o.orientation.terrainAlignment = 0.3f;
            Clusters(o, 8, 15f, 2.5f, 0.2f);
        }},
        new ObjectPreset { Name = "Driftwood Log", Menu = "Water/Log Along a River", Apply = o =>
        {
            o.probabilityToSpawn = 0.3f;
            o.minSpacing = 5f;
            o.water.placement = WaterPlacement.NearWater;
            o.water.bodies = WaterBodyMask.River;
            o.water.maxWaterDistance = 12f;
            o.orientation.mode = OrientationMode.AlongWaterFlow;
            o.orientation.yawJitter = 25f;
            o.orientation.terrainAlignment = 1f;
            o.isClusterable = false;
        }},
        new ObjectPreset { Name = "Small Rock", Menu = "Rocks/Small Rock", Apply = o =>
        {
            o.probabilityToSpawn = 1.5f;
            o.orientation.terrainAlignment = 0.8f;
            o.orientation.randomTilt = 15f;
            o.slopeThreshold = 60f;
            o.ground.sinkDepth = 0.15f;
            o.ground.maxPenetration = 0.5f;
            o.scaleRange = new Vector2(0.6f, 1.4f);
            Clusters(o, 6, 20f, 2.5f, 0.4f);
            o.relations.Add(new ObjectRelation { otherTag = "Tree", kind = RelationKind.CannotSpawnNear, radius = 2.5f });
        }},
        new ObjectPreset { Name = "Boulder", Menu = "Rocks/Boulder", Apply = o =>
        {
            o.probabilityToSpawn = 0.08f;
            o.orientation.terrainAlignment = 0.6f;
            o.orientation.randomTilt = 10f;
            o.slopeThreshold = 25f;
            o.ground.sinkDepth = 0.3f;
            o.ground.maxPenetration = 1f;
            o.ground.maxFloating = 0.15f;
            o.biomes.footprintInBiome = true;
            o.scaleRange = new Vector2(0.8f, 1.5f);
            o.isClusterable = false;
            o.spacing.softSpacing = 20f;
            o.placementPriority = 20;
        }},
        new ObjectPreset { Name = "Scree", Menu = "Rocks/Scree (steep slopes)", Apply = o =>
        {
            o.probabilityToSpawn = 3f;
            o.slopeThreshold = 60f;
            o.slope.minSlope = 20f;
            o.slope.usePreferredSlope = true;
            o.slope.preferredMinSlope = 28f;
            o.slope.preferredMaxSlope = 45f;
            o.orientation.mode = OrientationMode.FullyRandom;
            o.ground.sinkDepth = 0.1f;
            o.ground.maxPenetration = 0.4f;
            o.scaleRange = new Vector2(0.5f, 1.2f);
            o.altitude.terrainPosition = TerrainPosition.Slopes;
        }},
        new ObjectPreset { Name = "Hilltop Structure", Menu = "Landmarks/On Hilltops (ruins, towers)", Apply = o =>
        {
            Landmark(o, 900f, 1, 600f);
            o.altitude.terrainPosition = TerrainPosition.Ridges;
            o.altitude.terrainPositionRequired = true;
            o.slopeThreshold = 12f;
        }},
        new ObjectPreset { Name = "Valley Landmark", Menu = "Landmarks/In Valleys (camps, shrines)", Apply = o =>
        {
            Landmark(o, 800f, 1, 500f);
            o.altitude.terrainPosition = TerrainPosition.Valleys;
            o.slopeThreshold = 10f;
            o.water.placement = WaterPlacement.AwayFromWater;
            o.water.minWaterDistance = 8f;
        }},
        new ObjectPreset { Name = "Ancient Tree", Menu = "Landmarks/Ancient Tree (one per region)", Apply = o =>
        {
            Landmark(o, 700f, 1, 500f);
            o.altitude.terrainPosition = TerrainPosition.Flats;
            o.slopeThreshold = 15f;
            o.relations.Add(new ObjectRelation { otherTag = "Tree", kind = RelationKind.PreventsNearby, radius = 9f });
            o.relations.Add(new ObjectRelation { otherTag = "Rock", kind = RelationKind.PreventsNearby, radius = 8f });
        }},
        new ObjectPreset { Name = "Unique Landmark", Menu = "Landmarks/Unique (one in the world)", Apply = o =>
        {
            Landmark(o, 500f, 1, 0f);
            o.limits.unique = true;
            o.limits.guaranteed = true;
            o.slopeThreshold = 12f;
        }},
    };

    private static void Upright(BiomeObject o, float alignment, float maxTilt)
    {
        o.orientation.mode = alignment <= 0f ? OrientationMode.Upright : OrientationMode.RandomYaw;
        o.orientation.terrainAlignment = alignment;
        o.orientation.maxTilt = maxTilt;
    }

    private static void Clusters(BiomeObject o, int count, float radius, float inside, float outside)
    {
        o.isClusterable = true;
        o.clusterCount = count;
        o.clusterRadius = radius;
        o.clustering.insideDensity = inside;
        o.clustering.outsideDensity = outside;
    }

    private static void Landmark(BiomeObject o, float regionSize, int perRegion, float minDistance)
    {
        o.probabilityToSpawn = 60f;
        o.limits.mode = PlacementMode.Landmark;
        o.limits.regionSize = regionSize;
        o.limits.maxPerRegion = perRegion;
        o.limits.minDistanceBetween = minDistance;
        o.isClusterable = false;
        o.densityNoise.enabled = false;
        Upright(o, 0f, 0f);
        o.biomes.footprintInBiome = true;
        o.ground.maxFloating = 0.2f;
        o.ground.maxPenetration = 0.6f;
        o.placementPriority = 100;
    }
}
