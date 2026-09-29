using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>One kind of mob group the dungeon can place.</summary>
    [Serializable]
    public class EncounterEntry
    {
        [Tooltip("Label (reports, spawned object names).")]
        public string name = "Mob";
        [Tooltip("Mob prefab, with a NavMeshAgent of the profile's Build > NavMesh Agent Type (0 = Humanoid). Spawned on the floor's NavMesh once it is baked. Empty = the Placeholder primitive.")]
        public GameObject prefab;
        [Tooltip("Built-in stand-in used when Prefab is empty.")]
        public DungeonPrimitive placeholder = DungeonPrimitive.PlaceholderMob;
        [Tooltip("A boss: placed only in the Boss area, once. Normal encounters never pick it.")]
        public bool boss;
        [Tooltip("Relative chance of this entry among those allowed in an area.")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("Budget cost of one mob: tougher mobs cost more, so fewer of them fit (see Population > Encounter Density).")]
        [Min(1)] public int cost = 1;
        [Tooltip("Mobs per group (min, max).")]
        public IntRange packSize = new IntRange(1, 1);
        [Tooltip("How far pack members spread from the pack centre (cells).")]
        public float packRadius = 2.5f;
        [Tooltip("First floor it may appear on (0 = top floor).")]
        public int minFloor;
        [Tooltip("Last floor it may appear on (-1 = no limit).")]
        public int maxFloor = -1;
        [Tooltip("Where along the entrance-to-exit progression (0..1) it may appear.")]
        public FloatRange progress = new FloatRange(0f, 1f);
        [Tooltip("Zone styles it may appear in (Built, Cavern, Ruins).")]
        public ZoneMask styles = ZoneMask.All;
        [Tooltip("Only in areas with these roles (empty = any area except Entrance, Rest and landings).")]
        public List<AreaRole> roles = new List<AreaRole>();
        [Tooltip("Needed distance from walls (cells).")]
        public float clearance = 1f;
        [Tooltip("Tier / level handed to spawned mobs (read it from the DungeonSpawned component).")]
        public int tier;
    }

    /// <summary>Mob groups for dungeons (see <see cref="PopulationSettings.encounters"/>).</summary>
    [CreateAssetMenu(menuName = "SimpleMovements/Dungeon/Encounter Table", fileName = "DungeonEncounters")]
    public class DungeonEncounterTable : ScriptableObject
    {
        [Tooltip("The mob groups. The inspector's Import button copies EndlessTerrain's world mob list (prefab, weight, rarity, packs).")]
        public List<EncounterEntry> entries = new List<EncounterEntry>();
    }
}
