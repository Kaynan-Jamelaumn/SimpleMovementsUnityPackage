using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// "Rent Room 3 for 3 nights for 75 g?" with Confirm and Cancel, over a housing window (it dims the window and takes its
/// clicks). While it is open the keyboard and the gamepad are on it: Confirm is selected, so Enter (or the gamepad's
/// south button: the UI's Submit) presses Confirm - one press, one answer: the press that opened the question cannot
/// also answer it, nor press the button behind it again, and a question asked by Confirm is not answered by the same
/// press. Left and right move between its two buttons only. Escape - handled by the window, which closes the question
/// before itself - and Cancel cancel. When it closes, the selection goes back where it was. Built from code by
/// <see cref="Build"/>.
/// </summary>
[DisallowMultipleComponent]
public class HousingConfirm : MonoBehaviour
{
    private TextMeshProUGUI messageText;
    private Button confirmButton, cancelButton;
    private TextMeshProUGUI confirmLabel, cancelLabel;
    private Action onConfirm, onCancel;
    /// <summary>What had the keyboard's selection before the question (it gets it back).</summary>
    private GameObject selectedBefore;

    public bool IsOpen => gameObject.activeSelf && onConfirm != null;

    /// <summary>The question over <paramref name="window"/> (hidden until asked).</summary>
    public static HousingConfirm Build(Transform window)
    {
        Image overlay = UITheme.Panel("Confirm", window, new Color(0f, 0f, 0f, 0.62f), UITheme.RadiusLarge);
        InventoryUIFactory.Stretch(overlay.rectTransform);
        overlay.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var dialog = overlay.gameObject.AddComponent<HousingConfirm>();

        Image box = UITheme.Panel("Box", overlay.transform, UITheme.SurfaceRaised, UITheme.RadiusLarge);
        RectTransform b = box.rectTransform;
        b.anchorMin = b.anchorMax = b.pivot = new Vector2(0.5f, 0.5f);
        b.sizeDelta = new Vector2(640f, 0f);
        InventoryUIFactory.Vertical(box.gameObject, 24, 18f, true);
        dialog.messageText = UITheme.Label(box.transform, "", 18f, UITheme.Text, TextAlignmentOptions.Center);
        dialog.messageText.richText = false;
        RectTransform buttons = UITheme.Row("Buttons", box.transform, 44f, 12f);
        buttons.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        dialog.cancelButton = UITheme.Button("Cancel", buttons, "Cancel", dialog.Close, UITheme.ButtonStyle.Normal, 0f, 44f);
        dialog.cancelLabel = dialog.cancelButton.GetComponentInChildren<TextMeshProUGUI>(true);
        dialog.confirmButton = UITheme.Button("Confirm", buttons, "Confirm", dialog.Confirm, UITheme.ButtonStyle.Primary, 0f, 44f);
        dialog.confirmLabel = dialog.confirmButton.GetComponentInChildren<TextMeshProUGUI>(true);
        // The keyboard and the gamepad move between the two buttons only: nothing leads to the window behind.
        dialog.cancelButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = dialog.confirmButton };
        dialog.confirmButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = dialog.cancelButton };
        overlay.gameObject.SetActive(false);
        return dialog;
    }

    /// <summary>
    /// Asks; <paramref name="confirm"/> runs on Confirm, <paramref name="cancel"/> on anything else that closes it (its
    /// button says <paramref name="cancelText"/>, "Cancel" by default).
    /// </summary>
    public void Show(string message, string confirmText, Action confirm, bool danger = false, Action cancel = null, string cancelText = null)
    {
        if (confirm == null) return;
        Action previous = onCancel;
        onConfirm = null;
        onCancel = null;
        previous?.Invoke(); // a question still open is answered "no"
        onConfirm = confirm;
        onCancel = cancel;
        if (messageText != null) messageText.text = HousingUI.Clean(message);
        if (confirmButton != null) UITheme.Style(confirmButton, danger ? UITheme.ButtonStyle.Danger : UITheme.ButtonStyle.Primary);
        if (confirmLabel != null) confirmLabel.text = HousingUI.Clean(string.IsNullOrEmpty(confirmText) ? "Confirm" : confirmText);
        if (cancelLabel != null) cancelLabel.text = HousingUI.Clean(string.IsNullOrEmpty(cancelText) ? "Cancel" : cancelText);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        TakeSelection();
    }

    public void Confirm()
    {
        Action a = onConfirm;
        onCancel = null;
        Close();
        a?.Invoke();
    }

    public void Close()
    {
        Action c = onCancel;
        onConfirm = null;
        onCancel = null;
        if (gameObject.activeSelf) gameObject.SetActive(false);
        GiveSelectionBack();
        c?.Invoke();
    }

    /// <summary>Confirm gets the keyboard's selection (what had it is remembered, unless it was this question's own).</summary>
    private void TakeSelection()
    {
        EventSystem es = EventSystem.current;
        if (es == null || es.alreadySelecting || confirmButton == null) return;
        GameObject current = es.currentSelectedGameObject;
        if (current == null || !current.transform.IsChildOf(transform)) selectedBefore = current;
        if (current != confirmButton.gameObject) es.SetSelectedGameObject(confirmButton.gameObject);
    }

    /// <summary>The selection goes back to what had it before the question - only when the question still holds it.</summary>
    private void GiveSelectionBack()
    {
        GameObject back = selectedBefore;
        selectedBefore = null;
        EventSystem es = EventSystem.current;
        if (es == null || es.alreadySelecting) return;
        GameObject current = es.currentSelectedGameObject;
        if (current != null && current.activeInHierarchy && !current.transform.IsChildOf(transform)) return; // chosen elsewhere meanwhile
        es.SetSelectedGameObject(back != null && back.activeInHierarchy ? back : null);
    }

    private void Update()
    {
        // While the question is open the keyboard and the gamepad stay on it: a click beside its buttons, or a selection
        // that went to the window behind it, comes back to Confirm. A selection in another window (the chat) is left alone.
        if (onConfirm == null || confirmButton == null) return;
        EventSystem es = EventSystem.current;
        if (es == null || es.alreadySelecting) return;
        GameObject current = es.currentSelectedGameObject;
        Transform window = transform.parent != null ? transform.parent : transform;
        if (current == null || !current.activeInHierarchy || (current.transform.IsChildOf(window) && !current.transform.IsChildOf(transform)))
            es.SetSelectedGameObject(confirmButton.gameObject);
    }
}
