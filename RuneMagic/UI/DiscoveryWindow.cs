using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RuneMagic
{
    /// <summary>
    /// The concept discovery window (design spec 17.1): opens when a knowledge source starts a discovery for the local player.
    /// <list type="bullet">
    /// <item><b>Minigame (rune tracing):</b> a marker runs around a rune circle; the player seals each glyph by clicking
    /// <i>Seal</i> while the marker is inside the lit arc. The clicks are reported to the server, which replays them.</item>
    /// <item><b>Riddle:</b> the riddle, its step, and the answers to choose from. The answer is never on this machine.</item>
    /// <item><b>Result:</b> what was learned, or why the source is lost.</item>
    /// </list>
    /// Input is the pointer on the window (UI buttons): no key is read, so nothing is bound in code. Escape (or whatever
    /// closes gameplay panels) only hides the window; <i>Give up</i> leaves for good (a riddle left unanswered counts as wrong).
    /// Walking away from the source or running out of time ends it on the server. Created automatically.
    /// </summary>
    public class DiscoveryWindow : MonoBehaviour, IGameplayPanel
    {
        private const float Radius = 120f;

        private Canvas canvas;
        private CanvasGroup group;
        private SocialPanelHandle handle;
        private bool open;
        private RuneSoul soul;

        private TextMeshProUGUI title, status, body, footer;
        private RectTransform ringRoot, optionList;
        private Button sealButton, closeButton, giveUpButton;
        private TextMeshProUGUI sealLabel;
        private RectTransform marker;
        private readonly List<Image> arcs = new List<Image>();
        private readonly List<Image> dots = new List<Image>();

        private int shownVersion = -1;
        private int hiddenVersion = -1;
        private MinigameChallenge challenge;
        private float startedAt;
        private readonly List<float> presses = new List<float>();
        private bool submitted;

        public string PanelName => "Discovery";

        public static bool IsOpen => instance != null && instance.open;
        private static DiscoveryWindow instance;

        /// <summary>Hides the window (Escape): the discovery goes on; the source can be used again to show it.</summary>
        public void ClosePanel()
        {
            if (!open) return;
            hiddenVersion = soul != null ? soul.DiscoveryVersion : -1;
            Hide();
        }

        private void OnEnable() => instance = this;

        private void OnDisable()
        {
            if (instance == this) instance = null;
            if (open) Hide();
        }

        private void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
        }

        // ------------------------------------------------------------------ driving
        private void Update()
        {
            RuneSoul s = RuneSoul.Local;
            if (s != soul)
            {
                soul = s;
                shownVersion = -1;
            }
            if (soul == null || !soul.View.runeUser)
            {
                if (open) Hide();
                return;
            }
            DiscoveryView v = soul.DiscoveryView;
            if (v.phase == DiscoveryPhase.None)
            {
                if (open) Hide();
                hiddenVersion = -1;
                return;
            }
            if (!open)
            {
                // A new discovery (or a re-shown one) opens the window; one the player hid stays hidden until it changes.
                if (v.version == hiddenVersion) return;
                Show();
            }
            if (v.version != shownVersion) Rebuild(v);
            if (v.phase == DiscoveryPhase.Minigame && challenge != null) AnimateMinigame(v);
        }

        private void Show()
        {
            if (canvas == null) Build();
            open = true;
            canvas.gameObject.SetActive(true);
            SocialUI.Show(group, true);
            if (handle == null) handle = new SocialPanelHandle(this);
            handle.Open();
            shownVersion = -1;
        }

        private void Hide()
        {
            open = false;
            if (group != null) SocialUI.Show(group, false);
            if (canvas != null) canvas.gameObject.SetActive(false);
            handle?.Close();
            if (soul != null && !soul.IsDiscovering) soul.DismissDiscovery();
        }

        // ------------------------------------------------------------------ content per phase
        private void Rebuild(DiscoveryView v)
        {
            shownVersion = v.version;
            title.text = $"{v.sourceName}  <size=70%><color=#9aa4b5>{v.tierName}</color></size>";
            ringRoot.gameObject.SetActive(v.phase == DiscoveryPhase.Minigame);
            sealButton.gameObject.SetActive(v.phase == DiscoveryPhase.Minigame);
            optionList.gameObject.SetActive(v.phase == DiscoveryPhase.Riddle);
            giveUpButton.gameObject.SetActive(v.Active);
            closeButton.gameObject.SetActive(!v.Active);
            SocialUI.Clear(optionList);
            submitted = false;
            presses.Clear();
            challenge = null;
            switch (v.phase)
            {
                case DiscoveryPhase.Minigame:
                    BuildMinigame(v);
                    break;
                case DiscoveryPhase.Riddle:
                    status.text = v.steps > 1 ? $"Riddle {v.step} of {v.steps}" : "Riddle";
                    body.text = v.text;
                    for (int i = 0; i < v.options.Count; i++)
                    {
                        int index = i;
                        Button b = InventoryUIFactory.Button("Option", optionList, v.options[i], () => Answer(index));
                        InventoryUIFactory.Size(b, -1f, 44f, 1f);
                    }
                    footer.text = "Choose carefully: a wrong answer burns this source for this concept. " +
                                  "Escape only hides this window; walking away or giving up counts as a wrong answer.";
                    break;
                case DiscoveryPhase.Learned:
                    status.text = "<color=#7be0a0>Discovered</color>";
                    body.text = string.IsNullOrEmpty(v.learnedName) ? v.result : $"<size=140%><b>{v.learnedName}</b></size>\n\n{v.result}";
                    footer.text = "The concept is yours for good. It is available for synthesis.";
                    break;
                default:
                    status.text = "<color=#ff9a8a>The source falls silent</color>";
                    body.text = v.result;
                    footer.text = v.burned ? "Seek another dungeon with the same kind of knowledge." : "";
                    break;
            }
        }

        private void BuildMinigame(DiscoveryView v)
        {
            challenge = v.challenge;
            startedAt = Time.unscaledTime;
            status.text = $"Seal the glyphs  <size=75%>({v.attemptsLeft} {(v.attemptsLeft == 1 ? "try" : "tries")} left)</size>";
            body.text = "A marker circles the rune. Click <b>Seal</b> while it is inside the lit arc.";
            footer.text = "Only the clicks you make here are reported; the server checks the timing.";
            foreach (Image a in arcs) if (a != null) Destroy(a.gameObject);
            foreach (Image d in dots) if (d != null) Destroy(d.gameObject);
            arcs.Clear();
            dots.Clear();
            if (challenge == null || challenge.nodes == null) return;
            for (int i = 0; i < challenge.nodes.Count; i++)
            {
                TraceNode n = challenge.nodes[i];
                Image arc = HudStyle.Image("Target " + i, ringRoot, new Color(0.45f, 0.86f, 0.82f, 0.9f), HudStyle.Ring());
                Center(arc.rectTransform);
                arc.rectTransform.sizeDelta = new Vector2(Radius * 2f + 24f, Radius * 2f + 24f);
                arc.rectTransform.anchoredPosition = Vector2.zero;
                arc.type = Image.Type.Filled;
                arc.fillMethod = Image.FillMethod.Radial360;
                arc.fillOrigin = (int)Image.Origin360.Top;
                arc.fillClockwise = true;
                arc.fillAmount = Mathf.Clamp(n.width, 0.01f, 1f);
                arc.rectTransform.localEulerAngles = new Vector3(0f, 0f, -(n.center - n.width * 0.5f) * 360f);
                arcs.Add(arc);
                // A dot at the target's centre numbers the glyphs.
                Image dot = HudStyle.Image("Glyph " + i, ringRoot, Color.white, HudStyle.Circle());
                Center(dot.rectTransform);
                dot.rectTransform.sizeDelta = new Vector2(16f, 16f);
                dot.rectTransform.anchoredPosition = OnCircle(n.center, Radius + 28f);
                dots.Add(dot);
            }
            RefreshArcs();
        }

        private void RefreshArcs()
        {
            for (int i = 0; i < arcs.Count; i++)
            {
                bool current = i == presses.Count;
                bool done = i < presses.Count;
                Color c = done ? new Color(0.5f, 0.55f, 0.6f, 0.25f) : current ? new Color(0.45f, 0.86f, 0.82f, 1f) : new Color(0.45f, 0.86f, 0.82f, 0.25f);
                arcs[i].color = c;
                if (i < dots.Count) dots[i].color = done ? new Color(0.5f, 0.9f, 0.6f, 1f) : current ? Color.white : new Color(1f, 1f, 1f, 0.3f);
            }
            sealLabel.text = presses.Count < (challenge != null ? challenge.nodes.Count : 0)
                ? $"Seal glyph {presses.Count + 1} / {challenge.nodes.Count}" : "Judging…";
        }

        private void AnimateMinigame(DiscoveryView v)
        {
            float t = Time.unscaledTime - startedAt;
            marker.anchoredPosition = OnCircle(RuneTraceMinigame.MarkerAt(challenge, t), Radius);
            bool waiting = submitted || presses.Count >= challenge.nodes.Count;
            sealButton.interactable = !waiting && t < challenge.timeLimit;
            if (t >= challenge.timeLimit && !submitted) Submit(); // time is up: report what was sealed (a short report fails the try)
        }

        private static void Center(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static Vector2 OnCircle(float fraction, float radius)
        {
            float a = fraction * Mathf.PI * 2f;
            return new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * radius;
        }

        // ------------------------------------------------------------------ actions
        private void Seal()
        {
            if (challenge == null || submitted || presses.Count >= challenge.nodes.Count) return;
            presses.Add(Time.unscaledTime - startedAt);
            RefreshArcs();
            if (presses.Count >= challenge.nodes.Count) Submit();
        }

        private void Submit()
        {
            if (submitted || soul == null) return;
            submitted = true;
            RefreshArcs();
            // A report shorter than the challenge is judged as it is: a failed try.
            if (presses.Count == 0) presses.Add(challenge != null ? challenge.timeLimit : 0f);
            soul.RequestDiscoveryMinigame(presses);
        }

        private void Answer(int index)
        {
            if (soul == null) return;
            foreach (Button b in optionList.GetComponentsInChildren<Button>()) b.interactable = false;
            soul.RequestDiscoveryAnswer(index);
        }

        private void GiveUp()
        {
            if (soul != null) soul.RequestDiscoveryAbandon();
        }

        private void CloseResult()
        {
            if (soul != null) soul.DismissDiscovery();
            Hide();
        }

        // ------------------------------------------------------------------ building
        private void Build()
        {
            canvas = UITheme.Canvas("Discovery Window", 73, null);
            if (!canvas.TryGetComponent(out CanvasScaler scaler)) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (!canvas.TryGetComponent(out GraphicRaycaster _)) canvas.gameObject.AddComponent<GraphicRaycaster>();
            InventoryEventSystems.EnsureSingle();
            DontDestroyOnLoad(canvas.gameObject);

            Image panel = InventoryUIFactory.Panel("Discovery", canvas.transform, InventoryUIFactory.PanelColor);
            SocialUI.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 700f));
            group = panel.gameObject.AddComponent<CanvasGroup>();
            InventoryUIFactory.Vertical(panel.gameObject, 20, 10f, false);

            title = InventoryUIFactory.Text("Title", panel.transform, "", 28f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            title.richText = true;
            InventoryUIFactory.Size(title, -1f, 38f, 1f);
            status = InventoryUIFactory.Text("Status", panel.transform, "", 20f, InventoryUIFactory.TextColor, TextAlignmentOptions.Center);
            status.richText = true;
            InventoryUIFactory.Size(status, -1f, 28f, 1f);

            body = InventoryUIFactory.Text("Body", panel.transform, "", 19f, InventoryUIFactory.TextColor, TextAlignmentOptions.Center);
            body.richText = true;
            InventoryUIFactory.Size(body, -1f, 150f, 1f);

            ringRoot = InventoryUIFactory.Rect("Ring", panel.transform);
            InventoryUIFactory.Size(ringRoot, -1f, 300f, 1f);
            Image back = HudStyle.Image("Back", ringRoot, new Color(1f, 1f, 1f, 0.08f), HudStyle.Ring());
            back.rectTransform.sizeDelta = new Vector2(Radius * 2f + 24f, Radius * 2f + 24f);
            back.rectTransform.anchoredPosition = Vector2.zero;
            Image m = HudStyle.Image("Marker", ringRoot, new Color(1f, 0.85f, 0.4f, 1f), HudStyle.Circle());
            m.rectTransform.sizeDelta = new Vector2(24f, 24f);
            marker = m.rectTransform;
            foreach (Image i in new[] { back, m })
            {
                i.rectTransform.anchorMin = i.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                i.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }

            optionList = InventoryUIFactory.Rect("Options", panel.transform);
            var layout = optionList.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            InventoryUIFactory.Size(optionList, -1f, 200f, 1f);

            sealButton = InventoryUIFactory.Button("Seal", panel.transform, "Seal", Seal);
            sealLabel = sealButton.GetComponentInChildren<TextMeshProUGUI>();
            sealLabel.fontSize = 22f;
            InventoryUIFactory.Size(sealButton, -1f, 56f, 1f);

            footer = InventoryUIFactory.Text("Footer", panel.transform, "", 15f, InventoryUIFactory.MutedColor, TextAlignmentOptions.Center, FontStyles.Italic);
            InventoryUIFactory.Size(footer, -1f, 40f, 1f);

            RectTransform buttons = InventoryUIFactory.Rect("Buttons", panel.transform);
            InventoryUIFactory.Horizontal(buttons.gameObject, 12f);
            InventoryUIFactory.Size(buttons, -1f, 36f, 1f);
            giveUpButton = InventoryUIFactory.Button("Give Up", buttons, "Give up", GiveUp);
            closeButton = InventoryUIFactory.Button("Close", buttons, "Close", CloseResult);
            SocialUI.Show(group, false);
            canvas.gameObject.SetActive(false);
        }
    }
}
