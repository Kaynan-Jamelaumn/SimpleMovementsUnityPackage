using System.Collections.Generic;

namespace RuneMagic
{
    // Built-in concept discovery (design spec 8, 17): the five difficulty bands of 17.2, a set of knowledge sources that
    // together cover every rune, and the riddles. The Greater Wind "Pressure" riddle is the spec's own (17.3, verbatim); a
    // few more are written for the concepts of the four runes the spec details; every other concept gets a generated
    // riddle (see DiscoveryRules). Numbers are proposals.
    public static partial class RuneDefaults
    {
        private static DiscoveryTierDef Band(string id, string name, int minDungeonTier, int nodes, float speed, float width, float minScore,
            float timeLimit, int attempts, int steps, int options, int clues, params float[] rarityWeights) => new DiscoveryTierDef
        {
            id = id, name = name, minDungeonTier = minDungeonTier, minigameNodes = nodes, minigameSpeed = speed, nodeWidth = width,
            minScore = minScore, timeLimit = timeLimit, attempts = attempts, steps = steps, options = options, clues = clues,
            rarityWeights = new List<float>(rarityWeights),
        };

        /// <summary>Design spec 17.2: simple minigames and straightforward questions at the start, multi-step reasoning later, the rarest concepts last.</summary>
        public static DiscoveryBalance Discovery() => new DiscoveryBalance
        {
            tiers = new List<DiscoveryTierDef>
            {
                Band("beginner", "Beginner", 0, 3, 0.30f, 0.20f, 0.60f, 40f, 3, 1, 3, 3, 1f, 0.3f, 0f, 0f, 0f),
                Band("intermediate", "Intermediate", 1, 4, 0.38f, 0.16f, 0.65f, 45f, 2, 1, 4, 3, 1f, 1f, 0f, 0f, 0f),
                Band("advanced", "Advanced", 2, 5, 0.45f, 0.13f, 0.70f, 50f, 2, 2, 4, 2, 0.5f, 1f, 1f, 0f, 0f),
                Band("expert", "Expert", 3, 6, 0.52f, 0.11f, 0.75f, 55f, 1, 2, 5, 2, 0f, 0.5f, 1f, 1f, 0f),
                Band("legendary", "Legendary", 4, 7, 0.60f, 0.09f, 0.80f, 60f, 1, 3, 6, 1, 0f, 0f, 0.5f, 1f, 1f),
            },
            onlyRunesInSoul = true,
            abandonBurns = true,
            sessionSeconds = 240f,
            leaveDistance = 6f,
            complexityNarrowing = 0.35f,
            minPressInterval = 0.25f,
            clockSlack = 1.5f,
            avoidSeenRiddles = true,
            logLength = 30,
        };

        private static DiscoverySourceDef Source(string id, string name, DiscoverySourceKind kind, int minTier, string description, string intro, string runes = "")
        {
            var s = new DiscoverySourceDef { id = id, displayName = name, kind = kind, minTier = minTier, description = description, intro = intro };
            foreach (string r in runes.Split(' ')) if (r.Length > 0) s.runes.Add(r);
            return s;
        }

        /// <summary>
        /// Knowledge sources. A wrong answer burns a source <i>definition</i> for that concept (decision D7), so every rune is
        /// covered by several definitions: the general ones teach any rune, the themed ones the runes of their domain.
        /// </summary>
        public static List<DiscoverySourceDef> DiscoverySources() => new List<DiscoverySourceDef>
        {
            Source("ancient_inscription", "Ancient Inscription", DiscoverySourceKind.Inscription, 0,
                "Weathered glyphs cut into stone.", "Faded glyphs crawl across the wall. They seem to know your runes."),
            Source("shrine_of_principles", "Shrine of Principles", DiscoverySourceKind.Shrine, 0,
                "A shrine where magical principles are meditated upon.", "A quiet shrine. Something here teaches by asking."),
            Source("forgotten_library", "Forgotten Library", DiscoverySourceKind.Library, 1,
                "Shelves of crumbling scrolls.", "Scrolls crumble at a touch, yet one page still glows."),
            Source("magical_artifact", "Magical Artifact", DiscoverySourceKind.Artifact, 1,
                "An artifact humming with held knowledge.", "The artifact hums, waiting to be understood."),
            Source("sealed_chamber", "Sealed Chamber", DiscoverySourceKind.SealedChamber, 2,
                "A chamber sealed against the unworthy.", "The seal on the door will only yield to understanding."),
            Source("wind_inscription", "Gale Inscription", DiscoverySourceKind.Inscription, 0,
                "Glyphs that sing when the air moves.", "The glyphs whisper with every draught.", "wind gale vibration sound momentum friction scent mist"),
            Source("flame_inscription", "Ember Inscription", DiscoverySourceKind.Inscription, 0,
                "Glyphs scorched into the rock.", "The glyphs are warm, as if still being written.", "fire heat ash lightning energy powder"),
            Source("stone_inscription", "Stone Inscription", DiscoverySourceKind.Inscription, 0,
                "Deep-cut glyphs in bare rock.", "The stone remembers what carved it.", "earth matter armor metal crystal magnetism gravity"),
            Source("depth_inscription", "Depth Inscription", DiscoverySourceKind.Inscription, 0,
                "Glyphs that gleam like water, shadow and glass.", "The glyphs shimmer, light and shade chasing each other.", "water ice shadow light reflection"),
            Source("life_inscription", "Living Inscription", DiscoverySourceKind.Inscription, 0,
                "Glyphs that grow, bleed and decay.", "Roots have grown through the glyphs, and they have grown with the roots.", "blood poison plant decay growth restoration"),
            Source("mind_inscription", "Mind Inscription", DiscoverySourceKind.Inscription, 0,
                "Glyphs that rearrange when not watched.", "The glyphs seem to shift whenever you look away.", "mind dream soul absorption null binding shift space time"),
        };

        private static RiddleDef Riddle(string concept, int n, string text, int minTier = 0, int maxTier = -1) => new RiddleDef
        {
            id = concept + "." + n, concept = concept, text = text, minTier = minTier, maxTier = maxTier,
        };

        /// <summary>Riddles about magical principles (never memorization). The first is the spec's (17.3), verbatim.</summary>
        public static List<RiddleDef> Riddles() => new List<RiddleDef>
        {
            // Spec 17.3: options Flow, Pressure, Movement, Turbulence; answer Pressure.
            new RiddleDef
            {
                id = "wind.pressure.1", concept = "wind.pressure",
                text = "I cannot be seen, yet I can crush stone.\nI can be trapped within a vessel, and I grow stronger when confined.\nI am neither the wind's movement nor its direction.\nWhat Concept am I?",
                distractors = new List<string> { "wind.flow", "wind.movement", "wind.turbulence" },
            },
            Riddle("wind.vortex", 1, "I am not the wind's strength but its habit of returning to where it began.\nI turn upon myself, and whatever I touch is drawn toward my heart.\nCalm lies at my center; fury at my edge.\nWhat Concept am I?"),
            Riddle("wind.vacuum", 1, "I am what remains when the air has been taken away.\nI do not push: I pull, and I make a stillness that sound cannot cross.\nA sealed jar holds me when the breath is drawn out of it.\nWhat Concept am I?", 1),
            Riddle("wind.compression", 1, "I do not raise the air's push against its walls; I shrink the room it lives in.\nTwo hands closing on a bellows know me.\nWhat Concept am I?"),
            Riddle("fire.combustion", 1, "I ask for three things and give back two: fuel, air and a spark go in; light and heat come out.\nTake any one of the three away and I am gone.\nWhat Concept am I?"),
            Riddle("fire.flame_propagation", 1, "I am not the heat of the flame, nor its hunger, but the road it travels.\nDry grass and a following wind carry me faster than any messenger.\nWhat Concept am I?", 1),
            Riddle("fire.plasma", 1, "I am what fire becomes when it is given more than it can hold.\nI am no longer a flame, for matter itself has been torn apart and set aglow.\nLightning and stars know me.\nWhat Concept am I?", 3),
            Riddle("vibration.resonance", 1, "A singer breaks a glass without touching it, and I am the reason.\nI take a small push, timed rightly, and make it grow.\nWhat Concept am I?"),
            Riddle("vibration.structural_weakness", 1, "I do not break a thing with force; I find where it is already cracked and ask it, softly, to finish the job.\nWhat Concept am I?", 2),
        };
    }
}
