using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// What concept discovery remembers of a player (design spec 8, 17; <see cref="DiscoveryFlow"/>): the sources a wrong answer
    /// burned for a concept (decision D7: never again for this player), the minigame tries used, the written riddles already seen, a
    /// short log and two counters. The concepts learned themselves are the soul's knowledge (<see cref="RuneSoulState.knowledge"/>).
    /// </summary>
    [Serializable]
    public class DiscoveryState
    {
        [Tooltip("Burned keys: \"<source definition>|<concept>\" (DiscoveryRules.BurnKey).")]
        public List<string> burned = new List<string>();
        [Tooltip("Minigame tries used, per burn key (cleared when the concept is learned).")]
        public List<KeyCount> attempts = new List<KeyCount>();
        [Tooltip("Written riddles already asked (their ids).")]
        public List<string> seenRiddles = new List<string>();
        [Tooltip("The last attempts (Discovery Balance's Log Length).")]
        public List<DiscoveryLogEntry> log = new List<DiscoveryLogEntry>();
        [Tooltip("Concepts learned through discovery.")]
        public int learned;
        [Tooltip("Discoveries that burned a source.")]
        public int failed;

        /// <summary>Fills what an older save lacks (never changes what is there).</summary>
        public void EnsureLists()
        {
            if (burned == null) burned = new List<string>();
            if (attempts == null) attempts = new List<KeyCount>();
            if (seenRiddles == null) seenRiddles = new List<string>();
            if (log == null) log = new List<DiscoveryLogEntry>();
        }

        /// <summary>Drops missing entries of a loaded state (the rules expect none); a valid state is left as it is.</summary>
        public void Repair()
        {
            EnsureLists();
            burned.RemoveAll(k => k == null);
            attempts.RemoveAll(a => a == null);
            foreach (KeyCount a in attempts) if (a.key == null) a.key = "";
            seenRiddles.RemoveAll(id => id == null);
            log.RemoveAll(e => e == null);
        }

        /// <summary>A deep copy.</summary>
        public DiscoveryState Clone()
        {
            var c = (DiscoveryState)MemberwiseClone();
            c.burned = burned != null ? new List<string>(burned) : new List<string>();
            c.seenRiddles = seenRiddles != null ? new List<string>(seenRiddles) : new List<string>();
            c.attempts = new List<KeyCount>();
            if (attempts != null)
                foreach (KeyCount a in attempts)
                    if (a != null) c.attempts.Add(new KeyCount { key = a.key ?? "", count = a.count });
            c.log = new List<DiscoveryLogEntry>();
            if (log != null)
                foreach (DiscoveryLogEntry e in log)
                    if (e != null) c.log.Add(e.Clone());
            return c;
        }
    }

    /// <summary>A count under a key (JSON has no dictionaries): the minigame tries used at a source for a concept.</summary>
    [Serializable]
    public class KeyCount
    {
        public string key = "";
        public int count;
    }

    /// <summary>One discovery attempt in the player's log.</summary>
    [Serializable]
    public class DiscoveryLogEntry
    {
        [Tooltip("UTC ticks.")]
        public long utc;
        [Tooltip("The source definition id.")]
        public string source = "";
        [Tooltip("The concept id (the log is the server's: it is never sent to a client).")]
        public string concept = "";
        [Tooltip("The difficulty band (index).")]
        public int tier;
        [Tooltip("learned, wrong, minigame_failed, abandoned, expired.")]
        public string outcome = "";

        public DiscoveryLogEntry Clone() => (DiscoveryLogEntry)MemberwiseClone();
    }
}
