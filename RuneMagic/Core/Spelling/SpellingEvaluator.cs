using System;
using System.Collections.Generic;

namespace RuneMagic
{
    /// <summary>What a scoring term is given: the parameters, the replayed sequence and the letters.</summary>
    public struct SpellingTermInput
    {
        public SpellingParams p;
        public SpellingOutcome o;
        public IReadOnlyList<string> letters;
    }

    /// <summary>
    /// Replays a key sequence and scores it (design spec 16.5): which keys were right, missed, wrong or off the beat, how fast and
    /// how precisely, whether the spell fails (Strict or Lenient), and what the result does to the spell. Pure and deterministic: the
    /// server runs it on the presses a client reports; the client's overlay uses the same rules to know when to stop.
    /// <para>The evaluation is extensible: a term is a function from the replay to a score (0-1); register one with
    /// <see cref="Register"/> and give it a weight in the balance, without touching this class.</para>
    /// </summary>
    public static class SpellingEvaluator
    {
        /// <summary>A key may be pressed this long after its window ended and still count (frame and network jitter).</summary>
        public const float TimingSlack = 0.05f;

        private static readonly Dictionary<string, Func<SpellingTermInput, float>> terms =
            new Dictionary<string, Func<SpellingTermInput, float>>(StringComparer.OrdinalIgnoreCase)
            {
                { "accuracy", Accuracy },
                { "speed", Speed },
                { "precision", Precision },
                { "rhythm", Rhythm },
            };

        /// <summary>Is there a scoring term with this id?</summary>
        public static bool HasTerm(string id) => id != null && terms.ContainsKey(id);

        /// <summary>
        /// Adds (or replaces) a scoring term: it returns a score from 0 to 1, or a negative number when it does not apply to the
        /// sequence (it then counts for nothing). Its weight comes from the balance's Weights.
        /// </summary>
        public static void Register(string id, Func<SpellingTermInput, float> score)
        {
            if (!string.IsNullOrWhiteSpace(id) && score != null) terms[id.Trim()] = score;
        }

        // ------------------------------------------------------------------ the replay
        /// <summary>
        /// Replays <paramref name="presses"/> against <paramref name="letters"/>. Each key has a window that opens when the key before
        /// it was hit (the first at 0) and closes <see cref="SpellingParams.WindowOf"/> seconds later: a key not hit in time is a
        /// missed key and the next letter's window starts. A wrong key is a mistake and the window stays open. After the total limit
        /// nothing counts. With rhythm, a key pressed more than the tolerance from its beat is a mistake too. (This is a
        /// <see cref="SpellingTracker"/> run to its end: the overlay uses the same tracker live.)
        /// </summary>
        public static SpellingOutcome Evaluate(SpellingParams p, IReadOnlyList<string> letters, IList<SpellingPress> presses)
        {
            var tracker = new SpellingTracker(p, letters);
            if (presses != null)
                foreach (SpellingPress press in presses)
                    tracker.Press(press.key, press.t);
            tracker.Finish();
            return Complete(p, letters, tracker);
        }

        /// <summary>
        /// The result of a sequence that was not played (a machine that cannot spell: no keyboard, or no usable keys): a success that
        /// earns the least a success can, as if it had the most mistakes the spell allows (at least one) and no bonus, and costs the soul
        /// nothing for mistakes. Never better than playing it badly, so skipping it is not a way to a better cast.
        /// </summary>
        public static SpellingOutcome Skipped(SpellingParams p)
        {
            p.EnsureLists();
            var o = new SpellingOutcome { success = true, performance = 0f, damageKind = p.damageKind ?? "" };
            o.missed = Math.Max(1, p.maxMistakes);
            Consequences(p, o);
            o.mistakeDamage = 0f;
            return o;
        }

        /// <summary>Scores a finished tracker: the outcome with its performance and what it does to the spell and the soul.</summary>
        public static SpellingOutcome Complete(SpellingParams p, IReadOnlyList<string> letters, SpellingTracker tracker)
        {
            SpellingOutcome o = tracker.Outcome;
            o.duration = Math.Max(tracker.LastHit, o.hits == letters.Count ? tracker.LastHit : tracker.StepOpen);
            if (tracker.IsFatal)
            {
                o.success = false;
                o.failure = p.model == SpellingFailureModel.Strict ? SpellingFailure.Mistake : SpellingFailure.TooManyMistakes;
            }
            else o.success = true;
            Score(p, letters, o);
            Consequences(p, o);
            return o;
        }

        // ------------------------------------------------------------------ the score
        private static void Score(SpellingParams p, IReadOnlyList<string> letters, SpellingOutcome o)
        {
            o.accuracy = Accuracy(new SpellingTermInput { p = p, o = o, letters = letters });
            o.speed = Speed(new SpellingTermInput { p = p, o = o, letters = letters });
            o.precision = Precision(new SpellingTermInput { p = p, o = o, letters = letters });
            float rhythm = Rhythm(new SpellingTermInput { p = p, o = o, letters = letters });
            o.rhythm = rhythm < 0f ? 0f : rhythm;

            float sum = 0f, weights = 0f;
            foreach (SpellingWeight w in p.weights)
            {
                if (w == null || w.weight <= 0f || !terms.TryGetValue(w.term ?? "", out Func<SpellingTermInput, float> f)) continue;
                float v = f(new SpellingTermInput { p = p, o = o, letters = letters });
                if (v < 0f) continue; // does not apply (rhythm without a beat)
                sum += Clamp01(v) * w.weight;
                weights += w.weight;
            }
            o.performance = weights > 0f ? sum / weights : o.accuracy;
            float limit = p.EffectiveLimit;
            o.speedBonus = o.success && limit > 0f && o.duration <= p.speedBonusThreshold * limit && o.Mistakes <= p.speedBonusMaxMistakes;
        }

        private static float Accuracy(SpellingTermInput t) => Clamp01(1f - t.o.Mistakes / (float)Math.Max(1, t.letters.Count));

        private static float Speed(SpellingTermInput t)
        {
            float limit = t.p.EffectiveLimit;
            if (limit <= 0f) return 1f;
            float fast = t.p.speedBonusThreshold * limit;
            if (t.o.duration <= fast) return 1f;
            return Clamp01((limit - t.o.duration) / Math.Max(0.01f, limit - fast));
        }

        // How early in its window each key was hit (a missed key scores 0).
        private static float Precision(SpellingTermInput t)
        {
            int n = t.letters.Count;
            if (n == 0) return 1f;
            float sum = 0f, open = 0f;
            for (int i = 0; i < n; i++)
            {
                SpellingStep s = t.o.steps[i];
                float w = Math.Max(0.01f, t.p.WindowOf(i));
                if (s.state == SpellingStepState.Hit) { sum += Clamp01(1f - (s.t - open) / w); open = s.t; }
                else open = s.t;
            }
            return sum / n;
        }

        // How close to the beat each key was hit; -1 when the spell has no rhythm.
        private static float Rhythm(SpellingTermInput t)
        {
            if (!t.p.rhythm) return -1f;
            int n = t.letters.Count;
            if (n == 0) return 1f;
            float sum = 0f, tol = Math.Max(0.01f, t.p.tolerance);
            for (int i = 0; i < n; i++)
            {
                SpellingStep s = t.o.steps[i];
                if (s.state != SpellingStepState.Hit) continue;
                float err = Math.Abs(s.t - t.p.BeatOf(i));
                sum += err <= tol ? 1f : Clamp01(1f - (err - tol) / tol);
            }
            return sum / n;
        }

        // ------------------------------------------------------------------ what it does
        private static void Consequences(SpellingParams p, SpellingOutcome o)
        {
            int m = o.Mistakes;
            o.mistakeDamage = MistakeDamage(p, m, !o.success);
            if (!o.success) return;
            float swing = Clamp01(p.powerSwing);
            o.powerMultiplier = Lerp(1f - swing, 1f + swing, o.performance) * Math.Max(0.2f, 1f - p.powerPerMistake * m);
            o.castTimeMultiplier = 1f + p.castTimePerMistake * m;
            o.stabilityDelta = -p.stabilityPerMistake * m;
            if (o.speedBonus)
            {
                o.powerMultiplier *= 1f + p.speedBonusPower;
                o.costMultiplier *= 1f - Clamp01(p.speedBonusCost);
                o.strainMultiplier *= 1f - Clamp01(p.speedBonusStrain);
                o.stabilityDelta += p.speedBonusStability;
            }
        }

        /// <summary>
        /// The Soul Damage the mistakes of a sequence cost (design spec 16.5, never automatic): nothing, the same for each mistake,
        /// more for each further mistake, or one amount when the sequence fails.
        /// </summary>
        public static float MistakeDamage(SpellingParams p, int mistakes, bool failed)
        {
            if (p == null || mistakes <= 0 && !failed) return 0f;
            float a = Math.Max(0f, p.damageAmount);
            switch (p.damageMode)
            {
                case SpellingDamageMode.FlatPerMistake: return a * mistakes;
                case SpellingDamageMode.ScalingPerMistake:
                    {
                        float sum = 0f;
                        for (int k = 0; k < mistakes; k++) sum += a * (1f + Math.Max(0f, p.damageScaling) * k);
                        return sum;
                    }
                case SpellingDamageMode.OnceOnFailure: return failed ? a : 0f;
                default: return 0f;
            }
        }

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;
        private static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    }

    /// <summary>
    /// A key sequence as it happens (design spec 16.5): which letter is asked for, which windows have closed, how many mistakes there
    /// are, whether the spell has already failed. The overlay advances it with the clock and feeds it the key presses; the server
    /// runs the same rules over the presses a client reports (<see cref="SpellingEvaluator.Evaluate"/>).
    /// </summary>
    public sealed class SpellingTracker
    {
        private readonly SpellingParams p;
        private readonly IReadOnlyList<string> letters;
        private readonly float limit;
        private int step;
        private float open;
        private float lastHit;

        /// <summary>The counts and the letters' results so far (completed by <see cref="SpellingEvaluator.Complete"/>).</summary>
        public SpellingOutcome Outcome { get; } = new SpellingOutcome();

        public SpellingTracker(SpellingParams parameters, IReadOnlyList<string> sequence)
        {
            p = parameters;
            p.EnsureLists();
            letters = sequence;
            float l = p.EffectiveLimit;
            limit = l <= 0f ? float.MaxValue : l;
            for (int i = 0; i < letters.Count; i++) Outcome.steps.Add(new SpellingStep());
            Outcome.damageKind = p.damageKind ?? "";
        }

        /// <summary>The letter asked for now (the letters' count when every one has been answered).</summary>
        public int Step => step;
        /// <summary>The spell has failed already (Strict: a mistake; Lenient: too many).</summary>
        public bool IsFatal => p.model == SpellingFailureModel.Strict ? Outcome.Mistakes > 0 : Outcome.Mistakes > p.maxMistakes;
        /// <summary>Nothing more can happen: every letter answered, or the spell failed.</summary>
        public bool Over => step >= letters.Count || IsFatal;
        public float LastHit => lastHit;
        /// <summary>When the window of the letter asked for now opened (seconds after the first prompt).</summary>
        public float StepOpen => open;
        /// <summary>When the window of the letter asked for now closes.</summary>
        public float StepDeadline => open + p.WindowOf(step);
        public float Limit => limit;

        /// <summary>The clock is at <paramref name="t"/> seconds: the windows that closed before it missed their letters.</summary>
        public void Advance(float t)
        {
            while (!Over && step < letters.Count && t > open + p.WindowOf(step) + SpellingEvaluator.TimingSlack)
                Miss();
        }

        /// <summary>A key was pressed at <paramref name="t"/> seconds: a right key hits the letter, a wrong one is a mistake.</summary>
        public void Press(string key, float t)
        {
            if (Over) return;
            Advance(t);
            if (Over || t > limit + SpellingEvaluator.TimingSlack) return;
            if (!string.Equals(key, letters[step], StringComparison.OrdinalIgnoreCase))
            {
                Outcome.wrongKeys++;
                SpellingStep s = Outcome.steps[step];
                s.wrongKeys++;
                Outcome.steps[step] = s;
                return;
            }
            bool off = p.rhythm && Math.Abs(t - p.BeatOf(step)) > p.tolerance;
            Outcome.steps[step] = new SpellingStep { state = SpellingStepState.Hit, t = t, offBeat = off, wrongKeys = Outcome.steps[step].wrongKeys };
            Outcome.hits++;
            if (off) Outcome.offBeat++;
            lastHit = t;
            open = t;
            step++;
        }

        /// <summary>The sequence ends: the letters nobody answered (it ran out of time, or the spell failed first) are missed.</summary>
        public void Finish()
        {
            while (!IsFatal && step < letters.Count) Miss();
        }

        private void Miss()
        {
            float end = open + p.WindowOf(step);
            SpellingStep s = Outcome.steps[step];
            s.state = SpellingStepState.Missed;
            s.t = end;
            Outcome.steps[step] = s;
            Outcome.missed++;
            step++;
            open = end;
        }
    }
}
