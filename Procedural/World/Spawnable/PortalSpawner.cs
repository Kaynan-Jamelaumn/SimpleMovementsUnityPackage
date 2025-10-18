using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Specialized spawner for portals that inherits from SpawnerBase.
/// It handles the spawning of portal instances with improved natural distribution,
/// biome awareness, and configurable spawn parameters.
/// NOW USES PortalSettings FROM EndlessTerrain INSTEAD OF LOCAL DUPLICATE FIELDS
/// </summary>
public class PortalSpawner : SpawnerBase<SpawnablePortal, SpawnablePortal>
{
    // IMPLEMENTATION NOTE: Settings are now retrieved from parent component instead of duplicated here
    private PortalSettings settings;

    [Header("Debug Control")]
    [Tooltip("Stop spawning when no valid prefabs instead of infinite retries")]
    [SerializeField] private bool stopWhenNoValidPrefabs = true;

    [Tooltip("Maximum time to retry when no prefabs available (0 = retry forever)")]
    [SerializeField] private float maxRetryTime = 60f;

    // Private tracking variables
    private float noValidPrefabsStartTime = 0f;
    private bool hasLoggedNoValidPrefabs = false;

    /// <summary>
    /// Coroutine responsible for the portal spawning routine. It continuously attempts to spawn portals
    /// while the chunk is active in the scene and within the set conditions.
    /// </summary>
    /// <returns>IEnumerator for coroutine.</returns>
    protected override IEnumerator SpawnRoutine()
    {
        // Initial validation - stop immediately if no valid prefabs
        if (stopWhenNoValidPrefabs && GetValidPrefabCount() == 0)
        {
            if (!hasLoggedNoValidPrefabs)
            {
                Debug.LogWarning($"PortalSpawner {gameObject.name} stopping - no valid prefabs available");
                hasLoggedNoValidPrefabs = true;
            }
            yield break;
        }

        // Track how long we've been without valid prefabs
        noValidPrefabsStartTime = Time.time;

        // Continue spawning while the chunk is active in the scene
        while (chunkParent != null && chunkParent.gameObject.activeInHierarchy)
        {
            // Validate that we have spawnable portals
            if (spawnablePrefabs == null || spawnablePrefabs.Count == 0)
            {
                // Check if we should stop retrying
                if (stopWhenNoValidPrefabs)
                {
                    if (!hasLoggedNoValidPrefabs)
                    {
                        Debug.LogWarning($"No valid portal prefabs available for spawning in chunk at {chunkPosition}. Stopping spawner.");
                        hasLoggedNoValidPrefabs = true;
                    }
                    yield break;
                }

                // Check retry timeout
                if (maxRetryTime > 0 && Time.time - noValidPrefabsStartTime > maxRetryTime)
                {
                    Debug.LogWarning($"PortalSpawner {gameObject.name} timed out after {maxRetryTime} seconds of no valid prefabs. Stopping.");
                    yield break;
                }

                // Only log once, then wait longer
                if (!hasLoggedNoValidPrefabs)
                {
                    Debug.LogWarning($"No valid portal prefabs available for spawning in chunk at {chunkPosition}");
                    hasLoggedNoValidPrefabs = true;
                }

                yield return new WaitForSeconds(retryingSpawnTime * 5f);
                continue;
            }

            // Reset the flag since we have prefabs now
            hasLoggedNoValidPrefabs = false;

            // Check if we can spawn more portals globally
            if (totalActiveInstances >= globalMaxInstances)
            {
                yield return new WaitForSeconds(retryingSpawnTime);
                continue;
            }

            // IMPLEMENTED: Use settings for weighted selection
            bool useWeightedSelection = settings != null ? settings.useWeightedSelection : false;

            // Select a portal based on the configured selection method
            SpawnablePortal chosenPortal = useWeightedSelection ?
                ChooseWeightedPortal() :
                ChooseRandomPortal();

            // If a valid portal is chosen and it can be spawned, spawn it
            if (chosenPortal != null && CanSpawnPortal(chosenPortal))
            {
                SpawnInstance(chosenPortal);

                // Determine the waiting time before the next spawn based on the portal's settings
                float waitTime = chosenPortal.shouldHaveRandomSpawnTime
                    ? Random.Range(chosenPortal.minSpawnTime, chosenPortal.maxSpawnTime)
                    : chosenPortal.spawnTime;

                yield return new WaitForSeconds(waitTime);
            }
            else
            {
                // If no portal is chosen, retry spawning after a short delay
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
            if (prefab != null && prefab.prefab != null)
            {
                // IMPLEMENTED: Check global forbidden biomes if settings available
                if (settings != null && settings.useBiomeRestrictions)
                {
                    // Check if portal has preferred biomes that aren't all forbidden
                    if (prefab.preferredBiomes != null && prefab.preferredBiomes.Length > 0)
                    {
                        bool hasValidBiome = false;
                        foreach (var biome in prefab.preferredBiomes)
                        {
                            if (settings.IsBiomeAllowed(biome))
                            {
                                hasValidBiome = true;
                                break;
                            }
                        }
                        if (hasValidBiome) validCount++;
                    }
                    else
                    {
                        // No preferred biomes means can spawn anywhere not forbidden
                        validCount++;
                    }
                }
                else
                {
                    validCount++;
                }
            }
        }
        return validCount;
    }

    /// <summary>
    /// Chooses a random portal from the available spawnable portals.
    /// </summary>
    /// <returns>A randomly selected portal, or null if none available.</returns>
    private SpawnablePortal ChooseRandomPortal()
    {
        var availablePortals = GetAvailablePortals();
        if (availablePortals.Count == 0) return null;

        return availablePortals[Random.Range(0, availablePortals.Count)];
    }

    /// <summary>
    /// Chooses a portal based on weighted selection for more natural distribution.
    /// </summary>
    /// <returns>A weighted-selected portal, or null if none available.</returns>
    private SpawnablePortal ChooseWeightedPortal()
    {
        var availablePortals = GetAvailablePortals();
        if (availablePortals.Count == 0) return null;

        // IMPLEMENTED: Use rarity-based weight system with rarityFavorBias from settings
        float rarityFavorBias = settings != null ? settings.rarityFavorBias : 1f;

        float totalWeight = 0f;
        var portalWeights = new List<float>();

        foreach (var portal in availablePortals)
        {
            // Calculate weight based on spawn time (longer = rarer = lower weight) and rarity level
            float baseWeight = portal.shouldHaveRandomSpawnTime ?
                1f / Mathf.Max(0.1f, (portal.minSpawnTime + portal.maxSpawnTime) * 0.5f) :
                1f / Mathf.Max(0.1f, portal.spawnTime);

            // Apply rarity modifier with bias
            float rarityModifier = Mathf.Pow((11f - portal.rarityLevel) / 10f, rarityFavorBias);
            float weight = baseWeight * rarityModifier;

            portalWeights.Add(weight);
            totalWeight += weight;
        }

        if (totalWeight <= 0) return availablePortals[0];

        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        for (int i = 0; i < availablePortals.Count; i++)
        {
            currentWeight += portalWeights[i];
            if (randomValue <= currentWeight)
                return availablePortals[i];
        }

        return availablePortals[0];
    }

    /// <summary>
    /// Gets a list of portals that can currently be spawned.
    /// </summary>
    /// <returns>List of available portals for spawning.</returns>
    private List<SpawnablePortal> GetAvailablePortals()
    {
        var available = new List<SpawnablePortal>();

        foreach (var portal in spawnablePrefabs)
        {
            if (portal != null && CanSpawnPortal(portal))
            {
                // IMPLEMENTED: Check if portal's biomes are allowed
                if (settings != null && settings.useBiomeRestrictions)
                {
                    if (portal.preferredBiomes != null && portal.preferredBiomes.Length > 0)
                    {
                        bool hasValidBiome = false;
                        foreach (var biome in portal.preferredBiomes)
                        {
                            if (settings.IsBiomeAllowed(biome))
                            {
                                hasValidBiome = true;
                                break;
                            }
                        }
                        if (!hasValidBiome) continue;
                    }
                }

                available.Add(portal);
            }
        }

        return available;
    }

    /// <summary>
    /// Checks if a specific portal can be spawned based on instance limits.
    /// </summary>
    /// <param name="portal">The portal to check.</param>
    /// <returns>True if the portal can be spawned.</returns>
    private bool CanSpawnPortal(SpawnablePortal portal)
    {
        return portal.CurrentInstances < portal.MaxInstances;
    }

    /// <summary>
    /// Gets a random spawn position within the chunk with improved natural distribution.
    /// Includes distance checking, height validation, and edge avoidance.
    /// </summary>
    /// <returns>A random spawn position as a Vector3, or Vector3.negativeInfinity if no valid position found.</returns>
    protected override Vector3 GetRandomSpawnPosition(SpawnablePortal data)
    {
        // IMPLEMENTED: Use settings for max spawn attempts
        int maxSpawnAttempts = settings != null ? settings.maxSpawnAttempts : 10;

        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            Vector3 candidatePosition = GenerateCandidatePosition();

            if (IsValidPortalPosition(candidatePosition))
            {
                return candidatePosition;
            }
        }

        // If we couldn't find a valid position after max attempts
        return Vector3.negativeInfinity;
    }

    /// <summary>
    /// Generates a candidate spawn position with optional center preference.
    /// </summary>
    /// <returns>A candidate world position.</returns>
    private Vector3 GenerateCandidatePosition()
    {
        float xOffset, zOffset;

        // IMPLEMENTED: Use settings for center spawning preference
        bool preferCenterSpawning = settings != null ? settings.preferCenterSpawning : true;
        float edgeAvoidanceDistance = settings != null ? settings.edgeAvoidanceDistance : 15f;

        if (preferCenterSpawning)
        {
            // Generate positions biased toward the center, avoiding edges
            float centerBias = 0.7f; // How much to bias toward center (0.5 = no bias, 1.0 = always center)

            xOffset = Mathf.Lerp(
                Random.Range(edgeAvoidanceDistance, chunkSize - edgeAvoidanceDistance),
                chunkSize * 0.5f,
                Random.Range(0f, centerBias)
            );

            zOffset = Mathf.Lerp(
                Random.Range(edgeAvoidanceDistance, chunkSize - edgeAvoidanceDistance),
                chunkSize * 0.5f,
                Random.Range(0f, centerBias)
            );
        }
        else
        {
            // Standard random distribution across entire chunk
            xOffset = Random.Range(0, chunkSize);
            zOffset = Random.Range(0, chunkSize);
        }

        // Ensure we don't go out of bounds
        xOffset = Mathf.Clamp(xOffset, 0, chunkSize - 1);
        zOffset = Mathf.Clamp(zOffset, 0, chunkSize - 1);

        // Get the height from the height map
        float height = heightMap[(int)xOffset, (int)zOffset];

        return new Vector3(chunkPosition.x + xOffset, height, chunkPosition.y + zOffset);
    }

    /// <summary>
    /// Validates if a position is suitable for portal spawning with  checks.
    /// </summary>
    /// <param name="position">The position to validate.</param>
    /// <returns>True if the position is valid for portal spawning.</returns>
    private bool IsValidPortalPosition(Vector3 position)
    {
        // Basic validation
        if (!IsValidSpawnPosition(position))
            return false;

        // IMPLEMENTED: Use settings for height restrictions
        bool useHeightRestrictions = settings != null ? settings.useHeightRestrictions : true;
        float minSpawnHeight = settings != null ? settings.minSpawnHeight : 0f;
        float maxSpawnHeight = settings != null ? settings.maxSpawnHeight : 100f;

        // Height restrictions
        if (useHeightRestrictions && (position.y < minSpawnHeight || position.y > maxSpawnHeight))
            return false;

        // Distance check with existing portals
        if (!IsValidDistance(position))
            return false;

        // IMPLEMENTED: Check biome restrictions if enabled
        if (settings != null && settings.useBiomeRestrictions)
        {
            int localX = Mathf.Clamp((int)(position.x - chunkPosition.x), 0, chunkSize - 1);
            int localZ = Mathf.Clamp((int)(position.z - chunkPosition.y), 0, chunkSize - 1);
            Biome biome = biomeMap[localX, localZ];

            if (!settings.IsBiomeAllowed(biome))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if the position maintains minimum distance from existing portals.
    /// </summary>
    /// <param name="position">Position to check.</param>
    /// <returns>True if position maintains required distance.</returns>
    private bool IsValidDistance(Vector3 position)
    {
        // IMPLEMENTED: Use settings for minimum distance
        float minDistanceBetweenPortals = settings != null ? settings.minDistanceBetweenPortals : 25f;

        foreach (var portalList in activeInstances.Values)
        {
            foreach (var existingPortal in portalList)
            {
                if (existingPortal != null)
                {
                    float distance = Vector3.Distance(position, existingPortal.transform.position);
                    if (distance < minDistanceBetweenPortals)
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
    /// Checks if the given position is a valid spawn location.
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

        // Portals generally don't need complex terrain validation like mobs
        // They can spawn on most surfaces as they're typically magical/dimensional
        return true;
    }

    /// <summary>
    /// Retrieves the prefab for the portal, used when spawning a portal instance.
    /// </summary>
    /// <param name="data">The portal data used to retrieve the associated prefab.</param>
    /// <returns>The GameObject prefab for the portal.</returns>
    protected override GameObject GetPrefab(SpawnablePortal data)
    {
        return data.prefab;
    }

    /// <summary>
    /// Sets the portal settings for this spawner.
    /// </summary>
    /// <param name="portalSettings">The PortalSettings to use.</param>
    public void SetSettings(PortalSettings portalSettings)
    {
        settings = portalSettings;
    }

    /// <summary>
    /// Initializes the portal spawner with validation and setup.
    /// </summary>
    /// <param name="chunkPosition">Position of the chunk to be spawned.</param>
    /// <param name="heightMap">Height map for spawn locations.</param>
    /// <param name="chunkSize">Size of the chunk.</param>
    /// <param name="parent">Parent transform for the spawned objects.</param>
    /// <param name="biomeMap">Biome map for the chunk.</param>
    public override void InitializeSpawner(Vector2 chunkPosition, float[,] heightMap, int chunkSize, Transform parent, Biome[,] biomeMap)
    {
        base.InitializeSpawner(chunkPosition, heightMap, chunkSize, parent, biomeMap);

        // Filter out any null prefabs from the spawnable prefabs list to prevent runtime issues
        spawnablePrefabs = spawnablePrefabs?.FindAll(portal => portal != null && portal.prefab != null) ?? new List<SpawnablePortal>();

        // Check if we have any valid prefabs before starting
        int validPrefabCount = GetValidPrefabCount();
        if (validPrefabCount == 0)
        {
            Debug.LogWarning($"PortalSpawner at {chunkPosition} has no valid portal prefabs to spawn! Spawning will be disabled.");

            // Don't start the spawning routine if no prefabs
            if (stopWhenNoValidPrefabs)
            {
                enabled = false;
                return;
            }
        }
    }
}