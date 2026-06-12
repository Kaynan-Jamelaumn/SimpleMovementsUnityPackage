using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// An object placed in a biome (tree, rock, plant, landmark...), with the rules that decide where and how it
/// spawns. Placement is deterministic - the same world seed always gives the same objects in the same
/// places, whatever order chunks load in - and runs off the main thread (see <see cref="ObjectPlacementEngine"/>).
/// Every rule group defaults to "no constraint", so an object only gets pickier for the rules you set.
/// </summary>
[System.Serializable]
public class BiomeObject
{
    [Tooltip("The prefab to place.")]
    public GameObject terrainObject;

    [Tooltip("Name other objects use for this one in their Relationships (e.g. 'Tree', 'Rock'). Several objects can share a tag. Empty = the prefab's name.")]
    public string groupTag = "";

    [Header("Density")]
    [Tooltip("Base chance (%) per square world unit: 1 = about one object per 100 square units (576 per chunk), before the other rules raise or lower it. Very high values are capped by Min Spacing.")]
    public float probabilityToSpawn;

    [Tooltip("Smooth noise that makes this object come in denser and sparser patches.")]
    public DensityNoiseRules densityNoise = new DensityNoiseRules();

    [Tooltip("Limit how many of this object each chunk can have.")]
    public bool hasMaxNumberOfObjects;

    [Tooltip("Maximum per chunk (with Has Max Number Of Objects). For world-wide or per-region limits, see Limits & Landmarks.")]
    public int maxNumberOfThisObject;

    [HideInInspector]
    [Tooltip("No longer used: counts are now per chunk and computed during placement.")]
    public int currentNumberOfThisObject;

    [Tooltip("Objects with a higher priority are placed first, so they claim space before others (e.g. big trees before bushes). Relationships reorder objects when needed. Equal priorities: larger footprints first.")]
    public int placementPriority = 0;

    [Tooltip("Not used anymore (placement density now comes from the rules); kept so older code still compiles.")]
    [System.NonSerialized]
    public float[,] densityMap;

    [Header("Biomes")]
    [Tooltip("Which biomes (besides this one) the object may spawn in, which it must avoid, and how it treats biome borders.")]
    public BiomeRules biomes = new BiomeRules();

    [Tooltip("0 = spawns right up to the biome's border; 1 = thins out toward the border (within Border Distance), for a gradual transition into the next biome.")]
    [Range(0f, 1f)]
    public float biomeCenterPreference = 0.5f;

    [Header("Height")]
    [Tooltip("Prefer (soft) a height band given below, most likely at the Optimal height.")]
    public bool useCustomHeightPreference = false;

    [Tooltip("Preferred minimum height (world Y).")]
    public float preferredMinHeight = 0f;

    [Tooltip("Height (world Y) where this object is most likely.")]
    public float preferredOptimalHeight = 50f;

    [Tooltip("Preferred maximum height (world Y).")]
    public float preferredMaxHeight = 100f;

    [Tooltip("How strict the height preference is: higher = rarer away from the Optimal height.")]
    [Range(0.1f, 5f)]
    public float heightPreferenceStrength = 1f;

    [Tooltip("Hard altitude limits, height within the biome, height above water, and where in the land's shape (valleys, slopes, ridges) the object grows.")]
    public AltitudeRules altitude = new AltitudeRules();

    [Header("Slope")]
    [Tooltip("Maximum ground slope in degrees (times Slope Avoidance).")]
    public float slopeThreshold = 80f;

    [Tooltip("Multiplier on Slope Threshold: below 1 = keeps off slopes more, above 1 = tolerates steeper ground.")]
    [Range(0.1f, 2f)]
    public float slopeAvoidance = 1f;

    [Tooltip("Minimum slope, preferred slope range and allowed surface kinds (flat, inclined, steep, vertical).")]
    public SlopeRules slope = new SlopeRules();

    [Header("Water")]
    [Tooltip("Dry land, near/away from water, shoreline, or in water (on the bottom, floating, submerged), with distance and depth limits.")]
    public WaterRules water = new WaterRules();

    [Header("Climate")]
    [Tooltip("Limits by climate moisture, temperature and ground wetness.")]
    public ClimateRules climate = new ClimateRules();

    [Header("Distances To Features")]
    [Tooltip("Keep within, or away from, water bodies, cliffs, or features your game registers (roads, paths, settlements).")]
    public List<FeatureDistanceRule> featureDistances = new List<FeatureDistanceRule>();

    [Header("Clustering")]
    [Tooltip("Gather this object in clusters (forests, rock fields, flower patches).")]
    public bool isClusterable = true;

    [Tooltip("About how many clusters per chunk area.")]
    public int clusterCount = 5;

    [Tooltip("Radius of each cluster (world units).")]
    public float clusterRadius = 50f;

    [Tooltip("Densities inside/outside clusters, cluster size targets and limits, and growth near matching objects.")]
    public ClusterRules clustering = new ClusterRules();

    [Header("Spacing")]
    [Tooltip("Minimum distance to another copy of this object (world units, hard). 0 = worked out from the object's footprint.")]
    public float minSpacing = 0f;

    [Tooltip("Soft spacing beyond Min Spacing.")]
    public SpacingRules spacing = new SpacingRules();

    [Header("Relationships")]
    [Tooltip("How this object relates to others by their Group Tag: can't spawn near, prevents nearby, avoids, attracted to, requires nearby.")]
    public List<ObjectRelation> relations = new List<ObjectRelation>();

    [Header("Orientation & Scale")]
    [Tooltip("Rotation: random turn, upright, fully random, along water flow, downhill or across the slope, and how much it tilts with the terrain.")]
    public OrientationRules orientation = new OrientationRules();

    [Tooltip("Random uniform scale range (x = min, y = max).")]
    public Vector2 scaleRange = Vector2.one;

    [Header("Ground Contact")]
    [Tooltip("How the object sits on the ground: which point touches it, how much sinking or floating is allowed on uneven ground, and how its footprint is measured.")]
    public GroundContactRules ground = new GroundContactRules();

    [Header("Limits & Landmarks")]
    [Tooltip("Scatter or Landmark placement, per-region limits, and landmark options (unique, guaranteed, world-wide spacing).")]
    public LimitRules limits = new LimitRules();

    [Header("Random Seed")]
    [Tooltip("Follow the world seed or use a fixed one, and reshuffle this object alone.")]
    public SeedRules seed = new SeedRules();

    [System.NonSerialized]
    private float cachedEffectiveMinSpacing = -1f;

    /// <summary>The name relationships refer to this object by: <see cref="groupTag"/>, or the prefab's name.</summary>
    public string EffectiveTag => !string.IsNullOrEmpty(groupTag) ? groupTag : (terrainObject != null ? terrainObject.name : "");

    /// <summary>
    /// Returns the minimum spacing to enforce between instances of this object within a chunk.
    /// If <see cref="minSpacing"/> was left at 0, derives and caches a sensible default from the
    /// prefab's collider bounds instead of requiring every object type to be hand-tuned.
    /// (Placement itself now derives it from the object's measured footprint - see <see cref="PrefabShape"/>.)
    /// </summary>
    public float GetEffectiveMinSpacing()
    {
        if (cachedEffectiveMinSpacing >= 0f)
            return cachedEffectiveMinSpacing;

        if (minSpacing > 0f)
        {
            cachedEffectiveMinSpacing = minSpacing;
            return cachedEffectiveMinSpacing;
        }

        Collider collider = terrainObject != null ? terrainObject.GetComponent<Collider>() : null;
        cachedEffectiveMinSpacing = collider != null ? collider.bounds.extents.magnitude * 1.2f : 0f;
        return cachedEffectiveMinSpacing;
    }
}
