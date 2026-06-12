using UnityEngine;

/// <summary>
/// The environment object placement reads for one chunk, over the chunk plus a margin around it (so objects
/// near a chunk edge can be fitted to the ground and water just across it): final terrain heights, which
/// water covers each cell, distances to each kind of water and to the shore, the nearest water level and
/// the local river direction. Built on the worker thread together with the chunk's heights (see
/// <see cref="HeightGenerator"/>) and dropped once the chunk's objects are placed.
///
/// Distances are exact (Euclidean distance transforms over the covered area), combined with distances from
/// the world's water features for water further away than the area reaches.
/// </summary>
public sealed class PlacementFields
{
    /// <summary>Cells covered beyond each side of the chunk (when the height generation has that much padding).</summary>
    public const int DefaultMargin = 16;
    /// <summary>Distances are clamped to this (world units): "far".</summary>
    public const float FarDistance = 100000f;

    /// <summary>World cell of index 0 (x, y).</summary>
    public readonly int OriginX, OriginY;
    /// <summary>Cells per side.</summary>
    public readonly int Size;
    /// <summary>Cells beyond the chunk on each side.</summary>
    public readonly int Margin;

    /// <summary>Final terrain height per cell (index = y * Size + x).</summary>
    public readonly float[] Heights;
    /// <summary><see cref="WaterBodyType"/> per cell.</summary>
    public readonly byte[] WaterType;
    /// <summary>Water surface per cell, NaN where dry.</summary>
    public readonly float[] WaterSurface;
    /// <summary>Level of the nearest water (its surface on wet cells), NaN when none is near.</summary>
    public readonly float[] NearestWaterLevel;
    /// <summary>Distance to each kind of water from the world's water features, indexed by <see cref="WaterBodyType"/> (Ocean..River); negative inside.</summary>
    public readonly float[][] FeatureDistance;
    /// <summary>Direction of the nearest river's course (unit, downstream), zero when none is near.</summary>
    public readonly float[] RiverDirX, RiverDirY;

    // Distance fields, computed on first use (see WaterDistance / ShoreDistance / CliffDistance): one per
    // combination of water kinds asked for (index = WaterBodyMask value).
    private readonly float[][] waterDistance = new float[16][];
    private float[] shoreDistance;
    private float[] cliffDistance;
    private float cliffDistanceAngle = -1f;
    private readonly object fieldLock = new object();

    public PlacementFields(int originX, int originY, int size, int margin, bool withWater)
    {
        OriginX = originX;
        OriginY = originY;
        Size = size;
        Margin = margin;
        int count = size * size;
        Heights = new float[count];
        WaterType = new byte[count];
        WaterSurface = new float[count];
        NearestWaterLevel = new float[count];
        FeatureDistance = new float[5][];
        for (int t = 1; t <= 4; t++)
            FeatureDistance[t] = Filled(count, float.PositiveInfinity);
        RiverDirX = new float[count];
        RiverDirY = new float[count];
        for (int i = 0; i < count; i++)
        {
            WaterSurface[i] = float.NaN;
            NearestWaterLevel[i] = float.NaN;
        }
        HasWater = withWater;
    }

    /// <summary>False when water is turned off (every cell dry, distances "far").</summary>
    public readonly bool HasWater;

    /// <summary>Builds the fields from a chunk's final padded heights (and water context, if any).</summary>
    /// <param name="paddedOrigin">World cell of paddedHeights[0, 0].</param>
    /// <param name="padding">Padding of paddedHeights around the chunk.</param>
    /// <param name="margin">Margin to cover (at most <paramref name="padding"/>).</param>
    public static PlacementFields Build(float[,] paddedHeights, Vector2 paddedOrigin, int padding, int finalSize, int margin, ChunkWaterContext water)
    {
        margin = Mathf.Clamp(margin, 0, padding);
        int size = finalSize + 2 * margin;
        int offset = padding - margin;
        var fields = new PlacementFields(Mathf.RoundToInt(paddedOrigin.x) + offset, Mathf.RoundToInt(paddedOrigin.y) + offset, size, margin, water != null);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                fields.Heights[y * size + x] = paddedHeights[x + offset, y + offset];

        if (water != null)
            water.FillPlacementFields(fields, paddedHeights, offset);
        return fields;
    }

    public int Index(int x, int y) => y * Size + x;

    /// <summary>Local cell of a world position (may be outside the covered area).</summary>
    public void ToLocal(float worldX, float worldY, out float lx, out float ly)
    {
        lx = worldX - OriginX;
        ly = worldY - OriginY;
    }

    public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;

    /// <summary>Water type at the cell nearest a world position (None outside the covered area).</summary>
    public WaterBodyType WaterAt(float worldX, float worldY)
    {
        int x = Mathf.RoundToInt(worldX - OriginX), y = Mathf.RoundToInt(worldY - OriginY);
        if (!Contains(x, y))
            return WaterBodyType.None;
        return (WaterBodyType)WaterType[Index(x, y)];
    }

    /// <summary>
    /// Distance (world units) from a cell to the nearest cell covered by water of the given kinds (0 on such
    /// water). Uses the exact distance transform within the covered area and the world's water features beyond it.
    /// </summary>
    public float WaterDistance(int x, int y, WaterBodyMask bodies)
    {
        x = Mathf.Clamp(x, 0, Size - 1);
        y = Mathf.Clamp(y, 0, Size - 1);
        int i = Index(x, y);
        int mask = (int)bodies & 15;
        float best = DistanceField(mask)[i];
        for (int t = 1; t <= 4; t++)
        {
            if ((mask & (1 << (t - 1))) != 0)
                best = Mathf.Min(best, Mathf.Max(0f, FeatureDistance[t][i]));
        }
        return best;
    }

    /// <summary>On water: distance to the nearest dry cell (how far out from the shore). 0 on dry land.</summary>
    public float ShoreDistance(int x, int y)
    {
        x = Mathf.Clamp(x, 0, Size - 1);
        y = Mathf.Clamp(y, 0, Size - 1);
        if (shoreDistance == null)
        {
            lock (fieldLock)
            {
                if (shoreDistance == null)
                {
                    var seeds = new bool[Size * Size];
                    for (int i = 0; i < seeds.Length; i++)
                        seeds[i] = WaterType[i] == 0;
                    shoreDistance = DistanceTransform(seeds, Size);
                }
            }
        }
        return shoreDistance[Index(x, y)];
    }

    /// <summary>Distance to the nearest ground steeper than <paramref name="cliffAngle"/> degrees (within the covered area).</summary>
    public float CliffDistance(int x, int y, float cliffAngle)
    {
        x = Mathf.Clamp(x, 0, Size - 1);
        y = Mathf.Clamp(y, 0, Size - 1);
        if (cliffDistance == null || cliffDistanceAngle != cliffAngle)
        {
            lock (fieldLock)
            {
                if (cliffDistance == null || cliffDistanceAngle != cliffAngle)
                {
                    float tan = Mathf.Tan(Mathf.Clamp(cliffAngle, 1f, 89f) * Mathf.Deg2Rad);
                    var seeds = new bool[Size * Size];
                    for (int cy = 0; cy < Size; cy++)
                        for (int cx = 0; cx < Size; cx++)
                            seeds[Index(cx, cy)] = SlopeTangent(cx, cy) > tan;
                    cliffDistance = DistanceTransform(seeds, Size);
                    cliffDistanceAngle = cliffAngle;
                }
            }
        }
        return cliffDistance[Index(x, y)];
    }

    /// <summary>Steepness (rise over run) at a cell from central differences.</summary>
    public float SlopeTangent(int x, int y)
    {
        int x0 = Mathf.Max(0, x - 1), x1 = Mathf.Min(Size - 1, x + 1);
        int y0 = Mathf.Max(0, y - 1), y1 = Mathf.Min(Size - 1, y + 1);
        float dx = (Heights[Index(x1, y)] - Heights[Index(x0, y)]) / Mathf.Max(1, x1 - x0);
        float dy = (Heights[Index(x, y1)] - Heights[Index(x, y0)]) / Mathf.Max(1, y1 - y0);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Distance to the nearest cell whose water kind is in <paramref name="mask"/> (bit t-1 = WaterBodyType t).</summary>
    private float[] DistanceField(int mask)
    {
        float[] field = waterDistance[mask];
        if (field != null)
            return field;
        lock (fieldLock)
        {
            if (waterDistance[mask] == null)
            {
                var seeds = new bool[Size * Size];
                for (int i = 0; i < seeds.Length; i++)
                {
                    int type = WaterType[i];
                    seeds[i] = type >= 1 && type <= 4 && (mask & (1 << (type - 1))) != 0;
                }
                waterDistance[mask] = DistanceTransform(seeds, Size);
            }
            return waterDistance[mask];
        }
    }

    /// <summary>
    /// Exact Euclidean distance from every cell to the nearest seed cell (Felzenszwalb &amp; Huttenlocher's
    /// linear-time transform, rows then columns). <see cref="FarDistance"/> when there is no seed.
    /// </summary>
    public static float[] DistanceTransform(bool[] seeds, int size)
    {
        const float Infinity = 1e20f;
        int seedCount = 0;
        for (int i = 0; i < seeds.Length; i++)
            if (seeds[i])
                seedCount++;
        var result = new float[size * size];
        if (seedCount == 0 || seedCount == seeds.Length)
        {
            // No seed: everything is far. All seeds: everything is 0.
            if (seedCount == 0)
                for (int i = 0; i < result.Length; i++)
                    result[i] = FarDistance;
            return result;
        }

        var squared = new float[size * size];
        var f = new float[size];
        var d = new float[size];
        var v = new int[size];
        var z = new float[size + 1];

        for (int y = 0; y < size; y++)
        {
            int row = y * size;
            bool any = false;
            for (int x = 0; x < size; x++)
            {
                bool seed = seeds[row + x];
                f[x] = seed ? 0f : Infinity;
                any |= seed;
            }
            if (!any)
            {
                // A row without seeds stays "infinitely" far until the column pass.
                for (int x = 0; x < size; x++)
                    squared[row + x] = Infinity;
                continue;
            }
            Transform1D(f, size, d, v, z);
            for (int x = 0; x < size; x++)
                squared[row + x] = d[x];
        }
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
                f[y] = squared[y * size + x];
            Transform1D(f, size, d, v, z);
            for (int y = 0; y < size; y++)
                squared[y * size + x] = d[y];
        }

        for (int i = 0; i < result.Length; i++)
            result[i] = squared[i] >= Infinity * 0.5f ? FarDistance : Mathf.Sqrt(squared[i]);
        return result;
    }

    private static void Transform1D(float[] f, int n, float[] d, int[] v, float[] z)
    {
        int k = 0;
        v[0] = 0;
        z[0] = float.NegativeInfinity;
        z[1] = float.PositiveInfinity;
        for (int q = 1; q < n; q++)
        {
            float s = ((f[q] + q * (float)q) - (f[v[k]] + v[k] * (float)v[k])) / (2f * q - 2f * v[k]);
            while (s <= z[k])
            {
                k--;
                s = ((f[q] + q * (float)q) - (f[v[k]] + v[k] * (float)v[k])) / (2f * q - 2f * v[k]);
            }
            k++;
            v[k] = q;
            z[k] = s;
            z[k + 1] = float.PositiveInfinity;
        }
        k = 0;
        for (int q = 0; q < n; q++)
        {
            while (z[k + 1] < q)
                k++;
            float delta = q - v[k];
            d[q] = delta * delta + f[v[k]];
        }
    }

    private static float[] Filled(int count, float value)
    {
        var array = new float[count];
        for (int i = 0; i < count; i++)
            array[i] = value;
        return array;
    }
}
