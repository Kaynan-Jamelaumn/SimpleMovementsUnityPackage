using System.Collections.Generic;

namespace RuneMagic
{
    // Built-in synthesis data (design spec 11-13, 18, 19): the engine's numbers, the flaws a flawed result can carry, and
    // the sample outcome archetypes - the Pyroclastic Resonance adaptation of spec 18 and the ten original combinations of
    // spec 19. The archetypes are samples, not recipes: they name a result and refine its score when a formula resembles
    // them; every other combination is judged by the same evaluation. Numbers are proposals; tune them on the database.
    public static partial class RuneDefaults
    {
        public static SynthesisBalance SynthesisEngine() => new SynthesisBalance
        {
            powerCurve = new List<CurvePoint>
            {
                new CurvePoint(0f, 0.2f), new CurvePoint(30f, 0.45f), new CurvePoint(70f, 0.85f), new CurvePoint(99f, 1.2f), new CurvePoint(100f, 1.3f),
            },
            stabilityCurve = new List<CurvePoint>
            {
                new CurvePoint(0f, 0.2f), new CurvePoint(30f, 0.4f), new CurvePoint(70f, 0.75f), new CurvePoint(99f, 0.97f), new CurvePoint(100f, 1f),
            },
            costCurve = new List<CurvePoint>
            {
                new CurvePoint(0f, 2f), new CurvePoint(30f, 1.6f), new CurvePoint(70f, 1.2f), new CurvePoint(99f, 0.85f), new CurvePoint(100f, 0.8f),
            },
        };

        private static SynthesisFlawDef Flaw(string id, string name, FlawCause cause, string description, float power = 1f, float stability = 1f,
            float cost = 1f, float control = 0f, string sideEffect = "", float weight = 1f) => new SynthesisFlawDef
        {
            id = id, displayName = name, cause = cause, description = description, powerMultiplier = power, stabilityMultiplier = stability,
            costMultiplier = cost, controlPenalty = control, sideEffect = sideEffect, weight = weight,
        };

        /// <summary>Design spec 13.1: reduced power, increased energy consumption, reduced stability, hard to control, unintended secondary effects.</summary>
        public static List<SynthesisFlawDef> SynthesisFlaws() => new List<SynthesisFlawDef>
        {
            Flaw("thin", "Thin", FlawCause.Coverage, "The concepts only half provide what the effect needs: it is weaker than it should be.", power: 0.75f),
            Flaw("hollow", "Hollow", FlawCause.Coverage, "The effect is there in shape but not in substance.", power: 0.85f, stability: 0.9f, weight: 0.6f),
            Flaw("discordant", "Discordant", FlawCause.Compatibility, "The concepts pull against each other: the spell is hard to control and sometimes lashes back.",
                stability: 0.8f, control: 0.15f, sideEffect: "backlash"),
            Flaw("misdirected", "Misdirected", FlawCause.Intention, "The purpose and the effect do not quite agree: it drifts from what was intended.",
                power: 0.9f, control: 0.2f, sideEffect: "drift"),
            Flaw("disordered", "Disordered", FlawCause.Order, "The runes act in the wrong sequence: it works, but unsteadily and wastefully.",
                stability: 0.75f, cost: 1.15f),
            Flaw("wasteful", "Wasteful", FlawCause.EnergyImbalance, "The energy was spread badly: much of it is lost every time.", cost: 1.3f, weight: 1.2f),
            Flaw("lopsided", "Lopsided", FlawCause.EnergyImbalance, "One rune carries the work of several: the result leans and tires.", stability: 0.85f, cost: 1.1f, weight: 0.8f),
            Flaw("volatile", "Volatile", FlawCause.EnergyExcess, "A rune was given more than it could hold: the spell may release unpredictably.",
                stability: 0.65f, control: 0.1f, sideEffect: "unstable_release"),
            Flaw("crude", "Crude", FlawCause.Execution, "Poor runes, or concepts beyond their rank: the result is clumsy.", power: 0.9f, stability: 0.9f, control: 0.05f),
        };

        private static OutcomeArchetype Archetype(string id, string name, string intention, string effect, string description, string properties,
            bool orderMatters, params ArchetypeStage[] stages) => new OutcomeArchetype
        {
            id = id, displayName = name, intention = intention, effect = effect, description = description, resultProperties = properties,
            orderMatters = orderMatters, stages = new List<ArchetypeStage>(stages), resultRuneId = id, resultName = name,
        };

        /// <summary>
        /// Design spec 18 (Pyroclastic Resonance: 3 Wind, 1 Ash, 3 Vibration, flawless; 2 + 2 + 3 flawed) and 19 (ten original
        /// combinations). The concepts and effects are proposals chosen so that each ideal formula is coherent; none is the only
        /// way to make its result.
        /// </summary>
        public static List<OutcomeArchetype> Archetypes() => new List<OutcomeArchetype>
        {
            Archetype("pyroclastic_resonance", "Pyroclastic Resonance", "attack", "offense.explosion",
                "A rank 2 rune of resonant, ash-laden pressure, from the novel: Attack, Explosion, three Wind (Pressure), one Ash (Combustion), three Vibration (Resonance).",
                "Releases as a resonant pressure explosion that carries burning ash.", true,
                new ArchetypeStage("wind", "wind.pressure", 3, 1.0f), new ArchetypeStage("ash", "ash.combustion", 1, 0.6f),
                new ArchetypeStage("vibration", "vibration.resonance", 3, 0.8f)),
            Archetype("gravity_well", "Gravity Well", "control", "control.attract", "Gravity + Space + Matter.", "An area that pulls targets inward.", false,
                new ArchetypeStage("gravity", "gravity.attraction", 1), new ArchetypeStage("matter", "matter.mass", 1), new ArchetypeStage("space", "space.distance", 1)),
            Archetype("storm_lance", "Storm Lance", "attack", "offense.pierce", "Lightning + Wind + Momentum.", "A high-speed electrical projectile.", false,
                new ArchetypeStage("lightning", "lightning.discharge", 1), new ArchetypeStage("wind", "wind.flow", 1), new ArchetypeStage("momentum", "momentum.impact", 1)),
            Archetype("crystal_bastion", "Crystal Bastion", "defense", "defense.barrier", "Crystal + Earth + Armor.", "A durable crystalline defensive structure.", false,
                new ArchetypeStage("earth", "earth.solidity", 1), new ArchetypeStage("crystal", "crystal.growth", 1), new ArchetypeStage("armor", "armor.fortification", 1)),
            Archetype("soul_chain", "Soul Chain", "control", "control.bind", "Soul + Binding + Shadow.", "Restricts a target's soul and movement.", false,
                new ArchetypeStage("soul", "soul.tether", 1), new ArchetypeStage("shadow", "shadow.shade_binding", 1), new ArchetypeStage("binding", "binding.seal", 1)),
            Archetype("temporal_echo", "Temporal Echo", "creation", "creation.duplicate", "Time + Vibration + Space.", "Repeats a previous magical effect.", false,
                new ArchetypeStage("space", "space.distance", 1), new ArchetypeStage("time", "time.echo", 1), new ArchetypeStage("vibration", "vibration.frequency", 1)),
            Archetype("void_collapse", "Void Collapse", "destruction", "offense.implode", "Null + Space + Gravity.", "An area that suppresses and collapses magical effects.", false,
                new ArchetypeStage("null", "null.void", 1), new ArchetypeStage("space", "space.isolation", 1), new ArchetypeStage("gravity", "gravity.collapse", 1)),
            Archetype("living_forge", "Living Forge", "creation", "creation.forge", "Fire + Metal + Matter.", "Creates or reshapes metallic constructs.", false,
                new ArchetypeStage("matter", "matter.shaping", 1), new ArchetypeStage("metal", "metal.forging", 1), new ArchetypeStage("fire", "fire.temperature", 1)),
            Archetype("eternal_bloom", "Eternal Bloom", "creation", "creation.grow", "Plant + Growth + Restoration.", "Rapidly regenerating vegetation.", false,
                new ArchetypeStage("plant", "plant.growth", 1), new ArchetypeStage("restoration", "restoration.renewal", 1), new ArchetypeStage("growth", "growth.acceleration", 1)),
            Archetype("astral_ruin", "Astral Ruin", "attack", "offense.crush", "Fire + Space + Gravity + Metal.", "A powerful astral, meteor-like attack.", false,
                new ArchetypeStage("fire", "fire.plasma", 1), new ArchetypeStage("metal", "metal.forging", 1), new ArchetypeStage("gravity", "gravity.collapse", 1),
                new ArchetypeStage("space", "space.compression", 1)),
            Archetype("fractured_reality", "Fractured Reality", "movement", "movement.warp", "Space + Matter + Shift.", "A localized distortion of reality.", false,
                new ArchetypeStage("space", "space.distortion", 1), new ArchetypeStage("shift", "shift.misalignment", 1), new ArchetypeStage("matter", "matter.fragmentation", 1)),
        };
    }
}
