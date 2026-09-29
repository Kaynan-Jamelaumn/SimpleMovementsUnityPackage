using System.Collections.Generic;

namespace ProceduralDungeon
{
    /// <summary>
    /// A small, fast, seedable random generator (SplitMix64) for the dungeon pipeline. Every stage, floor and
    /// area gets its own stream derived from the dungeon seed with <see cref="PlacementRandom.Hash"/>, so
    /// changing one stage (say, loot rules) never moves the rooms, floors can be generated in parallel, and
    /// nothing depends on UnityEngine.Random's global state.
    /// </summary>
    public sealed class DungeonRandom
    {
        private ulong state;

        public DungeonRandom(ulong seed)
        {
            state = seed;
        }

        /// <summary>A stream for (seed, stage salt, a, b) - see <see cref="Salt"/>.</summary>
        public static DungeonRandom Create(int seed, int salt, int a = 0, int b = 0)
        {
            uint high = PlacementRandom.Hash(seed, salt, a, b, 0x51);
            uint low = PlacementRandom.Hash(seed, salt, a, b, 0x7F3);
            return new DungeonRandom(((ulong)high << 32) | low);
        }

        /// <summary>A stable salt for a stage or purpose name.</summary>
        public static int Salt(string name) => PlacementRandom.StableHash(name);

        public ulong NextULong()
        {
            unchecked
            {
                ulong z = state += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        public uint NextUInt() => (uint)(NextULong() >> 32);

        public int NextInt() => (int)(NextULong() >> 33);

        /// <summary>[0, 1).</summary>
        public float Value() => (NextULong() >> 40) * (1f / 16777216f);

        /// <summary>[min, maxExclusive). Returns min when the range is empty.</summary>
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min)
                return min;
            return min + (int)(NextULong() % (ulong)(maxExclusive - min));
        }

        /// <summary>[min, max).</summary>
        public float Range(float min, float max) => min + (max - min) * Value();

        public bool Chance(float probability) => probability > 0f && Value() < probability;

        /// <summary>-1..1 with a triangular distribution (small values more likely).</summary>
        public float Triangular() => Value() - Value();

        /// <summary>Index picked with probability proportional to its weight; -1 when every weight is zero.</summary>
        public int WeightedIndex(IReadOnlyList<float> weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Count; i++)
                if (weights[i] > 0f)
                    total += weights[i];
            if (total <= 0f)
                return -1;
            float r = Value() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0f)
                    continue;
                if (r < weights[i])
                    return i;
                r -= weights[i];
            }
            for (int i = weights.Count - 1; i >= 0; i--)
                if (weights[i] > 0f)
                    return i;
            return -1;
        }

        public T Pick<T>(IReadOnlyList<T> list) => list[Range(0, list.Count)];

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                T t = list[i];
                list[i] = list[j];
                list[j] = t;
            }
        }

        /// <summary>An independent stream derived from this one.</summary>
        public DungeonRandom Fork(int salt)
        {
            unchecked
            {
                return new DungeonRandom(NextULong() ^ ((ulong)(uint)salt * 0xD6E8FEB86659FD93UL));
            }
        }
    }
}
