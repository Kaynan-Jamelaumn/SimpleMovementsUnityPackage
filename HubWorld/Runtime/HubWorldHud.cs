using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HubWorld
{
    /// <summary>
    /// The hub world's on-screen information for this machine's player, built from code: the zone ("Safe Zone",
    /// "Dangerous Zone - PvP Active"), the time until the city jumps and the current / next dimension, large banners
    /// (warnings, the jump, zone changes, duel results), the duel challenge prompt (Accept [Y] / Decline [N]), and a veil
    /// while travelling between realms. Short messages go to the player's inventory message line when there is one.
    /// Added next to the <see cref="HubWorldDirector"/> automatically (turn off its Create Hud to use your own UI with
    /// <see cref="HubWorldMessages"/> and the director's events).
    /// </summary>
    [DisallowMultipleComponent]
    public class HubWorldHud : MonoBehaviour
    {
        [Tooltip("Seconds a banner stays fully visible.")]
        [Min(0.5f)] public float bannerSeconds = 3.5f;
        [Tooltip("Show the time until the jump in the corner.")]
        public bool showCycleTimer = true;
        [Tooltip("Show the current zone at the top of the screen.")]
        public bool showZone = true;

        private HubWorldDirector director;
        private Canvas canvas;
        private TextMeshProUGUI zoneText, timerText, dimensionText, bannerText, lineText, veilText, duelText;
        private CanvasGroup bannerGroup, lineGroup, veilGroup, duelGroup, flashGroup;
        private Image flashImage;
        private float bannerHideAt = -1f, lineHideAt = -1f, flashUntil = -1f;
        private GlobalInputBinding acceptInput, declineInput;

        private void Awake()
        {
            director = GetComponent<HubWorldDirector>();
            Build();
        }

        private void OnEnable()
        {
            HubWorldMessages.Sent += OnMessage;
            if (director != null)
                director.CycleJumped += OnJump;
        }

        private void OnDisable()
        {
            HubWorldMessages.Sent -= OnMessage;
            if (director != null)
                director.CycleJumped -= OnJump;
        }

        private void Start()
        {
            acceptInput = GlobalInputBinding.Create(new[] { "AcceptDuel" }, new[] { "<Keyboard>/y" }, "Accept Duel", false, true, this);
            declineInput = GlobalInputBinding.Create(new[] { "DeclineDuel" }, new[] { "<Keyboard>/n" }, "Decline Duel", false, true, this);
        }

        private void OnDestroy()
        {
            acceptInput?.Dispose();
            declineInput?.Dispose();
        }

        // ------------------------------------------------------------------ building

        private void Build()
        {
            var root = new GameObject("Hub World HUD", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            Transform t = root.transform;

            // Zone, top centre.
            zoneText = InventoryUIFactory.Text("Zone", t, "", 26f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(zoneText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(900f, 40f));

            // Cycle timer and dimension, top right.
            timerText = InventoryUIFactory.Text("Cycle Timer", t, "", 22f, InventoryUIFactory.TextColor, TextAlignmentOptions.Right, FontStyles.Bold);
            Place(timerText.rectTransform, new Vector2(1f, 1f), new Vector2(-28f, -22f), new Vector2(520f, 34f));
            dimensionText = InventoryUIFactory.Text("Dimension", t, "", 17f, InventoryUIFactory.MutedColor, TextAlignmentOptions.Right, FontStyles.Italic);
            Place(dimensionText.rectTransform, new Vector2(1f, 1f), new Vector2(-28f, -58f), new Vector2(520f, 52f));

            // Banner, upper middle.
            bannerText = InventoryUIFactory.Text("Banner", t, "", 40f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(bannerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 230f), new Vector2(1500f, 120f));
            bannerGroup = bannerText.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;

            // Fallback message line (when the player has no inventory to show it).
            lineText = InventoryUIFactory.Text("Message", t, "", 22f, InventoryUIFactory.TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(lineText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(1100f, 40f));
            lineGroup = lineText.gameObject.AddComponent<CanvasGroup>();
            lineGroup.alpha = 0f;

            // Duel prompt.
            Image duelPanel = InventoryUIFactory.Panel("Duel Prompt", t, InventoryUIFactory.PanelColor);
            Place(duelPanel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(560f, 120f));
            duelGroup = duelPanel.gameObject.AddComponent<CanvasGroup>();
            InventoryUIFactory.Vertical(duelPanel.gameObject, 14, 10f, false);
            duelText = InventoryUIFactory.Text("Text", duelPanel.transform, "", 20f, InventoryUIFactory.TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
            InventoryUIFactory.Size(duelText, -1f, 34f, 1f);
            RectTransform buttons = InventoryUIFactory.Rect("Buttons", duelPanel.transform);
            InventoryUIFactory.Horizontal(buttons.gameObject, 12f);
            InventoryUIFactory.Size(buttons, -1f, 36f, 1f);
            InventoryUIFactory.Button("Accept", buttons, "Accept  [Y]", AcceptDuel);
            InventoryUIFactory.Button("Decline", buttons, "Decline  [N]", DeclineDuel);
            SetGroup(duelGroup, false);

            // Veil while travelling.
            Image veil = InventoryUIFactory.Panel("Travel Veil", t, new Color(0.03f, 0.02f, 0.06f, 0.92f));
            InventoryUIFactory.Stretch(veil.rectTransform);
            veil.raycastTarget = false;
            veilGroup = veil.gameObject.AddComponent<CanvasGroup>();
            veilGroup.blocksRaycasts = false;
            veilGroup.alpha = 0f;
            veilText = InventoryUIFactory.Text("Text", veil.transform, "", 30f, new Color(0.85f, 0.8f, 1f), TextAlignmentOptions.Center, FontStyles.Italic);
            InventoryUIFactory.Stretch(veilText.rectTransform);

            // Flash at the jump.
            flashImage = InventoryUIFactory.Panel("Jump Flash", t, new Color(0.75f, 0.6f, 1f, 1f));
            InventoryUIFactory.Stretch(flashImage.rectTransform);
            flashImage.raycastTarget = false;
            flashGroup = flashImage.gameObject.AddComponent<CanvasGroup>();
            flashGroup.blocksRaycasts = false;
            flashGroup.alpha = 0f;

            bannerText.transform.SetAsLastSibling();
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        private static void SetGroup(CanvasGroup g, bool visible)
        {
            g.alpha = visible ? 1f : 0f;
            g.interactable = visible;
            g.blocksRaycasts = visible;
        }

        // ------------------------------------------------------------------ every frame

        private void Update()
        {
            // Seated at a card table: the zone and city messages step aside unless the inventory is open.
            if (canvas != null)
            {
                bool shown = !HudFocus.Hidden || InventoryOpen();
                if (canvas.enabled != shown) canvas.enabled = shown;
            }
            if (director == null)
                return;
            PlayerRealmState local = director.LocalState();
            UpdateZone(local);
            UpdateTimer(local);
            UpdateDuel(local);
            UpdateVeil(local);
            Fade(bannerGroup, bannerHideAt, 0.6f);
            Fade(lineGroup, lineHideAt, 0.4f);
            flashGroup.alpha = flashUntil > Time.unscaledTime ? Mathf.Clamp01((flashUntil - Time.unscaledTime) / 1.5f) * 0.85f : 0f;
        }

        private static bool InventoryOpen()
        {
            CombatEntity me = PlayerLocator.Local;
            return me != null && InventoryManager.IsInventoryOpenFor(me);
        }

        private void UpdateZone(PlayerRealmState local)
        {
            if (!showZone || local == null || string.IsNullOrEmpty(local.Zone.name))
            {
                zoneText.text = "";
                return;
            }
            ZoneInfo z = local.Zone;
            string label = z.name;
            if (local.InWorld && z.pvp)
                label += "  <color=#FF6A50>- PvP Active</color>";
            else if (local.InDungeon)
                label += $"  <size=70%>(difficulty {z.dungeonDifficulty:0.0})</size>";
            if (CyclePenalties.IsDisoriented(local.Entity))
                label += $"\n<size=60%><color=#B9A2FF>{director.Settings.disorientation.label}</color></size>";
            zoneText.text = label;
            zoneText.color = z.color.a > 0f ? z.color : Color.white;
        }

        private void UpdateTimer(PlayerRealmState local)
        {
            if (!showCycleTimer)
            {
                timerText.text = dimensionText.text = "";
                return;
            }
            double left = director.SecondsUntilJump;
            bool urgent = left <= 60d;
            timerText.text = director.IsJumping ? "The city is jumping..." : $"Next jump in {HubWorldMessages.FormatTime(left)}";
            timerText.color = urgent ? new Color(1f, 0.45f, 0.4f) : left <= 600d ? new Color(1f, 0.85f, 0.45f) : InventoryUIFactory.TextColor;
            string now = director.CurrentModifier != null ? $"Dimension: {director.CurrentModifier.displayName}" : "";
            // The next dimension is announced from the first warning on (or always while in the city).
            bool announce = director.NextModifier != null && (local == null || local.InCity ||
                            (director.Clock.Warnings.Count > 0 && left <= director.Clock.Warnings[0]));
            string next = announce ? $"Next: {director.NextModifier.displayName}" : "";
            dimensionText.text = string.IsNullOrEmpty(now) ? next : string.IsNullOrEmpty(next) ? now : now + "\n" + next;
        }

        private void UpdateDuel(PlayerRealmState local)
        {
            DuelSystem duels = DuelSystem.Instance;
            CombatEntity me = local != null ? local.Entity : HubWorldDirector.LocalPlayer;
            Duel d = duels != null && me != null ? duels.DuelOf(me) : null;
            bool asked = d != null && d.State == DuelState.Challenged && d.Challenged == PlayerIds.Of(me);
            if (asked)
            {
                duelText.text = $"{DuelSystem.DisplayName(duels.PlayerOf(d.Challenger))} challenges you to a duel ({Mathf.CeilToInt((float)(d.Deadline - director.Now))}s)";
                if (acceptInput != null && acceptInput.Pressed)
                    AcceptDuel();
                else if (declineInput != null && declineInput.Pressed)
                    DeclineDuel();
            }
            if ((duelGroup.alpha > 0.5f) != asked)
                SetGroup(duelGroup, asked);
            if (d != null && d.State == DuelState.Countdown)
            {
                int n = Mathf.CeilToInt((float)(d.Deadline - director.Now));
                if (n > 0)
                    ShowBanner(n.ToString(), new Color(1f, 0.85f, 0.4f), 0.4f);
            }
        }

        private void UpdateVeil(PlayerRealmState local)
        {
            // A network client also keeps the veil while the server holds the player for the destination to load here.
            bool travelling = local != null && (local.InTransit || local.AwaitingArrival);
            float target = travelling ? 1f : 0f;
            veilGroup.alpha = Mathf.MoveTowards(veilGroup.alpha, target, Time.unscaledDeltaTime * (travelling ? 4f : 1.5f));
            if (travelling)
            {
                RealmKind heading = local.InTransit ? local.Destination : local.Realm;
                veilText.text = heading == RealmKind.World ? "Stepping out of the city..." : heading == RealmKind.City ? "Returning to the city..." : "Between dimensions...";
            }
        }

        private static void Fade(CanvasGroup g, float hideAt, float fadeSeconds)
        {
            if (g.alpha <= 0f)
                return;
            float left = hideAt - Time.unscaledTime;
            g.alpha = left >= 0f ? 1f : Mathf.Clamp01(1f + left / fadeSeconds);
        }

        // ------------------------------------------------------------------ events

        private void OnMessage(CombatEntity to, string text, HubMessageKind kind)
        {
            if (!HubWorldMessages.IsForLocal(to))
                return;
            if (kind == HubMessageKind.Banner)
            {
                ShowBanner(text, Color.white, bannerSeconds);
                return;
            }
            CombatEntity local = to != null ? to : HubWorldDirector.LocalPlayer;
            InventoryManager inventory = local != null ? InventoryManager.For(local) : null;
            if (inventory != null)
            {
                inventory.ShowMessage(text, kind == HubMessageKind.Warning ? InventoryFeedback.Kind.Warning : InventoryFeedback.Kind.Info);
                return;
            }
            lineText.text = text;
            lineText.color = kind == HubMessageKind.Warning ? new Color(1f, 0.85f, 0.45f) : InventoryUIFactory.TextColor;
            lineHideAt = Time.unscaledTime + 3f;
            lineGroup.alpha = 1f;
        }

        private void ShowBanner(string text, Color color, float seconds)
        {
            bannerText.text = text;
            bannerText.color = color;
            bannerHideAt = Time.unscaledTime + seconds;
            bannerGroup.alpha = 1f;
        }

        private void OnJump(int cycle) => flashUntil = Time.unscaledTime + 1.5f;

        private void AcceptDuel()
        {
            CombatEntity me = HubWorldDirector.LocalPlayer;
            if (DuelSystem.Instance != null && me != null)
                DuelSystem.Instance.Accept(me);
        }

        private void DeclineDuel()
        {
            CombatEntity me = HubWorldDirector.LocalPlayer;
            if (DuelSystem.Instance != null && me != null)
                DuelSystem.Instance.Decline(me);
        }
    }
}
