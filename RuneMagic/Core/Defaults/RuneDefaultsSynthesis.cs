using System.Collections.Generic;

namespace RuneMagic
{
    // Built-in intentions (design spec 9.1) and desired effects (10.1-10.6), names and descriptions verbatim. What each
    // effect requires of the selected concepts (aspects) and which effects each intention supports are proposals: they
    // make the evaluation work for any combination, and are tuned on the database.
    public static partial class RuneDefaults
    {
        private const EffectCategory Off = EffectCategory.Offensive, Def = EffectCategory.Defensive, Mov = EffectCategory.Movement,
            Ctl = EffectCategory.Control, Cre = EffectCategory.CreationTransformation, Uti = EffectCategory.UtilityExploration;

        // ------------------------------------------------------------------ intentions
        public static List<IntentionDef> Intentions()
        {
            var list = new List<IntentionDef>();
            IntentionDef I(string id, string name, string description, EffectCategory[] primary, EffectCategory[] secondary, string[] bias, params string[] overrides)
            {
                var def = new IntentionDef
                {
                    id = id,
                    displayName = name,
                    description = description,
                    primaryCategories = new List<EffectCategory>(primary ?? new EffectCategory[0]),
                    secondaryCategories = new List<EffectCategory>(secondary ?? new EffectCategory[0]),
                    aspectBias = W(bias),
                };
                // "effect:support" (support defaults to 1).
                foreach (AspectWeight o in W(overrides))
                    def.effectOverrides.Add(new EffectSupport(o.aspect, o.weight));
                list.Add(def);
                return def;
            }
            EffectCategory[] Cats(params EffectCategory[] c) => c;
            string[] B(params string[] b) => b;

            I("attack", "Attack", "Harm or damage a target", Cats(Off), Cats(Ctl), B("force:0.6", "disruption:0.5", "thermal:0.4", "energy:0.4", "focus:0.3", "decay:0.3"),
                "control.weaken:1", "control.paralyze:0.8");
            I("defense", "Defense", "Protect the player or an ally", Cats(Def), Cats(Mov, Ctl), B("structure:0.7", "containment:0.5", "reflection:0.4", "absorption:0.4"),
                "movement.blink:0.8", "movement.dash:0.6");
            I("control", "Control", "Restrict or manipulate a target", Cats(Ctl), Cats(Mov, Off), B("binding:0.7", "suppression:0.5", "mind:0.4", "cold:0.3", "attraction:0.3"),
                "movement.pull:1", "movement.push:1", "movement.levitate:0.8", "movement.orbit:0.8", "defense.seal:0.6");
            I("movement", "Movement", "Alter movement or positioning", Cats(Mov), Cats(Ctl), B("motion:0.8", "spatial:0.5", "repulsion:0.3"));
            I("creation", "Creation", "Create something new", Cats(Cre), Cats(Def, Uti), B("matter:0.7", "structure:0.5", "life:0.4", "binding:0.3"));
            I("destruction", "Destruction", "Destroy a target or structure", Cats(Off), Cats(Cre), B("disruption:0.8", "decay:0.5", "force:0.5", "energy:0.3"),
                "creation.decompose:1", "utility.dispel:0.6");
            I("transformation", "Transformation", "Change properties of a target", Cats(Cre), Cats(Uti), B("matter:0.6", "energy:0.4", "disruption:0.3", "time:0.3"),
                "control.weaken:0.5");
            I("restoration", "Restoration", "Restore a target to a previous state", null, null, B("life:0.8", "time:0.4", "binding:0.3", "light:0.3"),
                "utility.restore:1", "utility.cleanse:1", "creation.reassemble:1", "creation.purify:1", "creation.grow:0.7", "defense.reinforce:0.7",
                "defense.stabilize:0.7", "utility.preserve:0.6");
            I("suppression", "Suppression", "Reduce or disable an ability", Cats(Ctl), null, B("suppression:0.9", "absorption:0.4", "containment:0.3"),
                "defense.nullify:1", "utility.dispel:1", "defense.counter:0.7", "defense.insulate:0.6");
            I("enhancement", "Enhancement", "Improve a target's capabilities", null, null, B("energy:0.5", "life:0.4", "structure:0.4", "expansion:0.3"),
                "defense.reinforce:1", "defense.fortify:1", "defense.harden:1", "movement.accelerate:1", "creation.refine:1", "creation.grow:0.8",
                "creation.expand:0.7", "defense.shield:0.6", "defense.stabilize:0.6", "offense.overload:0.5");
            I("concealment", "Concealment", "Hide the player or an object", null, null, B("shadow:0.7", "suppression:0.4", "mind:0.3", "fluid:0.2"),
                "utility.conceal:1", "control.disorient:0.6", "movement.phase:0.6", "movement.blink:0.5", "defense.disperse:0.5");
            I("detection", "Detection", "Reveal hidden targets or information", null, null, B("sense:0.8", "light:0.4", "mind:0.4", "wave:0.3"),
                "utility.reveal:1", "utility.track:1", "utility.scan:1", "utility.sense:1", "utility.detect:1", "utility.identify:1", "utility.locate:1",
                "utility.illuminate:0.7", "utility.mark:0.6");
            I("manipulation", "Manipulation", "Directly manipulate an object or force", Cats(Mov), Cats(Ctl, Cre), B("motion:0.5", "attraction:0.4", "repulsion:0.4", "force:0.4", "matter:0.3"));
            I("binding", "Binding", "Restrict a target's movement or abilities", Cats(Ctl), null, B("binding:0.9", "containment:0.5", "attraction:0.3"),
                "defense.seal:1", "utility.seal:1", "movement.orbit:0.5", "utility.mark:0.5");
            I("absorption", "Absorption", "Absorb energy or magical effects", null, null, B("absorption:0.9", "containment:0.3", "energy:0.3"),
                "defense.absorb:1", "offense.drain:1", "utility.extract:1", "utility.transfer:0.8", "defense.nullify:0.7", "creation.convert:0.6");
            I("replication", "Replication", "Reproduce an object or magical pattern", null, null, B("reflection:0.6", "matter:0.4", "time:0.3"),
                "creation.duplicate:1", "utility.record:1", "defense.reflect:0.7", "creation.reassemble:0.6");
            I("summoning", "Summoning", "Bring a creature or entity to a location", null, null, B("spatial:0.5", "soul:0.4", "life:0.4", "matter:0.3"),
                "creation.construct:1", "creation.animate:1", "movement.pull:0.6", "movement.teleport:0.6", "creation.grow:0.5");
            I("imbuement", "Imbuement", "Transfer a magical property into an object", null, null, B("binding:0.5", "energy:0.5", "focus:0.4"),
                "utility.transfer:1", "utility.mark:1", "creation.refine:1", "defense.reinforce:0.8", "defense.fortify:0.7", "defense.shield:0.6");
            I("displacement", "Displacement", "Move a target from one location to another", null, null, B("spatial:0.8", "motion:0.5"),
                "movement.teleport:1", "movement.blink:1", "movement.warp:1", "movement.reposition:1", "movement.push:1", "movement.pull:1",
                "movement.launch:0.8", "movement.propel:0.8", "movement.phase:0.7", "movement.dash:0.6", "control.repel:0.7", "control.attract:0.7");
            I("alteration", "Alteration", "Change structure or behavior of something", Cats(Cre), Cats(Ctl), B("matter:0.5", "disruption:0.4", "structure:0.3", "time:0.3"),
                "movement.warp:0.5");
            I("interference", "Interference", "Disrupt another magical effect", null, null, B("disruption:0.7", "suppression:0.6", "wave:0.3"),
                "utility.dispel:1", "defense.nullify:1", "defense.counter:1", "control.suppress:1", "control.disorient:0.8", "control.silence:0.8",
                "defense.disperse:0.7", "offense.overload:0.6");
            I("sealing", "Sealing", "Prevent something from being accessed or activated", null, null, B("containment:0.8", "binding:0.6", "suppression:0.4"),
                "defense.seal:1", "utility.seal:1", "control.confine:1", "control.bind:0.8", "control.restrict:0.8", "utility.preserve:0.6", "defense.barrier:0.6");
            I("amplification", "Amplification", "Increase power of an existing effect", null, null, B("energy:0.6", "expansion:0.5", "wave:0.4", "focus:0.3"),
                "offense.overload:1", "offense.chain_reaction:1", "defense.fortify:0.8", "creation.expand:0.8", "creation.grow:0.6", "movement.accelerate:0.6",
                "offense.detonate:0.6", "offense.explosion:0.5");
            I("deception", "Deception", "Create misleading magical information", null, null, B("mind:0.6", "shadow:0.4", "reflection:0.4", "light:0.3"),
                "utility.conceal:1", "control.disorient:1", "control.distract:1", "creation.duplicate:0.8", "movement.blink:0.5", "utility.mark:0.4");
            I("exploration", "Exploration", "Reveal or interact with unknown environments", Cats(Uti), Cats(Mov), B("sense:0.6", "motion:0.4", "light:0.4", "spatial:0.3"));
            I("purification", "Purification", "Remove unwanted properties or influences", null, null, B("light:0.6", "suppression:0.5", "fluid:0.3", "life:0.3"),
                "creation.purify:1", "utility.cleanse:1", "utility.dispel:1", "defense.nullify:0.7", "creation.refine:0.6", "utility.restore:0.6");
            I("communication", "Communication", "Transmit information", null, null, B("mind:0.6", "wave:0.5", "soul:0.3", "binding:0.3"),
                "utility.communicate:1", "utility.mark:0.8", "utility.record:0.8", "utility.sense:0.5");
            I("perception", "Perception", "Alter or enhance sensory capabilities", null, null, B("sense:0.9", "mind:0.4", "light:0.3"),
                "utility.sense:1", "utility.reveal:1", "utility.detect:1", "utility.scan:1", "utility.track:1", "utility.identify:0.8", "utility.locate:0.8",
                "utility.illuminate:0.6");
            I("preservation", "Preservation", "Prevent degradation or destruction", null, null, B("time:0.6", "containment:0.5", "cold:0.4", "structure:0.4"),
                "utility.preserve:1", "defense.stabilize:1", "defense.reinforce:0.8", "defense.harden:0.8", "defense.insulate:0.8", "creation.solidify:0.7",
                "creation.crystallize:0.7", "defense.seal:0.6");
            I("extraction", "Extraction", "Remove a specific property or substance", null, null, B("absorption:0.6", "attraction:0.4", "disruption:0.3", "focus:0.3"),
                "utility.extract:1", "offense.drain:1", "utility.transfer:0.8", "creation.decompose:0.7", "creation.condense:0.5");
            I("conversion", "Conversion", "Transform energy or magical properties", null, null, B("energy:0.6", "absorption:0.4", "matter:0.4"),
                "creation.convert:1", "creation.transmute:1", "creation.liquefy:1", "creation.solidify:1", "creation.condense:0.8", "creation.crystallize:0.8",
                "defense.absorb:0.6");
            return list;
        }

        // ------------------------------------------------------------------ desired effects
        public static List<DesiredEffectDef> Effects()
        {
            var list = new List<DesiredEffectDef>();
            string Prefix(EffectCategory c)
            {
                switch (c)
                {
                    case Off: return "offense";
                    case Def: return "defense";
                    case Mov: return "movement";
                    case Ctl: return "control";
                    case Cre: return "creation";
                    default: return "utility";
                }
            }
            float Power(EffectCategory c) => c == Off || c == Cre ? 1f : c == Def ? 0.9f : c == Ctl ? 0.8f : c == Mov ? 0.7f : 0.5f;
            // Requirements: "a|b|c" strings, each needing 0.5 of any listed aspect from the selected concepts together.
            DesiredEffectDef E(EffectCategory c, string key, string name, string description, params string[] requirements)
            {
                var e = new DesiredEffectDef
                {
                    id = Prefix(c) + "." + key,
                    displayName = name,
                    category = c,
                    description = description,
                    powerDemand = Power(c),
                    complexity = 0.4f,
                };
                foreach (string req in requirements)
                    e.required.Add(Req(req));
                list.Add(e);
                return e;
            }

            // Offensive (10.1).
            E(Off, "explosion", "Explosion", "Explosive release of energy", "pressure|compression|containment", "combustion|thermal|energy|wave", "expansion|force|wave")
                .Tune(1.3f, 0.6f, "expansion:0.5", "wave:0.4");
            E(Off, "pierce", "Pierce", "Penetrate a target", "focus|compression", "force|motion").Tune(1f, 0.4f, "motion:0.3");
            E(Off, "slash", "Slash", "Cutting effect", "focus", "motion|force").Tune(1f, 0.35f, "structure:0.3");
            E(Off, "crush", "Crush", "Apply destructive pressure", "matter|structure|attraction", "pressure|force|compression").Tune(1.1f, 0.4f);
            E(Off, "incinerate", "Incinerate", "Burn a target intensely", "thermal|combustion", "focus|energy|expansion").Tune(1.2f, 0.5f, "combustion:0.4");
            E(Off, "disintegrate", "Disintegrate", "Break a target down", "disruption|decay", "energy|wave|matter").Tune(1.5f, 0.75f);
            E(Off, "detonate", "Detonate", "Trigger a sudden destructive reaction", "containment|pressure|energy|combustion", "disruption|expansion").Tune(1.2f, 0.55f);
            E(Off, "erode", "Erode", "Gradually destroy a target", "decay|fluid|motion", "matter|structure|time").Tune(0.8f, 0.4f);
            E(Off, "shatter", "Shatter", "Break a target into fragments", "cold|wave|structure", "force|disruption").Tune(1.1f, 0.45f);
            E(Off, "rend", "Rend", "Tear a target apart", "force|motion|rotation", "disruption|focus").Tune(1.1f, 0.45f);
            E(Off, "overload", "Overload", "Exceed a target's structural or magical capacity", "energy", "expansion|disruption|absorption").Tune(1.3f, 0.6f);
            E(Off, "corrode", "Corrode", "Degrade a material", "decay", "matter|fluid").Tune(0.8f, 0.35f);
            E(Off, "implode", "Implode", "Collapse a target inward", "spatial|containment", "attraction|compression").Tune(1.4f, 0.7f);
            E(Off, "chain_reaction", "Chain Reaction", "Trigger consecutive effects", "energy|combustion|wave", "expansion|motion|disruption").Tune(1.3f, 0.65f);
            E(Off, "pulverize", "Pulverize", "Reduce a target to smaller fragments", "force|wave|pressure", "matter|structure|disruption").Tune(1.2f, 0.45f);
            E(Off, "sever", "Sever", "Separate a target or its components", "focus|spatial", "disruption|force").Tune(1.1f, 0.5f);
            E(Off, "infect", "Infect", "Introduce a harmful magical influence", "decay|mind|soul", "binding|motion|expansion").Tune(0.9f, 0.5f);
            E(Off, "drain", "Drain", "Remove energy from a target", "absorption", "life|energy|soul").Tune(0.9f, 0.45f);
            E(Off, "rupture", "Rupture", "Cause a target to break open", "pressure|expansion", "disruption|force").Tune(1.1f, 0.45f);
            E(Off, "penetrate", "Penetrate", "Pass through a target's defenses", "focus|spatial|motion", "suppression|force|disruption").Tune(1f, 0.5f);

            // Defensive (10.2).
            E(Def, "barrier", "Barrier", "Create a protective barrier", "structure|containment|matter", "expansion|spatial").Tune(0.9f, 0.4f);
            E(Def, "shield", "Shield", "Protect a specific target", "structure|containment|energy", "focus|binding").Tune(0.8f, 0.35f);
            E(Def, "deflect", "Deflect", "Redirect incoming attacks", "motion|repulsion|reflection", "force|rotation").Tune(0.8f, 0.4f);
            E(Def, "absorb", "Absorb", "Consume incoming energy", "absorption", "containment|structure").Tune(0.9f, 0.45f);
            E(Def, "reinforce", "Reinforce", "Strengthen a structure or body", "structure|binding", "matter|life|energy").Tune(0.8f, 0.35f);
            E(Def, "reflect", "Reflect", "Return an incoming effect", "reflection", "focus|structure|light").Tune(1f, 0.55f);
            E(Def, "nullify", "Nullify", "Cancel a magical effect", "suppression|absorption", "focus|containment").Tune(1f, 0.55f);
            E(Def, "fortify", "Fortify", "Improve defensive resistance", "structure", "expansion|binding|life").Tune(0.8f, 0.35f);
            E(Def, "stabilize", "Stabilize", "Prevent disruption or collapse", "binding|structure|containment", "time|focus|suppression").Tune(0.8f, 0.4f);
            E(Def, "disperse", "Disperse", "Spread out or weaken an incoming effect", "expansion|motion|disruption", "fluid|wave|rotation").Tune(0.8f, 0.4f);
            E(Def, "harden", "Harden", "Increase physical durability", "structure|compression|cold", "matter").Tune(0.7f, 0.3f);
            E(Def, "insulate", "Insulate", "Prevent transfer of energy", "containment|structure", "suppression|absorption").Tune(0.7f, 0.35f);
            E(Def, "seal", "Seal", "Prevent penetration or entry", "containment|binding", "structure|suppression").Tune(0.9f, 0.45f);
            E(Def, "counter", "Counter", "Intercept or neutralize an incoming effect", "reflection|suppression|focus", "time|motion|disruption").Tune(1f, 0.55f);

            // Movement (10.3).
            E(Mov, "dash", "Dash", "Move rapidly over a short distance", "motion", "force|focus").Tune(0.6f, 0.25f);
            E(Mov, "teleport", "Teleport", "Instantly relocate", "spatial", "focus|motion").Tune(1.4f, 0.8f);
            E(Mov, "propel", "Propel", "Generate movement force", "force|motion|repulsion", "pressure|energy").Tune(0.7f, 0.3f);
            E(Mov, "levitate", "Levitate", "Suspend a target in the air", "repulsion|pressure|motion", "binding|containment").Tune(0.8f, 0.45f);
            E(Mov, "accelerate", "Accelerate", "Increase movement speed", "motion|time", "energy|force").Tune(0.7f, 0.35f);
            E(Mov, "decelerate", "Decelerate", "Reduce movement speed", "motion|time|suppression", "binding|containment|cold").Tune(0.7f, 0.35f);
            E(Mov, "blink", "Blink", "Short-range displacement", "spatial", "focus").Tune(1f, 0.6f);
            E(Mov, "pull", "Pull", "Draw a target toward the caster", "attraction|binding|motion", "force|spatial").Tune(0.7f, 0.35f);
            E(Mov, "push", "Push", "Move a target away", "repulsion|force|pressure", "motion|wave").Tune(0.7f, 0.3f);
            E(Mov, "warp", "Warp", "Distort spatial positioning", "spatial", "disruption|expansion|compression").Tune(1.2f, 0.7f);
            E(Mov, "glide", "Glide", "Move through the air", "motion|pressure|fluid", "containment|focus").Tune(0.6f, 0.3f);
            E(Mov, "launch", "Launch", "Propel a target forcefully", "force|repulsion|pressure", "motion|expansion").Tune(0.9f, 0.35f);
            E(Mov, "orbit", "Orbit", "Make a target move around a central point", "rotation|attraction", "motion|binding").Tune(0.9f, 0.55f);
            E(Mov, "reposition", "Reposition", "Change a target's location", "spatial|motion", "focus|binding").Tune(0.9f, 0.5f);
            E(Mov, "phase", "Phase", "Temporarily bypass physical obstacles", "spatial|matter|shadow", "disruption|suppression").Tune(1.1f, 0.65f);

            // Control (10.4).
            E(Ctl, "immobilize", "Immobilize", "Prevent movement", "binding|structure|cold", "containment|suppression").Tune(0.9f, 0.4f);
            E(Ctl, "bind", "Bind", "Restrict movement or abilities", "binding", "containment|attraction|life").Tune(0.8f, 0.4f);
            E(Ctl, "suspend", "Suspend", "Hold a target in the air", "binding|attraction|repulsion|pressure", "containment|motion").Tune(0.9f, 0.5f);
            E(Ctl, "silence", "Silence", "Prevent sound or verbal casting", "suppression|wave", "focus|containment").Tune(0.8f, 0.45f);
            E(Ctl, "disorient", "Disorient", "Disrupt a target's perception", "mind|disruption|light|shadow|wave", "sense|motion").Tune(0.7f, 0.4f);
            E(Ctl, "repel", "Repel", "Force targets away", "repulsion|force|pressure", "expansion|wave").Tune(0.8f, 0.35f);
            E(Ctl, "attract", "Attract", "Draw targets toward a point", "attraction|binding", "spatial|motion").Tune(0.8f, 0.4f);
            E(Ctl, "slow", "Slow", "Reduce movement or action speed", "motion|time|cold|suppression", "binding|fluid|containment").Tune(0.7f, 0.35f);
            E(Ctl, "freeze", "Freeze", "Prevent movement through freezing", "cold", "binding|structure|containment").Tune(0.9f, 0.4f);
            E(Ctl, "confine", "Confine", "Restrict a target to an area", "containment|spatial|structure", "binding|expansion").Tune(0.9f, 0.45f);
            E(Ctl, "paralyze", "Paralyze", "Prevent voluntary movement", "energy|mind|decay|binding", "suppression|disruption").Tune(1f, 0.5f);
            E(Ctl, "suppress", "Suppress", "Reduce effectiveness of an ability", "suppression|absorption", "focus|binding").Tune(0.8f, 0.45f);
            E(Ctl, "distract", "Distract", "Disrupt a target's attention", "mind|sense|light|wave", "motion|disruption").Tune(0.5f, 0.3f);
            E(Ctl, "weaken", "Weaken", "Reduce a target's strength", "decay|absorption|suppression", "life|energy|soul|structure").Tune(0.8f, 0.4f);
            E(Ctl, "restrict", "Restrict", "Limit a target's actions", "binding|containment|suppression", "focus|structure").Tune(0.8f, 0.4f);

            // Creation and Transformation (10.5).
            E(Cre, "construct", "Construct", "Create a structure", "matter|structure", "binding|compression|expansion").Tune(1f, 0.5f);
            E(Cre, "forge", "Forge", "Create or reshape an object", "matter|structure", "thermal|force|pressure").Tune(1f, 0.5f);
            E(Cre, "shape", "Shape", "Alter the form of a material", "matter|fluid", "focus|motion|pressure").Tune(0.8f, 0.45f);
            E(Cre, "transmute", "Transmute", "Change a material into another form", "matter", "energy|disruption|time").Tune(1.6f, 0.85f);
            E(Cre, "duplicate", "Duplicate", "Create a copy", "reflection|matter|spatial", "focus|life|time").Tune(1.4f, 0.75f);
            E(Cre, "grow", "Grow", "Increase size", "life|expansion", "energy|time").Tune(0.8f, 0.35f);
            E(Cre, "shrink", "Shrink", "Reduce size", "compression|spatial", "matter|containment").Tune(0.9f, 0.45f);
            E(Cre, "animate", "Animate", "Give movement to an object", "motion|life|soul|energy", "matter|binding").Tune(1.2f, 0.65f);
            E(Cre, "condense", "Condense", "Concentrate a substance or energy", "compression|pressure|cold", "fluid|energy|matter").Tune(0.8f, 0.4f);
            E(Cre, "expand", "Expand", "Increase volume or area", "expansion", "matter|spatial|pressure").Tune(0.8f, 0.35f);
            E(Cre, "convert", "Convert", "Change one form of energy/material into another", "energy|absorption", "matter|thermal|fluid").Tune(1.1f, 0.6f);
            E(Cre, "purify", "Purify", "Remove impurities or unwanted properties", "light|fluid|suppression", "focus|disruption").Tune(0.8f, 0.4f);
            E(Cre, "refine", "Refine", "Improve quality of a material", "focus|structure", "matter|energy|thermal").Tune(0.9f, 0.5f);
            E(Cre, "crystallize", "Crystallize", "Convert a material into a crystalline structure", "structure|cold|compression", "matter|fluid").Tune(0.9f, 0.45f);
            E(Cre, "solidify", "Solidify", "Convert a substance into a solid", "structure|cold|compression", "fluid|matter").Tune(0.8f, 0.4f);
            E(Cre, "liquefy", "Liquefy", "Convert a substance into a liquid", "fluid|thermal|disruption", "matter|structure").Tune(0.8f, 0.4f);
            E(Cre, "decompose", "Decompose", "Break down a material", "decay|disruption", "matter|life").Tune(0.8f, 0.4f);
            E(Cre, "reassemble", "Reassemble", "Reconstruct an object from components", "binding|time|matter", "structure|focus").Tune(1.1f, 0.6f);

            // Utility and Exploration (10.6).
            E(Uti, "reveal", "Reveal", "Expose hidden information", "light|sense|mind", "expansion|focus").Tune(0.5f, 0.35f);
            E(Uti, "track", "Track", "Detect a target's location", "sense|mind|binding", "motion|time").Tune(0.5f, 0.35f);
            E(Uti, "scan", "Scan", "Analyze a target", "sense|wave|light", "focus|mind").Tune(0.5f, 0.35f);
            E(Uti, "sense", "Sense", "Detect magical or physical properties", "sense", "wave|mind|soul|energy").Tune(0.4f, 0.3f);
            E(Uti, "illuminate", "Illuminate", "Produce light", "light", "expansion|energy").Tune(0.4f, 0.2f);
            E(Uti, "conceal", "Conceal", "Hide a target", "shadow|fluid|light|suppression", "containment|expansion").Tune(0.6f, 0.4f);
            E(Uti, "communicate", "Communicate", "Transmit information", "mind|wave|soul", "binding|expansion").Tune(0.5f, 0.4f);
            E(Uti, "record", "Record", "Store information", "time|mind|structure", "containment|binding").Tune(0.5f, 0.45f);
            E(Uti, "dispel", "Dispel", "Remove a magical effect", "suppression|absorption|disruption", "focus|light").Tune(0.8f, 0.5f);
            E(Uti, "seal", "Seal", "Lock or restrict an object or location", "binding|containment", "structure|suppression|time").Tune(0.7f, 0.45f);
            E(Uti, "detect", "Detect", "Identify a specific property or entity", "sense|mind", "focus|wave|light").Tune(0.5f, 0.35f);
            E(Uti, "restore", "Restore", "Repair or recover a target", "life|time|binding", "structure|matter|soul").Tune(0.8f, 0.5f);
            E(Uti, "identify", "Identify", "Reveal properties of an object", "sense|mind|light", "focus").Tune(0.4f, 0.3f);
            E(Uti, "locate", "Locate", "Find a specific target", "sense|spatial|attraction", "focus|mind").Tune(0.5f, 0.4f);
            E(Uti, "mark", "Mark", "Apply a detectable magical signature", "binding|light|energy|soul", "focus").Tune(0.4f, 0.25f);
            E(Uti, "cleanse", "Cleanse", "Remove contamination", "fluid|light|suppression", "focus|expansion|motion").Tune(0.6f, 0.35f);
            E(Uti, "preserve", "Preserve", "Prevent deterioration", "time|cold|containment|structure", "binding|suppression").Tune(0.6f, 0.4f);
            E(Uti, "extract", "Extract", "Remove a specific component", "absorption|attraction|disruption", "focus|containment").Tune(0.7f, 0.45f);
            E(Uti, "transfer", "Transfer", "Move a property from one target to another", "binding|absorption|energy", "motion|spatial|soul").Tune(0.8f, 0.55f);
            return list;
        }

        /// <summary>Sets an effect's power demand, complexity and supporting aspects (built-in content helper).</summary>
        private static DesiredEffectDef Tune(this DesiredEffectDef e, float powerDemand, float complexity, params string[] supporting)
        {
            e.powerDemand = powerDemand;
            e.complexity = complexity;
            e.supporting = W(supporting);
            return e;
        }
    }
}
