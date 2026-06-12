using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What object placement can ask about the ground around one chunk: the height of the actual terrain mesh
/// (matching its triangles at the chunk's level of detail, so objects sit on what is rendered and collided
/// with), slope, the local shape of the land, water, distances to features, climate and biome. Built per
/// chunk on the worker thread from the chunk's generated data.
/// </summary>
public sealed class PlacementEnvironment
{
    public readonly PlacementPlan Plan;
    public readonly ObjectPlacementEngine.ChunkInput Chunk;
    private readonly PlacementFields fields;
    private readonly float[,] heightMap;
    private readonly int mapSize;
    private readonly int lod;

    // Summed-area tables of height and height squared over the fields' area, built on first use.
    private double[] sum, sumSquares;
    private readonly List<KeyValuePair<Biome, float>> gaps = new List<KeyValuePair<Biome, float>>(8);

    public PlacementEnvironment(PlacementPlan plan, ObjectPlacementEngine.ChunkInput chunk)
    {
        Plan = plan;
        Chunk = chunk;
        fields = chunk.Fields;
        heightMap = chunk.HeightMap;
        mapSize = heightMap != null ? heightMap.GetLength(0) : 0;
        lod = Mathf.Max(1, chunk.LodFactor);
    }

    public bool HasFields => fields != null;

    /// <summary>Terrain height at a whole world cell (from the fields around the chunk, else the chunk's own height map, clamped at its edges).</summary>
    public float CellHeight(int worldX, int worldY)
    {
        if (fields != null)
        {
            int x = Mathf.Clamp(worldX - fields.OriginX, 0, fields.Size - 1);
            int y = Mathf.Clamp(worldY - fields.OriginY, 0, fields.Size - 1);
            return fields.Heights[y * fields.Size + x];
        }
        int mx = Mathf.Clamp(worldX - Chunk.OriginX, 0, mapSize - 1);
        int my = Mathf.Clamp(worldY - Chunk.OriginY, 0, mapSize - 1);
        return heightMap[mx, my];
    }

    /// <summary>
    /// Height of the terrain mesh at a world position: vertices every <c>lod</c> cells on the world-aligned grid, each
    /// quad split along its (x+1, y)-(x, y+1) diagonal exactly like <see cref="MeshGenerator.GenerateTerrainMesh"/>.
    /// </summary>
    public float SurfaceHeight(float worldX, float worldY)
    {
        int x0 = Mathf.FloorToInt(worldX / lod) * lod;
        int y0 = Mathf.FloorToInt(worldY / lod) * lod;
        float u = (worldX - x0) / lod, v = (worldY - y0) / lod;
        float h00 = CellHeight(x0, y0);
        float h10 = CellHeight(x0 + lod, y0);
        float h01 = CellHeight(x0, y0 + lod);
        float h11 = CellHeight(x0 + lod, y0 + lod);
        if (u + v <= 1f)
            return h00 + u * (h10 - h00) + v * (h01 - h00);
        return h11 + (1f - u) * (h01 - h11) + (1f - v) * (h10 - h11);
    }

    /// <summary>Terrain normal averaged over about <paramref name="radius"/> (at least one mesh cell), so single triangles don't decide slopes.</summary>
    public Vector3 SurfaceNormal(float worldX, float worldY, float radius)
    {
        float d = Mathf.Max(radius, Mathf.Max(1f, lod));
        float dx = SurfaceHeight(worldX + d, worldY) - SurfaceHeight(worldX - d, worldY);
        float dy = SurfaceHeight(worldX, worldY + d) - SurfaceHeight(worldX, worldY - d);
        return new Vector3(-dx, 2f * d, -dy).normalized;
    }

    /// <summary>Slope in degrees at a position (averaged over about a mesh cell).</summary>
    public float SlopeDegrees(Vector3 normal)
    {
        return Mathf.Acos(Mathf.Clamp(normal.y, -1f, 1f)) * Mathf.Rad2Deg;
    }

    /// <summary>The water type at the cell nearest a world position.</summary>
    public WaterBodyType WaterAt(float worldX, float worldY)
    {
        if (fields != null)
            return fields.WaterAt(worldX, worldY);
        WaterMapData water = Chunk.Water;
        if (water == null)
            return WaterBodyType.None;
        int x = Mathf.RoundToInt(worldX) - Chunk.OriginX, y = Mathf.RoundToInt(worldY) - Chunk.OriginY;
        if (x < 0 || y < 0 || x >= water.Size || y >= water.Size)
            return WaterBodyType.None;
        return water.Type[x, y];
    }

    /// <summary>Water surface at the cell nearest a world position (NaN where dry).</summary>
    public float WaterSurfaceAt(float worldX, float worldY)
    {
        if (fields != null)
        {
            int x = Mathf.RoundToInt(worldX) - fields.OriginX, y = Mathf.RoundToInt(worldY) - fields.OriginY;
            return fields.Contains(x, y) ? fields.WaterSurface[fields.Index(x, y)] : float.NaN;
        }
        WaterMapData water = Chunk.Water;
        if (water == null)
            return float.NaN;
        int mx = Mathf.RoundToInt(worldX) - Chunk.OriginX, my = Mathf.RoundToInt(worldY) - Chunk.OriginY;
        if (mx < 0 || my < 0 || mx >= water.Size || my >= water.Size)
            return float.NaN;
        return water.Surface[mx, my];
    }

    /// <summary>Distance to the nearest water of the given kinds (0 on it). Far (100000) without water data.</summary>
    public float WaterDistance(float worldX, float worldY, WaterBodyMask bodies)
    {
        if (fields == null || !fields.HasWater)
            return PlacementFields.FarDistance;
        return fields.WaterDistance(Mathf.RoundToInt(worldX) - fields.OriginX, Mathf.RoundToInt(worldY) - fields.OriginY, bodies);
    }

    /// <summary>On water: distance out from the shore. 0 on land.</summary>
    public float ShoreDistance(float worldX, float worldY)
    {
        if (fields == null || !fields.HasWater)
            return 0f;
        return fields.ShoreDistance(Mathf.RoundToInt(worldX) - fields.OriginX, Mathf.RoundToInt(worldY) - fields.OriginY);
    }

    public float CliffDistance(float worldX, float worldY)
    {
        if (fields == null)
            return PlacementFields.FarDistance;
        return fields.CliffDistance(Mathf.RoundToInt(worldX) - fields.OriginX, Mathf.RoundToInt(worldY) - fields.OriginY, Plan.CliffAngle);
    }

    /// <summary>Level of the nearest water (its surface on water), NaN when none is near.</summary>
    public float NearestWaterLevel(float worldX, float worldY)
    {
        if (fields == null)
            return float.NaN;
        int x = Mathf.Clamp(Mathf.RoundToInt(worldX) - fields.OriginX, 0, fields.Size - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(worldY) - fields.OriginY, 0, fields.Size - 1);
        return fields.NearestWaterLevel[fields.Index(x, y)];
    }

    /// <summary>Water flow (direction x speed) in a river at a position inside the chunk; zero elsewhere.</summary>
    public Vector2 FlowAt(float worldX, float worldY)
    {
        WaterMapData water = Chunk.Water;
        if (water == null)
            return Vector2.zero;
        int x = Mathf.Clamp(Mathf.RoundToInt(worldX) - Chunk.OriginX, 0, water.Size - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(worldY) - Chunk.OriginY, 0, water.Size - 1);
        return new Vector2(water.FlowX[x, y], water.FlowY[x, y]);
    }

    /// <summary>Direction of the nearest river's course (unit vector downstream), zero when none is near.</summary>
    public Vector2 RiverDirection(float worldX, float worldY)
    {
        if (fields == null)
            return Vector2.zero;
        int x = Mathf.Clamp(Mathf.RoundToInt(worldX) - fields.OriginX, 0, fields.Size - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(worldY) - fields.OriginY, 0, fields.Size - 1);
        int i = fields.Index(x, y);
        return new Vector2(fields.RiverDirX[i], fields.RiverDirY[i]);
    }

    /// <summary>Ground wetness near water (0-1) inside the chunk.</summary>
    public float GroundWetness(float worldX, float worldY)
    {
        WaterMapData water = Chunk.Water;
        if (water == null)
            return 0f;
        int x = Mathf.Clamp(Mathf.RoundToInt(worldX) - Chunk.OriginX, 0, water.Size - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(worldY) - Chunk.OriginY, 0, water.Size - 1);
        return water.Wetness[x, y];
    }

    /// <summary>
    /// Relief (height above (+) or below (-) the average within <paramref name="radius"/>) and roughness (standard
    /// deviation of height within it), from summed-area tables of the heights around the chunk.
    /// </summary>
    public void Relief(float worldX, float worldY, float radius, out float relief, out float roughness)
    {
        float height = SurfaceHeight(worldX, worldY);
        BoxStats(Mathf.RoundToInt(worldX), Mathf.RoundToInt(worldY), Mathf.Max(1, Mathf.RoundToInt(radius)), out double mean, out double variance);
        relief = (float)(height - mean);
        roughness = Mathf.Sqrt((float)System.Math.Max(0.0, variance));
    }

    /// <summary>Curvature: height above (+, bumps and crests) or below (-, hollows and gullies) the average within 3 units.</summary>
    public float Curvature(float worldX, float worldY)
    {
        float height = SurfaceHeight(worldX, worldY);
        BoxStats(Mathf.RoundToInt(worldX), Mathf.RoundToInt(worldY), 3, out double mean, out _);
        return (float)(height - mean);
    }

    private void BoxStats(int worldX, int worldY, int radius, out double mean, out double variance)
    {
        int size;
        int originX, originY;
        if (fields != null)
        {
            size = fields.Size;
            originX = fields.OriginX;
            originY = fields.OriginY;
        }
        else
        {
            size = mapSize;
            originX = Chunk.OriginX;
            originY = Chunk.OriginY;
        }

        if (sum == null)
            BuildTables(size);

        int x0 = Mathf.Clamp(worldX - radius - originX, 0, size - 1), x1 = Mathf.Clamp(worldX + radius - originX, 0, size - 1);
        int y0 = Mathf.Clamp(worldY - radius - originY, 0, size - 1), y1 = Mathf.Clamp(worldY + radius - originY, 0, size - 1);
        double count = (x1 - x0 + 1) * (double)(y1 - y0 + 1);
        double s = Area(sum, size, x0, y0, x1, y1), s2 = Area(sumSquares, size, x0, y0, x1, y1);
        mean = s / count;
        variance = s2 / count - mean * mean;
    }

    private void BuildTables(int size)
    {
        int stride = size + 1;
        var s = new double[stride * stride];
        var s2 = new double[stride * stride];
        for (int y = 0; y < size; y++)
        {
            double row = 0, row2 = 0;
            for (int x = 0; x < size; x++)
            {
                double h = fields != null ? fields.Heights[y * size + x] : heightMap[x, y];
                row += h;
                row2 += h * h;
                s[(y + 1) * stride + x + 1] = s[y * stride + x + 1] + row;
                s2[(y + 1) * stride + x + 1] = s2[y * stride + x + 1] + row2;
            }
        }
        sum = s;
        sumSquares = s2;
    }

    private static double Area(double[] table, int size, int x0, int y0, int x1, int y1)
    {
        int stride = size + 1;
        return table[(y1 + 1) * stride + x1 + 1] - table[y0 * stride + x1 + 1] - table[(y1 + 1) * stride + x0] + table[y0 * stride + x0];
    }

    /// <summary>
    /// The biome of the whole world cell containing a position: the chunk's biome map where it covers the cell,
    /// otherwise the same lookup the biome map is built from - so every chunk agrees on it.
    /// </summary>
    public Biome BiomeAtCell(int worldX, int worldY)
    {
        Biome[,] map = Chunk.BiomeMap;
        if (map != null)
        {
            int x = worldX - Chunk.OriginX, y = worldY - Chunk.OriginY;
            if (x >= 0 && y >= 0 && x < map.GetLength(0) && y < map.GetLength(1))
                return map[x, y];
        }
        return Plan.Sampler.SampleBiome(worldX, worldY);
    }

    /// <summary>Biome gaps at a position (see <see cref="TerrainHeightSampler.GetBiomeGaps"/>). The list is reused between calls.</summary>
    public List<KeyValuePair<Biome, float>> BiomeGaps(float worldX, float worldY)
    {
        Plan.Sampler.GetBiomeGaps(worldX, worldY, gaps);
        return gaps;
    }

    /// <summary>Climate moisture and temperature (with the terrain's influence) at a position.</summary>
    public void Climate(float worldX, float worldY, out float temperature, out float moisture)
    {
        Vector2 position = new Vector2(worldX, worldY);
        temperature = ClimateGenerator.GetTemperature(position, Plan.VoronoiSeed, Plan.ClimateNoiseScale);
        moisture = ClimateGenerator.GetMoisture(position, Plan.VoronoiSeed, Plan.ClimateNoiseScale);
        if (Plan.Climate != null)
            Plan.Climate.Apply(position, ref temperature, ref moisture);
    }
}
