using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using UnityEngine.AI;

/// <summary>
/// Specialized spawner for mobs (enemies or NPCs) that inherits from SpawnerBase.
/// It handles the spawning of mobs with  natural distribution, biome awareness,
/// pack behavior simulation, and improved spawn validation using Unity's NavMesh system.
/// NOW USES MobSettings FROM EndlessTerrain INSTEAD OF LOCAL DUPLICATE FIELDS
/// </summary>
public class MobSpawner : SpawnerBase<SpawnableMob, SpawnableMob>
{
    // IMPLEMENTATION NOTE: Settings are now retrieved from parent component instead of duplicated here
    private MobSettings settings;

    [Header("Debug Control")]
    [Tooltip("Stop spawning when no valid prefabs instead of infinite retries")]
    [SerializeField] private bool stopWhenNoValidPrefabs = true;

    [Tooltip("Maximum time to retry when no prefabs available (0 = retry forever)")]
    [SerializeField] private float maxRetryTime = 60f;

    // Private tracking variables
    private float noValidPrefabsStartTime = 0f;
    private bool hasLoggedNoValidPrefabs = false;

    /// <summary>
    /// Coroutine responsible for the spawning routine. It continuously attempts to spawn mobs 
    /// with  natural distribution while the chunk is active.
    /// </summary>
    /// <returns>IEnumerator for coroutine.</returns>
    protected override IEnumerator SpawnRoutine()
    {
        // Initial validation - stop immediately if no valid prefabs
        if (stopWhenNoValidPrefabs && GetValidPrefabCount() == 0)
        {
            if (!hasLoggedNoValidPrefabs)
            {
                Debug.LogWarning($"MobSpawner {gameObject.name} stopping - no valid prefabs available");
                hasLoggedNoValidPrefabs = true;
            }
            yield break;
        }

        // Track how long we've been without valid prefabs
        noValidPrefabsStartTime = Time.time;

        // Continue spawning while the chunk is active in the scene
        while (chunkParent != null && chunkParent.gameObject.activeInHierarchy)
        {
            // Validate that we have spawnable mobs
            if (spawnablePrefabs == null || spawnablePrefabs.Count == 0)
            {
                // Check if we should stop retrying
                if (stopWhenNoValidPrefabs)
                {
                    if (!hasLoggedNoValidPrefabs)
                    {
                        Debug.LogWarning($"No valid mob prefabs available for spawning in chunk at {chunkPosition}. Stopping spawner.");
                        hasLoggedNoValidPrefabs = true;
                    }
                    yield break;
                }

                // Check retry timeout
                if (maxRetryTime > 0 && Time.time - noValidPrefabsStartTime > maxRetryTime)
                {
                    Debug.LogWarning($"MobSpawner {gameObject.name} timed out after {maxRetryTime} seconds of no valid prefabs. Stopping.");
                    yield break;
                }

                // Only log once, then wait longer
                if (!hasLoggedNoValidPrefabs)
                {
                    Debug.LogWarning($"No valid mob prefabs available for spawning in chunk at {chunkPosition}");
                    hasLoggedNoValidPrefabs = true;
                }

                yield return new WaitForSeconds(retryingSpawnTime * 5f);
                continue;
            }

            // Reset the flag since we have prefabs now
            hasLoggedNoValidPrefabs = false;

            // Check if we can spawn more mobs globally
            if (totalActiveInstances >= globalMaxInstances)
            {
                yield return new WaitForSeconds(retryingSpawnTime);
                continue;
            }

            // Select a mob to spawn based on weighted chance
            SpawnableMob chosenMob = ChooseWeightedMob();

            // If a valid mob was chosen, attempt to spawn it (potentially as a pack)
            if (chosenMob != null)
            {
                bool spawnedSuccessfully = false;

                // IMPLEMENTED: Use settings values instead of local fields
                bool enablePackSpawning = settings != null ? settings.enablePackSpawning : true;
                float packSpawnChance = 0.3f;

                if (settings != null && chosenMob.isPackAnimal)
                {
                    packSpawnChance = settings.GetEffectivePackSpawnChance(chosenMob.packSpawnChance);
                }

                // Determine if this should be a pack spawn
                if (enablePackSpawning && Random.value < packSpawnChance && CanSpawnPack(chosenMob))
                {
                    spawnedSuccessfully = SpawnMobPack(chosenMob);
                }
                else
                {
                    // Single mob spawn
                    SpawnInstance(chosenMob);
                    spawnedSuccessfully = true;
                }

                if (spawnedSuccessfully)
                {
                    // Determine the waiting time before the next spawn
                    float waitTime = CalculateSpawnWaitTime(chosenMob);
                    yield return new WaitForSeconds(waitTime);
                }
                else
                {
                    yield return new WaitForSeconds(retryingSpawnTime);
                }
            }
            else
            {
                // If no valid mob is selected, retry spawning after a short delay
                yield return new WaitForSeconds(retryingSpawnTime);
            }
        }
    }

    /// <summary>
    /// Gets the number of valid prefabs available for spawning.
    /// </summary>
    /// <returns>Count of valid prefabs.</returns>
    private int GetValidPrefabCount()
    {
        if (spawnablePrefabs == null) return 0;

        int validCount = 0;
        foreach (var prefab in spawnablePrefabs)
        {
            if (prefab != null && prefab.mobPrefab != null && prefab.allowedBiomes != null && prefab.allowedBiomes.Count > 0)
            {
                // IMPLEMENTED: Check global forbidden biomes from settings
                bool hasValidBiome = false;
                if (settings != null)
                {
                    foreach (var biome in prefab.allowedBiomes)
                    {
                        if (settings.IsBiomeAllowed(biome))
                        {
                            hasValidBiome = true;
                            break;
                        }
                    }
                }
                else
                {
                    hasValidBiome = prefab.allowedBiomes.Count > 0;
                }

                if (hasValidBiome) validCount++;
            }
        }
        return validCount;
    }

    /// <summary>
    /// Spawns a pack of mobs near each other for more natural group behavior.
    /// </summary>
    /// <param name="mobType">The type of mob to spawn as a pack.</param>
    /// <returns>True if pack was spawned successfully.</returns>
    private bool SpawnMobPack(SpawnableMob mobType)
    {
        // IMPLEMENTED: Use settings values for pack size
        int minPackSize = settings != null ? settings.minPackSize : 2;
        int maxPackSize = settings != null ? settings.maxPackSize : 4;

        // Use mob-specific pack size if available
        if (mobType.isPackAnimal && mobType.preferredPackSize > 0)
        {
            maxPackSize = mobType.preferredPackSize;
        }

        int packSize = Random.Range(minPackSize, maxPackSize + 1);
        List<Vector3> packPositions = new List<Vector3>();

        // Find a suitable center position for the pack
        Vector3 packCenter = GetRandomSpawnPosition(mobType);
        if (packCenter == Vector3.negativeInfinity)
            return false;

        packPositions.Add(packCenter);

        // Generate additional positions around the pack center
        for (int i = 1; i < packSize; i++)
        {
            Vector3 packPosition = FindPackMemberPosition(packCenter, packPositions, mobType);
            if (packPosition != Vector3.negativeInfinity)
            {
                packPositions.Add(packPosition);
            }
        }

        // Spawn all pack members
        bool anySpawned = false;
        foreach (Vector3 position in packPositions)
        {
            if (mobType.CurrentInstances < mobType.MaxInstances && totalActiveInstances < globalMaxInstances)
            {
                GameObject instance = Instantiate(GetPrefab(mobType), position, Quaternion.identity);
                instance.transform.parent = chunkParent;

                // Track the instance
                activeInstances[mobType].Add(instance);
                totalActiveInstances++;
                mobType.CurrentInstances++;

                // Subscribe to the instance's destruction event
                if (instance.TryGetComponent(out Mob mob))
                {
                    mob.OnMobDestroyed += () => DecrementInstanceCount(instance);
                }

                anySpawned = true;
            }
        }

        return anySpawned;
    }

    /// <summary>
    /// Finds a suitable position for a pack member near the pack center.
    /// </summary>
    /// <param name="packCenter">Center position of the pack.</param>
    /// <param name="existingPositions">Already assigned positions in the pack.</param>
    /// <param name="mobType">Type of mob being spawned.</param>
    /// <returns>A valid position for the pack member, or Vector3.negativeInfinity if none found.</returns>
    private Vector3 FindPackMemberPosition(Vector3 packCenter, List<Vector3> existingPositions, SpawnableMob mobType)
    {
        // IMPLEMENTED: Use settings for max spawn attempts and pack radius
        int maxSpawnAttempts = settings != null ? settings.maxSpawnAttempts : 15;
        float packSpawnRadius = settings != null ? settings.packSpawnRadius : 15f;

        // Use mob-specific pack spread if available
        if (mobType.isPackAnimal && mobType.packSpreadRadius > 0)
        {
            packSpawnRadius = mobType.packSpreadRadius;
        }

        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            // Generate random offset within pack radius
            Vector2 randomOffset = Random.insideUnitCircle * packSpawnRadius;
            Vector3 candidatePosition = packCenter + new Vector3(randomOffset.x, 0, randomOffset.y);

            // Ensure position is within chunk bounds
            if (candidatePosition.x < chunkPosition.x || candidatePosition.x >= chunkPosition.x + chunkSize ||
                candidatePosition.z < chunkPosition.y || candidatePosition.z >= chunkPosition.y + chunkSize)
                continue;

            // Get height at this position
            int localX = Mathf.Clamp((int)(candidatePosition.x - chunkPosition.x), 0, chunkSize - 1);
            int localZ = Mathf.Clamp((int)(candidatePosition.z - chunkPosition.y), 0, chunkSize - 1);
            candidatePosition.y = heightMap[localX, localZ];

            // Check if position is valid and maintains distance from other pack members
            if (IsValidMobPosition(candidatePosition, mobType) &&
                IsValidPackPosition(candidatePosition, existingPositions))
            {
                return candidatePosition;
            }
        }

        return Vector3.negativeInfinity;
    }

    /// <summary>
    /// Checks if a position maintains proper distance from other pack members.
    /// </summary>
    /// <param name="position">Position to check.</param>
    /// <param name="existingPositions">Existing pack member positions.</param>
    /// <returns>True if position is valid for pack member.</returns>
    private bool IsValidPackPosition(Vector3 position, List<Vector3> existingPositions)
    {
        // IMPLEMENTED: Use settings for minimum distance
        float minDistanceBetweenMobs = settings != null ? settings.minDistanceBetweenMobs : 5f;

        foreach (Vector3 existingPos in existingPositions)
        {
            if (Vector3.Distance(position, existingPos) < minDistanceBetweenMobs)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Determines if a pack can be spawned for the given mob type.
    /// </summary>
    /// <param name="mobType">Mob type to check.</param>
    /// <returns>True if pack spawning is possible.</returns>
    private bool CanSpawnPack(SpawnableMob mobType)
    {
        // IMPLEMENTED: Use settings for pack size
        int minPackSize = settings != null ? settings.minPackSize : 2;

        int remainingSlots = mobType.MaxInstances - mobType.CurrentInstances;
        int globalRemainingSlots = globalMaxInstances - totalActiveInstances;
        return remainingSlots >= minPackSize && globalRemainingSlots >= minPackSize;
    }

    /// <summary>
    /// Calculates spawn wait time with biome and environmental modifiers.
    /// </summary>
    /// <param name="mob">The mob that was spawned.</param>
    /// <returns>Time to wait before next spawn attempt.</returns>
    private float CalculateSpawnWaitTime(SpawnableMob mob)
    {
        float baseWaitTime = mob.shouldHaveRandomSpawnTime
            ? Random.Range(mob.minSpawnTime, mob.maxSpawnTime)
            : mob.spawnTime;

        // IMPLEMENTED: Apply biome-based modifiers if enabled in settings
        if (settings != null && settings.useBiomeSpawnModifiers)
        {
            // Sample biome at random position to get general chunk biome character
            int randomX = Random.Range(0, chunkSize);
            int randomY = Random.Range(0, chunkSize);
            Biome currentBiome = biomeMap[randomX, randomY];

            bool isPreferredBiome = mob.allowedBiomes.Contains(currentBiome);
            float biomeModifier = isPreferredBiome ?
                1f / settings.preferredBiomeMultiplier :
                1f / settings.nonPreferredBiomeMultiplier;
            baseWaitTime *= biomeModifier;
        }

        return baseWaitTime;
    }

    /// <summary>
    /// Selects a mob to spawn based on weighted chances with biome awareness.
    /// </summary>
    /// <returns>The chosen mob, or null if no mob can be spawned.</returns>
    private SpawnableMob ChooseWeightedMob()
    {
        float totalWeight = 0f;
        var viableMobs = new List<(SpawnableMob mob, float weight)>();

        // Calculate weights for all viable mobs
        foreach (var mob in spawnablePrefabs)
        {
            if (mob == null || !activeInstances.ContainsKey(mob) || activeInstances[mob] == null)
                continue;

            // Only consider mobs that can still be spawned
            if (mob.CurrentInstances >= mob.MaxInstances)
                continue;

            float mobWeight = CalculateMobWeight(mob);
            if (mobWeight > 0)
            {
                viableMobs.Add((mob, mobWeight));
                totalWeight += mobWeight;
            }
        }

        // No viable mobs
        if (totalWeight <= 0 || viableMobs.Count == 0)
            return null;

        // Select based on weighted probability
        float randomValue = Random.Range(0, totalWeight);
        float currentWeight = 0f;

        foreach (var (mob, weight) in viableMobs)
        {
            currentWeight += weight;
            if (randomValue <= currentWeight)
                return mob;
        }

        // Fallback to first viable mob
        return viableMobs.Count > 0 ? viableMobs[0].mob : null;
    }

    /// <summary>
    /// Calculates the effective spawn weight for a mob considering biome compatibility.
    /// </summary>
    /// <param name="mob">Mob to calculate weight for.</param>
    /// <returns>Effective spawn weight.</returns>
    private float CalculateMobWeight(SpawnableMob mob)
    {
        float baseWeight = mob.spawnWeight;

        // IMPLEMENTED: Use settings for biome spawn modifiers
        if (settings == null || !settings.useBiomeSpawnModifiers)
            return baseWeight;

        // Count how many positions in chunk are suitable for this mob
        int suitablePositions = 0;
        int totalSamples = 25; // Sample 5x5 grid for performance

        for (int i = 0; i < totalSamples; i++)
        {
            int x = (i % 5) * (chunkSize / 5);
            int y = (i / 5) * (chunkSize / 5);
            x = Mathf.Clamp(x, 0, chunkSize - 1);
            y = Mathf.Clamp(y, 0, chunkSize - 1);

            Biome biome = biomeMap[x, y];

            // IMPLEMENTED: Check global forbidden biomes
            if (settings.IsBiomeAllowed(biome) && mob.allowedBiomes.Contains(biome))
            {
                suitablePositions++;
            }
        }

        // Modify weight based on biome suitability
        float biomeCompatibility = (float)suitablePositions / totalSamples;
        return baseWeight * (0.1f + biomeCompatibility * 0.9f);
    }

    /// <summary>
    /// Gets a random spawn position with  natural distribution and validation.
    /// </summary>
    /// <returns>A random spawn position as a Vector3, or Vector3.negativeInfinity if no valid position found.</returns>
    protected override Vector3 GetRandomSpawnPosition(SpawnableMob chosenMob)
    {
        // IMPLEMENTED: Use settings for max spawn attempts
        int maxSpawnAttempts = settings != null ? settings.maxSpawnAttempts : 15;

        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            Vector3 candidatePosition = GenerateCandidatePosition(chosenMob);

            if (IsValidMobPosition(candidatePosition, chosenMob))
            {
                return candidatePosition;
            }
        }

        // If no valid position is found after max attempts
        return Vector3.negativeInfinity;
    }

    /// <summary>
    /// Generates a candidate spawn position with biome and height preferences.
    /// </summary>
    /// <param name="mob">The mob type being spawned.</param>
    /// <returns>A candidate world position.</returns>
    private Vector3 GenerateCandidatePosition(SpawnableMob mob)
    {
        float xOffset, zOffset;

        // IMPLEMENTED: Use settings for edge avoidance
        bool avoidChunkEdges = settings != null ? settings.avoidChunkEdges : true;
        float edgeAvoidanceDistance = settings != null ? settings.edgeAvoidanceDistance : 10f;

        if (avoidChunkEdges)
        {
            // Avoid spawning too close to chunk edges
            float minOffset = edgeAvoidanceDistance;
            float maxOffset = chunkSize - edgeAvoidanceDistance;

            xOffset = Random.Range(minOffset, maxOffset);
            zOffset = Random.Range(minOffset, maxOffset);
        }
        else
        {
            xOffset = Random.Range(0, chunkSize);
            zOffset = Random.Range(0, chunkSize);
        }

        // Clamp to valid bounds
        xOffset = Mathf.Clamp(xOffset, 0, chunkSize - 1);
        zOffset = Mathf.Clamp(zOffset, 0, chunkSize - 1);

        // Get height and biome information
        float height = heightMap[(int)xOffset, (int)zOffset];
        Biome biome = biomeMap[(int)xOffset, (int)zOffset];

        // If this biome is not allowed for the mob, try to find a nearby suitable biome
        if (!mob.allowedBiomes.Contains(biome) || (settings != null && !settings.IsBiomeAllowed(biome)))
        {
            Vector2 suitableOffset = FindNearbyAllowedBiome(new Vector2(xOffset, zOffset), mob, 10f);
            if (suitableOffset != Vector2.negativeInfinity)
            {
                xOffset = suitableOffset.x;
                zOffset = suitableOffset.y;
                height = heightMap[(int)xOffset, (int)zOffset];
            }
        }

        return new Vector3(chunkPosition.x + xOffset, height, chunkPosition.y + zOffset);
    }

    /// <summary>
    /// Finds a nearby position with an allowed biome for the mob.
    /// </summary>
    /// <param name="center">Center position to search from.</param>
    /// <param name="mob">Mob requiring suitable biome.</param>
    /// <param name="searchRadius">Search radius in units.</param>
    /// <returns>Suitable position or Vector2.negativeInfinity if not found.</returns>
    private Vector2 FindNearbyAllowedBiome(Vector2 center, SpawnableMob mob, float searchRadius)
    {
        int attempts = 10;
        for (int i = 0; i < attempts; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * searchRadius;
            Vector2 testPos = center + randomOffset;

            // Clamp to chunk bounds
            testPos.x = Mathf.Clamp(testPos.x, 0, chunkSize - 1);
            testPos.y = Mathf.Clamp(testPos.y, 0, chunkSize - 1);

            Biome testBiome = biomeMap[(int)testPos.x, (int)testPos.y];

            // IMPLEMENTED: Check both mob allowed biomes and global forbidden biomes
            bool biomeAllowed = mob.allowedBiomes.Contains(testBiome);
            if (settings != null)
            {
                biomeAllowed = biomeAllowed && settings.IsBiomeAllowed(testBiome);
            }

            if (biomeAllowed)
            {
                return testPos;
            }
        }

        return Vector2.negativeInfinity;
    }

    /// <summary>
    ///  position validation for mobs with distance, biome, and height checking.
    /// </summary>
    /// <param name="position">Position to validate.</param>
    /// <param name="mob">Mob type being spawned.</param>
    /// <returns>True if position is valid for mob spawning.</returns>
    private bool IsValidMobPosition(Vector3 position, SpawnableMob mob)
    {
        // Basic validation
        if (!IsValidSpawnPosition(position))
            return false;

        // Check biome compatibility
        int localX = Mathf.Clamp((int)(position.x - chunkPosition.x), 0, chunkSize - 1);
        int localZ = Mathf.Clamp((int)(position.z - chunkPosition.y), 0, chunkSize - 1);
        Biome biome = biomeMap[localX, localZ];

        // IMPLEMENTED: Check global forbidden biomes
        if (settings != null && !settings.IsBiomeAllowed(biome))
            return false;

        if (!mob.allowedBiomes.Contains(biome))
            return false;

        // IMPLEMENTED: Height-based validation using settings
        bool useHeightBasedSpawning = settings != null ? settings.useHeightBasedSpawning : true;
        if (useHeightBasedSpawning && !IsValidHeightForMob(position.y, mob, biome))
            return false;

        // Distance check with existing mobs
        if (!IsValidDistanceFromOtherMobs(position))
            return false;

        return true;
    }

    /// <summary>
    /// Checks if the height is suitable for the mob type and biome.
    /// </summary>
    /// <param name="height">Height to check.</param>
    /// <param name="mob">Mob type.</param>
    /// <param name="biome">Current biome.</param>
    /// <returns>True if height is suitable.</returns>
    private bool IsValidHeightForMob(float height, SpawnableMob mob, Biome biome)
    {
        // IMPLEMENTED: Use settings for height tolerance
        float heightTolerance = settings != null ? settings.globalHeightTolerance : 10f;

        // Use biome height range as reference with tolerance
        float minHeight = biome.minHeight - heightTolerance;
        float maxHeight = biome.maxHeight + heightTolerance;

        return height >= minHeight && height <= maxHeight;
    }

    /// <summary>
    /// Checks if position maintains minimum distance from existing mobs.
    /// </summary>
    /// <param name="position">Position to check.</param>
    /// <returns>True if position maintains required distance.</returns>
    private bool IsValidDistanceFromOtherMobs(Vector3 position)
    {
        // IMPLEMENTED: Use settings for minimum distance
        float minDistanceBetweenMobs = settings != null ? settings.minDistanceBetweenMobs : 5f;

        foreach (var mobList in activeInstances.Values)
        {
            foreach (var existingMob in mobList)
            {
                if (existingMob != null)
                {
                    float distance = Vector3.Distance(position, existingMob.transform.position);
                    if (distance < minDistanceBetweenMobs)
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Checks if position is within player proximity restrictions.
    /// </summary>
    /// <param name="position">Position to check.</param>
    /// <returns>True if position is valid (not too close to players).</returns>
    private bool IsValidPlayerDistance(Vector3 position)
    {
        // IMPLEMENTED: Use settings for player proximity
        if (settings == null || !settings.enablePlayerProximityInfluence) return true;

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        if (players.Length == 0) return true;

        foreach (GameObject player in players)
        {
            if (player != null)
            {
                float distance = Vector3.Distance(position, player.transform.position);

                // Too close to player
                if (distance < settings.minDistanceFromPlayer)
                {
                    return false;
                }

                // Too far from player (if max distance is set)
                if (settings.maxDistanceFromPlayer > 0 && distance > settings.maxDistanceFromPlayer)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if the given position is a valid spawn location by using Unity's NavMesh system.
    /// </summary>
    /// <param name="position">The position to check for validity.</param>
    /// <returns>True if the position is valid, otherwise false.</returns>
    protected override bool IsValidSpawnPosition(Vector3 position)
    {
        // Basic bounds checking
        if (position == Vector3.negativeInfinity)
            return false;

        // Check if position is within chunk bounds
        if (position.x < chunkPosition.x || position.x >= chunkPosition.x + chunkSize ||
            position.z < chunkPosition.y || position.z >= chunkPosition.y + chunkSize)
        {
            return false;
        }

        // Check player proximity
        if (!IsValidPlayerDistance(position))
        {
            return false;
        }

        // IMPLEMENTED: Use settings for NavMesh sample distance
        float navMeshSampleDistance = settings != null ? settings.navMeshSampleDistance : 2f;

        // Sample the NavMesh at the given position
        NavMeshHit hit;
        bool isOnNavMesh = NavMesh.SamplePosition(position, out hit, navMeshSampleDistance, NavMesh.AllAreas);

        return isOnNavMesh && Vector3.Distance(position, hit.position) < navMeshSampleDistance;
    }

    /// <summary>
    /// Retrieves the prefab for a given mob, used when spawning the mob instance.
    /// </summary>
    /// <param name="data">The data associated with the mob, used to get the prefab.</param>
    /// <returns>The GameObject prefab for the mob.</returns>
    protected override GameObject GetPrefab(SpawnableMob data)
    {
        return data.mobPrefab;
    }

    /// <summary>
    /// Sets the mob settings for this spawner.
    /// </summary>
    /// <param name="mobSettings">The MobSettings to use.</param>
    public void SetSettings(MobSettings mobSettings)
    {
        settings = mobSettings;
    }

    /// <summary>
    /// Initializes the spawner with  validation and parameter clamping.
    /// </summary>
    /// <param name="chunkPosition">Position of the chunk to be spawned.</param>
    /// <param name="heightMap">Height map for spawn locations.</param>
    /// <param name="chunkSize">Size of the chunk.</param>
    /// <param name="parent">Parent transform for the spawned objects.</param>
    /// <param name="biomeMap">Biome map for biome-aware spawning.</param>
    public override void InitializeSpawner(Vector2 chunkPosition, float[,] heightMap, int chunkSize, Transform parent, Biome[,] biomeMap)
    {
        base.InitializeSpawner(chunkPosition, heightMap, chunkSize, parent, biomeMap);

        // Filter out any null prefabs from the spawnable prefabs list to prevent runtime issues
        spawnablePrefabs = spawnablePrefabs?.FindAll(mob => mob != null && mob.mobPrefab != null && mob.allowedBiomes != null && mob.allowedBiomes.Count > 0) ?? new List<SpawnableMob>();

        // Check if we have any valid prefabs before starting
        int validPrefabCount = GetValidPrefabCount();
        if (validPrefabCount == 0)
        {
            Debug.LogWarning($"MobSpawner at {chunkPosition} has no valid mob prefabs to spawn! Spawning will be disabled.");

            // Don't start the spawning routine if no prefabs
            if (stopWhenNoValidPrefabs)
            {
                enabled = false;
                return;
            }
        }
    }
}