using System.Collections.Generic;
using ProceduralDungeon;
using UnityEngine;

namespace ProceduralCaves
{
    /// <summary>
    /// A <see cref="CaveMiningStage"/>'s settings as plain data, copied on the main thread when its profile is compiled
    /// (<see cref="DungeonStageAsset.Prepare"/>), so generation can read them on a worker thread while the asset is edited.
    /// The resource entries keep their prefab and item references for the build step (main thread only).
    /// </summary>
    public sealed class CaveRules
    {
        public CaveDepthSettings Depth = new CaveDepthSettings();
        public CaveDepositSettings Deposits = new CaveDepositSettings();
        public CaveChamberSettings Chambers = new CaveChamberSettings();
        public CaveFormationSettings Formations = new CaveFormationSettings();
        public CaveEnemySettings Enemies = new CaveEnemySettings();
        public CavePersistenceSettings Persistence = new CavePersistenceSettings();
        public CaveAccessSettings Access = new CaveAccessSettings();

        /// <summary>The resources in table order (a placement's Entry is its index here).</summary>
        public readonly List<CaveResourceEntry> Resources = new List<CaveResourceEntry>();

        /// <summary>A copy of the given settings and entries (null = the defaults; entries: the built-in resources).</summary>
        public static CaveRules Create(IEnumerable<CaveResourceEntry> entries, CaveDepthSettings depth = null, CaveDepositSettings deposits = null,
            CaveChamberSettings chambers = null, CaveFormationSettings formations = null, CaveEnemySettings enemies = null,
            CavePersistenceSettings persistence = null, CaveAccessSettings access = null)
        {
            var r = new CaveRules
            {
                Depth = DeepCopy.Copy(depth) ?? new CaveDepthSettings(),
                Deposits = DeepCopy.Copy(deposits) ?? new CaveDepositSettings(),
                Chambers = DeepCopy.Copy(chambers) ?? new CaveChamberSettings(),
                Formations = DeepCopy.Copy(formations) ?? new CaveFormationSettings(),
                Enemies = DeepCopy.Copy(enemies) ?? new CaveEnemySettings(),
                Persistence = DeepCopy.Copy(persistence) ?? new CavePersistenceSettings(),
                Access = DeepCopy.Copy(access) ?? new CaveAccessSettings(),
            };
            IEnumerable<CaveResourceEntry> source = entries ?? CaveDefaults.Resources();
            foreach (CaveResourceEntry e in source)
                if (e != null)
                    r.Resources.Add(e.Clone());
            return r;
        }

        /// <summary>The resource of a Resource placement's Entry (null when out of range).</summary>
        public CaveResourceEntry Resource(int entry) => entry >= 0 && entry < Resources.Count ? Resources[entry] : null;
    }

    /// <summary>Depth scores: how deep a place in a cave is (see <see cref="CaveDepthSettings"/>).</summary>
    public static class CaveDepth
    {
        /// <summary>
        /// The depth of a place on floor <paramref name="floor"/>, <paramref name="across"/> (0-1) of the way from the floor's
        /// arrival to its farthest point, in a cave <paramref name="chain"/> caves down a chain of deeper caves, at
        /// <paramref name="difficulty"/>.
        /// </summary>
        public static float Of(CaveDepthSettings s, int floor, float across, int chain, float difficulty)
        {
            if (s == null)
                s = new CaveDepthSettings();
            float depth = Mathf.Max(0, floor) * s.perLevel + Mathf.Clamp01(across) * s.acrossLevel + Mathf.Max(0, chain) * s.perDeeperCave
                          + Mathf.Max(0f, difficulty - 1f) * s.perDifficulty;
            // A chain of deeper caves goes on for ever; the resources do not: below the bottom it is all as deep.
            return s.maxDepth > 0f ? Mathf.Min(depth, s.maxDepth) : depth;
        }

        /// <summary>How suited a resource is to a depth: 0 outside its range, a quarter at the range's edges, 1 at its peak.</summary>
        public static float Suitability(CaveResourceEntry e, float depth)
        {
            if (e == null || !e.enabled || depth < e.depth.min || depth > e.depth.max)
                return 0f;
            float peak = Mathf.Clamp(e.peakDepth, e.depth.min, e.depth.max);
            float t;
            if (depth <= peak)
                t = peak - e.depth.min > 1e-4f ? (depth - e.depth.min) / (peak - e.depth.min) : 1f;
            else
                t = e.depth.max - peak > 1e-4f ? (e.depth.max - depth) / (e.depth.max - peak) : 1f;
            return 0.25f + 0.75f * Mathf.Clamp01(t);
        }

        /// <summary>The depth of each area of <paramref name="floor"/> (index = area id) and of its tunnels' cells.</summary>
        public static float[] AreaDepths(DungeonContext ctx, CaveDepthSettings s, FloorLayout floor)
        {
            var depths = new float[floor.Areas.Count];
            int[] reach = floor.DistanceFromArrival;
            int farthest = 0;
            if (reach != null)
                foreach (int d in reach)
                    if (d > farthest)
                        farthest = d;
            int chain = ctx.Request != null ? ctx.Request.depth : 0;
            float difficulty = ctx.Request != null ? ctx.Request.difficulty : 1f;
            foreach (Area a in floor.Areas)
            {
                float across = 0f;
                if (reach != null && farthest > 0)
                {
                    int sum = 0, n = 0;
                    foreach (int c in a.Cells)
                        if (reach[c] >= 0)
                        {
                            sum += reach[c];
                            n++;
                        }
                    if (n > 0)
                        across = sum / (float)n / farthest;
                }
                depths[a.Id] = Of(s, floor.Index, across, chain, difficulty);
            }
            return depths;
        }
    }

    /// <summary>The meaning of a Resource placement's fields, and the tags of the special chambers.</summary>
    public static class CavePlacements
    {
        /// <summary>Placement.Wave of a deposit on the floor.</summary>
        public const byte FloorMount = 0;
        /// <summary>Placement.Wave of a deposit in a wall.</summary>
        public const byte WallMount = 1;

        /// <summary>Area tag of a chamber full of ore.</summary>
        public const string RichVeinTag = "Rich Vein";
        /// <summary>Area tag of a chamber of glowing crystals and gemstone veins.</summary>
        public const string GrottoTag = "Crystal Grotto";

        /// <summary>Is <paramref name="p"/> a cave deposit?</summary>
        public static bool IsDeposit(in Placement p) => p.Kind == PlacementKind.Resource;
    }
}
