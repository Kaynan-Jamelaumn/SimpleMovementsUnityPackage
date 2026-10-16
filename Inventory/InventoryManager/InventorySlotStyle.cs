using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The look of a slot built by the Inventory UI Builder (themed look): a brighter frame under the pointer, an accent
/// outline on the selected hotbar slot, the hotbar key number in the corner, and the slot type ("Helmet", "Ring")
/// written faintly in an empty equipment slot. Purely visual: it reads the slot's state and changes nothing in it.
/// </summary>
[DisallowMultipleComponent]
public class InventorySlotStyle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("The slot's background (its own Image when empty).")]
    [SerializeField] private Image frame;
    [Tooltip("Outline shown on the selected hotbar slot.")]
    [SerializeField] private Image selection;
    [Tooltip("Slot type written in an empty equipment slot.")]
    [SerializeField] private TextMeshProUGUI typeHint;
    [Tooltip("Hotbar key number.")]
    [SerializeField] private TextMeshProUGUI keyNumber;
    [Tooltip("How much brighter the frame is under the pointer.")]
    [SerializeField, Range(0f, 1f)] private float hoverBrighten = 0.35f;

    private InventorySlot slot;
    private Color baseColor;
    private bool hovering;
    private bool lastSelected, lastEmpty = true, lastHover;
    private int lastNumber = -1;
    private bool started;

    /// <summary>Sets the parts (the builder; also usable from code).</summary>
    public void Configure(Image frameImage, Image selectionImage, TextMeshProUGUI hint, TextMeshProUGUI number)
    {
        frame = frameImage;
        selection = selectionImage;
        typeHint = hint;
        keyNumber = number;
    }

    /// <summary>A new resting colour for the frame (another look, e.g. the Player HUD's).</summary>
    public void SetBaseColor(Color color)
    {
        baseColor = color;
        if (frame == null) frame = GetComponent<Image>();
        if (frame != null) frame.color = color;
        started = false; // redraw next frame
    }

    private void Awake()
    {
        slot = GetComponent<InventorySlot>();
        if (frame == null) frame = GetComponent<Image>();
        if (frame != null) baseColor = frame.color;
    }

    private void OnEnable()
    {
        started = false; // refresh everything next frame
        hovering = false;
    }

    public void OnPointerEnter(PointerEventData eventData) => hovering = true;

    public void OnPointerExit(PointerEventData eventData) => hovering = false;

    private void LateUpdate()
    {
        if (slot == null)
            return;
        bool selected = slot.IsHotbarSlot && transform.localScale.x > 1.01f; // the hotbar scales its selected slot
        bool empty = slot.heldItem == null;
        int number = slot.IsHotbarSlot ? transform.GetSiblingIndex() + 1 : 0;
        // An item put in the slot becomes its last child: the key number goes back above it.
        if (keyNumber != null && keyNumber.enabled && keyNumber.transform.GetSiblingIndex() != transform.childCount - 1)
            keyNumber.transform.SetAsLastSibling();
        if (started && selected == lastSelected && empty == lastEmpty && hovering == lastHover && number == lastNumber)
            return;
        started = true;
        lastSelected = selected;
        lastEmpty = empty;
        lastHover = hovering;
        lastNumber = number;

        if (frame != null)
        {
            Color c = baseColor;
            if (hovering || selected)
                c = Color.Lerp(baseColor, new Color(1f, 1f, 1f, Mathf.Max(baseColor.a, 0.35f)), selected ? hoverBrighten * 0.6f : hoverBrighten);
            frame.color = c;
        }
        if (selection != null)
            selection.enabled = selected;
        if (typeHint != null)
        {
            bool show = empty && !slot.IsHotbarSlot && slot.SlotType != SlotType.Common;
            typeHint.enabled = show;
            if (show)
                typeHint.text = SlotTypeHelper.GetDisplayName(slot.SlotType);
        }
        if (keyNumber != null)
        {
            keyNumber.enabled = number > 0 && number <= 9;
            if (keyNumber.enabled)
                keyNumber.text = number.ToString();
        }
    }
}
