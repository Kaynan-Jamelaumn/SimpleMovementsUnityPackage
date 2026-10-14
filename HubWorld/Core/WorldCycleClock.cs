using System;
using System.Collections.Generic;

namespace HubWorld
{
    public enum CycleEventKind
    {
        /// <summary>The jump is <see cref="CycleEvent.SecondsLeft"/> seconds away.</summary>
        Warning,
        /// <summary>The cycle ended: the city jumps and the world is regenerated.</summary>
        Ended,
    }

    /// <summary>Something the clock reports while advancing.</summary>
    public struct CycleEvent
    {
        public CycleEventKind Kind;
        /// <summary>The cycle the event belongs to (for Ended: the cycle that just ended).</summary>
        public int CycleIndex;
        /// <summary>Warning: seconds left before the jump when it was due.</summary>
        public float SecondsLeft;

        public override string ToString() => Kind == CycleEventKind.Warning ? $"Warning {SecondsLeft:0}s (cycle {CycleIndex})" : $"Ended (cycle {CycleIndex})";
    }

    /// <summary>
    /// The timing of the dimensional cycles, as plain data: which cycle it is, when it ends, which warnings are due.
    /// It does not read any clock - the caller passes the time (scaled game time, server time...), so the same rules run
    /// on a server and in tests. Advance it every frame and act on the events it reports.
    /// </summary>
    public sealed class WorldCycleClock
    {
        private readonly List<float> warnings = new List<float>();
        private int nextWarning;

        /// <param name="durationSeconds">Length of one cycle (at least 1 s).</param>
        /// <param name="warningSeconds">Seconds before the end at which to warn (any order; values not shorter than the cycle are ignored).</param>
        /// <param name="startTime">Time the first cycle starts.</param>
        /// <param name="startIndex">Index of the first cycle.</param>
        public WorldCycleClock(double durationSeconds, IEnumerable<float> warningSeconds, double startTime = 0d, int startIndex = 0)
        {
            Duration = Math.Max(1d, durationSeconds);
            CycleStart = startTime;
            CycleIndex = startIndex;
            SetWarnings(warningSeconds);
        }

        /// <summary>Length of a cycle (seconds).</summary>
        public double Duration { get; private set; }
        /// <summary>Index of the current cycle (0, 1, 2...).</summary>
        public int CycleIndex { get; private set; }
        public double CycleStart { get; private set; }
        public double CycleEnd => CycleStart + Duration;
        /// <summary>The warning times (seconds before the end), longest first.</summary>
        public IReadOnlyList<float> Warnings => warnings;

        public double Remaining(double now) => Math.Max(0d, CycleEnd - now);

        /// <summary>0 at the start of the cycle, 1 at the jump.</summary>
        public float Progress(double now) => (float)Math.Min(1d, Math.Max(0d, (now - CycleStart) / Duration));

        /// <summary>The longest warning that is already due (seconds before the end), or -1.</summary>
        public float CurrentWarning(double now)
        {
            double left = Remaining(now);
            float best = -1f;
            for (int i = 0; i < warnings.Count; i++)
                if (warnings[i] < Duration && left <= warnings[i] && (best < 0f || warnings[i] < best))
                    best = warnings[i];
            return best;
        }

        /// <summary>Replaces the warning times (only warnings still ahead in this cycle will be raised).</summary>
        public void SetWarnings(IEnumerable<float> warningSeconds)
        {
            warnings.Clear();
            if (warningSeconds != null)
                foreach (float w in warningSeconds)
                    if (w > 0f && !warnings.Contains(w))
                        warnings.Add(w);
            warnings.Sort((a, b) => b.CompareTo(a));
            nextWarning = 0;
        }

        /// <summary>Changes the cycle length; the current cycle keeps its start (it may end sooner or later).</summary>
        public void SetDuration(double durationSeconds) => Duration = Math.Max(1d, durationSeconds);

        /// <summary>
        /// Moves the current cycle so <paramref name="secondsLeft"/> remain at <paramref name="now"/> (debug tools,
        /// restoring a saved cycle). Warnings longer than that are considered given.
        /// </summary>
        public void SetRemaining(double secondsLeft, double now)
        {
            secondsLeft = Math.Max(0d, Math.Min(Duration, secondsLeft));
            CycleStart = now + secondsLeft - Duration;
            nextWarning = 0;
            while (nextWarning < warnings.Count && warnings[nextWarning] > secondsLeft)
                nextWarning++;
        }

        /// <summary>Sets the cycle index and start (restoring saved state); warnings already passed are skipped.</summary>
        public void Restore(int cycleIndex, double cycleStart, double now)
        {
            CycleIndex = cycleIndex;
            CycleStart = cycleStart;
            SetRemaining(Remaining(now), now);
        }

        /// <summary>
        /// Advances to <paramref name="now"/>, adding to <paramref name="events"/> (not cleared) the warnings that became
        /// due and the end of the cycle. After an end the next cycle starts right where the last one ended, so the cadence
        /// never drifts; after a gap longer than a whole cycle (a suspended game) the next one starts at
        /// <paramref name="now"/> instead of replaying every missed jump.
        /// </summary>
        public void Advance(double now, List<CycleEvent> events)
        {
            if (events == null)
                throw new ArgumentNullException(nameof(events));
            while (true)
            {
                bool ended = now >= CycleEnd;
                while (nextWarning < warnings.Count && now >= CycleEnd - warnings[nextWarning])
                {
                    float w = warnings[nextWarning++];
                    // Warnings longer than the cycle, and warnings overtaken by the end itself, are not raised.
                    if (w < Duration && !ended)
                        events.Add(new CycleEvent { Kind = CycleEventKind.Warning, CycleIndex = CycleIndex, SecondsLeft = w });
                }
                if (!ended)
                    return;
                events.Add(new CycleEvent { Kind = CycleEventKind.Ended, CycleIndex = CycleIndex });
                double end = CycleEnd;
                CycleIndex++;
                CycleStart = now >= end + Duration ? now : end;
                nextWarning = 0;
            }
        }

        /// <summary>Ends the current cycle at <paramref name="now"/> (the next <see cref="Advance"/> reports the end).</summary>
        public void EndNow(double now)
        {
            CycleStart = now - Duration;
            nextWarning = warnings.Count; // no warnings for a forced jump
        }
    }
}
