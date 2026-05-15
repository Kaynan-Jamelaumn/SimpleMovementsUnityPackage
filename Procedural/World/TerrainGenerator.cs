using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Linq;
using static DataStructure;

/// <summary>
/// Available terrain sizes for chunk generation.
/// Values follow the pattern of (divisible number) + 1 for optimal LOD performance.
/// </summary>
public enum TerrainSize
{
    Small = 61,    // 60 + 1
    Medium = 121,  // 120 + 1
    Large = 181,   // 180 + 1
    ExtraLarge = 241  // 240 + 1 (maximum size)
}

/// <summary>
/// Generates terrain with customizable noise, textures, biomes, and objects.
/// Supports multithreading for map, terrain, and biome object generation.
/// </summary>
public partial class TerrainGenerator : MonoBehaviour
{
    [Header("Terrain Configuration")]
    /// <summary>
    /// Configurable size of a terrain chunk.
    /// </summary>
    [Tooltip("Size of the terrain chunk. Larger sizes provide more detail but require more processing.")]
    [SerializeField] private TerrainSize terrainSize = TerrainSize.ExtraLarge;

    /// <summary>
    /// Fixed maximum size of a terrain chunk for backward compatibility.
    /// </summary>
    [Tooltip("Fixed maximum size of a terrain chunk for backward compatibility.")]
    public static readonly int maxChunkSize = 241;

    /// <summary>
    /// Gets the current size of a terrain chunk based on the selected terrain size.
    /// </summary>
    [Tooltip("Property to get the current terrain chunk size.")]
    public int ChunkSize => (int)terrainSize;

    /// <summary>
    /// Gets the maximum chunk size (for backward compatibility).
    /// </summary>
    [Tooltip("Property to get the maximum terrain chunk size.")]
    public static int MaxChunkSize => maxChunkSize;

    [Header("Noise Configuration")]
    /// <summary>
    /// Scaling factor for terrain generation.
    /// </summary>
    [Tooltip("Scaling factor for terrain generation.")]
    [HideInInspector][SerializeField] private float scaleFactor = 1;

    /// <summary>
    /// Number of noise octaves for terrain generation.
    /// </summary>
    [Tooltip("Number of noise octaves for terrain generation.")]
    [SerializeField] private int octaves = 5;

    /// <summary>
    /// Lacunarity value for noise generation, affecting frequency scaling.
    /// </summary>
    [Tooltip("Lacunarity value for noise generation, affecting frequency scaling.")]
    [SerializeField] private float lacunarity = 2f;

    [Header("Texture")]
    /// <summary>
    /// Default texture for the terrain.
    /// </summary>
    [Tooltip("Default texture for the terrain.")]
    [HideInInspector][SerializeField] private Texture2D defaultTexture;

    /// <summary>
    /// Compute shader for generating splat maps.
    /// </summary>
    [Tooltip("Compute shader for generating splat maps.")]
    [HideInInspector][SerializeField] private ComputeShader splatMapShader;

    /// <summary>
    /// Minimum height for the terrain if it's NOT Voronoi-based texture.
    /// </summary>
    [Tooltip("Minimum height for the terrain if it's NOT Voronoi-based texture.")]
    [SerializeField] private float minHeight;

    /// <summary>
    /// Maximum height for the terrain if it's NOT Voronoi-based texture.
    /// </summary>
    [Tooltip("Maximum height for the terrain if it's NOT Voronoi-based texture.")]
    [SerializeField] private float maxHeight;

    /// <summary>
    /// Determines if terrain textures are based on Voronoi points.
    /// </summary>
    [Tooltip("Determines if terrain textures are based on Voronoi points.")]
    [SerializeField] private bool terrainTextureBasedOnVoronoiPoints = true;

    [Header("Texture Variation Settings")]
    /// <summary>
    /// Master toggle for all texture variation features. When OFF, uses original code path, which
    /// maps UVs as a plain linear (heightMapX * uvScale) with no rotation/offset - the same texture
    /// image then tiles identically every time, with zero variation, which is very noticeable for
    /// detailed textures (it reads as an obviously repeating pattern, both within one chunk, where
    /// it tiles several times, and continuing seamlessly across chunk borders). Strongly recommended on.
    /// </summary>
    [Tooltip("Master toggle for all texture variation features. Strongly recommended ON - without it, textures tile with zero variation and read as an obviously repeating pattern.")]
    [SerializeField] private bool enableTextureVariations = true;

    [Header("UV Rotation (Requires Texture Variations)")]
    /// <summary>
    /// Enable random UV rotation per chunk to reduce texture repetition.
    /// </summary>
    [Tooltip("Enable random UV rotation per chunk to reduce texture repetition.")]
    [SerializeField] private bool enableUVRotation = true;

    [Header("UV Noise Offset (Requires Texture Variations)")]
    /// <summary>
    /// Enable noise-based UV offset for additional texture variation.
    /// </summary>
    [Tooltip("Enable noise-based UV offset for additional texture variation.")]
    [SerializeField] private bool enableUVNoise = true;

    /// <summary>
    /// Strength of the noise-based UV offset.
    /// </summary>
    [Tooltip("Strength of the noise-based UV offset.")]
    [SerializeField][Range(0f, 1f)] private float uvNoiseStrength = 0.3f;

    /// <summary>
    /// Scale of the noise pattern used for UV offset.
    /// </summary>
    [Tooltip("Scale of the noise pattern used for UV offset.")]
    [SerializeField] private float uvNoiseScale = 0.1f;

    [Header("Texture Scale Variation (Requires Texture Variations)")]
    /// <summary>
    /// Enable random texture scale variation per chunk.
    /// </summary>
    [Tooltip("Enable random texture scale variation per chunk.")]
    [SerializeField] private bool enableTextureScaleVariation = true;

    /// <summary>
    /// Range of texture scale variation (multiplier).
    /// </summary>
    [Tooltip("Range of texture scale variation (multiplier).")]
    [SerializeField][Range(0.1f, 2f)] private float textureScaleVariationRange = 0.3f;

    [Header("Shader-Based Enhancements (Requires Texture Variations)")]
    /// <summary>
    /// Enable advanced shader-based texture blending and variations.
    /// </summary>
    [Tooltip("Enable advanced shader-based texture blending and variations.")]
    [SerializeField] private bool enableShaderEnhancements = false;

    /// <summary>
    /// Strength of UV rotation in shader (0-1).
    /// </summary>
    [Tooltip("Strength of UV rotation in shader (0-1).")]
    [SerializeField][Range(0f, 1f)] private float shaderUVRotationStrength = 0.5f;

    /// <summary>
    /// Scale variation for textures in shader.
    /// </summary>
    [Tooltip("Scale variation for textures in shader.")]
    [SerializeField][Range(0.1f, 3f)] private float shaderUVScaleVariation = 1.2f;

    /// <summary>
    /// Texture blend sharpness in shader.
    /// </summary>
    [Tooltip("Texture blend sharpness in shader.")]
    [SerializeField][Range(1f, 10f)] private float shaderTextureBlendSharpness = 4f;

    [Header("Voronoi Noise Configuration")]
    /// <summary>
    /// Number of Voronoi points to generate for terrain features.
    /// </summary>
    [Tooltip("Number of Voronoi points to generate for terrain features.")]
    [SerializeField] public int NumVoronoiPoints = 8;

    /// <summary>
    /// Seed value for Voronoi noise generation.
    /// </summary>
    [Tooltip("Seed value for Voronoi noise generation.")]
    [SerializeField] public int VoronoiSeed = 0;

    /// <summary>
    /// Scaling factor for Voronoi noise, affecting detail size.
    /// </summary>
    [Tooltip("Scaling factor for Voronoi noise, affecting detail size.")]
    [SerializeField] public float VoronoiScale = 350;

    /// <summary>
    /// Determines if biome selection should be weighted or random.
    /// </summary>
    [Tooltip("Determines if biome selection should be weighted or random.")]
    [SerializeField] public bool useWeightedBiome = true;

    [Header("Natural Biome Placement")]
    /// <summary>
    /// How strongly a newly placed Voronoi point's biome is pulled toward whatever biome already
    /// dominates its immediate neighborhood (already-placed nearby points). This is what actually
    /// turns scattered points into contiguous territories - it works independently of, and even
    /// with, climate-driven placement off, since it's purely about spatial proximity to neighbors.
    /// 0 disables it entirely, reproducing the original "every point rolls its biome in isolation"
    /// behavior. Keep this moderate rather than maxed out - pushed too high (close to 1), the
    /// outcome becomes close to deterministic once a pattern starts forming, which crushes organic
    /// variety and makes territories look like the same recognizable arrangement recurring everywhere.
    /// </summary>
    [Tooltip("How strongly a new biome cell is pulled toward matching its already-placed neighbors, purely by proximity. This is the main thing that turns scattered points into contiguous natural-looking territories. Keep it moderate (0.4-0.6) - too high and results become near-deterministic, looking like the same layout repeating.")]
    [SerializeField][Range(0f, 1f)] private float biomeClusterStrength = 0.5f;

    /// <summary>
    /// Radius of spatial clustering influence, expressed as a multiplier of VoronoiScale so it stays
    /// proportioned to the cell size no matter what VoronoiScale is set to.
    /// </summary>
    [Tooltip("Cluster influence radius = VoronoiScale * this multiplier. Larger values let territories grow across more cells before a different biome can take over.")]
    [SerializeField] private float biomeClusterRadiusMultiplier = 1.5f;

    /// <summary>
    /// How strongly a biome that's already common elsewhere is discouraged from spawning a brand new,
    /// disconnected patch of itself far from its existing territory. Without this, biomeClusterStrength
    /// alone only rewards growing an existing territory when a same-biome point happens to be nearby -
    /// it does nothing to stop the same biome from being independently rolled again somewhere else in
    /// the same area purely by chance, which is what produces several separate same-sized, same-shaped
    /// "islands" of one biome scattered inside a single chunk. 0 disables this (a biome can freely
    /// recur in multiple separate spots). This never penalizes legitimate contiguous growth - only
    /// picks where there's no nearby same-biome point to extend from are affected.
    /// </summary>
    [Tooltip("Discourages an already-common biome from spawning a brand new, disconnected patch far from its existing territory - this is what stops several separate same-shaped 'islands' of one biome inside a single chunk. Never penalizes growing an existing territory, only brand new disconnected ones.")]
    [SerializeField][Range(0f, 1f)] private float biomeRepeatPenalty = 0.6f;

    /// <summary>
    /// When enabled, biomes are assigned to Voronoi cells based on how well each biome's ideal
    /// temperature/moisture matches the climate at that point, instead of pure randomness -
    /// producing natural clustering (deserts in hot/dry areas, tundra in cold areas, etc.).
    /// This is an additional realism layer on top of biomeClusterStrength, not a replacement for it.
    /// </summary>
    [Tooltip("Biomes are placed by matching their ideal climate (temperature/moisture) instead of pure randomness, so similar biomes cluster naturally. Additional to biomeClusterStrength above, not a replacement for it.")]
    [SerializeField] private bool useNaturalClimatePlacement = true;

    /// <summary>
    /// Scale of the temperature/moisture noise fields, expressed as a multiplier of VoronoiScale so
    /// climate zones always span several biome cells (giving natural, clustered territories) no matter
    /// what VoronoiScale is set to. Must be well above 1 - a climate scale close to VoronoiScale still
    /// varies from cell to cell and produces the same "salt and pepper" patchwork as no climate placement at all.
    /// </summary>
    [Tooltip("Climate noise scale = VoronoiScale * this multiplier. Keep well above 1 (5-10 is a good range) so climate - and therefore biome clustering - spans several cells instead of varying cell-to-cell.")]
    [SerializeField] private float climateScaleMultiplier = 6f;

    /// <summary>
    /// Lets the large-scale terrain shape the climate: rain shadows behind mountain belts, colder mountains and
    /// continental interiors, wetter coasts. Changes where climate-placed biomes go, and erosion rainfall.
    /// </summary>
    [Tooltip("Lets the terrain shape the climate: mountain ranges cast rain shadows (wet windward side, dry land behind them - often desert), mountains and high continental interiors are colder, coasts are wetter and deep interiors drier. Affects climate-based biome placement and how much rain erodes the terrain. Rain shadow and mountain cooling need landforms with Mountain Belt Strength above 0; coastal effects need oceans.")]
    [SerializeField] private bool terrainAwareClimate = true;
    [Tooltip("Direction the prevailing wind blows TOWARD, in degrees: 0 = +X (east), 90 = +Z (north), 180 = west, 270 = south. Rain shadows form on this side of mountain ranges.")]
    [SerializeField][Range(0f, 360f)] private float prevailingWindAngle = 0f;
    [Tooltip("How much drier the land downwind of a mountain range is (and how much wetter its windward side). 0 = no rain shadow.")]
    [SerializeField][Range(0f, 1f)] private float rainShadowStrength = 0.6f;
    [Tooltip("How much colder mountain belts and risen continental interiors are. At 1, the heart of a mountain belt is 0.4 colder on the 0-1 temperature scale, favoring cold biomes (tundra, glacial) there.")]
    [SerializeField][Range(0f, 1f)] private float altitudeCooling = 0.5f;
    [Tooltip("How much wetter land near the sea is, and how much drier deep continental interiors are. Needs oceans.")]
    [SerializeField][Range(0f, 1f)] private float coastalMoisture = 0.4f;

    /// <summary>
    /// World-unit strength of the domain warp applied to Voronoi cell borders, making them read as
    /// organic, wobbly boundaries instead of straight polygon edges. 0 disables warping. Internally
    /// clamped relative to the warp noise scale so borders can never fold into self-intersecting shapes.
    /// </summary>
    [Tooltip("Strength of the organic domain-warp applied to biome cell borders. 0 = straight Voronoi edges.")]
    [SerializeField] private float voronoiWarpStrength = 50f;

    /// <summary>
    /// Scale of the domain-warp noise, expressed as a multiplier of VoronoiScale so the warp's
    /// wavelength always stays proportional to the cell size no matter what VoronoiScale is set to.
    /// Too small a multiplier makes the warp oscillate multiple times across a single cell edge,
    /// tearing it into jagged, self-crossing shapes instead of a gentle organic wobble.
    /// </summary>
    [Tooltip("Warp noise scale = VoronoiScale * this multiplier. Keep at or above 1 so the warp's wavelength isn't shorter than a cell, which tears borders into jagged, self-crossing shapes.")]
    [SerializeField] private float voronoiWarpScaleMultiplier = 1.5f;

    /// <summary>
    /// Fraction (0-1) of a Voronoi cell's scale used as the smooth transition band around each biome
    /// border, so height and (optionally) texture blend gradually between biomes instead of cutting hard.
    /// </summary>
    [Tooltip("Fraction of a biome cell's size used as a smooth blend band around its border.")]
    [SerializeField][Range(0f, 1f)] private float biomeBlendRange = 0.25f;

    /// <summary>
    /// When enabled (and TerrainTextureBasedOnVoronoiPoints is on), splat map weights are blended
    /// across biome borders the same way height is, instead of a hard one-biome-per-pixel texture.
    /// </summary>
    [Tooltip("Blend splat map textures across biome borders to match the blended terrain height.")]
    [SerializeField] private bool useBiomeBlendedTexturing = true;

    /// <summary>
    /// How many biome textures a single terrain pixel can mix, where several biomes meet. 2 = only the two
    /// strongest (a hard seam where three biomes meet); 3-4 = smooth three- and four-way junctions.
    /// </summary>
    [Tooltip("How many biome textures one terrain pixel can mix where several biomes meet. 2 = only the two strongest (a visible seam where three biomes meet); 3 or 4 = smooth three- and four-way junctions. Needs Blend Texturing on.")]
    [SerializeField][Range(2, 4)] private int splatTexturesPerPixel = 4;

    /// <summary>
    /// Steepest slope, in degrees, considered walkable at a biome border. When two neighboring biomes'
    /// height ranges (amplitude) differ a lot (e.g. Mountains next to Plains), <see cref="BiomeBlendRange"/>
    /// alone may not give the transition enough room to stay under this slope - in that case the blend
    /// band automatically widens (capped at one Voronoi cell width) just enough to keep the biome-to-biome
    /// elevation change walkable, without touching either biome's own natural terrain variation away from
    /// the border. This is deliberately separate from <see cref="TalusAngle"/>: that one governs when loose
    /// material collapses under erosion (a material-stability slope), this one governs whether a player can
    /// actually walk the transition (a traversal slope) - they don't have to match. 0 disables this and
    /// leaves border width exactly as <see cref="BiomeBlendRange"/> specifies (the original behavior).
    /// </summary>
    [Tooltip("Steepest slope (degrees) considered walkable where two biomes meet. The blend band automatically widens beyond Height Blend Range when two neighboring biomes' height ranges differ enough that this slope would otherwise be exceeded - capped at one Voronoi cell width. 0 disables this (border width comes purely from Height Blend Range, the original behavior). Separate from Talus Angle, which is about material stability, not player traversal.")]
    [SerializeField][Range(0f, 89f)] private float biomeBoundaryMaxSlopeDegrees = 28f;

    /// <summary>
    /// When on, which biome goes where depends only on the seed and settings. When off, the original
    /// behavior: clustering looks at whichever neighboring areas happen to be generated already, so the
    /// layout can differ depending on where the player goes first (chunks generate on worker threads in
    /// no fixed order). Point positions are the same either way; only which biome each point gets differs.
    /// </summary>
    [Tooltip("ON: the same seed and settings always give the same biome layout, no matter where the player goes first (recommended).\nOFF: the original behavior - biome clustering depends on which areas were generated first, so the layout can change between runs.\n\nSwitching this changes the biome layout of an existing world.")]
    [SerializeField] private bool orderIndependentBiomeLayout = true;

    [Header("Terrain Shape (Landforms)")]
    /// <summary>How each biome's landform is decided - see <see cref="global::TerrainShapeMode"/>.</summary>
    [Tooltip("Classic Only: every biome uses the original terrain; Landform settings are ignored.\nPer Biome: each biome uses its own Landform setting (biomes left on Classic keep the original terrain), so you can switch biomes over one at a time.\nLandforms Only: every biome uses a landform; biomes still on Classic get a suggested one based on their settings.")]
    [SerializeField] private TerrainShapeMode terrainShapeMode = TerrainShapeMode.PerBiome;

    /// <summary>Fraction of the typical distance between biome points over which a landform's relief fades out at a border.</summary>
    [Tooltip("How gradually a landform's relief (mountains, hills, dunes...) fades out toward a neighboring biome, as a fraction of the typical distance between biome points. Larger = long foothills; smaller = mountains stay tall closer to their edge. It is automatically widened so the fade itself stays walkable, but never beyond the biome's own blend band.")]
    [SerializeField][Range(0.05f, 1f)] private float landformTransitionWidth = 0.35f;

    /// <summary>0-1: how strongly Mountain/Plateau biomes gather along long belts.</summary>
    [Tooltip("How strongly Mountain and Plateau biomes are placed along long belts, so they form ranges that run across many chunks instead of scattered patches. Hills are favored next to the belts, Plains/Wetland/Dunes away from them. 0 = off. Only applies when landforms are in use.")]
    [SerializeField][Range(0f, 1f)] private float mountainBeltStrength = 0.5f;

    /// <summary>Belt spacing = VoronoiScale x this.</summary>
    [Tooltip("Spacing of mountain belts = Voronoi Scale x this. Larger = fewer, longer, more widely separated ranges.")]
    [SerializeField] private float mountainBeltScaleMultiplier = 6f;

    [Header("Volcanoes")]
    [Tooltip("Generate volcanoes and calderas: rare, very large landmarks (a cone or a collapsed caldera with a wide apron of lava plains) that reshape a big area around them. Rivers run down their flanks and lakes can form in calderas. A biome with Placement = Volcanic, if you have one, is painted over them.")]
    [SerializeField] private bool enableVolcanoes = true;
    [Tooltip("Size (world units) of the grid volcanoes are placed on - at most one per cell. Larger = rarer volcanoes, further apart.")]
    [SerializeField] private float volcanoSpacing = 5000f;
    [Tooltip("Chance a grid cell gets a volcano. With the default spacing, 0.35 means roughly one volcano every 8 km.")]
    [SerializeField][Range(0f, 1f)] private float volcanoChance = 0.35f;
    [Tooltip("Smallest volcano radius (world units) - the main cone; its lava apron reaches about twice as far.")]
    [SerializeField] private float volcanoMinRadius = 450f;
    [Tooltip("Largest volcano radius (world units).")]
    [SerializeField] private float volcanoMaxRadius = 1000f;
    [Tooltip("Lowest summit/rim height above the surrounding land.")]
    [SerializeField] private float volcanoMinHeight = 110f;
    [Tooltip("Highest summit/rim height above the surrounding land.")]
    [SerializeField] private float volcanoMaxHeight = 230f;
    [Tooltip("Chance a volcano is a caldera (a wide collapsed crater with steep stepped walls, a flat floor and sometimes a young cone inside) instead of a cone with a summit crater.")]
    [SerializeField][Range(0f, 1f)] private float calderaChance = 0.4f;

    [Header("Water")]
    /// <summary>
    /// Master toggle for oceans, lakes, ponds and rivers. When off, terrain generates exactly as it did
    /// before water existed - no coast shaping, no carving, no water geometry, zero extra cost.
    /// </summary>
    [Tooltip("Master toggle for oceans, lakes, ponds and rivers. Off = terrain generates exactly as before water existed.")]
    [SerializeField] private bool enableWater = true;

    /// <summary>
    /// Sea level (world Y). Only oceans use it - lakes and ponds each get their own level from the terrain
    /// around them, and rivers follow their own downhill surface. Terrain being below this level does NOT
    /// by itself make water: oceans are decided by the continent field (see Ocean settings).
    /// </summary>
    [Tooltip("Sea level (world Y), used by oceans only. Lakes/ponds/rivers each have their own water level. Being below this height does not by itself create water - oceans come from the continent field.")]
    [SerializeField] private float waterLevel = 0f;

    [Tooltip("Default water material for every water type that doesn't have its own below. Leave empty to use simple built-in transparent fallbacks (tinted per type).")]
    [SerializeField] private Material waterMaterial;
    [Tooltip("Optional material for oceans (falls back to the default water material).")]
    [SerializeField] private Material oceanMaterial;
    [Tooltip("Optional material for lakes (falls back to the default water material).")]
    [SerializeField] private Material lakeMaterial;
    [Tooltip("Optional material for ponds (falls back to the lake material, then the default).")]
    [SerializeField] private Material pondMaterial;
    [Tooltip("Optional material for rivers (falls back to the default water material).")]
    [SerializeField] private Material riverMaterial;
    [Tooltip("Optional material for waterfalls - the steep sheets of water where a river drops (falls back to the river material, then the default).")]
    [SerializeField] private Material waterfallMaterial;

    [Tooltip("Whether water bodies get a trigger volume that drives OxygenManager.SetUnderwater for anything with an OxygenManager (player, mobs, etc). Water rendering/geometry is unaffected either way.")]
    [SerializeField] private bool enableSwimDetection = true;

    [Header("Water - Oceans")]
    [Tooltip("Generate oceans. Oceans are large-scale geographic features from a low-frequency continent field, not every low area.")]
    [SerializeField] private bool enableOceans = true;
    [Tooltip("Continent field scale = VoronoiScale * this multiplier. Larger = bigger continents and oceans, further apart.")]
    [SerializeField] private float continentScaleMultiplier = 18f;
    [Tooltip("Where the continent field turns into ocean. Lower = rarer oceans (-0.2 is roughly 15% of the world).")]
    [SerializeField][Range(-0.8f, 0.8f)] private float oceanThreshold = -0.2f;
    [Tooltip("Width (world units, approximate) of the beach that rises from sea level.")]
    [SerializeField] private float beachWidth = 30f;
    [Tooltip("Height of the beach above sea level before inland terrain takes over.")]
    [SerializeField] private float beachHeight = 2.5f;
    [Tooltip("Width (world units, approximate) over which coastal terrain blends into normal inland terrain.")]
    [SerializeField] private float coastBlendWidth = 140f;
    [Tooltip("Width (world units, approximate) of the continental shelf between the shore and the deep seafloor.")]
    [SerializeField] private float continentalShelfWidth = 260f;
    [Tooltip("Depth of the open-ocean seafloor below sea level.")]
    [SerializeField] private float oceanDepth = 35f;
    [Tooltip("How much land gradually rises moving inland from the coast. Gives rivers a natural tendency to drain toward the sea.")]
    [SerializeField] private float inlandRise = 40f;
    [Tooltip("Distance (world units, approximate) over which the inland rise builds up.")]
    [SerializeField] private float inlandRiseDistance = 3000f;
    [Tooltip("How often islands rise out of open ocean. 0 = none.")]
    [SerializeField][Range(0f, 1f)] private float islandFrequency = 0.3f;
    [Tooltip("Island noise scale = VoronoiScale * this multiplier. Larger = bigger, fewer islands.")]
    [SerializeField] private float islandScaleMultiplier = 1.4f;
    [Tooltip("Peak height of islands above sea level.")]
    [SerializeField] private float islandPeakHeight = 14f;
    [Tooltip("Radius (world units) around the world origin kept on land, so the player never spawns at sea. 0 disables.")]
    [SerializeField] private float spawnLandRadius = 700f;

    [Header("Water - Coasts")]
    [Tooltip("How much of the coastline is cliffs (0 = all beaches). Coasts alternate naturally between beaches, rocky shores and cliffs along their length.")]
    [SerializeField][Range(0f, 1f)] private float coastCliffFrequency = 0.35f;
    [Tooltip("Height of the tallest sea cliffs (world units). Cliff height varies along the coast up to this.")]
    [SerializeField] private float coastCliffHeight = 26f;
    [Tooltip("How often cliffs are stepped into terraces (up to three faces with ledges between them) instead of one sheer face.")]
    [SerializeField][Range(0f, 1f)] private float coastCliffTerraces = 0.5f;
    [Tooltip("Chance of sea stacks (isolated rock towers in the sea) off a stretch of cliff coast. 0 = none.")]
    [SerializeField][Range(0f, 1f)] private float seaStackChance = 0.3f;
    [Tooltip("Grid size (world units) sea stacks are placed on - at most one small group per cell. Larger = rarer.")]
    [SerializeField] private float seaStackSpacing = 220f;
    [Tooltip("Tallest a sea stack can rise above the water (world units).")]
    [SerializeField] private float seaStackMaxHeight = 30f;

    [Header("Water - Lakes")]
    [Tooltip("Generate lakes: inland bodies of water sitting in a basin, with their own water level.")]
    [SerializeField] private bool enableLakes = true;
    [Tooltip("Size (world units) of the grid cells lakes are placed on - at most one lake per cell. Larger = sparser lakes.")]
    [SerializeField] private float lakeSpacing = 900f;
    [Tooltip("Base chance a cell gets a lake (then scaled by the site's biome Lake Likelihood and by how much the site is a natural depression).")]
    [SerializeField][Range(0f, 1f)] private float lakeChance = 0.35f;
    [Tooltip("Smallest lake radius (world units).")]
    [SerializeField] private float lakeMinRadius = 45f;
    [Tooltip("Largest lake radius (world units).")]
    [SerializeField] private float lakeMaxRadius = 130f;
    [Tooltip("Water depth at the center of the largest lakes (smaller lakes are proportionally shallower).")]
    [SerializeField] private float lakeMaxDepth = 10f;
    [Tooltip("Steepest average ground slope (rise/run) a lake can be placed on - lakes don't sit on mountainsides.")]
    [SerializeField] private float lakeMaxSiteSlope = 0.3f;
    [Tooltip("Chance a lake drains through an outlet river at the lowest point of its rim.")]
    [SerializeField][Range(0f, 1f)] private float lakeOutletChance = 0.5f;

    [Header("Water - Ponds")]
    [Tooltip("Generate ponds: small, shallow bodies of water in minor depressions.")]
    [SerializeField] private bool enablePonds = true;
    [Tooltip("Size (world units) of the grid cells ponds are placed on - at most one pond per cell.")]
    [SerializeField] private float pondSpacing = 220f;
    [Tooltip("Base chance a cell gets a pond (then scaled by the site's biome Pond Likelihood and depression shape).")]
    [SerializeField][Range(0f, 1f)] private float pondChance = 0.25f;
    [Tooltip("Smallest pond radius (world units).")]
    [SerializeField] private float pondMinRadius = 8f;
    [Tooltip("Largest pond radius (world units).")]
    [SerializeField] private float pondMaxRadius = 22f;
    [Tooltip("Typical water depth at a pond's center.")]
    [SerializeField] private float pondDepth = 2f;
    [Tooltip("Steepest average ground slope (rise/run) a pond can be placed on.")]
    [SerializeField] private float pondMaxSiteSlope = 0.45f;

    [Header("Water - Shorelines")]
    [Tooltip("Width (world units) of the band around a lake/pond where the terrain is guaranteed to stay above its water, so the shoreline is always closed. Automatically at least 1.5 terrain mesh vertices at the current Level Of Detail.")]
    [SerializeField] private float shoreRimWidth = 12f;
    [Tooltip("How far a lake/pond's rim stays above its water level.")]
    [SerializeField] private float shoreFreeboard = 0.6f;

    [Header("Water - Rivers")]
    [Tooltip("Generate rivers: traced paths from springs (and lake outlets) downhill to the ocean or a lake.")]
    [SerializeField] private bool enableRivers = true;
    [Tooltip("Size (world units) of the grid cells river springs are placed on - at most one spring per cell.")]
    [SerializeField] private float riverSpacing = 1000f;
    [Tooltip("Base chance a cell gets a spring (then scaled by the biome's River Spring Likelihood and the spring's elevation).")]
    [SerializeField][Range(0f, 1f)] private float riverChance = 0.35f;
    [Tooltip("Springs only appear at least this high above sea level.")]
    [SerializeField] private float riverMinSpringElevation = 10f;
    [Tooltip("Rivers from springs shorter than this are discarded.")]
    [SerializeField] private float riverMinLength = 350f;
    [Tooltip("Longest a river can be traced. Also how far away a chunk has to look for rivers that might reach it.")]
    [SerializeField] private float riverMaxLength = 2600f;
    [Tooltip("River width (world units) at its source.")]
    [SerializeField] private float riverSourceWidth = 5f;
    [Tooltip("River width (world units) at its mouth.")]
    [SerializeField] private float riverMouthWidth = 26f;
    [Tooltip("How much river width wobbles along its course (0 = smooth taper from source to mouth).")]
    [SerializeField][Range(0f, 0.9f)] private float riverWidthVariation = 0.35f;
    [Tooltip("How strongly rivers meander away from the straight downhill direction.")]
    [SerializeField][Range(0f, 1f)] private float riverMeander = 0.55f;
    [Tooltip("Typical length (world units) of one meander bend.")]
    [SerializeField] private float riverMeanderWavelength = 180f;
    [Tooltip("Water depth at the river's mouth (shallower toward the source).")]
    [SerializeField] private float riverDepth = 3f;
    [Tooltip("Steepness (degrees) of the valley walls a river carves. Higher = narrower, steeper valleys and gorges.")]
    [SerializeField][Range(5f, 80f)] private float riverValleySlope = 28f;
    [Tooltip("Largest distance (world units) from a river's center a valley can extend.")]
    [SerializeField] private float riverMaxValleyWidth = 150f;
    [Tooltip("How far river banks stay above the river's water.")]
    [SerializeField] private float riverBankFreeboard = 0.8f;

    [Header("Water - Waterfalls")]
    [Tooltip("Where a river drops steeply (over a cliff, off a volcano or plateau, down into a valley), turn the drop into a real waterfall: a flat pool, a rock lip, a sheer fall and a plunge pool, instead of steep rapids.")]
    [SerializeField] private bool enableWaterfalls = true;
    [Tooltip("Where a smaller river meets a bigger one, it flows into it: it ends at the confluence with its water stepping down to the bigger river's level (a small waterfall if the drop is steep). Off = rivers are independent and run alongside each other at their own levels.")]
    [SerializeField] private bool enableRiverJunctions = true;
    [Tooltip("Tidies tight river bends: where a river bends back so tightly that it would run right beside (or over) its own earlier stretch, it cuts through the neck of the bend instead, like real rivers do; and at sharp corners the water is levelled before the corner rather than dropping around it. Stops water standing against ground a lower stretch carved away.")]
    [SerializeField] private bool enableMeanderCutoffs = true;

    [Header("Water - Wetness & Snowmelt")]
    [Tooltip("How far (world units) from rivers, lakes and the sea the ground counts as wet, fading out. Wetness goes to the terrain mesh's vertex color (red) and a per-chunk _WetnessMap texture, for darker, glossier ground near water in your terrain shader.")]
    [SerializeField] private float wetnessDistance = 14f;
    [Tooltip("How far above the nearest water's level the ground can still be wet. Low = only low banks and beaches get wet, not cliff tops beside the water.")]
    [SerializeField] private float wetnessHeight = 4f;
    [Tooltip("Height above sea level where snow starts. Ground above it feeds snowmelt springs (see Snowmelt Springs).")]
    [SerializeField] private float snowLineHeight = 90f;
    [Tooltip("How much more likely river springs are above the snow line (snowmelt feeding streams). 0 = no effect; 1 = up to twice as likely high up; 3 = up to four times.")]
    [SerializeField][Range(0f, 3f)] private float snowmeltSprings = 1f;
    [Tooltip("Smallest drop (world units) that becomes a waterfall.")]
    [SerializeField] private float waterfallMinDrop = 4f;
    [Tooltip("Tallest single fall; bigger drops become several falls with pools between them (a multi-tier waterfall).")]
    [SerializeField] private float waterfallTierHeight = 12f;

    [Header("Erosion")]
    /// <summary>
    /// Master toggle for the erosion post-process (thermal + hydraulic/water erosion).
    /// </summary>
    [Tooltip("Enable thermal and hydraulic (water) erosion post-processing on generated heightmaps.")]
    [SerializeField] private bool enableErosion = true;

    /// <summary>
    /// Extra heightmap cells generated around each chunk purely to give erosion the neighboring
    /// context it needs, then discarded. Should comfortably exceed how far a droplet can travel
    /// (roughly dropletLifetime cells) so neighboring chunks erode consistently near their shared edge.
    /// </summary>
    [Tooltip("Padding (in cells) generated around each chunk for erosion context, then cropped away. Should exceed the droplet's typical travel distance (roughly dropletLifetime cells).")]
    [SerializeField] private int erosionPadding = 40;

    /// <summary>
    /// Erodes fixed world-space tiles (cached and shared between chunks) and crossfades them at tile
    /// borders, so neighboring chunks always agree exactly on their shared edge - see <see cref="ErosionTiles"/>.
    /// </summary>
    [Tooltip("Erode fixed world tiles shared between chunks and crossfade them at tile borders, so neighboring chunks always match exactly along their edges (no cracks or steps between chunks). Costs extra work for tiles just beyond the loaded area. Off = each chunk erodes on its own (the original behavior; small seams possible).")]
    [SerializeField] private bool seamlessErosion = true;

    /// <summary>
    /// Number of thermal erosion relaxation passes. Higher values produce smoother, more settled slopes.
    /// </summary>
    [Tooltip("Number of thermal erosion passes. Higher = smoother, more settled slopes.")]
    [SerializeField] private int thermalIterations = 5;

    /// <summary>
    /// Base slope angle (degrees) above which material starts sliding downhill. Scaled per-cell by biome erosion resistance.
    /// </summary>
    [Tooltip("Base slope angle (degrees) above which material starts sliding downhill.")]
    [SerializeField][Range(1f, 89f)] private float talusAngle = 33f;

    /// <summary>
    /// Fraction of excess slope height moved per thermal erosion pass.
    /// </summary>
    [Tooltip("Fraction of excess slope height moved per thermal erosion pass.")]
    [SerializeField][Range(0f, 1f)] private float thermalErosionRate = 0.5f;

    /// <summary>
    /// Average number of water droplets simulated per heightmap cell. 0 disables hydraulic erosion.
    /// </summary>
    [Tooltip("Average number of water droplets simulated per heightmap cell. 0 disables hydraulic (water) erosion.")]
    [SerializeField] private float hydraulicDropletDensity = 0.12f;

    /// <summary>
    /// Maximum number of simulation steps a single water droplet can take before it evaporates.
    /// </summary>
    [Tooltip("Maximum number of steps a single water droplet can take before evaporating.")]
    [SerializeField] private int dropletLifetime = 30;

    /// <summary>
    /// How much a droplet keeps its previous direction versus following the slope. Higher values carve straighter channels.
    /// </summary>
    [Tooltip("How much a droplet keeps its previous direction versus following the slope. Higher = straighter channels.")]
    [SerializeField][Range(0f, 1f)] private float dropletInertia = 0.05f;

    /// <summary>
    /// Multiplier controlling how much sediment a droplet can carry relative to its speed and water volume.
    /// </summary>
    [Tooltip("How much sediment a droplet can carry relative to its speed and water volume.")]
    [SerializeField] private float sedimentCapacityFactor = 4f;

    /// <summary>
    /// Minimum sediment capacity, preventing near-zero-slope droplets from carrying (and thus eroding) nothing at all.
    /// </summary>
    [Tooltip("Minimum sediment a droplet can carry, even on nearly flat ground.")]
    [SerializeField] private float minSedimentCapacity = 0.01f;

    /// <summary>
    /// How quickly a droplet erodes terrain when it has spare carrying capacity.
    /// </summary>
    [Tooltip("How quickly a droplet erodes terrain when it has spare carrying capacity.")]
    [SerializeField][Range(0f, 1f)] private float erodeSpeed = 0.3f;

    /// <summary>
    /// How quickly a droplet deposits sediment once it's over capacity.
    /// </summary>
    [Tooltip("How quickly a droplet deposits sediment once it's over capacity.")]
    [SerializeField][Range(0f, 1f)] private float depositSpeed = 0.3f;

    /// <summary>
    /// Fraction of a droplet's water lost per step. Higher values make droplets die out sooner.
    /// </summary>
    [Tooltip("Fraction of a droplet's water lost per step. Higher = droplets die out sooner.")]
    [SerializeField][Range(0f, 1f)] private float evaporateSpeed = 0.02f;

    /// <summary>
    /// Gravity constant used to accelerate droplets on downhill slopes.
    /// </summary>
    [Tooltip("Gravity constant used to accelerate droplets on downhill slopes.")]
    [SerializeField] private float erosionGravity = 4f;

    /// <summary>
    /// Radius (in cells) of the brush used to erode/deposit terrain around a droplet.
    /// </summary>
    [Tooltip("Radius (in cells) of the brush used to erode/deposit terrain around a droplet.")]
    [SerializeField][Range(1f, 6f)] private float erosionRadius = 3f;

    [Header("Erosion Debug Visualization")]
    /// <summary>
    /// Master toggle for the erosion debug tool: draws Scene-view gizmos over generated terrain marking
    /// exactly where thermal/hydraulic erosion removed material (red/orange) or deposited it (blue/cyan),
    /// so you can visually confirm erosion is actually running and see its shape/strength at a glance.
    /// Has no effect unless <see cref="EnableErosion"/> is also on. Adds a small amount of extra work per
    /// chunk (capturing pre-erosion heights) and Scene-view draw cost, so leave it off outside of tuning.
    /// </summary>
    [Tooltip("Draw Scene-view gizmos showing exactly where erosion removed material (red/orange) or deposited it (blue/cyan). Enable this to visually verify erosion is working. No effect unless 'Enable Erosion' above is also on.")]
    [SerializeField] private bool visualizeErosionDebug = false;

    /// <summary>
    /// Minimum |height delta| (world units) a cell must have been changed by erosion before it gets a
    /// gizmo at all. Filters out imperceptible noise so the view isn't cluttered with near-zero changes.
    /// </summary>
    [Tooltip("Minimum height change (world units) before a cell gets an erosion gizmo. Filters out imperceptible noise.")]
    [SerializeField] private float erosionDebugMinDelta = 0.05f;

    /// <summary>
    /// Height delta (world units) that maps to full gizmo size/color intensity. Cells changed by more
    /// than this are clamped to the strongest color, not drawn larger.
    /// </summary>
    [Tooltip("Height change (world units) that maps to the strongest gizmo color/size. Lower this if your erosion looks subtle and the gizmos all appear pale/small; raise it if everything looks maxed-out red/blue.")]
    [SerializeField] private float erosionDebugMaxDelta = 1.5f;

    /// <summary>
    /// Draws one gizmo every N heightmap cells instead of every cell. Higher values are much cheaper to
    /// draw (fewer gizmos) at the cost of a coarser-looking overlay. Keep well above 1 for anything but
    /// small chunk sizes - drawing every single cell can make the Scene view very slow.
    /// </summary>
    [Tooltip("Draw one gizmo every N heightmap cells instead of every cell. Higher = cheaper to draw but coarser overlay. Keep well above 1 for larger chunk sizes.")]
    [SerializeField][Range(1, 16)] private int erosionDebugStride = 4;

    /// <summary>
    /// Base size (world units) of each erosion gizmo cube before the delta-based size scaling is applied.
    /// </summary>
    [Tooltip("Base size (world units) of each erosion gizmo cube.")]
    [SerializeField][Range(0.1f, 5f)] private float erosionDebugGizmoSize = 1f;

    /// <summary>
    /// Vertical offset (world units) applied above the terrain surface so gizmo cubes don't z-fight with
    /// (or get hidden inside) the terrain mesh they're marking.
    /// </summary>
    [Tooltip("Vertical offset (world units) so gizmo cubes float just above the terrain instead of z-fighting with it.")]
    [SerializeField] private float erosionDebugHeightOffset = 0.25f;

    /// <summary>
    /// Hard cap on how many gizmos a single chunk will draw, regardless of stride, as a safety net
    /// against the Scene view grinding to a halt on very large/densely-eroded chunks.
    /// </summary>
    [Tooltip("Safety cap on how many gizmos a single chunk will draw, regardless of stride.")]
    [SerializeField] private int erosionDebugMaxGizmosPerChunk = 4000;

    [Header("Other Configurations")]
    /// <summary>
    /// Level of detail for terrain generation, controlling mesh resolution.
    /// </summary>
    [Tooltip("Level of detail for terrain generation, controlling mesh resolution.")]
    [SerializeField][Range(0, 6)] private int levelOfDetail = 6;

    /// <summary>Distant chunks switch to coarser meshes (see <see cref="LodForDistance"/>).</summary>
    [Tooltip("Distance-based level of detail: chunks far from the viewer switch to coarser meshes (built in the background the first time each is needed), so a longer view distance costs far fewer triangles. Nearby chunks keep the Level Of Detail above; collision, NavMesh and object placement always use it. Thin 'skirts' under every chunk edge hide the cracks between chunks of different detail.")]
    [SerializeField] private bool distanceLod = true;
    [Tooltip("Chunks whose nearest edge is within this distance (world units) of the viewer use the full Level Of Detail.")]
    [SerializeField] private float lodFullDetailDistance = 300f;
    [Tooltip("Beyond the full-detail distance, a chunk's mesh gets one level coarser every this many world units.")]
    [SerializeField] private float lodDistanceStep = 300f;
    [Tooltip("The coarsest level distant chunks use (same scale as Level Of Detail: 0 = every cell, 1 = every 2nd, 2 = every 4th ... 6 = every 12th). No effect if Level Of Detail is already this coarse.")]
    [SerializeField][Range(0, 6)] private int lodMaxLevel = 4;
    [Tooltip("Extra depth (world units) of the skirts hung under chunk edges, on top of what the terrain's shape needs. Raise it if you ever see cracks between chunks of different detail.")]
    [SerializeField] private float lodSkirtDepth = 2f;

    [Header("Biomes")]
    /// <summary>
    /// Definitions for biomes used in terrain generation.
    /// </summary>
    [Tooltip("Definitions for biomes used in terrain generation.")]
    [SerializeField] private BiomeInstance[] biomeDefinitions;

    [Header("Objects")]
    /// <summary>
    /// Determines if objects (e.g., trees, rocks) should be spawned on the terrain.
    /// </summary>
    [Tooltip("Determines if objects (e.g., trees, rocks) should be spawned on the terrain.")]
    [SerializeField] private bool shouldSpawnObjects = true;

    /// <summary>
    /// Base frequency for clustering spawned objects.
    /// </summary>
    [Tooltip("Base frequency for clustering spawned objects.")]
    [SerializeField] private float clusterBaseFrequency = 1f;

    /// <summary>
    /// Amplitude for object clustering, affecting density variations.
    /// </summary>
    [Tooltip("Amplitude for object clustering, affecting density variations.")]
    [SerializeField] private float clusterAmplitude = 1f;

    // Thread-safe queues
    private Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue;
    private Queue<MapThreadInfo<DataStructure.TerrainData>> terrainDataThreadInfoQueue;
    private Queue<MapThreadInfo<BiomeObjectData>> biomeObjectDataThreadInfoQueue;

    // Guards minHeight/maxHeight, which UpdateMinMaxHeight below mutates from multiple
    // concurrent per-chunk worker threads (all sharing this one TerrainGenerator instance).
    private readonly object heightRangeLock = new object();

    /// <summary>
    /// Updates the minimum and maximum height values based on the given height.
    /// Thread-safe: called once per heightmap cell from worker threads in HeightGenerator,
    /// potentially from several chunks generating concurrently.
    /// </summary>
    /// <param name="height">The height to evaluate.</param>
    public void UpdateMinMaxHeight(float height)
    {
        lock (heightRangeLock)
        {
            if (height > maxHeight) maxHeight = height;
            if (height < minHeight) minHeight = height;
        }
    }

    /// <summary>
    /// Constructor for the TerrainGenerator class.
    /// </summary>
    /// <param name="amplitude">Amplitude for noise generation.</param>
    /// <param name="lacunarity">Lacunarity for noise generation.</param>
    public TerrainGenerator(float amplitude = 100f, float lacunarity = 2f)
    {
        this.lacunarity = lacunarity;
    }

    /// <summary>
    /// Initializes the system by setting up thread-safe queues, Voronoi cache, and precomputing density maps for biome objects.
    /// </summary>
    private void Awake()
    {
        // Reset the (static, process-lifetime) Voronoi point cache before generating anything.
        // Without this, a chunk coordinate visited in a previous Play session (or before a
        // seed/scale/biome-list change) keeps serving its OLD cached points forever - the cache
        // is keyed only by chunk coordinate, not by seed, so nothing else invalidates it. This is
        // especially easy to hit with "Reload Domain" disabled in Enter Play Mode Settings, where
        // static fields like this one otherwise survive between separate Play sessions.
        VoronoiBiomeGenerator.ClearCache();
        // Same reasoning for the globally cached lakes, ponds and river paths.
        WaterGenerator.ClearCaches();

        // Initialize thread-safe queues for handling map data, terrain data, and biome object data from worker threads.
        mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
        terrainDataThreadInfoQueue = new Queue<MapThreadInfo<DataStructure.TerrainData>>();
        biomeObjectDataThreadInfoQueue = new Queue<MapThreadInfo<BiomeObjectData>>();

        // Precompute density maps for each biome object in the biome definitions.
        foreach (BiomeInstance biomeInstance in biomeDefinitions)
        {
            foreach (BiomeObject biomeObject in biomeInstance.runtimeObjects)
            {
                // If the density map for a biome object is not already computed, generate one.
                if (biomeObject.densityMap == null)
                {
                    biomeObject.densityMap = ObjectSpawner.GenerateClusteredDensityMap(
                        ChunkSize, ChunkSize,                          // Dimensions of the density map
                        biomeObject.clusterCount, biomeObject.clusterRadius,  // Cluster properties
                        clusterBaseFrequency, clusterAmplitude, scaleFactor // Frequency, amplitude, and scale for clustering
                    );
                }
            }
        }
    }
}
