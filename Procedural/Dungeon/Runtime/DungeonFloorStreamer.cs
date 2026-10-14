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
            // The floors that have a player on them: this dungeon's participants (or the Local player / Target).
            DungeonRun run = DungeonSession.RunOf(dungeon);
            Transform main = target != null ? target : LocalTraveller(run);
            int floor = main != null ? dungeon.FloorAt(main.position) : dungeon.CurrentFloor;
            dungeon.SetCurrentFloor(floor);
            int lowest = floor, highest = floor;
            if (target == null && run != null)
            {
                IReadOnlyList<GameObject> group = run.Participants;
                for (int i = 0; i < group.Count; i++)
                {
                    if (group[i] == null) continue;
                    int f = dungeon.FloorAt(group[i].transform.position);
                    lowest = Mathf.Min(lowest, f);
                    highest = Mathf.Max(highest, f);
                }
            }
            // Floors must stay active until they're fully built (their NavMesh is baked from active colliders). A client's
            // copy bakes nothing; its floors arrive one by one, so a new floor also re-applies the range.
            int key = floor * 1000 + lowest * 31 + highest + (dungeon.IsReplica ? dungeon.FloorCount * 1000000 : 0);
            if ((dungeon.IsFullyBuilt || dungeon.IsReplica) && key != applied)
            {
                applied = key;
                ApplyRange(lowest, highest);
            }
        }

        /// <summary>This machine's player when it is in this dungeon, else the first participant.</summary>
        private static Transform LocalTraveller(DungeonRun run)
        {
            CombatEntity local = PlayerLocator.Local;
            if (run == null)
                return local != null ? local.transform : null;
            if (local != null && run.IsParticipant(local.gameObject))
                return local.transform;
            IReadOnlyList<GameObject> group = run.Participants;
            for (int i = 0; i < group.Count; i++)
                if (group[i] != null)
                    return group[i].transform;
            return null;
        }

        /// <summary>Activates the floors around <paramref name="floor"/> (all of them when streaming is off).</summary>
        public void Apply(int floor)
        {
            applied = floor * 1000 + floor * 31 + floor;
            ApplyRange(floor, floor);
        }

        /// <summary>
        /// Are floors switched off here? Not on a network server: the mobs and item pickups it hands to the network live
        /// at the root of the run's scene (Fish-Net spawns root objects only), outside their floor's hierarchy, so a
        /// switched-off floor would leave them without ground and NavMesh. Clients' copies (presentation only) and single
        /// player stream as set.
        /// </summary>
        private bool StreamsFloors =>
            streaming && (dungeon.IsReplica || !GameNet.NetAuthority.IsNetworked || !GameNet.NetAuthority.IsServer);

        /// <summary>Activates the floors around every floor from <paramref name="lowest"/> to <paramref name="highest"/> (players spread over floors).</summary>
        private void ApplyRange(int lowest, int highest)
        {
            bool streams = StreamsFloors;
            int around = Mathf.Max(1, floorsAround);
            for (int f = 0; f < dungeon.FloorCount; f++)
            {
                Transform root = dungeon.FloorRoot(f);
                if (root == null)
                    continue;
                bool active = !streams || (f >= lowest - around && f <= highest + around);
                if (root.gameObject.activeSelf != active)
                    root.gameObject.SetActive(active);
            }
        }
    }
}
