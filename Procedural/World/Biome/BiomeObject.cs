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

    [Header("Height Preferences")]
    [Tooltip("Should this object use custom height preferences instead of biome defaults?")]
    public bool useCustomHeightPreference = false;

    [Tooltip("Preferred minimum height for this object (in world units).")]
    public float preferredMinHeight = 0f;

    [Tooltip("Optimal height where this object spawns most frequently (in world units).")]
    public float preferredOptimalHeight = 50f;

    [Tooltip("Preferred maximum height for this object (in world units).")]
    public float preferredMaxHeight = 100f;

    [Tooltip("How strict the height preference is. Higher values = more strict preference.")]
    [Range(0.1f, 5f)]
    public float heightPreferenceStrength = 1f;

    [Header("Environmental Preferences")]
    [Tooltip("Preferred distance from biome edges (0 = edge, 1 = center).")]
    [Range(0f, 1f)]
    public float biomeCenterPreference = 0.5f;

    [Tooltip("How much this object avoids steep terrain (multiplier on slope threshold).")]
    [Range(0.1f, 2f)]
    public float slopeAvoidance = 1f;
}