using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// VoronoiBiomeGenerator, part 3 of 5: choosing a biome for each Voronoi point - weights, climate and clustering (see VoronoiBiomeGenerator.cs).
public static partial class VoronoiBiomeGenerator
{
    /// <summary>
    /// Picks a biome for a newly generated Voronoi point. The base weight per biome comes from
    /// climate fitness (if enabled) or the biome's configured weight; on top of that, biomes that
    /// already dominate the point's immediate neighborhood (already-placed nearby points, from this
    /// chunk or already-cached neighboring chunks) get a strong multiplicative boost, so territories
    /// grow into contiguous blobs by spatial proximity alone - this holds even with climate off.
    /// </summary>
    private static Biome SelectBiomeForPoint(Vector2 pointPosition, List<Biome> availableBiomes, System.Random random, int seed,
        bool useWeightedBiome, bool useClimatePlacement, float climateNoiseScale,
        List<VoronoiPoint> nearbyKnownPoints, float clusterRadius, float clusterStrength, float repeatPenalty, LayoutOptions options = null)
    {
        if (availableBiomes == null || availableBiomes.Count == 0)
            return null;

        float[] weights = ComputeBaseWeights(pointPosition, availableBiomes, seed, useWeightedBiome, useClimatePlacement, climateNoiseScale, options);

        ApplyNeighborClusterBias(weights, availableBiomes, pointPosition, nearbyKnownPoints, clusterRadius, clusterStrength, repeatPenalty);

        return WeightedRandomBiome(availableBiomes, weights, random);
    }

    /// <summary>
    /// A point's biome weights before any neighbor influence: climate fitness (if enabled; including the
    /// terrain's influence on the climate when <see cref="LayoutOptions.Climate"/> is set) or each
    /// biome's configured weight, times the landform placement affinity (mountain belts) when active.
    /// Depends only on the point's position and settings.
    /// </summary>
    private static float[] ComputeBaseWeights(Vector2 pointPosition, List<Biome> availableBiomes, int seed,
        bool useWeightedBiome, bool useClimatePlacement, float climateNoiseScale, LayoutOptions options)
    {
        float[] weights = new float[availableBiomes.Count];

        if (useClimatePlacement)
        {
            float temperature = ClimateGenerator.GetTemperature(pointPosition, seed, climateNoiseScale);
            float moisture = ClimateGenerator.GetMoisture(pointPosition, seed, climateNoiseScale);
            if (options != null && options.Climate != null)
                options.Climate.Apply(pointPosition, ref temperature, ref moisture);

            for (int i = 0; i < availableBiomes.Count; i++)
            {
                float fitness = availableBiomes[i].ClimateFitness(temperature, moisture);
                weights[i] = useWeightedBiome ? fitness * Mathf.Max(0.0001f, availableBiomes[i].weight) : fitness;
            }

            // No biome fits this climate well enough to register a usable weight: fall back to plain
            // weight/uniform rather than always picking the same "least bad" biome out-of-range.
            if (weights.Sum() <= 0.0001f)
            {
                for (int i = 0; i < availableBiomes.Count; i++)
                    weights[i] = useWeightedBiome ? Mathf.Max(0.0001f, availableBiomes[i].weight) : 1f;
            }
        }
        else
        {
            for (int i = 0; i < availableBiomes.Count; i++)
                weights[i] = useWeightedBiome ? Mathf.Max(0.0001f, availableBiomes[i].weight) : 1f;
        }

        if (options != null && options.BeltStrength > 0f && options.ShapeMode != TerrainShapeMode.ClassicOnly)
        {
            float belt = LandformGenerator.MountainBelt(pointPosition, seed, options.BeltScale);
            for (int i = 0; i < availableBiomes.Count; i++)
            {
                LandformType landform = LandformGenerator.Effective(availableBiomes[i], options.ShapeMode);
                weights[i] *= LandformGenerator.PlacementAffinity(landform, belt, options.BeltStrength);
            }
        }

        return weights;
    }

    /// <summary>
    /// Two opposing biases, both driven by the same "who's already nearby" data:
    ///
    /// 1. A positive bias that boosts whichever biome(s) already dominate the immediate neighborhood
    ///    (within clusterRadius), so a new point extends an existing territory rather than starting
    ///    its own - this is what makes territories contiguous instead of speckled.
    ///
    /// 2. A negative bias against biomes that are already common somewhere in the known area but have
    ///    no instance within clusterRadius of this specific point. Without this, nothing stops the
    ///    same biome from being rolled again far from its own territory purely by chance, producing
    ///    several separate same-shaped, same-sized disconnected patches of one biome scattered inside
    ///    a single chunk. This only fires when there's no nearby point to legitimately grow from, so
    ///    real contiguous growth is never penalized.
    ///
    /// A point with no known neighbors at all (e.g. the very first point ever generated in an empty
    /// region) is left untouched by both - there's nothing to anchor either bias to yet.
    /// </summary>
    private static void ApplyNeighborClusterBias(float[] weights, List<Biome> availableBiomes, Vector2 pointPosition,
        List<VoronoiPoint> nearbyKnownPoints, float clusterRadius, float clusterStrength, float repeatPenalty)
    {
        if (nearbyKnownPoints == null || nearbyKnownPoints.Count == 0 || clusterRadius <= 0f)
            return;

        float[] nearbyInfluence = new float[availableBiomes.Count]; // distance-weighted, only within clusterRadius.
        int[] totalCount = new int[availableBiomes.Count];          // unweighted count anywhere in the known set.
        float totalNearbyInfluence = 0f;

        foreach (var knownPoint in nearbyKnownPoints)
        {
            int biomeIndex = availableBiomes.IndexOf(knownPoint.AssignedBiome);
            if (biomeIndex < 0)
                continue;

            totalCount[biomeIndex]++;

            float dist = Vector2.Distance(pointPosition, knownPoint.Position);
            if (dist >= clusterRadius)
                continue;

            float influence = 1f - dist / clusterRadius; // linear falloff: 1 at the point itself, 0 at the radius edge.
            nearbyInfluence[biomeIndex] += influence;
            totalNearbyInfluence += influence;
        }

        if (clusterStrength > 0f && totalNearbyInfluence > 0f)
        {
            for (int i = 0; i < weights.Length; i++)
            {
                float neighborShare = nearbyInfluence[i] / totalNearbyInfluence; // 0-1: this biome's share of nearby support.
                // Up to a ~9x boost at full clusterStrength when a biome fully dominates the neighborhood;
                // no change at all when clusterStrength is 0.
                weights[i] *= Mathf.Lerp(1f, 1f + neighborShare * 8f, clusterStrength);
            }
        }

        if (repeatPenalty > 0f)
        {
            for (int i = 0; i < weights.Length; i++)
            {
                // A same-biome point is already within reach: that's legitimate growth, not a repeat - never penalize it.
                if (nearbyInfluence[i] > 0f || totalCount[i] <= 0)
                    continue;

                // The more instances of this biome already exist elsewhere (out of reach here), the
                // less likely a brand new disconnected patch of it should spawn at this location.
                weights[i] /= 1f + totalCount[i] * repeatPenalty * 2f;
            }
        }
    }

    /// <summary>
    /// Selects a biome randomly from a list of biomes, weighted by a parallel array of weights.
    /// </summary>
    /// <param name="biomes">The list of available biomes.</param>
    /// <param name="weights">Weights parallel to <paramref name="biomes"/>; larger values are more likely to be picked.</param>
    /// <param name="random">The random number generator to use.</param>
    /// <returns>A randomly selected biome, based on weight distribution.</returns>
    private static Biome WeightedRandomBiome(List<Biome> biomes, float[] weights, System.Random random)
    {
        float totalWeight = 0f;
        for (int i = 0; i < weights.Length; i++)
            totalWeight += weights[i];

        if (totalWeight <= 0f)
            return biomes[random.Next(biomes.Count)];

        // Generate a random value within the range of the total weight.
        float randomValue = (float)random.NextDouble() * totalWeight;

        // Iterate through the biomes to find the one corresponding to the random value.
        for (int i = 0; i < biomes.Count; i++)
        {
            if (randomValue < weights[i])
                return biomes[i];

            randomValue -= weights[i];
        }

        // Return the last biome as a fallback in case of floating-point rounding error.
        return biomes[biomes.Count - 1];
    }
}
