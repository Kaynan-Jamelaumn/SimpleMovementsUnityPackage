using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Runs a room's fight: when a player walks in, the room's <see cref="DungeonGate"/>s close (boss, arena, guardian,
    /// throne, ambush and rising-crypt rooms); ambush mobs appear in waves, each after the previous one is dead; when
    /// every mob of the room is dead the gates open and the room's hidden reward appears. If every player leaves (or
    /// dies) during the fight the gates open again and the fight resumes when someone comes back - nobody is ever shut in
    /// or out for good. Placeholder mobs (no Combat Entity) don't count, so the room simply opens. A pit fight is an
    /// ambush with an audience: the waves come out at the arena's gates and its champion comes with the last one.
    /// </summary>
    public class DungeonRoomEvent : MonoBehaviour
    {
        public enum State { Waiting, Fighting, Cleared }

        [Tooltip("Lock Until Cleared, Ambush (mobs appear in waves) or Pit Fight (waves at the gates, a champion last).")]
        public RoomEventMode mode = RoomEventMode.LockUntilCleared;
        [Tooltip("Floor and area this event belongs to (set by the builder).")]
        public int floor;
        public int area;
        [Tooltip("Ambush waves.")]
        [Min(0)] public int waves;
        [Tooltip("Seconds without a living player in the room before the gates open again mid-fight.")]
        [Min(0.5f)] public float leaveGrace = 4f;
        [Tooltip("Show messages (\"The doors slam shut!\", \"Wave 2\"...).")]
        public bool messages = true;

        public State Current { get; private set; }
        public int Wave { get; private set; }

        public event Action<DungeonRoomEvent> Started;
        public event Action<DungeonRoomEvent> Completed;
        /// <summary>Any room event in any dungeon completed.</summary>
        public static event Action<DungeonRoomEvent> AnyCompleted;

        private DungeonInstance dungeon;
        private readonly List<DungeonSpawned> members = new List<DungeonSpawned>();
        private float nextPoll, empty, outside;
        private bool locked;

        public void Setup(DungeonInstance owner, int floorIndex, int areaId, RoomEventMode eventMode, int waveCount)
        {
            dungeon = owner;
            floor = floorIndex;
            area = areaId;
            mode = eventMode;
            waves = waveCount;
        }

        private void Update()
        {
            if (Current == State.Cleared || Time.time < nextPoll)
                return;
            nextPoll = Time.time + 0.2f;
            if (dungeon == null)
                dungeon = GetComponentInParent<DungeonInstance>();
            if (dungeon == null || !dungeon.IsFloorPopulated(floor))
                return;

            bool someoneInside = DungeonPlayers.CountInArea(dungeon, floor, area) > 0;
            if (Current == State.Waiting)
            {
                if (someoneInside)
                    Begin();
                return;
            }

            members.RemoveAll(m => m == null || !m.IsAliveMob);
            if (members.Count == 0 && !NextWave())
            {
                Finish();
                return;
            }
            // The room's mobs that are inside. Mobs that chased a player out (or were pulled through the doorway) before the
            // doors shut would otherwise leave the players locked in with nobody to fight: the doors open until they are back.
            bool enemiesInside = MembersInside() > 0;
            if (someoneInside && enemiesInside)
            {
                empty = 0f;
                outside = 0f;
                if (!locked)
                    Lock(true);
            }
            else if (someoneInside)
            {
                empty = 0f;
                if (locked && (outside += 0.2f) >= 1f)
                {
                    Lock(false);
                    if (messages)
                        DungeonMessages.Show(this, "The enemies left the room - the doors open.");
                }
            }
            else if ((empty += 0.2f) >= leaveGrace && locked)
            {
                Lock(false);   // nobody left inside: let them back in, the fight goes on when they return
            }
        }

        private void Begin()
        {
            Current = State.Fighting;
            Wave = 0;
            members.Clear();
            foreach (DungeonSpawned s in dungeon.SpawnedIn(floor, area))
                if (!s.dormant && s.IsAliveMob)
                    members.Add(s);
            if (mode == RoomEventMode.Ambush || mode == RoomEventMode.PitFight || waves > 0)
                NextWave();
            if (members.Count == 0 && !HasHiddenMobs())
            {
                Finish();
                return;
            }
            Lock(true);
            Started?.Invoke(this);
            if (messages)
                DungeonMessages.Show(this, mode == RoomEventMode.Ambush ? "Ambush! The doors slam shut!"
                    : mode == RoomEventMode.PitFight ? "The crowd roars! The gates slam shut - fight!" : "The doors slam shut!", true);
        }

        /// <summary>Reveals the next wave of hidden mobs. False when there is none left.</summary>
        private bool NextWave()
        {
            while (HasHiddenMobs())
            {
                Wave++;
                int revealed = 0;
                bool champion = false;
                foreach (DungeonSpawned s in dungeon.SpawnedIn(floor, area))
                {
                    if (!s.dormant || (s.kind != PlacementKind.Mob && s.kind != PlacementKind.Boss) || (s.wave > Wave && s.wave > 0))
                        continue;
                    s.Reveal();
                    if (s.IsAliveMob)
                    {
                        members.Add(s);
                        revealed++;
                        champion |= s.order == MobOrder.Champion;
                    }
                }
                if (revealed > 0)
                {
                    if (messages && champion)
                        DungeonMessages.Show(this, "The champion enters the arena!", true);
                    else if (messages && Wave > 1)
                        DungeonMessages.Show(this, $"Wave {Wave}!", true);
                    return true;
                }
            }
            return false;
        }

        /// <summary>How many living mobs of the fight are inside the room (or in its doorways).</summary>
        private int MembersInside()
        {
            if (dungeon.Layout == null)
                return members.Count;
            TileGrid g = dungeon.Layout.Floors[floor].Grid;
            int n = 0;
            foreach (DungeonSpawned m in members)
            {
                if (m == null) continue;
                CombatEntity e = m.GetComponentInChildren<CombatEntity>();
                Vector3 pos = e != null ? e.Position : m.transform.position;
                if (!dungeon.WorldToCell(pos, out int f, out Vector2Int cell) || f != floor)
                    continue;
                int i = g.Index(cell);
                if (g.Area[i] == area)
                {
                    n++;
                    continue;
                }
                // A doorway of the room counts as inside (a mob standing in it keeps the fight going).
                for (int d = 0; d < 4; d++)
                {
                    int nb = g.Neighbor(i, d);
                    if (nb >= 0 && g.Area[nb] == area) { n++; break; }
                }
            }
            return n;
        }

        private bool HasHiddenMobs()
        {
            foreach (DungeonSpawned s in dungeon.SpawnedIn(floor, area))
                if (s.dormant && (s.kind == PlacementKind.Mob || s.kind == PlacementKind.Boss))
                    return true;
            return false;
        }

        private void Finish()
        {
            bool fought = Current == State.Fighting && (locked || Wave > 0);
            Current = State.Cleared;
            Lock(false);
            // The room's reward appears.
            foreach (DungeonSpawned s in dungeon.SpawnedIn(floor, area))
                if (s.dormant && s.kind != PlacementKind.Mob && s.kind != PlacementKind.Boss)
                    s.Reveal();
            if (fought)
            {
                dungeon.Stats.Bump(ref dungeon.Stats.roomsCleared);
                if (messages)
                    DungeonMessages.Show(this, mode == RoomEventMode.PitFight ? "Victory! The crowd cheers and the gates open." : "The room is clear. The doors open.");
            }
            dungeon.RaiseRoomCompleted(floor, area);
            Completed?.Invoke(this);
            AnyCompleted?.Invoke(this);
        }

        private void Lock(bool closed)
        {
            locked = closed;
            foreach (DungeonSpawned s in dungeon.SpawnedIn(floor, area))
                if (s.kind == PlacementKind.Gate)
                {
                    var gate = s.GetComponent<DungeonGate>();
                    if (gate != null)
                        gate.SetClosed(closed);
                }
        }
    }
}
