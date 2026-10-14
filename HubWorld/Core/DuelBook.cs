using System;
using System.Collections.Generic;

namespace HubWorld
{
    public enum DuelState
    {
        /// <summary>Waiting for the challenged player to accept or decline.</summary>
        Challenged,
        /// <summary>Accepted: a short countdown before the fight.</summary>
        Countdown,
        /// <summary>Combat is on between the two (and only them).</summary>
        Fighting,
        /// <summary>Over (see <see cref="Duel.Reason"/> and <see cref="Duel.Winner"/>).</summary>
        Ended,
    }

    public enum DuelEndReason
    {
        None,
        /// <summary>One fighter was beaten (would have died).</summary>
        Defeat,
        /// <summary>One fighter gave up.</summary>
        Forfeit,
        /// <summary>One fighter left the arena (or the city).</summary>
        LeftArena,
        /// <summary>The fight lasted too long: a draw.</summary>
        TimeOut,
        /// <summary>The challenged player said no.</summary>
        Declined,
        /// <summary>The challenge was not answered in time.</summary>
        Expired,
        /// <summary>Called off (a player left, disconnected, travelled...).</summary>
        Cancelled,
    }

    /// <summary>One duel between two players, identified by the caller's ids.</summary>
    public sealed class Duel
    {
        internal Duel(int id, int challenger, int challenged, double now)
        {
            Id = id;
            Challenger = challenger;
            Challenged = challenged;
            CreatedAt = now;
        }

        public int Id { get; }
        public int Challenger { get; }
        public int Challenged { get; }
        public DuelState State { get; internal set; } = DuelState.Challenged;
        public double CreatedAt { get; }
        /// <summary>When the challenge expires (Challenged), the fight starts (Countdown) or times out (Fighting).</summary>
        public double Deadline { get; internal set; }
        /// <summary>The winner's id, or -1 (draws, declines, cancellations).</summary>
        public int Winner { get; internal set; } = -1;
        public DuelEndReason Reason { get; internal set; }

        public bool Involves(int player) => player == Challenger || player == Challenged;
        public int OpponentOf(int player) => player == Challenger ? Challenged : player == Challenged ? Challenger : -1;
        /// <summary>Accepted and not over (countdown or fight).</summary>
        public bool IsActive => State == DuelState.Countdown || State == DuelState.Fighting;

        public override string ToString() => $"Duel {Id} ({Challenger} vs {Challenged}) {State}{(State == DuelState.Ended ? " " + Reason : "")}";
    }

    /// <summary>
    /// The rules of duels, as plain state: challenge → accept (or decline / expire) → countdown → fight → end. A player
    /// is in at most one duel or challenge at a time. Time is passed in by the caller. The scene side (who is who,
    /// the arena, saving the loser) lives in the hub world's DuelSystem.
    /// </summary>
    public sealed class DuelBook
    {
        private readonly List<Duel> duels = new List<Duel>();
        private int nextId = 1;

        /// <summary>Seconds a challenge waits for an answer.</summary>
        public double ChallengeTimeout = 30d;
        /// <summary>Seconds between accepting and fighting.</summary>
        public double Countdown = 3d;
        /// <summary>Longest fight (seconds) before it ends in a draw. 0 = no limit.</summary>
        public double MaxDuration = 180d;

        /// <summary>Duels still going (challenges, countdowns, fights).</summary>
        public IReadOnlyList<Duel> Open => duels;

        /// <summary>Raised when a duel changes state (also when it ends; it is removed afterwards).</summary>
        public event Action<Duel> Changed;

        /// <summary>The open duel or challenge involving <paramref name="player"/>, or null.</summary>
        public Duel Of(int player)
        {
            for (int i = 0; i < duels.Count; i++)
                if (duels[i].Involves(player))
                    return duels[i];
            return null;
        }

        /// <summary>Are the two fighting each other right now (combat on)?</summary>
        public bool AreFighting(int a, int b)
        {
            Duel d = Of(a);
            return d != null && d.State == DuelState.Fighting && d.OpponentOf(a) == b;
        }

        /// <summary>
        /// <paramref name="challenger"/> challenges <paramref name="challenged"/>. Null with a reason when they cannot
        /// (the same player, or one of them is already in a duel or challenge).
        /// </summary>
        public Duel Challenge(int challenger, int challenged, double now, out string error)
        {
            error = null;
            if (challenger == challenged)
            {
                error = "You can't duel yourself.";
                return null;
            }
            if (Of(challenger) != null)
            {
                error = "You are already in a duel or waiting for an answer.";
                return null;
            }
            if (Of(challenged) != null)
            {
                error = "They are already in a duel or deciding on one.";
                return null;
            }
            var d = new Duel(nextId++, challenger, challenged, now) { Deadline = now + Math.Max(1d, ChallengeTimeout) };
            duels.Add(d);
            Changed?.Invoke(d);
            return d;
        }

        /// <summary><paramref name="player"/> accepts the challenge addressed to them. False if there is none.</summary>
        public bool Accept(int player, double now)
        {
            Duel d = Of(player);
            if (d == null || d.State != DuelState.Challenged || d.Challenged != player)
                return false;
            d.State = DuelState.Countdown;
            d.Deadline = now + Math.Max(0d, Countdown);
            Changed?.Invoke(d);
            if (Countdown <= 0d)
                StartFight(d, now);
            return true;
        }

        /// <summary><paramref name="player"/> declines the challenge addressed to them (or withdraws their own).</summary>
        public bool Decline(int player)
        {
            Duel d = Of(player);
            if (d == null || d.State != DuelState.Challenged)
                return false;
            Finish(d, -1, d.Challenged == player ? DuelEndReason.Declined : DuelEndReason.Cancelled);
            return true;
        }

        /// <summary>
        /// Ends <paramref name="player"/>'s duel: <paramref name="lost"/> = they lost (defeat, forfeit, left the arena) and
        /// their opponent wins; otherwise it is called off with no winner.
        /// </summary>
        public bool End(int player, DuelEndReason reason, bool lost)
        {
            Duel d = Of(player);
            if (d == null)
                return false;
            if (d.State == DuelState.Challenged)
            {
                Finish(d, -1, reason == DuelEndReason.Declined ? DuelEndReason.Declined : DuelEndReason.Cancelled);
                return true;
            }
            Finish(d, lost ? d.OpponentOf(player) : -1, reason);
            return true;
        }

        /// <summary>Expires challenges, starts fights after the countdown and ends fights that last too long.</summary>
        public void Update(double now)
        {
            for (int i = duels.Count - 1; i >= 0 && i < duels.Count; i--)
            {
                Duel d = duels[i];
                switch (d.State)
                {
                    case DuelState.Challenged:
                        if (now >= d.Deadline)
                            Finish(d, -1, DuelEndReason.Expired);
                        break;
                    case DuelState.Countdown:
                        if (now >= d.Deadline)
                            StartFight(d, now);
                        break;
                    case DuelState.Fighting:
                        if (MaxDuration > 0d && now >= d.Deadline)
                            Finish(d, -1, DuelEndReason.TimeOut);
                        break;
                }
            }
        }

        private void StartFight(Duel d, double now)
        {
            d.State = DuelState.Fighting;
            d.Deadline = MaxDuration > 0d ? now + MaxDuration : double.MaxValue;
            Changed?.Invoke(d);
        }

        private void Finish(Duel d, int winner, DuelEndReason reason)
        {
            if (d.State == DuelState.Ended)
                return;
            d.State = DuelState.Ended;
            d.Winner = winner;
            d.Reason = reason;
            duels.Remove(d);
            Changed?.Invoke(d);
        }

        /// <summary>Calls off everything (no events).</summary>
        public void Clear() => duels.Clear();

        /// <summary>
        /// Makes this book a copy of another one (a network client mirroring the server's duels): duels in
        /// <paramref name="open"/> are added or updated, duels missing from it end (reason <see cref="DuelEndReason.None"/>
        /// when unknown). <see cref="Changed"/> is raised for every duel whose state changed, like on the server. A copied
        /// book is never advanced with <see cref="Update"/> - the server decides every change.
        /// </summary>
        /// <param name="open">The server's open duels (invalid entries - an id of 0 or less, or Ended - are ignored).</param>
        /// <param name="now">This machine's time, stored as the creation time of duels seen for the first time.</param>
        public void ApplySnapshot(IReadOnlyList<DuelSnapshot> open, double now)
        {
            var changed = new List<Duel>();
            for (int i = duels.Count - 1; i >= 0; i--)
            {
                Duel d = duels[i];
                if (IndexIn(open, d.Id) >= 0)
                    continue;
                duels.RemoveAt(i);
                d.State = DuelState.Ended;
                changed.Add(d);
            }
            if (open != null)
            {
                for (int i = 0; i < open.Count; i++)
                {
                    DuelSnapshot v = open[i];
                    if (v.id <= 0 || v.state == DuelState.Ended || IndexIn(open, v.id) != i)
                        continue;
                    Duel d = null;
                    for (int k = 0; k < duels.Count; k++)
                        if (duels[k].Id == v.id)
                            d = duels[k];
                    if (d == null || d.Challenger != v.challenger || d.Challenged != v.challenged)
                    {
                        if (d != null)
                            duels.Remove(d);
                        // A player is in one duel at most: anything else listed for them is gone on the server.
                        for (int k = duels.Count - 1; k >= 0; k--)
                            if (duels[k].Involves(v.challenger) || duels[k].Involves(v.challenged))
                            {
                                Duel stale = duels[k];
                                duels.RemoveAt(k);
                                stale.State = DuelState.Ended;
                                changed.Add(stale);
                            }
                        d = new Duel(v.id, v.challenger, v.challenged, now) { State = v.state, Deadline = v.deadline, Winner = v.winner, Reason = v.reason };
                        duels.Add(d);
                        changed.Add(d);
                        nextId = Math.Max(nextId, v.id + 1);
                        continue;
                    }
                    bool stateChanged = d.State != v.state;
                    d.State = v.state;
                    d.Deadline = v.deadline;
                    d.Winner = v.winner;
                    d.Reason = v.reason;
                    if (stateChanged)
                        changed.Add(d);
                }
            }
            for (int i = 0; i < changed.Count; i++)
                Changed?.Invoke(changed[i]);
        }

        private static int IndexIn(IReadOnlyList<DuelSnapshot> list, int id)
        {
            if (list == null)
                return -1;
            for (int i = 0; i < list.Count; i++)
                if (list[i].id == id)
                    return i;
            return -1;
        }
    }
}
