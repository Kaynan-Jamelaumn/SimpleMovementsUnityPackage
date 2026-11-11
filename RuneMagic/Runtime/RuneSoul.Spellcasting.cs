using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>An ability key of the player, as the spellbook shows it (see <see cref="RuneSoul.GetSpellSlots"/>).</summary>
    public struct SpellSlotInfo
    {
        /// <summary>The slot's index in the player's ability controller.</summary>
        public int slot;
        /// <summary>The key's name as the Input System shows it ("Q", "Mouse 4"...); empty when the key has no binding.</summary>
        public string key;
        /// <summary>The spell on the slot (empty = none).</summary>
        public string spellId;
        /// <summary>Another (non-spell) ability on the slot: its name (empty = none).</summary>
        public string holds;
        /// <summary>A spell cannot be put here (another ability holds the slot and the Spell Balance does not let spells replace it).</summary>
        public bool locked;
    }

    // Spells and the shared ability slots (design spec 16.1-16.4, directive 8): a spell sits on one of the player's EXISTING ability
    // keys. The ability slots, their keys, cooldowns, cast times, effects and the ability bar are the ordinary ones; what is added
    // here is what the ability pipeline knows nothing about: which spell is on which key (kept in the soul and applied to the slot
    // like an absorbed ability), the energy a cast draws from the spell's anchored runes, the Soul Strain of a success or a
    // misfire, and the debuffs of the soul's condition (baked into the slot's modifiers).
    public partial class RuneSoul
    {
        private const string NotRuneUserToCast = "Only rune users (Mages) cast rune spells.";

        private sealed class PendingCast
        {
            public string spellId;
            public AbilityCastInstance cast;
            public CastPlan plan;
            public CastDraw draw;
            public CastRoll roll;
            public bool misfired;
        }

        private readonly Dictionary<int, PendingCast> pendingCasts = new Dictionary<int, PendingCast>();
        private readonly Dictionary<string, CompiledSpell> compiledSpells = new Dictionary<string, CompiledSpell>();
        private int compiledAt = -1;
        private readonly HashSet<int> spellSlotsApplied = new HashSet<int>();
        // When each spell's cooldown ends (Time.time): moving a spell to another key puts it in a new slot, which would start ready.
        private readonly Dictionary<string, float> spellCooldownEnd = new Dictionary<string, float>();
        private PlayerAbilityController abilities;
        private bool abilitiesHooked;
        private bool spellSyncDirty = true;
        private int spellSyncVersion = -1;
        private string spellSyncCondition;
        private bool spellSyncLocked;
        private float nextSpellTick;
        private SpellCastReport lastCast;

        /// <summary>A spell cast ended (where the rules run): it succeeded, misfired or was interrupted.</summary>
        public event Action<RuneSoul, SpellCastReport> SpellCastEnded;

        /// <summary>What the latest spell cast did (null = none yet). Where the rules run.</summary>
        public SpellCastReport LastSpellCast => lastCast;

        // ------------------------------------------------------------------ knowing a spell as something to cast
        /// <summary>The spell compiled (its form, cost, strain and numbers) with the current balance; null when there is no such spell. Any machine.</summary>
        public CompiledSpell CompiledSpellOf(string spellId)
        {
            SpellRecord sp = FindSpell(spellId);
            return sp != null ? CompileSpell(sp) : null;
        }

        private CompiledSpell CompileSpell(SpellRecord sp)
        {
            int v = RuneMagicDatabase.Instance.Version;
            if (compiledAt != v)
            {
                compiledSpells.Clear();
                compiledAt = v;
            }
            if (!compiledSpells.TryGetValue(sp.spellId, out CompiledSpell cs))
            {
                cs = SpellCompiler.Compile(Catalog, sp);
                cs.usesSpelling = SpellingGenerator.Uses(Catalog, sp); // the developer's / server's choice, never the player's
                SpellFormDef form = Catalog.SpellForm(cs.formId);
                cs.chargeable = form != null && form.chargeable && !cs.usesSpelling && Catalog.Spells.charge != null && Catalog.Spells.charge.enabled;
                compiledSpells[sp.spellId] = cs;
            }
            return cs;
        }

        /// <summary>The charge profile of the spell (Spells ▸ Charge) when it can be charged by holding its key; null otherwise. Any machine.</summary>
        public ChargeProfile ChargeProfileOfSpell(string spellId)
        {
            CompiledSpell cs = CompiledSpellOf(spellId);
            return cs != null && cs.chargeable ? Catalog.Spells.charge : null;
        }

        /// <summary>Do the anchored runes hold what a cast charged by <paramref name="fraction"/> draws? Where the rules run (elsewhere: true, the server decides).</summary>
        public bool CanAffordSpellCharge(string spellId, float fraction)
        {
            if (!IsAuthority) return true;
            SpellRecord sp = SpellRules.Find(state, spellId);
            CompiledSpell cs = sp != null ? CompileSpell(sp) : null;
            if (cs == null) return false;
            CastPlan plan = SpellCasting.Plan(Catalog, cs, Restrictions);
            SpellCasting.ApplyCharge(ref plan, Catalog.Spells, fraction);
            return plan.energyCost <= SpellRules.AnchoredEnergy(state, sp) + 1e-3f;
        }

        /// <summary>What casting the spell costs and does now, with the soul's condition applied (any machine; a client predicts from the server's view).</summary>
        public CastPlan PlanSpell(string spellId)
        {
            CompiledSpell cs = CompiledSpellOf(spellId);
            return cs != null ? SpellCasting.Plan(Catalog, cs, Restrictions) : default;
        }

        /// <summary>
        /// Can the spell be cast now: the soul's condition allows it and its anchored runes hold the energy a cast draws. (The
        /// ability's own checks - cooldown, silence, a cast in progress - are the ability system's.) Any machine.
        /// </summary>
        public bool CanCastSpell(string spellId, out string reason)
        {
            // The ability bar asks every frame (to tint a key it cannot use): the answer only changes with the soul, the runes or the data.
            int catalogVersion = RuneMagicDatabase.Instance.Version;
            if (spellId != null && castChecks.TryGetValue(spellId, out CastCheck known) && known.version == Version && known.runes == RunesVersion &&
                known.catalog == catalogVersion && known.runeUser == IsRuneUser)
            {
                reason = known.reason;
                return known.ok;
            }
            bool ok = CheckCastSpell(spellId, out reason);
            if (spellId != null)
                castChecks[spellId] = new CastCheck { version = Version, runes = RunesVersion, catalog = catalogVersion, runeUser = IsRuneUser, ok = ok, reason = reason };
            return ok;
        }

        private struct CastCheck
        {
            public int version, runes, catalog;
            public bool runeUser, ok;
            public string reason;
        }

        /// <summary>
        /// May a cast of the spell start now, beyond its resources: a spelled spell needs a good key sequence first (the server refuses a
        /// cast without one, whatever the client does). Asked when a cast is attempted. Any machine.
        /// </summary>
        public bool CanStartSpell(string spellId, out string reason)
        {
            reason = "";
            if (!SpellingRequired(spellId) || HasReadySpelling(spellId)) return true;
            if (CombatRules.IsVisualOnly(Entity)) return true; // abilities used where combat is off are only shown: nothing to spell
            reason = "Spell it first: press its key and enter the letters.";
            return false;
        }

        private readonly Dictionary<string, CastCheck> castChecks = new Dictionary<string, CastCheck>();

        private bool CheckCastSpell(string spellId, out string reason)
        {
            reason = "";
            if (!IsRuneUser) { reason = NotRuneUserToCast; return false; }
            RuneSoulState st = SynthesisSnapshot();
            SpellRecord sp = SpellRules.Find(st, spellId);
            if (sp == null) { reason = "That spell is no longer in your spellbook."; return false; }
            CastPlan plan = SpellCasting.Plan(Catalog, CompileSpell(sp), Restrictions);
            if (!plan.allowed) { reason = plan.reason; return false; }
            return SpellCasting.CanAfford(st, sp, plan.energyCost, out reason);
        }

        /// <summary>The lines of the ability bar's tooltip for a spell on a slot: costs, strain, the soul's debuff, why it cannot be cast. Any machine.</summary>
        public string DescribeSpell(string spellId)
        {
            RuneSoulState st = SynthesisSnapshot();
            SpellRecord sp = SpellRules.Find(st, spellId);
            if (sp == null) return "";
            CompiledSpell cs = CompileSpell(sp);
            SoulRestrictions r = Restrictions;
            CastPlan plan = SpellCasting.Plan(Catalog, cs, r);
            List<string> lines = SpellCompiler.Describe(Catalog, cs, plan);
            lines.Insert(2, $"Anchored runes hold {SpellRules.AnchoredEnergy(st, sp):0.#} Rune Energy");
            if (!string.IsNullOrEmpty(r.castingDebuff))
                lines.Add($"Casting debuff ({r.castingDebuff}): cast time ×{r.castTimeMultiplier:0.##}, power ×{r.powerMultiplier:0.##}, cost ×{r.energyCostMultiplier:0.##}" +
                          (r.instabilityChance > 0f ? $", +{r.instabilityChance * 100f:0.#}% misfire" : ""));
            if (cs.usesSpelling)
            {
                SpellingParams sparams = SpellingGenerator.Resolve(Catalog, cs, sp, SpellingContextOf(sp), 0);
                lines.Add($"Spelling: {sparams.letters} keys in about {sparams.EffectiveLimit:0.#} s, {(sparams.model == SpellingFailureModel.Strict ? "strict (one mistake fails it)" : $"lenient (up to {sparams.maxMistakes} mistakes)")}" +
                          (sparams.rhythm ? ", with rhythm" : "") + (sparams.damageMode != SpellingDamageMode.None ? ", mistakes strain the soul" : ""));
            }
            if (!CanCastSpell(spellId, out string why))
                lines.Add("Cannot be cast now: " + why);
            return string.Join("\n", lines);
        }

        // ------------------------------------------------------------------ the player's ability slots
        private PlayerAbilityController Abilities
        {
            get
            {
                if (abilities == null)
                {
                    abilitiesHooked = false;
                    Transform root = Entity != null ? Entity.transform : transform;
                    abilities = root.GetComponentInChildren<PlayerAbilityController>(true);
                    if (abilities == null) abilities = GetComponentInParent<PlayerAbilityController>();
                }
                if (abilities != null && !abilitiesHooked)
                {
                    abilitiesHooked = true;
                    abilities.SlotChanged += OnAbilitySlotChanged;
                    abilities.PreCast += OnPreCast; // the Spelling System's key sequence comes before a spell's cast (and before a charge)
                    AttachCastables();
                    // Spells are charged by holding the key: the source says which, and the charger (on the player) does the holding.
                    RuneSpellCharge.Register();
                    if (abilities.GetComponent<AbilityCharger>() == null && abilities.GetComponentInParent<AbilityCharger>() == null)
                        abilities.gameObject.AddComponent<AbilityCharger>();
                }
                return abilities;
            }
        }

        // The soul's hooks in the player's ability controller go when the soul does (the controller may outlive it).
        private void UnhookAbilities()
        {
            if (abilities == null) return;
            abilities.SlotChanged -= OnAbilitySlotChanged;
            abilities.PreCast -= OnPreCast;
            abilitiesHooked = false;
        }

        /// <summary>The ability keys, and what is on each (spell, another ability, nothing). Any machine; empty for a character that is not a rune user.</summary>
        public void GetSpellSlots(List<SpellSlotInfo> into)
        {
            into.Clear();
            if (!IsRuneUser) return;
            PlayerAbilityController pc = Abilities;
            if (pc == null) return;
            bool replace = Catalog.Spells.replaceOtherAbilities;
            for (int i = 0; i < pc.SlotCount; i++)
            {
                AbilityStateMachine key = pc.GetStateMachine(i);
                if (key == null) continue;
                var info = new SpellSlotInfo { slot = i, key = key.KeyLabel ?? "", spellId = "", holds = "" };
                AbilitySlot s = pc.GetSlot(i);
                if (s != null && s.ability != null)
                {
                    if (SpellCompiler.TryParseAbilityId(s.ability.Id, out _, out string id)) info.spellId = id;
                    else { info.holds = s.DisplayName; info.locked = !replace; }
                }
                into.Add(info);
            }
        }

        /// <summary>The ability key the spell is on (-1 = only in the spellbook). Any machine.</summary>
        public int SpellSlotOf(string spellId)
        {
            if (string.IsNullOrEmpty(spellId) || !IsRuneUser) return -1;
            PlayerAbilityController pc = Abilities;
            if (pc == null) return -1;
            for (int i = 0; i < pc.SlotCount; i++)
            {
                AbilitySlot s = pc.GetSlot(i);
                if (s != null && s.ability != null && SpellCompiler.TryParseAbilityId(s.ability.Id, out _, out string id) && id == spellId)
                    return i;
            }
            return -1;
        }

        private static bool IsSpellAbility(AbilityDefinition def) => def != null && SpellCompiler.TryParseAbilityId(def.Id, out _, out _);

        // A slot's ability changed (the server put a spell there, or its content arrived replicated): a spell gets its castable.
        private void OnAbilitySlotChanged(int index)
        {
            if (abilities != null) AttachCastable(abilities.GetSlot(index));
        }

        private void AttachCastables()
        {
            if (abilities == null) return;
            for (int i = 0; i < abilities.SlotCount; i++)
                AttachCastable(abilities.GetSlot(i));
        }

        private void AttachCastable(AbilitySlot s)
        {
            if (s == null || s.ability == null) return;
            if (!SpellCompiler.TryParseAbilityId(s.ability.Id, out _, out string spellId)) return;
            if (s.Castable is RuneSpellCastable rc && rc.SpellId == spellId) return;
            s.Castable = new RuneSpellCastable(this, spellId);
        }

        // ------------------------------------------------------------------ assigning, unassigning, swapping (where the rules run)
        /// <summary>
        /// Puts the spell on an ability key (design spec 16.4): an empty key, a key that holds a spell (the two swap when the spell
        /// already had a key; otherwise the other goes back to the spellbook) or, when the Spell Balance allows it, a key that
        /// holds another ability. Where the rules run.
        /// </summary>
        public bool AssignSpell(string spellId, int slot, out string reason)
        {
            reason = "";
            if (!IsAuthority) { reason = "Only the server assigns spells."; return false; }
            if (!CheckRuneUser(true)) { reason = NotRuneUserToCast; return false; }
            SpellRecord sp = SpellRules.Find(state, spellId);
            if (sp == null) { reason = "That spell is no longer in your spellbook."; Say(reason); return false; }
            PlayerAbilityController pc = Abilities;
            AbilityStateMachine key = pc != null ? pc.GetStateMachine(slot) : null;
            if (key == null) { reason = "That is not one of your ability keys."; Say(reason); return false; }
            AbilitySlot current = pc.GetSlot(slot);
            int from = SpellSlotOf(spellId);
            AbilitySlot moving = from >= 0 ? pc.GetSlot(from) : null;
            if (challenge != null || (current != null && current.cast != null) || (moving != null && moving.cast != null))
            {
                reason = challenge != null ? "Finish spelling first." : "A spell is being cast: wait until it is done.";
                Say(reason);
                return false;
            }
            if (current != null && current.ability != null && !IsSpellAbility(current.ability) && !Catalog.Spells.replaceOtherAbilities)
            {
                reason = $"{SlotName(key, slot)} holds {current.DisplayName}: a spell cannot replace it.";
                Say(reason);
                return false;
            }
            if (!SpellSlotRules.Assign(state, spellId, slot, null, out reason)) { Say(reason); return false; }
            MarkSaveDirty(true);
            ReconcileSpellSlots();
            Say($"{sp.name} is on {SlotName(key, slot)}.");
            return true;
        }

        /// <summary>Takes the spell off an ability key (it stays in the spellbook). Where the rules run.</summary>
        public bool UnassignSpell(int slot, out string reason)
        {
            reason = "";
            if (!IsAuthority) { reason = "Only the server assigns spells."; return false; }
            if (!CheckRuneUser(true)) { reason = NotRuneUserToCast; return false; }
            SpellSlotAssignment a = SpellSlotRules.At(state, slot);
            SpellRecord sp = a != null ? SpellRules.Find(state, a.spellId) : null;
            AbilitySlot held = Abilities != null ? Abilities.GetSlot(slot) : null;
            if (challenge != null || (held != null && held.cast != null))
            {
                reason = challenge != null ? "Finish spelling first." : "A spell is being cast: wait until it is done.";
                Say(reason);
                return false;
            }
            if (!SpellSlotRules.Unassign(state, slot)) { reason = "There is no spell on that key."; Say(reason); return false; }
            MarkSaveDirty(true);
            ReconcileSpellSlots();
            if (sp != null) Say($"{sp.name} is back in your spellbook.");
            return true;
        }

        /// <summary>Assign the spell to the key (any machine: a client asks the server).</summary>
        public bool RequestAssignSpell(string spellId, int slot)
        {
            if (string.IsNullOrEmpty(spellId) || slot < 0 || slot > 255) return false;
            if (!IsAuthority) return Send(RuneRequestKind.AssignSpell, spellId, slot, false);
            return AssignSpell(spellId, slot, out _);
        }

        /// <summary>Take the spell off the key (any machine: a client asks the server).</summary>
        public bool RequestUnassignSpell(int slot)
        {
            if (slot < 0 || slot > 255) return false;
            if (!IsAuthority) return Send(RuneRequestKind.UnassignSpell, "", slot, false);
            return UnassignSpell(slot, out _);
        }

        private void HandleSpellSlotRequest(RuneSoulRequest request)
        {
            int slot = float.IsNaN(request.amount) ? -1 : Mathf.RoundToInt(Mathf.Clamp(request.amount, -1f, 1000f));
            if (slot < 0 || slot > 255) return;
            if (request.kind == RuneRequestKind.AssignSpell) AssignSpell(request.text, slot, out _);
            else UnassignSpell(slot, out _);
        }

        private static string SlotName(AbilityStateMachine key, int slot) =>
            key != null && !string.IsNullOrEmpty(key.KeyLabel) ? $"[{key.KeyLabel}]" : $"ability key {slot + 1}";

        // ------------------------------------------------------------------ keeping the slots in line with the soul (where the rules run)
        // Runs about four times a second: a spell that was made, lost or dismissed, a condition that changed (its debuffs are in the
        // modifiers of the slot) or a key that appeared since the last time (the save came back before the keys existed).
        private void TickSpells()
        {
            if (!IsRuneUser)
            {
                if (spellSlotsApplied.Count > 0 && IsAuthority) ClearAppliedSpellSlots();
                return;
            }
            if (Time.unscaledTime < nextSpellTick) return;
            nextSpellTick = Time.unscaledTime + 0.25f;
            if (Abilities == null || !IsAuthority) return; // a client's slots arrive replicated; their castables attach on the change
            if (spellSyncDirty || spellSyncVersion != runesVersion || spellSyncCondition != view.conditionId || spellSyncLocked != view.collapseLocked)
                ReconcileSpellSlots();
            if (pendingCasts.Count > 0) PrunePendingCasts();
        }

        private void ReconcileSpellSlots()
        {
            if (!IsAuthority) return;
            PlayerAbilityController pc = Abilities;
            if (pc == null) return;
            spellSyncDirty = false;
            spellSyncVersion = runesVersion;
            spellSyncCondition = view.conditionId;
            spellSyncLocked = view.collapseLocked;
            if (!CheckRuneUser(true))
            {
                ClearAppliedSpellSlots();
                return;
            }

            SpellSlotRules.Normalize(state); // spells that are gone leave their keys
            RememberSpellCooldowns(pc);
            var wanted = new Dictionary<int, SpellRecord>();
            foreach (SpellSlotAssignment a in state.spellSlots)
                if (a != null) wanted[a.slot] = SpellRules.Find(state, a.spellId);
            foreach (int slot in new List<int>(spellSlotsApplied))
                if (!wanted.ContainsKey(slot)) ClearSpellSlot(pc, slot);

            SoulRestrictions r = SoulRules.Restrictions(Balance, state.soul, ComputeMax());
            bool replace = Catalog.Spells.replaceOtherAbilities;
            foreach (KeyValuePair<int, SpellRecord> kv in wanted)
            {
                int slot = kv.Key;
                SpellRecord sp = kv.Value;
                if (sp == null) continue;
                if (pc.GetStateMachine(slot) == null)
                {
                    spellSyncDirty = true; // its key does not exist (yet): try again
                    continue;
                }
                AbilitySlot s = pc.GetSlot(slot);
                if (s != null && s.ability != null && !IsSpellAbility(s.ability) && !replace && !spellSlotsApplied.Contains(slot))
                    continue; // another ability took the key since the spell was put there: the spell waits in the spellbook
                CompiledSpell cs = CompileSpell(sp);
                AbilityDefinition def = RuneSpellAbilities.For(cs);
                if (def == null) continue;
                CastPlan plan = SpellCasting.Plan(Catalog, cs, r);
                SpellingOutcome performance = ready != null && ready.spellId == sp.spellId && Time.unscaledTime < ready.expiresAt ? ready.outcome : null;
                AbilityModifierSet mods = RuneSpellAbilities.Modifiers(cs, plan, performance);
                bool same = s != null && s.ability == def && s.label == sp.name && s.Source == AbilitySlotSource.Skill && s.enabled && SameModifiers(s.modifiers, mods);
                if (!same)
                {
                    pc.ApplyReplicatedSlot(slot, def, mods, sp.name, AbilitySlotSource.Skill, true, null);
                    RestoreSpellCooldown(pc.GetSlot(slot), sp.spellId); // a spell moved to another key keeps what was left of its cooldown
                }
                AttachCastable(pc.GetSlot(slot));
                spellSlotsApplied.Add(slot);
            }
        }

        private void RememberSpellCooldowns(PlayerAbilityController pc)
        {
            float now = Time.time;
            for (int i = 0; i < pc.SlotCount; i++)
            {
                AbilitySlot s = pc.GetSlot(i);
                if (s == null || s.ability == null || !SpellCompiler.TryParseAbilityId(s.ability.Id, out _, out string id)) continue;
                float left = s.CooldownRemaining;
                if (left > 0.05f) spellCooldownEnd[id] = Mathf.Max(spellCooldownEnd.TryGetValue(id, out float known) ? known : 0f, now + left);
            }
            if (spellCooldownEnd.Count > 64)
            {
                var done = new List<string>();
                foreach (KeyValuePair<string, float> kv in spellCooldownEnd) if (kv.Value <= now) done.Add(kv.Key);
                foreach (string id in done) spellCooldownEnd.Remove(id);
            }
        }

        private void RestoreSpellCooldown(AbilitySlot s, string spellId)
        {
            if (s != null && spellCooldownEnd.TryGetValue(spellId, out float end) && end > Time.time)
                s.lockedUntil = Mathf.Max(s.lockedUntil, end);
        }

        private void ClearAppliedSpellSlots()
        {
            PlayerAbilityController pc = Abilities;
            foreach (int slot in new List<int>(spellSlotsApplied))
                ClearSpellSlot(pc, slot);
            spellSlotsApplied.Clear();
        }

        private void ClearSpellSlot(PlayerAbilityController pc, int slot)
        {
            spellSlotsApplied.Remove(slot);
            AbilitySlot s = pc != null ? pc.GetSlot(slot) : null;
            if (s == null || s.ability == null || !IsSpellAbility(s.ability)) return; // another ability took it meanwhile: leave it
            if (SpellCompiler.TryParseAbilityId(s.ability.Id, out _, out string clearedId) && s.CooldownRemaining > 0.05f)
                spellCooldownEnd[clearedId] = Time.time + s.CooldownRemaining;
            pc.ApplyReplicatedSlot(slot, null, null, "", AbilitySlotSource.Skill, true, null);
        }

        private static bool SameModifiers(AbilityModifierSet a, AbilityModifierSet b)
        {
            if (a == null || b == null) return false;
            return Mathf.Approximately(a.damageMultiplier, b.damageMultiplier) && Mathf.Approximately(a.healMultiplier, b.healMultiplier) &&
                   Mathf.Approximately(a.controlMultiplier, b.controlMultiplier) && Mathf.Approximately(a.castTimeMultiplier, b.castTimeMultiplier) &&
                   Mathf.Approximately(a.cooldownMultiplier, b.cooldownMultiplier) && Mathf.Approximately(a.rangeMultiplier, b.rangeMultiplier) &&
                   Mathf.Approximately(a.areaMultiplier, b.areaMultiplier) && Mathf.Approximately(a.durationMultiplier, b.durationMultiplier);
        }

        // ------------------------------------------------------------------ casting (where the rules run)
        // The ability pipeline runs the cast (cast time, cooldown, aim, effects); these are the moments it asks the soul.
        internal void SpellCastStarted(string spellId, AbilityCastInstance cast)
        {
            if (!IsAuthority || cast == null) return;
            SpellRecord sp = SpellRules.Find(state, spellId);
            if (sp == null) return;
            CompiledSpell cs = CompileSpell(sp);
            CastPlan plan = SpellCasting.Plan(Catalog, cs, Restrictions);
            // A held key: the cost, the strain and the instability grow with the charge the ability system kept for this cast.
            if (cast.Charge > 0f) SpellCasting.ApplyCharge(ref plan, Catalog.Spells, cast.Charge);
            if (ready != null && ready.spellId == spellId && Time.unscaledTime < ready.expiresAt)
            {
                // The key sequence that was just spelled: its power and cast time are already in the slot; the cost, the strain and the stability are here.
                SpellCasting.ApplyPerformance(ref plan, cs, ready.outcome, Catalog.Spells);
                ready = null;
                spellSyncDirty = true;
                spellingView.readySpellId = "";
                spellingView.readySecondsLeft = 0f;
                spellingVersion++;
            }
            // The ability bar checked the energy a moment ago (same frame): the draw is exact.
            CastDraw draw = SpellCasting.Draw(state, sp, plan.energyCost) ?? new CastDraw { spellId = spellId };
            var p = new PendingCast
            {
                spellId = spellId, cast = cast, plan = plan, draw = draw,
                // The cast's own seed is sent to the owner's client (it drives the cosmetic effects): a misfire rolled from it could be foreseen.
                roll = SpellCasting.Roll(Catalog.Spells, plan.instability, NewSynthesisSeed()),
            };
            pendingCasts[cast.Id] = p;
            energyVersion++; // the runes lost energy
            MarkSaveDirty(false);
            if (p.roll.misfire)
            {
                float castTime = Mathf.Max(0.05f, cast.Definition.CastTime(cast.Stats));
                AbilityRuntime.Schedule(p.roll.fizzleAt * castTime, () => Fizzle(p), cast);
            }
            if (logEvents)
                Debug.Log($"[Rune Magic] {name}: casting {sp.name} (drew {draw.Total:0.#} energy, misfire chance {plan.instability * 100f:0.#}%{(p.roll.misfire ? ", it will misfire" : "")}).", this);
            Refresh();
        }

        // A misfire: the cast is cut off somewhere in the second half of its cast time.
        private void Fizzle(PendingCast p)
        {
            AbilityCastInstance cast = p.cast;
            if (cast == null || cast.Released || cast.Interrupted || !pendingCasts.ContainsKey(cast.Id)) return;
            p.misfired = true;
            PlayerAbilityController pc = Abilities;
            if (pc != null && cast.Slot != null && cast.Slot.CurrentCast == cast)
                pc.InterruptSlot(cast.Slot, CastInterruptReason.Disabled);
        }

        internal void SpellCastReleased(string spellId, AbilityCastInstance cast)
        {
            if (!IsAuthority || cast == null || !pendingCasts.TryGetValue(cast.Id, out PendingCast p)) return; // "only shown" casts paid nothing
            pendingCasts.Remove(cast.Id);
            CastStrain strain = SpellCasting.Success(Balance, p.plan);
            if (strain.Any) Damage(strain.amount, strain.kind, "casting " + SpellName(spellId));
            EndReport(p, SpellCastOutcome.Success, 0f, strain, "");
        }

        internal void SpellCastInterrupted(string spellId, AbilityCastInstance cast, CastInterruptReason reason)
        {
            if (!IsAuthority || cast == null || !pendingCasts.TryGetValue(cast.Id, out PendingCast p)) return;
            pendingCasts.Remove(cast.Id);
            CastStrain strain = SpellCasting.End(Balance, state, p.draw, p.plan, p.misfired, out float refunded);
            if (refunded > 0f)
            {
                energyVersion++;
                MarkSaveDirty(false);
            }
            string spell = SpellName(spellId);
            string text = p.misfired
                ? $"{spell} misfires!" + (strain.Any ? " Your soul is strained." : "")
                : refunded > 0f ? $"{spell} is interrupted: its energy returns to the runes." : $"{spell} is interrupted.";
            if (strain.Any) Damage(strain.amount, strain.kind, (p.misfired ? "misfire of " : "interrupted ") + spell);
            Say(text);
            EndReport(p, p.misfired ? SpellCastOutcome.Misfire : SpellCastOutcome.Interrupted, refunded, strain, text);
        }

        private string SpellName(string spellId) => SpellRules.Find(state, spellId)?.name ?? spellId;

        private void EndReport(PendingCast p, SpellCastOutcome outcome, float refunded, CastStrain strain, string message)
        {
            lastCast = new SpellCastReport
            {
                spellId = p.spellId, name = SpellName(p.spellId), outcome = outcome, energyDrawn = p.draw.Total, energyRefunded = refunded,
                soulDamage = strain.amount, soulDamageKind = strain.kind ?? "", message = message,
            };
            if (logEvents)
                Debug.Log($"[Rune Magic] {name}: {lastCast.name} {outcome} (energy {lastCast.energyDrawn:0.#}, refunded {refunded:0.#}, strain {strain.amount:0.##} {strain.kind}).", this);
            SpellCastEnded?.Invoke(this, lastCast);
        }

        // A cast that never ended (its caster was destroyed mid-cast) must not stay here forever.
        private void PrunePendingCasts()
        {
            List<int> gone = null;
            foreach (KeyValuePair<int, PendingCast> kv in pendingCasts)
                if (kv.Value.cast == null || kv.Value.cast.Phase == AbilityPhase.InCooldown)
                    (gone ?? (gone = new List<int>())).Add(kv.Key);
            if (gone == null) return;
            foreach (int id in gone) pendingCasts.Remove(id);
        }
    }
}
