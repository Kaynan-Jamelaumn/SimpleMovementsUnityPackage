using System;

namespace ProceduralCommon
{
    /// <summary>
    /// Exact Euclidean distance transforms on rectangular grids (Felzenszwalb &amp; Huttenlocher's linear-time
    /// algorithm, rows then columns) - the rectangular counterpart of PlacementFields.DistanceTransform, which only
    /// handles square grids. Plain C#: safe on worker threads.
    /// </summary>
    public static class DistanceField
    {
        /// <summary>Returned for every cell when the grid has no seed at all.</summary>
        public const float Far = 1e6f;

        private const float Infinity = 1e20f;

        /// <summary>
        /// Distance (in cells) from every cell to the nearest cell where <paramref name="seeds"/> is true.
        /// Index = x + y * width. <paramref name="result"/> is reused when it has the right length.
        /// </summary>
        public static float[] Compute(bool[] seeds, int width, int height, float[] result = null)
        {
            int n = width * height;
            if (seeds == null || seeds.Length < n)
                throw new ArgumentException("seeds must hold width * height values.");
            if (result == null || result.Length != n)
                result = new float[n];

            bool any = false;
            for (int i = 0; i < n && !any; i++)
                any = seeds[i];
            if (!any)
            {
                for (int i = 0; i < n; i++)
                    result[i] = Far;
                return result;
            }

            int longest = Math.Max(width, height);
            var f = new float[longest];
            var d = new float[longest];
            var v = new int[longest];
            var z = new double[longest + 1];

            // Squared distances along rows.
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                    f[x] = seeds[row + x] ? 0f : Infinity;
                Transform1D(f, width, d, v, z);
                for (int x = 0; x < width; x++)
                    result[row + x] = d[x];
            }

            // Then along columns, over the row results.
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                    f[y] = result[x + y * width];
                Transform1D(f, height, d, v, z);
                for (int y = 0; y < height; y++)
                    result[x + y * width] = d[y] >= Infinity ? Far : (float)Math.Sqrt(d[y]);
            }

            return result;
        }

        /// <summary>1D squared-distance transform: the lower envelope of the parabolas rooted at the finite samples of f.</summary>
        private static void Transform1D(float[] f, int n, float[] d, int[] v, double[] z)
        {
            int k = -1;
            for (int q = 0; q < n; q++)
            {
                if (f[q] >= Infinity)
                    continue;
                if (k < 0)
                {
                    k = 0;
                    v[0] = q;
                    z[0] = double.NegativeInfinity;
                    z[1] = double.PositiveInfinity;
                    continue;
                }

                double s;
                while (true)
                {
                    int p = v[k];
                    s = ((f[q] + (double)q * q) - (f[p] + (double)p * p)) / (2.0 * q - 2.0 * p);
                    if (s <= z[k] && k > 0)
                    {
                        k--;
                        continue;
                    }
                    break;
                }
                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = double.PositiveInfinity;
            }

            if (k < 0)
            {
                for (int q = 0; q < n; q++)
                    d[q] = Infinity;
                return;
            }

            int j = 0;
            for (int q = 0; q < n; q++)
            {
                while (z[j + 1] < q)
                    j++;
                int p = v[j];
                d[q] = (q - p) * (q - p) + f[p];
            }
        }
    }
}
