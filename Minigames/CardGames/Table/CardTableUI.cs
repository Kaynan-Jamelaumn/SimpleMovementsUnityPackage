using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The window of the card table the local player sits at (built from code, one per machine): the scores and what the
/// hand is worth on top, the table in the middle (each seat's name, cards and speech bubble, the played cards with the
/// trick's best one lit, the vira), the player's hand at the bottom (click a card to play it), the buttons on the right
/// (Ready, the stake, Truco / Accept / Raise / Fold, Leave) and the log on the left. It is an open panel of the
/// player's inventory while shown: the cursor is free and clicks never attack. Escape asks before forfeiting a game.
/// </summary>
public class CardTableUI : MonoBehaviour, IGameplayPanel
{
    private static CardTableUI instance;

    private CardTable table;
    private InventoryManager registeredWith;
    private bool managesCursor;
    private CursorLockMode previousLock;
    private bool previousVisible;
    private int shownVersion = int.MinValue;
    private bool confirmLeave;
    private string shownHand, shownActions;

    private Canvas canvas;
    private TextMeshProUGUI title, stake, scores, value, status, logText, hint;
    private RectTransform tableArea, handRow, actionColumn, confirmBox;
    private readonly List<GameObject> dynamicTable = new List<GameObject>();

    private static readonly Color Felt = new Color(0.07f, 0.24f, 0.15f, 0.82f);
    private static readonly Color CardFace = new Color(0.97f, 0.96f, 0.92f, 1f);
    private static readonly Color CardBack = new Color(0.55f, 0.1f, 0.12f, 1f);
    private static readonly Color CardLit = new Color(1f, 0.9f, 0.55f, 1f);

    public string PanelName => table != null ? "Card table: " + table.name : "Card table";

    /// <summary>Shows the window for <paramref name="t"/> (the local player sits there). Called every frame by the table.</summary>
    public static void Show(CardTable t)
    {
        if (t == null)
            return;
        if (instance == null)
        {
            InventoryEventSystems.EnsureSingle();
            var go = new GameObject("Card Table UI");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CardTableUI>();
            instance.Build();
        }
        if (instance.table != t)
        {
            instance.table = t;
            instance.shownVersion = int.MinValue;
            instance.confirmLeave = false;
            instance.shownHand = instance.shownActions = null;
        }
        if (!instance.canvas.gameObject.activeSelf)
            instance.canvas.gameObject.SetActive(true);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private void Update()
    {
        if (table == null || table.LocalSeat < 0)
        {
            Hide();
            return;
        }
        Register();
        if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        int version = table.ViewVersion;
        if (version != shownVersion)
        {
            shownVersion = version;
            Refresh(table.LocalView);
        }
        HotKeys(table.LocalView);
    }

    /// <summary>Number keys press the buttons (1 = the first): handy to mash Push in arm wrestling.</summary>
    private void HotKeys(CardTableView v)
    {
        UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null || v == null || confirmLeave)
            return;
        for (int i = 0; i < 9 && i < v.actions.Count; i++)
        {
            if (!kb[UnityEngine.InputSystem.Key.Digit1 + i].wasPressedThisFrame) continue;
            CardActionOption a = v.actions[i];
            if (a.enabled && a.action.kind != CardTableActions.Leave)
                table.RequestAction(a.action);
            return;
        }
    }

    private void Hide()
    {
        Unregister();
        table = null;
        shownHand = shownActions = null;
        confirmLeave = false;
        if (canvas != null && canvas.gameObject.activeSelf)
            canvas.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        Unregister();
        if (instance == this)
            instance = null;
    }

    // ------------------------------------------------------------------ panel registration
    private void Register()
    {
        if (registeredWith != null || managesCursor)
            return;
        CombatEntity me = LocalPlayer();
        InventoryManager inv = me != null ? InventoryManager.For(me) : null;
        if (inv != null)
        {
            registeredWith = inv;
            inv.RegisterPanel(this);
        }
        else
        {
            managesCursor = true;
            previousLock = Cursor.lockState;
            previousVisible = Cursor.visible;
        }
    }

    private void Unregister()
    {
        if (registeredWith != null)
        {
            InventoryManager inv = registeredWith;
            registeredWith = null;
            inv.UnregisterPanel(this);
        }
        if (managesCursor)
        {
            managesCursor = false;
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
        }
    }

    /// <summary>Escape or the pause menu: leaving the lobby at once; during a game, asks first (leaving forfeits it).</summary>
    public void ClosePanel()
    {
        Unregister(); // registered again next frame while the player stays seated
        if (table == null)
            return;
        CardTableView v = table.LocalView;
        if (v != null && v.Playing && !confirmLeave)
        {
            confirmLeave = true;
            shownVersion = int.MinValue;
            return;
        }
        table.RequestAction(new CardAction(CardTableActions.Leave));
    }

    private static CombatEntity LocalPlayer()
    {
        IReadOnlyList<CombatEntity> players = CombatEntity.Players;
        for (int i = 0; i < players.Count; i++)
            if (CardTable.IsLocalPlayer(players[i]))
                return players[i];
        return null;
    }

    // ------------------------------------------------------------------ building
    private void Build()
    {
        canvas = UITheme.Canvas("Card Table", 45, transform);
        Transform c = canvas.transform;

        // Top: title, stake, scores, value of the hand.
        Image top = UITheme.Panel("Top", c, UITheme.Surface, UITheme.RadiusLarge);
        UITheme.Place(top.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-520f, -150f), new Vector2(520f, -16f));
        RectTransform topCol = UITheme.Column("Column", top.transform, 14, 4f);
        InventoryUIFactory.Stretch(topCol);
        title = UITheme.Label(topCol, "", 26f, UITheme.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
        scores = UITheme.Label(topCol, "", 24f, UITheme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
        value = UITheme.Label(topCol, "", 16f, UITheme.Muted, TextAlignmentOptions.Center);
        stake = UITheme.Label(topCol, "", 15f, UITheme.Accent, TextAlignmentOptions.Center);

        // Middle: the table.
        Image felt = UITheme.Panel("Table", c, Felt, 48);
        tableArea = felt.rectTransform;
        UITheme.Place(tableArea, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-420f, -190f), new Vector2(420f, 230f));

        // Status line above the hand.
        status = UITheme.Label(c, "", 22f, UITheme.Text, TextAlignmentOptions.Center, FontStyles.Bold, "Status");
        UITheme.Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-640f, 238f), new Vector2(640f, 280f));

        // Bottom: the hand.
        handRow = UITheme.Area("Hand", c, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-300f, 40f), new Vector2(300f, 230f));
        HorizontalLayoutGroup h = InventoryUIFactory.Horizontal(handRow.gameObject, 18f);
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childForceExpandWidth = false;
        h.childControlWidth = false;
        h.childControlHeight = false;

        // Right: the buttons.
        Image right = UITheme.Panel("Actions", c, UITheme.Surface, UITheme.RadiusLarge);
        UITheme.Place(right.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-380f, -260f), new Vector2(-24f, 300f));
        RectTransform rightCol = UITheme.Column("Column", right.transform, 16, 10f);
        InventoryUIFactory.Stretch(rightCol);
        UITheme.Header(rightCol, "Actions");
        actionColumn = UITheme.Column("Buttons", rightCol, 0, 8f, true);
        hint = UITheme.Label(rightCol, "", 14f, UITheme.Muted);
        InventoryUIFactory.Size(hint, -1f, 60f);

        // Left: the log.
        Image left = UITheme.Panel("Log", c, UITheme.Surface, UITheme.RadiusLarge);
        UITheme.Place(left.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, -260f), new Vector2(400f, 300f));
        RectTransform leftCol = UITheme.Column("Column", left.transform, 16, 6f);
        InventoryUIFactory.Stretch(leftCol);
        UITheme.Header(leftCol, "Table");
        logText = UITheme.Label(leftCol, "", 15f, UITheme.Muted, TextAlignmentOptions.BottomLeft);
        InventoryUIFactory.Size(logText, -1f, 470f);

        // The "leave and forfeit?" box.
        Image confirm = UITheme.Window("Confirm", c, new Vector2(560f, 210f));
        confirmBox = confirm.rectTransform;
        RectTransform cc = UITheme.Column("Column", confirm.transform, 22, 14f);
        InventoryUIFactory.Stretch(cc);
        UITheme.Label(cc, "Leave the game? Your team forfeits it (and loses the stake).", 19f, UITheme.Text, TextAlignmentOptions.Center);
        RectTransform row = UITheme.Row("Buttons", cc, 48f);
        UITheme.Button("Stay", row, "Stay", () => { confirmLeave = false; shownVersion = int.MinValue; }, UITheme.ButtonStyle.Primary);
        UITheme.Button("Leave", row, "Leave and forfeit", () =>
        {
            confirmLeave = false;
            table?.RequestAction(new CardAction(CardTableActions.Leave));
        }, UITheme.ButtonStyle.Danger);
        confirmBox.gameObject.SetActive(false);
        canvas.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ showing a view
    private void Refresh(CardTableView v)
    {
        if (v == null)
            return;
        title.text = v.title;
        stake.text = "Stake: " + v.stakeText;
        value.text = v.valueText;
        status.text = v.status;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < v.teamNames.Count; i++)
        {
            if (string.IsNullOrEmpty(v.teamNames[i])) continue; // an empty seat of a game where everyone plays alone
            if (sb.Length > 0) sb.Append("   —   ");
            bool mine = v.viewerSeat >= 0 && v.Seat(v.viewerSeat) != null && v.Seat(v.viewerSeat).team == i;
            string col = mine ? "#8fd68f" : "#f0a090";
            sb.Append($"<color={col}>{v.teamNames[i]}{(mine ? " (you)" : "")}</color>  <b>{(i < v.teamScores.Count ? v.teamScores[i] : 0)}</b>");
        }
        scores.text = sb.ToString();
        scores.fontSize = v.teamNames.Count > 2 ? 17f : 24f;
        int from = Mathf.Max(0, v.log.Count - 16);
        logText.text = string.Join("\n", v.log.GetRange(from, v.log.Count - from));

        BuildTable(v);
        // Buttons are only rebuilt when they change (real-time games change the view every frame: a button rebuilt under
        // the mouse would lose the click).
        string hand = HandSignature(v), acts = ActionSignature(v);
        if (hand != shownHand) { shownHand = hand; BuildHand(v); }
        if (acts != shownActions) { shownActions = acts; BuildActions(v); }
        confirmBox.gameObject.SetActive(confirmLeave);
        confirmBox.SetAsLastSibling();
    }

    /// <summary>The viewer at the bottom, the next seats clockwise to the left, across and to the right.</summary>
    private static Vector2 SeatDirection(int seat, int viewer, int count)
    {
        int rel = ((seat - Mathf.Max(0, viewer)) % count + count) % count;
        float angle = -90f - rel * 360f / count; // bottom, then clockwise on screen (left, top, right)
        float rad = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private void BuildTable(CardTableView v)
    {
        foreach (GameObject g in dynamicTable) if (g != null) Destroy(g);
        dynamicTable.Clear();
        int n = v.seats.Count;
        Vector2 half = new Vector2(420f, 210f);
        for (int s = 0; s < n; s++)
        {
            CardSeatView sv = v.seats[s];
            Vector2 dir = SeatDirection(s, v.viewerSeat, n);
            // The seat's label at the table's edge.
            string marks = (sv.isTurn ? "<color=#ffd166>▶ </color>" : "") + (v.tablePhase == 0 && sv.occupied && !sv.bot ? (sv.ready ? "<color=#8fd68f>✔ </color>" : "<color=#9aa0a6>… </color>") : "");
            string cards = v.tablePhase != 0 && sv.occupied ? $"  <color=#9aa0a6>({sv.cardCount})</color>" : "";
            string team = v.teamNames.Count > sv.team && sv.team >= 0 ? v.teamNames[sv.team] : "";
            TextMeshProUGUI label = UITheme.Label(tableArea, $"{marks}<b>{sv.name}</b>{cards}\n<size=13><color=#b8c0cc>{team}</color></size>",
                17f, sv.occupied ? UITheme.Text : UITheme.Muted, TextAlignmentOptions.Center, FontStyles.Normal, "Seat " + s);
            Vector2 at = new Vector2(dir.x * (half.x - 90f), dir.y * (half.y - 30f) + 20f);
            Center(label.rectTransform, at, new Vector2(220f, 52f));
            dynamicTable.Add(label.gameObject);
            if (!string.IsNullOrEmpty(sv.bubble))
            {
                Image bubble = UITheme.Panel("Bubble", tableArea, UITheme.Gold, UITheme.Radius);
                Center(bubble.rectTransform, at + new Vector2(0f, 46f), new Vector2(170f, 36f));
                TextMeshProUGUI bt = UITheme.Label(bubble.transform, sv.bubble, 18f, new Color(0.15f, 0.1f, 0.02f), TextAlignmentOptions.Center, FontStyles.Bold);
                InventoryUIFactory.Stretch(bt.rectTransform);
                dynamicTable.Add(bubble.gameObject);
            }
        }
        // A row of shared cards in the middle (poker's board, the dealer's hand) pushes the played cards out to the seats;
        // one or two (Truco's vira) lie in a corner.
        bool board = v.shared.Count >= 2;
        Vector2 radius = board ? new Vector2(250f, 118f) : new Vector2(120f, 62f);
        var perSeat = new Dictionary<int, int>();
        var shown = new Dictionary<int, int>();
        foreach (CardPlayView p in v.played)
            perSeat[p.seat] = perSeat.TryGetValue(p.seat, out int c) ? c + 1 : 1;
        Vector2 size = board ? new Vector2(58f, 84f) : new Vector2(78f, 112f);
        foreach (CardPlayView p in v.played)
        {
            Vector2 dir = SeatDirection(p.seat, v.viewerSeat, n);
            int k = shown.TryGetValue(p.seat, out int done) ? done : 0;
            shown[p.seat] = k + 1;
            float spread = (k - (perSeat[p.seat] - 1) * 0.5f) * (size.x * 0.55f);
            Vector2 along = new Vector2(-dir.y, dir.x);
            GameObject card = CardWidget(tableArea, p.card, size, p.best);
            Center((RectTransform)card.transform, new Vector2(dir.x * radius.x, dir.y * radius.y + 20f) + along * spread, size);
            dynamicTable.Add(card);
        }
        if (v.tablePhase != 0 && v.shared.Count > 0)
        {
            for (int i = 0; i < v.shared.Count; i++)
            {
                GameObject card = CardWidget(tableArea, v.shared[i], new Vector2(64f, 92f), false);
                Vector2 at = board ? new Vector2((i - (v.shared.Count - 1) * 0.5f) * 70f, 30f) : new Vector2(300f + i * 70f, -120f);
                Center((RectTransform)card.transform, at, new Vector2(64f, 92f));
                dynamicTable.Add(card);
            }
            if (v.deckCount > 0)
            {
                GameObject deck = CardWidget(tableArea, PlayingCard.Hidden, new Vector2(64f, 92f), false);
                Center((RectTransform)deck.transform, board ? new Vector2(-340f, -140f) : new Vector2(-300f, -120f), new Vector2(64f, 92f));
                dynamicTable.Add(deck);
            }
            TextMeshProUGUI shared = UITheme.Label(tableArea, v.sharedLabel, 15f, UITheme.Gold, TextAlignmentOptions.Center);
            Center(shared.rectTransform, board ? new Vector2(0f, -30f) : new Vector2(300f, -184f), new Vector2(board ? 420f : 240f, 24f));
            dynamicTable.Add(shared.gameObject);
        }
        BuildMeters(v);
    }

    private static readonly Color MeterBack = new Color(0f, 0f, 0f, 0.45f);

    /// <summary>The game's bars (arm wrestling's arms, a drinking timer, how drunk everyone is) in the middle of the table.</summary>
    private void BuildMeters(CardTableView v)
    {
        int count = v.meters.Count;
        for (int i = 0; i < count; i++)
        {
            CardMeter m = v.meters[i];
            float y = 70f - i * 46f + (count > 3 ? (count - 3) * 23f : 0f);
            TextMeshProUGUI label = UITheme.Label(tableArea, m.label, 15f, UITheme.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            Center(label.rectTransform, new Vector2(-120f, y + 15f), new Vector2(480f, 20f));
            dynamicTable.Add(label.gameObject);
            Image back = UITheme.Panel("Meter", tableArea, MeterBack, 6);
            Center(back.rectTransform, new Vector2(0f, y - 6f), new Vector2(520f, 16f));
            dynamicTable.Add(back.gameObject);
            Color c = m.color == 1 ? UITheme.Good : m.color == 2 ? UITheme.Bad : m.color == 3 ? UITheme.Gold : UITheme.Accent;
            Image fill = UITheme.Panel("Fill", back.transform, c, 6);
            RectTransform fr = fill.rectTransform;
            if (m.centred)
            {
                float v01 = Mathf.Clamp(m.value, -1f, 1f);
                fr.anchorMin = new Vector2(Mathf.Min(0.5f, 0.5f + v01 * 0.5f), 0f);
                fr.anchorMax = new Vector2(Mathf.Max(0.5f, 0.5f + v01 * 0.5f), 1f);
                Image mid = UITheme.Panel("Middle", back.transform, Color.white, 0);
                mid.rectTransform.anchorMin = new Vector2(0.5f, 0f);
                mid.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                mid.rectTransform.offsetMin = new Vector2(-1.5f, -4f);
                mid.rectTransform.offsetMax = new Vector2(1.5f, 4f);
            }
            else
            {
                fr.anchorMin = Vector2.zero;
                fr.anchorMax = new Vector2(Mathf.Clamp01(m.value), 1f);
            }
            fr.offsetMin = fr.offsetMax = Vector2.zero;
        }
    }

    private static string HandSignature(CardTableView v)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < v.hand.Count; i++)
            sb.Append(v.hand[i]).Append(i < v.handPlayable.Count && v.handPlayable[i] ? '+' : '-');
        return sb.ToString();
    }

    private static string ActionSignature(CardTableView v)
    {
        var sb = new System.Text.StringBuilder();
        foreach (CardActionOption a in v.actions)
            sb.Append(a.action.kind).Append(':').Append(a.action.arg).Append(a.label).Append(a.enabled ? '+' : '-').Append(a.style).Append('|');
        return sb.ToString();
    }

    private void BuildHand(CardTableView v)
    {
        for (int i = handRow.childCount - 1; i >= 0; i--)
            Destroy(handRow.GetChild(i).gameObject);
        for (int i = 0; i < v.hand.Count; i++)
        {
            int index = i;
            bool playable = i < v.handPlayable.Count && v.handPlayable[i];
            GameObject card = CardWidget(handRow, v.hand[i], new Vector2(118f, 170f), false);
            var b = card.AddComponent<Button>();
            b.targetGraphic = card.GetComponent<Image>();
            b.interactable = playable;
            ColorBlock cb = b.colors;
            cb.highlightedColor = new Color(1f, 0.95f, 0.75f);
            cb.disabledColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            b.colors = cb;
            b.onClick.AddListener(() => table?.RequestAction(new CardAction(CardGameSession.PlayCard, index)));
            InventoryUIFactory.Size(card.GetComponent<Image>(), 118f, 170f);
        }
    }

    private void BuildActions(CardTableView v)
    {
        for (int i = actionColumn.childCount - 1; i >= 0; i--)
            Destroy(actionColumn.GetChild(i).gameObject);
        string why = "";
        int number = 0;
        foreach (CardActionOption a in v.actions)
        {
            number++;
            CardAction act = a.action;
            UITheme.ButtonStyle style = a.style == 1 ? UITheme.ButtonStyle.Primary : a.style == 2 ? UITheme.ButtonStyle.Danger : UITheme.ButtonStyle.Normal;
            string label = number <= 9 && act.kind != CardTableActions.Leave ? $"{a.label}  <color=#9aa0a6>[{number}]</color>" : a.label;
            Button b = UITheme.Button("Action", actionColumn, label, () =>
            {
                if (act.kind == CardTableActions.Leave && table != null && table.LocalView != null && table.LocalView.Playing)
                {
                    confirmLeave = true;
                    shownVersion = int.MinValue;
                    return;
                }
                table?.RequestAction(act);
            }, style, 0f, 46f);
            b.interactable = a.enabled;
            if (!a.enabled && !string.IsNullOrEmpty(a.reason) && a.style == 1)
                why = a.reason;
        }
        hint.text = why;
    }

    // ------------------------------------------------------------------ widgets
    private static void Center(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    /// <summary>A card: its rank and suit on a light face (a face-down card is plain red).</summary>
    private static GameObject CardWidget(Transform parent, byte code, Vector2 size, bool lit)
    {
        bool face = PlayingCard.IsCard(code);
        Image img = UITheme.Panel("Card", parent, face ? (lit ? CardLit : CardFace) : CardBack, 10);
        img.rectTransform.sizeDelta = size;
        if (face)
        {
            PlayingCard c = PlayingCard.FromCode(code);
            Color ink = c.IsRed ? new Color(0.78f, 0.1f, 0.12f) : new Color(0.1f, 0.1f, 0.12f);
            TextMeshProUGUI corner = UITheme.Label(img.transform, c.RankLabel + "\n" + PlayingCard.SuitSymbol(c.suit), size.x * 0.22f, ink, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            InventoryUIFactory.Stretch(corner.rectTransform, 8f, 6f);
            TextMeshProUGUI middle = UITheme.Label(img.transform, PlayingCard.SuitSymbol(c.suit), size.x * 0.5f, ink, TextAlignmentOptions.Center);
            InventoryUIFactory.Stretch(middle.rectTransform);
            corner.raycastTarget = middle.raycastTarget = false;
        }
        else
        {
            Image inner = UITheme.Panel("Pattern", img.transform, new Color(1f, 1f, 1f, 0.12f), 6);
            InventoryUIFactory.Stretch(inner.rectTransform, 7f, 7f);
            inner.raycastTarget = false;
        }
        return img.gameObject;
    }
}
