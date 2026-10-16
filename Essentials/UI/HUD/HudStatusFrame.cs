using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The player frame of the HUD: portrait with the initial and level, name and class, health / mana / stamina bars,
/// experience, and the needs gauges under it. Built by <b>Player HUD ▸ Build Status UI</b> (in the editor: it is saved
/// in your prefab / scene, so you can move, resize and restyle it) or when the game starts. Every part below can be
/// replaced by your own (any can be empty), and <see cref="portrait"/> can show a picture.
/// </summary>
public class HudStatusFrame : MonoBehaviour
{
    [Header("Identity")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI classText;
    public TextMeshProUGUI levelText;
    [Tooltip("Hidden when the player has no Experience Manager.")]
    public GameObject levelBadge;
    [Tooltip("The letter in the portrait (hidden when the Portrait has a sprite).")]
    public TextMeshProUGUI initialText;
    [Tooltip("Optional: an image for a portrait picture (give it a sprite).")]
    public Image portrait;

    [Header("Bars")]
    public HudResourceBar healthBar;
    public HudResourceBar manaBar;
    public HudResourceBar staminaBar;
    public HudResourceBar experienceBar;

    [Header("Needs")]
    [Tooltip("Where the needs gauges are. Empty = no needs gauges.")]
    public RectTransform needsRoot;
    public List<HudNeedGauge> needGauges = new List<HudNeedGauge>();
    [Tooltip("Statuses without a gauge get one when the game starts (a copy of the Player HUD's Need Gauge Prefab, else the built-in gauge).")]
    public bool addMissingGauges = true;

    private StatusManager health, mana, stamina;
    private ExperienceManager xp;
    private PlayerStatusController status;
    private GameObject player;
    private float nextIdentity;
    private string lastName;
    private int lastLevel = int.MinValue;

    // ------------------------------------------------------------------ build
    /// <summary>Builds the frame under <paramref name="parent"/> for the player <paramref name="hud"/> belongs to.</summary>
    public static HudStatusFrame Build(Transform parent, PlayerHUD hud, GameObject playerRoot, bool withNeeds)
    {
        HudPalette p = hud.palette;
        Image frame = HudStyle.Frame("HUD Status", parent, p, 16);
        var f = frame.gameObject.AddComponent<HudStatusFrame>();
        RectTransform rt = frame.rectTransform;
        const float width = 420f;
        HudStyle.Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(hud.margin.x, -hud.margin.x), new Vector2(width, 120f));

        Image ring = HudStyle.Image("Portrait", frame.transform, p.accent, HudStyle.Circle());
        HudStyle.Place(ring.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(90f, 90f));
        Image face = HudStyle.Image("Face", ring.transform, p.slot, HudStyle.Circle());
        HudStyle.Stretch(face.rectTransform, 3f);
        Image picture = HudStyle.Image("Picture", face.transform, Color.white);
        HudStyle.Stretch(picture.rectTransform, 2f);
        picture.preserveAspect = true;
        picture.enabled = false;
        f.portrait = picture;
        Image sheen = HudStyle.Image("Sheen", face.transform, new Color(1f, 0.9f, 0.75f, 0.12f), HudStyle.Glow());
        HudStyle.Stretch(sheen.rectTransform, 4f);
        f.initialText = HudStyle.Text("Initial", face.transform, "?", 40f, p.accent, TextAlignmentOptions.Center, FontStyles.Bold);
        HudStyle.Stretch(f.initialText.rectTransform);
        Image badge = HudStyle.Image("Level", ring.transform, p.accent, HudStyle.Circle());
        HudStyle.Place(badge.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-10f, 10f), new Vector2(34f, 34f));
        Image badgeIn = HudStyle.Image("Inner", badge.transform, p.keyBadge, HudStyle.Circle());
        HudStyle.Stretch(badgeIn.rectTransform, 2.5f);
        f.levelText = HudStyle.Text("Text", badgeIn.transform, "1", 15f, p.text, TextAlignmentOptions.Center, FontStyles.Bold);
        HudStyle.Stretch(f.levelText.rectTransform);
        f.levelBadge = badge.gameObject;

        StatusManager hp = null, mp = null, sp = null;
        var others = new List<StatusManager>();
        foreach (StatusManager s in Distinct(playerRoot))
        {
            string key = HudStyle.StatusKey(s);
            if (key == "Health") hp = s;
            else if (key == "Mana") mp = s;
            else if (key == "Stamina") sp = s;
            else if (key != "Speed") others.Add(s);
        }
        ExperienceManager xp = playerRoot != null ? playerRoot.GetComponentInChildren<ExperienceManager>(true) : null;
        if (xp == null) badge.gameObject.SetActive(false);

        float left = 116f, right = 16f, y = -12f;
        f.nameText = HudStyle.Text("Name", frame.transform, NameOf(playerRoot), 20f, p.text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        Line(f.nameText.rectTransform, left, right, y, 24f);
        f.classText = HudStyle.Text("Class", frame.transform, "", 14f, p.muted, TextAlignmentOptions.MidlineRight, FontStyles.Italic);
        Line(f.classText.rectTransform, left, right, y, 24f);
        if (!string.IsNullOrEmpty(f.nameText.text)) f.initialText.text = f.nameText.text.Substring(0, 1).ToUpperInvariant();
        y -= 28f;
        // Health, mana and stamina are always built (hidden at runtime when the player lacks the status): easy to restyle.
        f.healthBar = Bar("Health", p.health, 22f, ref y);
        f.manaBar = Bar("Mana", p.mana, 15f, ref y);
        f.staminaBar = Bar("Stamina", p.stamina, 13f, ref y);
        f.experienceBar = Bar("Experience", p.experience, 5f, ref y, false);
        f.experienceBar.lowWarning = 0f;
        f.experienceBar.showValue = false;
        rt.sizeDelta = new Vector2(width, Mathf.Max(114f, -y + 8f));
        foreach (HudResourceBar b in new[] { f.healthBar, f.manaBar, f.staminaBar })
            b.Preview(1f);
        f.experienceBar.Preview(0.35f);

        if (withNeeds)
        {
            f.needsRoot = HudStyle.Rect("Needs", frame.transform);
            HudStyle.Place(f.needsRoot, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(4f, -8f), new Vector2(width, 64f));
            var h = f.needsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8f;
            h.childControlWidth = h.childControlHeight = false;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            h.childAlignment = TextAnchor.UpperLeft;
            foreach (StatusManager s in others)
                f.needGauges.Add(f.NewGauge(hud, HudStyle.StatusKey(s)));
        }
        return f;

        HudResourceBar Bar(string label, Color color, float height, ref float top, bool showLabel = true)
        {
            HudResourceBar b = HudResourceBar.Create(frame.transform, label, color, p, height, showLabel);
            Line((RectTransform)b.transform, left, right, top, height);
            top -= height + 5f;
            return b;
        }
    }

    private HudNeedGauge NewGauge(PlayerHUD hud, string key)
    {
        HudNeedGauge g;
        if (hud != null && hud.needGaugePrefab != null)
        {
            g = Instantiate(hud.needGaugePrefab, needsRoot, false);
            g.name = key;
            g.statusKey = key;
            if (g.label != null) g.label.text = HudStyle.NeedLabel(key);
            g.color = HudStyle.NeedColor(key);
            g.highIsBad = key == "Weight";
        }
        else
            g = HudNeedGauge.Create(needsRoot, key, hud != null ? hud.palette : new HudPalette());
        return g;
    }

    private static void Line(RectTransform rt, float left, float right, float top, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, top - height);
        rt.offsetMax = new Vector2(-right, top);
    }

    /// <summary>One status of each kind on the player (a second manager of the same kind is ignored).</summary>
    public static List<StatusManager> Distinct(GameObject playerRoot)
    {
        var list = new List<StatusManager>();
        if (playerRoot == null) return list;
        var seen = new HashSet<System.Type>();
        foreach (StatusManager s in playerRoot.GetComponentsInChildren<StatusManager>(true))
            if (s != null && seen.Add(s.GetType())) list.Add(s);
        return list;
    }

    internal static string NameOf(GameObject playerRoot)
    {
        if (playerRoot == null) return "";
        Player p = playerRoot.GetComponentInChildren<Player>(true);
        if (p != null && !string.IsNullOrWhiteSpace(p.PlayerName)) return p.PlayerName;
        PlayerNameComponent pn = playerRoot.GetComponentInChildren<PlayerNameComponent>(true);
        if (pn != null && !string.IsNullOrWhiteSpace(pn.PlayerName)) return pn.PlayerName;
        if (Application.isPlaying && CharacterSession.Current != null && !string.IsNullOrWhiteSpace(CharacterSession.Current.characterName))
            return CharacterSession.Current.characterName;
        return playerRoot.name.Replace("(Clone)", "").Trim();
    }

    // ------------------------------------------------------------------ runtime
    /// <summary>Connects the frame to the player's statuses (the game started).</summary>
    public void Bind(PlayerHUD hud, GameObject playerRoot, PlayerStatusController playerStatus, Transform proxyHolder)
    {
        player = playerRoot;
        status = playerStatus;
        xp = playerStatus != null && playerStatus.XPManager != null ? playerStatus.XPManager : playerRoot.GetComponentInChildren<ExperienceManager>(true);
        var others = new List<StatusManager>();
        foreach (StatusManager s in Distinct(playerRoot))
        {
            string key = HudStyle.StatusKey(s);
            if (key == "Health") health = s;
            else if (key == "Mana") mana = s;
            else if (key == "Stamina") stamina = s;
            else if (key != "Speed") others.Add(s);
        }
        if (healthBar != null) healthBar.gameObject.SetActive(health != null);
        if (manaBar != null) manaBar.gameObject.SetActive(mana != null);
        if (staminaBar != null) staminaBar.gameObject.SetActive(stamina != null);
        if (experienceBar != null) experienceBar.gameObject.SetActive(xp != null);
        if (levelBadge != null) levelBadge.SetActive(xp != null);

        needGauges.RemoveAll(g => g == null);
        if (needsRoot != null)
        {
            foreach (StatusManager s in others)
            {
                string key = HudStyle.StatusKey(s);
                HudNeedGauge g = needGauges.Find(x => x.statusKey == key);
                if (g == null && addMissingGauges)
                {
                    g = NewGauge(hud, key);
                    needGauges.Add(g);
                }
                if (g == null) continue;
                // The status fills a hidden proxy; statuses shown only when they matter switch it on and off.
                var proxyGo = new GameObject(key + " Proxy", typeof(RectTransform), typeof(Image));
                proxyGo.transform.SetParent(proxyHolder, false);
                var proxy = proxyGo.GetComponent<Image>();
                proxy.color = Color.clear;
                proxy.raycastTarget = false;
                g.Bind(s, proxy);
            }
        }
        foreach (HudNeedGauge g in needGauges)
            if (g != null && g.Status == null) g.gameObject.SetActive(false); // the player has no such status
        nextIdentity = 0f;
    }

    /// <summary>Called every frame by the Player HUD.</summary>
    public void Tick()
    {
        if (healthBar != null && health != null) healthBar.SetValue(health.CurrentValue, health.MaxValue);
        if (manaBar != null && mana != null) manaBar.SetValue(mana.CurrentValue, mana.MaxValue);
        if (staminaBar != null && stamina != null) staminaBar.SetValue(stamina.CurrentValue, stamina.MaxValue);
        if (experienceBar != null && xp != null) experienceBar.SetValue(xp.CurrentExperience, Mathf.Max(1, xp.ExperienceToNextLevel), "");
        foreach (HudNeedGauge g in needGauges)
            if (g != null && g.Status != null) g.Tick();
        if (Time.unscaledTime < nextIdentity) return;
        nextIdentity = Time.unscaledTime + 0.5f;
        string n = NameOf(player);
        if (n != lastName)
        {
            lastName = n;
            if (nameText != null) nameText.text = n;
            if (initialText != null) initialText.text = string.IsNullOrEmpty(n) ? "?" : n.Substring(0, 1).ToUpperInvariant();
        }
        if (classText != null)
        {
            string cls = status != null && status.CurrentPlayerClass != null ? status.CurrentPlayerClass.GetClassName() : "";
            if (classText.text != cls) classText.text = cls;
        }
        if (initialText != null && portrait != null) initialText.enabled = portrait.sprite == null;
        if (portrait != null) portrait.enabled = portrait.sprite != null;
        int level = xp != null ? xp.CurrentLevel : 0;
        if (level != lastLevel && levelText != null)
        {
            lastLevel = level;
            levelText.text = level.ToString();
        }
    }
}
