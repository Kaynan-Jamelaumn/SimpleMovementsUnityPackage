using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// "Buy 3 × Iron Sword for 450 g?" with Confirm and Cancel, shown over the shop window. Enter confirms; Escape (handled by
/// the window) cancels. Every field is optional for a prefab of your own.
/// </summary>
[DisallowMultipleComponent]
public class MerchantConfirmDialog : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TextMeshProUGUI confirmLabel;
    [SerializeField] private Button cancelButton;

    private Action onConfirm;
    private int openedFrame = -1;
    private bool wired;

    public bool IsOpen => gameObject.activeSelf && onConfirm != null;

    public void Configure(TextMeshProUGUI message, Button confirm, TextMeshProUGUI confirmText, Button cancel)
    {
        messageText = message;
        confirmButton = confirm;
        confirmLabel = confirmText;
        cancelButton = cancel;
    }

    private void Wire()
    {
        if (wired)
            return;
        wired = true;
        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(Close);
    }

    public void Show(string message, string confirmText, Action confirm)
    {
        Wire();
        onConfirm = confirm;
        openedFrame = Time.frameCount;
        if (messageText != null) messageText.text = message ?? "";
        if (confirmLabel != null) confirmLabel.text = string.IsNullOrEmpty(confirmText) ? "Confirm" : confirmText;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Confirm()
    {
        Action a = onConfirm;
        Close();
        a?.Invoke();
    }

    public void Close()
    {
        onConfirm = null;
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    private void Update()
    {
        if (onConfirm == null || Time.frameCount == openedFrame)
            return;
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        if ((kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) || (pad != null && pad.buttonSouth.wasPressedThisFrame))
            Confirm();
    }
}
