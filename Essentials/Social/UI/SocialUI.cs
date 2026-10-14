using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared pieces of the social interface (target frame, party frame, inspect window): one canvas, bars, windows that free the cursor.</summary>
public static class SocialUI
{
    private static Canvas canvas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => canvas = null;

    /// <summary>The overlay canvas of the social interface (created once, kept across scenes).</summary>
    public static Canvas Canvas
    {
        get
        {
            if (canvas != null)
                return canvas;
            var go = new GameObject("Social UI", typeof(RectTransform));
            Object.DontDestroyOnLoad(go);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            InventoryEventSystems.EnsureSingle();
            return canvas;
        }
    }

    /// <summary>A horizontal bar (returns the fill image; set its fillAmount).</summary>
    public static Image Bar(string name, Transform parent, Color fill, float height, out TextMeshProUGUI label)
    {
        Image bg = InventoryUIFactory.Panel(name, parent, new Color(0f, 0f, 0f, 0.55f));
        InventoryUIFactory.Size(bg, -1f, height, 1f);
        Image bar = InventoryUIFactory.Panel("Fill", bg.transform, fill);
        InventoryUIFactory.Stretch(bar.rectTransform, 1f, 1f);
        bar.type = Image.Type.Filled;
        bar.fillMethod = Image.FillMethod.Horizontal;
        bar.fillOrigin = 0;
        bar.sprite = WhiteSprite;
        bar.raycastTarget = false;
        label = InventoryUIFactory.Text("Value", bg.transform, "", Mathf.Max(11f, height - 6f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        InventoryUIFactory.Stretch(label.rectTransform);
        return bar;
    }

    private static Sprite white;

    /// <summary>A plain white sprite (filled images need one).</summary>
    public static Sprite WhiteSprite
    {
        get
        {
            if (white == null)
                white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            return white;
        }
    }

    public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    public static void Show(CanvasGroup g, bool visible)
    {
        if (g == null)
            return;
        g.alpha = visible ? 1f : 0f;
        g.interactable = visible;
        g.blocksRaycasts = visible;
    }

    public static void Clear(Transform list)
    {
        for (int i = list.childCount - 1; i >= 0; i--)
        {
            GameObject child = list.GetChild(i).gameObject;
            child.SetActive(false);
            child.transform.SetParent(null, false);
            Object.Destroy(child);
        }
    }

    /// <summary>"#ff4033" for rich text.</summary>
    public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
}

/// <summary>
/// Opens a window that needs the mouse: registered on the player's inventory (free cursor, clicks do not attack,
/// Escape closes it first), or the cursor is freed directly when the player has no inventory.
/// </summary>
public sealed class SocialPanelHandle
{
    private readonly IGameplayPanel panel;
    private InventoryManager registeredWith;
    private bool freedCursor;
    private CursorLockMode previousLock;
    private bool previousVisible;

    public SocialPanelHandle(IGameplayPanel panel)
    {
        this.panel = panel;
    }

    public bool IsOpen => registeredWith != null || freedCursor;

    public void Open()
    {
        if (IsOpen)
            return;
        CombatEntity me = PlayerLocator.Local;
        InventoryManager inv = me != null ? InventoryManager.For(me) : null;
        if (inv != null)
        {
            registeredWith = inv;
            inv.RegisterPanel(panel);
            return;
        }
        freedCursor = true;
        previousLock = Cursor.lockState;
        previousVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Close()
    {
        if (registeredWith != null)
        {
            InventoryManager inv = registeredWith;
            registeredWith = null;
            inv.UnregisterPanel(panel);
        }
        if (freedCursor)
        {
            freedCursor = false;
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
        }
    }
}
