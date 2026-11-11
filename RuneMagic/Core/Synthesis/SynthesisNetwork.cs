using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// A synthesis request on the wire (design spec 11.1, step 8): the owner's client sends the formula as JSON; the server
    /// decodes it with everything checked (sizes, finite numbers, known mode, sane ids) and then runs the ordinary
    /// resolver and engine on it, which check it against the soul again. The seed never travels: the server chooses the
    /// chance. Pure: no Unity objects but <see cref="JsonUtility"/>.
    /// </summary>
    public static class SynthesisRequestCodec
    {
        private const int MaxId = 64;
        private const int MaxName = 64;
        private const float MaxEnergy = 1e9f;

        /// <summary>The request as JSON (without its seed).</summary>
        public static string Encode(SynthesisRequest request)
        {
            SynthesisRequest r = request != null ? request.Clone() : new SynthesisRequest();
            r.seed = 0;
            return JsonUtility.ToJson(r);
        }

        /// <summary>
        /// Reads a request from <paramref name="json"/> (what a client sent: nothing in it is trusted). False with the reason
        /// when it is too long, unreadable, or holds anything out of bounds. The result has no seed and no null parts.
        /// </summary>
        public static bool TryDecode(string json, out SynthesisRequest request, out string why)
        {
            request = null;
            why = "";
            if (string.IsNullOrEmpty(json)) { why = "The request is empty."; return false; }
            if (json.Length > RuneNetLimits.MaxSynthesisRequestText) { why = "The request is too long."; return false; }
            SynthesisRequest r;
            try { r = JsonUtility.FromJson<SynthesisRequest>(json); }
            catch (ArgumentException) { why = "The request cannot be read."; return false; }
            if (r == null || r.slots == null) { why = "The request cannot be read."; return false; }
            if (!Enum.IsDefined(typeof(SynthesisMode), r.mode)) { why = "Unknown synthesis mode."; return false; }
            if (r.slots.Count == 0 || r.slots.Count > RuneNetLimits.MaxSynthesisSlots) { why = "Too many or too few runes."; return false; }
            r.intentionId = r.intentionId ?? "";
            r.effectId = r.effectId ?? "";
            r.name = r.name ?? "";
            if (!Sane(r.intentionId, MaxId, true) || !Sane(r.effectId, MaxId, true) || !Sane(r.name, MaxName, true)) { why = "The request holds invalid text."; return false; }
            foreach (SynthesisSlot s in r.slots)
            {
                if (s == null) { why = "The request holds an empty place."; return false; }
                s.runeInstanceId = s.runeInstanceId ?? "";
                s.conceptId = s.conceptId ?? "";
                if (!Sane(s.runeInstanceId, MaxId, false) || !Sane(s.conceptId, MaxId, true)) { why = "The request holds invalid text."; return false; }
                if (float.IsNaN(s.energy) || float.IsInfinity(s.energy) || s.energy < 0f || s.energy > MaxEnergy) { why = "The request holds an invalid energy."; return false; }
            }
            r.seed = 0;
            request = r;
            return true;
        }

        private static bool Sane(string text, int max, bool mayBeEmpty)
        {
            if (text.Length == 0) return mayBeEmpty;
            if (text.Length > max) return false;
            foreach (char ch in text)
                if (char.IsControl(ch)) return false;
            return true;
        }
    }

    /// <summary>
    /// What a performed synthesis did, for the owner's result screen (design spec 11.1, step 9): sent to the owner with the
    /// rune list, so the result and the changed runes arrive together. Plain data, JSON-serialisable; the details
    /// (flaws, destroyed runes) are names, not ids.
    /// </summary>
    [Serializable]
    public class SynthesisReport
    {
        [Tooltip("Counts the performed syntheses of this session (0 = none yet): a new number is a new result.")]
        public int seq;
        public bool performed;
        public int mode;
        public int runeCount;
        [Tooltip("0 complete failure, 1 flawed, 2 successful, 3 perfect (the SynthesisTier).")]
        public int tier;
        [Tooltip("The Synthesis Quality Score (0-100), after the roll for faltering.")]
        public float score;
        public bool faltered;
        public bool incoherent;
        [Tooltip("0 nothing, 1 a spell, 2 a rune (the SynthesisOutput).")]
        public int output;
        public string name = "";
        public int rank;
        public string severity = "";
        public List<string> flaws = new List<string>();
        public List<string> sideEffects = new List<string>();
        public float energyConsumed;
        public float energyLost;
        public List<string> destroyedRunes = new List<string>();
        public List<string> lostSpells = new List<string>();
        [Tooltip("Soul Integrity the synthesis cost and the deep damage it left, and whether the soul collapsed (set where the damage is applied).")]
        public float soulDamage;
        public float deepDamage;
        public bool collapsed;
        public float power;
        public float stability;
        public float energyCost;
        public float quality;
        public float controlPenalty;
        [Tooltip("A hint for the player's research (never numbers).")]
        public string hint = "";
        public string message = "";

        public bool HasResult => seq > 0 && performed;
        public SynthesisTier Tier => (SynthesisTier)tier;
        public SynthesisOutput Output => (SynthesisOutput)output;
        public SynthesisMode Mode => (SynthesisMode)mode;
        public bool Failed => performed && Tier == SynthesisTier.CompleteFailure;

        public void EnsureLists()
        {
            if (flaws == null) flaws = new List<string>();
            if (sideEffects == null) sideEffects = new List<string>();
            if (destroyedRunes == null) destroyedRunes = new List<string>();
            if (lostSpells == null) lostSpells = new List<string>();
        }

        public SynthesisReport Clone()
        {
            var r = (SynthesisReport)MemberwiseClone();
            r.flaws = new List<string>(flaws ?? new List<string>());
            r.sideEffects = new List<string>(sideEffects ?? new List<string>());
            r.destroyedRunes = new List<string>(destroyedRunes ?? new List<string>());
            r.lostSpells = new List<string>(lostSpells ?? new List<string>());
            return r;
        }

        /// <summary>The report of a performed synthesis (a refused one has none: the player is told why with a message).</summary>
        public static SynthesisReport From(RuneCatalog c, SynthesisResult result, SynthesisMode mode, int runeCount, int seq)
        {
            var r = new SynthesisReport { seq = seq };
            if (result == null || !result.performed) return r;
            r.performed = true;
            r.mode = (int)mode;
            r.runeCount = runeCount;
            r.tier = (int)result.tier;
            r.score = result.score;
            r.faltered = result.faltered;
            r.incoherent = result.incoherent;
            r.output = (int)result.output;
            r.name = result.name ?? "";
            r.rank = result.output == SynthesisOutput.Rune && result.rune != null ? result.rune.rank
                : result.output == SynthesisOutput.Spell && result.spell != null ? result.spell.rank : 0;
            r.severity = result.severity ?? "";
            foreach (string id in result.flaws)
            {
                SynthesisFlawDef f = c != null ? c.Flaw(id) : null;
                r.flaws.Add(f != null ? f.Name : id);
            }
            if (result.spell != null) r.sideEffects.AddRange(result.spell.sideEffects);
            r.energyConsumed = result.energyConsumed;
            r.energyLost = result.energyLost;
            r.destroyedRunes.AddRange(result.destroyedRunes);
            r.lostSpells.AddRange(result.lostSpells);
            r.power = result.stats.power;
            r.stability = result.stats.stability;
            r.energyCost = result.stats.energyCostMultiplier;
            r.quality = result.stats.quality;
            r.controlPenalty = result.stats.controlPenalty;
            r.hint = SynthesisEngine.Hint(result.evaluation);
            r.message = result.message ?? "";
            return r;
        }
    }

    /// <summary>
    /// The runes, knowledge and spells of a soul as the owner's client holds them (what the server sent), as the state the
    /// engine reads: so a client previews a synthesis with exactly the rules and the data the server will use. The server's
    /// own state is never sent whole (decision D18: owner only, and only what the UI needs).
    /// </summary>
    public static class SynthesisReplica
    {
        public static RuneSoulState StateFrom(RuneListView view)
        {
            var s = new RuneSoulState();
            if (view == null) return s;
            if (view.runes != null)
                foreach (RuneInstance r in view.runes)
                    if (r != null) s.runes.Add(r.Clone());
            if (view.knowledge != null)
                foreach (ConceptKnowledge k in view.knowledge)
                    if (k != null) s.knowledge.Add(new ConceptKnowledge { runeId = k.runeId, concepts = new List<string>(k.concepts ?? new List<string>()) });
            if (view.spells != null)
                foreach (SpellRecord sp in view.spells)
                    if (sp != null) s.spells.Add(sp.Clone());
            return s;
        }
    }
}
