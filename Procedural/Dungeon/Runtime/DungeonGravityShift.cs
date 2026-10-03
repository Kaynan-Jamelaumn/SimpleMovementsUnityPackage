using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// An astral floor's flipping gravity (on the floor's root): every so often, after a warning, gravity turns upside
    /// down for a few seconds - players on the floor float up (no higher than Float Height above where they stood, so
    /// nobody is thrown into the void) and drift there, then it flips back and they drop. Loose rigidbodies rise too.
    /// </summary>
    public class DungeonGravityShift : MonoBehaviour
    {
        [Tooltip("Seconds between flips (random in this range).")]
        public Vector2 interval = new Vector2(35f, 70f);
        [Tooltip("How long gravity stays flipped (seconds).")]
        [Min(0.5f)] public float duration = 6f;
        [Tooltip("How high players float (meters above where they stood).")]
        [Min(0.5f)] public float floatHeight = 3f;
        [Tooltip("Seconds of warning before a flip.")]
        [Min(0f)] public float warning = 3f;

        public bool IsFlipped { get; private set; }

        private DungeonInstance dungeon;
        private int floor;
        private float nextFlip, flipEnds;
        private bool warned;
        private readonly Dictionary<CombatEntity, float> floating = new Dictionary<CombatEntity, float>();

        public void Setup(DungeonInstance owner, int floorIndex, Vector2 flipInterval, float flipDuration, float height)
        {
            dungeon = owner;
            floor = floorIndex;
            interval = flipInterval;
            duration = flipDuration;
            floatHeight = height;
        }

        private void Start() => nextFlip = Time.time + Random.Range(interval.x, Mathf.Max(interval.x, interval.y));

        private void Update()
        {
            if (dungeon == null)
                dungeon = GetComponentInParent<DungeonInstance>();
            if (dungeon == null)
                return;
            if (!IsFlipped)
            {
                bool here = AnyPlayerHere();
                if (!warned && here && Time.time >= nextFlip - warning)
                {
                    warned = true;
                    DungeonMessages.Show("The stars shift... gravity is about to turn!", true);
                }
                if (Time.time >= nextFlip)
                {
                    if (here)
                        Flip(true);
                    else
                        nextFlip = Time.time + Random.Range(interval.x, Mathf.Max(interval.x, interval.y));
                }
                return;
            }
            Float();
            if (Time.time >= flipEnds)
                Flip(false);
        }

        private bool AnyPlayerHere()
        {
            foreach (CombatEntity p in CombatEntity.Players)
                if (p != null && p.IsAlive && dungeon.FloorAt(p.transform.position) == floor)
                    return true;
            return false;
        }

        private void Flip(bool flipped)
        {
            IsFlipped = flipped;
            warned = false;
            if (flipped)
            {
                flipEnds = Time.time + duration;
                floating.Clear();
                foreach (CombatEntity p in CombatEntity.Players)
                    if (p != null && p.IsAlive && p.PlayerMovement != null && dungeon.FloorAt(p.transform.position) == floor)
                        floating[p] = p.transform.position.y + floatHeight;
                foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
                    if (!rb.isKinematic)
                        rb.AddForce(Vector3.up * 6f, ForceMode.VelocityChange);
                DungeonMessages.Show("Gravity flips!", true);
                return;
            }
            foreach (var kv in floating)
                if (kv.Key != null && kv.Key.PlayerMovement != null)
                    kv.Key.PlayerMovement.SuspendGravity = false;
            floating.Clear();
            nextFlip = Time.time + Random.Range(interval.x, Mathf.Max(interval.x, interval.y));
        }

        /// <summary>Players rise towards their float height and hover there, bobbing gently.</summary>
        private void Float()
        {
            foreach (var kv in floating)
            {
                CombatEntity p = kv.Key;
                if (p == null || !p.IsAlive || p.PlayerMovement == null || p.Controller == null || !p.Controller.enabled)
                    continue;
                p.PlayerMovement.SuspendGravity = true;
                p.PlayerMovement.VerticalVelocity = 0f;
                float target = kv.Value + Mathf.Sin(Time.time * 2f) * 0.2f;
                float dy = Mathf.Clamp(target - p.transform.position.y, -2f, 2.5f) * Time.deltaTime * 2f;
                p.Controller.Move(Vector3.up * dy);
            }
        }

        private void OnDisable()
        {
            if (IsFlipped)
                Flip(false);
        }
    }
}
