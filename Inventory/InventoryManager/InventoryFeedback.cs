using TMPro;
using UnityEngine;

/// <summary>
/// Short messages above the hotbar that explain what just failed or happened: an item that cannot be used, does not fit,
/// is on cooldown; no ammo; an off-hand item stowed by a two-handed weapon. Built from code by the
/// <see cref="InventoryManager"/>; anything can show a message through <see cref="InventoryManager.ShowMessage"/>.
/// </summary>
[DisallowMultipleComponent]
public class InventoryFeedback : MonoBehaviour
{
    public enum Kind { Info, Warning, Error }

    [SerializeField] private TextMeshProUGUI text;
    [Tooltip("Seconds a message stays fully visible.")]
    [SerializeField, Min(0.2f)] private float showSeconds = 2f;
    [Tooltip("Seconds it takes to fade out.")]
    [SerializeField, Min(0.05f)] private float fadeSeconds = 0.4f;

    private CanvasGroup group;
    private float hideAt = -1f;
    private string current;

    public string CurrentMessage => hideAt > Time.unscaledTime ? current : "";

    /// <summary>Builds the message line under <paramref name="parent"/> (bottom centre, above the hotbar).</summary>
    public static InventoryFeedback Create(Transform parent)
    {
        RectTransform rt = InventoryUIFactory.Rect("InventoryFeedback", parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 150f);
        rt.sizeDelta = new Vector2(760f, 40f);
        var f = rt.gameObject.AddComponent<InventoryFeedback>();
        f.group = rt.gameObject.AddComponent<CanvasGroup>();
        f.group.blocksRaycasts = false;
        f.group.interactable = false;
        f.group.alpha = 0f;
        f.text = InventoryUIFactory.Text("Text", rt, "", 22f, InventoryUIFactory.TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
        InventoryUIFactory.Stretch(f.text.rectTransform);
        f.text.textWrappingMode = TextWrappingModes.NoWrap;
        return f;
    }

    private void Awake()
    {
        if (group == null)
        {
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
        }
        if (text == null)
            text = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    /// <summary>Shows <paramref name="message"/> for a moment (the same message again keeps it up).</summary>
    public void Show(string message, Kind kind = Kind.Warning)
    {
        if (string.IsNullOrWhiteSpace(message) || text == null)
            return;
        current = message;
        text.text = message;
        text.color = kind == Kind.Error ? new Color(1f, 0.45f, 0.4f) : kind == Kind.Warning ? new Color(1f, 0.85f, 0.45f) : InventoryUIFactory.TextColor;
        hideAt = Time.unscaledTime + showSeconds;
        if (group != null)
            group.alpha = 1f;
        transform.SetAsLastSibling();
    }

    private void Update()
    {
        if (group == null || group.alpha <= 0f)
            return;
        float left = hideAt - Time.unscaledTime;
        group.alpha = left >= 0f ? 1f : Mathf.Clamp01(1f + left / fadeSeconds);
    }
}
