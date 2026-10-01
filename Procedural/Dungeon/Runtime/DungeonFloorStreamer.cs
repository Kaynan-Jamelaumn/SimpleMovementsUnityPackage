using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Keeps only the floors near the player active (geometry, colliders, NavMesh, mobs, lights), so deep dungeons
    /// cost what a few floors cost. Tracks the player's height to know the current floor and raises
    /// <see cref="DungeonInstance.FloorEntered"/>. Added to the dungeon root by the builder.
    /// </summary>
    [RequireComponent(typeof(DungeonInstance))]
    public class DungeonFloorStreamer : MonoBehaviour
    {
        [Tooltip("Only keep the floors around the player's active (set from the profile's Build > Stream Floors). Off = every floor stays active.")]
        public bool streaming = true;
        [Tooltip("Floors above and below the current one that stay active (at least 1 so stairs stay visible).")]
        [Range(1, 4)] public int floorsAround = 1;
        [Tooltip("What to follow; empty = the players in the dungeon (every floor with a player stays active).")]
        public Transform target;

        private DungeonInstance dungeon;
        private int applied = -1;

        private void Awake()
        {
            dungeon = GetComponent<DungeonInstance>();
        }

        private void Update()
        {
            if (dungeon == null || dungeon.Layout == null)
                return;
            // The floors that have a player on them: the dungeon's participants (or the Local player / Target).
            Transform main = target != null ? target : LocalTraveller();
            int floor = main != null ? dungeon.FloorAt(main.position) : dungeon.CurrentFloor;
            dungeon.SetCurrentFloor(floor);
            int lowest = floor, highest = floor;
            if (target == null)
            {
                IReadOnlyList<GameObject> group = DungeonSession.Participants;
                for (int i = 0; i < group.Count; i++)
                {
                    if (group[i] == null) continue;
                    int f = dungeon.FloorAt(group[i].transform.position);
                    lowest = Mathf.Min(lowest, f);
                    highest = Mathf.Max(highest, f);
                }
            }
            // Floors must stay active until they're fully built (their NavMesh is baked from active colliders).
            int key = floor * 1000 + lowest * 31 + highest;
            if (dungeon.IsFullyBuilt && key != applied)
            {
                applied = key;
                ApplyRange(lowest, highest);
            }
        }

        /// <summary>This machine's player when it is in the dungeon, else the first participant.</summary>
        private static Transform LocalTraveller()
        {
            CombatEntity local = PlayerLocator.Local;
            if (local != null && DungeonSession.IsParticipant(local.gameObject))
                return local.transform;
            IReadOnlyList<GameObject> group = DungeonSession.Participants;
            for (int i = 0; i < group.Count; i++)
                if (group[i] != null)
                    return group[i].transform;
            return local != null ? local.transform : null;
        }

        /// <summary>Activates the floors around <paramref name="floor"/> (all of them when streaming is off).</summary>
        public void Apply(int floor)
        {
            applied = floor * 1000 + floor * 31 + floor;
            ApplyRange(floor, floor);
        }

        /// <summary>Activates the floors around every floor from <paramref name="lowest"/> to <paramref name="highest"/> (players spread over floors).</summary>
        private void ApplyRange(int lowest, int highest)
        {
            int around = Mathf.Max(1, floorsAround);
            for (int f = 0; f < dungeon.FloorCount; f++)
            {
                Transform root = dungeon.FloorRoot(f);
                if (root == null)
                    continue;
                bool active = !streaming || (f >= lowest - around && f <= highest + around);
                if (root.gameObject.activeSelf != active)
                    root.gameObject.SetActive(active);
            }
        }
    }
}
