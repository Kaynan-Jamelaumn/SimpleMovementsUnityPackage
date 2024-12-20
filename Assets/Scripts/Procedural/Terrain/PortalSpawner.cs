using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class PortalSpawner : SpawnerBase<PortalPrefab, PortalPrefab>
{
    protected override IEnumerator SpawnRoutine()
    {
        while (chunkParent != null && chunkParent.gameObject.activeInHierarchy)
        {
            PortalPrefab chosenPortal = spawnablePrefabs[Random.Range(0, spawnablePrefabs.Count)];
            if (chosenPortal != null)
            {
                SpawnInstance(chosenPortal);

                float waitTime = chosenPortal.shouldHaveRandomSpawnTime
                    ? Random.Range(chosenPortal.minSpawnTime, chosenPortal.maxSpawnTime)
                    : chosenPortal.spawnTime;

                yield return new WaitForSeconds(waitTime);
            }
            else
            {
                yield return new WaitForSeconds(retryingSpawnTime);
            }
        }
    }

    protected override Vector3 GetRandomSpawnPosition()
    {
        float xOffset = Random.Range(0, chunkSize);
        float zOffset = Random.Range(0, chunkSize);

        float height = heightMap[(int)xOffset, (int)zOffset];
        return new Vector3(chunkPosition.x + xOffset, height, chunkPosition.y + zOffset);
    }

    protected override bool IsValidSpawnPosition(Vector3 position)
    {
        return true; // Implement your own validation logic for portals
    }

    protected override GameObject GetPrefab(PortalPrefab data)
    {
        return data.prefab;
    }


}