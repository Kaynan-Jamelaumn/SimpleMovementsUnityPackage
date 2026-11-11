using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    // Concept discovery (design spec 8, 17): the player finds a knowledge source in a dungeon, plays a minigame, answers a
    // riddle about a hidden concept. A correct answer unlocks the concept for good; a wrong one burns that source for that
    // concept. Everything that tunes it is data: the tiers (difficulty), the sources, the riddles.

    /// <summary>What kind of place a knowledge source is (flavour and filtering; they all work the same).</summary>
    public enum DiscoverySourceKind
    {
        Inscription,
        Artifact,
        SealedChamber,
        Library,
        Shrine,
        Other,
    }

    /// <summary>
    /// A difficulty band (design spec 17.2: Beginner, Intermediate, Advanced, Expert, Legendary). A source's tier comes
    /// from the dungeon it is in; the band sets the minigame and the riddle, and which concept rarities it can teach.
    /// </summary>
    [Serializable]
    public class DiscoveryTierDef
    {
        public string id = "";
        public string name = "";
        [Tooltip("The lowest dungeon tier (DungeonSpawned.tier) that makes a source of this band; the highest band whose value is reached is used.")]
        [Min(0)] public int minDungeonTier = 0;

        [Header("Minigame (rune tracing)")]
        [Tooltip("Glyphs to seal, one after another.")]
        [Range(1, 16)] public int minigameNodes = 3;
        [Tooltip("Laps of the marker per second.")]
        [Min(0.05f)] public float minigameSpeed = 0.35f;
        [Tooltip("Width of each target as a fraction of the circle (0-1); concepts of higher complexity shrink it.")]
        [Range(0.02f, 0.5f)] public float nodeWidth = 0.18f;
        [Tooltip("Share of the glyphs (with partial credit for near misses) needed to pass (0-1).")]
        [Range(0f, 1f)] public float minScore = 0.6f;
        [Tooltip("Seconds the minigame may take.")]
        [Min(5f)] public float timeLimit = 40f;
        [Tooltip("Tries at the minigame per source and concept; failing the last one burns the source for that concept.")]
        [Min(1)] public int attempts = 2;

        [Header("Riddle")]
        [Tooltip("Questions to answer, all correctly (multi-step challenges at the advanced bands). A wrong answer ends it.")]
        [Range(1, 5)] public int steps = 1;
        [Tooltip("Answers to choose from.")]
        [Range(2, 8)] public int options = 4;
        [Tooltip("Clues of a generated riddle (fewer clues = harder).")]
        [Range(1, 6)] public int clues = 3;

        [Header("Which concepts")]
        [Tooltip("Relative chance of teaching a concept of each rarity (Common, Uncommon, Rare, Epic, Legendary) at this band; 0 = never.")]
        public List<float> rarityWeights = new List<float> { 1f, 1f, 0f, 0f, 0f };

        public string Name => string.IsNullOrEmpty(name) ? id : name;

        /// <summary>Weight of concepts of <paramref name="rarity"/> at this band.</summary>
        public float WeightOf(RuneRarity rarity)
        {
            int i = (int)rarity;
            return rarityWeights != null && i >= 0 && i < rarityWeights.Count ? Math.Max(0f, rarityWeights[i]) : 0f;
        }
    }

    /// <summary>The balance of concept discovery (design spec 17), part of the Rune Magic balance.</summary>
    [Serializable]
    public class DiscoveryBalance
    {
        [Tooltip("The difficulty bands, easiest first.")]
        public List<DiscoveryTierDef> tiers = new List<DiscoveryTierDef>();
        [Tooltip("Only concepts of runes the player has absorbed can be discovered (off: any rune's concepts).")]
        public bool onlyRunesInSoul = true;
        [Tooltip("Leaving a riddle unanswered (walking away, closing the window, running out of time) counts as a wrong answer: " +
                 "otherwise a player could look at the riddle and come back for another.")]
        public bool abandonBurns = true;
        [Tooltip("Seconds a whole discovery may take before it is abandoned.")]
        [Min(30f)] public float sessionSeconds = 240f;
        [Tooltip("Metres the player may move away from the source before the discovery is abandoned (0 = no limit).")]
        [Min(0f)] public float leaveDistance = 6f;
        [Tooltip("Concepts of higher complexity (0-1) make the targets of the minigame narrower by up to this share.")]
        [Range(0f, 0.9f)] public float complexityNarrowing = 0.35f;
        [Tooltip("Least seconds between two seals (a hand cannot be faster).")]
        [Min(0f)] public float minPressInterval = 0.25f;
        [Tooltip("Seconds a client's clock may run ahead of the server's before its seals are refused.")]
        [Min(0f)] public float clockSlack = 1.5f;
        [Tooltip("Riddles already seen are avoided while others remain.")]
        public bool avoidSeenRiddles = true;
        [Tooltip("How many discovery attempts the saved log keeps.")]
        [Range(0, 200)] public int logLength = 30;

        /// <summary>The band for a dungeon of tier <paramref name="dungeonTier"/> (the highest whose minimum is reached).</summary>
        public int TierIndexFor(int dungeonTier)
        {
            int best = -1;
            int bestMin = int.MinValue;
            for (int i = 0; i < tiers.Count; i++)
            {
                DiscoveryTierDef t = tiers[i];
                if (t != null && t.minDungeonTier <= dungeonTier && t.minDungeonTier >= bestMin) { best = i; bestMin = t.minDungeonTier; }
            }
            if (best >= 0) return best;
            for (int i = 0; i < tiers.Count; i++)
                if (tiers[i] != null) return i;
            return -1;
        }

        public DiscoveryTierDef Tier(int index) => index >= 0 && index < tiers.Count ? tiers[index] : null;
    }

    /// <summary>
    /// A kind of knowledge source (design spec 17.1: an inscription, a magical artifact, a sealed chamber, a library). The
    /// object in the dungeon names one by id. A wrong answer burns the source <i>definition</i> for that concept and
    /// player (decision D7): another source of that concept has to be found.
    /// </summary>
    [Serializable]
    public class DiscoverySourceDef
    {
        [Tooltip("Stable id (ancient_inscription...). Burned sources are saved by it.")]
        public string id = "";
        public string displayName = "";
        public DiscoverySourceKind kind = DiscoverySourceKind.Inscription;
        [TextArea(1, 3)] public string description = "";
        [Tooltip("Runes whose concepts it can teach (empty = any rune).")]
        public List<string> runes = new List<string>();
        [Tooltip("Concept ids it can teach, in addition to its runes' concepts.")]
        public List<string> concepts = new List<string>();
        [Tooltip("Lowest difficulty band index it works at (0 = Beginner).")]
        [Min(0)] public int minTier = 0;
        [Tooltip("Highest band index (-1 = no limit).")]
        public int maxTier = -1;
        [Tooltip("The minigame (id of a registered minigame; empty = rune tracing).")]
        public string minigame = "";
        [Tooltip("Shown when the player first approaches.")]
        [TextArea(1, 3)] public string intro = "";

        public string Name => string.IsNullOrEmpty(displayName) ? id : displayName;

        public bool WorksAt(int tierIndex) => tierIndex >= minTier && (maxTier < 0 || tierIndex <= maxTier);
    }

    /// <summary>
    /// A riddle written for a concept (design spec 17.3): its text, and optionally the wrong answers to offer. Without
    /// riddles a concept gets generated ones, built from what its aspects mean. Riddles must reason about magical
    /// principles, never ask for memorization.
    /// </summary>
    [Serializable]
    public class RiddleDef
    {
        [Tooltip("Stable id (wind.pressure.1). The riddles a player has seen are saved by it.")]
        public string id = "";
        [Tooltip("The concept it is about (the answer): wind.pressure.")]
        public string concept = "";
        [TextArea(3, 8)] public string text = "";
        [Tooltip("Wrong answers to offer, as concept ids (empty = the rune's other concepts). Each must be a concept.")]
        public List<string> distractors = new List<string>();
        [Tooltip("Lowest difficulty band index it is used at.")]
        [Min(0)] public int minTier = 0;
        [Tooltip("Highest band index (-1 = no limit).")]
        public int maxTier = -1;

        public bool WorksAt(int tierIndex) => tierIndex >= minTier && (maxTier < 0 || tierIndex <= maxTier);
    }
}
