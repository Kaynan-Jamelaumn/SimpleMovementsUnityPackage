using System;
using System.Collections.Generic;
using UnityEngine;

public static class ObjectSpawner
{

    /// <summary>
    /// Places objects on the terrain for a given biome, considering various factors like clustering, probability of spawn, and slope constraints.
    /// </summary>
    /// <param name="chunkTransform">The transform of the chunk to attach the objects to.</param>
    /// <param name="worldPosition">The world position of the terrain cell being processed.</param>
    /// <param name="biome">The biome in which the object will be placed.</param>
    /// <param name="heightMap">The height map that provides the height values for the terrain.</param>
    /// <param name="x">The X index of the terrain cell being processed.</param>
    /// <param name="y">The Y index of the terrain cell being processed.</param>
    public static void PlaceObjectsForBiome(Transform chunkTransform, Vector3 worldPosition, BiomeInstance biomeDefinition,  float[,] heightMap, int x, int y)
    {
        // Iterate through all objects defined in the biome
        foreach (BiomeObject biomeObject in biomeDefinition.runtimeObjects)
        {
            // Skip if the maximum number of objects has been reached for this biome object
             if (biomeObject.hasMaxNumberOfObjects && biomeObject.currentNumberOfThisObject >= biomeObject.maxNumberOfThisObject)

            continue;


            float adjustedProbability = biomeObject.probabilityToSpawn;
            // Adjust the probability to spawn based on clustering (if enabled)
            if (biomeObject.isClusterable)
            {
                // Retrieve density map value for clustering at the current cell
                float densityWeight = biomeObject.densityMap[x, y];
                // Increase spawn probability near cluster centers by amplifying the density weight
                adjustedProbability *= Mathf.Pow(densityWeight, 2); // Amplify clustering
            }

            // Check if an object should be spawned based on a random probability roll
            if ((UnityEngine.Random.value * 100) < adjustedProbability)
            {
                // Get the position to spawn the object, including terrain height
                Vector3 spawnPosition = GetSpawnPosition(worldPosition, heightMap, x, y);

                // Calculate the slope at the spawn position and skip placement if it exceeds the allowed slope threshold for the object
                Vector3 normal = CalculateTerrainNormal(heightMap, x, y);
                if (Vector3.Angle(Vector3.up, normal) > biomeObject.slopeThreshold)
                    continue;

                // Check if the spawn position is free of overlapping objects
                if (!IsPositionFree(spawnPosition, biomeObject.terrainObject))
                    continue;

                // Instantiate the object at the determined position with correct rotation
                InstantiateBiomeObject(chunkTransform, biomeObject.terrainObject, spawnPosition, heightMap, x, y);
                biomeObject.currentNumberOfThisObject++; // Increment the count of objects placed

            }

        }
    }
    /// <summary>
    /// Retrieves the position on the terrain where an object should be spawned based on the height map at the given coordinates.
    /// </summary>
    /// <param name="worldPosition">The world position of the terrain cell being processed.</param>
    /// <param name="heightMap">The height map that contains terrain height values.</param>
    /// <param name="x">The X index of the terrain cell.</param>
    /// <param name="y">The Y index of the terrain cell.</param>
    /// <returns>The 3D position (X, Y, Z) for spawning the object with correct height.</returns>
    private static Vector3 GetSpawnPosition(Vector3 worldPosition, float[,] heightMap, int x, int y)
    {

        // Retrieve the height value at the specific (x, y) location from the height map
        float height = heightMap[x, y];

        // Return the position with the correct height (Y-value) for the terrain
        return new Vector3(worldPosition.x, height, worldPosition.z);
    }
    /// <summary>
    /// Instantiates a biome object at a given position with a correct rotation, based on terrain normals.
    /// </summary>
    /// <param name="chunkTransform">The transform of the chunk to parent the object to.</param>
    /// <param name="prefab">The prefab of the biome object to instantiate.</param>
    /// <param name="position">The position where the object should be spawned.</param>
    /// <param name="heightMap">The height map used to adjust the object's placement.</param>
    /// <param name="x">The X index of the terrain cell being processed.</param>
    /// <param name="y">The Y index of the terrain cell being processed.</param>
    private static void InstantiateBiomeObject(Transform chunkTransform, GameObject prefab, Vector3 position, float[,] heightMap, int x, int y)
    {
        // Calculate the terrain normal at the given position (X, Y)
        Vector3 normal = CalculateTerrainNormal(heightMap, x, y);

        // Randomize the Y-axis rotation of the object to make its orientation more varied
        Quaternion randomYRotation = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);

        // Align the object’s up direction with the terrain’s normal (the slope of the terrain)
        Quaternion terrainAlignmentRotation = Quaternion.FromToRotation(Vector3.up, normal);

        // Combine both rotations: terrain alignment and random Y-axis rotation
        Quaternion finalRotation = terrainAlignmentRotation * randomYRotation;

        // Instantiate the object at the determined position with the calculated final rotation
        GameObject obj = UnityEngine.Object.Instantiate(prefab, position, finalRotation);
        obj.transform.parent = chunkTransform; // Parent the object to the chunk's transform
    }
    /// <summary>
    /// Calculates the terrain normal at a given position based on the surrounding height values from the height map.
    /// </summary>
    /// <param name="heightMap">The height map used to calculate the terrain's normals.</param>
    /// <param name="x">The X index of the terrain cell.</param>
    /// <param name="y">The Y index of the terrain cell.</param>
    /// <returns>The calculated normal vector of the terrain at the given position.</returns>
    private static Vector3 CalculateTerrainNormal(float[,] heightMap, int x, int y)
    {
        // Get the height values of the neighboring cells (left, right, down, up)
        float heightL = heightMap[Mathf.Max(0, x - 1), y];
        float heightR = heightMap[Mathf.Min(heightMap.GetLength(0) - 1, x + 1), y];
        float heightD = heightMap[x, Mathf.Max(0, y - 1)];
        float heightU = heightMap[x, Mathf.Min(heightMap.GetLength(1) - 1, y + 1)];

        // Calculate the normal using the cross product of vectors representing the slope in X and Y directions
        Vector3 normal = new Vector3(heightL - heightR, 2f, heightD - heightU).normalized;

        return normal;
    }

    /// <summary>
    /// Generates a density map for clustering objects on the terrain, using Perlin noise and cluster centers.
    /// </summary>
    /// <param name="width">The width of the density map.</param>
    /// <param name="height">The height of the density map.</param>
    /// <param name="clusterCount">The number of clusters to generate.</param>
    /// <param name="clusterRadius">The radius of each cluster.</param>
    /// <param name="baseFrequency">The frequency of Perlin noise for base density.</param>
    /// <param name="amplitude">The amplitude of Perlin noise to scale the density.</param>
    /// <returns>A 2D array representing the density map values, where higher values indicate more dense areas.</returns>
    public static float[,] GenerateClusteredDensityMap(int width, int height, int clusterCount, float clusterRadius, float baseFrequency, float amplitude, float scaleFactor)
    {
        //amplitude: Scales the values of the noise. Higher values increase the intensity of the noise (making the contrast between low and high values more noticeable).
        //baseFrequency: Controls the "zoom level" of the Perlin noise pattern. Higher values create tighter noise patterns (smaller "islands"), while lower values produce broader, smoother patterns.

        // Initialize an empty density map
        float[,] densityMap = new float[width, height];
        System.Random prng = new System.Random();

        Vector2[] clusterCenters = new Vector2[clusterCount];
        clusterRadius = clusterRadius * scaleFactor; // Scale factor to adjust cluster size
        float[] clusterRadii = new float[clusterCount];

        // Generate random cluster centers and radii
        for (int i = 0; i < clusterCount; i++)
        {
            clusterCenters[i] = new Vector2(prng.Next(0, width), prng.Next(0, height));
            clusterRadii[i] = clusterRadius * (0.8f + (0.4f * (float)prng.NextDouble())); // Slight radius variation between 80% and 120%
            //Case 1: prng.NextDouble() = 0.0: Multiplier: 0.8f + 0.4f * 0.0 = 0.8
            //Case 2: prng.NextDouble() = 0.5: Multiplier: 0.8f + 0.4f * 0.5 = 0.8 + 0.2 = 1.0
            //Case 3: prng.NextDouble() = 1.0: Multiplier: 0.8f + 0.4f * 1.0 = 0.8 + 0.4 = 1.2

        }
        // Loop through every cell in the density map

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float maxDensity = 0f;

                // Calculate density contribution from each cluster
                foreach (Vector2 center in clusterCenters)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center); // the distance from the current cell position to the cluster center
                    float radius = clusterRadii[Array.IndexOf(clusterCenters, center)];
                    if (distance < radius) //if the distance from the cluster center is less than the cluster radius(is inside the cluster area then...)
                    {
                        // float clusterDensity = Mathf.Max(0, 1 - (distance / radius)); // Linear falloff
                        //Distance: 0      0.25   0.5   0.75   1.0(clusterRadius)
                        //Value:    1.0    0.75   0.5   0.25   0.0


                        float clusterDensity = Mathf.Pow(1 - (distance / radius), 2); // Exponential falloff   // Quadratic falloff: closer to the center -> higher density
                        //Distance: 0      0.25   0.5   0.75   1.0(clusterRadius)
                        // Value:   1.0    0.56   0.25  0.06   0.0
                        maxDensity = Mathf.Max(maxDensity, clusterDensity);
                    }
                }

                // Combine the cluster density with Perlin noise for overall terrain density
                float baseDensity = Mathf.PerlinNoise(x * baseFrequency, y * baseFrequency) * amplitude;


                // Calculate the final density value and clamp it between 0 and 1
                float rawDensity = Mathf.Clamp01(maxDensity + baseDensity);
                // Normalize the value to be between 1 and 2
                densityMap[x, y] = 1 + rawDensity; // This will map rawDensity from [0, 1] to [1, 2]
            }
        }

        return densityMap;
    }
    /// <summary>
    /// Checks if the specified position is free of any overlapping objects, ensuring that the spawn location is valid for placing a new object.
    /// </summary>
    /// <param name="position">The position in world space where the object will be placed.</param>
    /// <param name="objectPrefab">The prefab of the object being placed, used to get its collider for overlap checks.</param>
    /// <returns>
    /// Returns `true` if the position is free of any overlapping objects (other than the terrain), otherwise `false` if there is overlap.
    /// </returns>
    private static bool IsPositionFree(Vector3 position, GameObject objectPrefab)
    {
        // Get the collider component attached to the prefab (used for determining its size and boundaries)
        Collider collider = objectPrefab.GetComponent<Collider>();
        // If the prefab doesn't have a collider, it cannot be placed, so return false
        if (collider == null)
        {
            Debug.LogError("No Collider Found for the Object Cant Spawn:" + objectPrefab.name);
            return false;
        }

        // Calculate the size of the object (bounds size) and derive half of it to perform the overlap check
        Vector3 objectSize = collider.bounds.size;
        Vector3 halfExtents = objectSize / 2f;

        // Perform an overlap box check to detect other colliders in the area
        Collider[] hitColliders = Physics.OverlapBox(position, halfExtents, Quaternion.identity);
        // Iterate through all colliders found in the overlap area
        foreach (Collider hitCollider in hitColliders)
        {
            // If the hit object is the terrain (ground), we ignore it since it's acceptable
        
            if (hitCollider.gameObject.CompareTag("Ground"))
            {
                continue; // Ignore terrain
            }
            return false; // If a non-terrain object overlaps, return false as the position is not free
        }

        return true; // No overlap with non-terrain objects, return true to indicate the position is free
    }


}

