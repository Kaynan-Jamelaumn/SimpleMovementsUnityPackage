using UnityEngine;
/// <summary>
/// Represents an object within a biome, with properties for spawning, clustering, and density.
/// </summary>
[System.Serializable]
public class BiomeObject
{
    [Tooltip("The terrain object to be spawned in the biome.")]
    public GameObject terrainObject;

    [Tooltip("Probability that this object will spawn per terrain cell!.")]
    public float probabilityToSpawn;

    [Tooltip("Does this object have a maximum number of instances allowed in the biome?")]
    public bool hasMaxNumberOfObjects;

    [Tooltip("Current number of this object spawned in the biome.")]
    public int currentNumberOfThisObject;

    [Tooltip("Maximum number of instances allowed for this object.")]
    public int maxNumberOfThisObject;

    [Tooltip("Slope threshold for this object to spawn. Objects may only spawn in areas with slopes below this value.")]
    public float slopeThreshold = 80f;

    [Tooltip("Density map for this object, representing how densely it should be placed within the biome.")]
    public float[,] densityMap;

    [Tooltip("Is this object allowed to cluster together with other objects of the same type?")]
    public bool isClusterable = true;

    [Tooltip("Number of clusters to form if the object is clusterable.")]
    public int clusterCount = 5;

    [Tooltip("Radius of each cluster of this object.")]
    public float clusterRadius = 50f;
}