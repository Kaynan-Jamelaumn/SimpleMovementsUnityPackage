using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Turns the preview model when the picture is dragged (and frames the face on a double click).</summary>
public class CharacterPreviewDrag : MonoBehaviour, IDragHandler, IPointerClickHandler, IScrollHandler
{
    public CharacterPreviewStage stage;
    public float degreesPerPixel = 0.45f;
    private float face;

    public void OnDrag(PointerEventData eventData)
    {
        if (stage != null) stage.Rotate(-eventData.delta.x * degreesPerPixel);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (stage == null || eventData.clickCount < 2) return;
        face = face > 0.5f ? 0f : 1f;
        stage.Frame(face);
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (stage == null) return;
        face = Mathf.Clamp01(face + eventData.scrollDelta.y * 0.15f);
        stage.Frame(face);
    }

    /// <summary>Sets the framing from a button (keeps scroll and double-click in step).</summary>
    public void SetFace(float amount)
    {
        face = Mathf.Clamp01(amount);
        if (stage != null) stage.Frame(face);
    }
}
