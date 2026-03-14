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

        [Tooltip("Debug-only: how much erosion changed each cell (positive = eroded away, negative = deposited). Null unless the erosion debug visualization is enabled.")]
        public readonly float[,] erosionDeltaMap;

        [Tooltip("Per-cell water (surface height, water body type, shoreline level). Null when EnableWater is off.")]
        public readonly WaterMapData waterData;

        /// <summary>
        /// Constructor for MapData.
        /// </summary>
        /// <param name="heightMap">The heightmap array.</param>
        /// <param name="splatMap">An optional array of splatmaps for terrain texturing.</param>
        /// <param name="erosionDeltaMap">Optional per-cell erosion debug data - see <see cref="erosionDeltaMap"/>.</param>
        /// <param name="waterData">Optional per-cell water data - see <see cref="waterData"/>.</param>
        public MapData(float[,] heightMap, Texture2D[] splatMap = null, float[,] erosionDeltaMap = null, WaterMapData waterData = null)
        {
            this.heightMap = heightMap;
            this.splatMap = splatMap;
            this.erosionDeltaMap = erosionDeltaMap;
            this.waterData = waterData;
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

        [Tooltip("Debug-only: how much erosion changed each cell (positive = eroded away, negative = deposited). Null unless the erosion debug visualization is enabled.")]
        public readonly float[,] erosionDeltaMap;

        [Tooltip("Per-cell water (surface height, water body type, shoreline level). Null when EnableWater is off.")]
        public readonly WaterMapData waterData;

        /// <summary>
        /// Constructor for TerrainData.
        /// </summary>
        /// <param name="meshData">Mesh data for terrain.</param>
        /// <param name="splatMap">Array of splatmaps for terrain texturing.</param>
        /// <param name="heightMap">Heightmap for terrain elevation.</param>
        /// <param name="terrainGenerator">The terrain generator used for creating the terrain.</param>
        /// <param name="globalOffset">Global offset for positioning the terrain.</param>
        /// <param name="biomeMap">Biome map representing the biome layout.</param>
        /// <param name="erosionDeltaMap">Optional per-cell erosion debug data - see <see cref="erosionDeltaMap"/>.</param>
        /// <param name="waterData">Optional per-cell water data - see <see cref="waterData"/>.</param>
        public TerrainData(
            MeshData meshData,
            Texture2D[] splatMap,
            float[,] heightMap,
            TerrainGenerator terrainGenerator,
            Vector2 globalOffset,
            Biome[,] biomeMap,
            float[,] erosionDeltaMap = null,
            WaterMapData waterData = null)
        {
            this.meshData = meshData;
            this.splatMap = splatMap;
            this.heightMap = heightMap;
            this.terrainGenerator = terrainGenerator;
            this.globalOffset = globalOffset;
            this.biomeMap = biomeMap;
            this.erosionDeltaMap = erosionDeltaMap;
            this.waterData = waterData;
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
        public readonly MeshData meshData;

        [Tooltip("Per-cell water (surface height, water body type, shoreline level). Null when EnableWater is off. Used to skip spawning land objects underwater.")]
        public readonly WaterMapData waterData;

        /// <summary>
        /// Constructor for BiomeObjectData.
        /// </summary>
        /// <param name="heightMap">Heightmap for the biome object.</param>
        /// <param name="globalOffset">Global offset for the biome object.</param>
        /// <param name="terrainGenerator">The terrain generator used to create the biome object.</param>
        /// <param name="biomeMap">Biome map representing the biome layout for this object.</param>
        /// <param name="chunkTransform">Transform of the chunk this biome object belongs to.</param>
        /// <param name="waterData">Optional per-cell water data - see <see cref="waterData"/>.</param>
        public BiomeObjectData(
            float[,] heightMap,
            Vector2 globalOffset,
            TerrainGenerator terrainGenerator,
            Biome[,] biomeMap,
            Transform chunkTransform,
            MeshData meshData,
            WaterMapData waterData = null)
        {
            this.heightMap = heightMap;
            this.globalOffset = globalOffset;
            this.terrainGenerator = terrainGenerator;
            this.biomeMap = biomeMap;
            this.chunkTransform = chunkTransform;
            this.meshData = meshData;
            this.waterData = waterData;
        }
    }
}