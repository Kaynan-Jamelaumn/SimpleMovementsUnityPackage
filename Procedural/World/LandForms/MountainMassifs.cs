using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The mountains of the Mountains landform, built from the shape of the mountain territory: every spot knows how
/// deep inside the territory it is (the distance to its edge, from a distance field on a coarse world grid), and
/// the mountain rises with that depth. So a small territory makes a small formation, a large one a broad massif,
/// a long one a range whose main ridge follows its spine, and a narrow neck a saddle between two massifs - the
/// structure comes from where the mountains are instead of a noise field cut out by the biome border. The height
/// itself (ridges, valleys, peaks, cliffs, benches) is shaped in <see cref="LandformGenerator.MassifHeight"/>.
///
/// Foothills reach a set distance past the territory's edge into the neighboring land (Foothill Reach), so
/// mountains rise out of it instead of standing on it, without taking over the neighbors. Everything is computed
/// from world position and settings only, cached in world tiles, so neighboring chunks always agree and the
/// result doesn't depend on which chunk was generated first.
/// </summary>
public sealed class MountainMassifs
{
    /// <summary>Grid nodes per tile side.</summary>
    private const int TileNodes = 32;
    /// <summary>Extra nodes stored around a tile, for smooth (bicubic) sampling across its edge.</summary>
    private const int Border = 2;
    private const int Stored = TileNodes + 2 * Border;
    /// <summary>Grid nodes per mask side (which nodes are mountain territory is found and cached in these blocks).</summary>
    private const int MaskNodes = 8;
    /// <summary>Most tiles and masks kept; beyond this the caches start over (recomputed exactly when needed).</summary>
    private const int MaxTiles = 3000;
    private const int MaxMasks = 80000;

    /// <summary>Height of the tallest massifs, in biome amplitudes (before each massif's own stature).</summary>
    public const float MassifHeightScale = 2.6f;
    /// <summary>Gentlest average flank a massif may have (degrees); decides how deep a territory must be for full height.</summary>
    public const float GentlestFlank = 20f;

    private sealed class MaskTile
    {
        /// <summary>Per node: index of its mountain biome's parameters, -1 = not mountain.</summary>
        public sbyte[] Mountain;
        /// <summary>Any node of this mask is mountain territory.</summary>
        public bool AnyMountain;
    }

    private sealed class Tile
    {
        public float[] Depth;          // signed distance to the territory edge, world units (+ inside)
        public float[] Amplitude;      // mountain parameters per node (null = the single parameter set)
        public float[] Wavelength;
        public float[] Roughness;
        /// <summary>Deepest stored node: a tile whose nodes are all far outside the territory adds nothing.</summary>
        public float MaxDepth;
    }

    private struct TileKey : IEquatable<TileKey>
    {
        public long Settings;
        public int X, Y;
        public bool Equals(TileKey other) => Settings == other.Settings && X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is TileKey other && Equals(other);
        public override int GetHashCode() => (int)Settings * 397 ^ X * 7919 ^ Y;
    }

    private static readonly ConcurrentDictionary<TileKey, Lazy<Tile>> Tiles = new ConcurrentDictionary<TileKey, Lazy<Tile>>();
    private static readonly ConcurrentDictionary<TileKey, Lazy<MaskTile>> Masks = new ConcurrentDictionary<TileKey, Lazy<MaskTile>>();

    // Tile and mask counts, kept here because ConcurrentDictionary.Count locks the whole dictionary.
    private static int tileCount, maskCount;

    // The last two tiles each thread used (a sample reads its own tile and, inside mountains, the one its bent
    // position falls in, usually the same).
    [ThreadStatic] private static Tile lastTile, otherTile;
    [ThreadStatic] private static TileKey lastKey, otherKey;

    /// <summary>Adds every land biome that owns any position in the rectangle [min, max] (it may add a few more).</summary>
    public delegate void BiomesInArea(Vector2 min, Vector2 max, HashSet<Biome> into);

    /// <summary>A mask with no mountain territory, shared by every such mask (most of the world).</summary>
    private static readonly MaskTile NoMountain = Uniform(-1);

    private readonly Func<float, float, Biome> nearestBiome;
    private readonly BiomesInArea biomesInArea;
    private MaskTile[] allMountain;
    private readonly Dictionary<Biome, int> mountainIndex = new Dictionary<Biome, int>();
    private readonly float[] amplitudes, wavelengths, roughnesses;
    private readonly long settings;
    private readonly int seed;
    private readonly float nodeSpacing, depthCap, foothillReach, warpScale, bendIn;

    /// <summary>Forgets every cached tile (call when settings change; <see cref="WaterGenerator.ClearCaches"/> does).</summary>
    public static void ClearCache()
    {
        Tiles.Clear();
        Masks.Clear();
        tileCount = 0;
        maskCount = 0;
    }

    /// <summary>
    /// The massifs for these land biomes, or null when none of them uses the Mountains landform (the terrain is
    /// then exactly as without this). <paramref name="nearestBiome"/> gives the land biome owning a position,
    /// <paramref name="biomesInArea"/> (optional) the land biomes an area may hold.
    /// </summary>
    public static MountainMassifs Create(TerrainGenerator tg, List<Biome> landBiomes, LandformSettings s, Func<float, float, Biome> nearestBiome,
        BiomesInArea biomesInArea = null)
    {
        if (s.Mode == TerrainShapeMode.ClassicOnly || landBiomes == null)
            return null;
        var mountains = new List<Biome>();
        foreach (Biome biome in landBiomes)
            if (biome != null && !mountains.Contains(biome) && LandformGenerator.Effective(biome, s.Mode) == LandformType.Mountains)
                mountains.Add(biome);
        if (mountains.Count == 0 || mountains.Count > 120)
            return null;
        return new MountainMassifs(tg, mountains, landBiomes, s, nearestBiome, biomesInArea);
    }

    private MountainMassifs(TerrainGenerator tg, List<Biome> mountains, List<Biome> landBiomes, LandformSettings s, Func<float, float, Biome> nearestBiome,
        BiomesInArea biomesInArea)
    {
        this.nearestBiome = nearestBiome;
        this.biomesInArea = biomesInArea;
        seed = s.Seed;
        amplitudes = new float[mountains.Count];
        wavelengths = new float[mountains.Count];
        roughnesses = new float[mountains.Count];
        float tallest = 1f;
        for (int i = 0; i < mountains.Count; i++)
        {
            Biome biome = mountains[i];
            mountainIndex[biome] = i;
            amplitudes[i] = Mathf.Max(0f, biome.amplitude);
            wavelengths[i] = s.ChunkWidth / Mathf.Max(0.05f, Mathf.Abs(biome.frequency));
            roughnesses[i] = Mathf.Clamp(biome.persistence, 0.2f, 0.7f);
            tallest = Mathf.Max(tallest, amplitudes[i] * MassifHeightScale);
        }

        // Depth beyond which every massif has reached its full height (the gentlest flank), and a grid fine
        // enough to follow biome borders but coarse enough that a tile's distance field stays cheap.
        depthCap = Mathf.Clamp(tallest / Mathf.Tan(GentlestFlank * Mathf.Deg2Rad) + 20f, 60f, 1600f);
        nodeSpacing = Mathf.Max(Mathf.Clamp(s.PointSpacing / 7f, 6f, 32f), depthCap / 44f);
        foothillReach = Mathf.Max(0f, s.FoothillReach);
        warpScale = s.PointSpacing;
        // Depth over which the bend fades in from the territory's edge: long enough that fading it in never
        // makes a flank noticeably steeper than the bend itself would.
        bendIn = 1.5f * s.PointSpacing;

        // Everything the territory and the parameters depend on, so tiles of different settings never mix.
        unchecked
        {
            long h = 1469598103934665603L;
            h = (h ^ tg.VoronoiSeed) * 1099511628211L;
            h = (h ^ BitConverter.ToInt32(BitConverter.GetBytes(tg.VoronoiScale), 0)) * 1099511628211L;
            h = (h ^ tg.NumVoronoiPoints) * 1099511628211L;
            h = (h ^ BitConverter.ToInt32(BitConverter.GetBytes(tg.VoronoiWarpStrength), 0)) * 1099511628211L;
            h = (h ^ BitConverter.ToInt32(BitConverter.GetBytes(tg.VoronoiWarpScale), 0)) * 1099511628211L;
            h = (h ^ BitConverter.ToInt32(BitConverter.GetBytes(tg.BiomeClusterStrength), 0)) * 1099511628211L;
            h = (h ^ BitConverter.ToInt32(BitConverter.GetBytes(tg.BiomeRepeatPenalty), 0)) * 1099511628211L;
            h = (h ^ (tg.OrderIndependentBiomeLayout ? 1 : 0) ^ (tg.UseNaturalClimatePlacement ? 2 : 0) ^ (tg.useWeightedBiome ? 4 : 0)) * 1099511628211L;
            h = (h ^ BitConverter.ToInt32(BitConverter.GetBytes(tg.MountainBeltStrength), 0)) * 1099511628211L;
            h = (h ^ (int)s.Mode) * 1099511628211L;
            h = (h ^ (long)s.ChunkWidth) * 1099511628211L;
            foreach (Biome biome in landBiomes)
            {
                if (biome == null)
                    continue;
                h = (h ^ System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(biome)) * 1099511628211L;
                h = (h ^ (int)biome.landform) * 1099511628211L;
                h = (h ^ BitConverter.ToInt32(BitConverter.GetBytes(biome.amplitude), 0)) * 1099511628211L;
                h = (h ^ BitConverter.ToInt32(BitConverter.GetBytes(biome.frequency), 0)) * 1099511628211L;
                h = (h ^ BitConverter.ToInt32(BitConverter.GetBytes(biome.persistence), 0)) * 1099511628211L;
                h = (h ^ BitConverter.ToInt32(BitConverter.GetBytes(biome.weight), 0)) * 1099511628211L;
            }
            settings = h;
        }
    }

    /// <summary>Height the mountains add at a world position (0 away from mountain territories).</summary>
    public float Height(float x, float y)
    {
        // Away from the mountains - most of the world - this is all a sample costs: one cached tile check.
        float gx = x / nodeSpacing, gy = y / nodeSpacing;
        Tile tile = TileAt(gx, gy, out float lx, out float ly);
        if (tile.MaxDepth + nodeSpacing <= -foothillReach)
            return 0f;
        float depth = Bicubic(tile.Depth, lx, ly);
        if (depth <= -foothillReach)
            return 0f;
        if (depth > 0f)
            depth = Bent(x, y, depth);

        float amplitude, wavelength, roughness;
        if (tile.Amplitude == null)
        {
            amplitude = amplitudes[0];
            wavelength = wavelengths[0];
            roughness = roughnesses[0];
        }
        else
        {
            amplitude = Bilinear(tile.Amplitude, lx, ly);
            wavelength = Bilinear(tile.Wavelength, lx, ly);
            roughness = Bilinear(tile.Roughness, lx, ly);
        }
        if (amplitude <= 0f)
            return 0f;
        return LandformGenerator.MassifHeight(x, y, depth, amplitude, wavelength, roughness, seed, foothillReach);
    }

    /// <summary>
    /// Inside the territory, the depth read at a bent position and varied, so massifs and their main ridges
    /// wander instead of following the biome cells' outline. Fades in from the edge, so the territory's edge
    /// itself stays where the biome border is and foothills never reach further than Foothill Reach.
    /// </summary>
    private float Bent(float x, float y, float depth)
    {
        LandformGenerator.MassifDepthWarp(x, y, seed, warpScale, out float wx, out float wy, out float depthOffset);
        Tile tile = TileAt((x + wx) / nodeSpacing, (y + wy) / nodeSpacing, out float lx, out float ly);
        float bent = Bicubic(tile.Depth, lx, ly) + depthOffset;
        float t = Mathf.Clamp01(depth / bendIn);
        return depth + t * t * (3f - 2f * t) * (bent - depth);
    }

    /// <summary>Signed distance (world units) to the edge of the mountain territory: positive inside, negative outside.</summary>
    public float Depth(float x, float y)
    {
        Tile tile = TileAt(x / nodeSpacing, y / nodeSpacing, out float lx, out float ly);
        float depth = Bicubic(tile.Depth, lx, ly);
        return depth > 0f ? Bent(x, y, depth) : depth;
    }

    /// <summary>The tile holding grid position (gx, gy), and that position in the tile's stored nodes.</summary>
    private Tile TileAt(float gx, float gy, out float lx, out float ly)
    {
        int tx = FloorDiv(Mathf.FloorToInt(gx), TileNodes), ty = FloorDiv(Mathf.FloorToInt(gy), TileNodes);
        lx = gx - tx * TileNodes + Border;
        ly = gy - ty * TileNodes + Border;
        return GetTile(tx, ty);
    }

    private static int FloorDiv(int a, int b)
    {
        return a >= 0 ? a / b : -((-a + b - 1) / b);
    }

    private Tile GetTile(int tx, int ty)
    {
        var key = new TileKey { Settings = settings, X = tx, Y = ty };
        Tile tile = lastTile;
        if (tile != null && lastKey.Equals(key))
            return tile;
        tile = otherTile;
        if (tile == null || !otherKey.Equals(key))
        {
            if (!Tiles.TryGetValue(key, out Lazy<Tile> entry))
            {
                if (tileCount > MaxTiles)
                    ClearCache();
                entry = Tiles.GetOrAdd(key, new Lazy<Tile>(() => BuildTile(tx, ty)));
            }
            tile = entry.Value;
        }
        otherTile = lastTile;
        otherKey = lastKey;
        lastTile = tile;
        lastKey = key;
        return tile;
    }

    // ------------------------------------------------------------------ building a tile

    /// <summary>Which grid nodes are mountain territory (and whose parameters), 8 x 8 nodes at a time.</summary>
    private MaskTile GetMask(int mx, int my)
    {
        var key = new TileKey { Settings = settings, X = mx, Y = my };
        if (!Masks.TryGetValue(key, out Lazy<MaskTile> entry))
        {
            if (maskCount > MaxMasks)
            {
                Masks.Clear();
                maskCount = 0;
            }
            entry = Masks.GetOrAdd(key, new Lazy<MaskTile>(() => BuildMask(mx, my)));
        }
        return entry.Value;
    }

    private MaskTile BuildMask(int mx, int my)
    {
        System.Threading.Interlocked.Increment(ref maskCount);
        // Most masks are away from any mountain, or entirely inside one: tell from the biomes the area can hold
        // (the Voronoi points that could be nearest in it) without looking up its nodes.
        if (biomesInArea != null)
        {
            var biomes = new HashSet<Biome>();
            float size = (MaskNodes - 1) * nodeSpacing;
            var min = new Vector2(mx * MaskNodes * nodeSpacing, my * MaskNodes * nodeSpacing);
            biomesInArea(min, min + new Vector2(size, size), biomes);
            int only = -1;
            bool anyMountain = false;
            foreach (Biome biome in biomes)
            {
                if (biome != null && mountainIndex.TryGetValue(biome, out int index))
                {
                    anyMountain = true;
                    only = index;
                }
            }
            if (!anyMountain)
                return NoMountain;
            if (biomes.Count == 1)
                return AllMountain(only);
        }

        // The biome is looked up at every other node first; nodes between two (or four) that agree take their
        // value, and only nodes where a border passes between them are looked up too - a quarter of the lookups
        // away from borders. Only sub-node slivers of a biome can be missed, which the massifs never show.
        const int C = MaskNodes / 2 + 1;
        var coarse = new sbyte[C * C];
        for (int j = 0; j < C; j++)
            for (int i = 0; i < C; i++)
                coarse[j * C + i] = Lookup(mx * MaskNodes + 2 * i, my * MaskNodes + 2 * j);

        var mask = new MaskTile { Mountain = new sbyte[MaskNodes * MaskNodes] };
        for (int j = 0; j < MaskNodes; j++)
        {
            int cj = j >> 1;
            bool oddJ = (j & 1) != 0;
            for (int i = 0; i < MaskNodes; i++)
            {
                int ci = i >> 1;
                bool oddI = (i & 1) != 0;
                sbyte value;
                if (!oddI && !oddJ)
                {
                    value = coarse[cj * C + ci];
                }
                else
                {
                    sbyte a = coarse[cj * C + ci];
                    sbyte b = coarse[(cj + (oddJ ? 1 : 0)) * C + ci + (oddI ? 1 : 0)];
                    bool same = a == b;
                    if (same && oddI && oddJ)
                        same = coarse[cj * C + ci + 1] == a && coarse[(cj + 1) * C + ci] == a;
                    value = same ? a : Lookup(mx * MaskNodes + i, my * MaskNodes + j);
                }
                mask.Mountain[j * MaskNodes + i] = value;
                mask.AnyMountain |= value >= 0;
            }
        }
        return mask;
    }

    private static MaskTile Uniform(int index)
    {
        var mask = new MaskTile { Mountain = new sbyte[MaskNodes * MaskNodes], AnyMountain = index >= 0 };
        for (int k = 0; k < mask.Mountain.Length; k++)
            mask.Mountain[k] = (sbyte)index;
        return mask;
    }

    /// <summary>A mask entirely of one mountain biome (shared; masks are never modified).</summary>
    private MaskTile AllMountain(int index)
    {
        MaskTile[] masks = allMountain;
        if (masks == null)
            allMountain = masks = new MaskTile[amplitudes.Length];
        return masks[index] ?? (masks[index] = Uniform(index));
    }

    /// <summary>Mountain parameter index at a grid node, -1 when it isn't mountain territory.</summary>
    private sbyte Lookup(int nodeX, int nodeY)
    {
        Biome biome = nearestBiome(nodeX * nodeSpacing, nodeY * nodeSpacing);
        return biome != null && mountainIndex.TryGetValue(biome, out int index) ? (sbyte)index : (sbyte)-1;
    }

    /// <summary>A tile with no mountain territory within Foothill Reach of it (shared; tiles are never modified).</summary>
    private Tile farTile;

    private Tile BuildTile(int tx, int ty)
    {
        System.Threading.Interlocked.Increment(ref tileCount);
        float cap = depthCap;

        // First only the tile and the foothill reach around it: with no mountain territory there, the tile adds
        // nothing, whatever lies further out - most tiles stop here without the wide margin below.
        int near = Mathf.CeilToInt(foothillReach / nodeSpacing) + 3;
        if (!AnyMountainIn(tx * TileNodes - Border - near, ty * TileNodes - Border - near, Stored + 2 * near))
        {
            if (farTile == null)
            {
                var far = new Tile { Depth = new float[Stored * Stored], MaxDepth = -cap };
                for (int k = 0; k < far.Depth.Length; k++) far.Depth[k] = -cap;
                farTile = far;
            }
            return farTile;
        }

        // The tile's stored nodes plus a margin wide enough for every distance up to the cap.
        int margin = Mathf.CeilToInt(cap / nodeSpacing) + 1;
        int n = Stored + 2 * margin;
        int originX = tx * TileNodes - Border - margin, originY = ty * TileNodes - Border - margin;

        // Copied a mask at a time (each mask looked up once, not once per node).
        var region = new sbyte[n * n];
        bool anyMountain = false, anyOther = false;
        int maskX0 = FloorDiv(originX, MaskNodes), maskX1 = FloorDiv(originX + n - 1, MaskNodes);
        int maskY0 = FloorDiv(originY, MaskNodes), maskY1 = FloorDiv(originY + n - 1, MaskNodes);
        for (int my = maskY0; my <= maskY1; my++)
        {
            int j0 = Mathf.Max(0, my * MaskNodes - originY), j1 = Mathf.Min(n, (my + 1) * MaskNodes - originY);
            for (int mx = maskX0; mx <= maskX1; mx++)
            {
                MaskTile mask = GetMask(mx, my);
                sbyte[] values = mask.Mountain;
                int i0 = Mathf.Max(0, mx * MaskNodes - originX), i1 = Mathf.Min(n, (mx + 1) * MaskNodes - originX);
                if (mask.AnyMountain) anyMountain = true;
                for (int j = j0; j < j1; j++)
                {
                    int row = (originY + j - my * MaskNodes) * MaskNodes - mx * MaskNodes + originX;
                    for (int i = i0; i < i1; i++)
                    {
                        sbyte value = values[row + i];
                        region[j * n + i] = value;
                        if (value < 0) anyOther = true;
                    }
                }
            }
        }

        var tile = new Tile { Depth = new float[Stored * Stored] };
        if (!anyMountain)
        {
            for (int k = 0; k < tile.Depth.Length; k++) tile.Depth[k] = -cap;
            tile.MaxDepth = -cap;
            return tile;
        }

        // Distance to the nearest non-mountain node (inside) and to the nearest mountain node (outside).
        var inside = new float[n * n];
        var outside = new float[n * n];
        var nearestMountain = new int[n * n];
        var scratchIndex = new int[n * n];
        const float Inf = 1e20f;
        for (int k = 0; k < region.Length; k++)
        {
            inside[k] = region[k] >= 0 ? Inf : 0f;
            outside[k] = region[k] >= 0 ? 0f : Inf;
            scratchIndex[k] = k;
        }
        if (anyOther)
            DistanceTransform(inside, (int[])scratchIndex.Clone(), n, null);
        DistanceTransform(outside, scratchIndex, n, nearestMountain);

        tile.MaxDepth = -cap;
        for (int j = 0; j < Stored; j++)
        {
            for (int i = 0; i < Stored; i++)
            {
                int k = (j + margin) * n + (i + margin);
                // Node centers: the edge lies half a node from the nearest node on the other side.
                float depth = region[k] >= 0
                    ? (anyOther ? Mathf.Sqrt(inside[k]) * nodeSpacing - 0.5f * nodeSpacing : cap)
                    : -(Mathf.Sqrt(outside[k]) * nodeSpacing - 0.5f * nodeSpacing);
                depth = Mathf.Clamp(depth, -cap, cap);
                tile.Depth[j * Stored + i] = depth;
                tile.MaxDepth = Mathf.Max(tile.MaxDepth, depth);
            }
        }

        // With several mountain biomes, each node takes its own (or its nearest mountain node's) parameters,
        // smoothed so different mountain biomes blend into each other instead of stepping.
        if (amplitudes.Length > 1)
        {
            var amp = new float[n * n];
            var wave = new float[n * n];
            var rough = new float[n * n];
            for (int k = 0; k < region.Length; k++)
            {
                int source = region[k] >= 0 ? k : nearestMountain[k];
                int index = source >= 0 && source < region.Length ? region[source] : -1;
                if (index < 0)
                    continue;
                amp[k] = amplitudes[index];
                wave[k] = wavelengths[index];
                rough[k] = roughnesses[index];
            }
            int radius = Mathf.Clamp(Mathf.RoundToInt(40f / nodeSpacing), 1, 4);
            BoxBlur(amp, n, radius); BoxBlur(amp, n, radius);
            BoxBlur(wave, n, radius); BoxBlur(wave, n, radius);
            BoxBlur(rough, n, radius); BoxBlur(rough, n, radius);
            tile.Amplitude = new float[Stored * Stored];
            tile.Wavelength = new float[Stored * Stored];
            tile.Roughness = new float[Stored * Stored];
            for (int j = 0; j < Stored; j++)
            {
                for (int i = 0; i < Stored; i++)
                {
                    int k = (j + margin) * n + (i + margin);
                    tile.Amplitude[j * Stored + i] = amp[k];
                    tile.Wavelength[j * Stored + i] = Mathf.Max(1f, wave[k]);
                    tile.Roughness[j * Stored + i] = rough[k] > 0f ? rough[k] : 0.5f;
                }
            }
        }
        return tile;
    }

    /// <summary>Whether any node of the n x n block starting at (x0, y0) is mountain territory.</summary>
    private bool AnyMountainIn(int x0, int y0, int n)
    {
        int mx0 = FloorDiv(x0, MaskNodes), mx1 = FloorDiv(x0 + n - 1, MaskNodes);
        int my0 = FloorDiv(y0, MaskNodes), my1 = FloorDiv(y0 + n - 1, MaskNodes);
        for (int my = my0; my <= my1; my++)
        {
            for (int mx = mx0; mx <= mx1; mx++)
            {
                MaskTile mask = GetMask(mx, my);
                if (!mask.AnyMountain)
                    continue;
                int i0 = Mathf.Max(x0, mx * MaskNodes) - mx * MaskNodes, i1 = Mathf.Min(x0 + n, (mx + 1) * MaskNodes) - mx * MaskNodes;
                int j0 = Mathf.Max(y0, my * MaskNodes) - my * MaskNodes, j1 = Mathf.Min(y0 + n, (my + 1) * MaskNodes) - my * MaskNodes;
                for (int j = j0; j < j1; j++)
                    for (int i = i0; i < i1; i++)
                        if (mask.Mountain[j * MaskNodes + i] >= 0)
                            return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Exact squared Euclidean distance transform (in node units) of an n x n grid: sites hold 0, others a large
    /// value. With <paramref name="nearest"/>, also the index of each node's nearest site.
    /// </summary>
    private static void DistanceTransform(float[] grid, int[] siteIndex, int n, int[] nearest)
    {
        var f = new float[n];
        var fi = new int[n];
        var d = new float[n];
        var di = new int[n];
        var v = new int[n];
        var z = new float[n + 1];
        // Columns, then rows (the second pass carries each node's nearest site from the first).
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++) { f[j] = grid[j * n + i]; fi[j] = siteIndex[j * n + i]; }
            Transform1D(f, fi, n, d, di, v, z);
            for (int j = 0; j < n; j++) { grid[j * n + i] = d[j]; siteIndex[j * n + i] = di[j]; }
        }
        for (int j = 0; j < n; j++)
        {
            for (int i = 0; i < n; i++) { f[i] = grid[j * n + i]; fi[i] = siteIndex[j * n + i]; }
            Transform1D(f, fi, n, d, di, v, z);
            for (int i = 0; i < n; i++) { grid[j * n + i] = d[i]; siteIndex[j * n + i] = di[i]; }
        }
        if (nearest != null)
            Array.Copy(siteIndex, nearest, siteIndex.Length);
    }

    /// <summary>1D squared distance transform (lower envelope of parabolas, Felzenszwalb & Huttenlocher).</summary>
    private static void Transform1D(float[] f, int[] fi, int n, float[] d, int[] di, int[] v, float[] z)
    {
        const float Inf = 1e20f;
        int k = -1;
        for (int q = 0; q < n; q++)
        {
            if (f[q] >= Inf)
                continue;
            if (k < 0)
            {
                k = 0; v[0] = q; z[0] = -Inf; z[1] = Inf;
                continue;
            }
            float s = 0f;
            while (k >= 0)
            {
                int p = v[k];
                s = ((f[q] + q * q) - (f[p] + p * p)) / (2f * q - 2f * p);
                if (s <= z[k]) k--;
                else break;
            }
            if (k < 0)
            {
                k = 0; v[0] = q; z[0] = -Inf; z[1] = Inf;
                continue;
            }
            k++;
            v[k] = q; z[k] = s; z[k + 1] = Inf;
        }
        if (k < 0)
        {
            for (int q = 0; q < n; q++) { d[q] = Inf; di[q] = -1; }
            return;
        }
        k = 0;
        for (int q = 0; q < n; q++)
        {
            while (z[k + 1] < q) k++;
            int p = v[k];
            d[q] = (q - p) * (q - p) + f[p];
            di[q] = fi[p];
        }
    }

    private static void BoxBlur(float[] grid, int n, int radius)
    {
        var temp = new float[n * n];
        for (int j = 0; j < n; j++)
        {
            for (int i = 0; i < n; i++)
            {
                float sum = 0f; int count = 0;
                for (int k = -radius; k <= radius; k++) { int ii = i + k; if (ii < 0 || ii >= n) continue; sum += grid[j * n + ii]; count++; }
                temp[j * n + i] = sum / count;
            }
        }
        for (int j = 0; j < n; j++)
        {
            for (int i = 0; i < n; i++)
            {
                float sum = 0f; int count = 0;
                for (int k = -radius; k <= radius; k++) { int jj = j + k; if (jj < 0 || jj >= n) continue; sum += temp[jj * n + i]; count++; }
                grid[j * n + i] = sum / count;
            }
        }
    }

    // ------------------------------------------------------------------ sampling

    /// <summary>Smooth (Catmull-Rom) interpolation of a stored tile grid, so slopes don't show the grid.</summary>
    private static float Bicubic(float[] grid, float lx, float ly)
    {
        int ix = Mathf.FloorToInt(lx), iy = Mathf.FloorToInt(ly);
        float fx = lx - ix, fy = ly - iy;
        ix = Mathf.Clamp(ix, 1, Stored - 3);
        iy = Mathf.Clamp(iy, 1, Stored - 3);
        float r0 = CatmullRom(grid, ix, iy - 1, fx);
        float r1 = CatmullRom(grid, ix, iy, fx);
        float r2 = CatmullRom(grid, ix, iy + 1, fx);
        float r3 = CatmullRom(grid, ix, iy + 2, fx);
        return Cubic(r0, r1, r2, r3, fy);
    }

    private static float CatmullRom(float[] grid, int ix, int row, float t)
    {
        int o = row * Stored + ix;
        return Cubic(grid[o - 1], grid[o], grid[o + 1], grid[o + 2], t);
    }

    private static float Cubic(float p0, float p1, float p2, float p3, float t)
    {
        return p1 + 0.5f * t * (p2 - p0 + t * (2f * p0 - 5f * p1 + 4f * p2 - p3 + t * (3f * (p1 - p2) + p3 - p0)));
    }

    private static float Bilinear(float[] grid, float lx, float ly)
    {
        int ix = Mathf.Clamp(Mathf.FloorToInt(lx), 0, Stored - 2), iy = Mathf.Clamp(Mathf.FloorToInt(ly), 0, Stored - 2);
        float fx = Mathf.Clamp01(lx - ix), fy = Mathf.Clamp01(ly - iy);
        int o = iy * Stored + ix;
        float a = Mathf.Lerp(grid[o], grid[o + 1], fx);
        float b = Mathf.Lerp(grid[o + Stored], grid[o + Stored + 1], fx);
        return Mathf.Lerp(a, b, fy);
    }
}
