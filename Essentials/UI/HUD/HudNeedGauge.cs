using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One needs gauge of the HUD (food, water, rest, mind, warmth, air, load): a ring that fills with the status and its
/// percentage. Shown only while its status shows its bar (sleep, body heat and oxygen appear when they matter); pulses
/// when low (Load: when nearly full). Built by Player HUD ▸ Build Status UI; restyle it or replace its parts freely -
/// <see cref="statusKey"/> says which status it shows.
/// </summary>
public class HudNeedGauge : MonoBehaviour
{
    [Tooltip("The status shown: its script name without \"Manager\" (Hunger, Thirst, Sleep, Sanity, BodyHeat, Oxygen, Weight...).")]
    public string statusKey = "Hunger";
    [Header("Parts")]
    public Image ring;
    public TextMeshProUGUI valueText;
    public TextMeshProUGUI label;
    [Header("Look")]
    public Color color = new Color(0.9f, 0.6f, 0.32f);
    public Color warningColor = new Color(1f, 0.42f, 0.36f);
    [Tooltip("A full gauge is the bad end (carried weight).")]
    public bool highIsBad;
    [Tooltip("Pulses below this fraction (above 1 - this when High Is Bad).")]
    [Range(0f, 1f)] public float warnAt = 0.25f;
    [Tooltip("Show the percentage in the ring.")]
    public bool showPercent = true;

    private StatusManager status;
    private Image proxy;
    private string last;

    public StatusManager Status => status;

    /// <summary>Builds a gauge for <paramref name="key"/>.</summary>
    public static HudNeedGauge Create(Transform parent, string key, HudPalette p)
    {
        RectTransform gauge = HudStyle.Rect(key, parent);
        gauge.sizeDelta = new Vector2(50f, 64f);
        var g = gauge.gameObject.AddComponent<HudNeedGauge>();
        g.statusKey = key;
        g.color = HudStyle.NeedColor(key);
        g.warningColor = p.warning;
        g.highIsBad = key == "Weight";
        if (g.highIsBad) g.warnAt = 0.1f;

        Image disc = HudStyle.Image("Back", gauge, p.panel, HudStyle.Circle());
        HudStyle.Place(disc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(46f, 46f));
        var sh = disc.gameObject.AddComponent<Shadow>();
        sh.effectColor = p.shadow;
        sh.effectDistance = new Vector2(0f, -2f);
        Image track = HudStyle.Image("Track", disc.transform, new Color(g.color.r, g.color.g, g.color.b, 0.18f), HudStyle.Ring());
        HudStyle.Stretch(track.rectTransform, 2f);
        g.ring = HudStyle.Image("Ring", disc.transform, g.color, HudStyle.Ring());
        g.ring.type = Image.Type.Filled;
        g.ring.fillMethod = Image.FillMethod.Radial360;
        g.ring.fillOrigin = (int)Image.Origin360.Top;
        g.ring.fillClockwise = true;
        g.ring.fillAmount = 1f;
        HudStyle.Stretch(g.ring.rectTransform, 2f);
        g.valueText = HudStyle.Text("Value", disc.transform, "100", 14f, p.text, TextAlignmentOptions.Center, FontStyles.Bold);
        HudStyle.Stretch(g.valueText.rectTransform);
        g.label = HudStyle.Text("Label", gauge, HudStyle.NeedLabel(key), 12f, p.muted, TextAlignmentOptions.Center);
        HudStyle.Place(g.label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(60f, 16f));
        return g;
    }

    /// <summary>
    /// Shows <paramref name="s"/>. The status fills <paramref name="proxyImage"/> (hidden) instead of a bar: statuses that
    /// show only when they matter switch it on and off, and the gauge follows.
    /// </summary>
    public void Bind(StatusManager s, Image proxyImage)
    {
        status = s;
        proxy = proxyImage;
        if (s != null && proxyImage != null) s.UIImage = proxyImage;
        last = null;
    }

    /// <summary>Called every frame by the status frame (also while hidden, to come back when needed).</summary>
    public void Tick()
    {
        bool show = status != null && status.isActiveAndEnabled && (proxy == null || proxy.gameObject.activeSelf);
        if (gameObject.activeSelf != show) gameObject.SetActive(show);
        if (!show) return;
        float f = status.MaxValue > 0f ? Mathf.Clamp01(status.CurrentValue / status.MaxValue) : 0f;
        float dt = Time.unscaledDeltaTime;
        if (ring != null)
        {
            ring.fillAmount = Mathf.MoveTowards(ring.fillAmount, f, dt * 1.5f);
            bool low = highIsBad ? f >= 1f - warnAt : f <= warnAt;
            float pulse = low ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f) : 0f;
            ring.color = Color.Lerp(color, warningColor, pulse * 0.7f);
            float sc = 1f + pulse * 0.06f;
            transform.localScale = new Vector3(sc, sc, 1f);
        }
        if (valueText != null && showPercent)
        {
            string t = Mathf.RoundToInt(f * 100f).ToString();
            if (t != last) { valueText.text = t; last = t; }
        }
    }
}
