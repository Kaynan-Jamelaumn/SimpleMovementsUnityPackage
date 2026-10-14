using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Base of the dungeon's usable objects (rest fountains, shrines, chests, herbs). Used with the player's Interact key
    /// (it is an <see cref="Interactable"/>, found by the player's interaction ray), or - when Hold To Use is above 0 - by
    /// standing next to it for that long, so it also works without any interaction setup.
    /// <para>Network sessions: only the server uses it (hold-to-use measured from the server's positions, or a client's
    /// request validated by GameNet.NetDungeonRun - see <see cref="IDungeonNetInteract"/>); the number of uses is
    /// published to the clients, whose copies show it (<see cref="ApplyNetState"/>).</para>
    /// </summary>
    public abstract class DungeonInteractable : Interactable, IDungeonNetState, IDungeonNetInteract
    {
        [Tooltip("Seconds a player must stand within Use Distance to use it without the Interact key (0 = Interact key only).")]
        [Min(0f)] public float holdToUse = 1.2f;
        [Tooltip("How close a player must stand (meters).")]
        [Min(0.3f)] public float useDistance = 1.8f;
        [Tooltip("How many times it can be used (0 = unlimited).")]
        [Min(0)] public int uses = 1;
        [Tooltip("Seconds between uses.")]
        [Min(0f)] public float cooldown = 2f;

        public int TimesUsed { get; private set; }
        public bool UsedUp => uses > 0 && TimesUsed >= uses;

        private float held;
        private float readyAt;
        private bool usedUpShown;
        private new DungeonSpawned tag;

        protected DungeonInstance Dungeon => (tag != null || (tag = GetComponent<DungeonSpawned>()) != null) ? tag.Dungeon : GetComponentInParent<DungeonInstance>();

        /// <summary>The Interact key: used by the nearest living player.</summary>
        public override void Interact()
        {
            if (DungeonNet.IsReplica(this))
            {
                // A client's copy: the server uses it (for this machine's player).
                CombatEntity mine = PlayerLocator.Local;
                DungeonNet.TryRelayInteract(this, mine != null ? PlayerLocator.MovableRoot(mine) : null);
                return;
            }
            CombatEntity player = DungeonPlayers.NearestAlive(transform.position, useDistance + 2f);
            // A host's Interact key is its own player's, even when a client's player stands nearer.
            CombatEntity local = GameNet.NetAuthority.IsNetworked ? PlayerLocator.Local : null;
            if (local != null && local != player && DungeonPlayers.IsWithin(local, transform.position, useDistance + 2f))
                player = local;
            if (player != null)
                TryUse(player);
        }

        protected virtual void Update()
        {
            if (holdToUse <= 0f || UsedUp || Time.time < readyAt)
                return;
            if (DungeonNet.IsReplica(this))
                return;   // the server measures who stands next to it
            CombatEntity player = DungeonPlayers.NearestAlive(transform.position, useDistance);
            held = player != null ? held + Time.deltaTime : 0f;
            if (player != null && held >= holdToUse)
            {
                held = 0f;
                TryUse(player);
            }
        }

        public bool TryUse(CombatEntity player)
        {
            if (UsedUp || Time.time < readyAt || player == null || DungeonNet.IsReplica(this))
                return false;
            if (!Use(player))
                return false;
            TimesUsed++;
            readyAt = Time.time + cooldown;
            if (UsedUp && !usedUpShown)
            {
                usedUpShown = true;
                OnUsedUp();
            }
            DungeonNet.Publish(this, NetState);
            return true;
        }

        /// <summary>The state published to clients after each use (default: the number of uses).</summary>
        protected virtual int NetState => TimesUsed;

        /// <summary>
        /// A client's copy shows the server's state: the number of uses (and, once used up, what <see cref="OnUsedUp"/>
        /// shows). Override <see cref="OnNetState"/> for what else a use shows (a lid, a handle...).
        /// </summary>
        public virtual void ApplyNetState(int state, byte[] payload)
        {
            TimesUsed = Mathf.Max(0, state);
            OnNetState(state, payload);
            if (UsedUp && !usedUpShown)
            {
                usedUpShown = true;
                OnUsedUp();
            }
        }

        /// <summary>A client's copy received a new state from the server (see <see cref="ApplyNetState"/>).</summary>
        protected virtual void OnNetState(int state, byte[] payload)
        {
        }

        /// <summary>How close a player must be (server positions) for a client's request to use it.</summary>
        public virtual float NetInteractRange => useDistance + 1.5f;

        /// <summary>The server uses it for a client's validated request.</summary>
        public virtual bool NetInteract(CombatEntity player) => TryUse(player);

        /// <summary>What using it does. Return false when nothing happened (it isn't counted as used).</summary>
        protected abstract bool Use(CombatEntity player);

        /// <summary>Called once it can't be used any more (dim its light, close it...).</summary>
        protected virtual void OnUsedUp()
        {
            foreach (Light l in GetComponentsInChildren<Light>())
                l.intensity *= 0.3f;
        }
    }
}
