using System.Collections.Generic;
using UnityEngine;

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
    private static readonly Dictionary<Vector2Int, List<VoronoiPoint>> ChunkVoronoiPoints = new Dictionary<Vector2Int, List<VoronoiPoint>>();

    // Global random instance for generating random numbers.
    private static readonly System.Random GlobalRandom = new System.Random();

    // Lock object used to ensure thread-safe access to the ChunkVoronoiPoints dictionary.
    private static readonly object LockObject = new object();

    /// <summary>
    /// Generates Voronoi biome data for a given chunk or retrieves it if already generated.
    /// </summary>
    /// <param name="chunkCoord">The coordinates of the chunk to generate.</param>
    /// <param name="scale">The scale of the Voronoi cells.</param>
    /// <param name="numPoints">The number of Voronoi points to generate for the chunk.</param>
    /// <param name="availableBiomes">A list of biomes to assign to the Voronoi points.</param>
    /// <param name="seed">A seed for random number generation, ensuring reproducibility.</param>
    public static void GenerateChunkVoronoi(Vector2Int chunkCoord, float scale, int numPoints, List<Biome> availableBiomes, int seed)
    {
        // Ensure thread-safe access to the shared dictionary.
        lock (LockObject)
        {
            // If the chunk's Voronoi points have already been generated, do nothing.
            if (ChunkVoronoiPoints.ContainsKey(chunkCoord))
                return;

            // List to hold the Voronoi points generated for this chunk.
            var chunkPoints = new List<VoronoiPoint>();

            // Create a random generator with a seed based on the chunk coordinates.
            var random = new System.Random(seed + chunkCoord.x * 73856093 ^ chunkCoord.y * 19349663);

            // Generate the specified number of Voronoi points for the chunk.
            for (int i = 0; i < numPoints; i++)
            {
                // Generate a random position for the Voronoi point.
                Vector2 randomPoint = GenerateRandomPoint(chunkCoord, scale, random);

                // Select a random biome from the available biomes.
                Biome randomBiome = availableBiomes[random.Next(availableBiomes.Count)];

                // Add the Voronoi point to the chunk's points list.
                chunkPoints.Add(new VoronoiPoint(randomPoint, randomBiome));
            }

            // Store the generated Voronoi points for the chunk in the dictionary.
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
    public static Biome GetBiomeAtPosition(Vector2 worldPosition, float scale, int numPoints, List<Biome> availableBiomes, int seed)
    {
        // Determine the chunk coordinates for the world position.
        Vector2Int chunkCoord = GetChunkCoord(worldPosition, scale);

        // Ensure the chunk and its neighboring chunks have their Voronoi points generated.
        EnsureChunkAndNeighbors(chunkCoord, scale, numPoints, availableBiomes, seed);

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

        // Return the closest biome.
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
    private static void EnsureChunkAndNeighbors(Vector2Int chunkCoord, float scale, int numPoints, List<Biome> availableBiomes, int seed)
    {
        // Loop through the chunk and its adjacent chunks to ensure all Voronoi points are generated.
        foreach (var neighbor in GetAdjacentChunks(chunkCoord))
        {
            lock (LockObject) // Ensure thread-safe access to shared resources.
            {
                // If the neighboring chunk does not have Voronoi points, generate them.
                if (!ChunkVoronoiPoints.ContainsKey(neighbor))
                {
                    GenerateChunkVoronoi(neighbor, scale, numPoints, availableBiomes, seed);
                }
            }
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
        yield return chunkCoord + Vector2Int.left;
        yield return chunkCoord + Vector2Int.right;
        yield return chunkCoord + Vector2Int.up;
        yield return chunkCoord + Vector2Int.down;
        yield return chunkCoord + Vector2Int.up + Vector2Int.left;
        yield return chunkCoord + Vector2Int.up + Vector2Int.right;
        yield return chunkCoord + Vector2Int.down + Vector2Int.left;
        yield return chunkCoord + Vector2Int.down + Vector2Int.right;
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

        // Iterate through the chunk and its neighbors to gather all Voronoi points.
        foreach (var neighbor in GetAdjacentChunks(chunkCoord))
        {
            // If the neighboring chunk has Voronoi points, add them to the list.
            if (ChunkVoronoiPoints.TryGetValue(neighbor, out var neighborPoints))
            {
                points.AddRange(neighborPoints);
            }
        }

        // Return the gathered Voronoi points.
        return points;
    }
}
