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
/// Foothills reach a little past the territory's edge into the neighboring land, so mountains rise out of it
/// instead of standing on it. Everything is computed from world position and settings only, cached in world
/// tiles, so neighboring chunks always agree and the result doesn't depend on which chunk was generated first.
/// </summary>
public sealed class MountainMassifs
{
    /// <summary>Grid nodes per tile side.</summary>
    private const int TileNodes = 32;
    /// <summary>Extra nodes stored around a tile, for smooth (bicubic) sampling across its edge.</summary>
    private const int Border = 2;
    private const int Stored = TileNodes + 2 * Border;
    /// <summary>Most tiles kept; beyond this the caches start over (tiles are recomputed exactly when needed).</summary>
    private const int MaxTiles = 3000;

    /// <summary>Height of the tallest massifs, in biome amplitudes (before each massif's own stature).</summary>
    public const float MassifHeightScale = 2.6f;
    /// <summary>Gentlest average flank a massif may have (degrees); decides how deep a territory must be for full height.</summary>
    public const float GentlestFlank = 20f;

    private sealed class MaskTile
    {
        /// <summary>Per node: index of its mountain biome's parameters, -1 = not mountain.</summary>
        public sbyte[] Mountain;
    }

    private sealed class Tile
    {
        public float[] Depth;          // signed distance to the territory edge, world units (+ inside)
        public float[] Amplitude;      // mountain parameters per node (null = the single parameter set)
        public float[] Wavelength;
        public float[] Roughness;
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

    [ThreadStatic] private static Tile lastTile;
    [ThreadStatic] private static TileKey lastKey;

    private readonly Func<float, float, Biome> nearestBiome;
    private readonly Dictionary<Biome, int> mountainIndex = new Dictionary<Biome, int>();
    private readonly float[] amplitudes, wavelengths, roughnesses;
    private readonly long settings;
    private readonly int seed;
    private readonly float nodeSpacing, depthCap, apronLimit, warpScale;

    /// <summary>Forgets every cached tile (call when settings change; <see cref="WaterGenerator.ClearCaches"/> does).</summary>
    public static void ClearCache()
    {
        Tiles.Clear();
        Masks.Clear();
    }

    /// <summary>
    /// The massifs for these land biomes, or null when none of them uses the Mountains landform (the terrain is
    /// then exactly as without this). <paramref name="nearestBiome"/> gives the land biome owning a position.
    /// </summary>
    public static MountainMassifs Create(TerrainGenerator tg, List<Biome> landBiomes, LandformSettings s, Func<float, float, Biome> nearestBiome)
    {
        if (s.Mode == TerrainShapeMode.ClassicOnly || landBiomes == null)
            return null;
        var mountains = new List<Biome>();
        foreach (Biome biome in landBiomes)
            if (biome != null && !mountains.Contains(biome) && LandformGenerator.Effective(biome, s.Mode) == LandformType.Mountains)
                mountains.Add(biome);
        if (mountains.Count == 0 || mountains.Count > 120)
            return null;
        return new MountainMassifs(tg, mountains, landBiomes, s, nearestBiome);
    }

    private MountainMassifs(TerrainGenerator tg, List<Biome> mountains, List<Biome> landBiomes, LandformSettings s, Func<float, float, Biome> nearestBiome)
    {
        this.nearestBiome = nearestBiome;
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
        apronLimit = 0.75f * s.PointSpacing;
        warpScale = s.PointSpacing;

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
        // The territory is read at a bent position and its depth varied, so massifs don't copy the cell outlines.
        LandformGenerator.MassifDepthWarp(x, y, seed, warpScale, out float wx, out float wy, out float depthOffset);
        float sx = x + wx, sy = y + wy;
        float gx = sx / nodeSpacing, gy = sy / nodeSpacing;
        int nx = Mathf.FloorToInt(gx), ny = Mathf.FloorToInt(gy);
        int tx = FloorDiv(nx, TileNodes), ty = FloorDiv(ny, TileNodes);
        Tile tile = GetTile(tx, ty);
        if (tile.MaxDepth <= -apronLimit)
            return 0f;

        // Local node coordinates inside the stored (bordered) tile.
        float lx = gx - tx * TileNodes + Border, ly = gy - ty * TileNodes + Border;
        float depth = Bicubic(tile.Depth, lx, ly) + depthOffset;
        if (depth <= -apronLimit)
            return 0f;

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
        return LandformGenerator.MassifHeight(x, y, depth, amplitude, wavelength, roughness, seed, apronLimit);
    }

    /// <summary>Signed distance (world units) to the edge of the mountain territory: positive inside, negative outside.</summary>
    public float Depth(float x, float y)
    {
        LandformGenerator.MassifDepthWarp(x, y, seed, warpScale, out float wx, out float wy, out float depthOffset);
        float gx = (x + wx) / nodeSpacing, gy = (y + wy) / nodeSpacing;
        int tx = FloorDiv(Mathf.FloorToInt(gx), TileNodes), ty = FloorDiv(Mathf.FloorToInt(gy), TileNodes);
        Tile tile = GetTile(tx, ty);
        return Bicubic(tile.Depth, gx - tx * TileNodes + Border, gy - ty * TileNodes + Border) + depthOffset;
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
        if (Tiles.Count > MaxTiles)
            ClearCache();
        tile = Tiles.GetOrAdd(key, k => new Lazy<Tile>(() => BuildTile(k.X, k.Y))).Value;
        lastTile = tile;
        lastKey = key;
        return tile;
    }

    // ------------------------------------------------------------------ building a tile

    /// <summary>Which grid nodes are mountain territory (and whose parameters), 32 x 32 nodes at a time.</summary>
    private MaskTile GetMask(int mx, int my)
    {
        var key = new TileKey { Settings = settings, X = mx, Y = my };
        if (Masks.Count > MaxTiles * 4)
            Masks.Clear();
        return Masks.GetOrAdd(key, k => new Lazy<MaskTile>(() => BuildMask(k.X, k.Y))).Value;
    }

    private MaskTile BuildMask(int mx, int my)
    {
        var mask = new MaskTile { Mountain = new sbyte[TileNodes * TileNodes] };
        for (int j = 0; j < TileNodes; j++)
        {
            for (int i = 0; i < TileNodes; i++)
            {
                float x = (mx * TileNodes + i) * nodeSpacing, y = (my * TileNodes + j) * nodeSpacing;
                Biome biome = nearestBiome(x, y);
                mask.Mountain[j * TileNodes + i] = biome != null && mountainIndex.TryGetValue(biome, out int index) ? (sbyte)index : (sbyte)-1;
            }
        }
        return mask;
    }

    private Tile BuildTile(int tx, int ty)
    {
        // The tile's stored nodes plus a margin wide enough for every distance up to the cap.
        int margin = Mathf.CeilToInt(depthCap / nodeSpacing) + 1;
        int n = Stored + 2 * margin;
        int originX = tx * TileNodes - Border - margin, originY = ty * TileNodes - Border - margin;

        var region = new sbyte[n * n];
        bool anyMountain = false, anyOther = false;
        for (int j = 0; j < n; j++)
        {
            int gy = originY + j;
            int my = FloorDiv(gy, TileNodes);
            for (int i = 0; i < n; i++)
            {
                int gx = originX + i;
                int mx = FloorDiv(gx, TileNodes);
                MaskTile mask = GetMask(mx, my);
                sbyte value = mask.Mountain[(gy - my * TileNodes) * TileNodes + (gx - mx * TileNodes)];
                region[j * n + i] = value;
                if (value >= 0) anyMountain = true; else anyOther = true;
            }
        }

        var tile = new Tile { Depth = new float[Stored * Stored] };
        float cap = depthCap;
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
