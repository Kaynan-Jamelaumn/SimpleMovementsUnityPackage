using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How mobs live on the endless terrain (edited on <see cref="EndlessTerrain"/>, used by each chunk's
/// <see cref="MobSpawner"/>).
///
/// Every chunk has a population: up to <see cref="maxNumberOfMobs"/>, fewer where few biomes suit any mob type.
/// Mobs only exist in chunks near the player (<see cref="activationDistance"/>): they are spawned there a few at a
/// time on valid ground (on the NavMesh, dry, not too steep, clear of trees and rocks, allowed biome and height, not
/// too close to the player or each other) and removed again when the player moves away
/// (<see cref="deactivationDistance"/>) or the chunk unloads. Mobs that were merely removed come back when the player
/// returns; killed ones only after <see cref="respawnDelay"/> - remembered per chunk, even while it is unloaded.
/// </summary>
[System.Serializable]
public class MobSettings : BaseSettings
{
    [Header("Mob Types")]
    [Tooltip("REQUIRED. The mob prefabs that can appear, each with its weight, biomes and pack behaviour. A prefab should have a NavMeshAgent (mobs are placed on the NavMesh of its agent type) and ideally a Mob component (so kills are counted). Empty = no mobs.")]
    public List<SpawnableMob> prefabs = new List<SpawnableMob>();

    [Header("Population")]
    [Tooltip("Most mobs one chunk holds at a time. A chunk's actual population is this times the share of it where some mob type may live, varied by Population Variation. Recommended 3-10 for a 240-unit chunk.")]
    [Min(0)] public int maxNumberOfMobs = 6;

    [Tooltip("How much populations vary between chunks (0 = every suitable chunk gets Max Number Of Mobs, 0.5 = between 50% and 100% of it).")]
    [Range(0f, 1f)] public float populationVariation = 0.3f;

    [Tooltip("World-wide limit on mobs from all chunks (0 = no limit). Protects performance when many chunks are active. Recommended 40-120.")]
    [Min(0)] public int maxMobsInWorld = 60;

    [Header("Activation (chunks near the player)")]
    [Tooltip("A chunk spawns its mobs while its nearest edge is within this distance of the player (world units). Must be less than EndlessTerrain's NavMesh Distance (mobs need the NavMesh). Recommended 80-160.")]
    [Min(10f)] public float activationDistance = 120f;

    [Tooltip("A chunk removes its mobs when the player is farther than this (world units) - they come back when the player returns (unless killed). Keep it above Activation Distance so mobs don't flicker at the boundary.")]
    [Min(10f)] public float deactivationDistance = 180f;

    [Tooltip("Seconds between spawns in one chunk while it fills up. Low = the area fills quickly. Recommended 0.5-3.")]
    [Min(0.05f)] public float spawnInterval = 1.5f;

    [Header("Respawning")]
    [Tooltip("Seconds before a killed mob is replaced in its chunk (remembered while the chunk is unloaded). Recommended 120-600.")]
    [Min(0f)] public float respawnDelay = 180f;

    [Tooltip("Random variation of Respawn Delay (0.25 = +/-25%).")]
    [Range(0f, 1f)] public float respawnDelayVariation = 0.25f;

    [Header("Pack Behavior")]
    [Tooltip("Mob types marked Is Pack Animal may spawn as a group.")]
    public bool enablePackSpawning = true;

    [Tooltip("Multiplies every type's Pack Spawn Chance (0 = no packs, 2 = twice as often).")]
    [Range(0f, 2f)] public float globalPackSpawnChanceModifier = 1f;

    [Tooltip("How far pack members spawn from the pack's centre (world units), unless the type sets its own Pack Spread Radius.")]
    [Range(2f, 50f)] public float packSpawnRadius = 12f;

    [Tooltip("Smallest pack.")]
    [Range(2, 8)] public int minPackSize = 2;

    [Tooltip("Largest pack (a type's Preferred Pack Size is used instead when set).")]
    [Range(2, 12)] public int maxPackSize = 4;

    [Header("Spacing")]
    [Tooltip("Minimum distance between two mobs when spawning (world units). Pack members use half of it.")]
    [Range(0.5f, 25f)] public float minDistanceBetweenMobs = 5f;

    [Header("Environmental and Biome Awareness")]
    [Tooltip("Weigh types by biome: more likely in their Preferred Biomes, less likely elsewhere (see the two multipliers).")]
    public bool useBiomeSpawnModifiers = true;

    [Tooltip("Weight multiplier where a type is in one of its Preferred Biomes.")]
    [Range(1f, 5f)] public float preferredBiomeMultiplier = 2f;

    [Tooltip("Weight multiplier where a type has Preferred Biomes but isn't in one.")]
    [Range(0.05f, 1f)] public float nonPreferredBiomeMultiplier = 0.5f;

    [Tooltip("Biomes where no mob ever spawns.")]
    public List<Biome> globalForbiddenBiomes = new List<Biome>();

    [Tooltip("Keep mobs within a height band: the type's own (Limit Height) or else the biome's Min/Max Height, widened by Global Height Tolerance.")]
    public bool useHeightBasedSpawning = false;

    [Tooltip("World units added above and below the height band (with Use Height Based Spawning).")]
    [Range(0f, 50f)] public float globalHeightTolerance = 10f;

    [Header("Spawn Position")]
    [Tooltip("Spots tried per spawn before waiting Retrying Spawn Time. Recommended 8-20.")]
    [Range(1, 50)] public int maxSpawnAttempts = 12;

    [Tooltip("How far a spot may be from the NavMesh (world units); the mob is placed exactly on it.")]
    [Range(0.1f, 10f)] public float navMeshSampleDistance = 2f;

    [Tooltip("Steepest ground a mob spawns on (degrees); a type's Max Spawn Slope can lower it.")]
    [Range(0f, 90f)] public float maxSlope = 35f;

    [Tooltip("Free space kept between a mob and placed objects such as trees and rocks (world units). -1 = ignore objects.")]
    public float objectClearance = 0.5f;

    [Tooltip("Don't spawn where the player's camera can see, within Hidden Spawn Distance - mobs appear out of sight instead of popping in.")]
    public bool avoidCameraView = true;

    [Tooltip("With Avoid Camera View: spots in view closer than this are skipped (world units).")]
    [Min(0f)] public float hiddenSpawnDistance = 70f;

    [Header("Time of Day")]
    [Tooltip("Weigh types by the day/night cycle (DayNightManager): types marked Is Diurnal / Is Nocturnal are rarer outside their time.")]
    public bool enableTimeBasedSpawning = false;

    [Tooltip("Weight multiplier for a type outside its active time (0 = never, 1 = no effect).")]
    [Range(0f, 1f)] public float inactiveTimeWeight = 0.15f;

    [Header("Player Interaction and Safety")]
    [Tooltip("Apply Min/Max Distance From Player.")]
    public bool enablePlayerProximityInfluence = true;

    [Tooltip("No mob spawns closer than this to the player (world units); a type's own Min Distance From Player can raise it.")]
    [Range(0f, 200f)] public float minDistanceFromPlayer = 30f;

    [Tooltip("No mob spawns farther than this from the player (world units, 0 = no limit besides Activation Distance).")]
    [Range(0f, 1000f)] public float maxDistanceFromPlayer = 0f;

    [Tooltip("No mob spawns within Safe Zone Radius of the Safe Zone Centers (e.g. towns, the player's base).")]
    public bool enableSafeZones = false;

    [Tooltip("Radius of each safe zone (world units).")]
    [Range(5f, 500f)] public float safeZoneRadius = 60f;

    [Tooltip("World positions of the safe zones (add more at runtime with MobSpawner.AddSafeZone).")]
    public List<Vector3> safeZoneCenters = new List<Vector3>();

    /// <summary>Clamps values to safe ranges.</summary>
    public void ValidateSettings()
    {
        maxNumberOfMobs = Mathf.Max(0, maxNumberOfMobs);
        maxMobsInWorld = Mathf.Max(0, maxMobsInWorld);
        activationDistance = Mathf.Max(10f, activationDistance);
        deactivationDistance = Mathf.Max(activationDistance + 10f, deactivationDistance);
        spawnInterval = Mathf.Max(0.05f, spawnInterval);
        respawnDelay = Mathf.Max(0f, respawnDelay);
        minPackSize = Mathf.Max(2, minPackSize);
        maxPackSize = Mathf.Max(minPackSize, maxPackSize);
        minDistanceBetweenMobs = Mathf.Max(0.5f, minDistanceBetweenMobs);
        maxSpawnAttempts = Mathf.Max(1, maxSpawnAttempts);
        navMeshSampleDistance = Mathf.Max(0.1f, navMeshSampleDistance);
        if (maxDistanceFromPlayer > 0f && maxDistanceFromPlayer < minDistanceFromPlayer)
            maxDistanceFromPlayer = minDistanceFromPlayer;
        if (prefabs != null)
            foreach (SpawnableMob m in prefabs)
                m?.ValidateConfiguration();
        ValidateBaseSettings();
    }

    /// <summary>Problems that stop mobs from appearing (empty when the settings look usable).</summary>
    public List<string> GetProblems(float navMeshDistance, bool bakeNavMesh)
    {
        var problems = new List<string>();
        if (prefabs == null || prefabs.Count == 0)
            problems.Add("No mob types: add an entry to Prefabs with a mob prefab.");
        else
        {
            for (int i = 0; i < prefabs.Count; i++)
            {
                SpawnableMob m = prefabs[i];
                if (m == null || m.mobPrefab == null)
                    problems.Add($"Prefabs[{i}] has no Mob Prefab.");
                else
                {
                    var agent = m.mobPrefab.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true);
                    if (agent == null)
                        problems.Add($"Prefabs[{i}] ({m.mobPrefab.name}) has no NavMeshAgent: it is placed on the default agent's NavMesh and may not move.");
                    else if (agent.agentTypeID != 0)
                        problems.Add($"Prefabs[{i}] ({m.mobPrefab.name}) uses a non-default NavMesh agent type, but chunk NavMeshes are built for the default one (Humanoid): it will never find a spot.");
                }
            }
        }
        if (maxNumberOfMobs <= 0)
            problems.Add("Max Number Of Mobs is 0: no mob can appear.");
        if (!bakeNavMesh)
            problems.Add("EndlessTerrain's Bake NavMesh is off: mobs can't be placed on a NavMesh (and can't walk).");
        else if (navMeshDistance > 0f && activationDistance > navMeshDistance)
            problems.Add($"Activation Distance ({activationDistance}) is beyond EndlessTerrain's NavMesh Distance ({navMeshDistance}): chunks between the two never get mobs.");
        return problems;
    }

    /// <summary>True when mobs may spawn in <paramref name="biome"/> (Global Forbidden Biomes).</summary>
    public bool IsBiomeAllowed(Biome biome) => biome == null || globalForbiddenBiomes == null || !globalForbiddenBiomes.Contains(biome);

    /// <summary>A type's pack chance with the global modifier (0 when packs are off).</summary>
    public float GetEffectivePackSpawnChance(float baseMobPackChance) => enablePackSpawning ? Mathf.Clamp01(baseMobPackChance * globalPackSpawnChanceModifier) : 0f;

    /// <summary>True when <paramref name="position"/> is in a safe zone.</summary>
    public bool IsInSafeZone(Vector3 position)
    {
        if (!enableSafeZones || safeZoneCenters == null)
            return false;
        float r2 = safeZoneRadius * safeZoneRadius;
        foreach (Vector3 c in safeZoneCenters)
        {
            float dx = c.x - position.x, dz = c.z - position.z;
            if (dx * dx + dz * dz < r2)
                return true;
        }
        return false;
    }

    public string GetDebugInfo()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Mob Settings ===");
        sb.AppendLine($"Types: {(prefabs != null ? prefabs.Count : 0)}");
        sb.AppendLine($"Per chunk: {maxNumberOfMobs} (variation {populationVariation:0.00}), world limit {(maxMobsInWorld > 0 ? maxMobsInWorld.ToString() : "none")}");
        sb.AppendLine($"Active within {activationDistance}, removed beyond {deactivationDistance}, one spawn per {spawnInterval} s");
        sb.AppendLine($"Packs: {enablePackSpawning} ({minPackSize}-{maxPackSize}), respawn after {respawnDelay} s");
        return sb.ToString();
    }

    /// <summary>Sensible defaults (keeps the prefab list and safe zones).</summary>
    public void ResetToDefaults()
    {
        maxNumberOfMobs = 6;
        populationVariation = 0.3f;
        maxMobsInWorld = 60;
        activationDistance = 120f;
        deactivationDistance = 180f;
        spawnInterval = 1.5f;
        respawnDelay = 180f;
        respawnDelayVariation = 0.25f;
        enablePackSpawning = true;
        globalPackSpawnChanceModifier = 1f;
        packSpawnRadius = 12f;
        minPackSize = 2;
        maxPackSize = 4;
        minDistanceBetweenMobs = 5f;
        useBiomeSpawnModifiers = true;
        preferredBiomeMultiplier = 2f;
        nonPreferredBiomeMultiplier = 0.5f;
        globalForbiddenBiomes = new List<Biome>();
        useHeightBasedSpawning = false;
        globalHeightTolerance = 10f;
        maxSpawnAttempts = 12;
        navMeshSampleDistance = 2f;
        maxSlope = 35f;
        objectClearance = 0.5f;
        avoidCameraView = true;
        hiddenSpawnDistance = 70f;
        enableTimeBasedSpawning = false;
        inactiveTimeWeight = 0.15f;
        enablePlayerProximityInfluence = true;
        minDistanceFromPlayer = 30f;
        maxDistanceFromPlayer = 0f;
        enableSafeZones = false;
        safeZoneRadius = 60f;
        shouldWaitToStartSpawning = false;
        retryingSpawnTime = 3f;
        playerTag = "Player";
    }
}
