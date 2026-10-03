using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Something that breaks: barrels, crates, wine barrels. A weapon hit (it is an <see cref="IWeaponHittable"/>) or a
    /// kick (using it) smashes it into a few pieces. In a room with a barrel stash (a wine cellar's hidden loot) every
    /// broken barrel may be the one hiding it - the last one always is - and the stash appears where the barrel stood.
    /// </summary>
    public class DungeonBreakable : DungeonInteractable, IWeaponHittable
    {
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
            if (Broken || ++taken < hits)
                return;
            Break();
        }

        private void Break()
        {
            Broken = true;
            Shatter();
            foreach (Collider c in GetComponentsInChildren<Collider>())
                c.enabled = false;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
                r.enabled = false;
            RevealStash();
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
            DungeonMessages.Show("Something was hidden in the barrel!", true);
        }
    }
}
