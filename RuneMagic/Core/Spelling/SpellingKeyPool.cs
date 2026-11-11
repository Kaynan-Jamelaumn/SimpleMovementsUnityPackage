using System;
using System.Collections.Generic;
using System.Text;

namespace RuneMagic
{
    /// <summary>Which keys a sequence may ask for, and why the others were left out.</summary>
    public sealed class SpellingKeyPool
    {
        /// <summary>The keys that may be asked for, as names of the Input System's Key enum ("A", "Q"...), in the candidates' order.</summary>
        public readonly List<string> keys = new List<string>();
        /// <summary>Keys left out because a movement action uses them (the player's own bindings, as rebound).</summary>
        public readonly List<string> leftOutMovement = new List<string>();
        /// <summary>Keys left out because another gameplay action (an ability, interact, inventory...) uses them.</summary>
        public readonly List<string> leftOutGameplay = new List<string>();
        /// <summary>Keys left out because the game reads them outside the Input System's actions.</summary>
        public readonly List<string> leftOutReserved = new List<string>();

        public int Count => keys.Count;

        // ------------------------------------------------------------------ building (the player's machine)
        /// <summary>
        /// The pool of the player's machine, from the live bindings (design spec 16.5: movement keys, default WASD or rebound, must
        /// never be used as casting keys): the candidates, minus the keys the game reads outside actions, minus the keys an ability
        /// uses (always), minus the keys any gameplay action uses (when the balance says so), minus the movement keys - unless the
        /// spell roots the caster and the balance allows it (the keys of abilities stay out).
        /// </summary>
        /// <param name="movementKeys">Keys of the movement actions as bound now.</param>
        /// <param name="gameplayKeys">Keys of every other action as bound now.</param>
        /// <param name="abilityKeys">Keys of the ability slots as bound now (never used).</param>
        /// <param name="rooted">The spell roots the caster: movement keys may be asked for.</param>
        public static SpellingKeyPool Build(SpellingBalance b, IEnumerable<string> movementKeys, IEnumerable<string> gameplayKeys,
            IEnumerable<string> abilityKeys, bool rooted)
        {
            var pool = new SpellingKeyPool();
            if (b == null) return pool;
            var movement = new HashSet<string>(Canon(movementKeys), StringComparer.OrdinalIgnoreCase);
            var gameplay = new HashSet<string>(Canon(gameplayKeys), StringComparer.OrdinalIgnoreCase);
            var ability = new HashSet<string>(Canon(abilityKeys), StringComparer.OrdinalIgnoreCase);
            var reserved = new HashSet<string>(Canon(b.reservedKeys), StringComparer.OrdinalIgnoreCase);
            bool movementAllowed = rooted && b.allowMovementKeysWhenRooted;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in b.candidateKeys ?? new List<string>())
            {
                string k = raw?.Trim();
                if (string.IsNullOrEmpty(k) || !seen.Add(k)) continue;
                if (reserved.Contains(k)) pool.leftOutReserved.Add(k);
                else if (ability.Contains(k)) pool.leftOutGameplay.Add(k);
                else if (movement.Contains(k) && !movementAllowed) pool.leftOutMovement.Add(k);
                else if (b.excludeGameplayKeys && gameplay.Contains(k)) pool.leftOutGameplay.Add(k);
                else pool.keys.Add(k);
            }
            return pool;
        }

        /// <summary>Is the pool big enough for the Spelling System (the balance's minimum pool size)?</summary>
        public bool IsUsable(SpellingBalance b) => b != null && keys.Count >= Math.Max(2, b.minPoolSize);

        // ------------------------------------------------------------------ the server's check of what a client reports
        /// <summary>
        /// The pool a client reported, cleaned up by the server's own rules: only candidate keys, none of the reserved ones, no
        /// duplicates, in the candidates' order. (Only the client knows its movement bindings: those it must have left out itself;
        /// a pool that lies only changes which letters it is asked for, never how well it can do.)
        /// </summary>
        public static List<string> Sanitize(SpellingBalance b, IEnumerable<string> reported)
        {
            var clean = new List<string>();
            if (b == null || reported == null) return clean;
            var asked = new HashSet<string>(Canon(reported), StringComparer.OrdinalIgnoreCase);
            var reserved = new HashSet<string>(Canon(b.reservedKeys), StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in b.candidateKeys ?? new List<string>())
            {
                string k = raw?.Trim();
                if (string.IsNullOrEmpty(k) || !seen.Add(k) || reserved.Contains(k)) continue;
                if (asked.Contains(k)) clean.Add(k);
            }
            return clean;
        }

        // ------------------------------------------------------------------ on the wire
        /// <summary>The longest pool text the server accepts (a request's text limit also applies).</summary>
        public const int MaxPoolKeys = 64;

        public static string ToText(IEnumerable<string> keys)
        {
            var sb = new StringBuilder();
            int n = 0;
            foreach (string k in keys ?? new List<string>())
            {
                if (string.IsNullOrEmpty(k) || k.IndexOf(',') >= 0) continue;
                if (n++ >= MaxPoolKeys) break;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(k);
            }
            return sb.ToString();
        }

        public static List<string> FromText(string text)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(text)) return list;
            foreach (string part in text.Split(','))
            {
                string k = part.Trim();
                if (k.Length > 0 && k.Length <= 24 && list.Count < MaxPoolKeys) list.Add(k);
            }
            return list;
        }

        private static IEnumerable<string> Canon(IEnumerable<string> keys)
        {
            if (keys == null) yield break;
            foreach (string k in keys)
                if (!string.IsNullOrWhiteSpace(k)) yield return k.Trim();
        }
    }

    /// <summary>The letters of a sequence, drawn from a pool by a seed (the same seed gives the same letters on every machine).</summary>
    public static class SpellingSequence
    {
        /// <summary>
        /// <paramref name="count"/> keys of the pool: all different while the pool allows it (the pool is shuffled and the first keys
        /// taken); when the pool is smaller than the count, the keys are used again, never the same twice in a row.
        /// </summary>
        public static List<string> Generate(int seed, IReadOnlyList<string> pool, int count)
        {
            var letters = new List<string>();
            if (pool == null || pool.Count == 0 || count <= 0) return letters;
            var rng = new SynthesisRng(seed);
            string previous = null;
            while (letters.Count < count)
            {
                var bag = new List<string>(pool);
                for (int i = bag.Count - 1; i > 0; i--)
                {
                    int j = rng.NextInt(i + 1);
                    string tmp = bag[i]; bag[i] = bag[j]; bag[j] = tmp;
                }
                foreach (string k in bag)
                {
                    if (letters.Count >= count) break;
                    if (bag.Count > 1 && string.Equals(k, previous, StringComparison.OrdinalIgnoreCase)) continue;
                    letters.Add(k);
                    previous = k;
                }
            }
            return letters;
        }
    }
}
