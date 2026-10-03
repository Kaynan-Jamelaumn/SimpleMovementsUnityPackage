using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A door on a loop that only opens from one side - its forward side, the side farther from the floor's way in.
    /// Players who come the long way round open it and get a quick way back; from the near side it stays shut.
    /// </summary>
    public class DungeonShortcutDoor : DungeonBarrier
    {
        [Tooltip("How close a player must come to open it (meters).")]
        [Min(0.5f)] public float reach = 2f;

        public bool IsOpen { get; private set; }

        private float nextHint;

        private void Start() => Initialize(true);

        protected override void Update()
        {
            base.Update();
            if (IsOpen)
                return;
            CombatEntity player = DungeonPlayers.NearestAlive(transform.position, reach);
            if (player == null)
                return;
            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            if (Vector3.Dot(toPlayer, transform.forward) > 0.1f)
            {
                IsOpen = true;
                SetClosed(false);
                DungeonInstance d = Dungeon;
                if (d != null)
                    d.Stats.Bump(ref d.Stats.shortcutsOpened);
                DungeonMessages.Show("A shortcut opens.");
            }
            else if (Time.time >= nextHint)
            {
                nextHint = Time.time + 8f;
                DungeonMessages.Show("It doesn't open from this side.");
            }
        }
    }
}
