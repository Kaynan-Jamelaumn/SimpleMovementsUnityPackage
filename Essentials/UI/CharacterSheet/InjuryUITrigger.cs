using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// One element of the Health page (a body part, an injury row, the whole-body card), for the mouse and for a keyboard
/// or gamepad alike: pointing at it or moving the selection onto it shows its details, clicking it or pressing Submit
/// selects it or opens its treatments, and a medical item dragged from the bag can be dropped on it. Built by
/// <see cref="InjuryHealthPanel"/> next to a Selectable, so the UI's navigation reaches it.
/// </summary>
public class InjuryUITrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
    ISelectHandler, IDeselectHandler, ISubmitHandler
{
    /// <summary>The hover text (asked each time it is shown, so it is always current).</summary>
    public Func<string> hover;
    /// <summary>Clicked (the event says which button), or Submit pressed on it (null: keyboard or gamepad).</summary>
    public Action<PointerEventData> click;
    /// <summary>The pointer is over it or the selection is on it (true), or neither any more (false).</summary>
    public Action<bool> focus;
    /// <summary>The selection moved onto it with a keyboard or a gamepad (a list scrolls it into view).</summary>
    public Action navigatedTo;
    /// <summary>The page showing the hover text.</summary>
    public InjuryHealthPanel panel;
    /// <summary>The body part it stands for (a dropped medical item treats it).</summary>
    public InjuryPartState part;
    /// <summary>The injury it stands for (rows), or null (a part, a missing limb's row).</summary>
    public Injury injury;

    private bool pointerOver, selected;

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerOver = true;
        if (panel != null && hover != null)
            panel.ShowHover(hover(), null);
        focus?.Invoke(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerOver = false;
        if (panel != null)
            panel.HideHover();
        if (!selected)
            focus?.Invoke(false);
    }

    public void OnPointerClick(PointerEventData eventData) => click?.Invoke(eventData);

    public void OnSelect(BaseEventData eventData)
    {
        // Selected by a click: the pointer already shows the details where the mouse is.
        if (eventData is PointerEventData)
            return;
        selected = true;
        navigatedTo?.Invoke();
        if (panel != null && hover != null)
            panel.ShowHover(hover(), transform as RectTransform);
        focus?.Invoke(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        bool was = selected;
        selected = false;
        if (!was)
            return;
        if (panel != null && !pointerOver)
            panel.HideHover();
        if (!pointerOver)
            focus?.Invoke(false);
    }

    public void OnSubmit(BaseEventData eventData) => click?.Invoke(null);

    private void OnDisable()
    {
        if (panel != null && (pointerOver || selected))
            panel.HideHover();
        pointerOver = selected = false;
    }
}
