using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A pressure plate of a puzzle room. A player stepping on it tells the room's <see cref="DungeonPuzzle"/>; the
    /// puzzle lights it (its Light children) to show the order and to mark it done. Its place in the order is
    /// DungeonSpawned.link.
    /// </summary>
    public class DungeonPressurePlate : MonoBehaviour
    {
        [Tooltip("How close (meters, horizontal) a player must stand to press it.")]
        [Min(0.2f)] public float radius = 0.75f;
        [Tooltip("How far the plate sinks when pressed (meters).")]
        public float sink = 0.05f;

        /// <summary>Its place in the order (from DungeonSpawned.link).</summary>
        public int Order => (tag != null || (tag = GetComponent<DungeonSpawned>()) != null) ? tag.link : 0;

        public bool IsPressed { get; private set; }
        public bool IsLit { get; private set; }

        internal DungeonPuzzle Puzzle;

        private DungeonSpawned tag;
        private Light[] lights;
        private Vector3 rest;

        private void Awake()
        {
            tag = GetComponent<DungeonSpawned>();
            lights = GetComponentsInChildren<Light>(true);
            rest = transform.localPosition;
            SetLit(false);
        }

        public void SetLit(bool lit)
        {
            IsLit = lit;
            if (lights != null)
                foreach (Light l in lights)
                    if (l != null)
                        l.enabled = lit;
        }

        private void Update()
        {
            CombatEntity player = DungeonPlayers.NearestAlive(transform.position, radius);
            bool pressed = player != null;
            if (pressed && !IsPressed && Puzzle != null)
                Puzzle.Pressed(this, player);
            IsPressed = pressed;
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, rest + Vector3.down * (pressed ? sink : 0f), Time.deltaTime);
        }
    }
}
