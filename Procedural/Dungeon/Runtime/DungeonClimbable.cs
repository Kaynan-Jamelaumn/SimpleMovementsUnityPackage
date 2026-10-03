using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Vines or a giant root you can climb (a climb shaft between two floors). Inside its trigger volume a player grabs
    /// on: Jump or forward climbs up, Crouch or back climbs down, nothing holds on (no gravity while holding). At the
    /// top they are helped over the edge; leaving the volume lets go.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class DungeonClimbable : MonoBehaviour
    {
        [Tooltip("Climbing speed (meters per second).")]
        [Min(0.2f)] public float speed = 2.6f;
        [Tooltip("Push over the edge when reaching the top (meters per second).")]
        [Min(0f)] public float mantle = 3.5f;

        private sealed class Climber
        {
            public PlayerInput input;
            public bool holding;
        }

        private readonly Dictionary<CombatEntity, Climber> climbers = new Dictionary<CombatEntity, Climber>();
        private readonly List<CombatEntity> gone = new List<CombatEntity>();
        private BoxCollider box;

        private void Awake()
        {
            box = GetComponent<BoxCollider>();
            box.isTrigger = true;
        }

        private void Update()
        {
            Bounds b = box.bounds;
            foreach (CombatEntity p in CombatEntity.Players)
            {
                if (p == null || !p.IsAlive || p.Controller == null || p.PlayerMovement == null)
                    continue;
                Vector3 pos = p.transform.position + Vector3.up * 0.5f;
                bool inside = b.Contains(pos);
                climbers.TryGetValue(p, out Climber c);
                if (!inside)
                {
                    if (c != null && c.holding)
                        LetGo(p, c, pos.y >= b.max.y - 0.6f);
                    continue;
                }
                if (c == null)
                    climbers[p] = c = new Climber { input = SharedPlayerInput.Acquire(p) };
                Climb(p, c, b);
            }
            // Players that were removed or died.
            gone.Clear();
            foreach (var kv in climbers)
                if (kv.Key == null || !kv.Key.IsAlive)
                    gone.Add(kv.Key);
            foreach (CombatEntity p in gone)
                Forget(p);
        }

        private void Climb(CombatEntity p, Climber c, Bounds b)
        {
            Vector2 move = c.input != null ? c.input.Player.Movement.ReadValue<Vector2>() : Vector2.zero;
            bool up = c.input != null && (c.input.Player.Jump.IsPressed() || move.y > 0.5f);
            bool down = c.input != null && (c.input.Player.Crouch.IsPressed() || move.y < -0.5f);
            bool grounded = p.Controller.isGrounded;
            if (!c.holding && !up && !(down && !grounded))
                return;   // standing at the foot of the vines: walk away freely
            c.holding = true;
            p.PlayerMovement.SuspendGravity = true;
            p.PlayerMovement.VerticalVelocity = 0f;
            float dir = up ? 1f : down ? -1f : 0f;
            // Keep against the middle of the shaft while climbing.
            Vector3 toCentre = b.center - p.transform.position;
            toCentre.y = 0f;
            Vector3 step = Vector3.up * dir * speed + Vector3.ClampMagnitude(toCentre, 1f) * 0.5f;
            p.Controller.Move(step * Time.deltaTime);
            if (down && grounded)
                LetGo(p, c, false);
            else if (p.transform.position.y + 0.5f >= b.max.y - 0.3f && up)
                LetGo(p, c, true);
        }

        private void LetGo(CombatEntity p, Climber c, bool top)
        {
            c.holding = false;
            if (p.PlayerMovement == null)
                return;
            p.PlayerMovement.SuspendGravity = false;
            if (top)
            {
                // Over the edge: a hop forward.
                p.PlayerMovement.VerticalVelocity = mantle;
                p.Controller.Move(p.transform.forward * 0.4f);
            }
        }

        private void Forget(CombatEntity p)
        {
            if (!climbers.TryGetValue(p, out Climber c))
                return;
            if (c.holding && p != null && p.PlayerMovement != null)
                p.PlayerMovement.SuspendGravity = false;
            SharedPlayerInput.Release(c.input);
            climbers.Remove(p);
        }

        private void OnDisable()
        {
            foreach (var kv in climbers)
            {
                if (kv.Value.holding && kv.Key != null && kv.Key.PlayerMovement != null)
                    kv.Key.PlayerMovement.SuspendGravity = false;
                SharedPlayerInput.Release(kv.Value.input);
            }
            climbers.Clear();
        }
    }
}
