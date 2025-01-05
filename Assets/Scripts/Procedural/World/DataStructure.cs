
using UnityEngine;
using UnityEngine;

/// <summary>
/// A static class that contains various data structures used for terrain and biome management.
/// </summary>
public static class DataStructure
{
    /// <summary>
    /// Represents map data including heightmap and splatmap textures for terrain generation.
    /// </summary>
    public struct MapData
    {
        [Tooltip("Heightmap for the terrain, representing the elevation at each point.")]
        public readonly float[,] heightMap;

        [Tooltip("Splatmap textures used for terrain texturing (optional).")]
        public Texture2D[] splatMap;

        /// <summary>
        /// Constructor for MapData.
        /// </summary>
        /// <param name="heightMap">The heightmap array.</param>
        /// <param name="splatMap">An optional array of splatmaps for terrain texturing.</param>
        public MapData(float[,] heightMap, Texture2D[] splatMap = null)
        {
            this.heightMap = heightMap;
            this.splatMap = splatMap;
        }
    }

    /// <summary>
    /// Represents terrain data, including mesh, heightmap, splatmap, and biome information.
    /// </summary>
    public struct TerrainData
    {
        [Tooltip("Mesh data used to create the terrain mesh.")]
        public readonly MeshData meshData;

        [Tooltip("Splatmap textures used for terrain texturing.")]
        public Texture2D[] splatMap;

        [Tooltip("Heightmap representing terrain elevation.")]
        public readonly float[,] heightMap;

        [Tooltip("Terrain generator responsible for generating the terrain.")]
        public readonly TerrainGenerator terrainGenerator;

        [Tooltip("Global offset for terrain positioning.")]
        public Vector2 globalOffset;

        [Tooltip("Biome map that defines the biome layout across the terrain.")]
        public Biome[,] biomeMap;

        /// <summary>
        /// Constructor for TerrainData.
        /// </summary>
        /// <param name="meshData">Mesh data for terrain.</param>
        /// <param name="splatMap">Array of splatmaps for terrain texturing.</param>
        /// <param name="heightMap">Heightmap for terrain elevation.</param>
        /// <param name="terrainGenerator">The terrain generator used for creating the terrain.</param>
        /// <param name="globalOffset">Global offset for positioning the terrain.</param>
        /// <param name="biomeMap">Biome map representing the biome layout.</param>
        public TerrainData(
            MeshData meshData,
            Texture2D[] splatMap,
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

    /// <summary>
    /// Represents data related to a specific biome object, including its heightmap, biome map, and chunk transform.
    /// </summary>
    public struct BiomeObjectData
    {
        [Tooltip("Heightmap for the biome object, representing the terrain elevation.")]
        public readonly float[,] heightMap;

        [Tooltip("Global offset for the biome object.")]
        public Vector2 globalOffset;

        [Tooltip("The terrain generator used to generate this biome object.")]
        public readonly TerrainGenerator terrainGenerator;

        [Tooltip("Biome map that defines the biome layout for this object.")]
        public readonly Biome[,] biomeMap;

        [Tooltip("Transform of the chunk this biome object belongs to.")]
        public readonly Transform chunkTransform;

        /// <summary>
        /// Constructor for BiomeObjectData.
        /// </summary>
        /// <param name="heightMap">Heightmap for the biome object.</param>
        /// <param name="globalOffset">Global offset for the biome object.</param>
        /// <param name="terrainGenerator">The terrain generator used to create the biome object.</param>
        /// <param name="biomeMap">Biome map representing the biome layout for this object.</param>
        /// <param name="chunkTransform">Transform of the chunk this biome object belongs to.</param>
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
