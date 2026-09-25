using System.Collections.Concurrent;
using System.Collections.Generic;

/// <summary>
/// Per-pixel biome blend for a chunk's splat maps: up to <see cref="Slots"/> (2-4) biome indices per pixel
/// with weights summing to 1 - see <see cref="SplatMapGenerator.GenerateSplatMaps"/>. Computed on the worker thread
/// that builds the chunk's biome map (sharing the same biome blend lookups), so the main thread only has
/// to write the pixels. <see cref="SplatMapGenerator.GenerateSplatMaps"/> computes it itself when none
/// is passed in.
/// </summary>
public sealed class SplatBlendData
{
    public readonly int Size;
    /// <summary>How many biomes each pixel can mix (see <see cref="TerrainGenerator.SplatTexturesPerPixel"/>).</summary>
    public readonly int Slots;
    public readonly int[,,] Indices;
    public readonly float[,,] Weights;

    public SplatBlendData(int size, int slots = 2)
    {
        Size = size;
        Slots = slots;
        Indices = new int[size, size, slots];
        Weights = new float[size, size, slots];
    }
}

/// <summary>
/// Maps a biome to its splat index exactly as the splat generator always has - through its name, in
/// <see cref="TerrainGenerator.BiomeDefinitions"/> order (a later biome with the same name wins) - but
/// reads each biome's name once per chunk instead of once per pixel.
/// </summary>
public sealed class SplatBiomeIndex
{
    private readonly Dictionary<string, int> byName;
    private readonly ConcurrentDictionary<Biome, int> byBiome = new ConcurrentDictionary<Biome, int>();

    public SplatBiomeIndex(Dictionary<string, int> byName)
    {
        this.byName = byName;
    }

    public int this[Biome biome]
    {
        get
        {
            if (biome == null)
                return byName[biome.name];   // throws exactly as the per-pixel lookup did
            if (byBiome.TryGetValue(biome, out int index))
                return index;
            index = byName[biome.name];
            byBiome[biome] = index;
            return index;
        }
    }
}
