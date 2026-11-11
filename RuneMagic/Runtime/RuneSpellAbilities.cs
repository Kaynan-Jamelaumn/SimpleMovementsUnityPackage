using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// The abilities rune spells run as (design spec 16.4: spells use the existing ability pipeline). A spell's ability is built
    /// from the executable of its form (an ability preset, or a template asset assigned on the Rune Magic Database) and has
    /// the id <c>rune:&lt;form&gt;:&lt;spell&gt;</c>, so any machine can rebuild it from the id alone: the server puts it in a slot, the
    /// slot's content travels like any ability's (id, modifiers, label) and the client finds the definition through
    /// <see cref="AbilityDatabase.FallbackResolver"/>. The spell's own numbers (power, cast time, range...) are the slot's
    /// modifiers, so the shared executable is never changed.
    /// </summary>
    public static class RuneSpellAbilities
    {
        private static readonly Dictionary<string, AbilityDefinition> built = new Dictionary<string, AbilityDefinition>();
        private static readonly HashSet<string> warned = new HashSet<string>();
        private static Func<string, AbilityDefinition> previous;
        private static Func<string, AbilityDefinition> ours;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            built.Clear();
            warned.Clear();
            previous = null;
            ours = null;
        }

        /// <summary>Forgets the abilities built so far and the warnings given (tests; also after editing the forms in play mode).</summary>
        internal static void ForgetBuilt()
        {
            built.Clear();
            warned.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInstall() => Install();

        /// <summary>
        /// Lets <see cref="AbilityDatabase.Resolve"/> build the ability of any rune spell id (chained after whatever resolver was
        /// installed before). Idempotent; done automatically when the game starts.
        /// </summary>
        public static void Install()
        {
            if (ours != null && AbilityDatabase.FallbackResolver == ours)
                return;
            previous = AbilityDatabase.FallbackResolver;
            Func<string, AbilityDefinition> before = previous;
            ours = id =>
            {
                AbilityDefinition found = Resolve(id);
                if (found != null) return found;
                return before != null ? before(id) : null;
            };
            AbilityDatabase.FallbackResolver = ours;
        }

        /// <summary>The ability of the spell ability id (<c>rune:&lt;form&gt;:&lt;spell&gt;</c>), built on first use; null for any other id or an unknown form.</summary>
        public static AbilityDefinition Resolve(string abilityId)
        {
            if (!SpellCompiler.TryParseAbilityId(abilityId, out string formId, out _))
                return null;
            return Build(abilityId, formId);
        }

        /// <summary>The ability <paramref name="spell"/> runs as (null when its form is unknown).</summary>
        public static AbilityDefinition For(CompiledSpell spell) => spell == null ? null : Build(spell.abilityId, spell.formId);

        /// <summary>The slot's modifiers of a spell: its numbers with the soul's condition applied.</summary>
        public static AbilityModifierSet Modifiers(CompiledSpell spell, in CastPlan plan, SpellingOutcome performance = null)
        {
            SpellModifiers m = SpellCompiler.ModifiersFor(spell, plan, performance);
            return new AbilityModifierSet
            {
                damageMultiplier = m.damage,
                healMultiplier = m.heal,
                controlMultiplier = m.control,
                castTimeMultiplier = m.castTime,
                cooldownMultiplier = m.cooldown,
                rangeMultiplier = m.range,
                areaMultiplier = m.area,
                durationMultiplier = m.duration,
            };
        }

        // ------------------------------------------------------------------ building
        private static AbilityDefinition Build(string abilityId, string formId)
        {
            if (built.TryGetValue(abilityId, out AbilityDefinition existing))
            {
                if (existing != null) return existing;
                built.Remove(abilityId); // destroyed (a scene change in the editor)
            }
            RuneMagicDatabase db = RuneMagicDatabase.Instance;
            SpellFormDef form = db.Catalog.SpellForm(formId);
            if (form == null)
                return null;
            AbilityDefinition def = Create(db, form);
            if (def == null)
                return null;
            def.InitializeRuntime(abilityId, form.Name);
            Adjust(def, form, db.Catalog.Spells);
            built[abilityId] = def;
            return def;
        }

        private static AbilityDefinition Create(RuneMagicDatabase db, SpellFormDef form)
        {
            AbilityDefinition template = db.TemplateFor(form.id);
            if (template != null)
                return UnityEngine.Object.Instantiate(template);
            if (!string.IsNullOrWhiteSpace(form.preset))
            {
                foreach (AbilityPresets.Preset p in AbilityPresets.All)
                    if (string.Equals(p.name, form.preset, StringComparison.OrdinalIgnoreCase) && p.create != null)
                        return p.create();
                if (warned.Add(form.id))
                    Debug.LogWarning($"[Rune Magic] Spell form '{form.id}': the ability preset '{form.preset}' does not exist. Its spells do nothing until you fix the " +
                                     "preset name (Rune Magic Database ▸ Spell Forms) or assign a template ability to the form.");
            }
            // An empty cast: costs and strain, no effect on the world.
            AbilityDefinition empty = ScriptableObject.CreateInstance<AbilityDefinition>();
            empty.castTime = 1f;
            empty.activeTime = 0.2f;
            empty.cooldown = 3f;
            empty.targeting.mode = AbilityTargetingMode.Self;
            empty.actions.Clear();
            return empty;
        }

        // What every spell of every form shares: the energy is the cost (not mana, stamina or health), the spell's castable
        // decides about the refund, a spell is shaped rather than clicked (a minimum cast time), the caster of a form that
        // allows it can move slowly while casting.
        private static void Adjust(AbilityDefinition def, SpellFormDef form, SpellBalance sb)
        {
            def.manaCost = def.staminaCost = def.healthCost = 0f;
            def.refundCostOnInterrupt = false;
            def.sharedCooldown = 0f;
            def.castTime = Mathf.Max(def.castTime, sb.minCastTime / Mathf.Max(0.1f, sb.castTimeBase));
            if (form.canMoveWhileCasting)
            {
                def.movementWhileCasting = CasterMovementRule.Slowed;
                def.castMoveSpeedMultiplier = sb.castMoveSpeed;
            }
            if (!string.IsNullOrEmpty(form.description))
                def.description = form.description;
        }
    }
}
