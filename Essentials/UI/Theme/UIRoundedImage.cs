using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Keeps an image's rounded corners: the sprite is generated in memory (<see cref="UITheme.RoundedSprite"/>), so
/// scene-built UI gets it back every time it loads.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class UIRoundedImage : MonoBehaviour
{
    [Tooltip("Corner radius in canvas pixels.")]
    [Range(1, 64)] public int radius = UITheme.Radius;

    private void Awake() => Apply();

    private void OnEnable() => Apply();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (isActiveAndEnabled)
            Apply();
    }
#endif

    public void Apply()
    {
        var img = GetComponent<Image>();
        if (img == null)
            return;
        img.sprite = UITheme.RoundedSprite(radius);
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;
        img.fillCenter = true;
    }
}
