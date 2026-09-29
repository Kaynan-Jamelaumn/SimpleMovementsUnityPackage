using System;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Added to every object the dungeon spawns from its placements (mobs, loot, props, portals): tells gameplay code
    /// what it is and where it belongs, and lets the dungeon track it (e.g. the respawn director counting living mobs).
    /// </summary>
    public class DungeonSpawned : MonoBehaviour
    {
        [Tooltip("What this object is (mob, boss, loot, prop...). Set by the builder - read it from your scripts.")]
        public PlacementKind kind;
        [Tooltip("Floor index (0 = top floor).")]
        public int floor;
        [Tooltip("Area id on that floor (-1 = none): DungeonInstance.Layout.Floors[floor].Areas.")]
        public int area = -1;
        [Tooltip("Tier / level from the table entry (scale stats or rewards with it).")]
        public int tier;
        [Tooltip("Pack id for mobs spawned together (-1 = none).")]
        public int group = -1;
        [Tooltip("Index in DungeonLayout.Placements (-1 for objects spawned later, e.g. respawns).")]
        public int placementIndex = -1;
        [Tooltip("Name of the table entry it came from.")]
        public string entryName = "";

        /// <summary>The dungeon it belongs to.</summary>
        public DungeonInstance Dungeon { get; internal set; }

        /// <summary>The prefab it was made from (null for primitives); used for pooling.</summary>
        public GameObject SourcePrefab { get; internal set; }

        /// <summary>The area it stands in, or null.</summary>
        public Area Area => Dungeon != null && area >= 0 ? Dungeon.Layout.Floors[floor].Areas[area] : null;

        /// <summary>Raised when the object is destroyed (a mob killed, loot picked up...).</summary>
        public event Action<DungeonSpawned> Destroyed;

        private void OnDestroy()
        {
            Destroyed?.Invoke(this);
            Destroyed = null;
        }
    }
}
