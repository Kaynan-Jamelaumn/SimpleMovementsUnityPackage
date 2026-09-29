using System.Collections.Generic;
using UnityEngine;

/// <summary>What happens to a portal's site once the player has used it (see <see cref="PortalSettings.closeAfterUse"/>).</summary>
public enum PortalUseRule
{
    /// <summary>The portal stays; the same dungeon can be entered again.</summary>
    StaysOpen,
    /// <summary>The portal closes as soon as the player goes through.</summary>
    CloseAfterEntering,
    /// <summary>The portal closes when the player finishes its dungeon (leaves through the exit portal); it stays if they just leave.</summary>
    CloseAfterCompleting,
}

/// <summary>
/// How world portals are placed on the endless terrain (edited on <see cref="EndlessTerrain"/>, used by each
/// chunk's <see cref="PortalSpawner"/>).
///
/// The world is divided into square regions of <see cref="regionSize"/>. Each region has
/// <see cref="maxNumberOfPortals"/> slots; each slot gets a portal with <see cref="spawnChance"/>, at a point and of a
/// type picked from the world seed - so the same seed always puts the same portals in the same places (and each
/// portal leads to the same dungeon), whatever order chunks load in. When the chunk holding a point is ready, the
/// flattest open spot within <see cref="searchRadius"/> of it (dry, not too steep, clear of trees and rocks, in an
/// allowed biome, on the NavMesh) receives the portal. The portal is removed with its chunk and comes back
/// identically with it.
/// </summary>
[System.Serializable]
public class PortalSettings : BaseSettings
{
    [Header("Portal Types")]
    [Tooltip("REQUIRED. The portal prefabs that can appear, each with its weight and restrictions. A prefab needs a Portal component and a trigger Collider (Tools > SimpleMovements > Dungeon > Create World Portal Prefab makes one). Empty = no portals.")]
    public List<SpawnablePortal> prefabs = new List<SpawnablePortal>();

    [Header("How Many and Where (world regions)")]
    [Tooltip("Size of the square world regions portals are planned in (world units). Each region gets up to Max Number Of Portals. Larger = portals further apart. A chunk is Terrain Size - 1 units wide (240 for Extra Large). Recommended 400-1200.")]
    [Min(64f)] public float regionSize = 600f;

    [Tooltip("Portal slots per region (the most portals a region can have). Recommended 1-2.")]
    [Min(0)] public int maxNumberOfPortals = 1;

    [Tooltip("Chance (0-1) that each slot actually gets a portal. 1 = every region has Max Number Of Portals (handy for testing), 0.5 = about half of the slots.")]
    [Range(0f, 1f)] public float spawnChance = 0.5f;

    [Tooltip("Smallest distance between two portals anywhere in the world (world units). Capped at 90% of Region Size.")]
    [Min(0f)] public float minDistanceBetweenPortals = 150f;

    [Tooltip("How far (world units) a portal may move from its planned point to find a flat, open spot. Larger finds more spots but moves portals further. Recommended 16-40.")]
    [Min(0f)] public float searchRadius = 24f;

    [Tooltip("Extra distance (world units) kept between the searched area and the chunk's edges, so a portal only depends on its own chunk's data (that's what makes placement independent of loading order). Recommended 4-16.")]
    [Min(0f)] public float edgeAvoidanceDistance = 8f;

    [Header("Ground")]
    [Tooltip("Radius of the ground that must be flat and free under a portal (world units). 0 = measured from the prefab's renderers.")]
    [Min(0f)] public float footprintRadius = 0f;

    [Tooltip("Steepest ground allowed under the portal (degrees). Recommended 8-15.")]
    [Range(0f, 45f)] public float maxSlope = 12f;

    [Tooltip("Largest height difference allowed across the footprint (world units). Recommended 0.3-1.")]
    [Min(0.01f)] public float maxUnevenness = 0.6f;

    [Tooltip("Free space kept between the portal's footprint and placed objects such as trees and rocks (world units).")]
    [Min(0f)] public float objectClearance = 2f;

    [Tooltip("Only on spots with a NavMesh (so the player and mobs can walk up to it). Ignored when EndlessTerrain's Bake NavMesh is off.")]
    public bool requireNavMesh = true;

    [Tooltip("How far the portal is pushed into the ground so no gap shows on uneven terrain (world units).")]
    [Min(0f)] public float sinkDepth = 0.05f;

    [Header("Environmental Restrictions")]
    [Tooltip("Only place portals between Min and Max Spawn Height (world Y).")]
    public bool useHeightRestrictions = false;

    [Tooltip("Lowest ground height for a portal (world Y), with Use Height Restrictions.")]
    public float minSpawnHeight = 0f;

    [Tooltip("Highest ground height for a portal (world Y), with Use Height Restrictions.")]
    public float maxSpawnHeight = 100f;

    [Tooltip("Apply Global Forbidden Biomes (and each type's Preferred Biomes).")]
    public bool useBiomeRestrictions = true;

    [Tooltip("Biomes where no portal ever appears (with Use Biome Restrictions).")]
    public List<Biome> globalForbiddenBiomes = new List<Biome>();

    [Header("Type Selection")]
    [Tooltip("Pick the type of each portal by Base Spawn Weight and Rarity Level. Off = every type equally likely.")]
    public bool useWeightedSelection = true;

    [Tooltip("How strongly Rarity Level lowers a type's chance: 0.1 = barely, 1 = level 10 is ten times rarer than level 1, 3 = much more.")]
    [Range(0.1f, 3f)] public float rarityFavorBias = 1f;

    [Header("After Use")]
    [Tooltip("What happens to a portal once the player has used it.\n\nStays Open: it stays; the same dungeon can be entered again.\nClose After Entering: it disappears as soon as the player goes through.\nClose After Completing: it disappears once the player finishes its dungeon.")]
    public PortalUseRule closeAfterUse = PortalUseRule.StaysOpen;

    [Tooltip("Seconds before a closed portal comes back (0 = never, for this session - see WorldSpawnRegistry.ClosedPortalSites to save it).")]
    [Min(0f)] public float reopenAfterUse = 0f;

    [Header("Dungeon Difficulty")]
    [Tooltip("Portals further from Difficulty Origin lead to harder dungeons (overrides the difficulty on the Portal prefab, unless the Portal's Use Spawner Difficulty is off).")]
    public bool scaleDifficultyWithDistance = true;

    [Tooltip("World (x, z) where difficulty is lowest - usually the player's starting point.")]
    public Vector2 difficultyOrigin = Vector2.zero;

    [Tooltip("Dungeon difficulty at Difficulty Origin (1 = normal).")]
    [Min(0.1f)] public float difficultyAtOrigin = 1f;

    [Tooltip("Difficulty added per 1000 world units from Difficulty Origin.")]
    [Min(0f)] public float difficultyPerKilometer = 0.5f;

    [Tooltip("Difficulty never goes above this.")]
    [Min(0.1f)] public float maxDifficulty = 5f;

    [Header("Player Proximity")]
    [Tooltip("Apply Min and Max Distance From Player.")]
    public bool enablePlayerProximityInfluence = true;

    [Tooltip("A portal never appears (or reappears) closer than this to the player (world units), so it doesn't pop into existence in front of them. Portals already standing stay. Recommended 30-80.")]
    [Min(0f)] public float minDistanceFromPlayer = 40f;

    [Tooltip("Portals only appear within this distance of the player (world units, 0 = in every ready chunk). Chunks only become ready within EndlessTerrain's NavMesh Distance anyway.")]
    [Min(0f)] public float maxDistanceFromPlayer = 0f;

    /// <summary>Clamps values to safe ranges and reports configuration problems.</summary>
    public void ValidateSettings()
    {
        regionSize = Mathf.Max(64f, regionSize);
        maxNumberOfPortals = Mathf.Max(0, maxNumberOfPortals);
        minDistanceBetweenPortals = Mathf.Max(0f, minDistanceBetweenPortals);
        searchRadius = Mathf.Max(0f, searchRadius);
        edgeAvoidanceDistance = Mathf.Max(0f, edgeAvoidanceDistance);
        maxUnevenness = Mathf.Max(0.01f, maxUnevenness);
        maxDifficulty = Mathf.Max(0.1f, maxDifficulty);
        if (useHeightRestrictions && minSpawnHeight > maxSpawnHeight)
        {
            float t = minSpawnHeight;
            minSpawnHeight = maxSpawnHeight;
            maxSpawnHeight = t;
        }
        if (maxDistanceFromPlayer > 0f && maxDistanceFromPlayer < minDistanceFromPlayer)
            maxDistanceFromPlayer = minDistanceFromPlayer;
        if (prefabs != null)
            foreach (SpawnablePortal p in prefabs)
                p?.ValidateConfiguration();
        ValidateBaseSettings();
    }

    /// <summary>Problems that stop portals from appearing (empty when the settings look usable).</summary>
    public List<string> GetProblems()
    {
        var problems = new List<string>();
        if (prefabs == null || prefabs.Count == 0)
            problems.Add("No portal types: add an entry to Prefabs with a portal prefab.");
        else
        {
            for (int i = 0; i < prefabs.Count; i++)
            {
                SpawnablePortal p = prefabs[i];
                if (p == null || p.prefab == null)
                    problems.Add($"Prefabs[{i}] has no prefab.");
                else if (p.prefab.GetComponentInChildren<Portal>(true) == null)
                    problems.Add($"Prefabs[{i}] ({p.prefab.name}) has no Portal component - it would appear but do nothing.");
            }
        }
        if (maxNumberOfPortals <= 0 || spawnChance <= 0f)
            problems.Add("Max Number Of Portals or Spawn Chance is 0: no portal can appear.");
        return problems;
    }

    /// <summary>True when portals may appear in <paramref name="biome"/> (Global Forbidden Biomes).</summary>
    public bool IsBiomeAllowed(Biome biome)
    {
        if (!useBiomeRestrictions || biome == null)
            return true;
        return globalForbiddenBiomes == null || !globalForbiddenBiomes.Contains(biome);
    }

    /// <summary><see cref="minDistanceBetweenPortals"/> as applied: at most 90% of <see cref="regionSize"/>.</summary>
    public float EffectiveMinDistance => Mathf.Min(minDistanceBetweenPortals, regionSize * 0.9f);

    /// <summary>The dungeon difficulty of a portal at world (x, z) (1 when Scale Difficulty With Distance is off).</summary>
    public float DifficultyAt(Vector2 position)
    {
        if (!scaleDifficultyWithDistance)
            return 1f;
        float km = Vector2.Distance(position, difficultyOrigin) / 1000f;
        return Mathf.Clamp(difficultyAtOrigin + km * difficultyPerKilometer, 0.1f, maxDifficulty);
    }

    public string GetDebugInfo()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Portal Settings ===");
        sb.AppendLine($"Types: {(prefabs != null ? prefabs.Count : 0)}");
        sb.AppendLine($"Region: {regionSize} units, {maxNumberOfPortals} slot(s), chance {spawnChance:0.00}, min distance {EffectiveMinDistance:0}");
        sb.AppendLine($"Ground: slope <= {maxSlope}, unevenness <= {maxUnevenness}, search {searchRadius}, NavMesh {requireNavMesh}");
        sb.AppendLine($"After use: {closeAfterUse} (reopen {(reopenAfterUse > 0f ? reopenAfterUse + " s" : "never")})");
        return sb.ToString();
    }

    /// <summary>Sensible defaults (keeps the prefab list).</summary>
    public void ResetToDefaults()
    {
        regionSize = 600f;
        maxNumberOfPortals = 1;
        spawnChance = 0.5f;
        minDistanceBetweenPortals = 150f;
        searchRadius = 24f;
        edgeAvoidanceDistance = 8f;
        footprintRadius = 0f;
        maxSlope = 12f;
        maxUnevenness = 0.6f;
        objectClearance = 2f;
        requireNavMesh = true;
        sinkDepth = 0.05f;
        useHeightRestrictions = false;
        minSpawnHeight = 0f;
        maxSpawnHeight = 100f;
        useBiomeRestrictions = true;
        globalForbiddenBiomes = new List<Biome>();
        useWeightedSelection = true;
        rarityFavorBias = 1f;
        closeAfterUse = PortalUseRule.StaysOpen;
        reopenAfterUse = 0f;
        scaleDifficultyWithDistance = true;
        difficultyOrigin = Vector2.zero;
        difficultyAtOrigin = 1f;
        difficultyPerKilometer = 0.5f;
        maxDifficulty = 5f;
        enablePlayerProximityInfluence = true;
        minDistanceFromPlayer = 40f;
        maxDistanceFromPlayer = 0f;
        shouldWaitToStartSpawning = false;
        retryingSpawnTime = 3f;
        playerTag = "Player";
    }
}
