
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Represents an instance of a biome in the game world, including its prefab and the runtime objects within it.
/// </summary>
[System.Serializable]
public class BiomeInstance
{
    [Tooltip("Prefab of the biome.")]
    public Biome BiomePrefab;

    [Tooltip("List of runtime objects present in this biome.")]
    public List<BiomeObject> runtimeObjects = new List<BiomeObject>();

    [Tooltip("Current number of objects spawned in this biome.")]
    public int currentNumberOfObjects;
}