
using System.Collections.Generic;


[System.Serializable]
public class BiomeInstance
{
    public Biome BiomePrefab;
    public List<BiomeObject> runtimeObjects = new List<BiomeObject>();
    public int currentNumberOfObjects;

}