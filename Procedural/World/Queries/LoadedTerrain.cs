using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Identifies one placed object across unloading and reloading: its chunk and its index in the chunk's placements.</summary>
[Serializable]
public struct PlacedObjectId : IEquatable<PlacedObjectId>
{
    public int ChunkX, ChunkY, Index;

    public PlacedObjectId(int chunkX, int chunkY, int index)
    {
        ChunkX = chunkX;
        ChunkY = chunkY;
        Index = index;
    }

    public bool Equals(PlacedObjectId other) => ChunkX == other.ChunkX && ChunkY == other.ChunkY && Index == other.Index;
    public override bool Equals(object obj) => obj is PlacedObjectId other && Equals(other);
    public override int GetHashCode() => (ChunkX * 73856093) ^ (ChunkY * 19349663) ^ (Index * 83492791);
    public override string ToString() => ChunkX + "," + ChunkY + "#" + Index;
}

/// <summary>
/// The generated data of the terrain chunks currently loaded by <see cref="EndlessTerrain"/> - heights (exactly as
/// the meshes and colliders have them), water, biomes and objects - for queries such as <see cref="FlatSpots"/>.
/// Main thread only. A chunk is here from the moment its data arrives until it is unloaded.
/// </summary>
public static class LoadedTerrain
{
    /// <summary>One loaded chunk's data.</summary>
    public sealed class Chunk
    {
        public Vector2Int Coord;
        /// <summary>World cell (x, z) of the chunk's corner.</summary>
        public int OriginX, OriginZ;
        /// <summary>Cells the chunk covers per side (world units at Scale Factor 1).</summary>
        public int Span;
        /// <summary>Mesh vertex spacing (cells) at the chunk's base level of detail - what its collider has.</summary>
        public int LodFactor = 1;
        public float[,] Heights;
        public WaterMapData Water;
        public Biome[,] Biomes;
        /// <summary>Its objects' placements (null until decided) and the rules they were placed with.</summary>
        public PlacementResult Placements;
        public PlacementPlan Plan;
        /// <summary>Created objects by placement index (null = not created yet, or removed).</summary>
        public GameObject[] Objects;
        /// <summary>Placement indices the game removed (see <see cref="EndlessTerrain.RemovePlacedObject"/>).</summary>
        public Func<int, bool> IsRemoved;
        public Transform Root;
        /// <summary>True once its NavMesh is built.</summary>
        public bool HasNavMesh;

        public bool Contains(float x, float z) => x >= OriginX && z >= OriginZ && x < OriginX + Span && z < OriginZ + Span;

        /// <summary>Height at a whole world cell (clamped to the chunk's data).</summary>
        public float CellHeight(int x, int z)
        {
            int size = Heights.GetLength(0);
            return Heights[Mathf.Clamp(x - OriginX, 0, size - 1), Mathf.Clamp(z - OriginZ, 0, size - 1)];
        }
    }

    private static readonly Dictionary<Vector2Int, Chunk> Chunks = new Dictionary<Vector2Int, Chunk>();

    /// <summary>Cells per chunk side (0 until a chunk is registered).</summary>
    public static int ChunkSpan { get; private set; }

    /// <summary>Every loaded chunk.</summary>
    public static IEnumerable<Chunk> All => Chunks.Values;

    public static int Count => Chunks.Count;

    public static void Register(Chunk chunk)
    {
        if (chunk == null)
            return;
        ChunkSpan = chunk.Span;
        Chunks[chunk.Coord] = chunk;
    }

    public static void Unregister(Vector2Int coord)
    {
        Chunks.Remove(coord);
    }

    public static void Clear()
    {
        Chunks.Clear();
    }

    /// <summary>The loaded chunk covering a world position (x, z), if any.</summary>
    public static bool TryGetChunk(float x, float z, out Chunk chunk)
    {
        chunk = null;
        if (ChunkSpan <= 0)
            return false;
        var coord = new Vector2Int(Mathf.FloorToInt(x / ChunkSpan), Mathf.FloorToInt(z / ChunkSpan));
        return Chunks.TryGetValue(coord, out chunk);
    }

    /// <summary>
    /// Height of the terrain's collider at a world position (x, z): its triangles at the chunk's base level of
    /// detail, exactly as the mesh is built. False where no chunk is loaded.
    /// </summary>
    public static bool TryGetHeight(float x, float z, out float height)
    {
        height = 0f;
        if (!TryGetChunk(x, z, out Chunk chunk))
            return false;
        height = SurfaceHeight(chunk, x, z);
        return true;
    }

    /// <summary>
    /// Terrain normal at a world position averaged over about <paramref name="radius"/> (at least one mesh cell).
    /// False where no chunk is loaded.
    /// </summary>
    public static bool TryGetNormal(float x, float z, float radius, out Vector3 normal)
    {
        normal = Vector3.up;
        if (!TryGetChunk(x, z, out Chunk chunk))
            return false;
        float d = Mathf.Max(radius, Mathf.Max(1f, chunk.LodFactor));
        float dx = Height(x + d, z, chunk) - Height(x - d, z, chunk);
        float dz = Height(x, z + d, chunk) - Height(x, z - d, chunk);
        normal = new Vector3(-dx, 2f * d, -dz).normalized;
        return true;
    }

    /// <summary>The water at a world position (None where dry or not loaded), and its surface height (NaN where dry).</summary>
    public static WaterBodyType WaterAt(float x, float z, out float surface)
    {
        surface = float.NaN;
        if (!TryGetChunk(x, z, out Chunk chunk) || chunk.Water == null)
            return WaterBodyType.None;
        int cx = Mathf.Clamp(Mathf.RoundToInt(x) - chunk.OriginX, 0, chunk.Water.Size - 1);
        int cz = Mathf.Clamp(Mathf.RoundToInt(z) - chunk.OriginZ, 0, chunk.Water.Size - 1);
        WaterBodyType type = chunk.Water.Type[cx, cz];
        if (type != WaterBodyType.None)
            surface = chunk.Water.Surface[cx, cz];
        return type;
    }

    /// <summary>The biome at a world position (null where not loaded).</summary>
    public static Biome BiomeAt(float x, float z)
    {
        if (!TryGetChunk(x, z, out Chunk chunk) || chunk.Biomes == null)
            return null;
        int size = chunk.Biomes.GetLength(0);
        return chunk.Biomes[Mathf.Clamp(Mathf.FloorToInt(x) - chunk.OriginX, 0, size - 1), Mathf.Clamp(Mathf.FloorToInt(z) - chunk.OriginZ, 0, size - 1)];
    }

    /// <summary>
    /// Calls <paramref name="visit"/> with every placed object (created or still waiting to be) whose footprint
    /// centre is within <paramref name="radius"/> of (x, z): its world position and radius. Removed objects are skipped.
    /// </summary>
    public static void ForEachObjectNear(float x, float z, float radius, Action<Vector3, float> visit)
    {
        if (ChunkSpan <= 0)
            return;
        int cx0 = Mathf.FloorToInt((x - radius - 64f) / ChunkSpan), cx1 = Mathf.FloorToInt((x + radius + 64f) / ChunkSpan);
        int cz0 = Mathf.FloorToInt((z - radius - 64f) / ChunkSpan), cz1 = Mathf.FloorToInt((z + radius + 64f) / ChunkSpan);
        for (int cz = cz0; cz <= cz1; cz++)
        {
            for (int cx = cx0; cx <= cx1; cx++)
            {
                if (!Chunks.TryGetValue(new Vector2Int(cx, cz), out Chunk chunk) || chunk.Placements == null)
                    continue;
                List<ObjectPlacement> objects = chunk.Placements.Objects;
                for (int i = 0; i < objects.Count; i++)
                {
                    if (chunk.IsRemoved != null && chunk.IsRemoved(i))
                        continue;
                    Vector3 p = objects[i].Position;
                    float reach = radius + objects[i].Radius;
                    float dx = p.x - x, dz = p.z - z;
                    if (dx * dx + dz * dz <= reach * reach)
                        visit(p, objects[i].Radius);
                }
            }
        }
    }

    /// <summary>Height across chunk borders (the neighbouring chunk's data when the point is outside <paramref name="near"/>).</summary>
    private static float Height(float x, float z, Chunk near)
    {
        if (near.Contains(x, z) || !TryGetChunk(x, z, out Chunk chunk))
            chunk = near;
        return SurfaceHeight(chunk, x, z);
    }

    /// <summary>
    /// A chunk's collider height at a world position: its mesh has vertices every LodFactor cells, each quad split
    /// along its (x+1, z)-(x, z+1) diagonal (see <see cref="MeshGenerator.GenerateTerrainMesh"/>).
    /// </summary>
    public static float SurfaceHeight(Chunk chunk, float x, float z)
    {
        int lod = Mathf.Max(1, chunk.LodFactor);
        int x0 = Mathf.FloorToInt(x / lod) * lod;
        int z0 = Mathf.FloorToInt(z / lod) * lod;
        float u = (x - x0) / lod, v = (z - z0) / lod;
        float h00 = chunk.CellHeight(x0, z0);
        float h10 = chunk.CellHeight(x0 + lod, z0);
        float h01 = chunk.CellHeight(x0, z0 + lod);
        float h11 = chunk.CellHeight(x0 + lod, z0 + lod);
        if (u + v <= 1f)
            return h00 + u * (h10 - h00) + v * (h01 - h00);
        return h11 + (1f - u) * (h01 - h11) + (1f - v) * (h10 - h11);
    }
}
