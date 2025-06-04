using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Helper script for additional Character Creation UI functionality
public class CharacterCreationUIHelpers : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterCreationUI mainUI;

    private void Awake()
    {
        if (mainUI == null)
            mainUI = FindFirstObjectByType<CharacterCreationUI>();
    }

    // Close trait details panel - call this from Close button
    public void CloseTraitDetails()
    {
        var traitDetailPanel = GameObject.Find("TraitDetailPanel");
        if (traitDetailPanel != null)
            traitDetailPanel.SetActive(false);
    }

    // Reset entire character creation - call this from Reset button
    public void ResetCharacterCreation()
    {
        if (mainUI != null)
            mainUI.ResetCharacterCreation();
    }

    // Cancel character creation - call this from Cancel button
    public void CancelCharacterCreation()
    {
        if (mainUI != null)
        {
            mainUI.ResetCharacterCreation();
            mainUI.gameObject.SetActive(false);
        }
    }
}

// Simple confirmation dialog for character creation actions
public class ConfirmationDialog : MonoBehaviour
{
    [Header("Dialog Elements")]
    [SerializeField] private GameObject dialogPanel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private System.Action onConfirm;
    private System.Action onCancel;

    private void Awake()
    {
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
        onConfirm?.Invoke();
        Hide();
    }

    private void Cancel()
    {
        onCancel?.Invoke();
        Hide();
    }
}

// Tooltip system for showing trait information on hover
public class TraitTooltip : MonoBehaviour
{
    [Header("Tooltip Settings")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TMP_Text tooltipText;
    [SerializeField] private float showDelay = 0.5f;

    private Coroutine showCoroutine;

    public void ShowTooltip(string text, Vector3 position)
    {
        if (showCoroutine != null)
            StopCoroutine(showCoroutine);

        showCoroutine = StartCoroutine(ShowTooltipDelayed(text, position));
    }

    public void HideTooltip()
    {
        if (showCoroutine != null)
        {
            StopCoroutine(showCoroutine);
            showCoroutine = null;
        }

        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }

    private System.Collections.IEnumerator ShowTooltipDelayed(string text, Vector3 position)
    {
        yield return new WaitForSeconds(showDelay);

        if (tooltipText != null)
            tooltipText.text = text;

        if (tooltipPanel != null)
        {
            tooltipPanel.transform.position = position;
            tooltipPanel.SetActive(true);
        }

        showCoroutine = null;
    }
}

// Extensions for UI animation and visual effects
public static class UIAnimationExtensions
{
    // Simple fade in animation for UI panels
    public static void FadeIn(this CanvasGroup canvasGroup, float duration = 0.3f)
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = 0f;
        canvasGroup.gameObject.SetActive(true);

        var component = canvasGroup.GetComponent<MonoBehaviour>();
        if (component != null)
            component.StartCoroutine(FadeCoroutine(canvasGroup, 0f, 1f, duration));
    }

    // Simple fade out animation for UI panels
    public static void FadeOut(this CanvasGroup canvasGroup, float duration = 0.3f, bool deactivateAfter = true)
    {
        if (canvasGroup == null) return;

        var component = canvasGroup.GetComponent<MonoBehaviour>();
        if (component != null)
            component.StartCoroutine(FadeCoroutine(canvasGroup, canvasGroup.alpha, 0f, duration, deactivateAfter));
    }

    private static System.Collections.IEnumerator FadeCoroutine(CanvasGroup canvasGroup, float startAlpha, float endAlpha, float duration, bool deactivateAfter = false)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = endAlpha;

        if (deactivateAfter && endAlpha <= 0f)
            canvasGroup.gameObject.SetActive(false);
    }
}

// Audio feedback for UI interactions
public class UIAudioFeedback : MonoBehaviour
{
    [Header("Audio Clips")]
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip traitSelectSound;
    [SerializeField] private AudioClip traitAddSound;
    [SerializeField] private AudioClip traitRemoveSound;
    [SerializeField] private AudioClip characterCreateSound;
    [SerializeField] private AudioClip errorSound;

    [Header("Settings")]
    [SerializeField] private float volume = 1f;

    public void PlayButtonClick()
    {
        PlaySound(buttonClickSound);
    }

    public void PlayTraitSelect()
    {
        PlaySound(traitSelectSound);
    }

    public void PlayTraitAdd()
    {
        PlaySound(traitAddSound);
    }

    public void PlayTraitRemove()
    {
        PlaySound(traitRemoveSound);
    }

    public void PlayCharacterCreate()
    {
        PlaySound(characterCreateSound);
    }

    public void PlayError()
    {
        PlaySound(errorSound);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayUISound(clip.name);
        }
        else if (clip != null)
        {
            // Fallback to AudioSource.PlayClipAtPoint if no SoundManager
            AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position, volume);
        }
    }
}