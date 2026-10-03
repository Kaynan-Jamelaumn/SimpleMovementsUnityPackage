using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A gate at a way into an event room (boss, arena, guardian, throne, ambush, crypt). It starts open; the room's
    /// <see cref="DungeonRoomEvent"/> closes it while the fight lasts and opens it when the room is cleared.
    /// </summary>
    public class DungeonGate : DungeonBarrier
    {
        [Tooltip("Start closed (the builder's gates start open).")]
        public bool startClosed;

        private void Start() => Initialize(startClosed);
    }
}
