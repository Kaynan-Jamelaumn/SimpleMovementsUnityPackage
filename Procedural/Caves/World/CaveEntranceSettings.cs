using System;
using System.Collections.Generic;
using ProceduralDungeon;
using UnityEngine;

namespace ProceduralCaves
{
    /// <summary>
    /// Where cave entrances appear on the endless terrain and what lies behind them (edited on <see cref="EndlessTerrain"/>
    /// under Cave Entrances, used by each chunk's <see cref="CaveEntranceSpawner"/>).
    /// <para>The world is cut into square regions (<see cref="regionSize"/>); each region has up to
    /// <see cref="maxPerRegion"/> caves, each slot used with <see cref="spawnChance"/> at a point hashed from the world
    /// seed (<see cref="CaveSitePlanner"/>) - the same seed always puts the same caves in the same places, whatever order
    /// chunks load in, and every cave always leads to the same cave system. The chunk holding a point gives the cave the
    /// best spot within <see cref="searchRadius"/> (dry, not too steep, clear of trees and rocks, an allowed biome and
    /// height), facing downhill so it opens out of the slope.</para>
    /// <para>Cave mouths are scenery every machine builds the same way (in a network session too); going in is the
    /// server's decision, as with every portal.</para>
    /// </summary>
    [Serializable]
    public class CaveEntranceSettings
    {
        [Tooltip("Put cave entrances in the world.")]
        public bool enabled = true;

        [Header("What Lies Behind")]
        [Tooltip("REQUIRED. The caves: Dungeon Profiles with a Cave Mining Stage (Tools > SimpleMovements > Caves > Create Cave Setup makes one). Each entrance leads to one of them, picked from its seed (always the same one). Empty = no cave entrances.")]
        public List<DungeonProfile> caveProfiles = new List<DungeonProfile>();
        [Tooltip("OPTIONAL. A Dungeon Manager prefab for the caves (empty = one is made from the profile).")]
        public DungeonManager dungeonManager;
        [Tooltip("Classic mode: where caves are built - far below the world, well under the lowest terrain (ignored when dungeons get scenes of their own).")]
        public Vector3 dungeonOrigin = new Vector3(0f, -10000f, 0f);

        [Header("How Many and Where (world regions)")]
        [Tooltip("Size of the square regions caves are planned in (world units). Each region gets up to Max Per Region. Recommended 400-1000.")]
        [Min(64f)] public float regionSize = 520f;
        [Tooltip("Cave slots per region (the most caves a region can have). Recommended 1-2.")]
        [Min(0)] public int maxPerRegion = 1;
        [Tooltip("Chance (0-1) each slot gets a cave. 1 = every region has Max Per Region (handy for testing).")]
        [Range(0f, 1f)] public float spawnChance = 0.6f;
        [Tooltip("Smallest distance between two cave entrances (world units). Capped at 90% of Region Size.")]
        [Min(0f)] public float minDistanceBetween = 180f;
        [Tooltip("How far (world units) an entrance may move from its planned point to find a spot. Recommended 20-48.")]
        [Min(0f)] public float searchRadius = 32f;
        [Tooltip("Distance kept between the searched area and the chunk's edges, so an entrance depends only on its own chunk (world units).")]
        [Min(0f)] public float edgeAvoidanceDistance = 8f;

        [Header("Ground")]
        [Tooltip("Radius of the ground the mouth stands on (world units).")]
        [Min(1f)] public float footprintRadius = 4.5f;
        [Tooltip("Steepest ground for a mouth (degrees). A mouth on a slope opens out of it (it faces downhill).")]
        [Range(0f, 45f)] public float maxSlope = 24f;
        [Tooltip("Largest height difference across the footprint (world units). Mouths follow the ground: they take rougher ground than portals.")]
        [Min(0.05f)] public float maxUnevenness = 2.6f;
        [Tooltip("Free space between the mouth and trees, rocks and other placed objects (world units).")]
        [Min(0f)] public float objectClearance = 1.5f;
        [Tooltip("Only between Min and Max Height (world Y).")]
        public bool limitHeight;
        public float minHeight = 0f;
        public float maxHeight = 200f;
        [Tooltip("Biomes without caves.")]
        public List<Biome> forbiddenBiomes = new List<Biome>();

        [Header("Size and Difficulty")]
        [Tooltip("Size class of caves near Difficulty Origin (Small: 1-2 levels, Medium, Large, Huge: many levels).")]
        public SizeClass nearSize = SizeClass.Small;
        [Tooltip("Size class of the caves far away (at Far Distance and beyond).")]
        public SizeClass farSize = SizeClass.Large;
        [Tooltip("Distance from Difficulty Origin where caves reach Far Size (world units).")]
        [Min(1f)] public float farDistance = 1600f;
        [Tooltip("Caves further from Difficulty Origin are harder: tougher creatures, better ores (their depth counts more - see Cave Mining Stage > Depth > Per Difficulty).")]
        public bool scaleDifficultyWithDistance = true;
        [Tooltip("World (x, z) where caves are easiest - usually where players start.")]
        public Vector2 difficultyOrigin = Vector2.zero;
        [Tooltip("Difficulty at Difficulty Origin (1 = normal).")]
        [Min(0.1f)] public float difficultyAtOrigin = 1f;
        [Tooltip("Difficulty added per 1000 world units from Difficulty Origin.")]
        [Min(0f)] public float difficultyPerKilometer = 0.6f;
        [Tooltip("Difficulty never goes above this.")]
        [Min(0.1f)] public float maxDifficulty = 5f;

        [Header("Players")]
        [Tooltip("Separate Runs: every group gets its own visit of the cave (its state is kept between visits all the same). Shared Run: everyone who enters while a visit is going joins it.")]
        public PortalSharing sharing = PortalSharing.SeparateRuns;
        [Tooltip("Most players in one visit (0 = no limit).")]
        [Min(0)] public int maxPlayers;
        [Tooltip("Party members within this distance (meters) go in together.")]
        [Min(0f)] public float bringPartyWithin = 10f;

        [Header("Mouth")]
        [Tooltip("Width of the opening (meters).")]
        [Min(1.6f)] public float mouthWidth = 3.4f;
        [Tooltip("Height of the opening (meters).")]
        [Min(2.2f)] public float mouthHeight = 3.6f;
        [Tooltip("Length of the tunnel behind the opening (meters).")]
        [Min(1.5f)] public float mouthDepth = 3.4f;
        [Tooltip("Colour of the mouth's rock (when the cave's theme has no cave wall material).")]
        public Color rockColor = new Color(0.43f, 0.4f, 0.36f);
        [Tooltip("Use the cave theme's cave wall material for the mouth's rock.")]
        public bool useThemeMaterial = true;

        [Header("Going In")]
        [Tooltip("Seconds a player stands in the mouth (the screen darkens, the cave's name shows) before going in.")]
        [Range(0.1f, 3f)] public float fadeSeconds = 0.7f;
        [Tooltip("OPTIONAL. Played when a player goes in (a gust of cave air).")]
        public AudioClip enterSound;

        [Header("Debugging")]
        [Tooltip("Log what the spawners do (sites, spots, rejections).")]
        public bool enableDetailedLogging;
        [Tooltip("Draw the planned sites and the search areas in the Scene view.")]
        public bool enableVisualDebug;

        /// <summary><see cref="minDistanceBetween"/> as applied: at most 90% of <see cref="regionSize"/>.</summary>
        public float EffectiveMinDistance => Mathf.Min(minDistanceBetween, regionSize * 0.9f);

        /// <summary>Is there anything to place?</summary>
        public bool IsUsable
        {
            get
            {
                if (!enabled || maxPerRegion <= 0 || spawnChance <= 0f || caveProfiles == null)
                    return false;
                foreach (DungeonProfile p in caveProfiles)
                    if (p != null)
                        return true;
                return false;
            }
        }

        /// <summary>The cave difficulty at world (x, z) (1 when Scale Difficulty With Distance is off).</summary>
        public float DifficultyAt(Vector2 position)
        {
            if (!scaleDifficultyWithDistance)
                return 1f;
            float km = Vector2.Distance(position, difficultyOrigin) / 1000f;
            return Mathf.Clamp(difficultyAtOrigin + km * difficultyPerKilometer, 0.1f, maxDifficulty);
        }

        /// <summary>The size class of a cave at world (x, z): Near Size close by, Far Size far away (<paramref name="random01"/> varies it a little).</summary>
        public SizeClass SizeAt(Vector2 position, float random01)
        {
            float t = Mathf.Clamp01(Vector2.Distance(position, difficultyOrigin) / Mathf.Max(1f, farDistance));
            int near = (int)nearSize, far = (int)farSize;
            float f = Mathf.Lerp(near, far, Mathf.Clamp01(t * 0.75f + random01 * 0.35f));
            return (SizeClass)Mathf.Clamp(Mathf.RoundToInt(f), Mathf.Min(near, far), Mathf.Max(near, far));
        }

        public bool IsBiomeAllowed(Biome biome) => biome == null || forbiddenBiomes == null || !forbiddenBiomes.Contains(biome);

        /// <summary>The live cave profiles (nulls left out).</summary>
        public DungeonProfile[] Profiles()
        {
            var list = new List<DungeonProfile>();
            if (caveProfiles != null)
                foreach (DungeonProfile p in caveProfiles)
                    if (p != null)
                        list.Add(p);
            return list.ToArray();
        }

        /// <summary>Clamps values to safe ranges.</summary>
        public void ValidateSettings()
        {
            regionSize = Mathf.Max(64f, regionSize);
            maxPerRegion = Mathf.Max(0, maxPerRegion);
            minDistanceBetween = Mathf.Max(0f, minDistanceBetween);
            searchRadius = Mathf.Max(0f, searchRadius);
            footprintRadius = Mathf.Max(1f, footprintRadius);
            maxUnevenness = Mathf.Max(0.05f, maxUnevenness);
            maxDifficulty = Mathf.Max(0.1f, maxDifficulty);
            if (limitHeight && minHeight > maxHeight)
            {
                float t = minHeight;
                minHeight = maxHeight;
                maxHeight = t;
            }
        }

        /// <summary>Problems that stop cave entrances from appearing (empty when the settings look usable).</summary>
        public List<string> GetProblems()
        {
            var problems = new List<string>();
            if (!enabled)
                return problems;
            if (Profiles().Length == 0)
                problems.Add("No cave profile: add one to Cave Profiles (Tools > SimpleMovements > Caves > Create Cave Setup makes one).");
            foreach (DungeonProfile p in Profiles())
            {
                int stages = CaveMiningStage.CountIn(p);
                if (stages == 0)
                    problems.Add($"Profile '{p.name}' has no Cave Mining Stage in its Custom Stages: its caves would have no deposits, formations or natural passages.");
                else if (stages > 1)
                    problems.Add($"Profile '{p.name}' has {stages} Cave Mining Stages: only the first is used - put every resource in its Resource Table.");
            }
            if (maxPerRegion <= 0 || spawnChance <= 0f)
                problems.Add("Max Per Region or Spawn Chance is 0: no cave can appear.");
            if (dungeonOrigin.y > -1000f)
                problems.Add("Dungeon Origin is close to the world: keep it far below the terrain (e.g. y = -10000).");
            return problems;
        }
    }

    /// <summary>The identity of a planned cave entrance: its world region and slot (stable for a world seed).</summary>
    [Serializable]
    public struct CaveSiteId : IEquatable<CaveSiteId>
    {
        public int RegionX, RegionZ, Slot;

        public CaveSiteId(int regionX, int regionZ, int slot)
        {
            RegionX = regionX;
            RegionZ = regionZ;
            Slot = slot;
        }

        public bool Equals(CaveSiteId other) => RegionX == other.RegionX && RegionZ == other.RegionZ && Slot == other.Slot;
        public override bool Equals(object obj) => obj is CaveSiteId other && Equals(other);
        public override int GetHashCode() => (int)PlacementRandom.Hash(RegionX, 0x43415645, RegionZ, Slot, 0);
        public override string ToString() => $"cave {RegionX},{RegionZ}#{Slot}";
    }

    /// <summary>
    /// Plans where cave entrances go - a pure function of the world seed and the <see cref="CaveEntranceSettings"/> (the
    /// same principle as <see cref="PortalSitePlanner"/>): no shared random generator, no dependence on which chunk loads
    /// first. Planned points; the chunk holding a point gives it a spot within Search Radius, or (no suitable ground) none.
    /// </summary>
    public static class CaveSitePlanner
    {
        public struct Site
        {
            public CaveSiteId Id;
            /// <summary>Planned point (world x, z).</summary>
            public Vector2 Point;
            /// <summary>Facing when the ground is flat (degrees).</summary>
            public float Yaw;
            /// <summary>The cave's own seed: its name, its mouth's shape.</summary>
            public int Seed;
        }

        private static readonly int Salt = PlacementRandom.StableHash("CaveSites");
        private const int StreamsPerSlot = 8;

        /// <summary>The planned caves of region (<paramref name="rx"/>, <paramref name="rz"/>), in slot order.</summary>
        public static void RegionSites(CaveEntranceSettings s, int worldSeed, int rx, int rz, List<Site> into)
        {
            if (s == null || into == null || !s.IsUsable)
                return;
            float size = Mathf.Max(64f, s.regionSize);
            float minDistance = s.EffectiveMinDistance;
            float margin = minDistance * 0.5f;
            int first = into.Count;
            for (int slot = 0; slot < s.maxPerRegion; slot++)
            {
                int stream = slot * StreamsPerSlot;
                if (PlacementRandom.Value(worldSeed, Salt, rx, rz, stream) >= s.spawnChance)
                    continue;
                float x = rx * size + margin + PlacementRandom.Value(worldSeed, Salt, rx, rz, stream + 1) * Mathf.Max(0f, size - 2f * margin);
                float z = rz * size + margin + PlacementRandom.Value(worldSeed, Salt, rx, rz, stream + 2) * Mathf.Max(0f, size - 2f * margin);
                var point = new Vector2(x, z);
                bool spaced = true;
                for (int i = first; i < into.Count && spaced; i++)
                    spaced = (into[i].Point - point).sqrMagnitude >= minDistance * minDistance;
                if (!spaced)
                    continue;
                int seed = (int)PlacementRandom.Hash(worldSeed, Salt, rx, rz, stream + 5);
                into.Add(new Site
                {
                    Id = new CaveSiteId(rx, rz, slot),
                    Point = point,
                    Yaw = PlacementRandom.Value(worldSeed, Salt, rx, rz, stream + 4) * 360f,
                    Seed = seed == 0 ? 1 : seed,
                });
            }
        }

        /// <summary>The planned caves whose point lies in [<paramref name="min"/>, <paramref name="max"/>) (a chunk, a map area).</summary>
        public static void SitesInArea(CaveEntranceSettings s, int worldSeed, Vector2 min, Vector2 max, List<Site> into)
        {
            if (s == null || into == null || !s.IsUsable)
                return;
            float size = Mathf.Max(64f, s.regionSize);
            int rx0 = Mathf.FloorToInt(min.x / size), rx1 = Mathf.FloorToInt(max.x / size);
            int rz0 = Mathf.FloorToInt(min.y / size), rz1 = Mathf.FloorToInt(max.y / size);
            var region = new List<Site>();
            for (int rz = rz0; rz <= rz1; rz++)
                for (int rx = rx0; rx <= rx1; rx++)
                {
                    region.Clear();
                    RegionSites(s, worldSeed, rx, rz, region);
                    foreach (Site site in region)
                        if (site.Point.x >= min.x && site.Point.y >= min.y && site.Point.x < max.x && site.Point.y < max.y)
                            into.Add(site);
                }
        }
    }
}
