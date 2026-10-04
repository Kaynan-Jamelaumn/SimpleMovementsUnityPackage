using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The prompt shown for the interaction target: a key badge, what interacting does and the target's name
/// ("[E] Trade  Bram the Smith"), why it cannot be used when it cannot ("Busy"), and a fill while the key is held.
/// Built from code by the <see cref="PlayerInteractor"/> when no prompt prefab is assigned; a prefab of your own needs
/// this component with its fields assigned (each one is optional).
/// </summary>
[DisallowMultipleComponent]
public class InteractionPromptUI : MonoBehaviour
{
    [SerializeField] private GameObject keyBadge;
    [SerializeField] private TextMeshProUGUI keyText;
    [SerializeField] private TextMeshProUGUI actionText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI reasonText;
    [Tooltip("Filled image (Image Type = Filled) that shows hold progress.")]
    [SerializeField] private Image holdFill;
    [SerializeField] private CanvasGroup group;
    [Tooltip("Where the prompt sits when it does not follow its target (anchored to the bottom centre of the screen).")]
    [SerializeField] private Vector2 screenBottomPosition = new Vector2(0f, 230f);
    [Tooltip("Colour of the action and name when the target cannot be used.")]
    [SerializeField] private Color unavailableColor = new Color(0.65f, 0.65f, 0.65f, 0.85f);

    private Color actionColor = Color.white, nameColor = Color.white;
    private bool colorsRead;

    /// <summary>Builds the default prompt under <paramref name="parent"/> (a canvas), hidden.</summary>
    public static InteractionPromptUI Create(Transform parent)
    {
        RectTransform rt = InventoryUIFactory.Rect("InteractionPrompt", parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        var p = rt.gameObject.AddComponent<InteractionPromptUI>();
        p.group = rt.gameObject.AddComponent<CanvasGroup>();
        p.group.blocksRaycasts = false;
        p.group.interactable = false;
        var fit = rt.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        VerticalLayoutGroup column = InventoryUIFactory.Vertical(rt.gameObject, 0, 4f, false);
        column.childAlignment = TextAnchor.MiddleCenter;
        column.childForceExpandWidth = false;

        Image bg = InventoryUIFactory.Panel("Row", rt, new Color(0f, 0f, 0f, 0.55f));
        bg.raycastTarget = false;
        HorizontalLayoutGroup row = InventoryUIFactory.Horizontal(bg.gameObject, 8f);
        row.padding = new RectOffset(8, 12, 6, 6);
        row.childAlignment = TextAnchor.MiddleCenter;
        var rowFit = bg.gameObject.AddComponent<ContentSizeFitter>();
        rowFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        Image badge = InventoryUIFactory.Panel("Key", bg.transform, new Color(1f, 1f, 1f, 0.9f));
        badge.raycastTarget = false;
        InventoryUIFactory.Size(badge, 30f, 30f, 0f);
        p.keyBadge = badge.gameObject;
        p.holdFill = InventoryUIFactory.Panel("Hold", badge.transform, new Color(0.35f, 0.6f, 1f, 0.85f));
        p.holdFill.raycastTarget = false;
        p.holdFill.type = Image.Type.Filled;
        p.holdFill.fillMethod = Image.FillMethod.Vertical;
        p.holdFill.fillAmount = 0f;
        InventoryUIFactory.Stretch(p.holdFill.rectTransform);
        p.keyText = InventoryUIFactory.Text("Label", badge.transform, "E", 16f, Color.black, TextAlignmentOptions.Center, FontStyles.Bold);
        p.keyText.textWrappingMode = TextWrappingModes.NoWrap;
        InventoryUIFactory.Stretch(p.keyText.rectTransform, 2f, 0f);

        p.actionText = InventoryUIFactory.Text("Action", bg.transform, "Talk", 20f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        p.actionText.textWrappingMode = TextWrappingModes.NoWrap;
        p.nameText = InventoryUIFactory.Text("Name", bg.transform, "", 20f, new Color(1f, 0.86f, 0.5f), TextAlignmentOptions.Left);
        p.nameText.textWrappingMode = TextWrappingModes.NoWrap;

        p.reasonText = InventoryUIFactory.Text("Reason", rt, "", 15f, new Color(1f, 0.6f, 0.5f), TextAlignmentOptions.Center, FontStyles.Italic);
        p.reasonText.textWrappingMode = TextWrappingModes.NoWrap;
        p.gameObject.SetActive(false);
        return p;
    }

    private void Awake()
    {
        if (group == null)
            group = GetComponent<CanvasGroup>();
        ReadColors();
    }

    private void ReadColors()
    {
        if (colorsRead)
            return;
        colorsRead = true;
        if (actionText != null) actionColor = actionText.color;
        if (nameText != null) nameColor = nameText.color;
    }

    /// <summary>
    /// Shows the prompt. <paramref name="key"/> empty hides the key badge; <paramref name="reason"/> not empty greys the
    /// prompt and explains it; <paramref name="hold01"/> fills the badge (0-1).
    /// </summary>
    public void Show(string key, string action, string targetName, string reason, float hold01)
    {
        ReadColors();
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        if (group != null)
            group.alpha = 1f;
        bool available = string.IsNullOrEmpty(reason);
        if (keyBadge != null)
            keyBadge.SetActive(!string.IsNullOrEmpty(key) && available);
        if (keyText != null && keyText.text != key)
            keyText.text = key ?? "";
        if (actionText != null)
        {
            actionText.text = action ?? "";
            actionText.color = available ? actionColor : unavailableColor;
        }
        if (nameText != null)
        {
            nameText.text = targetName ?? "";
            nameText.color = available ? nameColor : unavailableColor;
            nameText.gameObject.SetActive(!string.IsNullOrEmpty(targetName));
        }
        if (reasonText != null)
        {
            reasonText.text = reason ?? "";
            reasonText.gameObject.SetActive(!available);
        }
        if (holdFill != null)
            holdFill.fillAmount = Mathf.Clamp01(hold01);
    }

    public void Hide()
    {
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    /// <summary>Puts the prompt at a screen point (its bottom centre), or at its bottom-of-screen place when null.</summary>
    public void Place(Vector2? screenPoint)
    {
        var rt = (RectTransform)transform;
        if (screenPoint == null)
        {
            rt.anchoredPosition = screenBottomPosition;
            return;
        }
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;
        Canvas root = canvas.rootCanvas;
        Camera cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)root.transform, screenPoint.Value, cam, out Vector3 world))
            rt.position = world;
        InventoryUIFactory.ClampToCanvas(rt, canvas);
    }
}
