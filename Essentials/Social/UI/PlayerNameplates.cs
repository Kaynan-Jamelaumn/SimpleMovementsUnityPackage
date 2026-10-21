using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Names and health bars above other players' heads, on this machine's screen:
/// <list type="bullet">
/// <item>every other player near enough shows their name (coloured by what they are to you: party, guild, alliance,
/// duel, can attack you here, neutral) and, optionally, their guild tag;</item>
/// <item>party members, players in a duel and players in combat (hit or hitting within Combat Seconds) also show a
/// health bar - green for your party, guild and alliance, orange for duellists, red for players who can attack you;</item>
/// <item>players farther than Max Distance (or in another realm) show nothing; the plates fade out over the last metres
/// and shrink with distance.</item>
/// </list>
/// Created next to the <see cref="PlayerTargeting"/> (its Create Interface), never on a dedicated server. Put one on the
/// Player Targeting object yourself to change the settings.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Player Nameplates")]
public class PlayerNameplates : MonoBehaviour
{
    [Header("Who")]
    [Tooltip("Show other players' names.")]
    public bool showNames = true;
    [Tooltip("Show the guild tag after the name (\"Name [TAG]\").")]
    public bool showGuildTag = true;
    [Tooltip("Players farther than this (metres) show neither name nor health bar.")]
    [Min(1f)] public float maxDistance = 45f;
    [Tooltip("The plates fade out over this many metres before Max Distance.")]
    [Min(0f)] public float fadeDistance = 8f;
    [Tooltip("Hide the plates of players behind walls and terrain (one line-of-sight test per player, a few times a second).")]
    public bool hideBehindWalls = false;

    [Header("Health bars")]
    [Tooltip("Party members show a health bar.")]
    public bool barsForParty = true;
    [Tooltip("Players in a duel show a health bar.")]
    public bool barsForDuels = true;
    [Tooltip("Players in combat show a health bar.")]
    public bool barsInCombat = true;
    [Tooltip("Guild and alliance members show a health bar too.")]
    public bool barsForGuild = false;
    [Tooltip("Every player in range shows a health bar.")]
    public bool alwaysShowBars = false;
    [Tooltip("Seconds after the last hit (taken or landed) a player still counts as in combat.")]
    [Min(0.5f)] public float combatSeconds = 6f;

    [Header("Look")]
    [Tooltip("Height above the head (metres).")]
    public float headOffset = 0.35f;
    [Tooltip("Name text size at close range (reference 1920×1080).")]
    [Min(6f)] public float fontSize = 20f;
    [Tooltip("Health bar size at close range (reference 1920×1080).")]
    public Vector2 barSize = new Vector2(112f, 9f);
    [Tooltip("Size of a plate at Max Distance, relative to close range.")]
    [Range(0.2f, 1f)] public float farScale = 0.6f;
    public Color nameColor = new Color(0.95f, 0.95f, 0.95f, 1f);
    public Color partyColor = new Color(0.35f, 1f, 0.5f, 1f);
    public Color guildColor = new Color(0.45f, 0.9f, 1f, 1f);
    public Color allianceColor = new Color(0.6f, 0.72f, 1f, 1f);
    public Color duelColor = new Color(1f, 0.62f, 0.2f, 1f);
    public Color enemyColor = new Color(1f, 0.35f, 0.3f, 1f);
    public Color deadColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [Tooltip("Health bar of party, guild and alliance members.")]
    public Color friendlyBarColor = new Color(0.25f, 0.85f, 0.35f, 1f);
    [Tooltip("Health bar of duellists.")]
    public Color duelBarColor = new Color(1f, 0.55f, 0.15f, 1f);
    [Tooltip("Health bar of players who can attack you.")]
    public Color enemyBarColor = new Color(0.9f, 0.2f, 0.15f, 1f);
    [Tooltip("Health bar of everyone else.")]
    public Color neutralBarColor = new Color(0.95f, 0.8f, 0.25f, 1f);

    /// <summary>The nameplates of this machine (null on a dedicated server or before one exists).</summary>
    public static PlayerNameplates Instance { get; private set; }

    private sealed class Plate
    {
        public CombatEntity entity;
        public RectTransform root;
        public CanvasGroup group;
        public TextMeshProUGUI name;
        public RectTransform bar;
        public Image fill;
        public bool showBar;
        public bool visible;
        public bool occluded;
        public float distance;
        public float nextRefresh;
        public float nextSightCheck;
        public string text;
        public int seenFrame;
    }

    private Canvas canvas;
    private RectTransform canvasRect;
    private readonly Dictionary<CombatEntity, Plate> plates = new Dictionary<CombatEntity, Plate>();
    private readonly Stack<Plate> pool = new Stack<Plate>();
    private readonly List<Plate> ordered = new List<Plate>();
    private readonly List<CombatEntity> gone = new List<CombatEntity>();
    private float nextSort;

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

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (canvas != null)
            Destroy(canvas.gameObject);
    }

    private void OnDisable()
    {
        foreach (Plate p in plates.Values)
            Hide(p);
    }

    // ------------------------------------------------------------------ every frame

    private void LateUpdate()
    {
        if (GameNet.NetAuthority.IsHeadless)
            return;
        Camera cam = Camera.main;
        CombatEntity me = PlayerLocator.Local;
        int frame = Time.frameCount;
        if (cam != null && (showNames || barsForParty || barsForDuels || barsInCombat || alwaysShowBars))
        {
            EnsureCanvas();
            Vector3 from = me != null ? me.Position : cam.transform.position;
            IReadOnlyList<CombatEntity> players = CombatEntity.Players;
            for (int i = 0; i < players.Count; i++)
            {
                CombatEntity p = players[i];
                if (p == null || p == me || !p.isActiveAndEnabled || p.IsInvisible || (me != null && CombatRules.AreSeparated(me, p)))
                    continue; // invisible players (powers) show no plate either
                float d = Vector3.Distance(from, p.Position);
                if (d > maxDistance)
                    continue;
                Vector3 head = p.BasePosition + Vector3.up * (p.Height + headOffset);
                Vector3 screen = cam.WorldToScreenPoint(head);
                if (screen.z <= 0.05f || screen.x < -100f || screen.x > Screen.width + 100f || screen.y < -50f || screen.y > Screen.height + 100f)
                    continue;
                Plate plate = Get(p);
                plate.seenFrame = frame;
                plate.distance = d;
                if (hideBehindWalls && Time.unscaledTime >= plate.nextSightCheck)
                {
                    plate.nextSightCheck = Time.unscaledTime + 0.25f + Random.value * 0.1f;
                    plate.occluded = !CombatQuery.HasLineOfSight(cam.transform.position, head);
                }
                if (hideBehindWalls && plate.occluded)
                {
                    Hide(plate);
                    continue;
                }
                if (Time.unscaledTime >= plate.nextRefresh)
                    Refresh(plate, me);
                if (!showNames && !plate.showBar)
                {
                    Hide(plate);
                    continue;
                }
                Place(plate, screen, d);
            }
        }
        // Players out of range, off screen or gone: hidden (and recycled once gone).
        gone.Clear();
        foreach (KeyValuePair<CombatEntity, Plate> kv in plates)
        {
            if (kv.Value.seenFrame == frame)
                continue;
            Hide(kv.Value);
            if (kv.Key == null)
                gone.Add(kv.Key);
        }
        for (int i = 0; i < gone.Count; i++)
        {
            Plate p = plates[gone[i]];
            plates.Remove(gone[i]);
            p.entity = null;
            pool.Push(p);
        }
        // Nearer plates are drawn over farther ones.
        if (Time.unscaledTime >= nextSort)
        {
            nextSort = Time.unscaledTime + 0.2f;
            ordered.Clear();
            foreach (Plate p in plates.Values)
                if (p.visible)
                    ordered.Add(p);
            ordered.Sort((a, b) => b.distance.CompareTo(a.distance));
            for (int i = 0; i < ordered.Count; i++)
                ordered[i].root.SetSiblingIndex(i);
        }
    }

    /// <summary>Name, colours and whether the bar shows (a few times a second: relations change with parties, duels, zones).</summary>
    private void Refresh(Plate plate, CombatEntity me)
    {
        plate.nextRefresh = Time.unscaledTime + 0.25f;
        CombatEntity p = plate.entity;
        PlayerBond bond = PlayerBonds.Between(me, p);
        bool enemy = me != null && CombatRelations.Get(me, p) == CombatRelation.Enemy;
        bool dead = p.IsDead;

        bool bar = alwaysShowBars ||
                   (barsForParty && bond == PlayerBond.Party) ||
                   (barsForGuild && (bond == PlayerBond.Guild || bond == PlayerBond.Alliance)) ||
                   (barsForDuels && bond == PlayerBond.Duel) ||
                   (barsInCombat && CombatActivity.InCombat(p, combatSeconds));
        plate.showBar = bar && !dead;

        string text = "";
        if (showNames)
        {
            text = PartySystem.NameOf(p);
            if (showGuildTag)
            {
                PlayerSocial social = PlayerSocial.For(p, false);
                if (social != null && !string.IsNullOrEmpty(social.GuildTag))
                    text += $" <size=80%><color=#b8bec8>[{social.GuildTag}]</color></size>";
            }
        }
        if (text != plate.text)
        {
            plate.text = text;
            plate.name.text = text;
        }
        plate.name.gameObject.SetActive(showNames);
        plate.name.color = dead ? deadColor : NameColor(bond, enemy);
        plate.bar.gameObject.SetActive(plate.showBar);
        plate.fill.color = BarColor(bond, enemy);
    }

    private Color NameColor(PlayerBond bond, bool enemy)
    {
        switch (bond)
        {
            case PlayerBond.Duel: return duelColor;
            case PlayerBond.Party: return partyColor;
            case PlayerBond.Guild: return guildColor;
            case PlayerBond.Alliance: return allianceColor;
            default: return enemy ? enemyColor : nameColor;
        }
    }

    private Color BarColor(PlayerBond bond, bool enemy)
    {
        if (bond == PlayerBond.Duel)
            return duelBarColor;
        if (PlayerBonds.IsFriendly(bond))
            return friendlyBarColor;
        return enemy ? enemyBarColor : neutralBarColor;
    }

    private void Place(Plate plate, Vector3 screen, float distance)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
        {
            Hide(plate);
            return;
        }
        if (!plate.visible)
        {
            plate.visible = true;
            plate.root.gameObject.SetActive(true);
        }
        plate.root.anchoredPosition = local;
        float t = Mathf.Clamp01(distance / maxDistance);
        float scale = Mathf.Lerp(1f, farScale, t);
        plate.root.localScale = new Vector3(scale, scale, 1f);
        float fadeStart = maxDistance - fadeDistance;
        plate.group.alpha = fadeDistance > 0f && distance > fadeStart ? Mathf.Clamp01((maxDistance - distance) / fadeDistance) : 1f;
        if (plate.showBar)
            plate.fill.fillAmount = plate.entity.HealthRatio;
    }

    private static void Hide(Plate plate)
    {
        if (!plate.visible)
            return;
        plate.visible = false;
        if (plate.root != null)
            plate.root.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ building

    private void EnsureCanvas()
    {
        if (canvas != null)
            return;
        var go = new GameObject("Player Nameplates", typeof(RectTransform));
        DontDestroyOnLoad(go);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30; // under the HUD (60) and the social windows (70)
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)go.transform;
    }

    private Plate Get(CombatEntity e)
    {
        if (plates.TryGetValue(e, out Plate plate))
            return plate;
        plate = pool.Count > 0 ? pool.Pop() : Build();
        plate.entity = e;
        plate.text = null;
        plate.nextRefresh = 0f;
        plate.nextSightCheck = 0f;
        plate.occluded = false;
        plate.root.name = "Nameplate " + e.name;
        plates.Add(e, plate);
        return plate;
    }

    private Plate Build()
    {
        RectTransform root = InventoryUIFactory.Rect("Nameplate", canvasRect);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0f);
        root.sizeDelta = new Vector2(Mathf.Max(220f, barSize.x), fontSize + barSize.y + 8f);
        var plate = new Plate { root = root, group = root.gameObject.AddComponent<CanvasGroup>() };
        plate.group.interactable = false;
        plate.group.blocksRaycasts = false;

        // The bar at the bottom (the pivot sits over the head), the name above it.
        Image bg = InventoryUIFactory.Panel("Health", root, new Color(0f, 0f, 0f, 0.6f));
        bg.raycastTarget = false;
        plate.bar = bg.rectTransform;
        plate.bar.anchorMin = plate.bar.anchorMax = new Vector2(0.5f, 0f);
        plate.bar.pivot = new Vector2(0.5f, 0f);
        plate.bar.anchoredPosition = Vector2.zero;
        plate.bar.sizeDelta = barSize;
        plate.fill = InventoryUIFactory.Panel("Fill", bg.transform, friendlyBarColor);
        InventoryUIFactory.Stretch(plate.fill.rectTransform, 1f, 1f);
        plate.fill.type = Image.Type.Filled;
        plate.fill.fillMethod = Image.FillMethod.Horizontal;
        plate.fill.fillOrigin = 0;
        plate.fill.sprite = SocialUI.WhiteSprite;
        plate.fill.raycastTarget = false;

        plate.name = InventoryUIFactory.Text("Name", root, "", fontSize, nameColor, TextAlignmentOptions.Bottom, FontStyles.Bold);
        plate.name.textWrappingMode = TextWrappingModes.NoWrap;
        plate.name.overflowMode = TextOverflowModes.Overflow;
        plate.name.richText = true;
        plate.name.outlineWidth = 0.18f;
        plate.name.outlineColor = new Color32(0, 0, 0, 200);
        RectTransform nt = plate.name.rectTransform;
        nt.anchorMin = new Vector2(0f, 0f);
        nt.anchorMax = new Vector2(1f, 0f);
        nt.pivot = new Vector2(0.5f, 0f);
        nt.anchoredPosition = new Vector2(0f, barSize.y + 3f);
        nt.sizeDelta = new Vector2(0f, fontSize + 6f);

        root.gameObject.SetActive(false);
        return plate;
    }
}
