using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// The built-in Rune Magic content: the runes, alignments, concepts, intentions and desired effects of the design
    /// spec (sections 7-10, verbatim where the spec lists them), proposed concepts for the runes the spec leaves open,
    /// the aspect vocabulary, starter compatibility rules and the balancing tables (spec values, marked illustrative or
    /// proposed there). Used when the project has no Rune Magic Database, and to fill a new one (Tools ▸ SimpleMovements ▸
    /// Project Setup ▸ Create Rune Magic Database). Everything here is data: edit it on the database asset.
    /// </summary>
    public static partial class RuneDefaults
    {
        private static RuneCatalog catalog;

        /// <summary>A new copy of the built-in content and settings.</summary>
        public static RuneMagicData Create()
        {
            var data = new RuneMagicData
            {
                settings = Settings(),
                balance = Balance(),
                aspects = Aspects(),
                alignments = Alignments(),
                runes = Runes(),
                intentions = Intentions(),
                effects = Effects(),
                rules = Rules(),
                dropProfiles = DropProfiles(),
                discoverySources = DiscoverySources(),
                riddles = Riddles(),
                archetypes = Archetypes(),
                synthesisFlaws = SynthesisFlaws(),
                spellForms = SpellForms(),
                spellingRules = SpellingRules(),
            };
            return data;
        }

        /// <summary>The built-in content compiled (shared, read-only).</summary>
        public static RuneCatalog Catalog()
        {
            if (catalog == null)
                catalog = RuneCatalog.Compile(Create());
            return catalog;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => catalog = null;

        // ------------------------------------------------------------------ settings and balance
        public static RuneMagicSettings Settings() => new RuneMagicSettings
        {
            enabled = true,
            runeUserClassNames = new List<string> { "Mage" },
            matchClassArchetypes = true,
            essenceRollsOnRuneUserKills = true,
            addMissingSoulComponents = true,
            debugPanel = true,
            meditateAction = "Meditate",
            runeSoulAction = "RuneSoul",
            debugPanelAction = "RuneMagicDebug",
        };

        public static RuneBalance Balance()
        {
            var b = new RuneBalance
            {
                // Design spec 4.1 (proposed). The spec names the stages, not their levels: the levels are placeholders.
                capacityStages = new List<CapacityStage>
                {
                    new CapacityStage("Beginning", 1, 7),
                    new CapacityStage("Intermediate", 20, 14),
                    new CapacityStage("Advanced", 40, 21),
                    new CapacityStage("Endgame", 60, 28),
                },
                // Design spec 5.3 (illustrative).
                energyByRank = new List<RankEnergy>
                {
                    new RankEnergy(1, 1000f),
                    new RankEnergy(2, 5000f),
                    new RankEnergy(3, 25000f),
                    new RankEnergy(4, 125000f),
                    new RankEnergy(5, 625000f),
                },
                energyGrowthBeyondTable = 5f,
                qualityAffectsMaxEnergy = false,
                maxEnergyBonusAtFullQuality = 0f,
                // Rank 1 is "Greater" (Greater Wind, Greater Fire in the spec); the others are placeholders to rename.
                rankTitles = new List<RankTitle>
                {
                    new RankTitle(1, "Greater"),
                    new RankTitle(2, "Grand"),
                    new RankTitle(3, "Exalted"),
                    new RankTitle(4, "Mythic"),
                    new RankTitle(5, "Transcendent"),
                },
                runesPerRankAdvancement = 7,
                qualityTiers = new List<QualityTier>
                {
                    new QualityTier("Crude", 0f),
                    new QualityTier("Flawed", 15f),
                    new QualityTier("Common", 35f),
                    new QualityTier("Fine", 55f),
                    new QualityTier("Superior", 75f),
                    new QualityTier("Exceptional", 90f),
                    new QualityTier("Flawless", 100f),
                },
                // Design spec 12.2 (proposed).
                synthesis = new SynthesisThresholds { perfect = 100f, successful = 70f, flawed = 30f },
                // Design spec 13.3: severity scales with how low the score is and with the risk.
                failureSeverities = new List<FailureSeverity>
                {
                    new FailureSeverity("Minor", 20f, 0f, 0.25f, false),
                    new FailureSeverity("Moderate", 10f, 0f, 0.6f, false),
                    new FailureSeverity("Severe", 0f, 0f, 1f, false),
                    new FailureSeverity("Catastrophic", 0f, 0.7f, 1f, true),
                },
                maxSoulIntegrity = 100f,
                soulConditions = SoulConditions(),
                soulDamageKinds = SoulDamageKinds(),
                soulCollapse = SoulCollapse(),
                soulRecovery = SoulRecovery(),
                acquisition = Acquisition(),
                discovery = Discovery(),
                synthesisEngine = SynthesisEngine(),
                spells = Spells(),
                spelling = Spelling(),
                grimoire = Grimoire(),
            };
            return b;
        }

        /// <summary>Design spec 14.3 (proposed thresholds) with the casting debuffs of 16.2 mapped onto them.</summary>
        public static List<SoulCondition> SoulConditions()
        {
            List<SoulCondition> list = SoulConditionsBase();
            foreach (SoulCondition c in list) SpellingOfCondition(c);
            return list;
        }

        private static List<SoulCondition> SoulConditionsBase() => new List<SoulCondition>
        {
            new SoulCondition
            {
                id = "stable", name = "Stable", minPercent = 80f, castingDebuff = "",
                effects = "No penalties.",
            },
            new SoulCondition
            {
                id = "strained", name = "Strained", minPercent = 60f, castingDebuff = "Mild Strain",
                effects = "Minor synthesis instability; slightly longer casting.",
                synthesisStability = 0.95f, castTimeMultiplier = 1.1f,
            },
            new SoulCondition
            {
                id = "damaged", name = "Damaged", minPercent = 40f, castingDebuff = "Moderate Strain",
                effects = "Higher synthesis failure chance; weaker, costlier spells.",
                synthesisFailureChance = 0.1f, synthesisStability = 0.9f,
                castTimeMultiplier = 1.15f, powerMultiplier = 0.85f, energyCostMultiplier = 1.15f,
            },
            new SoulCondition
            {
                id = "critical", name = "Critical", minPercent = 20f, castingDebuff = "Severe Strain",
                effects = "Synthesis significantly restricted; spells may behave unstably.",
                synthesisFailureChance = 0.25f, synthesisStability = 0.75f, maxSynthesisRunes = 3, maxSynthesisComplexity = 0.7f,
                castTimeMultiplier = 1.25f, powerMultiplier = 0.75f, energyCostMultiplier = 1.25f, instabilityChance = 0.15f,
                soulDamageMultiplier = 1.25f,
            },
            new SoulCondition
            {
                id = "near_collapse", name = "Near Collapse", minPercent = 0f, castingDebuff = "Critical Strain",
                effects = "Synthesis disabled; casting heavily penalized; only the simplest spells work.",
                synthesisAllowed = false, synthesisStability = 0.5f, maxSynthesisComplexity = 0f,
                castTimeMultiplier = 1.5f, powerMultiplier = 0.6f, energyCostMultiplier = 1.5f, instabilityChance = 0.3f,
                maxCastableRank = 1, soulDamageMultiplier = 1.5f,
            },
            new SoulCondition
            {
                id = "collapse", name = "Soul Collapse", minPercent = 0f, isCollapse = true, castingDebuff = "Soul Collapse",
                effects = "The soul has collapsed: no synthesis, no casting, severe consequences until it recovers.",
                synthesisAllowed = false, castingAllowed = false, synthesisStability = 0f, maxSynthesisComplexity = 0f, powerMultiplier = 0f,
                soulDamageMultiplier = 2f,
            },
        };

        // ------------------------------------------------------------------ aspects
        public static List<AspectDef> Aspects() => new List<AspectDef>
        {
            new AspectDef("force", "Force", "Raw push and impact."),
            new AspectDef("pressure", "Pressure", "Force held against a boundary, waiting to be released."),
            new AspectDef("containment", "Containment", "Holding, enclosing, keeping in or out."),
            new AspectDef("expansion", "Expansion", "Spreading outward, growing in size or area."),
            new AspectDef("compression", "Compression", "Concentrating into less space."),
            new AspectDef("motion", "Motion", "Movement, flow and speed."),
            new AspectDef("rotation", "Rotation", "Spinning, vortices and orbits."),
            new AspectDef("thermal", "Thermal", "Heat and its intensity."),
            new AspectDef("cold", "Cold", "Freezing and the absence of heat."),
            new AspectDef("combustion", "Combustion", "Burning: ignition, flame and fuel."),
            new AspectDef("wave", "Wave", "Oscillation, vibration, resonance and sound."),
            new AspectDef("disruption", "Disruption", "Destabilizing a structure, pattern or flow."),
            new AspectDef("spatial", "Spatial", "Distance, position and the shape of space."),
            new AspectDef("matter", "Matter", "Physical substance and its form."),
            new AspectDef("structure", "Structure", "Solidity, hardness and ordered form."),
            new AspectDef("fluid", "Fluid", "Liquids, mists and what flows like them."),
            new AspectDef("life", "Life", "Living things, vitality and growth."),
            new AspectDef("decay", "Decay", "Rot, poison, corrosion and erosion."),
            new AspectDef("mind", "Mind", "Thought, will, emotion and dreams."),
            new AspectDef("sense", "Sense", "Perception and detection."),
            new AspectDef("light", "Light", "Illumination and radiance."),
            new AspectDef("shadow", "Shadow", "Darkness and what hides in it."),
            new AspectDef("energy", "Energy", "Raw magical power, charge and lightning."),
            new AspectDef("binding", "Binding", "Restraint, connection and holding together."),
            new AspectDef("attraction", "Attraction", "Pulling toward a point."),
            new AspectDef("repulsion", "Repulsion", "Pushing away from a point."),
            new AspectDef("absorption", "Absorption", "Taking in energy, effects or properties."),
            new AspectDef("reflection", "Reflection", "Returning, mirroring and redirecting."),
            new AspectDef("suppression", "Suppression", "Weakening, silencing and negating."),
            new AspectDef("focus", "Focus", "Precision and concentration on a point or an edge."),
            new AspectDef("time", "Time", "Duration, speed of events, past and future."),
            new AspectDef("soul", "Soul", "Spiritual essence and the bonds between souls."),
        };

        // ------------------------------------------------------------------ compatibility rules
        public static List<CompatibilityRule> Rules()
        {
            var r = new List<CompatibilityRule>();
            void AspectPair(string a, string b, float v, string note, bool ordered = false) => r.Add(new CompatibilityRule(CompatibilityScope.AspectPair, a, b, v, note, ordered));

            // Synergies.
            AspectPair("pressure", "combustion", 0.3f, "Contained pressure feeds a flame into a blast.");
            AspectPair("pressure", "combustion", 0.3f, "Pressure built first, then ignited: an explosion.", true);
            AspectPair("combustion", "wave", 0.4f, "Resonance spreads a burning blast further.");
            AspectPair("thermal", "combustion", 0.4f, "Heat and flame strengthen each other.");
            AspectPair("containment", "pressure", 0.4f, "Containment lets pressure build.");
            AspectPair("compression", "energy", 0.4f, "Condensed energy hits harder.");
            AspectPair("force", "motion", 0.4f, "Moving force carries further.");
            AspectPair("rotation", "motion", 0.3f, "Spin keeps a motion going.");
            AspectPair("focus", "force", 0.4f, "Concentrated force pierces.");
            AspectPair("focus", "energy", 0.3f, "Focused energy is precise.");
            AspectPair("cold", "structure", 0.4f, "Cold makes forms rigid.");
            AspectPair("cold", "fluid", 0.4f, "Cold shapes water into ice.");
            AspectPair("spatial", "focus", 0.4f, "A precise target point makes spatial magic reliable.");
            AspectPair("spatial", "binding", 0.3f, "A bound place stays where it is.");
            AspectPair("wave", "structure", 0.3f, "Vibration finds the weak points of a structure.");
            AspectPair("mind", "sense", 0.4f, "The mind interprets what the senses bring.");
            AspectPair("soul", "life", 0.3f, "Soul and life sustain each other.");
            AspectPair("time", "decay", 0.3f, "Time hastens decay.");
            AspectPair("absorption", "energy", 0.4f, "What is absorbed can be used.");
            AspectPair("reflection", "light", 0.4f, "Light bends and returns easily.");
            AspectPair("matter", "structure", 0.4f, "Matter given order holds its shape.");
            AspectPair("life", "expansion", 0.3f, "Life spreads.");
            AspectPair("shadow", "suppression", 0.3f, "Darkness muffles and hides.");
            AspectPair("attraction", "compression", 0.4f, "Pulling inward compresses: collapse.");
            AspectPair("binding", "containment", 0.4f, "What is bound stays contained.");
            // Conflicts.
            AspectPair("thermal", "cold", -0.6f, "Heat and cold cancel each other.");
            AspectPair("combustion", "fluid", -0.5f, "Water quenches flame.");
            AspectPair("light", "shadow", -0.6f, "Light and darkness oppose each other.");
            AspectPair("life", "decay", -0.5f, "Life and decay pull opposite ways.");
            AspectPair("suppression", "energy", -0.4f, "Suppression smothers the energy it is mixed with.");
            AspectPair("attraction", "repulsion", -0.4f, "Pulling and pushing at once.");
            AspectPair("binding", "motion", -0.3f, "Binding resists movement.");
            AspectPair("expansion", "compression", -0.3f, "Spreading and condensing at once.");

            // Alignments.
            void AlignmentPair(string a, string b, float v, string note) => r.Add(new CompatibilityRule(CompatibilityScope.AlignmentPair, a, b, v, note));
            AlignmentPair("fire", "water", -0.4f, "Opposed domains.");
            AlignmentPair("fire", "ice", -0.4f, "Opposed domains.");
            AlignmentPair("light", "shadow", -0.4f, "Opposed domains.");
            AlignmentPair("plant", "decay", -0.3f, "Growth against rot.");
            AlignmentPair("null", "energy", -0.3f, "Negation against raw power.");
            AlignmentPair("wind", "fire", 0.2f, "Air feeds flame.");
            AlignmentPair("earth", "metal", 0.2f, "Metal comes from the earth.");

            // Hard incoherences: contradictory concepts with no coherent relationship (design spec 13.2).
            r.Add(new CompatibilityRule(CompatibilityScope.ConceptPair, "null.negation", "energy.amplification", -1f,
                "Negating all magic while amplifying it has no coherent result.", false, true));
            r.Add(new CompatibilityRule(CompatibilityScope.ConceptPair, "time.haste", "time.slowness", -1f,
                "Time cannot run faster and slower for the same effect.", false, true));
            r.Add(new CompatibilityRule(CompatibilityScope.ConceptPair, "light.illumination", "shadow.darkness", -1f,
                "Filling a place with light and with darkness at once.", false, true));
            r.Add(new CompatibilityRule(CompatibilityScope.IntentionEffect, "restoration", "offense.disintegrate", 0f,
                "Restoring a target by breaking it down.", false, true));
            r.Add(new CompatibilityRule(CompatibilityScope.IntentionEffect, "purification", "offense.infect", 0f,
                "Purifying by introducing a harmful influence.", false, true));

            // Concept-specific affinities (the Pyroclastic Resonance concepts of design spec 18, as examples).
            r.Add(new CompatibilityRule(CompatibilityScope.ConceptEffect, "wind.pressure", "offense.explosion", 0.3f, "Contained pressure is the heart of an explosion."));
            r.Add(new CompatibilityRule(CompatibilityScope.ConceptEffect, "ash.combustion", "offense.explosion", 0.2f, "Smouldering ash ignites the blast."));
            r.Add(new CompatibilityRule(CompatibilityScope.ConceptEffect, "vibration.resonance", "offense.explosion", 0.2f, "Resonance carries the blast outward."));
            r.Add(new CompatibilityRule(CompatibilityScope.ConceptIntention, "restoration.revival", "attack", -0.5f, "Revival resists being turned to harm."));
            return r;
        }

        // ------------------------------------------------------------------ helpers
        /// <summary>"pressure:0.5" → aspect 'pressure', weight 0.5 ("pressure" alone = weight 1).</summary>
        internal static List<AspectWeight> W(params string[] specs)
        {
            var list = new List<AspectWeight>();
            foreach (string spec in specs)
            {
                if (string.IsNullOrEmpty(spec)) continue;
                int colon = spec.IndexOf(':');
                if (colon < 0) list.Add(new AspectWeight(spec.Trim(), 1f));
                else list.Add(new AspectWeight(spec.Substring(0, colon).Trim(), float.Parse(spec.Substring(colon + 1), CultureInfo.InvariantCulture)));
            }
            return list;
        }

        /// <summary>"a|b|c" → a requirement on any of a, b, c with the given minimum coverage.</summary>
        internal static AspectRequirement Req(string anyOf, float minCoverage = 0.5f) =>
            new AspectRequirement(minCoverage, anyOf.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));

        internal static Color Rgb(float r, float g, float b) => new Color(r, g, b, 1f);
    }
}
