using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    // Built-in alignments, runes and concepts. Design spec 7.2 (novel-inspired) and 7.3 (original) runes; the concepts of
    // Greater Wind, Fire, Vibration and Space are the spec's (section 8, verbatim). Every other rune's concepts are
    // proposals (the spec asks every rune to have its own): rename, replace or add to them on the database. Binding and
    // Restoration are added as original runes so the combinations of spec section 19 can be made.
    public static partial class RuneDefaults
    {
        // ------------------------------------------------------------------ alignments
        public static List<AlignmentDef> Alignments()
        {
            var list = new List<AlignmentDef>();
            void A(string id, string name, string domain, ElementType element, Color color, params string[] aspects) =>
                list.Add(new AlignmentDef { id = id, displayName = name, domain = domain, element = element, color = color, aspects = W(aspects) });

            // Novel-inspired (design spec 7.2).
            A("fire", "Fire", "Flames and combustion", ElementType.Fire, Rgb(1f, 0.45f, 0.1f), "combustion:1", "thermal:0.8", "expansion:0.3");
            A("wind", "Wind", "Air movement and pressure", ElementType.Wind, Rgb(0.7f, 0.9f, 1f), "motion:1", "pressure:0.6", "fluid:0.2");
            A("ash", "Ash", "Ash and particulate matter", ElementType.None, Rgb(0.45f, 0.42f, 0.4f), "combustion:0.5", "matter:0.5", "suppression:0.3", "decay:0.3");
            A("vibration", "Vibration", "Oscillation and vibration", ElementType.None, Rgb(0.8f, 0.6f, 1f), "wave:1", "disruption:0.4");
            A("heat", "Heat", "Thermal energy", ElementType.Fire, Rgb(1f, 0.3f, 0.2f), "thermal:1");
            A("shadow", "Shadow", "Darkness and shadow", ElementType.Dark, Rgb(0.25f, 0.15f, 0.35f), "shadow:1", "suppression:0.3");
            A("shift", "Shift", "Alteration and displacement", ElementType.None, Rgb(0.6f, 0.8f, 0.7f), "spatial:0.6", "motion:0.5", "disruption:0.3");
            A("water", "Water", "Water manipulation", ElementType.Water, Rgb(0.2f, 0.5f, 1f), "fluid:1", "motion:0.4");
            A("ice", "Ice", "Frozen water", ElementType.Ice, Rgb(0.7f, 0.9f, 1f), "cold:1", "structure:0.5");
            A("earth", "Earth", "Earth and soil", ElementType.Earth, Rgb(0.55f, 0.4f, 0.25f), "matter:0.8", "structure:0.8");
            A("gale", "Gale", "Powerful wind", ElementType.Wind, Rgb(0.5f, 0.8f, 0.9f), "force:0.8", "motion:0.8");
            A("lightning", "Lightning", "Electrical energy", ElementType.Lightning, Rgb(1f, 0.95f, 0.4f), "energy:1", "motion:0.4");
            A("light", "Light", "Illumination", ElementType.Holy, Rgb(1f, 1f, 0.8f), "light:1");
            A("space", "Space", "Spatial manipulation", ElementType.None, Rgb(0.3f, 0.2f, 0.6f), "spatial:1");
            A("matter", "Matter", "Physical matter", ElementType.None, Rgb(0.6f, 0.55f, 0.5f), "matter:1", "structure:0.4");
            A("mind", "Mind", "Mental influence", ElementType.None, Rgb(0.9f, 0.5f, 0.9f), "mind:1", "sense:0.3");
            A("blood", "Blood", "Blood manipulation", ElementType.None, Rgb(0.7f, 0.05f, 0.1f), "life:0.8", "fluid:0.4");
            A("poison", "Poison", "Toxic substances", ElementType.Poison, Rgb(0.4f, 0.8f, 0.2f), "decay:1");
            A("plant", "Plant", "Vegetation", ElementType.None, Rgb(0.3f, 0.7f, 0.2f), "life:1", "binding:0.3");
            A("armor", "Armor", "Defensive properties", ElementType.None, Rgb(0.6f, 0.6f, 0.65f), "structure:1");
            A("absorption", "Absorption", "Absorbing energy or properties", ElementType.None, Rgb(0.4f, 0.3f, 0.5f), "absorption:1");
            A("mist", "Mist", "Mist and fog", ElementType.Water, Rgb(0.8f, 0.85f, 0.9f), "fluid:0.6", "shadow:0.4", "expansion:0.4");
            A("powder", "Powder", "Fine particles", ElementType.None, Rgb(0.85f, 0.8f, 0.7f), "matter:0.6", "expansion:0.5");
            A("energy", "Energy", "Raw or magical energy", ElementType.None, Rgb(0.6f, 0.9f, 1f), "energy:1", "force:0.3");
            // Original (design spec 7.3).
            A("gravity", "Gravity", "Attraction, repulsion, gravitational force", ElementType.None, Rgb(0.25f, 0.25f, 0.4f), "attraction:0.8", "repulsion:0.4", "force:0.3");
            A("sound", "Sound", "Sound waves, acoustic manipulation", ElementType.None, Rgb(0.9f, 0.8f, 0.6f), "wave:1");
            A("metal", "Metal", "Metallic materials", ElementType.None, Rgb(0.7f, 0.7f, 0.75f), "structure:0.7", "matter:0.6");
            A("crystal", "Crystal", "Crystalline structures", ElementType.None, Rgb(0.7f, 0.9f, 0.95f), "structure:0.7", "light:0.4", "wave:0.3");
            A("magnetism", "Magnetism", "Magnetic fields", ElementType.None, Rgb(0.8f, 0.3f, 0.3f), "attraction:0.6", "repulsion:0.6", "energy:0.3");
            A("decay", "Decay", "Degradation of matter", ElementType.None, Rgb(0.45f, 0.4f, 0.2f), "decay:1", "time:0.3");
            A("growth", "Growth", "Accelerated growth", ElementType.None, Rgb(0.5f, 0.9f, 0.4f), "life:0.8", "expansion:0.6");
            A("reflection", "Reflection", "Reflection of energy or effects", ElementType.None, Rgb(0.85f, 0.9f, 1f), "reflection:1");
            A("friction", "Friction", "Resistance between surfaces", ElementType.None, Rgb(0.75f, 0.55f, 0.4f), "suppression:0.4", "thermal:0.3", "binding:0.3");
            A("momentum", "Momentum", "Motion and momentum", ElementType.None, Rgb(0.95f, 0.65f, 0.35f), "motion:0.8", "force:0.6");
            A("scent", "Scent", "Odors and chemical traces", ElementType.None, Rgb(0.85f, 0.7f, 0.85f), "sense:0.8", "mind:0.3");
            A("soul", "Soul", "Soul energy and spiritual interaction", ElementType.None, Rgb(0.7f, 0.8f, 1f), "soul:1");
            A("time", "Time", "Temporal influence", ElementType.None, Rgb(0.95f, 0.85f, 0.5f), "time:1");
            A("dream", "Dream", "Dream manipulation", ElementType.None, Rgb(0.55f, 0.45f, 0.85f), "mind:0.8", "shadow:0.2");
            A("null", "Null", "Suppression or absence of magical effects", ElementType.None, Rgb(0.15f, 0.15f, 0.15f), "suppression:1");
            // Added so the combinations of spec section 19 (Soul Chain, Eternal Bloom) are buildable.
            A("binding", "Binding", "Restraint, chains and connection", ElementType.None, Rgb(0.55f, 0.45f, 0.35f), "binding:1", "containment:0.4");
            A("restoration", "Restoration", "Renewal, mending and recovery", ElementType.Holy, Rgb(0.6f, 1f, 0.75f), "life:0.8", "time:0.3");
            return list;
        }

        // ------------------------------------------------------------------ runes and concepts
        private sealed class RuneBuilder
        {
            public readonly RuneDef def;

            public RuneBuilder(RuneDef def) { this.def = def; }

            /// <summary>Adds the concept '&lt;rune&gt;.&lt;key&gt;'.</summary>
            public RuneBuilder C(string key, string name, RuneRarity rarity, float complexity, string description, params string[] aspects)
            {
                def.concepts.Add(new ConceptDef
                {
                    id = def.id + "." + key,
                    displayName = name,
                    description = description,
                    rarity = rarity,
                    complexity = complexity,
                    aspects = W(aspects),
                });
                return this;
            }
        }

        private static RuneBuilder Rune(List<RuneDef> into, string id, string name, RuneRarity rarity, string properties, params string[] tags)
        {
            var def = new RuneDef
            {
                id = id,
                displayName = name,
                kind = RuneKind.Base,
                alignments = new List<string> { id },
                rarity = rarity,
                magicalProperties = properties,
                tags = new List<string>(tags),
            };
            into.Add(def);
            return new RuneBuilder(def);
        }

        public static List<RuneDef> Runes()
        {
            var r = new List<RuneDef>();
            const RuneRarity Co = RuneRarity.Common, Un = RuneRarity.Uncommon, Ra = RuneRarity.Rare, Ep = RuneRarity.Epic, Le = RuneRarity.Legendary;

            // ============================================================ novel-inspired runes (spec 7.2)
            // Greater Wind (spec 8.1, verbatim).
            Rune(r, "wind", "Wind", Co, "Light and quick: weak alone, strong when it moves or holds force.", "mobile", "light")
                .C("movement", "Movement", Co, 0.2f, "Manipulate the movement of air", "motion:1", "fluid:0.2")
                .C("flow", "Flow", Co, 0.25f, "Control direction and continuity of airflow", "motion:0.8", "focus:0.3", "fluid:0.2")
                .C("pressure", "Pressure", Un, 0.45f, "Increase or decrease air pressure", "pressure:1", "force:0.5", "containment:0.3")
                .C("compression", "Compression", Un, 0.45f, "Compress air into a smaller volume", "compression:1", "pressure:0.5", "containment:0.3")
                .C("turbulence", "Turbulence", Un, 0.4f, "Create chaotic airflow", "disruption:0.8", "motion:0.5")
                .C("vortex", "Vortex", Ra, 0.55f, "Create rotational airflow", "rotation:1", "motion:0.4", "attraction:0.3")
                .C("acceleration", "Acceleration", Un, 0.35f, "Increase speed of moving air", "motion:0.8", "energy:0.3", "force:0.3")
                .C("resistance", "Resistance", Co, 0.3f, "Increase resistance against movement", "suppression:0.5", "containment:0.4", "binding:0.3")
                .C("propulsion", "Propulsion", Un, 0.4f, "Generate force through directed airflow", "force:0.8", "motion:0.6", "repulsion:0.3")
                .C("vacuum", "Vacuum", Ra, 0.6f, "Create an area of reduced air pressure", "attraction:0.6", "suppression:0.5", "pressure:0.4", "containment:0.3");

            // Greater Fire (spec 8.2, verbatim).
            Rune(r, "fire", "Fire", Co, "Volatile and destructive: spreads by itself once lit.", "volatile", "destructive")
                .C("combustion", "Combustion", Co, 0.3f, "Initiate or sustain burning", "combustion:1", "thermal:0.5")
                .C("ignition", "Ignition", Co, 0.25f, "Cause a material to begin burning", "combustion:0.8", "energy:0.3", "focus:0.2")
                .C("expansion", "Expansion", Un, 0.4f, "Expand flames or thermal effects", "expansion:1", "thermal:0.4", "force:0.2")
                .C("temperature", "Temperature", Un, 0.4f, "Manipulate thermal intensity", "thermal:1", "focus:0.2")
                .C("fuel", "Fuel", Un, 0.35f, "Influence combustible material", "combustion:0.6", "matter:0.5", "energy:0.3")
                .C("plasma", "Plasma", Ep, 0.8f, "Manipulate highly energized matter", "energy:1", "thermal:0.7", "matter:0.4")
                .C("incineration", "Incineration", Ra, 0.6f, "Destroy material through intense heat", "thermal:0.9", "disruption:0.5", "decay:0.3")
                .C("flame_propagation", "Flame Propagation", Ra, 0.55f, "Control how fire spreads", "combustion:0.6", "expansion:0.5", "motion:0.4");

            // Greater Ash (concepts proposed; Combustion is the one spec 11.2 and 18 use).
            Rune(r, "ash", "Ash", Co, "What burning leaves: smothers, clouds and smoulders.", "particulate", "smouldering")
                .C("combustion", "Combustion", Co, 0.35f, "Keep embers smouldering inside drifting ash", "combustion:0.8", "thermal:0.4", "matter:0.2")
                .C("smother", "Smother", Un, 0.35f, "Starve flames and lungs of air", "suppression:0.7", "containment:0.4", "matter:0.2")
                .C("cinders", "Cinders", Co, 0.3f, "Scatter glowing embers that burn on contact", "thermal:0.6", "combustion:0.4", "motion:0.3")
                .C("dispersion", "Dispersion", Co, 0.25f, "Spread ash as a drifting cloud", "expansion:0.8", "matter:0.3", "fluid:0.2")
                .C("residue", "Residue", Un, 0.4f, "Reduce what burns to dust and nothing more", "decay:0.6", "matter:0.5")
                .C("obscuring_cloud", "Obscuring Cloud", Un, 0.35f, "Blind and choke with a thick cloud", "shadow:0.6", "expansion:0.4", "suppression:0.3");

            // Greater Vibration (spec 8.3, verbatim).
            Rune(r, "vibration", "Vibration", Un, "Invisible and penetrating: travels through anything solid.", "penetrating")
                .C("oscillation", "Oscillation", Co, 0.3f, "Create repetitive movement", "wave:1", "motion:0.3")
                .C("resonance", "Resonance", Un, 0.5f, "Amplify vibration through compatible frequencies", "wave:1", "energy:0.4", "expansion:0.3")
                .C("frequency", "Frequency", Un, 0.45f, "Manipulate vibration frequency", "wave:0.8", "focus:0.5")
                .C("amplitude", "Amplitude", Co, 0.35f, "Increase or decrease vibration strength", "wave:0.8", "force:0.5")
                .C("disruption", "Disruption", Un, 0.45f, "Destabilize a structure or pattern", "disruption:1", "wave:0.5")
                .C("harmonics", "Harmonics", Ra, 0.6f, "Create complex vibration patterns", "wave:0.8", "binding:0.3", "focus:0.3")
                .C("wave_propagation", "Wave Propagation", Un, 0.4f, "Control how vibrations travel", "wave:0.7", "motion:0.5", "expansion:0.4")
                .C("structural_weakness", "Structural Weakness", Ra, 0.55f, "Exploit structural weaknesses through vibration", "disruption:0.8", "structure:0.4", "focus:0.4", "wave:0.3");

            Rune(r, "heat", "Heat", Co, "Burns without flame; slow to build, slow to fade.", "lingering")
                .C("warmth", "Warmth", Co, 0.2f, "Raise the temperature of a target or an area", "thermal:0.8", "life:0.2")
                .C("conduction", "Conduction", Un, 0.35f, "Pass heat through whatever touches", "thermal:0.7", "binding:0.3", "motion:0.2")
                .C("radiation", "Radiation", Un, 0.4f, "Send heat across a distance", "thermal:0.8", "expansion:0.4", "light:0.2")
                .C("convection", "Convection", Un, 0.4f, "Move heat with rising air or currents", "thermal:0.6", "motion:0.5", "fluid:0.3")
                .C("thermal_shock", "Thermal Shock", Ra, 0.55f, "Crack a material with a sudden change of temperature", "thermal:0.6", "disruption:0.6", "structure:0.3")
                .C("searing", "Searing", Co, 0.3f, "Burn a surface with concentrated heat", "thermal:0.8", "focus:0.4")
                .C("heat_retention", "Heat Retention", Un, 0.3f, "Hold heat in and keep cold out", "thermal:0.5", "containment:0.6");

            Rune(r, "shadow", "Shadow", Un, "Strongest where light is weakest; fades in bright places.", "light-sensitive")
                .C("darkness", "Darkness", Co, 0.3f, "Deepen darkness and smother light", "shadow:1", "suppression:0.3")
                .C("concealment", "Concealment", Un, 0.4f, "Hide a target inside shadow", "shadow:0.8", "containment:0.3", "mind:0.2")
                .C("umbral_step", "Umbral Step", Ra, 0.65f, "Pass from one shadow to another", "shadow:0.6", "spatial:0.6", "motion:0.3")
                .C("shade_binding", "Shade Binding", Ra, 0.55f, "Pin a target by its own shadow", "shadow:0.6", "binding:0.7")
                .C("silhouette", "Silhouette", Un, 0.5f, "Cast false shapes and decoys", "shadow:0.6", "reflection:0.4", "mind:0.3")
                .C("light_devouring", "Light Devouring", Ra, 0.55f, "Swallow light and the energy it carries", "shadow:0.6", "absorption:0.7")
                .C("dread", "Dread", Un, 0.45f, "Fill a mind with fear of the dark", "mind:0.7", "shadow:0.5", "suppression:0.2");

            Rune(r, "shift", "Shift", Ra, "Never quite where it was: hard to hold steady.", "unstable")
                .C("displacement", "Displacement", Un, 0.45f, "Move a target slightly out of its place", "spatial:0.7", "motion:0.5")
                .C("exchange", "Exchange", Ra, 0.65f, "Swap the places or properties of two things", "spatial:0.6", "reflection:0.4", "binding:0.3")
                .C("phase", "Phase", Ra, 0.7f, "Slip between states to pass through matter", "spatial:0.5", "matter:0.4", "disruption:0.4")
                .C("transition", "Transition", Un, 0.45f, "Turn one state into another gradually", "matter:0.5", "time:0.4", "motion:0.2")
                .C("reversal", "Reversal", Ra, 0.6f, "Invert a direction, a force or a state", "reflection:0.6", "repulsion:0.4", "time:0.3")
                .C("misalignment", "Misalignment", Un, 0.45f, "Offset a pattern so it no longer fits together", "disruption:0.7", "spatial:0.4");

            Rune(r, "water", "Water", Co, "Yields and returns; takes the shape it is given.", "adaptable")
                .C("flow", "Flow", Co, 0.25f, "Guide water where it should go", "fluid:1", "motion:0.5")
                .C("hydraulic_pressure", "Hydraulic Pressure", Un, 0.45f, "Press water with crushing force", "pressure:0.8", "fluid:0.7", "force:0.4")
                .C("saturation", "Saturation", Co, 0.3f, "Soak a target or an area", "fluid:0.8", "expansion:0.3", "suppression:0.2")
                .C("tide", "Tide", Un, 0.4f, "Push and pull in waves", "fluid:0.6", "wave:0.5", "attraction:0.3", "repulsion:0.3")
                .C("cohesion", "Cohesion", Un, 0.4f, "Hold droplets together into shapes", "fluid:0.6", "binding:0.6", "structure:0.2")
                .C("cleansing", "Cleansing", Co, 0.3f, "Wash away dirt, poison and stains", "fluid:0.7", "life:0.3", "focus:0.2")
                .C("current", "Current", Co, 0.3f, "Make a steady stream that carries things", "fluid:0.6", "motion:0.7");

            Rune(r, "ice", "Ice", Co, "Hard but brittle; slows everything it touches.", "brittle", "slowing")
                .C("freezing", "Freezing", Co, 0.3f, "Turn water and warmth to ice", "cold:1", "structure:0.3")
                .C("crystallization", "Crystallization", Un, 0.45f, "Grow ordered ice crystals", "cold:0.6", "structure:0.7", "compression:0.2")
                .C("brittleness", "Brittleness", Un, 0.45f, "Make a frozen material crack and shatter", "cold:0.6", "disruption:0.5", "structure:0.3")
                .C("preservation", "Preservation", Un, 0.35f, "Keep something unchanged in cold", "cold:0.6", "time:0.4", "containment:0.3")
                .C("chill", "Chill", Co, 0.25f, "Slow bodies with numbing cold", "cold:0.7", "suppression:0.4")
                .C("ice_formation", "Ice Formation", Un, 0.4f, "Raise walls, spikes and bridges of ice", "cold:0.6", "structure:0.7", "matter:0.4")
                .C("frost_lock", "Frost Lock", Ra, 0.5f, "Freeze a target in place", "cold:0.7", "binding:0.7", "containment:0.3");

            Rune(r, "earth", "Earth", Co, "Slow and immovable; strongest standing on the ground.", "heavy", "grounded")
                .C("solidity", "Solidity", Co, 0.25f, "Make ground and stone hard and dense", "structure:0.9", "matter:0.5")
                .C("tremor", "Tremor", Un, 0.4f, "Shake the ground", "wave:0.7", "disruption:0.4", "force:0.3")
                .C("upheaval", "Upheaval", Un, 0.45f, "Raise or heave the ground", "force:0.6", "matter:0.6", "motion:0.3")
                .C("compaction", "Compaction", Co, 0.3f, "Press earth into a denser mass", "compression:0.8", "matter:0.5", "pressure:0.3")
                .C("burial", "Burial", Ra, 0.55f, "Sink or entomb a target in earth", "containment:0.7", "matter:0.5", "binding:0.3")
                .C("weight_of_stone", "Weight of Stone", Un, 0.4f, "Give a target the heaviness of rock", "attraction:0.4", "matter:0.5", "binding:0.4")
                .C("fissure", "Fissure", Un, 0.45f, "Split the ground open", "disruption:0.7", "force:0.4", "spatial:0.2");

            Rune(r, "gale", "Gale", Un, "Wind at its most violent: hard to aim, impossible to ignore.", "violent")
                .C("storm_force", "Storm Force", Un, 0.45f, "Hit with the full strength of a storm", "force:0.9", "motion:0.5")
                .C("sweep", "Sweep", Co, 0.35f, "Throw everything in an area aside", "repulsion:0.7", "expansion:0.5", "motion:0.4")
                .C("updraft", "Updraft", Co, 0.3f, "Lift with a rising blast of wind", "motion:0.6", "repulsion:0.4", "pressure:0.3")
                .C("shearing_wind", "Shearing Wind", Ra, 0.55f, "Cut with winds moving in opposite directions", "focus:0.6", "motion:0.6", "disruption:0.4")
                .C("squall", "Squall", Un, 0.4f, "Burst out in sudden violent gusts", "force:0.6", "energy:0.4", "expansion:0.4")
                .C("eye_of_the_storm", "Eye of the Storm", Ra, 0.6f, "Hold a calm centre inside the fury", "containment:0.6", "rotation:0.5", "suppression:0.3");

            Rune(r, "lightning", "Lightning", Un, "Instant and erratic: seeks whatever conducts.", "fast", "erratic")
                .C("discharge", "Discharge", Co, 0.35f, "Release stored charge as a bolt", "energy:1", "focus:0.4", "motion:0.3")
                .C("conduction", "Conduction", Un, 0.4f, "Run current through what conducts", "energy:0.7", "binding:0.4", "motion:0.3")
                .C("arcing", "Arcing", Ra, 0.55f, "Leap from one target to the next", "energy:0.7", "motion:0.5", "expansion:0.4")
                .C("overcharge", "Overcharge", Ra, 0.6f, "Push a target past what it can hold", "energy:0.9", "disruption:0.5")
                .C("paralysis", "Paralysis", Un, 0.45f, "Lock muscles with a jolt", "energy:0.6", "binding:0.5", "suppression:0.4")
                .C("ionization", "Ionization", Ra, 0.55f, "Charge the air until it glows and conducts", "energy:0.6", "expansion:0.4", "light:0.3");

            Rune(r, "light", "Light", Un, "Reveals and purifies; weak against what has no shadow.", "revealing")
                .C("illumination", "Illumination", Co, 0.2f, "Fill an area with light", "light:1", "expansion:0.3")
                .C("radiance", "Radiance", Un, 0.45f, "Burn with intense light", "light:0.8", "thermal:0.4", "energy:0.3")
                .C("revelation", "Revelation", Un, 0.45f, "Expose what is hidden", "light:0.6", "sense:0.7")
                .C("refraction", "Refraction", Ra, 0.55f, "Bend light to hide or deceive", "light:0.6", "reflection:0.5", "mind:0.3")
                .C("focus", "Focus", Un, 0.4f, "Concentrate light into a beam", "light:0.7", "focus:0.8", "energy:0.3")
                .C("purification", "Purification", Ra, 0.5f, "Burn away corruption and darkness", "light:0.7", "suppression:0.4", "life:0.3")
                .C("blinding_flash", "Blinding Flash", Co, 0.3f, "Dazzle with a sudden flash", "light:0.8", "disruption:0.4", "sense:0.2");

            // Greater Space (spec 8.4, verbatim).
            Rune(r, "space", "Space", Ra, "Precise and demanding: a small error lands far away.", "precise", "demanding")
                .C("distance", "Distance", Un, 0.5f, "Manipulate distance between locations", "spatial:1", "focus:0.2")
                .C("displacement", "Displacement", Un, 0.5f, "Move a target from its current position", "spatial:0.8", "motion:0.5")
                .C("folding", "Folding", Ep, 0.8f, "Compress spatial distance", "spatial:0.9", "compression:0.5", "motion:0.3")
                .C("expansion", "Expansion", Ra, 0.6f, "Increase spatial distance", "spatial:0.8", "expansion:0.7")
                .C("distortion", "Distortion", Ra, 0.65f, "Alter the geometry of space", "spatial:0.8", "disruption:0.6")
                .C("anchoring", "Anchoring", Un, 0.45f, "Stabilize a location", "spatial:0.6", "binding:0.7", "structure:0.3")
                .C("isolation", "Isolation", Ra, 0.6f, "Separate a space from its surroundings", "spatial:0.7", "containment:0.8", "suppression:0.3")
                .C("compression", "Compression", Ra, 0.65f, "Reduce spatial volume", "spatial:0.7", "compression:0.8", "pressure:0.3");

            Rune(r, "matter", "Matter", Un, "The stuff of things: shapes what exists rather than what moves.", "tangible")
                .C("mass", "Mass", Co, 0.3f, "Change how heavy a thing is", "matter:0.7", "attraction:0.4", "force:0.2")
                .C("density", "Density", Un, 0.4f, "Pack matter tighter or loosen it", "compression:0.6", "matter:0.7", "structure:0.3")
                .C("shaping", "Shaping", Un, 0.4f, "Mould a material into a form", "matter:0.8", "focus:0.4", "structure:0.3")
                .C("cohesion", "Cohesion", Un, 0.4f, "Hold a material together", "binding:0.6", "matter:0.6", "structure:0.4")
                .C("transmutation", "Transmutation", Ep, 0.85f, "Change one material into another", "matter:0.8", "energy:0.4", "disruption:0.3")
                .C("fragmentation", "Fragmentation", Un, 0.45f, "Break matter into pieces", "disruption:0.7", "matter:0.6", "force:0.3")
                .C("reconstitution", "Reconstitution", Ra, 0.6f, "Put broken matter back together", "matter:0.6", "binding:0.5", "time:0.3");

            Rune(r, "mind", "Mind", Ra, "Subtle: works on thinking beings, barely on anything else.", "subtle")
                .C("thought", "Thought", Co, 0.4f, "Shape and quicken thinking", "mind:0.9", "focus:0.3")
                .C("perception", "Perception", Un, 0.45f, "Widen or dull the senses", "sense:0.8", "mind:0.5")
                .C("suggestion", "Suggestion", Ra, 0.6f, "Plant an idea in another mind", "mind:0.8", "binding:0.3", "soul:0.2")
                .C("memory", "Memory", Ra, 0.55f, "Read, keep or fog memories", "mind:0.7", "time:0.5")
                .C("focus", "Focus", Un, 0.4f, "Sharpen concentration", "mind:0.6", "focus:0.8")
                .C("confusion", "Confusion", Un, 0.45f, "Scramble a target's thinking", "mind:0.7", "disruption:0.6")
                .C("telepathy", "Telepathy", Ep, 0.7f, "Link minds to share thoughts", "mind:0.8", "binding:0.5", "wave:0.2");

            Rune(r, "blood", "Blood", Ra, "Powerful but costly: draws on the life it touches.", "costly")
                .C("vitality", "Vitality", Co, 0.35f, "Strengthen the life in blood", "life:0.9", "energy:0.3")
                .C("circulation", "Circulation", Un, 0.4f, "Speed or slow the flow of blood", "life:0.6", "motion:0.5", "fluid:0.4")
                .C("coagulation", "Coagulation", Un, 0.4f, "Thicken blood to seal wounds or stop movement", "binding:0.5", "structure:0.4", "life:0.4", "fluid:0.2")
                .C("hemorrhage", "Hemorrhage", Ra, 0.55f, "Make wounds bleed", "disruption:0.5", "life:0.4", "decay:0.4", "fluid:0.3")
                .C("bloodline", "Bloodline", Ra, 0.55f, "Follow the trace of kin and spilled blood", "sense:0.6", "life:0.4", "binding:0.4")
                .C("sacrifice", "Sacrifice", Ep, 0.7f, "Spend life to gain power", "energy:0.7", "life:0.6", "absorption:0.3");

            Rune(r, "poison", "Poison", Un, "Slow but relentless: works from within.", "lingering")
                .C("toxin", "Toxin", Co, 0.3f, "Make a substance poisonous", "decay:0.8", "life:0.2")
                .C("venom", "Venom", Un, 0.4f, "Deliver poison through a wound", "decay:0.7", "focus:0.4")
                .C("paralytic", "Paralytic", Ra, 0.5f, "Numb and freeze the body", "suppression:0.6", "decay:0.4", "binding:0.4")
                .C("corrosion", "Corrosion", Un, 0.45f, "Eat away at materials", "decay:0.8", "matter:0.4")
                .C("contagion", "Contagion", Ra, 0.55f, "Spread from one victim to the next", "decay:0.6", "expansion:0.5", "motion:0.4")
                .C("antidote", "Antidote", Un, 0.4f, "Neutralize poisons", "suppression:0.6", "life:0.5")
                .C("miasma", "Miasma", Un, 0.4f, "Poison the air of an area", "decay:0.6", "expansion:0.6", "fluid:0.2");

            Rune(r, "plant", "Plant", Co, "Patient and tenacious: needs time and soil to show its strength.", "patient")
                .C("growth", "Growth", Co, 0.25f, "Make plants grow", "life:0.9", "expansion:0.4")
                .C("roots", "Roots", Co, 0.3f, "Anchor with roots that grip the ground", "binding:0.6", "life:0.4", "structure:0.3")
                .C("thorns", "Thorns", Co, 0.3f, "Grow piercing thorns", "focus:0.5", "life:0.4", "structure:0.3")
                .C("vines", "Vines", Un, 0.35f, "Entangle with living vines", "binding:0.8", "life:0.5", "motion:0.2")
                .C("photosynthesis", "Photosynthesis", Ra, 0.5f, "Turn light into living energy", "life:0.6", "light:0.5", "absorption:0.4", "energy:0.3")
                .C("spores", "Spores", Un, 0.4f, "Release a cloud of spores", "expansion:0.6", "life:0.4", "decay:0.3")
                .C("regrowth", "Regrowth", Un, 0.4f, "Regrow what was cut or burned", "life:0.7", "time:0.3", "binding:0.2");

            Rune(r, "armor", "Armor", Co, "Protects but weighs: strong defence, little else.", "protective", "heavy")
                .C("hardening", "Hardening", Co, 0.25f, "Make a surface harder", "structure:1", "compression:0.2")
                .C("plating", "Plating", Co, 0.3f, "Cover with protective plates", "structure:0.8", "containment:0.4", "matter:0.3")
                .C("layering", "Layering", Un, 0.4f, "Stack protection in layers", "structure:0.7", "containment:0.5")
                .C("deflection", "Deflection", Un, 0.4f, "Turn blows aside", "reflection:0.5", "repulsion:0.5", "structure:0.4")
                .C("impact_absorption", "Impact Absorption", Un, 0.4f, "Soak up the force of a hit", "absorption:0.7", "structure:0.5")
                .C("fortification", "Fortification", Ra, 0.5f, "Make a defence stronger and larger", "structure:0.8", "expansion:0.4", "binding:0.3");

            Rune(r, "absorption", "Absorption", Ra, "Takes rather than gives: its power comes from what it consumes.", "hungry")
                .C("draining", "Draining", Un, 0.45f, "Pull energy out of a target", "absorption:0.9", "energy:0.3", "life:0.2")
                .C("assimilation", "Assimilation", Ep, 0.75f, "Take in a property of what is absorbed", "absorption:0.7", "matter:0.4", "soul:0.3")
                .C("consumption", "Consumption", Ra, 0.55f, "Devour incoming magic", "absorption:0.8", "suppression:0.5")
                .C("siphon", "Siphon", Un, 0.45f, "Pass what is drained to yourself", "absorption:0.7", "motion:0.3", "binding:0.3")
                .C("capacity", "Capacity", Un, 0.4f, "Hold more of what is absorbed", "containment:0.7", "absorption:0.5")
                .C("neutralization", "Neutralization", Ra, 0.55f, "Absorb an effect to cancel it", "suppression:0.7", "absorption:0.6");

            Rune(r, "mist", "Mist", Co, "Everywhere and nowhere: hard to strike, hard to strike with.", "diffuse")
                .C("fog", "Fog", Co, 0.25f, "Fill the air with fog", "fluid:0.6", "shadow:0.4", "expansion:0.4")
                .C("diffusion", "Diffusion", Co, 0.3f, "Spread thinly through an area", "expansion:0.8", "fluid:0.4")
                .C("condensation", "Condensation", Un, 0.4f, "Gather mist into water", "fluid:0.7", "compression:0.6")
                .C("veil", "Veil", Un, 0.4f, "Hide behind a curtain of mist", "shadow:0.5", "containment:0.4", "fluid:0.4")
                .C("dampening", "Dampening", Un, 0.35f, "Muffle sound and smother flames", "suppression:0.7", "fluid:0.5")
                .C("haze", "Haze", Un, 0.4f, "Blur sight and sense of direction", "mind:0.4", "disruption:0.4", "fluid:0.3", "shadow:0.3");

            Rune(r, "powder", "Powder", Co, "Fine and light: little alone, much in a cloud.", "particulate")
                .C("dust", "Dust", Co, 0.25f, "Raise a cloud of fine dust", "matter:0.5", "expansion:0.5", "shadow:0.2")
                .C("grinding", "Grinding", Un, 0.4f, "Grind matter into powder", "disruption:0.6", "matter:0.6", "force:0.3")
                .C("dispersal", "Dispersal", Co, 0.3f, "Scatter powder over an area", "expansion:0.7", "motion:0.4", "matter:0.3")
                .C("volatile_dust", "Volatile Dust", Ra, 0.55f, "Make a dust cloud that can ignite", "combustion:0.6", "expansion:0.5", "matter:0.4", "energy:0.2")
                .C("abrasion", "Abrasion", Un, 0.4f, "Wear surfaces down with grit", "decay:0.4", "disruption:0.4", "matter:0.4", "motion:0.3")
                .C("sifting", "Sifting", Un, 0.35f, "Separate fine particles from coarse", "focus:0.6", "matter:0.5", "sense:0.2");

            Rune(r, "energy", "Energy", Un, "Pure power without form: strong, but needs other runes to give it shape.", "raw", "formless")
                .C("raw_power", "Raw Power", Co, 0.35f, "Release pure magical force", "energy:1", "force:0.5")
                .C("channeling", "Channeling", Un, 0.4f, "Guide energy along a path", "energy:0.7", "focus:0.5", "motion:0.3")
                .C("overload", "Overload", Ra, 0.6f, "Exceed what a target can contain", "energy:0.9", "disruption:0.6", "expansion:0.3")
                .C("conversion", "Conversion", Ra, 0.6f, "Turn one form of energy into another", "energy:0.7", "matter:0.3", "absorption:0.3")
                .C("storage", "Storage", Un, 0.4f, "Hold energy for later", "containment:0.7", "energy:0.5")
                .C("emission", "Emission", Co, 0.3f, "Throw energy out as a projectile", "energy:0.8", "motion:0.5", "focus:0.3")
                .C("amplification", "Amplification", Ra, 0.6f, "Strengthen an existing effect", "energy:0.7", "expansion:0.5", "wave:0.2");

            // ============================================================ original runes (spec 7.3)
            Rune(r, "gravity", "Gravity", Ra, "Heavy and inevitable: everything near it is affected.", "heavy", "area")
                .C("attraction", "Attraction", Un, 0.45f, "Pull things toward a point", "attraction:1", "force:0.3")
                .C("repulsion", "Repulsion", Un, 0.45f, "Push things away from a point", "repulsion:1", "force:0.4")
                .C("weight", "Weight", Co, 0.4f, "Make a target heavier or lighter", "attraction:0.6", "matter:0.4", "binding:0.3")
                .C("orbit", "Orbit", Ra, 0.6f, "Hold a target circling a centre", "rotation:0.8", "attraction:0.6", "motion:0.3")
                .C("collapse", "Collapse", Ep, 0.8f, "Crush inward under immense gravity", "attraction:0.8", "compression:0.8", "force:0.4")
                .C("weightlessness", "Weightlessness", Ra, 0.5f, "Free a target from gravity", "repulsion:0.5", "motion:0.4", "suppression:0.3");

            Rune(r, "sound", "Sound", Un, "Carries far and around corners; walls barely stop it.", "far-reaching")
                .C("echo", "Echo", Co, 0.3f, "Repeat and return a sound", "wave:0.7", "reflection:0.5")
                .C("pitch", "Pitch", Un, 0.4f, "Tune a sound high or low", "wave:0.8", "focus:0.4")
                .C("volume", "Volume", Co, 0.3f, "Make a sound louder or quieter", "wave:0.8", "force:0.4", "expansion:0.3")
                .C("silence", "Silence", Ra, 0.5f, "Cancel sound", "suppression:0.8", "wave:0.4", "containment:0.3")
                .C("sonic_wave", "Sonic Wave", Un, 0.45f, "Strike with a wave of sound", "wave:0.8", "force:0.6", "expansion:0.3")
                .C("harmony", "Harmony", Un, 0.4f, "Soothe and strengthen with consonant sound", "wave:0.6", "life:0.3", "binding:0.3", "mind:0.3")
                .C("echolocation", "Echolocation", Ra, 0.5f, "See with returning sound", "sense:0.8", "wave:0.6");

            Rune(r, "metal", "Metal", Un, "Strong and conductive; can be forged, sharpened and joined.", "conductive", "forgeable")
                .C("forging", "Forging", Un, 0.45f, "Shape metal", "matter:0.7", "structure:0.6", "thermal:0.3")
                .C("edge", "Edge", Co, 0.35f, "Sharpen to a cutting edge", "focus:0.8", "structure:0.4")
                .C("tempering", "Tempering", Un, 0.4f, "Make metal tougher", "structure:0.8", "thermal:0.3", "binding:0.2")
                .C("conductivity", "Conductivity", Un, 0.4f, "Carry heat and current through metal", "energy:0.5", "thermal:0.4", "motion:0.3")
                .C("alloying", "Alloying", Ra, 0.55f, "Blend metals into a new one", "matter:0.7", "binding:0.5")
                .C("rigidity", "Rigidity", Co, 0.3f, "Lock metal into an unbending shape", "structure:0.8", "binding:0.4");

            Rune(r, "crystal", "Crystal", Ra, "Ordered and clear: focuses and stores what passes through it.", "focusing", "brittle")
                .C("lattice", "Lattice", Un, 0.45f, "Order matter into a crystal lattice", "structure:0.9", "binding:0.4")
                .C("refraction", "Refraction", Un, 0.45f, "Split and bend light through crystal", "light:0.6", "reflection:0.6")
                .C("resonance", "Resonance", Ra, 0.55f, "Make crystal ring and store vibration", "wave:0.7", "structure:0.4", "energy:0.3")
                .C("growth", "Growth", Un, 0.4f, "Grow crystals", "structure:0.7", "expansion:0.5", "matter:0.3")
                .C("clarity", "Clarity", Un, 0.4f, "Make clear: see through, think clearly", "sense:0.5", "light:0.4", "focus:0.4")
                .C("focus", "Focus", Ra, 0.5f, "Gather energy through a crystal", "focus:0.8", "energy:0.5")
                .C("shards", "Shards", Co, 0.35f, "Shatter into cutting shards", "disruption:0.6", "focus:0.4", "structure:0.3");

            Rune(r, "magnetism", "Magnetism", Ra, "Invisible pull and push: strongest on metal and charge.", "metal-bound")
                .C("attraction", "Attraction", Un, 0.45f, "Pull metal and charged things", "attraction:0.9", "energy:0.3")
                .C("repulsion", "Repulsion", Un, 0.45f, "Push metal and charged things away", "repulsion:0.9", "energy:0.3")
                .C("polarity", "Polarity", Ra, 0.55f, "Reverse or set which way a force pulls", "attraction:0.4", "repulsion:0.4", "reflection:0.3", "focus:0.3")
                .C("field", "Field", Un, 0.45f, "Spread a magnetic field over an area", "expansion:0.6", "energy:0.5", "binding:0.3")
                .C("induction", "Induction", Ra, 0.6f, "Create current with moving fields", "energy:0.7", "motion:0.4")
                .C("alignment", "Alignment", Un, 0.4f, "Line up particles and pieces", "binding:0.6", "focus:0.5", "structure:0.3");

            Rune(r, "decay", "Decay", Ra, "Everything ends: slow, certain and hard to undo.", "inevitable")
                .C("rot", "Rot", Co, 0.35f, "Make living matter rot", "decay:0.9", "life:0.2")
                .C("erosion", "Erosion", Un, 0.4f, "Wear away over time", "decay:0.6", "time:0.4", "motion:0.3")
                .C("entropy", "Entropy", Ep, 0.75f, "Drive order into disorder", "disruption:0.7", "decay:0.6", "time:0.4")
                .C("withering", "Withering", Un, 0.45f, "Drain the life of a body", "decay:0.6", "absorption:0.4", "life:0.3")
                .C("corrosion", "Corrosion", Un, 0.45f, "Degrade metal and stone", "decay:0.8", "matter:0.5")
                .C("dissolution", "Dissolution", Ra, 0.6f, "Break a material down entirely", "decay:0.7", "disruption:0.5", "fluid:0.3");

            Rune(r, "growth", "Growth", Un, "Makes more of what is there: needs something to grow.", "amplifying")
                .C("acceleration", "Acceleration", Un, 0.4f, "Make growth happen fast", "life:0.6", "time:0.5", "energy:0.3")
                .C("proliferation", "Proliferation", Un, 0.45f, "Multiply and spread", "expansion:0.7", "life:0.5")
                .C("enlargement", "Enlargement", Co, 0.35f, "Make a target bigger", "expansion:0.8", "matter:0.4")
                .C("regeneration", "Regeneration", Ra, 0.5f, "Regrow lost tissue", "life:0.8", "time:0.3", "binding:0.3")
                .C("maturation", "Maturation", Un, 0.4f, "Bring to full development", "time:0.6", "life:0.5")
                .C("overgrowth", "Overgrowth", Un, 0.45f, "Grow out of control and smother", "expansion:0.6", "life:0.5", "binding:0.4");

            Rune(r, "reflection", "Reflection", Ra, "Returns what it receives: useless with nothing to reflect.", "reactive")
                .C("mirror", "Mirror", Co, 0.35f, "Make a surface that reflects", "reflection:0.9", "light:0.3")
                .C("rebound", "Rebound", Un, 0.45f, "Bounce a blow back", "reflection:0.7", "repulsion:0.5", "force:0.3")
                .C("redirection", "Redirection", Ra, 0.55f, "Turn an attack in a new direction", "reflection:0.6", "motion:0.5", "focus:0.3")
                .C("mirror_image", "Mirror Image", Ra, 0.55f, "Cast copies of a target", "reflection:0.7", "light:0.4", "mind:0.3")
                .C("inversion", "Inversion", Ra, 0.6f, "Turn an effect into its opposite", "reflection:0.7", "disruption:0.4")
                .C("return", "Return", Un, 0.5f, "Send an effect back to its source", "reflection:0.8", "binding:0.3", "motion:0.2");

            Rune(r, "friction", "Friction", Un, "Slows, grips and heats whatever rubs against it.", "slowing")
                .C("resistance", "Resistance", Co, 0.3f, "Make movement drag", "suppression:0.5", "binding:0.4", "structure:0.2")
                .C("heat_generation", "Heat Generation", Co, 0.3f, "Make rubbing produce heat", "thermal:0.7", "motion:0.4")
                .C("grip", "Grip", Co, 0.25f, "Hold fast to a surface", "binding:0.7", "structure:0.3")
                .C("slickness", "Slickness", Un, 0.35f, "Remove friction so things slide", "motion:0.6", "suppression:0.4", "fluid:0.2")
                .C("abrasion", "Abrasion", Un, 0.4f, "Grind a surface away", "decay:0.4", "disruption:0.4", "motion:0.4")
                .C("braking", "Braking", Un, 0.35f, "Stop movement quickly", "suppression:0.6", "binding:0.4", "motion:0.2");

            Rune(r, "momentum", "Momentum", Un, "Stronger the longer it moves; hard to stop once started.", "building")
                .C("inertia", "Inertia", Co, 0.35f, "Keep a thing moving or keep it still", "motion:0.6", "binding:0.4", "structure:0.2")
                .C("impact", "Impact", Co, 0.35f, "Hit with the full weight of motion", "force:0.9", "motion:0.4")
                .C("acceleration", "Acceleration", Un, 0.4f, "Build up speed", "motion:0.8", "energy:0.4")
                .C("transfer", "Transfer", Ra, 0.55f, "Pass momentum from one thing to another", "motion:0.6", "binding:0.4", "absorption:0.3")
                .C("redirection", "Redirection", Un, 0.45f, "Change the direction of movement", "motion:0.6", "rotation:0.4", "focus:0.3")
                .C("stopping", "Stopping", Un, 0.4f, "Kill momentum", "suppression:0.6", "absorption:0.4", "binding:0.3");

            Rune(r, "scent", "Scent", Un, "Lingers and travels with the air; speaks to instinct.", "lingering")
                .C("trail", "Trail", Co, 0.3f, "Follow a scent", "sense:0.8", "motion:0.3")
                .C("lure", "Lure", Un, 0.4f, "Draw creatures with a scent", "attraction:0.6", "mind:0.5")
                .C("repellent", "Repellent", Co, 0.3f, "Drive creatures away", "repulsion:0.6", "mind:0.4")
                .C("masking", "Masking", Un, 0.35f, "Hide a scent", "suppression:0.5", "shadow:0.3", "sense:0.3")
                .C("intoxication", "Intoxication", Ra, 0.5f, "Dull a mind with heady scent", "mind:0.6", "decay:0.3", "disruption:0.3")
                .C("identification", "Identification", Un, 0.4f, "Tell what something is by its scent", "sense:0.8", "focus:0.4");

            Rune(r, "soul", "Soul", Ep, "Touches what lives inside: dangerous to the soul that wields it.", "dangerous", "spiritual")
                .C("essence", "Essence", Un, 0.5f, "Touch the essence of a living thing", "soul:1", "life:0.3")
                .C("tether", "Tether", Ra, 0.6f, "Tie two souls together", "binding:0.7", "soul:0.6")
                .C("resonance", "Resonance", Ra, 0.6f, "Make souls answer each other", "soul:0.7", "wave:0.5")
                .C("fortitude", "Fortitude", Un, 0.45f, "Guard a soul against harm", "soul:0.6", "structure:0.5", "containment:0.3")
                .C("severance", "Severance", Ep, 0.8f, "Cut a soul from what holds it", "soul:0.6", "disruption:0.6", "focus:0.4")
                .C("communion", "Communion", Ra, 0.55f, "Speak soul to soul", "soul:0.7", "mind:0.4", "binding:0.3");

            Rune(r, "time", "Time", Le, "The rarest principle: every use is a risk.", "dangerous", "rare")
                .C("haste", "Haste", Ra, 0.6f, "Make time run faster for a target", "time:0.9", "motion:0.5")
                .C("slowness", "Slowness", Ra, 0.6f, "Make time run slower for a target", "time:0.9", "suppression:0.4")
                .C("stasis", "Stasis", Ep, 0.75f, "Stop time around a target", "time:0.9", "containment:0.6", "binding:0.4")
                .C("rewind", "Rewind", Le, 0.9f, "Turn back a small stretch of time", "time:1", "reflection:0.4")
                .C("echo", "Echo", Ep, 0.8f, "Repeat what has just happened", "time:0.8", "reflection:0.5", "wave:0.2")
                .C("foresight", "Foresight", Ep, 0.7f, "Glimpse what is about to happen", "sense:0.7", "time:0.6", "mind:0.3");
            r[r.Count - 1].slotCost = 2;

            Rune(r, "dream", "Dream", Ep, "Works through sleep and imagination; strongest on the unwary.", "subtle")
                .C("sleep", "Sleep", Un, 0.45f, "Send a target to sleep", "mind:0.7", "suppression:0.6")
                .C("illusion", "Illusion", Ra, 0.55f, "Make the unreal seem real", "mind:0.7", "light:0.3", "reflection:0.3")
                .C("nightmare", "Nightmare", Ra, 0.6f, "Torment with terrors", "mind:0.7", "shadow:0.4", "disruption:0.4")
                .C("lucidity", "Lucidity", Un, 0.45f, "Keep a clear mind against deception", "mind:0.6", "focus:0.5", "suppression:0.2")
                .C("dreamwalk", "Dreamwalk", Ep, 0.8f, "Walk into another's dreams", "mind:0.7", "spatial:0.4", "soul:0.4")
                .C("reverie", "Reverie", Un, 0.4f, "Soothe a mind into calm", "mind:0.6", "life:0.3", "suppression:0.3");

            Rune(r, "null", "Null", Ep, "Absence of magic: it weakens other runes as much as foes.", "negating")
                .C("negation", "Negation", Ra, 0.6f, "Cancel magic", "suppression:1", "absorption:0.3")
                .C("silence", "Silence", Ra, 0.55f, "Stop spellcasting", "suppression:0.8", "wave:0.3", "mind:0.2")
                .C("void", "Void", Ep, 0.75f, "Create an empty space where nothing works", "suppression:0.7", "spatial:0.6", "absorption:0.3")
                .C("dampening", "Dampening", Un, 0.45f, "Weaken magic nearby", "suppression:0.7", "expansion:0.4")
                .C("erasure", "Erasure", Ep, 0.8f, "Wipe an effect out of existence", "suppression:0.8", "disruption:0.5")
                .C("isolation", "Isolation", Ra, 0.6f, "Cut a target off from magic", "containment:0.7", "suppression:0.6");

            Rune(r, "binding", "Binding", Ra, "Holds and connects; strong against what moves, weak against what is still.", "restraining")
                .C("restraint", "Restraint", Co, 0.35f, "Hold a target back", "binding:0.9", "suppression:0.3")
                .C("chains", "Chains", Un, 0.4f, "Wrap with magical chains", "binding:0.9", "structure:0.4")
                .C("seal", "Seal", Ra, 0.55f, "Close something so it cannot open", "containment:0.8", "binding:0.6", "suppression:0.3")
                .C("anchor", "Anchor", Un, 0.4f, "Fix a target in place", "binding:0.7", "structure:0.4", "spatial:0.2")
                .C("constriction", "Constriction", Un, 0.45f, "Squeeze what is bound", "binding:0.6", "compression:0.6", "force:0.2")
                .C("link", "Link", Ra, 0.5f, "Connect two things so they share effects", "binding:0.7", "soul:0.2", "motion:0.2");

            Rune(r, "restoration", "Restoration", Ra, "Returns things to how they were; cannot create what never was.", "healing")
                .C("mending", "Mending", Co, 0.35f, "Repair what is broken", "binding:0.5", "matter:0.4", "structure:0.4", "time:0.2")
                .C("renewal", "Renewal", Un, 0.45f, "Restore strength and freshness", "life:0.7", "energy:0.4")
                .C("recovery", "Recovery", Un, 0.4f, "Help a body recover", "life:0.8", "time:0.3")
                .C("cleansing", "Cleansing", Un, 0.4f, "Remove harmful influences", "suppression:0.5", "light:0.4", "life:0.3")
                .C("revival", "Revival", Le, 0.9f, "Bring back from the brink", "life:0.8", "soul:0.6", "time:0.3")
                .C("preservation", "Preservation", Un, 0.4f, "Keep something from deteriorating", "time:0.6", "containment:0.4", "structure:0.3");

            // Colours from the alignments, so runes and their domains match in the UI.
            List<AlignmentDef> alignments = Alignments();
            foreach (RuneDef rune in r)
            {
                AlignmentDef a = alignments.Find(x => x.id == rune.alignments[0]);
                if (a != null) rune.color = a.color;
            }
            return r;
        }
    }
}
