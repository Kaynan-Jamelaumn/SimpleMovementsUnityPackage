#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;

namespace RuneMagic.Tests
{
    /// <summary>
    /// Edit Mode tests of the Rune Magic data foundation (Phase 1): the built-in content against the design spec (runes,
    /// alignments, the concepts of spec section 8, intentions, desired effects - names and descriptions verbatim), the
    /// balancing tables and their lookups, intention support, content packs (extensions and concept additions),
    /// validation and JSON round trips. Pure data: no scene, no assets.
    /// </summary>
    public class RuneMagicDataTests
    {
        private static RuneCatalog Built() => RuneDefaults.Catalog();

        private static void AssertTable(RuneCatalog c, string runeId, string[] nameAndDescription)
        {
            RuneDef r = c.Rune(runeId);
            Assert.IsNotNull(r, runeId);
            foreach (string row in nameAndDescription)
            {
                string[] parts = row.Split('|');
                ConceptDef k = r.concepts.Find(x => x.displayName == parts[0]);
                Assert.IsNotNull(k, $"{runeId}: concept {parts[0]}");
                Assert.AreEqual(parts[1], k.description, $"{runeId}.{parts[0]} description");
                Assert.AreSame(r, c.RuneOfConcept(k.id));
            }
        }

        // ------------------------------------------------------------------ built-in content
        [Test]
        public void BuiltInContentCompilesWithoutErrorsOrWarnings()
        {
            RuneCatalog c = Built();
            Assert.IsEmpty(c.Errors, string.Join("\n", c.Errors));
            Assert.IsEmpty(c.Warnings, string.Join("\n", c.Warnings));
            Assert.IsTrue(c.IsValid);
            Assert.IsFalse(string.IsNullOrEmpty(c.ContentHash));
        }

        [Test]
        public void SpecRunesAndAlignmentsArePresent()
        {
            RuneCatalog c = Built();
            string[] novel =
            {
                "Fire|Flames and combustion", "Wind|Air movement and pressure", "Ash|Ash and particulate matter",
                "Vibration|Oscillation and vibration", "Heat|Thermal energy", "Shadow|Darkness and shadow", "Shift|Alteration and displacement",
                "Water|Water manipulation", "Ice|Frozen water", "Earth|Earth and soil", "Gale|Powerful wind", "Lightning|Electrical energy",
                "Light|Illumination", "Space|Spatial manipulation", "Matter|Physical matter", "Mind|Mental influence", "Blood|Blood manipulation",
                "Poison|Toxic substances", "Plant|Vegetation", "Armor|Defensive properties", "Absorption|Absorbing energy or properties",
                "Mist|Mist and fog", "Powder|Fine particles", "Energy|Raw or magical energy",
            };
            string[] original =
            {
                "Gravity|Attraction, repulsion, gravitational force", "Sound|Sound waves, acoustic manipulation", "Metal|Metallic materials",
                "Crystal|Crystalline structures", "Magnetism|Magnetic fields", "Decay|Degradation of matter", "Growth|Accelerated growth",
                "Reflection|Reflection of energy or effects", "Friction|Resistance between surfaces", "Momentum|Motion and momentum",
                "Scent|Odors and chemical traces", "Soul|Soul energy and spiritual interaction", "Time|Temporal influence", "Dream|Dream manipulation",
                "Null|Suppression or absence of magical effects",
            };
            Assert.AreEqual(24, novel.Length);
            Assert.AreEqual(15, original.Length);
            foreach (string row in new List<string>(novel).With(original))
            {
                string[] parts = row.Split('|');
                string id = parts[0].ToLowerInvariant();
                RuneDef r = c.Rune(id);
                Assert.IsNotNull(r, "rune " + id);
                Assert.AreEqual(parts[0], r.displayName);
                Assert.AreEqual(RuneKind.Base, r.kind, id);
                CollectionAssert.AreEqual(new[] { id }, r.alignments, id);
                AlignmentDef a = c.Alignment(id);
                Assert.IsNotNull(a, "alignment " + id);
                Assert.AreEqual(parts[1], a.domain, id + " domain");
            }
            // Added for the combinations of spec section 19 (Soul Chain, Eternal Bloom).
            Assert.IsNotNull(c.Rune("binding"));
            Assert.IsNotNull(c.Rune("restoration"));
            Assert.AreEqual(41, c.Runes.Count);
            Assert.AreEqual(41, c.Alignments.Count);
        }

        [Test]
        public void SpecConceptTablesAreVerbatim()
        {
            RuneCatalog c = Built();
            AssertTable(c, "wind", new[]
            {
                "Movement|Manipulate the movement of air", "Flow|Control direction and continuity of airflow",
                "Pressure|Increase or decrease air pressure", "Compression|Compress air into a smaller volume",
                "Turbulence|Create chaotic airflow", "Vortex|Create rotational airflow", "Acceleration|Increase speed of moving air",
                "Resistance|Increase resistance against movement", "Propulsion|Generate force through directed airflow",
                "Vacuum|Create an area of reduced air pressure",
            });
            AssertTable(c, "fire", new[]
            {
                "Combustion|Initiate or sustain burning", "Ignition|Cause a material to begin burning", "Expansion|Expand flames or thermal effects",
                "Temperature|Manipulate thermal intensity", "Fuel|Influence combustible material", "Plasma|Manipulate highly energized matter",
                "Incineration|Destroy material through intense heat", "Flame Propagation|Control how fire spreads",
            });
            AssertTable(c, "vibration", new[]
            {
                "Oscillation|Create repetitive movement", "Resonance|Amplify vibration through compatible frequencies",
                "Frequency|Manipulate vibration frequency", "Amplitude|Increase or decrease vibration strength",
                "Disruption|Destabilize a structure or pattern", "Harmonics|Create complex vibration patterns",
                "Wave Propagation|Control how vibrations travel", "Structural Weakness|Exploit structural weaknesses through vibration",
            });
            AssertTable(c, "space", new[]
            {
                "Distance|Manipulate distance between locations", "Displacement|Move a target from its current position",
                "Folding|Compress spatial distance", "Expansion|Increase spatial distance", "Distortion|Alter the geometry of space",
                "Anchoring|Stabilize a location", "Isolation|Separate a space from its surroundings", "Compression|Reduce spatial volume",
            });
            Assert.AreEqual(10, c.Rune("wind").concepts.Count);
            Assert.AreEqual(8, c.Rune("fire").concepts.Count);
            Assert.AreEqual(8, c.Rune("vibration").concepts.Count);
            Assert.AreEqual(8, c.Rune("space").concepts.Count);
            // The concepts the Pyroclastic Resonance example uses (spec 11.2, 18.1).
            Assert.IsNotNull(c.Concept("wind.pressure"));
            Assert.IsNotNull(c.Concept("ash.combustion"));
            Assert.IsNotNull(c.Concept("vibration.resonance"));
            Assert.IsNotNull(c.Concept("fire.expansion"));
        }

        [Test]
        public void EveryRuneHasItsOwnConceptsWithAspects()
        {
            RuneCatalog c = Built();
            foreach (RuneDef r in c.Runes)
            {
                Assert.GreaterOrEqual(r.concepts.Count, 6, r.id + " needs its own concepts");
                foreach (ConceptDef k in r.concepts)
                {
                    StringAssert.StartsWith(r.id + ".", k.id);
                    Assert.IsNotEmpty(k.aspects, k.id);
                    Assert.IsFalse(string.IsNullOrEmpty(k.description), k.id);
                    // Rank and concept knowledge are independent: no built-in concept is tied to a rank.
                    Assert.AreEqual(0, k.minimumRankToUse, k.id);
                }
            }
            Assert.GreaterOrEqual(c.ConceptCount, 250);
        }

        [Test]
        public void IntentionsMatchTheSpec()
        {
            string[] spec =
            {
                "Attack|Harm or damage a target", "Defense|Protect the player or an ally", "Control|Restrict or manipulate a target",
                "Movement|Alter movement or positioning", "Creation|Create something new", "Destruction|Destroy a target or structure",
                "Transformation|Change properties of a target", "Restoration|Restore a target to a previous state",
                "Suppression|Reduce or disable an ability", "Enhancement|Improve a target's capabilities", "Concealment|Hide the player or an object",
                "Detection|Reveal hidden targets or information", "Manipulation|Directly manipulate an object or force",
                "Binding|Restrict a target's movement or abilities", "Absorption|Absorb energy or magical effects",
                "Replication|Reproduce an object or magical pattern", "Summoning|Bring a creature or entity to a location",
                "Imbuement|Transfer a magical property into an object", "Displacement|Move a target from one location to another",
                "Alteration|Change structure or behavior of something", "Interference|Disrupt another magical effect",
                "Sealing|Prevent something from being accessed or activated", "Amplification|Increase power of an existing effect",
                "Deception|Create misleading magical information", "Exploration|Reveal or interact with unknown environments",
                "Purification|Remove unwanted properties or influences", "Communication|Transmit information",
                "Perception|Alter or enhance sensory capabilities", "Preservation|Prevent degradation or destruction",
                "Extraction|Remove a specific property or substance", "Conversion|Transform energy or magical properties",
            };
            RuneCatalog c = Built();
            Assert.AreEqual(31, c.Intentions.Count);
            foreach (string row in spec)
            {
                string[] parts = row.Split('|');
                IntentionDef i = c.Intention(parts[0].ToLowerInvariant());
                Assert.IsNotNull(i, parts[0]);
                Assert.AreEqual(parts[0], i.displayName);
                Assert.AreEqual(parts[1], i.description, parts[0]);
                Assert.IsNotEmpty(c.EffectsSupportedBy(i.id), parts[0] + " supports nothing");
            }
        }

        [Test]
        public void DesiredEffectsMatchTheSpec()
        {
            var spec = new Dictionary<EffectCategory, string[]>
            {
                [EffectCategory.Offensive] = new[]
                {
                    "Explosion|Explosive release of energy", "Pierce|Penetrate a target", "Slash|Cutting effect", "Crush|Apply destructive pressure",
                    "Incinerate|Burn a target intensely", "Disintegrate|Break a target down", "Detonate|Trigger a sudden destructive reaction",
                    "Erode|Gradually destroy a target", "Shatter|Break a target into fragments", "Rend|Tear a target apart",
                    "Overload|Exceed a target's structural or magical capacity", "Corrode|Degrade a material", "Implode|Collapse a target inward",
                    "Chain Reaction|Trigger consecutive effects", "Pulverize|Reduce a target to smaller fragments",
                    "Sever|Separate a target or its components", "Infect|Introduce a harmful magical influence", "Drain|Remove energy from a target",
                    "Rupture|Cause a target to break open", "Penetrate|Pass through a target's defenses",
                },
                [EffectCategory.Defensive] = new[]
                {
                    "Barrier|Create a protective barrier", "Shield|Protect a specific target", "Deflect|Redirect incoming attacks",
                    "Absorb|Consume incoming energy", "Reinforce|Strengthen a structure or body", "Reflect|Return an incoming effect",
                    "Nullify|Cancel a magical effect", "Fortify|Improve defensive resistance", "Stabilize|Prevent disruption or collapse",
                    "Disperse|Spread out or weaken an incoming effect", "Harden|Increase physical durability", "Insulate|Prevent transfer of energy",
                    "Seal|Prevent penetration or entry", "Counter|Intercept or neutralize an incoming effect",
                },
                [EffectCategory.Movement] = new[]
                {
                    "Dash|Move rapidly over a short distance", "Teleport|Instantly relocate", "Propel|Generate movement force",
                    "Levitate|Suspend a target in the air", "Accelerate|Increase movement speed", "Decelerate|Reduce movement speed",
                    "Blink|Short-range displacement", "Pull|Draw a target toward the caster", "Push|Move a target away",
                    "Warp|Distort spatial positioning", "Glide|Move through the air", "Launch|Propel a target forcefully",
                    "Orbit|Make a target move around a central point", "Reposition|Change a target's location", "Phase|Temporarily bypass physical obstacles",
                },
                [EffectCategory.Control] = new[]
                {
                    "Immobilize|Prevent movement", "Bind|Restrict movement or abilities", "Suspend|Hold a target in the air",
                    "Silence|Prevent sound or verbal casting", "Disorient|Disrupt a target's perception", "Repel|Force targets away",
                    "Attract|Draw targets toward a point", "Slow|Reduce movement or action speed", "Freeze|Prevent movement through freezing",
                    "Confine|Restrict a target to an area", "Paralyze|Prevent voluntary movement", "Suppress|Reduce effectiveness of an ability",
                    "Distract|Disrupt a target's attention", "Weaken|Reduce a target's strength", "Restrict|Limit a target's actions",
                },
                [EffectCategory.CreationTransformation] = new[]
                {
                    "Construct|Create a structure", "Forge|Create or reshape an object", "Shape|Alter the form of a material",
                    "Transmute|Change a material into another form", "Duplicate|Create a copy", "Grow|Increase size", "Shrink|Reduce size",
                    "Animate|Give movement to an object", "Condense|Concentrate a substance or energy", "Expand|Increase volume or area",
                    "Convert|Change one form of energy/material into another", "Purify|Remove impurities or unwanted properties",
                    "Refine|Improve quality of a material", "Crystallize|Convert a material into a crystalline structure",
                    "Solidify|Convert a substance into a solid", "Liquefy|Convert a substance into a liquid", "Decompose|Break down a material",
                    "Reassemble|Reconstruct an object from components",
                },
                [EffectCategory.UtilityExploration] = new[]
                {
                    "Reveal|Expose hidden information", "Track|Detect a target's location", "Scan|Analyze a target",
                    "Sense|Detect magical or physical properties", "Illuminate|Produce light", "Conceal|Hide a target", "Communicate|Transmit information",
                    "Record|Store information", "Dispel|Remove a magical effect", "Seal|Lock or restrict an object or location",
                    "Detect|Identify a specific property or entity", "Restore|Repair or recover a target", "Identify|Reveal properties of an object",
                    "Locate|Find a specific target", "Mark|Apply a detectable magical signature", "Cleanse|Remove contamination",
                    "Preserve|Prevent deterioration", "Extract|Remove a specific component", "Transfer|Move a property from one target to another",
                },
            };
            RuneCatalog c = Built();
            int total = 0;
            foreach (KeyValuePair<EffectCategory, string[]> kv in spec)
            {
                List<DesiredEffectDef> inCategory = c.EffectsIn(kv.Key);
                Assert.AreEqual(kv.Value.Length, inCategory.Count, kv.Key.ToString());
                foreach (string row in kv.Value)
                {
                    string[] parts = row.Split('|');
                    DesiredEffectDef e = inCategory.Find(x => x.displayName == parts[0]);
                    Assert.IsNotNull(e, $"{kv.Key}: {parts[0]}");
                    Assert.AreEqual(parts[1], e.description, parts[0]);
                    Assert.IsNotEmpty(e.required, e.id + " has no requirement");
                }
                total += kv.Value.Length;
            }
            Assert.AreEqual(101, total);
            Assert.AreEqual(101, c.Effects.Count);
            // "Seal" is both a defensive and a utility effect: two ids.
            Assert.AreEqual(EffectCategory.Defensive, c.Effect("defense.seal").category);
            Assert.AreEqual(EffectCategory.UtilityExploration, c.Effect("utility.seal").category);
        }

        [Test]
        public void EveryEffectIsSupportedAndReachable()
        {
            RuneCatalog c = Built();
            foreach (DesiredEffectDef e in c.Effects)
            {
                bool supported = false;
                foreach (IntentionDef i in c.Intentions)
                    if (c.IntentionSupport(i.id, e.id) > 0f) { supported = true; break; }
                Assert.IsTrue(supported, e.id + " is supported by no intention");
                foreach (AspectRequirement req in e.required)
                {
                    bool reachable = false;
                    foreach (RuneDef r in c.Runes)
                        foreach (ConceptDef k in r.concepts)
                            foreach (string a in req.anyOf)
                                if (k.AspectWeightOf(a) > 0f) reachable = true;
                    Assert.IsTrue(reachable, $"{e.id}: no concept provides {req}");
                }
            }
        }

        [Test]
        public void IntentionSupportFollowsItsPrecedence()
        {
            RuneCatalog c = Built();
            Assert.AreEqual(1f, c.IntentionSupport("attack", "offense.explosion"), "primary category");
            Assert.AreEqual(0.5f, c.IntentionSupport("attack", "control.slow"), 1e-5, "secondary category");
            Assert.AreEqual(1f, c.IntentionSupport("attack", "control.weaken"), "override");
            Assert.AreEqual(0f, c.IntentionSupport("defense", "offense.pierce"), "not supported");
            Assert.AreEqual(0f, c.IntentionSupport("restoration", "offense.disintegrate"), "incoherent rule");
            Assert.AreEqual(0f, c.IntentionSupport("attack", "no.such_effect"));
            Assert.AreEqual(0f, c.IntentionSupport("no_such_intention", "offense.explosion"));
            List<DesiredEffectDef> restoration = c.EffectsSupportedBy("restoration");
            Assert.AreEqual(1f, c.IntentionSupport("restoration", restoration[0].id), "best supported first");
        }

        // ------------------------------------------------------------------ balance
        [Test]
        public void BalanceTablesHoldTheSpecValues()
        {
            RuneBalance b = Built().Balance;
            Assert.AreEqual(7, b.CapacityAt(1));
            Assert.AreEqual(7, b.CapacityAt(19));
            Assert.AreEqual(14, b.CapacityAt(20));
            Assert.AreEqual(21, b.CapacityAt(40));
            Assert.AreEqual(28, b.CapacityAt(99));
            Assert.AreEqual(9, b.CapacityAt(1, 2), "bonus slots");

            float[] energy = { 1000f, 5000f, 25000f, 125000f, 625000f };
            for (int rank = 1; rank <= 5; rank++)
                Assert.AreEqual(energy[rank - 1], b.MaxEnergy(rank), 1e-3, "rank " + rank);
            Assert.AreEqual(625000f * 5f, b.MaxEnergy(6), 1e-1, "beyond the table");
            Assert.AreEqual(1000f, b.MaxEnergy(0), 1e-3, "rank below 1 = rank 1");

            Assert.AreEqual(7, b.runesPerRankAdvancement);
            Assert.AreEqual("Greater", b.TitleOf(1));
            Assert.AreEqual("Greater Wind", Built().RuneName(Built().Rune("wind"), 1));
            Assert.AreEqual("Crude", b.QualityTierOf(0f).name);
            Assert.AreEqual("Flawless", b.QualityTierOf(100f).name);
            Assert.AreEqual(100f, b.maxSoulIntegrity);
        }

        [Test]
        public void RankQualityAndEnergyStayIndependent()
        {
            RuneBalance b = RuneDefaults.Balance();
            Assert.IsFalse(b.qualityAffectsMaxEnergy);
            Assert.AreEqual(b.MaxEnergy(1, 0f), b.MaxEnergy(1, 100f), 1e-3, "quality does not change capacity unless coupled");
            b.qualityAffectsMaxEnergy = true;
            b.maxEnergyBonusAtFullQuality = 0.2f;
            Assert.AreEqual(1200f, b.MaxEnergy(1, 100f), 1e-2, "explicit coupling");
            Assert.AreEqual(1100f, b.MaxEnergy(1, 50f), 1e-2);
        }

        [Test]
        public void SynthesisTiersFollowTheThresholds()
        {
            RuneBalance b = Built().Balance;
            Assert.AreEqual(SynthesisTier.Perfect, b.TierOf(100f));
            Assert.AreEqual(SynthesisTier.Successful, b.TierOf(99.9f));
            Assert.AreEqual(SynthesisTier.Successful, b.TierOf(70f));
            Assert.AreEqual(SynthesisTier.Flawed, b.TierOf(69.9f));
            Assert.AreEqual(SynthesisTier.Flawed, b.TierOf(30f));
            Assert.AreEqual(SynthesisTier.CompleteFailure, b.TierOf(29.9f));
            Assert.AreEqual(SynthesisTier.CompleteFailure, b.TierOf(0f));
        }

        [Test]
        public void FailureSeverityScalesWithScoreAndRisk()
        {
            RuneBalance b = Built().Balance;
            Assert.AreEqual("Minor", b.SeverityOf(25f, 0f).name);
            Assert.AreEqual("Moderate", b.SeverityOf(15f, 0f).name);
            Assert.AreEqual("Severe", b.SeverityOf(5f, 0f).name);
            Assert.AreEqual("Catastrophic", b.SeverityOf(5f, 0.8f).name, "destruction for risky attempts");
            Assert.IsTrue(b.SeverityOf(5f, 0.8f).destroysRunes);
            Assert.AreEqual("Moderate", b.SeverityOf(15f, 0.9f).name, "a near miss is not destroyed even when risky");
            Assert.IsFalse(b.SeverityOf(29f, 0f).destroysRunes);
        }

        [Test]
        public void SoulConditionsFollowTheSpecThresholds()
        {
            RuneBalance b = Built().Balance;
            string Of(float v) => b.ConditionOf(v, 100f).id;
            Assert.AreEqual("stable", Of(100f));
            Assert.AreEqual("stable", Of(80f));
            Assert.AreEqual("strained", Of(79.9f));
            Assert.AreEqual("strained", Of(60f));
            Assert.AreEqual("damaged", Of(59f));
            Assert.AreEqual("damaged", Of(40f));
            Assert.AreEqual("critical", Of(39f));
            Assert.AreEqual("critical", Of(20f));
            Assert.AreEqual("near_collapse", Of(19f));
            Assert.AreEqual("near_collapse", Of(1f));
            Assert.AreEqual("near_collapse", Of(0.5f));
            Assert.AreEqual("collapse", Of(0f));
            Assert.AreEqual("collapse", Of(-5f));
            Assert.AreEqual("stable", b.ConditionOf(62f, 62f).id, "percent of the maximum");

            Assert.IsTrue(b.Condition("stable").synthesisAllowed && b.Condition("stable").castingAllowed);
            Assert.IsFalse(b.Condition("near_collapse").synthesisAllowed, "synthesis disabled near collapse");
            Assert.IsTrue(b.Condition("near_collapse").castingAllowed, "casting heavily penalized, not disabled");
            Assert.IsFalse(b.Condition("collapse").castingAllowed, "no casting in soul collapse");
            Assert.Greater(b.Condition("damaged").synthesisFailureChance, 0f);
            Assert.AreEqual("Mild Strain", b.Condition("strained").castingDebuff);
            Assert.AreEqual("Soul Collapse", b.Condition("collapse").castingDebuff);
        }

        // ------------------------------------------------------------------ extensions, validation, JSON
        [Test]
        public void ContentPacksAddAndReplaceWithoutTouchingTheBase()
        {
            RuneMagicData baseData = RuneDefaults.Create();
            var pack = new RuneMagicData();
            pack.runes.Add(new RuneDef
            {
                id = "storm_glass", displayName = "Storm Glass", alignments = new List<string> { "crystal", "lightning" }, kind = RuneKind.Composite,
                concepts = new List<ConceptDef> { new ConceptDef { id = "storm_glass.capture", displayName = "Capture", description = "Trap a bolt in glass", aspects = RuneDefaults.W("energy:0.7", "containment:0.6") } },
            });
            pack.intentions.Add(new IntentionDef { id = "attack", displayName = "Attack", description = "Replaced by the pack", primaryCategories = new List<EffectCategory> { EffectCategory.Offensive } });
            pack.conceptAdditions.Add(new RuneConceptAddition
            {
                rune = "wind",
                concepts = new List<ConceptDef>
                {
                    new ConceptDef { id = "wind.whisper", displayName = "Whisper", description = "Carry a voice on the wind", aspects = RuneDefaults.W("wave:0.5", "motion:0.4") },
                    new ConceptDef { id = "wind.flow", displayName = "Flow", description = "Changed by the pack", aspects = RuneDefaults.W("motion:1") },
                },
            });

            RuneCatalog c = RuneCatalog.Compile(baseData, new[] { pack });
            Assert.IsEmpty(c.Errors, string.Join("\n", c.Errors));
            Assert.IsNotNull(c.Rune("storm_glass"));
            Assert.AreEqual(42, c.Runes.Count);
            Assert.AreEqual("Replaced by the pack", c.Intention("attack").description);
            Assert.AreEqual(31, c.Intentions.Count, "same id replaces");
            Assert.IsNotNull(c.Concept("wind.whisper"));
            Assert.AreEqual("Changed by the pack", c.Concept("wind.flow").description);
            Assert.AreEqual(11, c.Rune("wind").concepts.Count);

            Assert.AreEqual(41, baseData.runes.Count, "the base data is not modified");
            Assert.AreEqual("Control direction and continuity of airflow", baseData.runes.Find(r => r.id == "wind").FindConcept("wind.flow").description);
            Assert.AreEqual(0, pack.conceptAdditions[0].concepts.FindIndex(k => k.id == "wind.whisper"), "the pack is not modified");
            Assert.AreNotEqual(RuneDefaults.Catalog().ContentHash, c.ContentHash);
        }

        [Test]
        public void AdditionsForUnknownRunesAreReported()
        {
            var pack = new RuneMagicData();
            pack.conceptAdditions.Add(new RuneConceptAddition { rune = "no_such_rune", concepts = new List<ConceptDef> { new ConceptDef { id = "no_such_rune.x" } } });
            RuneCatalog c = RuneCatalog.Compile(RuneDefaults.Create(), new[] { pack });
            Assert.IsTrue(new List<string>(c.Warnings).Exists(w => w.Contains("no_such_rune")), string.Join("\n", c.Warnings));
        }

        [Test]
        public void ValidationFindsBrokenContent()
        {
            RuneMagicData d = RuneDefaults.Create();
            d.runes.Add(new RuneDef { id = "wind", displayName = "Wind again", alignments = new List<string> { "wind" } });
            RuneDef fire = d.runes.Find(r => r.id == "fire");
            fire.concepts.Add(new ConceptDef { id = "ice.misplaced", displayName = "Misplaced", aspects = RuneDefaults.W("thermal:1") });
            fire.concepts.Add(new ConceptDef { id = "fire.unknown_aspect", displayName = "Unknown", aspects = RuneDefaults.W("no_such_aspect:1") });
            d.runes.Add(new RuneDef { id = "Bad Id", alignments = new List<string> { "no_such_alignment" } });
            d.rules.Add(new CompatibilityRule(CompatibilityScope.ConceptPair, "wind.pressure", "no.such", 0.5f, "broken"));
            d.balance.soulConditions.RemoveAll(s => s.isCollapse);

            RuneCatalog c = RuneCatalog.Compile(d);
            string all = string.Join("\n", c.Errors);
            Assert.IsFalse(c.IsValid);
            StringAssert.Contains("rune id 'wind' is used twice", all);
            StringAssert.Contains("'ice.misplaced' of rune 'fire'", all);
            StringAssert.Contains("unknown aspect 'no_such_aspect'", all);
            StringAssert.Contains("'Bad Id' is not valid", all);
            StringAssert.Contains("unknown alignment 'no_such_alignment'", all);
            StringAssert.Contains("refers to an unknown id", all);
            StringAssert.Contains("exactly one Soul Collapse", all);
        }

        [Test]
        public void JsonRoundTripKeepsTheContent()
        {
            RuneMagicData d = RuneDefaults.Create();
            RuneMagicData back = RuneMagicData.FromJson(d.ToJson());
            Assert.AreEqual(RuneCatalog.Compile(d).ContentHash, RuneCatalog.Compile(back).ContentHash);
            Assert.AreEqual(d.runes.Count, back.runes.Count);
            Assert.AreEqual(d.runes[0].concepts[2].aspects[0].weight, back.runes[0].concepts[2].aspects[0].weight, 1e-6);

            RuneMagicData copy = d.Clone();
            copy.runes[0].displayName = "Changed";
            Assert.AreNotEqual("Changed", d.runes[0].displayName, "a clone is independent");
        }

        [Test]
        public void ContentHashFollowsTheContent()
        {
            RuneMagicData d = RuneDefaults.Create();
            string before = RuneCatalog.Compile(d).ContentHash;
            Assert.AreEqual(before, RuneCatalog.Compile(RuneDefaults.Create()).ContentHash, "same content, same hash");
            d.runes.Find(r => r.id == "wind").FindConcept("wind.pressure").aspects[0].weight = 0.9f;
            Assert.AreNotEqual(before, RuneCatalog.Compile(d).ContentHash);
            Assert.AreEqual(16, before.Length);
        }

        [Test]
        public void IdRulesAreStrict()
        {
            Assert.IsTrue(RuneCatalog.IsValidId("wind"));
            Assert.IsTrue(RuneCatalog.IsValidId("wind.pressure"));
            Assert.IsTrue(RuneCatalog.IsValidId("flame_propagation"));
            Assert.IsFalse(RuneCatalog.IsValidId("Wind"));
            Assert.IsFalse(RuneCatalog.IsValidId(".wind"));
            Assert.IsFalse(RuneCatalog.IsValidId("wind pressure"));
            Assert.IsFalse(RuneCatalog.IsValidId(""));
            Assert.AreEqual("aspect:pressure>combustion", CompatibilityRule.MakeId(CompatibilityScope.AspectPair, "pressure", "combustion", true));
        }
    }

    internal static class ListExtensions
    {
        public static List<T> With<T>(this List<T> a, IEnumerable<T> b)
        {
            var list = new List<T>(a);
            list.AddRange(b);
            return list;
        }
    }
}
#endif
