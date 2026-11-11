using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// Keeps rune users' souls between sessions, where the rules run (offline, the server): one record per player key
    /// (<see cref="PlayerKeys"/>) in the world's store (<see cref="WorldStore"/>, section "runeMagic"), written whole with
    /// the rest of the world (atomic, with a backup). Decision D6.
    /// <list type="bullet">
    /// <item>A soul comes back as soon as its player's key is known (in a network session: once the server accepted the
    /// character). Until then the soul runs with a fresh state, which the kept one then replaces.</item>
    /// <item>A player new to the world starts with a fresh soul (full Soul Integrity).</item>
    /// <item>Players that are not rune users get no record; a rune user who changed class keeps theirs, dormant.</item>
    /// <item>Each world keeps its own (single player never reaches the online world). A client keeps nothing.</item>
    /// </list>
    /// </summary>
    public static class RuneSoulSaves
    {
        [Serializable]
        private class Record
        {
            public string key = "";
            public string name = "";
            public long savedUtc;
            public RuneSoulState soul = new RuneSoulState();
        }

        [Serializable]
        private class Part
        {
            public int version = 1;
            public List<Record> souls = new List<Record>();
        }

        private sealed class Tracked
        {
            public string key;
            public bool restored;
        }

        /// <summary>The section of the world's store.</summary>
        public const string Section = "runeMagic";

        /// <summary>A switch for the whole game (tests, a game with its own saves): off = souls are never kept or restored.</summary>
        public static bool Enabled = true;

        private static readonly Dictionary<RuneSoul, Tracked> tracked = new Dictionary<RuneSoul, Tracked>(ReferenceComparer<RuneSoul>.Instance);
        private static readonly List<RuneSoul> scratch = new List<RuneSoul>();
        private static Dictionary<string, Record> records;
        private static Runner runner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Enabled = true;
            tracked.Clear();
            records = null;
            runner = null;
        }

        /// <summary>A rune user's soul started where the rules run: it comes back once its player is known.</summary>
        internal static void Track(RuneSoul soul)
        {
            if (!Enabled || !Application.isPlaying || soul == null || tracked.ContainsKey(soul))
                return;
            if (runner == null)
            {
                var go = new GameObject("[Rune Soul Saves]") { hideFlags = HideFlags.HideInHierarchy };
                UnityEngine.Object.DontDestroyOnLoad(go);
                runner = go.AddComponent<Runner>();
                WorldStore.Committing += Write;
                WorldStore.WorldChanged += OnWorldChanged;
            }
            var t = new Tracked();
            tracked[soul] = t;
            TryRestore(soul, t);
        }

        /// <summary>A soul goes (its player left): what it holds is kept.</summary>
        internal static void Untrack(RuneSoul soul)
        {
            if (soul == null || !tracked.TryGetValue(soul, out Tracked t))
                return;
            if (t.restored) Keep(soul, t);
            tracked.Remove(soul);
            if (t.restored) WorldStore.MarkDirty();
        }

        /// <summary>The soul changed: the world is written in a moment.</summary>
        internal static void Changed(RuneSoul soul)
        {
            if (soul != null && tracked.TryGetValue(soul, out Tracked t) && t.restored)
                WorldStore.MarkDirty();
        }

        /// <summary>Is this soul kept (its player has a key and the kept soul came back)?</summary>
        public static bool IsKept(RuneSoul soul) => soul != null && tracked.TryGetValue(soul, out Tracked t) && t.restored;

        /// <summary>Keeps every known soul now (they are written with the world).</summary>
        public static void SaveAll()
        {
            foreach (KeyValuePair<RuneSoul, Tracked> kv in tracked)
                if (kv.Key != null && kv.Value.restored) Keep(kv.Key, kv.Value);
        }

        /// <summary>The kept soul of the player with <paramref name="key"/> as JSON (null when none): tools, tests.</summary>
        public static string KeptJson(string key)
        {
            Dictionary<string, Record> all = Records();
            return all != null && !string.IsNullOrEmpty(key) && all.TryGetValue(key, out Record r) && r.soul != null ? r.soul.ToJson() : null;
        }

        // ---------------------------------------------------------------- the world's part
        private static Dictionary<string, Record> Records()
        {
            if (records != null) return records;
            if (!WorldStore.IsOpen) return null;
            records = new Dictionary<string, Record>();
            string json = WorldStore.Get(Section);
            if (!string.IsNullOrEmpty(json))
                try
                {
                    Part part = JsonUtility.FromJson<Part>(json);
                    if (part?.souls != null)
                        foreach (Record r in part.souls)
                            if (r != null && !string.IsNullOrEmpty(r.key))
                            {
                                if (r.soul == null) r.soul = new RuneSoulState();
                                if (r.soul.soul == null) r.soul.soul = new SoulState();
                                r.soul.soul.EnsureLists();
                                records[r.key] = r;
                            }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Rune Magic] The kept souls could not be read ({e.Message}).");
                }
            return records;
        }

        /// <summary>The world is being written: the souls' part goes in.</summary>
        private static void Write()
        {
            if (records == null) return;
            SaveAll();
            var part = new Part();
            part.souls.AddRange(records.Values);
            WorldStore.Put(Section, JsonUtility.ToJson(part));
        }

        private static void OnWorldChanged()
        {
            // Another world: what was read belongs to the old one; every soul comes back again from the new world.
            records = null;
            foreach (KeyValuePair<RuneSoul, Tracked> kv in tracked)
            {
                kv.Value.restored = false;
                kv.Value.key = null;
            }
        }

        private static void TryRestore(RuneSoul soul, Tracked t)
        {
            CombatEntity player = soul.Entity;
            if (player == null || player.Kind != CombatEntity.EntityKind.Player) return;
            string key = PlayerKeys.Of(player);
            if (key == null) return; // not known yet (the character has not arrived) - or a player who keeps nothing
            Dictionary<string, Record> all = Records();
            if (all == null) return; // the world is not open here (another window has it, or it cannot be read)
            t.key = key;
            all.TryGetValue(key, out Record r);
            soul.RestoreFromSave(r != null ? r.soul : null);
            t.restored = true;
            Keep(soul, t); // from now on what it holds is kept
            WorldStore.MarkDirty();
        }

        private static void Keep(RuneSoul soul, Tracked t)
        {
            Dictionary<string, Record> all = Records();
            string key = t?.key;
            if (all == null || soul == null || string.IsNullOrEmpty(key) || !PlayerKeys.IsValid(key)) return;
            if (!all.TryGetValue(key, out Record r))
            {
                r = new Record { key = key };
                all[key] = r;
            }
            CombatEntity p = soul.Entity;
            if (p != null) r.name = PartySystem.NameOf(p);
            r.savedUtc = DateTime.UtcNow.Ticks;
            r.soul = soul.StateForSave.Clone();
        }

        private sealed class Runner : MonoBehaviour
        {
            private float nextLook;

            private void Update()
            {
                float now = Time.unscaledTime;
                if (now < nextLook) return;
                nextLook = now + 0.5f;
                scratch.Clear();
                foreach (KeyValuePair<RuneSoul, Tracked> kv in tracked)
                {
                    if (kv.Key == null)
                    {
                        scratch.Add(kv.Key);
                        continue;
                    }
                    if (!kv.Value.restored) TryRestore(kv.Key, kv.Value);
                }
                foreach (RuneSoul s in scratch) tracked.Remove(s);
            }
        }
    }
}
