using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Immutable snapshot of every water-related <see cref="TerrainGenerator"/> setting, taken once per chunk
/// so the (background-thread) generators below don't read dozens of live properties per cell.
/// </summary>
public sealed class WaterSettings
{
    public int Seed;
    public float SeaLevel;
    public int LodCells;

    public bool OceansEnabled;
    public float ContinentScale;
    public float ContinentGradient;
    public float OceanThreshold;
    public float BeachWidth;
    public float CoastBlendWidth;
    public float BeachHeight;
    public float ShelfWidth;
    public float OceanDepth;
    public float InlandRise;
    public float InlandRiseDistance;
    public float IslandThreshold;
    public float IslandScale;
    public float IslandPeakHeight;
    public float SpawnLandRadius;

    public float CliffFrequency;
    public float CliffHeight;
    public float CliffTerraces;
    public float StackChance;
    public float StackSpacing;
    public float StackMaxHeight;

    public bool LakesEnabled;
    public float LakeSpacing;
    public float LakeChance;
    public float LakeMinRadius;
    public float LakeMaxRadius;
    public float LakeMaxDepth;
    public float LakeMaxSiteSlope;
    public float LakeOutletChance;

    public bool PondsEnabled;
    public float PondSpacing;
    public float PondChance;
    public float PondMinRadius;
    public float PondMaxRadius;
    public float PondDepth;
    public float PondMaxSiteSlope;

    public float ShoreRimWidth;
    public float ShoreFreeboard;

    public bool RiversEnabled;
    public float RiverSpacing;
    public float RiverChance;
    public float RiverMinSpringElevation;
    public float RiverMinLength;
    public float RiverMaxLength;
    public float RiverSourceWidth;
    public float RiverMouthWidth;
    public float RiverWidthVariation;
    public float RiverMeander;
    public float RiverMeanderWavelength;
    public float RiverDepth;
    public float RiverValleySlopeTan;
    public float RiverMaxValleyHalfWidth;
    public float RiverBankFreeboard;

    public bool WaterfallsEnabled;
    public bool RiverJunctions;
    public bool RiverMeanderCutoffs;
    public float WetnessDistance;
    public float WetnessHeight;
    public float SnowLineHeight;
    public float SnowmeltSprings;
    public float WaterfallMinDrop;
    public float WaterfallTierHeight;

    public static WaterSettings From(TerrainGenerator tg)
    {
        int lod = tg.LevelOfDetail;
        float continentScale = Mathf.Max(100f, tg.ContinentScale);

        return new WaterSettings
        {
            Seed = tg.VoronoiSeed,
            SeaLevel = tg.SeaLevel,
            LodCells = lod > 0 ? lod * 2 : 1,

            OceansEnabled = tg.EnableOceans,
            ContinentScale = continentScale,
            // Typical magnitude of the continent field's gradient per world unit, used to express the
            // coastline bands below in world units even though they're applied to a noise value.
            ContinentGradient = 1.5f / continentScale,
            OceanThreshold = tg.OceanThreshold,
            BeachWidth = Mathf.Max(1f, tg.BeachWidth),
            CoastBlendWidth = Mathf.Max(tg.BeachWidth + 1f, tg.CoastBlendWidth),
            BeachHeight = Mathf.Max(0f, tg.BeachHeight),
            ShelfWidth = Mathf.Max(1f, tg.ContinentalShelfWidth),
            OceanDepth = Mathf.Max(1f, tg.OceanDepth),
            InlandRise = tg.InlandRise,
            InlandRiseDistance = Mathf.Max(1f, tg.InlandRiseDistance),
            IslandThreshold = tg.IslandFrequency > 0f ? Mathf.Lerp(0.55f, 0.05f, Mathf.Clamp01(tg.IslandFrequency)) : float.PositiveInfinity,
            IslandScale = Mathf.Max(10f, tg.IslandScale),
            IslandPeakHeight = Mathf.Max(0f, tg.IslandPeakHeight),
            SpawnLandRadius = Mathf.Max(0f, tg.SpawnLandRadius),

            CliffFrequency = Mathf.Clamp01(tg.CoastCliffFrequency),
            CliffHeight = Mathf.Max(0f, tg.CoastCliffHeight),
            CliffTerraces = Mathf.Clamp01(tg.CoastCliffTerraces),
            StackChance = Mathf.Clamp01(tg.SeaStackChance),
            StackSpacing = Mathf.Max(60f, tg.SeaStackSpacing),
            StackMaxHeight = Mathf.Max(4f, tg.SeaStackMaxHeight),

            LakesEnabled = tg.EnableLakes,
            LakeSpacing = Mathf.Max(10f, tg.LakeSpacing),
            LakeChance = Mathf.Clamp01(tg.LakeChance),
            LakeMinRadius = Mathf.Max(2f, tg.LakeMinRadius),
            LakeMaxRadius = Mathf.Max(Mathf.Max(2f, tg.LakeMinRadius), tg.LakeMaxRadius),
            LakeMaxDepth = Mathf.Max(0.5f, tg.LakeMaxDepth),
            LakeMaxSiteSlope = Mathf.Max(0.01f, tg.LakeMaxSiteSlope),
            LakeOutletChance = Mathf.Clamp01(tg.LakeOutletChance),

            PondsEnabled = tg.EnablePonds,
            PondSpacing = Mathf.Max(10f, tg.PondSpacing),
            PondChance = Mathf.Clamp01(tg.PondChance),
            PondMinRadius = Mathf.Max(1f, tg.PondMinRadius),
            PondMaxRadius = Mathf.Max(Mathf.Max(1f, tg.PondMinRadius), tg.PondMaxRadius),
            PondDepth = Mathf.Max(0.2f, tg.PondDepth),
            PondMaxSiteSlope = Mathf.Max(0.01f, tg.PondMaxSiteSlope),

            ShoreRimWidth = Mathf.Max(1f, tg.ShoreRimWidth),
            ShoreFreeboard = Mathf.Max(0.05f, tg.ShoreFreeboard),

            RiversEnabled = tg.EnableRivers,
            RiverSpacing = Mathf.Max(50f, tg.RiverSpacing),
            RiverChance = Mathf.Clamp01(tg.RiverChance),
            RiverMinSpringElevation = Mathf.Max(0f, tg.RiverMinSpringElevation),
            RiverMinLength = Mathf.Max(0f, tg.RiverMinLength),
            RiverMaxLength = Mathf.Max(50f, tg.RiverMaxLength),
            RiverSourceWidth = Mathf.Max(0.5f, tg.RiverSourceWidth),
            RiverMouthWidth = Mathf.Max(0.5f, tg.RiverMouthWidth),
            RiverWidthVariation = Mathf.Clamp(tg.RiverWidthVariation, 0f, 0.9f),
            RiverMeander = Mathf.Clamp(tg.RiverMeander, 0f, 1f),
            RiverMeanderWavelength = Mathf.Max(10f, tg.RiverMeanderWavelength),
            RiverDepth = Mathf.Max(0.3f, tg.RiverDepth),
            RiverValleySlopeTan = Mathf.Tan(Mathf.Clamp(tg.RiverValleySlope, 5f, 80f) * Mathf.Deg2Rad),
            RiverMaxValleyHalfWidth = Mathf.Max(5f, tg.RiverMaxValleyWidth),
            RiverBankFreeboard = Mathf.Max(0.1f, tg.RiverBankFreeboard),

            WaterfallsEnabled = tg.EnableWaterfalls,
            RiverJunctions = tg.EnableRiverJunctions,
            RiverMeanderCutoffs = tg.EnableMeanderCutoffs,
            WetnessDistance = Mathf.Max(1f, tg.WetnessDistance),
            WetnessHeight = Mathf.Max(0.1f, tg.WetnessHeight),
            SnowLineHeight = tg.SnowLineHeight,
            SnowmeltSprings = Mathf.Max(0f, tg.SnowmeltSprings),
            WaterfallMinDrop = Mathf.Max(1f, tg.WaterfallMinDrop),
            WaterfallTierHeight = Mathf.Max(2f, tg.WaterfallTierHeight),
        };
    }
}
