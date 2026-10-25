using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// A thin accent bar on the left of a tab or list button, shown while it is the chosen one
/// (<see cref="UITheme.SetSelected"/>, <see cref="UITabGroup"/>).
/// </summary>
[DisallowMultipleComponent]
public class UIMarker : MonoBehaviour
{
    [SerializeField] private Image bar;
    [SerializeField] private Image glow;

    public void SetOn(bool on)
    {
        if (bar == null)
        {
            bar = InventoryUIFactory.Panel("Marker", transform, UITheme.Accent);
            UITheme.Round(bar, 2);
            bar.raycastTarget = false;
            RectTransform rt = bar.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.2f);
            rt.anchorMax = new Vector2(0f, 0.8f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(4f, 0f);
            rt.anchoredPosition = new Vector2(4f, 0f);
            var le = bar.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }
        if (glow == null)
        {
            glow = InventoryUIFactory.Panel("Glow", transform, UITheme.AccentSoft);
            UITheme.Round(glow, UITheme.Radius);
            glow.raycastTarget = false;
            InventoryUIFactory.Stretch(glow.rectTransform);
            glow.transform.SetAsFirstSibling();
            var le = glow.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }
        bar.enabled = on;
        glow.enabled = on;
    }
}
