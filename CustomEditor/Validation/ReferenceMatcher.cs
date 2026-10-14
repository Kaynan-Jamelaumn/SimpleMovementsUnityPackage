#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleMovements.Validation
{
    /// <summary>One object that could fill a field: its GameObject name and its type name.</summary>
    public struct ReferenceCandidate
    {
        public string objectName;
        public string typeName;

        public ReferenceCandidate(string objectName, string typeName)
        {
            this.objectName = objectName;
            this.typeName = typeName;
        }
    }

    /// <summary>How a field was matched (or why it was not).</summary>
    public enum MatchKind
    {
        /// <summary>No candidate at all.</summary>
        None,
        /// <summary>The only candidate.</summary>
        Single,
        /// <summary>Several candidates; one name clearly matches the field name.</summary>
        NameMatch,
        /// <summary>Several candidates and no clear winner: nothing is assigned.</summary>
        Ambiguous,
        /// <summary>Candidates exist but none is named like the field (fields that need a name: Transform, GameObject).</summary>
        NoNameMatch,
    }

    /// <summary>The result of <see cref="ReferenceMatcher.Pick"/>.</summary>
    public struct MatchResult
    {
        public MatchKind kind;
        /// <summary>Index of the chosen candidate, or -1.</summary>
        public int index;
        /// <summary>Score of the chosen (or best) candidate, 0 to 100.</summary>
        public int score;

        public bool Found => index >= 0;

        public override string ToString() => kind switch
        {
            MatchKind.Single => "only candidate",
            MatchKind.NameMatch => $"name match ({score}%)",
            MatchKind.Ambiguous => "ambiguous",
            MatchKind.NoNameMatch => "no object named like the field",
            _ => "nothing found",
        };
    }

    /// <summary>
    /// Decides which object fits a field, from names alone (no Unity API, so it is unit tested outside the editor).
    /// Names are compared without case, spaces, underscores, the usual prefixes (<c>m_</c>, <c>_</c>) and suffixes
    /// (<c>Ref</c>, <c>Reference</c>, <c>Component</c>, the type name: <c>hitCollider</c> → <c>hit</c>), word by word
    /// (<c>rightHandSocket</c> ↔ "Right Hand Socket", "RightHand_Socket", "socket.R"...).
    /// </summary>
    public static class ReferenceMatcher
    {
        /// <summary>Score a name match needs to be used when there are several candidates.</summary>
        public const int MinimumNameScore = 50;

        /// <summary>
        /// Score a name match needs for fields any object could fill (Transform, GameObject, or a field whose name says
        /// it points elsewhere - see <see cref="NeedsNameMatch"/>).
        /// </summary>
        public const int MinimumNameOnlyScore = 70;

        /// <summary>How far ahead of the second best the best candidate must be.</summary>
        public const int MinimumLead = 10;

        /// <summary>Words saying a field points somewhere else than the obvious nearby object (or may stay empty).</summary>
        private static readonly HashSet<string> ElsewhereWords = new HashSet<string>(StringComparer.Ordinal)
        {
            "target", "other", "ignore", "ignored", "override", "optional", "external", "enemy", "alternate", "alt",
            "fallback", "previous", "prev", "last", "next", "custom", "debug", "test", "extra", "secondary",
        };

        private static readonly string[] Prefixes = { "m_", "s_", "k_", "_" };
        private static readonly string[] SuffixWords = { "reference", "ref", "component", "comp", "obj", "object", "go" };

        private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["cam"] = "camera",
            ["anim"] = "animator",
            ["rb"] = "rigidbody",
            ["col"] = "collider",
            ["cc"] = "charactercontroller",
            ["l"] = "left",
            ["r"] = "right",
            ["lft"] = "left",
            ["rgt"] = "right",
            ["pos"] = "position",
            ["hp"] = "health",
            ["fx"] = "effect",
            ["vfx"] = "effect",
            ["sfx"] = "sound",
            ["btn"] = "button",
            ["img"] = "image",
            ["txt"] = "text",
            ["ctrl"] = "controller",
            ["mgr"] = "manager",
        };

        /// <summary>Lower-case letters and digits only, without the usual field prefixes and "(Clone)".</summary>
        public static string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "";
            name = StripPrefix(name.Replace("(Clone)", ""));
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        /// <summary>
        /// The words of a name, lower case: camelCase, PascalCase, digits, spaces, underscores, dots and dashes split
        /// words ("rightHand_Socket2" → right, hand, socket, 2). Short forms are expanded (cam → camera, R → right).
        /// </summary>
        public static List<string> Words(string name)
        {
            var words = new List<string>();
            if (string.IsNullOrEmpty(name))
                return words;
            name = StripPrefix(name.Replace("(Clone)", ""));
            var current = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (!char.IsLetterOrDigit(c))
                {
                    Flush(current, words);
                    continue;
                }
                if (current.Length > 0)
                {
                    char prev = name[i - 1];
                    bool lowerToUpper = char.IsUpper(c) && (char.IsLower(prev) || char.IsDigit(prev));
                    // "HTTPServer": a capital followed by a lower-case letter starts a new word after a run of capitals.
                    bool acronymEnd = char.IsUpper(c) && char.IsUpper(prev) && i + 1 < name.Length && char.IsLower(name[i + 1]);
                    bool digitBoundary = char.IsDigit(c) != char.IsDigit(prev);
                    if (lowerToUpper || acronymEnd || digitBoundary)
                        Flush(current, words);
                }
                current.Append(char.ToLowerInvariant(c));
            }
            Flush(current, words);
            return words;
        }

        /// <summary>
        /// The part of a field name that names the object: without prefixes, without "Ref"/"Reference"/"Component"
        /// and without its type name when something else is left (<c>weaponTransform</c> → <c>weapon</c>,
        /// <c>m_hitColliderRef</c> → <c>hit</c>, but <c>animator</c> stays <c>animator</c>).
        /// </summary>
        public static List<string> FieldWords(string fieldName, string fieldTypeName)
        {
            List<string> words = Words(fieldName);
            bool changed = true;
            while (changed && words.Count > 1)
            {
                changed = false;
                string last = words[words.Count - 1];
                foreach (string suffix in SuffixWords)
                {
                    if (last == suffix)
                    {
                        words.RemoveAt(words.Count - 1);
                        changed = true;
                        break;
                    }
                }
            }
            List<string> typeWords = Words(fieldTypeName);
            if (typeWords.Count == 0 || words.Count < 2)
                return words;
            if (words.Count > typeWords.Count && EndsWith(words, typeWords))
                words.RemoveRange(words.Count - typeWords.Count, typeWords.Count);
            else if (words[words.Count - 1] == typeWords[typeWords.Count - 1] && !EndsWith(typeWords, words))
                words.RemoveAt(words.Count - 1); // "hitBoxCollider" typed Collider → "hit box"; "characterController" stays
            return words;
        }

        /// <summary>
        /// How well <paramref name="objectName"/> (or the candidate's own type) fits a field, from 0 (not at all) to
        /// 100 (same name).
        /// </summary>
        public static int Score(string fieldName, string fieldTypeName, string objectName, string candidateTypeName = null)
        {
            List<string> field = FieldWords(fieldName, fieldTypeName);
            if (field.Count == 0)
                return 0;
            List<string> objectWords = Words(objectName);
            int best = Math.Max(ScoreWords(field, objectWords), ScoreWords(Words(fieldName), objectWords));
            string fullField = Normalize(fieldName);
            if (fullField.Length > 0 && fullField == Normalize(objectName))
                best = 100;
            if (!string.IsNullOrEmpty(candidateTypeName))
            {
                // A "movementController" field typed with a base class: the PlayerMovementController component fits.
                int byType = ScoreWords(field, Words(candidateTypeName)) - 10;
                if (byType > best)
                    best = byType;
            }
            return Clamp(best);
        }

        /// <summary>
        /// True when the field name says it points elsewhere or may stay empty ("targetTransform", "ignoreCollider",
        /// "overrideCamera", "fallbackAnimator"): such fields are only filled by a clear name match, never just because
        /// a single candidate exists.
        /// </summary>
        public static bool NeedsNameMatch(string fieldName)
        {
            foreach (string w in Words(fieldName))
                if (ElsewhereWords.Contains(w))
                    return true;
            return false;
        }

        /// <summary>
        /// Picks the candidate for a field: the only one, or the one whose name clearly fits best.
        /// <paramref name="requireName"/> (Transform and GameObject fields, which almost every object could fill) only
        /// accepts a name match, even with a single candidate.
        /// </summary>
        public static MatchResult Pick(IReadOnlyList<ReferenceCandidate> candidates, string fieldName, string fieldTypeName, bool requireName = false)
        {
            var result = new MatchResult { kind = MatchKind.None, index = -1 };
            if (candidates == null || candidates.Count == 0)
                return result;

            int best = -1, bestScore = -1, second = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                int s = Score(fieldName, fieldTypeName, candidates[i].objectName, requireName ? null : candidates[i].typeName);
                if (s > bestScore)
                {
                    second = bestScore;
                    bestScore = s;
                    best = i;
                }
                else if (s > second)
                {
                    second = s;
                }
            }
            result.score = Math.Max(bestScore, 0);

            bool lead = candidates.Count == 1 || bestScore - second >= MinimumLead;
            if (requireName)
            {
                if (bestScore >= MinimumNameOnlyScore && lead)
                {
                    result.kind = MatchKind.NameMatch;
                    result.index = best;
                }
                else
                {
                    result.kind = bestScore >= MinimumNameOnlyScore ? MatchKind.Ambiguous : MatchKind.NoNameMatch;
                }
                return result;
            }
            bool clearLead = bestScore >= MinimumNameScore && lead;
            if (candidates.Count == 1)
            {
                result.kind = MatchKind.Single;
                result.index = 0;
                return result;
            }
            if (clearLead)
            {
                result.kind = MatchKind.NameMatch;
                result.index = best;
                return result;
            }
            result.kind = MatchKind.Ambiguous;
            return result;
        }

        // ------------------------------------------------------------------ helpers

        private static int ScoreWords(List<string> field, List<string> obj)
        {
            if (field.Count == 0 || obj.Count == 0)
                return 0;
            string f = string.Concat(field);
            string o = string.Concat(obj);
            if (f == o)
                return 100;

            int fieldInObject = 0;
            foreach (string w in field)
                if (obj.Contains(w))
                    fieldInObject++;
            int objectInField = 0;
            foreach (string w in obj)
                if (field.Contains(w))
                    objectInField++;

            int score = 0;
            if (fieldInObject == field.Count && objectInField == obj.Count)
                score = 95; // the same words in another order ("rightHand" ↔ "Hand_R")
            else if (fieldInObject == field.Count)
                score = 90 - 5 * Math.Min(obj.Count - field.Count, 4); // every field word is in the object's name
            else if (objectInField == obj.Count)
                score = 70 - 5 * Math.Min(field.Count - obj.Count, 4); // the object's name is part of the field name
            else if (fieldInObject > 0)
                score = 60 * fieldInObject / Math.Max(field.Count, obj.Count);

            // Glued words ("weaponsocket" in "playerweaponsocket").
            if (f.Length >= 3 && o.Contains(f))
                score = Math.Max(score, 75);
            else if (o.Length >= 3 && f.Contains(o))
                score = Math.Max(score, 55);
            return score;
        }

        private static bool EndsWith(List<string> words, List<string> tail)
        {
            if (tail.Count > words.Count)
                return false;
            for (int i = 0; i < tail.Count; i++)
                if (words[words.Count - tail.Count + i] != tail[i])
                    return false;
            return true;
        }

        private static string StripPrefix(string name)
        {
            foreach (string p in Prefixes)
                if (name.Length > p.Length && name.StartsWith(p, StringComparison.Ordinal))
                    return name.Substring(p.Length);
            return name;
        }

        private static void Flush(StringBuilder current, List<string> words)
        {
            if (current.Length == 0)
                return;
            string w = current.ToString();
            current.Clear();
            words.Add(Aliases.TryGetValue(w, out string full) ? full : w);
        }

        private static int Clamp(int value) => value < 0 ? 0 : value > 100 ? 100 : value;
    }
}
#endif
