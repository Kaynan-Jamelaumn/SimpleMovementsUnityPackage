using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RuneMagic
{
    /// <summary>Where the owner's sequence stands.</summary>
    public enum SpellingState
    {
        /// <summary>Nothing being spelled.</summary>
        None = 0,
        /// <summary>The letters are asked for now.</summary>
        Active = 1,
        /// <summary>The sequence succeeded: the cast may start (see <see cref="SpellingView.readySpellId"/>).</summary>
        Succeeded = 2,
        /// <summary>The sequence failed.</summary>
        Failed = 3,
        /// <summary>The sequence was cut short (stun, silence, death, damage, a new cast, a refusal).</summary>
        Cancelled = 4,
    }

    /// <summary>
    /// What the owner's client needs of its sequence (sent by the server, never anything about other players): the letters and the
    /// windows to show, and the result when it is over. Holds no secret: the sequence is the point of the overlay.
    /// </summary>
    [Serializable]
    public class SpellingView
    {
        /// <summary>Counts the sequences of this soul (the overlay shows a new one when this changes).</summary>
        public int seq;
        public SpellingState state;
        public int slot = -1;
        public string spellId = "";
        public string spellName = "";
        public List<string> letters = new List<string>();
        public List<float> windows = new List<float>();
        public float totalLimit;
        public bool rhythm;
        public float tempo;
        public float tolerance;
        public SpellingFailureModel model;
        public int maxMistakes;
        public bool soundCues;

        // The result (state Succeeded / Failed / Cancelled).
        public int mistakes;
        public float performance;
        public bool speedBonus;
        public string message = "";

        /// <summary>A sequence that succeeded and is waiting for its cast: the spell, and how long it stays good.</summary>
        public string readySpellId = "";
        public float readySecondsLeft;

        public void EnsureLists()
        {
            if (letters == null) letters = new List<string>();
            if (windows == null) windows = new List<float>();
            if (spellId == null) spellId = "";
            if (spellName == null) spellName = "";
            if (message == null) message = "";
            if (readySpellId == null) readySpellId = "";
        }

        public float WindowOf(int i) => windows == null || windows.Count == 0 ? 1f : windows[Math.Max(0, Math.Min(i, windows.Count - 1))];

        public float EffectiveLimit
        {
            get
            {
                if (totalLimit > 0f) return totalLimit;
                float sum = 0f;
                if (windows != null) foreach (float w in windows) sum += w;
                return sum;
            }
        }

        public float BeatOf(int i) => (i + 1) * Math.Max(0.05f, tempo);

        public bool SameAs(SpellingView o) =>
            o != null && seq == o.seq && state == o.state && slot == o.slot && spellId == o.spellId && mistakes == o.mistakes &&
            Math.Abs(performance - o.performance) < 1e-4f && speedBonus == o.speedBonus && message == o.message &&
            readySpellId == o.readySpellId && Math.Abs(readySecondsLeft - o.readySecondsLeft) < 0.5f && letters.Count == o.letters.Count;

        public void CopyFrom(SpellingView o)
        {
            if (o == null) return;
            seq = o.seq; state = o.state; slot = o.slot; spellId = o.spellId ?? ""; spellName = o.spellName ?? "";
            letters = new List<string>(o.letters ?? new List<string>());
            windows = new List<float>(o.windows ?? new List<float>());
            totalLimit = o.totalLimit; rhythm = o.rhythm; tempo = o.tempo; tolerance = o.tolerance; model = o.model; maxMistakes = o.maxMistakes;
            soundCues = o.soundCues; mistakes = o.mistakes; performance = o.performance; speedBonus = o.speedBonus; message = o.message ?? "";
            readySpellId = o.readySpellId ?? ""; readySecondsLeft = o.readySecondsLeft;
        }
    }

    /// <summary>
    /// The key presses of a sequence on the wire: <c>seq|end|key=ms,key=ms,...</c> (times in milliseconds after the first prompt). Short
    /// enough for a request; decoded and bounds-checked by the server, which never trusts a number it is given.
    /// </summary>
    public static class SpellingResultCodec
    {
        /// <summary>Longest text a result request may carry.</summary>
        public const int MaxText = 1024;

        public static string Encode(int seq, float endSeconds, IList<SpellingPress> presses)
        {
            var sb = new StringBuilder();
            sb.Append(seq.ToString(CultureInfo.InvariantCulture)).Append('|').Append(ToMs(endSeconds).ToString(CultureInfo.InvariantCulture)).Append('|');
            for (int i = 0; presses != null && i < presses.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(presses[i].key).Append('=').Append(ToMs(presses[i].t).ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>Reads a result: false (with the reason) for anything malformed, too long, too many presses or not finite.</summary>
        public static bool TryDecode(string text, int maxPresses, out int seq, out float endSeconds, out List<SpellingPress> presses, out string reason)
        {
            seq = 0;
            endSeconds = 0f;
            presses = new List<SpellingPress>();
            reason = "";
            if (string.IsNullOrEmpty(text) || text.Length > MaxText) { reason = "The sequence could not be read."; return false; }
            string[] parts = text.Split('|');
            if (parts.Length != 3) { reason = "The sequence could not be read."; return false; }
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out seq)) { reason = "The sequence could not be read."; return false; }
            if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long endMs) || endMs < 0 || endMs > 600000)
            { reason = "The sequence's time is not valid."; return false; }
            endSeconds = endMs / 1000f;
            if (parts[2].Length == 0) return true;
            string[] items = parts[2].Split(',');
            if (items.Length > maxPresses) { reason = "Too many key presses for this sequence."; return false; }
            foreach (string item in items)
            {
                int eq = item.IndexOf('=');
                if (eq <= 0 || eq >= item.Length - 1 || eq > 24) { reason = "The sequence could not be read."; return false; }
                string key = item.Substring(0, eq);
                foreach (char ch in key)
                    if (!char.IsLetterOrDigit(ch)) { reason = "The sequence could not be read."; return false; }
                if (!long.TryParse(item.Substring(eq + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out long ms) || ms < 0 || ms > 600000)
                { reason = "A key's time is not valid."; return false; }
                presses.Add(new SpellingPress(key, ms / 1000f));
            }
            return true;
        }

        private static long ToMs(float seconds) => float.IsNaN(seconds) || float.IsInfinity(seconds) ? 0 : (long)Math.Round(Math.Max(0f, Math.Min(600f, seconds)) * 1000f);
    }

    /// <summary>
    /// What the server checks before it believes a sequence (design spec 16.5, plan risk R7): the presses are in order, no faster than
    /// a human, and fit in the real time that passed since the server issued the sequence. The score is computed by the server.
    /// </summary>
    public static class SpellingValidator
    {
        /// <param name="serverElapsed">Seconds since the server issued the sequence, when the result arrived.</param>
        public static bool Check(SpellingBalance b, IList<SpellingPress> presses, float endSeconds, float serverElapsed, out string reason)
        {
            reason = "";
            if (!(serverElapsed >= 0f) || float.IsInfinity(serverElapsed)) { reason = "The sequence is not valid."; return false; }
            float last = 0f;
            for (int i = 0; i < presses.Count; i++)
            {
                float t = presses[i].t;
                if (float.IsNaN(t) || t < 0f) { reason = "A key's time is not valid."; return false; }
                if (i > 0 && t < last) { reason = "The keys are not in order."; return false; }
                if (t - last < b.minPressInterval - 1e-4f) { reason = "Those keys were pressed faster than a hand can."; return false; }
                last = t;
            }
            if (endSeconds + 1e-3f < last) { reason = "The sequence ends before its last key."; return false; }
            if (endSeconds > serverElapsed + b.latencyAllowance) { reason = "The sequence took longer than the time that passed."; return false; }
            // And not much shorter: the result comes right after the sequence ends, so what the server waited cannot be far more than
            // what the player's machine says the sequence lasted (times claimed earlier than they happened are no gain, not a lie to forgive).
            if (serverElapsed - endSeconds > b.maxReportLag) { reason = "The sequence was reported too long after it ended."; return false; }
            return true;
        }
    }
}
