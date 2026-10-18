using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Something that breaks: urns, barrels, crates, wine barrels. A weapon hit (it is an <see cref="IWeaponHittable"/>) or
    /// a kick (using it) smashes it into a few pieces and drops items from its <see cref="dropTable"/> (else the Item Drop
    /// Manager's Breakable table). In a room with a barrel stash (a wine cellar's hidden loot) every broken barrel may be
    /// the one hiding it - the last one always is - and the stash appears where the barrel stood.
    /// </summary>
    public class DungeonBreakable : DungeonInteractable, IWeaponHittable
    {
        [Tooltip("What it drops when it breaks: items with chances, quantities and tiers. Empty = the dungeon loot / prop " +
                 "entry's table, else the Item Drop Manager's Breakable table (none = nothing).")]
        public ItemDropTable dropTable;
        [Tooltip("Tiers added to its own tier for the roll.")]
        public int tierBonus;
        [Tooltip("Use the Item Drop Manager's Breakable table when it has no table of its own.")]
        public bool useDefaultTable = true;
        [Tooltip("Hits it takes to break.")]
        [Min(1)] public int hits = 1;
        [Tooltip("Seconds the pieces lie around.")]
        [Min(0f)] public float debrisTime = 6f;

        public bool Broken { get; private set; }

        private int taken;

        public bool OnWeaponHit(in WeaponHitInfo hit)
        {
            Hit();
            return true;
        }

        protected override bool Use(CombatEntity player)
        {
            Hit();
            return true;
        }

        public void Hit()
        {
            if (Broken || DungeonNet.IsReplica(this) || ++taken < hits)
                return;   // a client's copy breaks when the server says so
            Break();
        }

        private void Break()
        {
            ShowBroken();
            DungeonNet.Publish(this, 1);
            DropItems();
            RevealStash();
        }

        private void DropItems()
        {
            ItemDropTable table = dropTable != null ? dropTable : useDefaultTable ? ItemDropManager.Instance.breakableTable : null;
            if (table == null)
                return;
            var tag = GetComponent<DungeonSpawned>();
            int tier = (tag != null ? tag.tier : 0) + tierBonus;
            ItemDropManager.Drop(table, tier, transform.position, gameObject);
        }

        private void ShowBroken()
        {
            Broken = true;
            Shatter();
            foreach (Collider c in GetComponentsInChildren<Collider>())
                c.enabled = false;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
                r.enabled = false;
        }

        /// <summary>The published state: 1 once broken.</summary>
        protected override int NetState => Broken ? 1 : 0;

        /// <summary>A client's copy: the server broke it (it shatters here too; the stash is the server's business).</summary>
        public override void ApplyNetState(int state, byte[] payload)
        {
            if (state > 0 && !Broken)
                ShowBroken();
        }

        /// <summary>A few loose pieces in the object's colours that tumble and vanish.</summary>
        private void Shatter()
        {
            Renderer source = GetComponentInChildren<Renderer>();
            Bounds b = source != null ? source.bounds : new Bounds(transform.position + Vector3.up * 0.4f, Vector3.one * 0.6f);
            for (int i = 0; i < 6; i++)
            {
                GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = "Piece";
                piece.transform.position = b.center + new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.2f, 0.2f), Random.Range(-0.2f, 0.2f));
                piece.transform.rotation = Random.rotation;
                piece.transform.localScale = new Vector3(Random.Range(0.1f, 0.3f), Random.Range(0.05f, 0.12f), Random.Range(0.1f, 0.35f));
                if (source != null)
                    piece.GetComponent<Renderer>().sharedMaterial = source.sharedMaterial;
                var rb = piece.AddComponent<Rigidbody>();
                rb.mass = 0.3f;
                rb.AddForce(new Vector3(Random.Range(-1f, 1f), Random.Range(1f, 2.5f), Random.Range(-1f, 1f)), ForceMode.VelocityChange);
                Destroy(piece, debrisTime);
            }
        }

        private void RevealStash()
        {
            var tag = GetComponent<DungeonSpawned>();
            DungeonInstance d = Dungeon;
            if (tag == null || d == null || tag.area < 0)
                return;
            DungeonSpawned stash = null;
            int intact = 0;
            foreach (DungeonSpawned s in d.SpawnedIn(tag.floor, tag.area))
            {
                if (s.kind == PlacementKind.Loot && s.dormant && s.link == PlacementLinks.BarrelStash)
                    stash = s;
                var other = s != tag ? s.GetComponent<DungeonBreakable>() : null;
                if (other != null && !other.Broken)
                    intact++;
            }
            if (stash == null || Random.value > 1f / (intact + 1))
                return;
            stash.transform.position = transform.position;
            stash.Reveal();
            DungeonMessages.Show(this, "Something was hidden in the barrel!", true);
        }
    }
}
