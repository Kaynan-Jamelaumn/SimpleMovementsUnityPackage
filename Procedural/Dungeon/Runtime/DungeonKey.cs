using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A key lying somewhere on a floor. The first player to touch it picks it up for the whole party
    /// (<see cref="DungeonKeyRing"/>); the <see cref="DungeonLockedDoor"/> with the same id then opens for any of them.
    /// </summary>
    public class DungeonKey : MonoBehaviour
    {
        [Tooltip("Name shown in messages.")]
        public string keyName = "Vault Key";
        [Tooltip("How close a player must come to pick it up (meters).")]
        [Min(0.3f)] public float pickupDistance = 1.3f;
        [Tooltip("Spin and bob so it catches the eye.")]
        public bool animate = true;

        private new DungeonSpawned tag;
        private Vector3 rest;

        private void Start()
        {
            tag = GetComponent<DungeonSpawned>();
            rest = transform.localPosition;
        }

        private void Update()
        {
            if (animate)
            {
                transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
                transform.localPosition = rest + Vector3.up * (0.12f + Mathf.Sin(Time.time * 2.2f) * 0.08f);
            }
            // A client's copy is only shown: the server's key is picked up and the copy hidden when it is told.
            if (DungeonNet.IsReplica(this) || DungeonPlayers.NearestAlive(transform.position, pickupDistance) == null)
                return;
            DungeonInstance dungeon = tag != null ? tag.Dungeon : null;
            DungeonKeyRing.Add(dungeon, tag != null ? tag.link : 0);
            if (dungeon != null)
                dungeon.Stats.Bump(ref dungeon.Stats.keysFound);
            DungeonMessages.Show(this, $"Found the {keyName}! A locked vault on this floor will open now.", true);
            DungeonNet.Publish(this, DungeonNet.Presence.Removed, null, DungeonNet.Channel.Presence);
            Destroy(gameObject);
        }
    }
}
