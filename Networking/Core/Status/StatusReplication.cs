using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Pure helpers of the status replication (health, resources, crowd control, buffs, traits): rounding of sent
    /// values, conversion between local timers and network ticks, the survival flag byte, the trait list difference and
    /// the validation of client requests. Always compiled and free of Fish-Net types, so the rules are unit tested and
    /// shared by the network component (<c>NetCombatStatus</c>).
    /// </summary>
    public static class StatusReplication
    {
        /// <summary>Resolution of replicated resource values (health, mana, hunger...).</summary>
        public const float ValueStep = 0.1f;

        /// <summary>Longest stat or trait name a client may send (longer requests are rejected).</summary>
        public const int MaxNameLength = 64;

        /// <summary>
        /// Rounds a value to <paramref name="step"/> so tiny changes (regeneration fractions, float noise) do not resend
        /// it. A positive value never rounds down to 0 (a character at 0.04 health is alive and must not look dead);
        /// non-finite values become 0.
        /// </summary>
        public static float Quantize(float value, float step = ValueStep)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0f;
            if (!(step > 0f))
                return value;
            float q = Mathf.Round(value / step) * step;
            if (value > 0f && q <= 0f)
                q = step;
            return q;
        }

        /// <summary>
        /// The network tick at which a local timer (<paramref name="until"/>, compared with <paramref name="now"/>, both
        /// Time.time of the machine that reads them) ends; 0 when it is not running. Rounded up, so a timer never ends
        /// earlier on a client than on the server.
        /// </summary>
        public static uint EndTick(float until, float now, uint currentTick, double tickDelta)
        {
            if (float.IsNaN(until) || float.IsNaN(now) || !(tickDelta > 0d) || until <= now)
                return 0;
            if (float.IsInfinity(until))
                return uint.MaxValue;
            double ticks = Math.Ceiling((until - now) / tickDelta);
            double end = currentTick + Math.Max(1d, ticks);
            return end >= uint.MaxValue ? uint.MaxValue : (uint)end;
        }

        /// <summary>
        /// Converts an end tick from the server back into a local timer (Time.time based, compared with
        /// <paramref name="now"/>): 0 when it is not running or already over at <paramref name="currentTick"/>.
        /// </summary>
        public static float LocalUntil(uint endTick, uint currentTick, double tickDelta, float now)
        {
            if (endTick == 0 || endTick <= currentTick || !(tickDelta > 0d))
                return 0f;
            if (endTick == uint.MaxValue)
                return float.PositiveInfinity;
            return now + (float)((endTick - currentTick) * tickDelta);
        }

        /// <summary>Seconds left until <paramref name="endTick"/> (0 when not running or over).</summary>
        public static float SecondsLeft(uint endTick, uint currentTick, double tickDelta)
        {
            if (endTick == 0 || endTick <= currentTick || !(tickDelta > 0d))
                return 0f;
            return (float)((endTick - currentTick) * tickDelta);
        }

        // ------------------------------------------------------------------ survival flags

        /// <summary>Bits of the survival flag byte sent to the owner.</summary>
        [Flags]
        public enum SurvivalFlags : byte
        {
            None = 0,
            Sleeping = 1 << 0,
            Collapsed = 1 << 1,
            Underwater = 1 << 2,
            HighAltitude = 1 << 3,
            PoorVentilation = 1 << 4,
            HasOxygenTank = 1 << 5,
        }

        /// <summary>Packs the sleep and breathing state into one byte.</summary>
        public static byte PackSurvival(bool sleeping, bool collapsed, bool underwater, bool highAltitude, bool poorVentilation, bool hasTank)
        {
            SurvivalFlags f = SurvivalFlags.None;
            if (sleeping) f |= SurvivalFlags.Sleeping;
            if (collapsed) f |= SurvivalFlags.Collapsed;
            if (underwater) f |= SurvivalFlags.Underwater;
            if (highAltitude) f |= SurvivalFlags.HighAltitude;
            if (poorVentilation) f |= SurvivalFlags.PoorVentilation;
            if (hasTank) f |= SurvivalFlags.HasOxygenTank;
            return (byte)f;
        }

        /// <summary>Is <paramref name="flag"/> set in a packed survival byte?</summary>
        public static bool Has(byte packed, SurvivalFlags flag) => (packed & (byte)flag) != 0;

        // ------------------------------------------------------------------ damage event flags

        /// <summary>Bits of a replicated hit (what the presentation needs to know about it).</summary>
        [Flags]
        public enum HitFlags : byte
        {
            None = 0,
            Critical = 1 << 0,
            Periodic = 1 << 1,
            Blocked = 1 << 2,
            Parried = 1 << 3,
            Reflected = 1 << 4,
        }

        /// <summary>Packs the presentation flags of a hit.</summary>
        public static byte PackHit(in DamageInfo info)
        {
            HitFlags f = HitFlags.None;
            if (info.isCritical) f |= HitFlags.Critical;
            if (info.isPeriodic) f |= HitFlags.Periodic;
            if (info.blocked) f |= HitFlags.Blocked;
            if (info.parried) f |= HitFlags.Parried;
            if (info.isReflected) f |= HitFlags.Reflected;
            return (byte)f;
        }

        /// <summary>Writes packed hit flags back into a damage info.</summary>
        public static void UnpackHit(byte packed, ref DamageInfo info)
        {
            HitFlags f = (HitFlags)packed;
            info.isCritical = (f & HitFlags.Critical) != 0;
            info.isPeriodic = (f & HitFlags.Periodic) != 0;
            info.blocked = (f & HitFlags.Blocked) != 0;
            info.parried = (f & HitFlags.Parried) != 0;
            info.isReflected = (f & HitFlags.Reflected) != 0;
        }

        // ------------------------------------------------------------------ lists

        /// <summary>
        /// What turns <paramref name="current"/> into <paramref name="wanted"/>: names to remove (in current, not wanted)
        /// and names to add (in wanted, not current), in list order, without duplicates. Null and empty names are
        /// ignored. Used by a client to follow the server's trait list.
        /// </summary>
        public static void Diff(IReadOnlyList<string> current, IReadOnlyList<string> wanted, List<string> toAdd, List<string> toRemove)
        {
            toAdd.Clear();
            toRemove.Clear();
            var want = new HashSet<string>(StringComparer.Ordinal);
            if (wanted != null)
                for (int i = 0; i < wanted.Count; i++)
                    if (!string.IsNullOrEmpty(wanted[i]))
                        want.Add(wanted[i]);
            var have = new HashSet<string>(StringComparer.Ordinal);
            if (current != null)
            {
                for (int i = 0; i < current.Count; i++)
                {
                    string n = current[i];
                    if (string.IsNullOrEmpty(n) || !have.Add(n))
                        continue;
                    if (!want.Contains(n))
                        toRemove.Add(n);
                }
            }
            if (wanted != null)
            {
                for (int i = 0; i < wanted.Count; i++)
                {
                    string n = wanted[i];
                    if (string.IsNullOrEmpty(n) || have.Contains(n))
                        continue;
                    have.Add(n);
                    toAdd.Add(n);
                }
            }
        }

        /// <summary>Do two name lists hold the same names in the same order?</summary>
        public static bool SameSequence(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            int na = a != null ? a.Count : 0;
            int nb = b != null ? b.Count : 0;
            if (na != nb)
                return false;
            for (int i = 0; i < na; i++)
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                    return false;
            return true;
        }

        // ------------------------------------------------------------------ request validation

        /// <summary>
        /// Validates a stat name a client asked to upgrade: not empty, not too long, and one of
        /// <paramref name="available"/> (case-insensitive). Returns the name as listed, or null when it is rejected.
        /// </summary>
        public static string ValidStatName(string requested, IReadOnlyList<string> available)
        {
            if (string.IsNullOrEmpty(requested) || requested.Length > MaxNameLength || available == null)
                return null;
            for (int i = 0; i < available.Count; i++)
                if (string.Equals(available[i], requested, StringComparison.OrdinalIgnoreCase))
                    return available[i];
            return null;
        }

        /// <summary>
        /// A simple per-request rate limit: true (and <paramref name="last"/> moves to <paramref name="now"/>) when at
        /// least <paramref name="interval"/> seconds passed since the last accepted request.
        /// </summary>
        public static bool Allow(ref float last, float now, float interval)
        {
            if (now - last < interval && now >= last)
                return false;
            last = now;
            return true;
        }
    }
}
