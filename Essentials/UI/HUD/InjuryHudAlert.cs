using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A small warning at the top of the screen while the local player bleeds, misses a limb (with the time left to
/// reattach it) or has a broken bone - so the player knows to open the inventory's Health page. Created by the
/// player's <see cref="InjuryController"/> (Injury Settings ▸ Show Hud Alert).
/// </summary>
public class InjuryHudAlert : MonoBehaviour
{
    [SerializeField] private InjuryController injuries;
    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI text;

    private readonly StringBuilder sb = new StringBuilder();
    private float nextRefresh;

    public static InjuryHudAlert Create(InjuryController owner)
    {
        if (owner == null)
            return null;
        Canvas canvas = UITheme.Canvas("Injury Alert", 40);
        var alert = canvas.gameObject.AddComponent<InjuryHudAlert>();
        alert.injuries = owner;
        alert.background = UITheme.Panel("Alert", canvas.transform, new Color(0.35f, 0.04f, 0.06f, 0.88f), UITheme.Radius);
        alert.background.raycastTarget = false;
        RectTransform rt = alert.background.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -18f);
        rt.sizeDelta = new Vector2(560f, 40f);
        alert.text = UITheme.Label(rt, "", 17f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        InventoryUIFactory.Stretch(alert.text.rectTransform, 12f, 4f);
        alert.background.gameObject.SetActive(false);
        return alert;
    }

    /// <summary>Redraws (called by the controller every frame; it refreshes a few times a second).</summary>
    public void Tick()
    {
        if (injuries == null || background == null || Time.unscaledTime < nextRefresh)
            return;
        nextRefresh = Time.unscaledTime + 0.25f;
        sb.Clear();
        float bleed = injuries.BleedingPerSecond;
        if (bleed > 1e-4f)
            Append(bleed >= injuries.Settings.bleeding.heavyBleedingPerSecond ? "HEAVY BLEEDING" : "Bleeding");
        bool broken = false;
        foreach (InjuryPartState p in injuries.Parts)
        {
            if (p.Lost && p.LostWith == null)
            {
                SeveredLimbRecord r = p.LostLimb;
                Append(r != null && !r.Consumed && !r.Expired ? $"{p.Name} lost ({SeveredLimbRecord.FormatTime(r.Remaining)})" : $"{p.Name} lost");
            }
            if (!broken && p.HasTag(InjuryDefaults.TagFracture))
                broken = true;
        }
        if (broken)
            Append("Broken bone");
        if (injuries.IsTreating)
            Append($"{injuries.CurrentTreatment.Treatment.name} {injuries.CurrentTreatment.Progress * 100f:0}%");
        bool show = sb.Length > 0;
        if (background.gameObject.activeSelf != show)
            background.gameObject.SetActive(show);
        if (!show)
            return;
        text.text = sb.ToString();
        background.rectTransform.sizeDelta = new Vector2(Mathf.Clamp(text.preferredWidth + 40f, 220f, 1100f), 40f);
        float pulse = bleed > 1e-4f ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f) : 0.88f;
        background.color = new Color(0.35f, 0.04f, 0.06f, pulse);
    }

    private void Append(string s)
    {
        if (sb.Length > 0)
            sb.Append("   ·   ");
        sb.Append(s);
    }
}
