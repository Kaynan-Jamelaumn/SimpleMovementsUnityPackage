using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using UnityEngine.AI;

/// <summary>
/// Specialized spawner for mobs (enemies or NPCs) that inherits from SpawnerBase.
/// It handles the spawning of mobs with weighted chances, random spawn positions, and validation using Unity's NavMesh system.
/// </summary>
public class MobSpawner : SpawnerBase<SpawnableMob, SpawnableMob>
{
    /// <summary>
    /// Coroutine responsible for the spawning routine. It continuously attempts to spawn mobs as long as the chunk is active.
    /// </summary>
    /// <returns>IEnumerator for coroutine.</returns>
    protected override IEnumerator SpawnRoutine()
    {
        // Continue spawning while the chunk is active in the scene
        while (chunkParent != null && chunkParent.gameObject.activeInHierarchy)
        {
            // Select a mob to spawn based on weighted chance
            SpawnableMob chosenMob = ChooseWeightedMob();

            // If a valid mob was chosen, spawn it
            if (chosenMob != null)
            {
                SpawnInstance(chosenMob);

                // Determine the waiting time before the next spawn, either random or fixed based on mob settings
                float waitTime = chosenMob.shouldHaveRandomSpawnTime
                    ? Random.Range(chosenMob.minSpawnTime, chosenMob.maxSpawnTime)
                    : chosenMob.spawnTime;

                yield return new WaitForSeconds(waitTime); // Wait for the calculated spawn time
            }
            else
            {
                // If no valid mob is selected, retry spawning after a short delay
                yield return new WaitForSeconds(retryingSpawnTime);
            }
        }
    }

    /// <summary>
    /// Selects a mob to spawn based on weighted chances. Mobs with a higher weight have a higher chance of being selected.
    /// </summary>
    /// <returns>The chosen mob, or null if no mob can be spawned.</returns>
    private SpawnableMob ChooseWeightedMob()
    {
        float totalWeight = 0f;

        // Calculate the total weight of all valid mobs (mobs that are not null and have remaining spawn capacity)
        foreach (var mob in spawnablePrefabs)
        {
            if (mob == null || !activeInstances.ContainsKey(mob) || activeInstances[mob] == null)
                continue;

            // Only consider mobs that can still be spawned (i.e., current instances are less than max instances)
            if (mob.CurrentInstances < mob.MaxInstances)
                totalWeight += mob.spawnWeight; // Accumulate spawn weight
        }

        // If the total weight is zero, no valid mob to spawn
        if (totalWeight <= 0)
            return null;

        // Generate a random value to select a mob based on its weight
        float randomValue = Random.Range(0, totalWeight);

        // Select a mob based on the weighted random value
        foreach (var mob in spawnablePrefabs)
        {
            if (mob == null || !activeInstances.ContainsKey(mob) || activeInstances[mob] == null)
                continue;

            if (mob.CurrentInstances < mob.MaxInstances)
            {
                // If the random value is within the mob's weight range, choose this mob
                if (randomValue <= mob.spawnWeight)
                    return mob;

                // Otherwise, subtract the mob's weight from the random value and check the next mob
                randomValue -= mob.spawnWeight;
            }
        }

        // Log a warning if the selection process failed, which should not happen under normal circumstances
        Debug.LogWarning("Failed to select a mob. This should not happen if totalWeight > 0.");
        return null;
    }

    /// <summary>
    /// Gets a random spawn position within the chunk based on the height map.
    /// </summary>
    /// <returns>A random spawn position as a Vector3.</returns>
    protected override Vector3 GetRandomSpawnPosition(SpawnableMob chosenMob)
    {
        int retries = 10; // Number of attempts to find a valid spawn position
        while (retries > 0)
        {
            // Generate random offsets within the chunk size
            float xOffset = Random.Range(0, chunkSize);
            float zOffset = Random.Range(0, chunkSize);

            // Get the height and biome from their respective maps
            float height = heightMap[(int)xOffset, (int)zOffset];
            Biome biome = biomeMap[(int)xOffset, (int)zOffset];

            // Check if the biome is allowed for the chosen mob
            if (chosenMob.allowedBiomes.Contains(biome))
            {
                // Return a valid spawn position
                return new Vector3(chunkPosition.x + xOffset, height, chunkPosition.y + zOffset);
            }

            retries--;
        }

        // If no valid position is found, return a default invalid position
        return Vector3.negativeInfinity;
    }

    /// <summary>
    /// Checks if the given position is a valid spawn location by using Unity's NavMesh system.
    /// </summary>
    /// <param name="position">The position to check for validity.</param>
    /// <returns>True if the position is valid, otherwise false.</returns>
    protected override bool IsValidSpawnPosition(Vector3 position)
    {
        // Sample the NavMesh at the given position to ensure it's a valid navigable location
        NavMeshHit hit;
        return NavMesh.SamplePosition(position, out hit, 1.0f, NavMesh.AllAreas) &&
               Vector3.Distance(position, hit.position) < 1.0f; // Check if the position is close enough to the NavMesh surface
    }

    /// <summary>
    /// Retrieves the prefab for a given mob, used when spawning the mob instance.
    /// </summary>
    /// <param name="data">The data associated with the mob, used to get the prefab.</param>
    /// <returns>The GameObject prefab for the mob.</returns>
    protected override GameObject GetPrefab(SpawnableMob data)
    {
        return data.mobPrefab; // Return the prefab associated with the mob data
    }

    /// <summary>
    /// Initializes the spawner by calling the base method and filtering out any null mobs from the list of spawnable prefabs.
    /// </summary>
    /// <param name="chunkPosition">Position of the chunk to be spawned.</param>
    /// <param name="heightMap">Height map for spawn locations.</param>
    /// <param name="chunkSize">Size of the chunk.</param>
    /// <param name="parent">Parent transform for the spawned objects.</param>
    public override void InitializeSpawner(Vector2 chunkPosition, float[,] heightMap, int chunkSize, Transform parent, Biome[,] biomeMap)
    {
        base.InitializeSpawner(chunkPosition, heightMap, chunkSize, parent, biomeMap);

        // Filter out any null prefabs from the spawnable prefabs list to prevent runtime issues
        spawnablePrefabs = spawnablePrefabs?.FindAll(mob => mob != null) ?? new List<SpawnableMob>();
    }
}
