using System.Collections.Generic;
using HubWorld;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The corner preview of the current target (<see cref="PlayerTargeting"/>): name (coloured by how they relate to
/// you), guild and alliance, level, health, and what they are to you (party member, guild member, duel opponent,
/// can attack you here). Its menu (the ▾ button or the Target Menu key) holds: Invite to Party / Remove from Party /
/// Make Party Leader, Trade, Challenge to a Duel, Inspect and Clear Target. Opening the menu frees the cursor.
/// </summary>
[DisallowMultipleComponent]
public class TargetFrame : MonoBehaviour, IGameplayPanel
{
    [Tooltip("Where the frame sits: from the top-left corner of the screen (reference 1920×1080).")]
    public Vector2 position = new Vector2(24f, -24f);

    private PlayerTargeting targeting;
    private CanvasGroup group, menuGroup;
    private RectTransform frame, menu;
    private TextMeshProUGUI nameText, levelText, guildText, relationText;
    private Image healthFill;
    private TextMeshProUGUI healthText;
    private SocialPanelHandle handle;
    private PlayerSocial watchedSocial;
    private bool menuOpen;
    private float nextRefresh;

    public string PanelName => "Target Menu";

    public void ClosePanel() => SetMenu(false);

    private void Start()
    {
        targeting = GetComponent<PlayerTargeting>();
        if (targeting == null)
            targeting = PlayerTargeting.Instance;
        handle = new SocialPanelHandle(this);
        Build();
        if (targeting != null)
        {
            targeting.TargetChanged += OnTargetChanged;
            targeting.MenuRequested += ToggleMenu;
        }
        PartySystem.Instance.Changed += Refresh;
        OnTargetChanged(targeting != null ? targeting.Target : null);
    }

    private void OnDestroy()
    {
        if (targeting != null)
        {
            targeting.TargetChanged -= OnTargetChanged;
            targeting.MenuRequested -= ToggleMenu;
        }
        if (PartySystem.Current != null)
            PartySystem.Current.Changed -= Refresh;
        Watch(null);
        handle?.Close();
        if (frame != null)
            Destroy(frame.gameObject);
    }

    private void OnTargetChanged(CombatEntity target)
    {
        SetMenu(false);
        Watch(target != null ? PlayerSocial.For(target, false) : null);
        Refresh();
    }

    private void Watch(PlayerSocial social)
    {
        if (watchedSocial != null)
            watchedSocial.Changed -= Refresh;
        watchedSocial = social;
        if (watchedSocial != null)
            watchedSocial.Changed += Refresh;
    }

    private void Update()
    {
        CombatEntity t = targeting != null ? targeting.Target : null;
        SocialUI.Show(group, t != null);
        if (t == null)
        {
            if (menuOpen)
                SetMenu(false);
            return;
        }
        if (healthFill != null)
        {
            healthFill.fillAmount = t.HealthRatio;
            healthFill.color = Color.Lerp(new Color(0.85f, 0.15f, 0.1f), new Color(0.2f, 0.8f, 0.3f), t.HealthRatio);
            float max = t.MaxHealth;
            healthText.text = max > 0f ? $"{Mathf.CeilToInt(max * t.HealthRatio)} / {Mathf.CeilToInt(max)}" : "";
        }
        // Relations change with zones, duels and parties: refreshed now and then.
        if (Time.unscaledTime >= nextRefresh)
            Refresh();
    }

    // ------------------------------------------------------------------ content

    private void Refresh()
    {
        nextRefresh = Time.unscaledTime + 0.5f;
        CombatEntity t = targeting != null ? targeting.Target : null;
        if (t == null || nameText == null)
            return;
        bool player = t.Kind == CombatEntity.EntityKind.Player;
        Color color = targeting.ColorOf(t);
        nameText.text = player ? PartySystem.NameOf(t) : t.name.Replace("(Clone)", "").Trim();
        nameText.color = color;
        if (Watched(t) == null && player)
            Watch(PlayerSocial.For(t, false));
        PlayerSocial social = watchedSocial;
        int level = LevelOf(t);
        levelText.text = level > 0 ? $"Lv {level}" : "";
        var line = new List<string>(2);
        if (social != null && social.HasGuild) line.Add(social.GuildLine);
        if (social != null && social.HasAlliance) line.Add($"<color=#9aa4b5>{social.AllianceName}</color>");
        if (line.Count == 0 && social != null && !string.IsNullOrEmpty(social.Title)) line.Add($"<i>{social.Title}</i>");
        guildText.text = line.Count > 0 ? string.Join("  ·  ", line) : (player ? "<color=#8a8f99>No guild</color>" : "");
        relationText.text = $"<color={SocialUI.Hex(color)}>{RelationLabel(t)}</color>";
        if (menuOpen)
            BuildMenu();
    }

    private PlayerSocial Watched(CombatEntity t) => watchedSocial != null && watchedSocial.Entity == t ? watchedSocial : null;

    private static int LevelOf(CombatEntity t) => PlayerSocial.LevelOf(t);

    private static string RelationLabel(CombatEntity t)
    {
        CombatEntity me = PlayerTargeting.Me;
        if (me == null)
            return "";
        if (t.Kind != CombatEntity.EntityKind.Player)
            return CombatRelations.IsEnemy(me, t) ? "Hostile" : "Neutral";
        if (CombatRules.Between(me, t) == PvpVerdict.Opponents)
            return "Duel opponent";
        if (CombatParties.SameParty(me, t))
            return PartySystem.Current != null && PartySystem.Current.IsLeader(t) ? "Party leader" : "Party member";
        if (CombatSocial.SameGuild(me, t))
            return "Guild member";
        if (CombatSocial.SameAlliance(me, t))
            return "Alliance member";
        switch (CombatRelations.Get(me, t))
        {
            case CombatRelation.Enemy: return "Can attack you here";
            case CombatRelation.Ally: return "Friendly";
            default: return "Neutral";
        }
    }

    // ------------------------------------------------------------------ menu

    private void ToggleMenu() => SetMenu(!menuOpen);

    private void SetMenu(bool open)
    {
        CombatEntity t = targeting != null ? targeting.Target : null;
        open &= t != null; // a mob's menu only has Clear Target
        menuOpen = open;
        if (open)
        {
            BuildMenu();
            handle.Open();
        }
        else
            handle?.Close();
        SocialUI.Show(menuGroup, open);
    }

    private void BuildMenu()
    {
        if (menu == null)
            return;
        SocialUI.Clear(menu);
        CombatEntity me = PlayerTargeting.Me, t = targeting != null ? targeting.Target : null;
        if (me == null || t == null)
            return;
        if (t.Kind == CombatEntity.EntityKind.Player)
        {
            PartySystem parties = PartySystem.Instance;
            Party mine = parties.PartyOf(me);
            bool together = mine != null && mine == parties.PartyOf(t);
            bool leader = parties.IsLeader(me);
            if (!together)
                Item("Invite to Party", () => PlayerActions.InviteToParty(me, t));
            else if (leader)
            {
                Item("Remove from Party", () => PlayerActions.Kick(me, t));
                Item("Make Party Leader", () => PlayerActions.Promote(me, t));
            }
            if (mine != null)
                Item("Leave Party", () => PlayerActions.LeaveParty(me));
            Item("Invite to Trade", () => PlayerActions.Trade(me, t), PlayerActions.CanTrade);
            if (PlayerActions.CanDuel)
            {
                Duel duel = DuelSystem.Instance.DuelOf(me);
                if (duel == null)
                    Item("Challenge to a Duel", () => PlayerActions.Duel(me, t));
                else if (DuelSystem.Instance.AreFighting(me, t))
                    Item("Forfeit the Duel", () => DuelSystem.Instance.Forfeit(me));
            }
            Item("Inspect", () => PlayerActions.Inspect(me, t));
        }
        Item("Clear Target", () => targeting.ClearTarget());
    }

    private void Item(string label, System.Action action, bool enabled = true)
    {
        Button b = InventoryUIFactory.Button(label, menu, label, () =>
        {
            action();
            SetMenu(false);
        });
        b.interactable = enabled;
        InventoryUIFactory.Size(b, -1f, 34f, 1f);
    }

    // ------------------------------------------------------------------ building

    private void Build()
    {
        Transform root = SocialUI.Canvas.transform;
        Image panel = InventoryUIFactory.Panel("Target Frame", root, InventoryUIFactory.PanelColor);
        frame = panel.rectTransform;
        SocialUI.Place(frame, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(400f, 132f));
        group = panel.gameObject.AddComponent<CanvasGroup>();
        InventoryUIFactory.Vertical(panel.gameObject, 10, 4f, false);

        RectTransform top = InventoryUIFactory.Rect("Top", panel.transform);
        InventoryUIFactory.Horizontal(top.gameObject, 8f);
        InventoryUIFactory.Size(top, -1f, 30f, 1f);
        nameText = InventoryUIFactory.Text("Name", top, "", 22f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.overflowMode = TextOverflowModes.Ellipsis;
        InventoryUIFactory.Size(nameText, -1f, 30f, 1f);
        levelText = InventoryUIFactory.Text("Level", top, "", 17f, InventoryUIFactory.MutedColor, TextAlignmentOptions.Right, FontStyles.Bold);
        InventoryUIFactory.Size(levelText, 60f, 30f, 0f);
        Button menuButton = InventoryUIFactory.Button("Menu", top, "▾", ToggleMenu, 36f);
        InventoryUIFactory.Size(menuButton, 36f, 30f, 0f);

        guildText = InventoryUIFactory.Text("Guild", panel.transform, "", 16f, InventoryUIFactory.TextColor);
        guildText.textWrappingMode = TextWrappingModes.NoWrap;
        guildText.overflowMode = TextOverflowModes.Ellipsis;
        guildText.richText = true;
        InventoryUIFactory.Size(guildText, -1f, 22f, 1f);

        healthFill = SocialUI.Bar("Health", panel.transform, new Color(0.2f, 0.8f, 0.3f), 18f, out healthText);

        relationText = InventoryUIFactory.Text("Relation", panel.transform, "", 15f, InventoryUIFactory.MutedColor, TextAlignmentOptions.Left, FontStyles.Italic);
        relationText.richText = true;
        InventoryUIFactory.Size(relationText, -1f, 20f, 1f);

        // The menu: a column of buttons under the frame.
        Image menuPanel = InventoryUIFactory.Panel("Target Menu", root, InventoryUIFactory.PanelColor);
        menu = menuPanel.rectTransform;
        SocialUI.Place(menu, new Vector2(0f, 1f), new Vector2(0f, 1f), position + new Vector2(0f, -140f), new Vector2(260f, 0f));
        InventoryUIFactory.Vertical(menuPanel.gameObject, 8, 4f, true);
        menuGroup = menuPanel.gameObject.AddComponent<CanvasGroup>();
        SocialUI.Show(menuGroup, false);
        SocialUI.Show(group, false);
    }
}
