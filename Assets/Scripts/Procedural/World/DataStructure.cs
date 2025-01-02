
using UnityEngine;
public static class DataStructure
{

    public struct MapData
    {

        public readonly float[,] heightMap;

        public Texture2D splatMap;

        public MapData(float[,] heightMap, Texture2D splatMap = null)
        {
            this.heightMap = heightMap;
            this.splatMap = splatMap;
        }
    }

    public struct TerrainData
    {

        public readonly MeshData meshData;

        public Texture2D splatMap;

        public readonly float[,] heightMap;

        public readonly TerrainGenerator terrainGenerator;

        public Vector2 globalOffset;

        public Biome[,] biomeMap;

        public TerrainData(
            MeshData meshData,
            Texture2D splatMap,
            float[,] heightMap,
            TerrainGenerator terrainGenerator,
            Vector2 globalOffset,
            Biome[,] biomeMap)
        {
            this.meshData = meshData;
            this.splatMap = splatMap;
            this.heightMap = heightMap;
            this.terrainGenerator = terrainGenerator;
            this.globalOffset = globalOffset;
            this.biomeMap = biomeMap;
        }
    }

    public struct BiomeObjectData
    {

        public readonly float[,] heightMap;

        public Vector2 globalOffset;

        public readonly TerrainGenerator terrainGenerator;

        public readonly Biome[,] biomeMap;

        public readonly Transform chunkTransform;

        public BiomeObjectData(
            float[,] heightMap,
            Vector2 globalOffset,
            TerrainGenerator terrainGenerator,
            Biome[,] biomeMap,
            Transform chunkTransform)
        {
            this.heightMap = heightMap;
            this.globalOffset = globalOffset;
            this.terrainGenerator = terrainGenerator;
            this.biomeMap = biomeMap;
            this.chunkTransform = chunkTransform;
        }
    }
}

