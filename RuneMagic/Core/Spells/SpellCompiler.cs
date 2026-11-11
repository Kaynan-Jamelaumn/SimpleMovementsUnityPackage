using System;
using System.Collections.Generic;

namespace RuneMagic
{
    /// <summary>
    /// What a synthesized spell is as something to cast (design spec 16.1): its form, what a cast costs in Rune Energy and
    /// Soul Strain, and how its stats become the numbers of an ability. Compiled from the spell's record and the data; the
    /// soul's condition is applied on top (see <see cref="SpellCasting.Plan"/>). Pure and deterministic.
    /// </summary>
    public sealed class CompiledSpell
    {
        public string spellId = "";
        public string name = "";
        public string effectId = "";
        public string intentionId = "";
        public string formId = "";
        /// <summary>The id of the ability that carries it: <c>rune:&lt;form&gt;:&lt;spell&gt;</c>.</summary>
        public string abilityId = "";
        public int rank = 1;
        public int tier;
        public float score;
        public float power;
        public float stability;
        public float complexity;
        public float controlPenalty;

        // ---- what a cast costs (before the soul's condition)
        public float energyCost;
        public float strain;
        public float failureDamage;
        public float baseInstability;

        // ---- how its stats become an ability's modifiers (before the soul's condition)
        public float damage = 1f, heal = 1f, control = 1f, castTime = 1f, cooldown = 1f, range = 1f, area = 1f, duration = 1f;
        public bool canMoveWhileCasting;
        /// <summary>Does casting it need the Spelling System (design spec 16.5)? Decided by the Spelling System (a later phase); false until then.</summary>
        public bool usesSpelling;
        /// <summary>Can it be charged by holding its key (its form allows it, the balance has a charge and it does not use the Spelling System)?</summary>
        public bool chargeable;

        public List<string> flaws = new List<string>();
        public List<string> sideEffects = new List<string>();
    }

    /// <summary>What a cast costs and does right now: a spell's numbers with the soul's condition applied (design spec 16.2).</summary>
    public struct CastPlan
    {
        public bool allowed;
        public string reason;
        /// <summary>Rune Energy drawn from the anchored runes.</summary>
        public float energyCost;
        public float castTimeMultiplier;
        public float powerMultiplier;
        /// <summary>The chance the cast misfires (stability and the soul's condition).</summary>
        public float instability;
        /// <summary>Soul Damage of a successful cast and of a misfire as asked for: the soul applies the damage kind's and the condition's multipliers to them.</summary>
        public float strain;
        public float failureDamage;
        /// <summary>What the soul will actually lose (the asked-for damage after the kind's and the condition's multipliers): what the tooltip shows. 0 = none.</summary>
        public float strainScaled;
        public float failureScaled;
    }

    /// <summary>The ability modifiers of a spell as plain numbers (the runtime turns them into an <c>AbilityModifierSet</c>).</summary>
    public struct SpellModifiers
    {
        public float damage, heal, control, castTime, cooldown, range, area, duration;
    }

    /// <summary>Compiles spells (design spec 16.1, 16.4). The shared executable of a form is the ability layer's business; the numbers are here.</summary>
    public static class SpellCompiler
    {
        public const string AbilityPrefix = "rune:";

        /// <summary>The ability id of a spell of a form: <c>rune:bolt:syn_0123456789ab</c>. Any machine can rebuild the ability from it.</summary>
        public static string AbilityId(string formId, string spellId) => AbilityPrefix + formId + ":" + spellId;

        /// <summary>Reads a spell ability id. False for any other id.</summary>
        public static bool TryParseAbilityId(string id, out string formId, out string spellId)
        {
            formId = spellId = "";
            if (string.IsNullOrEmpty(id) || !id.StartsWith(AbilityPrefix, StringComparison.Ordinal)) return false;
            int colon = id.IndexOf(':', AbilityPrefix.Length);
            if (colon <= AbilityPrefix.Length || colon >= id.Length - 1) return false;
            formId = id.Substring(AbilityPrefix.Length, colon - AbilityPrefix.Length);
            spellId = id.Substring(colon + 1);
            return true;
        }

        public static CompiledSpell Compile(RuneCatalog c, SpellRecord sp)
        {
            SpellBalance b = c.Spells;
            SpellFormDef form = c.FormOfEffect(sp.effectId);
            float complexity = Clamp01(sp.complexity);
            float stability = Math.Max(0f, Math.Min(100f, sp.stability));
            int rank = Math.Max(1, sp.rank);
            float rankStep = rank - 1;
            float formEnergy = form != null ? form.energyMultiplier : 1f;
            float formStrain = form != null ? form.strainMultiplier : 1f;
            float demand = 0.5f + complexity;

            var s = new CompiledSpell
            {
                spellId = sp.spellId, name = sp.name, effectId = sp.effectId, intentionId = sp.intentionId, formId = form != null ? form.id : "",
                rank = rank, tier = sp.tier, score = sp.score, power = sp.power, stability = stability, complexity = complexity, controlPenalty = Clamp01(sp.controlPenalty),
                canMoveWhileCasting = form != null && form.canMoveWhileCasting,
            };
            s.abilityId = AbilityId(s.formId, s.spellId);
            s.energyCost = Math.Max(b.minEnergyCost, b.castEnergyFraction * c.Balance.MaxEnergy(rank) * demand * Math.Max(0.1f, sp.energyCostMultiplier) * formEnergy);
            float rawStrain = b.strainBase * (1f + b.strainPerRank * rankStep) * demand * formStrain;
            s.strain = b.strainOnSuccess ? rawStrain : 0f;
            s.failureDamage = rawStrain * b.failureDamageFactor;
            s.baseInstability = Clamp01((1f - stability / 100f) * b.instabilityScale);

            s.damage = Math.Max(0.05f, sp.power / Math.Max(1f, b.referencePower));
            s.heal = s.damage;
            s.control = Math.Max(0.5f, (float)Math.Sqrt(s.damage));
            s.castTime = (b.castTimeBase + b.castTimeComplexity * complexity) * (1f + s.controlPenalty);
            s.cooldown = b.cooldownBase + b.cooldownComplexity * complexity;
            s.range = (1f + b.rangePerRank * rankStep) * (form != null ? form.rangeMultiplier : 1f);
            s.area = (1f + b.areaPerRank * rankStep) * (form != null ? form.areaMultiplier : 1f);
            s.duration = b.durationBase + b.durationStability * stability / 100f;
            foreach (string f in sp.flaws) s.flaws.Add(f);
            foreach (string e in sp.sideEffects) s.sideEffects.Add(e);
            return s;
        }

        /// <summary>The modifiers of the spell's slot: its numbers with the soul's condition (cast time, power) applied.</summary>
        public static SpellModifiers ModifiersFor(CompiledSpell s, in CastPlan plan) => new SpellModifiers
        {
            damage = s.damage * plan.powerMultiplier,
            heal = s.heal * plan.powerMultiplier,
            control = s.control * (float)Math.Sqrt(Math.Max(0f, plan.powerMultiplier)),
            castTime = plan.castTimeMultiplier,
            cooldown = s.cooldown,
            range = s.range,
            area = s.area,
            duration = s.duration,
        };

        /// <summary>
        /// The same with what a successful key sequence earned (Spelling System): its power and its mistakes' extra cast time. The
        /// sequence's effect on the energy cost, the strain and the stability is applied to the cast itself
        /// (<see cref="SpellCasting.ApplyPerformance"/>).
        /// </summary>
        public static SpellModifiers ModifiersFor(CompiledSpell s, in CastPlan plan, SpellingOutcome performance)
        {
            SpellModifiers m = ModifiersFor(s, plan);
            if (performance == null) return m;
            m.damage *= performance.powerMultiplier;
            m.heal *= performance.powerMultiplier;
            m.control *= (float)Math.Sqrt(Math.Max(0f, performance.powerMultiplier));
            m.castTime *= performance.castTimeMultiplier;
            return m;
        }

        /// <summary>The lines the slot's tooltip shows (design spec 16.4: Rune Energy cost, Soul Strain, whether it uses the Spelling System).</summary>
        public static List<string> Describe(RuneCatalog c, CompiledSpell s, in CastPlan plan)
        {
            var lines = new List<string>();
            SpellFormDef form = c.SpellForm(s.formId);
            lines.Add($"Rune spell · rank {s.rank}{(form != null ? " · " + form.Name : "")}");
            lines.Add($"Rune Energy {plan.energyCost:0.#} per cast");
            lines.Add(plan.strainScaled > 0.005f ? $"Soul Strain {plan.strainScaled:0.##} per cast" : "Soul Strain: none");
            if (plan.failureScaled > 0.005f) lines.Add($"A misfire costs {plan.failureScaled:0.##} Soul Integrity");
            lines.Add($"Stability {s.stability:0}%" + (plan.instability > 0.005f ? $" · misfire chance {plan.instability * 100f:0.#}%" : ""));
            lines.Add(s.usesSpelling ? "Spelling System: yes" : "Spelling System: no");
            if (s.chargeable)
            {
                ChargeProfile ch = c.Spells.charge;
                lines.Add($"Hold the key to charge ({ch.minSeconds:0.#}-{ch.maxSeconds:0.#} s): up to ×{ch.damageAtFull:0.#} power, ×{ChargeProfile.Scale(ch.costAtFull, 1f):0.#} energy, " +
                          $"×{c.Spells.chargeStrainAtFull:0.#} strain");
            }
            if (s.flaws.Count > 0)
            {
                var names = new List<string>();
                foreach (string id in s.flaws)
                {
                    SynthesisFlawDef f = c.Flaw(id);
                    names.Add(f != null ? f.Name : id);
                }
                lines.Add("Flaws: " + string.Join(", ", names));
            }
            if (s.sideEffects.Count > 0) lines.Add("Side effects: " + string.Join(", ", s.sideEffects));
            return lines;
        }

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
