using System;
using System.Text;

namespace RuneMagic
{
    /// <summary>What a character is doing and where, as far as soul recovery cares (built by the runtime each frame).</summary>
    public struct RecoveryContext
    {
        /// <summary>Alive and conscious (a dead or downed character recovers nothing).</summary>
        public bool alive;
        public bool inCombat;
        public bool sleeping;
        /// <summary>The sleep's Recovery Multiplier (a bed is better than the ground); 1 when unknown.</summary>
        public float sleepQuality;
        public bool meditating;
        /// <summary>Seconds since the meditation began.</summary>
        public float meditationSeconds;
        public bool inSanctuary;
        /// <summary>The sanctuary's multiplier (when <see cref="inSanctuary"/>).</summary>
        public float sanctuaryMultiplier;
        /// <summary>The sanctuary's recovery per minute just for being there out of combat.</summary>
        public float sanctuaryPassivePerMinute;
        public bool inCity;
        /// <summary>Product of the recovery multipliers of abilities, traits and equipment (1 = none).</summary>
        public float modifierMultiplier;
        /// <summary>Sum of the flat recovery per minute of abilities, traits and equipment.</summary>
        public float modifierFlatPerMinute;
        /// <summary>Percent bonus to every gain (the status's Increment Factor: +50 = 50% more).</summary>
        public float gainFactorPercent;

        /// <summary>A context with neutral multipliers.</summary>
        public static RecoveryContext Default => new RecoveryContext
        {
            alive = true, sleepQuality = 1f, sanctuaryMultiplier = 1f, modifierMultiplier = 1f,
        };
    }

    /// <summary>Where a soul's recovery per minute comes from (for the UI and the debug panel).</summary>
    public struct RecoveryBreakdown
    {
        public float passive;
        public float rest;
        public float meditation;
        /// <summary>Recovery for being in a sanctuary.</summary>
        public float sanctuary;
        public float flat;
        /// <summary>Multiplier of the place (sanctuary or city), applied to passive, rest and meditation.</summary>
        public float placeMultiplier;
        /// <summary>Multiplier of the modifiers and the gain factor, applied to everything.</summary>
        public float modifierMultiplier;
        public float total;

        /// <summary>"Meditation 10 × 2 (sanctuary) = 20/min".</summary>
        public string Describe()
        {
            if (total <= 0f) return "none";
            var sb = new StringBuilder();
            void Part(string name, float v)
            {
                if (v <= 0f) return;
                if (sb.Length > 0) sb.Append(" + ");
                sb.Append(name).Append(' ').Append(v.ToString("0.#"));
            }
            Part("passive", passive);
            Part("rest", rest);
            Part("meditation", meditation);
            if (placeMultiplier > 1.0001f || placeMultiplier < 0.9999f)
                sb.Append(" ×").Append(placeMultiplier.ToString("0.##")).Append(" (place)");
            Part("sanctuary", sanctuary);
            Part("bonus", flat);
            if (modifierMultiplier > 1.0001f || modifierMultiplier < 0.9999f)
                sb.Append(" ×").Append(modifierMultiplier.ToString("0.##"));
            return sb.ToString();
        }
    }

    /// <summary>
    /// How fast the soul recovers (design spec 15): rest, meditation, natural recovery (none by default), places and
    /// modifiers. Every rate comes from <see cref="SoulRecoveryBalance"/>. What can be reached is limited by deep damage
    /// (<see cref="SoulRules.Ceiling"/>); items and rituals heal on their own terms.
    /// </summary>
    public static class RecoveryRules
    {
        /// <summary>Soul Integrity recovered per minute in context <paramref name="c"/>.</summary>
        public static float PerMinute(SoulRecoveryBalance r, RecoveryContext c, out RecoveryBreakdown breakdown)
        {
            breakdown = new RecoveryBreakdown { placeMultiplier = 1f, modifierMultiplier = 1f };
            if (r == null || !c.alive)
                return 0f;

            float passive = !r.passiveOnlyOutOfCombat || !c.inCombat ? r.passivePerMinute : 0f;
            float rest = c.sleeping ? r.restPerMinute * (r.restUsesSleepQuality ? Math.Max(0f, Finite(c.sleepQuality, 1f)) : 1f) : 0f;
            float meditation = c.meditating && c.meditationSeconds >= r.meditationSettleSeconds ? r.meditationPerMinute : 0f;

            // The best place counts (a sanctuary in the city does not stack with the city).
            float place = 1f;
            if (c.inSanctuary) place = Math.Max(place, Math.Max(0f, Finite(c.sanctuaryMultiplier, 1f)));
            if (c.inCity) place = Math.Max(place, Math.Max(0f, r.cityMultiplier));
            float sanctuary = c.inSanctuary && !c.inCombat ? Math.Max(0f, Finite(c.sanctuaryPassivePerMinute, 0f)) : 0f;
            float flat = Math.Max(0f, Finite(c.modifierFlatPerMinute, 0f));

            float modifiers = Finite(c.modifierMultiplier, 1f);
            float lo = Math.Min(r.minModifierMultiplier, r.maxModifierMultiplier);
            float hi = Math.Max(r.minModifierMultiplier, r.maxModifierMultiplier);
            modifiers = modifiers < lo ? lo : modifiers > hi ? hi : modifiers;
            modifiers *= Math.Max(0f, 1f + Finite(c.gainFactorPercent, 0f) / 100f);

            float total = ((passive + rest + meditation) * place + sanctuary + flat) * modifiers;
            breakdown = new RecoveryBreakdown
            {
                passive = passive, rest = rest, meditation = meditation, sanctuary = sanctuary, flat = flat,
                placeMultiplier = place, modifierMultiplier = modifiers, total = Math.Max(0f, total),
            };
            return breakdown.total;
        }

        /// <summary>How settled a meditation is (0-1): recovery starts at 1.</summary>
        public static float MeditationSettled(SoulRecoveryBalance r, float seconds)
        {
            if (r == null || r.meditationSettleSeconds <= 0f) return 1f;
            return Math.Max(0f, Math.Min(1f, seconds / r.meditationSettleSeconds));
        }

        private static float Finite(float v, float fallback) => float.IsNaN(v) || float.IsInfinity(v) ? fallback : v;
    }
}
