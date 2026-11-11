using System;
using System.Collections.Generic;

namespace RuneMagic
{
    /// <summary>One glyph of the rune-tracing minigame: where on the circle (0-1) the marker must be, and how wide the target is.</summary>
    [Serializable]
    public class TraceNode
    {
        public float center;
        public float width;
    }

    /// <summary>
    /// A minigame the server issued (design spec 17.1): everything the client needs to play it, nothing about the riddle
    /// that follows. Plain data, sent to the owner as JSON.
    /// </summary>
    [Serializable]
    public class MinigameChallenge
    {
        public string minigameId = "";
        public int seed;
        public List<TraceNode> nodes = new List<TraceNode>();
        [UnityEngine.Tooltip("Laps of the marker per second.")]
        public float speed = 0.3f;
        [UnityEngine.Tooltip("Where on the circle the marker starts (0-1).")]
        public float phase;
        public float timeLimit = 40f;
        public float minScore = 0.6f;
    }

    /// <summary>What the server made of a played minigame.</summary>
    public struct MinigameResult
    {
        public bool valid;
        public string reason;
        public float score;
        public int hits;
        public bool passed;
    }

    /// <summary>
    /// A discovery minigame (design spec 17.1, step 4): pluggable. The server creates a challenge from a seed and the band's
    /// difficulty, the client plays it, and the server evaluates what the client reports. Implementations are pure.
    /// Register one with <see cref="DiscoveryMinigames.Register"/> and name its id in a source definition.
    /// </summary>
    public interface IDiscoveryMinigame
    {
        string Id { get; }

        /// <summary>A challenge for <paramref name="tier"/>; <paramref name="narrowing"/> (0-1) makes it harder for complex concepts.</summary>
        MinigameChallenge Create(int seed, DiscoveryTierDef tier, float narrowing, DiscoveryBalance balance);

        /// <summary>
        /// Judges the client's report: <paramref name="presses"/> are the seconds, on the client's clock since it started, at
        /// which it sealed each glyph; <paramref name="serverElapsed"/> is what the server measured since it issued the challenge.
        /// A report that cannot have been played (too fast, from the future, the wrong length) is invalid.
        /// </summary>
        MinigameResult Evaluate(MinigameChallenge challenge, IList<float> presses, float serverElapsed, DiscoveryBalance balance);
    }

    /// <summary>The registered discovery minigames.</summary>
    public static class DiscoveryMinigames
    {
        private static readonly Dictionary<string, IDiscoveryMinigame> all = new Dictionary<string, IDiscoveryMinigame>(StringComparer.Ordinal);
        private static readonly RuneTraceMinigame builtIn = new RuneTraceMinigame();

        /// <summary>Id of the built-in rune-tracing minigame.</summary>
        public const string RuneTrace = "rune_trace";

        /// <summary>Registers a minigame (the same id replaces).</summary>
        public static void Register(IDiscoveryMinigame minigame)
        {
            if (minigame != null && !string.IsNullOrEmpty(minigame.Id)) all[minigame.Id] = minigame;
        }

        public static void Unregister(string id)
        {
            if (!string.IsNullOrEmpty(id)) all.Remove(id);
        }

        /// <summary>The minigame with this id; the built-in rune tracing when the id is empty or unknown.</summary>
        public static IDiscoveryMinigame Get(string id) =>
            !string.IsNullOrEmpty(id) && all.TryGetValue(id, out IDiscoveryMinigame m) ? m : builtIn;
    }

    /// <summary>
    /// The built-in minigame (design spec 17.1, "complete a minigame"): a marker runs around a rune circle; the player seals
    /// each glyph by stopping the marker inside its target. The marker's place is a pure function of time, so the server
    /// can replay the reported moments exactly.
    /// </summary>
    public sealed class RuneTraceMinigame : IDiscoveryMinigame
    {
        public string Id => DiscoveryMinigames.RuneTrace;

        /// <summary>Most glyphs a challenge can have (a report must fit a network request).</summary>
        public const int MaxNodes = 16;

        public MinigameChallenge Create(int seed, DiscoveryTierDef tier, float narrowing, DiscoveryBalance balance)
        {
            var rng = new Random(seed);
            int n = Math.Max(1, Math.Min(MaxNodes, tier != null ? tier.minigameNodes : 3));
            float narrow = balance != null ? Clamp01(narrowing) * balance.complexityNarrowing : 0f;
            float width = Math.Max(0.02f, (tier != null ? tier.nodeWidth : 0.18f) * (1f - narrow));
            var c = new MinigameChallenge
            {
                minigameId = Id,
                seed = seed,
                speed = Math.Max(0.05f, tier != null ? tier.minigameSpeed : 0.3f),
                phase = (float)rng.NextDouble(),
                timeLimit = Math.Max(5f, tier != null ? tier.timeLimit : 40f),
                minScore = tier != null ? Clamp01(tier.minScore) : 0.6f,
            };
            float previous = -1f;
            for (int i = 0; i < n; i++)
            {
                float center;
                int guard = 0;
                do { center = (float)rng.NextDouble(); }
                while (previous >= 0f && CircularDistance(center, previous) < 0.15f && ++guard < 20);
                previous = center;
                c.nodes.Add(new TraceNode { center = center, width = width });
            }
            return c;
        }

        /// <summary>Where the marker is (0-1 around the circle) <paramref name="t"/> seconds after the start.</summary>
        public static float MarkerAt(MinigameChallenge c, float t)
        {
            double pos = (c.phase + (double)c.speed * t) % 1.0;
            if (pos < 0) pos += 1.0;
            return (float)pos;
        }

        /// <summary>Distance between two places on the circle (0 to 0.5).</summary>
        public static float CircularDistance(float a, float b)
        {
            float d = Math.Abs(a - b) % 1f;
            return d > 0.5f ? 1f - d : d;
        }

        /// <summary>The credit (0-1) of sealing a glyph at marker place <paramref name="marker"/>: 1 inside the target, half to none just outside.</summary>
        public static float NodeScore(TraceNode node, float marker, out bool hit)
        {
            float d = CircularDistance(marker, node.center);
            float half = node.width * 0.5f;
            hit = d <= half;
            if (hit) return 1f;
            return Math.Max(0f, 1f - (d - half) / Math.Max(0.01f, node.width)) * 0.5f;
        }

        public MinigameResult Evaluate(MinigameChallenge c, IList<float> presses, float serverElapsed, DiscoveryBalance balance)
        {
            var r = new MinigameResult { valid = false, reason = "" };
            if (c == null || c.nodes == null || c.nodes.Count == 0) { r.reason = "No such challenge."; return r; }
            if (presses == null || presses.Count != c.nodes.Count) { r.reason = "The glyphs were not all sealed."; return r; }
            float minInterval = balance != null ? balance.minPressInterval : 0.25f;
            float slack = balance != null ? balance.clockSlack : 1.5f;
            float last = -1000f;
            for (int i = 0; i < presses.Count; i++)
            {
                float t = presses[i];
                if (float.IsNaN(t) || float.IsInfinity(t) || t < 0f) { r.reason = "Impossible timing."; return r; }
                if (i > 0 && t - last < minInterval - 0.001f) { r.reason = "Too fast to be a human hand."; return r; }
                last = t;
            }
            if (last > c.timeLimit + 0.001f) { r.reason = "The time ran out."; return r; }
            if (last > serverElapsed + slack) { r.reason = "The timing does not match the clock."; return r; }

            float sum = 0f;
            int hits = 0;
            for (int i = 0; i < presses.Count; i++)
            {
                sum += NodeScore(c.nodes[i], MarkerAt(c, presses[i]), out bool hit);
                if (hit) hits++;
            }
            r.valid = true;
            r.hits = hits;
            r.score = sum / c.nodes.Count;
            r.passed = r.score + 1e-4f >= c.minScore;
            return r;
        }

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
