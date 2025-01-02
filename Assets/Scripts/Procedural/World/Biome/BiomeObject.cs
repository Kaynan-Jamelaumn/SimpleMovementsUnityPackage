using UnityEngine;

[System.Serializable]
public class BiomeObject
{

    public GameObject terrainObject;

    public float probabilityToSpawn;

    public bool hasMaxNumberOfObjects;

    public int currentNumberOfThisObject;

    public int maxNumberOfThisObject;

    public float slopeThreshold = 80f;

    public float[,] densityMap;

    public bool isClusterable = true;

    public int clusterCount = 5;

    public float clusterRadius = 50f;
}