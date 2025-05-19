using System.Collections.Generic;
using System.Linq;
using UnityEngine;
/// <summary>
/// Generates Voronoi-based biomes for a procedurally generated map.
/// </summary>
public static class VoronoiBiomeGenerator
{
    // Represents a point in the Voronoi diagram with an assigned biome.
    private class VoronoiPoint
    {
        public Vector2 Position;    // Position of the Voronoi point in the world.
        public Biome AssignedBiome; // Biome associated with this Voronoi point.

        // Constructor to initialize a Voronoi point with its position and assigned biome.
        public VoronoiPoint(Vector2 position, Biome biome)
        {
            Position = position;
            AssignedBiome = biome;
        }
    }

    // Dictionary to store Voronoi points for different chunks (keyed by chunk coordinates).
    private static readonly Dictionary<Vector2Int, List<VoronoiPoint>> ChunkVoronoiPoints = new();

    // Lock object used to ensure thread-safe access to the ChunkVoronoiPoints dictionary.
    private static readonly object LockObject = new();

    /// <summary>
    /// Generates Voronoi biome data for a given chunk or retrieves it if already generated.
    /// </summary>
    /// <param name="chunkCoord">The coordinates of the chunk to generate.</param>
    /// <param name="scale">The scale of the Voronoi cells.</param>
    /// <param name="numPoints">The number of Voronoi points to generate for the chunk.</param>
    /// <param name="availableBiomes">A list of biomes to assign to the Voronoi points.</param>
    /// <param name="seed">A seed for random number generation, ensuring reproducibility.</param>
    /// <param name="useWeightedBiome">Whether to use weighted biome selection.</param>
    public static void GenerateChunkVoronoi(Vector2Int chunkCoord, float scale, int numPoints, List<Biome> availableBiomes, int seed, bool useWeightedBiome)
    {
        // Ensure thread-safe access to the shared dictionary.
        lock (LockObject)
        {
            // If the chunk's Voronoi points have already been generated, do nothing.
            if (ChunkVoronoiPoints.ContainsKey(chunkCoord))
                return;

            var chunkPoints = new List<VoronoiPoint>();
            // Create a random generator with a seed based on the chunk coordinates.
            var random = new System.Random(GenerateSeed(chunkCoord, seed));

            // Generate the specified number of Voronoi points for the chunk.
            for (int i = 0; i < numPoints; i++)
            {
                // Generate a random position for the Voronoi point.
                var randomPoint = GenerateRandomPoint(chunkCoord, scale, random);
                // Select a random biome from the available biomes, considering or not their weights.
                Biome selectedBiome = useWeightedBiome
                   ? WeightedRandomBiome(availableBiomes, random)
                   : availableBiomes[random.Next(availableBiomes.Count)];
                // Add the Voronoi point to the chunk's points list.
                chunkPoints.Add(new VoronoiPoint(randomPoint, selectedBiome));
            }

            ChunkVoronoiPoints[chunkCoord] = chunkPoints;
        }
    }

    /// <summary>
    /// Determines the biome at a given world position using Voronoi-based logic.
    /// </summary>
    /// <param name="worldPosition">The world position to evaluate.</param>
    /// <param name="scale">The scale of the Voronoi cells.</param>
    /// <param name="numPoints">The number of Voronoi points per chunk.</param>
    /// <param name="availableBiomes">List of available biomes for selection.</param>
    /// <param name="seed">Seed for random generation.</param>
    /// <returns>The closest biome at the specified world position.</returns>
    public static Biome GetBiomeAtPosition(Vector2 worldPosition, float scale, int numPoints, List<Biome> availableBiomes, int seed, bool useWeightedBiome)
    {
        // Determine the chunk coordinates for the world position.
        var chunkCoord = GetChunkCoord(worldPosition, scale);
        // Ensure the chunk and its neighboring chunks have their Voronoi points generated.
        EnsureChunkAndNeighbors(chunkCoord, scale, numPoints, availableBiomes, seed, useWeightedBiome);
        // Variable to track the closest biome.
        Biome closestBiome = null;
        // Variable to track the minimum distance squared (avoiding expensive square roots).
        float minDistanceSquared = float.MaxValue;
        // Iterate over all Voronoi points in the relevant chunks.
        foreach (var point in GetRelevantVoronoiPoints(chunkCoord))
        {
            // Calculate the squared distance between the world position and the Voronoi point.
            float distanceSquared = (worldPosition - point.Position).sqrMagnitude;

            // If this point is closer, update the closest biome.
            if (distanceSquared < minDistanceSquared)
            {
                minDistanceSquared = distanceSquared;
                closestBiome = point.AssignedBiome;
            }
        }

        return closestBiome;
    }

    /// <summary>
    /// Generates a random point within the specified chunk.
    /// </summary>
    /// <param name="chunkCoord">The chunk coordinates.</param>
    /// <param name="scale">The scale of the Voronoi cells.</param>
    /// <param name="random">The random number generator.</param>
    /// <returns>A random point within the chunk.</returns>
    private static Vector2 GenerateRandomPoint(Vector2Int chunkCoord, float scale, System.Random random)
    {
        // Calculate a random point within the chunk based on the scale.
        float x = chunkCoord.x * scale + (float)random.NextDouble() * scale;
        float y = chunkCoord.y * scale + (float)random.NextDouble() * scale;
        return new Vector2(x, y);
    }

    /// <summary>
    /// Determines the chunk coordinates for a given world position based on the scale of the Voronoi cells.
    /// </summary>
    /// <param name="worldPosition">The world position.</param>
    /// <param name="scale">The scale of the Voronoi cells.</param>
    /// <returns>The chunk coordinates.</returns>
    private static Vector2Int GetChunkCoord(Vector2 worldPosition, float scale)
    {
        // Determine the chunk coordinates by dividing the world position by the scale.
        int x = Mathf.FloorToInt(worldPosition.x / scale);
        int y = Mathf.FloorToInt(worldPosition.y / scale);
        return new Vector2Int(x, y);
    }

    /// <summary>
    /// Ensures that the specified chunk and its neighboring chunks have their Voronoi points generated.
    /// </summary>
    /// <param name="chunkCoord">The chunk coordinates to check.</param>
    /// <param name="scale">The scale of the Voronoi cells.</param>
    /// <param name="numPoints">The number of points to generate per chunk.</param>
    /// <param name="availableBiomes">The list of available biomes.</param>
    /// <param name="seed">The seed for random generation.</param>
    /// <param name="useWeightedBiome">Whether to use weighted biome selection.</param>
    private static void EnsureChunkAndNeighbors(Vector2Int chunkCoord, float scale, int numPoints, List<Biome> availableBiomes, int seed, bool useWeightedBiome)
    {
        // Get the neighboring chunks that do not yet have Voronoi points generated.
        var neighbors = GetAdjacentChunks(chunkCoord).Where(neighbor =>
        {
            lock (LockObject)
            {
                return !ChunkVoronoiPoints.ContainsKey(neighbor);
            }
        });

        // Generate Voronoi points for the neighboring chunks.
        foreach (var neighbor in neighbors)
        {
            GenerateChunkVoronoi(neighbor, scale, numPoints, availableBiomes, seed, useWeightedBiome);
        }
    }
    /// <summary>
    /// Returns the coordinates of the neighboring chunks surrounding a given chunk.
    /// </summary>
    /// <param name="chunkCoord">The chunk coordinates.</param>
    /// <returns>An enumerable of neighboring chunk coordinates.</returns>
    private static IEnumerable<Vector2Int> GetAdjacentChunks(Vector2Int chunkCoord)
    {
        // Yield the current chunk and its 8 surrounding neighbors.
        yield return chunkCoord;
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x != 0 || y != 0) // Skip the center chunk since it's already included.
                    yield return chunkCoord + new Vector2Int(x, y);
            }
        }
    }

    /// <summary>
    /// Retrieves all Voronoi points from the specified chunk and its neighboring chunks.
    /// </summary>
    /// <param name="chunkCoord">The chunk coordinates.</param>
    /// <returns>A list of Voronoi points from the relevant chunks.</returns>
    private static List<VoronoiPoint> GetRelevantVoronoiPoints(Vector2Int chunkCoord)
    {
        // Create a list to store the Voronoi points from the relevant chunks.
        var points = new List<VoronoiPoint>();
        foreach (var neighbor in GetAdjacentChunks(chunkCoord))
        {
            // Iterate through the chunk and its neighbors to gather all Voronoi points.
            lock (LockObject)
            {
                // If the neighboring chunk has Voronoi points, add them to the list.
                if (ChunkVoronoiPoints.TryGetValue(neighbor, out var neighborPoints))
                {
                    points.AddRange(neighborPoints);
                }
            }
        }

        return points;
    }

    /// <summary>
    /// Generates a unique seed using the chunk coordinates and a base seed.
    /// </summary>
    /// <param name="chunkCoord">The coordinates of the chunk.</param>
    /// <param name="baseSeed">The base seed for randomness.</param>
    /// <returns>A unique integer seed for the given chunk.</returns>
    private static int GenerateSeed(Vector2Int chunkCoord, int baseSeed)
    {
        // Generate a unique seed using hash functions and chunk coordinates.
        return baseSeed + chunkCoord.x * 73856093 ^ chunkCoord.y * 19349663;
    }
    /// <summary>
    /// Selects a biome randomly from a list of biomes, taking into account the weights of each biome.
    /// </summary>
    /// <param name="biomes">The list of available biomes with associated weights.</param>
    /// <param name="random">The random number generator to use.</param>
    /// <returns>A randomly selected biome, based on weight distribution.</returns>
    private static Biome WeightedRandomBiome(List<Biome> biomes, System.Random random)
    {
        // Calculate the total weight of all biomes.
        float totalWeight = biomes.Sum(b => b.weight);

        // Generate a random value within the range of the total weight.
        float randomValue = (float)random.NextDouble() * totalWeight;

        // Iterate through the biomes to find the one corresponding to the random value.
        foreach (var biome in biomes)
        {
            if (randomValue < biome.weight)
                return biome;

            randomValue -= biome.weight;
        }

        // Return the first biome as a fallback in case of an error.
        return biomes[0];
    }
}
