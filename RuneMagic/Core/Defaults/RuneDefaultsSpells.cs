using System.Collections.Generic;

namespace RuneMagic
{
    // Built-in spellcasting data (design spec 16): the spell forms and the spellcasting balance. Every desired effect of the
    // seed content maps to a form (a test checks it). The forms are built on the ability presets that ship with the ability
    // system, so a spell works as soon as it is assigned; the effects with no combat equivalent (reveal, grow, communicate...)
    // use the "ritual" form, an empty self cast that costs energy and strains the soul but does nothing in the game world:
    // give those forms an ability template on the Rune Magic Database component (or edit the preset) to make them do something.
    public static partial class RuneDefaults
    {
        public static SpellBalance Spells() => new SpellBalance();

        private static SpellFormDef Form(string id, string name, string description, string preset, string[] effects, EffectCategory[] fallbackFor = null,
            bool canMove = false, float energy = 1f, float strain = 1f) => new SpellFormDef
        {
            id = id, displayName = name, description = description, preset = preset, effects = new List<string>(effects),
            fallbackFor = fallbackFor != null ? new List<EffectCategory>(fallbackFor) : new List<EffectCategory>(),
            canMoveWhileCasting = canMove, energyMultiplier = energy, strainMultiplier = strain,
        };

        /// <summary>The forms a hold-to-charge makes sense for: the attacks (a bolt, a lobbed charge, a sweep, a ring, a vortex).</summary>
        public static readonly string[] ChargeableForms = { "bolt", "blast", "cleave", "shockwave", "vortex" };

        public static List<SpellFormDef> SpellForms()
        {
            List<SpellFormDef> forms = SpellFormsBase();
            foreach (SpellFormDef f in forms)
                f.chargeable = System.Array.IndexOf(ChargeableForms, f.id) >= 0;
            return forms;
        }

        private static List<SpellFormDef> SpellFormsBase() => new List<SpellFormDef>
        {
            // ---- offensive
            Form("bolt", "Rune Bolt", "A fast bolt of shaped magic at one target.", "Firebolt (Projectile)",
                new[] { "offense.pierce", "offense.penetrate", "offense.infect", "offense.drain", "offense.erode", "offense.corrode" }, new[] { EffectCategory.Offensive }),
            Form("blast", "Rune Blast", "A charge lobbed at the target that goes off where it lands.", "Bomb (Lobbed)",
                new[] { "offense.explosion", "offense.detonate", "offense.overload", "offense.chain_reaction", "offense.rupture", "offense.shatter" }),
            Form("cleave", "Rune Cleave", "A sweep of force in front of the caster.", "Melee Cleave (Cone)",
                new[] { "offense.slash", "offense.rend", "offense.sever" }),
            Form("shockwave", "Rune Shockwave", "A ring of force spreading out from the caster.", "Shockwave (Ring)",
                new[] { "offense.crush", "offense.pulverize", "control.repel", "movement.push" }),
            Form("beam", "Rune Beam", "A sustained stream of magic that follows the target.", "Fire Breath (Beam)",
                new[] { "offense.incinerate", "offense.disintegrate" }),
            Form("vortex", "Rune Vortex", "Drags everything near a point into it.", "Vortex (Pull to Centre)",
                new[] { "offense.implode", "control.attract" }),
            // ---- control
            Form("cage", "Rune Cage", "Holds a target in place.", "Cage",
                new[] { "control.immobilize", "control.confine", "control.restrict", "control.suspend", "control.freeze", "control.paralyze" }),
            Form("chain", "Rune Chain", "Binds a target and pulls it in.", "Hook (Pull)",
                new[] { "control.bind", "movement.pull" }),
            Form("command", "Rune Command", "Weakens and silences everyone around the caster.", "War Cry (Silence Nova)",
                new[] { "control.silence", "control.suppress", "control.weaken", "control.distract", "control.disorient", "control.slow" }, new[] { EffectCategory.Control }),
            // ---- defensive
            Form("wall", "Rune Wall", "Raises a barrier between the caster and the target.", "Stone Wall",
                new[] { "defense.barrier", "defense.seal", "creation.construct", "creation.forge", "creation.shape" }),
            Form("ward", "Rune Ward", "Wraps the caster in protective magic.", "Guardian Barrier (Self)",
                new[] { "defense.shield", "defense.deflect", "defense.absorb", "defense.reinforce", "defense.reflect", "defense.nullify", "defense.fortify",
                        "defense.stabilize", "defense.disperse", "defense.harden", "defense.insulate", "defense.counter" }, new[] { EffectCategory.Defensive }),
            // ---- movement
            Form("rush", "Rune Rush", "Rushes the caster forward.", "Charge",
                new[] { "movement.dash", "movement.propel", "movement.accelerate", "movement.launch" }, energy: 0.8f),
            Form("blink", "Rune Blink", "Steps the caster a short way along the aim.", "Blink",
                new[] { "movement.teleport", "movement.blink", "movement.warp", "movement.phase", "movement.reposition" }, new[] { EffectCategory.Movement }, energy: 0.8f),
            Form("drift", "Rune Drift", "Throws the caster away from danger.", "Disengage (Retreat)",
                new[] { "movement.levitate", "movement.glide", "movement.decelerate", "movement.orbit" }, energy: 0.8f),
            // ---- mending and the rest
            Form("mending", "Rune Mending", "Restores and cleanses the caster.", "Heal Self",
                new[] { "utility.restore", "utility.cleanse", "creation.purify" }, canMove: true),
            Form("ritual", "Rune Ritual", "Shapes magic with no direct effect on the world: it costs energy and strains the soul. Give this form an ability template to make it do something.",
                "", new string[0], new[] { EffectCategory.CreationTransformation, EffectCategory.UtilityExploration }, canMove: true, energy: 0.6f, strain: 0.5f),
        };
    }
}
