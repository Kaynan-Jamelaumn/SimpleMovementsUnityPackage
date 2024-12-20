using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using UnityEngine.AI;

public class MobSpawner : SpawnerBase<SpawnableMob, SpawnableMob>
{
    protected override IEnumerator SpawnRoutine()
    {
        while (chunkParent != null && chunkParent.gameObject.activeInHierarchy)
        {
            SpawnableMob chosenMob = ChooseWeightedMob();

            if (chosenMob != null)
            {
                SpawnInstance(chosenMob);

                float waitTime = chosenMob.shouldHaveRandomSpawnTime
                    ? Random.Range(chosenMob.minSpawnTime, chosenMob.maxSpawnTime)
                    : chosenMob.spawnTime;

                yield return new WaitForSeconds(waitTime);
            }
            else
            {
                // No valid mob to spawn, wait for a short period before retrying
                yield return new WaitForSeconds(retryingSpawnTime);
            }
        }
    }


    private SpawnableMob ChooseWeightedMob()
    {
        float totalWeight = 0f;

        foreach (var mob in spawnablePrefabs)
        {
            if (mob == null || !activeInstances.ContainsKey(mob) || activeInstances[mob] == null)
            continue;

            if (mob.CurrentInstances < mob.MaxInstances)
                totalWeight += mob.spawnWeight;
             
        }

        if (totalWeight <= 0)

            return null;
        

        float randomValue = Random.Range(0, totalWeight);

        foreach (var mob in spawnablePrefabs)
        {
            if (mob == null || !activeInstances.ContainsKey(mob) || activeInstances[mob] == null)
                continue;

            if (mob.CurrentInstances < mob.MaxInstances)
            {
                if (randomValue <= mob.spawnWeight)
                    return mob;
                
                randomValue -= mob.spawnWeight;
            }
        }

        Debug.LogWarning("Failed to select a mob. This should not happen if totalWeight > 0.");
        return null;
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
        NavMeshHit hit;
        return NavMesh.SamplePosition(position, out hit, 1.0f, NavMesh.AllAreas) &&
               Vector3.Distance(position, hit.position) < 1.0f;
    }

    protected override GameObject GetPrefab(SpawnableMob data)
    {
        return data.mobPrefab;
    }
    public override void InitializeSpawner(Vector2 chunkPosition, float[,] heightMap, int chunkSize, Transform parent)
    {
        base.InitializeSpawner(chunkPosition, heightMap, chunkSize, parent);

        // Filter out null prefabs to prevent runtime issues.
        spawnablePrefabs = spawnablePrefabs?.FindAll(mob => mob != null) ?? new List<SpawnableMob>();
    }
}
