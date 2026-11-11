using System;
using System.Collections.Generic;

namespace RuneMagic
{
    /// <summary>
    /// Absorbing and releasing runes (design spec 3.2, 4.2) and what the soul knows of them. Absorption needs free soul
    /// slots for the rune's slot cost: with no capacity the rune cannot be absorbed (free a slot or increase the capacity).
    /// Absorbing a rune whose alignment conflicts with the runes held strains the soul. Released runes and their energy
    /// are gone. Pure functions over <see cref="RuneSoulState"/>.
    /// </summary>
    public static class RuneRules
    {
        /// <summary>Soul slots a rune of <paramref name="runeId"/> occupies (1 for an unknown rune).</summary>
        public static int SlotCost(RuneCatalog c, string runeId)
        {
            RuneDef def = c?.Rune(runeId);
            return def != null ? Math.Max(1, def.slotCost) : 1;
        }

        /// <summary>Soul slots a rune occupies: a composite rune's own cost, else its definition's.</summary>
        public static int SlotCost(RuneCatalog c, RuneInstance r)
        {
            if (r == null) return 1;
            if (r.IsComposite) return Math.Max(1, r.composite.slotCost);
            return SlotCost(c, r.runeId);
        }

        /// <summary>Soul slots the runes held use.</summary>
        public static int UsedSlots(RuneCatalog c, RuneSoulState s)
        {
            int used = 0;
            if (s?.runes == null) return 0;
            foreach (RuneInstance r in s.runes)
                if (r != null) used += SlotCost(c, r);
            return used;
        }

        /// <summary>"Greater Fire" (the rune's name at its rank), or the id when the rune is unknown here.</summary>
        public static string DisplayName(RuneCatalog c, string runeId, int rank)
        {
            RuneDef def = c?.Rune(runeId);
            return def != null ? c.RuneName(def, rank) : string.IsNullOrEmpty(runeId) ? "Unknown rune" : runeId;
        }

        /// <summary>The name of a rune: a composite rune's own name, else its definition's name at its rank.</summary>
        public static string DisplayName(RuneCatalog c, RuneInstance r)
        {
            if (r == null) return "";
            if (r.IsComposite && !string.IsNullOrEmpty(r.composite.name)) return r.composite.name;
            return DisplayName(c, r.runeId, r.rank);
        }

        /// <summary>The alignments of a rune: a composite rune's own, else its definition's.</summary>
        public static IReadOnlyList<string> AlignmentsOf(RuneCatalog c, RuneInstance r)
        {
            if (r == null) return Array.Empty<string>();
            if (r.IsComposite) return r.composite.alignments;
            RuneDef def = c?.Rune(r.runeId);
            return def != null && def.alignments != null ? (IReadOnlyList<string>)def.alignments : Array.Empty<string>();
        }

        // ------------------------------------------------------------------ absorption
        /// <summary>
        /// How strongly a rune of <paramref name="def"/> conflicts with the runes held: the sum of the negative alignment
        /// rules between its alignments and theirs (0 = no conflict).
        /// </summary>
        public static float Conflict(RuneCatalog c, RuneSoulState s, RuneDef def)
        {
            if (c == null || s?.runes == null || def?.alignments == null) return 0f;
            float conflict = 0f;
            foreach (RuneInstance held in s.runes)
            {
                if (held == null) continue;
                foreach (string a in def.alignments)
                    foreach (string b in AlignmentsOf(c, held))
                        foreach (CompatibilityRule rule in c.RulesBetween(CompatibilityScope.AlignmentPair, a, b))
                            if (rule != null && rule.value < 0f) conflict -= rule.value;
            }
            return conflict;
        }

        /// <summary>Soul Damage absorbing <paramref name="def"/> causes now (kind incompatible_absorption).</summary>
        public static float AbsorptionDamage(RuneCatalog c, RuneSoulState s, RuneDef def)
        {
            float perConflict = c?.Balance.acquisition != null ? c.Balance.acquisition.conflictDamage : 0f;
            return Math.Max(0f, Conflict(c, s, def) * perConflict);
        }

        /// <summary>Can a rune of <paramref name="runeId"/> be absorbed into a soul with <paramref name="capacity"/> slots?</summary>
        public static bool CanAbsorb(RuneCatalog c, RuneSoulState s, int capacity, string runeId, out string reason)
        {
            reason = "";
            RuneDef def = c?.Rune(runeId);
            if (def == null) { reason = $"Unknown rune '{runeId}'."; return false; }
            if (s == null) { reason = "No soul."; return false; }
            int used = UsedSlots(c, s), cost = SlotCost(c, runeId);
            if (!CapacityRules.CanFit(capacity, used, cost))
            {
                reason = $"Your soul has no room for {def.Name} (needs {cost} slot{(cost == 1 ? "" : "s")}, {CapacityRules.Free(capacity, used)} free of {capacity}). " +
                         "Free a slot (combine or release a rune) or increase your Soul Rune Capacity.";
                return false;
            }
            return true;
        }

        /// <summary>Binds <paramref name="d"/> to the soul (no checks: call <see cref="CanAbsorb"/> first). Its energy is kept within its maximum.</summary>
        public static RuneInstance Absorb(RuneCatalog c, RuneSoulState s, RuneDrop d, RuneOrigin origin, string instanceId, long utcTicks)
        {
            if (s == null || string.IsNullOrEmpty(d.runeId)) return null;
            s.EnsureLists();
            var r = new RuneInstance
            {
                instanceId = string.IsNullOrEmpty(instanceId) ? Guid.NewGuid().ToString("N") : instanceId,
                runeId = d.runeId,
                rank = Math.Max(1, d.rank),
                quality = Math.Max(0f, Math.Min(100f, d.quality)),
                energy = Math.Max(0f, d.energy),
                origin = origin ?? new RuneOrigin(),
                acquiredUtc = utcTicks,
            };
            if (c != null) r.energy = Math.Min(r.energy, EnergyRules.MaxEnergy(c.Balance, r));
            s.runes.Add(r);
            s.runesAbsorbed++;
            return r;
        }

        // ------------------------------------------------------------------ release (decision D12)
        /// <summary>May the rune <paramref name="instanceId"/> be released now?</summary>
        public static bool CanRelease(RuneAcquisitionBalance a, RuneSoulState s, string instanceId, bool inCombat, out string reason)
        {
            reason = "";
            if (a != null && !a.allowRelease) { reason = "Runes cannot be released."; return false; }
            if (s?.Rune(instanceId) == null) { reason = "That rune is no longer in your soul."; return false; }
            if (inCombat && (a == null || !a.releaseInCombat)) { reason = "You cannot release a rune in combat."; return false; }
            List<SpellRecord> anchored = SpellRules.AnchoredBy(s, instanceId);
            if (anchored.Count > 0)
            {
                reason = $"This rune anchors {(anchored.Count == 1 ? "the spell " + anchored[0].name : anchored.Count + " spells")}: dismiss {(anchored.Count == 1 ? "it" : "them")} first.";
                return false;
            }
            return true;
        }

        /// <summary>Removes the rune from the soul (its energy is lost). Returns it, or null when it was not there.</summary>
        public static RuneInstance Release(RuneSoulState s, string instanceId)
        {
            RuneInstance r = s?.Rune(instanceId);
            if (r == null) return null;
            s.runes.Remove(r);
            s.runesReleased++;
            return r;
        }

        // ------------------------------------------------------------------ concept knowledge (decision D13)
        /// <summary>The concepts the player knows of rune definition <paramref name="runeId"/> (empty when none).</summary>
        public static IReadOnlyList<string> KnownConcepts(RuneSoulState s, string runeId)
        {
            ConceptKnowledge k = Knowledge(s, runeId, false);
            return k != null ? (IReadOnlyList<string>)k.concepts : Array.Empty<string>();
        }

        /// <summary>Does the player know <paramref name="conceptId"/> ("wind.pressure")?</summary>
        public static bool Knows(RuneSoulState s, string runeId, string conceptId)
        {
            ConceptKnowledge k = Knowledge(s, runeId, false);
            return k != null && !string.IsNullOrEmpty(conceptId) && k.concepts.Contains(conceptId);
        }

        /// <summary>Records a discovered concept (permanent). Returns true when it was new.</summary>
        public static bool Learn(RuneSoulState s, string runeId, string conceptId)
        {
            if (s == null || string.IsNullOrEmpty(runeId) || string.IsNullOrEmpty(conceptId)) return false;
            ConceptKnowledge k = Knowledge(s, runeId, true);
            if (k.concepts.Contains(conceptId)) return false;
            k.concepts.Add(conceptId);
            return true;
        }

        private static ConceptKnowledge Knowledge(RuneSoulState s, string runeId, bool create)
        {
            if (s == null || string.IsNullOrEmpty(runeId)) return null;
            s.EnsureLists();
            foreach (ConceptKnowledge k in s.knowledge)
                if (k.runeId == runeId) return k;
            if (!create) return null;
            var made = new ConceptKnowledge { runeId = runeId };
            s.knowledge.Add(made);
            return made;
        }
    }
}
