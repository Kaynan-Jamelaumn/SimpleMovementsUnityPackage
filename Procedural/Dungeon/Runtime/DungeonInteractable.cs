using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Base of the dungeon's usable objects (rest fountains, shrines, chests, herbs). Used with the player's Interact key
    /// (it is an <see cref="Interactable"/>, found by the player's interaction ray), or - when Hold To Use is above 0 - by
    /// standing next to it for that long, so it also works without any interaction setup.
    /// </summary>
    public abstract class DungeonInteractable : Interactable
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
        private DungeonSpawned tag;

        protected DungeonInstance Dungeon => (tag != null || (tag = GetComponent<DungeonSpawned>()) != null) ? tag.Dungeon : GetComponentInParent<DungeonInstance>();

        /// <summary>The Interact key: used by the nearest living player.</summary>
        public override void Interact()
        {
            CombatEntity player = DungeonPlayers.NearestAlive(transform.position, useDistance + 2f);
            if (player != null)
                TryUse(player);
        }

        protected virtual void Update()
        {
            if (holdToUse <= 0f || UsedUp || Time.time < readyAt)
                return;
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
            if (UsedUp || Time.time < readyAt || player == null)
                return false;
            if (!Use(player))
                return false;
            TimesUsed++;
            readyAt = Time.time + cooldown;
            if (UsedUp)
                OnUsedUp();
            return true;
        }

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
