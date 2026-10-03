using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Shows a character sheet row's details while the pointer is over it.</summary>
public class CharacterSheetHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private CharacterSheetMenu sheet;
    private string text;

    public void Setup(CharacterSheetMenu owner, string hoverText)
    {
        sheet = owner;
        text = hoverText;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (sheet != null) sheet.ShowHover(text);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (sheet != null) sheet.HideHover();
    }

    private void OnDisable()
    {
        if (sheet != null) sheet.HideHover();
    }
}
