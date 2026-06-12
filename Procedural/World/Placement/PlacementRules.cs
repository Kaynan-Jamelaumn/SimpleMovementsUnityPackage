using System;
using System.Collections.Generic;
using UnityEngine;

// The per-object placement rules shown on each Biome Object (see BiomeObject.cs). Every group defaults to
// "no constraint", so an object only becomes pickier for the rules you actually set.

/// <summary>How an object relates to water.</summary>
public enum WaterPlacement
{
    /// <summary>Only on dry land (the default: never in or under water).</summary>
    DryLand,
    /// <summary>Water doesn't matter: dry land or in water, on the ground.</summary>
    Anywhere,
    /// <summary>Only on dry land within Max Water Distance of the chosen water bodies.</summary>
    NearWater,
    /// <summary>Only on dry land at least Min Water Distance from the chosen water bodies.</summary>
    AwayFromWater,
    /// <summary>Only right at the water's edge: dry land within Max Shore Distance of it.</summary>
    Shoreline,
    /// <summary>Only inside the chosen water bodies (on the bottom, floating on the surface, or in between - see Height In Water).</summary>
    InWater,
}

/// <summary>Which water bodies a water rule looks at.</summary>
[Flags]
public enum WaterBodyMask
{
    Ocean = 1,
    Lake = 2,
    Pond = 4,
    River = 8,
    All = Ocean | Lake | Pond | River,
}

/// <summary>Where an object in water sits vertically.</summary>
public enum WaterHeightMode
{
    /// <summary>On the bottom of the lake, river or sea (the real underwater terrain).</summary>
    OnBottom,
    /// <summary>Floating on the water surface.</summary>
    OnSurface,
    /// <summary>Between bottom and surface (Submerged Fraction of the depth above the bottom).</summary>
    Submerged,
}

/// <summary>Kinds of ground surface, by slope.</summary>
[Flags]
public enum SurfaceMask
{
    /// <summary>Under 10 degrees.</summary>
    Flat = 1,
    /// <summary>10 to 35 degrees.</summary>
    Inclined = 2,
    /// <summary>35 to 60 degrees.</summary>
    Steep = 4,
    /// <summary>Over 60 degrees (cliff faces).</summary>
    Vertical = 8,
    All = Flat | Inclined | Steep | Vertical,
}

/// <summary>Where in the local shape of the land an object prefers to be.</summary>
public enum TerrainPosition
{
    Any,
    /// <summary>Valley floors and hollows (lower than the surroundings).</summary>
    Valleys,
    /// <summary>The lower part of slopes.</summary>
    LowerSlopes,
    /// <summary>Hillsides (neither ridge nor valley).</summary>
    Slopes,
    /// <summary>The upper part of slopes.</summary>
    UpperSlopes,
    /// <summary>Ridges, crests and hilltops (higher than the surroundings).</summary>
    Ridges,
    /// <summary>Flat ground that is neither hollow nor crest.</summary>
    Flats,
}

/// <summary>How an object treats the borders between biomes.</summary>
public enum BiomeBorderMode
{
    /// <summary>Borders don't matter (apart from Center Preference fading).</summary>
    Anywhere,
    /// <summary>Not within Border Distance of another biome.</summary>
    AwayFromBorders,
    /// <summary>Only within Border Distance of another biome - transition-zone plants and rocks.</summary>
    OnlyNearBorders,
}

/// <summary>Terrain features a distance rule can measure to.</summary>
public enum PlacementFeature
{
    AnyWater,
    Ocean,
    Lake,
    Pond,
    River,
    /// <summary>Ground steeper than the Cliff Angle (see the Terrain Generator's object settings).</summary>
    Cliff,
    /// <summary>A feature your game registers by tag - roads, paths, settlements... (see PlacementFeatures.Register).</summary>
    Custom,
}

/// <summary>How an object relates to another kind of object (identified by its Group Tag).</summary>
public enum RelationKind
{
    /// <summary>Hard: this object never spawns within Radius of the other.</summary>
    CannotSpawnNear,
    /// <summary>Hard: the other object never spawns within Radius of this one.</summary>
    PreventsNearby,
    /// <summary>Soft: less likely within Radius of the other (by Strength).</summary>
    Avoids,
    /// <summary>Soft: more likely within Radius of the other (by Strength) - e.g. mushrooms near trees.</summary>
    AttractedTo,
    /// <summary>Hard: only spawns within Radius of the other.</summary>
    RequiresNearby,
}

/// <summary>How an object is rotated.</summary>
public enum OrientationMode
{
    /// <summary>Random turn around its up axis; tilted toward the terrain by Terrain Alignment.</summary>
    RandomYaw,
    /// <summary>Perfectly upright (ignores the terrain), random turn around its up axis.</summary>
    Upright,
    /// <summary>Any rotation at all - tumbled rocks and debris.</summary>
    FullyRandom,
    /// <summary>Faces along the water flow (in rivers), or along the nearest river's direction on its banks.</summary>
    AlongWaterFlow,
    /// <summary>Faces down the slope.</summary>
    FaceDownhill,
    /// <summary>Faces across the slope (along the contour line) - logs and ledges.</summary>
    AcrossSlope,
}

/// <summary>Which point of the object touches the ground.</summary>
public enum GroundAnchor
{
    /// <summary>The bottom of the object's bounds (renderers or colliders) - it sits on the ground whatever its pivot.</summary>
    BoundsBottom,
    /// <summary>The object's pivot is placed on the ground (only right for models whose pivot is at their base).</summary>
    Pivot,
    /// <summary>A height you give (Anchor Height, in the model's own units) touches the ground.</summary>
    CustomHeight,
}

/// <summary>What defines the part of an object that rests on the ground (its footprint).</summary>
public enum FootprintSource
{
    /// <summary>The measured base of its meshes (a tree's trunk, a rock's underside) when they are readable, else its colliders, else its renderers.</summary>
    Automatic,
    /// <summary>The box around its renderers (the whole object - a tree's canopy included).</summary>
    Renderers,
    /// <summary>The box around its colliders (usually just a tree's trunk).</summary>
    Colliders,
    /// <summary>A circle of Custom Radius around the pivot.</summary>
    CustomRadius,
}

/// <summary>How an object type is placed.</summary>
public enum PlacementMode
{
    /// <summary>Scattered over the world by density (trees, rocks, plants).</summary>
    Scatter,
    /// <summary>Rare landmarks: a few chosen spots per region (see Region Size), picked as the best-suited places there.</summary>
    Landmark,
}

[Serializable]
public class DensityNoiseRules
{
    [Tooltip("Vary this object's density with a smooth noise pattern, so it forms denser and sparser patches instead of an even spread.")]
    public bool enabled = true;
    [Tooltip("Size of the patches in world units.")]
    public float scale = 40f;
    [Tooltip("Noise layers: 1 = smooth blobs, more = ragged edges.")]
    [Range(1, 4)] public int octaves = 2;
    [Tooltip("How strongly the noise changes the density: 0 = not at all, 1 = from nothing to full density.")]
    [Range(0f, 1f)] public float strength = 0.5f;
    [Tooltip("Share of the area in the dense state (with Strength 1): low = rare patches, high = mostly covered with gaps.")]
    [Range(0f, 1f)] public float coverage = 0.5f;
}

[Serializable]
public class BiomeRules
{
    [Tooltip("Other biomes this object may also spawn in (besides the biome it is listed under) - e.g. a tree for both Forest and Taiga. Objects in two compatible biomes don't thin out at their shared border.")]
    public List<Biome> alsoAllowedIn = new List<Biome>();
    [Tooltip("Biomes this object never spawns in, even where listed.")]
    public List<Biome> forbiddenIn = new List<Biome>();
    [Tooltip("How this object treats borders with other biomes.\n\nAnywhere: borders don't matter (apart from Center Preference).\nAway From Borders: not within Border Distance of another biome.\nOnly Near Borders: only within Border Distance - transition-zone objects.")]
    public BiomeBorderMode borderMode = BiomeBorderMode.Anywhere;
    [Tooltip("World units: how close to another biome counts as 'near the border' for Border Mode and Center Preference.")]
    public float borderDistance = 20f;
    [Tooltip("Biomes this object must stay away from (never within Incompatible Distance of them) - e.g. no reeds near a desert.")]
    public List<Biome> incompatibleNeighbours = new List<Biome>();
    [Tooltip("World units to keep from Incompatible Neighbours.")]
    public float incompatibleDistance = 30f;
    [Tooltip("The whole footprint must be in an allowed biome, not just the centre - stops large objects hanging over a biome border.")]
    public bool footprintInBiome;
}

[Serializable]
public class AltitudeRules
{
    [Tooltip("Hard limits on absolute height (world Y).")]
    public bool limitAltitude;
    public float minAltitude = 0f;
    public float maxAltitude = 200f;

    [Tooltip("Prefer (soft) the middle of the biome's own height band (the Biome's Min/Max Height). Off by default: those heights mainly drive texturing.")]
    public bool useBiomeHeightBand;

    [Tooltip("Hard limits on height within the biome's height band: 0 = the biome's Min Height, 1 = its Max Height.")]
    public bool limitRelativeAltitude;
    [Range(0f, 1f)] public float minRelativeAltitude = 0f;
    [Range(0f, 1f)] public float maxRelativeAltitude = 1f;

    [Tooltip("Hard limits on height above the nearest water level (negative = below it). E.g. 2 to 20 for a plant that likes being a little above a lake.")]
    public bool limitHeightAboveWater;
    public float minHeightAboveWater = 0f;
    public float maxHeightAboveWater = 50f;

    [Tooltip("Where in the land's shape this object prefers to grow: valleys and hollows, slopes, ridges and crests...")]
    public TerrainPosition terrainPosition = TerrainPosition.Any;
    [Tooltip("Required: only there (hard). Off: just more likely there (soft, by Position Strength).")]
    public bool terrainPositionRequired;
    [Tooltip("For the soft preference: 0 = no effect, 1 = almost only there.")]
    [Range(0f, 1f)] public float positionStrength = 0.7f;
    [Tooltip("World units around a spot that decide whether it is a ridge, a valley or flat (and its relief, roughness and curvature below). Larger = broader landscape features.")]
    public float reliefRadius = 24f;

    [Tooltip("Hard limits on relief: height above (+) or below (-) the average of the surroundings within Relief Radius.")]
    public bool limitRelief;
    public float minRelief = -5f;
    public float maxRelief = 5f;
    [Tooltip("Hard limits on roughness: how much the height varies within Relief Radius (standard deviation, world units). Low = smooth ground.")]
    public bool limitRoughness;
    public float minRoughness = 0f;
    public float maxRoughness = 5f;
    [Tooltip("Hard limits on curvature: positive on bumps and crests, negative in hollows and gullies (height difference to the average within a few units).")]
    public bool limitCurvature;
    public float minCurvature = -1f;
    public float maxCurvature = 1f;
}

[Serializable]
public class SlopeRules
{
    [Tooltip("Hard minimum slope in degrees (with Slope Threshold as the maximum) - e.g. 40 for cliff plants.")]
    [Range(0f, 90f)] public float minSlope = 0f;
    [Tooltip("Prefer (soft) a slope range: most likely inside it, less likely further outside (by Slope Falloff).")]
    public bool usePreferredSlope;
    [Range(0f, 90f)] public float preferredMinSlope = 0f;
    [Range(0f, 90f)] public float preferredMaxSlope = 20f;
    [Tooltip("Degrees outside the preferred range over which the chance fades to nothing.")]
    public float slopeFalloff = 10f;
    [Tooltip("Kinds of surface allowed: Flat (<10°), Inclined (10-35°), Steep (35-60°), Vertical (>60°, cliff faces).")]
    public SurfaceMask surfaces = SurfaceMask.All;
}

[Serializable]
public class WaterRules
{
    [Tooltip("How this object relates to water.\n\nDry Land: never in water (default).\nAnywhere: water doesn't matter.\nNear Water: dry land within Max Water Distance (10 when left at 0).\nAway From Water: dry land at least Min Water Distance away (10 when left at 0).\nShoreline: dry land within Max Shore Distance of the water's edge (3 when left at 0).\nIn Water: inside the water (see Height In Water).")]
    public WaterPlacement placement = WaterPlacement.DryLand;
    [Tooltip("Which water bodies count for this object's water rules and distances.")]
    public WaterBodyMask bodies = WaterBodyMask.All;
    [Tooltip("World units from the nearest water of the chosen bodies (on dry land). Near Water uses Max (10 when 0), Away From Water uses Min (10 when 0); with the other modes, non-zero values are extra limits (0 = no limit).")]
    public float minWaterDistance = 0f;
    public float maxWaterDistance = 0f;
    [Tooltip("Shoreline: Max is the farthest on land from the water's edge (3 when 0). In Water: min/max distance from the shore out into the water (0 = no limit).")]
    public float minShoreDistance = 0f;
    public float maxShoreDistance = 0f;
    [Tooltip("In Water: allowed water depth at the spot, world units (0 max = no limit).")]
    public float minDepth = 0f;
    public float maxDepth = 0f;
    [Tooltip("In Water: sit on the bottom, float on the surface, or in between.")]
    public WaterHeightMode heightInWater = WaterHeightMode.OnBottom;
    [Tooltip("Submerged: position between bottom (0) and surface (1).")]
    [Range(0f, 1f)] public float submergedFraction = 0.5f;
    [Tooltip("Depth band weights (In Water): relative chance in shallow (< Shallow Depth), medium and deep (> Deep Depth) water.")]
    public float shallowDepth = 1.5f;
    public float deepDepth = 6f;
    [Range(0f, 1f)] public float shallowWeight = 1f;
    [Range(0f, 1f)] public float mediumWeight = 1f;
    [Range(0f, 1f)] public float deepWeight = 1f;
    [Tooltip("In rivers, how much a fast current discourages this object (0 = not at all, 1 = only in still water).")]
    [Range(0f, 1f)] public float avoidFastFlow = 0f;
}

[Serializable]
public class FeatureDistanceRule
{
    [Tooltip("What to measure the distance to.")]
    public PlacementFeature feature = PlacementFeature.AnyWater;
    [Tooltip("For Custom: the tag your game registered features with (roads, paths, settlements... see PlacementFeatures.Register).")]
    public string customTag = "";
    [Tooltip("Allowed distance range in world units (0 max = no maximum).")]
    public float minDistance = 0f;
    public float maxDistance = 0f;
    [Tooltip("Soft: outside the range the chance fades over Soft Falloff instead of dropping to zero.")]
    public bool soft;
    public float softFalloff = 10f;
}

[Serializable]
public class ClimateRules
{
    [Tooltip("Limit by climate moisture (0 dry - 1 wet, the value biomes are placed by, including rain shadows).")]
    public bool useMoisture;
    [Range(0f, 1f)] public float minMoisture = 0f;
    [Range(0f, 1f)] public float maxMoisture = 1f;
    [Tooltip("Limit by climate temperature (0 cold - 1 hot).")]
    public bool useTemperature;
    [Range(0f, 1f)] public float minTemperature = 0f;
    [Range(0f, 1f)] public float maxTemperature = 1f;
    [Tooltip("Limit by ground wetness near water (0 dry - 1 at the water's edge).")]
    public bool useGroundWetness;
    [Range(0f, 1f)] public float minWetness = 0f;
    [Range(0f, 1f)] public float maxWetness = 1f;
    [Tooltip("0 = hard limits. Above 0, the chance fades over this width outside the ranges instead.")]
    [Range(0f, 0.5f)] public float softness = 0f;
}

[Serializable]
public class ClusterRules
{
    [Tooltip("Density multiplier inside a cluster (with Is Clusterable on).")]
    public float insideDensity = 3f;
    [Tooltip("Density multiplier outside clusters: 0 = only in clusters, 1 = clusters are just denser spots.")]
    [Range(0f, 1f)] public float outsideDensity = 0.3f;
    [Tooltip("Aim for about this many objects per cluster (0 = just use the densities). Adjusts the inside density for the cluster's size.")]
    public int desiredClusterSize = 0;
    [Tooltip("Never more than this many objects per cluster (0 = no limit).")]
    public int maxClusterSize = 0;
    [Tooltip("Growth: objects that didn't make it by chance get another try when matching objects are within this radius (0 = off) - forests fill in, rock fields spread.")]
    public float growthRadius = 0f;
    [Tooltip("Growth: extra chance per matching object nearby (0.5 = +50% of the base chance each).")]
    public float growthPerNeighbour = 0.5f;
}

[Serializable]
public class SpacingRules
{
    [Tooltip("Soft spacing: beyond the hard Min Spacing, nearby copies still make this object less likely up to this distance (0 = off) - evens out the spread.")]
    public float softSpacing = 0f;
    [Tooltip("How much each nearby copy lowers the chance within Soft Spacing.")]
    [Range(0f, 1f)] public float softSpacingStrength = 0.5f;
}

[Serializable]
public class ObjectRelation
{
    [Tooltip("Group Tag of the other object (its prefab name when it has no tag).")]
    public string otherTag = "";
    [Tooltip("Cannot Spawn Near: this object never spawns within Radius of the other.\nPrevents Nearby: the other never spawns within Radius of this one.\nAvoids: this is less likely near the other.\nAttracted To: this is more likely near the other.\nRequires Nearby: this only spawns within Radius of the other.")]
    public RelationKind kind = RelationKind.CannotSpawnNear;
    [Tooltip("World units.")]
    public float radius = 5f;
    [Tooltip("For Avoids/Attracted To: 0 = no effect, 1 = strongest.")]
    [Range(0f, 1f)] public float strength = 0.5f;
}

[Serializable]
public class OrientationRules
{
    [Tooltip("Random Yaw: random turn, tilted toward the terrain by Terrain Alignment.\nUpright: always straight up.\nFully Random: any rotation (tumbled rocks).\nAlong Water Flow: faces downstream in rivers / along the river on its banks.\nFace Downhill / Across Slope: turned by the slope direction.")]
    public OrientationMode mode = OrientationMode.RandomYaw;
    [Tooltip("How much the object tilts to follow the ground: 0 = stays upright, 1 = fully perpendicular to the terrain. Trees ~0.1, rocks ~0.8.")]
    [Range(0f, 1f)] public float terrainAlignment = 1f;
    [Tooltip("Never tilt more than this from straight up (degrees). 90 = no limit.")]
    [Range(0f, 90f)] public float maxTilt = 90f;
    [Tooltip("Random turn around the up axis (degrees). 360 = any direction; small values keep the direction of Along Water Flow / Face Downhill.")]
    [Range(0f, 360f)] public float yawJitter = 360f;
    [Tooltip("Random extra tilt in degrees, for natural variety.")]
    [Range(0f, 45f)] public float randomTilt = 0f;
    [Tooltip("Reject spots where the object's up direction would be more than this from the terrain's normal (degrees). 180 = no limit.")]
    [Range(0f, 180f)] public float maxNormalDeviation = 180f;
}

[Serializable]
public class GroundContactRules
{
    [Tooltip("Which point of the object touches the ground.\n\nBounds Bottom: the bottom of its renderers/colliders (right whatever the pivot).\nPivot: the pivot (only if the model's pivot is at its base).\nCustom Height: Anchor Height (model units) touches the ground.")]
    public GroundAnchor anchor = GroundAnchor.BoundsBottom;
    [Tooltip("Custom Height: the model-space height that should touch the ground.")]
    public float anchorHeight = 0f;
    [Tooltip("What rests on the ground (the footprint), used to fit the object to the ground and to check the ground under it.\n\nAutomatic: the measured base of its meshes - a tree's trunk rather than its canopy (needs Read/Write enabled on the meshes), else its colliders, else its renderers.\nRenderers: the box around the whole visible object.\nColliders: the box around its colliders (for trees usually the trunk).\nCustom Radius: a circle of Custom Radius.\n\nSpacing between objects always uses the whole object's size.")]
    public FootprintSource footprint = FootprintSource.Automatic;
    [Tooltip("Custom Radius footprint, world units at scale 1.")]
    public float customRadius = 0.5f;
    [Tooltip("Shrinks the footprint box around its centre: the share of it that really rests on the ground. Lower it when a footprint is much wider than what touches the ground (a tree measured by its renderers), so the object isn't rejected on slopes.")]
    [Range(0.05f, 1f)] public float footprintScale = 1f;
    [Tooltip("How far the base is pushed into the ground (world units) so no gap shows under it.")]
    public float sinkDepth = 0.05f;
    [Tooltip("Largest part of the base that may end up inside the ground on uneven terrain (world units). Spots needing more are rejected.")]
    public float maxPenetration = 0.4f;
    [Tooltip("Largest gap allowed under the base on uneven terrain (world units). Spots needing more are rejected - large objects then only land on ground that fits them.")]
    public float maxFloating = 0.1f;
    [Tooltip("Share of the footprint that must be valid (dry for land objects, wet for water objects, in an allowed biome with Footprint In Biome). 1 = all of it.")]
    [Range(0f, 1f)] public float minValidFootprint = 1f;
    [Tooltip("Extra height offset after contact (world units, + = up).")]
    public float verticalOffset = 0f;
    [Tooltip("After spawning, measure the real object's bounds and correct its height if it doesn't sit exactly on the ground (upright objects).")]
    public bool verifyAfterSpawn = true;
}

[Serializable]
public class LimitRules
{
    [Tooltip("Scatter: spread by density (most objects).\nLandmark: rare objects - the best-suited spots in each region (see Region Size), with the options below.")]
    public PlacementMode mode = PlacementMode.Scatter;
    [Tooltip("Size of the world regions (world units) for Max Per Region and landmarks.")]
    public float regionSize = 1000f;
    [Tooltip("At most this many per region (0 = no limit; Landmark: 0 counts as 1).")]
    public int maxPerRegion = 0;
    [Tooltip("Landmark: world-wide minimum distance between any two of them (0 = none).")]
    public float minDistanceBetween = 0f;
    [Tooltip("Landmark: every region with a suitable spot gets one, even when the chance roll says no.")]
    public bool guaranteed;
    [Tooltip("Landmark: only one in the whole world - the best spot found in the region nearest Unique Search Center that has one.")]
    public bool unique;
    public Vector2 uniqueSearchCenter = Vector2.zero;
    [Tooltip("How far (world units) from Unique Search Center to look for the unique landmark's spot.")]
    public float uniqueSearchRadius = 3000f;
    [Tooltip("Landmark: candidate spots tried per region (more = better spots, slower).")]
    public int candidatesPerRegion = 48;
}

[Serializable]
public class SeedRules
{
    [Tooltip("On: placement follows the world seed (a new seed gives a new arrangement). Off: uses Fixed Seed, so this object stays put whatever the world seed.")]
    public bool useWorldSeed = true;
    public int fixedSeed = 0;
    [Tooltip("Change to reshuffle only this object's placement.")]
    public int seedOffset = 0;
}
