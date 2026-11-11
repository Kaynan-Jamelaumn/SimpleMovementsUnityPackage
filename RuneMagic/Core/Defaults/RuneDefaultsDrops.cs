using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    // Built-in rune drops (design spec 3.1). The first six profiles are the spec's illustrative table (Fire Elemental →
    // Fire, Heat, Energy...); the others are proposals so that ordinary monsters carry runes too. Monsters are matched by
    // words of their species (mob type), name or faction; a Rune Source on a monster can name a profile, its alignments,
    // rank, rarity and special properties. Every number is a proposal: tune them on the database.
    public static partial class RuneDefaults
    {
        private static RuneDropProfile Profile(string id, string name, int priority, float chance, float energy, string words, params RuneDropEntry[] runes)
        {
            var p = new RuneDropProfile
            {
                id = id,
                displayName = name,
                priority = priority,
                dropChance = chance,
                energy = energy,
                matchWords = new List<string>(),
                runes = new List<RuneDropEntry>(runes),
            };
            foreach (string w in words.Split(' '))
                if (w.Length > 0) p.matchWords.Add(w);
            return p;
        }

        private static RuneDropEntry E(string rune, float weight, int minRank = 0, int maxRank = 0) => new RuneDropEntry(rune, weight, minRank, maxRank);

        public static List<RuneDropProfile> DropProfiles()
        {
            var list = new List<RuneDropProfile>
            {
                // ---- design spec 3.1 (illustrative)
                Profile("fire_elemental", "Fire Elemental", 10, 0.12f, 150f, "fire flame ember inferno salamander magma lava blaze",
                    E("fire", 5f), E("heat", 3f), E("energy", 2f)),
                Profile("wind_beast", "Wind Beast", 10, 0.12f, 140f, "wind storm gale harpy griffin gryphon roc tempest cyclone",
                    E("wind", 5f), E("gale", 3f), E("vibration", 2f)),
                Profile("stone_golem", "Stone Golem", 10, 0.12f, 160f, "golem stone rock earth gargoyle boulder granite",
                    E("earth", 5f), E("matter", 3f), E("armor", 2f)),
                Profile("shadow_creature", "Shadow Creature", 10, 0.12f, 150f, "shadow shade wraith specter spectre phantom ghost dark umbral",
                    E("shadow", 5f), E("mind", 3f), E("absorption", 2f)),
                Profile("crystal_guardian", "Crystal Guardian", 10, 0.12f, 170f, "crystal gem prism quartz crystalline",
                    E("crystal", 5f), E("space", 3f), E("reflection", 2f)),
                // Ancient dragons: Fire, Gravity and rare higher-rank runes.
                Profile("ancient_dragon", "Ancient Dragon", 20, 0.5f, 1500f, "dragon drake wyrm wyvern",
                    E("fire", 4f, 2), E("gravity", 3f, 2), E("time", 0.3f, 2), E("soul", 0.3f, 2)),

                // ---- proposals for the other monsters
                Profile("beast", "Beast", 5, 0.06f, 80f, "wolf bear boar beast hound cat lion tiger rat bat deer stag animal",
                    E("momentum", 3f), E("friction", 2f), E("scent", 2f), E("blood", 1f)),
                Profile("undead", "Undead", 8, 0.1f, 130f, "skeleton zombie undead lich ghoul mummy revenant bone",
                    E("decay", 4f), E("soul", 1f), E("shadow", 2f), E("blood", 1f)),
                Profile("aquatic", "Aquatic", 8, 0.08f, 110f, "water fish shark naga siren crab kraken frost ice yeti",
                    E("water", 4f), E("ice", 3f), E("mist", 2f)),
                Profile("venomous", "Venomous", 8, 0.08f, 110f, "spider scorpion snake serpent viper wasp insect poison venom",
                    E("poison", 4f), E("decay", 2f), E("friction", 1f)),
                Profile("sylvan", "Sylvan", 8, 0.08f, 110f, "treant tree plant dryad vine thorn fungus mushroom",
                    E("plant", 4f), E("growth", 3f), E("restoration", 1f)),
                Profile("construct", "Construct", 8, 0.1f, 140f, "construct automaton machine metal iron steel clockwork",
                    E("metal", 4f), E("magnetism", 2f), E("armor", 2f), E("lightning", 1f)),
                Profile("storm_spirit", "Storm Spirit", 9, 0.12f, 150f, "lightning thunder spark static",
                    E("lightning", 5f), E("energy", 3f), E("sound", 1f)),
                Profile("humanoid", "Humanoid", 4, 0.05f, 90f, "bandit cultist mage wizard knight warrior soldier goblin orc kobold",
                    E("mind", 2f), E("energy", 2f), E("sound", 1f), E("binding", 1f)),
                // Everything else: the basic elemental runes.
                Profile("generic", "Any monster", 0, 0.05f, 100f, "",
                    E("wind", 2f), E("fire", 2f), E("water", 2f), E("earth", 2f), E("energy", 1f)),
            };
            RuneDropProfile dragon = list.Find(p => p.id == "ancient_dragon");
            dragon.baseRank = 2;
            dragon.maxDrops = 2;
            dragon.qualityRange = new Vector2(40f, 90f);
            dragon.startingEnergy = new Vector2(0.1f, 0.4f);
            return list;
        }

        /// <summary>How often each rarity drops for ordinary monsters and for rare monsters / bosses.</summary>
        public static List<RarityWeight> RarityWeights() => new List<RarityWeight>
        {
            new RarityWeight(RuneRarity.Common, 1f, 1f),
            new RarityWeight(RuneRarity.Uncommon, 0.6f, 1f),
            new RarityWeight(RuneRarity.Rare, 0.25f, 0.8f),
            new RarityWeight(RuneRarity.Epic, 0.08f, 0.4f),
            new RarityWeight(RuneRarity.Legendary, 0.02f, 0.15f),
        };

        public static RuneAcquisitionBalance Acquisition() => new RuneAcquisitionBalance
        {
            dropKillers = RuneDropKillers.RuneUsers,
            defaultProfile = "generic",
            globalChanceMultiplier = 1f,
            chancePerTier = 0.1f,
            eliteChanceMultiplier = 2f,
            rareChanceMultiplier = 3f,
            bossChanceMultiplier = 5f,
            bossAlwaysDrops = true,
            bossExtraDrops = 1,
            rarityWeights = RarityWeights(),
            alignmentWeightMultiplier = 3f,
            alignmentExtraWeight = 1f,
            rankUpChancePerTier = 0.15f,
            eliteRankBonus = 0,
            rareRankBonus = 1,
            bossRankBonus = 1,
            maxDropRank = 5,
            qualityPerTier = 4f,
            eliteQualityBonus = 5f,
            rareQualityBonus = 10f,
            bossQualityBonus = 15f,
            energyPerTier = 0.25f,
            eliteEnergyMultiplier = 2f,
            rareEnergyMultiplier = 3f,
            bossEnergyMultiplier = 6f,
            useZoneRewardMultiplier = true,
            globalEnergyMultiplier = 1f,
            maxPendingEnergy = 0f,
            conflictDamage = 25f,
            allowRelease = true,
            releaseInCombat = false,
            itemWeight = 0.1f,
            itemStack = 5,
            itemPricePerRank = 60f,
            itemsCanBeSold = true,
        };
    }
}
