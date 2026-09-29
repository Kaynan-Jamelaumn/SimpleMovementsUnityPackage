using System;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// What to generate: a seed plus the knobs a caller (a portal, a quest, a debug menu) may want to set. The profile
    /// holds everything else. The same request with the same profile always produces the same dungeon.
    /// </summary>
    [Serializable]
    public class DungeonRequest
    {
        [Tooltip("Seed of the dungeon. 0 = pick a random one when generating.")]
        public int seed;

        [Tooltip("Which of the profile's Size Classes to use (floor size and count).")]
        public SizeClass size = SizeClass.Medium;

        [Tooltip("1 = normal. Scales mob budgets and loot tiers (0.5 easy, 2 very hard).")]
        [Min(0.1f)] public float difficulty = 1f;

        [Tooltip("Fixed number of floors; 0 = use the profile's range.")]
        [Min(0)] public int floorCount;

        [Tooltip("Force every floor to one style (ignores the profile's Styles weights).")]
        public bool overrideStyle;
        [Tooltip("The style used on every floor, with Override Style.")]
        public FloorStyle style = FloorStyle.Rooms;

        [Tooltip("Dungeon depth when chaining dungeons through exit portals (0 = first). Adds difficulty.")]
        [Min(0)] public int depth;

        [Tooltip("Optional name for logs and the hierarchy.")]
        public string label = "";

        public DungeonRequest Clone() => (DungeonRequest)MemberwiseClone();

        /// <summary>A request whose seed is derived from a world seed and a position (e.g. a portal's), so the same portal always leads to the same dungeon.</summary>
        public static DungeonRequest FromWorldPosition(int worldSeed, Vector3 position, float cellSize = 8f)
        {
            int x = Mathf.FloorToInt(position.x / cellSize), z = Mathf.FloorToInt(position.z / cellSize);
            int seed = (int)PlacementRandom.Hash(worldSeed, PlacementRandom.StableHash("DungeonPortal"), x, z, 0);
            if (seed == 0)
                seed = 1;
            return new DungeonRequest { seed = seed };
        }

        /// <summary>The request for the next dungeon in a chain.</summary>
        public DungeonRequest Next()
        {
            DungeonRequest next = Clone();
            next.depth = depth + 1;
            next.seed = (int)PlacementRandom.Hash(seed, 0x4E657874, depth + 1, 0, 0);
            if (next.seed == 0)
                next.seed = 1;
            return next;
        }
    }
}
