using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>One kind of loot container / pickup.</summary>
    [Serializable]
    public class LootEntry
    {
        [Tooltip("Label (reports, spawned object names).")]
        public string name = "Chest";
        [Tooltip("The loot prefab (chest, pickup...). Empty = the Placeholder primitive.")]
        public GameObject prefab;
        [Tooltip("Built-in stand-in used when Prefab is empty.")]
        public DungeonPrimitive placeholder = DungeonPrimitive.Chest;
        [Tooltip("Relative chance of this entry.")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("0 = common. Higher tiers are favoured deeper, in treasure rooms and after bosses.")]
        [Min(0)] public int tier;
        [Tooltip("Where along the entrance-to-exit progression (0..1) it may appear.")]
        public FloatRange progress = new FloatRange(0f, 1f);
        [Tooltip("Zone styles it may appear in.")]
        public ZoneMask styles = ZoneMask.All;
        [Tooltip("Where in an area it goes: Anywhere, Wall Adjacent (against a wall), Center, Corner, Corridor, Doorway, Dead End, Chokepoint, or Transition (where zone styles meet).")]
        public PropPlacement placement = PropPlacement.WallAdjacent;
        [Tooltip("First floor it may appear on (0 = top floor).")]
        public int minFloor;
        [Tooltip("Last floor it may appear on (-1 = no limit).")]
        public int maxFloor = -1;
    }

    /// <summary>Loot for dungeons (see <see cref="PopulationSettings.loot"/>).</summary>
    [CreateAssetMenu(menuName = "SimpleMovements/Dungeon/Loot Table", fileName = "DungeonLoot")]
    public class DungeonLootTable : ScriptableObject
    {
        [Tooltip("The loot kinds. How many are placed is set on the profile (Population > Loot).")]
        public List<LootEntry> entries = new List<LootEntry>();
    }
}
