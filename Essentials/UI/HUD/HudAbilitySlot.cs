using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>What an ability slot of the HUD shows: an ability key of the player, or the active trait's ability.</summary>
public interface IHudAbilitySource
{
    /// <summary>The live slot (cooldown, charges, phase), or null when nothing is on it.</summary>
    AbilitySlot Slot { get; }
    Sprite Icon { get; }
    string Name { get; }
    string KeyLabel { get; }
    /// <summary>Text of the hover panel (description, cast time, cooldown, costs, effects).</summary>
    string Describe();
    /// <summary>Why it cannot be used now (None = it can).</summary>
    CastFailReason ReadyReason { get; }
    bool TryUse();
}

/// <summary>
/// One slot of the abilities bar (familiar from MMOs and MOBAs): the ability's icon, its key in a small badge, a soft
/// glow and a filling bar while it is cast, a dark sweep with the seconds left while it cools down, its charges, blue
/// when there is not enough mana / stamina, grey when it cannot be used (stunned, silenced). An empty slot shows no
/// icon. Hover: name, key and description (<see cref="HudTooltip"/>); left click casts it.
/// <para>Built by <see cref="PlayerHUD"/>. For a custom look, make a prefab with this component, assign its parts and
/// set it as the Player HUD's Ability Slot Prefab.</para>
/// </summary>
public class HudAbilitySlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Tooltip("Which ability key this slot shows (0 = the first key of the Abilities State Machine). -1 = set by the bar " +
             "(its position). Not used by the active trait slot.")]
    public int keyIndex = -1;

    [Header("Parts (assign your own: any can be empty)")]
    [SerializeField] private Image frame;
    [SerializeField] private Image icon;
    [SerializeField] private Image cooldown;
    [SerializeField] private TextMeshProUGUI cooldownText;
    [SerializeField] private TextMeshProUGUI keyText;
    [SerializeField] private TextMeshProUGUI chargesText;
    [SerializeField] private Image castGlow;
    [SerializeField] private Image castProgress;
    [SerializeField] private Image readyFlash;
    [SerializeField] private TextMeshProUGUI initialsText;

    private IHudAbilitySource source;
    private HudPalette palette;
    private PlayerHUD hud;
    private Color frameColor;
    private bool hovering;
    private AbilityPhase lastPhase = AbilityPhase.Ready;
    private bool wasCooling;
    private float punch, flash;
    private string lastKey, lastCooldown, lastCharges;

    public IHudAbilitySource Source => source;

    /// <summary>Editor preview: the icon and key a slot will show (without the game running).</summary>
    public void Preview(Sprite iconSprite, string key)
    {
        if (icon != null) { icon.sprite = iconSprite; icon.enabled = iconSprite != null; }
        if (keyText != null) keyText.text = key ?? "";
        if (cooldown != null) cooldown.fillAmount = 0f;
        if (cooldownText != null) cooldownText.text = "";
        if (chargesText != null) chargesText.text = "";
    }

    /// <summary>Builds a slot of <paramref name="size"/> pixels.</summary>
    public static HudAbilitySlot Create(Transform parent, string name, float size, HudPalette p, bool gold = false)
    {
        Image border = HudStyle.Image(name, parent, gold ? p.accent : p.slotBorder, UITheme.RoundedSprite(10), true);
        border.raycastTarget = true;
        border.rectTransform.sizeDelta = new Vector2(size, size);
        var slot = border.gameObject.AddComponent<HudAbilitySlot>();

        // Glow behind everything while casting.
        slot.castGlow = HudStyle.Image("Cast Glow", border.transform, new Color(p.casting.r, p.casting.g, p.casting.b, 0f), HudStyle.Glow());
        HudStyle.Stretch(slot.castGlow.rectTransform, -size * 0.35f);
        slot.castGlow.transform.SetAsFirstSibling();

        slot.frame = HudStyle.Image("Frame", border.transform, p.slot, UITheme.RoundedSprite(9), true);
        HudStyle.Stretch(slot.frame.rectTransform, gold ? 2.5f : 1.5f);

        slot.icon = HudStyle.Image("Icon", slot.frame.transform, Color.white);
        slot.icon.preserveAspect = true;
        HudStyle.Stretch(slot.icon.rectTransform, 4f);

        slot.castProgress = HudStyle.Image("Cast Progress", slot.frame.transform, new Color(p.casting.r, p.casting.g, p.casting.b, 0.3f), UITheme.RoundedSprite(8), true);
        HudStyle.Stretch(slot.castProgress.rectTransform, 2f);
        slot.castProgress.rectTransform.anchorMax = new Vector2(1f, 0f);
        slot.castProgress.gameObject.SetActive(false);

        slot.cooldown = HudStyle.Image("Cooldown", slot.frame.transform, p.cooldown, UITheme.RoundedSprite(8));
        slot.cooldown.type = Image.Type.Filled;
        slot.cooldown.fillMethod = Image.FillMethod.Radial360;
        slot.cooldown.fillOrigin = (int)Image.Origin360.Top;
        slot.cooldown.fillClockwise = false;
        slot.cooldown.fillAmount = 0f;
        HudStyle.Stretch(slot.cooldown.rectTransform, 2f);

        slot.readyFlash = HudStyle.Image("Ready Flash", slot.frame.transform, new Color(1f, 0.95f, 0.8f, 0f), UITheme.RoundedSprite(8), true);
        HudStyle.Stretch(slot.readyFlash.rectTransform, 1f);

        slot.initialsText = HudStyle.Text("Initials", slot.frame.transform, "", size * 0.32f, p.muted, TextAlignmentOptions.Center, FontStyles.Bold);
        HudStyle.Stretch(slot.initialsText.rectTransform);
        slot.cooldownText = HudStyle.Text("Cooldown Text", slot.frame.transform, "", size * 0.36f, p.text, TextAlignmentOptions.Center, FontStyles.Bold);
        HudStyle.Stretch(slot.cooldownText.rectTransform);
        Outline(slot.cooldownText);

        slot.chargesText = HudStyle.Text("Charges", slot.frame.transform, "", size * 0.26f, p.text, TextAlignmentOptions.BottomRight, FontStyles.Bold);
        HudStyle.Stretch(slot.chargesText.rectTransform, 4f);
        Outline(slot.chargesText);

        // Key badge on the top left corner, half outside the slot.
        Image badge = HudStyle.Image("Key", border.transform, p.keyBadge, UITheme.RoundedSprite(6), true);
        HudStyle.Place(badge.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(size * 0.22f, -size * 0.12f), new Vector2(size * 0.42f, size * 0.3f));
        var badgeOutline = badge.gameObject.AddComponent<Outline>();
        badgeOutline.effectColor = gold ? p.accent : p.slotBorder;
        badgeOutline.effectDistance = new Vector2(1f, -1f);
        slot.keyText = HudStyle.Text("Text", badge.transform, "", size * 0.2f, p.text, TextAlignmentOptions.Center, FontStyles.Bold);
        HudStyle.Stretch(slot.keyText.rectTransform, 1f);
        slot.keyText.enableAutoSizing = true;
        slot.keyText.fontSizeMin = 8f;
        slot.keyText.fontSizeMax = size * 0.22f;
        return slot;
    }

    private static void Outline(TextMeshProUGUI t)
    {
        var o = t.gameObject.AddComponent<Shadow>();
        o.effectColor = new Color(0f, 0f, 0f, 0.85f);
        o.effectDistance = new Vector2(1f, -1f);
    }

    /// <summary>Shows <paramref name="newSource"/> (null = an empty slot).</summary>
    public void Bind(IHudAbilitySource newSource, PlayerHUD owner, HudPalette p)
    {
        source = newSource;
        hud = owner;
        palette = p ?? new HudPalette();
        frameColor = frame != null ? frame.color : Color.white;
        lastKey = lastCooldown = lastCharges = null;
    }

    private void Update()
    {
        if (palette == null) palette = new HudPalette();
        AbilitySlot s = source != null ? source.Slot : null;
        bool has = s != null && s.ability != null;
        Sprite sprite = has ? (source.Icon != null ? source.Icon : null) : null;

        // Icon: none on an empty slot; tinted by what stops the ability.
        if (icon != null)
        {
            icon.enabled = has && sprite != null;
            icon.sprite = sprite;
            CastFailReason why = has ? source.ReadyReason : CastFailReason.NoAbility;
            Color c = Color.white;
            if (why == CastFailReason.NotEnoughResource) c = palette.noResource;
            else if (why == CastFailReason.Stunned || why == CastFailReason.Silenced || why == CastFailReason.Dead || why == CastFailReason.Disabled) c = palette.unusable;
            icon.color = c;
        }
        // An ability without an icon still shows its initials.
        if (initialsText != null)
        {
            string ini = has && sprite == null ? Initials(source.Name) : "";
            if (initialsText.text != ini) initialsText.text = ini;
        }
        string key = source != null ? source.KeyLabel : "";
        if (keyText != null && key != lastKey) { keyText.text = key; lastKey = key; }
        // The badge hides without a key (only when the text sits in a badge of its own, as in the built-in slot).
        if (keyText != null && keyText.transform.parent != transform && keyText.transform.parent != (frame != null ? frame.transform : null))
            keyText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(key));

        AbilityPhase phase = has ? s.Phase : AbilityPhase.Ready;
        bool casting = phase == AbilityPhase.Casting || phase == AbilityPhase.Launching || phase == AbilityPhase.Active;
        if (casting && lastPhase != phase && lastPhase == AbilityPhase.Ready) punch = 1f;
        lastPhase = phase;

        float remaining = has ? s.CooldownRemaining : 0f;
        bool cooling = has && remaining > 0.05f && !casting;
        if (wasCooling && !cooling && has) flash = 1f; // ready again
        wasCooling = cooling;

        if (cooldown != null) cooldown.fillAmount = cooling ? Mathf.Max(0.02f, s.CooldownFraction) : 0f;
        string cd = cooling ? (remaining >= 10f ? Mathf.CeilToInt(remaining).ToString() : remaining >= 1f ? Mathf.CeilToInt(remaining).ToString() : remaining.ToString("0.0")) : "";
        if (cooldownText != null && cd != lastCooldown) { cooldownText.text = cd; lastCooldown = cd; }

        string ch = has && s.MaxCharges > 1 ? s.Charges.ToString() : "";
        if (chargesText != null && ch != lastCharges) { chargesText.text = ch; lastCharges = ch; }

        // Casting: a warm glow that breathes, and the cast filling the slot from the bottom.
        float t = Time.unscaledTime;
        if (castGlow != null)
        {
            Color g = palette.casting;
            g.a = casting ? 0.55f + 0.25f * Mathf.Sin(t * 9f) : Mathf.MoveTowards(castGlow.color.a, 0f, Time.unscaledDeltaTime * 3f);
            castGlow.color = g;
        }
        if (castProgress != null)
        {
            bool showCast = has && phase == AbilityPhase.Casting;
            castProgress.gameObject.SetActive(showCast);
            if (showCast) castProgress.rectTransform.anchorMax = new Vector2(1f, s.PhaseProgress);
        }
        if (readyFlash != null)
        {
            flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 2.5f);
            readyFlash.color = new Color(1f, 0.95f, 0.8f, flash * 0.45f);
        }
        punch = Mathf.MoveTowards(punch, 0f, Time.unscaledDeltaTime * 5f);
        float scale = 1f + 0.08f * Mathf.Sin(punch * Mathf.PI) + (hovering && has ? 0.03f : 0f);
        transform.localScale = new Vector3(scale, scale, 1f);

        if (frame != null)
        {
            Color f = hovering ? palette.slotHover : frameColor;
            if (!has) f.a *= 0.6f;
            frame.color = f;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        if (hud != null) hud.ShowTooltip(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        if (hud != null) hud.HideTooltip(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && source != null && source.Slot != null)
            source.TryUse();
    }

    /// <summary>Title, body and footer of the hover panel.</summary>
    public void TooltipText(out string title, out string body, out string footer)
    {
        AbilitySlot s = source != null ? source.Slot : null;
        string key = source != null ? source.KeyLabel : "";
        if (s == null || s.ability == null)
        {
            title = "Empty slot";
            body = string.IsNullOrEmpty(key) ? "No ability here." : $"No ability on <b>{key}</b> yet.";
            footer = "";
            return;
        }
        title = source.Name + (string.IsNullOrEmpty(key) ? "" : $"  <size=75%><color=#{ColorUtility.ToHtmlStringRGB(palette.accent)}>[{key}]</color></size>");
        body = source.Describe();
        // The first line of Describe() is the name: not twice.
        int nl = body.IndexOf('\n');
        if (nl >= 0 && body.Substring(0, nl).Trim().StartsWith(s.ability.DisplayName)) body = body.Substring(nl + 1);
        float left = s.CooldownRemaining;
        CastFailReason why = source.ReadyReason;
        footer = left > 0.05f ? $"Ready in {left:0.0}s" : why == CastFailReason.None ? "Ready" : Nicify(why);
        if (s.MaxCharges > 1) footer += $"   ·   Charges {s.Charges}/{s.MaxCharges}";
    }

    private static string Initials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        string[] words = name.Split(new[] { ' ', '-', '_' }, System.StringSplitOptions.RemoveEmptyEntries);
        string r = words.Length > 1 ? $"{words[0][0]}{words[1][0]}" : name.Substring(0, Mathf.Min(2, name.Length));
        return r.ToUpperInvariant();
    }

    private static string Nicify(CastFailReason r)
    {
        switch (r)
        {
            case CastFailReason.NotEnoughResource: return "Not enough resource";
            case CastFailReason.OnCooldown: return "Cooling down";
            case CastFailReason.Busy: return "Busy";
            case CastFailReason.Stunned: return "Stunned";
            case CastFailReason.Silenced: return "Silenced";
            case CastFailReason.Dead: return "Dead";
            case CastFailReason.Disabled: return "Disabled";
            default: return "Ready";
        }
    }
}
