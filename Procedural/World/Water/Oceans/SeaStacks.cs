using System;
using System.Collections.Concurrent;
using UnityEngine;

/// <summary>
/// Sea stacks: isolated rock towers standing in the sea off rugged cliff coasts, the remains of old
/// headlands. Placed sparsely on a grid (at most one small group per cell, only where the coast is cliff
/// and the water is a short way offshore), each with its own height, width, fluted irregular outline,
/// weathered stepped top and a rocky apron just under the water.
/// </summary>
public static class SeaStacks
{
    public const float MaxOffshore = 90f;

    private sealed class Stack
    {
        public Vector2 Center;
        public float Radius;
        public float Top;
        public float H1, H2, P1, P2;
        public int Salt;
    }

    private static readonly Stack[] None = new Stack[0];
    private static readonly ConcurrentDictionary<long, Lazy<Stack[]>> Cache = new ConcurrentDictionary<long, Lazy<Stack[]>>();

    public static void ClearCache()
    {
        Cache.Clear();
    }

    public static float Apply(WaterSettings s, float x, float y, float floor)
    {
        float spacing = s.StackSpacing;
        int cx = Mathf.FloorToInt(x / spacing);
        int cy = Mathf.FloorToInt(y / spacing);
        float height = floor;

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                Stack[] stacks = Get(new Vector2Int(cx + dx, cy + dy), s);
                for (int i = 0; i < stacks.Length; i++)
                {
                    Stack stack = stacks[i];
                    float ox = x - stack.Center.x, oy = y - stack.Center.y;
                    float r = Mathf.Sqrt(ox * ox + oy * oy);
                    if (r > stack.Radius * 1.9f)
                        continue;

                    float cos = r > 1e-4f ? ox / r : 1f;
                    float sin = r > 1e-4f ? oy / r : 0f;
                    float angle = Mathf.Atan2(oy, ox);
                    float wobble = 1f + stack.H1 * Mathf.Sin(2f * angle + stack.P1) + stack.H2 * Mathf.Sin(3f * angle + stack.P2)
                                   + 0.12f * WaterGenerator.Fbm(cos * 2.5f + stack.Salt * 0.01f, sin * 2.5f, s.Seed, stack.Salt, 2);
                    float rho = r / (stack.Radius * Mathf.Max(0.4f, wobble));

                    // Rocky apron just under the water around the base.
                    height = Mathf.Max(height, s.SeaLevel - 1.2f - (s.OceanDepth + 10f) * WaterGenerator.SmoothStep01((rho - 1f) / 0.85f));

                    // Near-vertical sides; a weathered top with a couple of stepped shelves.
                    float body = 1f - WaterGenerator.SmoothStep01((rho - 0.8f) / 0.22f);
                    if (body <= 0f)
                        continue;
                    float above = stack.Top - s.SeaLevel;
                    float shelves = Mathf.Floor(Mathf.Clamp01(0.5f + 0.8f * WaterGenerator.Fbm(x / 7f, y / 7f, s.Seed, stack.Salt + 3, 2)) * 3f) / 3f;
                    float top = stack.Top - above * 0.12f * shelves + 0.6f * WaterGenerator.Fbm(x / 3f, y / 3f, s.Seed, stack.Salt + 7, 2);
                    height = Mathf.Max(height, s.SeaLevel - 2f + (top - s.SeaLevel + 2f) * body);
                }
            }
        }

        return height;
    }

    private static Stack[] Get(Vector2Int cell, WaterSettings s)
    {
        long key = WaterGenerator.CellKey(cell);
        if (!Cache.TryGetValue(key, out Lazy<Stack[]> lazy))
            lazy = Cache.GetOrAdd(key, NewEntry(cell, s));
        return lazy.Value;
    }

    // Separate from Get so the lambda's captured variables are only allocated when a cell is first seen,
    // not on every lookup (C# allocates a method's closure when the method starts).
    private static Lazy<Stack[]> NewEntry(Vector2Int cell, WaterSettings s)
    {
        return new Lazy<Stack[]>(() => Evaluate(cell, s), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private static Stack[] Evaluate(Vector2Int cell, WaterSettings s)
    {
        var random = new System.Random(WaterGenerator.Hash(cell.x, cell.y, s.Seed, 0x57AC));
        float spacing = s.StackSpacing;
        Vector2 site = new Vector2((cell.x + 0.2f + 0.6f * (float)random.NextDouble()) * spacing,
                                   (cell.y + 0.2f + 0.6f * (float)random.NextDouble()) * spacing);
        float g = s.ContinentGradient;
        float target = 12f + (MaxOffshore - 20f) * (float)random.NextDouble();

        // Move the site across the coast until it is `target` units offshore (two Newton steps on the
        // continent field), so any cell a cliff coast passes through can get a stack group.
        Vector2 start = site;
        for (int iteration = 0; iteration < 2; iteration++)
        {
            float side = OceanGenerator.LandSide(s, site.x, site.y);
            const float h = 5f;
            Vector2 gradient = new Vector2(
                OceanGenerator.LandSide(s, site.x + h, site.y) - OceanGenerator.LandSide(s, site.x - h, site.y),
                OceanGenerator.LandSide(s, site.x, site.y + h) - OceanGenerator.LandSide(s, site.x, site.y - h)) / (2f * h);
            float g2 = gradient.sqrMagnitude;
            if (g2 < 1e-14f)
                return None;
            site += gradient * ((-target * g - side) / g2);
        }
        if ((site - start).sqrMagnitude > 0.75f * spacing * 0.75f * spacing)
            return None;
        float offshore = -OceanGenerator.LandSide(s, site.x, site.y) / g;
        if (offshore < 10f || offshore > MaxOffshore)
            return None;

        float character = OceanGenerator.CoastCharacter(s, site.x, site.y);
        if (character < 0.45f || random.NextDouble() > s.StackChance * character)
            return None;

        int count = 1 + (random.NextDouble() < 0.45 ? 1 : 0) + (random.NextDouble() < 0.15 ? 1 : 0);
        float cliffTop = OceanGenerator.CliffTop(s, site.x, site.y);
        var stacks = new System.Collections.Generic.List<Stack>(count);
        for (int i = 0; i < count; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float offset = i == 0 ? 0f : 9f + 12f * (float)random.NextDouble();
            Vector2 center = site + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * offset;
            float radius = 4f + 9f * (float)random.NextDouble();
            // Each stack stays clear of the shore.
            if (-OceanGenerator.LandSide(s, center.x, center.y) / g < radius + 4f)
                continue;
            float height = Mathf.Max(4f, (0.45f + 0.55f * (float)random.NextDouble()) * Mathf.Min(s.StackMaxHeight, cliffTop * 1.15f));
            stacks.Add(new Stack
            {
                Center = center,
                Radius = radius,
                Top = s.SeaLevel + height,
                H1 = 0.08f + 0.14f * (float)random.NextDouble(),
                H2 = 0.04f + 0.1f * (float)random.NextDouble(),
                P1 = (float)random.NextDouble() * 6.283f,
                P2 = (float)random.NextDouble() * 6.283f,
                Salt = random.Next(1, 100000),
            });
        }
        return stacks.ToArray();
    }
}
