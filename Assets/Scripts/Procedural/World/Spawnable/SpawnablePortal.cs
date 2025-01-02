using UnityEngine;

[System.Serializable]
public class SpawnablePortal : ISpawbleBySpawner
{
    public GameObject prefab;
    public int maxInstances;
    public float spawnTime;
    public float minSpawnTime;
    public float maxSpawnTime;
    public bool shouldHaveRandomSpawnTime;
    [HideInInspector] public int currentInstances;

    public int MaxInstances { get => maxInstances; set => maxInstances = value; }
    public int CurrentInstances { get => currentInstances; set => currentInstances = value; }
}