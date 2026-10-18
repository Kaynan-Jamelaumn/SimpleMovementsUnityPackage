using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProceduralDungeon
{
    /// <summary>
    /// A loot chest: opening it lifts its lid (a child named "Lid"), drops items from its <see cref="dropTable"/> (else the
    /// Item Drop Manager's Chest table) at its tier (DungeonSpawned.tier) and its Drops prefabs in front of it, and raises
    /// <see cref="Opened"/> / <see cref="AnyOpened"/> with its tier for your own loot code.
    /// </summary>
    public class DungeonChest : DungeonInteractable
    {
        [Tooltip("What it drops: items with chances, quantities and tiers (Create ▸ SimpleMovements ▸ Items ▸ Item Drop Table). " +
                 "Empty = the dungeon loot entry's table, else the Item Drop Manager's Chest table.")]
        public ItemDropTable dropTable;
        [Tooltip("Tiers added to the chest's own tier for the roll (more rolls, better entries).")]
        public int tierBonus;
        [Tooltip("Prefabs dropped in front of the chest when it opens (pickups made by hand), every time. Optional.")]
        public GameObject[] drops = new GameObject[0];
        [Tooltip("Degrees the lid opens.")]
        public float lidAngle = 70f;

        public bool IsOpen { get; private set; }

        public event Action<DungeonChest, int> Opened;
        /// <summary>Any chest opened: (chest, tier).</summary>
        public static event Action<DungeonChest, int> AnyOpened;

        private Transform lid;

        private void Start()
        {
            holdToUse = holdToUse > 0f ? Mathf.Min(holdToUse, 0.8f) : 0f;
            lid = transform.Find("Lid");
        }

        protected override void Update()
        {
            base.Update();
            if (IsOpen && lid != null)
                lid.localRotation = Quaternion.RotateTowards(lid.localRotation, Quaternion.Euler(-lidAngle, 0f, 0f), 120f * Time.deltaTime);
        }

        protected override bool Use(CombatEntity player)
        {
            if (IsOpen)
                return false;
            IsOpen = true;
            var tag = GetComponent<DungeonSpawned>();
            int tier = tag != null ? tag.tier : 0;
            ItemDropTable table = dropTable != null ? dropTable : ItemDropManager.Instance.chestTable;
            if (table != null)
                ItemDropManager.Drop(table, tier + tierBonus, transform.position + transform.forward * 1f, gameObject);
            for (int i = 0; drops != null && i < drops.Length; i++)
            {
                if (drops[i] == null)
                    continue;
                Vector3 at = transform.position + transform.forward * 1f + Vector3.up * 0.4f + transform.right * (i - (drops.Length - 1) * 0.5f) * 0.4f;
                // Offline a plain Instantiate; on a server a networked world item in the chest's scene (see WorldItems).
                GameObject drop = WorldItems.InstantiatePickable(drops[i], at, Quaternion.identity, out bool carrier);
                if (drop == null)
                    continue;
                if (carrier)
                    WorldItems.Prepare(drop);
                WorldItems.Publish(drop, gameObject);
            }
            DungeonInstance d = Dungeon;
            if (d != null)
                d.Stats.Bump(ref d.Stats.chestsOpened);
            Opened?.Invoke(this, tier);
            AnyOpened?.Invoke(this, tier);
            return true;
        }

        protected override void OnUsedUp()
        {
        }

        /// <summary>A client's copy: the server opened it (the lid lifts; its loot arrives as networked world items).</summary>
        protected override void OnNetState(int state, byte[] payload)
        {
            if (state > 0)
                IsOpen = true;
        }
    }
}
