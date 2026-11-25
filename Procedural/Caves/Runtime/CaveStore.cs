using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralCaves
{
    /// <summary>
    /// Keeps every cave's state between visits, where the rules run (offline, the server): the deposits that were mined
    /// (and how long ago, so they go on growing back), the creatures that were slain and the chests and containers that
    /// were emptied (they come back after Cave Mining Stage > Between Visits). A cave itself - its tunnels, chambers,
    /// formations and deposits - is never stored: it comes back identically from its entrance's seed. One record per cave,
    /// in the world's store (<see cref="WorldStore"/>, section "caves"), written with the rest of the world.
    /// <list type="bullet">
    /// <item>Time here is play time (<see cref="Now"/>): what grows back or comes back does so while someone plays, never
    /// while the game is closed or paused.</item>
    /// <item>A record remembers the layout it belongs to (a fingerprint): if the cave behind an entrance changed (another
    /// world, an edited profile), the old record is dropped instead of being applied to the wrong deposits.</item>
    /// <item>The caves visited longest ago are forgotten beyond <see cref="MaxCaves"/> (never one a visit going on holds:
    /// <see cref="Open"/> / <see cref="Release"/>). A world that is not kept here (a
    /// network client, a refused world) keeps its caves in memory for the session; so does a network server, whose caves
    /// come from seeds keyed with its secret for this run (after a restart they are other caves).</item>
    /// <item>Two visits of one cave going at once (two groups, separate runs) share its record: each keeps its own copy's
    /// state, the last one kept wins, and one lump count per seam serves both, so no lump is ever given twice.</item>
    /// </list>
    /// </summary>
    public static class CaveStore
    {
        /// <summary>The section of the world's store.</summary>
        public const string Section = "caves";

        /// <summary>A switch for the whole game (tests, a game with its own saves): off = caves keep nothing between visits.</summary>
        public static bool Enabled = true;

        /// <summary>Most caves remembered (the ones visited longest ago are forgotten first).</summary>
        public static int MaxCaves = 96;

        /// <summary>A mined deposit: its placement and its state when it was kept.</summary>
        [Serializable]
        public struct DepositState
        {
            public int index;
            public OreDeposit.SavedState state;
            /// <summary>Play time it was kept at (see <see cref="Now"/>).</summary>
            public double at;
        }

        /// <summary>A slain creature or an emptied container: its placement and when.</summary>
        [Serializable]
        public struct Mark
        {
            public int index;
            public double at;
        }

        /// <summary>One cave's state.</summary>
        [Serializable]
        public sealed class Record
        {
            public string key = "";
            public string name = "";
            public int fingerprint;
            public double firstVisit;
            public double lastVisit;
            public int visits;
            public long lumpsMined;
            public List<DepositState> deposits = new List<DepositState>();
            public List<Mark> slain = new List<Mark>();
            public List<Mark> taken = new List<Mark>();

            [NonSerialized] private Dictionary<int, int> depositAt;
            // Changed since it was last written (its JSON is kept until then: the world's commits only redo what changed).
            [NonSerialized] internal bool dirty = true;
            [NonSerialized] internal string json;
            // Visits going on that hold it (never forgotten meanwhile, see Trim).
            [NonSerialized] internal int holders;

            /// <summary>Is a visit of the cave going on (its record is never forgotten meanwhile)?</summary>
            public bool IsHeld => holders > 0;

            private Dictionary<int, int> DepositIndex()
            {
                if (depositAt != null && depositAt.Count == deposits.Count)
                    return depositAt;
                depositAt = new Dictionary<int, int>();
                for (int i = 0; i < deposits.Count; i++)
                    depositAt[deposits[i].index] = i;
                return depositAt;
            }

            /// <summary>The kept state of deposit placement <paramref name="index"/>, and how many seconds of play ago it was kept.</summary>
            public bool TryGetDeposit(int index, out OreDeposit.SavedState state, out float secondsAgo)
            {
                if (DepositIndex().TryGetValue(index, out int i))
                {
                    state = deposits[i].state;
                    secondsAgo = (float)Math.Max(0.0, Now - deposits[i].at);
                    return true;
                }
                state = default;
                secondsAgo = 0f;
                return false;
            }

            /// <summary>
            /// Keeps deposit placement <paramref name="index"/>'s state. The lumps ever given never go down (two visits of the
            /// cave going at once each keep their copy's state; the last one kept wins, but no lump number is given twice).
            /// </summary>
            public void SetDeposit(int index, OreDeposit.SavedState state)
            {
                if (DepositIndex().TryGetValue(index, out int i))
                {
                    state.issued = Math.Max(state.issued, deposits[i].state.issued);
                    deposits[i] = new DepositState { index = index, state = state, at = Now };
                }
                else
                {
                    deposits.Add(new DepositState { index = index, state = state, at = Now });
                    depositAt[index] = deposits.Count - 1;
                }
                dirty = true;
            }

            /// <summary>
            /// The number of the next lump of deposit placement <paramref name="index"/>, one count for every copy of the cave
            /// going at once (<paramref name="localIssued"/>: what this copy has given; <paramref name="current"/>: its state, kept
            /// when the cave has none yet for it).
            /// </summary>
            public int NextLump(int index, int localIssued, OreDeposit.SavedState current)
            {
                int n = Math.Max(0, localIssued);
                if (DepositIndex().TryGetValue(index, out int i))
                {
                    DepositState d = deposits[i];
                    n = Math.Max(n, d.state.issued);
                    d.state.issued = n + 1;
                    deposits[i] = d;
                }
                else
                {
                    current.issued = n + 1;
                    deposits.Add(new DepositState { index = index, state = current, at = Now });
                    depositAt[index] = deposits.Count - 1;
                }
                dirty = true;
                return n;
            }

            /// <summary>Was placement <paramref name="index"/> slain less than <paramref name="returnSeconds"/> ago (0 = it never returns)?</summary>
            public bool IsSlain(int index, double returnSeconds) => Within(slain, index, returnSeconds);

            /// <summary>Was placement <paramref name="index"/> emptied less than <paramref name="returnSeconds"/> ago (0 = it never fills again)?</summary>
            public bool IsTaken(int index, double returnSeconds) => Within(taken, index, returnSeconds);

            public void Slay(int index) => Set(slain, index);

            public void Take(int index) => Set(taken, index);

            /// <summary>One more lump mined in this cave.</summary>
            public void NoteLump()
            {
                lumpsMined++;
                dirty = true;
            }

            private bool Within(List<Mark> marks, int index, double returnSeconds)
            {
                for (int i = marks.Count - 1; i >= 0; i--)
                {
                    if (marks[i].index != index)
                        continue;
                    if (returnSeconds <= 0.0 || Now - marks[i].at < returnSeconds)
                        return true;
                    marks.RemoveAt(i);   // it has come back
                    dirty = true;
                    return false;
                }
                return false;
            }

            private void Set(List<Mark> marks, int index)
            {
                dirty = true;
                for (int i = 0; i < marks.Count; i++)
                    if (marks[i].index == index)
                    {
                        marks[i] = new Mark { index = index, at = Now };
                        return;
                    }
                marks.Add(new Mark { index = index, at = Now });
            }

            /// <summary>Forgets everything (the cave behind the key changed).</summary>
            public void Clear()
            {
                deposits.Clear();
                slain.Clear();
                taken.Clear();
                depositAt = null;
                lumpsMined = 0;
                dirty = true;
            }
        }

        [Serializable]
        private sealed class Part
        {
            public int version = 1;
            public double clock;
            public List<Record> caves = new List<Record>();
        }

        private static Dictionary<string, Record> records;
        private static bool fromWorld, subscribed;
        private static double clockBase;
        private static float clockStart;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Enabled = true;
            MaxCaves = 96;
            records = null;
            fromWorld = subscribed = false;
            clockBase = 0.0;
            clockStart = 0f;
        }

        /// <summary>
        /// Seconds of play in this world (kept with the world, so it goes on where it stopped). Game time, as a deposit's own
        /// regrowth counts it: a paused game (time scale 0) is not play time on either side.
        /// </summary>
        public static double Now => clockBase + Math.Max(0f, Time.time - clockStart);

        /// <summary>Caves remembered now.</summary>
        public static int Count => Records().Count;

        /// <summary>
        /// The record of the cave <paramref name="key"/> (made when missing), for the layout with
        /// <paramref name="fingerprint"/> - a record made for another layout is cleared first. Null when disabled. The visit
        /// holds it (it is never forgotten while held) until it gives it back with <see cref="Release"/>.
        /// </summary>
        public static Record Open(string key, int fingerprint, string name)
        {
            if (!Enabled || string.IsNullOrEmpty(key))
                return null;
            Dictionary<string, Record> all = Records();
            if (!all.TryGetValue(key, out Record r) || r == null)
            {
                r = new Record { key = key, fingerprint = fingerprint, firstVisit = Now };
                all[key] = r;
            }
            else if (r.fingerprint != fingerprint)
            {
                r.Clear();
                r.fingerprint = fingerprint;
            }
            r.name = name ?? "";
            r.visits++;
            r.lastVisit = Now;
            r.holders++;
            r.dirty = true;
            Trim(all, r);
            Changed();
            return r;
        }

        /// <summary>
        /// A visit that opened <paramref name="r"/> is over: it may be forgotten again (the visit's end counts as its last
        /// visit, so the caves left longest ago go first).
        /// </summary>
        public static void Release(Record r)
        {
            if (r == null)
                return;
            r.holders = Math.Max(0, r.holders - 1);
            r.lastVisit = Now;
            r.dirty = true;
            Changed();
        }

        /// <summary>
        /// Starts this world's play clock now (the world side calls it when cave entrances appear): what grows back counts
        /// the whole time played, not only the time since a cave was first entered this session.
        /// </summary>
        public static void Begin()
        {
            if (Application.isPlaying)
                Records();
        }

        /// <summary>
        /// Forgets the caves visited longest ago beyond <see cref="MaxCaves"/> - never one a visit going on holds, nor
        /// <paramref name="keep"/> (the one being opened). With more caves held than that, more are kept for a while.
        /// </summary>
        private static void Trim(Dictionary<string, Record> all, Record keep = null)
        {
            int excess = all.Count - Mathf.Max(1, MaxCaves);
            if (excess <= 0)
                return;
            var idle = new List<Record>(all.Count);
            foreach (Record r in all.Values)
                if (r != null && !ReferenceEquals(r, keep) && r.holders <= 0)
                    idle.Add(r);
            // Oldest first.
            idle.Sort((a, b) => a.lastVisit.CompareTo(b.lastVisit));
            for (int i = 0; i < idle.Count && excess > 0; i++, excess--)
                all.Remove(idle[i].key);
        }

        /// <summary>The record of <paramref name="key"/>, or null (tools, tests, a map of visited caves).</summary>
        public static Record Find(string key) => !string.IsNullOrEmpty(key) && Records().TryGetValue(key, out Record r) ? r : null;

        /// <summary>Every remembered cave.</summary>
        public static IEnumerable<Record> All => Records().Values;

        /// <summary>Forgets one cave (it is as new on the next visit), or every cave with null.</summary>
        public static void Forget(string key = null)
        {
            if (key == null)
                Records().Clear();
            else
                Records().Remove(key);
            Changed();
        }

        /// <summary>A record changed: the world is written in a moment.</summary>
        public static void Changed()
        {
            if (fromWorld)
                WorldStore.MarkDirty();
        }

        /// <summary>
        /// The whole store as JSON (the world's part; tools, tests) and back. Each record's JSON is kept until it changes, so
        /// writing the world while one cave is mined redoes that cave only.
        /// </summary>
        public static string ToJson()
        {
            var sb = new System.Text.StringBuilder(256);
            sb.Append("{\"version\":1,\"clock\":");
            sb.Append(Now.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",\"caves\":[");
            bool first = true;
            foreach (Record r in Records().Values)
            {
                if (r == null)
                    continue;
                if (r.dirty || r.json == null)
                {
                    r.json = JsonUtility.ToJson(r);
                    r.dirty = false;
                }
                if (!first)
                    sb.Append(',');
                sb.Append(r.json);
                first = false;
            }
            sb.Append("]}");
            return sb.ToString();
        }

        public static void FromJson(string json)
        {
            records = new Dictionary<string, Record>();
            clockBase = 0.0;
            clockStart = Time.time;
            if (string.IsNullOrEmpty(json))
                return;
            try
            {
                Part part = JsonUtility.FromJson<Part>(json);
                if (part == null)
                    return;
                clockBase = Math.Max(0.0, part.clock);
                if (part.caves != null)
                    foreach (Record r in part.caves)
                        if (r != null && !string.IsNullOrEmpty(r.key))
                        {
                            r.deposits = r.deposits ?? new List<DepositState>();
                            r.slain = r.slain ?? new List<Mark>();
                            r.taken = r.taken ?? new List<Mark>();
                            r.dirty = true;
                            records[r.key] = r;
                        }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Caves] The kept caves could not be read ({e.Message}); they start fresh.");
            }
        }

        // ------------------------------------------------------------------ the world's part

        private static Dictionary<string, Record> Records()
        {
            if (records != null)
                return records;
            Subscribe();
            // A network session keys its dungeon seeds with the server's secret for this run: after a restart its caves are
            // new caves, so nothing is written to the world - they are remembered while the server runs.
            fromWorld = Application.isPlaying && !GameNet.NetAuthority.IsNetworked && WorldStore.IsOpen;
            FromJson(fromWorld ? WorldStore.Get(Section) : null);
            return records;
        }

        private static void Subscribe()
        {
            if (subscribed || !Application.isPlaying)
                return;
            subscribed = true;
            WorldStore.Committing += Write;
            WorldStore.WorldChanged += OnWorldChanged;
        }

        /// <summary>The world is being written: the caves' part goes in (the longest unvisited forgotten beyond the limit).</summary>
        private static void Write()
        {
            if (records == null || !fromWorld)
                return;
            Trim(records);
            WorldStore.Put(Section, ToJson());
        }

        private static void OnWorldChanged()
        {
            // Another world: what was read belongs to the old one.
            records = null;
            fromWorld = false;
        }
    }
}
