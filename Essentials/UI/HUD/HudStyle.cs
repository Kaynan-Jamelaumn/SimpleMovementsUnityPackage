using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The colours of the player HUD (warm, soft: a cozy look). Change them on the Player HUD component.</summary>
[Serializable]
public class HudPalette
{
    [Header("Panels")]
    public Color panel = new Color(0.11f, 0.09f, 0.078f, 0.9f);
    public Color panelBorder = new Color(0.96f, 0.8f, 0.55f, 0.24f);
    public Color slot = new Color(0.17f, 0.14f, 0.118f, 0.96f);
    public Color slotBorder = new Color(0.96f, 0.82f, 0.58f, 0.3f);
    public Color slotHover = new Color(0.27f, 0.22f, 0.17f, 1f);
    public Color shadow = new Color(0f, 0f, 0f, 0.35f);

    [Header("Text")]
    public Color text = new Color(0.98f, 0.94f, 0.86f, 1f);
    public Color muted = new Color(0.8f, 0.73f, 0.62f, 0.78f);
    public Color keyBadge = new Color(0.07f, 0.055f, 0.045f, 0.94f);

    [Header("Accents")]
    public Color accent = new Color(1f, 0.79f, 0.44f, 1f);
    public Color casting = new Color(1f, 0.86f, 0.5f, 1f);
    public Color cooldown = new Color(0.03f, 0.024f, 0.02f, 0.72f);
    public Color noResource = new Color(0.5f, 0.6f, 1f, 1f);
    public Color unusable = new Color(0.55f, 0.55f, 0.55f, 1f);
    public Color warning = new Color(1f, 0.42f, 0.36f, 1f);

    [Header("Bars")]
    public Color track = new Color(0.045f, 0.036f, 0.03f, 0.88f);
    public Color health = new Color(0.86f, 0.33f, 0.29f, 1f);
    public Color mana = new Color(0.38f, 0.6f, 0.97f, 1f);
    public Color stamina = new Color(0.97f, 0.77f, 0.37f, 1f);
    public Color experience = new Color(0.62f, 0.83f, 0.45f, 1f);
    public Color ghost = new Color(1f, 0.93f, 0.8f, 0.55f);
}

/// <summary>Sprites and small building blocks of the player HUD (made in code: no art needed).</summary>
public static class HudStyle
{
    private static Sprite circle, ring, glow;
    private static readonly Dictionary<string, Color> needColors = new Dictionary<string, Color>
    {
        { "Hunger", new Color(0.9f, 0.6f, 0.32f) }, { "Thirst", new Color(0.36f, 0.72f, 0.93f) },
        { "Sleep", new Color(0.62f, 0.54f, 0.88f) }, { "Sanity", new Color(0.78f, 0.5f, 0.8f) },
        { "BodyHeat", new Color(0.97f, 0.5f, 0.33f) }, { "Oxygen", new Color(0.58f, 0.88f, 0.96f) },
        { "Weight", new Color(0.76f, 0.68f, 0.55f) },
    };
    private static readonly Dictionary<string, string> needLabels = new Dictionary<string, string>
    {
        { "Hunger", "Food" }, { "Thirst", "Water" }, { "Sleep", "Rest" }, { "Sanity", "Mind" },
        { "BodyHeat", "Warmth" }, { "Oxygen", "Air" }, { "Weight", "Load" },
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { circle = ring = glow = null; }

    /// <summary>"HungerManager" → "Hunger".</summary>
    public static string StatusKey(StatusManager s)
    {
        string n = s != null ? s.GetType().Name : "";
        return n.EndsWith("Manager") ? n.Substring(0, n.Length - "Manager".Length) : n;
    }

    public static Color NeedColor(string key) => needColors.TryGetValue(key, out Color c) ? c : new Color(0.8f, 0.75f, 0.65f);
    public static string NeedLabel(string key) => needLabels.TryGetValue(key, out string l) ? l : key;

    /// <summary>An antialiased white disc.</summary>
    public static Sprite Circle() => circle != null ? circle : circle = Make("HUD Circle", 128, (d, r) => Mathf.Clamp01(r - d + 0.5f));

    /// <summary>An antialiased white ring (radial fills of the needs gauges).</summary>
    public static Sprite Ring() => ring != null ? ring : ring = Make("HUD Ring", 128, (d, r) =>
    {
        float thickness = r * 0.2f;
        return Mathf.Clamp01(r - d + 0.5f) * Mathf.Clamp01(d - (r - thickness) + 0.5f);
    });

    /// <summary>A soft white glow (bright in the middle, fading out).</summary>
    public static Sprite Glow() => glow != null ? glow : glow = Make("HUD Glow", 128, (d, r) =>
    {
        float t = Mathf.Clamp01(d / r);
        return Mathf.Pow(1f - t, 2.2f);
    });

    private static Sprite Make(string name, int size, Func<float, float, float> alpha)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
        };
        var px = new Color32[size * size];
        float r = size * 0.5f - 1f, c = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(d, r)) * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        Sprite s = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        s.name = name;
        s.hideFlags = HideFlags.DontSave;
        return s;
    }

    // ------------------------------------------------------------------ building blocks
    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent != null ? parent.gameObject.layer : 5;
        var rt = (RectTransform)go.transform;
        if (parent != null) rt.SetParent(parent, false);
        return rt;
    }

    public static Image Image(string name, Transform parent, Color color, Sprite sprite = null, bool sliced = false)
    {
        RectTransform rt = Rect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        img.color = color;
        img.raycastTarget = false;
        HudShape.Attach(img); // a code-made sprite is put back when a saved scene / prefab loads
        return img;
    }

    /// <summary>A rounded panel with a thin warm border and a soft shadow: the frame every part of the HUD shares.</summary>
    public static Image Frame(string name, Transform parent, HudPalette p, int radius = 12, bool shadow = true)
    {
        Image border = Image(name, parent, p.panelBorder, UITheme.RoundedSprite(radius), true);
        if (shadow)
        {
            var sh = border.gameObject.AddComponent<Shadow>();
            sh.effectColor = p.shadow;
            sh.effectDistance = new Vector2(0f, -3f);
        }
        Image inner = Image("Fill", border.transform, p.panel, UITheme.RoundedSprite(Mathf.Max(2, radius - 1)), true);
        Stretch(inner.rectTransform, 1.5f);
        return border;
    }

    /// <summary>Gives an existing image (a hotbar slot, a panel) the HUD's rounded look.</summary>
    public static void Restyle(Image img, Color color, int radius, Color? outline = null)
    {
        if (img == null) return;
        img.sprite = UITheme.RoundedSprite(radius);
        img.type = UnityEngine.UI.Image.Type.Sliced;
        img.color = color;
        HudShape.Attach(img);
        if (outline.HasValue)
        {
            Outline o = img.GetComponent<Outline>();
            if (o == null) o = img.gameObject.AddComponent<Outline>();
            o.effectColor = outline.Value;
            o.effectDistance = new Vector2(1f, -1f);
        }
    }

    public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal)
    {
        TextMeshProUGUI t = InventoryUIFactory.Text(name, parent, text, size, color, align, style);
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    public static RectTransform Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        return rt;
    }

    /// <summary>A short key name for a badge ("Q", "LMB", "Shift"): the first binding only.</summary>
    public static string ShortKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "";
        foreach (string sep in new[] { " | ", " / ", "|", "/" })
        {
            int i = key.IndexOf(sep, StringComparison.Ordinal);
            if (i > 0) { key = key.Substring(0, i); break; }
        }
        key = key.Trim().Trim('\'');
        switch (key)
        {
            case "Left Button": return "LMB";
            case "Right Button": return "RMB";
            case "Middle Button": return "MMB";
            case "Left Shift": return "Shift";
            case "Left Ctrl": case "Left Control": return "Ctrl";
            case "Left Alt": return "Alt";
        }
        return key.Length > 6 ? key.Substring(0, 6) : key;
    }

    /// <summary>Destroys in Play Mode, or immediately in the editor.</summary>
    public static void Destroy(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Object.Destroy(o);
        else Object.DestroyImmediate(o);
    }

    /// <summary>Places a rect at an anchor / pivot with a size and an offset.</summary>
    public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        return rt;
    }
}
