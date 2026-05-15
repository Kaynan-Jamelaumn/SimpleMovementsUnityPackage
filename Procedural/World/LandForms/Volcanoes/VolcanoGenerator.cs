using System;
using System.Collections.Concurrent;
using UnityEngine;

/// <summary>
/// Volcanoes and calderas: rare, large landmarks placed on a very coarse grid (at most one per cell,
/// with a low chance), so a volcano dominates a large area when it does appear. Each one is a pure
/// function of world position and seed and is cached globally, like lakes and rivers, so every chunk
/// sees the same volcano. It is part of the base terrain (<see cref="TerrainHeightSampler.SampleBaseHeight"/>),
/// so rivers run down its flanks and lakes can settle in a caldera.
/// </summary>
public static class VolcanoGenerator
{
    private static readonly ConcurrentDictionary<long, Lazy<VolcanoFeature>> Cache = new ConcurrentDictionary<long, Lazy<VolcanoFeature>>();

    public static void ClearCache()
    {
        Cache.Clear();
    }

    /// <summary>Adds any volcano near this position to <paramref name="height"/> (the terrain before volcanoes).</summary>
    public static float Apply(LandformSettings s, TerrainHeightSampler sampler, float x, float y, float height)
    {
        if (!s.VolcanoesEnabled)
            return height;

        VolcanoFeature[] near = Buffer;
        int count = GatherNear(s, sampler, x, y, near);
        for (int i = 0; i < count; i++)
        {
            VolcanoFeature volcano = near[i];
            float rho = volcano.Rho(x, y, out float cos, out float sin);
            if (rho >= VolcanoFeature.ApronReach)
                continue;

            // Under the edifice the old terrain is buried: its relief is pulled toward the volcano's base level.
            float cover = 1f - WaterGenerator.SmoothStep01((rho - 0.65f) / 0.6f);
            height += (volcano.BaseLevel - height) * cover * 0.75f;
            height += volcano.Elevation(x, y, rho, cos, sin);
        }
        return height;
    }

    /// <summary>0-1 volcanic ground cover at this position (0 when no volcano is near).</summary>
    public static float SurfaceMask(LandformSettings s, TerrainHeightSampler sampler, float x, float y)
    {
        if (!s.VolcanoesEnabled)
            return 0f;

        float mask = 0f;
        VolcanoFeature[] near = Buffer;
        int count = GatherNear(s, sampler, x, y, near);
        for (int i = 0; i < count; i++)
            mask = Mathf.Max(mask, near[i].SurfaceMask(x, y));
        return mask;
    }

    /// <summary>Every volcano whose center lies in the given world rectangle (e.g. for map markers).</summary>
    public static System.Collections.Generic.List<VolcanoFeature> InArea(LandformSettings s, TerrainHeightSampler sampler, Vector2 min, Vector2 max)
    {
        var found = new System.Collections.Generic.List<VolcanoFeature>();
        if (!s.VolcanoesEnabled)
            return found;
        Vector2Int c0 = WaterGenerator.CellOf(min, s.VolcanoSpacing);
        Vector2Int c1 = WaterGenerator.CellOf(max, s.VolcanoSpacing);
        for (int cy = c0.y; cy <= c1.y; cy++)
        {
            for (int cx = c0.x; cx <= c1.x; cx++)
            {
                VolcanoFeature volcano = Get(new Vector2Int(cx, cy), s, sampler);
                if (volcano != null && volcano.Center.x >= min.x && volcano.Center.x <= max.x && volcano.Center.y >= min.y && volcano.Center.y <= max.y)
                    found.Add(volcano);
            }
        }
        return found;
    }

    [ThreadStatic] private static VolcanoFeature[] buffer;
    private static VolcanoFeature[] Buffer => buffer ?? (buffer = new VolcanoFeature[9]);

    private static int GatherNear(LandformSettings s, TerrainHeightSampler sampler, float x, float y, VolcanoFeature[] into)
    {
        int count = 0;
        float spacing = s.VolcanoSpacing;
        int cx = Mathf.FloorToInt(x / spacing);
        int cy = Mathf.FloorToInt(y / spacing);
        float reach = s.VolcanoMaxRadius * VolcanoFeature.ApronReach * 1.15f;

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                // Skip neighbor cells whose area is farther away than any volcano in them could reach.
                float minX = (cx + dx) * spacing, minY = (cy + dy) * spacing;
                float ex = Mathf.Max(0f, Mathf.Max(minX - x, x - (minX + spacing)));
                float ey = Mathf.Max(0f, Mathf.Max(minY - y, y - (minY + spacing)));
                if (ex * ex + ey * ey > reach * reach)
                    continue;

                VolcanoFeature volcano = Get(new Vector2Int(cx + dx, cy + dy), s, sampler);
                if (volcano == null)
                    continue;
                float ddx = x - volcano.Center.x, ddy = y - volcano.Center.y;
                if (ddx * ddx + ddy * ddy > volcano.Influence * volcano.Influence)
                    continue;
                into[count++] = volcano;
            }
        }
        return count;
    }

    private static VolcanoFeature Get(Vector2Int cell, LandformSettings s, TerrainHeightSampler sampler)
    {
        long key = WaterGenerator.CellKey(cell);
        if (!Cache.TryGetValue(key, out Lazy<VolcanoFeature> lazy))
        {
            lazy = Cache.GetOrAdd(key, NewEntry(cell, s, sampler));
        }
        return lazy.Value;
    }

    // Separate from Get so the lambda's captured variables are only allocated when a cell is first seen,
    // not on every lookup (C# allocates a method's closure when the method starts).
    private static Lazy<VolcanoFeature> NewEntry(Vector2Int cell, LandformSettings s, TerrainHeightSampler sampler)
    {
        return new Lazy<VolcanoFeature>(() => Evaluate(cell, s, sampler), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private static VolcanoFeature Evaluate(Vector2Int cell, LandformSettings s, TerrainHeightSampler sampler)
    {
        var random = new System.Random(WaterGenerator.Hash(cell.x, cell.y, s.Seed, 0x7011));
        if (random.NextDouble() >= s.VolcanoChance)
            return null;

        float spacing = s.VolcanoSpacing;
        float radius = Mathf.Lerp(s.VolcanoMinRadius, s.VolcanoMaxRadius, (float)random.NextDouble());
        // Keep the whole volcano (with its apron) inside its own cell.
        float margin = Mathf.Min(0.45f * spacing, radius * VolcanoFeature.ApronReach * 1.15f);
        Vector2 center = new Vector2(
            cell.x * spacing + margin + (float)random.NextDouble() * Mathf.Max(0f, spacing - 2f * margin),
            cell.y * spacing + margin + (float)random.NextDouble() * Mathf.Max(0f, spacing - 2f * margin));

        // Never bury the spawn area.
        float keepClear = radius * VolcanoFeature.ApronReach * 1.15f + 150f;
        if (center.sqrMagnitude < keepClear * keepClear)
            return null;

        var volcano = new VolcanoFeature
        {
            Center = center,
            Radius = radius,
            Height = Mathf.Lerp(s.VolcanoMinHeight, s.VolcanoMaxHeight, (float)random.NextDouble()),
            IsCaldera = random.NextDouble() < s.CalderaChance,
            Harmonic1 = 0.05f + 0.1f * (float)random.NextDouble(),
            Harmonic2 = 0.03f + 0.07f * (float)random.NextDouble(),
            Phase1 = (float)random.NextDouble() * Mathf.PI * 2f,
            Phase2 = (float)random.NextDouble() * Mathf.PI * 2f,
            CraterRadius = 0.07f + 0.04f * (float)random.NextDouble(),
            CalderaRim = 0.45f + 0.15f * (float)random.NextDouble(),
            CalderaFloor = 0.25f + 0.15f * (float)random.NextDouble(),
            HasInnerCone = random.NextDouble() < 0.5,
            Salt = random.Next(1, 100000),
        };
        float coneAngle = (float)random.NextDouble() * Mathf.PI * 2f;
        float coneDistance = (float)random.NextDouble() * volcano.CalderaRim * 0.45f;
        volcano.InnerConeOffset = new Vector2(Mathf.Cos(coneAngle), Mathf.Sin(coneAngle)) * coneDistance;

        // Base level: the land it stands on, averaged over the center and a ring halfway out.
        float sum = sampler.SamplePreVolcanoHeight(center.x, center.y);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            sum += sampler.SamplePreVolcanoHeight(center.x + Mathf.Cos(a) * radius * 0.5f, center.y + Mathf.Sin(a) * radius * 0.5f);
        }
        volcano.BaseLevel = sum / 9f;
        return volcano;
    }
}
