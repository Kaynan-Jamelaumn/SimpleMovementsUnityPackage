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
        if (mainUI != null)
            mainUI.PlayButtonClickSound();

        var traitDetailPanel = GameObject.Find("TraitDetailPanel");
        if (traitDetailPanel != null)
            traitDetailPanel.SetActive(false);
    }

    // Reset entire character creation - call this from Reset button
    public void ResetCharacterCreation()
    {
        if (mainUI != null)
        {
            mainUI.PlayButtonClickSound();
            mainUI.ResetCharacterCreation();
        }
    }

    // Cancel character creation - call this from Cancel button
    public void CancelCharacterCreation()
    {
        if (mainUI != null)
        {
            mainUI.PlayButtonClickSound();
            mainUI.ResetCharacterCreation();
            mainUI.gameObject.SetActive(false);
        }
    }

    // Generic button click sound for any UI button
    public void PlayButtonClick()
    {
        if (mainUI != null)
            mainUI.PlayButtonClickSound();
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

    [Header("Audio Integration")]
    [SerializeField] private CharacterCreationUI mainUI;

    private System.Action onConfirm;
    private System.Action onCancel;

    private void Awake()
    {
        if (mainUI == null)
            mainUI = FindFirstObjectByType<CharacterCreationUI>();

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

// Tooltip system for showing trait information on hover
public class TraitTooltip : MonoBehaviour
{
    [Header("Tooltip Settings")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TMP_Text tooltipText;
    [SerializeField] private float showDelay = 0.5f;

    [Header("Audio Integration")]
    [SerializeField] private CharacterCreationUI mainUI;
    [SerializeField] private bool playHoverSound = false;
    [SerializeField] private string hoverSoundName = "UI_Hover";

    private Coroutine showCoroutine;

    private void Awake()
    {
        if (mainUI == null)
            mainUI = FindFirstObjectByType<CharacterCreationUI>();
    }

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

        // Play hover sound if enabled
        if (playHoverSound && mainUI != null && !string.IsNullOrEmpty(hoverSoundName))
        {
            mainUI.PlayUISound(hoverSoundName);
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

// Enhanced audio feedback for UI interactions - now works with SoundManager
public class UIAudioFeedback : MonoBehaviour
{
    [Header("Sound Names (must exist in SoundManager)")]
    [SerializeField] private string buttonClickSoundName = "UI_ButtonClick";
    [SerializeField] private string traitSelectSoundName = "UI_TraitSelect";
    [SerializeField] private string traitAddSoundName = "UI_TraitAdd";
    [SerializeField] private string traitRemoveSoundName = "UI_TraitRemove";
    [SerializeField] private string characterCreateSoundName = "UI_CharacterCreate";
    [SerializeField] private string errorSoundName = "UI_Error";
    [SerializeField] private string classSelectSoundName = "UI_ClassSelect";

    [Header("Fallback Audio Clips (if SoundManager unavailable)")]
    [SerializeField] private AudioClip buttonClickClip;
    [SerializeField] private AudioClip traitSelectClip;
    [SerializeField] private AudioClip traitAddClip;
    [SerializeField] private AudioClip traitRemoveClip;
    [SerializeField] private AudioClip characterCreateClip;
    [SerializeField] private AudioClip errorClip;
    [SerializeField] private AudioClip classSelectClip;

    [Header("Settings")]
    [SerializeField] private float fallbackVolume = 1f;
    [SerializeField] private bool enableDebugLogs = false;

    public void PlayButtonClick()
    {
        PlaySound(buttonClickSoundName, buttonClickClip);
    }

    public void PlayTraitSelect()
    {
        PlaySound(traitSelectSoundName, traitSelectClip);
    }

    public void PlayTraitAdd()
    {
        PlaySound(traitAddSoundName, traitAddClip);
    }

    public void PlayTraitRemove()
    {
        PlaySound(traitRemoveSoundName, traitRemoveClip);
    }

    public void PlayCharacterCreate()
    {
        PlaySound(characterCreateSoundName, characterCreateClip);
    }

    public void PlayError()
    {
        PlaySound(errorSoundName, errorClip);
    }

    public void PlayClassSelect()
    {
        PlaySound(classSelectSoundName, classSelectClip);
    }

    private void PlaySound(string soundName, AudioClip fallbackClip)
    {
        // Try SoundManager first
        if (!string.IsNullOrEmpty(soundName) && SoundManager.Instance != null)
        {
            try
            {
                SoundManager.Instance.PlayUISound(soundName);
                if (enableDebugLogs)
                    Debug.Log($"[UIAudioFeedback] Played sound via SoundManager: {soundName}");
                return;
            }
            catch (System.Exception e)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[UIAudioFeedback] SoundManager failed to play {soundName}: {e.Message}");
            }
        }

        // Fallback to AudioSource.PlayClipAtPoint
        if (fallbackClip != null)
        {
            var cameraTransform = Camera.main != null ? Camera.main.transform : transform;
            AudioSource.PlayClipAtPoint(fallbackClip, cameraTransform.position, fallbackVolume);

            if (enableDebugLogs)
                Debug.Log($"[UIAudioFeedback] Played fallback clip: {fallbackClip.name}");
        }
        else
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[UIAudioFeedback] No audio available for: {soundName}");
        }
    }

    // Utility method for testing all sounds
    [ContextMenu("Test All Sounds")]
    public void TestAllSounds()
    {
        StartCoroutine(TestSoundsSequence());
    }

    private System.Collections.IEnumerator TestSoundsSequence()
    {
        var sounds = new System.Collections.Generic.Dictionary<string, System.Action>
        {
            { "Button Click", PlayButtonClick },
            { "Class Select", PlayClassSelect },
            { "Trait Select", PlayTraitSelect },
            { "Trait Add", PlayTraitAdd },
            { "Trait Remove", PlayTraitRemove },
            { "Character Create", PlayCharacterCreate },
            { "Error", PlayError }
        };

        Debug.Log("[UIAudioFeedback] Testing all sounds...");

        foreach (var soundPair in sounds)
        {
            Debug.Log($"[UIAudioFeedback] Testing: {soundPair.Key}");
            soundPair.Value.Invoke();
            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log("[UIAudioFeedback] Sound testing complete!");
    }
}