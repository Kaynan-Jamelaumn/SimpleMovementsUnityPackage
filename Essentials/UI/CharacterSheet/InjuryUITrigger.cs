using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Pointer events of one element of the Health page (a body part, an injury row): hovering shows its details, clicking
/// selects it or opens its treatments. Built by <see cref="InjuryHealthPanel"/>.
/// </summary>
public class InjuryUITrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    /// <summary>The hover text (asked each time the pointer enters, so it is always current).</summary>
    public Func<string> hover;
    /// <summary>Clicked (the event says which button).</summary>
    public Action<PointerEventData> click;
    /// <summary>The page showing the hover text.</summary>
    public InjuryHealthPanel panel;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (panel != null && hover != null)
            panel.ShowHover(hover());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (panel != null)
            panel.HideHover();
    }

    public void OnPointerClick(PointerEventData eventData) => click?.Invoke(eventData);

    private void OnDisable()
    {
        if (panel != null)
            panel.HideHover();
    }
}
