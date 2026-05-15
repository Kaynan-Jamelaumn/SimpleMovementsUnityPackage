using UnityEngine;

/// <summary>
/// Post-processes a raw noise heightmap with erosion so terrain reads as naturally weathered
/// rather than pure fractal noise: thermal erosion collapses slopes steeper than their material's
/// talus angle (scree/landslide weathering), and hydraulic erosion simulates water droplets that
/// carve valleys and deposit sediment downhill. Both are modulated per-cell by biome-driven
/// resistance (harder rock erodes less) and by the climate/rainfall field (wetter regions carry
/// more water and erode more - "climate erosion").
///
/// Both passes operate in-place on a heightmap that should include a padding margin around the
/// chunk actually being rendered (see <see cref="HeightGenerator"/>). Erosion needs neighboring
/// height data to know which way things flow, so without padding, chunk edges would erode
/// differently depending on data that doesn't exist yet - the padding gives each chunk the context
/// it needs and keeps neighboring chunks' results consistent where their padded regions overlap.
/// Droplet spawn points are derived deterministically from world-space coordinates (not a shared
/// RNG sequence), so two chunks simulating the same overlapping world area reproduce the same
/// droplets and the same erosion there, keeping the seam between chunks as small as the geometry allows.
/// </summary>
public static class ErosionGenerator
{
    private static readonly int[] NeighborDx = { -1, 0, 1, -1, 1, -1, 0, 1 };
    private static readonly int[] NeighborDy = { -1, -1, -1, 0, 0, 1, 1, 1 };

    /// <summary>
    /// Simulates gravity collapsing slopes steeper than their local talus angle, moving material
    /// downhill toward the steepest lower neighbor(s) each iteration.
    /// </summary>
    /// <param name="heights">Heightmap to erode in-place.</param>
    /// <param name="resistanceMap">Per-cell erosion resistance in [0,1] (0 = soft, 1 = hard rock), same dimensions as heights. Null treats every cell as medium resistance.</param>
    /// <param name="iterations">Number of relaxation passes to run.</param>
    /// <param name="talusAngleDegrees">Base slope angle above which material starts sliding.</param>
    /// <param name="erosionRate">Fraction (0-1) of the excess height difference moved per iteration.</param>
    public static void ThermalErode(float[,] heights, float[,] resistanceMap, int iterations, float talusAngleDegrees, float erosionRate)
    {
        int width = heights.GetLength(0);
        int height = heights.GetLength(1);
        if (width < 3 || height < 3 || iterations <= 0 || erosionRate <= 0f)
            return;

        float talusTangent = Mathf.Tan(Mathf.Clamp(talusAngleDegrees, 1f, 89f) * Mathf.Deg2Rad);
        float[] deltas = new float[8];

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    float resistance = resistanceMap != null ? resistanceMap[x, y] : 0.5f;
                    // Harder rock tolerates steeper slopes before it gives way; softer material slumps sooner.
                    float localTalusTangent = talusTangent * Mathf.Lerp(0.4f, 2.2f, resistance);

                    float currentHeight = heights[x, y];
                    float totalExcess = 0f;
                    float maxExcess = 0f;
                    bool hasTarget = false;

                    for (int n = 0; n < 8; n++)
                    {
                        int nx = x + NeighborDx[n];
                        int ny = y + NeighborDy[n];
                        float diff = currentHeight - heights[nx, ny];
                        float distance = (NeighborDx[n] != 0 && NeighborDy[n] != 0) ? 1.41421356f : 1f;
                        float slope = diff / distance;

                        if (slope > localTalusTangent)
                        {
                            deltas[n] = diff;
                            totalExcess += diff;
                            hasTarget = true;
                            if (diff > maxExcess) maxExcess = diff;
                        }
                        else
                        {
                            deltas[n] = 0f;
                        }
                    }

                    if (!hasTarget || totalExcess <= 0f)
                        continue;

                    // Softer material sheds a larger share of its excess height each pass.
                    float materialFactor = Mathf.Lerp(1f, 0.35f, resistance);
                    float amountToMove = erosionRate * materialFactor * (maxExcess * 0.5f);
                    if (amountToMove <= 0f)
                        continue;

                    for (int n = 0; n < 8; n++)
                    {
                        if (deltas[n] <= 0f) continue;
                        float share = amountToMove * (deltas[n] / totalExcess);
                        heights[x, y] -= share;
                        heights[x + NeighborDx[n], y + NeighborDy[n]] += share;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Simulates water droplets flowing downhill across the heightmap, picking up sediment on steep
    /// fast-moving stretches and depositing it where they slow down, carving natural-looking valleys,
    /// gullies and alluvial fans. Droplet start positions are derived deterministically from world-space
    /// coordinates so neighboring chunks reproduce the same droplets (and therefore the same erosion)
    /// wherever their padded regions overlap.
    /// </summary>
    public static void HydraulicErode(
        float[,] heights,
        float[,] resistanceMap,
        float[,] rainfallMap,
        Vector2Int worldOrigin,
        int seed,
        float dropletDensity,
        int maxDropletLifetime,
        float inertia,
        float sedimentCapacityFactor,
        float minSedimentCapacity,
        float erodeSpeed,
        float depositSpeed,
        float evaporateSpeed,
        float gravity,
        float erosionRadius)
    {
        int width = heights.GetLength(0);
        int depth = heights.GetLength(1);
        if (width < 5 || depth < 5 || dropletDensity <= 0f || maxDropletLifetime <= 0)
            return;

        int radius = Mathf.Clamp(Mathf.RoundToInt(erosionRadius), 1, 6);
        // Keep every droplet's full lifetime + erosion brush comfortably inside the padded array.
        int margin = radius + 2;
        if (width <= margin * 2 + 1 || depth <= margin * 2 + 1)
            return;

        // Spacing between candidate spawn points, anchored to world space (not chunk-local space) so
        // the same global grid of candidates is produced regardless of which chunk is being generated.
        float spacing = Mathf.Max(1f, Mathf.Sqrt(1f / Mathf.Max(0.0001f, dropletDensity)));
        int spacingInt = Mathf.Max(1, Mathf.RoundToInt(spacing));

        int worldStartX = Mathf.CeilToInt((worldOrigin.x + margin) / (float)spacingInt) * spacingInt;
        int worldStartY = Mathf.CeilToInt((worldOrigin.y + margin) / (float)spacingInt) * spacingInt;
        int worldEndX = worldOrigin.x + width - margin;
        int worldEndY = worldOrigin.y + depth - margin;

        for (int worldX = worldStartX; worldX < worldEndX; worldX += spacingInt)
        {
            for (int worldY = worldStartY; worldY < worldEndY; worldY += spacingInt)
            {
                int hash = HashCoord(worldX, worldY, seed);
                float jitterX = HashToFloat01(hash) * spacingInt;
                float jitterY = HashToFloat01(hash ^ 0x5bd1e995) * spacingInt;

                float localX = (worldX + jitterX) - worldOrigin.x;
                float localY = (worldY + jitterY) - worldOrigin.y;

                if (localX < margin || localX >= width - margin - 1 || localY < margin || localY >= depth - margin - 1)
                    continue;

                float rainfall = SampleBilinear(rainfallMap, localX, localY, width, depth, 0.5f);
                SimulateDroplet(heights, resistanceMap, localX, localY, width, depth, margin,
                    maxDropletLifetime, inertia, sedimentCapacityFactor, minSedimentCapacity,
                    erodeSpeed, depositSpeed, evaporateSpeed, gravity, radius, rainfall);
            }
        }
    }

    private static void SimulateDroplet(
        float[,] heights, float[,] resistanceMap,
        float startX, float startY, int width, int depth, int margin,
        int maxLifetime, float inertia, float sedimentCapacityFactor, float minSedimentCapacity,
        float erodeSpeed, float depositSpeed, float evaporateSpeed, float gravity, int radius, float rainfall)
    {
        float posX = startX, posY = startY;
        float dirX = 0f, dirY = 0f;
        // Wetter climates feed more water into every droplet, giving it more erosive/carrying capacity over its life.
        float water = Mathf.Lerp(0.4f, 1.6f, rainfall);
        float speed = 1f;
        float sediment = 0f;

        for (int step = 0; step < maxLifetime; step++)
        {
            int nodeX = Mathf.FloorToInt(posX);
            int nodeY = Mathf.FloorToInt(posY);
            float cellOffsetX = posX - nodeX;
            float cellOffsetY = posY - nodeY;

            GetHeightAndGradient(heights, posX, posY, out float oldHeight, out float gradX, out float gradY);

            dirX = dirX * inertia - gradX * (1f - inertia);
            dirY = dirY * inertia - gradY * (1f - inertia);

            float dirLength = Mathf.Sqrt(dirX * dirX + dirY * dirY);
            if (dirLength < 1e-5f)
                break;
            dirX /= dirLength;
            dirY /= dirLength;

            posX += dirX;
            posY += dirY;

            if (posX < margin || posX >= width - margin - 1 || posY < margin || posY >= depth - margin - 1)
                break;

            GetHeightAndGradient(heights, posX, posY, out float newHeight, out _, out _);
            float deltaHeight = newHeight - oldHeight;

            float resistance = resistanceMap != null ? SampleBilinear(resistanceMap, nodeX + cellOffsetX, nodeY + cellOffsetY, width, depth, 0.5f) : 0.5f;
            float sedimentCapacity = Mathf.Max(-deltaHeight * speed * water * sedimentCapacityFactor, minSedimentCapacity);

            if (sediment > sedimentCapacity || deltaHeight > 0f)
            {
                // Moving uphill or already carrying more than it can hold: drop some sediment here.
                float depositAmount = deltaHeight > 0f
                    ? Mathf.Min(deltaHeight, sediment)
                    : (sediment - sedimentCapacity) * depositSpeed;

                sediment -= depositAmount;
                DepositAt(heights, nodeX, nodeY, cellOffsetX, cellOffsetY, width, depth, depositAmount);
            }
            else
            {
                // Room to carry more: erode the terrain, scaled down where the material is more resistant.
                float erodeAmount = Mathf.Min((sedimentCapacity - sediment) * erodeSpeed, -deltaHeight);
                erodeAmount *= Mathf.Lerp(1f, 0.15f, resistance);
                if (erodeAmount > 0f)
                {
                    ErodeAt(heights, nodeX, nodeY, width, depth, radius, erodeAmount);
                    sediment += erodeAmount;
                }
            }

            speed = Mathf.Sqrt(Mathf.Max(0f, speed * speed + deltaHeight * gravity));
            water *= (1f - evaporateSpeed);
            if (water < 0.01f)
                break;
        }
    }

    private static void GetHeightAndGradient(float[,] heights, float posX, float posY, out float height, out float gradX, out float gradY)
    {
        int x = Mathf.FloorToInt(posX);
        int y = Mathf.FloorToInt(posY);
        float u = posX - x;
        float v = posY - y;

        float heightNW = heights[x, y];
        float heightNE = heights[x + 1, y];
        float heightSW = heights[x, y + 1];
        float heightSE = heights[x + 1, y + 1];

        gradX = (heightNE - heightNW) * (1f - v) + (heightSE - heightSW) * v;
        gradY = (heightSW - heightNW) * (1f - u) + (heightSE - heightNE) * u;

        height = heightNW * (1f - u) * (1f - v) + heightNE * u * (1f - v) + heightSW * (1f - u) * v + heightSE * u * v;
    }

    private static void DepositAt(float[,] heights, int nodeX, int nodeY, float u, float v, int width, int depth, float amount)
    {
        if (amount <= 0f) return;
        heights[nodeX, nodeY] += amount * (1f - u) * (1f - v);
        heights[nodeX + 1, nodeY] += amount * u * (1f - v);
        heights[nodeX, nodeY + 1] += amount * (1f - u) * v;
        heights[nodeX + 1, nodeY + 1] += amount * u * v;
    }

    private static void ErodeAt(float[,] heights, int centerX, int centerY, int width, int depth, int radius, float amount)
    {
        if (amount <= 0f) return;

        float totalWeight = 0f;
        int minX = Mathf.Max(1, centerX - radius);
        int maxX = Mathf.Min(width - 2, centerX + radius);
        int minY = Mathf.Max(1, centerY - radius);
        int maxY = Mathf.Min(depth - 2, centerY + radius);

        // The whole brush fits (always the case for droplets, which stay a margin away from the edges):
        // use the precomputed weights - the same values, in the same order, as the loops below.
        if (minX == centerX - radius && maxX == centerX + radius && minY == centerY - radius && maxY == centerY + radius)
        {
            ErosionBrush brush = BrushFor(radius);
            for (int k = 0; k < brush.OffsetX.Length; k++)
                heights[centerX + brush.OffsetX[k], centerY + brush.OffsetY[k]] -= amount * brush.Weight[k];
            return;
        }

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                if (dist > radius) continue;
                totalWeight += 1f - dist / radius;
            }
        }

        if (totalWeight <= 0f) return;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                if (dist > radius) continue;
                float weight = (1f - dist / radius) / totalWeight;
                float erodeAmount = amount * weight;
                // Never carve below zero excess in a single step; cheap enough guard against runaway pits.
                heights[x, y] -= erodeAmount;
            }
        }
    }

    /// <summary>
    /// The cells an unclipped erosion brush of one radius touches and each one's share, computed once with
    /// exactly the expressions and loop order <see cref="ErodeAt"/> uses: a cell's distance from the brush
    /// center only depends on its offset (whole numbers, exact as floats), so the shares are identical
    /// wherever the brush is - only the sqrt-per-cell work is saved on every droplet step.
    /// </summary>
    private sealed class ErosionBrush
    {
        public int[] OffsetX;
        public int[] OffsetY;
        public float[] Weight;
    }

    private static readonly ErosionBrush[] Brushes = new ErosionBrush[7];

    private static ErosionBrush BrushFor(int radius)
    {
        ErosionBrush brush = Brushes[radius];
        if (brush != null)
            return brush;

        // Built around a center far enough from 0 that the window is never clipped, exactly like ErodeAt.
        int centerX = radius + 1, centerY = radius + 1;
        float totalWeight = 0f;
        for (int y = centerY - radius; y <= centerY + radius; y++)
        {
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                if (dist > radius) continue;
                totalWeight += 1f - dist / radius;
            }
        }

        var offsetX = new System.Collections.Generic.List<int>();
        var offsetY = new System.Collections.Generic.List<int>();
        var weight = new System.Collections.Generic.List<float>();
        if (totalWeight > 0f)
        {
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                    if (dist > radius) continue;
                    offsetX.Add(x - centerX);
                    offsetY.Add(y - centerY);
                    weight.Add((1f - dist / radius) / totalWeight);
                }
            }
        }

        brush = new ErosionBrush { OffsetX = offsetX.ToArray(), OffsetY = offsetY.ToArray(), Weight = weight.ToArray() };
        Brushes[radius] = brush;   // several threads may build the same brush at once; they are identical
        return brush;
    }

    private static float SampleBilinear(float[,] map, float x, float y, int width, int depth, float fallback)
    {
        if (map == null) return fallback;

        int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, width - 2);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, depth - 2);
        float u = Mathf.Clamp01(x - x0);
        float v = Mathf.Clamp01(y - y0);

        float a = map[x0, y0];
        float b = map[x0 + 1, y0];
        float c = map[x0, y0 + 1];
        float d = map[x0 + 1, y0 + 1];

        return a * (1f - u) * (1f - v) + b * u * (1f - v) + c * (1f - u) * v + d * u * v;
    }

    // 2654435761 (xxHash's PRIME32_1) doesn't fit a signed int literal; store its bit-identical
    // int representation once so the multiplication below stays int * int.
    private const int PrimeSeedConstant = unchecked((int)2654435761u);

    private static int HashCoord(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * PrimeSeedConstant;
            h = (h ^ (h >> 13)) * 1274126177;
            return h ^ (h >> 16);
        }
    }

    private static float HashToFloat01(int hash)
    {
        return (hash & 0x7fffffff) / (float)int.MaxValue;
    }
}
