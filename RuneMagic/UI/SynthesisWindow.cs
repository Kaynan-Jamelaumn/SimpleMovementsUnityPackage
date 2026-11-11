using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RuneMagic
{
    /// <summary>
    /// The Rune Synthesis window (design spec 11.1): the nine steps of a synthesis for a rune user.
    /// <list type="number">
    /// <item>Purpose (intention) · 2. Desired effect · 3. Input runes · 4. One concept from each rune · 5. Order · 6. Energy of
    /// each rune · 7. Review of the estimated requirements · 8. Confirm · 9. Result.</item>
    /// </list>
    /// Each rune shows its energy, rank, quality and the concepts the player has discovered; the soul's condition and what it
    /// asks (how many runes, how complex an effect) are shown; a synthesis that could destroy runes or collapse the soul asks
    /// for a second confirmation. A rank advancement of seven runes of one kind needs no formula, so it skips to the energy.
    /// <para>The window only <b>asks</b>: every choice is made in a <see cref="SynthesisDraft"/> (pure, tested), previewed with
    /// <see cref="RuneSoul.PreviewSynthesis"/> (exactly what the server will say) and sent with
    /// <see cref="RuneSoul.RequestSynthesis"/>; the server decides, chooses the chance and answers with a result.</para>
    /// Opened from the Rune Soul window's Synthesis button, or by the input action named in Settings ▸ Synthesis Action
    /// ("Synthesis") of the Input Actions asset (no key is fixed in code). Pointer UI; Escape closes it. Only rune users can
    /// open it. Created automatically.
    /// </summary>
    public class SynthesisWindow : MonoBehaviour, IGameplayPanel
    {
        private static SynthesisWindow instance;

        private GlobalInputBinding toggle;
        private Canvas canvas;
        private CanvasGroup group;
        private SocialPanelHandle handle;
        private bool open;

        private TextMeshProUGUI header, stepTitle, side, message;
        private RectTransform modeBar, stepBar, content;
        private Button backButton, nextButton;
        private TextMeshProUGUI nextLabel;

        // state
        private RuneSoul soul;
        private SynthesisDraft draft = new SynthesisDraft();
        private SynthesisStep step = SynthesisStep.Intention;
        private SynthesisEnv env;
        private SynthesisPreview preview;
        private bool previewDirty = true;
        private bool onlySupported = true;
        private int shownRunes = -1, shownVersion = -1;
        private string stepBarKey = "";
        private string shownCondition = "";
        private bool waiting;
        private float waitUntil;
        private int seenReport, seenMessage;
        private float confirmUntil;
        private string note = "";

        // widgets of the step being shown (rebuilt with it)
        private readonly List<EnergyRow> energyRows = new List<EnergyRow>();
        private TextMeshProUGUI reviewText, confirmWarning;
        private Button confirmButton;
        private TextMeshProUGUI confirmLabel;

        private sealed class EnergyRow
        {
            public int index;
            public Slider slider;
            public TextMeshProUGUI value, status;
        }

        public string PanelName => "Rune Synthesis";

        /// <summary>Is the window open?</summary>
        public static bool IsOpen => instance != null && instance.open;

        /// <summary>Opens or closes the window for the local player.</summary>
        public static void Toggle()
        {
            if (instance == null) return;
            if (instance.open) instance.Close();
            else instance.Open();
        }

        /// <summary>Opens the window for the local player (the Rune Soul window's button).</summary>
        public static void Show()
        {
            if (instance != null && !instance.open) instance.Open();
        }

        public void ClosePanel() => Close();

        private static RuneCatalog Catalog => RuneMagicDatabase.Instance.Catalog;

        private void OnEnable()
        {
            instance = this;
            if (!Application.isBatchMode) // a headless server has no player to open it
                toggle = RuneMagicInput.Global(RuneMagicInput.SynthesisAction, "Rune Synthesis", this);
        }

        private void OnDisable()
        {
            if (instance == this) instance = null;
            toggle?.Dispose();
            toggle = null;
            if (open) Close();
        }

        private void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
        }

        private void Update()
        {
            if (toggle != null && toggle.Pressed)
                Toggle();
            if (!open)
                return;
            if (soul == null || !soul.View.runeUser)
            {
                Close();
                return;
            }
            Refresh(false);
        }

        // ------------------------------------------------------------------ open / close
        public void Open()
        {
            RuneSoul s = RuneSoul.Local;
            if (s == null || !s.View.runeUser)
            {
                PlayerActions.Say("Only rune users (Mages) can synthesize runes.");
                return;
            }
            soul = s;
            if (canvas == null) Build();
            RuneSoulWindow.CloseWindow();
            open = true;
            canvas.gameObject.SetActive(true);
            SocialUI.Show(group, true);
            if (handle == null) handle = new SocialPanelHandle(this);
            handle.Open();
            seenReport = soul.LastReport.seq;
            seenMessage = soul.View.messageSeq;
            waiting = false;
            note = "";
            env = MakeEnv();
            draft.Sanitize(env);
            if (step == SynthesisStep.Result) step = draft.FirstIncomplete(env); // a result is not kept across openings
            if (!draft.Steps(env).Contains(step)) step = draft.Steps(env)[0];
            Refresh(true);
        }

        public void Close()
        {
            open = false;
            waiting = false;
            if (group != null) SocialUI.Show(group, false);
            if (canvas != null) canvas.gameObject.SetActive(false);
            handle?.Close();
        }

        private SynthesisEnv MakeEnv() => new SynthesisEnv(Catalog, soul.SynthesisSnapshot(), soul.BuildSynthesisContext());

        // ------------------------------------------------------------------ refreshing
        private void Refresh(bool force)
        {
            RuneSoulView v = soul.View;
            if (soul.LastReport.seq != seenReport)
            {
                // The server answered: the result (also when it arrives after another message).
                seenReport = soul.LastReport.seq;
                waiting = false;
                step = SynthesisStep.Result;
                force = true;
            }
            else if (waiting)
            {
                if (v.messageSeq != seenMessage)
                {
                    // Refused: nothing was spent; the reason is the soul's latest message.
                    waiting = false;
                    seenMessage = v.messageSeq;
                    note = v.message;
                    step = SynthesisStep.Confirm;
                    force = true;
                }
                else if (Time.unscaledTime > waitUntil)
                {
                    waiting = false;
                    note = "The server did not answer. Check your connection and try again.";
                    step = SynthesisStep.Confirm;
                    force = true;
                }
            }
            if (confirmUntil > 0f && Time.unscaledTime > confirmUntil)
            {
                // The second confirmation lapsed: only the button's label changes (the name field must keep what is typed in it).
                confirmUntil = 0f;
                UpdateDynamic();
            }
            bool runesChanged = force || soul.RunesVersion != shownRunes;
            bool viewChanged = force || soul.Version != shownVersion;
            if (!runesChanged && !viewChanged)
                return;
            shownRunes = soul.RunesVersion;
            shownVersion = soul.Version;
            env = MakeEnv();
            // What the steps offer depends on the runes and on the soul's condition (which effects, how many runes); the rest of
            // the soul's view (integrity ticking up) only updates texts, so that a slider being dragged is never rebuilt under the hand.
            string condition = soul.View.conditionId ?? "";
            bool conditionChanged = condition != shownCondition;
            shownCondition = condition;
            if (runesChanged && step != SynthesisStep.Result)
                draft.Sanitize(env);
            if (!draft.Steps(env).Contains(step)) step = draft.Steps(env)[0];
            previewDirty = true;
            if (runesChanged || conditionChanged)
                Rebuild();
            Apply();
        }

        /// <summary>Updates what changes without rebuilding the step: texts, the step bar, the buttons, the preview.</summary>
        private void Apply()
        {
            RuneSoulView v = soul.View;
            header.text = $"<b>Soul</b> {v.integrity:0.#} / {v.maxIntegrity:0.#} · {v.conditionName}" +
                          (v.collapseLocked ? " <color=#ff7a6b>(collapsed)</color>" : "") +
                          $"     <b>Slots</b> {v.usedSlots} / {v.capacity}";
            if (previewDirty)
            {
                preview = draft.runes.Count > 0 ? soul.PreviewSynthesis(draft.BuildRequest(env)) : null;
                previewDirty = false;
            }
            UpdateSide();
            UpdateModes();
            UpdateStepBar();
            UpdateFooter();
            UpdateDynamic();
        }

        private void UpdateModes()
        {
            for (int i = 0; i < modeButtons.Count; i++)
                modeButtons[i].GetComponent<Image>().color = i == (int)draft.mode ? new Color(0.35f, 0.6f, 1f, 0.55f) : InventoryUIFactory.ButtonColor;
        }

        private void MarkChanged()
        {
            previewDirty = true;
            Apply();
        }

        // ------------------------------------------------------------------ navigation
        private void GoTo(SynthesisStep target)
        {
            step = target;
            confirmUntil = 0f;
            note = "";
            Rebuild();
            previewDirty = true;
            Apply();
        }

        private bool CanGo(SynthesisStep target)
        {
            if (target == SynthesisStep.Result || waiting) return false;
            foreach (SynthesisStep s in draft.Steps(env))
            {
                if (s == target) return true;
                if (s == SynthesisStep.Review || s == SynthesisStep.Confirm || s == SynthesisStep.Result) break;
                if (!draft.Check(env, s, out _)) return false;
            }
            return false;
        }

        private bool CanNext(out string why)
        {
            why = "";
            if (step == SynthesisStep.Review)
            {
                if (preview != null && preview.CanAttempt) return true;
                why = preview != null ? preview.message : "Choose the runes first.";
                return false;
            }
            return draft.Check(env, step, out why);
        }

        private void Next()
        {
            if (!CanNext(out string why))
            {
                note = why;
                message.text = why;
                return;
            }
            GoTo(draft.Next(env, step));
        }

        private void Back() => GoTo(draft.Previous(env, step));

        private void NewSynthesis(bool keepFormula)
        {
            SynthesisEnv e = MakeEnv();
            if (!keepFormula)
            {
                draft = new SynthesisDraft { mode = draft.mode };
                step = draft.Steps(e)[0];
            }
            else
            {
                draft.name = "";
                draft.Sanitize(e);
                step = draft.Steps(e).Contains(SynthesisStep.Runes) ? SynthesisStep.Runes : draft.Steps(e)[0];
            }
            note = "";
            Refresh(true);
        }

        private void UpdateFooter()
        {
            bool result = step == SynthesisStep.Result;
            bool confirm = step == SynthesisStep.Confirm;
            backButton.gameObject.SetActive(!result && draft.Previous(env, step) != step && !waiting);
            nextButton.gameObject.SetActive(!result && !confirm);
            if (nextButton.gameObject.activeSelf)
            {
                bool can = CanNext(out string why);
                nextButton.interactable = can;
                nextLabel.text = step == SynthesisStep.Review ? "Continue" : "Next";
                message.text = note.Length > 0 ? note : can ? "" : why;
            }
            else message.text = note;
        }

        private void UpdateStepBar()
        {
            List<SynthesisStep> steps = draft.Steps(env);
            var key = new StringBuilder();
            foreach (SynthesisStep s in steps) key.Append((int)s).Append(s == step ? '*' : CanGo(s) ? '+' : '-');
            key.Append(waiting ? 'w' : 'n');
            if (key.ToString() == stepBarKey) return;
            stepBarKey = key.ToString();
            SocialUI.Clear(stepBar);
            for (int i = 0; i < steps.Count; i++)
            {
                SynthesisStep s = steps[i];
                SynthesisStep captured = s;
                Button b = InventoryUIFactory.Button("Step", stepBar, $"{i + 1} {StepName(s)}", () => GoTo(captured));
                bool current = s == step;
                b.interactable = !current && CanGo(s);
                b.GetComponent<Image>().color = current ? new Color(0.35f, 0.6f, 1f, 0.55f) : InventoryUIFactory.ButtonColor;
                TextMeshProUGUI t = b.GetComponentInChildren<TextMeshProUGUI>();
                t.fontSize = 14f;
                t.color = current ? Color.white : b.interactable ? InventoryUIFactory.TextColor : InventoryUIFactory.MutedColor;
            }
            bool early = step == SynthesisStep.Intention || step == SynthesisStep.Effect || step == SynthesisStep.Runes;
            modeBar.gameObject.SetActive(early && !waiting);
        }

        private static string StepName(SynthesisStep s)
        {
            switch (s)
            {
                case SynthesisStep.Intention: return "Purpose";
                case SynthesisStep.Effect: return "Effect";
                case SynthesisStep.Runes: return "Runes";
                case SynthesisStep.Concepts: return "Concepts";
                case SynthesisStep.Order: return "Order";
                case SynthesisStep.Energy: return "Energy";
                case SynthesisStep.Review: return "Review";
                case SynthesisStep.Confirm: return "Confirm";
                default: return "Result";
            }
        }

        // ------------------------------------------------------------------ the side panel: the formula so far
        private void UpdateSide()
        {
            RuneCatalog c = Catalog;
            var sb = new StringBuilder();
            sb.Append($"<b>Making:</b> {SynthesisDraft.ModeName(draft.mode)}\n");
            if (!draft.IsPure(env))
            {
                IntentionDef intention = c.Intention(draft.intentionId);
                DesiredEffectDef effect = c.Effect(draft.effectId);
                sb.Append($"<b>Purpose:</b> {(intention != null ? intention.Name : "<color=#8a8f99>not chosen</color>")}\n");
                sb.Append($"<b>Effect:</b> {(effect != null ? effect.Name : "<color=#8a8f99>not chosen</color>")}\n");
            }
            else sb.Append("<color=#9aa4b5>Seven runes of one kind need no formula.</color>\n");
            sb.Append("\n<b>Runes, in order</b>\n");
            if (draft.runes.Count == 0) sb.Append("<color=#8a8f99>none yet</color>\n");
            for (int i = 0; i < draft.runes.Count; i++)
            {
                DraftRune d = draft.runes[i];
                RuneInstance r = env.s.Rune(d.runeInstanceId);
                string concept = "";
                if (!draft.IsPure(env) && !string.IsNullOrEmpty(d.conceptId))
                {
                    ConceptChoice k = draft.Concepts(env, d.runeInstanceId).Find(x => x.id == d.conceptId);
                    concept = k != null ? " — " + k.name : "";
                }
                sb.Append($"{i + 1}. {(r != null ? RuneRules.DisplayName(c, r) : "?")}{concept}  <color=#ffd34d>{d.energy:N0}</color>\n");
            }
            sb.Append("\n<color=#9aa4b5>");
            foreach (string line in SynthesisDraft.SoulRequirementLines(env)) sb.Append(line).Append('\n');
            sb.Append("</color>");
            side.text = sb.ToString();
        }

        // ------------------------------------------------------------------ building a step
        private void Rebuild()
        {
            SocialUI.Clear(content);
            energyRows.Clear();
            reviewText = confirmWarning = null;
            confirmButton = null;
            confirmLabel = null;
            stepTitle.text = StepHeading(step);
            switch (step)
            {
                case SynthesisStep.Intention: BuildIntention(); break;
                case SynthesisStep.Effect: BuildEffect(); break;
                case SynthesisStep.Runes: BuildRunes(); break;
                case SynthesisStep.Concepts: BuildConcepts(); break;
                case SynthesisStep.Order: BuildOrder(); break;
                case SynthesisStep.Energy: BuildEnergy(); break;
                case SynthesisStep.Review: BuildReview(); break;
                case SynthesisStep.Confirm: BuildConfirm(); break;
                default: BuildResult(); break;
            }
            stepBarKey = "";
        }

        private string StepHeading(SynthesisStep s)
        {
            int number = draft.Steps(env).IndexOf(s) + 1;
            switch (s)
            {
                case SynthesisStep.Intention: return $"{number}. What are you trying to accomplish?";
                case SynthesisStep.Effect: return $"{number}. What exactly should the magic do?";
                case SynthesisStep.Runes: return $"{number}. Choose the runes";
                case SynthesisStep.Concepts: return $"{number}. Choose a concept from each rune";
                case SynthesisStep.Order: return $"{number}. Arrange the order";
                case SynthesisStep.Energy: return $"{number}. How much energy does each rune give?";
                case SynthesisStep.Review: return $"{number}. Review";
                case SynthesisStep.Confirm: return $"{number}. Confirm";
                default: return $"{number}. Result";
            }
        }

        private void Para(string text, Color? color = null, float size = 16f, FontStyles style = FontStyles.Normal)
        {
            TextMeshProUGUI t = InventoryUIFactory.Text("Text", content, text, size, color ?? InventoryUIFactory.TextColor, TextAlignmentOptions.Left, style);
            t.richText = true;
        }

        private Button Choice(string label, bool selected, bool enabled, Action onClick, float height = 40f)
        {
            Button b = InventoryUIFactory.Button("Choice", content, "", () => onClick());
            TextMeshProUGUI t = b.GetComponentInChildren<TextMeshProUGUI>();
            t.richText = true;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.margin = new Vector4(10f, 0f, 6f, 0f);
            t.text = label;
            InventoryUIFactory.Size(b, -1f, height, 1f);
            b.interactable = enabled;
            if (selected) b.GetComponent<Image>().color = new Color(0.35f, 0.6f, 1f, 0.35f);
            return b;
        }

        private static string Clip(string s, int max) => string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s.Substring(0, max - 1).TrimEnd() + "…";

        private void BuildIntention()
        {
            Para("The purpose decides which results make sense. Choose one.", InventoryUIFactory.MutedColor, 15f);
            foreach (IntentionDef it in SynthesisDraft.Intentions(env.c))
            {
                IntentionDef captured = it;
                Choice($"<b>{it.Name}</b>\n<size=80%><color=#9aa4b5>{Clip(it.description, 110)}</color></size>", draft.intentionId == it.id, true, () =>
                {
                    draft.intentionId = captured.id;
                    if (!string.IsNullOrEmpty(draft.effectId) && env.c.IntentionSupport(captured.id, draft.effectId) <= 0f) draft.effectId = "";
                    Rebuild();
                    MarkChanged();
                }, 54f);
            }
        }

        private void BuildEffect()
        {
            Choice(onlySupported ? "Showing only the effects this purpose supports (click to show all)" : "Showing every effect (click to show only supported ones)",
                false, true, () => { onlySupported = !onlySupported; Rebuild(); MarkChanged(); }, 30f);
            EffectCategory? last = null;
            foreach (EffectChoice ch in draft.Effects(env, onlySupported))
            {
                if (last != ch.def.category)
                {
                    last = ch.def.category;
                    Para(SynthesisDraft.CategoryName(ch.def.category), new Color(1f, 0.9f, 0.6f), 16f, FontStyles.Bold);
                }
                EffectChoice captured = ch;
                string tag = ch.available ? $" <size=80%><color={(ch.support >= 0.999f ? "#7bd88f" : ch.support > 0f ? "#ffd34d" : "#ff7a6b")}>{ch.SupportLabel}</color></size>"
                    : $" <size=80%><color=#ff7a6b>{ch.reason}</color></size>";
                Choice($"<b>{ch.def.Name}</b>{tag}\n<size=80%><color=#9aa4b5>{Clip(ch.def.description, 110)}</color></size>", draft.effectId == ch.def.id, ch.available, () =>
                {
                    draft.effectId = captured.def.id;
                    Rebuild();
                    MarkChanged();
                }, 54f);
            }
        }

        private void BuildRunes()
        {
            Para(draft.CountText(env) + $"  Chosen: {draft.runes.Count}.", new Color(1f, 0.9f, 0.6f), 15f);
            List<RuneChoice> choices = draft.RuneChoices(env);
            if (choices.Count == 0)
                Para("You have no runes. Defeat monsters to find runes and absorb them in the Rune Soul window.", InventoryUIFactory.MutedColor, 15f, FontStyles.Italic);
            foreach (RuneChoice ch in choices)
            {
                RuneChoice captured = ch;
                string concepts = ch.composite ? "essence" : $"{ch.knownConcepts}/{ch.totalConcepts} concepts known";
                string anchors = ch.anchors.Count > 0 ? $" · anchors {string.Join(", ", ch.anchors)}" : "";
                string line2 = ch.usable ? $"R{ch.rank} · Q{ch.quality:0} · energy {ch.energy:N0}/{ch.maxEnergy:N0} · {concepts}{anchors}"
                    : $"<color=#ff7a6b>{ch.reason}</color>";
                Choice($"{(ch.chosen ? "<color=#7bd88f>✔</color> " : "")}<b>{ch.name}</b>\n<size=80%><color=#9aa4b5>{line2}</color></size>", ch.chosen, ch.usable || ch.chosen, () =>
                {
                    if (captured.chosen) draft.Remove(draft.IndexOf(captured.instanceId));
                    else if (!draft.Add(env, captured.instanceId, out string why)) { note = why; message.text = why; return; }
                    note = "";
                    Rebuild();
                    MarkChanged();
                }, 54f);
            }
        }

        private void BuildConcepts()
        {
            if (draft.IsPure(env)) { Para("No concepts to choose: seven runes of one kind only need energy.", InventoryUIFactory.MutedColor); return; }
            Para("Each rune contributes one concept you have discovered. The concept, not only the rune, decides what it does.", InventoryUIFactory.MutedColor, 15f);
            for (int i = 0; i < draft.runes.Count; i++)
            {
                int index = i;
                DraftRune d = draft.runes[i];
                RuneInstance r = env.s.Rune(d.runeInstanceId);
                Para($"{i + 1}. <b>{(r != null ? RuneRules.DisplayName(env.c, r) : "?")}</b> <size=80%><color=#9aa4b5>R{(r != null ? r.rank : 0)}</color></size>", Color.white, 16f);
                List<ConceptChoice> options = draft.Concepts(env, d.runeInstanceId);
                if (options.Count == 0)
                    Para("You have not discovered any concept of this rune yet. Find knowledge sources in dungeons.", new Color(1f, 0.55f, 0.5f), 14f, FontStyles.Italic);
                foreach (ConceptChoice k in options)
                {
                    ConceptChoice captured = k;
                    string desc = k.usable ? Clip(k.description, 120) : $"<color=#ff7a6b>{k.reason}</color>";
                    Choice($"<b>{k.name}</b>\n<size=80%><color=#9aa4b5>{desc}</color></size>", d.conceptId == k.id, k.usable, () =>
                    {
                        draft.SetConcept(env, index, captured.id);
                        Rebuild();
                        MarkChanged();
                    }, 50f);
                }
            }
        }

        private void BuildOrder()
        {
            Para("The runes act one after another, first to last. The right sequence is part of your research: swapping runes changes the outcome.", InventoryUIFactory.MutedColor, 15f);
            for (int i = 0; i < draft.runes.Count; i++)
            {
                int index = i;
                DraftRune d = draft.runes[i];
                RuneInstance r = env.s.Rune(d.runeInstanceId);
                string concept = "";
                if (!draft.IsPure(env) && !string.IsNullOrEmpty(d.conceptId))
                {
                    ConceptChoice k = draft.Concepts(env, d.runeInstanceId).Find(x => x.id == d.conceptId);
                    concept = k != null ? $" <size=80%><color=#9aa4b5>— {k.name}</color></size>" : "";
                }
                RectTransform row = InventoryUIFactory.Rect("Order Row", content);
                InventoryUIFactory.Horizontal(row.gameObject, 6f);
                InventoryUIFactory.Size(row, -1f, 36f, 1f);
                TextMeshProUGUI t = InventoryUIFactory.Text("Name", row, $"{i + 1}. <b>{(r != null ? RuneRules.DisplayName(env.c, r) : "?")}</b>{concept}", 16f,
                    InventoryUIFactory.TextColor, TextAlignmentOptions.MidlineLeft);
                t.richText = true;
                InventoryUIFactory.Size(t, -1f, 36f, 1f);
                Button up = InventoryUIFactory.Button("Up", row, "▲", () => { draft.Move(index, -1); Rebuild(); MarkChanged(); }, 40f);
                up.interactable = i > 0;
                Button down = InventoryUIFactory.Button("Down", row, "▼", () => { draft.Move(index, 1); Rebuild(); MarkChanged(); }, 40f);
                down.interactable = i < draft.runes.Count - 1;
            }
        }

        private void BuildEnergy()
        {
            Para("Runes need not be fully charged. Giving more is not always better: too little cannot work, too much makes a rune overpower the others.",
                InventoryUIFactory.MutedColor, 15f);
            for (int i = 0; i < draft.runes.Count; i++)
            {
                int index = i;
                DraftRune d = draft.runes[i];
                RuneInstance r = env.s.Rune(d.runeInstanceId);
                float held = r != null ? r.energy : 0f;
                string concept = "";
                if (!draft.IsPure(env) && !string.IsNullOrEmpty(d.conceptId))
                {
                    ConceptChoice k = draft.Concepts(env, d.runeInstanceId).Find(x => x.id == d.conceptId);
                    concept = k != null ? $" <size=80%><color=#9aa4b5>— {k.name}</color></size>" : "";
                }
                Para($"{i + 1}. <b>{(r != null ? RuneRules.DisplayName(env.c, r) : "?")}</b>{concept} <size=80%><color=#9aa4b5>holds {held:N0}</color></size>", Color.white, 16f);
                RectTransform row = InventoryUIFactory.Rect("Energy Row", content);
                InventoryUIFactory.Horizontal(row.gameObject, 8f);
                InventoryUIFactory.Size(row, -1f, 32f, 1f);
                Slider slider = InventoryUIFactory.Slider("Energy", row);
                slider.minValue = 0f;
                slider.maxValue = Mathf.Max(1f, Mathf.Floor(held));
                slider.SetValueWithoutNotify(Mathf.Clamp(d.energy, 0f, slider.maxValue));
                TextMeshProUGUI value = InventoryUIFactory.Text("Value", row, "", 15f, new Color(1f, 0.83f, 0.3f), TextAlignmentOptions.MidlineRight);
                InventoryUIFactory.Size(value, 90f, 32f, 0f);
                var er = new EnergyRow { index = i, slider = slider, value = value };
                slider.onValueChanged.AddListener(x =>
                {
                    draft.SetEnergy(env, index, x);
                    MarkChanged();
                });
                InventoryUIFactory.Button("Min", row, "Least", () =>
                {
                    if (preview != null && index < preview.requirements.Count) draft.SetEnergy(env, index, preview.requirements[index].minEnergy);
                    MarkChanged();
                }, 70f);
                InventoryUIFactory.Button("All", row, "All", () => { draft.SetEnergy(env, index, held); MarkChanged(); }, 56f);
                er.status = InventoryUIFactory.Text("Status", content, "", 14f, InventoryUIFactory.MutedColor);
                er.status.richText = true;
                energyRows.Add(er);
            }
        }

        private void BuildReview()
        {
            reviewText = InventoryUIFactory.Text("Review", content, "", 16f, InventoryUIFactory.TextColor);
            reviewText.richText = true;
        }

        private void BuildConfirm()
        {
            Para("Everything is decided. The result depends on the magic as well as on your choices.", InventoryUIFactory.MutedColor, 15f);
            if (!draft.IsPure(env))
            {
                Para("Give it a name (optional):", InventoryUIFactory.TextColor, 15f);
                TMP_InputField field = UITheme.Input("Name", content, "A name for the result", 40, 40f);
                field.SetTextWithoutNotify(draft.name ?? "");
                field.onValueChanged.AddListener(x => draft.name = x ?? "");
            }
            confirmWarning = InventoryUIFactory.Text("Warning", content, "", 16f, new Color(1f, 0.55f, 0.5f));
            confirmWarning.richText = true;
            confirmButton = InventoryUIFactory.Button("Synthesize", content, "Synthesize", Confirm);
            confirmLabel = confirmButton.GetComponentInChildren<TextMeshProUGUI>();
            confirmLabel.fontSize = 20f;
            InventoryUIFactory.Size(confirmButton, -1f, 52f, 1f);
        }

        private void BuildResult()
        {
            SynthesisReport r = soul.LastReport;
            if (waiting || !r.HasResult)
            {
                Para(waiting ? "Waiting for the result…" : "No synthesis yet.", InventoryUIFactory.MutedColor, 18f, FontStyles.Italic);
                return;
            }
            Color tier = r.Tier == SynthesisTier.Perfect ? new Color(1f, 0.85f, 0.3f) : r.Tier == SynthesisTier.Successful ? new Color(0.5f, 0.9f, 0.55f)
                : r.Tier == SynthesisTier.Flawed ? new Color(1f, 0.7f, 0.35f) : new Color(1f, 0.42f, 0.4f);
            string title = r.Tier == SynthesisTier.Perfect ? "Perfect synthesis" : r.Tier == SynthesisTier.Successful ? "Successful synthesis"
                : r.Tier == SynthesisTier.Flawed ? "Flawed synthesis" : "Complete failure" + (string.IsNullOrEmpty(r.severity) ? "" : $" ({r.severity.ToLowerInvariant()})");
            Para($"<size=130%><b>{title}</b></size>", tier, 22f);
            Para($"Synthesis Quality Score <b>{r.score:0.#}%</b>" + (r.faltered ? "  <color=#ffb36b>(the magic faltered)</color>" : ""), InventoryUIFactory.TextColor, 17f);
            var sb = new StringBuilder();
            if (r.Output == SynthesisOutput.Spell) sb.Append($"You made the spell <b>{r.name}</b> (rank {r.rank}). Its runes stay in your soul and anchor it.\n");
            else if (r.Output == SynthesisOutput.Rune) sb.Append($"You made the rune <b>{r.name}</b> (rank {r.rank}). The runes you used are gone.\n");
            else sb.Append("Nothing was made.\n");
            if (r.Output != SynthesisOutput.None)
            {
                sb.Append($"Power {r.power:0} · stability {r.stability:0} · energy cost ×{r.energyCost:0.##} · quality {r.quality:0}");
                if (r.controlPenalty > 0f) sb.Append($" · harder to control ({r.controlPenalty * 100f:0}%)");
                sb.Append('\n');
            }
            if (r.flaws.Count > 0) sb.Append($"<color=#ffb36b>Flaws:</color> {string.Join(", ", r.flaws)}\n");
            if (r.sideEffects.Count > 0) sb.Append($"<color=#ffb36b>Side effects:</color> {string.Join(", ", r.sideEffects)}\n");
            sb.Append($"Energy used {r.energyConsumed:N0}" + (r.energyLost > 0.5f ? $" · <color=#ff7a6b>lost {r.energyLost:N0}</color>" : "") + "\n");
            if (r.soulDamage > 0.05f) sb.Append($"Soul Integrity lost: <color=#ff7a6b>{r.soulDamage:0.#}</color>" + (r.deepDamage > 0.05f ? $" (deep damage {r.deepDamage:0.#})" : "") + "\n");
            if (r.collapsed) sb.Append("<color=#ff7a6b><b>Your soul collapsed.</b></color>\n");
            if (r.destroyedRunes.Count > 0) sb.Append($"<color=#ff7a6b>Destroyed:</color> {string.Join(", ", r.destroyedRunes)}\n");
            if (r.lostSpells.Count > 0) sb.Append($"<color=#ff7a6b>Lost with them:</color> {string.Join(", ", r.lostSpells)}\n");
            if (!string.IsNullOrEmpty(r.hint)) sb.Append($"\n<color=#9aa4b5>What held it back:</color> {r.hint}\n");
            Para(sb.ToString());
            Para("Experiment, and write down what works: the best formulas are found by research.", InventoryUIFactory.MutedColor, 14f, FontStyles.Italic);
            Button again = InventoryUIFactory.Button("Again", content, "Try again with the same formula", () => NewSynthesis(true));
            InventoryUIFactory.Size(again, -1f, 38f, 1f);
            Button fresh = InventoryUIFactory.Button("New", content, "Start a new synthesis", () => NewSynthesis(false));
            InventoryUIFactory.Size(fresh, -1f, 38f, 1f);
        }

        // ------------------------------------------------------------------ what changes with the preview
        private void UpdateDynamic()
        {
            foreach (EnergyRow er in energyRows)
            {
                DraftRune d = draft.runes[er.index];
                er.slider.SetValueWithoutNotify(Mathf.Clamp(d.energy, 0f, er.slider.maxValue));
                er.value.text = $"{d.energy:N0}";
                SlotRequirement req = preview != null && er.index < preview.requirements.Count ? preview.requirements[er.index] : null;
                if (req == null) { er.status.text = ""; continue; }
                switch (req.shown)
                {
                    case EnergyState.Insufficient: er.status.text = $"<color=#ff7a6b>Not enough: it needs at least {req.minEnergy:N0}.</color>"; break;
                    case EnergyState.Excessive: er.status.text = "<color=#ffb36b>Far too much for its place: it will interfere.</color>"; break;
                    case EnergyState.Low: er.status.text = $"<color=#ffd34d>Low.</color> Needs at least {req.minEnergy:N0}."; break;
                    case EnergyState.High: er.status.text = "<color=#ffd34d>High.</color>"; break;
                    default: er.status.text = $"Needs at least {req.minEnergy:N0}."; break;
                }
            }
            if (reviewText != null) reviewText.text = ReviewText();
            if (confirmButton != null)
            {
                bool allowed = soul.CanSynthesize(out string whyNot); // in combat, meditating, collapsed... (the server checks again)
                bool can = allowed && preview != null && preview.CanAttempt && !waiting;
                bool risky = preview != null && (preview.destructionPossible || preview.soulCouldCollapse);
                confirmButton.interactable = can;
                bool armed = confirmUntil > Time.unscaledTime;
                confirmLabel.text = !can ? (!allowed || (preview != null && !preview.CanAttempt) ? "Cannot synthesize now" : "Synthesize")
                    : armed ? (preview.soulCouldCollapse ? "Confirm: your soul could collapse!" : "Confirm: runes could be destroyed!")
                    : risky ? "Synthesize (risky)" : "Synthesize";
                var w = new StringBuilder();
                if (!allowed) w.Append(whyNot).Append('\n');
                if (preview != null)
                {
                    if (!preview.CanAttempt) w.Append(preview.message).Append('\n');
                    if (preview.destructionPossible) w.Append("<b>Warning:</b> if this goes badly, runes you put in can be destroyed.\n");
                    if (preview.soulCouldCollapse) w.Append("<b>Warning:</b> a bad outcome could collapse your soul.\n");
                    foreach (string line in preview.warnings)
                        if (!line.Contains("destroyed") && !line.Contains("collapse")) w.Append(line).Append('\n');
                }
                confirmWarning.text = w.ToString();
            }
        }

        private string ReviewText()
        {
            var sb = new StringBuilder();
            if (preview == null) return "<color=#8a8f99>Choose the runes first.</color>";
            sb.Append($"<b>You are making:</b> {SynthesisDraft.ModeName(draft.mode)}" +
                      (preview.output == SynthesisOutput.Rune ? $" — a rune of rank {preview.resultRank}" : preview.output == SynthesisOutput.Spell ? $" — a spell of rank {preview.resultRank}" : "") + "\n");
            sb.Append(preview.CanAttempt ? "<color=#7bd88f>This can be attempted.</color>\n" : $"<color=#ff7a6b>This cannot be attempted yet:</color> {preview.message}\n");
            sb.Append("\n<b>Requirements</b>\n");
            foreach (SlotRequirement r in preview.requirements)
            {
                string state = r.shown == EnergyState.Insufficient ? "  <color=#ff7a6b>not enough</color>" : r.shown == EnergyState.Excessive ? "  <color=#ffb36b>far too much</color>"
                    : r.shown == EnergyState.Low ? "  <color=#ffd34d>low</color>" : r.shown == EnergyState.High ? "  <color=#ffd34d>high</color>" : "";
                sb.Append($"{r.index + 1}. {r.runeName}{(string.IsNullOrEmpty(r.conceptName) ? "" : " — " + r.conceptName)}: gives {r.allocated:N0} · needs at least {r.minEnergy:N0} · holds {r.available:N0}{state}\n");
            }
            if (preview.CanAttempt)
            {
                sb.Append($"\n<b>Estimated result:</b> {preview.estimateLow:0}–{preview.estimateHigh:0}%  <color=#9aa4b5>({TierName(preview.tierLow)}{(preview.tierLow != preview.tierHigh ? " to " + TierName(preview.tierHigh) : "")}; only an estimate: the exact formula is for you to find)</color>\n");
                sb.Append($"<b>Risk:</b> {preview.riskLevel}  <color=#9aa4b5>· chance the magic falters {preview.failureChance * 100f:0}%</color>\n");
                sb.Append($"<b>Soul Damage:</b> {preview.soulDamageLow:0.#}–{preview.soulDamageHigh:0.#}  <color=#9aa4b5>(of {env.ctx.maxIntegrity:0} Soul Integrity)</color>\n");
                if (draft.mode == SynthesisMode.Spell) sb.Append("<color=#9aa4b5>The runes stay in your soul and anchor the spell; the energy you give is used up.</color>\n");
                else sb.Append("<color=#9aa4b5>The runes you use are used up, and so is any energy they still hold beyond what you give.</color>\n");
            }
            foreach (string w in preview.warnings) sb.Append($"<color=#ffb36b>⚠ {w}</color>\n");
            sb.Append("\n<color=#9aa4b5>");
            foreach (string line in SynthesisDraft.SoulRequirementLines(env)) sb.Append(line).Append('\n');
            sb.Append("</color>");
            return sb.ToString();
        }

        private static string TierName(SynthesisTier t) =>
            t == SynthesisTier.Perfect ? "perfect" : t == SynthesisTier.Successful ? "successful" : t == SynthesisTier.Flawed ? "flawed" : "failure";

        // ------------------------------------------------------------------ confirming
        private void Confirm()
        {
            if (preview == null || !preview.CanAttempt || waiting || !soul.CanSynthesize(out _)) return;
            bool risky = preview.destructionPossible || preview.soulCouldCollapse;
            if (risky && confirmUntil <= Time.unscaledTime)
            {
                // A synthesis that can destroy runes or collapse the soul asks twice.
                confirmUntil = Time.unscaledTime + 5f;
                UpdateDynamic();
                return;
            }
            confirmUntil = 0f;
            SynthesisRequest req = draft.BuildRequest(env);
            seenReport = soul.LastReport.seq;
            seenMessage = soul.View.messageSeq;
            waiting = true;
            waitUntil = Time.unscaledTime + 12f;
            note = "";
            if (!soul.RequestSynthesis(req) && !soul.IsAuthority)
            {
                waiting = false;
                note = "The request could not be sent.";
                UpdateDynamic();
                return;
            }
            step = SynthesisStep.Result;
            Rebuild();
            MarkChanged();
        }

        // ------------------------------------------------------------------ building the window
        private void Build()
        {
            canvas = UITheme.Canvas("Rune Synthesis Window", 73, null);
            if (!canvas.TryGetComponent(out CanvasScaler scaler)) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (!canvas.TryGetComponent(out GraphicRaycaster _)) canvas.gameObject.AddComponent<GraphicRaycaster>();
            InventoryEventSystems.EnsureSingle();
            DontDestroyOnLoad(canvas.gameObject);

            Image panel = InventoryUIFactory.Panel("Rune Synthesis", canvas.transform, InventoryUIFactory.PanelColor);
            SocialUI.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 760f));
            group = panel.gameObject.AddComponent<CanvasGroup>();
            InventoryUIFactory.Vertical(panel.gameObject, 18, 8f, false);

            TextMeshProUGUI title = InventoryUIFactory.Text("Title", panel.transform, "Rune Synthesis", 28f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            InventoryUIFactory.Size(title, -1f, 36f, 1f);
            header = InventoryUIFactory.Text("Header", panel.transform, "", 17f, InventoryUIFactory.TextColor, TextAlignmentOptions.Center);
            header.richText = true;
            InventoryUIFactory.Size(header, -1f, 26f, 1f);

            modeBar = InventoryUIFactory.Rect("Mode", panel.transform);
            InventoryUIFactory.Horizontal(modeBar.gameObject, 8f);
            InventoryUIFactory.Size(modeBar, -1f, 34f, 1f);
            TextMeshProUGUI makeLabel = InventoryUIFactory.Text("Label", modeBar, "Make:", 16f, InventoryUIFactory.MutedColor, TextAlignmentOptions.MidlineRight);
            InventoryUIFactory.Size(makeLabel, 60f, 34f, 0f);
            foreach (SynthesisMode m in new[] { SynthesisMode.Spell, SynthesisMode.CompositeRune, SynthesisMode.RankAdvancement })
            {
                SynthesisMode captured = m;
                Button b = InventoryUIFactory.Button("Mode", modeBar, SynthesisDraft.ModeName(m), () => SetMode(captured), 170f);
                b.GetComponent<Image>().color = InventoryUIFactory.ButtonColor;
                modeButtons.Add(b);
            }

            stepBar = InventoryUIFactory.Rect("Steps", panel.transform);
            InventoryUIFactory.Horizontal(stepBar.gameObject, 4f);
            InventoryUIFactory.Size(stepBar, -1f, 32f, 1f);

            RectTransform columns = InventoryUIFactory.Rect("Columns", panel.transform);
            InventoryUIFactory.Horizontal(columns.gameObject, 14f);
            LayoutElement cle = InventoryUIFactory.Size(columns, -1f, 380f, 1f);
            cle.flexibleHeight = 1f;
            content = Column(columns, "", 1.6f, out _, out stepTitle);
            RectTransform sideContent = Column(columns, "Your formula", 1f, out _, out _);
            side = InventoryUIFactory.Text("Formula", sideContent, "", 16f, InventoryUIFactory.TextColor);
            side.richText = true;

            message = InventoryUIFactory.Text("Message", panel.transform, "", 16f, new Color(1f, 0.9f, 0.6f), TextAlignmentOptions.Center);
            InventoryUIFactory.Size(message, -1f, 24f, 1f);
            RectTransform buttons = InventoryUIFactory.Rect("Buttons", panel.transform);
            InventoryUIFactory.Horizontal(buttons.gameObject, 12f);
            InventoryUIFactory.Size(buttons, -1f, 36f, 1f);
            backButton = InventoryUIFactory.Button("Back", buttons, "Back", Back);
            nextButton = InventoryUIFactory.Button("Next", buttons, "Next", Next);
            nextLabel = nextButton.GetComponentInChildren<TextMeshProUGUI>();
            InventoryUIFactory.Button("Runes", buttons, "Rune Soul", () => { Close(); RuneSoulWindow.Toggle(); });
            InventoryUIFactory.Button("Close", buttons, "Close", Close);
            SocialUI.Show(group, false);
            canvas.gameObject.SetActive(false);
        }

        private readonly List<Button> modeButtons = new List<Button>();

        private void SetMode(SynthesisMode m)
        {
            if (waiting) return;
            draft.SetMode(env, m);
            if (!draft.Steps(env).Contains(step)) step = draft.Steps(env)[0];
            Rebuild();
            MarkChanged();
        }

        /// <summary>A titled, scrolling column; returns its content (and the column itself and its title).</summary>
        private static RectTransform Column(Transform parent, string title, float weight, out RectTransform column, out TextMeshProUGUI head)
        {
            Image bg = InventoryUIFactory.Panel(string.IsNullOrEmpty(title) ? "Column" : title, parent, new Color(1f, 1f, 1f, 0.04f));
            column = bg.rectTransform;
            InventoryUIFactory.Vertical(bg.gameObject, 10, 6f, false);
            LayoutElement le = InventoryUIFactory.Size(bg, -1f, -1f, weight);
            le.flexibleHeight = 1f;
            head = InventoryUIFactory.Text("Header", bg.transform, title, 18f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            InventoryUIFactory.Size(head, -1f, 26f, 1f);

            RectTransform viewport = InventoryUIFactory.Rect("List", bg.transform);
            LayoutElement vle = InventoryUIFactory.Size(viewport, -1f, 80f, 1f);
            vle.flexibleHeight = 1f;
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0.12f);
            RectTransform scrolled = InventoryUIFactory.Rect("Content", viewport);
            scrolled.anchorMin = new Vector2(0f, 1f);
            scrolled.anchorMax = new Vector2(1f, 1f);
            scrolled.pivot = new Vector2(0.5f, 1f);
            scrolled.offsetMin = scrolled.offsetMax = Vector2.zero;
            VerticalLayoutGroup v = scrolled.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 4f;
            v.padding = new RectOffset(4, 4, 4, 4);
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            ContentSizeFitter fit = scrolled.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = scrolled;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            return scrolled;
        }
    }
}
