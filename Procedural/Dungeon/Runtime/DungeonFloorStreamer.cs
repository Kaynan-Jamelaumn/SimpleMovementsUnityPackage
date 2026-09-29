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
        [Tooltip("What to follow; empty = the object tagged Player.")]
        public Transform target;
        [Tooltip("Tag used to find the player when Target is empty.")]
        public string playerTag = "Player";

        private DungeonInstance dungeon;
        private int applied = -1;
        private float nextSearch;

        private void Awake()
        {
            dungeon = GetComponent<DungeonInstance>();
        }

        private void Update()
        {
            if (dungeon == null || dungeon.Layout == null)
                return;
            if (target == null && Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + 1f;
                GameObject p = GameObject.FindGameObjectWithTag(playerTag);
                target = p != null ? p.transform : null;
            }

            int floor = target != null ? dungeon.FloorAt(target.position) : dungeon.CurrentFloor;
            dungeon.SetCurrentFloor(floor);
            // Floors must stay active until they're fully built (their NavMesh is baked from active colliders).
            if (dungeon.IsFullyBuilt && floor != applied)
                Apply(floor);
        }

        /// <summary>Activates the floors around <paramref name="floor"/> (all of them when streaming is off).</summary>
        public void Apply(int floor)
        {
            applied = floor;
            for (int f = 0; f < dungeon.FloorCount; f++)
            {
                Transform root = dungeon.FloorRoot(f);
                if (root == null)
                    continue;
                bool active = !streaming || Mathf.Abs(f - floor) <= Mathf.Max(1, floorsAround);
                if (root.gameObject.activeSelf != active)
                    root.gameObject.SetActive(active);
            }
        }
    }
}
