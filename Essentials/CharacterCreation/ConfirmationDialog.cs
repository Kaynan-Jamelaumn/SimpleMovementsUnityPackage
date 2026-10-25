using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ConfirmationDialog : MonoBehaviour
{
    [Header("Dialog Elements")]
    [SerializeField] private GameObject dialogPanel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("Audio Integration")]
    [SerializeField] private CharacterCreationUI mainUI;

    private System.Action onConfirm;
    private System.Action onCancel;

    private void Awake()
    {
        if (mainUI == null)
            mainUI = FindAnyObjectByType<CharacterCreationUI>();

        if (confirmButton != null)
            confirmButton.onClick.AddListener(Confirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(Cancel);

        Hide();
    }

    public void Show(string message, System.Action confirmAction, System.Action cancelAction = null)
    {
        if (messageText != null)
            messageText.text = message;

        onConfirm = confirmAction;
        onCancel = cancelAction;

        if (dialogPanel != null)
            dialogPanel.SetActive(true);

        // Play sound when showing dialog
        if (mainUI != null)
            mainUI.PlayButtonClickSound();
    }

    public void Hide()
    {
        if (dialogPanel != null)
            dialogPanel.SetActive(false);

        onConfirm = null;
        onCancel = null;
    }

    private void Confirm()
    {
        if (mainUI != null)
            mainUI.PlayButtonClickSound();

        onConfirm?.Invoke();
        Hide();
    }

    private void Cancel()
    {
        if (mainUI != null)
            mainUI.PlayButtonClickSound();

        onCancel?.Invoke();
        Hide();
    }
}
