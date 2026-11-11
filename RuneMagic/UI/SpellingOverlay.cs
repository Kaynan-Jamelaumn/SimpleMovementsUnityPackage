using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RuneMagic
{
    /// <summary>
    /// The Spelling System's overlay (design spec 16.5): the letters a spell asks for, shown large above the ability bar while the player
    /// presses them. It is a HUD-level canvas, not a window: gameplay input stays live (the keys it asks for are never movement or
    /// ability keys), nothing is blocked and the cursor is not touched.
    /// <list type="bullet">
    /// <item><b>Accessible:</b> big letters; the state of every letter is shown by a shape and a word (<c>&gt;</c> now, <c>OK</c>, <c>X</c> wrong key,
    /// <c>MISS</c> with the letter struck through) and by an outline style, besides a colour-blind-safe blue / orange; the time left is a bar
    /// and a number; optional short tones for right and wrong keys (Spelling Balance ▸ Sound Cues, generated, no assets).</item>
    /// <item><b>Layout-safe:</b> the keys are physical Key values; the letter shown is the one on the player's own layout.</item>
    /// <item><b>Only reads the keys of the sequence:</b> any other key (walking, other actions) is ignored, so moving while casting is not a mistake.</item>
    /// </list>
    /// It only records: the key presses (relative to the first prompt) go to the server, which validates and scores them
    /// (<see cref="RuneSoul.SubmitSpelling"/>); what it shows of the result is what the server says. Created automatically.
    /// </summary>
    public class SpellingOverlay : MonoBehaviour
    {
        // Blue and orange read the same for the common colour vision deficiencies; every state also has a word and an outline style.
        private static readonly Color Blue = new Color(0.20f, 0.52f, 1f, 1f);
        private static readonly Color Orange = new Color(1f, 0.62f, 0.10f, 1f);
        private static readonly Color Grey = new Color(0.78f, 0.80f, 0.84f, 1f);
        private static readonly Color Ink = new Color(0.05f, 0.06f, 0.09f, 0.88f);

        private Canvas canvas;
        private CanvasGroup group;
        private TextMeshProUGUI title, status, timeText, footer;
        private RectTransform cellRow, limitFill, windowFill, beatDot;
        private readonly List<Cell> cells = new List<Cell>();

        private RuneSoul soul;
        private int shownSeq = -1;
        private bool running;
        private int runSeq;
        private float startedAt;
        private SpellingParams view;
        private List<string> letters;
        private List<string> pool;
        private SpellingTracker tracker;
        private readonly List<SpellingPress> presses = new List<SpellingPress>();
        private bool submitted;
        private float hideAt;
        private float wrongFlashUntil;
        private int lastWrong = -1;
        private AudioSource audioSource;
        private AudioClip hitCue, wrongCue, goodCue, badCue;

        private sealed class Cell
        {
            public Image box;
            public Outline outline;
            public TextMeshProUGUI letter, mark;
        }

        private static RuneCatalog Catalog => RuneMagicDatabase.Instance.Catalog;

        /// <summary>Is a sequence being asked for on this machine?</summary>
        public static bool IsActive { get; private set; }

        private void OnDisable()
        {
            IsActive = false;
            running = false;
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
        }

        private void Update()
        {
            soul = RuneSoul.Local;
            if (soul == null || !soul.View.runeUser)
            {
                Hide();
                return;
            }
            SpellingView v = soul.SpellingView;
            if (v.state == SpellingState.Active && v.seq != runSeq && v.letters.Count > 0)
                StartRun(v);
            if (running) Run(v);
            else if (canvas != null && canvas.gameObject.activeSelf && Time.unscaledTime >= hideAt) Hide();
        }

        // ------------------------------------------------------------------ a sequence
        private void StartRun(SpellingView v)
        {
            if (canvas == null) Build();
            runSeq = v.seq;
            letters = new List<string>(v.letters);
            pool = new List<string>(soul.SpellingPoolKeys);
            foreach (string l in letters) if (!pool.Contains(l)) pool.Add(l); // the asked-for letters are always accepted
            view = new SpellingParams
            {
                letters = letters.Count, windows = new List<float>(v.windows), totalLimit = v.totalLimit, rhythm = v.rhythm, tempo = v.tempo,
                tolerance = v.tolerance, model = v.model, maxMistakes = v.maxMistakes,
            };
            tracker = new SpellingTracker(view, letters);
            presses.Clear();
            submitted = false;
            lastWrong = -1;
            startedAt = Time.realtimeSinceStartup;
            running = true;
            IsActive = true;
            BuildCells(letters.Count);
            title.text = string.IsNullOrEmpty(v.spellName) ? "Spell it!" : $"<b>{v.spellName}</b>  —  spell it!";
            canvas.gameObject.SetActive(true);
            group.alpha = 1f;
            hideAt = float.MaxValue;
            footer.text = "";
            EnsureAudio(v.soundCues);
        }

        private void Run(SpellingView v)
        {
            float now = Time.realtimeSinceStartup - startedAt;
            if (v.seq == runSeq && v.state != SpellingState.Active)
            {
                // The server has the last word (it cancelled the sequence, or it has already scored it).
                Finish(v);
                return;
            }
            CombatEntity e = soul.Entity;
            if (e != null && (e.IsDead || e.IsStunned || e.IsSilenced))
            {
                status.text = "Your concentration is broken.";
                running = false;
                IsActive = false;
                hideAt = Time.unscaledTime + 1f;
                return;
            }
            if (!submitted)
            {
                tracker.Advance(now);
                bool typing = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null &&
                              UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;
                int before = tracker.Outcome.wrongKeys;
                foreach (string key in pool)
                {
                    if (typing || !SpellingKeyProvider.PressedThisFrame(key)) continue; // a text field has the keys (chat, a name field)
                    // One key at a time, and never two closer than the server accepts (two keys in one frame, a bounce): the first counts.
                    float last = presses.Count > 0 ? presses[presses.Count - 1].t : 0f;
                    if (now - last < Catalog.Spelling.minPressInterval + 0.01f) break;
                    int stepBefore = tracker.Step;
                    int hitsBefore = tracker.Outcome.hits;
                    presses.Add(new SpellingPress(key, now));
                    tracker.Press(key, now);
                    if (tracker.Outcome.hits > hitsBefore) Cue(hitCue);
                    else if (tracker.Outcome.wrongKeys > before)
                    {
                        lastWrong = stepBefore;
                        wrongFlashUntil = Time.unscaledTime + 0.25f;
                        Cue(wrongCue);
                    }
                    before = tracker.Outcome.wrongKeys;
                    if (tracker.Over) break;
                }
                bool outOfTime = now > tracker.Limit + SpellingEvaluator.TimingSlack;
                if (tracker.Over || outOfTime)
                {
                    tracker.Finish();
                    submitted = true;
                    float end = Mathf.Max(now, presses.Count > 0 ? presses[presses.Count - 1].t : 0f);
                    soul.SubmitSpelling(runSeq, end, presses);
                }
            }
            Draw(now);
        }

        private void Finish(SpellingView v)
        {
            running = false;
            IsActive = false;
            Draw(float.MaxValue);
            status.text = v.message;
            if (v.state == SpellingState.Succeeded)
            {
                footer.text = $"Performance {v.performance * 100f:0}%" + (v.speedBonus ? "   FAST BONUS" : "");
                Cue(goodCue);
            }
            else if (v.state == SpellingState.Failed) { footer.text = "The spell failed."; Cue(badCue); }
            else footer.text = "";
            hideAt = Time.unscaledTime + 1.6f;
        }

        private void Hide()
        {
            running = false;
            IsActive = false;
            if (canvas != null && canvas.gameObject.activeSelf) canvas.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ drawing
        private void Draw(float now)
        {
            SpellingOutcome o = tracker.Outcome;
            for (int i = 0; i < cells.Count; i++)
            {
                SpellingStep s = o.steps[i];
                bool current = running && i == tracker.Step && !tracker.Over;
                Cell c = cells[i];
                string label = SpellingKeyProvider.Label(letters[i]);
                Color box, text;
                string mark;
                float outline;
                if (s.state == SpellingStepState.Hit) { box = Blue; text = Color.white; mark = s.offBeat ? "OK (off beat)" : "OK"; outline = 2f; c.letter.text = label; }
                else if (s.state == SpellingStepState.Missed) { box = new Color(Orange.r, Orange.g, Orange.b, 0.45f); text = Ink; mark = "MISS"; outline = 2f; c.letter.text = "<s>" + label + "</s>"; }
                else if (current)
                {
                    bool wrong = lastWrong == i && Time.unscaledTime < wrongFlashUntil;
                    box = wrong ? Orange : new Color(1f, 1f, 1f, 0.92f);
                    text = Ink;
                    mark = wrong ? "X" : ">";
                    outline = 6f; // thick: the letter to press now
                    c.letter.text = label;
                }
                else { box = new Color(1f, 1f, 1f, 0.10f); text = Grey; mark = ""; outline = 1f; c.letter.text = label; }
                c.box.color = box;
                c.letter.color = text;
                c.mark.text = mark;
                c.mark.color = s.state == SpellingStepState.Hit ? Color.white : s.state == SpellingStepState.Missed ? Ink : Grey;
                c.outline.effectDistance = new Vector2(outline, -outline);
                c.outline.effectColor = current ? (lastWrong == i && Time.unscaledTime < wrongFlashUntil ? Ink : Blue) : s.state == SpellingStepState.Missed ? Orange : Grey;
                c.box.rectTransform.localScale = current ? new Vector3(1.12f, 1.12f, 1f) : Vector3.one;
            }

            float limit = Mathf.Max(0.01f, tracker.Limit == float.MaxValue ? view.EffectiveLimit : tracker.Limit);
            float left = Mathf.Max(0f, limit - Mathf.Min(now, limit));
            limitFill.anchorMax = new Vector2(Mathf.Clamp01(left / limit), 1f);
            if (running && !tracker.Over)
            {
                float window = Mathf.Max(0.01f, view.WindowOf(tracker.Step));
                float inWindow = Mathf.Clamp01(1f - (now - tracker.StepOpen) / window);
                windowFill.anchorMax = new Vector2(inWindow, 1f);
                timeText.text = $"{left:0.0} s";
            }
            else
            {
                windowFill.anchorMax = new Vector2(0f, 1f);
                timeText.text = "";
            }
            int mistakes = o.Mistakes;
            string allowed = view.model == SpellingFailureModel.Strict ? "no mistakes allowed" : $"{mistakes} / {view.maxMistakes} mistakes allowed";
            if (running)
            {
                status.text = view.model == SpellingFailureModel.Strict ? $"Strict: one mistake fails the spell ({mistakes} so far)" : $"Mistakes {mistakes} of {view.maxMistakes} allowed";
                footer.text = view.rhythm ? "Keep the beat" : "";
            }
            else if (string.IsNullOrEmpty(status.text)) status.text = allowed;
            // The beat marker: a dot that moves across the window of the current letter, for spells that ask for rhythm.
            beatDot.gameObject.SetActive(running && view.rhythm);
            if (running && view.rhythm)
            {
                float phase = Mathf.Repeat(now, Mathf.Max(0.05f, view.tempo)) / Mathf.Max(0.05f, view.tempo);
                beatDot.anchorMin = beatDot.anchorMax = new Vector2(phase, 0.5f);
            }
        }

        // ------------------------------------------------------------------ building
        private void Build()
        {
            canvas = UITheme.Canvas("Spelling Overlay", 60, null);
            if (!canvas.TryGetComponent(out CanvasScaler scaler)) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            DontDestroyOnLoad(canvas.gameObject);

            Image panel = InventoryUIFactory.Panel("Spelling", canvas.transform, Ink);
            RectTransform pr = panel.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.30f);
            pr.pivot = new Vector2(0.5f, 0f);
            pr.sizeDelta = new Vector2(940f, 330f);
            pr.anchoredPosition = Vector2.zero;
            panel.raycastTarget = false;
            group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; // never in the way of the mouse
            group.interactable = false;
            InventoryUIFactory.Vertical(panel.gameObject, 16, 8f, false);

            title = InventoryUIFactory.Text("Title", panel.transform, "", 26f, Color.white, TextAlignmentOptions.Center);
            title.richText = true;
            title.raycastTarget = false;
            InventoryUIFactory.Size(title, -1f, 34f, 1f);
            cellRow = InventoryUIFactory.Rect("Letters", panel.transform);
            InventoryUIFactory.Horizontal(cellRow.gameObject, 12f);
            InventoryUIFactory.Size(cellRow, -1f, 130f, 1f);

            windowFill = Bar(panel.transform, "This key", Blue, 14f);
            limitFill = Bar(panel.transform, "Total time", Grey, 10f);
            timeText = InventoryUIFactory.Text("Time", panel.transform, "", 22f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            timeText.raycastTarget = false;
            InventoryUIFactory.Size(timeText, -1f, 28f, 1f);
            status = InventoryUIFactory.Text("Status", panel.transform, "", 20f, new Color(1f, 0.9f, 0.6f), TextAlignmentOptions.Center);
            status.raycastTarget = false;
            InventoryUIFactory.Size(status, -1f, 26f, 1f);
            footer = InventoryUIFactory.Text("Footer", panel.transform, "", 18f, Grey, TextAlignmentOptions.Center);
            footer.raycastTarget = false;
            InventoryUIFactory.Size(footer, -1f, 24f, 1f);

            Image dot = InventoryUIFactory.Panel("Beat", limitFill.parent, Orange);
            beatDot = dot.rectTransform;
            beatDot.sizeDelta = new Vector2(18f, 18f);
            dot.raycastTarget = false;
            dot.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            beatDot.gameObject.SetActive(false);
            canvas.gameObject.SetActive(false);
        }

        // A bar that empties to the left: a background and a fill whose right anchor is the share left.
        private static RectTransform Bar(Transform parent, string name, Color fill, float height)
        {
            Image back = InventoryUIFactory.Panel(name + " bar", parent, new Color(1f, 1f, 1f, 0.12f));
            back.raycastTarget = false;
            InventoryUIFactory.Size(back, -1f, height, 1f);
            Image f = InventoryUIFactory.Panel(name, back.transform, fill);
            f.raycastTarget = false;
            RectTransform r = f.rectTransform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            return r;
        }

        private void BuildCells(int count)
        {
            foreach (Transform child in cellRow) Destroy(child.gameObject);
            cells.Clear();
            float size = Mathf.Clamp(820f / Mathf.Max(1, count) - 12f, 56f, 120f);
            for (int i = 0; i < count; i++)
            {
                Image box = InventoryUIFactory.Panel("Letter " + (i + 1), cellRow, Color.white);
                box.raycastTarget = false;
                InventoryUIFactory.Size(box, size, 120f, 0f);
                Outline outline = box.gameObject.AddComponent<Outline>();
                TextMeshProUGUI letter = InventoryUIFactory.Text("Key", box.transform, "", size * 0.62f, Ink, TextAlignmentOptions.Center, FontStyles.Bold);
                letter.richText = true;
                letter.raycastTarget = false;
                RectTransform lr = letter.rectTransform;
                lr.anchorMin = new Vector2(0f, 0.28f);
                lr.anchorMax = Vector2.one;
                lr.offsetMin = lr.offsetMax = Vector2.zero;
                TextMeshProUGUI mark = InventoryUIFactory.Text("Mark", box.transform, "", 18f, Ink, TextAlignmentOptions.Center, FontStyles.Bold);
                mark.raycastTarget = false;
                RectTransform mr = mark.rectTransform;
                mr.anchorMin = Vector2.zero;
                mr.anchorMax = new Vector2(1f, 0.28f);
                mr.offsetMin = mr.offsetMax = Vector2.zero;
                cells.Add(new Cell { box = box, outline = outline, letter = letter, mark = mark });
            }
        }

        // ------------------------------------------------------------------ sound cues (generated: no assets)
        private void EnsureAudio(bool enabled)
        {
            if (!enabled) { audioSource = null; return; }
            if (audioSource == null)
            {
                if (!gameObject.TryGetComponent(out audioSource)) audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 0f;
                audioSource.playOnAwake = false;
            }
            if (hitCue == null)
            {
                hitCue = Tone("spelling-hit", 880f, 0.07f);
                wrongCue = Tone("spelling-wrong", 196f, 0.16f);
                goodCue = Tone("spelling-good", 1320f, 0.22f);
                badCue = Tone("spelling-bad", 130f, 0.30f);
            }
        }

        private void Cue(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, 0.5f);
        }

        private static AudioClip Tone(string name, float hz, float seconds)
        {
            const int rate = 44100;
            int n = Mathf.Max(1, (int)(rate * seconds));
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float fade = Mathf.Min(1f, i / (rate * 0.005f)) * Mathf.Min(1f, (n - i) / (rate * 0.03f));
                data[i] = Mathf.Sin(2f * Mathf.PI * hz * i / rate) * 0.4f * fade;
            }
            AudioClip clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
