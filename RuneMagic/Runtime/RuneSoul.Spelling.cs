using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RuneMagic
{
    // The Spelling System (design spec 16.5, directive 9): a key sequence some spells ask for before they are cast. The developer / server
    // decides which spells (the balance's global mode and the rules of the database), never the player. The flow is a PRE-CAST
    // challenge, so the ability's own phases are untouched:
    //   1. The player presses the spell's ability key; the player's ability controller asks this soul first (PreCast hook).
    //   2. The soul asks the server for a sequence (a client sends the keys it can use; the server cleans them up, generates the
    //      letters and the windows from the spell and the soul, and sends them back to the owner only).
    //   3. The overlay shows the letters; the player's machine records the key presses (relative to the first prompt).
    //   4. The server validates them (order, human speed, real time), replays them, scores them and decides.
    //   5. A success is good for a few seconds: the owner's machine then casts normally and the server applies what the performance
    //      earned (power and cast time in the slot's modifiers, cost, strain and stability on the cast). A failure costs what the spell's
    //      rules say (energy, Soul Damage), and the cast never starts.
    // A spelled spell cannot be cast without a good sequence: the server's cast check refuses it (HasResources), whatever the client does.
    public partial class RuneSoul
    {
        private sealed class SpellingChallenge
        {
            public int seq;
            public int slot;
            public string spellId;
            public CompiledSpell spell;
            public SpellingParams p;
            public List<string> letters;
            public float issuedAt;
            public float deadline;
        }

        private sealed class ReadySpelling
        {
            public string spellId;
            public SpellingOutcome outcome;
            public float expiresAt;
        }

        private SpellingChallenge challenge;
        private ReadySpelling ready;
        private readonly SpellingView spellingView = new SpellingView();
        private int spellingVersion;
        private int spellingSeq;
        private readonly Dictionary<string, float> spellingLocks = new Dictionary<string, float>();
        private readonly List<string> spellingPoolKeys = new List<string>();
        private float clientReadyUntil;
        private int autoCastSeq = -1;
        private bool warnedPool;
        // The owner's machine asked for a sequence and the server's answer has not come back yet: more presses of the key do nothing.
        private float spellingPendingUntil;

        /// <summary>A key sequence ended (where the rules run): it succeeded, failed or was interrupted.</summary>
        public event Action<RuneSoul, SpellingView> SpellingEnded;

        /// <summary>The owner's sequence: what to show and how it ended (on the owner's client: the server's, as last sent).</summary>
        public SpellingView SpellingView => spellingView;

        /// <summary>Changes whenever <see cref="SpellingView"/> does (the network layer sends it then).</summary>
        public int SpellingVersion => spellingVersion;

        /// <summary>The keys the owner's machine accepts for the sequence being asked for (the keys it could use when it asked).</summary>
        public IReadOnlyList<string> SpellingPoolKeys => spellingPoolKeys;

        /// <summary>Is a sequence being asked for now?</summary>
        public bool IsSpelling => spellingView.state == SpellingState.Active;

        // ------------------------------------------------------------------ which spells, and are they ready
        /// <summary>Does casting this spell need a key sequence (the global mode and the rules of the database)? Any machine.</summary>
        public bool SpellingRequired(string spellId)
        {
            CompiledSpell cs = CompiledSpellOf(spellId);
            return cs != null && cs.usesSpelling;
        }

        /// <summary>
        /// Did the owner just spell this spell (the sequence succeeded and is still good)? Where the rules run this is exact; on the
        /// owner's client it follows what the server said.
        /// </summary>
        public bool HasReadySpelling(string spellId)
        {
            if (string.IsNullOrEmpty(spellId)) return false;
            if (IsAuthority) return ready != null && ready.spellId == spellId && Time.unscaledTime < ready.expiresAt;
            return spellingView.readySpellId == spellId && Time.unscaledTime < clientReadyUntil;
        }

        // ------------------------------------------------------------------ the owner's input (any machine)
        // The player's ability controller asks before it does anything with a key press (PlayerAbilityController.PreCast).
        private bool OnPreCast(int slot)
        {
            if (!IsRuneUser) return false;
            PlayerAbilityController pc = abilities;
            AbilitySlot s = pc != null ? pc.GetSlot(slot) : null;
            if (s == null || !(s.Castable is RuneSpellCastable rc) || !SpellingRequired(rc.SpellId)) return false;
            if (CombatRules.IsVisualOnly(Entity)) return false; // only shown where combat is off (the city): free, nothing to spell
            if (IsSpelling || Time.unscaledTime < spellingPendingUntil) return true; // a sequence is running (or being issued): key presses do nothing else meanwhile
            if (HasReadySpelling(rc.SpellId)) return false; // spelled: the cast goes on
            // Everything a cast needs but the sequence is checked first, so a spell that cannot be cast anyway never asks for letters.
            CastFailReason why = pc.CheckSlotReady(slot);
            if (why != CastFailReason.None)
            {
                pc.RaiseCastFailed(slot, why);
                return true;
            }
            BeginSpelling(slot);
            return true;
        }

        /// <summary>
        /// Starts the key sequence of the spell on an ability key (any machine: a client sends the keys its bindings allow, the server
        /// decides everything else). A machine without a keyboard asks to skip it. Returns whether it started or was sent.
        /// </summary>
        public bool BeginSpelling(int slot)
        {
            PlayerAbilityController pc = Abilities;
            AbilitySlot s = pc != null ? pc.GetSlot(slot) : null;
            if (s == null || s.ability == null || !(s.Castable is RuneSpellCastable)) return false;
            SpellingBalance b = Catalog.Spelling;
            bool rooted = s.ability.movementWhileCasting == CasterMovementRule.Stop;
            var keys = new List<string>();
            if (SpellingKeyProvider.HasKeyboard)
                keys.AddRange(SpellingKeyProvider.BuildPool(b, rooted, AbilityKeyActions(pc)).keys);
            spellingPoolKeys.Clear();
            spellingPoolKeys.AddRange(keys);
            if (IsAuthority) return StartSpelling(slot, keys, out _);
            spellingPendingUntil = Time.unscaledTime + 1f;
            return Send(RuneRequestKind.SpellingStart, SpellingKeyPool.ToText(keys), slot, false);
        }

        private static IEnumerable<InputAction> AbilityKeyActions(PlayerAbilityController pc)
        {
            for (int i = 0; i < pc.SlotCount; i++)
            {
                AbilityStateMachine m = pc.GetStateMachine(i);
                InputAction a = m != null ? m.InputAction : null;
                if (a != null) yield return a;
            }
        }

        /// <summary>
        /// The key presses of the sequence that was asked for (any machine: a client sends them to the server). <paramref name="end"/>
        /// is when the player's machine ended the sequence, in seconds after the first prompt.
        /// </summary>
        public bool SubmitSpelling(int seq, float end, IList<SpellingPress> presses)
        {
            if (!IsAuthority) return Send(RuneRequestKind.SpellingResult, SpellingResultCodec.Encode(seq, end, presses), 0f, false);
            return FinishSpelling(seq, end, presses ?? new List<SpellingPress>(), false);
        }

        // ------------------------------------------------------------------ the server's side
        private bool StartSpelling(int slot, IReadOnlyList<string> reported, out string reason)
        {
            reason = "";
            if (!IsAuthority) { reason = "Only the server starts a key sequence."; return false; }
            if (!CheckRuneUser(true)) { reason = NotRuneUserToCast; return false; }
            PlayerAbilityController pc = Abilities;
            AbilitySlot s = pc != null ? pc.GetSlot(slot) : null;
            RuneSpellCastable rc = s != null ? s.Castable as RuneSpellCastable : null;
            SpellRecord sp = rc != null ? SpellRules.Find(state, rc.SpellId) : null;
            if (sp == null) { reason = "That spell is no longer in your spellbook."; Say(reason); return false; }
            CompiledSpell cs = CompileSpell(sp);
            if (!cs.usesSpelling) { reason = "That spell is cast without spelling."; return false; }
            SpellingBalance b = Catalog.Spelling;
            // A repeat of the same request a moment after the sequence was issued (a key pressed twice before the answer arrived, a key
            // that repeats while held) is the same request.
            if (challenge != null && challenge.slot == slot && Time.unscaledTime - challenge.issuedAt < 1f) return true;
            // The owner's input swallows key presses while a sequence runs, so a new start while one is still open is a sequence abandoned:
            // it counts as never answered (it fails), or stopping after a mistake and starting again would cost nothing.
            if (challenge != null) FinishSpelling(challenge.seq, 0f, new List<SpellingPress>(), true);
            if (spellingLocks.TryGetValue(sp.spellId, out float until) && Time.unscaledTime < until)
            {
                reason = $"{sp.name} is still recovering from the failed spelling.";
                Say(reason);
                return false;
            }
            // Everything a cast needs but the sequence itself (energy, the soul's condition, cooldown, silence, another cast in progress).
            CastFailReason why = pc.CheckSlotReady(slot);
            if (why != CastFailReason.None)
            {
                reason = !CanCastSpell(sp.spellId, out string spellWhy) ? spellWhy : $"{sp.name} cannot be cast now ({why}).";
                Say(reason);
                return false;
            }

            List<string> pool = SpellingKeyPool.Sanitize(b, reported);
            if (pool.Count < Math.Max(2, b.minPoolSize))
            {
                if (!b.skipWithoutKeyboard)
                {
                    reason = "This spell needs a keyboard.";
                    Say(reason);
                    return false;
                }
                if (!warnedPool)
                {
                    warnedPool = true;
                    Debug.LogWarning($"[Rune Magic] {name}: fewer than {Math.Max(2, b.minPoolSize)} keys can be asked for ({pool.Count}): the Spelling System is skipped and " +
                                     "spelled spells are cast like normal abilities (no keyboard, or nearly every letter is bound to an action).", this);
                }
                // The server cannot know a keyboard is missing, so a machine that says so gets the worst a successful sequence can earn.
                SpellingParams skipped = SpellingGenerator.Resolve(Catalog, cs, sp, SpellingContextOf(sp), NewSynthesisSeed(), 0);
                GrantSpelling(sp, slot, SpellingEvaluator.Skipped(skipped), "You cast it without spelling: weaker and slower.", true);
                return true;
            }

            SpellingContext ctx = SpellingContextOf(sp);
            int seed = NewSynthesisSeed();
            // The keys a client reports never shorten the sequence (it could report just a few to get fewer letters): keys are reused instead.
            SpellingParams p = SpellingGenerator.Resolve(Catalog, cs, sp, ctx, seed, 0);
            float now = Time.unscaledTime;
            challenge = new SpellingChallenge
            {
                seq = ++spellingSeq, slot = slot, spellId = sp.spellId, spell = cs, p = p,
                letters = SpellingSequence.Generate(seed, pool, p.letters),
                issuedAt = now, deadline = now + p.EffectiveLimit + b.answerGraceSeconds + b.latencyAllowance,
            };
            ready = null;
            spellSyncDirty = true;
            PublishSpelling(SpellingState.Active, null, "");
            if (logEvents)
                Debug.Log($"[Rune Magic] {name}: spelling {sp.name}: {string.Join(" ", challenge.letters)} ({p.model}, {p.EffectiveLimit:0.0}s, up to {p.maxMistakes} mistakes).", this);
            return true;
        }

        // What the sequence's difficulty depends on besides the spell: how much of its runes' concepts the player knows, its quality, the soul.
        private SpellingContext SpellingContextOf(SpellRecord sp)
        {
            RuneSoulState st = SynthesisSnapshot();
            float known = 0f;
            int count = 0;
            foreach (string anchor in sp.anchors)
            {
                RuneInstance r = st.Rune(anchor);
                if (r == null || r.IsComposite) continue;
                RuneDef def = Catalog.Rune(r.runeId);
                if (def == null || def.concepts == null || def.concepts.Count == 0) continue;
                known += Mathf.Clamp01(KnownConcepts(r.runeId).Count / (float)def.concepts.Count);
                count++;
            }
            return new SpellingContext { knowledge = count > 0 ? known / count : 0f, quality = sp.quality, soul = Restrictions };
        }

        private void HandleSpellingStart(RuneSoulRequest request)
        {
            int slot = float.IsNaN(request.amount) ? -1 : Mathf.RoundToInt(Mathf.Clamp(request.amount, -1f, 1000f));
            if (slot < 0 || slot > 255) return;
            StartSpelling(slot, SpellingKeyPool.FromText(request.text), out _);
        }

        private void HandleSpellingResult(RuneSoulRequest request)
        {
            if (challenge == null) return; // nothing was asked for (late, or invented)
            int max = Catalog.Spelling.maxPressesPerLetter * challenge.letters.Count + 2;
            if (!SpellingResultCodec.TryDecode(request.text, max, out int seq, out float end, out List<SpellingPress> presses, out string why))
            {
                // A result that cannot be read is no result: the sequence was never answered, so it fails (a client that knows it failed
                // must not escape the consequences by sending garbage).
                if (logEvents) Debug.Log($"[Rune Magic] {name}: a key sequence could not be read ({why}).", this);
                FinishSpelling(challenge.seq, 0f, new List<SpellingPress>(), true);
                return;
            }
            FinishSpelling(seq, end, presses, false);
        }

        private bool FinishSpelling(int seq, float end, IList<SpellingPress> presses, bool timedOut)
        {
            SpellingChallenge c = challenge;
            if (c == null || seq != c.seq) return false;
            SpellingBalance b = Catalog.Spelling;
            if (!timedOut && !SpellingValidator.Check(b, presses, end, Time.unscaledTime - c.issuedAt, out string why))
            {
                // Not believed: it counts as never answered (it fails like any other unanswered sequence).
                if (logEvents) Debug.Log($"[Rune Magic] {name}: a key sequence was not accepted ({why}).", this);
                presses = new List<SpellingPress>();
            }
            SpellingOutcome o = SpellingEvaluator.Evaluate(c.p, c.letters, presses);
            challenge = null;
            if (o.success)
            {
                SpellRecord sp = SpellRules.Find(state, c.spellId);
                if (sp == null) { PublishSpelling(SpellingState.Cancelled, null, "That spell is no longer in your spellbook."); return false; }
                if (o.mistakeDamage > 0f) Damage(o.mistakeDamage, o.damageKind, "mistakes while spelling " + sp.name);
                GrantSpelling(sp, c.slot, o, o.Summary());
            }
            else FailSpelling(c, o);
            return true;
        }

        private void GrantSpelling(SpellRecord sp, int slot, SpellingOutcome o, string message, bool skipped = false)
        {
            ready = new ReadySpelling { spellId = sp.spellId, outcome = o, expiresAt = Time.unscaledTime + Catalog.Spelling.readySeconds };
            // The slot takes the power and the cast time the sequence earned now, before the owner's machine casts.
            spellSyncDirty = true;
            ReconcileSpellSlots();
            PublishSpelling(SpellingState.Succeeded, o, sp.name + ": " + message, skipped ? slot : -1, sp.spellId);
            Say(sp.name + ": " + message);
            SpellingEnded?.Invoke(this, spellingView);
            Refresh();
        }

        // A failed sequence (design spec 16.5): what it costs is per spell data - energy, Soul Damage for the mistakes and for the failed cast.
        private void FailSpelling(SpellingChallenge c, SpellingOutcome o)
        {
            SpellRecord sp = SpellRules.Find(state, c.spellId);
            string spell = sp != null ? sp.name : c.spellId;
            SpellingBalance b = Catalog.Spelling;
            float lost = 0f;
            if (sp != null)
            {
                CastPlan plan = SpellCasting.Plan(Catalog, c.spell, Restrictions);
                float want = plan.energyCost * Mathf.Clamp01(c.p.energyLossOnFailure);
                float have = SpellRules.AnchoredEnergy(state, sp);
                CastDraw draw = want > 0f ? SpellCasting.Draw(state, sp, Mathf.Min(want, have)) : null;
                lost = draw != null ? draw.Total : 0f;
                if (lost > 0f) { energyVersion++; MarkSaveDirty(false); }
                CastStrain failure = SpellCasting.Failure(Balance, plan);
                float failureDamage = failure.amount * Mathf.Max(0f, c.p.failureDamageFactor);
                if (o.mistakeDamage > 0f) Damage(o.mistakeDamage, o.damageKind, "mistakes while spelling " + spell);
                if (failureDamage > 0f) Damage(failureDamage, failure.kind, "failed spelling of " + spell);
            }
            if (c.p.failureLockSeconds > 0f) spellingLocks[c.spellId] = Time.unscaledTime + c.p.failureLockSeconds;
            string text = $"{spell} fails: {o.Summary()}" + (lost > 0.5f ? $" {lost:0} Rune Energy is lost." : "");
            PublishSpelling(SpellingState.Failed, o, text);
            Say(text);
            if (logEvents) Debug.Log($"[Rune Magic] {name}: spelling {spell} failed ({o.failure}, {o.Mistakes} mistakes, energy lost {lost:0.#}).", this);
            SpellingEnded?.Invoke(this, spellingView);
            Refresh();
        }

        /// <summary>
        /// Cuts the sequence short (stun, silence, death, damage that interrupts, a new cast, a sequence that could not be accepted). No
        /// energy had been drawn; the Soul Damage of a failed cast follows the Spell Balance's Damage On Interrupt (never automatic).
        /// </summary>
        private void InterruptSpelling(string message, bool quiet)
        {
            SpellingChallenge c = challenge;
            if (c == null) return;
            challenge = null;
            if (!quiet && Catalog.Spells.damageOnInterrupt)
            {
                CastStrain failure = SpellCasting.Failure(Balance, SpellCasting.Plan(Catalog, c.spell, Restrictions));
                if (failure.Any) Damage(failure.amount, failure.kind, "interrupted spelling");
            }
            PublishSpelling(SpellingState.Cancelled, null, message);
            if (!quiet) Say(message);
            SpellingEnded?.Invoke(this, spellingView);
            Refresh();
        }

        /// <summary>Stops a sequence being asked for (developer tools, tests). Where the rules run.</summary>
        public void CancelSpelling()
        {
            if (IsAuthority) InterruptSpelling("The sequence was cancelled.", true);
        }

        // Updates what the owner is shown (where the rules run) and tells the network layer. A sequence that was skipped (no keys to ask
        // for) has no challenge: it is a new sequence of its own (<paramref name="skippedSlot"/> names its key).
        private void PublishSpelling(SpellingState newState, SpellingOutcome outcome, string message, int skippedSlot = -1, string skippedSpellId = "")
        {
            SpellingChallenge c = challenge;
            SpellingView v = spellingView;
            v.state = newState;
            v.message = message ?? "";
            if (c != null)
            {
                v.seq = c.seq;
                v.slot = c.slot;
                v.spellId = c.spellId;
                v.spellName = SpellRules.Find(state, c.spellId)?.name ?? "";
                v.letters = new List<string>(c.letters);
                v.windows = new List<float>(c.p.windows);
                v.totalLimit = c.p.totalLimit;
                v.rhythm = c.p.rhythm;
                v.tempo = c.p.tempo;
                v.tolerance = c.p.tolerance;
                v.model = c.p.model;
                v.maxMistakes = c.p.maxMistakes;
                v.soundCues = Catalog.Spelling.soundCues;
            }
            else if (skippedSlot >= 0)
            {
                v.seq = ++spellingSeq;
                v.slot = skippedSlot;
                v.spellId = skippedSpellId ?? "";
                v.spellName = SpellRules.Find(state, v.spellId)?.name ?? "";
                v.letters = new List<string>();
                v.windows = new List<float>();
            }
            v.mistakes = outcome != null ? outcome.Mistakes : 0;
            v.performance = outcome != null ? outcome.performance : 0f;
            v.speedBonus = outcome != null && outcome.speedBonus;
            v.readySpellId = ready != null ? ready.spellId : "";
            v.readySecondsLeft = ready != null ? Mathf.Max(0f, ready.expiresAt - Time.unscaledTime) : 0f;
            spellingVersion++;
        }

        // ------------------------------------------------------------------ per frame
        private void TickSpelling()
        {
            if (IsAuthority)
            {
                if (challenge != null)
                {
                    CombatEntity e = Entity;
                    if (e != null && (e.IsDead || e.IsStunned || e.IsSilenced)) InterruptSpelling("Your concentration is broken.", false);
                    else if (Time.unscaledTime > challenge.deadline) FinishSpelling(challenge.seq, 0f, new List<SpellingPress>(), true); // never answered: it failed
                }
                if (ready != null && Time.unscaledTime >= ready.expiresAt)
                {
                    ready = null;
                    spellSyncDirty = true;
                    spellingView.readySpellId = "";
                    spellingView.readySecondsLeft = 0f;
                    spellingVersion++;
                }
            }
            // The owner's machine casts the spell that was just spelled (the hook lets it through).
            if (spellingView.state == SpellingState.Succeeded && spellingView.seq != autoCastSeq && !string.IsNullOrEmpty(spellingView.readySpellId) && ReferenceEquals(Local, this))
            {
                autoCastSeq = spellingView.seq;
                PlayerAbilityController pc = Abilities;
                int key = SpellSlotOf(spellingView.readySpellId); // where the spell is now (it may have been moved since the sequence began)
                if (pc != null && key >= 0) pc.TryCastFromInput(key);
            }
        }

        // An enemy's blow while spelling: interrupts it when the spell's ability is interrupted by damage (the ability system's own rule).
        private void SpellingDamaged(DamageInfo info)
        {
            if (challenge == null || !IsAuthority || !(info.amount > 0f) || info.isPeriodic) return; // damage over time never interrupts a cast either
            AbilitySlot s = abilities != null ? abilities.GetSlot(challenge.slot) : null;
            AbilityDefinition def = s != null ? s.ability : null;
            if (def == null || !def.interruptedByDamage) return;
            CombatEntity e = Entity;
            float max = e != null ? e.MaxHealth : 0f;
            if (max > 0f && info.amount < def.interruptDamageThreshold * max) return;
            InterruptSpelling("You were hurt: your concentration is broken.", false);
        }

        private void SpellingControlled()
        {
            if (challenge == null || !IsAuthority) return;
            AbilitySlot s = abilities != null ? abilities.GetSlot(challenge.slot) : null;
            if (s != null && s.ability != null && !s.ability.interruptedByControl) return;
            InterruptSpelling("Your concentration is broken.", false);
        }

        // ------------------------------------------------------------------ the owner's client
        /// <summary>The sequence view from the server (owner's client, network layer).</summary>
        public void ApplyReplicatedSpelling(SpellingView v)
        {
            if (v == null || IsAuthority) return;
            v.EnsureLists();
            spellingPendingUntil = 0f;
            spellingView.CopyFrom(v);
            clientReadyUntil = Time.unscaledTime + (string.IsNullOrEmpty(v.readySpellId) ? 0f : v.readySecondsLeft);
            spellingVersion++;
        }

        // A sequence that is not over when the soul stops existing.
        private void ClearSpelling()
        {
            challenge = null;
            ready = null;
            spellingView.state = SpellingState.None;
        }
    }
}
