using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plans where world portals go - a pure function of the world seed and the <see cref="PortalSettings"/>, the same
/// principle as the object placement's landmarks (<see cref="ObjectPlacementEngine"/>): no shared random generator,
/// no dependence on which chunk loads first.
///
/// The world is cut into square regions of <see cref="PortalSettings.regionSize"/>. Each region has
/// <see cref="PortalSettings.maxNumberOfPortals"/> slots; a slot is used with <see cref="PortalSettings.spawnChance"/>,
/// its point is hashed inside the region at least half of <see cref="PortalSettings.minDistanceBetweenPortals"/> from
/// the region's edges (so portals of neighbouring regions keep that distance too), and its type is a weighted pick.
/// These are planned points; the chunk that contains a point turns it into a portal on the best nearby spot
/// (see <see cref="PortalSpawner"/>) - or leaves it empty when there is none.
/// </summary>
public static class PortalSitePlanner
{
    /// <summary>A planned portal: its identity, planned point (world x, z), type (index into Prefabs) and facing.</summary>
    public struct Site
    {
        public PortalSiteId Id;
        public Vector2 Point;
        public int Type;
        public float Yaw;
    }

    private static readonly int Salt = PlacementRandom.StableHash("PortalSites");
    private const int StreamsPerSlot = 8;

    /// <summary>The planned portals of region (<paramref name="rx"/>, <paramref name="rz"/>), in slot order.</summary>
    public static void RegionSites(PortalSettings settings, int worldSeed, int rx, int rz, List<Site> into)
    {
        if (settings == null || into == null || settings.prefabs == null || settings.prefabs.Count == 0)
            return;
        float size = Mathf.Max(64f, settings.regionSize);
        float minDistance = settings.EffectiveMinDistance;
        float margin = minDistance * 0.5f;
        int slots = Mathf.Max(0, settings.maxNumberOfPortals);
        var perType = new int[settings.prefabs.Count];
        int first = into.Count;

        for (int slot = 0; slot < slots; slot++)
        {
            int stream = slot * StreamsPerSlot;
            if (PlacementRandom.Value(worldSeed, Salt, rx, rz, stream) >= settings.spawnChance)
                continue;
            float x = rx * size + margin + PlacementRandom.Value(worldSeed, Salt, rx, rz, stream + 1) * (size - 2f * margin);
            float z = rz * size + margin + PlacementRandom.Value(worldSeed, Salt, rx, rz, stream + 2) * (size - 2f * margin);
            var point = new Vector2(x, z);

            bool spaced = true;
            for (int i = first; i < into.Count; i++)
            {
                if ((into[i].Point - point).sqrMagnitude < minDistance * minDistance)
                {
                    spaced = false;
                    break;
                }
            }
            if (!spaced)
                continue;

            int type = PickType(settings, PlacementRandom.Value(worldSeed, Salt, rx, rz, stream + 3), perType);
            if (type < 0)
                continue;
            perType[type]++;
            into.Add(new Site
            {
                Id = new PortalSiteId(rx, rz, slot),
                Point = point,
                Type = type,
                Yaw = PlacementRandom.Value(worldSeed, Salt, rx, rz, stream + 4) * 360f,
            });
        }
    }

    /// <summary>
    /// The planned portals whose point lies in [<paramref name="min"/>, <paramref name="max"/>) - e.g. one chunk, or
    /// a map area (for markers: these are planned points, a few end up empty or moved by up to Search Radius).
    /// </summary>
    public static void SitesInArea(PortalSettings settings, int worldSeed, Vector2 min, Vector2 max, List<Site> into)
    {
        if (settings == null || into == null)
            return;
        float size = Mathf.Max(64f, settings.regionSize);
        int rx0 = Mathf.FloorToInt(min.x / size), rx1 = Mathf.FloorToInt(max.x / size);
        int rz0 = Mathf.FloorToInt(min.y / size), rz1 = Mathf.FloorToInt(max.y / size);
        var region = new List<Site>();
        for (int rz = rz0; rz <= rz1; rz++)
        {
            for (int rx = rx0; rx <= rx1; rx++)
            {
                region.Clear();
                RegionSites(settings, worldSeed, rx, rz, region);
                foreach (Site site in region)
                    if (site.Point.x >= min.x && site.Point.y >= min.y && site.Point.x < max.x && site.Point.y < max.y)
                        into.Add(site);
            }
        }
    }

    /// <summary>A weighted pick among the types that still fit their per-region limit (-1 when none).</summary>
    private static int PickType(PortalSettings settings, float random01, int[] perType)
    {
        List<SpawnablePortal> types = settings.prefabs;
        float total = 0f;
        for (int t = 0; t < types.Count; t++)
            total += Available(settings, types[t], perType[t]);
        if (total <= 0f)
            return -1;
        float pick = random01 * total;
        for (int t = 0; t < types.Count; t++)
        {
            float w = Available(settings, types[t], perType[t]);
            if (w <= 0f)
                continue;
            if (pick < w)
                return t;
            pick -= w;
        }
        for (int t = types.Count - 1; t >= 0; t--)
            if (Available(settings, types[t], perType[t]) > 0f)
                return t;
        return -1;
    }

    private static float Available(PortalSettings settings, SpawnablePortal type, int placed)
    {
        if (type == null || (type.maxInstances > 0 && placed >= type.maxInstances))
            return 0f;
        return type.Weight(settings.useWeightedSelection, settings.rarityFavorBias);
    }
}
