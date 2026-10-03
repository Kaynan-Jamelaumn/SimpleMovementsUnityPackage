using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProceduralDungeon
{
    /// <summary>
    /// A loot chest: opening it lifts its lid (a child named "Lid"), drops its Drops in front of it (item pickups, gold...)
    /// and raises <see cref="Opened"/> / <see cref="AnyOpened"/> with its tier (DungeonSpawned.tier) so your loot code can
    /// fill it.
    /// </summary>
    public class DungeonChest : DungeonInteractable
    {
        [Tooltip("Prefabs dropped in front of the chest when it opens (e.g. item pickups). Optional.")]
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
            for (int i = 0; drops != null && i < drops.Length; i++)
                if (drops[i] != null)
                    Instantiate(drops[i], transform.position + transform.forward * 1f + Vector3.up * 0.4f + transform.right * (i - (drops.Length - 1) * 0.5f) * 0.4f, Quaternion.identity);
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
    }
}
