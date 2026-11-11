using System;
using System.Collections.Generic;
using System.Text;

namespace RuneMagic
{
    /// <summary>What is known about a defeated monster for rune drops (design spec 3.1). Built by the runtime from the mob.</summary>
    public struct RuneDropContext
    {
        /// <summary>The monster's species (its mob type).</summary>
        public string species;
        /// <summary>Its shown name.</summary>
        public string displayName;
        /// <summary>Its faction's name.</summary>
        public string faction;
        /// <summary>A drop profile named by the monster itself (Rune Source); empty = matched by species, name and tags.</summary>
        public string profileId;
        /// <summary>The monster's magical alignments (Rune Source): runes of these alignments drop more often.</summary>
        public IList<string> alignments;
        /// <summary>Its special properties (Rune Source ▸ Tags): they match profiles too.</summary>
        public IList<string> tags;
        /// <summary>The monster's rank (0 = unknown): its runes are at least this rank.</summary>
        public int monsterRank;
        /// <summary>Dungeon difficulty tier (0 outside dungeons).</summary>
        public int tier;
        public MonsterRarity rarity;
        public float chanceMultiplier;
        public float energyMultiplier;
        /// <summary>The hub world zone's reward multiplier (0 or 1 = none).</summary>
        public float zoneRewardMultiplier;

        public static RuneDropContext For(string species, int tier = 0, MonsterRarity rarity = MonsterRarity.Normal) => new RuneDropContext
        {
            species = species ?? "", displayName = "", faction = "", profileId = "", tier = Math.Max(0, tier), rarity = rarity,
            chanceMultiplier = 1f, energyMultiplier = 1f, zoneRewardMultiplier = 1f,
        };

        public bool IsElite => rarity == MonsterRarity.Elite;
        public bool IsRare => rarity == MonsterRarity.Rare;
        public bool IsBoss => rarity == MonsterRarity.Boss;
        /// <summary>Rare monsters and bosses carry unusual, powerful runes.</summary>
        public bool IsRareMonster => rarity == MonsterRarity.Rare || rarity == MonsterRarity.Boss;
    }

    /// <summary>One rune that dropped (not absorbed yet).</summary>
    public struct RuneDrop
    {
        public string runeId;
        public int rank;
        /// <summary>0-100, a whole number.</summary>
        public float quality;
        /// <summary>Energy it already holds, a whole number.</summary>
        public float energy;

        public override string ToString() => $"{runeId} r{rank} q{quality:0} e{energy:0}";
    }

    /// <summary>A rune a profile can drop with its final weight (for tools and tests).</summary>
    public struct RuneCandidate
    {
        public RuneDef rune;
        public RuneDropEntry entry;
        public float weight;
    }

    /// <summary>
    /// Rune drops and magical-energy rewards (design spec 3.1, 5.1): which profile a monster has (its species, name,
    /// faction, special properties, or its own choice), which runes drop, at what rank, quality and starting energy, and
    /// how much energy the killer gains. Monster rank, alignment, rarity, dungeon difficulty and boss status all count.
    /// Pure and deterministic for a given random generator.
    /// </summary>
    public static class DropRules
    {
        // ------------------------------------------------------------------ profile
        /// <summary>The drop profile of a monster (null when none applies: it carries no runes).</summary>
        public static RuneDropProfile SelectProfile(RuneCatalog c, RuneDropContext ctx)
        {
            if (c == null) return null;
            if (!string.IsNullOrEmpty(ctx.profileId))
            {
                RuneDropProfile named = c.DropProfile(ctx.profileId);
                if (named != null) return named;
            }
            HashSet<string> words = Words(ctx.species, ctx.displayName, ctx.faction);
            RuneDropProfile best = null;
            foreach (RuneDropProfile p in c.DropProfiles)
            {
                if (p == null || !Matches(p, words, ctx.tags)) continue;
                if (best == null || p.priority > best.priority) best = p;
            }
            if (best != null) return best;
            string fallback = c.Balance.acquisition != null ? c.Balance.acquisition.defaultProfile : "";
            return string.IsNullOrEmpty(fallback) ? null : c.DropProfile(fallback);
        }

        /// <summary>Does <paramref name="p"/> match a monster with these <paramref name="words"/> and <paramref name="tags"/>?</summary>
        public static bool Matches(RuneDropProfile p, HashSet<string> words, IList<string> tags)
        {
            if (p == null) return false;
            if (p.matchWords != null && words != null)
                foreach (string w in p.matchWords)
                {
                    if (string.IsNullOrWhiteSpace(w)) continue;
                    string k = w.Trim().ToLowerInvariant();
                    if (words.Contains(k) || words.Contains(k + "s") || words.Contains(k + "es")) return true;
                }
            if (p.matchTags != null && tags != null)
                foreach (string t in p.matchTags)
                    if (!string.IsNullOrWhiteSpace(t) && Contains(tags, t)) return true;
            return false;
        }

        /// <summary>The lowercase words of names: "FireElemental (Elite)" → fire, elemental, elite.</summary>
        public static HashSet<string> Words(params string[] texts)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (texts == null) return set;
            var sb = new StringBuilder();
            foreach (string text in texts)
            {
                if (string.IsNullOrEmpty(text)) continue;
                for (int i = 0; i <= text.Length; i++)
                {
                    char ch = i < text.Length ? text[i] : ' ';
                    bool boundary = !char.IsLetterOrDigit(ch) ||
                                    (char.IsUpper(ch) && sb.Length > 0 && char.IsLower(text[i - 1]));
                    if (boundary && sb.Length > 0)
                    {
                        set.Add(sb.ToString().ToLowerInvariant());
                        sb.Clear();
                    }
                    if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                }
            }
            return set;
        }

        // ------------------------------------------------------------------ chance and count
        /// <summary>Chance (0-1) of each possible drop of a monster with profile <paramref name="p"/>.</summary>
        public static float DropChance(RuneAcquisitionBalance a, RuneDropProfile p, RuneDropContext ctx)
        {
            if (a == null || p == null) return 0f;
            float chance = p.dropChance * a.globalChanceMultiplier * (1f + a.chancePerTier * Math.Max(0, ctx.tier));
            chance *= ctx.IsBoss ? a.bossChanceMultiplier : ctx.IsRare ? a.rareChanceMultiplier : ctx.IsElite ? a.eliteChanceMultiplier : 1f;
            chance *= Positive(ctx.chanceMultiplier, 1f);
            return Clamp01(chance);
        }

        /// <summary>Most runes one kill can drop.</summary>
        public static int MaxDrops(RuneAcquisitionBalance a, RuneDropProfile p, RuneDropContext ctx)
        {
            if (a == null || p == null) return 0;
            return Math.Max(0, p.maxDrops + (ctx.IsBoss ? a.bossExtraDrops : 0));
        }

        // ------------------------------------------------------------------ rolling
        /// <summary>The runes a defeated monster drops (empty for most kills).</summary>
        public static List<RuneDrop> Roll(RuneCatalog c, RuneDropContext ctx, Random rng)
        {
            var drops = new List<RuneDrop>();
            if (c == null || rng == null) return drops;
            RuneDropProfile p = SelectProfile(c, ctx);
            RuneAcquisitionBalance a = c.Balance.acquisition;
            if (p == null || a == null) return drops;
            float chance = DropChance(a, p, ctx);
            int max = MaxDrops(a, p, ctx);
            for (int i = 0; i < max; i++)
                if (rng.NextDouble() < chance && RollOne(c, p, ctx, rng, out RuneDrop d))
                    drops.Add(d);
            if (drops.Count == 0 && ctx.IsBoss && a.bossAlwaysDrops && max > 0 && RollOne(c, p, ctx, rng, out RuneDrop guaranteed))
                drops.Add(guaranteed);
            return drops;
        }

        /// <summary>One rune of profile <paramref name="p"/>: which rune, its rank, quality and starting energy.</summary>
        public static bool RollOne(RuneCatalog c, RuneDropProfile p, RuneDropContext ctx, Random rng, out RuneDrop drop)
        {
            drop = default;
            List<RuneCandidate> candidates = Candidates(c, p, ctx);
            float total = 0f;
            foreach (RuneCandidate k in candidates) total += k.weight;
            if (total <= 0f) return false;
            double pick = rng.NextDouble() * total;
            RuneCandidate chosen = candidates[candidates.Count - 1];
            foreach (RuneCandidate k in candidates)
            {
                pick -= k.weight;
                if (pick < 0) { chosen = k; break; }
            }
            RuneAcquisitionBalance a = c.Balance.acquisition;
            int rank = RollRank(a, p, chosen.entry, chosen.rune, ctx, rng);
            float quality = RollQuality(a, p, ctx, rng);
            float max = c.Balance.MaxEnergy(rank, quality);
            float lo = Clamp01(Math.Min(p.startingEnergy.x, p.startingEnergy.y)), hi = Clamp01(Math.Max(p.startingEnergy.x, p.startingEnergy.y));
            float energy = (float)Math.Round(max * (lo + (hi - lo) * rng.NextDouble()));
            drop = new RuneDrop { runeId = chosen.rune.id, rank = rank, quality = quality, energy = Math.Min(energy, (float)Math.Floor(max)) };
            return true;
        }

        /// <summary>The runes <paramref name="p"/> can drop for this monster, with their weights.</summary>
        public static List<RuneCandidate> Candidates(RuneCatalog c, RuneDropProfile p, RuneDropContext ctx)
        {
            var list = new List<RuneCandidate>();
            if (c == null || p == null) return list;
            RuneAcquisitionBalance a = c.Balance.acquisition;
            float alignmentMultiplier = a != null ? a.alignmentWeightMultiplier : 1f;
            var listed = new HashSet<string>(StringComparer.Ordinal);
            if (p.runes != null)
                foreach (RuneDropEntry e in p.runes)
                {
                    RuneDef def = e != null ? c.Rune(e.runeId) : null;
                    if (def == null || e.weight <= 0f) continue;
                    listed.Add(def.id);
                    float w = e.weight * c.Balance.RarityWeight(def.rarity, ctx.IsRareMonster);
                    if (SharesAlignment(def, ctx.alignments)) w *= alignmentMultiplier;
                    if (w > 0f) list.Add(new RuneCandidate { rune = def, entry = e, weight = w });
                }
            // The monster's own magical alignment: its runes can drop even when the profile does not list them.
            if (ctx.alignments != null && ctx.alignments.Count > 0 && a != null && a.alignmentExtraWeight > 0f)
                foreach (RuneDef def in c.Runes)
                {
                    if (def == null || listed.Contains(def.id) || !SharesAlignment(def, ctx.alignments)) continue;
                    float w = a.alignmentExtraWeight * c.Balance.RarityWeight(def.rarity, ctx.IsRareMonster);
                    if (w > 0f) list.Add(new RuneCandidate { rune = def, entry = null, weight = w });
                }
            return list;
        }

        /// <summary>
        /// The rank of a drop: the profile's base rank (at least the monster's rank), plus the elite / rare / boss bonus,
        /// plus one with a chance growing with the dungeon tier; within the rune's and the entry's ranks and Max Drop Rank.
        /// </summary>
        public static int RollRank(RuneAcquisitionBalance a, RuneDropProfile p, RuneDropEntry entry, RuneDef def, RuneDropContext ctx, Random rng)
        {
            int rank = Math.Max(p != null ? p.baseRank : 1, Math.Max(1, ctx.monsterRank));
            if (a != null)
            {
                rank += ctx.IsBoss ? a.bossRankBonus : ctx.IsRare ? a.rareRankBonus : ctx.IsElite ? a.eliteRankBonus : 0;
                float up = Math.Min(0.95f, Math.Max(0, ctx.tier) * a.rankUpChancePerTier);
                if (up > 0f && rng != null && rng.NextDouble() < up) rank++;
            }
            int lo = Math.Max(def != null ? def.minRank : 1, entry != null && entry.minRank > 0 ? entry.minRank : 1);
            int hi = Math.Min(def != null ? def.maxRank : int.MaxValue, a != null ? a.maxDropRank : int.MaxValue);
            if (entry != null && entry.maxRank > 0) hi = Math.Min(hi, entry.maxRank);
            if (hi < lo) hi = lo;
            return Math.Max(lo, Math.Min(hi, rank));
        }

        /// <summary>The quality (0-100, whole) of a drop: the profile's range raised by the tier and the monster's rarity.</summary>
        public static float RollQuality(RuneAcquisitionBalance a, RuneDropProfile p, RuneDropContext ctx, Random rng)
        {
            float lo = p != null ? Math.Min(p.qualityRange.x, p.qualityRange.y) : 0f;
            float hi = p != null ? Math.Max(p.qualityRange.x, p.qualityRange.y) : 50f;
            if (a != null)
            {
                float bonus = Math.Max(0, ctx.tier) * a.qualityPerTier +
                              (ctx.IsBoss ? a.bossQualityBonus : ctx.IsRare ? a.rareQualityBonus : ctx.IsElite ? a.eliteQualityBonus : 0f);
                lo += bonus;
                hi += bonus;
            }
            double q = lo + (hi - lo) * (rng != null ? rng.NextDouble() : 0.5);
            return (float)Math.Round(Math.Max(0, Math.Min(100, q)));
        }

        // ------------------------------------------------------------------ energy
        /// <summary>Magical energy the killer of this monster gains (whole number; 0 when no profile applies).</summary>
        public static float EnergyReward(RuneCatalog c, RuneDropContext ctx)
        {
            if (c == null) return 0f;
            RuneDropProfile p = SelectProfile(c, ctx);
            RuneAcquisitionBalance a = c.Balance.acquisition;
            if (p == null || a == null) return 0f;
            float e = p.energy * a.globalEnergyMultiplier * (1f + a.energyPerTier * Math.Max(0, ctx.tier));
            e *= ctx.IsBoss ? a.bossEnergyMultiplier : ctx.IsRare ? a.rareEnergyMultiplier : ctx.IsElite ? a.eliteEnergyMultiplier : 1f;
            e *= Positive(ctx.energyMultiplier, 1f);
            if (a.useZoneRewardMultiplier && ctx.zoneRewardMultiplier > 0f) e *= ctx.zoneRewardMultiplier;
            return (float)Math.Round(Math.Max(0f, e));
        }

        // ------------------------------------------------------------------ helpers
        /// <summary>Does <paramref name="def"/> belong to one of <paramref name="alignments"/> (by alignment id or rune id)?</summary>
        public static bool SharesAlignment(RuneDef def, IList<string> alignments)
        {
            if (def == null || alignments == null || alignments.Count == 0) return false;
            if (Contains(alignments, def.id)) return true;
            if (def.alignments != null)
                foreach (string al in def.alignments)
                    if (Contains(alignments, al)) return true;
            return false;
        }

        private static bool Contains(IList<string> list, string value)
        {
            if (list == null || string.IsNullOrWhiteSpace(value)) return false;
            string v = value.Trim();
            foreach (string s in list)
                if (s != null && string.Equals(s.Trim(), v, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static float Positive(float v, float fallback) => float.IsNaN(v) || float.IsInfinity(v) || v < 0f ? fallback : v;
        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
