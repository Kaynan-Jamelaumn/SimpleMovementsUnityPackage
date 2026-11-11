using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    // The balancing values of the Rune Magic System. The design spec marks these numbers as illustrative or proposed:
    // they are data, tuned on the Rune Magic Database. Rank, Quality, Energy and Soul Integrity are separate values; a
    // table here never links two of them unless it says so explicitly (Quality Affects Max Energy is off by default).

    /// <summary>A progression stage and the soul rune capacity it gives (design spec 4.1).</summary>
    [Serializable]
    public class CapacityStage
    {
        public string name = "";
        [Tooltip("Character level at which the stage starts.")]
        [Min(1)] public int fromLevel = 1;
        [Tooltip("Soul slots available at this stage (before bonuses from items, abilities, breakthroughs).")]
        [Min(0)] public int capacity = 7;

        public CapacityStage() { }
        public CapacityStage(string name, int fromLevel, int capacity)
        {
            this.name = name;
            this.fromLevel = fromLevel;
            this.capacity = capacity;
        }
    }

    /// <summary>The energy a rune of a rank can store (design spec 5.3).</summary>
    [Serializable]
    public class RankEnergy
    {
        [Min(1)] public int rank = 1;
        [Min(0f)] public float maxEnergy = 1000f;

        public RankEnergy() { }
        public RankEnergy(int rank, float maxEnergy)
        {
            this.rank = rank;
            this.maxEnergy = maxEnergy;
        }
    }

    /// <summary>The title shown before a rune's name at a rank ("Greater" Wind).</summary>
    [Serializable]
    public class RankTitle
    {
        [Min(1)] public int rank = 1;
        public string title = "";

        public RankTitle() { }
        public RankTitle(int rank, string title)
        {
            this.rank = rank;
            this.title = title;
        }
    }

    /// <summary>A named band of rune quality (stability and potential, 0-100).</summary>
    [Serializable]
    public class QualityTier
    {
        public string name = "";
        [Tooltip("Lowest quality (0-100) of the band.")]
        [Range(0f, 100f)] public float minQuality = 0f;

        public QualityTier() { }
        public QualityTier(string name, float minQuality)
        {
            this.name = name;
            this.minQuality = minQuality;
        }
    }

    /// <summary>Score thresholds of the synthesis outcome tiers (design spec 12.2).</summary>
    [Serializable]
    public class SynthesisThresholds
    {
        [Tooltip("Score (%) needed for a Perfect Synthesis.")]
        [Range(0f, 100f)] public float perfect = 100f;
        [Tooltip("Lowest score (%) of a Successful Synthesis.")]
        [Range(0f, 100f)] public float successful = 70f;
        [Tooltip("Lowest score (%) of a Flawed Synthesis; below it the synthesis fails completely.")]
        [Range(0f, 100f)] public float flawed = 30f;
    }

    /// <summary>The outcome tier of a synthesis score.</summary>
    public enum SynthesisTier
    {
        CompleteFailure,
        Flawed,
        Successful,
        Perfect,
    }

    /// <summary>A level of complete-failure severity and its consequence (design spec 13.3).</summary>
    [Serializable]
    public class FailureSeverity
    {
        public string name = "";
        [Tooltip("Applies when the score is at least this (%); the most lenient level that matches is used.")]
        [Range(0f, 100f)] public float scoreAtLeast = 0f;
        [Tooltip("Applies only when the synthesis risk (0-1) is at least this (destruction is reserved for risky attempts).")]
        [Range(0f, 1f)] public float minimumRisk = 0f;
        [Tooltip("Share of the energy allocated by each input rune that is lost.")]
        [Range(0f, 1f)] public float energyLoss = 0.25f;
        [Tooltip("The input runes are destroyed (removed from the soul).")]
        public bool destroysRunes = false;

        public FailureSeverity() { }
        public FailureSeverity(string name, float scoreAtLeast, float minimumRisk, float energyLoss, bool destroysRunes)
        {
            this.name = name;
            this.scoreAtLeast = scoreAtLeast;
            this.minimumRisk = minimumRisk;
            this.energyLoss = energyLoss;
            this.destroysRunes = destroysRunes;
        }
    }

    /// <summary>
    /// A Soul Integrity condition (design spec 14.3) and what it does to synthesis and casting (14.4, 16.2).
    /// </summary>
    [Serializable]
    public class SoulCondition
    {
        [Tooltip("Stable id: 'stable', 'strained', 'damaged', 'critical', 'near_collapse', 'collapse'.")]
        public string id = "";
        public string name = "";
        [Tooltip("Lowest Soul Integrity (% of the maximum) of the condition.")]
        [Range(0f, 100f)] public float minPercent = 0f;
        [Tooltip("This is Soul Collapse: the condition at 0 integrity.")]
        public bool isCollapse = false;
        [Tooltip("Name of the casting debuff tier (design spec 16.2): Mild / Moderate / Severe / Critical Strain, Soul Collapse.")]
        public string castingDebuff = "";
        [TextArea(1, 2)] public string effects = "";

        [Header("Synthesis")]
        public bool synthesisAllowed = true;
        [Tooltip("Added to the chance that a synthesis fails (0-1).")]
        [Range(0f, 1f)] public float synthesisFailureChance = 0f;
        [Tooltip("Multiplies the stability of a synthesis.")]
        [Range(0f, 2f)] public float synthesisStability = 1f;
        [Tooltip("Most runes one synthesis may use (0 = no limit).")]
        [Min(0)] public int maxSynthesisRunes = 0;
        [Tooltip("Most complex desired effect (complexity 0-1) a synthesis may aim for; 1 = any (design spec 14.4: complex " +
                 "combinations become unavailable).")]
        [Range(0f, 1f)] public float maxSynthesisComplexity = 1f;

        [Header("Casting")]
        public bool castingAllowed = true;
        [Range(0.1f, 5f)] public float castTimeMultiplier = 1f;
        [Range(0f, 2f)] public float powerMultiplier = 1f;
        [Range(0.1f, 5f)] public float energyCostMultiplier = 1f;
        [Tooltip("Chance (0-1) that a spell behaves unstably.")]
        [Range(0f, 1f)] public float instabilityChance = 0f;
        [Tooltip("Highest spell rank that can still be cast (0 = any).")]
        [Min(0)] public int maxCastableRank = 0;
        [Tooltip("Multiplies Soul Damage taken from casting and synthesis while in this condition.")]
        [Range(0f, 5f)] public float soulDamageMultiplier = 1f;

        [Header("Spelling System (design spec 16.5)")]
        [Tooltip("Letters added to every sequence of a spell that uses the Spelling System.")]
        [Min(0)] public int spellingExtraLetters = 0;
        [Tooltip("Multiplies the time window of every key (below 1 = shorter windows).")]
        [Range(0.1f, 2f)] public float spellingWindowMultiplier = 1f;
        [Tooltip("Multiplies what a mistake costs (power, time, stability).")]
        [Range(0.1f, 5f)] public float spellingPenaltyMultiplier = 1f;
    }

    /// <summary>All the balancing tables of the Rune Magic System, with the lookups the rules use.</summary>
    [Serializable]
    public class RuneBalance
    {
        [Header("Soul Rune Capacity (design spec 4.1, proposed values)")]
        public List<CapacityStage> capacityStages = new List<CapacityStage>();

        [Header("Rune Energy (design spec 5.3, illustrative values)")]
        public List<RankEnergy> energyByRank = new List<RankEnergy>();
        [Tooltip("Ranks above the table: the last rank's maximum × this, per rank.")]
        [Min(1f)] public float energyGrowthBeyondTable = 5f;
        [Tooltip("Explicit coupling, off by default: quality raises the maximum energy.")]
        public bool qualityAffectsMaxEnergy = false;
        [Tooltip("With the coupling on: extra maximum energy at quality 100 (0.2 = +20%).")]
        [Range(0f, 2f)] public float maxEnergyBonusAtFullQuality = 0f;

        [Header("Ranks")]
        public List<RankTitle> rankTitles = new List<RankTitle>();
        [Tooltip("Runes of the same rank a rank advancement combines (design spec 6.1).")]
        [Min(2)] public int runesPerRankAdvancement = 7;

        [Header("Quality (0-100)")]
        public List<QualityTier> qualityTiers = new List<QualityTier>();

        [Header("Synthesis outcomes (design spec 12.2, 13.3)")]
        public SynthesisThresholds synthesis = new SynthesisThresholds();
        public List<FailureSeverity> failureSeverities = new List<FailureSeverity>();

        [Header("Soul Integrity (design spec 14)")]
        [Min(1f)] public float maxSoulIntegrity = 100f;
        public List<SoulCondition> soulConditions = new List<SoulCondition>();
        [Tooltip("Sources of Soul Damage (design spec 14.1): how much of each becomes deep damage, and multipliers.")]
        public List<SoulDamageKind> soulDamageKinds = new List<SoulDamageKind>();
        [Tooltip("What happens when Soul Integrity reaches 0.")]
        public SoulCollapseRules soulCollapse = new SoulCollapseRules();

        [Header("Soul recovery (design spec 15)")]
        public SoulRecoveryBalance soulRecovery = new SoulRecoveryBalance();

        [Header("Acquisition, absorption and energy (design spec 3, 5)")]
        public RuneAcquisitionBalance acquisition = new RuneAcquisitionBalance();

        [Header("Concept discovery (design spec 17)")]
        public DiscoveryBalance discovery = new DiscoveryBalance();

        [Header("Synthesis engine (design spec 11-13)")]
        public SynthesisBalance synthesisEngine = new SynthesisBalance();

        [Header("Spellcasting (design spec 16)")]
        public SpellBalance spells = new SpellBalance();

        [Header("Spelling System (design spec 16.5)")]
        public SpellingBalance spelling = new SpellingBalance();

        [Header("Grimoire")]
        public GrimoireBalance grimoire = new GrimoireBalance();

        /// <summary>The weight of a rune rarity for drops (1 when the table does not list it).</summary>
        public float RarityWeight(RuneRarity rarity, bool rareMonster)
        {
            if (acquisition?.rarityWeights != null)
                foreach (RarityWeight w in acquisition.rarityWeights)
                    if (w != null && w.rarity == rarity) return rareMonster ? w.rareMonsters : w.normal;
            return 1f;
        }

        // ------------------------------------------------------------------ lookups
        /// <summary>The capacity stage at <paramref name="level"/> (null when the table is empty).</summary>
        public CapacityStage StageAt(int level)
        {
            CapacityStage best = null;
            foreach (CapacityStage s in capacityStages)
                if (s != null && s.fromLevel <= level && (best == null || s.fromLevel > best.fromLevel)) best = s;
            if (best == null)
                foreach (CapacityStage s in capacityStages)
                    if (s != null && (best == null || s.fromLevel < best.fromLevel)) best = s;
            return best;
        }

        /// <summary>Soul rune capacity of a character of <paramref name="level"/> plus <paramref name="bonus"/> slots.</summary>
        public int CapacityAt(int level, int bonus = 0)
        {
            CapacityStage s = StageAt(level);
            return Math.Max(0, (s != null ? s.capacity : 0) + bonus);
        }

        /// <summary>
        /// The energy a rune of <paramref name="rank"/> can store. <paramref name="quality"/> only matters when
        /// <see cref="qualityAffectsMaxEnergy"/> is on.
        /// </summary>
        public float MaxEnergy(int rank, float quality = 0f)
        {
            if (rank < 1) rank = 1;
            RankEnergy exact = null, highest = null;
            foreach (RankEnergy e in energyByRank)
            {
                if (e == null) continue;
                if (e.rank == rank) exact = e;
                if (e.rank <= rank && (highest == null || e.rank > highest.rank)) highest = e;
            }
            float max;
            if (exact != null) max = exact.maxEnergy;
            else if (highest != null) max = highest.maxEnergy * (float)Math.Pow(energyGrowthBeyondTable, rank - highest.rank);
            else max = 0f;
            if (qualityAffectsMaxEnergy)
                max *= 1f + maxEnergyBonusAtFullQuality * Mathf.Clamp01(quality / 100f);
            return max;
        }

        /// <summary>The title of <paramref name="rank"/> ("Greater"); empty when the table has none.</summary>
        public string TitleOf(int rank)
        {
            foreach (RankTitle t in rankTitles)
                if (t != null && t.rank == rank) return t.title ?? "";
            return "";
        }

        /// <summary>The quality band of <paramref name="quality"/> (null when the table is empty).</summary>
        public QualityTier QualityTierOf(float quality)
        {
            QualityTier best = null;
            foreach (QualityTier t in qualityTiers)
                if (t != null && t.minQuality <= quality && (best == null || t.minQuality > best.minQuality)) best = t;
            if (best == null)
                foreach (QualityTier t in qualityTiers)
                    if (t != null && (best == null || t.minQuality < best.minQuality)) best = t;
            return best;
        }

        /// <summary>The outcome tier of a synthesis score (0-100).</summary>
        public SynthesisTier TierOf(float score)
        {
            if (score >= synthesis.perfect) return SynthesisTier.Perfect;
            if (score >= synthesis.successful) return SynthesisTier.Successful;
            if (score >= synthesis.flawed) return SynthesisTier.Flawed;
            return SynthesisTier.CompleteFailure;
        }

        /// <summary>
        /// The severity of a complete failure with <paramref name="score"/> (0-100) and <paramref name="risk"/> (0-1). Of the
        /// levels whose conditions are met, the one with the highest score floor wins (a near miss is punished less); at
        /// the same floor the one with the highest risk floor wins (the more specific level: destruction for risky
        /// attempts), then the one losing less energy.
        /// </summary>
        public FailureSeverity SeverityOf(float score, float risk)
        {
            FailureSeverity best = null;
            foreach (FailureSeverity f in failureSeverities)
            {
                if (f == null || score < f.scoreAtLeast || risk < f.minimumRisk) continue;
                if (best == null || f.scoreAtLeast > best.scoreAtLeast)
                {
                    best = f;
                    continue;
                }
                if (!Mathf.Approximately(f.scoreAtLeast, best.scoreAtLeast)) continue;
                if (f.minimumRisk > best.minimumRisk || (Mathf.Approximately(f.minimumRisk, best.minimumRisk) && f.energyLoss < best.energyLoss))
                    best = f;
            }
            return best;
        }

        /// <summary>The Soul Integrity condition at <paramref name="integrity"/> out of <paramref name="max"/>.</summary>
        public SoulCondition ConditionOf(float integrity, float max)
        {
            SoulCondition collapse = null, best = null;
            float percent = max > 0f ? integrity / max * 100f : 0f;
            foreach (SoulCondition c in soulConditions)
            {
                if (c == null) continue;
                if (c.isCollapse) { collapse = c; continue; }
                if (c.minPercent <= percent && (best == null || c.minPercent > best.minPercent)) best = c;
            }
            if (integrity <= 0f && collapse != null)
                return collapse;
            if (best == null)
            {
                // Below every non-collapse threshold: the lowest one (or collapse if that is all there is).
                foreach (SoulCondition c in soulConditions)
                    if (c != null && !c.isCollapse && (best == null || c.minPercent < best.minPercent)) best = c;
            }
            return best ?? collapse;
        }

        /// <summary>The condition with this id (null when there is none).</summary>
        public SoulCondition Condition(string id)
        {
            foreach (SoulCondition c in soulConditions)
                if (c != null && c.id == id) return c;
            return null;
        }

        /// <summary>The soul damage kind with this id (null when there is none: the damage is ordinary).</summary>
        public SoulDamageKind DamageKind(string id)
        {
            if (string.IsNullOrEmpty(id) || soulDamageKinds == null) return null;
            foreach (SoulDamageKind k in soulDamageKinds)
                if (k != null && k.id == id) return k;
            return null;
        }

        /// <summary>Setup problems of the tables.</summary>
        public void Validate(List<string> errors, List<string> warnings)
        {
            if (capacityStages.Count == 0) errors.Add("Balance: the soul rune capacity table is empty.");
            var stages = new List<CapacityStage>(capacityStages);
            stages.RemoveAll(s => s == null);
            stages.Sort((x, y) => x.fromLevel.CompareTo(y.fromLevel));
            for (int i = 1; i < stages.Count; i++)
            {
                if (stages[i].fromLevel == stages[i - 1].fromLevel) errors.Add($"Balance: two capacity stages start at level {stages[i].fromLevel}.");
                if (stages[i].capacity < stages[i - 1].capacity) warnings.Add($"Balance: capacity stage '{stages[i].name}' gives fewer slots than the stage before it.");
            }

            bool hasRank1 = false;
            var ranks = new List<RankEnergy>(energyByRank);
            ranks.RemoveAll(r => r == null);
            ranks.Sort((x, y) => x.rank.CompareTo(y.rank));
            for (int i = 0; i < ranks.Count; i++)
            {
                if (ranks[i].rank == 1) hasRank1 = true;
                if (i > 0 && ranks[i].rank == ranks[i - 1].rank) errors.Add($"Balance: rank {ranks[i].rank} is in the energy table twice.");
                if (i > 0 && ranks[i].maxEnergy < ranks[i - 1].maxEnergy) warnings.Add($"Balance: rank {ranks[i].rank} stores less energy than rank {ranks[i - 1].rank}.");
                if (ranks[i].maxEnergy <= 0f) errors.Add($"Balance: rank {ranks[i].rank} stores no energy.");
            }
            if (!hasRank1) errors.Add("Balance: the energy table has no rank 1.");

            if (qualityTiers.Count == 0) warnings.Add("Balance: no quality tiers (qualities have no names).");
            if (!(synthesis.flawed <= synthesis.successful && synthesis.successful <= synthesis.perfect && synthesis.perfect <= 100f))
                errors.Add("Balance: synthesis thresholds must satisfy Flawed ≤ Successful ≤ Perfect ≤ 100.");
            if (failureSeverities.Count == 0) errors.Add("Balance: no failure severities (a complete failure would have no consequence).");
            else if (SeverityOf(0f, 0f) == null) errors.Add("Balance: no failure severity applies at score 0 and risk 0 (one level needs Score At Least 0 and Minimum Risk 0).");

            int collapses = 0;
            foreach (SoulCondition c in soulConditions)
            {
                if (c == null) continue;
                if (c.isCollapse) collapses++;
                if (string.IsNullOrEmpty(c.id)) errors.Add("Balance: a soul condition has no id.");
            }
            if (soulConditions.Count == 0) errors.Add("Balance: no soul conditions.");
            if (collapses != 1) errors.Add($"Balance: there must be exactly one Soul Collapse condition (found {collapses}).");
            if (ConditionOf(maxSoulIntegrity, maxSoulIntegrity) == null) errors.Add("Balance: no soul condition at full integrity.");
            if (runesPerRankAdvancement < 2) errors.Add("Balance: a rank advancement needs at least 2 runes.");
            ValidateSoul(errors, warnings);
        }

        private void ValidateDiscovery(List<string> errors, List<string> warnings)
        {
            DiscoveryBalance d = discovery;
            if (d == null) { errors.Add("Balance: concept discovery settings are missing."); return; }
            if (d.tiers == null || d.tiers.Count == 0) { errors.Add("Balance: no discovery tiers (no source could set a difficulty)."); return; }
            var ids = new HashSet<string>();
            int lastMin = int.MinValue;
            for (int i = 0; i < d.tiers.Count; i++)
            {
                DiscoveryTierDef t = d.tiers[i];
                if (t == null) { errors.Add($"Balance: discovery tier {i} is empty."); continue; }
                if (string.IsNullOrEmpty(t.id)) errors.Add($"Balance: discovery tier {i} has no id.");
                else if (!ids.Add(t.id)) errors.Add($"Balance: discovery tier id '{t.id}' is used twice.");
                if (t.minDungeonTier < lastMin) warnings.Add($"Balance: discovery tier '{t.Name}' starts at a lower dungeon tier than the one before it.");
                lastMin = t.minDungeonTier;
                if (t.rarityWeights == null || t.rarityWeights.Count < 5) errors.Add($"Balance: discovery tier '{t.Name}' needs 5 rarity weights (Common to Legendary).");
                else
                {
                    float sum = 0f;
                    foreach (float w in t.rarityWeights) sum += Math.Max(0f, w);
                    if (sum <= 0f) errors.Add($"Balance: discovery tier '{t.Name}' can teach no concept (all rarity weights are 0).");
                }
            }
        }

        private void ValidateSoul(List<string> errors, List<string> warnings)
        {
            var kinds = new HashSet<string>();
            if (soulDamageKinds == null || soulDamageKinds.Count == 0)
                warnings.Add("Balance: no soul damage kinds (every soul damage is ordinary: none of it is deep).");
            else
                foreach (SoulDamageKind k in soulDamageKinds)
                {
                    if (k == null) continue;
                    if (string.IsNullOrEmpty(k.id)) errors.Add("Balance: a soul damage kind has no id.");
                    else if (!kinds.Add(k.id)) errors.Add($"Balance: soul damage kind '{k.id}' is listed twice.");
                }
            SoulRecoveryBalance r = soulRecovery;
            if (r == null)
            {
                errors.Add("Balance: soul recovery settings are missing.");
                return;
            }
            if (r.minModifierMultiplier > r.maxModifierMultiplier)
                errors.Add("Balance: soul recovery Min Modifier Multiplier is above Max Modifier Multiplier.");
            if (r.passivePerMinute <= 0f && r.restPerMinute <= 0f && r.meditationPerMinute <= 0f && r.sanctuaryPassivePerMinute <= 0f)
                warnings.Add("Balance: no ordinary soul recovery at all (rest, meditation, passive and sanctuary rates are 0): only items and rituals heal the soul.");
            if (r.maxDeepDamageFraction >= 1f)
                warnings.Add("Balance: deep damage may scar the whole soul (Max Deep Damage Fraction 1): ordinary recovery could reach nothing.");
            if (soulCollapse == null)
                errors.Add("Balance: soul collapse rules are missing.");
            ValidateDiscovery(errors, warnings);
            if (synthesisEngine == null) errors.Add("Balance: the synthesis engine settings are missing.");
            else synthesisEngine.Validate(errors, warnings);
            if (spells == null) errors.Add("Balance: the spellcasting settings are missing.");
            else spells.Validate(errors, warnings);
            if (spelling == null) errors.Add("Balance: the Spelling System settings are missing.");
            else spelling.Validate(errors, warnings);
            if (grimoire == null) errors.Add("Balance: the Grimoire settings are missing.");
            else grimoire.Validate(errors, warnings);
            RuneAcquisitionBalance a = acquisition;
            if (a == null)
                errors.Add("Balance: acquisition settings are missing.");
            else if (a.rarityWeights == null || a.rarityWeights.Count == 0)
                warnings.Add("Balance: no rarity weights (every rune rarity drops as often).");
        }
    }
}
