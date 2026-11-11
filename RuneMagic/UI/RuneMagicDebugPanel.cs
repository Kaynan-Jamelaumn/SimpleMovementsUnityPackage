using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// The Rune Magic debug panel (editor and development builds only; Settings ▸ Debug Panel): the local player's soul -
    /// Soul Integrity, deep damage, condition and what it allows, capacity, runes, pending energy, recovery, meditation -
    /// and, where the rules run (offline, the host), buttons to damage, heal and test it. On a network client it is read-only.
    /// <para>Opened by the input action named in Settings ▸ Debug Panel Action ("RuneMagicDebug") when the Input Actions
    /// asset has it (no key is fixed in code), or from the editor: Tools ▸ SimpleMovements ▸ Rune Magic ▸ Toggle Debug
    /// Panel. Created automatically.</para>
    /// </summary>
    public class RuneMagicDebugPanel : MonoBehaviour
    {
        private static RuneMagicDebugPanel instance;

        /// <summary>Opens or closes the panel (play mode, editor and development builds).</summary>
        public static void Toggle()
        {
            if (instance != null && Allowed)
                instance.open = !instance.open;
        }

        /// <summary>Is the panel open?</summary>
        public static bool IsOpen => instance != null && instance.open;

        private const string DebugSource = "debug";
        private static readonly object DebugModifier = new object();

        private GlobalInputBinding toggle;
        private bool open;
        private Rect window = new Rect(20f, 80f, 400f, 10f);
        private int kindIndex;
        private bool fastRecovery;
        private int discoveryBand;
        private string discoverySource = "ancient_inscription";
        private int sampleIndex;
        private string copied = "";

        private void OnEnable()
        {
            instance = this;
            if (!Application.isBatchMode)
                toggle = RuneMagicInput.Global(RuneMagicInput.DebugPanelAction, "Rune Magic Debug", this, false);
        }

        private void OnDisable()
        {
            if (instance == this) instance = null;
            toggle?.Dispose();
            toggle = null;
        }

        private static bool Allowed => (Application.isEditor || Debug.isDebugBuild) && RuneMagicDatabase.Instance.Catalog.Settings.debugPanel;

        private void Update()
        {
            if (toggle != null && toggle.Pressed && Allowed)
                open = !open;
        }

        /// <summary>The locally controlled player's soul (null when none).</summary>
        public static RuneSoul LocalSoul() => RuneSoul.Local;

        private void OnGUI()
        {
            if (!open || !Allowed)
                return;
            window = GUILayout.Window(GetInstanceID(), window, Draw, "Rune Magic — Soul", GUILayout.Width(400f));
        }

        private void Draw(int id)
        {
            RuneSoul soul = LocalSoul();
            if (soul == null)
            {
                GUILayout.Label("No local player with a Rune Soul.");
                GUI.DragWindow();
                return;
            }
            RuneSoulView v = soul.View;
            GUILayout.Label($"{soul.name} — {(soul.IsAuthority ? "rules run here" : "network client (read-only)")}");
            if (!v.runeUser)
            {
                GUILayout.Label("Not a rune user: no soul resource (abilities and essences as usual).");
                GUI.DragWindow();
                return;
            }
            GUILayout.Label($"Soul Integrity {v.integrity:0.##} / {v.maxIntegrity:0.#} ({v.Percent:0.#}%)   deep damage {v.deepDamage:0.##} (ceiling {v.Ceiling:0.#})");
            GUILayout.Label($"Condition: {v.conditionName}{(string.IsNullOrEmpty(v.castingDebuff) ? "" : " — " + v.castingDebuff)}" +
                            $"{(v.collapseLocked ? $"   COLLAPSE LOCK until {v.lockUntilPercent:0}%" : "")}");
            SoulRestrictions r = soul.Restrictions;
            GUILayout.Label($"Synthesis: {(r.synthesisAllowed ? "allowed" : "NO")}  fail +{r.synthesisFailureChance * 100f:0}%  stability ×{r.synthesisStability:0.##}" +
                            $"{(r.maxSynthesisRunes > 0 ? $"  max runes {r.maxSynthesisRunes}" : "")}{(r.maxSynthesisComplexity < 1f ? $"  complexity ≤ {r.maxSynthesisComplexity:0.##}" : "")}");
            GUILayout.Label($"Casting: {(r.castingAllowed ? "allowed" : "NO")}  time ×{r.castTimeMultiplier:0.##}  power ×{r.powerMultiplier:0.##}  cost ×{r.energyCostMultiplier:0.##}" +
                            $"  unstable {r.instabilityChance * 100f:0}%{(r.maxCastableRank > 0 ? $"  rank ≤ {r.maxCastableRank}" : "")}");
            GUILayout.Label($"Capacity: {v.usedSlots} / {v.capacity} slots ({v.capacityStage})" +
                            (v.nextStageLevel > 0 ? $"   next: {v.nextStageCapacity} at level {v.nextStageLevel}" : ""));
            RuneCatalog cat = RuneMagicDatabase.Instance.Catalog;
            GUILayout.Label($"Runes: {soul.Runes.Count}   pending energy: {v.pendingEnergy:N0}");
            var anchored = new HashSet<string>();
            foreach (SpellRecord sp in soul.Spells)
                if (sp != null) foreach (string anchorId in sp.anchors) anchored.Add(anchorId);
            foreach (RuneInstance held in soul.Runes)
                if (held != null)
                    GUILayout.Label($"  • {RuneRules.DisplayName(cat, held)}  R{held.rank} Q{held.quality:0}  {held.energy:N0}/{cat.Balance.MaxEnergy(held.rank, held.quality):N0}" +
                                    (held.IsComposite ? $"  composite ({RuneRules.SlotCost(cat, held)} slot(s)){(held.flawed ? " FLAWED" : "")}"
                                        : $"  concepts {soul.KnownConcepts(held.runeId).Count}/{(cat.Rune(held.runeId)?.concepts.Count ?? 0)}") +
                                    (anchored.Contains(held.instanceId) ? "  [anchors a spell]" : ""));
            foreach (SpellRecord spell in soul.Spells)
                if (spell != null)
                    GUILayout.Label($"  ✦ {spell.name}  R{spell.rank} {(SynthesisTier)spell.tier} {spell.score:0.#}%  power {spell.power:0}  stability {spell.stability:0}" +
                                    $"  cost ×{spell.energyCostMultiplier:0.##}{(spell.flawed ? "  FLAWED: " + string.Join(",", spell.flaws) : "")}");
            foreach (SpellRecord spell in soul.Spells)
                if (spell != null)
                {
                    CastPlan plan = soul.PlanSpell(spell.spellId);
                    int on = soul.SpellSlotOf(spell.spellId);
                    GUILayout.Label($"    ↳ {(on >= 0 ? "slot " + (on + 1) : "in the book")}  draws {plan.energyCost:0.#} of {SpellRules.AnchoredEnergy(soul.SynthesisSnapshot(), spell):0.#}" +
                                    $"  strain {plan.strainScaled:0.##} / misfire {plan.failureScaled:0.##}  misfire chance {plan.instability * 100f:0.#}%" +
                                    $"  cast ×{plan.castTimeMultiplier:0.##}  power ×{plan.powerMultiplier:0.##}{(plan.allowed ? "" : "  CANNOT CAST: " + plan.reason)}" +
                                    (soul.SpellingRequired(spell.spellId) ? "  [spelled]" : ""));
                }
            SpellingBalance spelling = cat.Spelling;
            SpellingView sv = soul.SpellingView;
            GUILayout.Label($"Spelling System: {spelling.mode} · {spelling.failureModel} · {(SpellingKeyProvider.HasKeyboard ? "keyboard" : "NO KEYBOARD")} · sequence {sv.state}" +
                            (sv.letters.Count > 0 ? $" [{string.Join(" ", sv.letters)}]" : "") + (string.IsNullOrEmpty(sv.readySpellId) ? "" : $" · READY {sv.readySecondsLeft:0.0}s") +
                            (string.IsNullOrEmpty(sv.message) ? "" : $" — {sv.message}"));
            SpellCastReport lastCast = soul.LastSpellCast;
            if (lastCast != null)
                GUILayout.Label($"Last cast: {lastCast.name} — {lastCast.outcome}, energy {lastCast.energyDrawn:0.#} (refunded {lastCast.energyRefunded:0.#}), soul damage {lastCast.soulDamage:0.##} {lastCast.soulDamageKind}");
            GUILayout.Label($"Recovery: {v.recoveryPerMinute:0.##}/min — {v.recoverySources}");
            GUILayout.Label($"Focus: {(v.channel == SoulChannel.None ? "none" : $"{v.channelName} {v.channelProgress * 100f:0}%")}" +
                            $"{(v.incapacitatedFor > 0f ? $"   incapacitated {v.incapacitatedFor:0.0}s" : "")}   saved: {(v.kept ? "yes" : "not yet")}");
            if (!string.IsNullOrEmpty(v.message))
                GUILayout.Label($"Last message: {v.message}");

            if (!soul.IsAuthority)
            {
                GUILayout.Label("The server decides: testing tools work offline and on the host.");
                GUI.DragWindow();
                return;
            }
            var kinds = SoulRules.DamageKindIds(RuneMagicDatabase.Instance.Catalog.Balance);
            kinds.Insert(0, "");
            kindIndex = Mathf.Clamp(kindIndex, 0, kinds.Count - 1);
            string kind = kinds[kindIndex];
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"Kind: {(kind.Length == 0 ? "ordinary" : kind)} ▸")) kindIndex = (kindIndex + 1) % kinds.Count;
                if (GUILayout.Button("Damage 10")) soul.Damage(10f, kind, DebugSource);
                if (GUILayout.Button("Damage 30")) soul.Damage(30f, kind, DebugSource);
                if (GUILayout.Button("Collapse")) soul.Damage(v.integrity * 10f + 1000f, "", DebugSource);
            }
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Restore 10")) soul.Restore(10f, DebugSource);
                if (GUILayout.Button("Heal deep 10")) soul.HealDeep(10f);
                if (GUILayout.Button("Full")) soul.RestoreFully();
                if (GUILayout.Button(v.IsChannelling ? "Stop focus" : "Meditate")) soul.RequestMeditation(!v.IsChannelling);
            }
            using (new GUILayout.HorizontalScope())
            {
                int bonus = soul.CapacityBonusOf(DebugSource);
                if (GUILayout.Button("Capacity +1")) soul.SetCapacityBonus(DebugSource, bonus + 1);
                if (GUILayout.Button("Capacity −1")) soul.SetCapacityBonus(DebugSource, bonus - 1);
                bool fast = GUILayout.Toggle(fastRecovery, "Recovery ×10");
                if (fast != fastRecovery)
                {
                    fastRecovery = fast;
                    if (fast) soul.SetRecoveryModifier(DebugModifier, 10f, 0f);
                    else soul.ClearRecoveryModifier(DebugModifier);
                }
            }
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Energy +500")) soul.AddPendingEnergy(500f, DebugSource);
                if (GUILayout.Button("Absorb a rune")) soul.AbsorbDrop(RandomDrop(cat), new RuneOrigin { kind = DebugSource, source = "debug panel" }, out _);
                if (GUILayout.Button("Drop a rune")) DropNear(soul, RandomDrop(cat));
            }
            DiscoveryBalance disc = cat.Discovery;
            GUILayout.Label($"Discovery: {soul.DiscoveryState?.learned ?? 0} learned, {soul.DiscoveryState?.failed ?? 0} failed, " +
                            $"{soul.DiscoveryState?.burned.Count ?? 0} source(s) burned{(soul.IsDiscovering ? "  — IN PROGRESS" : "")}");
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"Band: {disc.Tier(discoveryBand)?.Name} ▸")) discoveryBand = (discoveryBand + 1) % Mathf.Max(1, disc.tiers.Count);
                if (GUILayout.Button("Start discovery")) soul.BeginDiscoveryAt(discoverySource, discoveryBand, out _);
                if (GUILayout.Button("Reset discovery")) soul.ResetDiscovery();
            }
            IReadOnlyList<OutcomeArchetype> samples = cat.Archetypes;
            if (samples.Count > 0)
            {
                sampleIndex = Mathf.Clamp(sampleIndex, 0, samples.Count - 1);
                OutcomeArchetype sample = samples[sampleIndex];
                bool advance = sample.TotalCount == cat.Balance.runesPerRankAdvancement;
                GUILayout.Label($"Synthesis sample: {sample.Name} ({sample.TotalCount} runes, ideal energy) — " +
                                $"{soul.Spells.Count} spell(s); synthesized {soul.SynthesisCounts()}");
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Sample ▸")) sampleIndex = (sampleIndex + 1) % samples.Count;
                    if (GUILayout.Button("Make spell")) SynthesizeSample(soul, cat, sample, SynthesisMode.Spell);
                    if (GUILayout.Button(advance ? "Advance rune" : "Make rune"))
                        SynthesizeSample(soul, cat, sample, advance ? SynthesisMode.RankAdvancement : SynthesisMode.CompositeRune);
                    if (GUILayout.Button("Dismiss spells"))
                        foreach (SpellRecord s in new List<SpellRecord>(soul.Spells))
                            if (s != null) soul.DismissSpell(s.spellId, out _);
                }
            }
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Spells → free keys")) AssignSpellsToFreeKeys(soul);
                if (GUILayout.Button("Clear spell keys")) ClearSpellKeys(soul);
                if (GUILayout.Button("Refill spell energy")) RefillSpellEnergy(soul, cat);
                if (GUILayout.Button("Spellbook")) SpellbookWindow.Show();
            }
            using (new GUILayout.HorizontalScope())
            {
                // Developer tools: the global mode is the developer's / server's choice (the player never has one).
                SpellingBalance sb = cat.Spelling;
                if (GUILayout.Button($"Spelling mode: {sb.mode} ▸"))
                {
                    sb.mode = (SpellingMode)(((int)sb.mode + 1) % 3);
                    RuneMagicDatabase.Instance.MarkChanged();
                }
                if (GUILayout.Button($"Model: {sb.failureModel} ▸"))
                {
                    sb.failureModel = sb.failureModel == SpellingFailureModel.Strict ? SpellingFailureModel.Lenient : SpellingFailureModel.Strict;
                    RuneMagicDatabase.Instance.MarkChanged();
                }
                if (GUILayout.Button("Cancel sequence")) soul.CancelSpelling();
            }
            using (new GUILayout.HorizontalScope())
            {
                RuneInstance first = soul.Runes.Count > 0 ? soul.Runes[0] : null;
                GUI.enabled = first != null;
                if (GUILayout.Button("Energy → first rune") && first != null) soul.AssignEnergy(first.instanceId, 0f);
                if (GUILayout.Button("Learn a concept") && first != null) LearnOne(soul, cat, first);
                if (GUILayout.Button("Release first") && first != null) soul.ReleaseRune(first.instanceId, out _);
                GUI.enabled = true;
            }
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy state (JSON)"))
                {
                    GUIUtility.systemCopyBuffer = soul.ExportState();
                    copied = "Copied.";
                }
                GUILayout.Label(copied);
            }
            GUI.DragWindow();
        }

        /// <summary>
        /// Developer test: absorbs the runes of an archetype's ideal formula (new ones, with every concept discovered, making room
        /// in the soul when needed), gives each the energy the engine says is ideal and synthesizes. The result is told to the
        /// player as usual (Last message). This reveals the optimum; the player's UI never does.
        /// </summary>
        private static void SynthesizeSample(RuneSoul soul, RuneCatalog cat, OutcomeArchetype a, SynthesisMode mode)
        {
            int need = 0;
            foreach (ArchetypeStage st in a.stages) need += Mathf.Max(1, st.count) * RuneRules.SlotCost(cat, st.runeId);
            if (soul.FreeSlots < need)
                soul.SetCapacityBonus(DebugSource, soul.CapacityBonusOf(DebugSource) + (need - soul.FreeSlots));
            var ids = new List<string>();
            foreach (ArchetypeStage st in a.stages)
            {
                RuneDef def = cat.Rune(st.runeId);
                if (def == null) return;
                for (int i = 0; i < Mathf.Max(1, st.count); i++)
                {
                    RuneInstance r = soul.AbsorbDrop(new RuneDrop { runeId = def.id, rank = Mathf.Max(1, def.minRank), quality = 60f, energy = 1e9f },
                        new RuneOrigin { kind = DebugSource, source = "synthesis sample" }, out _);
                    if (r == null) return;
                    ids.Add(r.instanceId);
                }
                soul.LearnConcept(st.conceptId);
            }
            SynthesisRequest req = SynthesisArchetypes.IdealRequest(a, ids, null, mode);
            SynthesisEvaluation ev = soul.ExplainSynthesis(req, out _);
            if (ev != null)
                for (int i = 0; i < req.slots.Count; i++)
                    req.slots[i].energy = Mathf.Min(ev.slots[i].idealEnergy, soul.FindRune(req.slots[i].runeInstanceId).energy);
            soul.Synthesize(req);
        }

        /// <summary>Developer test: puts every spell that has no key on the first free ability key.</summary>
        private static void AssignSpellsToFreeKeys(RuneSoul soul)
        {
            var keys = new List<SpellSlotInfo>();
            foreach (SpellRecord sp in new List<SpellRecord>(soul.Spells))
            {
                if (sp == null || soul.SpellSlotOf(sp.spellId) >= 0) continue;
                soul.GetSpellSlots(keys);
                foreach (SpellSlotInfo k in keys)
                    if (string.IsNullOrEmpty(k.spellId) && string.IsNullOrEmpty(k.holds))
                    {
                        soul.AssignSpell(sp.spellId, k.slot, out _);
                        break;
                    }
            }
        }

        private static void ClearSpellKeys(RuneSoul soul)
        {
            var keys = new List<SpellSlotInfo>();
            soul.GetSpellSlots(keys);
            foreach (SpellSlotInfo k in keys)
                if (!string.IsNullOrEmpty(k.spellId)) soul.UnassignSpell(k.slot, out _);
        }

        /// <summary>Developer test: fills the runes that anchor spells with energy (through the ordinary pending-energy path).</summary>
        private static void RefillSpellEnergy(RuneSoul soul, RuneCatalog cat)
        {
            foreach (SpellRecord sp in soul.Spells)
            {
                if (sp == null) continue;
                foreach (string anchorId in sp.anchors)
                {
                    RuneInstance r = soul.FindRune(anchorId);
                    if (r == null) continue;
                    float space = cat.Balance.MaxEnergy(r.rank, r.quality) - r.energy;
                    if (space <= 0f) continue;
                    soul.AddPendingEnergy(space, DebugSource);
                    soul.AssignEnergy(anchorId, space);
                }
            }
        }

        private static RuneDrop RandomDrop(RuneCatalog c)
        {
            IReadOnlyList<RuneDef> runes = c.Runes;
            RuneDef def = runes.Count > 0 ? runes[Random.Range(0, runes.Count)] : null;
            return new RuneDrop { runeId = def != null ? def.id : "fire", rank = def != null ? def.minRank : 1, quality = Random.Range(10, 90), energy = 0f };
        }

        private static void DropNear(RuneSoul soul, RuneDrop d)
        {
            CombatEntity e = soul.Entity;
            Transform t = e != null ? e.transform : soul.transform;
            RuneItems.Spawn(d, t.position + t.forward * 1.5f + Vector3.up * 0.6f, t.gameObject);
        }

        private static void LearnOne(RuneSoul soul, RuneCatalog c, RuneInstance r)
        {
            RuneDef def = c.Rune(r.runeId);
            if (def == null) return;
            IReadOnlyList<string> known = soul.KnownConcepts(def.id);
            foreach (ConceptDef k in def.concepts)
            {
                bool has = false;
                for (int i = 0; i < known.Count; i++) if (known[i] == k.id) has = true;
                if (!has) { soul.LearnConcept(k.id); return; }
            }
        }
    }
}
