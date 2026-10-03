using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A puzzle room: when a player comes in, its <see cref="DungeonPressurePlate"/>s light up one after the other.
    /// Stepping on them in that order solves it and reveals the room's hidden reward; a wrong plate gives a small shock,
    /// puts the lights out and shows the order again. With fewer than two plates it is solved at once.
    /// </summary>
    public class DungeonPuzzle : MonoBehaviour
    {
        public enum State { Waiting, Showing, Listening, Solved }

        [Tooltip("Floor and area of the puzzle room (set by the builder).")]
        public int floor;
        public int area;
        [Tooltip("Seconds each plate stays lit while the order is shown.")]
        [Min(0.1f)] public float showTime = 0.7f;
        [Tooltip("Damage of the shock on a wrong plate (0 = none).")]
        [Min(0f)] public float wrongDamage = 8f;

        public State Current { get; private set; }

        public event Action<DungeonPuzzle> Solved;
        public static event Action<DungeonPuzzle> AnySolved;

        private DungeonInstance dungeon;
        private readonly List<DungeonPressurePlate> plates = new List<DungeonPressurePlate>();
        private int next;
        private float nextPoll;

        public void Setup(DungeonInstance owner, int floorIndex, int areaId)
        {
            dungeon = owner;
            floor = floorIndex;
            area = areaId;
        }

        private void Update()
        {
            if (Current != State.Waiting || Time.time < nextPoll)
                return;
            nextPoll = Time.time + 0.25f;
            if (dungeon == null)
                dungeon = GetComponentInParent<DungeonInstance>();
            if (dungeon == null || DungeonPlayers.CountInArea(dungeon, floor, area) == 0)
                return;
            plates.Clear();
            foreach (DungeonSpawned s in dungeon.SpawnedIn(floor, area))
            {
                var plate = s.GetComponent<DungeonPressurePlate>();
                if (plate != null)
                {
                    plate.Puzzle = this;
                    plates.Add(plate);
                }
            }
            plates.Sort((a, b) => a.Order.CompareTo(b.Order));
            if (plates.Count < 2)
            {
                Solve();
                return;
            }
            DungeonMessages.Show("The floor plates glow in turn... remember the order.");
            StartCoroutine(ShowOrder());
        }

        private IEnumerator ShowOrder()
        {
            Current = State.Showing;
            foreach (DungeonPressurePlate p in plates)
                p.SetLit(false);
            yield return new WaitForSeconds(0.6f);
            foreach (DungeonPressurePlate p in plates)
            {
                p.SetLit(true);
                yield return new WaitForSeconds(showTime);
                p.SetLit(false);
                yield return new WaitForSeconds(0.15f);
            }
            next = 0;
            Current = State.Listening;
        }

        internal void Pressed(DungeonPressurePlate plate, CombatEntity who)
        {
            if (Current != State.Listening)
                return;
            if (plate == plates[next])
            {
                plate.SetLit(true);
                if (++next >= plates.Count)
                    Solve();
                return;
            }
            if (plate.IsLit)
                return;   // stepping back onto a plate already done
            if (wrongDamage > 0f && who != null)
                who.ApplyDamage(new DamageInfo { amount = wrongDamage, target = who, point = plate.transform.position, direction = Vector3.up, type = DamageType.True, element = ElementType.Lightning });
            DungeonMessages.Show("Wrong plate! The order shows again.");
            StartCoroutine(ShowOrder());
        }

        private void Solve()
        {
            Current = State.Solved;
            foreach (DungeonPressurePlate p in plates)
                p.SetLit(true);
            if (dungeon != null)
            {
                foreach (DungeonSpawned s in dungeon.SpawnedIn(floor, area))
                    if (s.dormant)
                        s.Reveal();
                dungeon.Stats.Bump(ref dungeon.Stats.puzzlesSolved);
                dungeon.RaiseRoomCompleted(floor, area);
            }
            if (plates.Count >= 2)
                DungeonMessages.Show("The puzzle is solved - something appears!", true);
            Solved?.Invoke(this);
            AnySolved?.Invoke(this);
        }
    }
}
