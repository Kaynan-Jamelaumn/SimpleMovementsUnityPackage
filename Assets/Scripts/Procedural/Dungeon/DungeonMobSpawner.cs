using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class DungeonMobSpawner : MonoBehaviour
{
    [System.Serializable]
    public class SpawnableMob
    {
        public GameObject mobPrefab;
        public int maxInstancesOfThisMob;
        public bool HasMaxInstancesOfThisMob;
        public float weightToSpawnFactor;
        public float minTimeToSpawn;
        public float maxTimeToSpawn;
        public float currentMobs;
        public float CalculateSpawnWeight(float weightSpawnFactor)
        {
            // If any of the factors is 0, we return 0
            if (weightToSpawnFactor == 0 || weightSpawnFactor == 0)
                return 0;

            // Calculate the weight by multiplying the spawn factor by the inverse of weightToSpawnFactor
            return weightSpawnFactor / weightToSpawnFactor;
        }

    }

    public enum ESpawnDifficulty
    {
        Easy,
        Normal,
        Medium,
        Difficult,
        Lethal

    }
    [SerializeField] private int currentMobs;
    [SerializeField] private float minMobsToStartSpawned;
    [SerializeField] private int maxMobs;
    [SerializeField] private bool hasMaxMobs;
    [SerializeField] private float weightSpawnFactor;

    [Header("Spawn  Times")]
    [SerializeField] private bool hasRandomSpawnTime;
    [SerializeField] private float spawnTime;
    [SerializeField] private float minTimeToStartSpawning;
    [SerializeField] private float minTimeToSpawn;
    [SerializeField] private float maxTimeToSpawn;
    [Header("Changed Difficulty Parameters")]
    [Tooltip("Exponential Time Reduced Per Difficulty increased")][SerializeField] private int spawnTimeReducedPerDifficulty;
    [Tooltip("Will the maxMobs Increase per difficulty")][SerializeField] private bool willMaxMobsIncrease;
    [Tooltip("How Much will maxMob increase per difficulty")][SerializeField] private int maxMobIncrease;
    [Tooltip("How Much will maxMob increase per difficulty")][SerializeField] private int weightSpawnFactorIncreasePerDifficulty;
    [SerializeField] private ESpawnDifficulty spawnDificulty = ESpawnDifficulty.Normal;
    [Header("Mob List")]
    [SerializeField] public List<SpawnableMob> mobList;

    private static ESpawnDifficulty[] cachedValues;
    [SerializeField] public DungeonGenerator dungeonGenerator;

    private void Awake()
    {
        if (!dungeonGenerator) dungeonGenerator = GetComponent<DungeonGenerator>();
    }


    private void Start()
    {
        cachedValues = (ESpawnDifficulty[])Enum.GetValues(typeof(ESpawnDifficulty));
    //    StartCoroutine(SpawnRoutine());
    }
    //void Spawn()
    //{
    //    if (hasMaxMobs && currentMobs < maxMobs) return;
    //    SpawnableMob chosenMob = ChooseMob();
    //    if (chosenMob != null)
    //    {
    //        Instantiate(chosenMob.mobPrefab, transform.position, Quaternion.identity);
    //        chosenMob.currentMobs++; // Increment the chosen mob counter
    //        currentMobs++;
    //    }
    //}
    void Spawn()
    {
        if (hasMaxMobs && currentMobs >= maxMobs) return;
        SpawnableMob chosenMob = ChooseMob();
        if (chosenMob != null)
        {
            // Find a random room with an available cell
            RoomBehaviour[] rooms = dungeonGenerator.GetComponentsInChildren<RoomBehaviour>();
            List<RoomBehaviour> availableRooms = new List<RoomBehaviour>();

            foreach (var room in rooms)
            {
                if (room.GetAvailableCellForSpawning() != null)
                {
                    availableRooms.Add(room);
                }
            }

            if (availableRooms.Count == 0)
            {
                return; // No room has an available cell
            }

            RoomBehaviour selectedRoom = availableRooms[UnityEngine.Random.Range(0, availableRooms.Count)];
            Vector3? spawnPosition = selectedRoom.GetAvailableCellForSpawning();

            if (spawnPosition.HasValue)
            {
                Instantiate(chosenMob.mobPrefab, spawnPosition.Value, Quaternion.identity);
                chosenMob.currentMobs++; // Increment the chosen mob counter
                currentMobs++;
            }
        }
    }
    SpawnableMob ChooseMob()
    {
        SpawnableMob chosenMob = null;
        float totalWeight = 0;

        // Calculate the total weight of mobs
        foreach (SpawnableMob mob in mobList)
        {
            if (mob.HasMaxInstancesOfThisMob && mob.currentMobs >= mob.maxInstancesOfThisMob)
                continue; // Skip this mob if it has reached its max instances

            totalWeight += mob.CalculateSpawnWeight(weightSpawnFactor);
        }

        // If no mob is available to be chosen, return null
        if (totalWeight == 0)
            return null;

        // Choose a random number within the total weight range
        float randomValue = UnityEngine.Random.Range(0f, totalWeight);

        // Iterate again over the mobs to determine which one corresponds to the random value
        foreach (SpawnableMob mob in mobList)
        {
            if (mob.HasMaxInstancesOfThisMob && mob.currentMobs >= mob.maxInstancesOfThisMob)
                continue; // Skip this mob if it has reached its max instances

            float weight = mob.CalculateSpawnWeight(weightSpawnFactor);

            // If the random value is within this mob's weight range, choose this mob
            if (randomValue <= weight)
            {
                chosenMob = mob;
                break;
            }

            // If not, subtract this mob's weight from the random value and continue to the next mob
            randomValue -= weight;
        }
        //chosenMob.currentMobs++; // Increment the chosen mob counter
        //currentMobs++;
        return chosenMob;
    }


    void ChangeDifficulty(ESpawnDifficulty difficulty)
    {
        int currentDifficulty = GetIndex(spawnDificulty);
        int newDifficulty = GetIndex(difficulty);

        if (currentDifficulty == newDifficulty) return;

        int increase = currentDifficulty - newDifficulty;

        if (willMaxMobsIncrease) maxMobs += (maxMobIncrease * increase);

        maxTimeToSpawn += (spawnTimeReducedPerDifficulty * increase);
        weightSpawnFactor += (weightSpawnFactor * increase);

    }

    public int GetIndex(ESpawnDifficulty difficulty)
    {
        return Array.IndexOf(cachedValues, difficulty);
    }
    public IEnumerator SpawnRoutine()
    {
        if (minMobsToStartSpawned > 0)
        {
            int count = 0;
            while (count < minMobsToStartSpawned)
            {
                Spawn();
                count++;
            }
        }
        yield return new WaitForSeconds(minTimeToStartSpawning);

        while (true)
        {
            if (currentMobs < maxMobs)
                Spawn();

            
            float spawnInterval = GenericMethods.GetRandomValue(spawnTime, hasRandomSpawnTime, minTimeToSpawn, maxTimeToSpawn);
            yield return new WaitForSeconds(spawnInterval);
        }
    }
    public void StartSpawningMobs()
    {
        StartCoroutine(SpawnRoutine()); 
    }
}
