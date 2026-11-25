using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// Everything a rune user's soul keeps between sessions (decision D6): the soul resource, the absorbed runes, the pending
    /// magical energy, the concept knowledge, the discovery state, the spells and their ability keys, the synthesis log, the
    /// grimoire and some statistics. Plain data, JSON-serialisable with <see cref="JsonUtility"/>: lists, never dictionaries,
    /// ids as strings. Where the rules run, <see cref="RuneSoul"/> holds one and <see cref="RuneSoulSaves"/> keeps it in the
    /// world's store; a network client never has the server's (it gets views).
    /// <para>The format only ever gains fields. <see cref="version"/> records the format a save was written with (it is not
    /// changed on load); an older save loads and fills what it lacks (<see cref="EnsureLists"/>).</para>
    /// <list type="bullet">
    /// <item>1: the soul (Phase 2).</item>
    /// <item>2: the runes, the pending energy and the concept knowledge (Phase 3).</item>
    /// <item>3: the discovery state (Phase 4).</item>
    /// <item>4: the spells, composite runes, the synthesis log and its counters (Phase 5).</item>
    /// <item>5: the ability keys of spells (Phase 7).</item>
    /// <item>6: the grimoire's serial (the grimoire update).</item>
    /// </list>
    /// </summary>
    [Serializable]
    public class RuneSoulState
    {
        /// <summary>The format this code writes (5 added the keys of spells; 6 the grimoire's serial).</summary>
        public const int CurrentVersion = 6;

        [Tooltip("The format the state was written with: only a record (fields are only ever added).")]
        public int version = CurrentVersion;

        // ---- version 1: the soul resource
        public SoulState soul = new SoulState();

        // ---- version 2: runes, pending energy, knowledge
        [Tooltip("The runes absorbed into the soul.")]
        public List<RuneInstance> runes = new List<RuneInstance>();
        [Tooltip("Magical energy from kills waiting to be given to a rune of the player's choice.")]
        public float pendingEnergy;
        [Tooltip("The concepts the player has discovered, per rune definition (decision D13): permanent.")]
        public List<ConceptKnowledge> knowledge = new List<ConceptKnowledge>();
        public int runesAbsorbed;
        public int runesReleased;
        public float lifetimeEnergyGained;
        public float lifetimeEnergyAssigned;

        // ---- version 3: concept discovery
        public DiscoveryState discovery = new DiscoveryState();

        // ---- version 4: synthesis
        [Tooltip("The spells made by synthesis (each anchors the runes it was made from).")]
        public List<SpellRecord> spells = new List<SpellRecord>();
        [Tooltip("The last attempts (inputs, score, outcome), for the player's research and debugging.")]
        public List<SynthesisLogEntry> synthesisLog = new List<SynthesisLogEntry>();
        public int synthesesPerfect;
        public int synthesesSuccessful;
        public int synthesesFlawed;
        public int synthesesFailed;

        // ---- version 5: the ability keys of spells
        [Tooltip("Which spell is on which ability key slot.")]
        public List<SpellSlotAssignment> spellSlots = new List<SpellSlotAssignment>();

        // ---- version 6: the grimoire
        [Tooltip("The serial of the Mage's own grimoire in the world's ledger (empty: none made yet).")]
        public string grimoireSerial = "";
        [Tooltip("The Mage has been given their grimoire at least once.")]
        public bool grimoireGiven;

        /// <summary>The absorbed rune with this instance id (null when none, or when the id is empty).</summary>
        public RuneInstance Rune(string instanceId)
        {
            if (runes == null || string.IsNullOrEmpty(instanceId)) return null;
            for (int i = 0; i < runes.Count; i++)
            {
                RuneInstance r = runes[i];
                if (r != null && string.Equals(r.instanceId, instanceId, StringComparison.Ordinal)) return r;
            }
            return null;
        }

        /// <summary>
        /// Fills what an older or hand-made state lacks (missing lists, parts and texts), at every level. Never removes, reorders or
        /// changes anything that is there, so it is cheap and safe to call at any time (the rules call it before they write).
        /// </summary>
        public void EnsureLists()
        {
            if (soul == null) soul = new SoulState();
            soul.EnsureLists();
            if (runes == null) runes = new List<RuneInstance>();
            for (int i = 0; i < runes.Count; i++) runes[i]?.EnsureLists();
            if (knowledge == null) knowledge = new List<ConceptKnowledge>();
            for (int i = 0; i < knowledge.Count; i++) knowledge[i]?.EnsureLists();
            if (discovery == null) discovery = new DiscoveryState();
            discovery.EnsureLists();
            if (spells == null) spells = new List<SpellRecord>();
            for (int i = 0; i < spells.Count; i++) spells[i]?.EnsureLists();
            if (synthesisLog == null) synthesisLog = new List<SynthesisLogEntry>();
            for (int i = 0; i < synthesisLog.Count; i++) synthesisLog[i]?.EnsureLists();
            if (spellSlots == null) spellSlots = new List<SpellSlotAssignment>();
            for (int i = 0; i < spellSlots.Count; i++) spellSlots[i]?.EnsureLists();
            if (grimoireSerial == null) grimoireSerial = "";
        }

        /// <summary>A deep copy: nothing is shared with this state (saves keep one, tools experiment on one).</summary>
        public RuneSoulState Clone()
        {
            var c = (RuneSoulState)MemberwiseClone();
            c.soul = soul != null ? soul.Clone() : new SoulState();
            c.runes = new List<RuneInstance>();
            if (runes != null)
                foreach (RuneInstance r in runes)
                    if (r != null) c.runes.Add(r.Clone());
            c.knowledge = new List<ConceptKnowledge>();
            if (knowledge != null)
                foreach (ConceptKnowledge k in knowledge)
                    if (k != null) c.knowledge.Add(k.Clone());
            c.discovery = discovery != null ? discovery.Clone() : new DiscoveryState();
            c.spells = new List<SpellRecord>();
            if (spells != null)
                foreach (SpellRecord sp in spells)
                    if (sp != null) c.spells.Add(sp.Clone());
            c.synthesisLog = new List<SynthesisLogEntry>();
            if (synthesisLog != null)
                foreach (SynthesisLogEntry e in synthesisLog)
                    if (e != null) c.synthesisLog.Add(e.Clone());
            c.spellSlots = new List<SpellSlotAssignment>();
            if (spellSlots != null)
                foreach (SpellSlotAssignment a in spellSlots)
                    if (a != null) c.spellSlots.Add(a.Clone());
            c.grimoireSerial = grimoireSerial ?? "";
            return c;
        }

        /// <summary>The state as JSON (what is saved).</summary>
        public string ToJson(bool pretty = false) => JsonUtility.ToJson(this, pretty);

        /// <summary>
        /// Reads a saved state. Never null and never throws: empty or unreadable text gives a fresh state (its soul starts full once
        /// normalized). What is read is repaired (see <see cref="Repair"/>); the version is kept as written. A valid save reads back
        /// exactly as it was written.
        /// </summary>
        public static RuneSoulState FromJson(string json)
        {
            RuneSoulState s = null;
            if (!string.IsNullOrEmpty(json))
            {
                try { s = JsonUtility.FromJson<RuneSoulState>(json); }
                catch (Exception) { s = null; } // not JSON, or not this format: a fresh soul
            }
            if (s == null) return new RuneSoulState();
            s.EnsureLists();
            s.Repair();
            return s;
        }

        /// <summary>
        /// Puts a loaded (or hand-edited) state back within its rules, changing nothing in a valid one: drops empty entries; gives a
        /// rune without an id (or with another rune's id) a new one; keeps a rune's rank at 1 or more, its quality within 0-100 and its
        /// energy and the pending energy at 0 or more (its maximum needs the database: <see cref="EnergyRules.ClampAll"/>); merges the
        /// knowledge of one rune written twice; drops the spells whose runes are gone (<see cref="SpellRules.RemoveBroken"/>) and the
        /// keys of spells that are gone (<see cref="SpellSlotRules.Normalize"/>).
        /// </summary>
        public void Repair()
        {
            EnsureLists();
            runes.RemoveAll(r => r == null);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (RuneInstance r in runes)
            {
                if (string.IsNullOrEmpty(r.instanceId) || !ids.Add(r.instanceId))
                {
                    r.instanceId = Guid.NewGuid().ToString("N");
                    ids.Add(r.instanceId);
                }
                if (r.rank < 1) r.rank = 1;
                r.quality = Clamp(r.quality, 0f, 100f);
                r.energy = Math.Max(0f, Finite(r.energy));
            }
            pendingEnergy = Math.Max(0f, Finite(pendingEnergy));

            knowledge.RemoveAll(k => k == null || string.IsNullOrEmpty(k.runeId));
            for (int i = 0; i < knowledge.Count; i++)
            {
                ConceptKnowledge k = knowledge[i];
                for (int j = i + 1; j < knowledge.Count;)
                {
                    if (knowledge[j].runeId != k.runeId) { j++; continue; }
                    k.concepts.AddRange(knowledge[j].concepts);
                    knowledge.RemoveAt(j);
                }
                var seen = new HashSet<string>(StringComparer.Ordinal);
                k.concepts.RemoveAll(id => string.IsNullOrEmpty(id) || !seen.Add(id));
            }

            discovery.Repair();
            soul.maxBonuses.RemoveAll(x => x == null);
            soul.capacityBonuses.RemoveAll(x => x == null);
            spells.RemoveAll(sp => sp == null);
            SpellRules.RemoveBroken(this);
            synthesisLog.RemoveAll(e => e == null);
            SpellSlotRules.Normalize(this);
        }

        internal static float Finite(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0f : v;
        internal static float Clamp(float v, float min, float max) => float.IsNaN(v) ? min : v < min ? min : v > max ? max : v;
    }

    /// <summary>
    /// The soul resource (design spec 14, 15): Soul Integrity, deep damage (part of what is missing; only specialized healing
    /// removes it), Soul Collapse and its lock, lasting bonuses to the maximum and to the Soul Rune Capacity, and counters. The
    /// rules are <see cref="SoulRules"/> and <see cref="CapacityRules"/>; the maximum itself is derived (balance + bonuses), never
    /// stored. It holds no health, mana, stamina or rune energy (design spec 20).
    /// </summary>
    [Serializable]
    public class SoulState
    {
        [Tooltip("False for a soul never normalized: it starts full the first time (SoulRules.Normalize).")]
        public bool initialized;
        [Tooltip("Soul Integrity now (0 = collapsed).")]
        public float integrity;
        [Tooltip("Deep damage: what ordinary recovery cannot reach (items flagged for it and rituals heal it).")]
        public float deepDamage;
        [Tooltip("Set by a Soul Collapse; no synthesis or casting until the soul is back to Lock Until Percent.")]
        public bool collapseLocked;
        public int collapses;
        [Tooltip("UTC ticks of the last Soul Collapse (0 = never).")]
        public long lastCollapseUtc;
        public float lifetimeDamage;
        public float lifetimeRecovered;
        [Tooltip("Lasting bonuses to the maximum Soul Integrity, by source.")]
        public List<IntegrityBonus> maxBonuses = new List<IntegrityBonus>();
        [Tooltip("Lasting bonuses to the Soul Rune Capacity, by source.")]
        public List<CapacityBonus> capacityBonuses = new List<CapacityBonus>();

        /// <summary>Fills the lists an older save lacks (never changes what is there).</summary>
        public void EnsureLists()
        {
            if (maxBonuses == null) maxBonuses = new List<IntegrityBonus>();
            if (capacityBonuses == null) capacityBonuses = new List<CapacityBonus>();
        }

        /// <summary>A deep copy.</summary>
        public SoulState Clone()
        {
            var c = (SoulState)MemberwiseClone();
            c.maxBonuses = new List<IntegrityBonus>();
            if (maxBonuses != null)
                foreach (IntegrityBonus x in maxBonuses)
                    if (x != null) c.maxBonuses.Add(new IntegrityBonus { source = x.source ?? "", amount = x.amount });
            c.capacityBonuses = new List<CapacityBonus>();
            if (capacityBonuses != null)
                foreach (CapacityBonus x in capacityBonuses)
                    if (x != null) c.capacityBonuses.Add(new CapacityBonus { source = x.source ?? "", slots = x.slots });
            return c;
        }
    }

    /// <summary>A lasting bonus to the maximum Soul Integrity (<see cref="SoulRules.SetMaxBonus"/>): one value per source.</summary>
    [Serializable]
    public class IntegrityBonus
    {
        [Tooltip("What gives it (a relic, a breakthrough...): one bonus per source.")]
        public string source = "";
        public float amount;
    }

    /// <summary>A lasting bonus to the Soul Rune Capacity (<see cref="CapacityRules.SetBonus"/>): one value per source.</summary>
    [Serializable]
    public class CapacityBonus
    {
        [Tooltip("What gives it (a breakthrough, a reward...): one bonus per source.")]
        public string source = "";
        public int slots;
    }
}
