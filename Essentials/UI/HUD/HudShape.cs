using UnityEngine;
using UnityEngine.UI;

/// <summary>Procedural shapes of the HUD (made in code, so they are not assets).</summary>
public enum HudShapeKind
{
    /// <summary>Leave the image's sprite alone (your own sprite).</summary>
    None,
    Rounded,
    Circle,
    Ring,
    Glow,
}

/// <summary>
/// Gives an image one of the HUD's code-made sprites (rounded rectangle, circle, ring, glow) whenever it loads, in the
/// editor too. Those sprites are not assets, so UI built in the editor and saved in a scene or prefab would lose them:
/// this puts them back. Set Kind to None (or remove it) to use a sprite of your own.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class HudShape : MonoBehaviour
{
    public HudShapeKind kind = HudShapeKind.Rounded;
    [Tooltip("Corner radius (Rounded) in canvas pixels.")]
    [Range(1, 64)] public int radius = 10;

    private void Awake() => Apply();
    private void OnEnable() => Apply();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (isActiveAndEnabled) Apply();
    }
#endif

    public void Apply()
    {
        var img = GetComponent<Image>();
        if (img == null || kind == HudShapeKind.None)
            return;
        switch (kind)
        {
            case HudShapeKind.Rounded:
                img.sprite = UITheme.RoundedSprite(radius);
                if (img.type == Image.Type.Simple) img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1f;
                break;
            case HudShapeKind.Circle: img.sprite = HudStyle.Circle(); break;
            case HudShapeKind.Ring: img.sprite = HudStyle.Ring(); break;
            case HudShapeKind.Glow: img.sprite = HudStyle.Glow(); break;
        }
    }

    /// <summary>Adds a shape keeper for the code-made sprite the image uses (nothing for other sprites).</summary>
    public static void Attach(Image img)
    {
        if (img == null || img.sprite == null)
            return;
        string n = img.sprite.name;
        HudShapeKind k = HudShapeKind.None;
        int r = 10;
        if (n.StartsWith("UITheme Rounded "))
        {
            k = HudShapeKind.Rounded;
            int.TryParse(n.Substring("UITheme Rounded ".Length), out r);
        }
        else if (n == "HUD Circle") k = HudShapeKind.Circle;
        else if (n == "HUD Ring") k = HudShapeKind.Ring;
        else if (n == "HUD Glow") k = HudShapeKind.Glow;
        if (k == HudShapeKind.None)
            return;
        HudShape s = img.GetComponent<HudShape>();
        if (s == null) s = img.gameObject.AddComponent<HudShape>();
        s.kind = k;
        s.radius = Mathf.Clamp(r, 1, 64);
    }
}
