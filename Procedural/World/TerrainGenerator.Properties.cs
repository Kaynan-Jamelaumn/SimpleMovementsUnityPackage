using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Linq;
using static DataStructure;

// TerrainGenerator, part 2 of 4: read-only access to the settings (see TerrainGenerator.cs, which holds the serialized fields).
public partial class TerrainGenerator : MonoBehaviour
{
    // Properties
    public float Lacunarity => lacunarity;
    public int Octaves => octaves;
    public BiomeInstance[] BiomeDefinitions { get => biomeDefinitions; set => biomeDefinitions = value; }
    public bool TerrainTextureBasedOnVoronoiPoints => terrainTextureBasedOnVoronoiPoints;
    public Texture2D DefaultTexture { get => defaultTexture; set => defaultTexture = value; }
    public ComputeShader SplatMapShader { get => splatMapShader; set => splatMapShader = value; }
    public float ScaleFactor { get => scaleFactor; set => scaleFactor = value; }
    public float MinHeight { get => minHeight; set => minHeight = value; }
    public float MaxHeight { get => maxHeight; set => maxHeight = value; }
    public int LevelOfDetail { get => levelOfDetail; set => levelOfDetail = value; }
    public bool DistanceLod => distanceLod;
    public float LodFullDetailDistance => Mathf.Max(0f, lodFullDetailDistance);
    public float LodDistanceStep => Mathf.Max(1f, lodDistanceStep);
    public int LodMaxLevel => Mathf.Clamp(lodMaxLevel, 0, 6);
    public float LodSkirtDepth => Mathf.Max(0f, lodSkirtDepth);

    /// <summary>
    /// The mesh level of detail for a chunk whose nearest edge is <paramref name="distance"/> world units from the
    /// viewer: <see cref="LevelOfDetail"/> up close, one level coarser every <see cref="LodDistanceStep"/> beyond
    /// <see cref="LodFullDetailDistance"/>, up to <see cref="LodMaxLevel"/>. Always LevelOfDetail when distance LOD is off.
    /// </summary>
    public int LodForDistance(float distance)
    {
        if (!distanceLod || distance <= LodFullDetailDistance || levelOfDetail >= LodMaxLevel)
            return levelOfDetail;
        int extra = 1 + Mathf.FloorToInt((distance - LodFullDetailDistance) / LodDistanceStep);
        return Mathf.Min(LodMaxLevel, levelOfDetail + extra);
    }
    public TerrainSize TerrainSizeValue { get => terrainSize; set => terrainSize = value; }

    // Texture Variation Properties (only active when enableTextureVariations is true)
    public bool EnableTextureVariations => enableTextureVariations;
    public bool EnableUVRotation => enableTextureVariations && enableUVRotation;
    public bool EnableUVNoise => enableTextureVariations && enableUVNoise;
    public float UVNoiseStrength => uvNoiseStrength;
    public float UVNoiseScale => uvNoiseScale;
    public bool EnableTextureScaleVariation => enableTextureVariations && enableTextureScaleVariation;
    public float TextureScaleVariationRange => textureScaleVariationRange;
    public bool EnableShaderEnhancements => enableTextureVariations && enableShaderEnhancements;
    public float ShaderUVRotationStrength => shaderUVRotationStrength;
    public float ShaderUVScaleVariation => shaderUVScaleVariation;
    public float ShaderTextureBlendSharpness => shaderTextureBlendSharpness;

    // Natural Biome Placement Properties
    public float BiomeClusterStrength => biomeClusterStrength;
    public float BiomeRepeatPenalty => biomeRepeatPenalty;
    // Both derived scales are multiples of VoronoiScale so they stay correctly proportioned to the
    // biome cell size regardless of what VoronoiScale is configured to - see the fields' tooltips.
    public float BiomeClusterRadius => VoronoiScale * biomeClusterRadiusMultiplier;
    public bool UseNaturalClimatePlacement => useNaturalClimatePlacement;
    public float ClimateNoiseScale => VoronoiScale * climateScaleMultiplier;
    public bool TerrainAwareClimate => terrainAwareClimate;
    public float PrevailingWindAngle => prevailingWindAngle;
    public float RainShadowStrength => rainShadowStrength;
    public float AltitudeCooling => altitudeCooling;
    public float CoastalMoisture => coastalMoisture;
    /// <summary>The terrain's influence on the climate (see <see cref="TerrainClimate"/>), or null when off.</summary>
    public TerrainClimate TerrainClimate => TerrainClimate.From(this);
    public float VoronoiWarpStrength => voronoiWarpStrength;
    public float VoronoiWarpScale => VoronoiScale * voronoiWarpScaleMultiplier;
    public float BiomeBlendRange => biomeBlendRange;
    public bool UseBiomeBlendedTexturing => useBiomeBlendedTexturing;
    public int SplatTexturesPerPixel => Mathf.Clamp(splatTexturesPerPixel, 2, 4);
    public float BiomeBoundaryMaxSlopeDegrees => biomeBoundaryMaxSlopeDegrees;
    // tan() of BiomeBoundaryMaxSlopeDegrees, precomputed once per access rather than per heightmap cell.
    // <= 0 (from a 0 degrees setting) means "disabled" to callers, same convention as the degrees field.
    public float BiomeBoundaryMaxSlopeTangent => biomeBoundaryMaxSlopeDegrees > 0f
        ? Mathf.Tan(biomeBoundaryMaxSlopeDegrees * Mathf.Deg2Rad)
        : 0f;

    public bool OrderIndependentBiomeLayout => orderIndependentBiomeLayout;

    // Terrain Shape (Landforms) Properties
    public TerrainShapeMode TerrainShapeMode => terrainShapeMode;
    public float LandformTransitionWidth => landformTransitionWidth;
    public float MountainBeltStrength => mountainBeltStrength;
    public float MountainBeltScale => VoronoiScale * Mathf.Max(0.5f, mountainBeltScaleMultiplier);

    // Volcano Properties
    public bool EnableVolcanoes => enableVolcanoes;
    public float VolcanoSpacing => volcanoSpacing;
    public float VolcanoChance => volcanoChance;
    public float VolcanoMinRadius => volcanoMinRadius;
    public float VolcanoMaxRadius => volcanoMaxRadius;
    public float VolcanoMinHeight => volcanoMinHeight;
    public float VolcanoMaxHeight => volcanoMaxHeight;
    public float CalderaChance => calderaChance;

    /// <summary>
    /// Biome layout options passed to every <see cref="VoronoiBiomeGenerator"/> query. Null (the original
    /// behavior) when none of order-independent layout, landform placement or terrain climate is in use.
    /// </summary>
    public VoronoiBiomeGenerator.LayoutOptions BiomeLayout
    {
        get
        {
            bool landforms = terrainShapeMode != TerrainShapeMode.ClassicOnly;
            bool belts = landforms && mountainBeltStrength > 0f;
            TerrainClimate climate = useNaturalClimatePlacement ? TerrainClimate : null;
            if (!orderIndependentBiomeLayout && !landforms && climate == null)
                return null;
            return new VoronoiBiomeGenerator.LayoutOptions
            {
                OrderIndependent = orderIndependentBiomeLayout,
                ShapeMode = terrainShapeMode,
                BeltStrength = belts ? mountainBeltStrength : 0f,
                BeltScale = MountainBeltScale,
                NearbyReach = landforms ? LandformSettings.NearbyReachFor(this) : 0f,
                Climate = climate,
            };
        }
    }

    // Water Properties
    public bool EnableWater => enableWater;
    public float SeaLevel => waterLevel;
    public bool EnableSwimDetection => enableSwimDetection;

    /// <summary>
    /// Material for a water type: its own material if assigned, else (for ponds) the lake material, else
    /// the default water material. Null means "use the built-in fallback".
    /// </summary>
    public Material GetWaterMaterial(WaterBodyType type)
    {
        Material specific = null;
        switch (type)
        {
            case WaterBodyType.Ocean: specific = oceanMaterial; break;
            case WaterBodyType.Lake: specific = lakeMaterial; break;
            case WaterBodyType.Pond: specific = pondMaterial != null ? pondMaterial : lakeMaterial; break;
            case WaterBodyType.River: specific = riverMaterial; break;
            case WaterBodyType.Waterfall: specific = waterfallMaterial != null ? waterfallMaterial : riverMaterial; break;
        }
        return specific != null ? specific : waterMaterial;
    }

    public bool EnableOceans => enableWater && enableOceans;
    // Derived from VoronoiScale like ClimateNoiseScale/VoronoiWarpScale, so it stays proportioned to biome size.
    public float ContinentScale => VoronoiScale * continentScaleMultiplier;
    public float OceanThreshold => oceanThreshold;
    public float BeachWidth => beachWidth;
    public float BeachHeight => beachHeight;
    public float CoastBlendWidth => coastBlendWidth;
    public float ContinentalShelfWidth => continentalShelfWidth;
    public float OceanDepth => oceanDepth;
    public float InlandRise => inlandRise;
    public float InlandRiseDistance => inlandRiseDistance;
    public float IslandFrequency => islandFrequency;
    public float IslandScale => VoronoiScale * islandScaleMultiplier;
    public float IslandPeakHeight => islandPeakHeight;
    public float SpawnLandRadius => spawnLandRadius;
    public float CoastCliffFrequency => coastCliffFrequency;
    public float CoastCliffHeight => coastCliffHeight;
    public float CoastCliffTerraces => coastCliffTerraces;
    public float SeaStackChance => seaStackChance;
    public float SeaStackSpacing => seaStackSpacing;
    public float SeaStackMaxHeight => seaStackMaxHeight;

    public bool EnableLakes => enableWater && enableLakes;
    public float LakeSpacing => lakeSpacing;
    public float LakeChance => lakeChance;
    public float LakeMinRadius => lakeMinRadius;
    public float LakeMaxRadius => lakeMaxRadius;
    public float LakeMaxDepth => lakeMaxDepth;
    public float LakeMaxSiteSlope => lakeMaxSiteSlope;
    public float LakeOutletChance => lakeOutletChance;

    public bool EnablePonds => enableWater && enablePonds;
    public float PondSpacing => pondSpacing;
    public float PondChance => pondChance;
    public float PondMinRadius => pondMinRadius;
    public float PondMaxRadius => pondMaxRadius;
    public float PondDepth => pondDepth;
    public float PondMaxSiteSlope => pondMaxSiteSlope;

    public float ShoreRimWidth => shoreRimWidth;
    public float ShoreFreeboard => shoreFreeboard;

    public bool EnableRivers => enableWater && enableRivers;
    public float RiverSpacing => riverSpacing;
    public float RiverChance => riverChance;
    public float RiverMinSpringElevation => riverMinSpringElevation;
    public float RiverMinLength => riverMinLength;
    public float RiverMaxLength => riverMaxLength;
    public float RiverSourceWidth => riverSourceWidth;
    public float RiverMouthWidth => riverMouthWidth;
    public float RiverWidthVariation => riverWidthVariation;
    public float RiverMeander => riverMeander;
    public float RiverMeanderWavelength => riverMeanderWavelength;
    public float RiverDepth => riverDepth;
    public float RiverValleySlope => riverValleySlope;
    public float RiverMaxValleyWidth => riverMaxValleyWidth;
    public float RiverBankFreeboard => riverBankFreeboard;
    public bool EnableWaterfalls => enableWaterfalls;
    public bool EnableRiverJunctions => enableRiverJunctions;
    public bool EnableMeanderCutoffs => enableMeanderCutoffs;
    public float WetnessDistance => wetnessDistance;
    public float WetnessHeight => wetnessHeight;
    public float SnowLineHeight => snowLineHeight;
    public float SnowmeltSprings => snowmeltSprings;
    public float WaterfallMinDrop => waterfallMinDrop;
    public float WaterfallTierHeight => waterfallTierHeight;

    // Erosion Properties
    public bool EnableErosion => enableErosion;
    public int ErosionPadding => erosionPadding;
    public bool SeamlessErosion => seamlessErosion;
    public int ThermalIterations => thermalIterations;
    public float TalusAngle => talusAngle;
    public float ThermalErosionRate => thermalErosionRate;
    public float HydraulicDropletDensity => hydraulicDropletDensity;
    public int DropletLifetime => dropletLifetime;
    public float DropletInertia => dropletInertia;
    public float SedimentCapacityFactor => sedimentCapacityFactor;
    public float MinSedimentCapacity => minSedimentCapacity;
    public float ErodeSpeed => erodeSpeed;
    public float DepositSpeed => depositSpeed;
    public float EvaporateSpeed => evaporateSpeed;
    public float ErosionGravity => erosionGravity;
    public float ErosionRadius => erosionRadius;

    // Erosion Debug Visualization Properties
    public bool VisualizeErosionDebug => visualizeErosionDebug;
    public float ErosionDebugMinDelta => erosionDebugMinDelta;
    public float ErosionDebugMaxDelta => erosionDebugMaxDelta;
    public int ErosionDebugStride => erosionDebugStride;
    public float ErosionDebugGizmoSize => erosionDebugGizmoSize;
    public float ErosionDebugHeightOffset => erosionDebugHeightOffset;
    public int ErosionDebugMaxGizmosPerChunk => erosionDebugMaxGizmosPerChunk;
}
