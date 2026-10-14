using System;
using System.Collections.Generic;

namespace HubWorld
{
    /// <summary>
    /// Deterministic seeds and rolls for the world cycles: the same shard seed always gives the same sequence of worlds
    /// and modifiers, so a server can announce the next world before it exists and every client can rebuild the terrain
    /// from the seed alone.
    /// </summary>
    public static class CycleSeeds
    {
        /// <summary>The world seed of cycle <paramref name="cycleIndex"/> of a shard (never 0).</summary>
        public static int WorldSeed(int shardSeed, int cycleIndex)
        {
            uint h = Mix((uint)shardSeed ^ 0x9E3779B9u, (uint)cycleIndex, 0x57524C44u); // "WRLD"
            int seed = (int)(h & 0x7FFFFFFF);
            return seed == 0 ? 1 : seed;
        }

        /// <summary>A uniform value in [0, 1) for (seed, salt).</summary>
        public static double Value(int seed, uint salt) => (Mix((uint)seed, salt, 0x6D6F6469u) >> 8) / (double)(1 << 24);

        /// <summary>
        /// Picks one entry by weight, or -1 for none: <paramref name="noneWeight"/> is the weight of "no entry". Entries
        /// with a weight of 0 or less are never picked.
        /// </summary>
        public static int PickWeighted(int seed, uint salt, IReadOnlyList<float> weights, float noneWeight)
        {
            double total = Math.Max(0f, noneWeight);
            if (weights != null)
                for (int i = 0; i < weights.Count; i++)
                    total += Math.Max(0f, weights[i]);
            if (total <= 0d)
                return -1;
            double roll = Value(seed, salt) * total;
            if (weights != null)
            {
                for (int i = 0; i < weights.Count; i++)
                {
                    double w = Math.Max(0f, weights[i]);
                    if (w <= 0d)
                        continue;
                    if (roll < w)
                        return i;
                    roll -= w;
                }
            }
            return -1;
        }

        /// <summary>A fresh, non-zero shard seed (when none is configured).</summary>
        public static int RandomShardSeed()
        {
            int s = Guid.NewGuid().GetHashCode() & 0x7FFFFFFF;
            return s == 0 ? 1 : s;
        }

        private static uint Mix(uint a, uint b, uint c)
        {
            // A small avalanche hash (lowbias32 on a combined key): stable across platforms and runtimes.
            uint h = a * 0x85EBCA6Bu ^ (b + 0x9E3779B9u + (a << 6) + (a >> 2)) ^ c * 0xC2B2AE35u;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h;
        }
    }
}
