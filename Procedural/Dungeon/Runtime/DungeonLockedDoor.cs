using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A vault door. It opens for any player who comes close while the party holds its key (a <see cref="DungeonKey"/>
    /// with the same id, found elsewhere on the floor - see <see cref="DungeonKeyRing"/>), or when <see cref="Unlock"/>
    /// is called. Without the key it tells the player to look for it.
    /// </summary>
    public class DungeonLockedDoor : DungeonBarrier
    {
        [Tooltip("How close a player must come to try the door (meters).")]
        [Min(0.5f)] public float reach = 2.2f;
        [Tooltip("Name of the key in messages.")]
        public string keyName = "Vault Key";

        public bool IsUnlocked { get; private set; }

        private float nextHint;

        private void Start() => Initialize(true);

        protected override void Update()
        {
            base.Update();
            if (IsUnlocked)
                return;
            CombatEntity player = DungeonPlayers.NearestAlive(transform.position, reach);
            if (player == null)
                return;
            if (DungeonKeyRing.Has(Dungeon, Link))
            {
                Unlock();
                DungeonMessages.Show($"The {keyName} opens the vault.");
            }
            else if (Time.time >= nextHint)
            {
                nextHint = Time.time + 6f;
                DungeonMessages.Show($"Locked. The {keyName} must be somewhere on this floor.");
            }
        }

        public void Unlock()
        {
            if (IsUnlocked)
                return;
            IsUnlocked = true;
            SetClosed(false);
        }
    }
}
