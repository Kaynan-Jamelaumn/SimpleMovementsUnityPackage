using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using System.Threading.Tasks;


public class WorldMobSpawner : MonoBehaviour
{
    private float[,] heightMap;
    private Biome[,] biomeMap;
    private Vector2 globalOffset;
    private int chunkSize;
    private NavMeshSurface navMeshSurface;


    public void Initialize(int chunkSize, Vector2 globalOffset, Transform parent, NavMeshSurface navMeshSurface)
    {
        this.chunkSize = chunkSize;
        this.globalOffset = globalOffset;
        this.navMeshSurface = navMeshSurface;
    }

    public void SetBiomes(Biome[,] biomes)
    {
        this.biomeMap = biomes;
    }

    public async void StartSpawningMobs(float[,] heightMap)
    {
  

        this.heightMap = heightMap;

        // Await the GameObject being active
        await WaitUntilActive();


        // Now start the coroutine
        StartCoroutine(SpawnRoutine());
    }

    private async Task WaitUntilActive()
    {
        // Wait until the GameObject this script is attached to is active
        while (!gameObject.activeInHierarchy)
        {
            await Task.Yield();  // Non-blocking wait until the next frame
        }
    }


    private IEnumerator SpawnRoutine()
    {
        while (true)
        {

            Spawn();
            yield return new WaitForSeconds(Random.Range(5f, 15f));  // Spawn time randomizer
        }
    }

    private void Spawn()
    {
        Vector2 randomPosInChunk = new Vector2(Random.Range(0, chunkSize), Random.Range(0, chunkSize));
        Biome currentBiome = biomeMap[(int)randomPosInChunk.x, (int)randomPosInChunk.y];  // Get biome at the random position

        // Select a random mob from the biome's mob list
        SpawnableMob chosenMob = ChooseMobFromBiome(currentBiome);

        if (chosenMob != null)
        {
            Vector3 spawnPosition = GetPositionAtTerrainHeight((int)randomPosInChunk.x, (int)randomPosInChunk.y);
            // Verify if the position is on a NavMesh
            if (IsValidSpawnPosition(spawnPosition))
            {

                Instantiate(chosenMob.mobPrefab, spawnPosition, Quaternion.identity);
            }
        }
    }

    private SpawnableMob ChooseMobFromBiome(Biome biome)
    {
        float totalWeight = 0;
        foreach (SpawnableMob mob in biome.mobList)
        {
            totalWeight += mob.CalculateSpawnWeight(1f);
        }

        if (totalWeight == 0) return null;

        float randomValue = Random.Range(0f, totalWeight);
        foreach (SpawnableMob mob in biome.mobList)
        {
            if (randomValue <= mob.CalculateSpawnWeight(1f))
            {
                return mob;
            }
            randomValue -= mob.CalculateSpawnWeight(1f);
        }

        return null;
    }

    private Vector3 GetPositionAtTerrainHeight(int x, int z)
    {
        // Get the height from the heightMap at the given x and z position
        float height = heightMap[x, z];

        // Convert the local position to world position using the global offset
        Vector3 spawnPosition = new Vector3(globalOffset.x + x, height, globalOffset.y + z);
        return spawnPosition;
    }

    private bool IsValidSpawnPosition(Vector3 spawnPosition)
    {
        // Check if the spawn position is on the NavMesh
        NavMeshHit hit;
        if (NavMesh.SamplePosition(spawnPosition, out hit, 1.0f, NavMesh.AllAreas))
        {
            // Ensure that the distance between the spawn position and the NavMesh hit point is minimal
            if (Vector3.Distance(spawnPosition, hit.position) < 1.0f)
            {
                return true;  // The position is valid for spawning
            }
        }

        return false;  // The position is not valid for spawning
    }
}
