using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    // Synthesis (design spec 6.1, 11-13): the runtime side of the synthesis engine. Where the rules run, the engine is given
    // the soul's state and what the soul's condition allows; what it plans is applied here (Soul Damage, a message for the
    // player, the save, the replication). Only rune users (Mages) synthesize. A client previews on its own copy of the runes
    // (the engine is pure, and nothing it needs is secret), but only where the rules run can a synthesis be performed: the
    // request message of a client comes with the synthesis UI (Phase 6). Dismissing a spell already travels as a request.
    public partial class RuneSoul
    {
        /// <summary>A synthesis was performed (where the rules run): what it made or cost.</summary>
        public event Action<RuneSoul, SynthesisResult> SynthesisPerformed;
        /// <summary>A spell was dismissed (where the rules run).</summary>
        public event Action<RuneSoul, SpellRecord> SpellDismissed;

        private const string NotRuneUserToSynthesize = "Only rune users (Mages) can synthesize runes.";

        private SynthesisReport lastReport = new SynthesisReport();
        private int reportSeq;
        private RuneSoulState replica;
        private int replicaVersion = -1;

        /// <summary>The spells made by synthesis (on the owner's client: the server's, as last sent).</summary>
        public IReadOnlyList<SpellRecord> Spells => IsAuthority ? (IReadOnlyList<SpellRecord>)state.spells : replicatedRunes.spells;

        /// <summary>The spell with this id (null when none).</summary>
        public SpellRecord FindSpell(string spellId)
        {
            IReadOnlyList<SpellRecord> list = Spells;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].spellId == spellId) return list[i];
            return null;
        }

        /// <summary>The attempts so far by outcome as a short text (where the rules run; tools and the debug panel).</summary>
        public string SynthesisCounts() => IsAuthority
            ? $"{state.synthesesPerfect} perfect, {state.synthesesSuccessful} successful, {state.synthesesFlawed} flawed, {state.synthesesFailed} failed"
            : "—";

        // ------------------------------------------------------------------ what a synthesis is given
        /// <summary>
        /// What a synthesis may use right now: the soul's capacity, what its condition allows and its integrity. Where the
        /// rules run these are exact; on a client they are predicted from the server's view (the server decides again).
        /// </summary>
        public SynthesisContext BuildSynthesisContext()
        {
            if (IsAuthority)
                return new SynthesisContext { capacity = ComputeCapacity(Balance), soul = Restrictions, integrity = state.soul.integrity, maxIntegrity = ComputeMax() };
            return new SynthesisContext { capacity = view.capacity, soul = Restrictions, integrity = view.integrity, maxIntegrity = Mathf.Max(1f, view.maxIntegrity) };
        }

        /// <summary>
        /// The runes, knowledge and spells a synthesis reads (read-only: never change it): the soul's own state where the rules
        /// run; on the owner's client a copy built from what the server sent (rebuilt only when that changes). The synthesis
        /// window previews with it.
        /// </summary>
        public RuneSoulState SynthesisSnapshot()
        {
            if (IsAuthority) return state;
            if (replica == null || replicaVersion != replicatedRunes.version)
            {
                replica = SynthesisReplica.StateFrom(replicatedRunes);
                replicaVersion = replicatedRunes.version;
            }
            return replica;
        }

        /// <summary>What the latest performed synthesis did (<c>seq</c> 0 = none yet this session). On the owner's client: as the server sent it.</summary>
        public SynthesisReport LastReport => IsAuthority ? lastReport : replicatedRunes.report ?? lastReport;

        /// <summary>
        /// Can a synthesis be made now? Rune users only; not in combat (unless the balance allows it), not downed, asleep,
        /// meditating or in a ritual. The soul's condition has its own say when the formula is checked. On a client: a quick check.
        /// </summary>
        public bool CanSynthesize(out string reason)
        {
            reason = "";
            if (!IsAuthority)
            {
                if (!view.runeUser) { reason = NotRuneUserToSynthesize; return false; }
                if (!view.synthesisAllowed) { reason = "Your soul cannot synthesize now."; return false; }
                if (view.IsChannelling) { reason = "You are focused on your soul."; return false; }
                return true;
            }
            if (!CheckRuneUser(true)) { reason = NotRuneUserToSynthesize; return false; }
            if (!CanChannel(out reason)) return false;
            if (!Catalog.Synthesis.allowInCombat)
            {
                CombatEntity e = Entity;
                SoulRecoveryBalance rb = Balance.soulRecovery;
                if (e != null && CombatActivity.InCombat(e, rb != null ? rb.combatWindowSeconds : 6f))
                {
                    reason = "You cannot synthesize in combat.";
                    return false;
                }
            }
            return true;
        }

        // ------------------------------------------------------------------ preview and exact evaluation
        /// <summary>
        /// What the player may know before confirming (design spec 11.1 step 7): requirements, a fuzzy estimate, risk. Works
        /// on every machine; never reveals the exact score or the ideal. Changes nothing.
        /// </summary>
        public SynthesisPreview PreviewSynthesis(SynthesisRequest request)
        {
            if (!IsRuneUser)
                return new SynthesisPreview { refusal = SynthesisRefusal.NotRuneUser, message = NotRuneUserToSynthesize };
            return SynthesisEngine.Preview(Catalog, SynthesisSnapshot(), BuildSynthesisContext(), request);
        }

        /// <summary>
        /// The exact evaluation of a request (the score, every factor, the ideal energies): for developer tools and tests, where
        /// the rules run. It reveals the optimum: the player's UI uses <see cref="PreviewSynthesis"/>. Null with the reason when refused.
        /// </summary>
        public SynthesisEvaluation ExplainSynthesis(SynthesisRequest request, out string reason)
        {
            reason = "";
            if (!IsAuthority || !CheckRuneUser(true)) { reason = IsAuthority ? NotRuneUserToSynthesize : "Only where the rules run."; return null; }
            return SynthesisEngine.Evaluate(Catalog, state, BuildSynthesisContext(), request, out _, out reason);
        }

        // ------------------------------------------------------------------ performing a synthesis (where the rules run)
        /// <summary>
        /// Performs a synthesis (design spec 11-13). The chance in it is the server's: the seed of <paramref name="request"/> is
        /// replaced. A refused request costs nothing (the reason is told to the player and returned in the result); a performed
        /// one changes the runes and spells, strains the soul as planned, saves and tells the player what happened.
        /// </summary>
        public SynthesisResult Synthesize(SynthesisRequest request)
        {
            var refused = new SynthesisResult();
            if (!IsAuthority)
            {
                refused.refusal = SynthesisRefusal.NotNow;
                refused.message = "Only where the rules run can a synthesis be performed.";
                return refused;
            }
            if (!CanSynthesize(out string why))
            {
                refused.refusal = CheckRuneUser(true) ? SynthesisRefusal.NotNow : SynthesisRefusal.NotRuneUser;
                refused.message = why;
                Say(why);
                return refused;
            }

            SynthesisRequest own = request != null ? request.Clone() : null;
            if (own != null) own.seed = NewSynthesisSeed();
            RuneCatalog c = Catalog;
            SynthesisResult result = SynthesisEngine.Execute(c, state, BuildSynthesisContext(), own, DateTime.UtcNow.Ticks);
            if (!result.performed)
            {
                Say(result.message);
                if (logEvents) Debug.Log($"[Rune Magic] {name}: synthesis refused ({result.refusal}): {result.message}", this);
                return result;
            }

            // The runes, the spells and the log changed.
            runesVersion++;
            MarkSaveDirty(true);

            // What the synthesis cost the soul, as the engine planned it; a collapse ends it.
            bool collapsed = false;
            float taken = 0f, deep = 0f;
            foreach (SoulDamageEntry d in result.soulDamage)
            {
                SoulDamageResult r = Damage(d.amount, d.kind, string.IsNullOrEmpty(d.source) ? "synthesis" : d.source);
                taken += r.applied;
                deep += r.deepAdded;
                if (r.collapsedNow) { collapsed = true; break; }
            }
            // The result screen: sent to the owner with the rune list, so the result and the changed runes arrive together.
            lastReport = SynthesisReport.From(c, result, own.mode, own.slots.Count, ++reportSeq);
            lastReport.soulDamage = taken;
            lastReport.deepDamage = deep;
            lastReport.collapsed = collapsed;
            runesVersion++;
            Say(collapsed ? result.message + " " + message : result.message);
            if (logEvents)
                Debug.Log($"[Rune Magic] {name}: synthesis {result.tier} {result.score:0.#}% ({own?.mode}, {own?.slots.Count} runes) → " +
                          $"{(result.output == SynthesisOutput.None ? "nothing" : result.name)}{(result.faltered ? ", faltered" : "")}" +
                          $"{(result.runesDestroyed ? ", destroyed " + string.Join("/", result.destroyedRunes) : "")}.", this);
            SynthesisPerformed?.Invoke(this, result);
            Refresh();
            return result;
        }

        /// <summary>Dismisses a spell: the runes it anchored are free again (they keep their energy). Where the rules run.</summary>
        public bool DismissSpell(string spellId, out string reason)
        {
            reason = "";
            if (!IsAuthority) { reason = "Only the server dismisses spells."; return false; }
            if (!CheckRuneUser(true)) { reason = NotRuneUserToSynthesize; return false; }
            SpellRecord spell = SpellRules.Dismiss(state, spellId);
            if (spell == null)
            {
                reason = "That spell is no longer in your spellbook.";
                Say(reason);
                return false;
            }
            runesVersion++;
            MarkSaveDirty(true);
            Say($"You dismiss {spell.name}: its runes are free again.");
            SpellDismissed?.Invoke(this, spell);
            Refresh();
            return true;
        }

        /// <summary>
        /// Asks for a synthesis (any machine; the synthesis window's confirm). Where the rules run it is performed now; a client
        /// sends the request to the server, which decodes and checks it again and chooses the chance. Either way the answer
        /// is a new <see cref="LastReport"/> (performed) or a message (<see cref="RuneSoulView.message"/>, refused). Returns
        /// whether the request was performed or sent.
        /// </summary>
        public bool RequestSynthesis(SynthesisRequest request)
        {
            if (request == null) return false;
            if (!IsAuthority) return Send(RuneRequestKind.Synthesize, SynthesisRequestCodec.Encode(request), 0f, false);
            return Synthesize(request).performed;
        }

        private void HandleSynthesisRequest(RuneSoulRequest request)
        {
            if (!SynthesisRequestCodec.TryDecode(request.text, out SynthesisRequest decoded, out string why))
            {
                Say("That synthesis could not be read: " + why);
                return;
            }
            Synthesize(decoded);
        }

        /// <summary>Dismiss the spell <paramref name="spellId"/> (any machine: a client asks the server).</summary>
        public bool RequestDismissSpell(string spellId)
        {
            if (string.IsNullOrEmpty(spellId)) return false;
            if (!IsAuthority) return Send(RuneRequestKind.DismissSpell, spellId, 0f, false);
            return DismissSpell(spellId, out _);
        }

        private static int NewSynthesisSeed()
        {
            unchecked { return (int)(DateTime.UtcNow.Ticks ^ ((long)Guid.NewGuid().GetHashCode() << 17) ^ Environment.TickCount); }
        }
    }
}
