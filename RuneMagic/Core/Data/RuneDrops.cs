using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    // Where runes and magical energy come from (design spec 3.1, 5.1): monsters carry runes in their bodies or souls; on
    // defeat a rune may drop and the killer gains magical energy. All of it is data on the Rune Magic Database: drop
    // profiles (which monsters carry which runes) and the acquisition balance (how species, rank, alignment, rarity, dungeon
    // difficulty, boss status and special properties change the drops).

    /// <summary>How rare a monster is for rune drops (Auto = from the dungeon placement: normal, elite or boss).</summary>
    public enum MonsterRarity
    {
        Auto,
        Normal,
        Elite,
        Rare,
        Boss,
    }

    /// <summary>Which kills roll rune drops.</summary>
    public enum RuneDropKillers
    {
        /// <summary>Only kills by rune users (Mages): other classes never see runes they cannot use.</summary>
        RuneUsers,
        /// <summary>Kills by any player: runes drop for everyone and can be traded or sold to Mages.</summary>
        AnyPlayer,
    }

    /// <summary>One rune a profile can drop, and how often compared with its other runes.</summary>
    [Serializable]
    public class RuneDropEntry
    {
        [Tooltip("Rune id (wind, fire...).")]
        public string runeId = "";
        [Tooltip("Relative weight among the profile's runes.")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("Lowest rank this entry drops at (0 = no limit).")]
        [Min(0)] public int minRank = 0;
        [Tooltip("Highest rank this entry drops at (0 = no limit).")]
        [Min(0)] public int maxRank = 0;

        public RuneDropEntry() { }

        public RuneDropEntry(string runeId, float weight, int minRank = 0, int maxRank = 0)
        {
            this.runeId = runeId;
            this.weight = weight;
            this.minRank = minRank;
            this.maxRank = maxRank;
        }
    }

    /// <summary>
    /// The runes a kind of monster carries (design spec 3.1): matched to monsters by words of their species, name or faction
    /// and by their special properties (tags), or named by a monster's Rune Source. The highest-priority match is used;
    /// monsters nothing matches use the default profile.
    /// </summary>
    [Serializable]
    public class RuneDropProfile
    {
        [Tooltip("Stable id (fire_elemental, stone_golem...).")]
        public string id = "";
        public string displayName = "";
        [Tooltip("Matches monsters whose species (mob type), name or faction contains one of these words (whole words, any case).")]
        public List<string> matchWords = new List<string>();
        [Tooltip("Matches monsters with one of these special properties (Rune Source ▸ Tags).")]
        public List<string> matchTags = new List<string>();
        [Tooltip("When several profiles match, the highest priority wins.")]
        public int priority = 0;

        [Header("Drops")]
        [Tooltip("The runes it carries (their alignments: Fire Elemental → Fire, Heat, Energy).")]
        public List<RuneDropEntry> runes = new List<RuneDropEntry>();
        [Tooltip("Chance (0-1) per kill of each possible drop, before the rarity and difficulty multipliers.")]
        [Range(0f, 1f)] public float dropChance = 0.08f;
        [Tooltip("Most runes one kill can drop (before a boss's extra drops).")]
        [Min(0)] public int maxDrops = 1;
        [Tooltip("Rank of its runes before rank bonuses (dungeon difficulty, elite, boss, the monster's rank).")]
        [Min(1)] public int baseRank = 1;
        [Tooltip("Quality (0-100) of its runes before bonuses: a random value in this range.")]
        public Vector2 qualityRange = new Vector2(10f, 55f);
        [Tooltip("Energy a dropped rune already holds, as a fraction (0-1) of its maximum: a random value in this range.")]
        public Vector2 startingEnergy = new Vector2(0f, 0.1f);

        [Header("Magical energy")]
        [Tooltip("Magical energy the killer gains (design spec 5.1), before multipliers.")]
        [Min(0f)] public float energy = 100f;

        public string Name => string.IsNullOrEmpty(displayName) ? id : displayName;
    }

    /// <summary>A rarity's weight on which runes drop: common runes are found more often than legendary ones.</summary>
    [Serializable]
    public class RarityWeight
    {
        public RuneRarity rarity;
        [Tooltip("Weight against the other rarities for ordinary monsters.")]
        [Min(0f)] public float normal = 1f;
        [Tooltip("Weight for rare monsters and bosses (they carry unusual, powerful runes: design spec 3.1).")]
        [Min(0f)] public float rareMonsters = 1f;

        public RarityWeight() { }

        public RarityWeight(RuneRarity rarity, float normal, float rareMonsters)
        {
            this.rarity = rarity;
            this.normal = normal;
            this.rareMonsters = rareMonsters;
        }
    }

    /// <summary>How runes and magical energy are obtained, absorbed and released (design spec 3, 4.2, 5).</summary>
    [Serializable]
    public class RuneAcquisitionBalance
    {
        [Header("Who gets drops")]
        [Tooltip("Which kills roll rune drops. Energy always goes to the killer only, if a rune user (decision D5).")]
        public RuneDropKillers dropKillers = RuneDropKillers.RuneUsers;
        [Tooltip("Profile of monsters no profile matches (empty = they drop nothing).")]
        public string defaultProfile = "generic";

        [Header("Drop chance")]
        [Min(0f)] public float globalChanceMultiplier = 1f;
        [Tooltip("Each dungeon difficulty tier adds this share to the chance (0.1 = +10% per tier).")]
        [Min(0f)] public float chancePerTier = 0.1f;
        [Min(0f)] public float eliteChanceMultiplier = 2f;
        [Min(0f)] public float rareChanceMultiplier = 3f;
        [Min(0f)] public float bossChanceMultiplier = 5f;
        [Tooltip("A boss always drops at least one rune.")]
        public bool bossAlwaysDrops = true;
        [Tooltip("Extra drops a boss can have above its profile's Max Drops.")]
        [Min(0)] public int bossExtraDrops = 1;

        [Header("Which runes")]
        [Tooltip("How often each rune rarity drops, for ordinary and for rare monsters / bosses.")]
        public List<RarityWeight> rarityWeights = new List<RarityWeight>();
        [Tooltip("Runes of the monster's own magical alignment (Rune Source ▸ Alignments) weigh this many times more.")]
        [Min(0f)] public float alignmentWeightMultiplier = 3f;
        [Tooltip("Runes of the monster's alignment that its profile does not list can drop too, with this weight.")]
        [Min(0f)] public float alignmentExtraWeight = 1f;

        [Header("Rank")]
        [Tooltip("Each dungeon difficulty tier adds this chance (0-1) of one rank more.")]
        [Range(0f, 1f)] public float rankUpChancePerTier = 0.15f;
        [Min(0)] public int eliteRankBonus = 0;
        [Min(0)] public int rareRankBonus = 1;
        [Min(0)] public int bossRankBonus = 1;
        [Tooltip("Highest rank a drop can have.")]
        [Min(1)] public int maxDropRank = 5;

        [Header("Quality")]
        [Tooltip("Quality added per dungeon difficulty tier.")]
        [Min(0f)] public float qualityPerTier = 4f;
        [Min(0f)] public float eliteQualityBonus = 5f;
        [Min(0f)] public float rareQualityBonus = 10f;
        [Min(0f)] public float bossQualityBonus = 15f;

        [Header("Magical energy from kills")]
        [Tooltip("Each dungeon difficulty tier adds this share to the energy (0.25 = +25% per tier).")]
        [Min(0f)] public float energyPerTier = 0.25f;
        [Min(0f)] public float eliteEnergyMultiplier = 2f;
        [Min(0f)] public float rareEnergyMultiplier = 3f;
        [Min(0f)] public float bossEnergyMultiplier = 6f;
        [Tooltip("The hub world zone's reward multiplier (richer far from the city) scales the energy.")]
        public bool useZoneRewardMultiplier = true;
        [Min(0f)] public float globalEnergyMultiplier = 1f;
        [Tooltip("Most magical energy that can wait unassigned (0 = no limit); more is lost.")]
        [Min(0f)] public float maxPendingEnergy = 0f;

        [Header("Absorption")]
        [Tooltip("Soul Damage per unit of alignment conflict when absorbing a rune whose alignment conflicts with runes held " +
                 "(kind incompatible_absorption; design spec 14.1). 0 = absorbing is always safe.")]
        [Min(0f)] public float conflictDamage = 25f;

        [Header("Release (design spec 4.2, decision D12)")]
        [Tooltip("Runes can be released from the soul (their energy is lost).")]
        public bool allowRelease = true;
        [Tooltip("Releasing is allowed in combat.")]
        public bool releaseInCombat = false;

        [Header("Rune items")]
        [Tooltip("Weight of one rune in the inventory.")]
        [Min(0f)] public float itemWeight = 0.1f;
        [Tooltip("How many identical runes stack in one slot.")]
        [Min(1)] public int itemStack = 5;
        [Tooltip("Merchant price of a rune per rank (× rarity: common 1, uncommon 2, rare 4, epic 8, legendary 16).")]
        [Min(0f)] public float itemPricePerRank = 60f;
        [Tooltip("Merchants buy runes.")]
        public bool itemsCanBeSold = true;
    }
}
