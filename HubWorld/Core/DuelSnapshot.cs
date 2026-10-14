using System;
using UnityEngine;

namespace HubWorld
{
    /// <summary>
    /// One open duel as the server sees it, for the copies of the duel book on network clients
    /// (<see cref="DuelBook.ApplySnapshot"/>, <see cref="DuelSystem.ApplyReplicated"/>). Players are named by their
    /// <see cref="PlayerIds"/> (the same on every machine); <see cref="deadline"/> is in the receiving machine's time.
    /// </summary>
    [Serializable]
    public struct DuelSnapshot : IEquatable<DuelSnapshot>
    {
        public int id;
        public int challenger;
        public int challenged;
        public DuelState state;
        /// <summary>When the challenge expires, the fight starts or times out (see <see cref="Duel.Deadline"/>).</summary>
        public double deadline;
        /// <summary>The winner's id, or -1.</summary>
        public int winner;
        public DuelEndReason reason;
        /// <summary>Does the duel have an arena ring (accepted, with Arena Radius and Show Arena on)?</summary>
        public bool arena;
        /// <summary>The arena's centre.</summary>
        public Vector3 center;

        /// <summary>The snapshot of <paramref name="d"/> (arena values left empty).</summary>
        public static DuelSnapshot Of(Duel d) => d == null
            ? default
            : new DuelSnapshot
            {
                id = d.Id,
                challenger = d.Challenger,
                challenged = d.Challenged,
                state = d.State,
                deadline = d.Deadline,
                winner = d.Winner,
                reason = d.Reason,
            };

        public bool Equals(DuelSnapshot o) =>
            id == o.id && challenger == o.challenger && challenged == o.challenged && state == o.state &&
            deadline.Equals(o.deadline) && winner == o.winner && reason == o.reason && arena == o.arena && center == o.center;

        public override bool Equals(object obj) => obj is DuelSnapshot o && Equals(o);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = id;
                h = h * 397 ^ challenger;
                h = h * 397 ^ challenged;
                h = h * 397 ^ (int)state;
                h = h * 397 ^ deadline.GetHashCode();
                return h;
            }
        }

        public override string ToString() => $"Duel {id} ({challenger} vs {challenged}) {state}";
    }
}
