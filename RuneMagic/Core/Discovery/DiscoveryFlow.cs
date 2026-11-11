using System;
using System.Collections.Generic;

namespace RuneMagic
{
    /// <summary>Where a discovery is (design spec 17.1).</summary>
    public enum DiscoveryPhase : byte
    {
        None = 0,
        /// <summary>The minigame is being played.</summary>
        Minigame = 1,
        /// <summary>A riddle waits for an answer.</summary>
        Riddle = 2,
        /// <summary>Ended: the concept was learned (the view says which).</summary>
        Learned = 3,
        /// <summary>Ended without learning (wrong answer, failed minigame, abandoned): the view says why.</summary>
        Failed = 4,
    }

    /// <summary>
    /// A discovery in progress, held where the rules run. It knows the secret (the concept and the correct options); only
    /// <see cref="DiscoveryView"/> goes to the player's machine, and that holds neither.
    /// </summary>
    public sealed class DiscoverySession
    {
        public string sourceId = "";
        public string sourceName = "";
        public string conceptId = "";
        public string runeId = "";
        public int tierIndex;
        public DiscoveryTierDef tier;
        public DiscoveryPhase phase;
        public MinigameChallenge challenge;
        public float startedAt;
        public float minigameIssuedAt;
        public int step;
        public List<DiscoveryQuestion> questions = new List<DiscoveryQuestion>();
        public Random rng;

        public string Key => DiscoveryRules.BurnKey(sourceId, conceptId);
        public bool Active => phase == DiscoveryPhase.Minigame || phase == DiscoveryPhase.Riddle;
        public DiscoveryQuestion Question => phase == DiscoveryPhase.Riddle && step >= 0 && step < questions.Count ? questions[step] : null;
    }

    /// <summary>
    /// What the owner's UI sees of a discovery. By construction it holds no secret: not the concept, not which option is right,
    /// not the riddles that follow the one shown. Plain data, sent as JSON.
    /// </summary>
    [Serializable]
    public class DiscoveryView
    {
        public int version;
        public DiscoveryPhase phase;
        public string sourceName = "";
        public string tierName = "";
        public int tierIndex;
        [UnityEngine.Tooltip("Minigame phase: the challenge to play (the client starts its clock when it arrives).")]
        public MinigameChallenge challenge;
        [UnityEngine.Tooltip("Riddle phase: step (1-based) of steps, the riddle and its options (shuffled).")]
        public int step;
        public int steps;
        public string text = "";
        public List<string> options = new List<string>();
        [UnityEngine.Tooltip("Minigame attempts left before this source is burned for this concept (a failed one ends the discovery).")]
        public int attemptsLeft;
        [UnityEngine.Tooltip("Ended: what happened.")]
        public string result = "";
        [UnityEngine.Tooltip("Learned: the concept's name (only after it was learned).")]
        public string learnedName = "";
        public bool burned;

        public bool Active => phase == DiscoveryPhase.Minigame || phase == DiscoveryPhase.Riddle;

        public DiscoveryView Clone()
        {
            var c = (DiscoveryView)MemberwiseClone();
            c.options = new List<string>(options ?? new List<string>());
            return c;
        }
    }

    /// <summary>The result of starting a discovery.</summary>
    public struct DiscoveryStart
    {
        public bool ok;
        public string reason;
        public DiscoverySession session;
    }

    public enum MinigameOutcome
    {
        /// <summary>No minigame is being played.</summary>
        None,
        /// <summary>Failed; the player may try again at the source (attempts left).</summary>
        Failed,
        /// <summary>Failed for the last time: the source is burned for this concept.</summary>
        Burned,
        /// <summary>Passed: the riddle begins.</summary>
        Passed,
    }

    public enum AnswerOutcome
    {
        /// <summary>No riddle is waiting, or the option does not exist.</summary>
        None,
        Wrong,
        /// <summary>Correct; another question follows.</summary>
        NextStep,
        /// <summary>Correct, the last one: the concept is permanently learned.</summary>
        Learned,
    }

    /// <summary>
    /// The discovery state machine (design spec 17.1), pure: start at a source, play the minigame, answer the riddle(s). It
    /// reads and changes only the <see cref="RuneSoulState"/> it is given (known concepts, burned sources, attempts, riddles
    /// seen, the log), so what a wrong answer burns and what a right one keeps is saved with the soul.
    /// <list type="bullet">
    /// <item>A correct answer (every step) permanently adds the concept to the player's knowledge.</item>
    /// <item>A wrong answer burns this source definition for this concept: it can never be tried again by this player.</item>
    /// <item>A failed minigame uses one attempt; the last one burns the source for the concept.</item>
    /// <item>Leaving a riddle unanswered counts as wrong (when the balance says so).</item>
    /// </list>
    /// </summary>
    public static class DiscoveryFlow
    {
        // ------------------------------------------------------------------ start
        /// <summary>
        /// Starts a discovery at the source <paramref name="sourceId"/>: picks the concept (secret), the band and the minigame.
        /// <paramref name="dungeonTier"/> sets the band unless <paramref name="tierOverride"/> is 0 or more;
        /// <paramref name="fixedConcept"/> forces the concept (when it can be taught). <paramref name="heldRunes"/> are the rune
        /// ids of the runes the player has absorbed.
        /// </summary>
        public static DiscoveryStart Start(RuneCatalog c, RuneSoulState s, string sourceId, int dungeonTier, int tierOverride,
            string fixedConcept, IList<string> heldRunes, Random rng, float now)
        {
            var r = new DiscoveryStart { reason = "" };
            DiscoverySourceDef src = c?.DiscoverySource(sourceId);
            if (c == null || s == null || rng == null) { r.reason = "Nothing happens."; return r; }
            if (src == null) { r.reason = $"This source ('{sourceId}') is not defined in the Rune Magic Database."; return r; }
            int tierIndex = DiscoveryRules.TierIndex(c, dungeonTier, tierOverride);
            DiscoveryTierDef tier = c.Discovery.Tier(tierIndex);
            if (tier == null) { r.reason = "Concept discovery has no difficulty bands."; return r; }
            if (!src.WorksAt(tierIndex)) { r.reason = $"{src.Name} does not work at this depth ({tier.Name})."; return r; }

            List<ConceptCandidate> candidates = DiscoveryRules.Candidates(c, s, src, tierIndex, heldRunes, out string why);
            if (candidates.Count == 0) { r.reason = why; return r; }
            ConceptCandidate chosen = default;
            bool forced = false;
            if (!string.IsNullOrEmpty(fixedConcept))
                foreach (ConceptCandidate k in candidates)
                    if (k.concept.id == fixedConcept) { chosen = k; forced = true; }
            if (!forced) chosen = DiscoveryRules.Pick(candidates, rng);

            var session = new DiscoverySession
            {
                sourceId = src.id,
                sourceName = src.Name,
                conceptId = chosen.concept.id,
                runeId = chosen.rune.id,
                tierIndex = tierIndex,
                tier = tier,
                phase = DiscoveryPhase.Minigame,
                startedAt = now,
                minigameIssuedAt = now,
                rng = rng,
            };
            IDiscoveryMinigame game = DiscoveryMinigames.Get(src.minigame);
            session.challenge = game.Create(rng.Next(), tier, chosen.concept.complexity, c.Discovery);
            r.ok = true;
            r.session = session;
            return r;
        }

        // ------------------------------------------------------------------ minigame
        /// <summary>
        /// Judges the minigame the client played: <paramref name="presses"/> as reported, <paramref name="now"/> the server's
        /// clock. An invalid report counts as a failed try. A pass begins the riddle; the last failed try burns the source.
        /// </summary>
        public static MinigameOutcome SubmitMinigame(RuneCatalog c, RuneSoulState s, DiscoverySession session, IList<float> presses, float now,
            long utc, out string message)
        {
            message = "";
            if (session == null || session.phase != DiscoveryPhase.Minigame) return MinigameOutcome.None;
            IDiscoveryMinigame game = DiscoveryMinigames.Get(session.challenge != null ? session.challenge.minigameId : "");
            MinigameResult result = game.Evaluate(session.challenge, presses, Math.Max(0f, now - session.minigameIssuedAt), c.Discovery);
            if (result.valid && result.passed)
            {
                var used = new List<string>();
                session.questions = DiscoveryRules.BuildQuestions(c, s, c.Concept(session.conceptId), session.tierIndex, session.rng, used);
                if (session.questions.Count == 0)
                {
                    // Nothing to ask (a broken catalog): ends without cost to the player.
                    session.phase = DiscoveryPhase.Failed;
                    message = "The glyphs fall silent.";
                    return MinigameOutcome.Failed;
                }
                foreach (string id in used)
                    if (!s.discovery.seenRiddles.Contains(id)) s.discovery.seenRiddles.Add(id);
                session.step = 0;
                session.phase = DiscoveryPhase.Riddle;
                message = "The glyphs settle. A riddle forms.";
                return MinigameOutcome.Passed;
            }

            // A failed try (an invalid report too).
            session.phase = DiscoveryPhase.Failed;
            int used2 = UseAttempt(s, session);
            int max = Math.Max(1, session.tier != null ? session.tier.attempts : 1);
            string why = result.valid ? "The glyphs scatter: you could not seal them." : result.reason;
            if (used2 >= max)
            {
                Burn(s, session);
                Log(c, s, session, "minigame_failed", utc);
                s.discovery.failed++;
                message = why + " The source falls dark for you: you will have to find another place that teaches this.";
                return MinigameOutcome.Burned;
            }
            Log(c, s, session, "minigame_failed", utc);
            message = why + $" You may try again ({max - used2} {(max - used2 == 1 ? "try" : "tries")} left).";
            return MinigameOutcome.Failed;
        }

        // ------------------------------------------------------------------ riddle
        /// <summary>Evaluates the player's answer to the current question (<paramref name="option"/> is an index into the options shown).</summary>
        public static AnswerOutcome SubmitAnswer(RuneCatalog c, RuneSoulState s, DiscoverySession session, int option, long utc, out string message)
        {
            message = "";
            DiscoveryQuestion q = session?.Question;
            if (q == null || option < 0 || option >= q.options.Count) return AnswerOutcome.None;
            if (option != q.correct)
            {
                session.phase = DiscoveryPhase.Failed;
                Burn(s, session);
                s.discovery.failed++;
                Log(c, s, session, "wrong", utc);
                message = "That is not the principle. The glyphs crumble to dust: this source can teach you nothing more of it. " +
                          "Find another place that holds this knowledge.";
                return AnswerOutcome.Wrong;
            }
            if (session.step + 1 < session.questions.Count)
            {
                session.step++;
                message = "Correct. The glyphs ask more of you.";
                return AnswerOutcome.NextStep;
            }
            ConceptDef concept = c.Concept(session.conceptId);
            session.phase = DiscoveryPhase.Learned;
            RuneRules.Learn(s, session.runeId, session.conceptId);
            ClearAttempts(s, session);
            s.discovery.learned++;
            Log(c, s, session, "learned", utc);
            message = $"Understanding floods in: you have discovered {(concept != null ? concept.Name : session.conceptId)}.";
            return AnswerOutcome.Learned;
        }

        // ------------------------------------------------------------------ leaving
        /// <summary>
        /// The player walked away, closed the window or ran out of time. During a riddle it counts as a wrong answer when
        /// <see cref="DiscoveryBalance.abandonBurns"/> is on (otherwise one could read the riddle and come back for another);
        /// during the minigame it uses a try. Returns true when the source was burned.
        /// </summary>
        public static bool Abandon(RuneCatalog c, RuneSoulState s, DiscoverySession session, bool expired, long utc, out string message)
        {
            message = "";
            if (session == null || !session.Active) return false;
            DiscoveryPhase was = session.phase;
            session.phase = DiscoveryPhase.Failed;
            string outcome = expired ? "expired" : "abandoned";
            if (was == DiscoveryPhase.Riddle)
            {
                if (!c.Discovery.abandonBurns)
                {
                    Log(c, s, session, outcome, utc);
                    message = "You step away from the riddle.";
                    return false;
                }
                Burn(s, session);
                s.discovery.failed++;
                Log(c, s, session, outcome, utc);
                message = "Unanswered, the riddle fades. The source will teach you nothing more of it.";
                return true;
            }
            int used = UseAttempt(s, session);
            int max = Math.Max(1, session.tier != null ? session.tier.attempts : 1);
            Log(c, s, session, outcome, utc);
            if (used >= max)
            {
                Burn(s, session);
                s.discovery.failed++;
                message = "The glyphs go dark. The source will teach you nothing more of it.";
                return true;
            }
            message = "The glyphs fade. You may try again.";
            return false;
        }

        /// <summary>
        /// Ends a discovery without any cost to the player: the system interrupted it (the player died, the source vanished,
        /// the server stopped), which is not the player's mistake.
        /// </summary>
        public static void Cancel(DiscoverySession session)
        {
            if (session != null && session.Active) session.phase = DiscoveryPhase.Failed;
        }

        // ------------------------------------------------------------------ view
        /// <summary>What the owner's UI sees (no secret in it). <paramref name="result"/> is the last message of an ended discovery.</summary>
        public static DiscoveryView ViewOf(RuneCatalog c, RuneSoulState s, DiscoverySession session, string result, int version)
        {
            var v = new DiscoveryView { version = version, result = result ?? "" };
            if (session == null) return v;
            v.phase = session.phase;
            v.sourceName = session.sourceName;
            v.tierIndex = session.tierIndex;
            v.tierName = session.tier != null ? session.tier.Name : "";
            int max = Math.Max(1, session.tier != null ? session.tier.attempts : 1);
            v.attemptsLeft = Math.Max(0, max - DiscoveryRules.AttemptsUsed(s, session.sourceId, session.conceptId));
            v.burned = DiscoveryRules.IsBurned(s, session.sourceId, session.conceptId);
            if (session.phase == DiscoveryPhase.Minigame) v.challenge = session.challenge;
            else if (session.phase == DiscoveryPhase.Riddle && session.Question != null)
            {
                v.step = session.step + 1;
                v.steps = session.questions.Count;
                v.text = session.Question.text;
                v.options = new List<string>(session.Question.options); // the labels only: the answer is not sent
            }
            else if (session.phase == DiscoveryPhase.Learned)
            {
                ConceptDef k = c.Concept(session.conceptId);
                v.learnedName = k != null ? k.Name : "";
            }
            return v;
        }

        // ------------------------------------------------------------------ bookkeeping
        private static void Burn(RuneSoulState s, DiscoverySession session)
        {
            if (!s.discovery.burned.Contains(session.Key)) s.discovery.burned.Add(session.Key);
        }

        private static int UseAttempt(RuneSoulState s, DiscoverySession session)
        {
            string key = session.Key;
            foreach (KeyCount a in s.discovery.attempts)
                if (a.key == key) return ++a.count;
            s.discovery.attempts.Add(new KeyCount { key = key, count = 1 });
            return 1;
        }

        private static void ClearAttempts(RuneSoulState s, DiscoverySession session) =>
            s.discovery.attempts.RemoveAll(a => a.key == session.Key);

        private static void Log(RuneCatalog c, RuneSoulState s, DiscoverySession session, string outcome, long utc)
        {
            int cap = c.Discovery.logLength;
            if (cap <= 0) return;
            s.discovery.log.Add(new DiscoveryLogEntry { utc = utc, source = session.sourceId, concept = session.conceptId, tier = session.tierIndex, outcome = outcome });
            while (s.discovery.log.Count > cap) s.discovery.log.RemoveAt(0);
        }
    }
}
