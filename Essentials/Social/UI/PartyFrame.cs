using System.Collections.Generic;
using HubWorld;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The party on screen: every member with their health (the leader marked ★; click a member to target them), the
/// "X invites you to a party" prompt with Accept / Decline (also the Accept / Decline Party keys, Y and N by default),
/// and, in scenes without the Hub World HUD, a line showing party and targeting messages.
/// </summary>
[DisallowMultipleComponent]
public class PartyFrame : MonoBehaviour
{
    [Tooltip("Where the party list sits: from the top-left corner of the screen (reference 1920×1080).")]
    public Vector2 position = new Vector2(24f, -300f);
    [SerializeField] private string[] acceptControls = { "<Keyboard>/y" };
    [SerializeField] private string[] declineControls = { "<Keyboard>/n" };

    private sealed class Row
    {
        public GameObject root;
        public TextMeshProUGUI name;
        public Image health;
        public TextMeshProUGUI healthText;
        public CombatEntity entity;
        public int id;
    }

    private RectTransform list, prompt;
    private CanvasGroup listGroup, promptGroup, toastGroup;
    private TextMeshProUGUI header, promptText, toastText;
    private readonly List<Row> rows = new List<Row>();
    private readonly List<CombatEntity> members = new List<CombatEntity>();
    private GlobalInputBinding acceptInput, declineInput;
    private bool dirty = true;
    private float toastUntil, nextHudCheck, nextRefresh;
    private bool hubHudPresent;
    private PartySystem parties;

    private void Start()
    {
        parties = PartySystem.Instance;
        parties.Changed += MarkDirty;
        acceptInput = GlobalInputBinding.Create(new[] { "AcceptParty" }, acceptControls, "Accept Party Invite", false, true, this);
        declineInput = GlobalInputBinding.Create(new[] { "DeclineParty" }, declineControls, "Decline Party Invite", false, true, this);
        HubWorldMessages.Sent += OnMessage;
        PlayerActions.Feedback += Toast;
        Build();
    }

    private void OnDestroy()
    {
        if (parties != null)
            parties.Changed -= MarkDirty;
        HubWorldMessages.Sent -= OnMessage;
        PlayerActions.Feedback -= Toast;
        acceptInput?.Dispose();
        declineInput?.Dispose();
        if (list != null) Destroy(list.gameObject);
        if (prompt != null) Destroy(prompt.gameObject);
        if (toastText != null) Destroy(toastText.gameObject);
    }

    private void MarkDirty() => dirty = true;

    // ------------------------------------------------------------------ messages

    /// <summary>Party messages for this machine's player, when no Hub World HUD shows them.</summary>
    private void OnMessage(CombatEntity to, string text, HubMessageKind kind)
    {
        if (!HubWorldMessages.IsForLocal(to) || HubHudPresent())
            return;
        Toast(text);
    }

    private void Toast(string text)
    {
        if (toastText == null || string.IsNullOrWhiteSpace(text))
            return;
        // With the Hub World HUD, feedback goes there (PlayerActions.Say falls back to it when nobody listens).
        if (HubHudPresent())
        {
            HubWorldMessages.DeliverLocal(PlayerLocator.Local, text, HubMessageKind.Warning);
            return;
        }
        toastText.text = text;
        toastUntil = Time.unscaledTime + 4f;
    }

    private bool HubHudPresent()
    {
        if (Time.unscaledTime >= nextHudCheck)
        {
            nextHudCheck = Time.unscaledTime + 2f;
#if UNITY_6000_4_OR_NEWER
            hubHudPresent = FindAnyObjectByType<HubWorldHud>() != null;
#else
            hubHudPresent = FindAnyObjectByType<HubWorldHud>(FindObjectsInactive.Exclude) != null;
#endif
        }
        return hubHudPresent;
    }

    // ------------------------------------------------------------------ every frame

    private void Update()
    {
        CombatEntity me = PlayerLocator.Local;
        if (toastGroup != null)
            toastGroup.alpha = Mathf.Clamp01((toastUntil - Time.unscaledTime) / 0.5f);
        if (me == null || parties == null)
        {
            SocialUI.Show(listGroup, false);
            SocialUI.Show(promptGroup, false);
            return;
        }

        // The invitation prompt.
        bool invited = parties.TryGetInvite(me, out CombatEntity from, out float left);
        SocialUI.Show(promptGroup, invited);
        if (invited)
        {
            promptText.text = $"<b>{PartySystem.NameOf(from)}</b> invites you to a party  <color=#9aa4b5>({Mathf.CeilToInt(left)} s)</color>";
            // The keys answer a party invitation only when no duel challenge is waiting for the same keys.
            bool duelWaiting = DuelSystem.Instance != null && DuelSystem.Instance.DuelOf(me) != null;
            if (!duelWaiting && acceptInput != null && acceptInput.Pressed)
                parties.Accept(me);
            else if (!duelWaiting && declineInput != null && declineInput.Pressed)
                parties.Decline(me);
        }

        if (dirty || Time.unscaledTime >= nextRefresh)
            RebuildList(me);
        for (int i = 0; i < rows.Count; i++)
        {
            Row r = rows[i];
            CombatEntity e = r.entity;
            bool here = e != null && e.isActiveAndEnabled;
            float ratio = here ? e.HealthRatio : 0f;
            r.health.fillAmount = ratio;
            r.health.color = here ? Color.Lerp(new Color(0.85f, 0.15f, 0.1f), new Color(0.2f, 0.8f, 0.3f), ratio) : new Color(0.4f, 0.4f, 0.4f);
            r.healthText.text = !here ? "away" : e.IsDead ? "dead" : "";
        }
    }

    private void RebuildList(CombatEntity me)
    {
        dirty = false;
        nextRefresh = Time.unscaledTime + 1f;
        Party party = parties.PartyOf(me);
        SocialUI.Show(listGroup, party != null);
        if (party == null)
        {
            ClearRows();
            return;
        }
        header.text = $"Party  <color=#9aa4b5>{party.Count}/{parties.Rules.maxMembers}</color>";
        bool same = rows.Count == party.Count;
        for (int i = 0; same && i < rows.Count; i++)
            same = rows[i].id == party.Members[i] && rows[i].entity == PlayerIds.Find(party.Members[i]);
        if (!same)
        {
            ClearRows();
            foreach (int id in party.Members)
                rows.Add(MakeRow(id));
        }
        foreach (Row r in rows)
        {
            bool leader = r.id == party.Leader;
            bool self = r.entity != null && r.entity == me;
            r.name.text = (leader ? "<color=#ffd34d>★</color> " : "") + PartySystem.NameOf(r.id) + (self ? " <color=#9aa4b5>(you)</color>" : "");
        }
    }

    private void ClearRows()
    {
        foreach (Row r in rows)
            if (r.root != null)
            {
                r.root.SetActive(false);
                Destroy(r.root);
            }
        rows.Clear();
    }

    private Row MakeRow(int id)
    {
        var row = new Row { id = id, entity = PlayerIds.Find(id) };
        Image bg = InventoryUIFactory.Panel("Member", list, new Color(1f, 1f, 1f, 0.05f));
        InventoryUIFactory.Size(bg, -1f, 46f, 1f);
        InventoryUIFactory.Vertical(bg.gameObject, 4, 2f, false);
        row.root = bg.gameObject;
        row.name = InventoryUIFactory.Text("Name", bg.transform, "", 16f, InventoryUIFactory.TextColor, TextAlignmentOptions.Left, FontStyles.Bold);
        row.name.richText = true;
        row.name.textWrappingMode = TextWrappingModes.NoWrap;
        row.name.overflowMode = TextOverflowModes.Ellipsis;
        InventoryUIFactory.Size(row.name, -1f, 20f, 1f);
        row.health = SocialUI.Bar("Health", bg.transform, new Color(0.2f, 0.8f, 0.3f), 14f, out row.healthText);
        Button b = bg.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        b.onClick.AddListener(() =>
        {
            CombatEntity e = PlayerIds.Find(row.id);
            if (e != null && e != PlayerLocator.Local)
                PlayerTargeting.Instance?.SetTarget(e);
        });
        return row;
    }

    // ------------------------------------------------------------------ building

    private void Build()
    {
        Transform root = SocialUI.Canvas.transform;

        Image panel = InventoryUIFactory.Panel("Party Frame", root, new Color(0.06f, 0.07f, 0.09f, 0.8f));
        list = panel.rectTransform;
        SocialUI.Place(list, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(240f, 0f));
        InventoryUIFactory.Vertical(panel.gameObject, 8, 4f, true);
        listGroup = panel.gameObject.AddComponent<CanvasGroup>();
        header = InventoryUIFactory.Text("Header", panel.transform, "Party", 16f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        header.richText = true;
        InventoryUIFactory.Size(header, -1f, 22f, 1f);
        SocialUI.Show(listGroup, false);
        // Rows go after the header, in the same panel.
        list = panel.rectTransform;

        Image ask = InventoryUIFactory.Panel("Party Invite", root, InventoryUIFactory.PanelColor);
        prompt = ask.rectTransform;
        SocialUI.Place(prompt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(560f, 100f));
        InventoryUIFactory.Vertical(ask.gameObject, 12, 8f, false);
        promptGroup = ask.gameObject.AddComponent<CanvasGroup>();
        promptText = InventoryUIFactory.Text("Text", ask.transform, "", 19f, InventoryUIFactory.TextColor, TextAlignmentOptions.Center);
        promptText.richText = true;
        InventoryUIFactory.Size(promptText, -1f, 30f, 1f);
        RectTransform buttons = InventoryUIFactory.Rect("Buttons", ask.transform);
        InventoryUIFactory.Horizontal(buttons.gameObject, 12f);
        InventoryUIFactory.Size(buttons, -1f, 34f, 1f);
        InventoryUIFactory.Button("Accept", buttons, "Accept (Y)", () => parties.Accept(PlayerLocator.Local));
        InventoryUIFactory.Button("Decline", buttons, "Decline (N)", () => parties.Decline(PlayerLocator.Local));
        SocialUI.Show(promptGroup, false);

        toastText = InventoryUIFactory.Text("Social Message", root, "", 20f, new Color(1f, 0.9f, 0.6f), TextAlignmentOptions.Center, FontStyles.Bold);
        SocialUI.Place(toastText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(900f, 34f));
        toastGroup = toastText.gameObject.AddComponent<CanvasGroup>();
        toastGroup.blocksRaycasts = false;
        toastGroup.alpha = 0f;
    }
}
