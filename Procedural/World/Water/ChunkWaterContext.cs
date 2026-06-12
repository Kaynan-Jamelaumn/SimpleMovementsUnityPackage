using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies every water feature touching one chunk to that chunk's (padded) height map, in the stages
/// <see cref="HeightGenerator"/> calls it:
///
/// 1. <see cref="RecordLandSide"/> - where the coastline is (the shaping itself happens in <see cref="TerrainHeightSampler.ShapeLand"/>).
/// 2. <see cref="ApplyPreErosion"/> - lake/pond bowls and rims, river valleys and channels, carved
///    before erosion so thermal/hydraulic erosion weathers them into natural-looking shapes.
/// 3. <see cref="ApplyPostErosion"/> - hard guarantees re-applied after erosion (coastline above sea
///    level, lake rims above lake level, river banks above the river, river channels below it), so
///    erosion can never open a leak or fill a channel.
/// 4. <see cref="BuildWaterMap"/> - which cells are wet, with which surface height and water type.
///
/// Every step is a per-cell function of world position and the globally shared feature data, which is
/// why neighboring chunks agree along their shared border.
/// </summary>
public sealed class ChunkWaterContext
{
    // Minimum height land keeps above sea level right at the shoreline, so the water plane and the beach
    // never end up coplanar (z-fighting).
    private const float CoastMargin = 0.25f;

    private readonly WaterSettings settings;
    private readonly Vector2 origin;
    private readonly int size;
    private readonly float[] landSide;
    private readonly LakeFeature[] lakes;
    private readonly RiverRaster rivers;
    private readonly float coastClampLandSide;

    public ChunkWaterContext(WaterSettings settings, Vector2 origin, int size, List<LakeFeature> lakes, List<RiverPath> rivers)
    {
        this.settings = settings;
        this.origin = origin;
        this.size = size;
        this.lakes = lakes.ToArray();
        landSide = new float[size * size];

        if (rivers.Count > 0)
        {
            this.rivers = new RiverRaster(size * size);
            RiverGenerator.Rasterize(rivers, settings, origin, size, this.rivers);
        }

        // Land this close to the ocean (in continent-noise units) is kept above sea level after erosion.
        // Wider than the beach itself by two mesh vertices, so the water mesh's edge always lands on it.
        coastClampLandSide = (settings.BeachWidth + 2f * settings.LodCells) * settings.ContinentGradient;
    }

    /// <summary>
    /// Stores the continent field value (<see cref="OceanGenerator.LandSide"/>) the terrain sampler computed
    /// for a cell while shaping it (float.MaxValue when oceans are off), for the coastline guarantee and
    /// ocean detection below.
    /// </summary>
    public void RecordLandSide(int x, int y, float side)
    {
        landSide[y * size + x] = settings.OceansEnabled ? side : float.MaxValue;
    }

    public float ApplyPreErosion(int x, int y, float worldX, float worldY, float height)
    {
        for (int i = 0; i < lakes.Length; i++)
        {
            LakeFeature lake = lakes[i];
            if (!lake.TryGetLocal(worldX, worldY, out float rho, out float beyond))
                continue;

            height -= lake.BowlCarve(rho);
            height = lake.ApplyRim(rho, beyond, height);
        }

        if (rivers != null)
            height = Mathf.Min(height, rivers.Carve[y * size + x]);

        return height;
    }

    public void ApplyPostErosion(float[,] heights)
    {
        for (int y = 0; y < size; y++)
        {
            float worldY = origin.y + y;
            for (int x = 0; x < size; x++)
            {
                float worldX = origin.x + x;
                int index = y * size + x;
                float height = heights[x, y];
                float side = landSide[index];

                if (settings.OceansEnabled && side >= 0f && side < coastClampLandSide)
                    height = Mathf.Max(height, settings.SeaLevel + CoastMargin);

                bool insideLakeWater = false;
                for (int i = 0; i < lakes.Length; i++)
                {
                    LakeFeature lake = lakes[i];
                    if (!lake.TryGetLocal(worldX, worldY, out float rho, out float beyond))
                        continue;

                    height = lake.ApplyRim(rho, beyond, height);
                    if (lake.IsInWaterZone(rho))
                        insideLakeWater = true;
                }

                if (rivers != null)
                {
                    // Banks first, channel last: a river is allowed to cut through a lake rim or the
                    // coastline (that's an inlet/outlet/mouth), but nothing is allowed to fill its channel.
                    float bank = rivers.Bank[index];
                    if (bank > float.NegativeInfinity && side >= 0f && !insideLakeWater)
                        height = Mathf.Max(height, bank);

                    height = Mathf.Min(height, rivers.Channel[index]);
                }

                heights[x, y] = height;
            }
        }
    }

    public WaterMapData BuildWaterMap(float[,] paddedHeights, int padding, int finalSize)
    {
        WaterMapData map = new WaterMapData(finalSize);

        for (int y = 0; y < finalSize; y++)
        {
            int paddedY = y + padding;
            float worldY = origin.y + paddedY;
            for (int x = 0; x < finalSize; x++)
            {
                int paddedX = x + padding;
                float worldX = origin.x + paddedX;
                int index = paddedY * size + paddedX;
                float height = paddedHeights[paddedX, paddedY];

                CellWater cell = Classify(index, worldX, worldY, height);
                map.Type[x, y] = cell.Type;
                map.Surface[x, y] = cell.Surface;
                map.ShoreLevel[x, y] = cell.Shore;
                map.FlowX[x, y] = cell.FlowX;
                map.FlowY[x, y] = cell.FlowY;
                map.Wetness[x, y] = cell.Type != WaterBodyType.None ? 1f : Wetness(cell.WetDistance, height - cell.WetLevel);
            }
        }

        return map;
    }

    /// <summary>
    /// Fills the water part of an object-placement environment (see <see cref="PlacementFields"/>) for the area
    /// it covers - the chunk plus a margin, which must lie within this context's padded area. Cells use exactly
    /// the classification <see cref="BuildWaterMap"/> gives them.
    /// </summary>
    /// <param name="offset">Index in this context's padded grid of the fields' first cell (padding - margin).</param>
    public void FillPlacementFields(PlacementFields fields, float[,] paddedHeights, int offset)
    {
        int n = fields.Size;
        for (int y = 0; y < n; y++)
        {
            int paddedY = y + offset;
            float worldY = origin.y + paddedY;
            for (int x = 0; x < n; x++)
            {
                int paddedX = x + offset;
                float worldX = origin.x + paddedX;
                int index = paddedY * size + paddedX;
                CellWater cell = Classify(index, worldX, worldY, paddedHeights[paddedX, paddedY]);

                int i = y * n + x;
                fields.WaterType[i] = (byte)cell.Type;
                fields.WaterSurface[i] = cell.Surface;
                fields.NearestWaterLevel[i] = cell.Type != WaterBodyType.None ? cell.Surface : cell.WetLevel;
                fields.FeatureDistance[(int)WaterBodyType.Ocean][i] = cell.OceanDistance;
                fields.FeatureDistance[(int)WaterBodyType.Lake][i] = cell.LakeDistance;
                fields.FeatureDistance[(int)WaterBodyType.Pond][i] = cell.PondDistance;
                fields.FeatureDistance[(int)WaterBodyType.River][i] = cell.RiverDistance;
                fields.RiverDirX[i] = cell.RiverDirX;
                fields.RiverDirY[i] = cell.RiverDirY;
            }
        }
    }

    /// <summary>Everything the water step decides about one cell.</summary>
    private struct CellWater
    {
        public WaterBodyType Type;
        public float Surface;
        public float Shore;
        public float FlowX, FlowY;
        /// <summary>Distance to the nearest water's edge (lake outline, coast, river channel) and that water's level, for wetness.</summary>
        public float WetDistance, WetLevel;
        /// <summary>Distance to each kind of water from the global feature data (negative inside it, +infinity when none is near).</summary>
        public float OceanDistance, LakeDistance, PondDistance, RiverDistance;
        /// <summary>Direction of the nearest river (unit vector along its course), zero when none is near.</summary>
        public float RiverDirX, RiverDirY;
    }

    private CellWater Classify(int index, float worldX, float worldY, float height)
    {
        CellWater cell = new CellWater
        {
            Type = WaterBodyType.None,
            Surface = float.NaN,
            Shore = float.NaN,
            WetDistance = float.PositiveInfinity,
            WetLevel = float.NaN,
            OceanDistance = float.PositiveInfinity,
            LakeDistance = float.PositiveInfinity,
            PondDistance = float.PositiveInfinity,
            RiverDistance = float.PositiveInfinity,
        };

        for (int i = 0; i < lakes.Length; i++)
        {
            LakeFeature lake = lakes[i];
            if (!lake.TryGetLocal(worldX, worldY, out float rho, out float beyond))
                continue;

            float level = lake.WaterLevel;
            if (lake.IsInWaterZone(rho) && height < level)
            {
                cell.Type = lake.Type;
                cell.Surface = level;
                break;
            }

            if (float.IsNaN(cell.Shore) && lake.IsInShoreZone(beyond))
                cell.Shore = level;
        }

        float side = landSide[index];

        // Nearest water for wetness: how far away it is (world units from its edge) and its level.
        for (int i = 0; i < lakes.Length; i++)
        {
            LakeFeature lake = lakes[i];
            if (!lake.TryGetLocal(worldX, worldY, out _, out float lakeBeyond))
                continue;

            if (lake.Type == WaterBodyType.Pond)
                cell.PondDistance = Mathf.Min(cell.PondDistance, lakeBeyond);
            else
                cell.LakeDistance = Mathf.Min(cell.LakeDistance, lakeBeyond);

            if (lakeBeyond < cell.WetDistance)
            {
                cell.WetDistance = lakeBeyond;
                cell.WetLevel = lake.WaterLevel;
            }
        }
        if (settings.OceansEnabled && side < float.MaxValue)
        {
            float coastDistance = side / settings.ContinentGradient;
            cell.OceanDistance = coastDistance;
            if (coastDistance < cell.WetDistance)
            {
                cell.WetDistance = coastDistance;
                cell.WetLevel = settings.SeaLevel;
            }
        }
        if (rivers != null)
        {
            cell.RiverDistance = rivers.HintEdgeDistance[index];
            cell.RiverDirX = rivers.HintDirX[index];
            cell.RiverDirY = rivers.HintDirY[index];
            if (rivers.HintEdgeDistance[index] < cell.WetDistance)
            {
                cell.WetDistance = rivers.HintEdgeDistance[index];
                cell.WetLevel = rivers.Hint[index];
            }
        }

        if (cell.Type == WaterBodyType.None && settings.OceansEnabled && side < 0f && height < settings.SeaLevel)
        {
            cell.Type = WaterBodyType.Ocean;
            cell.Surface = settings.SeaLevel;
        }

        if (cell.Type == WaterBodyType.None && rivers != null && rivers.OwnerNorm[index] <= 1f && height < rivers.Surface[index])
        {
            cell.Type = WaterBodyType.River;
            cell.Surface = rivers.Surface[index];
            cell.FlowX = rivers.FlowX[index];
            cell.FlowY = rivers.FlowY[index];
        }

        if (cell.Type != WaterBodyType.None)
        {
            cell.Shore = cell.Surface;
        }
        else if (float.IsNaN(cell.Shore))
        {
            if (rivers != null && !float.IsNaN(rivers.Hint[index]))
                cell.Shore = rivers.Hint[index];
            else if (settings.OceansEnabled && side < coastClampLandSide * 2f)
                cell.Shore = settings.SeaLevel;
        }

        return cell;
    }

    /// <summary>1 at the water's edge, fading to 0 with distance from it and with height above its level.</summary>
    private float Wetness(float distance, float heightAbove)
    {
        if (float.IsInfinity(distance) || float.IsNaN(heightAbove))
            return 0f;
        float near = 1f - WaterGenerator.SmoothStep01(Mathf.Max(0f, distance) / settings.WetnessDistance);
        float low = 1f - WaterGenerator.SmoothStep01(Mathf.Max(0f, heightAbove) / settings.WetnessHeight);
        return near * low;
    }
}
