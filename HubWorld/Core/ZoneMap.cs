using System;
using System.Collections.Generic;
using UnityEngine;

namespace HubWorld
{
    /// <summary>
    /// One ring of the world around the city (see <see cref="ZoneMap"/>): from the previous ring's outer edge out to
    /// <see cref="outerRadius"/>. Values marked "start / end" are interpolated across the ring, so difficulty grows
    /// smoothly with distance instead of jumping at the borders.
    /// </summary>
    [Serializable]
    public class ZoneBand
    {
        [Tooltip("Name shown on the HUD (\"Safe Zone\", \"Dangerous Zone\", \"The Wilds\").")]
        public string name = "Zone";
        [Tooltip("Distance from the city centre where this ring ends (metres). The last ring goes on forever.")]
        [Min(0f)] public float outerRadius = 1000f;
        [Tooltip("Players may fight each other here (both must be in a PvP ring).")]
        public bool pvp;
        [Tooltip("Dungeon difficulty of portals at the inner edge of the ring (1 = normal).")]
        [Min(0.1f)] public float dungeonDifficultyStart = 1f;
        [Tooltip("Dungeon difficulty at the outer edge (the last ring keeps growing by Zone Settings ▸ Difficulty Per Km Beyond).")]
        [Min(0.1f)] public float dungeonDifficultyEnd = 1f;
        [Tooltip("Mob strength at the inner edge: multiplies world mobs' max health and damage (1 = as authored).")]
        [Min(0.1f)] public float mobStrengthStart = 1f;
        [Tooltip("Mob strength at the outer edge.")]
        [Min(0.1f)] public float mobStrengthEnd = 1f;
        [Tooltip("Reward multiplier for game systems that use it (loot quantity / quality, XP). 1 = normal.")]
        [Min(0f)] public float rewardMultiplier = 1f;
        [Tooltip("HUD colour of the zone label.")]
        public Color color = Color.white;

        public ZoneBand() { }

        public ZoneBand(string name, float outerRadius, bool pvp, float difficultyStart, float difficultyEnd, float mobStart, float mobEnd, float reward, Color color)
        {
            this.name = name;
            this.outerRadius = outerRadius;
            this.pvp = pvp;
            dungeonDifficultyStart = difficultyStart;
            dungeonDifficultyEnd = difficultyEnd;
            mobStrengthStart = mobStart;
            mobStrengthEnd = mobEnd;
            rewardMultiplier = reward;
            this.color = color;
        }
    }

    /// <summary>What the zone map says about one place.</summary>
    public struct ZoneInfo
    {
        /// <summary>Ring index (0 = nearest the city); -1 inside the city walls.</summary>
        public int index;
        public string name;
        public bool pvp;
        /// <summary>Horizontal distance from the city centre (metres).</summary>
        public float distance;
        /// <summary>0 at the ring's inner edge, 1 at its outer edge (0 in the last ring's open end).</summary>
        public float depth;
        public float dungeonDifficulty;
        public float mobStrength;
        public float rewardMultiplier;
        public Color color;
        public bool InsideCity => index < 0;
    }

    /// <summary>
    /// The world's rings around the city, as plain rules: which ring a point is in, whether PvP is on there, and how hard
    /// its dungeons and mobs are. Safer, easier and poorer near the city; dangerous, harder and richer far away.
    /// </summary>
    public sealed class ZoneMap
    {
        private readonly List<ZoneBand> bands = new List<ZoneBand>();

        /// <param name="center">City centre (world x, z).</param>
        /// <param name="cityRadius">Radius of the city's walls: inside it is the city (no PvP, no difficulty).</param>
        /// <param name="rings">The rings, any order (sorted by outer radius). Empty = one safe ring.</param>
        /// <param name="difficultyPerKmBeyond">How much dungeon difficulty keeps growing per km past the last ring's start.</param>
        /// <param name="maxDifficulty">Dungeon difficulty never goes above this.</param>
        public ZoneMap(Vector2 center, float cityRadius, IEnumerable<ZoneBand> rings, float difficultyPerKmBeyond = 0.5f, float maxDifficulty = 10f)
        {
            Center = center;
            CityRadius = Mathf.Max(0f, cityRadius);
            DifficultyPerKmBeyond = Mathf.Max(0f, difficultyPerKmBeyond);
            MaxDifficulty = Mathf.Max(0.1f, maxDifficulty);
            if (rings != null)
                foreach (ZoneBand b in rings)
                    if (b != null)
                        bands.Add(b);
            if (bands.Count == 0)
                bands.Add(new ZoneBand("Safe Zone", 1000f, false, 1f, 1f, 1f, 1f, 1f, Color.white));
            bands.Sort((a, b) => a.outerRadius.CompareTo(b.outerRadius));
        }

        public Vector2 Center { get; }
        public float CityRadius { get; }
        public float DifficultyPerKmBeyond { get; }
        public float MaxDifficulty { get; }
        public IReadOnlyList<ZoneBand> Bands => bands;

        /// <summary>Horizontal distance of a world position from the city centre.</summary>
        public float DistanceFromCity(Vector3 worldPosition) => Vector2.Distance(new Vector2(worldPosition.x, worldPosition.z), Center);

        /// <summary>The ring index at a distance from the city (-1 inside the walls).</summary>
        public int IndexAtDistance(float distance)
        {
            if (distance < CityRadius)
                return -1;
            for (int i = 0; i < bands.Count; i++)
                if (distance < bands[i].outerRadius)
                    return i;
            return bands.Count - 1;
        }

        public ZoneInfo At(Vector3 worldPosition) => AtDistance(DistanceFromCity(worldPosition));

        /// <summary>Everything about the place <paramref name="distance"/> metres from the city centre.</summary>
        public ZoneInfo AtDistance(float distance)
        {
            int index = IndexAtDistance(distance);
            if (index < 0)
            {
                ZoneBand first = bands[0];
                return new ZoneInfo
                {
                    index = -1,
                    name = "City",
                    pvp = false,
                    distance = distance,
                    depth = 0f,
                    dungeonDifficulty = first.dungeonDifficultyStart,
                    mobStrength = first.mobStrengthStart,
                    rewardMultiplier = first.rewardMultiplier,
                    color = first.color,
                };
            }
            ZoneBand band = bands[index];
            float inner = index == 0 ? CityRadius : bands[index - 1].outerRadius;
            bool last = index == bands.Count - 1;
            float width = band.outerRadius - inner;
            float depth = width > 0.01f ? Mathf.Clamp01((distance - inner) / width) : 0f;
            float difficulty = Mathf.Lerp(band.dungeonDifficultyStart, band.dungeonDifficultyEnd, depth);
            float mobs = Mathf.Lerp(band.mobStrengthStart, band.mobStrengthEnd, depth);
            if (last && distance > band.outerRadius)
            {
                // The open end of the last ring keeps getting harder.
                difficulty = band.dungeonDifficultyEnd + (distance - band.outerRadius) / 1000f * DifficultyPerKmBeyond;
                mobs = band.mobStrengthEnd * (1f + (distance - band.outerRadius) / 1000f * DifficultyPerKmBeyond * 0.5f);
                depth = 1f;
            }
            return new ZoneInfo
            {
                index = index,
                name = band.name,
                pvp = band.pvp,
                distance = distance,
                depth = depth,
                dungeonDifficulty = Mathf.Clamp(difficulty, 0.1f, MaxDifficulty),
                mobStrength = Mathf.Max(0.1f, mobs),
                rewardMultiplier = band.rewardMultiplier,
                color = band.color,
            };
        }

        /// <summary>Is PvP on at a world position?</summary>
        public bool IsPvp(Vector3 worldPosition) => At(worldPosition).pvp;

        /// <summary>The dungeon difficulty of a portal at a world position.</summary>
        public float DungeonDifficultyAt(Vector3 worldPosition) => At(worldPosition).dungeonDifficulty;
    }
}
