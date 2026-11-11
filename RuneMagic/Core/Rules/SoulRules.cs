using System;
using System.Collections.Generic;

namespace RuneMagic
{
    /// <summary>What one Soul Damage did.</summary>
    public struct SoulDamageResult
    {
        /// <summary>The damage asked for, before multipliers.</summary>
        public float requested;
        /// <summary>The damage after the kind's and the condition's multipliers.</summary>
        public float scaled;
        /// <summary>Soul Integrity actually lost (never below 0).</summary>
        public float applied;
        /// <summary>Deep damage added (needs specialized healing).</summary>
        public float deepAdded;
        /// <summary>The soul reached 0 with this damage: Soul Collapse.</summary>
        public bool collapsedNow;
        public string kindId;
        public string conditionBefore;
        public string conditionAfter;
    }

    /// <summary>
    /// What the soul's condition allows (design spec 14.4, casting debuffs 16.2): read by synthesis and casting. A collapse
    /// lock (after a Soul Collapse, until the soul recovers enough) forbids both whatever the condition says.
    /// </summary>
    public struct SoulRestrictions
    {
        public string conditionId;
        public string conditionName;
        public string castingDebuff;
        public bool collapsed;
        public bool collapseLocked;

        public bool synthesisAllowed;
        public float synthesisFailureChance;
        public float synthesisStability;
        public int maxSynthesisRunes;
        public float maxSynthesisComplexity;

        public bool castingAllowed;
        public float castTimeMultiplier;
        public float powerMultiplier;
        public float energyCostMultiplier;
        public float instabilityChance;
        public int maxCastableRank;
        public float soulDamageMultiplier;

        // Spelling System (spec 16.5): what the condition does to a key sequence.
        public int spellingExtraLetters;
        public float spellingWindowMultiplier;
        public float spellingPenaltyMultiplier;

        /// <summary>No restriction at all (a soul that is not a rune user's, tools).</summary>
        public static SoulRestrictions None => new SoulRestrictions
        {
            conditionId = "", conditionName = "", castingDebuff = "",
            synthesisAllowed = true, synthesisStability = 1f, maxSynthesisComplexity = 1f,
            castingAllowed = true, castTimeMultiplier = 1f, powerMultiplier = 1f, energyCostMultiplier = 1f, soulDamageMultiplier = 1f,
            spellingWindowMultiplier = 1f, spellingPenaltyMultiplier = 1f,
        };

        /// <summary>May a synthesis of <paramref name="runeCount"/> runes aiming at an effect of <paramref name="complexity"/> (0-1) start?</summary>
        public bool AllowsSynthesis(int runeCount, float complexity, out string reason)
        {
            reason = "";
            if (collapseLocked) { reason = "Your soul has collapsed: recover before attempting a synthesis."; return false; }
            if (!synthesisAllowed) { reason = $"Your soul is too unstable to synthesize ({conditionName})."; return false; }
            if (maxSynthesisRunes > 0 && runeCount > maxSynthesisRunes)
            {
                reason = $"In your condition ({conditionName}) a synthesis may combine at most {maxSynthesisRunes} runes.";
                return false;
            }
            if (complexity > maxSynthesisComplexity + 1e-4f)
            {
                reason = $"This effect is too complex for your soul in its condition ({conditionName}).";
                return false;
            }
            return true;
        }

        /// <summary>May a spell of <paramref name="spellRank"/> be cast?</summary>
        public bool AllowsCasting(int spellRank, out string reason)
        {
            reason = "";
            if (collapseLocked) { reason = "Your soul has collapsed: you cannot cast until it recovers."; return false; }
            if (!castingAllowed) { reason = $"Your soul cannot sustain a spell ({conditionName})."; return false; }
            if (maxCastableRank > 0 && spellRank > maxCastableRank)
            {
                reason = $"In your condition ({conditionName}) only spells up to rank {maxCastableRank} can be cast.";
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// The Soul Integrity rules (design spec 14, 15): maximum, deep damage, damage, recovery, collapse, conditions and the
    /// restrictions they bring. Pure functions over <see cref="SoulState"/> and <see cref="RuneBalance"/>; they never touch
    /// health, mana, stamina or rune energy (design spec 20).
    /// <para>Invariants kept by every function: 0 ≤ deep damage ≤ maximum × Max Deep Damage Fraction, and
    /// 0 ≤ integrity ≤ maximum − deep damage (deep damage is part of what is missing).</para>
    /// </summary>
    public static class SoulRules
    {
        // ------------------------------------------------------------------ values
        /// <summary>The maximum Soul Integrity: the balance's, plus lasting bonuses and <paramref name="runtimeBonus"/> (at least 1).</summary>
        public static float MaxIntegrity(RuneBalance b, SoulState s, float runtimeBonus = 0f)
        {
            float max = (b != null ? b.maxSoulIntegrity : 100f) + SumBonuses(s) + Finite(runtimeBonus);
            return Math.Max(1f, max);
        }

        /// <summary>Most deep damage a soul of <paramref name="max"/> can carry.</summary>
        public static float MaxDeepDamage(RuneBalance b, float max)
        {
            float fraction = b != null && b.soulRecovery != null ? b.soulRecovery.maxDeepDamageFraction : 0.6f;
            return Math.Max(0f, max) * Clamp01(fraction);
        }

        /// <summary>Highest Soul Integrity ordinary recovery can reach (the maximum minus deep damage).</summary>
        public static float Ceiling(SoulState s, float max) => s == null ? max : Math.Max(0f, max - s.deepDamage);

        /// <summary>Soul Integrity as a percent (0-100) of <paramref name="max"/>.</summary>
        public static float Percent(SoulState s, float max) => s == null || max <= 0f ? 0f : Clamp(s.integrity / max * 100f, 0f, 100f);

        /// <summary>The soul's condition (design spec 14.3).</summary>
        public static SoulCondition Condition(RuneBalance b, SoulState s, float max) =>
            b == null || s == null ? null : b.ConditionOf(s.integrity, max);

        /// <summary>
        /// Puts the soul in a valid state for <paramref name="max"/>: a new soul starts full; broken numbers are repaired;
        /// deep damage and integrity are kept within their bounds; a collapse lock is released once recovered enough.
        /// </summary>
        public static void Normalize(RuneBalance b, SoulState s, float max)
        {
            if (s == null) return;
            s.EnsureLists();
            if (!s.initialized)
            {
                s.initialized = true;
                s.integrity = max;
                s.deepDamage = 0f;
            }
            s.integrity = Finite(s.integrity);
            s.deepDamage = Clamp(Finite(s.deepDamage), 0f, MaxDeepDamage(b, max));
            s.integrity = Clamp(s.integrity, 0f, Ceiling(s, max));
            UpdateCollapseLock(b, s, max);
        }

        // ------------------------------------------------------------------ damage
        /// <summary>
        /// Damages the soul by <paramref name="amount"/> of kind <paramref name="kindId"/> (see Soul Damage Kinds; an unknown
        /// kind is ordinary damage). The kind's multiplier and, when it says so, the current condition's Soul Damage
        /// Multiplier apply, then <paramref name="lossFactorPercent"/> (+50 = 50% more). A share of the damage becomes deep
        /// damage (the kind's Deep Share plus <paramref name="extraDeepShare"/>). Reaching 0 is a Soul Collapse: the collapse
        /// lock is set and the collapse's own deep damage added.
        /// </summary>
        public static SoulDamageResult Damage(RuneBalance b, SoulState s, float max, float amount, string kindId,
            float lossFactorPercent = 0f, float extraDeepShare = 0f, long utcNowTicks = 0)
        {
            var result = new SoulDamageResult { requested = Math.Max(0f, Finite(amount)), kindId = kindId ?? "" };
            if (s == null)
                return result;
            Normalize(b, s, max);
            SoulCondition before = Condition(b, s, max);
            result.conditionBefore = before != null ? before.id : "";
            result.conditionAfter = result.conditionBefore;
            if (result.requested <= 0f)
                return result;

            SoulDamageKind kind = b != null ? b.DamageKind(kindId) : null;
            float multiplier = kind != null ? kind.multiplier : 1f;
            if ((kind == null || kind.scaledByCondition) && before != null)
                multiplier *= before.soulDamageMultiplier;
            multiplier *= Math.Max(0f, 1f + Finite(lossFactorPercent) / 100f);
            float scaled = result.requested * Math.Max(0f, multiplier);
            result.scaled = scaled;
            if (scaled <= 0f)
                return result;

            float was = s.integrity;
            s.integrity = Math.Max(0f, was - scaled);
            result.applied = was - s.integrity;
            s.lifetimeDamage += result.applied;

            float deepShare = kind != null ? kind.deepShare
                : b != null && b.soulRecovery != null ? b.soulRecovery.unknownKindDeepShare : 0f;
            deepShare = Clamp01(deepShare + Finite(extraDeepShare));
            result.deepAdded = AddDeep(b, s, max, scaled * deepShare);

            if (was > 0f && s.integrity <= 0f)
            {
                result.collapsedNow = true;
                s.collapses++;
                s.collapseLocked = true;
                if (utcNowTicks > 0) s.lastCollapseUtc = utcNowTicks;
                float collapseDeep = b != null && b.soulCollapse != null ? b.soulCollapse.deepDamagePercent / 100f * max : 0f;
                result.deepAdded += AddDeep(b, s, max, collapseDeep);
            }
            SoulCondition after = Condition(b, s, max);
            result.conditionAfter = after != null ? after.id : "";
            return result;
        }

        // Adds deep damage within its bounds (never more than what is missing, never above the cap); returns what was added.
        private static float AddDeep(RuneBalance b, SoulState s, float max, float amount)
        {
            if (amount <= 0f) return 0f;
            float limit = Math.Min(MaxDeepDamage(b, max), Math.Max(0f, max - s.integrity));
            float was = s.deepDamage;
            if (was >= limit) return 0f; // already scarred as far as it can be (never reduced here)
            s.deepDamage = Math.Min(limit, was + amount);
            return s.deepDamage - was;
        }

        // ------------------------------------------------------------------ recovery
        /// <summary>
        /// Ordinary recovery (rest, meditation, sanctuaries, most items): adds up to <paramref name="amount"/>, never above
        /// the ceiling (the maximum minus deep damage). Returns what was restored.
        /// </summary>
        public static float Restore(RuneBalance b, SoulState s, float max, float amount)
        {
            if (s == null) return 0f;
            amount = Finite(amount);
            if (amount <= 0f) return 0f;
            Normalize(b, s, max);
            float ceiling = Ceiling(s, max);
            float was = s.integrity;
            s.integrity = Math.Min(ceiling, was + amount);
            float restored = Math.Max(0f, s.integrity - was);
            s.lifetimeRecovered += restored;
            UpdateCollapseLock(b, s, max);
            return restored;
        }

        /// <summary>
        /// Specialized healing (deep-healing items, rituals): removes up to <paramref name="amount"/> deep damage, so ordinary
        /// recovery can reach higher again. Integrity itself is not raised. Returns what was removed.
        /// </summary>
        public static float HealDeep(RuneBalance b, SoulState s, float max, float amount)
        {
            if (s == null) return 0f;
            amount = Finite(amount);
            if (amount <= 0f) return 0f;
            Normalize(b, s, max);
            float was = s.deepDamage;
            s.deepDamage = Math.Max(0f, was - amount);
            return was - s.deepDamage;
        }

        /// <summary>Restores the soul completely (deep damage too): tools and tests.</summary>
        public static void RestoreFully(RuneBalance b, SoulState s, float max)
        {
            if (s == null) return;
            Normalize(b, s, max);
            s.deepDamage = 0f;
            s.integrity = max;
            UpdateCollapseLock(b, s, max);
        }

        /// <summary>Releases the collapse lock once Soul Integrity is back to Lock Until Percent. Returns true when it changed.</summary>
        public static bool UpdateCollapseLock(RuneBalance b, SoulState s, float max)
        {
            if (s == null || !s.collapseLocked) return false;
            float lockUntil = b != null && b.soulCollapse != null ? b.soulCollapse.lockUntilPercent : 0f;
            if (s.integrity > 0f && Percent(s, max) >= lockUntil - 1e-4f)
            {
                s.collapseLocked = false;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ restrictions
        /// <summary>What the soul's condition (and a collapse lock) allows right now.</summary>
        public static SoulRestrictions Restrictions(RuneBalance b, SoulState s, float max)
        {
            SoulRestrictions r = SoulRestrictions.None;
            if (s == null) return r;
            SoulCondition c = Condition(b, s, max);
            if (c != null)
            {
                r.conditionId = c.id ?? "";
                r.conditionName = string.IsNullOrEmpty(c.name) ? c.id ?? "" : c.name;
                r.castingDebuff = c.castingDebuff ?? "";
                r.collapsed = c.isCollapse;
                r.synthesisAllowed = c.synthesisAllowed;
                r.synthesisFailureChance = c.synthesisFailureChance;
                r.synthesisStability = c.synthesisStability;
                r.maxSynthesisRunes = c.maxSynthesisRunes;
                r.maxSynthesisComplexity = c.maxSynthesisComplexity;
                r.castingAllowed = c.castingAllowed;
                r.castTimeMultiplier = c.castTimeMultiplier;
                r.powerMultiplier = c.powerMultiplier;
                r.energyCostMultiplier = c.energyCostMultiplier;
                r.instabilityChance = c.instabilityChance;
                r.maxCastableRank = c.maxCastableRank;
                r.soulDamageMultiplier = c.soulDamageMultiplier;
                r.spellingExtraLetters = c.spellingExtraLetters;
                r.spellingWindowMultiplier = c.spellingWindowMultiplier;
                r.spellingPenaltyMultiplier = c.spellingPenaltyMultiplier;
            }
            if (s.collapseLocked)
            {
                r.collapseLocked = true;
                r.synthesisAllowed = false;
                r.castingAllowed = false;
            }
            return r;
        }

        // ------------------------------------------------------------------ lasting bonuses
        /// <summary>Sets the lasting bonus to the maximum of <paramref name="source"/> (0 removes it). Returns true when it changed.</summary>
        public static bool SetMaxBonus(SoulState s, string source, float amount)
        {
            if (s == null || string.IsNullOrEmpty(source)) return false;
            s.EnsureLists();
            amount = Finite(amount);
            int i = s.maxBonuses.FindIndex(x => x != null && x.source == source);
            if (Math.Abs(amount) < 1e-6f)
            {
                if (i < 0) return false;
                s.maxBonuses.RemoveAt(i);
                return true;
            }
            if (i >= 0)
            {
                if (Math.Abs(s.maxBonuses[i].amount - amount) < 1e-6f) return false;
                s.maxBonuses[i].amount = amount;
                return true;
            }
            s.maxBonuses.Add(new IntegrityBonus { source = source, amount = amount });
            return true;
        }

        public static float SumBonuses(SoulState s)
        {
            float sum = 0f;
            if (s?.maxBonuses == null) return 0f;
            foreach (IntegrityBonus x in s.maxBonuses)
                if (x != null) sum += Finite(x.amount);
            return sum;
        }

        // ------------------------------------------------------------------ helpers
        internal static float Finite(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0f : v;
        internal static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
        internal static float Clamp01(float v) => Clamp(Finite(v), 0f, 1f);

        /// <summary>The ids of the kinds in <paramref name="b"/> (tools).</summary>
        public static List<string> DamageKindIds(RuneBalance b)
        {
            var ids = new List<string>();
            if (b?.soulDamageKinds == null) return ids;
            foreach (SoulDamageKind k in b.soulDamageKinds)
                if (k != null && !string.IsNullOrEmpty(k.id)) ids.Add(k.id);
            return ids;
        }
    }
}
