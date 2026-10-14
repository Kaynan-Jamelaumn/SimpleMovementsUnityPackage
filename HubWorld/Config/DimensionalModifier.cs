using System;
using System.Collections.Generic;
using ProceduralDungeon;
using UnityEngine;

namespace HubWorld
{
    /// <summary>How one mob type is affected by a dimensional modifier.</summary>
    [Serializable]
    public class ModifierMobWeight
    {
        [Tooltip("The mob prefab (as listed in the World scene's Endless Terrain ▸ Mob Settings).")]
        public GameObject mobPrefab;
        [Tooltip("Multiplies its spawn weight: 0 = absent from this dimension, 3 = three times as common.")]
        [Min(0f)] public float weightMultiplier = 1f;
    }

    /// <summary>
    /// A special condition the world can have for one cycle: Frozen Dimension, Eclipse, Sunken World, Invaded World...
    /// It is rolled when the cycle before it starts, so the city can announce it before anyone goes out. It can swap the
    /// whole world scene (other biomes and terrain settings), force the weather, tint the sky, reweight or ban mob types,
    /// make mobs and dungeons harder and change which dungeons the portals lead to.
    /// </summary>
    [CreateAssetMenu(fileName = "Dimensional Modifier", menuName = "SimpleMovements/Hub World/Dimensional Modifier", order = 1)]
    public class DimensionalModifier : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Name shown in announcements and on the HUD (\"Frozen Dimension\").")]
        public string displayName = "Frozen Dimension";
        [Tooltip("Announced in the city before the jump into this dimension.")]
        [TextArea(2, 4)] public string announcement = "Frost creeps over the walls: the next dimension is frozen.";
        [Tooltip("How often it is rolled compared to the other modifiers (and to Hub World Settings ▸ No Modifier Weight).")]
        [Min(0f)] public float weight = 1f;

        [Header("World")]
        [Tooltip("OPTIONAL. Name of a World scene to load instead of the default one for this cycle (in Build Settings) - " +
                 "a variant with other biomes, terrain or mob lists. Empty = the default World scene.")]
        public string worldSceneOverride = "";

        [Header("Weather and sky")]
        [Tooltip("Force a weather for the whole cycle (World scenes with a Weather System).")]
        public bool forceWeather;
        public WeatherType weather = WeatherType.Snow;
        [Range(0f, 1f)] public float weatherStrength = 1f;
        [Tooltip("Tint the world's ambient light and fog while in this dimension.")]
        public bool tintAtmosphere;
        public Color ambientTint = new Color(0.75f, 0.85f, 1f);
        public Color fogColor = new Color(0.7f, 0.8f, 0.9f);
        [Min(0f)] public float fogDensityMultiplier = 1.5f;
        [Tooltip("Multiplies the sun's intensity (0.2 = an eclipse).")]
        [Min(0f)] public float sunIntensityMultiplier = 1f;

        [Header("Mobs")]
        [Tooltip("Multiplies world mobs' strength (health and damage), on top of the zone's.")]
        [Min(0.1f)] public float mobStrengthMultiplier = 1f;
        [Tooltip("Mob types made more or less common (or absent) in this dimension.")]
        public List<ModifierMobWeight> mobWeights = new List<ModifierMobWeight>();
        [Tooltip("Only the mob types listed above appear (an Invaded World: only the invaders).")]
        public bool onlyListedMobs;

        [Header("Dungeons")]
        [Tooltip("Multiplies the dungeon difficulty of every portal, on top of the zone's.")]
        [Min(0.1f)] public float dungeonDifficultyMultiplier = 1f;
        [Tooltip("OPTIONAL. Portals lead to these kinds of dungeon (one per portal, stable per portal). Empty = the portals' own.")]
        public List<DungeonProfile> dungeonProfiles = new List<DungeonProfile>();

        /// <summary>The spawn weight multiplier of a mob prefab in this dimension.</summary>
        public float MobWeightFor(GameObject prefab)
        {
            if (mobWeights != null)
                foreach (ModifierMobWeight w in mobWeights)
                    if (w != null && w.mobPrefab == prefab)
                        return Mathf.Max(0f, w.weightMultiplier);
            return onlyListedMobs ? 0f : 1f;
        }

        /// <summary>The dungeon profile for a portal's seed, or null to keep the portal's own.</summary>
        public DungeonProfile ProfileFor(int seed)
        {
            if (dungeonProfiles == null || dungeonProfiles.Count == 0)
                return null;
            int count = 0;
            foreach (DungeonProfile p in dungeonProfiles)
                if (p != null)
                    count++;
            if (count == 0)
                return null;
            int pick = (int)(CycleSeeds.Value(seed, 0x50524F46u) * count); // "PROF"
            foreach (DungeonProfile p in dungeonProfiles)
            {
                if (p == null)
                    continue;
                if (pick-- == 0)
                    return p;
            }
            return null;
        }
    }
}
