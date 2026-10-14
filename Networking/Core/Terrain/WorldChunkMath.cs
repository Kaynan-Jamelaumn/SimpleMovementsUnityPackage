using System;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Pure arithmetic shared by the multi-viewer terrain streaming (<see cref="EndlessTerrain.SetViewers"/>) and the world
    /// relay (<c>NetWorldChunks</c>): distances from a set of viewers to a chunk, which chunk a point is in, and how far
    /// the server's weather clock has run since it was sent. No Unity scene needed (covered by Edit Mode tests).
    /// </summary>
    public static class WorldChunkMath
    {
        /// <summary>Longest a received weather time is run forward (seconds): beyond it the sample is stale anyway.</summary>
        public const double MaxWeatherExtrapolation = 600.0;

        /// <summary>Horizontal distance from <paramref name="point"/> (x, z) to the nearest point of the rectangle min..max (0 inside).</summary>
        public static float DistanceToRect(Vector2 point, Vector2 min, Vector2 max)
        {
            float dx = Mathf.Max(0f, Mathf.Max(min.x - point.x, point.x - max.x));
            float dy = Mathf.Max(0f, Mathf.Max(min.y - point.y, point.y - max.y));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Distance from the nearest of the first <paramref name="count"/> <paramref name="points"/> to the rectangle
        /// min..max; <see cref="float.PositiveInfinity"/> when there is no point. Safe to call while another thread
        /// replaces the array: <paramref name="count"/> is clamped to the array's length.
        /// </summary>
        public static float NearestDistanceToRect(Vector2[] points, int count, Vector2 min, Vector2 max)
        {
            if (points == null)
                return float.PositiveInfinity;
            int n = Math.Min(count, points.Length);
            float best = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                float d = DistanceToRect(points[i], min, max);
                if (d < best)
                    best = d;
            }
            return best;
        }

        /// <summary>The chunk grid cell containing world (x, z) <paramref name="point"/>: chunk (x, y) covers [x, x + 1) x [y, y + 1) chunk sizes.</summary>
        public static Vector2Int ChunkCoordOf(Vector2 point, float chunkWorldSize)
        {
            float size = chunkWorldSize > 0f ? chunkWorldSize : 1f;
            return new Vector2Int(Mathf.FloorToInt(point.x / size), Mathf.FloorToInt(point.y / size));
        }

        /// <summary>
        /// True when chunk <paramref name="coord"/> is within <paramref name="range"/> chunks (on both axes) of the chunk
        /// <paramref name="center"/> - the square of chunks a viewer may keep loaded.
        /// </summary>
        public static bool WithinChunkRange(Vector2 coord, Vector2Int center, int range) =>
            Mathf.Abs(coord.x - center.x) <= range && Mathf.Abs(coord.y - center.y) <= range;

        /// <summary>
        /// The weather time now on a machine that received <paramref name="sentWeatherTime"/>, captured at network time
        /// <paramref name="sentAt"/>, at network time <paramref name="now"/>: run forward at <paramref name="rate"/> weather
        /// seconds per second. A negative elapsed time (clock estimates crossing) counts as 0 and very old samples are run
        /// forward by at most <see cref="MaxWeatherExtrapolation"/>; invalid input gives the sent time unchanged.
        /// </summary>
        public static double ExtrapolateWeatherTime(double sentWeatherTime, double sentAt, double now, float rate)
        {
            if (double.IsNaN(sentWeatherTime) || double.IsInfinity(sentWeatherTime))
                return 0.0;
            double elapsed = now - sentAt;
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0.0)
                elapsed = 0.0;
            elapsed = Math.Min(elapsed, MaxWeatherExtrapolation);
            float r = float.IsNaN(rate) || float.IsInfinity(rate) ? 0f : Mathf.Max(0f, rate);
            return sentWeatherTime + elapsed * r;
        }
    }
}
