using System;
using System.Collections.Concurrent;
using UnityEngine;

/// <summary>
/// Seam-free erosion. Erosion is a simulation (droplets run across the terrain one after another, each
/// changing the ground the next one flows over), so two chunks eroding their own padded areas never agree
/// exactly along their shared border - that left small steps between chunks.
///
/// Instead, erosion runs on fixed world-space tiles: one per chunk position, each over the same padded area
/// a chunk at that position uses. A tile's result depends only on its position, the seed and the settings,
/// and is cached, so it is identical whichever chunk asks for it. A cell's final height is a blend of the
/// tiles overlapping it, with weights that also depend only on world position: inside a tile it is simply
/// that tile's result, and across a band of <see cref="FadeWidth"/> cells either side of a tile border it
/// crossfades smoothly from one tile's result to the next. Every chunk therefore computes exactly the same
/// height for every shared cell - no seams - and the crossfade itself is smooth, so there is no visible line.
///
/// A chunk at a tile position (as <see cref="EndlessTerrain"/> places them) is assembled straight from the
/// tiles; the only extra work is the tiles of not-yet-generated neighbors near its borders, which are then
/// reused as-is when those neighbors are generated.
/// </summary>
public static class ErosionTiles
{
    /// <summary>Most eroded tiles kept in memory (about 0.3 MB each); older ones are recomputed if needed again.</summary>
    private const int MaxCachedTiles = 160;

    private sealed class Tile
    {
        public int OriginX, OriginY;   // world cell of Heights[0]
        public int Size;               // cells per side
        public float[] Heights;

        public float At(int worldX, int worldY)
        {
            return Heights[(worldY - OriginY) * Size + (worldX - OriginX)];
        }
    }

    private static readonly ConcurrentDictionary<long, Lazy<Tile>> Cache = new ConcurrentDictionary<long, Lazy<Tile>>();
    private static readonly ConcurrentQueue<long> Order = new ConcurrentQueue<long>();

    public static void ClearCache()
    {
        Cache.Clear();
        while (Order.TryDequeue(out _)) { }
    }

    /// <summary>World-space tile size: the chunk spacing <see cref="EndlessTerrain"/> uses.</summary>
    public static int TileSize(TerrainGenerator tg) => tg.ChunkSize - 1;

    /// <summary>Width (cells) of the crossfade either side of a tile border - kept well inside the erosion padding.</summary>
    public static int FadeWidth(int padding, int tileSize)
    {
        return Mathf.Clamp(Mathf.Min(16, padding / 2), 1, Mathf.Max(1, tileSize / 4));
    }

    /// <summary>True when seamless (tiled) erosion is on and the padding is wide enough for it.</summary>
    public static bool Applies(TerrainGenerator tg, int padding)
    {
        return tg.SeamlessErosion && padding >= 4 && TileSize(tg) >= 16;
    }

    /// <summary>
    /// Replaces a chunk's padded pre-erosion heights with the seamless eroded heights. When the chunk sits
    /// on the tile grid, its own (already computed) area becomes that tile, unless the tile exists already.
    /// </summary>
    public static void ErodeSeamlessly(TerrainGenerator tg, float[,] heights, float[,] resistance, float[,] rainfall, Vector2 paddedOrigin, int padding)
    {
        int tileSize = TileSize(tg);
        int fade = FadeWidth(padding, tileSize);
        int originX = Mathf.RoundToInt(paddedOrigin.x);
        int originY = Mathf.RoundToInt(paddedOrigin.y);
        if (IsOwnTile(tg, originX, originY, heights.GetLength(0), padding, out Vector2Int own))
        {
            // Registered through the cache, so a thread already computing this tile is waited for rather than duplicated.
            float[,] preEroded = (float[,])heights.Clone();
            Lazy<Tile> entry = Cache.GetOrAdd(Key(own), key => new Lazy<Tile>(() =>
            {
                HeightGenerator.Erode(tg, preEroded, resistance, rainfall, paddedOrigin);
                return Extract(preEroded, originX, originY, padding, fade, tileSize);
            }, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication));
            _ = entry.Value;
            Order.Enqueue(Key(own));
            Trim();
        }

        Assemble(tg, heights, paddedOrigin, padding);
    }

    /// <summary>True when a padded chunk area is exactly the area of a tile.</summary>
    public static bool IsOwnTile(TerrainGenerator tg, int paddedOriginX, int paddedOriginY, int size, int padding, out Vector2Int tile)
    {
        int tileSize = TileSize(tg);
        int coreX = paddedOriginX + padding, coreY = paddedOriginY + padding;
        tile = new Vector2Int(FloorDiv(coreX, tileSize), FloorDiv(coreY, tileSize));
        return FloorMod(coreX, tileSize) == 0 && FloorMod(coreY, tileSize) == 0 && size == tg.ChunkSize + 1 + 2 * padding;
    }

    /// <summary>
    /// Fills a (padded) area with the seamless eroded heights: every cell is the position-weighted blend of
    /// the eroded tiles covering it (computing any tile not cached yet). Needs no pre-erosion data of its own.
    /// </summary>
    public static void Assemble(TerrainGenerator tg, float[,] heights, Vector2 paddedOrigin, int padding)
    {
        int tileSize = TileSize(tg);
        int fade = FadeWidth(padding, tileSize);
        int size = heights.GetLength(0);
        int originX = Mathf.RoundToInt(paddedOrigin.x);
        int originY = Mathf.RoundToInt(paddedOrigin.y);

        // Tiles overlapping this area, looked up once.
        int kx0 = FloorDiv(originX - fade, tileSize) - 1, kx1 = FloorDiv(originX + size + fade, tileSize) + 1;
        int ky0 = FloorDiv(originY - fade, tileSize) - 1, ky1 = FloorDiv(originY + size + fade, tileSize) + 1;
        var tiles = new Tile[kx1 - kx0 + 1, ky1 - ky0 + 1];

        float[] weightsX = new float[3];
        float[] weightsY = new float[3];
        for (int y = 0; y < size; y++)
        {
            int worldY = originY + y;
            int baseKy = FloorDiv(worldY, tileSize) - 1;
            for (int j = 0; j < 3; j++)
                weightsY[j] = Weight(worldY, baseKy + j, tileSize, fade);

            for (int x = 0; x < size; x++)
            {
                int worldX = originX + x;
                int baseKx = FloorDiv(worldX, tileSize) - 1;
                for (int i = 0; i < 3; i++)
                    weightsX[i] = Weight(worldX, baseKx + i, tileSize, fade);

                // Summed in a fixed order (tile rows, then columns) so every chunk gets the same float result.
                float height = 0f;
                for (int j = 0; j < 3; j++)
                {
                    if (weightsY[j] <= 0f)
                        continue;
                    for (int i = 0; i < 3; i++)
                    {
                        float weight = weightsX[i] * weightsY[j];
                        if (weight <= 0f)
                            continue;
                        int tx = baseKx + i, ty = baseKy + j;
                        Tile tile = tiles[tx - kx0, ty - ky0];
                        if (tile == null)
                            tiles[tx - kx0, ty - ky0] = tile = Get(tg, new Vector2Int(tx, ty));
                        height += weight * tile.At(worldX, worldY);
                    }
                }
                heights[x, y] = height;
            }
        }
    }

    /// <summary>
    /// Weight of tile <paramref name="k"/> at world coordinate <paramref name="w"/> along one axis: 1 inside
    /// the tile, 0 outside, crossfading over 2 x <paramref name="fade"/> cells centered on each border. The
    /// weights of all tiles always sum to 1 (each border's ramp is added to one tile and subtracted from the
    /// other).
    /// </summary>
    private static float Weight(int w, int k, int tileSize, int fade)
    {
        return Ramp(w - (k * tileSize - fade), fade) - Ramp(w - ((k + 1) * tileSize - fade), fade);
    }

    private static float Ramp(int t, int fade)
    {
        if (t <= 0) return 0f;
        int width = 2 * fade;
        if (t >= width) return 1f;
        return WaterGenerator.SmoothStep01(t / (float)width);
    }

    private static Tile Get(TerrainGenerator tg, Vector2Int tile)
    {
        long key = Key(tile);
        if (!Cache.TryGetValue(key, out Lazy<Tile> lazy))
        {
            lazy = Cache.GetOrAdd(key, NewEntry(tg, tile));
            Order.Enqueue(key);
            Trim();
        }
        return lazy.Value;
    }

    // Separate from Get so the lambda's captured variables are only allocated on a miss.
    private static Lazy<Tile> NewEntry(TerrainGenerator tg, Vector2Int tile)
    {
        return new Lazy<Tile>(() => Compute(tg, tile), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private static void Trim()
    {
        // Dropping a tile is always safe: recomputing it gives exactly the same result.
        while (Cache.Count > MaxCachedTiles && Order.TryDequeue(out long oldest))
            Cache.TryRemove(oldest, out _);
    }

    /// <summary>A tile computed on its own: exactly what a chunk at that position does before and during erosion.</summary>
    private static Tile Compute(TerrainGenerator tg, Vector2Int tile)
    {
        int tileSize = TileSize(tg);
        int padding = Mathf.Max(0, tg.ErosionPadding);
        int fade = FadeWidth(padding, tileSize);
        int paddedSize = tg.ChunkSize + 1 + 2 * padding;
        Vector2 paddedOrigin = new Vector2(tile.x * tileSize - padding, tile.y * tileSize - padding);

        WaterSettings waterSettings = tg.EnableWater ? WaterSettings.From(tg) : null;
        TerrainHeightSampler sampler = new TerrainHeightSampler(tg, waterSettings);
        ChunkWaterContext water = waterSettings != null
            ? WaterGenerator.CreateChunkContext(waterSettings, sampler, paddedOrigin, paddedSize)
            : null;

        float[,] heights = HeightGenerator.BuildBaseHeights(tg, sampler, water, paddedOrigin, paddedSize, true, out float[,] resistance, out float[,] rainfall);
        HeightGenerator.Erode(tg, heights, resistance, rainfall, paddedOrigin);
        return Extract(heights, Mathf.RoundToInt(paddedOrigin.x), Mathf.RoundToInt(paddedOrigin.y), padding, fade, tileSize);
    }

    /// <summary>The part of an eroded padded area a tile's weights can reach: its core plus the fade band around it.</summary>
    private static Tile Extract(float[,] eroded, int paddedOriginX, int paddedOriginY, int padding, int fade, int tileSize)
    {
        int size = tileSize + 2 * fade;
        int start = padding - fade;
        var tile = new Tile
        {
            OriginX = paddedOriginX + start,
            OriginY = paddedOriginY + start,
            Size = size,
            Heights = new float[size * size],
        };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tile.Heights[y * size + x] = eroded[start + x, start + y];
        return tile;
    }

    private static long Key(Vector2Int tile) => WaterGenerator.CellKey(tile);

    private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);
    private static int FloorMod(int a, int b) => a - FloorDiv(a, b) * b;
}
