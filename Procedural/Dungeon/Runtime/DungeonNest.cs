using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A nest (spawner room): while players are near it keeps hatching mobs - a few alive at a time, one every few
    /// seconds - until it is destroyed. Weapons damage it (it is an <see cref="IWeaponHittable"/>), and standing next to
    /// it and using it (Interact, or holding still beside it) stomps on it - set Uses to 0 so it can be stomped again
    /// and again. Destroying it doesn't kill what it hatched.
    /// </summary>
    public class DungeonNest : DungeonInteractable, IWeaponHittable
    {
        /// <summary>The mob it hatches (set by the builder: the cheapest encounter allowed in the room).</summary>
        [NonSerialized] public EncounterInfo encounter;
        [Tooltip("Hit points (weapon damage).")]
        [Min(1f)] public float health = 120f;
        [Tooltip("Share of its health one stomp (using it) takes.")]
        [Range(0.05f, 1f)] public float stomp = 0.25f;
        [Tooltip("Seconds between hatchings.")]
        [Min(0.5f)] public float spawnInterval = 7f;
        [Tooltip("Mobs it keeps alive at once.")]
        [Min(1)] public int maxAlive = 3;
        [Tooltip("Total mobs it can hatch (0 = until destroyed).")]
        [Min(0)] public int maxSpawns;
        [Tooltip("It only hatches while a player is this close (meters).")]
        [Min(1f)] public float activationRadius = 16f;

        public float Health { get; private set; }
        public bool Destroyed { get; private set; }

        /// <summary>Any nest destroyed.</summary>
        public static event Action<DungeonNest> AnyDestroyed;

        private readonly List<DungeonSpawned> hatched = new List<DungeonSpawned>();
        private float nextSpawn;
        private int spawned;
        private DungeonSpawned self;

        private void Awake() => Health = health;

        private void Start()
        {
            self = GetComponent<DungeonSpawned>();
            nextSpawn = Time.time + spawnInterval * 0.5f;
        }

        protected override void Update()
        {
            base.Update();
            if (Destroyed || Time.time < nextSpawn)
                return;
            nextSpawn = Time.time + spawnInterval;
            hatched.RemoveAll(h => h == null || (!h.IsAliveMob && h.GetComponentInChildren<CombatEntity>(true) != null));
            if (hatched.Count >= maxAlive || (maxSpawns > 0 && spawned >= maxSpawns) || !PlayerNear())
                return;
            Hatch();
        }

        private bool PlayerNear()
        {
            foreach (CombatEntity p in CombatEntity.Players)
                if (p != null && p.IsAlive && (p.transform.position - transform.position).sqrMagnitude <= activationRadius * activationRadius &&
                    Mathf.Abs(p.transform.position.y - transform.position.y) < 4f)
                    return true;
            return false;
        }

        private void Hatch()
        {
            DungeonInstance d = Dungeon;
            if (d == null || encounter == null)
                return;
            float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            Vector3 at = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.4f;
            GameObject go = d.SpawnMob(encounter, self != null ? self.floor : 0, self != null ? self.area : -1, self != null ? self.tier : 0, at, a * Mathf.Rad2Deg);
            if (go == null)
                return;
            spawned++;
            var tag = go.GetComponent<DungeonSpawned>();
            if (tag != null)
                hatched.Add(tag);
            var mob = go.GetComponent<Mob>();
            CombatEntity target = DungeonPlayers.NearestAlive(transform.position, activationRadius);
            if (mob != null && target != null)
                mob.OnSummoned(null, target, 0f, false);
        }

        public bool OnWeaponHit(in WeaponHitInfo hit)
        {
            if (Destroyed)
                return false;
            Damage(Mathf.Max(1f, hit.damage));
            return true;
        }

        protected override bool Use(CombatEntity player)
        {
            if (Destroyed)
                return false;
            Damage(health * stomp);
            return true;
        }

        public void Damage(float amount)
        {
            if (Destroyed)
                return;
            Health -= amount;
            transform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(Health / health));
            if (Health <= 0f)
                Break();
        }

        private void Break()
        {
            Destroyed = true;
            uses = 1;
            foreach (Collider c in GetComponentsInChildren<Collider>())
                c.enabled = false;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
                r.enabled = false;
            foreach (Light l in GetComponentsInChildren<Light>())
                l.enabled = false;
            AnyDestroyed?.Invoke(this);
            DungeonInstance d = Dungeon;
            bool last = true;
            if (d != null && self != null)
                foreach (DungeonSpawned s in d.SpawnedIn(self.floor, self.area))
                {
                    var other = s != self ? s.GetComponent<DungeonNest>() : null;
                    if (other != null && !other.Destroyed)
                        last = false;
                }
            DungeonMessages.Show(last ? "The last nest is destroyed!" : "The nest is destroyed.", last);
        }
    }
}
