using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
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
    public static void PlaceObjectsForBiome(
           Transform chunkTransform,
           Vector3 worldPosition,
           BiomeInstance biomeDefinition,
           float[,] heightMap,
           int x,
           int y,
           MeshData meshData,
           int lodFactor
       )
    {
        foreach (BiomeObject biomeObject in biomeDefinition.runtimeObjects)
        {
            if (biomeObject.hasMaxNumberOfObjects &&
                biomeObject.currentNumberOfThisObject >= biomeObject.maxNumberOfThisObject)
                continue;

            // Check if an object should be spawned based on a random probability roll
            float adjustedProbability = AdjustSpawnProbability(biomeObject, x, y);
            if (UnityEngine.Random.value * 100 >= adjustedProbability)
                continue;


            // Check if a valid spawn position and normal can be determined.
            // The method returns `false` if the position cannot be calculated (e.g., invalid terrain or out of bounds).
            // Additionally, ensure the terrain slope is within the allowable threshold for the biome object
            // by comparing the angle between the surface normal and the upward vector (Vector3.up).
            // Finally, verify that the calculated spawn position is not occupied by another object.

            if (TryGetSpawnPositionAndNormal(worldPosition, heightMap, meshData, chunkTransform, x, y, lodFactor,
                    out var spawnPosition, out var normal) &&
                IsValidSpawnPosition(spawnPosition, normal, biomeObject))
            {
                InstantiateBiomeObject(chunkTransform, biomeObject.terrainObject, spawnPosition, normal);
                biomeObject.currentNumberOfThisObject++;
            }
        }
    }
    /// <summary>
    /// Adjusts the probability of spawning based on the density at a given position. This accounts for whether the biome object is clusterable or not.
    /// </summary>
    /// <param name="biomeObject">The biome object representing the type of object being spawned.</param>
    /// <param name="x">The x-coordinate on the density map for the spawn position(Terrain Cell).</param>
    /// <param name="y">The y-coordinate on the density map for the spawn position(Terrain Cell).</param>
    /// <returns>The adjusted spawn probability.</returns>
    /// <remarks>
    /// If clustering is enabled for the given biome object, the spawn probability is adjusted using the density weight at the specified (x, y) position.
    /// The probability is amplified based on the square of the density weight.
    /// </remarks>
    private static float AdjustSpawnProbability(BiomeObject biomeObject, int x, int y)
    {
        if (!biomeObject.isClusterable)
            return biomeObject.probabilityToSpawn;
        // Adjust the probability to spawn based on clustering (if enabled)
        float densityWeight = biomeObject.densityMap[x, y];

        // Increase spawn probability near cluster centers by amplifying the density weight
        return biomeObject.probabilityToSpawn * Mathf.Pow(densityWeight, 2);
    }
    /// <summary>
    /// Attempts to calculate the spawn position and surface normal for an object.
    /// </summary>
    /// <param name="worldPosition">World position of the terrain cell.</param>
    /// <param name="heightMap">Height map for terrain data.</param>
    /// <param name="meshData">Mesh data for finer detail.</param>
    /// <param name="chunkTransform">Transform of the terrain chunk.</param>
    /// <param name="x">X index of the cell.</param>
    /// <param name="y">Y index of the cell.</param>
    /// <param name="lodFactor">Level of detail factor for mesh data.</param>
    /// <param name="spawnPosition">Calculated spawn position (output).</param>
    /// <param name="normal">Calculated surface normal (output).</param>
    /// <returns>True if position and normal were successfully determined; otherwise false.</returns>
    private static bool TryGetSpawnPositionAndNormal(
          Vector3 worldPosition,
          float[,] heightMap,
          MeshData meshData,
          Transform chunkTransform,
          int x,
          int y,
          int lodFactor,
          out Vector3 spawnPosition,
          out Vector3 normal
      )
    {
        float height;

        // ===========================================================================================
        // ===========================================================================================
        // When LOD (Level of Detail) is used, the terrain mesh is simplified. 
        // For example, with LOD 6:
        //   - lodFactor = 12 (calculated as lod * 2)
        //   - Instead of using ALL 241x241 heightmap points, the mesh only uses every 12th point
        //   - So it samples at positions: 0, 12, 24, 36, 48... creating a simplified mesh
        //
        // THE ORIGINAL PROBLEM:
        // - heightmap has heights at EVERY position: [0,1,2,3,4,5,6,7,8,9,10,11,12,13,14...]
        // -  LOD mesh only uses heights at positions: [0, 12, 24, 36, 48...]
        // - When placing an object at position (6, 6), using heightMap[6,6] directly gives the
        //   EXACT height at that position - but the mesh doesn't have a vertex there!
        // - The mesh only has vertices at corners like (0,0), (12,0), (0,12), (12,12) and draws
        //   flat triangles between them.
        // ===========================================================================================

        if (lodFactor > 1)
        {
            // When LOD is active, we MUST calculate the height that matches the simplified mesh
            // This uses triangle interpolation to match exactly what the player sees
            height = GetLODInterpolatedHeight(heightMap, x, y, lodFactor);
            normal = GetLODInterpolatedNormal(heightMap, x, y, lodFactor);
        }
        else
        {
            // No LOD (lodFactor = 1), the mesh uses every heightmap point, 
            // so we can use the exact height from heightmap
            height = heightMap[x, y];
            normal = CalculateTerrainNormal(heightMap, x, y);
        }

        spawnPosition = new Vector3(worldPosition.x, height, worldPosition.z);
        return true;
    }

    /// <summary>
    /// Gets the interpolated height that matches the LOD mesh triangulation.
    /// This matches the exact calculation used by MeshGenerator when creating the simplified mesh.
    /// </summary>
    private static float GetLODInterpolatedHeight(float[,] heightMap, int x, int y, int lodFactor)
    {
        // ===========================================================================================
        // For a single LOD cell from (0,0) to (12,12), the mesh creates:
        //
        //   (0,12) ●─────────● (12,12)
        //          │\        │
        //          │ \       │
        //          │  \      │
        //          │   \     │
        //          │    \    │
        //          │     \   │
        //          │      \  │
        //          │       \ │
        //          │        \│
        //   (0,0)  ●─────────● (12,0)
        //
        // Triangle 1 (Lower-left): Connects points (0,0), (0,12), (12,0)
        // Triangle 2 (Upper-right): Connects points (12,0), (0,12), (12,12)
        // ===========================================================================================

        int mapWidth = heightMap.GetLength(0);
        int mapHeight = heightMap.GetLength(1);

        // ===========================================================================================
        // STEP 1: Find which LOD grid cell contains this point
        // ===========================================================================================
        // Example: For position (6, 6) with lodFactor = 12:
        //   x0 = (6 / 12) * 12 = 0 * 12 = 0    (lower bound)
        //   y0 = (6 / 12) * 12 = 0 * 12 = 0    (lower bound)
        //   x1 = 0 + 12 = 12                   (upper bound)
        //   y1 = 0 + 12 = 12                   (upper bound)
        // So we're in the cell from (0,0) to (12,12)
        // ===========================================================================================
        int x0 = (x / lodFactor) * lodFactor;  // Lower-left corner X
        int y0 = (y / lodFactor) * lodFactor;  // Lower-left corner Y
        int x1 = Mathf.Min(x0 + lodFactor, mapWidth - 1);   // Upper-right corner X
        int y1 = Mathf.Min(y0 + lodFactor, mapHeight - 1);   // Upper-right corner Y

        // ===========================================================================================
        // STEP 2: Get the four corner heights from the heightmap
        // ===========================================================================================
        // These are the ACTUAL heights that the mesh uses for its vertices
        // Example heights:
        //   h00 = heightMap[0, 0]   = 10.0  (bottom-left)
        //   h10 = heightMap[12, 0]  = 14.0  (bottom-right)  
        //   h01 = heightMap[0, 12]  = 12.0  (top-left)
        //   h11 = heightMap[12, 12] = 18.0  (top-right)
        // ===========================================================================================
        float h00 = heightMap[x0, y0];  // bottom-left corner height
        float h10 = heightMap[x1, y0];  // bottom-right corner height
        float h01 = heightMap[x0, y1];  // top-left corner height
        float h11 = heightMap[x1, y1];  // top-right corner height

        // ===========================================================================================
        // STEP 3: Calculate position within the LOD cell (normalized to 0-1 range)
        // ===========================================================================================
        // This tells us where we are within the cell as a fraction
        // Example: For position (6, 6) in a cell from (0,0) to (12,12):
        //   fx = (6 - 0) / (12 - 0) = 6/12 = 0.5  (halfway across horizontally)
        //   fy = (6 - 0) / (12 - 0) = 6/12 = 0.5  (halfway up vertically)
        // ===========================================================================================
        float fx = (x1 > x0) ? (float)(x - x0) / (float)(x1 - x0) : 0f;
        float fy = (y1 > y0) ? (float)(y - y0) / (float)(y1 - y0) : 0f;

        // ===========================================================================================
        // STEP 4: Determine which triangle we're in
        // ===========================================================================================
        // The diagonal line from (0,1) to (1,0) divides the square:
        //   - If fx + fy <= 1.0: We're in Triangle 1 (lower-left triangle)
        //   - If fx + fy > 1.0:  We're in Triangle 2 (upper-right triangle)
        //
        // Example: Position (6,6) has fx=0.5, fy=0.5
        //   fx + fy = 0.5 + 0.5 = 1.0 (on the edge, counts as Triangle 1)
        //
        // Another example: Position (9,9) has fx=0.75, fy=0.75
        //   fx + fy = 0.75 + 0.75 = 1.5 > 1.0 (in Triangle 2)
        // ===========================================================================================

        if (fx + fy <= 1.0f)
        {
            // =====================================================================================
            // TRIANGLE 1 (Lower-left triangle)
            // =====================================================================================
            // This triangle uses vertices: (x0,y0), (x1,y0), (x0,y1)
            // Or in our example: (0,0), (12,0), (0,12)
            //
            // The height is calculated using barycentric interpolation:
            // We start from h00 (bottom-left) and add:
            //   - Horizontal contribution: fx * (h10 - h00)
            //   - Vertical contribution:   fy * (h01 - h00)
            //
            // EXAMPLE CALCULATION for position (6,6):
            //   height = 10.0 + 0.5 * (14.0 - 10.0) + 0.5 * (12.0 - 10.0)
            //   height = 10.0 + 0.5 * 4.0 + 0.5 * 2.0
            //   height = 10.0 + 2.0 + 1.0
            //   height = 13.0
            //
            // This gives us the EXACT height on the flat triangle surface!
            // =====================================================================================
            return h00 + fx * (h10 - h00) + fy * (h01 - h00);
        }
        else
        {
            // =====================================================================================
            // TRIANGLE 2 (Upper-right triangle)
            // =====================================================================================
            // This triangle uses vertices: (x1,y0), (x0,y1), (x1,y1)
            // Or in our example: (12,0), (0,12), (12,12)
            //
            // For this triangle, we need to remap our coordinates:
            // We measure from the opposite corner (1,1) back towards (0,0)
            //   u = 1.0 - fx  (horizontal distance from right edge)
            //   v = 1.0 - fy  (vertical distance from top edge)
            //
            // EXAMPLE CALCULATION for position (9,9) where fx=0.75, fy=0.75:
            //   u = 1.0 - 0.75 = 0.25
            //   v = 1.0 - 0.75 = 0.25
            //   height = 18.0 + 0.25 * (12.0 - 18.0) + 0.25 * (14.0 - 18.0)
            //   height = 18.0 + 0.25 * (-6.0) + 0.25 * (-4.0)
            //   height = 18.0 - 1.5 - 1.0
            //   height = 15.5
            // =====================================================================================
            float u = 1.0f - fx;
            float v = 1.0f - fy;
            return h11 + u * (h01 - h11) + v * (h10 - h11);
        }

        // ===========================================================================================
        // WHY THIS WORKS - THE KEY INSIGHT:
        // ===========================================================================================
        // The mesh is made of FLAT TRIANGLES, not smooth surfaces!
        // Each triangle is a flat plane in 3D space.
        //
        // VISUAL COMPARISON:
        // What your heightmap has (lots of detail):
        //      *
        //     * *
        //    *   *     <- Many height values
        //   *     *
        //  *       *
        // *         *
        //
        // What LOD 6 mesh shows (simplified triangles):
        //          /\
        //         /  \
        //        /    \      <- Just flat triangles!
        //       /      \
        //      /        \
        //     /          \
        //
        // THE PROBLEM WE SOLVED:
        // - Before: Objects were placed at the * height (exact heightmap value)
        // - But: The mesh only shows the triangle surface /\
        // - Result: Objects appeared floating above or sinking below the visible terrain!
        // - Now: We calculate the exact height ON the triangle that players see
        // ===========================================================================================
    }

    /// <summary>
    /// Gets the interpolated normal that matches the LOD mesh triangulation.
    /// </summary>
    private static Vector3 GetLODInterpolatedNormal(float[,] heightMap, int x, int y, int lodFactor)
    {
        // ===========================================================================================
        // CALCULATING NORMALS FOR LOD TERRAIN
        // ===========================================================================================
        // The normal vector tells us which direction the surface is facing.
        // For LOD terrain, we need to calculate normals based on the interpolated heights,
        // not the raw heightmap, to ensure objects are aligned with the visible surface.
        //
        // We sample the interpolated heights at neighboring positions and calculate
        // the normal from those samples.
        // ===========================================================================================

        int mapWidth = heightMap.GetLength(0);
        int mapHeight = heightMap.GetLength(1);

        // Sample neighboring points using the LOD interpolation
        // This ensures our normals match the actual triangle surfaces
        float center = GetLODInterpolatedHeight(heightMap, x, y, lodFactor);
        float left = (x > 0) ? GetLODInterpolatedHeight(heightMap, x - 1, y, lodFactor) : center;
        float right = (x < mapWidth - 1) ? GetLODInterpolatedHeight(heightMap, x + 1, y, lodFactor) : center;
        float down = (y > 0) ? GetLODInterpolatedHeight(heightMap, x, y - 1, lodFactor) : center;
        float up = (y < mapHeight - 1) ? GetLODInterpolatedHeight(heightMap, x, y + 1, lodFactor) : center;

        // Calculate normal from the interpolated heights
        // The normal points perpendicular to the surface
        return new Vector3(left - right, 2f, down - up).normalized;
    }

    /// <summary>
    /// Calculates the surface normal using mesh data.
    /// </summary>
    /// <param name="meshData">Mesh data of the terrain.</param>
    /// <param name="x">X index of the cell.</param>
    /// <param name="y">Y index of the cell.</param>
    /// <returns>The calculated surface normal vector.</returns>
    private static Vector3 CalculateTerrainNormalFromMesh(MeshData meshData, int x, int y)
    {
        int vertexIndex = y * (meshData.width + 1) + x;

        // Get neighboring vertices with boundary checks
        Vector3 right = x < meshData.width ?
            meshData.vertices[vertexIndex + 1] : meshData.vertices[vertexIndex];
        Vector3 left = x > 0 ?
            meshData.vertices[vertexIndex - 1] : meshData.vertices[vertexIndex];
        Vector3 up = y < meshData.depth ?
            meshData.vertices[vertexIndex + meshData.width + 1] : meshData.vertices[vertexIndex];
        Vector3 down = y > 0 ?
            meshData.vertices[vertexIndex - (meshData.width + 1)] : meshData.vertices[vertexIndex];

        Vector3 slopeX = right - left;
        Vector3 slopeZ = up - down;
        return Vector3.Cross(slopeZ, slopeX).normalized;
    }

    /// <summary>
    /// Calculates the surface normal using the height map.
    /// </summary>
    /// <param name="heightMap">Height map of the terrain.</param>
    /// <param name="x">X index of the cell.</param>
    /// <param name="y">Y index of the cell.</param>
    /// <returns>The calculated surface normal vector.</returns>
    private static Vector3 CalculateTerrainNormal(float[,] heightMap, int x, int y)
    {
        int width = heightMap.GetLength(0);
        int height = heightMap.GetLength(1);

        float left = heightMap[Mathf.Max(0, x - 1), y];
        float right = heightMap[Mathf.Min(width - 1, x + 1), y];
        float downVal = heightMap[x, Mathf.Max(0, y - 1)];
        float upVal = heightMap[x, Mathf.Min(height - 1, y + 1)];

        return new Vector3(left - right, 2f, downVal - upVal).normalized;
    }
    private static void InstantiateBiomeObject(
          Transform chunkTransform,
          GameObject prefab,
          Vector3 position,
          Vector3 normal
      )
    {
        Quaternion yRotation = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);
        Quaternion surfaceAlignment = Quaternion.FromToRotation(Vector3.up, normal);
        GameObject obj = UnityEngine.Object.Instantiate(prefab, position, surfaceAlignment * yRotation);
        obj.transform.parent = chunkTransform;
    }
    /// <summary>
    /// Checks if a spawn position is valid based on the terrain slope and whether the position is free of obstacles.
    /// </summary>
    /// <param name="position">The 3D world position where the spawn is being considered.</param>
    /// <param name="normal">The surface normal at the spawn position, used to check slope angle.</param>
    /// <param name="biomeObject">The biome object, containing information about spawn constraints.</param>
    /// <returns>True if the position is valid for spawning, false otherwise.</returns>
    /// <remarks>
    /// The spawn position is considered valid if the angle between the surface normal and the upward direction (Vector3.up) is within the allowable slope threshold defined by the biome object.
    /// Additionally, the position must be free from obstacles, as checked by the IsPositionFree method.
    /// </remarks>
    private static bool IsValidSpawnPosition(Vector3 position, Vector3 normal, BiomeObject biomeObject)
    {
        return Vector3.Angle(Vector3.up, normal) <= biomeObject.slopeThreshold &&
               IsPositionFree(position, biomeObject.terrainObject);
    }

    /// <summary>
    /// Checks if the specified position is free of any overlapping objects, ensuring that the spawn location is valid for placing a new object.
    /// </summary>
    /// <param name="position">The position in world space where the object will be placed.</param>
    /// <param name="objectPrefab">The prefab of the object being placed, used to get its collider for overlap checks.</param>
    /// <returns>
    /// Returns `true` if the position is free of any overlapping objects (other than the terrain), otherwise `false` if there is overlap.
    /// </returns>
    private static bool IsPositionFree(Vector3 position, GameObject prefab)
    {
        // Get the collider component attached to the prefab (used for determining its size and boundaries)
        Collider collider = prefab.GetComponent<Collider>();
        if (collider == null)
        {
            Debug.LogError("No Collider Found for the Object Cant Spawn:" + prefab.name);
            return false;
        }

        // Calculate the size of the object (bounds size) and derive half of it to perform the overlap check
        Vector3 halfExtents = collider.bounds.extents;
        Collider[] overlaps = Physics.OverlapBox(position, halfExtents, Quaternion.identity);

        foreach (Collider overlap in overlaps)
        {
            if (!overlap.CompareTag("Ground")) return false;
        }
        return true;
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
    /// <param name="scaleFactor">The scale factor to adjust cluster size.</param>
    /// <returns>A 2D array representing the density map values, where higher values indicate more dense areas.</returns>
    public static float[,] GenerateClusteredDensityMap(int width, int height, int clusterCount, float clusterRadius, float baseFrequency, float amplitude, float scaleFactor)
    {
        //amplitude: Scales the values of the noise. Higher values increase the intensity of the noise (making the contrast between low and high values more noticeable).
        //baseFrequency: Controls the "zoom level" of the Perlin noise pattern. Higher values create tighter noise patterns (smaller "islands"), while lower values produce broader, smoother patterns.
        // Create native arrays for cluster centers and radii
        NativeArray<Vector2> clusterCenters = new NativeArray<Vector2>(clusterCount, Allocator.TempJob);
        NativeArray<float> clusterRadii = new NativeArray<float>(clusterCount, Allocator.TempJob);

        // Generate cluster data
        // Randomly generate cluster centers and radii
        System.Random prng = new System.Random();
        for (int i = 0; i < clusterCount; i++)
        {
            clusterCenters[i] = new Vector2(prng.Next(0, width), prng.Next(0, height));
            clusterRadii[i] = clusterRadius * scaleFactor * (0.8f + 0.4f * (float)prng.NextDouble());
            // Slight variation in radius between 80% and 120% of the scaled cluster radius
            //Case 1: prng.NextDouble() = 0.0: Multiplier: 0.8f + 0.4f * 0.0 = 0.8
            //Case 2: prng.NextDouble() = 0.5: Multiplier: 0.8f + 0.4f * 0.5 = 0.8 + 0.2 = 1.0
            //Case 3: prng.NextDouble() = 1.0: Multiplier: 0.8f + 0.4f * 1.0 = 0.8 + 0.4 = 1.2
        }

        // Create a native array for the density map
        NativeArray<float> densityMap = new NativeArray<float>(width * height, Allocator.TempJob);

        // Create and schedule the job
        DensityMapJob densityMapJob = new DensityMapJob
        {
            DensityMap = densityMap,
            ClusterCenters = clusterCenters,
            ClusterRadii = clusterRadii,
            BaseFrequency = baseFrequency,
            Amplitude = amplitude,
            Width = width
        };

        // The DensityMapJob struct is designed to calculate the density map by:
        // - Using Perlin noise to generate a base terrain density.
        // - Adding contributions from cluster centers, with a quadratic falloff for cells within cluster radii.
        // The job is parameterized with:
        // - DensityMap: The output array storing density values.
        // - ClusterCenters and ClusterRadii: Representing the location and influence of clusters.
        // - BaseFrequency and Amplitude: Controlling the Perlin noise characteristics.
        // - Width: The map's width for index calculations.


        // Schedule the job with a batch size of 64
        JobHandle jobHandle = densityMapJob.Schedule(width * height, 64);
        jobHandle.Complete(); // Wait for the job to finish

        // Copy results back to a 2D array
        // The 2D array format is necessary for seamless integration with traditional game objects or visualizations.
        // This operation ensures that the data from the NativeArray, which is optimized for job systems,
        // can be readily used in Unity's rendering pipelines or gameplay mechanics.
        float[,] result = new float[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                result[x, y] = densityMap[y * width + x];
            }
        }
        // Dispose of native arrays to free memory
        // Disposing of NativeArrays is crucial to avoid memory leaks. Unity's job system does not automatically
        // manage the memory of NativeArrays, so failing to dispose of them can lead to increased memory usage
        // and potential crashes in long-running applications.
        densityMap.Dispose();
        clusterCenters.Dispose();
        clusterRadii.Dispose();

        return result;
    }

    /// <remarks>
    /// The density map combines two components:
    /// <list type="bullet">
    /// <item>
    /// <description>Cluster-based density: Higher density near cluster centers, decreasing with distance.</description>
    /// </item>
    /// <item>
    /// <description>Perlin noise: Adds natural variation to the density map.</description>
    /// </item>
    /// </list>
    /// </remarks>
    public struct DensityMapJob : IJobParallelFor
    {
        /// <summary>
        /// The output density map, stored as a 1D array.
        /// </summary>
        [WriteOnly] public NativeArray<float> DensityMap;

        /// <summary>
        /// The coordinates of cluster centers, which define areas of increased density.
        /// </summary>
        [ReadOnly] public NativeArray<Vector2> ClusterCenters;

        /// <summary>
        /// The radii of influence for each cluster.
        /// </summary>
        [ReadOnly] public NativeArray<float> ClusterRadii;

        /// <summary>
        /// The frequency of Perlin noise for generating base density patterns. Higher values create finer noise details.
        /// </summary>
        public float BaseFrequency;

        /// <summary>
        /// The amplitude of Perlin noise, scaling its impact on density values. Larger values result in greater variations.
        /// </summary>
        public float Amplitude;

        /// <summary>
        /// The width of the density map, used for converting between 1D and 2D indexing.
        /// </summary>
        public int Width;

        /// <summary>
        /// Executes the job for each cell in the density map.
        /// </summary>
        /// <param name="index">The linear index of the cell in the 1D array.</param>
        public void Execute(int index)
        {
            int x = index % Width; // Calculate x-coordinate
            int y = index / Width; // Calculate y-coordinate

            float maxDensity = 0f;

            // Calculate density contribution from each cluster
            for (int i = 0; i < ClusterCenters.Length; i++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), ClusterCenters[i]);
                if (distance < ClusterRadii[i]) //if the distance from the cluster center is less than the cluster radius(is inside the cluster area then...)
                {
                    // float clusterDensity = Mathf.Max(0, 1 - (distance / radius)); // Linear falloff
                    //Distance: 0      0.25   0.5   0.75   1.0(clusterRadius)
                    //Value:    1.0    0.75   0.5   0.25   0.0
                    // Quadratic falloff: closer to the center -> higher density
                    // -----------------------------------------------------------------------------------------------

                    maxDensity = Mathf.Max(maxDensity, (1 - distance / ClusterRadii[i]) * (1 - distance / ClusterRadii[i]));

                    //maxDensity = math.max(maxDensity, math.pow(1 - distance / ClusterRadii[i], 2)); 
                    // Exponential falloff   // Quadratic falloff: closer to the center -> higher density

                    //Distance: 0      0.25   0.5   0.75   1.0(clusterRadius)

                    // Value:   1.0    0.56   0.25  0.06   0.0
                }
            }

            // Combine the cluster density with Perlin noise for overall terrain density
            float baseDensity = Mathf.PerlinNoise(x * BaseFrequency, y * BaseFrequency) * Amplitude;

            // Calculate the final density value and clamp it between 0 and 1
            DensityMap[index] = 1 + math.clamp(maxDensity + baseDensity, 0, 1);
            // Normalize the value to be between 1 and 2
        }
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
    public static float[,] GenerateClusteredDensityMapWithoutJobSystem(int width, int height, int clusterCount, float clusterRadius, float baseFrequency, float amplitude, float scaleFactor)
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


}