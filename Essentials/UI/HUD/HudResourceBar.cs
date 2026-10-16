using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A resource bar of the player HUD (health, mana, stamina, experience): a rounded track, the fill gliding to the
/// value, a pale "ghost" that trails behind after a loss (you see how much you just lost), a soft shine, the name and
/// "120 / 150". Low values pulse. Built by <see cref="PlayerHUD"/>; usable on your own UI: call
/// <see cref="SetValue"/> every frame.
/// </summary>
public class HudResourceBar : MonoBehaviour
{
    [SerializeField] private RectTransform fill;
    [SerializeField] private RectTransform ghost;
    [SerializeField] private Image fillImage;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private TextMeshProUGUI value;
    [Tooltip("Below this fraction the bar pulses (0 = never).")]
    [Range(0f, 1f)] public float lowWarning = 0.25f;
    [Tooltip("Seconds the ghost waits before following a loss.")]
    public float ghostDelay = 0.45f;
    [Tooltip("Show \"current / max\" (off: only the name).")]
    public bool showValue = true;

    private Color baseColor;
    private Color warningColor = new Color(1f, 0.42f, 0.36f);
    private float shown = -1f, ghostShown = -1f, ghostWait;
    private string lastText;

    /// <summary>Builds a bar under <paramref name="parent"/>.</summary>
    public static HudResourceBar Create(Transform parent, string name, Color color, HudPalette p, float height, bool showLabel = true)
    {
        Image track = HudStyle.Image(name, parent, p.track, UITheme.RoundedSprite(Mathf.Clamp(Mathf.RoundToInt(height * 0.5f), 2, 12)), true);
        var bar = track.gameObject.AddComponent<HudResourceBar>();
        int r = Mathf.Clamp(Mathf.RoundToInt(height * 0.5f) - 1, 2, 11);
        RectTransform area = HudStyle.Stretch(HudStyle.Rect("Area", track.transform), 2f);

        Image ghostImg = HudStyle.Image("Ghost", area, p.ghost, UITheme.RoundedSprite(r), true);
        bar.ghost = ghostImg.rectTransform;
        Image fillImg = HudStyle.Image("Fill", area, color, UITheme.RoundedSprite(r), true);
        bar.fill = fillImg.rectTransform;
        bar.fillImage = fillImg;
        // A soft highlight on the top half: a little volume.
        Image shine = HudStyle.Image("Shine", fillImg.transform, new Color(1f, 1f, 1f, 0.16f), UITheme.RoundedSprite(Mathf.Max(2, r - 1)), true);
        shine.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        shine.rectTransform.anchorMax = Vector2.one;
        shine.rectTransform.offsetMin = new Vector2(2f, 0f);
        shine.rectTransform.offsetMax = new Vector2(-2f, -1f);
        foreach (RectTransform rt in new[] { bar.ghost, bar.fill })
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        if (showLabel && height >= 12f)
        {
            float size = Mathf.Clamp(height * 0.62f, 10f, 16f);
            bar.label = HudStyle.Text("Label", track.transform, name, size, p.text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            HudStyle.Stretch(bar.label.rectTransform).offsetMin = new Vector2(8f, 0f);
            bar.value = HudStyle.Text("Value", track.transform, "", size * 0.92f, p.text, TextAlignmentOptions.MidlineRight);
            HudStyle.Stretch(bar.value.rectTransform).offsetMax = new Vector2(-8f, 0f);
            foreach (TextMeshProUGUI t in new[] { bar.label, bar.value })
            {
                var sh = t.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.6f);
                sh.effectDistance = new Vector2(1f, -1f);
            }
        }
        bar.baseColor = color;
        bar.warningColor = p.warning;
        return bar;
    }

    private void Awake()
    {
        if (fillImage != null && baseColor.a <= 0f) baseColor = fillImage.color;
    }

    /// <summary>Shows <paramref name="current"/> of <paramref name="max"/> (call every frame: it animates).</summary>
    public void SetValue(float current, float max, string text = null)
    {
        float target = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        float dt = Time.unscaledDeltaTime;
        if (shown < 0f) { shown = ghostShown = target; }
        // The fill glides (fast down, a little slower up); the ghost waits, then follows a loss.
        shown = Mathf.MoveTowards(shown, target, dt * (target < shown ? 3.5f : 1.6f) + Mathf.Abs(target - shown) * dt * 6f);
        if (target >= ghostShown) { ghostShown = Mathf.Max(shown, target); ghostWait = ghostDelay; }
        else if ((ghostWait -= dt) <= 0f) ghostShown = Mathf.MoveTowards(ghostShown, target, dt * 0.9f);
        Set(fill, shown);
        Set(ghost, Mathf.Max(ghostShown, shown));

        if (fillImage != null)
        {
            bool low = lowWarning > 0f && target <= lowWarning && max > 0f;
            float pulse = low ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f) : 0f;
            fillImage.color = Color.Lerp(baseColor, warningColor, pulse * 0.6f);
        }
        if (value != null)
        {
            string v = text ?? (showValue ? $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}" : "");
            if (v != lastText) { value.text = v; lastText = v; }
        }
    }

    /// <summary>Editor preview: the bar drawn <paramref name="fraction"/> full (no animation).</summary>
    public void Preview(float fraction)
    {
        Set(fill, fraction);
        Set(ghost, fraction);
        if (value != null && showValue) value.text = $"{Mathf.RoundToInt(fraction * 100f)} / 100";
    }

    /// <summary>Changes the bar's name and colour (the warning colour stays).</summary>
    public void SetLook(string name, Color color)
    {
        if (label != null) label.text = name;
        baseColor = color;
    }

    private static void Set(RectTransform rt, float t)
    {
        if (rt == null) return;
        rt.anchorMax = new Vector2(t, 1f);
        if (rt.gameObject.activeSelf != t > 0.001f) rt.gameObject.SetActive(t > 0.001f);
    }
}
