using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The dungeon portal panel: when this machine's player selects a dungeon portal (clicks it, see
/// <see cref="PlayerTargeting.SelectedPortal"/>) or walks up to one, a small panel at the top of the screen shows how
/// many player slots it has left (<see cref="Portal.Slots"/>): "3 of 5 slots free", "Full", "No player limit", whether
/// a shared run is going and how many players are inside, and the dungeon's difficulty. In a network session the
/// numbers come from the server (asked about once a second while the panel is open).
/// <para>Created next to the <see cref="PlayerTargeting"/> (its Create Interface), never on a dedicated server.</para>
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Portal Info Panel")]
public class PortalInfoPanel : MonoBehaviour
{
    [Tooltip("Show the panel for a dungeon portal within this distance (metres) even when it is not selected (0 = only selected portals).")]
    [Min(0f)] public float nearDistance = 10f;
    [Tooltip("A selected portal is deselected when the player gets farther than this (metres).")]
    [Min(1f)] public float keepDistance = 40f;
    [Tooltip("Where the panel sits: from the top centre of the screen (reference 1920×1080).")]
    public Vector2 position = new Vector2(0f, -78f);
    public Color freeColor = new Color(0.4f, 1f, 0.5f, 1f);
    public Color fewColor = new Color(1f, 0.85f, 0.35f, 1f);
    public Color fullColor = new Color(1f, 0.35f, 0.3f, 1f);

    /// <summary>The panel of this machine (null on a dedicated server or before one exists).</summary>
    public static PortalInfoPanel Instance { get; private set; }

    /// <summary>The portal shown now, or null.</summary>
    public Portal Shown { get; private set; }

    private PlayerTargeting targeting;
    private CanvasGroup group;
    private RectTransform panel;
    private TextMeshProUGUI titleText, slotsText, detailText;
    private string lastSlots, lastDetail, lastTitle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        targeting = GetComponent<PlayerTargeting>();
        if (targeting == null)
            targeting = PlayerTargeting.Instance;
        if (!GameNet.NetAuthority.IsHeadless)
            Build();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (panel != null)
            Destroy(panel.gameObject);
    }

    private void Update()
    {
        if (panel == null)
            return;
        Portal portal = Pick();
        Shown = portal;
        SocialUI.Show(group, portal != null);
        if (portal != null)
            Show(portal, portal.Slots);
    }

    /// <summary>The selected portal while it is near enough, else the nearest open dungeon portal within Near Distance.</summary>
    private Portal Pick()
    {
        CombatEntity me = PlayerLocator.Local;
        if (me == null)
            return null;
        Vector3 at = me.Position;
        if (targeting == null)
            targeting = PlayerTargeting.Instance;
        Portal selected = targeting != null ? targeting.SelectedPortal : null;
        if (!ReferenceEquals(selected, null))
        {
            if (selected != null && selected.isActiveAndEnabled && selected.IsOpen && Vector3.Distance(at, selected.transform.position) <= keepDistance)
                return selected;
            targeting.SelectPortal(null); // gone, closed or left behind
        }
        if (nearDistance <= 0f)
            return null;
        Portal best = null;
        float bestDistance = nearDistance;
        var all = Portal.All;
        for (int i = 0; i < all.Count; i++)
        {
            Portal p = all[i];
            if (p == null || !p.LeadsToDungeon || !p.IsOpen)
                continue;
            float d = Vector3.Distance(at, p.transform.position);
            if (d <= bestDistance)
            {
                bestDistance = d;
                best = p;
            }
        }
        return best;
    }

    private void Show(Portal portal, PortalSlots s)
    {
        string title = s.known && s.difficulty > 0f && Mathf.Abs(s.difficulty - 1f) > 0.05f
            ? $"Dungeon Portal  <size=75%><color=#b8bec8>Difficulty ×{s.difficulty:0.0}</color></size>"
            : "Dungeon Portal";
        string slots, detail;
        Color color;
        if (!s.open)
        {
            slots = "Closed";
            detail = "This portal lets nobody in right now.";
            color = fullColor;
        }
        else if (s.Unlimited)
        {
            slots = "No player limit";
            detail = s.shared
                ? s.running ? $"A run is going: {Players(s.inside)} inside. Entering joins it." : "Everyone who enters joins the same dungeon."
                : "Each group gets a dungeon of its own.";
            color = freeColor;
        }
        else if (s.shared)
        {
            int free = s.Remaining;
            slots = free > 0 ? $"{free} of {s.maxPlayers} slot{(s.maxPlayers == 1 ? "" : "s")} free" : $"Full  ({s.maxPlayers} / {s.maxPlayers} players)";
            detail = s.running ? $"A run is going: {Players(s.inside)} inside. Entering joins it." : "Everyone who enters joins the same dungeon.";
            color = free <= 0 ? fullColor : free * 4 <= s.maxPlayers ? fewColor : freeColor;
        }
        else
        {
            slots = s.maxPlayers == 1 ? "1 slot  (solo only)" : $"{s.maxPlayers} slots free";
            detail = s.maxPlayers == 1 ? "Each player gets a dungeon of their own." : $"Each group gets a dungeon of its own (up to {s.maxPlayers} players).";
            color = freeColor;
        }
        if (!s.known)
            detail += "  <color=#8a8f99>(asking the server…)</color>";
        if (title != lastTitle)
            titleText.text = lastTitle = title;
        if (slots != lastSlots)
            slotsText.text = lastSlots = slots;
        if (detail != lastDetail)
            detailText.text = lastDetail = detail;
        slotsText.color = color;
    }

    private static string Players(int n) => n == 1 ? "1 player" : $"{n} players";

    // ------------------------------------------------------------------ building

    private void Build()
    {
        Image bg = InventoryUIFactory.Panel("Portal Info", SocialUI.Canvas.transform, InventoryUIFactory.PanelColor);
        bg.raycastTarget = false;
        panel = bg.rectTransform;
        SocialUI.Place(panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, new Vector2(500f, 104f));
        group = bg.gameObject.AddComponent<CanvasGroup>();
        InventoryUIFactory.Vertical(bg.gameObject, 10, 2f, false);

        titleText = InventoryUIFactory.Text("Title", panel, "Dungeon Portal", 17f, InventoryUIFactory.MutedColor, TextAlignmentOptions.Center, FontStyles.Bold);
        titleText.richText = true;
        InventoryUIFactory.Size(titleText, -1f, 22f, 1f);
        slotsText = InventoryUIFactory.Text("Slots", panel, "", 26f, freeColor, TextAlignmentOptions.Center, FontStyles.Bold);
        slotsText.textWrappingMode = TextWrappingModes.NoWrap;
        InventoryUIFactory.Size(slotsText, -1f, 32f, 1f);
        detailText = InventoryUIFactory.Text("Detail", panel, "", 15f, InventoryUIFactory.TextColor, TextAlignmentOptions.Center, FontStyles.Italic);
        detailText.richText = true;
        InventoryUIFactory.Size(detailText, -1f, 22f, 1f);
        SocialUI.Show(group, false);
    }
}
