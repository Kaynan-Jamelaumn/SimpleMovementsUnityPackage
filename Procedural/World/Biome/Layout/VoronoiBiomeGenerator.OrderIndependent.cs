using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// VoronoiBiomeGenerator, part 4 of 5: the order-independent layout (see VoronoiBiomeGenerator.cs).
public static partial class VoronoiBiomeGenerator
{
    // ------------------------------------------------------------------ order-independent layout
    //
    // The original assignment (above) lets each new point look at whichever neighboring points already
    // exist - so the result depends on which part of the world was generated first, and chunks are
    // generated on worker threads in no fixed order. This version gets the same kind of clustering from
    // information that never depends on order: every point first gets a provisional biome from its own
    // position alone; then, in a couple of passes, each point re-rolls its biome with the neighbor
    // clustering / repeat-penalty biases applied to its neighbors' labels from the previous pass. Every
    // label is a pure function of (cell, point index, seed, settings), so any visiting order - and any
    // thread - produces the same world. Point positions are identical to the original path.

    private const int OrderIndependentPasses = 2;
    private static readonly Dictionary<Vector2Int, Vector2[]> CellPositions = new Dictionary<Vector2Int, Vector2[]>();
    private static readonly Dictionary<Vector2Int, int[]>[] CellLabels =
    {
        new Dictionary<Vector2Int, int[]>(), new Dictionary<Vector2Int, int[]>(), new Dictionary<Vector2Int, int[]>(),
    };

    private sealed class LayoutArgs
    {
        public float Scale;
        public int NumPoints;
        public List<Biome> Biomes;
        public int Seed;
        public bool UseWeightedBiome;
        public bool UseClimatePlacement;
        public float ClimateNoiseScale;
        public float ClusterStrength;
        public float ClusterRadius;
        public float RepeatPenalty;
        public LayoutOptions Options;
    }

    private static List<VoronoiPoint> BuildOrderIndependentPoints(Vector2Int cell, LayoutArgs args)
    {
        Vector2[] positions = CellPointPositions(cell, args);
        int[] labels = CellLabelsAt(cell, OrderIndependentPasses, args);
        var points = new List<VoronoiPoint>(positions.Length);
        for (int k = 0; k < positions.Length; k++)
            points.Add(new VoronoiPoint(positions[k], labels[k] >= 0 ? args.Biomes[labels[k]] : null));
        return points;
    }

    private static Vector2[] CellPointPositions(Vector2Int cell, LayoutArgs args)
    {
        if (CellPositions.TryGetValue(cell, out Vector2[] positions))
            return positions;

        // Same seed and generator as the original path, so points sit in exactly the same places.
        var random = new System.Random(GenerateSeed(cell, args.Seed));
        positions = GenerateJitteredGridPoints(cell, args.Scale, args.NumPoints, random).ToArray();
        CellPositions[cell] = positions;
        return positions;
    }

    private static int[] CellLabelsAt(Vector2Int cell, int pass, LayoutArgs args)
    {
        Dictionary<Vector2Int, int[]> cache = CellLabels[pass];
        if (cache.TryGetValue(cell, out int[] labels))
            return labels;

        Vector2[] positions = CellPointPositions(cell, args);
        labels = new int[positions.Length];
        bool clustering = args.ClusterRadius > 0f && (args.ClusterStrength > 0f || args.RepeatPenalty > 0f);

        if (pass > 0 && !clustering)
        {
            labels = CellLabelsAt(cell, 0, args);
            cache[cell] = labels;
            return labels;
        }

        // Neighbors from the previous pass: every point within reach of this cell's points.
        List<VoronoiPoint> neighborhood = null;
        float reach = Mathf.Max(args.ClusterRadius, args.Scale * 1.5f);
        if (pass > 0)
        {
            int cellRadius = Mathf.Clamp(Mathf.CeilToInt(reach / args.Scale), 1, 5);
            neighborhood = new List<VoronoiPoint>();
            for (int dy = -cellRadius; dy <= cellRadius; dy++)
            {
                for (int dx = -cellRadius; dx <= cellRadius; dx++)
                {
                    Vector2Int other = new Vector2Int(cell.x + dx, cell.y + dy);
                    Vector2[] otherPositions = CellPointPositions(other, args);
                    int[] otherLabels = CellLabelsAt(other, pass - 1, args);
                    for (int m = 0; m < otherPositions.Length; m++)
                    {
                        if (otherLabels[m] >= 0)
                            neighborhood.Add(new VoronoiPoint(otherPositions[m], args.Biomes[otherLabels[m]]));
                    }
                }
            }
        }

        var nearby = new List<VoronoiPoint>();
        for (int k = 0; k < positions.Length; k++)
        {
            float[] weights = ComputeBaseWeights(positions[k], args.Biomes, args.Seed, args.UseWeightedBiome,
                args.UseClimatePlacement, args.ClimateNoiseScale, args.Options);

            if (neighborhood != null)
            {
                nearby.Clear();
                foreach (VoronoiPoint neighbor in neighborhood)
                {
                    // The point itself is not its own neighbor.
                    if (neighbor.Position == positions[k])
                        continue;
                    if ((neighbor.Position - positions[k]).sqrMagnitude < reach * reach)
                        nearby.Add(neighbor);
                }
                ApplyNeighborClusterBias(weights, args.Biomes, positions[k], nearby, args.ClusterRadius, args.ClusterStrength, args.RepeatPenalty);
            }

            unchecked
            {
                var random = new System.Random(GenerateSeed(cell, args.Seed ^ (int)(0x9E3779B9u * (uint)(k + 1)) ^ (pass * 0x2545F491)));
                labels[k] = WeightedRandomIndex(weights, random);
            }
        }

        cache[cell] = labels;
        return labels;
    }

    private static int WeightedRandomIndex(float[] weights, System.Random random)
    {
        if (weights.Length == 0)
            return -1;

        float totalWeight = 0f;
        for (int i = 0; i < weights.Length; i++)
            totalWeight += weights[i];

        if (totalWeight <= 0f)
            return random.Next(weights.Length);

        float randomValue = (float)random.NextDouble() * totalWeight;
        for (int i = 0; i < weights.Length; i++)
        {
            if (randomValue < weights[i])
                return i;
            randomValue -= weights[i];
        }
        return weights.Length - 1;
    }
}
