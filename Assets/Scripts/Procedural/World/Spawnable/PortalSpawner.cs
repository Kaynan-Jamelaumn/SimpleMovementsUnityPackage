using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Specialized spawner for portals that inherits from SpawnerBase.
/// It handles the spawning of portal instances with random spawn times and positions.
/// </summary>
public class PortalSpawner : SpawnerBase<SpawnablePortal, SpawnablePortal>
{
    /// <summary>
    /// Coroutine responsible for the portal spawning routine. It continuously attempts to spawn portals
    /// while the chunk is active in the scene and within the set conditions.
    /// </summary>
    /// <returns>IEnumerator for coroutine.</returns>
    protected override IEnumerator SpawnRoutine()
    {
        // Continue spawning while the chunk is active in the scene
        while (chunkParent != null && chunkParent.gameObject.activeInHierarchy)
        {
            // Select a random portal from the list of spawnable portals
            SpawnablePortal chosenPortal = spawnablePrefabs[Random.Range(0, spawnablePrefabs.Count)];

            // If a valid portal is chosen, spawn it
            if (chosenPortal != null)
            {
                SpawnInstance(chosenPortal);

                // Determine the waiting time before the next spawn based on the portal's settings
                float waitTime = chosenPortal.shouldHaveRandomSpawnTime
                    ? Random.Range(chosenPortal.minSpawnTime, chosenPortal.maxSpawnTime) // Randomize the wait time if enabled
                    : chosenPortal.spawnTime; // Use fixed spawn time otherwise

                yield return new WaitForSeconds(waitTime); // Wait for the calculated time before trying to spawn again
            }
            else
            {
                // If no portal is chosen, retry spawning after a short delay
                yield return new WaitForSeconds(retryingSpawnTime);
            }
        }
    }

    /// <summary>
    /// Gets a random spawn position within the chunk based on the chunk's size and height map.
    /// </summary>
    /// <returns>A random spawn position as a Vector3.</returns>
    protected override Vector3 GetRandomSpawnPosition(SpawnablePortal data)
    {
        // Generate random offsets within the chunk size
        float xOffset = Random.Range(0, chunkSize);
        float zOffset = Random.Range(0, chunkSize);

        // Get the height from the height map at the generated offsets
        float height = heightMap[(int)xOffset, (int)zOffset];

        // Return the spawn position adjusted by the chunk's position and the height
        return new Vector3(chunkPosition.x + xOffset, height, chunkPosition.y + zOffset);
    }

    /// <summary>
    /// Checks if the given position is a valid spawn location.
    /// Since no additional validation is required for portals in this example, it always returns true.
    /// </summary>
    /// <param name="position">The position to check for validity.</param>
    /// <returns>True, indicating that the spawn position is valid.</returns>
    protected override bool IsValidSpawnPosition(Vector3 position)
    {
        // No additional validation required for portals in this context
        return true;
    }

    /// <summary>
    /// Retrieves the prefab for the portal, used when spawning a portal instance.
    /// </summary>
    /// <param name="data">The portal data used to retrieve the associated prefab.</param>
    /// <returns>The GameObject prefab for the portal.</returns>
    protected override GameObject GetPrefab(SpawnablePortal data)
    {
        return data.prefab; // Return the portal prefab associated with the data
    }
}
