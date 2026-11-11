using System;
using System.Collections.Generic;

namespace RuneMagic
{
    /// <summary>One rune's share of the energy a cast drew.</summary>
    public struct RuneDraw
    {
        public string runeId;
        public float amount;
    }

    /// <summary>
    /// The energy one cast drew from the runes that anchor the spell (design spec 16.1: "the energy used is drawn from the
    /// anchored runes"). Kept by whoever runs the cast so an interrupted cast gives it back to the same runes.
    /// </summary>
    public sealed class CastDraw
    {
        public string spellId = "";
        public readonly List<RuneDraw> runes = new List<RuneDraw>();
        public float Total
        {
            get { float t = 0f; foreach (RuneDraw d in runes) t += d.amount; return t; }
        }
    }

    /// <summary>The server's roll for one cast: does it misfire, and at which share of its cast time does it fizzle?</summary>
    public struct CastRoll
    {
        public bool misfire;
        /// <summary>Share (0-1) of the cast time at which a misfire cuts the cast off.</summary>
        public float fizzleAt;
    }

    /// <summary>How a cast ended.</summary>
    public enum SpellCastOutcome
    {
        /// <summary>It reached its release (the Soul Strain of a success applies).</summary>
        Success,
        /// <summary>It misfired: cut off before its release (the failure damage applies, the energy is kept or refunded as the balance says).</summary>
        Misfire,
        /// <summary>An enemy interrupted it, or it was cancelled or replaced before its release (the energy is refunded).</summary>
        Interrupted,
    }

    /// <summary>What one cast did (events, the debug panel and tests).</summary>
    public sealed class SpellCastReport
    {
        public string spellId = "";
        public string name = "";
        public SpellCastOutcome outcome;
        public float energyDrawn;
        public float energyRefunded;
        /// <summary>The Soul Damage asked for (before the kind's and the condition's multipliers).</summary>
        public float soulDamage;
        public string soulDamageKind = "";
        public string message = "";
    }

    /// <summary>
    /// What a cast costs the soul: an amount of Soul Damage of a Soul Damage Kind (applied by the caller, so the soul's own
    /// rules, kind multiplier, condition multiplier, collapse and replication apply as for any Soul Damage). Amount 0 = none.
    /// </summary>
    public struct CastStrain
    {
        public float amount;
        public string kind;
        public bool Any => amount > 0f;
    }

    /// <summary>
    /// The rules of casting a spell (design spec 16.2, 16.3, 14.1): what the soul's condition lets a spell do, what a cast
    /// costs and from which runes, the Soul Strain of a success, the failure of a misfire and the refund of an interrupted
    /// cast. Pure functions over <see cref="RuneSoulState"/>; the runtime only decides when to call them.
    /// </summary>
    public static class SpellCasting
    {
        // ------------------------------------------------------------------ the plan of a cast
        /// <summary>
        /// What holding the key changes in a cast (a charge of <paramref name="fraction"/>, 0-1): the Rune Energy and the Soul Strain grow, and
        /// so does the chance of a misfire. The power, range, size and duration are the ability layer's (the charge's multipliers are in the cast's
        /// stats). A charge of 0 changes nothing.
        /// </summary>
        public static void ApplyCharge(ref CastPlan plan, SpellBalance b, float fraction)
        {
            if (b?.charge == null || !(fraction > 0f)) return;
            float f = Clamp01(fraction);
            plan.energyCost *= ChargeProfile.Scale(b.charge.costAtFull, f);
            float strain = ChargeProfile.Scale(b.chargeStrainAtFull, f);
            plan.strain *= strain;
            plan.strainScaled *= strain;
            plan.failureDamage *= strain;
            plan.failureScaled *= strain;
            plan.instability = Clamp01(plan.instability + b.chargeInstabilityAtFull * f);
        }

        /// <summary>
        /// What casting <paramref name="s"/> costs and does with the soul in condition <paramref name="r"/>: refused when the
        /// condition forbids casting (or the spell's rank), more expensive and slower in a worse condition, less powerful,
        /// and prone to misfire (the spell's own instability plus the condition's).
        /// </summary>
        public static CastPlan Plan(RuneCatalog c, CompiledSpell s, in SoulRestrictions r)
        {
            var plan = new CastPlan { castTimeMultiplier = 1f, powerMultiplier = 1f };
            plan.allowed = r.AllowsCasting(s.rank, out string why);
            plan.reason = why;
            plan.energyCost = s.energyCost * Math.Max(0f, r.energyCostMultiplier);
            plan.castTimeMultiplier = s.castTime * Math.Max(0.1f, r.castTimeMultiplier);
            plan.powerMultiplier = Math.Max(0f, r.powerMultiplier);
            plan.instability = Clamp01(s.baseInstability + r.instabilityChance);
            plan.strain = s.strain;
            plan.failureDamage = s.failureDamage;
            plan.strainScaled = s.strain * DamageMultiplier(c.Balance, c.Spells.strainKind, r);
            plan.failureScaled = s.failureDamage * DamageMultiplier(c.Balance, c.Spells.failureKind, r);
            return plan;
        }

        /// <summary>
        /// What a successful key sequence (Spelling System) changes in the cost of the cast: the energy and the strain (the speed
        /// bonus lowers them) and the stability (mistakes lower it, the speed bonus raises it), which moves the misfire chance. Its
        /// effect on power and cast time is in the slot's modifiers (<see cref="SpellCompiler.ModifiersFor(CompiledSpell, in CastPlan, SpellingOutcome)"/>).
        /// </summary>
        public static void ApplyPerformance(ref CastPlan plan, CompiledSpell s, SpellingOutcome performance, SpellBalance b)
        {
            if (performance == null) return;
            plan.energyCost = Math.Max(Math.Min(plan.energyCost, b.minEnergyCost), plan.energyCost * performance.costMultiplier);
            plan.strain *= performance.strainMultiplier;
            plan.strainScaled *= performance.strainMultiplier;
            float stability = Math.Max(0f, Math.Min(100f, s.stability + performance.stabilityDelta));
            float now = Clamp01((1f - stability / 100f) * b.instabilityScale);
            plan.instability = Clamp01(plan.instability + now - s.baseInstability);
        }

        // What the soul will multiply Soul Damage of this kind by (the kind's, and the condition's when the kind is scaled by it).
        private static float DamageMultiplier(RuneBalance b, string kindId, in SoulRestrictions r)
        {
            SoulDamageKind kind = b.DamageKind(kindId);
            float m = kind != null ? kind.multiplier : 1f;
            if (kind == null || kind.scaledByCondition) m *= r.soulDamageMultiplier;
            return Math.Max(0f, m);
        }

        // ------------------------------------------------------------------ energy
        /// <summary>Can the runes anchoring <paramref name="sp"/> pay <paramref name="cost"/>?</summary>
        public static bool CanAfford(RuneSoulState st, SpellRecord sp, float cost, out string why)
        {
            why = "";
            if (st == null || sp == null) { why = "That spell is no longer in your spellbook."; return false; }
            float have = SpellRules.AnchoredEnergy(st, sp);
            if (have + 0.0001f < cost)
            {
                why = $"Not enough Rune Energy: {sp.name} needs {cost:0.#} and its runes hold {have:0.#}.";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Draws <paramref name="cost"/> energy from the runes anchoring <paramref name="sp"/>, taking the same amount from each
        /// until a rune runs dry (the others carry on): no single rune is drained while another one is full. Null when the
        /// runes hold less than <paramref name="cost"/> (nothing is taken then).
        /// </summary>
        public static CastDraw Draw(RuneSoulState st, SpellRecord sp, float cost)
        {
            if (st == null || sp == null || sp.anchors == null) return null;
            cost = Math.Max(0f, float.IsNaN(cost) ? 0f : cost);
            var runes = new List<RuneInstance>();
            foreach (string id in sp.anchors)
            {
                RuneInstance r = st.Rune(id);
                if (r != null && r.energy > 0f) runes.Add(r);
            }
            float have = 0f;
            foreach (RuneInstance r in runes) have += r.energy;
            if (have + 0.0001f < cost) return null;

            var draw = new CastDraw { spellId = sp.spellId };
            runes.Sort((a, b) => a.energy.CompareTo(b.energy));
            float left = cost;
            for (int i = 0; i < runes.Count && left > 0f; i++)
            {
                float share = left / (runes.Count - i);
                float take = Math.Min(share, runes[i].energy);
                if (take <= 0f) continue;
                take = EnergyRules.Consume(runes[i], take);
                draw.runes.Add(new RuneDraw { runeId = runes[i].instanceId, amount = take });
                left -= take;
            }
            return draw;
        }

        /// <summary>
        /// Gives back <paramref name="fraction"/> (0-1) of what <paramref name="draw"/> took to the runes it came from (never
        /// above their maximum). Returns the energy returned.
        /// </summary>
        public static float Refund(RuneBalance b, RuneSoulState st, CastDraw draw, float fraction = 1f)
        {
            if (st == null || draw == null) return 0f;
            fraction = Clamp01(fraction);
            float given = 0f;
            foreach (RuneDraw d in draw.runes)
            {
                RuneInstance r = st.Rune(d.runeId);
                if (r == null) continue;
                float back = Math.Min(d.amount * fraction, EnergyRules.Space(b, r));
                if (back <= 0f) continue;
                r.energy += back;
                given += back;
            }
            return given;
        }

        // ------------------------------------------------------------------ misfires
        /// <summary>
        /// The roll of a cast: it misfires with probability <paramref name="instability"/> and then fizzles somewhere in the
        /// middle to the end of its cast time (the Spell Balance's Fizzle range). The server rolls once per cast; the same
        /// <paramref name="seed"/> gives the same result on any machine.
        /// </summary>
        public static CastRoll Roll(SpellBalance b, float instability, int seed)
        {
            var rng = new SynthesisRng(seed);
            float chance = Clamp01(instability);
            float roll = rng.NextFloat();
            float t = rng.NextFloat();
            var result = new CastRoll { misfire = chance > 0f && roll < chance };
            float min = Math.Max(0.05f, b.fizzleMin), max = Math.Max(min, b.fizzleMax);
            result.fizzleAt = min + (max - min) * t;
            return result;
        }

        // ------------------------------------------------------------------ strain
        /// <summary>
        /// The Soul Strain of a successful cast (design spec 14.1: each cast that completes strains the soul when the Spell
        /// Balance says so; none when it does not). The soul kind's and condition's multipliers apply when it is applied.
        /// </summary>
        public static CastStrain Success(RuneBalance b, in CastPlan plan) =>
            new CastStrain { amount = Math.Max(0f, plan.strain), kind = b.spells.strainKind };

        /// <summary>The Soul Damage of a misfired cast (design spec 14.1: casting failure); none when the Spell Balance's factor is 0.</summary>
        public static CastStrain Failure(RuneBalance b, in CastPlan plan) =>
            new CastStrain { amount = Math.Max(0f, plan.failureDamage), kind = b.spells.failureKind };

        /// <summary>
        /// How an unfinished cast ends. A misfire keeps its energy (or gives it back, per the Spell Balance) and causes the
        /// failure damage. An interruption by an enemy (or a cancel, a replaced slot) refunds all of the energy and causes the
        /// failure damage only when the Spell Balance says so. The refund is applied to the runes now; the strain is returned.
        /// </summary>
        public static CastStrain End(RuneBalance b, RuneSoulState st, CastDraw draw, in CastPlan plan, bool misfire, out float refunded)
        {
            SpellBalance sb = b.spells;
            refunded = 0f;
            if (misfire)
            {
                if (!sb.misfireKeepsEnergy) refunded = Refund(b, st, draw, 1f);
                return Failure(b, plan);
            }
            refunded = Refund(b, st, draw, 1f);
            return sb.damageOnInterrupt ? Failure(b, plan) : default;
        }

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
