/// <summary>
/// Lightweight thermal erosion post-process for heightmaps: redistributes height from a cell to its
/// lowest neighbor whenever the slope between them exceeds a talus angle (angle of repose),
/// softening raw multi-octave Perlin noise into more natural-looking slopes and ridgelines.
/// </summary>
/// <remarks>
/// Operates purely on a local 2D height buffer with no external dependencies, so it is safe to run
/// on a background thread during chunk generation. Each pass writes into a separate delta buffer
/// before applying it, so results don't depend on the order cells happen to be scanned in.
/// </remarks>
public static class TerrainErosion
{
    private static readonly (int dx, int dy)[] Neighbors4 = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    /// <summary>
    /// Applies thermal erosion in-place to a height buffer.
    /// </summary>
    /// <param name="heights">The height buffer to erode, modified in-place.</param>
    /// <param name="iterations">Number of erosion passes. More passes erode further but cost more to generate.</param>
    /// <param name="talusHeightThreshold">Height difference (same units as heights) between adjacent cells below which nothing moves - the "angle of repose" expressed as a height delta.</param>
    /// <param name="strength">Fraction (0..1) of the excess height above the talus threshold moved to the lower neighbor per pass.</param>
    public static void ApplyThermalErosion(float[,] heights, int iterations, float talusHeightThreshold, float strength)
    {
        if (iterations <= 0 || strength <= 0f)
            return;

        int width = heights.GetLength(0);
        int depth = heights.GetLength(1);
        float[,] delta = new float[width, depth];

        for (int iter = 0; iter < iterations; iter++)
        {
            System.Array.Clear(delta, 0, delta.Length);

            for (int y = 0; y < depth; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float h = heights[x, y];

                    int lowestDx = 0, lowestDy = 0;
                    float lowestHeight = h;
                    bool hasLower = false;

                    foreach (var (dx, dy) in Neighbors4)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= width || ny < 0 || ny >= depth)
                            continue;

                        float nh = heights[nx, ny];
                        if (nh < lowestHeight)
                        {
                            lowestHeight = nh;
                            lowestDx = dx;
                            lowestDy = dy;
                            hasLower = true;
                        }
                    }

                    if (!hasLower)
                        continue;

                    float diff = h - lowestHeight;
                    if (diff <= talusHeightThreshold)
                        continue;

                    // Move half the excess above the talus threshold, scaled by strength, so the
                    // pass converges toward a stable slope instead of overshooting it.
                    float amount = (diff - talusHeightThreshold) * 0.5f * strength;
                    delta[x, y] -= amount;
                    delta[x + lowestDx, y + lowestDy] += amount;
                }
            }

            for (int y = 0; y < depth; y++)
                for (int x = 0; x < width; x++)
                    heights[x, y] += delta[x, y];
        }
    }
}
