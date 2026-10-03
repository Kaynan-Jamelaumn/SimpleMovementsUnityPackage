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
        [Tooltip("Spawned hidden and revealed by its room's event (ambush waves, puzzle and cleared-room rewards).")]
        public bool dormant;
        [Tooltip("Ambush wave of a hidden mob (1 = first; 0 = not part of a wave).")]
        public int wave;
        [Tooltip("An elite mob (guardian and throne rooms): bigger and a tier higher.")]
        public bool elite;
        [Tooltip("Key id of keys and locked doors, order of puzzle plates, pair of teleporters, group of tripwires.")]
        public int link;
        [Tooltip("Mobs: asleep (barracks), roaming between rooms (mini-boss), a pit fight's champion.")]
        public MobOrder order;

        /// <summary>The dungeon it belongs to.</summary>
        public DungeonInstance Dungeon { get; internal set; }

        /// <summary>The prefab it was made from (null for primitives); used for pooling.</summary>
        public GameObject SourcePrefab { get; internal set; }

        /// <summary>The area it stands in, or null.</summary>
        public Area Area => Dungeon != null && area >= 0 ? Dungeon.Layout.Floors[floor].Areas[area] : null;

        /// <summary>Raised when the object is destroyed (a mob killed, loot picked up...).</summary>
        public event Action<DungeonSpawned> Destroyed;

        /// <summary>Is the object hidden, waiting for its room's event?</summary>
        public bool IsHidden => dormant && !gameObject.activeSelf;

        /// <summary>Shows a hidden object (its NavMeshAgent, if any, is put back on the NavMesh where it stands).</summary>
        public void Reveal()
        {
            dormant = false;
            if (gameObject.activeSelf)
                return;
            gameObject.SetActive(true);
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled && UnityEngine.AI.NavMesh.SamplePosition(transform.position, out UnityEngine.AI.NavMeshHit hit, 2.5f, agent.areaMask))
                agent.Warp(hit.position);
            Revealed?.Invoke(this);
        }

        /// <summary>Raised when a hidden object is revealed.</summary>
        public event Action<DungeonSpawned> Revealed;

        /// <summary>Alive for a room's fight: a living Combat Entity (placeholders and destroyed mobs don't count).</summary>
        public bool IsAliveMob
        {
            get
            {
                if (this == null || (kind != PlacementKind.Mob && kind != PlacementKind.Boss))
                    return false;
                CombatEntity e = GetComponentInChildren<CombatEntity>(true);
                return e != null && !e.IsDead;
            }
        }

        private void OnDestroy()
        {
            Destroyed?.Invoke(this);
            Destroyed = null;
        }
    }
}
