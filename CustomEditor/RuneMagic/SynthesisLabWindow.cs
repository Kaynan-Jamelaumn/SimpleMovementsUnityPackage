#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RuneMagic.EditorTools
{
    /// <summary>
    /// Tools ▸ SimpleMovements ▸ Rune Magic ▸ Synthesis Lab: a sandbox for the synthesis engine that needs no scene and no play
    /// mode. Choose an intention, a desired effect and a mode, add runes (with a concept, rank, quality and an energy as a
    /// multiple of the ideal) and see how the engine judges the formula: the exact score, every factor, why a formula is
    /// incoherent, each rune's ideal and safe energy, the stats a result would have, and - beside it - what the preview
    /// shows a player. "Execute" runs the synthesis on a copy of the sandbox. For designers tuning the Rune Magic Database
    /// (aspects, effects, rules, archetypes, the engine balance); a player never sees this view.
    /// </summary>
    public class SynthesisLabWindow : EditorWindow
    {
        [Serializable]
        private class Row
        {
            public int rune;
            public int concept;
            public int rank = 1;
            public float quality = 60f;
            [Tooltip("Energy as a multiple of the ideal.")] public float energy = 1f;
        }

        private const string Menu = SMMenu.Root + "Rune Magic/";
        private const int Section = SMMenu.Agriculture + 1;

        [MenuItem(Menu + "Synthesis Lab", false, Section + 40)]
        private static void Open() => GetWindow<SynthesisLabWindow>("Synthesis Lab").Show();

        [SerializeField] private SynthesisMode mode;
        [SerializeField] private int intention;
        [SerializeField] private int effect;
        [SerializeField] private int seed = 1;
        [SerializeField] private int capacity = 99;
        [SerializeField] private float integrity = 100f;
        [SerializeField] private bool learnAll = true;
        [SerializeField] private string resultName = "";
        [SerializeField] private List<Row> rows = new List<Row>();
        private Vector2 scroll;
        private string executed = "";
        private int archetypePick;

        private void OnGUI()
        {
            RuneCatalog c = RuneMagicDatabase.Instance.Catalog;
            IReadOnlyList<RuneDef> runes = c.Runes;
            if (runes.Count == 0 || c.Intentions.Count == 0 || c.Effects.Count == 0)
            {
                EditorGUILayout.HelpBox("The Rune Magic Database has no runes, intentions or desired effects.", MessageType.Warning);
                return;
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("The exact judgement of a formula, as the engine makes it. A player never sees this: the preview below shows what " +
                                    "they do. Energies are multiples of the ideal (1 = ideal).", MessageType.Info);

            mode = (SynthesisMode)EditorGUILayout.EnumPopup("Mode", mode);
            intention = Popup("Intention", intention, c.Intentions.Count, i => c.Intentions[i].Name);
            effect = Popup("Desired effect", effect, c.Effects.Count, i => c.Effects[i].category + "/" + c.Effects[i].Name);
            using (new EditorGUILayout.HorizontalScope())
            {
                seed = EditorGUILayout.IntField("Seed", seed);
                if (GUILayout.Button("Random", GUILayout.Width(70f))) seed = UnityEngine.Random.Range(1, int.MaxValue);
            }
            capacity = Mathf.Max(1, EditorGUILayout.IntField("Soul capacity (slots)", capacity));
            integrity = EditorGUILayout.Slider("Soul Integrity", integrity, 0f, 100f);
            learnAll = EditorGUILayout.Toggle("Every concept discovered", learnAll);
            resultName = EditorGUILayout.TextField("Name (optional)", resultName);

            DrawRows(c, runes);
            DrawLoader(c);

            Build(c, runes, out RuneSoulState soul, out SynthesisRequest req);
            var ctx = new SynthesisContext { capacity = capacity, integrity = integrity, maxIntegrity = 100f };
            EditorGUILayout.Space();
            SynthesisEvaluation ev = SynthesisEngine.Evaluate(c, soul, ctx, req, out SynthesisRefusal refusal, out string refusalText);
            if (ev == null)
                EditorGUILayout.HelpBox($"The engine refuses this request ({refusal}): {refusalText}", MessageType.Warning);
            else
                DrawEvaluation(c, ev);

            DrawPreview(SynthesisEngine.Preview(c, soul, ctx, req));

            EditorGUILayout.Space();
            if (GUILayout.Button("Execute on a copy of the sandbox"))
                executed = Describe(c, soul, ctx, req);
            if (!string.IsNullOrEmpty(executed))
                EditorGUILayout.HelpBox(executed, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------ the formula
        private void DrawRows(RuneCatalog c, IReadOnlyList<RuneDef> runes)
        {
            EditorGUILayout.LabelField("Runes, in order", EditorStyles.boldLabel);
            int remove = -1;
            for (int i = 0; i < rows.Count; i++)
            {
                Row r = rows[i];
                r.rune = Mathf.Clamp(r.rune, 0, runes.Count - 1);
                RuneDef def = runes[r.rune];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField((i + 1).ToString(), GUILayout.Width(20f));
                    r.rune = Popup(null, r.rune, runes.Count, k => runes[k].Name, 110f);
                    def = runes[r.rune];
                    r.concept = def.concepts.Count == 0 ? 0 : Popup(null, Mathf.Clamp(r.concept, 0, def.concepts.Count - 1), def.concepts.Count, k => def.concepts[k].Name, 150f);
                    EditorGUILayout.LabelField("R", GUILayout.Width(12f));
                    r.rank = Mathf.Max(1, EditorGUILayout.IntField(r.rank, GUILayout.Width(26f)));
                    EditorGUILayout.LabelField("Q", GUILayout.Width(12f));
                    r.quality = Mathf.Clamp(EditorGUILayout.FloatField(r.quality, GUILayout.Width(34f)), 0f, 100f);
                    EditorGUILayout.LabelField("×ideal", GUILayout.Width(40f));
                    r.energy = Mathf.Max(0f, EditorGUILayout.FloatField(r.energy, GUILayout.Width(40f)));
                    if (GUILayout.Button("↑", GUILayout.Width(22f)) && i > 0) { rows[i] = rows[i - 1]; rows[i - 1] = r; }
                    if (GUILayout.Button("✕", GUILayout.Width(22f))) remove = i;
                }
            }
            if (remove >= 0) rows.RemoveAt(remove);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add rune")) rows.Add(rows.Count > 0 ? new Row { rune = rows[rows.Count - 1].rune, concept = rows[rows.Count - 1].concept, rank = rows[rows.Count - 1].rank, quality = rows[rows.Count - 1].quality } : new Row());
                if (GUILayout.Button("Clear")) rows.Clear();
            }
        }

        /// <summary>Loads an archetype's ideal formula (its stages, in order) into the sandbox.</summary>
        private void DrawLoader(RuneCatalog c)
        {
            if (c.Archetypes.Count == 0) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                archetypePick = Popup("Load formula", Mathf.Clamp(archetypePick, 0, c.Archetypes.Count - 1), c.Archetypes.Count,
                    i => c.Archetypes[i].Name + " (" + c.Archetypes[i].TotalCount + " runes)");
                if (!GUILayout.Button("Load", GUILayout.Width(60f))) return;
                OutcomeArchetype a = c.Archetypes[archetypePick];
                rows.Clear();
                foreach (ArchetypeStage st in a.stages)
                    for (int n = 0; n < Mathf.Max(1, st.count); n++)
                        rows.Add(new Row { rune = IndexOfRune(c, st.runeId), concept = IndexOfConcept(c, st.runeId, st.conceptId) });
                intention = IndexOf(c.Intentions.Count, i => c.Intentions[i].id == a.intention);
                effect = IndexOf(c.Effects.Count, i => c.Effects[i].id == a.effect);
                mode = a.TotalCount == c.Balance.runesPerRankAdvancement ? SynthesisMode.RankAdvancement : a.mode == ArchetypeMode.Spell ? SynthesisMode.Spell : SynthesisMode.CompositeRune;
                GUI.FocusControl(null);
            }
        }

        private void Build(RuneCatalog c, IReadOnlyList<RuneDef> runes, out RuneSoulState s, out SynthesisRequest req)
        {
            s = new RuneSoulState();
            req = new SynthesisRequest
            {
                mode = mode, seed = seed, name = resultName,
                intentionId = c.Intentions[Mathf.Clamp(intention, 0, c.Intentions.Count - 1)].id,
                effectId = c.Effects[Mathf.Clamp(effect, 0, c.Effects.Count - 1)].id,
            };
            for (int i = 0; i < rows.Count; i++)
            {
                Row r = rows[i];
                RuneDef def = runes[Mathf.Clamp(r.rune, 0, runes.Count - 1)];
                RuneInstance inst = RuneRules.Absorb(c, s, new RuneDrop { runeId = def.id, rank = Mathf.Max(1, r.rank), quality = r.quality, energy = 1e9f },
                    new RuneOrigin { kind = "lab" }, "r" + i, 1L);
                ConceptDef k = def.concepts.Count > 0 ? def.concepts[Mathf.Clamp(r.concept, 0, def.concepts.Count - 1)] : null;
                if (learnAll)
                    foreach (ConceptDef known in def.concepts) RuneRules.Learn(s, def.id, known.id);
                else if (k != null)
                    RuneRules.Learn(s, def.id, k.id);
                req.slots.Add(new SynthesisSlot(inst.instanceId, k != null ? k.id : "", 0f));
            }
            // Energies: the ideal times each row's multiple (the ideal needs one evaluation with any energy).
            var ctx = new SynthesisContext { capacity = capacity, integrity = integrity, maxIntegrity = 100f };
            SynthesisEvaluation probe = SynthesisEngine.Evaluate(c, s, ctx, req.Clone(), out _, out _);
            if (probe == null) return;
            for (int i = 0; i < req.slots.Count; i++)
                req.slots[i].energy = Mathf.Min(probe.slots[i].idealEnergy * rows[i].energy, s.Rune(req.slots[i].runeInstanceId).energy);
        }

        // ------------------------------------------------------------------ what the engine thinks
        private static void DrawEvaluation(RuneCatalog c, SynthesisEvaluation ev)
        {
            EditorGUILayout.LabelField("Exact evaluation", EditorStyles.boldLabel);
            var tier = ev.Incoherent ? SynthesisTier.CompleteFailure : ev.tier;
            EditorGUILayout.LabelField($"Score {ev.score:0.##}% → {tier}{(ev.Incoherent ? " (incoherent: fails whatever the score)" : "")}",
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 });
            foreach (string why in ev.incoherence) EditorGUILayout.HelpBox(why, MessageType.Error);
            Bar("Coverage", ev.factors.coverage);
            Bar("Compatibility", ev.factors.compatibility);
            Bar("Intention", ev.factors.intention);
            Bar("Order", ev.factors.order);
            Bar("Energy", ev.factors.energy);
            Bar("Execution", ev.factors.execution);
            EditorGUILayout.LabelField($"Archetype: {(string.IsNullOrEmpty(ev.archetypeId) ? "none (a custom formula)" : ev.archetypeId + $" (fidelity {ev.archetypeFidelity:0.##})")}" +
                                       $"   synergy {ev.synergy:0.##}  conflict {ev.conflict:0.##}  interference {ev.interference:0.##}");
            EditorGUILayout.LabelField($"Risk {ev.risk:0.##} ({ev.riskLevel})   chance to falter {ev.failureChance * 100f:0.#}%   " +
                                       $"result rank {ev.rank}{(ev.pure ? " (pure advancement)" : "")}   overreach {ev.overreach:0.##}");
            EditorGUILayout.LabelField($"Stats at this score: power {ev.stats.power:0.#}  stability {ev.stats.stability:0.#}  cost ×{ev.stats.energyCostMultiplier:0.##}  quality {ev.stats.quality:0.#}");
            EditorGUILayout.Space(2f);
            foreach (SlotEvaluation se in ev.slots)
                EditorGUILayout.LabelField($"{se.index + 1}. {se.runeName} · {se.conceptName}: relevance {se.relevance:0.##}  stage {se.stage:0.##}  " +
                                           $"energy {se.energy:0} (min {se.minEnergy:0} · ideal {se.idealEnergy:0} · max safe {se.maxSafeEnergy:0}) {se.state}" +
                                           $"{(se.deadWeight ? "  DEAD WEIGHT" : "")}{(se.overpowering ? "  OVERPOWERING" : "")}{(se.overreach > 0f ? $"  overreach {se.overreach:0.##}" : "")}");
        }

        private static void DrawPreview(SynthesisPreview p)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("What the player's preview shows", EditorStyles.boldLabel);
            if (!p.CanAttempt)
            {
                EditorGUILayout.LabelField($"Cannot proceed ({p.refusal}): {p.message}");
                return;
            }
            EditorGUILayout.LabelField($"Estimate {p.estimateLow:0}–{p.estimateHigh:0}% ({p.tierLow} to {p.tierHigh}) · risk {p.riskLevel} · " +
                                       $"Soul Damage {p.soulDamageLow:0.#}–{p.soulDamageHigh:0.#}{(p.soulCouldCollapse ? " (COULD COLLAPSE THE SOUL)" : "")}" +
                                       $"{(p.destructionPossible ? " · runes could be destroyed" : "")}");
            foreach (SlotRequirement r in p.requirements)
                EditorGUILayout.LabelField($"   {r.runeName}: needs at least {r.minEnergy:0}, holds {r.available:0}, given {r.allocated:0} ({r.shown})");
            foreach (string w in p.warnings) EditorGUILayout.LabelField("   ⚠ " + w);
        }

        private static string Describe(RuneCatalog c, RuneSoulState sandbox, SynthesisContext ctx, SynthesisRequest req)
        {
            RuneSoulState copy = sandbox.Clone();
            SynthesisResult r = SynthesisEngine.Execute(c, copy, ctx, req, DateTime.UtcNow.Ticks);
            var sb = new StringBuilder();
            if (!r.performed) return $"Refused ({r.refusal}): {r.message}";
            sb.Append($"{r.tier} at {r.score:0.##}%{(r.faltered ? " (the magic faltered)" : "")}{(r.incoherent ? " — incoherent formula" : "")}\n{r.message}\n");
            if (r.output == SynthesisOutput.Spell)
                sb.Append($"Spell '{r.spell.name}': rank {r.spell.rank}, power {r.spell.power:0.#}, stability {r.spell.stability:0.#}, cost ×{r.spell.energyCostMultiplier:0.##}, " +
                          $"control penalty {r.spell.controlPenalty:0.##}{(r.spell.sideEffects.Count > 0 ? ", side effects: " + string.Join(", ", r.spell.sideEffects) : "")}\n");
            else if (r.output == SynthesisOutput.Rune)
                sb.Append($"Rune '{RuneRules.DisplayName(c, r.rune)}': rank {r.rune.rank}, quality {r.rune.quality:0}, energy {r.rune.energy:0}, " +
                          $"{(r.rune.IsComposite ? $"composite, {r.rune.composite.slotCost} slot(s)" : "the same rune, one rank higher")}\n");
            if (r.flaws.Count > 0) sb.Append("Flaws: ").Append(string.Join(", ", r.flaws)).Append('\n');
            sb.Append($"Energy consumed {r.energyConsumed:0}, lost {r.energyLost:0}{(string.IsNullOrEmpty(r.severity) ? "" : $" ({r.severity})")}\n");
            foreach (SoulDamageEntry d in r.soulDamage) sb.Append($"Soul Damage {d.amount:0.#} [{d.kind}] {d.source}\n");
            if (r.runesDestroyed) sb.Append("Destroyed: ").Append(string.Join(", ", r.destroyedRunes)).Append('\n');
            return sb.ToString().TrimEnd();
        }

        // ------------------------------------------------------------------ helpers
        private static void Bar(string label, float value)
        {
            Rect r = EditorGUILayout.GetControlRect(false, 16f);
            EditorGUI.ProgressBar(r, Mathf.Clamp01(value), $"{label} {value:0.##}");
        }

        private static int Popup(string label, int index, int count, Func<int, string> name, float width = -1f)
        {
            var names = new string[count];
            for (int i = 0; i < count; i++) names[i] = name(i);
            index = Mathf.Clamp(index, 0, Mathf.Max(0, count - 1));
            if (label == null)
                return width > 0f ? EditorGUILayout.Popup(index, names, GUILayout.Width(width)) : EditorGUILayout.Popup(index, names);
            return EditorGUILayout.Popup(label, index, names);
        }

        private static int IndexOf(int count, Func<int, bool> match)
        {
            for (int i = 0; i < count; i++) if (match(i)) return i;
            return 0;
        }

        private static int IndexOfRune(RuneCatalog c, string runeId) => IndexOf(c.Runes.Count, i => c.Runes[i].id == runeId);

        private static int IndexOfConcept(RuneCatalog c, string runeId, string conceptId)
        {
            RuneDef def = c.Rune(runeId);
            return def == null ? 0 : IndexOf(def.concepts.Count, i => def.concepts[i].id == conceptId);
        }
    }
}
#endif
